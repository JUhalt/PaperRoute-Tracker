using System.Diagnostics;
using System.Text.Json;

namespace PaperRoute.ViewModels.ManuscriptPage;

// The clone of one manuscript that the manuscript page edits (build 9's
// design, 2.2). Nothing else sees it until a save succeeds. It is built anew
// for the new interface: today's edit window keeps the same state in a
// private class (EditManuscriptForm.vb:20-64, RecordState), which stays as it
// is, and the spike's RecordState (Spikes/) stays until the spike's tests move.
//
// One working copy serves a page host and is loaded record after record, as
// today's kept editor is (LoadRecord). Edit is the one way to change the
// record it holds, and it refuses while a save writes (Freeze). After Load or
// Discard, Working is a new object graph, so rows and editors hold ids, never
// objects inside Working, and change it as Edit(w => ...find by id...).
//
// There is no save here: BuildCandidate makes what Save writes, without
// changing the working copy, and AcceptSaved starts again from what was
// written once the store reports success. The write is the session's; no
// view model calls a core save (lock 3).
public sealed class WorkingCopy
{
    private int _freezes;

    // No manuscript until the first is loaded: a working copy of nothing, with
    // nothing unsaved (EditManuscriptForm.vb:66-67).
    public WorkingCopy()
    {
        Working = new Manuscript();
        Baseline = Snapshot();
        Stamps = HeldValues.Of(Working);
        Loaded = TypedValues.Of(Working);
    }

    // The session's own record. Never changed here. Null until a record is
    // loaded (unit 4 adds a start with none).
    public Manuscript? Original { get; private set; }

    // The clone every edit changes. Sections read it; they change it only
    // through Edit.
    public Manuscript Working { get; private set; }

    // Working as JSON when it was loaded; empty for a recovered draft, which
    // is unsaved on purpose (EditManuscriptForm.vb:482-485).
    public string Baseline { get; private set; }

    // A stage chosen and not yet applied. Save completes it through the
    // stage rules (W2); a step that changes the stage clears it (2.4).
    public PaperStage? HeldStage { get; private set; }

    // Target journal text, stage and File Drawer reason as the load left
    // them, and the stage as the last step that changed it left it.
    public HeldValues Stamps { get; private set; }

    // Title, target journal and File Drawer reason as the load left Working,
    // not trimmed: what text typed back to them writes (TypedText). Taken
    // with the baseline, after the reconciliation.
    public TypedValues Loaded { get; private set; }

    // The people, affiliations, journals and tag colours made on the page and
    // not saved yet: a recovered draft's own records, and the page's.
    public LibraryChanges Delta { get; private set; } = new();

    // The author library read at load, with the delta in it, for names and
    // pickers.
    public AuthorLibraryData Library { get; private set; } = new();

    // Changes at each load, discard and save. Work started for an older token
    // changes nothing (Edit(int, ...)), as today's RecordToken does.
    public int Token { get; private set; }

    // A recovery draft is loaded and not yet saved or discarded.
    public bool IsRecovered { get; private set; }

    // A save is writing: every edit is refused until each Freeze is disposed.
    public bool IsFrozen => _freezes > 0;

    // After every Edit, HoldStage, delta change, load, discard and save; boxes and
    // rows reread the working copy then.
    public event EventHandler? Changed;

    // Loads a record: a clone of it, or of the recovery draft given, put right
    // from its latest submission or decision before the baseline is taken, so
    // the reconciliation alone is not an unsaved change but the next Save
    // writes it (EditManuscriptForm.vb:2316-2319, 482-485). The library is the
    // author library just read from disk; a draft's own people and
    // affiliations join it only where the disk lacks their id, and its tag
    // colours are set (EditManuscriptForm.vb:3030-3099). The library given
    // becomes Library and the delta is added into it, so it must be a fresh
    // read that nothing else holds (IManuscriptStore.ReadAuthorLibrary, F1a);
    // Discard takes the same.
    public void Load(Manuscript original, AuthorLibraryData libraryOnDisk, RecoveryDraft? draft = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(libraryOnDisk);
        if (draft is not null && draft.Manuscript is null)
        {
            throw new ArgumentException("A recovery draft without a manuscript cannot be loaded.", nameof(draft));
        }

        if (IsFrozen)
        {
            throw new InvalidOperationException("No other record can be loaded while a save writes this one.");
        }

        Start(original, libraryOnDisk, draft);
    }

    // Returns to the saved record and a fresh read of the author library, as
    // Load with no draft: the edits, the held stage and the page's own
    // records are dropped.
    public bool Discard(Manuscript saved, AuthorLibraryData libraryOnDisk)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(libraryOnDisk);
        if (IsFrozen)
        {
            return Refuse("a discard");
        }

