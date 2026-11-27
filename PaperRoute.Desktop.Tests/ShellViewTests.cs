using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Tests.Input;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The shell view (d1 8, step 7): the window, the example banner, the rail stub,
// the notice strip, Ctrl+1 and the F1 notice. Each test builds the example in
// a temporary folder of its own (never through TrialStartup, d1 6.1), under
// the assembly's storage guard.
[TestClass]
public sealed class ShellViewTests
{
    private static readonly string[] s_pages = ["Board", "Library", "Journals", "Deadlines", "Insights", "Import & Export"];
    private static readonly string[] s_commands = ["Online", "Settings", "Help"];

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialShell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        LargeLibraryFixture.WriteExample(_root);
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
    }

    // H4 (d1 6.2): the notice strip is a Polite live region, as today's status
    // line is (Form1.vb:42-43), on the element and on its automation peer,
    // and it shows the shell's notice as it changes.
    [TestMethod]
    public void H4_TheNoticeStripIsAPoliteLiveRegion()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);

        (AutomationLiveSetting setting, AutomationLiveSetting peerSetting, string id, string before, string after, bool visible) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                TextBlock notice = window.FindControl<TextBlock>("NoticeText")
                    ?? throw new AssertFailedException("The window has no notice text.");
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(notice);
                string first = notice.Text ?? string.Empty;
                ((IInteractions)shell).Notice("Loaded 7 manuscripts from local storage.");
                Dispatcher.UIThread.RunJobs();
                return (AutomationProperties.GetLiveSetting(notice), peer.GetLiveSetting(), peer.GetAutomationId() ?? string.Empty,
                        first, notice.Text ?? string.Empty, notice.IsEffectivelyVisible && notice.Bounds.Height > 0);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(AutomationLiveSetting.Polite, setting, "The notice strip's LiveSetting.");
        Assert.AreEqual(AutomationLiveSetting.Polite, peerSetting, "The live setting its automation peer reports.");
        Assert.AreEqual("Notice", id);
        Assert.AreEqual(ShellViewModel.LoadingText, before);
        Assert.AreEqual("Loaded 7 manuscripts from local storage.", after);
        Assert.IsTrue(visible, "The notice strip is not shown.");
    }

    // K6, the Ctrl+1 part (d1 4.5): Ctrl+1 and Ctrl+NumPad1 show the Board
    // (Form1.vb:2562-2565). The Board is the only page, so the rail keeps it
    // checked and nothing else changes.
    [TestMethod]
    public void K6_CtrlOneShowsTheBoard()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        int shown = 0;
        shell.BoardShown += (_, _) => shown++;

        (int afterDigit, int afterNumPad, bool boardChecked, string notice) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                RadioButton board = RailPages(window)[0];
                board.Focus();
                Keyboard.Press(window, Key.D1, RawInputModifiers.Control);
                int digit = shown;
                Keyboard.Press(window, Key.NumPad1, RawInputModifiers.Control);
                return (digit, shown, board.IsChecked == true, shell.Notice);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(1, afterDigit, "Ctrl+1 did not show the Board.");
        Assert.AreEqual(2, afterNumPad, "Ctrl+NumPad1 did not show the Board.");
        Assert.IsTrue(boardChecked, "The rail's Board item is not checked.");
        Assert.AreEqual(ShellViewModel.LoadingText, notice, "Ctrl+1 gives no notice.");
        Assert.IsTrue(shell.BoardItem.IsSelected);
        Assert.IsFalse(shell.RailPages.Skip(1).Any(page => page.IsSelected), "Only the Board is selected.");
    }

    // d1 1.2: F1 opens the User Guide today; in the trial it gives a notice.
    [TestMethod]
    public void F1GivesANotice()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);

        (string notice, string shown) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                RailPages(window)[0].Focus();
                Keyboard.Press(window, Key.F1);
                TextBlock text = window.FindControl<TextBlock>("NoticeText")
                    ?? throw new AssertFailedException("The window has no notice text.");
                return (shell.Notice, text.Text ?? string.Empty);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(ShellViewModel.HelpNotice, notice);
        Assert.AreEqual(ShellViewModel.HelpNotice, shown, "The notice strip shows the notice.");
    }

    // d1 1.2: Ctrl+2 to Ctrl+6 and Alt+Left/Right open pages and move through
    // history today; in the trial they do nothing.
    [TestMethod]
    public void TheOtherPageKeysDoNothingInTheTrial()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        int shown = 0;
        shell.BoardShown += (_, _) => shown++;

        bool boardChecked = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                RadioButton board = RailPages(window)[0];
                board.Focus();
                foreach (Key key in new[] { Key.D2, Key.D3, Key.D4, Key.D5, Key.D6 })
                {
                    Keyboard.Press(window, key, RawInputModifiers.Control);
                }

                Keyboard.Press(window, Key.Left, RawInputModifiers.Alt);
                Keyboard.Press(window, Key.Right, RawInputModifiers.Alt);
                return board.IsChecked == true;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(0, shown, "A page key other than Ctrl+1 showed the Board.");
        Assert.IsTrue(boardChecked);
        Assert.AreEqual(ShellViewModel.LoadingText, shell.Notice, "A page key gave a notice.");
        Assert.IsTrue(shell.BoardItem.IsSelected);
    }

    // d1 1.2: the rail shows today's six pages and three commands
    // (Form1.Shell.vb:230-237, 273-297). The Board is checked and live; every
    // other item is disabled and says it is not in the trial.
    [TestMethod]
    public void TheRailShowsTodaysDestinationsWithOnlyTheBoardLive()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);

        (List<RailFacts> pages, List<RailFacts> commands, string landmark, AutomationLandmarkType? landmarkType) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                ItemsControl list = window.FindControl<ItemsControl>("RailPages")
                    ?? throw new AssertFailedException("The window has no rail pages.");
                return (RailPages(window).Select(FactsOf).ToList(), RailCommands(window).Select(FactsOf).ToList(),
                        AutomationProperties.GetName(list) ?? string.Empty, AutomationProperties.GetLandmarkType(list));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(string.Join(" | ", s_pages), string.Join(" | ", pages.Select(page => page.Name)), "The rail's pages, in today's order.");
        Assert.AreEqual(string.Join(" | ", s_commands), string.Join(" | ", commands.Select(command => command.Name)), "The rail's commands, in today's order.");
        Assert.AreEqual("Pages", landmark, "Today's pages panel is named Pages (Form1.Shell.vb:224).");
        Assert.AreEqual(AutomationLandmarkType.Navigation, landmarkType);

        RailFacts board = pages[0];
        Assert.IsTrue(board.Enabled, "The Board item is live.");
        Assert.IsTrue(board.Checked, "The Board item is checked.");
        Assert.AreEqual("Board (Ctrl+1)", board.ToolTip, "Today's tooltip (Form1.Shell.vb:249).");
        Assert.AreEqual("Rail.Board", board.AutomationId);

        foreach (RailFacts item in pages.Skip(1).Concat(commands))
        {
            Assert.IsFalse(item.Enabled, $"{item.Name} is live; only the Board is in the trial.");
            Assert.IsFalse(item.Checked, $"{item.Name} is checked.");
            Assert.AreEqual(RailItemViewModel.NotInTrial, item.ToolTip, $"{item.Name}'s tooltip.");
            Assert.IsTrue(item.HelpText.Contains(RailItemViewModel.NotInTrial, StringComparison.Ordinal), $"{item.Name}'s description is \"{item.HelpText}\".");
            Assert.IsTrue(item.ToolTipOnDisabled, $"{item.Name} shows no tooltip while disabled.");
        }
    }

    // d1 1.2: a press on any rail item other than the Board does nothing (it
    // is disabled); a press on the Board shows the Board.
    [TestMethod]
    public void APressOnAnotherRailItemDoesNothing()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        int shown = 0;
        shell.BoardShown += (_, _) => shown++;

        (int afterOthers, bool boardChecked, bool anyOtherChecked, int afterBoard) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                IReadOnlyList<RadioButton> pages = RailPages(window);
                foreach (Control item in pages.Skip(1).Cast<Control>().Concat(RailCommands(window)))
                {
                    Click(window, item);
                }

                int others = shown;
                bool checkedNow = pages[0].IsChecked == true;
                bool otherNow = pages.Skip(1).Any(page => page.IsChecked == true);
                Click(window, pages[0]);
                return (others, checkedNow, otherNow, shown);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(0, afterOthers, "A press on another item showed the Board.");
        Assert.IsTrue(boardChecked, "The Board is still checked.");
        Assert.IsFalse(anyOtherChecked, "A disabled page became checked.");
        Assert.AreEqual(ShellViewModel.LoadingText, shell.Notice, "A press on a disabled item gave a notice.");
        Assert.AreEqual(1, afterBoard, "A press on the Board item shows the Board.");
        Assert.IsTrue(shell.BoardItem.IsSelected);
    }

    // The example window's title and banner (d1 3.3; Form1.Example.vb:36-60):
    // the banner is named "Example library" and holds today's text plus the
    // trial's sentence.
    [TestMethod]
    public void TheTitleAndBannerNameTheExample()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);

        (string title, string name, AutomationLandmarkType? landmark, string text) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                Border banner = window.FindControl<Border>("Banner") ?? throw new AssertFailedException("The window has no banner.");
                TextBlock bannerText = window.FindControl<TextBlock>("BannerText") ?? throw new AssertFailedException("The banner has no text.");
                return (window.Title ?? string.Empty, AutomationProperties.GetName(banner) ?? string.Empty,
                        AutomationProperties.GetLandmarkType(banner), bannerText.Text ?? string.Empty);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual("PaperRoute Board Trial - Example Library", title);
        Assert.AreEqual("Example library", name, "Today's accessible name (Form1.Example.vb:48).");
        Assert.AreEqual(AutomationLandmarkType.Banner, landmark);
        Assert.AreEqual(ShellViewModel.ExampleBannerText, text);
        Assert.IsTrue(text.StartsWith("Example library: a fictional research group's manuscripts, to explore and to teach with. ", StringComparison.Ordinal),
            "Today's banner text comes first (Form1.Example.vb:55-56).");
        Assert.IsTrue(text.EndsWith(" Trial build: nothing is saved.", StringComparison.Ordinal), "The trial's sentence comes last (d1 3.3).");
    }

    // Form1.Shell.vb UpdateRailCounts (:420-435): no summary before the first
    // render; after the load, "7 manuscripts" and the Deadlines badge from
    // DeadlineService.CountDueNow over the library, both in the window.
    [TestMethod]
    public void TheRailCountsFollowTheLoadedLibrary()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        Assert.AreEqual(string.Empty, shell.RailSummary, "No summary before the first render.");
        Assert.AreEqual(0, shell.DeadlinesItem.Count);

        Load(shell);
        int due = DeadlineService.CountDueNow(session.Manuscripts, session.Today);

        Assert.AreEqual("7 manuscripts", shell.RailSummary);
        Assert.AreEqual(due, shell.DeadlinesItem.Count, "The Deadlines badge counts what is due now.");
        Assert.AreEqual("Loaded 7 manuscripts from local storage.", shell.Notice);

        (string summary, bool summaryShown, bool badgeShown, string badge) = Headless.Run(() =>
        {
            MainWindow window = Show(shell);
            try
            {
                TextBlock text = window.FindControl<TextBlock>("RailSummary") ?? throw new AssertFailedException("The rail has no summary.");
                RadioButton deadlines = RailPages(window)[3];
                Border pill = deadlines.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("rail-badge"));
                TextBlock count = pill.GetVisualDescendants().OfType<TextBlock>().Single();
                return (text.Text ?? string.Empty, text.IsEffectivelyVisible, pill.IsVisible, count.Text ?? string.Empty);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual("7 manuscripts", summary);
        Assert.IsTrue(summaryShown);
        Assert.AreEqual(due > 0, badgeShown, "The badge shows only while something is due.");
        if (due > 0)
        {
            Assert.AreEqual(due.ToString(System.Globalization.CultureInfo.CurrentCulture), badge);
        }
    }

    // Pictures of the shell with the example loaded, for people to look at.
    [TestMethod]
    public void TheShellIsSavedInLight() => SaveShell(ThemeVariant.Light, "shell-light");

    [TestMethod]
    public void TheShellIsSavedInDark() => SaveShell(ThemeVariant.Dark, "shell-dark");

    private void SaveShell(ThemeVariant variant, string name)
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        Load(shell);

        string path = Headless.Run(() =>
        {
            MainWindow window = Show(shell, variant);
            try
            {
                return Pictures.Save(window, name, TestContext);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // On the test thread: the session reads off it and the shell's notice and
    // the Board's render follow, before any window is made.
    private static void Load(ShellViewModel shell) =>
        shell.LoadCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();

    private static MainWindow Show(ShellViewModel shell, ThemeVariant? variant = null)
    {
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        if (variant is not null)
        {
            window.RequestedThemeVariant = variant;
        }

        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static IReadOnlyList<RadioButton> RailPages(Window window) =>
        (window.FindControl<ItemsControl>("RailPages") ?? throw new AssertFailedException("The window has no rail pages."))
            .GetVisualDescendants().OfType<RadioButton>().ToList();

    private static IReadOnlyList<Button> RailCommands(Window window) =>
        (window.FindControl<ItemsControl>("RailCommands") ?? throw new AssertFailedException("The window has no rail commands."))
            .GetVisualDescendants().OfType<Button>().ToList();

    private static RailFacts FactsOf(Control item) => new(
        AutomationProperties.GetName(item) ?? string.Empty,
        AutomationProperties.GetAutomationId(item) ?? string.Empty,
        AutomationProperties.GetHelpText(item) ?? string.Empty,
        ToolTip.GetTip(item) as string ?? string.Empty,
        ToolTip.GetShowOnDisabled(item),
        item.IsEffectivelyEnabled,
        item is RadioButton radio && radio.IsChecked == true);

    private static void Click(Window window, Control control)
    {
        Point centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
            ?? throw new AssertFailedException($"{control} is not in the window.");
        window.MouseMove(centre, RawInputModifiers.None);
        window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private DiskLibrarySession OpenSession() => new(
        Path.Combine(_root, "data"),
        Path.Combine(_root, "library"),
        _root,
        LibraryKind.Example,
        LargeLibraryFixture.Today);

    private sealed record RailFacts(
        string Name,
        string AutomationId,
        string HelpText,
        string ToolTip,
        bool ToolTipOnDisabled,
        bool Enabled,
        bool Checked);
}
