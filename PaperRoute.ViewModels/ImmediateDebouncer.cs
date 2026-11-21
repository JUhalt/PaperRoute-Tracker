namespace PaperRoute.ViewModels;

// A wait that ends at once, for the "--self-check" mode, which has no
// interface thread: a search change renders as soon as it is made, as the
// timer's tick would 200 ms later in the app.
public sealed class ImmediateDebouncer : IDebouncer
{
    public void Restart(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public void Stop()
    {
    }
}
