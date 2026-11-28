using System.Globalization;
using System.Text;
using System.Text.Json;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Measurement;

// One timed action (d1 6.3): the library it ran on, a stable id, the action
// in words, Wall (a Stopwatch around it) and UI (the longest gap between the
// ticks of a 1 ms probe on the interface thread, never more than Wall), and
// the budget it is held to. An informational row has a budget for scale only
// and is never held to it; a note says what the row cannot show by itself.
//
// The busy-PC figures (busy-PC design, section 2) are report-only: no verdict
// reads them. Each is null when it could not be read, never 0. RunMs is how
// long the interface thread ran on a processor during the action (its thread
// clock), NotRunMs is WallMs - RunMs as computed (a small negative value shows
// the clock's error), RunUiMs the largest running time inside one gap of the
// UI probe (not validated: BoardMeasurementReport.RunUiStatus), PerfCoreShare the share of the probe's ticks on the PC's faster
// kind of core, GcPauseMs and JitMs the process's garbage-collection pauses
// and the thread's compilation time during the action. Waits marks a row that
// waits by design (a frame, a load off the thread), so its time not running
// is not all load.
public sealed record MeasuredRow(
    string Library,
    string Id,
    string Action,
    double WallMs,
    double UiMs,
    double BudgetMs,
    string BudgetName,
    bool Informational = false,
    string Note = "",
    double? RunMs = null,
    double? NotRunMs = null,
    double? RunUiMs = null,
    double? PerfCoreShare = null,
    double? GcPauseMs = null,
    double? JitMs = null,
    bool Waits = false)
{
    public bool Within(double multiplier) => UiMs <= BudgetMs * multiplier;

    // The Wall the busy-PC figures were taken against: RunMs + NotRunMs when
    // both are present, else WallMs. The two differ only for the scroll row,
    // whose figures come from its longest-UI step while its WallMs is the
    // longest Wall of any step.
    public double OwnWallMs => RunMs is { } ran && NotRunMs is { } notRun ? ran + notRun : WallMs;
}

// One rate check of the Windows thread clock (busy-PC design, section 4): the
// closest spin's cycles per ms as a share of the nominal rate (null when no
// spin could be read), the spins it took, and whether a spin came within 2%.
public sealed record RateCheck(double? Share, int Spins, bool Passed)
{
    public override string ToString() => Share is { } share
        ? string.Create(CultureInfo.InvariantCulture, $"{share:0.0000} of nominal after {Spins} spins ({(Passed ? "passed" : "failed")})")
        : string.Create(CultureInfo.InvariantCulture, $"no spin read after {Spins} spins (failed)");
}

// What the PC was doing during one run (busy-PC design, section 2), all
// report-only. Each figure is null when it could not be read; an absent
// figure never fails a test or voids a run.
public sealed record MeasurementConditions
{
    // "cycles", "thread-cputime", or "absent: <reason>" from a fixed list.
    public required string ThreadClock { get; init; }

    // Windows only: the nominal rate the cycles were divided by.
    public double? CyclesPerMs { get; init; }

    // Windows only: the rate checks before the first row and after the last.
    public RateCheck? RateCheckBefore { get; init; }

    public RateCheck? RateCheckAfter { get; init; }

    // How many of the PC's processors were busy on average, all programs.
    public double? MachineBusyProcessors { get; init; }

    // How long each of the two machine-load readings took.
    public double? LoadReadBeforeMs { get; init; }

    public double? LoadReadAfterMs { get; init; }

    // Linux only: processors' worth of time the host gave to other guests.
    public double? StealProcessors { get; init; }

    // MachineBusyProcessors less this process's own processor time.
    public double? OthersBusyProcessors { get; init; }

    // Time not running over Wall, summed over the rows that do not wait.
    public double? NotRunShare { get; init; }

    // Other programs kept more processors busy on average than the
    // disturbed line; null when the load could not be read, which counts as
    // not disturbed.
    public bool? Disturbed { get; init; }

    // The disturbed line (busy-PC design, section 2): other programs keep
    // more than 3 of the 12 processors busy, as the maintainer decided on
    // November 27, 2026, after the validation, for later sittings only. It is
    // written as a quarter of the processors, so it reads 1 of 4 on a CI
    // runner; that scaling is assumed, not part of the decision (section 11).
    // Until then a run was disturbed when other programs left fewer than two
    // processors free.
    public static double DisturbedLine(int processors) => processors / 4.0;
}

