using System.Text.Json;
using ManuscriptPipeline;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// What Save writes and what follows a save (build 9's design, 2.2 and 2.5;
// tests C-1 to C-9 and C-11 to C-13; C-10 is CommitTests). There is no store
// yet (F1a brings it): where a test saves, it plays the store's part on
// success, ManuscriptCommit.CopyInto then WorkingCopy.AcceptSaved, as the
// session's step 5 and the frame's step 9 do. Every record is built in
// memory; nothing is written.
[TestClass]
public sealed class CandidateTests : Board.StorageCheckedTests
{
    private static readonly DateTime Day = new(2026, 3, 2);
    private static readonly DateTime SaveTime = new(2026, 4, 6, 14, 30, 0);
    private static readonly DateTimeOffset FixedInstant = new(2031, 7, 4, 9, 15, 0, TimeSpan.Zero);
    private static readonly Guid Grace = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000002");
    private static readonly Guid TestsJournal = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000007");

    // C-1
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ABlankTitleIsRefusedAndNothingChanges(string title)
    {
        WorkingCopy copy = Loaded(SubmittablePaper());
        copy.Edit(working => working.Title = title);
        copy.HoldStage(PaperStage.Submitted);
        Manuscript working = copy.Working;
        string before = copy.Snapshot();

        SaveAttempt attempt = copy.BuildCandidate(SaveTime);

        Assert.AreEqual(SaveRefusal.TitleRequired, attempt.Refusal, "A blank title did not stop the save.");
        Assert.IsFalse(attempt.IsReady);
        Assert.IsNull(attempt.Candidate, "A refused save still built something to write.");
        Assert.AreSame(working, copy.Working);
        Assert.AreEqual(before, copy.Snapshot(), "A refused save changed the working copy.");
        Assert.AreEqual(PaperStage.Submitted, copy.HeldStage, "A refused save dropped the held stage.");
        Assert.IsTrue(copy.HasUnsavedChanges());
    }

    // C-2
    [TestMethod]
    public void ASupportedHeldStageIsAppliedWithOneStampedEvent()
    {
        using var clock = new FixedClock(FixedInstant);
        WorkingCopy copy = Loaded(SubmittablePaper());
        Assert.AreEqual(PaperStage.Draft, copy.Working.CurrentStage, "The load left the stage as stored.");
        int historyBefore = copy.Working.History.Count;
        copy.HoldStage(PaperStage.Submitted);

        Manuscript candidate = Ready(copy.BuildCandidate(CoreClock.Now));

        Assert.AreEqual(PaperStage.Submitted, candidate.CurrentStage, "The held stage was not applied.");
        Assert.AreEqual(clock.Local, candidate.StageEnteredDate);
        Assert.HasCount(historyBefore + 1, candidate.History, "One event, and only one, is added.");
        HistoryEvent added = candidate.History[^1];
        Assert.AreEqual("Stage changed from Draft to Submitted.", added.Note);
        Assert.AreEqual(PaperStage.Submitted, added.Stage);
        Assert.AreEqual(clock.Local, added.EventDate);
        Assert.AreEqual((DateTime?)FixedInstant.UtcDateTime, added.RecordedAtUtc, "The event is not stamped as created.");
        Assert.IsNull(added.LastModifiedAtUtc);
    }

    // C-3
    [TestMethod]
    [DataRow(PaperStage.Submitted, "an active Journal Submission record")]
    [DataRow(PaperStage.UnderReview, "an active Journal Submission record")]
    [DataRow(PaperStage.Revision, "a revision editorial decision")]
    [DataRow(PaperStage.Accepted, "an accepted editorial decision")]
    [DataRow(PaperStage.InPress, "an accepted editorial decision")]
    [DataRow(PaperStage.Published, "an accepted editorial decision")]
    public void AnUnsupportedHeldStageStopsAndKeepsTheStage(PaperStage stage, string requirement)
    {
        WorkingCopy copy = Loaded(Paper());
        copy.HoldStage(stage);
        string before = copy.Snapshot();

        SaveAttempt attempt = copy.BuildCandidate(SaveTime);

        Assert.AreEqual(SaveRefusal.NeedsStep, attempt.Refusal, $"{stage} without its submission or decision did not stop the save.");
        Assert.AreEqual(requirement, attempt.Requirement);
        Assert.IsNull(attempt.Candidate);
        Assert.AreEqual(stage, copy.HeldStage, "The stage was not kept held.");
        Assert.AreEqual(before, copy.Snapshot(), "A stopped save changed the working copy.");
        Assert.AreEqual(PaperStage.Draft, copy.Working.CurrentStage);
    }

