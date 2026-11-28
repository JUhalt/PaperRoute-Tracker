using Avalonia.Media;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// The rules' teeth (the harness design, part 2.4): each test hands a rule a
// deliberately bad input and expects the rule to report it, through the same
// code the real input takes.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class RuleTeethTests
{
    public TestContext TestContext { get; set; } = null!;

    // The plan's acceptance line "a deliberately low-contrast token fails R1":
    // a copy of the real Tokens.axaml whose Light MutedTextBrush is #B8C0C8
    // instead of #64717D, read by the same TokenFile reader and judged by the
    // same Rules.R1 over the same pairs as the real file. R1 must name muted
    // text on the card (1.84:1) and on the board (1.71:1), in Light only, and
    // no pair whose ink is another token. The real file, run the same way,
    // gives nothing, so the one changed value is the whole difference.
    [TestMethod]
    public void R1_FailsADeliberatelyLowContrastToken()
    {
        const string good = "<SolidColorBrush x:Key=\"MutedTextBrush\" Color=\"#64717D\" />";
        const string bad = "<SolidColorBrush x:Key=\"MutedTextBrush\" Color=\"#B8C0C8\" />";
        string real = File.ReadAllText(TokenFile.TokensPath);
        Assert.AreEqual(2, real.Split(good).Length, "The real token file must hold Light's MutedTextBrush, #64717D, exactly once.");

        string folder = Path.Combine(AssemblySetup.StorageRoot, "r1-teeth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, "Tokens.axaml");
        File.WriteAllText(copy, real.Replace(good, bad, StringComparison.Ordinal));
        Assert.AreEqual(TokenFile.ParseHex("#B8C0C8"), TokenFile.Colors("Light", copy)["MutedTextBrush"], "The copy's Light muted text is the bad colour.");
        Assert.AreEqual(TokenFile.Colors("Dark")["MutedTextBrush"], TokenFile.Colors("Dark", copy)["MutedTextBrush"], "The copy's Dark muted text is unchanged.");

        List<Finding> findings = RunR1(copy);
        foreach (Finding finding in findings)
        {
            TestContext.WriteLine(finding.Line);
        }

        Assert.IsEmpty(RunR1(null), "R1 must pass the real token file, or the copy's one change is not the only fault.");
        Assert.IsTrue(findings.Any(finding => finding.Line.StartsWith("Light: MutedTextBrush on CardBackgroundBrush is 1.84:1", StringComparison.Ordinal)),
            "R1 did not report muted text on the card at 1.84:1. It reported:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        Assert.IsTrue(findings.Any(finding => finding.Line.StartsWith("Light: MutedTextBrush on BoardBackgroundBrush is 1.71:1", StringComparison.Ordinal)),
            "R1 did not report muted text on the board at 1.71:1. It reported:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        Finding[] others = findings.Where(finding => finding.ThemeCase != "Light" || !finding.Subject.StartsWith("MutedTextBrush on ", StringComparison.Ordinal)).ToArray();
        Assert.IsEmpty(others, "R1 reported a pair the bad token is not in:" + Environment.NewLine + string.Join(Environment.NewLine, others));
    }

    // R1 over every declared pair in every theme case it runs in, on the
    // given token file (the real one when null).
    private static List<Finding> RunR1(string? tokensPath)
    {
        var findings = new List<Finding>();
        foreach (string variant in R1Tests.Variants)
        {
            IReadOnlyDictionary<string, Color> colors = TokenFile.Colors(variant, tokensPath);
            findings.AddRange(Rules.R1(variant, colors, TokenPairs.All));
        }

        return findings;
    }
}
