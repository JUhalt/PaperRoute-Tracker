using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// One shelf of the Board (d1 4.1, 4.4). Its data context is a ShelfViewModel.
// The view reports the shelf's width; the view model works out the columns
// and the width of a card from it.
//
// The arrow keys move between the shelf's cards (d1 4.5, new: plan item 62;
// today's Board has no such keys). With the keyboard anywhere in card i (its
// title, ⋯ or View route), bare Right goes to card i+1, Left to i-1, Down to
// i+columns, Up to i-columns, Home to the first card and End to the last, by
// the card's place in the shelf's list and the shelf's column count, not by
// where the card is drawn. The keyboard lands on that card's title, and the
// card is brought into view. At an edge (no card i+1, i-1, i+columns or
// i-columns) the key does nothing and goes no further. Arrows with a modifier,
// and keys anywhere else (the tabs, the boxes, an open ⋯ menu, which is not
// in the shelf's visual tree), are left alone. Avalonia's XYFocus is not used:
// it would move Right from a title to the ⋯ of the same card.
public partial class ShelfView : UserControl
{
    public ShelfView()
    {
        InitializeComponent();
        Scroller.SizeChanged += (_, e) => (DataContext as ShelfViewModel)?.SetWidth(e.NewSize.Width);
        DataContextChanged += (_, _) => (DataContext as ShelfViewModel)?.SetWidth(Scroller.Bounds.Width);

        // On the way down, so the scroller never takes an arrow a card uses.
        AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Tunnel);
    }

    // Where an arrow key, Home or End goes from the card at index in a shelf
    // of count cards laid out in rows of columns; index itself at an edge, and
    // null for any other key.
    public static int? ArrowTarget(Key key, int index, int count, int columns)
    {
        if (count <= 0 || index < 0 || index >= count)
        {
            return null;
        }

        columns = Math.Max(1, columns);
        int target = key switch
        {
            Key.Right => index + 1,
            Key.Left => index - 1,
            Key.Down => index + columns,
            Key.Up => index - columns,
            Key.Home => 0,
            Key.End => count - 1,
            _ => int.MinValue,
        };

        if (target == int.MinValue)
        {
            return null;
        }

        return target >= 0 && target < count ? target : index;
    }

    // The card views made so far, in shelf order.
    public IReadOnlyList<CardView> CardViews() =>
        Cards.GetVisualDescendants().OfType<CardView>().ToList();

    // Puts the keyboard on the first card's title, as Tab would. False when
    // the shelf shows no card.
    public bool FocusFirstCard()
    {
        Cards.UpdateLayout();
        CardView? first = CardViews().FirstOrDefault();
        return first?.FindControl<Button>("Title")?.Focus(NavigationMethod.Tab) ?? false;
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled
            || e.KeyModifiers != KeyModifiers.None
            || DataContext is not ShelfViewModel shelf
            || shelf.RealizedCards is not { } cards
            || CardOf(e.Source) is not { DataContext: CardViewModel card })
        {
            return;
        }

        int index = cards.IndexOf(card);
        if (ArrowTarget(e.Key, index, cards.Count, shelf.Columns) is not int target)
        {
            return;
        }

        e.Handled = true;
        if (target != index && Cards.ContainerFromIndex(target) is Control container)
        {
            container.BringIntoView();
            container.GetVisualDescendants().OfType<CardView>().FirstOrDefault()?
                .FindControl<Button>("Title")?.Focus(NavigationMethod.Directional);
        }
    }

    // The card of this shelf that holds the control a key went to, if any.
    private CardView? CardOf(object? source)
    {
        for (Visual? visual = source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is CardView card)
            {
                return card;
            }
        }

        return null;
    }
}
