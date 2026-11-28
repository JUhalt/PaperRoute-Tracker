using System.Text.RegularExpressions;

namespace PaperRoute.Desktop.Tests.Styles;

// AppTheme is the one owner of the theme variant (harness design 3.4): no
// other code of PaperRoute.Desktop sets a RequestedThemeVariant or reads the
// system's contrast state. This scan reads the app's .cs files (Source\,
// copied to the test output by the project file) and .axaml files (Axaml\)
// as text, with comments left out, and names every line that does either
// outside the files allowed to. PaperRoute.ViewModels is not scanned: it has
// no Avalonia reference, so it can do neither.
internal static partial class ThemeOwnership
{
    private static readonly Rule[] s_rules =
    [
        // Application, window or any other scope; a property assignment
        // (plain or ??=), an object initializer, an XAML attribute, a style's
        // Setter (Property="RequestedThemeVariant"), XAML's property-element
        // form, or the property object itself (SetValue, Bind, SetCurrentValue).
        // The one exception: the measurement's window shows the variant it
        // measures, whatever AppTheme says (BoardMeasurement, d1 7.3).
        new("sets a theme variant", WriterPattern(), ["Styles/AppTheme.cs", "Measurement/BoardMeasurement.cs"]),

        // Avalonia's report of the system's theme and contrast preference, and
        // its change event: ContrastPreference, which AppTheme follows, and the
        // platform check, which only records it (PlatformCheckWindow.FactsFor).
        new("reads the system's colour values", ColourValuesPattern(), ["Styles/ContrastPreference.cs", "Platform/PlatformCheckWindow.cs"]),

        // The reader of those values that AppTheme follows (declared in ContrastPreference.cs).
        new("makes a contrast preference from the platform", FromPlatformPattern(), ["Styles/AppTheme.cs", "Styles/ContrastPreference.cs"]),

        // Windows' own system colours and contrast flag.
        new("reads Windows' contrast colours", WindowsContrastPattern(), ["Styles/SystemContrastColors.cs", "Styles/ContrastPreference.cs"]),
    ];

    public static string SourceRoot => Path.Combine(AppContext.BaseDirectory, "Source");

    // Every allowed use, in Scan's "path: what it does" form, so a test can
    // fail on an exception that no longer matches anything.
    public static IReadOnlyList<string> Exceptions =>
        s_rules.SelectMany(rule => rule.AllowedIn.Select(path => $"{path}: {rule.What}")).ToList();

    // Every .cs and .axaml file of PaperRoute.Desktop, with its path relative
    // to the project folder ('/' separated) and its text.
    public static IReadOnlyList<(string Path, string Text)> AppFiles()
    {
        var files = new List<(string Path, string Text)>();
        foreach ((string root, string pattern) in new[] { (SourceRoot, "*.cs"), (TokenFile.AxamlRoot, "*.axaml") })
        {
            foreach (string file in Directory.GetFiles(root, pattern, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                files.Add((Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file)));
            }
        }

        return files;
    }

    // Each offending line as "path:line: what it does: the line", and each
    // allowed use as "path: what it does" (so a test can see the scan found
    // AppTheme's own).
    public static (List<string> Findings, List<string> Allowed) Scan(IEnumerable<(string Path, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var findings = new List<string>();
        var allowed = new List<string>();
        foreach ((string path, string text) in files)
        {
            string[] lines = WithoutComments(path, text).Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Rule rule in s_rules)
                {
                    if (!rule.Pattern.IsMatch(lines[index]))
                    {
                        continue;
                    }

                    if (rule.AllowedIn.Contains(path, StringComparer.Ordinal))
                    {
                        allowed.Add($"{path}: {rule.What}");
                    }
                    else
                    {
                        findings.Add($"{path}:{index + 1}: {rule.What}: {lines[index].Trim()}");
                    }
                }
            }
        }

        return (findings, allowed.Distinct(StringComparer.Ordinal).ToList());
    }

    // The text with its comments blanked, keeping every line where it was
    // (LiteralScan reads the files the same way).
    public static string WithoutComments(string path, string text)
    {
        string normal = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Regex block = path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ? XmlCommentPattern() : BlockCommentPattern();
        normal = block.Replace(normal, match => new string('\n', match.Value.Count(c => c == '\n')));
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? LineCommentPattern().Replace(normal, string.Empty) : normal;
    }

    [GeneratedRegex(@"\bRequestedThemeVariant(Property\b|\s*(\?\?)?=(?!=)|""|>)")]
    private static partial Regex WriterPattern();

    [GeneratedRegex(@"\bGetColorValues\s*\(|\bColorValuesChanged\b")]
    private static partial Regex ColourValuesPattern();

    [GeneratedRegex(@"\bFromPlatform\s*\(")]
    private static partial Regex FromPlatformPattern();

    [GeneratedRegex(@"\bSystemContrastColors\.Read\s*\(|\bGetSysColor\b|\bSPI_GETHIGHCONTRAST\b")]
    private static partial Regex WindowsContrastPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex XmlCommentPattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"//[^\n]*")]
    private static partial Regex LineCommentPattern();

    private sealed record Rule(string What, Regex Pattern, string[] AllowedIn);
}
