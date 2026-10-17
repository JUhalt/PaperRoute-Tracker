Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
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


        <DllImport("dwmapi.dll")>
        Private Shared Function DwmSetWindowAttribute(window As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
        End Function

    End Class

End Namespace
