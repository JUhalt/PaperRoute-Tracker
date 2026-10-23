Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Tabs drawn as text with an accent underline, matching the board and
    ' manuscript page, instead of classic raised tabs and a page frame. It
    ' remains a TabControl, so tab pages, Ctrl+Tab, arrow keys, and the tabs
    ' reported to assistive technology are unchanged.
    Friend Class UnderlineTabControl
        Inherits TabControl

        Private Const WM_SETFONT As Integer = &H30

        Private _selectedFont As Font

        ' The bold font's GDI handle, lent to the native control.
        Private _selectedFontHandle As IntPtr

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Padding = New Point(UiTheme.Px(UiTheme.SpaceMd, DeviceDpi), UiTheme.Px(6, DeviceDpi))
        End Sub

        Private ReadOnly Property SelectedFont As Font
            Get
                If _selectedFont Is Nothing Then
                    _selectedFont = New Font(Font, FontStyle.Bold)
                End If
                Return _selectedFont
            End Get
        End Property

        Private Function SelectedFontHandle() As IntPtr
            If _selectedFontHandle = IntPtr.Zero Then
                _selectedFontHandle = SelectedFont.ToHfont()
            End If
            Return _selectedFontHandle
        End Function

        ' The native control sizes each tab for its caption in the font it is
        ' handed, and Windows Forms hands a control that paints itself none
        ' when its window is made, so the tabs were measured in the stock
        ' system font, which does not follow the display's text: at 150% the
        ' selected tab read "Readiness Chec…" (#93). Whatever font the native
        ' control is handed, it measures in the bold one, so each tab is sized
        ' for its caption in the wider of the two fonts it is drawn in; the
        ' pages keep the control's own Font. The pages are placed again, as
        ' the tab strip's height may have changed.
        Protected Overrides Sub WndProc(ByRef m As Message)
            If m.Msg = WM_SETFONT Then
                m.WParam = SelectedFontHandle()
                MyBase.WndProc(m)
                For Each page As TabPage In TabPages
                    page.Bounds = DisplayRectangle
                Next
                Return
            End If
            MyBase.WndProc(m)
        End Sub

        ' Handed when the window is made and when the font changes; the
        ' handle for the font before is freed once the new one is in use.
        Private Sub SizeTabsForBoldCaptions(previous As IntPtr)
            If IsHandleCreated Then
                SendMessage(Handle, WM_SETFONT, SelectedFontHandle(), New IntPtr(1))
            End If
            If previous <> IntPtr.Zero Then
                DeleteObject(previous)
            End If
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            SizeTabsForBoldCaptions(IntPtr.Zero)
        End Sub

        Private Function HeaderBottom() As Integer
            If IsHandleCreated AndAlso TabCount > 0 Then
                Return GetTabRect(0).Bottom
            End If
            Return Font.Height + Padding.Y * 2 + 4
        End Function

        ' Pages fill the width below the tab strip, without a frame.
        Public Overrides ReadOnly Property DisplayRectangle As Rectangle
            Get
                Dim top As Integer = HeaderBottom() + UiTheme.Px(UiTheme.SpaceSm, DeviceDpi)
                Return New Rectangle(0, top, ClientSize.Width, Math.Max(0, ClientSize.Height - top))
            End Get
        End Property

        Protected Overrides Sub OnSelectedIndexChanged(e As EventArgs)
            MyBase.OnSelectedIndexChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            Dim previous As IntPtr = _selectedFontHandle
            _selectedFontHandle = IntPtr.Zero
            _selectedFont?.Dispose()
            _selectedFont = Nothing
            MyBase.OnFontChanged(e)
            SizeTabsForBoldCaptions(previous)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(SectionCard.SurfaceBehind(Me))

            If TabCount = 0 Then Return

            Dim bottom As Integer = HeaderBottom()
            Using hairline As New Pen(UiTheme.CardBorder())
                g.DrawLine(hairline, 0, bottom - 1, Width, bottom - 1)
            End Using

            Dim flags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis
            If Not ShowKeyboardCues Then flags = flags Or TextFormatFlags.HidePrefix

            Dim underline As Integer = UiTheme.Px(3, DeviceDpi)

            For index As Integer = 0 To TabCount - 1
                Dim bounds As Rectangle = GetTabRect(index)
                Dim selected As Boolean = index = SelectedIndex
                Dim textBounds As New Rectangle(bounds.Left, bounds.Top, bounds.Width, bottom - underline - bounds.Top)

                TextRenderer.DrawText(g, TabPages(index).Text, If(selected, SelectedFont, Font), textBounds,
                    If(Not Enabled, UiTheme.MutedText(), If(selected, UiTheme.PrimaryText(), UiTheme.SecondaryText())), flags)

                If selected Then
                    Using accent As New SolidBrush(UiTheme.AccentColor())
                        g.FillRectangle(accent, bounds.Left, bottom - underline, bounds.Width, underline)
                    End Using
                    If Focused AndAlso ShowFocusCues Then
                        ControlPaint.DrawFocusRectangle(g, New Rectangle(bounds.Left + 1, bounds.Top + 1, bounds.Width - 2, bottom - underline - bounds.Top - 2))
                    End If
                End If
            Next

        End Sub

        ' The font handle is freed after the native window, which used it.
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _selectedFont?.Dispose()
                _selectedFont = Nothing
            End If
            MyBase.Dispose(disposing)
            If _selectedFontHandle <> IntPtr.Zero Then
                DeleteObject(_selectedFontHandle)
                _selectedFontHandle = IntPtr.Zero
            End If
        End Sub

        <DllImport("user32.dll")>
        Private Shared Function SendMessage(window As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(handle As IntPtr) As Boolean
        End Function

    End Class

End Namespace
