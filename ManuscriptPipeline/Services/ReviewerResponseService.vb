Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ReviewerResponseService
        Private Sub New()
        End Sub

        Public Shared Function GetItems(
            submission As JournalSubmission,
            Optional decisionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing
        ) As IReadOnlyList(Of ReviewerResponseItem)
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            Dim items = If(submission.ReviewerResponses, New List(Of ReviewerResponseItem)())
            Return items.Where(Function(item) item IsNot Nothing AndAlso
                (Not decisionId.HasValue OrElse item.DecisionId = decisionId.Value) AndAlso
                (Not revisionRoundNumber.HasValue OrElse item.RevisionRoundNumber = revisionRoundNumber.Value)).ToList().AsReadOnly()
        End Function

        Public Shared Function AddItem(
            submission As JournalSubmission,
            draft As ReviewerResponseItem
        ) As ReviewerResponseItem
            Dim candidate = CopyForEdit(submission)
            Dim item = ManuscriptCloneService.CloneReviewerResponse(draft)
            item.Id = Guid.NewGuid()
            item.CreatedAtUtc = DateTime.UtcNow
            item.LastModifiedAtUtc = Nothing
            candidate.ReviewerResponses.Add(item)
            NormalizeAndValidateSubmission(candidate)
            submission.ReviewerResponses = candidate.ReviewerResponses
            Return item
        End Function

        Public Shared Function UpdateItem(
            submission As JournalSubmission,
            itemId As Guid,
            draft As ReviewerResponseItem
        ) As ReviewerResponseItem
            Dim candidate = CopyForEdit(submission)
            Dim index = candidate.ReviewerResponses.FindIndex(Function(existing) existing.Id = itemId)
            If index < 0 Then Throw New ArgumentException("The response item is not part of this submission.", NameOf(itemId))
            Dim item = ManuscriptCloneService.CloneReviewerResponse(draft)
            item.Id = itemId
            item.CreatedAtUtc = candidate.ReviewerResponses(index).CreatedAtUtc
            item.LastModifiedAtUtc = DateTime.UtcNow
            candidate.ReviewerResponses(index) = item
            NormalizeAndValidateSubmission(candidate)
            submission.ReviewerResponses = candidate.ReviewerResponses
            Return item
        End Function

        Public Shared Function RemoveItem(submission As JournalSubmission, itemId As Guid) As Boolean
            Dim candidate = CopyForEdit(submission)
            Dim index = candidate.ReviewerResponses.FindIndex(Function(existing) existing.Id = itemId)
            If index < 0 Then Return False
            candidate.ReviewerResponses.RemoveAt(index)
            NormalizeAndValidateSubmission(candidate)
            submission.ReviewerResponses = candidate.ReviewerResponses
            Return True
        End Function

        Public Shared Function MoveItem(submission As JournalSubmission, itemId As Guid, offset As Integer) As Boolean
            If offset <> -1 AndAlso offset <> 1 Then
                Throw New ArgumentOutOfRangeException(NameOf(offset), "Move one position up or down.")
            End If
            Dim candidate = CopyForEdit(submission)
            Dim index = candidate.ReviewerResponses.FindIndex(Function(existing) existing.Id = itemId)
            If index < 0 Then Return False
            Dim target = index + offset
            If target < 0 OrElse target >= candidate.ReviewerResponses.Count Then Return False
            Dim item = candidate.ReviewerResponses(index)
            candidate.ReviewerResponses.RemoveAt(index)
            candidate.ReviewerResponses.Insert(target, item)
            NormalizeAndValidateSubmission(candidate)
            submission.ReviewerResponses = candidate.ReviewerResponses
            Return True
        End Function

        Public Shared Sub NormalizeAndValidateManuscript(manuscript As Manuscript)
            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If manuscript.Submissions Is Nothing Then manuscript.Submissions = New List(Of JournalSubmission)()
            For Each submission In manuscript.Submissions
                If submission Is Nothing Then Throw New InvalidDataException("The manuscript library contains a null submission record.")
                NormalizeAndValidateSubmission(submission)
                If submission.ReviewerResponses.Count > 0 AndAlso
                    manuscript.Submissions.Where(Function(candidate) candidate IsNot Nothing AndAlso candidate.Id = submission.Id).Count() <> 1 Then
                    Throw New InvalidDataException("Reviewer responses require an unambiguous owning submission identifier.")
                End If
            Next
        End Sub

        Public Shared Sub NormalizeAndValidateSubmission(submission As JournalSubmission)
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            If submission.ReviewerResponses Is Nothing Then submission.ReviewerResponses = New List(Of ReviewerResponseItem)()
            If submission.ReviewerResponses.Count = 0 Then Return
            If submission.Id = Guid.Empty Then Throw New InvalidDataException("Reviewer responses require a valid owning submission.")

            Dim ids As New HashSet(Of Guid)()
            For Each item In submission.ReviewerResponses
                If item Is Nothing Then Throw New InvalidDataException("The reviewer response matrix contains a null item.")
                If item.Id = Guid.Empty OrElse Not ids.Add(item.Id) Then
                    Throw New InvalidDataException("The reviewer response matrix contains an invalid or duplicate item identifier.")
                End If
                If item.DecisionId = Guid.Empty OrElse submission.Decisions Is Nothing OrElse
                    submission.Decisions.Where(Function(decision) decision IsNot Nothing AndAlso decision.Id = item.DecisionId).Count() <> 1 Then
                    Throw New InvalidDataException("Each reviewer response must reference an existing editorial decision in its own submission.")
                End If
                If item.RevisionRoundNumber <= 0 Then Throw New InvalidDataException("A reviewer response must have a positive revision-round number.")
                If Not [Enum].IsDefined(item.Status) Then Throw New InvalidDataException("The reviewer response matrix contains an unsupported status.")
                item.ReviewerLabel = If(item.ReviewerLabel, String.Empty)
                item.CommentText = If(item.CommentText, String.Empty)
                item.ActionText = If(item.ActionText, String.Empty)
                item.ResponseText = If(item.ResponseText, String.Empty)
                item.ManuscriptLocation = If(item.ManuscriptLocation, String.Empty)
                item.Notes = If(item.Notes, String.Empty)
                If String.IsNullOrWhiteSpace(item.ReviewerLabel) Then Throw New InvalidDataException("Enter a reviewer or editor label for each response item.")
                If String.IsNullOrWhiteSpace(item.CommentText) AndAlso String.IsNullOrWhiteSpace(item.ActionText) Then
                    Throw New InvalidDataException("Enter a reviewer comment or an action for each response item.")
                End If
            Next
        End Sub

        Public Shared Function GetDecisionReferenceCount(submission As JournalSubmission, decisionId As Guid) As Integer
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            Return If(submission.ReviewerResponses, New List(Of ReviewerResponseItem)()).Where(
                Function(item) item IsNot Nothing AndAlso item.DecisionId = decisionId).Count()
        End Function

        Public Shared Sub EnsureDecisionCanBeRemoved(submission As JournalSubmission, decisionId As Guid)
            If GetDecisionReferenceCount(submission, decisionId) > 0 Then
                Throw New InvalidOperationException("This editorial decision is linked to reviewer responses. Reassign or remove those response items before deleting the decision.")
            End If
        End Sub

        Public Shared Function FormatStatus(status As ReviewerResponseStatus) As String
            Select Case status
                Case ReviewerResponseStatus.Unresolved : Return "Unresolved"
                Case ReviewerResponseStatus.InProgress : Return "In progress"
                Case ReviewerResponseStatus.Addressed : Return "Addressed"
                Case ReviewerResponseStatus.NotApplicable : Return "Not applicable"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(status))
            End Select
        End Function

        Private Shared Function CopyForEdit(submission As JournalSubmission) As JournalSubmission
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            Dim candidate = ManuscriptCloneService.CloneSubmission(submission)
            NormalizeAndValidateSubmission(candidate)
            Return candidate
        End Function
    End Class

End Namespace