// The sides of a with-and-without comparison of preparing Published while the
// Board is idle (idle-preparation design, section 8; busy-PC design, section
// 9). A run told a side ("--after-idle" in a real window, the
// PAPERROUTE_TRIAL_AFTER_IDLE variable headless) drives the preparation
// itself, then waits alike on every side (a fixed idle period, then one
// frame) and times the first switch as first-switch-published-after-idle. A
// run told no side is today's run: its first switch is first-switch-published.
//
// A first switch happens once per window, so each of the design's rows that
// needs one of its own has its own scenario, and each scenario two sides (or
// more) that write the same rows: one without preparation, one with it. Only
// two sides of one scenario are compared in a sitting. The scenarios:
// - switch (none, width, half, prepared, mid-step): the first switch after
//   the side's steps (rows 1 to 4); mid-step asks for the switch as the last
//   step starts, so the step runs first (row 11).
// - key (key-none, key-step): a search keystroke that meets the last step
//   (row 6, its first half).
// - timer (timer-none, timer-on): the Board's own idle timer prepares
//   Published, timed from the first frame (row 5); then a key typed after a
//   0.5 s pause in a search, and the timer's refresh (row 6, its second half).
// - search (search-pending, search-refreshed): a search typed, cleared and
//   typed again while Published is prepared and hidden, with or without the
//   idle refresh after each, then the first switch (rows 8 and 10).
// - maximise (maximise-none, maximise-prepared): the first switch within
//   0.4 s of a maximise (row 12).
// - scaling (scaling-none, scaling-prepared): the first switch after a
//   change of the display scaling (row 12); headless only, as a real window
//   cannot change its own scaling.
// - theme (theme-none, theme-prepared): managed memory before and after
//   preparation, then a theme change and its refresh (row 9).
public static class IdleSides
{
    // No preparation: the wait only.
    public const string None = "none";

    // The width step W only.
    public const string Width = "width";

    // W and the first two layout steps of the ladder: half its L steps.
    public const string Half = "half";

    // Every step, until the Board says Published is prepared.
    public const string Prepared = "prepared";

    // Every step but the last (W and the three capped layout steps); the
    // last, full-size step runs inside the first switch's row, as when the
    // switch is asked for while that step runs.
    public const string MidStep = "mid-step";

    // The key scenario: no preparation, or every step but the last, which
    // runs inside the keystroke's row.
    public const string KeyNone = "key-none";
    public const string KeyStep = "key-step";

    // The timer scenario: the timer off, or on until Published is prepared.
    public const string TimerNone = "timer-none";
    public const string TimerOn = "timer-on";

    // The search scenario: both prepare; the idle refresh runs only on
    // search-refreshed.
    public const string SearchPending = "search-pending";
    public const string SearchRefreshed = "search-refreshed";

    public const string MaximiseNone = "maximise-none";
    public const string MaximisePrepared = "maximise-prepared";

    public const string ScalingNone = "scaling-none";
    public const string ScalingPrepared = "scaling-prepared";

    public const string ThemeNone = "theme-none";
    public const string ThemePrepared = "theme-prepared";

    // The scenarios (ScenarioOf).
    public const string SwitchScenario = "switch";
    public const string KeyScenario = "key";
    public const string TimerScenario = "timer";
    public const string SearchScenario = "search";
    public const string MaximiseScenario = "maximise";
    public const string ScalingScenario = "scaling";
    public const string ThemeScenario = "theme";

    // The number of steps before the last, full-size one on the sides that
    // stop there (mid-step, key-step): W and the ladder's three capped steps
    // (HiddenShelfPreparer.LadderSteps, which a test holds equal).
    public const int StepsBeforeTheLast = 4;

    public static IReadOnlyList<string> All { get; } =
    [
        None, Width, Half, Prepared, MidStep,
        KeyNone, KeyStep,
        TimerNone, TimerOn,
        SearchPending, SearchRefreshed,
        MaximiseNone, MaximisePrepared,
        ScalingNone, ScalingPrepared,
        ThemeNone, ThemePrepared,
    ];

    public static bool IsKnown(string? side) => side is not null && All.Contains(side, StringComparer.Ordinal);

