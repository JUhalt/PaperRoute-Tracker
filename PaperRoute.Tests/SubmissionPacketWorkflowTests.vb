Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionPacketWorkflowTests

    <TestMethod>
    Public Sub RecordSubmission_UnresolvedReadinessIsAdvisoryAndInputRemainsIsolated()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        Dim packetId As Guid = manuscript.SubmissionPackets.Single().Id
        Dim readinessBefore As String = Snapshot(manuscript.ReadinessProfiles)
        Dim versionsBefore As String = Snapshot(manuscript.Versions)
        Dim historyBefore As String = Snapshot(manuscript.History)
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)
        Dim proposedBefore As String = Snapshot(proposed)

        SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript, packetId, proposed)

        Assert.AreEqual(1, manuscript.Submissions.Count)
        Assert.AreEqual(proposed.Id, manuscript.SubmissionPackets.Single().SubmissionId.Value)
        Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage)
        Assert.AreEqual(proposed.SubmittedDate.Date, manuscript.StageEnteredDate.Date)
        Assert.AreEqual(proposed.JournalId, manuscript.TargetJournalId)
        Assert.AreEqual(readinessBefore, Snapshot(manuscript.ReadinessProfiles))
        Assert.AreEqual(versionsBefore, Snapshot(manuscript.Versions))
        Assert.AreEqual(historyBefore, Snapshot(manuscript.History))
        Assert.AreEqual(ReadinessItemStatus.Unresolved, manuscript.ReadinessProfiles.Single().Items.Single().Status)
        Assert.AreEqual(proposedBefore, Snapshot(proposed))
        Assert.AreNotSame(proposed, manuscript.Submissions.Single())
        proposed.Notes = "Unadopted caller edit"
        Assert.AreNotEqual(proposed.Notes, manuscript.Submissions.Single().Notes)
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_CancelingOuterWorkingCopyLeavesOriginalUntouched()
        Dim original As Manuscript = CreatePreparedManuscript()
        Dim before As String = Snapshot(original)
        Dim working As Manuscript = ManuscriptCloneService.CloneManuscript(original)

        SubmissionPacketWorkflowService.RecordSubmissionForPacket(working,
            working.SubmissionPackets.Single().Id, CreateSubmission(working))

        Assert.AreEqual(1, working.Submissions.Count)
        Assert.AreEqual(before, Snapshot(original),
            "Discarding the outer edit must discard the recorded submission, packet association, and lifecycle changes.")
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_AlreadyLinkedPacketRejectsWithoutMutation()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        Dim packetId As Guid = manuscript.SubmissionPackets.Single().Id
        SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript, packetId, CreateSubmission(manuscript))
        Dim before As String = Snapshot(manuscript)
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript, packetId, proposed))

        Assert.AreEqual(before, Snapshot(manuscript))
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_ExistingSubmissionIdRejectsWithoutMutation()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)
        manuscript.Submissions.Add(proposed)
        Dim before As String = Snapshot(manuscript)

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
                manuscript.SubmissionPackets.Single().Id, proposed))

        Assert.AreEqual(before, Snapshot(manuscript))
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_JournalConflictRejectsWholeCandidateBeforeAdoption()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        manuscript.SubmissionPackets.Single().JournalId = Nothing
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)
        proposed.JournalId = Guid.NewGuid()
        proposed.JournalName = "Different reusable journal"
        Dim before As String = Snapshot(manuscript)
        Dim proposedBefore As String = Snapshot(proposed)

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub() SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
                manuscript.SubmissionPackets.Single().Id, proposed))

        Assert.AreEqual(before, Snapshot(manuscript))
        Assert.AreEqual(proposedBefore, Snapshot(proposed))
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_KnownPreparedJournalConflictWithoutReadinessRejectsAtomically()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        manuscript.SubmissionPackets.Single().ReadinessProfileId = Nothing
        manuscript.ReadinessProfiles.Clear()
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)
        proposed.JournalId = Guid.NewGuid()
        proposed.JournalName = "Different reusable journal"
        Dim before As String = Snapshot(manuscript)

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
                manuscript.SubmissionPackets.Single().Id, proposed))

        Assert.AreEqual(before, Snapshot(manuscript), "Preflight must run before UpdatePacket can replace the prepared journal snapshot.")
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_MissingExactVersionRejectsWithoutPartialSubmission()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        manuscript.SubmissionPackets.Single().ManuscriptVersionId = Guid.NewGuid()
        Dim before As String = Snapshot(manuscript)

        Assert.ThrowsExactly(Of ArgumentException)(
            Sub() SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
                manuscript.SubmissionPackets.Single().Id, CreateSubmission(manuscript)))

        Assert.AreEqual(before, Snapshot(manuscript))
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_DoesNotRewriteExactVersionsEarlierSubmissionOrDecisionProvenance()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        Dim earlier As JournalSubmission = CreateSubmission(manuscript)
        earlier.SubmittedDate = New DateTime(2026, 9, 1)
        Dim decision As New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 2)
        }
        earlier.Decisions.Add(decision)
        manuscript.Submissions.Add(earlier)
        Dim version As ManuscriptVersion = manuscript.Versions.Single()
        version.SubmissionId = earlier.Id
        version.DecisionId = decision.Id
        version.RevisionRoundNumber = 2
        Dim versionBefore As String = Snapshot(version)
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)

        SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
            manuscript.SubmissionPackets.Single().Id, proposed)

        Assert.AreSame(version, manuscript.Versions.Single())
        Assert.AreEqual(versionBefore, Snapshot(version))
        Assert.AreEqual(version.Id, manuscript.SubmissionPackets.Single().ManuscriptVersionId)
        Assert.AreEqual(proposed.Id, manuscript.SubmissionPackets.Single().SubmissionId.Value)
        Assert.AreEqual(2, manuscript.Submissions.Count)
    End Sub

    <TestMethod>
    Public Sub RecordSubmission_BackdatedEntryDoesNotRegressNewerAcceptedState()
        Dim manuscript As Manuscript = CreatePreparedManuscript()
        Dim existing As JournalSubmission = CreateSubmission(manuscript)
        existing.Decisions.Add(New EditorialDecisionEvent With {
            .Decision = EditorialDecision.Accepted, .DecisionDate = New DateTime(2026, 9, 20)
        })
        manuscript.Submissions.Add(existing)
        manuscript.CurrentStage = PaperStage.Accepted
        manuscript.StageEnteredDate = New DateTime(2026, 9, 20)
        Dim proposed As JournalSubmission = CreateSubmission(manuscript)
        proposed.SubmittedDate = New DateTime(2026, 9, 15)

        SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript,
            manuscript.SubmissionPackets.Single().Id, proposed)

        Assert.AreEqual(PaperStage.Accepted, manuscript.CurrentStage)
        Assert.AreEqual(New DateTime(2026, 9, 20), manuscript.StageEnteredDate)
        Assert.AreEqual(proposed.Id, manuscript.SubmissionPackets.Single().SubmissionId.Value)
        Assert.AreEqual(2, manuscript.Submissions.Count)
    End Sub

    <TestMethod>
    Public Sub PortableRestore_ReconstructsInitialAndRevisionPacketsWithExactMembershipAndFingerprints()
        Dim root As String = CreateTemporaryRoot()
        Try
            Dim manuscript As Manuscript = CreatePreparedManuscript()
            Dim firstPacket As SubmissionPacket = manuscript.SubmissionPackets.Single()
            Dim firstPath As String = Path.Combine(root, "initial-manuscript.txt")
            File.WriteAllText(firstPath, "Exact initial manuscript bytes")
            Dim initialFile As SubmissionPacketFile = SubmissionPacketService.AddFile(firstPacket,
                SubmissionPacketFileRole.Manuscript, "Initial manuscript", String.Empty,
                firstPath, SubmissionPacketFileStorageMode.ManagedCopy)
            SubmissionPacketIntegrityService.CaptureBaseline(initialFile)
            Dim proposed As JournalSubmission = CreateSubmission(manuscript)
            SubmissionPacketWorkflowService.RecordSubmissionForPacket(manuscript, firstPacket.Id, proposed)
            firstPacket = manuscript.SubmissionPackets.Single()
            Dim revisedVersion As New ManuscriptVersion With {
                .Label = "Round two version", .SubmissionId = proposed.Id, .RevisionRoundNumber = 2
            }
            manuscript.Versions.Add(revisedVersion)
            Dim revisionPacket As SubmissionPacket = SubmissionPacketService.CreatePacket(manuscript,
                revisedVersion.Id, "Round two packet", "Responded to review",
                manuscript.ReadinessProfiles.Single().Id, proposed.Id, 2)
            Dim revisionPath As String = Path.Combine(root, "revision-response.txt")
            File.WriteAllText(revisionPath, "Exact round two response bytes")
            Dim revisionFile As SubmissionPacketFile = SubmissionPacketService.AddFile(revisionPacket,
                SubmissionPacketFileRole.ResponseToReviewers, "Response to reviewers", "Round two response",
                revisionPath, SubmissionPacketFileStorageMode.ManagedCopy)
            SubmissionPacketIntegrityService.CaptureBaseline(revisionFile)

            Dim sourceData As String = Path.Combine(root, "source-data")
            Dim sourceManaged As String = Path.Combine(root, "source-managed")
            Dim repository As New ManuscriptRepository(sourceData, sourceManaged)
            Dim manuscripts As New List(Of Manuscript) From {manuscript}
            repository.Save(manuscripts)
            Dim authorRepository As New AuthorLibraryRepository(sourceData)
            authorRepository.Save(New AuthorLibraryData())
            Dim backupPath As String = Path.Combine(root, "two-rounds.zip")
            Dim backup As New PortableBackupService(sourceManaged)
            backup.CreateBackup(backupPath, manuscripts, repository)
            Dim targetManaged As String = Path.Combine(root, "restored-managed")
            Dim targetRepository As New ManuscriptRepository(Path.Combine(root, "restored-data"), targetManaged)
            Dim restore As New PortableRestoreService(targetManaged)
            restore.RestoreBackup(backupPath, New List(Of Manuscript)(), targetRepository)
            Dim restored As Manuscript = targetRepository.Load().Single()

            Assert.AreEqual(1, restored.Submissions.Count, "Revision packets belong to the existing real submission.")
            Assert.AreEqual(2, restored.SubmissionPackets.Count)
            Assert.AreEqual(2, restored.Versions.Count)
            For Each expected As SubmissionPacket In manuscript.SubmissionPackets
                Dim actual As SubmissionPacket = restored.SubmissionPackets.Single(Function(item) item.Id = expected.Id)
                Assert.AreEqual(expected.ManuscriptVersionId, actual.ManuscriptVersionId)
                Assert.AreEqual(proposed.Id, actual.SubmissionId.Value)
                Assert.AreEqual(expected.RevisionRoundNumber, actual.RevisionRoundNumber)
                Assert.AreEqual(expected.ReadinessProfileId, actual.ReadinessProfileId)
                Dim expectedFile As SubmissionPacketFile = expected.Files.Single()
                Dim actualFile As SubmissionPacketFile = actual.Files.Single()
                Assert.AreEqual(expectedFile.Id, actualFile.Id)
                Assert.AreEqual(expectedFile.Role, actualFile.Role)
                Assert.AreEqual(expectedFile.Notes, actualFile.Notes)
                Assert.AreEqual(expectedFile.Sha256, actualFile.Sha256)
                Assert.AreNotEqual(expectedFile.LocalFilePath, actualFile.LocalFilePath)
                CollectionAssert.AreEqual(File.ReadAllBytes(expectedFile.LocalFilePath), File.ReadAllBytes(actualFile.LocalFilePath))
                Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.Verify(actualFile).Status)
            Next
            Assert.AreEqual("Exact initial manuscript bytes", File.ReadAllText(firstPath))
            Assert.AreEqual("Exact round two response bytes", File.ReadAllText(revisionPath))
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    Private Shared Function Snapshot(Of T)(value As T) As String
        Return JsonSerializer.Serialize(value, CreateJsonOptions())
    End Function

    Private Shared Function CreatePreparedManuscript() As Manuscript
        Dim journalId As Guid = Guid.NewGuid()
        Dim manuscript As New Manuscript With {
            .Title = "Connected submission workflow", .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline, .StageEnteredDate = New DateTime(2026, 9, 10),
            .TargetJournalId = journalId, .TargetJournal = "Journal of Packet Workflows"
        }
        Dim version As New ManuscriptVersion With {.Label = "Exact prepared version"}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id
        Dim readiness As New ManuscriptReadiness With {.JournalId = journalId, .JournalName = manuscript.TargetJournal}
        readiness.Items.Add(New ReadinessItemState With {
            .Title = "Author approval", .IsRequired = True, .Status = ReadinessItemStatus.Unresolved
        })
        manuscript.ReadinessProfiles.Add(readiness)
        SubmissionPacketService.CreatePacket(manuscript, version.Id, "Prepared packet", "Preparation only", readiness.Id)
        Return manuscript
    End Function

    Private Shared Function CreateSubmission(manuscript As Manuscript) As JournalSubmission
        Return New JournalSubmission With {
            .JournalId = manuscript.TargetJournalId, .JournalName = manuscript.TargetJournal,
            .SubmittedDate = New DateTime(2026, 9, 12), .Notes = "Explicitly recorded by the user"
        }
    End Function

End Class
