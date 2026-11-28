namespace PaperRoute.ViewModels.Shell;

// A page of the window (shell design 2.1): what a person reaches from the
// rail, a link or history. Today's WorkspacePage (Form1.Shell.vb:12-18) with
// Settings added, which becomes a page (3.1 row 13). The catalog
// (PageCatalog) says everything else about each.
public readonly record struct PageKey(string Name)
{
    public static readonly PageKey Board = new("Board");

    public static readonly PageKey Library = new("Library");

    public static readonly PageKey Journals = new("Journals");

    public static readonly PageKey Deadlines = new("Deadlines");

    public static readonly PageKey Insights = new("Insights");

    public static readonly PageKey ImportExport = new("ImportExport");

    public static readonly PageKey Manuscript = new("Manuscript");

    public static readonly PageKey Settings = new("Settings");

    // The page's key in the harness's target catalog (target-surfaces.tsv):
    // "Page/Board" and so on.
    public string SurfaceKey => "Page/" + Name;

    public override string ToString() => Name;
}
