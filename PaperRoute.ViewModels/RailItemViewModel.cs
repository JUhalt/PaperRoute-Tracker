using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels;

// One item of the rail (d1 3.3): a page (Board, Library, ...) or a command
// (Online, Settings, Help). Today each is a RailButton or RailCommandButton
// (Controls\RailItems.vb:233-344). In the trial only the Board has a command;
// every other item is disabled (d1 1.2), so a press on it does nothing.
public sealed partial class RailItemViewModel : ObservableObject
{
    // The tooltip of what the trial leaves out (d1 1.2).
    public const string NotInTrial = "Not in the trial (read-only)";

    // A count beside the label, such as Deadlines' items due now; 0 shows none
    // (RailButton.Badge, RailItems.vb:256-269).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount), nameof(CountText), nameof(HelpText))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public RailItemViewModel(string label, string glyph, string? shortcut, ICommand? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(glyph);
        Label = label;
        Glyph = glyph;
        Shortcut = shortcut;
        Command = command;
    }

    public string Label { get; }

    // The name of the line icon (RailGlyph in RailItems.vb:9-27); the view draws it.
    public string Glyph { get; }

    // "Ctrl+1" and so on, as today's tooltips give them (Form1.Shell.vb:249, 293).
    public string? Shortcut { get; }

    public ICommand? Command { get; }

    public bool IsEnabled => Command is not null;

    public string AccessibleName => Label;

    public string AutomationId => "Rail." + Glyph;

    public bool HasCount => Count > 0;

    public string CountText => Count.ToString(CultureInfo.CurrentCulture);

    // "Board (Ctrl+1)" as today; a disabled item says it is not in the trial.
    public string ToolTip => !IsEnabled
        ? NotInTrial
        : Shortcut is null ? Label : Label + " (" + Shortcut + ")";

    // Today's description of a count ("3 due", RailItems.vb:265), and for a
    // disabled item why it is off. Empty when there is neither.
    public string HelpText
    {
        get
        {
            var parts = new List<string>(2);
            if (HasCount)
            {
                parts.Add(CountText + " due");
            }

            if (!IsEnabled)
            {
                parts.Add(NotInTrial);
            }

            return string.Join(". ", parts);
        }
    }

    public override string ToString() => Label;
}
