Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    Friend Enum ActionButtonRole
        ' The one main action of a view: filled with the accent color.
        Primary
        ' Ordinary actions: a quiet outline.
        Secondary
        ' Link-like actions inside content, such as a card's more-actions button.
        Quiet
        ' Destructive actions.
        Danger
    End Enum

    ' A rounded button painted from the theme tokens. It keeps Button's
    ' behavior (click, keyboard, mnemonics, accessibility) and only replaces
    ' the painting, so keyboard cues and focus remain visible.
    Friend Class ActionButton
        Inherits Button

        Private _role As ActionButtonRole = ActionButtonRole.Secondary
        Private _hover As Boolean
        Private _pressed As Boolean

        Public Sub New()
            SetStyle(
                ControlStyles.UserPaint Or
                ControlStyles.AllPaintingInWmPaint Or
                ControlStyles.OptimizedDoubleBuffer Or
                ControlStyles.ResizeRedraw,
                True)
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            UseVisualStyleBackColor = False
            Cursor = Cursors.Hand
            Padding = New Padding(14, 5, 14, 5)
        End Sub

        <DefaultValue(ActionButtonRole.Secondary)>
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Property Role As ActionButtonRole
            Get
                Return _role
            End Get
            Set(value As ActionButtonRole)
                _role = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            _pressed = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                _pressed = True
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
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

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim text As Size = TextRenderer.MeasureText(Me.Text, Me.Font, Size.Empty, TextFlags())
            Return New Size(text.Width + Padding.Horizontal, text.Height + Padding.Vertical)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BoardBackground()))
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim fill As Color = Color.Empty
            Dim border As Color = Color.Empty
            Dim ink As Color

            Dim hoverTint As Color = UiTheme.HoverBackground()

            Select Case _role
                Case ActionButtonRole.Primary
                    Dim accent As Color = UiTheme.AccentColor()
                    Dim shade As Color = If(UiTheme.IsDark(), Color.White, Color.Black)
                    fill = If(_pressed, UiTheme.Blend(accent, shade, 0.2F), If(_hover, UiTheme.Blend(accent, shade, 0.1F), accent))
                    ink = UiTheme.OnAccentText()
                Case ActionButtonRole.Quiet
                    fill = If(_hover OrElse _pressed, hoverTint, Color.Empty)
                    ink = UiTheme.AccentColor()
                Case ActionButtonRole.Danger
                    fill = If(_hover OrElse _pressed, hoverTint, UiTheme.CardBackground())
                    border = UiTheme.CardBorder()
                    ink = UiTheme.DangerColor()
                Case Else
                    fill = If(_hover OrElse _pressed, hoverTint, UiTheme.CardBackground())
                    border = UiTheme.CardBorder()
                    ink = UiTheme.PrimaryText()
            End Select

            If Not Enabled Then
                fill = If(_role = ActionButtonRole.Quiet, Color.Empty, UiTheme.BoardBackground())
                border = UiTheme.CardBorder()
                ink = UiTheme.MutedText()
            End If

            Dim radius As Single = UiTheme.Px(UiTheme.ControlRadius, DeviceDpi)
            Dim shape As New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F)

            Using path As GraphicsPath = RoundedShapes.Create(shape, radius)
                If Not fill.IsEmpty Then
                    Using brush As New SolidBrush(fill)
                        g.FillPath(brush, path)
                    End Using
                End If
                If Not border.IsEmpty Then
                    Using pen As New Pen(border)
                        g.DrawPath(pen, path)
                    End Using
                End If
            End Using

            If Focused AndAlso ShowFocusCues Then
                Dim ring As New RectangleF(2.0F, 2.0F, Width - 5.0F, Height - 5.0F)
                Using path As GraphicsPath = RoundedShapes.Create(ring, Math.Max(1.0F, radius - 2.0F))
                    Using pen As New Pen(If(_role = ActionButtonRole.Primary, UiTheme.OnAccentText(), UiTheme.AccentSecondaryColor()), 2.0F)
                        g.DrawPath(pen, path)
                    End Using
                End Using
            End If

            g.SmoothingMode = SmoothingMode.None
            TextRenderer.DrawText(g, Text, Font, New Rectangle(0, 0, Width, Height), ink, TextFlags())

        End Sub

        Private Function TextFlags() As TextFormatFlags
            Dim flags As TextFormatFlags =
                TextFormatFlags.HorizontalCenter Or
                TextFormatFlags.VerticalCenter Or
                TextFormatFlags.SingleLine Or
                TextFormatFlags.EndEllipsis
            If Not UseMnemonic Then
                flags = flags Or TextFormatFlags.NoPrefix
            ElseIf Not ShowKeyboardCues Then
                flags = flags Or TextFormatFlags.HidePrefix
            End If
            Return flags
        End Function

    End Class

End Namespace
