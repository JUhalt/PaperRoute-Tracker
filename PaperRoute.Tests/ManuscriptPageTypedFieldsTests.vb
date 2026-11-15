Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' What is typed in the Overview's Title and Target journal boxes, and a
' Current stage chosen there, reaches the working copy only at Save (#358).
' A window or a workflow step that refreshes those boxes in between rewrites
' a box only when it changed that field of the working copy; otherwise what
' the researcher typed or chose stays, and Save writes it. Save itself, the
' Journal Library link and the delete guards work as in v1.0.0.
Partial Public Class ManuscriptPageTests

    ' DOI & Crossref Metadata with Title left unticked, the default when the
    ' manuscript has a title, does not put the saved title back in the box.
    <TestMethod>
    Public Sub TypedTitleSurvivesACrossrefLookupThatLeavesTitleUnticked()
        WithTypedFieldsPage(
            WithCrossrefDoi(Sample("Saved title")),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "Typed title"
                Dim titleTicked As Boolean = True
                RunCrossrefLookup(board.Editor, Sub(dialog) titleTicked = LookupBox(dialog, "chkTitle").Checked)

                Assert.IsFalse(titleTicked, "Title is unticked by default: the manuscript has a title.")
                Assert.AreEqual("Typed title", board.Field("Title").Text, "The typed title is still in the box.")
                Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The typed title is still unsaved.")
                Assert.AreEqual("Saved title", manuscript.Title, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed title", manuscript.Title, "Save writes the typed title.")
            End Sub,
            New CrossrefWork())
    End Sub

    ' With Title ticked, the looked-up title replaces the typed one, in the
    ' box and at Save, as Apply Selected asks.
    <TestMethod>
    Public Sub TypedTitleGivesWayToTheCrossrefTitleWhenTitleIsTicked()
        WithTypedFieldsPage(
            WithCrossrefDoi(Sample("Saved title")),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "Typed title"
                RunCrossrefLookup(board.Editor, Sub(dialog) LookupBox(dialog, "chkTitle").Checked = True)

                Assert.AreEqual("Looked-up manuscript", board.Field("Title").Text, "The ticked Crossref title is in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Looked-up manuscript", manuscript.Title, "Save writes the Crossref title.")
            End Sub,
            New CrossrefWork())
    End Sub

    ' Title ticked is an explicit choice: the Crossref title goes in the box
    ' even when it is the saved title, so the working copy's title did not
    ' change.
    <TestMethod>
    Public Sub TickedCrossrefTitleSetsTheBoxEvenWhenItIsTheSavedTitle()
        WithTypedFieldsPage(
            WithCrossrefDoi(Sample("Looked-up manuscript")),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "Typed title"
                RunCrossrefLookup(board.Editor, Sub(dialog) LookupBox(dialog, "chkTitle").Checked = True)

                Assert.AreEqual("Looked-up manuscript", board.Field("Title").Text, "The ticked Crossref title is in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Looked-up manuscript", manuscript.Title, "Save writes the Crossref title.")
            End Sub,
            New CrossrefWork())
    End Sub

    ' A target journal typed after a rejection survives an edit of the
    ' rejected submission: the edit leaves the working copy's empty target
    ' as it was.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesEditingASubmission()
        WithTypedFieldsPage(
            RejectedAtFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Second Journal"
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Second Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual(String.Empty, manuscript.TargetJournal, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Second Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage, "The stage is unchanged.")
            End Sub)
    End Sub

    ' An edit that changes the active submission's journal changes the
    ' working copy's target, so it goes in the box over what was typed.
    <TestMethod>
    Public Sub TypedTargetJournalGivesWayToTheActiveSubmissionsJournal()
        Dim manuscript As Manuscript = Sample("Submitted manuscript")
        manuscript.CurrentStage = PaperStage.Submitted
        manuscript.StageEnteredDate = New DateTime(2026, 1, 5)
        manuscript.TargetJournal = "First Journal"
        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)})
        WithTypedFieldsPage(
            manuscript,
            Sub(board, saved, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                EditFirstSubmission(board.Editor, Sub(dialog) DirectCast(GetType(AddSubmissionForm).GetField("txtJournal", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox).Text = "Renamed Journal")

                Assert.AreEqual("Renamed Journal", board.Field("Target journal").Text, "The active submission's journal is the target.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Renamed Journal", saved.TargetJournal)
            End Sub)
    End Sub

    ' A stage change Save cannot complete opens the latest submission; the
    ' typed target journal is still in the box afterwards, the stage box is
    ' set back to the saved stage, and the next Save writes the journal.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesAStageChangeSaveCouldNotComplete()
        WithTypedFieldsPage(
            RejectedAtFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Second Journal"
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.Revision

                Dim detailsOpened As Integer = 0
                Using details As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of SubmissionDetailsForm)(
                    Sub(dialog)
                        If Not dialog.TopLevel Then Return
                        detailsOpened += 1
                        dialog.DialogResult = DialogResult.Cancel
                    End Sub)
                    Assert.IsFalse(board.SaveManuscriptPage(), "No revision decision was recorded, so Save stops.")
                End Using

                Assert.AreEqual(1, detailsOpened, "The latest submission opened once.")
                CollectionAssert.AreEqual({"Editorial Decision Required", "Stage Not Changed"}, dialogs.Closed, "Save asked, then said the stage was not changed.")
                Assert.AreEqual("Second Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual(PaperStage.Draft, CType(stage.SelectedItem, PaperStage), "The stage box shows the saved stage again.")
                Assert.AreEqual(String.Empty, manuscript.TargetJournal, "Nothing is saved.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The next Save completes.")
                Assert.AreEqual("Second Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
            End Sub,
            answerYes:="Editorial Decision Required")
    End Sub

    ' Journal, Preprint & Links closed with OK after changing only a
    ' preprint DOI leaves the target journal alone, so the typed one stays.
    <TestMethod>
    Public Sub TypedTargetJournalIsKeptWhenJournalLinksIsClosedWithOK()
        WithTypedFieldsPage(
            Sample("Linked manuscript"),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        LinksBox(dialog, "txtPreprintDoi").Text = "10.5555/paperroute.preprint"
                        InvokeLinks(dialog, "SaveChanges")
                    End Sub)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual("A fictional journal", manuscript.TargetJournal, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual("10.5555/paperroute.preprint", manuscript.Metadata.PreprintDoi, "Save writes the preprint DOI.")
            End Sub)
    End Sub

    ' Typed fields stay unsaved through a cancelled window, and Discard
    ' returns to the saved values without writing.
    <TestMethod>
    Public Sub TypedFieldsStayUnsavedThroughACancelledWindowAndDiscardRestores()
        WithTypedFieldsPage(
            Sample("Saved title"),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "Typed title"
                board.Field("Target journal").Text = "Typed Journal"
                AnswerJournalLinks(board.Editor, Sub(dialog) dialog.DialogResult = DialogResult.Cancel)

                Assert.AreEqual("Typed title", board.Field("Title").Text)
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text)
                Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The typed fields are unsaved.")
                board.Tick()
                Assert.IsTrue(board.SaveBarVisible, "The Unsaved changes bar shows.")

                board.Discard()
                Assert.AreEqual(0, board.SaveCount, "Discard writes nothing.")
                Assert.AreEqual("Saved title", manuscript.Title)
                Assert.AreEqual("A fictional journal", manuscript.TargetJournal)
                Assert.AreEqual("Saved title", board.Field("Title").Text, "The page shows the saved title again.")
                Assert.AreEqual("A fictional journal", board.Field("Target journal").Text, "The page shows the saved target journal again.")
                Assert.IsFalse(board.Editor.HasUnsavedChanges())
            End Sub)
    End Sub

    ' Discard after a step that kept the typed fields and the chosen stage
    ' shows the saved values again, and a later step leaves them there.
    <TestMethod>
    Public Sub DiscardAfterAStepThatKeptTypedFieldsRestoresTheSavedValues()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "Typed title"
                board.Field("Target journal").Text = "Typed Journal"
                StageBox(board).SelectedItem = PaperStage.UnderReview
                EditFirstSubmission(board.Editor, Nothing)
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The step kept the typed target journal.")

                board.Discard()
                Assert.AreEqual(0, board.SaveCount, "Discard writes nothing.")
                Assert.AreEqual("Submitted manuscript", board.Field("Title").Text, "The page shows the saved title again.")
                Assert.AreEqual("First Journal", board.Field("Target journal").Text, "The page shows the saved target journal again.")
                Assert.AreEqual(PaperStage.Submitted, CType(StageBox(board).SelectedItem, PaperStage), "The page shows the saved stage again.")
                Assert.IsFalse(board.Editor.HasUnsavedChanges())

                EditFirstSubmission(board.Editor, Nothing)
                Assert.AreEqual("First Journal", board.Field("Target journal").Text)
                Assert.AreEqual(PaperStage.Submitted, CType(StageBox(board).SelectedItem, PaperStage))
            End Sub)
    End Sub

    ' An emptied title box is not written anywhere, and Save still refuses
    ' it after a window was opened.
    <TestMethod>
    Public Sub TypedFieldsKeepTheTitleRequiredAfterAWindow()
        WithTypedFieldsPage(
            Sample("Saved title"),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "   "
                AnswerJournalLinks(board.Editor, Sub(dialog) InvokeLinks(dialog, "SaveChanges"))

                Assert.IsFalse(board.SaveManuscriptPage(), "Save refuses an empty title.")
                CollectionAssert.AreEqual({"Title Required"}, dialogs.Closed)
                Assert.AreEqual(0, board.SaveCount)
                Assert.AreEqual("Saved title", manuscript.Title)
                dialogs.Closed.Clear()
            End Sub)
    End Sub

    ' With nothing typed, Journal, Preprint & Links closed with OK and no
    ' change leaves the page with no unsaved changes.
    <TestMethod>
    Public Sub NothingTypedLeavesNoUnsavedChangesAfterJournalLinksOK()
        WithTypedFieldsPage(
            Sample("Saved title"),
            Sub(board, manuscript, dialogs)
                AnswerJournalLinks(board.Editor, Sub(dialog) InvokeLinks(dialog, "SaveChanges"))

                Assert.AreEqual("Saved title", board.Field("Title").Text)
                Assert.AreEqual("A fictional journal", board.Field("Target journal").Text)
                Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Nothing was typed or changed.")
            End Sub)
    End Sub

    ' An edit of the active submission that leaves its journal alone leaves
    ' the working copy's target as it was; the typed target journal stays in
    ' the box and Save writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesAnEditThatKeepsTheActiveSubmissionsJournal()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual("First Journal", manuscript.TargetJournal, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage, "The stage is unchanged.")
            End Sub)
    End Sub

    ' Deleting the last submission clears the working copy's target and
    ' takes the stage back to Draft: the step changed both, so both boxes
    ' show it, over what was typed, and Save writes it.
    <TestMethod>
    Public Sub DeletingTheLastSubmissionClearsTheTypedTargetJournal()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                DeleteSubmissionAt(board.Editor, 0)

                CollectionAssert.AreEqual({"Delete Journal Submission"}, dialogs.Closed, "Delete asked once.")
                Assert.AreEqual(String.Empty, board.Field("Target journal").Text, "The removal cleared the target.")
                Assert.AreEqual(PaperStage.Draft, CType(StageBox(board).SelectedItem, PaperStage))

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual(String.Empty, manuscript.TargetJournal, "Save writes the cleared target.")
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage, "The removal took the stage back to Draft.")
                Assert.AreEqual(0, manuscript.Submissions.Count)
            End Sub,
            answerYes:="Delete Journal Submission")
    End Sub

    ' A Current stage chosen but not saved stays chosen after a submission
    ' edit that leaves the stage alone, and Save moves to it.
    <TestMethod>
    Public Sub ChosenStageSurvivesEditingASubmission()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.UnderReview
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual(PaperStage.UnderReview, CType(stage.SelectedItem, PaperStage), "The chosen stage is still chosen.")
                Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The chosen stage is unsaved.")
                Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual(PaperStage.UnderReview, manuscript.CurrentStage, "Save moves to the chosen stage.")
            End Sub)
    End Sub

    ' A new revision decision at the active submission's journal moves the
    ' stage, so the stage box shows Revision; the target it sets is the one
    ' the working copy already had, so the typed target journal stays.
    <TestMethod>
    Public Sub NewRevisionDecisionMovesTheStageAndKeepsTheTypedTargetJournal()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
                board.Field("Target journal").Text = "Typed Journal"
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.Revision

                Dim opened As Integer = 0
                Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddDecisionForm)(
                    Sub(dialog)
                        opened += 1
                        Dim decision As ComboBox = DirectCast(GetType(AddDecisionForm).GetField("cmbDecision", flags).GetValue(dialog), ComboBox)
                        decision.SelectedItem = decision.Items.Cast(Of Object)().Single(Function(item) CType(item.GetType().GetProperty("Value").GetValue(item), EditorialDecision) = EditorialDecision.MajorRevision)
                        GetType(AddDecisionForm).GetMethod("SaveDecision", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                    End Sub)
                    GetType(EditManuscriptForm).GetMethod("OpenRevisionDeadlineEditor", flags).Invoke(board.Editor, New Object() {Nothing, EventArgs.Empty})
                    Application.DoEvents()
                End Using

                Assert.AreEqual(1, opened, "The decision window opened once.")
                Assert.AreEqual(PaperStage.Revision, CType(stage.SelectedItem, PaperStage))
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage)
            End Sub)
    End Sub

    ' A new, later submission to the journal the working copy already
    ' targets leaves the target as it was, so the typed one stays.
    <TestMethod>
    Public Sub NewSubmissionToTheSavedJournalKeepsTheTypedTargetJournal()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AddOneSubmission(board.Editor, "First Journal", New DateTime(2026, 2, 1))

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(2, manuscript.Submissions.Count)
            End Sub)
    End Sub

    ' A backdated submission leaves the target as it is: the typed target
    ' journal stays in the box and Save writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesABackdatedSubmission()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AddOneSubmission(board.Editor, "Earlier Journal", New DateTime(2025, 11, 3))

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(2, manuscript.Submissions.Count)
            End Sub)
    End Sub

    ' A backdated submission, then an edit of the active submission that
    ' changes nothing: neither step changed the target, so the typed target
    ' journal stays in the box and Save writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesABackdatedSubmissionThenAnEdit()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AddOneSubmission(board.Editor, "Earlier Journal", New DateTime(2025, 11, 3))
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(2, manuscript.Submissions.Count)
            End Sub)
    End Sub

    ' Correcting the journal of an older, rejected submission leaves the
    ' active submission, and so the target, alone: the typed target journal
    ' stays in the box and Save writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesCorrectingAnOlderSubmissionsJournal()
        Dim manuscript As Manuscript = Sample("Resubmitted manuscript")
        manuscript.CurrentStage = PaperStage.Submitted
        manuscript.StageEnteredDate = New DateTime(2026, 1, 5)
        manuscript.TargetJournal = "First Journal"
        Dim older As New JournalSubmission With {.JournalName = "Old Journl", .SubmittedDate = New DateTime(2025, 10, 1)}
        older.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Rejected, .DecisionDate = New DateTime(2025, 11, 1)})
        manuscript.Submissions.Add(older)
        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)})
        WithTypedFieldsPage(
            manuscript,
            Sub(board, saved, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                EditFirstSubmission(board.Editor, Sub(dialog) DirectCast(GetType(AddSubmissionForm).GetField("txtJournal", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox).Text = "Old Journal")

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", saved.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual("Old Journal", saved.Submissions(0).JournalName, "The correction is saved.")
                Assert.AreEqual(PaperStage.Submitted, saved.CurrentStage, "The stage is unchanged.")
            End Sub)
    End Sub

    ' A target journal changed in Journal, Preprint & Links wins over the
    ' typed one, in the box and at Save.
    <TestMethod>
    Public Sub TypedTargetJournalGivesWayToAJournalChangedInLinks()
        WithTypedFieldsPage(
            Sample("Linked manuscript"),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        LinksBox(dialog, "txtTargetJournal").Text = "Links Journal"
                        InvokeLinks(dialog, "SaveChanges")
                    End Sub)

                Assert.AreEqual("Links Journal", board.Field("Target journal").Text, "The journal from the window is in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Links Journal", manuscript.TargetJournal, "Save writes the journal from the window.")
            End Sub)
    End Sub

    ' Clear Library Link in Journal, Preprint & Links, then OK: the name
    ' stays and the link goes. Save and the recovery draft keep it gone.
    <TestMethod>
    Public Sub ClearedLibraryLinkStaysClearedAtSaveAndInTheRecoveryDraft()
        Dim journal As New JournalRecord With {.Name = "Linked Journal"}
        WithTypedFieldsPage(
            LinkedTo(journal),
            Sub(board, manuscript, dialogs)
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        InvokeLinks(dialog, "ClearJournalLink")
                        InvokeLinks(dialog, "SaveChanges")
                    End Sub)

                Assert.AreEqual("Linked Journal", board.Field("Target journal").Text, "The name stays.")
                Assert.IsNull(board.Editor.RecoveryCopy().TargetJournalId, "The recovery draft holds no link.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Linked Journal", manuscript.TargetJournal)
                Assert.IsNull(manuscript.TargetJournalId, "Save writes no link.")
            End Sub,
            seed:=Sub(board) SeedJournals(board, True, journal))
    End Sub

    ' Clear Library Link, OK, then Manage Journals from the window opened
    ' again: the working copy no longer links the record, so it can be
    ' deleted, as in v1.0.0, and Save writes no link.
    <TestMethod>
    Public Sub LibraryRecordCanBeDeletedInManageJournalsAfterClearLibraryLink()
        Dim journal As New JournalRecord With {.Name = "Linked Journal"}
        WithTypedFieldsPage(
            LinkedTo(journal),
            Sub(board, manuscript, dialogs)
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        InvokeLinks(dialog, "ClearJournalLink")
                        InvokeLinks(dialog, "SaveChanges")
                    End Sub)
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        DeleteFirstJournalInManageJournals(dialog)
                        dialog.DialogResult = DialogResult.Cancel
                    End Sub)

                CollectionAssert.AreEqual({"Delete Journal"}, dialogs.Closed, "Manage Journals asked once and deleted the record.")
                Assert.IsFalse(New AuthorLibraryRepository().Load().Journals.Any(Function(item) item.Id = journal.Id), "The record is gone from the Journal Library.")
                Assert.AreEqual("Linked Journal", board.Field("Target journal").Text, "The name stays.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Linked Journal", manuscript.TargetJournal)
                Assert.IsNull(manuscript.TargetJournalId, "Save writes no link.")
            End Sub,
            answerYes:="Delete Journal",
            seed:=Sub(board) SeedJournals(board, True, journal))
    End Sub

    ' A link whose library record was deleted is dropped by Journal,
    ' Preprint & Links on OK; Save and the recovery draft do not bring the
    ' deleted record's id back.
    <TestMethod>
    Public Sub DeletedLibraryRecordsLinkIsNotSavedBack()
        Dim journal As New JournalRecord With {.Name = "Linked Journal"}
        WithTypedFieldsPage(
            LinkedTo(journal),
            Sub(board, manuscript, dialogs)
                ' Deleted from the Journal Library the window reads.
                AnswerJournalLinks(board.Editor, Sub(dialog) InvokeLinks(dialog, "SaveChanges"))

                Assert.IsNull(board.Editor.RecoveryCopy().TargetJournalId, "The recovery draft holds no link.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Linked Journal", manuscript.TargetJournal)
                Assert.IsNull(manuscript.TargetJournalId, "Save writes no link to the deleted record.")
            End Sub,
            seed:=Sub(board) SeedJournals(board, False, journal))
    End Sub

    ' A submission to the saved journal's name with no library link sets
    ' the target with no link, as before; Save does not link it again.
    <TestMethod>
    Public Sub UnlinkedSubmissionToTheSavedJournalDoesNotLinkIt()
        Dim journal As New JournalRecord With {.Name = "First Journal"}
        Dim submitted As Manuscript = SubmittedToFirstJournal()
        submitted.TargetJournalId = journal.Id
        WithTypedFieldsPage(
            submitted,
            Sub(board, manuscript, dialogs)
                EditFirstSubmission(board.Editor, Nothing)

                Assert.IsNull(board.Editor.RecoveryCopy().TargetJournalId, "The recovery draft holds no link.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("First Journal", manuscript.TargetJournal)
                Assert.IsNull(manuscript.TargetJournalId, "The unlinked submission's target has no link.")
            End Sub,
            seed:=Sub(board) SeedJournals(board, True, journal))
    End Sub

    ' Two edits of the active submission in a row: neither changes the
    ' working copy's target, and the typed target journal survives both.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesTwoEditsInARow()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                EditFirstSubmission(board.Editor, Nothing)
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
            End Sub)
    End Sub

    ' Journal, Preprint & Links cancelled, then an edit of the active
    ' submission: the typed target journal survives.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesACancelledJournalLinksThenAnEdit()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AnswerJournalLinks(board.Editor, Sub(dialog) dialog.DialogResult = DialogResult.Cancel)
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
            End Sub)
    End Sub

    ' After an edit that kept the typed target journal, a second edit that
    ' renames the active submission's journal sets the target, and a third
    ' edit keeps that journal: the step's own target wins.
    <TestMethod>
    Public Sub ALaterStepThatSetsANewTargetWinsOverTheTypedOne()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                EditFirstSubmission(board.Editor, Nothing)
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The first edit keeps the typed target journal.")

                EditFirstSubmission(board.Editor, Sub(dialog) DirectCast(GetType(AddSubmissionForm).GetField("txtJournal", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox).Text = "Renamed Journal")
                Assert.AreEqual("Renamed Journal", board.Field("Target journal").Text, "The renamed journal is the target.")

                EditFirstSubmission(board.Editor, Nothing)
                Assert.AreEqual("Renamed Journal", board.Field("Target journal").Text, "A later edit does not bring the typed journal back.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Renamed Journal", manuscript.TargetJournal, "Save writes the step's journal.")
            End Sub)
    End Sub

    ' Typed over, then Add submission to the typed journal, then that
    ' submission deleted: each step changed the target, so the box follows,
    ' and the target goes back to the remaining submission's journal.
    <TestMethod>
    Public Sub DeletingASubmissionAddedToTheTypedJournalGivesBackTheEarlierTarget()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AddOneSubmission(board.Editor, "Typed Journal", New DateTime(2026, 2, 1))
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The new submission's journal is the target.")

                DeleteSubmissionAt(board.Editor, 1)
                CollectionAssert.AreEqual({"Delete Journal Submission"}, dialogs.Closed, "Delete asked once.")

                Assert.AreEqual("First Journal", board.Field("Target journal").Text, "The remaining submission's journal is the target.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("First Journal", manuscript.TargetJournal, "Save writes the remaining submission's journal.")
                Assert.AreEqual(1, manuscript.Submissions.Count)
            End Sub,
            answerYes:="Delete Journal Submission")
    End Sub

    ' Saved, then typed again: the page remembers the saved target, so a
    ' step that does not change it keeps the newly typed one.
    <TestMethod>
    Public Sub TargetJournalTypedAgainAfterSaveSurvivesAStep()
        WithTypedFieldsPage(
            RejectedAtFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Second Journal"
                Assert.IsTrue(board.SaveManuscriptPage(), "The first Save completes.")
                Assert.AreEqual("Second Journal", manuscript.TargetJournal)

                board.Field("Target journal").Text = "Third Journal"
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual("Third Journal", board.Field("Target journal").Text, "The newly typed target journal is still in the box.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Third Journal", manuscript.TargetJournal, "Save writes the newly typed target journal.")
            End Sub)
    End Sub

    ' A Current stage chosen but not saved stays chosen after a reviewer
    ' response edit in the inline submission pane, which leaves the stage
    ' alone.
    <TestMethod>
    Public Sub ChosenStageSurvivesAnInlineReviewerResponseEdit()
        WithTypedFieldsPage(
            WithRoute(),
            Sub(board, manuscript, dialogs)
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.UnderReview
                MoveFirstResponseDown(board)

                Assert.AreEqual(PaperStage.UnderReview, CType(stage.SelectedItem, PaperStage), "The chosen stage is still chosen.")
                Assert.IsTrue(board.Editor.HasUnsavedChanges())
                Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage, "Nothing is saved before Save.")
            End Sub)
    End Sub

    ' A rejection recorded in the inline submission pane moves the stage to
    ' Draft in the box; the pane does not touch the Target journal box, as
    ' in v1.0.0, so the typed target journal stays and Save writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesAnInlineRejection()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                AddInlineDecision(board, EditorialDecision.Rejected)

                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual(PaperStage.Draft, CType(StageBox(board).SelectedItem, PaperStage), "The rejection moved the stage box to Draft.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
            End Sub)
    End Sub

    ' A decision deleted in the inline submission pane with a target
    ' journal typed: the pane moves the stage box as in v1.0.0 and leaves
    ' the Target journal box alone, and Save writes the typed journal.
    <TestMethod>
    Public Sub TypedTargetJournalStaysWhenAnInlineDecisionIsDeleted()
        WithTypedFieldsPage(
            RejectedAtFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                DeleteFirstInlineDecision(board)

                CollectionAssert.AreEqual({"Delete Editorial Decision"}, dialogs.Closed, "Delete asked once.")
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual(PaperStage.Submitted, CType(StageBox(board).SelectedItem, PaperStage), "Without the rejection the stage box shows Submitted.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage)
                Assert.AreEqual(0, manuscript.Submissions(0).Decisions.Count)
            End Sub,
            answerYes:="Delete Editorial Decision")
    End Sub

    ' Under Review chosen on a draft: Save records the submission it needs,
    ' whose step puts Submitted in the stage box, and then saves Under
    ' Review while the box still shows Submitted. A later edit that changes
    ' nothing shows the saved stage again, as in v1.0.0, so the next Save
    ' keeps Under Review. The draft entered its stage before the submission
    ' date, so recording it moves the stage; Save then dates Under Review
    ' today, so the later edit leaves the stage alone.
    <TestMethod>
    Public Sub StageSavedThroughARecordedSubmissionIsShownAgainAfterAStep()
        Dim draft As Manuscript = Sample("Draft manuscript")
        draft.StageEnteredDate = New DateTime(2025, 10, 1)
        WithTypedFieldsPage(
            draft,
            Sub(board, manuscript, dialogs)
                Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.UnderReview

                Dim opened As Integer = 0
                Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
                    Sub(dialog)
                        opened += 1
                        DirectCast(GetType(AddSubmissionForm).GetField("dtpSubmitted", flags).GetValue(dialog), DateTimePicker).Value = New DateTime(2025, 11, 3)
                        GetType(AddSubmissionForm).GetMethod("SaveSubmission", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                    End Sub)
                    Assert.IsTrue(board.SaveManuscriptPage(), "Save recorded the submission and completed.")
                End Using
                Assert.AreEqual(1, opened, "The submission window opened once.")
                CollectionAssert.AreEqual({"Submission Details Required"}, dialogs.Closed, "Save asked to record the submission, and nothing else.")
                Assert.AreEqual(PaperStage.UnderReview, manuscript.CurrentStage, "Save wrote the chosen stage.")

                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual(PaperStage.UnderReview, CType(stage.SelectedItem, PaperStage), "The step shows the saved stage again.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual(PaperStage.UnderReview, manuscript.CurrentStage, "The next Save keeps Under Review.")
                Assert.IsFalse(manuscript.History.Any(Function(item) item.Note = "Stage changed from UnderReview to Submitted."), "No stage change the researcher did not choose.")
            End Sub,
            answerYes:="Submission Details Required")
    End Sub

    ' In Press chosen with a submission under way: Save opens the latest
    ' submission, an Accepted decision puts Accepted in the stage box, and
    ' Save then saves In Press while the box still shows Accepted. A later
    ' edit that changes nothing shows the saved stage again, as in v1.0.0,
    ' so the next Save keeps In Press.
    <TestMethod>
    Public Sub StageSavedThroughARecordedDecisionIsShownAgainAfterAStep()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
                Dim stage As ComboBox = StageBox(board)
                stage.SelectedItem = PaperStage.InPress

                Dim detailsOpened As Integer = 0
                Dim decisionsOpened As Integer = 0
                Using decisionAnswer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddDecisionForm)(
                        Sub(dialog)
                            decisionsOpened += 1
                            Dim decision As ComboBox = DirectCast(GetType(AddDecisionForm).GetField("cmbDecision", flags).GetValue(dialog), ComboBox)
                            decision.SelectedItem = decision.Items.Cast(Of Object)().Single(Function(item) CType(item.GetType().GetProperty("Value").GetValue(item), EditorialDecision) = EditorialDecision.Accepted)
                            DirectCast(GetType(AddDecisionForm).GetField("dtpDecisionDate", flags).GetValue(dialog), DateTimePicker).Value = New DateTime(2026, 1, 10)
                            GetType(AddDecisionForm).GetMethod("SaveDecision", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                        End Sub),
                      detailsAnswer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of SubmissionDetailsForm)(
                        Sub(dialog)
                            If Not dialog.TopLevel Then Return
                            detailsOpened += 1
                            GetType(SubmissionDetailsForm).GetMethod("AddDecision", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                            dialog.DialogResult = DialogResult.OK
                        End Sub)
                    Assert.IsTrue(board.SaveManuscriptPage(), "Save recorded the decision and completed.")
                End Using
                Assert.AreEqual(1, detailsOpened, "The latest submission opened once.")
                Assert.AreEqual(1, decisionsOpened, "The decision window opened once.")
                CollectionAssert.AreEqual({"Editorial Decision Required"}, dialogs.Closed, "Save asked to open the submission, and nothing else.")
                Assert.AreEqual(PaperStage.InPress, manuscript.CurrentStage, "Save wrote the chosen stage.")

                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual(PaperStage.InPress, CType(stage.SelectedItem, PaperStage), "The step shows the saved stage again.")
                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual(PaperStage.InPress, manuscript.CurrentStage, "The next Save keeps In Press.")
                Assert.IsFalse(manuscript.History.Any(Function(item) item.Note = "Stage changed from InPress to Accepted."), "No stage change the researcher did not choose.")
            End Sub,
            answerYes:="Editorial Decision Required")
    End Sub

    ' The Current stage box on the Overview.
    Private Shared Function StageBox(board As PageBoard) As ComboBox
        Return DirectCast(GetType(EditManuscriptForm).GetField("cmbStage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), ComboBox)
    End Function

    ' The inline submission pane's first decision selected and deleted (its
    ' Delete handler); the watcher answers the question.
    Private Shared Sub DeleteFirstInlineDecision(board As PageBoard)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
        DirectCast(GetType(SubmissionDetailsForm).GetField("lstDecisions", flags).GetValue(detail), ListBox).SelectedIndex = 0
        Application.DoEvents()
        GetType(SubmissionDetailsForm).GetMethod("DeleteSelectedDecision", flags).Invoke(detail, New Object() {Nothing, EventArgs.Empty})
        Application.DoEvents()
    End Sub

    ' Manage Journals (its Click handler in Journal, Preprint & Links): the
    ' first journal deleted (its Delete handler; the watcher answers the
    ' question), then the window closed.
    Private Shared Sub DeleteFirstJournalInManageJournals(links As ManuscriptLinksForm)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim opened As Integer = 0
        Using library As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of JournalLibraryForm)(
            Sub(window)
                opened += 1
                DirectCast(GetType(JournalLibraryForm).GetField("lstJournals", flags).GetValue(window), ListBox).SelectedIndex = 0
                GetType(JournalLibraryForm).GetMethod("DeleteSelected", flags).Invoke(window, New Object() {Nothing, EventArgs.Empty})
                window.DialogResult = DialogResult.Cancel
            End Sub)
            InvokeLinks(links, "ManageJournals")
        End Using
        Assert.AreEqual(1, opened, "Manage Journals opened once.")
    End Sub

    ' The submission at index selected and deleted (its Delete handler);
    ' the watcher answers the question.
    Private Shared Sub DeleteSubmissionAt(editor As EditManuscriptForm, index As Integer)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim list As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstSubmissions", flags).GetValue(editor), ListBox)
        list.SelectedIndex = index
        Application.DoEvents()
        GetType(EditManuscriptForm).GetMethod("DeleteSelectedSubmission", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
        Application.DoEvents()
    End Sub

    ' Add Decision in the inline submission pane (its Click handler); the
    ' window gets the decision, then Save Decision.
    Private Shared Sub AddInlineDecision(board As PageBoard, decisionKind As EditorialDecision)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
        Dim opened As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddDecisionForm)(
            Sub(dialog)
                opened += 1
                Dim decision As ComboBox = DirectCast(GetType(AddDecisionForm).GetField("cmbDecision", flags).GetValue(dialog), ComboBox)
                decision.SelectedItem = decision.Items.Cast(Of Object)().Single(Function(item) CType(item.GetType().GetProperty("Value").GetValue(item), EditorialDecision) = decisionKind)
                GetType(AddDecisionForm).GetMethod("SaveDecision", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
            End Sub)
            GetType(SubmissionDetailsForm).GetMethod("AddDecision", flags).Invoke(detail, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "The decision window opened once.")
    End Sub

    ' A sample linked to the journal record given.
    Private Shared Function LinkedTo(journal As JournalRecord) As Manuscript
        Dim manuscript As Manuscript = Sample("Linked manuscript")
        manuscript.TargetJournal = journal.Name
        manuscript.TargetJournalId = journal.Id
        Return manuscript
    End Function

    ' The journals in the page's Journal Library and, with inWindow, in the
    ' one Journal, Preprint & Links reads.
    Private Shared Sub SeedJournals(board As PageBoard, inWindow As Boolean, ParamArray journals As JournalRecord())
        Dim library As New AuthorLibraryData()
        library.Journals.AddRange(journals)
        Dim page As New AuthorLibraryRepository(board.AuthorDirectory)
        page.Save(library)
        If inWindow Then
            Dim window As New AuthorLibraryRepository()
            window.Save(library)
        End If
    End Sub

    Private Shared Sub InvokeLinks(dialog As ManuscriptLinksForm, name As String)
        GetType(ManuscriptLinksForm).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
    End Sub

    ' Add submission (its Click handler): the window gets journal and
    ' submitted, then Save Submission.
    Private Shared Sub AddOneSubmission(editor As EditManuscriptForm, journal As String, submitted As DateTime)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim opened As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
            Sub(dialog)
                opened += 1
                DirectCast(GetType(AddSubmissionForm).GetField("txtJournal", flags).GetValue(dialog), TextBox).Text = journal
                DirectCast(GetType(AddSubmissionForm).GetField("dtpSubmitted", flags).GetValue(dialog), DateTimePicker).Value = submitted
                GetType(AddSubmissionForm).GetMethod("SaveSubmission", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
            End Sub)
            GetType(EditManuscriptForm).GetMethod("AddSubmission", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "The submission window opened once.")
    End Sub

    ' Submitted to First Journal on the day it entered the stage.
    Private Shared Function SubmittedToFirstJournal() As Manuscript
        Dim manuscript As Manuscript = Sample("Submitted manuscript")
        manuscript.CurrentStage = PaperStage.Submitted
        manuscript.StageEnteredDate = New DateTime(2026, 1, 5)
        manuscript.TargetJournal = "First Journal"
        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)})
        Return manuscript
    End Function

    ' The page for one manuscript under a storage root of the test's own,
    ' with the scripted network (none unless given) and a watcher that
    ' closes message boxes, answering Yes to the one titled answerYes. Any
    ' message box the steps leave in the watcher fails the test.
    Private Shared Sub WithTypedFieldsPage(
        manuscript As Manuscript,
        steps As Action(Of PageBoard, Manuscript, DialogWatcher),
        Optional network As Net.Http.HttpMessageHandler = Nothing,
        Optional answerYes As String = Nothing,
        Optional seed As Action(Of PageBoard) = Nothing)

        RunOnStaThread(
            Sub()
                Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))
                StorageEnvironment.RootOverride = Path.Combine(root, "storage")
                OnlineAccess.ResetForTests()
                Dim handler As Net.Http.HttpMessageHandler = If(network, New NoNetwork())
                OnlineAccess.InnerHandlerFactory = Function() handler
                Try
                    Using board As New PageBoard()
                        board.Prepare(manuscript)
                        seed?.Invoke(board)
                        board.Open(manuscript)
                        Using dialogs As New DialogWatcher(answerYes)
                            steps(board, manuscript, dialogs)
                            If answerYes Is Nothing Then
                                CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                            End If
                        End Using
                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                    StorageEnvironment.RootOverride = Nothing
                    Try
                        Directory.Delete(root, True)
                    Catch
                    End Try
                End Try
            End Sub)
    End Sub

    Private Shared Function WithCrossrefDoi(manuscript As Manuscript) As Manuscript
        manuscript.Metadata = New ManuscriptMetadata With {.Doi = CrossrefWork.Doi}
        Return manuscript
    End Function

    ' Draft after a desk rejection at First Journal, which cleared the target.
    Private Shared Function RejectedAtFirstJournal() As Manuscript
        Dim manuscript As Manuscript = Sample("Rejected manuscript")
        manuscript.CurrentStage = PaperStage.Draft
        manuscript.StageEnteredDate = New DateTime(2026, 1, 12)
        manuscript.TargetJournal = String.Empty
        Dim submission As New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)}
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
        manuscript.Submissions.Add(submission)
        Return manuscript
    End Function

    ' DOI & Crossref Metadata: Look Up; authors unticked, then choose sets
    ' the boxes, then Apply Selected.
    Private Shared Sub RunCrossrefLookup(editor As EditManuscriptForm, choose As Action(Of CrossrefLookupForm))
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim opened As Integer = 0
        Dim applied As Boolean = False
        Using lookup As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of CrossrefLookupForm)(
            Sub(dialog)
                opened += 1
                Dim field As Func(Of String, Object) = Function(name) GetType(CrossrefLookupForm).GetField(name, flags).GetValue(dialog)
                DirectCast(field("btnLookup"), Button).PerformClick()
                Dim apply As Button = DirectCast(field("btnApply"), Button)
                Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                While (field("_suggestion") Is Nothing OrElse Not apply.Enabled) AndAlso waited.Elapsed < TimeSpan.FromSeconds(10)
                    Application.DoEvents()
                    Thread.Sleep(1)
                End While
                If field("_suggestion") Is Nothing OrElse Not apply.Enabled Then
                    dialog.DialogResult = DialogResult.Cancel
                    Return
                End If
                LookupBox(dialog, "chkAuthors").Checked = False
                choose(dialog)
                applied = True
                apply.PerformClick()
            End Sub)
            GetType(EditManuscriptForm).GetMethod("OpenCrossrefMetadata", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "DOI & Crossref Metadata opened once.")
        Assert.IsTrue(applied, "The scripted lookup returned a work and Apply Selected was chosen.")
    End Sub

    Private Shared Function LookupBox(dialog As CrossrefLookupForm, name As String) As CheckBox
        Return DirectCast(GetType(CrossrefLookupForm).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), CheckBox)
    End Function

    Private Shared Function LinksBox(dialog As ManuscriptLinksForm, name As String) As TextBox
        Return DirectCast(GetType(ManuscriptLinksForm).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox)
    End Function

    ' The first submission selected and edited (its Edit handler); change
    ' edits the window, then Save Changes.
    Private Shared Sub EditFirstSubmission(editor As EditManuscriptForm, change As Action(Of AddSubmissionForm))
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim list As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstSubmissions", flags).GetValue(editor), ListBox)
        list.SelectedIndex = 0
        Application.DoEvents()
        Dim opened As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
            Sub(dialog)
                opened += 1
                change?.Invoke(dialog)
                GetType(AddSubmissionForm).GetMethod("SaveSubmission", flags).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
            End Sub)
            GetType(EditManuscriptForm).GetMethod("EditSelectedSubmission", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "The submission window opened once.")
    End Sub

    ' Journal, Preprint & Links (its Click handler), answered by respond.
    Private Shared Sub AnswerJournalLinks(editor As EditManuscriptForm, respond As Action(Of ManuscriptLinksForm))
        Dim opened As Integer = 0
        Using links As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of ManuscriptLinksForm)(
            Sub(dialog)
                opened += 1
                respond(dialog)
            End Sub)
            GetType(EditManuscriptForm).GetMethod("OpenJournalLinks", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "Journal, Preprint & Links opened once.")
    End Sub

End Class
