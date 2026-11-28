using System.Globalization;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// R3, route kinds told apart without colour (plan :164; harness design 2.4),
// as today's rule (WorkflowThemeKeyboardSmokeTests.vb,
// RouteMapKinds_CanBeToldApartWithoutColor): each kind is painted alone; a
// pixel is in a swatch's pattern within 2:1 of the swatch's commonest colour;
// two swatches share a pattern when under 10 % or over 90 % of positions
// differ; a pair fails when it shares a pattern and either a contrast theme is
// on (the colours are the user's) or its commonest colours are under 3:1.
// Input: swatches of equal size. Output: findings, one line each.
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    public static List<string> R3Findings(IReadOnlyList<(string Name, Color[] Pixels)> swatches, bool contrast, ICollection<string> measured)
    {
        ArgumentNullException.ThrowIfNull(swatches);
        ArgumentNullException.ThrowIfNull(measured);
        var findings = new List<string>();
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

                double differing = R3Differing(one, other);
                double ratio = Contrast.ContrastRatio(Commonest(one), Commonest(other));
                bool same = differing < Thresholds.R3SamePatternUnder || differing > Thresholds.R3SamePatternOver;
                string line = string.Create(CultureInfo.InvariantCulture,
                    $"{oneName} and {otherName}: {differing * 100:F1} % of positions differ; commonest colours {ratio:F2}:1");
                measured.Add(line);
                if (same && (contrast || ratio < Thresholds.R3Colours))
                {
                    findings.Add(line + (contrast ? ": the same pattern under a contrast theme" : ": the same pattern and under 3:1"));
                }
            }
        }

        return findings;
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
