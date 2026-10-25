Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class WorkflowPresentationTests

    ' A current submission (no decision), a current major-revision decision
    ' and a current withdrawal each state the manuscript's current stage,
    ' one row each.
    <TestMethod>
    <DataRow(ManuscriptRouteWaypointKind.Submission, Nothing, PaperStage.Submitted, "Resulting state: Submitted")>
    <DataRow(ManuscriptRouteWaypointKind.Decision, EditorialDecision.MajorRevision, PaperStage.Revision, "Resulting state: Revision")>
    <DataRow(ManuscriptRouteWaypointKind.Decision, EditorialDecision.Withdrawn, PaperStage.Draft, "Resulting state: Draft")>
    Public Sub CurrentWaypointExplainsTheResultingState(
        kind As ManuscriptRouteWaypointKind,
        decision As EditorialDecision?,
        stage As PaperStage,
        expected As String
    )

        Dim manuscript As New Manuscript With {
            .CurrentStage = stage
        }

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = kind,
            .Decision = decision,
            .IsCurrent = True
        }

        Assert.AreEqual(
            expected,
            WorkflowPresentationService.ResultingStateText(
                manuscript,
                waypoint
            )
        )

    End Sub


    <TestMethod>
    Public Sub HistoricalWorkflowEventDoesNotClaimPresentState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision
        }

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = ManuscriptRouteWaypointKind.Submission,
            .IsCurrent = False
        }

        Assert.AreEqual(
            String.Empty,
            WorkflowPresentationService.ResultingStateText(
                manuscript,
                waypoint
            )
        )

    End Sub

End Class
