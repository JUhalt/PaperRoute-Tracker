using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One text element as R7 reads it in one subject and theme case: its name
// in the sweep and its text; its ink (none when its brush is not one opaque
// colour) and ink token; the surface drawn around it (the commonest colour
// of the 2 px frame around its bounds) and the surface token; the token
// ratio, ink token against surface token, where both exist; the contrast of
// its strongest pixel against the surface; how many of its pixels are full
// ink, exactly the ink's colour; and its bounds.
internal sealed record DrawnText(
    string ThemeCase,
    string Subject,
    string Element,
    string Text,
    Color? Ink,
    string InkToken,
    Color Surface,
    string SurfaceToken,
    double? TokenRatio,
    double Strongest,
    int FullInk,
    PixelRect Bounds)
{
    // How far the strongest pixel sits above R7's line.
    public double Margin => Strongest - Thresholds.Text;
}

internal static partial class Rules
{
    // R7: drawn text at 4.5:1 by its strongest pixel (plan :164), muted text
    // included. The tokens may pass R1 while the pixels are faint (an
    // opacity, a thin stroke that never reaches full ink); only the pixels
    // count here.
    // Limits of headless: HeadlessLimits.md
    public static IReadOnlyList<Finding> R7(IEnumerable<DrawnText> texts) =>
        texts.Where(text => text.Strongest < Thresholds.Text)
            .Select(text =>
            {
                string subject = $"{text.Subject}: {text.Element} \"{text.Text}\"";
                string tokens = text.TokenRatio is { } ratio
                    ? string.Create(CultureInfo.InvariantCulture, $"{text.InkToken} on {text.SurfaceToken} is {ratio:F2}:1")
                    : "no token pair";
                return new Finding("R7", text.ThemeCase, subject, string.Create(CultureInfo.InvariantCulture,
                    $"{text.ThemeCase}: {subject} is {text.Strongest:F2}:1 by its strongest pixel on {Hex(text.Surface)} ({tokens}; {text.FullInk} full-ink pixels)"));
            })
            .ToList();
}