    // How many steps the side runs before the first switch's wait: null for
    // every step due (by the timer on timer-on).
    public static int? StepsFor(string side) => side switch
    {
        None or KeyNone or TimerNone or MaximiseNone or ScalingNone or ThemeNone => 0,
        Width => 1,
        Half => 3,
        MidStep or KeyStep => StepsBeforeTheLast,
        Prepared or TimerOn or SearchPending or SearchRefreshed or MaximisePrepared or ScalingPrepared or ThemePrepared => null,
        _ => throw new ArgumentException($"No idle side is called {side}.", nameof(side)),
    };

    // The scenario a side belongs to; two sides are compared only when they
    // belong to the same one.
    public static string ScenarioOf(string side) => side switch
    {
        None or Width or Half or Prepared or MidStep => SwitchScenario,
        KeyNone or KeyStep => KeyScenario,
        TimerNone or TimerOn => TimerScenario,
        SearchPending or SearchRefreshed => SearchScenario,
        MaximiseNone or MaximisePrepared => MaximiseScenario,
        ScalingNone or ScalingPrepared => ScalingScenario,
        ThemeNone or ThemePrepared => ThemeScenario,
        _ => throw new ArgumentException($"No idle side is called {side}.", nameof(side)),
    };

    // Whether the side prepares Published at all.
    public static bool Prepares(string side) => StepsFor(side) != 0;

    // Whether the Board's own idle timer prepares Published on this side
    // (the measurement drives every other side's steps itself).
    public static bool ByTimer(string side) => side == TimerOn;

    // Whether only a headless run can make the side's change: a real window
    // cannot change its own display scaling.
    public static bool HeadlessOnly(string side) => ScenarioOf(side) == ScalingScenario;
}

// What idle preparation did before the first switch of one library, in a run
// told a side: the steps run and the card views they made, whether Published
// was prepared at the switch, why preparation stopped (null when it did not),
// and whether the first switch after idle was timed, with the card views it
// made. When preparation stopped, the switch is made untimed: the row is not
// taken (idle-preparation design, section 8, row 1), so ViewsMadeAtSwitch is
// null. Then what the wait before the switch did, which is the same on every
// side of a scenario: the idle period it waited, in ms, and the frames drawn
// from the start of the idle period to the switch (1, or 2 where a maximise
// or a scaling change and its frame come after the idle period).
//
// The rest are the counts the other scenarios add (IdleSides), each null or
// empty where the scenario has none: SessionSteps, every step the session ran
// up to and with the first switch (refreshes and a step met inside a row
// included); StepsMet and StepsMetViews, the steps run inside the row a key or
// a click meets (mid-step: the first switch; key-step: the keystroke; timer:
// the pause and the key after it, expected none) and the card views they made,
// counted inside that row's stopwatch (but on timer); ViewsMadeInRow, every
// card view made inside the row a key or a click meets (mid-step and key), so
// on mid-step ViewsMadeAtSwitch is the switch's own, the row's less the
// step's; StoppedInsideRow, whether SkippedReason was set by the step met
// inside that row, which ran there and made its views (mid-step, key-step):
// the row and the first switch after idle are then kept and timed;
// RefreshSteps, the idle refresh steps (search, timer, theme); RowsStaleAtSwitch,
// whether Published's rows were stale as the switch began (the switch then
// refills them); ManagedBytesBefore and ManagedBytesAfter, the managed heap
// after a full collection before and after preparation (theme); StepFrameMs,
// the frame drawn after each step the measurement drove, in ms (row 13); and
// ChangeToSwitchMs, from the start of a maximise or a scaling change to the
// start of the switch.
public sealed record IdlePreparationRecord(
    string Library,
    string Side,
    int StepsRun,
    int ViewsMade,
    bool Prepared,
    string? SkippedReason,
    bool FirstSwitchTaken,
    int? ViewsMadeAtSwitch,
    double IdleWaitMs,
    int FramesBeforeSwitch)
{
    public int SessionSteps { get; init; }

    public int? StepsMet { get; init; }

    public int? StepsMetViews { get; init; }

    public int? ViewsMadeInRow { get; init; }

    public bool StoppedInsideRow { get; init; }

    public int? RefreshSteps { get; init; }

    public bool RowsStaleAtSwitch { get; init; }

    public long? ManagedBytesBefore { get; init; }

    public long? ManagedBytesAfter { get; init; }

    public IReadOnlyList<double> StepFrameMs { get; init; } = [];

    public double? ChangeToSwitchMs { get; init; }
}

