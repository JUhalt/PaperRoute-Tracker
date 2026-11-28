using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// The six theme cases the legibility rules run in (harness design 2.2):
// Light, Dark, and Windows' four contrast themes, named by their theme files,
// each with the six system colours SystemContrastColors holds for it. The
// headless platform reports Light with no contrast (HeadlessLimits.md, row
// 4), so a case drives the theme itself, through AppTheme: an appearance, or
// a contrast preference over the appearance it is entered from.
// Limits of headless: HeadlessLimits.md
internal sealed record ThemeCase(string Name, ThemeVariant? Variant, SystemContrastColors? Contrast)
{
    public static ThemeCase Light { get; } = new("Light", ThemeVariant.Light, null);

    public static ThemeCase Dark { get; } = new("Dark", ThemeVariant.Dark, null);

    public static ThemeCase Hc1 { get; } = new("hc1", null, SystemContrastColors.Hc1);

    public static ThemeCase Hc2 { get; } = new("hc2", null, SystemContrastColors.NightSky);

    public static ThemeCase Hcblack { get; } = new("hcblack", null, SystemContrastColors.Hcblack);

    public static ThemeCase Hcwhite { get; } = new("hcwhite", null, SystemContrastColors.Desert);

    public static IReadOnlyList<ThemeCase> All { get; } = [Light, Dark, Hc1, Hc2, Hcblack, Hcwhite];

    // The four Windows contrast sets.
    public static IReadOnlyList<ThemeCase> ContrastCases { get; } = [Hc1, Hc2, Hcblack, Hcwhite];

    // A contrast case is entered from a Light or a Dark base (question 8: as
    // today's WinForms R2 runs, under SystemColorMode Classic and Dark).
    public static IReadOnlyList<ThemeVariant> ContrastBases { get; } = [ThemeVariant.Light, ThemeVariant.Dark];

    // The theme file a contrast case comes from.
    public string ThemeFile => Name + ".theme";

    // Puts the running application into this case: a contrast case from the
    // given base (Light when none is given), Light or Dark as they are.
    public void Apply(Application application, ThemeVariant? contrastBase = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        AppTheme theme = AppTheme.Of(application);
        if (Contrast is { } colors)
        {
            theme.Appearance = contrastBase == ThemeVariant.Dark ? Appearance.Dark : Appearance.Light;
            theme.Follow(new ContrastPreference(true, colors));
        }
        else
        {
            theme.Follow(new ContrastPreference(false, SystemContrastColors.Fallback));
            theme.Appearance = Variant == ThemeVariant.Dark ? Appearance.Dark : Appearance.Light;
        }

        Dispatcher.UIThread.RunJobs();
    }

    public override string ToString() => Name;
}
