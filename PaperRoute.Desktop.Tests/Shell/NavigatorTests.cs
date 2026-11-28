using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.ViewModels.Shell;

namespace PaperRoute.Desktop.Tests.Shell;

// The navigator with fake pages (shell design 5.1, B4): history as today's
// two stacks (Form1.Shell.vb:480-530, 777-821), the leave guard, kept pages
// and the manuscript page's origin. No window: the navigator is a pure class.
[TestClass]
public sealed class NavigatorTests
{
    private static readonly PageRequest s_board = new(PageKey.Board);
    private static readonly PageRequest s_library = new(PageKey.Library);
    private static readonly PageRequest s_journals = new(PageKey.Journals);
    private static readonly PageRequest s_deadlines = new(PageKey.Deadlines);
    private static readonly PageRequest s_settings = new(PageKey.Settings);

    [TestMethod]
    public async Task GoingRecordsThePageLeftAndClearsForward()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;

        Assert.IsTrue(await navigator.GoAsync(s_board));
        HistoryIs(navigator.BackHistory, "The first page has no page left to record.");
        Assert.IsTrue(await navigator.GoAsync(s_library));
        HistoryIs(navigator.BackHistory, "Going records the page left.", s_board);

        Assert.IsTrue(await navigator.GoBackAsync());
        HistoryIs(navigator.ForwardHistory, "Back puts the page left on Forward.", s_library);
        Assert.IsTrue(await navigator.GoAsync(s_journals));

