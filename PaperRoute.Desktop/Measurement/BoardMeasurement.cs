using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Measurement;

// The rows of d1 6.3, timed the way ResponsivenessBudgetTests.vb times
// today's Board, by the headless budget tests and by "--measure" in a real
// window (d1 7.3), which run these same actions; only how a frame is drawn
// differs (the frame action: a captured frame headless, the next two
// animation frames on a real window).
//
// Wall is a Stopwatch around the action. UI is the longest gap between the
// ticks of a 1 ms DispatcherTimer at Input priority on the interface thread
// while the action runs (the twin of UiThreadProbe, a WM_TIMER,
// ResponsivenessBudgetTests.vb:858-888), never more than Wall. The probe
// ticks only where input could be served, so an action whose queued jobs
// (layout, render, a virtualising list's rows) all outrank input has UI
// equal to Wall, which is every headless action but the startup's load (that
// runs off the interface thread). A row's verdict is UI against its budget. Each change is checked, outside its
// timed step, to have changed the Pipeline shelf as it should
// (ResponsivenessBudgetTests.vb:420-446), so no row passes on a change that
// did nothing; a failed check throws.
//
// The shelves draw option (a) or (b) of d1 4.4 (ShelfLayout.Option): every
// card of a shown shelf made at once, or rows of N with only the rows on
// screen made. Rows the 500 library cannot meet under the option built stay
// measured and reported, and are listed in WaitingForRowsOfN, one list per
// option; every other row is held.
public sealed class BoardMeasurement
{
    // Today's budgets, in milliseconds (ResponsivenessBudgetTests.vb:45-53).
    public const double StartupBudget = 1500;
    public const double PageSwitchBudget = 100;
    public const double UiThreadBudget = 250;
    public const double FilterKeystrokeBudget = 50;

    // A Debug build and the GitHub Actions runner multiply every budget
    // (ResponsivenessBudgetTests.vb:40-43, 901-910).
    public const double DebugMultiplier = 2;
    public const double CiMultiplier = 3;

    // The shelves' option (d1 4.4, ShelfLayout): (a), every card of a shown
    // shelf made at once, or (b), rows of N, only the rows on screen made.
    public const string Option = ShelfLayout.Option;

    // The search typed for the keystroke row: a tag of the 500 library's pool
    // (LargeLibraryFixture.vb:59-61) that no title holds.
    public const string SearchText = "teaching";

    // The stage box's items: All stages, then Idea, Draft, Submitted, ...
    public const int SubmittedIndex = 3;

    // Row ids. A paint row times the frame drawn after the change before it.
    public const string WarmStartup = "warm-startup";
    public const string StartupPaint = "warm-startup-paint";
    public const string FirstSwitchToPublished = "first-switch-published";
    public const string FirstSwitchToPublishedPaint = "first-switch-published-paint";
    public const string SwitchBackToPipeline = "switch-back-pipeline";
    public const string RenderWithNoChange = "render-no-card-changes";
    public const string StageToSubmitted = "stage-all-to-submitted";
    public const string StageToSubmittedPaint = "stage-all-to-submitted-paint";
    public const string StageBackToAll = "stage-back-to-all";
    public const string StageBackToAllPaint = "stage-back-to-all-paint";
    public const string OpenListStepped = "open-list-stepped-closed";
    public const string OpenListSteppedPaint = "open-list-stepped-closed-paint";
    public const string SearchKeystroke = "search-keystroke";
    public const string SearchKeystrokePaint = "search-keystroke-paint";
    public const string ScrollPublished = "scroll-published-longest-step";
    public const string ProcessStartToBoard = "process-start-to-board";

    // Report-only, not a row of d1 6.3: the switch to Published after a
    // search made while it was hidden (its rows refilled at the switch).
    public const string SwitchToPublishedAfterHiddenChange = "switch-published-after-hidden-change";

