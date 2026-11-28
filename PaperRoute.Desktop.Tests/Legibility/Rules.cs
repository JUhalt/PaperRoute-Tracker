using System.Globalization;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One fault a rule found: the rule, the theme case it ran in, what it judged
// (for R1, "<ink> on <surface>"), and the one line a reader is shown.
internal sealed record Finding(string Rule, string ThemeCase, string Subject, string Line)
{
    public override string ToString() => Line;
}

// One value R1 measures: a declared pair's contrast ratio in one theme case.
internal readonly record struct PairRatio(string ThemeCase, TokenPair Pair, double Ratio);

// The legibility rules (plan :164), one plain function each (the harness
// design, part 2.1). A rule takes colours, or later a frame and control
// bounds, and returns its findings; it never asserts. The tests assert, so a
// teeth test can hand a rule a bad input and read what it reports.
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    // R1: every ink on its surface at 4.5:1, muted and disabled text included
    // (question 5). The colours are one theme case's, read from the token file
    // (TokenFile.Colors), so a spoiled copy of the file runs through the same code.
    public static IReadOnlyList<Finding> R1(string themeCase, IReadOnlyDictionary<string, Color> colors, IEnumerable<TokenPair> pairs) =>
        R1(R1Ratios(themeCase, colors, pairs));

    // R1's findings from its measured ratios: the pairs under 4.5:1.
    public static IReadOnlyList<Finding> R1(IEnumerable<PairRatio> ratios)
    {
        var findings = new List<Finding>();
        foreach (PairRatio measured in ratios.Where(measured => measured.Ratio < Thresholds.Text))
        {
            string subject = $"{measured.Pair.Ink} on {measured.Pair.Surface}";
            string line = string.Create(CultureInfo.InvariantCulture, $"{measured.ThemeCase}: {subject} is {measured.Ratio:F2}:1 ({measured.Pair.Where})");
            findings.Add(new Finding("R1", measured.ThemeCase, subject, line));
        }

        return findings;
    }

    // What R1 measures: every pair's ratio in one theme case, passing or not.
    // The R1 runs write these to legibility-R1-<case>.txt (Measurements).
    public static IReadOnlyList<PairRatio> R1Ratios(string themeCase, IReadOnlyDictionary<string, Color> colors, IEnumerable<TokenPair> pairs) =>
        pairs.Select(pair => new PairRatio(themeCase, pair, Contrast.ContrastRatio(colors[pair.Ink], colors[pair.Surface]))).ToList();
}
