Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ReviewerResponseTests
    Private _root As String

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub AddAndEdit_KeepExactDecisionRoundAndDoNotCreateWorkflowEvents()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim draftItem = Draft(submission, "Reviewer 2", "Explain exclusions.", 2)
        Dim item = ReviewerResponseService.AddItem(submission, draftItem)
        Assert.AreNotEqual(draftItem.Id, item.Id)
        Assert.AreEqual(submission.Decisions(0).Id, item.DecisionId)
        Assert.AreEqual(2, item.RevisionRoundNumber)
        draftItem.CommentText = "Changed caller draft"
        Assert.AreEqual("Explain exclusions.", item.CommentText)
        Dim created = item.CreatedAtUtc
        Dim edited = ManuscriptCloneService.CloneReviewerResponse(item)
        edited.ResponseText = "We added the requested explanation."
        edited.Status = ReviewerResponseStatus.Addressed
        Dim updated = ReviewerResponseService.UpdateItem(submission, item.Id, edited)
        Assert.AreEqual(item.Id, updated.Id)
        Assert.AreEqual(created, updated.CreatedAtUtc)
        Assert.IsTrue(updated.LastModifiedAtUtc.HasValue)
        Assert.AreEqual(ReviewerResponseStatus.Addressed, updated.Status)
        Assert.AreEqual(2, submission.Decisions.Count)
        Assert.AreEqual(1, manuscript.Submissions.Count)
        Assert.AreEqual(0, manuscript.History.Count)
        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
    End Sub

    <TestMethod>
    Public Sub Add_RejectsForeignDecisionWithoutChangingAnyExistingResponse()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        ReviewerResponseService.AddItem(submission, Draft(submission))
        Dim before = JsonSerializer.Serialize(manuscript)
        Dim foreign = New EditorialDecisionEvent()
        Dim other As New JournalSubmission()
        other.Decisions.Add(foreign)
        manuscript.Submissions.Add(other)
        before = JsonSerializer.Serialize(manuscript)
        Dim invalid = Draft(submission)
        invalid.DecisionId = foreign.Id
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.AddItem(submission, invalid))
        Assert.AreEqual(before, JsonSerializer.Serialize(manuscript))
    End Sub

    <TestMethod>
    <DataRow(0)>
    <DataRow(-1)>
    Public Sub Add_RejectsNonpositiveRound(round As Integer)
        Dim submission = Fixture().Submissions(0)
        Dim item = Draft(submission)
        item.RevisionRoundNumber = round
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.AddItem(submission, item))
        Assert.AreEqual(0, submission.ReviewerResponses.Count)
    End Sub

    <TestMethod>
    Public Sub Update_RejectsInvalidInputWithoutLosingDraftOrOrder()
        Dim submission = Fixture().Submissions(0)
        Dim item = ReviewerResponseService.AddItem(submission, Draft(submission))
        Dim before = JsonSerializer.Serialize(submission)
        Dim invalid = ManuscriptCloneService.CloneReviewerResponse(item)
        invalid.Status = CType(99, ReviewerResponseStatus)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.UpdateItem(submission, item.Id, invalid))
        Assert.AreEqual(before, JsonSerializer.Serialize(submission))
        Assert.ThrowsExactly(Of ArgumentException)(Sub() ReviewerResponseService.UpdateItem(submission, Guid.NewGuid(), item))
        Assert.AreEqual(before, JsonSerializer.Serialize(submission))
    End Sub

    <TestMethod>
    Public Sub Clone_ChangesStayOnWorkingCopyUntilExplicitlyApplied()
        Dim manuscript = Fixture()
        ReviewerResponseService.AddItem(manuscript.Submissions(0), Draft(manuscript.Submissions(0)))
        Dim before = JsonSerializer.Serialize(manuscript)
        Dim workingCopy = ManuscriptCloneService.CloneManuscript(manuscript)
        workingCopy.Submissions(0).ReviewerResponses(0).ResponseText = "Only on the working copy"
        ReviewerResponseService.AddItem(workingCopy.Submissions(0), Draft(workingCopy.Submissions(0), "Editor", "New action"))
        Assert.AreEqual(before, JsonSerializer.Serialize(manuscript))
        Assert.AreEqual(2, workingCopy.Submissions(0).ReviewerResponses.Count)
    End Sub

    <TestMethod>
    Public Sub OrderingAndFilters_KeepReviewersAndRoundsDistinctThroughSaveReload()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim first = ReviewerResponseService.AddItem(submission, Draft(submission, "Reviewer 1", "First round", 1))
        Dim laterDraft = Draft(submission, "Reviewer 1", "Second round", 2)
        laterDraft.DecisionId = submission.Decisions(1).Id
        Dim second = ReviewerResponseService.AddItem(submission, laterDraft)
        Dim third = ReviewerResponseService.AddItem(submission, Draft(submission, "Reviewer 2", "Independent reviewer", 1))
        Assert.IsTrue(ReviewerResponseService.MoveItem(submission, third.Id, -1))
        Assert.IsFalse(ReviewerResponseService.MoveItem(submission, first.Id, -1))
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "data"), Path.Combine(_root, "library"))
        repository.Save(New List(Of Manuscript) From {manuscript})
        Dim loaded = repository.Load()(0).Submissions(0)
        CollectionAssert.AreEqual(New Guid() {first.Id, third.Id, second.Id}, loaded.ReviewerResponses.Select(Function(item) item.Id).ToArray())
        Assert.AreEqual(2, ReviewerResponseService.GetItems(loaded, revisionRoundNumber:=1).Count)
        Dim roundTwo = ReviewerResponseService.GetItems(loaded, submission.Decisions(1).Id, 2)
        Assert.AreEqual(1, roundTwo.Count)
        Assert.AreEqual(second.Id, roundTwo(0).Id)
        Assert.AreEqual("Second round", roundTwo(0).CommentText)
    End Sub

    <TestMethod>
    Public Sub RemoveAndDecisionGuard_RequireExplicitReassignmentOrRemoval()
        Dim submission = Fixture().Submissions(0)
        Dim item = ReviewerResponseService.AddItem(submission, Draft(submission))
        Assert.AreEqual(1, ReviewerResponseService.GetDecisionReferenceCount(submission, item.DecisionId))
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() ReviewerResponseService.EnsureDecisionCanBeRemoved(submission, item.DecisionId))
        Assert.IsFalse(ReviewerResponseService.RemoveItem(submission, Guid.NewGuid()))
        Assert.IsTrue(ReviewerResponseService.RemoveItem(submission, item.Id))
        ReviewerResponseService.EnsureDecisionCanBeRemoved(submission, item.DecisionId)
        Assert.AreEqual(0, submission.ReviewerResponses.Count)
    End Sub

    <TestMethod>
    Public Sub Save_RejectsDanglingDecisionBeforeReplacingDatabaseOrBackup()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        ReviewerResponseService.AddItem(submission, Draft(submission))
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "data"), Path.Combine(_root, "library"))
        Dim library As New List(Of Manuscript) From {manuscript}
        repository.Save(library)
        repository.Save(library)
        Dim database = File.ReadAllBytes(repository.DataFilePath)
        Dim backup = File.ReadAllBytes(repository.BackupFilePath)
        submission.Decisions.RemoveAt(0)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() repository.Save(library))
        CollectionAssert.AreEqual(database, File.ReadAllBytes(repository.DataFilePath))
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(repository.BackupFilePath))
        Assert.AreEqual(1, repository.Load()(0).Submissions(0).ReviewerResponses.Count)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub AutomaticRecovery_RejectsInvalidBackupBeforeReplacingOrCreatingPrimary(primaryExists As Boolean)
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        ReviewerResponseService.AddItem(submission, Draft(submission))
        submission.Decisions.Clear()
        Dim invalidBackup = JsonSerializer.Serialize(New List(Of Manuscript) From {manuscript})
        Dim data = Path.Combine(_root, "data")
        Directory.CreateDirectory(data)
        Dim repository As New ManuscriptRepository(data, Path.Combine(_root, "library"))
        File.WriteAllText(repository.BackupFilePath, invalidBackup)
        Const originalPrimary = "{corrupt primary that must remain available for recovery"
        If primaryExists Then File.WriteAllText(repository.DataFilePath, originalPrimary)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() repository.Load())
        Assert.AreEqual(primaryExists, File.Exists(repository.DataFilePath))
        If primaryExists Then Assert.AreEqual(originalPrimary, File.ReadAllText(repository.DataFilePath))
        Assert.AreEqual(invalidBackup, File.ReadAllText(repository.BackupFilePath))
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup)
        Assert.AreEqual(String.Empty, repository.LastRecoveryPreservedFilePath)
        Assert.AreEqual(If(primaryExists, 2, 1), Directory.GetFiles(data).Length)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub AutomaticRecovery_ValidBackupRestoresResponsesAndPreservesDamagedPrimary(semanticCorruption As Boolean)
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim response = ReviewerResponseService.AddItem(submission, Draft(submission))
        response.ResponseText = "Recovered response draft"
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "data"), Path.Combine(_root, "library"))
        Dim library As New List(Of Manuscript) From {manuscript}
        repository.Save(library)
        repository.Save(library)
        Dim backup = File.ReadAllBytes(repository.BackupFilePath)
        Dim invalidPrimary = "{not valid JSON"
        If semanticCorruption Then
            Dim damaged = ManuscriptCloneService.CloneManuscript(manuscript)
            damaged.Submissions(0).Decisions.Clear()
            invalidPrimary = JsonSerializer.Serialize(New List(Of Manuscript) From {damaged})
        End If
        File.WriteAllText(repository.DataFilePath, invalidPrimary)
        Dim loaded = repository.Load()(0)
        Assert.IsTrue(repository.LastLoadRecoveredFromBackup)
        Assert.AreEqual(invalidPrimary, File.ReadAllText(repository.LastRecoveryPreservedFilePath))
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(repository.BackupFilePath))
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(repository.DataFilePath))
        Assert.AreEqual(response.Id, loaded.Submissions(0).ReviewerResponses(0).Id)
        Assert.AreEqual("Recovered response draft", loaded.Submissions(0).ReviewerResponses(0).ResponseText)
    End Sub

    <TestMethod>
    <DataRow("duplicate")>
    <DataRow("null")>
    <DataRow("empty-id")>
    <DataRow("no-reviewer")>
    <DataRow("no-comment-or-action")>
    Public Sub Validation_RejectsInvalidResponseRecords(problem As String)
        Dim submission = Fixture().Submissions(0)
        Dim item = Draft(submission)
        submission.ReviewerResponses.Add(item)
        Select Case problem
            Case "duplicate" : submission.ReviewerResponses.Add(ManuscriptCloneService.CloneReviewerResponse(item))
            Case "null" : submission.ReviewerResponses.Add(Nothing)
            Case "empty-id" : item.Id = Guid.Empty
            Case "no-reviewer" : item.ReviewerLabel = " "
            Case "no-comment-or-action" : item.CommentText = Nothing : item.ActionText = " "
        End Select
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.NormalizeAndValidateSubmission(submission))
    End Sub

    <TestMethod>
    Public Sub Export_IsDeterministicEditableAndDoesNotModifyNullTextOrTimestamps()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim item = ReviewerResponseService.AddItem(submission, Draft(submission, "Reviewer 1", "Explain methods."))
        item.ResponseText = "A manual draft response."
        Dim secondDraft = Draft(submission, "Editor", "Clarify the second round.", 2)
        secondDraft.DecisionId = submission.Decisions(1).Id
        ReviewerResponseService.AddItem(submission, secondDraft)
        submission.ReviewerResponses(0).Notes = Nothing
        Dim before = JsonSerializer.Serialize(manuscript)
        Dim output = ReviewerResponseExportService.ExportMarkdown(manuscript, submission)
        Assert.AreEqual(output, ReviewerResponseExportService.ExportMarkdown(manuscript, submission))
        Assert.AreEqual(before, JsonSerializer.Serialize(manuscript))
        StringAssert.Contains(output, "# Response to reviewers")
        StringAssert.Contains(output, "## 1. Reviewer 1")
        StringAssert.Contains(output, "## 2. Editor")
        StringAssert.Contains(output, "Revision round: 2")
        StringAssert.Contains(output, "Editorial decision: Minor revision — 2026-09-15")
        Assert.IsFalse(output.Contains(submission.Id.ToString("D")))
        Assert.IsFalse(output.Contains(submission.Decisions(1).Id.ToString("D")))
        StringAssert.Contains(output, "A manual draft response\.")
        StringAssert.Contains(output, "[Not entered]")
    End Sub

    <TestMethod>
    Public Sub Export_EscapesUserMarkdownAndHtmlWithoutRunningLinks()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim item = Draft(submission, "# Reviewer", "<script>alert(1)</script>" & vbLf & "[click](https://example.test)")
        ReviewerResponseService.AddItem(submission, item)
        Dim output = ReviewerResponseExportService.ExportMarkdown(manuscript, submission)
        StringAssert.Contains(output, "\# Reviewer")
        StringAssert.Contains(output, "&lt;script\>")
        StringAssert.Contains(output, "\[click\]\(https://example\.test\)")
        Assert.IsFalse(output.Contains("<script>"))
    End Sub

    <TestMethod>
    Public Sub Export_RejectsWrongManuscriptSubmission()
        Dim manuscript = Fixture()
        Assert.ThrowsExactly(Of ArgumentException)(Sub() ReviewerResponseExportService.ExportMarkdown(manuscript, New JournalSubmission()))
    End Sub

    <TestMethod>
    Public Sub Export_EqualDateDecisionsRemainDistinctByExplicitRoundAndItemOrder()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        submission.Decisions(1).DecisionDate = submission.Decisions(0).DecisionDate
        submission.Decisions(1).Decision = submission.Decisions(0).Decision
        ReviewerResponseService.AddItem(submission, Draft(submission, "Reviewer 1", "First round instruction", 1))
        Dim second = Draft(submission, "Reviewer 1", "Later round instruction", 2)
        second.DecisionId = submission.Decisions(1).Id
        ReviewerResponseService.AddItem(submission, second)
        Dim output = ReviewerResponseExportService.ExportMarkdown(manuscript, submission)
        Dim firstRound = output.IndexOf("Revision round: 1", StringComparison.Ordinal)
        Dim secondRound = output.IndexOf("Revision round: 2", StringComparison.Ordinal)
        Assert.IsTrue(firstRound >= 0 AndAlso secondRound > firstRound)
        Assert.IsTrue(output.IndexOf("First round instruction", StringComparison.Ordinal) < secondRound)
        Assert.IsTrue(output.IndexOf("Later round instruction", StringComparison.Ordinal) > secondRound)
        Assert.AreNotEqual(submission.ReviewerResponses(0).DecisionId, submission.ReviewerResponses(1).DecisionId)
    End Sub

    <TestMethod>
    Public Sub Validation_RejectsAmbiguousOwningSubmissionForResponseItems()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        ReviewerResponseService.AddItem(submission, Draft(submission))
        manuscript.Submissions.Add(New JournalSubmission With {.Id = submission.Id})
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.NormalizeAndValidateManuscript(manuscript))
    End Sub

    <TestMethod>
    Public Sub PortableBackupRestore_PreservesResponsesOrderRoundsAndDraftText()
        Dim manuscript = Fixture()
        Dim submission = manuscript.Submissions(0)
        Dim first = ReviewerResponseService.AddItem(submission, Draft(submission, "Reviewer 1", "First round comment", 1))
        first.Status = ReviewerResponseStatus.Addressed
        first.ResponseText = "We revised the methods."
        first.ManuscriptLocation = "Methods, page 7"
        Dim later = Draft(submission, "Editor", "Second round comment", 2)
        later.DecisionId = submission.Decisions(1).Id
        later.Status = ReviewerResponseStatus.InProgress
        later.Notes = "Keep editor and reviewers distinct."
        Dim second = ReviewerResponseService.AddItem(submission, later)
        ReviewerResponseService.MoveItem(submission, second.Id, -1)
        Dim sourceLibrary = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "source-data"), sourceLibrary)
        Dim manuscripts As New List(Of Manuscript) From {manuscript}
        repository.Save(manuscripts)
        Dim zip = Path.Combine(_root, "reviewer-responses.zip")
        Dim backupService As New PortableBackupService(sourceLibrary)
        backupService.CreateBackup(zip, manuscripts, repository)
        Dim targetLibrary = Path.Combine(_root, "target-library")
        Dim target As New ManuscriptRepository(Path.Combine(_root, "target-data"), targetLibrary)
        Dim restore As New PortableRestoreService(targetLibrary)
        Assert.AreEqual(1, restore.InspectBackup(zip).SubmissionCount)
        restore.RestoreBackup(zip, New List(Of Manuscript)(), target)
        Dim restored = target.Load()(0)
        CollectionAssert.AreEqual(New Guid() {second.Id, first.Id}, restored.Submissions(0).ReviewerResponses.Select(Function(item) item.Id).ToArray())
        Assert.AreEqual(JsonSerializer.Serialize(submission.ReviewerResponses), JsonSerializer.Serialize(restored.Submissions(0).ReviewerResponses))
        Assert.AreEqual(ReviewerResponseExportService.ExportMarkdown(manuscript, submission), ReviewerResponseExportService.ExportMarkdown(restored, restored.Submissions(0)))
    End Sub

    <TestMethod>
    Public Sub PortableRestore_RejectsDanglingDecisionBeforeChangingCurrentDataOrFiles()
        Dim manuscript = Fixture()
        ReviewerResponseService.AddItem(manuscript.Submissions(0), Draft(manuscript.Submissions(0)))
        Dim sourceLibrary = Path.Combine(_root, "source-library")
        Dim source As New ManuscriptRepository(Path.Combine(_root, "source-data"), sourceLibrary)
        Dim manuscripts As New List(Of Manuscript) From {manuscript}
        source.Save(manuscripts)
        Dim zip = Path.Combine(_root, "invalid-reviewer-responses.zip")
        Dim backupService As New PortableBackupService(sourceLibrary)
        backupService.CreateBackup(zip, manuscripts, source)
        Using archive = ZipFile.Open(zip, ZipArchiveMode.Update)
            Dim entry = archive.GetEntry("manuscripts.json")
            Dim content As List(Of Manuscript)
            Using reader As New StreamReader(entry.Open())
                content = JsonSerializer.Deserialize(Of List(Of Manuscript))(reader.ReadToEnd(), CreateJsonOptions())
            End Using
            content(0).Submissions(0).Decisions.Clear()
            entry.Delete()
            Using writer As New StreamWriter(archive.CreateEntry("manuscripts.json").Open())
                writer.Write(JsonSerializer.Serialize(content, CreateJsonOptions()))
            End Using
        End Using
        Dim targetLibrary = Path.Combine(_root, "target-library")
        Dim targetData = Path.Combine(_root, "target-data")
        Dim target As New ManuscriptRepository(targetData, targetLibrary)
        Dim current As New List(Of Manuscript) From {New Manuscript With {.Title = "Keep this library"}}
        target.Save(current)
        File.WriteAllText(Path.Combine(targetData, "authors.json"), "{""Journals"":[]}")
        Directory.CreateDirectory(targetLibrary)
        Dim marker = Path.Combine(targetLibrary, "keep.txt")
        File.WriteAllText(marker, "Keep current managed files")
        Dim originalData = File.ReadAllBytes(target.DataFilePath)
        Dim originalAuthors = File.ReadAllBytes(Path.Combine(targetData, "authors.json"))
        Dim restore As New PortableRestoreService(targetLibrary)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() restore.InspectBackup(zip))
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() restore.RestoreBackup(zip, current, target))
        CollectionAssert.AreEqual(originalData, File.ReadAllBytes(target.DataFilePath))
        CollectionAssert.AreEqual(originalAuthors, File.ReadAllBytes(Path.Combine(targetData, "authors.json")))
        Assert.AreEqual("Keep current managed files", File.ReadAllText(marker))
    End Sub

    <TestMethod>
    <DataRow(ReviewerResponseStatus.Unresolved, "Unresolved")>
    <DataRow(ReviewerResponseStatus.InProgress, "In progress")>
    <DataRow(ReviewerResponseStatus.Addressed, "Addressed")>
    <DataRow(ReviewerResponseStatus.NotApplicable, "Not applicable")>
    Public Sub StatusLabels_ArePlainHumanText(status As ReviewerResponseStatus, expected As String)
        Assert.AreEqual(expected, ReviewerResponseService.FormatStatus(status))
    End Sub

    Private Shared Function Fixture() As Manuscript
        Dim manuscript As New Manuscript With {.Title = "Response workflow", .CurrentStage = PaperStage.Draft}
        Dim submission As New JournalSubmission With {.JournalName = "Journal of Responses", .ManuscriptNumber = "JR-42"}
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 1)})
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MinorRevision, .DecisionDate = New DateTime(2026, 9, 15)})
        manuscript.Submissions.Add(submission)
        Return manuscript
    End Function

    Private Shared Function Draft(submission As JournalSubmission,
                                  Optional label As String = "Reviewer 1",
                                  Optional comment As String = "Clarify methods.",
                                  Optional round As Integer = 1) As ReviewerResponseItem
        Return New ReviewerResponseItem With {
            .DecisionId = submission.Decisions(0).Id, .RevisionRoundNumber = round,
            .ReviewerLabel = label, .CommentText = comment
        }
    End Function
End Class
