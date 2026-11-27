using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Board;

// One shelf: Pipeline, Published or File Drawer (d1 3.3). Its tab text, its
// cards in Board order, and its empty message (Form1.vb RenderManuscripts
// :2780-2878).
public sealed partial class ShelfViewModel : ObservableObject
{
    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private int _shown;

    // The whole tab text, for assistive technology: "Pipeline (10)" or
    // "Pipeline (3 of 10)".
    [ObservableProperty]
    private string _tabText = string.Empty;

    // The count, painted apart from the name: "10" or "3 of 10".
    [ObservableProperty]
    private string _tabCount = "0";

    // Null while the shelf shows cards.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyMessage))]
    private string? _emptyMessage;

    // Set when the shelf is first shown (d1 4.4); until then the view makes
    // none of its cards, as today's hidden shelves are built only when first
    // shown (Form1.BoardFill.vb:195). It is never cleared.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RealizedRows))]
    private bool _isRealized;

    // The columns and the width of each card at the shelf's width (SetWidth).
    [ObservableProperty]
    private int _columns = 1;

    [ObservableProperty]
    private double _itemWidth = MinimumCardWidth;

    // The narrowest a card may be before a column goes: 280 logical px at
    // the 10 pt window font (Form1.vb UpdateShelfGrid :3575-3578), which the
    // trial's base font is.
    public const double MinimumCardWidth = 280;

    // The gap between cards, across and down (UiTheme.SpaceMd, Form1.vb:3580-3581).
    public const double CardGap = 12;

    // The shelf's own padding, 4 px on each side (Form1.vb:1687).
    public const double ShelfPadding = 8;

    public ShelfViewModel(ManuscriptLocation location)
    {
        Location = location;
        Name = BoardRules.ShelfName(location);
        TabText = BoardRules.TabText(Name, 0, 0, filtersActive: false);
    }

    public ManuscriptLocation Location { get; }

    public string Name { get; }

    // The name, painted apart from the count.
    public string TabName => Name;

    public SyncedCollection<CardViewModel> Cards { get; } = [];

    // The cards in rows of Columns (rows of N, d1 4.4 option (b)). Row objects
    // are kept and refilled in place (SyncRows).
    public ObservableCollection<CardRowViewModel> Rows { get; } = [];

    public bool HasEmptyMessage => EmptyMessage is not null;

    // What the view's row list shows: the rows once the shelf has been shown,
    // nothing before.
    public ObservableCollection<CardRowViewModel>? RealizedRows => IsRealized ? Rows : null;

    public override string ToString() => TabText;

    // The shelf's width, as the view measures it, sets its columns: as many
    // as fit (width - padding + gap) / (narrowest card + gap), at least one,
    // each an equal share of the rest (ManuscriptShelfPanel.vb:56-59, integer
    // arithmetic as there, so a row of cards never runs past the edge).
    public void SetWidth(double width)
    {
        if (double.IsNaN(width) || width <= 0)
        {
            return;
        }

        double available = Math.Max(1, Math.Floor(width - ShelfPadding));
        int columns = Math.Max(1, (int)Math.Floor((available + CardGap) / (MinimumCardWidth + CardGap)));
        Columns = columns;
        ItemWidth = Math.Max(1, Math.Floor((available - ((columns - 1) * CardGap)) / columns));
    }

    // Whether this is the shelf the Board shows (BoardViewModel sets it as the
    // shelf tab changes). A shelf shown once and hidden again keeps its row
    // views, so it does not refill its rows while hidden:
    // a render only marks them stale, and they are brought to the cards as
    // the shelf is shown again, before the view lays it out and draws it, so
    // nobody sees an old card. Its cards, tab text and empty message change
    // at once whether shown or not.
    public bool IsShown
    {
        get => _isShown;
        internal set
        {
            if (_isShown == value)
            {
                return;
            }

            _isShown = value;
            if (value && _rowsStale)
            {
                SyncRows();
            }
        }
    }

    // True while a hidden shelf's rows wait for it to be shown: Rows then
    // hold the cards of an earlier render, and only Cards is current.
    public bool RowsAreStale => _rowsStale;

    private bool _isShown;
    private bool _rowsStale;

    // Returns what SyncInto did: the number of changes, or -1 for a Reset.
    internal int Show(IReadOnlyList<CardViewModel> cards, int total, bool filtersActive, int resetThreshold)
    {
        Total = total;
        Shown = cards.Count;
        TabText = BoardRules.TabText(Name, cards.Count, total, filtersActive);
        TabCount = BoardRules.TabCount(cards.Count, total, filtersActive);
        EmptyMessage = cards.Count == 0 ? BoardRules.EmptyMessage(Location, total) : null;
        int changes = Cards.SyncInto(cards, resetThreshold);
        if (changes != 0)
        {
            SyncRowsWhenShown();
        }

        return changes;
    }

    // A new column count rewraps the cards into rows of the new N.
    partial void OnColumnsChanged(int value) => SyncRowsWhenShown();

    // Rows are refilled now unless the shelf has row views it is hiding
    // (realized, not shown); then they wait for IsShown.
    private void SyncRowsWhenShown()
    {
        if (IsRealized && !IsShown)
        {
            _rowsStale = true;
            return;
        }

        SyncRows();
    }

    // Brings Rows to the cards in rows of Columns: row i holds cards i x N to
    // i x N + N - 1. Rows past the new end are removed from the end, new ones
    // added at the end, and every other row object is kept and refilled only
    // if its cards changed, so a render never replaces the list and the views
    // make no card for it (d1 4.4: rebuilding the rows only rewraps existing
    // card view models).
    private void SyncRows()
    {
        _rowsStale = false;
        int columns = Math.Max(1, Columns);
        int count = (Cards.Count + columns - 1) / columns;
        while (Rows.Count > count)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }

        for (int index = 0; index < count; index++)
        {
            int first = index * columns;
            int length = Math.Min(columns, Cards.Count - first);
            if (index < Rows.Count)
            {
                Rows[index].Fill(Cards, first, length, columns, index == count - 1);
            }
            else
            {
                var row = new CardRowViewModel(this, index);
                row.Fill(Cards, first, length, columns, index == count - 1);
                Rows.Add(row);
            }
        }
    }
}