    // The 500 library's rows that do not meet their budget with every card of
    // a shown shelf made at once (option (a)). Their budgets stand; under (a)
    // they are measured and reported, not held. Medians of five headless
    // Release runs on the trial's development PC, 2026-11-20, each in its own
    // process, with the probe at Input priority (UI ms against the budget):
    // warm startup 3,032 / 1,500 (167 cards made); first switch to Published
    // 3,970 / 100 (272 cards made); stage filter to Submitted 1,107 / 250 and
    // back to All stages 1,406 / 250 (each a Reset of the Pipeline and of the
    // Published shelf, shown before); the open list stepped and closed 553 /
    // 250; the search keystroke 669 / 50 (a Reset of both shelves).
    private static readonly IReadOnlyList<string> s_waitingUnderEveryCard =
    [
        WarmStartup,
        FirstSwitchToPublished,
        StageToSubmitted,
        StageBackToAll,
        OpenListStepped,
        SearchKeystroke,
    ];

    // Under option (b), rows of N (design step 13), the rows the 500 library
    // still does not meet; every other row is held. Medians of five headless
    // Release runs on the same PC, 2026-11-20, each in its own process, with
    // the probe at Input priority, as today's WM_TIMER (UI ms against the
    // budget, option (a) the same day in brackets): warm startup 481 / 1,500
    // (3,032), held; first switch to Published 383 / 100 (3,970), waiting:
    // the card views on screen cost about 16 ms each to make; stage filter to
    // Submitted 240 / 250 (1,107), waiting: within its budget at the median
    // but over in one run of five (251), so not held three times in a row;
    // back to All stages 157 / 250 (1,406), held; the open list stepped and
    // closed 154 / 250 (553), held; the search keystroke 208 / 50 (669),
    // waiting; scrolling the Published shelf a page at a time, its longest
    // step 288 / 250 (16), waiting: held under option (a), whose cards are all
    // made before, but under (b) each page gives a page of card views new
    // cards, then their layout. (A probe at Normal priority, used before
    // 2026-11-20, ticked between the layout and render jobs and gave about
    // half these numbers under (b).)
    private static readonly IReadOnlyList<string> s_waitingUnderRows =
    [
        FirstSwitchToPublished,
        StageToSubmitted,
        SearchKeystroke,
        ScrollPublished,
    ];

    // The rows that wait under the option this build draws (ShelfLayout).
    public static IReadOnlyList<string> WaitingForRowsOfN => ShelfLayout.UsesRows ? s_waitingUnderRows : s_waitingUnderEveryCard;

    private static readonly TimeSpan s_loadLimit = TimeSpan.FromMinutes(2);

    private readonly Action<Window> _frame;
    private readonly List<MeasuredRow> _rows = [];

    // frame draws one frame of the window and returns once it is drawn.
    public BoardMeasurement(Action<Window> frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
    }

    public IReadOnlyList<MeasuredRow> Rows => _rows;

    public static string Build =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    public static double BudgetMultiplier()
    {
        double multiplier = 1;
#if DEBUG
        multiplier *= DebugMultiplier;
#endif
        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            multiplier *= CiMultiplier;
        }

