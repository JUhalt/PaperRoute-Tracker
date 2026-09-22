Imports System
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms
    Public Class ReviewerResponseExportForm
        Inherits Form

        Private ReadOnly txtMarkdown As New TextBox()

        Public Sub New(markdown As String)
            Text = "Export Reviewer Responses"
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(900, 720)
            MinimumSize = New Size(640, 480)
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(18)
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.Controls.Add(New Label With {
                .Text = "Review and edit this Markdown before saving. This draft includes unsaved matrix edits; changes here affect only the exported file.",
                .Dock = DockStyle.Fill, .AutoSize = True, .Margin = New Padding(0, 0, 0, 12)
            }, 0, 0)
            txtMarkdown.Text = If(markdown, String.Empty)
            txtMarkdown.Multiline = True
            txtMarkdown.AcceptsReturn = True
            txtMarkdown.AcceptsTab = False
            txtMarkdown.ScrollBars = ScrollBars.Both
            txtMarkdown.WordWrap = False
            txtMarkdown.Dock = DockStyle.Fill
            txtMarkdown.AccessibleName = "Editable Markdown export"
            root.Controls.Add(txtMarkdown, 0, 1)
            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False, .Padding = New Padding(0, 12, 0, 0)
            }
            Dim close As New Button With {.Text = "Close", .AutoSize = True, .DialogResult = DialogResult.Cancel}
            Dim save As New Button With {.Text = "&Save Markdown...", .AutoSize = True}
            AddHandler save.Click, AddressOf SaveMarkdown
            footer.Controls.Add(close)
            footer.Controls.Add(save)
            root.Controls.Add(footer, 0, 2)
            Controls.Add(root)
            CancelButton = close
            UiPolish.ApplyDialog(Me)
        End Sub

        Private Sub SaveMarkdown(sender As Object, e As EventArgs)
            Using picker As New SaveFileDialog With {
                .Title = "Save Reviewer Responses", .Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt",
                .DefaultExt = "md", .AddExtension = True, .OverwritePrompt = True,
                .FileName = "Reviewer responses.md"
            }
                If picker.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    File.WriteAllText(picker.FileName, txtMarkdown.Text, New UTF8Encoding(False))
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    MessageBox.Show(Me, "The Markdown file could not be saved." & Environment.NewLine & ex.Message,
                                    "Export Reviewer Responses", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End Try
                Text = "Export Reviewer Responses — Saved"
            End Using
        End Sub
    End Class
End Namespace
