Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class WorkflowPresentationTests

    <TestMethod>
    Public Sub CurrentSubmissionExplainsSubmittedState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted
        }

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = ManuscriptRouteWaypointKind.Submission,
            .IsCurrent = True
        }

        Assert.AreEqual(
            "Resulting state: Submitted",
            WorkflowPresentationService.ResultingStateText(
                manuscript,
                waypoint
            )
        )

    End Sub


    <TestMethod>
    Public Sub CurrentRevisionDecisionExplainsRevisionState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision
        }

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = ManuscriptRouteWaypointKind.Decision,
            .Decision = EditorialDecision.MajorRevision,
            .IsCurrent = True
        }

        Assert.AreEqual(
            "Resulting state: Revision",
            WorkflowPresentationService.ResultingStateText(
                manuscript,
                waypoint
            )
        )

    End Sub


    <TestMethod>
    Public Sub CurrentWithdrawalDecisionExplainsDraftState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft
        }

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = ManuscriptRouteWaypointKind.Decision,
            .Decision = EditorialDecision.Withdrawn,
            .IsCurrent = True
        }

        Assert.AreEqual(
            "Resulting state: Draft",
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
