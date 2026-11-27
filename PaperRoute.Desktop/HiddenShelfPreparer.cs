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
    private bool _widthGiven;

    public HiddenShelfPreparer(BoardView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
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
            || _view.DataContext is not BoardViewModel { RenderCount: > 0 } board
            || board.Shelves.Count < 2
            || board.SelectedShelfIndex == 1
            || !_view.IsEffectivelyVisible
            || _view.PublishedShelf.Parent is not Border border)
        {
            return false;
        }

        ShelfViewModel published = board.Shelves[1];

        // Shown before (only the switch realizes a shelf, besides W): its
        // views exist already, and IsShown's rule keeps them.
        if (published.IsShown || (published.IsRealized && !_widthGiven))
        {
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
}
