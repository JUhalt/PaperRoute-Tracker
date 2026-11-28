using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;
using PaperRoute.ViewModels.Shell;

namespace PaperRoute.Desktop.Tests.Shell;

// The window frame (shell design B5, 3.1 rows 1, 2 and 5): today's title,
// start size and minimum capped to the screen, the logo as its icon, and the
// close path: the page shown is asked, a running load is cancelled and
// awaited, and the operating system's shutdown is never refused and waits
// for running library work. Each test builds its library in a temporary
// folder of its own, under the assembly's storage guard.
[TestClass]
public sealed class WindowFrameTests : StorageCheckedTests
{
    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialFrame-" + Guid.NewGuid().ToString("N"));
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

    // Today's 1280x840 start and 960x680 minimum (Form1.vb:249-280), centred,
    // as the window requests them: the headless screen (1920x1280) caps
    // nothing, and Xvfb in CI has no window manager to enforce them. Then
    // the cap applied to a real window for a 1366x768 laptop's working area,
    // with no decoration (as on X11 before the window is mapped) and with
    // one, and today's cap for smaller working areas, from Form1.StartSizes's
    // rule: the minimum lowered to the area, the start size kept 72 pixels
    // inside it and never below the minimum, the whole window (title bar
    // and borders included) inside the area.
    [TestMethod]
    public void TheWindowStartsAtTodaysSizeAndMinimum()
    {
        var frame = new Size(16, 40);
        (Size requested, Size minimum, WindowStartupLocation place, Size shown, Size fitted, Size fittedMinimum, Size fittedShown, Size framed, Size framedMinimum) = Headless.Run(() =>
        {
            var window = new MainWindow();
            var asked = new Size(window.Width, window.Height);
            var least = new Size(window.MinWidth, window.MinHeight);
            WindowStartupLocation where = window.WindowStartupLocation;
            window.Show();

            var laptop = new MainWindow();
            laptop.FitTo(new PixelSize(1366, 728), 1, default);
            var laptopAsked = new Size(laptop.Width, laptop.Height);
            var laptopLeast = new Size(laptop.MinWidth, laptop.MinHeight);
            laptop.Show();

            var decorated = new MainWindow();
            decorated.FitTo(new PixelSize(1366, 728), 1, frame);
            try
            {
                Dispatcher.UIThread.RunJobs();
                return (asked, least, where, window.ClientSize, laptopAsked, laptopLeast, laptop.ClientSize,
                    new Size(decorated.Width, decorated.Height), new Size(decorated.MinWidth, decorated.MinHeight));
            }
            finally
            {
                window.Close();
                laptop.Close();
            }
        });

        Assert.AreEqual(new Size(1280, 840), requested, "Today's start size.");
        Assert.AreEqual(new Size(960, 680), minimum, "Today's minimum.");
        Assert.AreEqual(WindowStartupLocation.CenterScreen, place, "Centred, as today.");
        Assert.AreEqual(new Size(1280, 840), shown, "The window opens at its start size.");
        Assert.AreEqual(new Size(1280, 680), fitted, "The start size a window fitted to a 1366x768 laptop requests.");
        Assert.AreEqual(new Size(960, 680), fittedMinimum, "The minimum a window fitted to a 1366x768 laptop requests.");
        Assert.AreEqual(new Size(1280, 680), fittedShown, "The fitted window opens at its capped start size.");
        Assert.AreEqual(new Size(1278, 680), framed, "The start size a decorated window fitted to a 1366x768 laptop requests.");
        Assert.AreEqual(new Size(960, 680), framedMinimum, "The minimum a decorated window fitted to a 1366x768 laptop requests.");

        Assert.AreEqual(new Size(16, 40), MainWindow.DecorationOf(new Size(1296, 880), new Size(1280, 840)), "The decoration is the frame less the client area.");
        Assert.AreEqual(default, MainWindow.DecorationOf(null, new Size(1280, 840)), "No decoration while the platform reports no frame.");

        var start = new Size(1280, 840);
        var least = new Size(960, 680);
        AssertSizes(new PixelSize(1920, 1040), 1, frame, start, least, new Size(1280, 840), new Size(960, 680), "a 1920x1080 screen");
        AssertSizes(new PixelSize(1366, 728), 1, default, start, least, new Size(1280, 680), new Size(960, 680), "a 1366x768 laptop, no decoration");
        AssertSizes(new PixelSize(1366, 728), 1, frame, start, least, new Size(1278, 680), new Size(960, 680), "a 1366x768 laptop");
        AssertSizes(new PixelSize(1920, 1008), 1.5, frame, start, least, new Size(1216, 632), new Size(960, 632), "1920x1080 at 150%");
        AssertSizes(new PixelSize(1024, 600), 1, frame, start, least, new Size(960, 560), new Size(960, 560), "a 1024x600 screen");
    }

