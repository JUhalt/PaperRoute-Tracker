using System.Diagnostics;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Tests;

// The app's two timing services (d1 3.4) on the headless platform's
// interface thread: Post runs the work once the dispatcher gets to it, not
// before; the debouncer runs only the last action restarted, once, after its
// delay, and a stopped one never. The Board's own rules are tested with the
// hand-run twins (ManualDebouncer, QueuedUiDispatcher); these check that the
// app's versions keep the same promises.
[TestClass]
public sealed class UiThreadTimingTests
{
    [TestMethod]
    public void PostedWorkRunsOnceTheDispatcherGetsToIt()
    {
        (int before, int after, bool onThread) = Headless.Run(() =>
        {
            Dispatcher dispatcher = Dispatcher.UIThread;
            var posts = new UiThreadDispatcher(dispatcher);
            int ran = 0;
            bool onThread = false;
            posts.Post(() =>
            {
                ran++;
                onThread = dispatcher.CheckAccess();
            });
            int before = ran;
            dispatcher.RunJobs();
            return (before, ran, onThread);
        });

        Assert.AreEqual(0, before, "Posted work does not run inside Post.");
        Assert.AreEqual(1, after, "Posted work ran once.");
        Assert.IsTrue(onThread, "Posted work ran on the interface thread.");
    }

    [TestMethod]
    public void TheDebouncerRunsTheLastActionOnceAfterItsDelay()
    {
        (List<string> ran, long elapsed) = Headless.Run(() =>
        {
            Dispatcher dispatcher = Dispatcher.UIThread;
            var ran = new List<string>();
            var frame = new DispatcherFrame();
            var delay = TimeSpan.FromMilliseconds(50);

            var search = new UiThreadDebouncer(dispatcher);
            var stopped = new UiThreadDebouncer(dispatcher);
            var clock = Stopwatch.StartNew();
            search.Restart(delay, () => ran.Add("first"));
            search.Restart(delay, () => ran.Add("restarted"));
            stopped.Restart(TimeSpan.FromMilliseconds(10), () => ran.Add("stopped"));
            stopped.Stop();

            // Ends the frame a while after the wait, so a second tick would
            // be seen; and at the latest after 10 s, so a timer that never
            // fires fails the test rather than hanging it.
            var end = new UiThreadDebouncer(dispatcher);
            search.Restart(delay, () =>
            {
                ran.Add("last");
                end.Restart(TimeSpan.FromMilliseconds(200), () => frame.Continue = false);
            });
            var limit = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Background, dispatcher, (_, _) => frame.Continue = false);
            limit.Start();
            try
            {
                dispatcher.PushFrame(frame);
            }
            finally
            {
                limit.Stop();
            }

            return (ran, clock.ElapsedMilliseconds);
        });

        CollectionAssert.AreEqual(new[] { "last" }, ran, "Only the last action restarted ran, once; the stopped one never.");
        Assert.IsGreaterThanOrEqualTo(50L, elapsed, "The action waited for its delay.");
        Assert.IsLessThan(10_000L, elapsed, "The wait ended well before the time limit.");
    }
}
