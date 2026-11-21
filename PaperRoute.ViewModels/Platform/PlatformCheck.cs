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
// The outcomes are kept as they are gathered, so a window closed before the
// end (the person's way out of a dialog or browser that never comes back)
// still leaves what was learnt: Snapshot gives them, with the step that was
// running, and Abandon stops a second copy that is still running and tries
// no further step.
//
// Privacy: nothing chosen in the dialog is opened or read, and only how many
// items came back is kept. When the dialog throws, only the exception's type
// is kept, never its message, which may name a file the person chose. The
// other messages are passed through the scrubber, so no path under the
// person's profile, user name or computer name is kept.
public sealed class PlatformCheck
{
    public const string Mode = "platform-check";
    public const int FormatVersion = 1;

    public const string DialogNotice = "A file dialog opens now. Choose any file, or cancel. Nothing you choose is opened.";
    public const string LauncherNotice = "The trial asks the system to open example.org in your browser now.";
    public const string RestartNotice = "The trial starts a second copy of itself now and waits for it.";
    public const string DoneNotice = "The platform check is done. The trial writes the results and closes now.";

    // The detail of a dialog that threw: its message is not kept.
    public const string DialogFailedDetail =
        "The file dialog failed. Only the exception's type is kept: its message is left out, because it may name a file that was chosen.";

    // The names of the steps, as the JSON names their objects.
    public const string DialogStep = "fileDialog";
    public const string LauncherStep = "launcher";
    public const string RestartStep = "restart";

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
    private readonly object _gate = new();
    private readonly Stopwatch _stepClock = new();

    private CheckOutcome? _dialog;
    private CheckOutcome? _launcher;
    private CheckOutcome? _restartOutcome;
    private string? _running;
    private IStartedProgram? _started;
    private bool _abandoned;
    private string? _abandonedStop;

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

    // The step being tried now (DialogStep, LauncherStep or RestartStep), or
    // null between steps and after the last.
    public string? RunningStep
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    // Tries the three actions in order and returns their outcomes. After
    // Abandon, the steps not yet begun are not tried.
    public async Task<PlatformCheckOutcomes> RunAsync()
    {
        CheckOutcome dialog = await Step(DialogStep, TryDialogAsync);
        lock (_gate)
        {
            _dialog = dialog;
        }

        CheckOutcome launcher = await Step(LauncherStep, TryLauncherAsync);
        lock (_gate)
        {
            _launcher = launcher;
        }

        CheckOutcome restart = await Step(RestartStep, TryRestartAsync);
        lock (_gate)
        {
            _restartOutcome = restart;
        }

        if (!IsAbandoned)
        {
            _say(DoneNotice);
        }

        return new PlatformCheckOutcomes(dialog, launcher, restart);
    }

    // What is known so far: each finished step's outcome, the running step
    // as "still running", and the steps not reached as not tried.
    public PlatformCheckOutcomes Snapshot()
    {
        lock (_gate)
        {
            return new PlatformCheckOutcomes(
                _dialog ?? Pending(DialogStep),
                _launcher ?? Pending(LauncherStep),
                _restartOutcome ?? Pending(RestartStep));
        }
    }

    // Stops the check: a second copy still running is stopped, and no step
    // not yet begun is tried. Safe to call more than once.
    public void Abandon()
    {
        IStartedProgram? started;
        lock (_gate)
        {
            if (_abandoned)
            {
                return;
            }

            _abandoned = true;
            started = _started;
        }

        if (started is null)
        {
            return;
        }

        string stopped;
        try
        {
            started.Stop();
            stopped = "it was stopped";
        }
        catch (Exception ex)
        {
            stopped = "it could not be stopped (" + ex.GetType().Name + ": " + _scrubber.Scrub(ex.Message) + ")";
        }

        lock (_gate)
        {
            _abandonedStop = stopped;
        }
    }