        Assert.AreEqual(s_journals, navigator.Current);
        Assert.AreSame(pages.Page(PageKey.Journals), navigator.CurrentPage);
        HistoryIs(navigator.BackHistory, "Going records the page left.", s_board);
        HistoryIs(navigator.ForwardHistory, "A recorded move clears Forward.");
    }

    [TestMethod]
    public async Task GoingToThePageShownRecordsNothing()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);

        Assert.IsTrue(await navigator.GoAsync(new PageRequest(PageKey.Library)), "The page shown is where the request goes.");

        HistoryIs(navigator.BackHistory, "Nothing is recorded for the page shown.", s_board);
        FakePage library = pages.Page(PageKey.Library);
        Assert.HasCount(1, library.ShownWith, "The page shown is not shown again.");
        Assert.AreEqual(0, library.LeaveQuestions, "The page shown is not asked whether it may be left.");
        Assert.AreEqual(0, library.HiddenCount);
    }

    [TestMethod]
    public async Task ASectionOnTheSameManuscriptIsNotANewEntry()
    {
        Manuscript paper = Papers.Paper("A");
        var pages = new TestPages(paper);
        Navigator navigator = pages.Navigator;
        var opened = new PageRequest(PageKey.Manuscript, paper.Id);
        var route = new PageRequest(PageKey.Manuscript, paper.Id, "Route");
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(opened);

        Assert.IsTrue(await navigator.GoAsync(route));

        Assert.AreEqual(route, navigator.Current);
        HistoryIs(navigator.BackHistory, "Another section of the manuscript shown is no new entry.", s_board);
        FakePage page = pages.Page(PageKey.Manuscript);
        CollectionAssert.AreEqual(new[] { opened, route }, page.ShownWith, "The page is told the new section.");
        Assert.AreEqual(0, page.LeaveQuestions, "A section is not a leave.");
        Assert.AreEqual(0, page.HiddenCount, "A section is not a leave.");
    }

    [TestMethod]
    public async Task BackAndForwardWalkTheStacks()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(s_journals);

        Assert.IsTrue(await navigator.GoBackAsync());
        Assert.AreEqual(s_library, navigator.Current);
        HistoryIs(navigator.BackHistory, "Back takes the newest entry.", s_board);
        HistoryIs(navigator.ForwardHistory, "Back puts the page left on Forward.", s_journals);

        Assert.IsTrue(await navigator.GoBackAsync());
        Assert.AreEqual(s_board, navigator.Current);
        HistoryIs(navigator.BackHistory, "Back takes the newest entry.");
        HistoryIs(navigator.ForwardHistory, "Forward holds the pages left, newest first.", s_library, s_journals);

        Assert.IsTrue(await navigator.GoForwardAsync());
        Assert.IsTrue(await navigator.GoForwardAsync());
        Assert.AreEqual(s_journals, navigator.Current);
        Assert.AreSame(pages.Page(PageKey.Journals), navigator.CurrentPage);
        HistoryIs(navigator.BackHistory, "Forward puts the page left on Back.", s_library, s_board);
        HistoryIs(navigator.ForwardHistory, "Forward takes the newest entry.");
    }

    [TestMethod]
    public async Task BackWithNoHistoryDoesNothing()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        Assert.IsFalse(await navigator.GoBackAsync(), "Before any page, Back has nowhere to go.");
        await navigator.GoAsync(s_board);

        Assert.IsNull(navigator.BackTarget);
        Assert.IsFalse(await navigator.GoBackAsync());
        Assert.IsFalse(await navigator.GoForwardAsync());

        Assert.AreEqual(s_board, navigator.Current);
        FakePage board = pages.Page(PageKey.Board);
        Assert.AreEqual(0, board.LeaveQuestions, "With nowhere to go, the page is not asked.");
        Assert.AreEqual(0, board.HiddenCount);
        Assert.HasCount(1, board.ShownWith);
        HistoryIs(navigator.BackHistory, "Nothing is recorded.");
        HistoryIs(navigator.ForwardHistory, "Nothing is recorded.");
    }

    [TestMethod]
    public async Task BackSkipsADeletedManuscript()
    {
        Manuscript gone = Papers.Paper("Deleted");
        Manuscript kept = Papers.Paper("Kept");
        var pages = new TestPages(gone, kept);
        Navigator navigator = pages.Navigator;
        var keptRequest = new PageRequest(PageKey.Manuscript, kept.Id);
        var goneRequest = new PageRequest(PageKey.Manuscript, gone.Id);
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(keptRequest);
        await navigator.GoAsync(goneRequest);
        await navigator.GoAsync(s_library);
        pages.Session.Library.Remove(gone);

        Assert.AreEqual(keptRequest, navigator.BackTarget, "Back names the entry it will reach, not the deleted one.");
        Assert.IsTrue(await navigator.GoBackAsync());

        Assert.AreEqual(keptRequest, navigator.Current);
        HistoryIs(navigator.BackHistory, "The deleted manuscript's entry goes with the one taken.", s_board);
        HistoryIs(navigator.ForwardHistory, "Back puts the page left on Forward.", s_library);
    }

    [TestMethod]
    public async Task ARefusingPageIsNotLeftAndHistoryIsUnchanged()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoBackAsync();
        FakePage board = pages.Page(PageKey.Board);
        board.AllowsLeave = false;
        int asked = board.LeaveQuestions;
        int hidden = board.HiddenCount;
        int shown = board.ShownWith.Count;

        Assert.IsFalse(await navigator.GoAsync(s_journals), "A refused leave is no move.");

        Assert.AreEqual(asked + 1, board.LeaveQuestions, "The page shown is asked once.");
        Assert.AreEqual(s_board, navigator.Current);
        Assert.AreSame(board, navigator.CurrentPage);
        Assert.AreEqual(hidden, board.HiddenCount, "A page that refuses is not hidden.");
        Assert.HasCount(shown, board.ShownWith);
        Assert.IsEmpty(pages.MadeFor(PageKey.Journals), "The page asked for is not made.");
        HistoryIs(navigator.BackHistory, "A refusal records nothing.");
        HistoryIs(navigator.ForwardHistory, "A refusal keeps Forward.", s_library);
    }

    [TestMethod]
    public async Task ARefusedBackPutsTheEntryBack()
    {
        Manuscript gone = Papers.Paper("Deleted");
        var pages = new TestPages(gone);
        Navigator navigator = pages.Navigator;
        var goneRequest = new PageRequest(PageKey.Manuscript, gone.Id);
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(goneRequest);
        await navigator.GoAsync(s_journals);
        pages.Session.Library.Remove(gone);
        FakePage journals = pages.Page(PageKey.Journals);
        journals.AllowsLeave = false;

        Assert.IsFalse(await navigator.GoBackAsync());

        Assert.AreEqual(s_journals, navigator.Current);
        HistoryIs(navigator.BackHistory, "A refused Back keeps every entry, the skipped one too.", goneRequest, s_library, s_board);
        HistoryIs(navigator.ForwardHistory, "A refused Back records nothing.");

        journals.AllowsLeave = true;
        Assert.IsTrue(await navigator.GoBackAsync());
        Assert.AreEqual(s_library, navigator.Current, "The entry kept is the one Back then takes.");
        HistoryIs(navigator.BackHistory, "The skipped entry goes once Back moves.", s_board);
        HistoryIs(navigator.ForwardHistory, "Back puts the page left on Forward.", s_journals);
    }

    [TestMethod]
    public async Task GoingWithoutRecordingRecordsNothing()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(s_journals);
        await navigator.GoBackAsync();
        FakePage library = pages.Page(PageKey.Library);
        int hidden = library.HiddenCount;

        Assert.IsTrue(await navigator.GoAsync(s_deadlines, recordHistory: false));

        Assert.AreEqual(s_deadlines, navigator.Current);
        Assert.AreEqual(hidden + 1, library.HiddenCount, "The page left is still left.");
        HistoryIs(navigator.BackHistory, "The page left is not recorded.", s_board);
        HistoryIs(navigator.ForwardHistory, "Forward is not cleared.", s_journals);
    }

    [TestMethod]
    public async Task KeptPagesAreMadeOnce()
    {
        Manuscript first = Papers.Paper("First");
        Manuscript second = Papers.Paper("Second");
        var pages = new TestPages(first, second);
        Navigator navigator = pages.Navigator;

        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(new PageRequest(PageKey.Manuscript, first.Id));
        await navigator.GoAsync(new PageRequest(PageKey.Manuscript, second.Id));

        Assert.HasCount(1, pages.MadeFor(PageKey.Board));
        Assert.HasCount(1, pages.MadeFor(PageKey.Library));
        Assert.HasCount(1, pages.MadeFor(PageKey.Manuscript), "One manuscript page shows every manuscript.");
        FakePage board = pages.Page(PageKey.Board);
        Assert.HasCount(2, board.ShownWith, "A kept page is shown again on each visit.");
        Assert.AreEqual(2, board.HiddenCount);
        FakePage manuscript = pages.Page(PageKey.Manuscript);
        Assert.AreEqual(1, manuscript.HiddenCount, "Another manuscript is a leave of the one shown.");
        Assert.HasCount(2, manuscript.ShownWith);
    }

    [TestMethod]
    public async Task ASettingsVisitStartsFresh()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;

        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_settings);
        FakePage firstVisit = pages.Page(PageKey.Settings);
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_settings);

        IReadOnlyList<FakePage> made = pages.MadeFor(PageKey.Settings);
        Assert.HasCount(2, made, "Settings is made on each visit (#375: it remembers nothing of the last).");
        Assert.AreNotSame(made[0], made[1]);
        Assert.AreSame(made[1], navigator.CurrentPage);
        Assert.AreEqual(1, firstVisit.HiddenCount, "The page left is hidden before it is dropped.");
        Assert.HasCount(1, made[1].ShownWith);
    }

    [TestMethod]
    public async Task TheOriginStaysAcrossManuscripts()
    {
        Manuscript first = Papers.Paper("First");
        Manuscript second = Papers.Paper("Second");
        var pages = new TestPages(first, second);
        Navigator navigator = pages.Navigator;
        Assert.AreEqual(PageKey.Board, navigator.Origin, "Before any manuscript, today's origin is the Board.");

        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(new PageRequest(PageKey.Manuscript, first.Id));
        Assert.AreEqual(PageKey.Library, navigator.Origin, "The manuscript page belongs to the page it was opened from.");
        await navigator.GoAsync(new PageRequest(PageKey.Manuscript, second.Id));
        Assert.AreEqual(PageKey.Library, navigator.Origin, "From one manuscript to the next, the origin stays.");
        await navigator.GoBackAsync();
        Assert.AreEqual(PageKey.Library, navigator.Origin, "Back to a manuscript from a manuscript keeps it too.");

        await navigator.GoAsync(s_journals);
        await navigator.GoAsync(new PageRequest(PageKey.Manuscript, second.Id));
        Assert.AreEqual(PageKey.Journals, navigator.Origin, "Entered again from another page, the origin is that page.");
    }

    [TestMethod]
    public async Task HiddenIsCalledOncePerLeaveAndNotOnARefusal()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        FakePage board = pages.Page(PageKey.Board);
        FakePage library = pages.Page(PageKey.Library);
        Assert.AreEqual(1, board.HiddenCount, "A leave hides the page left once.");

        library.AllowsLeave = false;
        Assert.IsFalse(await navigator.GoAsync(s_journals));
        Assert.IsFalse(await navigator.GoBackAsync());
        Assert.AreEqual(2, library.LeaveQuestions);
        Assert.AreEqual(0, library.HiddenCount, "A refusal hides nothing.");

        library.AllowsLeave = true;
        Assert.IsTrue(await navigator.GoBackAsync());
        Assert.AreEqual(1, library.HiddenCount, "An allowed leave hides the page left once.");
        Assert.AreEqual(1, board.HiddenCount, "The page arrived at is not hidden.");
        Assert.HasCount(2, board.ShownWith);
    }

    // Not in the design's list: a page whose factory throws leaves the page
    // shown as it was, not told it is hidden while still shown, and the
    // navigator free for the next move.
    [TestMethod]
    public async Task APageThatCannotBeMadeLeavesThePageShownAsItWas()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        FakePage library = pages.Page(PageKey.Library);
        pages.Broken = PageKey.Journals;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => navigator.GoAsync(s_journals));

        Assert.AreEqual(s_library, navigator.Current);
        Assert.AreSame(library, navigator.CurrentPage);
        Assert.AreEqual(0, library.HiddenCount, "The page still shown is not hidden.");
        HistoryIs(navigator.BackHistory, "Nothing is recorded.", s_board);

        pages.Broken = null;
        Assert.IsTrue(await navigator.GoAsync(s_journals), "The next move goes.");
        Assert.AreEqual(1, library.HiddenCount);
    }

    // Not in the design's list: a page that asks to switch pages from its
    // Shown or Hidden is refused at once, as the page contract says, and the
    // move under way finishes as if alone.
    [TestMethod]
    public async Task ASwitchAskedFromShownOrHiddenIsRefused()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(s_board);
        FakePage board = pages.Page(PageKey.Board);
        FakePage library = pages.Page(PageKey.Library);
        Task<bool>? fromHidden = null;
        Task<bool>? fromShown = null;
        board.WhenHidden = () => fromHidden = navigator.GoAsync(s_journals);
        library.WhenShown = () => fromShown = navigator.GoAsync(s_deadlines);

        Assert.IsTrue(await navigator.GoAsync(s_library));

        Assert.IsNotNull(fromHidden);
        Assert.IsNotNull(fromShown);
        await RefusedAtOnce(fromHidden, "A switch asked from Hidden is refused.");
        await RefusedAtOnce(fromShown, "A switch asked from Shown is refused.");
        Assert.AreEqual(s_library, navigator.Current);
        Assert.AreSame(library, navigator.CurrentPage);
        Assert.IsEmpty(pages.MadeFor(PageKey.Journals), "The refused switches made no page.");
        Assert.IsEmpty(pages.MadeFor(PageKey.Deadlines), "The refused switches made no page.");
        HistoryIs(navigator.BackHistory, "Only the move under way is recorded.", s_board, s_library, s_board);
    }

    // Not in the design's list (it is silent on races): while one leave
    // guard is still answering, a second move of any kind is refused at once
    // and changes nothing; the first then finishes as if alone. Back and
    // Forward both have an entry to take, so only the guard refuses them.
    // The second moves are not awaited: without the guard each would wait on
    // the held answer, and the test would hang instead of failing.
    [TestMethod]
    public async Task ASecondMoveWhileALeaveIsAskedIsRefused()
    {
        var pages = new TestPages();
        Navigator navigator = pages.Navigator;
        await navigator.GoAsync(s_board);
        await navigator.GoAsync(s_library);
        await navigator.GoAsync(s_journals);
        await navigator.GoBackAsync();
        HistoryIs(navigator.BackHistory, "Back has an entry to take.", s_board);
        HistoryIs(navigator.ForwardHistory, "Forward has an entry to take.", s_journals);
        FakePage library = pages.Page(PageKey.Library);
        int asked = library.LeaveQuestions;
        int hidden = library.HiddenCount;
        var answer = new TaskCompletionSource<bool>();
        library.HeldAnswer = answer;

        Task<bool> first = navigator.GoAsync(s_deadlines);
        Assert.IsFalse(first.IsCompleted, "The first move waits on the page's answer.");

        await RefusedAtOnce(navigator.GoAsync(s_settings), "A second move is refused while the first is asked.");
        await RefusedAtOnce(navigator.GoBackAsync(), "Back is refused while a move is asked.");
        await RefusedAtOnce(navigator.GoForwardAsync(), "Forward is refused while a move is asked.");
        Assert.AreEqual(asked + 1, library.LeaveQuestions, "The page is asked once, by the first move only.");
        Assert.AreEqual(s_library, navigator.Current);
        HistoryIs(navigator.BackHistory, "Nothing moved yet.", s_board);
        HistoryIs(navigator.ForwardHistory, "Nothing moved yet.", s_journals);

        answer.SetResult(true);
        Assert.IsTrue(await first);

        Assert.AreEqual(s_deadlines, navigator.Current);
        Assert.IsEmpty(pages.MadeFor(PageKey.Settings), "The refused move made no page.");
        HistoryIs(navigator.BackHistory, "Only the first move is recorded.", s_library, s_board);
        HistoryIs(navigator.ForwardHistory, "The first move, recorded, clears Forward.");
        Assert.AreEqual(hidden + 1, library.HiddenCount, "The page left by the first move is hidden once.");

        library.HeldAnswer = null;
        Assert.IsTrue(await navigator.GoAsync(s_settings), "Once the first move is done, the next goes.");
    }

    // A move refused by the guard against a second move ends at once.
    private static async Task RefusedAtOnce(Task<bool> move, string message)
    {
        Assert.IsTrue(move.IsCompleted, message + " It waited instead of being refused at once.");
        Assert.IsFalse(await move, message);
    }

    private static void HistoryIs(IReadOnlyList<PageRequest> history, string message, params PageRequest[] expected) =>
        CollectionAssert.AreEqual(expected, history.ToArray(), message + " Was: " + string.Join(", ", history));
}
