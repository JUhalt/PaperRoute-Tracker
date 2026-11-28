using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Startup;

// Start-up's steps as a recording stand-in (start-up design 2.4, build 6 Q1):
// each call appends the step's record name to Calls, so a test reads the
// order from the calls StartupSequence made. Nothing here touches storage,
// the system or a window: the data root is a name only.
internal sealed class FakeStartupSteps : IStartupSteps
{
    public const string FakeDataRoot = "fake data root";

    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    // What step 1 answers: a kind of start, or null to refuse the command line.
    public StartupMode? Mode { get; set; } = StartupMode.Person;

    // What step 4 answers when it does not fail.
    public string DataRoot { get; set; } = FakeDataRoot;

    // Step 6 finds the library held by another copy.
    public bool HeldByAnotherCopy { get; set; }

    // What the request to the other copy answers.
    public bool OtherCopyAnswers { get; set; } = true;

    // The exit code the app returns.
    public int AppExitCode { get; set; }

    // The record name of a step that throws StepFailedException when it is
    // called, after it is recorded; null for none. The hold's release fails
    // with "release-hold".
    public string? FailAt { get; set; }

    public IReadOnlyList<string>? Arguments { get; private set; }

    public string? HeldRoot { get; private set; }

    public string? AskedRoot { get; private set; }

    public TimeSpan? AskedLimit { get; private set; }

    public string? Message { get; private set; }

    public int? HoldThread { get; private set; }

    public int? ReleaseThread { get; private set; }

    public StartupMode? ReadOptions(IReadOnlyList<string> args)
    {
        Record("options");
        Arguments = [.. args];
        return Mode;
    }

    public void RegisterPlatform() => Record("platform");

    public void BeginSession() => Record("session");

    public string FindDataRoot()
    {
        Record("data-root");
        return DataRoot;
    }

    public void HookErrorReports() => Record("error-reports");

    public IDisposable? TryHold(string dataRoot)
    {
        Record("single-instance");
        HeldRoot = dataRoot;
        if (HeldByAnotherCopy)
        {
            return null;
        }

        HoldThread = Environment.CurrentManagedThreadId;
        return new Hold(this);
    }

    public bool AskToComeForward(string dataRoot, TimeSpan limit)
    {
        Record("ask-to-come-forward");
        AskedRoot = dataRoot;
        AskedLimit = limit;
        return OtherCopyAnswers;
    }

    public int RunApp()
    {
        Record("avalonia");
        return AppExitCode;
    }

    public void EndSession() => Record("end-session");

    public void NoWindowMessage(string text)
    {
        Record("no-window-message");
        Message = text;
    }

    private void Record(string name)
    {
        _calls.Add(name);
        if (string.Equals(FailAt, name, StringComparison.Ordinal))
        {
            throw new StepFailedException(name);
        }
    }

    private sealed class Hold(FakeStartupSteps steps) : IDisposable
    {
        public void Dispose()
        {
            steps.ReleaseThread = Environment.CurrentManagedThreadId;
            steps.Record("release-hold");
        }
    }
}

// The failure FakeStartupSteps.FailAt throws, so a test can tell it from any
// other error.
internal sealed class StepFailedException(string step) : Exception($"The step \"{step}\" failed (FakeStartupSteps.FailAt).")
{
    public string Step { get; } = step;
}