    // C-4
    [TestMethod]
    public void PublishedMovesTheLocationAndLeavingItMovesItBack()
    {
        WorkingCopy accepted = Loaded(AcceptedPaper(PaperStage.Accepted, ManuscriptLocation.Pipeline));
        accepted.HoldStage(PaperStage.Published);
        Manuscript published = Ready(accepted.BuildCandidate(SaveTime));
        Assert.AreEqual(PaperStage.Published, published.CurrentStage);
        Assert.AreEqual(ManuscriptLocation.Published, published.Location, "Published did not move the manuscript to Published.");

        WorkingCopy wasPublished = Loaded(AcceptedPaper(PaperStage.Published, ManuscriptLocation.Published));
        wasPublished.HoldStage(PaperStage.Accepted);
        Manuscript back = Ready(wasPublished.BuildCandidate(SaveTime));
        Assert.AreEqual(PaperStage.Accepted, back.CurrentStage);
        Assert.AreEqual(ManuscriptLocation.Pipeline, back.Location, "Leaving Published did not move the manuscript back to the Pipeline.");

        WorkingCopy filed = Loaded(FiledPaper());
        Assert.AreEqual(ManuscriptLocation.FileDrawer, Ready(filed.BuildCandidate(SaveTime)).Location, "A stage that is not Published moved a filed manuscript.");
    }

