using System.Globalization;
using PaperRoute.Desktop.Tests.Board;

namespace PaperRoute.Desktop.Tests.Legibility;

// R9, edges at 3:1 under contrast, focus included (the harness design, part
// 2.4), as sweeps over every subject the harness can build (Sweep.Subjects),
// in the four Windows contrast cases and the off-Windows fallback set only,
// as the plan says; judging. Edges and focus each write their measurements
// beside the pictures, the thinnest margin first: legibility-r9-edges-<case>.txt
// and legibility-r9-focus-<case>.txt.
//
// Menus, tooltips and the expanded scroll bar join in S4, with the Fluent
// bridge and the gallery's specimens of them.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R9Tests : StorageCheckedTests
{
    private static readonly string[] s_edgeColumns = ["theme case", "subject", "part", "surface", "edge ratio", "margin", "bounds"];

    private static readonly string[] s_focusColumns = ["theme case", "subject", "control", "window", "changed", "strongest ratio", "margin", "area", "not judged"];

    // The gallery's bordered specimens (part 2.4): a card, each button role,
    // a chip, a pill, a tag, the text box, the drop-down and the ⋯ button.
    private static readonly string[] s_borderedSpecimens =
    [
        "Border#CardAtRest", "Button#SecondaryAtRest", "Button#PrimaryAtRest", "Button#QuietAtRest", "Button#DangerAtRest",
        "Button#ChipNoTarget", "Border#PillStageIdea", "Border#TagTeal", "TextBox#TextBoxAtRest", "ComboBox#ComboAtRest", "Button#CardAtRestMore",
    ];

    // The gallery's focusable specimens: a button of each kind, a chip, a
    // toggle chip, the text box, the drop-down, a tab, and a card's ⋯,
    // title and route link.
    private static readonly string[] s_focusSpecimens =
    [
        "Button#Focused", "Button#PrimaryAtRest", "Button#QuietAtRest", "Button#Link", "Button#ChipNoTarget", "ToggleButton#ToggleChip",
        "TextBox#TextBoxAtRest", "ComboBox#ComboAtRest", "TabStripItem#TabPipeline", "Button#CardAtRestMore", "Button#CardAtRestTitle", "Button#CardAtRestViewRoute",
    ];

    // R9's cases: Windows' four contrast sets and the fallback set.
    internal static IReadOnlyList<ThemeCase> Cases { get; } = [.. ThemeCase.ContrastCases, ThemeCase.Fallback];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void R9_EdgesHaveThreeToOneUnderContrast()
    {
        CollectionAssert.IsSubsetOf(
            TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList(),
            Sweep.Subjects.Select(subject => subject.Name).ToList(),
            "The sweep shows every built target surface.");

        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in Cases)
        {
            var edges = new List<DrawnEdge>();
            foreach (SweepSubject subject in Sweep.Subjects)
            {
                SweepFrame read = Sweep.Read(subject, themeCase, TestContext);
                Assert.IsNotEmpty(read.Edges, $"{themeCase}: {subject.Name}: the sweep found no border drawn in full.");
                TestContext.WriteLine($"{themeCase}: {subject.Name}: {read.Edges.Count} edges measured, {read.HiddenEdges} partly hidden and left out.");
                edges.AddRange(read.Edges);
            }

            foreach (string specimen in s_borderedSpecimens)
            {
                Assert.IsTrue(
                    edges.Any(edge => edge.Subject == "Styles/Gallery" && Names(edge.Part, specimen)),
                    $"{themeCase}: the gallery's {specimen} has no edge measured.");
            }

            string path = Measurements.Write(
                Pictures.Directory(),
                "R9-edges",
                themeCase.Name,
                s_edgeColumns,
                edges.OrderBy(edge => edge.Margin).ThenBy(edge => edge.Subject, StringComparer.Ordinal).ThenBy(edge => edge.Part, StringComparer.Ordinal).Select(EdgeRow));
            TestContext.WriteLine($"Measured values saved: {path}");
            findings.AddRange(Rules.R9Edges(edges).Where(finding => !KnownFailures.Lists(known, finding)));
        }

        Assert.IsEmpty(findings, "R9, edges under 3:1 under contrast:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    [TestMethod]
    public void R9_FocusIsVisibleUnderContrast()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in Cases)
        {
            var changes = new List<FocusChange>();
            foreach (SweepSubject subject in Sweep.Subjects)
            {
                SweepFrame read = Sweep.Read(subject, themeCase, TestContext, walkFocus: true);
                int judged = read.Focus.Count(change => change.NotJudged is null);
                Assert.IsGreaterThan(0, judged, $"{themeCase}: {subject.Name}: the sweep judged no focusable control.");
                TestContext.WriteLine($"{themeCase}: {subject.Name}: {judged} focusable controls judged, {read.Focus.Count - judged} not judged.");
                changes.AddRange(read.Focus);
            }

            foreach (string specimen in s_focusSpecimens)
            {
                Assert.IsTrue(
                    changes.Any(change => change.Subject == "Styles/Gallery" && change.NotJudged is null && change.Control.EndsWith("/" + specimen, StringComparison.Ordinal)),
                    $"{themeCase}: the gallery's {specimen} was not judged.");
            }

            string path = Measurements.Write(
                Pictures.Directory(),
                "R9-focus",
                themeCase.Name,
                s_focusColumns,
                changes.OrderBy(change => change.NotJudged is null ? 0 : 1).ThenBy(change => change.Changed == 0 ? double.MinValue : change.Margin)
                    .ThenBy(change => change.Subject, StringComparer.Ordinal).ThenBy(change => change.Control, StringComparer.Ordinal).Select(FocusRow));
            TestContext.WriteLine($"Measured values saved: {path}");
            findings.AddRange(Rules.R9Focus(changes).Where(finding => !KnownFailures.Lists(known, finding)));
        }

        Assert.IsEmpty(findings, "R9, focus not shown at 3:1 under contrast:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The edges' teeth: in every R9 case, two empty panels with a 1 px
    // border, one in the card border and one in the window's own background
    // brush. R9 must report the second's edge and nothing else.
    [TestMethod]
    public void R9_FindsAnEdgeInTheWindowColour()
    {
        foreach (ThemeCase themeCase in Cases)
        {
            SweepFrame read = Sweep.Read(TeethGallery.WindowEdge, themeCase, TestContext);
            foreach (DrawnEdge edge in read.Edges)
            {
                TestContext.WriteLine(string.Join(" | ", EdgeRow(edge)));
            }

            DrawnEdge outlined = read.Edges.Single(edge => edge.Part == "Border#Outlined");
            Assert.IsGreaterThanOrEqualTo(Thresholds.Edge, outlined.Edge, $"{themeCase}: the outlined panel's edge stands out.");
            IReadOnlyList<Finding> findings = Rules.R9Edges(read.Edges);
            foreach (Finding finding in findings)
            {
                TestContext.WriteLine(finding.Line);
            }

            Assert.HasCount(1, findings, $"{themeCase}: only the edge in the window colour:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
            Assert.AreEqual("Teeth/WindowEdge: Border#WindowEdge", findings[0].Subject);
            StringAssert.StartsWith(findings[0].Line, $"{themeCase}: Teeth/WindowEdge: Border#WindowEdge's edge is 1.00:1");
        }
    }

    // The focus's teeth: in every R9 case, two buttons, one with its focus
    // ring and one with no focus adorner. R9 must report that taking the
    // focus changes none of the second's pixels, and nothing else.
    [TestMethod]
    public void R9_FindsAControlWithNoFocusVisual()
    {
        foreach (ThemeCase themeCase in Cases)
        {
            SweepFrame read = Sweep.Read(TeethGallery.NoFocusVisual, themeCase, TestContext, walkFocus: true);
            foreach (FocusChange change in read.Focus)
            {
                TestContext.WriteLine(string.Join(" | ", FocusRow(change)));
            }

            FocusChange ringed = read.Focus.Single(change => change.Control == "Button#Ringed");
            Assert.IsNull(ringed.NotJudged, $"{themeCase}: the ringed button is judged.");
            Assert.IsGreaterThan(0, ringed.Changed, $"{themeCase}: the ringed button's focus changes pixels.");
            Assert.IsNull(read.Focus.Single(change => change.Control == "Button#NoFocusVisual").NotJudged, $"{themeCase}: the button with no focus visual is judged.");
            IReadOnlyList<Finding> findings = Rules.R9Focus(read.Focus);
            foreach (Finding finding in findings)
            {
                TestContext.WriteLine(finding.Line);
            }

            Assert.HasCount(1, findings, $"{themeCase}: only the button with no focus visual:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
            Assert.AreEqual("Teeth/NoFocusVisual: Button#NoFocusVisual, focus", findings[0].Subject);
            Assert.AreEqual($"{themeCase}: Teeth/NoFocusVisual: Button#NoFocusVisual, focus changes no pixel", findings[0].Line);
        }
    }

    // What R9 reports today, edges and focus, in its cases, listed failures
    // included (KnownFailuresTests.KnownFailuresAreCurrent): on every subject
    // the sweep shows, or on the subjects given, with no measurement file
    // written (the sweep's pictures are saved again).
    internal static IReadOnlyList<Finding> CurrentFindings(TestContext context, IReadOnlyList<SweepSubject>? subjects = null)
    {
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in Cases)
        {
            foreach (SweepSubject subject in subjects ?? Sweep.Subjects)
            {
                SweepFrame read = Sweep.Read(subject, themeCase, context, walkFocus: true);
                findings.AddRange(Rules.R9Edges(read.Edges));
                findings.AddRange(Rules.R9Focus(read.Focus));
            }
        }

        return findings;
    }

    // Whether a sweep key names the specimen: the specimen itself, or a part inside it.
    private static bool Names(string key, string specimen) =>
        key.EndsWith("/" + specimen, StringComparison.Ordinal) || key.Contains("/" + specimen + "/", StringComparison.Ordinal);

    private static IReadOnlyList<string> EdgeRow(DrawnEdge edge) =>
    [
        edge.ThemeCase,
        edge.Subject,
        edge.Part,
        Rules.Hex(edge.Surface),
        Measurements.Number(edge.Edge),
        Measurements.Number(edge.Margin),
        Rect(edge.Bounds),
    ];

    private static IReadOnlyList<string> FocusRow(FocusChange change) =>
    [
        change.ThemeCase,
        change.Subject,
        change.Control,
        Rules.Hex(change.Window),
        change.Changed.ToString(CultureInfo.InvariantCulture),
        change.NotJudged is null && change.Changed > 0 ? Measurements.Number(change.Strongest) : "-",
        change.NotJudged is null && change.Changed > 0 ? Measurements.Number(change.Margin) : "-",
        Rect(change.Area),
        change.NotJudged ?? "-",
    ];

    private static string Rect(Avalonia.PixelRect rect) =>
        string.Create(CultureInfo.InvariantCulture, $"{rect.X},{rect.Y},{rect.Width},{rect.Height}");
}
