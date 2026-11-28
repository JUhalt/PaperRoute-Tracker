using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Styles;

// The appearance a person chooses (AppSettings.Appearance, default System):
// the system's own light or dark look, or one of the two fixed.
public enum Appearance
{
    System,
    Light,
    Dark,
}

// The one owner of the application's theme variant (harness design 3.4):
// nothing else decides the variant; the system's contrast state is read only
// by AppTheme, through ContrastPreference and SystemContrastColors. The
// measurement's pinned window and the platform check's record are the two
// named exceptions, and AppThemeTests fails on any other code that sets a
// variant or reads that state in a form its scan (ThemeOwnership) knows.
//
// The rule, in order: a contrast theme on gives the contrast variant of its
// window colour (ContrastTheme.VariantFor); otherwise Light or Dark as chosen;
// otherwise ThemeVariant.Default, which keeps Avalonia's own following of the
// system. A change of appearance while a contrast theme is on changes only
// the appearance kept underneath, so the contrast theme going off lands on
// the newest choice. Every change moves RequestedThemeVariant, and every
// DynamicResource follows: no restart.
//
// ContrastTheme keeps the contrast dictionaries and their colours; this
// object decides which variant is live. Contrast.axaml, the rules a token
// cannot carry, is always loaded, and each of its rules holds only inside a
// top level carrying the class ContrastClass, which every top level carries
// exactly while it shows a contrast variant (set here, as the variant
// changes). Adding and removing the file left its setters on template parts
// after a contrast theme went off; WholeWindowThemeTests' round trips show
// that the class leaves nothing behind. Reading the stored appearance waits for the issue that
// opens real libraries or the Settings page (issue 20); until then the app
// runs at System.
public sealed class AppTheme
{
    // The class Contrast.axaml's selectors require of the top level.
    public const string ContrastClass = "contrast";

    private static readonly ConditionalWeakTable<Application, AppTheme> s_themes = new();

    private readonly Application _application;
    private Appearance _appearance;
    private ContrastPreference? _contrast;

    // Every top level (a window, a popup's root) carries ContrastClass while
    // it shows a contrast variant: the application's, or one of its own.
    static AppTheme()
    {
        TopLevel.ActualThemeVariantProperty.Changed.AddClassHandler<TopLevel>(
            (topLevel, _) => topLevel.Classes.Set(ContrastClass, ContrastTheme.IsContrast(topLevel.ActualThemeVariant)));
    }

    private AppTheme(Application application)
    {
        _application = application;
        Apply();
    }

    // The appearance chosen; setting it repaints at once, or, while a contrast
    // theme is on, only changes the variant the contrast theme gives back.
    public Appearance Appearance
    {
        get => _appearance;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }

            if (value != _appearance)
            {
                _appearance = value;
                Apply();
            }
        }
    }

    // The contrast preference followed, if any.
    public ContrastPreference? Contrast => _contrast;

    // The variant the rule gives now.
    public ThemeVariant Variant => VariantFor(_appearance, _contrast);

    // The application's theme, made (and applied, at System) on first use.
    // App.Initialize makes it, after the contrast dictionaries are registered.
    public static AppTheme Of(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return s_themes.GetValue(application, app => new AppTheme(app));
    }

    // The rule itself, with no application.
    public static ThemeVariant VariantFor(Appearance appearance, ContrastPreference? contrast)
    {
        if (contrast is { IsHigh: true })
        {
            return ContrastTheme.VariantFor(contrast.Colors);
        }

        return appearance switch
        {
            Appearance.Light => ThemeVariant.Light,
            Appearance.Dark => ThemeVariant.Dark,
            Appearance.System => ThemeVariant.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(appearance), appearance, null),
        };
    }

    // Follows the running platform's contrast preference from now on (d1 5.2):
    // at once, and whenever Avalonia reports a change or Refresh reads one.
    public void FollowPlatform() => Follow(ContrastPreference.FromPlatform(_application.PlatformSettings));

    // Follows a contrast preference from now on: at once, and on every
    // Changed. A test hands in one of its own; it replaces the one before.
    public void Follow(ContrastPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (_contrast is not null)
        {
            _contrast.Changed -= OnContrastChanged;
        }

        _contrast = preference;
        _contrast.Changed += OnContrastChanged;
        Apply();
    }

    // Reads the platform's contrast preference again (the window's activation,
    // for a switch between two contrast themes Avalonia might not report).
    public void Refresh() => _contrast?.Refresh();

    private void Apply()
    {
        if (_contrast is { IsHigh: true } contrast)
        {
            ContrastTheme.Fill(_application.Resources, contrast.Colors);
        }

        _application.RequestedThemeVariant = Variant;
    }

    private void OnContrastChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Dispatcher.UIThread.Post(Apply);
        }
    }
}
