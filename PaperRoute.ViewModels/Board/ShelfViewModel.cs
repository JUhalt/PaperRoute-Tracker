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
    [NotifyPropertyChangedFor(nameof(RealizedCards))]
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

    public bool HasEmptyMessage => EmptyMessage is not null;

    // What the view's card list shows: the cards once the shelf has been
    // shown, nothing before.
    public SyncedCollection<CardViewModel>? RealizedCards => IsRealized ? Cards : null;

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

    // Returns what SyncInto did: the number of changes, or -1 for a Reset.
    internal int Show(IReadOnlyList<CardViewModel> cards, int total, bool filtersActive, int resetThreshold)
    {
        Total = total;
        Shown = cards.Count;
        TabText = BoardRules.TabText(Name, cards.Count, total, filtersActive);
        TabCount = BoardRules.TabCount(cards.Count, total, filtersActive);
        EmptyMessage = cards.Count == 0 ? BoardRules.EmptyMessage(Location, total) : null;
        return Cards.SyncInto(cards, resetThreshold);
    }
}
