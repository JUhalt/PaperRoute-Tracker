Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    Friend Enum RailGlyph
        Board
        Library
        Journals
        Reminders
        ImportExport
        Settings
        Help
        CollapseRail
        ExpandRail
    End Enum

    ' Paints one left-rail item: an icon, its label, and an optional count.
    Friend NotInheritable Class RailPainter

        Private Sub New()
        End Sub

        Public Shared Sub Paint(
            control As Control,
            g As Graphics,
            glyph As RailGlyph,
            text As String,
            selected As Boolean,
            hover As Boolean,
            focusCue As Boolean,
            badge As Integer)

            Dim dpi As Integer = control.DeviceDpi
            g.Clear(If(control.Parent IsNot Nothing, control.Parent.BackColor, UiTheme.HeaderBackground()))
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim bounds As New RectangleF(0.5F, 0.5F, control.Width - 1.0F, control.Height - 1.0F)
            Dim radius As Single = UiTheme.Px(7, dpi)

            If selected OrElse hover Then
                Using path As GraphicsPath = RoundedShapes.Create(bounds, radius)
                    Using fill As New SolidBrush(If(selected, UiTheme.AccentMutedBackground(), UiTheme.HoverBackground()))
                        g.FillPath(fill, path)
                    End Using
                End Using
            End If

            If focusCue Then
                Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(1.5F, 1.5F, control.Width - 4.0F, control.Height - 4.0F), Math.Max(1.0F, radius - 1.5F))
                    Using pen As New Pen(UiTheme.AccentSecondaryColor(), 2.0F)
                        g.DrawPath(pen, path)
                    End Using
                End Using
            End If

            Dim ink As Color = If(selected, UiTheme.AccentColor(), UiTheme.SecondaryText())
            Dim icon As Single = UiTheme.Px(16, dpi)
            Dim left As Single = UiTheme.Px(10, dpi)
            Dim top As Single = (control.Height - icon) / 2.0F

            ' A collapsed rail shows only the icon; the name stays in the
            ' tooltip and in what assistive technology reads.
            If control.Width < UiTheme.Px(80, dpi) Then
                left = (control.Width - icon) / 2.0F
                DrawGlyph(g, glyph, New RectangleF(left, top, icon, icon), ink, Math.Max(1.4F, UiTheme.Px(2, dpi) * 0.75F))
                If badge > 0 Then
                    Dim dot As Single = UiTheme.Px(7, dpi)
                    Using fill As New SolidBrush(UiTheme.WarningColor())
                        g.FillEllipse(fill, left + icon - dot / 2.0F, top - dot / 3.0F, dot, dot)
                    End Using
                End If
                Return
            End If

            DrawGlyph(g, glyph, New RectangleF(left, top, icon, icon), ink, Math.Max(1.4F, UiTheme.Px(2, dpi) * 0.75F))

            Dim textLeft As Integer = CInt(left + icon + UiTheme.Px(10, dpi))
            Dim badgeWidth As Integer = 0

            Using font As New Font(control.Font, If(selected, FontStyle.Bold, FontStyle.Regular))
                If badge > 0 Then
                    Dim badgeText As String = badge.ToString()
                    Using badgeFont As New Font(control.Font.FontFamily, control.Font.SizeInPoints * 0.8F, FontStyle.Bold)
                        Dim size As Size = TextRenderer.MeasureText(badgeText, badgeFont, Size.Empty, TextFormatFlags.NoPadding)
                        badgeWidth = Math.Max(size.Width + UiTheme.Px(10, dpi), size.Height + UiTheme.Px(4, dpi))
                        Dim badgeBounds As New RectangleF(control.Width - UiTheme.Px(8, dpi) - badgeWidth, (control.Height - size.Height - UiTheme.Px(2, dpi)) / 2.0F, badgeWidth, size.Height + UiTheme.Px(2, dpi))
                        Using path As GraphicsPath = RoundedShapes.Create(badgeBounds, badgeBounds.Height / 2.0F)
                            Using fill As New SolidBrush(UiTheme.WarningMutedBackground())
                                g.FillPath(fill, path)
                            End Using
                        End Using
                        TextRenderer.DrawText(g, badgeText, badgeFont, Rectangle.Round(badgeBounds), UiTheme.WarningColor(),
                            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                    End Using
                End If

                Dim textBounds As New Rectangle(textLeft, 0, control.Width - textLeft - badgeWidth - UiTheme.Px(10, dpi), control.Height)
                TextRenderer.DrawText(g, text, font, textBounds, If(selected, UiTheme.AccentColor(), UiTheme.PrimaryText()),
                    TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
            End Using

        End Sub

        ' Simple line icons on a 16-unit grid, after the Workspace mockup.
        Private Shared Sub DrawGlyph(g As Graphics, glyph As RailGlyph, box As RectangleF, ink As Color, stroke As Single)

            Dim u As Single = box.Width / 16.0F
            Dim x0 As Single = box.X
            Dim y0 As Single = box.Y
            Dim p As Func(Of Single, Single, PointF) = Function(x, y) New PointF(x0 + x * u, y0 + y * u)
            Dim r As Func(Of Single, Single, Single, Single, RectangleF) = Function(x, y, w, h) New RectangleF(x0 + x * u, y0 + y * u, w * u, h * u)

            Using pen As New Pen(ink, stroke) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round, .LineJoin = LineJoin.Round}
                Select Case glyph
                    Case RailGlyph.Board
                        g.DrawRectangle(pen, Rectangle.Round(r(1.5F, 2.5F, 4, 11)))
                        g.DrawRectangle(pen, Rectangle.Round(r(6.5F, 2.5F, 4, 7)))
                        g.DrawRectangle(pen, Rectangle.Round(r(11.5F, 2.5F, 3, 9)))
                    Case RailGlyph.Library
                        g.DrawLine(pen, p(2, 4), p(14, 4))
                        g.DrawLine(pen, p(2, 8), p(14, 8))
                        g.DrawLine(pen, p(2, 12), p(14, 12))
                    Case RailGlyph.Journals
                        g.DrawLines(pen, {p(3, 2.5F), p(12.5F, 2.5F), p(12.5F, 13.5F), p(4, 13.5F), p(3, 12.5F), p(3, 2.5F)})
                        g.DrawLine(pen, p(5.5F, 5.5F), p(10, 5.5F))
                    Case RailGlyph.Reminders
                        g.DrawLines(pen, {p(4, 11), p(4, 7), p(5.2F, 4.2F), p(8, 3), p(10.8F, 4.2F), p(12, 7), p(12, 11), p(13, 12.5F), p(3, 12.5F), p(4, 11)})
                        g.DrawLine(pen, p(6.5F, 14.5F), p(9.5F, 14.5F))
                    Case RailGlyph.ImportExport
                        g.DrawLine(pen, p(5, 2.5F), p(5, 11.5F))
                        g.DrawLines(pen, {p(2.5F, 9), p(5, 11.5F), p(7.5F, 9)})
                        g.DrawLine(pen, p(11, 13.5F), p(11, 4.5F))
                        g.DrawLines(pen, {p(8.5F, 7), p(11, 4.5F), p(13.5F, 7)})
                    Case RailGlyph.Settings
                        g.DrawEllipse(pen, r(5.8F, 5.8F, 4.4F, 4.4F))
                        For index As Integer = 0 To 7
                            Dim angle As Double = index * Math.PI / 4.0
                            g.DrawLine(pen,
                                p(CSng(8 + 4.4 * Math.Cos(angle)), CSng(8 + 4.4 * Math.Sin(angle))),
                                p(CSng(8 + 6.2 * Math.Cos(angle)), CSng(8 + 6.2 * Math.Sin(angle))))
                        Next
                    Case RailGlyph.CollapseRail
                        g.DrawLines(pen, {p(8.5F, 4), p(4.5F, 8), p(8.5F, 12)})
                        g.DrawLines(pen, {p(12.5F, 4), p(8.5F, 8), p(12.5F, 12)})
                    Case RailGlyph.ExpandRail
                        g.DrawLines(pen, {p(3.5F, 4), p(7.5F, 8), p(3.5F, 12)})
                        g.DrawLines(pen, {p(7.5F, 4), p(11.5F, 8), p(7.5F, 12)})
                    Case RailGlyph.Help
                        g.DrawEllipse(pen, r(2, 2, 12, 12))
                        g.DrawBezier(pen, p(6.4F, 6.3F), p(6.4F, 4.3F), p(9.8F, 4.3F), p(9.6F, 6.6F))
                        g.DrawBezier(pen, p(9.6F, 6.6F), p(9.4F, 8), p(8, 8), p(8, 9.6F))
                        g.DrawLine(pen, p(8, 11.4F), p(8, 11.6F))
                End Select
            End Using

        End Sub

    End Class

    ' A page in the left rail. The page items form one radio group, so arrow
    ' keys move between pages and assistive technology reports the current page.
    Friend Class RailButton
        Inherits RadioButton

        Private _hover As Boolean
        Private _badge As Integer

        Public Sub New(glyph As RailGlyph, text As String)
            Me.Glyph = glyph
            Me.Text = text
            AccessibleName = text
            UseMnemonic = False
            Appearance = Appearance.Button
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            AutoSize = False
            Cursor = Cursors.Hand
            Margin = New Padding(0, 0, 0, 2)
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Glyph As RailGlyph

        ' A count shown beside the label, such as reminders that are due.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Badge As Integer
            Get
                Return _badge
            End Get
            Set(value As Integer)
                If _badge <> value Then
                    _badge = value
                    AccessibleDescription = If(value > 0, value.ToString() & " due", Nothing)
                    Invalidate()
                End If
            End Set
        End Property

        Protected Overrides Sub OnCheckedChanged(e As EventArgs)
            MyBase.OnCheckedChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            RailPainter.Paint(Me, e.Graphics, Glyph, Text, Checked, _hover, Focused AndAlso ShowFocusCues, _badge)
        End Sub

    End Class

    ' A command in the left rail, such as Settings or Help.
    Friend Class RailCommandButton
        Inherits Button

        Private _hover As Boolean

        Public Sub New(glyph As RailGlyph, text As String)
            Me.Glyph = glyph
            Me.Text = text
            AccessibleName = text
            UseMnemonic = False
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            AutoSize = False
            Cursor = Cursors.Hand
            Margin = New Padding(0, 0, 0, 2)
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Glyph As RailGlyph

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
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

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            RailPainter.Paint(Me, e.Graphics, Glyph, Text, False, _hover, Focused AndAlso ShowFocusCues, 0)
        End Sub

    End Class

    ' The PaperRoute mark at the top of the rail, drawn from the embedded
    ' 256-pixel logo so it stays sharp at every scale.
    Friend Class RailLogo
        Inherits Control

        Private Shared _logo As Image

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            TabStop = False
            Cursor = Cursors.Hand
            AccessibleRole = AccessibleRole.Graphic
        End Sub

        Friend Shared Function Logo() As Image
            If _logo Is Nothing Then
                Using stream As IO.Stream = GetType(RailLogo).Assembly.GetManifestResourceStream("PaperRoute.Logo.png")
                    If stream IsNot Nothing Then
                        Using original As Image = Image.FromStream(stream)
                            _logo = New Bitmap(original)
                        End Using
                    End If
                End Using
            End If
            Return _logo
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.Clear(If(Parent IsNot Nothing, Parent.BackColor, UiTheme.HeaderBackground()))
            Dim image As Image = Logo()
            If image Is Nothing Then Return
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            e.Graphics.DrawImage(image, ClientRectangle)
        End Sub

    End Class

End Namespace
