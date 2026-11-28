using System.Text.Json;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// The manuscript page's store in memory (build 9's design, section 5): the
// only implementation of IManuscriptStore until the session saves (S1). It
// keeps the save contract of 2.5 as far as memory goes: a save is refused
// while busy or for a snapshot; on success the page's records join the author
// "file", the candidate is copied into the session's record
// (ManuscriptCommit), the library's failed-save flag clears, and
// LibraryChanged is raised; a failure after the author write keeps the
// records written and leaves the flag as it was. Results are scripted, the
// author "file" is JSON read afresh on each read, and every read and write is
// counted. Nothing touches a disk.
internal sealed class FakeManuscriptStore : IManuscriptStore
{
    private readonly List<Manuscript> _manuscripts;
    private string _authorFile;
    private TaskCompletionSource? _gate;
    private bool _canSave = true;
    private bool _lastLibrarySaveFailed;

    public FakeManuscriptStore(IEnumerable<Manuscript> manuscripts, AuthorLibraryData? authorFile = null)
    {
        _manuscripts = [.. manuscripts];
        _authorFile = JsonSerializer.Serialize(authorFile ?? new AuthorLibraryData());
    }

    public event EventHandler? SaveStateChanged;

    public event EventHandler? LibraryChanged;

    public IReadOnlyList<Manuscript> Manuscripts => _manuscripts;

    public bool IsBusy { get; private set; }

    public bool CanSave
    {
        get => _canSave;
        set
        {
            _canSave = value;
            SaveStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool LastLibrarySaveFailed
    {
        get => _lastLibrarySaveFailed;
        set
        {
            _lastLibrarySaveFailed = value;
            SaveStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Results for the coming saves and retries, in order; Saved once empty.
    public Queue<ManuscriptSaveResult> Results { get; } = new();

    // The next save waits, busy, until Release.
    public bool HoldNextSave { get; set; }

    // Thrown by the next reads of the author file, until set back to null.
    public Exception? ReadFailure { get; set; }

    public int Reads { get; private set; }

    public int Saves { get; private set; }

    public int Retries { get; private set; }

    public int Deletes { get; private set; }

    public int Writes => Saves + Retries + Deletes;

    // Each candidate given to a save, as given.
    public List<Manuscript> Candidates { get; } = [];

    // Each delta given to a save, copied when it was given.
    public List<LibraryChanges> Deltas { get; } = [];

    // What the author "file" holds now.
    public AuthorLibraryData AuthorFile => JsonSerializer.Deserialize<AuthorLibraryData>(_authorFile)!;

    public Manuscript? Find(Guid id) => _manuscripts.FirstOrDefault(item => item.Id == id);

    public AuthorLibraryData ReadAuthorLibrary()
    {
        Reads++;
        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        return AuthorFile;
    }

    // The library changed elsewhere (a Board card's action, a restore), not
    // through this page's save.
    public void RaiseLibraryChanged() => LibraryChanged?.Invoke(this, EventArgs.Empty);

    // Library work begins or ends elsewhere (a backup, another save).
    public void SetBusy(bool busy)
    {
        IsBusy = busy;
        SaveStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<ManuscriptSaveResult> SaveManuscriptAsync(Manuscript candidate, LibraryChanges delta, CancellationToken cancel)
    {
        Saves++;
        if (IsBusy || !CanSave)
        {
            return Task.FromResult(ManuscriptSaveResult.Refused);
        }

        Candidates.Add(candidate);
        var copied = new LibraryChanges();
        copied.Authors.AddRange(delta.Authors);
        copied.Affiliations.AddRange(delta.Affiliations);
        copied.Journals.AddRange(delta.Journals);
        copied.TagColors.AddRange(delta.TagColors);
        Deltas.Add(copied);
        ManuscriptSaveResult result = Results.Count > 0 ? Results.Dequeue() : ManuscriptSaveResult.Saved;
        SetBusy(true);
        if (!HoldNextSave)
        {
            return Task.FromResult(Finish(candidate, copied, result));
        }

        // A plain TaskCompletionSource: Release runs what awaits the save on
        // the test's own thread, inside the call, so no test waits on the
        // thread pool.
        HoldNextSave = false;
        _gate = new TaskCompletionSource();
        return FinishAfter(_gate.Task, candidate, copied, result);
    }

    // Ends a held save with its scripted result.
    public void Release()
    {
        TaskCompletionSource gate = _gate ?? throw new InvalidOperationException("No save is held.");
        _gate = null;
        gate.SetResult();
    }

    public Task<ManuscriptSaveResult> RetryLibrarySaveAsync(CancellationToken cancel)
    {
        Retries++;
        if (IsBusy || !CanSave)
        {
            return Task.FromResult(ManuscriptSaveResult.Refused);
        }

        ManuscriptSaveResult result = Results.Count > 0 ? Results.Dequeue() : ManuscriptSaveResult.Saved;
        if (result.Outcome == ManuscriptSaveOutcome.Saved)
        {
            LastLibrarySaveFailed = false;
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.FromResult(result);
    }

    public Task<ManuscriptSaveResult> DeleteManuscriptAsync(Guid id, CancellationToken cancel)
    {
        Deletes++;
        _manuscripts.RemoveAll(item => item.Id == id);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(ManuscriptSaveResult.Saved);
    }

    private async Task<ManuscriptSaveResult> FinishAfter(Task gate, Manuscript candidate, LibraryChanges delta, ManuscriptSaveResult result)
    {
        await gate;
        return Finish(candidate, delta, result);
    }

    private ManuscriptSaveResult Finish(Manuscript candidate, LibraryChanges delta, ManuscriptSaveResult result)
    {
        bool authorsWritten = result.Outcome == ManuscriptSaveOutcome.Saved || result.AuthorLibraryWritten;
        if (authorsWritten && !delta.IsEmpty)
        {
            AuthorLibraryData file = AuthorFile;
            delta.ApplyTo(file);
            _authorFile = JsonSerializer.Serialize(file);
        }

        IsBusy = false;
        if (result.Outcome == ManuscriptSaveOutcome.Saved)
        {
            Manuscript? original = Find(candidate.Id);
            if (original is null)
            {
                _manuscripts.Add(candidate);
            }
            else
            {
                ManuscriptCommit.CopyInto(candidate, original);
            }

            _lastLibrarySaveFailed = false;
        }

        SaveStateChanged?.Invoke(this, EventArgs.Empty);
        if (result.Outcome == ManuscriptSaveOutcome.Saved)
        {
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }
}
