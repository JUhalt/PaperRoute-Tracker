using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// Build 9's W1 (the design's tests W-1 to W-15, and L-9 brought forward from
// R1): the manuscript page's working copy, with no window. Every record is built in memory; nothing is read from
// or written to disk, under the assembly's storage guard.
[TestClass]
public sealed partial class WorkingCopyTests : Board.StorageCheckedTests
{
    private static readonly DateTime Day = new(2026, 3, 2);
    private static readonly Guid Ada = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000001");
    private static readonly Guid Grace = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000002");
    private static readonly Guid University = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000003");
    private static readonly Guid Institute = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000004");

    // W-1
    [TestMethod]
    public void EditingChangesTheWorkingCopyNotTheOriginal()
    {
        Manuscript original = Paper();
        string originalBefore = JsonSerializer.Serialize(original);
        WorkingCopy copy = Loaded(original);

        Assert.IsTrue(copy.Edit(working =>
        {
            working.Title = "A Second Study of Tests";
            working.Tags.Add("methods");
        }));

        Assert.AreEqual("A Second Study of Tests", copy.Working.Title, "The edit reached the working copy.");
        CollectionAssert.Contains(copy.Working.Tags, "methods");
        Assert.AreEqual(originalBefore, JsonSerializer.Serialize(original), "The original's JSON changed: an edit reached the session's own record.");
        Assert.AreSame(original, copy.Original);
    }

    // W-2
    [TestMethod]
    public void UnsavedGoesTrueOnAnEditAndFalseWhenTheOldValueIsBack()
    {
        WorkingCopy copy = Loaded(Paper());
        Assert.IsFalse(copy.HasUnsavedChanges(), "Nothing is unsaved after a load.");

        copy.Edit(working => working.Title = "Another Title");
        Assert.IsTrue(copy.HasUnsavedChanges(), "An edit is unsaved.");

        copy.Edit(working => working.Title = "A Study of Tests");
        Assert.IsFalse(copy.HasUnsavedChanges(), "The old value back is not a change.");
    }

