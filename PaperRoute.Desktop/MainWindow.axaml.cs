using Avalonia.Controls;
using Avalonia.Input;
using PaperRoute.ViewModels;

namespace PaperRoute.Desktop;

// The shell's window (d1 4.1). Its window-wide keys are today's that the
// trial keeps (Form1.vb:2562-2565, 2589-2591; d1 1.2, 4.5): Ctrl+1 (and
// Ctrl+NumPad1) shows the Board, F1 gives a notice. Ctrl+2 to Ctrl+6 and
// Alt+Left/Right do nothing in the trial, so they are not bound. The
// bindings are made here, with the shell's own commands, whenever the
// window's data context changes.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindKeys(DataContext as ShellViewModel);
    }

    private void BindKeys(ShellViewModel? shell)
    {
        KeyBindings.Clear();
        if (shell is null)
        {
            return;
        }

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1, KeyModifiers.Control), Command = shell.ShowBoardCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.NumPad1, KeyModifiers.Control), Command = shell.ShowBoardCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F1), Command = shell.ShowHelpCommand });
    }
}
