Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class Schema4CertificationTests

    Private _root As String = String.Empty
    Private _currentData As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedRoot As String = String.Empty
    Private _legacyData As String = String.Empty
    Private _legacyManaged As String = String.Empty
    Private _managedVersion As String = String.Empty
    Private _managedCorrespondence As String = String.Empty
    Private _linkedVersion As String = String.Empty
    Private _missingAttachment As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _currentData = Path.Combine(_root, "current-data")
        _dataDirectory = Path.Combine(_currentData, "data")
        _managedRoot = Path.Combine(_root, "current-managed")
        _legacyData = Path.Combine(_root, "legacy-data")
        _legacyManaged = Path.Combine(_root, "legacy-managed")
        Directory.CreateDirectory(_dataDirectory)
        Directory.CreateDirectory(_managedRoot)
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub GenuineSchema4_MigratesWithoutRewritingRecordsOrFilesAndSecondRunIsIdempotent()
        SeedRepresentativeSchema4()
        Dim repository As New ManuscriptRepository(_dataDirectory, _managedRoot)
        Dim authors As New AuthorLibraryRepository(_dataDirectory)
        Dim beforeModels As String = Serialize(repository.Load())
        Dim beforeAuthors As String = Serialize(authors.Load())
        Dim beforeFiles As Dictionary(Of String, Byte()) = SnapshotFiles(_root)
        Dim oldSchema As Byte() = File.ReadAllBytes(SchemaPath())

        Migrate()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(SchemaPath()))
        Dim backupPath As String = Path.Combine(_dataDirectory, "schema.v4.bak")
        Dim schema5BackupPath As String = Path.Combine(_dataDirectory, "schema.v5.bak")
        CollectionAssert.AreEqual(oldSchema, File.ReadAllBytes(backupPath))
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(schema5BackupPath))
        AssertExistingFilesUnchanged(beforeFiles, SchemaPath())
        Dim afterFiles As Dictionary(Of String, Byte()) = SnapshotFiles(_root)
        CollectionAssert.AreEquivalent(
            beforeFiles.Keys.Concat({backupPath, schema5BackupPath}).ToArray(), afterFiles.Keys.ToArray())
        Assert.AreEqual(beforeModels, Serialize(repository.Load()))
        Assert.AreEqual(beforeAuthors, Serialize(authors.Load()))
        AssertRepresentativeSemantics(repository.Load(), authors.Load())

        Dim afterDirectories As String() = SnapshotDirectories()
        Migrate()

        AssertFileSnapshotEqual(afterFiles, SnapshotFiles(_root))
        CollectionAssert.AreEqual(afterDirectories, SnapshotDirectories())
        Assert.IsFalse(File.Exists(_missingAttachment))
    End Sub

    <TestMethod>
    Public Sub GenuineSchema4_ExplicitSaveAndPortableRestorePreserveHistoryAndRebaseOnlyManagedPaths()
        SeedRepresentativeSchema4()
        Migrate()
        Dim repository As New ManuscriptRepository(_dataDirectory, _managedRoot)
        Dim authors As New AuthorLibraryRepository(_dataDirectory)
        Dim manuscripts As List(Of Manuscript) = repository.Load()
        Dim library As AuthorLibraryData = authors.Load()
        Dim beforeModels As String = Serialize(manuscripts)
        Dim beforeAuthors As String = Serialize(library)
        Dim oldSchema As Byte() = File.ReadAllBytes(Path.Combine(_dataDirectory, "schema.v4.bak"))
        Dim originalManuscripts As Byte() = File.ReadAllBytes(repository.DataFilePath)
        Dim originalAuthors As Byte() = File.ReadAllBytes(authors.DataFilePath)
        Dim sourceFiles As Dictionary(Of String, Byte()) = SnapshotFiles(_managedRoot)
        sourceFiles.Add(_linkedVersion, File.ReadAllBytes(_linkedVersion))

        repository.Save(manuscripts)
        authors.Save(library)

        Assert.AreEqual(beforeModels, Serialize(repository.Load()))
        Assert.AreEqual(beforeAuthors, Serialize(authors.Load()))
        CollectionAssert.AreEqual(originalManuscripts, File.ReadAllBytes(repository.BackupFilePath))
        CollectionAssert.AreEqual(originalAuthors, File.ReadAllBytes(authors.BackupFilePath))
        CollectionAssert.AreEqual(oldSchema, File.ReadAllBytes(Path.Combine(_dataDirectory, "schema.v4.bak")))
        AssertExistingFilesUnchanged(sourceFiles)

        Dim backupPath As String = Path.Combine(_root, "migrated-library.zip")
        Dim backup As New PortableBackupService(_managedRoot)
        backup.CreateBackup(backupPath, manuscripts, repository)
        Dim targetData As String = Path.Combine(_root, "restored-data")
        Dim targetManaged As String = Path.Combine(_root, "restored-managed")
        Dim targetRepository As New ManuscriptRepository(targetData, targetManaged)
        Dim restore As New PortableRestoreService(targetManaged)
        Dim inspection As BackupInspection = restore.InspectBackup(backupPath)
        Assert.AreEqual(3, inspection.ManuscriptCount)
        Assert.AreEqual(1, inspection.SubmissionCount)
        Assert.AreEqual(2, inspection.ManagedFileCount)

        restore.RestoreBackup(backupPath, New List(Of Manuscript)(), targetRepository)

        Dim restored As List(Of Manuscript) = targetRepository.Load()
        Dim targetAuthors As New AuthorLibraryRepository(targetData)
        AssertRepresentativeSemantics(restored, targetAuthors.Load())
        Assert.AreEqual(beforeAuthors, Serialize(targetAuthors.Load()))
        For Each manuscript As Manuscript In restored
            Dim original As Manuscript = manuscripts.Single(Function(item) item.Id = manuscript.Id)
            For Each version As ManuscriptVersion In manuscript.Versions
                Dim originalVersion As ManuscriptVersion = original.Versions.Single(Function(item) item.Id = version.Id)
                If version.IsManagedCopy Then
                    Dim expectedPath As String = Path.Combine(targetManaged, Path.GetRelativePath(_managedRoot, originalVersion.LocalFilePath))
                    Assert.AreEqual(expectedPath, version.LocalFilePath)
                    CollectionAssert.AreEqual(sourceFiles(originalVersion.LocalFilePath), File.ReadAllBytes(version.LocalFilePath))
                    ' Normalize only the expected path relocation before comparing every persisted field.
                    version.LocalFilePath = originalVersion.LocalFilePath
                Else
                    Assert.AreEqual(originalVersion.LocalFilePath, version.LocalFilePath)
                End If
            Next
            For Each submission As JournalSubmission In manuscript.Submissions
                Dim originalSubmission As JournalSubmission = original.Submissions.Single(Function(item) item.Id = submission.Id)
                For Each attachment As CorrespondenceItem In submission.Correspondence
                    Dim originalAttachment As CorrespondenceItem = originalSubmission.Correspondence.Single(Function(item) item.Id = attachment.Id)
                    If attachment.IsManagedCopy Then
                        Dim expectedPath As String = Path.Combine(targetManaged, Path.GetRelativePath(_managedRoot, originalAttachment.LocalFilePath))
                        Assert.AreEqual(expectedPath, attachment.LocalFilePath)
                        CollectionAssert.AreEqual(sourceFiles(originalAttachment.LocalFilePath), File.ReadAllBytes(attachment.LocalFilePath))
                        attachment.LocalFilePath = originalAttachment.LocalFilePath
                    Else
                        Assert.AreEqual(originalAttachment.LocalFilePath, attachment.LocalFilePath)
                    End If
                Next
            Next
        Next
        Assert.AreEqual(beforeModels, Serialize(restored))
        AssertExistingFilesUnchanged(sourceFiles)
        CollectionAssert.AreEqual(oldSchema, File.ReadAllBytes(Path.Combine(_dataDirectory, "schema.v4.bak")))
        Assert.IsFalse(File.Exists(_missingAttachment))
    End Sub

    <TestMethod>
    <DataRow(1)>
    <DataRow(2)>
    <DataRow(3)>
    <DataRow(4)>
    <DataRow(5)>
    Public Sub LockedSchema_PreservesPriorBackupAndSourceBytesThenRetryCompletes(schemaVersion As Integer)
        File.WriteAllText(Path.Combine(_dataDirectory, "manuscripts.json"), "[]")
        File.WriteAllText(Path.Combine(_dataDirectory, "authors.json"), "{}")
        File.WriteAllText(SchemaPath(), "{""SchemaVersion"":" & schemaVersion.ToString() & "}")
        Dim backupPath As String = Path.Combine(_dataDirectory, "schema.v" & schemaVersion.ToString() & ".bak")
        File.WriteAllText(backupPath, "Previous recovery metadata must survive a failed upgrade.")
        Dim before As Dictionary(Of String, Byte()) = SnapshotFiles(_root)
        Dim oldSchema As Byte() = File.ReadAllBytes(SchemaPath())

        ' Permit migration validation reads, but prevent replacing the schema.
        Using lockedSchema As New FileStream(SchemaPath(), FileMode.Open, FileAccess.Read, FileShare.Read)
            Assert.ThrowsExactly(Of IOException)(Sub() Migrate())
            AssertFileSnapshotEqual(before, SnapshotFiles(_root))
            Assert.AreEqual(0, Directory.GetFiles(_dataDirectory, "schema.json.tmp-*").Length)
        End Using

        Migrate()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(SchemaPath()))
        CollectionAssert.AreEqual(oldSchema, File.ReadAllBytes(backupPath))
        CollectionAssert.AreEqual(before(Path.Combine(_dataDirectory, "manuscripts.json")), File.ReadAllBytes(Path.Combine(_dataDirectory, "manuscripts.json")))
        CollectionAssert.AreEqual(before(Path.Combine(_dataDirectory, "authors.json")), File.ReadAllBytes(Path.Combine(_dataDirectory, "authors.json")))
        Assert.AreEqual(0, Directory.GetFiles(_dataDirectory, "schema.json.tmp-*").Length)
    End Sub

    <TestMethod>
    Public Sub FutureSchema_RejectsBeforeChangingAnyCurrentOrLegacyRoot()
        SeedRepresentativeSchema4()
        File.WriteAllText(SchemaPath(), "{""SchemaVersion"":999,""futureMetadata"":""preserve exactly""}")
        WriteFixtureFile(Path.Combine(_legacyData, "data", "manuscripts.json"), {12, 42, 99})
        WriteFixtureFile(Path.Combine(_legacyManaged, "historical-source.bin"), {0, 255, 11})
        Dim before As Dictionary(Of String, Byte()) = SnapshotFiles(_root)
        Dim directoriesBefore As String() = SnapshotDirectories()

        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() Migrate())

        AssertFileSnapshotEqual(before, SnapshotFiles(_root))
        CollectionAssert.AreEqual(directoriesBefore, SnapshotDirectories())
        Assert.IsFalse(File.Exists(_missingAttachment))
    End Sub

    Private Sub SeedRepresentativeSchema4()
        Dim fixtures As String = FindFixtureDirectory()
        Dim manuscriptsJson As String = File.ReadAllText(Path.Combine(fixtures, "manuscripts.json"))
        Dim authorsJson As String = File.ReadAllText(Path.Combine(fixtures, "authors.json"))
        Using document As JsonDocument = JsonDocument.Parse(manuscriptsJson)
            For Each item As JsonElement In document.RootElement.EnumerateArray()
                Dim unused As JsonElement
                Assert.IsFalse(item.TryGetProperty("ReadinessProfiles", unused), "Fixture must use the actual older shape.")
                Assert.IsFalse(item.TryGetProperty("SubmissionPackets", unused), "Fixture must use the actual older shape.")
            Next
        End Using
        Using document As JsonDocument = JsonDocument.Parse(authorsJson)
            For Each item As JsonElement In document.RootElement.GetProperty("Journals").EnumerateArray()
                Dim unused As JsonElement
                Assert.IsFalse(item.TryGetProperty("ReadinessChecklistTemplate", unused))
            Next
        End Using

        Dim manuscriptId As String = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString("N")
        _managedVersion = Path.Combine(_managedRoot, manuscriptId, "versions", Guid.Parse("11111111-1111-1111-1111-111111111101").ToString("N"), "manuscript.txt")
        _managedCorrespondence = Path.Combine(_managedRoot, manuscriptId, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1").ToString("N"), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffff01").ToString("N"), "decision.txt")
        _linkedVersion = Path.Combine(_root, "external", "working.txt")
        _missingAttachment = Path.Combine(_root, "external", "missing.txt")
        WriteFixtureFile(_managedVersion, {0, 17, 128, 255, 13, 10})
        WriteFixtureFile(_managedCorrespondence, {65, 66, 67, 13, 10, 128})
        WriteFixtureFile(_linkedVersion, {84, 101, 120, 116, 13, 10, 255})

        Dim substitutions As New Dictionary(Of String, String) From {
            {"__MANAGED_VERSION__", _managedVersion},
            {"__MANAGED_CORRESPONDENCE__", _managedCorrespondence},
            {"__EXTERNAL_VERSION__", _linkedVersion},
            {"__MISSING_EXTERNAL__", _missingAttachment}
        }
        For Each pair As KeyValuePair(Of String, String) In substitutions
            manuscriptsJson = manuscriptsJson.Replace("""" & pair.Key & """", JsonSerializer.Serialize(pair.Value))
        Next
        File.WriteAllText(Path.Combine(_dataDirectory, "manuscripts.json"), manuscriptsJson)
        File.Copy(Path.Combine(fixtures, "authors.json"), Path.Combine(_dataDirectory, "authors.json"))
        File.Copy(Path.Combine(fixtures, "schema.json"), SchemaPath())
    End Sub

    Private Shared Sub AssertRepresentativeSemantics(manuscripts As List(Of Manuscript), library As AuthorLibraryData)
        Assert.AreEqual(3, manuscripts.Count)
        Assert.IsTrue(manuscripts.All(Function(item) item.ReadinessProfiles.Count = 0 AndAlso item.SubmissionPackets.Count = 0))
        Assert.AreEqual(2, library.Authors.Count)
        Assert.AreEqual(1, library.Affiliations.Count)
        Assert.AreEqual(1, library.Journals.Count)
        Assert.AreEqual(0, library.Journals(0).ReadinessChecklistTemplate.Count)
        Assert.IsFalse(library.Authors(0).OrcidLastCheckedUtc.HasValue)

        Dim revision As Manuscript = manuscripts(0)
        Assert.AreEqual("Fictional revision study — café methods", revision.Title)
        Assert.AreEqual(PaperStage.Revision, revision.CurrentStage)
        Assert.AreEqual(ManuscriptLocation.Pipeline, revision.Location)
        Assert.AreEqual(New DateTime(2026, 8, 15), revision.StageEnteredDate)
        Assert.AreEqual(New DateTime(2026, 9, 30), revision.RevisionDeadline.Value)
        Assert.AreEqual(2, revision.Authors.Count)
        Assert.AreEqual(library.Authors(0).Id, revision.Authors(0).AuthorId)
        Assert.AreEqual(library.Affiliations(0).Id, revision.Authors(0).AffiliationIds.Single())
        Assert.IsTrue(revision.Authors(0).IsCorrespondingAuthor)
        Assert.AreEqual("v03-revision", revision.Metadata.ExternalIdentifiers("fixture"))
        Assert.AreEqual(1, revision.History.Count)
        Assert.IsFalse(revision.History(0).RecordedAtUtc.HasValue)
        Assert.IsFalse(revision.History(0).LastModifiedAtUtc.HasValue)
        Assert.AreEqual(New DateTime(2026, 7, 1, 12, 34, 56), revision.History(0).EventDate)
        Assert.AreEqual(3, revision.Versions.Count)
        Assert.AreEqual(revision.Versions(1).Id, revision.CurrentVersionId.Value)
        Assert.AreEqual(New DateTime(2026, 8, 1, 12, 20, 0, DateTimeKind.Utc), revision.Versions(0).RecordedAtUtc.Value)
        Assert.IsFalse(revision.Versions(0).LastModifiedAtUtc.HasValue)
        Assert.IsFalse(revision.Versions(1).RecordedAtUtc.HasValue)
        Assert.IsFalse(revision.Versions(2).RecordedAtUtc.HasValue)
        Assert.AreEqual(1001, revision.Versions(2).RevisionRoundNumber.Value)
        Assert.AreEqual(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee9"), revision.Versions(2).SubmissionId.Value)
        Assert.AreEqual(Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd9"), revision.Versions(2).DecisionId.Value)
        Assert.AreEqual(String.Empty, revision.Versions(2).LocalFilePath)

        Dim submission As JournalSubmission = revision.Submissions.Single()
        Assert.AreEqual(library.Journals(0).Id, submission.JournalId.Value)
        Assert.AreEqual("Historical Journal Title", submission.JournalName)
        Assert.AreNotEqual(library.Journals(0).Name, submission.JournalName)
        Assert.AreEqual(New DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc), submission.RecordedAtUtc.Value)
        Assert.IsFalse(submission.LastModifiedAtUtc.HasValue)
        Assert.AreEqual(1, submission.Decisions.Count)
        Assert.IsFalse(submission.Decisions(0).RecordedAtUtc.HasValue)
        Assert.AreEqual(submission.Id, revision.Versions(0).SubmissionId.Value)
        Assert.AreEqual(submission.Decisions(0).Id, revision.Versions(1).DecisionId.Value)
        Assert.AreEqual(2, submission.Correspondence.Count)
        Assert.IsFalse(submission.Correspondence(0).RecordedAtUtc.HasValue)
        Assert.IsFalse(submission.Correspondence(1).RecordedAtUtc.HasValue)

        Assert.AreEqual(PaperStage.Published, manuscripts(1).CurrentStage)
        Assert.AreEqual(ManuscriptLocation.Published, manuscripts(1).Location)
        Assert.AreEqual("10.5555/paperroute-fixture", manuscripts(1).Metadata.Doi)
        Assert.AreEqual("Legacy author text retained verbatim", manuscripts(1).CoAuthors)
        Assert.AreEqual(ManuscriptLocation.FileDrawer, manuscripts(2).Location)
        Assert.AreEqual(New DateTime(2026, 4, 2), manuscripts(2).FileDrawerDate.Value)
        Assert.AreEqual("Paused pending additional data.", manuscripts(2).FileDrawerReason)
    End Sub

    Private Sub Migrate()
        StorageMigrationService.EnsureCurrentStorage(_currentData, _legacyData, _managedRoot, _legacyManaged)
    End Sub

    Private Function SchemaPath() As String
        Return StorageMigrationService.SchemaFilePath(_currentData)
    End Function

    Private Shared Function Serialize(Of T)(value As T) As String
        Return JsonSerializer.Serialize(value, CreateJsonOptions())
    End Function

    Private Shared Function FindFixtureDirectory() As String
        Dim current As New DirectoryInfo(AppContext.BaseDirectory)
        While current IsNot Nothing
            Dim candidate As String = Path.Combine(current.FullName, "Fixtures", "Schema4Representative")
            If File.Exists(Path.Combine(candidate, "manuscripts.json")) Then Return candidate
            current = current.Parent
        End While
        Throw New DirectoryNotFoundException("The checked-in Schema4Representative fixture directory was not found.")
    End Function

    Private Shared Sub WriteFixtureFile(filePath As String, bytes As Byte())
        Directory.CreateDirectory(Path.GetDirectoryName(filePath))
        File.WriteAllBytes(filePath, bytes)
    End Sub

    Private Shared Function SnapshotFiles(root As String) As Dictionary(Of String, Byte())
        Return Directory.GetFiles(root, "*", SearchOption.AllDirectories).
            ToDictionary(Function(filePath) filePath, Function(filePath) File.ReadAllBytes(filePath), StringComparer.OrdinalIgnoreCase)
    End Function

    Private Function SnapshotDirectories() As String()
        Return Directory.GetDirectories(_root, "*", SearchOption.AllDirectories).
            OrderBy(Function(directoryPath) directoryPath, StringComparer.OrdinalIgnoreCase).ToArray()
    End Function

    Private Shared Sub AssertExistingFilesUnchanged(before As Dictionary(Of String, Byte()), Optional exceptPath As String = Nothing)
        For Each pair As KeyValuePair(Of String, Byte()) In before
            If String.Equals(pair.Key, exceptPath, StringComparison.OrdinalIgnoreCase) Then Continue For
            Assert.IsTrue(File.Exists(pair.Key), "Source file was removed: " & pair.Key)
            CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), "Source bytes changed: " & pair.Key)
        Next
    End Sub

    Private Shared Sub AssertFileSnapshotEqual(before As Dictionary(Of String, Byte()), after As Dictionary(Of String, Byte()))
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray())
        For Each pair As KeyValuePair(Of String, Byte()) In before
            CollectionAssert.AreEqual(pair.Value, after(pair.Key), "File bytes changed: " & pair.Key)
        Next
    End Sub

End Class
