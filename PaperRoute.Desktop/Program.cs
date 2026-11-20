using Avalonia;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop;

internal static class Program
{
    // Exit code for a command line the trial will not run (a refused folder,
    // a bad argument); nothing was opened.
    private const int RefusedExitCode = 2;

    // Lock 1 of the trial design (d1 1.3): the isolated storage session is the
    // first thing Main does, before any other call into PaperRoute.Core, so no
    // default root can reach the user's own library. The library is the
    // example, or with "--snapshot-from <folder>" a read-only copy of the
    // library in that folder (step 11b), refused before anything is read when
    // the folder is, or lies inside, one of the real profile's storage folders.
    // The command line is read first; reading it calls nothing in the core.
    //
    // "--self-check" (d1 1.3 Modes, rules S1 and S2) opens no window: it loads
    // the session, checks where storage went, ends the session and exits 0 or 1.
    [STAThread]
    public static int Main(string[] args)
    {
        if (!TrialOptions.TryParse(args, out TrialOptions? options, out string? error))
        {
            Console.Error.WriteLine(error);
            return RefusedExitCode;
        }

        ILibrarySession session;
        if (options.SnapshotFrom is { } folder)
        {
            string? refusal = ReadOnlySnapshot.RefusalFor(folder);
            if (refusal is not null)
            {
                Console.Error.WriteLine(refusal);
                return RefusedExitCode;
            }

            session = TrialStartup.StartSnapshot(folder, options.Today ?? DateTime.Today);
        }
        else
        {
            session = TrialStartup.StartExample(DateTime.Today);
        }

        try
        {
            if (options.SelfCheck)
            {
                return TrialStartup.RunSelfCheck(session, Console.Out);
            }

            return Configure(AppBuilder.Configure(() => new App(session))).StartWithClassicDesktopLifetime(args);
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
