namespace PaperRoute.ViewModels.ManuscriptPage;

// What the page's header shows: the saved record, not the working copy, as
// today (Form1.ManuscriptPage.vb:1050-1073), so it changes only when a save
// succeeds or the library changes. Values only, taken from the record when
// it is read; the view (V1a) adds the shelf text, the stage pill and the
// meta line from them.
public sealed record ManuscriptHeader(Guid Id, string Title, PaperStage Stage, string TargetJournal, ManuscriptLocation Location)
{
    public static ManuscriptHeader Of(Manuscript saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        return new(saved.Id, saved.Title ?? string.Empty, saved.CurrentStage, saved.TargetJournal ?? string.Empty, saved.Location);
    }
}
