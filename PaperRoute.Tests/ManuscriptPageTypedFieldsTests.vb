Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' What is typed in the Overview's Title and Target journal boxes reaches the
' working copy only at Save. A window or a workflow edit that refreshes those
' boxes from the working copy in between must show, and Save must write, what
' was typed, unless that step itself sets the value (v1.0.1).
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

    ' A target journal typed after a rejection survives an edit of the
    ' rejected submission: the rejection clears only a target that is its
    ' own journal.
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

    ' An edit that changes the active submission's journal still sets the
    ' target journal to it, over what was typed, as before.
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
    ' typed target journal is still in the box afterwards, and the next Save
    ' writes it.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesAStageChangeSaveCouldNotComplete()
        WithTypedFieldsPage(
            RejectedAtFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Second Journal"
                Dim stage As ComboBox = DirectCast(GetType(EditManuscriptForm).GetField("cmbStage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), ComboBox)
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
                Assert.AreEqual(String.Empty, manuscript.TargetJournal, "Nothing is saved.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The next Save completes.")
                Assert.AreEqual("Second Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
            End Sub,
            answerYes:="Editorial Decision Required")
    End Sub

    ' Journal, Preprint & Links shows the typed target journal, and OK after
    ' changing only a preprint DOI keeps it.
    <TestMethod>
    Public Sub TypedTargetJournalIsShownInJournalLinksAndKeptOnOK()
        WithTypedFieldsPage(
            Sample("Linked manuscript"),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                Dim shown As String = Nothing
                AnswerJournalLinks(board.Editor,
                    Sub(dialog)
                        shown = LinksBox(dialog, "txtTargetJournal").Text
                        LinksBox(dialog, "txtPreprintDoi").Text = "10.5555/paperroute.preprint"
                        GetType(ManuscriptLinksForm).GetMethod("SaveChanges", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                    End Sub)

                Assert.AreEqual("Typed Journal", shown, "Journal, Preprint & Links shows the typed target journal.")
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual("A fictional journal", manuscript.TargetJournal, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
                Assert.AreEqual("10.5555/paperroute.preprint", manuscript.Metadata.PreprintDoi, "Save writes the preprint DOI.")
            End Sub)
    End Sub

    ' Typed fields carried into the working copy for a window still count as
    ' unsaved, and Discard returns to the saved values without writing.
    <TestMethod>
    Public Sub TypedFieldsCarriedIntoAWindowStayUnsavedAndDiscardKeepsTheSavedValues()
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

    ' An emptied title box is not carried into the working copy, and Save
    ' still refuses it after a window was opened.
    <TestMethod>
    Public Sub TypedFieldsKeepTheTitleRequiredAfterAWindow()
        WithTypedFieldsPage(
            Sample("Saved title"),
            Sub(board, manuscript, dialogs)
                board.Field("Title").Text = "   "
                AnswerJournalLinks(board.Editor,
                    Sub(dialog) GetType(ManuscriptLinksForm).GetMethod("SaveChanges", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, New Object() {Nothing, EventArgs.Empty}))

                Assert.IsFalse(board.SaveManuscriptPage(), "Save refuses an empty title.")
                CollectionAssert.AreEqual({"Title Required"}, dialogs.Closed)
                Assert.AreEqual(0, board.SaveCount)
                Assert.AreEqual("Saved title", manuscript.Title)
                dialogs.Closed.Clear()
            End Sub)
    End Sub

    ' An edit of the active submission that leaves its journal alone puts
    ' the saved target back in the working copy; the typed target journal
    ' stays in the box and Save writes it.
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

    ' Deleting the last submission clears the target it set, not a target
    ' journal typed since; Save writes the typed one with the Draft stage.
    <TestMethod>
    Public Sub TypedTargetJournalSurvivesDeletingTheLastSubmission()
        WithTypedFieldsPage(
            SubmittedToFirstJournal(),
            Sub(board, manuscript, dialogs)
                board.Field("Target journal").Text = "Typed Journal"
                DeleteFirstSubmission(board.Editor)

                CollectionAssert.AreEqual({"Delete Journal Submission"}, dialogs.Closed, "Delete asked once.")
                Assert.AreEqual("Typed Journal", board.Field("Target journal").Text, "The typed target journal is still in the box.")
                Assert.AreEqual("First Journal", manuscript.TargetJournal, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual("Typed Journal", manuscript.TargetJournal, "Save writes the typed target journal.")
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
                Dim stage As ComboBox = DirectCast(GetType(EditManuscriptForm).GetField("cmbStage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), ComboBox)
                stage.SelectedItem = PaperStage.UnderReview
                EditFirstSubmission(board.Editor, Nothing)

                Assert.AreEqual(PaperStage.UnderReview, CType(stage.SelectedItem, PaperStage), "The chosen stage is still chosen.")
                Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The chosen stage is unsaved.")
                Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage, "Nothing is saved before Save.")

                Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                Assert.AreEqual(PaperStage.UnderReview, manuscript.CurrentStage, "Save moves to the chosen stage.")
            End Sub)
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

    ' The first submission selected and deleted (its Delete handler); the
    ' watcher answers the question.
    Private Shared Sub DeleteFirstSubmission(editor As EditManuscriptForm)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim list As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstSubmissions", flags).GetValue(editor), ListBox)
        list.SelectedIndex = 0
        Application.DoEvents()
        GetType(EditManuscriptForm).GetMethod("DeleteSelectedSubmission", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
        Application.DoEvents()
    End Sub

    ' The page for one manuscript under a storage root of the test's own,
    ' with the scripted network (none unless given) and a watcher that
    ' closes message boxes, answering Yes to the one titled answerYes. Any
    ' message box the steps leave in the watcher fails the test.
    Private Shared Sub WithTypedFieldsPage(
        manuscript As Manuscript,
        steps As Action(Of PageBoard, Manuscript, DialogWatcher),
        Optional network As Net.Http.HttpMessageHandler = Nothing,
        Optional answerYes As String = Nothing)

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
