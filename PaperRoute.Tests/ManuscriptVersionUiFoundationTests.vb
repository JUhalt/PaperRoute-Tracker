Imports System
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptVersionUiFoundationTests

    <TestMethod>
    Public Sub UpdateVersion_ChangesMetadataButPreservesFileIdentity()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Draft 1",
                "Original notes",
                "C:\Research\draft1.docx",
                True,
                createdDate:=New DateTime(2026, 8, 1)
            )

        Dim originalRecorded As DateTime =
            version.RecordedAtUtc.Value

        Dim changed As Boolean =
            ManuscriptVersionService.UpdateVersion(
                manuscript,
                version.Id,
                "Draft 1 corrected",
                "Updated notes",
                New DateTime(2026, 8, 2)
            )

        Assert.IsTrue(changed)

        Assert.AreEqual(
            "Draft 1 corrected",
            version.Label
        )

        Assert.AreEqual(
            "Updated notes",
            version.Notes
        )

        Assert.AreEqual(
            New DateTime(2026, 8, 2),
            version.CreatedDate
        )

        Assert.AreEqual(
            "C:\Research\draft1.docx",
            version.LocalFilePath
        )

        Assert.IsTrue(
            version.IsManagedCopy
        )

        Assert.AreEqual(
            originalRecorded,
            version.RecordedAtUtc.Value
        )

        Assert.IsTrue(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_NoOpDoesNotCreateModificationAudit()

        Dim manuscript As New Manuscript()

        Dim createdDate As New DateTime(
            2026,
            8,
            10
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Same",
                "No changes",
                String.Empty,
                False,
                createdDate:=createdDate
            )

        Assert.IsFalse(
            version.LastModifiedAtUtc.HasValue
        )

        Dim changed As Boolean =
            ManuscriptVersionService.UpdateVersion(
                manuscript,
                version.Id,
                "Same",
                "No changes",
                createdDate
            )

        Assert.IsFalse(changed)

        Assert.IsFalse(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_CanClearWorkflowAssociations()

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
                False,
                decisionId:=decision.Id,
                revisionRoundNumber:=2,
                createdDate:=New DateTime(2026, 8, 12)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            submissionId:=Nothing,
            decisionId:=Nothing,
            revisionRoundNumber:=Nothing
        )

        Assert.IsFalse(
            version.SubmissionId.HasValue
        )

        Assert.IsFalse(
            version.DecisionId.HasValue
        )

        Assert.IsFalse(
            version.RevisionRoundNumber.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_DecisionInfersParentSubmission()

        Dim manuscript As New Manuscript()
        Dim submission As New JournalSubmission()
        Dim decision As New EditorialDecisionEvent With {
            .Decision =
                EditorialDecision.MajorRevision
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
                "Working",
                String.Empty,
                String.Empty,
                False,
                createdDate:=New DateTime(2026, 8, 15)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            "Revision 1",
            String.Empty,
            version.CreatedDate,
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
    Public Sub UpdateVersion_RejectsMismatchedDecisionWithoutMutation()

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

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Original",
                String.Empty,
                String.Empty,
                False,
                createdDate:=New DateTime(2026, 8, 15)
            )

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub()

                ManuscriptVersionService.UpdateVersion(
                    manuscript,
                    version.Id,
                    "Should not apply",
                    String.Empty,
                    version.CreatedDate,
                    submissionId:=firstSubmission.Id,
                    decisionId:=decision.Id
                )

            End Sub
        )

        Assert.AreEqual(
            "Original",
            version.Label
        )

        Assert.IsFalse(
            version.SubmissionId.HasValue
        )

        Assert.IsFalse(
            version.DecisionId.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_PreservesUnchangedUnresolvedHistoricalLinks()

        Dim manuscript As New Manuscript()

        Dim unresolvedSubmissionId As Guid =
            Guid.NewGuid()

        Dim unresolvedDecisionId As Guid =
            Guid.NewGuid()

        Dim version As New ManuscriptVersion With {
            .Label = "Imported historical version",
            .CreatedDate = New DateTime(2019, 5, 1),
            .SubmissionId = unresolvedSubmissionId,
            .DecisionId = unresolvedDecisionId,
            .RevisionRoundNumber = 1
        }

        manuscript.Versions.Add(
            version
        )

        Dim changed As Boolean =
            ManuscriptVersionService.UpdateVersion(
                manuscript,
                version.Id,
                "Imported historical version",
                "Metadata note added later.",
                version.CreatedDate,
                submissionId:=unresolvedSubmissionId,
                decisionId:=unresolvedDecisionId,
                revisionRoundNumber:=1
            )

        Assert.IsTrue(changed)

        Assert.AreEqual(
            unresolvedSubmissionId,
            version.SubmissionId.Value
        )

        Assert.AreEqual(
            unresolvedDecisionId,
            version.DecisionId.Value
        )

        Assert.AreEqual(
            "Metadata note added later.",
            version.Notes
        )

    End Sub


    <TestMethod>
    Public Sub ChronologicalVersions_UsesRecordedOrderForSameDayAndIgnoresEdits()

        Dim manuscript As New Manuscript()

        Dim first As New ManuscriptVersion With {
            .CreatedDate = New DateTime(2026, 8, 20),
            .Label = "First",
            .RecordedAtUtc =
                New DateTime(
                    2026,
                    8,
                    20,
                    14,
                    0,
                    0,
                    DateTimeKind.Utc
                ),
            .LastModifiedAtUtc =
                New DateTime(
                    2030,
                    1,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc
                )
        }

        Dim second As New ManuscriptVersion With {
            .CreatedDate = New DateTime(2026, 8, 20),
            .Label = "Second",
            .RecordedAtUtc =
                New DateTime(
                    2026,
                    8,
                    20,
                    14,
                    1,
                    0,
                    DateTimeKind.Utc
                )
        }

        manuscript.Versions.Add(
            second
        )

        manuscript.Versions.Add(
            first
        )

        Dim ordered As System.Collections.Generic.List(Of ManuscriptVersion) =
            ManuscriptVersionService.GetChronologicalVersions(
                manuscript
            )

        Assert.AreSame(
            first,
            ordered(0)
        )

        Assert.AreSame(
            second,
            ordered(1)
        )

    End Sub


    <TestMethod>
    Public Sub UpdatedDecisionAssociation_IsProjectedOntoRoute()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision,
            .StageEnteredDate = New DateTime(2026, 8, 20)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 1)
        }

        Dim decision As New EditorialDecisionEvent With {
            .Decision =
                EditorialDecision.MajorRevision,
            .DecisionDate =
                New DateTime(2026, 8, 20)
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
                String.Empty,
                String.Empty,
                False,
                createdDate:=New DateTime(2026, 8, 21)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            decisionId:=decision.Id,
            revisionRoundNumber:=1
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim decisionWaypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Find(
                Function(item)
                    Return item.DecisionId.HasValue AndAlso
                        item.DecisionId.Value =
                        decision.Id
                End Function
            )

        Assert.IsNotNull(
            decisionWaypoint
        )

        CollectionAssert.Contains(
            decisionWaypoint.RelatedVersionIds,
            version.Id
        )

        Assert.AreEqual(
            1,
            decisionWaypoint.RevisionRoundNumber.Value
        )

    End Sub


    <TestMethod>
    Public Sub ChronologicalVersions_LegacySameDayPreservesStoredOrder()

        Dim manuscript As New Manuscript()

        Dim first As New ManuscriptVersion With {
            .CreatedDate = New DateTime(2020, 4, 5),
            .Label = "Legacy first"
        }

        Dim second As New ManuscriptVersion With {
            .CreatedDate = New DateTime(2020, 4, 5),
            .Label = "Legacy second"
        }

        manuscript.Versions.Add(
            first
        )

        manuscript.Versions.Add(
            second
        )

        Dim ordered As System.Collections.Generic.List(Of ManuscriptVersion) =
            ManuscriptVersionService.GetChronologicalVersions(
                manuscript
            )

        Assert.AreSame(
            first,
            ordered(0)
        )

        Assert.AreSame(
            second,
            ordered(1)
        )

    End Sub



    <TestMethod>
    Public Sub UpdateVersion_LinkedFileCanBeChanged()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Linked",
                String.Empty,
                "C:\Research\old.docx",
                False,
                createdDate:=New DateTime(2026, 8, 1)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            "C:\Research\new.docx",
            False
        )

        Assert.AreEqual(
            "C:\Research\new.docx",
            version.LocalFilePath
        )

        Assert.IsFalse(
            version.IsManagedCopy
        )

        Assert.IsTrue(
            version.LastModifiedAtUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_LinkedFileCanBeCleared()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Linked",
                String.Empty,
                "C:\Research\old.docx",
                False,
                createdDate:=New DateTime(2026, 8, 1)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            String.Empty,
            False
        )

        Assert.AreEqual(
            String.Empty,
            version.LocalFilePath
        )

        Assert.IsFalse(
            version.IsManagedCopy
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_MetadataOnlyCanBecomeManagedSnapshot()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Metadata only",
                String.Empty,
                String.Empty,
                False,
                createdDate:=New DateTime(2026, 8, 1)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            "C:\Research\snapshot.docx",
            True
        )

        Assert.AreEqual(
            "C:\Research\snapshot.docx",
            version.LocalFilePath
        )

        Assert.IsTrue(
            version.IsManagedCopy
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_PendingManagedSnapshotCanChangeBeforeCommit()

        Dim manuscript As New Manuscript()

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Pending managed",
                String.Empty,
                "C:\Research\first.docx",
                True,
                createdDate:=New DateTime(2026, 8, 1)
            )

        ManuscriptVersionService.UpdateVersion(
            manuscript,
            version.Id,
            version.Label,
            version.Notes,
            version.CreatedDate,
            "C:\Research\second.docx",
            True
        )

        Assert.AreEqual(
            "C:\Research\second.docx",
            version.LocalFilePath
        )

        Assert.IsTrue(
            version.IsManagedCopy
        )

    End Sub


    <TestMethod>
    Public Sub UpdateVersion_ManagedSnapshotRejectsFileReplacement()

        Dim manuscript As New Manuscript()

        Dim version As New ManuscriptVersion With {
            .CreatedDate = New DateTime(2026, 8, 1),
            .Label = "Managed",
            .IsManagedCopy = True
        }

        Dim managedRoot As String =
            New ManagedLibraryService().RootDirectory

        version.LocalFilePath =
            Path.Combine(
                managedRoot,
                manuscript.Id.ToString("N"),
                "versions",
                version.Id.ToString("N"),
                "managed.docx"
            )

        manuscript.Versions.Add(
            version
        )

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub()

                ManuscriptVersionService.UpdateVersion(
                    manuscript,
                    version.Id,
                    version.Label,
                    version.Notes,
                    version.CreatedDate,
                    "C:\Research\replacement.docx",
                    True
                )

            End Sub
        )

        StringAssert.Contains(
            version.LocalFilePath,
            managedRoot
        )

        Assert.IsTrue(
            version.IsManagedCopy
        )

    End Sub


    <TestMethod>
    Public Sub EditedDecisionKeepsVersionAssociationAndUpdatesRouteMeaning()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 8, 23)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal A",
            .SubmittedDate = New DateTime(2026, 8, 1)
        }

        Dim decisionId As Guid =
            Guid.NewGuid()

        Dim withdrawn As New EditorialDecisionEvent With {
            .Id = decisionId,
            .DecisionDate = New DateTime(2026, 8, 23),
            .Decision = EditorialDecision.Withdrawn
        }

        submission.Decisions.Add(
            withdrawn
        )

        manuscript.Submissions.Add(
            submission
        )

        Dim version As ManuscriptVersion =
            ManuscriptVersionService.CreateVersion(
                manuscript,
                "Revision response",
                String.Empty,
                String.Empty,
                False,
                decisionId:=decisionId,
                revisionRoundNumber:=1,
                createdDate:=New DateTime(2026, 8, 23)
            )

        Assert.AreEqual(
            PaperStage.Draft,
            manuscript.CurrentStage
        )

        Dim revisedDecision As New EditorialDecisionEvent With {
            .Id = decisionId,
            .DecisionDate = withdrawn.DecisionDate,
            .Decision = EditorialDecision.MinorRevision
        }

        submission.Decisions(0) =
            revisedDecision

        ManuscriptLifecycleService.ApplyDecision(
            manuscript,
            submission,
            revisedDecision
        )

        Assert.AreEqual(
            PaperStage.Revision,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            decisionId,
            version.DecisionId.Value
        )

        Dim route As ManuscriptRoute =
            ManuscriptRouteProjectionService.Project(
                manuscript
            )

        Dim decisionWaypoint As ManuscriptRouteWaypoint =
            route.Waypoints.Find(
                Function(item)
                    Return item.DecisionId.HasValue AndAlso
                        item.DecisionId.Value =
                        decisionId
                End Function
            )

        Assert.IsNotNull(
            decisionWaypoint
        )

        Assert.AreEqual(
            EditorialDecision.MinorRevision,
            decisionWaypoint.Decision.Value
        )

        CollectionAssert.Contains(
            decisionWaypoint.RelatedVersionIds,
            version.Id
        )

    End Sub

End Class
