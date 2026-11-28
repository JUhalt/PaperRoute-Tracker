using System.Diagnostics;
using System.Text.Json;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Measurement;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Shell;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// Build 9's F1a (the design's tests F-1 to F-9, F-19, F-20, F-23 and F-24 to
// F-27): the manuscript page's frame, with no window, over the store in
// memory (FakeManuscriptStore) and stand-in sections. Save, the bar, Discard,
// the input block while saving and the author-file error state. Nothing is
// written to disk: the store's only implementation is the fake, and the
// view models cannot call a core save. F-23 alone reads a synthetic library
// written under the temporary folder.
[TestClass]
public sealed class EditorFrameTests : StorageCheckedTests
{
    private const string DiskFull = "PaperRoute could not save the library. There is not enough space on the disk.";
    private const string Damaged = "The author library file is damaged.";

    private static readonly DateTime Day = new(2026, 3, 2);
    private static readonly DateTimeOffset FirstInstant = new(2031, 7, 4, 9, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondInstant = new(2031, 7, 5, 16, 40, 0, TimeSpan.Zero);
    private static readonly Guid Ada = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000001");
    private static readonly Guid Grace = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000002");
    private static readonly Guid Preprint = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000005");
    private static readonly Guid Shortlisted = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000006");

    public TestContext TestContext { get; set; } = null!;

    // F-1
    [TestMethod]
    public async Task TheBarShowsOnAnEditAndHidesAfterSave()
    {
        Page page = Page.Open(Paper());
        Assert.IsFalse(page.Frame.ShowsSaveBar, "The bar shows on a record just opened.");

        page.Title.Text = "A Second Study of Tests";
        Assert.IsTrue(page.Frame.ShowsSaveBar, "The bar does not show after an edit.");

        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.IsFalse(page.Frame.ShowsSaveBar, "The bar still shows after the save.");
        Assert.AreEqual(1, page.Store.Saves);
        Assert.AreEqual("A Second Study of Tests", page.Saved.Title, "The save did not reach the session's record.");
    }

    // F-2
    [TestMethod]
    public async Task TheBarHintTextsAreTodays()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        Assert.AreEqual("Save keeps them. Discard returns to the last saved version.", page.Frame.SaveBarHint);

        // A failed library save with nothing edited: Save tries again
        // (Form1.ManuscriptPage.vb:1121-1125); with an edit, the usual hint.
        page.Frame.Discard();
        page.Store.LastLibrarySaveFailed = true;
        Assert.IsTrue(page.Frame.ShowsSaveBar, "The bar does not show after a failed library save.");
        Assert.AreEqual("Your last save did not finish. Save tries again.", page.Frame.SaveBarHint);
        page.Title.Text = "A Second Study of Tests";
        Assert.AreEqual("Save keeps them. Discard returns to the last saved version.", page.Frame.SaveBarHint);

        // This page's own save failed: its edits stay, and the bar says so
        // (2.5, step 9).
        page.Store.LastLibrarySaveFailed = false;
        page.Store.Results.Enqueue(ManuscriptSaveResult.Failed(DiskFull, authorLibraryWritten: false));
        Assert.IsFalse(await page.Frame.SaveAsync());
        Assert.AreEqual("Your last save did not finish. Save tries again.", page.Frame.SaveBarHint);
    }

    // F-3
    [TestMethod]
    public async Task SaveIsRefusedSilentlyWhileLibraryWorkRuns()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        page.Store.SetBusy(true);

        Assert.IsFalse(await page.Frame.SaveAsync(), "A save went on while library work ran.");

