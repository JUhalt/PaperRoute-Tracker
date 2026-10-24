Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' One journal on a manuscript's shortlist as the list shows it (#65,
    ' #87, #88): its place and status, its name with the details known about
    ' it, its facts and evidence lines, and what its Edit... and ⋯ do. The
    ' manuscript page makes one for each journal each time it fills the list.
    Friend NotInheritable Class ShortlistEntry

        Friend Property JournalName As String = String.Empty
        Friend Property Status As CandidateStatus = CandidateStatus.Considering
        Friend Property StatusText As String = String.Empty

        ' The name, then the details on a line of their own.
        Friend Property Text As String = String.Empty
        Friend Property StruckOut As Boolean
        Friend Property Facts As String = String.Empty
        Friend Property Evidence As String = String.Empty

        ' What the evidence line's tooltip says.
        Friend Property EvidenceTip As String = String.Empty

        Friend Property CanMakeTarget As Boolean
        Friend Property CanMoveUp As Boolean
        Friend Property CanMoveDown As Boolean
        Friend Property Edit As Action
        Friend Property MakeTarget As Action
        Friend Property MoveUp As Action
        Friend Property MoveDown As Action
        Friend Property Remove As Action

        ' Where the list put the row and each of its parts, in client
        ' coordinates.
        Friend Property RowBounds As Rectangle
        Friend Property StatusBounds As Rectangle
        Friend Property TextBounds As Rectangle
        Friend Property FactsBounds As Rectangle
        Friend Property EvidenceBounds As Rectangle
        Friend Property EditBounds As Rectangle
        Friend Property MoreBounds As Rectangle

    End Class


    ' The journal shortlist on the manuscript page (#133): every row drawn in
    ' one window, rather than a panel of labels and two buttons for each
    ' journal, which the page made anew for every manuscript it opened and
    ' every change to the list. It keeps what the rows did: Tab reaches each
    ' row's Edit... and ⋯ in turn, the arrow keys move between a row's two,
    ' Enter presses at once and Space once it is let go, ⋯ and the
    ' context-menu key open the row's menu, and the pointer hovers, presses
    ' and shows the evidence's keywords. A screen reader sees one list whose
    ' children, in reading order, are each row as a list item, its status,
    ' its name and details, its facts and evidence when it has them, and its
    ' two buttons.
    Friend NotInheritable Class JournalShortlistList
        Inherits Control

        Private Const WM_CONTEXTMENU As Integer = &H7B

        Private Const TextFlags As TextFormatFlags = TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix

        Private Const StatusFlags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine

        Private Const ButtonFlags As TextFormatFlags =
            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or
            TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix

        Private Const EditText As String = "Edit..."
        Private Const MoreText As String = "⋯"

        ' A row's parts, in the order a screen reader reads them.
        Private Enum RowPart
            Item
            Status
            Text
            Facts
            Evidence
            Edit
            More
        End Enum

        ' A place in the list: a row, and one of its parts.
        Private Structure Spot

            Public Sub New(entry As Integer, part As RowPart)
                Me.Entry = entry
                Me.Part = part
            End Sub

            Public ReadOnly Entry As Integer
            Public ReadOnly Part As RowPart

            Public Shared ReadOnly None As New Spot(-1, RowPart.Item)

            Public Function IsSameAs(other As Spot) As Boolean
                Return Entry = other.Entry AndAlso Part = other.Part
            End Function

            Public ReadOnly Property IsButton As Boolean
                Get
                    Return Entry >= 0 AndAlso (Part = RowPart.Edit OrElse Part = RowPart.More)
                End Get
            End Property

        End Structure

        Private ReadOnly _entries As New List(Of ShortlistEntry)()

        ' The keyboard's stops in Tab order, and the screen reader's
        ' children in reading order; _stopChild maps the one to the other.
        Private ReadOnly _stops As New List(Of Spot)()
        Private ReadOnly _readable As New List(Of Spot)()
        Private _stopChild As Integer() = {}
        Private _children As AccessibleObject() = {}

        Private ReadOnly _menu As New ContextMenuStrip()
        Private _menuEntry As ShortlistEntry = Nothing
        Private _boldFont As Font = Nothing
        Private _struckFont As Font = Nothing
        Private _arrangedWidth As Integer = -1
        Private _current As Integer = -1
        Private _hover As Spot = Spot.None
        Private _pressed As Spot = Spot.None
        Private _down As Spot = Spot.None
        Private _keyPressed As Boolean = False
        Private _generation As Integer = 0
        Private _signature As String = String.Empty
        Private _toolTip As ToolTip = Nothing

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            SetStyle(ControlStyles.ContainerControl, False)
            TabStop = False
            AddHandler _menu.Opening, AddressOf MenuOpening
        End Sub

        ' The tooltip the evidence line's keywords show in; the page's own.
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property ToolTip As ToolTip
            Get
                Return _toolTip
            End Get
            Set(value As ToolTip)
                _toolTip = value
            End Set
        End Property

        Friend ReadOnly Property Entries As IReadOnlyList(Of ShortlistEntry)
            Get
                Return _entries
            End Get
        End Property


        ' ---------------------------------------------------------------
        ' Filling
        ' ---------------------------------------------------------------

        ' Replaces the rows. Nothing is chosen afterwards; the page moves
        ' the keyboard off the list first when it had it, as it moved when
        ' the button pressed went with its row.
        Friend Sub SetEntries(entries As IEnumerable(Of ShortlistEntry))

            _generation += 1
            _entries.Clear()
            _entries.AddRange(entries)
            _stops.Clear()
            _readable.Clear()
            _current = -1
            _hover = Spot.None
            _pressed = Spot.None
            _down = Spot.None
            _keyPressed = False
            _menuEntry = Nothing
            ClearTip()

            Dim stopChild As New List(Of Integer)()

            For index As Integer = 0 To _entries.Count - 1
                Dim entry As ShortlistEntry = _entries(index)
                _readable.Add(New Spot(index, RowPart.Item))
                _readable.Add(New Spot(index, RowPart.Status))
                _readable.Add(New Spot(index, RowPart.Text))
                If entry.Facts.Length > 0 Then _readable.Add(New Spot(index, RowPart.Facts))
                If entry.Evidence.Length > 0 Then _readable.Add(New Spot(index, RowPart.Evidence))
                For Each button As RowPart In {RowPart.Edit, RowPart.More}
                    stopChild.Add(_readable.Count)
                    _stops.Add(New Spot(index, button))
                    _readable.Add(New Spot(index, button))
                Next
            Next

            _stopChild = stopChild.ToArray()
            _children = New AccessibleObject(_readable.Count - 1) {}
            TabStop = _stops.Count > 0

            Arrange()
            Invalidate()

            ' Every child is new; a screen reader is told so whenever what it
            ' reads of the rows changed, their journals and order, a row's
            ' status, details, facts or evidence, or what its menu allows, as
            ' DeadlineList tells it.
            Dim signature As String = String.Join(ChrW(0), _entries.Select(
                Function(entry) String.Join(ChrW(1), entry.JournalName, entry.StatusText, entry.Text, entry.Facts, entry.Evidence,
                                            entry.CanMakeTarget.ToString(), entry.CanMoveUp.ToString(), entry.CanMoveDown.ToString())))
            If signature <> _signature Then
                _signature = signature
                If IsHandleCreated Then DeadlineList.RaiseAccessibleEvent(Me, AccessibleEvents.Reorder, -1)
            End If

        End Sub


        ' ---------------------------------------------------------------
        ' Layout
        ' ---------------------------------------------------------------

        Private ReadOnly Property BoldFont As Font
            Get
                If _boldFont Is Nothing Then _boldFont = New Font(Font, FontStyle.Bold)
                Return _boldFont
            End Get
        End Property

        Private ReadOnly Property StruckFont As Font
            Get
                If _struckFont Is Nothing Then _struckFont = New Font(Font, FontStyle.Strikeout)
                Return _struckFont
            End Get
        End Property

        Private Function Px(pixels As Integer) As Integer
            Return LogicalToDeviceUnits(pixels)
        End Function

        Private Function ButtonHeight() As Integer
            Return Math.Max(Px(23), Font.Height + Px(9))
        End Function

        Private Function ButtonWidth(text As String) As Integer
            Return Math.Max(Px(75), TextRenderer.MeasureText(text, Font, Size.Empty, ButtonFlags).Width + Px(16))
        End Function

        ' Lays the rows out as the panel of rows laid them out: the status in
        ' a column as wide as the widest status could be, the name and its
        ' details beside it, wrapped, then the facts and the evidence; Edit...
        ' and ⋯ at the right; six pixels under each row. The list is as tall
        ' as its rows.
        Private Sub Arrange()

            Dim width As Integer = ClientSize.Width
            _arrangedWidth = width

            Dim count As Integer = _entries.Count
            Dim statusWidth As Integer =
                TextRenderer.MeasureText(count.ToString(Globalization.CultureInfo.CurrentCulture) & ".  Considering", BoldFont).Width + Px(12)
            Dim buttonHigh As Integer = ButtonHeight()
            Dim editWidth As Integer = ButtonWidth(EditText)
            Dim moreWidth As Integer = ButtonWidth(MoreText)
            Dim textWidth As Integer = Math.Max(Px(200), width - statusWidth - editWidth - moreWidth - Px(24))
            Dim y As Integer = 0

            For Each entry As ShortlistEntry In _entries

                Dim top As Integer = y
                Dim statusSize As Size = TextRenderer.MeasureText(entry.StatusText, BoldFont, Size.Empty, StatusFlags)
                entry.StatusBounds = New Rectangle(0, top + Px(3), statusSize.Width, Math.Max(statusSize.Height, Font.Height))

                Dim textTop As Integer = top + Px(3)
                Dim textHeight As Integer = MeasureWrapped(entry.Text, If(entry.StruckOut, StruckFont, Font), textWidth)
                entry.TextBounds = New Rectangle(statusWidth, textTop, textWidth, textHeight)
                Dim bottom As Integer = entry.TextBounds.Bottom

                If entry.Facts.Length > 0 Then
                    entry.FactsBounds = New Rectangle(statusWidth, bottom + Px(2), textWidth, MeasureWrapped(entry.Facts, Font, textWidth))
                    bottom = entry.FactsBounds.Bottom
                Else
                    entry.FactsBounds = Rectangle.Empty
                End If

                If entry.Evidence.Length > 0 Then
                    entry.EvidenceBounds = New Rectangle(statusWidth, bottom + Px(2), textWidth, MeasureWrapped(entry.Evidence, Font, textWidth))
                    bottom = entry.EvidenceBounds.Bottom
                Else
                    entry.EvidenceBounds = Rectangle.Empty
                End If

                entry.MoreBounds = New Rectangle(width - moreWidth, top, moreWidth, buttonHigh)
                entry.EditBounds = New Rectangle(entry.MoreBounds.Left - Px(6) - editWidth, top, editWidth, buttonHigh)

                Dim height As Integer = Math.Max(Math.Max(bottom, entry.StatusBounds.Bottom) - top, buttonHigh)
                entry.RowBounds = New Rectangle(0, top, width, height)
                y = top + height + Px(6)

            Next

            If Height <> y Then Height = y

        End Sub

        Private Shared Function MeasureWrapped(text As String, font As Font, width As Integer) As Integer
            Return TextRenderer.MeasureText(text, font, New Size(Math.Max(1, width), Integer.MaxValue), TextFlags).Height
        End Function

        Protected Overrides Sub OnClientSizeChanged(e As EventArgs)
            MyBase.OnClientSizeChanged(e)
            If ClientSize.Width <> _arrangedWidth AndAlso _entries.Count > 0 Then
                Arrange()
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            ResetFonts()
            MyBase.OnFontChanged(e)
            If _entries.Count > 0 Then Arrange()
            Invalidate()
        End Sub

        Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
            MyBase.OnDpiChangedAfterParent(e)
            ResetFonts()
            If _entries.Count > 0 Then Arrange()
            Invalidate()
        End Sub

        Private Sub ResetFonts()
            _boldFont?.Dispose()
            _boldFont = Nothing
            _struckFont?.Dispose()
            _struckFont = Nothing
        End Sub


        ' ---------------------------------------------------------------
        ' Geometry
        ' ---------------------------------------------------------------

        Private Function SpotBounds(spot As Spot) As Rectangle
            If spot.Entry < 0 OrElse spot.Entry >= _entries.Count Then Return Rectangle.Empty
            Dim entry As ShortlistEntry = _entries(spot.Entry)
            Select Case spot.Part
                Case RowPart.Status : Return entry.StatusBounds
                Case RowPart.Text : Return entry.TextBounds
                Case RowPart.Facts : Return entry.FactsBounds
                Case RowPart.Evidence : Return entry.EvidenceBounds
                Case RowPart.Edit : Return entry.EditBounds
                Case RowPart.More : Return entry.MoreBounds
                Case Else : Return entry.RowBounds
            End Select
        End Function

        ' The row under a point, and the button or line it is on.
        Private Function HitTest(point As Point) As Spot
            For index As Integer = 0 To _entries.Count - 1
                Dim entry As ShortlistEntry = _entries(index)
                If Not entry.RowBounds.Contains(point) Then Continue For
                For Each part As RowPart In {RowPart.Edit, RowPart.More, RowPart.Status, RowPart.Text, RowPart.Facts, RowPart.Evidence}
                    If SpotBounds(New Spot(index, part)).Contains(point) Then Return New Spot(index, part)
                Next
                Return New Spot(index, RowPart.Item)
            Next
            Return Spot.None
        End Function

        Private Function StopOf(spot As Spot) As Integer
            Return _stops.FindIndex(Function(candidate) candidate.IsSameAs(spot))
        End Function

        Private Sub InvalidateSpot(spot As Spot)
            If spot.Entry < 0 OrElse spot.Entry >= _entries.Count Then Return
            Dim bounds As Rectangle = SpotBounds(spot)
            bounds.Inflate(2, 2)
            Invalidate(bounds)
        End Sub


        ' ---------------------------------------------------------------
        ' Painting
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            MyBase.OnPaint(e)

            Dim g As Graphics = e.Graphics
            Dim ring As Boolean = Focused AndAlso ShowFocusCues

            For index As Integer = 0 To _entries.Count - 1

                Dim entry As ShortlistEntry = _entries(index)
                Dim row As Rectangle = entry.RowBounds
                row.Inflate(2, 2)
                If Not row.IntersectsWith(e.ClipRectangle) Then Continue For

                TextRenderer.DrawText(g, entry.StatusText, BoldFont, entry.StatusBounds, StatusInk(entry.Status), StatusFlags)
                TextRenderer.DrawText(g, entry.Text, If(entry.StruckOut, StruckFont, Font), entry.TextBounds, UiTheme.PrimaryText(), TextFlags)
                If entry.Facts.Length > 0 Then TextRenderer.DrawText(g, entry.Facts, Font, entry.FactsBounds, UiTheme.MutedText(), TextFlags)
                If entry.Evidence.Length > 0 Then TextRenderer.DrawText(g, entry.Evidence, Font, entry.EvidenceBounds, UiTheme.MutedText(), TextFlags)

                For Each button As RowPart In {RowPart.Edit, RowPart.More}
                    Dim spot As New Spot(index, button)
                    Dim focusedHere As Boolean = ring AndAlso _current >= 0 AndAlso _stops(_current).IsSameAs(spot)
                    Dim pressed As Boolean = _pressed.IsSameAs(spot) AndAlso (_keyPressed OrElse _hover.IsSameAs(spot))
                    ActionButton.PaintFace(g, SpotBounds(spot), ActionButtonRole.Secondary, If(button = RowPart.Edit, EditText, MoreText), Font, ButtonFlags,
                                           _hover.IsSameAs(spot), pressed, focusedHere, Enabled, DeviceDpi)
                Next

            Next

        End Sub

        ' A status in the accent while preferred, muted once ruled out, and
        ' secondary otherwise, read when it is drawn.
        Private Shared Function StatusInk(status As CandidateStatus) As Color
            Select Case status
                Case CandidateStatus.Preferred : Return UiTheme.AccentColor()
                Case CandidateStatus.RuledOut : Return UiTheme.MutedText()
                Case Else : Return UiTheme.SecondaryText()
            End Select
        End Function

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
            If _current >= 0 Then InvalidateSpot(_stops(_current))
            _current = index
            _keyPressed = False
            If index >= 0 Then
                InvalidateSpot(_stops(index))
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

        ' A screen reader hears which button the keyboard is on.
        Private Sub NotifyFocus()
            If _current < 0 OrElse Not Focused OrElse Not IsHandleCreated Then Return
            DeadlineList.RaiseAccessibleEvent(Me, AccessibleEvents.Focus, _stopChild(_current))
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            If _current < 0 AndAlso _stops.Count > 0 Then _current = 0
            If _current >= 0 Then InvalidateSpot(_stops(_current))
            NotifyFocus()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            _keyPressed = False
            If _current >= 0 Then InvalidateSpot(_stops(_current))
        End Sub

        ' Tab into the list lands on its first button, Shift+Tab on its last.
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

        ' Tab and Shift+Tab move through the buttons, then on out of the list.
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

        ' Any arrow moves to the row's other button, as the arrows moved
        ' between the two buttons a row held; Enter presses the button, and
        ' Space presses it once it is let go.
        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)

            MyBase.OnKeyDown(e)
            If e.Handled OrElse _current < 0 OrElse e.Alt OrElse e.Control OrElse e.Shift Then Return

            Dim spot As Spot = _stops(_current)

            Select Case e.KeyCode
                Case Keys.Up, Keys.Down, Keys.Left, Keys.Right
                    SetCurrent(StopOf(New Spot(spot.Entry, If(spot.Part = RowPart.Edit, RowPart.More, RowPart.Edit))))
                Case Keys.Enter
                    Press(spot)
                Case Keys.Space
                    _pressed = spot
                    _keyPressed = True
                    InvalidateSpot(spot)
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
            InvalidateSpot(spot)
            If _current >= 0 AndAlso _stops(_current).IsSameAs(spot) Then Press(spot)
            e.Handled = True
        End Sub


        ' ---------------------------------------------------------------
        ' Actions
        ' ---------------------------------------------------------------

        ' Runs an action of a row. The action may fill the list again.
        Private Sub RunHandler(handler As Action)
            If handler Is Nothing OrElse IsDisposed Then Return
            _pressed = Spot.None
            _keyPressed = False
            _hover = Spot.None
            Invalidate()
            handler()
        End Sub

        Private Sub Press(spot As Spot)
            Dim entry As ShortlistEntry = _entries(spot.Entry)
            If spot.Part = RowPart.More Then
                Dim more As Rectangle = entry.MoreBounds
                ShowMenu(entry, New Point(more.Left, more.Bottom))
            Else
                RunHandler(entry.Edit)
            End If
        End Sub

        ' What a screen reader's default action asks for, once the reader's
        ' call has returned, as a native button's click is posted.
        Private Sub Activate(spot As Spot, generation As Integer)
            If generation <> _generation OrElse IsDisposed OrElse Not Enabled Then Return
            If spot.IsButton Then Press(spot)
        End Sub

        Private Sub ShowMenu(entry As ShortlistEntry, location As Point)
            _menuEntry = entry
            _menu.Show(Me, location)
        End Sub

        ' The menu is filled for the row it opens on: Make Target Journal
        ' unless the row is the target, Move Up and Move Down where the row's
        ' place allows, and Remove from Shortlist.
        Private Sub MenuOpening(sender As Object, e As CancelEventArgs)
            For Each item As ToolStripItem In _menu.Items.Cast(Of ToolStripItem)().ToList()
                item.Dispose()
            Next
            Dim entry As ShortlistEntry = _menuEntry
            If entry Is Nothing Then
                e.Cancel = True
                Return
            End If
            Dim generation As Integer = _generation
            Dim run As Func(Of Action, EventHandler) =
                Function(handler) Sub(item, args) If generation = _generation Then RunHandler(handler)
            _menu.Items.Add("Make Target Journal", Nothing, run(entry.MakeTarget)).Enabled = entry.CanMakeTarget
            _menu.Items.Add("Move Up", Nothing, run(entry.MoveUp)).Enabled = entry.CanMoveUp
            _menu.Items.Add("Move Down", Nothing, run(entry.MoveDown)).Enabled = entry.CanMoveDown
            _menu.Items.Add(New ToolStripSeparator())
            _menu.Items.Add("Remove from Shortlist", Nothing, run(entry.Remove))
            ' A menu with no items yet starts out cancelled.
            e.Cancel = False
        End Sub

        ' The context-menu key and Shift+F10 open the menu of the row the
        ' keyboard is on, under its ⋯; a right-click does nothing, as it did
        ' on the rows.
        Protected Overrides Sub WndProc(ByRef m As Message)

            If m.Msg <> WM_CONTEXTMENU Then
                MyBase.WndProc(m)
                Return
            End If

            Dim position As Long = m.LParam.ToInt64() And &HFFFFFFFFL
            If position = &HFFFFFFFFL AndAlso _current >= 0 Then
                Press(New Spot(_stops(_current).Entry, RowPart.More))
                Return
            End If

            MyBase.WndProc(m)

        End Sub


        ' ---------------------------------------------------------------
        ' The pointer
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Dim hit As Spot = HitTest(e.Location)
            _down = hit
            If e.Button <> MouseButtons.Left OrElse Not hit.IsButton Then Return
            FocusStop(StopOf(hit))
            _pressed = hit
            _hover = hit
            InvalidateSpot(hit)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            Dim down As Spot = _down
            Dim pressed As Spot = _pressed
            If Not _keyPressed Then _pressed = Spot.None
            _down = Spot.None
            InvalidateSpot(pressed)
            If e.Button <> MouseButtons.Left OrElse Not down.IsButton Then Return
            Dim hit As Spot = HitTest(e.Location)
            If hit.IsSameAs(down) AndAlso pressed.IsSameAs(hit) Then Press(hit)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)

            MyBase.OnMouseMove(e)
            Dim hit As Spot = HitTest(e.Location)
            Dim hover As Spot = If(hit.IsButton, hit, Spot.None)
            If Not hover.IsSameAs(_hover) Then
                InvalidateSpot(_hover)
                _hover = hover
                InvalidateSpot(_hover)
            End If

            Dim pointer As Cursor = If(hit.IsButton, Cursors.Hand, Cursors.Default)
            If Cursor IsNot pointer Then Cursor = pointer

            ' The evidence's keywords show over its line, and no tip elsewhere.
            If _toolTip IsNot Nothing Then
                Dim tip As String = If(hit.Part = RowPart.Evidence AndAlso hit.Entry >= 0, _entries(hit.Entry).EvidenceTip, String.Empty)
                If _toolTip.GetToolTip(Me) <> tip Then _toolTip.SetToolTip(Me, tip)
            End If

        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            InvalidateSpot(_hover)
            _hover = Spot.None
            If Cursor IsNot Cursors.Default Then Cursor = Cursors.Default
            ClearTip()
        End Sub

        ' The tip is set on the list, a Tab stop, so it would show when the
        ' keyboard reaches the list, and keep a record let go of; it holds
        ' the evidence's keywords only while the pointer is over that line.
        Private Sub ClearTip()
            If _toolTip IsNot Nothing AndAlso _toolTip.GetToolTip(Me).Length > 0 Then _toolTip.SetToolTip(Me, String.Empty)
        End Sub


        ' ---------------------------------------------------------------
        ' What a screen reader sees
        ' ---------------------------------------------------------------

        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject
            Return New ListAccessibleObject(Me)
        End Function

        Private Function ReadableChild(index As Integer) As AccessibleObject
            If index < 0 OrElse index >= _children.Length Then Return Nothing
            If _children(index) Is Nothing Then _children(index) = New PartAccessibleObject(Me, index, _readable(index))
            Return _children(index)
        End Function

        ' The list: role List, named by the page, with one flat run of
        ' children, since a screen reader asks the list itself for each child
        ' by its number.
        Private NotInheritable Class ListAccessibleObject
            Inherits ControlAccessibleObject

            Private ReadOnly _list As JournalShortlistList

            Public Sub New(owner As JournalShortlistList)
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

            ' A button is focused only while the list has the keyboard, as a
            ' native button is, and not while another window has it.
            Public Overrides Function GetFocused() As AccessibleObject
                If Not _list.Focused Then Return Nothing
                If _list._current < 0 Then Return Me
                Return _list.ReadableChild(_list._stopChild(_list._current))
            End Function

            Public Overrides Function HitTest(x As Integer, y As Integer) As AccessibleObject
                If Not _list.IsHandleCreated Then Return Nothing
                Dim client As Point = _list.PointToClient(New Point(x, y))
                If Not _list.ClientRectangle.Contains(client) Then Return Nothing
                Dim hit As Spot = _list.HitTest(client)
                Dim index As Integer = _list._readable.FindIndex(Function(candidate) candidate.IsSameAs(hit))
                Return If(index < 0, Me, _list.ReadableChild(index))
            End Function

        End Class

        ' A row, one of its lines, or one of its buttons. A child kept from
        ' before a refill answers with what it was, and does nothing.
        Private NotInheritable Class PartAccessibleObject
            Inherits AccessibleObject

            Private ReadOnly _list As JournalShortlistList
            Private ReadOnly _index As Integer
            Private ReadOnly _spot As Spot
            Private ReadOnly _entry As ShortlistEntry
            Private ReadOnly _generation As Integer

            Public Sub New(list As JournalShortlistList, index As Integer, spot As Spot)
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

            Public Overrides Property Name As String
                Get
                    Select Case _spot.Part
                        Case RowPart.Item : Return _entry.JournalName
                        Case RowPart.Status : Return _entry.StatusText
                        Case RowPart.Text : Return _entry.Text
                        Case RowPart.Facts : Return _entry.Facts
                        Case RowPart.Evidence : Return _entry.Evidence
                        Case RowPart.Edit : Return "Edit " & _entry.JournalName
                        Case Else : Return "More actions for " & _entry.JournalName
                    End Select
                End Get
                Set(value As String)
                    ' Named by what it shows.
                End Set
            End Property

            Public Overrides ReadOnly Property Role As AccessibleRole
                Get
                    Select Case _spot.Part
                        Case RowPart.Item : Return AccessibleRole.ListItem
                        Case RowPart.Edit, RowPart.More : Return AccessibleRole.PushButton
                        Case Else : Return AccessibleRole.StaticText
                    End Select
                End Get
            End Property

            Public Overrides ReadOnly Property DefaultAction As String
                Get
                    Return If(_spot.IsButton, "Press", Nothing)
                End Get
            End Property

            ' A row and its buttons can be focused, as the row's panel and its
            ' buttons could; its lines are read only, as its labels were.
            Public Overrides ReadOnly Property State As AccessibleStates
                Get
                    If Not IsCurrent Then Return AccessibleStates.Unavailable
                    Dim states As AccessibleStates = AccessibleStates.None
                    If _spot.Part = RowPart.Item OrElse _spot.IsButton Then
                        states = states Or AccessibleStates.Focusable
                        Dim stopIndex As Integer = _list.StopOf(_spot)
                        If stopIndex >= 0 AndAlso _list.Focused AndAlso _list._current = stopIndex Then states = states Or AccessibleStates.Focused
                        If Not _list.Enabled Then states = states Or AccessibleStates.Unavailable
                    Else
                        states = states Or AccessibleStates.ReadOnly
                    End If
                    If Not _list.Visible Then states = states Or AccessibleStates.Invisible
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
                If Not IsCurrent OrElse Not _spot.IsButton OrElse Not _list.IsHandleCreated OrElse Not _list.Enabled Then Return
                Dim list As JournalShortlistList = _list
                Dim spot As Spot = _spot
                Dim generation As Integer = _generation
                list.BeginInvoke(New Action(Sub() list.Activate(spot, generation)))
            End Sub

        End Class

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _menu.Dispose()
                ResetFonts()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
