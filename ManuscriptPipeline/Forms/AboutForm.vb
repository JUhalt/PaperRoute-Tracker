Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AboutForm
        Inherits Form

        Public Sub New()

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            Me.Text = "About PaperRoute"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display; without the
            ' base dimensions the text grew at 150% but the dialog did not.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(640, 410)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi
            Me.BackColor = UiTheme.BoardBackground()

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 6,
                .Padding = New Padding(26),
                .BackColor = UiTheme.BoardBackground()
            }

            root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 116))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))

            Dim logo As New PictureBox With {
                .Dock = DockStyle.Top,
                .Height = 104,
                .Width = 104,
                .SizeMode = PictureBoxSizeMode.Zoom,
                .Margin = New Padding(0, 4, 12, 0),
                .BackColor = Color.Transparent
            }

            Try

                Dim appIcon As Icon =
                    Icon.ExtractAssociatedIcon(Application.ExecutablePath)

                If appIcon IsNot Nothing Then
                    logo.Image = appIcon.ToBitmap()
                End If

            Catch
                ' Branding image is optional at runtime.
            End Try

            Dim lblTitle As New Label With {
                .Text = "PaperRoute",
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 21.0F, FontStyle.Bold),
                .ForeColor = UiTheme.AccentColor(),
                .Anchor = AnchorStyles.Left
            }

            Dim lblTagline As New Label With {
                .Text = ProductInfo.Tagline,
                .AutoSize = True,
                .ForeColor = UiTheme.SecondaryText(),
                .Anchor = AnchorStyles.Left
            }

            Dim lblVersion As New Label With {
                .Text = "PaperRoute Tracker  •  Version " & UpdateService.CurrentVersionText(),
                .AutoSize = True,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Anchor = AnchorStyles.Left
            }

            Dim lblDescription As New Label With {
                .Text =
                    ProductInfo.Description &
                    Environment.NewLine & Environment.NewLine &
                    "Open source • Local first • Built for academic publishing workflows.",
                .Dock = DockStyle.Fill,
                .AutoSize = False,
                .ForeColor = UiTheme.PrimaryText()
            }

            Dim lblLicense As New Label With {
                .Text = "© " & DateTime.Now.Year.ToString() & " Joshua Uhalt  •  GNU GPL v3.0",
                .AutoSize = True,
                .ForeColor = UiTheme.SecondaryText(),
                .Anchor = AnchorStyles.Left
            }

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .BackColor = UiTheme.BoardBackground()
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .Width = 100,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            Dim btnCopyVersion As New Button With {
                .Text = "Copy Version Info",
                .AutoSize = True,
                .MinimumSize = New Size(138, 36),
                .Height = 36
            }

            AddHandler btnCopyVersion.Click,
                Sub(sender As Object, e As EventArgs)
                    Dim versionInfo As String =
                        "PaperRoute Tracker " & UpdateService.CurrentVersionText() & Environment.NewLine &
                        ".NET " & Environment.Version.ToString() & Environment.NewLine &
                        "Windows " & Environment.OSVersion.Version.ToString()

                    Try
                        Clipboard.SetText(versionInfo)
                        MessageBox.Show(
                            Me,
                            "Version information copied to the clipboard.",
                            "PaperRoute",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
                    Catch ex As Exception
                        MessageBox.Show(
                            Me,
                            "PaperRoute could not copy the version information." & Environment.NewLine & Environment.NewLine & ex.Message,
                            "PaperRoute",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning)
                    End Try
                End Sub

            buttons.Controls.Add(btnClose)
            buttons.Controls.Add(btnCopyVersion)

            root.Controls.Add(logo, 0, 0)
            root.SetRowSpan(logo, 4)
            root.Controls.Add(lblTitle, 1, 0)
            root.Controls.Add(lblTagline, 1, 1)
            root.Controls.Add(lblVersion, 1, 2)
            root.Controls.Add(lblDescription, 1, 3)
            root.Controls.Add(lblLicense, 1, 4)
            root.Controls.Add(buttons, 1, 5)

            Me.AcceptButton = btnClose
            Me.CancelButton = btnClose
            Me.Controls.Add(root)

        End Sub

    End Class

End Namespace
