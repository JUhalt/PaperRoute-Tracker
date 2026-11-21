using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Platform;

// What a reader of the platform check's JSON needs beside the outcomes to
// judge them: the system, the runtime, whether the trial ran from an AppImage
// (not where), the Linux session type (XDG_SESSION_TYPE's value, and only
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
// the exception's type and message when there was one, milliseconds), and
// the error when the check itself broke.
public sealed class PlatformCheckReport
{
    public required DateTime TakenAt { get; init; }

    public required PlatformFacts Facts { get; init; }

    // Null when the check broke before the actions were tried.
    public PlatformCheckOutcomes? Outcomes { get; init; }

    // Set when the check itself broke (scrubbed); the exit code is then 1.
    public string? Error { get; init; }

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
    // mode's words.
    public static string? RefusalFor(string path, IEnumerable<string>? alsoRefuse = null) =>
        BoardMeasurementReport.RefusalFor(path, TrialOptions.PlatformCheckArgument, "platform check", alsoRefuse);

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
            json.WriteString("exceptionMessage", outcome.ExceptionMessage ?? string.Empty);
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
