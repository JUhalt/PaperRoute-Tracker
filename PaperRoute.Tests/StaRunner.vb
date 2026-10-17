Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The one STA thread every UI test runs on (#134). The action runs on a
' thread of its own and whatever it throws comes back to the test with its
' stack. A test that never returns fails only itself: once the wait is up
' the runner tells every form and dialog box that thread opened to close,
' gives the thread a moment to finish, and then fails, so a form it left
' behind cannot fail the next test in the same process.
Friend NotInheritable Class StaRunner

    ' Long enough for the slowest UI test on a slow, shared machine; a test
    ' stuck in a message box still fails in bounded time.
    Public Shared ReadOnly DefaultTimeout As TimeSpan =
        TimeSpan.FromMinutes(2)

    ' How long a timed-out thread is given to finish once its windows have
    ' been told to close.
    Private Shared ReadOnly CloseGrace As TimeSpan =
        TimeSpan.FromSeconds(5)

    ' The window class of a message box or a common dialog.
    Private Const DialogBoxClass As String =
        "#32770"

    Private Const WM_CLOSE As UInteger =
        &H10


    Private Sub New()
    End Sub


    ' Runs action on a new STA thread, with visual styles on as in the app,
    ' and waits for it. With a color mode, the process is put in that mode
    ' for the action and back afterwards, as the theme tests need.
    Public Shared Sub Run(
        action As Action,
        Optional timeout As TimeSpan? = Nothing,
        Optional mode As SystemColorMode? = Nothing
    )

        Dim failure As ExceptionDispatchInfo =
            Nothing

        Dim nativeThreadId As Integer =
            0

        Dim thread As New Thread(
            Sub()

                Volatile.Write(nativeThreadId, GetCurrentThreadId())

                Dim priorMode As SystemColorMode =
                    SystemColorMode.Classic

                Try

                    ' Visual styles are on before the action creates a
                    ' window, mode or no mode, as the app turns them on before
                    ' its first. WinForms registers one window class per kind
                    ' of control per process, from the comctl32 active when the
                    ' first is created: a list shown without them left every
                    ' later list a comctl32 5 list, which has no groups.
                    Application.EnableVisualStyles()

                    ' The color mode is read only when a mode is asked for:
                    ' read on a thread that showed no form, before the process
                    ' had shown one, it left the next thread's first form
                    ' hanging in creation.
                    If mode.HasValue Then
                        priorMode = Application.ColorMode
                        Application.SetColorMode(mode.Value)
                    End If

                    action()

                Catch ex As Exception

                    failure = ExceptionDispatchInfo.Capture(ex)

                Finally

                    If mode.HasValue Then

                        Try
                            Application.SetColorMode(priorMode)
                        Catch ex As Exception
                            ' The action's own failure is the one to report.
                            If failure Is Nothing Then
                                failure = ExceptionDispatchInfo.Capture(ex)
                            End If
                        End Try

                    End If

                End Try

            End Sub
        ) With {
            .IsBackground = True,
            .Name = "PaperRoute UI test"
        }

        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()

        Dim limit As TimeSpan =
            If(timeout, DefaultTimeout)

        If Not thread.Join(limit) Then

            ' Again and again through the grace: a window that opens after
            ' the first pass, such as a message box a closing form shows,
            ' is closed by the next. The thread's id is read on every pass,
            ' as a thread that only just started may not have written it.
            Dim grace As Stopwatch =
                Stopwatch.StartNew()

            Dim finished As Boolean =
                False

            Do
                CloseWindowsOf(Volatile.Read(nativeThreadId))
                finished = thread.Join(TimeSpan.FromMilliseconds(250))
            Loop Until finished OrElse grace.Elapsed >= CloseGrace

            Assert.Fail(
                "The UI test did not finish within " & limit.ToString() & ". " &
                If(
                    finished,
                    "Its windows were closed and it has now finished.",
                    "Its windows were told to close; still open on its thread: " & DescribeWindowsOf(Volatile.Read(nativeThreadId)) & "."
                )
            )

        End If

        failure?.Throw()

    End Sub


    ' The open forms whose windows belong to the calling thread. A form
    ' another test left open on another thread is that test's failure, not
    ' this one's.
    Public Shared Function OpenFormsOnThisThread() As List(Of Form)

        Return Application.OpenForms.Cast(Of Form)().
            Where(Function(form) form.IsHandleCreated AndAlso Not form.InvokeRequired).
            ToList()

    End Function


    ' Asks every form and every dialog box the thread opened to close. The
    ' message is posted, never sent, so a thread that no longer pumps
    ' messages cannot hang this one as well. Other windows on the thread,
    ' such as WinForms' parking window, are left alone.
    Private Shared Sub CloseWindowsOf(
        threadId As Integer
    )

        EnumThreadWindows(
            threadId,
            Function(handle, parameter)

                If TypeOf Control.FromHandle(handle) Is Form OrElse
                   ClassNameOf(handle) = DialogBoxClass Then

                    PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)

                End If

                Return True

            End Function,
            IntPtr.Zero
        )

    End Sub


    ' Every window still on the thread, by title and class, so a modal
    ' message box that blocked the test is named rather than guessed at.
    Private Shared Function DescribeWindowsOf(
        threadId As Integer
    ) As String

        Dim windows As New List(Of String)()

        EnumThreadWindows(
            threadId,
            Function(handle, parameter)

                Dim title As New StringBuilder(256)
                GetWindowText(handle, title, title.Capacity)

                windows.Add(
                    "'" & title.ToString() & "' (" & ClassNameOf(handle) &
                    If(IsWindowVisible(handle), ", visible", String.Empty) & ")"
                )

                Return True

            End Function,
            IntPtr.Zero
        )

        Return If(
            windows.Count = 0,
            "none",
            String.Join("; ", windows)
        )

    End Function


    Private Shared Function ClassNameOf(
        handle As IntPtr
    ) As String

        Dim className As New StringBuilder(256)
        GetClassName(handle, className, className.Capacity)
        Return className.ToString()

    End Function


    Private Delegate Function EnumWindowsCallback(handle As IntPtr, parameter As IntPtr) As Boolean

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsCallback, parameter As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetWindowText(handle As IntPtr, text As StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(handle As IntPtr, text As StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(handle As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

End Class
