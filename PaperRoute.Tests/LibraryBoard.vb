Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The real main window on a test's library (#131): its repositories, author
' library and settings are swapped for the test's, so startup, saves,
' backups and restores run against the temporary root and nothing else.
Friend NotInheritable Class LibraryBoard
    Inherits Form1

    Private ReadOnly _library As LargeLibraryFixture.SyntheticLibrary
    Private _journals As Control = Nothing
    Private _painted As Boolean = False


    Public Sub New(library As LargeLibraryFixture.SyntheticLibrary, settingsDirectory As String)
        _library = library
        SetField("repository", library.Repository)
        SetField("authorRepository", library.AuthorRepository)
        SetField("settingsService", New AppSettingsService(settingsDirectory))
    End Sub


    ' Startup is driven step by step by Start.
    Protected Overrides Sub OnLoad(e As EventArgs)
    End Sub


    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property


    Protected Overrides Function CreateManuscriptEditor(manuscript As Manuscript) As EditManuscriptForm
        Return New EditManuscriptForm(manuscript, Library, _library.AuthorRepository, pageMode:=True)
    End Function


    ' The Authors view is not measured; the real editor would read the
    ' user's author library.
    Protected Overrides Function CreateAuthorLibraryEditor() As Form
        Return New Form()
    End Function


    ' Backup and Restore on the test's managed root, never the user's (#132).
    Protected Overrides Function CreateBackupService() As PortableBackupService
        Return New PortableBackupService(_library.ManagedDirectory)
    End Function


    Protected Overrides Function CreateRestoreService() As PortableRestoreService
        Return New PortableRestoreService(_library.ManagedDirectory)
    End Function


    ' The Insights figures are worked out off the UI thread (#132); pumps
    ' until they are shown.
    Public Sub WaitForInsights()
        Dim limit As Stopwatch = Stopwatch.StartNew()
        While InsightsCalculating AndAlso limit.Elapsed < TimeSpan.FromSeconds(60)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Application.DoEvents()
        Assert.IsFalse(InsightsCalculating, "Insights finished calculating.")
    End Sub


    ' Form1_Load's sequence, without the online gate, the update check and
    ' the reminder notification, then Show and one drained message queue:
    ' the Board is interactive. Whether a Paint arrived is recorded; an
    ' off-screen window may get none.
    Public Sub Start()
        SetField("appSettings", DirectCast(GetField("settingsService"), AppSettingsService).Load())
        CallPrivate("BuildInterface")
        CallPrivate("RefreshOnlineIndicators")
        Assert.IsTrue(CBool(CallPrivate("LoadManuscripts")), "The library loaded.")
        Assert.IsTrue(LoadAuthorLibrary(), "The author library loaded.")
        CallPrivate("RenderManuscripts")
        StartPosition = FormStartPosition.Manual
        Location = New Point(-20000, -20000)
        ShowInTaskbar = False
        AddHandler DirectCast(GetField("pipelinePanel"), Control).Paint, Sub(sender, e) _painted = True
        AddHandler Me.Paint, Sub(sender, e) _painted = True
        Show()
        Application.DoEvents()
    End Sub


    Public ReadOnly Property PaintObserved As Boolean
        Get
            Return _painted
        End Get
    End Property


    Public ReadOnly Property Library As List(Of Manuscript)
        Get
            Return DirectCast(GetField("manuscripts"), List(Of Manuscript))
        End Get
    End Property


    Public ReadOnly Property StatusText As String
        Get
            Return DirectCast(GetField("lblStatus"), Label).Text
        End Get
    End Property


    Public Sub Render()
        CallPrivate("RenderManuscripts")
    End Sub


    ' Work of the test's own behind the disabled window, as a save, backup
    ' or restore runs (#132).
    Public Function RunWork(status As String, work As Action) As Boolean
        Return CBool(CallPrivate("RunLibraryWork", status, work))
    End Function


    Public Function SaveNow() As Boolean
        Return SaveManuscripts()
    End Function


    Public Sub Open(manuscript As Manuscript)
        CallPrivate("OpenManuscript", manuscript, Nothing)
    End Sub


    Public Sub Tick()
        CallPrivate("ManuscriptDirtyTimer_Tick", Nothing, EventArgs.Empty)
    End Sub


    Public Sub TypeInLibraryFilter(text As String)
        DirectCast(GetField("libraryFilter"), TextBox).Text = text
    End Sub


    ' The Library filter's debounce timer firing (#133).
    Public Sub PauseTyping()
        CallPrivate("LibraryFilterDebounceElapsed", Nothing, EventArgs.Empty)
    End Sub


    Public Sub TypeInDeadlinesFilter(text As String)
        DirectCast(GetField("deadlinesFilter"), TextBox).Text = text
    End Sub


    Public Function Field(name As String) As TextBox
        Dim editor As Control = DirectCast(GetField("manuscriptEditor"), Control)
        Return Descendants(editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
    End Function


    ' The Journals page as ShowPage builds it, from the test's author
    ' library, and leaving it as LeaveCurrentPage leaves a hosted editor.
    Public Sub ShowJournals()
        Dim editor As New JournalLibraryForm(Library, _library.AuthorRepository, Nothing)
        _journals = DirectCast(CallPrivate("BuildHostedPage", "Journals",
                                           "Your reusable journal records and their submission checklists. Changes are saved as you make them.",
                                           editor), Control)
        Dim host As Panel = DirectCast(GetField("contentHost"), Panel)
        _journals.Dock = DockStyle.Fill
        host.Controls.Add(_journals)
        _journals.BringToFront()
        DirectCast(GetField("boardPage"), Panel).Visible = False
    End Sub


    Public Sub LeaveJournals()
        DirectCast(GetField("contentHost"), Panel).Controls.Remove(_journals)
        _journals.Dispose()
        _journals = Nothing
        SetField("pageHostedEditor", False)
        LoadAuthorLibrary()
        CallPrivate("RenderManuscripts")
        DirectCast(GetField("boardPage"), Panel).Visible = True
    End Sub


    Private Function CallPrivate(name As String, ParamArray args As Object()) As Object
        Return GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
    End Function


    Private Function GetField(name As String) As Object
        Return GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me)
    End Function


    Private Sub SetField(name As String, value As Object)
        GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, value)
    End Sub


    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

End Class
