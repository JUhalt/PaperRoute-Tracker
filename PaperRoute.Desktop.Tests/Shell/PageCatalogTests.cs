using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Parity;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Shell;

namespace PaperRoute.Desktop.Tests.Shell;

// Pages as data (shell design 5.1, B4): the catalog of build 5's pages, which
// the rail, the Ctrl+digit keys and the tooltips will read. No window.
[TestClass]
public sealed class PageCatalogTests
{
    private static readonly PageKey[] s_eightPages =
    [
        PageKey.Board, PageKey.Library, PageKey.Journals, PageKey.Deadlines,
        PageKey.Insights, PageKey.ImportExport, PageKey.Manuscript, PageKey.Settings,
    ];

    [TestMethod]
    public void TheRailListsTodaysPagesInOrder()
    {
        PageCatalogRow[] rail = [.. PageCatalog.Standard.Rows.Where(row => row.Place == RailPlace.Pages)];

        CollectionAssert.AreEqual(
            new[] { "Board", "Library", "Journals", "Deadlines", "Insights", "Import & Export" },
            rail.Select(row => row.Label).ToArray(),
            "Today's six rail pages, in today's order (Form1.Shell.vb:230-237).");
        CollectionAssert.AreEqual(
            new int?[] { 1, 2, 3, 4, 5, 6 },
            rail.Select(row => row.Digit).ToArray(),
            "Ctrl+1 to Ctrl+6 in the rail's order, as today's tooltips give them (Form1.Shell.vb:249).");

        PageCatalogRow settings = PageCatalog.Standard.Find(PageKey.Settings);
        Assert.AreEqual(RailPlace.Foot, settings.Place, "Settings sits at the rail's foot (3.1 row 13).");
        Assert.IsNull(settings.Digit, "Settings has no shortcut, so the guide's table stays at 13.");
        PageCatalogRow manuscript = PageCatalog.Standard.Find(PageKey.Manuscript);
        Assert.AreEqual(RailPlace.None, manuscript.Place, "The manuscript page is reached from a card or history.");
        Assert.IsNull(manuscript.Digit);
        Assert.IsNull(manuscript.Glyph, "A page off the rail has no rail icon.");

        foreach (PageCatalogRow row in PageCatalog.Standard.Rows.Where(row => row.Place != RailPlace.None))
        {
            CollectionAssert.Contains(RailGlyphs.Names.ToArray(), row.Glyph, $"{row.Key}'s icon is one the rail draws.");
        }

        CollectionAssert.AreEqual(
            new[] { PageKey.Settings },
            PageCatalog.Standard.Rows.Where(row => !row.Kept).Select(row => row.Key).ToArray(),
            "Every page is kept but Settings, which remembers nothing of the last visit (3.1 row 16, #375).");
    }

    [TestMethod]
    public void EveryPageHasAUniqueKeyAndDigit()
    {
        IReadOnlyList<PageCatalogRow> rows = PageCatalog.Standard.Rows;

        CollectionAssert.AreEquivalent(s_eightPages, rows.Select(row => row.Key).ToArray(), "Each of the eight pages has one row.");
        CollectionAssert.AllItemsAreUnique(rows.Select(row => row.Label).ToArray(), "Each page has its own label.");
        int?[] digits = [.. rows.Where(row => row.Digit is not null).Select(row => row.Digit)];
        CollectionAssert.AllItemsAreUnique(digits, "One page per Ctrl+digit.");
        foreach (int? digit in digits)
        {
            Assert.IsTrue(digit is >= 1 and <= 9, $"Ctrl+{digit} is not a digit key.");
        }
    }

    [TestMethod]
    public void EveryPagesSurfaceKeyIsATargetSurface()
    {
        IReadOnlySet<string> surfaces = ParityMap.Load().Surfaces.Select(surface => surface.Key).ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<string> findings = KeysNotInTheTargetCatalog(PageCatalog.Standard.Rows.Select(row => row.Key), surfaces);

        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));
        CollectionAssert.AreEqual(
            new[] { "Page/Libary: no row of target-surfaces.tsv has this key or a key under it." },
            KeysNotInTheTargetCatalog([new PageKey("Libary")], surfaces).ToArray(),
            "A misspelt page is found.");
    }

    [TestMethod]
    public void APageBeingBuiltNamesItsIssue()
    {
        ShellServices services = Services();

        foreach (PageCatalogRow row in PageCatalog.Standard.Rows.Where(row => row.Key != PageKey.Board))
        {
            var standIn = row.Make(services) as PageBeingBuiltViewModel;
            Assert.IsNotNull(standIn, $"{row.Key} is a page being built in build 5.");
            Assert.AreEqual(row.Label, standIn.Title, $"{row.Key}'s stand-in shows the page's title.");
            Assert.IsTrue(ParityMap.IsBuildIssue(standIn.Issue), $"{row.Key}'s stand-in names \"{standIn.Issue}\", not a build issue of the plan.");
        }
    }

    [TestMethod]
    public async Task TheBoardIsAPage()
    {
        ShellServices services = Services();
        PageCatalogRow row = PageCatalog.Standard.Find(PageKey.Board);
        Assert.IsTrue(row.Kept, "The Board is always kept, as today.");

        var board = row.Make(services) as BoardViewModel;

        Assert.IsNotNull(board, "The Board's row makes the Board itself.");
        IPage page = board;
        Assert.AreEqual(row.Label, page.Title, "The page host is named by the page.");
        Assert.AreSame(services.Interactions, board.Interactions, "The Board's notices go to the shell's.");
        Assert.IsTrue(await page.CanLeaveAsync(), "The Board never refuses a leave.");

        Assert.IsTrue(await services.Navigator.GoAsync(new PageRequest(PageKey.Board)));
        Assert.IsInstanceOfType(services.Navigator.CurrentPage, typeof(BoardViewModel), "The navigator shows the Board from the catalog.");
    }

    // What each page's factory is given, from a navigator over the real
    // catalog and a library held in memory.
    private static ShellServices Services() =>
        new Navigator(PageCatalog.Standard, new MemorySession([], Papers.Today), new NoticeLog(), new QueuedUiDispatcher(), new ManualDebouncer()).Services;

    // A page is a target surface when a row has its key, or when rows of its
    // own parts stand for it: the Library page, whose Manuscripts and
    // Authors & Affiliations views are rows and which has no row of its own.
    private static List<string> KeysNotInTheTargetCatalog(IEnumerable<PageKey> pages, IReadOnlySet<string> surfaces) =>
    [
        .. pages
            .Where(page => !surfaces.Contains(page.SurfaceKey)
                && !surfaces.Any(surface => surface.StartsWith(page.SurfaceKey + "/", StringComparison.Ordinal)))
            .Select(page => $"{page.SurfaceKey}: no row of {ParityMap.SurfacesFile} has this key or a key under it."),
    ];
}
