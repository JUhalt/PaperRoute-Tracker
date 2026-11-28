using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Recovery;

namespace PaperRoute.Desktop.Tests.Recovery;

// Recovery drafts without a page (start-up design 2.5 and 5.3, build 6 D1):
// RecoveryDrafts over a folder each test makes in the temporary folder, never
// under a data root. "An earlier session kept" a draft means it was written
// through the core's store (which the test project may call) and no
// RecoveryDrafts of this test owns it; "a crash" means a session that ends
// without DeleteThisSessionsDraft. The draft files of PaperRoute 1.0.0 and
// 1.0.1 are the fixtures under Fixtures\Recovery.
[TestClass]
public sealed partial class RecoveryDraftsTests
{
    private const string Owner = "PaperRoute.ViewModels/Recovery/RecoveryDrafts.cs";
    private const string V100Fixture = "draft-1.0.0.json";
    private const string V101Fixture = "draft-1.0.1.json";

    private static readonly string[] s_keysOf100 = ["ManuscriptId", "WrittenAtUtc", "Reason", "Manuscript"];
    private static readonly string[] s_keysOf101 = ["ManuscriptId", "WrittenAtUtc", "Reason", "Manuscript", "UnsavedLibrary"];

    private static readonly Guid s_x = new("0e6f1d2c-1111-4a2b-9c3d-4e5f6a7b8c01");
    private static readonly Guid s_y = new("0e6f1d2c-2222-4a2b-9c3d-4e5f6a7b8c02");
    private static readonly Guid s_z = new("0e6f1d2c-3333-4a2b-9c3d-4e5f6a7b8c03");
    private static readonly Guid s_w = new("0e6f1d2c-4444-4a2b-9c3d-4e5f6a7b8c04");
    private static readonly Guid s_v100 = new("4f1c2b7a-3d5e-4a61-9b8c-0d2e7f6a1b01");
    private static readonly Guid s_v101 = new("8a3e5d21-6c4b-4f70-a2d9-1e5b7c3f9d02");

    private static readonly DateTime s_first = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime s_second = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime s_third = new(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime s_fourth = new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    private string _root = string.Empty;
    private string _data = string.Empty;

    private string MainFile => Path.Combine(_data, "recovery", RecoveryDraftStore.FileName);

    [TestInitialize]
    public void MakeTheFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-DraftsTest-" + Guid.NewGuid().ToString("N"));
        _data = Path.Combine(_root, "data");
        Directory.CreateDirectory(_data);
    }

