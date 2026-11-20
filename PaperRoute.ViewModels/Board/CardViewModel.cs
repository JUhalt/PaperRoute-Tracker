using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaperRoute.ViewModels.Board;

// One card on a shelf (d1 3.2, "The card's texts"; Form1.vb
// CreateManuscriptCard :3131-3346). It copies what it shows, since the
// core's models do not notify: a changed manuscript gives a new key and so a
// new card. A card whose key is unchanged is kept from render to render and
// only told which record it now stands for (Form1.vb CardFor :2954-2992).
//
// The core's reference types carry no null information, so every string
// and list read from the manuscript goes through ?? here (d1 3.5).
//
// Its commands (d1 3.3) go through the Board that made it: Open and View
// route give the trial's notices (d1 1.2), and a click on the tag strip
// puts the first tag in the search box, as today (Form1.vb:3285-3289).
public sealed partial class CardViewModel : ObservableObject
{
    private readonly BoardViewModel _board;
    private Manuscript _manuscript;

    internal CardViewModel(BoardViewModel board, CardFacts facts, (Guid Id, int Occurrence) slot, AuthorLibraryData? library)
    {
        _board = board;
        Manuscript manuscript = facts.Manuscript;
        _manuscript = manuscript;
        Id = slot.Id;
        Occurrence = slot.Occurrence;
        Key = facts.Key;

        Title = manuscript.Title ?? string.Empty;
        Stage = manuscript.CurrentStage;
        Location = manuscript.Location;
        StageText = BoardRules.FormatStage(Stage).ToUpperInvariant();
        StageClass = StageClassOf(Stage);

        Status = facts.StatusText;
        StatusTone = facts.StatusIsUrgent ? facts.InsightTone : CardTone.Muted;
        IsStatusUrgent = facts.StatusIsUrgent;

        (JournalText, JournalTone) = BoardRules.JournalLine(manuscript);
        HasJournal = !string.IsNullOrWhiteSpace(manuscript.TargetJournal);

        List<string> tags = (manuscript.Tags ?? []).Select(tag => tag ?? string.Empty).ToList();
        Tags = tags.Select(tag => new TagViewModel(tag, WorkTypeService.ColorOf(tag, library))).ToList();
        TagsName = tags.Count == 0 ? string.Empty : "Tags: " + string.Join(", ", tags);
        TagsToolTip = tags.Count == 0 ? string.Empty : string.Join(", ", tags) + " (click to search the board)";
        FirstTag = tags.Count == 0 ? null : tags[0];

        RouteDots = facts.Route.Dots ?? [];
        RouteText = facts.Route.Text ?? string.Empty;

        // Form1.vb:3253: stage, status, journal or "no target journal",
        // route text, the empty ones left out.
        AccessibleDescription = string.Join(
            ", ",
            new[]
            {
                BoardRules.FormatStage(Stage),
                Status,
                HasJournal ? manuscript.TargetJournal ?? string.Empty : "no target journal",
                RouteText,
            }.Where(part => part.Length > 0));

        MoreActionsName = "More actions for " + Title;
        ViewRouteName = "View route for " + Title;
        AutomationKey = "Card." + Id.ToString("D") + "." + Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MenuItems = CardMenuItemViewModel.For(Location);
    }

    // The record the card stands for now (its actions, from step 9, act on
    // it).
    public Manuscript Manuscript
    {
        get => _manuscript;
        private set => SetProperty(ref _manuscript, value);
    }

    // The card's place in the cache: the manuscript's Id and how many shown
    // records with that Id came before it in this render (Form1.vb CardSlot,
    // :2938-2949).
    public Guid Id { get; }

    public int Occurrence { get; }

    // Form1.vb CardKey: the card is rebuilt when this changes.
    public string Key { get; }

    public string Title { get; }

    public PaperStage Stage { get; }

    public ManuscriptLocation Location { get; }

    // FormatStage in upper case, the pill's text (Form1.vb:3138).
    public string StageText { get; }

    // The style class of the stage pill.
    public string StageClass { get; }

    // The status line; empty hides it (Form1.vb:3199).
    public string Status { get; }

    public bool HasStatus => Status.Length > 0;

    public CardTone StatusTone { get; }

    // Drawn in the insight font (bold) rather than the meta font.
    public bool IsStatusUrgent { get; }

    public string JournalText { get; }

    public CardTone JournalTone { get; }

    // The journal line's tooltip is the journal, only when there is one.
    public bool HasJournal { get; }

    // That tooltip, or null for none (Form1.vb:3268).
    public string? JournalToolTip => HasJournal ? JournalText : null;

    public IReadOnlyList<TagViewModel> Tags { get; }

    public bool HasTags => Tags.Count > 0;

    // The tag strip's accessible name, "Tags: a, b" (TagChips.vb:91).
    public string TagsName { get; }

    // The tag strip's tooltip (Form1.vb:3285).
    public string TagsToolTip { get; }

    // That tooltip, or null for a card with no tags (no strip today).
    public string? TagStripToolTip => HasTags ? TagsToolTip : null;

    // What a click on the tag strip puts in the search box (Form1.vb:3288).
    public string? FirstTag { get; }

    // One per submission, oldest first, at most six (RouteSummaryService).
    public IReadOnlyList<RouteDotState> RouteDots { get; }

    public bool HasRouteDots => RouteDots.Count > 0;

    public string RouteText { get; }

    // The title's accessible description.
    public string AccessibleDescription { get; }

    public string MoreActionsName { get; }

    public string ViewRouteName { get; }

    // The start of the automation IDs of the card's parts (d1 4.3):
    // "Card.<id>.<occurrence>", then ".Title", ".More" and ".Route".
    public string AutomationKey { get; }

    // The ⋯ menu (Form1.vb BuildCardMenu :3488-3549), every item off (d1 1.2).
    public IReadOnlyList<CardMenuItemViewModel> MenuItems { get; }

    // A click on the card's body or its title, Enter or bare Space on the
    // title (Form1.vb:3352-3372).
    [RelayCommand]
    private void Open() => _board.OpenCardCommand.Execute(this);

    // "View route →" (Form1.vb:3374-3377).
    [RelayCommand]
    private void ViewRoute() => _board.ViewRouteCommand.Execute(this);

    // A left click on the tag strip (Form1.vb:3286-3289).
    [RelayCommand]
    private void ApplyFirstTag() => _board.ApplyFirstTagCommand.Execute(this);

    // A kept card stands for the record now in its slot, and its tag colours
    // follow the author library as it is now (Form1.vb CardFor :2971-2977).
    internal void Refresh(Manuscript manuscript, AuthorLibraryData? library)
    {
        Manuscript = manuscript;
        foreach (TagViewModel tag in Tags)
        {
            tag.Palette = WorkTypeService.ColorOf(tag.Text, library);
        }
    }

    private static string StageClassOf(PaperStage stage) => stage switch
    {
        PaperStage.Idea => "stage-idea",
        PaperStage.Draft => "stage-draft",
        PaperStage.Submitted => "stage-submitted",
        PaperStage.UnderReview => "stage-under-review",
        PaperStage.Revision => "stage-revision",
        PaperStage.Accepted => "stage-accepted",
        PaperStage.InPress => "stage-in-press",
        PaperStage.Published => "stage-published",
        _ => "stage-idea",
    };
}
