using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace PaperRoute.Desktop.Tests.Startup;

// A stand-in for a window's thread, for models that need no window: one
// thread with its own synchronization context, which runs the work it is
// given and every continuation posted back to it until the work's task ends,
// as an interface thread runs an awaited command. Run returns the thread's
// managed id, so a test can tell what ran there from what ran on a worker.
internal static class WindowThread
{
    public static int Run(Func<Task> start, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(start);
        var context = new QueueContext();
        int id = 0;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            id = Environment.CurrentManagedThreadId;
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                Task work = start();
                context.RunUntil(work, limit);
                work.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Window thread stand-in",
        };

        thread.Start();
        if (!thread.Join(limit + TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException($"The work on the window thread stand-in did not end within {limit.TotalSeconds} s.");
        }

        failure?.Throw();
        return id;
    }

    private sealed class QueueContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("The window thread stand-in takes posted work only.");

        // Runs posted work until the task ends; its end wakes the loop.
        public void RunUntil(Task work, TimeSpan limit)
        {
            _ = work.ContinueWith(done => _queue.Add((state => { }, null)), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var clock = Stopwatch.StartNew();
            while (!work.IsCompleted)
            {
                TimeSpan left = limit - clock.Elapsed;
                if (left <= TimeSpan.Zero)
                {
                    throw new TimeoutException($"The work on the window thread stand-in did not end within {limit.TotalSeconds} s.");
                }

                if (_queue.TryTake(out (SendOrPostCallback Callback, object? State) item, left))
                {
                    item.Callback(item.State);
                }
            }
        }
    }
}
