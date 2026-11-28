using System.Diagnostics;
using System.Runtime;
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
// The shelves draw rows of N (d1 4.4 option (b)), with only the rows on
// screen made. Rows the 500 library cannot meet stay measured and reported,
// and are listed in WaitingForRowsOfN; every other row is held.
//
// Beside Wall and UI, each row records the busy-PC figures (busy-PC design,
// sections 2 to 4), report-only: no verdict reads them. The interface
// thread's clock (ThreadClock), the process's garbage-collection pauses and
// the thread's compilation time are read just after the stopwatch starts and
// just before it stops; the probe reads the clock at each tick, so Run UI is
// the longest running time inside one gap. Nothing is read between rows but
// these; the machine's load is read around the run (RunConditions), never here.
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

    // The shelves' layout as the JSON's "option" names it: "b", rows of N
    // (d1 4.4 option (b)), the only one since the every-card layout ("a")
    // was removed. Kept so runs before and after the removal read alike.
    public const string Option = "b";

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

    // Report-only, not rows of d1 6.3, the last two rows: the search typed on
    // Pipeline while Published is hidden after the scroll showed it, then the
    // switch to Published after that search. Compared only between builds.
    public const string SearchKeystrokePublishedHidden = "search-keystroke-published-hidden";
    public const string SwitchToPublishedAfterHiddenChange = "switch-published-after-hidden-change";

    // Report-only, measured only in a run told an idle side (IdleSides;
    // idle-preparation design, section 8, rows 1, 3, 4 and 5; busy-PC design,
    // section 9), in place of the first switch and its paint, so every row
    // after them keeps its work and order: the preparation from its start to
    // done, each step followed by a frame; its longest step, the row judged
    // in a real window against the UI thread budget; then, after the same
    // fixed idle period and one frame on every side, the first switch and its
    // paint. Compared only between the two sides of a sitting. A run told no
    // side measures today's first-switch-published instead: a first switch
    // happens once per window.
    public const string PreparePublishedIdle = "prepare-published-idle";
    public const string PreparePublishedLongestStep = "prepare-published-longest-step";
    public const string FirstSwitchToPublishedAfterIdle = "first-switch-published-after-idle";
    public const string FirstSwitchToPublishedAfterIdlePaint = "first-switch-published-after-idle-paint";

    // The idle rows in the order they are measured, on a side of the switch
    // scenario (IdleSides.ScenarioOf).
    public static IReadOnlyList<string> IdleRows { get; } =
    [
        PreparePublishedIdle,
        PreparePublishedLongestStep,
        FirstSwitchToPublishedAfterIdle,
        FirstSwitchToPublishedAfterIdlePaint,
    ];

    // Report-only rows of the other scenarios (IdleSides; idle-preparation
    // design, section 8, rows 6, 8, 9, 10 and 12, and row 5's refresh), each
    // written on every side of its scenario, between the two preparation rows
    // and the first switch after idle, so the rows after the switch keep
    // their work and order. Compared only between the two sides of a sitting.
    // key: the search keystroke started when the last step is due (row 6).
    public const string SearchKeystrokeStepDue = "search-keystroke-step-due";

    // timer: a search keystroke after a 0.5 s pause in a search, with the
    // idle timer on (row 6), then the timer's own refresh after it.
    public const string SearchKeystrokeAfterPause = "search-keystroke-after-pause";
    public const string RefreshPublishedTimer = "refresh-published-timer";

    // search: the search typed while Published is prepared and hidden, the
    // idle refresh after it (on search-refreshed only), the search cleared,
    // and the refresh after that (rows 8 and 10).
    public const string SearchKeystrokePublishedPrepared = "search-keystroke-published-prepared";
    public const string RefreshPublishedAfterSearch = "refresh-published-after-search";
    public const string SearchClearedPublishedPrepared = "search-cleared-published-prepared";
    public const string RefreshPublishedAfterClear = "refresh-published-after-clear";

    // maximise and scaling: the change just before the first switch (row 12).
    public const string MaximiseWindow = "maximise-window";
    public const string ScalingChange = "scaling-change";

    // theme: a theme change while Published is prepared, and the refresh
    // after it (row 9).
    public const string ThemeChange = "theme-change";
    public const string RefreshPublishedAfterTheme = "refresh-published-after-theme";

    // The refresh rows: like the preparation rows, they time work the idle
    // refresh moves out of the switch, so they read about 0 on a side that
    // does not refresh, by design.
    public static IReadOnlyList<string> RefreshRows { get; } =
    [
        RefreshPublishedTimer,
        RefreshPublishedAfterSearch,
        RefreshPublishedAfterClear,
        RefreshPublishedAfterTheme,
    ];

    // The rows a scenario adds before its first switch, in order.
    public static IReadOnlyList<string> ScenarioRows(string scenario) => scenario switch
    {
        IdleSides.SwitchScenario => [],
        IdleSides.KeyScenario => [SearchKeystrokeStepDue],
        IdleSides.TimerScenario => [SearchKeystrokeAfterPause, RefreshPublishedTimer],
        IdleSides.SearchScenario => [SearchKeystrokePublishedPrepared, RefreshPublishedAfterSearch, SearchClearedPublishedPrepared, RefreshPublishedAfterClear],
        IdleSides.MaximiseScenario => [MaximiseWindow],
        IdleSides.ScalingScenario => [ScalingChange],
        IdleSides.ThemeScenario => [ThemeChange, RefreshPublishedAfterTheme],
        _ => throw new ArgumentException($"No idle scenario is called {scenario}.", nameof(scenario)),
    };

    // The idle rows a run told side writes, in order: the two preparation
    // rows, its scenario's rows, then the first switch after idle and its
    // paint, which are left out when preparation stopped.
    public static IReadOnlyList<string> IdleRowsFor(string side, bool switchTaken = true) =>
    [
        PreparePublishedIdle,
        PreparePublishedLongestStep,
        .. ScenarioRows(IdleSides.ScenarioOf(side)),
        .. switchTaken ? new[] { FirstSwitchToPublishedAfterIdle, FirstSwitchToPublishedAfterIdlePaint } : [],
    ];

    // Every row a run told some side may write.
    public static IReadOnlyList<string> AllIdleRows { get; } =
        IdleSides.All.SelectMany(side => IdleRowsFor(side)).Distinct().ToList();

    // The frames drawn from the start of the idle period to the first
    // switch: one, and one more after a maximise or a scaling change, which
    // come after the idle period so that the switch follows them within 0.4 s.
    public static int FramesBeforeSwitchFor(string side) =>
        IdleSides.ScenarioOf(side) is IdleSides.MaximiseScenario or IdleSides.ScalingScenario ? 2 : 1;

    // The note every idle row carries.
    public const string BetweenSides = "not a row of d1 6.3; compared only between the two sides of a sitting";

    // The fixed idle period every side waits, after preparation is done (at
    // once on the side none) and before the frame drawn ahead of the switch,
    // so the pairs separate the preparation from warm-up during the wait.
    public static readonly TimeSpan IdlePeriod = TimeSpan.FromSeconds(1);

    // The pause before the key of the timer scenario (row 6: "a key typed
    // after a 0.5 s pause in a search").
    public static readonly TimeSpan PausePeriod = TimeSpan.FromMilliseconds(500);

    // How long the timer scenario waits for the idle timer to prepare or
    // refresh Published, and a maximise or its restore for the window to
    // take its new size, before the run stops with an error.
    public static readonly TimeSpan TimerLimit = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ResizeLimit = TimeSpan.FromSeconds(5);

    // The scaling the scaling scenario changes to.
    public const double ChangedScaling = 1.25;

    // While the timer scenario waits headless, a frame is drawn this often:
    // the idle timer paces its steps by drawn frames.
    private static readonly TimeSpan s_drawInterval = TimeSpan.FromMilliseconds(16);

    // Under rows of N (design step 13), the rows the 500 library still does
    // not meet; every other row is held. Medians of five headless Release
    // runs on the trial's development PC, 2026-11-20, each in its own process,
    // with the probe at Input priority, as today's WM_TIMER (UI ms against the
    // budget, the every-card layout of option (a), since removed, the same
    // day in brackets): warm startup 481 / 1,500
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
    public static IReadOnlyList<string> WaitingForRowsOfN { get; } =
    [
        FirstSwitchToPublished,
        StageToSubmitted,
        SearchKeystroke,
        ScrollPublished,
    ];

    private static readonly TimeSpan s_loadLimit = TimeSpan.FromMinutes(2);

    private readonly Action<Window> _frame;

    // The frames _frame has drawn, for the idle record's frames before the
    // switch.
    private int _framesDrawn;
    private readonly ThreadClock _clock;
    private readonly ProcessorClasses? _cores = ProcessorClasses.Current;
    private readonly List<MeasuredRow> _rows = [];
    private readonly List<IdlePreparationRecord> _idle = [];
    private readonly string? _afterIdle;

    // frame draws one frame of the window and returns once it is drawn. Made
    // on the interface thread before the first row: the thread clock opens
    // here, with its first rate check (untimed).
    public BoardMeasurement(Action<Window> frame)
        : this(frame, ThreadClock.Open())
    {
    }

    // clock must be the interface thread's own.
    public BoardMeasurement(Action<Window> frame, ThreadClock clock)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(clock);
        _frame = window =>
        {
            frame(window);
            _framesDrawn++;
        };
        _clock = clock;
    }

    public IReadOnlyList<MeasuredRow> Rows => _rows;

    public ThreadClock Clock => _clock;

    // The frame action waits for the screen (a real window), so the scroll
    // row, which draws a frame in each step, waits by design.
    public bool FrameWaits { get; init; }

    // The run's side of a with-and-without comparison of idle preparation
    // (IdleSides), or null for today's run.
    public string? AfterIdle
    {
        get => _afterIdle;
        init
        {
            if (value is not null && !IdleSides.IsKnown(value))
            {
                throw new ArgumentException($"No idle side is called {value}; the sides are {string.Join(", ", IdleSides.All)}.", nameof(value));
            }

            _afterIdle = value;
        }
    }

    // What idle preparation did before each library's first switch, in a run
    // told a side; empty otherwise.
    public IReadOnlyList<IdlePreparationRecord> IdlePreparation => _idle;

    // The maximise scenario's change and its undoing. A real window is
    // maximised and restored; the headless platform has no screen to
    // maximise to, so its tests give a resize to a larger size instead.
    public Action<Window> Maximise { get; init; } = static window => window.WindowState = WindowState.Maximized;

    public Action<Window> Restore { get; init; } = static window => window.WindowState = WindowState.Normal;

    // The scaling scenario's change: only the headless platform can set a
    // window's scaling, so a real window has none and refuses that scenario.
    public Action<Window, double>? SetScaling { get; init; }

    // Draws a frame while the timer scenario waits; null in a real window,
    // whose frames are drawn by themselves. The headless tests force the
    // render timer here.
    public Action? DrawWhileWaiting { get; init; }

    // After the last row, on the interface thread: the clock's second rate
    // check. If it fails, every thread-clock figure of the run is dropped.
    public void Finish()
    {
        _clock.Close();
        if (_clock.IsPresent)
        {
            return;
        }

        for (int index = 0; index < _rows.Count; index++)
        {
            _rows[index] = _rows[index] with { RunMs = null, NotRunMs = null, RunUiMs = null };
        }
    }

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
        }, waits: true);

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

        // In every run, told a side or not: the Board's idle timer is off and
        // no preparation step has run, so no step can land inside today's
        // first-switch-published (a width step makes no card view, so the
        // check above would not see one) or before the measurement drives it.
        Require(!view.Preparer.Enabled, "The Board's idle timer is off in a measured run: the measurement drives any preparation itself.");
        Require(view.Preparer.StepsRun == 0, $"No idle preparation step ran before the first switch ({view.Preparer.StepsRun}).");

        if (AfterIdle is { } side)
        {
            // A run told an idle side: the preparation, the side's scenario,
            // the wait and the first switch after it, in place of the two
            // rows below.
            FirstSwitchAfterIdle(library, side, window, view, board, published, requireChanges);
        }
        else
        {
            // The first switch to Published makes its cards at the switch.
            Time(library, FirstSwitchToPublished, $"First switch to Published (the shelf's cards made at the switch, option ({Option}))", PageSwitchBudget, "page switch", () =>
            {
                board.SelectedShelfIndex = 1;
                Settle(window);
            });
            RequireShelf(view, "PublishedShelf", published, "after the first switch");
            Paint(library, FirstSwitchToPublishedPaint, "Published: its frame after the first switch", window);
        }

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
        double total = 0;
        int steps = 0;
        int madeBefore = CardView.MadeCount;
        Step? slowest = null;
        while (scroller.Offset.Y + scroller.Viewport.Height < scroller.Extent.Height - 0.5 && steps < 2000)
        {
            double next = Math.Min(scroller.Offset.Y + scroller.Viewport.Height, scroller.Extent.Height - scroller.Viewport.Height);
            Step step = TimeStep(() =>
            {
                scroller.Offset = new Vector(0, next);
                Settle(window);
                _frame(window);
            });
            longestWall = Math.Max(longestWall, step.WallMs);
            if (slowest is null || step.UiMs > slowest.UiMs)
            {
                slowest = step;
            }

            total += step.WallMs;
            steps++;
        }

        // Wall is the longest step's and UI the longest-UI step's, as before;
        // every busy-PC figure comes from the longest-UI step, and its time
        // not running from that step's own Wall.
        Require(!requireChanges || steps > 1, $"The Published shelf scrolled ({steps} steps).");
        string slowestNote = slowest is null ? string.Empty : $"; the longest-UI step's Wall {slowest.WallMs:0} ms";
        Add(new MeasuredRow(library, ScrollPublished, "Scroll the Published shelf top to bottom, a page at a time: its longest step (offset, layout, frame)",
            longestWall, slowest?.UiMs ?? 0, UiThreadBudget, "UI thread", Note: $"{steps} steps, {total:0} ms in all; {CardView.MadeCount - madeBefore} card views made{slowestNote}",
            RunMs: slowest?.RunMs, NotRunMs: slowest?.NotRunMs, RunUiMs: slowest?.RunUiMs, PerfCoreShare: slowest?.PerfCoreShare,
            GcPauseMs: slowest?.GcPauseMs, JitMs: slowest?.JitMs, Waits: FrameWaits));
        board.SelectedShelfIndex = 0;
        Settle(window);
        _frame(window);

        // Report-only, last, so no row above changes (busy-PC design, section
        // 9). Back on Pipeline, Published hidden after the scroll showed it,
        // with no stage row between: the search timed with the same work as
        // the search row, then the switch that shows Published again, then the
        // search cleared and the Board put back. Each figure is compared only
        // between builds, never with a budget row.
        const string betweenBuilds = "not a row of d1 6.3; compared only between builds";
        Require(board.SelectedShelfIndex == 0 && board.Shelves[1].IsRealized, "Published was shown before and is hidden at the search.");
        Time(library, SearchKeystrokePublishedHidden, $"Search keystroke to rendered Board with Published hidden after it was shown (\"{SearchText}\" typed into an empty search, then the render its wait ends in, and layout)", FilterKeystrokeBudget, "filter keystroke", () =>
        {
            board.SearchText = SearchText;
            board.Render();
            Settle(window);
        }, informational: true, note: betweenBuilds);
        NoteChanges(board);
        Require(!requireChanges || board.LastSyncChanges[1] != 0, "The search changed the hidden Published shelf's cards.");
        int foundHidden = board.Shelves[0].Cards.Count;
        Require(!requireChanges || (foundHidden > 0 && foundHidden < pipeline), $"The search with Published hidden left some of the Pipeline's cards and took others ({foundHidden} of {pipeline}).");
        RequireShelf(view, "PipelineShelf", foundHidden, "with the search made while Published was hidden");
        _frame(window);
        Time(library, SwitchToPublishedAfterHiddenChange, $"Switch to Published after a search made while it was hidden (\"{SearchText}\", option ({Option}))", PageSwitchBudget, "page switch", () =>
        {
            board.SelectedShelfIndex = 1;
            Settle(window);
        }, informational: true, note: betweenBuilds);
        Require(board.SelectedShelfIndex == 1, "The switch showed Published.");
        RequireShelf(view, "PublishedShelf", -1, "after a search made while it was hidden");
        _frame(window);
        board.SearchText = string.Empty;
        board.Render();
        board.SelectedShelfIndex = 0;
        Settle(window);
        RequireShelf(view, "PipelineShelf", pipeline, "at the end");
    }

    // The idle rows of a run told side (IdleRowsFor). The Board's idle timer
    // stays off, but on the side timer-on: the measurement drives the
    // preparation with RunStep, so no step can run inside another row, and
    // the warm-startup row's note says how many ran inside it (none). Each
    // step is timed on its own and followed by a frame, as the timer paces
    // them; the steps run are the side's (IdleSides.StepsFor), or every step
    // due until the Board says Published is prepared. On timer-on the Board's
    // own idle timer prepares Published instead, timed from the moment it is
    // turned on, just after the first frame, to prepared (row 5's time to
    // prepared), and is turned off again before the first switch. Then the
    // side's scenario (IdleSides): its rows, before the idle period, or after
    // it for a maximise or a scaling change, which the switch must follow
    // within 0.4 s. Then, on every side alike, the fixed idle period and one
    // frame; then the first switch. When preparation stopped (the preparer's
    // SkippedReason: a slow step or an accessibility client), the switch is
    // not the one the row is about: it is made untimed, so every row after it
    // does the same work, and the record says why (idle-preparation design,
    // section 8, row 1). The scenario's own rows are written either way.
    // Nothing here throws on a stop; the checks throw only when preparation
    // did not do what it says. After the switch the scenario's change is
    // undone (the search cleared, the window restored, the scaling and the
    // theme put back), untimed, so the rows after it run on the Board every
    // other run has.
    private void FirstSwitchAfterIdle(string library, string side, MainWindow window, BoardView view, BoardViewModel board, int published, bool requireChanges)
    {
        HiddenShelfPreparer preparer = view.Preparer;
        string scenario = IdleSides.ScenarioOf(side);
        bool byTimer = IdleSides.ByTimer(side);

        // MeasureBoard has required that none ran; the note says so in the
        // report, beside warm startup.
        int startup = _rows.FindLastIndex(row => row.Library == library && row.Id == WarmStartup);
        if (startup >= 0)
        {
            _rows[startup] = _rows[startup] with { Note = Join(_rows[startup].Note, FormattableString.Invariant($"{preparer.StepsRun} idle preparation steps inside")) };
        }

        // Row 9: the managed heap after a full collection, before and after
        // preparation, on the theme scenario only (a full collection changes
        // what the rows after it meet, so no other scenario takes one).
        long? memoryBefore = scenario == IdleSides.ThemeScenario ? GC.GetTotalMemory(forceFullCollection: true) : null;

        int? limit = IdleSides.StepsFor(side);
        var steps = new List<DrivenStep>();
        var driven = new List<DrivenStep>();
        int madeBefore = CardView.MadeCount;
        if (byTimer)
        {
            Time(library, PreparePublishedIdle, $"Idle preparation of Published, side {side}: by the Board's own idle timer, from the moment it is turned on, just after the first frame, to Published prepared (its 0.4 s of quiet, its ticks and the frame after each step included)", FilterKeystrokeBudget, "filter keystroke", () =>
            {
                preparer.Enabled = true;
                if (!WaitWhileDrawing(() => preparer.IsPrepared || preparer.SkippedReason is not null, TimerLimit))
                {
                    throw new TimeoutException($"The idle timer neither prepared Published nor stopped within {TimerLimit.TotalSeconds:0} s ({preparer.StepsRun} steps).");
                }
            }, informational: true, waits: true);
        }
        else
        {
            Time(library, PreparePublishedIdle, $"Idle preparation of Published, side {side}: from its start to done, each step followed by a frame", FilterKeystrokeBudget, "filter keystroke", () =>
                DriveSteps(preparer, window, limit, steps), informational: true, waits: true);
            driven.AddRange(steps);
        }

        int stepCount = byTimer ? preparer.StepsRun : steps.Count;
        int viewsMade = CardView.MadeCount - madeBefore;
        string? skipped = preparer.SkippedReason;
        _rows[^1] = _rows[^1] with
        {
            Note = FormattableString.Invariant($"{BetweenSides}; {(byTimer ? "by the idle timer: " : string.Empty)}{stepCount} steps, {viewsMade} card views made; preparation {Stopped(skipped)}; UI is the longest wait a key would meet, read against the filter keystroke budget for scale"),
        };

        if (byTimer)
        {
            // The timer's steps are timed by the preparer's own clock; each is
            // one interface-thread job, so no key is served inside it and its
            // UI is its Wall. The busy-PC figures are not read per step.
            HiddenShelfPreparer.Step? longest = preparer.Steps.Count == 0 ? null : preparer.Steps.MaxBy(step => step.Time);
            double ms = longest?.Time.TotalMilliseconds ?? 0;
            string timerList = preparer.Steps.Count == 0 ? "none" : string.Join("; ", preparer.Steps.Select(DescribeTimerStep));
            Add(new MeasuredRow(library, PreparePublishedLongestStep, $"Idle preparation of Published, side {side}: its longest step (W the width, R the rows, L a layout) by the preparer's own clock, one interface-thread job, judged in a real window against the UI thread budget",
                ms, ms, UiThreadBudget, "UI thread", Informational: true,
                Note: FormattableString.Invariant($"{BetweenSides}; {stepCount} steps: {timerList}; preparation {Stopped(skipped)}")));
        }
        else
        {
            // The longest step: Wall the longest step's, UI the longest-UI
            // step's, and every busy-PC figure from that step, as the scroll row.
            Step? slowest = null;
            double longestWall = 0;
            foreach (DrivenStep step in steps)
            {
                longestWall = Math.Max(longestWall, step.Timed.WallMs);
                if (slowest is null || step.Timed.UiMs > slowest.UiMs)
                {
                    slowest = step.Timed;
                }
            }

            string stepList = steps.Count == 0 ? "none" : string.Join("; ", steps.Select(DescribeStep));
            string slowestNote = slowest is null ? string.Empty : FormattableString.Invariant($"; the longest-UI step's Wall {slowest.WallMs:0} ms");
            Add(new MeasuredRow(library, PreparePublishedLongestStep, $"Idle preparation of Published, side {side}: its longest step (W the width, R the rows, L a layout), judged in a real window against the UI thread budget",
                longestWall, slowest?.UiMs ?? 0, UiThreadBudget, "UI thread", Informational: true,
                Note: FormattableString.Invariant($"{BetweenSides}; {steps.Count} steps: {stepList}; preparation {Stopped(skipped)}{slowestNote}"),
                RunMs: slowest?.RunMs, NotRunMs: slowest?.NotRunMs, RunUiMs: slowest?.RunUiMs, PerfCoreShare: slowest?.PerfCoreShare,
                GcPauseMs: slowest?.GcPauseMs, JitMs: slowest?.JitMs));
        }

        if (skipped is null)
        {
            Require(limit is null ? preparer.IsPrepared : steps.Count == limit,
                limit is null ? "Preparation ran to its end and Published is prepared." : $"The side {side} ran its {limit} steps ({steps.Count}).");
        }

        long? memoryAfter = scenario == IdleSides.ThemeScenario ? GC.GetTotalMemory(forceFullCollection: true) : null;

        // The scenario's rows before the idle period.
        int? stepsMet = side == IdleSides.MidStep ? 0 : null;
        int? stepsMetViews = side == IdleSides.MidStep ? 0 : null;
        int? refreshSteps = null;
        bool searchInEffect = false;
        ThemeVariant? themeBefore = window.RequestedThemeVariant;
        bool themeChanged = false;
        switch (scenario)
        {
            case IdleSides.KeyScenario:
            {
                // Row 6: the key arrives as the last step is due, so the step
                // runs first and the key waits for it (a step cannot stop
                // early). On key-none no step is due.
                int stepsBefore = preparer.StepsRun;
                int stepViews = 0;
                Time(library, SearchKeystrokeStepDue, $"Search keystroke started when an idle step is due (side {side}): the step, if one is due, then \"{SearchText}\" typed into an empty search, the render its wait ends in, and layout", FilterKeystrokeBudget, "filter keystroke", () =>
                {
                    if (IdleSides.Prepares(side))
                    {
                        int before = CardView.MadeCount;
                        preparer.RunStep();


                        stepViews = CardView.MadeCount - before;
                    }

                    board.SearchText = SearchText;
                    board.Render();
                    Settle(window);
                }, informational: true);
                stepsMet = preparer.StepsRun - stepsBefore;
                stepsMetViews = stepViews;
                _rows[^1] = _rows[^1] with { Note = FormattableString.Invariant($"{BetweenSides}; {stepsMet} idle steps met ({StepKinds(preparer, stepsBefore)}), {stepViews} card views made by them") };
                NoteChanges(board);
                _frame(window);
                board.SearchText = string.Empty;
                board.Render();
                Settle(window);
                _frame(window);
                break;
            }

            case IdleSides.TimerScenario:
            {
                // Row 6: a key after a 0.5 s pause in a search, with the timer
                // on and Published prepared and hidden; a refresh waits for 2 s
                // after a render, so no step is expected in the pause or the
                // key. Then the timer's own refresh after the search.
                string partial = SearchText[..^1];
                board.SearchText = partial;
                board.Render();
                Settle(window);
                _frame(window);
                int stepsBefore = preparer.StepsRun;
                WaitWhileDrawing(static () => false, PausePeriod);
                Time(library, SearchKeystrokeAfterPause, FormattableString.Invariant($"Search keystroke after a pause of {PausePeriod.TotalSeconds:0.0} s in a search (side {side}: \"{partial}\" typed, the pause, then \"{SearchText}\"), Published {(byTimer ? "prepared and hidden with the idle timer on" : "never shown")}: the render its wait ends in, and layout"), FilterKeystrokeBudget, "filter keystroke", () =>
                {
                    board.SearchText = SearchText;
                    board.Render();
                    Settle(window);
                }, informational: true);
                stepsMet = preparer.StepsRun - stepsBefore;
                stepsMetViews = 0;
                _rows[^1] = _rows[^1] with { Note = FormattableString.Invariant($"{BetweenSides}; {stepsMet} idle steps ran in the pause and the key ({StepKinds(preparer, stepsBefore)}); none expected: a refresh waits for 2 s after a render") };
                NoteChanges(board);
                _frame(window);

                int refreshBefore = preparer.StepsRun;
                int refreshMade = CardView.MadeCount;
                Time(library, RefreshPublishedTimer, $"The idle timer's refresh of Published after the search (side {side}): from the key's frame to Published prepared again (the timer's 2 s of quiet after the render, its ticks and the frame after each step included)", UiThreadBudget, "UI thread", () =>
                {
                    if (byTimer && preparer.SkippedReason is null && !WaitWhileDrawing(() => preparer.IsPrepared || preparer.SkippedReason is not null, TimerLimit))
                    {
                        throw new TimeoutException($"The idle timer neither refreshed Published nor stopped within {TimerLimit.TotalSeconds:0} s.");
                    }
                }, informational: true, waits: true);
                refreshSteps = preparer.StepsRun - refreshBefore;
                _rows[^1] = _rows[^1] with { Note = FormattableString.Invariant($"{BetweenSides}; {refreshSteps} refresh steps ({StepKinds(preparer, refreshBefore)}), {CardView.MadeCount - refreshMade} card views made; preparation {Stopped(preparer.SkippedReason)}; UI is the longest wait a key would meet") };
                preparer.Enabled = false;
                searchInEffect = true;
                break;
            }

            case IdleSides.SearchScenario:
            {
                // Rows 8 and 10: the search typed while Published is prepared
                // and hidden (on the 500 library it leaves about a fifth of
                // Published), the refresh, the search cleared (Published's
                // cards back), the refresh; then the search typed again, and
                // its refresh, untimed, so the first switch comes after a
                // search typed while Published was prepared and hidden, with
                // the idle refresh done (search-refreshed) or before it.
                bool refresh = side == IdleSides.SearchRefreshed;
                refreshSteps = 0;
                SearchWhileHidden(library, SearchKeystrokePublishedPrepared, $"Search keystroke while Published is prepared and hidden (side {side}): \"{SearchText}\" typed into an empty search, the render its wait ends in, and layout", SearchText, window, board, requireChanges);
                refreshSteps += RefreshRow(library, RefreshPublishedAfterSearch, $"The idle refresh of Published after the search (side {side}; none on search-pending): its steps, each followed by a frame", refresh, preparer, window, driven);
                SearchWhileHidden(library, SearchClearedPublishedPrepared, $"The search cleared while Published is hidden (side {side}): the render, putting most of Published's cards back, and layout", string.Empty, window, board, requireChanges);
                refreshSteps += RefreshRow(library, RefreshPublishedAfterClear, $"The idle refresh of Published after the search was cleared (side {side}; none on search-pending): its steps, each followed by a frame", refresh, preparer, window, driven);
                board.SearchText = SearchText;
                board.Render();
                Settle(window);
                _frame(window);
                if (refresh)
                {
                    var again = new List<DrivenStep>();
                    DriveSteps(preparer, window, null, again);
                    driven.AddRange(again);
                    refreshSteps += again.Count;
                }

                searchInEffect = true;
                break;
            }

            case IdleSides.ThemeScenario:
            {
                // Row 9: a theme change while Published is prepared, then the
                // refresh it makes due (one full-size layout).
                ThemeVariant target = window.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
                Time(library, ThemeChange, $"Theme change to {target} (side {side}) while Published is {(IdleSides.Prepares(side) ? "prepared and hidden" : "never shown")}: restyling and layout", UiThreadBudget, "UI thread", () =>
                {
                    window.RequestedThemeVariant = target;
                    Settle(window);
                }, informational: true, note: BetweenSides);
                themeChanged = true;
                _frame(window);
                refreshSteps = RefreshRow(library, RefreshPublishedAfterTheme, $"The idle refresh of Published after the theme change (side {side}; none on theme-none): its steps, each followed by a frame", IdleSides.Prepares(side), preparer, window, driven);
                break;
            }
        }

        // Every side alike: the fixed idle period, then one frame. What the
        // wait did is recorded, the idle period it waited and the frames
        // drawn from its start to the switch, so a test can hold that every
        // side of a scenario waits alike.
        int framesBefore = _framesDrawn;
        var waited = Stopwatch.StartNew();
        PumpUntil(static () => false, IdlePeriod);
        waited.Stop();
        _frame(window);

        // The scenario's change after the idle period (row 12): a maximise
        // or a scaling change, its frame, then the switch at once, as within
        // 0.4 s of the change and before any refresh.
        Stopwatch? sinceChange = null;
        Action? undo = null;
        Panel host = view.FindControl<Panel>("ShelfHost") ?? throw new InvalidOperationException("The Board has no shelf host.");
        if (scenario == IdleSides.MaximiseScenario)
        {
            Size before = host.Bounds.Size;
            sinceChange = Stopwatch.StartNew();
            Time(library, MaximiseWindow, $"Maximise the window (side {side}) while Published is {(IdleSides.Prepares(side) ? "prepared and hidden" : "never shown")}: the window's new size and layout", UiThreadBudget, "UI thread", () =>
            {
                Maximise(window);
                Settle(window);
                PumpUntil(() => host.Bounds.Size != before, ResizeLimit);
                Settle(window);
            }, informational: true, note: BetweenSides, waits: true);
            Require(host.Bounds.Size != before, $"The maximise changed the shelf host's size ({before.Width:0} x {before.Height:0}); a window maximised already cannot run this scenario.");
            _frame(window);
            undo = () =>
            {
                Restore(window);
                Settle(window);
                PumpUntil(() => host.Bounds.Size == before, ResizeLimit);
                Settle(window);
                Require(host.Bounds.Size == before, $"The window was restored to its size before the maximise ({host.Bounds.Width:0} x {host.Bounds.Height:0}, not {before.Width:0} x {before.Height:0}).");
            };
        }
        else if (scenario == IdleSides.ScalingScenario)
        {
            Action<Window, double> setScaling = SetScaling ?? throw new InvalidOperationException($"The side {side} changes the display scaling, which only a headless run can do.");
            double scalingBefore = window.RenderScaling;
            sinceChange = Stopwatch.StartNew();
            Time(library, ScalingChange, FormattableString.Invariant($"Display scaling changed to {ChangedScaling:0.00} (side {side}) while Published is {(IdleSides.Prepares(side) ? "prepared and hidden" : "never shown")}: layout"), UiThreadBudget, "UI thread", () =>
            {
                setScaling(window, ChangedScaling);
                Settle(window);
            }, informational: true, note: BetweenSides);
            Require(Math.Abs(window.RenderScaling - ChangedScaling) < 0.001, FormattableString.Invariant($"The window's scaling changed to {ChangedScaling} ({window.RenderScaling})."));
            _frame(window);
            undo = () =>
            {
                setScaling(window, scalingBefore);
                Settle(window);
            };
        }

        skipped = preparer.SkippedReason;
        bool prepared = preparer.IsPrepared;
        bool rowsStale = board.Shelves[1].RowsAreStale;
        int cardsNow = board.Shelves[1].Cards.Count;
        Require(skipped is not null || side != IdleSides.Prepared || prepared, "Published was still prepared after the idle period.");
        int viewsBefore = Views(view, "PublishedShelf");
        Require(!prepared || cardsNow == 0 || viewsBefore > 0, $"The prepared Published shelf holds card views before its switch ({viewsBefore}).");
        int expectedCards = searchInEffect ? -1 : published;

        bool taken = skipped is null;
        int? madeAtSwitch = null;
        int framesBeforeSwitch = _framesDrawn - framesBefore;
        double? changeToSwitch = sinceChange?.Elapsed.TotalMilliseconds;
        if (taken)
        {
            int madeBeforeSwitch = CardView.MadeCount;
            int stepsBeforeSwitch = preparer.StepsRun;
            int stepViews = 0;
            string action = side == IdleSides.MidStep
                ? FormattableString.Invariant($"First switch to Published asked for as the last idle step starts (side {side}: the steps but the last, the wait ({IdlePeriod.TotalSeconds:0} s idle; one frame), then the last step, which the switch waits for, and the switch)")
                : FormattableString.Invariant($"First switch to Published after the side's steps (none on the side none, every step due on prepared) and the wait ({IdlePeriod.TotalSeconds:0} s idle; one frame)");
            Time(library, FirstSwitchToPublishedAfterIdle, action, PageSwitchBudget, "page switch", () =>
            {
                if (side == IdleSides.MidStep)
                {
                    int before = CardView.MadeCount;
                    preparer.RunStep();


                    stepViews = CardView.MadeCount - before;
                }

                board.SelectedShelfIndex = 1;
                Settle(window);
            }, informational: true);
            madeAtSwitch = CardView.MadeCount - madeBeforeSwitch;
            if (side == IdleSides.MidStep)
            {
                stepsMet = preparer.StepsRun - stepsBeforeSwitch;
                stepsMetViews = stepViews;
            }

            if (preparer.SkippedReason is not null)
            {
                // The step inside the row stopped preparation: the row is not
                // the one it is about, as when preparation stops before it.
                _rows.RemoveAt(_rows.Count - 1);
                taken = false;
                madeAtSwitch = null;
                RequireShelf(view, "PublishedShelf", expectedCards, "after the first switch after idle (preparation stopped inside it, so not timed)");
                _frame(window);
            }
            else
            {
                string met = side == IdleSides.MidStep ? FormattableString.Invariant($"; {stepsMet} idle step run inside the row first ({StepKinds(preparer, stepsBeforeSwitch)}, {stepViews} card views), as the switch waited for it") : string.Empty;
                string scenarioNote = scenario == IdleSides.SwitchScenario ? string.Empty : FormattableString.Invariant($", Published's rows {(rowsStale ? "stale" : "current")}");
                _rows[^1] = _rows[^1] with
                {
                    Note = FormattableString.Invariant($"{BetweenSides}; side {side}: {stepCount} preparation steps before it, {viewsBefore} card views ready in the hidden shelf, Published {(prepared ? "prepared" : "not prepared")}{scenarioNote}{met}; {madeAtSwitch} card views made at the switch"),
                };
                RequireShelf(view, "PublishedShelf", expectedCards, "after the first switch after idle");
                Paint(library, FirstSwitchToPublishedAfterIdlePaint, "Published: its frame after the first switch after idle", window, informational: true, note: BetweenSides);
            }
        }
        else
        {
            board.SelectedShelfIndex = 1;
            Settle(window);
            RequireShelf(view, "PublishedShelf", expectedCards, "after the first switch after idle (preparation stopped, so not timed)");
            _frame(window);
        }

        // The scenario's change undone, untimed.
        if (searchInEffect)
        {
            board.SearchText = string.Empty;
            board.Render();
            Settle(window);
            RequireShelf(view, "PublishedShelf", published, "with the search cleared after the first switch after idle");
            _frame(window);
        }

        if (undo is not null)
        {
            undo();
            _frame(window);
        }

        if (themeChanged)
        {
            window.RequestedThemeVariant = themeBefore;
            Settle(window);
            _frame(window);
        }

        _idle.Add(new IdlePreparationRecord(library, side, stepCount, viewsMade, prepared, preparer.SkippedReason, taken, madeAtSwitch, waited.Elapsed.TotalMilliseconds, framesBeforeSwitch)
        {
            SessionSteps = preparer.StepsRun,
            StepsMet = stepsMet,
            StepsMetViews = stepsMetViews,
            RefreshSteps = refreshSteps,
            RowsStaleAtSwitch = rowsStale,
            ManagedBytesBefore = memoryBefore,
            ManagedBytesAfter = memoryAfter,
            StepFrameMs = driven.Select(step => step.FrameMs).ToList(),
            ChangeToSwitchMs = changeToSwitch,
        });
    }

    // Runs the preparer's steps, each timed as a row is timed and followed by
    // a frame, which is timed too (row 13), until limit steps ran (every step
    // due when limit is null) or none is due; adds each to into.
    private void DriveSteps(HiddenShelfPreparer preparer, Window window, int? limit, List<DrivenStep> into)
    {
        int ran = 0;
        while (limit is null || ran < limit)
        {
            bool stepped = false;
            Step timed = TimeStep(() => stepped = preparer.RunStep());
            if (!stepped)
            {
                break;
            }

            var frame = Stopwatch.StartNew();
            _frame(window);
            frame.Stop();
            into.Add(new DrivenStep(preparer.Steps[^1], timed, frame.Elapsed.TotalMilliseconds));
            ran++;
        }
    }

    // A row of idle refresh steps (the search and theme scenarios): every
    // step due, each followed by a frame, when run is set; nothing on a side
    // that does not refresh, so the row reads about 0 there, by design.
    // Returns the steps run.
    private int RefreshRow(string library, string id, string action, bool run, HiddenShelfPreparer preparer, Window window, List<DrivenStep> driven)
    {
        var steps = new List<DrivenStep>();
        int madeBefore = CardView.MadeCount;
        Time(library, id, action, UiThreadBudget, "UI thread", () =>
        {
            if (run)
            {
                DriveSteps(preparer, window, null, steps);
            }
        }, informational: true, waits: true);
        driven.AddRange(steps);
        string list = steps.Count == 0 ? string.Empty : ": " + string.Join("; ", steps.Select(DescribeStep));
        _rows[^1] = _rows[^1] with
        {
            Note = FormattableString.Invariant($"{BetweenSides}; {steps.Count} refresh steps{list}; {CardView.MadeCount - madeBefore} card views made; preparation {Stopped(preparer.SkippedReason)}; UI is the longest wait a key would meet"),
        };
        return steps.Count;
    }

    // A search typed or cleared while Published is hidden, timed as the
    // search keystroke row is, then its frame; on the 500 library it must
    // change the hidden Published shelf's cards.
    private void SearchWhileHidden(string library, string id, string action, string text, Window window, BoardViewModel board, bool requireChanges)
    {
        Time(library, id, action, FilterKeystrokeBudget, "filter keystroke", () =>
        {
            board.SearchText = text;
            board.Render();
            Settle(window);
        }, informational: true, note: BetweenSides);
        NoteChanges(board);
        Require(!requireChanges || board.LastSyncChanges[1] != 0, "The search changed the hidden Published shelf's cards.");
        _frame(window);
    }

    // Lets the dispatcher run until done holds or the limit passes, drawing a
    // frame every s_drawInterval when DrawWhileWaiting is set; returns done.
    private bool WaitWhileDrawing(Func<bool> done, TimeSpan limit)
    {
        var sinceDraw = Stopwatch.StartNew();
        return PumpUntil(() =>
        {
            if (DrawWhileWaiting is { } draw && sinceDraw.Elapsed >= s_drawInterval)
            {
                draw();
                sinceDraw.Restart();
            }

            return done();
        }, limit);
    }

    private static string Stopped(string? skipped) => skipped is null ? "not stopped" : "stopped: " + skipped;

    // The kinds of the steps run since the first, "none" for none.
    private static string StepKinds(HiddenShelfPreparer preparer, int first) =>
        preparer.StepsRun <= first ? "none" : string.Join(", ", preparer.Steps.Skip(first).Select(step => step.Kind + (step.Cap is null ? string.Empty : " capped")));

    // One step the measurement drove: what the preparer did, its timing, and
    // the frame drawn after it, in ms.
    private sealed record DrivenStep(HiddenShelfPreparer.Step Made, Step Timed, double FrameMs);

    // One step of the idle preparation, for the longest step's note: what it
    // was, an L's height cap on the ladder, its Wall and UI, the views made,
    // and the frame drawn after it.
    private static string DescribeStep(DrivenStep step)
    {
        string cap = step.Made.Cap is { } height ? FormattableString.Invariant($" (cap {height:0} px)") : string.Empty;
        return FormattableString.Invariant($"{step.Made.Kind}{cap} {step.Timed.WallMs:0.0} ms, UI {step.Timed.UiMs:0.0} ms, {step.Made.ViewsMade} views, frame {step.FrameMs:0.0} ms");
    }

    // One step the idle timer ran, by the preparer's own clock.
    private static string DescribeTimerStep(HiddenShelfPreparer.Step step)
    {
        string cap = step.Cap is { } height ? FormattableString.Invariant($" (cap {height:0} px)") : string.Empty;
        return FormattableString.Invariant($"{step.Kind}{cap} {step.Time.TotalMilliseconds:0.0} ms, {step.ViewsMade} views");
    }

    private static string Join(string note, string text) => string.IsNullOrEmpty(note) ? text : $"{note}; {text}";

    // waits marks a row that waits by design (a frame, a load off the
    // interface thread), so its time not running is not all load.
    public MeasuredRow Time(string library, string id, string action, double budget, string budgetName, Action work, bool informational = false, string note = "", bool waits = false)
    {
        ArgumentNullException.ThrowIfNull(work);
        int madeBefore = CardView.MadeCount;
        Step step = TimeStep(work);
        int made = CardView.MadeCount - madeBefore;
        if (made > 0)
        {
            note = string.IsNullOrEmpty(note) ? $"{made} card views made" : $"{note}; {made} card views made";
        }

        var row = new MeasuredRow(library, id, action, step.WallMs, step.UiMs, budget, budgetName, informational, note,
            step.RunMs, step.NotRunMs, step.RunUiMs, step.PerfCoreShare, step.GcPauseMs, step.JitMs, waits);
        _rows.Add(row);
        return row;
    }

    // The figures of one timed step. RunMs and RunUiMs are null when the
    // thread clock is absent; NotRunMs is WallMs - RunMs, not clamped.
    private sealed record Step(double WallMs, double UiMs, double? RunMs, double? RunUiMs, double? PerfCoreShare, double GcPauseMs, double JitMs)
    {
        public double? NotRunMs => RunMs is { } ran ? WallMs - ran : null;
    }

    // Times work as every row is timed: Wall by the stopwatch, UI by the
    // probe (never more than Wall), and inside the stopwatch the thread
    // clock, the GC pause total and the compilation time, read just after it
    // starts and just before it stops.
    private Step TimeStep(Action work)
    {
        using var probe = new UiProbe(_clock, _cores);
        var watch = Stopwatch.StartNew();
        double? runStart = _clock.Read();
        TimeSpan gcStart = GC.GetTotalPauseDuration();
        TimeSpan jitStart = JitInfo.GetCompilationTime(currentThread: true);
        probe.Begin(runStart);
        work();
        TimeSpan jitEnd = JitInfo.GetCompilationTime(currentThread: true);
        TimeSpan gcEnd = GC.GetTotalPauseDuration();
        double? runEnd = _clock.Read();
        watch.Stop();
        double wall = watch.Elapsed.TotalMilliseconds;
        double ui = Math.Min(wall, probe.Finish(runEnd));
        double? ran = runStart is { } from && runEnd is { } to ? to - from : null;
        return new Step(wall, ui, ran, ran is null ? null : probe.LongestRun, probe.PerfCoreShare,
            (gcEnd - gcStart).TotalMilliseconds, (jitEnd - jitStart).TotalMilliseconds);
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

    private void Paint(string library, string id, string action, Window window, bool informational = false, string note = "") =>
        Time(library, id, action, UiThreadBudget, "UI thread", () => _frame(window), informational, note, waits: true);

    // The card views a shelf has made.
    private static int Views(BoardView view, string shelf) =>
        (view.FindControl<ShelfView>(shelf) ?? throw new InvalidOperationException($"The Board has no shelf {shelf}.")).CardViews().Count;

    // The shelf shows expected cards (any number when expected is negative).
    // It has made a view for some of them (the rows made), for one at least
    // when it shows any, and each view shows a card of the shelf.
    private static void RequireShelf(BoardView view, string shelf, int expected, string context)
    {
        ShelfView shelfView = view.FindControl<ShelfView>(shelf) ?? throw new InvalidOperationException($"The Board has no shelf {shelf}.");
        var model = (shelfView.DataContext as ShelfViewModel)
            ?? throw new InvalidOperationException($"The shelf {shelf} has no view model.");
        IReadOnlyList<CardView> made = shelfView.CardViews();
        int views = made.Count;
        Require(expected < 0 || model.Cards.Count == expected, $"The {model.Name} shelf shows {model.Cards.Count} cards {context}, not {expected}.");
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

    //
    // Each tick also reads the thread clock once, so the probe knows the
    // longest running time inside one gap, the tail to the row's end
    // included: Run UI, which leaves out the time other programs had the
    // processor. It also notes which kind of core the thread is on at each
    // tick, and at the row's start and end, so a row with no tick inside it
    // still has a share.
    private sealed class UiProbe : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly ThreadClock _clock;
        private readonly ProcessorClasses? _cores;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private double _lastTick;
        private double _longestGap;
        private double? _lastRun;
        private double _longestRun;
        private bool _runLost;
        private int _coreReadings;
        private int _fastReadings;

        public UiProbe(ThreadClock clock, ProcessorClasses? cores)
        {
            _clock = clock;
            _cores = cores;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), ProbePriority, Dispatcher.UIThread, (_, _) =>
            {
                double now = _watch.Elapsed.TotalMilliseconds;
                _longestGap = Math.Max(_longestGap, now - _lastTick);
                _lastTick = now;
                NextRun(_clock.Read());
                NoteCore();
            });
            _timer.Start();
        }

        // The longest running time inside one gap; meaningful once Finish ran
        // with the clock present.
        public double? LongestRun => _runLost ? null : _longestRun;

        // The share of readings on the fastest kind of core; null with no
        // core-class map (one kind of core, or not readable) or no reading.
        public double? PerfCoreShare => _coreReadings == 0 ? null : (double)_fastReadings / _coreReadings;

        // The clock's reading as the row's stopwatch starts: the first gap's start.
        public void Begin(double? run)
        {
            _lastRun = run;
            NoteCore();
        }

        // run is the clock's reading as the row's stopwatch stops: the tail's end.
        public double Finish(double? run)
        {
            _timer.Stop();
            NextRun(run);
            NoteCore();
            return Math.Max(_longestGap, _watch.Elapsed.TotalMilliseconds - _lastTick);
        }

        private void NoteCore()
        {
            if (_cores?.OnFastCore() is { } fast)
            {
                _coreReadings++;
                if (fast)
                {
                    _fastReadings++;
                }
            }
        }

        public void Dispose() => _timer.Stop();

        private void NextRun(double? run)
        {
            if (run is { } now && _lastRun is { } last)
            {
                _longestRun = Math.Max(_longestRun, now - last);
            }
            else
            {
                _runLost = true;
            }

            _lastRun = run;
        }
    }
}
