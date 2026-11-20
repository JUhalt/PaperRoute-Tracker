using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Styles;

// High contrast (d1 5.2, step 11). Avalonia has no contrast variant (r2 4.2),
// so the trial makes two of its own, HighContrastDark (inheriting Dark) and
// HighContrastLight (inheriting Light), chosen by the window colour as today's
// IsDark test reads it. Fluent's own parts, which take no custom palette
// (r2 4.1), fall back to the variant each inherits.
//
// Each variant's dictionary is made in code and added to the ThemeDictionaries
// of Tokens.axaml, beside Light and Dark, before any control looks a token up.
// It gives every token of Tokens.axaml one of the six system colours, as
// UiTheme.vb does under a contrast theme (the table below), OutlineThickness 1
// (pills, tags and toned chips outlined in their ink, PillLabel.vb:31-46,
// FilterChip.vb:197-226, TagChips.vb:32), and the derived brushes.
//
// When the system colours change, the brushes keep their identity and take
// the new colours, so everything bound to them repaints at once; when the
// contrast theme goes on or off, the application's RequestedThemeVariant moves
// between the contrast variant and the one the app had, and every
// DynamicResource follows: no restart, where today a Board built before the
// switch keeps its colours until one (r1 5.1). Contrast.axaml, the few rules
// a token cannot carry (a pressed chip in the selection colours, an outlined
// quiet button, focus rings in the text colour, no motion), is added to the
// application's styles while a contrast theme is on.
public static class ContrastTheme
{
    public static ThemeVariant HighContrastDark { get; } = new("HighContrastDark", ThemeVariant.Dark);

    public static ThemeVariant HighContrastLight { get; } = new("HighContrastLight", ThemeVariant.Light);

    // The system colour each token takes under a contrast theme: UiTheme.vb's
    // contrast branches (lines 95-623), TagChips.vb:40, 54, and d1 5.1's table.
    public static IReadOnlyDictionary<string, SystemColorRole> TokenRoles { get; } = BuildTokenRoles();

    private static readonly Uri s_contrastStyles = new("avares://PaperRoute.Desktop/Styles/Contrast.axaml");

    private static readonly ConditionalWeakTable<Application, Follower> s_followers = new();

    public static bool IsContrast(ThemeVariant? variant) =>
        variant is not null && (variant.Key.Equals(HighContrastDark.Key) || variant.Key.Equals(HighContrastLight.Key));

    // The variant a set of system colours belongs to.
    public static ThemeVariant VariantFor(SystemContrastColors colors) =>
        colors.IsDark ? HighContrastDark : HighContrastLight;

    public static Color ColorOf(SystemContrastColors colors, SystemColorRole role) => role switch
    {
        SystemColorRole.Window => colors.Window,
        SystemColorRole.WindowText => colors.WindowText,
        SystemColorRole.GrayText => colors.GrayText,
        SystemColorRole.Highlight => colors.Highlight,
        SystemColorRole.HighlightText => colors.HighlightText,
        SystemColorRole.HotTrack => colors.HotTrack,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    // Every colour of a contrast dictionary for these system colours: the
    // tokens and the derived brushes, with no Avalonia object made.
    public static IReadOnlyDictionary<string, Color> Colors(SystemContrastColors colors)
    {
        var result = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, SystemColorRole> token in TokenRoles)
        {
            result[token.Key] = ColorOf(colors, token.Value);
        }

        // Today IsDark is never true under a contrast theme (UiTheme.vb:64-69),
        // so the primary button darkens towards black even on a dark one.
        foreach (KeyValuePair<string, Color> derived in ThemeTokens.DerivedColors(key => result[key], dark: false))
        {
            result[derived.Key] = derived.Value;
        }

        return result;
    }

    // Adds both contrast dictionaries to the theme dictionaries of Tokens.axaml
    // (found under the given resources), filled with the fallback colours.
    // Call once, before any control looks a token up (App.Initialize).
    public static void Register(IResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        IResourceDictionary tokens = FindTokens(resources)
            ?? throw new InvalidOperationException("No theme dictionary holds Light: Tokens.axaml is not merged.");
        foreach (ThemeVariant variant in new[] { HighContrastDark, HighContrastLight })
        {
            if (!tokens.ThemeDictionaries.ContainsKey(variant))
            {
                var dictionary = new ResourceDictionary
                {
                    ["OutlineThickness"] = new Thickness(1),
                };
                tokens.ThemeDictionaries[variant] = dictionary;
                FillDictionary(dictionary, SystemContrastColors.Fallback);
            }
        }
    }

    // Puts these system colours into the dictionary of their variant, keeping
    // each brush and changing its colour; returns the variant.
    public static ThemeVariant Fill(IResourceDictionary resources, SystemContrastColors colors)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ThemeVariant variant = VariantFor(colors);
        IResourceDictionary tokens = FindTokens(resources)
            ?? throw new InvalidOperationException("No theme dictionary holds Light: Tokens.axaml is not merged.");
        if (!tokens.ThemeDictionaries.TryGetValue(variant, out IThemeVariantProvider? provider) || provider is not IResourceDictionary dictionary)
        {
            throw new InvalidOperationException($"The {variant} dictionary is not registered: call ContrastTheme.Register first.");
        }

