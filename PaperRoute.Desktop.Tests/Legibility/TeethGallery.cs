using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// The sweeps' bad inputs (the harness design, part 2.1): small pages, each
// shown through the same Sweep.Read as the real subjects, that hold one
// deliberate fault beside parts that pass, so a teeth test can show that the
// rule names the fault and nothing else.
// Limits of headless: HeadlessLimits.md
internal static class TeethGallery
{
    // R5's bad input: on the page's own background, a panel filled with
    // #E0E0E0 (relative luminance 0.745), with no children, so both its
    // brush and its own pixels are light.
    public static SweepSubject LightPanel { get; } = new("Teeth/LightPanel", null, 400, 200, _ =>
    {
        var page = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        page.Children.Add(Text("A light panel in Dark", "PrimaryTextBrush"));
        page.Children.Add(new Border
        {
            Name = "LightPanel",
            Width = 160,
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
        });
        return page;
    });

    // R7's bad input: on a card, two 12 px Regular texts in valid tokens
    // (primary text on the card, which R1 passes at 21:1 in Light): one as
    // it is, and one at Opacity 0.35, whose pixels are faint.
    public static SweepSubject FaintText { get; } = new("Teeth/FaintText", null, 400, 200, _ =>
    {
        var lines = new StackPanel { Spacing = 8 };
        lines.Children.Add(Text("Steady text at full strength", "PrimaryTextBrush", "Steady"));
        TextBlock faint = Text("Faint text at Opacity 0.35", "PrimaryTextBrush", "Faint");
        faint.Opacity = 0.35;
        lines.Children.Add(faint);
        var card = new Border
        {
            Name = "Card",
            Margin = new Thickness(20),
            Padding = new Thickness(14),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = lines,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CardBackgroundBrush"));
        return card;
    });

    // A 12 px Regular text in a token ink, as {DynamicResource}.
    private static TextBlock Text(string text, string ink, string? name = null)
    {
        var block = new TextBlock { Name = name, Text = text, FontSize = 12, FontWeight = FontWeight.Normal };
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(ink));
        return block;
    }
}
