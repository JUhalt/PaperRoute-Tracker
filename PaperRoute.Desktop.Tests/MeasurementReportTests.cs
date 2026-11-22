using System.Text.Json;
using PaperRoute.ViewModels.Measurement;

namespace PaperRoute.Desktop.Tests;

// The busy-PC figures in the measurement report (busy-PC design, sections 2
// and 8), on canned reports: absent is JSON null and "absent" in the table,
// never 0; the failure message's exact text in each variant; and the
// verdicts, rules and failures exactly as before, since every new figure is
// report-only.
[TestClass]
public sealed class MeasurementReportTests
{
    private static readonly MeasurementConditions s_busy = new()
    {
        ThreadClock = "cycles",
        CyclesPerMs = 2_496_000,
        RateCheckBefore = new RateCheck(1.0002, 3, Passed: true),
        RateCheckAfter = new RateCheck(0.9951, 7, Passed: true),
        MachineBusyProcessors = 10.9,
        LoadReadBeforeMs = 8.46,
        LoadReadAfterMs = 398,
        OthersBusyProcessors = 9.8,
        NotRunShare = 0.4123,
        Disturbed = false,
    };

    [TestMethod]
    public void AbsentFiguresAreNullNeverZero()
    {
        BoardMeasurementReport report = Canned(
            [
                new MeasuredRow("500", "render-no-card-changes", "Board render, no card changes", 12.34, 12.34, 250, "UI thread"),
                new MeasuredRow("500", "stage-back-to-all", "Stage filter back to All stages", 404, 404, 250, "UI thread",
                    RunMs: 151.04, NotRunMs: 252.96, RunUiMs: 148.26, PerfCoreShare: 0.25, GcPauseMs: 12, JitMs: 0),
                new MeasuredRow("500", "stage-back-to-all-paint", "Its frame", 20, 20, 250, "UI thread",
                    RunMs: 21, NotRunMs: -1, RunUiMs: 0, GcPauseMs: 0, JitMs: 0, Waits: true),
            ],
            new MeasurementConditions { ThreadClock = "absent: no thread clock on this system" });

        using JsonDocument document = JsonDocument.Parse(report.ToJson());
        JsonElement[] rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        foreach (string name in new[] { "runMs", "notRunMs", "runUiMs", "perfCoreShare", "gcPauseMs", "jitMs" })
        {
            Assert.AreEqual(JsonValueKind.Null, rows[0].GetProperty(name).ValueKind, $"An absent {name} is null.");
        }

        Assert.IsFalse(rows[0].TryGetProperty("waits", out _), "Waits is written only when true.");
        Assert.AreEqual(151, rows[1].GetProperty("runMs").GetDouble());
        Assert.AreEqual(253, rows[1].GetProperty("notRunMs").GetDouble());
        Assert.AreEqual(148.3, rows[1].GetProperty("runUiMs").GetDouble());
        Assert.AreEqual(0.25, rows[1].GetProperty("perfCoreShare").GetDouble());
        Assert.AreEqual(12, rows[1].GetProperty("gcPauseMs").GetDouble());
        Assert.AreEqual(0, rows[1].GetProperty("jitMs").GetDouble(), "A measured 0 stays 0.");
        Assert.AreEqual(-1, rows[2].GetProperty("notRunMs").GetDouble(), "A small negative value is kept, not clamped.");
        Assert.AreEqual(0, rows[2].GetProperty("runUiMs").GetDouble());
        Assert.IsTrue(rows[2].GetProperty("waits").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, rows[2].GetProperty("perfCoreShare").ValueKind);

        JsonElement conditions = document.RootElement.GetProperty("conditions");
        Assert.AreEqual("absent: no thread clock on this system", conditions.GetProperty("threadClock").GetString());
        foreach (string name in new[] { "cyclesPerMs", "rateCheck", "machineBusyProcessors", "stealProcessors", "othersBusyProcessors", "notRunShare", "disturbed" })
        {
            Assert.AreEqual(JsonValueKind.Null, conditions.GetProperty(name).ValueKind, $"An absent {name} is null.");
        }

        CollectionAssert.AreEqual(new[] { JsonValueKind.Null, JsonValueKind.Null },
            conditions.GetProperty("loadReadMs").EnumerateArray().Select(item => item.ValueKind).ToArray());
        Assert.IsFalse(conditions.TryGetProperty("machineName", out _), "The conditions name no PC.");

        string table = report.ToTable();
        StringAssert.Contains(table, "| Wall ms | UI ms | Run UI ms | Budget ms |");
        StringAssert.Contains(table, "| 12 | 12 | absent | 250 (UI thread) | within | enforced |");
        StringAssert.Contains(table, "| 404 | 404 | 148 | 250 (UI thread) | over | enforced |");
        StringAssert.Contains(table, "Load unknown.");

        using JsonDocument none = JsonDocument.Parse(Canned([], conditions: null).ToJson());
        Assert.AreEqual(JsonValueKind.Null, none.RootElement.GetProperty("conditions").ValueKind, "A run without conditions writes null.");
    }

