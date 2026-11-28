using System.Globalization;

namespace PaperRoute.Desktop.Tests.Legibility;

internal static partial class Rules
{
    // R8: a disabled control's text at 3:1 on its surface, by its strongest
    // pixel (plan :164 and :175). Its input is read as R7's is (DrawnText),
    // for the texts of disabled controls (Sweep's DisabledTexts); the surface
    // is the commonest colour around the text, the control's own fill. The
    // WinForms caption-recolouring part is dropped, as the plan says.
    // Limits of headless: HeadlessLimits.md
    public static IReadOnlyList<Finding> R8(IEnumerable<DrawnText> disabledTexts) =>
        disabledTexts.Where(text => text.Strongest < Thresholds.DisabledText)
            .Select(text =>
            {
                string subject = $"{text.Subject}: {text.Element} \"{text.Text}\"";
                string tokens = text.TokenRatio is { } ratio
                    ? string.Create(CultureInfo.InvariantCulture, $"{text.InkToken} on {text.SurfaceToken} is {ratio:F2}:1")
                    : "no token pair";
                return new Finding("R8", text.ThemeCase, subject, string.Create(CultureInfo.InvariantCulture,
                    $"{text.ThemeCase}: {subject}, disabled, is {text.Strongest:F2}:1 by its strongest pixel on {Hex(text.Surface)}, under {Thresholds.DisabledText}:1 ({tokens}; {text.FullInk} full-ink pixels)"));
            })
            .ToList();
}
