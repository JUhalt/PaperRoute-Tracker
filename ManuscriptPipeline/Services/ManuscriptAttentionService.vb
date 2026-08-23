Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptAttentionService

        Private Sub New()
        End Sub


        ' =====================================================
        ' Overdue revision
        ' =====================================================

        Public Shared Function HasOverdueRevision(
            manuscript As Manuscript,
            today As DateTime
        ) As Boolean

            If manuscript Is Nothing Then
                Return False
            End If

            If manuscript.Location <>
               ManuscriptLocation.Pipeline Then

                Return False

            End If

            If manuscript.CurrentStage <>
               PaperStage.Revision Then

                Return False

            End If

            Dim latestSubmission As JournalSubmission =
                GetLatestSubmission(
                    manuscript
                )

            Dim latestDecision As EditorialDecisionEvent =
                GetLatestDecision(
                    latestSubmission
                )

            If latestDecision Is Nothing OrElse
               Not latestDecision.RevisionDeadline.HasValue Then

                Return False

            End If

            Return latestDecision.
                RevisionDeadline.
                Value.
                Date <
                today.Date

        End Function


        ' =====================================================
        ' Revision due soon
        ' =====================================================

        Public Shared Function IsRevisionDueSoon(
            manuscript As Manuscript,
            today As DateTime,
            warningDays As Integer
        ) As Boolean

            If manuscript Is Nothing Then
                Return False
            End If

            If manuscript.Location <>
               ManuscriptLocation.Pipeline Then

                Return False

            End If

            If manuscript.CurrentStage <>
               PaperStage.Revision Then

                Return False

            End If

            Dim latestSubmission As JournalSubmission =
                GetLatestSubmission(
                    manuscript
                )

            Dim latestDecision As EditorialDecisionEvent =
                GetLatestDecision(
                    latestSubmission
                )

            If latestDecision Is Nothing OrElse
               Not latestDecision.RevisionDeadline.HasValue Then

                Return False

            End If

            Dim daysRemaining As Integer =
                CInt(
                    Math.Floor(
                        (
                            latestDecision.
                                RevisionDeadline.
                                Value.
                                Date -
                            today.Date
                        ).TotalDays
                    )
                )

            Return daysRemaining >= 0 AndAlso
                   daysRemaining <= warningDays

        End Function


        ' =====================================================
        ' Long review / waiting period
        ' =====================================================

        Public Shared Function IsLongWaitingManuscript(
            manuscript As Manuscript,
            today As DateTime,
            thresholdDays As Integer
        ) As Boolean

            If manuscript Is Nothing Then
                Return False
            End If

            If manuscript.Location <>
               ManuscriptLocation.Pipeline Then

                Return False

            End If

            If manuscript.CurrentStage <>
               PaperStage.Submitted AndAlso
               manuscript.CurrentStage <>
               PaperStage.UnderReview Then

                Return False

            End If

            Dim latestSubmission As JournalSubmission =
                GetLatestSubmission(
                    manuscript
                )

            If latestSubmission Is Nothing Then
                Return False
            End If

            Dim waitingDays As Integer =
                CInt(
                    Math.Floor(
                        (
                            today.Date -
                            latestSubmission.
                                SubmittedDate.
                                Date
                        ).TotalDays
                    )
                )

            Return waitingDays >= thresholdDays

        End Function


        ' =====================================================
        ' Missing target journal
        ' =====================================================

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


        ' =====================================================
        ' Recent rejection
        ' =====================================================

        Public Shared Function WasRecentlyRejected(
            manuscript As Manuscript,
            today As DateTime,
            thresholdDays As Integer
        ) As Boolean

            If manuscript Is Nothing Then
                Return False
            End If

            Dim latestSubmission As JournalSubmission =
                GetLatestSubmission(
                    manuscript
                )

            Dim latestDecision As EditorialDecisionEvent =
                GetLatestDecision(
                    latestSubmission
                )

            If latestDecision Is Nothing Then
                Return False
            End If

            Select Case latestDecision.Decision

                Case EditorialDecision.Rejected,
                     EditorialDecision.DeskRejected,
                     EditorialDecision.RejectedAfterReview

                Case Else

                    Return False

            End Select

            Dim daysAgo As Integer =
                CInt(
                    Math.Floor(
                        (
                            today.Date -
                            latestDecision.
                                DecisionDate.
                                Date
                        ).TotalDays
                    )
                )

            Return daysAgo >= 0 AndAlso
                   daysAgo <= thresholdDays

        End Function


        ' =====================================================
        ' Latest route helpers
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

                    ' Submission dates are calendar dates. If two entries
                    ' share a date, the later stored entry represents the
                    ' later workflow event.
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

                    ' Decision dates are calendar dates. If two decisions
                    ' share a date, the later stored decision wins.
                    latest =
                        decision

                End If

            Next

            Return latest

        End Function

    End Class

End Namespace
