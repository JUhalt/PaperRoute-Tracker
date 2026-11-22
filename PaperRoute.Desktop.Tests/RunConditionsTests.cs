using System.Globalization;
using PaperRoute.Desktop.Measurement;
using PaperRoute.ViewModels.Measurement;

namespace PaperRoute.Desktop.Tests;

// What the PC was doing around a run (busy-PC design, section 2): the
// machine-load readings are present or null on Windows and Linux and never
// throw; others' load is never below 0; the disturbed line and the share of
// time not running follow their definitions. No check depends on how busy the
// PC is.
[TestClass]
public sealed class RunConditionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TheReadingsArePresentOrNullAndNeverThrow()
    {
        RunConditions run = RunConditions.Begin();
        LoadReading first = RunConditions.ReadOffThread();
        LoadReading second = RunConditions.ReadOffThread();
        TestContext.WriteLine(Describe("first", first));
        TestContext.WriteLine(Describe("second", second));
        Assert.IsGreaterThan(first.MidpointMs, second.MidpointMs, "The second reading is later.");
        foreach (LoadReading reading in new[] { first, second })
        {
            Assert.AreEqual(reading.BusySeconds is null, reading.ReadMs is null, "A reading's length is given when, and only when, the load was read.");
            Assert.IsTrue(reading.ReadMs is null or >= 0, "A reading takes no negative time.");
            if (OperatingSystem.IsWindows())
            {
                Assert.IsNull(reading.StealSeconds, "Steal is read on Linux only.");
            }
        }

        MeasurementConditions conditions = run.End(ThreadClock.Absent(ThreadClock.NoClockReason), []);
        TestContext.WriteLine(BoardMeasurementReport.DescribeConditions(conditions, Environment.ProcessorCount));
        Assert.AreEqual("absent: " + ThreadClock.NoClockReason, conditions.ThreadClock);
        Assert.IsNull(conditions.NotRunShare, "No rows, no share.");
        Assert.IsTrue(conditions.OthersBusyProcessors is null or >= 0, "Others' load is never below 0.");
        Assert.AreEqual(conditions.OthersBusyProcessors is null, conditions.Disturbed is null, "Disturbed is null exactly when the load is.");
    }

    [TestMethod]
    public void OthersLoadIsTheMachinesLessThisProcessAndNeverBelowZero()
    {
        ThreadClock clock = ThreadClock.Absent(ThreadClock.NoClockReason);
        LoadReading before = new(1_000, 8, BusySeconds: 100, StealSeconds: 2, ProcessSeconds: 10);

        // Two seconds: the machine busy 9 processors' worth, this process 1.
        MeasurementConditions busy = RunConditions.Compute(clock, before, new(3_000, 9, 118, 2.1, 12), [], 12);
        Assert.AreEqual(9, busy.MachineBusyProcessors!.Value, 1e-9);
        Assert.AreEqual(8, busy.OthersBusyProcessors!.Value, 1e-9);
        Assert.AreEqual(0.05, busy.StealProcessors!.Value, 1e-9);
        Assert.AreEqual(8.0, busy.LoadReadBeforeMs!.Value);
        Assert.AreEqual(9.0, busy.LoadReadAfterMs!.Value);
        Assert.IsFalse(busy.Disturbed!.Value, "8 of 12 busy leaves 4 free.");

        // This process counted more than the machine (the two clocks differ):
        // others' load is 0, not negative.
        MeasurementConditions own = RunConditions.Compute(clock, before, new(3_000, 9, 101, null, 14), [], 12);
        Assert.AreEqual(0, own.OthersBusyProcessors!.Value);
        Assert.IsNull(own.StealProcessors);

        // The line: more than processors - 2 busy with other programs.
        Assert.IsTrue(RunConditions.Compute(clock, before, new(3_000, 9, 122.2, null, 12), [], 12).Disturbed!.Value, "10.1 of 12 leaves fewer than two free.");
        Assert.IsFalse(RunConditions.Compute(clock, before, new(3_000, 9, 122, null, 12), [], 12).Disturbed!.Value, "10 of 12 leaves two free.");

        // No load reading: every load figure null, and so is disturbed.
        MeasurementConditions unknown = RunConditions.Compute(clock, before, new(3_000, null, null, null, 12), [], 12);
        Assert.IsNull(unknown.MachineBusyProcessors);
        Assert.IsNull(unknown.OthersBusyProcessors);
        Assert.IsNull(unknown.Disturbed);
        Assert.IsNull(unknown.LoadReadAfterMs);
    }

    [TestMethod]
    public void TheShareNotRunningCountsTheRowsThatDoNotWait()
    {
        MeasuredRow[] rows =
        [
            new("500", "a", "A", 100, 100, 250, "UI thread", RunMs: 60, NotRunMs: 40),
            new("500", "b", "B", 300, 300, 250, "UI thread", RunMs: 280, NotRunMs: 20),
            new("500", "a-paint", "A's frame", 50, 50, 250, "UI thread", RunMs: 5, NotRunMs: 45, Waits: true),
            new("500", "c", "C", 1_000, 1_000, 250, "UI thread"),
        ];
        Assert.AreEqual(60.0 / 400, RunConditions.NotRunShare(rows)!.Value, 1e-9);
        Assert.IsNull(RunConditions.NotRunShare([rows[2], rows[3]]), "No row that does not wait has the figure.");

        MeasurementConditions conditions = RunConditions.Compute(ThreadClock.Absent(ThreadClock.RateCheckFailedReason),
            new(0, null, null, null, null), new(1_000, null, null, null, null), rows, 12);
        Assert.AreEqual(0.15, conditions.NotRunShare!.Value, 1e-9);
        Assert.AreEqual("absent: rate check failed", conditions.ThreadClock);
    }

    // The core-class map: present on a PC with two kinds of core, null on one
    // kind or where it cannot be read. Either way, asking never throws.
    [TestMethod]
    public void TheCoreClassMapIsPresentOrNull()
    {
        ProcessorClasses? classes = ProcessorClasses.Current;
        if (classes is null)
        {
            TestContext.WriteLine("No core-class map on this system (one kind of core, or not readable).");
            return;
        }

        TestContext.WriteLine($"{classes.FastProcessors} of {classes.AllProcessors} logical processors are of the fastest class; this thread is on one now: {classes.OnFastCore()?.ToString() ?? "unknown"}.");
        Assert.IsTrue(classes.FastProcessors > 0 && classes.FastProcessors < classes.AllProcessors, "Two classes at least.");
    }

    private static string Describe(string name, LoadReading reading) => string.Create(CultureInfo.InvariantCulture,
        $"The {name} reading at {reading.MidpointMs:0.0} ms took {reading.ReadMs?.ToString("0.0", CultureInfo.InvariantCulture) ?? "absent"} ms: busy {reading.BusySeconds?.ToString("0.0", CultureInfo.InvariantCulture) ?? "absent"} s, steal {reading.StealSeconds?.ToString("0.0", CultureInfo.InvariantCulture) ?? "absent"} s, this process {reading.ProcessSeconds?.ToString("0.00", CultureInfo.InvariantCulture) ?? "absent"} s.");
}
