namespace PaperRoute.ViewModels.Session;

// How the trial starts and ends its storage session (d1 1.3 lock 1, 3.1).
//
// StartExample is the first call Main makes, before any other call into the
// core: ExampleLibraryService.StartSession points every storage root the
// process can resolve into a new <temp>\PaperRoute-Example-<guid> folder, so no
// default constructor anywhere in the process can reach %LOCALAPPDATA%\PaperRoute
// or the user's Documents library. End deletes that folder. StartSnapshot
// (step 11b) does the same with a new <temp>\PaperRoute-Trial-<guid> folder
// and loads a copy of a library folder a person named (ReadOnlySnapshot).
//
// No in-process test calls this class (d1 6.1): a session can be set once per
// process, and only before any root is resolved. The tests run it through
// "--self-check" in a child process.
public static class TrialStartup
{
    public const string SelfCheckArgument = "--self-check";

    private const string ExampleFolderPrefix = "PaperRoute-Example-";

    // A snapshot's session folder (d1 1.3 lock 1): <temp>\PaperRoute-Trial-<guid>.
    public const string SnapshotFolderPrefix = "PaperRoute-Trial-";

    private static readonly Lock s_gate = new();
    private static string? s_sessionRoot;
    private static LibraryKind s_kind;
    private static bool s_ended;

    // The session folder, once StartExample has run.
    public static string? SessionRoot
    {
        get
        {
            lock (s_gate)
            {
                return s_sessionRoot;
            }
        }
    }

    // Starts the example's isolated session synchronously and returns its
    // library session. The example is written later, off the interface
    // thread, at the start of the session's LoadAsync (d1 3.4).
    public static ILibrarySession StartExample(DateTime today)
    {
        lock (s_gate)
        {
            if (s_sessionRoot is not null)
            {
                throw new InvalidOperationException("The trial's storage session has already started.");
            }

            // Lock 1. It throws if any storage root was resolved before it.
            ExampleLibraryService.StartSession();

            string dataRoot = StorageMigrationService.CurrentDataRoot();
            s_sessionRoot = Path.GetDirectoryName(dataRoot)
                ?? throw new InvalidOperationException("The example session has no folder.");
            s_kind = LibraryKind.Example;

            DateTime day = today.Date;
            return new DiskLibrarySession(
                Path.Combine(dataRoot, "data"),
                StorageMigrationService.CurrentManagedLibraryRoot(),
                dataRoot,
                LibraryKind.Example,
                day,
                prepare: () => SeedExample(day),
                snapshot: null);
        }
    }

    // Starts a read-only snapshot of the library in sourceFolder (d1 3.1,
    // step 11b): refuses the folder first (ReadOnlySnapshot.RefusalFor, which
    // reads nothing in a default storage folder), then sets a new
    // <temp>\PaperRoute-Trial-<guid> as the isolated session, synchronously.
    // The copy is made later, off the interface thread, at the start of the
    // session's LoadAsync, into the session's data root; the session reads
    // only that copy. Throws SnapshotRefusedException, with no session
    // started, for a refused folder.
    public static ILibrarySession StartSnapshot(string sourceFolder, DateTime today)
    {
        lock (s_gate)
        {
            if (s_sessionRoot is not null)
            {
                throw new InvalidOperationException("The trial's storage session has already started.");
            }

            string? refusal = ReadOnlySnapshot.RefusalFor(sourceFolder);
            if (refusal is not null)
            {
                throw new SnapshotRefusedException(refusal);
            }

            var snapshot = new LibrarySnapshot(sourceFolder);
            string root = Path.Combine(Path.GetTempPath(), SnapshotFolderPrefix + Guid.NewGuid().ToString("N"));

            // Lock 1. It throws if any storage root was resolved before it.
            StorageEnvironment.ConfigureIsolatedSessionRoot(root);
            s_sessionRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            s_kind = LibraryKind.Snapshot;

            string dataRoot = StorageMigrationService.CurrentDataRoot();
            string session = s_sessionRoot;
            return new DiskLibrarySession(
                Path.Combine(dataRoot, ReadOnlySnapshot.DataFolderName),
                StorageMigrationService.CurrentManagedLibraryRoot(),
                dataRoot,
                LibraryKind.Snapshot,
                today.Date,
                prepare: () => snapshot.Took(ReadOnlySnapshot.Copy(snapshot.SourceFolder, dataRoot, session)),
                snapshot: snapshot);
        }
    }

