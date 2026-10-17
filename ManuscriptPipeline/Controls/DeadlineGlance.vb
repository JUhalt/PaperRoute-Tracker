Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' The Deadlines page's four counts across the top (overdue, due today,
    ' in the next 7 days, without a date), drawn as tiles in one window
    ' rather than a panel and two labels each (#133). The tiles wrap onto
    ' another line when the page is narrow. A screen reader sees a group
    ' with one line of text for each count.
    Friend NotInheritable Class DeadlineGlance
        Inherits Control

        Private Shared ReadOnly Captions As String() = {"overdue", "due today", "in the next 7 days", "without a date"}

        Private ReadOnly _counts As Integer() = {0, 0, 0, 0}
        Private _countFont As Font = Nothing

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            AutoSize = True
            AccessibleRole = AccessibleRole.Grouping
        End Sub

        ' Overdue, due today, in the next 7 days, and without a date.
        Friend ReadOnly Property Counts As IReadOnlyList(Of Integer)
            Get
                Return _counts
            End Get
        End Property

        Friend Sub SetCounts(overdue As Integer, dueToday As Integer, nextWeek As Integer, withoutDate As Integer)
            Dim values As Integer() = {overdue, dueToday, nextWeek, withoutDate}
            If values.SequenceEqual(_counts) Then Return
            values.CopyTo(_counts, 0)
            Invalidate()
            For index As Integer = 0 To 3
                DeadlineList.RaiseAccessibleEvent(Me, AccessibleEvents.NameChange, index)
            Next
        End Sub

        ' What each tile says, as a screen reader reads it: "3 overdue".
        Friend Function TileText(index As Integer) As String
            Return _counts(index).ToString(CultureInfo.CurrentCulture) & " " & Captions(index)
        End Function

        Private ReadOnly Property CountFont As Font
            Get
                If _countFont Is Nothing Then _countFont = New Font(Font.FontFamily, Font.SizeInPoints * 1.45F, FontStyle.Bold)
                Return _countFont
            End Get
        End Property

        Private Function Px(logicalPixels As Integer) As Integer
            Return UiTheme.Px(logicalPixels, DeviceDpi)
        End Function

        ' A tile: 12 pixels of padding at the sides and 8 above and below
        ' the count and its caption, and never narrower than 132 pixels.
        Private Function TileSize(index As Integer) As Size
            Dim width As Integer = Math.Max(Px(132), TextRenderer.MeasureText(Captions(index), Font).Width + Px(24))
            Dim height As Integer = Px(16) + TextRenderer.MeasureText("0", CountFont).Height + TextRenderer.MeasureText(Captions(index), Font).Height
            Return New Size(width, height)
        End Function

        ' The tiles left to right 10 pixels apart, onto a new line when the
        ' next would pass width.
        Private Function TileBounds(width As Integer) As Rectangle()
            Dim tiles(3) As Rectangle
            Dim x As Integer = 0
            Dim y As Integer = 0
            Dim line As Integer = 0
            For index As Integer = 0 To 3
                Dim size As Size = TileSize(index)
                If x > 0 AndAlso x + size.Width > width Then
                    x = 0
                    y += line
                    line = 0
                End If
                tiles(index) = New Rectangle(New Point(x, y), size)
                x += size.Width + Px(10)
                line = Math.Max(line, size.Height)
            Next
            Return tiles
        End Function

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim width As Integer = If(proposedSize.Width > 0 AndAlso proposedSize.Width < Integer.MaxValue / 2, proposedSize.Width, Integer.MaxValue)
            Dim tiles As Rectangle() = TileBounds(width)
            Dim right As Integer = tiles.Max(Function(tile) tile.Right)
            Dim bottom As Integer = tiles.Max(Function(tile) tile.Bottom)
            Return New Size(If(width = Integer.MaxValue, right, width), bottom)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            MyBase.OnPaint(e)
            Dim g As Graphics = e.Graphics
            Dim inks As Color() = {UiTheme.DangerColor(), UiTheme.WarningColor(), UiTheme.PrimaryText(), UiTheme.SecondaryText()}
            Dim tiles As Rectangle() = TileBounds(ClientSize.Width)
            Dim radius As Single = UiTheme.Px(UiTheme.ControlRadius, DeviceDpi)

            For index As Integer = 0 To 3
                Dim tile As Rectangle = tiles(index)
                g.SmoothingMode = SmoothingMode.AntiAlias
                Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(tile.X + 0.5F, tile.Y + 0.5F, tile.Width - 1.0F, tile.Height - 1.0F), radius)
                    Using fill As New SolidBrush(UiTheme.CardBackground())
                        g.FillPath(fill, path)
                    End Using
                    Using border As New Pen(UiTheme.CardBorder(), 1.0F)
                        g.DrawPath(border, path)
                    End Using
                End Using
                g.SmoothingMode = SmoothingMode.Default

                Dim count As String = _counts(index).ToString(CultureInfo.CurrentCulture)
                Dim countHeight As Integer = TextRenderer.MeasureText("0", CountFont).Height
                TextRenderer.DrawText(g, count, CountFont, New Point(tile.X + Px(12), tile.Y + Px(8)), inks(index), TextFormatFlags.NoPrefix)
                TextRenderer.DrawText(g, Captions(index), Font, New Point(tile.X + Px(12), tile.Y + Px(8) + countHeight), UiTheme.SecondaryText(), TextFormatFlags.NoPrefix)
            Next

        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            _countFont?.Dispose()
            _countFont = Nothing
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub OnSystemColorsChanged(e As EventArgs)
            MyBase.OnSystemColorsChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject
            Return New GlanceAccessibleObject(Me)
        End Function

        Private NotInheritable Class GlanceAccessibleObject
            Inherits ControlAccessibleObject

            Private ReadOnly _glance As DeadlineGlance
            Private ReadOnly _tiles As AccessibleObject() = New AccessibleObject(3) {}

            Public Sub New(owner As DeadlineGlance)
                MyBase.New(owner)
                _glance = owner
            End Sub

            Public Overrides ReadOnly Property Role As AccessibleRole
                Get
                    Return AccessibleRole.Grouping
                End Get
            End Property

            Public Overrides Function GetChildCount() As Integer
                Return 4
            End Function

            ' A line of text for each tile, read as it is now.
            Public Overrides Function GetChild(index As Integer) As AccessibleObject
                If index < 0 OrElse index > 3 Then Return Nothing
                If _tiles(index) Is Nothing Then
                    _tiles(index) = New DrawnTextAccessibleObject(_glance, index, Function() _glance.TileText(index),
                                                                  Function() _glance.TileBounds(_glance.ClientSize.Width)(index))
                End If
                Return _tiles(index)
            End Function

        End Class

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _countFont?.Dispose()
                _countFont = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
