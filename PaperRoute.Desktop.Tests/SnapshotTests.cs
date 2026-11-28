using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The copier from a folder (d1 section 8, step 11b; d1 1.3 lock 2; rule S1 of
// 6.2): ReadOnlySnapshot, "--snapshot-from <folder>", "--today", the snapshot
// banner and window title.
//
// Every library here is built by the test in a temporary folder of its own:
// the example (7 manuscripts) or the 500 library of LargeLibraryFixture. The
// in-process tests never call TrialStartup (d1 6.1): they copy into a
// stand-in session folder and read the copy through DiskLibrarySession, under
// the assembly's storage guard. S1 itself runs in a child process, through
// "--snapshot-from <folder> --self-check", as the design says, because a real
// snapshot needs the process's isolated session. Each source folder is
// hashed, file by file, before and after.
//
// The refusal of the real profile's storage folders is checked on paths only:
// the folder named does not exist, the refusal comes before anything is read,
// and no test reads or lists anything in a default storage folder.
[TestClass]
public sealed class SnapshotTests : StorageCheckedTests
{
    private const string FiveHundredTabs = "Pipeline (167), Published (272), File Drawer (61)";

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    private string Source => Path.Combine(_root, "Lab library");

    private string StandInSession => Path.Combine(_root, "session");

    private string Destination => Path.Combine(StandInSession, "current-data");

    // The folder and clock the snapshot picture's banner shows (see
    // TheBoardOnACopiedLibraryIsSavedWithTheSnapshotBanner). The folder is
    // only named, never made or read; its name is long enough that the
    // banner wraps to a second line, as a real library's folder often does.
    private static readonly string PictureSource = Path.Combine(
        Path.GetPathRoot(Path.GetTempPath()) ?? Path.DirectorySeparatorChar.ToString(),
        "Libraries", "Shared lab drive", "Research group", "Manuscripts in progress", "2026 submissions", "Lab library");

    private static readonly DateTime PictureTakenAt = new(2026, 11, 20, 9, 41, 0, DateTimeKind.Local);

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialSnapshot-" + Guid.NewGuid().ToString("N"));
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

    // ---- S1, in a child process ----

    [TestMethod]
    public void S1_TheFiveHundredLibraryIsCopiedIntoTheSessionAndItsFolderIsUnchanged()
    {
        WriteLibrary(500);
        string[] before = SourceState(Source);

        SessionStartupProcessTests.SelfCheckRun run = SessionStartupProcessTests.RunTrial(
            "--snapshot-from", Source, "--today", "2026-10-16", "--self-check");
        string report = run.Describe();

        AssertSnapshotSelfCheckPassed(run, report);
        Assert.Contains("PASS snapshot library: loaded 500 manuscript(s) from the copy: Pipeline 167, Published 272, FileDrawer 61", run.Lines, report);
        Assert.IsTrue(run.Lines.Any(line => line.StartsWith("PASS board render: 1 render(s); " + FiveHundredTabs, StringComparison.Ordinal)),
            "The Board shows the copied library's counts.\n" + report);
        Assert.Contains("PASS startup notice: Loaded 500 manuscripts from local storage.", run.Lines, report);
        CollectionAssert.AreEqual(before, SourceState(Source), "The source folder changed during the snapshot.\n" + report);
    }

    [TestMethod]
    public void S1_TheExampleIsCopiedIntoTheSessionAndItsFolderIsUnchanged()
    {
        LargeLibraryFixture.WriteExample(Source);
        File.WriteAllText(Path.Combine(Source, "settings.json"), "{}");
        string[] before = SourceState(Source);

        SessionStartupProcessTests.SelfCheckRun run = SessionStartupProcessTests.RunTrial(
            "--snapshot-from", Source, "--self-check");
        string report = run.Describe();

        AssertSnapshotSelfCheckPassed(run, report);
        string[] present = PresentLibraryFiles();
        Assert.Contains("settings.json", present);
        Assert.IsTrue(run.Lines.Any(line => line.StartsWith($"PASS snapshot copy: copied {present.Length} file(s) ({string.Join(", ", present)}) from {Source} into ", StringComparison.Ordinal)),
            "Every library file the folder holds is copied.\n" + report);
        Assert.IsTrue(run.Lines.Any(line => line.StartsWith("PASS snapshot library: loaded 7 manuscript(s) from the copy: ", StringComparison.Ordinal)), report);
        CollectionAssert.AreEqual(before, SourceState(Source), "The source folder changed during the snapshot.\n" + report);
    }

    // A source inside the real profile's default storage folders is refused
    // with a plain message, before a session is started or anything is read.
    // The folders named do not exist; nothing in a default folder is read.
    [TestMethod]
    public void ARefusedSourceGivesTheMessageAndOpensNothing()
    {
        string probe = "trial-refusal-probe-" + Guid.NewGuid().ToString("N");

        // A system that reports no Documents folder (a Linux CI runner) has no
        // default managed-library folder to refuse, as in DefaultStorageRoots.
        var sources = new List<string>();
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localData))
        {
            sources.Add(Path.Combine(localData, "PaperRoute", probe));
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documents))
        {
            sources.Add(Path.Combine(documents, "PaperRoute Library", probe));
        }

        Assert.IsNotEmpty(sources, "This system reports neither a local data folder nor a Documents folder.");

        foreach (string source in sources)
        {
            string[] sessionsBefore = TrialSessionFolders();
            SessionStartupProcessTests.SelfCheckRun run = SessionStartupProcessTests.RunTrial(
                "--snapshot-from", source, "--self-check");
            string report = run.Describe();

            Assert.AreEqual(2, run.ExitCode, report);
            StringAssert.Contains(run.Errors, "The Board trial does not open your own PaperRoute library.", report);
            StringAssert.Contains(run.Errors, source, report);
            Assert.IsFalse(run.Lines.Any(line => line.StartsWith("session folder: ", StringComparison.Ordinal)), "No session was started.\n" + report);
            Assert.IsEmpty(run.Lines, "The trial printed nothing else: it opened nothing.\n" + report);
            CollectionAssert.AreEquivalent(sessionsBefore, TrialSessionFolders(), "No snapshot session folder was made.\n" + report);
            Assert.IsFalse(Directory.Exists(source), "The probe folder was not created.");
        }
    }

    // "--snapshot" (the real profile, d1 step 14) is not built; "--today"
    // without "--snapshot-from" is refused (d1 1.3 Modes). Neither opens anything.
    [TestMethod]
    public void TheRealProfileModeAndALoneTodayAreRefused()
    {
        string[] sessionsBefore = TrialSessionFolders();

        SessionStartupProcessTests.SelfCheckRun snapshot = SessionStartupProcessTests.RunTrial("--snapshot", "--self-check");
        Assert.AreEqual(2, snapshot.ExitCode, snapshot.Describe());
        StringAssert.Contains(snapshot.Errors, "\"--snapshot\" (a copy of your own PaperRoute library) is not part of this trial build.", snapshot.Describe());
        Assert.IsEmpty(snapshot.Lines, snapshot.Describe());

        SessionStartupProcessTests.SelfCheckRun today = SessionStartupProcessTests.RunTrial("--today", "2026-10-16", "--self-check");
        Assert.AreEqual(2, today.ExitCode, today.Describe());
        StringAssert.Contains(today.Errors, "\"--today\" is accepted only with \"--snapshot-from\".", today.Describe());
        Assert.IsEmpty(today.Lines, today.Describe());

        CollectionAssert.AreEquivalent(sessionsBefore, TrialSessionFolders(), "No snapshot session folder was made.");
    }

    // ---- The copier, in process ----

    [TestMethod]
    public void TheCopierCopiesTheLibraryFilesIntoTheSessionAndLeavesTheSourceUnchanged()
    {
        WriteLibrary(500);
        File.WriteAllText(Path.Combine(Source, "settings.bak"), "{}");
        string[] before = SourceState(Source);

        SnapshotCopy copy = ReadOnlySnapshot.Copy(Source, Destination, StandInSession);

        CollectionAssert.AreEqual(before, SourceState(Source), "The copier changed the source folder.");
        Assert.AreEqual(Source, copy.SourceFolder);
        Assert.AreEqual(Destination, copy.DestinationFolder);
        string[] present = PresentLibraryFiles();
        CollectionAssert.IsSubsetOf(
            new[] { "settings.json", "settings.bak", Path.Combine("data", "manuscripts.json"), Path.Combine("data", "authors.json") },
            present);
        CollectionAssert.AreEqual(present, copy.Copied.ToArray());
        Assert.IsEmpty(copy.Skipped);
        foreach (string name in copy.Copied)
        {
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(Source, name)), File.ReadAllBytes(Path.Combine(Destination, name)), name);
        }

        // Only the library's files: the managed library (attachments) is not copied.
        string[] written = [.. Directory.EnumerateFiles(StandInSession, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(Destination, path)).Order(StringComparer.Ordinal)];
        CollectionAssert.AreEqual(copy.Copied.Order(StringComparer.Ordinal).ToArray(), written);
        Assert.IsTrue(Directory.EnumerateFiles(Path.Combine(Source, "library"), "*", SearchOption.AllDirectories).Any(), "The 500 library has managed files to leave behind.");
    }

    // A file that another program holds open for writing, with sharing (as a
    // running PaperRoute does), is still copied; a file held without sharing
    // is retried, then skipped and named. Neither is changed.
    [TestMethod]
    public void AHeldFileIsCopiedAndALockedFileIsSkippedAndNamed()
    {
        LargeLibraryFixture.WriteExample(Source);
        string manuscripts = Path.Combine(Source, "data", "manuscripts.json");
        string authors = Path.Combine(Source, "data", "authors.json");
        string[] before = SourceState(Source);

        SnapshotCopy copy;
        using (new FileStream(manuscripts, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        using (new FileStream(authors, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            copy = ReadOnlySnapshot.Copy(Source, Destination, StandInSession);
        }

        CollectionAssert.Contains(copy.Copied.ToArray(), Path.Combine("data", "manuscripts.json"));
        CollectionAssert.AreEqual(new[] { Path.Combine("data", "authors.json") }, copy.Skipped.ToArray());
        CollectionAssert.AreEqual(PresentLibraryFiles().Where(name => name != Path.Combine("data", "authors.json")).ToArray(), copy.Copied.ToArray());
        CollectionAssert.AreEqual(before, SourceState(Source), "The copier changed the source folder.");

        using var session = new DiskLibrarySession(copy, Path.Combine(StandInSession, "managed-library"), LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);
        Assert.AreEqual("Loaded 7 manuscripts from local storage. This file could not be read, so it was not copied: " + Path.Combine("data", "authors.json") + ".", session.StartupNotice);
    }

    // When the manuscripts file itself cannot be read (PaperRoute saving it
    // without sharing, d1 section 9), the line says the copy failed, not
    // "Loaded 0 manuscripts", and names the other files skipped after it.
    [TestMethod]
    public void ALockedManuscriptsFileIsReportedAsAFailedCopyNotAnEmptyLibrary()
    {
        LargeLibraryFixture.WriteExample(Source);
        string manuscripts = Path.Combine(Source, "data", "manuscripts.json");
        string[] before = SourceState(Source);

        SnapshotCopy copy;
        using (new FileStream(manuscripts, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            copy = ReadOnlySnapshot.Copy(Source, Destination, StandInSession);
        }

        CollectionAssert.AreEqual(before, SourceState(Source), "The copier changed the source folder.");
        CollectionAssert.Contains(copy.Skipped.ToArray(), Path.Combine("data", "manuscripts.json"));
        using var session = new DiskLibrarySession(copy, Path.Combine(StandInSession, "managed-library"), LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.IsEmpty(session.Manuscripts);
        Assert.AreEqual(
            "The snapshot could not copy the manuscripts file (" + Path.Combine("data", "manuscripts.json") + "): another program may have been using it. Try again.",
            session.StartupNotice);

        // The rule for the line, with the previous save and other files.
        string json = Path.Combine("data", "manuscripts.json");
        string authors = Path.Combine("data", "authors.json");
        string schema = "schema.json";
        Assert.AreEqual(
            "The snapshot could not copy the manuscripts file (" + json + "): another program may have been using it. Try again. " +
            "The Board shows the previous save, from " + Path.Combine("data", "manuscripts.bak") + ". " +
            "These files could not be read, so they were not copied: " + schema + ", " + authors + ".",
            DiskLibrarySession.SkippedNotice("Opened the previous save of the library - 9:00 AM", [schema, json, authors], 3));
        Assert.AreEqual(
            "Loaded 2 manuscripts from local storage. These files could not be read, so they were not copied: " + schema + ", " + authors + ".",
            DiskLibrarySession.SkippedNotice("Loaded 2 manuscripts from local storage.", [schema, authors], 2));
    }

    [TestMethod]
    public void TheCopierRefusesADestinationOutsideTheSessionAndWritesNothing()
    {
        LargeLibraryFixture.WriteExample(Source);
        string[] before = SourceState(Source);
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(StandInSession);

        var refused = Assert.ThrowsExactly<SnapshotRefusedException>(() => ReadOnlySnapshot.Copy(Source, outside, StandInSession));
        StringAssert.StartsWith(refused.Message, "The snapshot writes only inside the trial's session folder");
        Assert.IsFalse(Directory.Exists(outside), "Nothing was written outside the session.");

        // The session folder itself, a destination inside the source, and a
        // source inside the session are refused too.
        Assert.ThrowsExactly<SnapshotRefusedException>(() => ReadOnlySnapshot.Copy(Source, StandInSession, StandInSession));
        Assert.ThrowsExactly<SnapshotRefusedException>(() => ReadOnlySnapshot.Copy(Source, Path.Combine(Source, "copy"), Source));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(StandInSession));
        CollectionAssert.AreEqual(before, SourceState(Source));
    }

    // The refusal on a stand-in profile folder with a real library in it: the
    // message, and nothing read or written (the session stays empty, the
    // source unchanged). Then the real profile's default folders, on paths
    // only: they are refused before anything is looked at.
    [TestMethod]
    public void ASourceInsideAStorageFolderIsRefusedWithAPlainMessage()
    {
        string profile = Path.Combine(_root, "Profile", "PaperRoute");
        string inside = Path.Combine(profile, "Copied here");
        LargeLibraryFixture.WriteExample(inside);
        string[] before = SourceState(profile);
        Directory.CreateDirectory(StandInSession);

        foreach (string source in new[] { profile, inside, inside + Path.DirectorySeparatorChar })
        {
            string? message = ReadOnlySnapshot.RefusalFor(source, [profile]);
            Assert.IsNotNull(message, source);
            StringAssert.StartsWith(message, "The Board trial does not open your own PaperRoute library. The folder ");
            StringAssert.Contains(message, "Choose a library folder made for testing.");
            var refused = Assert.ThrowsExactly<SnapshotRefusedException>(() => ReadOnlySnapshot.Copy(source, Destination, StandInSession, [profile]));
            Assert.AreEqual(message, refused.Message);
        }

        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(StandInSession), "Nothing was copied.");

        // Other spellings of the same folders, which a string comparison with
        // "C:\..." would miss: the device prefixes, a loopback admin share and
        // the NTFS stream form. Each is refused as not a plain local path.
        // These spellings exist on Windows only.
        if (OperatingSystem.IsWindows())
        {
            string share = @"\\localhost\" + inside[0] + "$" + inside[2..];
            foreach (string source in new[]
            {
                @"\\?\" + inside, @"\\.\" + inside, @"\\?\" + profile, share, profile + "::$INDEX_ALLOCATION",
            })
            {
                string? message = ReadOnlySnapshot.RefusalFor(source, [profile]);
                Assert.AreEqual("The Board trial needs a local folder path such as C:\\Libraries\\Test.", message, source);
                var refused = Assert.ThrowsExactly<SnapshotRefusedException>(() => ReadOnlySnapshot.Copy(source, Destination, StandInSession, [profile]));
                Assert.AreEqual(message, refused.Message, source);
                Assert.IsEmpty(Directory.EnumerateFileSystemEntries(StandInSession), "Nothing was copied from " + source);
            }
        }

        CollectionAssert.AreEqual(before, SourceState(profile), "Nothing in the refused folder changed.");

        // The default roots: the data folder under the local data folder and
        // the managed library under Documents, each only when the system
        // reports that parent (a Linux CI runner reports no Documents folder).
        IReadOnlyList<string> defaults = ReadOnlySnapshot.DefaultStorageRoots();
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var expected = new List<string>();
        if (!string.IsNullOrEmpty(localData))
        {
            expected.Add(Path.Combine(localData, "PaperRoute"));
        }

        if (!string.IsNullOrEmpty(documents))
        {
            expected.Add(Path.Combine(documents, "PaperRoute Library"));
        }

        Assert.IsNotEmpty(expected, "This system reports neither a local data folder nor a Documents folder.");
        CollectionAssert.IsSubsetOf(
            expected,
            defaults.ToArray(),
            "The default data root and the default managed-library root are refused.");
        foreach (string root in defaults)
        {
            string? message = ReadOnlySnapshot.RefusalFor(Path.Combine(root, "data"));
            Assert.IsNotNull(message, root);
            StringAssert.Contains(message, "PaperRoute's storage folder " + root + ".");
        }
    }

    [TestMethod]
    public void AFolderThatIsNotALibraryIsRefused()
    {
        Directory.CreateDirectory(Source);
        Assert.AreEqual($"The folder {Source} holds no \"data\" folder, so it is not a PaperRoute library.", ReadOnlySnapshot.RefusalFor(Source));
        Assert.AreEqual($"The Board trial found no folder at {Path.Combine(_root, "missing")}.", ReadOnlySnapshot.RefusalFor(Path.Combine(_root, "missing")));
        StringAssert.StartsWith(ReadOnlySnapshot.RefusalFor("relative\\folder"), "The Board trial needs the full path of a library folder");
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(Source));
    }

    // ---- The command line ----

    [TestMethod]
    public void TheCommandLineReadsTheSnapshotModes()
    {
        Assert.IsTrue(TrialOptions.TryParse([], out TrialOptions? none, out _));
        Assert.IsNull(none.SnapshotFrom);
        Assert.IsNull(none.Today);
        Assert.IsFalse(none.SelfCheck);

        Assert.IsTrue(TrialOptions.TryParse(["--snapshot-from", @"C:\Libraries\Test", "--today", "2026-10-16", "--self-check"], out TrialOptions? all, out _));
        Assert.AreEqual(@"C:\Libraries\Test", all.SnapshotFrom);
        Assert.AreEqual(new DateTime(2026, 10, 16), all.Today);
        Assert.IsTrue(all.SelfCheck);

        Assert.IsTrue(TrialOptions.TryParse(["--self-check"], out TrialOptions? check, out _));
        Assert.IsTrue(check.SelfCheck);
        Assert.IsNull(check.SnapshotFrom);

        AssertRefused(["--snapshot"], "\"--snapshot\" (a copy of your own PaperRoute library) is not part of this trial build.");
        AssertRefused(["--today", "2026-10-16"], "\"--today\" is accepted only with \"--snapshot-from\".");
        AssertRefused(["--snapshot-from"], "\"--snapshot-from\" needs the folder of a library after it.");
        AssertRefused(["--snapshot-from", "--self-check"], "\"--snapshot-from\" needs the folder of a library after it.");
        AssertRefused(["--snapshot-from", "a", "--snapshot-from", "b"], "\"--snapshot-from\" is given twice.");
        AssertRefused(["--snapshot-from", "a", "--today", "16/10/2026"], "\"--today\" needs a date written yyyy-MM-dd, such as 2026-10-16.");
        AssertRefused(["--snapshot-from", "a", "--today"], "\"--today\" needs a date written yyyy-MM-dd, such as 2026-10-16.");
    }

    // ---- The banner and the window title ----

    [TestMethod]
    public void TheShellNamesTheFolderCopiedInItsTitleAndBanner()
    {
        LargeLibraryFixture.WriteExample(Source);
        SnapshotCopy copy = ReadOnlySnapshot.Copy(Source, Destination, StandInSession);
        using var session = new DiskLibrarySession(copy, Path.Combine(StandInSession, "managed-library"), LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        ShellViewModel shell = LibrarySessionTests.ShellOver(session);

        Assert.AreEqual(LibraryKind.Snapshot, session.Kind);
        Assert.AreEqual("PaperRoute Tracker - Read-only snapshot of " + Source, shell.WindowTitle);
        Assert.AreEqual("Read-only snapshot", shell.BannerName);
        Assert.AreEqual(
            "Read-only snapshot of the library in " + Source + ", taken at " + copy.TakenAt.ToString("h:mm tt", System.Globalization.CultureInfo.CurrentCulture) +
            ". Nothing you do here is saved; the trial shows a copy and never changes that folder.",
            shell.BannerText);

        // Before the copy is made, the banner names the folder without a time.
        Assert.AreEqual(
            "Read-only snapshot of the library in " + Source + ". Nothing you do here is saved; the trial shows a copy and never changes that folder.",
            ShellViewModel.SnapshotBannerText(Source, takenAt: null));

        // A snapshot is made only over its copy.
        Assert.ThrowsExactly<ArgumentException>(() => new DiskLibrarySession(
            Path.Combine(Destination, "data"), Path.Combine(StandInSession, "managed-library"), Destination, LibraryKind.Snapshot, LargeLibraryFixture.Today));
    }

    // The picture: the Board on a copied 500 library, with the snapshot
    // banner, in Light. The tabs show the copy's counts. The banner names a
    // fixed folder and a fixed clock instead of this run's random temporary
    // folder and the time of the copy, so that two runs of the suite make the
    // same bytes and a picture comparison sees a real change in the banner.
    // The banner's wording for a real copy is checked above.
    [TestMethod]
    public void TheBoardOnACopiedLibraryIsSavedWithTheSnapshotBanner()
    {
        WriteLibrary(500);
        SnapshotCopy made = ReadOnlySnapshot.Copy(Source, Destination, StandInSession);
        Assert.AreEqual(Source, made.SourceFolder);
        SnapshotCopy copy = made with { SourceFolder = PictureSource, TakenAt = PictureTakenAt };
        using var session = new DiskLibrarySession(copy, Path.Combine(StandInSession, "managed-library"), LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(500, session.Manuscripts);

        (string path, string title, string banner, string bannerName, string tabs, int shown) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            Dispatcher.UIThread.RunJobs();
            TextBlock bannerText = board.Window.FindControl<TextBlock>("BannerText")
                ?? throw new AssertFailedException("The window has no banner text.");
            Border bannerBox = board.Window.FindControl<Border>("Banner")
                ?? throw new AssertFailedException("The window has no banner.");
            string saved = Pictures.Save(board.Window, "board-snapshot-500-light", TestContext);
            return (saved, board.Window.Title ?? string.Empty, bannerText.Text ?? string.Empty,
                Avalonia.Automation.AutomationProperties.GetName(bannerBox) ?? string.Empty,
                string.Join(", ", board.Board.Shelves.Select(shelf => shelf.TabText)),
                board.Board.Shelves[0].Cards.Count);
        });

        TestContext.WriteLine($"Title: {title}\nBanner: {banner}\nTabs: {tabs}");
        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
        Assert.AreEqual(ShellViewModel.SnapshotWindowTitle(PictureSource), title);
        Assert.AreEqual(ShellViewModel.SnapshotBannerText(PictureSource, PictureTakenAt), banner);
        Assert.IsFalse(banner.Contains(_root, StringComparison.OrdinalIgnoreCase), $"The picture's banner names this run's temporary folder: {banner}");
        Assert.AreEqual("Read-only snapshot", bannerName);
        Assert.AreEqual(FiveHundredTabs, tabs);
        Assert.AreEqual(167, shown);
    }

    // ---- Helpers ----

    private static void AssertRefused(string[] args, string message)
    {
        Assert.IsFalse(TrialOptions.TryParse(args, out TrialOptions? options, out string? error), string.Join(" ", args));
        Assert.IsNull(options);
        Assert.IsNotNull(error);
        Assert.StartsWith(message, error);
    }

    private static void AssertSnapshotSelfCheckPassed(SessionStartupProcessTests.SelfCheckRun run, string report)
    {
        Assert.AreEqual(0, run.ExitCode, report);
        Assert.IsFalse(run.Lines.Any(line => line.StartsWith("FAIL ", StringComparison.Ordinal)), report);
        Assert.Contains("self-check passed", run.Lines, report);
        foreach (string check in new[]
        {
            "snapshot copy", "snapshot library", "snapshot destination refused", "startup notice", "board render",
            "board search", "board chip", "board clear", "isolated session", "session root", "data root",
            "managed library root", "legacy data root", "legacy managed library root",
        })
        {
            Assert.IsTrue(run.Lines.Any(line => line.StartsWith($"PASS {check}: ", StringComparison.Ordinal)),
                $"No passing \"{check}\" line.\n{report}");
        }

        Assert.Contains("PASS default roots: 0 default root(s) resolved", run.Lines, report);

        // The session was a new PaperRoute-Trial- child of the temporary
        // folder, the copy lay inside it, and the exit removed it.
        string? line = run.Lines.FirstOrDefault(item => item.StartsWith("session folder: ", StringComparison.Ordinal));
        Assert.IsNotNull(line, report);
        string folder = line["session folder: ".Length..];
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.AreEqual(temporary, Path.GetDirectoryName(folder), true, report);
        Assert.StartsWith(TrialStartup.SnapshotFolderPrefix, Path.GetFileName(folder), report);
        string copyLine = run.Lines.First(item => item.StartsWith("PASS snapshot copy: ", StringComparison.Ordinal));
        StringAssert.Contains(copyLine, " into " + Path.Combine(folder, "current-data") + "; skipped none", report);
        Assert.IsFalse(Directory.Exists(folder), $"The session folder {folder} is still there.\n{report}");
        Assert.Contains($"PASS session folder removed: {folder}", run.Lines, report);
    }

    // The library files the source holds, in the copier's order.
    private string[] PresentLibraryFiles() =>
        [.. ReadOnlySnapshot.FileNames.Where(name => File.Exists(Path.Combine(Source, name)))];

    private void WriteLibrary(int count)
    {
        LargeLibraryFixture.Create(Source, count);
        File.WriteAllText(Path.Combine(Source, "settings.json"), "{}");
    }

    // Every file (path, size, last write, SHA-256) and folder (path, last
    // write) under root, sorted: equal lists mean nothing was changed, added or
    // removed there.
    private static string[] SourceState(string root)
    {
        var entries = new List<string>();
        foreach (string folder in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).Append(root))
        {
            var info = new DirectoryInfo(folder);
            entries.Add($"D|{Path.GetRelativePath(root, folder)}|{info.LastWriteTimeUtc.Ticks}");
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            using FileStream stream = File.OpenRead(file);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            entries.Add($"F|{Path.GetRelativePath(root, file)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{hash}");
        }

        entries.Sort(StringComparer.Ordinal);
        return [.. entries];
    }

    private static string[] TrialSessionFolders() =>
        [.. Directory.EnumerateDirectories(Path.GetTempPath(), TrialStartup.SnapshotFolderPrefix + "*")];
}
