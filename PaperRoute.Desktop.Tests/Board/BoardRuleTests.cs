using System.Reflection;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Board;
using static PaperRoute.Desktop.Tests.Board.Papers;

namespace PaperRoute.Desktop.Tests.Board;

// The Board's rules in PaperRoute.ViewModels, with no window (d1 6.2, the V
// rows; step 4 of d1 section 8). Each test names the Windows Forms rule it
// pins: the file and member of today's code, in ManuscriptPipeline\ unless
// it says otherwise. Line numbers are those of the worktree at cbbabad.
[TestClass]
public sealed class BoardRuleTests : StorageCheckedTests
{
    // V1. Pins Form1.vb RenderManuscripts (:2696-2711 totals, :2743-2752
    // "Select Case manuscript.Location ... Case Else Continue For"): a
    // manuscript is on the shelf its Location names; any other value is on
    // no shelf and in no total.
    [TestMethod]
    public void V1_AManuscriptIsOnTheShelfItsLocationNames()
    {
        var harness = new BoardHarness(
        [
            Paper("on the pipeline"),
            Paper("published", PaperStage.Published, ManuscriptLocation.Published),
            Paper("set aside", PaperStage.Draft, ManuscriptLocation.FileDrawer),
            Paper("nowhere", PaperStage.Draft, (ManuscriptLocation)7),
            Paper("in press, still on the pipeline", PaperStage.InPress),
        ]);

        CollectionAssert.AreEqual(new[] { "on the pipeline", "in press, still on the pipeline" }, BoardHarness.Titles(harness.Pipeline));
        CollectionAssert.AreEqual(new[] { "published" }, BoardHarness.Titles(harness.Published));
        CollectionAssert.AreEqual(new[] { "set aside" }, BoardHarness.Titles(harness.FileDrawer));
        Assert.AreEqual("Pipeline (2)", harness.Pipeline.TabText);
        Assert.AreEqual("Published (1)", harness.Published.TabText);
        Assert.AreEqual("File Drawer (1)", harness.FileDrawer.TabText);
        Assert.AreEqual(5, harness.Board.ManuscriptCount, "The rail counts every record (Form1.Shell.vb UpdateRailCounts).");
    }

    // V2. Pins Form1.vb RefreshAttentionDashboard (:851-945, the counts over
    // every manuscript and the chip texts) and PaperRoute.Core
    // ManuscriptAttentionService.Evaluate (:137-252): a recent rejection
    // counts on any shelf; the other four count only on the Pipeline.
    [TestMethod]
    public void V2_ChipCountsCoverEveryShelfAndOnlyRejectionsLeaveThePipeline()
    {
        var harness = new BoardHarness(
        [
            Revision("overdue", -3),
            Revision("overdue too", -1),
            Revision("due soon", 5),
            Waiting("long wait", 100),
            Paper("no journal", PaperStage.Idea, journal: " "),
            Rejected("rejected", 2),
            Revision("overdue, set aside", -3, ManuscriptLocation.FileDrawer),
            Revision("due soon, set aside", 5, ManuscriptLocation.FileDrawer),
            Waiting("long wait, set aside", 100, ManuscriptLocation.FileDrawer),
            Paper("no journal, set aside", PaperStage.Idea, ManuscriptLocation.FileDrawer, journal: ""),
            Rejected("rejected, set aside", 5, ManuscriptLocation.FileDrawer),
            Rejected("rejected, published", 1, ManuscriptLocation.Published),
        ]);

        Assert.AreEqual(2, harness.Chip(AttentionFilter.OverdueRevision).Count);
        Assert.AreEqual("2 overdue revisions", harness.Chip(AttentionFilter.OverdueRevision).Text);
        Assert.AreEqual("1 revision due soon", harness.Chip(AttentionFilter.RevisionDueSoon).Text);
        Assert.AreEqual("1 waiting 90+ days", harness.Chip(AttentionFilter.LongReview).Text);
        Assert.AreEqual("1 no target journal", harness.Chip(AttentionFilter.MissingTargetJournal).Text);
        Assert.AreEqual("3 rejected in last 30 days", harness.Chip(AttentionFilter.RecentRejection).Text);

        CollectionAssert.AreEqual(
            new[] { ChipTone.Danger, ChipTone.Warning, ChipTone.Warning, ChipTone.Neutral, ChipTone.Danger },
            harness.Board.Chips.Select(chip => chip.Tone).ToArray(),
            "The tones at rest (Form1.vb:948-981).");

        // The thresholds come from the settings, in the texts too.
        harness.Session.Settings = new AppSettings { LongReviewThresholdDays = 60, RecentRejectionThresholdDays = 1 };
        harness.Board.Render();
        Assert.AreEqual("1 waiting 60+ days", harness.Chip(AttentionFilter.LongReview).Text);
        Assert.AreEqual("1 rejected in last 1 day", harness.Chip(AttentionFilter.RecentRejection).Text);
    }

