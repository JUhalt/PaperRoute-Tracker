Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class StorageMigrationService

        Public Const CurrentSchemaVersion As Integer = 3

        Private Const MinimumMigratableSchemaVersion As Integer = 1

        Private Sub New()
        End Sub


        Public Shared Sub EnsureCurrentStorage()

            EnsureCurrentStorage(
                CurrentDataRoot(),
                LegacyDataRoot(),
                CurrentManagedLibraryRoot(),
                LegacyManagedLibraryRoot()
            )

        End Sub


        Friend Shared Sub EnsureCurrentStorage(
            currentDataRoot As String,
            legacyDataRoot As String,
            currentManagedLibraryRoot As String,
            legacyManagedLibraryRoot As String
        )

            ValidateRoot(
                currentDataRoot,
                NameOf(currentDataRoot)
            )

            ValidateRoot(
                legacyDataRoot,
                NameOf(legacyDataRoot)
            )

            ValidateRoot(
                currentManagedLibraryRoot,
                NameOf(currentManagedLibraryRoot)
            )

            ValidateRoot(
                legacyManagedLibraryRoot,
                NameOf(legacyManagedLibraryRoot)
            )

            ValidateExistingSchemaIfPresent(
                currentDataRoot
            )

            MigrateManagedLibraryIfNeeded(
                currentManagedLibraryRoot,
                legacyManagedLibraryRoot
            )

            MigrateApplicationDataIfNeeded(
                currentDataRoot,
                legacyDataRoot,
                currentManagedLibraryRoot,
                legacyManagedLibraryRoot
            )

            MigrateSchemaIfNeeded(
                currentDataRoot
            )

            EnsureSchemaVersion(
                currentDataRoot
            )

        End Sub


        Public Shared Function CurrentDataRoot() As String

            Return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                StorageEnvironment.DataFolderName()
            )

        End Function


        Public Shared Function LegacyDataRoot() As String

            Return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                StorageEnvironment.LegacyDataFolderName()
            )

        End Function


        Public Shared Function CurrentManagedLibraryRoot() As String

            Return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                ),
                StorageEnvironment.ManagedLibraryFolderName()
            )

        End Function


        Public Shared Function LegacyManagedLibraryRoot() As String

            Return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                ),
                StorageEnvironment.LegacyManagedLibraryFolderName()
            )

        End Function


        Public Shared Function SchemaFilePath() As String

            Return SchemaFilePath(
                CurrentDataRoot()
            )

        End Function


        Friend Shared Function SchemaFilePath(
            currentDataRoot As String
        ) As String

            Return Path.Combine(
                currentDataRoot,
                "data",
                "schema.json"
            )

        End Function


        ' =====================================================
        ' Schema reading / validation
        ' =====================================================

        Public Shared Function ReadSchemaVersion() As Integer

            Return ReadSchemaVersion(
                SchemaFilePath()
            )

        End Function


        Friend Shared Function ReadSchemaVersion(
            schemaPath As String
        ) As Integer

            If Not File.Exists(schemaPath) Then
                Return 0
            End If

            Dim json As String =
                File.ReadAllText(schemaPath)

            If String.IsNullOrWhiteSpace(json) Then

                Throw New InvalidDataException(
                    "The PaperRoute storage schema file is empty. " &
                    "It was not changed."
                )

            End If

            Try

                Using document As JsonDocument =
                    JsonDocument.Parse(json)

                    Dim root As JsonElement =
                        document.RootElement

                    If root.ValueKind <> JsonValueKind.Object Then

                        Throw New InvalidDataException(
                            "The PaperRoute storage schema file does not contain a valid schema object. " &
                            "It was not changed."
                        )

                    End If

                    Dim versionElement As JsonElement

                    If Not root.TryGetProperty(
                        "SchemaVersion",
                        versionElement
                    ) Then

                        Throw New InvalidDataException(
                            "The PaperRoute storage schema file does not contain a SchemaVersion value. " &
                            "It was not changed."
                        )

                    End If

                    If versionElement.ValueKind <> JsonValueKind.Number Then

                        Throw New InvalidDataException(
                            "The PaperRoute storage schema version is not numeric. " &
                            "The schema file was not changed."
                        )

                    End If

                    Dim version As Integer

                    If Not versionElement.TryGetInt32(version) Then

                        Throw New InvalidDataException(
                            "The PaperRoute storage schema version is not a valid integer. " &
                            "The schema file was not changed."
                        )

                    End If

                    If version <= 0 Then

                        Throw New InvalidDataException(
                            "The PaperRoute storage schema version must be greater than zero. " &
                            "The schema file was not changed."
                        )

                    End If

                    Return version

                End Using

            Catch ex As JsonException

                Throw New InvalidDataException(
                    "The PaperRoute storage schema file contains invalid JSON. " &
                    "It was not changed.",
                    ex
                )

            End Try

        End Function


        Private Shared Sub ValidateExistingSchemaIfPresent(
            applicationRoot As String
        )

            Dim schemaPath As String =
                SchemaFilePath(applicationRoot)

            If Not File.Exists(schemaPath) Then
                Return
            End If

            Dim existingVersion As Integer =
                ReadSchemaVersion(schemaPath)

            ValidateSupportedSchemaVersion(
                existingVersion
            )

        End Sub


        Private Shared Sub ValidateSupportedSchemaVersion(
            existingVersion As Integer
        )

            If existingVersion > CurrentSchemaVersion Then

                Throw New InvalidOperationException(
                    "This PaperRoute library was created by a newer storage schema (" &
                    existingVersion.ToString() &
                    "). Update PaperRoute before opening it."
                )

            End If

            If existingVersion < MinimumMigratableSchemaVersion Then

                Throw New InvalidOperationException(
                    "This PaperRoute library uses storage schema " &
                    existingVersion.ToString() &
                    ", which cannot be migrated by this build."
                )

            End If

        End Sub


        Private Shared Sub MigrateSchemaIfNeeded(
            currentRoot As String
        )

            Dim schemaPath As String =
                SchemaFilePath(
                    currentRoot
                )

            If Not File.Exists(schemaPath) Then
                Return
            End If

            Dim existingVersion As Integer =
                ReadSchemaVersion(
                    schemaPath
                )

            ValidateSupportedSchemaVersion(
                existingVersion
            )

            While existingVersion < CurrentSchemaVersion

                Select Case existingVersion

                    Case 1

                        MigrateSchema1To2(
                            currentRoot,
                            schemaPath
                        )

                    Case 2

                        MigrateSchema2To3(
                            currentRoot,
                            schemaPath
                        )

                    Case Else

                        Throw New InvalidOperationException(
                            "PaperRoute does not have a migration path from storage schema " &
                            existingVersion.ToString() &
                            " to schema " &
                            CurrentSchemaVersion.ToString() &
                            "."
                        )

                End Select

                existingVersion =
                    ReadSchemaVersion(
                        schemaPath
                    )

            End While

        End Sub


        Private Shared Sub MigrateSchema1To2(
            currentRoot As String,
            schemaPath As String
        )

            ValidateCurrentManuscriptDataForSchema2(
                currentRoot
            )

            Dim backupPath As String =
                Path.Combine(
                    Path.GetDirectoryName(schemaPath),
                    "schema.v1.bak"
                )

            WriteSchemaVersionReplacingExisting(
                schemaPath,
                2,
                backupPath
            )

        End Sub


        Private Shared Sub MigrateSchema2To3(
            currentRoot As String,
            schemaPath As String
        )

            ValidateCurrentManuscriptDataForSchema3(
                currentRoot
            )

            Dim backupPath As String =
                Path.Combine(
                    Path.GetDirectoryName(schemaPath),
                    "schema.v2.bak"
                )

            WriteSchemaVersionReplacingExisting(
                schemaPath,
                3,
                backupPath
            )

        End Sub


        Private Shared Sub ValidateCurrentManuscriptDataForSchema2(
            currentRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    currentRoot,
                    "data",
                    "manuscripts.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(
                    dataPath
                )

            If String.IsNullOrWhiteSpace(json) Then

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 1 because the manuscript data file is empty. " &
                    "The existing schema and manuscript data were left unchanged."
                )

            End If

            Try

                Dim manuscripts As List(Of Manuscript) =
                    JsonSerializer.Deserialize(
                        Of List(Of Manuscript)
                    )(
                        json,
                        CreateManuscriptJsonOptions()
                    )

                If manuscripts Is Nothing Then

                    Throw New InvalidDataException(
                        "PaperRoute cannot migrate storage schema 1 because the manuscript data could not be read. " &
                        "The existing schema and manuscript data were left unchanged."
                    )

                End If

                For Each manuscript As Manuscript In manuscripts

                    If manuscript Is Nothing Then

                        Throw New InvalidDataException(
                            "PaperRoute cannot migrate storage schema 1 because the manuscript library contains a null record. " &
                            "The existing schema and manuscript data were left unchanged."
                        )

                    End If

                Next

            Catch ex As JsonException

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 1 because the manuscript data contains invalid JSON. " &
                    "The existing schema and manuscript data were left unchanged.",
                    ex
                )

            End Try

        End Sub


        Private Shared Sub ValidateCurrentManuscriptDataForSchema3(
            currentRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    currentRoot,
                    "data",
                    "manuscripts.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(
                    dataPath
                )

            If String.IsNullOrWhiteSpace(json) Then

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 2 because the manuscript data file is empty. " &
                    "The existing schema and manuscript data were left unchanged."
                )

            End If

            Try

                Dim manuscripts As List(Of Manuscript) =
                    JsonSerializer.Deserialize(
                        Of List(Of Manuscript)
                    )(
                        json,
                        CreateManuscriptJsonOptions()
                    )

                If manuscripts Is Nothing Then

                    Throw New InvalidDataException(
                        "PaperRoute cannot migrate storage schema 2 because the manuscript data could not be read. " &
                        "The existing schema and manuscript data were left unchanged."
                    )

                End If

                For Each manuscript As Manuscript In manuscripts

                    If manuscript Is Nothing Then

                        Throw New InvalidDataException(
                            "PaperRoute cannot migrate storage schema 2 because the manuscript library contains a null record. " &
                            "The existing schema and manuscript data were left unchanged."
                        )

                    End If

                Next

            Catch ex As JsonException

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 2 because the manuscript data contains invalid JSON. " &
                    "The existing schema and manuscript data were left unchanged.",
                    ex
                )

            End Try

        End Sub


        Private Shared Sub WriteSchemaVersionReplacingExisting(
            schemaPath As String,
            schemaVersion As Integer,
            backupPath As String
        )

            Dim payload As New Dictionary(Of String, Object) From {
                {
                    "SchemaVersion",
                    schemaVersion
                },
                {
                    "UpdatedAtUtc",
                    DateTime.UtcNow.ToString("O")
                }
            }

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }

            Dim tempPath As String =
                schemaPath &
                ".tmp-" &
                Guid.NewGuid().ToString("N")

            Try

                File.WriteAllText(
                    tempPath,
                    JsonSerializer.Serialize(
                        payload,
                        options
                    )
                )

                If File.Exists(backupPath) Then

                    File.Delete(
                        backupPath
                    )

                End If

                File.Replace(
                    tempPath,
                    schemaPath,
                    backupPath,
                    True
                )

            Finally

                If File.Exists(tempPath) Then

                    Try
                        File.Delete(tempPath)
                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

            End Try

        End Sub


        ' =====================================================
        ' Application data migration
        ' =====================================================

        Private Shared Sub MigrateApplicationDataIfNeeded(
            currentRoot As String,
            legacyRoot As String,
            currentManagedLibraryRoot As String,
            legacyManagedLibraryRoot As String
        )

            If HasCurrentApplicationData(currentRoot) Then
                Return
            End If

            If Not Directory.Exists(legacyRoot) Then

                Directory.CreateDirectory(
                    currentRoot
                )

                Return

            End If

            If Directory.Exists(currentRoot) AndAlso
               IsDirectoryEmpty(currentRoot) Then

                Directory.Delete(
                    currentRoot,
                    True
                )

            End If

            If Directory.Exists(currentRoot) Then
                Return
            End If

            Dim stagingRoot As String =
                currentRoot &
                ".migration-" &
                Guid.NewGuid().ToString("N")

            Try

                CopyDirectory(
                    legacyRoot,
                    stagingRoot
                )

                RewriteManagedLibraryPathsInCopiedData(
                    stagingRoot,
                    legacyManagedLibraryRoot,
                    currentManagedLibraryRoot
                )

                ValidateExistingSchemaIfPresent(
                    stagingRoot
                )

                ValidateCopiedManuscriptData(
                    stagingRoot
                )

                Directory.Move(
                    stagingRoot,
                    currentRoot
                )

                WriteMigrationReceipt(
                    currentRoot,
                    legacyRoot,
                    legacyManagedLibraryRoot,
                    currentManagedLibraryRoot
                )

            Catch ex As Exception

                DeleteDirectoryBestEffort(
                    stagingRoot
                )

                Throw New InvalidOperationException(
                    "PaperRoute could not safely migrate the legacy ManuscriptPipeline data folder. " &
                    "The original data was left unchanged." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    ex
                )

            End Try

        End Sub


        ' =====================================================
        ' Managed library migration
        ' =====================================================

        Private Shared Sub MigrateManagedLibraryIfNeeded(
            currentRoot As String,
            legacyRoot As String
        )

            If Directory.Exists(currentRoot) AndAlso
               Not IsDirectoryEmpty(currentRoot) Then

                Return

            End If

            If Not Directory.Exists(legacyRoot) Then
                Return
            End If

            If Directory.Exists(currentRoot) AndAlso
               IsDirectoryEmpty(currentRoot) Then

                Directory.Delete(
                    currentRoot,
                    True
                )

            End If

            Dim stagingRoot As String =
                currentRoot &
                ".migration-" &
                Guid.NewGuid().ToString("N")

            Try

                CopyDirectory(
                    legacyRoot,
                    stagingRoot
                )

                Directory.Move(
                    stagingRoot,
                    currentRoot
                )

            Catch ex As Exception

                DeleteDirectoryBestEffort(
                    stagingRoot
                )

                Throw New InvalidOperationException(
                    "PaperRoute could not safely migrate the managed manuscript library. " &
                    "The original library was left unchanged." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    ex
                )

            End Try

        End Sub


        ' =====================================================
        ' Schema creation
        ' =====================================================

        Private Shared Sub EnsureSchemaVersion(
            currentRoot As String
        )

            Dim dataDirectory As String =
                Path.Combine(
                    currentRoot,
                    "data"
                )

            Dim schemaPath As String =
                SchemaFilePath(currentRoot)

            Directory.CreateDirectory(
                dataDirectory
            )

            If Not File.Exists(schemaPath) Then

                WriteCurrentSchemaVersion(
                    schemaPath
                )

                Return

            End If

            Dim existingVersion As Integer =
                ReadSchemaVersion(schemaPath)

            ValidateSupportedSchemaVersion(
                existingVersion
            )

        End Sub


        Private Shared Sub WriteCurrentSchemaVersion(
            schemaPath As String
        )

            Dim payload As New Dictionary(Of String, Object) From {
                {
                    "SchemaVersion",
                    CurrentSchemaVersion
                },
                {
                    "UpdatedAtUtc",
                    DateTime.UtcNow.ToString("O")
                }
            }

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }

            Dim tempPath As String =
                schemaPath &
                ".tmp-" &
                Guid.NewGuid().ToString("N")

            Try

                File.WriteAllText(
                    tempPath,
                    JsonSerializer.Serialize(
                        payload,
                        options
                    )
                )

                File.Move(
                    tempPath,
                    schemaPath
                )

            Finally

                If File.Exists(tempPath) Then

                    Try
                        File.Delete(tempPath)
                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

            End Try

        End Sub


        ' =====================================================
        ' Storage detection
        ' =====================================================

        Private Shared Function HasCurrentApplicationData(
            root As String
        ) As Boolean

            If Not Directory.Exists(root) Then
                Return False
            End If

            Return (
                File.Exists(
                    Path.Combine(
                        root,
                        "settings.json"
                    )
                ) OrElse
                File.Exists(
                    Path.Combine(
                        root,
                        "data",
                        "manuscripts.json"
                    )
                ) OrElse
                File.Exists(
                    Path.Combine(
                        root,
                        "data",
                        "schema.json"
                    )
                )
            )

        End Function


        ' =====================================================
        ' Managed path rewriting
        ' =====================================================

        Private Shared Sub RewriteManagedLibraryPathsInCopiedData(
            copiedApplicationRoot As String,
            legacyManagedLibraryRoot As String,
            currentManagedLibraryRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    copiedApplicationRoot,
                    "data",
                    "manuscripts.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(dataPath)

            If String.IsNullOrWhiteSpace(json) Then
                Return
            End If

            Dim options As JsonSerializerOptions =
                CreateManuscriptJsonOptions()

            Dim manuscripts As List(Of Manuscript) =
                JsonSerializer.Deserialize(
                    Of List(Of Manuscript)
                )(
                    json,
                    options
                )

            If manuscripts Is Nothing Then

                Throw New InvalidDataException(
                    "The copied manuscript data could not be read."
                )

            End If

            Dim legacyRoot As String =
                NormalizeDirectoryPrefix(
                    legacyManagedLibraryRoot
                )

            Dim currentRoot As String =
                NormalizeDirectoryPrefix(
                    currentManagedLibraryRoot
                )

            Dim changed As Boolean =
                False

            For Each manuscript As Manuscript In manuscripts

                If manuscript.Submissions Is Nothing Then
                    Continue For
                End If

                For Each submission As JournalSubmission In manuscript.Submissions

                    If submission.Correspondence Is Nothing Then
                        Continue For
                    End If

                    For Each item As CorrespondenceItem In submission.Correspondence

                        If String.IsNullOrWhiteSpace(item.LocalFilePath) Then
                            Continue For
                        End If

                        Dim fullPath As String

                        Try

                            fullPath =
                                Path.GetFullPath(
                                    item.LocalFilePath
                                )

                        Catch

                            Continue For

                        End Try

                        If fullPath.StartsWith(
                            legacyRoot,
                            StringComparison.OrdinalIgnoreCase
                        ) Then

                            Dim relativePath As String =
                                fullPath.Substring(
                                    legacyRoot.Length
                                )

                            item.LocalFilePath =
                                Path.Combine(
                                    currentRoot,
                                    relativePath
                                )

                            changed =
                                True

                        End If

                    Next

                Next

            Next

            If changed Then

                File.WriteAllText(
                    dataPath,
                    JsonSerializer.Serialize(
                        manuscripts,
                        options
                    )
                )

            End If

        End Sub


        ' =====================================================
        ' Copied-data validation
        ' =====================================================

        Private Shared Sub ValidateCopiedManuscriptData(
            copiedApplicationRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    copiedApplicationRoot,
                    "data",
                    "manuscripts.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(dataPath)

            If String.IsNullOrWhiteSpace(json) Then
                Return
            End If

            Dim manuscripts As List(Of Manuscript) =
                JsonSerializer.Deserialize(
                    Of List(Of Manuscript)
                )(
                    json,
                    CreateManuscriptJsonOptions()
                )

            If manuscripts Is Nothing Then

                Throw New InvalidDataException(
                    "The copied manuscript data could not be validated."
                )

            End If

        End Sub


        Private Shared Function CreateManuscriptJsonOptions() As JsonSerializerOptions

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True,
                .IgnoreReadOnlyProperties = True,
                .PropertyNameCaseInsensitive = True
            }

            options.Converters.Add(
                New JsonStringEnumConverter()
            )

            Return options

        End Function


        ' =====================================================
        ' File-system helpers
        ' =====================================================

        Private Shared Sub CopyDirectory(
            sourceDirectory As String,
            destinationDirectory As String
        )

            Directory.CreateDirectory(
                destinationDirectory
            )

            For Each sourceFile As String In Directory.EnumerateFiles(sourceDirectory)

                Dim destinationFile As String =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(sourceFile)
                    )

                File.Copy(
                    sourceFile,
                    destinationFile,
                    False
                )

            Next

            For Each sourceSubdirectory As String In Directory.EnumerateDirectories(sourceDirectory)

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


        Private Shared Function IsDirectoryEmpty(
            directoryPath As String
        ) As Boolean

            If Not Directory.Exists(directoryPath) Then
                Return True
            End If

            Return Not Directory.EnumerateFileSystemEntries(directoryPath).Any()

        End Function


        Private Shared Function NormalizeDirectoryPrefix(
            directoryPath As String
        ) As String

            Dim fullPath As String =
                Path.GetFullPath(directoryPath)

            Return fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            ) &
            Path.DirectorySeparatorChar

        End Function


        Private Shared Sub DeleteDirectoryBestEffort(
            directoryPath As String
        )

            Try

                If Directory.Exists(directoryPath) Then

                    Directory.Delete(
                        directoryPath,
                        True
                    )

                End If

            Catch
                ' The source data is never deleted.
                ' Cleanup is best-effort.
            End Try

        End Sub


        ' =====================================================
        ' Migration receipt
        ' =====================================================

        Private Shared Sub WriteMigrationReceipt(
            currentRoot As String,
            legacyRoot As String,
            legacyLibraryRoot As String,
            currentLibraryRoot As String
        )

            Try

                Dim receiptPath As String =
                    Path.Combine(
                        currentRoot,
                        "migration.json"
                    )

                Dim payload As New Dictionary(Of String, Object) From {
                    {
                        "MigratedAtUtc",
                        DateTime.UtcNow.ToString("O")
                    },
                    {
                        "SourceDataRoot",
                        legacyRoot
                    },
                    {
                        "DestinationDataRoot",
                        currentRoot
                    },
                    {
                        "SourceManagedLibrary",
                        legacyLibraryRoot
                    },
                    {
                        "DestinationManagedLibrary",
                        currentLibraryRoot
                    },
                    {
                        "LegacyDataPreserved",
                        True
                    }
                }

                Dim options As New JsonSerializerOptions With {
                    .WriteIndented = True
                }

                File.WriteAllText(
                    receiptPath,
                    JsonSerializer.Serialize(
                        payload,
                        options
                    )
                )

            Catch
                ' The migration receipt is diagnostic information only.
            End Try

        End Sub


        ' =====================================================
        ' Argument validation
        ' =====================================================

        Private Shared Sub ValidateRoot(
            rootPath As String,
            parameterName As String
        )

            If String.IsNullOrWhiteSpace(rootPath) Then

                Throw New ArgumentException(
                    "A storage root path is required.",
                    parameterName
                )

            End If

        End Sub

    End Class

End Namespace