    [TestCleanup]
    public void RemoveTheFolder()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for the system's temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the system's temporary-file clean-up.
        }
    }

    // Rule 1: until a page can open a draft, a 1.0.x draft is not offered,
    // and neither a normal close nor a shutdown with nothing unsaved touches it.
    [TestMethod]
    public void NoPageNoOfferAndNoDelete()
    {
        byte[] before = PlaceFixture(V100Fixture, _data);
        var drafts = new RecoveryDrafts(_data);
        drafts.ReadAtStart([s_v100]);

        Assert.IsEmpty(drafts.Offers, "A draft was offered before any page could open it.");
        Assert.IsFalse(drafts.WriteFrom(new TestDraftPage(s_v100, "Unchanged", s_second) { NothingUnsaved = true }, RecoveryReasons.Shutdown),
            "A shutdown with nothing unsaved wrote a draft.");
        drafts.DeleteThisSessionsDraft();
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The 1.0.x draft changed although no page could open it.");

        drafts.OpenWith(_ => Task.FromResult(true));
        Assert.HasCount(1, drafts.Offers, "Once a page can open it, the draft should be offered.");
    }

    // Rule 2: a new draft never replaces one nobody answered.
    [TestMethod]
    public void ADraftForAnotherManuscriptGoesToAWaitingSlot()
    {
        Keep(_data, s_x, "Paper X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x, s_y);

        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_y, "Paper Y", s_second), RecoveryReasons.Shutdown), "The draft of Y was not written.");

        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The unanswered draft of X in the main slot was written over.");
        Assert.AreEqual("Paper Y", new RecoveryDraftStore(WaitingFolder(s_y)).Read()?.Manuscript.Title,
            "The draft of Y is not in Y's waiting slot, data\\recovery\\waiting-<id>\\recovery\\manuscript-page-draft.json.");
        Assert.AreEqual("Paper X", drafts.PendingDraftFor(s_x)?.Manuscript.Title, "The draft of X stopped waiting.");
    }

    // Rule 2: a session's second draft of a manuscript replaces its first,
    // in the main slot and in a waiting slot.
    [TestMethod]
    public void ADraftForTheSameManuscriptReplacesItsOwn()
    {
        RecoveryDrafts first = Session(s_x, s_y);
        Assert.IsTrue(first.WriteFrom(new TestDraftPage(s_x, "First", s_first), RecoveryReasons.Error));
        Assert.IsTrue(first.WriteFrom(new TestDraftPage(s_x, "Second", s_second), RecoveryReasons.Shutdown));
        Assert.AreEqual("Second", new RecoveryDraftStore(_data).Read()?.Manuscript.Title, "The main slot does not hold the session's newer draft.");
        Assert.IsEmpty(WaitingFolders(), "A second draft of the same manuscript took a waiting slot.");
        first.DeleteThisSessionsDraft();

        Keep(_data, s_y, "Paper Y", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts next = Session(s_x, s_y);
        Assert.IsTrue(next.WriteFrom(new TestDraftPage(s_x, "Third", s_second), RecoveryReasons.Error));
        Assert.IsTrue(next.WriteFrom(new TestDraftPage(s_x, "Fourth", s_third), RecoveryReasons.Shutdown));
        Assert.AreEqual("Fourth", new RecoveryDraftStore(WaitingFolder(s_x)).Read()?.Manuscript.Title, "X's waiting slot does not hold the session's newer draft.");
        CollectionAssert.AreEqual(new[] { WaitingFolder(s_x) }, WaitingFolders(), "The second draft of X took another slot.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "Y's unanswered draft in the main slot was written over.");
    }

    // Rules 2 and 4: an earlier session's unanswered draft of a manuscript in
    // the main slot is not written over by this session's draft of the same
    // manuscript (the page has not opened it, so the new draft does not hold
    // its changes); the new draft goes to the waiting slot, and after a crash
    // the newer is offered first.
    [TestMethod]
    public void AnEarlierDraftOfTheSameManuscriptIsNotWrittenOver()
    {
        Keep(_data, s_x, "Old X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x);

        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_x, "New X", s_second), RecoveryReasons.Shutdown), "The draft of X was not written.");

        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The earlier session's unanswered draft of X was written over.");
        Assert.AreEqual("New X", new RecoveryDraftStore(WaitingFolder(s_x)).Read()?.Manuscript.Title, "The new draft of X is not in X's waiting slot.");
        RecoveryDrafts next = Session(s_x);
        Assert.AreEqual("New X", next.PendingDraftFor(s_x)?.Manuscript.Title, "After a crash the newer draft of X is not the pending one.");
        next.Adopt(s_x);
        Assert.AreEqual("Old X", next.PendingDraftFor(s_x)?.Manuscript.Title, "Once the newer was opened, the older should be pending.");
    }

    // Rule 2: a page's draft is never refused. When the main slot holds
    // another manuscript's unanswered draft and the manuscript's waiting slot
    // is taken too (by a draft whose Discard was undone, or by a file that
    // cannot be read), the draft goes to the manuscript's next waiting slot,
    // nothing is written over, and after a crash the newest is offered first.
    [TestMethod]
    [DataRow(false, DisplayName = "after a Discard is undone")]
    [DataRow(true, DisplayName = "beside an unreadable waiting slot")]
    public void ADraftMeetingTwoTakenSlotsGoesToTheNextWaitingSlot(bool unreadable)
    {
        Keep(_data, s_y, "Paper Y", s_first);
        if (unreadable)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WaitingFile(s_x))!);
            File.WriteAllText(WaitingFile(s_x), "{ not a draft");
        }
        else
        {
            Keep(WaitingFolder(s_x), s_x, "Old X", s_second);
        }

        byte[] mainBefore = File.ReadAllBytes(MainFile);
        byte[] waitingBefore = File.ReadAllBytes(WaitingFile(s_x));
        RecoveryDrafts drafts = Session(s_x, s_y);
        if (!unreadable)
        {
            // The person discards X's offer, opens X (nothing is pending, so
            // nothing is adopted), edits, and presses Undo on the notice.
            Assert.IsTrue(drafts.Discard(s_x));
            Assert.IsNull(drafts.PendingDraftFor(s_x), "A discarded draft is still pending.");
            Assert.IsTrue(drafts.Undo(s_x), "Undo found no discarded draft.");
        }

        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_x, "Current X", s_third), RecoveryReasons.Shutdown),
            "The page's draft of X was refused, so its unsaved work was lost.");

        CollectionAssert.AreEqual(mainBefore, File.ReadAllBytes(MainFile), "Y's unanswered draft in the main slot was written over.");
        CollectionAssert.AreEqual(waitingBefore, File.ReadAllBytes(WaitingFile(s_x)), "X's taken waiting slot was written over.");
        Assert.AreEqual("Current X", new RecoveryDraftStore(WaitingFolder(s_x, 2)).Read()?.Manuscript.Title,
            "The draft of X is not in X's next waiting slot, data\\recovery\\waiting-<id>-2\\recovery\\manuscript-page-draft.json.");
        RecoveryDrafts next = Session(s_x, s_y);
        Assert.AreEqual("Current X", next.PendingDraftFor(s_x)?.Manuscript.Title, "After a crash the newest draft of X is not the pending one.");
        CollectionAssert.AreEqual(new[] { "Current X", "Paper Y" }, next.Offers.Select(offer => offer.Title).ToArray(),
            "After a crash each manuscript's newest draft should be offered, the newest first.");
        next.Adopt(s_x);
        Assert.AreEqual(unreadable ? null : "Old X", next.PendingDraftFor(s_x)?.Manuscript.Title,
            "Once the newest was opened, the older draft of X should be pending, and only a readable one.");
    }

    // Rules 2 and 8 (S14): with the newer draft of a manuscript opened and the
    // older discarded, the page's next draft replaces the opened one, wherever
    // the two are, and the discarded one keeps its file, so Undo still works.
    [TestMethod]
    [DataRow(true, DisplayName = "the opened one in the main slot")]
    [DataRow(false, DisplayName = "the opened one in the waiting slot")]
    public void AWriteReplacesTheOpenedDraftAndKeepsTheDiscardedOne(bool newerInMain)
    {
        Keep(_data, s_x, "Main copy", newerInMain ? s_second : s_first);
        Keep(WaitingFolder(s_x), s_x, "Waiting copy", newerInMain ? s_first : s_second);
        string older = newerInMain ? "Waiting copy" : "Main copy";
        string olderFile = newerInMain ? WaitingFile(s_x) : MainFile;
        string newerFolder = newerInMain ? _data : WaitingFolder(s_x);
        byte[] olderBefore = File.ReadAllBytes(olderFile);
        RecoveryDrafts drafts = Session(s_x);
        drafts.Adopt(s_x);
        Assert.IsTrue(drafts.Discard(s_x), "Discard found no older draft.");

        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_x, "Current X", s_third), RecoveryReasons.Shutdown), "The draft of X was not written.");

        CollectionAssert.AreEqual(olderBefore, File.ReadAllBytes(olderFile), "The page's draft was written over the discarded one.");
        Assert.AreEqual("Current X", new RecoveryDraftStore(newerFolder).Read()?.Manuscript.Title, "The page's draft did not replace the opened one.");
        Assert.IsTrue(drafts.Undo(s_x), "Undo no longer finds the discarded draft.");
        Assert.AreEqual(older, drafts.PendingDraftFor(s_x)?.Manuscript.Title, "After Undo the older draft is not pending again.");
    }

    // Rule 2: drafts left in waiting slots by two sessions that crashed are
    // offered with the main slot's, the newest first.
    [TestMethod]
    public void BothWaitingDraftsAreOffered()
    {
        Keep(_data, s_x, "Paper X", s_first);
        Assert.IsTrue(Session(s_x, s_y, s_z).WriteFrom(new TestDraftPage(s_y, "Paper Y", s_second), RecoveryReasons.Shutdown));
        Assert.IsTrue(Session(s_x, s_y, s_z).WriteFrom(new TestDraftPage(s_z, "Paper Z", s_third), RecoveryReasons.Shutdown));

        RecoveryDrafts next = Session(s_x, s_y, s_z);

        CollectionAssert.AreEqual(new[] { WaitingFolder(s_y), WaitingFolder(s_z) }, WaitingFolders(), "Each manuscript should have its own waiting slot.");
        CollectionAssert.AreEqual(new[] { "Paper Z", "Paper Y", "Paper X" }, next.Offers.Select(offer => offer.Title).ToArray(),
            "Every kept draft should be offered, the newest first.");
    }

    // The main slot is 1.0.x's file: 1.0.0's and 1.0.1's drafts read and are
    // offered unchanged, and a draft written into an empty main slot lands at
    // 1.0.x's path in the shape 1.0.0 wrote. Each fixture is a whole
    // manuscript as 1.0.x wrote it, calendar dates without an offset (#118),
    // and every one of its dates reads as the same day on every runner; so
    // does each date given the offset System.Text.Json writes for a local
    // time (+14:00 here), which the core's store takes off on the way in.
    [TestMethod]
    [DataRow(V100Fixture, "4f1c2b7a-3d5e-4a61-9b8c-0d2e7f6a1b01", "Edited under v1.0.0", false, DisplayName = "1.0.0's draft")]
    [DataRow(V101Fixture, "8a3e5d21-6c4b-4f70-a2d9-1e5b7c3f9d02", "Edited under v1.0.1", true, DisplayName = "1.0.1's draft")]
    public void TheMainSlotKeepsTheOneZeroBytes(string fixture, string manuscript, string title, bool withLibrary)
    {
        Guid id = Guid.Parse(manuscript);
        string written = File.ReadAllText(FixturePath(fixture));
        CollectionAssert.AreEqual(withLibrary ? s_keysOf101 : s_keysOf100, KeysOf(written),
            $"{fixture} does not have the keys its version writes.");
        byte[] before = PlaceFixture(fixture, _data);
        RecoveryDraft? read = new RecoveryDraftStore(_data).Read();
        Assert.AreEqual(id, read?.ManuscriptId, $"The core's store did not read {fixture}.");
        Assert.AreEqual(withLibrary, read?.UnsavedLibrary is not null, $"{fixture} read with the wrong library records.");
        AssertTheCalendarDays(written, read, fixture);

        Assert.HasCount(8, CalendarDate().Matches(written), $"{fixture} should hold eight calendar dates.");
        string offsetFolder = Path.Combine(_root, "offset");
        Directory.CreateDirectory(Path.Combine(offsetFolder, "recovery"));
        File.WriteAllText(Path.Combine(offsetFolder, "recovery", RecoveryDraftStore.FileName), CalendarDate().Replace(written, "${value}+14:00\""));
        AssertTheCalendarDays(written, new RecoveryDraftStore(offsetFolder).Read(), fixture + " with +14:00 on its calendar dates");

        RecoveryDrafts drafts = Session(id);
        Assert.AreEqual(title, drafts.Offers.SingleOrDefault()?.Title, $"{fixture} in the main slot was not offered.");
        Assert.AreEqual(title, drafts.PendingDraftFor(id)?.Manuscript.Title, $"{fixture} in the main slot is not waiting.");
        drafts.DeleteThisSessionsDraft();
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), $"{fixture} changed in the main slot.");

        File.Delete(MainFile);
        Assert.IsTrue(Session(s_y).WriteFrom(new TestDraftPage(s_y, "Written now", s_second), RecoveryReasons.Error),
            "No draft was written into the empty main slot.");
        CollectionAssert.AreEqual(s_keysOf100, KeysOf(File.ReadAllText(MainFile)),
            "A draft with no library records no longer has 1.0.0's four keys at 1.0.x's path.");
        Assert.AreEqual(s_y, new RecoveryDraftStore(_data).Read()?.ManuscriptId, "The core's store does not read the draft written into the main slot.");
    }

    // S14: two drafts of one manuscript, in either slot. The newer is offered
    // first; the older stays untouched and is offered once the newer is
    // answered.
    [TestMethod]
    [DataRow(true, DisplayName = "the newer in the main slot")]
    [DataRow(false, DisplayName = "the newer in the waiting slot")]
    public void TwoDraftsOfOneManuscriptOfferTheNewerFirstAndKeepTheOlder(bool newerInMain)
    {
        Keep(_data, s_x, "Main copy", newerInMain ? s_third : s_first);
        Keep(WaitingFolder(s_x), s_x, "Waiting copy", newerInMain ? s_first : s_third);
        string newer = newerInMain ? "Main copy" : "Waiting copy";
        string older = newerInMain ? "Waiting copy" : "Main copy";
        string olderFile = newerInMain ? WaitingFile(s_x) : MainFile;
        byte[] olderBefore = File.ReadAllBytes(olderFile);
        RecoveryDrafts drafts = Session(s_x);

        Assert.AreEqual(newer, drafts.Offers.SingleOrDefault()?.Title, "The newer draft should be the one offered first.");
        Assert.AreEqual(newer, drafts.PendingDraftFor(s_x)?.Manuscript.Title, "The newer draft should be the pending one.");

        drafts.Adopt(s_x);
        Assert.AreEqual(older, drafts.Offers.SingleOrDefault()?.Title, "Once the newer was opened, the older should be offered.");
        Assert.AreEqual(older, drafts.PendingDraftFor(s_x)?.Manuscript.Title, "Once the newer was opened, the older should be pending.");
        CollectionAssert.AreEqual(olderBefore, File.ReadAllBytes(olderFile), "The older draft changed before anyone answered it.");

        Assert.IsTrue(drafts.Discard(s_x));
        Assert.IsTrue(File.Exists(MainFile) && File.Exists(WaitingFile(s_x)), "A draft was deleted before PaperRoute closed.");
    }

    // Rules 3 and 8: asking does not take the draft; adopting or discarding
    // does.
    [TestMethod]
    [DataRow(true, DisplayName = "adopted")]
    [DataRow(false, DisplayName = "discarded")]
    public void PendingDraftForReturnsItUntilAdoptedOrDiscarded(bool adopt)
    {
        Keep(_data, s_x, "Paper X", s_first);
        RecoveryDrafts drafts = Session(s_x);

        Assert.AreEqual("Paper X", drafts.PendingDraftFor(s_x)?.Manuscript.Title, "The kept draft is not pending.");
        Assert.AreEqual("Paper X", drafts.PendingDraftFor(s_x)?.Manuscript.Title, "Asking a second time found no draft: asking took it.");
        if (adopt)
        {
            drafts.Adopt(s_x);
        }
        else
        {
            Assert.IsTrue(drafts.Discard(s_x), "Discard found no draft.");
        }

        Assert.IsNull(drafts.PendingDraftFor(s_x), "The draft is still pending after it was answered.");
        Assert.IsEmpty(drafts.Offers, "The draft is still offered after it was answered.");
    }

    // Rule 3: opening keeps the file, so a crash between Open and Save loses
    // nothing; the save deletes it.
    [TestMethod]
    public void AdoptKeepsTheFileUntilSave()
    {
        Keep(_data, s_x, "Paper X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x);

        drafts.Adopt(s_x);

        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "Opening the draft changed or deleted its file.");
        Assert.AreEqual("Paper X", Session(s_x).PendingDraftFor(s_x)?.Manuscript.Title, "After a crash between Open and Save the draft was gone.");
        drafts.PageSaved(s_x);
        Assert.IsFalse(File.Exists(MainFile), "The save did not delete the opened draft.");
    }

    // Rule 3: the page's save or discard deletes the draft it opened.
    [TestMethod]
    [DataRow(true, DisplayName = "the page saves")]
    [DataRow(false, DisplayName = "the page discards")]
    public void SaveDeletesAnAdoptedDraft(bool save)
    {
        Keep(WaitingFolder(s_x), s_x, "Paper X", s_first);
        RecoveryDrafts drafts = Session(s_x);
        drafts.Adopt(s_x);

        if (save)
        {
            drafts.PageSaved(s_x);
        }
        else
        {
            drafts.PageDiscarded(s_x);
        }

        Assert.IsFalse(File.Exists(WaitingFile(s_x)), "The opened draft's file is still there.");
        Assert.IsEmpty(Session(s_x).Offers, "The next start offers a draft that was saved or discarded.");
    }

    // Rule 8: a discarded draft goes at a normal close.
    [TestMethod]
    public void DiscardDeletesItAtClose()
    {
        Keep(_data, s_x, "Paper X", s_first);
        RecoveryDrafts drafts = Session(s_x);

        Assert.IsTrue(drafts.Discard(s_x));
        Assert.IsTrue(File.Exists(MainFile), "Discard deleted the file at once, so it cannot be undone.");
        drafts.DeleteThisSessionsDraft();

        Assert.IsFalse(File.Exists(MainFile), "The discarded draft is still there after a normal close.");
        Assert.IsEmpty(Session(s_x).Offers, "The next start offers a draft that was discarded and closed away.");
    }

    // S9: Discard can be undone until PaperRoute closes, even after the page
    // saved or discarded that manuscript without opening the draft.
    [TestMethod]
    public void DiscardCanBeUndoneUntilClose()
    {
        Keep(_data, s_x, "Paper X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x);

        Assert.IsTrue(drafts.Discard(s_x));
        Assert.IsEmpty(drafts.Offers, "A discarded draft is still offered.");
        Assert.IsNull(drafts.PendingDraftFor(s_x), "A discarded draft is still pending.");
        drafts.PageSaved(s_x);
        drafts.PageDiscarded(s_x);
        Assert.IsTrue(File.Exists(MainFile), "The page's save or discard deleted the discarded draft, so it cannot be undone.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The page's save or discard changed the discarded draft.");
        Assert.IsTrue(drafts.Undo(s_x), "Undo found no discarded draft.");
        Assert.AreEqual("Paper X", drafts.Offers.SingleOrDefault()?.Title, "After Undo the draft is not offered again.");
        Assert.AreEqual("Paper X", drafts.PendingDraftFor(s_x)?.Manuscript.Title, "After Undo the draft is not pending again.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "Discard and Undo changed the file.");

        Assert.IsTrue(drafts.Discard(s_x));
        drafts.DeleteThisSessionsDraft();
        Assert.IsFalse(drafts.Undo(s_x), "Undo worked after PaperRoute closed.");
    }

    // S9: a session that discarded a draft and then crashed leaves it for the
    // next start (the safe direction).
    [TestMethod]
    public void ADiscardedDraftIsOfferedAgainAfterACrash()
    {
        Keep(_data, s_x, "Paper X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        Assert.IsTrue(Session(s_x).Discard(s_x));

        RecoveryDrafts next = Session(s_x);

        Assert.AreEqual("Paper X", next.Offers.SingleOrDefault()?.Title, "After a crash the discarded draft is not offered again.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The discarded draft changed.");
    }

    // S14: the close deletes every file the session owns (two adopted, one
    // discarded), and the one draft nobody answered stays.
    [TestMethod]
    public void DeleteThisSessionsDraftDeletesEveryFileThisSessionOwns()
    {
        Keep(_data, s_x, "Paper X", s_first);
        Keep(WaitingFolder(s_y), s_y, "Paper Y", s_second);
        Keep(WaitingFolder(s_z), s_z, "Paper Z", s_third);
        Keep(WaitingFolder(s_w), s_w, "Paper W", s_fourth);
        byte[] unanswered = File.ReadAllBytes(WaitingFile(s_w));
        RecoveryDrafts drafts = Session(s_x, s_y, s_z, s_w);
        drafts.Adopt(s_x);
        drafts.Adopt(s_y);
        Assert.IsTrue(drafts.Discard(s_z));

        drafts.DeleteThisSessionsDraft();

        string[] left = [.. new[] { MainFile, WaitingFile(s_y), WaitingFile(s_z) }.Where(File.Exists)];
        Assert.IsEmpty(left, "The close left files this session owned: " + string.Join(", ", left.Select(Path.GetFileName)));
        CollectionAssert.AreEqual(unanswered, File.ReadAllBytes(WaitingFile(s_w)), "The close changed a draft nobody answered.");
    }

    // Rule 4 (the frame's R-5): a save of the manuscript does not delete a
    // draft an earlier session kept; nor does the page's discard or the close.
    [TestMethod]
    public void ADraftAnEarlierSessionWroteIsNotDeletedByThisSessionsSave()
    {
        Keep(_data, s_x, "Paper X", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x);

        drafts.PageSaved(s_x);
        drafts.PageDiscarded(s_x);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "This session's save or discard changed an earlier session's draft.");
        Assert.AreEqual("Paper X", drafts.PendingDraftFor(s_x)?.Manuscript.Title, "The earlier session's draft stopped waiting.");

        drafts.DeleteThisSessionsDraft();
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The close deleted an earlier session's draft.");
    }

    // Rule 4 (the frame's R-10): saving one manuscript keeps another's waiting
    // draft, unchanged and offered.
    [TestMethod]
    public void SavingAnotherManuscriptKeepsThePendingDraft()
    {
        Keep(_data, s_x, "Paper X", s_first);
        Keep(WaitingFolder(s_y), s_y, "Paper Y", s_second);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x, s_y);
        drafts.Adopt(s_y);

        drafts.PageSaved(s_y);

        Assert.IsFalse(File.Exists(WaitingFile(s_y)), "The save of Y did not delete Y's opened draft.");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The save of Y changed X's draft.");
        DraftOffer? offer = drafts.Offers.SingleOrDefault();
        Assert.AreEqual(s_x, offer?.ManuscriptId, "X's draft is no longer offered.");
        Assert.IsNull(offer?.SavedAfter, "X's offer says X was saved, but only Y was.");
    }

    // Rule 5: a draft whose manuscript is not in the library.
    [TestMethod]
    public void ADraftForAMissingManuscriptIsKeptAndNotOffered()
    {
        Keep(_data, s_z, "Gone", s_first);
        byte[] before = File.ReadAllBytes(MainFile);
        RecoveryDrafts drafts = Session(s_x);

        Assert.IsEmpty(drafts.Offers, "A draft for a manuscript not in the library is offered.");
        Assert.IsNull(drafts.PendingDraftFor(s_z), "A draft for a manuscript not in the library is pending.");
        drafts.Adopt(s_z);
        Assert.IsFalse(drafts.Discard(s_z), "A draft for a manuscript not in the library could be discarded.");
        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_x, "Paper X", s_second), RecoveryReasons.Shutdown), "The draft of X was not written.");
        Assert.IsTrue(File.Exists(WaitingFile(s_x)), "The draft of X did not go to a waiting slot.");
        drafts.DeleteThisSessionsDraft();

        CollectionAssert.AreEqual(before, File.ReadAllBytes(MainFile), "The draft for a manuscript not in the library changed or went.");
        Assert.IsFalse(File.Exists(WaitingFile(s_x)), "The close kept this session's own draft.");
    }

    // Rule 6: saving a manuscript without opening its waiting draft keeps the
    // draft and its offer, which now says the draft is older than the save.
    [TestMethod]
    public void ASaveWithoutOpeningKeepsTheDraftAndSaysItIsOlder()
    {
        Keep(_data, s_x, "Paper X", s_first);
        RecoveryDrafts drafts = Session(s_x);
        Assert.IsNull(drafts.Offers.Single().SavedAfter, "The offer says a save came after the draft before any save.");

        drafts.PageSaved(s_x);

        Assert.IsTrue(File.Exists(MainFile), "A save without opening deleted the draft.");
        Assert.AreEqual(
            "You saved 'Paper X' after these changes were kept; opening them shows the older text as unsaved changes.",
            drafts.Offers.SingleOrDefault()?.SavedAfter,
            "The offer does not say that the draft is older than the save.");
    }

    // Rule 7: a write that fails, or a page that throws, gives false and a
    // log line, never an exception.
    [TestMethod]
    public void AFailedDraftWriteReturnsFalseAndThrowsNothing()
    {
        File.WriteAllText(Path.Combine(_data, "recovery"), "a file where the recovery folder goes");
        RecoveryDrafts drafts = Session(s_x);
        var log = new LogCapture();
        Trace.Listeners.Add(log);
        try
        {
            Assert.IsFalse(drafts.WriteFrom(new TestDraftPage(s_x, "Paper X", s_first), RecoveryReasons.Shutdown),
                "A draft that could not be written gave true.");
            Assert.IsFalse(drafts.WriteFrom(new TestDraftPage(s_x, "Paper X", s_first) { Throws = new InvalidOperationException("probe") }, RecoveryReasons.Error),
                "A page that threw gave true.");
            Assert.IsFalse(drafts.WriteFrom(null, RecoveryReasons.Shutdown), "No page gave true.");
        }
        finally
        {
            Trace.Listeners.Remove(log);
        }

        Assert.HasCount(2, log.Lines.Where(line => line.Contains("Recovery drafts: ", StringComparison.Ordinal)),
            "Each failure should be logged once: " + string.Join(" | ", log.Lines));
    }

    // Only RecoveryDrafts names the core's draft store, in either project's
    // sources (lifted-bans.txt gives it the ban; EachLiftedBanHasOneOwner
    // checks the pragma). The scan's own check: a probe file that names it
    // is found.
    [TestMethod]
    public void OnlyRecoveryDraftsNamesTheStore()
    {
        (string File, string[] Lines)[] probe = [("PaperRoute.Desktop/Probe.cs", ["        var kept = new RecoveryDraftStore(folder).Read();"])];
        CollectionAssert.AreEqual(new[] { "PaperRoute.Desktop/Probe.cs" }, FilesNamingTheStore(probe).ToArray(), "The scan missed a file that names the store.");

        string[] naming = [.. FilesNamingTheStore(Sources())];
        CollectionAssert.AreEqual(new[] { Owner }, naming,
            "Only " + Owner + " may name RecoveryDraftStore (start-up design 2.5); found in: " + string.Join(", ", naming));

        string list = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CoreSurface", "lifted-bans.txt"));
        StringAssert.Contains(list, "T:ManuscriptPipeline.Services.RecoveryDraftStore;" + Owner + ";", "lifted-bans.txt does not give the store's ban to RecoveryDrafts.");
    }

    // The storage guard for the draft fixtures: both are run through every
    // step of RecoveryDrafts in this test's folder, and no default storage
    // root answers, nor does a draft file appear under the run's redirected
    // root (where the person's own data folder would be).
    [TestMethod]
    public void EveryFixtureLeavesNoDefaultRootUse()
    {
        PlaceFixture(V100Fixture, _data);
        PlaceFixture(V101Fixture, WaitingFolder(s_v101));
        RecoveryDrafts drafts = Session(s_v100, s_v101, s_x);

        Assert.HasCount(2, drafts.Offers, "Both fixtures should be offered.");
        drafts.Adopt(s_v100);
        Assert.IsTrue(drafts.Discard(s_v101));
        Assert.IsTrue(drafts.Undo(s_v101));
        drafts.Adopt(s_v101);
        Assert.IsTrue(drafts.WriteFrom(new TestDraftPage(s_x, "Paper X", s_third), RecoveryReasons.Shutdown));
        drafts.PageSaved(s_v100);
        drafts.PageDiscarded(s_v101);
        drafts.DeleteThisSessionsDraft();

        Assert.AreEqual(0, StorageEnvironment.DefaultRootUses, "A default storage root answered while the draft fixtures ran.");
        string[] strays = [.. Directory.EnumerateFiles(AssemblySetup.StorageRoot, RecoveryDraftStore.FileName, SearchOption.AllDirectories)];
        Assert.IsEmpty(strays, "A draft file was written under the run's storage root instead of the test's folder.");
        Assert.IsEmpty(Directory.GetFiles(_data, "*", SearchOption.AllDirectories), "A draft file was left in the test's folder.");
    }

    // A session at start, with a page that can open drafts.
    private RecoveryDrafts Session(params Guid[] library)
    {
        var drafts = new RecoveryDrafts(_data);
        drafts.ReadAtStart(library);
        drafts.OpenWith(_ => Task.FromResult(true));
        return drafts;
    }

    // A draft an earlier session kept in the slot whose folder is given.
    private static void Keep(string folder, Guid id, string title, DateTime writtenAtUtc) =>
        new RecoveryDraftStore(folder).Write(TestDraftPage.Draft(id, title, writtenAtUtc));

    // A manuscript's n-th waiting slot: waiting-<id>, then waiting-<id>-2 and
    // so on.
    private string WaitingFolder(Guid id, int n = 1) =>
        Path.Combine(_data, "recovery", RecoveryDrafts.WaitingSlotPrefix + id.ToString("N") + (n == 1 ? string.Empty : "-" + n.ToString(CultureInfo.InvariantCulture)));

    private string WaitingFile(Guid id, int n = 1) => Path.Combine(WaitingFolder(id, n), "recovery", RecoveryDraftStore.FileName);

    private string[] WaitingFolders()
    {
        string recovery = Path.Combine(_data, "recovery");
        return Directory.Exists(recovery) ? [.. Directory.GetDirectories(recovery).Order(StringComparer.Ordinal)] : [];
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recovery", name);

    // Copies a fixture into the slot whose folder is given; returns its bytes.
    private static byte[] PlaceFixture(string name, string folder)
    {
        string target = Path.Combine(folder, "recovery", RecoveryDraftStore.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(FixturePath(name), target);
        return File.ReadAllBytes(target);
    }

    private static string[] KeysOf(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(property => property.Name)];
    }

    // Each calendar date of a fixture's manuscript reads as the day the
    // fixture's text names (#118).
    private static void AssertTheCalendarDays(string fixtureJson, RecoveryDraft? draft, string what)
    {
        Assert.IsNotNull(draft, $"The core's store did not read {what}.");
        using JsonDocument document = JsonDocument.Parse(fixtureJson);
        JsonElement manuscript = document.RootElement.GetProperty("Manuscript");
        JsonElement submission = manuscript.GetProperty("Submissions")[0];
        JsonElement decision = submission.GetProperty("Decisions")[0];
        var read = draft.Manuscript;
        var readSubmission = read.Submissions.SingleOrDefault();
        var readDecision = readSubmission?.Decisions.SingleOrDefault();
        (string Name, JsonElement Written, DateTime? Read)[] dates =
        [
            ("StageEnteredDate", manuscript.GetProperty("StageEnteredDate"), read.StageEnteredDate),
            ("RevisionDeadline", manuscript.GetProperty("RevisionDeadline"), read.RevisionDeadline),
            ("a version's CreatedDate", manuscript.GetProperty("Versions")[0].GetProperty("CreatedDate"), read.Versions.SingleOrDefault()?.CreatedDate),
            ("a history event's EventDate", manuscript.GetProperty("History")[0].GetProperty("EventDate"), read.History.SingleOrDefault()?.EventDate),
            ("a submission's SubmittedDate", submission.GetProperty("SubmittedDate"), readSubmission?.SubmittedDate),
            ("a submission's FollowUpDate", submission.GetProperty("FollowUpDate"), readSubmission?.FollowUpDate),
            ("a decision's DecisionDate", decision.GetProperty("DecisionDate"), readDecision?.DecisionDate),
            ("a decision's RevisionDeadline", decision.GetProperty("RevisionDeadline"), readDecision?.RevisionDeadline),
        ];
        foreach ((string name, JsonElement writtenDate, DateTime? readDate) in dates)
        {
            Assert.AreEqual(writtenDate.GetString()![..10], readDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{what}: {name} read as another day.");
        }
    }

    // Both projects' sources as the test output holds them, by repository path.
    private static IEnumerable<(string File, string[] Lines)> Sources()
    {
        foreach ((string project, string folder) in new[] { ("PaperRoute.Desktop/", "Source"), ("PaperRoute.ViewModels/", "ViewModelSources") })
        {
            string root = Path.Combine(AppContext.BaseDirectory, folder);
            Assert.IsTrue(Directory.Exists(root), $"{root} was not copied to the test output.");
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                yield return (project + Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllLines(path));
            }
        }
    }

    private static IEnumerable<string> FilesNamingTheStore(IEnumerable<(string File, string[] Lines)> files) =>
        files.Where(file => file.Lines.Any(line => StoreName().IsMatch(line))).Select(file => file.File);

    [GeneratedRegex(@"\bRecoveryDraftStore\b", RegexOptions.CultureInvariant)]
    private static partial Regex StoreName();

    // A calendar date in a draft's text: a date and time with no offset and
    // no Z, under any name but a ...Utc stamp.
    [GeneratedRegex(@"(?<value>""[A-Za-z]+(?<!Utc)""\s*:\s*""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?)""", RegexOptions.CultureInvariant)]
    private static partial Regex CalendarDate();

    private sealed class LogCapture : TraceListener
    {
        private readonly StringBuilder _partial = new();

        public List<string> Lines { get; } = [];

        public override void Write(string? message) => _partial.Append(message);

        public override void WriteLine(string? message)
        {
            _partial.Append(message);
            Lines.Add(_partial.ToString());
            _partial.Clear();
        }
    }
}
