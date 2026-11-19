Imports ManuscriptPipeline.Models

Namespace Services
    Public NotInheritable Class EditorialDecisionDisplayService
        Private Sub New()
        End Sub

        Public Shared Function Format(value As EditorialDecision) As String
            Select Case value
                Case EditorialDecision.DeskRejected : Return "Desk rejected"
                Case EditorialDecision.RejectedAfterReview : Return "Rejected after review"
                Case EditorialDecision.MinorRevision : Return "Minor revision"
                Case EditorialDecision.MajorRevision : Return "Major revision"
                Case EditorialDecision.ReviseAndResubmit : Return "Revise and resubmit"
                Case Else : Return value.ToString()
            End Select
        End Function
    End Class
End Namespace
