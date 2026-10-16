Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Restore in the main window (#116) ends in a restart: the next session
' reads the restored library from disk, so nothing this session held in
' memory can be saved over it.
<TestClass>
<DoNotParallelize>
Public Class BackupRestoreUiTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedDirectory As String = String.Empty
    Private _backupPath As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedDirectory = Path.Combine(_root, "managed")
        _backupPath = Path.Combine(_root, "backup.zip")

        ' The backup: three manuscripts and one reusable author.
        Dim sourceData As String = Path.Combine(_root, "source-data")
        Dim sourceManaged As String = Path.Combine(_root, "source-library")
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceManaged)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        sourceRepository.Save(sourceManuscripts)
        Dim sourceAuthors As New AuthorLibraryData()
        sourceAuthors.Authors.Add(New AuthorRecord With {.GivenName = "Jane", .FamilyName = "Researcher"})
        Call New AuthorLibraryRepository(sourceData).Save(sourceAuthors)
        Call New PortableBackupService(sourceManaged).CreateBackup(_backupPath, sourceManuscripts, sourceRepository)

        ' The library being replaced, saved twice so its .bak files exist.
        Dim repository As New ManuscriptRepository(_dataDirectory, _managedDirectory)
        Dim current As New List(Of Manuscript) From {New Manuscript With {.Title = "Current Before Restore"}}
        Dim authors As New AuthorLibraryData()
        authors.Authors.Add(New AuthorRecord With {.GivenName = "Old", .FamilyName = "Author"})
        Dim authorRepository As New AuthorLibraryRepository(_dataDirectory)
        For pass As Integer = 1 To 2
            repository.Save(current)
            authorRepository.Save(authors)
        Next

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub RestoreTakesTheRestartPath()
        RunOnStaThread(
            Sub()
                Using session As New RestoreSession(_dataDirectory, _managedDirectory)
                    session.Prepare()
                    session.Restore(_backupPath)
                    Assert.IsTrue(session.RestartRequested, "Restore ends in a restart.")
                    StringAssert.Contains(session.Notice, "Restored 3 manuscript")
                    StringAssert.Contains(session.Notice, "restart")
                    session.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub TheSafetyBackupsEqualTheRestoredFiles()
        RunOnStaThread(
            Sub()
                Using session As New RestoreSession(_dataDirectory, _managedDirectory)
                    session.Prepare()
                    session.Restore(_backupPath)
                    session.Close()
                End Using
            End Sub)

        CollectionAssert.AreEqual(File.ReadAllBytes(DataFile("manuscripts.json")), File.ReadAllBytes(DataFile("manuscripts.bak")))
        CollectionAssert.AreEqual(File.ReadAllBytes(DataFile("authors.json")), File.ReadAllBytes(DataFile("authors.bak")))
        Assert.AreEqual(3, New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Count)
    End Sub


    ' Before the change the session went on with the pre-restore authors in
    ' memory, and the next bibliography import saved them over the restored
    ' authors.json. Now the session that goes on is the next one.
    <TestMethod>
    Public Sub ABibliographyImportInTheSessionThatGoesOnLeavesTheRestoredAuthorsOnDisk()
        RunOnStaThread(
            Sub()
                Dim session As New RestoreSession(_dataDirectory, _managedDirectory)
                Try
                    session.Prepare()
                    session.Restore(_backupPath)

                    If session.RestartRequested Then
                        session.Close()
                        session.Dispose()
                        session = New RestoreSession(_dataDirectory, _managedDirectory)
                        session.Prepare()
                    End If

                    session.ImportSavesAuthors()
                    session.Close()
                Finally
                    session.Dispose()
                End Try
            End Sub)

        CollectionAssert.AreEqual(
            {"Jane Researcher"},
            New AuthorLibraryRepository(_dataDirectory).Load().Authors.Select(Function(author) author.DisplayName).ToList(),
            "The restored authors are what the import saved.")
    End Sub


    ' Restore Backup... is on every page's Settings menu. With a manuscript
    ' page open and unsaved, the close the restart sends must neither ask
    ' (Cancel would leave this session running on a library no longer on
    ' disk) nor save (Yes would write the replaced library over the restored
    ' one): what this session holds in memory is the library just replaced.
    <TestMethod>
    <DataRow(DialogResult.Yes)>
    <DataRow(DialogResult.Cancel)>
    Public Sub ADirtyManuscriptPageIsNeitherAskedAboutNorSavedOverTheRestoredLibrary(answer As DialogResult)
        Dim asked As Integer = 0

        RunOnStaThread(
            Sub()
                Using session As New RestoreSession(_dataDirectory, _managedDirectory)
                    session.Prepare()
                    session.OpenFirstManuscript()
                    session.Field("Title").Text = "Edited before the restore"
                    session.SetPrompt(Function(title)
                                          asked += 1
                                          Return answer
                                      End Function)

                    session.Restore(_backupPath)
                    Assert.IsTrue(session.RestartRequested)

                    session.Close()
                    Assert.IsTrue(session.IsDisposed, "The close the restart sends is not held up.")
                End Using
            End Sub)

        Assert.AreEqual(0, asked, "The page is not asked about.")
        CollectionAssert.AreEquivalent(
            {"Active Study", "Published Study", "Filed Study"},
            New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Select(Function(manuscript) manuscript.Title).ToList(),
            "The restored library is what is on disk.")
    End Sub


    Private Function DataFile(name As String) As String
        Return Path.Combine(_dataDirectory, name)
    End Function


    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function


    ' The real main window on a temporary library, with the restore's
    ' restart recorded instead of performed.
    Private NotInheritable Class RestoreSession
        Inherits Form1

        Private ReadOnly _managedDirectory As String

        Public RestartRequested As Boolean
        Public Notice As String = String.Empty

        Public Sub New(dataDirectory As String, managedDirectory As String)
            _managedDirectory = managedDirectory
            SetField("repository", New ManuscriptRepository(dataDirectory, managedDirectory))
            SetField("authorRepository", New AuthorLibraryRepository(dataDirectory))
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function CreateRestoreService() As PortableRestoreService
            Return New PortableRestoreService(_managedDirectory)
        End Function

        Protected Overrides Sub RestartAfterRestore(notice As String)
            RestartRequested = True
            Me.Notice = notice
        End Sub

        ' The window as a session starts it: interface built, library and
        ' authors read from disk.
        Public Sub Prepare()
            CallPrivate("BuildInterface")
            Assert.IsTrue(CBool(CallPrivate("LoadManuscripts")), "The library loads.")
            Assert.IsTrue(LoadAuthorLibrary())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public Sub Restore(backupZipPath As String)
            CallPrivate("RestoreFromArchive", backupZipPath, CreateRestoreService())
            Application.DoEvents()
        End Sub

        Public Sub OpenFirstManuscript()
            CallPrivate("OpenManuscript", DirectCast(FieldValue("manuscripts"), List(Of Manuscript))(0), Nothing)
            Application.DoEvents()
        End Sub

        Public Function Field(name As String) As TextBox
            Return Descendants(DirectCast(FieldValue("manuscriptEditor"), Control)).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
        End Function

        ' A method rather than a property, which WinForms would try to serialize.
        Public Sub SetPrompt(value As Func(Of String, DialogResult))
            unsavedChangesPrompt = value
        End Sub

        ' The save a bibliography import ends with (Form1.Bibliography.vb):
        ' the reusable people as this session holds them.
        Public Sub ImportSavesAuthors()
            DirectCast(FieldValue("authorRepository"), AuthorLibraryRepository).
                SaveKeepingJournals(DirectCast(FieldValue("authorLibrary"), AuthorLibraryData))
        End Sub

        Private Function FieldValue(name As String) As Object
            Return GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me)
        End Function

        Private Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, value)
        End Sub

        Private Function CallPrivate(name As String, ParamArray args As Object()) As Object
            Return GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Function
    End Class

End Class
