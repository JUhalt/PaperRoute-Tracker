Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class Schema5MigrationTests

    Private _root As String = String.Empty
    Private _legacyData As String = String.Empty
    Private _currentData As String = String.Empty
    Private _legacyLibrary As String = String.Empty
    Private _currentLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

        _legacyData =
            Path.Combine(
                _root,
                "legacy-data"
            )

        _currentData =
            Path.Combine(
                _root,
                "paperroute-data"
            )

        _legacyLibrary =
            Path.Combine(
                _root,
                "legacy-library"
            )

        _currentLibrary =
            Path.Combine(
                _root,
                "paperroute-library"
            )

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub Schema4_MigratesToSchema5WithoutRewritingData()

        Dim dataDirectory As String =
            CreateDataDirectory()

        Dim manuscriptsPath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        Dim manuscript As New Manuscript With {
            .Title = "Schema 5 migration"
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Submission candidate"
        }

        manuscript.Versions.Add(version)

        Dim readiness As New ManuscriptReadiness With {
            .JournalName = "Journal of Schema Five"
        }

        readiness.Items.Add(
            New ReadinessItemState With {
                .Title = "Cover letter",
                .Status = ReadinessItemStatus.Complete
            }
        )

        manuscript.ReadinessProfiles.Add(readiness)

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ReadinessProfileId = readiness.Id,
                .JournalName = readiness.JournalName,
                .ManuscriptVersionId = version.Id,
                .Label = "Prepared packet"
            }
        )

        Dim originalManuscripts As String =
            JsonSerializer.Serialize(
                New List(Of Manuscript) From {
                    manuscript
                },
                CreateJsonOptions()
            )

        File.WriteAllText(
            manuscriptsPath,
            originalManuscripts
        )

        Dim library As New AuthorLibraryData()

        Dim journal As New JournalRecord With {
            .Name = "Journal of Schema Five"
        }

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Title = "Cover letter",
                .SortOrder = 10
            }
        )

        library.Journals.Add(journal)

        Dim authorsPath As String =
            Path.Combine(
                dataDirectory,
                "authors.json"
            )

        Dim originalAuthors As String =
            JsonSerializer.Serialize(
                library,
                CreateJsonOptions()
            )

        File.WriteAllText(
            authorsPath,
            originalAuthors
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":4,""UpdatedAtUtc"":""2026-08-27T00:00:00.0000000Z""}"

        File.WriteAllText(
            schemaPath,
            originalSchema
        )

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.AreEqual(
            StorageMigrationService.CurrentSchemaVersion,
            StorageMigrationService.ReadSchemaVersion(
                schemaPath
            )
        )

        Assert.AreEqual(
            originalManuscripts,
            File.ReadAllText(
                manuscriptsPath
            )
        )

        Assert.AreEqual(
            originalAuthors,
            File.ReadAllText(
                authorsPath
            )
        )

        Dim backupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v4.bak"
            )

        Assert.IsTrue(
            File.Exists(
                backupPath
            )
        )

        Assert.AreEqual(
            originalSchema,
            File.ReadAllText(
                backupPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema4_InvalidPacketReferenceDoesNotUpgradeSchema()

        Dim dataDirectory As String =
            CreateDataDirectory()

        Dim manuscript As New Manuscript With {
            .Title = "Invalid packet"
        }

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ManuscriptVersionId = Guid.NewGuid(),
                .Label = "Broken packet"
            }
        )

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            ),
            JsonSerializer.Serialize(
                New List(Of Manuscript) From {
                    manuscript
                },
                CreateJsonOptions()
            )
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":4,""UpdatedAtUtc"":""2026-08-27T00:00:00.0000000Z""}"

        File.WriteAllText(
            schemaPath,
            originalSchema
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()

                StorageMigrationService.EnsureCurrentStorage(
                    _currentData,
                    _legacyData,
                    _currentLibrary,
                    _legacyLibrary
                )

            End Sub
        )

        Assert.AreEqual(
            originalSchema,
            File.ReadAllText(
                schemaPath
            )
        )

        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v4.bak"
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema4_InvalidJournalTemplateDoesNotUpgradeSchema()

        Dim dataDirectory As String =
            CreateDataDirectory()

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            ),
            "[]"
        )

        Dim journal As New JournalRecord With {
            .Name = "Broken checklist journal"
        }

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Id = Guid.Empty,
                .Title = "Invalid item"
            }
        )

        Dim library As New AuthorLibraryData()

        library.Journals.Add(
            journal
        )

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "authors.json"
            ),
            JsonSerializer.Serialize(
                library,
                CreateJsonOptions()
            )
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":4,""UpdatedAtUtc"":""2026-08-27T00:00:00.0000000Z""}"

        File.WriteAllText(
            schemaPath,
            originalSchema
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()

                StorageMigrationService.EnsureCurrentStorage(
                    _currentData,
                    _legacyData,
                    _currentLibrary,
                    _legacyLibrary
                )

            End Sub
        )

        Assert.AreEqual(
            originalSchema,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema1_SequentialMigrationCreatesSchema4Backup()

        Dim dataDirectory As String =
            CreateDataDirectory()

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            ),
            "[]"
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        File.WriteAllText(
            schemaPath,
            "{""SchemaVersion"":1}"
        )

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.AreEqual(
            StorageMigrationService.CurrentSchemaVersion,
            StorageMigrationService.ReadSchemaVersion(
                schemaPath
            )
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v1.bak"
                )
            )
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v2.bak"
                )
            )
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v3.bak"
                )
            )
        )

        Dim schema4BackupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v4.bak"
            )

        Assert.IsTrue(
            File.Exists(
                schema4BackupPath
            )
        )

        Assert.AreEqual(
            4,
            StorageMigrationService.ReadSchemaVersion(
                schema4BackupPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema4_LegacyManuscriptWithoutReadinessLoadsWithEmptyCollections()

        Dim dataDirectory As String =
            CreateDataDirectory()

        Dim manuscriptsPath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        Const legacyJson As String =
            "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Legacy v0.3 manuscript"",""CurrentStage"":1,""Location"":0,""StageEnteredDate"":""2026-08-01T00:00:00"",""History"":[],""Submissions"":[]}]"

        File.WriteAllText(
            manuscriptsPath,
            legacyJson
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        File.WriteAllText(
            schemaPath,
            "{""SchemaVersion"":4}"
        )

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.AreEqual(
            legacyJson,
            File.ReadAllText(
                manuscriptsPath
            )
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            _currentLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            0,
            loaded(0).ReadinessProfiles.Count
        )

        Assert.AreEqual(
            0,
            loaded(0).SubmissionPackets.Count
        )

    End Sub


    Private Function CreateDataDirectory() As String

        Dim dataDirectory As String =
            Path.Combine(
                _currentData,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Return dataDirectory

    End Function

End Class
