using System.Diagnostics;
using System.Globalization;
using ManuscriptPipeline.Services;

namespace PaperRoute.ViewModels.Recovery;

// The recovery drafts of one library (start-up design 2.5, build 6 D1): the
// only caller of the core's draft store. A draft is the open manuscript
// page's unsaved work, kept when PaperRoute closes without being able to ask
// (a shutdown, an error) and offered at the next start.
//
// The slots. The main slot is the core's own file,
// data\recovery\manuscript-page-draft.json, the one file PaperRoute 1.0.x
// reads and writes. A waiting slot is the same file under
// data\recovery\waiting-<id>\ (then waiting-<id>-2\, waiting-<id>-3\ and so
// on when that one is taken too), used when the main slot already holds a
// draft that is not this session's: so a new draft never replaces one nobody
// has answered, and a page's draft is never refused. At start every slot is
// read; 1.0.x sees only the main slot.
//
// The rules (each tested by RecoveryDraftsTests):
// 1. Nothing is offered until a page can open a draft (OpenWith), and
//    nothing is deleted that this session does not own.
// 2. A write goes to this session's opened or written draft of that
//    manuscript, else to the main slot when it is empty or holds this
//    session's opened or written draft, else to the manuscript's first free
//    waiting slot. A draft nobody answered (an earlier one of the same
//    manuscript included), a discarded one (Undo keeps it until close) and a
//    file that cannot be read are never written over.
// 3. Adopting a draft (the page opened it) keeps its file until the page
//    saves or discards.
// 4. Only a draft this session wrote, adopted or discarded is deleted here.
// 5. A draft whose manuscript is not in the library is kept and not offered.
// 6. A draft offered after this session saved its manuscript says so.
// 7. A write never throws: a failure is logged and returns false.
// 8. A discarded draft is no longer offered, but its file stays until
//    PaperRoute closes (DeleteThisSessionsDraft), so Undo can bring it back;
//    after a crash it is offered again.
// Two drafts of one manuscript (1.0.x can write the main slot while a waiting
// slot holds the same manuscript): the newer is offered first, the older
// stays untouched and is offered once the newer is adopted or discarded.
public sealed class RecoveryDrafts : IRecoveryDrafts
{
    // A waiting slot's folder under data\recovery: this, then the
    // manuscript's id as 32 hexadecimal digits, then for the second and later
    // slots of one manuscript a hyphen and the slot's number (2, 3 and so on).
    public const string WaitingSlotPrefix = "waiting-";

    private readonly string _dataFolder;
    private readonly Lock _gate = new();
    private readonly List<Kept> _kept = [];
    private readonly HashSet<Guid> _library = [];
    private readonly HashSet<Guid> _savedThisSession = [];
    private Func<Guid, Task<bool>>? _open;
    private long _discards;

    // libraryDataFolder: the library's data folder, the one whose recovery
    // folder holds the main slot (StorageLocations.LibraryDataFolder() for a
    // library on disk; a folder of its own in a test).
    public RecoveryDrafts(string libraryDataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryDataFolder);
        if (!Path.IsPathFullyQualified(libraryDataFolder))
        {
            throw new ArgumentException("The library's data folder must be a full path.", nameof(libraryDataFolder));
        }

