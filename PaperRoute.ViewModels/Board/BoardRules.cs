using System.Globalization;

namespace PaperRoute.ViewModels.Board;

// The Board's rules that live only in Form1 today, ported as they are
// (d1 3.2). Every rule the VB core already owns is called, never written
// again here: ManuscriptAttentionService.Evaluate, StageClockService.Describe,
// RouteSummaryService.Describe, WorkTypeService.DisplayName and ColorOf,
// ManuscriptStageSummaryService.CountByStage, AuthorLibrarySearchIndex and
// DeadlineService.CountDueNow. What is here is Form1's own: the texts, the
// filter, the seven comparisons, the insight order, the journal line and the
// card key.
internal static class BoardRules
{
    // The sort box's seven items, in order (Form1.vb BuildInterface, :546-552).
    public static readonly IReadOnlyList<string> SortTexts =
    [
        "Sort: Current order",
        "Sort: Title A-Z",
        "Sort: Title Z-A",
        "Sort: Most rejections",
        "Sort: Fewest rejections",
        "Sort: Newest stage change",
        "Sort: Oldest stage change",
    ];

    // The stage box's eight stages, in PaperStage order (Form1.vb
    // RefreshStageFilterItems, :1754-1761).
    public static readonly IReadOnlyList<PaperStage> BoxStages =
    [
        PaperStage.Idea,
        PaperStage.Draft,
        PaperStage.Submitted,
        PaperStage.UnderReview,
        PaperStage.Revision,
        PaperStage.Accepted,
        PaperStage.InPress,
        PaperStage.Published,
    ];

    public const string AllStagesName = "All stages";

    // The text BuildManuscriptInsight gives a missing target journal
    // (Form1.vb:1027); the card shows it on the journal line instead.
    public const string MissingTargetInsight = "No target journal selected";

    public const string StatusFieldSeparator = "\u001F";

    // Form1.vb FormatStage (:5081-5116).
    public static string FormatStage(PaperStage stage) => stage switch
    {
        PaperStage.Idea => "Idea",
        PaperStage.Draft => "Draft",
        PaperStage.Submitted => "Submitted",
        PaperStage.UnderReview => "Under Review",
        PaperStage.Revision => "Revision",
        PaperStage.Accepted => "Accepted",
        PaperStage.InPress => "In Press",
        PaperStage.Published => "Published",
        _ => stage.ToString(),
    };

    // StageFilterOption.ToString (Form1.vb): "Name (count)".
    public static string StageOptionText(string name, int count) =>
        name + " (" + Number(count) + ")";

    // The shelf names (Form1.vb RenderManuscripts, :2780-2798).
    public static string ShelfName(ManuscriptLocation location) => location switch
    {
        ManuscriptLocation.Pipeline => "Pipeline",
        ManuscriptLocation.Published => "Published",
        ManuscriptLocation.FileDrawer => "File Drawer",
        _ => location.ToString(),
    };

    // Form1.vb BuildSectionTitle (:2375-2398): "Name (total)", or
    // "Name (shown of total)" while a Board filter is on and hides some.
    public static string TabText(string name, int shown, int total, bool filtersActive) =>
        name + " (" + TabCount(shown, total, filtersActive) + ")";

    // The part of the tab text inside its brackets, which the tab paints
    // apart from the name (ShelfTabButton.vb OnPaint, :35-41).
    public static string TabCount(int shown, int total, bool filtersActive) =>
        filtersActive && shown != total
            ? Number(shown) + " of " + Number(total)
            : Number(total);

    // Form1.vb RenderManuscripts, the empty states (:2806-2878).
    public static string EmptyMessage(ManuscriptLocation location, int total) => location switch
    {
        ManuscriptLocation.Pipeline => total == 0
            ? "Nothing in progress. Choose + Add Manuscript to start one."
            : "No Pipeline manuscripts match the current search or filter.",
        ManuscriptLocation.Published => total == 0
            ? "Manuscripts move here when their stage is Published."
            : "No Published manuscripts match the current search or filter.",
        _ => total == 0
            ? "Work you set aside waits here until you restore it."
            : "No File Drawer manuscripts match the current search or filter.",
    };

    // Form1.vb RefreshAttentionDashboard (:904-945).
    public static string ChipText(AttentionFilter filter, int count, AppSettings settings) => filter switch
    {
        AttentionFilter.OverdueRevision => Number(count) + " overdue revision" + (count == 1 ? "" : "s"),
        AttentionFilter.RevisionDueSoon => Number(count) + " revision" + (count == 1 ? "" : "s") + " due soon",
        AttentionFilter.LongReview => Number(count) + " waiting " + Number(settings.LongReviewThresholdDays) + "+ days",
        AttentionFilter.MissingTargetJournal => Number(count) + " no target journal",
        AttentionFilter.RecentRejection => Number(count) + (settings.RecentRejectionThresholdDays == 1
            ? " rejected in last 1 day"
            : " rejected in last " + Number(settings.RecentRejectionThresholdDays) + " days"),
        _ => string.Empty,
    };

    // FilterChip.Tone (FilterChip.vb:66).
    public static string ChipHelpText(bool active) =>
        active ? "Filter applied. Press to clear it." : "Filter the board";

    // Whether the snapshot carries a chip's flag (Form1.vb
    // RefreshAttentionDashboard :867-899 and ManuscriptMatchesBoardFilters
    // :2162-2208 read the same five flags).
    public static bool HasFlag(ManuscriptAttentionSnapshot snapshot, AttentionFilter filter) => filter switch
    {
        AttentionFilter.OverdueRevision => snapshot.HasOverdueRevision,
        AttentionFilter.RevisionDueSoon => snapshot.IsRevisionDueSoon,
        AttentionFilter.LongReview => snapshot.IsLongWaitingManuscript,
        AttentionFilter.MissingTargetJournal => snapshot.HasMissingTargetJournal,
        AttentionFilter.RecentRejection => snapshot.WasRecentlyRejected,
        _ => true,
    };

