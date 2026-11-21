using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Board;

// One row of a shelf under option (b) of d1 4.4 (ShelfLayout): the cards
// from Index x Columns, at most Columns of them. A shelf keeps its row
// objects from render to render and refills them in place, so the row at
// position i is always the same object: the virtualizing list keeps its row
// views, and a row view gives its card views new cards only where a card
// changed (CardRowView). Every card is the same height, so every row is,
// which the virtualizing list expects (r2 3.1).
public sealed partial class CardRowViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<CardViewModel> _cards = [];

    [ObservableProperty]
    private int _columns = 1;

    // The last row has no gap below it, as the wrap panel puts its 12 px
    // only between lines.
    [ObservableProperty]
    private bool _isLast;

    internal CardRowViewModel(ShelfViewModel shelf, int index)
    {
        Shelf = shelf;
        Index = index;
    }

    public ShelfViewModel Shelf { get; }

    public int Index { get; }

    // The shelf index of the row's first card.
    public int FirstIndex => Index * Columns;

    public override string ToString() => $"Row {Index} of {Shelf.Name}";

    // The row as the cards from first, length of them, in rows of columns.
    // Nothing changes, and nothing is raised, when the row already holds them.
    internal void Fill(IReadOnlyList<CardViewModel> cards, int first, int length, int columns, bool isLast)
    {
        Columns = columns;
        IsLast = isLast;
        if (Cards.Count == length)
        {
            bool same = true;
            for (int offset = 0; offset < length && same; offset++)
            {
                same = ReferenceEquals(Cards[offset], cards[first + offset]);
            }

            if (same)
            {
                return;
            }
        }

        var slice = new CardViewModel[length];
        for (int offset = 0; offset < length; offset++)
        {
            slice[offset] = cards[first + offset];
        }

        Cards = slice;
    }
}
