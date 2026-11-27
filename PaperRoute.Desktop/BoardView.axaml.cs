using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// The Board's view (d1 4.1, step 8). Everything it shows is bound to the
// BoardViewModel; the code here is the view's own: Ctrl+F's focus, the boxes'
// Esc (ArrowChoiceKeeper), the narrow layout, and where the keyboard goes
// after Clear.
//
// Narrow window (d1 4.1): today the header's tools wrap under the title and
// the shelf bar's boxes move above the tabs (SplitBar.WrapRightAbove,
// Form1.vb:613-619). Here the switch happens when the Board is narrower than
// either row needs side by side, measured from the controls themselves
// whenever the Board's size changes; the class "narrow" on the Board's body
// says which layout is on.
public partial class BoardView : UserControl
{
    public const string NarrowClass = "narrow";

    // The gap kept between the title and the header's tools.
    private const double HeaderGap = 16;

    public BoardView()
    {
        InitializeComponent();
        Preparer = new HiddenShelfPreparer(this);

        // Esc or a click away keeps the item the arrows reached, as today (ArrowChoiceKeeper).
        ArrowChoiceKeeper.Attach(StageFilter);
        ArrowChoiceKeeper.Attach(BoardSort);
        BoardBody.SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);

        // Clear turns itself off once pressed; pressed from the keyboard, the
        // keyboard goes on to the first card's title, the next tab stop, as
        // Windows Forms moves it today (BoardFilterTests.vb:273-309).
        ClearFilters.Click += (_, _) =>
        {
            if (ClearFilters.IsFocused)
            {
                Dispatcher.UIThread.Post(AfterClear);
            }
        };
    }

    // The shelf shown.
    public ShelfView SelectedShelfView =>
        (DataContext as BoardViewModel)?.SelectedShelfIndex switch
        {
            1 => PublishedShelf,
            2 => FileDrawerShelf,
            _ => PipelineShelf,
        };

    public bool IsNarrow => BoardBody.Classes.Contains(NarrowClass);

    // Prepares the hidden Published shelf before its first switch. It runs
    // only when called: nothing turns it on yet.
    public HiddenShelfPreparer Preparer { get; }

    // Ctrl+F (Form1.vb ProcessCmdKey, :2553-2560): the search box takes the
    // keyboard with its text selected. False while the welcome hides it.
    public bool FocusSearch()
    {
        if (!SearchBox.IsEffectivelyVisible)
        {
            return false;
        }

        bool focused = SearchBox.Focus(NavigationMethod.Tab);
        SearchBox.SelectAll();
        return focused;
    }

    private void AfterClear()
    {
        object? focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (ClearFilters.IsEffectivelyEnabled || (focused is not null && !ReferenceEquals(focused, ClearFilters)))
        {
            return;
        }

        SelectedShelfView.FocusFirstCard();
    }

    private void ApplyLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        double header = NaturalWidth(PageTitle) + HeaderGap + NaturalWidth(HeaderTools);
        double shelfBar = NaturalWidth(ShelfTabs) + NaturalWidth(ShelfTools);
        bool narrow = width < Math.Max(header, shelfBar);

        // Header: the tools beside the title, or under it.
        Place(HeaderTools, narrow ? 1 : 0, narrow ? 0 : 1, narrow ? 2 : 1, narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right);
        HeaderTools.Margin = narrow ? new Thickness(0, 8, 0, 0) : default;

        // Shelf bar: the boxes beside the tabs, or above them.
        Place(ShelfTools, 0, narrow ? 0 : 1, narrow ? 2 : 1, narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right);
        Place(ShelfTabs, narrow ? 1 : 0, 0, narrow ? 2 : 1, HorizontalAlignment.Left);

        BoardBody.Classes.Set(NarrowClass, narrow);
    }

    // The width a control asks for with no limit, which a Grid's star
    // column would otherwise cut (margins included); its next measure is the
    // layout's own again.
    private static double NaturalWidth(Control control)
    {
        control.Measure(Size.Infinity);
        double width = control.DesiredSize.Width;
        control.InvalidateMeasure();
        return width;
    }

    private static void Place(Control control, int row, int column, int columnSpan, HorizontalAlignment alignment)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, columnSpan);
        control.HorizontalAlignment = alignment;
    }
}
