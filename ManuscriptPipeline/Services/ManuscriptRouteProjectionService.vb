Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptRouteProjectionService

        Private Sub New()
        End Sub


        Public Shared Function Project(
            manuscript As Manuscript
        ) As ManuscriptRoute

            If manuscript Is Nothing Then

                Throw New ArgumentNullException(
                    NameOf(manuscript)
                )

            End If

            Dim route As New ManuscriptRoute With {
                .ManuscriptId = manuscript.Id,
                .Title = If(
                    manuscript.Title,
                    String.Empty
                )
            }

            Dim history As List(Of HistoryEvent) =
                If(
                    manuscript.History,
                    New List(Of HistoryEvent)()
                )

            Dim submissions As List(Of JournalSubmission) =
                If(
                    manuscript.Submissions,
                    New List(Of JournalSubmission)()
                )

            Dim versions As List(Of ManuscriptVersion) =
                If(
                    manuscript.Versions,
                    New List(Of ManuscriptVersion)()
                )

            Dim submissionWaypoints As New Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            )()

            Dim decisionWaypoints As New Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            )()

            AddHistoryWaypoints(
                route,
                history,
                submissions
            )

            AddSubmissionAndDecisionWaypoints(
                route,
                submissions,
                submissionWaypoints,
                decisionWaypoints
            )

            AttachVersionWaypoints(
                route,
                manuscript,
                versions,
                submissionWaypoints,
                decisionWaypoints
            )

            AddFileDrawerWaypoint(
                route,
                manuscript
            )

            AddOrMarkCurrentState(
                route,
                manuscript
            )

            MarkRerouteSources(
                route
            )

            SortRelatedVersions(
                route
            )

            route.Waypoints.Sort(
                AddressOf CompareWaypoints
            )

            Return route

        End Function


        Private Shared Sub AddHistoryWaypoints(
            route As ManuscriptRoute,
            history As List(Of HistoryEvent),
            submissions As List(Of JournalSubmission)
        )

            For Each historyEvent As HistoryEvent In history

                If historyEvent Is Nothing Then
                    Continue For
                End If

                If IsRedundantWorkflowStage(
                    historyEvent,
                    submissions
                ) Then

                    Continue For

                End If

                route.Waypoints.Add(
                    New ManuscriptRouteWaypoint With {
                        .Kind =
                            ManuscriptRouteWaypointKind.Stage,
                        .EventDate =
                            historyEvent.EventDate,
                        .RecordedAtUtc =
                            historyEvent.RecordedAtUtc,
                        .LastModifiedAtUtc =
                            historyEvent.LastModifiedAtUtc,
                        .Stage =
                            historyEvent.Stage,
                        .HistoryEventId =
                            historyEvent.Id,
                        .Note =
                            If(
                                historyEvent.Note,
                                String.Empty
                            ),
                        .ProjectionOrder =
                            route.Waypoints.Count
                    }
                )

            Next

        End Sub


        Private Shared Function IsRedundantWorkflowStage(
            historyEvent As HistoryEvent,
            submissions As List(Of JournalSubmission)
        ) As Boolean

            Dim note As String =
                If(
                    historyEvent.Note,
                    String.Empty
                ).Trim()

            If Not note.StartsWith(
                "Stage changed from ",
                StringComparison.OrdinalIgnoreCase
            ) Then

                Return False

            End If

            Select Case historyEvent.Stage

                Case PaperStage.Submitted

                    For Each submission As JournalSubmission In submissions

                        If submission IsNot Nothing AndAlso
                           submission.SubmittedDate.Date =
                           historyEvent.EventDate.Date Then

                            Return True

                        End If

                    Next

                Case PaperStage.Revision

                    Return HasMatchingDecision(
                        submissions,
                        historyEvent.EventDate.Date,
                        Function(decision As EditorialDecision) As Boolean

                            Return decision =
                                EditorialDecision.MajorRevision OrElse
                                decision =
                                EditorialDecision.MinorRevision OrElse
                                decision =
                                EditorialDecision.ReviseAndResubmit

                        End Function
                    )

                Case PaperStage.Accepted

                    Return HasMatchingDecision(
                        submissions,
                        historyEvent.EventDate.Date,
                        Function(decision As EditorialDecision) As Boolean

                            Return decision =
                                EditorialDecision.Accepted

                        End Function
                    )

            End Select

            Return False

        End Function


        Private Shared Function HasMatchingDecision(
            submissions As List(Of JournalSubmission),
            eventDate As DateTime,
            predicate As Func(Of EditorialDecision, Boolean)
        ) As Boolean

            For Each submission As JournalSubmission In submissions

                If submission Is Nothing OrElse
                   submission.Decisions Is Nothing Then

                    Continue For

                End If

                For Each decisionEvent As EditorialDecisionEvent In
                    submission.Decisions

                    If decisionEvent IsNot Nothing AndAlso
                       decisionEvent.DecisionDate.Date =
                       eventDate.Date AndAlso
                       predicate(
                           decisionEvent.Decision
                       ) Then

                        Return True

                    End If

                Next

            Next

            Return False

        End Function


        Private Shared Sub AddSubmissionAndDecisionWaypoints(
            route As ManuscriptRoute,
            submissions As List(Of JournalSubmission),
            submissionWaypoints As Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            ),
            decisionWaypoints As Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            )
        )

            For Each submission As JournalSubmission In submissions

                If submission Is Nothing Then
                    Continue For
                End If

                Dim submissionWaypoint As New ManuscriptRouteWaypoint With {
                    .Kind =
                        ManuscriptRouteWaypointKind.Submission,
                    .EventDate =
                        submission.SubmittedDate,
                    .RecordedAtUtc =
                        submission.RecordedAtUtc,
                    .LastModifiedAtUtc =
                        submission.LastModifiedAtUtc,
                    .SubmissionId =
                        submission.Id,
                    .JournalName =
                        If(
                            submission.JournalName,
                            String.Empty
                        ),
                    .Note =
                        If(
                            submission.Notes,
                            String.Empty
                        ),
                    .ProjectionOrder =
                        route.Waypoints.Count
                }

                route.Waypoints.Add(
                    submissionWaypoint
                )

                If submission.Id <> Guid.Empty AndAlso
                   Not submissionWaypoints.ContainsKey(
                       submission.Id
                   ) Then

                    submissionWaypoints.Add(
                        submission.Id,
                        submissionWaypoint
                    )

                End If

                Dim decisions As List(Of EditorialDecisionEvent) =
                    If(
                        submission.Decisions,
                        New List(Of EditorialDecisionEvent)()
                    )

                For Each decisionEvent As EditorialDecisionEvent In decisions

                    If decisionEvent Is Nothing Then
                        Continue For
                    End If

                    Dim decisionWaypoint As New ManuscriptRouteWaypoint With {
                        .Kind =
                            ManuscriptRouteWaypointKind.Decision,
                        .EventDate =
                            decisionEvent.DecisionDate,
                        .RecordedAtUtc =
                            decisionEvent.RecordedAtUtc,
                        .LastModifiedAtUtc =
                            decisionEvent.LastModifiedAtUtc,
                        .SubmissionId =
                            submission.Id,
                        .DecisionId =
                            decisionEvent.Id,
                        .JournalName =
                            If(
                                submission.JournalName,
                                String.Empty
                            ),
                        .Decision =
                            decisionEvent.Decision,
                        .Note =
                            If(
                                decisionEvent.Notes,
                                String.Empty
                            ),
                        .ProjectionOrder =
                            route.Waypoints.Count
                    }

                    route.Waypoints.Add(
                        decisionWaypoint
                    )

                    If decisionEvent.Id <> Guid.Empty AndAlso
                       Not decisionWaypoints.ContainsKey(
                           decisionEvent.Id
                       ) Then

                        decisionWaypoints.Add(
                            decisionEvent.Id,
                            decisionWaypoint
                        )

                    End If

                Next

            Next

        End Sub


        Private Shared Sub AttachVersionWaypoints(
            route As ManuscriptRoute,
            manuscript As Manuscript,
            versions As List(Of ManuscriptVersion),
            submissionWaypoints As Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            ),
            decisionWaypoints As Dictionary(
                Of Guid,
                ManuscriptRouteWaypoint
            )
        )

            For Each version As ManuscriptVersion In versions

                If version Is Nothing Then
                    Continue For
                End If

                Dim attached As Boolean =
                    False

                If version.DecisionId.HasValue Then

                    Dim decisionWaypoint As ManuscriptRouteWaypoint =
                        Nothing

                    If decisionWaypoints.TryGetValue(
                        version.DecisionId.Value,
                        decisionWaypoint
                    ) Then

                        AddRelatedVersion(
                            decisionWaypoint,
                            version,
                            manuscript.CurrentVersionId
                        )

                        attached =
                            True

                    End If

                End If

                If Not attached AndAlso
                   version.SubmissionId.HasValue Then

                    Dim submissionWaypoint As ManuscriptRouteWaypoint =
                        Nothing

                    If submissionWaypoints.TryGetValue(
                        version.SubmissionId.Value,
                        submissionWaypoint
                    ) Then

                        AddRelatedVersion(
                            submissionWaypoint,
                            version,
                            manuscript.CurrentVersionId
                        )

                        attached =
                            True

                    End If

                End If

                If attached Then
                    Continue For
                End If

                route.Waypoints.Add(
                    New ManuscriptRouteWaypoint With {
                        .Kind =
                            ManuscriptRouteWaypointKind.Version,
                        .EventDate =
                            version.CreatedDate,
                        .RecordedAtUtc =
                            version.RecordedAtUtc,
                        .LastModifiedAtUtc =
                            version.LastModifiedAtUtc,
                        .SubmissionId =
                            version.SubmissionId,
                        .DecisionId =
                            version.DecisionId,
                        .VersionId =
                            version.Id,
                        .RevisionRoundNumber =
                            version.RevisionRoundNumber,
                        .Note =
                            If(
                                version.Notes,
                                String.Empty
                            ),
                        .IsCurrent =
                            manuscript.CurrentVersionId.HasValue AndAlso
                            manuscript.CurrentVersionId.Value =
                            version.Id,
                        .ContainsCurrentVersion =
                            manuscript.CurrentVersionId.HasValue AndAlso
                            manuscript.CurrentVersionId.Value =
                            version.Id,
                        .HasUnresolvedLink =
                            version.SubmissionId.HasValue OrElse
                            version.DecisionId.HasValue,
                        .ProjectionOrder =
                            route.Waypoints.Count
                    }
                )

            Next

        End Sub


        Private Shared Sub AddRelatedVersion(
            waypoint As ManuscriptRouteWaypoint,
            version As ManuscriptVersion,
            currentVersionId As Guid?
        )

            waypoint.RelatedVersionIds.Add(
                version.Id
            )

            If currentVersionId.HasValue AndAlso
               currentVersionId.Value =
               version.Id Then

                waypoint.ContainsCurrentVersion =
                    True

            End If

            If version.RevisionRoundNumber.HasValue Then

                If Not waypoint.RevisionRoundNumber.HasValue OrElse
                   version.RevisionRoundNumber.Value >
                   waypoint.RevisionRoundNumber.Value Then

                    waypoint.RevisionRoundNumber =
                        version.RevisionRoundNumber

                End If

            End If

        End Sub


        Private Shared Sub AddFileDrawerWaypoint(
            route As ManuscriptRoute,
            manuscript As Manuscript
        )

            If manuscript.Location <>
               ManuscriptLocation.FileDrawer OrElse
               Not manuscript.FileDrawerDate.HasValue Then

                Return

            End If

            route.Waypoints.Add(
                New ManuscriptRouteWaypoint With {
                    .Kind =
                        ManuscriptRouteWaypointKind.FileDrawer,
                    .EventDate =
                        manuscript.FileDrawerDate.Value,
                    .Location =
                        ManuscriptLocation.FileDrawer,
                    .Note =
                        If(
                            manuscript.FileDrawerReason,
                            String.Empty
                        ),
                    .ProjectionOrder =
                        route.Waypoints.Count
                }
            )

        End Sub


        Private Shared Sub AddOrMarkCurrentState(
            route As ManuscriptRoute,
            manuscript As Manuscript
        )

            Dim matchingHistory As ManuscriptRouteWaypoint =
                Nothing

            For Each waypoint As ManuscriptRouteWaypoint In
                route.Waypoints

                If waypoint.Kind <>
                   ManuscriptRouteWaypointKind.Stage OrElse
                   Not waypoint.Stage.HasValue Then

                    Continue For

                End If

                If waypoint.Stage.Value =
                   manuscript.CurrentStage AndAlso
                   waypoint.EventDate =
                   manuscript.StageEnteredDate Then

                    If matchingHistory Is Nothing OrElse
                       CompareStableKeys(
                           waypoint,
                           matchingHistory
                       ) < 0 Then

                        matchingHistory =
                            waypoint

                    End If

                End If

            Next

            If matchingHistory IsNot Nothing Then

                matchingHistory.IsCurrent =
                    True

                matchingHistory.Location =
                    manuscript.Location

                Return

            End If

            route.Waypoints.Add(
                New ManuscriptRouteWaypoint With {
                    .Kind =
                        ManuscriptRouteWaypointKind.CurrentState,
                    .EventDate =
                        manuscript.StageEnteredDate,
                    .Stage =
                        manuscript.CurrentStage,
                    .Location =
                        manuscript.Location,
                    .IsCurrent =
                        True,
                    .ProjectionOrder =
                        route.Waypoints.Count
                }
            )

        End Sub


        Private Shared Sub MarkRerouteSources(
            route As ManuscriptRoute
        )

            For Each waypoint As ManuscriptRouteWaypoint In
                route.Waypoints

                If waypoint.Kind <>
                   ManuscriptRouteWaypointKind.Decision OrElse
                   Not waypoint.Decision.HasValue OrElse
                   Not ManuscriptAttentionService.
                       IsRejectionDecision(
                           waypoint.Decision.Value
                       ) Then

                    Continue For

                End If

                For Each candidate As ManuscriptRouteWaypoint In
                    route.Waypoints

                    If candidate.Kind =
                       ManuscriptRouteWaypointKind.Submission AndAlso
                       IsLaterWaypoint(
                           candidate,
                           waypoint
                       ) Then

                        waypoint.IsRerouteSource =
                            True

                        Exit For

                    End If

                Next

            Next

        End Sub


        Private Shared Function IsLaterWaypoint(
            candidate As ManuscriptRouteWaypoint,
            reference As ManuscriptRouteWaypoint
        ) As Boolean

            Return CompareChronology(
                candidate,
                reference
            ) > 0

        End Function


        Private Shared Sub SortRelatedVersions(
            route As ManuscriptRoute
        )

            For Each waypoint As ManuscriptRouteWaypoint In
                route.Waypoints

                waypoint.RelatedVersionIds.Sort(
                    Function(
                        leftId As Guid,
                        rightId As Guid
                    ) As Integer

                        Return StringComparer.Ordinal.Compare(
                            leftId.ToString("N"),
                            rightId.ToString("N")
                        )

                    End Function
                )

            Next

        End Sub


        Private Shared Function CompareWaypoints(
            left As ManuscriptRouteWaypoint,
            right As ManuscriptRouteWaypoint
        ) As Integer

            Dim chronologyComparison As Integer =
                CompareChronology(
                    left,
                    right
                )

            If chronologyComparison <> 0 Then
                Return chronologyComparison
            End If

            Return CompareStableKeys(
                left,
                right
            )

        End Function


        Private Shared Function CompareChronology(
            left As ManuscriptRouteWaypoint,
            right As ManuscriptRouteWaypoint
        ) As Integer

            ' Real-world chronology always wins. Audit timestamps are used
            ' only to resolve same-calendar-day ordering when both records
            ' genuinely have provenance. LastModifiedAtUtc is intentionally
            ' ignored so later metadata edits never reshuffle the Route.
            Dim dateComparison As Integer =
                DateTime.Compare(
                    left.EventDate.Date,
                    right.EventDate.Date
                )

            If dateComparison <> 0 Then
                Return dateComparison
            End If

            If left.RecordedAtUtc.HasValue AndAlso
               right.RecordedAtUtc.HasValue Then

                Dim recordedComparison As Integer =
                    DateTime.Compare(
                        left.RecordedAtUtc.Value,
                        right.RecordedAtUtc.Value
                    )

                If recordedComparison <> 0 Then
                    Return recordedComparison
                End If

            End If

            Return left.ProjectionOrder.CompareTo(
                right.ProjectionOrder
            )

        End Function


        Private Shared Function CompareStableKeys(
            left As ManuscriptRouteWaypoint,
            right As ManuscriptRouteWaypoint
        ) As Integer

            Return StringComparer.Ordinal.Compare(
                StableKey(
                    left
                ),
                StableKey(
                    right
                )
            )

        End Function


        Private Shared Function StableKey(
            waypoint As ManuscriptRouteWaypoint
        ) As String

            If waypoint.HistoryEventId.HasValue Then
                Return "H:" &
                    waypoint.HistoryEventId.Value.ToString("N")
            End If

            If waypoint.DecisionId.HasValue Then
                Return "D:" &
                    waypoint.DecisionId.Value.ToString("N")
            End If

            If waypoint.SubmissionId.HasValue Then
                Return "S:" &
                    waypoint.SubmissionId.Value.ToString("N")
            End If

            If waypoint.VersionId.HasValue Then
                Return "V:" &
                    waypoint.VersionId.Value.ToString("N")
            End If

            Return "Z:" &
                CInt(waypoint.Kind).ToString("D2") &
                ":" &
                If(
                    waypoint.Stage.HasValue,
                    CInt(
                        waypoint.Stage.Value
                    ).ToString("D2"),
                    "NA"
                ) &
                ":" &
                If(
                    waypoint.Location.HasValue,
                    CInt(
                        waypoint.Location.Value
                    ).ToString("D2"),
                    "NA"
                )

        End Function

    End Class

End Namespace
