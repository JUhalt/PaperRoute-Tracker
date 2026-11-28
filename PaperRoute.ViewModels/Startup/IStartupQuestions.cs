namespace PaperRoute.ViewModels.Startup;

// What the failure window asks the person while it restores a backup: which
// archive, and whether to go on once its contents are known. The window
// answers through the system's file picker and the one restore confirmation
// (build 6 F2); the tests answer instead. Both are called on the thread that
// started the restore, the window's, never on a worker.
public interface IStartupQuestions
{
    // The archive to restore, or null when the person chose none.
    Task<string?> ChooseBackupAsync();

    // "Restore this backup?", with the archive's origin and counts. True
    // restores; anything else changes nothing.
    Task<bool> ConfirmRestoreAsync(BackupInspection backup);
}
