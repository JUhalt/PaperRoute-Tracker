using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PaperRoute.ViewModels.Session;

// The trial's command line (d1 1.3 Modes), as far as this step builds it:
//   (none)                     the example
//   --snapshot-from <folder>   a read-only snapshot of the library in <folder>
//   --today yyyy-MM-dd         the session's day; only with --snapshot-from
//   --self-check               no window: load, check, exit 0 or 1
//   --measure <file>.json      a real window: time the rows of d1 6.3 on the
//                              library, write them as JSON, exit (d1 7.3)
//   --platform-check <file>.json  a real window on the example: try the file
//                              dialog, the launcher and a restart once each,
//                              write the outcomes as JSON, exit (step 15)
// "--snapshot" (the real profile, d1 step 14) is not built, and says so.
// "--measure" and "--self-check" do not go together: one opens a window, the
// other none. "--platform-check" goes with none of "--self-check", "--measure"
// and "--snapshot-from". Whether the file named may be written is checked after parsing
// (BoardMeasurementReport.RefusalFor for "--measure", PlatformCheckReport.RefusalFor
// for "--platform-check"), before any session starts.
// Arguments the trial does not know are left to Avalonia, as before.
// Parsing reads no file and calls nothing in the core.
public sealed class TrialOptions
{
    public const string SnapshotFromArgument = "--snapshot-from";
    public const string TodayArgument = "--today";
    public const string SnapshotArgument = "--snapshot";
    public const string MeasureArgument = "--measure";
    public const string PlatformCheckArgument = "--platform-check";
    public const string TodayFormat = "yyyy-MM-dd";

    private TrialOptions()
    {
    }

    // The folder named after --snapshot-from, or null for the example.
    public string? SnapshotFrom { get; private set; }

    // The day after --today, or null for the computer's date.
    public DateTime? Today { get; private set; }

    public bool SelfCheck { get; private set; }

    // The JSON file named after --measure, or null when not measuring.
    public string? MeasureFile { get; private set; }

    // The JSON file named after --platform-check, or null.
    public string? PlatformCheckFile { get; private set; }

    // True with the options, or false with a plain message.
    public static bool TryParse(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out TrialOptions? options,
        [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = new TrialOptions();
        options = null;
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index] ?? string.Empty;
            if (Is(argument, TrialStartup.SelfCheckArgument))
            {
                parsed.SelfCheck = true;
            }
            else if (Is(argument, SnapshotArgument))
            {
                error = "\"--snapshot\" (a copy of your own PaperRoute library) is not part of this trial build. " +
                    "Use \"--snapshot-from <folder>\" with a library folder made for testing.";
                return false;
            }
            else if (Is(argument, SnapshotFromArgument))
            {
                if (parsed.SnapshotFrom is not null)
                {
                    error = "\"--snapshot-from\" is given twice.";
                    return false;
                }

                if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = "\"--snapshot-from\" needs the folder of a library after it.";
                    return false;
                }

                parsed.SnapshotFrom = args[++index];
            }
            else if (Is(argument, TodayArgument))
            {
                if (parsed.Today is not null)
                {
                    error = "\"--today\" is given twice.";
                    return false;
                }

                if (index + 1 >= args.Count ||
                    !DateTime.TryParseExact(args[index + 1], TodayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
                {
                    error = "\"--today\" needs a date written " + TodayFormat + ", such as 2026-10-16.";
                    return false;
                }

                parsed.Today = day.Date;
                index++;
            }
            else if (Is(argument, MeasureArgument))
            {
                if (parsed.MeasureFile is not null)
                {
                    error = "\"--measure\" is given twice.";
                    return false;
                }

                if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = "\"--measure\" needs the full path of the JSON file to write after it.";
                    return false;
                }

                parsed.MeasureFile = args[++index];
            }
            else if (Is(argument, PlatformCheckArgument))
            {
                if (parsed.PlatformCheckFile is not null)
                {
                    error = "\"--platform-check\" is given twice.";
                    return false;
                }

                if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = "\"--platform-check\" needs the full path of the JSON file to write after it.";
                    return false;
                }

                parsed.PlatformCheckFile = args[++index];
            }
        }

        if (parsed.Today is not null && parsed.SnapshotFrom is null)
        {
            error = "\"--today\" is accepted only with \"--snapshot-from\".";
            return false;
        }

        if (parsed.MeasureFile is not null && parsed.SelfCheck)
        {
            error = "\"--measure\" opens a window and \"--self-check\" opens none: give one or the other.";
            return false;
        }

        if (parsed.PlatformCheckFile is not null && parsed.SelfCheck)
        {
            error = "\"--platform-check\" opens a window and \"--self-check\" opens none: give one or the other.";
            return false;
        }

        if (parsed.PlatformCheckFile is not null && parsed.MeasureFile is not null)
        {
            error = "\"--platform-check\" and \"--measure\" each run their own check in the window: give one or the other.";
            return false;
        }

        if (parsed.PlatformCheckFile is not null && parsed.SnapshotFrom is not null)
        {
            error = "\"--platform-check\" runs on the example library only: leave out \"--snapshot-from\".";
            return false;
        }

        options = parsed;
        error = null;
        return true;
    }

    private static bool Is(string argument, string name) => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase);
}
