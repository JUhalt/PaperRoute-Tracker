using System.Text.RegularExpressions;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Legibility;

namespace PaperRoute.Desktop.Tests.Styles;

// The theme rules of d1 6.2 that need no window: T3, the token file's
// completeness, and the port of UiTheme.Blend. T1 and T2 grew into R1
// (Legibility/R1Tests.cs, the harness design, part 2.4).
[TestClass]
public sealed partial class TokenTests
{
    private static readonly string[] s_variants = ["Light", "Dark"];

    // T3: a brush in ThemeDictionaries resolves only through DynamicResource;
    // StaticResource throws or keeps the first theme's colour (r2 4.1). No
    // .axaml file may name a themed key, a derived brush, or any "...Brush"
    // key through StaticResource.
    [TestMethod]
    public void T3_ThemedBrushesAreUsedOnlyThroughDynamicResource()
    {
        var themed = new HashSet<string>(TokenFile.ThemedKeys().Values.SelectMany(keys => keys), StringComparer.Ordinal);
        themed.UnionWith(ThemeTokens.DerivedKeys);

        IReadOnlyList<string> files = TokenFile.AllAxamlFiles();
        Assert.IsGreaterThanOrEqualTo(10, files.Count, "The test output holds too few .axaml files; the copy in the project file is broken.");

        var failures = new List<string>();
        int dynamicUses = 0;
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            dynamicUses += DynamicResourcePattern().Matches(text).Count;
            foreach (Match match in StaticResourcePattern().Matches(text))
            {
                string key = match.Groups["key"].Value;
                if (themed.Contains(key) || key.EndsWith("Brush", StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetRelativePath(TokenFile.AxamlRoot, file)}: {match.Value}");
                }
            }
        }

