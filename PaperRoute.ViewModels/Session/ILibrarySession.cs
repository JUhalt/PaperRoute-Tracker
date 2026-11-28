namespace PaperRoute.ViewModels.Session;

// The library session: the plan's application-services object (d1 3.1;
// shell design 2.5 and 2.6). It loads, saves the settings it loaded, and
// saves the library through the repository that loaded it. Only the kinds
// of library allowed to save may (DiskLibrarySession: the example alone);
// every other kind refuses every save. There is deliberately no backup or
// restore member (lock 3 of d1 1.3), and tests pin the save members:
// SaveSettingsAsync, SaveAsync and SaveFailed.
public interface ILibrarySession : IDisposable
{
    LibraryKind Kind { get; }

    // True when this kind of library cannot be saved: every save member then
    // refuses and writes nothing. False only for the kinds allowed to save.
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

    // Raised on the caller's thread after each load, and after each save that
    // ran, whatever it ended in. The Board renders on it and never assumes it
    // is the library's only writer.
    event EventHandler? LibraryChanged;

    // True while a library save runs, from the call of SaveAsync until it has
    // published on the caller's context; a load does not count. While it is
    // true, no reader touches Manuscripts: the save normalises each manuscript
    // in place on its worker (ManuscriptRepository.vb:1058-1076), as today's
    // guard says, "A board search firing mid-save would read the library while
    // the worker normalizes it" (Form1.LibraryWork.vb:82-84). The Board holds
    // its render and renders on the LibraryChanged raised after the save.
    bool IsWorking { get; }

    // True after a save failed, until a save succeeds. A refused save and a
    // load leave it as it was.
    bool SaveFailed { get; }

    // Raised on the caller's thread when IsWorking or SaveFailed changes: as a
    // save starts, and again once it has ended.
    event EventHandler? StateChanged;

    // Saves Manuscripts as they stand (their records changed in place) through
    // the repository whose load gave them, on a worker, and returns to the
    // caller's context to publish. That repository stages the managed folders
    // of removed versions and packet files against what its own load or last
    // save referenced (ManuscriptRepository.vb:1105-1131); a new one would
    // leave them on disk for good. One piece of library work at a time: while
    // a load or another save runs, the save is refused at once and writes
    // nothing. Refused, with nothing written, also for a kind that cannot be
    // saved (IsReadOnly), before a load has read the library, and after a
    // load that could not read it. Saved carries the repository's
    // LastSaveWarning and clears SaveFailed; Failed carries the exception's
    // message and sets SaveFailed. A cancelled token throws before anything is
    // written; once the worker runs, the save is not cut off, and WaitForWork
    // waits for it as for a load. Nor does a subscriber of StateChanged or
    // LibraryChanged that throws cut it off: the save still ends with its
    // worker and raises both end events, and only then does the task fault
    // with that exception. Its outcome is then lost, so read IsWorking and
    // SaveFailed instead.
    Task<SaveOutcome> SaveAsync(CancellationToken cancel);

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
    // quick requests remember the last. A load replaces Settings with what it
    // read, so a change made to Settings while a load runs is lost, and a save
    // held during that load writes the loaded settings: a caller changes
    // Settings after LibraryChanged, or applies its change again then. False,
    // with nothing written, when the write fails (an IOException or
    // UnauthorizedAccessException, as Form1.Online.vb:117 catches), for a
    // kind that cannot be saved (IsReadOnly: a snapshot among them), and
    // before a load has read settings to save.
    Task<bool> SaveSettingsAsync();

    // Blocks until a running worker ends, with no time limit, as today's
    // shutdown waits for library work (Form1.LibraryWork.vb:180-190). It waits
    // on the worker itself, never on the task the interface awaits, so it
    // cannot deadlock on the interface thread. True when a worker was running.
    // For the shutdown path only. A settings save held during that load is
    // written later, on the caller's context, which the shutdown path is
    // blocking: B5 writes it itself after this wait, or accepts its loss.
    bool WaitForWork();
}
