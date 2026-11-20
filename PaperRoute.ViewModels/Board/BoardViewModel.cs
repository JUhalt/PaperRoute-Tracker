using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Board;

// The Board (d1 3.2, 3.3): search, the stage and sort boxes, the Needs
// Attention chips, the three shelves and their cards. Render() ports Form1's
// RenderManuscripts (Form1.vb:2666-2931): it always works everything out
// again from the session's library, as today, and the 28 places that render
// today become LibraryChanged and the filter changes here.
//
// Not here yet (later steps of d1 section 8): the open-list rule of #338
// for the two boxes (step 5: today a box change renders at once, as on a
// closed box), the commands that give notices (open, route, deadlines, add)
// and the tag-strip search (step 9), and the view's own state (columns,
// realized shelves).
public sealed partial class BoardViewModel : ObservableObject
{
    // Form1.vb boardSearchDebounceTimer (:79-81).
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(200);

    private readonly ILibrarySession _session;
    private readonly IDebouncer _searchDebouncer;

    // The attention snapshots of this render, one per record, keyed by
    // reference so two records that share an Id keep their own (d1 3.2
    // item 1; today's cache is keyed by Id, Form1.vb:87-90, 2400-2428).
    private readonly Dictionary<Manuscript, ManuscriptAttentionSnapshot> _snapshots = new(ReferenceEqualityComparer.Instance);

    // Form1.vb manuscriptCards (:100-107): kept cards by (Id, occurrence).
    private readonly Dictionary<(Guid Id, int Occurrence), CardViewModel> _cards = [];

    // While set, a change of the search, a box or the chip filter does not
    // render (Form1.vb suppressBoardFilterEvents, :58).
    private bool _suppressFilterEvents;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<StageOptionViewModel> _stageOptions = [];

    [ObservableProperty]
    private int _selectedStageIndex;

    [ObservableProperty]
    private int _selectedSortIndex;

    // Form1.vb activeAttentionFilter (:14).
    [ObservableProperty]
    private AttentionFilter _activeFilter = AttentionFilter.None;

