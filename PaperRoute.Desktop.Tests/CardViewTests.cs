using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The cards and the shelves (d1 8, step 9): the card template, the shelves as
// option (a) of d1 4.4 (a wrap panel with a computed item width, each shelf's
// cards made when the shelf is first shown), the tag strip, the route dots,
// the ⋯ menu of disabled items, the hover and focus border, and the cards'
// commands. The tests are the ones step 9 names in d1 6.2: K1, K2, K4, H2, H6,
// the headless part of V17, V22 and B6; then the example's seven cards as r1
// 1.7 works them out, the columns, the pictures, and a first reading of the
// 500 library's times (no assertion on time: step 12 sets budgets).
//
// Libraries are the example written to a temporary folder of the test's own,
// the 500 library (LargeLibraryFixture) in the same way, or libraries held in
// memory; all under the assembly's storage guard.
[TestClass]
public sealed class CardViewTests : StorageCheckedTests
{
    private const string Retrieval = "Example: retrieval practice in an introductory statistics course";
    private const string Sleep = "Example: sleep and memory consolidation in older adults";
    private const string Invariance = "Example: measurement invariance of a short grit scale across four countries";
    private const string OpenMaterials = "Example: open materials in developmental science, a survey";
    private const string Pilot = "Example: pilot notes on reading fluency";
    private const string Anchoring = "Example: anchoring effects in clinical risk estimates, a preregistered replication";
    private const string NullResult = "Example: a null result on priming and choice";

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialCards-" + Guid.NewGuid().ToString("N"));
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

    // ---- Keyboard ----

