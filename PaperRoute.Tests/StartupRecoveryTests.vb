Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Startup with a damaged primary beside a good copy (#124): the migration
' check accepts a readable manuscripts.bak, and the previous save or a
' backup archive can be put in place with no library loaded.
<TestClass>
Public Class StartupRecoveryTests

    Private _root As String
    Private _current As String
    Private _data As String
    Private _schema As String
    Private _library As String


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _current = Path.Combine(_root, "current")
        _data = Path.Combine(_current, "data")
        _library = Path.Combine(_root, "library")
        Directory.CreateDirectory(_data)
        _schema = StorageMigrationService.SchemaFilePath(_current)
        File.WriteAllText(_schema, "{""SchemaVersion"":8}")

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' A pending upgrade met a primary it could not read and stopped, although
    ' a good manuscripts.bak sat beside it. The check accepts the backup, and
    ' the load that follows recovers from it as it does under a current marker.
    <TestMethod>
    Public Sub Upgrade_DamagedPrimaryWithValidBackupEndsAtTheCurrentSchemaWithTheRecoveredLibrary()

        Dim repository As ManuscriptRepository = SaveTwice("Recovered from the previous save")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")

        EnsureStorage()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{ damaged", File.ReadAllText(Path.Combine(_data, "manuscripts.json")), "The check changes no data.")

        Dim loaded As List(Of Manuscript) = repository.Load()
        Assert.IsTrue(repository.LastLoadRecoveredFromBackup, "The normal recovery takes over.")
        Assert.AreEqual("Recovered from the previous save", loaded.Single().Title)
        Assert.IsTrue(File.Exists(repository.LastRecoveryPreservedFilePath), "The unreadable file is kept.")

    End Sub


    ' Guard: with no readable copy the upgrade still stops and changes
    ' nothing. It passed before the change as well.
    <TestMethod>
    Public Sub Upgrade_DamagedPrimaryAndBackupStillStopsWithoutAdvancingSchema()

        SaveTwice("Lost to both files")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(Path.Combine(_data, "manuscripts.bak"), "[null]")

        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())
        Assert.AreEqual(8, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{ damaged", File.ReadAllText(Path.Combine(_data, "manuscripts.json")))
        Assert.AreEqual("[null]", File.ReadAllText(Path.Combine(_data, "manuscripts.bak")))

    End Sub


    ' An empty schema.json stops startup before any library file is read.
    ' With a readable manuscripts.bak the previous save can be offered, and
    ' the offer itself changes nothing; taking it puts the previous save in
    ' place under a current marker, and the library opens.
    <TestMethod>
    Public Sub EmptySchema_OffersThePreviousSaveWithoutTouchingData()

        Dim repository As ManuscriptRepository = SaveTwice("The previous save")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(_schema, String.Empty)
        Dim before As Dictionary(Of String, Byte()) = SnapshotData()

        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage(), "An empty marker still stops startup.")

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsTrue(recovery.PreviousSaveIsValid(), "The previous save is offered.")
        AssertSameData(before, "The offer touches no data.")

        Dim preserved As String = recovery.UsePreviousSave()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual(String.Empty, File.ReadAllText(Path.Combine(_data, "schema.previous.bak")), "The marker it replaced is kept.")
        Assert.AreEqual(repository.LastRecoveryPreservedFilePath, preserved, "The window is told where the replaced file went.")
        StringAssert.StartsWith(preserved, Path.Combine(_data, "recovery"), "The unreadable primary is kept under data\recovery.")
        Assert.AreEqual("{ damaged", File.ReadAllText(preserved))

        EnsureStorage()
        Assert.AreEqual("The previous save", repository.Load().Single().Title, "The library opens.")
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup, "As the primary, with no fallback needed.")

    End Sub


    ' A marker from a newer PaperRoute: the previous save reads with this
    ' build's rules only because it skips what it does not know, so it is not
    ' offered, and asking anyway changes nothing. The marker stays for the
    ' newer PaperRoute.
    <TestMethod>
    Public Sub NewerMarker_IsNotOfferedThePreviousSaveAndNothingChanges()

        Dim repository As ManuscriptRepository = SaveTwice("Saved by a newer PaperRoute")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(_schema, "{""SchemaVersion"":10}")
        Dim before As Dictionary(Of String, Byte()) = SnapshotData()

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsFalse(recovery.PreviousSaveIsValid(), "Not offered.")

        Dim failure As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.UsePreviousSave())
        StringAssert.Contains(failure.Message, "Update PaperRoute")
        AssertSameData(before, "Nothing was changed.")

    End Sub


    ' A readable marker at or below the current version is left as it is:
    ' the next start's upgrade runs from there over the recovered primary,
    ' instead of being skipped by a stamped marker.
    <TestMethod>
    Public Sub OlderMarker_UsePreviousSaveLeavesTheMarkerForTheNextStartToUpgrade()

        Dim repository As ManuscriptRepository = SaveTwice("The previous save")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsTrue(recovery.PreviousSaveIsValid())

        recovery.UsePreviousSave()

        Assert.AreEqual(8, StorageMigrationService.ReadSchemaVersion(_schema), "The marker is left for the upgrade.")
        Assert.IsFalse(File.Exists(Path.Combine(_data, "schema.previous.bak")))
        Assert.AreEqual("{ damaged", File.ReadAllText(repository.LastRecoveryPreservedFilePath))

        EnsureStorage()
        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("The previous save", repository.Load().Single().Title, "The upgrade ran over the recovered primary.")
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup)

    End Sub


    ' With no readable copy beside it, the previous save is not offered, and
    ' asking for it anyway changes nothing.
    <TestMethod>
    Public Sub DamagedBackup_IsNotOfferedAsThePreviousSave()

        SaveTwice("Lost to both files")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(Path.Combine(_data, "manuscripts.bak"), "[null]")

        Dim recovery As New StartupRecoveryService(New ManuscriptRepository(_data, _library), _library, _schema)
        Assert.IsFalse(recovery.PreviousSaveIsValid())

        Assert.ThrowsExactly(Of InvalidDataException)(Sub() recovery.UsePreviousSave())
        Assert.AreEqual("{ damaged", File.ReadAllText(Path.Combine(_data, "manuscripts.json")), "Nothing was changed.")
        Assert.AreEqual("[null]", File.ReadAllText(Path.Combine(_data, "manuscripts.bak")))
        Assert.AreEqual(8, StorageMigrationService.ReadSchemaVersion(_schema))

    End Sub


    ' A restore started from the startup window: no library is loaded, so
    ' the emergency copy is of the files as they are, safety backup and
    ' marker included, and the marker the library was under is rewritten.
    ' The library then opens.
    <TestMethod>
    Public Sub RestoreFromBackup_WithNoLibraryLoadedEndsWithALibraryThatOpens()

        Dim sourceData As String = Path.Combine(_root, "source-data")
        Dim sourceLibrary As String = Path.Combine(_root, "source-library")
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceLibrary)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        sourceRepository.Save(sourceManuscripts)
        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Call New PortableBackupService(sourceLibrary).CreateBackup(backupPath, sourceManuscripts, sourceRepository)

        ' Neither the primary nor the safety backup reads, under a pending
        ' upgrade, and a managed file is on disk.
        Dim repository As ManuscriptRepository = SaveTwice("Lost to both files")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(Path.Combine(_data, "manuscripts.bak"), "[null]")
        Dim managedFile As String = Path.Combine(_library, "stray", "note.txt")
        Directory.CreateDirectory(Path.GetDirectoryName(managedFile))
        File.WriteAllText(managedFile, "A managed file.")
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() EnsureStorage())

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsFalse(recovery.PreviousSaveIsValid())

        Dim result As RestoreResult = recovery.RestoreFromBackup(backupPath)

        Assert.AreEqual(3, result.ManuscriptCount)
        Using archive As ZipArchive = ZipFile.OpenRead(result.EmergencyBackupPath)
            Dim names As List(Of String) = archive.Entries.Select(Function(entry) entry.FullName.Replace("\", "/")).ToList()
            CollectionAssert.IsSubsetOf({"manuscripts.json", "manuscripts.bak", "schema.json", "files/stray/note.txt"}, names, "The emergency copy holds the files as they were.")
        End Using
        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("{""SchemaVersion"":8}", File.ReadAllText(Path.Combine(_data, "schema.previous.bak")), "The marker it restored over is kept.")

        EnsureStorage()
        Assert.AreEqual(3, repository.Load().Count, "The library opens.")
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup)
        Assert.IsFalse(File.Exists(managedFile), "The restored managed library took the old one's place.")

    End Sub


    ' A library from a newer PaperRoute is not damage, and updating
    ' PaperRoute is the fix: a restore asked for anyway stops with the
    ' message startup gave, before anything changes. Before the change it
    ' replaced the newer library and stamped this build's marker on it.
    <TestMethod>
    Public Sub NewerMarker_RestoreFromBackupIsRefusedAndNothingChanges()

        Dim backupPath As String = CreateBackup()
        Dim repository As ManuscriptRepository = SaveTwice("Saved by a newer PaperRoute")
        File.WriteAllText(_schema, "{""SchemaVersion"":10}")
        Dim before As Dictionary(Of String, Byte()) = SnapshotData()

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsTrue(recovery.LibraryIsFromANewerPaperRoute())

        Dim failure As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.RestoreFromBackup(backupPath))
        StringAssert.Contains(failure.Message, "Update PaperRoute")
        AssertSameData(before, "Nothing was changed.")
        Assert.IsFalse(Directory.Exists(Path.Combine(_data, "backups")), "Not even an emergency copy was made.")

    End Sub


    ' A backup from a newer storage schema, chosen in the startup window, is
    ' refused before anything changes (#115): the window's check and its
    ' restore both stop with the Update PaperRoute message, and every file
    ' and folder under the test root is as it was. The marker here is older,
    ' so only the backup's own version stops it. Before the change both
    ' went ahead and the restore replaced the library.
    <TestMethod>
    Public Sub ANewerBackup_IsRefusedBeforeAnythingChanges()

        Dim backupPath As String = CreateBackup()

        ' Schema 10, while this build is at 9.
        SetBackupManifest(backupPath, BackupManifestJson(StorageMigrationService.CurrentSchemaVersion + 1))

        Dim repository As ManuscriptRepository = SaveTwice("The library before the restore")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        Dim before As Dictionary(Of String, Byte()) = SnapshotTree(_root)

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)
        Assert.IsFalse(recovery.LibraryIsFromANewerPaperRoute())

        Dim inspect As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.InspectBackup(backupPath))
        StringAssert.Contains(inspect.Message, "Update PaperRoute")

        Dim restore As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.RestoreFromBackup(backupPath))
        StringAssert.Contains(restore.Message, "Update PaperRoute")

        AssertSameTree(before, _root, "Nothing was changed")

    End Sub


    ' A marker that cannot be written once the library step has changed
    ' the library must not read as a recovery that changed nothing: the
    ' message says what is in place and where the replaced files are, and
    ' carries the cause. Here schema.json's temporary sibling is a folder.
    <TestMethod>
    Public Sub AMarkerThatCannotBeWritten_SaysWhatIsAlreadyInPlace()

        Dim repository As ManuscriptRepository = SaveTwice("The previous save")
        File.WriteAllText(Path.Combine(_data, "manuscripts.json"), "{ damaged")
        File.WriteAllText(_schema, String.Empty)
        Directory.CreateDirectory(_schema & ".tmp")

        Dim recovery As New StartupRecoveryService(repository, _library, _schema)

        Dim previous As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.UsePreviousSave())
        StringAssert.StartsWith(previous.Message, "The previous save is in place")
        StringAssert.Contains(previous.Message, repository.LastRecoveryPreservedFilePath)
        StringAssert.Contains(previous.Message, "storage marker")
        Assert.IsNotNull(previous.InnerException)
        Assert.AreEqual("The previous save", repository.Load().Single().Title, "The previous save is in place.")

        Dim restore As InvalidOperationException = Assert.ThrowsExactly(Of InvalidOperationException)(Sub() recovery.RestoreFromBackup(CreateBackup()))
        StringAssert.StartsWith(restore.Message, "The backup was restored")
        StringAssert.Contains(restore.Message, Directory.GetFiles(Path.Combine(_data, "backups"), "PaperRoute_PreRestore_*.zip").Single())
        StringAssert.Contains(restore.Message, "storage marker")
        Assert.IsNotNull(restore.InnerException)
        Assert.AreEqual(3, repository.Load().Count, "The backup is in place.")

    End Sub


    ' A backup of the three-manuscript library, made elsewhere under the root.
    Private Function CreateBackup() As String

        Dim sourceData As String = Path.Combine(_root, "source-data-" & Guid.NewGuid().ToString("N"))
        Dim sourceLibrary As String = Path.Combine(_root, "source-library-" & Guid.NewGuid().ToString("N"))
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceLibrary)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        sourceRepository.Save(sourceManuscripts)
        Dim backupPath As String = Path.Combine(_root, "backup-" & Guid.NewGuid().ToString("N") & ".zip")
        Call New PortableBackupService(sourceLibrary).CreateBackup(backupPath, sourceManuscripts, sourceRepository)
        Return backupPath

    End Function


    Private Function SnapshotData() As Dictionary(Of String, Byte())

        Dim files As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)

        For Each filePath As String In Directory.GetFiles(_data, "*", SearchOption.AllDirectories)
            files(filePath) = File.ReadAllBytes(filePath)
        Next

        Return files

    End Function


    Private Sub AssertSameData(expected As Dictionary(Of String, Byte()), message As String)

        Dim actual As Dictionary(Of String, Byte()) = SnapshotData()
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message)

        For Each pair As KeyValuePair(Of String, Byte()) In expected
            CollectionAssert.AreEqual(pair.Value, actual(pair.Key), message & " (" & Path.GetFileName(pair.Key) & ")")
        Next

    End Sub


    ' Two saves, so manuscripts.bak holds the library as well.
    Private Function SaveTwice(title As String) As ManuscriptRepository

        Dim repository As New ManuscriptRepository(_data, _library)
        Dim library As New List(Of Manuscript) From {New Manuscript With {.Title = title}}
        repository.Save(library)
        repository.Save(library)
        Assert.IsTrue(File.Exists(Path.Combine(_data, "manuscripts.bak")))
        Return repository

    End Function


    Private Sub EnsureStorage()
        StorageMigrationService.EnsureCurrentStorage(_current, Path.Combine(_root, "legacy"), _library, Path.Combine(_root, "legacy-library"))
    End Sub

End Class
