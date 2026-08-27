Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class JournalChecklistItemEditForm
        Inherits Form

        Private ReadOnly _source As JournalChecklistTemplateItem

        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtCategory As New TextBox()
        Private ReadOnly txtDescription As New TextBox()
        Private ReadOnly chkRequired As New CheckBox()

        Private _result As JournalChecklistTemplateItem


        Public ReadOnly Property Result As JournalChecklistTemplateItem
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            source As JournalChecklistTemplateItem
        )

            _source = source

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadSource()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _source Is Nothing,
                    "Add Readiness Requirement",
                    "Edit Readiness Requirement"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    660,
                    520
                )

            Me.MinimumSize =
                New Size(
                    560,
                    440
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
                .ColumnCount = 2,
                .RowCount = 5,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    155
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            txtTitle.Dock =
                DockStyle.Fill

            txtCategory.Dock =
                DockStyle.Fill

            txtDescription.Dock =
                DockStyle.Fill

            txtDescription.Multiline =
                True

            txtDescription.ScrollBars =
                ScrollBars.Vertical

            chkRequired.Text =
                "Required for readiness"

            chkRequired.AutoSize =
                True

            chkRequired.Checked =
                True

            root.Controls.Add(CreateLabel("Requirement"), 0, 0)
            root.Controls.Add(txtTitle, 1, 0)

            root.Controls.Add(CreateLabel("Category"), 0, 1)
            root.Controls.Add(txtCategory, 1, 1)

            root.Controls.Add(CreateLabel("Instructions"), 0, 2)
            root.Controls.Add(txtDescription, 1, 2)

            root.Controls.Add(CreateLabel("Importance"), 0, 3)
            root.Controls.Add(chkRequired, 1, 3)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveItem

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(buttons, 0, 4)
            root.SetColumnSpan(buttons, 2)

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(root)

        End Sub


        Private Function CreateLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(
                    Me.Font,
                    FontStyle.Bold
                ),
                .Margin = New Padding(
                    3,
                    9,
                    3,
                    9
                )
            }

        End Function


        Private Sub LoadSource()

            If _source Is Nothing Then
                Return
            End If

            txtTitle.Text =
                _source.Title

            txtCategory.Text =
                _source.Category

            txtDescription.Text =
                _source.Description

            chkRequired.Checked =
                _source.IsRequired

        End Sub


        Private Sub SaveItem(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtTitle.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a short requirement title.",
                    "Requirement Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()
                Return

            End If

            _result =
                New JournalChecklistTemplateItem With {
                    .Id =
                        If(
                            _source Is Nothing,
                            Guid.NewGuid(),
                            _source.Id
                        ),
                    .Title = txtTitle.Text.Trim(),
                    .Description = txtDescription.Text.Trim(),
                    .Category = txtCategory.Text.Trim(),
                    .SortOrder =
                        If(
                            _source Is Nothing,
                            0,
                            _source.SortOrder
                        ),
                    .IsRequired = chkRequired.Checked
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
