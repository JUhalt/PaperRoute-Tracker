Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class ManagedLibraryService

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

                For Each submission As JournalSubmission In manuscript.Submissions

                    For Each item As CorrespondenceItem In submission.Correspondence

                        If Not item.IsManagedCopy Then
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
