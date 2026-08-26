Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class ManagedLibraryService

        Private Const VersionDeletionStagingFolder As String =
            ".paperroute-version-delete"

        Private ReadOnly _rootDirectory As String


        Public Sub New()
            Me.New(GetDefaultRootDirectory())
        End Sub


        Friend Sub New(
            rootDirectory As String
        )

            If String.IsNullOrWhiteSpace(rootDirectory) Then
                Throw New ArgumentException("A managed-library root directory is required.", NameOf(rootDirectory))
            End If

            _rootDirectory =
                Path.GetFullPath(rootDirectory)

        End Sub


        Private Shared Function GetDefaultRootDirectory() As String

            Dim documentsDirectory As String =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                )

            If String.IsNullOrWhiteSpace(documentsDirectory) Then

                Throw New InvalidOperationException(
                    "The user's Documents folder could not be located."
                )

            End If

            Return StorageMigrationService.CurrentManagedLibraryRoot()

        End Function


        Public ReadOnly Property RootDirectory As String

            Get
                Return _rootDirectory
            End Get

        End Property


        Public Function IsManagedPath(
            filePath As String
        ) As Boolean

            If String.IsNullOrWhiteSpace(filePath) Then
                Return False
            End If

            Try

                Dim fullRoot As String =
                    Path.GetFullPath(_rootDirectory)

                fullRoot =
                    fullRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    ) &
                    Path.DirectorySeparatorChar

                Dim fullPath As String =
                    Path.GetFullPath(filePath)

                Return fullPath.StartsWith(
                    fullRoot,
                    StringComparison.OrdinalIgnoreCase
                )

            Catch

                Return False

            End Try

        End Function


        Public Sub CommitManagedCopies(
            manuscripts As IEnumerable(Of Manuscript)
        )

            Dim operations As New List(Of CopyOperation)()

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing Then
                    Continue For
                End If

                If manuscript.Versions IsNot Nothing Then

                    For Each version As ManuscriptVersion In manuscript.Versions

                        If version Is Nothing OrElse
                           Not version.IsManagedCopy Then
                            Continue For
                        End If

                        If String.IsNullOrWhiteSpace(version.LocalFilePath) Then
                            Continue For
                        End If

                        If IsManagedPath(version.LocalFilePath) Then
                            Continue For
                        End If

                        If version.Id = Guid.Empty Then

                            Throw New InvalidDataException(
                                "A manuscript version marked for the PaperRoute Library does not have a valid identifier."
                            )

                        End If

                        If Not File.Exists(version.LocalFilePath) Then

                            Throw New FileNotFoundException(
                                "A manuscript version marked for the PaperRoute Library could not be found.",
                                version.LocalFilePath
                            )

                        End If

                        Dim destinationDirectory As String =
                            Path.Combine(
                                _rootDirectory,
                                manuscript.Id.ToString("N"),
                                "versions",
                                version.Id.ToString("N")
                            )

                        Dim destinationPath As String =
                            CreateUniqueDestinationPath(
                                destinationDirectory,
                                version.LocalFilePath
                            )

                        operations.Add(
                            New CopyOperation(
                                version,
                                version.LocalFilePath,
                                destinationDirectory,
                                destinationPath
                            )
                        )

                    Next

                End If

                If manuscript.Submissions Is Nothing Then
                    Continue For
                End If

                For Each submission As JournalSubmission In manuscript.Submissions

                    If submission Is Nothing OrElse
                       submission.Correspondence Is Nothing Then

                        Continue For

                    End If

                    For Each item As CorrespondenceItem In submission.Correspondence

                        If item Is Nothing OrElse
                           Not item.IsManagedCopy Then
                            Continue For
                        End If

                        If String.IsNullOrWhiteSpace(item.LocalFilePath) Then
                            Continue For
                        End If

                        If IsManagedPath(item.LocalFilePath) Then
                            Continue For
                        End If

                        If Not File.Exists(item.LocalFilePath) Then

                            Throw New FileNotFoundException(
                                "A file marked for the PaperRoute Library could not be found.",
                                item.LocalFilePath
                            )

                        End If

                        Dim destinationDirectory As String =
                            Path.Combine(
                                _rootDirectory,
                                manuscript.Id.ToString("N"),
                                submission.Id.ToString("N"),
                                item.Id.ToString("N")
                            )

                        Dim destinationPath As String =
                            CreateUniqueDestinationPath(
                                destinationDirectory,
                                item.LocalFilePath
                            )

                        operations.Add(
                            New CopyOperation(
                                item,
                                item.LocalFilePath,
                                destinationDirectory,
                                destinationPath
                            )
                        )

                    Next

                Next

            Next

            If operations.Count = 0 Then
                Return
            End If

            Dim createdFiles As New List(Of String)()

            Try

                For Each operation As CopyOperation In operations

                    Directory.CreateDirectory(
                        operation.DestinationDirectory
                    )

                    File.Copy(
                        operation.SourcePath,
                        operation.DestinationPath,
                        False
                    )

                    createdFiles.Add(
                        operation.DestinationPath
                    )

                Next

            Catch

                For Each createdFile As String In createdFiles

                    Try

                        If File.Exists(createdFile) Then
                            File.Delete(createdFile)
                        End If

                    Catch
                        ' Best-effort rollback only.
                    End Try

                Next

                Throw

            End Try

            For Each operation As CopyOperation In operations
                operation.CommitReference()
            Next

        End Sub


        Friend Function BeginVersionDeletionTransaction(
            manuscripts As IEnumerable(Of Manuscript)
        ) As ManagedVersionDeletionTransaction

            Dim transaction As New ManagedVersionDeletionTransaction(
                _rootDirectory,
                VersionDeletionStagingFolder
            )

            Try

                transaction.StageOrphanedVersionDirectories(
                    manuscripts
                )

                Return transaction

            Catch

                Try
                    transaction.Rollback()
                Catch
                    ' Preserve the original staging failure. Startup recovery
                    ' can reconcile any staging directory that could not be
                    ' restored immediately.
                End Try

                Throw

            End Try

        End Function


        Friend Overridable Sub RecoverStagedVersionDeletions(
            manuscripts As IEnumerable(Of Manuscript)
        )

            Dim stagingRoot As String =
                Path.Combine(
                    _rootDirectory,
                    VersionDeletionStagingFolder
                )

            If Not Directory.Exists(stagingRoot) Then
                Return
            End If

            Dim referenced As Dictionary(Of Guid, HashSet(Of Guid)) =
                BuildReferencedVersionMap(
                    manuscripts
                )

            For Each transactionDirectory As String In
                Directory.GetDirectories(
                    stagingRoot
                )

                RecoverTransactionDirectory(
                    transactionDirectory,
                    referenced
                )

            Next

            DeleteDirectoryIfEmpty(
                stagingRoot
            )

        End Sub


        Private Sub RecoverTransactionDirectory(
            transactionDirectory As String,
            referenced As Dictionary(Of Guid, HashSet(Of Guid))
        )

            For Each manuscriptDirectory As String In
                Directory.GetDirectories(
                    transactionDirectory
                )

                Dim manuscriptId As Guid

                If Not Guid.TryParseExact(
                    Path.GetFileName(
                        manuscriptDirectory
                    ),
                    "N",
                    manuscriptId
                ) Then

                    Continue For

                End If

                For Each versionDirectory As String In
                    Directory.GetDirectories(
                        manuscriptDirectory
                    )

                    Dim versionId As Guid

                    If Not Guid.TryParseExact(
                        Path.GetFileName(
                            versionDirectory
                        ),
                        "N",
                        versionId
                    ) Then

                        Continue For

                    End If

                    Dim isReferenced As Boolean =
                        referenced.ContainsKey(
                            manuscriptId
                        ) AndAlso
                        referenced(manuscriptId).Contains(
                            versionId
                        )

                    If isReferenced Then

                        Dim originalDirectory As String =
                            GetVersionDirectoryPath(
                                manuscriptId,
                                versionId
                            )

                        If Directory.Exists(
                            originalDirectory
                        ) Then

                            Directory.Delete(
                                versionDirectory,
                                True
                            )

                        Else

                            Directory.CreateDirectory(
                                Path.GetDirectoryName(
                                    originalDirectory
                                )
                            )

                            Directory.Move(
                                versionDirectory,
                                originalDirectory
                            )

                        End If

                    Else

                        Directory.Delete(
                            versionDirectory,
                            True
                        )

                    End If

                Next

                DeleteDirectoryIfEmpty(
                    manuscriptDirectory
                )

            Next

            DeleteDirectoryIfEmpty(
                transactionDirectory
            )

        End Sub


        Private Shared Function BuildReferencedVersionMap(
            manuscripts As IEnumerable(Of Manuscript)
        ) As Dictionary(Of Guid, HashSet(Of Guid))

            Dim result As New Dictionary(Of Guid, HashSet(Of Guid))()

            If manuscripts Is Nothing Then
                Return result
            End If

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing OrElse
                   manuscript.Id = Guid.Empty Then

                    Continue For

                End If

                Dim ids As New HashSet(Of Guid)()

                If manuscript.Versions IsNot Nothing Then

                    For Each version As ManuscriptVersion In manuscript.Versions

                        If version IsNot Nothing AndAlso
                           version.Id <> Guid.Empty Then

                            ids.Add(
                                version.Id
                            )

                        End If

                    Next

                End If

                result(manuscript.Id) =
                    ids

            Next

            Return result

        End Function


        Private Function GetVersionDirectoryPath(
            manuscriptId As Guid,
            versionId As Guid
        ) As String

            Return Path.Combine(
                _rootDirectory,
                manuscriptId.ToString("N"),
                "versions",
                versionId.ToString("N")
            )

        End Function


        Private Shared Sub DeleteDirectoryIfEmpty(
            directoryPath As String
        )

            If Not Directory.Exists(
                directoryPath
            ) Then

                Return

            End If

            If Directory.EnumerateFileSystemEntries(
                directoryPath
            ).Any() Then

                Return

            End If

            Directory.Delete(
                directoryPath,
                False
            )

        End Sub


        Private Function CreateUniqueDestinationPath(
            destinationDirectory As String,
            sourcePath As String
        ) As String

            Dim sourceName As String =
                Path.GetFileNameWithoutExtension(
                    sourcePath
                )

            Dim extension As String =
                Path.GetExtension(
                    sourcePath
                )

            If String.IsNullOrWhiteSpace(sourceName) Then
                sourceName = "document"
            End If

            Dim uniqueSuffix As String =
                Guid.NewGuid().ToString("N").Substring(0, 8)

            Dim destinationFileName As String =
                sourceName &
                "_" &
                uniqueSuffix &
                extension

            Return Path.Combine(
                destinationDirectory,
                destinationFileName
            )

        End Function


        Friend NotInheritable Class ManagedVersionDeletionTransaction
            Implements IDisposable

            Private ReadOnly _rootDirectory As String
            Private ReadOnly _stagingRoot As String
            Private ReadOnly _operations As New List(Of MoveOperation)()

            Private _completed As Boolean = False


            Public Sub New(
                rootDirectory As String,
                stagingFolderName As String
            )

                _rootDirectory =
                    rootDirectory

                _stagingRoot =
                    Path.Combine(
                        rootDirectory,
                        stagingFolderName,
                        Guid.NewGuid().ToString("N")
                    )

            End Sub


            Friend ReadOnly Property StagedDirectoryCount As Integer
                Get
                    Return _operations.Count
                End Get
            End Property


            Friend Sub StageOrphanedVersionDirectories(
                manuscripts As IEnumerable(Of Manuscript)
            )

                Dim referenced As Dictionary(Of Guid, HashSet(Of Guid)) =
                    ManagedLibraryService.BuildReferencedVersionMap(
                        manuscripts
                    )

                For Each pair As KeyValuePair(Of Guid, HashSet(Of Guid)) In
                    referenced

                    Dim versionsDirectory As String =
                        Path.Combine(
                            _rootDirectory,
                            pair.Key.ToString("N"),
                            "versions"
                        )

                    If Not Directory.Exists(
                        versionsDirectory
                    ) Then

                        Continue For

                    End If

                    For Each versionDirectory As String In
                        Directory.GetDirectories(
                            versionsDirectory
                        )

                        Dim versionId As Guid

                        If Not Guid.TryParseExact(
                            Path.GetFileName(
                                versionDirectory
                            ),
                            "N",
                            versionId
                        ) Then

                            Continue For

                        End If

                        If pair.Value.Contains(
                            versionId
                        ) Then

                            Continue For

                        End If

                        Dim stagedDirectory As String =
                            Path.Combine(
                                _stagingRoot,
                                pair.Key.ToString("N"),
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

                        _operations.Add(
                            New MoveOperation(
                                versionDirectory,
                                stagedDirectory
                            )
                        )

                    Next

                Next

            End Sub


            Friend Sub Commit()

                If _completed Then
                    Return
                End If

                If Directory.Exists(
                    _stagingRoot
                ) Then

                    Try

                        Directory.Delete(
                            _stagingRoot,
                            True
                        )

                    Catch
                        ' The authoritative metadata is already committed.
                        ' Leave staging in place; startup recovery will safely
                        ' discard unreferenced staged directories later.
                    End Try

                End If

                _completed =
                    True

            End Sub


            Friend Sub Rollback()

                If _completed Then
                    Return
                End If

                For index As Integer =
                    _operations.Count - 1 To 0 Step -1

                    Dim operation As MoveOperation =
                        _operations(index)

                    If Not Directory.Exists(
                        operation.StagedDirectory
                    ) Then

                        Continue For

                    End If

                    If Directory.Exists(
                        operation.OriginalDirectory
                    ) Then

                        Throw New IOException(
                            "PaperRoute could not restore a staged manuscript-version snapshot because the original directory already exists: " &
                            operation.OriginalDirectory
                        )

                    End If

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            operation.OriginalDirectory
                        )
                    )

                    Directory.Move(
                        operation.StagedDirectory,
                        operation.OriginalDirectory
                    )

                Next

                If Directory.Exists(
                    _stagingRoot
                ) Then

                    Try
                        Directory.Delete(
                            _stagingRoot,
                            True
                        )
                    Catch
                    End Try

                End If

                _completed =
                    True

            End Sub


            Public Sub Dispose() Implements IDisposable.Dispose

                If Not _completed Then
                    Rollback()
                End If

            End Sub


            Private NotInheritable Class MoveOperation

                Public ReadOnly Property OriginalDirectory As String
                Public ReadOnly Property StagedDirectory As String


                Public Sub New(
                    originalDirectory As String,
                    stagedDirectory As String
                )

                    Me.OriginalDirectory =
                        originalDirectory

                    Me.StagedDirectory =
                        stagedDirectory

                End Sub

            End Class

        End Class


        Private Class CopyOperation

            Private ReadOnly _correspondenceItem As CorrespondenceItem
            Private ReadOnly _version As ManuscriptVersion

            Public ReadOnly Property SourcePath As String
            Public ReadOnly Property DestinationDirectory As String
            Public ReadOnly Property DestinationPath As String


            Public Sub New(
                item As CorrespondenceItem,
                sourcePath As String,
                destinationDirectory As String,
                destinationPath As String
            )

                Me._correspondenceItem = item
                Me.SourcePath = sourcePath
                Me.DestinationDirectory = destinationDirectory
                Me.DestinationPath = destinationPath

            End Sub


            Public Sub New(
                version As ManuscriptVersion,
                sourcePath As String,
                destinationDirectory As String,
                destinationPath As String
            )

                Me._version = version
                Me.SourcePath = sourcePath
                Me.DestinationDirectory = destinationDirectory
                Me.DestinationPath = destinationPath

            End Sub


            Public Sub CommitReference()

                If _correspondenceItem IsNot Nothing Then

                    _correspondenceItem.LocalFilePath =
                        DestinationPath

                    _correspondenceItem.IsManagedCopy =
                        True

                    Return

                End If

                If _version IsNot Nothing Then

                    _version.LocalFilePath =
                        DestinationPath

                    _version.IsManagedCopy =
                        True

                    Return

                End If

                Throw New InvalidOperationException(
                    "A managed-library copy operation does not have a target record."
                )

            End Sub

        End Class

    End Class

End Namespace
