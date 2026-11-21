using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// One shelf of the Board (d1 4.1, 4.4). Its data context is a ShelfViewModel.
// The view reports the shelf's width; the view model works out the columns
// and the width of a card from it. ShelfLayout.Option says which list draws
// the cards (ShelfView.axaml): (a) every card at once, or (b) rows of N, only
// the rows on screen and a margin made.
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
// it would move Right from a title to the ⋯ of the same card. Under option
// (b) the target card's row may not be made yet: it is scrolled into view
// first (the list makes it), then the title takes the keyboard (d1 4.5).
//
// Under option (b) the view also keeps option (a)'s Tab order and focus:
// - Tab between cards goes by the cards' places in the shelf, not by the
//   order the list happens to hold its recycled rows in: from a card's last
//   stop (View route) to the next card's title, with Shift from a title to
//   the card before's View route, scrolling that card into view if its row is
//   not made; from the last card, or with Shift from the first, out of the
//   shelf to where Tab goes next. Within a card, Avalonia's own order stands.
// - Tab into the shelf from outside lands on the first card's title, and
//   Shift+Tab on the last card's View route, wherever the shelf is scrolled.
// - The row holding the keyboard is the list's tab-once element, which the
//   virtualizing list keeps made when it scrolls away, so the focused card
//   keeps the keyboard; a render that takes the focused card view out puts
//   the keyboard back on its card if the card is still shown
//   (FocusedCardWasTakenOut).
public partial class ShelfView : UserControl
{
    private static readonly string[] s_cardStops = ["Title", "More", "ViewRoute"];

    private ShelfViewModel? _shelf;
    private TopLevel? _topLevel;
    private bool _tabFromOutside;

    public ShelfView()
    {
        InitializeComponent();
        Scroller.SizeChanged += (_, e) => (DataContext as ShelfViewModel)?.SetWidth(e.NewSize.Width);
        DataContextChanged += (_, _) => OnShelfChanged();

        // On the way down, so the scroller never takes an arrow a card uses.
        AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Tunnel);
        if (ShelfLayout.UsesRows)
        {
            AddHandler(KeyDownEvent, OnCardTabDown, RoutingStrategies.Tunnel);
            AddHandler(GettingFocusEvent, OnGettingFocus, RoutingStrategies.Bubble);
            AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        }
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

    // The name of the card part that has the keyboard (Title, More or
    // ViewRoute), Title if none does.
    public static string FocusedPartOf(CardView card) =>
        s_cardStops.FirstOrDefault(name => card.FindControl<Button>(name)?.IsKeyboardFocusWithin == true) ?? "Title";

    // The card views made so far that show a card, in shelf order. Under
    // option (a) that is every card of a shown shelf; under (b) the cards of
    // the rows made (on screen, a margin, and a row kept for the keyboard).
    public IReadOnlyList<CardView> CardViews()
    {
        if (!ShelfLayout.UsesRows)
        {
            return Cards.GetVisualDescendants().OfType<CardView>().ToList();
        }

        if (DataContext is not ShelfViewModel shelf)
        {
            return [];
        }

        // A view counts where it shows the card at its place (its row's
        // index x the columns + its slot). That is every view shown while
        // the rows are current; a shelf hidden after it was shown keeps its
        // rows as they were until it is shown again (ShelfViewModel.IsShown),
        // and its views that no longer show the card at their place are left out.
        int columns = Math.Max(1, shelf.Columns);
        var made = new List<(int Place, CardView View)>();
        foreach (CardRowView row in RowViews())
        {
            int slot = 0;
            foreach (CardView view in row.ShownCards())
            {
                int place = (row.Row!.Index * columns) + slot;
                if (place < shelf.Cards.Count && ReferenceEquals(view.DataContext, shelf.Cards[place]))
                {
                    made.Add((place, view));
                }

                slot++;
            }
        }

        return made.OrderBy(item => item.Place).Select(item => item.View).ToList();
    }

    // The card view of the card at index if it is made, without scrolling.
    public CardView? FindCardView(int index)
    {
        if (DataContext is not ShelfViewModel { IsRealized: true } shelf || index < 0 || index >= shelf.Cards.Count)
        {
            return null;
        }

        if (!ShelfLayout.UsesRows)
        {
            return Cards.ContainerFromIndex(index)?.GetVisualDescendants().OfType<CardView>().FirstOrDefault();
        }

        // A hidden shelf's rows may wait for it to be shown again
        // (ShelfViewModel.RowsAreStale): a view that does not show the card
        // at index is not that card's view.
        int columns = Math.Max(1, shelf.Columns);
        CardView? view = RowView(index / columns)?.CardAt(index % columns);
        return view is not null && ReferenceEquals(view.DataContext, shelf.Cards[index]) ? view : null;
    }

