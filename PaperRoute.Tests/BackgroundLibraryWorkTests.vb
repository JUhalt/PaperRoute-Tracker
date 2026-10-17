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
' window open the 500 library; the others open the example library.
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
    ' backup's in place; the update now waits for the work to end, and the
    ' window it disabled stays disabled until the update is done. Driven
    ' through Check for Updates, with a download that ends during the work.
    <TestMethod>
    Public Sub AnUpdateWaitsForLibraryWorkToEndBeforeItRestarts()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()

                    WithScriptedUpdate(board,
                        Sub(update)
                            Dim download As New TaskCompletionSource()
                            update.DownloadGate = download.Task
                            Dim restarted As Task(Of Boolean) = board.StartUpdate()
                            Assert.AreEqual(1, update.Downloaded, "The update is downloading.")

                            Assert.IsTrue(board.RunWork("Restoring the backup...",
                                Sub()
                                    ' The download's end is posted to the window
                                    ' and run by the work's loop; once a message
                                    ' posted after it has run, so has it.
                                    download.SetResult()
                                    board.Invoke(New Action(Sub()
                                                            End Sub))
                                    ' The update checks a few more times while the work runs.
                                    Thread.Sleep(100)
                                End Sub))

                            Assert.AreEqual(0, update.Applied, "The update did not restart PaperRoute while the work ran.")
                            Assert.IsFalse(IsWindowEnabled(board.Handle), "The window stays disabled while the update finishes.")

                            PumpUntil(Function() restarted.IsCompleted, "The update went on once the work ended.")
                            Assert.IsTrue(restarted.Result)
                            Assert.AreEqual(1, update.Applied, "Once the work ended, the update went ahead.")
                            Assert.IsTrue(IsWindowEnabled(board.Handle), "The window takes clicks again.")
                        End Sub)

                    board.Close()
                End Using
            End Sub)
    End Sub


    ' An update offered while library work runs asks its questions once the
    ' work has ended (#112 with #132): a save the page's Yes starts then is
    ' not refused, and a save that failed during the work has been marked
    ' before the #113 question reads the mark.
    <TestMethod>
    Public Sub AnUpdateOfferedDuringLibraryWorkAsksOnceTheWorkHasEnded()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    Dim manuscript As ManuscriptPipeline.Models.Manuscript = board.Library(0)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited before the update"

                    Dim asked As Integer = 0
                    Dim askedDuringWork As Boolean = False
                    board.SetPrompt(Function(title)
                                        asked += 1
                                        askedDuringWork = askedDuringWork OrElse board.IsLibraryWorkInProgress
                                        Return DialogResult.Yes
                                    End Function)

                    WithScriptedUpdate(board,
                        Sub(update)
                            Dim restarted As Task(Of Boolean) = Nothing
                            Dim offeredDuringWork As Integer = 0

                            OnTheWindowDuringWork(board,
                                Sub()
                                    restarted = board.StartUpdate()
                                    offeredDuringWork = update.Offered
                                End Sub)

                            Assert.AreEqual(1, offeredDuringWork, "The update was offered while the work ran.")
                            Assert.AreEqual(0, asked, "The question waited for the work to end.")

                            PumpUntil(Function() restarted.IsCompleted, "The update went on once the work ended.")
                            Assert.AreEqual(1, asked, "The page's edits were asked about.")
                            Assert.IsFalse(askedDuringWork)
                            Assert.IsTrue(restarted.Result, "Yes saved the edits, and the update went ahead.")
                            Assert.AreEqual(1, update.Applied)
                            Assert.AreEqual("Edited before the update", manuscript.Title)
                            StringAssert.Contains(File.ReadAllText(example.Repository.DataFilePath), "Edited before the update", "The edits reached the disk before the restart.")
                        End Sub)

                    board.Close()
                End Using
            End Sub)
    End Sub


    ' A save that fails while an update downloads (#113), such as one from a
    ' dialog the disabled window owns, is asked about before the update
    ' restarts PaperRoute: applying it ends PaperRoute with no close to ask,
    ' and the changes the save missed would be lost. Cancel keeps PaperRoute
    ' open on them.
    <TestMethod>
    Public Sub ASaveThatFailsWhileAnUpdateDownloadsIsAskedAboutBeforeTheRestart()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings")), dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Start()

                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                        asked += 1
                                        Return DialogResult.Cancel
                                    End Function)

                    WithScriptedUpdate(board,
                        Sub(update)
                            Dim download As New TaskCompletionSource()
                            update.DownloadGate = download.Task
                            Dim restarted As Task(Of Boolean) = board.StartUpdate()
                            Assert.AreEqual(1, update.Downloaded, "The update is downloading.")
                            Assert.AreEqual(0, asked, "Nothing was unsaved when the update was accepted.")

                            ' A file held open where the save writes makes it fail.
                            Dim inTheWay As String = Path.Combine(Path.GetDirectoryName(example.Repository.DataFilePath), "manuscripts.tmp")
                            Using blocker As New FileStream(inTheWay, FileMode.Create, FileAccess.Write, FileShare.None)
                                Assert.IsFalse(board.SaveNow(), "The save failed.")
                            End Using
                            Assert.IsTrue(board.LastSaveFailed)
                            CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)

                            download.SetResult()
                            PumpUntil(Function() restarted.IsCompleted, "The update finished its download.")
                            Assert.AreEqual(1, asked, "The failed save was asked about before the restart.")
                            Assert.IsFalse(restarted.Result, "Cancel stopped the update.")
                            Assert.AreEqual(0, update.Applied, "PaperRoute did not restart over the unsaved changes.")
                            Assert.IsTrue(IsWindowEnabled(board.Handle), "The window takes clicks again.")
                        End Sub)

                    Assert.IsTrue(board.LastSaveFailed, "The changes are still marked unsaved.")
                    board.SetPrompt(Function(title) DialogResult.No)
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
    ' restored one, and no recovery draft of the open page (#123) goes into
    ' the restored library's data folder, where the next start would offer
    ' the replaced record's edits over the restored one.
    <TestMethod>
    Public Sub AWindowsShutdownDuringARestoreSavesNothingOverTheRestoredLibrary()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Dim backup As String = Path.Combine(_root, "backup.zip")
                Dim draft As String = Path.Combine(_root, "library", "data", "recovery", "manuscript-page-draft.json")
                Call New PortableBackupService(example.ManagedDirectory).CreateBackup(backup, example.Manuscripts, example.Repository)

                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    ' Edits the restore's question was told to let go.
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the restore"
                    board.Library.Add(New ManuscriptPipeline.Models.Manuscript With {.Title = "Only in memory"})
                    board.MarkLastSaveFailed()
                    Dim shutdown As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)

                    Using probe As New WorkProbe(board, Sub() board.SimulateClosing(shutdown))
                        board.Restore(backup)
                        Assert.IsTrue(probe.TicksDuringWork > 0, "Windows' close arrived during the restore.")
                    End Using

                    Assert.IsFalse(shutdown.Cancel)
                    Assert.IsFalse(example.Repository.Load().Any(Function(item) item.Title = "Only in memory"), "The restored library is what is on disk.")
                    Assert.IsFalse(File.Exists(draft), "No draft of the replaced library's page went to the restored one.")
                    Assert.AreEqual(1, board.RestoreNotices.Count, "The restore went on to its notice.")
                    Assert.IsFalse(board.SaveNow(), "Nothing is saved while the restore hands over.")
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' A save refused because other library work runs wrote nothing but did
    ' not fail (#132 with #113): the failed-save mark stays as it was.
    ' Cleared, a close would skip the question that keeps a failed save's
    ' changes; set, a close would ask about a save that never failed.
    <TestMethod>
    Public Sub ASaveRefusedDuringLibraryWorkLeavesTheFailedSaveMarkAsItWas()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()

                    For Each failedBefore As Boolean In {False, True}
                        If failedBefore Then board.MarkLastSaveFailed()
                        Dim saved As Boolean = True
                        Dim markDuringWork As Boolean = Not failedBefore

                        OnTheWindowDuringWork(board,
                            Sub()
                                saved = board.SaveNow()
                                markDuringWork = board.LastSaveFailed
                            End Sub)

                        Assert.IsFalse(saved, "The save was refused.")
                        Assert.AreEqual(failedBefore, markDuringWork, "The refusal left the mark as it was.")
                        Assert.AreEqual(failedBefore, board.LastSaveFailed, "So did the end of the work.")
                    Next

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The page's Save while library work runs keeps the edits on the page:
    ' its save would be refused, and committing them first left them in the
    ' records the work was reading, saved nowhere, with the page reporting
    ' nothing unsaved.
    <TestMethod>
    Public Sub APageSaveDuringLibraryWorkKeepsTheEditsOnThePage()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    Dim manuscript As ManuscriptPipeline.Models.Manuscript = board.Library(0)
                    Dim original As String = manuscript.Title
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited during the work"
                    Dim saved As Boolean = True

                    OnTheWindowDuringWork(board, Sub() saved = board.SaveManuscriptPage())

                    Assert.IsFalse(saved, "The page's save waits for the work.")
                    Assert.AreEqual(original, manuscript.Title, "The edits did not go into the records the work was reading.")
                    Assert.AreEqual("Edited during the work", board.Field("Title").Text, "They are still on the page.")
                    Assert.IsTrue(board.SaveManuscriptPage(), "Once the work has ended, Save saves them.")
                    Assert.AreEqual("Edited during the work", manuscript.Title)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The Library filter's refill reads every record (#133), which library
    ' work may be normalizing on its worker thread: a pause in typing while
    ' the work runs refills the grid once the work has ended.
    <TestMethod>
    Public Sub TheLibraryFilterRefillsOnceLibraryWorkHasEnded()
        RunOnStaThread(
            Sub()
                Dim example As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.WriteExample(Path.Combine(_root, "library"))
                Using board As New LibraryBoard(example, Path.Combine(_root, "settings"))
                    board.Start()
                    board.NavigateTo(ManuscriptPipeline.Form1.WorkspacePage.Library)
                    Dim grid As DataGridView = ViewCatalog.Descendants(board).OfType(Of DataGridView)().Single()
                    Dim everything As Integer = grid.Rows.Count
                    Assert.IsTrue(everything > 0)
                    board.TypeInLibraryFilter("no manuscript is called this")
                    Dim rowsDuringWork As Integer = -1

                    OnTheWindowDuringWork(board,
                        Sub()
                            board.PauseTyping()
                            rowsDuringWork = grid.Rows.Count
                        End Sub)

                    Assert.AreEqual(everything, rowsDuringWork, "The grid was not refilled while the work ran.")
                    PumpUntil(Function() grid.Rows.Count = 0, "Once the work ended, the paused filter refilled the grid.")
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


    ' Runs action on the window while library work of the test's own runs,
    ' as a message the work's loop takes, and rethrows what it threw.
    Private Shared Sub OnTheWindowDuringWork(board As LibraryBoard, action As Action)
        Dim failure As ExceptionDispatchInfo = Nothing

        Using done As New ManualResetEventSlim(False)
            Assert.IsTrue(board.RunWork("Backing up your library...",
                Sub()
                    board.BeginInvoke(
                        New Action(
                            Sub()
                                Try
                                    action()
                                Catch ex As Exception
                                    failure = ExceptionDispatchInfo.Capture(ex)
                                Finally
                                    done.Set()
                                End Try
                            End Sub))
                    Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(30)), "The window ran the posted action.")
                End Sub))
        End Using

        If failure IsNot Nothing Then failure.Throw()
    End Sub


    ' Processes the window's messages until the condition holds.
    Private Shared Sub PumpUntil(condition As Func(Of Boolean), message As String)
        Dim limit As Stopwatch = Stopwatch.StartNew()
        While Not condition() AndAlso limit.Elapsed < TimeSpan.FromSeconds(30)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(condition(), message)
    End Sub


    ' Check for Updates through scripted steps (#112) that apply nothing and
    ' reach no network; the real steps come back afterwards.
    Private Shared Sub WithScriptedUpdate(board As LibraryBoard, test As Action(Of ManuscriptPageTests.ScriptedUpdate))
        Dim update As New ManuscriptPageTests.ScriptedUpdate With {.Window = board}
        UpdateService.Steps = update.Steps()
        OnlineAccess.ResetForTests()
        Try
            test(update)
        Finally
            UpdateService.Steps = New UpdateSteps()
            OnlineAccess.ResetForTests()
        End Try
    End Sub


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


    ' The shared runner (#134), given the time the 500 library's backup and
    ' restore take on a slow host.
    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action, TimeSpan.FromMinutes(10))
    End Sub

End Class
