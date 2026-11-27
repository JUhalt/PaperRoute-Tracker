using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// Prepares the hidden Published shelf before its first switch, as today's
// app prepares its hidden shelves while the window waits
// (Form1.BoardFill.vb:341-390; the maintainer's decision of 2026-11-21). This
// follows the idle-preparation design (kept outside the repository). Its
// callers today: its own idle timer (Tick, only while Enabled, which the App
// does not set yet), a "--measure" run told an idle side
// (BoardMeasurement.FirstSwitchAfterIdle calls RunStep with the timer off),
// and the tests, with RunStep or RunToEnd.
//
// The steps, each one interface-thread job:
// - W, the width: the shown shelf's width is given to Published before it is
//   realized, so its rows are rewrapped to the columns at once and none wait
//   (ShelfViewModel.SyncRowsWhenShown); then Published is realized, which
//   binds its row list. Nothing is made: its Border is hidden.
// - L, the layout: Published's Border is made visible with SetCurrentValue,
//   which leaves the Border's binding to the shelf tab in force, laid out by
//   the window's layout pass (the list makes the rows it reaches), and hidden
//   again before the job ends. A hidden control keeps its bounds and stays
//   measured, so the switch shows those rows and makes none. Nothing of the
//   step is drawn, reached with Tab or listed to an accessibility client: the
//   Border is hidden again before anything else runs on the interface thread.
//   The first L steps climb a ladder: the Border's height is capped at
//   H / 2.2^3, H / 2.2^2 and H / 2.2 of the shelf host's height H, and the
//   last L lays it out at full height. The list makes rows only when its
//   viewport reaches past what it last measured, so each step makes the rows
//   of its own band, about twice the step before, and the last about half the
//   first screen and its cache.
// - R, the rows: while Published's rows are stale (a render changed its
//   cards, or W changed its columns), they are brought to its cards, as
//   showing the shelf would (ShelfViewModel.SyncStaleRows).
// Once prepared, any change that may leave the switch work to do ends
// IsPrepared: a render, a resize, a scaling or a theme change. The refresh
// is then W again if the shown shelf's width changed, R if the rows went
// stale, and one full-size L; after a change of the shelf host's size or
// of the shown shelf's width, the ladder instead, so the list measures for
// the new size from a small band up and keeps exactly the rows and cache a
// switch at that size would make. (Run by the idle timer, a refresh waits
// for 2 s of quiet after a render; RunStep itself runs whatever is due.)
// An empty shelf is not laid out: there is no card view to make, and a
// layout would leave the list measured for a whole screen, which its first
// cards would then fill in one step. It is prepared once its rows are
// current at the shown width; its first cards get the ladder.
// The slow-slice stop (the maintainer's answer of 2026-11-27: a step that
// runs or is predicted over SlowSlice): every step that made card views
// gives the cost of one card view (its time divided by the views it made).
// After an L on the ladder, the next step is predicted from that cost times
// the views the ladder's next band adds (about LadderRatio - 1 times those
// made so far in this ladder). Before an R, from the cost times the card
// views the rows it refills must add (a row view gains one for each card
// past those it ever held). Before a full-size L, from the cost times the
// views a screen and its cache need (the shelf host's height and the list's
// cache over the shortest row made, times the columns) less the views the
// list holds already. W is judged only after it runs. A step that took
// longer than SlowSlice, or a prediction over it, stops preparation for the
// session (SkippedReason) and the switch makes the rest, as on the base; a
// step predicted over it is not run. With no cost known yet nothing is
// predicted.
// Only a Published shelf that was never shown is prepared; once shown, the
// rule of ShelfViewModel.IsShown takes over unchanged.
// Accessibility: once an automation peer exists for Published's Border (an
// accessibility client has read the shelf host's children, or walked up
// from a card), a step would make the host's peer announce a change of its
// children, so preparation is skipped for the session (the maintainer's
// answer of 2026-11-27), with SkippedReason and one trace line.
//
// When the Board is idle (Enabled; the App does not set it yet): a timer at
// Background priority ticks every Interval, and a tick runs one step only if
// the Board has rendered and a frame has been drawn since; nobody has pressed
// a key, a pointer button or turned the wheel for Quiet, nor has the shelf
// host's size, the scaling or the theme changed for as long; Quiet has passed
// since the last render for the first preparation, and RefreshQuiet for a
// refresh; no pointer button is held, no stage or sort list and no card menu
// is open; the Board is visible and the window not minimized. The timer stops
// at every step and starts again only from the next frame, so a frame is
// drawn and the message loop runs between any two steps. It stops for good
// when Published is shown or preparation is skipped, stops when prepared,
// and starts again on a render, a resize, a scaling or a theme change. It
// also stops while the window is minimized, and starts again when the
// window is restored.
public sealed class HiddenShelfPreparer
{
    // The maintainer's threshold (2026-11-27): a step that runs, or is
    // predicted to run, longer than this stops preparation for the session.
    public static readonly TimeSpan SlowSlice = TimeSpan.FromMilliseconds(120);

