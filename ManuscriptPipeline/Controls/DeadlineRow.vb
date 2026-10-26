Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' What one Deadlines row is drawn with: the list's font, its bold,
    ' small and day sizes, and the width each action's button takes. The
    ' list makes one set for its font and DPI and keeps it until either
    ' changes.
    Friend NotInheritable Class DeadlineMetrics
        Implements IDisposable

        Private ReadOnly _widths As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

        Public Sub New(font As Font, dpi As Integer)
            Regular = font
            Me.Dpi = dpi
            Bold = New Font(font, FontStyle.Bold)
            Small = New Font(font.FontFamily, font.SizeInPoints * 0.85F)
            Day = New Font(font.FontFamily, font.SizeInPoints * 1.35F, FontStyle.Bold)
            Link = New Font(font, FontStyle.Underline)
            RegularHeight = font.Height
            BoldHeight = Bold.Height
            SmallHeight = Small.Height
        End Sub

        ' The list's own font, not owned here.
        Public ReadOnly Property Regular As Font
        Public ReadOnly Property Bold As Font
        Public ReadOnly Property Small As Font
        Public ReadOnly Property Day As Font
        Public ReadOnly Property Link As Font
        Public ReadOnly Property Dpi As Integer

        ' The font heights, read once: a list measures every row on a fill.
        Public ReadOnly Property RegularHeight As Integer
        Public ReadOnly Property BoldHeight As Integer
        Public ReadOnly Property SmallHeight As Integer

        Public Function Px(logicalPixels As Integer) As Integer
            Return UiTheme.Px(logicalPixels, Dpi)
        End Function

        ' As wide as the button the row once hosted for the action.
        Public Function ButtonWidth(text As String) As Integer
            Dim width As Integer
            If Not _widths.TryGetValue(text, width) Then
                width = TextRenderer.MeasureText(text, Regular).Width + Px(26)
                _widths(text) = width
            End If
            Return width
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Bold.Dispose()
            Small.Dispose()
            Day.Dispose()
            Link.Dispose()
        End Sub

    End Class

    ' How one row is drawn at the moment: whether the row itself has focus,
    ' and which of its buttons shows a focus ring, the pointer, or a press.
    ' A part is a button's index, or the index after the last button for
    ' the "⋯" button; NoPart is none.
    Friend Structure DeadlineRowPaintState
        Public Focused As Boolean
        Public FocusRingPart As Integer
        Public HoverPart As Integer
        Public PressedPart As Integer
        Public Enabled As Boolean
    End Structure

    ' One row on the Deadlines page: when, what kind, what and for which
    ' manuscript, any progress, and its actions. A DeadlineList draws the
    ' rows and gives them focus, so arrow keys move between rows, Enter runs
    ' the first action, and the context-menu key opens every action.
    Friend NotInheritable Class DeadlineRow

        ' The row itself, rather than one of its buttons.
        Friend Const ItemPart As Integer = -1
        Friend Const NoPart As Integer = -2

        Private ReadOnly _today As DateTime
        Private ReadOnly _actions As New List(Of RowAction)()
        Private ReadOnly _buttons As New List(Of RowAction)()
        Private _accessibleName As String = Nothing

        ' An action: its menu text, what it does, and whether it shows as a
        ' button or only in the menu.
        Friend NotInheritable Class RowAction

            Public Sub New(text As String, handler As Action, menuOnly As Boolean)
                Me.Text = text
                Me.Handler = handler
                Me.MenuOnly = menuOnly
            End Sub

            Public ReadOnly Property Text As String
            Public ReadOnly Property Handler As Action
            Public ReadOnly Property MenuOnly As Boolean

        End Class

        Public Sub New(item As DeadlineItem, today As DateTime)
            Me.Item = item
            _today = today.Date
        End Sub

        Friend ReadOnly Property Item As DeadlineItem

        ' The first visible action is also Enter's. Menu-only actions appear
        ' under the row's "⋯" button and its context menu.
        Friend Sub AddAction(text As String, handler As Action, Optional menuOnly As Boolean = False)
            Dim action As New RowAction(text, handler, menuOnly)
            _actions.Add(action)
            If Not menuOnly Then _buttons.Add(action)
        End Sub

        ' The row has a "⋯" button when the menu holds more than the buttons
        ' show. Otherwise its space stays empty, so buttons line up down the
        ' list. Nothing is left to do once the actions are added; the call
        ' marks the end of them.
        Friend Sub FinishActions()
        End Sub

        Friend ReadOnly Property ActionTexts As IEnumerable(Of String)
            Get
                Return _actions.Select(Function(entry) entry.Text).ToList()
            End Get
        End Property

        ' Every action, in menu order.
        Friend ReadOnly Property Actions As IReadOnlyList(Of RowAction)
            Get
                Return _actions
            End Get
        End Property

        ' The actions shown as buttons, in order.
        Friend ReadOnly Property Buttons As IReadOnlyList(Of RowAction)
            Get
                Return _buttons
            End Get
        End Property

        ' The first action shown as a button, which Enter and a double-click
        ' run; Nothing when every action is in the menu.
        Friend ReadOnly Property Primary As RowAction
            Get
                Return If(_buttons.Count > 0, _buttons(0), Nothing)
            End Get
        End Property

        Friend ReadOnly Property HasMore As Boolean
            Get
                Return _actions.Count > _buttons.Count
            End Get
        End Property

        ' The part number of the "⋯" button.
        Friend ReadOnly Property MorePart As Integer
            Get
                Return _buttons.Count
            End Get
        End Property

        Friend ReadOnly Property MoreName As String
            Get
                Return "More actions for " & Item.Title
            End Get
        End Property

        ' What a screen reader reads for the row, and the description of
        ' each of its buttons.
        Friend ReadOnly Property AccessibleName As String
            Get
                If _accessibleName Is Nothing Then _accessibleName = Describe()
                Return _accessibleName
            End Get
        End Property

        ' Runs an action by its text, as a click would.
        Friend Sub RunAction(text As String)
            _actions.First(Function(entry) entry.Text = text).Handler.Invoke()
        End Sub

        Private Function Describe() As String

            Dim parts As New List(Of String) From {GroupText(), WhenText(), Item.Title, Item.ManuscriptTitle}
            If Not String.IsNullOrWhiteSpace(Item.JournalName) AndAlso Not Item.Title.Contains(Item.JournalName) Then parts.Add(Item.JournalName)
            If Item.ProgressTotal > 0 Then parts.Add(ProgressText())
            Return String.Join(", ", parts.Where(Function(part) Not String.IsNullOrWhiteSpace(part)))

        End Function

        Private Function GroupText() As String
            Select Case Item.Group
                Case DeadlineGroup.Overdue : Return "Overdue"
                Case DeadlineGroup.Today : Return "Today"
                Case DeadlineGroup.NoDate : Return "No date"
                Case DeadlineGroup.Done : Return "Done"
                Case Else : Return String.Empty
            End Select
        End Function

        ' "3 days ago", "today", "in 5 days"; for Done, when it was done.
        Friend Function WhenText() As String

            If Item.Group = DeadlineGroup.Done AndAlso Item.CompletedDate.HasValue Then
                Return "done " & Item.CompletedDate.Value.ToString("MMM d", CultureInfo.CurrentCulture)
            End If
            If Not Item.DueDate.HasValue Then Return "no date"

            Dim days As Integer = (Item.DueDate.Value.Date - _today).Days
            Select Case days
                Case 0 : Return "today"
                Case -1 : Return "yesterday"
                Case 1 : Return "tomorrow"
                Case Is < 0 : Return (-days).ToString() & " days ago"
                Case Else : Return "in " & days.ToString() & " days"
            End Select

        End Function

        Private Function ProgressText() As String
            If Item.Kind = DeadlineKind.Preparation Then
                Return Item.ProgressDone.ToString() & " of " & Item.ProgressTotal.ToString() & If(Item.ProgressTotal = 1, " required checklist item resolved", " required checklist items resolved")
            End If
            Dim text As String = Item.ProgressDone.ToString() & " of " & Item.ProgressTotal.ToString() &
                                 If(Item.ProgressTotal = 1, " comment addressed", " comments addressed")
            If Item.ProgressActive > 0 Then text &= " · " & Item.ProgressActive.ToString() & " in progress"
            Return text
        End Function

        Private Function ContextText() As String
            Dim text As String = Item.ManuscriptTitle
            If Not String.IsNullOrWhiteSpace(Item.JournalName) AndAlso Not Item.Title.Contains(Item.JournalName) Then
                text &= "  ·  " & Item.JournalName
            End If
            Return text
        End Function

        ' The row's height: at least 64 pixels, taller with a progress line.
        Friend Function MeasureHeight(metrics As DeadlineMetrics) As Integer
            Dim pad As Integer = metrics.Px(11)
            Dim height As Integer = pad * 2 + metrics.BoldHeight + metrics.RegularHeight + metrics.Px(2)
            If Item.ProgressTotal > 0 Then height += metrics.SmallHeight + metrics.Px(5)
            Return Math.Max(height, metrics.Px(64))
        End Function

        ' Where each button sits in a row drawn at rowBounds, in order, then
        ' the slot of the "⋯" button, which stays empty when the row has no
        ' more actions so buttons line up down the list. Each button is 6
        ' pixels after the one before, the slot 4, and the last 12 from the
        ' row's right edge, centered on the row.
        Friend Function ButtonBounds(rowBounds As Rectangle, metrics As DeadlineMetrics) As Rectangle()

            Dim height As Integer = metrics.Px(30)
            Dim slot As Integer = metrics.Px(32)
            Dim total As Integer = metrics.Px(4) + slot
            For Each button As RowAction In _buttons
                total += metrics.Px(6) + metrics.ButtonWidth(button.Text)
            Next

            Dim top As Integer = rowBounds.Top + (rowBounds.Height - height) \ 2
            Dim x As Integer = rowBounds.Right - metrics.Px(12) - total
            Dim bounds(_buttons.Count) As Rectangle
            For index As Integer = 0 To _buttons.Count - 1
                Dim width As Integer = metrics.ButtonWidth(_buttons(index).Text)
                x += metrics.Px(6)
                bounds(index) = New Rectangle(x, top, width, height)
                x += width
            Next
            x += metrics.Px(4)
            bounds(_buttons.Count) = New Rectangle(x, top, slot, height)
            Return bounds

        End Function

        ' Draws the row in bounds: the card with its stripe and focus border,
        ' when, the kind badge, what and for which manuscript, any progress,
        ' and the buttons. Every color is the theme's at the time of drawing.
        Friend Sub Paint(g As Graphics, bounds As Rectangle, metrics As DeadlineMetrics, state As DeadlineRowPaintState)

            Dim dpi As Integer = metrics.Dpi
            Dim font As Font = metrics.Regular
            Dim boldFont As Font = metrics.Bold
            Dim smallFont As Font = metrics.Small
            Dim dayFont As Font = metrics.Day
            Dim left As Integer = bounds.Left
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim radius As Single = UiTheme.Px(UiTheme.CardRadius, dpi)
            Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(left + 0.5F, bounds.Top + 0.5F, bounds.Width - 1.0F, bounds.Height - 1.0F), radius)
                Using fill As New SolidBrush(UiTheme.CardBackground())
                    g.FillPath(fill, path)
                End Using

                ' Overdue and due-today rows carry a stripe on their left edge.
                Dim stripe As Color = Color.Empty
                If Item.Group = DeadlineGroup.Overdue Then stripe = UiTheme.DangerColor()
                If Item.Group = DeadlineGroup.Today Then stripe = UiTheme.WarningColor()
                If stripe <> Color.Empty Then
                    Dim clip As Region = g.Clip
                    g.SetClip(path, CombineMode.Intersect)
                    Using brush As New SolidBrush(stripe)
                        g.FillRectangle(brush, left, bounds.Top, UiTheme.Px(3, dpi), bounds.Height)
                    End Using
                    g.Clip = clip
                    clip.Dispose()
                End If

                Using border As New Pen(If(state.Focused, UiTheme.AccentColor(), UiTheme.CardBorder()), If(state.Focused, 2.0F, 1.0F))
                    g.DrawPath(border, path)
                End Using
            End Using

            Dim pad As Integer = UiTheme.Px(12, dpi)
            Dim center As Integer = bounds.Top + bounds.Height \ 2
            Const flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis

            ' When: month, day, and how far away.
            Dim whenWidth As Integer = UiTheme.Px(62, dpi)
            Dim whenInk As Color = WhenInkOf(Item.Group)
            Dim centered As TextFormatFlags = flags Or TextFormatFlags.HorizontalCenter
            Dim whenTop As Integer = center - (smallFont.Height + dayFont.Height + smallFont.Height) \ 2
            If Item.DueDate.HasValue AndAlso Item.Group <> DeadlineGroup.Done Then
                TextRenderer.DrawText(g, Item.DueDate.Value.ToString("MMM", CultureInfo.CurrentCulture).ToUpperInvariant(), smallFont,
                                      New Rectangle(left + pad, whenTop, whenWidth, smallFont.Height), UiTheme.SecondaryText(), centered)
                TextRenderer.DrawText(g, Item.DueDate.Value.Day.ToString(CultureInfo.CurrentCulture), dayFont,
                                      New Rectangle(left + pad, whenTop + smallFont.Height, whenWidth, dayFont.Height), DayInkOf(Item.Group), centered)
            Else
                TextRenderer.DrawText(g, "—", dayFont, New Rectangle(left + pad, whenTop + smallFont.Height, whenWidth, dayFont.Height), UiTheme.MutedText(), centered)
            End If
            TextRenderer.DrawText(g, WhenText(), smallFont, New Rectangle(left + pad, whenTop + smallFont.Height + dayFont.Height, whenWidth, smallFont.Height), whenInk, centered)

            ' Kind badge.
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim badge As Integer = UiTheme.Px(28, dpi)
            Dim badgeLeft As Integer = left + pad + whenWidth + UiTheme.Px(8, dpi)
            Dim badgeBounds As New RectangleF(badgeLeft, center - badge / 2.0F, badge, badge)
            Dim stage As PaperStage = StageOf(Item.Kind)
            Using path As GraphicsPath = RoundedShapes.Create(badgeBounds, UiTheme.Px(7, dpi))
                Using fill As New SolidBrush(UiTheme.StageBackground(stage))
                    g.FillPath(fill, path)
                End Using
            End Using
            Dim icon As Single = UiTheme.Px(16, dpi)
            RailPainter.DrawGlyph(g, KindGlyph(), New RectangleF(badgeBounds.X + (badge - icon) / 2.0F, badgeBounds.Y + (badge - icon) / 2.0F, icon, icon),
                                  UiTheme.StageForeground(stage), Math.Max(1.4F, UiTheme.Px(2, dpi) * 0.75F))
            g.SmoothingMode = SmoothingMode.Default

            ' What, and for which manuscript; the text ends 14 pixels before
            ' the first button.
            Dim buttons As Rectangle() = ButtonBounds(bounds, metrics)
            Dim actionsLeft As Integer = If(_buttons.Count > 0, buttons(0).Left - metrics.Px(6), buttons(_buttons.Count).Left - metrics.Px(4))
            Dim textLeft As Integer = badgeLeft + badge + UiTheme.Px(12, dpi)
            Dim textWidth As Integer = Math.Max(0, actionsLeft - UiTheme.Px(14, dpi) - textLeft)
            Dim lines As Integer = boldFont.Height + font.Height + UiTheme.Px(2, dpi) + If(Item.ProgressTotal > 0, smallFont.Height + UiTheme.Px(5, dpi), 0)
            Dim y As Integer = center - lines \ 2
            TextRenderer.DrawText(g, Item.Title, boldFont, New Rectangle(textLeft, y, textWidth, boldFont.Height), TitleInkOf(Item.Group), flags)
            y += boldFont.Height + UiTheme.Px(2, dpi)
            TextRenderer.DrawText(g, ContextText(), font, New Rectangle(textLeft, y, textWidth, font.Height), UiTheme.SecondaryText(), flags)
            y += font.Height

            If Item.ProgressTotal > 0 Then
                y += UiTheme.Px(5, dpi)
                Dim barWidth As Integer = UiTheme.Px(120, dpi)
                Dim barHeight As Integer = UiTheme.Px(6, dpi)
                Dim barTop As Integer = y + (smallFont.Height - barHeight) \ 2
                g.SmoothingMode = SmoothingMode.AntiAlias
                Using track As GraphicsPath = RoundedShapes.Create(New RectangleF(textLeft, barTop, barWidth, barHeight), barHeight / 2.0F)
                    Using fill As New SolidBrush(UiTheme.SubtleBorder())
                        g.FillPath(fill, track)
                    End Using
                    Dim clip As Region = g.Clip
                    g.SetClip(track, CombineMode.Intersect)
                    Dim doneWidth As Single = barWidth * Item.ProgressDone / CSng(Item.ProgressTotal)
                    Dim activeWidth As Single = barWidth * Item.ProgressActive / CSng(Item.ProgressTotal)
                    Using doneBrush As New SolidBrush(UiTheme.SuccessColor()), activeBrush As New SolidBrush(UiTheme.AccentSecondaryColor())
                        g.FillRectangle(doneBrush, textLeft, barTop, doneWidth, barHeight)
                        g.FillRectangle(activeBrush, textLeft + doneWidth, barTop, activeWidth, barHeight)
                    End Using
                    g.Clip = clip
                    clip.Dispose()
                End Using
                g.SmoothingMode = SmoothingMode.Default
                TextRenderer.DrawText(g, ProgressText(), smallFont,
                                      New Rectangle(textLeft + barWidth + UiTheme.Px(8, dpi), y, Math.Max(0, textWidth - barWidth - UiTheme.Px(8, dpi)), smallFont.Height),
                                      UiTheme.SecondaryText(), flags)
            End If

            ' The buttons, drawn as the ActionButtons the row once hosted.
            Const buttonFlags As TextFormatFlags = TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                                   TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix
            For index As Integer = 0 To _buttons.Count - 1
                ActionButton.PaintFace(g, buttons(index), ActionButtonRole.Secondary, _buttons(index).Text, font, buttonFlags,
                                       state.HoverPart = index, state.PressedPart = index, state.FocusRingPart = index, state.Enabled, dpi)
            Next
            If HasMore Then
                ActionButton.PaintFace(g, buttons(_buttons.Count), ActionButtonRole.Quiet, "⋯", font, buttonFlags,
                                       state.HoverPart = MorePart, state.PressedPart = MorePart, state.FocusRingPart = MorePart, state.Enabled, dpi)
            End If
            g.SmoothingMode = SmoothingMode.Default

        End Sub

        ' The inks a row picks by its group, read when it is drawn: the date
        ' and how far away it is in danger when overdue and warning when due
        ' today; the day of the month in primary text from tomorrow on; the
        ' title in secondary text once done.
        Friend Shared Function WhenInkOf(group As DeadlineGroup) As Color
            Return If(group = DeadlineGroup.Overdue, UiTheme.DangerColor(),
                      If(group = DeadlineGroup.Today, UiTheme.WarningColor(), UiTheme.SecondaryText()))
        End Function

        Friend Shared Function DayInkOf(group As DeadlineGroup) As Color
            Return If(group = DeadlineGroup.Next7Days OrElse group = DeadlineGroup.Later, UiTheme.PrimaryText(), WhenInkOf(group))
        End Function

        Friend Shared Function TitleInkOf(group As DeadlineGroup) As Color
            Return If(group = DeadlineGroup.Done, UiTheme.SecondaryText(), UiTheme.PrimaryText())
        End Function

        ' Kind colors follow the board's stage pills.
        Friend Shared Function StageOf(kind As DeadlineKind) As PaperStage
            Select Case kind
                Case DeadlineKind.Revision : Return PaperStage.Revision
                Case DeadlineKind.FollowUp : Return PaperStage.Submitted
                Case DeadlineKind.Preparation : Return PaperStage.UnderReview
                Case DeadlineKind.Publication : Return PaperStage.Published
                Case Else : Return PaperStage.Draft
            End Select
        End Function

        Private Function KindGlyph() As RailGlyph
            Select Case Item.Kind
                Case DeadlineKind.Revision : Return RailGlyph.Revision
                Case DeadlineKind.FollowUp : Return RailGlyph.FollowUp
                Case DeadlineKind.Preparation : Return RailGlyph.Checklist
                Case DeadlineKind.Publication : Return RailGlyph.Publication
                Case Else : Return RailGlyph.Reminders
            End Select
        End Function

    End Class

End Namespace
