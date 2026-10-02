Imports System

Namespace Models

    Public Class EditorialDecisionEvent

        Public Property Id As Guid = Guid.NewGuid()

        Public Property RecordedAtUtc As DateTime? = Nothing

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property DecisionDate As DateTime = DateTime.Now

        Public Property Decision As EditorialDecision =
            EditorialDecision.None

        Public Property RevisionDeadline As DateTime? = Nothing

        Public Property Notes As String = String.Empty

        ' The decision was proposed by the AI assistant from a decision
        ' letter and confirmed by the researcher (#84); Nothing otherwise.
        Public Property Suggestion As AssistantSuggestion = Nothing

    End Class

End Namespace
