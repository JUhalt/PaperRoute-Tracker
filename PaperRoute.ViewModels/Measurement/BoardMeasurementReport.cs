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
public sealed record MeasuredRow(
    string Library,
    string Id,
    string Action,
    double WallMs,
    double UiMs,
    double BudgetMs,
    string BudgetName,
    bool Informational = false,
    string Note = "")
{
    public bool Within(double multiplier) => UiMs <= BudgetMs * multiplier;
}

// The numbers of one measurement run, headless (the budget tests) or in a
// real window ("--measure", d1 7.3), and how they are written as JSON: per
// row the library, the action, Wall, UI, the budget, the verdict and the
// headless test's rule for it; for the run the machine, the DPI, the option
// (a: every card of a shown shelf made at once; b: rows of N), the
// SyncInto threshold, the day the session used and the build.
public sealed class BoardMeasurementReport
{
    // What a row's "rule" says in the JSON, as the headless budget test holds it.
    public const string EnforcedRule = "enforced";
    public const string WaitingRule = "waiting for rows of N";
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
    // hold under the option built: under (a) until rows of N (d1 4.4 option
    // (b)), under (b) the rows it does not meet either.
    public IReadOnlyList<string> WaitingForRowsOfN { get; init; } = [];

    // The library rows are held on ("500" headless); every other library's
    // rows are reported only, as the example carries first-time costs.
    public string HeldLibrary { get; init; } = string.Empty;

    // Set when the run stopped early; the rows measured so far are kept.
    public string? Error { get; init; }

    public IReadOnlyList<MeasuredRow> Rows { get; init; } = [];

    public string RuleFor(MeasuredRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Informational || !string.Equals(row.Library, HeldLibrary, StringComparison.Ordinal))
        {
            return ReportOnlyRule;
        }

        if (!WaitingForRowsOfN.Contains(row.Id, StringComparer.Ordinal))
        {
            return EnforcedRule;
        }

        return string.Equals(Option, "b", StringComparison.Ordinal) ? WaitingUnderRowsRule : WaitingRule;
    }

    public string VerdictFor(MeasuredRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Within(Multiplier) ? "within" : "over";
    }

    // The rows the headless test fails on: enforced and over their budget.
    public IReadOnlyList<MeasuredRow> Failures() =>
        Rows.Where(row => RuleFor(row) == EnforcedRule && !row.Within(Multiplier)).ToList();

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

                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
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
        text.AppendLine("| Library | Id | Action | Wall ms | UI ms | Budget ms | Verdict | Rule |");
        text.AppendLine("|---|---|---|---:|---:|---:|---|---|");
        foreach (MeasuredRow row in Rows)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {row.Library} | {row.Id} | {row.Action}{(string.IsNullOrEmpty(row.Note) ? string.Empty : " (" + row.Note + ")")} | {row.WallMs:0} | {row.UiMs:0} | {row.BudgetMs:0} ({row.BudgetName}) | {VerdictFor(row)} | {RuleFor(row)} |");
        }

        return text.ToString();
    }

    public const string LocalFileMessage = "\"--measure\" needs a local file path such as C:\\Measurements\\board.json.";

    // Why the measurement may not write its JSON at path, in plain words, or
    // null when it may: the path must be a plain local path, end in .json, and
    // lie in a folder that exists and is not, or does not lie inside, one of
    // the real profile's storage folders (ReadOnlySnapshot.DefaultStorageRoots),
    // with links and short names resolved as the snapshot resolves them.
    // Nothing is written. alsoRefuse adds folders (tests name a fake profile).
    public static string? RefusalFor(string path, IEnumerable<string>? alsoRefuse = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return "\"--measure\" needs the full path of the JSON file to write, such as C:\\Measurements\\board.json.";
        }

        string full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return $"The measurement is written as JSON: {full} does not end in .json.";
        }

        if (!ReadOnlySnapshot.IsPlainLocalPath(full))
        {
            return LocalFileMessage;
        }

        string folder = Path.GetDirectoryName(full) ?? full;
        string[] refused = [.. ReadOnlySnapshot.DefaultStorageRoots(), .. (alsoRefuse ?? []).Select(item => Path.GetFullPath(item))];
        string? root = ReadOnlySnapshot.RefusedRootFor(full, refused, out string real);
        if (root is null && !ReadOnlySnapshot.IsPlainLocalPath(real))
        {
            return LocalFileMessage;
        }

        if (root is not null)
        {
            return $"The Board trial writes nothing into your own PaperRoute library. The file {full} lies inside " +
                $"PaperRoute's storage folder {root}. Choose another folder.";
        }

        if (!Directory.Exists(folder))
        {
            return $"The Board trial found no folder at {folder} to write the measurement into.";
        }

        return null;
    }
}
