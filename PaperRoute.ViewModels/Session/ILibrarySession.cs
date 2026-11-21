namespace PaperRoute.ViewModels.Session;

// The library session: the read-only subset of the plan's application-services
// object (d1 3.1). It loads; it never saves. There is deliberately no save,
// backup or restore member (lock 3 of d1 1.3), and a test pins that.
public interface ILibrarySession : IDisposable
{
    LibraryKind Kind { get; }

    // Always true in the trial.
    bool IsReadOnly { get; }

    // Fixed when the session starts; tests pass their own.
    DateTime Today { get; }

    // For a snapshot: the folder copied, and once the load has copied it, what
    // was copied and when. Null for the example.
    LibrarySnapshot? Snapshot { get; }

    // The library's own list order. Empty until LoadAsync has finished, and
    // after a load that failed.
    IReadOnlyList<Manuscript> Manuscripts { get; }

    // Null when the author library could not be read.
    AuthorLibraryData? AuthorLibrary { get; }

    AuthorLibrarySearchIndex SearchIndex { get; }

    AppSettings Settings { get; }

    // "Loaded N manuscripts from local storage." and the other texts of
    // today's status line (Form1.vb:1370-1449); empty before the first load.
    string StartupNotice { get; }

    // Raised on the caller's thread after each load. The Board renders on it
    // and never assumes it is the library's only writer.
    event EventHandler? LibraryChanged;

    // Reads the library off the interface thread and returns to the caller's
    // context to publish it.
    Task LoadAsync(CancellationToken cancel);
}
