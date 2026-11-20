Imports System
Imports System.IO
Imports System.Reflection
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' StorageLocations answers "where is what" with the expressions the six
' default-path builders used before core stage 2's step 5 (design 4.1 and
' 4.4), and the builders now ask it. Each path is checked against today's
' expression written out, under a RootOverride, so nothing here reads the
' user's folders. The Windows answers themselves are pinned in
' PaperRoute.Tests' StoragePinTests.
<TestClass>
Public Class StorageLocationsTests

    Private _root As String
    Private _previousOverride As String


    <TestInitialize>
    Public Sub Initialize()

        _previousOverride = StorageEnvironment.RootOverride
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = _root

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        StorageEnvironment.RootOverride = _previousOverride
        DeleteTemporaryRoot(_root)

    End Sub


    <TestMethod>
    Public Sub StorageLocationsMatchesTheBuilders()

        ' The expressions each builder used before it called StorageLocations.
        Dim dataRoot As String = StorageMigrationService.CurrentDataRoot()
        Dim libraryData As String = Path.Combine(StorageMigrationService.CurrentDataRoot(), "data")
        Dim logs As String = Path.Combine(StorageMigrationService.CurrentDataRoot(), "logs")
        Dim keys As String = Path.Combine(StorageMigrationService.CurrentDataRoot(), "keys")

        Assert.AreEqual(Path.Combine(_root, "current-data"), dataRoot)

        Assert.AreEqual(dataRoot, StorageLocations.DataRoot())
        Assert.AreEqual(libraryData, StorageLocations.LibraryDataFolder())
        Assert.AreEqual(dataRoot, StorageLocations.SettingsFolder())
        Assert.AreEqual(logs, StorageLocations.LogsFolder())
        Assert.AreEqual(keys, StorageLocations.KeysFolder())
        Assert.AreEqual(
            StorageMigrationService.CurrentManagedLibraryRoot(),
            StorageLocations.ManagedLibraryRoot())
        Assert.AreEqual(Path.Combine(_root, "managed-library"), StorageLocations.ManagedLibraryRoot())

        ' What the six builders produce through StorageLocations.
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(libraryData), "manuscripts.json"),
            New ManuscriptRepository().DataFilePath)
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(libraryData), "authors.json"),
            New AuthorLibraryRepository().DataFilePath)
        Assert.AreEqual(
            Path.Combine(libraryData, CitationStore.FileName),
            New CitationStore().DataFilePath)
        Assert.AreEqual(
            Path.Combine(dataRoot, "settings.json"),
            PrivateField(New AppSettingsService(), "_settingsPath"))
        Assert.AreEqual(logs, ErrorReportService.DefaultDirectory())
        Assert.AreEqual(
            Path.GetFullPath(logs),
            PrivateField(New ErrorReportService(Function() "test"), "_directory"))
        Assert.AreEqual(
            keys,
            PrivateField(New ProtectedKeyStore(), "_directory"))

    End Sub


    Private Shared Function PrivateField(instance As Object, name As String) As String

        Dim field As FieldInfo =
            instance.GetType().GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
        Assert.IsNotNull(field, $"{instance.GetType().Name} has no field {name}.")
        Return CStr(field.GetValue(instance))

    End Function

End Class
