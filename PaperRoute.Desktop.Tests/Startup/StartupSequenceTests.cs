using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Startup;

// The start-up order (start-up design 2.4 and 5.1, build 6 Q1; acceptance
// line 1 of #421, "the start-up order of Program.vb:31-87 is kept and
// tested"). Each test runs StartupSequence over FakeStartupSteps and reads
// the order from the calls the fake recorded, not from source text. Steps 1
// to 7 and the end are here; the steps the app runs inside its window (8 to
// 13) join TheStartupOrderIsTodays when they are wired (W1a).
[TestClass]
public sealed class StartupSequenceTests
{
    private static readonly string[] s_toTheHold =
        ["options", "platform", "session", "data-root", "error-reports", "single-instance"];

    // "" is a plain start; the arguments are split on spaces.
    [TestMethod]
    [DataRow("", DisplayName = "a plain start")]
    [DataRow("--example", DisplayName = "--example")]
    public void TheStartupOrderIsTodays(string commandLine)
    {
        string[] args = Arguments(commandLine);
        var steps = new FakeStartupSteps { AppExitCode = 7 };

        int exit = StartupSequence.Run(args, steps);

        CallsAre(steps, "Start-up must keep today's order (Program.vb:31-90): " +
            "after Velopack in Main, the platform, the session, the data root, error reports, " +
            "the hold, the app, and at the end the hold's release and the session's end.",
            [.. s_toTheHold, "avalonia", "release-hold", "end-session"]);
        Assert.AreEqual(7, exit, "The exit code is the app's.");
        CollectionAssert.AreEqual(args, steps.Arguments!.ToArray(), "The options step reads the whole command line.");
        Assert.AreEqual(FakeStartupSteps.FakeDataRoot, steps.HeldRoot, "The hold is taken on the data root the data-root step found.");
    }

    [TestMethod]
    public void ARefusedCommandLineExitsTwoAndTouchesNothing()
    {
        var steps = new FakeStartupSteps { Mode = null };

        int exit = StartupSequence.Run(["--no-such-option"], steps);

        Assert.AreEqual(2, exit, "A refused command line exits with code 2 (the trial's rule).");
        CallsAre(steps, "A refused command line must stop before any other step, so nothing in the core is called.",
            ["options"]);
    }

    [TestMethod]
    public void TheExampleStartsItsSessionBeforeAnyRoot()
    {
        var steps = new FakeStartupSteps();

        StartupSequence.Run(["--example"], steps);

        IsBefore(steps, "platform", "session",
            "The platform is registered before the session (Program.vb:44, 49-50).");
        IsBefore(steps, "session", "data-root",
            "The example's session must start before any data root is found, or the root found would be the person's library (Program.vb:46-50).");
    }

    // The relative answer is the one the real step gives on Windows when the
    // local application data folder answers empty: Path.Combine("",
    // "PaperRoute") (StorageMigrationService.CurrentDataRoot).
    [TestMethod]
    [DataRow(true, "", DisplayName = "the data-root step throws")]
    [DataRow(false, " ", DisplayName = "the data-root step answers blank")]
    [DataRow(false, "PaperRoute", DisplayName = "the data-root step answers a relative path")]
    public void NoHomeFolderGivesTheNoWindowMessageAndExitsOne(bool throws, string answer)
    {
        var steps = throws
            ? new FakeStartupSteps { FailAt = "data-root" }
            : new FakeStartupSteps { DataRoot = answer };

        int exit = StartupSequence.Run(["--example"], steps);

        Assert.AreEqual(1, exit, "No home folder exits with code 1 (target map:377).");
        CallsAre(steps, "A data root that cannot be found must reach the no-window message, and no step after it may run, " +
            "apart from ending the session that had begun.",
            ["options", "platform", "session", "data-root", "no-window-message", "end-session"]);
        Assert.AreEqual("PaperRoute could not find your home folder, so it cannot open your library.", steps.Message,
            "The no-window message has the design's words (start-up design 2.4, step 4).");
    }

    [TestMethod]
    public void ErrorReportsAreHookedAfterTheSessionAndBeforeTheHold()
    {
        var steps = new FakeStartupSteps();

        StartupSequence.Run([], steps);

        IsBefore(steps, "session", "error-reports",
            "Error reports are hooked after the session, so the example's reports go to its own folder (Program.vb:52-56).");
        IsBefore(steps, "data-root", "error-reports",
            "Error reports are hooked after the data root is found in its guarded step, so finding it there cannot fail unguarded.");
        IsBefore(steps, "error-reports", "single-instance",
            "Error reports are hooked before the hold (Program.vb:56-66).");
    }

