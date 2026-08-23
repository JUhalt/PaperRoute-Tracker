Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptLifecycleService

        Private Enum WorkflowEventKind
            Submission
            Decision
        End Enum

        Private Class WorkflowEvent

            Public Property Kind As WorkflowEventKind
            Public Property EventDate As DateTime
            Public Property Submission As JournalSubmission
            Public Property Decision As EditorialDecisionEvent

        End Class


        Private Sub New()
        End Sub


        Public Shared Function ApplySubmission(
            manuscript As Manuscript,
            submission As JournalSubmission
        ) As Boolean

            ValidateManuscript(manuscript)

            If submission Is Nothing Then
                Throw New ArgumentNullException(NameOf(submission))
            End If

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(manuscript)

            If latest Is Nothing OrElse
               latest.Kind <> WorkflowEventKind.Submission OrElse
               latest.Submission Is Nothing OrElse
               latest.Submission.Id <> submission.Id Then

                Return False

            End If

            If Not EventCanUpdateCurrentState(
                manuscript,
                submission.SubmittedDate.Date,
                allowSameDay:=True
            ) Then

                Return False

            End If

            ApplySubmissionState(
                manuscript,
                submission
            )

            Return True

        End Function


        Public Shared Function ApplyDecision(
            manuscript As Manuscript,
            submission As JournalSubmission,
            decision As EditorialDecisionEvent
        ) As Boolean

            ValidateManuscript(manuscript)

            If submission Is Nothing Then
                Throw New ArgumentNullException(NameOf(submission))
            End If

            If decision Is Nothing Then
                Throw New ArgumentNullException(NameOf(decision))
            End If

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(manuscript)

            If latest Is Nothing OrElse
               latest.Kind <> WorkflowEventKind.Decision OrElse
               latest.Decision Is Nothing OrElse
               latest.Decision.Id <> decision.Id Then

                Return False

            End If

            If Not EventCanUpdateCurrentState(
                manuscript,
                decision.DecisionDate.Date,
                allowSameDay:=True
            ) Then

                Return False

            End If

            Return ApplyDecisionState(
                manuscript,
                submission,
                decision
            )

        End Function


        Public Shared Function ReconcileFromLatestWorkflow(
            manuscript As Manuscript,
            Optional allowSameDay As Boolean = False
        ) As Boolean

            ValidateManuscript(manuscript)

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(manuscript)

            If latest Is Nothing Then
                Return False
            End If

            If Not EventCanUpdateCurrentState(
                manuscript,
                latest.EventDate.Date,
                allowSameDay
            ) Then

                Return False

            End If

            Select Case latest.Kind

                Case WorkflowEventKind.Submission

                    ApplySubmissionState(
                        manuscript,
                        latest.Submission
                    )

                    Return True

                Case WorkflowEventKind.Decision

                    Return ApplyDecisionState(
                        manuscript,
                        latest.Submission,
                        latest.Decision
                    )

                Case Else
                    Return False

            End Select

        End Function


        Private Shared Sub ValidateManuscript(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

        End Sub


        Private Shared Function EventCanUpdateCurrentState(
            manuscript As Manuscript,
            eventDate As DateTime,
            allowSameDay As Boolean
        ) As Boolean

            Dim comparisonDate As DateTime =
                manuscript.StageEnteredDate.Date

            If manuscript.Location =
               ManuscriptLocation.FileDrawer AndAlso
               manuscript.FileDrawerDate.HasValue AndAlso
               manuscript.FileDrawerDate.Value.Date >
               comparisonDate Then

                comparisonDate =
                    manuscript.FileDrawerDate.Value.Date

            End If

            If allowSameDay Then
                Return eventDate.Date >= comparisonDate
            End If

            Return eventDate.Date > comparisonDate

        End Function


        Private Shared Sub ApplySubmissionState(
            manuscript As Manuscript,
            submission As JournalSubmission
        )

            manuscript.CurrentStage =
                PaperStage.Submitted

            manuscript.StageEnteredDate =
                submission.SubmittedDate.Date

            manuscript.RevisionDeadline =
                Nothing

            manuscript.Location =
                ManuscriptLocation.Pipeline

            manuscript.TargetJournal =
                If(
                    submission.JournalName,
                    String.Empty
                ).Trim()

            manuscript.TargetJournalId =
                submission.JournalId

        End Sub


        Private Shared Function ApplyDecisionState(
            manuscript As Manuscript,
            submission As JournalSubmission,
            decision As EditorialDecisionEvent
        ) As Boolean

            If decision Is Nothing OrElse
               decision.Decision = EditorialDecision.None Then

                Return False

            End If

            Select Case decision.Decision

                Case EditorialDecision.MajorRevision,
                     EditorialDecision.MinorRevision,
                     EditorialDecision.ReviseAndResubmit

                    manuscript.CurrentStage =
                        PaperStage.Revision

                    manuscript.RevisionDeadline =
                        decision.RevisionDeadline

                    SetCurrentTarget(
                        manuscript,
                        submission
                    )

                Case EditorialDecision.Accepted

                    manuscript.CurrentStage =
                        PaperStage.Accepted

                    manuscript.RevisionDeadline =
                        Nothing

                    SetCurrentTarget(
                        manuscript,
                        submission
                    )

                Case EditorialDecision.Rejected,
                     EditorialDecision.DeskRejected,
                     EditorialDecision.RejectedAfterReview,
                     EditorialDecision.Withdrawn

                    manuscript.CurrentStage =
                        PaperStage.Draft

                    manuscript.RevisionDeadline =
                        Nothing

                    ClearTargetIfItMatches(
                        manuscript,
                        submission
                    )

                Case Else
                    Return False

            End Select

            manuscript.StageEnteredDate =
                decision.DecisionDate.Date

            manuscript.Location =
                ManuscriptLocation.Pipeline

            Return True

        End Function


        Private Shared Sub SetCurrentTarget(
            manuscript As Manuscript,
            submission As JournalSubmission
        )

            manuscript.TargetJournal =
                If(
                    submission.JournalName,
                    String.Empty
                ).Trim()

            manuscript.TargetJournalId =
                submission.JournalId

        End Sub


        Private Shared Sub ClearTargetIfItMatches(
            manuscript As Manuscript,
            submission As JournalSubmission
        )

            Dim idMatches As Boolean =
                manuscript.TargetJournalId.HasValue AndAlso
                submission.JournalId.HasValue AndAlso
                manuscript.TargetJournalId.Value =
                submission.JournalId.Value

            Dim nameMatches As Boolean =
                Not String.IsNullOrWhiteSpace(
                    manuscript.TargetJournal
                ) AndAlso
                String.Equals(
                    manuscript.TargetJournal.Trim(),
                    If(
                        submission.JournalName,
                        String.Empty
                    ).Trim(),
                    StringComparison.CurrentCultureIgnoreCase
                )

            If Not idMatches AndAlso
               Not nameMatches Then

                Return

            End If

            manuscript.TargetJournal =
                String.Empty

            manuscript.TargetJournalId =
                Nothing

        End Sub


        Private Shared Function FindLatestWorkflowEvent(
            manuscript As Manuscript
        ) As WorkflowEvent

            If manuscript.Submissions Is Nothing Then
                Return Nothing
            End If

            Dim latest As WorkflowEvent =
                Nothing

            For Each submission As JournalSubmission In
                manuscript.Submissions

                If submission Is Nothing Then
                    Continue For
                End If

                ConsiderCandidate(
                    latest,
                    New WorkflowEvent With {
                        .Kind = WorkflowEventKind.Submission,
                        .EventDate = submission.SubmittedDate.Date,
                        .Submission = submission
                    }
                )

                If submission.Decisions Is Nothing Then
                    Continue For
                End If

                For Each decision As EditorialDecisionEvent In
                    submission.Decisions

                    If decision Is Nothing Then
                        Continue For
                    End If

                    ConsiderCandidate(
                        latest,
                        New WorkflowEvent With {
                            .Kind = WorkflowEventKind.Decision,
                            .EventDate = decision.DecisionDate.Date,
                            .Submission = submission,
                            .Decision = decision
                        }
                    )

                Next

            Next

            Return latest

        End Function


        Private Shared Sub ConsiderCandidate(
            ByRef latest As WorkflowEvent,
            candidate As WorkflowEvent
        )

            If latest Is Nothing Then

                latest =
                    candidate

                Return

            End If

            If candidate.EventDate >
               latest.EventDate Then

                latest =
                    candidate

                Return

            End If

            If candidate.EventDate <
               latest.EventDate Then

                Return

            End If

            If candidate.Kind =
               WorkflowEventKind.Decision AndAlso
               latest.Kind =
               WorkflowEventKind.Submission Then

                latest =
                    candidate

                Return

            End If

            If candidate.Kind =
               latest.Kind Then

                ' Lists preserve workflow entry order. When two events share
                ' a calendar date, the later stored event is the later event.
                latest =
                    candidate

            End If

        End Sub

    End Class

End Namespace
