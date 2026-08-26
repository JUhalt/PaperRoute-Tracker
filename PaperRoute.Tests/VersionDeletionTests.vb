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

End Class