    // W-3
    [TestMethod]
    public void ReconciliationAloneIsNotAnUnsavedChange()
    {
        Manuscript original = Paper();
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(20)));

        WorkingCopy copy = Loaded(original);

        Assert.AreEqual(PaperStage.Submitted, copy.Working.CurrentStage, "The load put the stage right from the latest submission.");
        Assert.AreEqual(PaperStage.Draft, original.CurrentStage, "The original keeps its stage until a save writes the reconciliation.");
        Assert.IsFalse(copy.HasUnsavedChanges(), "The reconciliation alone counted as unsaved: the baseline was taken before it.");
    }

    // W-4
    [TestMethod]
    public void ARecoveredDraftIsUnsavedUntilSavedOrDiscarded()
    {
        Manuscript original = Paper();
        var copy = new WorkingCopy();
        copy.Load(original, Library(), Draft(original, title: "A Study of Tests, recovered"));

        Assert.IsTrue(copy.IsRecovered);
        Assert.AreEqual("A Study of Tests, recovered", copy.Working.Title, "The working copy is the draft's.");
        Assert.AreEqual(string.Empty, copy.Baseline);
        Assert.IsTrue(copy.HasUnsavedChanges(), "A recovered draft is unsaved.");

        // A draft that equals the saved record is still unsaved: nothing has
        // written it, and leaving must not drop it unasked.
        var same = new WorkingCopy();
        same.Load(original, Library(), Draft(original, title: original.Title));
        Assert.IsTrue(same.HasUnsavedChanges(), "A draft that equals the saved record is still unsaved.");

        Assert.IsTrue(copy.Discard(original, Library()));
        Assert.IsFalse(copy.IsRecovered);
        Assert.AreEqual(original.Title, copy.Working.Title);
        Assert.IsFalse(copy.HasUnsavedChanges(), "A discarded draft leaves nothing unsaved.");
    }

    // L-9, brought forward from R1 because W1's Load already joins a draft's
    // records: a draft's people and affiliations join the library only where
    // the disk lacks their id, the delta holds only those, and its tag
    // colours are set.
    [TestMethod]
    public void ADraftsRecordsJoinOnlyWhenNotOnDisk()
    {
        Manuscript original = Paper();
        RecoveryDraft draft = Draft(original, title: original.Title);
        draft.UnsavedLibrary = new RecoveryDraftLibrary
        {
            Authors =
            [
                new AuthorRecord { Id = Ada, GivenName = "Ada", FamilyName = "Byron" },
                new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" },
            ],
            Affiliations =
            [
                new AffiliationRecord { Id = University, Institution = "Another University" },
                new AffiliationRecord { Id = Institute, Institution = "Example Institute" },
            ],
            TagColors = [new TagColor { Tag = "methods", Color = TagPalette.Violet }],
        };
        AuthorLibraryData onDisk = Library();
        AuthorRecord adaOnDisk = onDisk.Authors[0];
        AffiliationRecord universityOnDisk = onDisk.Affiliations[0];
        var copy = new WorkingCopy();

        copy.Load(original, onDisk, draft);

        Assert.AreSame(adaOnDisk, copy.Library.Authors.Single(author => author.Id == Ada), "The draft's record of a person on disk replaced the disk's.");
        Assert.AreEqual("Hopper", copy.Library.Authors.Single(author => author.Id == Grace).FamilyName, "The draft's new person is not in the library the pickers read.");
        CollectionAssert.AreEqual(new[] { Grace }, copy.Delta.Authors.Select(author => author.Id).ToArray(), "The delta holds a person the disk has, or lacks the new one.");
        Assert.AreSame(universityOnDisk, copy.Library.Affiliations.Single(affiliation => affiliation.Id == University), "The draft's record of an affiliation on disk replaced the disk's.");
        Assert.IsTrue(copy.Library.Affiliations.Any(affiliation => affiliation.Id == Institute), "The draft's new affiliation is not in the library.");
        CollectionAssert.AreEqual(new[] { Institute }, copy.Delta.Affiliations.Select(affiliation => affiliation.Id).ToArray(), "The delta holds an affiliation the disk has, or lacks the new one.");
        Assert.AreEqual(TagPalette.Violet, WorkTypeService.ColorOf("methods", copy.Library), "The draft's tag colour is not set in the library.");
        Assert.HasCount(1, copy.Delta.TagColors, "The draft's tag colour is not in the delta.");
    }

    // W-5
    [TestMethod]
    [DataRow(TypedField.Title)]
    [DataRow(TypedField.TargetJournal)]
    [DataRow(TypedField.FileDrawerReason)]
    public void SpacesAtTheEndsAreNotAChange(TypedField field)
    {
        WorkingCopy copy = Loaded(FiledPaper());
        var box = new TypedText(copy, field);
        string stored = box.Text;
        Assert.AreNotEqual(string.Empty, stored, "The record holds a value in this field.");

        box.Text = "  " + stored + "   ";

        Assert.AreEqual("  " + stored + "   ", box.Text, "The box keeps what was typed.");
        Assert.AreEqual(stored, Read(copy.Working, field), "Spaces at the ends reached the working copy.");
        Assert.IsFalse(copy.HasUnsavedChanges(), "Spaces at the ends counted as a change.");
    }

    // W-5 (critique N6)
    [TestMethod]
    public void AStoredTitleWithEndSpacesStaysSavedAfterTypingAndRemovingACharacter()
    {
        WorkingCopy copy = Loaded(Paper(" A Study of Tests "));
        var box = new TypedText(copy, TypedField.Title);
        Assert.AreEqual(" A Study of Tests ", box.Text, "The box shows the title as stored.");

        box.Text = " A Study of Tests s";
        Assert.IsTrue(copy.HasUnsavedChanges(), "A typed character is unsaved.");

        box.Text = " A Study of Tests ";
        Assert.AreEqual(" A Study of Tests ", copy.Working.Title, "The title typed back is the title as stored.");
        Assert.IsFalse(copy.HasUnsavedChanges(), "A character typed and removed left the stored title unsaved.");
    }

    // W-5: the value typed back is the one the load left, not the original
    // record's. The load's reconciliation rewrote a journal stored with
    // spaces at its ends as its submission's name, trimmed.
    [TestMethod]
    public void AJournalTheLoadReconciledStaysSavedAfterTypingAndRemovingACharacter()
    {
        Manuscript original = Paper();
        original.TargetJournal = " Journal of Tests ";
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(20)));
        WorkingCopy copy = Loaded(original);
        var box = new TypedText(copy, TypedField.TargetJournal);
        Assert.AreEqual("Journal of Tests", copy.Working.TargetJournal, "The load's reconciliation set the submission's journal.");
        Assert.IsFalse(copy.HasUnsavedChanges());

        box.Text = "Journal of Testsx";
        Assert.IsTrue(copy.HasUnsavedChanges(), "A typed character is unsaved.");

        box.Text = "Journal of Tests";
        Assert.AreEqual("Journal of Tests", copy.Working.TargetJournal, "The journal typed back is not the one the load left.");
        Assert.IsFalse(copy.HasUnsavedChanges(), "A character typed and removed left the reconciled journal unsaved.");
    }

    // W-6
    [TestMethod]
    public void AHeldStageIsUnsaved()
    {
        WorkingCopy copy = Loaded(Paper());

        Assert.IsTrue(copy.HoldStage(PaperStage.Submitted));

        Assert.AreEqual(PaperStage.Submitted, copy.HeldStage);
        Assert.AreEqual(PaperStage.Draft, copy.Working.CurrentStage, "A chosen stage waits for Save.");
        Assert.IsTrue(copy.HasUnsavedChanges(), "A held stage was not counted as unsaved.");
    }

    // W-6
    [TestMethod]
    public void ChoosingTheWorkingStageClearsIt()
    {
        WorkingCopy copy = Loaded(Paper());
        copy.HoldStage(PaperStage.Submitted);

        copy.HoldStage(PaperStage.Draft);

        Assert.IsNull(copy.HeldStage, "Choosing the working copy's own stage left a stage held.");
        Assert.IsFalse(copy.HasUnsavedChanges());
    }

    // W-7
    [TestMethod]
    [DataRow("person")]
    [DataRow("tag colour")]
    public void APageMadePersonOrTagColourIsUnsaved(string made)
    {
        WorkingCopy copy = Loaded(Paper());

        Assert.IsTrue(copy.EditLibrary(delta =>
        {
            if (made == "person")
            {
                delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" });
            }
            else
            {
                delta.TagColors.Add(new TagColor { Tag = "methods", Color = TagPalette.Violet });
            }
        }));

        Assert.IsTrue(copy.HasUnsavedChanges(), $"A page-made {made} was not counted as unsaved.");
        Assert.AreEqual(copy.Baseline, copy.Snapshot(), "The record itself is unchanged.");
        if (made == "person")
        {
            Assert.IsTrue(copy.Library.Authors.Any(author => author.Id == Grace), "The page's person is in the library the pickers read.");
        }
        else
        {
            Assert.AreEqual(TagPalette.Violet, WorkTypeService.ColorOf("methods", copy.Library), "The page's colour is in the library the chips read.");
        }
    }

    // W-8
    [TestMethod]
    public void EditingWhileFrozenIsRefusedAndChangesNothing()
    {
        WorkingCopy copy = Loaded(Paper());
        string before = copy.Snapshot();
        var log = new LogCapture();
        Trace.Listeners.Add(log);
        try
        {
            using (copy.Freeze())
            {
                Assert.IsTrue(copy.IsFrozen);
                Assert.IsFalse(copy.Edit(working => working.Title = "Typed while saving"), "An edit while frozen was accepted.");
                Assert.IsFalse(copy.HoldStage(PaperStage.Submitted), "A stage choice while frozen was accepted.");
                Assert.IsFalse(copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace })), "A library change while frozen was accepted.");
                Assert.IsFalse(copy.Discard(Paper(), Library()), "A discard while frozen was accepted.");

                // Loading another record is the frame's call, never a
                // section's, so it throws instead of refusing.
                Manuscript original = copy.Original!;
                Manuscript working = copy.Working;
                int token = copy.Token;
                Assert.ThrowsExactly<InvalidOperationException>(() => copy.Load(Paper("Another Paper"), Library()), "A load while frozen was accepted.");
                Assert.AreSame(original, copy.Original, "A refused load changed the original.");
                Assert.AreSame(working, copy.Working, "A refused load changed the working copy.");
                Assert.AreEqual(token, copy.Token, "A refused load started a new token.");
            }
        }
        finally
        {
            Trace.Listeners.Remove(log);
        }

        Assert.AreEqual(before, copy.Snapshot(), "The copy's JSON changed while frozen.");
        Assert.IsNull(copy.HeldStage);
        Assert.IsTrue(copy.Delta.IsEmpty);
        Assert.IsFalse(copy.HasUnsavedChanges());
        Assert.AreEqual(4, log.Lines.Count(line => line.Contains("refused", StringComparison.Ordinal)), "Each refusal writes one log line:" + Environment.NewLine + string.Join(Environment.NewLine, log.Lines));

        Assert.IsFalse(copy.IsFrozen, "The freeze ends when it is disposed.");
        Assert.IsTrue(copy.Edit(working => working.Title = "Typed after the save"), "Edits are taken again after the freeze.");
    }

    // W-9
    [TestMethod]
    public void EveryEditRaisesChanged()
    {
        WorkingCopy copy = Loaded(Paper());
        int changes = 0;
        copy.Changed += (_, _) => changes++;

        copy.Edit(working => working.Title = "Another Title");
        Assert.AreEqual(1, changes, "Edit");
        copy.HoldStage(PaperStage.Submitted);
        Assert.AreEqual(2, changes, "HoldStage");
        copy.EditLibrary(delta => delta.Authors.Add(new AuthorRecord { Id = Grace }));
        Assert.AreEqual(3, changes, "a library change");
        copy.Discard(Paper(), Library());
        Assert.AreEqual(4, changes, "Discard");
        copy.Load(Paper("Another Paper"), Library());
        Assert.AreEqual(5, changes, "Load");

        using (copy.Freeze())
        {
            copy.Edit(working => working.Title = "Typed while saving");
        }

        Assert.AreEqual(5, changes, "A refused edit raises nothing.");
    }

    // W-10 (#358)
    [TestMethod]
    public void AStepThatChangesTheTitleReplacesWhatWasTyped()
    {
        WorkingCopy copy = Loaded(Paper());
        var box = new TypedText(copy, TypedField.Title);
        box.Text = "A typed title";

        copy.Edit(working => working.Title = "A title a step set");

        Assert.AreEqual("A title a step set", box.Text, "The box kept typed text over the title a step set.");
    }

    // W-10 (#358)
    [TestMethod]
    public void AStepThatLeavesItKeepsWhatWasTyped()
    {
        WorkingCopy copy = Loaded(Paper());
        var box = new TypedText(copy, TypedField.Title);
        box.Text = "A typed title  ";

        // A workflow step that changes the stage and the target journal, not the title.
        copy.Edit(working =>
        {
            working.Submissions.Add(SubmissionTo("Journal of Steps", Day.AddDays(20)));
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(working);
        });

        Assert.AreEqual(PaperStage.Submitted, copy.Working.CurrentStage, "The step ran.");
        Assert.AreEqual("A typed title  ", box.Text, "A step that left the title replaced what was typed.");
        Assert.AreEqual("A typed title", copy.Working.Title);
    }

    // W-11 (#368 case 1)
    [TestMethod]
    public void AStepThatChangesTheTargetJournalShowsIt()
    {
        Manuscript original = Paper();
        original.CurrentStage = PaperStage.Submitted;
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day));
        WorkingCopy copy = Loaded(original);
        var box = new TypedText(copy, TypedField.TargetJournal);
        Assert.AreEqual("Journal of Tests", box.Text);

        // A rejection recorded in the submission's details clears the target journal.
        copy.Edit(working =>
        {
            working.Submissions[0].Decisions.Add(new EditorialDecisionEvent { Decision = EditorialDecision.Rejected, DecisionDate = Day.AddDays(30) });
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(working);
        });

        Assert.AreEqual(string.Empty, copy.Working.TargetJournal, "The step cleared the target journal.");
        Assert.AreEqual(string.Empty, box.Text, "The box kept the old journal after a step cleared it, so Save would write it back.");

        box.Text = "Journal of Second Chances";
        copy.Edit(working => working.Tags.Add("methods"));
        Assert.AreEqual("Journal of Second Chances", box.Text, "A later step that left the journal replaced the typed one.");
        Assert.AreEqual("Journal of Second Chances", copy.Working.TargetJournal);
    }

    // W-12
    [TestMethod]
    public void AStepThatChangesTheStageClearsTheHeldStage()
    {
        WorkingCopy copy = Loaded(Paper());
        copy.HoldStage(PaperStage.Accepted);

        copy.Edit(working =>
        {
            working.Submissions.Add(SubmissionTo("Journal of Tests", Day.AddDays(20)));
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(working);
        });

        Assert.AreEqual(PaperStage.Submitted, copy.Working.CurrentStage, "The step changed the stage.");
        Assert.IsNull(copy.HeldStage, "A step that changed the stage left the held stage in place.");
        Assert.AreEqual(PaperStage.Submitted, copy.Stamps.Stage, "The stage stamp follows the step.");

        // A stage chosen after the step is held through the next edit.
        copy.HoldStage(PaperStage.UnderReview);
        copy.Edit(working => working.Title = "Another Title");
        Assert.AreEqual(PaperStage.UnderReview, copy.HeldStage, "A stage chosen after the step was dropped by the next edit.");
    }

    // W-12
    [TestMethod]
    public void AStepThatLeavesItKeepsIt()
    {
        WorkingCopy copy = Loaded(Paper());
        copy.HoldStage(PaperStage.Submitted);

        copy.Edit(working => working.Title = "Another Title");

        Assert.AreEqual(PaperStage.Submitted, copy.HeldStage, "A step that left the stage dropped the held stage.");
    }

    // W-12
    [TestMethod]
    public void ALifecycleChangeDoesNotOverwriteAHeldStageItDidNotTouch()
    {
        Manuscript original = Paper();
        original.CurrentStage = PaperStage.Submitted;
        original.TargetJournal = string.Empty;
        original.Submissions.Add(SubmissionTo("Journal of Tests", Day));
        WorkingCopy copy = Loaded(original);
        copy.HoldStage(PaperStage.UnderReview);

        // Reconciling on the submission's own day sets the stage it already
        // has, and the target journal it lacked.
        copy.Edit(working => ManuscriptLifecycleService.ReconcileFromLatestWorkflow(working, allowSameDay: true));

        Assert.AreEqual("Journal of Tests", copy.Working.TargetJournal, "The lifecycle change ran.");
        Assert.AreEqual(PaperStage.Submitted, copy.Working.CurrentStage);
        Assert.AreEqual(PaperStage.UnderReview, copy.HeldStage, "A lifecycle change that left the stage overwrote the held stage.");
    }

    // W-13
    [TestMethod]
    public void DiscardReturnsToTheSavedRecordAndDropsPageMadeRecords()
    {
        Manuscript saved = Paper();
        WorkingCopy copy = Loaded(saved);
        copy.Edit(working => working.Title = "Another Title");
        copy.HoldStage(PaperStage.Submitted);
        copy.EditLibrary(delta =>
        {
            delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" });
            delta.TagColors.Add(new TagColor { Tag = "methods", Color = TagPalette.Violet });
        });
        Manuscript workingBefore = copy.Working;
        int token = copy.Token;
        AuthorLibraryData fresh = Library();

        Assert.IsTrue(copy.Discard(saved, fresh));

        Assert.AreEqual(saved.Title, copy.Working.Title, "The saved record's title is back.");
        Assert.AreNotSame(workingBefore, copy.Working, "The working copy is a new clone.");
        Assert.AreNotSame(saved, copy.Working);
        Assert.IsNull(copy.HeldStage, "The held stage stayed.");
        Assert.IsTrue(copy.Delta.IsEmpty, "The page's own records stayed in the delta.");
        Assert.AreSame(fresh, copy.Library, "The library is the fresh read.");
        Assert.IsFalse(copy.Library.Authors.Any(author => author.Id == Grace), "The page's person stayed in the library.");
        Assert.IsFalse(copy.Library.TagColors.Any(entry => entry.Tag == "methods"), "The page's tag colour stayed in the library.");
        Assert.AreNotEqual(token, copy.Token, "Discard starts a new token.");
        Assert.IsFalse(copy.HasUnsavedChanges());
    }

    // W-14: every field the working copy keeps is counted by
    // HasUnsavedChanges or named here with the reason it is not. A new field
    // on neither list fails, so a new kind of unsaved state cannot be missed.
    [TestMethod]
    public void EveryWorkingCopyFieldIsCountedOrNamed()
    {
        var counted = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(WorkingCopy.Working)] = "its snapshot against Baseline",
            [nameof(WorkingCopy.HeldStage)] = "a held stage is unsaved",
            [nameof(WorkingCopy.Delta)] = "a page-made record is unsaved",
        };
        var named = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(WorkingCopy.Original)] = "the saved record, never changed by the page",
            [nameof(WorkingCopy.Baseline)] = "what Working is compared with",
            [nameof(WorkingCopy.Stamps)] = "what the load and steps left; follows the record, edits nothing",
            [nameof(WorkingCopy.Loaded)] = "the typed fields as the load left them; what typing back writes, edits nothing",
            [nameof(WorkingCopy.Library)] = "the library on disk with Delta in it; Delta is counted",
            [nameof(WorkingCopy.Token)] = "names the record loaded",
            [nameof(WorkingCopy.IsRecovered)] = "counted through the empty Baseline of a draft",
            ["_freezes"] = "a save writing, not a change",
            [nameof(WorkingCopy.Changed)] = "the event's handlers",
        };
        var listed = counted.Keys.Concat(named.Keys).ToHashSet(StringComparer.Ordinal);

        // The audit finds a field it was not told of, or it proves nothing.
        CollectionAssert.AreEqual(new[] { "Unlisted" }, Unaccounted(typeof(ProbeWithAnUnlistedField), listed).ToArray(), "The audit missed a field on a probe type.");

        string[] unaccounted = [.. Unaccounted(typeof(WorkingCopy), listed)];
        Assert.IsEmpty(unaccounted,
            "WorkingCopy keeps state that HasUnsavedChanges does not count and this test does not name. Count it, or name it here with why it is not unsaved: " +
            string.Join(", ", unaccounted));

        var fields = FieldNames(typeof(WorkingCopy)).ToHashSet(StringComparer.Ordinal);
        string[] stale = [.. listed.Where(name => !fields.Contains(name)).Order(StringComparer.Ordinal)];
        Assert.IsEmpty(stale, "This test names fields WorkingCopy no longer has: " + string.Join(", ", stale));
    }

    // W-15
    [TestMethod]
    public void ALateContinuationForAnOlderRecordDoesNothing()
    {
        WorkingCopy copy = Loaded(Paper());
        int first = copy.Token;

        copy.Load(Paper("Another Paper"), Library());

        Assert.IsFalse(copy.Edit(first, working => working.Title = "Late"), "Work started for the first record changed the second.");
        Assert.AreEqual("Another Paper", copy.Working.Title);
        Assert.IsFalse(copy.HasUnsavedChanges());

        int second = copy.Token;
        Assert.IsTrue(copy.Edit(second, working => working.Title = "On time"), "Work for the record loaded is taken.");
        Assert.IsTrue(copy.Discard(Paper("Another Paper"), Library()));
        Assert.IsFalse(copy.Edit(second, working => working.Title = "Late again"), "Work started before a discard changed the record after it.");
        Assert.AreEqual("Another Paper", copy.Working.Title);
    }

    // W-15: a box shows the record as it is after a load or a discard, even
    // where the text typed into it equals the record once trimmed.
    [TestMethod]
    public void AfterALoadOrADiscardTheBoxShowsTheRecordAsItIs()
    {
        WorkingCopy copy = Loaded(Paper());
        var box = new TypedText(copy, TypedField.Title);

        box.Text = "A Study of Tests  ";
        copy.Load(Paper(), Library());
        Assert.AreEqual("A Study of Tests", box.Text, "After a load the box kept the text typed for the record before.");

        box.Text = "A Study of Tests  ";
        Assert.IsTrue(copy.Discard(Paper(), Library()));
        Assert.AreEqual("A Study of Tests", box.Text, "After a discard the box kept the text typed before it.");
    }

    private static Manuscript Paper(string title = "A Study of Tests") => new()
    {
        Title = title,
        CurrentStage = PaperStage.Draft,
        StageEnteredDate = Day,
        TargetJournal = "Journal of Tests",
    };

    private static Manuscript FiledPaper()
    {
        Manuscript paper = Paper();
        paper.Location = ManuscriptLocation.FileDrawer;
        paper.FileDrawerDate = Day.AddDays(5);
        paper.FileDrawerReason = "Waiting for the second data set";
        return paper;
    }

    private static JournalSubmission SubmissionTo(string journal, DateTime submitted) => new()
    {
        JournalName = journal,
        SubmittedDate = submitted,
    };

    private static AuthorLibraryData Library() => new()
    {
        Authors = [new AuthorRecord { Id = Ada, GivenName = "Ada", FamilyName = "Lovelace" }],
        Affiliations = [new AffiliationRecord { Id = University, Institution = "Example University" }],
    };

    private static RecoveryDraft Draft(Manuscript original, string title)
    {
        Manuscript draft = ManuscriptCloneService.CloneManuscript(original);
        draft.Title = title;
        return new RecoveryDraft { ManuscriptId = original.Id, Manuscript = draft, Reason = "shutdown" };
    }

    private static WorkingCopy Loaded(Manuscript original)
    {
        var copy = new WorkingCopy();
        copy.Load(original, Library());
        return copy;
    }

    private static string Read(Manuscript record, TypedField field) => field switch
    {
        TypedField.Title => record.Title,
        TypedField.TargetJournal => record.TargetJournal,
        TypedField.FileDrawerReason => record.FileDrawerReason,
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    // The instance fields a type keeps, by the name a reader knows: an
    // auto-property's field by its property's name.
    private static IEnumerable<string> FieldNames(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(field => BackingField().Match(field.Name) is { Success: true } match ? match.Groups[1].Value : field.Name);

    private static IEnumerable<string> Unaccounted(Type type, IReadOnlySet<string> listed) =>
        FieldNames(type).Where(name => !listed.Contains(name)).Order(StringComparer.Ordinal);

    [GeneratedRegex("^<(.+)>k__BackingField$")]
    private static partial Regex BackingField();

    // WorkingCopy's shape with one field more, for the audit's own check.
    private sealed class ProbeWithAnUnlistedField
    {
        public Manuscript Working { get; } = new();

        public bool Unlisted { get; set; }
    }

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
