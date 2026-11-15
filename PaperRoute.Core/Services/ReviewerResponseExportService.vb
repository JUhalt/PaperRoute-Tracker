Imports System
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
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
            output.AppendLine(If(String.IsNullOrWhiteSpace(value), "[Not entered]", EscapeMarkdown(value, startsLine:=True)))
            output.AppendLine()
        End Sub

        ' Escapes only what would change meaning when rendered, so the text
        ' stays readable as plain text (#73): markers at the start of a line,
        ' emphasis and code markers, link brackets, table pipes, and autolinks.
        ' Raw HTML is always neutralized.
        Private Shared ReadOnly LineStartMarker As New Regex("^(\s{0,3})([#>+\-=~])")
        Private Shared ReadOnly OrderedListMarker As New Regex("^(\s{0,3}\d{1,9})([.)])(?=\s|$)")

        Friend Shared Function EscapeMarkdown(value As String, Optional startsLine As Boolean = False) As String
            Dim lines As String() = If(value, String.Empty).Split(ControlChars.Lf)
            For index As Integer = 0 To lines.Length - 1
                Dim line As String = EscapeInline(lines(index))
                If startsLine OrElse index > 0 Then
                    Dim ordered As Match = OrderedListMarker.Match(line)
                    If ordered.Success Then
                        line = ordered.Groups(1).Value & "\" & line.Substring(ordered.Groups(2).Index)
                    Else
                        line = LineStartMarker.Replace(line, "$1\$2", 1)
                    End If
                End If
                lines(index) = line
            Next
            Return String.Join(ControlChars.Lf, lines)
        End Function

        Private Shared Function EscapeInline(line As String) As String
            Dim result As New StringBuilder(line.Length + 8)
            Dim index As Integer = 0
            While index < line.Length
                Dim character As Char = line(index)
                Dim previous As Char = If(index > 0, line(index - 1), " "c)
                Dim nextCharacter As Char = If(index + 1 < line.Length, line(index + 1), " "c)
                Select Case character
                    Case "\"c
                        ' A backslash escapes punctuation, and at the end of a line it
                        ' would become a line break.
                        Dim endsLine As Boolean = index + 1 >= line.Length OrElse nextCharacter = ControlChars.Cr
                        result.Append(If(endsLine OrElse Char.IsPunctuation(nextCharacter) OrElse Char.IsSymbol(nextCharacter), "\\", "\"))
                    Case "`"c, "*"c, "["c, "]"c, "|"c
                        result.Append("\"c).Append(character)
                    Case "_"c
                        ' Underscores inside words, as in file_name, are never emphasis.
                        If Char.IsLetterOrDigit(previous) AndAlso Char.IsLetterOrDigit(nextCharacter) Then
                            result.Append(character)
                        Else
                            result.Append("\_")
                        End If
                    Case "~"c
                        result.Append(If(previous = "~"c OrElse nextCharacter = "~"c, "\~", "~"))
                    Case "&"c
                        result.Append("&amp;")
                    Case "<"c
                        result.Append("&lt;")
                    Case ":"c
                        ' A bare URL stays text rather than becoming a link.
                        result.Append(If(String.CompareOrdinal(line, index, "://", 0, 3) = 0 AndAlso Char.IsLetter(previous), "\:", ":"))
                    Case "w"c, "W"c
                        If Not Char.IsLetterOrDigit(previous) AndAlso
                           String.Compare(line, index, "www.", 0, 4, StringComparison.OrdinalIgnoreCase) = 0 Then
                            result.Append(line, index, 3).Append("\.")
                            index += 4
                            Continue While
                        End If
                        result.Append(character)
                    Case Else
                        result.Append(character)
                End Select
                index += 1
            End While
            Return result.ToString()
        End Function

    End Class

End Namespace