// The numbers of one measurement run, headless (the budget tests) or in a
// real window ("--measure", d1 7.3), and how they are written as JSON: per
// row the library, the action, Wall, UI, the budget, the verdict and the
// headless test's rule for it; for the run the machine, the DPI, the option
// (b: rows of N, the only layout since the every-card one, a, was removed),
// the SyncInto threshold, the day the session used and the build. The busy-PC
// figures, per row and in "conditions" for the run, are report-only: the
// verdicts, rules and Failures() do not read them.
public sealed class BoardMeasurementReport
{
    // What a row's "rule" says in the JSON, as the headless budget test holds it.
    public const string EnforcedRule = "enforced";
    public const string WaitingUnderRowsRule = "waiting: over under rows of N too";
    public const string ReportOnlyRule = "report-only";

    public required string Mode { get; init; }

    public required string MachineName { get; init; }

    public required double Dpi { get; init; }

    public required string Option { get; init; }

    public required int ResetThreshold { get; init; }

    public required DateTime Day { get; init; }

    // Where the day came from: "the computer's date" or "--today".
    public required string DaySource { get; init; }

    public required string Build { get; init; }

    // Every budget is multiplied by this before a verdict: 2 for a Debug
    // build, 3 more on GitHub Actions (ResponsivenessBudgetTests.vb:40-43, 901-910).
    public required double Multiplier { get; init; }

    public required DateTime TakenAt { get; init; }

    public int Processors { get; init; } = Environment.ProcessorCount;

    public string OperatingSystem { get; init; } = Environment.OSVersion.VersionString;

    // The rows of the 500 library the headless test measures but does not yet
    // hold: the rows it does not meet under rows of N either.
    public IReadOnlyList<string> WaitingForRowsOfN { get; init; } = [];

    // The library rows are held on ("500" headless); every other library's
    // rows are reported only, as the example carries first-time costs.
    public string HeldLibrary { get; init; } = string.Empty;

    // Set when the run stopped early; the rows measured so far are kept.
    public string? Error { get; init; }

    public IReadOnlyList<MeasuredRow> Rows { get; init; } = [];

    // What the PC was doing during the run; null when it was not recorded
    // (the JSON then says null).
    public MeasurementConditions? Conditions { get; init; }

    // What idle preparation did, one record per library, in a run told a side
    // (IdleSides); empty in a run told no side, whose JSON then has no
    // "idlePreparation".
    public IReadOnlyList<IdlePreparationRecord> IdlePreparation { get; init; } = [];

    // The last line of every timing failure: always there, so the message
    // also reads correctly on a quiet PC (busy-PC design, section 8).
    public const string QuietPcLine = "Timing tests need a quiet PC. Close other programs and run the test again before reading this as a slower Board.";

    public const string RunUnreadableLine = "  How long the interface thread ran could not be read on this system.";

    public const string LoadUnreadableLine = "How busy the PC was could not be read on this system.";

    // Run UI failed the validation of November 27, 2026 (busy-PC design,
    // section 6), so the figure stays, report-only, under this label: in the
    // JSON ("runUiMsStatus") and wherever a person reads it.
    public const string RunUiStatus = "not validated";

    public const string RunUiHeading = "Run UI ms (" + RunUiStatus + ")";

    public string RuleFor(MeasuredRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Informational || !string.Equals(row.Library, HeldLibrary, StringComparison.Ordinal))
        {
            return ReportOnlyRule;
        }

