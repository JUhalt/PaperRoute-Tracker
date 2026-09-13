Imports System
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class SubmissionPacketWorkflowService
        Private Sub New()
        End Sub

        ' Called only after the user completes Record Journal Submission. Validate
        ' the whole candidate before adopting any records or lifecycle changes.
        Public Shared Sub RecordSubmissionForPacket(manuscript As Manuscript, packetId As Guid,
                                                    submission As JournalSubmission)
            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            If String.IsNullOrWhiteSpace(submission.JournalName) Then
                Throw New InvalidOperationException("Record the journal before associating a submission.")
            End If
            Dim candidate As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
            Dim packet As SubmissionPacket = candidate.SubmissionPackets.SingleOrDefault(Function(item) item.Id = packetId)
            If packet Is Nothing Then Throw New InvalidOperationException("The selected packet no longer exists.")
            If packet.SubmissionId.HasValue Then
                Throw New InvalidOperationException("This packet is already associated with a submission. Open that submission or prepare a separate packet.")
            End If
            If packet.JournalId.HasValue AndAlso submission.JournalId.HasValue AndAlso
               packet.JournalId.Value <> submission.JournalId.Value Then
                Throw New InvalidOperationException("The recorded journal differs from this prepared packet. Edit the packet's journal associations or prepare a separate packet before recording this submission.")
            End If
            If candidate.Submissions.Any(Function(item) item.Id = submission.Id) Then
                Throw New InvalidOperationException("This submission is already recorded.")
            End If
            Dim recorded As JournalSubmission = ManuscriptCloneService.CloneSubmission(submission)
            candidate.Submissions.Add(recorded)
            SubmissionPacketService.UpdatePacket(candidate, packet.Id, packet.ManuscriptVersionId,
                packet.Label, packet.Notes, packet.ReadinessProfileId, recorded.Id)
            SubmissionReadinessValidationService.NormalizeAndValidateManuscript(candidate)
            ManuscriptLifecycleService.ApplySubmission(candidate, recorded)

            manuscript.Submissions = candidate.Submissions
            manuscript.SubmissionPackets = candidate.SubmissionPackets
            manuscript.CurrentStage = candidate.CurrentStage
            manuscript.StageEnteredDate = candidate.StageEnteredDate
            manuscript.RevisionDeadline = candidate.RevisionDeadline
            manuscript.Location = candidate.Location
            manuscript.TargetJournal = candidate.TargetJournal
            manuscript.TargetJournalId = candidate.TargetJournalId
        End Sub
    End Class

End Namespace
