using System.Globalization;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R2, contrast-theme tokens (plan :164; harness design 2.4): three checks,
// all token arithmetic, as plain functions. Input: keys, roles, pairs and
// colours. Output: findings (Rules.cs), one line each. The rule does not
// assert; its tests do (R2Tests.cs).
//
//   1. Every token, derived brush and Fluent bridge key has a system role,
//      and a shown window resolves each to its role's colour.
//   2. Every R1 pair, mapped through the roles, is a pair a contrast theme is
//      built from.
//   3. Every R1 pair has its contrast in each contrast set.
//
// Plus the check that the four contrast sets are Windows' own, read from the
// theme files. A finding of check 3, of the window's resolution and of the
// theme files names its set as its theme case (hc1, hc2, hcblack, hcwhite,
// or fallback); the role table and check 2 hold for every set at once, so
// their findings name the theme case "contrast" (R2AnySet). A finding's
// subject is the pair, "<ink> on <surface>", for checks 2 and 3; the key for
// check 1, or "the window's variant" when the window is not in the set's
// variant; and for the theme files "<file> <key>" (such as "hc1.theme
// GrayText"), or "<file>" when the file is missing. The window's resolution
// is checked with each set entered from a Light base and from a Dark base;
// the base is named only in the finding's line, so one listed entry covers
// both.
// Limits of headless: HeadlessLimits.md
internal static partial class Rules
{
    // The theme case of an R2 finding that holds in every contrast set.
    public const string R2AnySet = "contrast";

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

    // Check 1, the role table: every key has a role, and every role is for a key.
    public static List<Finding> R2RoleFindings(IEnumerable<string> keys, IReadOnlyDictionary<string, SystemColorRole> roles)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(roles);
        var known = new HashSet<string>(keys, StringComparer.Ordinal);
        var findings = known.Where(key => !roles.ContainsKey(key)).Order(StringComparer.Ordinal)
            .Select(key => R2Finding(R2AnySet, key, $"{key} has no system role")).ToList();
        findings.AddRange(roles.Keys.Where(key => !known.Contains(key)).Order(StringComparer.Ordinal)
            .Select(key => R2Finding(R2AnySet, key, $"{key} has a system role but is no token, derived brush or bridge key")));
        return findings;
    }

    // Check 1, in a shown window: each key resolves, in one contrast set, to
    // the colour its role gives it there: a token to its system colour, a
    // derived brush to the colour ContrastTheme.Colors derives from its
    // role's, any other key with a role (a bridge key) to its role's system
    // colour. A key with no role at all is reported, not looked up. resolve
    // gives the colour of the solid brush a key resolves to in the window, or
    // null when it does not resolve to one; where names the set and the base
    // it was entered from.
    public static List<Finding> R2ResolutionFindings(
        string set,
        string where,
        IEnumerable<string> keys,
        IReadOnlyDictionary<string, SystemColorRole> roles,
        SystemContrastColors colors,
        Func<string, Color?> resolve)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(resolve);
        IReadOnlyDictionary<string, Color> derived = ContrastTheme.Colors(colors);
        var findings = new List<Finding>();
        foreach (string key in keys)
        {
            Color expected;
            if (ContrastTheme.TokenRoles.TryGetValue(key, out SystemColorRole tokenRole))
            {
                expected = ContrastTheme.ColorOf(colors, tokenRole);
            }
            else if (derived.TryGetValue(key, out Color derivedColour))
            {
                expected = derivedColour;
            }
            else if (roles.TryGetValue(key, out SystemColorRole otherRole))
            {
                expected = ContrastTheme.ColorOf(colors, otherRole);
            }
            else
            {
                findings.Add(R2Finding(set, key, $"{where}: {key} has no system role, so no colour is expected of it"));
                continue;
            }

            Color? found = resolve(key);
            if (found is not { } colour)
            {
                findings.Add(R2Finding(set, key, $"{where}: {key} does not resolve to a solid brush"));
            }
            else if (colour != expected)
            {
                string role = roles.TryGetValue(key, out SystemColorRole named) ? named.ToString() : "no role";
                findings.Add(R2Finding(set, key, $"{where}: {key} is {colour}, not {expected} ({role})"));
            }
        }

        return findings;
    }

    // Check 2: every pair, mapped through the roles, is a designed pair.
    public static List<Finding> R2DesignedPairFindings(IEnumerable<TokenPair> pairs, IReadOnlyDictionary<string, SystemColorRole> roles)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(roles);
        var findings = new List<Finding>();
        foreach (TokenPair pair in pairs)
        {
            string subject = $"{pair.Ink} on {pair.Surface}";
            if (!roles.TryGetValue(pair.Ink, out SystemColorRole inkRole) || !roles.TryGetValue(pair.Surface, out SystemColorRole surfaceRole))
            {
                findings.Add(R2Finding(R2AnySet, subject, $"{pair.Where}: {subject} has a key with no system role"));
            }
            else if (!R2DesignedPairs.Contains((inkRole, surfaceRole)))
            {
                findings.Add(R2Finding(R2AnySet, subject, $"{pair.Where}: {subject} is {inkRole} on {surfaceRole}, not a designed pair"));
            }
        }

        return findings;
    }

    // Check 3: every pair at the text threshold in one contrast set's colours
    // (ContrastTheme.Colors), named by the set.
    public static List<Finding> R2ContrastFindings(string set, IReadOnlyDictionary<string, Color> colors, IEnumerable<TokenPair> pairs) =>
        R2ContrastFindings(R2Ratios(set, colors, pairs));

    // Check 3's findings from its measured ratios: the pairs under 4.5:1.
    public static List<Finding> R2ContrastFindings(IEnumerable<PairRatio> ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        var findings = new List<Finding>();
        foreach (PairRatio measured in ratios.Where(measured => measured.Ratio < Thresholds.Text))
        {
            string subject = $"{measured.Pair.Ink} on {measured.Pair.Surface}";
            string line = string.Create(CultureInfo.InvariantCulture, $"{measured.ThemeCase}: {measured.Pair.Where}: {subject} is {measured.Ratio:F2}:1");
            findings.Add(R2Finding(measured.ThemeCase, subject, line));
        }

        return findings;
    }

    // What check 3 measures: every pair's ratio in one contrast set, passing
    // or not, by R1's arithmetic. The R2 run writes these to
    // legibility-r2-<set>.txt (Measurements).
    public static IReadOnlyList<PairRatio> R2Ratios(string set, IReadOnlyDictionary<string, Color> colors, IEnumerable<TokenPair> pairs) =>
        R1Ratios(set, colors, pairs);

    // The six system colours of one .theme file's [Control Panel\Colors]
    // section, compared with the set the app holds for it. The file names the
    // selection Hilight and HilightText, and the hyperlink HotTrackingColor.
    // A finding's theme case is the file's name without ".theme".
    public static List<Finding> R2ThemeFileFindings(string file, string text, SystemContrastColors expected)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        string set = Path.GetFileNameWithoutExtension(file);
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

        var findings = new List<Finding>();
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
                findings.Add(R2Finding(set, $"{file} {key}", $"{file}: no {key} in {ThemeColoursSection}"));
            }
            else if (!string.Join(' ', found.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Equals(want, StringComparison.Ordinal))
            {
                findings.Add(R2Finding(set, $"{file} {key}", $"{file}: {key} is {found}, but the app's {role} is {want}"));
            }
        }

        return findings;
    }

    private static Finding R2Finding(string themeCase, string subject, string line) => new("R2", themeCase, subject, line);
}
