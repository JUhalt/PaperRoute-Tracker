Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A board shelf tab. It is a radio button so the shelves form one keyboard
    ' group (arrow keys move between them) and report their selected state to
    ' assistive technology; it paints as a text tab with an accent underline.
    Friend Class ShelfTabButton
        Inherits RadioButton

        Public Sub New()
            Appearance = Appearance.Button
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            AutoSize = True
            Cursor = Cursors.Hand
            Padding = New Padding(2, 6, 2, 8)
            Margin = New Padding(0, 0, 22, 0)
            TextAlign = ContentAlignment.MiddleCenter
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        End Sub

        Protected Overrides Sub OnCheckedChanged(e As System.EventArgs)
            MyBase.OnCheckedChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim background As Color = UiTheme.BoardBackground()
            e.Graphics.Clear(background)

            ' "Pipeline (10)" paints as the name followed by a quieter count;
            ' the text itself stays whole for assistive technology.
            Dim name As String = Text
            Dim count As String = String.Empty
            Dim open As Integer = Text.LastIndexOf(" (", System.StringComparison.Ordinal)
            If open > 0 AndAlso Text.EndsWith(")") Then
                name = Text.Substring(0, open)
                count = Text.Substring(open + 2, Text.Length - open - 3)
            End If

            Const flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Using font As New Font(Me.Font, FontStyle.Bold)
                Dim nameSize As Size = TextRenderer.MeasureText(name, font, Size.Empty, flags)
                Dim countSize As Size = If(count.Length > 0, TextRenderer.MeasureText(count, font, Size.Empty, flags), Size.Empty)
                Dim gap As Integer = If(count.Length > 0, UiTheme.Px(5, DeviceDpi), 0)
                Dim x As Integer = (Width - nameSize.Width - gap - countSize.Width) \ 2
                Dim y As Integer = (Height - 3 - nameSize.Height) \ 2
                TextRenderer.DrawText(e.Graphics, name, font, New Point(x, y), If(Checked, UiTheme.PrimaryText(), UiTheme.SecondaryText()), flags)
                If count.Length > 0 Then
                    TextRenderer.DrawText(e.Graphics, count, font, New Point(x + nameSize.Width + gap, y), UiTheme.MutedText(), flags)
                End If
            End Using

            ' Continue the strip's hairline under this tab; the selected tab
            ' sits on it with an accent underline.
            If Checked Then
                Using underline As New SolidBrush(UiTheme.AccentColor())
                    e.Graphics.FillRectangle(underline, 0, Height - 3, Width, 3)
                End Using
            Else
                Using hairline As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(hairline, 0, Height - 1, Width, Height - 1)
                End Using
            End If

            If Focused AndAlso ShowFocusCues Then
                ControlPaint.DrawFocusRectangle(e.Graphics, New Rectangle(0, 1, Width, Height - 5))
            End If

        End Sub

        ' Size for the bold (selected) text so tabs do not shift when selected.
        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size

            Using font As New Font(Me.Font, FontStyle.Bold)
                Dim text As Size = TextRenderer.MeasureText(Me.Text, font, Size.Empty, TextFormatFlags.NoPrefix)
                Return New Size(text.Width + Padding.Horizontal + 6, text.Height + Padding.Vertical + 3)
            End Using

        End Function

    End Class

End Namespace
