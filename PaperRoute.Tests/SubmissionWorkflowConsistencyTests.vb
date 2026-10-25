Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class SubmissionWorkflowConsistencyTests

    <TestMethod>
    Public Sub SeededSubmission_UnchangedSnapshotPreservesJournalIdWithoutLibraryLookup()
        Dim journalId As Guid = Guid.NewGuid()
        Const journalName As String = "Historical journal snapshot name"
        Const portal As String = "https://journal.example.invalid/submissions"

        RunOnStaThread(
            Sub()
                ' The arbitrary ID deliberately has no library fixture. An unchanged
                ' seeded name must retain its identity without loading user journals.
                Using dialog As New NonactivatingSubmissionForm(journalName, journalId, portal)
                    dialog.ShowInTaskbar = False
                    dialog.Opacity = 0
                    dialog.StartPosition = FormStartPosition.Manual
                    dialog.Location = New Point(-20000, -20000)
                    dialog.Show()
                    Application.DoEvents()
                    Assert.IsNull(dialog.CreatedSubmission)
                    dialog.AcceptButton.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.IsNotNull(dialog.CreatedSubmission)
                    Assert.AreEqual(journalId, dialog.CreatedSubmission.JournalId.Value)
                    Assert.AreEqual(journalName, dialog.CreatedSubmission.JournalName)
                    Assert.AreEqual(portal, dialog.CreatedSubmission.PortalUrl)
                    Assert.AreEqual(0, dialog.CreatedSubmission.Decisions.Count)
                    Assert.AreEqual(0, dialog.CreatedSubmission.Correspondence.Count)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    ' Record Submission... on the Submissions tab starts at the target
    ' journal, as recording one from a stage change does (#204); with no
    ' target journal it opens blank, and Edit opens the selected submission
    ' as before. Each dialog is read and cancelled as it opens, so nothing
    ' is recorded and no window is left open.
    <TestMethod>
    Public Sub RecordSubmissionStartsAtTheTargetJournalAndEditOpensTheSelectedOne()
        Dim root As String = CreateTemporaryRoot()
        Try
            RunOnStaThread(
                Sub()
                    StorageEnvironment.RootOverride = root
                    Try
                        Dim manuscript As Manuscript = CreateLinkedManuscript()
                        manuscript.TargetJournal = "Fictional Open Psychology"
                        Dim opened As New List(Of (Title As String, Journal As String))()
                        Using editor As New EditManuscriptForm(manuscript, {manuscript}, New AuthorLibraryRepository(Path.Combine(root, "authors")), pageMode:=True),
                              dialogs As New ModalAnswer(Of AddSubmissionForm)(
                                  Sub(dialog)
                                      opened.Add((dialog.Text, JournalBox(dialog).Text))
                                      dialog.DialogResult = DialogResult.Cancel
                                  End Sub)
                            ViewCatalog.ShowOffscreen(editor)

                            CallPrivate(editor, "AddSubmission")
                            Assert.AreEqual(1, opened.Count, "Record Submission... opened its dialog.")
                            Assert.AreEqual(("Record Journal Submission", "Fictional Open Psychology"), opened.Last(), "Record Submission... starts at the target journal.")

                            Descendants(editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Target journal").Text = String.Empty
                            CallPrivate(editor, "AddSubmission")
                            Assert.AreEqual(2, opened.Count)
                            Assert.AreEqual(("Record Journal Submission", String.Empty), opened.Last(), "With no target journal it opens blank.")

                            DirectCast(GetType(EditManuscriptForm).GetField("lstSubmissions", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), ListBox).SelectedIndex = 0
                            CallPrivate(editor, "EditSelectedSubmission")
                            Assert.AreEqual(3, opened.Count)
                            Assert.AreEqual(("Edit Journal Submission", "Journal A"), opened.Last(), "Edit opens the selected submission as before.")

                            Assert.AreEqual(1, manuscript.Submissions.Count, "Cancelled, the dialogs record nothing.")
                            editor.Close()
                        End Using
                    Finally
                        StorageEnvironment.RootOverride = Nothing
                    End Try
                End Sub)
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    ' Record Submission... keeps the target journal's Journal Library link
    ' and portal (#224), from the Submissions tab and from a stage change
    ' (RecordRequiredSubmission), as Use Library... does: the dialog opens
    ' with the record's portal, the new submission carries the record's id
    ' and portal, and so the target stays linked and its notes stay on the
    ' Overview, also when the page reads them again as a load does.
    <TestMethod>
    <DataRow("AddSubmission")>
    <DataRow("RecordRequiredSubmission")>
    Public Sub RecordSubmissionKeepsTheTargetJournalsLinkAndPortal(record As String)
        Dim root As String = CreateTemporaryRoot()
        Try
            RunOnStaThread(
                Sub()
                    StorageEnvironment.RootOverride = root
                    Try
                        Dim journal As JournalRecord = SeedTargetJournal(LibraryPortal)
                        Dim manuscript As Manuscript = TargetingManuscript(journal)
                        Dim portals As New List(Of String)()
                        Using editor As New EditManuscriptForm(manuscript, {manuscript}, New AuthorLibraryRepository(), pageMode:=True),
                              dialogs As New ModalAnswer(Of AddSubmissionForm)(
                                  Sub(dialog)
                                      portals.Add(PortalBox(dialog).Text)
                                      dialog.AcceptButton.PerformClick()
                                  End Sub)
                            ViewCatalog.ShowOffscreen(editor)
                            Assert.IsTrue(NotesCard(editor).Visible, "The linked target's notes show before.")

                            RecordThrough(editor, record)

                            Assert.AreEqual(1, portals.Count, "The dialog opens once.")
                            Assert.AreEqual(LibraryPortal, portals.Single(), "The dialog opens with the record's portal.")
                            Dim working As Manuscript = WorkingCopy(editor)
                            Dim recorded As JournalSubmission = working.Submissions.Single()
                            Assert.AreEqual(CType(journal.Id, Guid?), recorded.JournalId, "The new submission keeps the Journal Library link.")
                            Assert.AreEqual(LibraryPortal, recorded.PortalUrl, "The new submission keeps the record's portal.")
                            Assert.AreEqual(CType(journal.Id, Guid?), working.TargetJournalId, "The target journal stays linked.")
                            ' Nothing reads the notes again right after a record, so
                            ' this first look passed before the change too (a guard);
                            ' read again, as the next load reads them, they had gone
                            ' with the link.
                            Assert.IsTrue(NotesCard(editor).Visible, "The target journal's notes still show.")
                            CallNoArguments(editor, "RefreshJournalNotes")
                            Assert.IsTrue(NotesCard(editor).Visible, "Read again, the target journal's notes still show.")
                            editor.Close()
                        End Using
                    Finally
                        StorageEnvironment.RootOverride = Nothing
                    End Try
                End Sub)
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    ' The edges of #224: an unlinked target opens with only its name, as
    ' before (a guard: that passed before the change too); a record with
    ' no portal leaves the portal blank; a journal name edited in the
    ' dialog is another journal, so the new submission has neither its
    ' link nor its portal; a name edited and then put back keeps both;
    ' and a portal typed in the dialog stays whatever the name.
    <TestMethod>
    <DataRow("AddSubmission")>
    <DataRow("RecordRequiredSubmission")>
    Public Sub RecordSubmissionLinksOnlyTheTargetJournalAsNamed(record As String)
        Dim root As String = CreateTemporaryRoot()
        Try
            RunOnStaThread(
                Sub()
                    StorageEnvironment.RootOverride = root
                    Try
                        For Each edge As String In {"unlinked", "no portal", "edited name", "name put back", "typed portal"}
                            Dim journal As JournalRecord = SeedTargetJournal(If(edge = "no portal", String.Empty, LibraryPortal))
                            Dim manuscript As Manuscript = TargetingManuscript(journal)
                            If edge = "unlinked" Then manuscript.TargetJournalId = Nothing
                            Dim opened As New List(Of (Journal As String, Portal As String))()
                            Using editor As New EditManuscriptForm(manuscript, {manuscript}, New AuthorLibraryRepository(), pageMode:=True),
                                  dialogs As New ModalAnswer(Of AddSubmissionForm)(
                                      Sub(dialog)
                                          opened.Add((JournalBox(dialog).Text, PortalBox(dialog).Text))
                                          Select Case edge
                                              Case "edited name"
                                                  JournalBox(dialog).Text = "Fictional Open Psychology Letters"
                                              Case "name put back"
                                                  JournalBox(dialog).Text = "Fictional Open Psychology Letters"
                                                  JournalBox(dialog).Text = journal.Name
                                              Case "typed portal"
                                                  PortalBox(dialog).Text = TypedPortal
                                                  JournalBox(dialog).Text = "Fictional Open Psychology Letters"
                                          End Select
                                          dialog.AcceptButton.PerformClick()
                                      End Sub)
                                ViewCatalog.ShowOffscreen(editor)

                                RecordThrough(editor, record)

                                Dim recorded As JournalSubmission = WorkingCopy(editor).Submissions.Single()
                                Select Case edge
                                    Case "unlinked"
                                        Assert.AreEqual((journal.Name, String.Empty), opened.Single(), "Unlinked: only the name, as before.")
                                        Assert.IsFalse(recorded.JournalId.HasValue, "Unlinked: no link.")
                                    Case "no portal"
                                        Assert.AreEqual((journal.Name, String.Empty), opened.Single(), "No portal: the portal stays blank.")
                                        Assert.AreEqual(CType(journal.Id, Guid?), recorded.JournalId, "No portal: the link is kept.")
                                        Assert.AreEqual(String.Empty, recorded.PortalUrl)
                                    Case "edited name"
                                        Assert.AreEqual((journal.Name, LibraryPortal), opened.Single(), "Edited name: it opened at the linked record.")
                                        Assert.IsFalse(recorded.JournalId.HasValue, "Edited name: another journal, so no link.")
                                        Assert.AreEqual("Fictional Open Psychology Letters", recorded.JournalName)
                                        Assert.AreEqual(String.Empty, recorded.PortalUrl, "Edited name: the target's portal went with its name.")
                                    Case "name put back"
                                        Assert.AreEqual(CType(journal.Id, Guid?), recorded.JournalId, "Name put back: the link is kept.")
                                        Assert.AreEqual(LibraryPortal, recorded.PortalUrl, "Name put back: the target's portal came back.")
                                    Case Else
                                        Assert.IsFalse(recorded.JournalId.HasValue, "Typed portal: another journal, so no link.")
                                        Assert.AreEqual(TypedPortal, recorded.PortalUrl, "Typed portal: a portal typed in the dialog stays.")
                                End Select
                                editor.Close()
                            End Using
                        Next
                    Finally
                        StorageEnvironment.RootOverride = Nothing
                    End Try
                End Sub)
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    Private Const LibraryPortal As String = "https://journal.example.invalid/submissions"
    Private Const TypedPortal As String = "https://letters.example.invalid/submit"

    ' A Journal Library record with notes, saved as the temp root's whole
    ' author library, where both the editor and the dialog read it.
    Private Shared Function SeedTargetJournal(portal As String) As JournalRecord
        Assert.IsNotNull(StorageEnvironment.RootOverride, "The author library is the test's own.")
        Dim journal As New JournalRecord With {
            .Name = "Fictional Open Psychology",
            .Notes = "Report exact p values.",
            .SubmissionPortalUrl = portal
        }
        Dim library As New AuthorLibraryData()
        library.Journals.Add(journal)
        Dim repository As New AuthorLibraryRepository()
        repository.Save(library)
        Return journal
    End Function

    Private Shared Function TargetingManuscript(journal As JournalRecord) As Manuscript
        Return New Manuscript With {
            .Title = "Targets a Journal Library record",
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline,
            .TargetJournal = journal.Name,
            .TargetJournalId = journal.Id
        }
    End Function

    ' Record Submission... on the Submissions tab, or the record a stage
    ' change asks for.
    Private Shared Sub RecordThrough(editor As EditManuscriptForm, record As String)
        If record = "AddSubmission" Then
            CallPrivate(editor, record)
        Else
            Assert.IsTrue(CBool(CallNoArguments(editor, record)), "The submission was recorded.")
        End If
    End Sub

    Private Shared Function CallNoArguments(target As Object, name As String) As Object
        Return target.GetType().GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(target, Nothing)
    End Function

    Private Shared Function WorkingCopy(editor As EditManuscriptForm) As Manuscript
        Return DirectCast(GetType(EditManuscriptForm).GetProperty("_workingManuscript", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), Manuscript)
    End Function

    Private Shared Function NotesCard(editor As EditManuscriptForm) As Control
        Return DirectCast(GetType(EditManuscriptForm).GetField("journalNotesGroup", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), Control)
    End Function

    ' The portal box of a submission dialog: the field beside its label.
    Private Shared Function PortalBox(dialog As Form) As TextBox
        Dim label As Label = Descendants(dialog).OfType(Of Label)().Single(Function(item) item.Text = "Publisher portal URL")
        Dim grid As TableLayoutPanel = DirectCast(label.Parent, TableLayoutPanel)
        Return DirectCast(grid.GetControlFromPosition(1, grid.GetRow(label)), TextBox)
    End Function

    ' Delete Journal Submission says what goes with the submission, in
    ' plain words (#222): only the kinds it holds, with real plurals, the
    ' day it was submitted, that the stage may move, and that Discard
    ' leaves the saved library as it was.
    <TestMethod>
    <DataRow(0, 0, 0, "")>
    <DataRow(1, 1, 1, "This also removes 1 editorial decision, 1 item from Correspondence & Files, and 1 reviewer comment with its drafted response.")>
    <DataRow(3, 3, 3, "This also removes 3 editorial decisions, 3 items from Correspondence & Files, and 3 reviewer comments with their drafted responses.")>
    <DataRow(2, 0, 1, "This also removes 2 editorial decisions and 1 reviewer comment with its drafted response.")>
    Public Sub DeleteSubmissionQuestionSaysWhatGoesWithIt(decisions As Integer, correspondence As Integer, comments As Integer, removes As String)
        Dim submission As New JournalSubmission With {
            .JournalName = "Fictional Open Psychology",
            .SubmittedDate = New DateTime(2026, 3, 1)
        }
        For i As Integer = 1 To decisions
            submission.Decisions.Add(New EditorialDecisionEvent())
        Next
        For i As Integer = 1 To correspondence
            submission.Correspondence.Add(New CorrespondenceItem())
        Next
        For i As Integer = 1 To comments
            submission.ReviewerResponses.Add(New ReviewerResponseItem())
        Next

        Dim question As String = EditManuscriptForm.DeleteSubmissionQuestion(submission)

        StringAssert.StartsWith(question, "Delete the submission to 'Fictional Open Psychology' (submitted " & New DateTime(2026, 3, 1).ToString("MMM d, yyyy") & ")?", "The question names the journal and the day it was submitted.")
        StringAssert.Contains(question, "The manuscript's stage may change to match the submissions left.")
        StringAssert.Contains(question, "Discarding the manuscript's unsaved changes leaves the saved library unchanged.")
        Assert.IsFalse(question.Contains("(s)", StringComparison.Ordinal), "Real plurals, never (s): " & question)

        If removes.Length = 0 Then
            Assert.IsFalse(question.Contains("This also removes", StringComparison.Ordinal), "Nothing else goes, so nothing else is listed: " & question)
        Else
            StringAssert.Contains(question, removes, "Only the kinds the submission holds are listed.")
        End If
    End Sub

    <TestMethod>
    <DataRow(1000)>
    <DataRow(Integer.MaxValue)>
    Public Sub VersionMetadataEdit_PreservesRevisionRoundsAboveFormerEditorLimit(round As Integer)
        Dim manuscript As Manuscript = CreateLinkedManuscript()
        Dim version As ManuscriptVersion = manuscript.Versions.Single()
        version.SubmissionId = manuscript.Submissions.Single().Id
        version.RevisionRoundNumber = round
        Dim original As String = Snapshot(version)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingVersionForm(manuscript, version)
                    dialog.ShowInTaskbar = False
                    dialog.Opacity = 0
                    dialog.StartPosition = FormStartPosition.Manual
                    dialog.Location = New Point(-20000, -20000)
                    dialog.Show()
                    Application.DoEvents()
                    Dim notes As TextBox = Descendants(dialog).OfType(Of TextBox)().Single(Function(item) item.Multiline)
                    notes.Text = "Metadata-only note correction"
                    Assert.AreEqual(round, dialog.RevisionRoundNumber.Value)
                    dialog.AcceptButton.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(original, Snapshot(version), "The editor must wait for its caller to adopt metadata changes.")
                    ManuscriptVersionService.UpdateVersion(manuscript, version.Id, dialog.VersionLabel,
                        dialog.VersionNotes, dialog.VersionDate, dialog.VersionFilePath, dialog.IsManagedCopy,
                        dialog.SubmissionId, dialog.DecisionId, dialog.RevisionRoundNumber)
                    dialog.Close()
                End Using
            End Sub)

        Assert.AreEqual(round, version.RevisionRoundNumber.Value)
        Assert.AreEqual("Metadata-only note correction", version.Notes)
        Assert.AreEqual(manuscript.Submissions.Single().Id, version.SubmissionId.Value)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Validation_RejectsLinkedSubmissionJournalConflict(readinessOnly As Boolean)
        Dim manuscript As Manuscript = CreateLinkedManuscript(readinessOnly)
        manuscript.Submissions.Single().JournalId = Guid.NewGuid()

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub() SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript))
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub SubmissionEditPreflight_RejectsConflictWithoutChangingEitherRecord(readinessOnly As Boolean)
        Dim manuscript As Manuscript = CreateLinkedManuscript(readinessOnly)
        Dim proposed As JournalSubmission = ManuscriptCloneService.CloneSubmission(manuscript.Submissions.Single())
        proposed.JournalId = Guid.NewGuid()
        proposed.JournalName = "A different reusable journal"
        proposed.SubmittedDate = proposed.SubmittedDate.AddDays(2)
        Dim original As String = Snapshot(manuscript)
        Dim candidate As String = Snapshot(proposed)

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub() SubmissionReadinessValidationService.ValidateSubmissionJournalAssociations(manuscript, proposed))

        Assert.AreEqual(original, Snapshot(manuscript),
            "Rejecting the journal edit must preserve packet links, submission dates, and lifecycle state.")
        Assert.AreEqual(candidate, Snapshot(proposed))
    End Sub

    <TestMethod>
    Public Sub SameJournalIdentity_AllowsHistoricalNamesToDifferWithoutRewritingSnapshots()
        Dim manuscript As Manuscript = CreateLinkedManuscript()
        manuscript.Submissions.Single().JournalName = "Renamed journal"
        manuscript.ReadinessProfiles.Single().JournalName = "Historical readiness name"
        manuscript.SubmissionPackets.Single().JournalName = "Historical packet name"
        Dim original As String = Snapshot(manuscript)

        SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
        SubmissionReadinessValidationService.ValidateSubmissionJournalAssociations(
            manuscript, manuscript.Submissions.Single())

        Assert.AreEqual(original, Snapshot(manuscript))
    End Sub

    <TestMethod>
    <DataRow(True)>
    <DataRow(False)>
    Public Sub MissingJournalIdentity_DoesNotInferConflictsFromNames(submissionIdentityMissing As Boolean)
        Dim manuscript As Manuscript = CreateLinkedManuscript()
        Dim submission As JournalSubmission = manuscript.Submissions.Single()
        If submissionIdentityMissing Then
            submission.JournalId = Nothing
        Else
            manuscript.SubmissionPackets.Single().JournalId = Nothing
            manuscript.ReadinessProfiles.Single().JournalId = Nothing
        End If
        submission.JournalName = "Different free-text submission name"
        Dim original As String = Snapshot(manuscript)

        SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
        SubmissionReadinessValidationService.ValidateSubmissionJournalAssociations(manuscript, submission)

        Assert.AreEqual(original, Snapshot(manuscript))
    End Sub

    <TestMethod>
    Public Sub SubmissionEditPreflight_DoesNotRestrictAnUnlinkedSubmission()
        Dim manuscript As Manuscript = CreateLinkedManuscript()
        Dim otherSubmission As New JournalSubmission With {
            .JournalId = Guid.NewGuid(), .JournalName = "Other submission"
        }
        manuscript.Submissions.Add(otherSubmission)
        Dim proposed As JournalSubmission = ManuscriptCloneService.CloneSubmission(otherSubmission)
        proposed.JournalId = Guid.NewGuid()
        Dim original As String = Snapshot(manuscript)

        SubmissionReadinessValidationService.ValidateSubmissionJournalAssociations(manuscript, proposed)

        Assert.AreEqual(original, Snapshot(manuscript))
    End Sub

    <TestMethod>
    Public Sub RepositorySave_RejectsJournalConflictBeforeCopyingFilesOrReplacingSavedLibrary()
        Dim root As String = CreateTemporaryRoot()
        Try
            Dim managedRoot As String = Path.Combine(root, "managed")
            Dim repository As New ManuscriptRepository(Path.Combine(root, "data"), managedRoot)
            Dim manuscript As Manuscript = CreateLinkedManuscript()
            Dim manuscripts As New List(Of Manuscript) From {manuscript}
            repository.Save(manuscripts)
            Dim originalJson As String = File.ReadAllText(repository.DataFilePath)
            Dim sourcePath As String = Path.Combine(root, "pending-cover-letter.txt")
            File.WriteAllText(sourcePath, "Keep these source bytes")
            SubmissionPacketService.AddFile(manuscript.SubmissionPackets.Single(),
                SubmissionPacketFileRole.CoverLetter, "Pending managed file", String.Empty,
                sourcePath, SubmissionPacketFileStorageMode.ManagedCopy)
            manuscript.Submissions.Single().JournalId = Guid.NewGuid()

            Assert.ThrowsExactly(Of InvalidDataException)(Sub() repository.Save(manuscripts))

            Assert.AreEqual(originalJson, File.ReadAllText(repository.DataFilePath))
            Assert.AreEqual("Keep these source bytes", File.ReadAllText(sourcePath))
            Assert.AreEqual(sourcePath, manuscript.SubmissionPackets.Single().Files.Single().LocalFilePath)
            Assert.IsFalse(Directory.Exists(managedRoot), "Validation must run before creating a managed copy.")
            Dim reloaded As Manuscript = repository.Load().Single()
            Assert.AreEqual(reloaded.SubmissionPackets.Single().JournalId, reloaded.Submissions.Single().JournalId)
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    Private Shared Function Snapshot(Of T)(value As T) As String
        Return JsonSerializer.Serialize(value, CreateJsonOptions())
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action)
    End Sub

    ' A handler with no arguments of its own, as a button's Click calls it.
    Private Shared Sub CallPrivate(target As Object, name As String)
        target.GetType().GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(target, {Nothing, EventArgs.Empty})
    End Sub

    ' The journal box of a submission dialog: the field beside Use Library...
    Private Shared Function JournalBox(dialog As Form) As TextBox
        Return Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Use Library...").Parent.Controls.OfType(Of TextBox)().Single()
    End Function

    ' Answers each window of one kind as it opens on the test thread, with
    ' what the test gives, so a handler that shows it modally comes back.
    ' The answer runs from a timer the modal loop pumps, on the window as
    ' it shows; a handler that never opens one leaves the test to fail on
    ' its count, not to block. FileDrawerTests answers its dialog with it too.
    Friend NotInheritable Class ModalAnswer(Of T As Form)
        Implements IDisposable

        Private ReadOnly _timer As New System.Windows.Forms.Timer With {.Interval = 50}
        Private ReadOnly _seen As New HashSet(Of Form)()

        Public Sub New(answer As Action(Of T))
            AddHandler _timer.Tick,
                Sub(sender, e)
                    For Each dialog As T In Application.OpenForms.OfType(Of T)().Where(Function(form) form.Visible).ToList()
                        If _seen.Add(dialog) Then answer(dialog)
                    Next
                End Sub
            _timer.Start()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Dispose()
        End Sub
    End Class

    Private Class NonactivatingVersionForm
        Inherits ManuscriptVersionEditForm

        Public Sub New(manuscript As Manuscript, version As ManuscriptVersion)
            MyBase.New(manuscript, version)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class NonactivatingSubmissionForm
        Inherits AddSubmissionForm

        Public Sub New(journalName As String, journalId As Guid, portal As String)
            MyBase.New(journalName, journalId, portal)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Shared Function CreateLinkedManuscript(Optional readinessOnly As Boolean = False) As Manuscript
        Dim journalId As Guid = Guid.NewGuid()
        Dim manuscript As New Manuscript With {
            .Title = "Linked workflow consistency",
            .CurrentStage = PaperStage.Submitted,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = New DateTime(2026, 9, 10)
        }
        manuscript.History.Add(New HistoryEvent With {
            .Stage = PaperStage.Submitted, .EventDate = manuscript.StageEnteredDate, .Note = "Recorded submission"
        })
        Dim version As New ManuscriptVersion With {.Label = "Exact submitted version"}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id
        Dim submission As New JournalSubmission With {
            .JournalId = journalId, .JournalName = "Journal A", .SubmittedDate = manuscript.StageEnteredDate
        }
        manuscript.Submissions.Add(submission)
        Dim readiness As New ManuscriptReadiness With {.JournalId = journalId, .JournalName = "Journal A"}
        manuscript.ReadinessProfiles.Add(readiness)
        manuscript.SubmissionPackets.Add(New SubmissionPacket With {
            .Label = "Submitted packet", .ManuscriptVersionId = version.Id,
            .SubmissionId = submission.Id, .ReadinessProfileId = readiness.Id,
            .JournalId = If(readinessOnly, Nothing, CType(journalId, Guid?)), .JournalName = "Journal A"
        })
        Return manuscript
    End Function

End Class