    // V2, the two rejection sorts. Pins Form1.vb GetVisibleManuscripts
    // (:2261-2271) with CompareRejectionsDescending and
    // CompareRejectionsAscending (:2319-2348): the attention snapshot's
    // RejectionCount, most first and fewest first. The count depends on no
    // date (PaperRoute.Core ManuscriptAttentionService.vb:66-97 counts every
    // rejection decision). This test pins the comparison on distinct counts;
    // the order of manuscripts with equal counts (Form1 and the port's
    // BoardRules.Sort both use List.Sort, introsort, unstable above 16 items)
    // is pinned on the 500 library by V8 (BoardSortOrderTests), whose
    // checked-in orders hold sorts 3 and 4.
    [TestMethod]
    public void V2_TheRejectionSortsOrderByTheSnapshotsRejectionCount()
    {
        var harness = new BoardHarness(
        [
            RejectedTimes("one", 1),
            RejectedTimes("three", 3),
            Paper("none"),
            RejectedTimes("two", 2),
        ]);

        harness.Board.SelectedSortIndex = 3;
        CollectionAssert.AreEqual(new[] { "three", "two", "one", "none" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.SelectedSortIndex = 4;
        CollectionAssert.AreEqual(new[] { "none", "one", "two", "three" }, BoardHarness.Titles(harness.Pipeline));

        CollectionAssert.AreEqual(
            new[]
            {
                "Sort: Current order", "Sort: Title A-Z", "Sort: Title Z-A", "Sort: Most rejections",
                "Sort: Fewest rejections", "Sort: Newest stage change", "Sort: Oldest stage change",
            },
            harness.Board.SortOptions.ToArray(),
            "The sort box's items (Form1.vb:546-552).");
    }

    // V8b, the title comparison. Pins Form1.vb CompareTitleAscending and
    // CompareTitleDescending (:2293-2316): StringComparer.CurrentCultureIgnoreCase.
    // V8 cannot see the comparer, since the 500 library's titles are already
    // in title order ("Synthetic NNN:"). An ordinal comparer would put
    // "Banana split" and "Cherry" before "apple"; no sort would keep the
    // library's order.
    [TestMethod]
    public void V8b_TheTitleSortsIgnoreCaseInTheCurrentCulture()
    {
        var harness = new BoardHarness(
        [
            Paper("banana"),
            Paper("Cherry"),
            Paper("apple"),
            Paper("Banana split"),
        ]);

        harness.Board.SelectedSortIndex = 1;
        CollectionAssert.AreEqual(new[] { "apple", "banana", "Banana split", "Cherry" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.SelectedSortIndex = 2;
        CollectionAssert.AreEqual(new[] { "Cherry", "Banana split", "banana", "apple" }, BoardHarness.Titles(harness.Pipeline));
    }

    // V3. Pins Form1.vb StyleAttentionLabel (:991-1025: a chip shows while
    // its count is above zero or it is the active filter) and
    // RefreshAttentionDashboard (:983-986: "Nothing needs attention right
    // now." when all five are zero and no chip filter is on).
    [TestMethod]
    public void V3_AChipShowsWhileItCountsOrIsActive()
    {
        Manuscript overdue = Revision("overdue", -2);
        var harness = new BoardHarness([overdue, Paper("quiet")]);
        ChipViewModel chip = harness.Chip(AttentionFilter.OverdueRevision);

        Assert.IsTrue(chip.IsVisible);
        Assert.IsTrue(harness.Board.Chips.Where(other => other != chip).All(other => !other.IsVisible), "Chips with no count are hidden.");
        Assert.IsFalse(harness.Board.NothingNeedsAttention);
        Assert.AreEqual("Filter the board", chip.HelpText);

        harness.Board.ToggleChipCommand.Execute(chip);
        Assert.IsTrue(chip.IsActive);
        Assert.AreEqual("Filter applied. Press to clear it.", chip.HelpText);

        // The library changes under the active filter: the chip stays, at 0.
        harness.Session.Library.Remove(overdue);
        harness.Session.RaiseChanged();
        Assert.AreEqual(0, chip.Count);
        Assert.AreEqual("0 overdue revisions", chip.Text);
        Assert.IsTrue(chip.IsVisible, "The active chip stays shown at 0.");
        Assert.IsFalse(harness.Board.NothingNeedsAttention, "Not while a chip filter is on.");

        harness.Board.ClearCommand.Execute(null);
        Assert.IsFalse(chip.IsVisible);
        Assert.IsTrue(harness.Board.NothingNeedsAttention);

        var empty = new BoardHarness([]);
        Assert.IsTrue(empty.Board.NothingNeedsAttention);
        Assert.IsTrue(empty.Board.Chips.All(other => !other.IsVisible));
    }

    // V4. Pins Form1.vb ToggleAttentionFilter (:809-828: one chip filter at
    // a time; pressing the active chip clears it) and the click handlers
    // FilterOverdueRevisions ... FilterRecentRejections (:696-773) with
    // AttentionLabelHasItems (:776-806): a chip whose count is 0 ignores the
    // press. The code ignores it for the active chip too (the count is the
    // chip's Tag, and the check is count > 0); r1 and d1 say "unless it is
    // active", and the code wins here.
    [TestMethod]
    public void V4_OneChipAtATimeAndAZeroCountChipIgnoresAPress()
    {
        Manuscript overdue = Revision("overdue", -2);
        var harness = new BoardHarness([overdue, Waiting("long wait", 120)]);
        ChipViewModel overdueChip = harness.Chip(AttentionFilter.OverdueRevision);
        ChipViewModel waitChip = harness.Chip(AttentionFilter.LongReview);
        ChipViewModel journalChip = harness.Chip(AttentionFilter.MissingTargetJournal);

        harness.Board.ToggleChipCommand.Execute(overdueChip);
        Assert.AreEqual(AttentionFilter.OverdueRevision, harness.Board.ActiveFilter);
        CollectionAssert.AreEqual(new[] { "overdue" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.ToggleChipCommand.Execute(waitChip);
        Assert.AreEqual(AttentionFilter.LongReview, harness.Board.ActiveFilter);
        Assert.IsFalse(overdueChip.IsActive);
        Assert.IsTrue(waitChip.IsActive);
        CollectionAssert.AreEqual(new[] { "long wait" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.ToggleChipCommand.Execute(waitChip);
        Assert.AreEqual(AttentionFilter.None, harness.Board.ActiveFilter);
        Assert.HasCount(2, harness.Pipeline.Cards);

        int renders = harness.Board.RenderCount;
        harness.Board.ToggleChipCommand.Execute(journalChip);
        Assert.AreEqual(AttentionFilter.None, harness.Board.ActiveFilter, "A chip with no count ignores the press.");
        Assert.AreEqual(renders, harness.Board.RenderCount, "and does not render.");

        // The active chip at 0 ignores the press as well (today's code).
        harness.Board.ToggleChipCommand.Execute(overdueChip);
        harness.Session.Library.Remove(overdue);
        harness.Session.RaiseChanged();
        Assert.AreEqual(0, overdueChip.Count);
        harness.Board.ToggleChipCommand.Execute(overdueChip);
        Assert.AreEqual(AttentionFilter.OverdueRevision, harness.Board.ActiveFilter, "Today's AttentionLabelHasItems ignores it; Clear clears it.");
    }

    // V5. Pins Form1.vb ManuscriptMatchesBoardFilters (:2124-2150) with
    // ContainsSearchText (:2083-2097) and GetVisibleManuscripts (:2221-2222):
    // the title, the journal, the author text (PaperRoute.Core
    // AuthorLibrarySearchIndex.BuildSearchText), any tag against the query
    // with a leading # stripped, and the work type's name unless it is
    // Unspecified; the query trimmed; current culture, ignoring case.
    [TestMethod]
    public void V5_SearchLooksAtTitleJournalAuthorsTagsAndWorkType()
    {
        var author = new AuthorRecord { GivenName = "Wilhelmina", FamilyName = "Xylophone" };
        var authors = new AuthorLibraryData();
        authors.Authors.Add(author);

        Manuscript byAuthor = Paper("Gamma");
        byAuthor.Authors.Add(new ManuscriptAuthor { AuthorId = author.Id });
        Manuscript tagged = Paper("Delta");
        tagged.Tags.Add("Yak-shaving");
        Manuscript preprint = Paper("Epsilon");
        preprint.WorkType = WorkType.Preprint;
        Manuscript unspecified = Paper("Zeta");
        unspecified.WorkType = WorkType.Unspecified;

        var harness = new BoardHarness(
            [Paper("Alpha study of zebras"), Paper("Beta", journal: "Journal of Quokkas"), byAuthor, tagged, preprint, unspecified],
            authorLibrary: authors);

        (string Query, string[] Titles)[] cases =
        [
            ("ZEBRAS", ["Alpha study of zebras"]),
            ("  quokkas  ", ["Beta"]),
            ("xylophone", ["Gamma"]),
            ("yak", ["Delta"]),
            ("#yak", ["Delta"]),
            ("PREPRINT", ["Epsilon"]),
            ("Not specified", []),
            ("#", ["Delta"]),
            ("   ", ["Alpha study of zebras", "Beta", "Gamma", "Delta", "Epsilon", "Zeta"]),
        ];

        foreach ((string query, string[] titles) in cases)
        {
            harness.Search(query);
            CollectionAssert.AreEqual(titles, BoardHarness.Titles(harness.Pipeline), $"Search \"{query}\".");
        }

        Assert.AreEqual("Not specified", WorkTypeService.DisplayName(WorkType.Unspecified), "The name an Unspecified type would match.");
    }

    // V6. Pins Form1.vb BoardSearchTextChanged and BoardSearchDebounceElapsed
    // (:1890-1918, the 200 ms timer of :79-81), RenderManuscripts (:2668,
    // the timer stopped first) and BoardFilterChanged (:1940, a box change
    // drops the pending search tick).
    [TestMethod]
    public void V6_SearchWaits200MillisecondsAndARenderDropsThePendingTick()
    {
        var harness = new BoardHarness([Paper("apple"), Paper("banana")]);
        int renders = harness.Board.RenderCount;

        harness.Board.SearchText = "app";
        harness.Board.SearchText = "apple";
        Assert.IsTrue(harness.Debouncer.IsPending);
        Assert.AreEqual(2, harness.Debouncer.Restarts, "Each keystroke restarts the wait.");
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), harness.Debouncer.Delay);
        Assert.AreEqual(renders, harness.Board.RenderCount, "Nothing renders while the wait runs.");
        Assert.HasCount(2, harness.Pipeline.Cards);

        Assert.IsTrue(harness.Debouncer.Fire());
        Assert.AreEqual(renders + 1, harness.Board.RenderCount);
        CollectionAssert.AreEqual(new[] { "apple" }, BoardHarness.Titles(harness.Pipeline));

        // A box change renders at once, with the search typed so far, and
        // drops the tick.
        harness.Board.SearchText = "banana";
        harness.Board.SelectedSortIndex = 1;
        Assert.AreEqual(renders + 2, harness.Board.RenderCount);
        Assert.IsFalse(harness.Debouncer.IsPending);
        CollectionAssert.AreEqual(new[] { "banana" }, BoardHarness.Titles(harness.Pipeline));

        // Any render stops a pending tick.
        harness.Board.SearchText = "apple";
        harness.Board.Render();
        Assert.IsFalse(harness.Debouncer.IsPending);
    }

    // V7. Pins Form1.vb ManuscriptMatchesBoardFilters (:2112-2213): the
    // search, the stage and the chip filter must all hold.
    [TestMethod]
    public void V7_SearchStageAndChipMustAllHold()
    {
        var harness = new BoardHarness(
        [
            Paper("apple pie", PaperStage.Draft, journal: ""),
            Paper("apple tart", PaperStage.Draft, journal: "Journal"),
            Paper("apple cake", PaperStage.Idea, journal: ""),
            Paper("banana", PaperStage.Draft, journal: ""),
        ]);

        harness.Search("apple");
        CollectionAssert.AreEqual(new[] { "apple pie", "apple tart", "apple cake" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.SelectedStageIndex = 2;
        Assert.AreEqual(PaperStage.Draft, harness.Board.StageOptions[2].Stage);
        CollectionAssert.AreEqual(new[] { "apple pie", "apple tart" }, BoardHarness.Titles(harness.Pipeline));

        harness.Board.ToggleChipCommand.Execute(harness.Chip(AttentionFilter.MissingTargetJournal));
        CollectionAssert.AreEqual(new[] { "apple pie" }, BoardHarness.Titles(harness.Pipeline));

        harness.Search(string.Empty);
        CollectionAssert.AreEqual(new[] { "apple pie", "banana" }, BoardHarness.Titles(harness.Pipeline));
    }

    // V9. Pins Form1.vb BuildSectionTitle (:2375-2398) and
    // HasActiveBoardFilters (:2044-2080): "Name (total)", or "Name (shown of
    // total)" only while a Board filter is on and the shelf shows fewer than
    // its total; ShelfTabButton.vb OnPaint (:35-41) paints the count apart.
    [TestMethod]
    public void V9_TabsShowShownOfTotalOnlyWhileAFilterHidesSome()
    {
        var harness = new BoardHarness(
        [
            Paper("x1"),
            Paper("x2"),
            Paper("y1"),
            Paper("x3", PaperStage.Published, ManuscriptLocation.Published),
        ]);

        Assert.AreEqual("Pipeline (3)", harness.Pipeline.TabText);
        Assert.AreEqual("3", harness.Pipeline.TabCount);
        Assert.AreEqual("Pipeline", harness.Pipeline.TabName);
        Assert.AreEqual("Published (1)", harness.Published.TabText);
        Assert.AreEqual("File Drawer (0)", harness.FileDrawer.TabText);

        harness.Search("x");
        Assert.AreEqual("Pipeline (2 of 3)", harness.Pipeline.TabText);
        Assert.AreEqual("2 of 3", harness.Pipeline.TabCount);
        Assert.AreEqual(2, harness.Pipeline.Shown);
        Assert.AreEqual(3, harness.Pipeline.Total);
        Assert.AreEqual("Published (1)", harness.Published.TabText, "All shown: the total alone.");
        Assert.AreEqual("File Drawer (0)", harness.FileDrawer.TabText);

        harness.Search("   ");
        Assert.AreEqual("Pipeline (3)", harness.Pipeline.TabText, "A blank search is no filter.");

        harness.Board.SelectedSortIndex = 1;
        Assert.IsTrue(harness.Board.HasActiveFilters);
        Assert.AreEqual("Pipeline (3)", harness.Pipeline.TabText, "A sort hides nothing.");
    }

    // V10. Pins Form1.vb RenderManuscripts, the empty states (:2806-2878).
    [TestMethod]
    public void V10_EachShelfHasItsEmptyAndItsFilteredMessage()
    {
        var empty = new BoardHarness([]);
        Assert.AreEqual("Nothing in progress. Choose + Add Manuscript to start one.", empty.Pipeline.EmptyMessage);
        Assert.AreEqual("Manuscripts move here when their stage is Published.", empty.Published.EmptyMessage);
        Assert.AreEqual("Work you set aside waits here until you restore it.", empty.FileDrawer.EmptyMessage);
        Assert.IsTrue(empty.Board.IsLibraryEmpty, "An empty library shows the welcome (Form1.Welcome.vb:21-34).");

        var harness = new BoardHarness(
        [
            Paper("one"),
            Paper("two", PaperStage.Published, ManuscriptLocation.Published),
            Paper("three", PaperStage.Draft, ManuscriptLocation.FileDrawer),
        ]);
        Assert.IsFalse(harness.Board.IsLibraryEmpty);
        Assert.IsTrue(harness.Board.Shelves.All(shelf => shelf.EmptyMessage is null && !shelf.HasEmptyMessage));

        harness.Search("nothing matches this");
        Assert.AreEqual("No Pipeline manuscripts match the current search or filter.", harness.Pipeline.EmptyMessage);
        Assert.AreEqual("No Published manuscripts match the current search or filter.", harness.Published.EmptyMessage);
        Assert.AreEqual("No File Drawer manuscripts match the current search or filter.", harness.FileDrawer.EmptyMessage);
        Assert.IsTrue(harness.Board.Shelves.All(shelf => shelf.Cards.Count == 0));
    }

    // V11. Pins Form1.vb HasActiveBoardFilters (:2044-2080; Clear is enabled
    // at :2888-2889) and ClearBoardFilters (:2011-2041): any of search,
    // stage, sort or chip enables Clear; Clear resets all four, with the
    // change rules held off, and renders once.
    [TestMethod]
    public void V11_ClearIsEnabledByAnyFilterAndResetsAllFourInOneRender()
    {
        var harness = new BoardHarness([Revision("overdue", -1), Paper("plain")]);
        BoardViewModel board = harness.Board;
        Assert.IsFalse(board.HasActiveFilters);
        Assert.IsFalse(board.ClearCommand.CanExecute(null));

        harness.Search("   ");
        Assert.IsFalse(board.HasActiveFilters, "A blank search is no filter.");

        harness.Search("plain");
        Assert.IsTrue(board.HasActiveFilters);
        Assert.IsTrue(board.ClearCommand.CanExecute(null));
        harness.Search(string.Empty);
        Assert.IsFalse(board.HasActiveFilters);

        board.SelectedStageIndex = 1;
        Assert.IsTrue(board.HasActiveFilters, "A stage.");
        board.SelectedStageIndex = 0;
        Assert.IsFalse(board.HasActiveFilters);

        board.SelectedSortIndex = 2;
        Assert.IsTrue(board.HasActiveFilters, "A sort other than Current order.");
        board.SelectedSortIndex = 0;

        board.ToggleChipCommand.Execute(harness.Chip(AttentionFilter.OverdueRevision));
        Assert.IsTrue(board.HasActiveFilters, "A chip.");

        harness.Search("overdue");
        board.SelectedStageIndex = 5;
        board.SelectedSortIndex = 3;
        board.SearchText = "overdue again";
        Assert.IsTrue(harness.Debouncer.IsPending);
        int renders = board.RenderCount;

        board.ClearCommand.Execute(null);

        Assert.AreEqual(renders + 1, board.RenderCount, "Clear renders once.");
        Assert.AreEqual(string.Empty, board.SearchText);
        Assert.AreEqual(0, board.SelectedStageIndex);
        Assert.AreEqual(0, board.SelectedSortIndex);
        Assert.AreEqual(AttentionFilter.None, board.ActiveFilter);
        Assert.IsFalse(harness.Debouncer.IsPending, "The pending search is dropped, and emptying the box starts no new wait.");
        Assert.IsFalse(board.HasActiveFilters);
        Assert.IsFalse(board.ClearCommand.CanExecute(null));
        Assert.HasCount(2, harness.Pipeline.Cards);
    }

    // V12. Pins Form1.vb BuildManuscriptInsight (:1029-1181) and
    // CreateManuscriptCard (:3141-3147, 3199-3209): the first match among
    // overdue, due, waiting, rejected, then "consider filing" (Pipeline,
    // CanSuggestFiling, rejections at or above the threshold); a missing
    // journal is not a status, and since it comes before the filing check it
    // also keeps "consider filing" off; otherwise PaperRoute.Core
    // StageClockService.Describe in the muted tone.
    [TestMethod]
    public void V12_TheStatusLineIsTheFirstInsightOrElseTheStageClock()
    {
        Manuscript waitingAndRejected = Paper("waiting and rejected", PaperStage.UnderReview)
            .With(Submission(100, (EditorialDecision.Rejected, 2, null)));
        Manuscript restored = RejectedTimes("restored", 3);
        restored.History.Add(new HistoryEvent { EventDate = Today.AddDays(-5), Note = ManuscriptAttentionService.RestoredFromFileDrawerNote });
        Manuscript changedToday = Paper("changed today", stageDaysAgo: 0);
        changedToday.History.AddRange([new HistoryEvent(), new HistoryEvent()]);
        Manuscript changedYesterday = Paper("changed yesterday", stageDaysAgo: 1);
        changedYesterday.History.AddRange([new HistoryEvent(), new HistoryEvent()]);

        var harness = new BoardHarness(
        [
            Revision("overdue 1", -1),
            Revision("overdue 3", -3),
            Revision("due today", 0),
            Revision("due in 1", 1),
            Revision("due in 5", 5),
            Waiting("waiting 120", 120),
            waitingAndRejected,
            Rejected("rejected 1", 1),
            Rejected("rejected 6", 6),
            RejectedTimes("filing", 3),
            RejectedTimes("filing, set aside", 3, ManuscriptLocation.FileDrawer),
            RejectedTimes("filing, no journal", 3, journal: ""),
            restored,
            Paper("idea without a journal", PaperStage.Idea, journal: "", stageDaysAgo: 5),
            Paper("added today", stageDaysAgo: 0),
            changedToday,
            changedYesterday,
            Paper("published", PaperStage.Published, ManuscriptLocation.Published),
        ]);

        (string Title, string Status, CardTone Tone, bool Urgent)[] expected =
        [
            ("overdue 1", "Revision overdue by 1 day", CardTone.Danger, true),
            ("overdue 3", "Revision overdue by 3 days", CardTone.Danger, true),
            ("due today", "Revision due today", CardTone.Warning, true),
            ("due in 1", "Revision due in 1 day", CardTone.Warning, true),
            ("due in 5", "Revision due in 5 days", CardTone.Warning, true),
            ("waiting 120", "Waiting 120 days", CardTone.Warning, true),
            ("waiting and rejected", "Waiting 100 days", CardTone.Warning, true),
            ("rejected 1", "Rejected 1 day ago", CardTone.Danger, true),
            ("rejected 6", "Rejected 6 days ago", CardTone.Danger, true),
            ("filing", "3 rejections; consider filing", CardTone.Warning, true),
            ("filing, set aside", "", CardTone.Muted, false),
            ("filing, no journal", "added 10 days ago", CardTone.Muted, false),
            ("restored", "added 10 days ago", CardTone.Muted, false),
            ("idea without a journal", "added 5 days ago", CardTone.Muted, false),
            ("added today", "added today", CardTone.Muted, false),
            ("changed today", "since today", CardTone.Muted, false),
            ("changed yesterday", "1 day", CardTone.Muted, false),
            ("published", "", CardTone.Muted, false),
        ];

        foreach ((string title, string status, CardTone tone, bool urgent) in expected)
        {
            CardViewModel card = harness.Card(title);
            Assert.AreEqual(status, card.Status, title);
            Assert.AreEqual(tone, card.StatusTone, title);
            Assert.AreEqual(urgent, card.IsStatusUrgent, title);
            Assert.AreEqual(status.Length > 0, card.HasStatus, title);
        }

        // The singular texts that need other settings.
        var single = new BoardHarness(
            [Waiting("waiting 1", 1), RejectedTimes("filing once", 1)],
            new AppSettings { LongReviewThresholdDays = 1, FileDrawerSuggestionThreshold = 1 });
        Assert.AreEqual("Waiting 1 day", single.Card("waiting 1").Status);
        Assert.AreEqual("1 rejection; consider filing", single.Card("filing once").Status);
        Assert.AreEqual(CardTone.Warning, single.Card("filing once").StatusTone);
    }

    // V13. Pins Form1.vb CreateManuscriptCard (:3257-3268): the journal in
    // the secondary tone; with none, "No target journal yet" in the warning
    // tone on the Pipeline and "No target journal" muted elsewhere; and the
    // title's description (:3253).
    [TestMethod]
    public void V13_TheJournalLineNamesTheJournalOrSaysThereIsNone()
    {
        var harness = new BoardHarness(
        [
            Paper("with a journal", journal: "Journal of Examples"),
            Paper("pipeline, none", journal: "   "),
            Paper("published, none", PaperStage.Published, ManuscriptLocation.Published, journal: ""),
            Paper("drawer, none", PaperStage.Draft, ManuscriptLocation.FileDrawer, journal: ""),
        ]);

        CardViewModel withJournal = harness.Card("with a journal");
        Assert.AreEqual("Journal of Examples", withJournal.JournalText);
        Assert.AreEqual(CardTone.Secondary, withJournal.JournalTone);
        Assert.IsTrue(withJournal.HasJournal);
        Assert.AreEqual("Draft, added 10 days ago, Journal of Examples, Not yet submitted", withJournal.AccessibleDescription);

        CardViewModel pipeline = harness.Card("pipeline, none");
        Assert.AreEqual("No target journal yet", pipeline.JournalText);
        Assert.AreEqual(CardTone.Warning, pipeline.JournalTone);
        Assert.IsFalse(pipeline.HasJournal);
        Assert.AreEqual("Draft, added 10 days ago, no target journal, Not yet submitted", pipeline.AccessibleDescription);

        CardViewModel published = harness.Card("published, none");
        Assert.AreEqual("No target journal", published.JournalText);
        Assert.AreEqual(CardTone.Muted, published.JournalTone);
        Assert.AreEqual("Published, no target journal, No submissions recorded", published.AccessibleDescription, "An empty status is left out.");

        CardViewModel drawer = harness.Card("drawer, none");
        Assert.AreEqual("No target journal", drawer.JournalText);
        Assert.AreEqual(CardTone.Muted, drawer.JournalTone);

        Assert.AreEqual("DRAFT", withJournal.StageText);
        Assert.AreEqual("More actions for with a journal", withJournal.MoreActionsName);
        Assert.AreEqual("View route for with a journal", withJournal.ViewRouteName);
    }

    // V14. Pins Form1.vb CardKey (:3034-3055) and CardFor (:2954-2992): a
    // card is kept while its key is unchanged (title, stage, location,
    // journal, tags, status text, insight colour, route text, route dots),
    // and stands for the record now in its slot; a change in any of them
    // builds a new card.
    [TestMethod]
    public void V14_ACardIsKeptWhileItsKeyIsUnchanged()
    {
        (string Input, Action<Manuscript> Change)[] keyed =
        [
            ("title", item => item.Title = "another title"),
            ("stage", item => item.CurrentStage = PaperStage.Submitted),
            ("location", item => item.Location = ManuscriptLocation.FileDrawer),
            ("journal", item => item.TargetJournal = "Another Journal"),
            ("tags", item => item.Tags.Add("second")),
            ("status (stage clock)", item => item.StageEnteredDate = Today.AddDays(-3)),
            ("status (insight)", item => item.Submissions[0].Decisions.Add(new EditorialDecisionEvent { Decision = EditorialDecision.Rejected, DecisionDate = Today.AddDays(-1) })),
            ("route", item => item.Submissions.Add(Submission(5))),
        ];

        foreach ((string input, Action<Manuscript> change) in keyed)
        {
            Manuscript manuscript = Subject();
            var harness = new BoardHarness([manuscript]);
            CardViewModel before = harness.Board.Shelves.SelectMany(shelf => shelf.Cards).Single();

            change(manuscript);
            harness.Board.Render();

            CardViewModel after = harness.Board.Shelves.SelectMany(shelf => shelf.Cards).Single();
            Assert.AreNotSame(before, after, $"A change of the {input} builds a new card.");
            Assert.AreNotEqual(before.Key, after.Key, input);
        }

        // Outside the key: the same card.
        Manuscript subject = Subject();
        var kept = new BoardHarness([subject]);
        CardViewModel card = kept.Pipeline.Cards.Single();
        subject.CoAuthors = "Someone Else";
        subject.ManuscriptUrl = "https://example.org/elsewhere";
        subject.Reminders.Add(new ManuscriptReminder { Title = "A reminder", DueDate = Today.AddDays(30) });
        kept.Board.Render();
        Assert.AreSame(card, kept.Pipeline.Cards.Single(), "A change outside the key keeps the card.");

        // An equal record put in its place (same Id, same texts): the card is
        // kept and stands for the new record.
        Manuscript copy = Subject();
        copy.Id = subject.Id;
        copy.Submissions[0].SubmittedDate = subject.Submissions[0].SubmittedDate;
        kept.Session.Library[0] = copy;
        kept.Session.RaiseChanged();
        Assert.AreSame(card, kept.Pipeline.Cards.Single());
        Assert.AreSame(copy, card.Manuscript);

        static Manuscript Subject()
        {
            Manuscript manuscript = Paper("subject", PaperStage.UnderReview).With(Submission(20));
            manuscript.Tags.Add("first");
            return manuscript;
        }
    }

    // V15. Pins Form1.vb CardSlot (:2938-2949), whose rule is tested today by
    // PaperRoute.Tests ManuscriptPageTests.vb (:2188, :2221, :2256): two
    // records that share an Id each keep a card of their own.
    [TestMethod]
    public void V15_TwoRecordsWithOneIdKeepACardEach()
    {
        Manuscript first = Revision("first copy", -2);
        Manuscript second = Paper("second copy");
        second.Id = first.Id;
        var harness = new BoardHarness([first, second]);

        Assert.HasCount(2, harness.Pipeline.Cards);
        CardViewModel one = harness.Card("first copy");
        CardViewModel two = harness.Card("second copy");
        Assert.AreNotSame(one, two);
        Assert.AreEqual(first.Id, one.Id);
        Assert.AreEqual(first.Id, two.Id);
        Assert.AreEqual(0, one.Occurrence);
        Assert.AreEqual(1, two.Occurrence);
        Assert.AreSame(first, one.Manuscript);
        Assert.AreSame(second, two.Manuscript);

        // Each shows its own record's status: the snapshots are kept by
        // record, not by Id (d1 3.2 item 1).
        Assert.AreEqual("Revision overdue by 2 days", one.Status);
        Assert.AreEqual("added 10 days ago", two.Status);

        harness.Board.Render();
        Assert.AreSame(one, harness.Card("first copy"), "Both are kept from render to render.");
        Assert.AreSame(two, harness.Card("second copy"));
    }

    // V16. Pins Form1.vb RefreshStageFilterItems (:1729-1853), with
    // StageFilterOption.ToString and PaperRoute.Core
    // ManuscriptStageSummaryService.CountByStage: "All stages (N)" then the
    // eight stages "Name (count)" over every shelf; the options are rebuilt
    // only when they change, and the stage chosen stays chosen.
    [TestMethod]
    public void V16_TheStageBoxCountsEveryShelfAndKeepsItsChoice()
    {
        var harness = new BoardHarness(
        [
            Paper("idea 1", PaperStage.Idea),
            Paper("idea 2", PaperStage.Idea),
            Paper("draft", PaperStage.Draft),
            Paper("published", PaperStage.Published, ManuscriptLocation.Published),
            Paper("draft, set aside", PaperStage.Draft, ManuscriptLocation.FileDrawer),
        ]);

        CollectionAssert.AreEqual(
            new[]
            {
                "All stages (5)", "Idea (2)", "Draft (2)", "Submitted (0)", "Under Review (0)",
                "Revision (0)", "Accepted (0)", "In Press (0)", "Published (1)",
            },
            harness.Board.StageOptions.Select(option => option.Text).ToArray());
        Assert.AreEqual("Draft (2)", harness.Board.StageOptions[2].ToString(), "ToString is the text.");
        Assert.IsNull(harness.Board.StageOptions[0].Stage);

        harness.Board.SelectedStageIndex = 2;
        CollectionAssert.AreEqual(new[] { "draft" }, BoardHarness.Titles(harness.Pipeline));
        CollectionAssert.AreEqual(new[] { "draft, set aside" }, BoardHarness.Titles(harness.FileDrawer));

        IReadOnlyList<StageOptionViewModel> options = harness.Board.StageOptions;
        harness.Board.Render();
        Assert.AreSame(options, harness.Board.StageOptions, "Unchanged options are not rebuilt.");

        harness.Session.Library.Add(Paper("another draft", PaperStage.Draft));
        harness.Session.RaiseChanged();
        Assert.AreNotSame(options, harness.Board.StageOptions, "Changed counts rebuild the options.");
        Assert.AreEqual("Draft (3)", harness.Board.StageOptions[2].Text);
        Assert.AreEqual("All stages (6)", harness.Board.StageOptions[0].Text);
        Assert.AreEqual(2, harness.Board.SelectedStageIndex, "The stage chosen stays chosen.");
        CollectionAssert.AreEqual(new[] { "draft", "another draft" }, BoardHarness.Titles(harness.Pipeline));
    }

    // V18. Pins what r1 1.7 worked out from today's code for the example
    // library (PaperRoute.Core ExampleLibraryService.Create, the Public,
    // in-memory form; never StartSession or Seed in this process), with
    // default settings: Form1.vb RefreshAttentionDashboard's chips,
    // BuildManuscriptInsight's status lines, and CreateManuscriptCard's
    // journal lines.
    [TestMethod]
    public void V18_TheExampleLibraryShowsWhatTodaysBoardWorksOut()
    {
        (List<Manuscript> manuscripts, AuthorLibraryData library) = ExampleLibraryService.Create(Today);
        var harness = new BoardHarness(manuscripts, authorLibrary: library);

        CollectionAssert.AreEqual(
            new[] { "1 revision due soon", "1 waiting 90+ days", "1 no target journal", "1 rejected in last 30 days" },
            harness.Board.Chips.Where(chip => chip.IsVisible).Select(chip => chip.Text).ToArray());
        Assert.IsFalse(harness.Chip(AttentionFilter.OverdueRevision).IsVisible, "No overdue chip.");
        Assert.IsFalse(harness.Board.NothingNeedsAttention);

        Assert.AreEqual("Pipeline (5)", harness.Pipeline.TabText);
        Assert.AreEqual("Published (1)", harness.Published.TabText);
        Assert.AreEqual("File Drawer (1)", harness.FileDrawer.TabText);

        CollectionAssert.AreEquivalent(
            new[] { "Revision due in 10 days", "Waiting 104 days", "Rejected 6 days ago" },
            harness.Board.Shelves.SelectMany(shelf => shelf.Cards).Where(card => card.IsStatusUrgent).Select(card => card.Status).ToArray());
        Assert.AreEqual("Revision due in 10 days", harness.Card("Example: retrieval practice in an introductory statistics course").Status);
        Assert.AreEqual("Waiting 104 days", harness.Card("Example: sleep and memory consolidation in older adults").Status);
        Assert.AreEqual("Rejected 6 days ago", harness.Card("Example: measurement invariance of a short grit scale across four countries").Status);

        CardViewModel idea = harness.Card("Example: pilot notes on reading fluency");
        Assert.AreEqual("No target journal yet", idea.JournalText);
        Assert.AreEqual(CardTone.Warning, idea.JournalTone);

        CardViewModel filed = harness.FileDrawer.Cards.Single();
        Assert.AreEqual("Example: a null result on priming and choice", filed.Title);
        Assert.AreEqual("No target journal", filed.JournalText);
        Assert.AreEqual(CardTone.Muted, filed.JournalTone);
        Assert.IsFalse(filed.HasStatus, "Its latest rejection was 190 days ago and it is not on the Pipeline: no status line.");
        Assert.AreEqual("3 journals · 3 rejections", filed.RouteText);

        Assert.AreEqual(3, harness.Board.Shelves.SelectMany(shelf => shelf.Cards).Count(card => card.Tags.Count > 0), "Three cards carry tags.");
        Assert.AreEqual("Tags: replication", harness.Published.Cards.Single().TagsName);
    }

    // V20. Pins the null handling of today's card and filter (Form1.vb
    // ManuscriptMatchesBoardFilters :2143 "If(manuscript.Tags, New List(Of
    // String)())", CardKey :3049, CreateManuscriptCard :3148-3274) against
    // d1 3.5: the core's types carry no null information, so a manuscript
    // whose every string and list is null must not break a render, a search
    // or a sort.
    [TestMethod]
    public void V20_NullStringsAndListsFromTheCoreDoNotBreakACard()
    {
        var hollow = new Manuscript();
        foreach (PropertyInfo property in typeof(Manuscript).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            bool nullable = property.PropertyType == typeof(string)
                || (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>));
            if (nullable && property.CanWrite)
            {
                property.SetValue(hollow, null);
            }
        }

        Assert.IsNull(hollow.Title);
        Assert.IsNull(hollow.Tags);
        Assert.IsNull(hollow.Submissions);

        var withNullTag = Paper("null tag");
        withNullTag.Tags = [null!, "kept"];

        var harness = new BoardHarness([hollow, withNullTag, Paper("plain")]);
        CardViewModel card = harness.Pipeline.Cards.First();
        Assert.AreEqual(string.Empty, card.Title);
        Assert.AreEqual("No target journal yet", card.JournalText);
        Assert.IsEmpty(card.Tags);
        Assert.IsNull(card.FirstTag);
        Assert.AreEqual("Not yet submitted", card.RouteText);
        Assert.IsFalse(card.HasRouteDots);
        Assert.AreEqual("More actions for ", card.MoreActionsName);

        CardViewModel tagged = harness.Card("null tag");
        CollectionAssert.AreEqual(new[] { string.Empty, "kept" }, tagged.Tags.Select(tag => tag.Text).ToArray());

        harness.Search("kept");
        CollectionAssert.AreEqual(new[] { "null tag" }, BoardHarness.Titles(harness.Pipeline));
        harness.Search("x");
        harness.Search(string.Empty);
        for (int sort = 0; sort < harness.Board.SortOptions.Count; sort++)
        {
            harness.Board.SelectedSortIndex = sort;
            Assert.HasCount(3, harness.Pipeline.Cards, harness.Board.SortOptions[sort]);
        }

        harness.Board.SelectedStageIndex = 1;
        harness.Board.ToggleChipCommand.Execute(harness.Chip(AttentionFilter.MissingTargetJournal));
        Assert.HasCount(1, harness.Pipeline.Cards, "The hollow record is an Idea with no journal.");
    }

    // V21. Pins PaperRoute.Core RouteSummaryService.Describe (:36, :41-102,
    // at most six dots, oldest first) as the card takes it (Form1.vb
    // CreateManuscriptCard :3303-3325) and Controls\RouteDots.vb (:72-88,
    // the three states): Closed for a rejection, a withdrawal or an earlier
    // submission, Current for the open latest one, Accepted for an accepted
    // latest one; none when nothing was submitted.
    [TestMethod]
    public void V21_RouteDotsAreAtMostSixOldestFirstInThreeStates()
    {
        Manuscript eight = Paper("eight journals", PaperStage.UnderReview);
        for (int index = 0; index < 7; index++)
        {
            int submitted = 800 - (index * 100);
            eight.Submissions.Add(Submission(submitted, (EditorialDecision.Rejected, submitted - 30, null)));
        }

        eight.Submissions.Add(Submission(20));

        Manuscript accepted = Paper("accepted", PaperStage.Accepted);
        accepted.Submissions.Add(Submission(100, (EditorialDecision.Accepted, 10, null)));
        accepted.Submissions.Add(Submission(300, (EditorialDecision.DeskRejected, 290, null)));

        Manuscript withdrawn = Paper("withdrawn").With(Submission(90, (EditorialDecision.Withdrawn, 60, null)));

        var harness = new BoardHarness([eight, accepted, withdrawn, Paper("never submitted")]);

        CardViewModel longCard = harness.Card("eight journals");
        CollectionAssert.AreEqual(
            new[] { RouteDotState.Closed, RouteDotState.Closed, RouteDotState.Closed, RouteDotState.Closed, RouteDotState.Closed, RouteDotState.Current },
            longCard.RouteDots.ToArray());
        Assert.AreEqual(RouteSummaryService.MaximumDots, longCard.RouteDots.Count);
        Assert.AreEqual("8th journal · 7 rejections", longCard.RouteText);

        CardViewModel acceptedCard = harness.Card("accepted");
        CollectionAssert.AreEqual(new[] { RouteDotState.Closed, RouteDotState.Accepted }, acceptedCard.RouteDots.ToArray(), "Oldest first, whatever the list's order.");

        CardViewModel withdrawnCard = harness.Card("withdrawn");
        CollectionAssert.AreEqual(new[] { RouteDotState.Closed }, withdrawnCard.RouteDots.ToArray());
        Assert.AreEqual("1 journal · withdrawn", withdrawnCard.RouteText);

        CardViewModel never = harness.Card("never submitted");
        Assert.IsEmpty(never.RouteDots);
        Assert.IsFalse(never.HasRouteDots);
        Assert.AreEqual("Not yet submitted", never.RouteText);
    }
}