    // Form1.vb ManuscriptMatchesBoardFilters (:2112-2213), with
    // ContainsSearchText (:2083-2097). The query comes trimmed
    // (GetVisibleManuscripts :2221-2222).
    public static bool Matches(
        Manuscript manuscript,
        string query,
        PaperStage? stage,
        AttentionFilter chip,
        ManuscriptAttentionSnapshot snapshot,
        AuthorLibrarySearchIndex searchIndex)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            bool matchesSearch =
                ContainsSearchText(manuscript.Title, query)
                || ContainsSearchText(manuscript.TargetJournal, query)
                || ContainsSearchText(searchIndex.BuildSearchText(manuscript), query)
                || (manuscript.Tags ?? []).Any(tag => ContainsSearchText(tag, query.TrimStart('#')))
                || (manuscript.WorkType != WorkType.Unspecified
                    && ContainsSearchText(WorkTypeService.DisplayName(manuscript.WorkType), query));

            if (!matchesSearch)
            {
                return false;
            }
        }

        if (stage.HasValue && manuscript.CurrentStage != stage.Value)
        {
            return false;
        }

        return chip == AttentionFilter.None || HasFlag(snapshot, chip);
    }

    // Form1.vb ContainsSearchText (:2083-2097).
    public static bool ContainsSearchText(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value)
        && value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;

    // Form1.vb GetVisibleManuscripts (:2247-2285) with the comparisons
    // CompareTitleAscending ... CompareStageDateAscending (:2293-2372):
    // List.Sort, as today, so the same input gives the same order (the sort
    // does not promise to keep equal items in their order). An index outside
    // 1 to 6 leaves the library's order.
    public static void Sort(
        List<Manuscript> manuscripts,
        int sortIndex,
        Func<Manuscript, ManuscriptAttentionSnapshot> snapshotOf)
    {
        switch (sortIndex)
        {
            case 1:
                manuscripts.Sort((first, second) => StringComparer.CurrentCultureIgnoreCase.Compare(first.Title, second.Title));
                break;
            case 2:
                manuscripts.Sort((first, second) => StringComparer.CurrentCultureIgnoreCase.Compare(second.Title, first.Title));
                break;
            case 3:
                manuscripts.Sort((first, second) => snapshotOf(second).RejectionCount.CompareTo(snapshotOf(first).RejectionCount));
                break;
            case 4:
                manuscripts.Sort((first, second) => snapshotOf(first).RejectionCount.CompareTo(snapshotOf(second).RejectionCount));
                break;
            case 5:
                manuscripts.Sort((first, second) => second.StageEnteredDate.CompareTo(first.StageEnteredDate));
                break;
            case 6:
                manuscripts.Sort((first, second) => first.StageEnteredDate.CompareTo(second.StageEnteredDate));
                break;
        }
    }

    // Form1.vb BuildManuscriptInsight (:1029-1181): the first match, in this
    // order, with the colour it is drawn in (here a tone). The missing target
    // journal comes before the filing suggestion, so a manuscript with no
    // journal is never told to consider filing.
    public static (string Text, CardTone Tone) Insight(
        Manuscript manuscript,
        ManuscriptAttentionSnapshot snapshot,
        int fileDrawerSuggestionThreshold)
    {
        if (snapshot.HasOverdueRevision && snapshot.OverdueDays.HasValue)
        {
            int days = snapshot.OverdueDays.Value;
            return ("Revision overdue by " + Number(days) + " day" + (days == 1 ? "" : "s"), CardTone.Danger);
        }

        if (snapshot.IsRevisionDueSoon && snapshot.RevisionDaysRemaining.HasValue)
        {
            int days = snapshot.RevisionDaysRemaining.Value;
            return days == 0
                ? ("Revision due today", CardTone.Warning)
                : ("Revision due in " + Number(days) + " day" + (days == 1 ? "" : "s"), CardTone.Warning);
        }

        if (snapshot.IsLongWaitingManuscript && snapshot.WaitingDays.HasValue)
        {
            int days = snapshot.WaitingDays.Value;
            return (days == 1 ? "Waiting 1 day" : "Waiting " + Number(days) + " days", CardTone.Warning);
        }

        if (snapshot.WasRecentlyRejected && snapshot.RejectionDaysAgo.HasValue)
        {
            int days = snapshot.RejectionDaysAgo.Value;
            return ("Rejected " + Number(days) + " day" + (days == 1 ? "" : "s") + " ago", CardTone.Danger);
        }

        if (snapshot.HasMissingTargetJournal)
        {
            return (MissingTargetInsight, CardTone.Warning);
        }

        if (manuscript.Location == ManuscriptLocation.Pipeline
            && snapshot.CanSuggestFiling
            && snapshot.RejectionCount >= fileDrawerSuggestionThreshold)
        {
            int count = snapshot.RejectionCount;
            return (Number(count) + (count == 1 ? " rejection; consider filing" : " rejections; consider filing"), CardTone.Warning);
        }

        return (string.Empty, CardTone.Secondary);
    }

    // Form1.vb CreateManuscriptCard (:3257-3266): the journal line.
    public static (string Text, CardTone Tone) JournalLine(Manuscript manuscript)
    {
        string journal = manuscript.TargetJournal ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(journal))
        {
            return (journal, CardTone.Secondary);
        }

        return manuscript.Location == ManuscriptLocation.Pipeline
            ? ("No target journal yet", CardTone.Warning)
            : ("No target journal", CardTone.Muted);
    }

    // Integer.ToString() as Form1 calls it (current culture).
    public static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);
}
