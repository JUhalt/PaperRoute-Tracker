using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
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
//   preparation for the session; a refresh that gives Published its cards
//   back climbs the ladder, and a full-size refresh predicted over the limit
//   is not run.
// - T3: before and after the switch the window draws the same picture, byte
//   for byte, light and dark; after it, with the same scroll extent and thumb.
// - T4: the shelf tab still decides which shelf is shown.
// - T5: a switch after any number of steps shows the whole first screen.
// - T9: a change while Published is prepared or half prepared never shows
//   an old card, and after the idle refresh the switch makes no view,
//   refills no row, and leaves the tree and Tab walk of a twin that had the
//   same change unprepared.
// - T7: once an accessibility client has read the shelf host's children, or
//   walked up from a card, preparation is skipped and raises no event, also
//   when the client reads them only after preparation or between two steps;
//   a client that read the Board only above the shelf host hears nothing
//   while preparation runs.
// - T8: the idle rules: nothing runs at startup, while keys come closer than
//   0.4 s, while a pointer button is held, a list or a card menu is open, the
//   Board is hidden or the window minimized, nor twice without a frame; a
//   refresh waits for 2 s after the last render.
// - T10: the idle timer itself prepares Published and stops; closing the
//   window midway is safe; a show and hide between two ticks stops it.
// - T6: with preparation done, or half done, the Board's accessibility tree
//   and Tab order are the checked-in ones, and after the switch they are the
//   twin's; the keyboard stays where it was through every step, and Tab and
//   Shift+Tab then reach what they reach on the twin.
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

    // PerViewClock's cost of one card view: about a warm card view's cost
    // headless (the design's figure), so a step of 18 views is over the
    // 120 ms limit.
    private const int PerViewMs = 7;

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
            Assert.AreEqual(prepared, AssertCardsLaidOut(board, "prepared, before the switch"), "Every card view preparation made is laid out, ready to show.");

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

    // A search with no Published match while the first preparation runs,
    // then the search cleared: the empty shelf was not laid out, so the
    // refresh climbs the whole ladder and no step makes a screen at once.
    // The clock is a load-free model: each step takes 7 ms for each card
    // view it made (PerViewClock), so a step of 18 views or more would be
    // over the limit. No step goes over it, Published is prepared, and the
    // switch makes no card view.
    [TestMethod]
    public void T2_ARefreshAfterASearchWithNoMatchClimbsTheLadder()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            int twinMade = TwinFirstSwitchViews(WholeLibrary(disk));
            using BoardWindow board = BoardWindow.Show(WholeLibrary(disk), ThemeVariant.Light);
            HiddenShelfPreparer preparer = board.View.Preparer;
            preparer.Clock = PerViewClock;
            ShelfViewModel published = board.Board.Shelves[1];
            board.Board.SearchText = "zzzz no such paper";
            board.Board.Render();
            Settle(board);
            Assert.IsEmpty(published.Cards, "The search leaves Published empty.");
            int start = CardView.MadeCount;
            preparer.RunToEnd();
            Assert.AreEqual("W", string.Join(",", preparer.Steps.Select(step => step.Kind)), "An empty Published is given its width and not laid out.");
            Assert.IsTrue(preparer.IsPrepared, "An empty Published is prepared.");

            board.Board.SearchText = string.Empty;
            board.Board.Render();
            Settle(board);
            Assert.IsNotEmpty(published.Cards, "The search cleared, Published has its cards.");
            Assert.IsFalse(preparer.IsPrepared, "Its cards changed: not prepared.");
            int stepsBefore = preparer.StepsRun;
            preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            List<HiddenShelfPreparer.Step> refresh = preparer.Steps.Skip(stepsBefore).ToList();
            string steps = string.Join(", ", refresh.Select(step => $"{step.Kind}{(step.Cap is null ? string.Empty : "c")} {step.ViewsMade} views {step.Time.TotalMilliseconds:0} ms"));
            Assert.IsNull(preparer.SkippedReason, $"Nothing stopped the refresh ({steps}).");
            Assert.IsTrue(preparer.IsPrepared, "Refreshed, Published is prepared.");
            Assert.AreEqual("R,Lc,Lc,Lc,L", string.Join(",", refresh.Select(step => step.Kind + (step.Cap is null ? string.Empty : "c"))), "The refresh: R, then the whole ladder.");
            foreach (HiddenShelfPreparer.Step step in preparer.Steps)
            {
                Assert.IsLessThanOrEqualTo(preparer.SlowSliceLimit, step.Time, $"No step over the limit ({steps}).");
            }

            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            Assert.AreEqual(twinMade, prepared, "Preparation made the twin's card views.");
            Assert.AreEqual(0, atSwitch, "The switch made no card view.");
            AssertDrawnRight(board, "after the refresh that followed the search");
            return new List<string> { $"search with no match, then cleared: refresh {steps}; preparation made {prepared} (twin {twinMade}), the switch {atSwitch}" };
        }));
    }

    // Published shorter than a screen at its preparation (2 cards), then
    // given the rest of its cards. The list was laid out for a whole screen
    // with 2 cards, so a full-size layout would now make the rest of the
    // screen in one step: at 7 ms a view (PerViewClock) that is over the
    // limit, so it is predicted before it runs and preparation stops
    // without running it. No step goes over the limit, and the switch makes
    // the rest, as on the base.
    [TestMethod]
    public void T2_AFullSizeRefreshPredictedOverTheLimitIsNotRun()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        List<Manuscript> arriving = disk.Manuscripts.Where(manuscript => manuscript is not null && manuscript.Location == ManuscriptLocation.Published).ToList();
        Write(Headless.Run(() =>
        {
            MemorySession twinSession = WithoutPublished(disk);
            twinSession.Library.AddRange(arriving);
            int twinMade = TwinFirstSwitchViews(twinSession);

            MemorySession session = WithoutPublished(disk);
            session.Library.AddRange(arriving.Take(2));
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = board.View.Preparer;
            preparer.Clock = PerViewClock;
            int start = CardView.MadeCount;
            preparer.RunToEnd();
            Assert.IsTrue(preparer.IsPrepared, "The short Published is prepared.");
            Assert.IsNull(preparer.SkippedReason, "Nothing stopped its preparation.");

            session.Library.AddRange(arriving.Skip(2));
            session.RaiseChanged();
            Settle(board);
            int stepsBefore = preparer.StepsRun;
            preparer.RunToEnd();
            int prepared = CardView.MadeCount - start;
            string steps = string.Join(", ", preparer.Steps.Select(step => $"{step.Kind}{(step.Cap is null ? string.Empty : "c")} {step.ViewsMade} views {step.Time.TotalMilliseconds:0} ms"));
            Assert.AreEqual(HiddenShelfPreparer.SlowSliceReason, preparer.SkippedReason, $"The full-size refresh was predicted over the limit ({steps}).");
            Assert.IsFalse(preparer.IsPrepared, "Stopped, Published is not prepared.");
            Assert.IsFalse(preparer.Steps.Skip(stepsBefore).Any(step => step.Kind == "L"), $"The full-size refresh did not run ({steps}).");
            foreach (HiddenShelfPreparer.Step step in preparer.Steps)
            {
                Assert.IsLessThanOrEqualTo(preparer.SlowSliceLimit, step.Time, $"No step over the limit ({steps}).");
            }

            int beforeSwitch = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - beforeSwitch;
            Assert.AreEqual(twinMade, prepared + atSwitch, "Preparation and the switch together made the twin's views, no more.");
            AssertDrawnRight(board, "after the stopped refresh");
            return new List<string> { $"2 cards, then {arriving.Count}: steps {steps}; stopped ({preparer.SkippedReason}); preparation made {prepared}, the switch {atSwitch} (twin {twinMade})" };
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

    // Published with no card: preparation gives it its width and does not
    // lay it out (there is nothing to make), the switch makes nothing, the
    // empty message shows, and the tree is the twin's.
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
            int steps = Still(board).RunToEnd();
            int prepared = CardView.MadeCount - start;
            Assert.IsTrue(board.View.Preparer.IsPrepared, "An empty Published is prepared.");
            Assert.AreEqual(1, steps, "Only the width step ran: an empty Published is not laid out.");
            Assert.AreEqual("W", board.View.Preparer.Steps[0].Kind, "The step was W.");
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
    // and it is no longer called prepared. An empty shelf was never laid out
    // (a layout would leave the list measured for a whole screen, which the
    // first cards would then fill in one step), so the idle refresh is R and
    // then the whole ladder, each layout step making no more than about half
    // the first screen, as in a first preparation (T2). Together they make
    // the views the twin's switch makes, the switch then makes none and
    // refills no row, and every view draws its own card.
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
            List<HiddenShelfPreparer.Step> refresh = preparer.Steps.Skip(stepsBefore).ToList();
            string kinds = string.Join(",", refresh.Select(step => step.Kind + (step.Cap is null ? string.Empty : "c")));
            Assert.AreEqual("R,Lc,Lc,Lc,L", kinds, "The refresh: the rows brought current, then the ladder's capped layout steps and the full-size one.");
            Assert.AreEqual(0, refresh[0].ViewsMade, "R made no card view: the list had made no row.");
            int half = (int)Math.Ceiling(twinMade / 2.0 / published.Columns) * published.Columns;
            foreach (HiddenShelfPreparer.Step step in refresh)
            {
                Assert.IsLessThanOrEqualTo(half, step.ViewsMade, $"A refresh step ({step.Kind}) made {step.ViewsMade} views, more than about half the first screen ({half}).");
            }

            Assert.IsTrue(preparer.IsPrepared, "Refreshed, Published is prepared again.");
            AssertOnlyShown(board, 0, "after the refresh");

            Assert.AreEqual(refreshed, AssertCardsLaidOut(board, "refreshed, before the switch"), "Every card view the refresh made is laid out, ready to show.");
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
    // and shows every card right. After the switch the accessibility tree
    // and Tab walk are those of a twin Board that had the same change and
    // was not prepared, so a screen reader lists the same cards, cache rows
    // included (after a resize the refresh climbs the ladder again, so the
    // list keeps the rows a switch at the new size makes); after a theme
    // change the switch also draws the twin's picture. The twin and each
    // mode have their own library: an edit changes a manuscript in place.
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
        using DiskLibrarySession halfDisk = OpenFiveHundred("five-hundred-half");
        using DiskLibrarySession twinDisk = OpenFiveHundred("five-hundred-twin");
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            byte[]? twinPixels = null;
            string twinTree;
            MemorySession twinSession = WholeLibrary(twinDisk);
            using (BoardWindow twin = BoardWindow.Show(twinSession, ThemeVariant.Light))
            {
                Change(twin, twinSession, change);
                Settle(twin);
                Switch(twin, 1);
                if (change == "theme")
                {
                    twinPixels = Frame(twin).Pixels;
                }

                twinTree = AccessibilityTreeTests.AccessibilityTree.Describe(twin, FiveHundredTabLimit);
            }

            foreach ((int first, DiskLibrarySession library) in new[] { (FirstPreparationSteps, disk), (3, halfDisk) })
            {
                string mode = first == FirstPreparationSteps ? "prepared" : "half prepared";
                MemorySession session = WholeLibrary(library);
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
                int laidOut = AssertCardsLaidOut(board, $"{change}, {mode}: refreshed, before the switch");
                Assert.AreEqual(published.Cards.Count > 0, laidOut > 0, $"{change}, {mode}: the refreshed shelf holds laid-out card views when it has cards ({laidOut}).");

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

                // Last: the Tab walk moves the keyboard.
                string preparedTree = AccessibilityTreeTests.AccessibilityTree.Describe(board, FiveHundredTabLimit);
                AssertSameTree(twinTree, preparedTree, $"{change}, {mode}: after the switch");
                facts.Add($"{change}, {mode}: after the switch the tree and Tab walk equal the twin's ({preparedTree.Split('\n').Length} lines)");
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

    // T7: an automation peer for Published's Border exists once a client has
    // read the shelf host's children, or asked a Pipeline card's peer for its
    // parent (which connects every ancestor's peer). Preparation then runs no
    // step, says why, and raises no ChildrenChanged on the host's peer. A
    // flip made by hand with the peer present does raise it: the check is
    // what keeps a step quiet.
    [TestMethod]
    [DataRow("host children")]
    [DataRow("card parent")]
    public void T7_APeerOnTheShelfHostStopsPreparation(string read)
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            Panel host = board.Find<Panel>("ShelfHost");
            Border border = (Border)(board.Shelf(PublishedName).Parent ?? throw new AssertFailedException("Published has no Border."));
            Assert.IsNull(ControlAutomationPeer.FromElement(border), "No peer for Published's Border before a client reads the Board.");
            if (read == "host children")
            {
                ControlAutomationPeer.CreatePeerForElement(host).GetChildren();
            }
            else
            {
                CardView card = board.Cards("PipelineShelf")[0];
                Assert.IsNotNull(ControlAutomationPeer.CreatePeerForElement(card).GetParent(), "The card's peer has a parent.");
            }

            AutomationPeer borderPeer = ControlAutomationPeer.FromElement(border) ?? throw new AssertFailedException($"After the {read} read, Published's Border has a peer.");
            AutomationPeer hostPeer = ControlAutomationPeer.CreatePeerForElement(host);
            int changes = 0;
            int borderChanges = 0;
            hostPeer.ChildrenChanged += (_, _) => changes++;
            borderPeer.PropertyChanged += (_, _) => borderChanges++;

            HiddenShelfPreparer preparer = Still(board);
            int start = CardView.MadeCount;
            int ran = preparer.RunToEnd();
            int made = CardView.MadeCount - start;
            Assert.AreEqual(0, ran, "No step ran.");
            Assert.AreEqual(0, made, "No card view was made.");
            Assert.AreEqual(HiddenShelfPreparer.AutomationPeerReason, preparer.SkippedReason, "Skipped for an automation peer.");
            Assert.AreEqual(0, changes, "No ChildrenChanged was raised.");
            Assert.AreEqual(0, borderChanges, "No PropertyChanged was raised on the Border's peer.");
            Assert.IsFalse(preparer.IsPrepared, "Not prepared.");
            Assert.IsFalse(board.Board.Shelves[1].IsRealized, "Published was not touched.");

            // The same Board with the timer's rules: still nothing.
            preparer.Interval = TimeSpan.FromHours(1);
            TimeSpan now = TimeSpan.FromSeconds(100);
            preparer.Clock = () => now;
            preparer.Enabled = true;
            DrawFrame();
            now += TimeSpan.FromSeconds(5);
            Assert.IsFalse(preparer.Tick(), "A tick runs no step either.");
            Assert.IsFalse(preparer.IsTimerRunning, "Skipped, the timer is stopped.");
            preparer.Enabled = false;

            border.SetCurrentValue(Visual.IsVisibleProperty, true);
            border.SetCurrentValue(Visual.IsVisibleProperty, false);
            int byHand = changes;
            Assert.IsGreaterThan(0, byHand, "A flip made by hand with the peer present raises ChildrenChanged.");
            Switch(board, 1);
            AssertOnlyShown(board, 1, "switched after the skip");
            return new List<string> { $"{read}: steps run {ran}, views made {made}, skipped reason {preparer.SkippedReason}, ChildrenChanged during RunToEnd 0; a flip by hand raised {byHand}" };
        }));
    }

    // T7: a client reads the shelves only after Published was prepared. The
    // next change makes a refresh due; it is skipped for the peer, raises
    // nothing on the host's or the Border's peer, and touches nothing: the
    // switch then brings the rows current, as on the base.
    [TestMethod]
    public void T7_APeerMadeAfterPreparationStopsTheNextRefresh()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            MemorySession session = WholeLibrary(disk);
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Still(board);
            preparer.RunToEnd();
            Assert.IsTrue(preparer.IsPrepared, "Published is prepared before any client reads the Board.");
            Panel host = board.Find<Panel>("ShelfHost");
            Border border = (Border)(board.Shelf(PublishedName).Parent ?? throw new AssertFailedException("Published has no Border."));
            Assert.IsNull(ControlAutomationPeer.FromElement(border), "No peer for Published's Border yet.");
            AutomationPeer hostPeer = ControlAutomationPeer.CreatePeerForElement(host);
            hostPeer.GetChildren();
            AutomationPeer borderPeer = ControlAutomationPeer.FromElement(border) ?? throw new AssertFailedException("Read, the shelf host's children include a peer for Published's Border.");

            ShelfViewModel published = board.Board.Shelves[1];
            board.Board.SearchText = published.Cards[40].Title;
            board.Board.Render();
            Settle(board);
            Assert.IsFalse(preparer.IsPrepared, "The search makes a refresh due.");
            var events = new List<string>();
            hostPeer.ChildrenChanged += (_, _) => events.Add("ChildrenChanged on the shelf host");
            hostPeer.PropertyChanged += (_, e) => events.Add($"PropertyChanged {e.Property} on the shelf host");
            borderPeer.ChildrenChanged += (_, _) => events.Add("ChildrenChanged on the Border");
            borderPeer.PropertyChanged += (_, e) => events.Add($"PropertyChanged {e.Property} on the Border");
            int steps = preparer.StepsRun;
            int start = CardView.MadeCount;
            int ran = preparer.RunToEnd();
            Assert.AreEqual(0, ran, "No refresh step ran.");
            Assert.AreEqual(steps, preparer.StepsRun, "No step was counted.");
            Assert.AreEqual(0, CardView.MadeCount - start, "No card view was made.");
            Assert.AreEqual(HiddenShelfPreparer.AutomationPeerReason, preparer.SkippedReason, "Skipped for the automation peer.");
            Assert.IsTrue(published.RowsAreStale, "The rows still wait for the switch: the refresh touched nothing.");
            DrawFrame();
            Settle(board);
            Assert.AreEqual(string.Empty, string.Join("; ", events), "No automation event was raised by the skipped refresh.");
            AssertOnlyShown(board, 0, "after the skipped refresh");

            Switch(board, 1);
            AssertOnlyShown(board, 1, "switched after the skipped refresh");
            AssertDrawnRight(board, "switched after the skipped refresh");
            return new List<string> { $"peer made after preparation: the refresh after a search ran {ran} steps, skipped reason {preparer.SkippedReason}, no event" };
        }));
    }

    // T7: a client that has read the Board from the window down to the shelf
    // host's own peer, but not the host's children, holds no peer for
    // Published's Border. Preparation runs, and no peer the client holds
    // raises ChildrenChanged or PropertyChanged during the steps or at the
    // next frame.
    [TestMethod]
    public void T7_AClientAboveTheShelfHostHearsNothing()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            Panel host = board.Find<Panel>("ShelfHost");
            Border border = (Border)(board.Shelf(PublishedName).Parent ?? throw new AssertFailedException("Published has no Border."));
            var peers = new List<AutomationPeer>();
            void Walk(AutomationPeer peer)
            {
                peers.Add(peer);
                if (peer is ControlAutomationPeer control && ReferenceEquals(control.Owner, host))
                {
                    return;
                }

                foreach (AutomationPeer child in peer.GetChildren())
                {
                    Walk(child);
                }
            }

            Walk(ControlAutomationPeer.CreatePeerForElement(board.Window));
            Assert.IsTrue(peers.Any(peer => peer is ControlAutomationPeer control && ReferenceEquals(control.Owner, host)), "The walk reached the shelf host's peer.");
            Assert.IsNull(ControlAutomationPeer.FromElement(border), "No peer for Published's Border.");
            var events = new List<string>();
            foreach (AutomationPeer peer in peers)
            {
                string who = ((peer as ControlAutomationPeer)?.Owner.GetType().Name ?? peer.GetType().Name) + "/" + peer.GetAutomationId();
                peer.ChildrenChanged += (_, _) => events.Add("ChildrenChanged " + who);
                peer.PropertyChanged += (_, e) => events.Add("PropertyChanged " + e.Property + " " + who);
            }

            HiddenShelfPreparer preparer = Still(board);
            int ran = preparer.RunToEnd();
            string during = string.Join("; ", events);
            events.Clear();
            DrawFrame();
            Settle(board);
            string after = string.Join("; ", events);
            Assert.IsNull(preparer.SkippedReason, "Not skipped.");
            Assert.IsTrue(preparer.IsPrepared, "Prepared.");
            Assert.IsNull(ControlAutomationPeer.FromElement(border), "Still no peer for Published's Border.");
            Assert.AreEqual(string.Empty, during, "No automation event while the steps ran.");
            Assert.AreEqual(string.Empty, after, "No automation event at the next frame.");
            return new List<string> { $"a client above the shelf host: {ran} steps, {peers.Count} peers watched, no event" };
        }));
    }

    // T7: a client reads the whole tree between two ladder steps: it lists
    // nothing of Published, the next step is skipped for the peer the read
    // made, and nothing is raised.
    [TestMethod]
    public void T7_AClientBetweenTwoStepsSeesNothingAndStopsTheRest()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Still(board);
            Assert.IsTrue(preparer.RunStep(), "W ran.");
            Assert.IsTrue(preparer.RunStep(), "The first layout step ran.");
            ShelfView published = board.Shelf(PublishedName);
            Assert.IsNotEmpty(published.GetVisualDescendants().OfType<CardView>().ToList(), "The first layout step made card views.");
            var peers = new List<AutomationPeer>();
            void Walk(AutomationPeer peer)
            {
                peers.Add(peer);
                foreach (AutomationPeer child in peer.GetChildren())
                {
                    Walk(child);
                }
            }

            Walk(ControlAutomationPeer.CreatePeerForElement(board.Window));
            int inside = peers.OfType<ControlAutomationPeer>().Count(peer => peer.Owner == published || peer.Owner.FindAncestorOfType<ShelfView>() == published);
            Assert.AreEqual(0, inside, "Nothing of Published is listed.");
            var events = new List<string>();
            foreach (AutomationPeer peer in peers)
            {
                peer.ChildrenChanged += (_, _) => events.Add("ChildrenChanged");
                peer.PropertyChanged += (_, e) => events.Add("PropertyChanged " + e.Property);
            }

            int ran = preparer.RunToEnd();
            Assert.AreEqual(0, ran, "No more steps.");
            Assert.AreEqual(HiddenShelfPreparer.AutomationPeerReason, preparer.SkippedReason, "Skipped for the peer the read made.");
            DrawFrame();
            Settle(board);
            Assert.AreEqual(string.Empty, string.Join("; ", events), "Nothing raised.");
            return new List<string> { $"a client between two steps: {peers.Count} peers, none of Published; skipped, nothing raised" };
        }));
    }

    // T8: the idle rules, with a test clock and the timer slowed so that
    // only the test's own Tick calls run steps.
    [TestMethod]
    public void T8_NothingRunsInStartupOrWhileBusy()
    {
        using DiskLibrarySession disk = OpenFiveHundred("five-hundred");
        MemorySession session = WholeLibrary(disk);
        Write(Headless.Run(() =>
        {
            var facts = new List<string>();
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = board.View.Preparer;
            TimeSpan now = TimeSpan.FromSeconds(100);
            preparer.Clock = () => now;
            preparer.Interval = TimeSpan.FromHours(1);
            Assert.IsFalse(preparer.IsTimerRunning, "Off: no timer.");
            preparer.Enabled = true;
            Assert.IsTrue(preparer.IsTimerRunning, "On: the timer waits for its tick.");

            // Startup: not before 0.4 s of quiet, nor before a frame.
            Assert.IsFalse(preparer.Tick(), "At once: no step.");
            now += TimeSpan.FromSeconds(1);
            Assert.IsFalse(preparer.Tick(), "No frame drawn since the Board rendered: no step.");
            DrawFrame();
            Assert.IsTrue(preparer.Tick(), "Quiet and a frame drawn: the first step.");
            Assert.AreEqual("W", preparer.Steps[^1].Kind, "The first step is W.");

            // Never two steps without a frame between them.
            Assert.IsFalse(preparer.Tick(), "No frame since the step: no step.");
            Assert.IsFalse(preparer.IsTimerRunning, "The timer waits for the frame.");
            DrawFrame();
            Assert.IsTrue(preparer.IsTimerRunning, "The frame starts the timer again.");

            // Keys closer than 0.4 s.
            for (int key = 0; key < 5; key++)
            {
                PressShift(board);
                now += TimeSpan.FromMilliseconds(300);
                Assert.IsFalse(preparer.Tick(), $"Key {key + 1}, 0.3 s ago: no step.");
            }

            now += TimeSpan.FromMilliseconds(100);
            Assert.IsTrue(preparer.Tick(), "0.4 s after the last key: a step.");
            DrawFrame();

            // A pointer button held for 1 s.
            Control title = board.Find<TextBlock>("PageTitle");
            Point at = title.TranslatePoint(new Point(4, 4), board.Window) ?? throw new AssertFailedException("The title is not in the window.");
            board.Window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            now += TimeSpan.FromSeconds(1);
            Assert.IsFalse(preparer.Tick(), "A pointer button held: no step.");
            board.Window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Assert.IsTrue(preparer.Tick(), "Released, 1 s after the press: a step.");
            DrawFrame();

            // The stage list, the sort list and a card menu open.
            now += TimeSpan.FromSeconds(1);
            board.Board.IsStageListOpen = true;
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(preparer.Tick(), "The stage list open: no step.");
            board.Board.IsStageListOpen = false;
            board.Board.IsSortListOpen = true;
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(preparer.Tick(), "The sort list open: no step.");
            board.Board.IsSortListOpen = false;
            Dispatcher.UIThread.RunJobs();
            CardView card = board.Cards("PipelineShelf")[0];
            card.MoreMenu.ShowAt(card.FindControl<Button>("More") ?? throw new AssertFailedException("The card has no More button."));
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(card.MoreMenu.IsOpen, "The card menu is open.");
            Assert.IsFalse(preparer.Tick(), "A card menu open: no step.");
            card.MoreMenu.Hide();
            Dispatcher.UIThread.RunJobs();
            now += TimeSpan.FromSeconds(1);
            Assert.IsTrue(preparer.Tick(), "Lists and menu closed: a step.");
            DrawFrame();

            // The Board hidden; the window minimized.
            board.View.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            now += TimeSpan.FromSeconds(1);
            Assert.IsFalse(preparer.Tick(), "The Board hidden: no step.");
            board.View.IsVisible = true;
            Settle(board);
            board.Window.WindowState = WindowState.Minimized;
            Dispatcher.UIThread.RunJobs();
            now += TimeSpan.FromSeconds(1);
            Assert.IsFalse(preparer.Tick(), "The window minimized: no step.");
            board.Window.WindowState = WindowState.Normal;
            Settle(board);
            now += TimeSpan.FromSeconds(1);
            Assert.IsTrue(preparer.Tick(), "Shown again: the last step.");
            DrawFrame();
            Assert.IsTrue(preparer.IsPrepared, "Published is prepared.");
            Assert.AreEqual(FirstPreparationSteps, preparer.StepsRun, "Five steps in all.");
            Assert.IsFalse(preparer.IsTimerRunning, "Prepared, the timer has stopped.");
            facts.Add($"first preparation by ticks: {string.Join(", ", preparer.Steps.Select(step => $"{step.Kind} {step.ViewsMade}"))}");

            // A search typed with pauses of 0.5 s: no refresh until 2 s after
            // the last render.
            ShelfViewModel published = board.Board.Shelves[1];
            List<string> words = published.Cards.Take(10).Select(cardModel => cardModel.Title).ToList();
            foreach (string word in words)
            {
                PressShift(board);
                board.Board.SearchText = word;
                board.Board.Render();
                Assert.IsTrue(preparer.IsTimerRunning, "The render starts the timer again.");
                now += TimeSpan.FromMilliseconds(500);
                DrawFrame();
                Assert.IsFalse(preparer.Tick(), $"0.5 s after the search \"{word}\": no refresh.");
            }

            Assert.AreEqual(FirstPreparationSteps, preparer.StepsRun, "No step during the search.");
            now += TimeSpan.FromMilliseconds(1500);
            int refresh = 0;
            while (preparer.Tick())
            {
                refresh++;
                DrawFrame();
            }

            Assert.IsTrue(preparer.IsPrepared, "2 s after the last render, the refresh prepared Published again.");
            facts.Add($"refresh after the search: {string.Join(", ", preparer.Steps.Skip(FirstPreparationSteps).Select(step => step.Kind))} ({refresh} steps, one per tick and frame)");
            return facts;
        }));
    }

    // T10: the idle timer, with the real clock, prepares Published by itself
    // and stops; the switch then makes no card view.
    [TestMethod]
    public void T10_TheTimerPathFinishesAndStops()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Busy(board);
            preparer.Enabled = true;
            Assert.IsTrue(Pump(() => preparer.IsPrepared, TimeSpan.FromSeconds(30)), "The timer prepared Published.");
            Assert.AreEqual(FirstPreparationSteps, preparer.StepsRun, "Five steps.");
            Assert.IsNull(preparer.SkippedReason, "Nothing stopped it.");
            Pump(() => false, TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(FirstPreparationSteps, preparer.StepsRun, "No step after it was prepared.");
            Assert.IsFalse(preparer.IsTimerRunning, "The timer stopped.");
            AssertOnlyShown(board, 0, "after the timer's preparation");
            int before = CardView.MadeCount;
            Switch(board, 1);
            int atSwitch = CardView.MadeCount - before;
            Assert.AreEqual(0, atSwitch, "The switch made no card view.");
            AssertDrawnRight(board, "after the timer's preparation");
            return new List<string> { $"timer: {string.Join(", ", preparer.Steps.Select(step => $"{step.Kind} {step.ViewsMade}"))}; the switch made {atSwitch}" };
        }));
    }

    // T10: the window closed after two steps: the timer stops, nothing more
    // runs and nothing throws.
    [TestMethod]
    public void T10_ClosingMidwayIsSafe()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Busy(board);
            preparer.Enabled = true;
            Assert.IsTrue(Pump(() => preparer.StepsRun >= 2, TimeSpan.FromSeconds(30)), "Two steps ran.");
            board.Window.Close();
            int steps = preparer.StepsRun;
            Pump(() => false, TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(steps, preparer.StepsRun, "No step after the window closed.");
            Assert.IsFalse(preparer.IsTimerRunning, "The timer stopped.");
            Assert.IsFalse(preparer.IsPrepared, "Not prepared.");
            return new List<string> { $"closed after {steps} steps; none after" };
        }));
    }

    // T10: Published shown and hidden again between two ticks: the timer
    // stops for good.
    [TestMethod]
    public void T10_AShowAndHideBetweenTwoTicksStopsItForGood()
    {
        using DiskLibrarySession session = OpenFiveHundred("five-hundred");
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HiddenShelfPreparer preparer = Busy(board);
            preparer.Enabled = true;
            Assert.IsTrue(Pump(() => preparer.StepsRun >= 1, TimeSpan.FromSeconds(30)), "A step ran.");
            int steps = preparer.StepsRun;
            board.Board.SelectedShelfIndex = 1;
            board.Board.SelectedShelfIndex = 0;
            Assert.IsFalse(preparer.IsTimerRunning, "The timer stopped.");
            Pump(() => false, TimeSpan.FromSeconds(1));
            Assert.AreEqual(steps, preparer.StepsRun, "No step after the show and hide.");
            Assert.IsFalse(preparer.IsTimerRunning, "The timer stays stopped.");
            Assert.IsFalse(preparer.IsPrepared, "Not called prepared.");
            AssertOnlyShown(board, 0, "after the show and hide");
            return new List<string> { $"shown and hidden after {steps} steps; none after" };
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

        // Half prepared (W and two capped layout steps): still the
        // checked-in tree.
        string half = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            HalfPrepare(board);
            return AccessibilityTreeTests.AccessibilityTree.Describe(board, limit);
        });
        AccessibilityTreeTests.Compare(expected, half);

        (string twinTree, string preparedTree, string halfTree) = Headless.Run(() =>
        {
            string twinTree;
            using (BoardWindow twin = BoardWindow.Show(session, ThemeVariant.Light))
            {
                Switch(twin, 1);
                twinTree = AccessibilityTreeTests.AccessibilityTree.Describe(twin, limit);
            }

            string preparedTree;
            using (BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light))
            {
                Still(board).RunToEnd();
                Assert.IsTrue(board.View.Preparer.IsPrepared, "Published is prepared.");
                Switch(board, 1);
                preparedTree = AccessibilityTreeTests.AccessibilityTree.Describe(board, limit);
            }

            using BoardWindow halfBoard = BoardWindow.Show(session, ThemeVariant.Light);
            HalfPrepare(halfBoard);
            Switch(halfBoard, 1);
            return (twinTree, preparedTree, AccessibilityTreeTests.AccessibilityTree.Describe(halfBoard, limit));
        });

        AssertSameTree(twinTree, preparedTree, "After the switch");
        AssertSameTree(twinTree, halfTree, "After the switch from half prepared");
        return [$"{expected}: equal with preparation done and half done; after the switch, prepared or half prepared, equal to the twin's ({preparedTree.Split('\n').Length} lines)"];
    }

    // W and the first two capped layout steps.
    private static void HalfPrepare(BoardWindow board)
    {
        HiddenShelfPreparer preparer = Still(board);
        for (int step = 0; step < 3; step++)
        {
            Assert.IsTrue(preparer.RunStep(), $"Half prepared: step {step + 1} ran.");
        }

        Assert.IsFalse(preparer.IsPrepared, "Half prepared: not yet prepared.");
    }

    // T6: the keyboard on the search box, a shelf tab, a Pipeline card's
    // title or View route (the first card's, or the last card's), or in the
    // open stage list, stays there through every step: no step moves it or
    // raises a focus event, the list stays open, and then six Tab and three
    // Shift+Tab presses reach what they reach on a twin Board that was not
    // prepared.
    [TestMethod]
    [DataRow("search")]
    [DataRow("tab")]
    [DataRow("title")]
    [DataRow("route")]
    [DataRow("last card")]
    [DataRow("stage list")]
    public void T6_FocusAndTheNextTabAreKeptThroughTheSteps(string where)
    {
        using DiskLibrarySession session = OpenExample("example");
        Write(Headless.Run(() =>
        {
            string twin = KeysAfterSteps(session, where, prepare: false);
            string prepared = KeysAfterSteps(session, where, prepare: true);
            Assert.AreEqual(twin, prepared, $"{where}: the keys after preparation reach what they reach on the twin.");
            return new List<string> { $"{where}: {prepared}" };
        }));
    }

    // Puts the keyboard where, prepares Published step by step (or not),
    // checks the keyboard stayed and no focus event was raised, then returns
    // where Tab and Shift+Tab take it.
    private static string KeysAfterSteps(ILibrarySession session, string where, bool prepare)
    {
        using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
        Control target;
        switch (where)
        {
            case "search":
                target = board.Find<TextBox>("SearchBox");
                break;
            case "tab":
                target = board.Tabs()[0];
                break;
            case "title":
                target = board.Cards("PipelineShelf")[0].FindControl<Button>("Title") ?? throw new AssertFailedException("The card has no Title.");
                break;
            case "route":
                target = board.Cards("PipelineShelf")[0].FindControl<Button>("ViewRoute") ?? throw new AssertFailedException("The card has no View route.");
                break;
            case "last card":
                target = board.Cards("PipelineShelf")[^1].FindControl<Button>("ViewRoute") ?? throw new AssertFailedException("The card has no View route.");
                break;
            case "stage list":
                board.Find<ComboBox>("StageFilter").Focus(NavigationMethod.Tab);
                board.Board.IsStageListOpen = true;
                Settle(board);
                target = board.Focused() as Control ?? throw new AssertFailedException("Nothing is focused in the open list.");
                break;
            default:
                throw new AssertFailedException($"No place called {where}.");
        }

        if (where != "stage list")
        {
            Assert.IsTrue(target.Focus(NavigationMethod.Tab), $"{where}: focused.");
            Settle(board);
        }

        Assert.AreSame(target, board.Focused(), $"{where}: the target holds the keyboard.");
        var focusEvents = new List<string>();
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> onFocus = (_, e) => focusEvents.Add(e.RoutedEvent!.Name + " " + e.Source);
        board.Window.AddHandler(InputElement.GotFocusEvent, onFocus, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        board.Window.AddHandler(InputElement.LostFocusEvent, onFocus, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        if (prepare)
        {
            HiddenShelfPreparer preparer = Still(board);
            int steps = 0;
            while (preparer.RunStep())
            {
                steps++;
                Assert.AreSame(target, board.Focused(), $"{where}: step {steps} left the keyboard where it was.");
                Assert.IsTrue(target.IsFocused, $"{where}: step {steps}: the target is still focused.");
            }

            Assert.IsTrue(preparer.IsPrepared, $"{where}: prepared ({preparer.SkippedReason}).");
            DrawFrame();
            Settle(board);
        }

        Assert.AreSame(target, board.Focused(), $"{where}: the keyboard is where it was.");
        Assert.AreEqual(string.Empty, string.Join("; ", focusEvents), $"{where}: no focus event.");
        if (where == "stage list")
        {
            Assert.IsTrue(board.Board.IsStageListOpen, "The stage list is still open.");
            return "stage list kept open and focused";
        }

        var reached = new List<string>();
        for (int press = 0; press < 6; press++)
        {
            board.Press(Key.Tab);
            reached.Add(DescribeFocus(board.Focused()));
        }

        for (int press = 0; press < 3; press++)
        {
            board.Press(Key.Tab, RawInputModifiers.Shift);
            reached.Add("Shift+Tab: " + DescribeFocus(board.Focused()));
        }

        return string.Join(" > ", reached);
    }

    private static string DescribeFocus(object? focused) =>
        focused is Control control
            ? $"{control.GetType().Name}[{control.Name}|{Avalonia.Automation.AutomationProperties.GetAutomationId(control)}|{(control.DataContext as CardViewModel)?.Title}|{control.FindAncestorOfType<ShelfView>()?.Name}]"
            : "(none)";

    // A prepared Board's tree and Tab walk (AccessibilityTree.Describe)
    // equal a twin's, or the first line that differs is named.
    private static void AssertSameTree(string twinTree, string preparedTree, string step)
    {
        if (twinTree == preparedTree)
        {
            return;
        }

        string[] want = twinTree.Split('\n');
        string[] got = preparedTree.Split('\n');
        int line = 0;
        while (line < want.Length && line < got.Length && want[line] == got[line])
        {
            line++;
        }

        Assert.Fail($"{step}, the prepared Board's tree differs from the twin's at line {line + 1} (twin {want.Length} lines, prepared {got.Length}):\n" +
            $"  twin:     {(line < want.Length ? want[line] : "(end)")}\n" +
            $"  prepared: {(line < got.Length ? got[line] : "(end)")}");
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

    // Every card view the switch will show (shown, in a row view the list
    // shows) is measured and arranged before the switch, so the switch has
    // no layout of a card left to do: CardView.MadeCount alone would not see
    // prepared views left waiting for a layout. Returns how many were
    // checked. Public properties only; load-free.
    private static int AssertCardsLaidOut(BoardWindow board, string step)
    {
        int laidOut = 0;
        foreach (CardRowView row in board.Shelf(PublishedName).GetVisualDescendants().OfType<CardRowView>().Where(row => row.IsVisible && row.Row is not null))
        {
            foreach (CardView view in row.Children.OfType<CardView>().Where(view => view.IsVisible))
            {
                Assert.IsTrue(view.IsMeasureValid && view.IsArrangeValid,
                    $"{step}: the card view of \"{(view.DataContext as CardViewModel)?.Title}\" waits for a layout (measure valid {view.IsMeasureValid}, arrange valid {view.IsArrangeValid}).");
                laidOut++;
            }
        }

        return laidOut;
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

    // A load-free model of a step's time: the clock moves PerViewMs for
    // every card view made, so a step takes PerViewMs for each view it made.
    private static TimeSpan PerViewClock() => TimeSpan.FromMilliseconds(PerViewMs * (double)CardView.MadeCount);

    // A frame drawn, as the render timer would draw it.
    private static void DrawFrame()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    // A key that changes nothing on the Board.
    private static void PressShift(BoardWindow board)
    {
        board.Window.KeyPress(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        board.Window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        Dispatcher.UIThread.RunJobs();
    }

    // The Board's preparer on the real clock, with the stop threshold raised
    // so that a busy PC does not stop the timer tests early.
    private static HiddenShelfPreparer Busy(BoardWindow board)
    {
        HiddenShelfPreparer preparer = board.View.Preparer;
        preparer.SlowSliceLimit = TimeSpan.FromHours(1);
        return preparer;
    }

    // Runs the interface thread, drawing frames, until the condition holds
    // or the limit passes; returns whether it held.
    private static bool Pump(Func<bool> condition, TimeSpan limit) =>
        BoardWindow.WaitFor(() =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            return condition();
        }, limit);

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
