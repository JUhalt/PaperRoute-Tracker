Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The publication check (#61), with synthetic Crossref and ORCID answers.
' A check finds possible publications; only Mark Published changes a
' manuscript, and Fill Blanks never replaces a value.
<TestClass>
Public Class PublicationCheckTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)
    Private Const LongTitle As String = "A preregistered replication of anchoring effects in clinical risk estimates"

    <TestMethod>
    <DataRow("A Preregistered Replication of Anchoring Effects in Clinical Risk Estimates.", True)>
    <DataRow("A preregistered replication of anchoring effects in clinical-risk estimates", True)>
    <DataRow("Preregistered replication of anchoring effects in clinical risk estimates: Evidence from nurses", True)>
    <DataRow("Anchoring effects in consumer price estimates", False)>
    <DataRow("A replication of priming effects in social judgment", False)>
    <DataRow("Anchoring effects in clinical risk estimates", True)>
    <DataRow("Clinical risk", False)>
    Public Sub TitlesMatchRegardlessOfCasePunctuationAndSmallDifferences(candidate As String, expected As Boolean)
        Assert.AreEqual(expected, PublicationMatchService.IsSameTitle(LongTitle, candidate), PublicationMatchService.TitleSimilarity(LongTitle, candidate).ToString())
        Assert.AreEqual(1.0, PublicationMatchService.TitleSimilarity("Élan and naïve café", "elan AND naive cafe!"), "Accents and case do not matter.")
    End Sub

    <TestMethod>
    Public Sub OnlyWorkThatIsNotPublishedIsCheckedAndGoneToAJournalIsSuggested()
        Dim idea As New Manuscript With {.CurrentStage = PaperStage.Idea}
        Dim review As New Manuscript With {.CurrentStage = PaperStage.UnderReview}
        Dim drawer As New Manuscript With {.CurrentStage = PaperStage.Submitted, .Location = ManuscriptLocation.FileDrawer}
        Dim published As New Manuscript With {.CurrentStage = PaperStage.Published, .Location = ManuscriptLocation.Published}

        CollectionAssert.AreEqual({True, True, True, False}, {idea, review, drawer, published}.Select(Function(item) PublicationMatchService.IsEligible(item)).ToList())
        CollectionAssert.AreEqual({False, True, False, False}, {idea, review, drawer, published}.Select(Function(item) PublicationMatchService.IsSuggested(item)).ToList())
    End Sub

    <TestMethod>
    Public Sub ConsiderAcceptsOnlyPlausibleNewPublications()
        Dim manuscript As Manuscript = Tracked()
        manuscript.Metadata.PreprintDoi = "10.31234/osf.io/abcde"

        Assert.IsNotNull(PublicationMatchService.Consider(manuscript, Work("10.5555/a", LongTitle, "Collabra", Today.AddDays(-10)), PublicationMatchSource.Title))
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("10.5555/b", LongTitle, "PsyArXiv", Today, "posted-content"), PublicationMatchSource.Title), "A preprint is not a publication.")
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("10.5555/c", "Anchoring in pricing", "Collabra", Today), PublicationMatchSource.Title), "A different title is another work.")
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("10.5555/d", LongTitle, "Collabra", New DateTime(2024, 1, 1)), PublicationMatchSource.Title),
                      "A work published long before the first submission is an older one.")
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("10.31234/osf.io/abcde", LongTitle, "Collabra", Today), PublicationMatchSource.Title), "The manuscript's own preprint is not its publication.")
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("10.5555/e", LongTitle, "", Today), PublicationMatchSource.Doi), "A DOI without a venue is not yet published.")

        Dim shortTitle As New Manuscript With {.Title = "Introduction", .CurrentStage = PaperStage.Submitted}
        Assert.IsNull(PublicationMatchService.Consider(shortTitle, Work("10.5555/f", "Introduction", "Any Journal", Today), PublicationMatchSource.Title), "Short titles are too common to match.")

        manuscript.PublicationMatches.Add(New PublicationMatch With {.Doi = "10.5555/A", .Status = PublicationMatchStatus.Ignored})
        Assert.IsNull(PublicationMatchService.Consider(manuscript, Work("https://doi.org/10.5555/a", LongTitle, "Collabra", Today), PublicationMatchSource.Title), "An ignored match never returns.")
    End Sub

    <TestMethod>
    Public Sub MarkPublishedRecordsALifecycleEventAndFillsOnlyBlanks()
        Dim manuscript As Manuscript = Tracked()
        manuscript.Location = ManuscriptLocation.FileDrawer
        manuscript.FileDrawerReason = "Waiting"
        manuscript.Metadata.Pages = "e123"
        Dim submissions As String = JsonSerializer.Serialize(manuscript.Submissions)
        Dim match As New PublicationMatch With {
            .Doi = "10.1525/collabra.1", .Title = LongTitle, .Journal = "Collabra: Psychology",
            .PublishedDate = Today.AddDays(-12), .Volume = "12", .Pages = "1-20", .Url = "https://online.ucpress.edu/collabra/article/12/1/1"
        }
        Dim other As New PublicationMatch With {.Doi = "10.5555/other", .Journal = "Other"}
        manuscript.PublicationMatches.AddRange({match, other})

        Dim described As List(Of String) = PublicationMatchService.DescribeMarkPublished(manuscript, match, Today)
        StringAssert.Contains(described(0), "Published shelf")
        Assert.IsTrue(described.Any(Function(line) line.Contains("from Open Psychology Letters to Collabra: Psychology")))
        StringAssert.Contains(described.Last(), "DOI, Journal, Publication date, Volume, Publication URL")

        PublicationMatchService.MarkPublished(manuscript, match, Today)

        Assert.AreEqual(PaperStage.Published, manuscript.CurrentStage)
        Assert.AreEqual(ManuscriptLocation.Published, manuscript.Location)
        Assert.AreEqual(Today.AddDays(-12), manuscript.StageEnteredDate)
        Assert.AreEqual("Collabra: Psychology", manuscript.TargetJournal)
        Assert.IsFalse(manuscript.TargetJournalId.HasValue, "A changed journal no longer points at the old Journal Library record.")
        Assert.AreEqual(String.Empty, manuscript.FileDrawerReason)
        Dim published As HistoryEvent = manuscript.History.Last()
        Assert.AreEqual(PaperStage.Published, published.Stage)
        StringAssert.Contains(published.Note, "Published in Collabra: Psychology (DOI 10.1525/collabra.1)")
        Assert.IsTrue(published.RecordedAtUtc.HasValue)
        Assert.AreEqual("10.1525/collabra.1", manuscript.Metadata.Doi)
        Assert.AreEqual("12", manuscript.Metadata.Volume)
        Assert.AreEqual("e123", manuscript.Metadata.Pages, "A field with a value is kept.")
        Assert.AreEqual(submissions, JsonSerializer.Serialize(manuscript.Submissions), "Submissions and decisions are left as recorded.")
        Assert.AreEqual(PublicationMatchStatus.Confirmed, match.Status)
        Assert.AreEqual(PublicationMatchStatus.Ignored, other.Status, "Other possible matches no longer need review.")
    End Sub

    <TestMethod>
    Public Sub FillPlansOnlyEmptyFields()
        Dim manuscript As New Manuscript With {.Title = "Grit scale"}
        manuscript.Metadata.Doi = "10.5555/grit"
        manuscript.Metadata.PublicationJournal = "Assessment"
        manuscript.Metadata.Keywords.Add("grit")
        Dim record As CrossrefMetadataSuggestion = Work("10.5555/grit", "Grit scale", "Another name for the journal", Today)
        record.Volume = "33"
        record.AbstractText = "An abstract."
        record.Keywords = New List(Of String) From {"personality"}

        Dim plan As List(Of MetadataFill) = PublicationMatchService.PlanFill(manuscript, record)

        CollectionAssert.AreEqual({"Publication date", "Volume", "Abstract"}, plan.Select(Function(fill) fill.Field).ToList())
        PublicationMatchService.ApplyFill(manuscript, plan)
        Assert.AreEqual("Assessment", manuscript.Metadata.PublicationJournal)
        Assert.AreEqual("33", manuscript.Metadata.Volume)
        CollectionAssert.AreEqual({"grit"}, manuscript.Metadata.Keywords)
        Assert.AreEqual(0, PublicationMatchService.PlanFill(manuscript, record).Count, "Filling twice changes nothing.")
    End Sub

    <TestMethod>
    Public Sub SearchResultsAndPreprintRelationsAreRead()
        Dim search As String =
            "{""message"":{""items"":[" &
            "{""DOI"":""10.5555/one"",""title"":[""One""],""container-title"":[""Journal A""],""type"":""journal-article"",""volume"":""4"",""issued"":{""date-parts"":[[2026,5]]}}," &
            "{""title"":[""No DOI""]}," &
            "{""DOI"":""10.31234/two"",""title"":[""Two""],""type"":""posted-content"",""relation"":{""is-preprint-of"":[{""id-type"":""doi"",""id"":""10.5555/two-published""},{""id-type"":""uri"",""id"":""https://example.org""}]}}" &
            "]}}"

        Dim works As List(Of CrossrefMetadataSuggestion) = CrossrefClient.ParseSearchJson(search)

        Assert.AreEqual(2, works.Count, "Items without a DOI are skipped.")
        Assert.AreEqual("journal-article", works(0).WorkType)
        Assert.AreEqual("4", works(0).Volume)
        Assert.AreEqual(New DateTime(2026, 5, 1), works(0).PublishedDate)
        CollectionAssert.AreEqual({"10.5555/two-published"}, works(1).PublishedVersionDois)
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() CrossrefClient.ParseSearchJson("{""message"":{}}"))
    End Sub

    <TestMethod>
    Public Sub CheckLooksByDoiPreprintTitleAndOrcidWithoutChangingManuscripts()
        Dim byDoi As Manuscript = Tracked("Measurement invariance of a short grit scale across four countries")
        byDoi.Metadata.Doi = "10.5555/grit"
        Dim byPreprint As Manuscript = Tracked("Sleep and emotional memory consolidation in older adults")
        byPreprint.Metadata.PreprintDoi = "10.31234/sleep"
        Dim byTitle As Manuscript = Tracked(LongTitle)
        Dim byOrcid As Manuscript = Tracked("Teaching open science to nursing students with a mixed methods evaluation")
        Dim nothingFound As Manuscript = Tracked("Attention capture by salient distractors under working memory load")
        Dim published As New Manuscript With {.Title = "Already published work in this library", .CurrentStage = PaperStage.Published, .Location = ManuscriptLocation.Published}

        Dim source As New FakeSource()
        source.Dois("10.5555/grit") = Work("10.5555/grit", byDoi.Title, "Assessment", Today.AddDays(-3))
        Dim preprint As CrossrefMetadataSuggestion = Work("10.31234/sleep", byPreprint.Title, "PsyArXiv", Today.AddDays(-90), "posted-content")
        preprint.PublishedVersionDois.Add("10.5555/sleep")
        source.Dois("10.31234/sleep") = preprint
        source.Dois("10.5555/sleep") = Work("10.5555/sleep", "Sleep and emotional memory consolidation in older adults", "Aging & Cognition", Today.AddDays(-5))
        source.Titles(LongTitle) = New List(Of CrossrefMetadataSuggestion) From {
            Work("10.5555/near", "Anchoring effects in clinical settings", "Medical Decision Making", Today),
            Work("10.5555/anchoring", LongTitle, "Collabra: Psychology", Today.AddDays(-1))
        }
        source.Works.Add(New OrcidWorkSuggestion With {.Title = byOrcid.Title, .JournalTitle = "Nurse Education Today", .PublishedDate = Today.AddDays(-20), .WorkType = "journal-article"})

        Dim library As New List(Of Manuscript) From {byDoi, byPreprint, byTitle, byOrcid, nothingFound, published}
        Dim before As String = JsonSerializer.Serialize(library)

        Dim result As PublicationCheckResult = PublicationCheckService.CheckAsync(library, "0000-0002-1825-0097", source).GetAwaiter().GetResult()

        Assert.AreEqual(before, JsonSerializer.Serialize(library), "A check never changes a manuscript.")
        Assert.AreEqual(5, result.Checked, "Published work is not checked.")
        Assert.AreEqual(0, result.Failures.Count)
        Dim found = result.Matches.ToDictionary(Function(entry) entry.Manuscript, Function(entry) entry.Match)
        Assert.AreEqual(PublicationMatchSource.Doi, found(byDoi).Source)
        Assert.AreEqual("10.5555/sleep", found(byPreprint).Doi)
        Assert.AreEqual(PublicationMatchSource.Preprint, found(byPreprint).Source)
        Assert.AreEqual("10.5555/anchoring", found(byTitle).Doi, "The closest title wins.")
        Assert.AreEqual(PublicationMatchSource.Orcid, found(byOrcid).Source)
        Assert.AreEqual("Nurse Education Today", found(byOrcid).Journal)
        Assert.IsFalse(found.ContainsKey(nothingFound))
        Assert.AreEqual(1, source.OrcidReads, "The ORCID record is read once per check.")
    End Sub

    <TestMethod>
    Public Sub NetworkTroubleSkipsOneManuscriptAndRateLimitingStopsKeepingWhatWasFound()
        Dim first As Manuscript = Tracked(LongTitle)
        Dim broken As Manuscript = Tracked("Measurement invariance of a short grit scale across four countries")
        Dim limited As Manuscript = Tracked("Sleep and emotional memory consolidation in older adults")
        Dim never As Manuscript = Tracked("Teaching open science to nursing students with a mixed methods evaluation")

        Dim source As New FakeSource()
        source.Titles(LongTitle) = New List(Of CrossrefMetadataSuggestion) From {Work("10.5555/anchoring", LongTitle, "Collabra", Today)}
        source.Failures(broken.Title) = New HttpRequestException("No connection.")
        source.Failures(limited.Title) = New CrossrefRateLimitException()

        Dim result As PublicationCheckResult =
            PublicationCheckService.CheckAsync(New List(Of Manuscript) From {first, broken, limited, never}, Nothing, source).GetAwaiter().GetResult()

        Assert.AreEqual(1, result.Matches.Count, "What was found before stopping is kept.")
        Assert.AreSame(broken, result.Failures.Single().Manuscript)
        StringAssert.Contains(result.Failures.Single().Reason, "No connection")
        StringAssert.Contains(result.StoppedReason, "rate-limiting")
        Assert.IsFalse(source.Searched.Contains(never.Title), "Nothing more is asked after Crossref asks PaperRoute to slow down.")
    End Sub

    <TestMethod>
    Public Sub PendingMatchesAppearOnDeadlinesUntilReviewed()
        Dim manuscript As Manuscript = Tracked(LongTitle)
        Dim pending As New PublicationMatch With {.Doi = "10.5555/a", .Journal = "Collabra: Psychology"}
        manuscript.PublicationMatches.AddRange({pending, New PublicationMatch With {.Doi = "10.5555/b", .Status = PublicationMatchStatus.Ignored}})

        Dim item As DeadlineItem = DeadlineService.Build({manuscript}, Today).Single(Function(entry) entry.Kind = DeadlineKind.Publication)
        Assert.AreEqual(DeadlineGroup.NoDate, item.Group)
        Assert.AreEqual("May have been published in Collabra: Psychology", item.Title)
        Assert.AreEqual(pending.Id, item.PublicationMatchId)
        Assert.AreEqual(0, DeadlineService.CountDueNow({manuscript}, Today), "Possible publications have no date and never count as due.")

        PublicationMatchService.Ignore(pending)
        Assert.IsFalse(DeadlineService.Build({manuscript}, Today).Any(Function(entry) entry.Kind = DeadlineKind.Publication))
    End Sub

    <TestMethod>
    Public Sub InvalidStoredMatchesAreRejectedAndMissingListsAreEmpty()
        Dim manuscript As New Manuscript With {.PublicationMatches = Nothing}
        PublicationMatchService.NormalizeAndValidateManuscript(manuscript)
        Assert.AreEqual(0, manuscript.PublicationMatches.Count)

        manuscript.PublicationMatches.Add(New PublicationMatch With {.Status = CType(9, PublicationMatchStatus)})
        Assert.ThrowsExactly(Of System.IO.InvalidDataException)(Sub() PublicationMatchService.NormalizeAndValidateManuscript(manuscript))
        manuscript.PublicationMatches(0) = Nothing
        Assert.ThrowsExactly(Of System.IO.InvalidDataException)(Sub() PublicationMatchService.NormalizeAndValidateManuscript(manuscript))
    End Sub

    ' Under review at a second journal after a first submission in 2026.
    Private Shared Function Tracked(Optional title As String = LongTitle) As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = title,
            .CurrentStage = PaperStage.UnderReview,
            .Location = ManuscriptLocation.Pipeline,
            .TargetJournal = "Open Psychology Letters",
            .TargetJournalId = Guid.NewGuid()
        }
        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "Open Psychology Letters", .SubmittedDate = New DateTime(2026, 2, 1)})
        Return manuscript
    End Function

    Private Shared Function Work(doi As String, title As String, journal As String, published As DateTime?, Optional type As String = "journal-article") As CrossrefMetadataSuggestion
        Return New CrossrefMetadataSuggestion With {.Doi = doi, .Title = title, .Journal = journal, .PublishedDate = published, .WorkType = type}
    End Function

    Private NotInheritable Class FakeSource
        Implements IPublicationSource

        Public ReadOnly Dois As New Dictionary(Of String, CrossrefMetadataSuggestion)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly Titles As New Dictionary(Of String, List(Of CrossrefMetadataSuggestion))()
        Public ReadOnly Failures As New Dictionary(Of String, Exception)()
        Public ReadOnly Works As New List(Of OrcidWorkSuggestion)()
        Public ReadOnly Searched As New List(Of String)()
        Public OrcidReads As Integer

        Public Function LookupDoiAsync(doi As String, cancellationToken As CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Dim work As CrossrefMetadataSuggestion = Nothing
            Dois.TryGetValue(doi, work)
            Return Task.FromResult(work)
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Searched.Add(title)
            Dim failure As Exception = Nothing
            If Failures.TryGetValue(title, failure) Then Return Task.FromException(Of List(Of CrossrefMetadataSuggestion))(failure)
            Dim found As List(Of CrossrefMetadataSuggestion) = Nothing
            Return Task.FromResult(If(Titles.TryGetValue(title, found), found, New List(Of CrossrefMetadataSuggestion)()))
        End Function

        Public Function OrcidWorksAsync(orcid As String, cancellationToken As CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            OrcidReads += 1
            Return Task.FromResult(Works)
        End Function
    End Class

End Class
