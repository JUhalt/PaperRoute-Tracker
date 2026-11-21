using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace PaperRoute.Desktop.Styles;

// The one part of the Inputs family a style cannot reach. Fluent's TextBox
// template (Avalonia 12.1.6) sets its TextBlock#PART_Placeholder to Opacity 0.5
// itself, and a value set in a template outranks a style setter, so
// Inputs.axaml's MutedText placeholder drew at half strength, about 2:1 against
// the field. Today's placeholder is solid MutedText (about 5:1). When a text
// box's template is applied, its placeholder's opacity is set back to 1.
public static class InputTemplates
{
    private static bool s_registered;

    public static void Register()
    {
        if (s_registered)
        {
            return;
        }

        s_registered = true;
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TextBox>(OnTemplateApplied, RoutingStrategies.Direct);
    }

    private static void OnTemplateApplied(TextBox box, TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<TextBlock>("PART_Placeholder") is { } placeholder)
        {
            placeholder.Opacity = 1;
        }
    }
}