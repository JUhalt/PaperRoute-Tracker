using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Platform;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Platform;

// "--platform-check <file>.json" in the trial's window (d1 1.3 Modes, step
// 15): once the window is open, the example library loads as it does without
// the mode, then the check tries its three actions (PlatformCheck), telling
// the person before each one in the notice strip and on standard output. The
// outcomes and the facts go to the JSON at path (accepted by
// PlatformCheckReport.RefusalFor), the window closes (beforeClose runs
// first), and the exit code is 0
// when the check ran to its end and the JSON was written, whatever each
// action's outcome, or 1 when the check itself broke.
public static class PlatformCheckWindow
{
    public static async Task<int> RunAsync(
        Window window,
        ShellViewModel shell,
        string path,
        IPlatformActions actions,
        RestartTarget restart,
        PrivacyScrubber scrubber,
        TextWriter output,
        TimeSpan restartLimit,
        Func<string, string?> environment,
        Action? beforeClose = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(restart);
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(environment);

        void Say(string text)
        {
            ((IInteractions)shell).Notice(text);
            output.WriteLine(text);
            output.Flush();
        }

        PlatformCheckOutcomes? outcomes = null;
        string? error = null;
        try
        {
            if (shell.LoadCommand.CanExecute(null))
            {
                await shell.LoadCommand.ExecuteAsync(null);
            }

            outcomes = await new PlatformCheck(actions, restart, scrubber, Say, restartLimit).RunAsync();
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + scrubber.Scrub(ex.Message);
        }

        PlatformFacts facts;
        try
        {
            facts = FactsFor(window, environment);
        }
        catch (Exception ex)
        {
            error ??= ex.GetType().Name + ": " + scrubber.Scrub(ex.Message);
            facts = PlatformFacts.Read(environment, 0, "unknown", "unknown");
        }

        var report = new PlatformCheckReport { TakenAt = DateTime.Now, Facts = facts, Outcomes = outcomes, Error = error };
        try
        {
            report.WriteJson(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"The platform check could not be written to {path}: {ex.Message}");
            error ??= ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"The platform check could not be written to {path}: {ex.Message}");
            error ??= ex.Message;
        }

        beforeClose?.Invoke();
        window.Close();
        return error is null ? 0 : 1;
    }

    // The window's render scaling and what Avalonia reports of the system's
    // theme (PlatformSettings.GetColorValues).
    public static PlatformFacts FactsFor(Window window, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(window);
        PlatformColorValues? colors = Application.Current?.PlatformSettings?.GetColorValues();
        return PlatformFacts.Read(
            environment,
            window.RenderScaling,
            colors?.ThemeVariant.ToString() ?? "unknown",
            colors?.ContrastPreference.ToString() ?? "unknown");
    }
}
