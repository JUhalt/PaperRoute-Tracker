Imports System
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class RouteParsimonyRegressionTests

    <TestMethod>
    Public Sub ImportedSubmittedStage_IsCollapsedIntoCurrentSubmission()

        Dim eventDate As New DateTime(2026, 2, 10)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.Submitted,
                .Note = "Imported from standard PaperRoute workbook."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = eventDate
        }

        manuscript.Submissions.Add(submission)

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value = PaperStage.Submitted
                End Function
            )
        )

        Dim submissionWaypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Submission
                End Function
            )

        Assert.IsTrue(submissionWaypoint.IsCurrent)

    End Sub


    <TestMethod>
    Public Sub ImportedRevisionStage_IsCollapsedIntoCurrentDecision()

        Dim decisionDate As New DateTime(2026, 3, 5)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision,
            .StageEnteredDate = decisionDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = decisionDate,
                .Stage = PaperStage.Revision,
                .Note = "Imported from standard PaperRoute workbook."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = decisionDate.AddDays(-10)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = decisionDate,
                .Decision = EditorialDecision.MajorRevision
            }
        )

        manuscript.Submissions.Add(submission)

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value = PaperStage.Revision
                End Function
            )
        )

        Dim decisionWaypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Decision
                End Function
            )

        Assert.IsTrue(decisionWaypoint.IsCurrent)

    End Sub


    <TestMethod>
    Public Sub ImportedDraftAfterWithdrawal_IsCollapsedIntoCurrentDecision()

        Dim decisionDate As New DateTime(2026, 5, 12)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = decisionDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = decisionDate,
                .Stage = PaperStage.Draft,
                .Note = "Imported from standard PaperRoute workbook."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = decisionDate.AddDays(-11)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = decisionDate,
                .Decision = EditorialDecision.Withdrawn
            }
        )

        manuscript.Submissions.Add(submission)

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value = PaperStage.Draft
                End Function
            )
        )

        Dim decisionWaypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Decision
                End Function
            )

        Assert.AreEqual(
            EditorialDecision.Withdrawn,
            decisionWaypoint.Decision.Value
        )

        Assert.IsTrue(decisionWaypoint.IsCurrent)

    End Sub


    <TestMethod>
    Public Sub SameDayRevision_OrdersSubmissionBeforeCurrentDecision()

        Dim eventDate As New DateTime(2026, 6, 15)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.Revision,
                .Note = "Imported from standard PaperRoute workbook."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Same Day Review",
            .SubmittedDate = eventDate
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = eventDate,
                .Decision = EditorialDecision.MajorRevision
            }
        )

        manuscript.Submissions.Add(submission)

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Assert.AreEqual(2, route.Waypoints.Count)

        Assert.AreEqual(
            ManuscriptRouteWaypointKind.Submission,
            route.Waypoints(0).Kind
        )

        Assert.AreEqual(
            ManuscriptRouteWaypointKind.Decision,
            route.Waypoints(1).Kind
        )

        Assert.IsTrue(
            route.Waypoints(1).IsCurrent
        )

    End Sub


    <TestMethod>
    Public Sub CurrentSubmissionCanAlsoContainCurrentVersion()

        Dim eventDate As New DateTime(2026, 2, 10)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = eventDate
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = eventDate
        }

        manuscript.Submissions.Add(submission)

        Dim version As New ManuscriptVersion With {
            .CreatedDate = eventDate,
            .Label = "Submitted Manuscript",
            .SubmissionId = submission.Id
        }

        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Dim waypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Submission
                End Function
            )

        Assert.IsTrue(waypoint.IsCurrent)
        Assert.IsTrue(waypoint.ContainsCurrentVersion)

    End Sub


    <TestMethod>
    Public Sub DraftWithoutWorkflow_RemainsCurrentStageWaypoint()

        Dim eventDate As New DateTime(2026, 1, 5)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.Draft,
                .Note = "Imported from standard PaperRoute workbook."
            }
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(manuscript)

        Assert.AreEqual(1, route.Waypoints.Count)

        Assert.AreEqual(
            ManuscriptRouteWaypointKind.Stage,
            route.Waypoints(0).Kind
        )

        Assert.IsTrue(route.Waypoints(0).IsCurrent)

    End Sub

End Class
