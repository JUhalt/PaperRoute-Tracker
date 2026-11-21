Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionPacketBackupTests

    Private _root As String = String.Empty
    Private _backupPath As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _backupPath = Path.Combine(_root, "packet-backup.zip")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Restore_RebasesManagedPacketAndPreservesReadinessAndSubmissionLinks(
        linkSubmission As Boolean
    )

        Dim source As Manuscript = CreateSourceBackup(linkSubmission)
        Dim originalPacket As SubmissionPacket = source.SubmissionPackets(0)
        Dim originalFile As SubmissionPacketFile = originalPacket.Files(0)
        Dim targetManaged As String = Path.Combine(_root, "target-library")
        Dim targetData As String = Path.Combine(_root, "target-data")
        Dim repository As New ManuscriptRepository(targetData, targetManaged)
        Dim restore As New PortableRestoreService(targetManaged)

        Dim inspection As BackupInspection = restore.InspectBackup(_backupPath)
        Assert.AreEqual(1, inspection.ManagedFileCount)
        Assert.AreEqual(If(linkSubmission, 1, 0), inspection.SubmissionCount)

        restore.RestoreBackup(_backupPath, New List(Of Manuscript)(), repository)

        Dim restored As Manuscript = repository.Load().Single()
        Dim packet As SubmissionPacket = restored.SubmissionPackets.Single()
        Dim managedFile As SubmissionPacketFile = packet.Files(0)
        Dim expectedPath As String = Path.Combine(
            targetManaged,
            source.Id.ToString("N"),
            "packets",
            originalPacket.Id.ToString("N"),
            originalFile.Id.ToString("N"),
            Path.GetFileName(originalFile.LocalFilePath)
        )

        Assert.AreEqual(expectedPath, managedFile.LocalFilePath)
        Assert.IsTrue(File.Exists(expectedPath))
        CollectionAssert.AreEqual(
            File.ReadAllBytes(originalFile.LocalFilePath),
            File.ReadAllBytes(expectedPath)
        )
        Assert.AreEqual(originalFile.Id, managedFile.Id)
        Assert.AreEqual(originalFile.Role, managedFile.Role)
        Assert.AreEqual(originalFile.Label, managedFile.Label)
        Assert.AreEqual(originalFile.Notes, managedFile.Notes)
        Assert.AreEqual(originalFile.OriginalFileName, managedFile.OriginalFileName)
        Assert.AreEqual(originalFile.Sha256, managedFile.Sha256)
        Assert.AreEqual(originalFile.FileSizeBytes, managedFile.FileSizeBytes)
        Assert.AreEqual(originalFile.LastWriteTimeUtc, managedFile.LastWriteTimeUtc)
        Assert.AreEqual(originalFile.HashComputedAtUtc, managedFile.HashComputedAtUtc)
        Assert.AreEqual(SubmissionPacketFileStorageMode.ManagedCopy, managedFile.StorageMode)

        Dim externalFile As SubmissionPacketFile = packet.Files(1)
        Assert.AreEqual(originalPacket.Files(1).LocalFilePath, externalFile.LocalFilePath)
        Assert.AreEqual(SubmissionPacketFileStorageMode.LinkedExternal, externalFile.StorageMode)
        Assert.IsFalse(File.Exists(externalFile.LocalFilePath))
        Assert.AreEqual(SubmissionPacketFileStorageMode.MetadataOnly, packet.Files(2).StorageMode)
        Assert.AreEqual(String.Empty, packet.Files(2).LocalFilePath)

        Assert.AreEqual(originalPacket.Id, packet.Id)
        Assert.AreEqual(originalPacket.ManuscriptVersionId, packet.ManuscriptVersionId)
        Assert.AreEqual(originalPacket.ReadinessProfileId, packet.ReadinessProfileId)
        Assert.AreEqual(originalPacket.JournalId, packet.JournalId)
        Assert.AreEqual(originalPacket.JournalName, packet.JournalName)
        Assert.AreEqual(originalPacket.SubmissionId, packet.SubmissionId)
        Assert.AreEqual(originalPacket.RevisionRoundNumber, packet.RevisionRoundNumber)
        Assert.AreEqual(source.CurrentStage, restored.CurrentStage)
        Assert.AreEqual(source.Location, restored.Location)
        Assert.AreEqual(source.History.Count, restored.History.Count)
        Assert.AreEqual(source.Submissions.Count, restored.Submissions.Count)

        Dim readiness As ManuscriptReadiness = restored.ReadinessProfiles.Single()
        Assert.AreEqual(source.ReadinessProfiles(0).Id, readiness.Id)
        Assert.AreEqual(source.ReadinessProfiles(0).Notes, readiness.Notes)
        Assert.AreEqual(ReadinessItemStatus.Complete, readiness.Items(0).Status)
        Assert.AreEqual(source.ReadinessProfiles(0).Items(0).CompletedAtUtc, readiness.Items(0).CompletedAtUtc)
        Assert.AreEqual("Approved by coauthors", readiness.Items(0).UserNotes)
        Assert.AreEqual(ReadinessItemStatus.NotApplicable, readiness.Items(1).Status)
        Assert.IsFalse(readiness.Items(1).CompletedAtUtc.HasValue)

        Dim library As AuthorLibraryData = New AuthorLibraryRepository(targetData).Load()
        Dim template As JournalChecklistTemplateItem = library.Journals.Single().ReadinessChecklistTemplate(0)
        Assert.AreEqual(readiness.Items(0).TemplateItemId.Value, template.Id)
        Assert.AreEqual("Cover letter", template.Title)
        Assert.IsTrue(template.IsRequired)

    End Sub

    <TestMethod>
    Public Sub Restore_RejectsMissingManagedPacketBeforeChangingCurrentLibrary()

        CreateSourceBackup(False)

        Using archive As ZipArchive = ZipFile.Open(_backupPath, ZipArchiveMode.Update)
            Dim entry As ZipArchiveEntry = archive.Entries.Single(
                Function(item) item.FullName.Replace("\", "/").Contains("/packets/")
            )
            entry.Delete()
        End Using

        AssertRejectedBeforeChangingCurrentLibrary()

    End Sub

    <TestMethod>
    <DataRow("version")>
    <DataRow("readiness")>
    <DataRow("submission")>
    <DataRow("round")>
    <DataRow("managed-path")>
    Public Sub Restore_RejectsInvalidPacketDataBeforeChangingCurrentLibrary(
        invalidField As String
    )

        CreateSourceBackup(False)

        RewriteJsonEntry(Of List(Of Manuscript))(
            "manuscripts.json",
            Sub(manuscripts)
                Dim packet As SubmissionPacket = manuscripts(0).SubmissionPackets(0)
                Select Case invalidField
                    Case "version"
                        packet.ManuscriptVersionId = Guid.NewGuid()
                    Case "readiness"
                        packet.ReadinessProfileId = Guid.NewGuid()
                    Case "submission"
                        packet.SubmissionId = Guid.NewGuid()
                    Case "round"
                        packet.RevisionRoundNumber = 1
                    Case "managed-path"
                        packet.Files(0).LocalFilePath = String.Empty
                End Select
            End Sub
        )

        AssertRejectedBeforeChangingCurrentLibrary()

    End Sub

    <TestMethod>
    Public Sub Restore_RejectsInvalidJournalTemplateBeforeChangingCurrentLibrary()

        CreateSourceBackup(False)

        RewriteJsonEntry(Of AuthorLibraryData)(
            "authors.json",
            Sub(library)
                Dim template As List(Of JournalChecklistTemplateItem) =
                    library.Journals(0).ReadinessChecklistTemplate
                template(1).Id = template(0).Id
            End Sub
        )

        AssertRejectedBeforeChangingCurrentLibrary()

    End Sub

    Private Function CreateSourceBackup(linkSubmission As Boolean) As Manuscript

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscript As New Manuscript With {
            .Title = "Packet restore study",
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }
        Dim version As New ManuscriptVersion With {.Label = "Exact submitted version"}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim journal As New JournalRecord With {.Name = "Journal of Portable Packets"}
        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Title = "Cover letter", .IsRequired = True, .SortOrder = 0
            }
        )
        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Title = "Supplement", .IsRequired = False, .SortOrder = 1
            }
        )
        Dim readiness As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(manuscript, journal)
        readiness.Notes = "Retain the journal-specific snapshot"
        SubmissionReadinessService.SetStatus(
            readiness.Items(0),
            ReadinessItemStatus.Complete,
            New DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
        )
        readiness.Items(0).UserNotes = "Approved by coauthors"
        SubmissionReadinessService.SetStatus(readiness.Items(1), ReadinessItemStatus.NotApplicable)

        Dim packet As SubmissionPacket = SubmissionPacketService.CreatePacket(
            manuscript, version.Id, "Revision package", "Prepared files", readiness.Id
        )

        If linkSubmission Then
            Dim submission As New JournalSubmission With {
                .JournalName = journal.Name, .SubmittedDate = New DateTime(2026, 9, 10)
            }
            manuscript.Submissions.Add(submission)
            packet.SubmissionId = submission.Id
            packet.RevisionRoundNumber = 2
        End If

        Dim sourceFile As String = Path.Combine(_root, "cover-letter.txt")
        File.WriteAllText(sourceFile, "These are the exact packet bytes.")
        Dim managedFile As SubmissionPacketFile = SubmissionPacketService.AddFile(
            packet, SubmissionPacketFileRole.CoverLetter, "Cover letter", "Approved copy",
            sourceFile, SubmissionPacketFileStorageMode.ManagedCopy
        )
        managedFile.Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile)))
        managedFile.HashComputedAtUtc = New DateTime(2026, 9, 11, 12, 30, 0, DateTimeKind.Utc)
        packet.Files.Add(
            New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.Figure,
                .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                .LocalFilePath = Path.Combine(_root, "missing-external-figure.png")
            }
        )
        packet.Files.Add(
            New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.ReportingChecklist,
                .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly,
                .Notes = "Completed in portal"
            }
        )

        Dim manuscripts As New List(Of Manuscript) From {manuscript}
        repository.Save(manuscripts)
        Dim library As New AuthorLibraryData()
        library.Journals.Add(journal)
        Dim authorRepository As New AuthorLibraryRepository(dataDirectory)
        authorRepository.Save(library)
        Dim backup As New PortableBackupService(managedDirectory)
        backup.CreateBackup(_backupPath, manuscripts, repository)
        Return manuscript

    End Function

    Private Sub AssertRejectedBeforeChangingCurrentLibrary()

        Dim dataDirectory As String = Path.Combine(_root, "current-data")
        Dim managedDirectory As String = Path.Combine(_root, "current-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim current As New List(Of Manuscript) From {
            New Manuscript With {.Title = "Keep the current manuscript"}
        }
        repository.Save(current)
        Dim authorRepository As New AuthorLibraryRepository(dataDirectory)
        authorRepository.Save(New AuthorLibraryData())
        Directory.CreateDirectory(managedDirectory)
        Dim markerPath As String = Path.Combine(managedDirectory, "existing-document.txt")
        File.WriteAllText(markerPath, "Keep the current files")
        Dim originalJson As String = File.ReadAllText(repository.DataFilePath)
        Dim authorsPath As String = Path.Combine(dataDirectory, "authors.json")
        Dim originalAuthors As String = File.ReadAllText(authorsPath)
        Dim restore As New PortableRestoreService(managedDirectory)

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                restore.InspectBackup(_backupPath)
            End Sub
        )
        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                restore.RestoreBackup(_backupPath, current, repository)
            End Sub
        )

        Assert.AreEqual(originalJson, File.ReadAllText(repository.DataFilePath))
        Assert.AreEqual(originalAuthors, File.ReadAllText(authorsPath))
        Assert.AreEqual("Keep the current files", File.ReadAllText(markerPath))

    End Sub

    Private Sub RewriteJsonEntry(Of T)(entryName As String, change As Action(Of T))

        Using archive As ZipArchive = ZipFile.Open(_backupPath, ZipArchiveMode.Update)
            Dim entry As ZipArchiveEntry = archive.GetEntry(entryName)
            Dim value As T
            Using reader As New StreamReader(entry.Open())
                value = JsonSerializer.Deserialize(Of T)(reader.ReadToEnd(), CreateJsonOptions())
            End Using
            change(value)
            entry.Delete()
            Using writer As New StreamWriter(archive.CreateEntry(entryName).Open())
                writer.Write(JsonSerializer.Serialize(value, CreateJsonOptions()))
            End Using
        End Using

    End Sub

End Class
