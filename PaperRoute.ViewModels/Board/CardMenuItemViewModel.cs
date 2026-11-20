namespace PaperRoute.ViewModels.Board;

// One item of a card's menu, or the line between two groups (Form1.vb
// BuildCardMenu, :3488-3549): Open and View Route; then Move to File
// Drawer... on the Pipeline or Restore to Pipeline in the File Drawer; then
// Delete..., drawn in the danger colour. The Published shelf's menu has no
// middle group.
//
// In the trial every item is off, with the tooltip "Not in the trial
// (read-only)" (d1 1.2): the menu shows what today's offers and does nothing.
public sealed class CardMenuItemViewModel
{
    // Header of the line between groups (an Avalonia MenuItem draws "-" as a separator).
    public const string SeparatorHeader = "-";

    private static readonly IReadOnlyList<CardMenuItemViewModel> s_pipeline = Build("Move to File Drawer...");
    private static readonly IReadOnlyList<CardMenuItemViewModel> s_published = Build(middle: null);
    private static readonly IReadOnlyList<CardMenuItemViewModel> s_fileDrawer = Build("Restore to Pipeline");

    private CardMenuItemViewModel(string header, bool isDanger = false)
    {
        Header = header;
        IsDanger = isDanger;
    }

    public string Header { get; }

    public bool IsSeparator => Header == SeparatorHeader;

    // Delete... (Form1.vb:3545, DangerColor).
    public bool IsDanger { get; }

    // Off in the trial (d1 1.2).
    public bool IsEnabled => false;

    public string? ToolTip => IsSeparator ? null : RailItemViewModel.NotInTrial;

    public override string ToString() => Header;

    // The items for a card on this shelf. A record on no shelf the Board
    // knows gets the Published form, as BuildCardMenu adds no middle group
    // for it.
    public static IReadOnlyList<CardMenuItemViewModel> For(ManuscriptLocation location) => location switch
    {
        ManuscriptLocation.Pipeline => s_pipeline,
        ManuscriptLocation.FileDrawer => s_fileDrawer,
        _ => s_published,
    };

    private static List<CardMenuItemViewModel> Build(string? middle)
    {
        var items = new List<CardMenuItemViewModel> { new("Open"), new("View Route") };
        if (middle is not null)
        {
            items.Add(new(SeparatorHeader));
            items.Add(new(middle));
        }

        items.Add(new(SeparatorHeader));
        items.Add(new("Delete...", isDanger: true));
        return items;
    }
}