    public const string SlowSliceReason = "slow slice";

    // The ladder: the L steps before the last cap the Border's height at the
    // shelf host's height divided by LadderRatio^3, ^2 and ^1.
    public const int LadderSteps = 3;

    public const double LadderRatio = 2.2;

    public const string AutomationPeerReason = "automation peer";

    // The idle rules: quiet after input, a resize, a scaling or theme change
    // and a render before a first preparation; longer after a render before a
    // refresh, so a person typing with pauses meets no step.
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(400);

    public static readonly TimeSpan RefreshQuiet = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(50);

    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private readonly BoardView _view;
    private readonly List<Step> _steps = [];
    private BoardViewModel? _board;
    private bool _widthGiven;

    // The shown shelf's width W last gave Published.
    private double _widthFor;

    // Set for good once the shelf tab has chosen Published, even if it was
    // left again before the next step (the design's rule 5): a shelf shown
    // once is never prepared.
    private bool _shownOnce;

    // The ladder's L steps run so far, and the card views they made.
    private int _ladderRun;
    private int _ladderMade;

    // Whether the next L steps climb the ladder: until the first full-size
    // layout with cards, and again after the shelf host's size or the shown
    // shelf's width changed.
    private bool _ladderNeeded = true;

    // The time one card view took in the last step that made any, in ms;
    // null before one has.
    private double? _costPerView;

    // What the last full-size layout step laid Published out for (or, for
    // an empty shelf, what it was found empty at); null before either.
    private LayoutFor? _laidOutFor;

    // The idle timer and what its rules watch; used only while Enabled.
    private DispatcherTimer? _timer;
    private bool _enabled;
    private TopLevel? _topLevel;
    private Control? _host;
    private bool _awaitingFrame;
    private bool _frameSinceRender;
    private bool _pointerHeld;
    private TimeSpan _lastInput;
    private TimeSpan _lastHostChange;
    private TimeSpan _lastRender;

    public HiddenShelfPreparer(BoardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
        _view.DataContextChanged += (_, _) => Watch(_view.DataContext as BoardViewModel);
        _view.AttachedToVisualTree += (_, _) => Listen();
        _view.DetachedFromVisualTree += (_, _) => StopListening();
        Watch(_view.DataContext as BoardViewModel);
    }

