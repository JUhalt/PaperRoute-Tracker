using Avalonia.Controls;
using Avalonia.Input;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// The list of a shelf's rows, rows of N (d1 4.4 option (b)): an
// ItemsControl over a virtualizing stack panel, drawn as an ItemsControl.
//
// Each row's container is its CardRowView itself, so when the list recycles
// a container for another row, the row view keeps its card views and gives
// them the new row's cards (CardRowView); nothing is made again. A recycled
// container is only hidden by the list until it is reused: it keeps its
// card views and their cards, and stops following its old row.
//
// It leaves the keys alone. An ItemsControl moves the keyboard between its
// containers on the arrow keys, Home, End and the page keys, through its
// panel; over a virtualizing panel that scrolls the list to rows the keyboard
// cannot land on (a row is not a tab stop; Ctrl+Down went to the last row,
// the keyboard staying where it was). The shelf moves between cards itself
// (ShelfView), and every other key goes on to the shelf's scroller.
public sealed class CardRowList : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<CardRowView>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new CardRowView();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.DataContext = item;
        (container as CardRowView)?.Show(item as CardRowViewModel);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        (container as CardRowView)?.Release();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Not the ItemsControl's own key handling: see the class notes.
    }
}