        Assert.IsGreaterThan(100, dynamicUses, "The styles should reach their tokens through DynamicResource.");
        Assert.IsEmpty(failures, "Themed keys named through StaticResource:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    // A misspelt DynamicResource key fails silently at run time (the property
    // keeps its default), so every key the trial's own files name must be a token.
    [TestMethod]
    public void EveryDynamicResourceTheStylesNameIsAToken()
    {
        var known = new HashSet<string>(TokenFile.ThemeIndependentKeys(), StringComparer.Ordinal);
        IReadOnlyDictionary<string, IReadOnlySet<string>> themed = TokenFile.ThemedKeys();
        Assert.IsTrue(themed["Light"].SetEquals(themed["Dark"]),
            "Light and Dark must define the same keys. Only in Light: " + string.Join(", ", themed["Light"].Except(themed["Dark"])) +
            "; only in Dark: " + string.Join(", ", themed["Dark"].Except(themed["Light"])));
        known.UnionWith(themed["Light"]);
        known.UnionWith(ThemeTokens.DerivedKeys);

        var unknown = new List<string>();
        foreach (string file in TokenFile.AllAxamlFiles())
        {
            foreach (Match match in DynamicResourcePattern().Matches(File.ReadAllText(file)))
            {
                if (!known.Contains(match.Groups["key"].Value))
                {
                    unknown.Add($"{Path.GetRelativePath(TokenFile.AxamlRoot, file)}: {match.Value}");
                }
            }
        }

        Assert.IsEmpty(unknown, "DynamicResource keys that are not tokens:" + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }

    // Every brush of d1 5.1's table, in both variants.
    [TestMethod]
    public void TheTokenFileHoldsTheWholeTableInBothThemes()
    {
        string[] table =
        [
            "BoardBackgroundBrush", "HeaderBackgroundBrush", "CardBackgroundBrush", "CardBorderBrush", "SubtleBorderBrush",
            "PrimaryTextBrush", "SecondaryTextBrush", "MutedTextBrush", "AccentBrush", "AccentSecondaryBrush",
            "AccentMutedBackgroundBrush", "OnAccentTextBrush", "HoverBackgroundBrush", "WarningBrush", "WarningMutedBackgroundBrush",
            "DangerBrush", "DangerMutedBackgroundBrush",
        ];
        IEnumerable<string> pairs = TokenPairs.Stages.Select(stage => "Stage" + stage).Concat(TokenPairs.TagPalettes.Select(palette => "Tag" + palette))
            .SelectMany(pair => new[] { pair + "BackgroundBrush", pair + "ForegroundBrush" });

        foreach (string variant in s_variants)
        {
            IReadOnlyDictionary<string, Color> colors = TokenFile.Colors(variant);
            string[] missing = table.Concat(pairs).Where(key => !colors.ContainsKey(key)).ToArray();
            Assert.IsEmpty(missing, $"{variant} lacks: {string.Join(", ", missing)}");
            Assert.Contains("OutlineThickness", TokenFile.ThemedKeys()[variant], $"{variant} lacks OutlineThickness.");
        }
    }

    // UiTheme.Blend rounds with VB's CInt, which rounds half to even: a channel
    // landing on .5 goes to the even neighbour, and the fraction is clamped.
    [TestMethod]
    public void BlendRoundsAsTheVisualBasicOriginalDoes()
    {
        Assert.AreEqual(Color.FromRgb(0, 2, 2), ThemeTokens.Blend(Color.FromRgb(1, 3, 5), Color.FromRgb(0, 0, 0), 0.5F),
            "1 -> 0.5 rounds to 0, 3 -> 1.5 to 2, 5 -> 2.5 to 2.");
        Assert.AreEqual(Color.FromRgb(10, 20, 30), ThemeTokens.Blend(Color.FromRgb(10, 20, 30), Color.FromRgb(200, 200, 200), -1F));
        Assert.AreEqual(Color.FromRgb(200, 200, 200), ThemeTokens.Blend(Color.FromRgb(10, 20, 30), Color.FromRgb(200, 200, 200), 2F));
        Assert.AreEqual((byte)0x80, ThemeTokens.Blend(Color.FromArgb(0x80, 0, 0, 0), Colors.White, 0.5F).A, "The alpha is the base colour's.");

        // The Light primary button hovered: #0F766E 10 % of the way to black.
        Assert.AreEqual(Color.FromRgb(14, 106, 99), ThemeTokens.Blend(Color.FromRgb(15, 118, 110), Colors.Black, 0.1F));
    }

    // Every value Tokens.axaml writes, pinned (the harness design, part 3.1,
    // as WinForms pins UiTheme's colours in TokensBefore): the accepted Light
    // and Dark colours and every theme-independent token. A token whose value
    // changes, a new token, or a removed one fails here until this table
    // changes in the same pull request, so review sees every token change; an
    // unplanned one stops and asks the maintainer first. The check runs again
    // on a copy of the file with one colour changed, and must name exactly it.
    [TestMethod]
    public void TokenValuesArePinned()
    {
        List<string> problems = PinProblems(TokenFile.TokensPath);
        Assert.IsEmpty(problems, "Token values differ from the pinned table:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        string folder = Path.Combine(AssemblySetup.StorageRoot, "pins-teeth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, "Tokens.axaml");
        string real = File.ReadAllText(TokenFile.TokensPath);
        const string good = "<SolidColorBrush x:Key=\"CardBorderBrush\" Color=\"#3D444F\" />";
        Assert.AreEqual(2, real.Split(good).Length, "The real token file must hold Dark's CardBorderBrush, #3D444F, exactly once.");
        File.WriteAllText(copy, real.Replace(good, "<SolidColorBrush x:Key=\"CardBorderBrush\" Color=\"#3D4450\" />", StringComparison.Ordinal));
        Assert.AreEqual(
            "Dark CardBorderBrush is #3D4450 in the file, pinned #3D444F.",
            string.Join(Environment.NewLine, PinProblems(copy)),
            "A copy with one Dark colour changed by one step.");
    }

    // The differences between a token file's values and the pinned tables.
    private static List<string> PinProblems(string tokensPath)
    {
        var pinned = new Dictionary<(string Scope, string Key), string>();
        foreach ((string key, string value) in s_pinnedIndependent)
        {
            pinned.Add(("", key), value);
        }

        foreach ((string key, string light, string dark) in s_pinnedThemed)
        {
            pinned.Add(("Light", key), light);
            pinned.Add(("Dark", key), dark);
        }

        var problems = new List<string>();
        var seen = new HashSet<(string, string)>();
        foreach ((string scope, string key, string value) in TokenFile.Values(tokensPath))
        {
            string name = scope.Length == 0 ? key : scope + " " + key;
            seen.Add((scope, key));
            if (!pinned.TryGetValue((scope, key), out string? expected))
            {
                problems.Add($"{name} is {value} in the file and not pinned.");
            }
            else if (!string.Equals(value, expected, StringComparison.Ordinal))
            {
                problems.Add($"{name} is {value} in the file, pinned {expected}.");
            }
        }

        foreach ((string scope, string key) in pinned.Keys.Where(entry => !seen.Contains(entry)))
        {
            problems.Add($"{(scope.Length == 0 ? key : scope + " " + key)} is pinned but not in the file.");
        }

        return problems;
    }

    // The theme-independent tokens: key, value as written.
    private static readonly (string Key, string Value)[] s_pinnedIndependent =
    [
        ("CardRadius", "10"),
        ("ControlRadius", "8"),
        ("FullRadius", "999"),
        ("SpaceXs", "4"),
        ("SpaceSm", "8"),
        ("SpaceMd", "12"),
        ("SpaceLg", "16"),
        ("SpaceXl", "24"),
        ("CardPadding", "14"),
        ("BodyFontSize", "13"),
        ("PageTitleFontSize", "21"),
        ("CardTitleFontSize", "14"),
        ("InsightFontSize", "12"),
        ("MetaFontSize", "12"),
        ("BadgeFontSize", "11"),
        ("CaptionFontSize", "11"),
        ("ControlContentThemeFontSize", "13"),
    ];

    // The themed tokens: key, Light value, Dark value, as written.
    private static readonly (string Key, string Light, string Dark)[] s_pinnedThemed =
    [
        ("BoardBackgroundBrush", "#F4F7F8", "#181B20"),
        ("HeaderBackgroundBrush", "#FFFFFF", "#1F2329"),
        ("CardBackgroundBrush", "#FFFFFF", "#282C33"),
        ("CardBorderBrush", "#DAE4E7", "#3D444F"),
        ("SubtleBorderBrush", "#E7EEF0", "#323841"),
        ("PrimaryTextBrush", "#000000", "#F0F0F0"),
        ("SecondaryTextBrush", "#5B6977", "#ACB5C0"),
        ("MutedTextBrush", "#64717D", "#8A95A1"),
        ("AccentBrush", "#0F766E", "#2DD4BF"),
        ("AccentSecondaryBrush", "#0E7490", "#22D3EE"),
        ("AccentMutedBackgroundBrush", "#CCFBF1", "#144544"),
        ("OnAccentTextBrush", "#FFFFFF", "#07201D"),
        ("HoverBackgroundBrush", "#EBF4F5", "#323841"),
        ("WarningBrush", "#B45309", "#FBBF24"),
        ("WarningMutedBackgroundBrush", "#FEF3C7", "#5C3F15"),
        ("DangerBrush", "#BE232D", "#F87C7C"),
        ("DangerMutedBackgroundBrush", "#FEE2E2", "#5B2126"),
        ("StageIdeaBackgroundBrush", "#EDE9FE", "#483678"),
        ("StageIdeaForegroundBrush", "#5B21B6", "#DDD6FE"),
        ("StageDraftBackgroundBrush", "#E5E7EB", "#41444B"),
        ("StageDraftForegroundBrush", "#374151", "#E5E7EB"),
        ("StageSubmittedBackgroundBrush", "#DBEAFE", "#1E406E"),
        ("StageSubmittedForegroundBrush", "#1D4ED8", "#BFDBFE"),
        ("StageUnderReviewBackgroundBrush", "#CCFBF1", "#115E59"),
        ("StageUnderReviewForegroundBrush", "#0F766E", "#99F6E4"),
        ("StageRevisionBackgroundBrush", "#FEF3C7", "#5C3F15"),
        ("StageRevisionForegroundBrush", "#B45309", "#FDE68A"),
        ("StageAcceptedBackgroundBrush", "#DCFCE7", "#1F4C2F"),
        ("StageAcceptedForegroundBrush", "#15803D", "#BBF7D0"),
        ("StageInPressBackgroundBrush", "#CFFAFE", "#134E4A"),
        ("StageInPressForegroundBrush", "#0E7490", "#A5F3FC"),
        ("StagePublishedBackgroundBrush", "#D1FAE5", "#114E3C"),
        ("StagePublishedForegroundBrush", "#047857", "#A7F3D0"),
        ("TagTealBackgroundBrush", "#CCFBF1", "#134E4A"),
        ("TagTealForegroundBrush", "#115E59", "#99F6E4"),
        ("TagBlueBackgroundBrush", "#DBEAFE", "#1E3A8A"),
        ("TagBlueForegroundBrush", "#1E40AF", "#BFDBFE"),
        ("TagVioletBackgroundBrush", "#EDE9FE", "#4C1D95"),
        ("TagVioletForegroundBrush", "#5B21B6", "#DDD6FE"),
        ("TagRoseBackgroundBrush", "#FFE4E6", "#881337"),
        ("TagRoseForegroundBrush", "#9F1239", "#FECDD3"),
        ("TagAmberBackgroundBrush", "#FEF3C7", "#78350F"),
        ("TagAmberForegroundBrush", "#92400E", "#FDE68A"),
        ("TagGreenBackgroundBrush", "#DCFCE7", "#14532D"),
        ("TagGreenForegroundBrush", "#166534", "#BBF7D0"),
        ("TagSlateBackgroundBrush", "#E2E8F0", "#334155"),
        ("TagSlateForegroundBrush", "#334155", "#E2E8F0"),
        ("TagOrangeBackgroundBrush", "#FFEDD5", "#7C2D12"),
        ("TagOrangeForegroundBrush", "#9A3412", "#FED7AA"),
        ("OutlineThickness", "0", "0"),
    ];

    [GeneratedRegex(@"\{StaticResource\s+(?:ResourceKey=)?(?<key>[^\s,}]+)[^}]*\}")]
    private static partial Regex StaticResourcePattern();

    [GeneratedRegex(@"\{DynamicResource\s+(?:ResourceKey=)?(?<key>[^\s,}]+)[^}]*\}")]
    private static partial Regex DynamicResourcePattern();
}
