using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// Prepares the hidden Published shelf before its first switch, as today's
// app prepares its hidden shelves while the window waits
// (Form1.BoardFill.vb:341-390; the maintainer's decision of 2026-11-21). This
// follows the idle-preparation design (kept outside the repository). Nothing
// calls it on its own: a test (or, later, the Board's idle timer and the
// measurement) runs it with RunStep or RunToEnd.
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
// The slow-slice stop: after each step its time is divided by the card views
// it made; if a step took longer than SlowSlice, or that cost predicts the
// next step longer than SlowSlice, preparation stops for the session
// (SkippedReason) and the switch makes the rest, as on the base.
// Only a Published shelf that was never shown is prepared; once shown, the
// rule of ShelfViewModel.IsShown takes over unchanged.
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

    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private readonly BoardView _view;
    private readonly List<Step> _steps = [];
    private BoardViewModel? _board;
    private bool _widthGiven;

    // Set for good once the shelf tab has chosen Published, even if it was
    // left again before the next step (the design's rule 5): a shelf shown
    // once is never prepared.
    private bool _shownOnce;

    // The ladder's L steps run so far.
    private int _ladderRun;

    // What the last full-size layout step laid Published out for; null
    // before it has run.
    private LayoutFor? _laidOutFor;

    public HiddenShelfPreparer(BoardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
        _view.DataContextChanged += (_, _) => Watch(_view.DataContext as BoardViewModel);
        Watch(_view.DataContext as BoardViewModel);
    }

    // True while the last full-size layout step stands: it ran with
    // Published's rows current, and since then the rows have not gone stale
    // and the shelf host's size, the scaling, the theme and Published's
    // columns and card width are those it laid out for. Any of those changes
    // makes the switch lay the shelf out again (a rewrap after a resize), so
    // the shelf is no longer called prepared. An empty or short shelf makes
    // fewer views, or none.
    public bool IsPrepared =>
        !_shownOnce
        && _laidOutFor is { } laidOut
        && _board is { Shelves.Count: > 1 } board
        && !board.Shelves[1].RowsAreStale
        && laidOut == CurrentLayout(board.Shelves[1]);

    // How many steps have run.
    public int StepsRun { get; private set; }

    // Why preparation stopped for the session, or null: SlowSliceReason.
    public string? SkippedReason { get; private set; }

    // The longest step so far.
    public TimeSpan Longest { get; private set; }

    // Every step run, in order: what it was, the height cap of an L step on
    // the ladder (null at full height), the card views it made and its time.
    public IReadOnlyList<Step> Steps => _steps;

    // The clock the steps are timed with; a test may replace it.
    public Func<TimeSpan> Clock { get; set; } = static () => s_clock.Elapsed;

    // Runs the next step if one is due and can run now; returns whether one ran.
    public bool RunStep()
    {
        if (_laidOutFor is not null
            || _shownOnce
            || SkippedReason is not null
            || _board is not { RenderCount: > 0 } board
            || !ReferenceEquals(_view.DataContext, board)
            || board.Shelves.Count < 2
            || board.SelectedShelfIndex == 1
            || !_view.IsEffectivelyVisible
            || _view.PublishedShelf.Parent is not Border { Parent: Control host } border)
        {
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

        if (!_widthGiven)
        {
            double width = _view.SelectedShelfView.Scroller.Bounds.Width;
            if (double.IsNaN(width) || width <= 0)
            {
                return false;
            }

            TimeSpan started = Clock();
            published.SetWidth(width);
            published.IsRealized = true;
            _widthGiven = true;
            Finish("W", null, 0, started, predicted: null);
            return true;
        }

        // A render after W changed Published's cards: its rows wait. Bringing
        // them current is a step of its own, not built yet.
        if (published.RowsAreStale)
        {
            return false;
        }

        double full = host.Bounds.Height;
        if (double.IsNaN(full) || full <= 0)
        {
            return false;
        }

        double? cap = _ladderRun < LadderSteps ? full / Math.Pow(LadderRatio, LadderSteps - _ladderRun) : null;
        TimeSpan start = Clock();
        int before = CardView.MadeCount;
        Flip(border, cap);
        int made = CardView.MadeCount - before;
        _ladderRun++;
        if (cap is null)
        {
            _laidOutFor = CurrentLayout(published);
        }

        // The next step on the ladder reaches LadderRatio times as far, so it
        // makes about (LadderRatio - 1) times the views made so far.
        int madeSoFar = _steps.Where(step => step.Kind == "L").Sum(step => step.ViewsMade) + made;
        double? predicted = cap is not null && made > 0 ? madeSoFar * (LadderRatio - 1) : null;
        Finish("L", cap, made, start, predicted);
        return true;
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

        bool slow = took > SlowSlice
            || (predicted is { } views && made > 0 && took.TotalMilliseconds / made * views > SlowSlice.TotalMilliseconds);
        if (slow)
        {
            SkippedReason = SlowSliceReason;
        }
    }

    // What a full-size layout step lays Published out for.
    private LayoutFor CurrentLayout(ShelfViewModel published) =>
        new(
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
        _shownOnce = false;
        _ladderRun = 0;
        _laidOutFor = null;
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
        }
    }

    // One step run: W (the width), R (the rows) or L (a layout), the height
    // cap of an L on the ladder, the card views it made, and its time.
    public sealed record Step(string Kind, double? Cap, int ViewsMade, TimeSpan Time);

    private readonly record struct LayoutFor(Size Host, double Scaling, ThemeVariant? Theme, int Columns, double ItemWidth);
}
