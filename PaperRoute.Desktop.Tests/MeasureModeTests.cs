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
    // The refusal of an unknown idle side: every side a real window can run,
    // which leaves out the scaling sides (headless only).
    private const string SidesMessage = "\"--after-idle\" needs one of none, width, half, prepared, mid-step, key-none, key-step, timer-none, timer-on, search-pending, search-refreshed, maximise-none, maximise-prepared, theme-none, theme-prepared after it.";

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

        foreach (string side in IdleSides.All.Where(side => !IdleSides.HeadlessOnly(side)))
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
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle" }, SidesMessage)]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "all" }, SidesMessage)]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "Prepared" }, SidesMessage)]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "none", "--after-idle", "prepared" }, "\"--after-idle\" is given twice.")]
    [DataRow(new[] { "--after-idle", "prepared" }, "\"--after-idle\" is accepted only with \"--measure\".")]
    [DataRow(new[] { "--self-check", "--after-idle", "none" }, "\"--after-idle\" is accepted only with \"--measure\".")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "scaling-prepared" }, "\"--after-idle scaling-prepared\" changes the display scaling, which a real window cannot do by itself; that side runs headless only.")]
    [DataRow(new[] { "--measure", @"C:\a.json", "--after-idle", "scaling-none" }, "\"--after-idle scaling-none\" changes the display scaling, which a real window cannot do by itself; that side runs headless only.")]
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
    // the card views it made (null when it was not timed), and what the wait
    // before the switch did: the idle period waited, to 0.1 ms, and the
    // frames drawn from the end of preparation to the switch.
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
                new IdlePreparationRecord("example", IdleSides.Prepared, 5, 4, true, null, true, 0, 1000.44, 1),
                new IdlePreparationRecord("500", IdleSides.Prepared, 3, 6, false, HiddenShelfPreparer.SlowSliceReason, false, null, 1001.26, 1),
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
            item.GetProperty("viewsMadeAtSwitch").ValueKind == JsonValueKind.Null ? "null" : item.GetProperty("viewsMadeAtSwitch").GetInt32().ToString(CultureInfo.InvariantCulture),
            item.GetProperty("idleWaitMs").GetDouble().ToString(CultureInfo.InvariantCulture),
            item.GetProperty("framesBeforeSwitch").GetInt32().ToString(CultureInfo.InvariantCulture));
        CollectionAssert.AreEqual(
            new[]
            {
                "example | prepared | 5 | 4 | prepared | null | taken | 0 | 1000.4 | 1",
                "500 | prepared | 3 | 6 | not prepared | slow slice | not taken | null | 1001.3 | 1",
            },
            idle.Select(Describe).ToArray());

        string table = report.ToTable();
        StringAssert.Contains(table, "Idle side prepared (500): 3 preparation steps made 6 card views; Published not prepared at the switch; preparation stopped: slow slice; the first switch after idle was made but not timed.");
        StringAssert.Contains(table, "Idle side prepared (example): 5 preparation steps made 4 card views; Published prepared at the switch; preparation not stopped; the first switch after idle made 0 card views.");

        CollectionAssert.AreEqual(
            new[]
            {
                "none", "width", "half", "prepared", "mid-step", "key-none", "key-step", "timer-none", "timer-on", "search-pending", "search-refreshed",
                "maximise-none", "maximise-prepared", "scaling-none", "scaling-prepared", "theme-none", "theme-prepared",
            },
            IdleSides.All.ToArray());
        CollectionAssert.AreEqual(new int?[] { 0, 1, 3, null, 4, 0, 4, 0, null, null, null, 0, null, 0, null, 0, null }, IdleSides.All.Select(IdleSides.StepsFor).ToArray());
        CollectionAssert.AreEqual(
            new[] { "switch", "switch", "switch", "switch", "switch", "key", "key", "timer", "timer", "search", "search", "maximise", "maximise", "scaling", "scaling", "theme", "theme" },
            IdleSides.All.Select(IdleSides.ScenarioOf).ToArray());
        CollectionAssert.AreEqual(new[] { "timer-on" }, IdleSides.All.Where(IdleSides.ByTimer).ToArray(), "Only timer-on lets the Board's own idle timer prepare Published.");
        CollectionAssert.AreEqual(new[] { "scaling-none", "scaling-prepared" }, IdleSides.All.Where(IdleSides.HeadlessOnly).ToArray(), "Only the scaling sides are headless only.");
        Assert.AreEqual(HiddenShelfPreparer.LadderSteps + 1, IdleSides.StepsFor(IdleSides.MidStep), "The steps before the last are W and the ladder's capped steps.");
        Assert.IsFalse(IdleSides.IsKnown("Prepared"), "Sides are written in lower case.");

        // Every side of a scenario writes the same rows, so two sides of one
        // scenario can be paired; the switch scenario's rows are IdleRows.
        // Both sides of the search scenario prepare: they differ in the idle
        // refresh (row 8), so it has no side without preparation.
        foreach (IGrouping<string, string> scenario in IdleSides.All.GroupBy(IdleSides.ScenarioOf))
        {
            Assert.IsGreaterThanOrEqualTo(2, scenario.Count(), $"The scenario {scenario.Key} has two sides at least.");
            Assert.AreEqual(scenario.Key != IdleSides.SearchScenario, scenario.Any(side => !IdleSides.Prepares(side)), $"The scenario {scenario.Key} has a side without preparation, but for search.");
            Assert.IsTrue(scenario.Any(IdleSides.Prepares), $"The scenario {scenario.Key} has a side with preparation.");
            Assert.AreEqual(1, scenario.Select(side => string.Join(",", BoardMeasurement.IdleRowsFor(side))).Distinct().Count(), $"Every side of the scenario {scenario.Key} writes the same rows.");
        }

        CollectionAssert.AreEqual(BoardMeasurement.IdleRows.ToArray(), BoardMeasurement.IdleRowsFor(IdleSides.Prepared).ToArray(), "The switch scenario writes the idle rows.");
    }

    // The counts the other scenarios add to the idle record (IdleSides), in
    // the JSON and the table: the scenario, the steps of the session, the
    // steps a key or a click met and their card views, every card view made
    // in that row (on mid-step the switch's own is the row's less the
    // step's, so the table says the switch made none), the refresh steps,
    // whether Published's rows were stale at the switch, the managed heap
    // before and after preparation, the frame after each step and the time
    // from a change to the switch; each null or empty where the scenario has
    // none.
    [TestMethod]
    public void TheJsonOfARunToldAScenarioRecordsItsCounts()
    {
        var report = new BoardMeasurementReport
        {
            Mode = "headless", MachineName = "TEST-PC", Dpi = 96, Option = "b", ResetThreshold = 64,
            Day = new DateTime(2026, 11, 27), DaySource = "the computer's date", Build = "Release", Multiplier = 1,
            TakenAt = new DateTime(2026, 11, 27, 9, 0, 0), HeldLibrary = "500",
            IdlePreparation =
            [
                new IdlePreparationRecord("500", IdleSides.ThemePrepared, 5, 21, true, null, true, 0, 1000.2, 1)
                {
                    SessionSteps = 6, RefreshSteps = 1, ManagedBytesBefore = 10 * 1024 * 1024, ManagedBytesAfter = 12 * 1024 * 1024,
                    StepFrameMs = [4.04, 5.06, 6, 7, 8, 9],
                },
                new IdlePreparationRecord("500", IdleSides.MidStep, 4, 11, false, null, true, 0, 1000.2, 1)
                {
                    SessionSteps = 5, StepsMet = 1, StepsMetViews = 10, ViewsMadeInRow = 10, StepFrameMs = [1, 2, 3, 4],
                },
                new IdlePreparationRecord("500", IdleSides.MaximisePrepared, 5, 21, false, null, true, 3, 1000.2, 2)
                {
                    SessionSteps = 5, RowsStaleAtSwitch = true, ChangeToSwitchMs = 41.26,
                },
            ],
        };

        using JsonDocument document = JsonDocument.Parse(report.ToJson());
        JsonElement[] idle = document.RootElement.GetProperty("idlePreparation").EnumerateArray().ToArray();
        static string Number(JsonElement item, string name) =>
            item.GetProperty(name).ValueKind == JsonValueKind.Null ? "null" : item.GetProperty(name).GetDouble().ToString(CultureInfo.InvariantCulture);
        string Describe(JsonElement item) => string.Join(" | ",
            item.GetProperty("scenario").GetString(),
            Number(item, "sessionSteps"),
            Number(item, "stepsMet"),
            Number(item, "stepsMetViews"),
            Number(item, "viewsMadeInRow"),
            Number(item, "refreshSteps"),
            item.GetProperty("rowsStaleAtSwitch").GetBoolean() ? "stale" : "current",
            Number(item, "managedBytesBefore"),
            Number(item, "managedBytesAfter"),
            string.Join(",", item.GetProperty("stepFrameMs").EnumerateArray().Select(frame => frame.GetDouble().ToString(CultureInfo.InvariantCulture))),
            Number(item, "changeToSwitchMs"));
        CollectionAssert.AreEqual(
            new[]
            {
                "theme | 6 | null | null | null | 1 | current | 10485760 | 12582912 | 4,5.1,6,7,8,9 | null",
                "switch | 5 | 1 | 10 | 10 | null | current | null | null | 1,2,3,4 | null",
                "maximise | 5 | null | null | null | null | stale | null | null |  | 41.3",
            },
            idle.Select(Describe).ToArray());

        string table = report.ToTable();
        StringAssert.Contains(table, "Idle side theme-prepared (500): 5 preparation steps made 21 card views; Published prepared at the switch; preparation not stopped; the first switch after idle made 0 card views. 6 steps in the session; Published's rows current at the switch; 1 idle refresh steps; managed memory 10.0 MB before preparation, 12.0 MB after.");
        StringAssert.Contains(table, "Idle side mid-step (500): 4 preparation steps made 11 card views; Published not prepared at the switch; preparation not stopped; the first switch after idle made 0 card views. 5 steps in the session; Published's rows current at the switch; 1 steps met by the key or the click (10 card views; 10 made in that row in all).");
        StringAssert.Contains(table, "Idle side maximise-prepared (500): 5 preparation steps made 21 card views; Published not prepared at the switch; preparation not stopped; the first switch after idle made 3 card views. 5 steps in the session; Published's rows stale at the switch; 41 ms from the change to the switch.");
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
