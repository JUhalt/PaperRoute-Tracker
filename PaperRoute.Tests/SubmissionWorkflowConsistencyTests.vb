Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
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
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "The isolated version editor test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

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
