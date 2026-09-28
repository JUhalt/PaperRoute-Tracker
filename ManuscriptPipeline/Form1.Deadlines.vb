Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The Deadlines page (#28): what needs action, and when. Rows are rebuilt
' from the library each time; every action changes the record that owns the
' row and saves, like any other edit.
Partial Public Class Form1

    Private deadlinesList As TableLayoutPanel = Nothing
    Private deadlinesFilter As TextBox = Nothing
    Private deadlinesKind As DeadlineKind? = Nothing
    Private deadlinesShowDone As Boolean = False
    Private ReadOnly deadlineChips As New Dictionary(Of String, FilterChip)()
    Private ReadOnly deadlineGlance As New Dictionary(Of DeadlineGroup, Label)()

    ' Tests answer the date and confirmation prompts.
    Friend deadlineDatePrompt As Func(Of DeadlineItem, DateTime?) = Nothing
    Friend deadlineConfirmPrompt As Func(Of String, Boolean) = Nothing

    Private Shared ReadOnly OpenGroups As DeadlineGroup() = {
        DeadlineGroup.Overdue, DeadlineGroup.Today, DeadlineGroup.Next7Days, DeadlineGroup.Later, DeadlineGroup.NoDate
    }


    Private Function BuildDeadlinesPage() As Control

        Dim dpi As Integer = DeviceDpi

        deadlinesFilter = New TextBox With {
            .PlaceholderText = "Filter by manuscript or journal",
            .BorderStyle = BorderStyle.None,
            .BackColor = UiTheme.CardBackground(),
            .ForeColor = UiTheme.PrimaryText(),
            .AccessibleName = "Filter deadlines"
        }
        deadlinesFilter.Width = Math.Max(UiTheme.Px(240, dpi), TextRenderer.MeasureText(deadlinesFilter.PlaceholderText, Me.Font).Width + UiTheme.Px(16, dpi))
        AddHandler deadlinesFilter.TextChanged, Sub(sender, e) FillDeadlines()

        Dim btnExport As New ActionButton With {
            .Text = "Export Calendar...",
            .Width = GetResponsiveButtonWidth("Export Calendar...", 140),
            .Height = GetResponsiveButtonHeight(34),
            .AccessibleDescription = "Saves the dated items as an .ics file for Outlook, Google Calendar, or Apple Calendar."
        }
        AddHandler btnExport.Click, AddressOf ExportDeadlineCalendar

        Dim btnAdd As New ActionButton With {
            .Text = "+ Add Reminder",
            .Role = ActionButtonRole.Primary,
            .AccessibleName = "Add Reminder",
            .Width = GetResponsiveButtonWidth("+ Add Reminder", 140),
            .Height = GetResponsiveButtonHeight(34)
        }
        AddHandler btnAdd.Click, AddressOf AddDeadlineReminder

        Dim frame As TableLayoutPanel = CreatePageFrame(
            "Deadlines",
            DateTime.Today.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture) &
                ". Revision deadlines, journal follow-ups, your reminders, and unfinished preparation.",
            CreateSearchField(deadlinesFilter), btnExport, btnAdd)

        Dim body As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        body.Controls.Add(BuildDeadlineGlance(), 0, 0)
        body.Controls.Add(BuildDeadlineChips(), 0, 1)

        Dim scroller As New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground(),
            .AccessibleName = "Deadlines",
            .AccessibleRole = AccessibleRole.List
        }
        deadlinesList = New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0, 0, UiTheme.Px(4, dpi), UiTheme.Px(12, dpi)),
            .BackColor = UiTheme.BoardBackground()
        }
        deadlinesList.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        scroller.Controls.Add(deadlinesList)
        body.Controls.Add(scroller, 0, 2)

        frame.Controls.Add(body, 0, 2)
        FillDeadlines()
        Return frame

    End Function


    ' Four counts across the top; they ignore the filters.
    Private Function BuildDeadlineGlance() As Control

        Dim dpi As Integer = DeviceDpi
        Dim strip As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, dpi)),
            .BackColor = UiTheme.BoardBackground(),
            .AccessibleName = "At a glance"
        }

        deadlineGlance.Clear()

        For Each tile In {
            (DeadlineGroup.Overdue, "overdue", UiTheme.DangerColor()),
            (DeadlineGroup.Today, "due today", UiTheme.WarningColor()),
            (DeadlineGroup.Next7Days, "in the next 7 days", UiTheme.PrimaryText()),
            (DeadlineGroup.NoDate, "without a date", UiTheme.SecondaryText())
        }
            Dim panel As New RoundedPanel With {
                .BackColor = UiTheme.CardBackground(),
                .BorderColor = UiTheme.CardBorder(),
                .BorderThickness = 1.0F,
                .CornerRadius = UiTheme.Px(UiTheme.ControlRadius, dpi),
                .Padding = New Padding(UiTheme.Px(12, dpi), UiTheme.Px(8, dpi), UiTheme.Px(12, dpi), UiTheme.Px(8, dpi)),
                .Margin = New Padding(0, 0, UiTheme.Px(10, dpi), 0)
            }
            Dim count As New Label With {
                .AutoSize = True,
                .Text = "0",
                .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.45F, FontStyle.Bold),
                .ForeColor = tile.Item3,
                .BackColor = UiTheme.CardBackground(),
                .Location = New Point(panel.Padding.Left, panel.Padding.Top)
            }
            Dim caption As New Label With {
                .AutoSize = True,
                .Text = tile.Item2,
                .ForeColor = UiTheme.SecondaryText(),
                .BackColor = UiTheme.CardBackground()
            }
            panel.Controls.Add(count)
            panel.Controls.Add(caption)
            caption.Location = New Point(panel.Padding.Left, count.Bottom)
            panel.Size = New Size(
                Math.Max(UiTheme.Px(132, dpi), TextRenderer.MeasureText(tile.Item2, Me.Font).Width + panel.Padding.Horizontal),
                caption.Bottom + panel.Padding.Bottom)
            deadlineGlance(tile.Item1) = count
            strip.Controls.Add(panel)
        Next

        Return strip

    End Function


    Private Function BuildDeadlineChips() As Control

        Dim dpi As Integer = DeviceDpi
        Dim row As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(4, dpi)),
            .BackColor = UiTheme.BoardBackground(),
            .AccessibleName = "Show"
        }

        deadlineChips.Clear()

        For Each entry In {
            ("All", CType(Nothing, DeadlineKind?)),
            ("Revisions", CType(DeadlineKind.Revision, DeadlineKind?)),
            ("Follow-ups", CType(DeadlineKind.FollowUp, DeadlineKind?)),
            ("Reminders", CType(DeadlineKind.Reminder, DeadlineKind?)),
            ("Preparation", CType(DeadlineKind.Preparation, DeadlineKind?))
        }
            Dim kind As DeadlineKind? = entry.Item2
            Dim chip As New FilterChip With {
                .Text = entry.Item1,
                .Margin = New Padding(0, 0, UiTheme.Px(8, dpi), UiTheme.Px(6, dpi))
            }
            AddHandler chip.Click,
                Sub(sender, e)
                    deadlinesKind = If(Nullable.Equals(deadlinesKind, kind), Nothing, kind)
                    FillDeadlines()
                End Sub
            deadlineChips(entry.Item1) = chip
            row.Controls.Add(chip)
        Next

        Return row

    End Function


    Private Sub FillDeadlines()

        If deadlinesList Is Nothing OrElse deadlinesList.IsDisposed Then
            Return
        End If

        Dim today As DateTime = DateTime.Today
        Dim all As List(Of DeadlineItem) = DeadlineService.Build(manuscripts, today)
        Dim open As List(Of DeadlineItem) = all.Where(Function(item) item.Group <> DeadlineGroup.Done).ToList()

        For Each tile In deadlineGlance
            tile.Value.Text = open.Where(Function(item) item.Group = tile.Key).Count().ToString(CultureInfo.CurrentCulture)
        Next

        For Each chip In deadlineChips
            Dim kind As DeadlineKind? = ChipKind(chip.Key)
            Dim count As Integer = open.Where(Function(item) Not kind.HasValue OrElse item.Kind = kind.Value).Count()
            chip.Value.Text = chip.Key & "  " & count.ToString(CultureInfo.CurrentCulture)
            chip.Value.Tone = If(Nullable.Equals(deadlinesKind, kind), FilterChipTone.Active, FilterChipTone.Neutral)
        Next

        Dim filter As String = If(deadlinesFilter?.Text, String.Empty).Trim()
        Dim shown As List(Of DeadlineItem) = all.Where(
            Function(item) (Not deadlinesKind.HasValue OrElse item.Kind = deadlinesKind.Value) AndAlso
                           (filter.Length = 0 OrElse
                            {item.ManuscriptTitle, item.JournalName, item.Title}.Any(
                                Function(value) If(value, String.Empty).IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0))).ToList()

        ' Keep the keyboard where it was across a rebuild.
        Dim focusedIndex As Integer =
            deadlinesList.Controls.OfType(Of DeadlineRow)().ToList().FindIndex(Function(row) row.ContainsFocus)

        deadlinesList.SuspendLayout()
        For Each child As Control In deadlinesList.Controls.Cast(Of Control)().ToList()
            deadlinesList.Controls.Remove(child)
            child.Dispose()
        Next
        deadlinesList.RowStyles.Clear()
        deadlinesList.RowCount = 0

        For Each group As DeadlineGroup In OpenGroups
            Dim items As List(Of DeadlineItem) = shown.Where(Function(item) item.Group = group).ToList()
            If items.Count = 0 Then Continue For
            AddDeadlineHeading(GroupHeading(group), items.Count, group)
            For Each item As DeadlineItem In items
                AddDeadlineRow(CreateDeadlineRow(item, today))
            Next
        Next

        If Not shown.Any(Function(item) item.Group <> DeadlineGroup.Done) Then
            AddDeadlineRow(New Label With {
                .Text = If(open.Count = 0,
                           "Nothing needs action right now. Revision deadlines, journal follow-ups, your reminders, and unfinished submission preparation appear here.",
                           "Nothing matches this filter."),
                .AutoSize = True,
                .UseMnemonic = False,
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, UiTheme.Px(18, DeviceDpi), 0, UiTheme.Px(18, DeviceDpi))
            })
        End If

        Dim done As List(Of DeadlineItem) = shown.Where(Function(item) item.Group = DeadlineGroup.Done).ToList()
        If done.Count > 0 Then
            AddDoneHeading(done.Count)
            If deadlinesShowDone Then
                For Each item As DeadlineItem In done
                    AddDeadlineRow(CreateDeadlineRow(item, today))
                Next
            End If
        End If

        deadlinesList.ResumeLayout(True)

        Dim rows As List(Of DeadlineRow) = deadlinesList.Controls.OfType(Of DeadlineRow)().ToList()
        If focusedIndex >= 0 AndAlso rows.Count > 0 Then
            rows(Math.Min(focusedIndex, rows.Count - 1)).Focus()
        End If

    End Sub


    Private Shared Function ChipKind(name As String) As DeadlineKind?
        Select Case name
            Case "Revisions" : Return DeadlineKind.Revision
            Case "Follow-ups" : Return DeadlineKind.FollowUp
            Case "Reminders" : Return DeadlineKind.Reminder
            Case "Preparation" : Return DeadlineKind.Preparation
            Case Else : Return Nothing
        End Select
    End Function


    Private Shared Function GroupHeading(group As DeadlineGroup) As String
        Select Case group
            Case DeadlineGroup.Overdue : Return "OVERDUE"
            Case DeadlineGroup.Today : Return "TODAY"
            Case DeadlineGroup.Next7Days : Return "NEXT 7 DAYS"
            Case DeadlineGroup.Later : Return "LATER"
            Case Else : Return "NO DATE"
        End Select
    End Function


    Private Sub AddDeadlineRow(control As Control)
        control.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        If TypeOf control Is DeadlineRow Then
            control.Margin = New Padding(0, 0, 0, UiTheme.Px(6, DeviceDpi))
            control.Height = control.GetPreferredSize(Size.Empty).Height
        End If
        deadlinesList.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        deadlinesList.Controls.Add(control, 0, deadlinesList.RowCount)
        deadlinesList.RowCount += 1
    End Sub


    Private Sub AddDeadlineHeading(text As String, count As Integer, group As DeadlineGroup)
        AddDeadlineRow(New Label With {
            .Text = text & "  " & count.ToString(CultureInfo.CurrentCulture),
            .AutoSize = True,
            .UseMnemonic = False,
            .Font = attentionTitleFont,
            .ForeColor = If(group = DeadlineGroup.Overdue, UiTheme.DangerColor(),
                            If(group = DeadlineGroup.Today, UiTheme.WarningColor(), UiTheme.MutedText())),
            .Margin = New Padding(0, UiTheme.Px(10, DeviceDpi), 0, UiTheme.Px(6, DeviceDpi)),
            .AccessibleRole = AccessibleRole.StaticText
        })
    End Sub


    ' Done stays collapsed behind Show.
    Private Sub AddDoneHeading(count As Integer)
        Dim link As New LinkLabel With {
            .Text = "Done in the last " & DeadlineService.DoneDays.ToString(CultureInfo.CurrentCulture) & " days: " &
                    count.ToString(CultureInfo.CurrentCulture) & "  ·  " & If(deadlinesShowDone, "Hide", "Show"),
            .AutoSize = True,
            .UseMnemonic = False,
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .ForeColor = UiTheme.SecondaryText(),
            .Margin = New Padding(0, UiTheme.Px(12, DeviceDpi), 0, UiTheme.Px(6, DeviceDpi)),
            .AccessibleName = If(deadlinesShowDone, "Hide done items", "Show done items")
        }
        link.LinkArea = New LinkArea(link.Text.Length - 4, 4)
        AddHandler link.LinkClicked,
            Sub(sender, e)
                deadlinesShowDone = Not deadlinesShowDone
                FillDeadlines()
            End Sub
        AddDeadlineRow(link)
    End Sub


    ' Each kind has its own actions; the first is also Enter's.
    Private Function CreateDeadlineRow(item As DeadlineItem, today As DateTime) As DeadlineRow

        Dim row As New DeadlineRow(item, today)

        Select Case True
            Case item.Group = DeadlineGroup.Done
                row.AddAction("Open Manuscript", Sub() OpenDeadline(item), menuOnly:=True)

            Case item.Kind = DeadlineKind.Revision AndAlso Not item.DueDate.HasValue
                row.AddAction("Set Deadline...", Sub() PostponeDeadline(item))
                row.AddAction("Open", Sub() OpenDeadline(item))

            Case item.Kind = DeadlineKind.Revision
                row.AddAction("Open", Sub() OpenDeadline(item))
                row.AddAction("Postpone...", Sub() PostponeDeadline(item))

            Case item.Kind = DeadlineKind.FollowUp
                row.AddAction("Open", Sub() OpenDeadline(item))
                row.AddAction("Postpone...", Sub() PostponeDeadline(item))
                row.AddAction("Clear Follow-up...", Sub() ClearDeadlineFollowUp(item), menuOnly:=True)

            Case item.Kind = DeadlineKind.Reminder
                row.AddAction("Done", Sub() CompleteDeadlineReminder(item))
                row.AddAction("Postpone...", Sub() PostponeDeadline(item))
                row.AddAction("Edit Reminder...", Sub() EditDeadlineReminder(item), menuOnly:=True)
                row.AddAction("Open Manuscript", Sub() OpenDeadline(item), menuOnly:=True)

            Case Else
                row.AddAction("Open Readiness", Sub() OpenDeadline(item))

        End Select

        row.FinishActions()
        Return row

    End Function


    ' Opens the manuscript where the item lives: a submission (at its reviewer
    ' responses for a revision with comments), or Readiness & Packets.
    Private Sub OpenDeadline(item As DeadlineItem)

        Dim manuscript As Manuscript = FindManuscript(item.ManuscriptId)
        If manuscript Is Nothing Then Return

        OpenManuscript(manuscript)

        If currentPage <> WorkspacePage.Manuscript OrElse manuscriptEditor Is Nothing OrElse manuscriptEditor.IsDisposed Then
            Return
        End If

        Select Case item.Kind
            Case DeadlineKind.Revision, DeadlineKind.FollowUp
                If item.SubmissionId.HasValue Then
                    manuscriptEditor.ShowSubmission(item.SubmissionId.Value, showResponses:=item.Kind = DeadlineKind.Revision AndAlso item.ProgressTotal > 0)
                End If
            Case DeadlineKind.Preparation
                manuscriptEditor.ShowReadinessAndPackets()
        End Select

    End Sub


    Private Sub PostponeDeadline(item As DeadlineItem)

        Dim chosen As DateTime? = PromptDeadlineDate(item)
        If Not chosen.HasValue Then Return

        Dim dateText As String = chosen.Value.ToString("MMM d", CultureInfo.CurrentCulture)
        ChangeDeadlineDate(item, chosen,
                           If(item.DueDate.HasValue, "Moved """ & item.Title & """ to " & dateText & ".",
                                                     "Set the revision deadline to " & dateText & "."))

    End Sub


    Private Function PromptDeadlineDate(item As DeadlineItem) As DateTime?

        If deadlineDatePrompt IsNot Nothing Then
            Return deadlineDatePrompt(item)
        End If

        Dim heading As String
        Dim note As String
        Dim verb As String = "Postpone to"

        Select Case item.Kind
            Case DeadlineKind.FollowUp
                heading = "Postpone the follow-up"
                note = "This changes the follow-up date on the " & If(String.IsNullOrWhiteSpace(item.JournalName), "journal", item.JournalName) &
                       " submission. The manuscript page shows the same date."
            Case DeadlineKind.Revision
                If item.DueDate.HasValue Then
                    heading = "Postpone the revision deadline"
                    note = "This changes the revision deadline on the editorial decision. The manuscript page shows the same date."
                Else
                    heading = "Set the revision deadline"
                    note = "The deadline is saved on the latest editorial decision, as if you had entered it there."
                    verb = "Set to"
                End If
            Case Else
                heading = "Postpone the reminder"
                note = "This changes the reminder's date."
        End Select

        Using dialog As New DeadlineDateForm(heading, item.Title & "  ·  " & item.ManuscriptTitle, note, verb, item.DueDate, DateTime.Today)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return Nothing
            Return dialog.SelectedDate
        End Using

    End Function


    ' Changes the owning record, saves, and puts the old date back if the
    ' save fails.
    Private Sub ChangeDeadlineDate(item As DeadlineItem, newDate As DateTime?, outcome As String)

        Dim manuscript As Manuscript = FindManuscript(item.ManuscriptId)
        If manuscript Is Nothing Then Return

        Dim previous As DateTime?
        Try
            previous = DeadlineService.SetDate(manuscript, item, newDate)
        Catch ex As InvalidOperationException
            MessageBox.Show(Me, ex.Message, "Deadlines", MessageBoxButtons.OK, MessageBoxIcon.Information)
            FillDeadlines()
            Return
        End Try

        If Not SaveManuscripts() Then
            DeadlineService.SetDate(manuscript, item, previous)
            FillDeadlines()
            Return
        End If

        RenderManuscripts()
        lblStatus.Text = outcome

    End Sub


    Private Sub ClearDeadlineFollowUp(item As DeadlineItem)

        Dim question As String =
            "Clear the follow-up date for " & If(String.IsNullOrWhiteSpace(item.JournalName), "this submission", item.JournalName) & "?" &
            Environment.NewLine & Environment.NewLine &
            "The submission stays as it is; only its follow-up date is removed."

        Dim confirmed As Boolean =
            If(deadlineConfirmPrompt IsNot Nothing,
               deadlineConfirmPrompt(question),
               MessageBox.Show(Me, question, "Clear Follow-up", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes)

        If confirmed Then
            ChangeDeadlineDate(item, Nothing, "Cleared the follow-up for " & If(String.IsNullOrWhiteSpace(item.JournalName), "the submission", item.JournalName) & ".")
        End If

    End Sub


    Private Sub CompleteDeadlineReminder(item As DeadlineItem)

        Dim manuscript As Manuscript = FindManuscript(item.ManuscriptId)
        If manuscript Is Nothing Then Return

        Dim reminder As ManuscriptReminder
        Try
            reminder = DeadlineService.Complete(manuscript, item, DateTime.Today)
        Catch ex As InvalidOperationException
            MessageBox.Show(Me, ex.Message, "Deadlines", MessageBoxButtons.OK, MessageBoxIcon.Information)
            FillDeadlines()
            Return
        End Try

        If Not SaveManuscripts() Then
            reminder.IsCompleted = False
            reminder.CompletedDate = Nothing
            FillDeadlines()
            Return
        End If

        RenderManuscripts()
        lblStatus.Text = "Marked """ & item.Title & """ done."

    End Sub


    Private Sub AddDeadlineReminder(sender As Object, e As EventArgs)

        If manuscripts.Count = 0 Then
            MessageBox.Show(Me, "Add a manuscript before creating a reminder.", "No Manuscripts", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using dialog As New ReminderEditForm(manuscripts)
            If dialog.ShowDialog(Me) <> DialogResult.OK OrElse dialog.Result Is Nothing Then Return

            Dim manuscript As Manuscript = FindManuscript(dialog.SelectedManuscriptId)
            If manuscript Is Nothing Then Return
            If manuscript.Reminders Is Nothing Then manuscript.Reminders = New List(Of ManuscriptReminder)()

            manuscript.Reminders.Add(dialog.Result)
            If Not SaveManuscripts() Then
                manuscript.Reminders.Remove(dialog.Result)
                Return
            End If
        End Using

        RenderManuscripts()

    End Sub


    Private Sub EditDeadlineReminder(item As DeadlineItem)

        Dim manuscript As Manuscript = FindManuscript(item.ManuscriptId)
        If manuscript Is Nothing OrElse manuscript.Reminders Is Nothing OrElse Not item.ReminderId.HasValue Then Return

        Dim index As Integer = manuscript.Reminders.FindIndex(Function(reminder) reminder IsNot Nothing AndAlso reminder.Id = item.ReminderId.Value)
        If index < 0 Then Return

        Dim existing As ManuscriptReminder = manuscript.Reminders(index)

        Using dialog As New ReminderEditForm(manuscripts, manuscript, existing)
            If dialog.ShowDialog(Me) <> DialogResult.OK OrElse dialog.Result Is Nothing Then Return

            manuscript.Reminders(index) = dialog.Result
            If Not SaveManuscripts() Then
                manuscript.Reminders(index) = existing
                Return
            End If
        End Using

        RenderManuscripts()

    End Sub


    ' The dated items as an .ics file; undated work has no calendar date.
    Private Sub ExportDeadlineCalendar(sender As Object, e As EventArgs)

        Dim dated As List(Of ReminderOccurrence) = ReminderService.BuildOccurrences(manuscripts, DateTime.Today)

        If dated.Count = 0 Then
            MessageBox.Show(Me, "There are no dated items to export.", "No Calendar Events", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using picker As New SaveFileDialog With {
            .Title = "Export PaperRoute Deadlines",
            .Filter = "iCalendar file (*.ics)|*.ics",
            .DefaultExt = "ics",
            .AddExtension = True,
            .OverwritePrompt = True,
            .FileName = "PaperRoute-Deadlines.ics"
        }
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return

            Try
                File.WriteAllText(picker.FileName, IcsCalendarService.Export(dated))
                lblStatus.Text = "Exported " & dated.Count.ToString(CultureInfo.CurrentCulture) &
                                 If(dated.Count = 1, " dated item", " dated items") & " to " & Path.GetFileName(picker.FileName) & "."
            Catch ex As Exception
                MessageBox.Show(Me, "PaperRoute could not create the calendar file." & Environment.NewLine & Environment.NewLine & ex.Message,
                                "Calendar Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using

    End Sub

End Class
