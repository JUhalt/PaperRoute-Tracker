namespace PaperRoute.ViewModels.Startup;

// The order of start-up as one class (start-up design 2.4, build 6 Q1): the
// order of today's Program.vb:31-90, kept and tested. Program.Main runs
// Velopack first and then returns Run(args, steps) (build 6, G1 and I3);
// until I3 only the tests call it.
//
// The order, by record name (IStartupSteps):
//   options, platform, session, data-root, error-reports, single-instance,
//   then avalonia for the copy that holds the library, or
//   ask-to-come-forward for a second copy, and at the end the hold's release
//   and end-session.
// A refused command line stops after options with exit code 2. A data root
// that cannot be found, or that is not a fully qualified path, stops with
// the no-window message and exit code 1. A
// second copy stops before any storage step and before the app starts.
public static class StartupSequence
{
    // A command line PaperRoute will not run; nothing was opened (the
    // trial's rule, PlatformCheck.MeaningOfExitCode).
    public const int RefusedExitCode = 2;

    // No data root: the system names no home folder (target map:377).
    public const int NoHomeFolderExitCode = 1;

    // A second copy started by a person exits silently, as today
    // (Program.vb:69-72), whether or not the first copy came forward. Unlike
    // today, which returns before its Try/Finally, it ends the session it
    // began first, so a second copy started with --example leaves no
    // example folder behind (start-up design 2.4, step 6).
    public const int SecondCopyExitCode = 0;

    // A second copy in a check mode: "another copy holds this library and
    // was asked to come forward".
    public const int SecondCopyInACheckModeExitCode = 3;

    // How long a second copy waits to reach the copy that holds the library.
    public static readonly TimeSpan ComeForwardLimit = TimeSpan.FromSeconds(2);

    // Before any window can exist and before error reports are hooked, so
    // this message is the only word the person gets. Today the process
    // crashes with none (start-up design N4).
    public const string NoHomeFolderMessage =
        "PaperRoute could not find your home folder, so it cannot open your library.";

    // Runs start-up and returns the process's exit code. The hold is
    // released on the thread that calls Run (the mutex must be released on
    // the thread that took it, SingleInstanceService.vb:105-107), and then
    // the session is ended, whatever happened after the session step began:
    // a step that throws still leaves no hold and no example folder behind,
    // and its error goes on to the caller.
    public static int Run(IReadOnlyList<string> args, IStartupSteps steps)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.ReadOptions(args) is not { } mode)
        {
            return RefusedExitCode;
        }

        steps.RegisterPlatform();
        try
        {
            steps.BeginSession();
            if (FindDataRoot(steps) is not { } dataRoot)
            {
                steps.NoWindowMessage(NoHomeFolderMessage);
                return NoHomeFolderExitCode;
            }

            steps.HookErrorReports();
            IDisposable? hold = steps.TryHold(dataRoot);
            if (hold is null)
            {
                // The answer does not change the exit: a first copy that
                // cannot be reached still holds the library.
                steps.AskToComeForward(dataRoot, ComeForwardLimit);
                return mode == StartupMode.Check ? SecondCopyInACheckModeExitCode : SecondCopyExitCode;
            }

            using (hold)
            {
                return steps.RunApp();
            }
        }
        finally
        {
            steps.EndSession();
        }
    }

    // Null when the data root cannot be found. Every failure here counts:
    // no window and no logs folder exist yet, so an error that escaped would
    // end the process with no word at all. Any answer that is not a fully
    // qualified path is refused too, so no root becomes a path inside the
    // folder PaperRoute started from. On Windows the real step can give
    // such an answer without throwing: a redirected folder that is missing
    // right now answers empty (StorageEnvironment.UserFolder), and the root
    // then comes back as the bare folder name, "PaperRoute"
    // (StorageMigrationService.CurrentDataRoot).
    private static string? FindDataRoot(IStartupSteps steps)
    {
        string dataRoot;
        try
        {
            dataRoot = steps.FindDataRoot();
        }
        catch (Exception)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(dataRoot) || !Path.IsPathFullyQualified(dataRoot) ? null : dataRoot;
    }
}
