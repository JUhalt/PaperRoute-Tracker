Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Schema 7 (#61) remembers publication matches. Upgrading changes only the
' marker, and a library that does not validate is left exactly as it was.
<TestClass>
Public Class Schema7MigrationTests
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
        File.WriteAllText(_schema, "{""SchemaVersion"":6}")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Upgrade_PreservesOlderBytesAndLoadsNoMatches(explicitNull As Boolean)
        Dim suffix = If(explicitNull, ",""PublicationMatches"":null", String.Empty)
        Dim original = "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Old manuscript""" & suffix & "}]"
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), original)
        File.WriteAllText(Path.Combine(_data, "manuscripts.bak"), "preserve existing automatic backup")
        File.WriteAllText(Path.Combine(_data, "authors.json"), "{""Journals"":[]}")

        EnsureStorage()

        Assert.AreEqual(7, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{""SchemaVersion"":6}", File.ReadAllText(Path.Combine(_data, "schema.v6.bak")))
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
        Assert.AreEqual("preserve existing automatic backup", File.ReadAllText(Path.Combine(_data, "manuscripts.bak")))
        Dim repository As New ManuscriptRepository(_data, Path.Combine(_root, "library"))
        Assert.AreEqual(0, repository.Load()(0).PublicationMatches.Count)

        Dim afterFirstUpgrade = File.ReadAllText(_schema)
        EnsureStorage()
        Assert.AreEqual(afterFirstUpgrade, File.ReadAllText(_schema), "Upgrading twice changes nothing.")
    End Sub

    <TestMethod>
    <DataRow("manuscripts.json", "not json")>
    <DataRow("manuscripts.json", "null")>
    <DataRow("manuscripts.json", "[null]")>
    <DataRow("authors.json", "null")>
    Public Sub Upgrade_RejectsInvalidExistingDataWithoutAdvancingSchema(fileName As String, data As String)
        File.WriteAllText(Path.Combine(_data, fileName), data)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(6, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(data, File.ReadAllText(Path.Combine(_data, fileName)))
        Assert.IsFalse(File.Exists(Path.Combine(_data, "schema.v6.bak")))
    End Sub

    <TestMethod>
    Public Sub MatchesRoundTripAndDuplicatesAreRejected()
        Dim manuscript As New Manuscript With {.Title = "Anchoring replication"}
        Dim match As New PublicationMatch With {
            .Doi = "10.1234/abc", .Title = "Anchoring replication", .Journal = "Collabra",
            .Status = PublicationMatchStatus.Ignored, .Source = PublicationMatchSource.Orcid
        }
        manuscript.PublicationMatches.Add(match)
        File.WriteAllText(_schema, "{""SchemaVersion"":7}")
        Dim repository As New ManuscriptRepository(_data, Path.Combine(_root, "library"))
        repository.Save(New List(Of Manuscript) From {manuscript})

        Dim loaded As PublicationMatch = repository.Load()(0).PublicationMatches.Single()
        Assert.AreEqual(match.Id, loaded.Id)
        Assert.AreEqual(PublicationMatchStatus.Ignored, loaded.Status, "An ignored match stays ignored.")
        Assert.AreEqual(PublicationMatchSource.Orcid, loaded.Source)

        manuscript.PublicationMatches.Add(New PublicationMatch With {.Id = match.Id})
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() repository.Save(New List(Of Manuscript) From {manuscript}))
        Assert.AreEqual(1, repository.Load()(0).PublicationMatches.Count, "A rejected save leaves the library as it was.")
    End Sub

    Private Sub EnsureStorage()
        StorageMigrationService.EnsureCurrentStorage(_current, Path.Combine(_root, "legacy"), Path.Combine(_root, "library"), Path.Combine(_root, "legacy-library"))
    End Sub
End Class