        Start(saved, libraryOnDisk, draft: null);
        return true;
    }

    // The edit door. While frozen it refuses: it returns false, changes
    // nothing and writes a log line; it never throws (2.2). Otherwise it
    // applies the change, lets a step's stage win over a held one (2.4) and
    // raises Changed.
    public bool Edit(Action<Manuscript> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (IsFrozen)
        {
            return Refuse("an edit");
        }

        change(Working);
        FollowAStepsStage();
        OnChanged();
        return true;
    }

    // The edit door for work started earlier, which read Token then: false,
    // and nothing changed, when another record or a discard came since.
    public bool Edit(int token, Action<Manuscript> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return token == Token && Edit(change);
    }

    // A stage chosen in the stage list. It waits for Save; choosing the
    // working copy's own stage clears it.
    public bool HoldStage(PaperStage stage)
    {
        if (IsFrozen)
        {
            return Refuse("a stage choice");
        }

        HeldStage = stage == Working.CurrentStage ? null : stage;
        OnChanged();
        return true;
    }

    // The door for the page's own library records: the change is made to the
    // delta, which then joins Library. Joining only adds, so the delta only
    // grows until a save or a discard: a later step that would take a record
    // out of it (undoing a new person, changing or clearing a held tag
    // colour) must rebuild Library from a fresh read too, or Library would
    // still show what Save no longer writes.
    public bool EditLibrary(Action<LibraryChanges> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (IsFrozen)
        {
            return Refuse("a library change");
        }

        change(Delta);
        Delta.ApplyTo(Library);
        OnChanged();
        return true;
    }

    // Something is unsaved: a held stage, a page-made library record, or a
    // working copy that differs from its baseline. Pending input is not in
    // the copy; the page counts it on top.
    public bool HasUnsavedChanges() =>
        HeldStage is not null ||
        !Delta.IsEmpty ||
        !string.Equals(Snapshot(), Baseline, StringComparison.Ordinal);

    // EditManuscriptForm.Snapshot (:698-700): System.Text.Json with the
    // default options; there is no core member for it.
    public string Snapshot() => JsonSerializer.Serialize(Working);

    // What Save writes (2.5), in today's order (EditManuscriptForm.vb:
    // 4535-4687): a fresh clone of Working with Save's rules applied
    // (ManuscriptRules), or why nothing is written. It never changes the
    // working copy, the held stage or the delta, so a save that fails or is
    // refused leaves every edit as it was, and a retry builds anew from the
    // same state: it cannot add an event twice. Now is the core's clock
    // (CoreClock.Now, D9), given by the caller.
    public SaveAttempt BuildCandidate(DateTime now)
    {
        if (ManuscriptRules.IsTitleMissing(Working.Title))
        {
            return SaveAttempt.TitleRequired();
        }

        Manuscript candidate = ManuscriptCloneService.CloneManuscript(Working);
        PaperStage oldStage = candidate.CurrentStage;
        PaperStage newStage = HeldStage ?? oldStage;
        if (newStage != oldStage && ManuscriptRules.StepNeededFor(candidate, newStage) is { } requirement)
        {
            return SaveAttempt.NeedsStep(requirement);
        }

        candidate.Location = ManuscriptRules.LocationFor(newStage, candidate.Location);

        candidate.Title = (candidate.Title ?? string.Empty).Trim();
        string journalText = (candidate.TargetJournal ?? string.Empty).Trim();
        candidate.TargetJournalId = ManuscriptRules.TargetJournalIdFor(candidate.TargetJournalId, Stamps.TargetJournal, journalText, Library.Journals);
        candidate.TargetJournal = journalText;

        if (newStage != oldStage)
        {
            candidate.CurrentStage = newStage;
            candidate.StageEnteredDate = now;
            candidate.History.Add(ManuscriptRules.StageChanged(oldStage, newStage, now));
        }

        // By location, as today: Save may be pressed on any section. The
        // saved record's reason, not the one loaded, so a recovered draft's
        // reason gets its event (D16).
        if (candidate.Location == ManuscriptLocation.FileDrawer &&
            ManuscriptRules.FileDrawerReasonChanged(Original?.FileDrawerReason, candidate.FileDrawerReason, candidate.CurrentStage, now) is { } reasonChange)
        {
            candidate.FileDrawerReason = (candidate.FileDrawerReason ?? string.Empty).Trim();
            candidate.History.Add(reasonChange);
        }

        // The clone leaves the matches empty, and the candidate, not the
        // original, goes into the list written to disk: without them a page
        // save would erase a publication check's matches on disk. The
        // candidate gets its own list, holding the original's items.
        candidate.PublicationMatches = [.. Original?.PublicationMatches ?? []];
        return SaveAttempt.Ready(candidate);
    }

    // After the store reports that the candidate was written (2.2): the
    // working copy starts again from the record as written, which the
    // repository may have normalised in place. Working becomes a clone of
    // it, the baseline, the stamps and the typed values are retaken from
    // that clone, the held stage and the delta are cleared, a recovered
    // draft is saved, and Token changes. No reconcile runs, as today's
    // commit retakes the baseline straight after the copy
    // (EditManuscriptForm.vb:4667-4670). Original is the session's to
    // change (ManuscriptCommit), not this. It is called while the save's
    // Freeze is still held, so it is not refused when frozen.
    public void AcceptSaved(Manuscript written)
    {
        ArgumentNullException.ThrowIfNull(written);
        Working = ManuscriptCloneService.CloneManuscript(written);
        Baseline = Snapshot();
        Stamps = HeldValues.Of(Working);
        Loaded = TypedValues.Of(Working);
        HeldStage = null;
        Delta = new LibraryChanges();
        IsRecovered = false;
        Token++;
        OnChanged();
    }

    // Stops every edit until the result is disposed, for a save's write.
    public IDisposable Freeze()
    {
        _freezes++;
        return new Thaw(this);
    }

    private void Start(Manuscript original, AuthorLibraryData libraryOnDisk, RecoveryDraft? draft)
    {
        Original = original;
        Working = ManuscriptCloneService.CloneManuscript(draft?.Manuscript ?? original);
        ManuscriptLifecycleService.ReconcileFromLatestWorkflow(Working);
        Delta = Carried(draft?.UnsavedLibrary, libraryOnDisk);
        Delta.ApplyTo(libraryOnDisk);
        Library = libraryOnDisk;
        IsRecovered = draft is not null;
        Baseline = IsRecovered ? string.Empty : Snapshot();
        HeldStage = null;
        Stamps = HeldValues.Of(Working);
        Loaded = TypedValues.Of(Working);
        Token++;
        OnChanged();
    }

    // A step that changed the stage (a submission or decision recorded, a
    // lifecycle reconcile) wins: a stage chosen and not saved is dropped, and
    // the stage stamp follows the step, so a stage chosen after it is held.
    // A step that left the stage leaves a held stage alone.
    private void FollowAStepsStage()
    {
        if (Working.CurrentStage != Stamps.Stage)
        {
            HeldStage = null;
            Stamps = Stamps with { Stage = Working.CurrentStage };
        }
    }

    // A recovery draft's own records that a library read from disk lacks:
    // people and affiliations whose id the disk does not hold, each id once,
    // and every tag colour.
    private static LibraryChanges Carried(RecoveryDraftLibrary? draft, AuthorLibraryData onDisk)
    {
        var delta = new LibraryChanges();
        if (draft is null)
        {
            return delta;
        }

        var authorIds = (onDisk.Authors ?? Enumerable.Empty<AuthorRecord>()).OfType<AuthorRecord>().Select(author => author.Id).ToHashSet();
        delta.Authors.AddRange((draft.Authors ?? Enumerable.Empty<AuthorRecord>()).OfType<AuthorRecord>()
            .Where(author => author.Id != Guid.Empty && authorIds.Add(author.Id)));

        var affiliationIds = (onDisk.Affiliations ?? Enumerable.Empty<AffiliationRecord>()).OfType<AffiliationRecord>().Select(affiliation => affiliation.Id).ToHashSet();
        delta.Affiliations.AddRange((draft.Affiliations ?? Enumerable.Empty<AffiliationRecord>()).OfType<AffiliationRecord>()
            .Where(affiliation => affiliation.Id != Guid.Empty && affiliationIds.Add(affiliation.Id)));

        delta.TagColors.AddRange((draft.TagColors ?? Enumerable.Empty<TagColor>()).OfType<TagColor>());
        return delta;
    }

    // A refusal while a save writes: a log line, never an exception. In a
    // Debug build under a debugger it also stops as an assertion does (a
    // Release build compiles Debug.Fail out); without a debugger it does not,
    // because a failed assertion ends a process that has no debugger. Tests
    // check the log line instead of an assertion (W-8).
    private static bool Refuse(string what)
    {
        string message = $"The manuscript page refused {what} to its working copy while a save was writing it.";
        Trace.TraceWarning(message);
        if (Debugger.IsAttached)
        {
            Debug.Fail(message);
        }

        return false;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class Thaw(WorkingCopy copy) : IDisposable
    {
        private WorkingCopy? _copy = copy;

        public void Dispose()
        {
            if (_copy is not null)
            {
                _copy._freezes--;
                _copy = null;
            }
        }
    }
}
