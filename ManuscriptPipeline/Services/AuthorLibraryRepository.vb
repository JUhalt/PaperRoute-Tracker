Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class AuthorLibraryRepository

        Private ReadOnly _dataDirectory As String
        Private ReadOnly _dataFilePath As String
        Private ReadOnly _backupFilePath As String
        Private ReadOnly _jsonOptions As JsonSerializerOptions

        Private _lastLoadRecoveredFromBackup As Boolean
        Private _lastRecoveryPreservedFilePath As String =
            String.Empty


        Public Sub New()

            Me.New(
                GetDefaultDataDirectory()
            )

        End Sub


        Friend Sub New(
            dataDirectory As String
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
                    "authors.json"
                )

            _backupFilePath =
                Path.Combine(
                    _dataDirectory,
                    "authors.bak"
                )

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


        Public Function Load() As AuthorLibraryData

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

            If Not primaryExists AndAlso
               Not backupExists Then

                Return New AuthorLibraryData()

            End If

            If primaryExists Then

                Dim primaryFailure As Exception =
                    Nothing

                Dim primary As AuthorLibraryData =
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

                If backupExists Then

                    Dim backupFailure As Exception =
                        Nothing

                    Dim backup As AuthorLibraryData =
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

                    Throw New InvalidDataException(
                        "PaperRoute could not safely load either the primary " &
                        "author library or its safety backup. Neither file has " &
                        "been overwritten.",
                        primaryFailure
                    )

                End If

                Throw New InvalidDataException(
                    "PaperRoute could not read the author library, and no " &
                    "safety backup is available. The existing file has not " &
                    "been overwritten.",
                    primaryFailure
                )

            End If

            Dim backupOnlyFailure As Exception =
                Nothing

            Dim recovered As AuthorLibraryData =
                TryLoadLibraryFile(
                    _backupFilePath,
                    backupOnlyFailure
                )

            If recovered Is Nothing Then

                Throw New InvalidDataException(
                    "PaperRoute's primary author library is missing, and the " &
                    "available safety backup could not be read.",
                    backupOnlyFailure
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


        Public Sub Save(
            library As AuthorLibraryData
        )

            If library Is Nothing Then

                Throw New ArgumentNullException(
                    NameOf(library)
                )

            End If

            NormalizeLoadedData(
                library
            )

            Directory.CreateDirectory(
                _dataDirectory
            )

            Dim json As String =
                JsonSerializer.Serialize(
                    library,
                    _jsonOptions
                )

            Dim tempFilePath As String =
                Path.Combine(
                    _dataDirectory,
                    "authors.tmp"
                )

            Try

                File.WriteAllText(
                    tempFilePath,
                    json
                )

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


        Private Function TryLoadLibraryFile(
            filePath As String,
            ByRef failure As Exception
        ) As AuthorLibraryData

            failure =
                Nothing

            Try

                Dim json As String =
                    File.ReadAllText(
                        filePath
                    )

                If String.IsNullOrWhiteSpace(
                    json
                ) Then

                    Throw New InvalidDataException(
                        "The author library is empty."
                    )

                End If

                Dim loaded As AuthorLibraryData =
                    JsonSerializer.Deserialize(
                        Of AuthorLibraryData
                    )(
                        json,
                        _jsonOptions
                    )

                If loaded Is Nothing Then

                    Throw New InvalidDataException(
                        "The author library does not contain valid PaperRoute data."
                    )

                End If

                Return loaded

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
                    "authors.recovery.tmp"
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
                    "PaperRoute found a valid author-library safety backup, " &
                    "but could not restore it to the primary data location.",
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
                    End Try

                End If

            End Try

        End Sub


        Private Function CreateUniqueRecoveryPath(
            recoveryDirectory As String
        ) As String

            Dim baseName As String =
                "authors_corrupt_" &
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


        Private Sub ResetRecoveryState()

            _lastLoadRecoveredFromBackup =
                False

            _lastRecoveryPreservedFilePath =
                String.Empty

        End Sub


        Private Sub NormalizeLoadedData(
            library As AuthorLibraryData
        )

            If library.Authors Is Nothing Then

                library.Authors =
                    New List(Of AuthorRecord)()

            End If

            If library.Affiliations Is Nothing Then

                library.Affiliations =
                    New List(Of AffiliationRecord)()

            End If

            If library.Journals Is Nothing Then

                library.Journals =
                    New List(Of JournalRecord)()

            End If

            Dim authorIds As New HashSet(Of Guid)()

            For Each author As AuthorRecord In library.Authors

                If author Is Nothing Then

                    Throw New InvalidDataException(
                        "The author library contains a null author record."
                    )

                End If

                If author.Id = Guid.Empty Then

                    Throw New InvalidDataException(
                        "The author library contains an author without a valid identifier."
                    )

                End If

                If Not authorIds.Add(
                    author.Id
                ) Then

                    Throw New InvalidDataException(
                        "The author library contains duplicate author identifiers."
                    )

                End If

                author.GivenName =
                    If(
                        author.GivenName,
                        String.Empty
                    )

                author.MiddleName =
                    If(
                        author.MiddleName,
                        String.Empty
                    )

                author.FamilyName =
                    If(
                        author.FamilyName,
                        String.Empty
                    )

                author.Suffix =
                    If(
                        author.Suffix,
                        String.Empty
                    )

                author.DisplayNameOverride =
                    If(
                        author.DisplayNameOverride,
                        String.Empty
                    )

                author.Orcid =
                    If(
                        author.Orcid,
                        String.Empty
                    )

                author.Notes =
                    If(
                        author.Notes,
                        String.Empty
                    )

            Next

            Dim affiliationIds As New HashSet(Of Guid)()

            For Each affiliation As AffiliationRecord In library.Affiliations

                If affiliation Is Nothing Then

                    Throw New InvalidDataException(
                        "The author library contains a null affiliation record."
                    )

                End If

                If affiliation.Id = Guid.Empty Then

                    Throw New InvalidDataException(
                        "The author library contains an affiliation without a valid identifier."
                    )

                End If

                If Not affiliationIds.Add(
                    affiliation.Id
                ) Then

                    Throw New InvalidDataException(
                        "The author library contains duplicate affiliation identifiers."
                    )

                End If

                affiliation.Institution =
                    If(
                        affiliation.Institution,
                        String.Empty
                    )

                affiliation.Department =
                    If(
                        affiliation.Department,
                        String.Empty
                    )

                affiliation.City =
                    If(
                        affiliation.City,
                        String.Empty
                    )

                affiliation.Region =
                    If(
                        affiliation.Region,
                        String.Empty
                    )

                affiliation.Country =
                    If(
                        affiliation.Country,
                        String.Empty
                    )

                affiliation.Notes =
                    If(
                        affiliation.Notes,
                        String.Empty
                    )

            Next

            Dim journalIds As New HashSet(Of Guid)()

            For Each journal As JournalRecord In library.Journals

                If journal Is Nothing Then

                    Throw New InvalidDataException(
                        "The reusable metadata library contains a null journal record."
                    )

                End If

                If journal.Id = Guid.Empty Then

                    Throw New InvalidDataException(
                        "The reusable metadata library contains a journal without a valid identifier."
                    )

                End If

                If Not journalIds.Add(
                    journal.Id
                ) Then

                    Throw New InvalidDataException(
                        "The reusable metadata library contains duplicate journal identifiers."
                    )

                End If

                journal.Name =
                    If(
                        journal.Name,
                        String.Empty
                    )

                journal.Publisher =
                    If(
                        journal.Publisher,
                        String.Empty
                    )

                journal.HomepageUrl =
                    If(
                        journal.HomepageUrl,
                        String.Empty
                    )

                journal.SubmissionPortalUrl =
                    If(
                        journal.SubmissionPortalUrl,
                        String.Empty
                    )

                journal.Notes =
                    If(
                        journal.Notes,
                        String.Empty
                    )

                SubmissionReadinessValidationService.NormalizeAndValidateJournal(
                    journal
                )

            Next

        End Sub

    End Class

End Namespace
