Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
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
Public Class PersistenceSharingTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedLibrary = Path.Combine(_root, "managed")

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' Against the error Windows really raises: manuscripts.json held open
    ' by another program for the whole load is reported in use, and the
    ' loader changes nothing. PersistenceTests.Load_FileHeldOpen-
    ' ByAnotherProgramKeepsItsBytesOnEverySystem, in PaperRoute.Core.Tests,
    ' records what the same load does on Linux and macOS.
    <TestMethod>
    Public Sub Load_FileHeldOpenByAnotherProgramIsReportedInUse()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        manuscripts(0).Title =
            "Second Version"

        repository.Save(manuscripts)

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Using holder As New FileStream(
            repository.DataFilePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None
        )

            Assert.ThrowsExactly(Of StorageFileInUseException)(
                Sub()
                    Dim ignored As List(Of Manuscript) =
                        repository.Load()
                End Sub
            )

        End Using

        Assert.IsFalse(
            repository.LastLoadRecoveredFromBackup
        )

        CollectionAssert.AreEqual(
            primaryBefore,
            File.ReadAllBytes(repository.DataFilePath)
        )

        CollectionAssert.AreEqual(
            backupBefore,
            File.ReadAllBytes(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_dataDirectory, "recovery")
            )
        )

    End Sub

End Class