    // C-5
    [TestMethod]
    public void TitleAndJournalAreTrimmed()
    {
        Manuscript original = Paper("  A Study of Tests  ");
        original.TargetJournal = " Journal of Tests  ";
        WorkingCopy copy = Loaded(original);

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));

        Assert.AreEqual("A Study of Tests", candidate.Title);
        Assert.AreEqual("Journal of Tests", candidate.TargetJournal);
        Assert.AreEqual("  A Study of Tests  ", copy.Working.Title, "Building the candidate trimmed the working copy.");
    }

    // C-6: the text it was linked under, and the linked journal's name in
    // another capitalization.
    [TestMethod]
    [DataRow("J. Tests")]
    [DataRow(" journal of tests ")]
    public void TheJournalLinkIsKeptWhileTheTextIsItsName(string typed)
    {
        WorkingCopy copy = LoadedLinked();
        var box = new TypedText(copy, TypedField.TargetJournal);

        box.Text = typed;

        Assert.AreEqual(TestsJournal, Ready(copy.BuildCandidate(SaveTime)).TargetJournalId, $"The link was dropped for \"{typed}\".");
    }

    // C-6
    [TestMethod]
    public void TheJournalLinkIsDroppedForOtherText()
    {
        WorkingCopy copy = LoadedLinked();
        var box = new TypedText(copy, TypedField.TargetJournal);

        box.Text = "Journal of Other Tests";

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));
        Assert.IsNull(candidate.TargetJournalId, "The link was kept for another journal's name.");
        Assert.AreEqual("Journal of Other Tests", candidate.TargetJournal);
        Assert.AreEqual(TestsJournal, copy.Working.TargetJournalId, "Typing cleared the link in the working copy.");
    }

    // C-6
    [TestMethod]
    public void TypingTheNameBackKeepsTheLink()
    {
        WorkingCopy copy = LoadedLinked();
        var box = new TypedText(copy, TypedField.TargetJournal);

        box.Text = "Journal of Other Tests";
        box.Text = "Journal of Tests";

        Assert.AreEqual(TestsJournal, Ready(copy.BuildCandidate(SaveTime)).TargetJournalId, "Typing the linked journal's name back lost the link.");
    }

    // C-7
    [TestMethod]
    [DataRow("", " Waiting for data ", "File Drawer reason added. Reason: Waiting for data")]
    [DataRow("Waiting for data", "Waiting for reviewers", "File Drawer reason updated. Reason: Waiting for reviewers")]
    [DataRow("Waiting for data", "  ", "File Drawer reason cleared.")]
    public void AReasonAddedUpdatedOrClearedAddsItsEvent(string saved, string typed, string note)
    {
        Manuscript original = FiledPaper();
        original.FileDrawerReason = saved;
        WorkingCopy copy = Loaded(original);
        int historyBefore = copy.Working.History.Count;
        copy.Edit(working => working.FileDrawerReason = typed);

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));

        Assert.HasCount(historyBefore + 1, candidate.History, "One event, and only one, is added.");
        HistoryEvent added = candidate.History[^1];
        Assert.AreEqual(note, added.Note);
        Assert.AreEqual(SaveTime, added.EventDate);
        Assert.AreEqual(candidate.CurrentStage, added.Stage);
        Assert.IsNotNull(added.RecordedAtUtc, "The event is not stamped as created.");
        Assert.AreEqual(typed.Trim(), candidate.FileDrawerReason);
    }

    // C-7
    [TestMethod]
    public void NoReasonEventOutsideTheFileDrawer()
    {
        WorkingCopy copy = Loaded(Paper());
        int historyBefore = copy.Working.History.Count;
        copy.Edit(working => working.FileDrawerReason = "Waiting for data");

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));
        Assert.HasCount(historyBefore, candidate.History, "A reason outside the File Drawer added an event.");

        WorkingCopy filed = Loaded(FiledPaper());
        filed.Edit(working => working.FileDrawerReason = working.FileDrawerReason + "  ");
        Assert.HasCount(historyBefore, Ready(filed.BuildCandidate(SaveTime)).History, "Spaces at the ends of the reason added an event.");
    }

    // C-7 (D16)
    [TestMethod]
    public void ARecoveredReasonGetsItsEvent()
    {
        Manuscript saved = FiledPaper();
        Manuscript drafted = ManuscriptCloneService.CloneManuscript(saved);
        drafted.FileDrawerReason = "The second data set arrived";
        var copy = new WorkingCopy();
        copy.Load(saved, Library(), new RecoveryDraft { ManuscriptId = saved.Id, Manuscript = drafted, Reason = "shutdown" });

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));

        Assert.AreEqual("File Drawer reason updated. Reason: The second data set arrived", candidate.History[^1].Note,
            "A recovered reason, compared with the draft instead of the saved record, got no event.");
    }

    // C-8
    [TestMethod]
    public void BuildingACandidateNeverChangesTheWorkingCopy()
    {
        Manuscript original = FiledPaper();
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(-10)));
        original.Title = "  A Study of Tests  ";
        WorkingCopy copy = Loaded(original);
        copy.Edit(working => working.FileDrawerReason = "  Waiting for reviewers  ");
        copy.HoldStage(PaperStage.Submitted);
        copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" }));
        Manuscript working = copy.Working;
        string before = copy.Snapshot();
        int token = copy.Token;

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));

        Assert.AreNotSame(working, candidate, "The candidate is the working copy itself.");
        Assert.AreEqual(PaperStage.Submitted, candidate.CurrentStage, "The candidate did change.");
        Assert.AreSame(working, copy.Working);
        Assert.AreEqual(before, copy.Snapshot(), "Building the candidate changed the working copy.");
        Assert.AreEqual(PaperStage.Submitted, copy.HeldStage, "Building the candidate cleared the held stage.");
        Assert.HasCount(1, copy.Delta.Authors, "Building the candidate changed the delta.");
        Assert.AreEqual(token, copy.Token);
        Assert.IsTrue(copy.HasUnsavedChanges());
        Assert.IsEmpty(copy.Working.PublicationMatches);
    }

    // C-8
    [TestMethod]
    public void ARetryAddsNoSecondEvent()
    {
        WorkingCopy copy = Loaded(SubmittablePaper());
        copy.HoldStage(PaperStage.Submitted);

        Manuscript first = Ready(copy.BuildCandidate(SaveTime));
        Manuscript retry = Ready(copy.BuildCandidate(SaveTime.AddMinutes(1)));

        Assert.AreNotSame(first, retry, "A retry reused the first candidate.");
        Assert.AreEqual(1, StageChanges(first));
        Assert.AreEqual(1, StageChanges(retry), "A retry after a failure added the stage event twice.");
        Assert.AreEqual(SaveTime.AddMinutes(1), retry.StageEnteredDate);
    }

    // C-9
    [TestMethod]
    public void ASaveKeepsThePublicationMatchesTheLibraryHolds()
    {
        Manuscript original = PaperWithMatches();
        WorkingCopy copy = Loaded(original);
        Assert.IsEmpty(copy.Working.PublicationMatches, "The clone keeps the matches now; this test and BuildCandidate's last step can be revisited.");

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));

        Assert.HasCount(2, candidate.PublicationMatches, "A page save would erase the matches on disk.");
        Assert.AreSame(original.PublicationMatches[0], candidate.PublicationMatches[0], "The matches are the library's own, in its order.");
        Assert.AreSame(original.PublicationMatches[1], candidate.PublicationMatches[1]);
    }

    // C-9 (critique S3)
    [TestMethod]
    public void TheCandidatesMatchListIsNotTheOriginals()
    {
        Manuscript original = PaperWithMatches();
        WorkingCopy copy = Loaded(original);

        Manuscript candidate = Ready(copy.BuildCandidate(SaveTime));
        candidate.PublicationMatches.RemoveAt(0);

        Assert.AreNotSame(original.PublicationMatches, candidate.PublicationMatches, "The candidate shares the saved record's match list.");
        Assert.HasCount(2, original.PublicationMatches, "Changing the candidate's list changed the saved record's.");
    }

    // C-11
    [TestMethod]
    public void AcceptingASaveClonesTheWrittenRecordTakesANewBaselineAndClearsHeldStageAndDelta()
    {
        Manuscript saved = SubmittablePaper();
        saved.Title = "  A Study of Tests  ";
        Manuscript drafted = ManuscriptCloneService.CloneManuscript(saved);
        drafted.TargetJournal = "Journal of Drafts";
        var copy = new WorkingCopy();
        copy.Load(saved, Library(), new RecoveryDraft { ManuscriptId = saved.Id, Manuscript = drafted, Reason = "shutdown" });
        var box = new TypedText(copy, TypedField.Title);
        box.Text = "A Saved Title";
        copy.HoldStage(PaperStage.Submitted);
        copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" }));
        Assert.IsTrue(copy.IsRecovered);
        int token = copy.Token;
        int changes = 0;
        copy.Changed += (_, _) => changes++;

        Manuscript written = Ready(copy.BuildCandidate(SaveTime));
        written.ManuscriptUrl = "https://example.org/as-the-store-left-it";
        ManuscriptCommit.CopyInto(written, saved);
        copy.AcceptSaved(written);

        Assert.AreNotSame(written, copy.Working, "The working copy is the written record itself.");
        Assert.AreEqual(JsonSerializer.Serialize(written), copy.Snapshot(), "The working copy is not the record as written.");
        Assert.AreEqual(copy.Snapshot(), copy.Baseline, "The baseline was not retaken.");
        Assert.IsFalse(copy.HasUnsavedChanges(), "A saved record is still unsaved.");
        Assert.IsNull(copy.HeldStage, "The held stage stayed.");
        Assert.IsTrue(copy.Delta.IsEmpty, "The delta stayed.");
        Assert.IsTrue(copy.Library.Authors.Any(author => author.Id == Grace), "The saved person left the library the pickers read.");
        Assert.IsFalse(copy.IsRecovered, "The saved draft still counts as recovered.");
        Assert.AreNotEqual(token, copy.Token, "A save does not start a new token.");
        Assert.AreEqual(1, changes, "AcceptSaved raises Changed once.");
        Assert.AreEqual(HeldValues.Of(written), copy.Stamps, "The stamps were not retaken.");
        Assert.AreEqual(TypedValues.Of(written), copy.Loaded, "The typed values were not retaken.");

        Assert.AreEqual("A Saved Title", box.Text, "The box does not show the saved title.");
        box.Text = "A Saved Titl";
        box.Text = "A Saved Title";
        Assert.IsFalse(copy.HasUnsavedChanges(), "Typing a character and removing it made the saved record unsaved.");
        box.Text = "  A Study of Tests";
        Assert.AreEqual("A Study of Tests", copy.Working.Title, "Typing the title loaded before the save wrote that load's text, not what was typed.");
    }

    // C-12 (D9)
    [TestMethod]
    public void HistoryUsesTheCoreClock()
    {
        using var clock = new FixedClock(FixedInstant);
        Manuscript original = FiledPaper();
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(-10)));
        WorkingCopy copy = Loaded(original);
        copy.HoldStage(PaperStage.Submitted);
        copy.Edit(working => working.FileDrawerReason = "Waiting for reviewers");

        Manuscript candidate = Ready(copy.BuildCandidate(CoreClock.Now));

        HistoryEvent[] added = [.. candidate.History.Skip(original.History.Count)];
        Assert.HasCount(2, added, "A stage change and a reason change add two events.");
        Assert.AreEqual(clock.Local, candidate.StageEnteredDate);
        foreach (HistoryEvent item in added)
        {
            Assert.AreEqual(clock.Local, item.EventDate, $"\"{item.Note}\" is not dated by the core's clock.");
            Assert.AreEqual((DateTime?)FixedInstant.UtcDateTime, item.RecordedAtUtc,$"\"{item.Note}\" is not stamped by the core's clock.");
        }
    }

    // C-13 (critique M2)
    [TestMethod]
    public void ASecondSaveKeepsTheFirstSavesStageChangeAndItsEvent()
    {
        Manuscript saved = SubmittablePaper();
        WorkingCopy copy = Loaded(saved);
        copy.HoldStage(PaperStage.Submitted);
        Manuscript first = Ready(copy.BuildCandidate(SaveTime));
        ManuscriptCommit.CopyInto(first, saved);
        copy.AcceptSaved(first);

        var box = new TypedText(copy, TypedField.Title);
        box.Text = "A Second Title";
        Manuscript second = Ready(copy.BuildCandidate(SaveTime.AddHours(2)));

        Assert.AreEqual("A Second Title", second.Title);
        Assert.AreEqual(PaperStage.Submitted, second.CurrentStage, "The second save erased the first save's stage change.");
        Assert.AreEqual(SaveTime, second.StageEnteredDate, "The second save moved the first save's stage date.");
        Assert.AreEqual(1, StageChanges(second), "The second save lost the first save's stage event or added another.");
        Assert.AreEqual(PaperStage.Submitted, saved.CurrentStage, "The commit did not reach the session's record.");
    }

    private static Manuscript Ready(SaveAttempt attempt)
    {
        Assert.AreEqual(SaveRefusal.None, attempt.Refusal, $"The save was refused: {attempt.Requirement}");
        Assert.IsTrue(attempt.IsReady);
        Assert.IsNotNull(attempt.Candidate);
        return attempt.Candidate;
    }

    private static int StageChanges(Manuscript record) =>
        record.History.Count(item => item.Note.StartsWith("Stage changed from ", StringComparison.Ordinal));

    private static Manuscript Paper(string title = "A Study of Tests") => new()
    {
        Title = title,
        CurrentStage = PaperStage.Draft,
        StageEnteredDate = Day,
        TargetJournal = "Journal of Tests",
    };

    // A draft with a submission recorded before its stage date: the load's
    // reconcile leaves it at Draft, and Submitted is supported.
    private static Manuscript SubmittablePaper()
    {
        Manuscript paper = Paper();
        paper.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(-10)));
        return paper;
    }

    private static Manuscript AcceptedPaper(PaperStage stage, ManuscriptLocation location)
    {
        Manuscript paper = Paper();
        paper.CurrentStage = stage;
        paper.Location = location;
        JournalSubmission submission = SubmissionTo("Journal of Tests", Day.AddDays(-60));
        submission.Decisions.Add(new EditorialDecisionEvent { Decision = EditorialDecision.Accepted, DecisionDate = Day.AddDays(-30) });
        paper.Submissions.Add(submission);
        return paper;
    }

    private static Manuscript FiledPaper()
    {
        Manuscript paper = Paper();
        paper.Location = ManuscriptLocation.FileDrawer;
        paper.FileDrawerDate = Day.AddDays(5);
        paper.FileDrawerReason = "Waiting for the second data set";
        return paper;
    }

    private static Manuscript PaperWithMatches()
    {
        Manuscript paper = Paper();
        paper.PublicationMatches.Add(new PublicationMatch { Doi = "10.1000/one", Title = "A Study of Tests" });
        paper.PublicationMatches.Add(new PublicationMatch { Doi = "10.1000/two", Title = "A Study of Tests, Again" });
        return paper;
    }

    private static JournalSubmission SubmissionTo(string journal, DateTime submitted) => new()
    {
        JournalName = journal,
        SubmittedDate = submitted,
    };

    private static AuthorLibraryData Library() => new()
    {
        Journals = [new JournalRecord { Id = TestsJournal, Name = "Journal of Tests" }],
    };

    private static WorkingCopy Loaded(Manuscript original)
    {
        var copy = new WorkingCopy();
        copy.Load(original, Library());
        return copy;
    }

    // Linked to the Journal of Tests under an abbreviation of its name.
    private static WorkingCopy LoadedLinked()
    {
        Manuscript original = Paper();
        original.TargetJournal = "J. Tests";
        original.TargetJournalId = TestsJournal;
        return Loaded(original);
    }

    // The core's clock fixed at one instant until disposed.
    private sealed class FixedClock : TimeProvider, IDisposable
    {
        private readonly TimeProvider _before = PlatformServices.Clock;
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now)
        {
            _now = now;
            PlatformServices.Clock = this;
        }

        public DateTime Local => _now.UtcDateTime.ToLocalTime();

        public override DateTimeOffset GetUtcNow() => _now;

        public void Dispose() => PlatformServices.Clock = _before;
    }
}
