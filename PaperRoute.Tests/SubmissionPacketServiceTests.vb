Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionPacketServiceTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub CreatePacket_RequiresExactExistingVersion()

        Dim manuscript As New Manuscript()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                SubmissionPacketService.CreatePacket(
                    manuscript,
                    Guid.NewGuid(),
                    "Packet",
                    String.Empty
                )
            End Sub
        )

        Assert.AreEqual(0, manuscript.SubmissionPackets.Count)

    End Sub


    <TestMethod>
    Public Sub CreatePacket_UsesReadinessJournalSnapshotWithoutLifecycleMutation()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Submission candidate"
        }

        manuscript.Versions.Add(version)

        Dim journalId As Guid = Guid.NewGuid()

        Dim readiness As New ManuscriptReadiness With {
            .JournalId = journalId,
            .JournalName = "Journal of Packet Tests"
        }

        manuscript.ReadinessProfiles.Add(readiness)

        Dim historyCount As Integer = manuscript.History.Count
        Dim submissionCount As Integer = manuscript.Submissions.Count

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                version.Id,
                "Initial packet",
                "Prepared before submission.",
                readiness.Id
            )

        Assert.AreEqual(version.Id, packet.ManuscriptVersionId)
        Assert.AreEqual(readiness.Id, packet.ReadinessProfileId.Value)
        Assert.AreEqual(journalId, packet.JournalId.Value)
        Assert.AreEqual("Journal of Packet Tests", packet.JournalName)
        Assert.IsFalse(packet.SubmissionId.HasValue)
        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(ManuscriptLocation.Pipeline, manuscript.Location)
        Assert.AreEqual(historyCount, manuscript.History.Count)
        Assert.AreEqual(submissionCount, manuscript.Submissions.Count)

    End Sub


    <TestMethod>
    Public Sub CreatePacket_RevisionRoundRequiresRealSubmission()

        Dim manuscript As Manuscript = CreateManuscriptWithVersion()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                SubmissionPacketService.CreatePacket(
                    manuscript,
                    manuscript.Versions(0).Id,
                    "Revision packet",
                    String.Empty,
                    revisionRoundNumber:=1
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub AddLinkedFile_RecordsMetadataWithoutMovingSource()

        Dim packet As New SubmissionPacket()
        Dim sourcePath As String =
            CreateSourceFile(
                "cover-letter.docx",
                "cover"
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.CoverLetter,
                "Cover letter",
                "Tailored for journal.",
                sourcePath,
                SubmissionPacketFileStorageMode.LinkedExternal
            )

        Assert.AreEqual(
            SubmissionPacketFileStorageMode.LinkedExternal,
            packetFile.StorageMode
        )

        Assert.AreEqual(
            Path.GetFullPath(sourcePath),
            packetFile.LocalFilePath
        )

        Assert.AreEqual(
            "cover-letter.docx",
            packetFile.OriginalFileName
        )

        Assert.IsTrue(packetFile.FileSizeBytes.HasValue)
        Assert.IsTrue(packetFile.LastWriteTimeUtc.HasValue)
        Assert.IsTrue(File.Exists(sourcePath))

    End Sub


    <TestMethod>
    Public Sub AddMetadataOnlyFile_DoesNotRequirePath()

        Dim packet As New SubmissionPacket()

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.ReportingChecklist,
                String.Empty,
                "Complete in the journal portal.",
                String.Empty,
                SubmissionPacketFileStorageMode.MetadataOnly
            )

        Assert.AreEqual(
            SubmissionPacketFileStorageMode.MetadataOnly,
            packetFile.StorageMode
        )

        Assert.AreEqual(
            String.Empty,
            packetFile.LocalFilePath
        )

        Assert.AreEqual(
            "Reporting checklist",
            packetFile.Label
        )

    End Sub


    <TestMethod>
    Public Sub ManagedPacketFile_RepositorySaveCopiesIntoManagedLibraryAndPreservesSource()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim sourcePath As String =
            CreateSourceFile(
                "manuscript.docx",
                "packet manuscript"
            )

        Dim manuscript As Manuscript =
            CreateManuscriptWithVersion()

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                manuscript.Versions(0).Id,
                "Managed packet",
                String.Empty
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.Manuscript,
                "Main manuscript",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        repository.Save(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Assert.IsTrue(
            File.Exists(
                sourcePath
            )
        )

        Assert.AreNotEqual(
            Path.GetFullPath(sourcePath),
            packetFile.LocalFilePath
        )

        Assert.IsTrue(
            File.Exists(
                packetFile.LocalFilePath
            )
        )

        Dim managedLibrary As New ManagedLibraryService(
            managedDirectory
        )

        Assert.IsTrue(
            managedLibrary.IsManagedPath(
                packetFile.LocalFilePath
            )
        )

        StringAssert.Contains(
            packetFile.LocalFilePath,
            Path.Combine(
                manuscript.Id.ToString("N"),
                "packets",
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )
        )

    End Sub


    <TestMethod>
    Public Sub RemoveCommittedManagedFile_QueuesRecordRemovalWithoutDeletingPhysicalFileImmediately()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim managedLibrary As New ManagedLibraryService(
            managedDirectory
        )

        Dim packet As New SubmissionPacket()
        Dim packetFileId As Guid = Guid.NewGuid()

        Dim managedPath As String =
            Path.Combine(
                managedDirectory,
                Guid.NewGuid().ToString("N"),
                "packets",
                Guid.NewGuid().ToString("N"),
                packetFileId.ToString("N"),
                "file.docx"
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                managedPath
            )
        )

        File.WriteAllText(
            managedPath,
            "managed"
        )

        packet.Files.Add(
            New SubmissionPacketFile With {
                .Id = packetFileId,
                .StorageMode =
                    SubmissionPacketFileStorageMode.ManagedCopy,
                .LocalFilePath = managedPath
            }
        )

        SubmissionPacketService.RemoveFile(
            packet,
            packetFileId,
            managedLibrary
        )

        Assert.AreEqual(
            0,
            packet.Files.Count
        )

        ' Metadata removal is immediate on the working model. Physical
        ' deletion is deferred to the repository's reversible save staging.
        Assert.IsTrue(
            File.Exists(
                managedPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub RemoveLinkedFile_RemovesRecordButNeverDeletesExternalFile()

        Dim managedLibrary As New ManagedLibraryService(
            Path.Combine(
                _root,
                "managed"
            )
        )

        Dim packet As New SubmissionPacket()

        Dim sourcePath As String =
            CreateSourceFile(
                "figure.png",
                "figure"
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.Figure,
                "Figure 1",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.LinkedExternal
            )

        SubmissionPacketService.RemoveFile(
            packet,
            packetFile.Id,
            managedLibrary
        )

        Assert.AreEqual(
            0,
            packet.Files.Count
        )

        Assert.IsTrue(
            File.Exists(
                sourcePath
            )
        )

    End Sub


    <TestMethod>
    Public Sub UpdatePacket_CanRetargetExactVersionAndStampsModification()

        Dim manuscript As Manuscript =
            CreateManuscriptWithVersion()

        Dim secondVersion As New ManuscriptVersion With {
            .Label = "Second version"
        }

        manuscript.Versions.Add(
            secondVersion
        )

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                manuscript.Versions(0).Id,
                "Packet",
                String.Empty
            )

        Dim modified As DateTime =
            New DateTime(
                2026,
                8,
                28,
                10,
                0,
                0,
                DateTimeKind.Utc
            )

        Dim changed As Boolean =
            SubmissionPacketService.UpdatePacket(
                manuscript,
                packet.Id,
                secondVersion.Id,
                "Retargeted packet",
                "Updated",
                modifiedAtUtc:=modified
            )

        Assert.IsTrue(
            changed
        )

        Assert.AreEqual(
            secondVersion.Id,
            packet.ManuscriptVersionId
        )

        Assert.AreEqual(
            "Retargeted packet",
            packet.Label
        )

        Assert.AreEqual(
            modified,
            packet.LastModifiedAtUtc.Value
        )

    End Sub


    Private Function CreateManuscriptWithVersion() As Manuscript

        Dim manuscript As New Manuscript With {
            .Title = "Packet test manuscript"
        }

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Label = "Version 1"
            }
        )

        Return manuscript

    End Function


    Private Function CreateSourceFile(
        fileName As String,
        contents As String
    ) As String

        Dim sourceDirectory As String =
            Path.Combine(
                _root,
                "source"
            )

        Directory.CreateDirectory(
            sourceDirectory
        )

        Dim filePath As String =
            Path.Combine(
                sourceDirectory,
                fileName
            )

        File.WriteAllText(
            filePath,
            contents
        )

        Return filePath

    End Function

End Class
