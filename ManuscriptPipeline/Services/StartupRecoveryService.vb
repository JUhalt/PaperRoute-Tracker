Imports System
Imports System.IO

Namespace Services

    ' Recovery when PaperRoute cannot start (#124), with no library loaded:
    ' the previous save (manuscripts.bak) when it reads with the current
    ' rules, or a backup archive. The previous save leaves a readable marker
    ' as it is, so the next start upgrades or validates the recovered
    ' library from where it was; a marker that cannot be read gives way to
    ' the current one, as does the one a restore writes over, since what a
    ' restore puts in place is written with the current rules. A marker from
    ' a newer PaperRoute rules the previous save out: it reads here only
    ' because what this build does not know is skipped. A replaced marker
    ' is kept as schema.previous.bak.
    Public Class StartupRecoveryService

        Private ReadOnly _repository As ManuscriptRepository
        Private ReadOnly _managedLibraryRoot As String
        Private ReadOnly _schemaPath As String


        Public Sub New()

            Me.New(
                New ManuscriptRepository(),
                Nothing,
                StorageMigrationService.SchemaFilePath()
            )

        End Sub


        ' Tests work on a temporary library; Nothing for the managed root
        ' means the installed one.
        Friend Sub New(
            repository As ManuscriptRepository,
            managedLibraryRoot As String,
            schemaPath As String
        )

            If repository Is Nothing Then
                Throw New ArgumentNullException(NameOf(repository))
            End If

            If String.IsNullOrWhiteSpace(schemaPath) Then
                Throw New ArgumentException("A schema marker path is required.", NameOf(schemaPath))
            End If

            _repository = repository
            _managedLibraryRoot = managedLibraryRoot
            _schemaPath = schemaPath

        End Sub


        ' True when manuscripts.bak reads with the current rules and the
        ' marker is not from a newer PaperRoute, so Use the Previous Save can
        ' be offered. Nothing is changed.
        Public Function PreviousSaveIsValid() As Boolean

            Dim marker As Integer? =
                ReadableSchemaVersion()

            If marker.HasValue AndAlso
               marker.Value > StorageMigrationService.CurrentSchemaVersion Then

                Return False

            End If

            Return _repository.HasValidBackup()

        End Function


        ' Puts the previous save in the primary file's place and returns
        ' where the file it replaced is kept under data\recovery (empty when
        ' there was none), so the window can name it. A marker from a newer
        ' PaperRoute stops this before anything changes.
        Public Function UsePreviousSave() As String

            Dim marker As Integer? =
                ReadableSchemaVersion()

            If marker.HasValue Then

                StorageMigrationService.ValidateSupportedSchemaVersion(
                    marker.Value
                )

            End If

            _repository.UseBackup()

            If Not marker.HasValue Then

                StorageMigrationService.WriteCurrentSchemaVersionReplacingExisting(
                    _schemaPath
                )

            End If

            Return _repository.LastRecoveryPreservedFilePath

        End Function


        ' The marker's version when there is one that reads; Nothing when it
        ' is absent or cannot be read, so there is nothing to keep.
        Private Function ReadableSchemaVersion() As Integer?

            Try

                Dim version As Integer =
                    StorageMigrationService.ReadSchemaVersion(
                        _schemaPath
                    )

                If version = 0 Then
                    Return Nothing
                End If

                Return version

            Catch ex As InvalidDataException

                Return Nothing

            End Try

        End Function


        ' Restores the archive over the library on disk, loaded or not, and
        ' rewrites the marker it restored over.
        Public Function RestoreFromBackup(
            backupZipPath As String
        ) As RestoreResult

            Dim result As RestoreResult =
                New PortableRestoreService(_managedLibraryRoot).RestoreBackup(
                    backupZipPath,
                    _repository
                )

            StorageMigrationService.WriteCurrentSchemaVersionReplacingExisting(
                _schemaPath
            )

            Return result

        End Function

    End Class

End Namespace
