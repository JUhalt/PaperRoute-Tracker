Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A list whose group headings can be read in Dark. Windows draws a
    ' group's heading in navy whatever the list's colors, and offers no
    ' color for it, so in Dark this list draws the heading and its rule
    ' itself, in the theme's colors. In Light, Windows draws them as before.
    ' Only what this app's lists use is drawn: a left-aligned heading.
    Friend Class GroupedListView
        Inherits ListView

        ' A list's notifications go to its parent, which sends them back.
        Private Const WM_REFLECT_NOTIFY As Integer = &H204E
        Private Const NM_CUSTOMDRAW As Integer = -12
        Private Const CDDS_PREPAINT As Integer = &H1
        Private Const CDRF_SKIPDEFAULT As Integer = &H4
        Private Const LVCDI_GROUP As Integer = &H1
        Private Const LVM_GETGROUPSTATE As Integer = &H105C
        Private Const LVGS_FOCUSED As Integer = &H10

        ' Space beside a heading, and between it and its rule, at 96 DPI.
        Private Const HeadingInset As Integer = 10
        Private Const RuleGap As Integer = 6

        Protected Overrides Sub WndProc(ByRef m As Message)

            If m.Msg = WM_REFLECT_NOTIFY AndAlso UiTheme.IsDark() AndAlso DrawGroupHeading(m) Then
                Return
            End If

            MyBase.WndProc(m)

        End Sub

        ' True when Windows was about to draw a group's heading and it was
        ' drawn here instead. Anything else, and any failure, is left to
        ' Windows.
        Private Function DrawGroupHeading(ByRef m As Message) As Boolean

            Try
                If Marshal.PtrToStructure(Of NMHDR)(m.LParam).code <> NM_CUSTOMDRAW Then Return False

                Dim draw As NMLVCUSTOMDRAW = Marshal.PtrToStructure(Of NMLVCUSTOMDRAW)(m.LParam)
                If draw.dwItemType <> LVCDI_GROUP OrElse draw.nmcd.dwDrawStage <> CDDS_PREPAINT Then Return False

                Dim group As ListViewGroup = GroupBetween(draw.nmcd.rc.Top, draw.nmcd.rc.Bottom)
                Dim row As Rectangle = Rectangle.FromLTRB(draw.rcText.Left, draw.rcText.Top, draw.rcText.Right, draw.rcText.Bottom)
                If group Is Nothing OrElse row.Width <= 0 OrElse row.Height <= 0 Then Return False

                Using g As Graphics = Graphics.FromHdc(draw.nmcd.hdc)
                    PaintHeading(g, group.Header, row, HeadingHasFocus(draw.nmcd.dwItemSpec))
                End Using

                m.Result = New IntPtr(CDRF_SKIPDEFAULT)
                Return True
            Catch ex As Exception
                Return False
            End Try

        End Function

        ' Windows names a group by a number WinForms keeps to itself, and
        ' gives the rectangle of the heading with its rows: the group is
        ' the one with a row there.
        Private Function GroupBetween(top As Integer, bottom As Integer) As ListViewGroup

            For Each group As ListViewGroup In Groups
                For Each item As ListViewItem In group.Items
                    If item.ListView Is Me Then
                        Dim y As Integer = item.Position.Y
                        If y >= top AndAlso y < bottom Then Return group
                        Exit For
                    End If
                Next
            Next

            Return Nothing

        End Function

        ' The arrow keys move from a group's first row up to its heading.
        Private Function HeadingHasFocus(groupId As IntPtr) As Boolean

            If Not Focused Then Return False

            Dim query As Message = Message.Create(Handle, LVM_GETGROUPSTATE, groupId, New IntPtr(LVGS_FOCUSED))
            DefWndProc(query)
            Return (query.Result.ToInt64() And LVGS_FOCUSED) <> 0

        End Function

        Private Sub PaintHeading(g As Graphics, heading As String, row As Rectangle, hasFocus As Boolean)

            Dim inset As Integer = UiTheme.Px(HeadingInset, DeviceDpi)
            Dim flags As TextFormatFlags =
                TextFormatFlags.Left Or
                TextFormatFlags.VerticalCenter Or
                TextFormatFlags.SingleLine Or
                TextFormatFlags.EndEllipsis Or
                TextFormatFlags.NoPrefix Or
                TextFormatFlags.NoPadding

            Dim area As New Rectangle(row.Left + inset, row.Top, Math.Max(0, row.Width - 2 * inset), row.Height)
            Dim needed As Size = TextRenderer.MeasureText(g, heading, Font, area.Size, flags)
            Dim label As New Rectangle(area.Left, area.Top, Math.Min(needed.Width, area.Width), area.Height)

            TextRenderer.DrawText(g, heading, Font, label, UiTheme.SecondaryText(), flags)

            ' The rule runs from the heading to the right edge.
            Dim ruleLeft As Integer = label.Right + UiTheme.Px(RuleGap, DeviceDpi)
            If ruleLeft < area.Right Then
                Dim y As Integer = row.Top + (row.Height - 1) \ 2
                Using pen As New Pen(UiTheme.CardBorder())
                    g.DrawLine(pen, ruleLeft, y, area.Right - 1, y)
                End Using
            End If

            If hasFocus Then
                Dim ring As Rectangle = Rectangle.Inflate(label, UiTheme.Px(2, DeviceDpi), 0)
                ring.Intersect(row)
                ControlPaint.DrawFocusRectangle(g, ring, ForeColor, BackColor)
            End If

        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NMHDR
            Public hwndFrom As IntPtr
            Public idFrom As IntPtr
            Public code As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure RECT
            Public Left As Integer
            Public Top As Integer
            Public Right As Integer
            Public Bottom As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NMCUSTOMDRAW
            Public hdr As NMHDR
            Public dwDrawStage As Integer
            Public hdc As IntPtr
            Public rc As RECT
            Public dwItemSpec As IntPtr
            Public uItemState As Integer
            Public lItemlParam As IntPtr
        End Structure

        ' A list view's custom-draw notification (commctrl.h). For a group,
        ' rc is the heading with its rows and rcText the heading alone.
        <StructLayout(LayoutKind.Sequential)>
        Private Structure NMLVCUSTOMDRAW
            Public nmcd As NMCUSTOMDRAW
            Public clrText As Integer
            Public clrTextBk As Integer
            Public iSubItem As Integer
            Public dwItemType As Integer
            Public clrFace As Integer
            Public iIconEffect As Integer
            Public iIconPhase As Integer
            Public iPartId As Integer
            Public iStateId As Integer
            Public rcText As RECT
            Public uAlign As Integer
        End Structure

    End Class

End Namespace
