Imports System
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ReviewerResponseExportService
        Private Sub New()
        End Sub

        ' Pure text generation: exporting never saves, normalizes, timestamps,
        ' reorders or otherwise changes the caller's matrix or workflow.
        Public Shared Function ExportMarkdown(manuscript As Manuscript, submission As JournalSubmission) As String
            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            If manuscript.Submissions Is Nothing OrElse
                manuscript.Submissions.Where(Function(item) item IsNot Nothing AndAlso item.Id = submission.Id).Count() <> 1 Then
                Throw New ArgumentException("The submission must belong to the manuscript being exported.", NameOf(submission))
            End If
            Dim snapshot = ManuscriptCloneService.CloneSubmission(submission)
            ReviewerResponseService.NormalizeAndValidateSubmission(snapshot)
            Dim output As New StringBuilder()
            output.AppendLine("# Response to reviewers — draft")
            output.AppendLine()
            AppendField(output, "Manuscript", manuscript.Title)
            AppendField(output, "Journal", snapshot.JournalName)
            AppendField(output, "Manuscript number", snapshot.ManuscriptNumber)
            output.AppendLine()
            If snapshot.ReviewerResponses.Count = 0 Then
                output.AppendLine("No reviewer response items have been recorded.")
            End If
            For index As Integer = 0 To snapshot.ReviewerResponses.Count - 1
                Dim item = snapshot.ReviewerResponses(index)
                Dim decision = snapshot.Decisions.Single(Function(candidate) candidate.Id = item.DecisionId)
                output.AppendLine("## " & (index + 1).ToString(CultureInfo.InvariantCulture) & ". " & EscapeMarkdown(item.ReviewerLabel))
                output.AppendLine()
                output.AppendLine("Revision round: " & item.RevisionRoundNumber.ToString(CultureInfo.InvariantCulture))
                output.AppendLine("Editorial decision: " & EscapeMarkdown(EditorialDecisionDisplayService.Format(decision.Decision)) & " — " & decision.DecisionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                output.AppendLine("Status: " & ReviewerResponseService.FormatStatus(item.Status))
                output.AppendLine()
                AppendSection(output, "Comment", item.CommentText)
                AppendSection(output, "Action", item.ActionText)
                AppendSection(output, "Response", item.ResponseText)
                AppendSection(output, "Manuscript location", item.ManuscriptLocation)
                AppendSection(output, "Notes", item.Notes)
            Next
            Return output.ToString()
        End Function

        Private Shared Sub AppendField(output As StringBuilder, label As String, value As String)
            output.AppendLine(label & ": " & EscapeMarkdown(value))
        End Sub

        Private Shared Sub AppendSection(output As StringBuilder, label As String, value As String)
            output.AppendLine("### " & label)
            output.AppendLine()
            output.AppendLine(If(String.IsNullOrWhiteSpace(value), "[Not entered]", EscapeMarkdown(value)))
            output.AppendLine()
        End Sub

        Private Shared Function EscapeMarkdown(value As String) As String
            Dim result = If(value, String.Empty).Replace("\", "\\")
            For Each character In New String() {"`", "*", "_", "{", "}", "[", "]", "(", ")", "#", "+", "-", ".", "!", "|", ">"}
                result = result.Replace(character, "\" & character)
            Next
            ' Raw HTML is never executable when this Markdown is rendered.
            Return result.Replace("&", "&amp;").Replace("<", "&lt;")
        End Function

    End Class

End Namespace
