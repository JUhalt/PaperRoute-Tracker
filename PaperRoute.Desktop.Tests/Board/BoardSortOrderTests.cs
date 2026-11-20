using System.Globalization;
using System.Text.Json;
using ManuscriptPipeline.Models;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Board;

// V8 (d1 6.2): the order the trial's Board gives the 500-manuscript library
// under the five sorts the checked-in file holds (0, 1, 2, 5, 6) equals the order
// today's Windows Forms Board gives it, as PaperRoute.Tests'
// BoardSortOrderTests.TheFiveDateFreeSortsGiveTheCheckedInOrders read it
// and checked it in (PaperRoute.TestFixtures\SortOrders\
// board-sort-orders-500.json).
//
// Pins Form1.vb GetVisibleManuscripts (:2215-2290) with
// CompareTitleAscending, CompareTitleDescending, CompareStageDateDescending
// and CompareStageDateAscending (:2293-2372), and the library order for
// "Current order".
//
// The fixture gives every manuscript a new Id each run, so the file names a
// manuscript by its position in the library's order (0-based; position n is
// the title "Synthetic" n + 1), and so does this test.
//
// What this file can and cannot see. Every fixture title starts "Synthetic
// NNN:" with a unique number, so the library is already in title order:
// here Title A-Z is pinned only as the library's order and Title Z-A as its
// reverse. The title comparer itself (CurrentCultureIgnoreCase) is pinned
// by BoardRuleTests V8b_TheTitleSortsIgnoreCaseInTheCurrentCulture.
//
// The two rejection sorts (Most and Fewest rejections) are not in the file
// (PR A's generator left indices 3 and 4 out), although their key, the
// snapshot's RejectionCount, depends on no date. Their comparison on
// distinct counts is pinned by BoardRuleTests
// V2_TheRejectionSortsOrderByTheSnapshotsRejectionCount; their order on
// equal counts (List.Sort, unstable above 16 items) is not pinned.
[TestClass]
public sealed class BoardSortOrderTests : StorageCheckedTests
{
    private const string OrdersFile = "board-sort-orders-500.json";
    private const int ManuscriptCount = 500;

    private static readonly (string Json, ManuscriptLocation Location)[] s_shelves =
    [
        ("Pipeline", ManuscriptLocation.Pipeline),
        ("Published", ManuscriptLocation.Published),
        ("FileDrawer", ManuscriptLocation.FileDrawer),
    ];

    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialSorts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for Windows' temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for Windows' temporary-file clean-up.
        }
    }

    [TestMethod]
    public async Task V8_TheFiveDateFreeSortsGiveTodaysOrderOnThe500Library()
    {
        // The library written by the shared fixture into this test's own
        // folder and read back through the trial's session, as today's Board
        // reads it from disk.
        LargeLibraryFixture.Create(_root, ManuscriptCount);
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            Path.Combine(_root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        var board = new BoardViewModel(session, new ManualDebouncer(), new QueuedUiDispatcher());

        await session.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, board.RenderCount, "The load renders the Board once.");
        Dictionary<Manuscript, int> positions = Positions(session.Manuscripts);
        Assert.AreEqual(167, board.Shelves[0].Total, "Pipeline");
        Assert.AreEqual(272, board.Shelves[1].Total, "Published");
        Assert.AreEqual(61, board.Shelves[2].Total, "File Drawer");

        using JsonDocument expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SortOrders", OrdersFile)));
        Assert.AreEqual(ManuscriptCount, expected.RootElement.GetProperty("manuscripts").GetInt32());
        List<JsonElement> sorts = expected.RootElement.GetProperty("sorts").EnumerateArray().ToList();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 5, 6 }, sorts.Select(sort => sort.GetProperty("index").GetInt32()).ToArray());

        foreach (JsonElement sort in sorts)
        {
            int index = sort.GetProperty("index").GetInt32();
            string text = sort.GetProperty("text").GetString() ?? string.Empty;
            Assert.AreEqual(text, board.SortOptions[index], "The sort box's item " + index.ToString(CultureInfo.InvariantCulture) + ".");

            board.SelectedSortIndex = index;
            board.Render();

            for (int shelf = 0; shelf < s_shelves.Length; shelf++)
            {
                (string json, ManuscriptLocation location) = s_shelves[shelf];
                ShelfViewModel shown = board.Shelves[shelf];
                Assert.AreEqual(location, shown.Location);
                List<int> want = sort.GetProperty(json).EnumerateArray().Select(item => item.GetInt32()).ToList();
                List<int> have = shown.Cards.Select(card => positions[card.Manuscript]).ToList();
                AssertSameOrder(want, have, text + ", " + json);
            }
        }
    }

    // Each record's position in the library's order, by reference. Position
    // n must be the title "Synthetic" n + 1, or the library is not in the
    // order the fixture wrote it.
    private static Dictionary<Manuscript, int> Positions(IReadOnlyList<Manuscript> library)
    {
        Assert.HasCount(ManuscriptCount, library);
        var result = new Dictionary<Manuscript, int>(ReferenceEqualityComparer.Instance);
        for (int position = 0; position < library.Count; position++)
        {
            string prefix = "Synthetic " + (position + 1).ToString("000", CultureInfo.InvariantCulture) + ":";
            Assert.StartsWith(prefix, library[position].Title, "The library is in the order the fixture wrote it.");
            result.Add(library[position], position);
        }

        return result;
    }

    private static void AssertSameOrder(List<int> expected, List<int> actual, string what)
    {
        Assert.AreEqual(expected.Count, actual.Count, what + ": the number of cards.");
        for (int index = 0; index < expected.Count; index++)
        {
            if (expected[index] != actual[index])
            {
                Assert.Fail($"{what}: card {index} is the manuscript at position {actual[index]}; today's Board has {expected[index]}.");
            }
        }
    }
}
