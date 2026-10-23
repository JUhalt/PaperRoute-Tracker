Imports System
Imports System.Linq
Imports System.Text.Json
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


    ' A decision dated after the manuscript was filed takes it out of the
    ' File Drawer with when and why it was filed in a note (#207), dated
    ' with the decision in the stage it was filed in, and clears the two
    ' fields.
    <TestMethod>
    Public Sub ApplyDecision_TakesAFiledManuscriptOutOfTheFileDrawerWithItsNote()

        Dim manuscript As Manuscript =
            FiledWhileWaiting()

        Dim submission As JournalSubmission =
            manuscript.Submissions.Single()

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = New DateTime(2026, 9, 1),
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(decision)

        Assert.IsTrue(
            ManuscriptLifecycleService.ApplyDecision(
                manuscript,
                submission,
                decision
            )
        )

        Assert.AreEqual(ManuscriptLocation.Pipeline, manuscript.Location)
        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.IsFalse(manuscript.FileDrawerDate.HasValue)
        Assert.AreEqual(String.Empty, manuscript.FileDrawerReason)

        Dim returned As HistoryEvent =
            manuscript.History.Single()

        Assert.AreEqual(
            "Returned to the Pipeline by an editorial decision (filed Aug 17, 2026: Waiting).",
            returned.Note
        )

        Assert.AreEqual(PaperStage.Submitted, returned.Stage)
        Assert.AreEqual(New DateTime(2026, 9, 1), returned.EventDate)
        Assert.IsTrue(returned.RecordedAtUtc.HasValue)

    End Sub


    ' Reconciling, as the manuscript page does each time it opens, puts the
    ' stage right from a decision dated after the manuscript was filed, as
    ' an import can leave one, and leaves it in the File Drawer with its
    ' date and reason and no note: only a submission or a decision
    ' recorded takes it out. Run again, the same day or not, it changes
    ' nothing (#207).
    <TestMethod>
    Public Sub ReconcileFromLatestWorkflow_PutsTheStageRightAndKeepsAFiledManuscriptFiled()

        Dim manuscript As Manuscript =
            FiledWhileWaiting()

        manuscript.Submissions.Single().Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 9, 1),
                .Decision = EditorialDecision.MajorRevision
            }
        )

        Assert.IsTrue(
            ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
                manuscript
            )
        )

        Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage)
        Assert.AreEqual(New DateTime(2026, 9, 1), manuscript.StageEnteredDate)
        Assert.AreEqual(ManuscriptLocation.FileDrawer, manuscript.Location)
        Assert.AreEqual(New DateTime(2026, 8, 17), manuscript.FileDrawerDate.Value)
        Assert.AreEqual("Waiting", manuscript.FileDrawerReason)
        Assert.AreEqual(0, manuscript.History.Count, "No note.")

        Dim reconciled As String =
            Snapshot(manuscript)

        ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
            manuscript
        )

        ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
            manuscript,
            allowSameDay:=True
        )

        Assert.AreEqual(
            reconciled,
            Snapshot(manuscript),
            "Run again, reconciling changes nothing."
        )

    End Sub


    ' Rejected and filed the same day. Reconciling again, as editing a
    ' submission does, keeps it filed; applying the decision again, as
    ' editing the decision does, finds it already in the state the
    ' decision sets. Either way it stays in the File Drawer as it was,
    ' with no note (#207).
    <TestMethod>
    Public Sub ReconcileAndApplyDecision_KeepAManuscriptFiledTheDayOfItsLastDecision()

        Dim decisionDate As New DateTime(2026, 8, 22)

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = decisionDate,
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = decisionDate.AddHours(15),
            .FileDrawerReason = "Three rejections"
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal F",
            .SubmittedDate = New DateTime(2026, 6, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .DecisionDate = decisionDate,
            .Decision = EditorialDecision.Rejected
        }

        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)

        manuscript.History.Add(
            New HistoryEvent With {
                .EventDate = decisionDate.AddHours(15),
                .Stage = PaperStage.Draft,
                .Note = "Moved to File Drawer. Reason: Three rejections"
            }
        )

        Dim filed As String =
            Snapshot(manuscript)

        ManuscriptLifecycleService.ReconcileFromLatestWorkflow(
            manuscript,
            allowSameDay:=True
        )

        Assert.AreEqual(
            filed,
            Snapshot(manuscript),
            "Reconciled again, it stays filed."
        )

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            decision
        )

        Assert.AreEqual(
            filed,
            Snapshot(manuscript),
            "Its decision applied again, it stays filed."
        )

    End Sub


    ' Deleting a decision or a submission puts the stage back from the
    ' workflow left, and a filed manuscript stays filed, with its date,
    ' reason and history as they were. A guard: this passed before #207;
    ' it keeps the note and the cleared fields to a submission or a
    ' decision recorded.
    <TestMethod>
    Public Sub ReconcileAfterRemoval_KeepsAFiledManuscriptFiled()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 6, 1),
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = New DateTime(2026, 8, 17),
            .FileDrawerReason = "Two rejections"
        }

        For Each round In {
            (Journal:="Journal G", Submitted:=New DateTime(2026, 2, 1), Decided:=New DateTime(2026, 3, 1)),
            (Journal:="Journal H", Submitted:=New DateTime(2026, 5, 1), Decided:=New DateTime(2026, 6, 1))
        }

            Dim submission As New JournalSubmission With {
                .JournalName = round.Journal,
                .SubmittedDate = round.Submitted
            }

            submission.Decisions.Add(
                New EditorialDecisionEvent With {
                    .DecisionDate = round.Decided,
                    .Decision = EditorialDecision.Rejected
                }
            )

            manuscript.Submissions.Add(submission)

        Next

        Dim latest As JournalSubmission =
            manuscript.Submissions.Last()

        Dim removedDecision As EditorialDecisionEvent =
            latest.Decisions.Single()

        latest.Decisions.Remove(removedDecision)

        Assert.IsTrue(
            ManuscriptLifecycleService.ReconcileAfterDecisionRemoval(
                manuscript,
                removedDecision
            )
        )

        Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage, "Back to waiting on the second journal.")
        AssertStillFiled(manuscript, "Two rejections")

        manuscript.Submissions.Remove(latest)

        Assert.IsTrue(
            ManuscriptLifecycleService.ReconcileAfterSubmissionRemoval(
                manuscript,
                latest
            )
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage, "Back to the first rejection.")
        AssertStillFiled(manuscript, "Two rejections")

    End Sub


    Private Shared Sub AssertStillFiled(
        manuscript As Manuscript,
        reason As String
    )

        Assert.AreEqual(ManuscriptLocation.FileDrawer, manuscript.Location)
        Assert.AreEqual(New DateTime(2026, 8, 17), manuscript.FileDrawerDate.Value)
        Assert.AreEqual(reason, manuscript.FileDrawerReason)
        Assert.AreEqual(0, manuscript.History.Count, "No note.")

    End Sub


    ' Submitted on July 1 and filed on August 17 while it waited, with the
    ' reason "Waiting".
    Private Shared Function FiledWhileWaiting() As Manuscript

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = New DateTime(2026, 7, 1),
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = New DateTime(2026, 8, 17),
            .FileDrawerReason = "Waiting",
            .TargetJournal = "Journal E"
        }

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName = "Journal E",
                .SubmittedDate = New DateTime(2026, 7, 1)
            }
        )

        Return manuscript

    End Function


    Private Shared Function Snapshot(
        manuscript As Manuscript
    ) As String

        Return JsonSerializer.Serialize(
            manuscript,
            CreateJsonOptions()
        )

    End Function

End Class
