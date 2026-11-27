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
    private readonly ThreadClock _clock;
    private readonly ProcessorClasses? _cores = ProcessorClasses.Current;
    private readonly List<MeasuredRow> _rows = [];

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
        _frame = frame;
        _clock = clock;
    }

    public IReadOnlyList<MeasuredRow> Rows => _rows;

    public ThreadClock Clock => _clock;

    // The frame action waits for the screen (a real window), so the scroll
    // row, which draws a frame in each step, waits by design.
    public bool FrameWaits { get; init; }

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
    }

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

    private void Paint(string library, string id, string action, Window window) =>
        Time(library, id, action, UiThreadBudget, "UI thread", () => _frame(window), waits: true);

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
