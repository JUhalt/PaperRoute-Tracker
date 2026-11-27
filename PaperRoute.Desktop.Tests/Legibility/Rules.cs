using System.Globalization;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One fault a rule found: the rule, the theme case it ran in, what it judged
// (for R1, "<ink> on <surface>"), and the one line a reader is shown.
internal sealed record Finding(string Rule, string ThemeCase, string Subject, string Line)
{
    public override string ToString() => Line;
}

// The legibility rules (plan :164), one plain function each (the harness
// design, part 2.1). A rule takes colours, or later a frame and control
// bounds, and returns its findings; it never asserts. The tests assert, so a
// teeth test can hand a rule a bad input and read what it reports.
// Limits of headless: HeadlessLimits.md
internal static class Rules
{
    // R1: every ink on its surface at 4.5:1, muted and disabled text included
    // (question 5). The colours are one theme case's, read from the token file
    // (TokenFile.Colors), so a spoiled copy of the file runs through the same code.
    public static IReadOnlyList<Finding> R1(string themeCase, IReadOnlyDictionary<string, Color> colors, IEnumerable<TokenPair> pairs)
    {
        var findings = new List<Finding>();
        foreach (TokenPair pair in pairs)
        {
            double ratio = Contrast.ContrastRatio(colors[pair.Ink], colors[pair.Surface]);
            if (ratio < Thresholds.Text)
            {
                string subject = $"{pair.Ink} on {pair.Surface}";
                string line = string.Create(CultureInfo.InvariantCulture, $"{themeCase}: {subject} is {ratio:F2}:1 ({pair.Where})");
                findings.Add(new Finding("R1", themeCase, subject, line));
            }
        }

        return findings;
    }
}