    // Today's title for the example (Form1.vb:294-303), and the app's name
    // before a shell is bound. The picture is the window at its own start
    // size with the example loaded, for people to look at.
    [TestMethod]
    public void TheTitleNamesTheExampleAsToday()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);
        shell.LoadCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();

        (string unbound, string title, string picture) = Headless.Run(() =>
        {
            string before = new MainWindow().Title ?? string.Empty;
            var window = new MainWindow { DataContext = shell, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            try
            {
                return (before, window.Title ?? string.Empty, Pictures.Save(window, "window-frame-light", TestContext));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual("PaperRoute Tracker", unbound, "The window's own name before a shell is bound.");
        Assert.AreEqual("PaperRoute Tracker - Example Library", title, "Today's title for the example.");
        Assert.AreEqual(ShellViewModel.ExampleWindowTitle, title);
        Assert.IsTrue(File.Exists(picture), $"No picture at {picture}.");
    }

    // The window's icon is the rail's logo, the Windows Forms app's own file
    // linked into the app's resources (PaperRoute.Desktop.csproj).
    [TestMethod]
    public void TheWindowHasItsIcon()
    {
        string axaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Axaml", "MainWindow.axaml"));
        string icon = Regex.Match(axaml, "\\bIcon=\"(?<path>[^\"]+)\"").Groups["path"].Value;
        string logo = Regex.Match(axaml, "<Image\\b[^>]*\\bSource=\"(?<path>[^\"]+)\"").Groups["path"].Value;

        (bool hasIcon, bool exists) = Headless.Run(() =>
        {
            var window = new MainWindow();
            return (window.Icon is not null, !string.IsNullOrEmpty(icon) && AssetLoader.Exists(new Uri("avares://PaperRoute.Desktop" + icon)));
        });

        Assert.IsTrue(hasIcon, "The window has no icon.");
        Assert.AreEqual("/Assets/PaperRoute.png", icon, "The window names the logo as its icon.");
        Assert.AreEqual(logo, icon, "The icon is the rail's logo.");
        Assert.IsTrue(exists, $"The icon {icon} is not among the app's resources.");
    }

    // A page whose leave guard refuses keeps the window open (3.1 row 5), and
    // is asked once per close; once it allows the leave, the window closes.
    [TestMethod]
    public void AWindowWhosePageRefusesDoesNotClose()
    {
        var session = new MemorySession([], Papers.Today);
        var page = new FakePage(PageKey.Library) { AllowsLeave = false };

        (bool closedOnRefusal, bool shownOnRefusal, int askedOnRefusal, bool closedOnAllow, int askedOnAllow) = Headless.Run(() =>
        {
            ShellViewModel shell = App.CreateShell(session);
            shell.CurrentPage = page;
            var window = new MainWindow { DataContext = shell };
            bool closed = false;
            window.Closed += (_, _) => closed = true;
            window.Show();
            try
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                (bool refused, bool shown, int asked) = (closed, window.IsVisible, page.LeaveQuestions);

                page.AllowsLeave = true;
                window.Close();
                Dispatcher.UIThread.RunJobs();
                return (refused, shown, asked, closed, page.LeaveQuestions);
            }
            finally
            {
                if (!closed)
                {
                    page.AllowsLeave = true;
                    window.Close();
                }
            }
        });

        Assert.IsFalse(closedOnRefusal, "The window closed though its page refused the leave.");
        Assert.IsTrue(shownOnRefusal, "The window is no longer shown after the refused close.");
        Assert.AreEqual(1, askedOnRefusal, "The close asked the page once.");
        Assert.IsTrue(closedOnAllow, "The window did not close once its page allowed the leave.");
        Assert.AreEqual(2, askedOnAllow, "The second close asked the page again.");
    }

    // A close while the page has not yet answered the last one (its question
    // to the person still open) is refused without asking the page again;
    // the window closes once the page answers yes.
    [TestMethod]
    public void ASecondCloseWhileThePageIsAskedAsksNothing()
    {
        var session = new MemorySession([], Papers.Today);
        var page = new FakePage(PageKey.Library) { HeldAnswer = new TaskCompletionSource<bool>() };

        (bool closedWhileAsked, int askedWhileAsked, bool closed, int asked) = Headless.Run(() =>
        {
            ShellViewModel shell = App.CreateShell(session);
            shell.CurrentPage = page;
            var window = new MainWindow { DataContext = shell };
            bool isClosed = false;
            window.Closed += (_, _) => isClosed = true;
            window.Show();
            try
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                window.Close();
                Dispatcher.UIThread.RunJobs();
                (bool open, int questions) = (isClosed, page.LeaveQuestions);

                page.HeldAnswer.SetResult(true);
                BoardWindow.WaitFor(() => isClosed, TimeSpan.FromSeconds(30));
                return (open, questions, isClosed, page.LeaveQuestions);
            }
            finally
            {
                page.HeldAnswer.TrySetResult(true);
                if (!isClosed)
                {
                    page.HeldAnswer = null;
                    window.Close();
                }
            }
        });

        Assert.IsFalse(closedWhileAsked, "The window closed while its page had not answered.");
        Assert.AreEqual(1, askedWhileAsked, "The second close asked the page again while its first question was open.");
        Assert.IsTrue(closed, "The window did not close once its page answered yes.");
        Assert.AreEqual(1, asked, "The page was asked again after it answered.");
    }

    // A close while the load runs (3.1 row 5): the window stays open while the
    // load's worker is held, the load is cancelled (it reads no manuscripts,
    // so the data folder the repository's load would create never appears,
    // and nothing is published), and the window closes once it has ended.
    [TestMethod]
    public void ACloseDuringTheLoadCancelsItAndWaitsForIt()
    {
        using var held = new HeldLibrary(_root);

        (bool openWhileHeld, bool runningWhileHeld, bool closed, bool running, string notice, int manuscripts) = Headless.Run(() =>
        {
            ShellViewModel shell = App.CreateShell(held.Session);
            var window = new MainWindow { DataContext = shell };
            bool isClosed = false;
            window.Closed += (_, _) => isClosed = true;
            window.Show();
            try
            {
                held.StartLoad(shell);
                window.Close();
                Dispatcher.UIThread.RunJobs();
                bool open = !isClosed && window.IsVisible;
                bool stillRunning = shell.LoadCommand.IsRunning;

                held.Release();
                BoardWindow.WaitFor(() => isClosed, TimeSpan.FromSeconds(30));
                return (open, stillRunning, isClosed, shell.LoadCommand.IsRunning, shell.Notice, held.Session.Manuscripts.Count);
            }
            finally
            {
                held.Release();
                if (!isClosed)
                {
                    window.Close();
                }
            }
        });

        Assert.IsTrue(openWhileHeld, "The window closed while the load was still running.");
        Assert.IsTrue(runningWhileHeld, "The load ended while its worker was held.");
        Assert.IsTrue(closed, "The window did not close once the load had ended.");
        Assert.IsFalse(running, "The window closed before the load had ended.");
        Assert.IsFalse(Directory.Exists(held.DataDirectory), "The load read the library after the close: it was not cancelled.");
        Assert.AreEqual(0, manuscripts, "The cancelled load published manuscripts.");
        Assert.AreEqual(ShellViewModel.LoadingText, notice, "The cancelled load published its notice.");
    }

    // The operating system's shutdown (3.1 row 5): the close step finishes
    // at once with yes, never asking the page (here one that would refuse),
    // and only after the running load's worker has ended. A settings save
    // held during that load is not written by the close: it would write the
    // settings that load read, which are on disk already, so its loss loses
    // nothing a person changed; it is written only if the interface thread
    // runs again. The window maps the shutdown's reason, and only it, to
    // this step (WindowClosingEventArgs cannot be made outside Avalonia).
    //
    // The worker is released from a thread of the test's own, which has
    // signalled its start, once the interface thread is blocked in the close
    // step: never from the thread pool, which may start a task late.
    [TestMethod]
    public void AShutdownCloseIsNeverRefusedAndWaitsForTheLoad()
    {
        string settingsFile = Path.Combine(_root, "settings.json");
        using var held = new HeldLibrary(_root);
        var page = new FakePage(PageKey.Library) { AllowsLeave = false };

        (bool finished, bool allowed, bool workerRunningAfter, bool released, int asked, bool saveDoneAtClose, bool writtenAtClose, string noticeAtClose, bool saveLater, bool writtenLater) = Headless.Run(() =>
        {
            ShellViewModel shell = App.CreateShell(held.Session);
            shell.CurrentPage = page;
            held.StartLoad(shell);
            Task<bool> save = held.Session.SaveSettingsAsync();

            using var calling = new ManualResetEventSlim();
            using var started = new ManualResetEventSlim();
            Thread ui = Thread.CurrentThread;
            bool sawBlocked = false;
            var releaser = new Thread(() =>
            {
                started.Set();
                calling.Wait();
                var limit = Stopwatch.StartNew();
                while ((ui.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0 && limit.Elapsed < TimeSpan.FromSeconds(30))
                {
                    Thread.Yield();
                }

                sawBlocked = (ui.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0;
                held.Release();
            })
            { IsBackground = true, Name = "Shutdown test releaser" };
            releaser.Start();
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(30)), "The releasing thread did not start.");

            calling.Set();
            ValueTask<bool> step = shell.CloseAsync(CloseKind.Shutdown);
            bool done = step.IsCompleted;
            bool yes = done && step.Result;
            bool workerRunning = held.Session.WaitForWork();
            (bool saveDone, bool written, string notice) = (save.IsCompleted, File.Exists(settingsFile), held.Session.StartupNotice);
            Assert.IsTrue(releaser.Join(TimeSpan.FromSeconds(30)), "The releasing thread did not end.");

            BoardWindow.WaitFor(() => !shell.LoadCommand.IsRunning && save.IsCompleted, TimeSpan.FromSeconds(30));
            return (done, yes, workerRunning, sawBlocked, page.LeaveQuestions, saveDone, written, notice, save.IsCompleted && save.Result, File.Exists(settingsFile));
        });

        Assert.IsTrue(finished, "The shutdown's close step did not finish at once.");
        Assert.IsTrue(allowed, "The shutdown's close step refused.");
        Assert.AreEqual(0, asked, "The shutdown asked the page.");
        Assert.IsTrue(released, "The interface thread never blocked in the close step: it did not wait for the load.");
        Assert.IsFalse(workerRunningAfter, "The close step returned while the load's worker still ran.");
        Assert.AreEqual(string.Empty, noticeAtClose, "The close step ran the load's publish on the blocked interface thread.");
        Assert.IsFalse(saveDoneAtClose, "The close step wrote the held settings save.");
        Assert.IsFalse(writtenAtClose, "The close step wrote settings.json.");
        Assert.IsTrue(saveLater, "The held save was not written when the interface thread ran again.");
        Assert.IsTrue(writtenLater, "The held save wrote no settings.json when the interface thread ran again.");

        Assert.AreEqual(CloseKind.Shutdown, MainWindow.KindOf(WindowCloseReason.OSShutdown));
        foreach (WindowCloseReason reason in Enum.GetValues<WindowCloseReason>().Where(reason => reason != WindowCloseReason.OSShutdown))
        {
            Assert.AreEqual(CloseKind.Ordinary, MainWindow.KindOf(reason), $"{reason} is an ordinary close.");
        }
    }

    // The sizes for a working area, and, as today's rule holds (#93), the
    // whole window at either size inside it.
    private static void AssertSizes(PixelSize workingArea, double scaling, Size decoration, Size requestedStart, Size requestedMinimum, Size start, Size minimum, string screen)
    {
        (Size Start, Size Minimum) actual = MainWindow.StartSizes(workingArea, scaling, decoration, requestedStart, requestedMinimum);
        Assert.AreEqual(start, actual.Start, $"The start size on {screen}.");
        Assert.AreEqual(minimum, actual.Minimum, $"The minimum on {screen}.");
        foreach (Size size in new[] { actual.Start, actual.Minimum })
        {
            Assert.IsTrue((size.Width + decoration.Width) * scaling <= workingArea.Width, $"The window at {size} is wider than the working area on {screen}.");
            Assert.IsTrue((size.Height + decoration.Height) * scaling <= workingArea.Height, $"The window at {size} is taller than the working area on {screen}.");
        }
    }

    private DiskLibrarySession OpenSession() => new(
        Path.Combine(_root, "data"),
        Path.Combine(_root, "library"),
        _root,
        LibraryKind.Example,
        LargeLibraryFixture.Today);

    // An empty library under root whose load stops in its prepare step (the
    // step the example's seed runs in) until Release: the load has started,
    // and has read and written nothing yet.
    private sealed class HeldLibrary : IDisposable
    {
        private readonly ManualResetEventSlim _entered = new();
        private readonly ManualResetEventSlim _gate = new();

        public HeldLibrary(string root)
        {
            DataDirectory = Path.Combine(root, "data");
            Session = new DiskLibrarySession(
                DataDirectory,
                Path.Combine(root, "library"),
                root,
                LibraryKind.Example,
                LargeLibraryFixture.Today,
                prepare: () =>
                {
                    _entered.Set();
                    _gate.Wait(TimeSpan.FromSeconds(60));
                },
                snapshot: null);
        }

        public DiskLibrarySession Session { get; }

        public string DataDirectory { get; }

        // Starts the shell's load, as the window's opening does, and returns
        // once its worker is held.
        public void StartLoad(ShellViewModel shell)
        {
            shell.LoadCommand.Execute(null);
            Assert.IsTrue(_entered.Wait(TimeSpan.FromSeconds(30)), "The load did not reach its prepare step.");
        }

        public void Release() => _gate.Set();

        // The two events are left undisposed: a worker still held when a test
        // fails must find its gate open, not disposed.
        public void Dispose()
        {
            _gate.Set();
            Session.Dispose();
        }
    }
}
