using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using PaperRoute.Desktop.Measurement;
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
// first), and the exit code is 0 when the check ran to its end and the JSON
// was written, whatever each action's outcome, or 1 when the check itself
// broke.
//
// Closing the window before the end is the person's way out of a dialog or
// a browser that never comes back: CloseEarly then stops a second copy that
// is still running, writes what was learnt so far with the error and the
// step that was running, and the exit code is 1.
public sealed class PlatformCheckWindow
{
    // The step named when the window is closed while the library still loads.
    public const string LoadStep = "load";

    private readonly Window _window;
    private readonly ShellViewModel _shell;
    private readonly string _path;
    private readonly TextWriter _output;
    private readonly Func<string, string?> _environment;
    private readonly PrivacyScrubber _scrubber;
    private readonly PlatformCheck _check;
    private bool _loading;
    private bool _written;

    public PlatformCheckWindow(
        Window window,
        ShellViewModel shell,
        string path,
        IPlatformActions actions,
        RestartTarget restart,
        PrivacyScrubber scrubber,
        TextWriter output,
        TimeSpan restartLimit,
        Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(restart);
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(environment);
        _window = window;
        _shell = shell;
        _path = path;
        _output = output;
        _environment = environment;
        _scrubber = scrubber;
        _check = new PlatformCheck(actions, restart, scrubber, Say, restartLimit);
    }

    // True once the JSON was written, by the run or by CloseEarly.
    public bool Written => _written;

    public static Task<int> RunAsync(
        Window window,
        ShellViewModel shell,
        string path,
        IPlatformActions actions,
        RestartTarget restart,
        PrivacyScrubber scrubber,
        TextWriter output,
        TimeSpan restartLimit,
        Func<string, string?> environment,
        Action? beforeClose = null) =>
        new PlatformCheckWindow(window, shell, path, actions, restart, scrubber, output, restartLimit, environment).RunAsync(beforeClose);

    public async Task<int> RunAsync(Action? beforeClose = null)
    {
        PlatformCheckOutcomes? outcomes = null;
        string? error = null;
        try
        {
            if (_shell.LoadCommand.CanExecute(null))
            {
                _loading = true;
                try
                {
                    await _shell.LoadCommand.ExecuteAsync(null);
                }
                finally
                {
                    _loading = false;
                }
            }

            outcomes = await _check.RunAsync();
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + _scrubber.Scrub(ex.Message);
        }

        if (_written)
        {
            // CloseEarly wrote the JSON, and the window is closed already.
            return 1;
        }

        if (!Write(new PlatformCheckReport { TakenAt = DateTime.Now, Facts = Facts(ref error), Outcomes = outcomes, Error = error }))
        {
            error ??= "The JSON could not be written.";
        }

        beforeClose?.Invoke();
        _window.Close();
        return error is null ? 0 : 1;
    }

    // The window was closed before the check finished: a second copy still
    // running is stopped, no further step is tried, and the JSON gets what is
    // known (finished steps, the running one as still running, the rest as
    // not reached), the error and the running step. Returns 1. Does nothing
    // more once the JSON was written.
    public int CloseEarly()
    {
        if (_written)
        {
            return 1;
        }

        string? step = _loading ? LoadStep : _check.RunningStep;
        _check.Abandon();
        string? error = PlatformCheckReport.ClosedEarlyError;
        PlatformFacts facts = Facts(ref error);
        Write(new PlatformCheckReport
        {
            TakenAt = DateTime.Now,
            Facts = facts,
            Outcomes = _check.Snapshot(),
            Error = error,
            StepRunning = step,
        });
        return 1;
    }

    // The window's render scaling and what Avalonia reports of the system's
    // theme (PlatformSettings.GetColorValues), for the record only: a named
    // exception in ThemeOwnership, since nothing here decides a variant.
    public static PlatformFacts FactsFor(Window window, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(window);
        PlatformColorValues? colors = Application.Current?.PlatformSettings?.GetColorValues();
        return Stamped(PlatformFacts.Read(
            environment,
            window.RenderScaling,
            colors?.ThemeVariant.ToString() ?? "unknown",
            colors?.ContrastPreference.ToString() ?? "unknown"));
    }

    // The trial's version as it was built: "0.1.0-trial+" and the hash of the
    // commit the build was made from (the SDK adds the hash).
    public static string? TrialVersion =>
        typeof(PlatformCheckWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    // The facts with which trial this is: its version and its configuration.
    public static PlatformFacts Stamped(PlatformFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts with { TrialVersion = TrialVersion, Build = BoardMeasurement.Build };
    }

    private void Say(string text)
    {
        ((IInteractions)_shell).Notice(text);
        _output.WriteLine(text);
        _output.Flush();
    }

    private PlatformFacts Facts(ref string? error)
    {
        try
        {
            return FactsFor(_window, _environment);
        }
        catch (Exception ex)
        {
            error ??= ex.GetType().Name + ": " + _scrubber.Scrub(ex.Message);
            return Stamped(PlatformFacts.Read(_environment, 0, "unknown", "unknown"));
        }
    }

    // Writes the JSON once; false (and a line on standard error) when it
    // could not be written.
    private bool Write(PlatformCheckReport report)
    {
        _written = true;
        try
        {
            report.WriteJson(_path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"The platform check could not be written to {_path}: {ex.Message}");
            return false;
        }
    }
}
