namespace PaperRoute.ViewModels.Board;

// The colour a card's text is drawn in, by role. Today's Form1 picks the
// colours themselves (UiTheme.SecondaryText, MutedText, WarningColor,
// DangerColor); the trial's view maps each tone to its brush.
public enum CardTone
{
    Secondary,
    Muted,
    Warning,
    Danger,
}

// A Needs Attention chip's tone at rest (Form1.vb RefreshAttentionDashboard,
// :948-981). The active chip is drawn in the accent instead (IsActive).
public enum ChipTone
{
    Neutral,
    Warning,
    Danger,
}
