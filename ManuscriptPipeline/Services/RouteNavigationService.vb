Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum ManuscriptDetailsSection
        Manuscript
        VersionHistory
        JournalSubmissions
    End Enum


    Public NotInheritable Class RouteNavigationService

        Private Sub New()
        End Sub


        Public Shared Function SectionForWaypoint(
            waypoint As ManuscriptRouteWaypoint
        ) As ManuscriptDetailsSection

            If waypoint Is Nothing Then
                Throw New ArgumentNullException(NameOf(waypoint))
            End If

            Select Case waypoint.Kind

                Case ManuscriptRouteWaypointKind.Version

                    Return ManuscriptDetailsSection.VersionHistory

                Case ManuscriptRouteWaypointKind.Submission,
                     ManuscriptRouteWaypointKind.Decision

                    Return ManuscriptDetailsSection.JournalSubmissions

                Case Else

                    Return ManuscriptDetailsSection.Manuscript

            End Select

        End Function

    End Class

End Namespace
