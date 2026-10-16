Imports System
Imports System.IO

Namespace Services

    ' Recovery when PaperRoute cannot start (#124), with no library loaded:
    ' the previous save (manuscripts.bak) when it reads with the current
    ' rules, or a backup archive. Each leaves the schema marker current,
    ' since what it put in place was validated with the current rules, and
    ' keeps the marker it replaced as schema.previous.bak.
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


        ' True when manuscripts.bak reads with the current rules, so Use the
        ' Previous Save can be offered. Nothing is changed.
        Public Function PreviousSaveIsValid() As Boolean

            Return _repository.HasValidBackup()

        End Function


        ' Puts the previous save in the primary file's place; the file it
        ' replaces is kept under data\recovery, and the marker is rewritten.
        Public Sub UsePreviousSave()

            _repository.UseBackup()

            StorageMigrationService.WriteCurrentSchemaVersionReplacingExisting(
                _schemaPath
            )

        End Sub


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
