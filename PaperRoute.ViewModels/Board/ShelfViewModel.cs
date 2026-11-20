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

    public override string ToString() => TabText;

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