    // The card view of the card at index, its row scrolled into view first
    // and made if it was not (option (b)); null if the shelf has no such card.
    public CardView? BringCardIntoView(int index)
    {
        if (DataContext is not ShelfViewModel { IsRealized: true } shelf || index < 0 || index >= shelf.Cards.Count)
        {
            return null;
        }

        if (ShelfLayout.UsesRows)
        {
            // Rows that wait for the shelf to be shown are not scrolled to.
            if (shelf.RowsAreStale)
            {
                return null;
            }

            int row = index / Math.Max(1, shelf.Columns);
            Rows.ScrollIntoView(row);
            if (RowView(row) is null)
            {
                Rows.UpdateLayout();
            }
        }

        CardView? view = FindCardView(index);
        view?.BringIntoView();
        return view;
    }

    // Puts the keyboard on the first card's title, as Tab would. False when
    // the shelf shows no card.
    public bool FocusFirstCard()
    {
        if (!ShelfLayout.UsesRows)
        {
            Cards.UpdateLayout();
            CardView? first = CardViews().FirstOrDefault();
            return first?.FindControl<Button>("Title")?.Focus(NavigationMethod.Tab) ?? false;
        }

        return BringCardIntoView(0)?.FindControl<Button>("Title")?.Focus(NavigationMethod.Tab) ?? false;
    }

