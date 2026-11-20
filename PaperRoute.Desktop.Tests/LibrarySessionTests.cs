using System.Reflection;
using Avalonia.Controls;
using ManuscriptPipeline.Services;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
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
        var shell = new ShellViewModel(session);

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

    // The window binds its notice to the shell (compiled binding, x:DataType).
    [TestMethod]
    public void TheWindowShowsTheShellsNotice()
    {
        using DiskLibrarySession session = OpenSession();
        var shell = new ShellViewModel(session);

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

    private DiskLibrarySession OpenSession() => new(
        Path.Combine(_root, "data"),
        Path.Combine(_root, "library"),
        _root,
        LibraryKind.Example,
        LargeLibraryFixture.Today);
}
