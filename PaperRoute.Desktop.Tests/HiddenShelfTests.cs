using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// A shelf shown once and hidden again does not refill its rows while hidden
// (ShelfViewModel.IsShown): a search, a filter or a library change changes
// its cards, its tab and its empty message at once, and its card views get
// their cards as it is shown again, before it is laid out or drawn. These
// tests change the Board while Published is hidden, show it again by each
// way there is (its tab clicked, the arrow key on the tab strip), and check:
// - before the switch, every shelf's tab text, drawn tab and empty message
//   are those of a Board that never hid anything (a fresh Board over the same
//   manuscripts with the same filters);
// - as the shelf becomes visible, its rows and every row view already hold
//   its cards in rows of its columns (so no frame can show an old card);
// - after the switch, every card view laid out on the shelf draws the card
//   at its place (CardParts.DrawnWrong), found by walking the visual tree,
//   not by the shelf's own look-ups.
// Under the every-card build (ShelfLayout option (a)) there are no rows, and
// no shelf ever has stale rows.
[TestClass]
public sealed class HiddenShelfTests : StorageCheckedTests
{
    private const string PublishedName = "PublishedShelf";

    private static readonly string[] s_shelfNames = ["PipelineShelf", PublishedName, "FileDrawerShelf"];

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialHidden-" + Guid.NewGuid().ToString("N"));
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
    public void ASearchTypedWhilePublishedIsHiddenIsDrawnRightWhenItsTabIsClickedOrReachedByArrow()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            Check(board, session, "Published first shown", facts);
            string word = published.Cards[40].Title;
            ClickTab(board, 0);

            board.Board.SearchText = word;
            board.Board.Render();
            Settle(board);
            if (ShelfLayout.UsesRows)
            {
                Assert.IsTrue(published.RowsAreStale, "The hidden shelf's rows wait for it to be shown.");
            }

