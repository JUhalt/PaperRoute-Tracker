using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Board;

// One Needs Attention chip (Form1.vb RefreshAttentionDashboard :851-988 and
// StyleAttentionLabel :991-1025). Its count covers every manuscript on
// every shelf; it shows while its count is above zero or it is the active
// filter.
public sealed partial class ChipViewModel : ObservableObject
{
    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HelpText))]
    private bool _isActive;

    [ObservableProperty]
    private bool _isVisible;

    public ChipViewModel(AttentionFilter filter, ChipTone tone)
    {
        Filter = filter;
        Tone = tone;
    }

    public AttentionFilter Filter { get; }

    // The tone at rest; while active the chip is drawn in the accent.
    public ChipTone Tone { get; }

    // The chip's accessible description (FilterChip.vb:66).
    public string HelpText => BoardRules.ChipHelpText(IsActive);

    public override string ToString() => Text;

    internal void Update(int count, string text, bool active)
    {
        Count = count;
        Text = text;
        IsActive = active;
        IsVisible = count > 0 || active;
    }
}
