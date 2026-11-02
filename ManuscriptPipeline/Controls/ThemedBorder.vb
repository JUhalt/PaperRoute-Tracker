Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Paints a text box's, list's or drop-down's border in the theme's quiet
    ' border color, and in the accent color while it has focus, instead of
    ' the system's black frame or a flat drop-down's white one (#266), and a
    ' panel's in the border color. Only the frame changes; the control is
    ' untouched, but for a text box UiPolish leaves with no border of its
    ' own because it scrolls (#266): it is given Windows' window border,
    ' which Windows keeps outside the scroll bars, and the frame is painted
    ' there. A FixedSingle box's frame is drawn round its text, left of a
    ' scroll bar that runs to the box's edge and paints over any frame drawn
    ' there, so the box looked open on its right.
    Friend NotInheritable Class ThemedBorder
        Inherits NativeWindow

        Private Const WM_NCPAINT As Integer = &H85
        Private Const WM_PAINT As Integer = &HF
        Private Const WM_SETFOCUS As Integer = &H7
        Private Const WM_KILLFOCUS As Integer = &H8
        Private Const WM_PRINT As Integer = &H317
        Private Const PRF_NONCLIENT As Integer = &H4
        Private Const RDW_INVALIDATE As Integer = &H1
        Private Const RDW_FRAME As Integer = &H400
        Private Const GWL_STYLE As Integer = -16
        Private Const WS_BORDER As Integer = &H800000
        ' SWP_NOSIZE, NOMOVE, NOZORDER, NOACTIVATE and FRAMECHANGED: the
        ' window keeps its place and lays out its frame again.
        Private Const FrameChanged As Integer = &H1 Or &H2 Or &H4 Or &H10 Or &H20

        Private Shared ReadOnly Attached As New ConditionalWeakTable(Of Control, ThemedBorder)()

        Private ReadOnly _control As Control

        Private Sub New(control As Control)
            _control = control
            AddHandler control.HandleCreated, Sub(sender, e) Hook()
            ' An editable drop-down's keyboard focus is in its text, whose
            ' window alone hears it come and go (#266); Windows Forms tells
            ' the drop-down as GotFocus and LostFocus.
            If TypeOf control Is ComboBox Then
                AddHandler control.GotFocus, Sub(sender, e) Redraw()
                AddHandler control.LostFocus, Sub(sender, e) Redraw()
            End If
            If control.IsHandleCreated Then Hook()
        End Sub

        ' Windows Forms sets a box's styles from its own only as it makes a
        ' new window for the box, as a change of its BorderStyle, Multiline
        ' or ScrollBars does, so each window is given the border as it is
        ' made.
        Private Sub Hook()

            AssignHandle(_control.Handle)

            Dim style As Integer = GetWindowLong(Handle, GWL_STYLE)

            If HasNoBorderOfItsOwn() AndAlso (style And WS_BORDER) = 0 Then
                SetWindowLong(Handle, GWL_STYLE, style Or WS_BORDER)
                SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged)
            End If

        End Sub

        ' A text box UiPolish left without a border, which Windows Forms'
        ' styles do not give one: it keeps the window border it is given.
        Private Function HasNoBorderOfItsOwn() As Boolean
            Dim box As TextBoxBase = TryCast(_control, TextBoxBase)
            Return box IsNot Nothing AndAlso box.BorderStyle = BorderStyle.None
        End Function

        Public Shared Sub Attach(control As Control)
            Dim existing As ThemedBorder = Nothing
            If control Is Nothing OrElse Attached.TryGetValue(control, existing) Then Return
            Attached.Add(control, New ThemedBorder(control))
        End Sub

        Protected Overrides Sub WndProc(ByRef m As Message)

            MyBase.WndProc(m)

            Select Case m.Msg
                ' A themed text box redraws its frame while painting its text,
                ' so the border is repainted after both.
                Case WM_NCPAINT, WM_PAINT
                    PaintBorder()
                Case WM_PRINT
                    If (m.LParam.ToInt64() And PRF_NONCLIENT) <> 0 Then
                        Using g As Graphics = Graphics.FromHdc(m.WParam)
                            DrawBorder(g)
                        End Using
                    End If
                Case WM_SETFOCUS, WM_KILLFOCUS
                    Redraw()
            End Select

        End Sub

        ' The frame is painted again in the color for the control's focus.
        Private Sub Redraw()
            If Handle <> IntPtr.Zero Then
                RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME Or RDW_INVALIDATE)
            End If
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

        <DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
        Private Shared Function GetWindowLong(window As IntPtr, index As Integer) As Integer
        End Function

        <DllImport("user32.dll", EntryPoint:="SetWindowLongW")>
        Private Shared Function SetWindowLong(window As IntPtr, index As Integer, value As Integer) As Integer
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SetWindowPos(window As IntPtr, after As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, flags As Integer) As Boolean
        End Function

    End Class

End Namespace
