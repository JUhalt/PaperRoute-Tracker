using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One part a control paints, as R5 reads it in one subject and theme case:
// its name in the sweep, its brush's colour laid over what lies beneath it
// (none for a brush that is not one solid colour), the commonest colour of
// its own pixels (none when its children cover it), and its shown bounds.
internal sealed record PaintedPart(string ThemeCase, string Subject, string Part, Color? Brush, Color? Own, PixelRect Bounds)
{
    // The lighter of the two readings, which R5 judges.
    public double Lightest => Math.Max(Brush is { } brush ? Contrast.RelativeLuminance(brush) : 0, Own is { } own ? Contrast.RelativeLuminance(own) : 0);

    // How far the part sits under R5's light line.
    public double Margin => Thresholds.LightPart - Lightest;
}

// An accepted light part in Dark (the harness design, part 2.4): its name,
// the reason it is accepted, the User Guide's words for it, and which parts
// it covers. An entry that covers no part is stale and fails.
internal sealed record R5Allowance(string Name, string Reason, string GuideWords, Func<PaintedPart, bool> Matches);

// R5's allow-list: looks that are accepted, not faults (those go on
// KnownFailures.tsv). It starts empty: none of today's five WinForms entries
// (DarkThemeLegibilityTests.vb:24-40) exists in the Avalonia interface yet.
// Limits of headless: HeadlessLimits.md
internal static class R5AllowList
{
    public static IReadOnlyList<R5Allowance> Entries { get; } = [];
}

internal static partial class Rules
{
    // R5: no light parts in Dark (plan :164). Dark only (part 2.4): under a
    // contrast set the window or highlight colour is the user's own and may
    // be light; R2 judges those. A part fails by its brush (the cause) or by
    // its own pixels (a template part or an opacity), each when above
    // relative luminance 0.6; a part an allow-list entry covers is not
    // judged, and an entry that covers no part is reported as stale.
    public static IReadOnlyList<Finding> R5(string themeCase, IReadOnlyList<PaintedPart> parts, IReadOnlyList<R5Allowance> allowList)
    {
        var findings = new List<Finding>();
        foreach (R5Allowance stale in allowList.Where(entry => !parts.Any(entry.Matches)))
        {
            findings.Add(new Finding("R5", themeCase, $"allow-list: {stale.Name}", $"{themeCase}: the R5 allow-list entry \"{stale.Name}\" covers no part: it is stale."));
        }

        foreach (PaintedPart part in parts.Where(part => !allowList.Any(entry => entry.Matches(part))))
        {
            string subject = $"{part.Subject}: {part.Part}";
            if (part.Brush is { } brush && Contrast.RelativeLuminance(brush) > Thresholds.LightPart)
            {
                findings.Add(new Finding("R5", themeCase, subject, Line(themeCase, subject, "paints by its brush", brush)));
            }

            if (part.Own is { } own && Contrast.RelativeLuminance(own) > Thresholds.LightPart)
            {
                findings.Add(new Finding("R5", themeCase, subject, Line(themeCase, subject, "shows in its own pixels", own)));
            }
        }

        return findings;
    }

    // A colour as a measurement cell writes it.
    public static string Hex(Color colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

    private static string Line(string themeCase, string subject, string how, Color colour) =>
        string.Create(CultureInfo.InvariantCulture, $"{themeCase}: {subject} {how} {Hex(colour)}, luminance {Contrast.RelativeLuminance(colour):0.000}, above {Thresholds.LightPart}");
}
