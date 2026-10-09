Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class PortableBackupService

        Private ReadOnly _managedLibrary As ManagedLibraryService


        ' Restore's safety limits, so a library too large to take back is
        ' reported now rather than when the backup is needed (#114).
        ' Settable only so a test need not write twenty thousand files.
        Friend Property MaximumArchiveEntries As Integer =
            PortableRestoreService.MaximumArchiveEntries

        Friend Property MaximumUncompressedBytes As Long =
            PortableRestoreService.MaximumUncompressedBytes


        Public Sub New()
            _managedLibrary = New ManagedLibraryService()
        End Sub


        Friend Sub New(
            managedLibraryRootDirectory As String
        )

            _managedLibrary =
                New ManagedLibraryService(
                    managedLibraryRootDirectory
                )

        End Sub


        Public Sub CreateBackup(
            destinationZipPath As String,
            manuscripts As List(Of Manuscript),
            repository As ManuscriptRepository
        )

            If String.IsNullOrWhiteSpace(destinationZipPath) Then
                Throw New ArgumentException("A backup destination is required.")
            End If

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If repository Is Nothing Then
                Throw New ArgumentNullException(NameOf(repository))
            End If

            If Not File.Exists(repository.DataFilePath) Then
                Throw New FileNotFoundException(
                    "The PaperRoute data file could not be found.",
                    repository.DataFilePath
                )
            End If

            Dim stagingDirectory As String =
                Path.Combine(
                    Path.GetTempPath(),
                    "PaperRouteBackup_" & Guid.NewGuid().ToString("N")
                )

            ' The ZIP is written here, beside the destination, and takes
            ' the destination's place only once it is finished and proved.
            Dim partialZipPath As String =
                destinationZipPath & ".partial"

            Try

                Directory.CreateDirectory(stagingDirectory)

                ' =============================================
                ' Native PaperRoute data
                ' =============================================

                Dim jsonDestination As String =
                    Path.Combine(
                        stagingDirectory,
                        "manuscripts.json"
                    )

                File.Copy(
                    repository.DataFilePath,
                    jsonDestination,
                    True
                )

                Dim authorLibrarySource As String =
                    Path.Combine(
                        Path.GetDirectoryName(
                            repository.DataFilePath
                        ),
                        "authors.json"
                    )

                If File.Exists(
                    authorLibrarySource
                ) Then

                    File.Copy(
                        authorLibrarySource,
                        Path.Combine(
                            stagingDirectory,
                            "authors.json"
                        ),
                        True
                    )

                End If

                ' Saved citation figures (#91), when present: read and written
                ' again, so a damaged file (or its readable citations.bak)
                ' never makes the backup unrestorable. Unreadable, it is left out.
                Dim savedCitations As CitationSnapshot = Nothing

                Try

                    savedCitations =
                        New CitationStore(
                            Path.GetDirectoryName(
                                repository.DataFilePath
                            )
                        ).Load()

                Catch ex As InvalidDataException

                    savedCitations = Nothing

                End Try

                If savedCitations IsNot Nothing Then

                    Dim stagedCitations As New CitationStore(
                        stagingDirectory
                    )

                    stagedCitations.Save(
                        savedCitations
                    )

                End If

                ' =============================================
                ' Human-readable Excel export
                ' =============================================

                Dim excelDestination As String =
                    Path.Combine(
                        stagingDirectory,
                        "library.xlsx"
                    )

                Dim excelExporter As New LibraryExcelExporter()

                excelExporter.Export(
                    excelDestination,
                    manuscripts
                )

                ' =============================================
                ' Managed document library
                ' =============================================

                If Directory.Exists(_managedLibrary.RootDirectory) Then

                    Dim filesDestination As String =
                        Path.Combine(
                            stagingDirectory,
                            "files"
                        )

                    CopyDirectory(
                        _managedLibrary.RootDirectory,
                        filesDestination
                    )

                End If

                ' =============================================
                ' Backup information
                ' =============================================

                Dim submissionCount As Integer = 0
                Dim decisionCount As Integer = 0
                Dim correspondenceCount As Integer = 0

                For Each manuscript As Manuscript In manuscripts

                    submissionCount += manuscript.Submissions.Count

                    For Each submission As JournalSubmission In manuscript.Submissions

                        decisionCount += submission.Decisions.Count
                        correspondenceCount += submission.Correspondence.Count

                    Next

                Next

                Dim backupInfo As String =
                    "PaperRoute Portable Backup" &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Created: " &
                    DateTime.Now.ToString("O") &
                    Environment.NewLine &
                    "Manuscripts: " &
                    manuscripts.Count.ToString() &
                    Environment.NewLine &
                    "Submissions: " &
                    submissionCount.ToString() &
                    Environment.NewLine &
                    "Editorial decisions: " &
                    decisionCount.ToString() &
                    Environment.NewLine &
                    "Correspondence records: " &
                    correspondenceCount.ToString() &
                    Environment.NewLine &
                    Environment.NewLine &
                    "manuscripts.json is the native PaperRoute manuscript data file." &
                    Environment.NewLine &
                    "authors.json contains reusable authors, affiliations, and journals when present." &
                    Environment.NewLine &
                    "citations.json contains your saved citation figures when present." &
                    Environment.NewLine &
                    "library.xlsx is a human-readable export of the manuscript library." &
                    Environment.NewLine &
                    "files contains documents managed by PaperRoute." &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Externally linked files are referenced by path but are not copied into this backup."

                File.WriteAllText(
                    Path.Combine(
                        stagingDirectory,
                        "backup-info.txt"
                    ),
                    backupInfo
                )

                ' =============================================
                ' ZIP
                ' =============================================

                ' A full or unplugged drive, or a failed write, must never
                ' cost the previous backup (#114): write beside it, prove
                ' the archive, and only then replace it.
                If File.Exists(partialZipPath) Then
                    File.Delete(partialZipPath)
                End If

                ZipFile.CreateFromDirectory(
                    stagingDirectory,
                    partialZipPath,
                    CompressionLevel.Optimal,
                    False
                )

                ThrowIfLargerThanRestoreAccepts(
                    partialZipPath
                )

                Call New PortableRestoreService(
                    _managedLibrary.RootDirectory
                ).InspectBackup(
                    partialZipPath
                )

                StorageFile.Replace(
                    partialZipPath,
                    destinationZipPath,
                    Nothing
                )

            Finally

                If File.Exists(partialZipPath) Then

                    Try

                        File.Delete(
                            partialZipPath
                        )

                    Catch
                        ' Temporary cleanup is best-effort.
                    End Try

                End If

                If Directory.Exists(stagingDirectory) Then

                    Try

                        Directory.Delete(
                            stagingDirectory,
                            True
                        )

                    Catch
                        ' Temporary cleanup is best-effort.
                    End Try

                End If

            End Try

        End Sub


        ' Restore refuses an archive over its safety limits, so a library
        ' that size is reported here, at backup time (#114).
        Private Sub ThrowIfLargerThanRestoreAccepts(
            zipPath As String
        )

            Dim entries As Integer
            Dim uncompressedBytes As Long = 0

            Using archive As ZipArchive = ZipFile.OpenRead(zipPath)

                entries = archive.Entries.Count

                For Each entry As ZipArchiveEntry In archive.Entries
                    uncompressedBytes += entry.Length
                Next

            End Using

            If entries > MaximumArchiveEntries Then

                Throw New InvalidDataException(
                    "Restore accepts a backup of up to " &
                    MaximumArchiveEntries.ToString("N0") &
                    " files, and this library has " &
                    entries.ToString("N0") &
                    ". The backup was not written."
                )

            End If

            If uncompressedBytes > MaximumUncompressedBytes Then

                Throw New InvalidDataException(
                    "Restore accepts a backup of up to " &
                    Gigabytes(MaximumUncompressedBytes) &
                    " GB, and this library is " &
                    Gigabytes(uncompressedBytes) &
                    " GB. The backup was not written."
                )

            End If

        End Sub


        Private Shared Function Gigabytes(
            bytes As Long
        ) As String

            Return (bytes / 1073741824.0).ToString("0.#")

        End Function


        Private Sub CopyDirectory(
            sourceDirectory As String,
            destinationDirectory As String
        )

            Directory.CreateDirectory(destinationDirectory)

            For Each sourceFile As String In Directory.GetFiles(sourceDirectory)

                Dim destinationFile As String =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(sourceFile)
                    )

                File.Copy(
                    sourceFile,
                    destinationFile,
                    True
                )

            Next

            For Each sourceSubdirectory As String In Directory.GetDirectories(sourceDirectory)

                Dim destinationSubdirectory As String =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(sourceSubdirectory)
                    )

                CopyDirectory(
                    sourceSubdirectory,
                    destinationSubdirectory
                )

            Next

        End Sub

    End Class

End Namespace