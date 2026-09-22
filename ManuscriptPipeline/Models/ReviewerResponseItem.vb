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
    End Class

End Namespace
