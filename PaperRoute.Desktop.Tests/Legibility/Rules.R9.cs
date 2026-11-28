using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// One border as R9 reads it in one subject and theme case: its name in the
// sweep, the surface drawn around it (the commonest colour of the 2 px frame
// around its bounds), how far its edge stands out from that surface by the
// weakest of its four sides (FramePixels.EdgeContrast), and its bounds.
internal sealed record DrawnEdge(string ThemeCase, string Subject, string Part, Color Surface, double Edge, PixelRect Bounds)
{
    // How far the edge sits above R9's line.
    public double Margin => Edge - Thresholds.Edge;
}

// What taking the keyboard focus changes around one focusable control, as
// R9 reads it in one subject and contrast case: the control's name in the
// sweep, the set's window colour it is judged against, how many pixels of
// its bounds plus Sweep.FocusReach changed, the strongest of them against
// the window colour (0 when none changed), the area read, and why it was
// not judged (none when it was).
internal sealed record FocusChange(string ThemeCase, string Subject, string Control, Color Window, int Changed, double Strongest, PixelRect Area, string? NotJudged)
{
    // How far the strongest changed pixel sits above R9's line.
    public double Margin => Strongest - Thresholds.Edge;
}

internal static partial class Rules
{
    // R9, edges at 3:1 under contrast (plan :164), its first part: a bordered
    // control's edge against the surface around it, by its weakest side.
    // Limits of headless: HeadlessLimits.md
    public static IReadOnlyList<Finding> R9Edges(IEnumerable<DrawnEdge> edges) =>
        edges.Where(edge => edge.Edge < Thresholds.Edge)
            .Select(edge =>
            {
                string subject = $"{edge.Subject}: {edge.Part}";
                return new Finding("R9", edge.ThemeCase, subject, string.Create(CultureInfo.InvariantCulture,
                    $"{edge.ThemeCase}: {subject}'s edge is {edge.Edge:F2}:1 against {Hex(edge.Surface)} by its weakest side, under {Thresholds.Edge}:1"));
            })
            .ToList();

    // R9's second part, focus included (part 2.4): a focusable control's
    // focus must change at least one pixel of its bounds plus 4 px to one at
    // 3:1 against the window colour. A control whose focus changes no pixel
    // fails. A control the sweep could not judge (NotJudged) is not judged.
    public static IReadOnlyList<Finding> R9Focus(IEnumerable<FocusChange> changes) =>
        changes.Where(change => change.NotJudged is null && (change.Changed == 0 || change.Strongest < Thresholds.Edge))
            .Select(change =>
            {
                string subject = $"{change.Subject}: {change.Control}, focus";
                string how = change.Changed == 0
                    ? "changes no pixel"
                    : string.Create(CultureInfo.InvariantCulture, $"is {change.Strongest:F2}:1 against the window colour {Hex(change.Window)} by its strongest changed pixel ({change.Changed} pixels changed), under {Thresholds.Edge}:1");
                return new Finding("R9", change.ThemeCase, subject, $"{change.ThemeCase}: {subject} {how}");
            })
            .ToList();
}