        FillDictionary(dictionary, colors);
        return variant;
    }

    // Makes the application follow a contrast preference from now on: at once,
    // and on every Changed. The variant the app has when this is called is the
    // one it returns to when the contrast theme goes off.
    public static void Follow(Application application, ContrastPreference preference)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(preference);
        if (s_followers.TryGetValue(application, out Follower? old))
        {
            old.Stop();
            s_followers.Remove(application);
        }

        var follower = new Follower(application, preference);
        s_followers.Add(application, follower);
        follower.Apply();
    }

    private static void FillDictionary(IResourceDictionary dictionary, SystemContrastColors colors)
    {
        foreach (KeyValuePair<string, Color> entry in Colors(colors))
        {
            if (dictionary.TryGetValue(entry.Key, out object? existing) && existing is SolidColorBrush brush)
            {
                brush.Color = entry.Value;
            }
            else
            {
                dictionary[entry.Key] = new SolidColorBrush(entry.Value);
            }
        }
    }

    private static IResourceDictionary? FindTokens(IResourceDictionary resources)
    {
        if (resources.ThemeDictionaries.ContainsKey(ThemeVariant.Light))
        {
            return resources;
        }

        foreach (IResourceProvider merged in resources.MergedDictionaries)
        {
            IResourceDictionary? nested = merged switch
            {
                IResourceDictionary dictionary => dictionary,
                ResourceInclude include => include.Loaded as IResourceDictionary,
                _ => null,
            };
            if (nested is not null && FindTokens(nested) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Dictionary<string, SystemColorRole> BuildTokenRoles()
    {
        var roles = new Dictionary<string, SystemColorRole>(StringComparer.Ordinal)
        {
            // Surfaces and tints: the window colour (UiTheme.vb:144-171, 194-266, 338-350).
            ["BoardBackgroundBrush"] = SystemColorRole.Window,
            ["HeaderBackgroundBrush"] = SystemColorRole.Window,
            ["CardBackgroundBrush"] = SystemColorRole.Window,
            ["HoverBackgroundBrush"] = SystemColorRole.Window,
            ["AccentMutedBackgroundBrush"] = SystemColorRole.Window,
            ["WarningMutedBackgroundBrush"] = SystemColorRole.Window,
            ["DangerMutedBackgroundBrush"] = SystemColorRole.Window,

            // Text, borders and the warning and danger inks: the window text (:239-251, 269-292, 380-408).
            ["CardBorderBrush"] = SystemColorRole.WindowText,
            ["PrimaryTextBrush"] = SystemColorRole.WindowText,
            ["SecondaryTextBrush"] = SystemColorRole.WindowText,
            ["WarningBrush"] = SystemColorRole.WindowText,
            ["DangerBrush"] = SystemColorRole.WindowText,

            // Quiet text and dividers: the inactive text (:95-125).
            ["SubtleBorderBrush"] = SystemColorRole.GrayText,
            ["MutedTextBrush"] = SystemColorRole.GrayText,

            // The accent: the selection; the secondary accent: the hyperlink (:306-335);
            // text on the accent: the selection's text (:128-141).
            ["AccentBrush"] = SystemColorRole.Highlight,
            ["AccentSecondaryBrush"] = SystemColorRole.HotTrack,
            ["OnAccentTextBrush"] = SystemColorRole.HighlightText,
        };

        // Stage pills and tags: the window's own colours (:476-482, 551-557; TagChips.vb:40, 54).
        foreach (string stage in new[] { "Idea", "Draft", "Submitted", "UnderReview", "Revision", "Accepted", "InPress", "Published" })
        {
            roles["Stage" + stage + "BackgroundBrush"] = SystemColorRole.Window;
            roles["Stage" + stage + "ForegroundBrush"] = SystemColorRole.WindowText;
        }

        foreach (string palette in new[] { "Teal", "Blue", "Violet", "Rose", "Amber", "Green", "Slate", "Orange" })
        {
            roles["Tag" + palette + "BackgroundBrush"] = SystemColorRole.Window;
            roles["Tag" + palette + "ForegroundBrush"] = SystemColorRole.WindowText;
        }

        return roles;
    }

    // Keeps one application on one preference.
    private sealed class Follower
    {
        private readonly Application _application;
        private readonly ContrastPreference _preference;
        private readonly ThemeVariant? _baseVariant;
        private IStyle? _contrastStyles;

        public Follower(Application application, ContrastPreference preference)
        {
            _application = application;
            _preference = preference;
            ThemeVariant? current = application.RequestedThemeVariant;
            _baseVariant = IsContrast(current) ? ThemeVariant.Default : current;
            _preference.Changed += OnChanged;
        }

        public void Stop() => _preference.Changed -= OnChanged;

        public void Apply()
        {
            if (_preference.IsHigh)
            {
                ThemeVariant variant = Fill(_application.Resources, _preference.Colors);
                if (_contrastStyles is null)
                {
                    _contrastStyles = (IStyle)AvaloniaXamlLoader.Load(s_contrastStyles);
                    _application.Styles.Add(_contrastStyles);
                }

                _application.RequestedThemeVariant = variant;
            }
            else
            {
                if (_contrastStyles is not null)
                {
                    _application.Styles.Remove(_contrastStyles);
                    _contrastStyles = null;
                }

                _application.RequestedThemeVariant = _baseVariant;
            }
        }

        private void OnChanged(object? sender, EventArgs e)
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
}

// The six system colours of SystemContrastColors, by role.
public enum SystemColorRole
{
    Window,
    WindowText,
    GrayText,
    Highlight,
    HighlightText,
    HotTrack,
}
