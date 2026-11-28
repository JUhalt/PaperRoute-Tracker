using System.Globalization;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One value R3 measures: how far two route kinds' patterns differ in one
// theme case (the share of positions, from 0 to 1), and the contrast of their
// commonest colours.
internal readonly record struct PatternMeasure(string ThemeCase, string One, string Other, double Differing, double Ratio)
{
    // "<one> and <other>: <n> % of positions differ; commonest colours <r>:1".
    public string Line => string.Create(CultureInfo.InvariantCulture,
        $"{One} and {Other}: {Differing * 100:F1} % of positions differ; commonest colours {Ratio:F2}:1");
}

// R3, route kinds told apart without colour (plan :164; harness design 2.4),
// as today's rule (WorkflowThemeKeyboardSmokeTests.vb,
// RouteMapKinds_CanBeToldApartWithoutColor): each kind is painted alone; a
// pixel is in a swatch's pattern within 2:1 of the swatch's commonest colour;
// two swatches share a pattern when under 10 % or over 90 % of positions
// differ; a pair fails when it shares a pattern and either a contrast theme is
// on (the colours are the user's) or its commonest colours are under 3:1.
// Input: swatches of equal size. Output: findings (Rules.cs), one line each,
// whose subject is the pair, "<one> and <other>".
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    public static List<Finding> R3Findings(string themeCase, IReadOnlyList<(string Name, Color[] Pixels)> swatches, bool contrast) =>
        R3Findings(R3Measures(themeCase, swatches), contrast);

    // R3's findings from its measurements: the pairs that share a pattern,
    // under a contrast theme or under 3:1.
    public static List<Finding> R3Findings(IEnumerable<PatternMeasure> measures, bool contrast)
    {
        ArgumentNullException.ThrowIfNull(measures);
        var findings = new List<Finding>();
        foreach (PatternMeasure measured in measures)
        {
            bool same = measured.Differing < Thresholds.R3SamePatternUnder || measured.Differing > Thresholds.R3SamePatternOver;
            if (same && (contrast || measured.Ratio < Thresholds.R3Colours))
            {
                findings.Add(new Finding("R3", measured.ThemeCase, $"{measured.One} and {measured.Other}",
                    measured.Line + (contrast ? ": the same pattern under a contrast theme" : ": the same pattern and under 3:1")));
            }
        }

        return findings;
    }

    // What R3 measures: every pair of swatches, in order, passing or not. The
    // R3 run writes these to legibility-r3-<case>.txt (Measurements).
    public static IReadOnlyList<PatternMeasure> R3Measures(string themeCase, IReadOnlyList<(string Name, Color[] Pixels)> swatches)
    {
        ArgumentNullException.ThrowIfNull(swatches);
        var measures = new List<PatternMeasure>();
        for (int first = 0; first < swatches.Count - 1; first++)
        {
            for (int second = first + 1; second < swatches.Count; second++)
            {
                (string oneName, Color[] one) = swatches[first];
                (string otherName, Color[] other) = swatches[second];
                if (one.Length != other.Length || one.Length == 0)
                {
                    throw new ArgumentException($"The swatches of {oneName} ({one.Length} pixels) and {otherName} ({other.Length}) are not one size.", nameof(swatches));
                }

                measures.Add(new PatternMeasure(themeCase, oneName, otherName, R3Differing(one, other), Contrast.ContrastRatio(Commonest(one), Commonest(other))));
            }
        }

        return measures;
    }

    // The share of positions in one swatch's pattern and not the other's, or
    // the other way round.
    private static double R3Differing(Color[] one, Color[] other)
    {
        Color oneColour = Commonest(one);
        Color otherColour = Commonest(other);
        int differing = 0;
        for (int index = 0; index < one.Length; index++)
        {
            bool inOne = Contrast.ContrastRatio(one[index], oneColour) < Thresholds.R3InPattern;
            bool inOther = Contrast.ContrastRatio(other[index], otherColour) < Thresholds.R3InPattern;
            if (inOne != inOther)
            {
                differing++;
            }
        }

        return differing / (double)one.Length;
    }

    private static Color Commonest(Color[] pixels) =>
        pixels.GroupBy(pixel => pixel).OrderByDescending(group => group.Count()).First().Key;
}
