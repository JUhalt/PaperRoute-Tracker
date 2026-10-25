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
    ' the painting, so keyboard cues and focus remain visible. Its fill
    ' eases into and out of the hover color (#142); a press shows at once.
    Friend Class ActionButton
        Inherits Button

        Private _role As ActionButtonRole = ActionButtonRole.Secondary
        Private _hover As HoverTween = Nothing
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

        ' Made on the first hover.
        Private ReadOnly Property Hover As HoverTween
            Get
                If _hover Is Nothing Then _hover = New HoverTween(Me)
                Return _hover
            End Get
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            Hover.MoveTo(True)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _pressed Then
                _pressed = False
                Invalidate()
            End If
            Hover.MoveTo(False)
        End Sub

        ' A press shows at once, and lets go to the full hover color.
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button = MouseButtons.Left Then
                _pressed = True
                Hover.SnapTo(True)
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

        ' Never smaller than the button's minimum: a layout must place the
        ' button at the size it is drawn, or its neighbors overlap it and its
        ' row is too short for it (as Look Up Journal Facts showed at 150%).
        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim text As Size = TextRenderer.MeasureText(Me.Text, Me.Font, Size.Empty, TextFlags())
            Return New Size(
                Math.Max(text.Width + Padding.Horizontal, MinimumSize.Width),
                Math.Max(text.Height + Padding.Vertical, MinimumSize.Height))
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            Dim behind As Color = If(Parent IsNot Nothing, Parent.BackColor, UiTheme.BoardBackground())
            g.Clear(behind)
            PaintFace(g, New Rectangle(0, 0, Width, Height), _role, Text, Font, TextFlags(),
                      FaceColorsAt(_role, If(_hover Is Nothing, 0.0F, _hover.Amount), _pressed, Enabled, behind),
                      Focused AndAlso ShowFocusCues, DeviceDpi)

        End Sub

        ' The button's face in bounds, for this button and for buttons a list
        ' draws in its rows (the Deadlines page), so both look the same.
        Friend Shared Sub PaintFace(
            g As Graphics,
            bounds As Rectangle,
            role As ActionButtonRole,
            text As String,
            font As Font,
            flags As TextFormatFlags,
            hover As Boolean,
            pressed As Boolean,
            focusRing As Boolean,
            enabled As Boolean,
            dpi As Integer
        )

            PaintFace(g, bounds, role, text, font, flags, FaceColors(role, hover, pressed, enabled), focusRing, dpi)

        End Sub

        Private Shared Sub PaintFace(
            g As Graphics,
            bounds As Rectangle,
            role As ActionButtonRole,
            text As String,
            font As Font,
            flags As TextFormatFlags,
            face As (Fill As Color, Border As Color, Ink As Color),
            focusRing As Boolean,
            dpi As Integer
        )

            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim fill As Color = face.Fill
            Dim border As Color = face.Border
            Dim ink As Color = face.Ink

            Dim radius As Single = UiTheme.Px(UiTheme.ControlRadius, dpi)
            Dim shape As New RectangleF(bounds.X + 0.5F, bounds.Y + 0.5F, bounds.Width - 1.0F, bounds.Height - 1.0F)

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

            If focusRing Then
                Dim ring As New RectangleF(bounds.X + 2.0F, bounds.Y + 2.0F, bounds.Width - 5.0F, bounds.Height - 5.0F)
                Using path As GraphicsPath = RoundedShapes.Create(ring, Math.Max(1.0F, radius - 2.0F))
                    Using pen As New Pen(If(role = ActionButtonRole.Primary, UiTheme.OnAccentText(), UiTheme.AccentSecondaryColor()), 2.0F)
                        g.DrawPath(pen, path)
                    End Using
                End Using
            End If

            g.SmoothingMode = SmoothingMode.None
            TextRenderer.DrawText(g, text, font, bounds, ink, flags)

        End Sub

        ' What a face of the role is filled, outlined and written with in a
        ' state, from the theme as it is now; Color.Empty is no fill or no
        ' outline. A quiet face has no outline, but under a contrast theme,
        ' where its fill is the window color too, it is outlined as every
        ' other button is, so its edge shows (#138).
        Friend Shared Function FaceColors(
            role As ActionButtonRole,
            hover As Boolean,
            pressed As Boolean,
            enabled As Boolean
        ) As (Fill As Color, Border As Color, Ink As Color)

            Dim fill As Color = Color.Empty
            Dim border As Color = Color.Empty
            Dim ink As Color

            Dim hoverTint As Color = UiTheme.HoverBackground()

            Select Case role
                Case ActionButtonRole.Primary
                    Dim accent As Color = UiTheme.AccentColor()
                    Dim shade As Color = If(UiTheme.IsDark(), Color.White, Color.Black)
                    fill = If(pressed, UiTheme.Blend(accent, shade, 0.2F), If(hover, UiTheme.Blend(accent, shade, 0.1F), accent))
                    ink = UiTheme.OnAccentText()
                Case ActionButtonRole.Quiet
                    fill = If(hover OrElse pressed, hoverTint, Color.Empty)
                    If UiTheme.IsHighContrast() Then border = UiTheme.CardBorder()
                    ink = UiTheme.AccentColor()
                Case ActionButtonRole.Danger
                    fill = If(hover OrElse pressed, hoverTint, UiTheme.CardBackground())
                    border = UiTheme.CardBorder()
                    ink = UiTheme.DangerColor()
                Case Else
                    fill = If(hover OrElse pressed, hoverTint, UiTheme.CardBackground())
                    border = UiTheme.CardBorder()
                    ink = UiTheme.PrimaryText()
            End Select

            If Not enabled Then
                fill = If(role = ActionButtonRole.Quiet, Color.Empty, UiTheme.BoardBackground())
                border = UiTheme.CardBorder()
                ink = UiTheme.MutedText()
            End If

            Return (fill, border, ink)

        End Function

        ' The face part way from rest (0) to hover (1), as a hover eases in
        ' or out (#142): only the fill moves, since the outline and the text
        ' are the same at rest and hovered in every role and state. At 0 and
        ' 1 it is FaceColors exactly; a press is FaceColors' pressed face at
        ' once. A quiet face, which has no fill at rest, eases in from what
        ' is behind it.
        Friend Shared Function FaceColorsAt(
            role As ActionButtonRole,
            hover As Single,
            pressed As Boolean,
            enabled As Boolean,
            behind As Color
        ) As (Fill As Color, Border As Color, Ink As Color)

            If pressed OrElse Not enabled OrElse hover <= 0.0F Then
                Return FaceColors(role, False, pressed, enabled)
            End If

            Dim hovered As (Fill As Color, Border As Color, Ink As Color) = FaceColors(role, True, False, enabled)

            If hover >= 1.0F Then
                Return hovered
            End If

            Dim rest As Color = FaceColors(role, False, False, enabled).Fill
            Return (UiTheme.Blend(If(rest.IsEmpty, behind, rest), hovered.Fill, hover), hovered.Border, hovered.Ink)

        End Function

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
