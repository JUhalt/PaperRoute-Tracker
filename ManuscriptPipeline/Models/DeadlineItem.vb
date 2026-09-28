Imports System

Namespace Models

    ' One row on the Deadlines page. It is derived from the record that owns
    ' it each time the page is built and is never stored.
    Public Class DeadlineItem

        Public Property Kind As DeadlineKind

        Public Property Group As DeadlineGroup

        ' Nothing for work that has no date yet.
        Public Property DueDate As DateTime? = Nothing

        Public Property Title As String = String.Empty

        Public Property ManuscriptId As Guid

        Public Property ManuscriptTitle As String = String.Empty

        Public Property JournalName As String = String.Empty

        ' The records that own the item, for opening it where it lives.
        Public Property SubmissionId As Guid? = Nothing

        Public Property DecisionId As Guid? = Nothing

        Public Property PacketId As Guid? = Nothing

        Public Property ReminderId As Guid? = Nothing

        ' Progress, when the item has parts: reviewer comments for a revision
        ' (addressed or not applicable count as done) or required checklist
        ' items for a packet. A total of zero means no progress is shown.
        Public Property ProgressDone As Integer

        Public Property ProgressActive As Integer

        Public Property ProgressTotal As Integer

        ' When a reminder in the Done group was completed.
        Public Property CompletedDate As DateTime? = Nothing

    End Class

End Namespace
