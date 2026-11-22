using Avalonia.Threading;
using PaperRoute.Desktop.Measurement;
using PaperRoute.ViewModels.Measurement;

namespace PaperRoute.Desktop.Tests;

// How BoardMeasurement fills a row's busy-PC figures (busy-PC design,
// sections 2 to 4), on a timed step with no window: an absent clock gives
// null, never 0; a clock whose second rate check fails has its figures
// dropped by Finish; the machine's load is read off the interface thread; and
// the real window's process-start row leaves out the untimed set-up. No check
// depends on how busy the PC is.
[TestClass]
public sealed class BusyPcFiguresTests
{
    [TestMethod]
    public void AnAbsentClockGivesNullRunFiguresNeverZero()
    {
        MeasuredRow row = Headless.Run(() =>
        {
            var measurement = new BoardMeasurement(_ => { }, ThreadClock.Absent(ThreadClock.NoClockReason));
            return measurement.Time("500", "step", "A step", BoardMeasurement.UiThreadBudget, "UI thread", () => Thread.Sleep(5));
        });

        Assert.IsNull(row.RunMs, "No clock, no running time.");
        Assert.IsNull(row.NotRunMs, "No clock, no time not running.");
        Assert.IsNull(row.RunUiMs, "No clock, no Run UI.");
        Assert.IsTrue(row.GcPauseMs is >= 0 && row.JitMs is >= 0, "The GC and compilation counters need no clock.");
        Assert.IsGreaterThan(0, row.WallMs);
    }

    [TestMethod]
    public void AFailedSecondRateCheckDropsTheRunFigures()
    {
        foreach (bool passes in new[] { true, false })
        {
            double reading = 0;
            ThreadClock clock = ThreadClock.Canned(() => reading += 0.5, new RateCheck(passes ? 1.0 : 1.05, 1, passes));
            (MeasuredRow timed, MeasuredRow finished) = Headless.Run(() =>
            {
                var measurement = new BoardMeasurement(_ => { }, clock);
                MeasuredRow row = measurement.Time("500", "step", "A step", BoardMeasurement.UiThreadBudget, "UI thread", () => Thread.Sleep(5));
                measurement.Finish();
                return (row, measurement.Rows[0]);
            });

            Assert.IsNotNull(timed.RunMs, "A present clock gives running time.");
            Assert.AreEqual(timed.WallMs - timed.RunMs!.Value, timed.NotRunMs!.Value, 1e-9, "Not running is Wall less running time.");
            Assert.IsNotNull(timed.RunUiMs);
            if (passes)
            {
                Assert.AreEqual(timed, finished, "A passed second check keeps every figure.");
                Assert.IsTrue(clock.IsPresent);
                continue;
            }

            Assert.IsNull(finished.RunMs, "A failed second check drops the running time.");
            Assert.IsNull(finished.NotRunMs);
            Assert.IsNull(finished.RunUiMs);
            Assert.AreEqual(timed.GcPauseMs, finished.GcPauseMs, "The counters that need no clock stay.");
            Assert.AreEqual(timed.WallMs, finished.WallMs, "Wall and UI do not change.");
            Assert.AreEqual(timed.UiMs, finished.UiMs);
            Assert.AreEqual("absent: " + ThreadClock.RateCheckFailedReason, clock.Describe());
        }
    }

    // The interface thread hands the reading to a thread of its own and
    // waits for it, untimed: GetSystemTimes can block for hundreds of ms.
    [TestMethod]
    public void TheLoadIsNotReadOnTheInterfaceThread()
    {
        (int interfaceThread, LoadReading reading) = Headless.Run(() =>
        {
            Assert.IsTrue(Dispatcher.UIThread.CheckAccess(), "Run on the interface thread.");
            return (Environment.CurrentManagedThreadId, RunConditions.ReadOffThread());
        });

        Assert.AreNotEqual(0, reading.ReadOnThread, "The reading says which thread read it.");
        Assert.AreNotEqual(interfaceThread, reading.ReadOnThread, "The load was read on another thread than the interface thread.");
    }

    // In a real window the clock's first rate check, the core-class map and
    // the first load reading run after the process starts and before the
    // Board; the process-start row leaves them out, so it reads as before.
    [TestMethod]
    public void TheProcessStartRowLeavesOutTheSetUp()
    {
        MeasuredRow row = WindowMeasurement.ProcessStartRow("snapshot", sinceStartMs: 2_000, setUpMs: 140.4);
        Assert.AreEqual(BoardMeasurement.ProcessStartToBoard, row.Id);
        Assert.AreEqual(1_859.6, row.WallMs, 1e-9);
        Assert.AreEqual(row.WallMs, row.UiMs);
        Assert.IsTrue(row.Informational && row.Waits);
        Assert.AreEqual("the busy-PC set-up before it (140 ms, untimed) left out", row.Note);
        Assert.IsNull(row.RunMs, "The process-start row is not timed on the thread.");
    }
}
