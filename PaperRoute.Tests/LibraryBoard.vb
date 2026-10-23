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
    Private ReadOnly _fontScale As Single
    Private _painted As Boolean = False


    ' A text size other than 1 is the window's font scaled, as a larger
    ' Windows text size gives it.
    Public Sub New(library As LargeLibraryFixture.SyntheticLibrary, settingsDirectory As String, Optional fontScale As Single = 1.0F)
        _library = library
        _fontScale = fontScale
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


    ' The Journals page as the window builds it, on the test's author
    ' library rather than the user's.
    Protected Overrides Function CreateJournalLibraryEditor() As Form
        Return New JournalLibraryForm(Library, _library.AuthorRepository, Nothing)
    End Function


    ' Backup and Restore on the test's managed root, never the user's (#132).
    Protected Overrides Function CreateBackupService() As PortableBackupService
        Return New PortableBackupService(_library.ManagedDirectory)
    End Function


    Protected Overrides Function CreateRestoreService() As PortableRestoreService
        Return New PortableRestoreService(_library.ManagedDirectory)
    End Function


    ' A failed load is recorded rather than shown in the startup window
    ' (#119), which offers nothing to put right, so EndFailedStartup closes.
    Protected Overrides Function ShowStartupFailure(failure As StartupFailure) As Boolean
        StartupFailures.Add(failure)
        Return False
    End Function


    ' A restore ends in a restart (#116); its notice is recorded instead,
    ' and nothing restarts the test host.
    Protected Overrides Sub RestartAfterRestore(notice As String)
        RestoreNotices.Add(notice)
    End Sub


    Protected Overrides Sub RestartOnTheLibraryOnDisk()
        Assert.Fail("Nothing in these tests restarts PaperRoute.")
    End Sub


    Public ReadOnly StartupFailures As New List(Of StartupFailure)()
    Public ReadOnly RestoreNotices As New List(Of String)()


    ' The Insights figures are worked out off the UI thread (#132); pumps
    ' until they are shown.
    Public Sub WaitForInsights()
        TestSupport.WaitForInsights(Me, TimeSpan.FromSeconds(60))
    End Sub


    ' Form1_Load's sequence, without the online gate, the update check and
    ' the reminder notification, then Show and one drained message queue:
    ' the Board is interactive. A library that does not load ends the start
    ' through EndFailedStartup, as Form1_Load does (#124); once both have
    ' loaded, startup is complete, and a recovery draft is offered and
    ' answered No (#123). Whether a Paint arrived is recorded; an
    ' off-screen window may get none.
    Public Sub Start()
        SetField("appSettings", DirectCast(GetField("settingsService"), AppSettingsService).Load())
        CallPrivate("BuildInterface")
        If _fontScale <> 1.0F Then Font = New Font(Font.FontFamily, 10.0F * _fontScale)
        CallPrivate("RefreshOnlineIndicators")
        If Not CBool(CallPrivate("LoadManuscripts")) OrElse Not LoadAuthorLibrary() Then
            CallPrivate("EndFailedStartup")
            Assert.Fail("The library did not load: " & String.Join(" | ", StartupFailures.Select(Function(failure) failure.Heading)))
        End If
        SetField("startupComplete", True)
        CallPrivate("DeferBoardFill")
        CallPrivate("RenderManuscripts")
        recoveryDraftPrompt = Function(title) DialogResult.No
        CallPrivate("OfferRecoveryDraft")
        StartPosition = FormStartPosition.Manual
        Location = New Point(-20000, -20000)
        ShowInTaskbar = False
        AddHandler DirectCast(GetField("pipelinePanel"), Control).Paint, Sub(sender, e) _painted = True
        AddHandler Me.Paint, Sub(sender, e) _painted = True
        Show()
        Application.DoEvents()
    End Sub


    ' A shelf of the Board and its tab, by field name.
    Public Function Shelf(name As String) As ManuscriptPipeline.Controls.ManuscriptShelfPanel
        Return DirectCast(GetField(name), ManuscriptPipeline.Controls.ManuscriptShelfPanel)
    End Function


    Public Function ShelfTab(name As String) As RadioButton
        Return DirectCast(GetField(name), RadioButton)
    End Function


    ' Typing in the Board's search, then the pause its timer waits for.
    Public Sub SearchBoard(text As String)
        DirectCast(GetField("txtBoardSearch"), TextBox).Text = text
        CallPrivate("BoardSearchDebounceElapsed", Nothing, EventArgs.Empty)
    End Sub


    ' Processes messages until the Board has shown every card of the shown
    ' shelf, as it does in slices while the window idles (#133).
    Public Sub WaitForBoardFill()
        PumpWhile(Function() IsBoardFillPending, "The Board did not fill in.")
    End Sub


    ' Processes messages until the window's idle work is done.
    Public Sub WaitForIdleWork()
        PumpWhile(Function() IsIdleWorkPending, "The window's idle work did not finish.")
    End Sub


    ' Paints what waits to be painted, and nothing else, as the message
    ' loop would paint it next: a page switch paints nothing until it has
    ' returned (#133), so a row timed after one can leave that paint out.
    Public Sub PaintNow()
        Dim message As PaintMessage
        Dim remaining As Integer = 1000
        While remaining > 0 AndAlso PeekMessage(message, IntPtr.Zero, WM_PAINT, WM_PAINT, PM_REMOVE)
            DispatchMessage(message)
            remaining -= 1
        End While
    End Sub

    Private Const WM_PAINT As UInteger = &HF
    Private Const PM_REMOVE As UInteger = &H1

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure PaintMessage
        Public Handle As IntPtr
        Public Message As UInteger
        Public WParam As IntPtr
        Public LParam As IntPtr
        Public Time As UInteger
        Public X As Integer
        Public Y As Integer
    End Structure

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function PeekMessage(ByRef message As PaintMessage, handle As IntPtr, filterMin As UInteger, filterMax As UInteger, remove As UInteger) As Boolean
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function DispatchMessage(ByRef message As PaintMessage) As IntPtr
    End Function


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
        Return CBool(CallPrivate("RunLibraryWork", status, work, Nothing))
    End Function


    Public Function SaveNow() As Boolean
        Return SaveManuscripts()
    End Function


    ' What Restore Backup... does after its dialogs (#116): the swap behind
    ' the disabled window (#132), then the notice and the restart, recorded
    ' in RestoreNotices.
    Public Sub Restore(backupPath As String)
        CallPrivate("RestoreFromArchive", backupPath, CreateRestoreService())
    End Sub


    ' The close Windows sends at shutdown, or any other, without closing
    ' the window.
    Public Sub SimulateClosing(e As FormClosingEventArgs)
        OnFormClosing(e)
    End Sub


    ' A method rather than a property, which WinForms would try to serialize.
    Public Sub SetPrompt(value As Func(Of String, DialogResult))
        unsavedChangesPrompt = value
    End Sub


    ' The state a save that failed leaves (#113).
    Public Sub MarkLastSaveFailed()
        SetField("librarySaveFailed", True)
    End Sub


    Public ReadOnly Property LastSaveFailed As Boolean
        Get
            Return CBool(GetField("librarySaveFailed"))
        End Get
    End Property


    ' Check for Updates as the window runs it (#112), through the test's
    ' UpdateService.Steps; the update goes on as its awaits complete.
    Public Function StartUpdate() As System.Threading.Tasks.Task(Of Boolean)
        Return DirectCast(CallPrivate("OfferUpdateAsync", True), System.Threading.Tasks.Task(Of Boolean))
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


    ' ---------------------------------------------------------------
    ' The manuscript page, for the tests of an editor that loads one
    ' record after another (#133)
    ' ---------------------------------------------------------------

    ' Saves that went through SaveManuscripts, as they are written.
    Public SaveCount As Integer

    Protected Overrides Function SaveManuscripts() As Boolean
        SaveCount += 1
        Return MyBase.SaveManuscripts()
    End Function


    Public ReadOnly Property Editor As EditManuscriptForm
        Get
            Return DirectCast(GetField("manuscriptEditor"), EditManuscriptForm)
        End Get
    End Property


    ' The page area, and the Board page in it.
    Public ReadOnly Property ContentHost As Control
        Get
            Return DirectCast(GetField("contentHost"), Control)
        End Get
    End Property


    Public ReadOnly Property BoardPage As Control
        Get
            Return DirectCast(GetField("boardPage"), Control)
        End Get
    End Property


    ' The page now shown in the content area, other than the Board.
    Public ReadOnly Property PageView As Control
        Get
            Return DirectCast(GetField("currentPageView"), Control)
        End Get
    End Property


    Public ReadOnly Property PageName As String
        Get
            Return GetField("currentPage").ToString()
        End Get
    End Property


    Public ReadOnly Property OpenManuscriptId As Guid
        Get
            Return DirectCast(GetField("currentManuscriptId"), Guid)
        End Get
    End Property


    Public ReadOnly Property SaveBarVisible As Boolean
        Get
            Dim bar As Control = DirectCast(GetField("manuscriptSaveBar"), Control)
            Return bar IsNot Nothing AndAlso bar.Visible
        End Get
    End Property


    Public ReadOnly Property SaveBarHint As String
        Get
            Return DirectCast(GetField("manuscriptSaveHint"), Label).Text
        End Get
    End Property


    ' Opens a manuscript at a waypoint as Route View does, without
    ' processing the messages it posts.
    Public Sub OpenAt(manuscript As Manuscript, waypoint As ManuscriptRouteWaypoint)
        CallPrivate("OpenManuscript", manuscript, waypoint)
    End Sub


    ' The manuscript page kept between manuscripts (#133), or Nothing.
    Public ReadOnly Property KeptPage As Control
        Get
            Return DirectCast(GetField("manuscriptPage"), Control)
        End Get
    End Property


    ' The Journals and Insights pages kept between visits (#133), or
    ' Nothing.
    Public ReadOnly Property JournalsPage As Control
        Get
            Return DirectCast(GetField("journalsPage"), Control)
        End Get
    End Property


    Public ReadOnly Property InsightsPage As Control
        Get
            Return DirectCast(GetField("insightsPage"), Control)
        End Get
    End Property


    ' As Windows does when a contrast theme is turned on or off.
    Public Sub ChangeSystemColors()
        OnSystemColorsChanged(EventArgs.Empty)
        Application.DoEvents()
    End Sub


    ' The Discard button of the Unsaved changes bar.
    Public Sub Discard()
        CallPrivate("DiscardManuscriptChanges")
        Application.DoEvents()
    End Sub


    ' Delete Manuscript... once its question is answered Yes.
    Public Sub ConfirmDelete()
        CallPrivate("ManuscriptDeleteConfirmed", Nothing, EventArgs.Empty)
    End Sub


    ' The startup offer of a recovery draft, answered Yes (#123).
    Public Sub AcceptRecoveryDraft()
        recoveryDraftPrompt = Function(title) DialogResult.Yes
        Try
            CallPrivate("OfferRecoveryDraft")
        Finally
            recoveryDraftPrompt = Function(title) DialogResult.No
        End Try
    End Sub


    Public Sub GoBackInHistory()
        CallPrivate("GoBack")
    End Sub


    ' A command key as the window handles it (Ctrl+1, Alt+Left).
    Public Sub PressCommandKey(keyData As Keys)
        Dim message As New Message()
        GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
            Invoke(Me, New Object() {message, keyData})
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
