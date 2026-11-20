using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PaperRoute.Desktop.Styles;
using PaperRoute.ViewModels;

namespace PaperRoute.Desktop;

// App never starts a library session or calls core storage: the previewer and
// the headless tests build it without Main, where the storage lock is set
// (d1 3.1). Main hands the shell in; without one the window is empty.
public partial class App : Application
{
    private readonly ShellViewModel? _shell;

    public App()
    {
    }

    public App(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
    }

    // The computed brushes (ThemeTokens) join Tokens.axaml's Light and Dark
    // dictionaries before any control looks them up.
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (ThemeTokens.AddDerivedToAll(Resources) == 0)
        {
            throw new InvalidOperationException("Tokens.axaml has no theme dictionaries to extend.");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = _shell };
            if (_shell is not null)
            {
                // The window shows "Loading…" first; the load starts once it is open.
                ShellViewModel shell = _shell;
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
