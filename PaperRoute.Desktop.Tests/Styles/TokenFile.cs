using System.Globalization;
using System.Xml.Linq;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Styles;

// Reads the trial's .axaml files as text from the test output (Axaml\, copied
// there by the project file), with no Avalonia platform: the token tests are
// plain colour arithmetic over the values the files hold (d1 6.2, T1 and T2).
internal static class TokenFile
{
    private static readonly XNamespace s_x = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static string AxamlRoot => Path.Combine(AppContext.BaseDirectory, "Axaml");

    public static string TokensPath => Path.Combine(AxamlRoot, "Styles", "Tokens.axaml");

    public static IReadOnlyList<string> AllAxamlFiles() =>
        Directory.GetFiles(AxamlRoot, "*.axaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

    // The keys of every entry in Tokens.axaml outside its theme dictionaries.
    public static IReadOnlySet<string> ThemeIndependentKeys()
    {
        XElement root = XDocument.Load(TokensPath).Root!;
        return root.Elements()
            .Where(element => element.Name.LocalName != "ResourceDictionary.ThemeDictionaries")
            .Select(element => (string?)element.Attribute(s_x + "Key"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    // Every key a theme dictionary defines (brushes and OutlineThickness), by variant ("Light", "Dark").
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ThemedKeys()
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (XElement dictionary in ThemeDictionaries())
        {
            result[(string)dictionary.Attribute(s_x + "Key")!] = dictionary.Elements()
                .Select(element => (string?)element.Attribute(s_x + "Key"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        }

        return result;
    }

    // The colours of one variant: every SolidColorBrush in its theme
    // dictionary, plus the brushes ThemeTokens derives from them.
    public static IReadOnlyDictionary<string, Color> Colors(string variant)
    {
        XElement dictionary = ThemeDictionaries().Single(element => (string?)element.Attribute(s_x + "Key") == variant);
        var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (XElement brush in dictionary.Elements().Where(element => element.Name.LocalName == "SolidColorBrush"))
        {
            colors[(string)brush.Attribute(s_x + "Key")!] = ParseHex((string)brush.Attribute("Color")!);
        }

        foreach (KeyValuePair<string, Color> derived in ThemeTokens.DerivedColors(key => colors[key], variant == "Dark"))
        {
            colors[derived.Key] = derived.Value;
        }

        return colors;
    }

    // "#RRGGBB" only: the token file names every colour that way.
    public static Color ParseHex(string text)
    {
        if (text.Length != 7 || text[0] != '#')
        {
            throw new FormatException($"A token colour must be #RRGGBB, not \"{text}\".");
        }

        return Color.FromRgb(
            byte.Parse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static IEnumerable<XElement> ThemeDictionaries()
    {
        XElement root = XDocument.Load(TokensPath).Root!;
        XElement themes = root.Elements().Single(element => element.Name.LocalName == "ResourceDictionary.ThemeDictionaries");
        return themes.Elements().Where(element => element.Name.LocalName == "ResourceDictionary");
    }
}
