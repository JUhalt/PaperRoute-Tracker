using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// Prepares the hidden Published shelf before its first switch, as today's
// app prepares its hidden shelves while the window waits
// (Form1.BoardFill.vb:341-390; the maintainer's decision of 2026-11-21). This
// is build step 2 of the idle-preparation design (kept outside the
// repository), the spike: two steps and no timer. Nothing calls it on its
// own: a test (or, later, the Board's idle timer and the measurement) runs it
// with RunStep or RunToEnd.
//
// The steps, each one interface-thread job:
// - W, the width: the shown shelf's width is given to Published before it is
//   realized, so its rows are rewrapped to the columns at once and none wait
//   (ShelfViewModel.SyncRowsWhenShown); then Published is realized, which
//   binds its row list. Nothing is made: its Border is hidden.
// - L, the layout: Published's Border is made visible with SetCurrentValue,
//   which leaves the Border's binding to the shelf tab in force, laid out by
//   the window's layout pass (the list makes the rows of the first screen and
//   its cache), and hidden again before the job ends. A hidden control keeps
//   its bounds and stays measured, so the switch shows those rows and makes
//   none. Nothing of the step is drawn, reached with Tab or listed to an
//   accessibility client: the Border is hidden again before anything else
//   runs on the interface thread.
// Only a Published shelf that was never shown is prepared; once shown, the
// rule of ShelfViewModel.IsShown takes over unchanged.
public sealed class HiddenShelfPreparer
{
    private readonly BoardView _view;
    private BoardViewModel? _board;
    private bool _widthGiven;

    // Set for good once the shelf tab has chosen Published, even if it was
    // left again before the next step (the design's rule 5): a shelf shown
    // once is never prepared.
    private bool _shownOnce;

    public HiddenShelfPreparer(BoardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
        _view.DataContextChanged += (_, _) => Watch(_view.DataContext as BoardViewModel);
        Watch(_view.DataContext as BoardViewModel);
    }

    // True once the full-size layout step has run with Published's rows
    // current. An empty or short shelf makes fewer views, or none.
    public bool IsPrepared { get; private set; }

    // How many steps have run.
    public int StepsRun { get; private set; }

    // Runs the next step if one is due and can run now; returns whether one ran.
    public bool RunStep()
    {
        if (IsPrepared
            || _shownOnce
            || _board is not { RenderCount: > 0 } board
            || !ReferenceEquals(_view.DataContext, board)
            || board.Shelves.Count < 2
            || board.SelectedShelfIndex == 1
            || !_view.IsEffectivelyVisible
            || _view.PublishedShelf.Parent is not Border border)
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

            published.SetWidth(width);
            published.IsRealized = true;
            _widthGiven = true;
            StepsRun++;
            return true;
        }

        // A render after W changed Published's cards: its rows wait. Bringing
        // them current is a step of its own, not in this spike.
        if (published.RowsAreStale)
        {
            return false;
        }

        try
        {
            border.SetCurrentValue(Visual.IsVisibleProperty, true);
            border.UpdateLayout();
        }
        finally
        {
            border.SetCurrentValue(Visual.IsVisibleProperty, false);
        }

        IsPrepared = true;
        StepsRun++;
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
        IsPrepared = false;
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
}
