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

        Private _lastLoadRecoveredFromBackup As Boolean
        Private _lastRecoveryPreservedFilePath As String =
            String.Empty


        Public Sub New()
            Me.New(
                GetDefaultDataDirectory(),
                Nothing
            )
        End Sub


        Friend Sub New(
            dataDirectory As String,
            managedLibraryRoot As String
        )

            If String.IsNullOrWhiteSpace(
                dataDirectory
            ) Then

                Throw New ArgumentException(
                    "A data directory is required.",
                    NameOf(dataDirectory)
                )

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

            If String.IsNullOrWhiteSpace(
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

                    NormalizeLoadedData(
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

                        NormalizeLoadedData(
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

            NormalizeLoadedData(
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

                Using stream As New FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize:=65536,
                    options:=FileOptions.SequentialScan
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

                    Return loaded

                End Using

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


        Private Sub ResetRecoveryState()

            _lastLoadRecoveredFromBackup =
                False

            _lastRecoveryPreservedFilePath =
                String.Empty

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

            Try

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

            Finally

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

            Next

        End Sub

    End Class

End Namespace