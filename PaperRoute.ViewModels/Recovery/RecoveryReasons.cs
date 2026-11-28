namespace PaperRoute.ViewModels.Recovery;

// Why a recovery draft was kept, in today's words (start-up design 2.5; the
// reasons Form1 passes to WriteRecoveryDraft). Written into the draft file.
public static class RecoveryReasons
{
    // The operating system was shutting down.
    public static string Shutdown =>
        OperatingSystem.IsWindows() ? "Windows was shutting down" : "the computer was shutting down";

    // An unexpected error ended the session.
    public const string Error = "an error ended the session";
}