            CheckBeforeSwitch(board, session, "a search while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("a search while hidden, its tab clicked");
            Check(board, session, "a search while hidden, its tab clicked", facts);

            // The same reached with the Right arrow key on the tab strip.
            ClickTab(board, 0);
            board.Board.SearchText = published.Cards[^1].Title;
            board.Board.Render();
            Settle(board);
            CheckBeforeSwitch(board, session, "another search while hidden");
            TabStripItem pipelineTab = board.Tabs()[0];
            pipelineTab.Focus(NavigationMethod.Tab);
            board.Press(Key.Right);
            Settle(board);
            Assert.AreEqual(1, board.Board.SelectedShelfIndex, "Right on the Pipeline tab shows Published.");
            watch.AssertCurrent("another search while hidden, by the Right arrow");
            Check(board, session, "another search while hidden, by the Right arrow", facts);

            // K8 after such a switch: Right and Down go by place in the rows.
            board.Board.SearchText = string.Empty;
            board.Board.Render();
            Settle(board);
            ShelfView shelf = board.Shelf(PublishedName);
            Assert.IsTrue(shelf.FocusFirstCard(), "The first card takes the keyboard.");
            board.Press(Key.Right);
            Assert.AreEqual(published.Cards[1].AutomationKey + ".Title", CardParts.Id(board.Focused() as Control), "Right goes to the second card.");
            board.Press(Key.Down);
            int below = 1 + published.Columns;
            Assert.AreEqual(published.Cards[below].AutomationKey + ".Title", CardParts.Id(board.Focused() as Control), "Down goes to the card below.");
            Assert.IsNull(CardParts.DrawnWrong(shelf.FindCardView(below)!, published.Cards[below]), "The focused view draws its own card.");
            return facts;
        }));
    }

    [TestMethod]
    public void TheStageTheSortAndClearChangedWhilePublishedIsHiddenAreDrawnRightWhenShown()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];

            // Published first shown in the third sort, so its row views last
            // held its cards in that order.
            board.Board.SelectedSortIndex = 2;
            Settle(board);
            ClickTab(board, 1);
            List<string> shownBefore = BoardHarness.Titles(published);
            ClickTab(board, 0);

            // Every Published manuscript of the test library is at the
            // Published stage, so a stage either keeps all its cards or none.
            // First a stage that empties it (and the first sort put back),
            // shown; then, hidden again, the Published stage, which gives it
            // its cards back in the first sort's order: its reused rows and
            // row views are refilled with other manuscripts at their places.
            board.Board.SelectedStageIndex = 3;
            board.Board.SelectedSortIndex = 0;
            Settle(board);
            Assert.HasCount(0, published.Cards, "The Submitted stage leaves Published empty.");
            CheckBeforeSwitch(board, session, "the stage changed while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("the stage changed while hidden");
            Check(board, session, "the stage changed while hidden", facts);

            ClickTab(board, 0);
            int publishedStage = board.Board.StageOptions.ToList().FindIndex(option => option.Stage == PaperStage.Published);
            Assert.IsGreaterThan(0, publishedStage, "The stage box has the Published stage.");
            board.Board.SelectedStageIndex = publishedStage;
            Settle(board);
            List<string> refilled = BoardHarness.Titles(published);
            Assert.IsNotEmpty(refilled, "The Published stage gives the hidden shelf its cards back.");
            Assert.AreNotEqual(shownBefore[0], refilled[0], "The first place holds another manuscript than when the shelf's views were last filled.");
            if (ShelfLayout.UsesRows)
            {
                Assert.IsTrue(published.RowsAreStale, "The stage change leaves the hidden shelf's rows waiting.");
            }

            CheckBeforeSwitch(board, session, "the stage changed again while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("the stage changed again while hidden");
            Check(board, session, "the stage changed again while hidden", facts);
            facts.Add($"{refilled.Count} cards after the stage change, first \"{refilled[0]}\" (was \"{shownBefore[0]}\")");

            ClickTab(board, 0);
            board.Board.SelectedStageIndex = 0;
            board.Board.SelectedSortIndex = 1;
            Settle(board);
            CheckBeforeSwitch(board, session, "the sort changed while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("the sort changed while hidden");
            Check(board, session, "the sort changed while hidden", facts);

            ClickTab(board, 0);
            board.Board.SearchText = "teaching";
            board.Board.Render();
            Settle(board);
            Button clear = board.Find<Button>("ClearFilters");
            Assert.IsTrue(clear.IsEffectivelyEnabled, "Clear is on with a search and a sort.");
            board.ClickAt(clear, new Point(clear.Bounds.Width / 2, clear.Bounds.Height / 2));
            Settle(board);
            Assert.AreEqual(string.Empty, board.Board.SearchText, "Clear empties the search.");
            Assert.AreEqual(0, board.Board.SelectedSortIndex, "Clear puts the first sort back.");
            CheckBeforeSwitch(board, session, "Clear pressed while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("Clear pressed while hidden");
            Check(board, session, "Clear pressed while hidden", facts);
            return facts;
        }));
    }

    [TestMethod]
    public void ASearchThatEmptiesTheHiddenShelfShowsTheEmptyMessageAndItsClearingTheCards()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            ClickTab(board, 0);

            board.Board.SearchText = "zzqx no manuscript holds this";
            board.Board.Render();
            Settle(board);
            Assert.HasCount(0, published.Cards, "The search leaves Published empty.");
            CheckBeforeSwitch(board, session, "an emptying search while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("an emptying search while hidden");
            Check(board, session, "an emptying search while hidden", facts);
            Assert.IsTrue(board.EmptyMessage(PublishedName).IsEffectivelyVisible, "The empty message is shown.");
            Assert.IsEmpty(board.Cards(PublishedName), "No card is drawn.");

            board.Board.SearchText = string.Empty;
            board.Board.Render();
            Settle(board);
            Check(board, session, "the search cleared while shown", facts);
            Assert.IsFalse(board.EmptyMessage(PublishedName).IsEffectivelyVisible, "The empty message goes.");
            return facts;
        }));
    }

    [TestMethod]
    public void SwitchingAwayAndBackTwiceWithAChangeEachTimeDrawsRight()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            string[] searches = [published.Cards[7].Title, "teaching", string.Empty];
            for (int round = 0; round < 2; round++)
            {
                ClickTab(board, 0);
                board.Board.SearchText = searches[round];
                board.Board.Render();
                board.Board.SelectedSortIndex = round + 1;
                Settle(board);
                CheckBeforeSwitch(board, session, $"round {round + 1} while hidden");
                ClickTab(board, 1);
                watch.AssertCurrent($"round {round + 1}");
                Check(board, session, $"round {round + 1}", facts);
            }

            // Away and back with no layout between, then a change on the shown shelf.
            board.Board.SelectedShelfIndex = 0;
            board.Board.SearchText = searches[2];
            board.Board.Render();
            board.Board.SelectedShelfIndex = 1;
            Settle(board);
            Check(board, session, "away and back at once", facts);
            return facts;
        }));
    }

    [TestMethod]
    public void AThemeChangeWhilePublishedIsHiddenThenShownDrawsRight()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ClickTab(board, 1);
            ClickTab(board, 0);
            board.Board.SearchText = "teaching";
            board.Board.Render();
            Settle(board);
            board.Window.RequestedThemeVariant = ThemeVariant.Dark;
            Settle(board);
            CheckBeforeSwitch(board, session, "a theme change while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("a theme change while hidden");
            Assert.AreEqual(ThemeVariant.Dark, board.Shelf(PublishedName).ActualThemeVariant, "Published is drawn dark.");
            Check(board, session, "a theme change while hidden", facts);
            return facts;
        }));
    }

    [TestMethod]
    public void TheWindowMadeNarrowerWhilePublishedIsHiddenRewrapsItsRowsWhenShown()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1, checkColumns: false);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            int wide = published.Columns;
            ClickTab(board, 0);
            board.Board.SearchText = "teaching";
            board.Board.Render();
            board.Window.Width = 760;
            Settle(board);
            Assert.AreNotEqual(wide, board.Board.Shelves[0].Columns, "The narrower window has fewer columns on the Pipeline.");
            CheckBeforeSwitch(board, session, "the window narrower while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("the window narrower while hidden");
            Assert.AreNotEqual(wide, published.Columns, "Published has the narrower window's columns once shown.");
            Check(board, session, "the window narrower while hidden", facts);
            facts.Add($"columns {wide} wide, {published.Columns} narrow");
            return facts;
        }));
    }

    // Records where the keyboard is after: a card of Published has it,
    // Published is hidden, a search takes that card out, Published is shown
    // again. At f51b5e2 (before the hidden shelf waited for its rows) and
    // after, the keyboard is on the Pipeline tab ("Shelf.Pipeline") once
    // Published is hidden and on the Published tab ("Shelf.Published") once
    // its tab is clicked: neither keeps it on a card.
    [TestMethod]
    public void TheKeyboardOnACardOfAShelfThenHiddenAndThatCardSearchedOutGoesWhereItWent()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        string record = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            ShelfView shelf = board.Shelf(PublishedName);
            Assert.IsTrue(shelf.FocusFirstCard(), "The first card takes the keyboard.");
            CardViewModel focused = published.Cards[0];
            facts.Add("on the card: " + Where(board));
            ClickTab(board, 0);
            facts.Add("Published hidden: " + Where(board));
            board.Board.SearchText = published.Cards[5].Title;
            board.Board.Render();
            Settle(board);
            Assert.IsFalse(published.Cards.Contains(focused), "The search takes the focused card out.");
            facts.Add("card searched out: " + Where(board));
            CheckBeforeSwitch(board, session, "the focused card searched out while hidden");
            ClickTab(board, 1);
            BoardWindow.WaitFor(() => false, TimeSpan.FromMilliseconds(100));
            Settle(board);
            facts.Add("Published shown: " + Where(board));
            Check(board, session, "the focused card searched out while hidden", []);
            return string.Join("; ", facts);
        });

        TestContext.WriteLine(record);
        Assert.AreEqual(
            "on the card: card part; Published hidden: Shelf.Pipeline; card searched out: Shelf.Pipeline; Published shown: Shelf.Published",
            record,
            "Where the keyboard goes is as it was at f51b5e2.");
    }

    [TestMethod]
    public void TheLibraryChangingWhilePublishedIsHiddenIsDrawnRightWhenShown()
    {
        using DiskLibrarySession disk = OpenFiveHundred();
        var session = new MemorySession(disk.Manuscripts.Where(manuscript => manuscript is not null), LargeLibraryFixture.Today, disk.Settings, disk.AuthorLibrary);
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 1);
            var facts = new List<string>();
            ShelfViewModel published = board.Board.Shelves[1];
            ClickTab(board, 1);
            HashSet<string> gone = [published.Cards[0].Title, published.Cards[3].Title];
            ClickTab(board, 0);

            session.Library.RemoveAll(manuscript => manuscript.Location == ManuscriptLocation.Published && gone.Contains(manuscript.Title));
            session.RaiseChanged();
            Settle(board);
            Assert.IsFalse(published.Cards.Any(card => gone.Contains(card.Title)), "The hidden shelf's cards change at once.");
            CheckBeforeSwitch(board, session, "the library changed while hidden");
            ClickTab(board, 1);
            watch.AssertCurrent("the library changed while hidden");
            Check(board, session, "the library changed while hidden", facts);
            return facts;
        }));
    }

    // A shelf never shown keeps its rows current at every render, as at
    // f51b5e2: nothing waits, nothing is made, and it is drawn right when
    // first shown.
    [TestMethod]
    public void AShelfNeverShownKeepsItsRowsCurrentAndMakesNothing()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        Write(Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            using var watch = new ShownWatch(board, 2);
            var facts = new List<string>();
            ShelfViewModel drawer = board.Board.Shelves[2];
            board.Board.SelectedSortIndex = 1;
            board.Board.SearchText = "a";
            board.Board.Render();
            Settle(board);
            Assert.IsFalse(drawer.IsRealized, "The File Drawer was never shown.");
            Assert.IsFalse(drawer.RowsAreStale, "A shelf never shown has no stale rows.");
            Assert.IsTrue(RowsMatch(drawer), "A shelf never shown keeps its rows current.");
            Assert.IsEmpty(board.Cards("FileDrawerShelf"), "A shelf never shown makes no card view.");
            CheckBeforeSwitch(board, session, "File Drawer never shown");
            ClickTab(board, 2);
            watch.AssertCurrent("File Drawer first shown");
            Check(board, session, "File Drawer first shown", facts, 2);
            return facts;
        }));
    }

    private void Write(List<string> facts)
    {
        foreach (string fact in facts)
        {
            TestContext.WriteLine(fact);
        }
    }

    // The keyboard's place: a card part's id, a tab's automation id, or the
    // control's type; "nothing" when no control has it.
    private static string Where(BoardWindow board)
    {
        Dispatcher.UIThread.RunJobs();
        if (board.Focused() is not Control control)
        {
            return "nothing";
        }

        if (control.FindAncestorOfType<CardView>(includeSelf: true) is not null)
        {
            return "card " + (CardParts.Id(control) is { Length: > 0 } id ? "part" : control.GetType().Name);
        }

        string automation = Avalonia.Automation.AutomationProperties.GetAutomationId(control) ?? string.Empty;
        return automation.Length > 0 ? automation : control.GetType().Name;
    }

    private static void ClickTab(BoardWindow board, int index)
    {
        TabStripItem tab = board.Tabs()[index];
        board.ClickAt(tab, new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2));
        Settle(board);
        Assert.AreEqual(index, board.Board.SelectedShelfIndex, $"The tab {index} is chosen.");
    }

    private static void Settle(BoardWindow board)
    {
        Dispatcher.UIThread.RunJobs();
        board.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    // The rows hold the shelf's cards in rows of its columns (option (b));
    // true under option (a), which has no rows.
    private static bool RowsMatch(ShelfViewModel model)
    {
        if (!ShelfLayout.UsesRows)
        {
            return model.Rows.Count == 0;
        }

        int columns = Math.Max(1, model.Columns);
        int rows = (model.Cards.Count + columns - 1) / columns;
        if (model.Rows.Count != rows)
        {
            return false;
        }

        for (int row = 0; row < rows; row++)
        {
            List<CardViewModel> expected = model.Cards.Skip(row * columns).Take(columns).ToList();
            IReadOnlyList<CardViewModel> held = model.Rows[row].Cards;
            if (held.Count != expected.Count || expected.Where((card, offset) => !ReferenceEquals(card, held[offset])).Any())
            {
                return false;
            }
        }

        return true;
    }

    // Before a switch: each shelf's tab text and count, its drawn tab, its
    // cards and its empty message are those of a fresh Board over the same
    // manuscripts with the same filters; under option (a) no shelf has stale rows.
    private static void CheckBeforeSwitch(BoardWindow board, ILibrarySession session, string step)
    {
        Assert.AreEqual(AttentionFilter.None, board.Board.ActiveFilter, $"{step}: no chip is on.");
        var reference = new BoardHarness(session.Manuscripts.Where(manuscript => manuscript is not null), session.Settings, session.AuthorLibrary, LargeLibraryFixture.Today);
        reference.Board.SearchText = board.Board.SearchText;
        reference.Board.SelectedStageIndex = board.Board.SelectedStageIndex;
        reference.Board.SelectedSortIndex = board.Board.SelectedSortIndex;
        reference.Debouncer.Stop();
        reference.Board.Render();
        IReadOnlyList<TabStripItem> tabs = board.Tabs();
        for (int index = 0; index < board.Board.Shelves.Count; index++)
        {
            ShelfViewModel live = board.Board.Shelves[index];
            ShelfViewModel expected = reference.Board.Shelves[index];
            Assert.AreEqual(expected.TabText, live.TabText, $"{step}: the tab text of {live.Name}.");
            Assert.AreEqual(expected.TabCount, live.TabCount, $"{step}: the tab count of {live.Name}.");
            Assert.AreEqual(live.Name + " " + live.TabCount, BoardWindow.ShownText(tabs[index]), $"{step}: the tab of {live.Name} as drawn.");
            Assert.AreEqual(expected.EmptyMessage, live.EmptyMessage, $"{step}: the empty message of {live.Name}.");
            CollectionAssert.AreEqual(BoardHarness.Titles(expected), BoardHarness.Titles(live), $"{step}: the cards of {live.Name}.");
            TextBlock message = board.EmptyMessage(s_shelfNames[index]);
            Assert.AreEqual(expected.EmptyMessage is not null, message.IsVisible, $"{step}: the empty message of {live.Name} is set to show.");
            if (expected.EmptyMessage is not null)
            {
                Assert.AreEqual(expected.EmptyMessage, message.Text, $"{step}: the empty message of {live.Name} as set.");
            }

            if (!ShelfLayout.UsesRows)
            {
                Assert.IsFalse(live.RowsAreStale, $"{step}: no rows wait under option (a).");
            }
        }
    }

    // After a switch: the shown shelf has no stale rows, its rows hold its
    // cards, every card view laid out on it draws the card at its place, the
    // first card is drawn when there is one, and the tabs and empty messages
    // are as a fresh Board's.
    private static void Check(BoardWindow board, ILibrarySession session, string step, List<string> facts, int index = 1)
    {
        CheckBeforeSwitch(board, session, step + " (after)");
        ShelfView shelf = board.Shelf(s_shelfNames[index]);
        ShelfViewModel model = board.Board.Shelves[index];
        Assert.IsTrue(shelf.IsEffectivelyVisible, $"{step}: the shelf is shown.");
        Assert.IsFalse(model.RowsAreStale, $"{step}: the shown shelf's rows are current.");
        Assert.IsTrue(RowsMatch(model), $"{step}: the rows hold the cards in rows of {model.Columns}.");
        int drawn = 0;
        foreach (CardView view in shelf.GetVisualDescendants().OfType<CardView>().Where(view => view.IsEffectivelyVisible))
        {
            CardViewModel card;
            if (ShelfLayout.UsesRows)
            {
                CardRowView row = view.FindAncestorOfType<CardRowView>() ?? throw new AssertFailedException($"{step}: a card view outside a row.");
                Assert.IsNotNull(row.Row, $"{step}: a laid-out card view in a row kept for reuse.");
                int place = (row.Row.Index * Math.Max(1, model.Columns)) + row.Children.IndexOf(view);
                Assert.IsLessThan(model.Cards.Count, place, $"{step}: a card view at place {place} past the shelf's cards.");
                card = model.Cards[place];
            }
            else
            {
                card = view.DataContext as CardViewModel ?? throw new AssertFailedException($"{step}: a card view with no card.");
                Assert.Contains(card, model.Cards, $"{step}: a card view shows a card the shelf does not.");
            }

            Assert.IsNull(CardParts.DrawnWrong(view, card), $"{step}: the view of \"{card.Title}\" {CardParts.DrawnWrong(view, card)}.");
            drawn++;
        }

        Assert.AreEqual(model.Cards.Count > 0, drawn > 0, $"{step}: cards are drawn when the shelf has some ({drawn} of {model.Cards.Count}).");
        if (model.Cards.Count > 0)
        {
            Assert.IsNotNull(shelf.FindCardView(0), $"{step}: the first card is drawn.");
        }

        Assert.AreEqual(model.Cards.Count == 0, board.EmptyMessage(s_shelfNames[index]).IsEffectivelyVisible, $"{step}: the empty message shows only when no card does.");
        facts.Add($"{step}: {model.Cards.Count} cards, {drawn} drawn, tab \"{model.TabText}\"");
    }

    private DiskLibrarySession OpenFiveHundred()
    {
        LargeLibraryFixture.Create(_root, 500);
        var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            Path.Combine(_root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(500, session.Manuscripts);
        return session;
    }

    // Watches a shelf's host become visible and checks, at that moment, before
    // any layout or frame, that the shelf's rows and every row view of it
    // already hold its cards.
    private sealed class ShownWatch : IDisposable
    {
        private readonly BoardWindow _board;
        private readonly int _index;
        private readonly bool _checkColumns;
        private readonly Control _host;
        private readonly List<string> _seen = [];

        public ShownWatch(BoardWindow board, int index, bool checkColumns = true)
        {
            _board = board;
            _index = index;
            _checkColumns = checkColumns;
            _host = (Control)(board.Shelf(s_shelfNames[index]).GetVisualParent() ?? throw new AssertFailedException("The shelf has no host."));
            _host.PropertyChanged += OnHostChanged;
        }

        public void AssertCurrent(string step)
        {
            Assert.IsNotEmpty(_seen, $"{step}: the shelf was made visible.");
            Assert.AreEqual(string.Empty, string.Join(", ", _seen.Where(seen => seen != "current")), $"{step}: as the shelf became visible, its rows and row views held its cards.");
            _seen.Clear();
        }

        public void Dispose() => _host.PropertyChanged -= OnHostChanged;

        private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Visual.IsVisibleProperty || e.NewValue is not true)
            {
                return;
            }

            ShelfViewModel model = _board.Board.Shelves[_index];
            if (!ShelfLayout.UsesRows || !model.IsRealized)
            {
                _seen.Add("current");
                return;
            }

            // The window narrower while hidden: the rows are current in the
            // old columns until the shelf is laid out at its new width, as
            // at f51b5e2; then they are rewrapped before the frame.
            bool rows = model.RowsAreStale is false && (!_checkColumns ? true : RowsMatch(model));
            ShelfView shelf = _board.Shelf(s_shelfNames[_index]);
            bool views = (shelf.FindControl<CardRowList>("Rows")?.ItemsPanelRoot?.Children ?? [])
                .OfType<CardRowView>()
                .Where(view => view.IsVisible && view.Row is not null)
                .All(view => view.Row!.Cards.Select((card, slot) => view.CardAt(slot) is { } made && ReferenceEquals(made.DataContext, card)).All(same => same));
            _seen.Add(rows && views ? "current" : $"stale (rows {rows}, views {views})");
        }
    }
}
