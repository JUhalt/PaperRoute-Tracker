Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class Schema6MigrationTests
    Private _root As String
    Private _current As String
    Private _data As String
    Private _schema As String

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _current = Path.Combine(_root, "current")
        _data = Path.Combine(_current, "data")
        Directory.CreateDirectory(_data)
        _schema = StorageMigrationService.SchemaFilePath(_current)
        File.WriteAllText(_schema, "{""SchemaVersion"":5}")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Upgrade_PreservesOlderBytesAndLoadsEmptyResponseCollections(explicitNull As Boolean)
        Dim suffix = If(explicitNull, ",""ReviewerResponses"":null", String.Empty)
        Dim original = "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Old manuscript"",""Submissions"":[{""Id"":""22222222-2222-2222-2222-222222222222"",""JournalName"":""Old journal""" & suffix & "}]}]"
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        File.WriteAllText(Path.Combine(_data, "manuscripts.bak"), "preserve existing automatic backup")
        File.WriteAllText(Path.Combine(_data, "authors.json"), "{""Journals"":[]}")
        EnsureStorage()
        Assert.AreEqual(6, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{""SchemaVersion"":5}", File.ReadAllText(Path.Combine(_data, "schema.v5.bak")))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
        Assert.AreEqual("preserve existing automatic backup", File.ReadAllText(Path.Combine(_data, "manuscripts.bak")))
        Assert.AreEqual("{""Journals"":[]}", File.ReadAllText(Path.Combine(_data, "authors.json")))
        Dim repository As New ManuscriptRepository(_data, Path.Combine(_root, "library"))
        Assert.AreEqual(0, repository.Load()(0).Submissions(0).ReviewerResponses.Count)
        Dim afterFirstUpgrade = File.ReadAllText(_schema)
        EnsureStorage()
        Assert.AreEqual(afterFirstUpgrade, File.ReadAllText(_schema))
        Assert.AreEqual("{""SchemaVersion"":5}", File.ReadAllText(Path.Combine(_data, "schema.v5.bak")))
    End Sub

    <TestMethod>
    <DataRow("manuscripts.json", "not json")>
    <DataRow("manuscripts.json", "null")>
    <DataRow("manuscripts.json", "[null]")>
    <DataRow("manuscripts.json", "")>
    <DataRow("authors.json", "not json")>
    <DataRow("authors.json", "null")>
    Public Sub Upgrade_RejectsInvalidExistingDataWithoutAdvancingSchema(fileName As String, data As String)
        File.WriteAllText(Path.Combine(_data, fileName), data)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(data, File.ReadAllText(Path.Combine(_data, fileName)))
        Assert.IsFalse(File.Exists(Path.Combine(_data, "schema.v5.bak")))
    End Sub

    <TestMethod>
    Public Sub Upgrade_RejectsDanglingResponseDecisionWithoutDroppingIt()
        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {
            .DecisionId = Guid.NewGuid(), .ReviewerLabel = "Reviewer 1", .CommentText = "Keep this comment"
        })
        manuscript.Submissions.Add(submission)
        Dim original = JsonSerializer.Serialize(New List(Of Manuscript) From {manuscript})
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
    End Sub

    <TestMethod>
    Public Sub Upgrade_DoesNotInventRoundForResponseItemMissingStoredRound()
        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision}
        submission.Decisions.Add(decision)
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {
            .DecisionId = decision.Id, .RevisionRoundNumber = 1,
            .ReviewerLabel = "Editor", .CommentText = "Preserve my round association"
        })
        manuscript.Submissions.Add(submission)
        Dim original = JsonSerializer.Serialize(New List(Of Manuscript) From {manuscript}).Replace("""RevisionRoundNumber"":1,", String.Empty)
        Assert.IsFalse(original.Contains("RevisionRoundNumber"))
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
    End Sub

    <TestMethod>
    Public Sub Upgrade_WhenMarkerCannotBeReplaced_PreservesPreviousBackupAndCleansTemporaryFile()
        Dim backupPath = Path.Combine(_data, "schema.v5.bak")
        File.WriteAllText(backupPath, "previous recovery marker")
        Using locked As New FileStream(_schema, FileMode.Open, FileAccess.Read, FileShare.Read)
            Assert.ThrowsExactly(Of IOException)(Sub() EnsureStorage())
        End Using
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("previous recovery marker", File.ReadAllText(backupPath))
        Assert.AreEqual(0, Directory.GetFiles(_data, "schema.json.tmp-*").Length)
    End Sub

    <TestMethod>
    Public Sub FutureSchema_IsRejectedBeforeAnyStorageMigration()
        File.WriteAllText(_schema, "{""SchemaVersion"":7}")
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() EnsureStorage())
        Assert.AreEqual(7, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "library")))
    End Sub

    Private Sub EnsureStorage()
        StorageMigrationService.EnsureCurrentStorage(_current, Path.Combine(_root, "legacy"), Path.Combine(_root, "library"), Path.Combine(_root, "legacy-library"))
    End Sub
End Class
