using Avalonia;

namespace PaperRoute.Desktop;

internal static class Program
{
    // Lock 1 of the trial design (d1 1.3): the isolated storage session is the
    // first thing Main does, before any other call into PaperRoute.Core, so no
    // default root can reach the user's own library. This step makes no core
    // call at all; the session start (TrialStartup.StartExample) comes in the
    // next step and goes above the line that starts Avalonia.
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer. No developer tools (d1 2.2).
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
