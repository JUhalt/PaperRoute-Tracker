Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionPacketReferenceSafetyTests

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
    Public Sub DeleteVersion_UsedByPacketRejectsRemovalUntilPacketIsRemoved()

        Dim manuscript As New Manuscript With {
            .Title = "Packet reference safety"
        }
        Dim version As New ManuscriptVersion With {
            .Label = "Exact submitted version"
        }
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                version.Id,
                "Submission packet",
                String.Empty
            )

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub()
                ManuscriptVersionService.DeleteVersion(manuscript, version.Id)
            End Sub
        )

        Assert.AreEqual(1, manuscript.Versions.Count)
        Assert.AreSame(version, manuscript.Versions(0))
        Assert.AreEqual(version.Id, manuscript.CurrentVersionId.Value)
        Assert.AreEqual(version.Id, packet.ManuscriptVersionId)

        SubmissionPacketService.RemovePacket(
            manuscript,
            packet.Id,
            New ManagedLibraryService(Path.Combine(_root, "managed"))
        )

        ManuscriptVersionService.DeleteVersion(manuscript, version.Id)

        Assert.AreEqual(0, manuscript.Versions.Count)
        Assert.IsFalse(manuscript.CurrentVersionId.HasValue)

    End Sub


    <TestMethod>
    <DataRow("version")>
    <DataRow("submission")>
    Public Sub RepositorySave_DanglingPacketReferencePreservesDatabaseAndManagedFiles(
        removedReference As String
    )

        Dim dataDirectory As String = Path.Combine(_root, "data")
        Dim managedRoot As String = Path.Combine(_root, "managed")
        Dim sourcePath As String = Path.Combine(_root, "source.txt")
        File.WriteAllText(sourcePath, "Original manuscript content")

        Dim manuscript As New Manuscript With {
            .Title = "Packet reference safety"
        }
        Dim version As New ManuscriptVersion With {
            .Label = "Exact submitted version",
            .IsManagedCopy = True,
            .LocalFilePath = sourcePath
        }
        Dim submission As New JournalSubmission With {
            .JournalName = "Test Journal",
            .SubmittedDate = New DateTime(2026, 9, 1)
        }
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id
        manuscript.Submissions.Add(submission)

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                version.Id,
                "Submission packet",
                String.Empty,
                submissionId:=submission.Id
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.Manuscript,
                "Exact manuscript file",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        repository.Save(New List(Of Manuscript) From {manuscript})

        Dim originalJson As String = File.ReadAllText(repository.DataFilePath)
        Dim originalManagedVersionPath As String = version.LocalFilePath
        Dim originalManagedPacketPath As String = packetFile.LocalFilePath
        Dim working As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)

        If removedReference = "version" Then
            working.Versions.Clear()
            working.CurrentVersionId = Nothing
        Else
            working.Submissions.Clear()
        End If

        ' Queue both deletion and a new copy. Invalid references must be
        ' rejected before either physical operation can begin.
        Dim workingPacket As SubmissionPacket = working.SubmissionPackets(0)
        workingPacket.Files.Clear()

        Dim pendingFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                workingPacket,
                SubmissionPacketFileRole.CoverLetter,
                "Pending copy",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                repository.Save(New List(Of Manuscript) From {working})
            End Sub
        )

        Assert.AreEqual(originalJson, File.ReadAllText(repository.DataFilePath))
        Assert.AreEqual(sourcePath, pendingFile.LocalFilePath)
        Assert.AreEqual("Original manuscript content", File.ReadAllText(sourcePath))
        Assert.AreEqual("Original manuscript content", File.ReadAllText(originalManagedVersionPath))
        Assert.AreEqual("Original manuscript content", File.ReadAllText(originalManagedPacketPath))
        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    managedRoot,
                    working.Id.ToString("N"),
                    "packets",
                    workingPacket.Id.ToString("N"),
                    pendingFile.Id.ToString("N")
                )
            )
        )

        Dim reloaded As List(Of Manuscript) = repository.Load()
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup)
        Assert.AreEqual(1, reloaded.Count)
        Assert.AreEqual(version.Id, reloaded(0).SubmissionPackets(0).ManuscriptVersionId)
        Assert.AreEqual(submission.Id, reloaded(0).SubmissionPackets(0).SubmissionId.Value)
        Assert.AreEqual(1, reloaded(0).Versions.Count)
        Assert.AreEqual(1, reloaded(0).Submissions.Count)

    End Sub

End Class
