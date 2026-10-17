Imports System.ComponentModel
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    Friend Enum DeadlineEntryKind
        Heading
        Row
        Message
        DoneToggle
    End Enum

    ' One line of the Deadlines list: a group's heading, a row, the
    ' sentence an empty list shows, or the line that shows or hides what was
    ' done lately.
    Friend NotInheritable Class DeadlineEntry

        Private Sub New(kind As DeadlineEntryKind, text As String, group As DeadlineGroup, row As DeadlineRow, linkText As String, name As String)
            Me.Kind = kind
            Me.Text = text
            Me.Group = group
            Me.Row = row
            Me.LinkText = linkText
            Me.Name = name
        End Sub

        Public ReadOnly Property Kind As DeadlineEntryKind
        Public ReadOnly Property Text As String
        Public ReadOnly Property Group As DeadlineGroup
        Public ReadOnly Property Row As DeadlineRow

        ' The Done line's link, the last words of its text, and what a
        ' screen reader calls it.
        Public ReadOnly Property LinkText As String
        Public ReadOnly Property Name As String

        ' Where the list put the entry, in its content's coordinates.
        Friend Property Top As Integer
        Friend Property Height As Integer

        Public Shared Function Heading(text As String, group As DeadlineGroup) As DeadlineEntry
            Return New DeadlineEntry(DeadlineEntryKind.Heading, text, group, Nothing, Nothing, Nothing)
        End Function

        Public Shared Function ForRow(row As DeadlineRow) As DeadlineEntry
            Return New DeadlineEntry(DeadlineEntryKind.Row, Nothing, row.Item.Group, row, Nothing, Nothing)
        End Function

        Public Shared Function Message(text As String) As DeadlineEntry
            Return New DeadlineEntry(DeadlineEntryKind.Message, text, DeadlineGroup.NoDate, Nothing, Nothing, Nothing)
        End Function

        Public Shared Function DoneToggle(text As String, linkText As String, name As String) As DeadlineEntry
            Return New DeadlineEntry(DeadlineEntryKind.DoneToggle, text, DeadlineGroup.Done, Nothing, linkText, name)
        End Function

    End Class

    ' The Deadlines page's list (#133): every heading, row, button and line
    ' drawn in one window that scrolls itself, rather than a control per row
    ' (221 rows on a 500-manuscript library were about 1,100 windows, and
    ' each window costs milliseconds to make on a busy desktop). It keeps the
    ' rows' keyboard map: Tab reaches each row, its buttons and its "⋯" in
    ' turn, then the Done line; Up and Down move between rows; Left and
    ' Right move across the rows and the Done line; on a button the arrows
    ' move between the row's buttons; Enter runs a row's first action or
    ' presses a button; Space presses a button; and the context-menu key
    ' opens a row's actions. A screen reader sees one list whose children,
    ' in reading order, are each heading, each row as a list item followed by
    ' its buttons, the empty list's sentence, and the Done link.
    Friend NotInheritable Class DeadlineList
        Inherits ScrollableControl

        Private Const WM_CONTEXTMENU As Integer = &H7B

        Private Const MessageFlags As TextFormatFlags =
            TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.TextBoxControl

        Private Const LineFlags As TextFormatFlags =
            TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine

        Private Const HeadingFlags As TextFormatFlags =
            TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis

        ' A place in the list: an entry, and for a row, the row itself or one
        ' of its buttons.
        Private Structure Spot

            Public Sub New(entry As Integer, part As Integer)
                Me.Entry = entry
                Me.Part = part
            End Sub

            Public ReadOnly Entry As Integer
            Public ReadOnly Part As Integer

            Public Shared ReadOnly None As New Spot(-1, DeadlineRow.NoPart)

            Public Function IsSameAs(other As Spot) As Boolean
                Return Entry = other.Entry AndAlso Part = other.Part
            End Function

        End Structure

        Private ReadOnly _entries As New List(Of DeadlineEntry)()
        Private ReadOnly _rows As New List(Of DeadlineRow)()
        Private ReadOnly _rowEntries As New List(Of Integer)()

        ' The keyboard's stops in Tab order, and the screen reader's
        ' children in reading order; _stopChild maps the one to the other.
        Private ReadOnly _stops As New List(Of Spot)()
        Private ReadOnly _readable As New List(Of Spot)()
        Private _stopChild As Integer() = {}
        Private _children As AccessibleObject() = {}

        Private ReadOnly _menu As New ContextMenuStrip()
        Private _menuRow As DeadlineRow = Nothing
        Private _metrics As DeadlineMetrics = Nothing
        Private _headingFont As Font = Nothing
        Private _arrangedWidth As Integer = -1
        Private _current As Integer = -1
        Private _hover As Spot = Spot.None
        Private _pressed As Spot = Spot.None
        Private _down As Spot = Spot.None
        Private _downGeneration As Integer = -1
        Private _keyPressed As Boolean = False
        Private _generation As Integer = 0
        Private _signature As String() = {}
        Private _doneName As String = Nothing

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            SetStyle(ControlStyles.ContainerControl, False)
            AutoScroll = True
            TabStop = False
            BackColor = UiTheme.BoardBackground()
            AddHandler _menu.Opening, AddressOf MenuOpening
        End Sub

        ' Clicking the Done line's Show or Hide.
        Friend Event DoneToggleClicked As EventHandler

        ' The group headings' font, which the page shares with the Board's
        ' headings; not owned here.
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property HeadingFont As Font
            Get
                Return If(_headingFont, Font)
            End Get
            Set(value As Font)
                _headingFont = value
                Arrange(remeasure:=True)
                Invalidate()
            End Set
        End Property

        Friend ReadOnly Property Entries As IReadOnlyList(Of DeadlineEntry)
            Get
                Return _entries
            End Get
        End Property

        ' The rows in order, open rows first.
        Friend ReadOnly Property Rows As IReadOnlyList(Of DeadlineRow)
            Get
                Return _rows
            End Get
        End Property

        ' The row the keyboard is on, or Nothing.
        Friend ReadOnly Property FocusedRow As DeadlineRow
            Get
                If _current < 0 Then Return Nothing
                Return _entries(_stops(_current).Entry).Row
            End Get
        End Property

        ' The row itself (DeadlineRow.ItemPart), a button's index, the "⋯"
        ' button (the row's MorePart), or NoPart.
        Friend ReadOnly Property FocusedPart As Integer
            Get
                Return If(_current < 0, DeadlineRow.NoPart, _stops(_current).Part)
            End Get
        End Property

        ' Whether the keyboard is on the Done line.
        Friend ReadOnly Property DoneLineFocused As Boolean
            Get
                Return _current >= 0 AndAlso _entries(_stops(_current).Entry).Kind = DeadlineEntryKind.DoneToggle
            End Get
        End Property

        ' The menu a row's "⋯" button and the context-menu key open.
        Friend ReadOnly Property Menu As ContextMenuStrip
            Get
                Return _menu
            End Get
        End Property

        ' Where a row's button (by index, or the row's MorePart) or the row
        ' itself (ItemPart) is drawn, in client coordinates.
        Friend Function ButtonBounds(row As DeadlineRow, part As Integer) As Rectangle
            Dim index As Integer = _rows.IndexOf(row)
            If index < 0 Then Return Rectangle.Empty
            Return SpotBounds(New Spot(_rowEntries(index), part))
        End Function

        ' The Done line's Show or Hide, in client coordinates.
        Friend Function DoneLinkBounds() As Rectangle
            Dim index As Integer = _entries.FindIndex(Function(entry) entry.Kind = DeadlineEntryKind.DoneToggle)
            Return If(index < 0, Rectangle.Empty, LinkBounds(index))
        End Function

        ' Back to the top with nothing chosen, as a page opened anew.
        Friend Sub ResetView()
            SetCurrent(-1)
            AutoScrollPosition = Point.Empty
            Invalidate()
        End Sub


        ' ---------------------------------------------------------------
        ' Filling
        ' ---------------------------------------------------------------

        ' Replaces what the list shows. When the list has focus it stays on
        ' the same row by position (or on the Done line), as the page did when
        ' it rebuilt its rows; otherwise nothing is chosen.
        Friend Sub SetEntries(entries As IEnumerable(Of DeadlineEntry))

            Dim hadFocus As Boolean = Focused
            Dim focusedRow As Integer = -1
            Dim onDoneLine As Boolean = False
            If hadFocus AndAlso _current >= 0 Then
                onDoneLine = DoneLineFocused
                focusedRow = _rowEntries.IndexOf(_stops(_current).Entry)
            End If

            _generation += 1
            _entries.Clear()
            _entries.AddRange(entries)
            _rows.Clear()
            _rowEntries.Clear()
            _stops.Clear()
            _readable.Clear()
            _current = -1
            _hover = Spot.None
            _pressed = Spot.None
            _down = Spot.None
            _keyPressed = False

            Dim stopChild As New List(Of Integer)()
            Dim signature(_entries.Count - 1) As String
            Dim doneName As String = Nothing

            For index As Integer = 0 To _entries.Count - 1
                Dim entry As DeadlineEntry = _entries(index)
                Select Case entry.Kind
                    Case DeadlineEntryKind.Row
                        _rows.Add(entry.Row)
                        _rowEntries.Add(index)
                        AddStop(stopChild, New Spot(index, DeadlineRow.ItemPart))
                        For part As Integer = 0 To entry.Row.Buttons.Count - 1
                            AddStop(stopChild, New Spot(index, part))
                        Next
                        If entry.Row.HasMore Then AddStop(stopChild, New Spot(index, entry.Row.MorePart))
                        signature(index) = "R" & entry.Row.AccessibleName & "|" & String.Join("|", entry.Row.ActionTexts)
                    Case DeadlineEntryKind.DoneToggle
                        AddStop(stopChild, New Spot(index, DeadlineRow.ItemPart))
                        doneName = entry.Name
                        signature(index) = "D"
                    Case Else
                        _readable.Add(New Spot(index, DeadlineRow.ItemPart))
                        signature(index) = If(entry.Kind = DeadlineEntryKind.Heading, "H", "M") & entry.Text
                End Select
            Next

            _stopChild = stopChild.ToArray()
            _children = New AccessibleObject(_readable.Count - 1) {}
            TabStop = _stops.Count > 0

            Arrange(remeasure:=True)

            If hadFocus Then
                If onDoneLine Then _current = _stops.FindIndex(Function(spot) _entries(spot.Entry).Kind = DeadlineEntryKind.DoneToggle)
                If _current < 0 AndAlso (focusedRow >= 0 OrElse onDoneLine) AndAlso _rows.Count > 0 Then
                    Dim row As Integer = If(focusedRow >= 0, Math.Min(focusedRow, _rows.Count - 1), _rows.Count - 1)
                    _current = StopOf(New Spot(_rowEntries(row), DeadlineRow.ItemPart))
                End If
            End If

            Dim reordered As Boolean = Not signature.SequenceEqual(_signature)
            Dim doneRenamed As Boolean = doneName IsNot Nothing AndAlso _doneName IsNot Nothing AndAlso doneName <> _doneName
            _signature = signature
            _doneName = doneName

            Invalidate()

            If IsHandleCreated Then
                If reordered Then RaiseAccessibleEvent(Me, AccessibleEvents.Reorder, -1)
                If doneRenamed Then
                    Dim doneStop As Integer = _stops.FindIndex(Function(spot) _entries(spot.Entry).Kind = DeadlineEntryKind.DoneToggle)
                    If doneStop >= 0 Then RaiseAccessibleEvent(Me, AccessibleEvents.NameChange, _stopChild(doneStop))
                End If
            End If

            If _current >= 0 Then
                ScrollStopIntoView(_current)
                NotifyFocus()
            End If

        End Sub

        Private Sub AddStop(stopChild As List(Of Integer), spot As Spot)
            stopChild.Add(_readable.Count)
            _stops.Add(spot)
            _readable.Add(spot)
        End Sub

        Private ReadOnly Property Metrics As DeadlineMetrics
            Get
                If _metrics Is Nothing Then _metrics = New DeadlineMetrics(Font, DeviceDpi)
                Return _metrics
            End Get
        End Property

        Private Function ContentWidth() As Integer
            Return Math.Max(0, ClientSize.Width - Metrics.Px(4))
        End Function

        ' Stacks the entries as the page's table stacked its controls: a
        ' heading 10 pixels below what comes before it and 6 above its rows,
        ' each row 6 pixels above the next, the empty list's sentence 18 above
        ' and below, the Done line 12 above and 6 below, and 12 at the bottom.
        ' Rows are measured again only when they are new or the font or DPI
        ' changed; a change of width re-wraps only the sentence.
        Private Sub Arrange(remeasure As Boolean)

            Dim measures As DeadlineMetrics = Metrics
            Dim width As Integer = ContentWidth()
            Dim y As Integer = 0

            For Each entry As DeadlineEntry In _entries
                Select Case entry.Kind
                    Case DeadlineEntryKind.Heading
                        y += measures.Px(10)
                        entry.Top = y
                        entry.Height = HeadingFont.Height
                        y += entry.Height + measures.Px(6)
                    Case DeadlineEntryKind.Row
                        If remeasure OrElse entry.Height = 0 Then entry.Height = entry.Row.MeasureHeight(measures)
                        entry.Top = y
                        y += entry.Height + measures.Px(6)
                    Case DeadlineEntryKind.Message
                        y += measures.Px(18)
                        entry.Top = y
                        entry.Height = TextRenderer.MeasureText(entry.Text, measures.Regular, New Size(Math.Max(1, width), Integer.MaxValue), MessageFlags).Height
                        y += entry.Height + measures.Px(18)
                    Case Else
                        y += measures.Px(12)
                        entry.Top = y
                        entry.Height = measures.RegularHeight
                        y += entry.Height + measures.Px(6)
                End Select
            Next

            y += measures.Px(12)
            _arrangedWidth = ClientSize.Width

            Dim minimum As New Size(0, y)
            If AutoScrollMinSize <> minimum Then AutoScrollMinSize = minimum

        End Sub

        Protected Overrides Sub OnClientSizeChanged(e As EventArgs)
            MyBase.OnClientSizeChanged(e)
            If ClientSize.Width <> _arrangedWidth AndAlso _entries.Count > 0 Then
                Arrange(remeasure:=False)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            ResetMetrics()
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
            MyBase.OnDpiChangedAfterParent(e)
            ResetMetrics()
        End Sub

        Private Sub ResetMetrics()
            _metrics?.Dispose()
            _metrics = Nothing
            If _entries.Count > 0 Then Arrange(remeasure:=True)
            Invalidate()
        End Sub


        ' ---------------------------------------------------------------
        ' Geometry
        ' ---------------------------------------------------------------

        Private Function EntryBounds(index As Integer) As Rectangle
            Dim entry As DeadlineEntry = _entries(index)
            Return New Rectangle(0, entry.Top + AutoScrollPosition.Y, ContentWidth(), entry.Height)
        End Function

        Private Function SpotBounds(spot As Spot) As Rectangle
            If spot.Entry < 0 OrElse spot.Entry >= _entries.Count Then Return Rectangle.Empty
            Dim entry As DeadlineEntry = _entries(spot.Entry)
            Dim bounds As Rectangle = EntryBounds(spot.Entry)
            If entry.Kind = DeadlineEntryKind.Row AndAlso spot.Part >= 0 Then
                Return entry.Row.ButtonBounds(bounds, Metrics)(spot.Part)
            End If
            If entry.Kind = DeadlineEntryKind.DoneToggle Then Return LinkBounds(spot.Entry)
            Return bounds
        End Function

        ' The Done line's Show or Hide: the end of the line.
        Private Function LinkBounds(index As Integer) As Rectangle
            Dim entry As DeadlineEntry = _entries(index)
            Dim bounds As Rectangle = EntryBounds(index)
            Dim lineWidth As Integer = TextRenderer.MeasureText(entry.Text, Metrics.Regular, Size.Empty, LineFlags).Width
            Dim linkWidth As Integer = TextRenderer.MeasureText(entry.LinkText, Metrics.Link, Size.Empty, LineFlags).Width
            Return New Rectangle(bounds.Left + lineWidth - linkWidth, bounds.Top, linkWidth, bounds.Height)
        End Function

        ' The first entry that ends below y, in content coordinates.
        Private Function FirstEntryEndingBelow(y As Integer) As Integer
            Dim low As Integer = 0
            Dim high As Integer = _entries.Count
            While low < high
                Dim middle As Integer = (low + high) \ 2
                If _entries(middle).Top + _entries(middle).Height <= y Then
                    low = middle + 1
                Else
                    high = middle
                End If
            End While
            Return low
        End Function

        Private Function HitTest(point As Point) As Spot

            Dim y As Integer = point.Y - AutoScrollPosition.Y
            Dim index As Integer = FirstEntryEndingBelow(y)
            If index >= _entries.Count OrElse y < _entries(index).Top OrElse point.X < 0 OrElse point.X >= ContentWidth() Then
                Return Spot.None
            End If

            Dim entry As DeadlineEntry = _entries(index)
            Select Case entry.Kind
                Case DeadlineEntryKind.Row
                    Dim buttons As Rectangle() = entry.Row.ButtonBounds(EntryBounds(index), Metrics)
                    For part As Integer = 0 To entry.Row.Buttons.Count - 1
                        If buttons(part).Contains(point) Then Return New Spot(index, part)
                    Next
                    If entry.Row.HasMore AndAlso buttons(entry.Row.MorePart).Contains(point) Then Return New Spot(index, entry.Row.MorePart)
                    Return New Spot(index, DeadlineRow.ItemPart)
                Case DeadlineEntryKind.DoneToggle
                    If LinkBounds(index).Contains(point) Then Return New Spot(index, DeadlineRow.ItemPart)
            End Select

            Return Spot.None

        End Function

        Private Function IsButton(spot As Spot) As Boolean
            Return spot.Entry >= 0 AndAlso spot.Part >= 0 AndAlso _entries(spot.Entry).Kind = DeadlineEntryKind.Row
        End Function

        Private Function StopOf(spot As Spot) As Integer
            Return _stops.FindIndex(Function(candidate) candidate.IsSameAs(spot))
        End Function

        Private Sub InvalidateEntry(index As Integer)
            If index < 0 OrElse index >= _entries.Count Then Return
            Dim bounds As Rectangle = EntryBounds(index)
            bounds.Inflate(2, 2)
            Invalidate(bounds)
        End Sub

        ' Brings a stop into view as focusing a window brought that window
        ' into view: a button by its own bounds, so a button already in view
        ' never moves (and a click on it lands where it was pressed); a row or
        ' the Done line by its whole entry.
        Private Sub ScrollStopIntoView(index As Integer)
            Dim spot As Spot = _stops(index)
            If IsButton(spot) Then
                Dim bounds As Rectangle = SpotBounds(spot)
                ScrollIntoView(bounds.Top - AutoScrollPosition.Y, bounds.Height)
            Else
                Dim entry As DeadlineEntry = _entries(spot.Entry)
                ScrollIntoView(entry.Top, entry.Height)
            End If
        End Sub

        ' Scrolls as little as shows the span, in content coordinates, or its
        ' top when it is taller than the list.
        Private Sub ScrollIntoView(top As Integer, height As Integer)
            Dim offset As Integer = -AutoScrollPosition.Y
            Dim view As Integer = ClientSize.Height
            Dim target As Integer = offset
            If top < offset Then
                target = top
            ElseIf top + height > offset + view Then
                target = Math.Min(top, top + height - view)
            End If
            If target <> offset Then AutoScrollPosition = New Point(0, target)
        End Sub


        ' ---------------------------------------------------------------
        ' Painting
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            MyBase.OnPaint(e)
            If _entries.Count = 0 Then Return

            Dim g As Graphics = e.Graphics
            Dim measures As DeadlineMetrics = Metrics
            Dim offset As Integer = AutoScrollPosition.Y
            Dim clip As Rectangle = e.ClipRectangle

            For index As Integer = FirstEntryEndingBelow(clip.Top - offset) To _entries.Count - 1
                Dim entry As DeadlineEntry = _entries(index)
                If entry.Top + offset >= clip.Bottom Then Exit For
                Dim bounds As Rectangle = EntryBounds(index)
                Select Case entry.Kind
                    Case DeadlineEntryKind.Heading
                        TextRenderer.DrawText(g, entry.Text, HeadingFont, bounds,
                                              If(entry.Group = DeadlineGroup.Overdue, UiTheme.DangerColor(),
                                                 If(entry.Group = DeadlineGroup.Today, UiTheme.WarningColor(), UiTheme.MutedText())),
                                              HeadingFlags)
                    Case DeadlineEntryKind.Row
                        entry.Row.Paint(g, bounds, measures, RowState(index))
                    Case DeadlineEntryKind.Message
                        TextRenderer.DrawText(g, entry.Text, measures.Regular, bounds, UiTheme.SecondaryText(), MessageFlags)
                    Case DeadlineEntryKind.DoneToggle
                        PaintDoneLine(g, index, bounds)
                End Select
            Next

        End Sub

        Private Function RowState(index As Integer) As DeadlineRowPaintState

            Dim state As New DeadlineRowPaintState With {
                .FocusRingPart = DeadlineRow.NoPart,
                .HoverPart = DeadlineRow.NoPart,
                .PressedPart = DeadlineRow.NoPart,
                .Enabled = Enabled
            }

            If Focused AndAlso _current >= 0 AndAlso _stops(_current).Entry = index Then
                If _stops(_current).Part = DeadlineRow.ItemPart Then
                    state.Focused = True
                ElseIf ShowFocusCues Then
                    state.FocusRingPart = _stops(_current).Part
                End If
            End If
            If _hover.Entry = index Then state.HoverPart = _hover.Part
            If _pressed.Entry = index AndAlso (_keyPressed OrElse _hover.IsSameAs(_pressed)) Then state.PressedPart = _pressed.Part

            Return state

        End Function

        ' "Done in the last 30 days: n  ·  Show", with Show or Hide drawn as
        ' a link and a focus rectangle around it when the keyboard is there.
        Private Sub PaintDoneLine(g As Graphics, index As Integer, bounds As Rectangle)

            Dim entry As DeadlineEntry = _entries(index)
            Dim link As Rectangle = LinkBounds(index)
            Dim lead As String = entry.Text.Substring(0, entry.Text.Length - entry.LinkText.Length)
            TextRenderer.DrawText(g, lead, Metrics.Regular, New Point(bounds.Left, bounds.Top), UiTheme.SecondaryText(), LineFlags)
            TextRenderer.DrawText(g, entry.LinkText, Metrics.Link, New Point(link.Left, link.Top), UiTheme.AccentColor(), LineFlags)

            If Focused AndAlso ShowFocusCues AndAlso _current >= 0 AndAlso _stops(_current).Entry = index Then
                Dim ring As Rectangle = link
                ring.Inflate(1, 0)
                ControlPaint.DrawFocusRectangle(g, ring, UiTheme.AccentColor(), BackColor)
            End If

        End Sub

        Protected Overrides Sub OnSystemColorsChanged(e As EventArgs)
            MyBase.OnSystemColorsChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnChangeUICues(e As UICuesEventArgs)
            MyBase.OnChangeUICues(e)
            Invalidate()
        End Sub


        ' ---------------------------------------------------------------
        ' Focus and the keyboard
        ' ---------------------------------------------------------------

        Private Sub SetCurrent(index As Integer)
            If index = _current Then Return
            If _current >= 0 Then InvalidateEntry(_stops(_current).Entry)
            _current = index
            _keyPressed = False
            If index >= 0 Then
                InvalidateEntry(_stops(index).Entry)
                ScrollStopIntoView(index)
                InvalidateEntry(_stops(index).Entry)
                NotifyFocus()
            End If
        End Sub

        ' Puts the keyboard on a stop, focusing the list if it was not.
        Private Sub FocusStop(index As Integer)
            If index < 0 Then Return
            If Focused Then
                SetCurrent(index)
            Else
                _current = index
                Focus()
            End If
        End Sub

        ' A screen reader hears which item or button the keyboard is on.
        Private Sub NotifyFocus()
            If _current < 0 OrElse Not Focused OrElse Not IsHandleCreated Then Return
            RaiseAccessibleEvent(Me, AccessibleEvents.Focus, _stopChild(_current))
        End Sub

        ' Raises an MSAA event for a child (its index) or for the control
        ' (-1), as a native list does. Control.AccessibilityNotifyClients
        ' raises nothing on .NET 10.0: it calls NotifyWinEvent only while the
        ' NoClientNotifications switch is set, which it is not.
        Friend Shared Sub RaiseAccessibleEvent(control As Control, accessibleEvent As AccessibleEvents, child As Integer)
            If control.IsHandleCreated Then NotifyWinEvent(CUInt(accessibleEvent), control.Handle, OBJID_CLIENT, child + 1)
        End Sub

        Private Const OBJID_CLIENT As Integer = -4

        <DllImport("user32.dll")>
        Private Shared Sub NotifyWinEvent(eventType As UInteger, window As IntPtr, objectId As Integer, childId As Integer)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            If _current < 0 AndAlso _stops.Count > 0 Then _current = 0
            If _current >= 0 Then
                ScrollStopIntoView(_current)
                InvalidateEntry(_stops(_current).Entry)
            End If
            NotifyFocus()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _keyPressed = False
            If _current >= 0 Then InvalidateEntry(_stops(_current).Entry)
        End Sub

        ' Tab into the list lands on its first stop, Shift+Tab on its last.
        Protected Overrides Sub [Select](directed As Boolean, forward As Boolean)
            If directed AndAlso _stops.Count > 0 Then _current = If(forward, 0, _stops.Count - 1)
            MyBase.Select(directed, forward)
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData
                Case Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.Space
                    Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        ' Tab and Shift+Tab move through the stops, then on out of the list.
        Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
            If (keyData And (Keys.KeyCode Or Keys.Control Or Keys.Alt)) = Keys.Tab AndAlso _current >= 0 Then
                Dim target As Integer = _current + If((keyData And Keys.Shift) = Keys.Shift, -1, 1)
                If target >= 0 AndAlso target < _stops.Count Then
                    SetCurrent(target)
                    Return True
                End If
            End If
            Return MyBase.ProcessDialogKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)

            MyBase.OnKeyDown(e)
            If e.Handled OrElse _current < 0 OrElse e.Alt OrElse e.Control OrElse e.Shift Then Return

            Dim spot As Spot = _stops(_current)
            Dim entry As DeadlineEntry = _entries(spot.Entry)
            Dim onRow As Boolean = entry.Kind = DeadlineEntryKind.Row
            Dim onItem As Boolean = onRow AndAlso spot.Part = DeadlineRow.ItemPart

            Select Case e.KeyCode
                Case Keys.Up, Keys.Down, Keys.Left, Keys.Right
                    Dim forward As Boolean = e.KeyCode = Keys.Down OrElse e.KeyCode = Keys.Right
                    If onItem AndAlso (e.KeyCode = Keys.Up OrElse e.KeyCode = Keys.Down) Then
                        MoveToRow(forward)
                    ElseIf onRow AndAlso Not onItem Then
                        MoveAmong(Function(candidate) candidate.Entry = spot.Entry AndAlso candidate.Part >= 0, forward)
                    Else
                        MoveAmong(Function(candidate) candidate.Part = DeadlineRow.ItemPart, forward)
                    End If
                Case Keys.Enter
                    If onItem Then
                        RunHandler(entry.Row.Primary?.Handler)
                    ElseIf onRow Then
                        Press(spot)
                    Else
                        ToggleDone()
                    End If
                Case Keys.Space
                    If Not onRow OrElse onItem Then Return
                    _pressed = spot
                    _keyPressed = True
                    InvalidateEntry(spot.Entry)
                Case Else
                    Return
            End Select

            e.Handled = True

        End Sub

        Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
            MyBase.OnKeyUp(e)
            If e.KeyCode <> Keys.Space OrElse Not _keyPressed Then Return
            Dim spot As Spot = _pressed
            _keyPressed = False
            _pressed = Spot.None
            InvalidateEntry(spot.Entry)
            If _current >= 0 AndAlso _stops(_current).IsSameAs(spot) Then Press(spot)
            e.Handled = True
        End Sub

        ' Up and Down go from row to row, skipping headings and the Done
        ' line, and stop at either end.
        Private Sub MoveToRow(forward As Boolean)
            Dim index As Integer = _current
            Do
                index += If(forward, 1, -1)
                If index < 0 OrElse index >= _stops.Count Then Return
            Loop Until _stops(index).Part = DeadlineRow.ItemPart AndAlso _entries(_stops(index).Entry).Kind = DeadlineEntryKind.Row
            SetCurrent(index)
        End Sub

        ' The next or previous stop of those that qualify, wrapping, as the
        ' arrow keys moved among a container's controls.
        Private Sub MoveAmong(qualifies As Func(Of Spot, Boolean), forward As Boolean)
            Dim candidates As New List(Of Integer)()
            For index As Integer = 0 To _stops.Count - 1
                If qualifies(_stops(index)) Then candidates.Add(index)
            Next
            Dim position As Integer = candidates.IndexOf(_current)
            If candidates.Count < 2 OrElse position < 0 Then Return
            SetCurrent(candidates((position + If(forward, 1, candidates.Count - 1)) Mod candidates.Count))
        End Sub


        ' ---------------------------------------------------------------
        ' Actions
        ' ---------------------------------------------------------------

        ' Runs an action's handler. A save inside it refills the list, so the
        ' caller learns whether the list it acted on is still the one shown.
        Private Function RunHandler(handler As Action) As Boolean
            If handler Is Nothing OrElse IsDisposed Then Return False
            Dim generation As Integer = _generation
            _pressed = Spot.None
            _keyPressed = False
            _hover = Spot.None
            Invalidate()
            handler()
            Return Not IsDisposed AndAlso generation = _generation
        End Function

        Private Sub Press(spot As Spot)
            Dim row As DeadlineRow = _entries(spot.Entry).Row
            If spot.Part = row.MorePart Then
                Dim more As Rectangle = SpotBounds(spot)
                ShowMenu(row, New Point(more.Left, more.Bottom), keyboard:=False)
            Else
                RunHandler(row.Buttons(spot.Part).Handler)
            End If
        End Sub

        Private Sub ToggleDone()
            RunHandler(Sub() RaiseEvent DoneToggleClicked(Me, EventArgs.Empty))
        End Sub

        ' What a screen reader's default action asks for, once the reader's
        ' call has returned, as a native button's click is posted.
        Private Sub Activate(spot As Spot, generation As Integer)
            If generation <> _generation OrElse IsDisposed OrElse Not Enabled Then Return
            Dim entry As DeadlineEntry = _entries(spot.Entry)
            Select Case entry.Kind
                Case DeadlineEntryKind.Row
                    If spot.Part = DeadlineRow.ItemPart Then
                        RunHandler(entry.Row.Primary?.Handler)
                    Else
                        Press(spot)
                    End If
                Case DeadlineEntryKind.DoneToggle
                    ToggleDone()
            End Select
        End Sub

        Private Sub ShowMenu(row As DeadlineRow, location As Point, keyboard As Boolean)
            _menuRow = row
            _menu.Show(Me, location)
            If keyboard AndAlso _menu.Visible AndAlso _menu.Items.Count > 0 Then _menu.Items(0).Select()
        End Sub

        ' The menu is filled for the row it opens on, with every action.
        Private Sub MenuOpening(sender As Object, e As CancelEventArgs)
            For Each item As ToolStripItem In _menu.Items.Cast(Of ToolStripItem)().ToList()
                item.Dispose()
            Next
            If _menuRow Is Nothing Then
                e.Cancel = True
                Return
            End If
            For Each action As DeadlineRow.RowAction In _menuRow.Actions
                Dim handler As Action = action.Handler
                _menu.Items.Add(action.Text, Nothing, Sub(item, args) RunHandler(handler))
            Next
            ' A menu with no items yet starts out cancelled.
            e.Cancel = False
        End Sub

        ' The context-menu key and Shift+F10 open the menu of the row the
        ' keyboard is on, at the row's center; a right-click opens the menu of
        ' the row under the pointer, where it was clicked.
        Protected Overrides Sub WndProc(ByRef m As Message)

            If m.Msg <> WM_CONTEXTMENU Then
                MyBase.WndProc(m)
                Return
            End If

            Dim position As Long = m.LParam.ToInt64() And &HFFFFFFFFL
            If position = &HFFFFFFFFL Then
                Dim row As DeadlineRow = FocusedRow
                If row IsNot Nothing Then
                    Dim bounds As Rectangle = EntryBounds(_stops(_current).Entry)
                    bounds.Intersect(ClientRectangle)
                    ShowMenu(row, New Point(bounds.Left + bounds.Width \ 2, bounds.Top + bounds.Height \ 2), keyboard:=True)
                End If
                Return
            End If

            Dim x As Integer = CInt(position And &HFFFFL)
            Dim y As Integer = CInt((position >> 16) And &HFFFFL)
            If x > Short.MaxValue Then x -= &H10000
            If y > Short.MaxValue Then y -= &H10000
            Dim client As Point = PointToClient(New Point(x, y))
            Dim hit As Spot = HitTest(client)
            If hit.Entry >= 0 AndAlso _entries(hit.Entry).Kind = DeadlineEntryKind.Row Then
                ShowMenu(_entries(hit.Entry).Row, client, keyboard:=False)
            End If

        End Sub


        ' ---------------------------------------------------------------
        ' The pointer
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)

            MyBase.OnMouseDown(e)
            Dim hit As Spot = HitTest(e.Location)
            _down = hit
            _downGeneration = _generation
            If hit.Entry < 0 Then Return

            Select Case e.Button
                Case MouseButtons.Left
                    FocusStop(StopOf(hit))
                    If IsButton(hit) Then
                        _pressed = hit
                        _hover = hit
                        InvalidateEntry(hit.Entry)
                    End If
                Case MouseButtons.Right
                    ' A right-click on a button opens the menu and leaves the
                    ' keyboard where it was, as it did on a real button.
                    If hit.Part = DeadlineRow.ItemPart AndAlso _entries(hit.Entry).Kind = DeadlineEntryKind.Row Then FocusStop(StopOf(hit))
            End Select

        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)

            MyBase.OnMouseUp(e)
            Dim down As Spot = _down
            Dim pressed As Spot = _pressed
            If Not _keyPressed Then _pressed = Spot.None
            _down = Spot.None
            If pressed.Entry >= 0 Then InvalidateEntry(pressed.Entry)
            If e.Button <> MouseButtons.Left OrElse down.Entry < 0 OrElse _downGeneration <> _generation Then Return

            Dim hit As Spot = HitTest(e.Location)
            If Not hit.IsSameAs(down) Then Return
            If IsButton(hit) Then
                If pressed.IsSameAs(hit) Then Press(hit)
            ElseIf _entries(hit.Entry).Kind = DeadlineEntryKind.DoneToggle Then
                ToggleDone()
            End If

        End Sub

        ' A double-click on a row runs its first action; on a button it is a
        ' second press, as on a real button.
        Protected Overrides Sub OnDoubleClick(e As EventArgs)
            MyBase.OnDoubleClick(e)
            Dim down As Spot = _down
            If down.Entry < 0 OrElse _downGeneration <> _generation OrElse down.Part <> DeadlineRow.ItemPart Then Return
            Dim entry As DeadlineEntry = _entries(down.Entry)
            If entry.Kind = DeadlineEntryKind.Row Then RunHandler(entry.Row.Primary?.Handler)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)

            MyBase.OnMouseMove(e)
            Dim hit As Spot = HitTest(e.Location)
            Dim hover As Spot = If(IsButton(hit), hit, Spot.None)
            If Not hover.IsSameAs(_hover) Then
                InvalidateEntry(_hover.Entry)
                _hover = hover
                InvalidateEntry(_hover.Entry)
            End If

            Dim pointer As Cursor = If(IsButton(hit) OrElse (hit.Entry >= 0 AndAlso _entries(hit.Entry).Kind = DeadlineEntryKind.DoneToggle),
                                       Cursors.Hand, Cursors.Default)
            If Cursor IsNot pointer Then Cursor = pointer

        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            InvalidateEntry(_hover.Entry)
            _hover = Spot.None
            If Cursor IsNot Cursors.Default Then Cursor = Cursors.Default
        End Sub


        ' ---------------------------------------------------------------
        ' What a screen reader sees
        ' ---------------------------------------------------------------

        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject
            Return New ListAccessibleObject(Me)
        End Function

        Private Function ReadableChild(index As Integer) As AccessibleObject
            If index < 0 OrElse index >= _children.Length Then Return Nothing
            If _children(index) Is Nothing Then _children(index) = New EntryAccessibleObject(Me, index, _readable(index))
            Return _children(index)
        End Function

        ' The list: role List, named by the page, with one flat run of
        ' children, since a screen reader asks the list itself for each child
        ' by its number.
        Private NotInheritable Class ListAccessibleObject
            Inherits ControlAccessibleObject

            Private ReadOnly _list As DeadlineList

            Public Sub New(owner As DeadlineList)
                MyBase.New(owner)
                _list = owner
            End Sub

            Public Overrides ReadOnly Property Role As AccessibleRole
                Get
                    Return AccessibleRole.List
                End Get
            End Property

            Public Overrides Function GetChildCount() As Integer
                Return _list._readable.Count
            End Function

            Public Overrides Function GetChild(index As Integer) As AccessibleObject
                Return _list.ReadableChild(index)
            End Function

            Public Overrides Function GetFocused() As AccessibleObject
                If Not _list.Focused Then Return Nothing
                If _list._current < 0 Then Return Me
                Return _list.ReadableChild(_list._stopChild(_list._current))
            End Function

            ' The row the keyboard is on.
            Public Overrides Function GetSelected() As AccessibleObject
                If _list._current < 0 Then Return Nothing
                Dim row As Integer = _list.StopOf(New Spot(_list._stops(_list._current).Entry, DeadlineRow.ItemPart))
                Return If(row < 0, Nothing, _list.ReadableChild(_list._stopChild(row)))
            End Function

            Public Overrides Function HitTest(x As Integer, y As Integer) As AccessibleObject
                If Not _list.IsHandleCreated Then Return Nothing
                Dim client As Point = _list.PointToClient(New Point(x, y))
                If Not _list.ClientRectangle.Contains(client) Then Return Nothing
                Dim hit As Spot = _list.HitTest(client)
                Dim index As Integer = _list._readable.FindIndex(Function(candidate) candidate.IsSameAs(hit))
                If index < 0 AndAlso hit.Entry >= 0 Then
                    index = _list._readable.FindIndex(Function(candidate) candidate.Entry = hit.Entry)
                End If
                Return If(index < 0, Me, _list.ReadableChild(index))
            End Function

        End Class

        ' A heading, a row, one of its buttons, the sentence or the Done
        ' link. A child kept from before a refill answers with what it was,
        ' and does nothing.
        Private NotInheritable Class EntryAccessibleObject
            Inherits AccessibleObject

            Private ReadOnly _list As DeadlineList
            Private ReadOnly _index As Integer
            Private ReadOnly _spot As Spot
            Private ReadOnly _entry As DeadlineEntry
            Private ReadOnly _generation As Integer

            Public Sub New(list As DeadlineList, index As Integer, spot As Spot)
                _list = list
                _index = index
                _spot = spot
                _entry = list._entries(spot.Entry)
                _generation = list._generation
            End Sub

            Private ReadOnly Property IsCurrent As Boolean
                Get
                    Return _generation = _list._generation AndAlso Not _list.IsDisposed
                End Get
            End Property

            Private ReadOnly Property IsItem As Boolean
                Get
                    Return _spot.Part = DeadlineRow.ItemPart
                End Get
            End Property

            Public Overrides Property Name As String
                Get
                    Select Case _entry.Kind
                        Case DeadlineEntryKind.Row
                            If IsItem Then Return _entry.Row.AccessibleName
                            If _spot.Part = _entry.Row.MorePart Then Return _entry.Row.MoreName
                            Return _entry.Row.Buttons(_spot.Part).Text
                        Case DeadlineEntryKind.DoneToggle
                            Return _entry.Name
                        Case Else
                            Return _entry.Text
                    End Select
                End Get
                Set(value As String)
                    ' Named by what it shows.
                End Set
            End Property

            Public Overrides ReadOnly Property Role As AccessibleRole
                Get
                    Select Case _entry.Kind
                        Case DeadlineEntryKind.Row
                            Return If(IsItem, AccessibleRole.ListItem, AccessibleRole.PushButton)
                        Case DeadlineEntryKind.DoneToggle
                            Return AccessibleRole.Link
                        Case Else
                            Return AccessibleRole.StaticText
                    End Select
                End Get
            End Property

            ' A button is described by its row, as each button was.
            Public Overrides ReadOnly Property Description As String
                Get
                    If _entry.Kind = DeadlineEntryKind.Row AndAlso Not IsItem Then Return _entry.Row.AccessibleName
                    If _entry.Kind = DeadlineEntryKind.DoneToggle Then Return _entry.Text
                    Return Nothing
                End Get
            End Property

            Public Overrides ReadOnly Property DefaultAction As String
                Get
                    Select Case _entry.Kind
                        Case DeadlineEntryKind.Row
                            If IsItem Then Return _entry.Row.Primary?.Text
                            Return "Press"
                        Case DeadlineEntryKind.DoneToggle
                            Return "Click"
                        Case Else
                            Return Nothing
                    End Select
                End Get
            End Property

            Public Overrides ReadOnly Property State As AccessibleStates
                Get
                    If Not IsCurrent Then Return AccessibleStates.Unavailable
                    Dim states As AccessibleStates = AccessibleStates.None
                    Dim stopIndex As Integer = _list.StopOf(_spot)
                    If stopIndex >= 0 Then
                        states = states Or AccessibleStates.Focusable
                        If _list.Focused AndAlso _list._current = stopIndex Then states = states Or AccessibleStates.Focused
                        If Not _list.Enabled Then states = states Or AccessibleStates.Unavailable
                    Else
                        states = states Or AccessibleStates.ReadOnly
                    End If
                    If _entry.Kind = DeadlineEntryKind.DoneToggle Then states = states Or AccessibleStates.Linked
                    If Not _list.ClientRectangle.IntersectsWith(_list.SpotBounds(_spot)) Then states = states Or AccessibleStates.Offscreen
                    Return states
                End Get
            End Property

            Public Overrides ReadOnly Property Bounds As Rectangle
                Get
                    If Not IsCurrent OrElse Not _list.IsHandleCreated Then Return Rectangle.Empty
                    Return _list.RectangleToScreen(_list.SpotBounds(_spot))
                End Get
            End Property

            Public Overrides ReadOnly Property Parent As AccessibleObject
                Get
                    Return _list.AccessibilityObject
                End Get
            End Property

            Public Overrides Function GetChildCount() As Integer
                Return 0
            End Function

            Public Overrides Function Navigate(navdir As AccessibleNavigation) As AccessibleObject
                If Not IsCurrent Then Return Nothing
                Select Case navdir
                    Case AccessibleNavigation.Next, AccessibleNavigation.Down, AccessibleNavigation.Right
                        Return _list.ReadableChild(_index + 1)
                    Case AccessibleNavigation.Previous, AccessibleNavigation.Up, AccessibleNavigation.Left
                        Return _list.ReadableChild(_index - 1)
                End Select
                Return Nothing
            End Function

            Public Overrides Sub [Select](flags As AccessibleSelection)
                If Not IsCurrent OrElse (flags And AccessibleSelection.TakeFocus) = 0 Then Return
                _list.FocusStop(_list.StopOf(_spot))
            End Sub

            Public Overrides Sub DoDefaultAction()
                If Not IsCurrent OrElse Not _list.IsHandleCreated OrElse Not _list.Enabled Then Return
                Dim list As DeadlineList = _list
                Dim spot As Spot = _spot
                Dim generation As Integer = _generation
                list.BeginInvoke(New Action(Sub() list.Activate(spot, generation)))
            End Sub

        End Class

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _menu.Dispose()
                _metrics?.Dispose()
                _metrics = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
