using System.Globalization;
using PaperRoute.Desktop.Tests.Board;

namespace PaperRoute.Desktop.Tests.Legibility;

// R8, a disabled control's text at 3:1 on its surface (the harness design,
// part 2.4), as a sweep over every subject the harness can build
// (Sweep.Subjects), in the six theme cases, judging. Every disabled text's
// measurements go to legibility-r8-<case>.txt beside the pictures, in R7's
// columns, the margin being over 3:1, the thinnest margin first.
//
// The menu item's row (the card menu's disabled "Delete…", which Fluent's
// own disabled ink may leave under 3:1 in Light) joins in S4, with the
// Fluent bridge and the gallery's menu specimen; until then no menu is
// drawn in a window the sweep shows.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R8Tests : StorageCheckedTests
{
    // The gallery's off column (StyleGallery): every button role, the chip,
    // the text box, the drop-down and the tab, each shown disabled.
    private static readonly string[] s_offSpecimens =
    [
        "Button#SecondaryOff", "Button#PrimaryOff", "Button#QuietOff", "Button#DangerOff",
        "Button#ChipOff", "TextBox#TextBoxOff", "ComboBox#ComboOff", "TabStripItem#TabOff",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void R8_DisabledTextHasThreeToOne()
    {
        CollectionAssert.IsSubsetOf(
            TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList(),
            Sweep.Subjects.Select(subject => subject.Name).ToList(),
            "The sweep shows every built target surface.");

        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            var texts = new List<DrawnText>();
            foreach (SweepSubject subject in Sweep.Subjects)
            {
                SweepFrame read = Sweep.Read(subject, themeCase, TestContext);
                TestContext.WriteLine($"{themeCase}: {subject.Name}: {read.DisabledTexts.Count} disabled texts measured, {read.HiddenDisabledTexts} partly hidden and left out.");
                texts.AddRange(read.DisabledTexts);
            }

            foreach (string specimen in s_offSpecimens)
            {
                Assert.IsTrue(
                    texts.Any(text => text.Subject == "Styles/Gallery" && text.Element.Contains("/" + specimen + "/", StringComparison.Ordinal)),
                    $"{themeCase}: the gallery's {specimen} has no disabled text measured.");
            }

            string path = Write(Pictures.Directory(), themeCase.Name, texts);
            TestContext.WriteLine($"Measured values saved: {path}");
            findings.AddRange(Rules.R8(texts).Where(finding => !KnownFailures.Lists(known, finding)));
        }

        Assert.IsEmpty(findings, "R8, disabled text under 3:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The rule's teeth: in every theme case, two disabled buttons of one
    // role on the page, one of them faded by a style that sets Opacity 0.3.
    // Both texts have the same ink token, the off style's; only the faded
    // one's pixels are faint, and R8 must report it and nothing else.
    [TestMethod]
    public void R8_FindsAnOpacityDisabledLook()
    {
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            SweepFrame read = Sweep.Read(TeethGallery.FadedOff, themeCase, TestContext);
            foreach (DrawnText text in read.DisabledTexts)
            {
                TestContext.WriteLine(string.Join(" | ", Row(text)));
            }

            DrawnText off = read.DisabledTexts.Single(text => text.Element.StartsWith("Button#Off/", StringComparison.Ordinal));
            DrawnText faded = read.DisabledTexts.Single(text => text.Element.StartsWith("Button#Faded/", StringComparison.Ordinal));
            Assert.AreNotEqual("-", faded.InkToken, $"{themeCase}: the faded text's ink is a token.");
            Assert.AreEqual(off.InkToken, faded.InkToken, $"{themeCase}: both texts have the off style's ink.");
            Assert.IsLessThan(off.Strongest, faded.Strongest, $"{themeCase}: the faded text's pixels are fainter than the other's.");

            IReadOnlyList<Finding> findings = Rules.R8(read.DisabledTexts);
            foreach (Finding finding in findings)
            {
                TestContext.WriteLine(finding.Line);
            }

            Assert.HasCount(1, findings, $"{themeCase}: only the faded text:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
            Assert.AreEqual($"Teeth/FadedOff: {faded.Element} \"Faded\"", findings[0].Subject);
            StringAssert.StartsWith(findings[0].Line, $"{themeCase}: Teeth/FadedOff: ");
            StringAssert.Contains(findings[0].Line, "by its strongest pixel");
        }
    }

    // What R8 reports today in the six theme cases, listed failures included
    // (KnownFailuresTests.KnownFailuresAreCurrent): on every subject the
    // sweep shows, or on the subjects given, with no measurement file written
    // (the sweep's pictures are saved again).
    internal static IReadOnlyList<Finding> CurrentFindings(TestContext context, IReadOnlyList<SweepSubject>? subjects = null) =>
        Rules.R8(ThemeCase.All
            .SelectMany(themeCase => (subjects ?? Sweep.Subjects).SelectMany(subject => Sweep.Read(subject, themeCase, context).DisabledTexts))
            .ToList());

    // Writes every disabled text's measurements, the thinnest margin over
    // 3:1 first (then by subject and element), to legibility-r8-<case>.txt
    // in the folder; returns its path.
    private static string Write(string folder, string themeCase, IEnumerable<DrawnText> texts) =>
        Measurements.Write(
            folder,
            "R8",
            themeCase,
            R7Tests.Columns,
            texts.OrderBy(Margin).ThenBy(text => text.Subject, StringComparer.Ordinal).ThenBy(text => text.Element, StringComparer.Ordinal)
                .Select(Row));

    // How far a disabled text's strongest pixel sits above R8's line.
    private static double Margin(DrawnText text) => text.Strongest - Thresholds.DisabledText;

    private static IReadOnlyList<string> Row(DrawnText text) =>
    [
        text.ThemeCase,
        text.Subject,
        text.Element,
        text.Text,
        text.Ink is { } ink ? Rules.Hex(ink) : "-",
        text.InkToken,
        Rules.Hex(text.Surface),
        text.SurfaceToken,
        text.TokenRatio is { } ratio ? Measurements.Number(ratio) : "-",
        Measurements.Number(text.Strongest),
        text.FullInk.ToString(CultureInfo.InvariantCulture),
        Measurements.Number(Margin(text)),
        string.Create(CultureInfo.InvariantCulture, $"{text.Bounds.X},{text.Bounds.Y},{text.Bounds.Width},{text.Bounds.Height}"),
    ];
}
