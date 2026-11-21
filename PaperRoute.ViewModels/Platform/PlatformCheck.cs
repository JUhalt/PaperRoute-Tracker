using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace PaperRoute.ViewModels.Platform;

// "--platform-check <file>.json" (d1 1.3 Modes, 7.2 row 5, step 15): with the
// trial's window open on the example library, it tries, one after the other,
// the system's file dialog once, the system's launcher once on
// https://example.org, and a restart (a second copy of the program started
// with "--self-check", waited for within a time limit). Each outcome is
// recorded, whatever it is; an action that throws is an outcome, not a
// failure of the check. Before each action the person is told what happens
// next (say: the notice strip and standard output).
//
// Privacy: nothing chosen in the dialog is opened or read, and only how many
// items came back is kept; every message is passed through the scrubber, so
// no path under the person's profile, user name or computer name is kept.
public sealed class PlatformCheck
{
    public const string Mode = "platform-check";
    public const int FormatVersion = 1;

    public const string DialogNotice = "A file dialog opens now. Choose any file, or cancel. Nothing you choose is opened.";
    public const string LauncherNotice = "Your browser opens example.org now.";
    public const string RestartNotice = "The trial starts a second copy of itself now and waits for it.";
    public const string DoneNotice = "The platform check is done. The trial writes the results and closes now.";

    // The address the launcher is asked to open.
    public static readonly Uri LaunchAddress = new("https://example.org");

    // The second copy runs the example's self-check: no window, its own
    // session folder, exit code 0 when every check passed.
    public static readonly IReadOnlyList<string> RestartArguments = ["--self-check"];

    // The self-check takes seconds; a slow virtual machine gets room.
    public static readonly TimeSpan DefaultRestartLimit = TimeSpan.FromMinutes(2);

    private readonly IPlatformActions _actions;
    private readonly RestartTarget _restart;
    private readonly PrivacyScrubber _scrubber;
    private readonly Action<string> _say;
    private readonly TimeSpan _restartLimit;

    public PlatformCheck(IPlatformActions actions, RestartTarget restart, PrivacyScrubber scrubber, Action<string> say, TimeSpan restartLimit)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(restart);
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(say);
        _actions = actions;
        _restart = restart;
        _scrubber = scrubber;
        _say = say;
        _restartLimit = restartLimit;
    }

    // Tries the three actions in order and returns their outcomes.
    public async Task<PlatformCheckOutcomes> RunAsync()
    {
        CheckOutcome dialog = await TryDialogAsync();
        CheckOutcome launcher = await TryLauncherAsync();
        CheckOutcome restart = await TryRestartAsync();
        _say(DoneNotice);
        return new PlatformCheckOutcomes(dialog, launcher, restart);
    }

    private async Task<CheckOutcome> TryDialogAsync()
    {
        if (!_actions.CanOpenFileDialog)
        {
            return new CheckOutcome(false, false,
                "Avalonia reports no file dialog on this system (StorageProvider.CanOpen is false), so none was opened.", 0);
        }

        _say(DialogNotice);
        var clock = Stopwatch.StartNew();
        try
        {
            IReadOnlyList<string> chosen = await _actions.OpenFileDialogAsync();
            int count = chosen?.Count ?? 0;
            string detail = count == 0
                ? "The file dialog opened and came back with nothing chosen (cancelled)."
                : string.Format(CultureInfo.InvariantCulture,
                    "The file dialog opened and came back with {0} item(s) chosen. Nothing chosen was opened or read.", count);
            return new CheckOutcome(true, true, detail, clock.Elapsed.TotalMilliseconds) { ItemsChosen = count };
        }
        catch (Exception ex)
        {
            return Failed("The file dialog failed.", ex, clock);
        }
    }

    private async Task<CheckOutcome> TryLauncherAsync()
    {
        _say(LauncherNotice);
        var clock = Stopwatch.StartNew();
        try
        {
            bool launched = await _actions.LaunchAsync(LaunchAddress);
            string detail = launched
                ? $"The system reports that it opened {LaunchAddress.OriginalString}."
                : $"The system reports that it could not open {LaunchAddress.OriginalString}.";
            return new CheckOutcome(true, launched, detail, clock.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            return Failed("The launcher failed.", ex, clock);
        }
    }

    private async Task<CheckOutcome> TryRestartAsync()
    {
        if (_restart.FileName is null)
        {
            return new CheckOutcome(false, null, _restart.NotPossible ?? "A restart could not be tried.", 0)
            {
                StartedFrom = _restart.Source,
            };
        }

        string program = RestartTarget.FileNameOf(_restart.FileName);
        _say(RestartNotice);
        var clock = Stopwatch.StartNew();
        IStartedProgram started;
        try
        {
            started = _actions.StartProgram(_restart.FileName, RestartArguments);
        }
        catch (Exception ex)
        {
            string reason = ex is Win32Exception or FileNotFoundException
                ? $"The second copy could not be started from {program}."
                : $"Starting the second copy from {program} failed.";
            return Failed(reason, ex, clock) with { Program = program, StartedFrom = _restart.Source };
        }

        using (started)
        {
            try
            {
                if (await started.WaitForExitAsync(_restartLimit))
                {
                    int code = started.ExitCode;
                    string detail = code == 0
                        ? $"The second copy started from {program} with \"--self-check\" and exited with code 0: its self-check passed."
                        : string.Format(CultureInfo.InvariantCulture,
                            "The second copy started from {0} with \"--self-check\" and exited with code {1}: it started, but its self-check did not pass.", program, code);
                    return new CheckOutcome(true, code == 0, detail, clock.Elapsed.TotalMilliseconds)
                    {
                        Program = program, StartedFrom = _restart.Source, ExitCode = code, StoppedAfterLimit = false,
                    };
                }
            }
            catch (Exception ex)
            {
                return Failed($"Waiting for the second copy from {program} failed.", ex, clock) with
                {
                    Program = program, StartedFrom = _restart.Source,
                };
            }

            string stopped;
            try
            {
                started.Stop();
                stopped = "and was stopped";
            }
            catch (Exception ex)
            {
                stopped = "and could not be stopped (" + ex.GetType().Name + ": " + _scrubber.Scrub(ex.Message) + ")";
            }

            return new CheckOutcome(true, false,
                string.Format(CultureInfo.InvariantCulture,
                    "The second copy started from {0} did not exit within {1:0} s {2}.", program, _restartLimit.TotalSeconds, stopped),
                clock.Elapsed.TotalMilliseconds)
            {
                Program = program, StartedFrom = _restart.Source, StoppedAfterLimit = true,
            };
        }
    }

    private CheckOutcome Failed(string detail, Exception ex, Stopwatch clock) =>
        new(true, false, detail, clock.Elapsed.TotalMilliseconds)
        {
            ExceptionType = ex.GetType().FullName,
            ExceptionMessage = _scrubber.Scrub(ex.Message),
        };
}

