Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class ManuscriptRepository

        Private ReadOnly _dataDirectory As String
        Private ReadOnly _dataFilePath As String
        Private ReadOnly _backupFilePath As String
        Private ReadOnly _jsonOptions As JsonSerializerOptions
        Private ReadOnly _managedLibrary As ManagedLibraryService
        Private ReadOnly _openFile As Func(Of String, Stream)

        Private _lastLoadRecoveredFromBackup As Boolean
        Private _lastRecoveryPreservedFilePath As String =
            String.Empty
        Private _lastManagedLibraryRecoveryWarning As String =
            String.Empty
        Private _lastRecoveryKeptStagingNotice As String =
            String.Empty
        Private _lastSaveWarning As String =
            String.Empty

        ' The managed folders this library referenced when it was last
        ' loaded or saved. A save stages only a folder in this baseline that
        ' the library no longer references, so folders another computer's
        ' library, a set-aside newer file, or a library never loaded from
        ' disk may still need are left alone. Nothing until a load or save,
        ' and then a save stages nothing.
        Private _baselineVersions As Dictionary(Of Guid, HashSet(Of Guid)) =
            Nothing
        Private _baselinePacketFiles As Dictionary(Of Guid, Dictionary(Of Guid, HashSet(Of Guid))) =
            Nothing
        Private _baselineCorrespondence As Dictionary(Of Guid, HashSet(Of (SubmissionId As Guid, ItemId As Guid))) =
            Nothing


        Public Sub New()
            Me.New(
                GetDefaultDataDirectory(),
                Nothing
            )
        End Sub


        ' openFile stands in for the file system in tests (#111).
        Friend Sub New(
            dataDirectory As String,
            managedLibraryRoot As String,
            Optional managedLibraryOverride As ManagedLibraryService = Nothing,
            Optional openFile As Func(Of String, Stream) = Nothing
        )

            If String.IsNullOrWhiteSpace(
                dataDirectory
            ) Then

                Throw New ArgumentException(
                    "A data directory is required.",
                    NameOf(dataDirectory)
                )

            End If

            If openFile IsNot Nothing Then

                _openFile =
                    openFile

            Else

                _openFile =
                    AddressOf StorageFile.OpenRead

            End If

            _dataDirectory =
                Path.GetFullPath(
                    dataDirectory
                )

            _dataFilePath =
                Path.Combine(
                    _dataDirectory,
                    "manuscripts.json"
                )

            _backupFilePath =
                Path.Combine(
                    _dataDirectory,
                    "manuscripts.bak"
                )

            If managedLibraryOverride IsNot Nothing Then

                _managedLibrary =
                    managedLibraryOverride

            ElseIf String.IsNullOrWhiteSpace(
                managedLibraryRoot
            ) Then

                _managedLibrary =
                    New ManagedLibraryService()

            Else

                _managedLibrary =
                    New ManagedLibraryService(
                        managedLibraryRoot
                    )

            End If

            _jsonOptions =
                New JsonSerializerOptions With {
                    .WriteIndented = True,
                    .IgnoreReadOnlyProperties = True,
                    .PropertyNameCaseInsensitive = True
                }

            _jsonOptions.Converters.Add(
                New JsonStringEnumConverter()
            )

        End Sub


        Private Shared Function GetDefaultDataDirectory() As String

            Return Path.Combine(
                StorageMigrationService.CurrentDataRoot(),
                "data"
            )

        End Function


        ' =====================================================
        ' Paths / recovery state
        ' =====================================================

        Public ReadOnly Property DataFilePath As String
            Get
                Return _dataFilePath
            End Get
        End Property


        Public ReadOnly Property BackupFilePath As String
            Get
                Return _backupFilePath
            End Get
        End Property


        Public ReadOnly Property LastLoadRecoveredFromBackup As Boolean
            Get
                Return _lastLoadRecoveredFromBackup
            End Get
        End Property


        Public ReadOnly Property LastRecoveryPreservedFilePath As String
            Get
                Return _lastRecoveryPreservedFilePath
            End Get
        End Property


        Public ReadOnly Property LastManagedLibraryRecoveryWarning As String
            Get
                Return _lastManagedLibraryRecoveryWarning
            End Get
        End Property


        ' Set by a load from the safety backup that kept staged folders the
        ' backup does not reference, in case the set-aside primary file
        ' needs them; empty otherwise. Nothing failed and nothing is missing,
        ' so this is a notice, apart from the recovery warning.
        Public ReadOnly Property LastRecoveryKeptStagingNotice As String
            Get
                Return _lastRecoveryKeptStagingNotice
            End Get
        End Property


        ' Set by a save that went through but left a managed folder in
        ' place because a file in it was in use; empty otherwise.
        Public ReadOnly Property LastSaveWarning As String
            Get
                Return _lastSaveWarning
            End Get
        End Property


        ' =====================================================
        ' Load
        ' =====================================================

        Public Function Load() As List(Of Manuscript)

            Directory.CreateDirectory(
                _dataDirectory
            )

            ResetRecoveryState()

            Dim primaryExists As Boolean =
                File.Exists(
                    _dataFilePath
                )

            Dim backupExists As Boolean =
                File.Exists(
                    _backupFilePath
                )

            ' A genuinely new PaperRoute installation has neither
            ' a primary data file nor a backup.
            If Not primaryExists AndAlso
               Not backupExists Then

                Return New List(Of Manuscript)()

            End If

            ' Normal case: prefer the primary database.
            If primaryExists Then

                Dim primaryFailure As Exception =
                    Nothing

                Dim primary As List(Of Manuscript) =
                    TryLoadLibraryFile(
                        _dataFilePath,
                        primaryFailure
                    )

                If primary IsNot Nothing Then

                    TryRecoverManagedLibraryStaging(
                        primary
                    )

                    RecordBaseline(
                        primary
                    )

                    Return primary

                End If

                ' The primary exists but is invalid. If a backup
                ' exists, validate it before touching either file.
                If backupExists Then

                    Dim backupFailure As Exception =
                        Nothing

                    Dim backup As List(Of Manuscript) =
                        TryLoadLibraryFile(
                            _backupFilePath,
                            backupFailure
                        )

                    If backup IsNot Nothing Then

                        RecoverPrimaryFromBackup(
                            preserveExistingPrimary:=True
                        )

                        TryRecoverManagedLibraryStaging(
                            backup,
                            keepUnreferenced:=True
                        )

                        RecordBaseline(
                            backup
                        )

                        _lastLoadRecoveredFromBackup =
                            True

                        Return backup

                    End If

                    Throw CreateUnrecoverableLoadException(
                        primaryFailure,
                        backupFailure
                    )

                End If

                Throw New InvalidDataException(
                    "PaperRoute could not read the manuscript data file, " &
                    "and no safety backup is available. The existing file " &
                    "has not been overwritten.",
                    primaryFailure
                )

            End If

            ' If the primary is unexpectedly missing but a backup
            ' remains, validate and restore the backup.
            Dim missingPrimaryBackupFailure As Exception =
                Nothing

            Dim recovered As List(Of Manuscript) =
                TryLoadLibraryFile(
                    _backupFilePath,
                    missingPrimaryBackupFailure
                )

            If recovered Is Nothing Then

                Throw New InvalidDataException(
                    "PaperRoute's primary manuscript data file is missing, " &
                    "and the available safety backup could not be read. " &
                    "The backup has not been overwritten.",
                    missingPrimaryBackupFailure
                )

            End If

            RecoverPrimaryFromBackup(
                preserveExistingPrimary:=False
            )

            TryRecoverManagedLibraryStaging(
                recovered,
                keepUnreferenced:=True
            )

            RecordBaseline(
                recovered
            )

            _lastLoadRecoveredFromBackup =
                True

            Return recovered

        End Function


        Private Function TryLoadLibraryFile(
            filePath As String,
            ByRef failure As Exception
        ) As List(Of Manuscript)

            failure =
                Nothing

            Try

                Using stream As Stream =
                    StorageFile.OpenReadWithRetry(
                        filePath,
                        _openFile
                    )

                    If stream.Length = 0 Then

                        Throw New InvalidDataException(
                            "The manuscript data file is empty."
                        )

                    End If

                    Dim loaded As List(Of Manuscript) =
                        JsonSerializer.Deserialize(
                            Of List(Of Manuscript)
                        )(
                            stream,
                            _jsonOptions
                        )

                    If loaded Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript data file does not contain a valid PaperRoute library."
                        )

                    End If

                    ' Validate references and records before a backup is
                    ' eligible to replace the primary database. Syntactically
                    ' valid JSON can still contain an unusable library.
                    NormalizeLoadedData(loaded)

                    Return loaded

                End Using

            Catch ex As StorageFileInUseException

                ' Not damage: stop here, before anything turns to the backup.
                Throw

            Catch ex As Exception When StorageFile.IsInUse(ex)

                ' The same error raised while reading, after the open
                ' succeeded: another program holds part of the file.
                Throw New StorageFileInUseException(
                    filePath,
                    ex
                )

            Catch ex As Exception

                failure =
                    ex

                Return Nothing

            End Try

        End Function


        Private Sub RecoverPrimaryFromBackup(
            preserveExistingPrimary As Boolean
        )

            Dim recoveryTempPath As String =
                Path.Combine(
                    _dataDirectory,
                    "manuscripts.recovery.tmp"
                )

            Try

                If File.Exists(
                    recoveryTempPath
                ) Then

                    File.Delete(
                        recoveryTempPath
                    )

                End If

                File.Copy(
                    _backupFilePath,
                    recoveryTempPath,
                    False
                )

                If preserveExistingPrimary AndAlso
                   File.Exists(_dataFilePath) Then

                    Dim recoveryDirectory As String =
                        Path.Combine(
                            _dataDirectory,
                            "recovery"
                        )

                    Directory.CreateDirectory(
                        recoveryDirectory
                    )

                    Dim preservedPath As String =
                        CreateUniqueRecoveryPath(
                            recoveryDirectory
                        )

                    File.Replace(
                        recoveryTempPath,
                        _dataFilePath,
                        preservedPath,
                        True
                    )

                    _lastRecoveryPreservedFilePath =
                        preservedPath

                Else

                    If File.Exists(
                        _dataFilePath
                    ) Then

                        File.Delete(
                            _dataFilePath
                        )

                    End If

                    File.Move(
                        recoveryTempPath,
                        _dataFilePath
                    )

                End If

            Catch ex As Exception

                Throw New InvalidDataException(
                    "PaperRoute found a valid safety backup, but could not " &
                    "restore it to the primary data location. Existing data " &
                    "files have been left in place wherever possible.",
                    ex
                )

            Finally

                If File.Exists(
                    recoveryTempPath
                ) Then

                    Try

                        File.Delete(
                            recoveryTempPath
                        )

                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

            End Try

        End Sub


        Private Function CreateUniqueRecoveryPath(
            recoveryDirectory As String
        ) As String

            Dim baseName As String =
                "manuscripts_corrupt_" &
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss_fff"
                )

            Dim candidate As String =
                Path.Combine(
                    recoveryDirectory,
                    baseName & ".json"
                )

            Dim suffix As Integer =
                1

            While File.Exists(
                candidate
            )

                candidate =
                    Path.Combine(
                        recoveryDirectory,
                        baseName &
                        "_" &
                        suffix.ToString() &
                        ".json"
                    )

                suffix += 1

            End While

            Return candidate

        End Function


        Private Function CreateUnrecoverableLoadException(
            primaryFailure As Exception,
            backupFailure As Exception
        ) As InvalidDataException

            Dim message As String =
                "PaperRoute could not safely load either the primary " &
                "manuscript data file or its safety backup. Neither file " &
                "has been overwritten."

            Dim detail As String =
                String.Empty

            If primaryFailure IsNot Nothing Then

                detail &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "Primary data error: " &
                    primaryFailure.Message

            End If

            If backupFailure IsNot Nothing Then

                detail &=
                    Environment.NewLine &
                    "Backup data error: " &
                    backupFailure.Message

            End If

            Return New InvalidDataException(
                message & detail,
                primaryFailure
            )

        End Function


        ' A library recovered from its safety backup can be older than the
        ' set-aside primary file, so its recovery keeps the staged folders it
        ' does not reference and reports them instead of discarding them.
        Private Sub TryRecoverManagedLibraryStaging(
            manuscripts As IEnumerable(Of Manuscript),
            Optional keepUnreferenced As Boolean = False
        )

            Dim failures As New List(Of String)()
            Dim kept As New List(Of String)()

            Try

                If keepUnreferenced Then

                    kept.AddRange(
                        _managedLibrary.RecoverStagedVersionDeletions(
                            manuscripts,
                            keepUnreferenced:=True
                        )
                    )

                Else

                    _managedLibrary.RecoverStagedVersionDeletions(
                        manuscripts
                    )

                End If

            Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is IOException

                failures.Add("Version History: " & ex.Message)

            End Try

            ' Recovery of one staging area must not prevent an independent
            ' packet snapshot from being restored to its recorded location.
            Try

                Dim packetDeletionService As New ManagedPacketDeletionService(
                    _managedLibrary.RootDirectory
                )

                If keepUnreferenced Then

                    kept.AddRange(
                        packetDeletionService.RecoverStagedDeletions(
                            manuscripts,
                            keepUnreferenced:=True
                        )
                    )

                Else

                    packetDeletionService.RecoverStagedDeletions(
                        manuscripts
                    )

                End If

            Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is IOException

                failures.Add("Submission Packets: " & ex.Message)

            End Try

            If kept.Count > 0 Then
                _lastRecoveryKeptStagingNotice = BuildKeptStagingNotice(
                    kept
                )
            End If

            If failures.Count > 0 Then
                _lastManagedLibraryRecoveryWarning = BuildManagedLibraryRecoveryWarning(
                    String.Join(Environment.NewLine, failures)
                )
            End If

        End Sub


        Private Function BuildManagedLibraryRecoveryWarning(
            technicalDetails As String
        ) As String

            Return (
                "PaperRoute loaded the manuscript database, but could not finish recovery or cleanup of its internal managed-file staging area." &
                Environment.NewLine &
                Environment.NewLine &
                "Managed library: " &
                _managedLibrary.RootDirectory &
                Environment.NewLine &
                Environment.NewLine &
                "PaperRoute did not discard the manuscript database. You can continue using the library, but one or more managed Version History or Submission Packet files may be missing or awaiting cleanup." &
                Environment.NewLine &
                Environment.NewLine &
                "Technical details: " &
                technicalDetails
            )

        End Function


        Private Sub ResetRecoveryState()

            _lastLoadRecoveredFromBackup =
                False

            _lastRecoveryPreservedFilePath =
                String.Empty

            _lastManagedLibraryRecoveryWarning =
                String.Empty

            _lastRecoveryKeptStagingNotice =
                String.Empty

        End Sub


        ' A notice, not a warning: nothing failed, nothing is missing, and
        ' the kept folders hold the only copy of their files.
        Private Shared Function BuildKeptStagingNotice(
            kept As List(Of String)
        ) As String

            Return (
                "PaperRoute kept " &
                If(
                    kept.Count = 1,
                    "a staged folder",
                    kept.Count.ToString() & " staged folders"
                ) &
                " that this safety backup does not reference, in case the set-aside primary file needs " &
                If(kept.Count = 1, "it", "them") &
                ":" &
                Environment.NewLine &
                String.Join(Environment.NewLine, kept) &
                Environment.NewLine &
                Environment.NewLine &
                "Nothing is missing. Leave " &
                If(kept.Count = 1, "it", "them") &
                " where " &
                If(kept.Count = 1, "it is", "they are") &
                "."
            )

        End Function


        ' =====================================================
        ' Save baseline
        ' =====================================================

        Private Sub RecordBaseline(
            manuscripts As IEnumerable(Of Manuscript)
        )

            _baselineVersions =
                ManagedLibraryService.BuildReferencedVersionMap(
                    manuscripts
                )

            _baselinePacketFiles =
                ManagedPacketDeletionService.BuildReferencedPacketFileMap(
                    manuscripts
                )

            _baselineCorrespondence =
                ManagedLibraryService.BuildReferencedCorrespondenceMap(
                    manuscripts
                )

        End Sub


        ' Folders a sweep could not move because a file in them was in use
        ' stay in the baseline, so the next save tries them again, and are
        ' named on the status line.
        Private Sub KeepFoldersLeftInPlace(
            versionTransaction As ManagedLibraryService.ManagedVersionDeletionTransaction,
            packetTransaction As ManagedPacketDeletionService.ManagedPacketDeletionTransaction
        )

            Dim folders As New List(Of String)()

            For Each skipped In versionTransaction.SkippedFolders

                AddBaselineVersion(
                    skipped.ManuscriptId,
                    skipped.VersionId
                )

                folders.Add(
                    skipped.Folder
                )

            Next

            For Each skipped In packetTransaction.SkippedFolders

                AddBaselinePacketFile(
                    skipped.ManuscriptId,
                    skipped.PacketId,
                    skipped.PacketFileId
                )

                folders.Add(
                    skipped.Folder
                )

            Next

            If folders.Count = 0 Then
                Return
            End If

            If folders.Count = 1 Then

                _lastSaveWarning =
                    "A folder stayed in place because a file in it is in use: " &
                    folders(0)

            Else

                _lastSaveWarning =
                    folders.Count.ToString() &
                    " folders stayed in place because files in them are in use: " &
                    String.Join("; ", folders)

            End If

        End Sub


        Private Sub AddBaselineVersion(
            manuscriptId As Guid,
            versionId As Guid
        )

            Dim versions As HashSet(Of Guid) =
                Nothing

            If Not _baselineVersions.TryGetValue(
                manuscriptId,
                versions
            ) Then

                versions =
                    New HashSet(Of Guid)()

                _baselineVersions(manuscriptId) =
                    versions

            End If

            versions.Add(
                versionId
            )

        End Sub


        Private Sub AddBaselinePacketFile(
            manuscriptId As Guid,
            packetId As Guid,
            packetFileId As Guid
        )

            Dim packets As Dictionary(Of Guid, HashSet(Of Guid)) =
                Nothing

            If Not _baselinePacketFiles.TryGetValue(
                manuscriptId,
                packets
            ) Then

                packets =
                    New Dictionary(Of Guid, HashSet(Of Guid))()

                _baselinePacketFiles(manuscriptId) =
                    packets

            End If

            Dim files As HashSet(Of Guid) =
                Nothing

            If Not packets.TryGetValue(
                packetId,
                files
            ) Then

                files =
                    New HashSet(Of Guid)()

                packets(packetId) =
                    files

            End If

            files.Add(
                packetFileId
            )

        End Sub


        ' =====================================================
        ' Save
        ' =====================================================

        Public Sub Save(
            manuscripts As List(Of Manuscript)
        )

            If manuscripts Is Nothing Then

                Throw New ArgumentNullException(
                    NameOf(manuscripts)
                )

            End If

            _lastSaveWarning =
                String.Empty

            ' Reject dangling packet/readiness references before copying or
            ' staging managed files, or replacing the authoritative database.
            ' The saved model must satisfy the same constraints as a reload.
            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing Then
                    Throw New InvalidDataException(
                        "The manuscript library contains an invalid null manuscript record."
                    )
                End If

                SubmissionReadinessValidationService.NormalizeAndValidateManuscript(
                    manuscript
                )

                ReviewerResponseService.NormalizeAndValidateManuscript(manuscript)
                PublicationMatchService.NormalizeAndValidateManuscript(manuscript)
                WorkTypeService.NormalizeAndValidateManuscript(manuscript)
                JournalShortlistService.NormalizeManuscript(manuscript)
                AssistantSuggestionService.NormalizeManuscript(manuscript)

            Next

            Directory.CreateDirectory(
                _dataDirectory
            )

            ' Finish pending managed-library copies first.
            _managedLibrary.CommitManagedCopies(
                manuscripts
            )

            Dim tempFilePath As String =
                Path.Combine(
                    _dataDirectory,
                    "manuscripts.tmp"
                )

            Dim deletionTransaction As ManagedLibraryService.ManagedVersionDeletionTransaction =
                Nothing

            Dim packetDeletionTransaction As ManagedPacketDeletionService.ManagedPacketDeletionTransaction =
                Nothing

            Try

                ' Managed version directories removed from the working model
                ' are moved into reversible staging before authoritative JSON
                ' changes. A failed save restores those snapshots; a
                ' successful save commits their removal.
                ' Only folders the last load or save referenced are staged;
                ' without a baseline, nothing is.
                deletionTransaction =
                    _managedLibrary.BeginVersionDeletionTransaction(
                        manuscripts,
                        If(
                            _baselineVersions,
                            New Dictionary(Of Guid, HashSet(Of Guid))()
                        ),
                        If(
                            _baselineCorrespondence,
                            New Dictionary(Of Guid, HashSet(Of (SubmissionId As Guid, ItemId As Guid)))()
                        )
                    )

                Dim packetDeletionService As New ManagedPacketDeletionService(
                    _managedLibrary.RootDirectory
                )

                packetDeletionTransaction =
                    packetDeletionService.BeginDeletionTransaction(
                        manuscripts,
                        If(
                            _baselinePacketFiles,
                            New Dictionary(Of Guid, Dictionary(Of Guid, HashSet(Of Guid)))()
                        )
                    )

                Using stream As New FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize:=65536,
                    options:=FileOptions.SequentialScan
                )

                    JsonSerializer.Serialize(
                        Of List(Of Manuscript)
                    )(
                        stream,
                        manuscripts,
                        _jsonOptions
                    )

                    stream.Flush(
                        flushToDisk:=True
                    )

                End Using

                If File.Exists(
                    _dataFilePath
                ) Then

                    File.Replace(
                        tempFilePath,
                        _dataFilePath,
                        _backupFilePath,
                        True
                    )

                Else

                    File.Move(
                        tempFilePath,
                        _dataFilePath
                    )

                End If

                ' The saved library is now the authority, so it is the next
                ' save's baseline, together with the folders left in place
                ' this time, which the next save tries again.
                RecordBaseline(
                    manuscripts
                )

                KeepFoldersLeftInPlace(
                    deletionTransaction,
                    packetDeletionTransaction
                )

                packetDeletionTransaction.Commit()
                deletionTransaction.Commit()

            Catch saveException As Exception

                Dim rollbackFailures As New List(Of Exception)()

                If packetDeletionTransaction IsNot Nothing Then

                    Try

                        packetDeletionTransaction.Rollback()

                    Catch packetRollbackException As Exception

                        rollbackFailures.Add(
                            packetRollbackException
                        )

                    End Try

                End If

                If deletionTransaction IsNot Nothing Then

                    Try

                        deletionTransaction.Rollback()

                    Catch versionRollbackException As Exception

                        rollbackFailures.Add(
                            versionRollbackException
                        )

                    End Try

                End If

                If rollbackFailures.Count > 0 Then

                    Dim failures As New List(Of Exception) From {
                        saveException
                    }

                    failures.AddRange(
                        rollbackFailures
                    )

                    Throw New InvalidDataException(
                        "PaperRoute could not save the manuscript library and could not fully restore one or more staged managed files. The staged files have been preserved for startup recovery.",
                        New AggregateException(
                            failures
                        )
                    )

                End If

                Throw

            Finally

                If packetDeletionTransaction IsNot Nothing Then
                    packetDeletionTransaction.Dispose()
                End If

                If deletionTransaction IsNot Nothing Then
                    deletionTransaction.Dispose()
                End If

                If File.Exists(
                    tempFilePath
                ) Then

                    Try

                        File.Delete(
                            tempFilePath
                        )

                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

            End Try

        End Sub


        ' =====================================================
        ' Pre-import safety backup
        ' =====================================================

        Public Function CreatePreImportBackup() As String

            If Not File.Exists(
                _dataFilePath
            ) Then

                Return String.Empty

            End If

            Dim backupDirectory As String =
                Path.Combine(
                    _dataDirectory,
                    "backups"
                )

            Directory.CreateDirectory(
                backupDirectory
            )

            Dim backupName As String =
                "manuscripts_pre-import_" &
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss"
                ) &
                ".json"

            Dim backupPath As String =
                Path.Combine(
                    backupDirectory,
                    backupName
                )

            File.Copy(
                _dataFilePath,
                backupPath,
                False
            )

            Return backupPath

        End Function


        ' =====================================================
        ' Compatibility / normalization
        ' =====================================================

        Private Sub NormalizeLoadedData(
            manuscripts As List(Of Manuscript)
        )

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing Then

                    Throw New InvalidDataException(
                        "The manuscript library contains an invalid null manuscript record."
                    )

                End If

                If manuscript.CurrentStage =
                   PaperStage.Published AndAlso
                   manuscript.Location =
                   ManuscriptLocation.Pipeline Then

                    manuscript.Location =
                        ManuscriptLocation.Published

                End If

                If manuscript.Metadata Is Nothing Then

                    manuscript.Metadata =
                        New ManuscriptMetadata()

                End If

                If manuscript.Metadata.Keywords Is Nothing Then

                    manuscript.Metadata.Keywords =
                        New List(Of String)()

                End If

                If manuscript.Metadata.ExternalIdentifiers Is Nothing Then

                    manuscript.Metadata.ExternalIdentifiers =
                        New Dictionary(Of String, String)()

                End If

                manuscript.ManuscriptUrl =
                    If(
                        manuscript.ManuscriptUrl,
                        String.Empty
                    )

                If manuscript.RelatedLinks Is Nothing Then

                    manuscript.RelatedLinks =
                        New List(Of ManuscriptExternalLink)()

                End If

                Dim relatedLinkIds As New HashSet(Of Guid)()

                For Each relatedLink As ManuscriptExternalLink In
                    manuscript.RelatedLinks

                    If relatedLink Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an invalid null external-link record."
                        )

                    End If

                    If relatedLink.Id = Guid.Empty OrElse
                       Not relatedLinkIds.Add(
                           relatedLink.Id
                       ) Then

                        Throw New InvalidDataException(
                            "The manuscript library contains invalid or duplicate external-link identifiers."
                        )

                    End If

                    relatedLink.Label =
                        If(
                            relatedLink.Label,
                            String.Empty
                        )

                    relatedLink.Url =
                        If(
                            relatedLink.Url,
                            String.Empty
                        )

                    relatedLink.Notes =
                        If(
                            relatedLink.Notes,
                            String.Empty
                        )

                Next

                If manuscript.Reminders Is Nothing Then

                    manuscript.Reminders =
                        New List(Of ManuscriptReminder)()

                End If

                Dim reminderIds As New HashSet(Of Guid)()

                For Each reminder As ManuscriptReminder In
                    manuscript.Reminders

                    If reminder Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an invalid null reminder record."
                        )

                    End If

                    If reminder.Id = Guid.Empty OrElse
                       Not reminderIds.Add(
                           reminder.Id
                       ) Then

                        Throw New InvalidDataException(
                            "The manuscript library contains invalid or duplicate reminder identifiers."
                        )

                    End If

                    reminder.Title =
                        If(
                            reminder.Title,
                            String.Empty
                        )

                    reminder.Notes =
                        If(
                            reminder.Notes,
                            String.Empty
                        )

                    reminder.DueDate =
                        reminder.DueDate.Date

                    If reminder.CompletedDate.HasValue Then

                        reminder.CompletedDate =
                            reminder.CompletedDate.Value

                    End If

                Next

                If manuscript.Versions Is Nothing Then

                    manuscript.Versions =
                        New List(Of ManuscriptVersion)()

                End If

                Dim versionIds As New HashSet(Of Guid)()

                For Each version As ManuscriptVersion In
                    manuscript.Versions

                    If version Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an invalid null manuscript-version record."
                        )

                    End If

                    If version.Id = Guid.Empty OrElse
                       Not versionIds.Add(
                           version.Id
                       ) Then

                        Throw New InvalidDataException(
                            "The manuscript library contains invalid or duplicate manuscript-version identifiers."
                        )

                    End If

                    version.Label =
                        If(
                            version.Label,
                            String.Empty
                        )

                    version.Notes =
                        If(
                            version.Notes,
                            String.Empty
                        )

                    version.LocalFilePath =
                        If(
                            version.LocalFilePath,
                            String.Empty
                        )

                    If version.SubmissionId.HasValue AndAlso
                       version.SubmissionId.Value = Guid.Empty Then

                        Throw New InvalidDataException(
                            "The manuscript library contains a manuscript version with an invalid submission reference."
                        )

                    End If

                    If version.DecisionId.HasValue AndAlso
                       version.DecisionId.Value = Guid.Empty Then

                        Throw New InvalidDataException(
                            "The manuscript library contains a manuscript version with an invalid decision reference."
                        )

                    End If

                    If version.RevisionRoundNumber.HasValue AndAlso
                       version.RevisionRoundNumber.Value <= 0 Then

                        Throw New InvalidDataException(
                            "The manuscript library contains a manuscript version with an invalid revision-round number."
                        )

                    End If

                Next

                If manuscript.CurrentVersionId.HasValue AndAlso
                   Not versionIds.Contains(
                       manuscript.CurrentVersionId.Value
                   ) Then

                    Throw New InvalidDataException(
                        "The manuscript library identifies a current manuscript version that is not present in version history."
                    )

                End If

                If manuscript.Authors Is Nothing Then

                    manuscript.Authors =
                        New List(Of ManuscriptAuthor)()

                End If

                Dim manuscriptAuthorIds As New HashSet(Of Guid)()

                For Each authorLink As ManuscriptAuthor In manuscript.Authors

                    If authorLink Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an invalid null author link."
                        )

                    End If

                    If authorLink.AuthorId = Guid.Empty Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an author link without a valid author identifier."
                        )

                    End If

                    If Not manuscriptAuthorIds.Add(
                        authorLink.AuthorId
                    ) Then

                        Throw New InvalidDataException(
                            "The manuscript library contains the same structured author more than once on a manuscript."
                        )

                    End If

                    If authorLink.AffiliationIds Is Nothing Then

                        authorLink.AffiliationIds =
                            New List(Of Guid)()

                    End If

                Next

                If manuscript.History Is Nothing Then

                    manuscript.History =
                        New List(Of HistoryEvent)()

                End If

                If manuscript.Submissions Is Nothing Then

                    manuscript.Submissions =
                        New List(Of JournalSubmission)()

                End If

                For Each submission As JournalSubmission In manuscript.Submissions

                    If submission Is Nothing Then

                        Throw New InvalidDataException(
                            "The manuscript library contains an invalid null submission record."
                        )

                    End If

                    If submission.Decisions Is Nothing Then

                        submission.Decisions =
                            New List(Of EditorialDecisionEvent)()

                    End If

                    If submission.Correspondence Is Nothing Then

                        submission.Correspondence =
                            New List(Of CorrespondenceItem)()

                    End If

                    If submission.FollowUpDate.HasValue Then

                        submission.FollowUpDate =
                            submission.FollowUpDate.Value.Date

                    End If

                Next

                SubmissionReadinessValidationService.NormalizeAndValidateManuscript(
                    manuscript
                )

                ReviewerResponseService.NormalizeAndValidateManuscript(manuscript)
                PublicationMatchService.NormalizeAndValidateManuscript(manuscript)
                WorkTypeService.NormalizeAndValidateManuscript(manuscript)
                JournalShortlistService.NormalizeManuscript(manuscript)
                AssistantSuggestionService.NormalizeManuscript(manuscript)

            Next

        End Sub

    End Class

End Namespace
