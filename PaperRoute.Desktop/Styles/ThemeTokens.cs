using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PaperRoute.Desktop.Styles;

// The brushes today's code computes rather than names (d1 5.1, "Computed
// brushes"), added to each theme dictionary of Tokens.axaml at start-up:
//
//   AccentHoverBrush, AccentPressedBrush: the primary button hovered and pressed,
//     the accent 10 % and 20 % of the way to black in Light, white in Dark
//     (ActionButton.vb:222-226).
//   WarningChipHoverBrush, DangerChipHoverBrush, AccentChipHoverBrush: a toned
//     chip hovered, its fill 40 % of the way back to the card surface
//     (FilterChip.HoverFill, FilterChip.vb:146-148). The active chip is toned
//     too (its tone is not Neutral), so it gets the same ease; the design named
//     only the warning and danger chips.
//
// Light and Dark each have their own dictionary, so the derived brushes follow a
// theme change through DynamicResource without running again. A high-contrast
// dictionary made at run time (step 11) runs AddDerived itself.
public static class ThemeTokens
{
    public const string AccentHoverBrush = nameof(AccentHoverBrush);
    public const string AccentPressedBrush = nameof(AccentPressedBrush);
    public const string WarningChipHoverBrush = nameof(WarningChipHoverBrush);
    public const string DangerChipHoverBrush = nameof(DangerChipHoverBrush);
    public const string AccentChipHoverBrush = nameof(AccentChipHoverBrush);

    // Every key AddDerived adds.
    public static IReadOnlyList<string> DerivedKeys { get; } =
    [
        AccentHoverBrush,
        AccentPressedBrush,
        WarningChipHoverBrush,
        DangerChipHoverBrush,
        AccentChipHoverBrush,
    ];

    // Adds the derived brushes to one theme dictionary, read from that
    // dictionary's own AccentBrush, CardBackgroundBrush and muted fills.
    public static void AddDerived(IResourceDictionary dictionary, ThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(variant);

        IReadOnlyDictionary<string, Color> derived = DerivedColors(key => ColorOf(dictionary, variant, key), IsDark(variant));
        foreach (KeyValuePair<string, Color> entry in derived)
        {
            dictionary[entry.Key] = new SolidColorBrush(entry.Value);
        }
    }

    // The derived colours from a theme's own colours (looked up by brush key),
    // with no Avalonia object made, so a plain test can check them.
    public static IReadOnlyDictionary<string, Color> DerivedColors(Func<string, Color> colorOf, bool dark)
    {
        ArgumentNullException.ThrowIfNull(colorOf);
        Color accent = colorOf("AccentBrush");
        Color card = colorOf("CardBackgroundBrush");
        Color shade = dark ? Colors.White : Colors.Black;
        return new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            [AccentHoverBrush] = Blend(accent, shade, 0.1F),
            [AccentPressedBrush] = Blend(accent, shade, 0.2F),
            [WarningChipHoverBrush] = Blend(colorOf("WarningMutedBackgroundBrush"), card, 0.4F),
            [DangerChipHoverBrush] = Blend(colorOf("DangerMutedBackgroundBrush"), card, 0.4F),
            [AccentChipHoverBrush] = Blend(colorOf("AccentMutedBackgroundBrush"), card, 0.4F),
        };
    }

    // Runs AddDerived on every theme dictionary found under the given resources,
    // including those of merged dictionaries (Tokens.axaml is merged into App's).
    // Returns how many theme dictionaries it extended.
    public static int AddDerivedToAll(IResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        int count = 0;
        foreach (KeyValuePair<ThemeVariant, IThemeVariantProvider> entry in resources.ThemeDictionaries)
        {
            if (entry.Value is IResourceDictionary themed)
            {
                AddDerived(themed, entry.Key);
                count++;
            }
        }

        foreach (IResourceProvider merged in resources.MergedDictionaries)
        {
            IResourceDictionary? nested = merged switch
            {
                IResourceDictionary dictionary => dictionary,
                Avalonia.Markup.Xaml.Styling.ResourceInclude include => include.Loaded as IResourceDictionary,
                _ => null,
            };
            if (nested is not null)
            {
                count += AddDerivedToAll(nested);
            }
        }

        return count;
    }

    // UiTheme.Blend (UiTheme.vb:176-191), ported as it is: the fraction is
    // clamped to 0..1, each channel is worked in single precision, and VB's
    // CInt rounds the result half to even ("banker's rounding"), so a channel
    // that lands on .5 goes to the even neighbour. The alpha is the base's.
    public static Color Blend(Color baseColor, Color toward, float amount)
    {
        float t = Math.Max(0.0F, Math.Min(1.0F, amount));
        return Color.FromArgb(
            baseColor.A,
            Channel(baseColor.R, toward.R, t),
            Channel(baseColor.G, toward.G, t),
            Channel(baseColor.B, toward.B, t));
    }

    private static byte Channel(byte from, byte to, float t)
    {
        float value = from + ((to - from) * t);
        return (byte)(int)MathF.Round(value, MidpointRounding.ToEven);
    }

    private static bool IsDark(ThemeVariant variant)
    {
        for (ThemeVariant? current = variant; current is not null; current = current.InheritVariant)
        {
            if (current == ThemeVariant.Dark)
            {
                return true;
            }
        }

        return false;
    }

    private static Color ColorOf(IResourceDictionary dictionary, ThemeVariant variant, string key)
    {
        if (dictionary.TryGetResource(key, variant, out object? value) && value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        throw new InvalidOperationException($"The theme dictionary for {variant} has no solid brush \"{key}\".");
    }
}
