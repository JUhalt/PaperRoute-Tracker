namespace PaperRoute.ViewModels.ManuscriptPage;

// The working copy's stamps (build 9's design, 2.2 and 2.4): its target
// journal text, stage and File Drawer reason as the last load, discard or
// save left them, trimmed. Today's counterpart is the Shown values of
// EditManuscriptForm's RecordState (EditManuscriptForm.vb:54-62). A step that
// changes the stage moves the stage stamp (WorkingCopy.Edit); a save retakes
// them all (WorkingCopy.AcceptSaved), and Save's journal-link rule reads the
// journal text the link was made under.
public sealed record HeldValues(string TargetJournal, PaperStage Stage, string FileDrawerReason)
{
    public static HeldValues Of(Manuscript record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new HeldValues(
            (record.TargetJournal ?? string.Empty).Trim(),
            record.CurrentStage,
            (record.FileDrawerReason ?? string.Empty).Trim());
    }
}
