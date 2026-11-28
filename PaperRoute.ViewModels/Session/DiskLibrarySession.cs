using System.Globalization;

namespace PaperRoute.ViewModels.Session;

// The one ILibrarySession: a library read from explicit folders (d1 3.1).
// Every folder is given; nothing here resolves a default storage root, so a
// session over a test's temporary folder can never reach the user's library.
public sealed class DiskLibrarySession : ILibrarySession
{
    private readonly string _dataDirectory;
    private readonly string _managedDirectory;
    private readonly string _settingsDirectory;

    // Work done once, off the interface thread, before the first read: the
    // example's seed (TrialStartup). Null when there is none, or once done.
    private Action? _prepare;

    // One piece of library work at a time. _loading is true from the start of
    // a load until it has published on the caller's context; _worker is the
    // load's worker task, which WaitForWork waits on.
    private readonly object _work = new();
    private bool _loading;
    private Task? _worker;

    // The settings service whose Load gave Settings, kept so a save goes
    // through it (shell design 2.5, critic M5): only that instance knows that
    // settings.json could not be read and must be set aside. Null before a
    // load, and after a load whose settings read failed.
    private AppSettingsService? _settingsService;

    // A settings save asked for while a load runs, written once when it ends.
    private TaskCompletionSource<bool>? _heldSettingsSave;

    private IReadOnlyList<Manuscript> _manuscripts = [];
    private bool _disposed;

    public DiskLibrarySession(
        string dataDirectory,
        string managedDirectory,
        string settingsDirectory,
        LibraryKind kind,
        DateTime today)
        : this(dataDirectory, managedDirectory, settingsDirectory, kind, today, prepare: null, snapshot: null)
    {
        if (kind == LibraryKind.Snapshot)
        {
            throw new ArgumentException("A snapshot session is made over its copy (the SnapshotCopy constructor).", nameof(kind));
        }
    }

    // A snapshot over a copy already made (ReadOnlySnapshot.Copy): the copy's
    // own folder is read, never the folder it was copied from.
    public DiskLibrarySession(
        SnapshotCopy copy,
        string managedDirectory,
        DateTime today)
        : this(
            Path.Combine((copy ?? throw new ArgumentNullException(nameof(copy))).DestinationFolder, ReadOnlySnapshot.DataFolderName),
            managedDirectory,
            copy.DestinationFolder,
            LibraryKind.Snapshot,
            today,
            prepare: null,
            snapshot: new LibrarySnapshot(copy))
    {
    }

    internal DiskLibrarySession(
        string dataDirectory,
        string managedDirectory,
        string settingsDirectory,
        LibraryKind kind,
        DateTime today,
        Action? prepare,
        LibrarySnapshot? snapshot)
    {
        _dataDirectory = FullPath(dataDirectory, nameof(dataDirectory));
        _managedDirectory = FullPath(managedDirectory, nameof(managedDirectory));
        _settingsDirectory = FullPath(settingsDirectory, nameof(settingsDirectory));
        if ((kind == LibraryKind.Snapshot) != (snapshot is not null))
        {
            throw new ArgumentException("A snapshot session, and only one, names the folder it copies.", nameof(snapshot));
        }

        Kind = kind;
        Today = today.Date;
        _prepare = prepare;
        Snapshot = snapshot;
    }

    public LibraryKind Kind { get; }

    public bool IsReadOnly => true;

    public DateTime Today { get; }

    public LibrarySnapshot? Snapshot { get; }

    public IReadOnlyList<Manuscript> Manuscripts => _manuscripts;

    public AuthorLibraryData? AuthorLibrary { get; private set; }

    public AuthorLibrarySearchIndex SearchIndex { get; private set; } = new(null);

    public AppSettings Settings { get; private set; } = new();

    public string StartupNotice { get; private set; } = string.Empty;

    public event EventHandler? LibraryChanged;

    public async Task<bool> LoadAsync(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Off the interface thread (d1 3.4); the await comes back on the
        // caller's context, where the results are published.
        Task<LoadResult> worker;
        lock (_work)
        {
            if (_loading)
            {
                return false;
            }

            _loading = true;
            worker = Task.Run(() => Read(cancel), cancel);
            _worker = worker;
        }

        try
        {
            LoadResult result = await worker;

            _manuscripts = result.Manuscripts;
            AuthorLibrary = result.AuthorLibrary;
            SearchIndex = new AuthorLibrarySearchIndex(result.AuthorLibrary);
            Settings = result.Settings;
            _settingsService = result.SettingsService;
            StartupNotice = result.Notice;
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            TaskCompletionSource<bool>? held;
            lock (_work)
            {
                _loading = false;
                held = _heldSettingsSave;
                _heldSettingsSave = null;
            }

            // On the caller's context, as every settings save is.
            held?.SetResult(WriteSettings());
        }
    }

