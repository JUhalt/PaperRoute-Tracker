Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class JournalSubmission

        Public Property Id As Guid = Guid.NewGuid()

        Public Property RecordedAtUtc As DateTime? = Nothing

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property JournalName As String = String.Empty

        Public Property JournalId As Guid? = Nothing

        Public Property ManuscriptNumber As String = String.Empty

        Public Property SubmittedDate As DateTime = DateTime.Now

        Public Property FollowUpDate As DateTime? = Nothing

        Public Property Notes As String = String.Empty

        Public Property PortalUrl As String = String.Empty

        Public Property Decisions As List(Of EditorialDecisionEvent) =
            New List(Of EditorialDecisionEvent)()

        Public Property Correspondence As List(Of CorrespondenceItem) =
            New List(Of CorrespondenceItem)()

        ' List order is the user-controlled matrix and export order.
        Public Property ReviewerResponses As List(Of ReviewerResponseItem) =
            New List(Of ReviewerResponseItem)()

    End Class

End Namespace
