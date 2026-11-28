using ManuscriptPipeline.Services;

namespace PaperRoute.Desktop.Platform;

// Everything this system supplies to the core, registered once at start-up
// before anything could read a stored key (start-up design 2.4, step 2), as
// Program.vb:44 registers WindowsPlatform today. On Windows that is today's
// WindowsPlatform, unchanged, so the new app and PaperRoute 1.0.x read each
// other's DPAPI keys. Linux and macOS keep the core's defaults for now; their
// key protectors come with the keyring pull requests (K1 to K3 of build 6).
internal static class DesktopPlatform
{
    public static void Register()
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsPlatform.Register();
        }
    }
}
