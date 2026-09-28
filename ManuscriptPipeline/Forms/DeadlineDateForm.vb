Imports System
Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Postpone or set a deadline: a few near dates or any date. The caller
    ' says which record the date belongs to.
    Friend Class DeadlineDateForm
        Inherits Form

        Private ReadOnly _today As DateTime
        Private ReadOnly _choices As New List(Of (Button As RadioButton, Days As Integer))()
        Private ReadOnly rdoPick As New RadioButton()
        Private ReadOnly dtpDate As New DateTimePicker()
        Private ReadOnly btnApply As New ActionButton()
        Private ReadOnly _verb As String

        Public Sub New(heading As String, context As String, note As String, verb As String, current As DateTime?, today As DateTime)

            _today = today.Date
            _verb = verb

            Text = heading
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            StartPosition = FormStartPosition.CenterParent
            AutoSize = True
            AutoSizeMode = AutoSizeMode.GrowAndShrink
            AutoScaleMode = AutoScaleMode.Dpi
            Font = New Font("Segoe UI", 9.0F)

            Dim root As New TableLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .Padding = New Padding(18, 16, 18, 14)
            }

            Dim lblHeading As New Label With {
                .Text = heading,
                .AutoSize = True,
                .UseMnemonic = False,
                .Font = New Font(Font, FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 2)
            }
            Dim lblContext As New Label With {
                .Text = context,
                .AutoSize = True,
                .UseMnemonic = False,
                .MaximumSize = New Size(380, 0),
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 0, 0, 12)
            }
            root.Controls.Add(lblHeading)
            root.Controls.Add(lblContext)

            Dim choices As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .Margin = New Padding(0, 0, 0, 8),
                .AccessibleName = "New date",
                .AccessibleRole = AccessibleRole.Grouping
            }

            For Each choice In {(1, "Tomorrow"), (7, "In 1 week"), (14, "In 2 weeks")}
                Dim radio As New RadioButton With {
                    .Text = choice.Item2 & "  ·  " & _today.AddDays(choice.Item1).ToString("ddd, MMM d", CultureInfo.CurrentCulture),
                    .AutoSize = True,
                    .UseMnemonic = False,
                    .Margin = New Padding(0, 0, 0, 4)
                }
                AddHandler radio.CheckedChanged, AddressOf ChoiceChanged
                _choices.Add((radio, choice.Item1))
                choices.Controls.Add(radio)
            Next

            Dim pickRow As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0)}
            rdoPick.Text = "Pick a date"
            rdoPick.AutoSize = True
            rdoPick.Margin = New Padding(0, 4, 8, 0)
            AddHandler rdoPick.CheckedChanged, AddressOf ChoiceChanged
            dtpDate.Format = DateTimePickerFormat.Long
            dtpDate.Width = 220
            dtpDate.AccessibleName = "Pick a date"
            dtpDate.Value = If(current.HasValue AndAlso current.Value.Date > _today, current.Value.Date, _today.AddDays(7))
            AddHandler dtpDate.ValueChanged, Sub(sender, e)
                                                 rdoPick.Checked = True
                                                 UpdateApply()
                                             End Sub
            pickRow.Controls.Add(rdoPick)
            pickRow.Controls.Add(dtpDate)
            choices.Controls.Add(pickRow)
            root.Controls.Add(choices)

            Dim lblNote As New Label With {
                .Text = note,
                .AutoSize = True,
                .UseMnemonic = False,
                .MaximumSize = New Size(380, 0),
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 4, 0, 12)
            }
            root.Controls.Add(lblNote)

            Dim buttons As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0)
            }
            btnApply.Role = ActionButtonRole.Primary
            btnApply.Height = 34
            btnApply.DialogResult = DialogResult.OK
            Dim btnCancel As New ActionButton With {.Text = "Cancel", .Height = 34, .Width = 90, .DialogResult = DialogResult.Cancel, .Margin = New Padding(0, 0, 8, 0)}
            buttons.Controls.Add(btnApply)
            buttons.Controls.Add(btnCancel)
            root.Controls.Add(buttons)

            Controls.Add(root)
            AcceptButton = btnApply
            CancelButton = btnCancel

            _choices(If(current.HasValue AndAlso current.Value.Date >= _today, 1, 0)).Button.Checked = True
            UiPolish.ApplyDialog(Me)

        End Sub

        Public ReadOnly Property SelectedDate As DateTime
            Get
                If rdoPick.Checked Then Return dtpDate.Value.Date
                For Each choice In _choices
                    If choice.Button.Checked Then Return _today.AddDays(choice.Days)
                Next
                Return _today.AddDays(7)
            End Get
        End Property

        Private Sub ChoiceChanged(sender As Object, e As EventArgs)
            UpdateApply()
        End Sub

        ' The button says exactly what will happen: "Postpone to Oct 4".
        Private Sub UpdateApply()
            btnApply.Text = _verb & " " & SelectedDate.ToString("MMM d", CultureInfo.CurrentCulture)
            btnApply.Width = TextRenderer.MeasureText(btnApply.Text, Font).Width + 36
        End Sub

    End Class

End Namespace
