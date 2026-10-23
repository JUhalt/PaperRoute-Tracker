Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptAttentionSnapshot

        Public Property LatestSubmission As JournalSubmission = Nothing
        Public Property LatestDecision As EditorialDecisionEvent = Nothing

        Public Property SubmissionCount As Integer = 0
        Public Property RejectionCount As Integer = 0

        Public Property HasOverdueRevision As Boolean = False
        Public Property IsRevisionDueSoon As Boolean = False
        Public Property IsLongWaitingManuscript As Boolean = False
        Public Property HasMissingTargetJournal As Boolean = False
        Public Property WasRecentlyRejected As Boolean = False

        ' The latest submission ended in a rejection or a withdrawal, and the
        ' manuscript was not restored from the File Drawer since (#203). The
        ' Board suggests filing when this holds and the rejections reach the
        ' threshold.
        Public Property CanSuggestFiling As Boolean = False

        Public Property OverdueDays As Integer? = Nothing
        Public Property RevisionDaysRemaining As Integer? = Nothing
        Public Property WaitingDays As Integer? = Nothing
        Public Property RejectionDaysAgo As Integer? = Nothing

    End Class


    Public NotInheritable Class ManuscriptAttentionService

        ' How Form1.RestoreToPipeline's history note begins; when and why the
        ' manuscript was filed follow it (#202).
        Public Const RestoredFromFileDrawerNote As String =
            "Restored from File Drawer to active Pipeline"


        Private Sub New()
        End Sub


        Public Shared Function Evaluate(
            manuscript As Manuscript,
            today As DateTime,
            revisionWarningDays As Integer,
            longReviewThresholdDays As Integer,
            recentRejectionThresholdDays As Integer
        ) As ManuscriptAttentionSnapshot

            Dim snapshot As New ManuscriptAttentionSnapshot()

            If manuscript Is Nothing Then
                Return snapshot
            End If

            Dim latestSubmission As JournalSubmission =
                Nothing

            Dim latestDecision As EditorialDecisionEvent =
                Nothing

            If manuscript.Submissions IsNot Nothing Then

                For Each submission As JournalSubmission In
                    manuscript.Submissions

                    If submission Is Nothing Then
                        Continue For
                    End If

                    snapshot.SubmissionCount +=
                        1

                    Dim latestDecisionForSubmission As EditorialDecisionEvent =
                        Nothing

                    If submission.Decisions IsNot Nothing Then

                        For Each decision As EditorialDecisionEvent In
                            submission.Decisions

                            If decision Is Nothing Then
                                Continue For
                            End If

                            If IsRejectionDecision(
                                decision.Decision
                            ) Then

                                snapshot.RejectionCount +=
                                    1

                            End If

                            If latestDecisionForSubmission Is Nothing OrElse
                               decision.DecisionDate.Date >=
                               latestDecisionForSubmission.DecisionDate.Date Then

                                latestDecisionForSubmission =
                                    decision

                            End If

                        Next

                    End If

                    If latestSubmission Is Nothing OrElse
                       submission.SubmittedDate.Date >=
                       latestSubmission.SubmittedDate.Date Then

                        latestSubmission =
                            submission

                        latestDecision =
                            latestDecisionForSubmission

                    End If

                Next

            End If

            snapshot.LatestSubmission =
                latestSubmission

            snapshot.LatestDecision =
                latestDecision

            Dim todayDate As DateTime =
                today.Date

            If manuscript.Location =
               ManuscriptLocation.Pipeline Then

                If manuscript.CurrentStage =
                   PaperStage.Revision AndAlso
                   latestDecision IsNot Nothing AndAlso
                   latestDecision.RevisionDeadline.HasValue Then

                    Dim daysRemaining As Integer =
                        CInt(
                            Math.Floor(
                                (
                                    latestDecision.
                                        RevisionDeadline.
                                        Value.
                                        Date -
                                    todayDate
                                ).TotalDays
                            )
                        )

                    snapshot.RevisionDaysRemaining =
                        daysRemaining

                    If daysRemaining < 0 Then

                        snapshot.HasOverdueRevision =
                            True

                        snapshot.OverdueDays =
                            Math.Abs(
                                daysRemaining
                            )

                    ElseIf daysRemaining <=
                           revisionWarningDays Then

                        snapshot.IsRevisionDueSoon =
                            True

                    End If

                End If

                If (
                    manuscript.CurrentStage =
                        PaperStage.Submitted OrElse
                    manuscript.CurrentStage =
                        PaperStage.UnderReview
                ) AndAlso
                   latestSubmission IsNot Nothing Then

                    Dim waitingDays As Integer =
                        CInt(
                            Math.Floor(
                                (
                                    todayDate -
                                    latestSubmission.
                                        SubmittedDate.
                                        Date
                                ).TotalDays
                            )
                        )

                    snapshot.WaitingDays =
                        waitingDays

                    snapshot.IsLongWaitingManuscript =
                        waitingDays >=
                        longReviewThresholdDays

                End If

                If (
                    manuscript.CurrentStage =
                        PaperStage.Idea OrElse
                    manuscript.CurrentStage =
                        PaperStage.Draft
                ) AndAlso
                   String.IsNullOrWhiteSpace(
                       manuscript.TargetJournal
                   ) Then

                    snapshot.HasMissingTargetJournal =
                        True

                End If

            End If

            If latestDecision IsNot Nothing AndAlso
               IsRejectionDecision(
                   latestDecision.Decision
               ) Then

                Dim daysAgo As Integer =
                    CInt(
                        Math.Floor(
                            (
                                todayDate -
                                latestDecision.
                                    DecisionDate.
                                    Date
                            ).TotalDays
                        )
                    )

                snapshot.RejectionDaysAgo =
                    daysAgo

                snapshot.WasRecentlyRejected =
                    daysAgo >= 0 AndAlso
                    daysAgo <=
                    recentRejectionThresholdDays

            End If

            If latestDecision IsNot Nothing AndAlso
               (
                   IsRejectionDecision(
                       latestDecision.Decision
                   ) OrElse
                   latestDecision.Decision =
                       EditorialDecision.Withdrawn
               ) Then

                snapshot.CanSuggestFiling =
                    Not WasRestoredSince(
                        manuscript,
                        latestDecision.DecisionDate
                    )

            End If

            Return snapshot

        End Function


        ' A revival after the decision (#203): the researcher took the
        ' manuscript out of the File Drawer knowing how its last route ended.
        Private Shared Function WasRestoredSince(
            manuscript As Manuscript,
            decisionDate As DateTime
        ) As Boolean

            If manuscript.History Is Nothing Then
                Return False
            End If

            For Each historyEvent As HistoryEvent In
                manuscript.History

                If historyEvent IsNot Nothing AndAlso
                   historyEvent.EventDate.Date >=
                   decisionDate.Date AndAlso
                   If(
                       historyEvent.Note,
                       String.Empty
                   ).StartsWith(
                       RestoredFromFileDrawerNote,
                       StringComparison.Ordinal
                   ) Then

                    Return True

                End If

            Next

            Return False

        End Function


        ' =====================================================
        ' Compatibility helpers
        ' =====================================================

        Public Shared Function HasOverdueRevision(
            manuscript As Manuscript,
            today As DateTime
        ) As Boolean

            Return Evaluate(
                manuscript,
                today,
                revisionWarningDays:=0,
                longReviewThresholdDays:=Integer.MaxValue,
                recentRejectionThresholdDays:=0
            ).HasOverdueRevision

        End Function


        Public Shared Function IsRevisionDueSoon(
            manuscript As Manuscript,
            today As DateTime,
            warningDays As Integer
        ) As Boolean

            Return Evaluate(
                manuscript,
                today,
                warningDays,
                longReviewThresholdDays:=Integer.MaxValue,
                recentRejectionThresholdDays:=0
            ).IsRevisionDueSoon

        End Function


        Public Shared Function IsLongWaitingManuscript(
            manuscript As Manuscript,
            today As DateTime,
            thresholdDays As Integer
        ) As Boolean

            Return Evaluate(
                manuscript,
                today,
                revisionWarningDays:=0,
                longReviewThresholdDays:=thresholdDays,
                recentRejectionThresholdDays:=0
            ).IsLongWaitingManuscript

        End Function


        Public Shared Function HasMissingTargetJournal(
            manuscript As Manuscript
        ) As Boolean

            If manuscript Is Nothing Then
                Return False
            End If

            If manuscript.Location <>
               ManuscriptLocation.Pipeline Then

                Return False

            End If

            If manuscript.CurrentStage <>
               PaperStage.Idea AndAlso
               manuscript.CurrentStage <>
               PaperStage.Draft Then

                Return False

            End If

            Return String.IsNullOrWhiteSpace(
                manuscript.TargetJournal
            )

        End Function


        Public Shared Function WasRecentlyRejected(
            manuscript As Manuscript,
            today As DateTime,
            thresholdDays As Integer
        ) As Boolean

            Return Evaluate(
                manuscript,
                today,
                revisionWarningDays:=0,
                longReviewThresholdDays:=Integer.MaxValue,
                recentRejectionThresholdDays:=thresholdDays
            ).WasRecentlyRejected

        End Function


        ' =====================================================
        ' Latest workflow helpers
        ' =====================================================

        Public Shared Function GetLatestSubmission(
            manuscript As Manuscript
        ) As JournalSubmission

            If manuscript Is Nothing OrElse
               manuscript.Submissions Is Nothing Then

                Return Nothing

            End If

            Dim latest As JournalSubmission =
                Nothing

            For Each submission As JournalSubmission In
                manuscript.Submissions

                If submission Is Nothing Then
                    Continue For
                End If

                If latest Is Nothing OrElse
                   submission.SubmittedDate.Date >=
                   latest.SubmittedDate.Date Then

                    latest =
                        submission

                End If

            Next

            Return latest

        End Function


        Public Shared Function GetLatestDecision(
            submission As JournalSubmission
        ) As EditorialDecisionEvent

            If submission Is Nothing OrElse
               submission.Decisions Is Nothing Then

                Return Nothing

            End If

            Dim latest As EditorialDecisionEvent =
                Nothing

            For Each decision As EditorialDecisionEvent In
                submission.Decisions

                If decision Is Nothing Then
                    Continue For
                End If

                If latest Is Nothing OrElse
                   decision.DecisionDate.Date >=
                   latest.DecisionDate.Date Then

                    latest =
                        decision

                End If

            Next

            Return latest

        End Function


        Public Shared Function IsRejectionDecision(
            decision As EditorialDecision
        ) As Boolean

            Select Case decision

                Case EditorialDecision.Rejected,
                     EditorialDecision.DeskRejected,
                     EditorialDecision.RejectedAfterReview

                    Return True

                Case Else
                    Return False

            End Select

        End Function

    End Class

End Namespace
