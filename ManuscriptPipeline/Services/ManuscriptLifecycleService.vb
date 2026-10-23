Imports System
Imports System.Collections.Generic
Imports System.Globalization
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

            ValidateManuscript(
                manuscript
            )

            If submission Is Nothing Then
                Throw New ArgumentNullException(NameOf(submission))
            End If

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(
                    manuscript
                )

            If latest Is Nothing OrElse
               latest.Kind <>
               WorkflowEventKind.Submission OrElse
               latest.Submission Is Nothing OrElse
               latest.Submission.Id <>
               submission.Id Then

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

            ValidateManuscript(
                manuscript
            )

            If submission Is Nothing Then
                Throw New ArgumentNullException(NameOf(submission))
            End If

            If decision Is Nothing Then
                Throw New ArgumentNullException(NameOf(decision))
            End If

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(
                    manuscript
                )

            If latest Is Nothing OrElse
               latest.Kind <>
               WorkflowEventKind.Decision OrElse
               latest.Decision Is Nothing OrElse
               latest.Decision.Id <>
               decision.Id Then

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

            ValidateManuscript(
                manuscript
            )

            Dim latest As WorkflowEvent =
                FindLatestWorkflowEvent(
                    manuscript
                )

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

            ' Reconciling, as the manuscript page does when it opens and after
            ' an edit, puts the stage right and never takes a manuscript out
            ' of the File Drawer: only a submission or a decision recorded
            ' does (#207).
            Select Case latest.Kind

                Case WorkflowEventKind.Submission

                    ApplySubmissionState(
                        manuscript,
                        latest.Submission,
                        keepFileDrawer:=True
                    )

                    Return True

                Case WorkflowEventKind.Decision

                    Return ApplyDecisionState(
                        manuscript,
                        latest.Submission,
                        latest.Decision,
                        keepFileDrawer:=True
                    )

                Case Else
                    Return False

            End Select

        End Function


        Public Shared Function ReconcileAfterDecisionRemoval(
            manuscript As Manuscript,
            removedDecision As EditorialDecisionEvent
        ) As Boolean

            ValidateManuscript(
                manuscript
            )

            If removedDecision Is Nothing Then
                Throw New ArgumentNullException(NameOf(removedDecision))
            End If

            If CurrentStageMatchesRemovedDecision(
                manuscript,
                removedDecision
            ) Then

                Return ApplyLatestWorkflowState(
                    manuscript
                )

            End If

            Return ReconcileAfterWorkflowMutation(
                manuscript
            )

        End Function


        Public Shared Function ReconcileAfterSubmissionRemoval(
            manuscript As Manuscript,
            removedSubmission As JournalSubmission
        ) As Boolean

            ValidateManuscript(
                manuscript
            )

            If removedSubmission Is Nothing Then
                Throw New ArgumentNullException(NameOf(removedSubmission))
            End If

            Dim removedLatestDecision As EditorialDecisionEvent =
                ManuscriptAttentionService.
                    GetLatestDecision(
                        removedSubmission
                    )

            If removedLatestDecision IsNot Nothing AndAlso
               CurrentStageMatchesRemovedDecision(
                   manuscript,
                   removedLatestDecision
               ) Then

                Return ApplyLatestWorkflowState(
                    manuscript
                )

            End If

            If (
                manuscript.CurrentStage =
                    PaperStage.Submitted OrElse
                manuscript.CurrentStage =
                    PaperStage.UnderReview
            ) AndAlso
               manuscript.StageEnteredDate.Date =
               removedSubmission.SubmittedDate.Date Then

                Return ApplyLatestWorkflowState(
                    manuscript
                )

            End If

            Return ReconcileAfterWorkflowMutation(
                manuscript
            )

        End Function


        Public Shared Function ReconcileAfterWorkflowMutation(
            manuscript As Manuscript
        ) As Boolean

            ValidateManuscript(
                manuscript
            )

            If ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                manuscript.CurrentStage
            ) Then

                Return False

            End If

            Dim requirement As ManuscriptStageWorkflowRequirement =
                ManuscriptStagePolicyService.GetRequirement(
                    manuscript.CurrentStage
                )

            If requirement =
               ManuscriptStageWorkflowRequirement.None Then

                Return False

            End If

            Return ApplyLatestWorkflowState(
                manuscript
            )

        End Function


        Private Shared Function CurrentStageMatchesRemovedDecision(
            manuscript As Manuscript,
            decision As EditorialDecisionEvent
        ) As Boolean

            If manuscript.StageEnteredDate.Date <>
               decision.DecisionDate.Date Then

                Return False

            End If

            Select Case decision.Decision

                Case EditorialDecision.MajorRevision,
                     EditorialDecision.MinorRevision,
                     EditorialDecision.ReviseAndResubmit

                    Return manuscript.CurrentStage =
                        PaperStage.Revision

                Case EditorialDecision.Accepted

                    Return manuscript.CurrentStage =
                        PaperStage.Accepted

                Case EditorialDecision.Rejected,
                     EditorialDecision.DeskRejected,
                     EditorialDecision.RejectedAfterReview,
                     EditorialDecision.Withdrawn

                    Return manuscript.CurrentStage =
                        PaperStage.Draft

                Case Else

                    Return False

            End Select

        End Function


        Private Shared Function ApplyLatestWorkflowState(
            manuscript As Manuscript
        ) As Boolean

            ' Putting the stage back after a deletion keeps a filed
            ' manuscript filed (#207), as the fallback does.
            Dim latestSubmission As JournalSubmission =
                ManuscriptAttentionService.
                    GetLatestSubmission(
                        manuscript
                    )

            If latestSubmission Is Nothing Then

                ApplyFallbackDraftState(
                    manuscript
                )

            Else

                Dim latestDecision As EditorialDecisionEvent =
                    ManuscriptAttentionService.
                        GetLatestDecision(
                            latestSubmission
                        )

                If latestDecision Is Nothing OrElse
                   latestDecision.Decision =
                   EditorialDecision.None Then

                    ApplySubmissionState(
                        manuscript,
                        latestSubmission,
                        keepFileDrawer:=True
                    )

                ElseIf Not ApplyDecisionState(
                    manuscript,
                    latestSubmission,
                    latestDecision,
                    keepFileDrawer:=True
                ) Then

                    ApplySubmissionState(
                        manuscript,
                        latestSubmission,
                        keepFileDrawer:=True
                    )

                End If

            End If

            Return True

        End Function


        Private Shared Sub ValidateManuscript(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

        End Sub


        ' The history note a manuscript leaving the File Drawer keeps (#202):
        ' the sentence, then when and why it was filed, as "Restored from
        ' File Drawer to active Pipeline (filed Aug 17, 2026: Three
        ' rejections; revisit with a larger sample.)". With neither known,
        ' the sentence alone.
        Public Shared Function WithFiledClause(
            sentence As String,
            filedDate As DateTime?,
            filedReason As String
        ) As String

            Dim reason As String =
                If(
                    filedReason,
                    String.Empty
                ).Trim()

            If Not filedDate.HasValue AndAlso
               reason.Length = 0 Then

                Return sentence & "."

            End If

            Dim clause As String =
                "filed"

            If filedDate.HasValue Then

                clause &=
                    " " &
                    filedDate.Value.ToString(
                        "MMM d, yyyy",
                        CultureInfo.CurrentCulture
                    )

            End If

            If reason.Length > 0 Then

                clause &=
                    ": " &
                    reason

            End If

            Dim note As String =
                sentence &
                " (" &
                clause &
                ")"

            ' A reason that ends in a period closes the sentence.
            If reason.EndsWith(
                ".",
                StringComparison.Ordinal
            ) Then

                Return note

            End If

            Return note & "."

        End Function


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
            submission As JournalSubmission,
            Optional keepFileDrawer As Boolean = False
        )

            Dim stageBefore As PaperStage =
                manuscript.CurrentStage

            Dim enteredBefore As DateTime =
                manuscript.StageEnteredDate

            manuscript.CurrentStage =
                PaperStage.Submitted

            manuscript.StageEnteredDate =
                submission.SubmittedDate.Date

            manuscript.RevisionDeadline =
                Nothing

            MoveToPipeline(
                manuscript,
                stageBefore,
                enteredBefore,
                "Returned to the Pipeline by a new submission",
                keepFileDrawer
            )

            SetCurrentTarget(
                manuscript,
                submission
            )

        End Sub


        Private Shared Function ApplyDecisionState(
            manuscript As Manuscript,
            submission As JournalSubmission,
            decision As EditorialDecisionEvent,
            Optional keepFileDrawer As Boolean = False
        ) As Boolean

            If decision Is Nothing OrElse
               decision.Decision =
               EditorialDecision.None Then

                Return False

            End If

            Dim stageBefore As PaperStage =
                manuscript.CurrentStage

            Dim enteredBefore As DateTime =
                manuscript.StageEnteredDate

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

            MoveToPipeline(
                manuscript,
                stageBefore,
                enteredBefore,
                "Returned to the Pipeline by an editorial decision",
                keepFileDrawer
            )

            Return True

        End Function


        ' A submission or a decision puts the manuscript on the Pipeline. One
        ' it takes out of the File Drawer keeps when and why it was filed in
        ' a note, as Restore to Pipeline does, dated with the event in the
        ' stage it was filed in, and the two fields are cleared (#207). A
        ' filed manuscript stays filed while reconciling, and when the event
        ' leaves its stage and the date it entered it as they were: the
        ' decision it was filed after the same day, applied again by an edit.
        Private Shared Sub MoveToPipeline(
            manuscript As Manuscript,
            stageBefore As PaperStage,
            enteredBefore As DateTime,
            sentence As String,
            keepFileDrawer As Boolean
        )

            If manuscript.Location <>
               ManuscriptLocation.FileDrawer Then

                manuscript.Location =
                    ManuscriptLocation.Pipeline

                Return

            End If

            If keepFileDrawer OrElse
               (
                   manuscript.CurrentStage =
                       stageBefore AndAlso
                   manuscript.StageEnteredDate.Date =
                       enteredBefore.Date
               ) Then

                Return

            End If

            Dim returned As New HistoryEvent With {
                .EventDate =
                    manuscript.StageEnteredDate,
                .Stage =
                    stageBefore,
                .Note =
                    WithFiledClause(
                        sentence,
                        manuscript.FileDrawerDate,
                        manuscript.FileDrawerReason
                    )
            }

            ChronologyProvenanceService.StampCreated(
                returned
            )

            If manuscript.History Is Nothing Then

                manuscript.History =
                    New List(Of HistoryEvent)()

            End If

            manuscript.History.Add(
                returned
            )

            manuscript.Location =
                ManuscriptLocation.Pipeline

            manuscript.FileDrawerDate =
                Nothing

            manuscript.FileDrawerReason =
                String.Empty

        End Sub


        Private Shared Sub ApplyFallbackDraftState(
            manuscript As Manuscript
        )

            manuscript.CurrentStage =
                PaperStage.Draft

            manuscript.StageEnteredDate =
                DateTime.Now

            manuscript.RevisionDeadline =
                Nothing

            manuscript.TargetJournal =
                String.Empty

            manuscript.TargetJournalId =
                Nothing

            If manuscript.Location <>
               ManuscriptLocation.FileDrawer Then

                manuscript.Location =
                    ManuscriptLocation.Pipeline

            End If

        End Sub


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
                        .Kind =
                            WorkflowEventKind.Submission,
                        .EventDate =
                            submission.SubmittedDate.Date,
                        .Submission =
                            submission
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
                            .Kind =
                                WorkflowEventKind.Decision,
                            .EventDate =
                                decision.DecisionDate.Date,
                            .Submission =
                                submission,
                            .Decision =
                                decision
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

                latest =
                    candidate

            End If

        End Sub

    End Class

End Namespace
