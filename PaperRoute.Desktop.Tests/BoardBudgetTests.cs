using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Measurement;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Measurement;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The 500-manuscript budget (d1 6.3, step 12), measured the way
// ResponsivenessBudgetTests.vb measures today's Board: the same library
// (LargeLibraryFixture.Create(root, 500)) on the computer's date, the same
// budgets and multipliers (Debug x2, GitHub Actions x3), the same metric
// (BoardMeasurement: Wall, and UI from a 1 ms dispatcher probe at Input
// priority, which ticks only where a key press could be served, as today's
// WM_TIMER does; headless, UI equals Wall for every synchronous action).
// The example runs first and is
// reported only, as it carries the process's first-time costs; then the 500
// library, held to the budgets. The actions are BoardMeasurement's, which
// "--measure" runs in a real window; a frame here is a captured frame.
//
// A row of the 500 library fails the test when it is over its budget, unless
// it is informational or waits for rows of N (BoardMeasurement.WaitingForRowsOfN):
// those are measured and reported. Every row goes to the test log, and the
// run's JSON to budget-headless.json beside the pictures. The busy-PC
// figures (busy-PC design) are report-only: how busy the PC was is read
// around the 500 library's run, and a failure's message says what the
// interface thread did and how busy the PC was, measured in that run.
//
// Then both sides of SyncInto's reset threshold (64): a change of 64 cards
// against one of 65, reported only. And the manual writer of the 500 library
// that "--measure" opens (TestCategory Manual; CI leaves it out).
[TestClass]
public sealed class BoardBudgetTests : StorageCheckedTests
{
    public const string ManualCategory = "Manual";

    // The 500 library's shelves (d1 "Start here").
    public const int PipelineCount = 167;
    public const int PublishedCount = 272;
    public const int FileDrawerCount = 61;

    // The rows of d1 6.3 the 500 library must have, each timed once.
    public static readonly IReadOnlyList<string> BudgetRows =
    [
        BoardMeasurement.WarmStartup,
        BoardMeasurement.StartupPaint,
        BoardMeasurement.FirstSwitchToPublished,
        BoardMeasurement.FirstSwitchToPublishedPaint,
        BoardMeasurement.RenderWithNoChange,
        BoardMeasurement.StageToSubmitted,
        BoardMeasurement.StageToSubmittedPaint,
        BoardMeasurement.StageBackToAll,
        BoardMeasurement.StageBackToAllPaint,
        BoardMeasurement.OpenListStepped,
        BoardMeasurement.OpenListSteppedPaint,
        BoardMeasurement.SearchKeystroke,
        BoardMeasurement.SearchKeystrokePaint,
        BoardMeasurement.ScrollPublished,
    ];

    // The rows that are not rows of d1 6.3, timed once each and report-only.
    public static readonly IReadOnlyList<string> ReportOnlyRows =
    [
        BoardMeasurement.SwitchBackToPipeline,
        BoardMeasurement.SearchKeystrokePublishedHidden,
        BoardMeasurement.SwitchToPublishedAfterHiddenChange,
    ];

    // The 500 library's last rows, in order: the scroll, then the two rows
    // compared only between builds (busy-PC design, section 9).
    public static readonly IReadOnlyList<string> LastRows =
    [
        BoardMeasurement.ScrollPublished,
        BoardMeasurement.SearchKeystrokePublishedHidden,
        BoardMeasurement.SwitchToPublishedAfterHiddenChange,
    ];

    // The variable that tells the headless budget run its side of a
    // with-and-without comparison of idle preparation (IdleSides), as
    // "--after-idle" tells a real-window run; Measure-BoardPairs.ps1 sets it
    // for each side. Unset or empty: today's run, whose rows are those above.
    public const string AfterIdleVariable = "PAPERROUTE_TRIAL_AFTER_IDLE";

    // The folder the manual writer fills and "--measure" opens (d1 7.3),
    // under the repository's ignored TestResults folder.
    public static readonly string MeasurementFolder = Path.Combine("TestResults", "v11-trial", "large-500");

    // The library of several thousand for the search scenario's refresh in a
    // real window (idle-preparation design, section 8, row 10), written by
    // its own manual writer.
    public const int SeveralThousand = 3000;
    public static readonly string SeveralThousandFolder = Path.Combine("TestResults", "v11-trial", "large-3000");

    // The window's size in every measured run, and the size the headless
    // runs give for a maximise: the headless platform has no screen to
    // maximise to, so a resize to a full-HD screen less a taskbar stands in.
    public const double Width = 1280;
    public const double Height = 800;
    public const double MaximisedWidth = 1920;
    public const double MaximisedHeight = 1040;

    private static readonly TimeSpan s_runLimit = TimeSpan.FromMinutes(10);

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialBudget-" + Guid.NewGuid().ToString("N"));
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

    [TestMethod]
    public void EveryRowIsMeasuredOnTheExampleAndTheFiveHundredLibrary()
    {
        DateTime today = DateTime.Today;
        using DiskLibrarySession example = Unloaded("example", today, root => LargeLibraryFixture.WriteExample(root));
        using DiskLibrarySession large = Unloaded("five-hundred", today, root => LargeLibraryFixture.Create(root, 500));

        // A run told an idle side measures the idle rows on both libraries in
        // place of the first switch and its paint (BoardMeasurement.IdleRows).
        string? side = AfterIdleSide();

        // How busy the PC was is read around the 500 library's measurement
        // only, from this test thread, never the interface thread.
        var rows = new List<MeasuredRow>();
        var idle = new List<IdlePreparationRecord>();
        (IReadOnlyList<MeasuredRow> exampleRows, _, IReadOnlyList<IdlePreparationRecord> exampleIdle) = Headless.Run(() => MeasureLibrary("example", example, requireChanges: false, side), s_runLimit);
        rows.AddRange(exampleRows);
        idle.AddRange(exampleIdle);
        RunConditions around = RunConditions.Begin();
        (IReadOnlyList<MeasuredRow> largeRows, ThreadClock clock, IReadOnlyList<IdlePreparationRecord> largeIdle) = Headless.Run(() => MeasureLibrary("500", large, requireChanges: true, side), s_runLimit);
        MeasurementConditions conditions = around.End(clock, largeRows);
        rows.AddRange(largeRows);
        idle.AddRange(largeIdle);

        Assert.HasCount(500, large.Manuscripts);
        CollectionAssert.AreEqual(
            new[] { PipelineCount, PublishedCount, FileDrawerCount },
            new[] { ManuscriptLocation.Pipeline, ManuscriptLocation.Published, ManuscriptLocation.FileDrawer }
                .Select(location => large.Manuscripts.Count(manuscript => manuscript.Location == location)).ToArray(),
            "The 500 library's shelves.");

        BoardMeasurementReport report = Report(rows, today, "the computer's date", "500", conditions, idle);
        Save(report, "budget-headless");
        CheckNewFieldsInJson(report);

        // In a run told a side, today's first switch and its paint give way
        // to the idle rows; every other row of d1 6.3 is timed as always.
        List<string> missing = BudgetRows
            .Where(id => side is null || (id != BoardMeasurement.FirstSwitchToPublished && id != BoardMeasurement.FirstSwitchToPublishedPaint))
            .Where(id => rows.Count(row => row.Library == "500" && row.Id == id) != 1).ToList();
        Assert.IsEmpty(missing, "Rows of d1 6.3 not timed exactly once on the 500 library: " + string.Join(", ", missing));
        Assert.HasCount(side is null ? 0 : 2, idle, "One idle record per library in a run told a side, none otherwise.");
        foreach (string library in new[] { "example", "500" })
        {
            CheckIdleRows(rows.Where(row => row.Library == library).ToList(), side, idle.FirstOrDefault(record => record.Library == library)?.FirstSwitchTaken ?? true);
        }
        List<string> reported = ReportOnlyRows.Where(id => rows.Count(row => row.Library == "500" && row.Id == id && row.Informational) != 1).ToList();
        Assert.IsEmpty(reported, "Report-only rows not timed exactly once, report-only, on the 500 library: " + string.Join(", ", reported));
        Assert.AreEqual(BoardMeasurement.SwitchToPublishedAfterHiddenChange, rows.Last(row => row.Library == "500").Id, "The switch after a hidden change is the last row, so no row before it changes.");
        CheckLastRows(rows.Where(row => row.Library == "500").ToList());
        foreach (string id in BoardMeasurement.WaitingForRowsOfN)
        {
            Assert.Contains(id, BudgetRows, $"WaitingForRowsOfN names {id}, which is not a row of d1 6.3.");
        }

        List<string> outOfBounds = CheckThreadClockBounds(rows.Where(row => row.Library == "500"));
        Assert.IsEmpty(outOfBounds, "Thread-clock figures past their bounds:" + Environment.NewLine + string.Join(Environment.NewLine, outOfBounds));
        CheckWaitsAndScroll(rows);

        // Today's rule, unchanged; the message adds what was measured in this
        // run (busy-PC design, section 8).
        IReadOnlyList<MeasuredRow> failures = report.Failures();
        Assert.IsEmpty(failures, report.FailureMessage());
    }