    private bool IsAbandoned
    {
        get
        {
            lock (_gate)
            {
                return _abandoned;
            }
        }
    }

    private CheckOutcome Pending(string step)
    {
        if (string.Equals(step, _running, StringComparison.Ordinal))
        {
            string detail = "Still running when the window was closed: no outcome came back.";
            if (step == RestartStep && _abandonedStop is not null)
            {
                detail += " The second copy was still running; " + _abandonedStop + ".";
            }

            return new CheckOutcome(true, null, detail, _stepClock.Elapsed.TotalMilliseconds);
        }

        return new CheckOutcome(false, null, "Not reached: the window was closed before this step.", 0);
    }

    private async Task<CheckOutcome> Step(string name, Func<Task<CheckOutcome>> step)
    {
        lock (_gate)
        {
            if (_abandoned)
            {
                return new CheckOutcome(false, null, "Not reached: the window was closed before this step.", 0);
            }

            _running = name;
            _stepClock.Restart();
        }

        try
        {
            return await step();
        }
        finally
        {
            lock (_gate)
            {
                if (!_abandoned)
                {
                    _running = null;
                }
            }
        }
    }

    private async Task<CheckOutcome> TryDialogAsync()
    {
        if (!_actions.CanOpenFileDialog)
        {
            return new CheckOutcome(false, false,
                "Avalonia (the toolkit the trial's window is made with) reports no file dialog on this system (StorageProvider.CanOpen is false), so none was opened.", 0);
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
            // The type only: the message may name the chosen file.
            return new CheckOutcome(true, false, DialogFailedDetail, clock.Elapsed.TotalMilliseconds)
            {
                ExceptionType = ex.GetType().FullName,
            };
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
                ? $"The system accepted the request to open {LaunchAddress.OriginalString} (ok: true does not show that a page appeared; see the person's note)."
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
        string? note = _restart.Note is null ? null : " " + _restart.Note;
        if (_restart.FileName is null)
        {
            return new CheckOutcome(false, null, (_restart.NotPossible ?? "A restart could not be tried.") + note, 0)
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
            return Failed(reason + note, ex, clock) with { Program = program, StartedFrom = _restart.Source };
        }

        lock (_gate)
        {
            _started = started;
        }

        try
        {
            bool exited;
            try
            {
                exited = await started.WaitForExitAsync(_restartLimit);
            }
            catch (Exception ex)
            {
                string afterFailedWait = StopAfter(started);
                return Failed($"Waiting for the second copy from {program} failed; afterwards it {afterFailedWait}.{note}", ex, clock) with
                {
                    Program = program, StartedFrom = _restart.Source,
                };
            }

            if (exited)
            {
                int code = started.ExitCode;
                string detail = string.Format(CultureInfo.InvariantCulture,
                    "The second copy started from {0} with \"--self-check\" and exited with code {1}: {2}{3}",
                    program, code, MeaningOfExitCode(code), note);
                return new CheckOutcome(true, code == 0, detail, clock.Elapsed.TotalMilliseconds)
                {
                    Program = program, StartedFrom = _restart.Source, ExitCode = code, StoppedAfterLimit = false,
                };
            }

            string stopped = StopAfter(started);
            return new CheckOutcome(true, false,
                string.Format(CultureInfo.InvariantCulture,
                    "The second copy started from {0} did not exit within {1:0} s and {2}.{3}", program, _restartLimit.TotalSeconds, stopped, note),
                clock.Elapsed.TotalMilliseconds)
            {
                Program = program, StartedFrom = _restart.Source, StoppedAfterLimit = true,
            };
        }
        finally
        {
            lock (_gate)
            {
                _started = null;
            }

            started.Dispose();
        }
    }

    // What an exit code of the second copy tells, and no more: the trial's
    // self-check exits 0 or 1, and the trial exits 2 for a refused command
    // line; any other code came from elsewhere.
    public static string MeaningOfExitCode(int code) => code switch
    {
        0 => "its self-check passed.",
        1 => "it started, but its self-check did not pass.",
        2 => "the trial refused its command line or folder, so its self-check did not run.",
        _ => "the trial's self-check exits only 0 or 1 (and 2 for a refused command line), so this code came from somewhere else, " +
            "for example the AppImage runtime or the .NET runtime, before the self-check finished.",
    };

    // Stops a started program: "was stopped", or why it could not be.
    private string StopAfter(IStartedProgram started)
    {
        try
        {
            started.Stop();
            return "was stopped";
        }
        catch (Exception ex)
        {
            return "could not be stopped (" + ex.GetType().Name + ": " + _scrubber.Scrub(ex.Message) + ")";
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
// that cannot be judged), a detail in plain words, the exception's type when
// there was one and its message (scrubbed) when it may be kept, and how long
// it took. The restart adds the program's file name (never its folder), where
// that came from, its exit code and whether it was stopped at the limit; the
// dialog adds how many items came back.
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

// What a restart starts: the AppImage this process runs from, else the
// process's own program file. APPIMAGE is passed down to every program an
// AppImage starts (a terminal opened from an AppImage editor, say), so it is
// taken only when it is a full path to a file that exists and this process's
// program lies inside APPDIR, where the AppImage runtime mounted the image;
// otherwise it is ignored, with a note. When the process is the dotnet host
// ("dotnet PaperRoute.Desktop.dll") there is no program file of the trial's
// own to start, and the check says so instead of trying.
public sealed record RestartTarget(string? FileName, string Source, string? NotPossible)
{
    public const string FromAppImage = "APPIMAGE";
    public const string FromProcessPath = "process path";

    public const string AppImageIgnoredNote =
        "APPIMAGE is set, but this process is not running from it (it is not a full path to an existing file, or the program does not lie inside APPDIR), so it was ignored.";

    // Set when APPIMAGE was set and ignored.
    public string? Note { get; init; }

    // appImage and appDir are APPIMAGE's and APPDIR's values; fileExists
    // asks whether a file exists (the app gives File.Exists).
    public static RestartTarget For(string? appImage, string? appDir, string? processPath, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        string? note = null;
        if (!string.IsNullOrWhiteSpace(appImage))
        {
            if (RunsFromAppImage(appImage, appDir, processPath, fileExists))
            {
                return new RestartTarget(appImage, FromAppImage, null);
            }

            note = AppImageIgnoredNote;
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new RestartTarget(null, FromProcessPath,
                "The program's own file is not known (Environment.ProcessPath is empty), so a restart could not be tried.") { Note = note };
        }

        if (IsDotnetHost(processPath))
        {
            return new RestartTarget(null, FromProcessPath,
                "The trial was started through the dotnet host (\"dotnet PaperRoute.Desktop.dll\"), so it has no program file of its own to start again. " +
                "A restart was not tried. Run the published program file to try one.") { Note = note };
        }

        return new RestartTarget(processPath, FromProcessPath, null) { Note = note };
    }

    // True when appImage is a full path to an existing file and processPath
    // lies inside appDir (both full paths).
    public static bool RunsFromAppImage(string appImage, string? appDir, string? processPath, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(appImage);
        ArgumentNullException.ThrowIfNull(fileExists);
        if (!IsFullPath(appImage) || string.IsNullOrWhiteSpace(appDir) || !IsFullPath(appDir)
            || string.IsNullOrWhiteSpace(processPath) || !IsFullPath(processPath))
        {
            return false;
        }

        string folder = appDir.Replace('\\', '/').TrimEnd('/') + "/";
        string program = processPath.Replace('\\', '/');
        if (folder.Length < 2 || !program.StartsWith(folder, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return fileExists(appImage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    // A full path on any system: "/..." as Linux and macOS write one, or a
    // fully qualified path of the system this runs on.
    public static bool IsFullPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.StartsWith('/') || Path.IsPathFullyQualified(path);
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
