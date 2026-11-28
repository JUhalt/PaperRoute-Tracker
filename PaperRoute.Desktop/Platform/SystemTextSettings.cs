using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Platform;

// What the system says of its text size, read for "--platform-check" (the
// harness design, part 3.5). Avalonia 12.1.6 offers no text-size setting of
// its own (AvaloniaOffersNoTextSizeSetting), so the check reads the setting
// where each system keeps it and writes it beside the window's render
// scaling and what the window lays out (PlatformCheckWindow.FactsFor):
// - Windows: TextScaleFactor, a percentage, under the user's
//   Software\Microsoft\Accessibility key; Windows leaves it out at 100 %.
//   That this is where Text size lands is assumed until the Windows guest
//   run shows it.
// - Linux: GNOME's text-scaling-factor (gsettings) and the X resource
//   Xft.dpi (xrdb -query), each asked for with a short limit. A missing
//   tool or a refusal leaves the value null and says so.
// - Elsewhere nothing is read.
// Only numbers and fixed words are kept, never a tool's own message.
public static class SystemTextSettings
{
    public const string WindowsKey = @"Software\Microsoft\Accessibility";

    public const string WindowsValue = "TextScaleFactor";

    public const string NotReadHere = "not read on this system";

    private static readonly TimeSpan s_toolLimit = TimeSpan.FromSeconds(3);

    public static SystemTextSetting Read()
    {
        if (OperatingSystem.IsWindows())
        {
            return ReadWindows();
        }

        if (OperatingSystem.IsLinux())
        {
            return FromLinuxOutput(
                Ask("gsettings", ["get", "org.gnome.desktop.interface", "text-scaling-factor"]),
                Ask("xrdb", ["-query"]));
        }

        return new SystemTextSetting(null, NotReadHere, null);
    }

    // TextScaleFactor as the registry holds it: a whole percentage, or
    // nothing at Windows' default of 100 %.
    public static SystemTextSetting FromWindowsValue(object? value) => value switch
    {
        null => new SystemTextSetting(1.0, $"Windows: {WindowsValue} is not set (Windows' default, 100 %)", null),
        int percent when percent > 0 => new SystemTextSetting(percent / 100.0, $"Windows: {WindowsValue} under HKEY_CURRENT_USER\\{WindowsKey}", null),
        _ => new SystemTextSetting(null, $"Windows: {WindowsValue} holds a value that is not a positive whole number", null),
    };

    // What "gsettings get org.gnome.desktop.interface text-scaling-factor"
    // and "xrdb -query" wrote, or null for a tool that did not answer.
    public static SystemTextSetting FromLinuxOutput(string? gsettings, string? xrdb)
    {
        double? scale = Number(gsettings);
        string scaleSource = gsettings is null
            ? "GNOME: text-scaling-factor not read (gsettings did not answer)"
            : scale is null
                ? "GNOME: gsettings gave no number for text-scaling-factor"
                : "GNOME: text-scaling-factor (gsettings, org.gnome.desktop.interface)";
        double? dpi = XftDpiIn(xrdb);
        string dpiSource = xrdb is null
            ? "Xft.dpi not read (xrdb did not answer)"
            : dpi is null ? "Xft.dpi not set (xrdb -query)" : "Xft.dpi from xrdb -query";
        return new SystemTextSetting(scale, scaleSource + "; " + dpiSource, dpi);
    }

    // The value of the line "Xft.dpi:" in the X resources, or null.
    public static double? XftDpiIn(string? resources)
    {
        if (resources is null)
        {
            return null;
        }

        foreach (string line in resources.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Xft.dpi:", StringComparison.Ordinal))
            {
                return Number(trimmed["Xft.dpi:".Length..]);
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static SystemTextSetting ReadWindows()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(WindowsKey);
            return FromWindowsValue(key?.GetValue(WindowsValue));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new SystemTextSetting(null, $"Windows: {WindowsValue} could not be read ({ex.GetType().Name})", null);
        }
    }

    // A positive finite number written the invariant way, or null.
    private static double? Number(string? text) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && value > 0
            ? value
            : null;

    // Runs a tool with no window and returns what it wrote, or null when it
    // is missing, fails, or has not ended within the limit (it is stopped).
    private static string? Ask(string tool, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(tool)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(s_toolLimit))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            process.WaitForExit();
            _ = errors.Result;
            return process.ExitCode == 0 ? output.Result : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or AggregateException)
        {
            return null;
        }
    }
}