        _dataFolder = libraryDataFolder;
    }

    private enum Holder
    {
        // An earlier session kept it and nobody has answered it yet.
        Earlier,

        // This session wrote it.
        Written,

        // This session's page opened it.
        Adopted,

        // Discarded in this session: no longer offered, deleted at close.
        Discarded,
    }

    // Rule 6's sentence.
    public static string SavedAfterText(string title) =>
        $"You saved '{title}' after these changes were kept; opening them shows the older text as unsaved changes.";

    // The drafts waiting for an answer, one per manuscript (its newest), the
    // newest first. Empty until a page can open a draft (rule 1).
    public IReadOnlyList<DraftOffer> Offers
    {
        get
        {
            lock (_gate)
            {
                if (_open is null)
                {
                    return [];
                }

                return [.. _kept
                    .Select(kept => kept.Id)
                    .Distinct()
                    .Select(NewestPending)
                    .OfType<Kept>()
                    .OrderByDescending(kept => kept.Draft.WrittenAtUtc)
                    .ThenBy(kept => kept.Id)
                    .Select(kept => new DraftOffer(
                        kept.Id,
                        kept.Draft.Manuscript.Title,
                        kept.Draft.WrittenAtUtc,
                        _savedThisSession.Contains(kept.Id) ? SavedAfterText(kept.Draft.Manuscript.Title) : null))];
            }
        }
    }

    // Reads every slot once, at start, after the library has loaded: the ids
    // are the manuscripts in the library (rule 5). A slot that cannot be read
    // is left where it is and is never written over.
    public void ReadAtStart(IReadOnlyCollection<Guid> manuscriptIds)
    {
        ArgumentNullException.ThrowIfNull(manuscriptIds);
        lock (_gate)
        {
            _library.Clear();
            _library.UnionWith(manuscriptIds);
            foreach (string folder in SlotFolders())
            {
                if (_kept.Any(kept => kept.IsIn(folder)))
                {
                    continue;
                }

                try
                {
                    if (ReadSlot(folder) is { Manuscript: not null } draft && draft.ManuscriptId != Guid.Empty)
                    {
                        _kept.Add(new Kept(folder, draft, Holder.Earlier));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    Log($"a kept draft could not be read and was left where it is ({ex.GetType().Name}: {ex.Message}).");
                }
            }
        }
    }

    // What opens a manuscript when its offer is taken (the shell registers
    // the Manuscript page's route). Until then nothing is offered (rule 1).
    public void OpenWith(Func<Guid, Task<bool>> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        lock (_gate)
        {
            _open = open;
        }
    }

    public RecoveryDraft? PendingDraftFor(Guid manuscriptId)
    {
        lock (_gate)
        {
            return NewestPending(manuscriptId)?.Draft;
        }
    }

    public void Adopt(Guid manuscriptId)
    {
        lock (_gate)
        {
            if (NewestPending(manuscriptId) is { } kept)
            {
                kept.Holder = Holder.Adopted;
            }
        }
    }

    public void PageSaved(Guid manuscriptId)
    {
        lock (_gate)
        {
            _savedThisSession.Add(manuscriptId);
            DeleteLive(manuscriptId);
        }
    }

    public void PageDiscarded(Guid manuscriptId)
    {
        lock (_gate)
        {
            DeleteLive(manuscriptId);
        }
    }

    // Discard on an offer: the manuscript's newest waiting draft is no longer
    // offered, and its file stays until PaperRoute closes (rule 8). False
    // when nothing was waiting.
    public bool Discard(Guid manuscriptId)
    {
        lock (_gate)
        {
            if (NewestPending(manuscriptId) is not { } kept)
            {
                return false;
            }

            kept.Holder = Holder.Discarded;
            kept.DiscardOrder = ++_discards;
            return true;
        }
    }

    // Undo of a Discard: the manuscript's last discarded draft waits for an
    // answer again. False when there is none, as after a close.
    public bool Undo(Guid manuscriptId)
    {
        lock (_gate)
        {
            if (_kept.Where(kept => kept.Id == manuscriptId && kept.Holder == Holder.Discarded)
                    .MaxBy(kept => kept.DiscardOrder) is not { } kept)
            {
                return false;
            }

            kept.Holder = Holder.Earlier;
            return true;
        }
    }

    // Keeps the current page's unsaved work, when there is no one to ask (a
    // shutdown, an error). True when a draft was written; false when there
    // is no page, nothing is unsaved, or the write failed, which is logged
    // (rule 7). Never throws.
    public bool WriteFrom(IDraftSource? current, string reason)
    {
        if (current is null)
        {
            return false;
        }

        RecoveryDraft? draft;
        try
        {
            draft = current.DraftForRecovery(reason ?? string.Empty);
        }
        catch (Exception ex)
        {
            Log($"the page could not give its unsaved work as a draft ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }

        if (draft is null)
        {
            return false;
        }

        if (draft.Manuscript is null || draft.ManuscriptId == Guid.Empty)
        {
            Log("the page gave a draft with no manuscript; nothing was written.");
            return false;
        }

        lock (_gate)
        {
            try
            {
                string folder = SlotFor(draft.ManuscriptId);
                WriteSlot(folder, draft);
                _kept.RemoveAll(kept => kept.IsIn(folder));
                _kept.Add(new Kept(folder, draft, Holder.Written));
                return true;
            }
            catch (Exception ex)
            {
                Log($"the draft could not be written ({ex.GetType().Name}: {ex.Message}).");
                return false;
            }
        }
    }

    // Deletes every file this session owns: written, adopted or discarded
    // (rule 4). A draft an earlier session kept and nobody answered stays.
    public void DeleteThisSessionsDraft()
    {
        lock (_gate)
        {
            foreach (Kept kept in _kept.Where(kept => kept.IsOwn).ToList())
            {
                if (TryDelete(kept))
                {
                    _kept.Remove(kept);
                }
            }
        }
    }

    // A manuscript's newest draft waiting for an answer, or null.
    private Kept? NewestPending(Guid manuscriptId) =>
        !_library.Contains(manuscriptId)
            ? null
            : _kept
                .Where(kept => kept.Id == manuscriptId && kept.Holder == Holder.Earlier)
                .OrderByDescending(kept => kept.Draft.WrittenAtUtc)
                .ThenBy(kept => kept.Folder, StringComparer.Ordinal)
                .FirstOrDefault();

    // The page's own draft of that manuscript goes when the page saves or
    // discards; a discarded one stays for Undo until close.
    private void DeleteLive(Guid manuscriptId)
    {
        foreach (Kept kept in _kept.Where(kept => kept.Id == manuscriptId && kept.IsLive).ToList())
        {
            if (TryDelete(kept))
            {
                _kept.Remove(kept);
            }
        }
    }

    // Rule 2: the slot a new draft of this manuscript goes to. This session's
    // opened or written draft of it is replaced (never a discarded one, even
    // when it comes first, so Undo keeps its file); otherwise the first free
    // slot of the main slot and the manuscript's waiting slots. There is
    // always one, since a waiting slot with no file is free: a page's draft
    // is never refused for want of a slot.
    private string SlotFor(Guid manuscriptId)
    {
        if (_kept.FirstOrDefault(kept => kept.Id == manuscriptId && kept.IsLive) is { } live)
        {
            return live.Folder;
        }

        if (IsFree(_dataFolder))
        {
            return _dataFolder;
        }

        for (int n = 1; ; n++)
        {
            string waiting = Path.Combine(_dataFolder, "recovery", WaitingSlotName(manuscriptId, n));
            if (IsFree(waiting))
            {
                return waiting;
            }
        }
    }

    // No file, or this session's opened or written draft. A draft nobody
    // answered, a discarded one and a file that cannot be read are not free.
    private bool IsFree(string folder) =>
        !File.Exists(SlotFile(folder)) ||
        _kept.Any(kept => kept.IsIn(folder) && kept.IsLive);

    // A manuscript's n-th waiting slot's folder name: waiting-<id> for the
    // first, waiting-<id>-<n> after it.
    private static string WaitingSlotName(Guid manuscriptId, int n) =>
        WaitingSlotPrefix + manuscriptId.ToString("N") + (n == 1 ? string.Empty : "-" + n.ToString(CultureInfo.InvariantCulture));

    // A folder name WaitingSlotName gives, written exactly as it writes it.
    private static bool IsWaitingSlotName(string name)
    {
        string rest = name[WaitingSlotPrefix.Length..];
        if (rest.Length < 32 || !Guid.TryParseExact(rest[..32], "N", out Guid id))
        {
            return false;
        }

        int n = 1;
        if (rest.Length > 32 &&
            (rest[32] != '-' || !int.TryParse(rest[33..], NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 2))
        {
            return false;
        }

        return name == WaitingSlotName(id, n);
    }

    // The main slot, then every waiting slot, in ordinal order.
    private IEnumerable<string> SlotFolders()
    {
        var folders = new List<string> { _dataFolder };
        string recovery = Path.Combine(_dataFolder, "recovery");
        try
        {
            if (Directory.Exists(recovery))
            {
                folders.AddRange(Directory.GetDirectories(recovery, WaitingSlotPrefix + "*")
                    .Where(path => IsWaitingSlotName(Path.GetFileName(path)))
                    .Order(StringComparer.Ordinal));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"the waiting slots could not be listed ({ex.GetType().Name}: {ex.Message}).");
        }

        return folders;
    }

    private static bool TryDelete(Kept kept)
    {
        try
        {
            DeleteSlot(kept.Folder);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"a draft of this session could not be deleted and stays until the next try ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }

    private static void Log(string message) => Trace.TraceWarning("Recovery drafts: " + message);

    // The core's draft store, called here and nowhere else (start-up design
    // 2.5, lifted-bans.txt): each call reads, writes or deletes the one draft
    // file of a slot under the library's data\recovery folder, and the
    // members above write or delete only a draft this session owns.
#pragma warning disable RS0030
    private static string SlotFile(string folder) => Path.Combine(folder, "recovery", RecoveryDraftStore.FileName);

    private static RecoveryDraft? ReadSlot(string folder) => new RecoveryDraftStore(folder).Read();

    private static void WriteSlot(string folder, RecoveryDraft draft) => new RecoveryDraftStore(folder).Write(draft);

    private static void DeleteSlot(string folder) => new RecoveryDraftStore(folder).Delete();
#pragma warning restore RS0030

    // One draft on disk: its slot's folder, the draft as read or written,
    // and who holds it.
    private sealed class Kept(string folder, RecoveryDraft draft, Holder holder)
    {
        public string Folder { get; } = folder;

        public RecoveryDraft Draft { get; } = draft;

        public Holder Holder { get; set; } = holder;

        public long DiscardOrder { get; set; }

        public Guid Id => Draft.ManuscriptId;

        public bool IsOwn => Holder != Holder.Earlier;

        // Opened or written in this session: what the page holds.
        public bool IsLive => Holder is Holder.Written or Holder.Adopted;

        public bool IsIn(string slot) => string.Equals(Folder, slot, StringComparison.Ordinal);
    }
}
