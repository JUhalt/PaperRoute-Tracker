Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class PortableBackupService

        Private ReadOnly _managedLibrary As ManagedLibraryService


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

                    ' Files PaperRoute no longer needs wait in the library's
                    ' removed folder; a backup leaves them out.
                    CopyDirectory(
                        _managedLibrary.RootDirectory,
                        filesDestination,
                        excludedSubdirectory:=ManagedLibraryService.RemovedFolderName
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

                If File.Exists(destinationZipPath) Then
                    File.Delete(destinationZipPath)
                End If

                ZipFile.CreateFromDirectory(
                    stagingDirectory,
                    destinationZipPath,
                    CompressionLevel.Optimal,
                    False
                )

            Finally

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


        Private Sub CopyDirectory(
            sourceDirectory As String,
            destinationDirectory As String,
            Optional excludedSubdirectory As String = Nothing
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

                If excludedSubdirectory IsNot Nothing AndAlso
                   String.Equals(
                       Path.GetFileName(sourceSubdirectory),
                       excludedSubdirectory,
                       StringComparison.OrdinalIgnoreCase
                   ) Then

                    Continue For

                End If

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