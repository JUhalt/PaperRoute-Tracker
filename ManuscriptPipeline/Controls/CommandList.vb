Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Windows.Forms.Layout
Imports ManuscriptPipeline.Services

Namespace Controls

    ' The Import & Export page's commands (#133): each section's heading,
    ' then each command's button with what it does beside it, wrapping at
    ' the page's width. The buttons are real buttons; the headings and the
    ' descriptions are drawn, so the page makes 20 windows rather than 40,
    ' and the list lays itself out only when its width, font or DPI changes.
    ' A screen reader reads them in order: a heading, then each button and
    ' its description.
    Friend NotInheritable Class CommandList
        Inherits ScrollableControl

        Private Const DescriptionFlags As TextFormatFlags =
            TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.TextBoxControl

        Private Const HeadingFlags As TextFormatFlags =
            TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine

        ' A section's heading, or a command: its button and its description.
        Private NotInheritable Class Line
            Public Heading As String
            Public Button As Control
            Public Description As String
            ' Where the heading or the description is drawn, in content
            ' coordinates.
            Public Text As Rectangle
            Public Top As Integer
            Public Height As Integer
        End Class

        Private ReadOnly _lines As New List(Of Line)()
        Private _children As List(Of AccessibleObject) = Nothing
        Private ReadOnly _engine As New Arranger()
        Private _headingFont As Font = Nothing
        Private _buttonWidth As Integer = 0
        Private _arrangedWidth As Integer = -1
        Private _arrangedFont As Font = Nothing
        Private _arrangedDpi As Integer = 0

        ' Not a Tab stop itself, as the Panel it replaces was not: Tab goes
        ' straight to its buttons.
        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            AutoScroll = True
            BackColor = UiTheme.BoardBackground()
        End Sub

        ' The headings' font, which the page shares with the Board's
        ' headings; not owned here.
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property HeadingFont As Font
            Get
                Return If(_headingFont, Font)
            End Get
            Set(value As Font)
                _headingFont = value
                Rearrange()
            End Set
        End Property

        ' Every button's width; the descriptions start 14 pixels after it.
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property ButtonWidth As Integer
            Get
                Return _buttonWidth
            End Get
            Set(value As Integer)
                _buttonWidth = value
                Rearrange()
            End Set
        End Property

        Friend Sub AddSection(heading As String)
            _lines.Add(New Line With {.Heading = heading})
            _children = Nothing
            Rearrange()
        End Sub

        Friend Sub AddCommand(button As Control, description As String)
            _lines.Add(New Line With {.Button = button, .Description = description})
            _children = Nothing
            Controls.Add(button)
            Rearrange()
        End Sub

        ' The width the descriptions wrap at: what the page leaves beside the
        ' buttons, and never under 200 pixels.
        Friend Function DescriptionWidth() As Integer
            Return Math.Max(Px(200), ClientSize.Width - _buttonWidth - Px(40))
        End Function

        ' Where a command's description is drawn, in client coordinates.
        Friend Function DescriptionBounds(button As Control) As Rectangle
            Dim line As Line = _lines.FirstOrDefault(Function(candidate) candidate.Button Is button)
            If line Is Nothing Then Return Rectangle.Empty
            Return Offset(line.Text)
        End Function

        ' The descriptions, in order.
        Friend ReadOnly Property Descriptions As IReadOnlyList(Of String)
            Get
                Return _lines.Where(Function(line) line.Button IsNot Nothing).Select(Function(line) line.Description).ToList()
            End Get
        End Property

        Private Function Px(logicalPixels As Integer) As Integer
            Return UiTheme.Px(logicalPixels, DeviceDpi)
        End Function

        Private Function Offset(bounds As Rectangle) As Rectangle
            bounds.Offset(AutoScrollPosition)
            Return bounds
        End Function

        Private Sub Rearrange()
            _arrangedWidth = -1
            PerformLayout()
            Invalidate()
        End Sub


        ' ---------------------------------------------------------------
        ' Layout
        ' ---------------------------------------------------------------

        ' The list's own layout: the default would move the buttons by
        ' their anchors whenever the list is laid out.
        Public Overrides ReadOnly Property LayoutEngine As LayoutEngine
            Get
                Return _engine
            End Get
        End Property

        Private NotInheritable Class Arranger
            Inherits LayoutEngine

            Public Overrides Function Layout(container As Object, layoutEventArgs As LayoutEventArgs) As Boolean
                DirectCast(container, CommandList).Arrange()
                Return False
            End Function

        End Class

        ' As the page's table placed its controls: a heading 14 pixels below
        ' the command before it (none for the first) and 6 above the next; a
        ' command's button at the left and top of its row, and its description
        ' 14 pixels after the button, wrapped at DescriptionWidth and centered
        ' on the button's line; each row 8 pixels taller than the taller of
        ' the two. Only a change of width, font or DPI arranges the list again.
        Private Sub Arrange()

            If ClientSize.Width = _arrangedWidth AndAlso Font Is _arrangedFont AndAlso DeviceDpi = _arrangedDpi Then Return
            _arrangedWidth = ClientSize.Width
            _arrangedFont = Font
            _arrangedDpi = DeviceDpi

            Dim wrapWidth As Integer = DescriptionWidth()
            Dim textLeft As Integer = _buttonWidth + Px(14)
            Dim scroll As Point = AutoScrollPosition
            Dim y As Integer = 0

            For index As Integer = 0 To _lines.Count - 1
                Dim line As Line = _lines(index)
                If line.Button Is Nothing Then
                    y += If(index = 0, 0, Px(14))
                    line.Top = y
                    line.Height = HeadingFont.Height
                    line.Text = New Rectangle(0, y, TextRenderer.MeasureText(line.Heading, HeadingFont, Size.Empty, HeadingFlags).Width, line.Height)
                    y += line.Height + Px(6)
                Else
                    Dim text As Size = TextRenderer.MeasureText(line.Description, Font, New Size(wrapWidth, Integer.MaxValue), DescriptionFlags)
                    Dim buttonHeight As Integer = line.Button.Height
                    line.Top = y
                    line.Height = Math.Max(buttonHeight + Px(8), text.Height + Px(8))
                    line.Text = New Rectangle(textLeft, y + (line.Height - Px(8) - text.Height) \ 2, wrapWidth, text.Height)
                    Dim bounds As New Rectangle(scroll.X, y + scroll.Y, _buttonWidth, buttonHeight)
                    If line.Button.Bounds <> bounds Then line.Button.Bounds = bounds
                    y += line.Height
                End If
            Next

            Dim minimum As New Size(textLeft + Px(200) + Px(8), y)
            If AutoScrollMinSize <> minimum Then AutoScrollMinSize = minimum
            Invalidate()

        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            _arrangedWidth = -1
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
            MyBase.OnDpiChangedAfterParent(e)
            Rearrange()
        End Sub


        ' ---------------------------------------------------------------
        ' Painting
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            MyBase.OnPaint(e)
            Dim g As Graphics = e.Graphics
            Dim clip As Rectangle = e.ClipRectangle

            For Each line As Line In _lines
                Dim bounds As Rectangle = Offset(line.Text)
                If Not bounds.IntersectsWith(clip) Then Continue For
                If line.Button Is Nothing Then
                    TextRenderer.DrawText(g, line.Heading, HeadingFont, bounds, UiTheme.MutedText(), HeadingFlags)
                Else
                    TextRenderer.DrawText(g, line.Description, Font, bounds, UiTheme.SecondaryText(), DescriptionFlags)
                End If
            Next

        End Sub

        Protected Overrides Sub OnSystemColorsChanged(e As EventArgs)
            MyBase.OnSystemColorsChanged(e)
            Invalidate()
        End Sub


        ' ---------------------------------------------------------------
        ' What a screen reader sees
        ' ---------------------------------------------------------------

        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject
            Return New ListAccessibleObject(Me)
        End Function

        ' Each heading, then each button and its description, in order; made
        ' once, and again only after a section or a command is added.
        Private Function ReadableChildren() As List(Of AccessibleObject)
            If _children Is Nothing Then
                _children = New List(Of AccessibleObject)()
                For Each line As Line In _lines
                    Dim drawn As Line = line
                    If drawn.Button Is Nothing Then
                        _children.Add(New DrawnTextAccessibleObject(Me, _children.Count, Function() drawn.Heading, Function() Offset(drawn.Text)))
                    Else
                        _children.Add(drawn.Button.AccessibilityObject)
                        _children.Add(New DrawnTextAccessibleObject(Me, _children.Count, Function() drawn.Description, Function() Offset(drawn.Text)))
                    End If
                Next
            End If
            Return _children
        End Function

        Private NotInheritable Class ListAccessibleObject
            Inherits ControlAccessibleObject

            Private ReadOnly _list As CommandList

            Public Sub New(owner As CommandList)
                MyBase.New(owner)
                _list = owner
            End Sub

            Public Overrides Function GetChildCount() As Integer
                Return _list.ReadableChildren().Count
            End Function

            Public Overrides Function GetChild(index As Integer) As AccessibleObject
                Dim children As List(Of AccessibleObject) = _list.ReadableChildren()
                Return If(index >= 0 AndAlso index < children.Count, children(index), Nothing)
            End Function

        End Class

    End Class

End Namespace
