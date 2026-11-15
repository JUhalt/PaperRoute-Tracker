Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class WorkflowPresentationService

        Private Sub New()
        End Sub


        Public Shared Function ResultingStateText(
            manuscript As Manuscript,
            waypoint As ManuscriptRouteWaypoint
        ) As String

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If waypoint Is Nothing Then
                Throw New ArgumentNullException(NameOf(waypoint))
            End If

            If Not waypoint.IsCurrent Then
                Return String.Empty
            End If

            Select Case waypoint.Kind

                Case ManuscriptRouteWaypointKind.Submission,
                     ManuscriptRouteWaypointKind.Decision

                    Return "Resulting state: " &
                        FormatStage(
                            manuscript.CurrentStage
                        )

                Case Else

                    Return String.Empty

            End Select

        End Function


        Public Shared Function FormatStage(
            stage As PaperStage
        ) As String

            Select Case stage

                Case PaperStage.UnderReview
                    Return "Under Review"

                Case PaperStage.InPress
                    Return "In Press"

                Case Else
                    Return stage.ToString()

            End Select

        End Function

    End Class

End Namespace
