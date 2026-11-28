using System.Reflection;
using Avalonia.Controls;
using ManuscriptPipeline.Services;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The library session and the window's notice (d1 3.1, 3.4, step 3). These run
// in the test process, so they never call TrialStartup or start a storage
// session (d1 6.1): each builds DiskLibrarySession over a temporary folder of
// its own, written by the test fixture, under the assembly's storage guard.
[TestClass]
public sealed class LibrarySessionTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialSession-" + Guid.NewGuid().ToString("N"));
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
    }

    [TestMethod]
    public async Task TheSessionLoadsTheExampleAndSaysHowMany()
    {
        LargeLibraryFixture.WriteExample(_root);
        int expected = ExampleLibraryService.Create(LargeLibraryFixture.Today).Manuscripts.Count;
        using DiskLibrarySession session = OpenSession();

        Assert.IsEmpty(session.Manuscripts);
        Assert.AreEqual(string.Empty, session.StartupNotice);
        int changes = 0;
        session.LibraryChanged += (_, _) => changes++;

        await session.LoadAsync(CancellationToken.None);

        Assert.AreEqual(7, expected, "The example library no longer holds 7 manuscripts.");
        Assert.HasCount(expected, session.Manuscripts);
        Assert.AreEqual("Loaded 7 manuscripts from local storage.", session.StartupNotice);
        Assert.AreEqual(1, changes, "LibraryChanged is raised once per load.");
        Assert.IsNotNull(session.AuthorLibrary);
        Assert.HasCount(3, session.AuthorLibrary.Authors);
        Assert.AreEqual(3, session.SearchIndex.AuthorCount);
        Assert.IsNotNull(session.Settings);
        Assert.IsTrue(session.IsReadOnly);
        Assert.AreEqual(LibraryKind.Example, session.Kind);
        Assert.AreEqual(LargeLibraryFixture.Today, session.Today);
    }

    [TestMethod]
    public async Task ALibraryThatCannotBeReadLeavesTheBoardEmptyWithANotice()
    {
        string data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(Path.Combine(data, "manuscripts.json"), "{ this is not a library");
        using DiskLibrarySession session = OpenSession();

        await session.LoadAsync(CancellationToken.None);

        Assert.IsEmpty(session.Manuscripts);
        Assert.AreEqual("Library could not be loaded safely.", session.StartupNotice, "Today's status line (Form1.vb:1448-1449), with no exception text.");
    }

    [TestMethod]
    public async Task TheShellShowsLoadingAndThenTheLoadedCount()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = ShellOver(session);

        Assert.AreEqual("Loading…", shell.Notice);
        await shell.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("Loaded 7 manuscripts from local storage.", shell.Notice);
        ((IInteractions)shell).Notice("A notice.");
        Assert.AreEqual("A notice.", shell.Notice);
    }

    // ManuscriptRepository.Load can write (d1 1.3), so the session refuses any
    // folder outside the temporary folder: the user's own profile above all.
    // Only the paths are checked; nothing is read or created.
    [TestMethod]
    public void TheSessionRefusesAFolderOutsideTheTemporaryFolder()
    {
        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaperRoute");

        Assert.ThrowsExactly<ArgumentException>(() => new DiskLibrarySession(
            Path.Combine(profile, "data"),
            Path.Combine(_root, "library"),
            _root,
            LibraryKind.Example,
            LargeLibraryFixture.Today));
        Assert.ThrowsExactly<ArgumentException>(() => new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            profile,
            LibraryKind.Example,
            LargeLibraryFixture.Today));
    }

    // Lock 3 of d1 1.3: the session has no member that could save, back up or
    // restore the library.
    [TestMethod]
    public void TheSessionHasNoSaveBackupOrRestoreMember()
    {
        string[] words = ["Save", "Backup", "Restore", "Recover", "Write", "Delete"];
        foreach (Type type in new[] { typeof(ILibrarySession), typeof(DiskLibrarySession) })
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.IsFalse(
                    words.Any(word => member.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"{type.Name}.{member.Name} looks like a write; the trial's session only reads.");
            }
        }
    }

    // A load is library work (ManuscriptRepository.Load can write), so it runs
    // one at a time (shell design 2.5, critic M4). The first load is held in
    // its prepare step, before it reads anything; a second load meanwhile is
    // refused at once, runs no prepare and creates no data folder.
    [TestMethod]
    public async Task ASecondLoadWhileOneRunsIsRefusedAndWritesNothing()
    {
        using var held = new HeldLoad(_root);
        int changes = 0;
        held.Session.LibraryChanged += (_, _) => changes++;
        Task<bool> first = held.StartLoad();

        Task<bool> second = held.Session.LoadAsync(CancellationToken.None);

        Assert.IsTrue(second.IsCompleted, "A load refused while another runs returns at once.");
        Assert.IsFalse(await second, "A second load while one runs is refused.");
        Assert.IsFalse(Directory.Exists(held.DataDirectory), "The refused load wrote nothing: the data folder was not created.");
        Assert.AreEqual(1, held.PrepareRuns, "The refused load ran no work of its own.");
        Assert.AreEqual(0, changes);

        held.Release();
        Assert.IsTrue(await first, "The first load finishes.");
        Assert.IsTrue(Directory.Exists(held.DataDirectory), "The first load read the library, creating its data folder as the repository does.");
        Assert.AreEqual(1, changes, "Only the first load published.");
        Assert.IsTrue(await held.Session.LoadAsync(CancellationToken.None), "Once the first load has ended, a new one runs.");
    }

    // The shutdown wait (shell design 2.5) blocks on the worker itself until
    // it ends, and returns at once when nothing runs.
    [TestMethod]
    public async Task WaitForWorkReturnsWhenTheWorkerEnds()
    {
        using var held = new HeldLoad(_root);
        Assert.IsFalse(held.Session.WaitForWork(), "Nothing runs before the first load.");
        Task<bool> load = held.StartLoad();

        Task<bool> waiting = Task.Run(held.Session.WaitForWork);
        Assert.IsFalse(waiting.Wait(TimeSpan.FromMilliseconds(300)), "WaitForWork returned while the worker was still held.");

        held.Release();
        Assert.IsTrue(waiting.Wait(TimeSpan.FromSeconds(30)), "WaitForWork did not return once the worker ended.");
        Assert.IsTrue(waiting.Result, "WaitForWork waited for a running worker.");
        Assert.IsTrue(await load);
        Assert.IsFalse(held.Session.WaitForWork(), "Nothing runs after the load.");
    }

    // The window binds its notice to the shell (compiled binding, x:DataType).
    [TestMethod]
    public void TheWindowShowsTheShellsNotice()
    {
        using DiskLibrarySession session = OpenSession();
        ShellViewModel shell = ShellOver(session);

        (string before, string after) = Headless.Run(() =>
        {
            var window = new MainWindow { DataContext = shell };
            window.Show();
            try
            {
                TextBlock notice = window.FindControl<TextBlock>("NoticeText")
                    ?? throw new AssertFailedException("The window has no notice text.");
                string first = notice.Text ?? string.Empty;
                ((IInteractions)shell).Notice("Loaded 7 manuscripts from local storage.");
                return (first, notice.Text ?? string.Empty);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual("Loading…", before);
        Assert.AreEqual("Loaded 7 manuscripts from local storage.", after);
    }

    // The shell as the app makes it, with the waits the tests use.
    internal static ShellViewModel ShellOver(ILibrarySession session) =>
        new(session, new BoardViewModel(session, new ImmediateDebouncer(), new QueuedUiDispatcher()));

    private DiskLibrarySession OpenSession() => new(
        Path.Combine(_root, "data"),
        Path.Combine(_root, "library"),
        _root,
        LibraryKind.Example,
        LargeLibraryFixture.Today);

    // A session over root whose first load stops in its prepare step (the
    // step the example's seed runs in) until Release: the load has started,
    // and has read and written nothing yet.
    private sealed class HeldLoad : IDisposable
    {
        private readonly ManualResetEventSlim _entered = new();
        private readonly ManualResetEventSlim _gate = new();
        private int _prepareRuns;

        public HeldLoad(string root)
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
                    Interlocked.Increment(ref _prepareRuns);
                    _entered.Set();
                    _gate.Wait(TimeSpan.FromSeconds(60));
                },
                snapshot: null);
        }

        public DiskLibrarySession Session { get; }

        public string DataDirectory { get; }

        public int PrepareRuns => Volatile.Read(ref _prepareRuns);

        // Starts the first load and returns once it is held.
        public Task<bool> StartLoad()
        {
            Task<bool> load = Session.LoadAsync(CancellationToken.None);
            Assert.IsTrue(_entered.Wait(TimeSpan.FromSeconds(30)), "The load did not reach its prepare step.");
            return load;
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
