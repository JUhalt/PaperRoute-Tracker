Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ExternalLinkEditForm
        Inherits Form

        Private ReadOnly _source As ManuscriptExternalLink

        Private ReadOnly txtLabel As New TextBox()
        Private ReadOnly txtUrl As New TextBox()
        Private ReadOnly txtNotes As New TextBox()

        Private _result As ManuscriptExternalLink


        Public ReadOnly Property Result As ManuscriptExternalLink
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            source As ManuscriptExternalLink
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
                    "Add External Link",
                    "Edit External Link"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    660,
                    440
                )

            Me.MinimumSize =
                New Size(
                    560,
                    380
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
                .RowCount = 4,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    130
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

            txtLabel.Dock = DockStyle.Fill
            txtUrl.Dock = DockStyle.Fill
            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            root.Controls.Add(CreateLabel("Label"), 0, 0)
            root.Controls.Add(txtLabel, 1, 0)

            root.Controls.Add(CreateLabel("URL"), 0, 1)
            root.Controls.Add(txtUrl, 1, 1)

            root.Controls.Add(CreateLabel("Notes"), 0, 2)
            root.Controls.Add(txtNotes, 1, 2)

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
                AddressOf SaveLink

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(buttons, 0, 3)
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
                .Font = New Font(
                    Me.Font,
                    FontStyle.Bold
                ),
                .Margin = New Padding(
                    3,
                    6,
                    3,
                    12
                )
            }

        End Function


        Private Sub LoadSource()

            If _source Is Nothing Then
                Return
            End If

            txtLabel.Text = _source.Label
            txtUrl.Text = _source.Url
            txtNotes.Text = _source.Notes

        End Sub


        Private Sub SaveLink(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtLabel.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a short label such as OSF Project, Data, Preregistration, or Publisher Page.",
                    "Link Label Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtLabel.Focus()
                Return

            End If

            Dim normalizedUrl As String

            Try

                normalizedUrl =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtUrl.Text,
                        "External link"
                    )

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check External Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtUrl.Focus()
                Return

            End Try

            If String.IsNullOrWhiteSpace(
                normalizedUrl
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a URL.",
                    "Link URL Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtUrl.Focus()
                Return

            End If

            _result =
                New ManuscriptExternalLink With {
                    .Id =
                        If(
                            _source Is Nothing,
                            Guid.NewGuid(),
                            _source.Id
                        ),
                    .Label = txtLabel.Text.Trim(),
                    .Url = normalizedUrl,
                    .Notes = txtNotes.Text.Trim()
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
