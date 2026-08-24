Imports System
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SameDayWorkflowTests

    <TestMethod>
    Public Sub Attention_SameDayLaterDecisionControlsRecentRejection()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Same Day Journal",
            .SubmittedDate = eventDate
        }

        Dim revision As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.MajorRevision,
            .RevisionDeadline = eventDate.AddDays(30)
        }

        Dim rejection As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(
            revision
        )

        submission.Decisions.Add(
            rejection
        )

        manuscript.Submissions.Add(
            submission
        )

        Assert.AreSame(
            rejection,
            ManuscriptAttentionService.GetLatestDecision(
                submission
            )
        )

        Assert.IsTrue(
            ManuscriptAttentionService.WasRecentlyRejected(
                manuscript,
                eventDate,
                30
            )
        )

    End Sub


    <TestMethod>
    Public Sub Attention_SameDayLaterSubmissionControlsResult()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim rejectedSubmission As New JournalSubmission With {
            .JournalName = "First Journal",
            .SubmittedDate = eventDate
        }

        rejectedSubmission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = eventDate,
                .Decision = EditorialDecision.Rejected
            }
        )

        Dim laterSubmission As New JournalSubmission With {
            .JournalName = "Second Journal",
            .SubmittedDate = eventDate
        }

        manuscript.Submissions.Add(
            rejectedSubmission
        )

        manuscript.Submissions.Add(
            laterSubmission
        )

        Assert.AreSame(
            laterSubmission,
            ManuscriptAttentionService.GetLatestSubmission(
                manuscript
            )
        )

        Assert.IsFalse(
            ManuscriptAttentionService.WasRecentlyRejected(
                manuscript,
                eventDate,
                30
            )
        )

    End Sub


    <TestMethod>
    Public Sub Route_SameDayWorkflowKeepsRecordedSequenceAndCurrentLast()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate.AddHours(10),
                .Stage = PaperStage.Submitted,
                .Note = "Manuscript added to PaperRoute."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Same Day Journal",
            .SubmittedDate = eventDate
        }

        Dim revision As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.MajorRevision
        }

        Dim rejection As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(
            revision
        )

        submission.Decisions.Add(
            rejection
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.AreEqual(
            3,
            route.Waypoints.Count
        )

        Assert.AreEqual(
            ManuscriptRouteWaypointKind.Submission,
            route.Waypoints(0).Kind
        )

        Assert.AreEqual(
            revision.Id,
            route.Waypoints(1).DecisionId.Value
        )

        Assert.AreEqual(
            rejection.Id,
            route.Waypoints(2).DecisionId.Value
        )

        Assert.AreEqual(
            EditorialDecision.Rejected,
            route.Waypoints(2).Decision.Value
        )

        Assert.IsTrue(
            route.Waypoints(2).IsCurrent
        )

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage OrElse
                        item.Kind =
                        ManuscriptRouteWaypointKind.CurrentState
                End Function
            )
        )

    End Sub


    <TestMethod>
    Public Sub Route_SameDayLaterSubmissionMarksRejectionAsReroute()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = eventDate
        }

        Dim firstSubmission As New JournalSubmission With {
            .JournalName = "First Journal",
            .SubmittedDate = eventDate
        }

        Dim rejection As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.Rejected
        }

        firstSubmission.Decisions.Add(
            rejection
        )

        Dim secondSubmission As New JournalSubmission With {
            .JournalName = "Second Journal",
            .SubmittedDate = eventDate
        }

        manuscript.Submissions.Add(
            firstSubmission
        )

        manuscript.Submissions.Add(
            secondSubmission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim rejectionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.DecisionId.HasValue AndAlso
                        item.DecisionId.Value = rejection.Id
                End Function
            )

        Assert.IsTrue(
            rejectionPoint.IsRerouteSource
        )

    End Sub

End Class
