using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Notices;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels;

// The window's view model (d1 3.3): the title, the banner (the example's, or
// a snapshot's naming the folder copied), the rail,
// the Board, and the notice strip. The window shows "Loading…" at once, then
// the session's startup notice ("Loaded 7 manuscripts from local storage.")
// once the load is done (d1 3.4). The session and the Board are handed in;
// nothing here starts a session.
//
// The rail is a stub (d1 1.2): today's six pages and three commands, with the
// Board checked and live and everything else disabled. Its library summary
// and Deadlines badge are real, refreshed after each render of the Board as
// Form1.Shell.vb UpdateRailCounts (:419-435) is.
public sealed partial class ShellViewModel : ObservableObject, IInteractions
{
    public const string LoadingText = "Loading…";

    // The window title. Today's example window is "PaperRoute Tracker -
    // Example Library" (Form1.vb:294-303); the trial names itself (d1 3.3).
    public const string ExampleWindowTitle = "PaperRoute Board Trial - Example Library";

    // The banner's accessible name, as today's (Form1.Example.vb:48).
    public const string ExampleBannerName = "Example library";

    // Today's banner text (Form1.Example.vb:55-56), then the trial's sentence (d1 3.3).
    public const string ExampleBannerText =
        "Example library: a fictional research group's manuscripts, to explore and to teach with. " +
        "Change anything; it is discarded when this window closes, and your own library is never opened." +
        " Trial build: nothing is saved.";

    // F1 opens the User Guide today (Form1.vb:2589-2591); in the trial it gives
    // a notice only (d1 1.2), worded as the trial's other notices are.
    public const string HelpNotice = "The User Guide is not part of the trial.";

    private readonly ILibrarySession _session;

    // "7 manuscripts" under the brand once the Board has rendered; empty
    // before (Form1.Shell.vb:187-194, 420-426).
    [ObservableProperty]
    private string _railSummary = string.Empty;

    public ShellViewModel(ILibrarySession session, BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(board);
        _session = session;
        NoticeStrip.Notice(LoadingText);
        NoticeStrip.PropertyChanged += OnNoticeStripPropertyChanged;
        Board = board;
        Board.Interactions = this;

        // Today's order and names (Form1.Shell.vb:230-237); only the Board has a command.
        RailPages =
        [
            new RailItemViewModel("Board", "Board", "Ctrl+1", ShowBoardCommand) { IsSelected = true },
            new RailItemViewModel("Library", "Library", "Ctrl+2", command: null),
            new RailItemViewModel("Journals", "Journals", "Ctrl+3", command: null),
            new RailItemViewModel("Deadlines", "Deadlines", "Ctrl+4", command: null),
            new RailItemViewModel("Insights", "Insights", "Ctrl+5", command: null),
            new RailItemViewModel("Import & Export", "ImportExport", "Ctrl+6", command: null),
        ];

        // Online, Settings and Help (Form1.Shell.vb:273-297; Form1.Online.vb:78-91).
        RailCommands =
        [
            new RailItemViewModel("Online", "Online", shortcut: null, command: null),
            new RailItemViewModel("Settings", "Settings", shortcut: null, command: null),
            new RailItemViewModel("Help", "Help", "F1", command: null),
        ];

        Board.PropertyChanged += OnBoardPropertyChanged;
        UpdateRailCounts();
    }

    // Raised by ShowBoardCommand. Today Ctrl+1 on the Board only re-checks the
    // rail (Form1.Shell.vb:488-492); the trial has no other page to leave.
    public event EventHandler? BoardShown;

    public string WindowTitle => _session.Kind switch
    {
        LibraryKind.Example => ExampleWindowTitle,
        LibraryKind.Snapshot => SnapshotWindowTitle(SnapshotSource()),
        _ => throw new InvalidOperationException($"No window title for a {_session.Kind} library."),
    };

    public string BannerName => _session.Kind switch
    {
        LibraryKind.Example => ExampleBannerName,
        LibraryKind.Snapshot => SnapshotBannerName,
        _ => throw new InvalidOperationException($"No banner for a {_session.Kind} library."),
    };

