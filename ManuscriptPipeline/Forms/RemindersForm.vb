Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class RemindersForm
        Inherits Form

        Private ReadOnly _manuscripts As List(Of Manuscript)
        Private ReadOnly _repository As ManuscriptRepository

        Private ReadOnly cboFilter As New ComboBox()
        Private ReadOnly lblSummary As New Label()
        Private ReadOnly grid As New DataGridView()

        Private _occurrences As New List(Of ReminderOccurrence)()


        Public Sub New(
            manuscripts As List(Of Manuscript),
            repository As ManuscriptRepository
        )

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If repository Is Nothing Then
                Throw New ArgumentNullException(NameOf(repository))
            End If

            _manuscripts =
                manuscripts

            _repository =
                repository

            BuildInterface()
            EmptyHint.Attach(grid, "No reminders to show. Revision deadlines and follow-up dates appear here automatically; use Add Reminder... for your own.")
            UiPolish.ApplyDialog(Me)
            RefreshGrid()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Reminders & Calendar"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    1080,
                    690
                )

            Me.MinimumSize =
                New Size(
                    820,
                    520
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .Text =
                    "PaperRoute combines revision deadlines, journal follow-up dates, and your own custom reminders. " &
                    "The list is local; calendar export creates a portable .ics file.",
                .AutoSize = True,
                .MaximumSize = New Size(1000, 0),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim filterBar As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblFilter As New Label With {
                .Text = "Show",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    ),
                .Margin = New Padding(0, 7, 8, 0)
            }

            cboFilter.DropDownStyle =
                ComboBoxStyle.DropDownList

            cboFilter.Width =
                210

            cboFilter.Items.Add(
                "All active reminders"
            )

            cboFilter.Items.Add(
                "Overdue / due today"
            )

            cboFilter.Items.Add(
                "Upcoming"
            )

            cboFilter.SelectedIndex =
                0

            AddHandler cboFilter.SelectedIndexChanged,
                Sub(sender, e)
                    RefreshGrid()
                End Sub

            lblSummary.AutoSize =
                True

            lblSummary.ForeColor =
                SystemColors.GrayText

            lblSummary.Margin =
                New Padding(18, 7, 0, 0)

            filterBar.Controls.Add(
                lblFilter
            )

            filterBar.Controls.Add(
                cboFilter
            )

            filterBar.Controls.Add(
                lblSummary
            )

            ConfigureGrid()

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(0, 10, 0, 0)
            }

            footer.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            footer.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            Dim help As New Label With {
                .Text =
                    "Revision deadlines come from editorial decisions. Journal follow-ups come from submission records. " &
                    "Custom reminders can be edited or completed here.",
                .AutoSize = True,
                .MaximumSize = New Size(1000, 0),
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim buttonBar As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True
            }

            Dim btnAdd As New Button With {
                .Text = "Add Reminder...",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnEdit As New Button With {
                .Text = "Edit Custom...",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnComplete As New Button With {
                .Text = "Complete Custom",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnExport As New Button With {
                .Text = "Export Calendar (.ics)...",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnRefresh As New Button With {
                .Text = "Refresh",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            AddHandler btnAdd.Click,
                AddressOf AddReminder

            AddHandler btnEdit.Click,
                AddressOf EditReminder

            AddHandler btnComplete.Click,
                AddressOf CompleteReminder

            AddHandler btnExport.Click,
                AddressOf ExportCalendar

            AddHandler btnRefresh.Click,
                Sub(sender, e)
                    RefreshGrid()
                End Sub

            buttonBar.Controls.Add(btnAdd)
            buttonBar.Controls.Add(btnEdit)
            buttonBar.Controls.Add(btnComplete)
            buttonBar.Controls.Add(btnExport)
            buttonBar.Controls.Add(btnRefresh)
            buttonBar.Controls.Add(btnClose)

            footer.Controls.Add(
                help,
                0,
                0
            )

            footer.Controls.Add(
                buttonBar,
                0,
                1
            )

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(filterBar, 0, 1)
            root.Controls.Add(grid, 0, 2)
            root.Controls.Add(footer, 0, 3)

            Me.CancelButton =
                btnClose

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub ConfigureGrid()

            grid.Dock =
                DockStyle.Fill

            grid.ReadOnly =
                True

            grid.AllowUserToAddRows =
                False

            grid.AllowUserToDeleteRows =
                False

            grid.AllowUserToResizeRows =
                False

            grid.AutoGenerateColumns =
                False

            grid.MultiSelect =
                False

            grid.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect

            grid.RowHeadersVisible =
                False

            grid.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.None

            grid.Columns.Add(
                CreateTextColumn(
                    "Due",
                    110
                )
            )

            grid.Columns.Add(
                CreateTextColumn(
                    "Status",
                    105
                )
            )

            grid.Columns.Add(
                CreateTextColumn(
                    "Type",
                    100
                )
            )

            grid.Columns.Add(
                CreateTextColumn(
                    "Manuscript",
                    260
                )
            )

            grid.Columns.Add(
                CreateTextColumn(
                    "Reminder",
                    270
                )
            )

            grid.Columns.Add(
                CreateTextColumn(
                    "Journal",
                    170
                )
            )

            AddHandler grid.CellDoubleClick,
                Sub(sender, e)

                    If e.RowIndex < 0 Then
                        Return
                    End If

                    EditReminder(
                        sender,
                        EventArgs.Empty
                    )

                End Sub

        End Sub


        Private Function CreateTextColumn(
            header As String,
            width As Integer
        ) As DataGridViewTextBoxColumn

            Return New DataGridViewTextBoxColumn With {
                .HeaderText = header,
                .Width = width,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }

        End Function


        Private Sub RefreshGrid()

            _occurrences =
                ReminderService.BuildOccurrences(
                    _manuscripts,
                    DateTime.Today
                )

            Dim filtered As IEnumerable(Of ReminderOccurrence) =
                _occurrences

            Select Case cboFilter.SelectedIndex

                Case 1

                    filtered =
                        filtered.Where(
                            Function(item)
                                Return item.Status =
                                    ReminderStatus.Overdue OrElse
                                    item.Status =
                                    ReminderStatus.DueToday
                            End Function
                        )

                Case 2

                    filtered =
                        filtered.Where(
                            Function(item)
                                Return item.Status =
                                    ReminderStatus.Upcoming
                            End Function
                        )

            End Select

            Dim visible As List(Of ReminderOccurrence) =
                filtered.ToList()

            grid.Rows.Clear()

            For Each item As ReminderOccurrence In visible

                Dim rowIndex As Integer =
                    grid.Rows.Add(
                        item.DueDate.ToString(
                            "yyyy-MM-dd"
                        ),
                        item.StatusLabel,
                        item.KindLabel,
                        item.ManuscriptTitle,
                        item.Title,
                        item.JournalName
                    )

                grid.Rows(rowIndex).Tag =
                    item

            Next

            Dim overdueCount As Integer =
                _occurrences.
                    Where(
                        Function(item)
                            Return item.Status =
                                ReminderStatus.Overdue
                        End Function
                    ).
                    Count()

            Dim dueTodayCount As Integer =
                _occurrences.
                    Where(
                        Function(item)
                            Return item.Status =
                                ReminderStatus.DueToday
                        End Function
                    ).
                    Count()

            lblSummary.Text =
                visible.Count.ToString() &
                " shown • " &
                overdueCount.ToString() &
                " overdue • " &
                dueTodayCount.ToString() &
                " due today"

        End Sub


        Private Function SelectedOccurrence() As ReminderOccurrence

            If grid.SelectedRows.Count = 0 Then
                Return Nothing
            End If

            Return TryCast(
                grid.SelectedRows(0).Tag,
                ReminderOccurrence
            )

        End Function


        Private Sub AddReminder(
            sender As Object,
            e As EventArgs
        )

            If _manuscripts.Count = 0 Then

                MessageBox.Show(
                    Me,
                    "Add a manuscript before creating a reminder.",
                    "No Manuscripts",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using dialog As New ReminderEditForm(
                _manuscripts
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim manuscript As Manuscript =
                    _manuscripts.
                        FirstOrDefault(
                            Function(item)
                                Return item.Id =
                                    dialog.SelectedManuscriptId
                            End Function
                        )

                If manuscript Is Nothing Then
                    Return
                End If

                If manuscript.Reminders Is Nothing Then

                    manuscript.Reminders =
                        New List(Of ManuscriptReminder)()

                End If

                manuscript.Reminders.Add(
                    dialog.Result
                )

                Try

                    _repository.Save(
                        _manuscripts
                    )

                Catch ex As Exception

                    manuscript.Reminders.Remove(
                        dialog.Result
                    )

                    ShowSaveError(
                        ex
                    )

                    Return

                End Try

                RefreshGrid()

            End Using

        End Sub


        Private Sub EditReminder(
            sender As Object,
            e As EventArgs
        )

            Dim occurrence As ReminderOccurrence =
                SelectedOccurrence()

            If occurrence Is Nothing Then
                Return
            End If

            If occurrence.Kind <>
               ReminderKind.Custom Then

                ShowSourceReminderHelp(
                    occurrence
                )

                Return

            End If

            Dim manuscript As Manuscript =
                FindManuscript(
                    occurrence.ManuscriptId
                )

            If manuscript Is Nothing OrElse
               manuscript.Reminders Is Nothing Then

                Return

            End If

            Dim existing As ManuscriptReminder =
                manuscript.Reminders.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.Id =
                                    occurrence.SourceId
                        End Function
                    )

            If existing Is Nothing Then
                Return
            End If

            Using dialog As New ReminderEditForm(
                _manuscripts,
                manuscript,
                existing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim index As Integer =
                    manuscript.Reminders.FindIndex(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.Id =
                                    existing.Id
                        End Function
                    )

                If index < 0 Then
                    Return
                End If

                manuscript.Reminders(index) =
                    dialog.Result

                Try

                    _repository.Save(
                        _manuscripts
                    )

                Catch ex As Exception

                    manuscript.Reminders(index) =
                        existing

                    ShowSaveError(
                        ex
                    )

                    Return

                End Try

                RefreshGrid()

            End Using

        End Sub


        Private Sub CompleteReminder(
            sender As Object,
            e As EventArgs
        )

            Dim occurrence As ReminderOccurrence =
                SelectedOccurrence()

            If occurrence Is Nothing Then
                Return
            End If

            If occurrence.Kind <>
               ReminderKind.Custom Then

                ShowSourceReminderHelp(
                    occurrence
                )

                Return

            End If

            Dim manuscript As Manuscript =
                FindManuscript(
                    occurrence.ManuscriptId
                )

            If manuscript Is Nothing OrElse
               manuscript.Reminders Is Nothing Then

                Return

            End If

            Dim reminder As ManuscriptReminder =
                manuscript.Reminders.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.Id =
                                    occurrence.SourceId
                        End Function
                    )

            If reminder Is Nothing Then
                Return
            End If

            Dim oldCompleted As Boolean =
                reminder.IsCompleted

            Dim oldCompletedDate As DateTime? =
                reminder.CompletedDate

            reminder.IsCompleted =
                True

            reminder.CompletedDate =
                DateTime.Now

            Try

                _repository.Save(
                    _manuscripts
                )

            Catch ex As Exception

                reminder.IsCompleted =
                    oldCompleted

                reminder.CompletedDate =
                    oldCompletedDate

                ShowSaveError(
                    ex
                )

                Return

            End Try

            RefreshGrid()

        End Sub


        Private Sub ExportCalendar(
            sender As Object,
            e As EventArgs
        )

            Dim allActive As List(Of ReminderOccurrence) =
                ReminderService.BuildOccurrences(
                    _manuscripts,
                    DateTime.Today
                )

            If allActive.Count = 0 Then

                MessageBox.Show(
                    Me,
                    "There are no active reminders to export.",
                    "No Calendar Events",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using picker As New SaveFileDialog With {
                .Title = "Export PaperRoute Reminder Calendar",
                .Filter = "iCalendar file (*.ics)|*.ics",
                .DefaultExt = "ics",
                .AddExtension = True,
                .OverwritePrompt = True,
                .FileName = "PaperRoute-Reminders.ics"
            }

                If picker.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                Try

                    File.WriteAllText(
                        picker.FileName,
                        IcsCalendarService.Export(
                            allActive
                        )
                    )

                    MessageBox.Show(
                        Me,
                        "Calendar exported successfully." &
                        Environment.NewLine &
                        Environment.NewLine &
                        "The .ics file can be imported into Outlook, Google Calendar, Apple Calendar, and other compatible calendar apps.",
                        "Calendar Export Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not create the calendar file." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "Calendar Export Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub


        Private Function FindManuscript(
            manuscriptId As Guid
        ) As Manuscript

            Return _manuscripts.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.Id =
                                manuscriptId
                    End Function
                )

        End Function


        Private Sub ShowSourceReminderHelp(
            occurrence As ReminderOccurrence
        )

            Dim message As String

            Select Case occurrence.Kind

                Case ReminderKind.RevisionDeadline

                    message =
                        "This reminder comes from the manuscript's recorded revision deadline." &
                        Environment.NewLine &
                        Environment.NewLine &
                        "Edit the relevant editorial decision or revision workflow to change the deadline."

                Case ReminderKind.SubmissionFollowUp

                    message =
                        "This reminder comes from the journal submission's follow-up date." &
                        Environment.NewLine &
                        Environment.NewLine &
                        "Edit that journal submission to change or remove the follow-up date."

                Case Else

                    message =
                        "This reminder is managed by its source record."

            End Select

            MessageBox.Show(
                Me,
                message,
                "Source Reminder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

        End Sub


        Private Sub ShowSaveError(
            ex As Exception
        )

            MessageBox.Show(
                Me,
                "PaperRoute could not save the reminder change." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Reminder Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

        End Sub

    End Class

End Namespace
