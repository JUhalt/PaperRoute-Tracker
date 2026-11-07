Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class DeleteManuscriptForm
        Inherits Form

        Private ReadOnly _manuscriptTitle As String

        Private ReadOnly txtConfirm As New TextBox()
        Private ReadOnly btnDelete As New Button()


        Public Sub New(manuscriptTitle As String)

            _manuscriptTitle = manuscriptTitle

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            ' Sizes below are at 96 DPI and scale with the display (#93). At a
            ' real 150% display the window kept its 520x300 and its 72-pixel
            ' warning row while its text grew, and the warning lost "notes,
            ' and file references." to an ellipsis (v1.0's C3, #313).
            DialogScaling.Begin(Me)

            Me.Text = "Delete Manuscript"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(520, 300)
            Me.Font = New Font("Segoe UI", 10.0F)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(24)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim lblHeading As New Label With {
                .Text = "Permanently remove this manuscript?",
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 13, FontStyle.Bold),
                .ForeColor = SystemColors.ControlText
            }

            Dim lblWarning As New Label With {
                .Text =
                    "'" &
                    _manuscriptTitle &
                    "' will be removed from PaperRoute, including its " &
                    "submission history, editorial decisions, notes, and file references.",
                .Dock = DockStyle.Fill,
                .AutoEllipsis = True
            }

            Dim lblInstruction As New Label With {
                .Text = "Type DELETE to confirm:",
                .AutoSize = True,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

            txtConfirm.Dock = DockStyle.Fill

            AddHandler txtConfirm.TextChanged,
                AddressOf ConfirmationChanged

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            btnDelete.Text = "Delete Manuscript"
            btnDelete.AutoSize = True
            btnDelete.Height = 36
            btnDelete.Enabled = False

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnDelete.Click,
                AddressOf ConfirmDelete

            buttons.Controls.Add(btnDelete)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(lblHeading, 0, 0)
            root.Controls.Add(lblWarning, 0, 1)
            root.Controls.Add(lblInstruction, 0, 2)
            root.Controls.Add(txtConfirm, 0, 3)
            root.Controls.Add(buttons, 0, 4)

            Me.CancelButton = btnCancel

            Me.Controls.Add(root)

            DialogScaling.Finish(Me)

        End Sub


        Private Sub ConfirmationChanged(
            sender As Object,
            e As EventArgs
        )

            btnDelete.Enabled =
                String.Equals(
                    txtConfirm.Text.Trim(),
                    "DELETE",
                    StringComparison.Ordinal
                )

        End Sub


        Private Sub ConfirmDelete(
            sender As Object,
            e As EventArgs
        )

            If Not btnDelete.Enabled Then
                Return
            End If

            Me.DialogResult = DialogResult.OK

        End Sub

    End Class

End Namespace