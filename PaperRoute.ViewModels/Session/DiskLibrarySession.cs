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
        : this(dataDirectory, managedDirectory, settingsDirectory, kind, today, prepare: null)
    {
    }

    internal DiskLibrarySession(
        string dataDirectory,
        string managedDirectory,
        string settingsDirectory,
        LibraryKind kind,
        DateTime today,
        Action? prepare)
    {
        _dataDirectory = FullPath(dataDirectory, nameof(dataDirectory));
        _managedDirectory = FullPath(managedDirectory, nameof(managedDirectory));
        _settingsDirectory = FullPath(settingsDirectory, nameof(settingsDirectory));
        Kind = kind;
        Today = today.Date;
        _prepare = prepare;
    }

    public LibraryKind Kind { get; }

    public bool IsReadOnly => true;

    public DateTime Today { get; }

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
        Interlocked.Exchange(ref _prepare, null)?.Invoke();

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
            return ([], "Library could not be loaded safely. " + ex.Message);
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

        return Path.GetFullPath(path);
    }

    private sealed record LoadResult(
        IReadOnlyList<Manuscript> Manuscripts,
        AuthorLibraryData? AuthorLibrary,
        AppSettings Settings,
        string Notice);
}
