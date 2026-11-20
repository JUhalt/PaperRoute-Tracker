Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class JournalBackupTests

    <TestMethod>
    Public Sub PortableBackup_IncludesReusableJournalMetadata()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim dataDirectory As String =
                Path.Combine(
                    root,
                    "data"
                )

            Dim managedDirectory As String =
                Path.Combine(
                    root,
                    "managed"
                )

            Directory.CreateDirectory(
                dataDirectory
            )

            Directory.CreateDirectory(
                managedDirectory
            )

            Dim repository As New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            )

            Dim manuscripts As New List(Of Manuscript) From {
                New Manuscript With {
                    .Title = "Backup Journal Test"
                }
            }

            repository.Save(
                manuscripts
            )

            Dim reusable As New AuthorLibraryData()

            reusable.Journals.Add(
                New JournalRecord With {
                    .Name = "Journal Stored in Backup",
                    .SubmissionPortalUrl = "https://submit.example.org/"
                }
            )

            Dim reusableRepository As New AuthorLibraryRepository(
                dataDirectory
            )

            reusableRepository.Save(
                reusable
            )

            Dim backupPath As String =
                Path.Combine(
                    root,
                    "backup.zip"
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
                ZipFile.OpenRead(
                    backupPath
                )

                Dim entry As ZipArchiveEntry =
                    archive.GetEntry(
                        "authors.json"
                    )

                Assert.IsNotNull(
                    entry
                )

                Using reader As New StreamReader(
                    entry.Open()
                )

                    Dim json As String =
                        reader.ReadToEnd()

                    StringAssert.Contains(
                        json,
                        "Journal Stored in Backup"
                    )

                End Using

            End Using

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub PortableRestore_RestoresReusableJournalMetadata()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim sourceData As String =
                Path.Combine(
                    root,
                    "source-data"
                )

            Dim sourceManaged As String =
                Path.Combine(
                    root,
                    "source-managed"
                )

            Directory.CreateDirectory(sourceData)
            Directory.CreateDirectory(sourceManaged)

            Dim sourceRepository As New ManuscriptRepository(
                sourceData,
                sourceManaged
            )

            Dim sourceManuscripts As New List(Of Manuscript) From {
                New Manuscript With {
                    .Title = "Source"
                }
            }

            sourceRepository.Save(
                sourceManuscripts
            )

            Dim sourceReusable As New AuthorLibraryData()

            sourceReusable.Journals.Add(
                New JournalRecord With {
                    .Name = "Restored Journal",
                    .HomepageUrl = "https://example.org/restored"
                }
            )

            Dim sourceReusableRepository As New AuthorLibraryRepository(
                sourceData
            )

            sourceReusableRepository.Save(
                sourceReusable
            )

            Dim backupPath As String =
                Path.Combine(
                    root,
                    "restore-source.zip"
                )

            Dim sourceBackupService As New PortableBackupService(
                sourceManaged
            )

            sourceBackupService.CreateBackup(
                backupPath,
                sourceManuscripts,
                sourceRepository
            )

            Dim destinationData As String =
                Path.Combine(
                    root,
                    "destination-data"
                )

            Dim destinationManaged As String =
                Path.Combine(
                    root,
                    "destination-managed"
                )

            Directory.CreateDirectory(destinationData)
            Directory.CreateDirectory(destinationManaged)

            Dim destinationRepository As New ManuscriptRepository(
                destinationData,
                destinationManaged
            )

            Dim currentManuscripts As New List(Of Manuscript) From {
                New Manuscript With {
                    .Title = "Current"
                }
            }

            destinationRepository.Save(
                currentManuscripts
            )

            Dim destinationReusableRepository As New AuthorLibraryRepository(
                destinationData
            )

            destinationReusableRepository.Save(
                New AuthorLibraryData()
            )

            Dim restoreService As New PortableRestoreService(
                destinationManaged
            )

            restoreService.RestoreBackup(
                backupPath,
                currentManuscripts,
                destinationRepository
            )

            Dim restoredRepository As New AuthorLibraryRepository(
                destinationData
            )

            Dim restored As AuthorLibraryData =
                restoredRepository.Load()

            Assert.AreEqual(
                1,
                restored.Journals.Count
            )

            Assert.AreEqual(
                "Restored Journal",
                restored.Journals(0).Name
            )

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    Private Shared Function CreateTempDirectory() As String

        Dim tempPath As String =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteJournalBackupTests_" &
                Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(
            tempPath
        )

        Return tempPath

    End Function


    Private Shared Sub DeleteTempDirectory(
        path As String
    )

        If String.IsNullOrWhiteSpace(path) OrElse
           Not Directory.Exists(path) Then

            Return

        End If

        Try

            Directory.Delete(
                path,
                True
            )

        Catch
        End Try

    End Sub

End Class
