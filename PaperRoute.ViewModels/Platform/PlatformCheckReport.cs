using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Platform;

// What a reader of the platform check's JSON needs beside the outcomes to
// judge them: the system, the runtime, whether APPIMAGE was set (not its
// value; whether the trial ran from it is the restart's startedFrom, since
// APPIMAGE is passed down to every program an AppImage starts), the Linux
// session type (XDG_SESSION_TYPE's value, and only
// whether WAYLAND_DISPLAY and DISPLAY are set), the window's render scaling
// and the theme Avalonia reports. No names of people, computers or files.
public sealed record PlatformFacts(
    string OperatingSystem,
    string RuntimeIdentifier,
    string ProcessArchitecture,
    bool AppImageSet,
    string? SessionType,
    bool WaylandDisplaySet,
    bool DisplaySet,
    double RenderScaling,
    string ThemeVariant,
    string ContrastPreference)
{
    // The facts for this process, with the environment read through
    // environment (tests give their own) and the window's figures given.
    public static PlatformFacts Read(Func<string, string?> environment, double renderScaling, string themeVariant, string contrastPreference)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string? session = environment("XDG_SESSION_TYPE");
        return new PlatformFacts(
            RuntimeInformation.OSDescription,
            RuntimeInformation.RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(),
            !string.IsNullOrEmpty(environment("APPIMAGE")),
            string.IsNullOrWhiteSpace(session) ? null : session.Trim(),
            !string.IsNullOrEmpty(environment("WAYLAND_DISPLAY")),
            !string.IsNullOrEmpty(environment("DISPLAY")),
            renderScaling,
            themeVariant ?? string.Empty,
            contrastPreference ?? string.Empty);
    }
}

// The JSON "--platform-check" writes: the mode and the format's version,
// the time, the facts, then one small object per action (tried, ok, detail,
// the exception's type when there was one and its message when it may be
// kept, milliseconds), the error when the check itself broke or its window
// was closed before the end, and then the step that was still running.
public sealed class PlatformCheckReport
{
    public required DateTime TakenAt { get; init; }

    public required PlatformFacts Facts { get; init; }

    // Null when the check broke before the actions were tried.
    public PlatformCheckOutcomes? Outcomes { get; init; }

    // Set when the check itself broke (scrubbed), or the window was closed
    // before it finished; the exit code is then 1.
    public string? Error { get; init; }

    // The step that was still running when the window was closed early
    // (PlatformCheck.DialogStep, LauncherStep or RestartStep), else null.
    public string? StepRunning { get; init; }

    // The error of a check whose window was closed before it finished.
    public const string ClosedEarlyError = "The window was closed before the check finished.";

    public const string ExistingFileMessage =
        "The platform check does not replace a file it did not write: {0} already exists. Choose a new file name.";

    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("mode", PlatformCheck.Mode);
            json.WriteNumber("formatVersion", PlatformCheck.FormatVersion);
            json.WriteString("takenAt", TakenAt.ToString("o", CultureInfo.InvariantCulture));
            json.WriteString("operatingSystem", Facts.OperatingSystem);
            json.WriteString("runtimeIdentifier", Facts.RuntimeIdentifier);
            json.WriteString("processArchitecture", Facts.ProcessArchitecture);
            json.WriteBoolean("appImageSet", Facts.AppImageSet);
            if (Facts.SessionType is null)
            {
                json.WriteNull("xdgSessionType");
            }
            else
            {
                json.WriteString("xdgSessionType", Facts.SessionType);
            }

            json.WriteBoolean("waylandDisplaySet", Facts.WaylandDisplaySet);
            json.WriteBoolean("displaySet", Facts.DisplaySet);
            json.WriteNumber("renderScaling", Math.Round(Facts.RenderScaling, 3));
            json.WriteString("themeVariant", Facts.ThemeVariant);
            json.WriteString("contrastPreference", Facts.ContrastPreference);
            if (Outcomes is not null)
            {
                Write(json, "fileDialog", Outcomes.Dialog);
                Write(json, "launcher", Outcomes.Launcher);
                Write(json, "restart", Outcomes.Restart);
            }

            if (Error is not null)
            {
                json.WriteString("error", Error);
            }

            if (StepRunning is not null)
            {
                json.WriteString("stepRunning", StepRunning);
            }

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

    // Why the check may not write its JSON at path, or null when it may: the
    // rule "--measure" follows (BoardMeasurementReport.RefusalFor), in this
    // mode's words, and one more: a path that already exists is refused
    // unless it is a JSON this mode wrote (its root "mode" is
    // "platform-check"), so a mistyped path never replaces another file.
    public static string? RefusalFor(string path, IEnumerable<string>? alsoRefuse = null)
    {
        string? refusal = BoardMeasurementReport.RefusalFor(path, TrialOptions.PlatformCheckArgument, "platform check", alsoRefuse);
        if (refusal is not null)
        {
            return refusal;
        }

        string full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            return string.Format(CultureInfo.InvariantCulture, ExistingFileMessage, full);
        }

        return File.Exists(full) && !WrittenByThisMode(full)
            ? string.Format(CultureInfo.InvariantCulture, ExistingFileMessage, full)
            : null;
    }

    // True when the file is a small JSON whose root "mode" is this mode's.
    // Only a file of at most 1 MB is read; anything else is not this mode's.
    private static bool WrittenByThisMode(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 1024 * 1024)
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("mode", out JsonElement mode)
                && mode.ValueKind == JsonValueKind.String
                && string.Equals(mode.GetString(), PlatformCheck.Mode, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static void Write(Utf8JsonWriter json, string name, CheckOutcome outcome)
    {
        json.WriteStartObject(name);
        json.WriteBoolean("tried", outcome.Tried);
        if (outcome.Ok is { } ok)
        {
            json.WriteBoolean("ok", ok);
        }
        else
        {
            json.WriteNull("ok");
        }

        json.WriteString("detail", outcome.Detail);
        if (outcome.ExceptionType is not null)
        {
            json.WriteString("exceptionType", outcome.ExceptionType);
        }

        // Left out when it may not be kept (the file dialog's).
        if (outcome.ExceptionMessage is not null)
        {
            json.WriteString("exceptionMessage", outcome.ExceptionMessage);
        }

        json.WriteNumber("milliseconds", Math.Round(outcome.Milliseconds, 1));
        if (outcome.ItemsChosen is { } items)
        {
            json.WriteNumber("itemsChosen", items);
        }

        if (outcome.Program is not null)
        {
            json.WriteString("program", outcome.Program);
        }

        if (outcome.StartedFrom is not null)
        {
            json.WriteString("startedFrom", outcome.StartedFrom);
        }

        if (outcome.ExitCode is { } code)
        {
            json.WriteNumber("exitCode", code);
        }

        if (outcome.StoppedAfterLimit is { } stopped)
        {
            json.WriteBoolean("stoppedAfterLimit", stopped);
        }

        json.WriteEndObject();
    }
}
