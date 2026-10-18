Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms

Namespace Services

    ' Windows 11's frame on PaperRoute's windows (#143): a title bar that
    ' is dark when the theme is and light when it is not, and rounded
    ' corners. WinForms asks for a dark title bar only on the windows it
    ' knows are in dark mode; this sets both on every window it is given,
    ' each time the window is created, and where WinForms already set the
    ' title bar it sets the same value again. Windows 10 has no rounded
    ' corners and may refuse either; a refusal changes nothing.
    Public NotInheritable Class WindowChrome

        Friend Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
        Friend Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
        Friend Const DWMWCP_ROUND As Integer = 2

        ' The answer when the window manager cannot be asked at all.
        Private Const E_NOTIMPL As Integer = &H80004001

        ' A thread's window hook, told as each of its windows is about to be
        ' activated; and the window class of a message box or common dialog.
        Private Const WH_CBT As Integer = 5
        Private Const HCBT_ACTIVATE As Integer = 5
        Private Const DialogBoxClass As String = "#32770"


        ' What was asked of a window's frame, and Windows' answer to each
        ' (an HRESULT; 0 is done).
        Friend NotInheritable Class Frame

            Public ReadOnly Dark As Boolean
            Public ReadOnly DarkResult As Integer
            Public ReadOnly Corners As Integer
            Public ReadOnly CornersResult As Integer

            Public Sub New(dark As Boolean, darkResult As Integer, corners As Integer, cornersResult As Integer)
                Me.Dark = dark
                Me.DarkResult = darkResult
                Me.Corners = corners
                Me.CornersResult = cornersResult
            End Sub

        End Class


        Private Shared ReadOnly Frames As New ConditionalWeakTable(Of Form, Frame)()


        Private Sub New()
        End Sub


        ' Sets the frame now if the window exists, and each time it is
        ' created. A window inside another, such as the manuscript page,
        ' has no frame of its own and is left alone.
        Public Shared Sub Apply(
            form As Form
        )

            If form Is Nothing Then
                Return
            End If

            ' A window is styled more than once; one handler is enough.
            RemoveHandler form.HandleCreated,
                AddressOf FormCreated

            AddHandler form.HandleCreated,
                AddressOf FormCreated

            If form.IsHandleCreated Then
                SetFormFrame(form)
            End If

        End Sub


        ' What the helper last set on a window, or Nothing.
        Friend Shared Function FrameOf(
            form As Form
        ) As Frame

            Dim frame As Frame = Nothing
            Frames.TryGetValue(form, frame)
            Return frame

        End Function


        ' Sets both attributes on a window and returns Windows' answers.
        Friend Shared Function SetFrame(
            window As IntPtr
        ) As Frame

            Dim dark As Integer = If(UiTheme.IsDark(), 1, 0)
            Dim corners As Integer = DWMWCP_ROUND
            Dim darkResult As Integer = E_NOTIMPL
            Dim cornersResult As Integer = E_NOTIMPL

            Try
                darkResult = DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE, dark, Marshal.SizeOf(Of Integer)())
                cornersResult = DwmSetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, corners, Marshal.SizeOf(Of Integer)())
            Catch ex As Exception When TypeOf ex Is DllNotFoundException OrElse TypeOf ex Is EntryPointNotFoundException
                ' No desktop window manager to ask: the frame stays Windows' own.
            End Try

            Return New Frame(dark = 1, darkResult, corners, cornersResult)

        End Function


        ' Message boxes, the Open and Save dialogs and the folder picker
        ' (#188) are Windows' own windows, made on the thread that shows
        ' them, in a modal loop of Windows' own, where neither UiPolish's
        ' idle hook nor a form's Load runs. This hooks the calling thread: as
        ' each window of that thread is about to be activated, a dialog box
        ' (#32770) among them gets the frame every other window gets. The
        ' hook hears of no other thread's windows, so never of another
        ' process's. Disposing it unhooks it; a hook Windows refuses, or a
        ' frame it cannot set, leaves Windows' own frame. Tests pass framed
        ' to hear of each dialog box it framed.
        Friend Shared Function HookDialogs(
            Optional framed As Action(Of IntPtr, Frame) = Nothing
        ) As IDisposable

            Return New DialogHook(
                framed
            )

        End Function


        Private Shared Sub FormCreated(
            sender As Object,
            e As EventArgs
        )

            SetFormFrame(DirectCast(sender, Form))

        End Sub


        Private Shared Sub SetFormFrame(
            form As Form
        )

            If Not form.TopLevel Then
                Return
            End If

            Frames.AddOrUpdate(form, SetFrame(form.Handle))

        End Sub


        Private NotInheritable Class DialogHook
            Implements IDisposable

            ' Held for as long as the hook stands: Windows calls it, and the
            ' collector must not free it first.
            Private ReadOnly _callback As HookProcedure
            Private ReadOnly _framed As Action(Of IntPtr, Frame)
            Private _hook As IntPtr = IntPtr.Zero


            Public Sub New(
                framed As Action(Of IntPtr, Frame)
            )

                _framed = framed
                _callback = AddressOf Activating

                Try
                    _hook = SetWindowsHookEx(WH_CBT, _callback, IntPtr.Zero, GetCurrentThreadId())
                Catch ex As Exception
                    ' Nothing is hooked: the dialogs keep Windows' own frame.
                End Try

            End Sub


            Private Function Activating(
                code As Integer,
                wParam As IntPtr,
                lParam As IntPtr
            ) As IntPtr

                If code = HCBT_ACTIVATE Then

                    ' Nothing may escape into Windows; the window is
                    ' activated whatever happens here.
                    Try
                        If ClassNameOf(wParam) = DialogBoxClass Then
                            Dim frame As Frame = SetFrame(wParam)
                            _framed?.Invoke(wParam, frame)
                        End If
                    Catch ex As Exception
                    End Try

                End If

                Return CallNextHookEx(_hook, code, wParam, lParam)

            End Function


            Public Sub Dispose() Implements IDisposable.Dispose

                If _hook <> IntPtr.Zero Then
                    UnhookWindowsHookEx(_hook)
                    _hook = IntPtr.Zero
                End If

            End Sub

        End Class


        Private Shared Function ClassNameOf(
            window As IntPtr
        ) As String

            Dim name As New StringBuilder(32)
            GetClassName(window, name, name.Capacity)
            Return name.ToString()

        End Function


        Private Delegate Function HookProcedure(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

        <DllImport("dwmapi.dll")>
        Private Shared Function DwmSetWindowAttribute(window As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SetWindowsHookEx(hookType As Integer, callback As HookProcedure, instance As IntPtr, threadId As Integer) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThreadId() As Integer
        End Function

        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetClassName(window As IntPtr, name As StringBuilder, capacity As Integer) As Integer
        End Function

    End Class

End Namespace
