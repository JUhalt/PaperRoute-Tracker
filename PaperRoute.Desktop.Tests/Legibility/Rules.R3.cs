using System.Globalization;
using Avalonia;
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
// Input: one frame and each kind's slot in it, all of one size. The pattern
// arithmetic is FramePixels' (PatternDifference, CommonestOwnColour,
// SharesPattern), which PrimitiveTests checks on bitmaps made in code.
// Output: findings (Rules.cs), one line each, whose subject is the pair,
// "<one> and <other>".
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    public static List<Finding> R3Findings(string themeCase, FramePixels frame, IReadOnlyList<(string Name, PixelRect Area)> slots, bool contrast) =>
        R3Findings(R3Measures(themeCase, frame, slots), contrast);

    // R3's findings from its measurements: the pairs that share a pattern,
    // under a contrast theme or under 3:1.
    public static List<Finding> R3Findings(IEnumerable<PatternMeasure> measures, bool contrast)
    {
        ArgumentNullException.ThrowIfNull(measures);
        var findings = new List<Finding>();
        foreach (PatternMeasure measured in measures)
        {
            if (FramePixels.SharesPattern(measured.Differing) && (contrast || measured.Ratio < Thresholds.R3Colours))
            {
                findings.Add(new Finding("R3", measured.ThemeCase, $"{measured.One} and {measured.Other}",
                    measured.Line + (contrast ? ": the same pattern under a contrast theme" : ": the same pattern and under 3:1")));
            }
        }

        return findings;
    }

    // What R3 measures: every pair of slots, in order, passing or not. The R3
    // run writes these to legibility-r3-<case>.txt (Measurements).
    public static IReadOnlyList<PatternMeasure> R3Measures(string themeCase, FramePixels frame, IReadOnlyList<(string Name, PixelRect Area)> slots)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(slots);
        var measures = new List<PatternMeasure>();
        for (int first = 0; first < slots.Count - 1; first++)
        {
            for (int second = first + 1; second < slots.Count; second++)
            {
                (string oneName, PixelRect one) = slots[first];
                (string otherName, PixelRect other) = slots[second];
                double ratio = Contrast.ContrastRatio(Commonest(frame, one, oneName), Commonest(frame, other, otherName));
                measures.Add(new PatternMeasure(themeCase, oneName, otherName, FramePixels.PatternDifference(frame, one, frame, other), ratio));
            }
        }

        return measures;
    }

    // A slot's commonest colour; a slot with no children always has one.
    private static Color Commonest(FramePixels frame, PixelRect slot, string name) =>
        frame.CommonestOwnColour(slot, []) ?? throw new InvalidOperationException($"No colour in the slot of {name}.");
}