    // Ends the session: the example's folder is deleted, or the snapshot's.
    // Safe to call twice, and before a start.
    public static void End()
    {
        lock (s_gate)
        {
            if (s_sessionRoot is null || s_ended)
            {
                return;
            }

            s_ended = true;
            if (s_kind == LibraryKind.Example)
            {
                ExampleLibraryService.EndSession();
            }
            else
            {
                DeleteSnapshotFolder(s_sessionRoot);
            }
        }
    }

    // Deletes only a direct child of the temporary folder named
    // PaperRoute-Trial-; one it cannot delete is left for Windows'
    // temporary-file clean-up, as ExampleLibraryService.EndSession does.
    private static void DeleteSnapshotFolder(string root)
    {
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(root), temporary, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).StartsWith(SnapshotFolderPrefix, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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

    // Rule S2 (d1 6.2): true only if the session is isolated, its folder is a
    // PaperRoute-Example- (or, for a snapshot, PaperRoute-Trial-) child of the
    // temporary folder, every root the core
    // can resolve lies inside it and none inside a default location, and no
    // default root has been resolved. Writes one line per check.
    public static bool SelfCheck(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        bool isolated = StorageEnvironment.IsIsolatedSession;
        bool passed = Report(output, isolated, "isolated session",
            isolated ? "every storage root resolves inside the session" : "no isolated session is set");

        string? root = SessionRoot;
        if (!isolated || root is null)
        {
            // Resolving a root now would use the default location.
            Report(output, false, "session root", root is null ? "the trial started no session" : root);
            return false;
        }

        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        bool rootPlaced =
            string.Equals(Path.GetDirectoryName(root), temporary, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(root).StartsWith(s_kind == LibraryKind.Example ? ExampleFolderPrefix : SnapshotFolderPrefix, StringComparison.Ordinal) &&
            Directory.Exists(root);
        passed &= Report(output, rootPlaced, "session root", root);

        string[] defaults = DefaultLocations();
        (string Name, Func<string> Resolve)[] roots =
        [
            ("data root", StorageMigrationService.CurrentDataRoot),
            ("managed library root", StorageMigrationService.CurrentManagedLibraryRoot),
            ("legacy data root", StorageMigrationService.LegacyDataRoot),
            ("legacy managed library root", StorageMigrationService.LegacyManagedLibraryRoot),
        ];
        foreach ((string name, Func<string> resolve) in roots)
        {
            string path = Path.GetFullPath(resolve());
            bool inside = IsInside(path, root) && !defaults.Any(location => IsInside(path, location));
            passed &= Report(output, inside, name, path);
        }

        int uses = StorageEnvironment.DefaultRootUses;
        passed &= Report(output, uses == 0, "default roots", $"{uses} default root(s) resolved");
        return passed;
    }

    // The "--self-check" mode (d1 1.3 Modes): with no window, loads the
    // session StartExample or StartSnapshot returned, checks the example
    // arrived whole (or the snapshot's copy, CheckSnapshot), renders
    // its Board and runs each filter on it (BoardSelfCheck), runs SelfCheck,
    // ends the session and checks its folder is gone. Returns the process
    // exit code: 0 when every check passed, else 1.
    public static int RunSelfCheck(ILibrarySession session, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);

        bool passed = true;
        string? root = SessionRoot;
        output.WriteLine($"session folder: {root}");
        try
        {
            var board = new BoardSelfCheck(session, output);

            // No synchronization context here, so blocking cannot deadlock.
            session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            int loaded = session.Manuscripts.Count;
            if (session.Kind == LibraryKind.Example)
            {
                int expected = ExampleLibraryService.Create(session.Today).Manuscripts.Count;
                passed &= Report(output, loaded > 0 && loaded == expected, "example library",
                    $"loaded {loaded} manuscript(s); the example holds {expected}");
            }
            else
            {
                passed &= CheckSnapshot(session, root, output);
            }

            passed &= Report(output, session.StartupNotice == DiskLibrarySession.LoadedText(loaded), "startup notice",
                session.StartupNotice);

            // The Board's renders and filters first, so the storage checks
            // that follow also cover them.
            passed &= board.Run();
            passed &= SelfCheck(output);
        }
        catch (Exception ex)
        {
            passed = Report(output, false, "exception", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            session.Dispose();
            End();
        }

        bool removed = root is not null && !Directory.Exists(root);
        passed &= Report(output, removed, "session folder removed", root ?? "(none)");

        output.WriteLine(passed ? "self-check passed" : "self-check failed");
        output.Flush();
        return passed ? 0 : 1;
    }

    // Rule S1's in-session half (d1 6.2): the copy was made, whole, into the
    // session's data root and nowhere else; the library loaded is the copy's,
    // counted by shelf; and the copier refuses a destination outside the
    // session, creating nothing there. The source's own files are hashed by
    // the test that starts this process, before and after.
    private static bool CheckSnapshot(ILibrarySession session, string? root, TextWriter output)
    {
        SnapshotCopy? copy = session.Snapshot?.Copy;
        if (copy is null || root is null)
        {
            return Report(output, false, "snapshot copy", "no copy was made");
        }

        string dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(StorageMigrationService.CurrentDataRoot()));
        bool placed = string.Equals(copy.DestinationFolder, dataRoot, StringComparison.OrdinalIgnoreCase) &&
            IsInside(copy.DestinationFolder, root) &&
            !string.Equals(copy.DestinationFolder, copy.SourceFolder, StringComparison.OrdinalIgnoreCase);
        bool whole = copy.Skipped.Count == 0 && copy.Copied.Count > 0 &&
            copy.Copied.All(name => File.Exists(Path.Combine(copy.DestinationFolder, name)));
        bool passed = Report(output, placed && whole, "snapshot copy",
            $"copied {copy.Copied.Count} file(s) ({string.Join(", ", copy.Copied)}) from {copy.SourceFolder} into {copy.DestinationFolder}; " +
            (copy.Skipped.Count == 0 ? "skipped none" : "skipped " + string.Join(", ", copy.Skipped)));

        IReadOnlyList<Manuscript> library = session.Manuscripts;
        string shelves = string.Join(", ",
            new[] { ManuscriptLocation.Pipeline, ManuscriptLocation.Published, ManuscriptLocation.FileDrawer }
                .Select(location => $"{location} {library.Count(manuscript => manuscript.Location == location)}"));
        passed &= Report(output, library.Count > 0, "snapshot library", $"loaded {library.Count} manuscript(s) from the copy: {shelves}");

        string outside = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialOutside-" + Guid.NewGuid().ToString("N"));
        string refusal;
        try
        {
            ReadOnlySnapshot.Copy(copy.SourceFolder, outside, root);
            refusal = "(the copier accepted it)";
        }
        catch (SnapshotRefusedException ex)
        {
            refusal = ex.Message;
        }

        bool refused = refusal.StartsWith("The snapshot writes only inside", StringComparison.Ordinal) && !Directory.Exists(outside);
        passed &= Report(output, refused, "snapshot destination refused", refusal);
        return passed;
    }

    private static void SeedExample(DateTime today)
    {
        // One of lock 3's two allowed writes (d1 1.3; the other is the
        // session's settings save, DiskLibrarySession.WriteSettings): the
        // example's Seed saves its four files into the example's own session
        // folder, which StartExample set up and End deletes. Every other save
        // stays banned (BannedSymbols.txt).
#pragma warning disable RS0030
        ExampleLibraryService.Seed(today);
#pragma warning restore RS0030
    }

    // Where the core's default roots would be, worked out from the folder
    // names the way the core does, never through a root resolver (which would
    // answer with the session folder, or count a default use).
    private static string[] DefaultLocations()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var locations = new List<string>();
        if (!string.IsNullOrEmpty(localData))
        {
            locations.Add(Path.Combine(localData, StorageEnvironment.DataFolderName()));
            locations.Add(Path.Combine(localData, StorageEnvironment.LegacyDataFolderName()));
        }

        if (!string.IsNullOrEmpty(documents))
        {
            locations.Add(Path.Combine(documents, StorageEnvironment.ManagedLibraryFolderName()));
            locations.Add(Path.Combine(documents, StorageEnvironment.LegacyManagedLibraryFolderName()));
        }

        return [.. locations];
    }

    internal static bool IsInside(string path, string folder)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        return string.Equals(full, parent, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Report(TextWriter output, bool passed, string check, string detail)
    {
        output.WriteLine($"{(passed ? "PASS" : "FAIL")} {check}: {detail}");
        return passed;
    }
}
