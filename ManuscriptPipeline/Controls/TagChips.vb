Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Draws one tag as a small rounded chip in its palette color (#64).
    Friend NotInheritable Class TagChipPainter

        Private Sub New()
        End Sub

        Public Shared Function Measure(text As String, font As Font, dpi As Integer) As Size
            Dim size As Size = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
            Return New Size(size.Width + UiTheme.Px(14, dpi), size.Height + UiTheme.Px(4, dpi))
        End Function

        Public Shared Sub Draw(g As Graphics, bounds As Rectangle, text As String, palette As TagPalette, font As Font)
            Dim dark As Boolean = UiTheme.IsDark()
            g.SmoothingMode = SmoothingMode.AntiAlias
            Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), bounds.Height / 2.0F),
                  fill As New SolidBrush(WorkTypeService.Background(palette, dark))
                g.FillPath(fill, path)
            End Using
            TextRenderer.DrawText(g, text, font, bounds, WorkTypeService.Foreground(palette, dark),
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        End Sub

    End Class


    ' A card's tags on one line: as many as fit, then "+N" (#64).
    Friend Class TagStrip
        Inherits Control

        Private _tags As IReadOnlyList(Of String) = Array.Empty(Of String)()
        Private _library As AuthorLibraryData = Nothing

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            AccessibleRole = AccessibleRole.StaticText
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Tags As IReadOnlyList(Of String)
            Get
                Return _tags
            End Get
            Set(value As IReadOnlyList(Of String))
                _tags = If(value, Array.Empty(Of String)())
                AccessibleName = If(_tags.Count = 0, String.Empty, "Tags: " & String.Join(", ", _tags))
                Invalidate()
            End Set
        End Property

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Library As AuthorLibraryData
            Get
                Return _library
            End Get
            Set(value As AuthorLibraryData)
                _library = value
                Invalidate()
            End Set
        End Property

        ' The chips that fit in a width, with "+N" when some do not.
        Private Function Layout(width As Integer) As List(Of (Text As String, Bounds As Rectangle, Palette As TagPalette))

            Dim result As New List(Of (Text As String, Bounds As Rectangle, Palette As TagPalette))()
            Dim gap As Integer = UiTheme.Px(4, DeviceDpi)
            Dim x As Integer = 0

            For index As Integer = 0 To _tags.Count - 1
                Dim size As Size = TagChipPainter.Measure(_tags(index), Font, DeviceDpi)
                Dim remaining As Integer = _tags.Count - index - 1
                Dim moreWidth As Integer = If(remaining > 0, TagChipPainter.Measure("+" & remaining.ToString(), Font, DeviceDpi).Width + gap, 0)
                If x + size.Width + moreWidth > width Then
                    Dim more As String = "+" & (_tags.Count - index).ToString()
                    Dim moreSize As Size = TagChipPainter.Measure(more, Font, DeviceDpi)
                    If x + moreSize.Width <= width Then result.Add((more, New Rectangle(x, 0, moreSize.Width, moreSize.Height), TagPalette.Slate))
                    Exit For
                End If
                result.Add((_tags(index), New Rectangle(x, 0, size.Width, size.Height), WorkTypeService.ColorOf(_tags(index), _library)))
                x += size.Width + gap
            Next

            Return result

        End Function

        ' The width the chips need, up to a limit.
        Public Function PreferredWidth(maximum As Integer) As Integer
            Dim chips = Layout(maximum)
            Return If(chips.Count = 0, 0, chips.Last().Bounds.Right)
        End Function

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Return New Size(PreferredWidth(If(proposedSize.Width > 0, proposedSize.Width, Integer.MaxValue)),
                            TagChipPainter.Measure("Ag", Font, DeviceDpi).Height)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            For Each chip In Layout(Width)
                Dim bounds As Rectangle = chip.Bounds
                bounds.Offset(0, (Height - bounds.Height) \ 2)
                TagChipPainter.Draw(e.Graphics, bounds, chip.Text, chip.Palette, Font)
            Next
        End Sub

    End Class


    ' Edits a manuscript's tags: chips with remove and color, and a box that
    ' adds a tag on Enter or comma, suggesting tags already in the library.
    Friend Class TagEditor
        Inherits FlowLayoutPanel

        Private ReadOnly _input As New TextBox()
        Private _tags As List(Of String) = New List(Of String)()
        Private _library As AuthorLibraryData = Nothing

        ' Raised after a tag is added or removed, or its color changes.
        Public Event TagsChanged As EventHandler
        Public Event ColorChanged As EventHandler

        ' Not AutoSize: a wrapping panel measured before it knows its width
        ' asks for one line per chip. Its height follows its actual width.
        Public Sub New()
            AutoSize = False
            WrapContents = True
            Margin = New Padding(0)
            AccessibleName = "Tags"

            _input.PlaceholderText = "Add a tag"
            _input.AccessibleName = "Add a tag"
            _input.AccessibleDescription = "Type a tag and press Enter. Backspace in an empty box removes the last tag."
            _input.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            _input.AutoCompleteSource = AutoCompleteSource.CustomSource
            _input.Margin = New Padding(0, 3, 0, 3)
            AddHandler _input.KeyDown, AddressOf InputKeyDown
            AddHandler _input.KeyPress,
                Sub(sender, e)
                    If e.KeyChar = ","c OrElse e.KeyChar = ";"c Then
                        e.Handled = True
                        Commit()
                    End If
                End Sub
            AddHandler _input.Leave, Sub(sender, e) Commit()
            Controls.Add(_input)
        End Sub

        Friend ReadOnly Property Input As TextBox
            Get
                Return _input
            End Get
        End Property

        ' Edits this list in place.
        Public Sub Bind(tags As List(Of String), library As AuthorLibraryData, suggestions As IEnumerable(Of String))
            _tags = tags
            _library = library
            _input.AutoCompleteCustomSource.Clear()
            _input.AutoCompleteCustomSource.AddRange(If(suggestions, Enumerable.Empty(Of String)()).ToArray())
            _input.Width = Math.Max(LogicalToDeviceUnits(140), TextRenderer.MeasureText(_input.PlaceholderText, _input.Font).Width + LogicalToDeviceUnits(24))
            Rebuild()
        End Sub

        Protected Overrides Sub OnSizeChanged(e As EventArgs)
            MyBase.OnSizeChanged(e)
            FitHeight()
        End Sub

        Private Sub FitHeight()
            If Width <= 0 Then Return
            Dim needed As Integer = MyBase.GetPreferredSize(New Size(Width, 0)).Height
            If needed > 0 AndAlso needed <> Height Then Height = needed
        End Sub

        Private Sub InputKeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode = Keys.Enter Then
                e.SuppressKeyPress = True
                e.Handled = True
                Commit()
            ElseIf e.KeyCode = Keys.Back AndAlso _input.TextLength = 0 AndAlso _tags.Count > 0 Then
                e.SuppressKeyPress = True
                RemoveTag(_tags.Last())
            End If
        End Sub

        Friend Sub Commit()
            Dim value As String = WorkTypeService.NormalizeTag(_input.Text)
            _input.Clear()
            If value.Length = 0 OrElse _tags.Contains(value, StringComparer.CurrentCultureIgnoreCase) Then Return
            _tags.Add(value)
            Rebuild()
            RaiseEvent TagsChanged(Me, EventArgs.Empty)
            _input.Focus()
        End Sub

        Friend Sub RemoveTag(tag As String)
            If _tags.RemoveAll(Function(item) String.Equals(item, tag, StringComparison.CurrentCultureIgnoreCase)) = 0 Then Return
            Rebuild()
            RaiseEvent TagsChanged(Me, EventArgs.Empty)
            _input.Focus()
        End Sub

        Private Sub Rebuild()
            SuspendLayout()
            For Each chip As Control In Controls.OfType(Of TagChipButton)().Cast(Of Control)().ToList()
                Controls.Remove(chip)
                chip.Dispose()
            Next
            Dim index As Integer = 0
            For Each tag As String In _tags
                Dim chip As New TagChipButton(tag, WorkTypeService.ColorOf(tag, _library)) With {.Font = Font}
                AddHandler chip.RemoveRequested, Sub(sender, e) RemoveTag(DirectCast(sender, TagChipButton).TagText)
                AddHandler chip.ColorRequested,
                    Sub(sender, color)
                        If _library Is Nothing Then Return
                        WorkTypeService.SetColor(_library, DirectCast(sender, TagChipButton).TagText, color)
                        Rebuild()
                        RaiseEvent ColorChanged(Me, EventArgs.Empty)
                    End Sub
                Controls.Add(chip)
                Controls.SetChildIndex(chip, index)
                index += 1
            Next
            ResumeLayout(True)
            FitHeight()
        End Sub

    End Class


    ' One tag in the editor: Delete or the × removes it; the menu sets its color.
    Friend Class TagChipButton
        Inherits Control

        Private ReadOnly _palette As TagPalette
        Private _hot As Boolean

        Public ReadOnly Property TagText As String

        Public Event RemoveRequested As EventHandler
        Public Event ColorRequested As EventHandler(Of TagPalette)

        Public Sub New(tag As String, palette As TagPalette)
            TagText = tag
            _palette = palette
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.Selectable Or ControlStyles.SupportsTransparentBackColor, True)
            TabStop = True
            Margin = New Padding(0, 3, 6, 3)
            AccessibleRole = AccessibleRole.PushButton
            AccessibleName = "Tag " & tag
            AccessibleDescription = "Press Delete to remove. Open the context menu to choose a color."

            Dim menu As New ContextMenuStrip()
            Dim colors As New ToolStripMenuItem("Color")
            For Each value As TagPalette In [Enum].GetValues(GetType(TagPalette))
                Dim choice As TagPalette = value
                Dim item As New ToolStripMenuItem(value.ToString()) With {.Checked = value = palette}
                AddHandler item.Click, Sub(sender, e) RaiseEvent ColorRequested(Me, choice)
                colors.DropDownItems.Add(item)
            Next
            menu.Items.Add(colors)
            menu.Items.Add("Remove Tag", Nothing, Sub(sender, e) RaiseEvent RemoveRequested(Me, EventArgs.Empty))
            ContextMenuStrip = menu
            AddHandler Disposed, Sub(sender, e) menu.Dispose()
        End Sub

        Private ReadOnly Property RemoveWidth As Integer
            Get
                Return TextRenderer.MeasureText("×", Font).Width + UiTheme.Px(4, DeviceDpi)
            End Get
        End Property

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim chip As Size = TagChipPainter.Measure(TagText, Font, DeviceDpi)
            Return New Size(chip.Width + RemoveWidth, chip.Height + UiTheme.Px(2, DeviceDpi))
        End Function

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            Size = GetPreferredSize(Size.Empty)
        End Sub

        Protected Overrides Sub OnCreateControl()
            MyBase.OnCreateControl()
            Size = GetPreferredSize(Size.Empty)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim bounds As New Rectangle(0, 0, Width, Height)
            TagChipPainter.Draw(e.Graphics, bounds, String.Empty, _palette, Font)
            Dim ink As Color = WorkTypeService.Foreground(_palette, UiTheme.IsDark())
            Dim textBounds As New Rectangle(UiTheme.Px(7, DeviceDpi), 0, Width - RemoveWidth - UiTheme.Px(7, DeviceDpi), Height)
            TextRenderer.DrawText(e.Graphics, TagText, Font, textBounds, ink, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
            TextRenderer.DrawText(e.Graphics, "×", Font, New Rectangle(Width - RemoveWidth - UiTheme.Px(3, DeviceDpi), 0, RemoveWidth, Height), If(_hot, UiTheme.DangerColor(), ink),
                TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPadding)
            If Focused AndAlso ShowFocusCues Then
                Using pen As New Pen(UiTheme.AccentColor(), UiTheme.Px(2, DeviceDpi))
                    Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(1, 1, Width - 3, Height - 3), (Height - 3) / 2.0F)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End Using
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim hot As Boolean = e.X >= Width - RemoveWidth - UiTheme.Px(4, DeviceDpi)
            If hot <> _hot Then
                _hot = hot
                Cursor = If(hot, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hot = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If e.Button = MouseButtons.Left AndAlso e.X >= Width - RemoveWidth - UiTheme.Px(4, DeviceDpi) Then
                RaiseEvent RemoveRequested(Me, EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Delete OrElse e.KeyCode = Keys.Back Then
                e.Handled = True
                RaiseEvent RemoveRequested(Me, EventArgs.Empty)
            End If
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

    End Class

End Namespace
