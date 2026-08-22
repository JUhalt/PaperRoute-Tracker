Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class Schema3MigrationTests

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
    Public Sub Schema2_MigratesToSchema3WithoutRewritingManuscripts()

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

        Dim originalManuscripts As String =
            JsonSerializer.Serialize(
                CreateRepresentativeLibrary(),
                CreateJsonOptions()
            )

        File.WriteAllText(
            manuscriptsPath,
            originalManuscripts
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":2,""UpdatedAtUtc"":""2026-08-22T00:00:00.0000000Z""}"

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
            3,
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

        Dim schemaBackupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v2.bak"
            )

        Assert.IsTrue(
            File.Exists(
                schemaBackupPath
            )
        )

        Assert.AreEqual(
            originalSchema,
            File.ReadAllText(
                schemaBackupPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema1_MigratesSequentiallyAndPreservesBothSchemaBackups()

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

        Dim originalManuscripts As String =
            JsonSerializer.Serialize(
                CreateRepresentativeLibrary(),
                CreateJsonOptions()
            )

        File.WriteAllText(
            manuscriptsPath,
            originalManuscripts
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":1,""UpdatedAtUtc"":""2026-08-22T00:00:00.0000000Z""}"

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
            3,
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

        Dim schema1BackupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v1.bak"
            )

        Dim schema2BackupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v2.bak"
            )

        Assert.AreEqual(
            originalSchema,
            File.ReadAllText(
                schema1BackupPath
            )
        )

        Assert.AreEqual(
            2,
            StorageMigrationService.ReadSchemaVersion(
                schema2BackupPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema2_InvalidManuscriptDataDoesNotUpgradeSchema()

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

        Const invalidManuscripts As String =
            "{ definitely not valid json"

        File.WriteAllText(
            manuscriptsPath,
            invalidManuscripts
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Const originalSchema As String =
            "{""SchemaVersion"":2,""UpdatedAtUtc"":""2026-08-22T00:00:00.0000000Z""}"

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
            invalidManuscripts,
            File.ReadAllText(
                manuscriptsPath
            )
        )

        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    dataDirectory,
                    "schema.v2.bak"
                )
            )
        )

    End Sub

End Class
