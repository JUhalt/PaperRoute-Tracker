using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop;

// One row of N cards under option (b) of d1 4.4 (ShelfLayout), and the row
// list's container for it (CardRowList), which gives it its CardRowViewModel
// (Show). It lays its cards out as the wrap panel of
// option (a) does: each the shelf's card width (ShelfViewModel.ItemWidth),
// 12 px apart, from the left, a short last row left as it is; 12 px below
// every row but the last.
//
// The row keeps its card views. When the virtualizing list recycles the row
// for another row, or a render refills it, each card view is given its new
// card only where the card changed; a card view is made only when the row
// holds more cards than ever before. A card view the row does not need is
// hidden, with no card.
//
// What a recycled card view must not keep: the keyboard. A card view that has
// the keyboard inside it is never given another card (its focus ring and its
// focused title would then belong to the wrong manuscript): it is taken out
// and a new card view takes its place, so the keyboard leaves it exactly as
// it leaves a card view option (a) removes. On a render, the shelf then puts
// the keyboard back on that card if the card is still shown
// (ShelfView.FocusedCardWasTakenOut).
public sealed class CardRowView : Panel
{
    private CardRowViewModel? _row;

    // The shelf's card view at this place of the row, if it shows one.
    public CardView? CardAt(int slot) =>
        slot >= 0 && slot < Children.Count && Children[slot] is CardView { IsVisible: true, DataContext: CardViewModel } view ? view : null;

    // The card views that show a card, left to right.
    public IEnumerable<CardView> ShownCards() =>
        Children.OfType<CardView>().Where(view => view.IsVisible && view.DataContext is CardViewModel);

    // The row shown, or null while the list keeps this view for reuse.
    public CardRowViewModel? Row => _row;

    // The list (CardRowList) gives the view a row to show: the view follows
    // it and gives each card view its card, where the card changed.
    public void Show(CardRowViewModel? row)
    {
        Release();
        _row = row;
        if (_row is not null)
        {
            _row.PropertyChanged += OnRowChanged;
        }

        Fill(render: false);
    }

    // The list keeps the view for reuse: it stops following its row and
    // keeps its card views as they are (hidden by the list), so reusing it
    // for another row gives them new cards and makes nothing.
    public void Release()
    {
        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowChanged;
            _row = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double itemWidth = ItemWidth();
        int columns = Math.Max(1, _row?.Columns ?? 1);
        double height = 0;
        foreach (Control child in Children)
        {
            if (child.IsVisible)
            {
                child.Measure(new Size(itemWidth, double.PositiveInfinity));
                height = Math.Max(height, child.DesiredSize.Height);
            }
        }

        return new Size((columns * itemWidth) + ((columns - 1) * ShelfViewModel.CardGap), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double itemWidth = ItemWidth();
        for (int slot = 0; slot < Children.Count; slot++)
        {
            Control child = Children[slot];
            if (child.IsVisible)
            {
                child.Arrange(new Rect(slot * (itemWidth + ShelfViewModel.CardGap), 0, itemWidth, finalSize.Height));
            }
        }

        return finalSize;
    }

    private double ItemWidth() => _row?.Shelf.ItemWidth ?? ShelfViewModel.MinimumCardWidth;

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Fill(render: true);

    // Gives each card view its card, where the card changed.
    private void Fill(bool render)
    {
        IReadOnlyList<CardViewModel> cards = _row?.Cards ?? [];
        for (int slot = 0; slot < Math.Max(cards.Count, Children.Count); slot++)
        {
            CardViewModel? card = slot < cards.Count ? cards[slot] : null;
            if (slot >= Children.Count)
            {
                Children.Add(new CardView { DataContext = card });
                continue;
            }

            var view = (CardView)Children[slot];
            if (!ReferenceEquals(view.DataContext, card))
            {
                if (view.IsKeyboardFocusWithin)
                {
                    TakeOutFocused(slot, view, card, render);
                    continue;
                }

                view.DataContext = card;
            }

            view.IsVisible = card is not null;
        }

        Margin = new Thickness(0, 0, 0, _row is { IsLast: false } ? ShelfViewModel.CardGap : 0);
        InvalidateMeasure();
    }

    private void TakeOutFocused(int slot, CardView view, CardViewModel? card, bool render)
    {
        string part = ShelfView.FocusedPartOf(view);
        CardViewModel? had = view.DataContext as CardViewModel;
        ShelfView? shelf = this.FindAncestorOfType<ShelfView>();
        Children[slot] = new CardView { DataContext = card, IsVisible = card is not null };
        if (render && had is not null)
        {
            shelf?.FocusedCardWasTakenOut(had, part);
        }
    }
}
