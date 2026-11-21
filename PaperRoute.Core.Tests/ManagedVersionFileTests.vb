Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManagedVersionFileTests

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
    Public Sub Save_CopiesManagedVersionAndPreservesImmutableSnapshot()

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

        Dim sourceDirectory As String =
            Path.Combine(
                _root,
                "working"
            )

        Directory.CreateDirectory(
            sourceDirectory
        )

        Dim sourcePath As String =
            Path.Combine(
                sourceDirectory,
                "manuscript.txt"
            )

        File.WriteAllText(
            sourcePath,
            "Original submitted snapshot."
        )

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript With {
            .Title = "Immutable version test"
        }

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .Label = "Submitted version",
                .LocalFilePath = sourcePath,
                .IsManagedCopy = True
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim manuscripts As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            manuscripts
        )

        Dim managedPath As String =
            manuscript.Versions(0).LocalFilePath

        Assert.IsTrue(
            File.Exists(
                managedPath
            )
        )

        Assert.IsTrue(
            Path.GetFullPath(
                managedPath
            ).StartsWith(
                Path.GetFullPath(
                    managedDirectory
                ),
                StringComparison.OrdinalIgnoreCase
            )
        )

        Assert.AreEqual(
            "Original submitted snapshot.",
            File.ReadAllText(
                managedPath
            )
        )

        File.WriteAllText(
            sourcePath,
            "Later working-copy change."
        )

        repository.Save(
            manuscripts
        )

        Assert.AreEqual(
            managedPath,
            manuscript.Versions(0).LocalFilePath
        )

        Assert.AreEqual(
            "Original submitted snapshot.",
            File.ReadAllText(
                managedPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Save_MissingManagedVersionSourceFailsWithoutChangingReference()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "missing-data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "missing-managed"
            )

        Dim missingPath As String =
            Path.Combine(
                _root,
                "does-not-exist.txt"
            )

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .LocalFilePath = missingPath,
                .IsManagedCopy = True
            }
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Assert.ThrowsExactly(Of FileNotFoundException)(
            Sub()
                repository.Save(
                    New List(Of Manuscript) From {
                        manuscript
                    }
                )
            End Sub
        )

        Assert.AreEqual(
            missingPath,
            manuscript.Versions(0).LocalFilePath
        )

    End Sub


    <TestMethod>
    Public Sub BackupRestore_RoundTripsManagedVersionSnapshot()

        Dim sourceData As String =
            Path.Combine(
                _root,
                "source-data"
            )

        Dim sourceManaged As String =
            Path.Combine(
                _root,
                "source-managed"
            )

        Dim workingDirectory As String =
            Path.Combine(
                _root,
                "working"
            )

        Directory.CreateDirectory(
            workingDirectory
        )

        Dim workingPath As String =
            Path.Combine(
                workingDirectory,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Revision snapshot for backup."
        )

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript With {
            .Title = "Backup version test"
        }

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .Label = "Revision 1",
                .LocalFilePath = workingPath,
                .IsManagedCopy = True,
                .RevisionRoundNumber = 1
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim sourceRepository As New ManuscriptRepository(
            sourceData,
            sourceManaged
        )

        Dim sourceManuscripts As New List(Of Manuscript) From {
            manuscript
        }

        sourceRepository.Save(
            sourceManuscripts
        )

        File.Delete(
            workingPath
        )

        Dim backupPath As String =
            Path.Combine(
                _root,
                "version-backup.zip"
            )

        Dim backupService As New PortableBackupService(
            sourceManaged
        )

        backupService.CreateBackup(
            backupPath,
            sourceManuscripts,
            sourceRepository
        )

        Dim inspection As BackupInspection =
            New PortableRestoreService(
                sourceManaged
            ).InspectBackup(
                backupPath
            )

        Assert.AreEqual(
            1,
            inspection.ManagedFileCount
        )

        Dim targetData As String =
            Path.Combine(
                _root,
                "target-data"
            )

        Dim targetManaged As String =
            Path.Combine(
                _root,
                "target-managed"
            )

        Dim targetRepository As New ManuscriptRepository(
            targetData,
            targetManaged
        )

        Dim current As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Before restore"
            }
        }

        targetRepository.Save(
            current
        )

        Dim restoreService As New PortableRestoreService(
            targetManaged
        )

        Dim restoreResult As RestoreResult =
            restoreService.RestoreBackup(
                backupPath,
                current,
                targetRepository
            )

        Assert.AreEqual(
            1,
            restoreResult.ManuscriptCount
        )

        Dim restored As List(Of Manuscript) =
            targetRepository.Load()

        Assert.AreEqual(
            1,
            restored.Count
        )

        Assert.AreEqual(
            versionId,
            restored(0).CurrentVersionId.Value
        )

        Assert.AreEqual(
            1,
            restored(0).Versions.Count
        )

        Dim restoredVersion As ManuscriptVersion =
            restored(0).Versions(0)

        Assert.IsTrue(
            restoredVersion.IsManagedCopy
        )

        Assert.IsTrue(
            File.Exists(
                restoredVersion.LocalFilePath
            )
        )

        Assert.IsTrue(
            Path.GetFullPath(
                restoredVersion.LocalFilePath
            ).StartsWith(
                Path.GetFullPath(
                    targetManaged
                ),
                StringComparison.OrdinalIgnoreCase
            )
        )

        Assert.AreEqual(
            "Revision snapshot for backup.",
            File.ReadAllText(
                restoredVersion.LocalFilePath
            )
        )

    End Sub


    <TestMethod>
    Public Sub InspectBackup_RejectsMissingManagedVersionSnapshot()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "inspect-data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "inspect-managed"
            )

        Dim workingPath As String =
            Path.Combine(
                _root,
                "inspect-version.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed version fixture."
        )

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .LocalFilePath = workingPath,
                .IsManagedCopy = True
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim manuscripts As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            manuscripts
        )

        Dim backupPath As String =
            Path.Combine(
                _root,
                "missing-version.zip"
            )

        Dim backupService As New PortableBackupService(
            managedDirectory
        )

        backupService.CreateBackup(
            backupPath,
            manuscripts,
            repository
        )

        Using archive As ZipArchive =
            ZipFile.Open(
                backupPath,
                ZipArchiveMode.Update
            )

            Dim versionEntry As ZipArchiveEntry =
                archive.Entries.FirstOrDefault(
                    Function(entry)
                        Dim normalized As String =
                            entry.FullName.Replace("\", "/")

                        Return normalized.Contains(
                            "/versions/" &
                            versionId.ToString("N") &
                            "/",
                            StringComparison.OrdinalIgnoreCase
                        )
                    End Function
                )

            Assert.IsNotNull(
                versionEntry
            )

            versionEntry.Delete()

        End Using

        Dim restoreService As New PortableRestoreService(
            managedDirectory
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As BackupInspection =
                    restoreService.InspectBackup(
                        backupPath
                    )
            End Sub
        )

    End Sub

End Class
