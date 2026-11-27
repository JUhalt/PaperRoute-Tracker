namespace PaperRoute.Desktop.Tests.Legibility;

// The lines the legibility rules judge by, in one place, each with the line
// of today's WinForms suite or of the plan it comes from (plan :164, which
// cites them). No rule writes its own number.
// Limits of headless: HeadlessLimits.md
internal static class Thresholds
{
    // Text on its surface, muted and disabled text included (R1, R2, R7):
    // WorkflowThemeKeyboardSmokeTests.vb:43, the drawn pairs at 4.5:1.
    public const double Text = 4.5;

    // A disabled control's text on its surface (R8):
    // DarkThemeLegibilityTests.vb:1197 and :1282, "a disabled caption needs 3:1".
    public const double DisabledText = 3.0;

    // A control's edge against the window under a contrast theme (R9):
    // DeadlineListTests.vb:1022, EdgeContrast at 3:1.
    public const double Edge = 3.0;

    // "Light" in Dark: a relative luminance above this (R5):
    // DarkThemeLegibilityTests.vb:1018-1020, IsLight.
    public const double LightPart = 0.6;

    // R3, route kinds told apart without colour (WorkflowThemeKeyboardSmokeTests.vb):
    // a pixel is in a swatch's pattern within 2:1 of its commonest colour (:1010-1011);
    public const double R3InPattern = 2.0;

    // two swatches share a pattern when under 10 % (:1013)
    public const double R3SamePatternUnder = 0.10;

    // or over 90 % of positions differ (:1013);
    public const double R3SamePatternOver = 0.90;

    // and a pair sharing a pattern fails when its colours are under 3:1 (:132).
    public const double R3Colours = 3.0;
}
