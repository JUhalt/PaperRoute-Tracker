namespace PaperRoute.ViewModels.Session;

// How the trial starts and ends its storage session (d1 1.3 lock 1, 3.1).
//
// StartExample is the first call Main makes, before any other call into the
// core: ExampleLibraryService.StartSession points every storage root the
// process can resolve into a new <temp>\PaperRoute-Example-<guid> folder, so no
// default constructor anywhere in the process can reach %LOCALAPPDATA%\PaperRoute
// or the user's Documents library. End deletes that folder.
//
// No in-process test calls this class (d1 6.1): a session can be set once per
// process, and only before any root is resolved. The tests run it through
// "--self-check" in a child process.
public static class TrialStartup
{
    public const string SelfCheckArgument = "--self-check";

    private const string ExampleFolderPrefix = "PaperRoute-Example-";

    private static readonly Lock s_gate = new();
    private static string? s_sessionRoot;
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

            DateTime day = today.Date;
            return new DiskLibrarySession(
                Path.Combine(dataRoot, "data"),
                StorageMigrationService.CurrentManagedLibraryRoot(),
                dataRoot,
                LibraryKind.Example,
                day,
                prepare: () => SeedExample(day));
        }
    }

    // Ends the session: the example's folder is deleted. Safe to call twice,
    // and before a start.
    public static void End()
    {
        lock (s_gate)
        {
            if (s_sessionRoot is null || s_ended)
            {
                return;
            }

            s_ended = true;
            ExampleLibraryService.EndSession();
        }
    }

    // Rule S2 (d1 6.2): true only if the session is isolated, its folder is a
    // PaperRoute-Example- child of the temporary folder, every root the core
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
            Path.GetFileName(root).StartsWith(ExampleFolderPrefix, StringComparison.Ordinal) &&
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
    // session StartExample returned, checks the example arrived whole, renders
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

            int expected = ExampleLibraryService.Create(session.Today).Manuscripts.Count;
            int loaded = session.Manuscripts.Count;
            passed &= Report(output, loaded > 0 && loaded == expected, "example library",
                $"loaded {loaded} manuscript(s); the example holds {expected}");
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

    private static void SeedExample(DateTime today)
    {
        // Lock 3's one allowed write (d1 1.3): the example's Seed saves its four
        // files into the example's own session folder, which StartExample set
        // up and End deletes. Every other save stays banned (BannedSymbols.txt).
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

    private static bool IsInside(string path, string folder)
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
