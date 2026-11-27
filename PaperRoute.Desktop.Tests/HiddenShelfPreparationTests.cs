using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The hidden Published shelf prepared before its first switch
// (HiddenShelfPreparer; the idle-preparation design's build step 2, the
// spike: the width step W and one full-size layout step L, run by RunToEnd).
// Each test compares a prepared Board with a twin Board over the same library
// that was not prepared, which switches as today. Preparation runs only where
// a test calls it.
// - T1: after preparation Published holds the card views the twin's first
//   switch makes, for the same cards, and its own first switch makes none
//   (CardView.MadeCount, a count that does not depend on the PC's load).
// - T3: before and after the switch the window draws the same picture, byte
//   for byte, light and dark; after it, with the same scroll extent and thumb.
// - T4: the shelf tab still decides which shelf is shown.
// - T6: with preparation done the Board's accessibility tree and Tab order
//   are the checked-in ones, and after the switch they are the twin's.
// - T11: an empty Published, a short one, and the first cards arriving on a
//   prepared empty one.
[TestClass]
public sealed class HiddenShelfPreparationTests : StorageCheckedTests
{
    private const string PublishedName = "PublishedShelf";
    private const int ExampleTabLimit = 120;
    private const int FiveHundredTabLimit = 40;

    private static readonly string[] s_shelfNames = ["PipelineShelf", PublishedName, "FileDrawerShelf"];

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialPrepare-" + Guid.NewGuid().ToString("N"));
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
            // Left for the system's temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the system's temporary-file clean-up.
        }
    }

    [TestMethod]
    public void T1_PreparedPublishedMakesNoViewAtItsFirstSwitch()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            int twinMade;
            List<string> twinTitles;
            using (BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light))
            {
                Assert.IsEmpty(twin.Cards(PublishedName), "The twin's Published made no card before its switch.");
                int before = CardView.MadeCount;
                Switch(twin, 1);
                twinMade = CardView.MadeCount - before;
                twinTitles = Titles(twin);
            }

            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = board.View.Preparer;
            Assert.AreEqual(0, preparer.StepsRun, "Nothing ran before the test called it.");
            Assert.IsEmpty(board.Cards(PublishedName), "Published made no card before preparation.");
            int start = CardView.MadeCount;
            int steps = preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            List<string> preparedTitles = Titles(board);
            Assert.IsTrue(preparer.IsPrepared, "Published is prepared.");
            Assert.AreEqual(2, steps, "Two steps ran: the width and the layout.");
            Assert.IsFalse(preparer.RunStep(), "Prepared, no step is due.");
            AssertOnlyShown(board, 0, "after preparation");

            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            facts.Add($"card views: the twin's first switch made {twinMade}; preparation made {prepared} ({preparedTitles.Count} on the shelf); the prepared first switch made {atSwitch}");

            Assert.IsGreaterThan(0, twinMade, "The twin's first switch made the first screen's card views.");
            Assert.AreEqual(twinMade, prepared, "Preparation made the card views the twin's first switch made.");
            CollectionAssert.AreEqual(twinTitles, preparedTitles, "Prepared, Published holds the twin's cards.");
            Assert.AreEqual(0, atSwitch, "The prepared first switch made no card view.");
            CollectionAssert.AreEqual(twinTitles, Titles(board), "After the switch, the twin's cards.");
            AssertDrawnRight(board, "after the prepared switch");
            return facts;
        }));
    }

    [TestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void T3_APreparedShelfShowsAsTheUnpreparedOne(string theme)
    {
        ThemeVariant variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            (byte[] Pixels, string Scroll) unpreparedBefore;
            (byte[] Pixels, string Scroll) unprepared;
            using (BoardWindow twin = BoardWindow.Show(session, variant))
            {
                unpreparedBefore = Frame(twin);
                Switch(twin, 1);
                unprepared = Frame(twin);
            }

            using BoardWindow board = BoardWindow.Show(session, variant);
            board.View.Preparer.RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");

            // Before the switch nothing of the preparation is drawn.
            int differBefore = DifferingPixels(unpreparedBefore.Pixels, Frame(board).Pixels);
            if (differBefore != 0)
            {
                string path = Pictures.Save(board.Window, $"pipeline-prepared-{theme}", TestContext);
                Assert.Fail($"Before the switch, {differBefore} pixels differ from the unprepared Board's picture; the prepared one is at {path}.");
            }

            facts.Add($"{theme}: before the switch the pictures are equal byte for byte");
            Switch(board, 1);
            (byte[] Pixels, string Scroll) prepared = Frame(board);
            facts.Add($"{theme}: unprepared {unprepared.Scroll}; prepared {prepared.Scroll}");

            Assert.AreEqual(unprepared.Scroll, prepared.Scroll, "The scroll extent, viewport, offset and thumb are the unprepared switch's.");
            int differ = DifferingPixels(unprepared.Pixels, prepared.Pixels);
            if (differ != 0)
            {
                string path = Pictures.Save(board.Window, $"published-prepared-{theme}", TestContext);
                Assert.Fail($"{differ} pixels differ from the unprepared switch's picture; the prepared one is at {path}.");
            }

            facts.Add($"{theme}: the pictures are equal byte for byte ({prepared.Pixels.Length} bytes)");
            return facts;
        }));
    }

    [TestMethod]
    public void T4_TheBordersFollowTheShelfTabAfterPreparation()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            board.View.Preparer.RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");
            AssertOnlyShown(board, 0, "after preparation");
            foreach (int index in new[] { 1, 0, 2, 1 })
            {
                Switch(board, index);
                AssertOnlyShown(board, index, $"switched to {index}");
                facts.Add($"switched to {index}: only {s_shelfNames[index]} shown");
            }

            Assert.AreEqual(2, board.View.Preparer.StepsRun, "Once shown, Published is not prepared again.");
            Switch(board, 0);
            Assert.IsFalse(board.View.Preparer.RunStep(), "Published shown once: no step.");
            return facts;
        }));
    }

    // Published shown and hidden again between two steps: the shelf was
    // shown once, so no further step runs and it is not called prepared.
    [TestMethod]
    public void T4_AShowAndHideBetweenStepsStopsPreparationForGood()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = board.View.Preparer;
            Assert.IsTrue(preparer.RunStep(), "The width step ran.");
            Switch(board, 1);
            Switch(board, 0);
            int stepsBefore = preparer.StepsRun;
            Assert.IsFalse(preparer.RunStep(), "Shown once between two steps: no step runs.");
            Assert.AreEqual(0, preparer.RunToEnd(), "Nor later.");
            Assert.IsFalse(preparer.IsPrepared, "A shelf shown once is not called prepared.");
            Assert.AreEqual(stepsBefore, preparer.StepsRun, "No step ran after the show.");
            AssertOnlyShown(board, 0, "back on Pipeline");
            return new List<string> { $"after the width step, a show and a hide: steps run {preparer.StepsRun}, prepared {preparer.IsPrepared}" };
        }));
    }

    [TestMethod]
    public void T6_TheTreeAndTabOrderAreTodaysAfterPreparation_Example()
    {
        using DiskLibrarySession session = OpenExample("example");
        Write(CompareTrees(session, "board-tree-example", ExampleTabLimit));
    }

    [TestMethod]
    public void T6_TheTreeAndTabOrderAreTodaysAfterPreparation_FiveHundred()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(CompareTrees(session, "board-tree-500", FiveHundredTabLimit));
    }

    // Published with no card: preparation runs and makes nothing, the switch
    // makes nothing, the empty message shows, and the tree is the twin's.
    [TestMethod]
    public void T11_AnEmptyPublishedIsPrepared()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        var session = WithoutPublished(disk);
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            string twinTree;
            using (BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light))
            {
                Switch(twin, 1);
                twinTree = AccessibilityTreeTests.AccessibilityTree.Describe(twin, FiveHundredTabLimit);
            }

            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            Assert.HasCount(0, board.Board.Shelves[1].Cards, "Published has no card.");
            int start = CardView.MadeCount;
            board.View.Preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            Assert.IsTrue(board.View.Preparer.IsPrepared, "An empty Published is prepared.");
            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            facts.Add($"empty Published: preparation made {prepared} card views, the switch {atSwitch}");
            Assert.AreEqual(0, prepared, "Preparation made no card view.");
            Assert.AreEqual(0, atSwitch, "The switch made no card view.");
            Assert.IsTrue(board.EmptyMessage(PublishedName).IsEffectivelyVisible, "The empty message is shown.");
            Assert.AreEqual(twinTree, AccessibilityTreeTests.AccessibilityTree.Describe(board, FiveHundredTabLimit), "The tree and Tab order are the twin's.");
            return facts;
        }));
    }

    // Published smaller than a screen: the example's one Published card.
    [TestMethod]
    public void T11_AShortPublishedIsPrepared()
    {
        using DiskLibrarySession session = OpenExample("example");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            int twinMade;
            List<string> twinTitles;
            using (BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light))
            {
                int before = CardView.MadeCount;
                Switch(twin, 1);
                twinMade = CardView.MadeCount - before;
                twinTitles = Titles(twin);
            }

            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            int start = CardView.MadeCount;
            board.View.Preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            Assert.IsTrue(board.View.Preparer.IsPrepared, "A short Published is prepared.");
            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            facts.Add($"short Published ({board.Board.Shelves[1].Cards.Count} cards): the twin's switch made {twinMade}, preparation {prepared}, the prepared switch {atSwitch}");
            Assert.IsGreaterThan(0, twinMade, "The twin's switch made the shelf's card views.");
            Assert.AreEqual(twinMade, prepared, "Preparation made the twin's card views.");
            Assert.AreEqual(0, atSwitch, "The prepared switch made no card view.");
            CollectionAssert.AreEqual(twinTitles, Titles(board), "The twin's cards.");
            AssertDrawnRight(board, "the short Published");
            return facts;
        }));
    }

    // The first cards arrive on a prepared, empty Published: its rows wait
    // for the switch (this spike has no step that brings them current), the
    // switch brings them current and makes the views the twin's switch makes,
    // and every view draws its own card.
    [TestMethod]
    public void T11_TheFirstCardsArrivingOnAPreparedEmptyPublishedAreDrawnRight()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        List<Manuscript> arriving = disk.Manuscripts.Where(manuscript => manuscript is not null && manuscript.Location == ManuscriptLocation.Published).ToList();
        Assert.IsNotEmpty(arriving, "The 500 library has Published manuscripts.");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            int twinMade;
            List<string> twinTitles;
            MemorySession twinSession = WithoutPublished(disk);
            using (BoardWindow twin = BoardWindow.Show(twinSession, ThemeVariant.Light))
            {
                twinSession.Library.AddRange(arriving);
                twinSession.RaiseChanged();
                Settle(twin);
                int before = CardView.MadeCount;
                Switch(twin, 1);
                twinMade = CardView.MadeCount - before;
                twinTitles = Titles(twin);
            }

            MemorySession session = WithoutPublished(disk);
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            board.View.Preparer.RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "The empty Published is prepared.");
            session.Library.AddRange(arriving);
            session.RaiseChanged();
            Settle(board);
            ShelfViewModel published = board.Board.Shelves[1];
            Assert.IsNotEmpty(published.Cards, "The cards arrived on the hidden Published.");
            Assert.IsTrue(published.RowsAreStale, "Its rows wait for the switch.");
            AssertOnlyShown(board, 0, "after the cards arrived");

            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            facts.Add($"first cards on a prepared empty Published ({published.Cards.Count}): the twin's switch made {twinMade}, the prepared switch {atSwitch}");
            Assert.IsFalse(published.RowsAreStale, "Shown, its rows are current.");
            Assert.AreEqual(twinMade, atSwitch, "The switch made the twin's card views, no more.");
            CollectionAssert.AreEqual(twinTitles, Titles(board), "The twin's cards.");
            Assert.IsFalse(board.EmptyMessage(PublishedName).IsEffectivelyVisible, "The empty message went.");
            AssertDrawnRight(board, "the first cards on a prepared Published");
            return facts;
        }));
    }

    // T6: the window's tree and Tab walk with preparation done, against the
    // checked-in text; then, on fresh Boards, the walk after the first switch,
    // prepared and not.
    private static List<string> CompareTrees(ILibrarySession session, string expected, int limit)
    {
        string prepared = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            board.View.Preparer.RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");
            return AccessibilityTreeTests.AccessibilityTree.Describe(board, limit);
        });
        AccessibilityTreeTests.Compare(expected, prepared);

        (string twinTree, string preparedTree) = Headless.Run(() =>
        {
            string twinTree;
            using (BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light))
            {
                Switch(twin, 1);
                twinTree = AccessibilityTreeTests.AccessibilityTree.Describe(twin, limit);
            }

            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            board.View.Preparer.RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");
            Switch(board, 1);
            return (twinTree, AccessibilityTreeTests.AccessibilityTree.Describe(board, limit));
        });

        if (twinTree != preparedTree)
        {
            string[] want = twinTree.Split('\n');
            string[] got = preparedTree.Split('\n');
            int line = 0;
            while (line < want.Length && line < got.Length && want[line] == got[line])
            {
                line++;
            }

            Assert.Fail($"After the switch, the prepared Board's tree differs from the twin's at line {line + 1}:\n" +
                $"  twin:     {(line < want.Length ? want[line] : "(end)")}\n" +
                $"  prepared: {(line < got.Length ? got[line] : "(end)")}");
        }

        return [$"{expected}: equal with preparation done; after the switch equal to the twin's ({preparedTree.Split('\n').Length} lines)"];
    }

    private static void Switch(BoardWindow board, int index)
    {
        board.Board.SelectedShelfIndex = index;
        Settle(board);
        Assert.AreEqual(index, board.Board.SelectedShelfIndex, $"The shelf {index} is chosen.");
    }

    private static void Settle(BoardWindow board)
    {
        Dispatcher.UIThread.RunJobs();
        board.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    // Exactly one shelf's Border is visible, the one at index, and only its
    // shelf is shown.
    private static void AssertOnlyShown(BoardWindow board, int index, string step)
    {
        List<Border> borders = board.Find<Panel>("ShelfHost").Children.OfType<Border>().ToList();
        Assert.HasCount(3, borders, $"{step}: the shelf host holds three Borders.");
        for (int at = 0; at < borders.Count; at++)
        {
            Assert.AreEqual(at == index, borders[at].IsVisible, $"{step}: the Border of {s_shelfNames[at]} is {(at == index ? "shown" : "hidden")}.");
            Assert.AreEqual(at == index, board.Shelf(s_shelfNames[at]).IsEffectivelyVisible, $"{step}: {s_shelfNames[at]} is {(at == index ? "shown" : "hidden")}.");
        }
    }

    // Every card view laid out on the shown Published shelf draws the card
    // at its place, and the first card is drawn.
    private static void AssertDrawnRight(BoardWindow board, string step)
    {
        ShelfView shelf = board.Shelf(PublishedName);
        ShelfViewModel model = board.Board.Shelves[1];
        Assert.IsFalse(model.RowsAreStale, $"{step}: the rows are current.");
        int drawn = 0;
        foreach (CardView view in shelf.GetVisualDescendants().OfType<CardView>().Where(view => view.IsEffectivelyVisible))
        {
            CardRowView row = view.FindAncestorOfType<CardRowView>() ?? throw new AssertFailedException($"{step}: a card view outside a row.");
            Assert.IsNotNull(row.Row, $"{step}: a laid-out card view in a row kept for reuse.");
            int place = (row.Row.Index * Math.Max(1, model.Columns)) + row.Children.IndexOf(view);
            Assert.IsLessThan(model.Cards.Count, place, $"{step}: a card view at place {place} past the shelf's cards.");
            CardViewModel card = model.Cards[place];
            Assert.IsNull(CardParts.DrawnWrong(view, card), $"{step}: the view of \"{card.Title}\" {CardParts.DrawnWrong(view, card)}.");
            drawn++;
        }

        Assert.IsGreaterThan(0, drawn, $"{step}: cards are drawn.");
        Assert.IsNotNull(shelf.FindCardView(0), $"{step}: the first card is drawn.");
    }

    private static List<string> Titles(BoardWindow board) =>
        board.Cards(PublishedName).Select(view => ((CardViewModel)view.DataContext!).Title).ToList();

    // The frame drawn once transitions end, as Pictures.Save draws it, and
    // the Published scroller's extent, viewport, offset and vertical thumb.
    private static (byte[] Pixels, string Scroll) Frame(BoardWindow board)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = board.Window.CaptureRenderedFrame();
        Assert.IsNotNull(frame, "The headless platform captured a frame.");
        byte[] pixels;
        using (ILockedFramebuffer buffer = frame.Lock())
        {
            pixels = new byte[buffer.RowBytes * buffer.Size.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        }

        ScrollViewer scroller = board.Shelf(PublishedName).FindControl<ScrollViewer>("Scroller")
            ?? throw new AssertFailedException("The Published shelf has no scroller.");
        Thumb? thumb = scroller.GetVisualDescendants().OfType<Thumb>()
            .FirstOrDefault(candidate => candidate.FindAncestorOfType<ScrollBar>() is { Orientation: Orientation.Vertical });
        string scroll = $"extent {scroller.Extent}, viewport {scroller.Viewport}, offset {scroller.Offset}, " +
            $"thumb {(thumb is null ? "none" : $"{thumb.Bounds} shown {thumb.IsEffectivelyVisible}")}";
        return (pixels, scroll);
    }

    private static int DifferingPixels(byte[] first, byte[] second)
    {
        if (first.Length != second.Length)
        {
            return int.MaxValue;
        }

        int differ = 0;
        for (int index = 0; index + 3 < first.Length; index += 4)
        {
            if (BitConverter.ToUInt32(first, index) != BitConverter.ToUInt32(second, index))
            {
                differ++;
            }
        }

        return differ;
    }

    private void Write(List<string> facts)
    {
        foreach (string fact in facts)
        {
            TestContext.WriteLine(fact);
        }
    }

    // The 500 library without its Published manuscripts, in memory.
    private static MemorySession WithoutPublished(DiskLibrarySession disk) =>
        new(disk.Manuscripts.Where(manuscript => manuscript is not null && manuscript.Location != ManuscriptLocation.Published),
            LargeLibraryFixture.Today, disk.Settings, disk.AuthorLibrary);

    private DiskLibrarySession OpenFiveHundred(string folder)
    {
        string root = Path.Combine(_root, folder);
        Directory.CreateDirectory(root);
        LargeLibraryFixture.Create(root, 500);
        DiskLibrarySession session = Load(root, Path.Combine(root, "settings"));
        Assert.HasCount(500, session.Manuscripts);
        return session;
    }

    private DiskLibrarySession OpenExample(string folder)
    {
        string root = Path.Combine(_root, folder);
        Directory.CreateDirectory(root);
        LargeLibraryFixture.WriteExample(root);
        DiskLibrarySession session = Load(root, Path.Combine(root, "settings"));
        Assert.HasCount(7, session.Manuscripts);
        return session;
    }

    private static DiskLibrarySession Load(string root, string settings)
    {
        var session = new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            settings,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return session;
    }
}
