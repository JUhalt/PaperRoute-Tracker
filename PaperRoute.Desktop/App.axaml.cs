using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PaperRoute.Desktop.Measurement;
using PaperRoute.Desktop.Platform;
using PaperRoute.Desktop.Styles;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Platform;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop;

// App never starts a library session or calls core storage: the previewer and
// the headless tests build it without Main, where the storage lock is set
// (d1 3.1). Main hands the session in; without one the window is empty.
public partial class App : Application
{
    private readonly ILibrarySession? _session;
    private readonly string? _measureFile;
    private readonly string _daySource = string.Empty;
    private readonly string? _platformCheckFile;

    public App()
    {
    }

    public App(ILibrarySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    // "--measure" (d1 7.3): the app measures the Board in its own window,
    // writes the JSON to measureFile and shuts down with the run's exit code.
    public App(ILibrarySession session, string measureFile, string daySource)
        : this(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measureFile);
        _measureFile = measureFile;
        _daySource = daySource ?? string.Empty;
    }

    // "--platform-check" (step 15): the app opens its window on the example,
    // tries the system's file dialog, launcher and a restart once each
    // (PlatformCheckWindow), writes the JSON to platformCheckFile and shuts
    // down with the check's exit code. The bool only tells this constructor
    // from the measurement's.
    public App(ILibrarySession session, string platformCheckFile, bool platformCheck)
        : this(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformCheckFile);
        if (!platformCheck)
        {
            throw new ArgumentException("This constructor is for the platform check.", nameof(platformCheck));
        }

        _platformCheckFile = platformCheckFile;
    }

    // The shell over a session, made on the interface thread: the Board's
    // search wait is a DispatcherTimer on that thread (d1 3.4). The Board is
    // made before the load, so the load's LibraryChanged renders it.
    public static ShellViewModel CreateShell(ILibrarySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var board = new BoardViewModel(
            session,
            new UiThreadDebouncer(Dispatcher.UIThread),
            new UiThreadDispatcher(Dispatcher.UIThread));
        return new ShellViewModel(session, board);
    }

    // The computed brushes (ThemeTokens) join Tokens.axaml's Light and Dark
    // dictionaries before any control looks them up, and the two contrast
    // dictionaries join them after (ContrastTheme, which derives its own); text
    // boxes get their placeholder at full strength (InputTemplates).
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (ThemeTokens.AddDerivedToAll(Resources) == 0)
        {
            throw new InvalidOperationException("Tokens.axaml has no theme dictionaries to extend.");
        }

        ContrastTheme.Register(Resources);
        InputTemplates.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A Windows contrast theme, at start and whenever it changes
            // (d1 5.2). The window's activation reads it again, for a switch
            // between two contrast themes that Avalonia might not report.
            ContrastPreference contrast = ContrastPreference.FromPlatform(PlatformSettings);
            ContrastTheme.Follow(this, contrast);

            if (_session is not null && _measureFile is not null)
            {
                // The measurement makes, times and closes its own window, then
                // ends the app with its exit code.
                ILibrarySession session = _session;
                string file = _measureFile;
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Dispatcher.UIThread.Post(() => desktop.Shutdown(WindowMeasurement.Run(session, file, _daySource)), DispatcherPriority.Background);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            ShellViewModel? shell = _session is null ? null : CreateShell(_session);
            var window = new MainWindow { DataContext = shell };
            window.Activated += (_, _) => contrast.Refresh();
            if (shell is not null && _platformCheckFile is not null)
            {
                // The check loads the library once the window is open, then
                // runs. Closing the window before it ends writes what was
                // learnt so far, stops a second copy still running, and
                // stops the app with 1.
                string file = _platformCheckFile;
                bool finished = false;
                bool shutDown = false;
                RestartTarget restart = RestartTarget.For(
                    Environment.GetEnvironmentVariable("APPIMAGE"),
                    Environment.GetEnvironmentVariable("APPDIR"),
                    Environment.ProcessPath,
                    File.Exists);
                var check = new PlatformCheckWindow(
                    window,
                    shell,
                    file,
                    new AvaloniaPlatformActions(window),
                    restart,
                    PrivacyScrubber.ForThisProcess(restart.FileName ?? Environment.ProcessPath),
                    Console.Out,
                    PlatformCheck.DefaultRestartLimit,
                    Environment.GetEnvironmentVariable,
                    SystemTextSettings.Read);
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                window.Closed += (_, _) =>
                {
                    if (!finished && !shutDown)
                    {
                        shutDown = true;
                        try
                        {
                            check.CloseEarly();
                            Console.Error.WriteLine("The window was closed before the platform check finished; the JSON holds what was learnt so far.");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine("The window was closed before the platform check finished, and its JSON could not be written: " + ex.GetType().Name);
                        }

                        desktop.Shutdown(1);
                    }
                };
                window.Opened += async (_, _) =>
                {
                    int code = 1;
                    try
                    {
                        code = await check.RunAsync(beforeClose: () => finished = true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("The platform check broke: " + ex.GetType().Name);
                    }
                    finally
                    {
                        finished = true;
                        if (!shutDown)
                        {
                            shutDown = true;
                            desktop.Shutdown(code);
                        }
                    }
                };
            }
            else if (shell is not null)
            {
                // The window shows "Loading…" first; the load starts once it is open.
                window.Opened += (_, _) =>
                {
                    if (shell.LoadCommand.CanExecute(null))
                    {
                        shell.LoadCommand.Execute(null);
                    }
                };
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
