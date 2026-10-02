Imports System

Namespace Models

    Public Class ReviewerResponseItem
        Public Property Id As Guid = Guid.NewGuid()
        ' The owning JournalSubmission supplies the submission identity.
        ' Decisions and round numbers are selected explicitly, never inferred.
        Public Property DecisionId As Guid = Guid.Empty
        Public Property RevisionRoundNumber As Integer = 0
        Public Property ReviewerLabel As String = String.Empty
        Public Property CommentText As String = String.Empty
        Public Property ActionText As String = String.Empty
        Public Property ResponseText As String = String.Empty
        Public Property ManuscriptLocation As String = String.Empty
        Public Property Notes As String = String.Empty
        Public Property Status As ReviewerResponseStatus = ReviewerResponseStatus.Unresolved
        Public Property CreatedAtUtc As DateTime = DateTime.UtcNow
        Public Property LastModifiedAtUtc As DateTime? = Nothing
        ' The comment began as an AI assistant suggestion from a decision
        ' letter (#84); Nothing when it was entered by hand.
        Public Property CommentSuggestion As AssistantSuggestion = Nothing
        ' The draft response began as an AI assistant suggestion (#84).
        Public Property ResponseSuggestion As AssistantSuggestion = Nothing
    End Class

End Namespace
