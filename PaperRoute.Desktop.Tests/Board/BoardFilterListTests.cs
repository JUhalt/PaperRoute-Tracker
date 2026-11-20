using System.ComponentModel;
using ManuscriptPipeline.Models;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Board;

// The #338 rule in the view model: the V parts of B1 to B5 (d1 6.2; step 5
// of d1 section 8). Each ports a test of PaperRoute.Tests\BoardFilterTests.vb
// on the same library (LargeLibraryFixture, 120 manuscripts, read back from
// disk through the trial's session), and pins Form1.vb BoardFilterChanged,
// BoardFilterListClosed and FlushBoardFilterChange (:1921-1992) with the
// fields of :60-69 and RenderManuscripts' boardFilterShown (:2678).
//
// Today's tests tell a render by the window's attention snapshots and an
// open list by the BoardFilterListOpen seam; here a render is
// BoardViewModel.RenderCount and an open list IsStageListOpen or
// IsSortListOpen, which the view binds to the boxes (step 8 adds the
// headless halves, on real ComboBoxes). The posted flush, Form1's
// BeginInvoke, waits in a QueuedUiDispatcher until RunAll, the twin of
// today's Application.DoEvents.
[TestClass]
public sealed class BoardFilterListTests : StorageCheckedTests
{
    private const int AllStages = 0;
    private const int Draft = 2;
    private const int Submitted = 3;

    private string _root = string.Empty;
    private DiskLibrarySession? _session;
    private QueuedUiDispatcher _posts = new();
    private BoardViewModel _board = null!;

