using Avalonia.Threading;
using PaperRoute.ViewModels;

namespace PaperRoute.Desktop;

// The app's IUiDispatcher (d1 3.4): Dispatcher.Post on the named dispatcher
// (the app passes Dispatcher.UIThread), the Avalonia form of Form1's
// BeginInvoke. The default priority runs the work after what is in hand.
public sealed class UiThreadDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public UiThreadDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.Post(action);
    }
}
