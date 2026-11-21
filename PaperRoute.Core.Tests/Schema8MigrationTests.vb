Imports System
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Schema 8 (#64) adds work types, tags, and tag colors. Upgrading changes
' only the marker; records load with no type and no tags.
<TestClass>
Public Class Schema8MigrationTests
    Private _root As String
    Private _current As String
    Private _data As String
    Private _schema As String

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _current = Path.Combine(_root, "current")
        _data = Path.Combine(_current, "data")
        Directory.CreateDirectory(_data)
        _schema = StorageMigrationService.SchemaFilePath(_current)
        File.WriteAllText(_schema, "{""SchemaVersion"":7}")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub Upgrade_PreservesOlderBytesAndLoadsWithoutTypeOrTags()
        Dim original = "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Old manuscript""}]"
        Dim authors = "{""Journals"":[]}"
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        File.WriteAllText(Path.Combine(_data, "authors.json"), authors)

        EnsureStorage()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{""SchemaVersion"":7}", File.ReadAllText(Path.Combine(_data, "schema.v7.bak")))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
        Assert.AreEqual(authors, File.ReadAllText(Path.Combine(_data, "authors.json")))
        Dim loaded As Manuscript = New ManuscriptRepository(_data, Path.Combine(_root, "library")).Load().Single()
        Assert.AreEqual(WorkType.Unspecified, loaded.WorkType, "No type is inferred.")
        Assert.AreEqual(0, loaded.Tags.Count)
        Assert.AreEqual(0, New AuthorLibraryRepository(_data).Load().TagColors.Count)
    End Sub

    <TestMethod>
    Public Sub Upgrade_RejectsAnUnknownTypeWithoutAdvancingSchema()
        Dim original = "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Odd"",""WorkType"":99}]"
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(7, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
    End Sub

    <TestMethod>
    Public Sub FutureSchema_IsRejected()
        File.WriteAllText(_schema, "{""SchemaVersion"":10}")
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() EnsureStorage())
        Assert.AreEqual(10, StorageMigrationService.ReadSchemaVersion(_schema))
    End Sub

    Private Sub EnsureStorage()
        StorageMigrationService.EnsureCurrentStorage(_current, Path.Combine(_root, "legacy"), Path.Combine(_root, "library"), Path.Combine(_root, "legacy-library"))
    End Sub
End Class
