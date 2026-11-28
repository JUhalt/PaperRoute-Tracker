using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManuscriptPipeline;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.ManuscriptPage;

// The manuscript page's frame (build 9's design, 2.2 and 2.5): one working
// copy, its sections, the Unsaved changes bar, Save and Discard. One serves a
// page host and is opened record after record, as today's kept editor is.
// It has no window: the view (V1a) binds to it.
//
// Save writes first, and memory changes only after the write succeeded: the
// store commits the candidate to the session's record, and then the working
// copy starts again from it (AcceptSaved). A failed save changes nothing in
// memory, and the edits stay unsaved on the page with the bar up (D1, the
// maintainer's answer to question 2). Nothing here writes a file: the write
// is the store's.
public sealed partial class ManuscriptEditorViewModel : ObservableObject
{
    // The bar's hints (Form1.ManuscriptPage.vb:59-66).
    public const string EditedHint = "Save keeps them. Discard returns to the last saved version.";

    public const string SaveFailedHint = "Your last save did not finish. Save tries again.";

    // After a save has run for SavingHintDelay (2.5, step 7).
    public const string SavingHint = "Saving...";

    // The title's inline problem (EditManuscriptForm.vb:4537-4553; D4).
    public const string TitleField = "Title";

    public const string TitleRequired = "Please enter a manuscript title.";

    // The page's two states with nothing to edit: the record is gone
    // (Form1.ManuscriptPage.vb:154-162), or the author file cannot be read
    // (critique N7), followed by the reader's message.
    public const string MissingManuscript = "This manuscript is no longer in the library.";

    public const string UnreadableAuthorLibrary = "PaperRoute could not read the author library, so this manuscript cannot be edited.";

    // Form1.ManuscriptPage.vb:1183-1193.
    public const string Discarded = "Unsaved changes discarded.";

    // A discard whose fresh read of the author file failed, followed by the
    // reader's message.
    public const string DiscardKeptLibrary = "PaperRoute could not read the author library again, so the page keeps the one it read before.";

    public static readonly TimeSpan SavingHintDelay = TimeSpan.FromMilliseconds(200);

    private readonly IManuscriptStore _store;
    private readonly IInteractions _interactions;
    private readonly IDebouncer _savingTimer;

    // The record the page shows, editable or not.
    private Guid? _shownId;

    // IsLoaded's value.
    private bool _isLoaded;

    // The author file as the last read or save left it on disk, without the
    // page's own records: what a discard keeps when it cannot read the file.
    private string _libraryOnDisk = string.Empty;

    // This page's last save did not finish, and the edits it tried are still
    // unsaved here.
    private bool _lastSaveFailed;

    // A save has run for SavingHintDelay.
    private bool _savingShown;

    // The frame is loading, discarding or accepting a save: the sections are
    // attached afresh after it, so the copy's Changed does not refresh them.
    private bool _starting;

    // The held stage StageMessage speaks of.
    private PaperStage? _stageMessageFor;

    [ObservableProperty]
    private IManuscriptSection? _selectedSection;

    [ObservableProperty]
    private ManuscriptHeader? _header;

    [ObservableProperty]
    private bool _showsSaveBar;