    // A render took the card view holding the keyboard out of its row
    // (CardRowView): once laid out, the keyboard goes back to the same part
    // of that card, if the card is still on the shelf.
    internal void FocusedCardWasTakenOut(CardViewModel card, string part) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is ShelfViewModel shelf && shelf.Cards.IndexOf(card) is int index and >= 0)
            {
                BringCardIntoView(index)?.FindControl<Button>(part)?.Focus(NavigationMethod.Directional);
            }
        }, DispatcherPriority.Loaded);

    private void OnShelfChanged()
    {
        if (_shelf is not null)
        {
            _shelf.PropertyChanged -= OnShelfPropertyChanged;
        }

        _shelf = DataContext as ShelfViewModel;
        if (_shelf is not null)
        {
            _shelf.PropertyChanged += OnShelfPropertyChanged;
            _shelf.SetWidth(Scroller.Bounds.Width);
        }
    }

    // A new card width (with the same column count) lays the made rows out again.
    private void OnShelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ShelfLayout.UsesRows && e.PropertyName == nameof(ShelfViewModel.ItemWidth))
        {
            foreach (CardRowView row in RowViews())
            {
                row.InvalidateMeasure();
            }
        }
    }

    // The row views the list has made and shows: the rows realized, and the
    // row it keeps made for the keyboard, not the ones it keeps hidden for reuse.
    private IEnumerable<CardRowView> RowViews() =>
        (Rows.ItemsPanelRoot?.Children ?? []).OfType<CardRowView>()
            .Where(view => view.IsVisible && view.Row is not null);

    private CardRowView? RowView(int row) =>
        DataContext is ShelfViewModel shelf && row >= 0 && row < shelf.Rows.Count
            ? RowViews().FirstOrDefault(view => ReferenceEquals(view.Row, shelf.Rows[row]))
            : null;

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled
            || e.KeyModifiers != KeyModifiers.None
            || DataContext is not ShelfViewModel { IsRealized: true } shelf
            || CardOf(e.Source) is not { DataContext: CardViewModel card })
        {
            return;
        }

        int index = shelf.Cards.IndexOf(card);
        if (ArrowTarget(e.Key, index, shelf.Cards.Count, shelf.Columns) is not int target)
        {
            return;
        }

        e.Handled = true;
        if (target == index)
        {
            return;
        }

        if (!ShelfLayout.UsesRows)
        {
            if (Cards.ContainerFromIndex(target) is Control container)
            {
                container.BringIntoView();
                container.GetVisualDescendants().OfType<CardView>().FirstOrDefault()?
                    .FindControl<Button>("Title")?.Focus(NavigationMethod.Directional);
            }

            return;
        }

        // Option (b): the row is scrolled into view, and made if it was not;
        // should the list not have made it yet, once laid out (d1 4.5).
        if (BringCardIntoView(target) is CardView view)
        {
            view.FindControl<Button>("Title")?.Focus(NavigationMethod.Directional);
        }
        else
        {
            Dispatcher.UIThread.Post(() => BringCardIntoView(target)?.FindControl<Button>("Title")?.Focus(NavigationMethod.Directional), DispatcherPriority.Loaded);
        }
    }

    // Option (b): Tab and Shift+Tab across a card's edge (see the class notes).
    private void OnCardTabDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled
            || e.Key != Key.Tab
            || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None
            || DataContext is not ShelfViewModel { IsRealized: true } shelf
            || CardOf(e.Source) is not { DataContext: CardViewModel card } from)
        {
            return;
        }

        bool back = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        List<Button> stops = Stops(from);
        int at = stops.FindIndex(stop => ReferenceEquals(stop, e.Source) || stop.IsKeyboardFocusWithin);
        if (at < 0 || (back ? at > 0 : at < stops.Count - 1))
        {
            return;
        }

        int index = shelf.Cards.IndexOf(card);
        int target = back ? index - 1 : index + 1;
        if (target < 0 || target >= shelf.Cards.Count)
        {
            // Out of the shelf: for this one key the rows' list allows no
            // Tab inside it, so the focus manager's own Tab takes the
            // keyboard to what comes after the shelf (before it, with Shift).
            KeyboardNavigation.SetTabNavigation(Rows, KeyboardNavigationMode.None);
            Dispatcher.UIThread.Post(() => KeyboardNavigation.SetTabNavigation(Rows, KeyboardNavigationMode.Continue), DispatcherPriority.Input);
            return;
        }

        List<Button> its = BringCardIntoView(target) is CardView view ? Stops(view) : [];
        if (its.Count > 0)
        {
            e.Handled = true;
            (back ? its[^1] : its[0]).Focus(NavigationMethod.Tab, e.KeyModifiers);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ShelfLayout.UsesRows)
        {
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.AddHandler(KeyDownEvent, OnTabAnywhere, RoutingStrategies.Tunnel);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnTabAnywhere);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Option (b): notes that a Tab key is on its way from outside the shelf,
    // until the key has been handled (OnGettingFocus).
    private void OnTabAnywhere(object? sender, KeyEventArgs e)
    {
        if (e.Handled
            || e.Key != Key.Tab
            || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None
            || (e.Source is Visual source && IsInside(source)))
        {
            return;
        }

        _tabFromOutside = true;
        Dispatcher.UIThread.Post(() => _tabFromOutside = false, DispatcherPriority.Input);
    }

    // Option (b): a Tab pressed outside the shelf that lands in it lands on
    // the first card's title (Shift+Tab: the last card's View route), as
    // option (a)'s tree order gives, whichever recycled row the list holds
    // first and wherever the shelf is scrolled.
    private void OnGettingFocus(object? sender, FocusChangingEventArgs e)
    {
        if (!_tabFromOutside
            || e.NavigationMethod != NavigationMethod.Tab
            || DataContext is not ShelfViewModel { IsRealized: true } shelf
            || shelf.Cards.Count == 0
            || CardOf(e.NewFocusedElement) is not { DataContext: CardViewModel card } to)
        {
            return;
        }

        _tabFromOutside = false;
        bool back = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        int expected = back ? shelf.Cards.Count - 1 : 0;
        List<Button> stops = Stops(to);
        if (shelf.Cards.IndexOf(card) == expected && stops.Count > 0 && ReferenceEquals(e.NewFocusedElement, back ? stops[^1] : stops[0]))
        {
            return;
        }

        List<Button> its = BringCardIntoView(expected) is CardView view ? Stops(view) : [];
        if (its.Count > 0)
        {
            e.TrySetNewFocusedElement(back ? its[^1] : its[0]);
        }
    }

    // Option (b): the row that holds the keyboard is the list's tab-once
    // element, which the virtualizing list keeps made while it scrolls away.
    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        for (Visual? visual = e.Source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is CardRowView row && ReferenceEquals(row.GetVisualParent(), Rows.ItemsPanelRoot))
            {
                KeyboardNavigation.SetTabOnceActiveElement(Rows, row);
                return;
            }
        }
    }

    // A card's tab stops in Tab order: its title, ⋯ and View route, those that can take the keyboard.
    private static List<Button> Stops(CardView card) =>
        s_cardStops.Select(name => card.FindControl<Button>(name))
            .OfType<Button>()
            .Where(stop => stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled && stop.Focusable && stop.IsTabStop)
            .ToList();

    private bool IsInside(Visual visual)
    {
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
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
