using System.Globalization;
using PaperRoute.Desktop.Tests.Board;

namespace PaperRoute.Desktop.Tests.Legibility;

// R5, no light parts in Dark (the harness design, part 2.4), as a sweep over
// every subject the harness can build (Sweep.Subjects), with the empty
// allow-list (R5AllowList), judging. Every part's two readings go to
// legibility-r5-dark.txt beside the pictures, the thinnest margin first.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R5Tests : StorageCheckedTests
{
    private static readonly string[] s_columns =
        ["theme case", "subject", "part", "brush", "brush luminance", "own colour", "own luminance", "margin", "bounds"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void R5_NoLightPartsOnAnySurfaceInDark()
    {
        CollectionAssert.IsSubsetOf(
            TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList(),
            Sweep.Subjects.Select(subject => subject.Name).ToList(),
            "The sweep shows every built target surface.");

        var parts = new List<PaintedPart>();
        foreach (SweepSubject subject in Sweep.Subjects)
        {
            SweepFrame read = Sweep.Read(subject, ThemeCase.Dark, TestContext);
            Assert.IsNotEmpty(read.Parts, $"{subject.Name}: the sweep found no part that paints a background.");
            TestContext.WriteLine($"{subject.Name}: {read.Parts.Count} parts that paint a background.");
            parts.AddRange(read.Parts);
        }

        string path = Write(Pictures.Directory(), ThemeCase.Dark.Name, parts);
        TestContext.WriteLine($"Measured values saved: {path}");
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        List<Finding> findings = Rules.R5(ThemeCase.Dark.Name, parts, R5AllowList.Entries).Where(finding => !KnownFailures.Lists(known, finding)).ToList();
        Assert.IsEmpty(findings, "R5, light parts in Dark:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The rule's teeth: on the Dark page, a panel filled with #E0E0E0
    // (luminance 0.745) must be named by both readings, its brush and its own
    // pixels; a panel drawn in the same colour by a gradient, which no brush
    // reading can name, must be named by its own pixels alone; and nothing
    // else on the page may be named.
    [TestMethod]
    public void R5_FindsALightPanelInDark()
    {
        SweepFrame read = Sweep.Read(TeethGallery.LightPanel, ThemeCase.Dark, TestContext);
        IReadOnlyList<Finding> findings = Rules.R5(ThemeCase.Dark.Name, read.Parts, R5AllowList.Entries);
        foreach (Finding finding in findings)
        {
            TestContext.WriteLine(finding.Line);
        }

        Assert.HasCount(3, findings, "Both readings of the solid panel, the own pixels of the gradient panel, and nothing else:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        CollectionAssert.IsSubsetOf(
            findings.Select(finding => finding.Subject).Distinct().ToList(),
            new[] { "Teeth/LightPanel: Border#LightPanel", "Teeth/LightPanel: Border#LightGradient" },
            "Only the two light panels are named.");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "Dark: Teeth/LightPanel: Border#LightPanel paints by its brush #E0E0E0, luminance 0.745, above 0.6",
                "Dark: Teeth/LightPanel: Border#LightPanel shows in its own pixels #E0E0E0, luminance 0.745, above 0.6",
                "Dark: Teeth/LightPanel: Border#LightGradient shows in its own pixels #E0E0E0, luminance 0.745, above 0.6",
            },
            findings.Select(finding => finding.Line).ToArray());
    }

    // The allow-list's teeth: an entry that covers no part fails as stale,
    // while an entry that covers the light panels takes them out of the run
    // and is not stale.
    [TestMethod]
    public void R5_AStaleAllowListEntryFails()
    {
        SweepFrame read = Sweep.Read(TeethGallery.LightPanel, ThemeCase.Dark, TestContext);
        var covering = new R5Allowance("the teeth's light panels", "a test of the allow-list", "(none)", part => part.Part is "Border#LightPanel" or "Border#LightGradient");
        var stale = new R5Allowance("a panel no surface has", "a test of the allow-list", "(none)", part => part.Part == "Border#NoSuchPanel");

        Assert.IsEmpty(Rules.R5(ThemeCase.Dark.Name, read.Parts, [covering]), "An entry that covers the light panels takes them out of the run.");
        IReadOnlyList<Finding> findings = Rules.R5(ThemeCase.Dark.Name, read.Parts, [covering, stale]);
        Assert.AreEqual(
            "Dark: the R5 allow-list entry \"a panel no surface has\" covers no part: it is stale.",
            string.Join(Environment.NewLine, findings),
            "The entry that covers nothing, and only it, is reported.");
    }

    // What R5 reports today on every subject, listed failures included
    // (KnownFailuresTests.KnownFailuresAreCurrent), with no measurement file
    // written (the sweep's pictures are saved again).
    internal static IReadOnlyList<Finding> CurrentFindings(TestContext context)
    {
        var parts = new List<PaintedPart>();
        foreach (SweepSubject subject in Sweep.Subjects)
        {
            parts.AddRange(Sweep.Read(subject, ThemeCase.Dark, context).Parts);
        }

        return Rules.R5(ThemeCase.Dark.Name, parts, R5AllowList.Entries);
    }

    // Writes every part's readings, the thinnest margin first, to
    // legibility-r5-<case>.txt in the folder; returns the file's path.
    private static string Write(string folder, string themeCase, IEnumerable<PaintedPart> parts) =>
        Measurements.Write(
            folder,
            "R5",
            themeCase,
            s_columns,
            parts.OrderBy(part => part.Margin).ThenBy(part => part.Subject, StringComparer.Ordinal).ThenBy(part => part.Part, StringComparer.Ordinal)
                .Select(part => (IReadOnlyList<string>)
                [
                    part.ThemeCase,
                    part.Subject,
                    part.Part,
                    part.Brush is { } brush ? Rules.Hex(brush) : "-",
                    part.Brush is { } lit ? Luminance(lit) : "-",
                    part.Own is { } own ? Rules.Hex(own) : "-",
                    part.Own is { } shown ? Luminance(shown) : "-",
                    Luminance(part.Margin),
                    string.Create(CultureInfo.InvariantCulture, $"{part.Bounds.X},{part.Bounds.Y},{part.Bounds.Width},{part.Bounds.Height}"),
                ]));

    // A luminance, or a margin of luminance, to three decimals in the invariant culture.
    private static string Luminance(Avalonia.Media.Color colour) => Luminance(Contrast.RelativeLuminance(colour));

    private static string Luminance(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
