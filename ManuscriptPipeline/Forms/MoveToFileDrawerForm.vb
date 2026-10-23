Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Move to File Drawer, asked once (#205): the question, an optional
    ' reason, and Move to File Drawer or Cancel. Cancel can be told from an
    ' empty reason, which the input box it replaces could not.
    Public Class MoveToFileDrawerForm
        Inherits Form

        Private ReadOnly txtReason As New TextBox()


        ' The reason as typed, trimmed; empty when none was given.
        Public ReadOnly Property Reason As String
            Get
                Return txtReason.Text.Trim()
            End Get
        End Property


        Public Sub New(manuscriptTitle As String)

            BuildInterface(manuscriptTitle)
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface(manuscriptTitle As String)

            Me.Text = "Move to File Drawer"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            Me.AutoSize = True
            Me.AutoSizeMode = AutoSizeMode.GrowAndShrink
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(24)
            }

            Dim lblHeading As New Label With {
                .Text = "Move '" & manuscriptTitle & "' to the File Drawer?",
                .AutoSize = True,
                .MaximumSize = New Size(472, 0),
                .UseMnemonic = False,
                .Font = New Font(Me.Font.FontFamily, 13, FontStyle.Bold),
                .Margin = New Padding(3, 0, 3, 8)
            }

            Dim lblKept As New Label With {
                .Text = "Its history, submissions, decisions, and correspondence will be preserved.",
                .AutoSize = True,
                .MaximumSize = New Size(472, 0),
                .Margin = New Padding(3, 0, 3, 16)
            }

            Dim lblReason As New Label With {
                .Text = "Why are you filing it? (optional)",
                .AutoSize = True,
                .Margin = New Padding(3, 0, 3, 4)
            }

            txtReason.Width = 472
            txtReason.AccessibleName = "File Drawer reason"
            txtReason.Margin = New Padding(3, 0, 3, 20)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Margin = New Padding(0)
            }

            Dim btnMove As New Button With {
                .Text = "Move to File Drawer",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            buttons.Controls.Add(btnMove)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(lblHeading, 0, 0)
            root.Controls.Add(lblKept, 0, 1)
            root.Controls.Add(lblReason, 0, 2)
            root.Controls.Add(txtReason, 0, 3)
            root.Controls.Add(buttons, 0, 4)

            Me.AcceptButton = btnMove
            Me.CancelButton = btnCancel

            Me.Controls.Add(root)

        End Sub

    End Class

End Namespace
