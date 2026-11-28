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

    public IReadOnlyList<PageCatalogRow> Rows { get; }

    // The row of a page; a key the catalog does not hold is a mistake in code.
    public PageCatalogRow Find(PageKey key) =>
        _byKey.TryGetValue(key, out PageCatalogRow? row)
            ? row
            : throw new ArgumentException($"The page catalog has no page {key}.", nameof(key));
}
