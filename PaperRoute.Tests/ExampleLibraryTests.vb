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
        Assert.IsTrue(example.Library.Authors.All(Function(item) item.Notes = "Fictional author."))
        Assert.IsTrue(example.Manuscripts.Where(Function(item) item.Metadata.Doi.Length > 0).All(Function(item) item.Metadata.Doi.StartsWith("10.5555/", StringComparison.Ordinal)),
                      "DOIs use the reserved example prefix.")

        Dim later = ExampleLibraryService.Create(Today.AddDays(30))
        Assert.AreEqual(example.Manuscripts(1).RevisionDeadline.Value.AddDays(30), later.Manuscripts(1).RevisionDeadline.Value, "Dates are relative to today.")
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
