Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptLifecycleServiceTests

    <TestMethod>
    Public Sub ApplySubmission_LatestSubmissionBecomesCurrentTarget()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 1)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 20)
        }

        manuscript.Submissions.Add(submission)

        Assert.IsTrue(
            ManuscriptLifecycleService.ApplySubmission(
                manuscript,
                submission
            )
        )

        Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage)
        Assert.AreEqual(New DateTime(2026, 8, 20), manuscript.StageEnteredDate)
        Assert.AreEqual("Journal A", manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ApplySubmission_HistoricalSubmissionDoesNotOverrideNewerState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 20)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Old Journal",
            .SubmittedDate = New DateTime(2026, 5, 1)
        }

        manuscript.Submissions.Add(submission)

        Assert.IsFalse(
            ManuscriptLifecycleService.ApplySubmission(
                manuscript,
                submission
            )
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_RejectionMovesToDraftAndClearsMatchingTarget()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = New DateTime(2026, 8, 21),
            .TargetJournal = "Meta-Psychology"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Meta-Psychology",
            .SubmittedDate = New DateTime(2026, 7, 30)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 8, 22),
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        Assert.IsTrue(
            ManuscriptLifecycleService.ApplyDecision(
                manuscript,
                submission,
                decision
            )
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(New DateTime(2026, 8, 22), manuscript.StageEnteredDate)
        Assert.AreEqual(String.Empty, manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_RejectionPreservesDifferentNextTarget()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = New DateTime(2026, 8, 1),
            .TargetJournal = "Next Journal"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Rejected Journal",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 8, 10),
            .Decision = EditorialDecision.RejectedAfterReview
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            decision
        )

        Assert.AreEqual("Next Journal", manuscript.TargetJournal)
        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_RevisionSetsStageAndDeadline()

        Dim deadline As New DateTime(2026, 10, 1)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = New DateTime(2026, 8, 1)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal B",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 8, 15),
            .Decision = EditorialDecision.MajorRevision,
            .RevisionDeadline = deadline
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            decision
        )

        Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage)
        Assert.AreEqual(deadline, manuscript.RevisionDeadline.Value)
        Assert.AreEqual("Journal B", manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_AcceptanceMovesToAccepted()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = New DateTime(2026, 8, 1)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal C",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 8, 18),
            .Decision = EditorialDecision.Accepted
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            decision
        )

        Assert.AreEqual(PaperStage.Accepted, manuscript.CurrentStage)
        Assert.AreEqual("Journal C", manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_HistoricalDecisionDoesNotOverrideNewerState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 20)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Old Journal",
            .SubmittedDate = New DateTime(2026, 1, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 2, 1),
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        Assert.IsFalse(
            ManuscriptLifecycleService.ApplyDecision(
                manuscript,
                submission,
                decision
            )
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(New DateTime(2026, 8, 20), manuscript.StageEnteredDate)

    End Sub


    <TestMethod>
    Public Sub ReconcileFromLatestWorkflow_RepairsStaleRejectionState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.UnderReview,
            .StageEnteredDate = New DateTime(2026, 8, 21),
            .TargetJournal = "Meta-Psychology"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Meta-Psychology",
            .SubmittedDate = New DateTime(2026, 7, 30)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 8, 22),
                .Decision = EditorialDecision.Rejected
            }
        )

        manuscript.Submissions.Add(submission)

        Assert.IsTrue(
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
                manuscript
            )
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(String.Empty, manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ReconcileFromLatestWorkflow_DoesNotOverrideSameDayManualStage()

        Dim sharedDate As New DateTime(2026, 8, 22)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = sharedDate,
            .TargetJournal = "Next Journal"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Old Journal",
            .SubmittedDate = New DateTime(2026, 8, 1)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = sharedDate,
                .Decision = EditorialDecision.Rejected
            }
        )

        manuscript.Submissions.Add(submission)

        Assert.IsFalse(
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
                manuscript
            )
        )

        Assert.AreEqual("Next Journal", manuscript.TargetJournal)

    End Sub


    <TestMethod>
    Public Sub ReconcileFromLatestWorkflow_DoesNotReactivateLaterFileDrawerRecord()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 6, 1),
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = New DateTime(2026, 8, 20),
            .TargetJournal = String.Empty
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Historical Journal",
            .SubmittedDate = New DateTime(2026, 5, 1)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 7, 15),
                .Decision = EditorialDecision.Rejected
            }
        )

        manuscript.Submissions.Add(submission)

        Assert.IsFalse(
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
                manuscript
            )
        )

        Assert.AreEqual(
            ManuscriptLocation.FileDrawer,
            manuscript.Location
        )

    End Sub


    <TestMethod>
    Public Sub ApplyDecision_SameDayDecisionWinsOverSubmission()

        Dim eventDate As New DateTime(2026, 8, 22)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = eventDate
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal D",
            .SubmittedDate = eventDate
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = eventDate,
            .Decision = EditorialDecision.MinorRevision
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        Assert.IsTrue(
            ManuscriptLifecycleService.ApplyDecision(
                manuscript,
                submission,
                decision
            )
        )

        Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage)

    End Sub

End Class
