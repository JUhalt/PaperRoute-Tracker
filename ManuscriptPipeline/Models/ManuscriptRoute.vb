Imports System
Imports System.Collections.Generic

Namespace Models

    Public Enum ManuscriptRouteWaypointKind
        Stage
        Submission
        Decision
        Version
        CurrentState
        FileDrawer
    End Enum


    Public Class ManuscriptRouteWaypoint

        Public Property Kind As ManuscriptRouteWaypointKind

        Public Property EventDate As DateTime

        Public Property Stage As PaperStage? = Nothing

        Public Property Location As ManuscriptLocation? = Nothing

        Public Property SubmissionId As Guid? = Nothing

        Public Property DecisionId As Guid? = Nothing

        Public Property VersionId As Guid? = Nothing

        Public Property HistoryEventId As Guid? = Nothing

        Public Property JournalName As String = String.Empty

        Public Property Decision As EditorialDecision? = Nothing

        Public Property RevisionRoundNumber As Integer? = Nothing

        Public Property Note As String = String.Empty

        Public Property IsCurrent As Boolean = False

        Public Property ContainsCurrentVersion As Boolean = False

        Public Property IsRerouteSource As Boolean = False

        Public Property HasUnresolvedLink As Boolean = False

        Public Property RelatedVersionIds As List(Of Guid) =
            New List(Of Guid)()

    End Class


    Public Class ManuscriptRoute

        Public Property ManuscriptId As Guid

        Public Property Title As String = String.Empty

        Public Property Waypoints As List(Of ManuscriptRouteWaypoint) =
            New List(Of ManuscriptRouteWaypoint)()

    End Class

End Namespace
