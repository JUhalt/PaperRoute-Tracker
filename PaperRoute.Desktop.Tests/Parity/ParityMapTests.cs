using System.Text;

namespace PaperRoute.Desktop.Tests.Parity;

// The parity-map tests (harness design 2.3 and 5, "Parity"): the target
// catalog and the parity map are complete, their states match the code
// catalog, and every WinForms view whose place is not built yet is listed by
// the build issue that builds it. They prove the map complete and current,
// not right: the rows are judgement read from the target map's tables, each
// with where it was read.
[TestClass]
public sealed class ParityMapTests
{
    // The file beside the pictures (Pictures.Directory) that lists every view
    // not built yet, so the CI artifact carries the list.
    public const string UnbuiltListName = "parity-unbuilt.txt";

    // Inventory rows no target surface covers, each with its reason (critic N1).
    private static readonly IReadOnlyDictionary<int, string> s_rowsNoTargetSurfaceCovers = new Dictionary<int, string>
    {
        [96] = "System file pickers (20 call sites) stay the platform's own pickers (5d). PaperRoute draws none of them, so no factory can show one, and headless has no native dialogs.",
        [97] = "The startup reminder stays a system notification the platform shows outside the window (5a). Its in-window summary is the notice strip's, part of Window/Main.",
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EveryWinFormsViewHasADestinationOrANamedReason()
    {
        ParityMap map = ParityMap.Load();
        IReadOnlyList<string> findings = map.ViewsWithoutADestinationOrReason();
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));
        Assert.HasCount(ParityMap.CatalogParts.Values.Sum(), map.Views, "The map has one row per WinForms catalog key.");
    }

    [TestMethod]
    public void EveryDestinationIsATargetSurface()
    {
        IReadOnlyList<string> findings = ParityMap.Load().DestinationsThatAreNotSurfaces();
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    [TestMethod]
    public void EveryTargetSurfaceIsBuiltOrPlanned()
    {
        IReadOnlyList<string> findings = ParityMap.Load().SurfacesNeitherBuiltNorPlanned();
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));

        // A build issue is one of the opened build issues, written "#NNN";
        // the plan's own numbering, once the issues are open, is not one.
        Assert.IsTrue(ParityMap.IsBuildIssue("#418") && ParityMap.IsBuildIssue("#439"), "The first and last build issues are build issues.");
        string[] notBuildIssues = ["plan 8.5", "#417", "#440", "#0420", "#", "420", "#420 "];
        Assert.IsEmpty(notBuildIssues.Where(ParityMap.IsBuildIssue), "Not build issues: " + string.Join(", ", notBuildIssues.Where(ParityMap.IsBuildIssue)));
    }

    // The state column cannot go stale: built means a factory in
    // TargetSurfaceCatalog, and a factory means built (critic S9).
    [TestMethod]
    public void EveryBuiltStateMatchesTheCodeCatalog()
    {
        ParityMap map = ParityMap.Load();
        string[] factories = [.. TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key)];
        IReadOnlyList<string> findings = map.BuiltStatesThatDifferFrom(factories);
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));

        // The check sees a missing factory and a factory the data does not list.
        Assert.IsNotEmpty(map.BuiltStatesThatDifferFrom([]), "A built surface with no factory is reported.");
        Assert.IsNotEmpty(map.BuiltStatesThatDifferFrom([.. factories, "Board"]), "A factory whose key is not in the data is reported.");
    }

    // One row per closed interface issue: its last pull request adds the row
    // and quotes this test as its proof of "the Avalonia build's view catalog
    // matches the target map ... for the target-map rows it builds"
    // (Avalonia-Migration-Plan.md section 8). MSTest refuses a data-driven
    // test with no row, so issue 3 (#418), which builds no target surface,
    // holds the first. Each row checks that its issue is named as the data
    // names build issues and that no surface it builds is still planned, and
    // then, on a small map made here rather than the live data (which a later
    // issue changes), that the check reports a surface of that issue still
    // planned and leaves a built one out.
    [TestMethod]
    [DataRow("#418")]
    public void TheSurfacesThisIssueBuildsAreBuilt(string issue)
    {
        Assert.IsTrue(ParityMap.IsBuildIssue(issue), $"\"{issue}\" is not a build issue as {ParityMap.SurfacesFile} names them.");
        IReadOnlyList<string> findings = ParityMap.Load().SurfacesStillPlannedFor(issue);
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));

        ParityMap probe = ParityMap.Parse(
            "key\tmap rows\tbuilt by\tstate\tread from\tnote\n"
            + $"Page/StillPlanned\t3\t{issue}\tplanned\there\n"
            + $"Page/AlreadyBuilt\t4\t{issue}\tbuilt\there\n",
            "part\tkey\tdestination\tmap row\tread from\treason\n"
            + "dialog\tStillPlanned\tPage/StillPlanned\t3\there\n"
            + "dialog\tAlreadyBuilt\tPage/AlreadyBuilt\t4\there\n");
        IReadOnlyList<string> planned = probe.SurfacesStillPlannedFor(issue);
        Assert.HasCount(1, planned, "Only the surface still planned is reported: " + string.Join(" | ", planned));
        Assert.Contains("Page/StillPlanned", planned[0]);
    }

    [TestMethod]
    public void EveryTargetSurfaceIsReachedOrSaysWhyNot()
    {
        IReadOnlyList<string> findings = ParityMap.Load().UnreachedSurfacesWithoutANote();
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    [TestMethod]
    public void EveryInventoryRowIsCoveredOrNamed()
    {
        ParityMap map = ParityMap.Load();
        IReadOnlyList<string> findings = map.InventoryRowsNotCovered(s_rowsNoTargetSurfaceCovers);
        Assert.IsEmpty(findings, Environment.NewLine + string.Join(Environment.NewLine, findings));

        // The check sees an uncovered row that is not named, and a named row the data covers.
        Assert.IsNotEmpty(map.InventoryRowsNotCovered(new Dictionary<int, string> { [96] = "file pickers" }), "Row 97, not named, is reported.");
        Assert.IsNotEmpty(map.InventoryRowsNotCovered(new Dictionary<int, string>(s_rowsNoTargetSurfaceCovers) { [3] = "the Board" }), "A named row the data covers is reported as stale.");
    }

    // "List every unmapped WinForms view", the reading that does not fail
    // yet: every view whose destination is not built is listed, grouped by
    // the build issue that builds it, in the test log and in
    // parity-unbuilt.txt beside the pictures. Issue 21 adds the line that
    // turns a non-empty list into a failure.
    [TestMethod]
    public void TheParityMapListsEveryWinFormsViewNotYetBuilt()
    {
        ParityMap map = ParityMap.Load();
        IReadOnlyList<UnbuiltView> unbuilt = map.UnbuiltViews();
        IReadOnlyList<ParityRow> built = map.BuiltViews();
        int removed = map.Views.Count(view => view.Destination == ParityMap.Removed);
        int catalog = ParityMap.CatalogParts.Values.Sum();

        Assert.AreEqual(catalog, unbuilt.Count + built.Count + removed, $"Listed ({unbuilt.Count}), built ({built.Count}) and removed ({removed}) are the catalog's {catalog} views.");
        string[] withoutIssue = [.. unbuilt.Where(view => !ParityMap.IsBuildIssue(view.BuiltBy)).Select(view => $"{view.Part} \"{view.Key}\"")];
        Assert.IsEmpty(withoutIssue, "Listed views without a build issue: " + string.Join(", ", withoutIssue));

        var text = new StringBuilder();
        text.AppendLine("# WinForms views whose place in the Avalonia interface is not built yet, by the build issue that builds it.");
        text.AppendLine($"# From parity-map.tsv and target-surfaces.tsv: {unbuilt.Count} not built, {built.Count} built, {removed} removed; {catalog} in all.");
        foreach (IGrouping<string, UnbuiltView> issue in unbuilt.GroupBy(view => view.BuiltBy).OrderBy(group => ParityMap.BuildIssueOrder(group.Key)))
        {
            text.AppendLine();
            text.AppendLine($"{issue.Key} ({issue.Count()})");
            foreach (UnbuiltView view in issue)
            {
                text.AppendLine($"  {view.Part}\t{view.Key}\t-> {view.Destination}");
            }
        }

        string folder = Pictures.Directory();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, UnbuiltListName);
        File.WriteAllText(path, text.ToString());
        TestContext.WriteLine(text.ToString());
        TestContext.WriteLine($"Written to {path}");
    }

    [TestMethod]
    public void AMalformedOrMissingRowIsReported()
    {
        string surfaces = File.ReadAllText(Path.Combine(ParityMap.Folder, ParityMap.SurfacesFile));
        string map = File.ReadAllText(Path.Combine(ParityMap.Folder, ParityMap.MapFile));

        // A malformed line fails the reader with its file and line number.
        string[] mapLines = map.ReplaceLineEndings("\n").Split('\n');
        int form1 = Array.FindIndex(mapLines, line => line.StartsWith("dialog\tForm1\t", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, form1, "The map has a row for Form1.");

        string[] shortLine = [.. mapLines];
        shortLine[form1] = "dialog\tForm1\tPage/Board";
        FormatException malformed = Assert.ThrowsExactly<FormatException>(() => ParityMap.Parse(surfaces, string.Join("\n", shortLine)));
        Assert.Contains($"{ParityMap.MapFile}:{form1 + 1}: 3 cells", malformed.Message);

        string[] emptyCell = [.. mapLines];
        emptyCell[form1] = "dialog\tForm1\t\t3\tsomewhere";
        FormatException empty = Assert.ThrowsExactly<FormatException>(() => ParityMap.Parse(surfaces, string.Join("\n", emptyCell)));
        Assert.Contains($"{ParityMap.MapFile}:{form1 + 1}: the cell \"destination\" is empty", empty.Message);

        string[] surfaceLines = surfaces.ReplaceLineEndings("\n").Split('\n');
        int board = Array.FindIndex(surfaceLines, line => line.StartsWith("Page/Board\t3,12\t", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, board, "The catalog has a row for Page/Board with rows 3 and 12.");
        surfaceLines[board] = surfaceLines[board].Replace("\t3,12\t", "\t3,98\t", StringComparison.Ordinal);
        FormatException badRows = Assert.ThrowsExactly<FormatException>(() => ParityMap.Parse(string.Join("\n", surfaceLines), map));
        Assert.Contains($"{ParityMap.SurfacesFile}:{board + 1}: the cell \"map rows\" is \"3,98\"", badRows.Message);

        // A missing row: a part one short of the catalog.
        string[] missing = [.. mapLines.Where((_, index) => index != form1)];
        Assert.Contains("62 dialog rows; the WinForms catalog has 63", string.Join("\n", ParityMap.Parse(surfaces, string.Join("\n", missing)).ViewsWithoutADestinationOrReason()));

        // A removed view with no reason, and a destination that is no surface.
        string[] noReason = [.. mapLines];
        noReason[form1] = "dialog\tForm1\tremoved\t3\tsomewhere";
        Assert.Contains("dialog \"Form1\" is removed with no reason", string.Join("\n", ParityMap.Parse(surfaces, string.Join("\n", noReason)).ViewsWithoutADestinationOrReason()));

        string[] unknown = [.. mapLines];
        unknown[form1] = "dialog\tForm1\tPage/Nowhere\t3\tsomewhere";
        Assert.Contains("goes to \"Page/Nowhere\"", string.Join("\n", ParityMap.Parse(surfaces, string.Join("\n", unknown)).DestinationsThatAreNotSurfaces()));
    }
}