        return WaitingForRowsOfN.Contains(row.Id, StringComparer.Ordinal) ? WaitingUnderRowsRule : EnforcedRule;
    }

    public string VerdictFor(MeasuredRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Within(Multiplier) ? "within" : "over";
    }

    // The rows the headless test fails on: enforced and over their budget.
    public IReadOnlyList<MeasuredRow> Failures() =>
        Rows.Where(row => RuleFor(row) == EnforcedRule && !row.Within(Multiplier)).ToList();

    // The headless test's failure message (busy-PC design, section 8): for
    // each failed row today's line and what its thread did, then how busy the
    // PC was and the quiet-PC line, once. Empty when nothing failed. It states
    // what was measured in this run and never names a cause.
    public string FailureMessage()
    {
        IReadOnlyList<MeasuredRow> failures = Failures();
        if (failures.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (MeasuredRow row in failures)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{row.Library}: {row.Action} took {row.UiMs:0} ms against {row.BudgetMs * Multiplier:0} ms."));
            lines.Add(RunLine(row));
        }

        lines.Add(Conditions?.OthersBusyProcessors is { } others
            ? string.Create(CultureInfo.InvariantCulture, $"During this run other programs kept {others:0.0} of {Processors} processors busy on average.")
            : LoadUnreadableLine);
        lines.Add(QuietPcLine);
        return string.Join(Environment.NewLine, lines);
    }

    // The second line of a failure: how long the interface thread ran, of the
    // Wall its figures were taken against (OwnWallMs, so the scroll row's line
    // speaks of one step). The line names that Wall: the first line gives UI,
    // which is shorter than the Wall on some rows, so "ran for 284 ms of it"
    // after "took 259 ms" read as more than the whole.
    private static string RunLine(MeasuredRow row)
    {
        if (row.RunMs is not { } ran || row.NotRunMs is not { } notRun)
        {
            return RunUnreadableLine;
        }

        double wall = row.OwnWallMs;
        if (wall > 0 && ran >= 0.9 * wall)
        {
            return string.Create(CultureInfo.InvariantCulture, $"  Of the {wall:0} ms from its start to its end, the interface thread was running for {ran:0} ms. A busy PC can still slow running code.");
        }

        string line = string.Create(CultureInfo.InvariantCulture,
            $"  Of the {wall:0} ms from its start to its end, the interface thread ran for {ran:0} ms and was not running for {notRun:0} ms ({(wall > 0 ? notRun / wall : 0):0%})");
        return row.GcPauseMs is { } gc && row.JitMs is { } jit
            ? line + string.Create(CultureInfo.InvariantCulture, $"; garbage collection paused {gc:0} ms, compilation {jit:0} ms.")
            : line + ".";
    }

    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("mode", Mode);
            json.WriteString("machineName", MachineName);
            json.WriteNumber("dpi", Dpi);
            json.WriteString("option", Option);
            json.WriteNumber("syncIntoResetThreshold", ResetThreshold);
            json.WriteString("day", Day.ToString(TrialOptions.TodayFormat, CultureInfo.InvariantCulture));
            json.WriteString("daySource", DaySource);
            json.WriteString("build", Build);
            json.WriteNumber("budgetMultiplier", Multiplier);
            json.WriteString("takenAt", TakenAt.ToString("o", CultureInfo.InvariantCulture));
            json.WriteNumber("processors", Processors);
            json.WriteString("operatingSystem", OperatingSystem);
            json.WriteString("heldLibrary", HeldLibrary);
            json.WriteStartArray("waitingForRowsOfN");
            foreach (string id in WaitingForRowsOfN)
            {
                json.WriteStringValue(id);
            }

            json.WriteEndArray();
            if (Error is not null)
            {
                json.WriteString("error", Error);
            }

            // Every row's runUiMs is read under this label.
            json.WriteString("runUiMsStatus", RunUiStatus);
            json.WriteStartArray("rows");
            foreach (MeasuredRow row in Rows)
            {
                json.WriteStartObject();
                json.WriteString("library", row.Library);
                json.WriteString("id", row.Id);
                json.WriteString("action", row.Action);
                json.WriteNumber("wallMs", Math.Round(row.WallMs, 1));
                json.WriteNumber("uiMs", Math.Round(row.UiMs, 1));
                json.WriteNumber("budgetMs", row.BudgetMs);
                json.WriteString("budgetName", row.BudgetName);
                json.WriteString("verdict", VerdictFor(row));
                json.WriteString("rule", RuleFor(row));
                if (!string.IsNullOrEmpty(row.Note))
                {
                    json.WriteString("note", row.Note);
                }

                // The busy-PC figures, report-only: null when absent, never 0.
                WriteNumberOrNull(json, "runMs", row.RunMs, 1);
                WriteNumberOrNull(json, "notRunMs", row.NotRunMs, 1);
                WriteNumberOrNull(json, "runUiMs", row.RunUiMs, 1);
                WriteNumberOrNull(json, "perfCoreShare", row.PerfCoreShare, 3);
                WriteNumberOrNull(json, "gcPauseMs", row.GcPauseMs, 1);
                WriteNumberOrNull(json, "jitMs", row.JitMs, 1);
                if (row.Waits)
                {
                    json.WriteBoolean("waits", true);
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
            WriteConditions(json, Conditions);
            WriteIdlePreparation(json, IdlePreparation);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteConditions(Utf8JsonWriter json, MeasurementConditions? conditions)
    {
        if (conditions is null)
        {
            json.WriteNull("conditions");
            return;
        }

        json.WriteStartObject("conditions");
        json.WriteString("threadClock", conditions.ThreadClock);
        WriteNumberOrNull(json, "cyclesPerMs", conditions.CyclesPerMs, 0);
        if (conditions.RateCheckBefore is null && conditions.RateCheckAfter is null)
        {
            json.WriteNull("rateCheck");
        }
        else
        {
            json.WriteStartArray("rateCheck");
            foreach (RateCheck? check in new[] { conditions.RateCheckBefore, conditions.RateCheckAfter })
            {
                if (check is null)
                {
                    json.WriteNullValue();
                    continue;
                }

                json.WriteStartObject();
                WriteNumberOrNull(json, "share", check.Share, 4);
                json.WriteNumber("spins", check.Spins);
                json.WriteBoolean("passed", check.Passed);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        WriteNumberOrNull(json, "machineBusyProcessors", conditions.MachineBusyProcessors, 2);
        json.WriteStartArray("loadReadMs");
        foreach (double? read in new[] { conditions.LoadReadBeforeMs, conditions.LoadReadAfterMs })
        {
            if (read is { } ms)
            {
                json.WriteNumberValue(Math.Round(ms, 1));
            }
            else
            {
                json.WriteNullValue();
            }
        }

        json.WriteEndArray();
        WriteNumberOrNull(json, "stealProcessors", conditions.StealProcessors, 2);
        WriteNumberOrNull(json, "othersBusyProcessors", conditions.OthersBusyProcessors, 2);
        WriteNumberOrNull(json, "notRunShare", conditions.NotRunShare, 3);
        if (conditions.Disturbed is { } disturbed)
        {
            json.WriteBoolean("disturbed", disturbed);
        }
        else
        {
            json.WriteNull("disturbed");
        }

        json.WriteEndObject();
    }

    // Written only in a run told a side, so a run told no side writes the JSON
    // it wrote before.
    private static void WriteIdlePreparation(Utf8JsonWriter json, IReadOnlyList<IdlePreparationRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        json.WriteStartArray("idlePreparation");
        foreach (IdlePreparationRecord record in records)
        {
            json.WriteStartObject();
            json.WriteString("library", record.Library);
            json.WriteString("side", record.Side);
            json.WriteNumber("stepsRun", record.StepsRun);
            json.WriteNumber("viewsMade", record.ViewsMade);
            json.WriteBoolean("prepared", record.Prepared);
            if (record.SkippedReason is { } reason)
            {
                json.WriteString("skippedReason", reason);
            }
            else
            {
                json.WriteNull("skippedReason");
            }

            json.WriteBoolean("firstSwitchTaken", record.FirstSwitchTaken);
            if (record.ViewsMadeAtSwitch is { } made)
            {
                json.WriteNumber("viewsMadeAtSwitch", made);
            }
            else
            {
                json.WriteNull("viewsMadeAtSwitch");
            }

            json.WriteNumber("idleWaitMs", Math.Round(record.IdleWaitMs, 1));
            json.WriteNumber("framesBeforeSwitch", record.FramesBeforeSwitch);

            // The counts of the other scenarios: null where the scenario has none.
            json.WriteString("scenario", IdleSides.IsKnown(record.Side) ? IdleSides.ScenarioOf(record.Side) : null);
            json.WriteNumber("sessionSteps", record.SessionSteps);
            WriteNumberOrNull(json, "stepsMet", record.StepsMet, 0);
            WriteNumberOrNull(json, "stepsMetViews", record.StepsMetViews, 0);
            WriteNumberOrNull(json, "viewsMadeInRow", record.ViewsMadeInRow, 0);
            json.WriteBoolean("stoppedInsideRow", record.StoppedInsideRow);
            WriteNumberOrNull(json, "refreshSteps", record.RefreshSteps, 0);
            json.WriteBoolean("rowsStaleAtSwitch", record.RowsStaleAtSwitch);
            WriteNumberOrNull(json, "managedBytesBefore", record.ManagedBytesBefore, 0);
            WriteNumberOrNull(json, "managedBytesAfter", record.ManagedBytesAfter, 0);
            json.WriteStartArray("stepFrameMs");
            foreach (double frame in record.StepFrameMs)
            {
                json.WriteNumberValue(Math.Round(frame, 1));
            }

            json.WriteEndArray();
            WriteNumberOrNull(json, "changeToSwitchMs", record.ChangeToSwitchMs, 1);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void WriteNumberOrNull(Utf8JsonWriter json, string name, double? value, int digits)
    {
        if (value is { } number && double.IsFinite(number))
        {
            json.WriteNumber(name, Math.Round(number, digits));
        }
        else
        {
            json.WriteNull(name);
        }
    }

    // Writes the JSON to path, which RefusalFor has accepted.
    public void WriteJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // The table the tests print: one line per row.
    public string ToTable()
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Board trial budgets ({Mode}, option {Option}, SyncInto threshold {ResetThreshold}). Verdict: UI against the budget x {Multiplier:0.#}. " +
            $"{Build} build, {Processors} processors, DPI {Dpi:0}, day {Day:yyyy-MM-dd} ({DaySource}), {MachineName}.");
        if (Conditions is not null)
        {
            text.AppendLine(DescribeConditions(Conditions, Processors));
        }

        foreach (IdlePreparationRecord idle in IdlePreparation)
        {
            text.AppendLine(DescribeIdlePreparation(idle));
        }

        text.AppendLine($"| Library | Id | Action | Wall ms | UI ms | {RunUiHeading} | Budget ms | Verdict | Rule |");
        text.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|");
        foreach (MeasuredRow row in Rows)
        {
            string runUi = row.RunUiMs is { } ms ? ms.ToString("0", CultureInfo.InvariantCulture) : "absent";
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {row.Library} | {row.Id} | {row.Action}{(string.IsNullOrEmpty(row.Note) ? string.Empty : " (" + row.Note + ")")} | {row.WallMs:0} | {row.UiMs:0} | {runUi} | {row.BudgetMs:0} ({row.BudgetName}) | {VerdictFor(row)} | {RuleFor(row)} |");
        }

        return text.ToString();
    }

    // One line on what the PC was doing, report-only, above the table.
    public static string DescribeConditions(MeasurementConditions conditions, int processors)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Report-only: thread clock {conditions.ThreadClock}. ");
        text.Append(conditions.OthersBusyProcessors is { } others && conditions.MachineBusyProcessors is { } machine
            ? string.Create(CultureInfo.InvariantCulture, $"Other programs kept {others:0.0} of {processors} processors busy on average ({machine:0.0} with this one). ")
            : LoadUnreadableLine + " ");
        if (conditions.NotRunShare is { } share)
        {
            text.Append(CultureInfo.InvariantCulture, $"The interface thread was not running for {share:0%} of the Wall of the rows that do not wait. ");
        }

        text.Append(conditions.Disturbed switch
        {
            true => string.Create(CultureInfo.InvariantCulture, $"Disturbed: other programs kept more than {MeasurementConditions.DisturbedLine(processors):0.##} of {processors} processors busy."),
            false => "Not disturbed.",
            null => "Load unknown.",
        });
        return text.ToString();
    }

    // One line on what idle preparation did before one library's first switch.
    public static string DescribeIdlePreparation(IdlePreparationRecord idle)
    {
        ArgumentNullException.ThrowIfNull(idle);
        string switched = idle.FirstSwitchTaken
            ? string.Create(CultureInfo.InvariantCulture, $"the first switch after idle made {idle.ViewsMadeAtSwitch} card views")
            : "the first switch after idle was made but not timed";
        string line = string.Create(CultureInfo.InvariantCulture,
            $"Idle side {idle.Side} ({idle.Library}): {idle.StepsRun} preparation steps made {idle.ViewsMade} card views; Published {(idle.Prepared ? "prepared" : "not prepared")} at the switch; {(idle.SkippedReason is { } reason ? "preparation stopped: " + reason + (idle.StoppedInsideRow ? " by the step met inside the row a key or a click meets, which ran there and is kept" : string.Empty) : "preparation not stopped")}; {switched}.");
        var more = new List<string>();
        if (idle.StepsMet is { } met)
        {
            string inRow = idle.ViewsMadeInRow is { } views ? string.Create(CultureInfo.InvariantCulture, $"; {views} made in that row in all") : string.Empty;
            more.Add(string.Create(CultureInfo.InvariantCulture, $"{met} steps met by the key or the click ({idle.StepsMetViews ?? 0} card views{inRow})"));
        }

        if (idle.RefreshSteps is { } refresh)
        {
            more.Add(string.Create(CultureInfo.InvariantCulture, $"{refresh} idle refresh steps"));
        }

        if (idle.ManagedBytesBefore is { } before && idle.ManagedBytesAfter is { } after)
        {
            more.Add(string.Create(CultureInfo.InvariantCulture, $"managed memory {before / 1024.0 / 1024.0:0.0} MB before preparation, {after / 1024.0 / 1024.0:0.0} MB after"));
        }

        if (idle.ChangeToSwitchMs is { } change)
        {
            more.Add(string.Create(CultureInfo.InvariantCulture, $"{change:0} ms from the change to the switch"));
        }

        if (more.Count > 0 || idle.SessionSteps != idle.StepsRun)
        {
            more.Insert(0, string.Create(CultureInfo.InvariantCulture, $"{idle.SessionSteps} steps in the session; Published's rows {(idle.RowsStaleAtSwitch ? "stale" : "current")} at the switch"));
        }

        return more.Count == 0 ? line : line + " " + string.Join("; ", more) + ".";
    }

    public const string LocalFileMessage = "\"--measure\" needs a local file path such as C:\\Measurements\\board.json.";

    // Why the measurement may not write its JSON at path, in plain words, or
    // null when it may: the path must be a plain local path, end in .json, and
    // lie in a folder that exists and is not, or does not lie inside, one of
    // the real profile's storage folders (ReadOnlySnapshot.DefaultStorageRoots),
    // with links and short names resolved as the snapshot resolves them.
    // Nothing is written. alsoRefuse adds folders (tests name a fake profile).
    public static string? RefusalFor(string path, IEnumerable<string>? alsoRefuse = null) =>
        RefusalFor(path, TrialOptions.MeasureArgument, "measurement", alsoRefuse);

    // The same rule for another mode that writes a JSON file
    // ("--platform-check"): argument names the mode, what the file holds.
    // For "--measure" the messages are the ones above, word for word.
    public static string? RefusalFor(string path, string argument, string what, IEnumerable<string>? alsoRefuse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        bool measure = string.Equals(argument, TrialOptions.MeasureArgument, StringComparison.Ordinal);
        string example = measure ? @"C:\Measurements\board.json"
            : System.OperatingSystem.IsWindows() ? @"C:\Checks\platform-check.json" : "/tmp/platform-check.json";
        string localFile = measure ? LocalFileMessage : $"\"{argument}\" needs a local file path such as {example}.";
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return $"\"{argument}\" needs the full path of the JSON file to write, such as {example}.";
        }

        string full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return $"The {what} is written as JSON: {full} does not end in .json.";
        }

        if (!ReadOnlySnapshot.IsPlainLocalPath(full))
        {
            return localFile;
        }

        string folder = Path.GetDirectoryName(full) ?? full;
        string[] refused = [.. ReadOnlySnapshot.DefaultStorageRoots(), .. (alsoRefuse ?? []).Select(item => Path.GetFullPath(item))];
        string? root = ReadOnlySnapshot.RefusedRootFor(full, refused, out string real);
        if (root is null && !ReadOnlySnapshot.IsPlainLocalPath(real))
        {
            return localFile;
        }

        if (root is not null)
        {
            return $"The Board trial writes nothing into your own PaperRoute library. The file {full} lies inside " +
                $"PaperRoute's storage folder {root}. Choose another folder.";
        }

        if (!Directory.Exists(folder))
        {
            return $"The Board trial found no folder at {folder} to write the {what} into.";
        }

        return null;
    }
}
