using PaperRoute.ViewModels.Board;

namespace PaperRoute.ViewModels.Shell;

// Where a page's button sits: among the rail's pages, at its foot, or nowhere
// (the manuscript page is reached from a card or history).
public enum RailPlace
{
    None,
    Pages,
    Foot,
}

// One page (shell design 2.1, 2.3): its key; its label; the name of its rail
// icon (RailGlyphs), or none off the rail; its Ctrl+digit, or none; where its
// button sits; whether it is kept (made once, then hidden and shown again,
// today's word, Form1.Shell.vb:12-18) or made afresh on each visit; and how
// it is made.
public sealed record PageCatalogRow(
    PageKey Key,
    string Label,
    string? Glyph,
    int? Digit,
    RailPlace Place,
    bool Kept,
    Func<ShellServices, IPage> Make);

// Pages as data (shell design 2.1): one row per page, in the rail's order.
// The rail, the Ctrl+digit keys and the tooltips read it, so a new page is
// one row and one view (2.3).
public sealed class PageCatalog
{
    private readonly Dictionary<PageKey, PageCatalogRow> _byKey;

    public PageCatalog(IEnumerable<PageCatalogRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = [.. rows];
        _byKey = Rows.ToDictionary(row => row.Key);
    }

    // Build 5's pages (3.1 rows 6, 7, 13 and 16): today's six rail pages in
    // today's order with their Ctrl+digits (Form1.Shell.vb:230-249), the
    // manuscript page off the rail, and Settings at the rail's foot with no
    // shortcut, made afresh on each visit (#375). The Board is real; every
    // other page is a page being built, naming the issue that builds it (its
    // "built by" in the harness's target catalog).
    public static PageCatalog Standard { get; } = new(
    [
        new(PageKey.Board, "Board", "Board", 1, RailPlace.Pages, Kept: true,
            services => new BoardViewModel(services.Session, services.Debouncer, services.Dispatcher) { Interactions = services.Interactions }),
        BeingBuilt(PageKey.Library, "Library", 2, RailPlace.Pages, "#430"),
        BeingBuilt(PageKey.Journals, "Journals", 3, RailPlace.Pages, "#431"),
        BeingBuilt(PageKey.Deadlines, "Deadlines", 4, RailPlace.Pages, "#432"),
        BeingBuilt(PageKey.Insights, "Insights", 5, RailPlace.Pages, "#433"),
        BeingBuilt(PageKey.ImportExport, "Import & Export", 6, RailPlace.Pages, "#434"),
        BeingBuilt(PageKey.Manuscript, "Manuscript", digit: null, RailPlace.None, "#424"),
        BeingBuilt(PageKey.Settings, "Settings", digit: null, RailPlace.Foot, "#435", kept: false),
    ]);

    public IReadOnlyList<PageCatalogRow> Rows { get; }

    // The row of a page; a key the catalog does not hold is a mistake in code.
    public PageCatalogRow Find(PageKey key) =>
        _byKey.TryGetValue(key, out PageCatalogRow? row)
            ? row
            : throw new ArgumentException($"The page catalog has no page {key}.", nameof(key));

    // A page on the rail draws the icon of its key's name.
    private static PageCatalogRow BeingBuilt(PageKey key, string label, int? digit, RailPlace place, string issue, bool kept = true) =>
        new(key, label, place == RailPlace.None ? null : key.Name, digit, place, kept, _ => new PageBeingBuiltViewModel(label, issue));
}
