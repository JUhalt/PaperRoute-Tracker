Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptRouteProjectionTests

    <TestMethod>
    Public Sub Project_EmptyHistoryStillShowsCanonicalCurrentState()

        Dim entered As New DateTime(
            2026,
            8,
            20
        )

        Dim manuscript As New Manuscript With {
            .Title = "Current state only",
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = entered
        }

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.AreEqual(
            1,
            route.Waypoints.Count
        )

        Dim current As ManuscriptRouteWaypoint =
            route.Waypoints(0)

        Assert.AreEqual(
            ManuscriptRouteWaypointKind.CurrentState,
            current.Kind
        )

        Assert.AreEqual(
            PaperStage.Draft,
            current.Stage.Value
        )

        Assert.AreEqual(
            entered,
            current.EventDate
        )

        Assert.IsTrue(
            current.IsCurrent
        )

    End Sub


    <TestMethod>
    Public Sub Project_SortsHistoryChronologicallyWithoutMutatingSourceOrder()

        Dim later As New HistoryEvent With {
            .Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            .EventDate = New DateTime(2026, 2, 1),
            .Stage = PaperStage.Draft
        }

        Dim earlier As New HistoryEvent With {
            .Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            .EventDate = New DateTime(2026, 1, 1),
            .Stage = PaperStage.Idea
        }

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = later.EventDate
        }

        manuscript.History.Add(
            later
        )

        manuscript.History.Add(
            earlier
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim stagePoints As List(Of ManuscriptRouteWaypoint) =
            route.Waypoints.Where(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage
                End Function
            ).ToList()

        Assert.AreEqual(
            earlier.Id,
            stagePoints(0).HistoryEventId.Value
        )

        Assert.AreEqual(
            later.Id,
            stagePoints(1).HistoryEventId.Value
        )

        Assert.AreSame(
            later,
            manuscript.History(0)
        )

        Assert.AreSame(
            earlier,
            manuscript.History(1)
        )

    End Sub


    <TestMethod>
    Public Sub Project_EmitsSubmissionAndDecisionWithJournalLinkage()

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 3, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 4, 1),
            .Decision = EditorialDecision.MajorRevision
        }

        submission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim submissionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Submission
                End Function
            )

        Dim decisionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Decision
                End Function
            )

        Assert.AreEqual(
            submission.Id,
            submissionPoint.SubmissionId.Value
        )

        Assert.AreEqual(
            "Journal A",
            submissionPoint.JournalName
        )

        Assert.AreEqual(
            submission.Id,
            decisionPoint.SubmissionId.Value
        )

        Assert.AreEqual(
            decision.Id,
            decisionPoint.DecisionId.Value
        )

        Assert.AreEqual(
            EditorialDecision.MajorRevision,
            decisionPoint.Decision.Value
        )

    End Sub


    <TestMethod>
    Public Sub Project_RejectionBecomesRerouteOnlyWhenLaterSubmissionExists()

        Dim manuscript As New Manuscript()

        Dim first As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 1, 1)
        }

        Dim rejection As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 2, 1),
            .Decision = EditorialDecision.Rejected
        }

        first.Decisions.Add(
            rejection
        )

        Dim second As New JournalSubmission With {
            .JournalName = "Journal B",
            .SubmittedDate = New DateTime(2026, 3, 1)
        }

        manuscript.Submissions.Add(
            first
        )

        manuscript.Submissions.Add(
            second
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

        second.SubmittedDate =
            New DateTime(2026, 1, 15)

        route =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        rejectionPoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.DecisionId.HasValue AndAlso
                        item.DecisionId.Value = rejection.Id
                End Function
            )

        Assert.IsFalse(
            rejectionPoint.IsRerouteSource
        )

    End Sub


    <TestMethod>
    Public Sub Project_DecisionLinkedVersionDecoratesDecisionWaypoint()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MinorRevision
        }

        submission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid(),
            .DecisionId = decision.Id,
            .SubmissionId = submission.Id,
            .RevisionRoundNumber = 2
        }

        manuscript.Versions.Add(
            version
        )

        manuscript.CurrentVersionId =
            version.Id

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim decisionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.DecisionId.HasValue AndAlso
                        item.DecisionId.Value = decision.Id
                End Function
            )

        Assert.IsTrue(
            decisionPoint.RelatedVersionIds.Contains(
                version.Id
            )
        )

        Assert.IsTrue(
            decisionPoint.ContainsCurrentVersion
        )

        Assert.AreEqual(
            2,
            decisionPoint.RevisionRoundNumber.Value
        )

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Version AndAlso
                        item.VersionId.HasValue AndAlso
                        item.VersionId.Value = version.Id
                End Function
            )
        )

    End Sub


    <TestMethod>
    Public Sub Project_SubmissionLinkedVersionDecoratesSubmissionWaypoint()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()

        manuscript.Submissions.Add(
            submission
        )

        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid(),
            .SubmissionId = submission.Id
        }

        manuscript.Versions.Add(
            version
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim submissionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.SubmissionId.HasValue AndAlso
                        item.SubmissionId.Value = submission.Id AndAlso
                        item.Kind = ManuscriptRouteWaypointKind.Submission
                End Function
            )

        Assert.IsTrue(
            submissionPoint.RelatedVersionIds.Contains(
                version.Id
            )
        )

    End Sub


    <TestMethod>
    Public Sub Project_UnresolvedHistoricalVersionRemainsVisibleAndFlagged()

        Dim manuscript As New Manuscript()

        Dim version As New ManuscriptVersion With {
            .Id = Guid.NewGuid(),
            .CreatedDate = New DateTime(2024, 5, 1),
            .SubmissionId = Guid.NewGuid(),
            .DecisionId = Guid.NewGuid()
        }

        manuscript.Versions.Add(
            version
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim versionPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Version
                End Function
            )

        Assert.AreEqual(
            version.Id,
            versionPoint.VersionId.Value
        )

        Assert.IsTrue(
            versionPoint.HasUnresolvedLink
        )

    End Sub


    <TestMethod>
    Public Sub Project_ExactCurrentHistoryPointIsMarkedCurrentWithoutDuplicate()

        Dim entered As New DateTime(
            2026,
            6,
            15
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = entered
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = entered,
                .Stage = PaperStage.UnderReview
            }
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.AreEqual(
            0,
            route.Waypoints.Where(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.CurrentState
                End Function
            ).Count()
        )

        Dim currentStages As List(Of ManuscriptRouteWaypoint) =
            route.Waypoints.Where(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.IsCurrent
                End Function
            ).ToList()

        Assert.AreEqual(
            1,
            currentStages.Count
        )

        Assert.AreEqual(
            PaperStage.UnderReview,
            currentStages(0).Stage.Value
        )

    End Sub


    <TestMethod>
    Public Sub Project_FileDrawerUsesExplicitStoredDateAndReason()

        Dim drawerDate As New DateTime(
            2025,
            10,
            12
        )

        Dim manuscript As New Manuscript With {
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = drawerDate,
            .FileDrawerReason = "Paused after repeated rejections."
        }

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim drawerPoint As ManuscriptRouteWaypoint =
            route.Waypoints.Single(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.FileDrawer
                End Function
            )

        Assert.AreEqual(
            drawerDate,
            drawerPoint.EventDate
        )

        Assert.AreEqual(
            "Paused after repeated rejections.",
            drawerPoint.Note
        )

        Assert.AreEqual(
            ManuscriptLocation.FileDrawer,
            drawerPoint.Location.Value
        )

    End Sub


    <TestMethod>
    Public Sub Project_SameSourceDataProducesSameDeterministicOrder()

        Dim manuscript As New Manuscript With {
            .StageEnteredDate = New DateTime(2026, 8, 1)
        }

        Dim sharedDate As New DateTime(
            2026,
            7,
            1
        )

        manuscript.History.Add(
            New HistoryEvent With {
                .Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                .EventDate = sharedDate,
                .Stage = PaperStage.Draft
            }
        )

        manuscript.History.Add(
            New HistoryEvent With {
                .Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                .EventDate = sharedDate,
                .Stage = PaperStage.Idea
            }
        )

        Dim first As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim second As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim firstSignature As String =
            String.Join(
                "|",
                first.Waypoints.Select(
                    Function(item)
                        Return Signature(
                            item
                        )
                    End Function
                )
            )

        Dim secondSignature As String =
            String.Join(
                "|",
                second.Waypoints.Select(
                    Function(item)
                        Return Signature(
                            item
                        )
                    End Function
                )
            )

        Assert.AreEqual(
            firstSignature,
            secondSignature
        )

        Assert.IsTrue(
            firstSignature.Contains(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        )

    End Sub


    Private Function Signature(
        waypoint As ManuscriptRouteWaypoint
    ) As String

        Return CInt(
            waypoint.Kind
        ).ToString() &
            ":" &
            waypoint.EventDate.ToString("O") &
            ":" &
            If(
                waypoint.HistoryEventId.HasValue,
                waypoint.HistoryEventId.Value.ToString("N"),
                String.Empty
            ) &
            ":" &
            If(
                waypoint.SubmissionId.HasValue,
                waypoint.SubmissionId.Value.ToString("N"),
                String.Empty
            ) &
            ":" &
            If(
                waypoint.DecisionId.HasValue,
                waypoint.DecisionId.Value.ToString("N"),
                String.Empty
            ) &
            ":" &
            If(
                waypoint.VersionId.HasValue,
                waypoint.VersionId.Value.ToString("N"),
                String.Empty
            )

    End Function

End Class