        return multiplier;
    }

    public void Add(MeasuredRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _rows.Add(row);
    }

    // Warm startup to an interactive Board (d1 6.3): the shell and the window
    // made and shown, the session loaded (off the interface thread, as the
    // app's LoadCommand does), the Board rendered and laid out. The session
    // must not be loaded yet. Then the frame after it.
    public (MainWindow Window, ShellViewModel Shell) Start(string library, ILibrarySession session, ThemeVariant? variant, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(session);
        ShellViewModel? shell = null;
        MainWindow? window = null;
        Time(library, WarmStartup, "Warm startup to an interactive Board (window shown, session loaded, first render, layout done)", StartupBudget, "warm startup", () =>
        {
            shell = App.CreateShell(session);
            window = new MainWindow { DataContext = shell, Width = width, Height = height };
            if (variant is not null)
            {
                window.RequestedThemeVariant = variant;
            }

            window.Show();
            Task load = shell.LoadCommand.ExecuteAsync(null);
            if (!PumpUntil(() => load.IsCompleted, s_loadLimit))
            {
                throw new TimeoutException($"The library did not load within {s_loadLimit.TotalSeconds} s.");
            }

            load.GetAwaiter().GetResult();
            Settle(window);
        });

        MainWindow shown = window ?? throw new InvalidOperationException("No window was made.");
        ShellViewModel made = shell ?? throw new InvalidOperationException("No shell was made.");
        Require(made.Board.RenderCount > 0, "The Board rendered once the library loaded.");
        Paint(library, StartupPaint, "Its first frame after startup", shown);
        return (shown, made);
    }

    // Every other row of d1 6.3 on a Board Start made, in the order a person
    // meets them. requireChanges holds the 500 library's checks: a Published
    // shelf of 100 or more, and Submitted manuscripts on the Pipeline among others.
    public void MeasureBoard(string library, MainWindow window, ShellViewModel shell, ILibrarySession session, bool requireChanges)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(session);

        BoardViewModel board = shell.Board;
        BoardView view = window.FindControl<BoardView>("BoardView") ?? throw new InvalidOperationException("The window has no Board view.");
        ComboBox stageBox = view.FindControl<ComboBox>("StageFilter") ?? throw new InvalidOperationException("The Board has no stage box.");
        List<Manuscript> manuscripts = session.Manuscripts.Where(manuscript => manuscript is not null).ToList();
        int pipeline = manuscripts.Count(manuscript => manuscript.Location == ManuscriptLocation.Pipeline);
        int published = manuscripts.Count(manuscript => manuscript.Location == ManuscriptLocation.Published);
        int submitted = manuscripts.Count(manuscript => manuscript.Location == ManuscriptLocation.Pipeline && manuscript.CurrentStage == PaperStage.Submitted);
        if (requireChanges)
        {
            Require(published >= 100, $"The library has a Published shelf to fill a screen ({published}).");
            Require(submitted > 0 && submitted < pipeline, $"The library has Submitted manuscripts on its Pipeline shelf, and others ({submitted} of {pipeline}).");
        }

        RequireShelf(view, "PipelineShelf", pipeline, "at the start");
        Require(Views(view, "PublishedShelf") == 0, "The Published shelf made no card before it was first shown.");

        // The first switch to Published makes its cards at the switch.
        Time(library, FirstSwitchToPublished, $"First switch to Published (the shelf's cards made at the switch, option ({Option}))", PageSwitchBudget, "page switch", () =>
        {
            board.SelectedShelfIndex = 1;
            Settle(window);
        });
        RequireShelf(view, "PublishedShelf", published, "after the first switch");
        Paint(library, FirstSwitchToPublishedPaint, "Published: its frame after the first switch", window);

        Time(library, SwitchBackToPipeline, "Switch back to Pipeline (its cards made before)", PageSwitchBudget, "page switch", () =>
        {
            board.SelectedShelfIndex = 0;
            Settle(window);
        }, informational: true, note: "not a row of d1 6.3");
        _frame(window);

        // A render that changes no card.
        Time(library, RenderWithNoChange, "Board render, no card changes (Render(), then layout)", UiThreadBudget, "UI thread", () =>
        {
            board.Render();
            Settle(window);
        });
        NoteChanges(board);
        Require(board.LastSyncChanges.All(changes => changes == 0), "The render changed no card: " + string.Join(", ", board.LastSyncChanges));
        _frame(window);

        // The stage filter on the closed box, one change, then back.
        int expectSubmitted = requireChanges ? submitted : -1;
        Time(library, StageToSubmitted, "Stage filter All stages to Submitted (one change on the closed box)", UiThreadBudget, "UI thread", () =>
        {
            stageBox.SelectedIndex = SubmittedIndex;
            Settle(window);
        });
        NoteChanges(board);
        Require(board.SelectedStageIndex == SubmittedIndex, "Submitted is chosen.");
        RequireShelf(view, "PipelineShelf", expectSubmitted, "with Submitted chosen");
        Paint(library, StageToSubmittedPaint, "Its frame after the stage filter change", window);

        Time(library, StageBackToAll, "Stage filter back to All stages", UiThreadBudget, "UI thread", () =>
        {
            stageBox.SelectedIndex = 0;
            Settle(window);
        });
        NoteChanges(board);
        RequireShelf(view, "PipelineShelf", pipeline, "with All stages chosen again");
        Paint(library, StageBackToAllPaint, "Its frame after the stage filter goes back to All stages", window);

        // The open list stepped Idea, Draft, Submitted, then closed: the
        // #338 rule renders once, as the list closes. The steps set the box's
        // choice while its list is open, as ResponsivenessBudgetTests'
        // StepOpenList does (Avalonia's arrows in an open list do not choose).
        int rendersBefore = board.RenderCount;
        Time(library, OpenListStepped, "The open stage list stepped Idea, Draft, Submitted, then closed (its messages processed, layout done)", UiThreadBudget, "UI thread", () =>
        {
            stageBox.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            stageBox.SelectedIndex = 1;
            stageBox.SelectedIndex = 2;
            stageBox.SelectedIndex = SubmittedIndex;
            stageBox.IsDropDownOpen = false;
            Settle(window);
        });
        NoteChanges(board);
        Require(board.RenderCount == rendersBefore + 1, $"The open list's steps rendered once, as it closed ({board.RenderCount - rendersBefore} renders).");
        RequireShelf(view, "PipelineShelf", expectSubmitted, "with Submitted chosen from the open list");
        Paint(library, OpenListSteppedPaint, "Its frame after the open list closed", window);
        stageBox.SelectedIndex = 0;
        Settle(window);
        RequireShelf(view, "PipelineShelf", pipeline, "after the stage filter rows");
        _frame(window);

        // A search: the text typed, and the render the search's wait ends in
        // (BoardViewModel.SearchDelayElapsed renders; here Render is called at
        // once, which also stops the wait), then layout.
        Time(library, SearchKeystroke, $"Search keystroke to rendered Board (\"{SearchText}\" typed into an empty search, then the render its wait ends in, and layout)", FilterKeystrokeBudget, "filter keystroke", () =>
        {
            board.SearchText = SearchText;
            board.Render();
            Settle(window);
        });
        NoteChanges(board);
        int found = board.Shelves[0].Cards.Count;
        Require(!requireChanges || (found > 0 && found < pipeline), $"The search left some of the Pipeline's cards and took others ({found} of {pipeline}).");
        RequireShelf(view, "PipelineShelf", found, "with the search");
        Paint(library, SearchKeystrokePaint, "Its frame after the search", window);
        board.SearchText = string.Empty;
        board.Render();
        Settle(window);
        RequireShelf(view, "PipelineShelf", pipeline, "with the search cleared");
        _frame(window);

        // Scrolling the Published shelf from top to bottom, a page at a time.
        board.SelectedShelfIndex = 1;
        Settle(window);
        _frame(window);
        ScrollViewer scroller = view.FindControl<ShelfView>("PublishedShelf")?.FindControl<ScrollViewer>("Scroller")
            ?? throw new InvalidOperationException("The Published shelf has no scroller.");
        scroller.Offset = default;
        Settle(window);
        double longestWall = 0;
        double longestUi = 0;
        double total = 0;
        int steps = 0;
        int madeBefore = CardView.MadeCount;
        while (scroller.Offset.Y + scroller.Viewport.Height < scroller.Extent.Height - 0.5 && steps < 2000)
        {
            double next = Math.Min(scroller.Offset.Y + scroller.Viewport.Height, scroller.Extent.Height - scroller.Viewport.Height);
            using var probe = new UiProbe();
            var watch = Stopwatch.StartNew();
            scroller.Offset = new Vector(0, next);
            Settle(window);
            _frame(window);
            watch.Stop();
            double wall = watch.Elapsed.TotalMilliseconds;
            longestWall = Math.Max(longestWall, wall);
            longestUi = Math.Max(longestUi, Math.Min(wall, probe.Finish()));
            total += wall;
            steps++;
        }

        Require(!requireChanges || steps > 1, $"The Published shelf scrolled ({steps} steps).");
        Add(new MeasuredRow(library, ScrollPublished, "Scroll the Published shelf top to bottom, a page at a time: its longest step (offset, layout, frame)",
            longestWall, longestUi, UiThreadBudget, "UI thread", Note: $"{steps} steps, {total:0} ms in all; {CardView.MadeCount - madeBefore} card views made"));
        board.SelectedShelfIndex = 0;
        Settle(window);
        _frame(window);

        // Report-only, last, so no row above changes: a hidden shelf shown
        // before does not refill its rows while hidden (ShelfViewModel.IsShown),
        // so a search made while Published is hidden is paid for at the switch
        // that shows it. The search set with Pipeline shown, then the switch
        // timed, then the search cleared and the Board put back.
        board.SearchText = SearchText;
        board.Render();
        Settle(window);
        List<int> hiddenChanges = board.LastSyncChanges.ToList();
        Require(!requireChanges || hiddenChanges[1] != 0, "The search changed the hidden Published shelf's cards.");
        Require(!requireChanges || board.Shelves[1].RowsAreStale == ShelfLayout.UsesRows, ShelfLayout.UsesRows
            ? "The hidden Published shelf's rows waited for the switch."
            : "No rows wait under option (a).");
        _frame(window);
        string atSwitch = ShelfLayout.UsesRows ? "its rows brought to its cards at the switch" : "no rows wait";
        Time(library, SwitchToPublishedAfterHiddenChange, $"Switch to Published after a search made while it was hidden (\"{SearchText}\"; {atSwitch}, option ({Option}))", PageSwitchBudget, "page switch", () =>
        {
            board.SelectedShelfIndex = 1;
            Settle(window);
        }, informational: true, note: "not a row of d1 6.3; compare with the same sequence on a build where hidden rows did not wait (f51b5e2), which is over this budget too");
        Require(!board.Shelves[1].RowsAreStale, "Published's rows are current once it is shown.");
        RequireShelf(view, "PublishedShelf", -1, "after a search made while it was hidden");
        _frame(window);
        board.SearchText = string.Empty;
        board.Render();
        board.SelectedShelfIndex = 0;
        Settle(window);
        RequireShelf(view, "PipelineShelf", pipeline, "at the end");
    }
    public MeasuredRow Time(string library, string id, string action, double budget, string budgetName, Action work, bool informational = false, string note = "")
    {
        ArgumentNullException.ThrowIfNull(work);
        int madeBefore = CardView.MadeCount;
        using var probe = new UiProbe();
        var watch = Stopwatch.StartNew();
        work();
        watch.Stop();
        double wall = watch.Elapsed.TotalMilliseconds;
        double ui = Math.Min(wall, probe.Finish());
        int made = CardView.MadeCount - madeBefore;
        if (made > 0)
        {
            note = string.IsNullOrEmpty(note) ? $"{made} card views made" : $"{note}; {made} card views made";
        }

        var row = new MeasuredRow(library, id, action, wall, ui, budget, budgetName, informational, note);
        _rows.Add(row);
        return row;
    }

    // Runs the interface thread's queued work and lays the window out.
    public static void Settle(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    // Lets the dispatcher run until done holds or the limit passes; returns done.
    public static bool PumpUntil(Func<bool> done, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(done);
        if (done())
        {
            return true;
        }

        var frame = new DispatcherFrame();
        var clock = Stopwatch.StartNew();
        var poll = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Background, Dispatcher.UIThread, (_, _) =>
        {
            if (done() || clock.Elapsed >= limit)
            {
                frame.Continue = false;
            }
        });
        poll.Start();
        try
        {
            Dispatcher.UIThread.PushFrame(frame);
        }
        finally
        {
            poll.Stop();
        }

        return done();
    }

    private void Paint(string library, string id, string action, Window window) =>
        Time(library, id, action, UiThreadBudget, "UI thread", () => _frame(window));

    // The card views a shelf has made.
    private static int Views(BoardView view, string shelf) =>
        (view.FindControl<ShelfView>(shelf) ?? throw new InvalidOperationException($"The Board has no shelf {shelf}.")).CardViews().Count;

    // The shelf shows expected cards (any number when expected is negative).
    // Under option (a) it has made a view for each card it shows; under (b)
    // a view for some of them (the rows made), for one at least when it shows
    // any, and each view shows a card of the shelf.
    private static void RequireShelf(BoardView view, string shelf, int expected, string context)
    {
        ShelfView shelfView = view.FindControl<ShelfView>(shelf) ?? throw new InvalidOperationException($"The Board has no shelf {shelf}.");
        var model = (shelfView.DataContext as ShelfViewModel)
            ?? throw new InvalidOperationException($"The shelf {shelf} has no view model.");
        IReadOnlyList<CardView> made = shelfView.CardViews();
        int views = made.Count;
        Require(expected < 0 || model.Cards.Count == expected, $"The {model.Name} shelf shows {model.Cards.Count} cards {context}, not {expected}.");
        if (!ShelfLayout.UsesRows)
        {
            Require(views == model.Cards.Count, $"The {model.Name} shelf made {views} card views for its {model.Cards.Count} cards {context}.");
            return;
        }

        var shown = new HashSet<CardViewModel>(model.Cards, ReferenceEqualityComparer.Instance);
        Require(views <= model.Cards.Count && (views > 0) == (model.Cards.Count > 0),
            $"The {model.Name} shelf made {views} card views for its {model.Cards.Count} cards {context}.");
        Require(made.All(card => card.DataContext is CardViewModel cardModel && shown.Contains(cardModel)),
            $"A card view of the {model.Name} shelf shows a card the shelf does not show {context}.");
    }

    // Notes on the last row what the last render did to each shelf's cards:
    // the number of changes SyncInto made, or a reset (d1 3.2 item 8).
    public void NoteChanges(BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);
        string[] names = ["Pipeline", "Published", "File Drawer"];
        IReadOnlyList<int> changes = board.LastSyncChanges;
        string text = "cards changed: " + string.Join(", ", changes.Select((count, index) =>
            names[Math.Min(index, names.Length - 1)] + " " + (count < 0 ? "reset" : count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        _rows[^1] = _rows[^1] with { Note = string.IsNullOrEmpty(_rows[^1].Note) ? text : $"{text}; {_rows[^1].Note}" };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // Ticks on the interface thread while an action runs; the longest gap
    // between ticks is the longest time a key press could not have been
    // handled. The timer runs at Input priority, below Default, Loaded and
    // Render, so it ticks only where input could be served, as today's
    // WM_TIMER (the lowest-priority message) does; a tick at Normal would land
    // between the layout and render jobs a synchronous action queues and
    // halve the stall a person feels.
    public static readonly DispatcherPriority ProbePriority = DispatcherPriority.Input;

    private sealed class UiProbe : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private double _lastTick;
        private double _longestGap;

        public UiProbe()
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), ProbePriority, Dispatcher.UIThread, (_, _) =>
            {
                double now = _watch.Elapsed.TotalMilliseconds;
                _longestGap = Math.Max(_longestGap, now - _lastTick);
                _lastTick = now;
            });
            _timer.Start();
        }

        public double Finish()
        {
            _timer.Stop();
            return Math.Max(_longestGap, _watch.Elapsed.TotalMilliseconds - _lastTick);
        }

        public void Dispose() => _timer.Stop();
    }
}