// The three outcomes, in the order tried.
public sealed record PlatformCheckOutcomes(CheckOutcome Dialog, CheckOutcome Launcher, CheckOutcome Restart);

// One action's outcome: whether it was tried, whether it worked (null when
// that cannot be judged), a detail in plain words, the exception when there
// was one (its message scrubbed), and how long it took. The restart adds the
// program's file name (never its folder), where that came from, its exit
// code and whether it was stopped at the limit; the dialog adds how many
// items came back.
public sealed record CheckOutcome(bool Tried, bool? Ok, string Detail, double Milliseconds)
{
    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public int? ItemsChosen { get; init; }

    public string? Program { get; init; }

    public string? StartedFrom { get; init; }

    public int? ExitCode { get; init; }

    public bool? StoppedAfterLimit { get; init; }
}

// What a restart starts: the AppImage when APPIMAGE is set, else the process's
// own program file. When the process is the dotnet host ("dotnet
// PaperRoute.Desktop.dll") there is no program file of the trial's own to
// start, and the check says so instead of trying.
public sealed record RestartTarget(string? FileName, string Source, string? NotPossible)
{
    public const string FromAppImage = "APPIMAGE";
    public const string FromProcessPath = "process path";

    public static RestartTarget For(string? appImage, string? processPath)
    {
        if (!string.IsNullOrWhiteSpace(appImage))
        {
            return new RestartTarget(appImage, FromAppImage, null);
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new RestartTarget(null, FromProcessPath,
                "The program's own file is not known (Environment.ProcessPath is empty), so a restart could not be tried.");
        }

        if (IsDotnetHost(processPath))
        {
            return new RestartTarget(null, FromProcessPath,
                "The trial was started through the dotnet host (\"dotnet PaperRoute.Desktop.dll\"), so it has no program file of its own to start again. " +
                "A restart was not tried. Run the published program file to try one.");
        }

        return new RestartTarget(processPath, FromProcessPath, null);
    }

    // "dotnet" or "dotnet.exe", in any folder and on any system.
    public static bool IsDotnetHost(string processPath)
    {
        ArgumentNullException.ThrowIfNull(processPath);
        string name = FileNameOf(processPath);
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase);
    }

    // The file name of a path written for any system: the part after the last
    // slash or backslash.
    public static string FileNameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string normal = path.Replace('\\', '/');
        return normal[(normal.LastIndexOf('/') + 1)..];
    }
}
