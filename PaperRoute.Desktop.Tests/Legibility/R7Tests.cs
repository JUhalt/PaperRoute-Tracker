using System.Globalization;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R7, drawn text at 4.5:1 by its strongest pixel (the harness design, part
// 2.4), as a sweep over every subject the harness can build
// (Sweep.Subjects), in the six theme cases.
//
// MEASURE-ONLY for now (the maintainer's answer to question 2 of
// 2026-11-27): a result within a small margin of 4.5:1 will count as a pass,
// and the margin's size is set only after both CI runners have measured. So
// the sweep judges nothing yet: for every text element of every subject and
// case it writes the token ratio where one exists, the strongest-pixel
// ratio, the full-ink pixel count and the margin over 4.5, the thinnest
// margin first, to legibility-R7-<case>.txt beside the pictures, the same
// form on every system, so the two runners' files can be laid side by side.
// R7's judging function (Rules.R7) is built and proved by R7_FindsFaintText;
// the commit that turns the sweep's verdict on comes later.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R7Tests : StorageCheckedTests
{
    internal static readonly string[] Columns =
    [
        "theme case", "subject", "element", "text", "ink", "ink token", "surface", "surface token",
        "token ratio", "strongest ratio", "full ink", "margin", "bounds",
    ];

    public TestContext TestContext { get; set; } = null!;

    // P_R7_CardTitlesHaveTheirContrastOnScreenInLight and ...InDark judge the
    // Board's titles as before. This sweep absorbs them only once it judges
    // and the Board subject also shows the Published and File Drawer shelves:
    // the subject is shown as it opens, on the Pipeline shelf, while P-R7
    // checks the seven titles of all three shelves. The verdict commit adds
    // two subjects for Page/Board whose Shown sets SelectedShelfIndex to 1
    // and to 2.
    [TestMethod]
    public void R7_EveryTextHasItsContrastByItsStrongestPixel()
    {
        CollectionAssert.IsSubsetOf(
            TargetSurfaceCatalog.Surfaces.Select(surface => surface.Key).ToList(),
            Sweep.Subjects.Select(subject => subject.Name).ToList(),
            "The sweep shows every built target surface.");

        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            var texts = new List<DrawnText>();
            foreach (SweepSubject subject in Sweep.Subjects)
            {
                SweepFrame read = Sweep.Read(subject, themeCase, TestContext);
                Assert.IsNotEmpty(read.Texts, $"{themeCase}: {subject.Name}: the sweep found no text drawn in full.");
                TestContext.WriteLine($"{themeCase}: {subject.Name}: {read.Texts.Count} texts measured, {read.HiddenTexts} partly hidden and left out.");
                texts.AddRange(read.Texts);
            }

            string path = Write(Pictures.Directory(), themeCase.Name, texts);
            string[] lines = File.ReadAllLines(path);
            Assert.AreEqual(string.Join('\t', Columns), lines[0], "The first line names the columns.");
            Assert.HasCount(texts.Count + 1, lines, $"{themeCase}: one line per text.");
            TestContext.WriteLine($"Measured values saved: {path}; {texts.Count(text => text.Margin < 0)} texts under {Thresholds.Text}:1 by their strongest pixel, not judged until the margin is set.");
            foreach (string line in lines.Skip(1).Take(5))
            {
                TestContext.WriteLine("  " + line);
            }
        }
    }

    // The rule's teeth: on a card, a 12 px Regular text in valid tokens
    // (primary text on the card, which R1 passes) at Opacity 0.35. R1 passes
    // and the token ratio is the steady text's; only the pixels are faint,
    // and R7 must report that text and no other.
    [TestMethod]
    public void R7_FindsFaintText()
    {
        Assert.IsEmpty(
            Rules.R1("Light", TokenFile.Colors("Light"), [new TokenPair("the R7 teeth text", "PrimaryTextBrush", "CardBackgroundBrush")]),
            "R1 passes the teeth text's tokens.");

        SweepFrame read = Sweep.Read(TeethGallery.FaintText, ThemeCase.Light, TestContext);
        DrawnText steady = read.Texts.Single(text => text.Element.EndsWith("TextBlock#Steady", StringComparison.Ordinal));
        DrawnText faint = read.Texts.Single(text => text.Element.EndsWith("TextBlock#Faint", StringComparison.Ordinal));
        foreach (DrawnText text in read.Texts)
        {
            TestContext.WriteLine(string.Join(" | ", Row(text)));
        }

        Assert.AreEqual("PrimaryTextBrush", faint.InkToken, "The faint text's ink is a token.");
        Assert.AreEqual("CardBackgroundBrush", faint.SurfaceToken, "The faint text's surface is a token.");
        Assert.IsNotNull(faint.TokenRatio, "The faint text has a token ratio.");
        Assert.AreEqual(steady.TokenRatio, faint.TokenRatio, "Both texts have one token pair.");
        Assert.IsGreaterThanOrEqualTo(Thresholds.Text, faint.TokenRatio ?? 0, "By its tokens the faint text passes.");
        Assert.IsLessThan(steady.Strongest, faint.Strongest, "The faint text's pixels are fainter than the steady text's.");

        IReadOnlyList<Finding> findings = Rules.R7(read.Texts);
        foreach (Finding finding in findings)
        {
            TestContext.WriteLine(finding.Line);
        }

        Assert.HasCount(1, findings, "Only the faint text:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        Assert.AreEqual("Teeth/FaintText: " + faint.Element + " \"Faint text at Opacity 0.35\"", findings[0].Subject);
        StringAssert.StartsWith(findings[0].Line, "Light: Teeth/FaintText: ");
        StringAssert.Contains(findings[0].Line, "by its strongest pixel");
    }

    // Writes every text's measurements, the thinnest margin first (then by
    // subject and element, so the order is the same wherever the margins
    // are), to legibility-R7-<case>.txt in the folder; returns its path.
    private static string Write(string folder, string themeCase, IEnumerable<DrawnText> texts) =>
        Measurements.Write(
            folder,
            "R7",
            themeCase,
            Columns,
            texts.OrderBy(text => text.Margin).ThenBy(text => text.Subject, StringComparer.Ordinal).ThenBy(text => text.Element, StringComparer.Ordinal)
                .Select(Row));

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
        Measurements.Number(text.Margin),
        string.Create(CultureInfo.InvariantCulture, $"{text.Bounds.X},{text.Bounds.Y},{text.Bounds.Width},{text.Bounds.Height}"),
    ];
}
