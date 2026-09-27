Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A titled section drawn as a card: a rounded surface with its title
    ' inside, instead of a classic group frame. It remains a GroupBox, so
    ' layout, mnemonics, and the grouping announced to assistive technology
    ' are unchanged.
    Friend Class SectionCard
        Inherits GroupBox

        Private _titleFont As Font

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Padding = New Padding(14)
        End Sub

        Private ReadOnly Property TitleFont As Font
            Get
                If _titleFont Is Nothing Then
                    _titleFont = New Font(Font, FontStyle.Bold)
                End If
                Return _titleFont
            End Get
        End Property

        ' The title sits inside the top padding, followed by a small gap.
        Private Function TitleSpace() As Integer
            If String.IsNullOrEmpty(Text) Then Return 0
            Return TitleFont.Height + UiTheme.Px(UiTheme.SpaceSm, DeviceDpi)
        End Function

        Public Overrides ReadOnly Property DisplayRectangle As Rectangle
            Get
                Dim inner As Padding = Padding
                Dim top As Integer = inner.Top + TitleSpace()
                Return New Rectangle(
                    inner.Left,
                    top,
                    Math.Max(0, ClientSize.Width - inner.Horizontal),
                    Math.Max(0, ClientSize.Height - top - inner.Bottom))
            End Get
        End Property

        ' The base size reserves one caption line; reserve the card title instead.
        ' Reading the base display area first also measures that caption, which
        ' the base size depends on.
        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim baseCaption As Integer = MyBase.DisplayRectangle.Top - Padding.Top
            Dim size As Size = MyBase.GetPreferredSize(proposedSize)
            Return New Size(size.Width, Math.Max(0, size.Height - baseCaption + TitleSpace()))
        End Function

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            _titleFont?.Dispose()
            _titleFont = Nothing
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub OnTextChanged(e As EventArgs)
            MyBase.OnTextChanged(e)
            PerformLayout()
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(SurfaceBehind(Me))

            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim radius As Single = UiTheme.Px(UiTheme.CardRadius, DeviceDpi)
            Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F), radius)
                Using fill As New SolidBrush(BackColor)
                    g.FillPath(fill, path)
                End Using
                Using border As New Pen(UiTheme.CardBorder())
                    g.DrawPath(border, path)
                End Using
            End Using
            g.SmoothingMode = SmoothingMode.Default

            If Not String.IsNullOrEmpty(Text) Then
                Dim flags As TextFormatFlags = TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis
                If Not ShowKeyboardCues Then flags = flags Or TextFormatFlags.HidePrefix
                Dim bounds As New Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), TitleFont.Height)
                TextRenderer.DrawText(g, Text, TitleFont, bounds, If(Enabled, ForeColor, UiTheme.MutedText()), flags)
            End If

        End Sub

        ' The first opaque color behind a control, for the corners of a
        ' rounded surface.
        Friend Shared Function SurfaceBehind(control As Control) As Color
            Dim parent As Control = control.Parent
            While parent IsNot Nothing AndAlso parent.BackColor.A < 255
                parent = parent.Parent
            End While
            Return If(parent Is Nothing, UiTheme.BoardBackground(), parent.BackColor)
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _titleFont?.Dispose()
                _titleFont = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
