using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PaperRoute.Desktop.Styles;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop;

// App never starts a library session or calls core storage: the previewer and
// the headless tests build it without Main, where the storage lock is set
// (d1 3.1). Main hands the session in; without one the window is empty.
public partial class App : Application
{
    private readonly ILibrarySession? _session;

    public App()
    {
    }

    public App(ILibrarySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
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
    // dictionaries before any control looks them up; text boxes get their
    // placeholder at full strength (InputTemplates).
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (ThemeTokens.AddDerivedToAll(Resources) == 0)
        {
            throw new InvalidOperationException("Tokens.axaml has no theme dictionaries to extend.");
        }

        InputTemplates.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShellViewModel? shell = _session is null ? null : CreateShell(_session);
            var window = new MainWindow { DataContext = shell };
            if (shell is not null)
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