    [TestMethod]
    public void TheConditionsAreWrittenAsMeasured()
    {
        BoardMeasurementReport report = Canned([], s_busy with { StealProcessors = 0.04 });
        using JsonDocument document = JsonDocument.Parse(report.ToJson());
        JsonElement conditions = document.RootElement.GetProperty("conditions");
        Assert.AreEqual("cycles", conditions.GetProperty("threadClock").GetString());
        Assert.AreEqual(2_496_000, conditions.GetProperty("cyclesPerMs").GetDouble());
        JsonElement[] checks = conditions.GetProperty("rateCheck").EnumerateArray().ToArray();
        Assert.HasCount(2, checks);
        Assert.AreEqual(1.0002, checks[0].GetProperty("share").GetDouble());
        Assert.AreEqual(3, checks[0].GetProperty("spins").GetInt32());
        Assert.IsTrue(checks[0].GetProperty("passed").GetBoolean());
        Assert.AreEqual(0.9951, checks[1].GetProperty("share").GetDouble());
        Assert.AreEqual(10.9, conditions.GetProperty("machineBusyProcessors").GetDouble());
        CollectionAssert.AreEqual(new[] { 8.5, 398 }, conditions.GetProperty("loadReadMs").EnumerateArray().Select(item => item.GetDouble()).ToArray());
        Assert.AreEqual(0.04, conditions.GetProperty("stealProcessors").GetDouble());
        Assert.AreEqual(9.8, conditions.GetProperty("othersBusyProcessors").GetDouble());
        Assert.AreEqual(0.412, conditions.GetProperty("notRunShare").GetDouble());
        Assert.IsFalse(conditions.GetProperty("disturbed").GetBoolean());

        StringAssert.Contains(report.ToTable(),
            "Report-only: thread clock cycles. Other programs kept 9.8 of 12 processors busy on average (10.9 with this one). " +
            "The interface thread was not running for 41% of the Wall of the rows that do not wait. Not disturbed.");
    }

    // The message of section 8, word for word, from the failed row and the
    // run's conditions.
    [TestMethod]
    public void TheFailureMessageStatesWhatWasMeasured()
    {
        MeasuredRow backToAll = new("500", "stage-back-to-all", "Stage filter back to All stages", 404, 404, 250, "UI thread",
            RunMs: 151, NotRunMs: 253, RunUiMs: 148, GcPauseMs: 12, JitMs: 0);
        Assert.AreEqual(
            Lines(
                "500: Stage filter back to All stages took 404 ms against 250 ms.",
                "  The interface thread ran for 151 ms of it and was not running for 253 ms (63%); garbage collection paused 12 ms, compilation 0 ms.",
                "During this run other programs kept 9.8 of 12 processors busy on average.",
                "Timing tests need a quiet PC. Close other programs and run the test again before reading this as a slower Board."),
            Canned([backToAll], s_busy).FailureMessage());

        // No thread clock.
        Assert.AreEqual(
            Lines(
                "500: Stage filter back to All stages took 404 ms against 250 ms.",
                "  How long the interface thread ran could not be read on this system.",
                "During this run other programs kept 9.8 of 12 processors busy on average.",
                BoardMeasurementReport.QuietPcLine),
            Canned([backToAll with { RunMs = null, NotRunMs = null, RunUiMs = null }], s_busy).FailureMessage());

        // No load reading, and no conditions at all.
        string noLoad = Lines(
            "500: Stage filter back to All stages took 404 ms against 250 ms.",
            "  The interface thread ran for 151 ms of it and was not running for 253 ms (63%); garbage collection paused 12 ms, compilation 0 ms.",
            "How busy the PC was could not be read on this system.",
            BoardMeasurementReport.QuietPcLine);
        Assert.AreEqual(noLoad, Canned([backToAll], s_busy with { MachineBusyProcessors = null, OthersBusyProcessors = null, Disturbed = null }).FailureMessage());
        Assert.AreEqual(noLoad, Canned([backToAll], conditions: null).FailureMessage());

        // The thread ran for at least 90% of the row.
        MeasuredRow ran = new("500", "stage-back-to-all", "Stage filter back to All stages", 257, 257, 250, "UI thread",
            RunMs: 240, NotRunMs: 17, RunUiMs: 236, GcPauseMs: 3, JitMs: 1);
        Assert.AreEqual(
            Lines(
                "500: Stage filter back to All stages took 257 ms against 250 ms.",
                "  The interface thread was running for 240 of the 257 ms. A busy PC can still slow running code.",
                "During this run other programs kept 9.8 of 12 processors busy on average.",
                BoardMeasurementReport.QuietPcLine),
            Canned([ran], s_busy).FailureMessage());

        // The budget shown is the run's own: a Debug build doubles it.
        StringAssert.StartsWith(
            Canned([backToAll with { UiMs = 600, WallMs = 600 }], s_busy, multiplier: 2).FailureMessage(),
            "500: Stage filter back to All stages took 600 ms against 500 ms.");

        // Two failures: each row's two lines, then the load and the quiet-PC line once.
        MeasuredRow openList = new("500", "open-list-stepped-closed", "The open stage list stepped Idea, Draft, Submitted, then closed", 329, 318, 250, "UI thread");
        Assert.AreEqual(
            Lines(
                "500: Stage filter back to All stages took 404 ms against 250 ms.",
                "  The interface thread ran for 151 ms of it and was not running for 253 ms (63%); garbage collection paused 12 ms, compilation 0 ms.",
                "500: The open stage list stepped Idea, Draft, Submitted, then closed took 318 ms against 250 ms.",
                BoardMeasurementReport.RunUnreadableLine,
                "During this run other programs kept 9.8 of 12 processors busy on average.",
                BoardMeasurementReport.QuietPcLine),
            Canned([backToAll, openList], s_busy).FailureMessage());

        // Nothing failed, nothing to say; a waiting or informational row over
        // its budget is not a failure.
        Assert.AreEqual(string.Empty, Canned([backToAll with { UiMs = 200, WallMs = 200 }], s_busy).FailureMessage());
        Assert.AreEqual(string.Empty, Canned([backToAll with { Informational = true }], s_busy).FailureMessage());
        Assert.AreEqual(string.Empty, Canned([backToAll], s_busy, waiting: ["stage-back-to-all"]).FailureMessage());
    }

