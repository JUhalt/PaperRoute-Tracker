using System.Text.RegularExpressions;
using Avalonia.Media;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Legibility;

namespace PaperRoute.Desktop.Tests.Styles;

// The theme and legibility rules of d1 6.2 that need no window: T1, T2, T3,
// and the port of UiTheme.Blend.
[TestClass]
public sealed partial class TokenTests
{
    private static readonly string[] s_variants = ["Light", "Dark"];

    private static readonly string[] s_stages =
        ["Idea", "Draft", "Submitted", "UnderReview", "Revision", "Accepted", "InPress", "Published"];

    private static readonly string[] s_tagPalettes =
        ["Teal", "Blue", "Violet", "Rose", "Amber", "Green", "Slate", "Orange"];

    // T1: every text and background pair the Board uses, in Light and Dark, at
    // 4.5:1. The design asked only 3:1 for muted and disabled text (R8); today's
    // suite holds every pair to 4.5:1, muted and disabled text included
    // (WorkflowThemeKeyboardSmokeTests.DrawnPairs, its "off" buttons), and
    // UiTheme.MutedText promises 4.5:1 on the board and card, so the trial does too.
    [TestMethod]
    public void T1_EveryTextPairTheBoardUsesHasItsContrast()
    {
        var failures = new List<string>();
        int checkedPairs = 0;
        foreach (string variant in s_variants)
        {
            IReadOnlyDictionary<string, Color> colors = TokenFile.Colors(variant);
            foreach ((string where, string ink, string surface) in BoardPairs())
            {
                double ratio = Contrast.ContrastRatio(colors[ink], colors[surface]);
                checkedPairs++;
                if (ratio < Thresholds.Text)
                {
                    failures.Add($"{variant}: {where}: {ink} on {surface} is {ratio:F2}:1");
                }
            }
        }

        Assert.IsGreaterThan(40, checkedPairs, "Too few pairs were checked.");
        Assert.IsEmpty(failures, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    // T2: the eight stage pills and the eight tag palettes, Light and Dark, at
    // 4.5:1 (WorkTypeAndTagTests.EveryTagColorIsLegibleInBothThemes today).
    [TestMethod]
    public void T2_EveryStageAndTagPairIsLegibleInBothThemes()
    {
        var failures = new List<string>();
        foreach (string variant in s_variants)
        {
            IReadOnlyDictionary<string, Color> colors = TokenFile.Colors(variant);
            foreach (string pair in s_stages.Select(stage => "Stage" + stage).Concat(s_tagPalettes.Select(palette => "Tag" + palette)))
            {
                double ratio = Contrast.ContrastRatio(colors[pair + "ForegroundBrush"], colors[pair + "BackgroundBrush"]);
                if (ratio < Thresholds.Text)
                {
                    failures.Add($"{variant}: {pair} is {ratio:F2}:1");
                }
            }
        }

        Assert.IsEmpty(failures, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

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
        IEnumerable<string> pairs = s_stages.Select(stage => "Stage" + stage).Concat(s_tagPalettes.Select(palette => "Tag" + palette))
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

    // Where each pair is drawn: the ink and surface keys, from r1 5.1's "Board
    // use" column and today's pair list (WorkflowThemeKeyboardSmokeTests.DrawnPairs).
    private static IEnumerable<(string Where, string Ink, string Surface)> BoardPairs()
    {
        string[] inks =
        [
            "PrimaryTextBrush", "SecondaryTextBrush", "MutedTextBrush", "AccentBrush", "AccentSecondaryBrush",
            "WarningBrush", "DangerBrush",
        ];
        foreach (string surface in new[] { "BoardBackgroundBrush", "CardBackgroundBrush", "HeaderBackgroundBrush" })
        {
            foreach (string ink in inks)
            {
                yield return ("text on a page, card or header", ink, surface);
            }
        }

        yield return ("a hovered secondary button", "PrimaryTextBrush", "HoverBackgroundBrush");
        yield return ("a hovered quiet button (the card's ⋯)", "AccentBrush", "HoverBackgroundBrush");
        yield return ("a hovered danger button", "DangerBrush", "HoverBackgroundBrush");
        yield return ("a hovered neutral chip", "SecondaryTextBrush", "HoverBackgroundBrush");
        yield return ("the example banner and the welcome's privacy note", "PrimaryTextBrush", "AccentMutedBackgroundBrush");
        yield return ("an active chip", "AccentBrush", "AccentMutedBackgroundBrush");
        yield return ("a pressed link on a tinted panel", "AccentSecondaryBrush", "AccentMutedBackgroundBrush");
        yield return ("a warning chip", "WarningBrush", "WarningMutedBackgroundBrush");
        yield return ("a danger chip", "DangerBrush", "DangerMutedBackgroundBrush");
        yield return ("a warning chip, hovered", "WarningBrush", ThemeTokens.WarningChipHoverBrush);
        yield return ("a danger chip, hovered", "DangerBrush", ThemeTokens.DangerChipHoverBrush);
        yield return ("an active chip, hovered", "AccentBrush", ThemeTokens.AccentChipHoverBrush);
        yield return ("a primary button", "OnAccentTextBrush", "AccentBrush");
        yield return ("a primary button, hovered", "OnAccentTextBrush", ThemeTokens.AccentHoverBrush);
        yield return ("a primary button, pressed", "OnAccentTextBrush", ThemeTokens.AccentPressedBrush);
        yield return ("an off button (filled roles)", "MutedTextBrush", "BoardBackgroundBrush");
        yield return ("an off quiet button on a card", "MutedTextBrush", "CardBackgroundBrush");
    }

    [GeneratedRegex(@"\{StaticResource\s+(?:ResourceKey=)?(?<key>[^\s,}]+)[^}]*\}")]
    private static partial Regex StaticResourcePattern();

    [GeneratedRegex(@"\{DynamicResource\s+(?:ResourceKey=)?(?<key>[^\s,}]+)[^}]*\}")]
    private static partial Regex DynamicResourcePattern();
}
