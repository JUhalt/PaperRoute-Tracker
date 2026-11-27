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

        // The counters move in steps (15.6 ms a processor on Windows), so two
        // readings a millisecond apart give a meaningless rate; a run is
        // seconds long. Half a second apart keeps the logged figure readable.
        Thread.Sleep(500);
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
        Assert.IsTrue(busy.Disturbed!.Value, "8 of 12 is more than a quarter of the processors.");

        // This process counted more than the machine (the two clocks differ):
        // others' load is 0, not negative.
        MeasurementConditions own = RunConditions.Compute(clock, before, new(3_000, 9, 101, null, 14), [], 12);
        Assert.AreEqual(0, own.OthersBusyProcessors!.Value);
        Assert.IsNull(own.StealProcessors);

        // The line itself: TheDisturbedLineIsAQuarterOfTheProcessors.
        Assert.IsTrue(RunConditions.Compute(clock, before, new(3_000, 9, 122.2, null, 12), [], 12).Disturbed!.Value, "10.1 of 12 is disturbed.");
        Assert.IsTrue(RunConditions.Compute(clock, before, new(3_000, 9, 122, null, 12), [], 12).Disturbed!.Value, "10 of 12 is disturbed.");

        // No load reading: every load figure null, and so is disturbed.
        MeasurementConditions unknown = RunConditions.Compute(clock, before, new(3_000, null, null, null, 12), [], 12);
        Assert.IsNull(unknown.MachineBusyProcessors);
        Assert.IsNull(unknown.OthersBusyProcessors);
        Assert.IsNull(unknown.Disturbed);
        Assert.IsNull(unknown.LoadReadAfterMs);
    }

    // The line the maintainer set on November 27, 2026 (busy-PC design,
    // section 2): a run is disturbed when other programs keep more than a
    // quarter of the processors busy, 3 of 12 and 1 of 4. Just under the line
    // and on it is not disturbed; just over it is. An absent reading stays
    // "not disturbed, load unknown".
    [TestMethod]
    public void TheDisturbedLineIsAQuarterOfTheProcessors()
    {
        ThreadClock clock = ThreadClock.Absent(ThreadClock.NoClockReason);
        LoadReading before = new(1_000, 8, BusySeconds: 100, StealSeconds: null, ProcessSeconds: 10);

        // Two seconds, this process 1 processor's worth: the machine's busy
        // seconds that leave others at the given load.
        MeasurementConditions WithOthers(double others, int processors) =>
            RunConditions.Compute(clock, before, new(3_000, 9, 100 + 2 * (others + 1), null, 12), [], processors);

        Assert.AreEqual(3.0, MeasurementConditions.DisturbedLine(12));
        Assert.AreEqual(1.0, MeasurementConditions.DisturbedLine(4));
        foreach ((int processors, double line) in new[] { (12, 3.0), (4, 1.0) })
        {
            MeasurementConditions under = WithOthers(line - 0.01, processors);
            MeasurementConditions on = WithOthers(line, processors);
            MeasurementConditions over = WithOthers(line + 0.01, processors);
            Assert.AreEqual(line - 0.01, under.OthersBusyProcessors!.Value, 1e-9);
            Assert.AreEqual(line + 0.01, over.OthersBusyProcessors!.Value, 1e-9);
            string which = string.Create(CultureInfo.InvariantCulture, $"of {processors} processors, the line at {line:0}");
            Assert.IsFalse(under.Disturbed!.Value, $"Just under the line is not disturbed ({which}).");
            Assert.IsFalse(on.Disturbed!.Value, $"On the line is not over it ({which}).");
            Assert.IsTrue(over.Disturbed!.Value, $"Just over the line is disturbed ({which}).");
            StringAssert.EndsWith(BoardMeasurementReport.DescribeConditions(under, processors), "Not disturbed.");
            StringAssert.EndsWith(BoardMeasurementReport.DescribeConditions(over, processors),
                string.Create(CultureInfo.InvariantCulture, $"Disturbed: other programs kept more than {line:0} of {processors} processors busy."));

            MeasurementConditions unknown = RunConditions.Compute(clock, before, new(3_000, null, null, null, 12), [], processors);
            Assert.IsNull(unknown.Disturbed, "No load reading, no line to hold it to.");
            StringAssert.EndsWith(BoardMeasurementReport.DescribeConditions(unknown, processors), "Load unknown.");
        }
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

        // The scroll row's figures are its longest-UI step's (Wall 200 ms),
        // not its longest Wall (500 ms): the share counts that step's Wall.
        MeasuredRow scroll = new("500", "scroll-published-longest-step", "Scroll", 500, 180, 250, "UI thread", RunMs: 150, NotRunMs: 50);
        Assert.AreEqual(50.0 / 200, RunConditions.NotRunShare([scroll])!.Value, 1e-9);

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
