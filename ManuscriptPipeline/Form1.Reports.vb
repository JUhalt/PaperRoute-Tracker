Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Shareable reports (#30, #63): saved as self-contained web pages that
' print to PDF. Building one never changes a record.
Partial Public Class Form1

    Private Function PreparedByName() As String
        Dim self As AuthorRecord = If(authorLibrary?.Authors, New List(Of AuthorRecord)()).
            FirstOrDefault(Function(author) author IsNot Nothing AndAlso author.IsMe)
        Return If(self?.DisplayName, String.Empty)
    End Function


    Private Sub SavePipelineReport(sender As Object, e As EventArgs)

        If manuscripts.Count = 0 Then
            MessageBox.Show(Me, "Add a manuscript before making a pipeline report.", "Pipeline Report", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim choices As List(Of Manuscript) = manuscripts.
            OrderBy(Function(item) item.Location).
            ThenBy(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).ToList()
        Dim preparedBy As String = PreparedByName()
        Dim today As DateTime = DateTime.Today

        Using dialog As New ReportForm("Pipeline Report", "PaperRoute-Pipeline-" & today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) & ".html", choices,
                                       Function(chosen, deadlines) ReportService.PipelineReport(chosen, today, preparedBy, deadlines))
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                lblStatus.Text = "Saved the pipeline report to " & Path.GetFileName(dialog.SavedPath) & "."
            End If
        End Using

    End Sub


    Private Sub SaveRouteReport(manuscript As Manuscript)

        If manuscript Is Nothing Then Return
        Dim today As DateTime = DateTime.Today
        Dim fileTitle As String = String.Concat(ReminderService.SafeManuscriptTitle(manuscript).Take(60).Where(Function(ch) Not Path.GetInvalidFileNameChars().Contains(ch))).Trim()

        Using dialog As New ReportForm("Route Report", fileTitle & " - Route.html", Nothing,
                                       Function(chosen, deadlines) ReportService.RouteReport(manuscript, today))
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                lblStatus.Text = "Saved the route report to " & Path.GetFileName(dialog.SavedPath) & "."
            End If
        End Using

    End Sub

End Class
