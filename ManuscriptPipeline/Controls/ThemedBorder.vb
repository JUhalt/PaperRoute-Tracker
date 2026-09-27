Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Paints a single-line text box or list border in the theme's quiet border
    ' color, and in the accent color while it has focus, instead of the
    ' system's black frame. Only the frame changes; the control is untouched.
    Friend NotInheritable Class ThemedBorder
        Inherits NativeWindow

        Private Const WM_NCPAINT As Integer = &H85
        Private Const WM_SETFOCUS As Integer = &H7
        Private Const WM_KILLFOCUS As Integer = &H8
        Private Const WM_PRINT As Integer = &H317
        Private Const PRF_NONCLIENT As Integer = &H4
        Private Const RDW_INVALIDATE As Integer = &H1
        Private Const RDW_FRAME As Integer = &H400

        Private Shared ReadOnly Attached As New ConditionalWeakTable(Of Control, ThemedBorder)()

        Private ReadOnly _control As Control

        Private Sub New(control As Control)
            _control = control
            AddHandler control.HandleCreated, Sub(sender, e) AssignHandle(_control.Handle)
            If control.IsHandleCreated Then AssignHandle(control.Handle)
        End Sub

        Public Shared Sub Attach(control As Control)
            Dim existing As ThemedBorder = Nothing
            If control Is Nothing OrElse Attached.TryGetValue(control, existing) Then Return
            Attached.Add(control, New ThemedBorder(control))
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)

            MyBase.WndProc(m)

            Select Case m.Msg
                Case WM_NCPAINT
                    PaintBorder()
                Case WM_PRINT
                    If (m.LParam.ToInt64() And PRF_NONCLIENT) <> 0 Then
                        Using g As Graphics = Graphics.FromHdc(m.WParam)
                            DrawBorder(g)
                        End Using
                    End If
                Case WM_SETFOCUS, WM_KILLFOCUS
                    RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME Or RDW_INVALIDATE)
            End Select

        End Sub

        Private Sub PaintBorder()

            If Handle = IntPtr.Zero OrElse _control.IsDisposed Then Return

            Dim dc As IntPtr = GetWindowDC(Handle)
            If dc = IntPtr.Zero Then Return

            Try
                Using g As Graphics = Graphics.FromHdc(dc)
                    DrawBorder(g)
                End Using
            Finally
                ReleaseDC(Handle, dc)
            End Try

        End Sub

        Private Sub DrawBorder(g As Graphics)
            Using pen As New Pen(If(_control.Focused, UiTheme.AccentColor(), UiTheme.CardBorder()))
                g.DrawRectangle(pen, 0, 0, _control.Width - 1, _control.Height - 1)
            End Using
        End Sub

        <DllImport("user32.dll")>
        Private Shared Function GetWindowDC(window As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function ReleaseDC(window As IntPtr, dc As IntPtr) As Integer
        End Function

        <DllImport("user32.dll")>
        Private Shared Function RedrawWindow(window As IntPtr, update As IntPtr, region As IntPtr, flags As Integer) As Boolean
        End Function

    End Class

End Namespace