    // The new figures change no verdict, rule or failure: a row with every
    // figure and the same row with none are judged alike.
    [TestMethod]
    public void TheVerdictsAndRulesIgnoreTheNewFigures()
    {
        MeasuredRow[] plain =
        [
            new MeasuredRow("500", "render-no-card-changes", "Board render", 12, 12, 250, "UI thread"),
            new MeasuredRow("500", "first-switch-published", "First switch", 140, 130, 100, "page switch"),
            new MeasuredRow("500", "stage-back-to-all", "Back to All", 404, 404, 250, "UI thread"),
            new MeasuredRow("500", "switch-back-pipeline", "Switch back", 300, 300, 100, "page switch", Informational: true),
            new MeasuredRow("example", "stage-back-to-all", "Back to All", 400, 400, 250, "UI thread"),
        ];
        MeasuredRow[] measured = plain.Select(row => row with
        {
            RunMs = row.WallMs / 10, NotRunMs = row.WallMs * 0.9, RunUiMs = 1, PerfCoreShare = 1, GcPauseMs = 0, JitMs = 0, Waits = true,
        }).ToArray();

        BoardMeasurementReport before = Canned(plain, conditions: null, waiting: ["first-switch-published"]);
        BoardMeasurementReport after = Canned(measured, s_busy with { Disturbed = true }, waiting: ["first-switch-published"]);
        CollectionAssert.AreEqual(plain.Select(before.VerdictFor).ToArray(), measured.Select(after.VerdictFor).ToArray());
        CollectionAssert.AreEqual(plain.Select(before.RuleFor).ToArray(), measured.Select(after.RuleFor).ToArray());
        CollectionAssert.AreEqual(new[] { "stage-back-to-all" }, before.Failures().Select(row => row.Id).ToArray());
        CollectionAssert.AreEqual(before.Failures().Select(row => row.Id).ToArray(), after.Failures().Select(row => row.Id).ToArray());
        CollectionAssert.AreEqual(
            new[] { "within", "over", "over", "over", "over" },
            measured.Select(after.VerdictFor).ToArray());
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private static BoardMeasurementReport Canned(IReadOnlyList<MeasuredRow> rows, MeasurementConditions? conditions, double multiplier = 1, IReadOnlyList<string>? waiting = null) => new()
    {
        Mode = "headless",
        MachineName = "TEST-PC",
        Dpi = 96,
        Option = "b",
        ResetThreshold = 64,
        Day = new DateTime(2026, 11, 21),
        DaySource = "--today",
        Build = multiplier == 1 ? "Release" : "Debug",
        Multiplier = multiplier,
        TakenAt = new DateTime(2026, 11, 21, 12, 0, 0),
        Processors = 12,
        WaitingForRowsOfN = waiting ?? [],
        HeldLibrary = "500",
        Rows = rows,
        Conditions = conditions,
    };
}
