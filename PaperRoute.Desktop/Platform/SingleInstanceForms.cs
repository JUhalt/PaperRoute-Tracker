using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Platform;

// Which single-instance form this system gets (start-up design 2.3): today's
// mutex on Windows, the lock file and socket on Linux and macOS. Start-up's
// step 6 takes its hold through this (build 6, I3); note takes the Unix
// form's line that says come forward is off.
public static class SingleInstanceForms
{
    public static ISingleInstance ForThisSystem(Action<string>? note = null) =>
        OperatingSystem.IsWindows() ? new WindowsSingleInstance() : new UnixSingleInstance(note: note);
}
