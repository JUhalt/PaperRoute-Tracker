using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using ManuscriptPipeline.Models;
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
    // restore the library. Its one write, the settings save, is the single
    // name let through here; TheSessionWritesOnlyThroughItsSaveMembers pins
    // it (shell design 2.5).
    [TestMethod]
    public void TheSessionHasNoSaveBackupOrRestoreMember()
    {
        string[] words = ["Save", "Backup", "Restore", "Recover", "Write", "Delete"];
        foreach (Type type in new[] { typeof(ILibrarySession), typeof(DiskLibrarySession) })
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(member => member.Name != nameof(ILibrarySession.SaveSettingsAsync)))
            {
                Assert.IsFalse(
                    words.Any(word => member.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"{type.Name}.{member.Name} looks like a library write; the session saves only its settings.");
            }
        }
    }

    // The session's writes, by name (shell design 2.5): no member named
    // Backup, Restore, Recover, Write or Delete, and SaveSettingsAsync, which
    // returns Task<bool>, is the only member with Save in its name. The
    // library save is not built in build 5 (the design's question 3).
    [TestMethod]
    public void TheSessionWritesOnlyThroughItsSaveMembers()
    {
        string[] words = ["Backup", "Restore", "Recover", "Write", "Delete"];
        foreach (Type type in new[] { typeof(ILibrarySession), typeof(DiskLibrarySession) })
        {
            MemberInfo[] members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (MemberInfo member in members)
            {
                Assert.IsFalse(
                    words.Any(word => member.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"{type.Name}.{member.Name} looks like a write; the session writes only through its save members.");
            }

            string[] saves = [.. members
                .Where(member => member.Name.Contains("Save", StringComparison.OrdinalIgnoreCase))
                .Select(member => member.Name)
                .Distinct(StringComparer.Ordinal)];
            CollectionAssert.AreEqual(new[] { nameof(ILibrarySession.SaveSettingsAsync) }, saves,
                $"{type.Name}'s only save member is the settings save; the library save comes with the first issue that changes the library.");
            MethodInfo save = type.GetMethod(nameof(ILibrarySession.SaveSettingsAsync), Type.EmptyTypes)
                ?? throw new AssertFailedException($"{type.Name} has no SaveSettingsAsync().");
            Assert.AreEqual(typeof(Task<bool>), save.ReturnType);
        }
    }

    // Settings are saved through the session's own folder and nothing else
    // (shell design 2.5). Before a load there are no settings to save, so a
    // save then writes nothing; after it, only settings.json and its backup
    // change, and a new session reads the saved value back.
    [TestMethod]
    public async Task ASettingsSaveWritesOnlyTheSettingsFiles()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = OpenSession();
        IReadOnlyDictionary<string, string> unloaded = FileState(_root);

        Assert.IsFalse(await session.SaveSettingsAsync(), "Before a load there are no loaded settings to save.");
        CollectionAssert.AreEquivalent(unloaded.ToArray(), FileState(_root).ToArray(), "A save before the load wrote something.");

        await session.LoadAsync(CancellationToken.None);
        IReadOnlyDictionary<string, string> before = FileState(_root);
        bool collapsed = !session.Settings.NavigationCollapsed;
        session.Settings.NavigationCollapsed = collapsed;
        Assert.IsTrue(await session.SaveSettingsAsync());
        IReadOnlyDictionary<string, string> after = FileState(_root);

        string[] changed = [.. after.Keys.Union(before.Keys)
            .Where(file => !before.TryGetValue(file, out string? was) || !after.TryGetValue(file, out string? now) || was != now)
            .Order(StringComparer.Ordinal)];
        CollectionAssert.Contains(changed, "settings.json", "The save did not write settings.json.");
        CollectionAssert.IsSubsetOf(changed, new[] { "settings.json", "settings.bak" }, "The settings save changed other files: " + string.Join(", ", changed));
        Assert.IsTrue(before.Keys.All(after.ContainsKey), "The settings save removed a file.");

        using DiskLibrarySession again = OpenSession();
        await again.LoadAsync(CancellationToken.None);
        Assert.AreEqual(collapsed, again.Settings.NavigationCollapsed, "A new session reads the saved settings back.");
    }

    // Critic M5, settings half: an unreadable settings.json beside a good
    // settings.bak. The service that loaded knows the file was unreadable, so
    // its save sets the file aside; a new service would not, and its write
    // would make the unreadable file the new settings.bak, losing the only
    // good copy (AppSettingsService.vb:68-75, 102-107).
    [TestMethod]
    public async Task ASettingsSaveAfterAnUnreadableFileSetsItAside()
    {
        const string Unreadable = "{ this is not settings";
        const string Good = "{ \"FileDrawerSuggestionThreshold\": 7 }";
        string settingsFile = Path.Combine(_root, "settings.json");
        string backupFile = Path.Combine(_root, "settings.bak");
        string asideFile = Path.Combine(_root, "settings.unreadable.json");
        await File.WriteAllTextAsync(settingsFile, Unreadable);
        await File.WriteAllTextAsync(backupFile, Good);
        using DiskLibrarySession session = OpenSession();
        await session.LoadAsync(CancellationToken.None);
        Assert.AreEqual(7, session.Settings.FileDrawerSuggestionThreshold, "The load fell back to settings.bak.");

        Assert.IsTrue(await session.SaveSettingsAsync());

        Assert.AreEqual(Good, await File.ReadAllTextAsync(backupFile),
            "settings.bak no longer holds the good backup: the unreadable settings.json was kept as the backup over it.");
        Assert.IsTrue(File.Exists(asideFile), "The unreadable settings.json was not set aside as settings.unreadable.json.");
        Assert.AreEqual(Unreadable, await File.ReadAllTextAsync(asideFile));
        using DiskLibrarySession again = OpenSession();
        await again.LoadAsync(CancellationToken.None);
        Assert.AreEqual(7, again.Settings.FileDrawerSuggestionThreshold, "The saved settings.json is readable and holds the settings in use.");
    }

    // On the interface thread, held during a load (critic S3): requests made
    // while a load runs write nothing then, and one write runs when it ends.
    // One write only: a second would have kept the first as settings.bak.
    [TestMethod]
    public async Task ASettingsSaveDuringALoadIsWrittenOnceAfterIt()
    {
        string settingsFile = Path.Combine(_root, "settings.json");
        using var held = new HeldLoad(_root);
        Task<bool> load = held.StartLoad();

        Task<bool> first = held.Session.SaveSettingsAsync();
        Task<bool> second = held.Session.SaveSettingsAsync();

        Assert.IsFalse(first.IsCompleted, "A settings save during a load is held until the load ends.");
        Assert.IsFalse(second.IsCompleted, "A settings save during a load is held until the load ends.");
        Assert.IsFalse(File.Exists(settingsFile), "A settings save wrote while the load ran.");

        held.Release();
        Assert.IsTrue(await load);
        Assert.IsTrue(await first, "The held save was written when the load ended.");
        Assert.IsTrue(await second, "The held save was written when the load ended.");
        Assert.IsTrue(File.Exists(settingsFile), "The held save wrote no settings.json.");
        Assert.IsFalse(File.Exists(Path.Combine(_root, "settings.bak")), "The held requests were written more than once.");
    }

    // A load replaces Settings with what it read (ILibrarySession), so a
    // change made to Settings while a load runs is lost, and the save held
    // during that load writes the loaded settings. A caller such as the rail's
    // collapse toggle (B12) changes Settings after LibraryChanged, or applies
    // its change again then.
    [TestMethod]
    public async Task ASettingsChangeDuringALoadIsReplacedByTheLoad()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "settings.json"), "{ \"NavigationCollapsed\": false }");
        using var held = new HeldLoad(_root);
        Task<bool> load = held.StartLoad();
        AppSettings during = held.Session.Settings;
        during.NavigationCollapsed = true;
        Task<bool> save = held.Session.SaveSettingsAsync();

        held.Release();
        Assert.IsTrue(await load);
        Assert.IsTrue(await save, "The held save was written when the load ended.");
        Assert.AreNotSame(during, held.Session.Settings, "The load replaces Settings with what it read.");
        Assert.IsFalse(held.Session.Settings.NavigationCollapsed, "The settings in use are the loaded ones, without the change made during the load.");
        using DiskLibrarySession again = OpenSession();
        await again.LoadAsync(CancellationToken.None);
        Assert.IsFalse(again.Settings.NavigationCollapsed, "The held save wrote the loaded settings, not the change made during the load.");
    }

    // With no load running, each save is written at once on the caller's
    // thread and reads Settings as it is then, so the last request wins.
    [TestMethod]
    public async Task TwoQuickSettingsSavesRememberTheLast()
    {
        using DiskLibrarySession session = OpenSession();
        await session.LoadAsync(CancellationToken.None);

        session.Settings.NavigationCollapsed = true;
        Task<bool> first = session.SaveSettingsAsync();
        session.Settings.NavigationCollapsed = false;
        Task<bool> second = session.SaveSettingsAsync();

        Assert.IsTrue(first.IsCompleted && second.IsCompleted, "With no load running, a settings save is written at once.");
        Assert.IsTrue(await first);
        Assert.IsTrue(await second);
        using DiskLibrarySession again = OpenSession();
        await again.LoadAsync(CancellationToken.None);
        Assert.IsFalse(again.Settings.NavigationCollapsed, "The last request was not the one remembered.");
        using JsonDocument backup = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_root, "settings.bak")));
        Assert.IsTrue(backup.RootElement.GetProperty(nameof(AppSettings.NavigationCollapsed)).GetBoolean(),
            "The first request was written before the second and kept as settings.bak.");
    }

    // A write that fails returns false and throws nothing, as today's save
    // catches IOException and UnauthorizedAccessException
    // (Form1.Online.vb:117). The failure is a file where the settings folder
    // should be, so the write cannot create its folder.
    [TestMethod]
    public async Task AFailedSettingsSaveReturnsFalse()
    {
        string settingsFolder = Path.Combine(_root, "settings");
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            settingsFolder,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        await session.LoadAsync(CancellationToken.None);
        await File.WriteAllTextAsync(settingsFolder, "not a folder");
        session.Settings.NavigationCollapsed = true;

        Assert.IsFalse(await session.SaveSettingsAsync(), "A failed settings write returns false.");
        Assert.AreEqual("not a folder", await File.ReadAllTextAsync(settingsFolder), "The failed write changed the file in its way.");
    }

    // A snapshot writes nothing (critic N8): its settings save is refused and
    // no file in the copy or its source changes.
    [TestMethod]
    public async Task ASnapshotRefusesASettingsSave()
    {
        string source = Path.Combine(_root, "Lab library");
        string sessionFolder = Path.Combine(_root, "session");
        LargeLibraryFixture.WriteExample(source);
        SnapshotCopy copy = ReadOnlySnapshot.Copy(source, Path.Combine(sessionFolder, "current-data"), sessionFolder);
        using var session = new DiskLibrarySession(copy, Path.Combine(sessionFolder, "managed-library"), LargeLibraryFixture.Today);
        await session.LoadAsync(CancellationToken.None);
        IReadOnlyDictionary<string, string> before = FileState(_root);
        session.Settings.NavigationCollapsed = !session.Settings.NavigationCollapsed;

        Assert.IsFalse(await session.SaveSettingsAsync(), "A snapshot's settings save is refused.");
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "A snapshot's settings save wrote a file.");
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

    // The held save is written on the interface thread (critic S3), not on
    // the load's worker. Under an interface thread the test drives, the worker
    // ends and nothing is written until the test runs what was posted to that
    // thread; the write is done by the time those callbacks return.
    [TestMethod]
    public async Task AHeldSettingsSaveIsWrittenOnTheCallersContext()
    {
        string settingsFile = Path.Combine(_root, "settings.json");
        var ui = new QueuedContext();
        using var held = new HeldLoad(_root);
        (Task<bool> load, Task<bool> save) = ui.Enter(() => (held.StartLoad(), held.Session.SaveSettingsAsync()));

        held.Release();
        held.Session.WaitForWork();
        Assert.IsFalse(File.Exists(settingsFile), "The held save was written before the interface thread ran: it ran on another thread.");
        Assert.IsFalse(save.IsCompleted, "The held save finished before the interface thread ran.");

        Assert.IsGreaterThan(0, ui.RunPosted(TimeSpan.FromSeconds(30)), "The load posted nothing to the interface thread.");
        Assert.IsTrue(save.IsCompleted, "The held save was not done when the interface thread's callbacks returned: it ran on another thread.");
        Assert.IsTrue(File.Exists(settingsFile), "The held save wrote no settings.json on the interface thread.");
        Assert.IsTrue(await save);
        Assert.IsTrue(await load);
    }

    // The shutdown wait on the interface thread (shell design 2.5, B5's
    // close path): it waits on the worker, never on the publish posted to the
    // interface thread, so it returns while that thread runs nothing.
    [TestMethod]
    public async Task WaitForWorkReturnsWhileTheInterfaceThreadIsBlocked()
    {
        var ui = new QueuedContext();
        using var held = new HeldLoad(_root);
        Task<bool> load = ui.Enter(held.StartLoad);
        Task<bool> waiting = Task.Run(held.Session.WaitForWork);
        Assert.IsFalse(waiting.Wait(TimeSpan.FromMilliseconds(300)), "WaitForWork returned while the worker was still held.");

        held.Release();
        Assert.IsTrue(waiting.Wait(TimeSpan.FromSeconds(30)),
            "WaitForWork did not return while the interface thread ran nothing: it waits for the publish, and would deadlock the shutdown path.");
        Assert.IsTrue(waiting.Result, "WaitForWork waited for a running worker.");
        Assert.IsFalse(load.IsCompleted, "The load publishes on the interface thread only.");

        Assert.IsGreaterThan(0, ui.RunPosted(TimeSpan.FromSeconds(30)), "The load posted nothing to the interface thread.");
        Assert.IsTrue(await load);
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

    // Every file under root, by its path relative to root ("/" between
    // folders), with a hash of its bytes.
    private static IReadOnlyDictionary<string, string> FileState(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(root, file).Replace('\\', '/'),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
            StringComparer.Ordinal);

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

    // An interface thread the test drives: what is posted to it waits in a
    // queue until the test runs it on its own thread, so a test can tell work
    // done on the caller's context from work done on a worker.
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        private int Pending
        {
            get
            {
                lock (_posted)
                {
                    return _posted.Count;
                }
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_posted)
            {
                _posted.Enqueue((d, state));
            }
        }

        public override SynchronizationContext CreateCopy() => this;

        // Runs start with this as the current context, as on the interface
        // thread, so the awaits it begins come back here.
        public T Enter<T>(Func<T> start)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try
            {
                return start();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        // Waits for a first post, then runs on this thread the callbacks
        // queued at that moment (not those they post) and returns how many.
        public int RunPosted(TimeSpan wait)
        {
            if (!SpinWait.SpinUntil(() => Pending > 0, wait))
            {
                return 0;
            }

            int count = Pending;
            for (int i = 0; i < count; i++)
            {
                (SendOrPostCallback callback, object? state) next;
                lock (_posted)
                {
                    next = _posted.Dequeue();
                }

                Enter(() =>
                {
                    next.callback(next.state);
                    return 0;
                });
            }

            return count;
        }
    }
}
