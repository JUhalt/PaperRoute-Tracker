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
    ' Enter or Space click it, like a button. Hovered, its fill eases to
    ' the hover color (#142), except under a contrast theme, where every
    ' fill is the window color.
    Friend Class FilterChip
        Inherits Label

        Private _tone As FilterChipTone = FilterChipTone.Neutral

        ' Held down by the mouse; drawn only under a contrast theme (#191).
        Private _pressed As Boolean = False

        ' Made on the first hover.
        Private _hover As HoverTween = Nothing

        ' The bold weight an emphasized chip is measured and drawn in, made
        ' once for its font rather than at each measure (#133).
        Private _boldFont As Font = Nothing

        ' The text is kept by the chip rather than read from its window at
        ' each measure: a Board shown measures each chip some thirty times
        ' (#133).
        Public Sub New()
            SetStyle(
                ControlStyles.Selectable Or
                ControlStyles.UserPaint Or
                ControlStyles.AllPaintingInWmPaint Or
                ControlStyles.OptimizedDoubleBuffer Or
                ControlStyles.ResizeRedraw Or
                ControlStyles.CacheText,
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
            If _tone = FilterChipTone.Neutral Then Return Font
            If _boldFont Is Nothing Then _boldFont = New Font(Font, FontStyle.Bold)
            Return _boldFont
        End Function

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            _boldFont?.Dispose()
            _boldFont = Nothing
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _boldFont?.Dispose()
                _boldFont = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim text As Size = TextRenderer.MeasureText(Me.Text, ChipFont(), Size.Empty, TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
            Return New Size(text.Width + Padding.Horizontal, text.Height + Padding.Vertical)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Enter OrElse e.KeyCode = Keys.Space Then
                e.Handled = True
                OnClick(EventArgs.Empty)
            End If
        End Sub

        ' A screen reader presses the chip as Enter does (#139).
        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject
            Return New PressableAccessibleObject(Me, Sub() OnClick(EventArgs.Empty))
        End Function

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left AndAlso CanFocus Then Focus()
            SetPressed(e.Button = MouseButtons.Left)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            SetPressed(False)
        End Sub

        Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
            MyBase.OnMouseCaptureChanged(e)
            SetPressed(False)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            If _hover Is Nothing Then _hover = New HoverTween(Me)
            _hover.MoveTo(True)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            SetPressed(False)
            _hover?.MoveTo(False)
        End Sub

        ' The fill hovered: the card surface's hover color for a plain chip;
        ' for a toned chip its own fill part way back to the card surface,
        ' which keeps its text at least as legible as at rest in Light and
        ' Dark (a step toward the text, as the rail's tinted item hovers,
        ' left a warning chip at 3.8:1).
        Friend Shared Function HoverFill(tone As FilterChipTone, fill As Color) As Color
            Return If(tone = FilterChipTone.Neutral, UiTheme.HoverBackground(), UiTheme.Blend(fill, UiTheme.CardBackground(), 0.4F))
        End Function

        Private Sub SetPressed(pressed As Boolean)
            If _pressed = pressed Then Return
            _pressed = pressed
            If UiTheme.IsHighContrast() Then Invalidate()
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

            ' Under a contrast theme every fill is the window color (#191):
            ' the chip is outlined in its text color instead, pressed is the
            ' highlight pair, and focus a ring in the text color, as Windows
            ' draws a control there.
            Dim contrast As Boolean = UiTheme.IsHighContrast()

            If contrast Then
                border = Color.Empty
                If _pressed Then
                    fill = SystemColors.Highlight
                    ink = SystemColors.HighlightText
                End If
            ElseIf _hover IsNot Nothing AndAlso _hover.Amount > 0.0F Then
                Dim hovered As Color = HoverFill(_tone, fill)
                fill = If(_hover.Amount >= 1.0F, hovered, UiTheme.Blend(fill, hovered, _hover.Amount))
            End If

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

            RoundedShapes.OutlineUnderContrast(g, New RectangleF(0, 0, Width, Height), Height / 2.0F, If(_pressed, fill, ink))

            If Focused AndAlso ShowFocusCues Then
                Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(1.5F, 1.5F, Width - 4.0F, Height - 4.0F), (Height - 4.0F) / 2.0F)
                    Using pen As New Pen(If(contrast, UiTheme.PrimaryText(), UiTheme.AccentSecondaryColor()), 2.0F)
                        g.DrawPath(pen, path)
                    End Using
                End Using
            End If

            g.SmoothingMode = SmoothingMode.None
            TextRenderer.DrawText(g, Text, ChipFont(), New Rectangle(0, 0, Width, Height), ink,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)

        End Sub

    End Class

End Namespace
