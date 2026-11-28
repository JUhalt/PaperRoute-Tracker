using System.Globalization;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R2, contrast-theme tokens (plan :164; harness design 2.4): three checks,
// all token arithmetic, as plain functions. Input: keys, roles, pairs and
// colours. Output: findings, one line each. The rule does not assert; its
// tests do (R2Tests.cs).
//
//   1. Every token, derived brush and Fluent bridge key has a system role.
//   2. Every R1 pair, mapped through the roles, is a pair a contrast theme is
//      built from.
//   3. Every R1 pair has its contrast in each contrast set.
//
// Plus the check that the four contrast sets are Windows' own, read from the
// theme files.
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    // The section of a .theme file that holds the system colours.
    private const string ThemeColoursSection = "[Control Panel\\Colors]";

    // The pairs a Windows contrast theme is built from: its text, inactive
    // text and hyperlinks on the window, and the selected text on the
    // selection. Highlight as ink on the window is the link text's pair under
    // contrast (Board.axaml's link Foreground is AccentBrush, which is
    // Highlight), as in today's WinForms mapping. Windows pairs each of the
    // first four with its background when a person customises a theme's
    // colours; Highlight on Window carries no such promise, so only check 3's
    // ratios judge it.
    public static IReadOnlySet<(SystemColorRole Ink, SystemColorRole Surface)> R2DesignedPairs { get; } =
        new HashSet<(SystemColorRole, SystemColorRole)>
        {
            (SystemColorRole.WindowText, SystemColorRole.Window),
            (SystemColorRole.GrayText, SystemColorRole.Window),
            (SystemColorRole.HotTrack, SystemColorRole.Window),
            (SystemColorRole.HighlightText, SystemColorRole.Highlight),
            (SystemColorRole.Highlight, SystemColorRole.Window),
        };

    // The role each derived brush takes under contrast: the colour
    // ThemeTokens.DerivedColors derives it from. The primary button's hover
    // and press are shades of the accent (Highlight); a toned chip's hover
    // blends its muted fill with the card, both the window colour.
    public static IReadOnlyDictionary<string, SystemColorRole> R2DerivedRoles { get; } =
        new Dictionary<string, SystemColorRole>(StringComparer.Ordinal)
        {
            [ThemeTokens.AccentHoverBrush] = SystemColorRole.Highlight,
            [ThemeTokens.AccentPressedBrush] = SystemColorRole.Highlight,
            [ThemeTokens.WarningChipHoverBrush] = SystemColorRole.Window,
            [ThemeTokens.DangerChipHoverBrush] = SystemColorRole.Window,
            [ThemeTokens.AccentChipHoverBrush] = SystemColorRole.Window,
        };

    // The Fluent bridge's keys and roles (decision 10). The bridge does not
    // exist yet; when it lands, its keys join check 1 here.
    public static IReadOnlyDictionary<string, SystemColorRole> R2BridgeRoles { get; } =
        new Dictionary<string, SystemColorRole>(StringComparer.Ordinal);

    // Every role R2 knows: the tokens', the derived brushes' and the bridge's.
    public static IReadOnlyDictionary<string, SystemColorRole> R2Roles() =>
        ContrastTheme.TokenRoles.Concat(R2DerivedRoles).Concat(R2BridgeRoles)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    // Check 1: every key has a role, and every role is for a key.
    public static List<string> R2RoleFindings(IEnumerable<string> keys, IReadOnlyDictionary<string, SystemColorRole> roles)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(roles);
        var known = new HashSet<string>(keys, StringComparer.Ordinal);
        var findings = known.Where(key => !roles.ContainsKey(key)).Order(StringComparer.Ordinal)
            .Select(key => $"{key} has no system role").ToList();
        findings.AddRange(roles.Keys.Where(key => !known.Contains(key)).Order(StringComparer.Ordinal)
            .Select(key => $"{key} has a system role but is no token, derived brush or bridge key"));
        return findings;
    }

    // Check 2: every pair, mapped through the roles, is a designed pair.
    public static List<string> R2DesignedPairFindings(
        IEnumerable<(string Where, string Ink, string Surface)> pairs,
        IReadOnlyDictionary<string, SystemColorRole> roles)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(roles);
        var findings = new List<string>();
        foreach ((string where, string ink, string surface) in pairs)
        {
            if (!roles.TryGetValue(ink, out SystemColorRole inkRole) || !roles.TryGetValue(surface, out SystemColorRole surfaceRole))
            {
                findings.Add($"{where}: {ink} on {surface} has a key with no system role");
            }
            else if (!R2DesignedPairs.Contains((inkRole, surfaceRole)))
            {
                findings.Add($"{where}: {ink} on {surface} is {inkRole} on {surfaceRole}, not a designed pair");
            }
        }

        return findings;
    }

    // Check 3: every pair at the text threshold in one contrast set's colours
    // (ContrastTheme.Colors). Each ratio goes to measured.
    public static List<string> R2ContrastFindings(
        string set,
        IEnumerable<(string Where, string Ink, string Surface)> pairs,
        IReadOnlyDictionary<string, Color> colors,
        ICollection<string> measured)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(measured);
        var findings = new List<string>();
        foreach ((string where, string ink, string surface) in pairs)
        {
            double ratio = Contrast.ContrastRatio(colors[ink], colors[surface]);
            string line = string.Create(CultureInfo.InvariantCulture, $"{set}: {where}: {ink} on {surface} is {ratio:F2}:1");
            measured.Add(line);
            if (ratio < Thresholds.Text)
            {
                findings.Add(line);
            }
        }

        return findings;
    }

    // The six system colours of one .theme file's [Control Panel\Colors]
    // section, compared with the set the app holds for it. The file names the
    // selection Hilight and HilightText, and the hyperlink HotTrackingColor.
    public static List<string> R2ThemeFileFindings(string file, string text, SystemContrastColors expected)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inColours = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('['))
            {
                inColours = line.Equals(ThemeColoursSection, StringComparison.OrdinalIgnoreCase);
            }
            else if (inColours && line.IndexOf('=', StringComparison.Ordinal) is int equals and > 0)
            {
                values[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }
        }

        var findings = new List<string>();
        foreach ((string key, string role, Color colour) in new[]
        {
            ("Window", "Window", expected.Window),
            ("WindowText", "WindowText", expected.WindowText),
            ("GrayText", "GrayText", expected.GrayText),
            ("Hilight", "Highlight", expected.Highlight),
            ("HilightText", "HighlightText", expected.HighlightText),
            ("HotTrackingColor", "HotTrack", expected.HotTrack),
        })
        {
            string want = $"{colour.R} {colour.G} {colour.B}";
            if (!values.TryGetValue(key, out string? found))
            {
                findings.Add($"{file}: no {key} in {ThemeColoursSection}");
            }
            else if (!string.Join(' ', found.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Equals(want, StringComparison.Ordinal))
            {
                findings.Add($"{file}: {key} is {found}, but the app's {role} is {want}");
            }
        }

        return findings;
    }
}
