Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class MigrationTests

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
    Public Sub Migration_CopiesDataAndPreservesLegacySource()

        WriteLegacyLibrary()

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    _currentData,
                    "data",
                    "manuscripts.json"
                )
            )
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    _legacyData,
                    "data",
                    "manuscripts.json"
                )
            )
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    _currentData,
                    "migration.json"
                )
            )
        )

        Assert.AreEqual(
            StorageMigrationService.CurrentSchemaVersion,
            StorageMigrationService.ReadSchemaVersion(
                StorageMigrationService.SchemaFilePath(
                    _currentData
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Migration_CopiesManagedFilesAndRewritesCorrespondencePath()

        Dim legacyFile As String =
            WriteLegacyLibrary()

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    _currentLibrary,
                    "decision-letter.txt"
                )
            )
        )

        Assert.IsTrue(
            File.Exists(
                legacyFile
            )
        )

        Dim repository As New ManuscriptRepository(
            Path.Combine(
                _currentData,
                "data"
            ),
            _currentLibrary
        )

        Dim migrated As List(Of Manuscript) =
            repository.Load()

        Dim migratedPath As String =
            migrated(0).
                Submissions(0).
                Correspondence(0).
                LocalFilePath

        Assert.IsTrue(
            Path.GetFullPath(
                migratedPath
            ).StartsWith(
                Path.GetFullPath(
                    _currentLibrary
                ),
                StringComparison.OrdinalIgnoreCase
            )
        )

    End Sub


    <TestMethod>
    Public Sub Migration_InvalidLegacyJsonDoesNotDestroyLegacySource()

        Dim legacyDataDirectory As String =
            Path.Combine(
                _legacyData,
                "data"
            )

        Directory.CreateDirectory(
            legacyDataDirectory
        )

        Dim legacyPath As String =
            Path.Combine(
                legacyDataDirectory,
                "manuscripts.json"
            )

        Const original As String =
            "{ definitely not json"

        File.WriteAllText(
            legacyPath,
            original
        )

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub()

                StorageMigrationService.EnsureCurrentStorage(
                    _currentData,
                    _legacyData,
                    _currentLibrary,
                    _legacyLibrary
                )

            End Sub
        )

        Assert.IsTrue(
            File.Exists(
                legacyPath
            )
        )

        Assert.AreEqual(
            original,
            File.ReadAllText(
                legacyPath
            )
        )

        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    _currentData,
                    "data",
                    "manuscripts.json"
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Migration_RejectsFutureSchemaAndPreservesFile()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{""SchemaVersion"":999}"

        File.WriteAllText(
            schemaPath,
            original
        )

        Assert.ThrowsExactly(Of InvalidOperationException)(
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
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema1_MigratesToCurrentSchemaWithoutRewritingManuscripts()

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
            "{""SchemaVersion"":1,""UpdatedAtUtc"":""2026-08-20T00:00:00.0000000Z""}"

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

        Dim schemaBackupPath As String =
            Path.Combine(
                dataDirectory,
                "schema.v1.bak"
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
    Public Sub Schema1_InvalidManuscriptDataDoesNotUpgradeSchema()

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
            "{""SchemaVersion"":1,""UpdatedAtUtc"":""2026-08-20T00:00:00.0000000Z""}"

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
                    "schema.v1.bak"
                )
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_MissingFileCreatesCurrentSchema()

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Assert.IsTrue(
            File.Exists(
                schemaPath
            )
        )

        Assert.AreEqual(
            StorageMigrationService.CurrentSchemaVersion,
            StorageMigrationService.ReadSchemaVersion(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_CurrentVersionIsAcceptedWithoutRewrite()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{""SchemaVersion"":3,""UpdatedAtUtc"":""2000-01-01T00:00:00.0000000Z""}"

        File.WriteAllText(
            schemaPath,
            original
        )

        StorageMigrationService.EnsureCurrentStorage(
            _currentData,
            _legacyData,
            _currentLibrary,
            _legacyLibrary
        )

        Assert.AreEqual(
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_MalformedJsonIsRejectedAndPreserved()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{ definitely not valid json"

        File.WriteAllText(
            schemaPath,
            original
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
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_MissingVersionIsRejectedAndPreserved()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{""UpdatedAtUtc"":""2026-08-19T00:00:00Z""}"

        File.WriteAllText(
            schemaPath,
            original
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
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_NonNumericVersionIsRejectedAndPreserved()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{""SchemaVersion"":""banana""}"

        File.WriteAllText(
            schemaPath,
            original
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
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_ZeroVersionIsRejectedAndPreserved()

        Dim schemaPath As String =
            CreateCurrentSchemaDirectory()

        Const original As String =
            "{""SchemaVersion"":0}"

        File.WriteAllText(
            schemaPath,
            original
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
            original,
            File.ReadAllText(
                schemaPath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Schema_MissingMetadataWithExistingLibraryAdoptsCurrentSchema()

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

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim originalJson As String =
            JsonSerializer.Serialize(
                manuscripts,
                CreateJsonOptions()
            )

        File.WriteAllText(
            manuscriptsPath,
            originalJson
        )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                _currentData
            )

        Assert.IsFalse(
            File.Exists(
                schemaPath
            )
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
            originalJson,
            File.ReadAllText(
                manuscriptsPath
            )
        )

    End Sub


    Private Function CreateCurrentSchemaDirectory() As String

        Dim dataDirectory As String =
            Path.Combine(
                _currentData,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Return Path.Combine(
            dataDirectory,
            "schema.json"
        )

    End Function


    Private Function WriteLegacyLibrary() As String

        Dim legacyDataDirectory As String =
            Path.Combine(
                _legacyData,
                "data"
            )

        Directory.CreateDirectory(
            legacyDataDirectory
        )

        Directory.CreateDirectory(
            _legacyLibrary
        )

        Dim legacyFile As String =
            Path.Combine(
                _legacyLibrary,
                "decision-letter.txt"
            )

        File.WriteAllText(
            legacyFile,
            "Synthetic reviewer/editor correspondence fixture."
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        manuscripts(0).
            Submissions(0).
            Correspondence.Add(
                New CorrespondenceItem With {
                    .ItemDate =
                        New DateTime(
                            2026,
                            7,
                            10
                        ),
                    .Type =
                        CorrespondenceType.DecisionLetter,
                    .Title =
                        "Synthetic decision letter",
                    .LocalFilePath =
                        legacyFile,
                    .IsManagedCopy =
                        True
                }
            )

        File.WriteAllText(
            Path.Combine(
                legacyDataDirectory,
                "manuscripts.json"
            ),
            JsonSerializer.Serialize(
                manuscripts,
                CreateJsonOptions()
            )
        )

        Return legacyFile

    End Function

End Class