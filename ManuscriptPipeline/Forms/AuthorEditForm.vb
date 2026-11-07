Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AuthorEditForm
        Inherits Form

        Private ReadOnly _source As AuthorRecord

        Private ReadOnly txtGiven As New TextBox()
        Private ReadOnly txtMiddle As New TextBox()
        Private ReadOnly txtFamily As New TextBox()
        Private ReadOnly txtSuffix As New TextBox()
        Private ReadOnly txtDisplay As New TextBox()
        Private ReadOnly txtOrcid As New TextBox()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly chkMe As New CheckBox()

        Private _result As AuthorRecord


        Public ReadOnly Property Result As AuthorRecord
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            source As AuthorRecord
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
                    "Add Author",
                    "Edit Author"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    650,
                    600
                )

            Me.MinimumSize =
                New Size(
                    590,
                    540
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
                .RowCount = 9,
                .Padding = New Padding(20)
            }

            ' As wide as its widest label, and never less than 170 pixels
            ' (CreateLabel). Windows Forms does not scale this window, so
            ' at 150% the bold Display override outgrew a fixed 170 and
            ' wrapped onto a second line, as Add Manuscript's labels did
            ' (#263, #313).
            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For i As Integer = 0 To 6
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

            txtGiven.Dock = DockStyle.Fill
            txtMiddle.Dock = DockStyle.Fill
            txtFamily.Dock = DockStyle.Fill
            txtSuffix.Dock = DockStyle.Fill
            txtDisplay.Dock = DockStyle.Fill
            txtOrcid.Dock = DockStyle.Fill

            ' The box takes the row the window leaves it. It kept a least
            ' height of 110 pixels, which the row does not have at 150%,
            ' where the window keeps its 650x600 pixels and the rows above
            ' grow with the text: the box ran into the footer's row, over
            ' the tops of Cancel and Save (#313).
            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            chkMe.Text = "This is me"
            chkMe.AutoSize = True
            ' At the top of its row, its caption on Identity's line (#266).
            chkMe.Anchor = AnchorStyles.Top Or AnchorStyles.Left
            chkMe.Margin = New Padding(3, 5, 3, 3)

            root.Controls.Add(CreateLabel("Given name"), 0, 0)
            root.Controls.Add(txtGiven, 1, 0)

            root.Controls.Add(CreateLabel("Middle name"), 0, 1)
            root.Controls.Add(txtMiddle, 1, 1)

            root.Controls.Add(CreateLabel("Family name"), 0, 2)
            root.Controls.Add(txtFamily, 1, 2)

            root.Controls.Add(CreateLabel("Suffix"), 0, 3)
            root.Controls.Add(txtSuffix, 1, 3)

            root.Controls.Add(CreateLabel("Display override"), 0, 4)
            root.Controls.Add(txtDisplay, 1, 4)

            root.Controls.Add(CreateLabel("ORCID iD"), 0, 5)
            root.Controls.Add(txtOrcid, 1, 5)

            root.Controls.Add(CreateLabel("Identity"), 0, 6)
            root.Controls.Add(chkMe, 1, 6)

            root.Controls.Add(CreateLabel("Notes"), 0, 7)
            root.Controls.Add(txtNotes, 1, 7)

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
                AddressOf SaveAuthor

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(buttons, 0, 8)
            root.SetColumnSpan(buttons, 2)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)

        End Sub


        ' At the top of its row, its text on the line of the field beside
        ' it, which sits there too, as the correspondence window's labels
        ' are (#258, #266): centred in the row, it sat about 3 pixels below
        ' its box. Its row keeps the height it had. Its least width, with
        ' its margins, is the 170 pixels its column had, so the column
        ' keeps them wherever every label fits (#313).
        Private Function CreateLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(3, 6, 3, 12),
                .MinimumSize = New Size(164, 0)
            }

        End Function


        Private Sub LoadSource()

            If _source Is Nothing Then
                Return
            End If

            txtGiven.Text = _source.GivenName
            txtMiddle.Text = _source.MiddleName
            txtFamily.Text = _source.FamilyName
            txtSuffix.Text = _source.Suffix
            txtDisplay.Text = _source.DisplayNameOverride
            txtOrcid.Text = _source.Orcid
            txtNotes.Text = _source.Notes
            chkMe.Checked = _source.IsMe

        End Sub


        Private Sub SaveAuthor(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtDisplay.Text
            ) AndAlso
               String.IsNullOrWhiteSpace(
                   txtGiven.Text
               ) AndAlso
               String.IsNullOrWhiteSpace(
                   txtFamily.Text
               ) Then

                MessageBox.Show(
                    Me,
                    "Enter a name or a display-name override.",
                    "Author Name Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtGiven.Focus()
                Return

            End If

            Dim orcid As String =
                OrcidIdentifierService.Normalize(
                    txtOrcid.Text
                )

            If Not String.IsNullOrWhiteSpace(
                orcid
            ) AndAlso
               Not OrcidIdentifierService.IsValid(
                   orcid
               ) Then

                MessageBox.Show(
                    Me,
                    "Enter a valid ORCID iD, including its checksum. Example: 0000-0002-1825-0097.",
                    "Check ORCID iD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtOrcid.Focus()
                Return

            End If

            _result =
                New AuthorRecord With {
                    .Id =
                        If(
                            _source Is Nothing,
                            Guid.NewGuid(),
                            _source.Id
                        ),
                    .GivenName = txtGiven.Text.Trim(),
                    .MiddleName = txtMiddle.Text.Trim(),
                    .FamilyName = txtFamily.Text.Trim(),
                    .Suffix = txtSuffix.Text.Trim(),
                    .DisplayNameOverride = txtDisplay.Text.Trim(),
                    .Orcid = orcid,
                    .OrcidLastCheckedUtc =
                        If(
                            _source Is Nothing,
                            Nothing,
                            _source.OrcidLastCheckedUtc
                        ),
                    .Notes = txtNotes.Text.Trim(),
                    .IsMe = chkMe.Checked
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
