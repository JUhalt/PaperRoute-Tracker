using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Input;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The Board view without cards (d1 8, step 8): the header and its search, the
// Needs Attention chips, the shelf bar's tabs, boxes and Clear, the empty
// messages and the welcome. The tests are the H parts of d1 6.2 that the step
// names: H1, H5, K3, K5, K6 (Ctrl+F), K7, and the headless halves of B1 to B4
// and B7, which drive the real ComboBox with keys and count renders by
// BoardViewModel.RenderCount.
//
// Each test builds the shell with App.CreateShell, the app's own: the search
// waits on a DispatcherTimer and the box rule's flush is posted to the
// interface thread, as in the app. The library is the example, written to a
// temporary folder of the test's own and read through the trial's session
// before any window is made (never through TrialStartup, d1 6.1), or an empty
// library held in memory; all under the assembly's storage guard.
[TestClass]
public sealed class BoardViewTests : StorageCheckedTests
{
    private const int AllStages = 0;
    private const int Idea = 1;
    private const int Draft = 2;
    private const int Submitted = 3;

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialBoardView-" + Guid.NewGuid().ToString("N"));
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

    // ---- The #338 rule on the real boxes (B1 to B4 and B7, H parts) ----

    // What Avalonia 12.1.6's ComboBox does while its list is open, recorded
    // step by step: the box's own SelectedIndex, the view model's, and whether
    // the list is open. The finding the d1 9 risk row asks for: the arrow keys
    // in the open list do NOT change the box's selection (they move the
    // keyboard from item to item); Enter selects the item reached and closes
    // the list, and the selection arrives while the list is still open, so
    // the #338 rule renders once, on the close. The two-way bindings of
    // SelectedIndex and IsDropDownOpen stay as they are. What does need the
    // view is Esc (B7): see ArrowChoiceKeeper.
    [TestMethod]
    public void TheStageBoxKeepsItsSelectionWhileItsListIsOpen()
    {
        using DiskLibrarySession session = OpenExample();

        List<string> steps = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox box = board.Find<ComboBox>("StageFilter");
            var steps = new List<string>();
            void Record(string step) => steps.Add(
                $"{step}: box {box.SelectedIndex}, view model {board.Board.SelectedStageIndex}, open {box.IsDropDownOpen}/{board.Board.IsStageListOpen}, renders {board.Board.RenderCount}");

            box.Focus(NavigationMethod.Tab);
            Record("focused");
            board.Press(Key.Down, RawInputModifiers.Alt);
            Record("Alt+Down");
            for (int index = 0; index < 3; index++)
            {
                board.Press(Key.Down);
                Record("Down");
            }

            board.Press(Key.Enter);
            Record("Enter");
            return steps;
        });

        foreach (string step in steps)
        {
            TestContext.WriteLine(step);
        }

        Assert.HasCount(6, steps);
        Assert.AreEqual("Alt+Down: box 0, view model 0, open True/True, renders 1", steps[1], "Alt+Down opens the list and changes nothing.");
        Assert.AreEqual("Down: box 0, view model 0, open True/True, renders 1", steps[2], "A step in the open list leaves the box's selection alone.");
        Assert.AreEqual("Down: box 0, view model 0, open True/True, renders 1", steps[4], "So does every step; nothing renders while the list is open.");
        Assert.AreEqual("Enter: box 3, view model 3, open False/False, renders 2", steps[5], "Enter closes the list, and the Board renders once.");
    }

    // The other half of the finding, on a plain ComboBox with nothing of the
    // trial's: Esc closes the list on the opening choice, forgetting the item
    // the arrows reached. Today's Windows list keeps that item (r1 2.3, 4.3),
    // so the Board's boxes carry ArrowChoiceKeeper (B7).
    [TestMethod]
    public void APlainComboBoxForgetsTheArrowsOnEsc()
    {
        (int afterArrows, int afterEsc, bool open) = Headless.Run(() =>
        {
            var box = new ComboBox { ItemsSource = new[] { "One", "Two", "Three", "Four" }, SelectedIndex = 0 };
            var window = new Window { Width = 400, Height = 300, Content = box };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                box.Focus(NavigationMethod.Tab);
                Keyboard.Press(window, Key.Down, RawInputModifiers.Alt);
                Keyboard.Press(window, Key.Down);
                Keyboard.Press(window, Key.Down);
                int afterArrows = box.SelectedIndex;
                Keyboard.Press(window, Key.Escape);
                return (afterArrows, box.SelectedIndex, box.IsDropDownOpen);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(0, afterArrows, "The arrows in the open list leave the selection alone.");
        Assert.AreEqual(0, afterEsc, "Esc closes on the opening choice.");
        Assert.IsFalse(open);
    }

    // B1, H part. Ports BoardFilterTests.vb:58 on a real ComboBox: Alt+Down,
    // Down three times, Enter. Nothing renders while the list is open; the
    // close renders once, with the item the arrows reached.
    [TestMethod]
    public void B1_SteppingTheOpenStageListRendersOnceWhenItCloses()
    {
        using DiskLibrarySession session = OpenExample();

        (int whileOpen, int afterClose, int index, bool open, string tab) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox box = board.Find<ComboBox>("StageFilter");
            int start = board.Board.RenderCount;
            box.Focus(NavigationMethod.Tab);
            board.Press(Key.Down, RawInputModifiers.Alt);
            Assert.IsTrue(board.Board.IsStageListOpen, "Alt+Down opened the list.");
            board.Press(Key.Down);
            board.Press(Key.Down);
            board.Press(Key.Down);
            int whileOpen = board.Board.RenderCount - start;
            board.Press(Key.Enter);
            return (whileOpen, board.Board.RenderCount - start, board.Board.SelectedStageIndex, box.IsDropDownOpen, board.Board.Shelves[0].TabText);
        });

        Assert.AreEqual(0, whileOpen, "Nothing is rendered while the list is open.");
        Assert.AreEqual(1, afterClose, "The close rendered the Board once.");
        Assert.AreEqual(Submitted, index, "The Board shows the item the arrows reached.");
        Assert.IsFalse(open);
        Assert.AreEqual("Pipeline (0 of 5)", tab, "The Pipeline shelf shows its Submitted manuscripts (none).");
    }

    // B2, H part. Ports BoardFilterTests.vb:93: a list opened and closed
    // with no choice renders nothing, whether it closes by Alt+Up, Esc, or
    // Enter after the arrows came back to the opening choice.
    [TestMethod]
    public void B2_AListClosedWithoutAChoiceSettlesOnTheBoxWithoutAStrayRender()
    {
        using DiskLibrarySession session = OpenExample();

        List<(string Close, int Renders, int Index)> results = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox box = board.Find<ComboBox>("StageFilter");
            box.Focus(NavigationMethod.Tab);
            var results = new List<(string, int, int)>();

            void Run(string name, Action steps)
            {
                int start = board.Board.RenderCount;
                board.Press(Key.Down, RawInputModifiers.Alt);
                Assert.IsTrue(box.IsDropDownOpen, name + ": the list opened.");
                steps();
                Assert.IsFalse(box.IsDropDownOpen, name + ": the list closed.");
                Dispatcher.UIThread.RunJobs();
                results.Add((name, board.Board.RenderCount - start, board.Board.SelectedStageIndex));
            }

            Run("Alt+Up", () => board.Press(Key.Up, RawInputModifiers.Alt));
            Run("Esc", () => board.Press(Key.Escape));
            Run("Down, Up, Enter", () =>
            {
                board.Press(Key.Down);
                board.Press(Key.Up);
                board.Press(Key.Enter);
            });
            return results;
        });

        foreach ((string close, int renders, int index) in results)
        {
            Assert.AreEqual(0, renders, close + ": a list closed on its opening choice renders nothing.");
            Assert.AreEqual(AllStages, index, close + ": the box still shows All stages.");
        }
    }

    // B3, H part. Ports BoardFilterTests.vb:143: Down on the closed box
    // renders at once, inside the key press, before any posted work runs;
    // Up goes back, again at once.
    [TestMethod]
    public void B3_AStepOnAClosedStageBoxRendersAtOnce()
    {
        using DiskLibrarySession session = OpenExample();

        (int down, int downIndex, int afterJobs, int up, int upIndex) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox box = board.Find<ComboBox>("StageFilter");
            box.Focus(NavigationMethod.Tab);
            int start = board.Board.RenderCount;

            // The press alone, no RunJobs: a posted flush could not have run.
            board.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            int down = board.Board.RenderCount - start;
            int downIndex = board.Board.SelectedStageIndex;
            board.Window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Dispatcher.UIThread.RunJobs();
            int afterJobs = board.Board.RenderCount - start;

            start = board.Board.RenderCount;
            board.Window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            int up = board.Board.RenderCount - start;
            board.Window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(box.IsDropDownOpen, "The list never opened.");
            return (down, downIndex, afterJobs, up, board.Board.SelectedStageIndex);
        });

        Assert.AreEqual(1, down, "Down on the closed box rendered inside the key press.");
        Assert.AreEqual(Idea, downIndex);
        Assert.AreEqual(1, afterJobs, "No flush rendered again.");
        Assert.AreEqual(1, up, "Up on the closed box rendered at once.");
        Assert.AreEqual(AllStages, upIndex);
    }

    // B4, H part. Ports BoardFilterTests.vb:171: the sort box renders once
    // when its list closes, and at once while closed.
    [TestMethod]
    public void B4_TheSortBoxRendersOnceWhenItsListClosesAndAtOnceWhenClosed()
    {
        using DiskLibrarySession session = OpenExample();

        (int whileOpen, int afterClose, int openIndex, int closedRenders, int closedIndex, bool clearEnabled) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox box = board.Find<ComboBox>("BoardSort");
            box.Focus(NavigationMethod.Tab);
            int start = board.Board.RenderCount;
            board.Press(Key.Down, RawInputModifiers.Alt);
            Assert.IsTrue(board.Board.IsSortListOpen, "Alt+Down opened the sort list.");
            board.Press(Key.Down);
            board.Press(Key.Down);
            int whileOpen = board.Board.RenderCount - start;
            board.Press(Key.Enter);
            int afterClose = board.Board.RenderCount - start;
            int openIndex = board.Board.SelectedSortIndex;

            start = board.Board.RenderCount;
            board.Window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            int closedRenders = board.Board.RenderCount - start;
            board.Window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            Dispatcher.UIThread.RunJobs();
            return (whileOpen, afterClose, openIndex, closedRenders, board.Board.SelectedSortIndex, board.Find<Button>("ClearFilters").IsEffectivelyEnabled);
        });

        Assert.AreEqual(0, whileOpen, "Nothing is rendered while the sort list is open.");
        Assert.AreEqual(1, afterClose, "The close rendered once.");
        Assert.AreEqual(2, openIndex, "Title Z-A, the item the arrows reached.");
        Assert.AreEqual(1, closedRenders, "Up on the closed sort box rendered at once.");
        Assert.AreEqual(1, closedIndex, "Title A-Z.");
        Assert.IsTrue(clearEnabled, "A sort other than Current order is a filter Clear can clear.");
    }

    // B7, H part. Esc in an open list keeps the item the arrows reached, and
    // the Board shows it, rendered once (the C3 checklist, r1 4.3), for both boxes.
    [TestMethod]
    public void B7_EscInAnOpenListShowsTheItemTheArrowsReached()
    {
        using DiskLibrarySession session = OpenExample();

        (int stageRenders, int stageIndex, string stageShown, int sortRenders, int sortIndex) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            ComboBox stage = board.Find<ComboBox>("StageFilter");
            stage.Focus(NavigationMethod.Tab);
            int start = board.Board.RenderCount;
            board.Press(Key.Down, RawInputModifiers.Alt);
            board.Press(Key.Down);
            board.Press(Key.Down);
            board.Press(Key.Escape);
            Assert.IsFalse(stage.IsDropDownOpen, "Esc closed the stage list.");
            int stageRenders = board.Board.RenderCount - start;
            int stageIndex = board.Board.SelectedStageIndex;
            string stageShown = (stage.SelectedItem as StageOptionViewModel)?.Text ?? string.Empty;

            ComboBox sort = board.Find<ComboBox>("BoardSort");
            sort.Focus(NavigationMethod.Tab);
            start = board.Board.RenderCount;
            board.Press(Key.Down, RawInputModifiers.Alt);
            board.Press(Key.Down);
            board.Press(Key.Escape);
            Assert.IsFalse(sort.IsDropDownOpen, "Esc closed the sort list.");
            return (stageRenders, stageIndex, stageShown, board.Board.RenderCount - start, board.Board.SelectedSortIndex);
        });

        Assert.AreEqual(1, stageRenders, "Esc rendered the stage reached once.");
        Assert.AreEqual(Draft, stageIndex, "The Board shows the stage the arrows reached.");
        Assert.AreEqual("Draft (3)", stageShown, "The box shows it too (two Pipeline drafts and the File Drawer's).");
        Assert.AreEqual(1, sortRenders, "Esc rendered the sort reached once.");
        Assert.AreEqual(1, sortIndex, "Title A-Z, the sort the arrow reached.");
    }

    // ---- Keyboard ----

    // K3. Ports BoardFillTests.vb:211: the arrow keys on the shelf tabs move
    // between them and switch the shelf; Tab enters only the selected tab.
    [TestMethod]
    public void K3_ArrowsOnTheShelfTabsSwitchTheShelf()
    {
        using DiskLibrarySession session = OpenExample();

        (int afterRight, bool publishedShown, bool pipelineHidden, int afterLeft, string focusedTab, KeyboardNavigationMode tabMode) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            TabStrip tabs = board.Find<TabStrip>("ShelfTabs");
            IReadOnlyList<TabStripItem> items = board.Tabs();
            items[0].Focus(NavigationMethod.Tab);
            board.Press(Key.Right);
            int afterRight = board.Board.SelectedShelfIndex;
            bool publishedShown = board.Shelf("PublishedShelf").IsEffectivelyVisible;
            bool pipelineHidden = !board.Shelf("PipelineShelf").IsEffectivelyVisible;
            board.Press(Key.Left);
            return (afterRight, publishedShown, pipelineHidden, board.Board.SelectedShelfIndex,
                    AutomationProperties.GetName(board.Focused() as Control ?? tabs) ?? string.Empty,
                    KeyboardNavigation.GetTabNavigation(tabs));
        });

        Assert.AreEqual(1, afterRight, "Right moves to the Published tab and selects it.");
        Assert.IsTrue(publishedShown, "The Published shelf shows.");
        Assert.IsTrue(pipelineHidden, "The Pipeline shelf is hidden.");
        Assert.AreEqual(0, afterLeft, "Left moves back to the Pipeline.");
        Assert.AreEqual("Pipeline (5)", focusedTab, "The keyboard is on the Pipeline tab.");
        Assert.AreEqual(KeyboardNavigationMode.Once, tabMode, "Tab enters the tabs once, as today's radio group.");
    }

    // K5. Ports DashboardShelfLayoutTests.vb:274
    // AttentionChipsTakeFocusAndFilterFromTheKeyboard: the chips are tab
    // stops after the search box (Add is disabled in the trial); Enter
    // applies a chip's filter and Space clears it again.
    [TestMethod]
    public void K5_AttentionChipsTakeFocusAndFilterFromTheKeyboard()
    {
        using DiskLibrarySession session = OpenExample();

        (string firstStop, bool active, string tabAfterEnter, string shownAfterEnter, string helpAfterEnter, bool cleared, string tabAfterSpace) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            board.Find<TextBox>("SearchBox").Focus(NavigationMethod.Tab);
            board.Press(Key.Tab);
            string firstStop = AutomationProperties.GetAutomationId(board.Focused() as Control ?? board.Window) ?? string.Empty;

            Button chip = board.Chip(AttentionFilter.MissingTargetJournal);
            chip.Focus(NavigationMethod.Tab);
            board.Press(Key.Enter);
            bool active = chip.Classes.Contains("active");
            string tabAfterEnter = board.Board.Shelves[0].TabText;
            string shownAfterEnter = BoardWindow.ShownText(board.Tabs()[0]);
            string help = AutomationProperties.GetHelpText(chip) ?? string.Empty;
            board.Press(Key.Space);
            return (firstStop, active, tabAfterEnter, shownAfterEnter, help, !chip.Classes.Contains("active"), board.Board.Shelves[0].TabText);
        });

        Assert.AreEqual("Chip.RevisionDueSoon", firstStop, "Tab from the search box reaches the first chip shown (the overdue chip has no count).");
        Assert.IsTrue(active, "Enter applies the chip's filter.");
        Assert.AreEqual("Pipeline (1 of 5)", tabAfterEnter, "Only the manuscript with no target journal shows.");
        Assert.AreEqual("Pipeline 1 of 5", shownAfterEnter, "The tab draws the name and the bare count.");
        Assert.AreEqual("Filter applied. Press to clear it.", helpAfterEnter);
        Assert.IsTrue(cleared, "Space clears it again.");
        Assert.AreEqual("Pipeline (5)", tabAfterSpace);
    }

    // K6, the Ctrl+F part (Form1.vb ProcessCmdKey, :2553-2560): from anywhere
    // in the window, Ctrl+F puts the keyboard in the search box with its text
    // selected.
    [TestMethod]
    public void K6_CtrlFFocusesTheSearchWithItsTextSelected()
    {
        using DiskLibrarySession session = OpenExample();
        int shown = 0;

        List<(string From, bool Focused, string Selected)> results = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            board.Shell.BoardShown += (_, _) => shown++;
            TextBox search = board.Find<TextBox>("SearchBox");
            board.Board.SearchText = "memory";
            Dispatcher.UIThread.RunJobs();
            var results = new List<(string, bool, string)>();
            var starts = new (string Name, Control Control)[]
            {
                ("a chip", board.Chip(AttentionFilter.RevisionDueSoon)),
                ("the stage box", board.Find<ComboBox>("StageFilter")),
                ("the rail", board.Window.FindControl<ItemsControl>("RailPages")!.GetVisualDescendants().OfType<RadioButton>().First()),

                // Focus that arrives by Tab selects a TextBox's text by itself; from inside
                // the box, with the caret in the text, only the Ctrl+F handler selects it.
                ("the search box itself", search),
            };
            foreach ((string name, Control control) in starts)
            {
                search.ClearSelection();
                if (ReferenceEquals(control, search))
                {
                    search.Focus(NavigationMethod.Pointer);
                    search.ClearSelection();
                    search.CaretIndex = 3;
                }
                else
                {
                    control.Focus(NavigationMethod.Tab);
                }

                Dispatcher.UIThread.RunJobs();
                board.Press(Key.F, RawInputModifiers.Control);
                results.Add((name, ReferenceEquals(board.Focused(), search), search.SelectedText ?? string.Empty));
            }

            return results;
        });

        foreach ((string from, bool focused, string selected) in results)
        {
            Assert.IsTrue(focused, $"Ctrl+F from {from} put the keyboard in the search box.");
            Assert.AreEqual("memory", selected, $"Ctrl+F from {from} selected the search text.");
        }

        Assert.AreEqual(results.Count, shown, "Ctrl+F shows the Board each time, as Ctrl+1 does.");
    }

    // K7. Esc on the Board does nothing (r1 2.7: Form1 has no Esc case and no
    // CancelButton): the search, the boxes, the chip, the shelf, the focus and
    // the notice stay as they were, and nothing renders.
    [TestMethod]
    public void K7_EscOnTheBoardDoesNothing()
    {
        using DiskLibrarySession session = OpenExample();

        List<string> changes = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            BoardViewModel vm = board.Board;
            vm.SearchText = "memory";
            vm.SelectedSortIndex = 1;
            vm.ToggleChipCommand.Execute(vm.Chips.Single(chip => chip.Filter == AttentionFilter.LongReview));
            vm.SelectedShelfIndex = 0;
            Dispatcher.UIThread.RunJobs();

            var changes = new List<string>();
            var starts = new (string Name, Control Control)[]
            {
                ("the search box", board.Find<TextBox>("SearchBox")),
                ("a chip", board.Chip(AttentionFilter.LongReview)),
                ("the shelf tab", board.Tabs()[0]),
                ("the closed stage box", board.Find<ComboBox>("StageFilter")),
                ("View in Deadlines", board.Find<Button>("ViewDeadlines")),
            };
            foreach ((string name, Control control) in starts)
            {
                control.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                string before = board.State();
                object? focused = board.Focused();
                board.Press(Key.Escape);
                string after = board.State();
                if (before != after)
                {
                    changes.Add($"From {name}: {before} became {after}");
                }

                if (!ReferenceEquals(focused, board.Focused()))
                {
                    changes.Add($"From {name}: the focus moved to {board.Focused()}");
                }
            }

            return changes;
        });

        Assert.IsEmpty(changes, string.Join(Environment.NewLine, changes));
    }

    // ---- Names, notices, welcome ----

    // H5. Ports DialogPolishTests.vb:1642 and widens it to every name of d1
    // 4.3 that this step builds (r1 4.1): accessible names, descriptions,
    // tooltips and automation IDs, and the heading level of "Board".
    [TestMethod]
    public void H5_EveryBoardControlHasTodaysName()
    {
        using DiskLibrarySession session = OpenExample();

        Dictionary<string, string> facts = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var facts = new Dictionary<string, string>();
            void Add(string key, Control control)
            {
                facts[key + ".Name"] = AutomationProperties.GetName(control) ?? string.Empty;
                facts[key + ".Help"] = AutomationProperties.GetHelpText(control) ?? string.Empty;
                facts[key + ".Id"] = AutomationProperties.GetAutomationId(control) ?? string.Empty;
                facts[key + ".Tip"] = ToolTip.GetTip(control) as string ?? string.Empty;
                facts[key + ".Peer"] = ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? string.Empty;
            }

            Add("Search", board.Find<TextBox>("SearchBox"));
            Add("Add", board.Find<Button>("AddButton"));
            Add("Deadlines", board.Find<Button>("ViewDeadlines"));
            Add("Stage", board.Find<ComboBox>("StageFilter"));
            Add("Sort", board.Find<ComboBox>("BoardSort"));
            Add("Clear", board.Find<Button>("ClearFilters"));
            foreach (ChipViewModel chip in board.Board.Chips.Where(chip => chip.IsVisible))
            {
                Button button = board.Chip(chip.Filter);
                string key = "Chip." + chip.Filter;
                Add(key, button);
                facts[key + ".Shown"] = button.Content as string ?? string.Empty;
                facts[key + ".Tone"] = button.Classes.Contains("warning") ? "warning" : button.Classes.Contains("danger") ? "danger" : "neutral";
                facts[key + ".Fill"] = BoardWindow.TokenOf(button, button.Background, "WarningMutedBackgroundBrush", "DangerMutedBackgroundBrush", "CardBackgroundBrush");
            }

            IReadOnlyList<TabStripItem> tabs = board.Tabs();
            for (int index = 0; index < tabs.Count; index++)
            {
                Add("Tab" + index, tabs[index]);
                facts["Tab" + index + ".Shown"] = BoardWindow.ShownText(tabs[index]);
            }

            TextBlock title = board.Find<TextBlock>("PageTitle");
            facts["Title.Text"] = title.Text ?? string.Empty;
            facts["Title.Heading"] = AutomationProperties.GetHeadingLevel(title).ToString(System.Globalization.CultureInfo.InvariantCulture);
            facts["Deadlines.Type"] = AutomationProperties.GetControlTypeOverride(board.Find<Button>("ViewDeadlines"))?.ToString() ?? string.Empty;
            facts["Add.Enabled"] = board.Find<Button>("AddButton").IsEffectivelyEnabled.ToString();
            facts["Add.TipWhenOff"] = ToolTip.GetShowOnDisabled(board.Find<Button>("AddButton")).ToString();
            facts["Search.Placeholder"] = board.Find<TextBox>("SearchBox").PlaceholderText ?? string.Empty;

            // The placeholder as drawn: solid MutedText, as today's, not dimmed by Fluent's own opacity.
            TextBlock placeholder = board.Find<TextBox>("SearchBox").GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Name == "PART_Placeholder");
            facts["Search.PlaceholderOpacity"] = placeholder.Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            facts["Search.PlaceholderInk"] = BoardWindow.TokenOf(placeholder, placeholder.Foreground, "MutedTextBrush");
            return facts;
        });

        void Expect(string key, string expected) => Assert.AreEqual(expected, facts[key], key);

        Expect("Search.Name", "Search the board (Ctrl+F)");
        Expect("Search.Peer", "Search the board (Ctrl+F)");
        Expect("Search.Tip", "Search the board (Ctrl+F)");
        Expect("Search.Id", "BoardSearch");
        Expect("Search.Placeholder", "Search titles, journals, authors");
        Expect("Search.PlaceholderOpacity", "1");
        Expect("Search.PlaceholderInk", "MutedTextBrush");
        Expect("Add.Name", "Add Manuscript");
        Expect("Add.Tip", RailItemViewModel.NotInTrial);
        Expect("Add.Help", RailItemViewModel.NotInTrial);
        Expect("Add.Enabled", "False");
        Expect("Add.TipWhenOff", "True");
        Expect("Deadlines.Name", "View in Deadlines");
        Expect("Deadlines.Help", "Opens the Deadlines page (Ctrl+4).");
        Expect("Deadlines.Tip", "Open Deadlines (Ctrl+4)");
        Expect("Deadlines.Type", nameof(AutomationControlType.Hyperlink));
        Expect("Stage.Name", "Filter by stage");
        Expect("Stage.Id", "StageFilter");
        Expect("Sort.Name", "Sort the board");
        Expect("Sort.Id", "BoardSort");
        Expect("Clear.Name", "Clear search and filters");
        Expect("Clear.Id", "ClearFilters");
        Expect("Title.Text", "Board");
        Expect("Title.Heading", "1");

        // The example's four chips (r1 1.7), each named and shown by its text, in
        // today's tone (Form1.vb:959-980: due soon and long review Warning, missing
        // journal Neutral, recent rejection Danger) and that tone's fill.
        foreach ((AttentionFilter filter, string text, string tone, string fill) in new[]
        {
            (AttentionFilter.RevisionDueSoon, "1 revision due soon", "warning", "WarningMutedBackgroundBrush"),
            (AttentionFilter.LongReview, "1 waiting 90+ days", "warning", "WarningMutedBackgroundBrush"),
            (AttentionFilter.MissingTargetJournal, "1 no target journal", "neutral", "CardBackgroundBrush"),
            (AttentionFilter.RecentRejection, "1 rejected in last 30 days", "danger", "DangerMutedBackgroundBrush"),
        })
        {
            Expect($"Chip.{filter}.Name", text);
            Expect($"Chip.{filter}.Peer", text);
            Expect($"Chip.{filter}.Shown", text);
            Expect($"Chip.{filter}.Help", "Filter the board");
            Expect($"Chip.{filter}.Id", "Chip." + filter);
            Expect($"Chip.{filter}.Tone", tone);
            Expect($"Chip.{filter}.Fill", fill);
        }

        Assert.IsFalse(facts.ContainsKey("Chip.OverdueRevision.Name"), "The example has no overdue revision, so its chip is hidden.");

        // The tabs, whole, as assistive technology reads them.
        Expect("Tab0.Name", "Pipeline (5)");
        Expect("Tab0.Peer", "Pipeline (5)");
        Expect("Tab0.Id", "Shelf.Pipeline");
        Expect("Tab1.Name", "Published (1)");
        Expect("Tab1.Id", "Shelf.Published");
        Expect("Tab2.Name", "File Drawer (1)");
        Expect("Tab2.Id", "Shelf.FileDrawer");

        // The tabs as drawn: the name, then the bare count (ShelfTabButton.vb:35-54).
        Expect("Tab0.Shown", "Pipeline 5");
        Expect("Tab1.Shown", "Published 1");
        Expect("Tab2.Shown", "File Drawer 1");
    }

    // d1 1.2: View in Deadlines gives a notice in the trial; Add is disabled
    // and a press on it does nothing.
    [TestMethod]
    public void ViewInDeadlinesGivesANoticeAndAddDoesNothing()
    {
        using DiskLibrarySession session = OpenExample();

        (string beforeAdd, string afterAdd, string afterDeadlines, string shown) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            string beforeAdd = board.Shell.Notice;
            board.Click(board.Find<Button>("AddButton"));
            string afterAdd = board.Shell.Notice;
            board.Find<Button>("ViewDeadlines").Focus(NavigationMethod.Tab);
            board.Press(Key.Enter);
            return (beforeAdd, afterAdd, board.Shell.Notice, board.Window.FindControl<TextBlock>("NoticeText")?.Text ?? string.Empty);
        });

        Assert.AreEqual("Loaded 7 manuscripts from local storage.", beforeAdd);
        Assert.AreEqual(beforeAdd, afterAdd, "A press on the disabled Add gives no notice.");
        Assert.AreEqual(BoardViewModel.DeadlinesNotice, afterDeadlines);
        Assert.AreEqual(BoardViewModel.DeadlinesNotice, shown, "The notice strip shows it.");
    }

    // The search box (r1 2.4) through the real TextBox: typing reaches the
    // view model at once, each key restarts the app's 200 ms wait
    // (Form1.vb BoardSearchTextChanged), and the Board renders once, the
    // wait after the last key.
    [TestMethod]
    public void TypingInTheSearchBoxRendersOnceAfterTheWait()
    {
        using DiskLibrarySession session = OpenExample();
        var gap = TimeSpan.FromMilliseconds(120);

        (string text, int beforeLastKey, long lastKeyAt, int afterWait, long waited, string tab) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            board.Find<TextBox>("SearchBox").Focus(NavigationMethod.Tab);
            int start = board.Board.RenderCount;
            var clock = Stopwatch.StartNew();
            board.Window.KeyTextInput("m");
            Dispatcher.UIThread.RunJobs();
            BoardWindow.WaitFor(() => false, gap);
            board.Window.KeyTextInput("e");
            Dispatcher.UIThread.RunJobs();
            BoardWindow.WaitFor(() => false, gap);

            // At least 240 ms after the first key: a wait that did not restart would have rendered by now.
            int beforeLastKey = board.Board.RenderCount - start;
            long lastKeyAt = clock.ElapsedMilliseconds;
            var sinceLastKey = Stopwatch.StartNew();
            board.Window.KeyTextInput("m");
            Dispatcher.UIThread.RunJobs();
            BoardWindow.WaitFor(() => board.Board.RenderCount > start, TimeSpan.FromSeconds(10));
            long waited = sinceLastKey.ElapsedMilliseconds;

            // A while longer, so a second render would be seen.
            int after = board.Board.RenderCount;
            BoardWindow.WaitFor(() => board.Board.RenderCount > after, TimeSpan.FromMilliseconds(400));
            return (board.Board.SearchText, beforeLastKey, lastKeyAt, board.Board.RenderCount - start, waited, board.Board.Shelves[0].TabText);
        });

        TestContext.WriteLine($"Last key {lastKeyAt} ms after the first; the render came {waited} ms after the last key.");
        Assert.AreEqual("mem", text, "The typed text reached the view model.");
        Assert.IsGreaterThanOrEqualTo(2 * (long)gap.TotalMilliseconds, lastKeyAt);
        Assert.AreEqual(0, beforeLastKey, $"No render {lastKeyAt} ms after the first key: each key restarts the wait.");
        Assert.AreEqual(1, afterWait, "One render after the wait, for three keys.");
        Assert.IsGreaterThanOrEqualTo(190L, waited, $"The render waited {waited} ms after the last key; the wait is {BoardViewModel.SearchDelay.TotalMilliseconds} ms.");
        Assert.AreEqual("Pipeline (1 of 5)", tab, "\"mem\" matches the sleep and memory manuscript only.");
    }

    // The shelves' empty messages (r1 1.2; Form1.vb:2806-2878), shown where
    // a shelf's cards will be: here, filters that hide everything, then the
    // Clear button, which brings back an empty message only where it belongs.
    [TestMethod]
    public void AShelfThatShowsNothingShowsItsEmptyMessage()
    {
        using DiskLibrarySession session = OpenExample();

        (string pipeline, bool pipelineShown, string published, bool clearOn, bool messageAfterClear, bool clearOff) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            board.Board.SearchText = "no manuscript has this";
            board.Board.Render();
            Dispatcher.UIThread.RunJobs();
            TextBlock message = board.EmptyMessage("PipelineShelf");
            string pipeline = message.Text ?? string.Empty;
            bool shown = message.IsEffectivelyVisible;
            bool clearOn = board.Find<Button>("ClearFilters").IsEffectivelyEnabled;

            board.Board.SelectedShelfIndex = 1;
            Dispatcher.UIThread.RunJobs();
            string published = board.EmptyMessage("PublishedShelf").IsEffectivelyVisible ? board.EmptyMessage("PublishedShelf").Text ?? string.Empty : string.Empty;

            board.Board.SelectedShelfIndex = 0;
            board.Find<Button>("ClearFilters").Focus(NavigationMethod.Tab);
            board.Press(Key.Space);
            return (pipeline, shown, published, clearOn, board.EmptyMessage("PipelineShelf").IsVisible, board.Find<Button>("ClearFilters").IsEffectivelyEnabled);
        });

        Assert.AreEqual("No Pipeline manuscripts match the current search or filter.", pipeline);
        Assert.IsTrue(pipelineShown, "The message shows where the cards would be.");
        Assert.AreEqual("No Published manuscripts match the current search or filter.", published);
        Assert.IsTrue(clearOn, "Clear is on while a search is on.");
        Assert.IsFalse(messageAfterClear, "Clear brought the Pipeline's manuscripts back.");
        Assert.IsFalse(clearOff, "Clear turns itself off.");
    }

    // H1. Ports ManuscriptPageTests.vb:1020 and :1075: an empty library opens
    // on the welcome, not on the Board, until the first manuscript; the
    // welcome links to the Quick Start; the example link is left out of the
    // example window (Form1.Welcome.vb:215). In the trial the welcome's
    // buttons give notices and Add is disabled (d1 1.2).
    [TestMethod]
    public void H1_AnEmptyLibraryShowsTheWelcomeUntilTheFirstManuscript()
    {
        var session = new MemorySession([], Papers.Today);

        Dictionary<string, string> facts = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var facts = new Dictionary<string, string>
            {
                ["Welcome"] = board.Find<WelcomeView>("Welcome").IsEffectivelyVisible.ToString(),
                ["Board"] = board.Find<Grid>("BoardBody").IsEffectivelyVisible.ToString(),
            };
            WelcomeView welcome = board.Find<WelcomeView>("Welcome");
            TextBlock title = welcome.FindControl<TextBlock>("WelcomeTitle")!;
            Button quickStart = welcome.FindControl<Button>("QuickStart")!;
            Button add = welcome.FindControl<Button>("WelcomeAdd")!;
            Control ways = welcome.FindControl<Control>("WaysToStart")!;
            facts["Title"] = title.Text ?? string.Empty;
            facts["Heading"] = AutomationProperties.GetHeadingLevel(title).ToString(System.Globalization.CultureInfo.InvariantCulture);
            facts["QuickStart"] = quickStart.IsEffectivelyVisible + " " + (quickStart.Content as string);
            facts["QuickStartHelp"] = AutomationProperties.GetHelpText(quickStart) ?? string.Empty;
            facts["ExampleLine"] = welcome.FindControl<Control>("ExampleLine")!.IsEffectivelyVisible.ToString();
            facts["Add"] = add.IsEffectivelyEnabled.ToString();
            facts["Ways"] = AutomationProperties.GetName(ways) ?? string.Empty;
            facts["WayHelp"] = string.Join(" | ", ways.GetVisualDescendants().OfType<Button>().Select(way => AutomationProperties.GetHelpText(way)));
            facts["Privacy"] = welcome.FindControl<Border>("PrivacyNote")!.IsEffectivelyVisible.ToString();

            quickStart.Focus(NavigationMethod.Tab);
            board.Press(Key.Enter);
            facts["QuickStartNotice"] = board.Shell.Notice;
            board.Click(welcome.FindControl<Button>("WelcomeImport")!);
            facts["ImportNotice"] = board.Shell.Notice;

            // The first manuscript: the Board replaces the welcome.
            session.Library.Add(Papers.Paper("First paper"));
            session.RaiseChanged();
            Dispatcher.UIThread.RunJobs();
            facts["WelcomeAfter"] = board.Find<WelcomeView>("Welcome").IsEffectivelyVisible.ToString();
            facts["BoardAfter"] = board.Find<Grid>("BoardBody").IsEffectivelyVisible.ToString();
            return facts;
        });

        void Expect(string key, string expected) => Assert.AreEqual(expected, facts[key], key);

        Expect("Welcome", "True");
        Expect("Board", "False");
        Expect("Title", "Your manuscript library is empty");
        Expect("Heading", "1");
        Expect("QuickStart", "True Read the Quick Start");
        Expect("QuickStartHelp", "Opens Help at the Quick Start.");
        Expect("ExampleLine", "False");
        Expect("Add", "False");
        Expect("Ways", "Ways to start");
        Expect("WayHelp", "Start by importing: Paste a title page | Start by importing: ORCID works | Start by importing: BibTeX or RIS | Start by importing: Spreadsheet");
        Expect("Privacy", "True");
        Expect("QuickStartNotice", "The User Guide" + BoardViewModel.NotInTrialSuffix);
        Expect("ImportNotice", "Import & Export" + BoardViewModel.NotInTrialSuffix);
        Expect("WelcomeAfter", "False");
        Expect("BoardAfter", "True");
    }

    // d1 4.1, narrow window: below the width both rows need, the header's
    // tools wrap under the title and the boxes move above the tabs, as
    // today's SplitBar does; at 1280 they sit side by side.
    [TestMethod]
    public void ANarrowWindowStacksTheHeaderAndTheShelfBar()
    {
        using DiskLibrarySession session = OpenExample();

        (bool wideNarrow, int wideToolsRow, bool narrow, int toolsRow, int tabsRow, int headerToolsRow, double tabsTop, double toolsBottom) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            bool wide = board.View.IsNarrow;
            int wideTools = Grid.GetRow(board.Find<StackPanel>("ShelfTools"));
            board.Window.Width = 760;
            Dispatcher.UIThread.RunJobs();
            board.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            StackPanel tools = board.Find<StackPanel>("ShelfTools");
            TabStrip tabs = board.Find<TabStrip>("ShelfTabs");
            Point tabsTop = tabs.TranslatePoint(default, board.Window) ?? default;
            Point toolsBottom = tools.TranslatePoint(new Point(0, tools.Bounds.Height), board.Window) ?? default;

            // Every chip shown lies inside the Board: the chips wrap rather than run off its edge.
            Grid body = board.Find<Grid>("BoardBody");
            double bodyRight = (body.TranslatePoint(new Point(body.Bounds.Width, 0), board.Window) ?? default).X;
            double chipsRight = board.Board.Chips.Where(chip => chip.IsVisible)
                .Select(chip => board.Chip(chip.Filter))
                .Max(button => (button.TranslatePoint(new Point(button.Bounds.Width, 0), board.Window) ?? default).X);
            Assert.IsLessThanOrEqualTo(bodyRight, chipsRight, "A chip runs past the Board's edge.");
            return (wide, wideTools, board.View.IsNarrow, Grid.GetRow(tools), Grid.GetRow(tabs), Grid.GetRow(board.Find<StackPanel>("HeaderTools")), tabsTop.Y, toolsBottom.Y);
        });

        Assert.IsFalse(wideNarrow, "At 1280 px the Board is not narrow.");
        Assert.AreEqual(0, wideToolsRow);
        Assert.IsTrue(narrow, "At 760 px the Board is narrow.");
        Assert.AreEqual(0, toolsRow, "The boxes go above the tabs.");
        Assert.AreEqual(1, tabsRow);
        Assert.AreEqual(1, headerToolsRow, "The search and Add wrap under the title.");
        Assert.IsGreaterThanOrEqualTo(toolsBottom, tabsTop, "The tabs start below the boxes.");
    }

    // ---- Pictures ----

    [TestMethod]
    public void TheBoardIsSavedInLight() => SaveExample(ThemeVariant.Light, "board-light");

    [TestMethod]
    public void TheBoardIsSavedInDark() => SaveExample(ThemeVariant.Dark, "board-dark");

    // The 500 library (LargeLibraryFixture) with one chip on: the first chip
    // with a count, in the order the bar shows them.
    [TestMethod]
    public void TheBoardOnFiveHundredWithAChipOnIsSaved()
    {
        LargeLibraryFixture.Create(_root, 500);
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            Path.Combine(_root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(500, session.Manuscripts);

        (string path, string emptyPath, string chip, string tabs, string empty) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            // Pressed through its command, so the picture shows the chip on, not the keyboard's ring.
            ChipViewModel first = board.Board.Chips.First(candidate => candidate.Count > 0);
            board.Board.ToggleChipCommand.Execute(first);
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(first.IsActive, "The chip is on.");
            string tabs = string.Join(" | ", board.Board.Shelves.Select(shelf => shelf.TabText));
            string path = Pictures.Save(board.Window, "board-500-chip-light", TestContext);

            // The Published shelf under the same chip, which hides all of it: the
            // shelf area shows only its empty message, as this step builds it.
            board.Board.SelectedShelfIndex = 1;
            Dispatcher.UIThread.RunJobs();
            TextBlock message = board.EmptyMessage("PublishedShelf");
            Assert.IsTrue(message.IsEffectivelyVisible, "The Published shelf shows its empty message.");
            return (path, Pictures.Save(board.Window, "board-500-empty-light", TestContext), first.Text, tabs, message.Text ?? string.Empty);
        });

        TestContext.WriteLine($"Chip on: {chip}; tabs: {tabs}; Published shows \"{empty}\"");
        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
        Assert.IsTrue(File.Exists(emptyPath), $"No picture at {emptyPath}.");
    }

    // The narrow layout, for people to look at.
    [TestMethod]
    public void TheNarrowBoardIsSaved()
    {
        using DiskLibrarySession session = OpenExample();
        string path = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light, width: 760);
            return Pictures.Save(board.Window, "board-narrow-light", TestContext);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // The welcome, for people to look at.
    [TestMethod]
    public void TheWelcomeIsSaved()
    {
        var session = new MemorySession([], Papers.Today);
        string path = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            return Pictures.Save(board.Window, "welcome-light", TestContext);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    private void SaveExample(ThemeVariant variant, string name)
    {
        using DiskLibrarySession session = OpenExample();
        string path = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, variant);
            return Pictures.Save(board.Window, name, TestContext);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // The example written to this test's folder and read through the trial's
    // session on the test thread, before any window or view model is made.
    private DiskLibrarySession OpenExample()
    {
        LargeLibraryFixture.WriteExample(_root);
        var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            _root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);
        return session;
    }
}

