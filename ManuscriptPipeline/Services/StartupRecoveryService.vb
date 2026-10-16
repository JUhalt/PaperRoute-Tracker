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
    ' a newer PaperRoute rules both out: the library is not damaged, and the
    ' previous save reads here only because what this build does not know
    ' is skipped. A replaced marker is kept as schema.previous.bak.
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


        ' True when manuscripts.bak reads with the current rules, the marker
        ' is not from a newer PaperRoute, and manuscripts.json does not read,
        ' so Use the Previous Save can be offered: over a primary that reads
        ' it would only roll the library back. Nothing is changed.
        Public Function PreviousSaveIsValid() As Boolean

            If LibraryIsFromANewerPaperRoute() OrElse
               _repository.PrimaryReads() Then

                Return False

            End If

            Return _repository.HasValidBackup()

        End Function


        ' True when the marker says a newer PaperRoute wrote the library. That
        ' is not damage, and updating PaperRoute is the fix, so neither the
        ' previous save nor a backup is put over it. Nothing is changed.
        Public Function LibraryIsFromANewerPaperRoute() As Boolean

            Dim marker As Integer? =
                ReadableSchemaVersion()

            Return marker.HasValue AndAlso
                   marker.Value > StorageMigrationService.CurrentSchemaVersion

        End Function


        ' Puts the previous save in the primary file's place and returns
        ' where the file it replaced is kept under data\recovery (empty when
        ' there was none), so the window can name it. A marker from a newer
        ' PaperRoute, or a primary that reads, stops this before anything
        ' changes.
        Public Function UsePreviousSave() As String

            Dim marker As Integer? =
                ReadableSchemaVersion()

            If marker.HasValue Then

                StorageMigrationService.ValidateSupportedSchemaVersion(
                    marker.Value
                )

            End If

            If _repository.PrimaryReads() Then

                Throw New InvalidOperationException(
                    "Your library file can be read, so the previous save is not needed. Nothing has been changed."
                )

            End If

            _repository.UseBackup()

            Dim preserved As String =
                _repository.LastRecoveryPreservedFilePath

            If Not marker.HasValue Then

                WriteMarkerAfter(
                    "The previous save is in place of your library file" &
                    If(String.IsNullOrEmpty(preserved),
                       "",
                       ", and the file it replaced is kept at " & preserved)
                )

            End If

            Return preserved

        End Function


        ' Writes the current marker once the library step has changed the
        ' library. A failure here must not read as a recovery that changed
        ' nothing, so it says what is already in place.
        Private Sub WriteMarkerAfter(
            whatIsInPlace As String
        )

            Try

                StorageMigrationService.WriteCurrentSchemaVersionReplacingExisting(
                    _schemaPath
                )

            Catch ex As Exception

                Throw New InvalidOperationException(
                    whatIsInPlace &
                    ", but PaperRoute could not write its storage marker (" &
                    _schemaPath &
                    "): " &
                    ex.Message,
                    ex
                )

            End Try

        End Sub


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


        ' What the archive holds, for the window to show before it asks
        ' (#124). Nothing is changed.
        Public Function InspectBackup(
            backupZipPath As String
        ) As BackupInspection

            Return New PortableRestoreService(_managedLibraryRoot).InspectBackup(
                backupZipPath
            )

        End Function


        ' Restores the archive over the library on disk, loaded or not, and
        ' rewrites the marker it restored over. A library from a newer
        ' PaperRoute stops this before anything changes, with the message
        ' startup gave.
        Public Function RestoreFromBackup(
            backupZipPath As String
        ) As RestoreResult

            Dim marker As Integer? =
                ReadableSchemaVersion()

            If marker.HasValue AndAlso
               marker.Value > StorageMigrationService.CurrentSchemaVersion Then

                StorageMigrationService.ValidateSupportedSchemaVersion(
                    marker.Value
                )

            End If

            Dim result As RestoreResult =
                New PortableRestoreService(_managedLibraryRoot).RestoreBackup(
                    backupZipPath,
                    _repository
                )

            WriteMarkerAfter(
                "The backup was restored" &
                If(String.IsNullOrWhiteSpace(result.EmergencyBackupPath),
                   "",
                   ", and your library files from before the restore are kept at " & result.EmergencyBackupPath)
            )

            Return result

        End Function

    End Class

End Namespace
