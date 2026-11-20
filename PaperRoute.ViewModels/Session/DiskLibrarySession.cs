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

    public async Task LoadAsync(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Off the interface thread (d1 3.4); the await comes back on the
        // caller's context, where the results are published.
        LoadResult result = await Task.Run(() => Read(cancel), cancel);

        _manuscripts = result.Manuscripts;
        AuthorLibrary = result.AuthorLibrary;
        SearchIndex = new AuthorLibrarySearchIndex(result.AuthorLibrary);
        Settings = result.Settings;
        StartupNotice = result.Notice;
        LibraryChanged?.Invoke(this, EventArgs.Empty);
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
                return new LoadResult([], null, new AppSettings(), "The snapshot could not be taken: " + ex.Message);
            }
        }
        else
        {
            prepare?.Invoke();
        }

        AppSettings settings;
        try
        {
            settings = new AppSettingsService(_settingsDirectory).Load();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            settings = new AppSettings();
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
            notice += " Could not be read, so not copied: " + string.Join(", ", skipped) + ".";
        }

        return new LoadResult(manuscripts, authors, settings, notice);
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
        string Notice);
}
