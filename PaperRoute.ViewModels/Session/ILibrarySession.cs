namespace PaperRoute.ViewModels.Session;

// The library session: the plan's application-services object as far as
// build 5 takes it (d1 3.1; shell design 2.5). It loads, and it saves the
// settings it loaded; it never saves the library. There is deliberately no
// library save, backup or restore member (lock 3 of d1 1.3), and tests pin
// that: SaveSettingsAsync is its one write.
public interface ILibrarySession : IDisposable
{
    LibraryKind Kind { get; }

    // Always true until the library save comes: the library cannot be saved.
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
    // context to publish it. A load is library work, because
    // ManuscriptRepository.Load can write (it creates the data folder,
    // recovers staged moves and can rebuild the main file from its backup), so
    // it runs one at a time: while one runs, another returns false at once and
    // reads and writes nothing.
    Task<bool> LoadAsync(CancellationToken cancel);

    // Writes Settings through the settings service that loaded them, so a
    // settings.json that load could not read is set aside as
    // settings.unreadable.json, never kept as settings.bak over the good
    // backup. On the interface thread, as today (Form1.Shell.vb:201-204): at
    // once on the caller's thread, returning a finished task, unless a load
    // runs; then the request is held, and one write runs on the caller's
    // context when the load ends. The write reads Settings when it runs, so
    // quick requests remember the last. False, with nothing written, when the
    // write fails (an IOException or UnauthorizedAccessException, as
    // Form1.Online.vb:117 catches), for a snapshot, and before a load has
    // read settings to save.
    Task<bool> SaveSettingsAsync();

    // Blocks until a running worker ends, with no time limit, as today's
    // shutdown waits for library work (Form1.LibraryWork.vb:180-190). It waits
    // on the worker itself, never on the task the interface awaits, so it
    // cannot deadlock on the interface thread. True when a worker was running.
    // For the shutdown path only.
    bool WaitForWork();
}
