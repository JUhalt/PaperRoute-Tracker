using PaperRoute.ViewModels.Board;

namespace PaperRoute.ViewModels.Session;

// The Board part of "--self-check" (d1 6.2 S2, extended at step 5 of d1
// section 8): the example's Board rendered with no window, then each filter
// the Board has (search, the stage and sort boxes with their open-list rule
// of #338, a chip, Clear), each checked for what it shows and how often it
// renders. It runs before the storage checks, so they also cover every
// render made here. The search wait ends at once and posted work runs at
// known points, as there is no interface thread in this mode.
internal sealed class BoardSelfCheck
{
    private readonly ILibrarySession _session;
    private readonly QueuedUiDispatcher _posts = new();
    private readonly TextWriter _output;

    public BoardSelfCheck(ILibrarySession session, TextWriter output)
    {
        _session = session;
        _output = output;
        Board = new BoardViewModel(session, new ImmediateDebouncer(), _posts);
    }

    // Built before the session loads, so the load's LibraryChanged renders it.
    public BoardViewModel Board { get; }

    private IEnumerable<CardViewModel> Shown => Board.Shelves.SelectMany(shelf => shelf.Cards);

    private int ShownCount => Board.Shelves.Sum(shelf => shelf.Cards.Count);

    // Runs every check and reports one line each. True when all passed.
    public bool Run()
    {
        IReadOnlyList<Manuscript> library = _session.Manuscripts;
        bool passed = true;

        // The first render, made by the load.
        bool totals = Board.Shelves.All(shelf =>
            shelf.Total == library.Count(manuscript => manuscript.Location == shelf.Location) &&
            shelf.Cards.Count == shelf.Total);
        passed &= Report(Board.RenderCount == 1 && totals && !Board.HasActiveFilters && library.Count > 0,
            "board render",
            $"{Board.RenderCount} render(s); " + string.Join(", ", Board.Shelves.Select(shelf => shelf.TabText)));

        passed &= CheckSearch(library);
        passed &= CheckStageList();
        passed &= CheckSortList();
        passed &= CheckChip();
        passed &= CheckClear(library.Count);
        return passed;
    }

    // The search, with its wait ended at once: one render, the manuscript
    // whose title was typed is shown, and the empty search shows all again.
    private bool CheckSearch(IReadOnlyList<Manuscript> library)
    {
        string title = library.Select(manuscript => manuscript.Title ?? string.Empty).FirstOrDefault(text => text.Length > 0) ?? string.Empty;
        int renders = Board.RenderCount;
        Board.SearchText = title;
        bool found = title.Length > 0 && Shown.Any(card => card.Title == title);
        bool once = Board.RenderCount == renders + 1;
        bool active = Board.HasActiveFilters;
        int shown = ShownCount;

        Board.SearchText = string.Empty;
        bool back = ShownCount == library.Count && Board.RenderCount == renders + 2;
        return Report(found && once && active && back, "board search",
            $"\"{title}\" showed {shown} card(s) in one render; the empty search shows {ShownCount}");
    }

    // #338 on the stage box: three steps in the open list render nothing; the
    // close posts one flush, which renders once with the last step; a list
    // opened and closed with no change renders nothing; a step on the closed
    // box renders at once.
    private bool CheckStageList()
    {
        int target = LastStageWithManuscripts();
        int renders = Board.RenderCount;
        Board.IsStageListOpen = true;
        foreach (int index in new[] { 1, 2, target })
        {
            Board.SelectedStageIndex = index;
        }

        bool quietWhileOpen = Board.RenderCount == renders;
        Board.IsStageListOpen = false;
        bool quietAtClose = Board.RenderCount == renders && _posts.Count == 1;
        _posts.RunAll();
        StageOptionViewModel chosen = Board.StageOptions[target];
        bool flushed = Board.RenderCount == renders + 1 &&
            Shown.All(card => card.Stage == chosen.Stage) &&
            ShownCount == chosen.Count;
        bool passed = Report(target > 0 && quietWhileOpen && quietAtClose && flushed, "board stage list",
            $"stepped to \"{chosen.Text}\" in the open list: {Board.RenderCount - renders} render(s) after the close, {ShownCount} card(s)");

        renders = Board.RenderCount;
        Board.IsStageListOpen = true;
        Board.IsStageListOpen = false;
        _posts.RunAll();
        passed &= Report(Board.RenderCount == renders, "board stage list unchanged",
            $"opened and closed with no change: {Board.RenderCount - renders} render(s)");

        renders = Board.RenderCount;
        Board.SelectedStageIndex = 0;
        bool atOnce = Board.RenderCount == renders + 1 && _posts.Count == 0;
        passed &= Report(atOnce && ShownCount == _session.Manuscripts.Count, "board stage box closed",
            $"All stages on the closed box: {Board.RenderCount - renders} render(s) at once, {ShownCount} card(s)");
        return passed;
    }

