Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class UpdateProgressForm
        Inherits Form

        Private ReadOnly lblStatus As New Label()
        Private ReadOnly progressBar As New ProgressBar()


        Public Sub New(
            version As String
        )

            BuildInterface(version)
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface(
            version As String
        )

            Me.Text = "Updating PaperRoute"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.ControlBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(500, 165)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(22)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim lblTitle As New Label With {
                .Text = "Installing PaperRoute " & version,
                .AutoSize = True,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Anchor = AnchorStyles.Left
            }

            progressBar.Dock = DockStyle.Fill
            progressBar.Minimum = 0
            progressBar.Maximum = 100
            progressBar.Value = 0

            lblStatus.Text = "Preparing update..."
            lblStatus.AutoSize = True
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Anchor = AnchorStyles.Left

            root.Controls.Add(lblTitle, 0, 0)
            root.Controls.Add(progressBar, 0, 1)
            root.Controls.Add(lblStatus, 0, 2)

            Me.Controls.Add(root)

        End Sub


        Public Sub SetProgress(
            value As Integer
        )

            If Me.InvokeRequired Then

                Me.BeginInvoke(
                    New Action(Of Integer)(AddressOf SetProgress),
                    value
                )

                Return

            End If

            progressBar.Value =
                Math.Max(
                    progressBar.Minimum,
                    Math.Min(
                        progressBar.Maximum,
                        value
                    )
                )

        End Sub


        Public Sub SetStatus(
            text As String
        )

            If Me.InvokeRequired Then

                Me.BeginInvoke(
                    New Action(Of String)(AddressOf SetStatus),
                    text
                )

                Return

            End If

            lblStatus.Text = text

        End Sub

    End Class

End Namespace
