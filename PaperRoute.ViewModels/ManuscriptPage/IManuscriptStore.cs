namespace PaperRoute.ViewModels.ManuscriptPage;

// What the manuscript page asks of the library session (build 9's design,
// 2.2 and 2.5), which implements it when the session saves (#420; S1). Until
// then its only implementation is the tests' FakeManuscriptStore, so nothing
// the page does reaches a disk.
//
// The save contract (2.5): refuse, writing nothing, while other library work
// runs; write the author file first, and only when the delta is not empty,
// adding the delta to a fresh read; then the manuscripts, with the candidate
// in the original's place; only after both succeed, copy the candidate into
// the session's record (ManuscriptCommit) and clear the library's failed-save
// flag. A failure after the author write returns Failed(..., true) and leaves
// the flag as it was.
public interface IManuscriptStore
{
    // Tag suggestions, shortlist names.
    IReadOnlyList<Manuscript> Manuscripts { get; }

    // The session's own record, or null when the library no longer holds it.
    Manuscript? Find(Guid id);

    // A fresh read of the author file (load, Discard), which the caller then
    // owns. Throws InvalidDataException on an unreadable file
    // (AuthorLibraryRepository.Load).
    AuthorLibraryData ReadAuthorLibrary();

    // False for a read-only snapshot.
    bool CanSave { get; }

    // A save, backup or restore runs, or a restore hands over.
    bool IsBusy { get; }

    // The last save of the whole library did not finish (#113).
    bool LastLibrarySaveFailed { get; }

    // CanSave, IsBusy or LastLibrarySaveFailed changed.
    event EventHandler? SaveStateChanged;

    event EventHandler? LibraryChanged;

    Task<ManuscriptSaveResult> SaveManuscriptAsync(Manuscript candidate, LibraryChanges delta, CancellationToken cancel);

    Task<ManuscriptSaveResult> RetryLibrarySaveAsync(CancellationToken cancel);

    Task<ManuscriptSaveResult> DeleteManuscriptAsync(Guid id, CancellationToken cancel);
}