    [TestInitialize]
    public void StartBoard()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialFilters-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        LargeLibraryFixture.Create(_root, 120);
        _session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            Path.Combine(_root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        _posts = new QueuedUiDispatcher();
        _board = new BoardViewModel(_session, new ManualDebouncer(), _posts);
        _session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.AreEqual(1, _board.RenderCount, "The load rendered the Board once.");
        Assert.HasCount(120, _session.Manuscripts);
        Assert.AreEqual(PaperStage.Idea, _board.StageOptions[1].Stage);
        Assert.AreEqual(PaperStage.Draft, _board.StageOptions[Draft].Stage);
        Assert.AreEqual(PaperStage.Submitted, _board.StageOptions[Submitted].Stage);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        _session?.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for Windows' temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for Windows' temporary-file clean-up.
        }
    }

    private ShelfViewModel Pipeline => _board.Shelves[0];

    // B1. Ports BoardFilterTests.vb:58 SteppingTheOpenStageListRendersOnceWhenItCloses:
    // Idea, Draft, Submitted in the open list render nothing, nor does the
    // close; the posted flush renders once, with Submitted.
    [TestMethod]
    public void B1_SteppingTheOpenStageListRendersOnceWhenItCloses()
    {
        List<Manuscript> all = ShelfManuscripts();
        int renders = _board.RenderCount;

        _board.IsStageListOpen = true;
        _board.SelectedStageIndex = 1;
        _board.SelectedStageIndex = Draft;
        _board.SelectedStageIndex = Submitted;
        _board.IsStageListOpen = false;

        Assert.AreEqual(renders, _board.RenderCount, "Nothing is rendered while the list is open, nor as it closes.");
        CollectionAssert.AreEqual(all, ShelfManuscripts(), "The shelf still shows All stages.");
        Assert.AreEqual(1, _posts.Count, "The close posted one flush.");

        Assert.AreEqual(1, _posts.RunAll());
        Assert.AreEqual(renders + 1, _board.RenderCount, "The Board rendered once the close was handled.");
        AssertShows(Submitted);
    }

    // B2. Ports BoardFilterTests.vb:93 AListClosedWithoutAChoiceSettlesOnTheBoxWithoutAStrayRender:
    // whether the opening choice comes back before the close or after it, or
    // the last step is kept, the Board ends on the box's choice and renders
    // only when that is not what it shows; a choice that comes after the
    // close renders once.
    [TestMethod]
    public void B2_AListClosedWithoutAChoiceSettlesOnTheBoxWithoutAStrayRender()
    {
        List<Manuscript> all = ShelfManuscripts();

        // Opened and closed with no step at all.
        Assert.AreEqual(0, Renders(() => _board.IsStageListOpen = true, () => _board.IsStageListOpen = false),
            "A list opened and closed renders nothing.");

        // The choice handed back while the list is open.
        Assert.AreEqual(0, Renders(() => StepOpenStageList(1, Draft, AllStages), () => _board.IsStageListOpen = false),
            "Steps handed back before the close render nothing.");
        CollectionAssert.AreEqual(all, ShelfManuscripts(), "The shelf shows All stages, as the box does.");

        // The choice handed back after the close, before its flush runs.
        Assert.AreEqual(0, Renders(
            () => StepOpenStageList(1, Draft),
            () =>
            {
                _board.IsStageListOpen = false;
                _board.SelectedStageIndex = AllStages;
            }),
            "Steps handed back after the close render nothing.");
        CollectionAssert.AreEqual(all, ShelfManuscripts(), "The shelf shows All stages, as the box does.");

        // The last step kept.
        Assert.AreEqual(1, Renders(() => StepOpenStageList(1, Draft), () => _board.IsStageListOpen = false),
            "A step kept as the list closes renders once.");
        AssertShows(Draft);

        // A pick whose choice comes after the close: the list is already
        // closed, but its flush is posted, so the choice waits for it.
        Assert.AreEqual(1, Renders(
            () => StepOpenStageList(),
            () =>
            {
                _board.IsStageListOpen = false;
                _board.SelectedStageIndex = Submitted;
            }),
            "A choice that comes after the close renders once.");
        AssertShows(Submitted);
    }

    // B3. Ports BoardFilterTests.vb:143 AStepOnAClosedStageBoxRendersAtOnce:
    // an arrow key or the mouse wheel on the closed box renders at once, and
    // posts nothing.
    [TestMethod]
    public void B3_AStepOnAClosedStageBoxRendersAtOnce()
    {
        foreach (int stage in new[] { Draft, AllStages })
        {
            int renders = _board.RenderCount;
            _board.SelectedStageIndex = stage;
            Assert.AreEqual(renders + 1, _board.RenderCount, "The step rendered before any posted work ran.");
            Assert.AreEqual(0, _posts.Count, "No flush was posted.");
            AssertShows(stage);
        }
    }

    // B4. Ports BoardFilterTests.vb:171 TheSortBoxRendersOnceWhenItsListClosesAndAtOnceWhenClosed:
    // the sort box follows the same rule.
    [TestMethod]
    public void B4_TheSortBoxRendersOnceWhenItsListClosesAndAtOnceWhenClosed()
    {
        List<Manuscript> current = ShelfManuscripts();
        int renders = _board.RenderCount;

        // Title A-Z, then Title Z-A, in the open list.
        _board.IsSortListOpen = true;
        _board.SelectedSortIndex = 1;
        _board.SelectedSortIndex = 2;
        _board.IsSortListOpen = false;
        Assert.AreEqual(renders, _board.RenderCount, "Nothing is rendered while the sort list is open, nor as it closes.");
        CollectionAssert.AreEqual(current, ShelfManuscripts(), "The shelf keeps its order until the close is handled.");
        Assert.AreEqual(1, _posts.RunAll());
        Assert.AreEqual(renders + 1, _board.RenderCount, "The Board rendered once the close was handled.");

        List<Manuscript> zToA = ShelfManuscripts();
        CollectionAssert.AreEqual(
            current.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToList(),
            zToA,
            "The shelf is sorted Title Z-A.");
        _board.Render();
        CollectionAssert.AreEqual(zToA, ShelfManuscripts(), "The shelf shows what a render with Title Z-A chosen shows.");

        // Title A-Z on the closed box.
        renders = _board.RenderCount;
        _board.SelectedSortIndex = 1;
        Assert.AreEqual(renders + 1, _board.RenderCount, "A step on the closed sort box renders at once.");
        Assert.AreEqual(0, _posts.Count, "No flush was posted.");
        CollectionAssert.AreEqual(Enumerable.Reverse(zToA).ToList(), ShelfManuscripts(), "The shelf is sorted Title A-Z.");
    }

    // B5. The view-model half of BoardFilterTests.vb:220
    // AFilterChangePaintsNothingSynchronously: today a filter change holds the
    // shelves' drawing so the cards are painted once. Here each change of the
    // same test (to Submitted, back to All stages, to Draft, Clear) renders
    // once, the cards whose key is unchanged are the same instances (so their
    // views stay), and a kept card raises no change notification, so nothing
    // on it is drawn again.
    [TestMethod]
    public void B5_AFilterChangeRendersOnceAndKeepsUnchangedCards()
    {
        Dictionary<Guid, CardViewModel> first = Pipeline.Cards.ToDictionary(card => card.Id);
        Assert.IsNotEmpty(first);

        void Change(string name, Action change)
        {
            var notified = new List<string>();
            PropertyChangedEventHandler handler = (sender, e) => notified.Add(((CardViewModel)sender!).Title + "." + e.PropertyName);
            foreach (CardViewModel card in first.Values)
            {
                card.PropertyChanged += handler;
            }

            int renders = _board.RenderCount;
            try
            {
                change();
                _posts.RunAll();
            }
            finally
            {
                foreach (CardViewModel card in first.Values)
                {
                    card.PropertyChanged -= handler;
                }
            }

            Assert.AreEqual(renders + 1, _board.RenderCount, name + ": one render.");
            Assert.IsEmpty(notified, name + ": a kept card changed: " + string.Join(", ", notified.Take(10)));
            foreach (CardViewModel card in Pipeline.Cards)
            {
                Assert.IsTrue(first.TryGetValue(card.Id, out CardViewModel? kept), name + ": a card with no key change was new.");
                Assert.AreSame(kept, card, name + ": the card of " + card.Title + " was rebuilt though its key is unchanged.");
            }
        }

        Change("All stages to Submitted", () => _board.SelectedStageIndex = Submitted);
        AssertShows(Submitted);
        Change("Submitted to All stages", () => _board.SelectedStageIndex = AllStages);
        AssertShows(AllStages);
        Change("All stages to Draft in the open list", () =>
        {
            StepOpenStageList(1, Draft);
            _board.IsStageListOpen = false;
        });
        AssertShows(Draft);
        Change("Clear", () => _board.ClearCommand.Execute(null));
        AssertShows(AllStages);
        Assert.HasCount(first.Count, Pipeline.Cards, "Clear shows every Pipeline card again.");
    }

    // Opens the stage list and steps through it, as the arrow keys do.
    private void StepOpenStageList(params int[] indices)
    {
        _board.IsStageListOpen = true;
        foreach (int index in indices)
        {
            _board.SelectedStageIndex = index;
        }
    }

    // How many renders the steps and the close gave once the posted work ran;
    // none before then (BoardFilterTests.vb Renders, :322-336).
    private int Renders(Action steps, Action close)
    {
        int renders = _board.RenderCount;
        steps();
        close();
        Assert.AreEqual(renders, _board.RenderCount, "Nothing is rendered before the close is handled.");
        _posts.RunAll();
        Assert.AreEqual(0, _posts.Count);
        return _board.RenderCount - renders;
    }

    // The Pipeline shelf shows the manuscripts of the stage chosen, in the
    // library's order, and the box shows that stage
    // (BoardFilterTests.vb AssertShows, :341-349).
    private void AssertShows(int stage)
    {
        Assert.AreEqual(stage, _board.SelectedStageIndex, "The stage filter's choice.");
        PaperStage? chosen = _board.StageOptions[stage].Stage;
        List<Manuscript> expected = _session!.Manuscripts
            .Where(item => item.Location == ManuscriptLocation.Pipeline && (chosen is null || item.CurrentStage == chosen))
            .ToList();
        Assert.IsNotEmpty(expected, "The library has Pipeline manuscripts at the stage chosen.");
        CollectionAssert.AreEqual(expected, ShelfManuscripts(), "The Pipeline shelf's cards for the stage chosen.");
    }

    private List<Manuscript> ShelfManuscripts() => Pipeline.Cards.Select(card => card.Manuscript).ToList();
}