        AssertRefusedSilently(page);
    }

    [TestMethod]
    public async Task SnapshotLibrariesCannotSave()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        page.Store.CanSave = false;

        Assert.IsFalse(await page.Frame.SaveAsync(), "A snapshot was saved.");

        AssertRefusedSilently(page);
    }

    // F-4 (D5)
    [TestMethod]
    public async Task SaveWithNothingUnsavedWritesNothing()
    {
        Page page = Page.Open(Paper());

        Assert.IsTrue(await page.Frame.SaveAsync(), "Saving nothing did not count as saved.");

        Assert.AreEqual(0, page.Store.Writes, "Saving nothing wrote.");
        Assert.IsEmpty(page.Notices.Notices);
    }

    [TestMethod]
    public async Task SaveWithNothingUnsavedRetriesWhenALibrarySaveFailed()
    {
        Page page = Page.Open(Paper());
        page.Store.LastLibrarySaveFailed = true;
        Assert.IsTrue(page.Frame.ShowsSaveBar);

        Assert.IsTrue(await page.Frame.SaveAsync(), "The retry that succeeded did not count as saved.");

        Assert.AreEqual(1, page.Store.Retries, "Save did not try the library's save again.");
        Assert.AreEqual(0, page.Store.Saves, "Save wrote the unchanged manuscript instead of retrying.");
        Assert.IsFalse(page.Store.LastLibrarySaveFailed);
        Assert.IsFalse(page.Frame.ShowsSaveBar, "The bar still shows after the retry succeeded.");
    }

    // F-5 (D1)
    [TestMethod]
    public async Task AFailedSaveLeavesTheEditsUnsavedAndTheOriginalUntouched()
    {
        Page page = Page.Open(SubmittablePaper());
        string savedBefore = JsonSerializer.Serialize(page.Saved);
        page.Title.Text = "A Second Study of Tests";
        page.Frame.Copy.HoldStage(PaperStage.Submitted);
        int token = page.Frame.Copy.Token;
        page.Store.Results.Enqueue(ManuscriptSaveResult.Failed(DiskFull, authorLibraryWritten: false));

        Assert.IsFalse(await page.Frame.SaveAsync(), "A failed save counted as saved.");

        Assert.AreEqual(savedBefore, JsonSerializer.Serialize(page.Saved), "A failed save changed the session's record.");
        Assert.AreEqual("A Second Study of Tests", page.Frame.Copy.Working.Title, "A failed save lost the edit.");
        Assert.AreEqual(PaperStage.Submitted, page.Frame.Copy.HeldStage, "A failed save lost the held stage.");
        Assert.AreEqual(PaperStage.Draft, page.Frame.Copy.Working.CurrentStage, "A failed save applied the held stage to the working copy.");
        Assert.AreEqual(token, page.Frame.Copy.Token, "A failed save started the working copy again.");
        Assert.IsTrue(page.Frame.HasUnsavedWork, "After a failed save the page counts as saved.");
        Assert.IsTrue(page.Frame.ShowsSaveBar);
        Assert.AreEqual(ManuscriptEditorViewModel.SaveFailedHint, page.Frame.SaveBarHint);
        CollectionAssert.AreEqual(new[] { DiskFull }, page.Notices.Notices, "The failure's message was not given.");
        Assert.AreEqual("A Study of Tests", page.Frame.Header!.Title, "The header shows what failed to save.");
        Assert.IsFalse(page.Frame.Copy.IsFrozen, "The working copy stayed frozen.");
        Assert.IsFalse(page.Frame.IsSaving);
        Assert.IsFalse(page.Frame.IsInputBlocked);

        // Save tries again from the same state, and the retry writes one
        // stage change.
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.AreEqual(1, page.Saved.History.Count(item => item.Note.StartsWith("Stage changed from ", StringComparison.Ordinal)));
        Assert.AreEqual(ManuscriptEditorViewModel.EditedHint, page.Frame.SaveBarHint, "A save that succeeded kept the failure's hint.");
    }

    // F-6. LibraryChanges.MarkWritten raises no Changed, so the frame must
    // recompute the bar itself once the save returns.
    [TestMethod]
    public async Task AFailedSaveAfterTheAuthorWriteMarksThePeopleOnDisk()
    {
        Page page = Page.Open(Paper());
        page.Frame.Copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" }));
        Assert.IsTrue(page.Frame.ShowsSaveBar, "A page-made person does not show the bar.");
        page.Store.Results.Enqueue(ManuscriptSaveResult.Failed(DiskFull, authorLibraryWritten: true));
        page.Store.HoldNextSave = true;
        Task<bool> save = page.Frame.SaveAsync();
        int changes = 0;
        page.Frame.Copy.Changed += (_, _) => changes++;

        page.Store.Release();
        Assert.IsFalse(await save);

        Assert.AreEqual(0, changes, "The working copy raised Changed after the write, so this test no longer shows the frame's own recompute.");
        Assert.IsTrue(page.Frame.Copy.Delta.IsEmpty, "The people written to disk are still waiting to be saved.");
        Assert.IsTrue(page.Store.AuthorFile.Authors.Any(author => author.Id == Grace), "The fake did not write the author file first.");
        Assert.IsFalse(page.Frame.HasUnsavedWork, "Nothing is left to save, yet the page counts as unsaved.");
        Assert.IsFalse(page.Frame.ShowsSaveBar, "The bar was not recomputed after the people were marked written.");
        Assert.IsTrue(page.Frame.Copy.Library.Authors.Any(author => author.Id == Grace), "The person written left the pickers' library.");

        // The next save has no people to add.
        page.Title.Text = "A Second Study of Tests";
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.IsTrue(page.Store.Deltas[^1].IsEmpty, "The next save wrote the people again.");
    }

    // F-7 (D10)
    [TestMethod]
    public async Task ASaveAddsAHalfTypedTagFirst()
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        section.TypedTag = "methods ";
        Assert.IsTrue(page.Frame.HasUnsavedWork, "A typed tag alone is not unsaved work.");
        Assert.IsTrue(page.Frame.ShowsSaveBar, "A typed tag alone does not show the bar.");

        Assert.IsTrue(await page.Frame.SaveAsync());

        CollectionAssert.Contains(page.Store.Candidates.Single().Tags, "methods", "The typed tag was not in what Save wrote.");
        CollectionAssert.Contains(page.Saved.Tags, "methods");
        Assert.IsFalse(section.HasPendingInput(), "The tag that joined is still pending.");
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsFalse(page.Frame.ShowsSaveBar);
    }

    // F-8 (D4)
    [TestMethod]
    public async Task AProblemStopsSaveFocusesTheFieldAndChangesNothing()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "   ";
        string before = page.Frame.Copy.Snapshot();
        var focused = new List<FieldProblem>();
        page.Frame.ProblemFocusRequested += (_, problem) => focused.Add(problem);

        Assert.IsFalse(await page.Frame.SaveAsync(), "A blank title was saved.");

        var expected = new FieldProblem("Title", "Please enter a manuscript title.");
        CollectionAssert.AreEqual(new[] { expected }, page.Frame.Problems.ToArray());
        CollectionAssert.AreEqual(new[] { expected }, focused, "Save did not ask once for the title to be focused.");
        Assert.AreEqual(0, page.Store.Writes, "A save stopped by a problem wrote.");
        Assert.AreEqual(before, page.Frame.Copy.Snapshot(), "A save stopped by a problem changed the working copy.");
        Assert.IsTrue(page.Frame.HasUnsavedWork);
        Assert.IsEmpty(page.Notices.Notices);

        page.Title.Text = "A Second Study of Tests";
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.IsEmpty(page.Frame.Problems, "The problem stayed after a save that went on.");
    }

    [TestMethod]
    public async Task AnIncompleteNewRowStopsSave()
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        section.IncompleteRow = true;
        Assert.IsTrue(page.Frame.ShowsSaveBar, "An incomplete new row does not show the bar.");
        var focused = new List<FieldProblem>();
        page.Frame.ProblemFocusRequested += (_, problem) => focused.Add(problem);

        Assert.IsFalse(await page.Frame.SaveAsync(), "A save went on with an incomplete new row.");

        var expected = new FieldProblem(StandInSection.IncompleteRowField, StandInSection.IncompleteRowProblem);
        CollectionAssert.AreEqual(new[] { expected }, page.Frame.Problems.ToArray());
        CollectionAssert.AreEqual(new[] { expected }, focused);
        Assert.AreEqual(0, page.Store.Writes);
        Assert.IsTrue(section.HasPendingInput(), "The incomplete row was dropped.");
        Assert.IsTrue(page.Frame.ShowsSaveBar);
    }

    // F-9 (critique S2)
    [TestMethod]
    public async Task InputIsBlockedDuringASaveAndFocusStays()
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        page.Title.Text = "A Second Study of Tests";
        IManuscriptSection? selected = page.Frame.SelectedSection;
        var changed = new List<string>();
        page.Frame.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        bool frozenWhenBlocked = true;
        page.Frame.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ManuscriptEditorViewModel.IsInputBlocked) && page.Frame.IsInputBlocked)
            {
                frozenWhenBlocked = page.Frame.Copy.IsFrozen;
            }
        };
        page.Store.HoldNextSave = true;

        Task<bool> save = page.Frame.SaveAsync();

        Assert.IsFalse(save.IsCompleted, "The held save did not wait.");
        Assert.IsTrue(page.Frame.IsInputBlocked, "Input to the content was not blocked while saving.");
        Assert.IsTrue(page.Frame.IsSaving);
        Assert.IsFalse(frozenWhenBlocked, "Input was blocked only after the copy froze.");
        Assert.IsTrue(page.Frame.Copy.IsFrozen, "The working copy accepts edits while the save writes.");
        Assert.IsFalse(page.Frame.SaveCommand.CanExecute(null), "Save is not disabled while saving.");
        Assert.IsFalse(page.Frame.DiscardCommand.CanExecute(null), "Discard is not disabled while saving.");
        CollectionAssert.AreEqual(
            new[] { nameof(ManuscriptEditorViewModel.IsInputBlocked), nameof(ManuscriptEditorViewModel.IsSaving) },
            changed.SkipWhile(name => name != nameof(ManuscriptEditorViewModel.IsInputBlocked)).ToArray(),
            "Something else on the page changed as the save began.");
        Assert.AreSame(selected, page.Frame.SelectedSection, "The selected section moved.");

        // After 200 ms of saving, the hint says so.
        Assert.AreEqual(ManuscriptEditorViewModel.EditedHint, page.Frame.SaveBarHint, "The hint changed before the wait ended.");
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), page.Timer.Delay);
        Assert.IsTrue(page.Timer.Fire());
        Assert.AreEqual("Saving...", page.Frame.SaveBarHint);

        page.Store.Release();
        Assert.IsTrue(await save);

        Assert.IsFalse(page.Frame.IsInputBlocked, "Input stayed blocked after the save.");
        Assert.IsFalse(page.Frame.IsSaving);
        Assert.IsFalse(page.Frame.Copy.IsFrozen);
        Assert.IsTrue(page.Frame.SaveCommand.CanExecute(null));
        Assert.IsTrue(page.Frame.DiscardCommand.CanExecute(null));
        Assert.AreEqual(ManuscriptEditorViewModel.EditedHint, page.Frame.SaveBarHint, "The hint still says Saving....");
        Assert.IsFalse(page.Timer.IsPending, "The wait for the Saving hint was left running.");
    }

    [TestMethod]
    public async Task AnEditDuringASaveIsRefusedAndLogged()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        page.Store.HoldNextSave = true;
        Task<bool> save = page.Frame.SaveAsync();
        var log = new LogCapture();
        Trace.Listeners.Add(log);
        bool accepted;
        try
        {
            accepted = page.Frame.Copy.Edit(working => working.Title = "Typed while saving");
        }
        finally
        {
            Trace.Listeners.Remove(log);
        }

        page.Store.Release();
        Assert.IsTrue(await save);

        Assert.IsFalse(accepted, "An edit was accepted while the save wrote.");
        Assert.IsTrue(log.Lines.Any(line => line.Contains("refused an edit", StringComparison.Ordinal)), "The refused edit was not logged.");
        Assert.AreEqual("A Second Study of Tests", page.Frame.Copy.Working.Title, "The refused edit reached the working copy.");
        Assert.AreEqual("A Second Study of Tests", page.Saved.Title);
        Assert.IsFalse(page.Frame.HasUnsavedWork);
    }

    [TestMethod]
    public async Task DisablingNothingDuringASaveCommitsNothing()
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        page.Title.Text = "A Second Study of Tests";
        page.Store.HoldNextSave = true;
        var commandsChanged = new List<string>();
        page.Frame.SaveCommand.CanExecuteChanged += (_, _) => commandsChanged.Add("Save");
        page.Frame.DiscardCommand.CanExecuteChanged += (_, _) => commandsChanged.Add("Discard");

        Task<bool> save = page.Frame.SaveAsync();
        var changed = new List<string>();
        page.Frame.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        (int attaches, int refreshes, int clears, int applied) = (section.Attaches, section.Refreshes, section.Clears, section.Applied.Count);
        string snapshot = page.Frame.Copy.Snapshot();
        page.Timer.Fire();

        // While the save writes, the frame tells the sections nothing and
        // changes nothing in the copy, and only Save and Discard change.
        Assert.AreEqual((attaches, refreshes, clears, applied), (section.Attaches, section.Refreshes, section.Clears, section.Applied.Count), "A section was told something while the save wrote.");
        Assert.AreEqual(snapshot, page.Frame.Copy.Snapshot(), "Something was committed to the working copy while the save wrote.");
        CollectionAssert.AreEqual(new[] { nameof(ManuscriptEditorViewModel.SaveBarHint) }, changed, "Something on the page other than the hint changed while the save wrote.");
        CollectionAssert.AreEquivalent(new[] { "Save", "Discard" }, commandsChanged, "Something other than Save and Discard was disabled.");

        page.Store.Release();
        Assert.IsTrue(await save);
    }

    // F-19
    [TestMethod]
    public void DiscardAsksNothingAndSaysSo()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        page.Frame.Copy.HoldStage(PaperStage.Submitted);
        int reads = page.Store.Reads;

        Assert.IsTrue(page.Frame.DiscardCommand.CanExecute(null));
        page.Frame.DiscardCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "Unsaved changes discarded." }, page.Notices.Notices);
        Assert.AreEqual("A Study of Tests", page.Frame.Copy.Working.Title);
        Assert.AreEqual("A Study of Tests", page.Title.Text, "The title box kept the discarded text.");
        Assert.IsNull(page.Frame.Copy.HeldStage);
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsFalse(page.Frame.ShowsSaveBar);
        Assert.AreEqual(reads + 1, page.Store.Reads, "Discard did not read the author file afresh.");
        Assert.AreEqual(0, page.Store.Writes, "Discard wrote.");
    }

    [TestMethod]
    public async Task DiscardAfterAFailedSaveReturnsToTheSavedRecord()
    {
        Page page = Page.Open(Paper());
        page.Title.Text = "A Second Study of Tests";
        page.Store.Results.Enqueue(ManuscriptSaveResult.Failed(DiskFull, authorLibraryWritten: false));
        Assert.IsFalse(await page.Frame.SaveAsync());

        page.Frame.Discard();

        Assert.AreEqual("A Study of Tests", page.Frame.Copy.Working.Title, "Discard after a failed save kept what failed to save.");
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsFalse(page.Frame.ShowsSaveBar);
        Assert.AreEqual(ManuscriptEditorViewModel.EditedHint, page.Frame.SaveBarHint, "The failure's hint outlived the discard.");
    }

    [TestMethod]
    public void DiscardKeepsTheLibraryFailureFlag()
    {
        Page page = Page.Open(Paper());
        page.Store.LastLibrarySaveFailed = true;
        page.Title.Text = "A Second Study of Tests";

        page.Frame.Discard();

        Assert.IsTrue(page.Store.LastLibrarySaveFailed, "Discard cleared the library's failed-save flag.");
        Assert.IsTrue(page.Frame.ShowsSaveBar, "The bar hid while the library's last save had failed.");
        Assert.AreEqual(ManuscriptEditorViewModel.SaveFailedHint, page.Frame.SaveBarHint);
    }

    [TestMethod]
    public void DiscardClearsPendingInput()
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        section.TypedTag = "meth";
        section.IncompleteRow = true;
        section.EditorOpen = true;

        page.Frame.Discard();

        Assert.IsFalse(section.HasPendingInput(), "Discard kept the pending input.");
        Assert.IsFalse(section.EditorOpen, "Discard left an editor open.");
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsFalse(page.Frame.ShowsSaveBar);
    }

    // F-20
    [TestMethod]
    public async Task TheHeaderShowsTheSavedRecord()
    {
        Page page = Page.Open(SubmittablePaper());
        Assert.AreEqual(new ManuscriptHeader(page.Saved.Id, "A Study of Tests", PaperStage.Draft, "Journal of Tests", ManuscriptLocation.Pipeline), page.Frame.Header);

        page.Title.Text = "A Second Study of Tests";
        page.Frame.Copy.HoldStage(PaperStage.Submitted);
        Assert.AreEqual("A Study of Tests", page.Frame.Header!.Title, "The header shows the working copy, not the saved record.");
        Assert.AreEqual(PaperStage.Draft, page.Frame.Header.Stage);

        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.AreEqual(new ManuscriptHeader(page.Saved.Id, "A Second Study of Tests", PaperStage.Submitted, "Journal of Tests", ManuscriptLocation.Pipeline), page.Frame.Header, "The header was not refreshed after the save.");
    }

    // F-23 (critique N4): reports the time; fails only above four times the
    // Board's keystroke budget. The strict budget is the real window's.
    [TestMethod]
    public void TypingInTheLargestTitleReportsItsTime()
    {
        string root = Path.Combine(Path.GetTempPath(), "PaperRoute-FrameTyping-" + Guid.NewGuid().ToString("N"));
        try
        {
            Manuscript largest = LargeLibraryFixture.Create(root, 60).Manuscripts.MaxBy(item => JsonSerializer.Serialize(item).Length)!;
            Page page = Page.Open(largest, new StandInSection());
            const int Rounds = 200;
            page.Title.Text = largest.Title + " ";
            var clock = Stopwatch.StartNew();
            for (int index = 0; index < Rounds; index++)
            {
                page.Title.Text = largest.Title + " " + (char)('a' + (index % 26));
            }

            clock.Stop();
            Assert.IsTrue(page.Frame.ShowsSaveBar, "The keystrokes did not reach the working copy.");
            double perKeystroke = clock.Elapsed.TotalMilliseconds / Rounds;
            TestContext.WriteLine(
                $"largest record serializes to {page.Frame.Copy.Snapshot().Length:N0} characters; one keystroke's edit, snapshot and bar recompute cost {perKeystroke:N3} ms on average over {Rounds}");
            double ceiling = 4 * BoardMeasurement.FilterKeystrokeBudget;
            Assert.IsLessThanOrEqualTo(ceiling, perKeystroke, $"One keystroke took {perKeystroke:N1} ms, above four times the Board's keystroke budget ({ceiling} ms).");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Left for the system's temporary-file clean-up.
            }
        }
    }

    // F-24 (critique M3): after a save Working is a new object graph, so a
    // row that held an object would edit a copy nobody reads; rows hold ids.
    [TestMethod]
    [DataRow(RowKind.Link)]
    [DataRow(RowKind.Shortlist)]
    [DataRow(RowKind.Author)]
    public async Task AnEditAfterASaveReachesTheWorkingCopy(RowKind kind)
    {
        var section = new StandInSection();
        Page page = Page.Open(Paper(), section);
        Guid id = IdOf(kind);
        Assert.IsTrue(section.Change(kind, id, "First"));
        object before = Item(kind, page.Frame.Copy.Working, id);
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.AreNotSame(before, Item(kind, page.Frame.Copy.Working, id), "The save kept the old object graph, so this test shows nothing.");
        string saved = Read(kind, page.Frame.Copy.Working, id);

        Assert.IsTrue(section.Change(kind, id, "Second"), "The row's edit after the save was refused.");

        Assert.AreNotEqual(saved, Read(kind, page.Frame.Copy.Working, id), "The row's edit after the save did not reach the working copy.");
        Assert.IsTrue(page.Frame.HasUnsavedWork, "The row's edit after the save is not unsaved.");
        Assert.IsTrue(page.Frame.ShowsSaveBar);
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.AreEqual(Read(kind, page.Frame.Copy.Working, id), Read(kind, page.Saved, id), "The second save did not keep the row's edit.");
    }

    // F-25 (the design's rule 2)
    [TestMethod]
    public async Task EverySectionIsAttachedAfterASaveAndAfterDiscard()
    {
        var overview = new StandInSection("Overview");
        var authors = new StandInSection("Authors");
        Page page = Page.Open(Paper(), [overview, authors]);
        AssertAttached(page, 1, overview, authors);

        page.Title.Text = "A Second Study of Tests";
        Assert.IsTrue(await page.Frame.SaveAsync());
        AssertAttached(page, 2, overview, authors);

        // A failed save starts nothing again: the sections keep their state.
        page.Title.Text = "A Third Study of Tests";
        page.Store.Results.Enqueue(ManuscriptSaveResult.Failed(DiskFull, authorLibraryWritten: false));
        Assert.IsFalse(await page.Frame.SaveAsync());
        AssertAttached(page, 2, overview, authors);

        page.Frame.Discard();
        AssertAttached(page, 3, overview, authors);
    }

    // F-26 (the design's rule 2): closed at Save's step 3, before anything
    // is written.
    [TestMethod]
    public async Task SaveClosesEveryCompleteExpandedEditor()
    {
        var overview = new StandInSection("Overview");
        var authors = new StandInSection("Authors");
        Page page = Page.Open(Paper(), [overview, authors]);
        overview.EditorOpen = true;
        authors.EditorOpen = true;
        page.Title.Text = "A Second Study of Tests";
        page.Store.HoldNextSave = true;

        Task<bool> save = page.Frame.SaveAsync();

        Assert.IsFalse(overview.EditorOpen, "An expanded editor stayed open while the save wrote.");
        Assert.IsFalse(authors.EditorOpen, "An expanded editor with no pending input stayed open while the save wrote.");
        CollectionAssert.AreEqual(new[] { PendingUse.Save }, authors.Applied, "Save did not ask a section with no pending input.");
        page.Store.Release();
        Assert.IsTrue(await save);
    }

    // F-27 (critique N7)
    [TestMethod]
    public async Task AnUnreadableAuthorFileShowsTheErrorStateNotTheErrorWindow()
    {
        var section = new StandInSection();
        Manuscript saved = Paper();
        var store = new FakeManuscriptStore([saved]) { ReadFailure = new InvalidDataException(Damaged) };
        Page page = Page.Over(store, saved, section);

        Assert.IsFalse(page.Frame.Open(saved.Id), "The page opened with an unreadable author file.");

        Assert.AreEqual("PaperRoute could not read the author library, so this manuscript cannot be edited. The author library file is damaged.", page.Frame.LoadProblem);
        Assert.IsFalse(page.Frame.IsLoaded, "The record can be edited without its author library.");
        Assert.AreEqual(0, section.Attaches, "A section was given a record to edit.");
        Assert.AreEqual("A Study of Tests", page.Frame.Header?.Title, "The page shows no header to leave by.");
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsFalse(page.Frame.ShowsSaveBar);
        Assert.IsTrue(await page.Frame.SaveAsync());
        Assert.AreEqual(0, store.Writes, "Save wrote from the error state.");

        store.ReadFailure = null;
        Assert.IsTrue(page.Frame.Open(saved.Id));
        Assert.AreEqual(string.Empty, page.Frame.LoadProblem, "The error stayed once the author file could be read.");
        Assert.IsTrue(page.Frame.IsLoaded);
    }

    [TestMethod]
    public void ADiscardThatCannotReadTheAuthorFileKeepsTheLibraryAndSaysSo()
    {
        Page page = Page.Open(Paper());
        page.Frame.Copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" }));
        page.Title.Text = "A Second Study of Tests";
        page.Store.ReadFailure = new InvalidDataException(Damaged);

        page.Frame.Discard();

        CollectionAssert.AreEqual(
            new[] { "Unsaved changes discarded. PaperRoute could not read the author library again, so the page keeps the one it read before. The author library file is damaged." },
            page.Notices.Notices);
        Assert.AreEqual("A Study of Tests", page.Frame.Copy.Working.Title, "The record's edits were kept.");
        Assert.IsTrue(page.Frame.Copy.Delta.IsEmpty, "The page-made person is still waiting to be saved.");
        CollectionAssert.AreEqual(new[] { Ada }, page.Frame.Copy.Library.Authors.Select(author => author.Id).ToArray(),
            "The library read before was not kept as it was on disk.");
        Assert.IsFalse(page.Frame.HasUnsavedWork);
        Assert.IsTrue(page.Frame.IsLoaded);
    }

    // C-13 through the frame (W2's review): the store commits to the
    // session's record first, then the copy accepts the save inside the
    // freeze, so the second save starts from the first.
    [TestMethod]
    public async Task ASecondSaveThroughTheFrameKeepsTheFirstSavesStageChangeAndItsEvent()
    {
        Page page = Page.Open(SubmittablePaper());
        var accepted = new List<(bool Frozen, PaperStage Committed)>();
        int token = page.Frame.Copy.Token;
        page.Frame.Copy.Changed += (_, _) =>
        {
            if (page.Frame.Copy.Token != token)
            {
                token = page.Frame.Copy.Token;
                accepted.Add((page.Frame.Copy.IsFrozen, page.Saved.CurrentStage));
            }
        };
        page.Frame.Copy.HoldStage(PaperStage.Submitted);
        DateTime firstSave;
        using (var clock = new FixedClock(FirstInstant))
        {
            firstSave = clock.Local;
            Assert.IsTrue(await page.Frame.SaveAsync());
        }

        page.Title.Text = "A Second Title";
        using (new FixedClock(SecondInstant))
        {
            Assert.IsTrue(await page.Frame.SaveAsync());
        }

        CollectionAssert.AreEqual(new[] { (true, PaperStage.Submitted), (true, PaperStage.Submitted) }, accepted,
            "Each save must be committed to the session's record and then accepted inside the freeze.");
        Manuscript second = page.Store.Candidates[1];
        Assert.AreEqual("A Second Title", second.Title);
        Assert.AreEqual(PaperStage.Submitted, second.CurrentStage, "The second save erased the first save's stage change.");
        Assert.AreEqual(firstSave, second.StageEnteredDate, "The second save moved the first save's stage date.");
        Assert.AreEqual(1, StageChanges(second), "The second save lost the first save's stage event or added another.");
        Assert.AreEqual(1, StageChanges(page.Saved));
    }

    // D9 for the page (W2's review, beyond C-12): the frame's own save dates
    // the stage and File Drawer reason events by the core's clock.
    [TestMethod]
    public async Task TheFramesSaveDatesItsEventsByTheCoreClock()
    {
        Manuscript original = SubmittablePaper();
        original.Location = ManuscriptLocation.FileDrawer;
        original.FileDrawerDate = Day.AddDays(5);
        original.FileDrawerReason = "Waiting for the second data set";
        int historyBefore = original.History.Count;
        Page page = Page.Open(original);
        page.Frame.Copy.HoldStage(PaperStage.Submitted);
        page.Frame.Copy.Edit(working => working.FileDrawerReason = "Waiting for reviewers");

        using var clock = new FixedClock(FirstInstant);
        Assert.IsTrue(await page.Frame.SaveAsync());

        Manuscript candidate = page.Store.Candidates.Single();
        HistoryEvent[] added = [.. candidate.History.Skip(historyBefore)];
        Assert.HasCount(2, added, "A stage change and a reason change add two events.");
        Assert.AreEqual(clock.Local, candidate.StageEnteredDate, "The stage date is not the core's clock.");
        foreach (HistoryEvent item in added)
        {
            Assert.AreEqual(clock.Local, item.EventDate, $"\"{item.Note}\" is not dated by the core's clock.");
            Assert.AreEqual((DateTime?)FirstInstant.UtcDateTime, item.RecordedAtUtc, $"\"{item.Note}\" is not stamped by the core's clock.");
        }
    }

    // Save's step 6 (D3): the stage stays held, and the requirement shows
    // under the stage list in today's words (EditManuscriptForm.vb:4473-4483)
    // until another stage is chosen.
    [TestMethod]
    public async Task AHeldStageThatNeedsAStepStopsSaveAndKeepsTheStage()
    {
        Page page = Page.Open(Paper());
        page.Frame.Copy.HoldStage(PaperStage.Revision);

        Assert.IsFalse(await page.Frame.SaveAsync(), "A stage that needs a decision was saved.");

        Assert.AreEqual("PaperRoute did not find a revision editorial decision, so the current stage was not changed.", page.Frame.StageMessage);
        Assert.AreEqual(PaperStage.Revision, page.Frame.Copy.HeldStage, "The held stage was dropped.");
        Assert.AreEqual(0, page.Store.Writes);
        Assert.IsTrue(page.Frame.ShowsSaveBar);

        page.Frame.Copy.HoldStage(PaperStage.Draft);
        Assert.AreEqual(string.Empty, page.Frame.StageMessage, "The requirement stayed after another stage was chosen.");
    }

    private static void AssertRefusedSilently(Page page)
    {
        Assert.AreEqual(0, page.Store.Writes, "A refused save reached the store's write.");
        Assert.IsEmpty(page.Notices.Notices, "A refused save said something.");
        Assert.AreEqual("A Second Study of Tests", page.Frame.Copy.Working.Title, "A refused save lost the edit.");
        Assert.IsTrue(page.Frame.HasUnsavedWork);
        Assert.IsTrue(page.Frame.ShowsSaveBar);
        Assert.IsFalse(page.Frame.IsInputBlocked);
    }

    private static void AssertAttached(Page page, int times, params StandInSection[] sections)
    {
        foreach (StandInSection section in sections)
        {
            Assert.AreEqual(times, section.Attaches, $"{section.Key} was not attached {times} times.");
            Assert.AreSame(page.Frame.Copy, section.Copy, $"{section.Key} was attached to another copy.");
        }
    }

    private static int StageChanges(Manuscript record) =>
        record.History.Count(item => item.Note.StartsWith("Stage changed from ", StringComparison.Ordinal));

    private static Guid IdOf(RowKind kind) => kind switch
    {
        RowKind.Link => Preprint,
        RowKind.Shortlist => Shortlisted,
        RowKind.Author => Ada,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static object Item(RowKind kind, Manuscript record, Guid id) => kind switch
    {
        RowKind.Link => record.RelatedLinks.Single(link => link.Id == id),
        RowKind.Shortlist => record.JournalShortlist.Single(candidate => candidate.Id == id),
        RowKind.Author => record.Authors.Single(author => author.AuthorId == id),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Read(RowKind kind, Manuscript record, Guid id) => Item(kind, record, id) switch
    {
        ManuscriptExternalLink link => link.Label,
        JournalCandidate candidate => candidate.Notes,
        ManuscriptAuthor author => author.IsCorrespondingAuthor ? "corresponding" : "not corresponding",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Manuscript Paper() => new()
    {
        Title = "A Study of Tests",
        CurrentStage = PaperStage.Draft,
        StageEnteredDate = Day,
        TargetJournal = "Journal of Tests",
        Authors = [new ManuscriptAuthor { AuthorId = Ada }],
        RelatedLinks = [new ManuscriptExternalLink { Id = Preprint, Label = "Preprint", Url = "https://example.org/preprint" }],
        JournalShortlist = [new JournalCandidate { Id = Shortlisted, JournalName = "Journal of Examples" }],
    };

    // A draft with a submission recorded before its stage date: the load's
    // reconcile leaves it at Draft, and Submitted is supported.
    private static Manuscript SubmittablePaper()
    {
        Manuscript paper = Paper();
        paper.Submissions.Add(new JournalSubmission { JournalName = "Journal of Tests", SubmittedDate = Day.AddDays(-10) });
        return paper;
    }

    private static AuthorLibraryData AuthorFile() => new()
    {
        Authors = [new AuthorRecord { Id = Ada, GivenName = "Ada", FamilyName = "Lovelace" }],
    };

    // A frame over the store in memory, with the session's record, a title
    // box over its working copy, and what it was given.
    private sealed class Page
    {
        private Page(FakeManuscriptStore store, Manuscript saved, StandInSection[] sections)
        {
            Store = store;
            Saved = saved;
            Frame = new ManuscriptEditorViewModel(store, Notices, Timer, sections);
            Title = new TypedText(Frame.Copy, TypedField.Title);
        }

        public FakeManuscriptStore Store { get; }

        // The session's own record, which only a save that succeeded changes.
        public Manuscript Saved { get; }

        public NoticeLog Notices { get; } = new();

        public ManualDebouncer Timer { get; } = new();

        public ManuscriptEditorViewModel Frame { get; }

        public TypedText Title { get; }

        public static Page Open(Manuscript saved, params StandInSection[] sections)
        {
            Page page = Over(new FakeManuscriptStore([saved], AuthorFile()), saved, sections);
            Assert.IsTrue(page.Frame.Open(saved.Id), "The page did not open its record.");
            return page;
        }

        public static Page Over(FakeManuscriptStore store, Manuscript saved, params StandInSection[] sections) =>
            new(store, saved, sections);
    }
}
