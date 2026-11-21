Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class FoundationHardeningTests

    <TestMethod>
    Public Sub StagePolicy_SubmittedRequiresActiveUndecidedSubmission()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft
        }

        Assert.IsFalse(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Submitted
            )
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 20)
        }

        manuscript.Submissions.Add(
            submission
        )

        Assert.IsTrue(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Submitted
            )
        )

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 22),
                .Decision = EditorialDecision.Rejected
            }
        )

        Assert.IsFalse(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Submitted
            )
        )

    End Sub


    <TestMethod>
    Public Sub StagePolicy_RevisionRequiresRevisionDecision()

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission With {
            .SubmittedDate = New DateTime(2026, 8, 1)
        }

        manuscript.Submissions.Add(
            submission
        )

        Assert.IsFalse(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Revision
            )
        )

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 20),
                .Decision = EditorialDecision.MajorRevision
            }
        )

        Assert.IsTrue(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Revision
            )
        )

    End Sub


    <TestMethod>
    Public Sub StagePolicy_PublicationStagesRequireAcceptance()

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission With {
            .SubmittedDate = New DateTime(2026, 8, 1)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 20),
                .Decision = EditorialDecision.Accepted
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Assert.IsTrue(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Accepted
            )
        )

        Assert.IsTrue(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.InPress
            )
        )

        Assert.IsTrue(
            ManuscriptStagePolicyService.IsStageSupported(
                manuscript,
                PaperStage.Published
            )
        )

    End Sub


    <TestMethod>
    Public Sub LifecycleRepair_RejectedSubmissionCannotRemainSubmitted()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = New DateTime(2026, 8, 20),
            .TargetJournal = "Journal A"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 20)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 22),
                .Decision = EditorialDecision.Rejected
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Assert.IsTrue(
            ManuscriptLifecycleService.
                ReconcileAfterWorkflowMutation(
                    manuscript
                )
        )

        Assert.AreEqual(
            PaperStage.Draft,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            String.Empty,
            manuscript.TargetJournal
        )

    End Sub


    <TestMethod>
    Public Sub LifecycleRepair_RemovedAcceptanceFallsBackToRevision()

        Dim deadline As New DateTime(
            2026,
            10,
            1
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Published,
            .StageEnteredDate = New DateTime(2026, 8, 25)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal B",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 10),
                .Decision = EditorialDecision.MajorRevision,
                .RevisionDeadline = deadline
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Assert.IsTrue(
            ManuscriptLifecycleService.
                ReconcileAfterWorkflowMutation(
                    manuscript
                )
        )

        Assert.AreEqual(
            PaperStage.Revision,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            deadline,
            manuscript.RevisionDeadline.Value
        )

    End Sub


    <TestMethod>
    Public Sub DecisionRemoval_RejectionRestoresSubmittedState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = New DateTime(2026, 8, 20),
            .TargetJournal = "Journal A"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 20)
        }

        Dim rejection As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 8, 22),
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(
            rejection
        )

        manuscript.Submissions.Add(
            submission
        )

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            rejection
        )

        Assert.AreEqual(
            PaperStage.Draft,
            manuscript.CurrentStage
        )

        submission.Decisions.Remove(
            rejection
        )

        Assert.IsTrue(
            ManuscriptLifecycleService.
                ReconcileAfterDecisionRemoval(
                    manuscript,
                    rejection
                )
        )

        Assert.AreEqual(
            PaperStage.Submitted,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            "Journal A",
            manuscript.TargetJournal
        )

    End Sub


    <TestMethod>
    Public Sub SubmissionRemoval_CurrentSubmittedFallsBackToDraft()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 1)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 20)
        }

        manuscript.Submissions.Add(
            submission
        )

        ManuscriptLifecycleService.ApplySubmission(
            manuscript,
            submission
        )

        Assert.AreEqual(
            PaperStage.Submitted,
            manuscript.CurrentStage
        )

        manuscript.Submissions.Remove(
            submission
        )

        Assert.IsTrue(
            ManuscriptLifecycleService.
                ReconcileAfterSubmissionRemoval(
                    manuscript,
                    submission
                )
        )

        Assert.AreEqual(
            PaperStage.Draft,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            String.Empty,
            manuscript.TargetJournal
        )

    End Sub


    <TestMethod>
    Public Sub AttentionSnapshot_ComputesCountsAndSameDayLatestDecision()

        Dim today As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim first As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        first.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 7, 20),
                .Decision = EditorialDecision.Rejected
            }
        )

        Dim second As New JournalSubmission With {
            .JournalName = "Journal B",
            .SubmittedDate = today
        }

        second.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = today,
                .Decision = EditorialDecision.MajorRevision
            }
        )

        Dim latestRejection As New EditorialDecisionEvent With {
            .DecisionDate = today,
            .Decision = EditorialDecision.RejectedAfterReview
        }

        second.Decisions.Add(
            latestRejection
        )

        manuscript.Submissions.Add(
            first
        )

        manuscript.Submissions.Add(
            second
        )

        Dim snapshot As ManuscriptAttentionSnapshot =
            ManuscriptAttentionService.Evaluate(
                manuscript,
                today,
                revisionWarningDays:=14,
                longReviewThresholdDays:=90,
                recentRejectionThresholdDays:=30
            )

        Assert.AreEqual(
            2,
            snapshot.SubmissionCount
        )

        Assert.AreEqual(
            2,
            snapshot.RejectionCount
        )

        Assert.AreSame(
            second,
            snapshot.LatestSubmission
        )

        Assert.AreSame(
            latestRejection,
            snapshot.LatestDecision
        )

        Assert.IsTrue(
            snapshot.WasRecentlyRejected
        )

        Assert.AreEqual(
            0,
            snapshot.RejectionDaysAgo.Value
        )

    End Sub


    <TestMethod>
    Public Sub AuthorSearchIndex_UsesStructuredAuthorsAndAffiliations()

        Dim author As New AuthorRecord With {
            .GivenName = "Alex",
            .FamilyName = "Researcher",
            .Orcid = "0000-0002-1825-0097"
        }

        Dim affiliation As New AffiliationRecord With {
            .Department = "Department of Psychology",
            .Institution = "Example University",
            .City = "Hartford",
            .Region = "CT",
            .Country = "USA"
        }

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            author
        )

        library.Affiliations.Add(
            affiliation
        )

        Dim manuscript As New Manuscript()

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = author.Id,
                .AffiliationIds =
                    New List(Of Guid) From {
                        affiliation.Id
                    }
            }
        )

        Dim index As New AuthorLibrarySearchIndex(
            library
        )

        Dim searchText As String =
            index.BuildSearchText(
                manuscript
            )

        StringAssert.Contains(
            searchText,
            "Alex Researcher"
        )

        StringAssert.Contains(
            searchText,
            "0000-0002-1825-0097"
        )

        StringAssert.Contains(
            searchText,
            "Example University"
        )

    End Sub


    <TestMethod>
    Public Sub RouteProjection_CollapsesRedundantSubmittedStage()

        Dim eventDate As New DateTime(
            2026,
            8,
            23,
            11,
            50,
            0
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.Submitted,
                .Note = "Stage changed from Draft to Submitted."
            }
        )

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName = "Journal A",
                .SubmittedDate = eventDate.Date
            }
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value =
                        PaperStage.Submitted
                End Function
            )
        )

        Assert.AreEqual(
            1,
            route.Waypoints.Where(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Submission
                End Function
            ).Count()
        )

    End Sub


    <TestMethod>
    Public Sub RouteProjection_CollapsesRedundantRevisionStage()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.Revision,
                .Note = "Stage changed from UnderReview to Revision."
            }
        )

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = eventDate.AddDays(-30)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = eventDate,
                .Decision = EditorialDecision.MinorRevision
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.IsFalse(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value =
                        PaperStage.Revision
                End Function
            )
        )

        Assert.IsTrue(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Decision AndAlso
                        item.Decision.HasValue AndAlso
                        item.Decision.Value =
                        EditorialDecision.MinorRevision
                End Function
            )
        )

    End Sub


    <TestMethod>
    Public Sub RouteProjection_KeepsMeaningfulUnderReviewStage()

        Dim eventDate As New DateTime(
            2026,
            8,
            23
        )

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = eventDate
        }

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = eventDate,
                .Stage = PaperStage.UnderReview,
                .Note = "Stage changed from Submitted to UnderReview."
            }
        )

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName = "Journal A",
                .SubmittedDate = eventDate
            }
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Assert.IsTrue(
            route.Waypoints.Any(
                Function(item)
                    Return item.Kind =
                        ManuscriptRouteWaypointKind.Stage AndAlso
                        item.Stage.HasValue AndAlso
                        item.Stage.Value =
                        PaperStage.UnderReview
                End Function
            )
        )

    End Sub


    <TestMethod>
    Public Sub Repository_LargeLibraryRoundTripsWithoutChangingSemantics()

        Dim root As String =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteHardening_" &
                Guid.NewGuid().ToString("N")
            )

        Dim dataDirectory As String =
            Path.Combine(
                root,
                "data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                root,
                "managed"
            )

        Try

            Dim repository As New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            )

            Dim manuscripts As New List(Of Manuscript)()

            For index As Integer = 1 To 300

                Dim manuscript As New Manuscript With {
                    .Title = "Performance manuscript " & index.ToString(),
                    .CurrentStage = PaperStage.Draft,
                    .StageEnteredDate =
                        New DateTime(2026, 1, 1).AddDays(index)
                }

                manuscript.History.Add(
                    New HistoryEvent With {
                        .EventDate = manuscript.StageEnteredDate,
                        .Stage = PaperStage.Draft,
                        .Note = "Round-trip test."
                    }
                )

                manuscripts.Add(
                    manuscript
                )

            Next

            repository.Save(
                manuscripts
            )

            Dim loaded As List(Of Manuscript) =
                repository.Load()

            Assert.AreEqual(
                300,
                loaded.Count
            )

            Assert.AreEqual(
                "Performance manuscript 300",
                loaded(299).Title
            )

            Assert.AreEqual(
                manuscripts(299).StageEnteredDate,
                loaded(299).StageEnteredDate
            )

        Finally

            If Directory.Exists(
                root
            ) Then

                Directory.Delete(
                    root,
                    True
                )

            End If

        End Try

    End Sub

End Class
