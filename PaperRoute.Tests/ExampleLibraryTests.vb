Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The example library for teaching (#83): every route a new researcher
' needs to see, fictional throughout, with dates relative to today.
<TestClass>
Public Class ExampleLibraryTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    <TestMethod>
    Public Sub TheExampleShowsEveryRouteANewResearcherNeedsToSee()
        Dim example = ExampleLibraryService.Create(Today)
        Dim manuscripts As List(Of Manuscript) = example.Manuscripts

        Dim published As Manuscript = manuscripts.Single(Function(item) item.Location = ManuscriptLocation.Published)
        Dim map As RouteMap = RouteMapService.Build(published, Today)
        Assert.IsTrue(map.Markers.Any(Function(item) item.Kind = RouteMarkerKind.Published), "A route that reaches publication.")
        Assert.AreEqual(0, map.DaysOf(RouteSegmentKind.NotRecorded), "Every stretch of the published route is recorded.")
        Assert.AreEqual(EditorialDecision.DeskRejected, published.Submissions(0).Decisions(0).Decision, "It starts with a desk rejection and a reroute.")
        Assert.AreEqual(2, map.Journals.Count)

        Dim statuses As IEnumerable(Of ReviewerResponseStatus) = manuscripts.SelectMany(Function(item) item.Submissions).
            SelectMany(Function(item) item.ReviewerResponses).Select(Function(item) item.Status).Distinct()
        Assert.AreEqual(4, statuses.Count(), "Reviewer comments at every stage of response.")
        Assert.IsTrue(manuscripts.Any(Function(item) item.CurrentStage = PaperStage.Revision AndAlso item.RevisionDeadline.HasValue))
        Assert.IsTrue(manuscripts.Any(Function(item) item.WorkType = WorkType.Preprint AndAlso item.Metadata.PreprintDoi.Length > 0), "A preprint.")
        Assert.IsTrue(manuscripts.Any(Function(item) item.Location = ManuscriptLocation.FileDrawer AndAlso item.FileDrawerReason.Length > 0), "The File Drawer.")
        Assert.IsTrue(manuscripts.Any(Function(item) JournalShortlistService.RerouteOfferFor(item, Today) IsNot Nothing), "A shortlist offering the next journal.")

        Dim groups As List(Of DeadlineGroup) = DeadlineService.Build(manuscripts, Today).Select(Function(item) item.Group).Distinct().ToList()
        For Each group As DeadlineGroup In {DeadlineGroup.Overdue, DeadlineGroup.Today, DeadlineGroup.Next7Days, DeadlineGroup.Later}
            CollectionAssert.Contains(groups, group, "Deadlines always has " & group.ToString() & " items.")
        Next
    End Sub

    <TestMethod>
    Public Sub TheExampleIsFictionalAndMovesWithToday()
        Dim example = ExampleLibraryService.Create(Today)
        Assert.IsTrue(example.Manuscripts.All(Function(item) item.Title.StartsWith("Example:", StringComparison.Ordinal)))
        Assert.IsTrue(example.Library.Journals.All(Function(item) item.Name.StartsWith("Fictional ", StringComparison.Ordinal)))
        Assert.IsTrue(example.Manuscripts.SelectMany(Function(item) item.Submissions).All(Function(item) item.JournalName.StartsWith("Fictional ", StringComparison.Ordinal)))
        Assert.IsTrue(example.Manuscripts.SelectMany(Function(item) item.SubmissionPackets).All(Function(item) item.JournalName.StartsWith("Fictional ", StringComparison.Ordinal)))
        Assert.IsTrue(example.Library.Authors.All(Function(item) item.Notes = "Fictional author."))
        Assert.IsTrue(example.Manuscripts.Where(Function(item) item.Metadata.Doi.Length > 0).All(Function(item) item.Metadata.Doi.StartsWith("10.5555/", StringComparison.Ordinal)),
                      "DOIs use the reserved example prefix.")

        Dim later = ExampleLibraryService.Create(Today.AddDays(30))
        Assert.AreEqual(example.Manuscripts(1).RevisionDeadline.Value.AddDays(30), later.Manuscripts(1).RevisionDeadline.Value, "Dates are relative to today.")
    End Sub

    ' #96: a submission packet to look at, as records only. The example has
    ' no files, so nothing points at a file on this computer, and the packet
    ' is linked to the submission it went with, so Deadlines gains nothing.
    <TestMethod>
    Public Sub TheExampleHasOneMetadataOnlySubmissionPacket()
        Dim example = ExampleLibraryService.Create(Today)
        Dim packets As List(Of SubmissionPacket) = example.Manuscripts.SelectMany(Function(item) item.SubmissionPackets).ToList()
        Assert.AreEqual(1, packets.Count)

        Dim packet As SubmissionPacket = packets(0)
        Dim published As Manuscript = example.Manuscripts.Single(Function(item) item.SubmissionPackets.Contains(packet))
        Assert.AreEqual(ManuscriptLocation.Published, published.Location)
        Dim submission As JournalSubmission = published.Submissions.Single(Function(item) item.Id = packet.SubmissionId.Value)
        Assert.AreEqual(submission.JournalId, packet.JournalId)
        Assert.IsTrue(packet.JournalName.StartsWith("Fictional ", StringComparison.Ordinal))
        Assert.IsTrue(published.Versions.Any(Function(item) item.Id = packet.ManuscriptVersionId AndAlso item.SubmissionId.HasValue AndAlso item.SubmissionId.Value = submission.Id),
                      "The packet holds the version sent with that submission.")
        Assert.IsTrue(packet.CreatedAtUtc.Date <= submission.SubmittedDate.Date, "Prepared before it was sent.")

        CollectionAssert.AreEquivalent(
            {SubmissionPacketFileRole.BlindedManuscript, SubmissionPacketFileRole.TitlePage, SubmissionPacketFileRole.CoverLetter, SubmissionPacketFileRole.DataAvailability},
            packet.Files.Select(Function(item) item.Role).ToList())
        Assert.IsTrue(packet.Files.All(Function(item) item.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly AndAlso
                                                      item.LocalFilePath.Length = 0 AndAlso item.OriginalFileName.Length = 0 AndAlso
                                                      Not item.FileSizeBytes.HasValue AndAlso String.IsNullOrEmpty(item.Sha256)),
                      "Metadata only: no file, path, size, or fingerprint.")

        Assert.IsFalse(DeadlineService.Build(example.Manuscripts, Today).Any(Function(item) item.Kind = DeadlineKind.Preparation),
                       "A packet linked to its submission adds no Preparation item.")

        ' Saving checks every packet against its manuscript, as Seed does.
        For Each manuscript As Manuscript In example.Manuscripts
            SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
        Next
    End Sub

    <TestMethod>
    Public Sub OnlyTheExampleArgumentOpensTheExample()
        Assert.IsTrue(ExampleLibraryService.IsExampleLaunch({"--example"}))
        Assert.IsTrue(ExampleLibraryService.IsExampleLaunch({"--EXAMPLE"}))
        Assert.IsFalse(ExampleLibraryService.IsExampleLaunch({}))
        Assert.IsFalse(ExampleLibraryService.IsExampleLaunch(Nothing))
        Assert.IsFalse(ExampleLibraryService.IsActive, "Tests never run inside an example session.")
    End Sub

End Class
