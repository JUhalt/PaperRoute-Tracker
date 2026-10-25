Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The ways into and out of the File Drawer from a Board card (#202, #205)
' and the card's suggestion to file (#203): what each writes to the
' manuscript and its history, and what the card then says, on the real
' main window over in-memory records, with saves counted and not written.
' A submission recorded on the manuscript page is the third way out
' (#207).
<TestClass>
<DoNotParallelize>
Public Class FileDrawerTests

    Private _root As String = String.Empty

    Private Shared ReadOnly FiledOn As New DateTime(2026, 8, 17)

    Private Const Reason As String = "Three rejections; revisit with a larger sample."

    Private Const ConsiderFiling As String = "consider filing"

    ' What the main window's status line says once a save has failed.
    Private Const SaveFailedWarning As String = "Warning: latest changes were not saved."

    ' The main window's fields resolve the storage roots as it is built;
    ' the override keeps them off the user's profile.
    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub

    ' Move to File Drawer asks once (#205). Cancel, with nothing typed or
    ' with a reason typed, leaves the manuscript where it was and saves
    ' nothing.
    <TestMethod>
    Public Sub CancelOnMoveToFileDrawerLeavesTheManuscriptWhereItWas()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim manuscript As Manuscript = RejectedThreeTimes()
                    Dim before As String = Snapshot(manuscript)
                    board.Prepare(manuscript)

                    For Each typed As String In {String.Empty, "Not worth another round."}
                        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of MoveToFileDrawerForm)(
                            Sub(dialog)
                                ReasonBox(dialog).Text = typed
                                DirectCast(dialog.CancelButton, Button).PerformClick()
                            End Sub)
                            MenuItem(board.Card(manuscript.Title), "Move to File Drawer...").PerformClick()
                        End Using
                        Application.DoEvents()

                        Assert.AreEqual(ManuscriptLocation.Pipeline, manuscript.Location, "Cancelled with '" & typed & "' typed: still on the Pipeline.")
                        Assert.AreEqual(before, Snapshot(manuscript), "Cancelled with '" & typed & "' typed: nothing changes.")
                        Assert.AreEqual(0, board.SaveCount, "Cancelled: nothing is saved.")
                    Next

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' OK with no reason files the manuscript without one; OK with a reason
    ' files it with that reason in the field and the history note, as
    ' before (#205).
    <TestMethod>
    Public Sub MoveToFileDrawerFilesWithTheReasonGivenOrWithoutOne()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim withoutReason As Manuscript = RejectedThreeTimes()
                    Dim withReason As Manuscript = RejectedThreeTimes()
                    withReason.Title = "Rejected three times, with a reason"
                    board.Prepare(withoutReason, withReason)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of MoveToFileDrawerForm)(
                        Sub(dialog) DirectCast(dialog.AcceptButton, Button).PerformClick())
                        MenuItem(board.Card(withoutReason.Title), "Move to File Drawer...").PerformClick()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.FileDrawer, withoutReason.Location)
                    Assert.IsTrue(withoutReason.FileDrawerDate.HasValue, "Filed today.")
                    Assert.AreEqual(String.Empty, withoutReason.FileDrawerReason)
                    Assert.AreEqual("Moved to File Drawer.", withoutReason.History.Last().Note)
                    Assert.AreEqual(1, board.SaveCount)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of MoveToFileDrawerForm)(
                        Sub(dialog)
                            ReasonBox(dialog).Text = "  Not worth another round.  "
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        MenuItem(board.Card(withReason.Title), "Move to File Drawer...").PerformClick()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.FileDrawer, withReason.Location)
                    Assert.AreEqual("Not worth another round.", withReason.FileDrawerReason)
                    Assert.AreEqual("Moved to File Drawer. Reason: Not worth another round.", withReason.History.Last().Note)
                    Assert.AreEqual(2, board.SaveCount)
                    Assert.IsNotNull(MenuItem(board.Card(withReason.Title), "Restore to Pipeline"), "The card now offers the File Drawer's menu.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The card reads "3 rejections; consider filing" while the latest
    ' submission ended in a rejection, at the default threshold of three,
    ' and not once a newer submission is open (#203).
    <TestMethod>
    Public Sub TheCardSuggestsFilingOnlyWhileTheLatestRouteIsClosed()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim rejected As Manuscript = RejectedThreeTimes()
                    board.Prepare(rejected)
                    Assert.AreEqual("3 rejections; consider filing", StatusLine(board.Card(rejected.Title)), "Three rejections, the latest submission closed.")

                    rejected.Submissions.Add(New JournalSubmission With {.JournalName = "A fourth journal", .SubmittedDate = DateTime.Today.AddDays(-10)})
                    rejected.CurrentStage = PaperStage.Submitted
                    rejected.StageEnteredDate = DateTime.Today.AddDays(-10)
                    board.Render()
                    Assert.IsFalse(StatusLine(board.Card(rejected.Title)).Contains(ConsiderFiling), "A newer submission is open: " & StatusLine(board.Card(rejected.Title)))

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Restored from the File Drawer after its last rejection, a manuscript
    ' is not asked to consider filing again (#203): the researcher just
    ' decided otherwise.
    <TestMethod>
    Public Sub ARevivedManuscriptIsNotAskedToConsiderFilingAgain()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim revived As Manuscript = RejectedThreeTimes()
                    revived.Location = ManuscriptLocation.FileDrawer
                    revived.FileDrawerDate = FiledOn
                    revived.FileDrawerReason = Reason
                    board.Prepare(revived)

                    MenuItem(board.Card(revived.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, revived.Location)
                    Assert.IsFalse(StatusLine(board.Card(revived.Title)).Contains(ConsiderFiling), "Just revived: " & StatusLine(board.Card(revived.Title)))

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A manuscript filed by a spreadsheet import, or in the example library,
    ' has a filed date and reason but no "Moved to File Drawer" note (#202):
    ' the restore note is the only place the reason survives, and Route View
    ' shows it. Nothing else in the manuscript changes.
    <TestMethod>
    Public Sub RestoreKeepsWhenAndWhyAManuscriptFiledByImportWasFiled()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim filed As Manuscript = FiledByImport(Reason)
                    Dim before As String = Snapshot(filed)
                    board.Prepare(filed)

                    MenuItem(board.Card(filed.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
                    Assert.IsFalse(filed.FileDrawerDate.HasValue, "Back on the Pipeline, the filed date is cleared.")
                    Assert.AreEqual(String.Empty, filed.FileDrawerReason, "Back on the Pipeline, the reason is cleared.")
                    Assert.AreEqual(1, board.SaveCount)

                    Dim restored As HistoryEvent = filed.History.Last()
                    Assert.AreEqual("Restored from File Drawer to active Pipeline (filed Aug 17, 2026: Three rejections; revisit with a larger sample.)", restored.Note)
                    Assert.AreEqual(PaperStage.Draft, restored.Stage)
                    Assert.IsTrue(restored.RecordedAtUtc.HasValue)

                    Dim route As ManuscriptRoute = ManuscriptRouteProjectionService.Project(filed)
                    Assert.IsTrue(route.Waypoints.Any(Function(point) point.Kind = ManuscriptRouteWaypointKind.Stage AndAlso point.Note = restored.Note),
                                  "Route View shows the restore note.")

                    ' The restore undone, the record reads as it did.
                    filed.History.Remove(restored)
                    filed.Location = ManuscriptLocation.FileDrawer
                    filed.FileDrawerDate = FiledOn
                    filed.FileDrawerReason = Reason
                    Assert.AreEqual(before, Snapshot(filed), "Nothing else in the manuscript changes.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub RestoreKeepsTheDateOfAManuscriptFiledWithoutAReason()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim filed As Manuscript = FiledByImport(String.Empty)
                    board.Prepare(filed)

                    MenuItem(board.Card(filed.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
                    Assert.AreEqual("Restored from File Drawer to active Pipeline (filed Aug 17, 2026).", filed.History.Last().Note)
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Recording a submission is the third way out of the File Drawer
    ' (#207). Record Submission... on the manuscript page, answered as it
    ' opens and saved with the page's own Save, takes a filed manuscript
    ' out with when and why it was filed in its note, with a reason and
    ' without one.
    <TestMethod>
    Public Sub RecordSubmissionOnThePageKeepsWhenAndWhyTheManuscriptWasFiled()
        StaRunner.Run(
            Sub()
                For Each filedReason As String In {Reason, String.Empty}
                    Dim filed As Manuscript = FiledByImport(filedReason)
                    Dim before As String = Snapshot(filed)

                    OnThePage(filed,
                        Sub(editor)
                            Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
                                Sub(dialog) dialog.AcceptButton.PerformClick())
                                CallPrivate(editor, "AddSubmission", Nothing, EventArgs.Empty)
                            End Using
                            Assert.IsTrue(editor.CommitChanges(), "Saved.")
                        End Sub)

                    AssertLeftForASubmission(filed, before, filedReason)
                Next
            End Sub)
    End Sub

    ' The same from a stage change: Submitted asks to record the
    ' submission, Yes records it, and Save completes the change.
    <TestMethod>
    Public Sub AStageChangeToSubmittedKeepsWhenAndWhyTheManuscriptWasFiled()
        StaRunner.Run(
            Sub()
                For Each filedReason As String In {Reason, String.Empty}
                    Dim filed As Manuscript = FiledByImport(filedReason)
                    Dim before As String = Snapshot(filed)

                    OnThePage(filed,
                        Sub(editor)
                            ViewCatalog.Descendants(editor).OfType(Of ComboBox)().Single(Function(box) box.AccessibleName = "Current stage").SelectedItem = PaperStage.Submitted
                            Using dialogs As New ManuscriptPageTests.DialogWatcher(answerYes:="Submission Details Required"),
                                  answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
                                      Sub(dialog) dialog.AcceptButton.PerformClick())
                                Assert.IsTrue(editor.CommitChanges(), "Saved with the stage change.")
                                CollectionAssert.AreEqual({"Submission Details Required"}, dialogs.Closed, "The stage change asked to record the submission, and nothing else.")
                            End Using
                        End Sub)

                    AssertLeftForASubmission(filed, before, filedReason)
                Next
            End Sub)
    End Sub

    ' The same from a prepared packet: its submission is recorded through
    ' SubmissionPacketWorkflowService, on a copy of the manuscript, and
    ' the packet is linked to it.
    <TestMethod>
    Public Sub RecordingAPacketsSubmissionKeepsWhenAndWhyTheManuscriptWasFiled()
        StaRunner.Run(
            Sub()
                For Each filedReason As String In {Reason, String.Empty}
                    Dim filed As Manuscript = FiledByImport(filedReason)
                    Dim version As New ManuscriptVersion With {.Label = "Exact submitted version"}
                    filed.Versions.Add(version)
                    filed.CurrentVersionId = version.Id
                    Dim packet As SubmissionPacket = SubmissionPacketService.CreatePacket(filed, version.Id, "Prepared packet", "Preparation only")
                    Dim before As String = Snapshot(filed)

                    OnThePage(filed,
                        Sub(editor)
                            Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddSubmissionForm)(
                                Sub(dialog) dialog.AcceptButton.PerformClick())
                                CallPrivate(editor, "RecordPreparedPacketSubmission", New SubmissionWorkflowRequest With {
                                    .Target = SubmissionWorkflowTarget.RecordSubmission,
                                    .PacketId = packet.Id
                                })
                            End Using
                            Assert.IsTrue(editor.CommitChanges(), "Saved.")
                        End Sub)

                    Dim linked As SubmissionPacket = filed.SubmissionPackets.Single()
                    Assert.AreEqual(filed.Submissions.Single().Id, linked.SubmissionId.Value, "The packet is linked to the submission.")
                    linked.SubmissionId = Nothing
                    linked.LastModifiedAtUtc = Nothing

                    AssertLeftForASubmission(filed, before, filedReason)
                Next
            End Sub)
    End Sub

    ' A decision dated after the manuscript was filed, recorded in the
    ' Submissions tab's detail pane, takes it out the same way, and Save
    ' with the Overview showing keeps the reason cleared (#207).
    <TestMethod>
    Public Sub ADecisionOnThePageKeepsWhenAndWhyTheManuscriptWasFiled()
        StaRunner.Run(
            Sub()
                Dim filed As Manuscript = FiledByImport(Reason)
                filed.CurrentStage = PaperStage.Submitted
                filed.StageEnteredDate = New DateTime(2026, 7, 1)
                filed.Submissions.Add(New JournalSubmission With {.JournalName = "A fictional journal", .SubmittedDate = filed.StageEnteredDate})

                OnThePage(filed,
                    Sub(editor)
                        Dim detail As SubmissionDetailsForm = DirectCast(GetType(EditManuscriptForm).GetProperty("_submissionDetail", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), SubmissionDetailsForm)
                        Assert.IsNotNull(detail, "The submission's details are on the page.")
                        CallPrivate(detail, "RecordDecision", New EditorialDecisionEvent With {.Decision = EditorialDecision.Rejected, .DecisionDate = New DateTime(2026, 9, 1)})
                        Assert.IsTrue(editor.CommitChanges(), "Saved.")
                    End Sub)

                Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
                Assert.AreEqual(PaperStage.Draft, filed.CurrentStage)
                Assert.IsFalse(filed.FileDrawerDate.HasValue, "Out of the File Drawer, the filed date is cleared.")
                Assert.AreEqual(String.Empty, filed.FileDrawerReason, "Out of the File Drawer, the reason is cleared.")
                Assert.AreEqual(2, filed.History.Count, "One note is added.")
                Assert.AreEqual("Returned to the Pipeline by an editorial decision (filed Aug 17, 2026: Three rejections; revisit with a larger sample.)", filed.History.Last().Note)
            End Sub)
    End Sub

    ' A reason typed on the Overview of a filed manuscript, with another
    ' tab then showing, is still an unsaved change, goes into a recovery
    ' draft, and is written by Save: the Overview hidden hides the File
    ' Drawer group with it, and that is not what decides.
    <TestMethod>
    Public Sub AReasonTypedOnTheOverviewIsSavedFromAnotherTab()
        StaRunner.Run(
            Sub()
                Const Typed As String = "Revisit once the replication is in."
                Dim filed As Manuscript = FiledByImport(Reason)

                OnThePage(filed,
                    Sub(editor)
                        Dim box As TextBox = ReasonBox(editor)
                        Assert.IsTrue(box.Visible, "The reason shows on the Overview.")
                        box.Text = Typed
                        ViewCatalog.Descendants(editor).OfType(Of ShelfTabButton)().Single(Function(tab) tab.Text = "Submissions").Checked = True
                        Application.DoEvents()
                        Assert.IsFalse(box.Visible, "The Submissions tab hides the Overview.")

                        Assert.IsTrue(editor.HasUnsavedChanges(), "The typed reason is unsaved.")
                        Assert.AreEqual(Typed, editor.RecoveryCopy().FileDrawerReason, "A recovery draft keeps it.")
                        Assert.IsTrue(editor.CommitChanges(), "Saved.")
                        Assert.IsFalse(editor.HasUnsavedChanges(), "Nothing is left unsaved.")
                    End Sub)

                Assert.AreEqual(ManuscriptLocation.FileDrawer, filed.Location)
                Assert.AreEqual(Typed, filed.FileDrawerReason)
                Assert.AreEqual("File Drawer reason updated. Reason: " & Typed, filed.History.Last().Note)
            End Sub)
    End Sub

    ' The status line, which Narrator reads as it changes, says what Move,
    ' Restore and Delete did to the card (#225), as Done and Mark Published
    ' already did. Before the change it said only "Saved locally".
    <TestMethod>
    Public Sub TheStatusLineSaysWhatMoveRestoreAndDeleteDid()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim manuscript As Manuscript = RejectedThreeTimes()
                    board.Prepare(manuscript)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of MoveToFileDrawerForm)(
                        Sub(dialog) DirectCast(dialog.AcceptButton, Button).PerformClick())
                        MenuItem(board.Card(manuscript.Title), "Move to File Drawer...").PerformClick()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.FileDrawer, manuscript.Location)
                    Assert.AreEqual("Moved ""Rejected three times"" to the File Drawer.", board.Status)

                    MenuItem(board.Card(manuscript.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, manuscript.Location)
                    Assert.AreEqual("Restored ""Rejected three times"" to the Pipeline.", board.Status)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of DeleteManuscriptForm)(AddressOf ConfirmDeletion)
                        MenuItem(board.Card(manuscript.Title), "Delete...").PerformClick()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(0, board.Library.Count, "Deleted.")
                    Assert.AreEqual("Deleted ""Rejected three times"".", board.Status)
                    Assert.AreEqual(3, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Add Manuscript says what it added (#225), and names the shelf when
    ' the card lands anywhere but the Pipeline. The window adds only to
    ' the Pipeline today, so the second record is moved to Published as
    ' the window hands it over, to stand for one that does not.
    <TestMethod>
    Public Sub TheStatusLineSaysWhatAddManuscriptAdded()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    board.Prepare()

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddManuscriptForm)(
                        Sub(dialog)
                            TitleBox(dialog).Text = "A new study"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        board.Add()
                    End Using

                    Assert.AreEqual("A new study", board.Library.Single().Title)
                    Assert.AreEqual("Added ""A new study"".", board.Status)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddManuscriptForm)(
                        Sub(dialog)
                            TitleBox(dialog).Text = "A published study"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                            dialog.CreatedManuscript.Location = ManuscriptLocation.Published
                        End Sub)
                        board.Add()
                    End Using

                    Assert.AreEqual(2, board.Library.Count)
                    Assert.AreEqual("Added ""A published study"" to Published.", board.Status)
                    Assert.AreEqual(2, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A save that fails says so on the status line, and the action that
    ' asked for it leaves that warning there (#225). A guard: before the
    ' change no action wrote the status line after its save, so this
    ' passed then too.
    <TestMethod>
    Public Sub AnActionWhoseSaveFailsLeavesTheSaveWarning()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim active As Manuscript = RejectedThreeTimes()
                    Dim filed As Manuscript = FiledByImport(Reason)
                    Dim deleted As Manuscript = RejectedThreeTimes()
                    deleted.Title = "Deleted after a failed save"
                    board.Prepare(active, filed, deleted)
                    board.SaveFails = True

                    board.SetStatus("Before the action.")
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of MoveToFileDrawerForm)(
                        Sub(dialog) DirectCast(dialog.AcceptButton, Button).PerformClick())
                        MenuItem(board.Card(active.Title), "Move to File Drawer...").PerformClick()
                    End Using
                    Application.DoEvents()
                    Assert.AreEqual(SaveFailedWarning, board.Status, "Move to File Drawer.")

                    board.SetStatus("Before the action.")
                    MenuItem(board.Card(filed.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual(SaveFailedWarning, board.Status, "Restore to Pipeline.")

                    board.SetStatus("Before the action.")
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of DeleteManuscriptForm)(AddressOf ConfirmDeletion)
                        MenuItem(board.Card(deleted.Title), "Delete...").PerformClick()
                    End Using
                    Application.DoEvents()
                    Assert.AreEqual(SaveFailedWarning, board.Status, "Delete.")

                    board.SetStatus("Before the action.")
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AddManuscriptForm)(
                        Sub(dialog)
                            TitleBox(dialog).Text = "Added before a failed save"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        board.Add()
                    End Using
                    Assert.AreEqual(SaveFailedWarning, board.Status, "Add Manuscript.")

                    Assert.AreEqual(4, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The status line a start leaves counts the library in plain words
    ' (#225), not "manuscript(s)".
    <TestMethod>
    Public Sub TheStartupLineCountsTheLibraryInPlainWords()
        StaRunner.Run(
            Sub()
                For Each count As Integer In {1, 2}
                    Call New ManuscriptRepository().Save(
                        Enumerable.Range(1, count).Select(Function(index) New Manuscript With {.Title = "Study " & index.ToString()}).ToList())

                    Using board As New FileDrawerBoard()
                        Assert.AreEqual(
                            If(count = 1,
                               "Loaded 1 manuscript from local storage.",
                               "Loaded 2 manuscripts from local storage."),
                            board.LoadLibrary())
                    End Using
                Next
            End Sub)
    End Sub

    ' Out of the File Drawer by a new submission (#207): on the Pipeline as
    ' Submitted, the filed date and reason cleared and kept in one note,
    ' dated with the submission in the stage the manuscript was filed in,
    ' which Route View shows. With the submission undone, the record reads
    ' as it did.
    Private Shared Sub AssertLeftForASubmission(filed As Manuscript, before As String, filedReason As String)
        Dim submission As JournalSubmission = filed.Submissions.Single()
        Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
        Assert.AreEqual(PaperStage.Submitted, filed.CurrentStage)
        Assert.IsFalse(filed.FileDrawerDate.HasValue, "Out of the File Drawer, the filed date is cleared.")
        Assert.AreEqual(String.Empty, filed.FileDrawerReason, "Out of the File Drawer, the reason is cleared.")
        Assert.AreEqual(2, filed.History.Count, "One note is added.")

        Dim returned As HistoryEvent = filed.History.Last()
        Assert.AreEqual(If(filedReason.Length = 0,
                           "Returned to the Pipeline by a new submission (filed Aug 17, 2026).",
                           "Returned to the Pipeline by a new submission (filed Aug 17, 2026: Three rejections; revisit with a larger sample.)"),
                        returned.Note)
        Assert.AreEqual(PaperStage.Draft, returned.Stage, "The stage it was filed in.")
        Assert.AreEqual(submission.SubmittedDate.Date, returned.EventDate, "Dated with the submission.")
        Assert.IsTrue(returned.RecordedAtUtc.HasValue)

        Dim route As ManuscriptRoute = ManuscriptRouteProjectionService.Project(filed)
        Assert.IsTrue(route.Waypoints.Any(Function(point) point.Kind = ManuscriptRouteWaypointKind.Stage AndAlso point.Note = returned.Note),
                      "Route View shows the note.")

        filed.History.Remove(returned)
        filed.Submissions.Remove(submission)
        filed.Location = ManuscriptLocation.FileDrawer
        filed.FileDrawerDate = FiledOn
        filed.FileDrawerReason = filedReason
        filed.CurrentStage = PaperStage.Draft
        filed.StageEnteredDate = New DateTime(2026, 3, 2)
        filed.TargetJournal = "A fictional journal"
        filed.TargetJournalId = Nothing
        Assert.AreEqual(before, Snapshot(filed), "Nothing else in the manuscript changes.")
    End Sub

    ' The manuscript page on the record, over the temporary root, closed
    ' when the test is done with it.
    Private Sub OnThePage(manuscript As Manuscript, use As Action(Of EditManuscriptForm))
        Using editor As New EditManuscriptForm(manuscript, {manuscript}, New AuthorLibraryRepository(Path.Combine(_root, "authors")), pageMode:=True)
            ViewCatalog.ShowOffscreen(editor)
            use(editor)
            editor.Close()
        End Using
    End Sub

    Private Shared Function CallPrivate(target As Object, name As String, ParamArray args As Object()) As Object
        Return target.GetType().GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(target, args)
    End Function

    ' Filed as an import writes it: the stage it was in, with no note of
    ' the move.
    Private Shared Function FiledByImport(reason As String) As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Filed by import",
            .Location = ManuscriptLocation.FileDrawer,
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 3, 2),
            .TargetJournal = "A fictional journal",
            .FileDrawerDate = FiledOn,
            .FileDrawerReason = reason
        }
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Draft, .EventDate = manuscript.StageEnteredDate, .Note = "Imported from a spreadsheet."})
        Return manuscript
    End Function

    ' On the Pipeline in Draft after three rejections, the last of them long
    ' enough ago that the card's "Rejected N days ago" line has gone, with a
    ' target journal so the card has no other line to show.
    Private Shared Function RejectedThreeTimes() As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Rejected three times",
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = DateTime.Today.AddDays(-150),
            .TargetJournal = "A fictional journal"
        }
        For round As Integer = 3 To 1 Step -1
            Dim submitted As DateTime = DateTime.Today.AddDays(-150 * round - 60)
            Dim submission As New JournalSubmission With {.JournalName = "Journal " & round.ToString(), .SubmittedDate = submitted}
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Rejected, .DecisionDate = submitted.AddDays(60)})
            manuscript.Submissions.Add(submission)
        Next
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Draft, .EventDate = manuscript.StageEnteredDate, .Note = "Back to Draft after the third rejection."})
        Return manuscript
    End Function

    ' The card's status line: what needs attention, otherwise how long the
    ' manuscript has been in its stage.
    Private Shared Function StatusLine(card As Control) As String
        Return If(card.Controls.OfType(Of Label)().
            Where(Function(label) Not TypeOf label Is LinkLabel AndAlso Not TypeOf label Is PillLabel).
            Select(Function(label) label.Text).
            FirstOrDefault(Function(text) text.Contains(ConsiderFiling) OrElse text.Contains("day")), String.Empty)
    End Function

    Private Shared Function ReasonBox(dialog As Form) As TextBox
        Return ViewCatalog.Descendants(dialog).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "File Drawer reason")
    End Function

    Private Shared Function TitleBox(dialog As AddManuscriptForm) As TextBox
        Return DirectCast(GetType(AddManuscriptForm).GetField("txtTitle", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox)
    End Function

    ' DELETE typed, then Delete Manuscript, as the window asks.
    Private Shared Sub ConfirmDeletion(dialog As DeleteManuscriptForm)
        ViewCatalog.Descendants(dialog).OfType(Of TextBox)().Single().Text = "DELETE"
        ViewCatalog.Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Delete Manuscript").PerformClick()
    End Sub

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private Shared Function MenuItem(card As Control, text As String) As ToolStripItem
        Return card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Single(Function(item) item.Text = text)
    End Function

    ' The real main window on in-memory records, as ViewCatalog builds it:
    ' Load is skipped and the window never activates. A save is counted, not
    ' written; the record's change is what these tests read.
    Private NotInheritable Class FileDrawerBoard
        Inherits Form1

        Public SaveCount As Integer

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        ' A save that fails writes the status line's warning, as the real
        ' one's failure does (#225). The flag that would then hold the
        ' warning there (#113) stays unset, so the line keeps it only while
        ' nothing after the save writes over it.
        Public SaveFails As Boolean

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            If SaveFails Then
                StatusLabel().Text = SaveFailedWarning
                Return False
            End If
            Return True
        End Function

        Public ReadOnly Property Status As String
            Get
                Return StatusLabel().Text
            End Get
        End Property

        Public Sub SetStatus(text As String)
            StatusLabel().Text = text
        End Sub

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), List(Of Manuscript))
            End Get
        End Property

        ' + Add Manuscript, answered by the test's ModalAnswer.
        Public Sub Add()
            CallPrivate("AddManuscript", Nothing, EventArgs.Empty)
            Application.DoEvents()
        End Sub

        ' A start's read of the library under the test's root, without the
        ' rest of Load; the status line it leaves.
        Public Function LoadLibrary() As String
            CallPrivate("BuildInterface")
            Assert.IsTrue(CBool(GetType(Form1).GetMethod("LoadManuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)), "The library loads.")
            Return Status
        End Function

        Private Function StatusLabel() As Label
            Return DirectCast(GetType(Form1).GetField("lblStatus", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label)
        End Function

        Public Sub Prepare(ParamArray records As Manuscript())
            CallPrivate("BuildInterface")
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, records.ToList())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public Sub Render()
            CallPrivate("RenderManuscripts")
            Application.DoEvents()
        End Sub

        ' The card with this title, on whichever shelf it is.
        Public Function Card(title As String) As RoundedPanel
            Return {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.
                SelectMany(Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control).Controls.OfType(Of RoundedPanel)()).
                Single(Function(panel) panel.AccessibleName = title)
        End Function

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub
    End Class

End Class