    // The JSON holds every busy-PC figure on every row, as a number or as
    // null, and the run's conditions; which are present depends on the
    // system, so presence is logged, not held. Nothing new names the PC.
    private void CheckNewFieldsInJson(BoardMeasurementReport report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJson());
        Assert.AreEqual(BoardMeasurementReport.RunUiStatus, document.RootElement.GetProperty("runUiMsStatus").GetString(), "Run UI is labelled not validated (busy-PC design, section 6).");
        string[] names = ["runMs", "notRunMs", "runUiMs", "perfCoreShare", "gcPauseMs", "jitMs"];
        foreach (JsonElement row in document.RootElement.GetProperty("rows").EnumerateArray())
        {
            foreach (string name in names)
            {
                Assert.IsTrue(row.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
                    $"Row {row.GetProperty("id").GetString()} has {name}, a number or null.");
            }
        }

        JsonElement conditions = document.RootElement.GetProperty("conditions");
        Assert.AreEqual(JsonValueKind.Object, conditions.ValueKind, "The run has its conditions.");
        string[] present = conditions.EnumerateObject().Where(item => item.Value.ValueKind != JsonValueKind.Null).Select(item => item.Name).ToArray();
        string[] absent = conditions.EnumerateObject().Where(item => item.Value.ValueKind == JsonValueKind.Null).Select(item => item.Name).ToArray();
        TestContext.WriteLine($"Conditions present: {string.Join(", ", present)}; absent: {(absent.Length == 0 ? "none" : string.Join(", ", absent))}.");
        Assert.IsFalse(conditions.TryGetProperty("machineName", out _), "The conditions name no PC.");
    }

    // The library's rows end with LastRows, in that order: the scroll, the
    // search with Published hidden (report-only, against the filter
    // keystroke budget for scale) and the switch after it (report-only,
    // against the page switch budget). A row removed or moved fails.
    public static void CheckLastRows(IReadOnlyList<MeasuredRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        CollectionAssert.AreEqual(
            LastRows.ToArray(),
            rows.Skip(Math.Max(0, rows.Count - LastRows.Count)).Select(row => row.Id).ToArray(),
            "The last rows: the scroll, the search with Published hidden, the switch after it.");
        MeasuredRow search = rows[^2];
        MeasuredRow switched = rows[^1];
        Assert.IsTrue(search.Informational, "The search with Published hidden is report-only.");
        Assert.AreEqual(BoardMeasurement.FilterKeystrokeBudget, search.BudgetMs, "The search with Published hidden is shown against the filter keystroke budget.");
        Assert.AreEqual("filter keystroke", search.BudgetName);
        Assert.IsTrue(switched.Informational, "The switch after a hidden change is report-only.");
        Assert.AreEqual(BoardMeasurement.PageSwitchBudget, switched.BudgetMs, "The switch after a hidden change is shown against the page switch budget.");
        Assert.AreEqual("page switch", switched.BudgetName);
        foreach (MeasuredRow row in new[] { search, switched })
        {
            Assert.IsFalse(row.Waits, $"{row.Id} does not wait by design.");
            Assert.Contains("compared only between builds", row.Note, $"{row.Id}'s note says it is compared only between builds.");
        }
    }

    // Where the idle rows (BoardMeasurement.IdleRowsFor) sit in one library's
    // rows. In a run told no side: none of them, and today's first switch and
    // its paint right after the startup rows and before the switch back to
    // Pipeline. In a run told a side: the side's idle rows there instead, in
    // their order (the two preparation rows, the scenario's rows, then the
    // switch and its paint, left out when preparation stopped and the switch
    // was not timed), each report-only, shown against its budget for scale,
    // waiting by design only where a frame or a wait is part of it, with the
    // note that it is compared only between the two sides of a sitting; and
    // today's first switch, and every idle row of another scenario, nowhere.
    // A row removed, moved, doubled or not report-only fails.
    public static void CheckIdleRows(IReadOnlyList<MeasuredRow> rows, string? side, bool switchTaken = true)
    {
        ArgumentNullException.ThrowIfNull(rows);
        string[] ids = rows.Select(row => row.Id).ToArray();
        string[] expected = side is null
            ? [BoardMeasurement.FirstSwitchToPublished, BoardMeasurement.FirstSwitchToPublishedPaint]
            : BoardMeasurement.IdleRowsFor(side, switchTaken).ToArray();
        string[] watched = [BoardMeasurement.FirstSwitchToPublished, BoardMeasurement.FirstSwitchToPublishedPaint, .. BoardMeasurement.AllIdleRows];
        foreach (string id in watched)
        {
            Assert.AreEqual(expected.Contains(id) ? 1 : 0, ids.Count(item => item == id), $"{id} is timed {(expected.Contains(id) ? "once" : "not at all")} in a run told {side ?? "no idle side"}.");
        }

        int back = Array.IndexOf(ids, BoardMeasurement.SwitchBackToPipeline);
        int start = back - expected.Length;
        Assert.IsGreaterThanOrEqualTo(1, start, "The first switch's rows come after the startup rows and before the switch back to Pipeline.");
        CollectionAssert.AreEqual(expected, ids[start..back], $"The rows before the switch back to Pipeline in a run told {side ?? "no idle side"}.");
        Assert.IsTrue(ids[start - 1] is BoardMeasurement.StartupPaint or BoardMeasurement.ProcessStartToBoard, $"The startup rows come right before {expected[0]}, not {ids[start - 1]}.");
        if (side is null)
        {
            return;
        }

        foreach (MeasuredRow row in rows.Skip(start).Take(expected.Length))
        {
            Assert.IsTrue(row.Informational, $"{row.Id} is report-only.");
            Assert.Contains(BoardMeasurement.BetweenSides, row.Note, $"{row.Id}'s note says it is compared only between the two sides of a sitting.");
            (double budget, string name, bool waits) = IdleRowShape(row.Id);
            Assert.AreEqual(budget, row.BudgetMs, $"{row.Id} is shown against the {name} budget.");
            Assert.AreEqual(name, row.BudgetName, row.Id);
            Assert.AreEqual(waits, row.Waits, $"{row.Id} waits by design: {waits}.");
        }
    }

    // The budget each idle row is shown against, for scale only, and whether
    // it waits by design: the preparation and refresh rows (a frame after
    // each step, or the idle timer's waits), the maximise (the window's new
    // size) and the paint.
    public static (double Budget, string Name, bool Waits) IdleRowShape(string id) => id switch
    {
        BoardMeasurement.PreparePublishedIdle => (BoardMeasurement.FilterKeystrokeBudget, "filter keystroke", true),
        BoardMeasurement.PreparePublishedLongestStep => (BoardMeasurement.UiThreadBudget, "UI thread", false),
        BoardMeasurement.FirstSwitchToPublishedAfterIdle => (BoardMeasurement.PageSwitchBudget, "page switch", false),
        BoardMeasurement.FirstSwitchToPublishedAfterIdlePaint => (BoardMeasurement.UiThreadBudget, "UI thread", true),
        BoardMeasurement.SearchKeystrokeStepDue or BoardMeasurement.SearchKeystrokeAfterPause
            or BoardMeasurement.SearchKeystrokePublishedPrepared or BoardMeasurement.SearchClearedPublishedPrepared => (BoardMeasurement.FilterKeystrokeBudget, "filter keystroke", false),
        BoardMeasurement.RefreshPublishedTimer or BoardMeasurement.RefreshPublishedAfterSearch
            or BoardMeasurement.RefreshPublishedAfterClear or BoardMeasurement.RefreshPublishedAfterTheme => (BoardMeasurement.UiThreadBudget, "UI thread", true),
        BoardMeasurement.MaximiseWindow => (BoardMeasurement.UiThreadBudget, "UI thread", true),
        BoardMeasurement.ScalingChange or BoardMeasurement.ThemeChange => (BoardMeasurement.UiThreadBudget, "UI thread", false),
        _ => throw new AssertFailedException($"{id} is not an idle row."),
    };

    // CheckIdleRows, on canned rows: it passes on the orders the measurement
    // writes and fails when an idle row is removed, moved, doubled or not
    // report-only, or when today's first switch and the idle rows are mixed.
    [TestMethod]
    public void TheIdleRowsCheckFailsWhenARowIsRemovedMovedOrNotReportOnly()
    {
        static MeasuredRow Row(string id, double budget, string name, bool informational = false, bool waits = false) =>
            new("500", id, id, 10, 10, budget, name, informational, informational ? BoardMeasurement.BetweenSides : string.Empty, Waits: waits);
        MeasuredRow startup = Row(BoardMeasurement.WarmStartup, BoardMeasurement.StartupBudget, "warm startup", waits: true);
        MeasuredRow startupPaint = Row(BoardMeasurement.StartupPaint, BoardMeasurement.UiThreadBudget, "UI thread", waits: true);
        MeasuredRow first = Row(BoardMeasurement.FirstSwitchToPublished, BoardMeasurement.PageSwitchBudget, "page switch");
        MeasuredRow firstPaint = Row(BoardMeasurement.FirstSwitchToPublishedPaint, BoardMeasurement.UiThreadBudget, "UI thread", waits: true);
        MeasuredRow prepare = Row(BoardMeasurement.PreparePublishedIdle, BoardMeasurement.FilterKeystrokeBudget, "filter keystroke", informational: true, waits: true);
        MeasuredRow longest = Row(BoardMeasurement.PreparePublishedLongestStep, BoardMeasurement.UiThreadBudget, "UI thread", informational: true);
        MeasuredRow after = Row(BoardMeasurement.FirstSwitchToPublishedAfterIdle, BoardMeasurement.PageSwitchBudget, "page switch", informational: true);
        MeasuredRow afterPaint = Row(BoardMeasurement.FirstSwitchToPublishedAfterIdlePaint, BoardMeasurement.UiThreadBudget, "UI thread", informational: true, waits: true);
        MeasuredRow back = Row(BoardMeasurement.SwitchBackToPipeline, BoardMeasurement.PageSwitchBudget, "page switch", informational: true);
        MeasuredRow render = Row(BoardMeasurement.RenderWithNoChange, BoardMeasurement.UiThreadBudget, "UI thread");
        MeasuredRow[] idle = [prepare, longest, after, afterPaint];
        const string sided = IdleSides.Prepared;

        CheckIdleRows([startup, startupPaint, first, firstPaint, back, render], side: null);
        CheckIdleRows([startup, startupPaint, prepare, longest, after, afterPaint, back, render], sided);
        CheckIdleRows([startup, startupPaint, prepare, longest, back, render], sided, switchTaken: false);

        // Today's run with an idle row, or without its first switch.
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, prepare, longest, after, afterPaint, back, render], side: null));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, first, firstPaint, prepare, back, render], side: null));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, firstPaint, back, render], side: null));

        // A run told a side with today's first switch in place of, or beside, the idle rows.
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, first, firstPaint, back, render], sided));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, first, firstPaint, prepare, longest, after, afterPaint, back, render], sided));

        // Each idle row removed, doubled, or not report-only.
        for (int index = 0; index < idle.Length; index++)
        {
            MeasuredRow[] removed = [startup, startupPaint, .. idle.Where((_, at) => at != index), back, render];
            Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows(removed, sided), $"{idle[index].Id} removed.");
            MeasuredRow[] doubled = [startup, startupPaint, .. idle, idle[index], back, render];
            Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows(doubled, sided), $"{idle[index].Id} doubled.");
            MeasuredRow[] held = [startup, startupPaint, .. idle.Select((row, at) => at == index ? row with { Informational = false } : row), back, render];
            Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows(held, sided), $"{idle[index].Id} not report-only.");
        }

        // Moved: two idle rows swapped, the idle rows before the startup
        // paint or after the switch back, and the switch kept when it was
        // not taken.
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, longest, prepare, after, afterPaint, back, render], sided));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, prepare, longest, afterPaint, after, back, render], sided));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, prepare, longest, after, afterPaint, startupPaint, back, render], sided));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, back, prepare, longest, after, afterPaint, render], sided));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckIdleRows([startup, startupPaint, prepare, longest, after, afterPaint, back, render], sided, switchTaken: false));
    }

    // The idle rows measured for each side, on the example library and for
    // none and prepared on the 500 library, with the
    // preparer's clock standing still so that no step reads as slow and a
    // busy PC cannot stop the preparation here (the headless budget run and
    // "--measure" keep the real clock). Counts only, which are load-free;
    // the times are reported, never read. The rows sit where CheckIdleRows
    // says and the two last rows stay last; the side ran its steps, none
    // inside warm startup; the width step makes no card view; after full
    // preparation Published is prepared and its first switch makes none.
    [TestMethod]
    [DataRow(IdleSides.None, 0, "example")]
    [DataRow(IdleSides.Width, 1, "example")]
    [DataRow(IdleSides.Half, 3, "example")]
    [DataRow(IdleSides.Prepared, 1 + HiddenShelfPreparer.LadderSteps + 1, "example")]
    [DataRow(IdleSides.None, 0, "500")]
    [DataRow(IdleSides.Prepared, 1 + HiddenShelfPreparer.LadderSteps + 1, "500")]
    public void TheIdleRowsAreMeasuredForEachSide(string side, int steps, string library)
    {
        DateTime today = DateTime.Today;
        bool large = library == "500";
        using DiskLibrarySession session = large
            ? Unloaded("five-hundred", today, root => LargeLibraryFixture.Create(root, 500))
            : Unloaded("example", today, root => LargeLibraryFixture.WriteExample(root));
        (IReadOnlyList<MeasuredRow> rows, _, IReadOnlyList<IdlePreparationRecord> idle) =
            Headless.Run(() => MeasureLibrary(library, session, requireChanges: large, side, StillPreparer), s_runLimit);
        BoardMeasurementReport report = Report(rows, today, "the computer's date", heldLibrary: string.Empty, idle: idle);
        TestContext.WriteLine(report.ToTable());
        Assert.IsEmpty(report.Failures(), "Nothing is held here.");

        CheckIdleRows(rows, side);
        CheckLastRows(rows);
        IdlePreparationRecord record = idle.Single();
        Assert.AreEqual(library, record.Library);
        Assert.AreEqual(side, record.Side);
        Assert.AreEqual(steps, record.StepsRun, "The side ran its steps.");
        Assert.IsNull(record.SkippedReason, "Preparation did not stop.");
        Assert.IsTrue(record.FirstSwitchTaken, "The first switch after idle was timed.");
        Assert.AreEqual(side == IdleSides.Prepared, record.Prepared, "Published is prepared after full preparation only.");
        Assert.Contains("0 idle preparation steps inside", rows.Single(row => row.Id == BoardMeasurement.WarmStartup).Note, "No step ran inside warm startup.");
        StringAssert.StartsWith(rows.Single(row => row.Id == BoardMeasurement.PreparePublishedLongestStep).Note, $"{BoardMeasurement.BetweenSides}; {steps} steps: ");
        AssertTheWaitIsTheSame(record);

        int published = session.Manuscripts.Count(manuscript => manuscript.Location == ManuscriptLocation.Published);
        Assert.IsGreaterThan(0, published, "The library has a Published shelf.");
        TestContext.WriteLine(BoardMeasurementReport.DescribeIdlePreparation(record));
        if (side is IdleSides.None or IdleSides.Width)
        {
            Assert.AreEqual(0, record.ViewsMade, "No card view is made before the switch without a layout step.");
            Assert.IsGreaterThan(0, record.ViewsMadeAtSwitch ?? 0, "The switch made the shelf's card views.");
        }

        if (side == IdleSides.Prepared)
        {
            Assert.IsGreaterThan(0, record.ViewsMade, "Preparation made the shelf's card views.");
            Assert.AreEqual(0, record.ViewsMadeAtSwitch, "The first switch after full preparation made no card view.");
        }

        StringAssert.EndsWith(rows.Single(row => row.Id == BoardMeasurement.FirstSwitchToPublishedAfterIdle).Note, $"; {record.ViewsMadeAtSwitch} card views made at the switch");
    }

    // Every side waits alike before the first switch after idle: the whole
    // idle period, then exactly one frame from the end of preparation to the
    // switch. The wait's length is a lower bound only, never read as a time.
    private static void AssertTheWaitIsTheSame(IdlePreparationRecord record)
    {
        Assert.IsGreaterThanOrEqualTo(BoardMeasurement.IdlePeriod.TotalMilliseconds, record.IdleWaitMs, $"The side {record.Side} waited the whole idle period.");
        Assert.AreEqual(1, record.FramesBeforeSwitch, $"The side {record.Side} drew one frame between the end of preparation and the switch.");
    }

    // The path a run takes when preparation stops, which a real window takes
    // whenever a step is slow or an accessibility client has read the
    // shelves: the first switch after idle is made untimed and its two rows
    // are not written, the record says why, FirstSwitchTaken is false and
    // ViewsMadeAtSwitch null, the wait is the same as on any side, and the
    // rows from the switch back to Pipeline on are a timed run's. The stops:
    // the preparer's clock moves 130 ms at each reading, so the width step
    // takes 130 ms and stops it; it moves 1 ms but 130 ms at its sixth
    // reading, so the second capped layout step (the third step) takes
    // 130 ms and stops it after card views were made; or a client reads the
    // shelf host's children before the rows, so no step runs.
    [TestMethod]
    [DataRow("slow width step", 1, HiddenShelfPreparer.SlowSliceReason)]
    [DataRow("slow layout step", 3, HiddenShelfPreparer.SlowSliceReason)]
    [DataRow("automation peer", 0, HiddenShelfPreparer.AutomationPeerReason)]
    public void TheIdleRowsWhenPreparationStops(string stop, int steps, string reason)
    {
        DateTime today = DateTime.Today;
        using DiskLibrarySession session = Unloaded("example", today, root => LargeLibraryFixture.WriteExample(root));
        (IReadOnlyList<MeasuredRow> taken, _, _) =
            Headless.Run(() => MeasureLibrary("example", session, requireChanges: false, IdleSides.Prepared, StillPreparer), s_runLimit);
        (IReadOnlyList<MeasuredRow> rows, _, IReadOnlyList<IdlePreparationRecord> idle) =
            Headless.Run(() => MeasureLibrary("example", session, requireChanges: false, IdleSides.Prepared, view => Stop(view, stop)), s_runLimit);
        BoardMeasurementReport report = Report(rows, today, "the computer's date", heldLibrary: string.Empty, idle: idle);
        TestContext.WriteLine(report.ToTable());
        Assert.IsEmpty(report.Failures(), "Nothing is held here.");

        CheckIdleRows(rows, IdleSides.Prepared, switchTaken: false);
        CheckLastRows(rows);
        IdlePreparationRecord record = idle.Single();
        TestContext.WriteLine(BoardMeasurementReport.DescribeIdlePreparation(record));
        Assert.AreEqual(reason, record.SkippedReason, $"{stop}: preparation stopped for its reason.");
        Assert.AreEqual(steps, record.StepsRun, $"{stop}: the steps that ran before the stop.");
        Assert.IsFalse(record.Prepared, $"{stop}: Published was not prepared at the switch.");
        Assert.IsFalse(record.FirstSwitchTaken, $"{stop}: the first switch after idle was not timed.");
        Assert.IsNull(record.ViewsMadeAtSwitch, $"{stop}: no card views are counted for an untimed switch.");
        AssertTheWaitIsTheSame(record);
        Assert.Contains($"preparation stopped: {reason}", rows.Single(row => row.Id == BoardMeasurement.PreparePublishedLongestStep).Note, $"{stop}: the longest step's note says why preparation stopped.");
        StringAssert.StartsWith(rows.Single(row => row.Id == BoardMeasurement.PreparePublishedLongestStep).Note, $"{BoardMeasurement.BetweenSides}; {steps} steps: ");

        static string[] FromTheSwitchBack(IReadOnlyList<MeasuredRow> run) =>
            run.SkipWhile(row => row.Id != BoardMeasurement.SwitchBackToPipeline).Select(row => row.Id).ToArray();
        CollectionAssert.AreEqual(FromTheSwitchBack(taken), FromTheSwitchBack(rows), $"{stop}: from the switch back to Pipeline on, the rows are a timed run's.");
    }

    // Sets up the Board view so preparation stops as TheIdleRowsWhenPreparationStops names.
    private static void Stop(BoardView view, string stop)
    {
        TimeSpan now = TimeSpan.Zero;
        int reads = 0;
        switch (stop)
        {
            case "slow width step":
                view.Preparer.Clock = () => now += TimeSpan.FromMilliseconds(130);
                break;
            case "slow layout step":
                view.Preparer.Clock = () => now += TimeSpan.FromMilliseconds(++reads == 6 ? 130 : 1);
                break;
            case "automation peer":
                StillPreparer(view);
                ControlAutomationPeer.CreatePeerForElement(view.FindControl<Panel>("ShelfHost") ?? throw new AssertFailedException("The Board has no shelf host.")).GetChildren();
                break;
            default:
                throw new AssertFailedException($"No stop called {stop}.");
        }
    }

    // The rows of the other scenarios (IdleSides; idle-preparation design,
    // section 8, rows 5, 6, 8, 9, 11, 12 and 13), one run per side, each on
    // the library where its counts mean something: the 500 library for a
    // side that prepares, the example for one that does not. The preparer's
    // clock stands still, so no step reads as slow; on timer-on, whose idle
    // rules read that clock, the real clock runs with the slow-step limit
    // raised instead, so the stop cannot fire and only the counts are held.
    // Counts only, which are load-free; the times are reported, never read.
    // The rows sit where CheckIdleRows says and the two last rows stay last;
    // every side of a scenario waits alike; every step of the session is
    // counted once; and each scenario did what it says: the last step met
    // by the switch or the key, no step in the pause before a key, the idle
    // refresh's steps, Published's rows at the switch, the managed heap read,
    // and no step between a maximise or a scaling change and the switch.
    [TestMethod]
    [DataRow(IdleSides.MidStep, "500")]
    [DataRow(IdleSides.KeyNone, "example")]
    [DataRow(IdleSides.KeyStep, "500")]
    [DataRow(IdleSides.TimerNone, "example")]
    [DataRow(IdleSides.TimerOn, "500")]
    [DataRow(IdleSides.SearchPending, "500")]
    [DataRow(IdleSides.SearchRefreshed, "500")]
    [DataRow(IdleSides.MaximiseNone, "example")]
    [DataRow(IdleSides.MaximisePrepared, "500")]
    [DataRow(IdleSides.ScalingNone, "example")]
    [DataRow(IdleSides.ScalingPrepared, "500")]
    [DataRow(IdleSides.ThemeNone, "example")]
    [DataRow(IdleSides.ThemePrepared, "500")]
    public void TheScenarioRowsAreMeasuredForEachSide(string side, string library)
    {
        DateTime today = DateTime.Today;
        bool large = library == "500";
        using DiskLibrarySession session = large
            ? Unloaded("five-hundred", today, root => LargeLibraryFixture.Create(root, 500))
            : Unloaded("example", today, root => LargeLibraryFixture.WriteExample(root));
        Action<BoardView> prepare = IdleSides.ByTimer(side) ? NoSlowStop : StillPreparer;
        (IReadOnlyList<MeasuredRow> rows, _, IReadOnlyList<IdlePreparationRecord> idle) =
            Headless.Run(() => MeasureLibrary(library, session, requireChanges: large, side, prepare), s_runLimit);
        BoardMeasurementReport report = Report(rows, today, "the computer's date", heldLibrary: string.Empty, idle: idle);
        TestContext.WriteLine(report.ToTable());
        Assert.IsEmpty(report.Failures(), "Nothing is held here.");

        CheckIdleRows(rows, side);
        CheckLastRows(rows);
        IdlePreparationRecord record = idle.Single();
        TestContext.WriteLine(BoardMeasurementReport.DescribeIdlePreparation(record));
        Assert.AreEqual(library, record.Library);
        Assert.AreEqual(side, record.Side);
        Assert.IsNull(record.SkippedReason, "Preparation did not stop.");
        Assert.IsTrue(record.FirstSwitchTaken, "The first switch after idle was timed.");
        Assert.Contains("0 idle preparation steps inside", rows.Single(row => row.Id == BoardMeasurement.WarmStartup).Note, "No step ran inside warm startup.");
        string scenario = IdleSides.ScenarioOf(side);
        bool change = scenario is IdleSides.MaximiseScenario or IdleSides.ScalingScenario;
        Assert.IsGreaterThanOrEqualTo(BoardMeasurement.IdlePeriod.TotalMilliseconds, record.IdleWaitMs, $"The side {side} waited the whole idle period.");
        Assert.AreEqual(change ? 2 : 1, record.FramesBeforeSwitch, $"The side {side} drew one frame after the idle period{(change ? ", and one after its change" : string.Empty)}.");
        Assert.AreEqual(change, record.ChangeToSwitchMs is not null, "Only a maximise or a scaling change is timed to the switch.");

        const int full = 1 + HiddenShelfPreparer.LadderSteps + 1;
        int before = side is IdleSides.MidStep or IdleSides.KeyStep ? IdleSides.StepsBeforeTheLast : IdleSides.Prepares(side) ? full : 0;
        Assert.AreEqual(before, record.StepsRun, "The side ran its steps before its scenario.");
        Assert.AreEqual(record.StepsRun + (record.StepsMet ?? 0) + (record.RefreshSteps ?? 0), record.SessionSteps, "Every step of the session is counted once: the preparation, a step met by a key or a click, the refresh.");
        int driven = IdleSides.ByTimer(side) ? 0 : record.StepsRun + (scenario is IdleSides.SearchScenario or IdleSides.ThemeScenario ? record.RefreshSteps ?? 0 : 0);
        Assert.HasCount(driven, record.StepFrameMs, "A frame is timed after every step the measurement drove.");
        if (!IdleSides.Prepares(side))
        {
            Assert.AreEqual(0, record.SessionSteps, "A side that does not prepare runs no step.");
            Assert.AreEqual(0, record.ViewsMade, "A side that does not prepare makes no card view before the switch.");

            // The timer scenario's switch shows the search's cards, and the
            // example's Published shelf holds none that match it.
            if (scenario != IdleSides.TimerScenario)
            {
                Assert.IsGreaterThan(0, record.ViewsMadeAtSwitch ?? 0, "Its switch made the shelf's card views.");
            }
        }

        switch (side)
        {
            case IdleSides.MidStep:
            case IdleSides.KeyStep:
                // Counted inside the row's own stopwatch, so a step run just
                // before the row would read 0 here.
                Assert.AreEqual(1, record.StepsMet, $"{side}: the last step ran inside the row's stopwatch and was met.");
                Assert.IsGreaterThan(0, record.StepsMetViews ?? 0, $"{side}: the last step made card views.");
                Assert.IsNotNull(record.ViewsMadeInRow, $"{side}: the card views made in the row are counted.");
                Assert.IsGreaterThanOrEqualTo(record.StepsMetViews ?? 0, record.ViewsMadeInRow.Value, $"{side}: the step's card views were made inside the row.");
                Assert.IsFalse(record.Prepared && side == IdleSides.MidStep, "mid-step: Published was not prepared as the switch was asked for.");
                if (side == IdleSides.MidStep)
                {
                    Assert.AreEqual(record.StepsMetViews, record.ViewsMadeInRow, "mid-step: every card view made in the row was the step's.");
                    Assert.AreEqual(0, record.ViewsMadeAtSwitch, "mid-step: after the last step the switch itself made no card view.");
                }

                break;
            case IdleSides.KeyNone:
                Assert.AreEqual(0, record.StepsMet, "key-none: no step met the key.");
                Assert.IsNotNull(record.ViewsMadeInRow, "key-none: the card views made in the row are counted.");
                break;
            case IdleSides.TimerNone:
                Assert.AreEqual(0, record.StepsMet, "timer-none: no step in the pause or the key.");
                Assert.AreEqual(0, record.RefreshSteps, "timer-none: no refresh.");
                break;
            case IdleSides.TimerOn:
                Assert.AreEqual(0, record.StepsMet, "timer-on: a key after a pause of 0.5 s met no step (a refresh waits for 2 s after a render).");
                Assert.AreEqual(2, record.RefreshSteps, "timer-on: the timer's refresh after the search was one row step and one layout.");
                Assert.IsTrue(record.Prepared, "timer-on: Published was prepared again at the switch.");
                Assert.IsFalse(record.RowsStaleAtSwitch, "timer-on: its rows were current at the switch.");
                Assert.AreEqual(0, record.ViewsMadeAtSwitch, "timer-on: the switch made no card view.");
                break;
            case IdleSides.SearchPending:
                Assert.AreEqual(0, record.RefreshSteps, "search-pending: no refresh.");
                Assert.IsTrue(record.RowsStaleAtSwitch, "search-pending: Published's rows waited for the switch.");
                Assert.IsFalse(record.Prepared, "search-pending: Published was not prepared at the switch.");
                break;
            case IdleSides.SearchRefreshed:
                Assert.AreEqual(6, record.RefreshSteps, "search-refreshed: each of the three refreshes was one row step and one layout.");
                Assert.IsFalse(record.RowsStaleAtSwitch, "search-refreshed: Published's rows were current at the switch.");
                Assert.IsTrue(record.Prepared, "search-refreshed: Published was prepared at the switch.");
                Assert.AreEqual(0, record.ViewsMadeAtSwitch, "search-refreshed: the switch made no card view.");
                break;
            case IdleSides.MaximisePrepared:
            case IdleSides.ScalingPrepared:
                Assert.IsFalse(record.Prepared, $"{side}: after the change Published was no longer called prepared.");
                Assert.AreEqual(full, record.SessionSteps, $"{side}: no step ran between the change and the switch.");
                break;
            case IdleSides.ThemeNone:
            case IdleSides.ThemePrepared:
                Assert.IsNotNull(record.ManagedBytesBefore, $"{side}: the managed heap was read before preparation.");
                Assert.IsNotNull(record.ManagedBytesAfter, $"{side}: the managed heap was read after preparation.");
                Assert.AreEqual(side == IdleSides.ThemePrepared ? 1 : 0, record.RefreshSteps, $"{side}: the refresh after the theme change.");
                if (side == IdleSides.ThemePrepared)
                {
                    Assert.IsTrue(record.Prepared, "theme-prepared: Published was prepared again at the switch.");
                    Assert.AreEqual(0, record.ViewsMadeAtSwitch, "theme-prepared: the switch made no card view.");
                }

                break;
        }

        if (scenario != IdleSides.ThemeScenario)
        {
            Assert.IsNull(record.ManagedBytesBefore, "Only the theme scenario takes a full collection.");
        }
    }

    // The harm rows keep their worst case: on mid-step and key-step, when the
    // last step, met inside the row, takes over the slow-step limit,
    // preparation stops there; but that step ran inside the row, so the row
    // is kept, the record says the stop came inside it, the run is not one
    // the pairs script drops, and the first switch after idle is timed as on
    // any run. The preparer's clock stands still until the steps before the
    // last have run, then moves 130 ms at each reading, so only the last
    // step reads as slow. The example library, whose Published shelf every
    // step lays out.
    [TestMethod]
    [DataRow(IdleSides.MidStep, BoardMeasurement.FirstSwitchToPublishedAfterIdle)]
    [DataRow(IdleSides.KeyStep, BoardMeasurement.SearchKeystrokeStepDue)]
    public void ASlowStepMetInsideTheRowKeepsTheRow(string side, string metRow)
    {
        DateTime today = DateTime.Today;
        using DiskLibrarySession session = Unloaded("example", today, root => LargeLibraryFixture.WriteExample(root));
        (IReadOnlyList<MeasuredRow> rows, _, IReadOnlyList<IdlePreparationRecord> idle) =
            Headless.Run(() => MeasureLibrary("example", session, requireChanges: false, side, SlowLastStep), s_runLimit);
        BoardMeasurementReport report = Report(rows, today, "the computer's date", heldLibrary: string.Empty, idle: idle);
        TestContext.WriteLine(report.ToTable());
        Assert.IsEmpty(report.Failures(), "Nothing is held here.");

        CheckIdleRows(rows, side);
        CheckLastRows(rows);
        IdlePreparationRecord record = idle.Single();
        TestContext.WriteLine(BoardMeasurementReport.DescribeIdlePreparation(record));
        Assert.AreEqual(IdleSides.StepsBeforeTheLast, record.StepsRun, $"{side}: the steps before the last ran and none stopped preparation.");
        Assert.AreEqual(HiddenShelfPreparer.SlowSliceReason, record.SkippedReason, $"{side}: the last step stopped preparation as slow.");
        Assert.IsTrue(record.StoppedInsideRow, $"{side}: the record says the stop came inside the row.");
        Assert.AreEqual(1, record.StepsMet, $"{side}: the slow step ran inside the row.");
        Assert.IsTrue(record.FirstSwitchTaken, $"{side}: the first switch after idle was timed.");
        Assert.IsNotNull(record.ViewsMadeAtSwitch, $"{side}: the switch's card views are counted.");
        Assert.Contains("the row kept", rows.Single(row => row.Id == metRow).Note, $"{side}: the met row's note says the stop came inside it.");
    }

    // The preparer's clock stands still for the steps before the last and
    // moves 130 ms at each reading once they have run.
    private static void SlowLastStep(BoardView view)
    {
        TimeSpan now = TimeSpan.Zero;
        HiddenShelfPreparer preparer = view.Preparer;
        preparer.Clock = () => now += preparer.StepsRun >= IdleSides.StepsBeforeTheLast ? TimeSpan.FromMilliseconds(130) : TimeSpan.Zero;
    }

    // The preparer on the real clock with the slow-step limit raised, so a
    // busy PC cannot stop the idle timer: its idle rules read that clock.
    private static void NoSlowStop(BoardView view) => view.Preparer.SlowSliceLimit = TimeSpan.FromHours(1);

    // The pairs script knows the sides IdleSides knows, in the same order,
    // each in its scenario, and the scaling scenario as headless only.
    [TestMethod]
    public void ThePairsScriptKnowsEverySideAndItsScenario()
    {
        string script = File.ReadAllText(Path.Combine(RepositoryRoot(), "ManualCertification", "Measure-BoardPairs.ps1"));
        MatchCollection sets = Regex.Matches(script, @"\[ValidateSet\(([^)]*)\)\] \[string\] \$(BeforeIdle|AfterIdle)");
        Assert.HasCount(2, sets, "-BeforeIdle and -AfterIdle each name their sides.");
        foreach (Match set in sets)
        {
            string[] names = Regex.Matches(set.Groups[1].Value, "'([^']*)'").Select(name => name.Groups[1].Value).ToArray();
            CollectionAssert.AreEqual(IdleSides.All.ToArray(), names, $"-{set.Groups[2].Value} names the sides IdleSides knows.");
        }

        foreach (string side in IdleSides.All)
        {
            Assert.Contains($"'{side}' = '{IdleSides.ScenarioOf(side)}'", script, $"The pairs script puts {side} in its scenario.");
        }

        string[] headlessOnly = IdleSides.All.Where(IdleSides.HeadlessOnly).Select(IdleSides.ScenarioOf).Distinct().ToArray();
        Assert.Contains($"$HeadlessOnlyScenarios = @({string.Join(", ", headlessOnly.Select(name => $"'{name}'"))})", script, "The pairs script refuses the headless-only scenarios in a window.");
    }

    // CheckLastRows, on canned rows: it passes on the order the measurement
    // writes and fails when either row is removed, moved or not report-only.
    [TestMethod]
    public void TheLastRowsCheckFailsWhenEitherRowIsRemovedOrMoved()
    {
        const string note = "not a row of d1 6.3; compared only between builds";
        var stage = new MeasuredRow("500", BoardMeasurement.StageBackToAll, "Back to All", 10, 10, BoardMeasurement.UiThreadBudget, "UI thread");
        var scroll = new MeasuredRow("500", BoardMeasurement.ScrollPublished, "Scroll", 10, 10, BoardMeasurement.UiThreadBudget, "UI thread");
        var search = new MeasuredRow("500", BoardMeasurement.SearchKeystrokePublishedHidden, "Search, Published hidden", 900, 900, BoardMeasurement.FilterKeystrokeBudget, "filter keystroke", Informational: true, Note: note);
        var switched = new MeasuredRow("500", BoardMeasurement.SwitchToPublishedAfterHiddenChange, "Switch after", 900, 900, BoardMeasurement.PageSwitchBudget, "page switch", Informational: true, Note: note);

        CheckLastRows([stage, scroll, search, switched]);
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, scroll, switched]));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, scroll, search]));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, scroll, switched, search]));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, search, scroll, switched]));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, scroll, search with { Informational = false }, switched]));
        Assert.ThrowsExactly<AssertFailedException>(() => CheckLastRows([stage, scroll, search, switched with { Informational = false }]));
    }

    // Both sides of SyncInto's reset threshold (d1 3.2 item 8, d1 9): on the
    // 500 library's Pipeline shelf, a search that hides 64 cards (64 removes,
    // below the threshold of 64 changes or at it) against one that hides 65
    // (one Reset, every card of the shelf made again), and the search cleared
    // again, each with its frame. For comparison only, the 65 again with the
    // threshold moved to 65, so the same change is made one card at a time.
    // The cards to hide go by position, spread along the shelf, since the
    // library's ids are random per run; each search matches a tag every
    // other manuscript is given. Reported, not held: the numbers decide.
    [TestMethod]
    public void BothSidesOfTheResetThresholdAreMeasured()
    {
        DateTime today = DateTime.Today;
        using DiskLibrarySession large = Unloaded("five-hundred", today, root => LargeLibraryFixture.Create(root, 500));
        large.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        List<Manuscript> manuscripts = large.Manuscripts.ToList();
        List<Manuscript> pipeline = manuscripts.Where(manuscript => manuscript.Location == ManuscriptLocation.Pipeline).ToList();
        Assert.HasCount(PipelineCount, pipeline);

        int threshold = SyncedCollection<CardViewModel>.DefaultResetThreshold;
        int[] sizes = [threshold, threshold + 1];
        foreach (int size in sizes)
        {
            var hidden = new HashSet<Manuscript>(Enumerable.Range(0, size).Select(index => pipeline[index * pipeline.Count / size]), ReferenceEqualityComparer.Instance);
            Assert.HasCount(size, hidden);
            foreach (Manuscript manuscript in manuscripts.Where(manuscript => !hidden.Contains(manuscript)))
            {
                manuscript.Tags.Add(Tag(size));
            }
        }

        var session = new MemorySession(manuscripts, today);
        List<MeasuredRow> rows = Headless.Run(() =>
        {
            var measurement = new BoardMeasurement(Frame);
            (MainWindow window, ShellViewModel shell) = measurement.Start("500", session, ThemeVariant.Light, 1280, 800);
            try
            {
                BoardViewModel board = shell.Board;
                Assert.AreEqual(threshold, board.ResetThreshold);
                foreach (int size in sizes)
                {
                    Change(measurement, window, board, size, threshold, comparison: false);
                }

                board.ResetThreshold = threshold + 1;
                Change(measurement, window, board, threshold + 1, threshold + 1, comparison: true);
                board.ResetThreshold = threshold;
                measurement.Finish();
            }
            finally
            {
                window.Close();
            }

            return measurement.Rows.ToList();
        }, s_runLimit);

        BoardMeasurementReport report = Report(rows, today, "the computer's date", heldLibrary: string.Empty);
        Save(report, "reset-threshold");
    }

    // The 500 library written once into TestResults\v11-trial\large-500 under
    // the repository, for the real-window run (d1 7.3):
    //   PaperRoute.Desktop.exe --snapshot-from <that folder> --measure <file>.json
    // Its ids are new on each write. Run by hand (TestCategory Manual).
    [TestMethod]
    [TestCategory(ManualCategory)]
    public void WriteLargeLibraryForMeasurement()
    {
        string repository = RepositoryRoot();
        string folder = Path.Combine(repository, MeasurementFolder);
        Assert.EndsWith(Path.Combine("TestResults", "v11-trial", "large-500"), folder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        LargeLibraryFixture.SyntheticLibrary library = LargeLibraryFixture.Create(folder, 500);

        CollectionAssert.AreEqual(
            new[] { PipelineCount, PublishedCount, FileDrawerCount },
            new[] { ManuscriptLocation.Pipeline, ManuscriptLocation.Published, ManuscriptLocation.FileDrawer }
                .Select(location => library.Manuscripts.Count(manuscript => manuscript.Location == location)).ToArray(),
            "The 500 library's shelves.");
        Assert.IsNull(ReadOnlySnapshot.RefusalFor(folder), "The trial's --snapshot-from accepts the folder.");
        TestContext.WriteLine($"The 500 library is in {folder}. Measure it with:");
        TestContext.WriteLine($"  PaperRoute.Desktop\\bin\\Release\\net10.0\\PaperRoute.Desktop.exe --snapshot-from \"{folder}\" --measure \"{Path.Combine(repository, "TestResults", "v11-trial", "measure-1.json")}\"");
    }

    // The library of several thousand written once into
    // TestResults\v11-trial\large-3000 under the repository, for the search
    // scenario's refresh in a real window (idle-preparation design, section
    // 8, row 10):
    //   PaperRoute.Desktop.exe --snapshot-from <that folder> --measure <file>.json --after-idle search-refreshed
    // Run by hand (TestCategory Manual).
    [TestMethod]
    [TestCategory(ManualCategory)]
    public void WriteSeveralThousandLibraryForMeasurement()
    {
        string repository = RepositoryRoot();
        string folder = Path.Combine(repository, SeveralThousandFolder);
        Assert.EndsWith(Path.Combine("TestResults", "v11-trial", "large-3000"), folder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        LargeLibraryFixture.SyntheticLibrary library = LargeLibraryFixture.Create(folder, SeveralThousand);
        Assert.HasCount(SeveralThousand, library.Manuscripts);
        Assert.IsGreaterThan(1000, library.Manuscripts.Count(manuscript => manuscript.Location == ManuscriptLocation.Published), "Published holds over a thousand manuscripts.");
        Assert.IsNull(ReadOnlySnapshot.RefusalFor(folder), "The trial's --snapshot-from accepts the folder.");
        TestContext.WriteLine($"The library of {SeveralThousand} is in {folder}. Measure the refresh on it with:");
        TestContext.WriteLine($"  PaperRoute.Desktop\\bin\\Release\\net10.0\\PaperRoute.Desktop.exe --snapshot-from \"{folder}\" --measure \"{Path.Combine(repository, "TestResults", "v11-trial", "refresh-1.json")}\" --after-idle search-refreshed");
    }

    // A captured frame: headless Skia draws the window as it is now.
    internal static void Frame(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            throw new InvalidOperationException("The headless platform captured no frame.");
        }
    }

    internal static BoardMeasurementReport Report(IReadOnlyList<MeasuredRow> rows, DateTime day, string daySource, string heldLibrary, MeasurementConditions? conditions = null, IReadOnlyList<IdlePreparationRecord>? idle = null) => new()
    {
        Mode = "headless",
        MachineName = Environment.MachineName,
        Dpi = 96,
        Option = BoardMeasurement.Option,
        ResetThreshold = SyncedCollection<CardViewModel>.DefaultResetThreshold,
        Day = day,
        DaySource = daySource,
        Build = BoardMeasurement.Build,
        Multiplier = BoardMeasurement.BudgetMultiplier(),
        TakenAt = DateTime.Now,
        WaitingForRowsOfN = BoardMeasurement.WaitingForRowsOfN,
        HeldLibrary = heldLibrary,
        Rows = rows,
        Conditions = conditions,
        IdlePreparation = idle ?? [],
    };

    // The library's rows, the interface thread's clock they were timed with,
    // closed after the last row, and in a run told an idle side what idle
    // preparation did. prepare, if given, sets up the Board view after
    // startup and before the other rows: StillPreparer stops the preparer's
    // own clock, so no step reads as slow and preparation cannot stop for the
    // PC's load.
    private static (IReadOnlyList<MeasuredRow> Rows, ThreadClock Clock, IReadOnlyList<IdlePreparationRecord> Idle) MeasureLibrary(string library, ILibrarySession session, bool requireChanges, string? side, Action<BoardView>? prepare = null)
    {
        var measurement = new BoardMeasurement(Frame)
        {
            AfterIdle = side,
            Maximise = static window =>
            {
                window.Width = MaximisedWidth;
                window.Height = MaximisedHeight;
            },
            Restore = static window =>
            {
                window.Width = Width;
                window.Height = Height;
            },
            SetScaling = static (window, scaling) => window.SetRenderScaling(scaling),
            DrawWhileWaiting = static () => AvaloniaHeadlessPlatform.ForceRenderTimerTick(),
        };
        (MainWindow window, ShellViewModel shell) = measurement.Start(library, session, ThemeVariant.Light, Width, Height);
        try
        {
            prepare?.Invoke(window.FindControl<BoardView>("BoardView") ?? throw new AssertFailedException("The window has no Board view."));
            measurement.MeasureBoard(library, window, shell, session, requireChanges);
            measurement.Finish();
        }
        finally
        {
            window.Close();
        }

        return (measurement.Rows.ToList(), measurement.Clock, measurement.IdlePreparation.ToList());
    }

    // The preparer's clock stands still: no step reads as slow.
    private static void StillPreparer(BoardView view) => view.Preparer.Clock = static () => TimeSpan.Zero;

    // The run's idle side from AfterIdleVariable, or null. An unknown side
    // fails the test before anything is measured.
    private static string? AfterIdleSide()
    {
        string? side = Environment.GetEnvironmentVariable(AfterIdleVariable);
        if (string.IsNullOrEmpty(side))
        {
            return null;
        }

        Assert.IsTrue(IdleSides.IsKnown(side), $"{AfterIdleVariable} names {side}; the sides are {string.Join(", ", IdleSides.All)}.");
        return side;
    }

    // Which headless rows wait by design (busy-PC design, section 2): warm
    // startup, every paint row and the idle preparation (a frame follows each
    // of its steps), and no other (the scroll row waits only in
    // a real window). The scroll row's figures come from its longest-UI step:
    // with no step (the example's shelf may not scroll) every figure is null,
    // never 0; with steps, RunMs + NotRunMs is the Wall its note gives for
    // that step (rounded there to a whole ms).
    private void CheckWaitsAndScroll(IReadOnlyList<MeasuredRow> rows)
    {
        foreach (MeasuredRow row in rows)
        {
            bool waits = row.Id is BoardMeasurement.WarmStartup or BoardMeasurement.PreparePublishedIdle or BoardMeasurement.MaximiseWindow
                || BoardMeasurement.RefreshRows.Contains(row.Id) || row.Id.EndsWith("-paint", StringComparison.Ordinal);
            Assert.AreEqual(waits, row.Waits, $"{row.Library} {row.Id}: waits by design is {waits}.");
        }

        foreach (MeasuredRow scroll in rows.Where(row => row.Id == BoardMeasurement.ScrollPublished))
        {
            if (scroll.Note.StartsWith("0 steps", StringComparison.Ordinal))
            {
                TestContext.WriteLine($"{scroll.Library} scroll: no step, so no busy-PC figure.");
                Assert.IsTrue(scroll.RunMs is null && scroll.NotRunMs is null && scroll.RunUiMs is null && scroll.PerfCoreShare is null && scroll.GcPauseMs is null && scroll.JitMs is null,
                    $"{scroll.Library}: a scroll row with no step has every busy-PC figure null, never 0.");
                continue;
            }

            if (scroll.RunMs is not { } ran || scroll.NotRunMs is not { } notRun)
            {
                continue;
            }

            const string marker = "the longest-UI step's Wall ";
            int at = scroll.Note.IndexOf(marker, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, at, $"{scroll.Library}: the scroll row's note gives its longest-UI step's Wall: {scroll.Note}");
            string text = scroll.Note[(at + marker.Length)..].Split(' ')[0];
            double stepWall = double.Parse(text, CultureInfo.InvariantCulture);
            TestContext.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{scroll.Library} scroll: runMs + notRunMs {ran + notRun:0.0} against the step's Wall {stepWall:0} ms in the note (row wallMs {scroll.WallMs:0.0})."));
            Assert.AreEqual(stepWall, ran + notRun, 0.51, $"{scroll.Library}: the scroll row's figures come from the step its note names.");
        }
    }

    // The thread clock's figures against Wall and UI (busy-PC design, section
    // 4): a gap's running time is at most the gap, so runMs <= wallMs and
    // runUiMs <= uiMs hold up to the clock's error, checked with the rate
    // check's 2% and 1 ms. Load only takes time away from the thread, so a
    // busy PC cannot push a figure past them. runMs is held to the Wall it was
    // taken against (OwnWallMs: for the scroll row its longest-UI step's
    // Wall, not the row's longest Wall). Each comparison goes to the log; a
    // row without the figures is skipped, never failed. That the clock is
    // read inside the stopwatch is held by reading BoardMeasurement.TimeStep,
    // not by a test.
    private List<string> CheckThreadClockBounds(IEnumerable<MeasuredRow> rows)
    {
        var outOfBounds = new List<string>();
        foreach (MeasuredRow row in rows)
        {
            if (row.RunMs is { } ran)
            {
                Compare(row.Id, "runMs", ran, row.Id == BoardMeasurement.ScrollPublished ? "its step's Wall" : "wallMs", row.OwnWallMs, outOfBounds);
            }

            if (row.RunUiMs is { } runUi)
            {
                Compare(row.Id, "runUiMs", runUi, "uiMs", row.UiMs, outOfBounds);
            }

            if (row.RunMs is null && row.RunUiMs is null)
            {
                TestContext.WriteLine($"{row.Id}: no thread-clock figures.");
            }
        }

        return outOfBounds;
    }

    private void Compare(string id, string name, double value, string againstName, double against, List<string> outOfBounds)
    {
        double bound = (1.02 * against) + 1;
        string line = string.Create(CultureInfo.InvariantCulture, $"{id}: {name} {value:0.0} <= 1.02 x {againstName} {against:0.0} + 1 = {bound:0.0}");
        bool within = value <= bound;
        TestContext.WriteLine(line + (within ? " (holds)" : " (does not hold)"));
        if (!within)
        {
            outOfBounds.Add(line);
        }
    }

    // A search that hides size of the Pipeline's cards, then the search
    // cleared, each change with its frame; each checked to have made the
    // changes it should: size of them up to the threshold, else one Reset.
    private static void Change(BoardMeasurement measurement, MainWindow window, BoardViewModel board, int size, int threshold, bool comparison)
    {
        string label = comparison ? $"{size}, threshold moved to {threshold} for comparison" : size.ToString(CultureInfo.InvariantCulture);
        string suffix = comparison ? "-no-reset" : string.Empty;
        int expected = size <= threshold ? size : -1;
        ShelfView shelf = window.FindControl<BoardView>("BoardView")!.FindControl<ShelfView>("PipelineShelf")!;

        measurement.Time("500", $"hide-{size}{suffix}", $"A search that takes {label} of the Pipeline's {PipelineCount} cards off the shelf (render, layout)", BoardMeasurement.UiThreadBudget, "UI thread", () =>
        {
            board.SearchText = Tag(size);
            board.Render();
            BoardMeasurement.Settle(window);
        }, informational: true);
        measurement.NoteChanges(board);
        Assert.AreEqual(expected, board.LastSyncChanges[0], $"Hiding {label}: the Pipeline's changes (-1 for a Reset).");
        CardViewsFor(shelf, PipelineCount - size, $"Hiding {label}");
        measurement.Time("500", $"hide-{size}{suffix}-paint", "Its frame", BoardMeasurement.UiThreadBudget, "UI thread", () => Frame(window), informational: true);

        measurement.Time("500", $"show-{size}{suffix}", $"The search cleared, putting the {label} back (render, layout)", BoardMeasurement.UiThreadBudget, "UI thread", () =>
        {
            board.SearchText = string.Empty;
            board.Render();
            BoardMeasurement.Settle(window);
        }, informational: true);
        measurement.NoteChanges(board);
        Assert.AreEqual(expected, board.LastSyncChanges[0], $"Showing {label} again: the Pipeline's changes (-1 for a Reset).");
        CardViewsFor(shelf, PipelineCount, $"Showing {label} again");
        measurement.Time("500", $"show-{size}{suffix}-paint", "Its frame", BoardMeasurement.UiThreadBudget, "UI thread", () => Frame(window), informational: true);
    }

    // The shelf has a card view for some of its cards (the rows made), one at least.
    private static void CardViewsFor(ShelfView shelf, int cards, string context)
    {
        int views = shelf.CardViews().Count;
        Assert.IsTrue(views > 0 && views < cards, $"{context}: rows of N made {views} card views for the Pipeline's {cards} cards.");
    }

    private static string Tag(int size) => "budget-keep-" + size.ToString(CultureInfo.InvariantCulture);

    // A library written to a folder of this test's own and a session over it,
    // not loaded: the startup row loads it.
    private DiskLibrarySession Unloaded(string name, DateTime today, Action<string> write)
    {
        string root = Path.Combine(_root, name);
        Directory.CreateDirectory(root);
        write(root);
        return new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            Path.Combine(root, "settings"),
            LibraryKind.Example,
            today);
    }

    // The report to the test log, and its JSON beside the pictures.
    private void Save(BoardMeasurementReport report, string name)
    {
        TestContext.WriteLine(report.ToTable());
        string folder = Pictures.Directory();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ".json");
        report.WriteJson(path);
        TestContext.WriteLine($"Measurement saved: {path}");
    }

    // The folder that holds PaperRoute.Desktop.slnx, above the test output.
    private static string RepositoryRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "PaperRoute.Desktop.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.IsNotNull(folder, $"No PaperRoute.Desktop.slnx above {AppContext.BaseDirectory}.");
        return folder.FullName;
    }
}
