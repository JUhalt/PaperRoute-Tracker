Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptVersionServiceTests

    <TestMethod>
    Public Sub CreateVersion_AddsVersionAndMakesItCurrent()

        Dim manuscript As New Manuscript()

        Dim created As DateTime =
            New DateTime(
                2026,
                8,
                22,
                19,
                30,
                0
            )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Working draft",
                "Initial tracked version.",
                "C:\Research\working.docx",
                False,
                createdDate:=created
            )

        Assert.AreEqual(
            1,
            manuscript.Versions.Count
        )

        Assert.AreSame(
            version,
            manuscript.Versions(0)
        )

        Assert.AreEqual(
            version.Id,
            manuscript.CurrentVersionId.Value
        )

        Assert.AreEqual(
            created,
            version.CreatedDate
        )

        Assert.AreEqual(
            "Working draft",
            version.Label
        )

    End Sub


    <TestMethod>
    Public Sub CreateVersion_CanAddHistoricalVersionWithoutChangingCurrent()

        Dim manuscript As New Manuscript()

        Dim current As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Current",
                String.Empty,
                String.Empty,
                False
            )

        Dim historical As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Historical",
                String.Empty,
                String.Empty,
                False,
                makeCurrent:=False
            )

        Assert.AreEqual(
            2,
            manuscript.Versions.Count
        )

        Assert.AreEqual(
            current.Id,
            manuscript.CurrentVersionId.Value
        )

        Assert.AreNotEqual(
            current.Id,
            historical.Id
        )

    End Sub


    <TestMethod>
    Public Sub CreateVersion_ManagedCopyRequiresFilePath()

        Dim manuscript As New Manuscript()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                ManuscriptVersionService.CreateVersion(
                    manuscript,
                    "Managed",
                    String.Empty,
                    String.Empty,
                    True
                )
            End Sub
        )

        Assert.AreEqual(
            0,
            manuscript.Versions.Count
        )

    End Sub


    <TestMethod>
    Public Sub CreateVersion_RejectsUnknownSubmission()

        Dim manuscript As New Manuscript()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                ManuscriptVersionService.CreateVersion(
                    manuscript,
                    "Submitted",
                    String.Empty,
                    String.Empty,
                    False,
                    submissionId:=Guid.NewGuid()
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub CreateVersion_DecisionAutomaticallyLinksParentSubmission()

        Dim manuscript As New Manuscript()

        Dim submission As New JournalSubmission()

        Dim decision As New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MajorRevision
        }

        submission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision 1",
                "Prepared after major revision.",
                String.Empty,
                False,
                decisionId:=decision.Id,
                revisionRoundNumber:=1
            )

        Assert.AreEqual(
            submission.Id,
            version.SubmissionId.Value
        )

        Assert.AreEqual(
            decision.Id,
            version.DecisionId.Value
        )

        Assert.AreEqual(
            1,
            version.RevisionRoundNumber.Value
        )

    End Sub


    <TestMethod>
    Public Sub CreateVersion_RejectsDecisionFromDifferentExplicitSubmission()

        Dim manuscript As New Manuscript()

        Dim firstSubmission As New JournalSubmission()
        Dim secondSubmission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent()

        secondSubmission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            firstSubmission
        )

        manuscript.Submissions.Add(
            secondSubmission
        )

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                ManuscriptVersionService.CreateVersion(
                    manuscript,
                    "Bad link",
                    String.Empty,
                    String.Empty,
                    False,
                    submissionId:=firstSubmission.Id,
                    decisionId:=decision.Id
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub SetCurrentVersion_ChangesCurrentVersion()

        Dim manuscript As New Manuscript()

        Dim first As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "First",
                String.Empty,
                String.Empty,
                False
            )

        Dim second As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Second",
                String.Empty,
                String.Empty,
                False
            )

        ManuscriptVersionService.SetCurrentVersion(
            manuscript,
            first.Id
        )

        Assert.AreEqual(
            first.Id,
            manuscript.CurrentVersionId.Value
        )

        Assert.AreNotEqual(
            second.Id,
            manuscript.CurrentVersionId.Value
        )

    End Sub


    <TestMethod>
    Public Sub SetCurrentVersion_RejectsUnknownVersion()

        Dim manuscript As New Manuscript()

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()
                ManuscriptVersionService.SetCurrentVersion(
                    manuscript,
                    Guid.NewGuid()
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub LinkVersionToSubmission_AssignsExistingSubmission()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Submitted version",
                String.Empty,
                String.Empty,
                False
            )

        ManuscriptVersionService.LinkVersionToSubmission(
            manuscript,
            version.Id,
            submission.Id
        )

        Assert.AreEqual(
            submission.Id,
            version.SubmissionId.Value
        )

    End Sub


    <TestMethod>
    Public Sub LinkVersionToDecision_AssignsDecisionSubmissionAndRound()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent()

        submission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision",
                String.Empty,
                String.Empty,
                False
            )

        ManuscriptVersionService.LinkVersionToDecision(
            manuscript,
            version.Id,
            decision.Id,
            2
        )

        Assert.AreEqual(
            submission.Id,
            version.SubmissionId.Value
        )

        Assert.AreEqual(
            decision.Id,
            version.DecisionId.Value
        )

        Assert.AreEqual(
            2,
            version.RevisionRoundNumber.Value
        )

    End Sub


    <TestMethod>
    Public Sub LinkVersionToDecision_RejectsNonpositiveRevisionRound()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent()

        submission.Decisions.Add(
            decision
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision",
                String.Empty,
                String.Empty,
                False
            )

        Assert.ThrowsExactly(Of ArgumentOutOfRangeException)(
            Sub()
                ManuscriptVersionService.LinkVersionToDecision(
                    manuscript,
                    version.Id,
                    decision.Id,
                    0
                )
            End Sub
        )

        Assert.IsFalse(
            version.DecisionId.HasValue
        )

    End Sub

End Class