// The main window over a loaded library, with the Board rendered once, and
// the lookups and key presses the Board's view tests share. Made and closed
// inside Headless.Run.
internal sealed class BoardWindow : IDisposable
{
    private BoardWindow(MainWindow window, ShellViewModel shell, BoardView view)
    {
        Window = window;
        Shell = shell;
        View = view;
    }

    public MainWindow Window { get; }

    public ShellViewModel Shell { get; }

    public BoardViewModel Board => Shell.Board;

    public BoardView View { get; }

    // The app's own shell (App.CreateShell: the DispatcherTimer search wait
    // and the posted flush), over a library loaded already: the Board renders
    // once here, as the load's LibraryChanged would.
    public static BoardWindow Show(ILibrarySession session, ThemeVariant? variant = null, double width = 1280, double height = 800)
    {
        ShellViewModel shell = App.CreateShell(session);
        shell.Board.Render();
        if (!string.IsNullOrEmpty(session.StartupNotice))
        {
            ((IInteractions)shell).Notice(session.StartupNotice);
        }

        var window = new MainWindow { DataContext = shell, Width = width, Height = height };
        if (variant is not null)
        {
            window.RequestedThemeVariant = variant;
        }

        window.Show();
        Dispatcher.UIThread.RunJobs();
        BoardView view = window.FindControl<BoardView>("BoardView")
            ?? throw new AssertFailedException("The window has no Board view.");
        return new BoardWindow(window, shell, view);
    }

