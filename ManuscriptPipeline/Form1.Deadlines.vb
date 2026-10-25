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

' The Deadlines page (#28): what needs action, and when. The page is built
' once and kept (#133); its rows are rebuilt from the library on each visit
' and each change, and drawn by one list. Every action changes the record
' that owns the row and saves, like any other edit.
Partial Public Class Form1

    Private deadlinesPage As Control = Nothing
    Private deadlinesList As DeadlineList = Nothing
    Private deadlinesFilter As TextBox = Nothing
    Private deadlinesSubtitle As Label = Nothing
    Private deadlineGlance As DeadlineGlance = Nothing
    Private deadlinesPageDate As DateTime = DateTime.MinValue
    Private deadlinesClearingFilter As Boolean = False
    Private deadlinesPublicationsShown As Boolean? = Nothing
    Private deadlinesKind As DeadlineKind? = Nothing
    Private deadlinesShowDone As Boolean = False
    Private ReadOnly deadlineChips As New Dictionary(Of String, FilterChip)()

    ' Tests answer the date and confirmation prompts.
    Friend deadlineDatePrompt As Func(Of DeadlineItem, DateTime?) = Nothing
    Friend deadlineConfirmPrompt As Func(Of String, Boolean) = Nothing

    Private Shared ReadOnly OpenGroups As DeadlineGroup() = {
        DeadlineGroup.Overdue, DeadlineGroup.Today, DeadlineGroup.Next7Days, DeadlineGroup.Later, DeadlineGroup.NoDate
    }


    ' The page for a visit: built on the first, and on each later one shown
    ' as a new page was, with no filter text and the list at its top. The
    ' kind chosen with a chip and whether Done is shown carry over, as they
    ' did when the page was built anew.
    Private Function OpenDeadlinesPage() As Control

        If deadlinesPage Is Nothing OrElse deadlinesPage.IsDisposed Then
            deadlinesPage = BuildDeadlinesPage()
            Return deadlinesPage
        End If

        If deadlinesFilter.TextLength > 0 Then
            deadlinesClearingFilter = True
            Try
                deadlinesFilter.Text = String.Empty
            Finally
                deadlinesClearingFilter = False
            End Try
        End If

        deadlinesList.ResetView()
        FillDeadlines()
        Return deadlinesPage

    End Function


    Private Shared Function DeadlinesDescription(today As DateTime) As String
        Return today.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture) &
               ". Revision deadlines, journal follow-ups, your reminders, unfinished preparation, and possible publications."
    End Function


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
        AddHandler deadlinesFilter.TextChanged,
            Sub(sender, e)
                If Not deadlinesClearingFilter Then FillDeadlines()
            End Sub

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
            DeadlinesDescription(DateTime.Today),
            CreateSearchField(deadlinesFilter), btnExport, btnAdd)
        frame.SuspendLayout()

        ' The subtitle carries the date, so a page kept past midnight puts
        ' the new one there when it is next filled.
        deadlinesSubtitle = DirectCast(frame.GetControlFromPosition(0, 1), Label)
        deadlinesPageDate = DateTime.Today

        ' The counts, the chips and the list take the frame's last three
        ' rows, so the page needs no table of its own for them.
        frame.RowStyles(2).SizeType = SizeType.AutoSize
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        frame.RowCount = 5

        ' Four counts across the top; they ignore the filters.
        deadlineGlance = New DeadlineGlance With {
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, dpi)),
            .AccessibleName = "At a glance"
        }
        frame.Controls.Add(deadlineGlance, 0, 2)
        frame.Controls.Add(BuildDeadlineChips(), 0, 3)

        deadlinesList = New DeadlineList With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .HeadingFont = attentionTitleFont,
            .AccessibleName = "Deadlines"
        }
        AddHandler deadlinesList.DoneToggleClicked,
            Sub(sender, e)
                deadlinesShowDone = Not deadlinesShowDone
                FillDeadlines()
            End Sub
        frame.Controls.Add(deadlinesList, 0, 4)

        deadlinesPublicationsShown = Nothing
        FillDeadlines()
        frame.ResumeLayout(False)
        Return frame

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
            ("Preparation", CType(DeadlineKind.Preparation, DeadlineKind?)),
            ("Publications", CType(DeadlineKind.Publication, DeadlineKind?))
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


    ' Refills the page from the library while it is the one shown; the list
    ' keeps the keyboard on the same row (#133).
    Private Sub FillDeadlines()

        If deadlinesList Is Nothing OrElse deadlinesList.IsDisposed OrElse currentPage <> WorkspacePage.Deadlines Then
            Return
        End If

        Dim today As DateTime = DateTime.Today
        If today <> deadlinesPageDate Then
            deadlinesPageDate = today
            If deadlinesSubtitle IsNot Nothing Then deadlinesSubtitle.Text = DeadlinesDescription(today)
        End If

        Dim all As List(Of DeadlineItem) = DeadlineService.Build(manuscripts, today)
        Dim open As List(Of DeadlineItem) = all.Where(Function(item) item.Group <> DeadlineGroup.Done).ToList()

        deadlineGlance.SetCounts(
            open.Where(Function(item) item.Group = DeadlineGroup.Overdue).Count(),
            open.Where(Function(item) item.Group = DeadlineGroup.Today).Count(),
            open.Where(Function(item) item.Group = DeadlineGroup.Next7Days).Count(),
            open.Where(Function(item) item.Group = DeadlineGroup.NoDate).Count())

        ' Each chip changes only what differs, so an unchanged row of chips
        ' is not laid out again.
        Dim chipsRow As Control = deadlineChips.Values.First().Parent
        chipsRow.SuspendLayout()
        For Each chip In deadlineChips
            Dim kind As DeadlineKind? = ChipKind(chip.Key)
            Dim count As Integer = open.Where(Function(item) Not kind.HasValue OrElse item.Kind = kind.Value).Count()
            Dim text As String = chip.Key & "  " & count.ToString(CultureInfo.CurrentCulture)
            If chip.Value.Text <> text Then chip.Value.Text = text
            Dim tone As FilterChipTone = If(Nullable.Equals(deadlinesKind, kind), FilterChipTone.Active, FilterChipTone.Neutral)
            If chip.Value.Tone <> tone Then chip.Value.Tone = tone
            ' Possible publications exist only after a publication check.
            If kind = DeadlineKind.Publication Then
                Dim visible As Boolean = count > 0 OrElse Nullable.Equals(deadlinesKind, kind)
                If Not Nullable.Equals(deadlinesPublicationsShown, visible) Then
                    chip.Value.Visible = visible
                    deadlinesPublicationsShown = visible
                End If
            End If
        Next
        chipsRow.ResumeLayout(True)

        Dim filter As String = If(deadlinesFilter?.Text, String.Empty).Trim()
        Dim shown As List(Of DeadlineItem) = all.Where(
            Function(item) (Not deadlinesKind.HasValue OrElse item.Kind = deadlinesKind.Value) AndAlso
                           (filter.Length = 0 OrElse
                            {item.ManuscriptTitle, item.JournalName, item.Title}.Any(
                                Function(value) If(value, String.Empty).IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0))).ToList()

        Dim entries As New List(Of DeadlineEntry)()

        For Each group As DeadlineGroup In OpenGroups
            Dim items As List(Of DeadlineItem) = shown.Where(Function(item) item.Group = group).ToList()
            If items.Count = 0 Then Continue For
            entries.Add(DeadlineEntry.Heading(GroupHeading(group) & "  " & items.Count.ToString(CultureInfo.CurrentCulture), group))
            For Each item As DeadlineItem In items
                entries.Add(DeadlineEntry.ForRow(CreateDeadlineRow(item, today)))
            Next
        Next

        If Not shown.Any(Function(item) item.Group <> DeadlineGroup.Done) Then
            entries.Add(DeadlineEntry.Message(
                If(open.Count = 0,
                   "Nothing needs action right now. Revision deadlines, journal follow-ups, your reminders, and unfinished submission preparation appear here.",
                   "Nothing matches this filter.")))
        End If

        ' Done stays collapsed behind Show.
        Dim done As List(Of DeadlineItem) = shown.Where(Function(item) item.Group = DeadlineGroup.Done).ToList()
        If done.Count > 0 Then
            Dim link As String = If(deadlinesShowDone, "Hide", "Show")
            entries.Add(DeadlineEntry.DoneToggle(
                "Done in the last " & DeadlineService.DoneDays.ToString(CultureInfo.CurrentCulture) & " days: " &
                    done.Count.ToString(CultureInfo.CurrentCulture) & "  ·  " & link,
                link,
                If(deadlinesShowDone, "Hide done items", "Show done items")))
            If deadlinesShowDone Then
                For Each item As DeadlineItem In done
                    entries.Add(DeadlineEntry.ForRow(CreateDeadlineRow(item, today)))
                Next
            End If
        End If

        deadlinesList.SetEntries(entries)

    End Sub


    Private Shared Function ChipKind(name As String) As DeadlineKind?
        Select Case name
            Case "Revisions" : Return DeadlineKind.Revision
            Case "Follow-ups" : Return DeadlineKind.FollowUp
            Case "Reminders" : Return DeadlineKind.Reminder
            Case "Preparation" : Return DeadlineKind.Preparation
            Case "Publications" : Return DeadlineKind.Publication
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

            Case item.Kind = DeadlineKind.Publication
                row.AddAction("Review Match", Sub() ReviewDeadlineMatch(item))
                row.AddAction("Mark Published...", Sub() MarkDeadlinePublished(item))
                row.AddAction("Ignore Match", Sub() IgnoreDeadlineMatch(item), menuOnly:=True)
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

        ' The row the keyboard is on chooses the manuscript (#229), as Edit
        ' does; with no row chosen the window starts on its first.
        Dim focusedRow As DeadlineRow = deadlinesList?.FocusedRow
        Dim chosen As Manuscript = If(focusedRow Is Nothing, Nothing, FindManuscript(focusedRow.Item.ManuscriptId))

        Using dialog As New ReminderEditForm(manuscripts, chosen)
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
