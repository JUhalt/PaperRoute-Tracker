namespace PaperRoute.ViewModels.ManuscriptPage;

// What a save that succeeded copies into the session's own record (build 9's
// design, 2.2 and 2.5, session step 5). Today's window copies 25 properties
// by hand (EditManuscriptForm.vb:4830-4910, with CopyReadinessStateToOriginal
// in EditManuscriptForm.Readiness.vb) and leaves PublicationMatches to the
// library. Here every settable Manuscript property is either in Copied or
// named in LeftOut with its reason, and CommitTests fails on a property in
// neither list, so a property added to the model later cannot be dropped
// without a test saying so.
public static class ManuscriptCommit
{
    private static readonly (string Name, Action<Manuscript, Manuscript> Copy)[] Copies =
    [
        (nameof(Manuscript.Id), (from, to) => to.Id = from.Id),
        (nameof(Manuscript.Title), (from, to) => to.Title = from.Title),
        (nameof(Manuscript.CoAuthors), (from, to) => to.CoAuthors = from.CoAuthors),
        (nameof(Manuscript.Authors), (from, to) => to.Authors = from.Authors),
        (nameof(Manuscript.TargetJournal), (from, to) => to.TargetJournal = from.TargetJournal),
        (nameof(Manuscript.TargetJournalId), (from, to) => to.TargetJournalId = from.TargetJournalId),
        (nameof(Manuscript.ManuscriptUrl), (from, to) => to.ManuscriptUrl = from.ManuscriptUrl),
        (nameof(Manuscript.Metadata), (from, to) => to.Metadata = from.Metadata),
        (nameof(Manuscript.WorkType), (from, to) => to.WorkType = from.WorkType),
        (nameof(Manuscript.Tags), (from, to) => to.Tags = from.Tags),
        (nameof(Manuscript.JournalShortlist), (from, to) => to.JournalShortlist = from.JournalShortlist),
        (nameof(Manuscript.RelatedLinks), (from, to) => to.RelatedLinks = from.RelatedLinks),
        (nameof(Manuscript.Reminders), (from, to) => to.Reminders = from.Reminders),
        (nameof(Manuscript.Versions), (from, to) => to.Versions = from.Versions),
        (nameof(Manuscript.CurrentVersionId), (from, to) => to.CurrentVersionId = from.CurrentVersionId),
        (nameof(Manuscript.ReadinessProfiles), (from, to) => to.ReadinessProfiles = from.ReadinessProfiles),
        (nameof(Manuscript.SubmissionPackets), (from, to) => to.SubmissionPackets = from.SubmissionPackets),
        (nameof(Manuscript.CurrentStage), (from, to) => to.CurrentStage = from.CurrentStage),
        (nameof(Manuscript.Location), (from, to) => to.Location = from.Location),
        (nameof(Manuscript.StageEnteredDate), (from, to) => to.StageEnteredDate = from.StageEnteredDate),
        (nameof(Manuscript.RevisionDeadline), (from, to) => to.RevisionDeadline = from.RevisionDeadline),
        (nameof(Manuscript.FileDrawerDate), (from, to) => to.FileDrawerDate = from.FileDrawerDate),
        (nameof(Manuscript.FileDrawerReason), (from, to) => to.FileDrawerReason = from.FileDrawerReason),
        (nameof(Manuscript.History), (from, to) => to.History = from.History),
        (nameof(Manuscript.Submissions), (from, to) => to.Submissions = from.Submissions),
    ];

    // The properties CopyInto copies, in today's order.
    public static IReadOnlyList<string> Copied { get; } = [.. Copies.Select(copy => copy.Name)];

    // The settable properties CopyInto leaves as the record has them, each
    // with its reason.
    public static IReadOnlyDictionary<string, string> LeftOut { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Manuscript.PublicationMatches)] =
            "the library's own, as today: no page edits them, and the candidate carries the record's matches in its own list " +
            "only so the file keeps them (BuildCandidate), so the record keeps its list",
    };

    // Copies the written record into the session's own: a clone of it first,
    // as today (CopyWorkingToOriginal), so the two never share a list or an
    // object and a later change to one cannot reach the other.
    public static void CopyInto(Manuscript from, Manuscript to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        Manuscript committed = ManuscriptCloneService.CloneManuscript(from);
        foreach ((string _, Action<Manuscript, Manuscript> copy) in Copies)
        {
            copy(committed, to);
        }
    }
}
