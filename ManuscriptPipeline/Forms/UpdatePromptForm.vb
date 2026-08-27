Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class UpdatePromptForm
        Inherits Form

        Public Sub New(
            currentVersion As String,
            availableVersion As String,
            channelName As String,
            releaseNotes As String
        )

            BuildInterface(
                currentVersion,
                availableVersion,
                channelName,
                releaseNotes
            )

            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface(
            currentVersion As String,
            availableVersion As String,
            channelName As String,
            releaseNotes As String
        )

            Me.Text = "PaperRoute Update Available"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(620, 500)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(22)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))

            Dim lblTitle As New Label With {
                .Text = "A new PaperRoute update is ready",
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 16.0F, FontStyle.Bold),
                .Anchor = AnchorStyles.Left
            }

            Dim lblVersion As New Label With {
                .Text =
                    "Current: " & currentVersion &
                    "     →     Available: " & availableVersion &
                    Environment.NewLine &
                    "Update channel: " & channelName,
                .AutoSize = True,
                .ForeColor = UiTheme.SecondaryText(),
                .Anchor = AnchorStyles.Left
            }

            Dim lblNotes As New Label With {
                .Text = "What's new",
                .AutoSize = True,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Anchor = AnchorStyles.Left
            }

            Dim txtNotes As New TextBox With {
                .Text =
                    ReleaseNotesPresentationService.ToDisplayText(
                        releaseNotes
                    ),
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Vertical
            }

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnInstall As New Button With {
                .Text = "Download & Restart",
                .Width = 165,
                .Height = 38,
                .DialogResult = DialogResult.OK
            }

            Dim btnLater As New Button With {
                .Text = "Later",
                .Width = 95,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            buttons.Controls.Add(btnInstall)
            buttons.Controls.Add(btnLater)

            root.Controls.Add(lblTitle, 0, 0)
            root.Controls.Add(lblVersion, 0, 1)
            root.Controls.Add(lblNotes, 0, 2)
            root.Controls.Add(txtNotes, 0, 3)
            root.Controls.Add(buttons, 0, 4)

            Me.AcceptButton = btnInstall
            Me.CancelButton = btnLater
            Me.Controls.Add(root)

        End Sub

    End Class

End Namespace
