Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddDecisionForm
        Inherits Form

        Private Class DecisionOption

            Public ReadOnly Property Label As String
            Public ReadOnly Property Value As EditorialDecision

            Public Sub New(label As String, value As EditorialDecision)
                Me.Label = label
                Me.Value = value
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function

        End Class


        Private ReadOnly _existingDecision As EditorialDecisionEvent

        Private ReadOnly cmbDecision As New ComboBox()
        Private ReadOnly dtpDecisionDate As New DateTimePicker()
        Private ReadOnly chkDeadline As New CheckBox()
        Private ReadOnly dtpDeadline As New DateTimePicker()
        Private ReadOnly txtNotes As New TextBox()

        Private _createdDecision As EditorialDecisionEvent


        Public ReadOnly Property CreatedDecision As EditorialDecisionEvent
            Get
                Return _createdDecision
            End Get
        End Property


        Public Sub New()

            _existingDecision = Nothing

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Public Sub New(existingDecision As EditorialDecisionEvent)

            _existingDecision = existingDecision

            BuildInterface()
            LoadExistingDecision()

        End Sub


        Private Sub BuildInterface()

            If _existingDecision Is Nothing Then
                Me.Text = "Record Editorial Decision"
            Else
                Me.Text = "Edit Editorial Decision"
            End If

            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(650, 500)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 6,
                .Padding = New Padding(22)
            }

            root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))

            cmbDecision.Dock = DockStyle.Fill
            cmbDecision.DropDownStyle = ComboBoxStyle.DropDownList

            AddDecisionOption("Rejected", EditorialDecision.Rejected)
            AddDecisionOption("Desk Rejected", EditorialDecision.DeskRejected)
            AddDecisionOption("Rejected After Review", EditorialDecision.RejectedAfterReview)
            AddDecisionOption("Major Revision", EditorialDecision.MajorRevision)
            AddDecisionOption("Minor Revision", EditorialDecision.MinorRevision)
            AddDecisionOption("Revise and Resubmit", EditorialDecision.ReviseAndResubmit)
            AddDecisionOption("Accepted", EditorialDecision.Accepted)
            AddDecisionOption("Withdrawn", EditorialDecision.Withdrawn)

            cmbDecision.SelectedIndex = 0

            root.Controls.Add(CreateFieldLabel("Decision"), 0, 0)
            root.Controls.Add(cmbDecision, 1, 0)

            dtpDecisionDate.Format = DateTimePickerFormat.Short
            dtpDecisionDate.Value = DateTime.Today
            dtpDecisionDate.Width = 180

            root.Controls.Add(CreateFieldLabel("Decision date"), 0, 1)
            root.Controls.Add(dtpDecisionDate, 1, 1)

            Dim deadlinePanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False
            }

            chkDeadline.Text = "Set deadline"
            chkDeadline.AutoSize = True

            dtpDeadline.Format = DateTimePickerFormat.Short
            dtpDeadline.Value = DateTime.Today.AddDays(30)
            dtpDeadline.Width = 180
            dtpDeadline.Enabled = False

            AddHandler chkDeadline.CheckedChanged,
                AddressOf DeadlineCheckedChanged

            deadlinePanel.Controls.Add(chkDeadline)
            deadlinePanel.Controls.Add(dtpDeadline)

            root.Controls.Add(CreateFieldLabel("Revision deadline"), 0, 2)
            root.Controls.Add(deadlinePanel, 1, 2)

            Dim lblNotes As New Label With {
                .Text = "Decision / Editor / Reviewer Notes",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

            root.Controls.Add(lblNotes, 0, 3)
            root.SetColumnSpan(lblNotes, 2)

            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            root.Controls.Add(txtNotes, 0, 4)
            root.SetColumnSpan(txtNotes, 2)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnSave As New Button With {
                .AutoSize = True,
                .Height = 36
            }

            If _existingDecision Is Nothing Then
                btnSave.Text = "Add Decision"
            Else
                btnSave.Text = "Save Changes"
            End If

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveDecision

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(buttons, 0, 5)
            root.SetColumnSpan(buttons, 2)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            Me.Controls.Add(root)

        End Sub


        Private Sub AddDecisionOption(
            label As String,
            value As EditorialDecision
        )

            cmbDecision.Items.Add(
                New DecisionOption(label, value)
            )

        End Sub


        Private Sub LoadExistingDecision()

            For i As Integer = 0 To cmbDecision.Items.Count - 1

                Dim optionItem As DecisionOption =
                    DirectCast(cmbDecision.Items(i), DecisionOption)

                If optionItem.Value = _existingDecision.Decision Then
                    cmbDecision.SelectedIndex = i
                    Exit For
                End If

            Next

            dtpDecisionDate.Value =
                _existingDecision.DecisionDate

            If _existingDecision.RevisionDeadline.HasValue Then

                chkDeadline.Checked = True

                dtpDeadline.Value =
                    _existingDecision.RevisionDeadline.Value

            End If

            txtNotes.Text =
                _existingDecision.Notes

        End Sub


        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function


        Private Sub DeadlineCheckedChanged(
            sender As Object,
            e As EventArgs
        )

            dtpDeadline.Enabled =
                chkDeadline.Checked

        End Sub


        Private Sub SaveDecision(
            sender As Object,
            e As EventArgs
        )

            If cmbDecision.SelectedItem Is Nothing Then
                Return
            End If

            Dim selectedOption As DecisionOption =
                DirectCast(
                    cmbDecision.SelectedItem,
                    DecisionOption
                )

            Dim deadline As DateTime? = Nothing

            If chkDeadline.Checked Then
                deadline = dtpDeadline.Value.Date
            End If

            Dim decisionId As Guid

            If _existingDecision Is Nothing Then
                decisionId = Guid.NewGuid()
            Else
                decisionId = _existingDecision.Id
            End If

            _createdDecision =
                New EditorialDecisionEvent With {
                    .Id = decisionId,
                    .RecordedAtUtc =
                        If(
                            _existingDecision Is Nothing,
                            Nothing,
                            _existingDecision.RecordedAtUtc
                        ),
                    .LastModifiedAtUtc =
                        If(
                            _existingDecision Is Nothing,
                            Nothing,
                            _existingDecision.LastModifiedAtUtc
                        ),
                    .DecisionDate = dtpDecisionDate.Value.Date,
                    .Decision = selectedOption.Value,
                    .RevisionDeadline = deadline,
                    .Notes = txtNotes.Text.Trim()
                }

            If _existingDecision Is Nothing Then

                ChronologyProvenanceService.StampCreated(
                    _createdDecision
                )

            Else

                ChronologyProvenanceService.StampModified(
                    _createdDecision
                )

            End If

            Me.DialogResult = DialogResult.OK

        End Sub

    End Class

End Namespace