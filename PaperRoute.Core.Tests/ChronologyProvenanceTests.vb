Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ChronologyProvenanceTests

    <TestMethod>
    Public Sub NewChronologyModels_DoNotFabricateProvenance()

        Assert.IsFalse(
            New HistoryEvent().RecordedAtUtc.HasValue
        )

        Assert.IsFalse(
            New JournalSubmission().RecordedAtUtc.HasValue
        )

        Assert.IsFalse(
            New EditorialDecisionEvent().RecordedAtUtc.HasValue
        )

        Assert.IsFalse(
            New ManuscriptVersion().RecordedAtUtc.HasValue
        )

        Assert.IsFalse(
            New CorrespondenceItem().RecordedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub StampCreated_SetsImmutableRecordedTimeOnly()

        Dim stamp As New DateTime(
            2026,
            8,
            23,
            18,
            30,
            0,
            DateTimeKind.Utc
        )

        Dim historyEvent As New HistoryEvent()

        ChronologyProvenanceService.StampCreated(
            historyEvent,
            stamp
        )

        Assert.AreEqual(
            stamp,
            historyEvent.RecordedAtUtc.Value
        )

        Assert.IsFalse(
            historyEvent.LastModifiedAtUtc.HasValue
        )

        Dim later As DateTime =
            stamp.AddHours(2)

        ChronologyProvenanceService.StampCreated(
            historyEvent,
            later
        )

        Assert.AreEqual(
            stamp,
            historyEvent.RecordedAtUtc.Value
        )

    End Sub


    <TestMethod>
    Public Sub StampModified_PreservesRecordedTimeAndTracksEdit()

        Dim recorded As New DateTime(
            2026,
            8,
            23,
            18,
            30,
            0,
            DateTimeKind.Utc
        )

        Dim modified As DateTime =
            recorded.AddDays(4)

        Dim submission As New JournalSubmission()

        ChronologyProvenanceService.StampCreated(
            submission,
            recorded
        )

        ChronologyProvenanceService.StampModified(
            submission,
            modified
        )

        Assert.AreEqual(
            recorded,
            submission.RecordedAtUtc.Value
        )

        Assert.AreEqual(
            modified,
            submission.LastModifiedAtUtc.Value
        )

    End Sub


    <TestMethod>
    Public Sub StampModified_LegacyRecordDoesNotInventRecordedTime()

        Dim modified As New DateTime(
            2026,
            8,
            24,
            12,
            0,
            0,
            DateTimeKind.Utc
        )

        Dim decision As New EditorialDecisionEvent()

        ChronologyProvenanceService.StampModified(
            decision,
            modified
        )

        Assert.IsFalse(
            decision.RecordedAtUtc.HasValue
        )

        Assert.AreEqual(
            modified,
            decision.LastModifiedAtUtc.Value
        )

    End Sub


    <TestMethod>
    Public Sub ImportedManuscript_StampsWholeImportedChronologyOnce()

        Dim stamp As New DateTime(
            2026,
            8,
            23,
            18,
            45,
            0,
            DateTimeKind.Utc
        )

        Dim manuscript As New Manuscript()

        Dim historyEvent As New HistoryEvent()
        manuscript.History.Add(historyEvent)

        Dim version As New ManuscriptVersion()
        manuscript.Versions.Add(version)

        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent()
        Dim correspondence As New CorrespondenceItem()

        submission.Decisions.Add(decision)
        submission.Correspondence.Add(correspondence)
        manuscript.Submissions.Add(submission)

        ChronologyProvenanceService.StampImportedManuscript(
            manuscript,
            stamp
        )

        Assert.AreEqual(stamp, historyEvent.RecordedAtUtc.Value)
        Assert.AreEqual(stamp, version.RecordedAtUtc.Value)
        Assert.AreEqual(stamp, submission.RecordedAtUtc.Value)
        Assert.AreEqual(stamp, decision.RecordedAtUtc.Value)
        Assert.AreEqual(stamp, correspondence.RecordedAtUtc.Value)

    End Sub


    <TestMethod>
    Public Sub VersionService_CreateVersionRecordsAuditProvenance()

        Dim manuscript As New Manuscript()

        Dim before As DateTime =
            DateTime.UtcNow.AddSeconds(-1)

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Working copy",
                String.Empty,
                String.Empty,
                False
            )

        Dim after As DateTime =
            DateTime.UtcNow.AddSeconds(1)

        Assert.IsTrue(
            version.RecordedAtUtc.HasValue
        )

        Assert.IsTrue(
            version.RecordedAtUtc.Value >= before AndAlso
            version.RecordedAtUtc.Value <= after
        )

        Assert.IsFalse(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub VersionService_LinkMutationUpdatesModifiedButNotRecorded()

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2024, 6, 4)
        }

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Submitted draft",
                String.Empty,
                String.Empty,
                False
            )

        Dim recorded As DateTime =
            version.RecordedAtUtc.Value

        ManuscriptVersionService.LinkVersionToSubmission(
            manuscript,
            version.Id,
            submission.Id
        )

        Assert.AreEqual(
            recorded,
            version.RecordedAtUtc.Value
        )

        Assert.IsTrue(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub SetCurrentVersion_DoesNotModifyVersionAuditMetadata()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Historical",
                String.Empty,
                String.Empty,
                False,
                makeCurrent:=False
            )

        Dim recorded As DateTime =
            version.RecordedAtUtc.Value

        Assert.IsFalse(
            version.LastModifiedAtUtc.HasValue
        )

        ManuscriptVersionService.SetCurrentVersion(
            manuscript,
            version.Id
        )

        Assert.AreEqual(
            recorded,
            version.RecordedAtUtc.Value
        )

        Assert.IsFalse(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub CloneService_PreservesChronologyProvenance()

        Dim recorded As New DateTime(
            2025,
            1,
            2,
            15,
            30,
            0,
            DateTimeKind.Utc
        )

        Dim modified As DateTime =
            recorded.AddDays(2)

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission With {
            .RecordedAtUtc = recorded,
            .LastModifiedAtUtc = modified,
            .SubmittedDate = New DateTime(2024, 12, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .RecordedAtUtc = recorded.AddMinutes(1),
            .LastModifiedAtUtc = modified.AddMinutes(1),
            .DecisionDate = New DateTime(2024, 12, 20),
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                manuscript
            )

        Assert.AreEqual(
            submission.RecordedAtUtc,
            clone.Submissions(0).RecordedAtUtc
        )

        Assert.AreEqual(
            submission.LastModifiedAtUtc,
            clone.Submissions(0).LastModifiedAtUtc
        )

        Assert.AreEqual(
            decision.RecordedAtUtc,
            clone.Submissions(0).Decisions(0).RecordedAtUtc
        )

        Assert.AreEqual(
            decision.LastModifiedAtUtc,
            clone.Submissions(0).Decisions(0).LastModifiedAtUtc
        )

    End Sub


    <TestMethod>
    Public Sub Route_SameDayNewRecordsUseRecordedOrder()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim submissionRecorded As New DateTime(
            2026,
            8,
            23,
            17,
            0,
            0,
            DateTimeKind.Utc
        )

        Dim reviewRecorded As DateTime =
            submissionRecorded.AddMinutes(2)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = eventDate
        }

        Dim underReview As New HistoryEvent With {
            .EventDate = eventDate,
            .Stage = PaperStage.UnderReview,
            .Note = "Stage changed from Submitted to UnderReview.",
            .RecordedAtUtc = reviewRecorded
        }

        manuscript.History.Add(
            underReview
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = eventDate,
            .RecordedAtUtc = submissionRecorded
        }

        manuscript.Submissions.Add(
            submission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim submissionIndex As Integer =
            route.Waypoints.FindIndex(
                Function(item)
                    Return item.SubmissionId.HasValue AndAlso
                        item.SubmissionId.Value =
                        submission.Id AndAlso
                        item.Kind =
                        ManuscriptRouteWaypointKind.Submission
                End Function
            )

        Dim reviewIndex As Integer =
            route.Waypoints.FindIndex(
                Function(item)
                    Return item.HistoryEventId.HasValue AndAlso
                        item.HistoryEventId.Value =
                        underReview.Id
                End Function
            )

        Assert.IsTrue(
            submissionIndex >= 0
        )

        Assert.IsTrue(
            reviewIndex > submissionIndex
        )

    End Sub


    <TestMethod>
    Public Sub Route_LastModifiedTimeNeverReordersHistory()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim firstRecorded As New DateTime(
            2026,
            8,
            23,
            17,
            0,
            0,
            DateTimeKind.Utc
        )

        Dim secondRecorded As DateTime =
            firstRecorded.AddMinutes(1)

        Dim first As New HistoryEvent With {
            .EventDate = eventDate,
            .Stage = PaperStage.Idea,
            .Note = "First",
            .RecordedAtUtc = firstRecorded,
            .LastModifiedAtUtc = secondRecorded.AddYears(5)
        }

        Dim second As New HistoryEvent With {
            .EventDate = eventDate,
            .Stage = PaperStage.Draft,
            .Note = "Second",
            .RecordedAtUtc = secondRecorded,
            .LastModifiedAtUtc = firstRecorded
        }

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(first)
        manuscript.History.Add(second)

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim projectedHistory As List(Of ManuscriptRouteWaypoint) =
            route.Waypoints.
                Where(
                    Function(item)
                        Return item.HistoryEventId.HasValue
                    End Function
                ).
                ToList()

        Assert.AreEqual(
            first.Id,
            projectedHistory(0).HistoryEventId.Value
        )

        Assert.AreEqual(
            second.Id,
            projectedHistory(1).HistoryEventId.Value
        )

    End Sub


    <TestMethod>
    Public Sub Route_BackfilledRecordStaysOnRealWorldEventDate()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 23)
        }

        Dim historicalSubmission As New JournalSubmission With {
            .JournalName = "Old Journal",
            .SubmittedDate = New DateTime(2022, 3, 10),
            .RecordedAtUtc =
                New DateTime(
                    2026,
                    8,
                    23,
                    18,
                    0,
                    0,
                    DateTimeKind.Utc
                )
        }

        manuscript.Submissions.Add(
            historicalSubmission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim waypoint As ManuscriptRouteWaypoint =
            route.Waypoints.First(
                Function(item)
                    Return item.SubmissionId.HasValue AndAlso
                        item.SubmissionId.Value =
                        historicalSubmission.Id
                End Function
            )

        Assert.AreEqual(
            New DateTime(2022, 3, 10),
            waypoint.EventDate.Date
        )

    End Sub


    <TestMethod>
    Public Sub Repository_RoundTripsProvenanceWithoutChangingEventDate()

        Dim root As String =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteProvenance_" &
                Guid.NewGuid().ToString("N")
            )

        Try

            Dim repository As New ManuscriptRepository(
                Path.Combine(root, "data"),
                Path.Combine(root, "managed")
            )

            Dim recorded As New DateTime(
                2026,
                8,
                23,
                18,
                0,
                0,
                DateTimeKind.Utc
            )

            Dim modified As DateTime =
                recorded.AddHours(1)

            Dim manuscript As New Manuscript()

            manuscript.History.Add(
                New HistoryEvent With {
                    .EventDate = New DateTime(2020, 5, 4),
                    .Stage = PaperStage.Draft,
                    .RecordedAtUtc = recorded,
                    .LastModifiedAtUtc = modified
                }
            )

            repository.Save(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            Dim loaded As List(Of Manuscript) =
                repository.Load()

            Assert.AreEqual(
                New DateTime(2020, 5, 4),
                loaded(0).History(0).EventDate
            )

            Assert.AreEqual(
                recorded,
                loaded(0).History(0).RecordedAtUtc.Value
            )

            Assert.AreEqual(
                modified,
                loaded(0).History(0).LastModifiedAtUtc.Value
            )

            Assert.AreEqual(
                DateTimeKind.Utc,
                loaded(0).History(0).RecordedAtUtc.Value.Kind
            )

        Finally

            If Directory.Exists(root) Then
                Directory.Delete(root, True)
            End If

        End Try

    End Sub

End Class
