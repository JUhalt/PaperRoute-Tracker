Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class Schema4MigrationTests

    Private _root As String = String.Empty
    Private _legacyData As String = String.Empty
    Private _currentData As String = String.Empty
    Private _legacyLibrary As String = String.Empty
    Private _currentLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteSchema4_" &
                Guid.NewGuid().ToString("N")
            )

        _legacyData =
            Path.Combine(_root, "legacy-data")

        _currentData =
            Path.Combine(_root, "paperroute-data")

        _legacyLibrary =
            Path.Combine(_root, "legacy-library")

        _currentLibrary =
            Path.Combine(_root, "paperroute-library")

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        If Directory.Exists(_root) Then
            Directory.Delete(_root, True)
        End If

    End Sub


    <TestMethod>
    Public Sub Schema3_MigratesToSchema4WithoutRewritingManuscripts()

        Dim dataDirectory As String =
            Path.Combine(
                _currentData,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Dim manuscriptsPath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        Const legacyJson As String =
            "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Legacy provenance"",""CurrentStage"":1,""Location"":0,""StageEnteredDate"":""2022-03-10T00:00:00"",""History"":[{""Id"":""22222222-2222-2222-2222-222222222222"",""EventDate"":""2022-03-10T00:00:00"",""Stage"":1,""Note"":""Legacy""}],""Submissions"":[]}]"

        File.WriteAllText(
            manuscriptsPath,
            legacyJson
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":3,""UpdatedAtUtc"":""2026-08-23T00:00:00.0000000Z""}"

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
            legacyJson,
            File.ReadAllText(
                manuscriptsPath
            )
        )

        Dim backupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v3.bak"
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

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            _currentLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsFalse(
            loaded(0).History(0).RecordedAtUtc.HasValue
        )

        Assert.IsFalse(
            loaded(0).History(0).LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub Schema3_InvalidManuscriptDataDoesNotUpgradeSchema()

        Dim dataDirectory As String =
            Path.Combine(
                _currentData,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Dim manuscriptsPath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        Const invalidJson As String =
            "{ definitely not valid json"

        File.WriteAllText(
            manuscriptsPath,
            invalidJson
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":3,""UpdatedAtUtc"":""2026-08-23T00:00:00.0000000Z""}"

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

        Assert.AreEqual(
            invalidJson,
            File.ReadAllText(
                manuscriptsPath
            )
        )

        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v3.bak"
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema1_SequentialMigrationCreatesSchema3Backup()

        Dim dataDirectory As String =
            Path.Combine(
                _currentData,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Dim manuscriptsPath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        File.WriteAllText(
            manuscriptsPath,
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

    End Sub

End Class
