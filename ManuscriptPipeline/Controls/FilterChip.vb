Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    Friend Enum FilterChipTone
        Neutral
        Warning
        Danger
        ' The chip's filter is applied to the board.
        Active
    End Enum

    ' A rounded chip that applies a board filter. It is a Label so existing
    ' text and layout code keeps working, but it takes keyboard focus and
    ' Enter or Space click it, like a button.
    Friend Class FilterChip
        Inherits Label

        Private _tone As FilterChipTone = FilterChipTone.Neutral

        Public Sub New()
            SetStyle(
                ControlStyles.Selectable Or
                ControlStyles.UserPaint Or
                ControlStyles.AllPaintingInWmPaint Or
                ControlStyles.OptimizedDoubleBuffer Or
                ControlStyles.ResizeRedraw,
                True)
            TabStop = True
            AutoSize = True
            UseMnemonic = False
            Cursor = Cursors.Hand
            Padding = New Padding(10, 3, 10, 3)
            AccessibleRole = AccessibleRole.PushButton
        End Sub

        <DefaultValue(FilterChipTone.Neutral)>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Property Tone As FilterChipTone
            Get
                Return _tone
            End Get
            Set(value As FilterChipTone)
                If _tone <> value Then
                    _tone = value
                    AccessibleDescription = If(value = FilterChipTone.Active, "Filter applied. Press to clear it.", "Filter the board")
                    ' The bold weight of emphasized chips changes their width.
                    PerformLayout()
                    If Parent IsNot Nothing Then Parent.PerformLayout()
                    Invalidate()
                End If
            End Set
        End Property

        Private Function ChipFont() As Font
            Return If(_tone = FilterChipTone.Neutral, Font, New Font(Font, FontStyle.Bold))
        End Function

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim font As Font = ChipFont()
            Try
                Dim text As Size = TextRenderer.MeasureText(Me.Text, font, Size.Empty, TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
                Return New Size(text.Width + Padding.Horizontal, text.Height + Padding.Vertical)
            Finally
                If font IsNot Me.Font Then font.Dispose()
            End Try
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Enter OrElse e.KeyCode = Keys.Space Then
                e.Handled = True
                OnClick(EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left AndAlso CanFocus Then Focus()
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            AccessibleName = Text
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BoardBackground()))
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim fill As Color
            Dim ink As Color
            Dim border As Color = Color.Empty

            Select Case _tone
                Case FilterChipTone.Warning
                    fill = UiTheme.WarningMutedBackground()
                    ink = UiTheme.WarningColor()
                Case FilterChipTone.Danger
                    fill = UiTheme.DangerMutedBackground()
                    ink = UiTheme.DangerColor()
                Case FilterChipTone.Active
                    fill = UiTheme.AccentMutedBackground()
                    ink = UiTheme.AccentColor()
                Case Else
                    fill = UiTheme.CardBackground()
                    ink = UiTheme.SecondaryText()
                    border = UiTheme.CardBorder()
            End Select

            Dim shape As New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F)
            Using path As GraphicsPath = RoundedShapes.Create(shape, Height / 2.0F)
                Using brush As New SolidBrush(fill)
                    g.FillPath(brush, path)
                End Using
                If Not border.IsEmpty Then
                    Using pen As New Pen(border)
                        g.DrawPath(pen, path)
                    End Using
                End If
            End Using

            If Focused AndAlso ShowFocusCues Then
                Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(1.5F, 1.5F, Width - 4.0F, Height - 4.0F), (Height - 4.0F) / 2.0F)
                    Using pen As New Pen(UiTheme.AccentSecondaryColor(), 2.0F)
                        g.DrawPath(pen, path)
                    End Using
                End Using
            End If

            g.SmoothingMode = SmoothingMode.None
            Dim font As Font = ChipFont()
            Try
                TextRenderer.DrawText(g, Text, font, New Rectangle(0, 0, Width, Height), ink,
                    TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)
            Finally
                If font IsNot Me.Font Then font.Dispose()
            End Try

        End Sub

    End Class

End Namespace
