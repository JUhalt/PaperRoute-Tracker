namespace PaperRoute.ViewModels.ManuscriptPage;

// The three typed fields as the last load or discard left the working copy,
// after its reconciliation and not trimmed: the state the baseline was taken
// from (build 9's design, 2.4). Text typed back to one of them writes it as it
// was, so the page is not unsaved after a character is typed and removed.
// The original record is not that state: a load's reconciliation can rewrite
// the target journal (ManuscriptLifecycleService.SetCurrentTarget trims the
// submission's journal name). A save retakes them (W2).
public sealed record TypedValues(string Title, string TargetJournal, string FileDrawerReason)
{
    public static TypedValues Of(Manuscript record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new TypedValues(
            record.Title ?? string.Empty,
            record.TargetJournal ?? string.Empty,
            record.FileDrawerReason ?? string.Empty);
    }

    public string this[TypedField field] => field switch
    {
        TypedField.Title => Title,
        TypedField.TargetJournal => TargetJournal,
        TypedField.FileDrawerReason => FileDrawerReason,
        _ => throw new InvalidOperationException($"No typed field {field}."),
    };
}