    // K1. Ports KeyboardAccessTests.vb:538 (order and names) over a 60-card
    // shelf: from the sort box, Tab reaches each card in shelf order, and in
    // each its title, then ⋯, then View route, nothing else (the pill, the
    // texts, the tag strip and the dots are not tab stops; Clear is off). The
    // title is named by its manuscript, ⋯ "More actions for <title>", the link
    // "View route for <title>".
    [TestMethod]
    public void K1_TitlesAreTabStopsInCardOrderEachJustBeforeItsMoreButton()
    {
        var session = new MemorySession(Sixty(), Papers.Today);

        (List<string> stops, List<string> expected, List<string> names, List<string> expectedNames, int views) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            List<CardViewModel> cards = board.Board.Shelves[0].Cards.ToList();
            var expected = cards.SelectMany(card => new[] { card.AutomationKey + ".Title", card.AutomationKey + ".More", card.AutomationKey + ".Route" }).ToList();
            var expectedNames = cards.SelectMany(card => new[] { card.Title, "More actions for " + card.Title, "View route for " + card.Title }).ToList();

            board.Find<ComboBox>("BoardSort").Focus(NavigationMethod.Tab);
            var stops = new List<string>();
            var names = new List<string>();
            for (int index = 0; index < expected.Count; index++)
            {
                board.Press(Key.Tab);
                var focused = board.Focused() as Control;
                stops.Add(CardParts.Id(focused));
                names.Add(focused is null ? string.Empty : AutomationProperties.GetName(focused) ?? string.Empty);
            }

            return (stops, expected, names, expectedNames, board.Cards("PipelineShelf").Count);
        });

        Assert.AreEqual(60, views, "The Pipeline shelf made a card view for each of its 60 cards.");
        CollectionAssert.AreEqual(expected, stops, "Tab went to: " + string.Join(" > ", stops.Take(12)) + " ...");
        CollectionAssert.AreEqual(expectedNames, names);
    }

    // K2. Ports KeyboardAccessTests.vb:707: with Ctrl, then Alt, pressed and
    // released before every Tab (the nearest a headless test comes to a key
    // left marked down), Tab still reaches each title once, in card order,
    // starting from the search box.
    [TestMethod]
    public void K2_TabReachesTheTitlesWithAModifierPressedBeforeEachTab()
    {
        using DiskLibrarySession session = OpenExample();

        List<(string Modifier, List<string> Reached, List<string> Expected, string Stops)> results = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            List<string> expected = board.Board.Shelves[0].Cards.Select(card => card.AutomationKey + ".Title").ToList();
            var results = new List<(string, List<string>, List<string>, string)>();
            foreach ((string name, Key key, RawInputModifiers held, PhysicalKey physical) in new[]
            {
                ("Ctrl", Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft),
                ("Alt", Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft),
            })
            {
                board.Find<TextBox>("SearchBox").Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                var reached = new List<string>();
                var stops = new List<string>();
                for (int count = 0; count < 80; count++)
                {
                    board.Window.KeyPress(key, held, physical, null);
                    board.Window.KeyRelease(key, RawInputModifiers.None, physical, null);
                    board.Press(Key.Tab);
                    string id = CardParts.Id(board.Focused() as Control);
                    stops.Add(id);
                    if (id.EndsWith(".Title", StringComparison.Ordinal))
                    {
                        if (reached.Contains(id))
                        {
                            break;
                        }

                        reached.Add(id);
                    }
                }

                results.Add((name, reached, expected, string.Join(" > ", stops)));
            }

            return results;
        });

        foreach ((string modifier, List<string> reached, List<string> expected, string stops) in results)
        {
            CollectionAssert.AreEqual(expected, reached, $"With {modifier} pressed before each Tab, Tab went to: {stops}");
        }
    }

    // K4. Ports KeyboardAccessTests.vb:538 (the keys): on a card's title,
    // Space with Alt, Ctrl or Shift does nothing; Enter opens the card, and so
    // does bare Space. Opening raises OpenRequested and gives the trial's
    // notice (d1 1.2), nothing else.
    [TestMethod]
    public void K4_EnterOrBareSpaceOnATitleOpensTheCardAndAModifiedSpaceDoesNot()
    {
        using DiskLibrarySession session = OpenExample();

        (List<string> modified, string afterEnter, string noticeAfterEnter, string afterSpace, bool stillFocused) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var opened = new List<string>();
            board.Board.OpenRequested += (_, card) => opened.Add(card.Title);
            CardView card = board.Card("PipelineShelf", Sleep);
            Button title = card.Part<Button>("Title");
            title.Focus(NavigationMethod.Tab);
            string notice = board.Shell.Notice;

            var modified = new List<string>();
            foreach (RawInputModifiers modifier in new[] { RawInputModifiers.Alt, RawInputModifiers.Control, RawInputModifiers.Shift })
            {
                board.Press(Key.Space, modifier);
                if (opened.Count > 0 || board.Shell.Notice != notice)
                {
                    modified.Add($"{modifier}+Space opened \"{string.Join(", ", opened)}\" with the notice \"{board.Shell.Notice}\"");
                    opened.Clear();
                }
            }

            board.Press(Key.Enter);
            string afterEnter = string.Join(" | ", opened);
            string noticeAfterEnter = board.Shell.Notice;
            opened.Clear();
            board.Press(Key.Space);
            return (modified, afterEnter, noticeAfterEnter, string.Join(" | ", opened), ReferenceEquals(board.Focused(), title));
        });

        Assert.IsEmpty(modified, string.Join(Environment.NewLine, modified));
        Assert.AreEqual(Sleep, afterEnter, "Enter on the title opens that card, once.");
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, BoardViewModel.OpenNoticeFormat, Sleep), noticeAfterEnter);
        Assert.AreEqual(Sleep, afterSpace, "Bare Space on the title opens it, once.");
        Assert.IsTrue(stillFocused, "Opening moves nothing in the trial: the keyboard stays on the title.");
    }

    // B6. Ports BoardFilterTests.vb:273: Tab to Clear, Space; Clear turns
    // itself off and the keyboard lands on the first card's title.
    [TestMethod]
    public void B6_ClearPressedWithTheKeyboardLeavesFocusOnTheFirstCard()
    {
        using DiskLibrarySession session = OpenExample();

        (string onClear, string focused, string expected, bool clearOn) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            board.Board.SelectedSortIndex = 1;
            Dispatcher.UIThread.RunJobs();
            board.Find<ComboBox>("BoardSort").Focus(NavigationMethod.Tab);
            board.Press(Key.Tab);
            string onClear = CardParts.Id(board.Focused() as Control);
            board.Press(Key.Space);
            Dispatcher.UIThread.RunJobs();
            return (onClear, CardParts.Id(board.Focused() as Control), board.Board.Shelves[0].Cards[0].AutomationKey + ".Title",
                    board.Find<Button>("ClearFilters").IsEffectivelyEnabled);
        });

        Assert.AreEqual("ClearFilters", onClear, "Tab from the sort box reaches Clear while a sort is on.");
        Assert.IsFalse(clearOn, "Clear turned itself off.");
        Assert.AreEqual(expected, focused, "The keyboard lands on the first card's title.");
    }

    // ---- Pointer, menu, notices ----

    // H2. Ports DashboardShelfLayoutTests.vb:159
    // CardsOpenOnClickAndKeepOtherActionsInAMenu: a click on the card's body
    // or on one of its labels opens it (OpenRequested and the notice); a click
    // on the tag strip, ⋯ or View route does not. ⋯ (named "More actions for
    // <title>") opens a menu of today's items for the card's shelf, every one
    // off with the tooltip "Not in the trial (read-only)", Delete... in the
    // danger class; a right-click on the card opens the same menu. View route
    // gives its notice.
    [TestMethod]
    public void H2_ACardOpensOnClickAndKeepsItsOtherActionsInADisabledMenu()
    {
        using DiskLibrarySession session = OpenExample();

        Dictionary<string, string> facts = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var facts = new Dictionary<string, string>();
            var opened = new List<string>();
            board.Board.OpenRequested += (_, card) => opened.Add(card.Title);
            string Take()
            {
                string result = string.Join(" | ", opened);
                opened.Clear();
                return result;
            }

            CardView sleep = board.Card("PipelineShelf", Sleep);
            Border body = sleep.Part<Border>("Card");
            board.ClickAt(body, new Point(body.Bounds.Width / 2, 5));
            facts["Body"] = Take();
            facts["BodyNotice"] = board.Shell.Notice;
            board.Click(sleep.Part<TextBlock>("Status"));
            facts["Status"] = Take();
            board.Click(sleep.Part<TextBlock>("Journal"));
            facts["Journal"] = Take();
            board.Click(sleep.Part<Button>("Title"));
            facts["Title"] = Take();

            board.Click(sleep.Part<Button>("ViewRoute"));
            facts["Route"] = Take();
            facts["RouteNotice"] = board.Shell.Notice;

            Button more = sleep.Part<Button>("More");
            facts["MoreName"] = AutomationProperties.GetName(more) ?? string.Empty;
            board.Click(more);
            facts["More"] = Take();
            facts["MoreOpen"] = sleep.MoreMenu.IsOpen.ToString();
            facts["PipelineMenu"] = CardParts.Describe(sleep.MoreMenu);
            sleep.MoreMenu.Hide();
            Dispatcher.UIThread.RunJobs();

            board.ClickAt(body, new Point(body.Bounds.Width / 2, 5), MouseButton.Right);
            facts["RightClick"] = Take();
            facts["RightClickOpen"] = sleep.RightClickMenu.IsOpen.ToString();
            facts["RightClickMenu"] = CardParts.Describe(sleep.RightClickMenu);
            sleep.RightClickMenu.Hide();
            Dispatcher.UIThread.RunJobs();

            foreach ((int index, string shelf, string key) in new[] { (1, "PublishedShelf", "PublishedMenu"), (2, "FileDrawerShelf", "FileDrawerMenu") })
            {
                board.Board.SelectedShelfIndex = index;
                Dispatcher.UIThread.RunJobs();
                CardView first = board.Cards(shelf).Single();
                board.Click(first.Part<Button>("More"));
                facts[key] = CardParts.Describe(first.MoreMenu);
                first.MoreMenu.Hide();
                Dispatcher.UIThread.RunJobs();
            }

            facts["Menus"] = Take();

            // Last, as it starts the search's wait.
            board.Board.SelectedShelfIndex = 0;
            Dispatcher.UIThread.RunJobs();
            board.Click(board.Card("PipelineShelf", Retrieval).Part<TagStripPanel>("TagStrip"));
            facts["Tags"] = Take();
            facts["TagsSearch"] = board.Board.SearchText;
            return facts;
        });

        void Expect(string key, string expected) => Assert.AreEqual(expected, facts[key], key);
        const string Off = " (off: Not in the trial (read-only))";

        Expect("Body", Sleep);
        Expect("BodyNotice", "Opening a manuscript is not part of the trial: " + Sleep + ".");
        Expect("Status", Sleep);
        Expect("Journal", Sleep);
        Expect("Title", Sleep);
        Expect("Route", string.Empty);
        Expect("RouteNotice", "Viewing the route is not part of the trial: " + Sleep + ".");
        Expect("MoreName", "More actions for " + Sleep);
        Expect("More", string.Empty);
        Expect("MoreOpen", "True");
        Expect("PipelineMenu", "Open" + Off + " / View Route" + Off + " / - / Move to File Drawer..." + Off + " / - / Delete..." + Off + " danger");
        Expect("RightClick", string.Empty);
        Expect("RightClickOpen", "True");
        Expect("RightClickMenu", facts["PipelineMenu"]);
        Expect("PublishedMenu", "Open" + Off + " / View Route" + Off + " / - / Delete..." + Off + " danger");
        Expect("FileDrawerMenu", "Open" + Off + " / View Route" + Off + " / - / Restore to Pipeline" + Off + " / - / Delete..." + Off + " danger");
        Expect("Menus", string.Empty);
        Expect("Tags", string.Empty);
        Expect("TagsSearch", "teaching");
    }

    // V17, headless part. Ports Form1.vb:3285-3289: a click on any chip of the
    // tag strip, here the "+N" at its end, puts the card's FIRST tag in the
    // search box, and the Board renders the search after its wait. The strip
    // shows as many tags as fit in half the line, then "+N" (TagChips.vb:
    // 107-130); it is not a tab stop, and is named and tipped as today.
    [TestMethod]
    public void V17_AClickOnTheTagStripSearchesForTheFirstTag()
    {
        Manuscript tagged = Papers.Paper("A paper with many tags");
        tagged.Tags = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta"];
        var session = new MemorySession([tagged, Papers.Paper("Another paper"), Papers.Paper("A third paper")], Papers.Today);

        Dictionary<string, string> facts = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var opened = new List<string>();
            board.Board.OpenRequested += (_, card) => opened.Add(card.Title);
            CardView card = board.Card("PipelineShelf", "A paper with many tags");
            TagStripPanel strip = card.Part<TagStripPanel>("TagStrip");
            var facts = new Dictionary<string, string>
            {
                ["Shown"] = string.Join(" ", strip.ShownTexts),
                ["Name"] = AutomationProperties.GetName(strip) ?? string.Empty,
                ["Tip"] = ToolTip.GetTip(strip) as string ?? string.Empty,
                ["Focusable"] = (strip.Focusable || strip.IsTabStop).ToString(),
                ["HalfLine"] = (strip.Bounds.Width <= Math.Floor(card.Part<Border>("Card").Bounds.Width / 2)).ToString(),
            };

            // The last chip drawn: "+N".
            Border last = strip.GetVisualChildren().OfType<Border>().Where(chip => chip.Bounds.Width > 0).OrderBy(chip => chip.Bounds.X).Last();
            board.Click(last);
            facts["Opened"] = string.Join(" | ", opened);
            facts["Search"] = board.Board.SearchText;
            facts["Box"] = board.Find<TextBox>("SearchBox").Text ?? string.Empty;
            int renders = board.Board.RenderCount;
            BoardWindow.WaitFor(() => board.Board.RenderCount > renders, TimeSpan.FromSeconds(10));
            facts["Tab"] = board.Board.Shelves[0].TabText;
            return facts;
        });

        string[] shown = facts["Shown"].Split(' ');
        Assert.IsTrue(shown[^1].StartsWith('+'), $"The strip ends with +N: {facts["Shown"]}");
        int more = int.Parse(shown[^1].TrimStart('+'), CultureInfo.CurrentCulture);
        Assert.AreEqual(8, shown.Length - 1 + more, $"The chips shown and N count every tag: {facts["Shown"]}");
        CollectionAssert.AreEqual(new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta" }.Take(shown.Length - 1).ToArray(), shown[..^1], "The first tags, in order.");
        Assert.AreEqual("True", facts["HalfLine"], "The strip keeps to half the line.");
        Assert.AreEqual("Tags: alpha, beta, gamma, delta, epsilon, zeta, eta, theta", facts["Name"]);
        Assert.AreEqual("alpha, beta, gamma, delta, epsilon, zeta, eta, theta (click to search the board)", facts["Tip"]);
        Assert.AreEqual("False", facts["Focusable"], "The strip is not a tab stop.");
        Assert.AreEqual(string.Empty, facts["Opened"], "A click on the strip does not open the card.");
        Assert.AreEqual("alpha", facts["Search"], "The first tag, whichever chip was clicked.");
        Assert.AreEqual("alpha", facts["Box"], "The search box shows it.");
        Assert.AreEqual("Pipeline (1 of 3)", facts["Tab"], "The search rendered after its wait.");
    }

    // H6. Ports MotionTests.vb:290: focus on a title turns its card's edge to
    // the accent at once (:focus-within, no transition: an accent ring over
    // the border, see Card.axaml); the pointer over a card turns its border
    // there through its 100 ms transition; both go back when focus and
    // pointer leave.
    [TestMethod]
    public void H6_FocusHighlightsACardAtOnceAndThePointerEasesIt()
    {
        using DiskLibrarySession session = OpenExample();

        Dictionary<string, string> facts = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
            Dispatcher.UIThread.RunJobs();
            IReadOnlyList<CardView> cards = board.Cards("PipelineShelf");
            Border first = cards[0].Part<Border>("Card");
            Border second = cards[1].Part<Border>("Card");
            Color accent = CardParts.Token(first, "AccentBrush");
            Color rest = CardParts.Token(first, "CardBorderBrush");
            var facts = new Dictionary<string, string>
            {
                ["Rest"] = (CardParts.ColorOf(first.BorderBrush) == rest).ToString(),
            };

            // Focus: the accent edge over the card's border, drawn at once, with no frame ticked.
            Border ring = cards[0].Part<Border>("FocusRing");
            facts["RingAtRest"] = ring.IsVisible.ToString();
            cards[0].Part<Button>("Title").Focus(NavigationMethod.Tab);
            facts["FocusAtOnce"] = $"{ring.IsEffectivelyVisible} {CardParts.ColorOf(ring.BorderBrush) == accent} {ring.BorderThickness}";
            facts["RingTransitions"] = (ring.Transitions?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
            board.Window.UpdateLayout();
            facts["RingCoversCard"] = (ring.Bounds == first.Bounds).ToString();

            board.Find<TextBox>("SearchBox").Focus(NavigationMethod.Tab);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
            Dispatcher.UIThread.RunJobs();
            facts["BackAtRest"] = (!ring.IsVisible && CardParts.ColorOf(first.BorderBrush) == rest).ToString();
            facts["BackAtRestColour"] = $"{CardParts.ColorOf(first.BorderBrush)} (rest {rest}, accent {accent}, ring shown {ring.IsVisible})";

            BrushTransition? transition = second.Transitions?.OfType<BrushTransition>().SingleOrDefault(item => item.Property == Border.BorderBrushProperty);
            facts["Transition"] = transition?.Duration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) ?? "none";
            Point centre = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2), board.Window) ?? default;
            // The transition runs on the clock, not on frames: read the border as the
            // pointer arrives, then after more than 100 ms and a frame.
            board.Window.MouseMove(centre, RawInputModifiers.None);
            Color atMove = CardParts.ColorOf(second.BorderBrush);
            facts["HoverNotAtOnce"] = (second.IsPointerOver && atMove != accent).ToString();
            facts["HoverSteps"] = $"at the move {atMove}, pointer over {second.IsPointerOver} (rest {rest}, accent {accent})";
            void Settle()
            {
                BoardWindow.WaitFor(() => false, TimeSpan.FromMilliseconds(250));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                Dispatcher.UIThread.RunJobs();
            }

            Settle();
            facts["HoverEased"] = (CardParts.ColorOf(second.BorderBrush) == accent).ToString();
            board.Window.MouseMove(new Point(5, board.Window.Bounds.Height - 5), RawInputModifiers.None);
            Settle();
            facts["HoverLeft"] = (CardParts.ColorOf(second.BorderBrush) == rest).ToString();
            return facts;
        });

        void Expect(string key, string expected) => Assert.AreEqual(expected, facts[key], key);
        Expect("Rest", "True");
        Expect("RingAtRest", "False");
        Expect("FocusAtOnce", "True True 1,1,1,1");
        Expect("RingTransitions", "0");
        Expect("RingCoversCard", "True");
        Assert.AreEqual("True", facts["BackAtRest"], facts["BackAtRestColour"]);
        Expect("Transition", "100");
        // Whether the border is still on its way as the pointer arrives is only
        // logged: run alone it is (the colour at rest), but in the whole suite's
        // run the headless clock has shown it at the accent already. The ease
        // itself is for a person to look at on a real window.
        TestContext.WriteLine($"Hover: still easing as the pointer arrives: {facts["HoverNotAtOnce"]}; {facts["HoverSteps"]}");
        Expect("HoverEased", "True");
        Expect("HoverLeft", "True");
    }

    // ---- What the cards show ----

    // r1 1.7 worked out what today's Board shows on the example, from the
    // code, never run: the three status lines, the idea's "No target journal
    // yet" in the warning ink, the File Drawer card's "No target journal" in
    // the muted ink with no status line, and three cards with tags. This reads
    // the same off the drawn cards, all seven, one shelf at a time, and writes
    // every card's drawn texts to the test log. Every card is one height.
    [TestMethod]
    public void TheExampleShowsItsSevenCardsAsR1WorksThemOut()
    {
        using DiskLibrarySession session = OpenExample();

        List<DrawnCard> cards = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var cards = new List<DrawnCard>();
            foreach ((int index, string shelf) in new[] { (0, "PipelineShelf"), (1, "PublishedShelf"), (2, "FileDrawerShelf") })
            {
                board.Board.SelectedShelfIndex = index;
                Dispatcher.UIThread.RunJobs();
                board.Window.UpdateLayout();
                cards.AddRange(board.Cards(shelf).Select(card => DrawnCard.Of(shelf, card)));
            }

            return cards;
        });

        foreach (DrawnCard card in cards)
        {
            TestContext.WriteLine(card.ToString());
        }

        Assert.HasCount(7, cards);
        CollectionAssert.AreEqual(
            new[] { Retrieval, Sleep, Invariance, OpenMaterials, Pilot, Anchoring, NullResult },
            cards.Select(card => card.Title).ToArray(),
            "Five on the Pipeline in library order, one Published, one in the File Drawer.");

        DrawnCard Of(string title) => cards.Single(card => card.Title == title);

        Assert.AreEqual("REVISION", Of(Retrieval).Pill);
        Assert.AreEqual("Revision due in 10 days [warning]", Of(Retrieval).Status);
        Assert.AreEqual("UNDER REVIEW", Of(Sleep).Pill);
        Assert.AreEqual("Waiting 104 days [warning]", Of(Sleep).Status);
        Assert.AreEqual("DRAFT", Of(Invariance).Pill);
        Assert.AreEqual("Rejected 6 days ago [danger]", Of(Invariance).Status);
        Assert.AreEqual("IDEA", Of(Pilot).Pill);
        Assert.AreEqual("No target journal yet [warning]", Of(Pilot).Journal);
        Assert.AreEqual("PUBLISHED", Of(Anchoring).Pill);
        Assert.AreEqual("No target journal [muted]", Of(NullResult).Journal);
        Assert.AreEqual(string.Empty, Of(NullResult).Status, "No status line: its latest rejection was 190 days ago and it is off the Pipeline.");
        Assert.AreEqual("3 journals · 3 rejections", Of(NullResult).Route);
        Assert.AreEqual("closed closed closed", Of(NullResult).Dots);

        // Every other status line is the stage clock, in the muted ink, or nothing off the Pipeline.
        Assert.IsTrue(Of(OpenMaterials).Status.EndsWith("[muted]", StringComparison.Ordinal), Of(OpenMaterials).Status);
        Assert.IsTrue(Of(Pilot).Status.EndsWith("[muted]", StringComparison.Ordinal), Of(Pilot).Status);
        Assert.AreEqual(string.Empty, Of(Anchoring).Status);

        CollectionAssert.AreEquivalent(
            new[] { Retrieval + ": teaching", OpenMaterials + ": open science", Anchoring + ": replication" },
            cards.Where(card => card.Tags.Length > 0).Select(card => card.Title + ": " + card.Tags).ToArray(),
            "Three cards carry tags.");

        Assert.HasCount(1, cards.Select(card => card.Height).Distinct().ToList(), "Every card is one height: " + string.Join(", ", cards.Select(card => card.Height)));
    }

    // The columns (ManuscriptShelfPanel.vb:56-59 with Form1.vb's 280 px card
    // and 12 px gap): as many as fit (width - 8 + 12) / (280 + 12), at least
    // one, each an equal whole-pixel share of the rest.
    [TestMethod]
    [DataRow(200.0, 1, 192.0)]
    [DataRow(296.0, 1, 288.0)]
    [DataRow(579.0, 1, 571.0)]
    [DataRow(580.0, 2, 280.0)]
    [DataRow(1000.0, 3, 322.0)]
    [DataRow(1176.0, 4, 283.0)]
    public void AShelfShowsAsManyColumnsAsTodaysShelf(double width, int columns, double itemWidth)
    {
        var shelf = new ShelfViewModel(ManuscriptLocation.Pipeline);
        shelf.SetWidth(width);
        Assert.AreEqual(columns, shelf.Columns, $"Columns at {width} px.");
        Assert.AreEqual(itemWidth, shelf.ItemWidth, $"Card width at {width} px.");
    }

    // The same on the drawn shelf, at 1280 and at 760 px: the cards fill as
    // many columns as the rule gives, every card lies inside the shelf, and
    // the hidden shelves made no card until first shown (d1 4.4).
    [TestMethod]
    public void TheCardsFillTheShelfInColumnsAndHiddenShelvesWaitUntilShown()
    {
        using DiskLibrarySession session = OpenExample();

        List<string> facts = Headless.Run(() =>
        {
            var facts = new List<string>();
            foreach (double width in new[] { 1280.0, 760.0 })
            {
                using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light, width);
                ShelfView shelf = board.Shelf("PipelineShelf");
                ShelfViewModel model = board.Board.Shelves[0];
                IReadOnlyList<CardView> cards = board.Cards("PipelineShelf");
                double shelfRight = shelf.Bounds.Width;
                int columnsDrawn = cards.Select(card => Math.Round(card.TranslatePoint(default, shelf)?.X ?? -1)).Distinct().Count();
                bool inside = cards.All(card => (card.TranslatePoint(new Point(card.Bounds.Width, 0), shelf)?.X ?? double.MaxValue) <= shelfRight + 0.5);
                facts.Add($"{width}: columns {model.Columns}, drawn {Math.Min(columnsDrawn, cards.Count)} of {Math.Min(model.Columns, cards.Count)}, " +
                          $"card {model.ItemWidth} = {cards[0].Bounds.Width}, inside {inside}, " +
                          $"published views {board.Cards("PublishedShelf").Count}, published realized {board.Board.Shelves[1].IsRealized}");
                Assert.AreEqual(Math.Min(model.Columns, cards.Count), columnsDrawn, facts[^1]);
                Assert.AreEqual(model.ItemWidth, cards[0].Bounds.Width, facts[^1]);
                Assert.IsTrue(inside, "A card runs past the shelf's edge: " + facts[^1]);
                Assert.IsEmpty(board.Cards("PublishedShelf"), "The Published shelf made no card before it was shown.");
                Assert.IsFalse(board.Board.Shelves[1].IsRealized);

                board.Board.SelectedShelfIndex = 1;
                Dispatcher.UIThread.RunJobs();
                Assert.HasCount(1, board.Cards("PublishedShelf"), "Shown, the Published shelf made its card.");
                Assert.IsTrue(board.Board.Shelves[1].IsRealized);
            }

            return facts;
        });

        foreach (string fact in facts)
        {
            TestContext.WriteLine(fact);
        }
    }

    // ---- V22 ----

    // V22. The plan's section 3, check 1: every target surface in the test
    // project's own catalog has a factory, and the factory builds the view it
    // names over its library, which renders.
    [TestMethod]
    public void V22_EveryTargetSurfaceHasAFactory()
    {
        Assert.IsTrue(TargetSurfaceCatalog.Surfaces.Count > 0, "The catalog lists at least one surface.");
        CollectionAssert.AllItemsAreUnique(TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList());
        CollectionAssert.Contains(TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList(), "Board");

        foreach (TargetSurface surface in TargetSurfaceCatalog.Surfaces)
        {
            Assert.AreEqual(TargetSurfaceCatalog.ExampleLibrary, surface.Library, $"{surface.Key}: no test library is known by \"{surface.Library}\".");
            using DiskLibrarySession session = OpenExample(surface.Key);
            (string type, int colours, int cards) = Headless.Run(() =>
            {
                Control view = surface.Create(session);
                var window = new Window { Width = 1280, Height = 800, Content = view };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                using WriteableBitmap? frame = window.CaptureRenderedFrame();
                int colours = frame is null ? 0 : Pictures.CountColours(frame);
                int cards = view.GetVisualDescendants().OfType<CardView>().Count();
                window.Close();
                return (view.GetType().FullName ?? string.Empty, colours, cards);
            });

            Assert.AreEqual(surface.ViewType.FullName, type, $"{surface.Key}: the factory built the view it names.");
            Assert.IsGreaterThan(1, colours, $"{surface.Key}: the view renders.");
            TestContext.WriteLine($"{surface.Key}: {type}, {cards} card views");
            if (surface.Key == "Board")
            {
                Assert.AreEqual(5, cards, "The Board shows the example's five Pipeline cards.");
            }
        }
    }

    // ---- Pictures ----

    [TestMethod]
    public void ThePublishedShelfIsSaved() => SaveExampleShelf(1, "board-published-light");

    [TestMethod]
    public void TheFileDrawerShelfIsSaved() => SaveExampleShelf(2, "board-filedrawer-light");

    // The 500 library's Pipeline shelf at 1280 x 800, as it opens.
    [TestMethod]
    public void TheFiveHundredPipelineIsSaved()
    {
        using DiskLibrarySession session = OpenFiveHundred();
        (string path, int views) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            return (Pictures.Save(board.Window, "board-500-pipeline-light", TestContext), board.Cards("PipelineShelf").Count);
        });

        Assert.AreEqual(167, views, "The Pipeline shelf made all 167 of its cards.");
        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // A close-up of one shelf: cards of the Pipeline's stages from Idea to In
    // Press, and each tone of the status and journal lines side by side:
    // danger (overdue, rejected), warning (due soon, waiting, consider
    // filing, no target journal yet), muted (the stage clock); tags with
    // "+N"; route dots closed, current and accepted.
    [TestMethod]
    public void TheCloseUpOfStagesAndTonesIsSavedInLight() => SaveCloseUp(ThemeVariant.Light, "cards-closeup-light");

    [TestMethod]
    public void TheCloseUpOfStagesAndTonesIsSavedInDark() => SaveCloseUp(ThemeVariant.Dark, "cards-closeup-dark");

    // ---- A first reading of the 500 library's times ----

    // How long the 500 library takes to show its first shelf and to switch
    // to the others, in this headless run, as d1 6.3 measures (Stopwatch, the
    // interface thread does nothing else, so UI equals wall). Nothing is
    // asserted about time: step 12 sets the budgets. The example goes first,
    // report-only, as it carries the first-time costs; then the 500 library
    // twice. "Shown" is the Board rendered, the window shown and laid out;
    // "painted" adds one rendered frame. Each switch is the index set and laid
    // out, then a frame. The numbers go to the test log and to
    // timings-500.txt beside the pictures.
    [TestMethod]
    public void TheFiveHundredLibrarysShelfTimesAreRead()
    {
        using DiskLibrarySession example = OpenExample();
        using DiskLibrarySession large = OpenFiveHundred();

#if DEBUG
        const string Build = "Debug";
#else
        const string Build = "Release";
#endif
        var lines = new List<string>
        {
            $"Board trial, headless (Skia), {DateTime.Now:yyyy-MM-dd HH:mm}, {Environment.ProcessorCount} processors, {Build} build.",
        };

        foreach ((string name, DiskLibrarySession session) in new[] { ("example", example), ("500 (1st)", large), ("500 (2nd)", large) })
        {
            string line = Headless.Run(() => Measure(name, session));
            lines.Add(line);
        }

        foreach (string line in lines)
        {
            TestContext.WriteLine(line);
        }

        string folder = Pictures.Directory();
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "timings-500.txt"), lines);
    }

    private static string Measure(string name, ILibrarySession session)
    {
        var clock = Stopwatch.StartNew();
        ShellViewModel shell = App.CreateShell(session);
        shell.Board.Render();
        long rendered = clock.ElapsedMilliseconds;
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        long shown = clock.ElapsedMilliseconds;
        Frame(window);
        long painted = clock.ElapsedMilliseconds;
        BoardView view = window.FindControl<BoardView>("BoardView")!;
        int pipeline = view.FindControl<ShelfView>("PipelineShelf")!.CardViews().Count;
        int publishedBefore = view.FindControl<ShelfView>("PublishedShelf")!.CardViews().Count;

        var parts = new StringBuilder();
        parts.Append(CultureInfo.InvariantCulture, $"{name}: first shelf (Pipeline, {pipeline} cards): render {rendered} ms, shown {shown} ms, painted {painted} ms");
        foreach ((int index, string shelf) in new[] { (1, "PublishedShelf"), (2, "FileDrawerShelf"), (0, "PipelineShelf"), (1, "PublishedShelf") })
        {
            clock.Restart();
            shell.Board.SelectedShelfIndex = index;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            long laidOut = clock.ElapsedMilliseconds;
            Frame(window);
            long framed = clock.ElapsedMilliseconds;
            int views = view.FindControl<ShelfView>(shelf)!.CardViews().Count;
            parts.Append(CultureInfo.InvariantCulture, $"; to {shelf.Replace("Shelf", string.Empty, StringComparison.Ordinal)} ({views} cards): shown {laidOut} ms, painted {framed} ms");
        }

        parts.Append(CultureInfo.InvariantCulture, $"; Published views before its first show: {publishedBefore}");
        window.Close();
        return parts.ToString();
    }

    private static void Frame(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
    }

    private void SaveExampleShelf(int index, string name)
    {
        using DiskLibrarySession session = OpenExample();
        string path = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            board.Board.SelectedShelfIndex = index;
            Dispatcher.UIThread.RunJobs();
            return Pictures.Save(board.Window, name, TestContext);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    private void SaveCloseUp(ThemeVariant variant, string name)
    {
        var session = new MemorySession(Showcase(), Papers.Today);
        string path = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, variant, height: 1060);
            Panel host = board.Find<Panel>("ShelfHost");
            Point origin = host.TranslatePoint(default, board.Window) ?? default;
            var region = new PixelRect((int)origin.X, (int)origin.Y, (int)host.Bounds.Width, (int)host.Bounds.Height);
            return Pictures.Save(board.Window, name, TestContext, region);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // ---- Libraries ----

    // Ten Pipeline cards, one of each stage from Idea to In Press and each tone.
    private static List<Manuscript> Showcase()
    {
        Manuscript idea = Papers.Paper("An idea with no target journal yet", PaperStage.Idea, journal: string.Empty, stageDaysAgo: 3);
        Manuscript tagged = Papers.Paper("A draft with four tags, more than half its line holds", PaperStage.Draft, journal: "Fictional Open Psychology", stageDaysAgo: 12);
        tagged.Tags = ["teaching", "replication", "open science", "pilot"];
        Manuscript submitted = Papers.Paper("Submitted last month, inside the waiting threshold", PaperStage.Submitted, journal: "Fictional Psychological Letters", stageDaysAgo: 30)
            .With(Papers.Submission(30));
        Manuscript waiting = Papers.Waiting("Under review for four months, with a long title that runs past two lines on a narrow card", 120);
        waiting.TargetJournal = "Fictional Assessment Quarterly";
        Manuscript dueSoon = Papers.Revision("A major revision due in five days", 5);
        dueSoon.TargetJournal = "Fictional Journal of Research Methods";
        Manuscript overdue = Papers.Revision("A revision three days overdue", -3);
        overdue.TargetJournal = "Fictional Journal of Research Methods";
        Manuscript rejected = Papers.Rejected("Rejected after review last week", 4);
        rejected.TargetJournal = "Fictional Open Psychology";
        Manuscript filing = Papers.RejectedTimes("Three rejections: time to consider the File Drawer", 3, journal: "Fictional Psychological Letters");
        Manuscript accepted = Papers.Paper("Accepted, waiting for proofs", PaperStage.Accepted, journal: "Fictional Open Psychology", stageDaysAgo: 20)
            .With(Papers.Submission(300, (EditorialDecision.DeskRejected, 290, null)), Papers.Submission(200, (EditorialDecision.Accepted, 20, null)));
        Manuscript inPress = Papers.Paper("In press at last", PaperStage.InPress, journal: "Fictional Journal of Research Methods", stageDaysAgo: 8)
            .With(Papers.Submission(260, (EditorialDecision.Accepted, 40, null)));
        return [idea, tagged, submitted, waiting, dueSoon, overdue, rejected, filing, accepted, inPress];
    }

    // Sixty Pipeline papers, some with tags and some with a submission.
    private static List<Manuscript> Sixty() =>
        Enumerable.Range(1, 60).Select(number =>
        {
            Manuscript paper = Papers.Paper($"Paper {number:00} on the board's keyboard route", number % 3 == 0 ? PaperStage.Submitted : PaperStage.Draft);
            if (number % 4 == 0)
            {
                paper.Tags = ["tag " + number.ToString(CultureInfo.InvariantCulture), "second"];
            }

            if (number % 5 == 0)
            {
                paper.With(Papers.Submission(30));
            }

            return paper;
        }).ToList();

    // The example written to a folder of this test's own and read through the
    // trial's session on the test thread, before any window is made.
    private DiskLibrarySession OpenExample(string folder = "example")
    {
        string root = Path.Combine(_root, folder);
        Directory.CreateDirectory(root);
        LargeLibraryFixture.WriteExample(root);
        var session = new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);
        return session;
    }

    // The 500 library (167 Pipeline, 272 Published, 61 File Drawer).
    private DiskLibrarySession OpenFiveHundred()
    {
        string root = Path.Combine(_root, "five-hundred");
        Directory.CreateDirectory(root);
        LargeLibraryFixture.Create(root, 500);
        var session = new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            Path.Combine(root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(500, session.Manuscripts);
        return session;
    }

    // What one drawn card shows.
    private sealed record DrawnCard(string Shelf, string Title, string Pill, string Status, string Journal, string Tags, string Route, string Dots, double Height)
    {
        public static DrawnCard Of(string shelf, CardView card)
        {
            TextBlock status = card.Part<TextBlock>("Status");
            TextBlock journal = card.Part<TextBlock>("Journal");
            string dots = string.Join(" ", card.Part<ItemsControl>("RouteDots").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
                .Select(dot => dot.Classes.FirstOrDefault(name => name is "closed" or "current" or "accepted") ?? "?"));
            return new DrawnCard(
                shelf,
                card.Part<TextBlock>("TitleText").Text ?? string.Empty,
                card.Part<TextBlock>("StageText").Text ?? string.Empty,
                status.IsEffectivelyVisible ? $"{status.Text} [{Tone(status)}]" : string.Empty,
                $"{journal.Text} [{Tone(journal)}]",
                string.Join(", ", card.Part<TagStripPanel>("TagStrip").ShownTexts),
                card.Part<TextBlock>("RouteText").Text ?? string.Empty,
                card.Part<ItemsControl>("RouteDots").IsVisible ? dots : string.Empty,
                card.Part<Border>("Card").Bounds.Height);
        }

        private static string Tone(Control text) =>
            text.Classes.Contains("danger") ? "danger" : text.Classes.Contains("warning") ? "warning" : text.Classes.Contains("muted") ? "muted" : "secondary";
    }
}

