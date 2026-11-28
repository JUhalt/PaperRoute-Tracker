using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PaperRoute.Desktop.Platform;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Platform;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// "--platform-check <file>.json" (d1 1.3 Modes, step 15), proved without a
// real window, dialog, browser or second program: the command line, the
// files it refuses before anything opens (the rule "--measure" uses), the
// check's flow with stand-ins for every outcome of its three actions, the
// JSON read back field by field, that nothing about a chosen file, the
// person or the computer reaches the JSON, and the flow in the trial's own
// window on the headless platform. The real dialog and launcher
// (AvaloniaPlatformActions) are run by a person only, and no test here
// starts the program with the mode: its window would be real. The real
// start of a second program opens nothing, so the last three tests run it.
[TestClass]
public sealed class PlatformCheckTests : StorageCheckedTests
{
    // A file name nobody else writes, to search the JSON for.
    private const string ChosenName = "Chosen-Secret-Draft-7Q4Z.docx";

    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialPlatform-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for the temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the temporary-file clean-up.
        }
    }

    // ---- The command line ----

    [TestMethod]
    public void PlatformCheckTakesTheFileAfterIt()
    {
        string file = Path.Combine(_root, "platform.json");
        Assert.IsTrue(TrialOptions.TryParse(["--platform-check", file], out TrialOptions? options, out string? error), error);
        Assert.AreEqual(file, options.PlatformCheckFile);
        Assert.IsNull(options.MeasureFile);
        Assert.IsNull(options.SnapshotFrom);
        Assert.IsFalse(options.SelfCheck);

        Assert.IsTrue(TrialOptions.TryParse(["--PLATFORM-CHECK", "/tmp/p.json"], out TrialOptions? upper, out error), error);
        Assert.AreEqual("/tmp/p.json", upper.PlatformCheckFile, "Arguments are read without regard to case, as the others are.");

        Assert.IsTrue(TrialOptions.TryParse([], out TrialOptions? none, out _));
        Assert.IsNull(none.PlatformCheckFile);
    }

    [TestMethod]
    [DataRow(new[] { "--platform-check" }, "\"--platform-check\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--platform-check", "" }, "\"--platform-check\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--platform-check", "--self-check" }, "\"--platform-check\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--platform-check", "/tmp/a.json", "--platform-check", "/tmp/b.json" }, "\"--platform-check\" is given twice.")]
    [DataRow(new[] { "--platform-check", "/tmp/a.json", "--self-check" }, "\"--platform-check\" opens a window and \"--self-check\" opens none: give one or the other.")]
    [DataRow(new[] { "--self-check", "--platform-check", "/tmp/a.json" }, "\"--platform-check\" opens a window and \"--self-check\" opens none: give one or the other.")]
    [DataRow(new[] { "--platform-check", "/tmp/a.json", "--measure", "/tmp/b.json" }, "\"--platform-check\" and \"--measure\" each run their own check in the window: give one or the other.")]
    [DataRow(new[] { "--snapshot-from", "/tmp/library", "--platform-check", "/tmp/a.json" }, "\"--platform-check\" runs on the example library only: leave out \"--snapshot-from\".")]
    [DataRow(new[] { "--platform-check", "/tmp/a.json", "--today", "2026-10-16" }, "\"--today\" is accepted only with \"--snapshot-from\".")]
    public void PlatformCheckRefusesACommandLineItCannotRun(string[] args, string message)
    {
        Assert.IsFalse(TrialOptions.TryParse(args, out TrialOptions? options, out string? error), string.Join(" ", args));
        Assert.IsNull(options);
        Assert.AreEqual(message, error);
    }

    // The refusals the process gives before it opens anything; each run also
    // carries --self-check, so a refusal that failed would open no window.
    [TestMethod]
    public void TheTrialRefusesAPlatformCheckItCannotRunAndOpensNothing()
    {
        SessionStartupProcessTests.SelfCheckRun missing = SessionStartupProcessTests.RunTrial("--self-check", "--platform-check");
        Assert.AreEqual(2, missing.ExitCode, missing.Describe());
        StringAssert.Contains(missing.Errors, "\"--platform-check\" needs the full path of the JSON file to write after it.", missing.Describe());
        Assert.IsEmpty(missing.Lines, missing.Describe());

        string file = Path.Combine(_root, "platform.json");
        SessionStartupProcessTests.SelfCheckRun both = SessionStartupProcessTests.RunTrial("--platform-check", file, "--self-check");
        Assert.AreEqual(2, both.ExitCode, both.Describe());
        StringAssert.Contains(both.Errors, "\"--platform-check\" opens a window and \"--self-check\" opens none", both.Describe());
        Assert.IsEmpty(both.Lines, both.Describe());
        Assert.IsFalse(File.Exists(file), "Nothing was written.");
    }

    // ---- Where the JSON may go: the rule "--measure" follows ----

    [TestMethod]
    public void TheCheckIsWrittenOnlyWhereTheMeasurementMayBe()
    {
        string fakeProfile = Path.Combine(_root, "profile", "PaperRoute");
        Directory.CreateDirectory(fakeProfile);

        Assert.IsNull(PlatformCheckReport.RefusalFor(Path.Combine(_root, "platform.json"), [fakeProfile]));
        Assert.StartsWith("\"--platform-check\" needs the full path of the JSON file to write, such as ", PlatformCheckReport.RefusalFor("platform.json"));
        Assert.StartsWith("The platform check is written as JSON:", PlatformCheckReport.RefusalFor(Path.Combine(_root, "platform.txt")));
        Assert.AreEqual($"The Board trial found no folder at {Path.Combine(_root, "missing")} to write the platform check into.",
            PlatformCheckReport.RefusalFor(Path.Combine(_root, "missing", "platform.json")));
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            PlatformCheckReport.RefusalFor(Path.Combine(fakeProfile, "platform.json"), [fakeProfile]));
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            PlatformCheckReport.RefusalFor(Path.Combine(fakeProfile, "data", "platform.json"), [fakeProfile]));

        // The real profile's folders, by path only.
        foreach (string root in ReadOnlySnapshot.DefaultStorageRoots())
        {
            Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
                PlatformCheckReport.RefusalFor(Path.Combine(root, "platform.json")), root);
        }

        // The example names a path of the system it runs on.
        string example = OperatingSystem.IsWindows() ? @"C:\Checks\platform-check.json" : "/tmp/platform-check.json";
        StringAssert.Contains(PlatformCheckReport.RefusalFor("platform.json"), example);

        // The measurement's own messages are unchanged.
        Assert.StartsWith("\"--measure\" needs the full path of the JSON file to write, such as C:\\Measurements\\board.json.",
            ViewModels.Measurement.BoardMeasurementReport.RefusalFor("board.json"));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fakeProfile).Any(), "Nothing was written into the fake profile.");
    }

    // A mistyped or tab-completed path never replaces another program's
    // file: an existing path is refused unless this mode wrote it.
    [TestMethod]
    [DataRow("settings.json", "{\"editor.fontSize\": 14}", "another program's settings")]
    [DataRow("other.json", "{\"mode\": \"measure\"}", "a JSON with another mode")]
    [DataRow("array.json", "[{\"mode\": \"platform-check\"}]", "a JSON whose root is not an object")]
    [DataRow("broken.json", "not json at all", "a file that is not JSON")]
    [DataRow("empty.json", "", "an empty file")]
    public void AnExistingFileTheCheckDidNotWriteIsRefusedAndKept(string name, string content, string why)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        Assert.AreEqual(
            $"The platform check does not replace a file it did not write: {path} already exists. Choose a new file name.",
            PlatformCheckReport.RefusalFor(path), why);
        Assert.AreEqual(content, File.ReadAllText(path), why);
    }

    [TestMethod]
    public void AJsonThePlatformCheckWroteMayBeReplaced()
    {
        string path = Path.Combine(_root, "platform-check.json");
        Assert.IsNull(PlatformCheckReport.RefusalFor(path), "A new file may be written.");
        (PlatformCheckOutcomes outcomes, _) = Run(new StandInActions(), Target());
        new PlatformCheckReport { TakenAt = new DateTime(2026, 11, 21), Facts = PlatformFacts.Read(_ => null, 1, "Light", "NoPreference"), Outcomes = outcomes }.WriteJson(path);
        Assert.IsNull(PlatformCheckReport.RefusalFor(path), "A JSON this mode wrote may be written again.");

        // A folder named like the file is refused.
        string folder = Path.Combine(_root, "folder.json");
        Directory.CreateDirectory(folder);
        StringAssert.StartsWith(PlatformCheckReport.RefusalFor(folder), "The platform check does not replace a file it did not write:");

        // "--measure" keeps its own rule: it replaces its file, as before.
        string measured = Path.Combine(_root, "settings.json");
        File.WriteAllText(measured, "{}");
        Assert.IsNull(ViewModels.Measurement.BoardMeasurementReport.RefusalFor(measured));
    }

    // ---- What a restart starts ----

    private const string TrialAppImage = "/opt/trial/PaperRoute-Board-Trial-x86_64.AppImage";
    private const string MountedProgram = "/tmp/.mount_x/usr/bin/PaperRoute.Desktop";
    private const string MountPoint = "/tmp/.mount_x";

    [TestMethod]
    public void TheRestartStartsTheAppImageOrTheProgramAndNeverTheDotnetHost()
    {
        RestartTarget appImage = RestartTarget.For(TrialAppImage, MountPoint, MountedProgram, path => path == TrialAppImage);
        Assert.AreEqual(TrialAppImage, appImage.FileName);
        Assert.AreEqual(RestartTarget.FromAppImage, appImage.Source);
        Assert.IsNull(appImage.NotPossible);
        Assert.IsNull(appImage.Note);
        Assert.AreEqual(TrialAppImage, RestartTarget.For(TrialAppImage, MountPoint + "/", MountedProgram, _ => true).FileName, "APPDIR with a slash at its end.");

        RestartTarget program = RestartTarget.For(null, null, "/opt/trial/PaperRoute.Desktop", _ => true);
        Assert.AreEqual("/opt/trial/PaperRoute.Desktop", program.FileName);
        Assert.AreEqual(RestartTarget.FromProcessPath, program.Source);
        Assert.IsNull(program.Note);
        Assert.AreEqual("/opt/trial/PaperRoute.Desktop", RestartTarget.For("", null, "/opt/trial/PaperRoute.Desktop", _ => true).FileName, "An empty APPIMAGE is not an AppImage.");

        foreach (string host in new[] { "/usr/share/dotnet/dotnet", @"C:\Program Files\dotnet\dotnet.exe", "/usr/lib/dotnet/DOTNET" })
        {
            RestartTarget viaHost = RestartTarget.For(null, null, host, _ => true);
            Assert.IsNull(viaHost.FileName, host);
            StringAssert.Contains(viaHost.NotPossible, "dotnet host", host);
        }

        Assert.IsFalse(RestartTarget.IsDotnetHost("/opt/trial/dotnetish"));
        Assert.IsNull(RestartTarget.For(null, null, null, _ => true).FileName);
        StringAssert.Contains(RestartTarget.For(null, null, null, _ => true).NotPossible, "Environment.ProcessPath is empty");
    }

    // APPIMAGE is passed down to every program an AppImage starts, so a value
    // this process does not run from is ignored, with a note, and the
    // process's own program is started instead.
    [TestMethod]
    [DataRow("ls", MountPoint, MountedProgram, true, "a bare name, which would be looked up on PATH")]
    [DataRow("relative/Trial.AppImage", MountPoint, MountedProgram, true, "a relative path")]
    [DataRow(TrialAppImage, MountPoint, MountedProgram, false, "a file that does not exist")]
    [DataRow("/opt/other/Editor-x86_64.AppImage", "/tmp/.mount_other", "/opt/trial/PaperRoute.Desktop", true, "another AppImage, whose mount this program is not in")]
    [DataRow(TrialAppImage, null, "/opt/trial/PaperRoute.Desktop", true, "no APPDIR")]
    [DataRow(TrialAppImage, "relative/mount", "relative/mount/usr/bin/PaperRoute.Desktop", true, "a relative APPDIR")]
    [DataRow(TrialAppImage, "/tmp/.mount_x", "/tmp/.mount_xy/usr/bin/PaperRoute.Desktop", true, "a program in a folder whose name only begins like APPDIR")]
    public void AnAppImageThisProcessDoesNotRunFromIsIgnored(string appImage, string? appDir, string processPath, bool exists, string why)
    {
        RestartTarget target = RestartTarget.For(appImage, appDir, processPath, _ => exists);
        Assert.AreEqual(processPath, target.FileName, why);
        Assert.AreEqual(RestartTarget.FromProcessPath, target.Source, why);
        Assert.AreEqual(RestartTarget.AppImageIgnoredNote, target.Note, why);

        // The note reaches the restart's detail, and APPIMAGE is not named as
        // where the program came from.
        (PlatformCheckOutcomes outcomes, _) = Run(new StandInActions(), target);
        Assert.AreEqual("process path", outcomes.Restart.StartedFrom, why);
        StringAssert.EndsWith(outcomes.Restart.Detail, " " + RestartTarget.AppImageIgnoredNote, why);
        using JsonDocument document = JsonDocument.Parse(Json(outcomes));
        Assert.AreEqual("process path", document.RootElement.GetProperty("restart").GetProperty("startedFrom").GetString(), why);
        Assert.AreEqual("PaperRoute.Desktop", document.RootElement.GetProperty("restart").GetProperty("program").GetString(), why);
    }

    // ---- The flow, with stand-ins ----

    [TestMethod]
    public void EveryActionIsTriedInOrderAndThePersonIsToldBeforeEach()
    {
        var actions = new StandInActions { Chosen = [Path.Combine(_root, ChosenName)] };
        (PlatformCheckOutcomes outcomes, List<string> said) = Run(actions, Target());

        CollectionAssert.AreEqual(
            new[] { "say: " + PlatformCheck.DialogNotice, "dialog", "say: " + PlatformCheck.LauncherNotice, "launch https://example.org/", "say: " + PlatformCheck.RestartNotice, "start PaperRoute.Desktop --self-check", "wait", "say: " + PlatformCheck.DoneNotice },
            actions.Log,
            string.Join(Environment.NewLine, actions.Log));
        Assert.IsTrue(outcomes.Dialog.Tried && outcomes.Launcher.Tried && outcomes.Restart.Tried);
        Assert.AreEqual(true, outcomes.Dialog.Ok);
        Assert.AreEqual(1, outcomes.Dialog.ItemsChosen);
        Assert.AreEqual(true, outcomes.Launcher.Ok);
        Assert.AreEqual(true, outcomes.Restart.Ok);
        Assert.AreEqual(0, outcomes.Restart.ExitCode);
        Assert.AreEqual(false, outcomes.Restart.StoppedAfterLimit);
        Assert.AreEqual("PaperRoute.Desktop", outcomes.Restart.Program);
        Assert.IsTrue(actions.Disposed, "The started program is let go.");
    }

    [TestMethod]
    [DataRow(0, "The file dialog opened and came back with nothing chosen (cancelled).")]
    [DataRow(1, "The file dialog opened and came back with 1 item(s) chosen. Nothing chosen was opened or read.")]
    [DataRow(2, "The file dialog opened and came back with 2 item(s) chosen. Nothing chosen was opened or read.")]
    public void TheDialogCountsWhatCameBackAndKeepsNothingElse(int count, string detail)
    {
        var actions = new StandInActions
        {
            Chosen = [.. Enumerable.Range(0, count).Select(index => Path.Combine(_root, index + "-" + ChosenName))],
        };
        (PlatformCheckOutcomes outcomes, _) = Run(actions, Target());
        Assert.IsTrue(outcomes.Dialog.Tried);
        Assert.AreEqual(true, outcomes.Dialog.Ok);
        Assert.AreEqual(count, outcomes.Dialog.ItemsChosen);
        Assert.AreEqual(detail, outcomes.Dialog.Detail);
        Assert.IsNull(outcomes.Dialog.ExceptionType);

        string json = Json(outcomes);
        Assert.DoesNotContain(ChosenName, json, "No chosen file's name is in the JSON.");
        Assert.DoesNotContain(_root, json, "No chosen file's folder is in the JSON.");
        Assert.DoesNotContain(JsonEncodedText.Encode(_root).ToString(), json, "No chosen file's folder is in the JSON, escaped either.");
    }

    [TestMethod]
    public void ADialogThatThrowsOrIsMissingIsAnOutcome()
    {
        // The message names the chosen file below the home folder and on a
        // drive outside it: neither may reach the JSON, whatever the
        // scrubber knows.
        string home = Path.GetDirectoryName(_root) ?? _root;
        string outside = "/media/usb-stick/Grants/" + ChosenName;
        var throwing = new StandInActions
        {
            DialogError = new InvalidOperationException($"The portal refused {Path.Combine(_root, "Sub-Folder-K2", ChosenName)} and file://{outside}."),
        };
        var scrubber = new PrivacyScrubber("/opt/trial/PaperRoute.Desktop", null, home, "someone", "a-computer");
        (PlatformCheckOutcomes thrown, List<string> said) = Run(throwing, Target(), scrubber: scrubber);
        Assert.IsTrue(thrown.Dialog.Tried);
        Assert.AreEqual(false, thrown.Dialog.Ok);
        Assert.AreEqual(PlatformCheck.DialogFailedDetail, thrown.Dialog.Detail);
        Assert.AreEqual("System.InvalidOperationException", thrown.Dialog.ExceptionType);
        Assert.IsNull(thrown.Dialog.ExceptionMessage, "The dialog's message is not kept.");
        Assert.IsTrue(thrown.Launcher.Tried && thrown.Restart.Tried, "The other actions are still tried.");
        Assert.Contains(PlatformCheck.DialogNotice, said);

        string json = Json(thrown);
        foreach (string secret in new[] { ChosenName, "Sub-Folder-K2", "usb-stick", "Grants", _root, JsonEncodedText.Encode(_root).ToString() })
        {
            Assert.DoesNotContain(secret, json, secret);
        }

        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement dialog = document.RootElement.GetProperty("fileDialog");
            Assert.AreEqual("System.InvalidOperationException", dialog.GetProperty("exceptionType").GetString());
            Assert.IsFalse(dialog.TryGetProperty("exceptionMessage", out _), "No exceptionMessage for the dialog.");
        }

        var none = new StandInActions { CanOpen = false };
        (PlatformCheckOutcomes missing, List<string> saidNone) = Run(none, Target());
        Assert.IsFalse(missing.Dialog.Tried);
        Assert.AreEqual(false, missing.Dialog.Ok);
        StringAssert.Contains(missing.Dialog.Detail, "StorageProvider.CanOpen is false");
        Assert.DoesNotContain("dialog", none.Log, "No dialog was asked for.");
        Assert.DoesNotContain(PlatformCheck.DialogNotice, saidNone, "The person is not told of a dialog that does not open.");
        Assert.IsTrue(missing.Launcher.Tried && missing.Restart.Tried);
    }

    [TestMethod]
    public void TheLauncherReportsWhatTheSystemSaid()
    {
        (PlatformCheckOutcomes opened, _) = Run(new StandInActions { Launched = true }, Target());
        Assert.AreEqual(true, opened.Launcher.Ok);
        Assert.AreEqual(
            "The system accepted the request to open https://example.org (ok: true does not show that a page appeared; see the person's note).",
            opened.Launcher.Detail);
        Assert.AreEqual("The trial asks the system to open example.org in your browser now.", PlatformCheck.LauncherNotice);

        (PlatformCheckOutcomes refused, _) = Run(new StandInActions { Launched = false }, Target());
        Assert.IsTrue(refused.Launcher.Tried);
        Assert.AreEqual(false, refused.Launcher.Ok);
        Assert.AreEqual("The system reports that it could not open https://example.org.", refused.Launcher.Detail);

        (PlatformCheckOutcomes thrown, _) = Run(new StandInActions { LaunchError = new NotSupportedException("No launcher here.") }, Target());
        Assert.IsTrue(thrown.Launcher.Tried);
        Assert.AreEqual(false, thrown.Launcher.Ok);
        Assert.AreEqual("System.NotSupportedException", thrown.Launcher.ExceptionType);
        Assert.AreEqual("No launcher here.", thrown.Launcher.ExceptionMessage);
        Assert.IsTrue(thrown.Restart.Tried, "The restart is still tried.");

        // Both reach the JSON.
        using JsonDocument document = JsonDocument.Parse(Json(thrown));
        JsonElement launcher = document.RootElement.GetProperty("launcher");
        Assert.AreEqual("System.NotSupportedException", launcher.GetProperty("exceptionType").GetString());
        Assert.AreEqual("No launcher here.", launcher.GetProperty("exceptionMessage").GetString());
        Assert.AreEqual("The launcher failed.", launcher.GetProperty("detail").GetString());
    }

    [TestMethod]
    public void ARestartThatExitsIsJudgedByItsExitCode()
    {
        (PlatformCheckOutcomes passed, _) = Run(new StandInActions { ExitCode = 0 }, Target());
        Assert.AreEqual(true, passed.Restart.Ok);
        Assert.AreEqual(0, passed.Restart.ExitCode);
        StringAssert.Contains(passed.Restart.Detail, "exited with code 0: its self-check passed.");

        var failing = new StandInActions { ExitCode = 1 };
        (PlatformCheckOutcomes failed, _) = Run(failing, Target());
        Assert.IsTrue(failed.Restart.Tried);
        Assert.AreEqual(false, failed.Restart.Ok);
        Assert.AreEqual(1, failed.Restart.ExitCode);
        Assert.AreEqual(false, failed.Restart.StoppedAfterLimit);
        StringAssert.Contains(failed.Restart.Detail, "exited with code 1: it started, but its self-check did not pass.");
        Assert.IsFalse(failing.Stopped, "A program that exited is not stopped.");
    }

    // Only 1 means a failed self-check: 2 is a refused command line, and any
    // other code came from elsewhere (an AppImage runtime that cannot mount,
    // a missing loader, a crash), so the detail does not claim a self-check.
    [TestMethod]
    [DataRow(2, "exited with code 2: the trial refused its command line or folder, so its self-check did not run.")]
    [DataRow(127, "exited with code 127: the trial's self-check exits only 0 or 1 (and 2 for a refused command line), so this code came from somewhere else, for example the AppImage runtime or the .NET runtime, before the self-check finished.")]
    [DataRow(134, "exited with code 134: the trial's self-check exits only 0 or 1")]
    [DataRow(-1, "exited with code -1: the trial's self-check exits only 0 or 1")]
    public void AnExitCodeOtherThan0Or1IsNotCalledAFailedSelfCheck(int code, string expected)
    {
        (PlatformCheckOutcomes outcomes, _) = Run(new StandInActions { ExitCode = code }, Target());
        Assert.IsTrue(outcomes.Restart.Tried);
        Assert.AreEqual(false, outcomes.Restart.Ok);
        Assert.AreEqual(code, outcomes.Restart.ExitCode);
        StringAssert.Contains(outcomes.Restart.Detail, expected);
        Assert.DoesNotContain("self-check did not pass", outcomes.Restart.Detail);
    }

    // A wait that throws is not a pass: the second copy is stopped, and the
    // exception is named.
    [TestMethod]
    public void ARestartWhoseWaitThrowsIsNotOkAndIsStopped()
    {
        var actions = new StandInActions { WaitError = new InvalidOperationException("No process is associated with this object.") };
        (PlatformCheckOutcomes outcomes, _) = Run(actions, Target());
        Assert.IsTrue(outcomes.Restart.Tried);
        Assert.AreEqual(false, outcomes.Restart.Ok);
        Assert.AreEqual("System.InvalidOperationException", outcomes.Restart.ExceptionType);
        Assert.AreEqual("No process is associated with this object.", outcomes.Restart.ExceptionMessage);
        Assert.AreEqual("PaperRoute.Desktop", outcomes.Restart.Program);
        Assert.IsNull(outcomes.Restart.ExitCode);
        Assert.AreEqual("Waiting for the second copy from PaperRoute.Desktop failed; afterwards it was stopped.", outcomes.Restart.Detail);
        Assert.IsTrue(actions.Stopped, "The second copy was stopped.");
        Assert.IsTrue(actions.Disposed, "The second copy was let go.");
    }

    // At the limit, a Stop that throws is reported, its message scrubbed.
    [TestMethod]
    public void ARestartThatCannotBeStoppedAtTheLimitSaysSo()
    {
        string program = Path.Combine(_root, "trial", "PaperRoute.Desktop");
        var actions = new StandInActions
        {
            Exits = false,
            StopError = new InvalidOperationException($"Cannot stop {program}."),
        };
        var scrubber = new PrivacyScrubber(program, null, null, null, null);
        (PlatformCheckOutcomes outcomes, _) = Run(actions, RestartTarget.For(null, null, program, _ => true), TimeSpan.FromSeconds(90), scrubber);
        Assert.AreEqual(false, outcomes.Restart.Ok);
        Assert.AreEqual(true, outcomes.Restart.StoppedAfterLimit);
        Assert.AreEqual(
            "The second copy started from PaperRoute.Desktop did not exit within 90 s and could not be stopped (InvalidOperationException: Cannot stop PaperRoute.Desktop.).",
            outcomes.Restart.Detail);
        Assert.DoesNotContain(_root, Json(outcomes));
        Assert.IsTrue(actions.Disposed);
    }

    [TestMethod]
    public void ARestartThatDoesNotExitWithinTheLimitIsStopped()
    {
        var hanging = new StandInActions { Exits = false };
        (PlatformCheckOutcomes outcomes, _) = Run(hanging, Target(), TimeSpan.FromSeconds(90));
        Assert.IsTrue(hanging.Stopped, "The second copy was stopped.");
        Assert.AreEqual(TimeSpan.FromSeconds(90), hanging.WaitedFor, "The wait used the limit given.");
        Assert.IsTrue(outcomes.Restart.Tried);
        Assert.AreEqual(false, outcomes.Restart.Ok);
        Assert.AreEqual(true, outcomes.Restart.StoppedAfterLimit);
        Assert.IsNull(outcomes.Restart.ExitCode);
        Assert.AreEqual("The second copy started from PaperRoute.Desktop did not exit within 90 s and was stopped.", outcomes.Restart.Detail);

        using JsonDocument document = JsonDocument.Parse(Json(outcomes));
        Assert.IsTrue(document.RootElement.GetProperty("restart").GetProperty("stoppedAfterLimit").GetBoolean());
    }

    [TestMethod]
    public void ARestartThatCannotStartSaysWhyWithoutTheProgramsFolder()
    {
        string folder = Path.Combine(_root, "trial");
        string program = Path.Combine(folder, "PaperRoute.Desktop");
        var actions = new StandInActions
        {
            StartError = new Win32Exception(2, $"An error occurred trying to start process '{program}' with working directory '{_root}'. No such file or directory"),
        };
        var scrubber = new PrivacyScrubber(program, _root, null, null, null);
        (PlatformCheckOutcomes outcomes, _) = Run(actions, RestartTarget.For(null, null, program, _ => true), scrubber: scrubber);
        Assert.IsTrue(outcomes.Restart.Tried);
        Assert.AreEqual(false, outcomes.Restart.Ok);
        Assert.AreEqual("The second copy could not be started from PaperRoute.Desktop.", outcomes.Restart.Detail);
        Assert.AreEqual("System.ComponentModel.Win32Exception", outcomes.Restart.ExceptionType);
        Assert.AreEqual(
            "An error occurred trying to start process 'PaperRoute.Desktop' with working directory '<working folder>'. No such file or directory",
            outcomes.Restart.ExceptionMessage);
        Assert.AreEqual("PaperRoute.Desktop", outcomes.Restart.Program);

        string json = Json(outcomes);
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement restart = document.RootElement.GetProperty("restart");
            Assert.AreEqual("System.ComponentModel.Win32Exception", restart.GetProperty("exceptionType").GetString());
            Assert.AreEqual(
                "An error occurred trying to start process 'PaperRoute.Desktop' with working directory '<working folder>'. No such file or directory",
                restart.GetProperty("exceptionMessage").GetString());
        }

        Assert.DoesNotContain(folder, json);
        Assert.DoesNotContain(_root, json);
        Assert.DoesNotContain(JsonEncodedText.Encode(_root).ToString(), json);
    }

    // The second copy has no window (RestartArguments: the self-check), and
    // the notice must say so: on 2026-11-21 a person running the check by hand
    // reported "No second window opened" as a problem although the step passed.
    [TestMethod]
    public void TheRestartNoticeSaysThatNoSecondWindowOpens()
    {
        StringAssert.Contains(PlatformCheck.RestartNotice, "No second window opens");
        CollectionAssert.AreEqual(new[] { "--self-check" }, PlatformCheck.RestartArguments.ToArray(),
            "The notice is true only while the second copy runs a mode without a window.");
    }

    [TestMethod]
    public void ARestartThroughTheDotnetHostIsNotTriedAndSaysSo()
    {
        var actions = new StandInActions();
        (PlatformCheckOutcomes outcomes, List<string> said) = Run(actions, RestartTarget.For(null, null, "/usr/share/dotnet/dotnet", _ => true));
        Assert.IsFalse(outcomes.Restart.Tried);
        Assert.IsNull(outcomes.Restart.Ok);
        StringAssert.Contains(outcomes.Restart.Detail, "dotnet host");
        Assert.IsFalse(actions.Log.Any(item => item.StartsWith("start", StringComparison.Ordinal)), "Nothing was started.");
        Assert.DoesNotContain(PlatformCheck.RestartNotice, said, "The person is not told of a restart that is not tried.");

        using JsonDocument document = JsonDocument.Parse(Json(outcomes));
        JsonElement restart = document.RootElement.GetProperty("restart");
        Assert.IsFalse(restart.GetProperty("tried").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, restart.GetProperty("ok").ValueKind);
        Assert.IsFalse(restart.TryGetProperty("program", out _), "No program was started, so none is named.");
    }

    // ---- The JSON ----

    [TestMethod]
    public void TheJsonHoldsTheFactsAndEachOutcomeFieldByField()
    {
        var environment = new Dictionary<string, string?>
        {
            ["APPIMAGE"] = "/home/someone-secret/Apps/Trial.AppImage",
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-secret-0",
            ["DISPLAY"] = ":secret.9",
        };
        PlatformFacts facts = PlatformFacts.Read(name => environment.GetValueOrDefault(name), 1.25, "Dark", "NoPreference")
            with { TrialVersion = "0.1.0-trial+0123abc", Build = "Release" };
        var actions = new StandInActions { Chosen = [ChosenName, "two.txt"], Launched = false, ExitCode = 1 };
        (PlatformCheckOutcomes outcomes, _) = Run(actions, Target());
        var report = new PlatformCheckReport { TakenAt = new DateTime(2026, 11, 21, 9, 30, 0), Facts = facts, Outcomes = outcomes };

        string path = Path.Combine(_root, "platform.json");
        report.WriteJson(path);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "No byte-order mark.");
        string text = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;

        Assert.AreEqual("platform-check", root.GetProperty("mode").GetString());
        Assert.AreEqual(1, root.GetProperty("formatVersion").GetInt32());
        Assert.AreEqual("2026-11-21T09:30:00.0000000", root.GetProperty("takenAt").GetString());
        Assert.AreEqual("0.1.0-trial+0123abc", root.GetProperty("trialVersion").GetString());
        Assert.AreEqual("Release", root.GetProperty("build").GetString());
        Assert.AreEqual(System.Runtime.InteropServices.RuntimeInformation.OSDescription, root.GetProperty("operatingSystem").GetString());
        Assert.AreEqual(System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, root.GetProperty("runtimeIdentifier").GetString());
        Assert.AreEqual(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), root.GetProperty("processArchitecture").GetString());
        Assert.IsTrue(root.GetProperty("appImageSet").GetBoolean());
        Assert.AreEqual("wayland", root.GetProperty("xdgSessionType").GetString());
        Assert.IsTrue(root.GetProperty("waylandDisplaySet").GetBoolean());
        Assert.IsTrue(root.GetProperty("displaySet").GetBoolean());
        Assert.AreEqual(1.25, root.GetProperty("renderScaling").GetDouble());
        Assert.AreEqual("Dark", root.GetProperty("themeVariant").GetString());
        Assert.AreEqual("NoPreference", root.GetProperty("contrastPreference").GetString());
        Assert.IsFalse(root.TryGetProperty("error", out _), "No error when the check ran to its end.");

        JsonElement dialog = root.GetProperty("fileDialog");
        Assert.IsTrue(dialog.GetProperty("tried").GetBoolean());
        Assert.IsTrue(dialog.GetProperty("ok").GetBoolean());
        Assert.AreEqual(2, dialog.GetProperty("itemsChosen").GetInt32());
        Assert.AreEqual("The file dialog opened and came back with 2 item(s) chosen. Nothing chosen was opened or read.", dialog.GetProperty("detail").GetString());
        Assert.IsGreaterThanOrEqualTo(0, dialog.GetProperty("milliseconds").GetDouble());
        Assert.IsFalse(dialog.TryGetProperty("exceptionType", out _));

        JsonElement launcher = root.GetProperty("launcher");
        Assert.IsTrue(launcher.GetProperty("tried").GetBoolean());
        Assert.IsFalse(launcher.GetProperty("ok").GetBoolean());
        Assert.AreEqual("The system reports that it could not open https://example.org.", launcher.GetProperty("detail").GetString());
        Assert.IsGreaterThanOrEqualTo(0, launcher.GetProperty("milliseconds").GetDouble());

        JsonElement restart = root.GetProperty("restart");
        Assert.IsTrue(restart.GetProperty("tried").GetBoolean());
        Assert.IsFalse(restart.GetProperty("ok").GetBoolean());
        Assert.AreEqual(1, restart.GetProperty("exitCode").GetInt32());
        Assert.IsFalse(restart.GetProperty("stoppedAfterLimit").GetBoolean());
        Assert.AreEqual("PaperRoute.Desktop", restart.GetProperty("program").GetString());
        Assert.AreEqual("process path", restart.GetProperty("startedFrom").GetString());

        // Only whether the variables are set, never their values; no chosen file.
        foreach (string secret in new[] { "someone-secret", "Trial.AppImage", "wayland-secret-0", ":secret.9", ChosenName, "two.txt" })
        {
            Assert.DoesNotContain(secret, text, secret);
        }

        // A check that broke names its error and has no outcomes.
        var broken = new PlatformCheckReport { TakenAt = report.TakenAt, Facts = facts, Error = "InvalidOperationException: the load failed" };
        using JsonDocument brokenDocument = JsonDocument.Parse(broken.ToJson());
        Assert.AreEqual("InvalidOperationException: the load failed", brokenDocument.RootElement.GetProperty("error").GetString());
        Assert.IsFalse(brokenDocument.RootElement.TryGetProperty("fileDialog", out _));

        // No session type off Linux is written as null.
        PlatformFacts bare = PlatformFacts.Read(_ => null, 1, "Light", "High");
        using JsonDocument bareDocument = JsonDocument.Parse(new PlatformCheckReport { TakenAt = report.TakenAt, Facts = bare, Outcomes = outcomes }.ToJson());
        Assert.AreEqual(JsonValueKind.Null, bareDocument.RootElement.GetProperty("xdgSessionType").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, bareDocument.RootElement.GetProperty("trialVersion").ValueKind, "A version nobody gave is written as null.");
        Assert.AreEqual(JsonValueKind.Null, bareDocument.RootElement.GetProperty("build").ValueKind);
        Assert.IsFalse(bareDocument.RootElement.GetProperty("appImageSet").GetBoolean());
        Assert.IsFalse(bareDocument.RootElement.GetProperty("displaySet").GetBoolean());
    }

    [TestMethod]
    public void TheScrubberTakesOutFoldersAndNames()
    {
        var scrubber = new PrivacyScrubber(
            "/home/pat-example/Apps/Trial/PaperRoute.Desktop",
            "/home/pat-example/Work",
            "/home/pat-example",
            "pat-example",
            "pat-laptop-77");
        Assert.AreEqual(
            "Could not start 'PaperRoute.Desktop' in '<working folder>'; see <home>/.config on <computer> for <user>; also <program folder>/x.",
            scrubber.Scrub("Could not start '/home/pat-example/Apps/Trial/PaperRoute.Desktop' in '/home/pat-example/Work'; see /home/pat-example/.config on PAT-LAPTOP-77 for pat-example; also /home/pat-example/Apps/Trial/x."));
        Assert.AreEqual(string.Empty, scrubber.Scrub(null));

        // This process's own names never reach a scrubbed message.
        PrivacyScrubber here = PrivacyScrubber.ForThisProcess(Environment.ProcessPath);
        string scrubbed = here.Scrub($"{Environment.MachineName} {Environment.UserName} {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");
        Assert.DoesNotContain(Environment.MachineName, scrubbed, StringComparison.OrdinalIgnoreCase);
        if (Environment.UserName.Length >= 2)
        {
            Assert.DoesNotContain(Environment.UserName, scrubbed, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- In the trial's own window (headless) ----

    // PlatformCheckWindow in the app's window over a library in memory, with
    // stand-ins: the notice strip shows each sentence while its action runs,
    // the same lines go to the output, the JSON is written, the window closes
    // and the exit code is 0; a JSON that cannot be written gives 1.
    [TestMethod]
    public void TheWindowTellsThePersonWritesTheJsonAndCloses()
    {
        // A library of three that has not loaded yet: the mode loads it, as
        // the window's usual opening does without the mode.
        var session = new MemorySession([Papers.Paper("One"), Papers.Paper("Two"), Papers.Paper("Three")], Papers.Today);
        string path = Path.Combine(_root, "platform.json");
        (int code, List<string> shown, string output, bool closed, string railBefore, string railAfter, string railDuringDialog) = Headless.Run(() =>
        {
            ShellViewModel shell = App.CreateShell(session);
            var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            string railBefore = shell.RailSummary;
            TextBlock notice = window.FindControl<TextBlock>("NoticeText") ?? throw new AssertFailedException("No notice strip.");
            var shown = new List<string>();
            string railDuringDialog = string.Empty;
            var actions = new StandInActions { Chosen = [ChosenName] };
            actions.Seen = () =>
            {
                shown.Add(notice.Text ?? string.Empty);
                if (shown.Count == 1)
                {
                    railDuringDialog = shell.RailSummary;
                }
            };
            bool closed = false;
            window.Closed += (_, _) => closed = true;
            using var output = new StringWriter();
            Task<int> run = PlatformCheckWindow.RunAsync(window, shell, path, actions, Target(),
                new PrivacyScrubber(null, null, null, null, null), output, TimeSpan.FromSeconds(5), _ => null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(run.IsCompleted, "With stand-ins that answer at once, the check ends at once.");
            return (run.Result, shown, output.ToString(), closed, railBefore, shell.RailSummary, railDuringDialog);
        });

        Assert.AreEqual(string.Empty, railBefore, "The library had not loaded before the check.");
        Assert.AreEqual("3 manuscripts", railDuringDialog, "The Board showed the library behind the dialog.");
        Assert.AreEqual("3 manuscripts", railAfter);
        Assert.AreEqual(0, code);
        CollectionAssert.AreEqual(new[] { PlatformCheck.DialogNotice, PlatformCheck.LauncherNotice, PlatformCheck.RestartNotice }, shown,
            "The notice strip showed each sentence while its action ran.");
        CollectionAssert.AreEqual(
            new[] { PlatformCheck.DialogNotice, PlatformCheck.LauncherNotice, PlatformCheck.RestartNotice, PlatformCheck.DoneNotice },
            output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries));
        Assert.IsTrue(closed, "The window was closed.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.AreEqual("platform-check", document.RootElement.GetProperty("mode").GetString());
        Assert.IsTrue(document.RootElement.GetProperty("renderScaling").GetDouble() > 0, "The window's render scaling is read.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(PlatformCheckWindow.TrialVersion), "The trial's assembly carries its version.");
        Assert.AreEqual(PlatformCheckWindow.TrialVersion, document.RootElement.GetProperty("trialVersion").GetString(),
            "The window's JSON says which trial ran.");
        Assert.AreEqual(PaperRoute.Desktop.Measurement.BoardMeasurement.Build, document.RootElement.GetProperty("build").GetString());
        Assert.DoesNotContain(ChosenName, File.ReadAllText(path));

        // A folder that went away before the write: the check broke, exit 1.
        string gone = Path.Combine(_root, "gone", "platform.json");
        int failed = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(new MemorySession([], Papers.Today));
            Task<int> run = PlatformCheckWindow.RunAsync(board.Window, board.Shell, gone, new StandInActions(), Target(),
                new PrivacyScrubber(null, null, null, null, null), TextWriter.Null, TimeSpan.FromSeconds(5), _ => null);
            Dispatcher.UIThread.RunJobs();
            return run.IsCompleted ? run.Result : -1;
        });
        Assert.AreEqual(1, failed);
        Assert.IsFalse(File.Exists(gone));
    }

    // The person closes the window while the file dialog has not come back:
    // the JSON names the step and the error, the steps not reached are not
    // tried, and when the dialog comes back later nothing more happens.
    [TestMethod]
    public void ClosingTheWindowDuringTheDialogWritesWhatIsKnown()
    {
        string path = Path.Combine(_root, "platform.json");
        var pending = new TaskCompletionSource<IReadOnlyList<string>>();
        var actions = new StandInActions { PendingDialog = pending };
        (int early, int runCode, string before, string after) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(new MemorySession([], Papers.Today));
            var check = new PlatformCheckWindow(board.Window, board.Shell, path, actions, Target(),
                new PrivacyScrubber(null, null, null, null, null), TextWriter.Null, TimeSpan.FromSeconds(5), _ => null);
            Task<int> run = check.RunAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(run.IsCompleted, "The dialog has not come back.");
            int early = check.CloseEarly();
            Assert.IsTrue(check.Written);
            string before = File.ReadAllText(path);
            Assert.AreEqual(1, check.CloseEarly(), "A second close changes nothing.");

            // The dialog comes back after all: no further step, no second write.
            pending.SetResult([ChosenName]);
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(run.IsCompleted);
            return (early, run.Result, before, File.ReadAllText(path));
        });

        Assert.AreEqual(1, early);
        Assert.AreEqual(1, runCode);
        Assert.AreEqual(before, after, "The JSON was written once.");
        CollectionAssert.AreEqual(new[] { "dialog" }, actions.Log, "Nothing after the dialog was tried.");

        using JsonDocument document = JsonDocument.Parse(before);
        JsonElement root = document.RootElement;
        Assert.AreEqual(PlatformCheckReport.ClosedEarlyError, root.GetProperty("error").GetString());
        Assert.AreEqual("fileDialog", root.GetProperty("stepRunning").GetString());
        JsonElement dialog = root.GetProperty("fileDialog");
        Assert.IsTrue(dialog.GetProperty("tried").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, dialog.GetProperty("ok").ValueKind);
        Assert.AreEqual("Still running when the window was closed: no outcome came back.", dialog.GetProperty("detail").GetString());
        foreach (string step in new[] { "launcher", "restart" })
        {
            Assert.IsFalse(root.GetProperty(step).GetProperty("tried").GetBoolean(), step);
            Assert.AreEqual("Not reached: the window was closed before this step.", root.GetProperty(step).GetProperty("detail").GetString(), step);
        }
    }

    // Closed while the second copy runs: it is stopped, and the outcomes
    // already gathered are kept.
    [TestMethod]
    public void ClosingTheWindowDuringTheRestartStopsTheSecondCopy()
    {
        string path = Path.Combine(_root, "platform.json");
        var pending = new TaskCompletionSource<bool>();
        var actions = new StandInActions { Chosen = [ChosenName], PendingWait = pending };
        int runCode = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(new MemorySession([], Papers.Today));
            var check = new PlatformCheckWindow(board.Window, board.Shell, path, actions, Target(),
                new PrivacyScrubber(null, null, null, null, null), TextWriter.Null, TimeSpan.FromSeconds(5), _ => null);
            Task<int> run = check.RunAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(run.IsCompleted, "The second copy has not ended.");
            Assert.AreEqual(1, check.CloseEarly());
            Assert.IsTrue(actions.Stopped, "The second copy was stopped at once.");

            // Stopping it ends its wait.
            pending.SetResult(true);
            Dispatcher.UIThread.RunJobs();
            return run.IsCompleted ? run.Result : -1;
        });

        Assert.AreEqual(1, runCode);
        Assert.IsTrue(actions.Disposed, "The second copy was let go.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        Assert.AreEqual("restart", root.GetProperty("stepRunning").GetString());
        Assert.AreEqual(PlatformCheckReport.ClosedEarlyError, root.GetProperty("error").GetString());
        Assert.IsTrue(root.GetProperty("fileDialog").GetProperty("ok").GetBoolean(), "The dialog's outcome was kept.");
        Assert.AreEqual(1, root.GetProperty("fileDialog").GetProperty("itemsChosen").GetInt32());
        Assert.IsTrue(root.GetProperty("launcher").GetProperty("ok").GetBoolean(), "The launcher's outcome was kept.");
        JsonElement restart = root.GetProperty("restart");
        Assert.IsTrue(restart.GetProperty("tried").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, restart.GetProperty("ok").ValueKind);
        Assert.AreEqual(
            "Still running when the window was closed: no outcome came back. The second copy was still running; it was stopped.",
            restart.GetProperty("detail").GetString());
        Assert.DoesNotContain(ChosenName, File.ReadAllText(path));
    }

    // ---- Whether the system's text size reaches the window ----

    // The check records what the system says of its text size, the window's
    // render scaling, and the heights in device pixels of a 13 px body line
    // in Inter and a fixed box 100 layout units high (the harness design,
    // part 3.5), so a run at a larger system text size tells which happened:
    // text grows (the line grows, the box stays), everything grows (both
    // grow with the render scaling), or nothing changes. Here with fake
    // readers, in the trial's window and
    // in a window whose render scaling a test sets; then a reader that
    // fails or is missing, and the real reader's reading of what Windows'
    // registry and Linux's tools give. Running the real reader is left to
    // the Xvfb job and the guest runs.
    [TestMethod]
    public void ThePlatformCheckRecordsTextScaleScalingAndLineHeight()
    {
        string path = Path.Combine(_root, "platform.json");
        var fake = new SystemTextSetting(1.5, "fake: Text size 150 %", 144);
        (int code, PlatformFacts atOne, PlatformFacts atTwo, PlatformFacts failing, PlatformFacts missing, double interLine, double interLineAt16) = Headless.Run(() =>
        {
            double inter = InterLineHeight(PlatformCheckWindow.BodyLineFontSize);
            double interAt16 = InterLineHeight(16);
            int exit;
            using (BoardWindow board = BoardWindow.Show(new MemorySession([], Papers.Today)))
            {
                Task<int> run = PlatformCheckWindow.RunAsync(board.Window, board.Shell, path, new StandInActions(), Target(),
                    new PrivacyScrubber(null, null, null, null, null), TextWriter.Null, TimeSpan.FromSeconds(5), _ => null, systemText: () => fake);
                Dispatcher.UIThread.RunJobs();
                exit = run.IsCompleted ? run.Result : -1;
            }

            var window = new Window { Width = 400, Height = 200 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                PlatformFacts one = PlatformCheckWindow.FactsFor(window, _ => null, () => fake);
                window.SetRenderScaling(2);
                PlatformFacts two = PlatformCheckWindow.FactsFor(window, _ => null, () => fake);
                PlatformFacts fails = PlatformCheckWindow.FactsFor(window, _ => null, () => throw new InvalidOperationException("No settings here."));
                PlatformFacts none = PlatformCheckWindow.FactsFor(window, _ => null);
                return (exit, one, two, fails, none, inter, interAt16);
            }
            finally
            {
                window.Close();
            }
        });

        // The trial's window writes the four fields into its JSON.
        Assert.AreEqual(0, code, "The check ran to its end.");
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path)))
        {
            JsonElement root = document.RootElement;
            Assert.AreEqual(1.5, root.GetProperty("systemTextScale").GetDouble(), "The system's text scale, from the reader.");
            Assert.AreEqual("fake: Text size 150 %", root.GetProperty("systemTextScaleSource").GetString());
            Assert.AreEqual(144, root.GetProperty("xftDpi").GetDouble());
            Assert.AreEqual(1.0, root.GetProperty("renderScaling").GetDouble(), "Headless renders at scale 1.");
            Assert.AreEqual(Math.Round(interLine, 2), root.GetProperty("bodyLinePixels").GetDouble(),
                "At scale 1 the body line is as high as a 13 px line of Inter, the window's font.");
            Assert.AreEqual(PlatformCheckWindow.BoxSize, root.GetProperty("boxPixels").GetDouble(), "At scale 1 the fixed box is 100 pixels high.");
        }

        // The body line is a 13 px line of Inter: the same text measured here
        // in Inter at 13, and not at 16 (the reference has teeth).
        Assert.IsTrue(interLine > PlatformCheckWindow.BodyLineFontSize && interLine < 2 * PlatformCheckWindow.BodyLineFontSize,
            $"A 13 px line of Inter lays out taller than 13 and shorter than 26, not {interLine}.");
        Assert.AreNotEqual(interLine, interLineAt16, "A 16 px line of Inter is as high as a 13 px one, so the comparison shows nothing.");
        Assert.AreEqual(interLine, atOne.BodyLinePixels, "The body line is not a 13 px line of Inter.");

        // A render scaling of 2 is recorded, and both heights double in
        // pixels: that is "everything grows", which the fixed box shows.
        Assert.AreEqual(1.0, atOne.RenderScaling);
        Assert.AreEqual(2.0, atTwo.RenderScaling, "The window's render scaling is read when the facts are taken.");
        Assert.AreEqual((double?)PlatformCheckWindow.BoxSize, atOne.BoxPixels, "At scale 1 the fixed box is 100 pixels high.");
        Assert.AreEqual((double?)(2 * PlatformCheckWindow.BoxSize), atTwo.BoxPixels, "At scale 2 the fixed box is 200 pixels high.");
        Assert.AreEqual(2 * atOne.BodyLinePixels, atTwo.BodyLinePixels, "At scale 2 the body line is twice as many pixels high.");
        Assert.AreEqual(fake, atTwo.SystemText);

        // A reader that throws or is missing is a fact, never a broken check.
        Assert.AreEqual(new SystemTextSetting(null, "not read: the reader failed (InvalidOperationException)", null), failing.SystemText);
        Assert.AreEqual(new SystemTextSetting(null, "not read: no reader was given", null), missing.SystemText);
        Assert.AreEqual((double?)(2 * PlatformCheckWindow.BoxSize), failing.BoxPixels, "The heights are still recorded.");
        using (JsonDocument document = JsonDocument.Parse(new PlatformCheckReport { TakenAt = new DateTime(2026, 11, 27), Facts = failing }.ToJson()))
        {
            Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("systemTextScale").ValueKind);
            Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("xftDpi").ValueKind);
            Assert.AreEqual("not read: the reader failed (InvalidOperationException)", document.RootElement.GetProperty("systemTextScaleSource").GetString());
        }

        // Facts taken outside a window carry no text-size fields: each is null.
        using (JsonDocument document = JsonDocument.Parse(Json(Run(new StandInActions(), Target()).Outcomes)))
        {
            foreach (string name in new[] { "systemTextScale", "systemTextScaleSource", "xftDpi", "bodyLinePixels", "boxPixels" })
            {
                Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty(name).ValueKind, name);
            }
        }

        // Windows: TextScaleFactor is a whole percentage, left out at 100 %.
        Assert.AreEqual(1.5, SystemTextSettings.FromWindowsValue(150).Scale);
        StringAssert.StartsWith(SystemTextSettings.FromWindowsValue(150).Source, "Windows: TextScaleFactor under HKEY_CURRENT_USER\\Software\\Microsoft\\Accessibility");
        Assert.AreEqual(1.0, SystemTextSettings.FromWindowsValue(null).Scale, "Windows leaves TextScaleFactor out at its default, 100 %.");
        StringAssert.Contains(SystemTextSettings.FromWindowsValue(null).Source, "is not set");
        Assert.IsNull(SystemTextSettings.FromWindowsValue("150").Scale, "A value of another type is not read as a percentage.");
        Assert.IsNull(SystemTextSettings.FromWindowsValue(0).Scale);
        Assert.IsNull(SystemTextSettings.FromWindowsValue(150).XftDpi, "Windows has no Xft.dpi.");

        // Linux: gsettings writes the factor; xrdb -query writes the X resources.
        SystemTextSetting gnome = SystemTextSettings.FromLinuxOutput("1.5\n", "Xft.antialias:\t1\nXft.dpi:\t144\nXft.hinting:\t1\n");
        Assert.AreEqual(1.5, gnome.Scale);
        Assert.AreEqual(144.0, gnome.XftDpi);
        Assert.AreEqual("GNOME: text-scaling-factor (gsettings, org.gnome.desktop.interface); Xft.dpi from xrdb -query", gnome.Source);
        SystemTextSetting bare = SystemTextSettings.FromLinuxOutput(null, "Xft.antialias:\t1\n");
        Assert.IsNull(bare.Scale);
        Assert.IsNull(bare.XftDpi, "No Xft.dpi line: not set.");
        Assert.AreEqual("GNOME: text-scaling-factor not read (gsettings did not answer); Xft.dpi not set (xrdb -query)", bare.Source);
        SystemTextSetting odd = SystemTextSettings.FromLinuxOutput("1,5", null);
        Assert.IsNull(odd.Scale, "Only a number written the invariant way is read.");
        Assert.AreEqual("GNOME: gsettings gave no number for text-scaling-factor; Xft.dpi not read (xrdb did not answer)", odd.Source);
    }

    // The laid-out height of the check's body line in Inter, the app's one
    // embedded font, at the size given; on the UI thread.
    private static double InterLineHeight(double size)
    {
        var line = new TextBlock { Text = PlatformCheckWindow.BodyLineText, FontSize = size, FontFamily = new Avalonia.Media.FontFamily("fonts:Inter#Inter") };
        line.Measure(Avalonia.Size.Infinity);
        return line.DesiredSize.Height;
    }

    // ---- The real start of a second program (it opens nothing) ----

    // AvaloniaPlatformActions.Start is what the restart runs for real. Here
    // it starts the trial's own "--self-check" through the dotnet host, as
    // SessionStartupProcessTests does, waits for it and reads its exit code.
    [TestMethod]
    public void TheRealStartRunsASecondCopyAndReadsItsExitCode()
    {
        using IStartedProgram started = AvaloniaPlatformActions.Start(
            SessionStartupProcessTests.DotnetHost(),
            [SessionStartupProcessTests.DesktopAssemblyPath(), "--self-check"]);
        bool exited = started.WaitForExitAsync(TimeSpan.FromMinutes(2)).GetAwaiter().GetResult();
        if (!exited)
        {
            started.Stop();
        }

        Assert.IsTrue(exited, "The second copy's self-check ended within two minutes.");
        Assert.AreEqual(0, started.ExitCode, "The second copy's self-check passed.");
    }

    // A program that outlives its limit: the wait says it has not ended,
    // Stop ends it, and a second wait sees it gone. The program writes
    // nothing: ping to this computer on Windows, sleep elsewhere.
    [TestMethod]
    public void TheRealStopEndsAProgramThatOutlivesItsLimit()
    {
        (string file, string[] arguments) = OperatingSystem.IsWindows()
            ? (Path.Combine(Environment.SystemDirectory, "ping.exe"), new[] { "-n", "120", "127.0.0.1" })
            : ("sleep", new[] { "120" });
        using IStartedProgram started = AvaloniaPlatformActions.Start(file, arguments);
        bool endedEarly;
        try
        {
            endedEarly = started.WaitForExitAsync(TimeSpan.FromMilliseconds(300)).GetAwaiter().GetResult();
        }
        finally
        {
            started.Stop();
        }

        Assert.IsFalse(endedEarly, "It runs for two minutes, so it had not ended at the limit.");
        Assert.IsTrue(started.WaitForExitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult(), "Once stopped, it is gone.");
    }

    // A program file that is not there: Start throws what the check's flow
    // expects on Windows and Linux alike, and the scrubbed message names
    // neither the file's folder nor the working folder.
    [TestMethod]
    public void TheRealStartOfAMissingProgramThrowsWhatTheCheckExpects()
    {
        string missing = Path.Combine(_root, "no-such-program");
        Win32Exception thrown = Assert.ThrowsExactly<Win32Exception>(() => AvaloniaPlatformActions.Start(missing, ["--self-check"]));
        string scrubbed = PrivacyScrubber.ForThisProcess(missing).Scrub(thrown.Message);
        Assert.DoesNotContain(_root, scrubbed, StringComparison.OrdinalIgnoreCase, thrown.Message);
        Assert.DoesNotContain(Path.TrimEndingDirectorySeparator(Environment.CurrentDirectory), scrubbed, StringComparison.OrdinalIgnoreCase, thrown.Message);
    }

    // ---- Helpers ----

    private static RestartTarget Target() => RestartTarget.For(null, null, "/opt/trial/PaperRoute.Desktop", _ => true);

    // Runs the flow with no synchronization context: the stand-ins answer
    // at once, so nothing waits.
    private static (PlatformCheckOutcomes Outcomes, List<string> Said) Run(
        StandInActions actions, RestartTarget target, TimeSpan? limit = null, PrivacyScrubber? scrubber = null)
    {
        var said = new List<string>();
        var check = new PlatformCheck(actions, target, scrubber ?? new PrivacyScrubber(null, null, null, null, null), text =>
        {
            said.Add(text);
            actions.Note("say: " + text);
        }, limit ?? TimeSpan.FromSeconds(5));
        PlatformCheckOutcomes outcomes = check.RunAsync().GetAwaiter().GetResult();
        return (outcomes, said);
    }

    private static string Json(PlatformCheckOutcomes outcomes) => new PlatformCheckReport
    {
        TakenAt = new DateTime(2026, 11, 21),
        Facts = PlatformFacts.Read(_ => null, 1, "Light", "NoPreference"),
        Outcomes = outcomes,
    }.ToJson();

    // Stand-ins for the dialog, the launcher and the started program; each
    // call is logged in order.
    private sealed class StandInActions : IPlatformActions
    {
        public List<string> Log { get; } = [];

        public bool CanOpen { get; init; } = true;

        public IReadOnlyList<string> Chosen { get; init; } = [];

        public Exception? DialogError { get; init; }

        public bool Launched { get; init; } = true;

        public Exception? LaunchError { get; init; }

        public Exception? StartError { get; init; }

        public Exception? WaitError { get; init; }

        public Exception? StopError { get; init; }

        // A dialog that has not come back (the test completes it, or not).
        public TaskCompletionSource<IReadOnlyList<string>>? PendingDialog { get; init; }

        // A wait that has not ended (the test completes it, or not).
        public TaskCompletionSource<bool>? PendingWait { get; init; }

        public bool Exits { get; init; } = true;

        public int ExitCode { get; init; }

        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public TimeSpan WaitedFor { get; private set; }

        // Called as each action runs (the window test reads the notice strip).
        public Action? Seen { get; set; }

        public bool CanOpenFileDialog => CanOpen;

        public void Note(string item) => Log.Add(item);

        public Task<IReadOnlyList<string>> OpenFileDialogAsync()
        {
            Add("dialog");
            Seen?.Invoke();
            if (PendingDialog is not null)
            {
                return PendingDialog.Task;
            }

            return DialogError is null ? Task.FromResult(Chosen) : Task.FromException<IReadOnlyList<string>>(DialogError);
        }

        public Task<bool> LaunchAsync(Uri address)
        {
            Add("launch " + address.AbsoluteUri);
            Seen?.Invoke();
            return LaunchError is null ? Task.FromResult(Launched) : Task.FromException<bool>(LaunchError);
        }

        public IStartedProgram StartProgram(string fileName, IReadOnlyList<string> arguments)
        {
            Add("start " + RestartTarget.FileNameOf(fileName) + " " + string.Join(" ", arguments));
            Seen?.Invoke();
            if (StartError is not null)
            {
                throw StartError;
            }

            return new StandInProgram(this);
        }

        private void Add(string item) => Log.Add(item);

        private sealed class StandInProgram(StandInActions owner) : IStartedProgram
        {
            public int ExitCode => owner.Exits ? owner.ExitCode : throw new InvalidOperationException("It has not exited.");

            public Task<bool> WaitForExitAsync(TimeSpan limit)
            {
                owner.Add("wait");
                owner.WaitedFor = limit;
                if (owner.WaitError is not null)
                {
                    return Task.FromException<bool>(owner.WaitError);
                }

                return owner.PendingWait?.Task ?? Task.FromResult(owner.Exits);
            }

            public void Stop()
            {
                owner.Add("stop");
                if (owner.StopError is not null)
                {
                    throw owner.StopError;
                }

                owner.Stopped = true;
            }

            public void Dispose() => owner.Disposed = true;
        }
    }
}
