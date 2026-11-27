using System.Text.RegularExpressions;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R1, every ink on its surface at 4.5:1, run over the real token file in
// Light and Dark (the harness design, part 2.4; the contrast cases join it
// with R2's theme cases). The trial's T1 and T2 grew into the first two tests
// here; R1_FailsADeliberatelyLowContrastToken (RuleTeethTests.cs) gives R1
// its teeth through the same reader and the same rule.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed partial class R1Tests
{
    // The theme cases R1 runs in today.
    internal static IReadOnlyList<string> Variants { get; } = ["Light", "Dark"];

    // The properties whose brush is drawn as text, and those whose brush is
    // drawn under it (Fill: the route dots are shapes, not borders).
    private static readonly HashSet<string> s_inkProperties =
        new(["Foreground", "PlaceholderForeground", "SelectionForegroundBrush"], StringComparer.Ordinal);

    private static readonly HashSet<string> s_surfaceProperties = new(["Background", "Fill"], StringComparer.Ordinal);

    // Tokens the styles paint as a background that never carries text, each
    // with its reason. An entry that is no longer such a background fails.
    private static readonly Dictionary<string, string> s_neverUnderText = new(StringComparer.Ordinal)
    {
        ["MutedTextBrush"] = "the route dots' fill (Board.axaml, Ellipse.route-dot and .closed): a dot, with no text on it",
        ["CardBorderBrush"] = "1 px hairlines: the rail's rule (Rail.axaml, Border.rail-rule) and the shelf bar's baseline (Tabs.axaml, Border.hairline)",
        ["SubtleBorderBrush"] = "the 1 px line inside a card (Card.axaml, Border.divider)",
    };

    public TestContext TestContext { get; set; } = null!;

    // Was T1_EveryTextPairTheBoardUsesHasItsContrast.
    // Every text and background pair the Board uses, in Light and Dark, at
    // 4.5:1. The design asked only 3:1 for muted and disabled text (R8); today's
    // suite holds every pair to 4.5:1, muted and disabled text included
    // (WorkflowThemeKeyboardSmokeTests.DrawnPairs, its "off" buttons), and
    // UiTheme.MutedText promises 4.5:1 on the board and card, so the trial does too.
    [TestMethod]
    public void R1_EveryTextPairHasItsContrast()
    {
        var findings = new List<Finding>();
        int checkedPairs = 0;
        foreach (string variant in Variants)
        {
            findings.AddRange(Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.Text));
            checkedPairs += TokenPairs.Text.Count;
        }

        Assert.IsGreaterThan(40, checkedPairs, "Too few pairs were checked.");
        Assert.IsEmpty(findings, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // Was T2_EveryStageAndTagPairIsLegibleInBothThemes.
    // The eight stage pills and the eight tag palettes, Light and Dark, at
    // 4.5:1 (WorkTypeAndTagTests.EveryTagColorIsLegibleInBothThemes today).
    [TestMethod]
    public void R1_EveryStageAndTagPairHasItsContrast()
    {
        var findings = new List<Finding>();
        foreach (string variant in Variants)
        {
            findings.AddRange(Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.StagesAndTags));
        }

        Assert.AreEqual(16, TokenPairs.StagesAndTags.Count, "Eight stages and eight tag palettes.");
        Assert.IsEmpty(findings, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The completeness guard: every token an .axaml file uses as an ink
    // (Foreground, PlaceholderForeground, SelectionForegroundBrush) is the ink
    // of an R1 pair, and every token it uses as a background (Background, Fill)
    // is the surface of one, unless it is on the never-under-text list. So a
    // style that starts drawing text in a new colour, or on a new surface,
    // fails here until its pair is declared and R1 judges it.
    [TestMethod]
    public void R1_EveryInkAndSurfaceTheStylesUseIsInAPair()
    {
        var inks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var surfaces = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in TokenFile.AllAxamlFiles())
        {
            string name = Path.GetRelativePath(TokenFile.AxamlRoot, file).Replace('\\', '/');
            foreach ((string property, string key) in BrushUses(File.ReadAllText(file)))
            {
                string leaf = property[(property.LastIndexOf('.') + 1)..];
                if (s_inkProperties.Contains(leaf))
                {
                    inks.TryAdd(key, $"{name}, {property}");
                }
                else if (s_surfaceProperties.Contains(leaf))
                {
                    surfaces.TryAdd(key, $"{name}, {property}");
                }
            }
        }

        TestContext.WriteLine("Inks: " + string.Join(", ", inks.Keys));
        TestContext.WriteLine("Backgrounds: " + string.Join(", ", surfaces.Keys));
        Assert.IsGreaterThanOrEqualTo(10, inks.Count, "Too few inks were found; the scan of the .axaml files is broken.");
        Assert.IsGreaterThanOrEqualTo(10, surfaces.Count, "Too few backgrounds were found; the scan of the .axaml files is broken.");

        var pairInks = TokenPairs.All.Select(pair => pair.Ink).ToHashSet(StringComparer.Ordinal);
        var pairSurfaces = TokenPairs.All.Select(pair => pair.Surface).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach ((string key, string use) in inks.Where(ink => !pairInks.Contains(ink.Key)))
        {
            problems.Add($"{key} is drawn as text ({use}) but is the ink of no R1 pair.");
        }

        foreach ((string key, string use) in surfaces.Where(surface => !pairSurfaces.Contains(surface.Key) && !s_neverUnderText.ContainsKey(surface.Key)))
        {
            problems.Add($"{key} is drawn as a background ({use}) but is the surface of no R1 pair and not on the never-under-text list.");
        }

        foreach (string key in s_neverUnderText.Keys.Where(key => !surfaces.ContainsKey(key) || pairSurfaces.Contains(key)))
        {
            problems.Add($"{key} is on the never-under-text list, but no style draws it as a background, or an R1 pair already has it as a surface.");
        }

        Assert.IsEmpty(problems, "R1's pairs are incomplete:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // Each (property, token) an .axaml file sets through DynamicResource, as a
    // Setter or as an attribute.
    private static IEnumerable<(string Property, string Key)> BrushUses(string text)
    {
        foreach (Match match in SetterUse().Matches(text))
        {
            yield return (match.Groups["property"].Value, match.Groups["key"].Value);
        }

        foreach (Match match in AttributeUse().Matches(text).Where(match => match.Groups["property"].Value != "Value"))
        {
            yield return (match.Groups["property"].Value, match.Groups["key"].Value);
        }
    }

    [GeneratedRegex(@"Property=""(?<property>[\w.]+)""\s+Value=""\{DynamicResource\s+(?:ResourceKey=)?(?<key>\w+)\s*\}""")]
    private static partial Regex SetterUse();

    [GeneratedRegex(@"(?<=\s)(?<property>[\w.]+)=""\{DynamicResource\s+(?:ResourceKey=)?(?<key>\w+)\s*\}""")]
    private static partial Regex AttributeUse();
}
