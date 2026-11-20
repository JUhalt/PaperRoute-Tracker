namespace PaperRoute.ViewModels;

// Posted work held in a queue until RunAll, for the tests and the
// "--self-check" mode, which have no interface thread: what a posted flush
// does is then seen at a known point, and nothing waits.
public sealed class QueuedUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _queue = new();

    // Work posted and not yet run.
    public int Count => _queue.Count;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(action);
    }

    // Runs the work posted so far, and any work it posts in turn, in order.
    // Returns how many ran.
    public int RunAll()
    {
        int ran = 0;
        while (_queue.TryDequeue(out Action? action))
        {
            action();
            ran++;
        }

        return ran;
    }
}