    // The sort box shares the rule: Title A-Z then Title Z-A in the open list,
    // one render at the close; Title A-Z on the closed box at once.
    private bool CheckSortList()
    {
        int renders = Board.RenderCount;
        Board.IsSortListOpen = true;
        Board.SelectedSortIndex = 1;
        Board.SelectedSortIndex = 2;
        bool quiet = Board.RenderCount == renders;
        Board.IsSortListOpen = false;
        _posts.RunAll();
        bool passed = Report(quiet && Board.RenderCount == renders + 1 && Sorted(descending: true), "board sort list",
            $"\"{Board.SortOptions[2]}\" in the open list: {Board.RenderCount - renders} render(s) after the close");

        renders = Board.RenderCount;
        Board.SelectedSortIndex = 1;
        passed &= Report(Board.RenderCount == renders + 1 && _posts.Count == 0 && Sorted(descending: false), "board sort box closed",
            $"\"{Board.SortOptions[1]}\" on the closed box: {Board.RenderCount - renders} render(s) at once");
        return passed;
    }

    // The first chip with a count filters every shelf to what it counts.
    private bool CheckChip()
    {
        ChipViewModel? chip = Board.Chips.FirstOrDefault(item => item.Count > 0);
        if (chip is null)
        {
            return Report(false, "board chip", "no chip has a count in the example");
        }

        int renders = Board.RenderCount;
        Board.ToggleChipCommand.Execute(chip);
        return Report(Board.RenderCount == renders + 1 && chip.IsActive && ShownCount == chip.Count, "board chip",
            $"\"{chip.Text}\": {ShownCount} card(s) in {Board.RenderCount - renders} render(s)");
    }

    // Clear resets the search, both boxes and the chip in one render.
    private bool CheckClear(int libraryCount)
    {
        Board.SearchText = "a";
        int renders = Board.RenderCount;
        bool enabled = Board.ClearCommand.CanExecute(null);
        Board.ClearCommand.Execute(null);
        bool reset = Board.SearchText.Length == 0 && Board.SelectedStageIndex == 0 && Board.SelectedSortIndex == 0 &&
            Board.ActiveFilter == AttentionFilter.None && !Board.HasActiveFilters;
        return Report(enabled && reset && Board.RenderCount == renders + 1 && ShownCount == libraryCount, "board clear",
            $"{Board.RenderCount - renders} render(s), {ShownCount} card(s)");
    }

    private int LastStageWithManuscripts()
    {
        for (int index = Board.StageOptions.Count - 1; index > 0; index--)
        {
            if (Board.StageOptions[index].Count > 0)
            {
                return index;
            }
        }

        return 0;
    }

    private bool Sorted(bool descending)
    {
        foreach (ShelfViewModel shelf in Board.Shelves)
        {
            for (int index = 1; index < shelf.Cards.Count; index++)
            {
                int order = StringComparer.CurrentCultureIgnoreCase.Compare(shelf.Cards[index - 1].Title, shelf.Cards[index].Title);
                if (descending ? order < 0 : order > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool Report(bool passed, string check, string detail)
    {
        _output.WriteLine($"{(passed ? "PASS" : "FAIL")} {check}: {detail}");
        return passed;
    }
}
