using System.Globalization;
using System.Text.Json;
using Avalonia.Headless;
using Avalonia.Threading;
using PaperRoute.Desktop.Measurement;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// "--measure <file>.json" (d1 7.3), proved without opening a window: how the
// command line reads it, which files it refuses before anything opens, the
// JSON it writes (the same writer the budget tests use), and the wait for a
// real window's frames, run on the headless platform's render timer. The
// real-window run itself is left to a person.
[TestClass]
public sealed class MeasureModeTests : StorageCheckedTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialMeasure-" + Guid.NewGuid().ToString("N"));
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
            // Left for Windows' temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for Windows' temporary-file clean-up.
        }
    }

    // ---- The command line ----

    [TestMethod]
    public void MeasureTakesTheFileAfterIt()
    {
        Assert.IsTrue(TrialOptions.TryParse([], out TrialOptions? none, out _));
        Assert.IsNull(none.MeasureFile);

        Assert.IsTrue(TrialOptions.TryParse(["--measure", @"C:\Measurements\board.json"], out TrialOptions? example, out string? error), error);
        Assert.AreEqual(@"C:\Measurements\board.json", example.MeasureFile);
        Assert.IsNull(example.SnapshotFrom);
        Assert.IsFalse(example.SelfCheck);

        Assert.IsTrue(TrialOptions.TryParse(
            ["--snapshot-from", @"C:\Libraries\Large", "--measure", @"C:\Measurements\board.json", "--today", "2026-10-16"],
            out TrialOptions? snapshot, out error), error);
        Assert.AreEqual(@"C:\Libraries\Large", snapshot.SnapshotFrom);
        Assert.AreEqual(@"C:\Measurements\board.json", snapshot.MeasureFile);
        Assert.AreEqual(new DateTime(2026, 10, 16), snapshot.Today);

        Assert.IsTrue(TrialOptions.TryParse(["--MEASURE", @"C:\m.json"], out TrialOptions? upper, out error), error);
        Assert.AreEqual(@"C:\m.json", upper.MeasureFile, "Arguments are read without regard to case, as the others are.");
        Assert.IsNull(upper.AfterIdle, "A run told no idle side is today's run.");

        foreach (string side in IdleSides.All)
        {
            Assert.IsTrue(TrialOptions.TryParse(["--measure", @"C:\m.json", "--after-idle", side], out TrialOptions? sided, out error), error);
            Assert.AreEqual(side, sided.AfterIdle);
        }

        Assert.IsTrue(TrialOptions.TryParse(["--after-idle", "prepared", "--snapshot-from", @"C:\Libraries\Large", "--measure", @"C:\m.json"], out TrialOptions? first, out error), error);
        Assert.AreEqual("prepared", first.AfterIdle, "The side may come before --measure.");
    }

    [TestMethod]
    [DataRow(new[] { "--measure" }, "\"--measure\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--measure", "" }, "\"--measure\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--measure", "--snapshot-from", @"C:\Libraries\Large" }, "\"--measure\" needs the full path of the JSON file to write after it.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--measure", @"C:\b.json" }, "\"--measure\" is given twice.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--self-check" }, "\"--measure\" opens a window and \"--self-check\" opens none: give one or the other.")]
    [DataRow(new[] { "--self-check", "--measure", @"C:\a.json" }, "\"--measure\" opens a window and \"--self-check\" opens none: give one or the other.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--today", "2026-10-16" }, "\"--today\" is accepted only with \"--snapshot-from\".")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle" }, "\"--after-idle\" needs one of none, width, half, prepared after it.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "all" }, "\"--after-idle\" needs one of none, width, half, prepared after it.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "Prepared" }, "\"--after-idle\" needs one of none, width, half, prepared after it.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "none", "--after-idle", "prepared" }, "\"--after-idle\" is given twice.")]
    [DataRow(new[] { "--after-idle", "prepared" }, "\"--after-idle\" is accepted only with \"--measure\".")]
    [DataRow(new[] { "--self-check", "--after-idle", "none" }, "\"--after-idle\" is accepted only with \"--measure\".")]
    public void MeasureRefusesACommandLineItCannotRun(string[] args, string message)
    {
        Assert.IsFalse(TrialOptions.TryParse(args, out TrialOptions? options, out string? error), string.Join(" ", args));
        Assert.IsNull(options);
        Assert.AreEqual(message, error);
    }

    // The refusals the process gives before it opens anything; each run also
    // carries --self-check, so a refusal that failed would open no window.
    [TestMethod]
    public void TheTrialRefusesAMeasureItCannotRunAndOpensNothing()
    {
        SessionStartupProcessTests.SelfCheckRun missing = SessionStartupProcessTests.RunTrial("--self-check", "--measure");
        Assert.AreEqual(2, missing.ExitCode, missing.Describe());
        StringAssert.Contains(missing.Errors, "\"--measure\" needs the full path of the JSON file to write after it.", missing.Describe());
        Assert.IsEmpty(missing.Lines, missing.Describe());

        SessionStartupProcessTests.SelfCheckRun both = SessionStartupProcessTests.RunTrial("--measure", Path.Combine(_root, "board.json"), "--self-check");
        Assert.AreEqual(2, both.ExitCode, both.Describe());
        StringAssert.Contains(both.Errors, "\"--measure\" opens a window and \"--self-check\" opens none", both.Describe());
        Assert.IsEmpty(both.Lines, both.Describe());
        Assert.IsFalse(File.Exists(Path.Combine(_root, "board.json")), "Nothing was written.");
    }

    // ---- Where the JSON may go ----

    [TestMethod]
    public void TheMeasurementIsWrittenOnlyToAJsonFileInAFolderMadeForIt()
    {
        string fakeProfile = Path.Combine(_root, "profile", "PaperRoute");
        Directory.CreateDirectory(fakeProfile);

        Assert.IsNull(BoardMeasurementReport.RefusalFor(Path.Combine(_root, "board.json"), [fakeProfile]));

        Assert.StartsWith("\"--measure\" needs the full path of the JSON file to write", BoardMeasurementReport.RefusalFor("board.json"));
        Assert.StartsWith("\"--measure\" needs the full path of the JSON file to write", BoardMeasurementReport.RefusalFor(" "));
        Assert.StartsWith("The measurement is written as JSON:", BoardMeasurementReport.RefusalFor(Path.Combine(_root, "board.txt")));
        Assert.StartsWith("The Board trial found no folder at", BoardMeasurementReport.RefusalFor(Path.Combine(_root, "missing", "board.json")));
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            BoardMeasurementReport.RefusalFor(Path.Combine(fakeProfile, "board.json"), [fakeProfile]));
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            BoardMeasurementReport.RefusalFor(Path.Combine(fakeProfile, "data", "board.json"), [fakeProfile]));

        // Spellings a string comparison would miss: a link that leads into the
        // profile is resolved and refused; a device prefix, a loopback share
        // and a stream form, which exist on Windows only, are refused as not
        // plain local paths.
        string link = Path.Combine(_root, "J");
        MakeLink(link, fakeProfile);
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            BoardMeasurementReport.RefusalFor(Path.Combine(link, "m.json"), [fakeProfile]));
        Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
            BoardMeasurementReport.RefusalFor(Path.Combine(link, "data", "m.json"), [fakeProfile]));
        if (OperatingSystem.IsWindows())
        {
            string inProfile = Path.Combine(fakeProfile, "m.json");
            foreach (string spelling in new[]
            {
                @"\\?\" + inProfile, @"\\.\" + inProfile, @"\\localhost\" + inProfile[0] + "$" + inProfile[2..], fakeProfile + "::$INDEX_ALLOCATION\\m.json",
            })
            {
                Assert.AreEqual(BoardMeasurementReport.LocalFileMessage, BoardMeasurementReport.RefusalFor(spelling, [fakeProfile]), spelling);
            }
        }

        // The real profile's folders, by path only: refused before anything is read there.
        foreach (string root in ReadOnlySnapshot.DefaultStorageRoots())
        {
            Assert.StartsWith("The Board trial writes nothing into your own PaperRoute library.",
                BoardMeasurementReport.RefusalFor(Path.Combine(root, "board.json")), root);
        }

        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fakeProfile).Any(), "Nothing was written into the fake profile.");
    }

    // ---- The JSON ----

    [TestMethod]
    public void TheJsonHoldsEveryRowWithItsVerdictAndRule()
    {
        var report = new BoardMeasurementReport
        {
            Mode = "window",
            MachineName = "TEST-PC",
            Dpi = 144,
            Option = "b",
            ResetThreshold = 64,
            Day = new DateTime(2026, 10, 16),
            DaySource = "--today",
            Build = "Release",
            Multiplier = 1,
            TakenAt = new DateTime(2026, 11, 20, 9, 30, 0),
            WaitingForRowsOfN = ["stage-back-to-all"],
            HeldLibrary = "snapshot",
            Rows =
            [
                new MeasuredRow("snapshot", "render-no-card-changes", "Board render, no card changes", 12.34, 12.34, 250, "UI thread"),
                new MeasuredRow("snapshot", "first-switch-published", "First switch to Published", 140, 130.06, 100, "page switch"),
                new MeasuredRow("snapshot", "stage-back-to-all", "Stage filter back to All stages", 900, 880, 250, "UI thread", Note: "cards changed: Pipeline reset"),
                new MeasuredRow("snapshot", "switch-back-pipeline", "Switch back to Pipeline", 300, 300, 100, "page switch", Informational: true),
                new MeasuredRow("example", "first-switch-published", "First switch to Published", 400, 400, 100, "page switch"),
            ],
        };

        string path = Path.Combine(_root, "board.json");
        report.WriteJson(path);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "No byte-order mark.");
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Assert.AreEqual("window", root.GetProperty("mode").GetString());
        Assert.AreEqual("TEST-PC", root.GetProperty("machineName").GetString());
        Assert.AreEqual(144, root.GetProperty("dpi").GetDouble());
        Assert.AreEqual("b", root.GetProperty("option").GetString());
        Assert.AreEqual(64, root.GetProperty("syncIntoResetThreshold").GetInt32());
        Assert.AreEqual("2026-10-16", root.GetProperty("day").GetString());
        Assert.AreEqual("--today", root.GetProperty("daySource").GetString());
        Assert.AreEqual("Release", root.GetProperty("build").GetString());
        Assert.AreEqual(1, root.GetProperty("budgetMultiplier").GetDouble());
        Assert.AreEqual("snapshot", root.GetProperty("heldLibrary").GetString());
        Assert.AreEqual(Environment.ProcessorCount, root.GetProperty("processors").GetInt32());
        CollectionAssert.AreEqual(new[] { "stage-back-to-all" }, root.GetProperty("waitingForRowsOfN").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.IsFalse(root.TryGetProperty("error", out _), "No error when the run finished.");
        Assert.IsFalse(root.TryGetProperty("idlePreparation", out _), "A run told no idle side writes no idlePreparation.");

        List<string> rows = root.GetProperty("rows").EnumerateArray().Select(row =>
            string.Join(" | ",
                row.GetProperty("library").GetString(),
                row.GetProperty("id").GetString(),
                row.GetProperty("wallMs").GetDouble().ToString(CultureInfo.InvariantCulture),
                row.GetProperty("uiMs").GetDouble().ToString(CultureInfo.InvariantCulture),
                row.GetProperty("budgetMs").GetDouble().ToString(CultureInfo.InvariantCulture),
                row.GetProperty("verdict").GetString(),
                row.GetProperty("rule").GetString(),
                row.TryGetProperty("note", out JsonElement note) ? note.GetString() : "-")).ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                "snapshot | render-no-card-changes | 12.3 | 12.3 | 250 | within | enforced | -",
                "snapshot | first-switch-published | 140 | 130.1 | 100 | over | enforced | -",
                "snapshot | stage-back-to-all | 900 | 880 | 250 | over | waiting: over under rows of N too | cards changed: Pipeline reset",
                "snapshot | switch-back-pipeline | 300 | 300 | 100 | over | report-only | -",
                "example | first-switch-published | 400 | 400 | 100 | over | report-only | -",
            },
            rows,
            string.Join(Environment.NewLine, rows));
        Assert.AreEqual("First switch to Published", root.GetProperty("rows")[1].GetProperty("action").GetString());

        // The headless test fails only on the enforced row over its budget.
        CollectionAssert.AreEqual(new[] { "first-switch-published" }, report.Failures().Select(row => row.Id).ToArray());
        Assert.AreEqual("over", report.VerdictFor(report.Rows[1]));
        var doubled = new BoardMeasurementReport
        {
            Mode = report.Mode, MachineName = report.MachineName, Dpi = report.Dpi, Option = report.Option, ResetThreshold = report.ResetThreshold,
            Day = report.Day, DaySource = report.DaySource, Build = "Debug", Multiplier = 2, TakenAt = report.TakenAt,
            WaitingForRowsOfN = report.WaitingForRowsOfN, HeldLibrary = report.HeldLibrary, Rows = report.Rows,
        };
        Assert.IsEmpty(doubled.Failures(), "A Debug build doubles the budget: 130 ms is within 200.");

        // A run that stopped early names its error.
        var stopped = new BoardMeasurementReport
        {
            Mode = report.Mode, MachineName = report.MachineName, Dpi = 0, Option = report.Option, ResetThreshold = report.ResetThreshold,
            Day = report.Day, DaySource = report.DaySource, Build = report.Build, Multiplier = 1, TakenAt = report.TakenAt,
            Error = "TimeoutException: no frame",
        };
        using JsonDocument stoppedDocument = JsonDocument.Parse(stopped.ToJson());
        Assert.AreEqual("TimeoutException: no frame", stoppedDocument.RootElement.GetProperty("error").GetString());
        Assert.AreEqual(0, stoppedDocument.RootElement.GetProperty("rows").GetArrayLength());
    }

    // A run told an idle side records, per library, what preparation did
    // before the first switch: the side, the steps and their card views,
    // whether Published was prepared, why preparation stopped (null, or the
    // preparer's reason) and whether the switch after idle was timed, with
    // the card views it made (null when it was not timed).
    [TestMethod]
    public void TheJsonOfARunToldAnIdleSideRecordsWhatPreparationDid()
    {
        var report = new BoardMeasurementReport
        {
            Mode = "headless", MachineName = "TEST-PC", Dpi = 96, Option = "b", ResetThreshold = 64,
            Day = new DateTime(2026, 11, 27), DaySource = "the computer's date", Build = "Release", Multiplier = 1,
            TakenAt = new DateTime(2026, 11, 27, 9, 0, 0), HeldLibrary = "500",
            IdlePreparation =
            [
                new IdlePreparationRecord("example", IdleSides.Prepared, 5, 4, true, null, true, 0),
                new IdlePreparationRecord("500", IdleSides.Prepared, 3, 6, false, HiddenShelfPreparer.SlowSliceReason, false, null),
            ],
        };

        using JsonDocument document = JsonDocument.Parse(report.ToJson());
        JsonElement[] idle = document.RootElement.GetProperty("idlePreparation").EnumerateArray().ToArray();
        Assert.HasCount(2, idle);
        string Describe(JsonElement item) => string.Join(" | ",
            item.GetProperty("library").GetString(),
            item.GetProperty("side").GetString(),
            item.GetProperty("stepsRun").GetInt32().ToString(CultureInfo.InvariantCulture),
            item.GetProperty("viewsMade").GetInt32().ToString(CultureInfo.InvariantCulture),
            item.GetProperty("prepared").GetBoolean() ? "prepared" : "not prepared",
            item.GetProperty("skippedReason").ValueKind == JsonValueKind.Null ? "null" : item.GetProperty("skippedReason").GetString(),
            item.GetProperty("firstSwitchTaken").GetBoolean() ? "taken" : "not taken",
            item.GetProperty("viewsMadeAtSwitch").ValueKind == JsonValueKind.Null ? "null" : item.GetProperty("viewsMadeAtSwitch").GetInt32().ToString(CultureInfo.InvariantCulture));
        CollectionAssert.AreEqual(
            new[]
            {
                "example | prepared | 5 | 4 | prepared | null | taken | 0",
                "500 | prepared | 3 | 6 | not prepared | slow slice | not taken | null",
            },
            idle.Select(Describe).ToArray());

        string table = report.ToTable();
        StringAssert.Contains(table, "Idle side prepared (500): 3 preparation steps made 6 card views; Published not prepared at the switch; preparation stopped: slow slice; the first switch after idle was made but not timed.");
        StringAssert.Contains(table, "Idle side prepared (example): 5 preparation steps made 4 card views; Published prepared at the switch; preparation not stopped; the first switch after idle made 0 card views.");

        CollectionAssert.AreEqual(new[] { "none", "width", "half", "prepared" }, IdleSides.All.ToArray());
        CollectionAssert.AreEqual(new int?[] { 0, 1, 3, null }, IdleSides.All.Select(IdleSides.StepsFor).ToArray());
        Assert.IsFalse(IdleSides.IsKnown("Prepared"), "Sides are written in lower case.");
    }

    // ---- The UI probe ----

    // The probe ticks only where a key press could be served (Input priority,
    // as today's WM_TIMER): work queued at Render priority and run within the
    // action is a stall a person feels, so UI is Wall for such an action. A
    // probe at Normal priority ticked between those jobs and gave about one
    // job's time.
    [TestMethod]
    public void TheProbeCountsQueuedRenderPriorityWorkAsOneStall()
    {
        Assert.AreEqual(DispatcherPriority.Input, BoardMeasurement.ProbePriority);
        MeasuredRow row = Headless.Run(() =>
        {
            var measurement = new BoardMeasurement(_ => { });
            return measurement.Time("probe", "queued-render-jobs", "Ten 15 ms jobs at Render priority, run within the action", 250, "UI thread", () =>
            {
                for (int job = 0; job < 10; job++)
                {
                    Dispatcher.UIThread.Post(() => Thread.Sleep(15), DispatcherPriority.Render);
                }

                Dispatcher.UIThread.RunJobs();
            });
        });

        TestContext.WriteLine($"Wall {row.WallMs:0.0} ms, UI {row.UiMs:0.0} ms.");
        Assert.IsGreaterThanOrEqualTo(140, row.WallMs, "The ten jobs ran within the action.");
        Assert.IsGreaterThanOrEqualTo(row.WallMs * 0.9, row.UiMs, $"UI {row.UiMs:0} ms against Wall {row.WallMs:0} ms: the probe ticked between the jobs.");
    }

    public TestContext TestContext { get; set; } = null!;

    // ---- The wait for a real window's frames ----

    // WindowMeasurement.WaitForFrames waits for the window's next animation
    // frames. On the headless platform frames come only when the render
    // timer is ticked, so a timer ticks it here: the wait ends after two
    // frames, and a wait for more frames than its limit allows gives up at the
    // limit with a plain message.
    [TestMethod]
    public void TheWaitForAWindowsFramesEndsOnTheSecondFrameOrAtItsLimit()
    {
        var session = new MemorySession([], Papers.Today);
        (bool waited, string timedOut) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session);
            var ticks = new DispatcherTimer(TimeSpan.FromMilliseconds(5), DispatcherPriority.Normal, Dispatcher.UIThread, (_, _) => AvaloniaHeadlessPlatform.ForceRenderTimerTick());
            ticks.Start();
            bool waited = false;
            string timedOut = "(no timeout)";
            try
            {
                WindowMeasurement.WaitForFrames(board.Window, 2, TimeSpan.FromSeconds(10));
                waited = true;
                WindowMeasurement.WaitForFrames(board.Window, 1_000_000, TimeSpan.FromMilliseconds(200));
            }
            catch (TimeoutException ex)
            {
                timedOut = ex.Message;
            }
            finally
            {
                ticks.Stop();
            }

            return (waited, timedOut);
        });

        Assert.IsTrue(waited, "The wait for two frames ended within its limit: " + timedOut);
        Assert.StartsWith("The window drew ", timedOut);
        StringAssert.Contains(timedOut, " of 1000000 frames within 0.2 s: is it minimized, or the screen locked?");
    }

    // A link at link leading to the folder target (both in this test's own
    // folder). On Windows it is a directory junction: junctions need no
    // privilege, and .NET makes none, so cmd does. Elsewhere it is a symbolic
    // link, which needs no privilege there.
    private static void MakeLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            Assert.IsNotNull(new DirectoryInfo(link).LinkTarget, "The symbolic link was made.");
            return;
        }

        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in new[] { "/c", "mklink", "/J", link, target })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(start) ?? throw new AssertFailedException("cmd did not start.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, output);
        Assert.IsNotNull(new DirectoryInfo(link).LinkTarget, "The junction was made: " + output);
    }
}