    // Runs the interface thread's work until the condition holds or the
    // limit passes; returns whether it held.
    public static bool WaitFor(Func<bool> condition, TimeSpan limit)
    {
        Dispatcher dispatcher = Dispatcher.UIThread;
        var frame = new DispatcherFrame();
        var clock = Stopwatch.StartNew();
        var poll = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background, dispatcher, (_, _) =>
        {
            if (condition() || clock.Elapsed >= limit)
            {
                frame.Continue = false;
            }
        });
        poll.Start();
        try
        {
            if (!condition())
            {
                dispatcher.PushFrame(frame);
            }
        }
        finally
        {
            poll.Stop();
        }

        return condition();
    }

    public T Find<T>(string name)
        where T : Control =>
        View.FindControl<T>(name) ?? throw new AssertFailedException($"The Board has no {typeof(T).Name} named {name}.");

    public Button Chip(AttentionFilter filter) =>
        Find<ItemsControl>("Chips").GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == "Chip." + filter);

    public IReadOnlyList<TabStripItem> Tabs() =>
        Find<TabStrip>("ShelfTabs").GetVisualDescendants().OfType<TabStripItem>().ToList();

    public ShelfView Shelf(string name) => Find<ShelfView>(name);

    // The text a control draws, its visible text blocks in order, joined by a space.
    public static string ShownText(Control control) =>
        string.Join(" ", control.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text));

    // Which of the named brush tokens a brush shows, by colour, in the control's theme.
    public static string TokenOf(Control control, IBrush? brush, params string[] tokens)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return $"not a solid brush ({brush})";
        }

        foreach (string token in tokens)
        {
            if (control.TryFindResource(token, control.ActualThemeVariant, out object? found)
                && found is ISolidColorBrush candidate
                && candidate.Color == solid.Color)
            {
                return token;
            }
        }

        return $"none of the tokens ({solid.Color})";
    }

    public TextBlock EmptyMessage(string shelf) =>
        Shelf(shelf).FindControl<TextBlock>("EmptyMessage") ?? throw new AssertFailedException($"{shelf} has no empty message.");

    public object? Focused() => FocusWalk.Focused(Window);

    // What a key on the Board could change, in one line.
    public string State() =>
        $"search \"{Board.SearchText}\", stage {Board.SelectedStageIndex}, sort {Board.SelectedSortIndex}, chip {Board.ActiveFilter}, " +
        $"shelf {Board.SelectedShelfIndex}, stage list {Board.IsStageListOpen}, sort list {Board.IsSortListOpen}, renders {Board.RenderCount}, notice \"{Shell.Notice}\"";

    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None) => Keyboard.Press(Window, key, modifiers);

    public void Click(Control control)
    {
        Point centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
            ?? throw new AssertFailedException($"{control} is not in the window.");
        Window.MouseMove(centre, RawInputModifiers.None);
        Window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose() => Window.Close();
}
