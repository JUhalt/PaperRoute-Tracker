namespace PaperRoute.ViewModels.ManuscriptPage;

// Save's rules, ported from today's edit window (EditManuscriptForm's
// CommitChanges, :4535-4687, and what it calls). Each is pure: it reads what
// it is given and changes nothing, so WorkingCopy.BuildCandidate applies them
// to a fresh clone and the working copy itself never changes (build 9's
// design, 2.2 and 2.5).
internal static class ManuscriptRules
{
    // A blank title stops Save (EditManuscriptForm.vb:4537-4553). Today a
    // message box says so; the page shows it beside the field (D4).
    public static bool IsTitleMissing(string? title) => string.IsNullOrWhiteSpace(title);

    // The stage policy (EditManuscriptForm.vb:4568-4586, through
    // EnsureWorkflowForRequestedStage, :4310-4321): null when the record
    // supports the stage, else what the stage needs first, in the core's
    // words. Today the window then offers to record the submission or
    // decision; until unit 6's "Record and Save" the page says what is
    // needed and keeps the stage held (D3).
    public static string? StepNeededFor(Manuscript record, PaperStage stage)
    {
        ArgumentNullException.ThrowIfNull(record);
        return ManuscriptStagePolicyService.IsStageSupported(record, stage)
            ? null
            : ManuscriptStagePolicyService.RequirementDescription(ManuscriptStagePolicyService.GetRequirement(stage));
    }

    // The location follows Published: into it with the stage, and back to
    // the Pipeline when the stage leaves it (EditManuscriptForm.vb:4588-4599).
    public static ManuscriptLocation LocationFor(PaperStage stage, ManuscriptLocation location) =>
        stage == PaperStage.Published ? ManuscriptLocation.Published
        : location == ManuscriptLocation.Published ? ManuscriptLocation.Pipeline
        : location;

    // The journal link once the target journal text is applied
    // (EditManuscriptForm.vb:4693-4732): kept while the text is the one it
    // was linked under or the linked journal's name, cleared otherwise.
    // Today the link is compared with the stored text, which the box leaves
    // alone until Save; here typing writes the text at once, so it is
    // compared with the text the link was made under (WorkingCopy.Stamps,
    // 2.4). Typing never clears the id, so typing the name back keeps it.
    public static Guid? TargetJournalIdFor(Guid? linkedId, string? linkedUnder, string journalText, IEnumerable<JournalRecord>? journals)
    {
        if (!linkedId.HasValue ||
            string.Equals((linkedUnder ?? string.Empty).Trim(), journalText, StringComparison.CurrentCultureIgnoreCase))
        {
            return linkedId;
        }

        JournalRecord? linked = (journals ?? []).FirstOrDefault(journal => journal is not null && journal.Id == linkedId.Value);
        return linked is not null && string.Equals(linked.Name, journalText, StringComparison.CurrentCultureIgnoreCase)
            ? linkedId
            : null;
    }

    // The stage-change event (EditManuscriptForm.vb:4616-4642): dated now,
    // the stages by their enum names as today, stamped as created. Now is
    // the core's clock (D9), where today's window reads DateTime.Now.
    public static HistoryEvent StageChanged(PaperStage from, PaperStage to, DateTime now)
    {
        var stageChange = new HistoryEvent
        {
            EventDate = now,
            Stage = to,
            Note = $"Stage changed from {from} to {to}.",
        };
        ChronologyProvenanceService.StampCreated(stageChange);
        return stageChange;
    }

    // The File Drawer reason event (EditManuscriptForm.vb:4735-4801): null
    // when the trimmed reasons are the same; otherwise "added", "updated" or
    // "cleared", dated now, at the stage given. The caller applies it only
    // while the record is in the File Drawer, and compares with the saved
    // record's reason (2.4, D16).
    public static HistoryEvent? FileDrawerReasonChanged(string? savedReason, string? newReason, PaperStage stage, DateTime now)
    {
        string before = (savedReason ?? string.Empty).Trim();
        string after = (newReason ?? string.Empty).Trim();
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return null;
        }

        string note = string.IsNullOrWhiteSpace(after) ? "File Drawer reason cleared."
            : string.IsNullOrWhiteSpace(before) ? "File Drawer reason added. Reason: " + after
            : "File Drawer reason updated. Reason: " + after;
        var reasonChange = new HistoryEvent
        {
            EventDate = now,
            Stage = stage,
            Note = note,
        };
        ChronologyProvenanceService.StampCreated(reasonChange);
        return reasonChange;
    }
}