    // A snapshot's banner changes once, when the load has made the copy and
    // its time is known.
    public string BannerText => _session.Kind switch
    {
        LibraryKind.Example => ExampleBannerText,
        LibraryKind.Snapshot => SnapshotBannerText(SnapshotSource(), _session.Snapshot?.Copy?.TakenAt),
        _ => throw new InvalidOperationException($"No banner for a {_session.Kind} library."),
    };

    // The snapshot's banner name (d1 1.3).
    public const string SnapshotBannerName = "Read-only snapshot";

    // The window title of a snapshot: the trial, then the folder copied.
    public static string SnapshotWindowTitle(string sourceFolder) =>
        "PaperRoute Board Trial - Read-only snapshot of " + sourceFolder;

    // The snapshot's banner (d1 1.3, "Read-only snapshot taken at h:mm.
    // Nothing you do here is saved."), naming the folder copied; before the
    // copy is made it has no time.
    public static string SnapshotBannerText(string sourceFolder, DateTime? takenAt) =>
        "Read-only snapshot of the library in " + sourceFolder +
        (takenAt is { } time ? ", taken at " + ReadOnlySnapshot.TimeText(time) : string.Empty) +
        ". Nothing you do here is saved; the trial shows a copy and never changes that folder.";

    // The line under the brand (Form1.Shell.vb:180-185).
    public string Tagline => ProductInfo.Tagline;

    public BoardViewModel Board { get; }

    // Board, Library, Journals, Deadlines, Insights, Import & Export.
    public IReadOnlyList<RailItemViewModel> RailPages { get; }

    // Online, Settings, Help.
    public IReadOnlyList<RailItemViewModel> RailCommands { get; }

    public RailItemViewModel BoardItem => RailPages[0];

    public RailItemViewModel DeadlinesItem => RailPages[3];

    // The notice strip (shell design 2.4): the status line and the notices
    // that stay. Every notice goes through it.
    public NoticeStripViewModel NoticeStrip { get; } = new();

    // The strip's status line, which the window shows and the Board's tests read.
    public string Notice => NoticeStrip.Status;

    void IInteractions.Notice(string text) => NoticeStrip.Notice(text);

    void IInteractions.Show(StandingNotice notice) => NoticeStrip.Show(notice);

    void IInteractions.Withdraw(string key) => NoticeStrip.Withdraw(key);

    // Ctrl+1 and the rail's Board item (Form1.vb:2562-2565): the Board is the
    // page shown and the one checked in the rail.
    [RelayCommand]
    private void ShowBoard()
    {
        foreach (RailItemViewModel page in RailPages)
        {
            page.IsSelected = ReferenceEquals(page, BoardItem);
        }

        BoardShown?.Invoke(this, EventArgs.Empty);
    }

    // F1 (d1 1.2): a notice only.
    [RelayCommand]
    private void ShowHelp() => NoticeStrip.Notice(HelpNotice);

    private void OnNoticeStripPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NoticeStripViewModel.Status))
        {
            OnPropertyChanged(nameof(Notice));
        }
    }

    private void OnBoardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // RenderCount moves last in a render, after both figures.
        if (e.PropertyName == nameof(BoardViewModel.RenderCount))
        {
            UpdateRailCounts();
        }
    }

    // Form1.Shell.vb UpdateRailCounts (:420-435), without the development
    // profile's line: the trial never runs on the development profile.
    private void UpdateRailCounts()
    {
        if (Board.RenderCount == 0)
        {
            RailSummary = string.Empty;
            DeadlinesItem.Count = 0;
            return;
        }

        int count = Board.ManuscriptCount;
        RailSummary = count.ToString(CultureInfo.CurrentCulture) + (count == 1 ? " manuscript" : " manuscripts");
        DeadlinesItem.Count = Board.DeadlinesDueCount;
    }

    // Started from the interface thread; the session reads off it and the
    // await comes back here (no ConfigureAwait(false) in view-model code).
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancel)
    {
        NoticeStrip.Notice(LoadingText);
        await _session.LoadAsync(cancel);
        NoticeStrip.Notice(_session.StartupNotice);
        if (_session.Kind == LibraryKind.Snapshot)
        {
            OnPropertyChanged(nameof(BannerText));
        }
    }

    private string SnapshotSource() =>
        _session.Snapshot?.SourceFolder
        ?? throw new InvalidOperationException("A snapshot session names no folder.");
}
