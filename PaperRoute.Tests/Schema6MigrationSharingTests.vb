Imports System
Imports System.IO
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
Public Class Schema6MigrationSharingTests
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
        File.WriteAllText(_schema, "{""SchemaVersion"":5}")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub Upgrade_WhenMarkerCannotBeReplaced_PreservesPreviousBackupAndCleansTemporaryFile()
        Dim backupPath = Path.Combine(_data, "schema.v5.bak")
        File.WriteAllText(backupPath, "previous recovery marker")
        Using locked As New FileStream(_schema, FileMode.Open, FileAccess.Read, FileShare.Read)
            Assert.ThrowsExactly(Of IOException)(Sub() EnsureStorage())
        End Using
        Assert.AreEqual(5, StorageMigrationService.ReadSchemaVersion(_schema))
        Assert.AreEqual("previous recovery marker", File.ReadAllText(backupPath))
        Assert.AreEqual(0, Directory.GetFiles(_data, "schema.json.tmp*").Length)
    End Sub

    Private Sub EnsureStorage()
        StorageMigrationService.EnsureCurrentStorage(_current, Path.Combine(_root, "legacy"), Path.Combine(_root, "library"), Path.Combine(_root, "legacy-library"))
    End Sub
End Class
