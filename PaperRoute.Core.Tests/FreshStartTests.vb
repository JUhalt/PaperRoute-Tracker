Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' What a first start makes on disk (core stage 2, design 5.2), under an
' empty RootOverride, by the calls the app makes: Program.Run's storage
' step (StorageMigrationService.EnsureCurrentStorage), then Form1_Load's
' reads of the settings, the library, the author library and a recovery
' draft. Nothing is made outside the data root and the managed-library
' root: no legacy folder, nothing beside them. The managed-library root
' itself is not made by a first start; the first managed copy makes it.
<TestClass>
<DoNotParallelize>
Public Class FreshStartTests

    Private _root As String
    Private _sources As String
    Private _previousOverride As String


    <TestInitialize>
    Public Sub Initialize()

        _previousOverride = StorageEnvironment.RootOverride
        _root = CreateTemporaryRoot()
        _sources = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = _root

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        StorageEnvironment.RootOverride = _previousOverride
        DeleteTemporaryRoot(_root)
        DeleteTemporaryRoot(_sources)

    End Sub


    <TestMethod>
    Public Sub AFirstStartMakesTheDataRootAndNothingElse()

        Assert.AreEqual(0, Directory.GetFileSystemEntries(_root).Length, "The root starts empty.")

        Dim repository As ManuscriptRepository = StartAsTheAppDoes()

        CollectionAssert.AreEqual(
            {"current-data", "current-data/data", "current-data/data/schema.json"},
            EntriesUnder(_root),
            String.Join(Environment.NewLine, EntriesUnder(_root)))

        Assert.AreEqual(Path.Combine(_root, "current-data"), StorageLocations.DataRoot())
        Assert.AreEqual(Path.Combine(_root, "managed-library"), StorageLocations.ManagedLibraryRoot())
        Assert.IsFalse(Directory.Exists(StorageLocations.ManagedLibraryRoot()), "A first start makes no managed-library root.")

    End Sub


    ' The first saves after a first start: the settings, then a library
    ' whose one version is kept as a managed copy. The data root takes the
    ' two files, the managed-library root appears with the copy, and the
    ' root holds those two roots and nothing else.
    <TestMethod>
    Public Sub TheFirstSavesMakeTheManagedLibraryRootAndNothingElse()

        Dim repository As ManuscriptRepository = StartAsTheAppDoes()

        Call New AppSettingsService().Save(New AppSettings())

        Dim source As String = Path.Combine(_sources, "draft.docx")
        File.WriteAllText(source, "draft")
        Dim manuscript As New Manuscript With {.Title = "A first manuscript"}
        Dim version As New ManuscriptVersion With {.Label = "Version 1", .LocalFilePath = source, .IsManagedCopy = True}
        manuscript.Versions.Add(version)
        repository.Save(New List(Of Manuscript) From {manuscript})

        Dim versionFolder As String = Path.Combine(
            StorageLocations.ManagedLibraryRoot(), manuscript.Id.ToString("N"), "versions", version.Id.ToString("N"))
        Assert.AreEqual(versionFolder, Path.GetDirectoryName(version.LocalFilePath))
        Assert.AreEqual("draft", File.ReadAllText(version.LocalFilePath))

        Dim mid As String = "managed-library/" & manuscript.Id.ToString("N")
        Dim expected As New List(Of String) From {
            "current-data",
            "current-data/data",
            "current-data/data/manuscripts.json",
            "current-data/data/schema.json",
            "current-data/settings.json",
            "managed-library",
            mid,
            mid & "/versions",
            mid & "/versions/" & version.Id.ToString("N"),
            mid & "/versions/" & version.Id.ToString("N") & "/" & Path.GetFileName(version.LocalFilePath)
        }
        expected.Sort(StringComparer.Ordinal)

        CollectionAssert.AreEqual(
            expected,
            EntriesUnder(_root),
            String.Join(Environment.NewLine, EntriesUnder(_root)))

        CollectionAssert.AreEqual(
            {"current-data", "managed-library"},
            EntriesUnder(_root).Where(Function(entry) Not entry.Contains("/"c)).ToList())

    End Sub


    ' The calls a first start makes, in the app's order. The repository is
    ' made with the folders its default constructor resolves (the data
    ' folder and the managed-library root, which StorageLocationsTests
    ' pins), because the default constructor refuses to start on a system
    ' that reports no Documents folder, such as a Linux CI runner, which
    ' is a question for later and not this test's.
    Private Shared Function StartAsTheAppDoes() As ManuscriptRepository

        StorageMigrationService.EnsureCurrentStorage()

        Dim settings As AppSettings = New AppSettingsService().Load()
        Assert.IsNotNull(settings)

        Dim repository As New ManuscriptRepository(StorageLocations.LibraryDataFolder(), StorageLocations.ManagedLibraryRoot())
        Assert.AreEqual(0, repository.Load().Count)

        Assert.AreEqual(0, New AuthorLibraryRepository().Load().Authors.Count)

        Assert.IsNull(New RecoveryDraftStore(Path.GetDirectoryName(repository.DataFilePath)).Read())

        Return repository

    End Function


    ' Every folder and file under the root, relative and with "/" between
    ' the parts, in ordinal order.
    Private Shared Function EntriesUnder(root As String) As List(Of String)

        Dim entries As List(Of String) =
            Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).
                Select(Function(entry) Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, "/"c)).
                ToList()
        entries.Sort(StringComparer.Ordinal)
        Return entries

    End Function

End Class