// Lookups the card tests share.
internal static class CardParts
{
    public static IReadOnlyList<CardView> Cards(this BoardWindow board, string shelf) => board.Shelf(shelf).CardViews();

    public static CardView Card(this BoardWindow board, string shelf, string title) =>
        board.Cards(shelf).Single(card => (card.DataContext as CardViewModel)?.Title == title);

    public static T Part<T>(this CardView card, string name)
        where T : Control =>
        card.FindControl<T>(name) ?? throw new AssertFailedException($"The card has no {typeof(T).Name} named {name}.");

    public static void ClickAt(this BoardWindow board, Control control, Point point, MouseButton button = MouseButton.Left)
    {
        Point at = control.TranslatePoint(point, board.Window) ?? throw new AssertFailedException($"{control} is not in the window.");
        board.Window.MouseMove(at, RawInputModifiers.None);
        board.Window.MouseDown(at, button, RawInputModifiers.None);
        board.Window.MouseUp(at, button, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    // A control's automation ID, else its type: what Tab reached.
    public static string Id(Control? control) =>
        control is null ? "(nothing)" : AutomationProperties.GetAutomationId(control) is { Length: > 0 } id ? id : control.GetType().Name;

    // A menu's items in order: "Header (off: tooltip) danger", "-" for a line.
    public static string Describe(MenuFlyout menu) =>
        string.Join(" / ", menu.Items.Select(item => item switch
        {
            Separator => "-",
            MenuItem entry => $"{entry.Header}{(entry.IsEnabled ? string.Empty : " (off: " + (ToolTip.GetTip(entry) as string) + ")")}{(entry.Classes.Contains("danger") ? " danger" : string.Empty)}",
            _ => item?.ToString() ?? "(null)",
        }));

    public static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    public static Color Token(Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out object? found) && found is ISolidColorBrush brush
            ? brush.Color
            : throw new AssertFailedException($"No brush {key}.");
}
