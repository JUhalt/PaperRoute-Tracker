Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AffiliationEditForm
        Inherits Form

        Private ReadOnly _source As AffiliationRecord

        Private ReadOnly txtInstitution As New TextBox()
        Private ReadOnly txtDepartment As New TextBox()
        Private ReadOnly txtCity As New TextBox()
        Private ReadOnly txtRegion As New TextBox()
        Private ReadOnly txtCountry As New TextBox()
        Private ReadOnly txtNotes As New TextBox()

        Private _result As AffiliationRecord


        Public ReadOnly Property Result As AffiliationRecord
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            source As AffiliationRecord
        )

            _source =
                source

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadSource()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _source Is Nothing,
                    "Add Affiliation",
                    "Edit Affiliation"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    680,
                    540
                )

            Me.MinimumSize =
                New Size(
                    600,
                    500
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
                .RowCount = 7,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    170
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For i As Integer = 0 To 4
                root.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )
            Next

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            txtInstitution.Dock = DockStyle.Fill
            txtDepartment.Dock = DockStyle.Fill
            txtCity.Dock = DockStyle.Fill
            txtRegion.Dock = DockStyle.Fill
            txtCountry.Dock = DockStyle.Fill

            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            root.Controls.Add(CreateLabel("Institution"), 0, 0)
            root.Controls.Add(txtInstitution, 1, 0)

            root.Controls.Add(CreateLabel("Department"), 0, 1)
            root.Controls.Add(txtDepartment, 1, 1)

            root.Controls.Add(CreateLabel("City"), 0, 2)
            root.Controls.Add(txtCity, 1, 2)

            root.Controls.Add(CreateLabel("State / region"), 0, 3)
            root.Controls.Add(txtRegion, 1, 3)

            root.Controls.Add(CreateLabel("Country"), 0, 4)
            root.Controls.Add(txtCountry, 1, 4)

            root.Controls.Add(CreateLabel("Notes"), 0, 5)
            root.Controls.Add(txtNotes, 1, 5)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
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
                AddressOf SaveAffiliation

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(buttons, 0, 6)
            root.SetColumnSpan(buttons, 2)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)

        End Sub


        ' At the top of its row, its text on the line of the field beside
        ' it, which sits there too, as the correspondence window's labels
        ' are (#258, #266): centred in the row, it sat about 3 pixels below
        ' its box. Its row keeps the height it had.
        Private Function CreateLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(3, 6, 3, 12)
            }

        End Function


        Private Sub LoadSource()

            If _source Is Nothing Then
                Return
            End If

            txtInstitution.Text = _source.Institution
            txtDepartment.Text = _source.Department
            txtCity.Text = _source.City
            txtRegion.Text = _source.Region
            txtCountry.Text = _source.Country
            txtNotes.Text = _source.Notes

        End Sub


        Private Sub SaveAffiliation(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtInstitution.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Please enter the institution or organization.",
                    "Institution Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtInstitution.Focus()
                Return

            End If

            _result =
                New AffiliationRecord With {
                    .Id =
                        If(
                            _source Is Nothing,
                            Guid.NewGuid(),
                            _source.Id
                        ),
                    .Institution = txtInstitution.Text.Trim(),
                    .Department = txtDepartment.Text.Trim(),
                    .City = txtCity.Text.Trim(),
                    .Region = txtRegion.Text.Trim(),
                    .Country = txtCountry.Text.Trim(),
                    .Notes = txtNotes.Text.Trim()
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