    [ObservableProperty]
    private string _saveBarHint = EditedHint;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardCommand))]
    private bool _isSaving;

    // Input to the section content stops while a save writes, and no
    // control is disabled, so focus does not move and no lost-focus commit
    // fires (2.5, step 7; Form1.LibraryWork.vb:86-104). The view lays a
    // panel over the content and handles its keys.
    [ObservableProperty]
    private bool _isInputBlocked;

    // Under the stage list: what a held stage needs first (D3).
    [ObservableProperty]
    private string _stageMessage = string.Empty;

    // Why nothing can be edited: MissingManuscript, or UnreadableAuthorLibrary
    // with its message; empty when the record is loaded.
    [ObservableProperty]
    private string _loadProblem = string.Empty;

    // What stopped the last Save, each shown beside its field; empty after a
    // save that went on, a discard and a load.
    [ObservableProperty]
    private IReadOnlyList<FieldProblem> _problems = [];

    public ManuscriptEditorViewModel(IManuscriptStore store, IInteractions interactions, IDebouncer savingTimer, IEnumerable<IManuscriptSection> sections)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(savingTimer);
        ArgumentNullException.ThrowIfNull(sections);
        _store = store;
        _interactions = interactions;
        _savingTimer = savingTimer;
        Sections = [.. sections];
        _selectedSection = Sections.FirstOrDefault();
        Copy.Changed += OnCopyChanged;
        foreach (IManuscriptSection section in Sections)
        {
            section.PendingChanged += OnPendingChanged;
        }

        store.SaveStateChanged += OnSaveStateChanged;
        store.LibraryChanged += OnLibraryChanged;
    }

    // Raised when Save stops on problems, with the first: the view focuses
    // its field and the message is read (2.5, step 5).
    public event EventHandler<FieldProblem>? ProblemFocusRequested;

    public WorkingCopy Copy { get; } = new();

    // The tabs, in order. Only built sections are given (D13).
    public IReadOnlyList<IManuscriptSection> Sections { get; }

    // The working copy holds the shown record and it can be edited. False
    // before the first Open and in the missing and error states; the view
    // switches between the editor and those states on it.
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    // Something on the page is unsaved: the working copy, or text in a
    // section that is not in it yet (critique M4). Recomputed whenever it is
    // read, never taken from the bar.
    public bool HasUnsavedWork => IsLoaded &&(Copy.HasUnsavedChanges() || Sections.Any(section => section.HasPendingInput()));

    // Shows a record: a clone of the session's own, with a fresh read of the
    // author file. False, with the page's state saying why, when the record
    // is gone or the author file cannot be read; the header still shows a
    // record that exists, so the page can be left. False too, changing
    // nothing, while a save writes (the shell refuses page changes then), so
    // a false result with LoadProblem empty is not a load problem. Asking
    // about unsaved work on the record left is the guard's, before this
    // (F1b).
    public bool Open(Guid id)
    {
        if (IsSaving)
        {
            return false;
        }

        _shownId = id;
        foreach (IManuscriptSection section in Sections)
        {
            section.ClearPending();
        }

        Manuscript? found = _store.Find(id);
        Header = found is null ? null : ManuscriptHeader.Of(found);
        if (found is not { } saved)
        {
            ShowProblem(MissingManuscript);
            return false;
        }

        AuthorLibraryData library;
        try
        {
            library = _store.ReadAuthorLibrary();
        }
        catch (InvalidDataException ex)
        {
            ShowProblem($"{UnreadableAuthorLibrary} {ex.Message}");
            return false;
        }

        _libraryOnDisk = JsonSerializer.Serialize(library);
        LoadProblem = string.Empty;
        Restart(() => Copy.Load(saved, library));
        return true;
    }

    // Save (2.5), from the bar, Ctrl+S and the guard's Yes. True when
    // nothing is left to save; false when the edits stay unsaved.
    public async Task<bool> SaveAsync()
    {
        // 1. No record loaded.
        if (!IsLoaded)
        {
            return true;
        }

        // A second Save while one writes.
        if (IsSaving)
        {
            return false;
        }

        // 2. Refused silently while library work runs, and for a snapshot
        // (Form1.ManuscriptPage.vb:1150-1155): the edits stay.
        if (_store.IsBusy || !_store.CanSave)
        {
            return false;
        }

        // 3. Complete pending input joins the copy, and complete expanded
        // editors close (D10; the design's rule 2).
        Copy.Edit(working =>
        {
            foreach (IManuscriptSection section in Sections)
            {
                section.ApplyPending(working, PendingUse.Save);
            }
        });

        // 4. Nothing unsaved: write nothing (D5), unless the library's last
        // save failed; then Save tries again.
        if (!HasUnsavedWork)
        {
            Problems = [];
            return !_store.LastLibrarySaveFailed ||
                await WriteAsync(() => _store.RetryLibrarySaveAsync(CancellationToken.None), candidate: null);
        }

        // 5. Inline problems stop Save before anything is built.
        List<FieldProblem> found = [];
        if (ManuscriptRules.IsTitleMissing(Copy.Working.Title))
        {
            found.Add(new FieldProblem(TitleField, TitleRequired));
        }

        foreach (IManuscriptSection section in Sections)
        {
            found.AddRange(section.Problems());
        }

        Problems = found;
        if (found.Count > 0)
        {
            ProblemFocusRequested?.Invoke(this, found[0]);
            return false;
        }

        // 6. The candidate, dated by the core's clock (D9). A held stage
        // that needs a step stops here and stays held (D3), in today's words
        // (EditManuscriptForm.vb:4473-4483).
        SaveAttempt attempt = Copy.BuildCandidate(CoreClock.Now);
        if (!attempt.IsReady)
        {
            if (attempt.Refusal == SaveRefusal.NeedsStep)
            {
                _stageMessageFor = Copy.HeldStage;
                StageMessage = $"PaperRoute did not find {attempt.Requirement}, so the current stage was not changed.";
            }

            return false;
        }

        _stageMessageFor = null;
        StageMessage = string.Empty;
        Manuscript candidate = attempt.Candidate;
        return await WriteAsync(() => _store.SaveManuscriptAsync(candidate, Copy.Delta, CancellationToken.None), candidate);
    }

    // Back to the saved record and a fresh read of the author file, with no
    // question (Form1.ManuscriptPage.vb:1183-1193): the edits, the held
    // stage, the page's own records and pending input go. The library's
    // failed-save flag is left as it is. When the author file cannot be read
    // now, the library read before is kept and the notice says so.
    [RelayCommand(CanExecute = nameof(CanSaveOrDiscard))]
    public void Discard()
    {
        if (!IsLoaded || IsSaving || _shownId is not Guid id)
        {
            return;
        }

        foreach (IManuscriptSection section in Sections)
        {
            section.ClearPending();
        }

        if (_store.Find(id) is not { } saved)
        {
            Header = null;
            ShowProblem(MissingManuscript);
            return;
        }

        string notice = Discarded;
        AuthorLibraryData library;
        try
        {
            library = _store.ReadAuthorLibrary();
            _libraryOnDisk = JsonSerializer.Serialize(library);
        }
        catch (InvalidDataException ex)
        {
            library = JsonSerializer.Deserialize<AuthorLibraryData>(_libraryOnDisk) ?? new AuthorLibraryData();
            notice = $"{Discarded} {DiscardKeptLibrary} {ex.Message}";
        }

        Header = ManuscriptHeader.Of(saved);
        Restart(() => Copy.Discard(saved, library));
        _interactions.Notice(notice);
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrDiscard))]
    private Task Save() => SaveAsync();

    private bool CanSaveOrDiscard() => !IsSaving;

    // Steps 7 to 10 of Save, for a candidate or a retry of the library's
    // save (candidate null).
    private async Task<bool> WriteAsync(Func<Task<ManuscriptSaveResult>> write, Manuscript? candidate)
    {
        // 7. Input stops first, then the copy refuses edits.
        IsInputBlocked = true;
        try
        {
            ManuscriptSaveResult result;
            using (Copy.Freeze())
            {
                IsSaving = true;
                _savingTimer.Restart(SavingHintDelay, ShowSaving);

                // 8.
                result = await write();

                // 9. Saved: the store has committed the candidate to the
                // session's record; the copy starts again from it, inside
                // the freeze, and every section is attached afresh.
                if (result.Outcome == ManuscriptSaveOutcome.Saved && candidate is not null)
                {
                    Restart(() => Copy.AcceptSaved(candidate));
                }
            }

            switch (result.Outcome)
            {
                case ManuscriptSaveOutcome.Saved:
                    _lastSaveFailed = false;
                    _libraryOnDisk = JsonSerializer.Serialize(Copy.Library);
                    RefreshHeader();
                    return true;

                case ManuscriptSaveOutcome.Failed:
                    // The edits stay unsaved here (D1). When the author file
                    // was written first, the page's records are on disk and
                    // the next save has none to add. MarkWritten raises no
                    // Changed, so the bar is recomputed below.
                    _lastSaveFailed = true;
                    if (result.AuthorLibraryWritten)
                    {
                        Copy.Delta.MarkWritten();
                        _libraryOnDisk = JsonSerializer.Serialize(Copy.Library);
                    }

                    _interactions.Notice(result.Message);
                    return false;

                default:
                    return false;
            }
        }
        finally
        {
            // 10. Focus going back is the view's.
            _savingTimer.Stop();
            _savingShown = false;
            IsSaving = false;
            IsInputBlocked = false;
            Recompute();
        }
    }

    private void ShowSaving()
    {
        if (IsSaving)
        {
            _savingShown = true;
            Recompute();
        }
    }

    // Loads, discards or accepts a save, then attaches every section to the
    // record as it now is (the design's rule 2).
    private void Restart(Action change)
    {
        _starting = true;
        try
        {
            change();
        }
        finally
        {
            _starting = false;
        }

        IsLoaded = true;
        _lastSaveFailed = false;
        _stageMessageFor = null;
        StageMessage = string.Empty;
        Problems = [];
        foreach (IManuscriptSection section in Sections)
        {
            section.Attach(Copy);
        }

        Recompute();
    }

    private void ShowProblem(string problem)
    {
        _lastSaveFailed = false;
        _stageMessageFor = null;
        StageMessage = string.Empty;
        Problems = [];

        // The problem first, so a view that switches on IsLoaded finds it.
        LoadProblem = problem;
        IsLoaded = false;
        Recompute();
    }

    private void RefreshHeader()
    {
        if (_shownId is Guid id && _store.Find(id) is { } saved)
        {
            Header = ManuscriptHeader.Of(saved);
        }
    }

    // The bar: shown while something is unsaved or the library's last save
    // failed (Form1.ManuscriptPage.vb:1117-1134), recomputed on every change
    // instead of on today's timer.
    private void Recompute()
    {
        bool unsaved = HasUnsavedWork;
        ShowsSaveBar = IsLoaded &&(unsaved || _store.LastLibrarySaveFailed);
        SaveBarHint = _savingShown ? SavingHint
            : _lastSaveFailed || (_store.LastLibrarySaveFailed && !unsaved) ? SaveFailedHint
            : EditedHint;
    }

    private void OnCopyChanged(object? sender, EventArgs e)
    {
        if (!_starting)
        {
            foreach (IManuscriptSection section in Sections)
            {
                section.Refresh();
            }
        }

        if (_stageMessageFor is not null && Copy.HeldStage != _stageMessageFor)
        {
            _stageMessageFor = null;
            StageMessage = string.Empty;
        }

        Recompute();
    }

    private void OnPendingChanged(object? sender, EventArgs e) => Recompute();

    private void OnSaveStateChanged(object? sender, EventArgs e) => Recompute();

    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshHeader();
}
