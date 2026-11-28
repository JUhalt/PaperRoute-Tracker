using System.Globalization;
using System.Xml.Linq;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Styles;

// Reads the trial's .axaml files as text from the test output (Axaml\, copied
// there by the project file), with no Avalonia platform: the token tests are
// plain colour arithmetic over the values the files hold (d1 6.2, R1, was T1
// and T2). Each reader of the token file takes a path, the real file when none
// is given, so R1's teeth run a spoiled copy through the same code.
internal static class TokenFile
{
    private static readonly XNamespace s_x = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static string AxamlRoot => Path.Combine(AppContext.BaseDirectory, "Axaml");

    public static string TokensPath => Path.Combine(AxamlRoot, "Styles", "Tokens.axaml");

    public static IReadOnlyList<string> AllAxamlFiles() =>
        Directory.GetFiles(AxamlRoot, "*.axaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

    // The keys of every entry in Tokens.axaml outside its theme dictionaries.
    public static IReadOnlySet<string> ThemeIndependentKeys(string? tokensPath = null)
    {
        return IndependentEntries(tokensPath)
            .Select(element => (string?)element.Attribute(s_x + "Key"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    // Every key a theme dictionary defines (brushes and OutlineThickness), by variant ("Light", "Dark").
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ThemedKeys(string? tokensPath = null)
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (XElement dictionary in ThemeDictionaries(tokensPath))
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
    public static IReadOnlyDictionary<string, Color> Colors(string variant, string? tokensPath = null)
    {
        XElement dictionary = ThemeDictionaries(tokensPath).Single(element => (string?)element.Attribute(s_x + "Key") == variant);
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

    // Every value the token file writes, as written: a brush's Color, any other
    // entry's text. The scope is "" outside the theme dictionaries, else the
    // variant ("Light", "Dark"). TokenValuesArePinned compares these with its table.
    public static IReadOnlyList<(string Scope, string Key, string Value)> Values(string? tokensPath = null)
    {
        var values = new List<(string, string, string)>();
        foreach (XElement element in IndependentEntries(tokensPath))
        {
            values.Add(("", (string)element.Attribute(s_x + "Key")!, ValueOf(element)));
        }

        foreach (XElement dictionary in ThemeDictionaries(tokensPath))
        {
            string variant = (string)dictionary.Attribute(s_x + "Key")!;
            foreach (XElement element in dictionary.Elements())
            {
                values.Add((variant, (string)element.Attribute(s_x + "Key")!, ValueOf(element)));
            }
        }

        return values;
    }

    private static string ValueOf(XElement element) =>
        element.Name.LocalName == "SolidColorBrush" ? (string)element.Attribute("Color")! : element.Value.Trim();

    private static IEnumerable<XElement> IndependentEntries(string? tokensPath) =>
        XDocument.Load(tokensPath ?? TokensPath).Root!.Elements()
            .Where(element => element.Name.LocalName != "ResourceDictionary.ThemeDictionaries");

    private static IEnumerable<XElement> ThemeDictionaries(string? tokensPath)
    {
        XElement root = XDocument.Load(tokensPath ?? TokensPath).Root!;
        XElement themes = root.Elements().Single(element => element.Name.LocalName == "ResourceDictionary.ThemeDictionaries");
        return themes.Elements().Where(element => element.Name.LocalName == "ResourceDictionary");
    }
}