    public Task<bool> SaveSettingsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Kind == LibraryKind.Snapshot)
        {
            // A snapshot writes nothing, its settings included.
            return Task.FromResult(false);
        }

        lock (_work)
        {
            if (_loading)
            {
                _heldSettingsSave ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _heldSettingsSave.Task;
            }
        }

        return Task.FromResult(WriteSettings());
    }

    private bool WriteSettings()
    {
        AppSettingsService? service = _settingsService;
        if (service is null || Kind == LibraryKind.Snapshot)
        {
            return false;
        }

        try
        {
            // One of lock 3's two allowed writes (d1 1.3; shell design 2.5):
            // the settings, through the service that loaded them, into the
            // settings folder this session was given, which FullPath admits
            // only inside the temporary folder. Every library save stays
            // banned (BannedSymbols.txt).
#pragma warning disable RS0030
            service.Save(Settings);
#pragma warning restore RS0030
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool WaitForWork()
    {
        Task? worker;
        lock (_work)
        {
            worker = _worker;
        }

        if (worker is null || worker.IsCompleted)
        {
            return false;
        }

        // The wait handle, not Wait(): it neither runs the worker inline on
        // this thread nor throws what the worker ended in.
        ((IAsyncResult)worker).AsyncWaitHandle.WaitOne();
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        LibraryChanged = null;
    }

    private LoadResult Read(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        Action? prepare = Interlocked.Exchange(ref _prepare, null);
        if (Kind == LibraryKind.Snapshot)
        {
            // The snapshot's copy. A copy refused or failed leaves the Board
            // empty with the reason, as a load that failed does.
            try
            {
                prepare?.Invoke();
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                return new LoadResult([], null, new AppSettings(), null, "The snapshot could not be taken: " + ex.Message);
            }
        }
        else
        {
            prepare?.Invoke();
        }

        AppSettings settings;
        AppSettingsService? settingsService = new(_settingsDirectory);
        try
        {
            settings = settingsService.Load();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // Defaults in memory; with no service, nothing saves over the
            // file that could not be read.
            settings = new AppSettings();
            settingsService = null;
        }

        cancel.ThrowIfCancellationRequested();
        (IReadOnlyList<Manuscript> manuscripts, string notice) = ReadManuscripts();

        cancel.ThrowIfCancellationRequested();
        AuthorLibraryData? authors;
        try
        {
            authors = new AuthorLibraryRepository(_dataDirectory).Load();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            authors = null;
        }

        // A file the snapshot could not read is named (d1 1.3, lock 2).
        IReadOnlyList<string> skipped = Snapshot?.Copy?.Skipped ?? [];
        if (skipped.Count > 0)
        {
            notice = SkippedNotice(notice, skipped, manuscripts.Count);
        }

        return new LoadResult(manuscripts, authors, settings, settingsService, notice);
    }

    // The status line of a snapshot that skipped files. Without the
    // manuscripts file the copy holds no library (or only the previous save),
    // so the line says that in place of "Loaded 0 manuscripts", lest an empty
    // Board read as an empty library; other files are named after it.
    public static string SkippedNotice(string loadNotice, IReadOnlyList<string> skipped, int loaded)
    {
        string manuscriptsFile = Path.Combine(ReadOnlySnapshot.DataFolderName, "manuscripts.json");
        string notice = loadNotice;
        List<string> others = [.. skipped];
        if (others.Remove(manuscriptsFile))
        {
            notice = $"The snapshot could not copy the manuscripts file ({manuscriptsFile}): another program may have been using it. Try again.";
            if (loaded > 0)
            {
                notice += $" The Board shows the previous save, from {Path.Combine(ReadOnlySnapshot.DataFolderName, "manuscripts.bak")}.";
            }
        }

        if (others.Count == 1)
        {
            notice += " This file could not be read, so it was not copied: " + others[0] + ".";
        }
        else if (others.Count > 1)
        {
            notice += " These files could not be read, so they were not copied: " + string.Join(", ", others) + ".";
        }

        return notice;
    }

    // The status texts of today's load (Form1.vb:1309-1453). The dialogs that
    // go with some of them are not part of the trial; a failure leaves the
    // Board empty with its notice, in place of today's startup-failure window.
    private (IReadOnlyList<Manuscript> Manuscripts, string Notice) ReadManuscripts()
    {
        var repository = new ManuscriptRepository(_dataDirectory, _managedDirectory);
        try
        {
            List<Manuscript> manuscripts = repository.Load() ?? [];
            string notice = repository.LastLoadRecoveredFromBackup
                ? "Opened the previous save of the library - " + DateTime.Now.ToString("h:mm tt", CultureInfo.CurrentCulture)
                : LoadedText(manuscripts.Count);

            if (!string.IsNullOrWhiteSpace(repository.LastManagedLibraryRecoveryWarning))
            {
                notice = "Library loaded with a managed-file recovery warning.";
            }

            return (manuscripts, notice);
        }
        catch (StorageFileInUseException)
        {
            return ([], "The library file is in use by another program.");
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // Today's status line exactly (Form1.vb:1448-1449): the exception's
            // message goes to Form1's startup-failure window, not to the line.
            return ([], "Library could not be loaded safely.");
        }
    }

    internal static string LoadedText(int count) =>
        "Loaded " + count.ToString(CultureInfo.CurrentCulture) + (count == 1 ? " manuscript" : " manuscripts") + " from local storage.";

    private static bool IsReadFailure(Exception ex) =>
        ex is not (OperationCanceledException or OutOfMemoryException);

    private static string FullPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("An absolute folder is required.", name);
        }

        // ManuscriptRepository.Load can write (it creates the data folder and
        // can recover from a backup; d1 1.3), and lock 1 guards only the
        // default roots. The trial reads only the example's session, a
        // snapshot's copy inside its session (never the folder copied) and the
        // tests' libraries, all inside the temporary folder.
        string full = Path.GetFullPath(path);
        if (!TrialStartup.IsInside(full, Path.GetTempPath()))
        {
            throw new ArgumentException("The trial reads only libraries inside the temporary folder.", name);
        }

        return full;
    }

    private sealed record LoadResult(
        IReadOnlyList<Manuscript> Manuscripts,
        AuthorLibraryData? AuthorLibrary,
        AppSettings Settings,
        AppSettingsService? SettingsService,
        string Notice);
}
