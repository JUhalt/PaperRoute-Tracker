Imports System
Imports System.Drawing
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class RoutePolishFoundationTests

    <TestMethod>
    Public Sub ResponsiveDialogSizing_UsesDesiredSizeOnLargeDisplay()

        Dim actual As Size =
            ResponsiveDialogSizingService.CalculateInitialSize(
                New Rectangle(0, 0, 1920, 1080),
                New Size(1180, 940),
                New Size(860, 760),
                72
            )

        Assert.AreEqual(
            New Size(1180, 940),
            actual
        )

    End Sub


    <TestMethod>
    Public Sub ResponsiveDialogSizing_CapsToSmallWorkingArea()

        Dim actual As Size =
            ResponsiveDialogSizingService.CalculateInitialSize(
                New Rectangle(0, 0, 1000, 800),
                New Size(1180, 940),
                New Size(860, 760),
                72
            )

        Assert.AreEqual(
            New Size(928, 728),
            actual
        )

    End Sub


    <TestMethod>
    Public Sub ResponsiveDialogSizing_CentersInsideWorkingArea()

        Dim actual As Point =
            ResponsiveDialogSizingService.CalculateCenteredLocation(
                New Rectangle(100, 50, 1600, 900),
                New Size(1000, 700)
            )

        Assert.AreEqual(
            New Point(400, 150),
            actual
        )

    End Sub


    <TestMethod>
    Public Sub RouteNavigation_VersionTargetsVersionHistory()

        Dim waypoint As New ManuscriptRouteWaypoint With {
            .Kind = ManuscriptRouteWaypointKind.Version
        }

        Assert.AreEqual(
            ManuscriptDetailsSection.VersionHistory,
            RouteNavigationService.SectionForWaypoint(
                waypoint
            )
        )

    End Sub


    <TestMethod>
    Public Sub RouteNavigation_SubmissionAndDecisionTargetJournalSubmissions()

        For Each kind As ManuscriptRouteWaypointKind In
            New ManuscriptRouteWaypointKind() {
                ManuscriptRouteWaypointKind.Submission,
                ManuscriptRouteWaypointKind.Decision
            }

            Dim waypoint As New ManuscriptRouteWaypoint With {
                .Kind = kind
            }

            Assert.AreEqual(
                ManuscriptDetailsSection.JournalSubmissions,
                RouteNavigationService.SectionForWaypoint(
                    waypoint
                )
            )

        Next

    End Sub


    <TestMethod>
    Public Sub RouteNavigation_StageAndCurrentStateTargetManuscript()

        For Each kind As ManuscriptRouteWaypointKind In
            New ManuscriptRouteWaypointKind() {
                ManuscriptRouteWaypointKind.Stage,
                ManuscriptRouteWaypointKind.CurrentState,
                ManuscriptRouteWaypointKind.FileDrawer
            }

            Dim waypoint As New ManuscriptRouteWaypoint With {
                .Kind = kind
            }

            Assert.AreEqual(
                ManuscriptDetailsSection.Manuscript,
                RouteNavigationService.SectionForWaypoint(
                    waypoint
                )
            )

        Next

    End Sub

End Class