    [TestMethod]
    [DataRow(StartupMode.Person, 0, DisplayName = "a person's start exits 0")]
    [DataRow(StartupMode.Check, 3, DisplayName = "a check mode exits 3")]
    public void ASecondCopyStopsBeforeStorage(StartupMode mode, int expectedExit)
    {
        var steps = new FakeStartupSteps { Mode = mode, HeldByAnotherCopy = true };

        int exit = StartupSequence.Run([], steps);

        Assert.AreEqual(expectedExit, exit, "A second copy exits 0, or 3 in a check mode (start-up design 2.4, step 6).");
        CallsAre(steps, "A second copy must ask the first to come forward and stop: no storage step and no app " +
            "(Program.vb:64-72), only the end of the session it began.",
            [.. s_toTheHold, "ask-to-come-forward", "end-session"]);
        Assert.AreEqual(FakeStartupSteps.FakeDataRoot, steps.AskedRoot, "The copy asked is the one that holds this data root.");
        Assert.AreEqual(TimeSpan.FromSeconds(2), steps.AskedLimit, "A second copy waits 2 seconds at most to reach the first.");
    }

    [TestMethod]
    public void ASecondCopyExitsEvenWhenTheFirstCannotBeReached()
    {
        var steps = new FakeStartupSteps { HeldByAnotherCopy = true, OtherCopyAnswers = false };

        int exit = StartupSequence.Run([], steps);

        Assert.AreEqual(0, exit, "A come-forward request that fails must not change the exit: today's second copy exits silently.");
        CallsAre(steps, "A second copy whose request failed still stops before storage.",
            [.. s_toTheHold, "ask-to-come-forward", "end-session"]);
    }

    [TestMethod]
    public void TheHoldIsReleasedAndTheExampleEndedEvenWhenTheAppThrows()
    {
        var steps = new FakeStartupSteps { FailAt = "avalonia" };

        StepFailedException error = Assert.ThrowsExactly<StepFailedException>(() => StartupSequence.Run(["--example"], steps),
            "The app's error goes on to the caller.");

        Assert.AreEqual("avalonia", error.Step);
        CallsAre(steps, "When the app throws, the hold must still be released and then the example's session ended.",
            [.. s_toTheHold, "avalonia", "release-hold", "end-session"]);
        Assert.AreEqual(Environment.CurrentManagedThreadId, steps.HoldThread, "The hold is taken on the thread that runs start-up.");
        Assert.AreEqual(steps.HoldThread, steps.ReleaseThread,
            "The hold is released on the thread that took it, as the Windows mutex requires.");
    }

    // Not named in the design: its rule that the example's folder is deleted
    // at the end "whatever happened" (2.4, the end), step by step. A step
    // before the session ends nothing, since no session began.
    [TestMethod]
    [DataRow("platform", false, "options platform")]
    [DataRow("session", false, "options platform session end-session")]
    [DataRow("error-reports", false, "options platform session data-root error-reports end-session")]
    [DataRow("single-instance", false, "options platform session data-root error-reports single-instance end-session")]
    [DataRow("ask-to-come-forward", true, "options platform session data-root error-reports single-instance ask-to-come-forward end-session")]
    [DataRow("release-hold", false, "options platform session data-root error-reports single-instance avalonia release-hold end-session")]
    public void TheSessionIsEndedWhicheverStepFailsAfterItBegan(string failAt, bool heldByAnotherCopy, string expected)
    {
        var steps = new FakeStartupSteps { FailAt = failAt, HeldByAnotherCopy = heldByAnotherCopy };

        StepFailedException error = Assert.ThrowsExactly<StepFailedException>(() => StartupSequence.Run(["--example"], steps),
            "A failing step's error goes on to the caller.");

        Assert.AreEqual(failAt, error.Step);
        CallsAre(steps, $"When \"{failAt}\" fails, every step after it is skipped and the session that began is ended.",
            Arguments(expected));
    }

    private static string[] Arguments(string commandLine) =>
        commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static void CallsAre(FakeStartupSteps steps, string rule, string[] expected) =>
        Assert.AreEqual(string.Join(", ", expected), string.Join(", ", steps.Calls), rule);

    private static void IsBefore(FakeStartupSteps steps, string first, string then, string rule)
    {
        int firstAt = Index(steps, first);
        int thenAt = Index(steps, then);
        Assert.IsTrue(firstAt < thenAt,
            $"{rule} Recorded: {string.Join(", ", steps.Calls)}.");
    }

    private static int Index(FakeStartupSteps steps, string name)
    {
        int at = steps.Calls.ToList().IndexOf(name);
        Assert.IsGreaterThanOrEqualTo(0, at, $"The step \"{name}\" never ran. Recorded: {string.Join(", ", steps.Calls)}.");
        return at;
    }
}
