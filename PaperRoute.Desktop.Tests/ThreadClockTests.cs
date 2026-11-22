using System.Diagnostics;
using PaperRoute.Desktop.Measurement;

namespace PaperRoute.Desktop.Tests;

// The thread clock of the busy-PC design (section 4). Every check is
// one-sided with the relative margin of the rate check (1.02 x wall + 1 ms):
// a busy PC only takes running time away from a thread, so it cannot fail
// them. Where the system has no clock the fields are null and the test passes.
[TestClass]
public sealed class ThreadClockTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RunningTimeIsNeverMoreThanTheStopwatchTime()
    {
        ThreadClock clock = ThreadClock.Open();
        TestContext.WriteLine($"Thread clock: {clock.Describe()}; nominal {clock.CyclesPerMs?.ToString("0", System.Globalization.CultureInfo.InvariantCulture) ?? "none"} cycles per ms; rate check {clock.Before?.ToString() ?? "none"}.");
        double? start = clock.Read();
        var watch = Stopwatch.StartNew();
        double work = 0;
        while (watch.Elapsed.TotalMilliseconds < 30)
        {
            work += Math.Sqrt(work + 1);
        }

        double? end = clock.Read();
        watch.Stop();
        clock.Close();
        TestContext.WriteLine($"Second rate check: {clock.After?.ToString() ?? "none"}; the clock is now {clock.Describe()}.");
        if (start is not { } from || end is not { } to)
        {
            Assert.IsNull(start, "An absent clock reads null at the start.");
            Assert.IsNull(end, "An absent clock reads null at the end.");
            Assert.IsFalse(string.IsNullOrEmpty(clock.Reason), "An absent clock says why.");
            return;
        }

        double ran = to - from;
        double wall = watch.Elapsed.TotalMilliseconds;
        TestContext.WriteLine($"Spun {wall:0.00} ms by the stopwatch, ran {ran:0.00} ms by the thread clock (bound {(1.02 * wall) + 1:0.00} ms); work {work:0}.");
        Assert.IsGreaterThanOrEqualTo(0, ran, "The thread clock does not run backwards.");
        Assert.IsLessThanOrEqualTo((1.02 * wall) + 1, ran, $"Ran {ran:0.00} ms in {wall:0.00} ms by the stopwatch.");
    }

    [TestMethod]
    public void ASleepIsNotRunningTime()
    {
        ThreadClock clock = ThreadClock.Open();
        double? start = clock.Read();
        Thread.Sleep(50);
        double? end = clock.Read();
        if (start is not { } from || end is not { } to)
        {
            TestContext.WriteLine($"Thread clock {clock.Describe()}: nothing to check.");
            Assert.IsNull(end, "An absent clock reads null.");
            return;
        }

        TestContext.WriteLine($"A 50 ms sleep ran {to - from:0.000} ms by the thread clock.");
        Assert.IsLessThan(5, to - from, "A sleeping thread does not run.");
    }

    [TestMethod]
    public void AnAbsentClockReadsNullAndSaysWhy()
    {
        foreach (string reason in new[] { ThreadClock.NoClockReason, ThreadClock.NominalUnreadableReason, ThreadClock.RateCheckFailedReason })
        {
            ThreadClock clock = ThreadClock.Absent(reason);
            Assert.IsFalse(clock.IsPresent);
            Assert.IsNull(clock.Read());
            Assert.IsNull(clock.CyclesPerMs);
            Assert.AreEqual("absent: " + reason, clock.Describe());
            clock.Close();
            Assert.IsNull(clock.After, "An absent clock runs no second check.");
        }
    }

    // What this system gives: on Windows a cycle clock with both rate checks
    // shown, on Linux the thread's own processor time. Neither fails the test
    // when absent; the log shows which.
    [TestMethod]
    public void TheClockSaysWhichKindItIs()
    {
        ThreadClock clock = ThreadClock.Open();
        clock.Close();
        TestContext.WriteLine($"Thread clock: {clock.Describe()}; first rate check {clock.Before?.ToString() ?? "none"}; second {clock.After?.ToString() ?? "none"}.");
        if (!clock.IsPresent)
        {
            CollectionAssert.Contains(new[] { ThreadClock.NoClockReason, ThreadClock.NominalUnreadableReason, ThreadClock.RateCheckFailedReason }, clock.Reason);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(ThreadClock.CyclesKind, clock.Kind);
            Assert.IsNotNull(clock.CyclesPerMs);
            Assert.IsTrue(clock.Before is { Passed: true } && clock.After is { Passed: true }, "A present Windows clock passed both rate checks.");
        }
        else
        {
            Assert.AreEqual(ThreadClock.ThreadCpuTimeKind, clock.Kind);
            Assert.IsNull(clock.CyclesPerMs, "Linux needs no conversion.");
            Assert.IsNull(clock.Before);
        }
    }
}
