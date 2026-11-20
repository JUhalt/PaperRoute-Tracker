using Avalonia.Threading;
using PaperRoute.ViewModels;

namespace PaperRoute.Desktop;

// The app's search wait (d1 3.4): a one-shot DispatcherTimer, the twin of
// Form1's boardSearchDebounceTimer (Form1.vb:79-81, 200 ms, which the Board
// passes as BoardViewModel.SearchDelay). The dispatcher is named, not taken
// from the current thread, because in Avalonia 12 a DispatcherTimer
// otherwise uses Dispatcher.CurrentDispatcher (r2 8, item 4); the app passes
// Dispatcher.UIThread. Create and use it on that dispatcher's thread.
public sealed class UiThreadDebouncer : IDebouncer
{
    private readonly DispatcherTimer _timer;
    private Action? _action;

    public UiThreadDebouncer(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _timer.Tick += OnTick;
    }

    public void Restart(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _timer.Stop();
        _action = action;
        _timer.Interval = delay;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _action = null;
    }

    // One tick, then the timer stops, as Form1's handler stops its timer
    // first (BoardSearchDebounceElapsed, :1905-1918).
    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        Action? action = _action;
        _action = null;
        action?.Invoke();
    }
}
