using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace PaperRoute.Desktop.Tests.Legibility;

// The sweeps' bad inputs (the harness design, part 2.1): small pages, each
// shown through the same Sweep.Read as the real subjects, that hold one
// deliberate fault beside parts that pass, so a teeth test can show that the
// rule names the fault and nothing else.
// Limits of headless: HeadlessLimits.md
internal static class TeethGallery
{
    // R5's bad input: on the page's own background, two panels with no
    // children, both drawn in #E0E0E0 (relative luminance 0.745). The first
    // is filled with that solid colour, so both its brush and its own pixels
    // are light. The second is filled with a gradient whose two stops are
    // that colour: not one solid colour, so its brush names nothing, and only
    // the reading of its own pixels can find it (as it must find a template
    // part or an opacity).
    public static SweepSubject LightPanel { get; } = new("Teeth/LightPanel", null, 400, 260, _ =>
    {
        Color light = Color.FromRgb(0xE0, 0xE0, 0xE0);
        var page = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        page.Children.Add(Text("A light panel in Dark", "PrimaryTextBrush"));
        page.Children.Add(new Border
        {
            Name = "LightPanel",
            Width = 160,
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(light),
        });
        page.Children.Add(new Border
        {
            Name = "LightGradient",
            Width = 160,
            Height = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new LinearGradientBrush { GradientStops = { new GradientStop(light, 0), new GradientStop(light, 1) } },
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

    // R8's bad input: on the page's own background, two disabled buttons of
    // the default role, which the off style draws in muted text on the board
    // colour. The second also carries the class "faded", and a style on the
    // page sets Opacity 0.3 on a disabled "faded" button, so its text and
    // fill are drawn faint over the page: the opacity look of a disabled
    // control, which leaves its tokens as they are.
    public static SweepSubject FadedOff { get; } = new("Teeth/FadedOff", null, 400, 160, _ =>
    {
        var page = new StackPanel { Margin = new Thickness(20), Spacing = 12, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        page.Styles.Add(new Style(selector => selector.OfType<Button>().Class("faded").Class(":disabled"))
        {
            Setters = { new Setter(Visual.OpacityProperty, 0.3) },
        });
        page.Children.Add(new Button { Name = "Off", Content = "Off", IsEnabled = false });
        page.Children.Add(new Button { Name = "Faded", Classes = { "faded" }, Content = "Faded", IsEnabled = false });
        return page;
    });

    // R9's bad input for edges: on the page's own background, two empty
    // panels with a 1 px border. The first's border is the card border
    // (WindowText under a contrast set); the second's is the brush the
    // window's background is drawn with, so its edge is in the window colour
    // and cannot be seen.
    public static SweepSubject WindowEdge { get; } = new("Teeth/WindowEdge", null, 400, 200, _ =>
    {
        var page = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        page.Children.Add(Outline("Outlined", "CardBorderBrush"));
        page.Children.Add(Outline("WindowEdge", "BoardBackgroundBrush"));
        return page;
    });

    // R9's bad inputs for focus: two buttons of the default role and two
    // text boxes holding text. The first button shows its focus ring when it
    // takes the keyboard focus; the second has no focus adorner, and nothing
    // else in its look follows focus, so taking the focus changes none of its
    // pixels. The first text box turns its frame to the accent; the second's
    // frame part keeps its border, fill and width as local values, which no
    // focus style outranks, so taking the focus changes only its caret and
    // selection, which R9 leaves out (Sweep.TextAreas).
    public static SweepSubject NoFocusVisual { get; } = new("Teeth/NoFocusVisual", null, 440, 200, _ =>
    {
        var buttons = new StackPanel { Spacing = 16, Orientation = Orientation.Horizontal };
        buttons.Children.Add(new Button { Name = "Ringed", Content = "With a ring" });
        buttons.Children.Add(new Button { Name = "NoFocusVisual", Content = "No focus visual", FocusAdorner = null });
        var boxes = new StackPanel { Spacing = 16, Orientation = Orientation.Horizontal };
        boxes.Children.Add(new TextBox { Name = "Framed", Text = "A frame", Width = 160 });
        var unframed = new TextBox { Name = "NoFocusFrame", Text = "No frame", Width = 160 };
        unframed.TemplateApplied += (_, applied) =>
        {
            Border frame = applied.NameScope.Find<Border>("PART_BorderElement") ?? throw new InvalidOperationException("The text box's template has no PART_BorderElement.");
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("CardBorderBrush"));
            frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable("CardBackgroundBrush"));
            frame.BorderThickness = new Thickness(1);
        };
        boxes.Children.Add(unframed);
        var page = new StackPanel { Margin = new Thickness(20), Spacing = 16, VerticalAlignment = VerticalAlignment.Top };
        page.Children.Add(buttons);
        page.Children.Add(boxes);
        return page;
    });

    // An empty panel, 160 x 40, with a 1 px border in a token brush, as {DynamicResource}.
    private static Border Outline(string name, string brush)
    {
        var border = new Border
        {
            Name = name,
            Width = 160,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderThickness = new Thickness(1),
        };
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable(brush));
        return border;
    }

    // A 12 px Regular text in a token ink, as {DynamicResource}.
    private static TextBlock Text(string text, string ink, string? name = null)
    {
        var block = new TextBlock { Name = name, Text = text, FontSize = 12, FontWeight = FontWeight.Normal };
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(ink));
        return block;
    }
}
