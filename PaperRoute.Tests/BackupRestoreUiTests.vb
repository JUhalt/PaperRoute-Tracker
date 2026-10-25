Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
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


    ' Until the restart, a save from anywhere in the session, such as an
    ' update check's question answered behind the Restore Complete box,
    ' writes nothing: what is in memory is the library just replaced. Before
    ' the change the page's Save wrote it over the restored manuscripts.json.
    <TestMethod>
    Public Sub ASaveWhileTheRestoreHandsOverWritesNothing()
        RunOnStaThread(
            Sub()
                Using session As New RestoreSession(_dataDirectory, _managedDirectory)
                    session.Prepare()
                    session.OpenFirstManuscript()
                    session.Field("Title").Text = "Edited before the restore"
                    session.Restore(_backupPath)
                    Assert.IsTrue(session.RestartRequested)
                    Dim restored As Byte() = File.ReadAllBytes(DataFile("manuscripts.json"))

                    Assert.IsFalse(session.SaveManuscriptPage(), "Nothing is saved while the restore hands over.")
                    CollectionAssert.AreEqual(restored, File.ReadAllBytes(DataFile("manuscripts.json")))

                    session.Close()
                End Using
            End Sub)

        CollectionAssert.AreEquivalent(
            {"Active Study", "Published Study", "Filed Study"},
            New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Select(Function(manuscript) manuscript.Title).ToList(),
            "The restored library is what is on disk.")
    End Sub


    ' Restore Backup... asks with the backup's date and the PaperRoute that
    ' made it, as its backup.json records them (#115), and with what it
    ' holds. A backup with no backup.json, as PaperRoute 0.9.1 and earlier
    ' made, shows the date its backup-info.txt records and says the version
    ' was not recorded; an archive with neither file says neither was. Before
    ' the change a backup carried neither and the preview showed only the
    ' counts, and then a backup without backup.json showed "Created: not
    ' recorded" although its backup-info.txt held the date.
    <TestMethod>
    Public Sub TheRestorePreviewShowsTheBackupsDateAndVersion()

        Dim created As DateTime

        Using archive As ZipArchive = ZipFile.OpenRead(_backupPath)
            Dim entry As ZipArchiveEntry = archive.GetEntry("backup.json")
            Assert.IsNotNull(entry, "A backup made now carries backup.json.")
            Using reader As New StreamReader(entry.Open())
                Using manifest As JsonDocument = JsonDocument.Parse(reader.ReadToEnd())
                    created = manifest.RootElement.GetProperty("CreatedUtc").GetDateTimeOffset().UtcDateTime
                End Using
            End Using
        End Using

        Dim preview As String = RestorePreview(_backupPath)

        StringAssert.Contains(preview, "Created: " & created.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture))
        StringAssert.Contains(preview, "PaperRoute version: " & PacketExportService.AppVersion())
        StringAssert.Contains(preview, "Manuscripts: 3", "What the backup holds is still shown.")

        SetBackupManifest(_backupPath, Nothing)

        Dim recorded As DateTimeOffset

        Using archive As ZipArchive = ZipFile.OpenRead(_backupPath)
            Using reader As New StreamReader(archive.GetEntry("backup-info.txt").Open())
                Dim line As String = reader.ReadToEnd().Split({Environment.NewLine}, StringSplitOptions.None).Single(Function(text) text.StartsWith("Created: "))
                recorded = DateTimeOffset.Parse(line.Substring("Created: ".Length), CultureInfo.InvariantCulture)
            End Using
        End Using

        preview = RestorePreview(_backupPath)

        StringAssert.Contains(preview, "Created: " & recorded.UtcDateTime.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture))
        StringAssert.Contains(preview, "PaperRoute version: not recorded")
        StringAssert.Contains(preview, "Manuscripts: 3")

        Using archive As ZipArchive = ZipFile.Open(_backupPath, ZipArchiveMode.Update)
            archive.GetEntry("backup-info.txt").Delete()
        End Using

        preview = RestorePreview(_backupPath)

        StringAssert.Contains(preview, "Created: not recorded")
        StringAssert.Contains(preview, "PaperRoute version: not recorded")
        StringAssert.Contains(preview, "Manuscripts: 3")

    End Sub


    ' Restore Backup... shows the library it would replace under the
    ' backup's counts (#226), and how many fewer manuscripts the backup
    ' holds, so last month's backup over a grown library looks different
    ' from yesterday's while the researcher can still back out. The backup
    ' holds 3.
    <TestMethod>
    Public Sub TheRestorePreviewShowsTheLibraryItWouldReplace()

        Dim preview As String = RestorePreview(_backupPath, 1)

        StringAssert.Contains(preview, "Your library now: 1 manuscript.")
        Assert.IsFalse(preview.Contains("fewer"), "A backup with more manuscripts says nothing of fewer: " & preview)
        Assert.IsTrue(preview.IndexOf("Your library now:", StringComparison.Ordinal) > preview.IndexOf("Expanded size:", StringComparison.Ordinal) AndAlso
                      preview.IndexOf("Your library now:", StringComparison.Ordinal) < preview.IndexOf("IMPORTANT:", StringComparison.Ordinal),
                      "The library now is under the backup's counts, before the warning: " & preview)

        preview = RestorePreview(_backupPath, 5)

        StringAssert.Contains(preview, "Your library now: 5 manuscripts.")
        StringAssert.Contains(preview, "This backup has 2 fewer manuscripts than your library now.")

        StringAssert.Contains(RestorePreview(_backupPath, 4), "This backup has 1 fewer manuscript than your library now.")
        Assert.IsFalse(RestorePreview(_backupPath, 3).Contains("fewer"), "A backup the library's size says nothing of fewer.")

        For Each count As Integer In {0, 1, 3, 4, 5, 48}
            Assert.IsFalse(RestorePreview(_backupPath, count).Contains("(s)"), "No ""(s)"" with " & count.ToString() & " manuscripts now.")
        Next

    End Sub


    ' The question Restore Backup... asks about the archive, as the main
    ' window builds it after its check, over a library of this many
    ' manuscripts: by default the one Initialize saved.
    Private Function RestorePreview(backupZipPath As String, Optional currentManuscriptCount As Integer = 1) As String
        Dim build As MethodInfo = GetType(Form1).GetMethod("RestorePreview", BindingFlags.NonPublic Or BindingFlags.Static)
        Assert.IsNotNull(build, "The main window builds its restore question in RestorePreview.")
        Return CStr(build.Invoke(Nothing, New Object() {New PortableRestoreService(_managedDirectory).InspectBackup(backupZipPath), currentManuscriptCount}))
    End Function


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
