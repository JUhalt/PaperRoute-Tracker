using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Shell;

namespace PaperRoute.Desktop;

// The shell's window (d1 4.1; shell design B5). Its window-wide keys are
// today's that the trial keeps (Form1.vb:2553-2565, 2589-2591; d1 1.2, 4.5):
// Ctrl+1 (and Ctrl+NumPad1) shows the Board, Ctrl+F shows the Board and puts
// the keyboard in its search box with the text selected, F1 gives a notice.
// Esc is not bound: on the Board it does nothing. Ctrl+2 to Ctrl+6 and
// Alt+Left/Right do nothing in the trial, so they are not bound. The
// bindings are made here, with the shell's own commands, whenever the
// window's data context changes.
//
// The frame (MainWindow.axaml) is capped to its screen here, and a close goes
// through the shell's close step (ShellViewModel.CloseAsync).
public partial class MainWindow : Window
{
    // Today's dialogs keep this many pixels of the working area free around
    // a window they open (ResponsiveDialogSizingService.CalculateInitialSize).
    private const double EdgeMargin = 72;

    // The shell's close step said yes: the close it starts goes through.
    private bool _closeAllowed;

    // An ordinary close is waiting for the shell's close step (the page's
    // answer, or a load's end); another ordinary close meanwhile is refused.
    private bool _closePending;

    private bool _closed;

    // The frame's start size and minimum as MainWindow.axaml requests them,
    // before any cap.
    private readonly Size _requestedStart;
    private readonly Size _requestedMinimum;

    public MainWindow()
    {
        InitializeComponent();
        _requestedStart = new Size(Width, Height);
        _requestedMinimum = new Size(MinWidth, MinHeight);
        FitToScreen();
        DataContextChanged += (_, _) => BindKeys(DataContext as ShellViewModel);
    }

    // The start size and the minimum for a working area of a screen, in its
    // pixels at its scaling, as today's Form1.StartSizes (Form1.vb:249-280):
    // the minimum lowered to the area when even it would not fit; the start
    // size capped to the area less the dialogs' margin, never below the
    // minimum. Both in the window's own units.
    //
    // Today's rule caps the whole window, title bar and borders included
    // (a form's Size), so that it is never taller than the working area
    // (#93). Avalonia's sizes are the client area's, so the decoration
    // around it (in the window's units) is taken from the area first.
    public static (Size Start, Size Minimum) StartSizes(PixelSize workingArea, double scaling, Size decoration, Size start, Size minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);
        double areaWidth = Math.Max(1, workingArea.Width - (decoration.Width * scaling));
        double areaHeight = Math.Max(1, workingArea.Height - (decoration.Height * scaling));
        double minimumWidth = Math.Max(1, Math.Min(minimum.Width * scaling, areaWidth));
        double minimumHeight = Math.Max(1, Math.Min(minimum.Height * scaling, areaHeight));

        double availableWidth = Math.Max(1, areaWidth - EdgeMargin);
        double availableHeight = Math.Max(1, areaHeight - EdgeMargin);
        double width = Math.Min(start.Width * scaling, availableWidth);
        double height = Math.Min(start.Height * scaling, availableHeight);
        if (availableWidth >= minimumWidth)
        {
            width = Math.Max(minimumWidth, width);
        }

        if (availableHeight >= minimumHeight)
        {
            height = Math.Max(minimumHeight, height);
        }

        return (
            new Size(Math.Max(width, minimumWidth) / scaling, Math.Max(height, minimumHeight) / scaling),
            new Size(minimumWidth / scaling, minimumHeight / scaling));
    }

    // Which close the shell is asked about: the operating system's shutdown
    // or log-off, or any other (shell design 3.1 row 5). A close the
    // application's lifetime asks for is an ordinary one until start-up
    // (#421) gives it a use of its own.
    public static CloseKind KindOf(WindowCloseReason reason) =>
        reason == WindowCloseReason.OSShutdown ? CloseKind.Shutdown : CloseKind.Ordinary;

    // An operating-system shutdown goes through at once, once running library
    // work has ended. Any other close is held while the shell's step runs
    // (the page asked, a running load cancelled and awaited) and made again
    // when it says yes; a step that answers at once decides this close.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_closeAllowed && DataContext is ShellViewModel shell)
        {
            CloseKind kind = KindOf(e.CloseReason);
            if (_closePending && kind == CloseKind.Ordinary)
            {
                e.Cancel = true;
            }
            else
            {
                ValueTask<bool> ready = shell.CloseAsync(kind);
                if (!ready.IsCompleted)
                {
                    e.Cancel = true;
                    _closePending = true;
                    CloseWhenReady(ready);
                }
                else if (!ready.Result)
                {
                    e.Cancel = true;
                }
            }
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        base.OnClosed(e);
    }

    // Async void as an event handler is: a fault in the page's answer reaches
    // the interface thread, as any handler's would.
    private async void CloseWhenReady(ValueTask<bool> ready)
    {
        bool close;
        try
        {
            close = await ready;
        }
        finally
        {
            _closePending = false;
        }

        if (close && !_closed)
        {
            _closeAllowed = true;
            try
            {
                Close();
            }
            finally
            {
                _closeAllowed = false;
            }
        }
    }

    // The title bar and borders around the client area, in the window's
    // units, from the frame the platform reports (Avalonia's WindowBase takes
    // it from the platform when it is made). Before the window is shown,
    // Windows and macOS report it; X11 reports none until a window manager
    // has mapped the window, so there the cap counts no decoration and the
    // window manager keeps the frame on the screen.
    public static Size DecorationOf(Size? frame, Size client) =>
        frame is { } outer
            ? new Size(Math.Max(0, outer.Width - client.Width), Math.Max(0, outer.Height - client.Height))
            : default;

    // Capped, before the window is shown, to the working area of the screen
    // it opens on: the one Avalonia centres it on (with no owner, the screen
    // at its position, else the primary), which then centres the whole
    // frame. Today's window takes the screen under the pointer, which
    // Avalonia cannot find before the window exists.
    private void FitToScreen()
    {
        Screens? screens = Screens;
        Screen? screen = screens?.ScreenFromPoint(Position) ?? screens?.Primary;
        if (screen is null)
        {
            return;
        }

        FitTo(screen.WorkingArea.Size, screen.Scaling, DecorationOf(FrameSize, ClientSize));
    }

    // Today's rule applied to this window for a working area, in its screen's
    // pixels at its scaling, with the decoration around the client area: the
    // minimum first, so that the start size is never held under the old one.
    // The requested sizes are MainWindow.axaml's, so a second fit replaces
    // the first.
    public void FitTo(PixelSize workingArea, double scaling, Size decoration)
    {
        (Size start, Size minimum) = StartSizes(workingArea, scaling, decoration, _requestedStart, _requestedMinimum);
        MinWidth = minimum.Width;
        MinHeight = minimum.Height;
        Width = start.Width;
        Height = start.Height;
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
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F, KeyModifiers.Control), Command = new RelayCommand(() => SearchBoard(shell)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F1), Command = shell.ShowHelpCommand });
    }

    // Ctrl+F (Form1.vb ProcessCmdKey, :2553-2560): the Board first, then its search.
    private void SearchBoard(ShellViewModel shell)
    {
        if (shell.ShowBoardCommand.CanExecute(null))
        {
            shell.ShowBoardCommand.Execute(null);
        }

        BoardView.FocusSearch();
    }
}
