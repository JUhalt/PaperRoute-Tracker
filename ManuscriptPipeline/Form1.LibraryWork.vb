Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Library work that takes seconds on a large library in a synced folder
' (#132): Backup, Restore and the save with its sweep of the managed root
' run on a worker thread while the window is disabled and the status line
' says what is happening. The window keeps painting and its timers keep
' ticking; nothing else can start until the work ends; a failure arrives on
' the UI thread, in the caller's Catch, as it did when the work ran inline.
Partial Public Class Form1

    Private libraryWorkInProgress As Boolean = False


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


    ' Tests supply services on a temporary root.
    Protected Overridable Function CreateBackupService() As PortableBackupService

        Return New PortableBackupService()

    End Function


    Protected Overridable Function CreateRestoreService() As PortableRestoreService

        Return New PortableRestoreService()

    End Function


    ' Runs work on a worker thread behind the disabled window, processing
    ' messages until it ends, as a modal dialog does for its owner. Returns
    ' False, without running the work, while other library work is still in
    ' progress: a second save, backup or restore cannot start before the
    ' first ends.
    Private Function RunLibraryWork(
        status As String,
        work As Action
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

            While Not pending.IsCompleted
                Application.DoEvents()
                DirectCast(pending, IAsyncResult).AsyncWaitHandle.WaitOne(10)
            End While

            ' The work's own exception, not an AggregateException.
            pending.GetAwaiter().GetResult()

            Return True

        Finally

            lblStatus.Text = previousStatus

            For Each window As Form In windows
                If Not window.IsDisposed AndAlso window.IsHandleCreated Then
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


    ' Restores the backup and reloads the library behind the disabled
    ' window, then shows it. Nothing when other library work is still
    ' running.
    Friend Function ReplaceLibraryFrom(
        backupPath As String
    ) As RestoreResult

        Dim service As PortableRestoreService = CreateRestoreService()
        Dim result As RestoreResult = Nothing
        Dim restored As List(Of Manuscript) = Nothing

        If Not RunLibraryWork(
            "Restoring the backup...",
            Sub()
                result = service.RestoreBackup(backupPath, manuscripts, repository)
                restored = repository.Load()
            End Sub
        ) Then

            Return Nothing

        End If

        manuscripts = restored
        RenderManuscripts()

        Return result

    End Function

End Class
