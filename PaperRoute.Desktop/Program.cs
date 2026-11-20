using Avalonia;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop;

internal static class Program
{
    // Lock 1 of the trial design (d1 1.3): the isolated storage session is the
    // first thing Main does, before any other call into PaperRoute.Core, so no
    // default root can reach the user's own library. The example is the only
    // library this wave opens.
    //
    // "--self-check" (d1 1.3 Modes, rule S2) opens no window: it loads the
    // session, checks where storage went, ends the session and exits 0 or 1.
    [STAThread]
    public static int Main(string[] args)
    {
        ILibrarySession session = TrialStartup.StartExample(DateTime.Today);
        try
        {
            if (args.Contains(TrialStartup.SelfCheckArgument, StringComparer.OrdinalIgnoreCase))
            {
                return TrialStartup.RunSelfCheck(session, Console.Out);
            }

            var shell = new ShellViewModel(session);
            return Configure(AppBuilder.Configure(() => new App(shell))).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            session.Dispose();
            TrialStartup.End();
        }
    }

    // Used by the XAML previewer, which skips Main: the App it builds has no
    // session and touches no storage (d1 3.1). No developer tools (d1 2.2).
    public static AppBuilder BuildAvaloniaApp() => Configure(AppBuilder.Configure<App>());

    private static AppBuilder Configure(AppBuilder builder) => builder
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
