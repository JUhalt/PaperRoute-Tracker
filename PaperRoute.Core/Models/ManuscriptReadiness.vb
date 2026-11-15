Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class ManuscriptReadiness

        Public Property Id As Guid = Guid.NewGuid()

        Public Property JournalId As Guid? = Nothing

        ' JournalName is a snapshot/fallback so readiness remains meaningful
        ' even if a reusable journal record is later renamed or removed.
        Public Property JournalName As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property CreatedAtUtc As DateTime = DateTime.UtcNow

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property Items As List(Of ReadinessItemState) =
            New List(Of ReadinessItemState)()

    End Class

End Namespace
