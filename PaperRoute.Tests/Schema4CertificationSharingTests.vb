Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Tests of a rule core stage 2 keeps for Windows only, for now (design d2,
' section 5.3): another program holding a file open stops PaperRoute
' replacing, moving or deleting it, and "file in use" is recognised from
' the Windows error codes. On Linux and macOS a lock is advisory and
' replacing, moving or deleting an open file succeeds, so these tests stay
' in PaperRoute.Tests, which runs on Windows only. Each was cut unchanged
' from the core test class of the same name without "Sharing", in
' PaperRoute.Core.Tests, with copies of the helpers it shares there.
<TestClass>
Public Class Schema4CertificationSharingTests

    Private _root As String = String.Empty
    Private _currentData As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedRoot As String = String.Empty
    Private _legacyData As String = String.Empty
    Private _legacyManaged As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _currentData = Path.Combine(_root, "current-data")
        _dataDirectory = Path.Combine(_currentData, "data")
        _managedRoot = Path.Combine(_root, "current-managed")
        _legacyData = Path.Combine(_root, "legacy-data")
        _legacyManaged = Path.Combine(_root, "legacy-managed")
        Directory.CreateDirectory(_dataDirectory)
        Directory.CreateDirectory(_managedRoot)
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    <DataRow(1)>
    <DataRow(2)>
    <DataRow(3)>
    <DataRow(4)>
    <DataRow(5)>
    Public Sub LockedSchema_PreservesPriorBackupAndSourceBytesThenRetryCompletes(schemaVersion As Integer)
        File.WriteAllText(Path.Combine(_dataDirectory, "manuscripts.json"), "[]")
        File.WriteAllText(Path.Combine(_dataDirectory, "authors.json"), "{}")
        File.WriteAllText(SchemaPath(), "{""SchemaVersion"":" & schemaVersion.ToString() & "}")
        Dim backupPath As String = Path.Combine(_dataDirectory, "schema.v" & schemaVersion.ToString() & ".bak")
        File.WriteAllText(backupPath, "Previous recovery metadata must survive a failed upgrade.")
        Dim before As Dictionary(Of String, Byte()) = SnapshotFiles(_root)
        Dim oldSchema As Byte() = File.ReadAllBytes(SchemaPath())

        ' Permit migration validation reads, but prevent replacing the schema.
        Using lockedSchema As New FileStream(SchemaPath(), FileMode.Open, FileAccess.Read, FileShare.Read)
            Assert.ThrowsExactly(Of IOException)(Sub() Migrate())
            AssertFileSnapshotEqual(before, SnapshotFiles(_root))
            Assert.AreEqual(0, Directory.GetFiles(_dataDirectory, "schema.json.tmp*").Length)
        End Using

        Migrate()

        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(SchemaPath()))
        CollectionAssert.AreEqual(oldSchema, File.ReadAllBytes(backupPath))
        CollectionAssert.AreEqual(before(Path.Combine(_dataDirectory, "manuscripts.json")), File.ReadAllBytes(Path.Combine(_dataDirectory, "manuscripts.json")))
        CollectionAssert.AreEqual(before(Path.Combine(_dataDirectory, "authors.json")), File.ReadAllBytes(Path.Combine(_dataDirectory, "authors.json")))
        Assert.AreEqual(0, Directory.GetFiles(_dataDirectory, "schema.json.tmp*").Length)
    End Sub

    Private Sub Migrate()
        StorageMigrationService.EnsureCurrentStorage(_currentData, _legacyData, _managedRoot, _legacyManaged)
    End Sub

    Private Function SchemaPath() As String
        Return StorageMigrationService.SchemaFilePath(_currentData)
    End Function

    Private Shared Function SnapshotFiles(root As String) As Dictionary(Of String, Byte())
        Return Directory.GetFiles(root, "*", SearchOption.AllDirectories).
            ToDictionary(Function(filePath) filePath, Function(filePath) File.ReadAllBytes(filePath), StringComparer.OrdinalIgnoreCase)
    End Function

    Private Shared Sub AssertFileSnapshotEqual(before As Dictionary(Of String, Byte()), after As Dictionary(Of String, Byte()))
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray())
        For Each pair As KeyValuePair(Of String, Byte()) In before
            CollectionAssert.AreEqual(pair.Value, after(pair.Key), "File bytes changed: " & pair.Key)
        Next
    End Sub

End Class
