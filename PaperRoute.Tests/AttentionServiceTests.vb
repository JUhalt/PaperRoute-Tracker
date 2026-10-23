Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class AttentionServiceTests

    Private ReadOnly _today As New DateTime(
        2026,
        8,
        19
    )


    ' =====================================================
    ' Overdue revisions
    ' =====================================================

    <TestMethod>
    Public Sub OverdueRevision_DeadlineBeforeToday_ReturnsTrue()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today.AddDays(-1)
            )

        Assert.IsTrue(
            ManuscriptAttentionService.
                HasOverdueRevision(
                    manuscript,
                    _today
                )
        )

    End Sub


    <TestMethod>
    Public Sub OverdueRevision_DeadlineToday_ReturnsFalse()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today
            )

        Assert.IsFalse(
            ManuscriptAttentionService.
                HasOverdueRevision(
                    manuscript,
                    _today
                )
        )

    End Sub


    ' =====================================================
    ' Revision due soon
    ' =====================================================

    <TestMethod>
    Public Sub RevisionDueSoon_AtWarningBoundary_ReturnsTrue()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today.AddDays(14)
            )

        Assert.IsTrue(
            ManuscriptAttentionService.
                IsRevisionDueSoon(
                    manuscript,
                    _today,
                    14
                )
        )

    End Sub


    <TestMethod>
    Public Sub RevisionDueSoon_BeyondWarningBoundary_ReturnsFalse()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today.AddDays(15)
            )

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsRevisionDueSoon(
                    manuscript,
                    _today,
                    14
                )
        )

    End Sub


    <TestMethod>
    Public Sub RevisionDueSoon_WrongStageOrLocation_ReturnsFalse()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today.AddDays(5)
            )

        manuscript.CurrentStage =
            PaperStage.Draft

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsRevisionDueSoon(
                    manuscript,
                    _today,
                    14
                )
        )

        manuscript.CurrentStage =
            PaperStage.Revision

        manuscript.Location =
            ManuscriptLocation.FileDrawer

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsRevisionDueSoon(
                    manuscript,
                    _today,
                    14
                )
        )

    End Sub


    <TestMethod>
    Public Sub RevisionDueSoon_UsesLatestDecision()

        Dim manuscript As Manuscript =
            CreateRevisionManuscript(
                _today.AddDays(5)
            )

        Dim submission As JournalSubmission =
            manuscript.Submissions(0)

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate =
                    _today.AddDays(-1),
                .Decision =
                    EditorialDecision.Accepted,
                .RevisionDeadline =
                    Nothing
            }
        )

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsRevisionDueSoon(
                    manuscript,
                    _today,
                    14
                )
        )

    End Sub


    ' =====================================================
    ' Long review
    ' =====================================================

    <TestMethod>
    Public Sub LongReview_AtThreshold_ReturnsTrue()

        Dim manuscript As Manuscript =
            CreateWaitingManuscript(
                _today.AddDays(-90)
            )

        Assert.IsTrue(
            ManuscriptAttentionService.
                IsLongWaitingManuscript(
                    manuscript,
                    _today,
                    90
                )
        )

    End Sub


    <TestMethod>
    Public Sub LongReview_BelowThreshold_ReturnsFalse()

        Dim manuscript As Manuscript =
            CreateWaitingManuscript(
                _today.AddDays(-89)
            )

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsLongWaitingManuscript(
                    manuscript,
                    _today,
                    90
                )
        )

    End Sub


    <TestMethod>
    Public Sub LongReview_UsesLatestSubmission()

        Dim manuscript As Manuscript =
            CreateWaitingManuscript(
                _today.AddDays(-120)
            )

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName =
                    "Newer Journal",
                .SubmittedDate =
                    _today.AddDays(-10)
            }
        )

        Assert.IsFalse(
            ManuscriptAttentionService.
                IsLongWaitingManuscript(
                    manuscript,
                    _today,
                    90
                )
        )

    End Sub


    ' =====================================================
    ' Missing target journal
    ' =====================================================

    <TestMethod>
    Public Sub MissingTargetJournal_DraftPipelineWithoutTarget_ReturnsTrue()

        Dim manuscript As New Manuscript With {
            .Title =
                "Untargeted Draft",
            .CurrentStage =
                PaperStage.Draft,
            .Location =
                ManuscriptLocation.Pipeline,
            .TargetJournal =
                String.Empty
        }

        Assert.IsTrue(
            ManuscriptAttentionService.
                HasMissingTargetJournal(
                    manuscript
                )
        )

    End Sub


    <TestMethod>
    Public Sub MissingTargetJournal_UnderReviewWithoutTarget_ReturnsFalse()

        Dim manuscript As New Manuscript With {
            .Title =
                "Already Submitted",
            .CurrentStage =
                PaperStage.UnderReview,
            .Location =
                ManuscriptLocation.Pipeline,
            .TargetJournal =
                String.Empty
        }

        Assert.IsFalse(
            ManuscriptAttentionService.
                HasMissingTargetJournal(
                    manuscript
                )
        )

    End Sub


    ' =====================================================
    ' Recent rejection
    ' =====================================================

    <TestMethod>
    Public Sub RecentRejection_AtThreshold_ReturnsTrue()

        Dim manuscript As Manuscript =
            CreateRejectedManuscript(
                _today.AddDays(-30)
            )

        Assert.IsTrue(
            ManuscriptAttentionService.
                WasRecentlyRejected(
                    manuscript,
                    _today,
                    30
                )
        )

    End Sub


    <TestMethod>
    Public Sub RecentRejection_OutsideThreshold_ReturnsFalse()

        Dim manuscript As Manuscript =
            CreateRejectedManuscript(
                _today.AddDays(-31)
            )

        Assert.IsFalse(
            ManuscriptAttentionService.
                WasRecentlyRejected(
                    manuscript,
                    _today,
                    30
                )
        )

    End Sub


    <TestMethod>
    Public Sub RecentRejection_LatestDecisionControlsResult()

        Dim manuscript As Manuscript =
            CreateRejectedManuscript(
                _today.AddDays(-5)
            )

        Dim submission As JournalSubmission =
            manuscript.Submissions(0)

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate =
                    _today.AddDays(-1),
                .Decision =
                    EditorialDecision.MajorRevision,
                .RevisionDeadline =
                    _today.AddDays(20)
            }
        )

        Assert.IsFalse(
            ManuscriptAttentionService.
                WasRecentlyRejected(
                    manuscript,
                    _today,
                    30
                )
        )

    End Sub


    <TestMethod>
    Public Sub RecentRejection_LatestSubmissionControlsResult()

        Dim manuscript As Manuscript =
            CreateRejectedManuscript(
                _today.AddDays(-5)
            )

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName =
                    "New Route Journal",
                .SubmittedDate =
                    _today.AddDays(-1)
            }
        )

        Assert.IsFalse(
            ManuscriptAttentionService.
                WasRecentlyRejected(
                    manuscript,
                    _today,
                    30
                )
        )

    End Sub


    ' =====================================================
    ' File Drawer suggestion (#203)
    ' =====================================================

    <TestMethod>
    Public Sub FilingSuggestion_LatestSubmissionRejected_CanSuggest()

        Dim snapshot As ManuscriptAttentionSnapshot =
            Evaluate(
                CreateRejectedThreeTimes()
            )

        Assert.AreEqual(
            3,
            snapshot.RejectionCount
        )

        Assert.IsTrue(
            snapshot.CanSuggestFiling
        )

    End Sub


    <TestMethod>
    Public Sub FilingSuggestion_LatestSubmissionWithdrawn_CanSuggest()

        Dim manuscript As Manuscript =
            CreateRejectedThreeTimes()

        manuscript.Submissions.Add(
            CreateSubmission(
                "Fourth Journal",
                _today.AddDays(-40),
                EditorialDecision.Withdrawn,
                _today.AddDays(-35)
            )
        )

        Dim snapshot As ManuscriptAttentionSnapshot =
            Evaluate(
                manuscript
            )

        Assert.AreEqual(
            3,
            snapshot.RejectionCount,
            "A withdrawal is not a rejection."
        )

        Assert.IsTrue(
            snapshot.CanSuggestFiling
        )

    End Sub


    <TestMethod>
    Public Sub FilingSuggestion_NewerSubmissionOpen_CannotSuggest()

        Dim manuscript As Manuscript =
            CreateRejectedThreeTimes()

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName =
                    "Fourth Journal",
                .SubmittedDate =
                    _today.AddDays(-10)
            }
        )

        Assert.IsFalse(
            Evaluate(
                manuscript
            ).CanSuggestFiling
        )

    End Sub


    <TestMethod>
    Public Sub FilingSuggestion_LatestDecisionAsksForRevision_CannotSuggest()

        Dim manuscript As Manuscript =
            CreateRejectedThreeTimes()

        manuscript.Submissions.Add(
            CreateSubmission(
                "Fourth Journal",
                _today.AddDays(-40),
                EditorialDecision.MajorRevision,
                _today.AddDays(-5)
            )
        )

        Assert.IsFalse(
            Evaluate(
                manuscript
            ).CanSuggestFiling
        )

    End Sub


    ' The note Form1.RestoreToPipeline writes, with when and why the
    ' manuscript was filed after it (#202). A revival before the last
    ' rejection belongs to an older route.
    <TestMethod>
    Public Sub FilingSuggestion_RestoredAfterTheLastRejection_CannotSuggest()

        Dim manuscript As Manuscript =
            CreateRejectedThreeTimes()

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate =
                    _today.AddDays(-300),
                .Stage =
                    PaperStage.Draft,
                .Note =
                    ManuscriptAttentionService.RestoredFromFileDrawerNote & "."
            }
        )

        Assert.IsTrue(
            Evaluate(
                manuscript
            ).CanSuggestFiling,
            "Restored before the last rejection."
        )

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate =
                    _today.AddDays(-5),
                .Stage =
                    PaperStage.Draft,
                .Note =
                    ManuscriptAttentionService.RestoredFromFileDrawerNote &
                    " (filed Aug 17, 2026: Three rejections; revisit with a larger sample.)"
            }
        )

        Assert.IsFalse(
            Evaluate(
                manuscript
            ).CanSuggestFiling,
            "Restored after the last rejection."
        )

    End Sub


    ' A rejection that came after the manuscript was filed takes it out of
    ' the File Drawer (#207), but the researcher did not revive it: its
    ' note, "Returned to the Pipeline by an editorial decision (filed
    ' ...)", does not begin as the restore note does, and the card may
    ' suggest filing again. A guard: before #207 no note was written and
    ' the suggestion showed as it does now.
    <TestMethod>
    Public Sub FilingSuggestion_TakenOutOfTheFileDrawerByARejection_CanSuggest()

        Dim manuscript As Manuscript =
            CreateRejectedThreeTimes()

        Dim waiting As New JournalSubmission With {
            .JournalName =
                "Fourth Journal",
            .SubmittedDate =
                _today.AddDays(-60)
        }

        manuscript.Submissions.Add(
            waiting
        )

        manuscript.CurrentStage =
            PaperStage.Submitted

        manuscript.StageEnteredDate =
            _today.AddDays(-60)

        manuscript.Location =
            ManuscriptLocation.FileDrawer

        manuscript.FileDrawerDate =
            _today.AddDays(-30)

        manuscript.FileDrawerReason =
            "Waited too long."

        Dim rejection As New EditorialDecisionEvent With {
            .Decision =
                EditorialDecision.Rejected,
            .DecisionDate =
                _today.AddDays(-10)
        }

        waiting.Decisions.Add(
            rejection
        )

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            waiting,
            rejection
        )

        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscript.Location
        )

        Assert.IsTrue(
            Evaluate(
                manuscript
            ).CanSuggestFiling
        )

    End Sub


    ' =====================================================
    ' Helpers
    ' =====================================================

    Private Function Evaluate(
        manuscript As Manuscript
    ) As ManuscriptAttentionSnapshot

        Return ManuscriptAttentionService.Evaluate(
            manuscript,
            _today,
            revisionWarningDays:=14,
            longReviewThresholdDays:=90,
            recentRejectionThresholdDays:=30
        )

    End Function


    ' Back in Draft on the Pipeline after three rejections, the last of
    ' them 100 days ago.
    Private Function CreateRejectedThreeTimes() As Manuscript

        Dim manuscript As New Manuscript With {
            .Title =
                "Rejected Three Times",
            .CurrentStage =
                PaperStage.Draft,
            .Location =
                ManuscriptLocation.Pipeline,
            .TargetJournal =
                "Journal of Fourth Attempts"
        }

        For round As Integer = 3 To 1 Step -1

            Dim decided As DateTime =
                _today.AddDays(-100 * round)

            manuscript.Submissions.Add(
                CreateSubmission(
                    "Journal " & round.ToString(),
                    decided.AddDays(-60),
                    EditorialDecision.Rejected,
                    decided
                )
            )

        Next

        Return manuscript

    End Function


    Private Function CreateSubmission(
        journalName As String,
        submittedDate As DateTime,
        decision As EditorialDecision,
        decisionDate As DateTime
    ) As JournalSubmission

        Dim submission As New JournalSubmission With {
            .JournalName =
                journalName,
            .SubmittedDate =
                submittedDate
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate =
                    decisionDate,
                .Decision =
                    decision
            }
        )

        Return submission

    End Function


    Private Function CreateRevisionManuscript(
        deadline As DateTime
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Title =
                "Revision Study",
            .CurrentStage =
                PaperStage.Revision,
            .Location =
                ManuscriptLocation.Pipeline
        }

        Dim submission As New JournalSubmission With {
            .JournalName =
                "Journal of Revision Tests",
            .SubmittedDate =
                _today.AddDays(-60)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate =
                    _today.AddDays(-20),
                .Decision =
                    EditorialDecision.MajorRevision,
                .RevisionDeadline =
                    deadline
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Return manuscript

    End Function


    Private Function CreateWaitingManuscript(
        submittedDate As DateTime
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Title =
                "Waiting Study",
            .CurrentStage =
                PaperStage.UnderReview,
            .Location =
                ManuscriptLocation.Pipeline
        }

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName =
                    "Journal of Waiting",
                .SubmittedDate =
                    submittedDate
            }
        )

        Return manuscript

    End Function


    Private Function CreateRejectedManuscript(
        decisionDate As DateTime
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Title =
                "Rejected Study",
            .CurrentStage =
                PaperStage.Draft,
            .Location =
                ManuscriptLocation.Pipeline
        }

        Dim submission As New JournalSubmission With {
            .JournalName =
                "Journal of Rejections",
            .SubmittedDate =
                decisionDate.AddDays(-30)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate =
                    decisionDate,
                .Decision =
                    EditorialDecision.Rejected
            }
        )

        manuscript.Submissions.Add(
            submission
        )

        Return manuscript

    End Function

End Class