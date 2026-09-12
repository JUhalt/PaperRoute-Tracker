Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManagedPacketDeletionTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub Transaction_StagesOrphanedManagedPacketFileAndRollbackRestoresIt()

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim manuscript As Manuscript =
            CreateManuscriptWithManagedPacketReference(
                managedRoot
            )

        Dim managedFilePath As String =
            manuscript.SubmissionPackets(0).Files(0).LocalFilePath

        manuscript.SubmissionPackets(0).Files.Clear()

        Dim service As New ManagedPacketDeletionService(
            managedRoot
        )

        Using transaction =
            service.BeginDeletionTransaction(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            Assert.AreEqual(
                1,
                transaction.StagedDirectoryCount
            )

            Assert.IsFalse(
                File.Exists(
                    managedFilePath
                )
            )

            transaction.Rollback()

        End Using

        Assert.IsTrue(
            File.Exists(
                managedFilePath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Transaction_CommitDeletesOrphanedManagedPacketFile()

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim manuscript As Manuscript =
            CreateManuscriptWithManagedPacketReference(
                managedRoot
            )

        Dim managedFilePath As String =
            manuscript.SubmissionPackets(0).Files(0).LocalFilePath

        manuscript.SubmissionPackets(0).Files.Clear()

        Dim service As New ManagedPacketDeletionService(
            managedRoot
        )

        Using transaction =
            service.BeginDeletionTransaction(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            transaction.Commit()

        End Using

        Assert.IsFalse(
            File.Exists(
                managedFilePath
            )
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    managedRoot,
                    ManagedPacketDeletionService.StagingFolderName
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Recovery_RestoresReferencedStagedPacketFile()

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim manuscript As Manuscript =
            CreateManuscriptWithManagedPacketReference(
                managedRoot
            )

        Dim packet As SubmissionPacket =
            manuscript.SubmissionPackets(0)

        Dim packetFile As SubmissionPacketFile =
            packet.Files(0)

        Dim originalDirectory As String =
            Path.GetDirectoryName(
                packetFile.LocalFilePath
            )

        Dim transactionId As Guid =
            Guid.NewGuid()

        Dim stagedDirectory As String =
            Path.Combine(
                managedRoot,
                ManagedPacketDeletionService.StagingFolderName,
                transactionId.ToString("N"),
                manuscript.Id.ToString("N"),
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                stagedDirectory
            )
        )

        Directory.Move(
            originalDirectory,
            stagedDirectory
        )

        Dim stagedFilePath As String =
            Directory.GetFiles(
                stagedDirectory
            )(0)

        Assert.IsTrue(
            File.Exists(
                stagedFilePath
            )
        )

        Dim service As New ManagedPacketDeletionService(
            managedRoot
        )

        service.RecoverStagedDeletions(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Assert.IsTrue(
            File.Exists(
                packetFile.LocalFilePath
            )
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    managedRoot,
                    ManagedPacketDeletionService.StagingFolderName
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Recovery_DeletesUnreferencedStagedPacketFile()

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim manuscript As Manuscript =
            CreateManuscriptWithManagedPacketReference(
                managedRoot
            )

        Dim packet As SubmissionPacket =
            manuscript.SubmissionPackets(0)

        Dim packetFile As SubmissionPacketFile =
            packet.Files(0)

        Dim originalDirectory As String =
            Path.GetDirectoryName(
                packetFile.LocalFilePath
            )

        Dim stagedDirectory As String =
            Path.Combine(
                managedRoot,
                ManagedPacketDeletionService.StagingFolderName,
                Guid.NewGuid().ToString("N"),
                manuscript.Id.ToString("N"),
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                stagedDirectory
            )
        )

        Directory.Move(
            originalDirectory,
            stagedDirectory
        )

        manuscript.SubmissionPackets.Clear()

        Dim service As New ManagedPacketDeletionService(
            managedRoot
        )

        service.RecoverStagedDeletions(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Assert.IsFalse(
            Directory.Exists(
                stagedDirectory
            )
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterManagedPacketFileRemovalDeletesPhysicalManagedCopy()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim sourceDirectory As String =
            Path.Combine(
                _root,
                "source"
            )

        Directory.CreateDirectory(
            sourceDirectory
        )

        Dim sourcePath As String =
            Path.Combine(
                sourceDirectory,
                "cover-letter.docx"
            )

        File.WriteAllText(
            sourcePath,
            "source remains"
        )

        Dim manuscript As New Manuscript With {
            .Title = "Managed packet removal"
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Submission version"
        }

        manuscript.Versions.Add(
            version
        )

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                version.Id,
                "Packet",
                String.Empty
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.CoverLetter,
                "Cover letter",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedRoot
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            packetFile.LocalFilePath

        Assert.IsTrue(
            File.Exists(
                managedPath
            )
        )

        SubmissionPacketService.RemoveFile(
            packet,
            packetFile.Id,
            New ManagedLibraryService(
                managedRoot
            )
        )

        repository.Save(
            library
        )

        Assert.IsFalse(
            File.Exists(
                managedPath
            )
        )

        Assert.IsTrue(
            File.Exists(
                sourcePath
            )
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterWholePacketRemovalDeletesItsManagedFiles()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedRoot As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim sourceDirectory As String =
            Path.Combine(
                _root,
                "source"
            )

        Directory.CreateDirectory(
            sourceDirectory
        )

        Dim sourcePath As String =
            Path.Combine(
                sourceDirectory,
                "title-page.docx"
            )

        File.WriteAllText(
            sourcePath,
            "source remains"
        )

        Dim manuscript As New Manuscript With {
            .Title = "Packet delete"
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Submission version"
        }

        manuscript.Versions.Add(
            version
        )

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                version.Id,
                "Packet",
                String.Empty
            )

        Dim packetFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.TitlePage,
                "Title page",
                String.Empty,
                sourcePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedRoot
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            packetFile.LocalFilePath

        SubmissionPacketService.RemovePacket(
            manuscript,
            packet.Id,
            New ManagedLibraryService(
                managedRoot
            )
        )

        repository.Save(
            library
        )

        Assert.AreEqual(
            0,
            manuscript.SubmissionPackets.Count
        )

        Assert.IsFalse(
            File.Exists(
                managedPath
            )
        )

        Assert.IsTrue(
            File.Exists(
                sourcePath
            )
        )

    End Sub


    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Recovery_ExistingDestinationPreservesStagedSnapshot(
        hasConflictingFile As Boolean
    )

        Dim managedRoot As String = Path.Combine(_root, "managed")
        Dim manuscript As Manuscript = CreateManuscriptWithManagedPacketReference(managedRoot)
        Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
        Dim packetFile As SubmissionPacketFile = packet.Files(0)
        Dim originalDirectory As String = Path.GetDirectoryName(packetFile.LocalFilePath)
        Dim stagedDirectory As String =
            Path.Combine(
                managedRoot,
                ManagedPacketDeletionService.StagingFolderName,
                Guid.NewGuid().ToString("N"),
                manuscript.Id.ToString("N"),
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )

        Directory.CreateDirectory(Path.GetDirectoryName(stagedDirectory))
        Directory.Move(originalDirectory, stagedDirectory)
        Directory.CreateDirectory(originalDirectory)

        If hasConflictingFile Then
            File.WriteAllText(packetFile.LocalFilePath, "different file")
        End If

        Dim service As New ManagedPacketDeletionService(managedRoot)

        Assert.ThrowsExactly(Of IOException)(
            Sub()
                service.RecoverStagedDeletions(New List(Of Manuscript) From {manuscript})
            End Sub
        )

        Assert.AreEqual(
            "managed",
            File.ReadAllText(Path.Combine(stagedDirectory, "manuscript.docx"))
        )

        If hasConflictingFile Then
            Assert.AreEqual("different file", File.ReadAllText(packetFile.LocalFilePath))
        Else
            Assert.IsFalse(File.Exists(packetFile.LocalFilePath))
        End If

    End Sub


    <TestMethod>
    Public Sub RepositorySave_WhenJsonWriteFailsRestoresManagedPacketFileAndPreservesExternalFile()

        Dim dataDirectory As String = Path.Combine(_root, "data")
        Dim managedRoot As String = Path.Combine(_root, "managed")
        Dim manuscript As Manuscript = CreateManuscriptWithManagedPacketReference(managedRoot)
        Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
        Dim packetFile As SubmissionPacketFile = packet.Files(0)
        Dim managedPath As String = packetFile.LocalFilePath
        Dim externalPath As String = Path.Combine(_root, "external.txt")

        File.WriteAllText(externalPath, "external source")

        SubmissionPacketService.AddFile(
            packet,
            SubmissionPacketFileRole.CoverLetter,
            "Linked cover letter",
            String.Empty,
            externalPath,
            SubmissionPacketFileStorageMode.LinkedExternal
        )

        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        Dim library As New List(Of Manuscript) From {manuscript}

        repository.Save(library)

        Dim originalJson As String = File.ReadAllText(repository.DataFilePath)

        SubmissionPacketService.RemovePacket(
            manuscript,
            packet.Id,
            New ManagedLibraryService(managedRoot)
        )

        ' A directory at the temporary JSON path forces failure after the
        ' managed deletion has been staged but before metadata is replaced.
        Directory.CreateDirectory(Path.Combine(dataDirectory, "manuscripts.tmp"))

        Assert.ThrowsExactly(Of UnauthorizedAccessException)(
            Sub()
                repository.Save(library)
            End Sub
        )

        Assert.AreEqual(originalJson, File.ReadAllText(repository.DataFilePath))
        Assert.AreEqual("managed", File.ReadAllText(managedPath))
        Assert.AreEqual("external source", File.ReadAllText(externalPath))
        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(managedRoot, ManagedPacketDeletionService.StagingFolderName)
            )
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_ManagedCopyFromExistingPacketFileSurvivesOriginalRemoval()

        Dim dataDirectory As String = Path.Combine(_root, "data")
        Dim managedRoot As String = Path.Combine(_root, "managed")
        Dim manuscript As Manuscript = CreateManuscriptWithManagedPacketReference(managedRoot)
        Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
        Dim originalFile As SubmissionPacketFile = packet.Files(0)

        Dim copiedFile As SubmissionPacketFile =
            SubmissionPacketService.AddFile(
                packet,
                SubmissionPacketFileRole.Manuscript,
                "Second snapshot",
                String.Empty,
                originalFile.LocalFilePath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        Dim library As New List(Of Manuscript) From {manuscript}

        repository.Save(library)

        Assert.AreNotEqual(originalFile.LocalFilePath, copiedFile.LocalFilePath)

        SubmissionPacketService.RemoveFile(
            packet,
            originalFile.Id,
            New ManagedLibraryService(managedRoot)
        )

        repository.Save(library)

        Assert.IsTrue(
            File.Exists(copiedFile.LocalFilePath),
            "Removing the first packet record must not delete the second record's managed snapshot."
        )
        Assert.AreEqual("managed", File.ReadAllText(copiedFile.LocalFilePath))

    End Sub


    Private Function CreateManuscriptWithManagedPacketReference(
        managedRoot As String
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Title = "Managed packet staging test"
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Version"
        }

        manuscript.Versions.Add(
            version
        )

        Dim packet As New SubmissionPacket With {
            .Label = "Packet",
            .ManuscriptVersionId = version.Id
        }

        Dim packetFile As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.Manuscript,
            .Label = "Manuscript",
            .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy
        }

        Dim fileDirectory As String =
            Path.Combine(
                managedRoot,
                manuscript.Id.ToString("N"),
                "packets",
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )

        Directory.CreateDirectory(
            fileDirectory
        )

        packetFile.LocalFilePath =
            Path.Combine(
                fileDirectory,
                "manuscript.docx"
            )

        File.WriteAllText(
            packetFile.LocalFilePath,
            "managed"
        )

        packet.Files.Add(
            packetFile
        )

        manuscript.SubmissionPackets.Add(
            packet
        )

        Return manuscript

    End Function

End Class
