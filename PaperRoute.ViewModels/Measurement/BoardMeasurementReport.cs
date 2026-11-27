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
    // more than a quarter of the processors busy, 3 of 12 on the development
    // PC and 1 of 4 on a CI runner. The maintainer re-set it on November 27,
    // 2026, after the validation, for later sittings only. Until then a run
    // was disturbed when other programs left fewer than two processors free.
    public static double DisturbedLine(int processors) => processors / 4.0;
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
