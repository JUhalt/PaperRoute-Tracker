Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' Where accepted AI assistant suggestions came from (#84), kept tidy on
    ' load, save, and restore. Lenient: a malformed record is trimmed or
    ' dropped, never fatal.
    Public NotInheritable Class AssistantSuggestionService

        Private Sub New()
        End Sub

        Public Const MaximumSourceLength As Integer = 4000


        Public Shared Sub NormalizeManuscript(manuscript As Manuscript)
            If manuscript Is Nothing Then Return
            For Each submission As JournalSubmission In If(manuscript.Submissions, New List(Of JournalSubmission)()).Where(Function(item) item IsNot Nothing)
                For Each decision As EditorialDecisionEvent In If(submission.Decisions, New List(Of EditorialDecisionEvent)()).Where(Function(item) item IsNot Nothing)
                    decision.Suggestion = Normalize(decision.Suggestion)
                Next
                For Each item As ReviewerResponseItem In If(submission.ReviewerResponses, New List(Of ReviewerResponseItem)()).Where(Function(entry) entry IsNot Nothing)
                    item.CommentSuggestion = Normalize(item.CommentSuggestion)
                    item.ResponseSuggestion = Normalize(item.ResponseSuggestion)
                Next
            Next
            ' Requirements copied from a journal's checklist (#95).
            For Each profile As ManuscriptReadiness In If(manuscript.ReadinessProfiles, New List(Of ManuscriptReadiness)()).Where(Function(item) item IsNot Nothing)
                For Each item As ReadinessItemState In If(profile.Items, New List(Of ReadinessItemState)()).Where(Function(entry) entry IsNot Nothing)
                    item.Suggestion = Normalize(item.Suggestion)
                Next
            Next
        End Sub


        ' A journal's checklist requirements read from its author
        ' instructions (#95).
        Public Shared Sub NormalizeJournal(journal As JournalRecord)
            If journal?.ReadinessChecklistTemplate Is Nothing Then Return
            For Each item As JournalChecklistTemplateItem In journal.ReadinessChecklistTemplate.Where(Function(entry) entry IsNot Nothing)
                item.Suggestion = Normalize(item.Suggestion)
            Next
        End Sub


        Public Shared Function Normalize(suggestion As AssistantSuggestion) As AssistantSuggestion
            If suggestion Is Nothing Then Return Nothing
            suggestion.Feature = Clip(suggestion.Feature, 40)
            suggestion.Provider = Clip(suggestion.Provider, 80)
            suggestion.Model = Clip(suggestion.Model, 120)
            suggestion.SourceText = Clip(suggestion.SourceText, MaximumSourceLength)
            If suggestion.SuggestedUtc.HasValue AndAlso (suggestion.SuggestedUtc.Value.Year < 2000 OrElse suggestion.SuggestedUtc.Value.Year > 2200) Then suggestion.SuggestedUtc = Nothing
            If suggestion.Feature.Length = 0 AndAlso suggestion.Provider.Length = 0 AndAlso suggestion.Model.Length = 0 AndAlso suggestion.SourceText.Length = 0 Then Return Nothing
            Return suggestion
        End Function


        ' "Began as an AI suggestion (Claude, claude-opus-5-5, Oct 1, 2026)."
        Public Shared Function Describe(suggestion As AssistantSuggestion) As String
            If suggestion Is Nothing Then Return String.Empty
            Return "Began as an AI suggestion" & Details(suggestion) & "."
        End Function


        ' A checklist requirement read from a journal's author instructions
        ' (#95): where it came from, and after a year, that the journal may
        ' have changed it.
        Public Shared Function DescribeRequirement(suggestion As AssistantSuggestion, nowUtc As DateTime) As String
            If suggestion Is Nothing Then Return String.Empty
            Dim text As String = "Began as an AI suggestion from the journal's author instructions" & Details(suggestion) & "."
            If JournalFactsService.IsStale(suggestion.SuggestedUtc, nowUtc) Then
                text &= " That was over a year ago; check the journal's current instructions."
            End If
            Return text
        End Function


        ' " (Claude, claude-opus-5-5, Oct 1, 2026)", or "" when nothing is known.
        Private Shared Function Details(suggestion As AssistantSuggestion) As String
            Dim parts As New List(Of String)()
            If Not String.IsNullOrEmpty(suggestion.Provider) Then parts.Add(suggestion.Provider)
            If Not String.IsNullOrEmpty(suggestion.Model) Then parts.Add(suggestion.Model)
            If suggestion.SuggestedUtc.HasValue Then parts.Add(suggestion.SuggestedUtc.Value.ToLocalTime().ToString("MMM d, yyyy", Globalization.CultureInfo.CurrentCulture))
            Return If(parts.Count > 0, " (" & String.Join(", ", parts) & ")", String.Empty)
        End Function


        Private Shared Function Clip(value As String, length As Integer) As String
            Dim text As String = If(value, String.Empty).Trim()
            Return If(text.Length <= length, text, text.Substring(0, length))
        End Function

    End Class

End Namespace
