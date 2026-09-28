Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' One row on the Deadlines page: when, what kind, what and for which
    ' manuscript, any progress, and its actions. The row itself takes focus,
    ' so arrow keys move between rows, Enter runs the first action, and the
    ' context-menu key opens every action.
    Friend Class DeadlineRow
        Inherits Control

        Private ReadOnly _today As DateTime
        Private ReadOnly _actions As FlowLayoutPanel
        Private ReadOnly _menu As New ContextMenuStrip()
        Private _primary As Action = Nothing
        Private _boldFont As Font
        Private _smallFont As Font
        Private _dayFont As Font

        Public Sub New(item As DeadlineItem, today As DateTime)

            Me.Item = item
            _today = today.Date

            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            TabStop = True
            BackColor = UiTheme.CardBackground()
            AccessibleRole = AccessibleRole.ListItem
            AccessibleName = Describe()
            ContextMenuStrip = _menu

            _actions = New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .WrapContents = False,
                .Margin = New Padding(0),
                .BackColor = UiTheme.CardBackground()
            }
            Controls.Add(_actions)

        End Sub

        <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
        Friend ReadOnly Property Item As DeadlineItem

        ' The first visible action is also Enter's. Menu-only actions appear
        ' under the row's "⋯" button and its context menu.
        Friend Sub AddAction(text As String, handler As Action, Optional menuOnly As Boolean = False)

            If _primary Is Nothing AndAlso Not menuOnly Then _primary = handler
            _menu.Items.Add(text, Nothing, Sub(sender, e) handler())
            If menuOnly Then Return

            Dim button As New ActionButton With {
                .Text = text,
                .UseMnemonic = False,
                .Height = UiTheme.Px(30, DeviceDpi),
                .Width = TextRenderer.MeasureText(text, Font).Width + UiTheme.Px(26, DeviceDpi),
                .Margin = New Padding(UiTheme.Px(6, DeviceDpi), 0, 0, 0),
                .AccessibleDescription = Describe()
            }
            AddHandler button.Click, Sub(sender, e) handler()
            _actions.Controls.Add(button)

        End Sub

        ' Adds the "⋯" button when the menu holds more than the buttons show.
        ' Otherwise its space stays empty, so buttons line up down the list.
        Friend Sub FinishActions()

            If _menu.Items.Count <= _actions.Controls.Count Then
                _actions.Controls.Add(New Panel With {
                    .Size = New Size(UiTheme.Px(32, DeviceDpi), UiTheme.Px(30, DeviceDpi)),
                    .Margin = New Padding(UiTheme.Px(4, DeviceDpi), 0, 0, 0),
                    .TabStop = False
                })
                Return
            End If

            Dim more As New ActionButton With {
                .Text = "⋯",
                .Role = ActionButtonRole.Quiet,
                .UseMnemonic = False,
                .Height = UiTheme.Px(30, DeviceDpi),
                .Width = UiTheme.Px(32, DeviceDpi),
                .Margin = New Padding(UiTheme.Px(4, DeviceDpi), 0, 0, 0),
                .AccessibleName = "More actions for " & Item.Title
            }
            AddHandler more.Click, Sub(sender, e) _menu.Show(more, New Point(0, more.Height))
            _actions.Controls.Add(more)

        End Sub

        Friend ReadOnly Property ActionTexts As IEnumerable(Of String)
            Get
                Return _menu.Items.Cast(Of ToolStripItem)().Select(Function(entry) entry.Text).ToList()
            End Get
        End Property

        ' Runs an action by its text, as a click would.
        Friend Sub RunAction(text As String)
            _menu.Items.Cast(Of ToolStripItem)().First(Function(entry) entry.Text = text).PerformClick()
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
                Return Item.ProgressDone.ToString() & " of " & Item.ProgressTotal.ToString() & " required checklist items resolved"
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

        Private ReadOnly Property BoldFont As Font
            Get
                If _boldFont Is Nothing Then _boldFont = New Font(Font, FontStyle.Bold)
                Return _boldFont
            End Get
        End Property

        Private ReadOnly Property SmallFont As Font
            Get
                If _smallFont Is Nothing Then _smallFont = New Font(Font.FontFamily, Font.SizeInPoints * 0.85F)
                Return _smallFont
            End Get
        End Property

        Private ReadOnly Property DayFont As Font
            Get
                If _dayFont Is Nothing Then _dayFont = New Font(Font.FontFamily, Font.SizeInPoints * 1.35F, FontStyle.Bold)
                Return _dayFont
            End Get
        End Property

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim pad As Integer = UiTheme.Px(11, DeviceDpi)
            Dim height As Integer = pad * 2 + BoldFont.Height + Font.Height + UiTheme.Px(2, DeviceDpi)
            If Item.ProgressTotal > 0 Then height += SmallFont.Height + UiTheme.Px(5, DeviceDpi)
            Return New Size(proposedSize.Width, Math.Max(height, UiTheme.Px(64, DeviceDpi)))
        End Function

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            _actions.Location = New Point(Width - _actions.Width - UiTheme.Px(12, DeviceDpi), (Height - _actions.Height) \ 2)
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            For Each cached As Font In {_boldFont, _smallFont, _dayFont}
                cached?.Dispose()
            Next
            _boldFont = Nothing : _smallFont = Nothing : _dayFont = Nothing
            MyBase.OnFontChanged(e)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
        End Sub

        Protected Overrides Sub OnDoubleClick(e As EventArgs)
            MyBase.OnDoubleClick(e)
            _primary?.Invoke()
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData
                Case Keys.Up, Keys.Down, Keys.Enter : Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            Select Case e.KeyCode
                Case Keys.Enter
                    _primary?.Invoke()
                    e.Handled = True
                Case Keys.Up, Keys.Down
                    MoveFocus(e.KeyCode = Keys.Down)
                    e.Handled = True
            End Select
        End Sub

        Private Sub MoveFocus(forward As Boolean)
            Dim rows As List(Of DeadlineRow) = Parent.Controls.OfType(Of DeadlineRow)().Where(Function(row) row.Visible).ToList()
            Dim index As Integer = rows.IndexOf(Me) + If(forward, 1, -1)
            If index >= 0 AndAlso index < rows.Count Then rows(index).Focus()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            Dim dpi As Integer = DeviceDpi
            g.Clear(SectionCard.SurfaceBehind(Me))
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim radius As Single = UiTheme.Px(UiTheme.CardRadius, dpi)
            Using path As GraphicsPath = RoundedShapes.Create(New RectangleF(0.5F, 0.5F, Width - 1.0F, Height - 1.0F), radius)
                Using fill As New SolidBrush(UiTheme.CardBackground())
                    g.FillPath(fill, path)
                End Using

                ' Overdue and due-today rows carry a stripe on their left edge.
                Dim stripe As Color = Color.Empty
                If Item.Group = DeadlineGroup.Overdue Then stripe = UiTheme.DangerColor()
                If Item.Group = DeadlineGroup.Today Then stripe = UiTheme.WarningColor()
                If stripe <> Color.Empty Then
                    Dim clip As Region = g.Clip
                    g.SetClip(path)
                    Using brush As New SolidBrush(stripe)
                        g.FillRectangle(brush, 0, 0, UiTheme.Px(3, dpi), Height)
                    End Using
                    g.Clip = clip
                End If

                Using border As New Pen(If(Focused, UiTheme.AccentColor(), UiTheme.CardBorder()), If(Focused, 2.0F, 1.0F))
                    g.DrawPath(border, path)
                End Using
            End Using

            Dim pad As Integer = UiTheme.Px(12, dpi)
            Dim center As Integer = Height \ 2
            Const flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis

            ' When: month, day, and how far away.
            Dim whenWidth As Integer = UiTheme.Px(62, dpi)
            Dim whenInk As Color = If(Item.Group = DeadlineGroup.Overdue, UiTheme.DangerColor(),
                                      If(Item.Group = DeadlineGroup.Today, UiTheme.WarningColor(), UiTheme.SecondaryText()))
            Dim centered As TextFormatFlags = flags Or TextFormatFlags.HorizontalCenter
            Dim whenTop As Integer = center - (SmallFont.Height + DayFont.Height + SmallFont.Height) \ 2
            If Item.DueDate.HasValue AndAlso Item.Group <> DeadlineGroup.Done Then
                TextRenderer.DrawText(g, Item.DueDate.Value.ToString("MMM", CultureInfo.CurrentCulture).ToUpperInvariant(), SmallFont,
                                      New Rectangle(pad, whenTop, whenWidth, SmallFont.Height), UiTheme.SecondaryText(), centered)
                TextRenderer.DrawText(g, Item.DueDate.Value.Day.ToString(CultureInfo.CurrentCulture), DayFont,
                                      New Rectangle(pad, whenTop + SmallFont.Height, whenWidth, DayFont.Height), If(Item.Group = DeadlineGroup.Next7Days OrElse Item.Group = DeadlineGroup.Later, UiTheme.PrimaryText(), whenInk), centered)
            Else
                TextRenderer.DrawText(g, "—", DayFont, New Rectangle(pad, whenTop + SmallFont.Height, whenWidth, DayFont.Height), UiTheme.MutedText(), centered)
            End If
            TextRenderer.DrawText(g, WhenText(), SmallFont, New Rectangle(pad, whenTop + SmallFont.Height + DayFont.Height, whenWidth, SmallFont.Height), whenInk, centered)

            ' Kind badge.
            Dim badge As Integer = UiTheme.Px(28, dpi)
            Dim badgeLeft As Integer = pad + whenWidth + UiTheme.Px(8, dpi)
            Dim badgeBounds As New RectangleF(badgeLeft, center - badge / 2.0F, badge, badge)
            Dim stage As PaperStage = KindStage()
            Using path As GraphicsPath = RoundedShapes.Create(badgeBounds, UiTheme.Px(7, dpi))
                Using fill As New SolidBrush(UiTheme.StageBackground(stage))
                    g.FillPath(fill, path)
                End Using
            End Using
            Dim icon As Single = UiTheme.Px(16, dpi)
            RailPainter.DrawGlyph(g, KindGlyph(), New RectangleF(badgeBounds.X + (badge - icon) / 2.0F, badgeBounds.Y + (badge - icon) / 2.0F, icon, icon),
                                  UiTheme.StageForeground(stage), Math.Max(1.4F, UiTheme.Px(2, dpi) * 0.75F))
            g.SmoothingMode = SmoothingMode.Default

            ' What, and for which manuscript.
            Dim textLeft As Integer = badgeLeft + badge + UiTheme.Px(12, dpi)
            Dim textWidth As Integer = Math.Max(0, _actions.Left - UiTheme.Px(14, dpi) - textLeft)
            Dim lines As Integer = BoldFont.Height + Font.Height + UiTheme.Px(2, dpi) + If(Item.ProgressTotal > 0, SmallFont.Height + UiTheme.Px(5, dpi), 0)
            Dim y As Integer = center - lines \ 2
            TextRenderer.DrawText(g, Item.Title, BoldFont, New Rectangle(textLeft, y, textWidth, BoldFont.Height),
                                  If(Item.Group = DeadlineGroup.Done, UiTheme.SecondaryText(), UiTheme.PrimaryText()), flags)
            y += BoldFont.Height + UiTheme.Px(2, dpi)
            TextRenderer.DrawText(g, ContextText(), Font, New Rectangle(textLeft, y, textWidth, Font.Height), UiTheme.SecondaryText(), flags)
            y += Font.Height

            If Item.ProgressTotal > 0 Then
                y += UiTheme.Px(5, dpi)
                Dim barWidth As Integer = UiTheme.Px(120, dpi)
                Dim barHeight As Integer = UiTheme.Px(6, dpi)
                Dim barTop As Integer = y + (SmallFont.Height - barHeight) \ 2
                g.SmoothingMode = SmoothingMode.AntiAlias
                Using track As GraphicsPath = RoundedShapes.Create(New RectangleF(textLeft, barTop, barWidth, barHeight), barHeight / 2.0F)
                    Using fill As New SolidBrush(UiTheme.SubtleBorder())
                        g.FillPath(fill, track)
                    End Using
                    Dim clip As Region = g.Clip
                    g.SetClip(track)
                    Dim doneWidth As Single = barWidth * Item.ProgressDone / CSng(Item.ProgressTotal)
                    Dim activeWidth As Single = barWidth * Item.ProgressActive / CSng(Item.ProgressTotal)
                    Using doneBrush As New SolidBrush(UiTheme.SuccessColor()), activeBrush As New SolidBrush(UiTheme.AccentSecondaryColor())
                        g.FillRectangle(doneBrush, textLeft, barTop, doneWidth, barHeight)
                        g.FillRectangle(activeBrush, textLeft + doneWidth, barTop, activeWidth, barHeight)
                    End Using
                    g.Clip = clip
                End Using
                g.SmoothingMode = SmoothingMode.Default
                TextRenderer.DrawText(g, ProgressText(), SmallFont,
                                      New Rectangle(textLeft + barWidth + UiTheme.Px(8, dpi), y, Math.Max(0, textWidth - barWidth - UiTheme.Px(8, dpi)), SmallFont.Height),
                                      UiTheme.SecondaryText(), flags)
            End If

        End Sub

        ' Kind colors follow the board's stage pills.
        Private Function KindStage() As PaperStage
            Select Case Item.Kind
                Case DeadlineKind.Revision : Return PaperStage.Revision
                Case DeadlineKind.FollowUp : Return PaperStage.Submitted
                Case DeadlineKind.Preparation : Return PaperStage.UnderReview
                Case Else : Return PaperStage.Draft
            End Select
        End Function

        Private Function KindGlyph() As RailGlyph
            Select Case Item.Kind
                Case DeadlineKind.Revision : Return RailGlyph.Revision
                Case DeadlineKind.FollowUp : Return RailGlyph.FollowUp
                Case DeadlineKind.Preparation : Return RailGlyph.Checklist
                Case Else : Return RailGlyph.Reminders
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                For Each cached As Font In {_boldFont, _smallFont, _dayFont}
                    cached?.Dispose()
                Next
                _menu.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
