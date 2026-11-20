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
Public Class VersionDeletionSharingTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub
    <TestMethod>
    Public Sub RepositorySave_LeavesADeletedVersionFolderInUseAndNamesIt()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim workingPath As String =
            Path.Combine(
                _root,
                "revision.txt"
            )

        File.WriteAllText(
            workingPath,
            "Managed revision snapshot."
        )

        Dim manuscript As New Manuscript With {
            .Title = "Version in use"
        }

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                String.Empty,
                workingPath,
                True,
                makeCurrent:=True,
                createdDate:=New DateTime(2026, 3, 1)
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim managedPath As String =
            version.LocalFilePath

        Dim managedVersionDirectory As String =
            Path.GetDirectoryName(
                managedPath
            )

        ManuscriptVersionService.DeleteVersion(
            manuscript,
            version.Id
        )

        ' The snapshot is open in another program, so its folder cannot move.
        Using File.Open(
            managedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        )

            repository.Save(
                library
            )

        End Using

        Assert.AreEqual(
            "Managed revision snapshot.",
            File.ReadAllText(
                managedPath
            ),
            "A folder in use is left where it is."
        )

        Assert.AreEqual(
            0,
            New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            ).Load()(0).Versions.Count,
            "The save itself went through."
        )

        StringAssert.Contains(
            repository.LastSaveWarning,
            managedVersionDirectory
        )

        Dim stagingRoot As String =
            Path.Combine(
                managedDirectory,
                ".paperroute-version-delete"
            )

        Assert.IsFalse(
            Directory.Exists(
                stagingRoot
            ) AndAlso
            Directory.GetFiles(
                stagingRoot,
                "*",
                SearchOption.AllDirectories
            ).Length > 0,
            "Nothing of the folder in use was staged."
        )

        ' Once the file is closed, the next save finishes the removal.
        repository.Save(
            library
        )

        Assert.IsFalse(
            Directory.Exists(
                managedVersionDirectory
            ),
            "The next save removes the folder once the file is closed."
        )

        Assert.AreEqual(
            String.Empty,
            repository.LastSaveWarning
        )

    End Sub

End Class
