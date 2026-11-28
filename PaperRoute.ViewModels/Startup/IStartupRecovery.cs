namespace PaperRoute.ViewModels.Startup;

// The failure window's two ways to put the library right (#124): the
// previous save and a backup archive. StartupRecovery is the real one, over
// the core; the tests' FakeStartupRecovery plays each answer. Each member may
// read or write files and may wait for a busy folder, so the window's model
// runs the backup's two members on a worker (StartupFailureViewModel).
public interface IStartupRecovery
{
    // True when manuscripts.bak reads with the current rules, the storage
    // marker is not from a newer PaperRoute, and manuscripts.json does not
    // read. Changes nothing.
    bool PreviousSaveIsValid();

    // True when the storage marker says a newer PaperRoute wrote the library:
    // not damage, so nothing is put over it. Changes nothing.
    bool LibraryIsFromANewerPaperRoute();

    // Puts the previous save in place of manuscripts.json and returns where
    // the replaced file is kept (empty when there was none). Throws, having
    // changed nothing, when it cannot.
    string UsePreviousSave();

    // What the archive holds, read before anything is asked. Changes nothing.
    BackupInspection InspectBackup(string backupZipPath);

    // Restores the archive over the library on disk. A failure says what was
    // put back and names the emergency copy.
    RestoreResult RestoreFromBackup(string backupZipPath);
}
