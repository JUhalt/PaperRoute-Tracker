Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddManuscriptForm
        Inherits Form

        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtTargetJournal As New TextBox()
        Private ReadOnly cmbStage As New ComboBox()

        Private _createdManuscript As Manuscript

        Public ReadOnly Property CreatedManuscript As Manuscript
            Get
                Return _createdManuscript
            End Get
        End Property

        Public Sub New()
            BuildInterface()
            UiPolish.ApplyDialog(Me)
        End Sub

        Private Sub BuildInterface()

            Me.Text = "Add Manuscript"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(590, 300)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 5,
                .Padding = New Padding(20)
            }

            layout.ColumnStyles.Add(
                New ColumnStyle(SizeType.Absolute, 145)
            )

            layout.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 100)
            )

            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))

            txtTitle.Dock = DockStyle.Fill
            txtTargetJournal.Dock = DockStyle.Fill

            cmbStage.Dock = DockStyle.Fill
            cmbStage.DropDownStyle = ComboBoxStyle.DropDownList

            cmbStage.Items.Add(
                PaperStage.Idea
            )

            cmbStage.Items.Add(
                PaperStage.Draft
            )

            cmbStage.SelectedItem =
                PaperStage.Draft

            layout.Controls.Add(
                CreateFieldLabel("Title"),
                0,
                0
            )

            layout.Controls.Add(txtTitle, 1, 0)

            layout.Controls.Add(
                CreateFieldLabel("Target journal"),
                0,
                1
            )

            layout.Controls.Add(txtTargetJournal, 1, 1)

            layout.Controls.Add(
                CreateFieldLabel("Starting stage"),
                0,
                2
            )

            layout.Controls.Add(cmbStage, 1, 2)

            Dim lblAuthorsNote As New Label With {
                .Text =
                    "New manuscripts begin as an Idea or Draft. Add a Journal Submission to enter the submission workflow; " &
                    "editorial decisions then drive Revision, Accepted, and rejection outcomes. " &
                    "Authors are managed as structured records after the manuscript is created.",
                .AutoSize = True,
                .MaximumSize = New Size(520, 0),
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 8, 0, 0)
            }

            layout.Controls.Add(
                lblAuthorsNote,
                0,
                3
            )

            layout.SetColumnSpan(
                lblAuthorsNote,
                2
            )

            Dim btnAdd As New Button With {
                .Text = "Add Manuscript",
                .AutoSize = True,
                .Height = 34
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 34,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnAdd.Click, AddressOf AddManuscript

            Dim buttonPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            buttonPanel.Controls.Add(btnAdd)
            buttonPanel.Controls.Add(btnCancel)

            layout.Controls.Add(buttonPanel, 0, 4)
            layout.SetColumnSpan(buttonPanel, 2)

            Me.AcceptButton = btnAdd
            Me.CancelButton = btnCancel

            Me.Controls.Add(layout)

        End Sub

        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function

        Private Sub AddManuscript(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(txtTitle.Text) Then

                MessageBox.Show(
                    Me,
                    "Please enter a manuscript title.",
                    "Title Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()
                Return

            End If

            Dim selectedStage As PaperStage =
                CType(cmbStage.SelectedItem, PaperStage)

            Dim createdAt As DateTime =
                DateTime.Now

            _createdManuscript = New Manuscript With {
                .Title = txtTitle.Text.Trim(),
                .TargetJournal = txtTargetJournal.Text.Trim(),
                .CurrentStage = selectedStage,
                .Location = ManuscriptLocation.Pipeline,
                .StageEnteredDate = createdAt
            }

            Dim initialHistory As New HistoryEvent With {
                .EventDate = createdAt,
                .Stage = selectedStage,
                .Note = "Manuscript added to PaperRoute."
            }

            ChronologyProvenanceService.StampCreated(
                initialHistory
            )

            _createdManuscript.History.Add(
                initialHistory
            )

            Me.DialogResult = DialogResult.OK

        End Sub

    End Class

End Namespace
