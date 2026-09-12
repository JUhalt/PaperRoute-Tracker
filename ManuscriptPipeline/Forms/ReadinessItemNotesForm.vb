Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ReadinessItemNotesForm
        Inherits Form

        Private ReadOnly _source As ReadinessItemState
        Private ReadOnly txtNotes As New TextBox()

        Private _notes As String =
            String.Empty


        Public ReadOnly Property Notes As String
            Get
                Return _notes
            End Get
        End Property


        Public Sub New(
            source As ReadinessItemState
        )

            If source Is Nothing Then
                Throw New ArgumentNullException(
                    NameOf(source)
                )
            End If

            _source = source

            BuildInterface()
            UiPolish.ApplyDialog(Me)

            txtNotes.Text =
                If(
                    _source.UserNotes,
                    String.Empty
                )

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Readiness Notes"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    620,
                    430
                )

            Me.MinimumSize =
                New Size(
                    520,
                    360
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
                .RowCount = 3,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Percent, 40))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 60))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            Dim intro As New TextBox With {
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Vertical,
                .AccessibleName = "Requirement instructions",
                .Text =
                    _source.Title &
                    Environment.NewLine &
                    If(
                        String.IsNullOrWhiteSpace(
                            _source.Description
                        ),
                        "Add manuscript-specific notes for this requirement.",
                        _source.Description
                    ),
                .Margin = New Padding(0, 0, 0, 10)
            }

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True
            txtNotes.AccessibleName = "Manuscript-specific notes"

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save Notes",
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
                AddressOf SaveNotes

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(txtNotes, 0, 1)
            root.Controls.Add(buttons, 0, 2)

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(root)

        End Sub


        Private Sub SaveNotes(
            sender As Object,
            e As EventArgs
        )

            _notes =
                txtNotes.Text.Trim()

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