    [ObservableProperty]
    private bool _nothingNeedsAttention;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedShelf))]
    private int _selectedShelfIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private bool _hasActiveFilters;

    // Shows the welcome in place of the Board (Form1.Welcome.vb:21-34).
    [ObservableProperty]
    private bool _isLibraryEmpty;

    // The rail's figures, refreshed with the Board (Form1.Shell.vb
    // UpdateRailCounts, :419-435).
    [ObservableProperty]
    private int _manuscriptCount;

    [ObservableProperty]
    private int _deadlinesDueCount;

    // Renders so far; tests read it.
    [ObservableProperty]
    private int _renderCount;

    public BoardViewModel(ILibrarySession session, IDebouncer searchDebouncer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(searchDebouncer);
        _session = session;
        _searchDebouncer = searchDebouncer;

        StageOptions = BuildStageOptions([]);

        Chips =
        [
            new ChipViewModel(AttentionFilter.OverdueRevision, ChipTone.Danger),
            new ChipViewModel(AttentionFilter.RevisionDueSoon, ChipTone.Warning),
            new ChipViewModel(AttentionFilter.LongReview, ChipTone.Warning),
            new ChipViewModel(AttentionFilter.MissingTargetJournal, ChipTone.Neutral),
            new ChipViewModel(AttentionFilter.RecentRejection, ChipTone.Danger),
        ];

        Shelves =
        [
            new ShelfViewModel(ManuscriptLocation.Pipeline),
            new ShelfViewModel(ManuscriptLocation.Published),
            new ShelfViewModel(ManuscriptLocation.FileDrawer),
        ];

        _session.LibraryChanged += OnLibraryChanged;
    }

    // The seven sort texts (Form1.vb:546-552).
    public IReadOnlyList<string> SortOptions => BoardRules.SortTexts;

    // Overdue, due soon, long wait, no journal, recent rejection.
    public IReadOnlyList<ChipViewModel> Chips { get; }

    // Pipeline, Published, File Drawer.
    public IReadOnlyList<ShelfViewModel> Shelves { get; }

    // Pipeline at start; not saved between sessions, as today.
    public ShelfViewModel SelectedShelf => Shelves[Math.Clamp(SelectedShelfIndex, 0, Shelves.Count - 1)];

    // The changes the last render made to each shelf's cards (Pipeline,
    // Published, File Drawer): a count, or -1 for one Reset.
    public IReadOnlyList<int> LastSyncChanges { get; private set; } = [0, 0, 0];

    // The SyncInto threshold (d1 3.2 item 8), tuned by measurement.
    public int ResetThreshold { get; set; } = SyncedCollection<CardViewModel>.DefaultResetThreshold;

    // The stage chosen, or null for All stages (also for no choice at all,
    // as a box with no selected item filters nothing, Form1.vb:2153-2155).
    private PaperStage? SelectedStage =>
        SelectedStageIndex >= 0 && SelectedStageIndex < StageOptions.Count
            ? StageOptions[SelectedStageIndex].Stage
            : null;

    // Form1.vb RenderManuscripts (:2666-2931).
    public void Render()
    {
        _searchDebouncer.Stop();

        IReadOnlyList<Manuscript> library = _session.Manuscripts;
        List<Manuscript> all = library.Where(manuscript => manuscript is not null).ToList();
        AppSettings settings = _session.Settings ?? new AppSettings();
        DateTime today = _session.Today;

        // 1. One attention snapshot per record (Form1.vb:2670-2673, 2400-2428).
        _snapshots.Clear();
        foreach (Manuscript manuscript in all)
        {
            _snapshots[manuscript] = ManuscriptAttentionService.Evaluate(
                manuscript,
                today,
                settings.RevisionWarningDays,
                settings.LongReviewThresholdDays,
                settings.RecentRejectionThresholdDays);
        }

        // 2. The stage box and the chips (:2675-2676).
        RefreshStageOptions(all);
        RefreshChips(all, settings);

        DropStaleCards(all);

        // 3. Totals per shelf (:2692-2711).
        var totals = new Dictionary<ManuscriptLocation, int>
        {
            [ManuscriptLocation.Pipeline] = 0,
            [ManuscriptLocation.Published] = 0,
            [ManuscriptLocation.FileDrawer] = 0,
        };
        foreach (Manuscript manuscript in all)
        {
            if (totals.ContainsKey(manuscript.Location))
            {
                totals[manuscript.Location]++;
            }
        }

        // 4. Filter and sort (GetVisibleManuscripts, :2215-2290).
        string query = (SearchText ?? string.Empty).Trim();
        PaperStage? stage = SelectedStage;
        AttentionFilter chip = ActiveFilter;
        List<Manuscript> visible = all
            .Where(manuscript => BoardRules.Matches(manuscript, query, stage, chip, SnapshotOf(manuscript), _session.SearchIndex))
            .ToList();
        BoardRules.Sort(visible, SelectedSortIndex, SnapshotOf);

        // 5. A card for each shown record, kept or new (:2730-2769). The
        // occurrence counts the shown records with that Id before it, in
        // Board order, as CardSlot is called there.
        var perShelf = Shelves.ToDictionary(shelf => shelf.Location, _ => new List<CardViewModel>());
        var seen = new Dictionary<Guid, int>();
        foreach (Manuscript manuscript in visible)
        {
            (Guid Id, int Occurrence) slot = CardSlot(manuscript, seen);
            if (!perShelf.TryGetValue(manuscript.Location, out List<CardViewModel>? cards))
            {
                continue;
            }

            cards.Add(CardFor(manuscript, slot, today, settings));
        }

        // 6. Tabs, empty messages and the cards in order (:2780-2886).
        bool filtersActive = ComputeHasActiveFilters();
        LastSyncChanges = Shelves
            .Select(shelf => shelf.Show(perShelf[shelf.Location], totals[shelf.Location], filtersActive, ResetThreshold))
            .ToList();

        // 7. Clear, the rail and the welcome (:2888-2925).
        HasActiveFilters = filtersActive;
        IsLibraryEmpty = library.Count == 0;
        ManuscriptCount = library.Count;
        DeadlinesDueCount = DeadlineService.CountDueNow(all, today);
        RenderCount++;
    }

    // The attention snapshot this render made for a record.
    internal ManuscriptAttentionSnapshot SnapshotOf(Manuscript manuscript) =>
        _snapshots.TryGetValue(manuscript, out ManuscriptAttentionSnapshot? snapshot)
            ? snapshot
            : new ManuscriptAttentionSnapshot();

    // Form1.vb ToggleAttentionFilter (:809-828), behind the chip's click
    // handler (FilterOverdueRevisions and the four like it, :696-773), which
    // first asks AttentionLabelHasItems (:776-806): a chip whose count is 0
    // ignores the press. That holds for the active chip too, as the code
    // reads (the chip's Tag holds its count, so the check is count > 0).
    [RelayCommand]
    private void ToggleChip(ChipViewModel? chip)
    {
        if (chip is null || chip.Count <= 0)
        {
            return;
        }

        ActiveFilter = ActiveFilter == chip.Filter ? AttentionFilter.None : chip.Filter;
        Render();
    }

    // Form1.vb ClearBoardFilters (:2011-2041): the search emptied, stage 0,
    // sort 0, no chip, with the change rules held off, then one render.
    [RelayCommand(CanExecute = nameof(HasActiveFilters))]
    private void Clear()
    {
        _searchDebouncer.Stop();
        _suppressFilterEvents = true;
        try
        {
            SearchText = string.Empty;
            SelectedStageIndex = 0;
            SelectedSortIndex = 0;
            ActiveFilter = AttentionFilter.None;
        }
        finally
        {
            _suppressFilterEvents = false;
        }

        Render();
    }

    // Form1.vb BoardSearchTextChanged (:1890-1902): each change restarts the
    // wait; the render runs when it ends (BoardSearchDebounceElapsed).
    partial void OnSearchTextChanged(string value)
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        _searchDebouncer.Restart(SearchDelay, SearchDelayElapsed);
    }

    partial void OnSelectedStageIndexChanged(int value) => FilterChanged();

    partial void OnSelectedSortIndexChanged(int value) => FilterChanged();

    // Form1.vb BoardSearchDebounceElapsed (:1905-1918).
    private void SearchDelayElapsed()
    {
        _searchDebouncer.Stop();
        if (_suppressFilterEvents)
        {
            return;
        }

        Render();
    }

    // Form1.vb BoardFilterChanged (:1921-1943) for a closed box: the
    // pending search is dropped and the Board renders at once. The open-list
    // half of the rule (#338) is step 5's.
    private void FilterChanged()
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        _searchDebouncer.Stop();
        Render();
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => Render();

    // Form1.vb HasActiveBoardFilters (:2044-2080).
    private bool ComputeHasActiveFilters() =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedStage.HasValue
        || SelectedSortIndex > 0
        || ActiveFilter != AttentionFilter.None;

    // Form1.vb RefreshStageFilterItems (:1729-1853): "All stages (N)" and the
    // eight stages with their counts over every shelf. The options are
    // replaced only when one differs, and the stage chosen stays chosen.
    private void RefreshStageOptions(IReadOnlyList<Manuscript> all)
    {
        IReadOnlyList<StageOptionViewModel> options = BuildStageOptions(all);
        IReadOnlyList<StageOptionViewModel> shown = StageOptions;
        if (shown.Count == options.Count && options.Select((option, index) => option.SameAs(shown[index])).All(same => same))
        {
            return;
        }

        PaperStage? chosen = SelectedStage;
        bool suppressed = _suppressFilterEvents;
        _suppressFilterEvents = true;
        try
        {
            StageOptions = options;
            int index = 0;
            if (chosen.HasValue)
            {
                for (int candidate = 0; candidate < options.Count; candidate++)
                {
                    if (options[candidate].Stage == chosen)
                    {
                        index = candidate;
                        break;
                    }
                }
            }

            SelectedStageIndex = index;
        }
        finally
        {
            _suppressFilterEvents = suppressed;
        }
    }

    private static IReadOnlyList<StageOptionViewModel> BuildStageOptions(IReadOnlyList<Manuscript> all)
    {
        Dictionary<PaperStage, int> counts = ManuscriptStageSummaryService.CountByStage(all);
        var options = new List<StageOptionViewModel> { new(null, BoardRules.AllStagesName, all.Count) };
        foreach (PaperStage stage in BoardRules.BoxStages)
        {
            options.Add(new StageOptionViewModel(stage, BoardRules.FormatStage(stage), counts.GetValueOrDefault(stage)));
        }

        return options;
    }

    // Form1.vb RefreshAttentionDashboard (:851-988): counts over every
    // manuscript on every shelf; the snapshot decides which shelves count.
    private void RefreshChips(IReadOnlyList<Manuscript> all, AppSettings settings)
    {
        int sum = 0;
        foreach (ChipViewModel chip in Chips)
        {
            int count = all.Count(manuscript => BoardRules.HasFlag(SnapshotOf(manuscript), chip.Filter));
            sum += count;
            chip.Update(count, BoardRules.ChipText(chip.Filter, count, settings), ActiveFilter == chip.Filter);
        }

        NothingNeedsAttention = sum == 0 && ActiveFilter == AttentionFilter.None;
    }

    // Form1.vb CardSlot (:2938-2949).
    private static (Guid Id, int Occurrence) CardSlot(Manuscript manuscript, Dictionary<Guid, int> seen)
    {
        seen.TryGetValue(manuscript.Id, out int occurrence);
        seen[manuscript.Id] = occurrence + 1;
        return (manuscript.Id, occurrence);
    }

    // Form1.vb CardFor (:2954-2992): the card kept in the slot while its key
    // is unchanged, or a new one in its place.
    private CardViewModel CardFor(Manuscript manuscript, (Guid Id, int Occurrence) slot, DateTime today, AppSettings settings)
    {
        CardFacts facts = CardFacts.For(manuscript, SnapshotOf(manuscript), today, settings.FileDrawerSuggestionThreshold);
        if (_cards.TryGetValue(slot, out CardViewModel? kept) && string.Equals(kept.Key, facts.Key, StringComparison.Ordinal))
        {
            kept.Refresh(manuscript, _session.AuthorLibrary);
            return kept;
        }

        var card = new CardViewModel(facts, slot, _session.AuthorLibrary);
        _cards[slot] = card;
        return card;
    }

    // Form1.vb DropStaleCards (:3059-3074): the cards of slots the library no
    // longer has, counted in library order.
    private void DropStaleCards(IReadOnlyList<Manuscript> all)
    {
        var seen = new Dictionary<Guid, int>();
        var current = new HashSet<(Guid Id, int Occurrence)>(all.Select(manuscript => CardSlot(manuscript, seen)));
        foreach ((Guid Id, int Occurrence) slot in _cards.Keys.ToList())
        {
            if (!current.Contains(slot))
            {
                _cards.Remove(slot);
            }
        }
    }
}
