Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A recovery draft of the open manuscript page (#123): written when
' PaperRoute closes without being able to ask, offered at the next start,
' and never saved into the library until the user saves.
<TestClass>
<DoNotParallelize>
Public Class RecoveryDraftTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedDirectory As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedDirectory = Path.Combine(_root, "managed")

        Dim repository As New ManuscriptRepository(_dataDirectory, _managedDirectory)
        repository.Save(New List(Of Manuscript) From {Sample("Saved title")})

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' Windows shutting down closes the window with no one to answer a
    ' prompt: the open page's working copy goes to data\recovery instead,
    ' and manuscripts.json is untouched.
    <TestMethod>
    Public Sub ShutdownWithUnsavedChangesWritesTheDraftAndDoesNotPrompt()
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the shutdown"
                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

                    Dim closing As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)
                    board.SimulateClosing(closing)

                    Assert.AreEqual(0, asked, "Shutdown is not held up by a prompt.")
                    Assert.IsFalse(closing.Cancel)
                    Assert.IsTrue(File.Exists(DraftPath), "The working copy was written to data\recovery.")
                    StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the shutdown")
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath), "manuscripts.json is untouched.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The next start offers the draft. Opening it shows the page with the
    ' recovered copy as unsaved changes; the file has served and goes, and
    ' manuscripts.json is byte-identical until the user saves.
    <TestMethod>
    Public Sub TheNextStartOffersTheDraftAndOpeningItShowsThePageWithUnsavedChanges()
        LeaveADraft("Edited before the crash")
        Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Dim offered As String = Nothing
                    board.SetDraftPrompt(Function(title)
                                             offered = title
                                             Return DialogResult.Yes
                                         End Function)

                    board.Offer()

                    Assert.AreEqual("Saved title", offered, "The offer names the manuscript as saved.")
                    Assert.AreEqual("Manuscript", board.PageName, "Accepting opens the manuscript page.")
                    Assert.AreEqual("Edited before the crash", board.Field("Title").Text, "With the recovered copy.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    board.Tick()
                    Assert.IsTrue(board.SaveBarVisible, "The Unsaved changes bar shows.")
                    Assert.IsFalse(File.Exists(DraftPath), "The draft now lives in the page.")
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath), "manuscripts.json is byte-identical until the user saves.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    board.Close()
                End Using
            End Sub)

        Assert.AreEqual("Edited before the crash", New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title, "Save writes the recovered changes.")
    End Sub


    <TestMethod>
    Public Sub DiscardingTheOfferDeletesTheDraftAndLeavesTheLibraryUntouched()
        LeaveADraft("Edited before the crash")
        Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title) DialogResult.No)

                    board.Offer()

                    Assert.AreEqual("Board", board.PageName)
                    Assert.IsFalse(File.Exists(DraftPath), "Discarding deletes the file.")
                    board.Close()
                End Using
            End Sub)

        CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath))
        Assert.AreEqual("Saved title", New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title)
    End Sub


    ' The error dialog's fatal path calls this before PaperRoute ends. With
    ' nothing unsaved there is nothing to write.
    <TestMethod>
    Public Sub AnErrorEndingTheSessionWritesTheDraftOnlyWhenThePageHasUnsavedChanges()
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Assert.IsFalse(board.WriteDraft("an error"), "No page is open.")
                    board.Open(board.Library(0))
                    Assert.IsFalse(board.WriteDraft("an error"), "The page has no unsaved changes.")
                    Assert.IsFalse(File.Exists(DraftPath))

                    board.Field("Title").Text = "Edited before the error"
                    Assert.IsTrue(board.WriteDraft("an error"))
                    StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the error")
                    StringAssert.Contains(File.ReadAllText(DraftPath), "an error")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The UI-thread error handler (#121) writes the open page's unsaved
    ' edits to the draft before it shows anything, so they are offered at
    ' the next start whatever follows; Keep Working then leaves the page as
    ' it was. The real handler runs, its error report under this test's
    ' root so the user's profile is never touched.
    <TestMethod>
    Public Sub TheErrorHandlerWritesTheDraftBeforeItShowsTheDialog()
        Dim draftWhenAsked As Boolean = False
        Dim e As New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The route map could not be drawn."))

        StorageEnvironment.RootOverride = _root
        Try
            RunOnStaThread(
                Sub()
                    Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                        board.Prepare()
                        board.Open(board.Library(0))
                        board.Field("Title").Text = "Edited before the error"
                        board.SetErrorPrompt(Function(failure, unsaved)
                                                 draftWhenAsked = File.Exists(DraftPath)
                                                 Return DialogResult.Cancel
                                             End Function)

                        RaiseUnhandledError(board, e)

                        Assert.IsFalse(board.IsDisposed, "Keep Working goes on.")
                        Assert.AreEqual("Edited before the error", board.Field("Title").Text, "The page is intact.")

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                End Sub)

            Assert.AreEqual(1, Directory.GetFiles(ErrorReportService.DefaultDirectory(), "error-report_*").Length, "The report comes first (#120).")
        Finally
            StorageEnvironment.RootOverride = Nothing
        End Try

        Assert.IsTrue(draftWhenAsked, "The draft was there before the dialog was shown.")
        Assert.IsFalse(e.ExitApplication)
        StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the error")
        StringAssert.Contains(File.ReadAllText(DraftPath), "an error ended the session")
    End Sub


    ' A draft that cannot be written (data\recovery is a file here) does
    ' not keep the error dialog from being shown.
    <TestMethod>
    Public Sub ADraftThatCannotBeWrittenDoesNotHideTheErrorDialog()
        Dim asked As Integer = 0
        File.WriteAllText(Path.Combine(_dataDirectory, "recovery"), "in the way")

        StorageEnvironment.RootOverride = _root
        Try
            RunOnStaThread(
                Sub()
                    Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                        board.Prepare()
                        board.Open(board.Library(0))
                        board.Field("Title").Text = "Edited before the error"
                        board.SetErrorPrompt(Function(failure, unsaved)
                                                 asked += 1
                                                 Return DialogResult.Cancel
                                             End Function)

                        RaiseUnhandledError(board, New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The route map could not be drawn.")))

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                End Sub)
        Finally
            StorageEnvironment.RootOverride = Nothing
        End Try

        Assert.AreEqual(1, asked, "The error dialog is still shown.")
        Assert.IsFalse(File.Exists(DraftPath))
    End Sub


    ' The handler as the application framework calls it, with the board as
    ' the one main window open. A session's window is listed in
    ' Application.OpenForms by Form.OnLoad, which the board skips with
    ' Form1's own load, so it is listed here for the call and unlisted
    ' after, whatever happens.
    Private Shared Sub RaiseUnhandledError(board As Form1, e As Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs)
        Dim openForms As FormCollection = Application.OpenForms
        Dim list As MethodInfo = GetType(FormCollection).GetMethod("Add", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, {GetType(Form)}, Nothing)
        Dim unlist As MethodInfo = GetType(FormCollection).GetMethod("Remove", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, {GetType(Form)}, Nothing)

        list.Invoke(openForms, New Object() {board})
        Try
            Assert.AreSame(board, openForms.OfType(Of Form1)().Single(), "The board is the one main window open.")
            ' Not named application: that would hide Application above.
            Dim host As New ManuscriptPipeline.My.MyApplication()
            GetType(ManuscriptPipeline.My.MyApplication).GetMethod("MyApplication_UnhandledException", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(host, New Object() {host, e})
            Application.DoEvents()
        Finally
            unlist.Invoke(openForms, New Object() {board})
        End Try
    End Sub


    ' A draft for a manuscript no longer in the library cannot be opened; it
    ' is neither offered nor deleted.
    <TestMethod>
    Public Sub ADraftForAManuscriptNoLongerInTheLibraryIsLeftAlone()
        Dim store As New RecoveryDraftStore(_dataDirectory)
        store.Write(New RecoveryDraft With {.ManuscriptId = Guid.NewGuid(), .WrittenAtUtc = DateTime.UtcNow, .Reason = "test", .Manuscript = Sample("Gone")})

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Dim asked As Boolean = False
                    board.SetDraftPrompt(Function(title)
                                             asked = True
                                             Return DialogResult.No
                                         End Function)
                    board.Offer()
                    Assert.IsFalse(asked)
                    Assert.AreEqual("Board", board.PageName)
                    board.Close()
                End Using
            End Sub)

        Assert.IsTrue(File.Exists(DraftPath))
    End Sub


    ' Guard: the window tests call OfferRecoveryDraft directly, so the one
    ' call that wires it into startup, after the board is rendered, is
    ' checked in the source, as the online gate's wiring is. It fails only
    ' if that call is removed; it passed before any change here.
    <TestMethod>
    Public Sub StartupOffersTheDraftAfterTheBoardIsRendered()
        Dim startup As String = File.ReadAllText(Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Form1.vb"))
        Dim loading As Integer = startup.IndexOf("Private Sub Form1_Load(", StringComparison.Ordinal)
        Dim rendering As Integer = startup.IndexOf("RenderManuscripts()", loading, StringComparison.Ordinal)
        Dim offering As Integer = startup.IndexOf("OfferRecoveryDraft()", loading, StringComparison.Ordinal)
        Assert.IsTrue(loading >= 0 AndAlso rendering > loading AndAlso offering > rendering, "Startup offers the draft once the board is rendered.")
    End Sub


    ' A session edits the one manuscript and ends without asking.
    Private Sub LeaveADraft(editedTitle As String)
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = editedTitle
                    Assert.IsTrue(board.WriteDraft("test"))
                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    Private ReadOnly Property ManuscriptsPath As String
        Get
            Return Path.Combine(_dataDirectory, "manuscripts.json")
        End Get
    End Property


    Private ReadOnly Property DraftPath As String
        Get
            Return Path.Combine(_dataDirectory, "recovery", "manuscript-page-draft.json")
        End Get
    End Property


    Private Shared Function Sample(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function


    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function


    ' The real main window on a temporary library: its own load, save and
    ' manuscript page, so a draft is written and read as in a session.
    Private NotInheritable Class DraftBoard
        Inherits Form1

        Public Sub New(dataDirectory As String, managedDirectory As String)
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

        ' The window as a session starts it, up to the point where a draft
        ' would be offered.
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

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(FieldValue("manuscripts"), List(Of Manuscript))
            End Get
        End Property

        Public Sub Open(manuscript As Manuscript)
            CallPrivate("OpenManuscript", manuscript, Nothing)
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Editor As EditManuscriptForm
            Get
                Return DirectCast(FieldValue("manuscriptEditor"), EditManuscriptForm)
            End Get
        End Property

        Public Function Field(name As String) As TextBox
            Return Descendants(Editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
        End Function

        Public ReadOnly Property PageName As String
            Get
                Return FieldValue("currentPage").ToString()
            End Get
        End Property

        Public ReadOnly Property SaveBarVisible As Boolean
            Get
                Dim bar As Control = DirectCast(FieldValue("manuscriptSaveBar"), Control)
                Return bar IsNot Nothing AndAlso bar.Visible
            End Get
        End Property

        Public Sub Tick()
            CallPrivate("ManuscriptDirtyTimer_Tick", Nothing, EventArgs.Empty)
        End Sub

        ' A method rather than a property, which WinForms would try to serialize.
        Public Sub SetPrompt(value As Func(Of String, DialogResult))
            unsavedChangesPrompt = value
        End Sub

        Public Sub SetDraftPrompt(value As Func(Of String, DialogResult))
            recoveryDraftPrompt = value
        End Sub

        Public Sub SetErrorPrompt(value As Func(Of Exception, Boolean, DialogResult))
            unhandledErrorPrompt = value
        End Sub

        ' What a session does right after it has loaded and rendered.
        Public Sub Offer()
            CallPrivate("OfferRecoveryDraft")
            Application.DoEvents()
        End Sub

        ' What the error dialog's fatal path does before PaperRoute ends.
        Public Function WriteDraft(reason As String) As Boolean
            Return WriteRecoveryDraft(reason)
        End Function

        ' The close Windows sends at shutdown, without closing the window.
        Public Sub SimulateClosing(e As FormClosingEventArgs)
            OnFormClosing(e)
        End Sub

        Public Sub PressCommandKey(keyData As Keys)
            Dim message As New Message()
            GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(Me, New Object() {message, keyData})
            Application.DoEvents()
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
