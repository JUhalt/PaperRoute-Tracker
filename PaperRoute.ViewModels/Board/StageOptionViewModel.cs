namespace PaperRoute.ViewModels.Board;

// One item of the stage box: "All stages (N)" or "Name (count)" (Form1.vb
// StageFilterOption). ToString returns the text, so a screen reader reading
// the item reads it rather than the class name (Avalonia issue #17000).
public sealed class StageOptionViewModel
{
    public StageOptionViewModel(PaperStage? stage, string name, int count)
    {
        Stage = stage;
        Name = name ?? string.Empty;
        Count = count;
        Text = BoardRules.StageOptionText(Name, count);
    }

    // Null for "All stages".
    public PaperStage? Stage { get; }

    public string Name { get; }

    public int Count { get; }

    public string Text { get; }

    public override string ToString() => Text;

    // Form1.vb SameStageOption (:1877-1887).
    internal bool SameAs(StageOptionViewModel? other) =>
        other is not null
        && Nullable.Equals(Stage, other.Stage)
        && Name == other.Name
        && Count == other.Count;
}
