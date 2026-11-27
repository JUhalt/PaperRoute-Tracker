using System.Runtime.InteropServices;
using Avalonia;
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
// (HiddenShelfPreparer; the idle-preparation design: the width step W, the
// ladder of layout steps L, run by RunStep or RunToEnd).
// Each test compares a prepared Board with a twin Board over the same library
// that was not prepared, which switches as today. Preparation runs only where
// a test calls it.
// - T1: after preparation Published holds the card views the twin's first
//   switch makes, for the same cards, and its own first switch makes none
//   (CardView.MadeCount, a count that does not depend on the PC's load).
// - T2: the views each step makes follow the ladder, and a slow step stops
//   preparation for the session.
// - T3: before and after the switch the window draws the same picture, byte
//   for byte, light and dark; after it, with the same scroll extent and thumb.
// - T4: the shelf tab still decides which shelf is shown.
// - T5: a switch after any number of steps shows the whole first screen.
// - T9: a change while Published is prepared or half prepared never shows
//   an old card, and after the idle refresh the switch makes no view and
//   refills no row.
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

    // W, then the ladder's capped L steps and the full-size one.
    private const int FirstPreparationSteps = 1 + HiddenShelfPreparer.LadderSteps + 1;

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
            HiddenShelfPreparer preparer = Still(board);
            Assert.AreEqual(0, preparer.StepsRun, "Nothing ran before the test called it.");
            Assert.IsEmpty(board.Cards(PublishedName), "Published made no card before preparation.");
            int start = CardView.MadeCount;
            int steps = preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            List<string> preparedTitles = Titles(board);
            Assert.IsTrue(preparer.IsPrepared, "Published is prepared.");
            Assert.AreEqual(FirstPreparationSteps, steps, "The width step and the ladder's layout steps ran.");
            Assert.IsFalse(preparer.RunStep(), "Prepared, no step is due.");
            AssertOnlyShown(board, 0, "after preparation");
            List<CardView> preparedViews = board.Cards(PublishedName).ToList();
            List<object?> preparedCards = preparedViews.Select(view => view.DataContext).ToList();

            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;

            // A frame drawn after the switch makes none either: no work of the
            // first screen waits for the first drawing.
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            board.Window.UpdateLayout();
            int afterFrame = CardView.MadeCount - beforeSwitch;
            List<CardView> shownViews = board.Cards(PublishedName).ToList();
            facts.Add($"card views: the twin's first switch made {twinMade}; preparation made {prepared} ({preparedTitles.Count} on the shelf); the prepared first switch made {atSwitch}, {afterFrame} with the frame after it");

            Assert.IsGreaterThan(0, twinMade, "The twin's first switch made the first screen's card views.");
            Assert.AreEqual(twinMade, prepared, "Preparation made the card views the twin's first switch made.");
            CollectionAssert.AreEqual(twinTitles, preparedTitles, "Prepared, Published holds the twin's cards.");
            Assert.AreEqual(0, atSwitch, "The prepared first switch made no card view.");
            Assert.AreEqual(0, afterFrame, "Nor did the frame drawn after it.");
            CollectionAssert.AreEqual(twinTitles, Titles(board), "After the switch, the twin's cards.");

            // Not refilled and not rebound: the switch shows the very views
            // preparation made, each with the card it was given then.
            Assert.HasCount(preparedViews.Count, shownViews, "The switch shows as many card views as preparation made.");
            for (int at = 0; at < preparedViews.Count; at++)
            {
                Assert.AreSame(preparedViews[at], shownViews[at], $"Card view {at} is the one preparation made.");
                Assert.AreSame(preparedCards[at], shownViews[at].DataContext, $"Card view {at} keeps the card preparation gave it.");
            }
            AssertDrawnRight(board, "after the prepared switch");
            return facts;
        }));
    }

    // The views each step makes follow the ladder: the layout steps together
    // make the twin's first screen, none makes more than about half of it,
    // each makes no more than the ladder predicts from the steps before it,
    // and the last is the largest.
    [TestMethod]
    public void T2_EachSliceStaysSmall()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            int twinMade = TwinFirstSwitchViews(session);
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Still(board);
            preparer.RunToEnd();
            IReadOnlyList<HiddenShelfPreparer.Step> steps = preparer.Steps;
            Assert.IsTrue(preparer.IsPrepared, "Published is prepared.");
            Assert.IsNull(preparer.SkippedReason, "No step was slow enough to stop preparation.");
            int columns = board.Board.Shelves[1].Columns;
            facts.Add($"twin's first switch: {twinMade} card views; columns {columns}; steps: " +
                string.Join(", ", steps.Select(step => $"{step.Kind}{(step.Cap is { } cap ? $"(cap {cap:0})" : string.Empty)} {step.ViewsMade}")));

            Assert.AreEqual("W,L,L,L,L", string.Join(",", steps.Select(step => step.Kind)), "W, three capped layout steps, one at full height.");
            Assert.AreEqual(0, steps[0].ViewsMade, "W makes no view.");
            Assert.IsNull(steps[^1].Cap, "The last layout step is at full height.");
            List<int> made = steps.Skip(1).Select(step => step.ViewsMade).ToList();
            Assert.AreEqual(twinMade, made.Sum(), "The layout steps together make the twin's first screen.");

            // About half: half the first screen, rounded up to whole rows.
            int half = (int)Math.Ceiling(twinMade / 2.0 / columns) * columns;
            int soFar = 0;
            for (int at = 0; at < made.Count; at++)
            {
                Assert.IsLessThanOrEqualTo(half, made[at], $"Layout step {at + 1} made {made[at]} views, more than about half the first screen ({half}).");
                if (at > 0)
                {
                    int predicted = (int)Math.Ceiling(soFar * (HiddenShelfPreparer.LadderRatio - 1)) + columns;
                    Assert.IsLessThanOrEqualTo(predicted, made[at], $"Layout step {at + 1} made {made[at]} views, more than the ladder predicts with a row to spare ({predicted}).");
                }

                soFar += made[at];
            }

            Assert.AreEqual(made.Max(), made[^1], "The last layout step is the largest.");
            return facts;
        }));
    }

    // The slow-slice stop, with a test clock: a step longer than the
    // threshold stops preparation at once; a step under it whose time per
    // view predicts the next one over it stops it too (the first capped
    // layout step of 110 ms predicts 1.2 x 110 = 132 ms); steps of 40 ms run
    // to the end (the ladder's steps make about twice the views of the step
    // before, so each predicts at most 2.4 x 40 = 96 ms). A stopped Board
    // switches as the twin does, making the rest of the first screen.
    [TestMethod]
    [DataRow(130, 1)]
    [DataRow(110, 2)]
    [DataRow(40, FirstPreparationSteps)]
    public void T2_ASlowSliceStopsPreparation(int stepMilliseconds, int stepsBeforeStop)
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            int twinMade = TwinFirstSwitchViews(session);
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Still(board);
            TimeSpan now = TimeSpan.Zero;
            TimeSpan step = TimeSpan.FromMilliseconds(stepMilliseconds);

            // Called once as a step starts and once as it ends: each step takes step.
            preparer.Clock = () => now += step;
            int start = CardView.MadeCount;
            int ran = preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            int beforeSwitch = CardView.MadeCount;
            bool stopped = preparer.SkippedReason is not null;
            bool wasPrepared = preparer.IsPrepared;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            string fact = $"steps of {stepMilliseconds} ms: {ran} ran, skipped reason {preparer.SkippedReason ?? "none"}, longest {preparer.Longest.TotalMilliseconds} ms; preparation made {prepared}, the switch {atSwitch} (twin {twinMade})";

            Assert.AreEqual(stepsBeforeStop, ran, "The steps that ran before the stop.");
            Assert.AreEqual(step, preparer.Longest, "The longest step is the test clock's step.");
            if (stepsBeforeStop < FirstPreparationSteps)
            {
                Assert.IsTrue(stopped, "Preparation stopped.");
                Assert.AreEqual(HiddenShelfPreparer.SlowSliceReason, preparer.SkippedReason, "Stopped as a slow slice.");
                Assert.IsFalse(wasPrepared, "Stopped early, Published is not prepared.");
            }
            else
            {
                Assert.IsFalse(stopped, "No step was slow.");
                Assert.IsTrue(wasPrepared, "Published is prepared.");
            }

            Assert.AreEqual(twinMade, prepared + atSwitch, "Preparation and the switch together made the twin's views, no more.");
            AssertDrawnRight(board, $"after steps of {stepMilliseconds} ms");
            return new List<string> { fact };
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
            Still(board).RunToEnd();
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
            Still(board).RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");
            AssertOnlyShown(board, 0, "after preparation");
            foreach (int index in new[] { 1, 0, 2, 1 })
            {
                Switch(board, index);
                AssertOnlyShown(board, index, $"switched to {index}");
                facts.Add($"switched to {index}: only {s_shelfNames[index]} shown");
            }

            Assert.AreEqual(FirstPreparationSteps, board.View.Preparer.StepsRun, "Once shown, Published is not prepared again.");
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
            HiddenShelfPreparer preparer = Still(board);
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
            Still(board).RunToEnd();
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
            Still(board).RunToEnd();
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

    // The first cards arrive on a prepared, empty Published: its rows wait,
    // and it is no longer called prepared; the idle refresh (R, then one
    // full-size L) makes the views the twin's switch makes, the switch then
    // makes none and refills no row, and every view draws its own card.
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
            Still(board).RunToEnd();
            Assert.IsTrue(board.View.Preparer.IsPrepared, "The empty Published is prepared.");
            session.Library.AddRange(arriving);
            session.RaiseChanged();
            Settle(board);
            ShelfViewModel published = board.Board.Shelves[1];
            Assert.IsNotEmpty(published.Cards, "The cards arrived on the hidden Published.");
            Assert.IsTrue(published.RowsAreStale, "Its rows wait for the switch.");
            Assert.IsFalse(board.View.Preparer.IsPrepared, "With its rows stale, Published is no longer called prepared.");
            AssertOnlyShown(board, 0, "after the cards arrived");

            HiddenShelfPreparer preparer = board.View.Preparer;
            int stepsBefore = preparer.StepsRun;
            int beforeRefresh = CardView.MadeCount;
            preparer.RunToEnd();
            int refreshed = CardView.MadeCount - beforeRefresh;
            string kinds = string.Join(",", preparer.Steps.Skip(stepsBefore).Select(step => step.Kind));
            Assert.AreEqual("R,L", kinds, "The refresh: the rows brought current, then one full-size layout.");
            Assert.IsTrue(preparer.IsPrepared, "Refreshed, Published is prepared again.");
            AssertOnlyShown(board, 0, "after the refresh");

            using var refills = new RowRefills(published);
            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            facts.Add($"first cards on a prepared empty Published ({published.Cards.Count}): the twin's switch made {twinMade}; the refresh ({kinds}) made {refreshed}; the prepared switch made {atSwitch} and refilled {refills.Count} rows");
            Assert.IsFalse(published.RowsAreStale, "Shown, its rows are current.");
            Assert.AreEqual(twinMade, refreshed, "The refresh made the twin's card views.");
            Assert.AreEqual(0, atSwitch, "The switch made no card view.");
            Assert.AreEqual(0, refills.Count, "The switch refilled no row.");
            CollectionAssert.AreEqual(twinTitles, Titles(board), "The twin's cards.");
            Assert.IsFalse(board.EmptyMessage(PublishedName).IsEffectivelyVisible, "The empty message went.");
            AssertDrawnRight(board, "the first cards on a prepared Published");
            return facts;
        }));
    }

    // T9: a change while Published is prepared, and while it is half
    // prepared (W and two capped layout steps): the change ends IsPrepared
    // (a refresh is due), the idle refresh brings it back (W after a resize,
    // R when the rows went stale, then one full-size L, or the rest of the
    // ladder), and every time Published's Border becomes visible, for a step
    // or for the switch, its rows and row views hold its cards in the shown
    // shelf's columns. The switch then makes no card view and refills no row,
    // and shows every card right. After a theme change the switch draws the
    // picture of a twin Board that had the same change and was not prepared.
    [TestMethod]
    [DataRow("search")]
    [DataRow("stage")]
    [DataRow("sort")]
    [DataRow("edit")]
    [DataRow("delete")]
    [DataRow("theme")]
    [DataRow("narrower")]
    [DataRow("wider")]
    [DataRow("taller")]
    [DataRow("scaling")]
    public void T9_AChangeWhilePreparedNeverShowsAnOldCard(string change)
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            byte[]? twinPixels = null;
            if (change == "theme")
            {
                using BoardWindow twin = BoardWindow.Show(WholeLibrary(disk), ThemeVariant.Light);
                Change(twin, null, change);
                Settle(twin);
                Switch(twin, 1);
                twinPixels = Frame(twin).Pixels;
            }

            foreach (int first in new[] { FirstPreparationSteps, 3 })
            {
                string mode = first == FirstPreparationSteps ? "prepared" : "half prepared";
                MemorySession session = WholeLibrary(disk);
                using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
                HiddenShelfPreparer preparer = Still(board);
                for (int step = 0; step < first; step++)
                {
                    Assert.IsTrue(preparer.RunStep(), $"{mode}: step {step + 1} ran.");
                }

                Assert.AreEqual(first == FirstPreparationSteps, preparer.IsPrepared, $"{mode}: prepared as expected before the change.");
                ShelfViewModel published = board.Board.Shelves[1];
                using var watch = new PublishedWatch(board);
                Change(board, session, change);
                Settle(board);
                Assert.IsFalse(preparer.IsPrepared, $"{change}, {mode}: after the change Published is not called prepared.");
                AssertOnlyShown(board, 0, $"{change}, {mode}: after the change");

                int stepsBefore = preparer.StepsRun;
                int beforeRefresh = CardView.MadeCount;
                preparer.RunToEnd();
                int refreshed = CardView.MadeCount - beforeRefresh;
                string kinds = string.Join(",", preparer.Steps.Skip(stepsBefore).Select(step => step.Kind + (step.Cap is null ? string.Empty : "c")));
                Assert.IsTrue(preparer.IsPrepared, $"{change}, {mode}: after the refresh Published is prepared.");
                Assert.IsNull(preparer.SkippedReason, $"{change}, {mode}: nothing stopped preparation.");
                AssertOnlyShown(board, 0, $"{change}, {mode}: after the refresh");

                using var refills = new RowRefills(published);
                int beforeSwitch = CardView.MadeCount;
                Switch(board, 1);
                int atSwitch = CardView.MadeCount - beforeSwitch;
                facts.Add($"{change}, {mode}: refresh steps {kinds} made {refreshed} card views; the switch made {atSwitch} and refilled {refills.Count} rows; {published.Cards.Count} cards, {published.Columns} columns; Published made visible {watch.Seen} times");
                watch.AssertCurrent($"{change}, {mode}");
                Assert.AreEqual(0, atSwitch, $"{change}, {mode}: the switch made no card view.");
                Assert.AreEqual(0, refills.Count, $"{change}, {mode}: the switch refilled no row.");
                if (published.Cards.Count == 0)
                {
                    Assert.IsTrue(board.EmptyMessage(PublishedName).IsEffectivelyVisible, $"{change}, {mode}: the empty message is shown.");
                }
                else
                {
                    AssertDrawnRight(board, $"{change}, {mode}");
                }

                if (twinPixels is not null && first == FirstPreparationSteps)
                {
                    int differ = DifferingPixels(twinPixels, Frame(board).Pixels);
                    if (differ != 0)
                    {
                        string path = Pictures.Save(board.Window, $"published-theme-changed-prepared", TestContext);
                        Assert.Fail($"After the theme change, {differ} pixels differ from the twin's picture; the prepared one is at {path}.");
                    }

                    facts.Add($"{change}, {mode}: the picture after the switch equals the twin's byte for byte");
                }
            }

            return facts;
        }));
    }

    // One change made to a Board, as a person would make it. The library's
    // own changes are made to the Board's session (a twin may pass null for
    // the ones that do not touch it).
    private static void Change(BoardWindow board, MemorySession? session, string change)
    {
        ShelfViewModel published = board.Board.Shelves[1];
        switch (change)
        {
            case "search":
                board.Board.SearchText = published.Cards[40].Title;
                board.Board.Render();
                break;
            case "stage":
                // Submitted: every Published manuscript of the test library is
                // at the Published stage, so this empties the shelf.
                board.Board.SelectedStageIndex = 3;
                break;
            case "sort":
                board.Board.SelectedSortIndex = 2;
                break;
            case "edit":
            {
                MemorySession library = session ?? throw new AssertFailedException("An edit needs the session.");
                string title = published.Cards[1].Title;
                Manuscript edited = library.Library.First(manuscript => manuscript.Location == ManuscriptLocation.Published && manuscript.Title == title);
                edited.Title = title + " (edited)";
                library.RaiseChanged();
                break;
            }

            case "delete":
            {
                MemorySession library = session ?? throw new AssertFailedException("A delete needs the session.");
                string title = published.Cards[0].Title;
                library.Library.RemoveAll(manuscript => manuscript.Location == ManuscriptLocation.Published && manuscript.Title == title);
                library.RaiseChanged();
                break;
            }

            case "theme":
                board.Window.RequestedThemeVariant = ThemeVariant.Dark;
                break;
            case "narrower":
                board.Window.Width = 1000;
                break;
            case "wider":
                board.Window.Width = 1600;
                break;
            case "taller":
                board.Window.Height = 1000;
                break;
            case "scaling":
                board.Window.SetRenderScaling(1.25);
                break;
            default:
                throw new AssertFailedException($"No change named {change}.");
        }
    }
    // A switch after k steps (none; W only; W and one, two or three capped
    // layout steps): as Published becomes visible its rows and row views
    // hold its cards, in the shown shelf's columns once W has run; at the end
    // it holds the twin's cards, drawn right, and preparation and the switch
    // together made no more views than the twin's switch.
    [TestMethod]
    public void T5_ASwitchAfterKStepsShowsTheWholeFirstScreen()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
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

            for (int k = 0; k < FirstPreparationSteps; k++)
            {
                using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
                HiddenShelfPreparer preparer = Still(board);
                int start = CardView.MadeCount;
                for (int step = 0; step < k; step++)
                {
                    Assert.IsTrue(preparer.RunStep(), $"k = {k}: step {step + 1} ran.");
                }

                int prepared = CardView.MadeCount - start;
                Assert.IsFalse(preparer.IsPrepared, $"k = {k}: not yet prepared.");
                using var watch = new PublishedWatch(board, checkColumns: k > 0);
                int beforeSwitch = CardView.MadeCount;
                Switch(board, 1);
                int atSwitch = CardView.MadeCount - beforeSwitch;
                facts.Add($"k = {k}: preparation made {prepared}, the switch {atSwitch} (twin {twinMade})");
                watch.AssertCurrent($"k = {k}");
                Assert.AreEqual(twinMade, prepared + atSwitch, $"k = {k}: preparation and the switch made the twin's views, no more.");
                CollectionAssert.AreEqual(twinTitles, Titles(board), $"k = {k}: the twin's cards.");
                AssertDrawnRight(board, $"k = {k}");
            }

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
            Still(board).RunToEnd();
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
            Still(board).RunToEnd();
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

    // The card views a twin Board over the same library, not prepared,
    // makes at its first switch to Published.
    private static int TwinFirstSwitchViews(ILibrarySession session)
    {
        using BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light);
        int before = CardView.MadeCount;
        Switch(twin, 1);
        return CardView.MadeCount - before;
    }

    // The Board's preparer with a clock that stands still, so no step counts
    // as slow however busy the PC is: the tests that count views are not
    // about time. The slow-slice stop has its own test with a test clock.
    private static HiddenShelfPreparer Still(BoardWindow board)
    {
        HiddenShelfPreparer preparer = board.View.Preparer;
        preparer.Clock = static () => TimeSpan.Zero;
        return preparer;
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

    // Watches Published's Border become visible, by the switch or by a
    // preparation step, and checks at that moment, before any layout or
    // frame, that its rows are current, in the shown Pipeline's columns and
    // card width (unless told not to: before W they are not), and that every
    // row view holds its row's cards (as HiddenShelfTests' ShownWatch).
    private sealed class PublishedWatch : IDisposable
    {
        private readonly BoardWindow _board;
        private readonly bool _checkColumns;
        private readonly Control _host;
        private readonly List<string> _seen = [];

        public PublishedWatch(BoardWindow board, bool checkColumns = true)
        {
            _board = board;
            _checkColumns = checkColumns;
            _host = (Control)(board.Shelf(PublishedName).GetVisualParent() ?? throw new AssertFailedException("Published has no host."));
            _host.PropertyChanged += OnHostChanged;
        }

        public int Seen { get; private set; }

        public void AssertCurrent(string step)
        {
            Assert.IsNotEmpty(_seen, $"{step}: Published was made visible.");
            Assert.AreEqual(string.Empty, string.Join(", ", _seen.Where(seen => seen != "current").Distinct()), $"{step}: as Published became visible, its rows and row views held its cards.");
            _seen.Clear();
        }

        public void Dispose() => _host.PropertyChanged -= OnHostChanged;

        private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Visual.IsVisibleProperty || e.NewValue is not true)
            {
                return;
            }

            Seen++;
            ShelfViewModel model = _board.Board.Shelves[1];
            ShelfViewModel shown = _board.Board.Shelves[0];
            if (!model.IsRealized)
            {
                _seen.Add("current");
                return;
            }

            bool columns = !_checkColumns || (model.Columns == shown.Columns && model.ItemWidth == shown.ItemWidth);
            bool rows = !model.RowsAreStale && RowsHoldCards(model);
            bool views = (_board.Shelf(PublishedName).FindControl<CardRowList>("Rows")?.ItemsPanelRoot?.Children ?? [])
                .OfType<CardRowView>()
                .Where(view => view.IsVisible && view.Row is not null)
                .All(view => view.Row!.Cards.Select((card, slot) => view.CardAt(slot) is { } made && ReferenceEquals(made.DataContext, card)).All(same => same));
            _seen.Add(columns && rows && views ? "current" : $"stale (columns {columns}, rows {rows}, views {views})");
        }

        // Row i holds cards i x N to i x N + N - 1 of the shelf's cards.
        private static bool RowsHoldCards(ShelfViewModel model)
        {
            int columns = Math.Max(1, model.Columns);
            if (model.Rows.Count != (model.Cards.Count + columns - 1) / columns)
            {
                return false;
            }

            for (int index = 0; index < model.Rows.Count; index++)
            {
                IReadOnlyList<CardViewModel> cards = model.Rows[index].Cards;
                for (int slot = 0; slot < cards.Count; slot++)
                {
                    if (!ReferenceEquals(cards[slot], model.Cards[(index * columns) + slot]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    // Counts Published's rows refilled (a row given other cards) and rows
    // added or removed while it is watched.
    private sealed class RowRefills : IDisposable
    {
        private readonly ShelfViewModel _shelf;
        private readonly List<CardRowViewModel> _rows;

        public RowRefills(ShelfViewModel shelf)
        {
            _shelf = shelf;
            _rows = [.. shelf.Rows];
            foreach (CardRowViewModel row in _rows)
            {
                row.PropertyChanged += OnRowChanged;
            }

            shelf.Rows.CollectionChanged += OnRowsChanged;
        }

        public int Count { get; private set; }

        public void Dispose()
        {
            foreach (CardRowViewModel row in _rows)
            {
                row.PropertyChanged -= OnRowChanged;
            }

            _shelf.Rows.CollectionChanged -= OnRowsChanged;
        }

        private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CardRowViewModel.Cards))
            {
                Count++;
            }
        }

        private void OnRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Count++;
    }

    // The whole 500 library, in memory, so a test can change it.
    private static MemorySession WholeLibrary(DiskLibrarySession disk) =>
        new(disk.Manuscripts.Where(manuscript => manuscript is not null), LargeLibraryFixture.Today, disk.Settings, disk.AuthorLibrary);

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
