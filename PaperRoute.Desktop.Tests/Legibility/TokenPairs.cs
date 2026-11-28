using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// One declared (ink, surface) pair of tokens, with where the app draws it.
internal readonly record struct TokenPair(string Where, string Ink, string Surface);

// The pairs R1 judges (the harness design, part 2.4): every text pair the
// Board draws (was T1's list) and the eight stage pills and eight tag palettes
// (was T2's). R1_EveryInkAndSurfaceTheStylesUseIsInAPair keeps the lists
// complete: a token the styles use as an ink or a background with no pair fails.
// Limits of headless: HeadlessLimits.md
internal static class TokenPairs
{
    public static IReadOnlyList<string> Stages { get; } =
        ["Idea", "Draft", "Submitted", "UnderReview", "Revision", "Accepted", "InPress", "Published"];

    public static IReadOnlyList<string> TagPalettes { get; } =
        ["Teal", "Blue", "Violet", "Rose", "Amber", "Green", "Slate", "Orange"];

    // Where each pair is drawn: the ink and surface keys, from r1 5.1's "Board
    // use" column and today's pair list (WorkflowThemeKeyboardSmokeTests.DrawnPairs),
    // then Appendix C.1's success and info inks, which no view draws yet: each
    // is paired from the day it is a token (the harness design, part 3.1).
    public static IReadOnlyList<TokenPair> Text { get; } = BoardPairs().Concat(TonePairs()).ToList();

    // Each stage's and tag palette's foreground on its own background
    // (WorkTypeAndTagTests.EveryTagColorIsLegibleInBothThemes today).
    public static IReadOnlyList<TokenPair> StagesAndTags { get; } =
        Stages.Select(stage => ("a stage pill", "Stage" + stage))
            .Concat(TagPalettes.Select(palette => ("a tag", "Tag" + palette)))
            .Select(pair => new TokenPair(pair.Item1, pair.Item2 + "ForegroundBrush", pair.Item2 + "BackgroundBrush"))
            .ToList();

    public static IEnumerable<TokenPair> All => Text.Concat(StagesAndTags);

    private static IEnumerable<TokenPair> BoardPairs()
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
                yield return new("text on a page, card or header", ink, surface);
            }
        }

        yield return new("a hovered secondary button", "PrimaryTextBrush", "HoverBackgroundBrush");
        yield return new("a hovered quiet button (the card's ⋯)", "AccentBrush", "HoverBackgroundBrush");
        yield return new("a hovered danger button", "DangerBrush", "HoverBackgroundBrush");
        yield return new("a hovered neutral chip", "SecondaryTextBrush", "HoverBackgroundBrush");
        yield return new("the example banner and the welcome's privacy note", "PrimaryTextBrush", "AccentMutedBackgroundBrush");
        yield return new("an active chip", "AccentBrush", "AccentMutedBackgroundBrush");
        yield return new("a pressed link on a tinted panel", "AccentSecondaryBrush", "AccentMutedBackgroundBrush");
        yield return new("a warning chip", "WarningBrush", "WarningMutedBackgroundBrush");
        yield return new("a danger chip", "DangerBrush", "DangerMutedBackgroundBrush");
        yield return new("a warning chip, hovered", "WarningBrush", ThemeTokens.WarningChipHoverBrush);
        yield return new("a danger chip, hovered", "DangerBrush", ThemeTokens.DangerChipHoverBrush);
        yield return new("an active chip, hovered", "AccentBrush", ThemeTokens.AccentChipHoverBrush);
        yield return new("a primary button", "OnAccentTextBrush", "AccentBrush");
        yield return new("a primary button, hovered", "OnAccentTextBrush", ThemeTokens.AccentHoverBrush);
        yield return new("a primary button, pressed", "OnAccentTextBrush", ThemeTokens.AccentPressedBrush);
        yield return new("an off button (filled roles)", "MutedTextBrush", "BoardBackgroundBrush");
        yield return new("an off quiet button on a card", "MutedTextBrush", "CardBackgroundBrush");
    }

    // The success and info tones (Appendix C.1, the harness design's part 3.1):
    // each ink on the board, on a card and on its own muted fill, as a pill or
    // a callout will draw it. No view draws them yet.
    private static IEnumerable<TokenPair> TonePairs()
    {
        yield return new("success text on a page (no view yet)", "SuccessBrush", "BoardBackgroundBrush");
        yield return new("success text on a card (no view yet)", "SuccessBrush", "CardBackgroundBrush");
        yield return new("a success pill or callout (no view yet)", "SuccessBrush", "SuccessMutedBackgroundBrush");
        yield return new("info text on a page (no view yet)", "InfoBrush", "BoardBackgroundBrush");
        yield return new("info text on a card (no view yet)", "InfoBrush", "CardBackgroundBrush");
        yield return new("an info pill or callout (no view yet)", "InfoBrush", "InfoMutedBackgroundBrush");
    }
}
