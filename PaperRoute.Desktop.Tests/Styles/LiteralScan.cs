using System.Text.RegularExpressions;

namespace PaperRoute.Desktop.Tests.Styles;

// The scan behind NoViewOrStyleNamesALiteral (harness design 3.1, pull
// request S2): every colour, font family, font size, corner radius and
// duration the views and styles use is a named token of Tokens.axaml, so no
// .axaml or .cs file of PaperRoute.Desktop may write its own. It reads the
// app's files as text (ThemeOwnership.AppFiles), with comments blanked, and
// names each literal by file and line:
//   - a #RRGGBB or #AARRGGBB colour, anywhere;
//   - a FontFamily, FontSize or Duration given a value that is not a markup
//     extension ({DynamicResource ...}, {TemplateBinding ...}), as an
//     attribute or a style's Setter, or in code;
//   - a CornerRadius with a number other than 0 (0 is no rounding, not a size);
//   - a numeric FontWeight (such as 700); named weights (Bold) are names already.
// Paddings, margins, heights, line heights, border thicknesses and named font
// weights stay written in the styles, as Appendix C writes them (3.1), so the
// scan does not look at them.
internal static partial class LiteralScan
{
    // The files the scan does not judge, and why. Nothing else is exempt.
    public static readonly IReadOnlyDictionary<string, string> ExemptFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Styles/Tokens.axaml"] = "the token file, the one place the values are written",
        ["Styles/ContrastTheme.cs"] = "the contrast code: it gives every token its system colour under a contrast theme; those colours are the system's, not PaperRoute's",
        ["Styles/SystemContrastColors.cs"] = "the contrast code: Windows' own contrast colour sets, and the stand-in set used off Windows",
    };

    private static readonly Rule[] s_rules =
    [
        new("names a colour", ".axaml", ColourPattern(), _ => true),
        new("names a colour", ".cs", ColourPattern(), _ => true),
        new("names a font family", ".axaml", AxamlPattern("FontFamily"), IsNotAnExtension),
        new("names a font family", ".cs", CodeFontFamilyPattern(), _ => true),
        new("names a font size", ".axaml", AxamlPattern("FontSize"), IsNotAnExtension),
        new("names a font size", ".cs", CodeFontSizePattern(), _ => true),
        new("names a corner radius", ".axaml", AxamlPattern("CornerRadius"), value => IsNotAnExtension(value) && NonZeroPattern().IsMatch(value)),
        new("names a corner radius", ".cs", CodeCornerRadiusPattern(), value => NonZeroPattern().IsMatch(value)),
        new("names a duration", ".axaml", AxamlPattern("Duration"), IsNotAnExtension),
        new("names a duration", ".cs", CodeDurationPattern(), _ => true),
        new("names a numeric font weight", ".axaml", AxamlPattern("FontWeight"), value => NumberPattern().IsMatch(value)),
        new("names a numeric font weight", ".cs", CodeFontWeightPattern(), _ => true),
    ];

    // Each literal as "path:line: what it names: the line", and the number of
    // literals each exempt file holds (so a test can see the scan read them).
    public static (List<string> Findings, Dictionary<string, int> Exempt) Scan(IEnumerable<(string Path, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var findings = new List<string>();
        var exempt = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string path, string text) in files)
        {
            string normal = ThemeOwnership.WithoutComments(path, text);
            string[] lines = normal.Split('\n');
            var found = new List<(int Index, string Line)>();
            foreach (Rule rule in s_rules.Where(rule => path.EndsWith(rule.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (Match match in rule.Pattern.Matches(normal))
                {
                    Group value = match.Groups["value"];
                    if (!rule.IsLiteral(value.Success ? value.Value.Trim() : match.Value))
                    {
                        continue;
                    }

                    int line = normal.AsSpan(0, match.Index).Count('\n');
                    found.Add((match.Index, $"{path}:{line + 1}: {rule.What}: {lines[line].Trim()}"));
                }
            }

            if (ExemptFiles.ContainsKey(path))
            {
                exempt[path] = found.Select(item => item.Line).Distinct(StringComparer.Ordinal).Count();
            }
            else
            {
                findings.AddRange(found.OrderBy(item => item.Index).Select(item => item.Line).Distinct(StringComparer.Ordinal));
            }
        }

        return (findings, exempt);
    }

    private static bool IsNotAnExtension(string value) => !value.StartsWith('{');

    // An attribute (FontSize="12", TextElement.FontSize="12") or a Setter
    // (Property="FontSize" Value="12", in either order), its value captured.
    private static Regex AxamlPattern(string property) => new(
        $@"(?<!\w){property}\s*=\s*""(?<value>[^""]*)""" +
        $@"|<Setter\s+Property=""(?:\w+\.)?{property}""\s+Value=""(?<value>[^""]*)""" +
        $@"|<Setter\s+Value=""(?<value>[^""]*)""\s+Property=""(?:\w+\.)?{property}""",
        RegexOptions.CultureInvariant);

    // Six or eight hex digits after a #, not part of a longer name
    // (ContentPresenter#PART_ContentPresenter, fonts:Inter#Inter) or a
    // character reference (&#x22EF;).
    [GeneratedRegex(@"(?<![\w&#])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})(?![\w])")]
    private static partial Regex ColourPattern();

    [GeneratedRegex(@"\bFontFamily\s*=\s*(?:""|new\b)|\bnew\s+FontFamily\s*\(|\bFontFamily\.Parse\s*\(")]
    private static partial Regex CodeFontFamilyPattern();

    [GeneratedRegex(@"\bFontSize\s*=\s*\d")]
    private static partial Regex CodeFontSizePattern();

    [GeneratedRegex(@"\bnew\s+CornerRadius\s*\((?<value>[^)]*)\)|\bCornerRadius\s*=\s*(?<value>\d[\d.]*)")]
    private static partial Regex CodeCornerRadiusPattern();

    [GeneratedRegex(@"\bDuration\s*=\s*(?:TimeSpan\.(?:From\w+|Parse)|new\s+TimeSpan)")]
    private static partial Regex CodeDurationPattern();

    [GeneratedRegex(@"\bFontWeight\s*=\s*(?:\(\s*FontWeight\s*\)\s*)?\d")]
    private static partial Regex CodeFontWeightPattern();

    [GeneratedRegex(@"[1-9]")]
    private static partial Regex NonZeroPattern();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumberPattern();

    private sealed record Rule(string What, string Extension, Regex Pattern, Func<string, bool> IsLiteral);
}
