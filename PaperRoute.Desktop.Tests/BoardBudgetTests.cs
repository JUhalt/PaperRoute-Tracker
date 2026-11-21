using System.Globalization;
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
// WM_TIMER does; headless, UI equals Wall for every synchronous action under
// either option). The example runs first and is
// reported only, as it carries the process's first-time costs; then the 500
// library, held to the budgets. The actions are BoardMeasurement's, which
// "--measure" runs in a real window; a frame here is a captured frame.
//
// A row of the 500 library fails the test when it is over its budget, unless
// it is informational or waits for rows of N (BoardMeasurement.WaitingForRowsOfN):
// those are measured and reported. Every row goes to the test log, and the
// run's JSON to budget-headless.json beside the pictures.
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
        BoardMeasurement.SwitchToPublishedAfterHiddenChange,
    ];

    // The folder the manual writer fills and "--measure" opens (d1 7.3),
    // under the repository's ignored TestResults folder.
    public static readonly string MeasurementFolder = Path.Combine("TestResults", "v11-trial", "large-500");

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

        var rows = new List<MeasuredRow>();
        rows.AddRange(Headless.Run(() => MeasureLibrary("example", example, requireChanges: false), s_runLimit));
        rows.AddRange(Headless.Run(() => MeasureLibrary("500", large, requireChanges: true), s_runLimit));

        Assert.HasCount(500, large.Manuscripts);
        CollectionAssert.AreEqual(
            new[] { PipelineCount, PublishedCount, FileDrawerCount },
            new[] { ManuscriptLocation.Pipeline, ManuscriptLocation.Published, ManuscriptLocation.FileDrawer }
                .Select(location => large.Manuscripts.Count(manuscript => manuscript.Location == location)).ToArray(),
            "The 500 library's shelves.");

        BoardMeasurementReport report = Report(rows, today, "the computer's date", "500");
        Save(report, "budget-headless");

        List<string> missing = BudgetRows.Where(id => rows.Count(row => row.Library == "500" && row.Id == id) != 1).ToList();
        Assert.IsEmpty(missing, "Rows of d1 6.3 not timed exactly once on the 500 library: " + string.Join(", ", missing));
        List<string> reported = ReportOnlyRows.Where(id => rows.Count(row => row.Library == "500" && row.Id == id && row.Informational) != 1).ToList();
        Assert.IsEmpty(reported, "Report-only rows not timed exactly once, report-only, on the 500 library: " + string.Join(", ", reported));
        Assert.AreEqual(BoardMeasurement.SwitchToPublishedAfterHiddenChange, rows.Last(row => row.Library == "500").Id, "The switch after a hidden change is the last row, so no row before it changes.");
        foreach (string id in BoardMeasurement.WaitingForRowsOfN)
        {
            Assert.Contains(id, BudgetRows, $"WaitingForRowsOfN names {id}, which is not a row of d1 6.3.");
        }

        IReadOnlyList<MeasuredRow> failures = report.Failures();
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Select(row =>
            $"{row.Library}: {row.Action} took {row.UiMs:0} ms against {row.BudgetMs * report.Multiplier:0} ms")));
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

    // A captured frame: headless Skia draws the window as it is now.
    internal static void Frame(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            throw new InvalidOperationException("The headless platform captured no frame.");
        }
    }

    internal static BoardMeasurementReport Report(IReadOnlyList<MeasuredRow> rows, DateTime day, string daySource, string heldLibrary) => new()
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
    };

    private static IReadOnlyList<MeasuredRow> MeasureLibrary(string library, ILibrarySession session, bool requireChanges)
    {
        var measurement = new BoardMeasurement(Frame);
        (MainWindow window, ShellViewModel shell) = measurement.Start(library, session, ThemeVariant.Light, 1280, 800);
        try
        {
            measurement.MeasureBoard(library, window, shell, session, requireChanges);
        }
        finally
        {
            window.Close();
        }

        return measurement.Rows.ToList();
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

    // Under option (a) the shelf has a card view for each of its cards; under
    // (b) for some of them (the rows made), one at least.
    private static void CardViewsFor(ShelfView shelf, int cards, string context)
    {
        int views = shelf.CardViews().Count;
        if (ShelfLayout.UsesRows)
        {
            Assert.IsTrue(views > 0 && views < cards, $"{context}: rows of N made {views} card views for the Pipeline's {cards} cards.");
        }
        else
        {
            Assert.AreEqual(cards, views, $"{context}: the Pipeline's card views.");
        }
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
