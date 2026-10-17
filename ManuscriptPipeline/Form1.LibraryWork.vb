Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

' Library work that takes seconds on a large library in a synced folder
' (#132): Backup, Restore and the save with its sweep of the managed root
' run on a worker thread while the window is disabled and the status line
' says what is happening. The window keeps painting and its timers keep
' ticking; nothing else can start until the work ends; a failure arrives on
' the UI thread, in the caller's Catch, as it did when the work ran inline.
' The window closes only once the work has ended (OnFormClosing).
Partial Public Class Form1

    Private libraryWorkInProgress As Boolean = False

    ' The work on the worker thread now, and what a Windows shutdown that
    ' waits for it makes of its outcome; Nothing between pieces of work.
    Private runningLibraryWork As Task = Nothing
    Private runningLibraryWorkAtShutdown As Action(Of Task) = Nothing


    ' Disables a window the way a modal dialog disables its owner: input
    ' stops at the window itself. Form.Enabled would walk every control on
    ' the window and repaint it, seconds each way on a large Board.
    <DllImport("user32.dll")>
    Private Shared Function EnableWindow(
        handle As IntPtr,
        enable As Boolean
    ) As Boolean
    End Function


    ' True while a save, a backup or a restore is running.
    Friend ReadOnly Property IsLibraryWorkInProgress As Boolean
        Get
            Return libraryWorkInProgress
        End Get
    End Property


    ' Runs work on a worker thread behind the disabled window, processing
    ' messages until it ends, as a modal dialog does for its owner. Returns
    ' False, without running the work, while other library work is still in
    ' progress: a second save, backup or restore cannot start before the
    ' first ends. Should Windows shut down meanwhile, atShutdown is given
    ' the ended work (FinishLibraryWorkForShutdown).
    Private Function RunLibraryWork(
        status As String,
        work As Action,
        Optional atShutdown As Action(Of Task) = Nothing
    ) As Boolean

        If libraryWorkInProgress Then
            Return False
        End If

        libraryWorkInProgress = True

        ' The window in use: this one, or a dialog it owns that saves through
        ' it (Check for Publications, Fill Blanks), whose modal loop has
        ' already disabled this window.
        Dim windows As List(Of Form) =
            OwnedForms.Where(Function(owned) owned.Visible AndAlso owned.Modal).ToList()

        If windows.Count = 0 Then
            windows.Add(Me)
        End If

        Dim previousStatus As String = lblStatus.Text

        ' A board search firing mid-save would read the library while the
        ' worker normalizes it.
        boardSearchDebounceTimer.Stop()

        For Each window As Form In windows
            If window.IsHandleCreated Then
                EnableWindow(window.Handle, False)
            End If
        Next

        lblStatus.Text = status

        Try

            Dim pending As Task = Task.Run(work)

            runningLibraryWork = pending
            runningLibraryWorkAtShutdown = atShutdown

            While Not pending.IsCompleted
                Application.DoEvents()
                DirectCast(pending, IAsyncResult).AsyncWaitHandle.WaitOne(10)
            End While

            ' The work's own exception, not an AggregateException.
            pending.GetAwaiter().GetResult()

            Return True

        Finally

            runningLibraryWork = Nothing
            runningLibraryWorkAtShutdown = Nothing

            lblStatus.Text = previousStatus

            ' A window disabled meanwhile stays so, such as this one while
            ' an update accepted during the work downloads.
            For Each window As Form In windows
                If Not window.IsDisposed AndAlso window.IsHandleCreated AndAlso window.Enabled Then
                    EnableWindow(window.Handle, True)
                End If
            Next

            libraryWorkInProgress = False

        End Try

    End Function


    ' Writes the backup behind the disabled window; the handler owns the
    ' dialogs around it. False when other library work is still running.
    Friend Function WriteBackup(
        destinationPath As String
    ) As Boolean

        Dim service As PortableBackupService = CreateBackupService()

        Return RunLibraryWork(
            "Backing up your library...",
            Sub() service.CreateBackup(destinationPath, manuscripts, repository)
        )

    End Function


    ' Reads what a backup contains behind the disabled window. Nothing when
    ' other library work is still running.
    Friend Function InspectBackupFile(
        backupPath As String
    ) As BackupInspection

        Dim service As PortableRestoreService = CreateRestoreService()
        Dim inspection As BackupInspection = Nothing

        If Not RunLibraryWork(
            "Reading the backup...",
            Sub() inspection = service.InspectBackup(backupPath)
        ) Then

            Return Nothing

        End If

        Return inspection

    End Function


    ' Windows is shutting down while a save, backup or restore runs: its
    ' message arrives in the work's message loop. Cut off partway, the work
    ' could leave the library half-written, so this thread waits for it to
    ' end, without processing messages, so nothing else starts meanwhile.
    ' What it ended in is then what the shutdown acts on (a failed save is
    ' tried again, a restore that went through is handed over to), and the
    ' shutdown's own save can start. The loop that started the work finishes
    ' once the close returns.
    Private Sub FinishLibraryWorkForShutdown()

        Dim pending As Task = runningLibraryWork

        If pending Is Nothing Then
            Return
        End If

        DirectCast(pending, IAsyncResult).AsyncWaitHandle.WaitOne()

        runningLibraryWorkAtShutdown?.Invoke(pending)

        runningLibraryWork = Nothing
        runningLibraryWorkAtShutdown = Nothing
        libraryWorkInProgress = False

    End Sub

End Class
