Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Where the library goes when the system has no Documents folder yet
' (#395, decided November 21, 2026: create the folder on first use). Linux
' and macOS name ~/Documents whether or not it exists, nothing is made
' until the first managed copy, and that copy makes it. Windows answers as
' before. The system's answers are played by StorageEnvironment's
' AskSystemFolder under a temporary home folder, so no test reads or makes
' the real home folder, whatever the machine running it has.
<TestClass>
<DoNotParallelize>
Public Class DocumentsFolderTests

    Private _home As String
    Private _sources As String
    Private _asked As List(Of Environment.SpecialFolderOption)
    Private _previousOverride As String
    Private _previousUses As Integer
    Private _previousAsk As Func(Of Environment.SpecialFolder, Environment.SpecialFolderOption, String)
    Private _previousRunsOnWindows As Func(Of Boolean)


    <TestInitialize>
    Public Sub Initialize()

        _previousOverride = StorageEnvironment.RootOverride
        _previousUses = StorageEnvironment.DefaultRootUses
        _previousAsk = StorageEnvironment.AskSystemFolder
        _previousRunsOnWindows = StorageEnvironment.RunsOnWindows

        _home = CreateTemporaryRoot()
        _sources = CreateTemporaryRoot()
        _asked = New List(Of Environment.SpecialFolderOption)()

        ' No override: the default builders run, against the played system.
        StorageEnvironment.RootOverride = Nothing
        StorageEnvironment.AskSystemFolder = AddressOf AFreshProfile

    End Sub


    ' The default roots were reached, but only the played ones, so the
    ' count AssemblyCleanup checks goes back to where it was.
    <TestCleanup>
    Public Sub Cleanup()

        StorageEnvironment.RootOverride = _previousOverride
        StorageEnvironment.DefaultRootUses = _previousUses
        StorageEnvironment.AskSystemFolder = _previousAsk
        StorageEnvironment.RunsOnWindows = _previousRunsOnWindows
        DeleteTemporaryRoot(_home)
        DeleteTemporaryRoot(_sources)

    End Sub


    <TestMethod>
    Public Sub OffWindowsTheLibraryIsInDocumentsThatDoesNotExistYet()

        StorageEnvironment.RunsOnWindows = Function() False

        Dim expected As String =
            Path.Combine(_home, "Documents", StorageEnvironment.ManagedLibraryFolderName())

        Assert.AreEqual(expected, StorageMigrationService.CurrentManagedLibraryRoot())
        Assert.AreEqual(Path.GetFullPath(expected), New ManagedLibraryService().RootDirectory)
        Assert.AreEqual(
            Path.Combine(_home, ".local", "share", StorageEnvironment.DataFolderName()),
            StorageMigrationService.CurrentDataRoot())
        CollectionAssert.DoesNotContain(_asked, Environment.SpecialFolderOption.None)

    End Sub


    <TestMethod>
    Public Sub OffWindowsNothingIsMadeBeforeFirstUse()

        StorageEnvironment.RunsOnWindows = Function() False

        Dim library As New ManagedLibraryService()
        Dim repository As New ManuscriptRepository()

        Assert.IsFalse(String.IsNullOrEmpty(repository.DataFilePath))
        Assert.IsFalse(String.IsNullOrEmpty(library.RootDirectory))
        Assert.AreEqual(
            0,
            Directory.GetFileSystemEntries(_home).Length,
            String.Join(Environment.NewLine, Directory.GetFileSystemEntries(_home)))

    End Sub


    <TestMethod>
    Public Sub OffWindowsTheFirstManagedCopyMakesTheDocumentsFolder()

        StorageEnvironment.RunsOnWindows = Function() False

        Dim source As String = Path.Combine(_sources, "draft.docx")
        File.WriteAllText(source, "draft")
        Dim version As New ManuscriptVersion() With {
            .Id = Guid.NewGuid(),
            .LocalFilePath = source,
            .IsManagedCopy = True
        }
        Dim manuscript As New Manuscript() With {.Id = Guid.NewGuid()}
        manuscript.Versions.Add(version)

        Dim library As New ManagedLibraryService()
        library.CommitManagedCopies({manuscript})

        Dim documents As String = Path.Combine(_home, "Documents")
        Assert.IsTrue(Directory.Exists(documents), documents)
        Assert.IsTrue(Directory.Exists(library.RootDirectory), library.RootDirectory)
        Assert.IsTrue(library.IsManagedPath(version.LocalFilePath), version.LocalFilePath)
        Assert.AreEqual("draft", File.ReadAllText(version.LocalFilePath))

    End Sub


    ' The app's first start (Program.Run's storage step) makes a missing
    ' ~/.local/share with the data folder inside it, and still no
    ' Documents folder: that waits for the first managed copy.
    <TestMethod>
    Public Sub OffWindowsTheFirstStartMakesTheDataFolderButNoDocuments()

        StorageEnvironment.RunsOnWindows = Function() False

        StorageMigrationService.EnsureCurrentStorage()

        Dim schema As String = Path.Combine(
            _home, ".local", "share", StorageEnvironment.DataFolderName(), "data", "schema.json")
        Assert.IsTrue(File.Exists(schema), schema)
        Assert.IsFalse(Directory.Exists(Path.Combine(_home, "Documents")))

    End Sub


    ' Where the system names no home at all, every default root is
    ' refused, so none becomes a relative path in the start folder.
    <TestMethod>
    Public Sub OffWindowsASystemWithNoHomeIsRefused()

        StorageEnvironment.RunsOnWindows = Function() False
        StorageEnvironment.AskSystemFolder = Function(folder, answer) String.Empty

        Dim documents As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub() StorageMigrationService.CurrentManagedLibraryRoot())
        Assert.AreEqual("The user's Documents folder could not be located.", documents.Message)

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() StorageMigrationService.CurrentDataRoot())
        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() StorageMigrationService.EnsureCurrentStorage())
        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub()
                Dim neverMade As New ManagedLibraryService()
            End Sub)

    End Sub


    ' A redirected Documents folder (OneDrive, a network share) that is
    ' missing right now answers empty on Windows, and the library still
    ' refuses to start there, as in v1.0. Nothing is made.
    <TestMethod>
    Public Sub OnWindowsAMissingDocumentsFolderIsRefusedAsBefore()

        StorageEnvironment.RunsOnWindows = Function() True

        Dim refused As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub()
                    Dim neverMade As New ManagedLibraryService()
                End Sub)

        Assert.AreEqual("The user's Documents folder could not be located.", refused.Message)
        CollectionAssert.DoesNotContain(_asked, Environment.SpecialFolderOption.DoNotVerify)
        Assert.AreEqual(0, Directory.GetFileSystemEntries(_home).Length)

    End Sub


    ' Where Windows names the folder, the library is inside it, asked for
    ' exactly as in v1.0 (no DoNotVerify).
    <TestMethod>
    Public Sub OnWindowsAnExistingDocumentsFolderIsUsedAsBefore()

        Dim documents As String = Path.Combine(_home, "Documents")
        Directory.CreateDirectory(documents)
        StorageEnvironment.RunsOnWindows = Function() True

        Dim expected As String =
            Path.Combine(documents, StorageEnvironment.ManagedLibraryFolderName())

        Assert.AreEqual(Path.GetFullPath(expected), New ManagedLibraryService().RootDirectory)
        CollectionAssert.DoesNotContain(_asked, Environment.SpecialFolderOption.DoNotVerify)

    End Sub


    ' A profile whose Documents and ~/.local/share do not exist: asked with
    ' the existence check, the system answers empty for a missing folder,
    ' as .NET does; asked without it, it names the folder anyway.
    Private Function AFreshProfile(
        folder As Environment.SpecialFolder,
        answer As Environment.SpecialFolderOption
    ) As String

        _asked.Add(answer)

        Dim named As String
        Select Case folder
            Case Environment.SpecialFolder.MyDocuments
                named = Path.Combine(_home, "Documents")
            Case Environment.SpecialFolder.LocalApplicationData
                named = Path.Combine(_home, ".local", "share")
            Case Else
                Throw New AssertFailedException($"Asked for {folder}, which this test does not play.")
        End Select

        If answer = Environment.SpecialFolderOption.DoNotVerify OrElse Directory.Exists(named) Then
            Return named
        End If

        Return String.Empty

    End Function

End Class