    // Whether the Board prepares Published by itself while idle. Off by
    // default: only a test turns it on in this work.
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            if (value)
            {
                Listen();
            }
            else
            {
                StopListening();
            }
        }
    }

    // How often the idle timer ticks; a test may make it longer, to drive
    // ticks itself with Tick.
    public TimeSpan Interval { get; set; } = DefaultInterval;

    // The stop threshold: SlowSlice, unless a test sets another.
    public TimeSpan SlowSliceLimit { get; set; } = SlowSlice;

    // Whether the idle timer is running (waiting for its next tick).
    public bool IsTimerRunning => _timer?.IsEnabled == true;

    // True while the last full-size layout step stands: it ran with
    // Published's rows current, and since then the rows have not gone stale,
    // the Board has not rendered, and the shelf host's size, the scaling, the
    // theme and Published's columns and card width are those it laid out
    // for. Any of those changes may leave the switch work to do (a rewrap
    // after a resize, a card changed in place to measure again), so the shelf
    // is no longer called prepared until a refresh lays it out again. An
    // empty or short shelf makes fewer views, or none; an empty one is not
    // laid out at all, and stands as prepared for what it was found empty at.
    public bool IsPrepared =>
        !_shownOnce
        && _laidOutFor is { } laidOut
        && _board is { Shelves.Count: > 1 } board
        && !board.Shelves[1].RowsAreStale
        && laidOut == CurrentLayout(board, board.Shelves[1]);

    // How many steps have run.
    public int StepsRun { get; private set; }

    // Why preparation stopped for the session, or null: SlowSliceReason or
    // AutomationPeerReason.
    public string? SkippedReason { get; private set; }

    // The longest step so far.
    public TimeSpan Longest { get; private set; }

    // Every step run, in order: what it was, the height cap of an L step on
    // the ladder (null at full height), the card views it made and its time.
    public IReadOnlyList<Step> Steps => _steps;

    // The clock the steps are timed with and the idle rules read; a test may
    // replace it.
    public Func<TimeSpan> Clock { get; set; } = static () => s_clock.Elapsed;

    // Runs the next step if one is due and can run now; returns whether one ran.
    // The steps due, in this order: W while Published has not been given the
    // shown shelf's width (first, or again after a resize), R while its rows
    // are stale (a render changed its cards, or W changed its columns), then
    // L: the ladder for the first preparation and after a change of the
    // shelf host's size or the shown width, and one full-size L for a
    // refresh after any other change that ended IsPrepared (a render, a
    // scaling or theme change). The shelf host must be shown: with the
    // library empty the Board hides it, and a layout under it makes nothing.
    public bool RunStep()
    {
        if (_shownOnce
            || SkippedReason is not null
            || _board is not { RenderCount: > 0 } board
            || !ReferenceEquals(_view.DataContext, board)
            || board.Shelves.Count < 2
            || board.SelectedShelfIndex == 1
            || !_view.IsEffectivelyVisible
            || _view.PublishedShelf.Parent is not Border { Parent: Control host } border
            || !host.IsEffectivelyVisible)
        {
            return false;
        }

        // An accessibility client has read the Board's structure: a step
        // would be announced as a change of the shelf host's children.
        if (ControlAutomationPeer.FromElement(border) is not null)
        {
            Skip(AutomationPeerReason);
            return false;
        }

        ShelfViewModel published = board.Shelves[1];

        // Shown before this preparer watched the Board (only the switch
        // realizes a shelf, besides W): its views exist already, and
        // IsShown's rule keeps them.
        if (published.IsShown || (published.IsRealized && !_widthGiven))
        {
            _shownOnce = true;
            return false;
        }

        if (IsPrepared)
        {
            return false;
        }

        double width = _view.SelectedShelfView.Scroller.Bounds.Width;
        double full = host.Bounds.Height;
        if (double.IsNaN(width) || width <= 0 || double.IsNaN(full) || full <= 0)
        {
            return false;
        }

        // A change of the shelf host's size or of the shown width since the
        // last full-size layout: the refresh climbs the ladder again. A
        // full-size L at the new size alone would leave the rows and cache
        // the list made for the old size (it measures again only when its
        // viewport passes the band it last saw), so the switch would show,
        // and an accessibility client would list, other cache rows than a
        // switch at the new size makes. A capped step first makes the list
        // see a small band, and the full-size step then makes it measure
        // for the new size.
        if (!_ladderNeeded && _laidOutFor is { } last && (last.Host != host.Bounds.Size || width != _widthFor))
        {
            _ladderNeeded = true;
            _ladderRun = 0;
            _ladderMade = 0;
        }

        // W. Given before Published is realized, the rows are wrapped to the
        // columns at once; given again to a realized hidden shelf after a
        // resize, a change of columns leaves its rows stale for R.
        if (!_widthGiven || width != _widthFor)
        {
            TimeSpan started = Clock();
            published.SetWidth(width);
            published.IsRealized = true;
            _widthGiven = true;
            _widthFor = width;
            Finish("W", null, 0, started, predicted: null);
            return true;
        }

        // R. The body of IsShown's sync: the rows brought to the cards. The
        // row views of rows refilled show their new cards at once; any card
        // views made for them are counted. Not run when the views its row
        // views must add are predicted over the limit.
        CardRowList? rows = _view.PublishedShelf.FindControl<CardRowList>("Rows");
        if (published.RowsAreStale)
        {
            if (PredictedSlow(ViewsTheRowsAdd(published, rows)))
            {
                Skip(SlowSliceReason);
                return false;
            }

            TimeSpan started = Clock();
            int before = CardView.MadeCount;
            published.SyncStaleRows();
            Finish("R", null, CardView.MadeCount - before, started, predicted: null);
            return true;
        }

        // An empty shelf: nothing to lay out (the class comment). Prepared
        // as it stands; no step runs.
        if (published.Cards.Count == 0)
        {
            _laidOutFor = CurrentLayout(board, published);
            return false;
        }

        // L: on the ladder while one is needed, then full-size. A full-size
        // L is not run when the views it must add are predicted over the
        // limit.
        double? cap = _ladderNeeded && _ladderRun < LadderSteps ? full / Math.Pow(LadderRatio, LadderSteps - _ladderRun) : null;
        if (cap is null && PredictedSlow(ViewsAScreenAdds(published, rows, full)))
        {
            Skip(SlowSliceReason);
            return false;
        }

        TimeSpan start = Clock();
        int madeBefore = CardView.MadeCount;
        Flip(border, cap);
        int made = CardView.MadeCount - madeBefore;
        double? predicted = null;
        if (cap is not null)
        {
            // The next step on the ladder reaches LadderRatio times as far,
            // so it makes about (LadderRatio - 1) times the views this
            // ladder made so far.
            _ladderRun++;
            _ladderMade += made;
            predicted = made > 0 ? _ladderMade * (LadderRatio - 1) : null;
        }
        else
        {
            _laidOutFor = CurrentLayout(board, published);
            _ladderNeeded = false;
            _ladderRun = 0;
            _ladderMade = 0;
        }

        Finish("L", cap, made, start, predicted);
        return true;
    }

    // Whether views card views, at the last cost known, would take longer
    // than the limit.
    private bool PredictedSlow(int views) =>
        _costPerView is { } cost && views > 0 && cost * views > SlowSliceLimit.TotalMilliseconds;

    // The card views R must add: each row view the list shows gains one
    // card view for every card its row will hold past those it ever held
    // (CardRowView keeps its card views). Rows past the new end are removed
    // and add none; row views not yet made add none here (a layout makes
    // them, and a full-size L is predicted on its own).
    private static int ViewsTheRowsAdd(ShelfViewModel published, CardRowList? rows)
    {
        int columns = Math.Max(1, published.Columns);
        int adds = 0;
        foreach (CardRowView view in RowViews(rows))
        {
            if (view.Row is not { } row)
            {
                continue;
            }

            int holds = Math.Clamp(published.Cards.Count - (row.Index * columns), 0, columns);
            adds += Math.Max(0, holds - view.Children.Count);
        }

        return adds;
    }

    // The card views a full-size layout must add: the views a screen and
    // the list's cache need (the list measures, from the top, the shelf
    // host's height and the cache below it, over the shortest row view made,
    // times the columns, at most the shelf's cards) less the views the list
    // holds already, in the row views it shows or keeps for reuse (each
    // counted up to the columns). With no row view made, every card.
    private static int ViewsAScreenAdds(ShelfViewModel published, CardRowList? rows, double hostHeight)
    {
        int columns = Math.Max(1, published.Columns);
        List<CardRowView> views = RowViews(rows).ToList();
        double shortest = views.Where(view => view.Row is not null && view.DesiredSize.Height > 0).Select(view => view.DesiredSize.Height).DefaultIfEmpty(0).Min();
        double cache = rows?.ItemsPanelRoot is VirtualizingStackPanel panel ? panel.CacheLength : 0.5;
        int need = shortest > 0
            ? (int)Math.Min(published.Cards.Count, Math.Ceiling(hostHeight * (1 + (2 * cache)) / shortest) * columns)
            : published.Cards.Count;
        int holds = views.Sum(view => Math.Min(columns, view.Children.Count));
        return Math.Max(0, need - holds);
    }

    // Every row view of the list, shown or kept for reuse.
    private static IEnumerable<CardRowView> RowViews(CardRowList? rows) =>
        rows?.ItemsPanelRoot?.Children.OfType<CardRowView>() ?? [];

    // What one tick of the idle timer does: runs the next step if the idle
    // rules allow one now; returns whether one ran. After a step the next
    // tick waits for a drawn frame. Tests call it with a longer Interval.
    public bool Tick()
    {
        _timer?.Stop();
        bool ran = IdleRulesHold() && RunStep();
        if (ran)
        {
            _awaitingFrame = true;
            RequestFrame(() =>
            {
                _awaitingFrame = false;
                Arm();
            });
        }
        else
        {
            Arm();
        }

        return ran;
    }

    // Runs every step due, one after another; returns how many ran.
    public int RunToEnd()
    {
        int ran = 0;
        while (RunStep())
        {
            ran++;
        }

        return ran;
    }

    // Shows the Border to the layout only, lays it out (capped at cap, or at
    // full height) and hides it again, all in this one job.
    private static void Flip(Border border, double? cap)
    {
        try
        {
            if (cap is { } height)
            {
                border.MaxHeight = height;
            }

            border.SetCurrentValue(Visual.IsVisibleProperty, true);
            border.UpdateLayout();
        }
        finally
        {
            border.SetCurrentValue(Visual.IsVisibleProperty, false);
            if (cap is not null)
            {
                border.ClearValue(Layoutable.MaxHeightProperty);
            }
        }
    }

    // Records a step and applies the slow-slice stop: the step's own time,
    // or its time per view made times the views the next step is predicted
    // to make.
    private void Finish(string kind, double? cap, int made, TimeSpan started, double? predicted)
    {
        TimeSpan took = Clock() - started;
        _steps.Add(new Step(kind, cap, made, took));
        StepsRun++;
        if (took > Longest)
        {
            Longest = took;
        }

        if (made > 0)
        {
            _costPerView = took.TotalMilliseconds / made;
        }

        bool slow = took > SlowSliceLimit
            || (predicted is { } views && made > 0 && took.TotalMilliseconds / made * views > SlowSliceLimit.TotalMilliseconds);
        if (slow)
        {
            Skip(SlowSliceReason);
        }
    }

    // Stops preparation for the session, and says why once.
    private void Skip(string reason)
    {
        if (SkippedReason is not null)
        {
            return;
        }

        SkippedReason = reason;
        _timer?.Stop();
        Trace.WriteLine($"PaperRoute Board: idle preparation of Published skipped for this session ({reason}).");
    }

    // The idle rules of a tick (the class comment); RunStep checks the rest.
    private bool IdleRulesHold()
    {
        if (!_enabled || _awaitingFrame || !_frameSinceRender || _board is not { RenderCount: > 0 } board)
        {
            return false;
        }

        TimeSpan now = Clock();
        TimeSpan renderQuiet = _laidOutFor is null ? Quiet : RefreshQuiet;
        if (now - _lastInput < Quiet || now - _lastHostChange < Quiet || now - _lastRender < renderQuiet)
        {
            return false;
        }

        if (_pointerHeld || board.IsStageListOpen || board.IsSortListOpen)
        {
            return false;
        }

        if (!_view.IsEffectivelyVisible || _topLevel is Window { WindowState: WindowState.Minimized })
        {
            return false;
        }

        return !_view.SelectedShelfView.CardViews().Any(card => card.MoreMenu.IsOpen || card.RightClickMenu.IsOpen);
    }

    // Starts the timer if a step may still be due. Not while the window is
    // minimized: it starts again when the window is restored.
    private void Arm()
    {
        if (!_enabled || _topLevel is null || _awaitingFrame || _shownOnce || SkippedReason is not null || IsPrepared
            || _topLevel is Window { WindowState: WindowState.Minimized })
        {
            return;
        }

        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Tick += (_, _) => Tick();
        }

        _timer.Interval = Interval;
        _timer.Start();
    }

    private void RequestFrame(Action then) => _topLevel?.RequestAnimationFrame(_ => then());

    // While Enabled and in a window: follows input, the shelf host's size,
    // the scaling and the theme, and arms the timer.
    private void Listen()
    {
        if (!_enabled || _topLevel is not null || TopLevel.GetTopLevel(_view) is not { } topLevel)
        {
            return;
        }

        _topLevel = topLevel;
        topLevel.AddHandler(InputElement.KeyDownEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerReleased, RoutingStrategies.Direct, handledEventsToo: true);
        topLevel.ScalingChanged += OnHostChanged;
        topLevel.PropertyChanged += OnTopLevelChanged;
        _host = _view.PublishedShelf.Parent?.Parent as Control;
        if (_host is not null)
        {
            _host.SizeChanged += OnHostChanged;
        }

        _view.ActualThemeVariantChanged += OnHostChanged;
        TimeSpan now = Clock();
        _lastInput = now;
        _lastHostChange = now;
        _lastRender = now;
        _pointerHeld = false;
        WaitForFrame();
        Arm();
    }

    private void StopListening()
    {
        _timer?.Stop();
        _awaitingFrame = false;
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnInput);
        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnInput);
        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _topLevel.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerReleased);
        _topLevel.ScalingChanged -= OnHostChanged;
        _topLevel.PropertyChanged -= OnTopLevelChanged;
        if (_host is not null)
        {
            _host.SizeChanged -= OnHostChanged;
        }

        _view.ActualThemeVariantChanged -= OnHostChanged;
        _topLevel = null;
        _host = null;
    }

    // A first step waits for a frame drawn after the latest render.
    private void WaitForFrame()
    {
        _frameSinceRender = false;
        RequestFrame(() => _frameSinceRender = true);
    }

    private void OnInput(object? sender, RoutedEventArgs e) => _lastInput = Clock();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastInput = Clock();
        _pointerHeld = true;
    }

    private void OnPointerReleased(object? sender, RoutedEventArgs e) => _pointerHeld = false;

    private void OnHostChanged(object? sender, EventArgs e)
    {
        _lastHostChange = Clock();
        Arm();
    }

    // Minimized, the timer stops, so a window minimized before Published is
    // prepared does not wake every Interval; restored, it starts again.
    private void OnTopLevelChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty)
        {
            return;
        }

        if (_topLevel is Window { WindowState: WindowState.Minimized })
        {
            _timer?.Stop();
        }
        else
        {
            Arm();
        }
    }

    // What a full-size layout step lays Published out for.
    private LayoutFor CurrentLayout(BoardViewModel board, ShelfViewModel published) =>
        new(
            board.RenderCount,
            (_view.PublishedShelf.Parent?.Parent as Control)?.Bounds.Size ?? default,
            TopLevel.GetTopLevel(_view)?.RenderScaling ?? 1,
            _view.ActualThemeVariant,
            published.Columns,
            published.ItemWidth);

    // Follows the Board the view shows. A different Board starts afresh.
    private void Watch(BoardViewModel? board)
    {
        if (ReferenceEquals(board, _board))
        {
            return;
        }

        if (_board is not null)
        {
            _board.PropertyChanged -= OnBoardChanged;
        }

        _board = board;
        _widthGiven = false;
        _widthFor = 0;
        _shownOnce = false;
        _ladderRun = 0;
        _ladderMade = 0;
        _ladderNeeded = true;
        _costPerView = null;
        _laidOutFor = null;
        _timer?.Stop();
        _awaitingFrame = false;
        if (board is null)
        {
            return;
        }

        board.PropertyChanged += OnBoardChanged;
        _shownOnce = board.SelectedShelfIndex == 1;
    }

    private void OnBoardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BoardViewModel.SelectedShelfIndex) && sender is BoardViewModel { SelectedShelfIndex: 1 })
        {
            _shownOnce = true;
            _timer?.Stop();
        }
        else if (e.PropertyName == nameof(BoardViewModel.RenderCount) && _topLevel is not null)
        {
            _lastRender = Clock();
            WaitForFrame();
            Arm();
        }
    }

    // One step run: W (the width), R (the rows) or L (a layout), the height
    // cap of an L on the ladder, the card views it made, and its time.
    public sealed record Step(string Kind, double? Cap, int ViewsMade, TimeSpan Time);

    private readonly record struct LayoutFor(int Renders, Size Host, double Scaling, ThemeVariant? Theme, int Columns, double ItemWidth);
}
