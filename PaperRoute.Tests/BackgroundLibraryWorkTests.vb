Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Backup, Restore and Save behind the disabled window (#132): the window
' keeps processing messages while the work runs on a worker thread, the
' status line says what is happening, and a second piece of library work
' cannot start before the first ends, nor an update restart PaperRoute;
' Windows shutting down lets the work end first. The tests that time the
' window open the 500 library; the update's and the shutdown's open the
' example library.
' Before #132 the handlers ran inline and held the UI thread for the whole
' run (the before table shows UI time equal to wall time for all three), so
' no tick could land; the seams these tests call did not exist then.
<TestClass>
<DoNotParallelize>
Public Class BackgroundLibraryWorkTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub TheWindowKeepsTickingDuringABackupAndASecondBackupCannotStart()
        RunOnStaThread(
            Sub()
                Using board As LibraryBoard = OpenLargeLibrary()
                    Dim first As String = Path.Combine(_root, "first.zip")
                    Dim second As String = Path.Combine(_root, "second.zip")
                    Dim secondStarted As Boolean = True

                    Using probe As New WorkProbe(board, Sub() secondStarted = board.WriteBackup(second))
                        Assert.IsTrue(board.WriteBackup(first), "The first backup was written.")
                        probe.AssertTickedDuringWork("Backing up your library...")
                    End Using

                    Assert.IsTrue(File.Exists(first))
                    Assert.IsFalse(secondStarted, "A second backup cannot start before the first ends.")
                    Assert.IsFalse(File.Exists(second), "Nothing was written under the second name.")
                    Assert.IsFalse(board.IsLibraryWorkInProgress)
                    Assert.IsTrue(IsWindowEnabled(board.Handle), "The window is enabled again.")
                    Assert.IsTrue(board.WriteBackup(second), "Once the first ends, the next backup can start.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The restore's swap runs behind the disabled window; the notice and the
    ' restart that follow it on the UI thread (#116) are recorded.
    <TestMethod>
    Public Sub TheWindowKeepsTickingDuringARestoreAndASecondRestoreCannotStart()
        RunOnStaThread(
            Sub()
                Using board As LibraryBoard = OpenLargeLibrary()
                    Dim backup As String = Path.Combine(_root, "backup.zip")
                    Assert.IsTrue(board.WriteBackup(backup))
                    Dim secondBackup As Boolean = True

                    Using probe As New WorkProbe(board,
                                                 Sub()
                                                     board.Restore(backup)
                                                     secondBackup = board.WriteBackup(Path.Combine(_root, "during.zip"))
                                                 End Sub)
                        board.Restore(backup)
                        probe.AssertTickedDuringWork("Restoring the backup...")
                    End Using

                    Assert.AreEqual(1, board.RestoreNotices.Count, "One restore went through: a second cannot start before the first ends.")
                    StringAssert.StartsWith(board.RestoreNotices(0), "Restored 500 manuscript(s).")
                    Assert.IsFalse(secondBackup, "Nor can a backup.")
                    Assert.IsFalse(board.IsLibraryWorkInProgress)
                    Assert.IsTrue(IsWindowEnabled(board.Handle), "The window is enabled again.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub TheWindowKeepsTickingDuringASaveAndASecondSaveCannotStart()
        RunOnStaThread(
            Sub()
                Using board As LibraryBoard = OpenLargeLibrary()
                    Dim secondSaved As Boolean = True

                    Using probe As New WorkProbe(board, Sub() secondSaved = board.SaveNow())
                        Assert.IsTrue(board.SaveNow(), "The save went through.")
                        probe.AssertTickedDuringWork("Saving your library...")
                    End Using

                    Assert.IsFalse(secondSaved, "A second save cannot start before the first ends.")
                    StringAssert.StartsWith(board.StatusText, "Saved locally", "The status line reports the save that went through.")
                    Assert.IsFalse(board.IsLibraryWorkInProgress)
                    Assert.IsTrue(IsWindowEnabled(board.Handle), "The window is enabled again.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' Applying an update ends PaperRoute at once. An update whose download
    ' finished during a restore ran in the restore's message loop and could
    ' end it partway, between moving the library aside and putting the
    ' backup's in place; the update now waits for the work to end.
    <TestMethod>
    Public Sub AnUpdateWaitsForLibraryWorkToEndBeforeItRestarts()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    Dim update As Task = Nothing

                    Using posted As New ManualResetEventSlim(False)
                        Assert.IsTrue(board.RunWork("Restoring the backup...",
                            Sub()
                                ' Posted to the window, as the download's
                                ' continuation is, and run by the work's loop.
                                board.BeginInvoke(
                                    New Action(
                                        Sub()
                                            update = UpdateService.WaitForLibraryWorkAsync(board, interval:=10)
                                            posted.Set()
                                        End Sub))
                                Assert.IsTrue(posted.Wait(TimeSpan.FromSeconds(30)), "The window ran the posted update.")
                                ' The update checks a few more times while the work runs.
                                Thread.Sleep(100)
                            End Sub))
                    End Using

                    Assert.IsFalse(update.IsCompleted, "The update did not go ahead while the work ran.")

                    Dim limit As Stopwatch = Stopwatch.StartNew()
                    While Not update.IsCompleted AndAlso limit.Elapsed < TimeSpan.FromSeconds(30)
                        Application.DoEvents()
                        Thread.Sleep(1)
                    End While

                    Assert.IsTrue(update.IsCompleted, "Once the work ended, the update went ahead.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' Windows shutting down sends its close into the work's message loop. A
    ' user's close then is refused, as before; the shutdown's is not, but
    ' it lets the work end before it writes the open page's recovery draft
    ' (#123), so neither the work nor the draft is lost. Before the change
    ' the shutdown went on at once, with the work still running.
    <TestMethod>
    Public Sub AWindowsShutdownDuringLibraryWorkLetsItEndAndThenWritesTheDraft()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Dim draft As String = Path.Combine(_root, "library", "data", "recovery", "manuscript-page-draft.json")

                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the shutdown"

                    Dim userClose As New FormClosingEventArgs(CloseReason.UserClosing, False)
                    Dim shutdown As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)
                    Dim workEnded As Boolean = False
                    Dim workEndedFirst As Boolean = False

                    Using closing As New ManualResetEventSlim(False)
                        Assert.IsTrue(board.RunWork("Backing up your library...",
                            Sub()
                                ' Both closes arrive in the work's loop, as
                                ' Windows' messages do.
                                board.BeginInvoke(
                                    New Action(
                                        Sub()
                                            closing.Set()
                                            board.SimulateClosing(userClose)
                                            board.SimulateClosing(shutdown)
                                            workEndedFirst = workEnded
                                        End Sub))
                                Assert.IsTrue(closing.Wait(TimeSpan.FromSeconds(30)), "The window took the closes.")
                                ' The work goes on a while after the shutdown began.
                                Thread.Sleep(200)
                                workEnded = True
                            End Sub))
                    End Using

                    Assert.IsTrue(userClose.Cancel, "A user's close waits for the work.")
                    Assert.IsFalse(shutdown.Cancel, "Windows' close is not refused.")
                    Assert.IsTrue(workEndedFirst, "The work ended before the shutdown went on.")
                    Assert.IsTrue(File.Exists(draft), "The open page's working copy was written to data\recovery.")
                    StringAssert.Contains(File.ReadAllText(draft), "Edited before the shutdown")
                    Assert.IsFalse(board.IsLibraryWorkInProgress)

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' A restore whose swap goes through while Windows shuts down has put the
    ' backup's library on disk, and what is in memory is the library it
    ' replaced: the shutdown hands over to the restored library as the
    ' restart would (#116). Its retry of a failed save (#113) writes
    ' nothing, where it would have written the replaced library over the
    ' restored one.
    <TestMethod>
    Public Sub AWindowsShutdownDuringARestoreSavesNothingOverTheRestoredLibrary()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Dim backup As String = Path.Combine(_root, "backup.zip")
                Call New PortableBackupService(example.ManagedDirectory).CreateBackup(backup, example.Manuscripts, example.Repository)

                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    board.Library.Add(New ManuscriptPipeline.Models.Manuscript With {.Title = "Only in memory"})
                    board.MarkLastSaveFailed()
                    Dim shutdown As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)

                    Using probe As New WorkProbe(board, Sub() board.SimulateClosing(shutdown))
                        board.Restore(backup)
                        Assert.IsTrue(probe.TicksDuringWork > 0, "Windows' close arrived during the restore.")
                    End Using

                    Assert.IsFalse(shutdown.Cancel)
                    Assert.IsFalse(example.Repository.Load().Any(Function(item) item.Title = "Only in memory"), "The restored library is what is on disk.")
                    Assert.AreEqual(1, board.RestoreNotices.Count, "The restore went on to its notice.")
                    Assert.IsFalse(board.SaveNow(), "Nothing is saved while the restore hands over.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    Private Function OpenLargeLibrary() As LibraryBoard
        Dim library As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.Create(Path.Combine(_root, "library"), 500)
        Dim board As New LibraryBoard(library, Path.Combine(_root, "settings"))
        board.Start()
        Return board
    End Function


    ' A timer on the UI thread. While the board reports library work in
    ' progress it counts its ticks, notes the window's state and the status
    ' line, and tries once to start a second piece of work.
    Private NotInheritable Class WorkProbe
        Implements IDisposable

        Private ReadOnly _board As LibraryBoard
        Private ReadOnly _timer As New System.Windows.Forms.Timer With {.Interval = 15}
        Private ReadOnly _secondAttempt As Action
        Private _attempted As Boolean = False

        Public TicksDuringWork As Integer = 0
        Public DisabledDuringWork As Boolean = False
        Public StatusDuringWork As String = String.Empty

        Public Sub New(board As LibraryBoard, secondAttempt As Action)
            _board = board
            _secondAttempt = secondAttempt
            AddHandler _timer.Tick, AddressOf Tick
            _timer.Start()
        End Sub

        Private Sub Tick(sender As Object, e As EventArgs)
            If Not _board.IsLibraryWorkInProgress Then Return
            TicksDuringWork += 1
            DisabledDuringWork = DisabledDuringWork OrElse Not IsWindowEnabled(_board.Handle)
            If StatusDuringWork.Length = 0 Then StatusDuringWork = _board.StatusText
            If Not _attempted Then
                _attempted = True
                _secondAttempt()
            End If
        End Sub

        Public Sub AssertTickedDuringWork(status As String)
            Assert.IsTrue(TicksDuringWork > 0, "A timer tick landed while the work ran.")
            Assert.IsTrue(DisabledDuringWork, "The window was disabled while the work ran.")
            Assert.AreEqual(status, StatusDuringWork, "The status line said what was happening.")
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Dispose()
        End Sub

    End Class


    ' The window is disabled the way a modal dialog disables its owner, at
    ' the window itself, so Windows is asked rather than Form.Enabled.
    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function IsWindowEnabled(handle As IntPtr) As Boolean
    End Function


    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        ' The 500 library's backup and restore take their time on a slow host.
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(10)), "Background library work test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
