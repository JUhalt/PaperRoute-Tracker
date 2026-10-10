Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class VersionDeletionTests

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
    Public Sub DeleteVersion_CurrentVersionFallsBackToNewestRemaining()

        Dim manuscript As Manuscript =
            BuildThreeVersionManuscript()

        Dim currentId As Guid =
            manuscript.Versions(2).Id

        Dim expectedFallback As Guid =
            manuscript.Versions(1).Id

        Dim deleted As ManuscriptVersion =
            ManuscriptVersionService.DeleteVersion(
                manuscript,
                currentId
            )

        Assert.AreEqual(
            currentId,
            deleted.Id
        )

        Assert.AreEqual(
            2,
            manuscript.Versions.Count
        )

        Assert.IsTrue(
            manuscript.CurrentVersionId.HasValue
        )

        Assert.AreEqual(
            expectedFallback,
            manuscript.CurrentVersionId.Value
        )

    End Sub


    <TestMethod>
    Public Sub DeleteVersion_LastVersionClearsCurrentVersion()

        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid(),
            .CreatedDate = New DateTime(2026, 1, 1)
        }

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            version
        )

        manuscript.CurrentVersionId =
            version.Id

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        Assert.AreEqual(
            0,
            manuscript.Versions.Count
        )

        Assert.IsFalse(
            manuscript.CurrentVersionId.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub DeleteVersion_NonCurrentVersionPreservesCurrentVersion()

        Dim manuscript As Manuscript =
            BuildThreeVersionManuscript()

        Dim currentId As Guid =
            manuscript.Versions(2).Id

        Dim deletedId As Guid =
            manuscript.Versions(0).Id

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            deletedId
        )

        Assert.AreEqual(
            currentId,
            manuscript.CurrentVersionId.Value
        )

    End Sub


    <TestMethod>
    Public Sub DeleteVersion_UnknownIdentifierIsRejected()

        Dim manuscript As Manuscript =
            BuildThreeVersionManuscript()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                ManuscriptVersionService.DeleteVersion(
                    manuscript,
                    Guid.NewGuid()
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterVersionDeletionRemovesManagedSnapshot()

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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Delete managed version"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            version.LocalFilePath

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                managedPath
            )

        Assert.IsTrue(
            File.Exists(
                managedPath
            )
        )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                managedVersionDirectory
            )
        )

        Dim reloaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            0,
            reloaded(0).Versions.Count
        )

        Assert.IsFalse(
            reloaded(0).CurrentVersionId.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub ManagedDeletionTransaction_RollbackRestoresSnapshotDirectory()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-rollback"
            )

        Dim manuscript As New Manuscript()
        Dim versionId As Guid =
            Guid.NewGuid()

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        Directory.CreateDirectory(
            versionDirectory
        )

        File.WriteAllText(
            Path.Combine(
                versionDirectory,
                "snapshot.txt"
            ),
            "keep me"
        )

        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        Dim transaction As ManagedLibraryService.ManagedVersionDeletionTransaction =
            service.BeginVersionDeletionTransaction(
                New Manuscript() {
                    manuscript
                }
            )

        Assert.AreEqual(
            1,
            transaction.StagedDirectoryCount
        )

        Assert.IsFalse(
            Directory.Exists(
                versionDirectory
            )
        )

        transaction.Rollback()

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    versionDirectory,
                    "snapshot.txt"
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub ManagedDeletionRecovery_RestoresReferencedInterruptedDeletion()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-recovery"
            )

        Dim manuscript As New Manuscript()
        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid()
        }

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N"),
                "versions",
                version.Id.ToString("N")
            )

        Directory.CreateDirectory(
            versionDirectory
        )

        File.WriteAllText(
            Path.Combine(
                versionDirectory,
                "snapshot.txt"
            ),
            "restore me"
        )

        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        Dim transaction As ManagedLibraryService.ManagedVersionDeletionTransaction =
            service.BeginVersionDeletionTransaction(
                New Manuscript() {
                    manuscript
                }
            )

        Assert.IsFalse(
            Directory.Exists(
                versionDirectory
            )
        )

        manuscript.Versions.Add(
            version
        )

        manuscript.CurrentVersionId =
            version.Id

        service.RecoverStagedVersionDeletions(
            New Manuscript() {
                manuscript
            }
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    versionDirectory,
                    "snapshot.txt"
                )
            )
        )

        transaction.Dispose()

    End Sub


    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub ManagedDeletionRecovery_PutsBackStagedVersionOfManuscriptNotInLibrary(
        originalPlaceIsOccupied As Boolean
    )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-foreign"
            )

        ' A manuscript another computer's library holds; this one's does not.
        Dim foreignManuscript As New Manuscript()
        Dim versionId As Guid =
            Guid.NewGuid()

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                foreignManuscript.Id.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        Dim snapshotPath As String =
            Path.Combine(
                versionDirectory,
                "snapshot.bin"
            )

        Dim snapshotBytes As Byte() =
            New Byte() {80, 75, 3, 4, 255, 0, 127}

        Directory.CreateDirectory(
            versionDirectory
        )

        File.WriteAllBytes(
            snapshotPath,
            snapshotBytes
        )

        ' An interrupted save by the other computer staged this folder.
        Dim stagedDirectory As String =
            StageVersionDirectory(
                managedDirectory,
                foreignManuscript.Id,
                versionId
            )

        If originalPlaceIsOccupied Then

            Directory.CreateDirectory(
                versionDirectory
            )

            File.WriteAllText(
                snapshotPath,
                "different file"
            )

        End If

        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        service.RecoverStagedVersionDeletions(
            New Manuscript() {
                New Manuscript With {
                    .Title = "Another manuscript"
                }
            }
        )

        Dim stagedSnapshotPath As String =
            Path.Combine(
                stagedDirectory,
                "snapshot.bin"
            )

        If originalPlaceIsOccupied Then

            Assert.IsTrue(
                File.Exists(
                    stagedSnapshotPath
                ),
                "Recovery must keep the staged copy when its original place is taken."
            )

            CollectionAssert.AreEqual(
                snapshotBytes,
                File.ReadAllBytes(
                    stagedSnapshotPath
                )
            )

            Assert.AreEqual(
                "different file",
                File.ReadAllText(
                    snapshotPath
                )
            )

        Else

            Assert.IsTrue(
                File.Exists(
                    snapshotPath
                ),
                "Recovery must put back a staged version of a manuscript the library does not contain."
            )

            CollectionAssert.AreEqual(
                snapshotBytes,
                File.ReadAllBytes(
                    snapshotPath
                )
            )

            Assert.IsFalse(
                Directory.Exists(
                    Path.Combine(
                        managedDirectory,
                        ".paperroute-version-delete"
                    )
                )
            )

        End If

    End Sub


    <TestMethod>
    Public Sub ManagedDeletionRecovery_DeletesStagedVersionOfManuscriptInLibrary()

        ' This passes before and after the put-back of foreign manuscripts'
        ' staged versions: it pins that a version the library itself dropped
        ' is still deleted, not put back.
        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-dropped"
            )

        Dim manuscript As New Manuscript()
        Dim versionId As Guid =
            Guid.NewGuid()

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        Directory.CreateDirectory(
            versionDirectory
        )

        File.WriteAllText(
            Path.Combine(
                versionDirectory,
                "snapshot.txt"
            ),
            "removed on purpose"
        )

        Dim stagedDirectory As String =
            StageVersionDirectory(
                managedDirectory,
                manuscript.Id,
                versionId
            )

        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        ' The library still holds the manuscript without this version.
        service.RecoverStagedVersionDeletions(
            New Manuscript() {
                manuscript
            }
        )

        Assert.IsFalse(
            Directory.Exists(
                stagedDirectory
            )
        )

        Assert.IsFalse(
            Directory.Exists(
                versionDirectory
            ),
            "Recovery must not put back a version the library no longer references."
        )

    End Sub


    <TestMethod>
    Public Sub DeleteOnWorkingCloneDoesNotTouchOriginalUntilRepositorySave()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-clone"
            )

        Dim manuscript As New Manuscript()
        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid(),
            .CreatedDate = New DateTime(2026, 4, 1)
        }

        manuscript.Versions.Add(
            version
        )

        manuscript.CurrentVersionId =
            version.Id

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                manuscript
            )

        ManuscriptVersionService.DeleteVersion(
            clone,
            version.Id
        )

        Assert.AreEqual(
            1,
            manuscript.Versions.Count
        )

        Assert.AreEqual(
            0,
            clone.Versions.Count
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterLoadRemovesDeletedVersionSnapshot()

        ' Passes before the save baseline too: it pins that a version removed
        ' from a loaded library is still swept, now because the load
        ' referenced it.
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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Delete after load"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Call New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        ).Save(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                version.LocalFilePath
            )

        ' A restart: the repository learns the library from disk.
        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As List(Of Manuscript) =
            repository.Load()

        ManuscriptVersionService.DeleteVersion(
            library(0),
            version.Id
        )

        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                managedVersionDirectory
            )
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_LeavesADeletedVersionFolderInUseAndNamesIt()

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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Version in use"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            version.LocalFilePath

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                managedPath
            )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        ' The snapshot is open in another program, so its folder cannot move.
        Using File.Open(
            managedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        )

            repository.Save(
                library
            )

        End Using

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                managedPath
            ),
            "A folder in use is left where it is."
        )

        Assert.AreEqual(
            0,
            New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            ).Load()(0).Versions.Count,
            "The save itself went through."
        )

        StringAssert.Contains(
            repository.LastSaveWarning,
            managedVersionDirectory
        )

        Dim stagingRoot As String =
            Path.Combine(
                managedDirectory,
                ".paperroute-version-delete"
            )

        Assert.IsFalse(
            Directory.Exists(
                stagingRoot
            ) AndAlso
            Directory.GetFiles(
                stagingRoot,
                "*",
                SearchOption.AllDirectories
            ).Length > 0,
            "Nothing of the folder in use was staged."
        )

        ' Once the file is closed, the next save finishes the removal.
        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                managedVersionDirectory
            ),
            "The next save removes the folder once the file is closed."
        )

        Assert.AreEqual(
            String.Empty,
            repository.LastSaveWarning
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterVersionDeletionMovesSnapshotToRemoved()

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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Remove to the removed folder"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            version.LocalFilePath

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                managedPath
            )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                managedVersionDirectory
            )
        )

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                Path.Combine(
                    RemovedCopyOf(
                        managedDirectory,
                        managedVersionDirectory
                    ),
                    Path.GetFileName(
                        managedPath
                    )
                )
            ),
            "A removed version's snapshot waits under removed\<date>, at its path in the library."
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    managedDirectory,
                    ".paperroute-version-delete"
                )
            ),
            "Nothing is left in staging."
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterAddingBackARemovedSnapshotMakesAFreshManagedCopy()

        ' The guide says a removed file can be added again as a version and
        ' PaperRoute makes a fresh managed copy. A file under removed is
        ' inside the library, so the copy must not be skipped as already
        ' managed: the record would point into removed, and every later
        ' backup would leave its only file out.
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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Put back a removed version"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                version.LocalFilePath
            )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        repository.Save(
            library
        )

        Dim removedSnapshot As String =
            Path.Combine(
                RemovedCopyOf(
                    managedDirectory,
                    managedVersionDirectory
                ),
                Path.GetFileName(
                    version.LocalFilePath
                )
            )

        Assert.IsTrue(
            File.Exists(
                removedSnapshot
            )
        )

        ' Put it back as the guide says: add it again, from removed.
        Dim putBack As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Put back",
                String.Empty,
                removedSnapshot,
                True,
                makeCurrent:=True
            )

        repository.Save(
            library
        )

        StringAssert.StartsWith(
            putBack.LocalFilePath,
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N"),
                "versions",
                putBack.Id.ToString("N")
            ) & Path.DirectorySeparatorChar,
            "The put-back version gets a fresh managed copy under its own folder."
        )

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                putBack.LocalFilePath
            )
        )

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                removedSnapshot
            ),
            "The removed copy stays where it is."
        )

        Dim backupPath As String =
            Path.Combine(
                _root,
                "put-back.zip"
            )

        Call New PortableBackupService(
            managedDirectory
        ).CreateBackup(
            backupPath,
            library,
            repository
        )

        Assert.AreEqual(
            1,
            New PortableRestoreService(
                managedDirectory
            ).InspectBackup(
                backupPath
            ).ManagedFileCount,
            "The next backup holds the put-back version's file."
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_AfterManuscriptDeletionMovesItsFoldersToRemoved()

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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim coverLetterPath As String =
            Path.Combine(
                _root,
                "cover-letter.txt"
            )

        File.WriteAllText(
            coverLetterPath,
            "Cover letter bytes."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Deleted manuscript"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
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
                coverLetterPath,
                SubmissionPacketFileStorageMode.ManagedCopy
            )

        ' A decision letter PaperRoute keeps a managed copy of, at
        ' <manuscript>\<submission>\<item>.
        Dim letterPath As String =
            Path.Combine(
                _root,
                "decision-letter.txt"
            )

        File.WriteAllText(
            letterPath,
            "Decision letter bytes."
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Fictional Journal of Psychology",
            .SubmittedDate = New DateTime(2026, 4, 1)
        }

        Dim letter As New CorrespondenceItem With {
            .ItemDate = New DateTime(2026, 5, 1),
            .Type = CorrespondenceType.DecisionLetter,
            .Title = "Decision letter",
            .LocalFilePath = letterPath,
            .IsManagedCopy = True
        }

        submission.Correspondence.Add(
            letter
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim versionDirectory As String =
            Path.GetDirectoryName(
                version.LocalFilePath
            )

        Dim packetFileDirectory As String =
            Path.GetDirectoryName(
                packetFile.LocalFilePath
            )

        Dim letterDirectory As String =
            Path.GetDirectoryName(
                letter.LocalFilePath
            )

        Dim manuscriptDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N")
            )

        Assert.IsTrue(
            Directory.Exists(
                manuscriptDirectory
            )
        )

        StringAssert.StartsWith(
            letterDirectory,
            manuscriptDirectory & Path.DirectorySeparatorChar,
            "The save made a managed copy of the letter under the manuscript's folder."
        )

        ' Delete the manuscript, as the board's Delete does, and save.
        library.Remove(
            manuscript
        )

        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                manuscriptDirectory
            ),
            "Nothing of a deleted manuscript stays under its folder."
        )

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                Path.Combine(
                    RemovedCopyOf(
                        managedDirectory,
                        versionDirectory
                    ),
                    Path.GetFileName(
                        version.LocalFilePath
                    )
                )
            ),
            "The deleted manuscript's version snapshot waits under removed\<date>."
        )

        Assert.AreEqual(
            "Cover letter bytes.",
            File.ReadAllText(
                Path.Combine(
                    RemovedCopyOf(
                        managedDirectory,
                        packetFileDirectory
                    ),
                    Path.GetFileName(
                        packetFile.LocalFilePath
                    )
                )
            ),
            "The deleted manuscript's packet file waits under removed\<date>."
        )

        Assert.AreEqual(
            "Decision letter bytes.",
            File.ReadAllText(
                Path.Combine(
                    RemovedCopyOf(
                        managedDirectory,
                        letterDirectory
                    ),
                    Path.GetFileName(
                        letter.LocalFilePath
                    )
                )
            ),
            "The deleted manuscript's managed correspondence copy waits under removed\<date>."
        )

        Assert.AreEqual(
            0,
            repository.Load().Count
        )

    End Sub


    <TestMethod>
    Public Sub RepositorySave_WhenJsonWriteFailsRestoresDeletedVersionSnapshot()

        ' Passes before the removed folder too: it pins that a failed save
        ' still puts a staged snapshot back and removes nothing.
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

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Failed save"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            version.LocalFilePath

        Dim originalJson As String =
            File.ReadAllText(
                repository.DataFilePath
            )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        ' A directory at the temporary JSON path forces failure after the
        ' snapshot has been staged but before metadata is replaced.
        Directory.CreateDirectory(
            Path.Combine(
                dataDirectory,
                "manuscripts.tmp"
            )
        )

        Assert.ThrowsExactly(Of UnauthorizedAccessException)(
            Sub()
                repository.Save(
                    library
                )
            End Sub
        )

        Assert.AreEqual(
            originalJson,
            File.ReadAllText(
                repository.DataFilePath
            )
        )

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                managedPath
            )
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    managedDirectory,
                    "removed"
                )
            ),
            "A failed save removes nothing."
        )

    End Sub


    <TestMethod>
    Public Sub ManagedDeletionRecovery_MovesStagedVersionOfManuscriptInLibraryToRemoved()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-discard"
            )

        Dim manuscript As New Manuscript()
        Dim versionId As Guid =
            Guid.NewGuid()

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        Directory.CreateDirectory(
            versionDirectory
        )

        File.WriteAllText(
            Path.Combine(
                versionDirectory,
                "snapshot.txt"
            ),
            "removed on purpose"
        )

        Dim stagedDirectory As String =
            StageVersionDirectory(
                managedDirectory,
                manuscript.Id,
                versionId
            )

        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        ' The library still holds the manuscript without this version.
        service.RecoverStagedVersionDeletions(
            New Manuscript() {
                manuscript
            }
        )

        Assert.IsFalse(
            Directory.Exists(
                stagedDirectory
            )
        )

        Assert.IsFalse(
            Directory.Exists(
                versionDirectory
            )
        )

        Assert.AreEqual(
            "removed on purpose",
            File.ReadAllText(
                Path.Combine(
                    RemovedCopyOf(
                        managedDirectory,
                        versionDirectory
                    ),
                    "snapshot.txt"
                )
            ),
            "A load-time discard moves the snapshot under removed\<date> instead of deleting it."
        )

    End Sub


    Private Shared Function BuildThreeVersionManuscript() As Manuscript

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .Label = "Draft",
                .CreatedDate = New DateTime(2026, 1, 1)
            }
        )

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .Label = "Submitted",
                .CreatedDate = New DateTime(2026, 2, 1)
            }
        )

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .Label = "Revision",
                .CreatedDate = New DateTime(2026, 3, 1)
            }
        )

        manuscript.CurrentVersionId =
            manuscript.Versions(2).Id

        Return manuscript

    End Function


    ' Moves a version folder into the staging folder the way an interrupted
    ' save leaves it, and returns the staged path. The emptied versions folder
    ' stays in place, as the sweep leaves it.
    Private Shared Function StageVersionDirectory(
        managedDirectory As String,
        manuscriptId As Guid,
        versionId As Guid
    ) As String

        Dim versionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscriptId.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        Dim stagedDirectory As String =
            Path.Combine(
                managedDirectory,
                ".paperroute-version-delete",
                Guid.NewGuid().ToString("N"),
                manuscriptId.ToString("N"),
                versionId.ToString("N")
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                stagedDirectory
            )
        )

        Directory.Move(
            versionDirectory,
            stagedDirectory
        )

        Return stagedDirectory

    End Function

End Class
