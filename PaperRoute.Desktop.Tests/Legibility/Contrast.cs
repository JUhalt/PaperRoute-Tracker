using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// The WCAG contrast maths every legibility rule shares (plan :164), moved
// unchanged from Styles/TokenFile.cs, so the token rules and the pixel rules
// compute a ratio one way.
// Limits of headless: HeadlessLimits.md
internal static class Contrast
{
    // The WCAG contrast ratio, from 1 to 21, as PaperRoute.Tests' TestSupport.ContrastRatio.
    public static double ContrastRatio(Color foreground, Color background)
    {
        double first = RelativeLuminance(foreground);
        double second = RelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    public static double RelativeLuminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
