Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Your citations (#91), on answers recorded from ORCID and OpenAlex on
' September 30, 2026, for ORCID's fictional test researcher Josiah Carberry
' (0000-0002-1825-0097). No test reaches the network.
<TestClass>
<DoNotParallelize>
Public Class CitationsTests

    Private Const Carberry As String = "0000-0002-1825-0097"
    Private _directory As String

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        _directory = TestSupport.CreateTemporaryRoot()
        IsolateKeys()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub

    Private Sub IsolateKeys()
        Dim keys As New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
    End Sub


    ' ---------------------------------------------------------------
    ' Figures
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheFiguresFollowTheirDefinitions()
        Dim summary As CitationSummary = CitationMetricsService.Summarize(Works((10, 2020), (8, 2021), (5, 2022), (4, 2023), (3, 2024)), 2026)
        Assert.AreEqual(5, summary.Works)
        Assert.AreEqual(30L, summary.Citations)
        Assert.AreEqual(4, summary.HIndex, "Four works cited at least four times; the fifth has 3.")
        Assert.AreEqual(1, summary.I10Index)
        Assert.AreEqual(5, summary.GIndex, "30 citations in the top 5 is at least 25.")
        Assert.AreEqual(2020, summary.FirstYear)
        Assert.AreEqual(0.57, summary.MQuotient.Value, 0.0001, "4 over the 7 years 2020 to 2026.")

        Dim one As CitationSummary = CitationMetricsService.Summarize(Works((100, 2025)), 2026)
        Assert.AreEqual(1, one.HIndex)
        Assert.AreEqual(1, one.GIndex, "g is at most the number of works.")
        Assert.AreEqual(1, one.I10Index)
        Assert.AreEqual(0.5, one.MQuotient.Value, 0.0001)

        Dim uncited As CitationSummary = CitationMetricsService.Summarize(Works((0, 2024), (0, 2025)), 2026)
        Assert.AreEqual(0, uncited.HIndex)
        Assert.AreEqual(0, uncited.GIndex)
        Assert.AreEqual(0L, uncited.Citations)

        Dim none As CitationSummary = CitationMetricsService.Summarize(Nothing, 2026)
        Assert.AreEqual(0, none.Works)
        Assert.IsFalse(none.MQuotient.HasValue, "No year, no m-quotient.")
        Assert.IsFalse(CitationMetricsService.Summarize({New CitedWork With {.CitedByCount = 3}}, 2026).MQuotient.HasValue)
    End Sub

    <TestMethod>
    Public Sub CitationsByYearCoverTheLastTenYearsWithZeros()
        Dim first As New CitedWork With {.CountsByYear = New List(Of YearCount) From {YearOf(2026, 2), YearOf(2025, 3)}}
        Dim second As New CitedWork With {.CountsByYear = New List(Of YearCount) From {YearOf(2025, 1), YearOf(2014, 5)}}
        Dim years As List(Of YearCount) = CitationMetricsService.PerYear({first, second, Nothing}, 2026)
        CollectionAssert.AreEqual(Enumerable.Range(2017, 10).ToList(), years.Select(Function(item) item.Year).ToList())
        Assert.AreEqual(4, years.Single(Function(item) item.Year = 2025).Count)
        Assert.AreEqual(2, years.Single(Function(item) item.Year = 2026).Count)
        Assert.AreEqual(0, years.Single(Function(item) item.Year = 2017).Count)
        Assert.AreEqual(6, years.Sum(Function(item) item.Count), "2014 is outside the ten years.")
    End Sub

    <TestMethod>
    Public Sub PercentileAndFwciReadPlainly()
        Assert.AreEqual("89th (top 10%)", CitationsService.PercentileText(New CitedWork With {.Percentile = 0.894, .InTop10Percent = True}))
        Assert.AreEqual("99th (top 1%)", CitationsService.PercentileText(New CitedWork With {.Percentile = 0.995, .InTop1Percent = True, .InTop10Percent = True}))
        Assert.AreEqual("11th", CitationsService.PercentileText(New CitedWork With {.Percentile = 0.115}))
        Assert.AreEqual("42nd", CitationsService.PercentileText(New CitedWork With {.Percentile = 0.42}))
        Assert.AreEqual("Not available", CitationsService.PercentileText(New CitedWork()))
        Assert.AreEqual(1.47.ToString("N2") & " (provisional)", CitationsService.FwciText(New CitedWork With {.Fwci = 1.4712, .Year = 2024}, 2026))
        Assert.AreEqual(0.23.ToString("N2"), CitationsService.FwciText(New CitedWork With {.Fwci = 0.231, .Year = 1987}, 2026))
        Assert.AreEqual("Not available", CitationsService.FwciText(New CitedWork With {.Year = 2026}, 2026))
    End Sub


    ' ---------------------------------------------------------------
    ' Reading the answers
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub OrcidGroupsGiveOneEntryPerWork()
        Dim groups As List(Of OrcidWorkGroup) = OrcidClient.ParseWorkGroups(Fixture("orcid_works.json"))
        Assert.AreEqual(6, groups.Count, "Two versions of one work form one group.")
        CollectionAssert.AreEqual({"10.5555/12345680"}, groups(0).Dois)
        StringAssert.StartsWith(groups(0).Title, "A Methodology for the Emulation of Archi")
        Assert.AreEqual(2012, groups(0).Year)
        CollectionAssert.AreEqual({"10.1109/tps.1987.4316723"}, groups(5).Dois, "DOIs are normalized and lower-case; the Scopus id is ignored.")

        Dim withoutDoi As String = "{""group"":[{""external-ids"":{""external-id"":[{""external-id-type"":""isbn"",""external-id-value"":""9780000000002"",""external-id-relationship"":""self""}]}," &
                                   """work-summary"":[{""display-index"":""1"",""title"":{""title"":{""value"":""A book""}},""type"":""book"",""publication-date"":{""year"":{""value"":""2020""}}}]}," &
                                   "{""external-ids"":{""external-id"":[{""external-id-type"":""doi"",""external-id-value"":""10.1234/BOOK"",""external-id-relationship"":""part-of""}," &
                                   "{""external-id-type"":""doi"",""external-id-value"":""https://doi.org/10.1234/Chapter.2"",""external-id-relationship"":""self""}]}," &
                                   """work-summary"":[{""display-index"":""0"",""title"":{""title"":{""value"":""Old title""}}},{""display-index"":""5"",""title"":{""title"":{""value"":""A chapter""}}}]}]}"
        Dim parsed As List(Of OrcidWorkGroup) = OrcidClient.ParseWorkGroups(withoutDoi)
        Assert.AreEqual(0, parsed(0).Dois.Count)
        Assert.AreEqual("A book", parsed(0).Title)
        CollectionAssert.AreEqual({"10.1234/chapter.2"}, parsed(1).Dois, "A part-of DOI (the book's) is left out.")
        Assert.AreEqual("A chapter", parsed(1).Title, "The preferred version's title.")

        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() OrcidClient.ParseWorkGroups("{""unexpected"":true}"))
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() OrcidClient.ParseWorkGroups("not json"))
    End Sub

    <TestMethod>
    Public Sub OpenAlexWorksCarryTheirFigures()
        Dim works As List(Of CitedWork) = CitationsService.ParseWorks(Fixture("works_edgecases.json"))
        Assert.AreEqual(9, works.Count, "A DOI OpenAlex doesn't know is simply missing.")
        Dim plos As CitedWork = works.Single(Function(item) item.Doi = "10.1371/journal.pone.0000001")
        Assert.AreEqual("W2066445331", plos.OpenAlexId)
        Assert.AreEqual(167, plos.CitedByCount)
        Assert.AreEqual(2006, plos.Year)
        Assert.AreEqual(10.7333, plos.Fwci.Value, 0.0001)
        Assert.AreEqual(0.98409489, plos.Percentile.Value, 0.000001)
        Assert.IsTrue(plos.InTop10Percent)
        Assert.IsFalse(plos.InTop1Percent)
        Assert.AreEqual(15, plos.CountsByYear.Count)
        Assert.IsFalse(String.IsNullOrEmpty(plos.Journal))

        Dim recent As CitedWork = works.Single(Function(item) item.Doi = "10.53731/wtf9v-y0n52")
        Assert.IsFalse(recent.Fwci.HasValue)
        Assert.IsFalse(recent.Percentile.HasValue)
        Assert.IsTrue(works.Any(Function(item) item.Doi = "10.1002/(sici)1096-9098(199702)64:2<122::aid-jso6>3.0.co;2-d"), "A DOI with < and > survives.")

        Dim lookedUp As List(Of CitedWork) = CitationsService.ParseWorks(Fixture("work_single.json"))
        Assert.AreEqual(1, lookedUp.Count, "A single lookup answers with one work.")
        Assert.AreEqual("10.6084/m9.figshare.681737", lookedUp(0).Doi)

        Dim meta = CitationsService.ParseMeta(Fixture("works_batch.json"))
        Assert.AreEqual(5L, meta.Count)
        Assert.IsNull(meta.NextCursor, "The last page has no cursor.")
        Assert.ThrowsExactly(Of JournalFactsFormatException)(Sub() CitationsService.ParseWorks("<html>"))
    End Sub

    <TestMethod>
    Public Sub DoisGoInBatchesOfFiftyThatFitTheAddress()
        Dim dois As List(Of String) = Enumerable.Range(1, 120).Select(Function(index) "10.5555/example." & index.ToString()).ToList()
        Dim batches As List(Of List(Of String)) = CitationsService.DoiBatches(dois)
        CollectionAssert.AreEqual({50, 50, 20}, batches.Select(Function(item) item.Count).ToList())
        CollectionAssert.AreEqual(dois, batches.SelectMany(Function(item) item).ToList(), "Every DOI once, in order.")

        Dim long_ As List(Of String) = Enumerable.Range(1, 40).Select(Function(index) "10.5555/" & New String("x"c, 300) & index.ToString()).ToList()
        Dim longBatches As List(Of List(Of String)) = CitationsService.DoiBatches(long_)
        Assert.IsTrue(longBatches.Count > 1)
        Assert.IsTrue(longBatches.All(Function(item) CitationsService.BatchUrl(item).Length <= OnlineCitationSource.MaximumUrlLength))
        Assert.AreEqual(40, longBatches.Sum(Function(item) item.Count))

        Dim awkward As List(Of List(Of String)) = CitationsService.DoiBatches({"10.1/a,b", "10.1/c|d", "10.1/e+f", "10.1/plain"})
        CollectionAssert.AreEqual({"10.1/plain"}, awkward.Single(), "A DOI with a separator in it is looked up on its own.")
        Assert.IsFalse(CitationsService.CanBatch("10.1/a,b"))

        Dim address As String = CitationsService.BatchUrl({"10.1002/(sici)1096-9098(199702)64:2<122::aid-jso6>3.0.co;2-d", "10.5555/12345678"})
        StringAssert.StartsWith(address, "https://api.openalex.org/works?filter=doi:10.1002%2F%28sici%291096-9098%28199702%2964%3A2%3C122%3A%3Aaid-jso6%3E3.0.co%3B2-d|10.5555%2F12345678&per_page=100&select=")
        Assert.IsFalse(address.Contains(Carberry), "The DOIs only.")
    End Sub


    ' ---------------------------------------------------------------
    ' Confirming works
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub EachWorkIsOfferedOnceAndOnlyYourRecordIsChecked()
        Dim groups As New List(Of OrcidWorkGroup) From {
            New OrcidWorkGroup With {.Dois = New List(Of String) From {"10.5555/preprint", "10.5555/article"}, .Title = "Versions"},
            New OrcidWorkGroup With {.Dois = New List(Of String) From {"10.5555/missing"}, .Title = "Missing work"},
            New OrcidWorkGroup With {.Title = "No DOI"},
            New OrcidWorkGroup With {.Dois = New List(Of String) From {"10.5555/left"}, .Title = "Left out before"}
        }
        Dim found As New Dictionary(Of String, CitedWork) From {
            {"10.5555/preprint", Work("W1", "10.5555/preprint", 3)},
            {"10.5555/article", Work("W2", "10.5555/article", 40)},
            {"10.5555/left", Work("W3", "10.5555/left", 5)},
            {"10.5555/manuscript", Work("W4", "10.5555/manuscript", 7)}
        }
        Dim manuscripts As New List(Of (Doi As String, Title As String)) From {("10.5555/article", "Already on the record"), ("10.5555/manuscript", "Only in PaperRoute"), ("10.5555/unknown", "Not in OpenAlex")}
        Dim linked As New List(Of CitedWork) From {Work("W2", "10.5555/article", 40), Work("W9", "10.5555/someone-else", 900), Work("W10", String.Empty, 2)}

        Dim lookup As CitationLookup = CitationsService.Assemble(Carberry, groups, manuscripts, found, linked, 250, {"doi:10.5555/left", "doi:10.5555/gone"})

        CollectionAssert.AreEqual({"W2", "W3", "W4", "W9", "W10"}, lookup.Candidates.Select(Function(item) item.Work.OpenAlexId).ToList(),
                                  "The most-cited version, then your manuscripts, then OpenAlex's; each once.")
        Assert.AreEqual(CitationSection.OrcidRecord, lookup.Candidates(0).Section)
        Assert.AreEqual(CitationSection.PaperRouteManuscripts, lookup.Candidates(2).Section)
        Assert.AreEqual(CitationSection.OpenAlexOnly, lookup.Candidates(3).Section)
        CollectionAssert.AreEqual({True, False, True, False, False}, lookup.Candidates.Select(Function(item) item.Selected).ToList(),
                                  "A work left out before stays out, and OpenAlex's own links start unchecked.")
        Assert.IsTrue(lookup.Candidates(1).ExcludedBefore)
        CollectionAssert.AreEqual({"Missing work", "Not in OpenAlex"}, lookup.NotFound)
        Assert.AreEqual(1, lookup.OrcidWithoutDoi)
        Assert.AreEqual(250L, lookup.OpenAlexLinkedTotal)
        Assert.AreEqual(3, lookup.OpenAlexLinkedRead)
        Assert.AreEqual("openalex:W10", lookup.Candidates(4).Key)

        ' Saving keeps what you checked, and remembers what you left out.
        lookup.Candidates(3).Selected = True
        lookup.Candidates(2).Selected = False
        Dim snapshot As CitationSnapshot = CitationsService.BuildSnapshot(lookup, {"doi:10.5555/left", "doi:10.5555/gone"})
        CollectionAssert.AreEqual({"W2", "W9"}, snapshot.Works.Select(Function(item) item.OpenAlexId).ToList())
        CollectionAssert.AreEquivalent({"doi:10.5555/gone", "doi:10.5555/left", "doi:10.5555/manuscript"}, snapshot.Excluded,
                                       "A choice about a work not offered this time is kept; an unchecked OpenAlex-only work isn't remembered.")
        Assert.AreEqual(Carberry, snapshot.Orcid)
        Assert.AreEqual(JournalFactCatalog.OpenAlexSource, snapshot.Source)
        Assert.IsTrue(snapshot.IncludeOpenAlexLinked)
    End Sub

    <TestMethod>
    Public Sub OnlyPublishedManuscriptDoisAreLookedUp()
        Dim published As New Manuscript With {.Title = "Published", .CurrentStage = PaperStage.Published}
        published.Metadata.Doi = "https://doi.org/10.5555/Published"
        Dim filed As New Manuscript With {.Title = "Moved to Published", .Location = ManuscriptLocation.Published, .CurrentStage = PaperStage.Accepted}
        filed.Metadata.Doi = "10.5555/filed"
        Dim duplicate As New Manuscript With {.Title = "Duplicate", .CurrentStage = PaperStage.Published}
        duplicate.Metadata.Doi = "10.5555/published"
        Dim preprint As New Manuscript With {.Title = "Preprint", .CurrentStage = PaperStage.Published}
        preprint.Metadata.PreprintDoi = "10.5555/preprint"
        Dim draft As New Manuscript With {.Title = "Draft", .CurrentStage = PaperStage.Draft}
        draft.Metadata.Doi = "10.5555/draft"

        Dim dois = CitationsService.PublishedDois({published, filed, duplicate, preprint, draft, Nothing})
        CollectionAssert.AreEqual({"10.5555/published", "10.5555/filed"}, dois.Select(Function(item) item.Doi).ToList(),
                                  "Published by stage or location, a DOI once, never a preprint or a draft.")
        Assert.AreEqual("Published", dois(0).Title)
    End Sub


    ' ---------------------------------------------------------------
    ' Online, through the gate
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheLookupSendsTheIdToOrcidAndOnlyDoisToOpenAlex()
        Dim network As FixtureNetwork = UseNetwork(AddressOf Recorded)
        Dim lookup As CitationLookup = New OnlineCitationSource().LookupAsync(
            "https://orcid.org/" & Carberry, {("10.1371/journal.pone.0000001", "A manuscript not in the batch")}, False, Nothing, Nothing, CancellationToken.None).GetAwaiter().GetResult()

        Dim addresses As List(Of Uri) = network.Requests.Select(Function(item) item.Uri).ToList()
        Assert.AreEqual(2, addresses.Count, "One ORCID read and one batch of DOIs.")
        Assert.AreEqual("https://pub.orcid.org/v3.0/" & Carberry & "/works", addresses(0).AbsoluteUri)
        Assert.AreEqual("application/json", network.Requests(0).Accept)
        Assert.AreEqual("api.openalex.org", addresses(1).Host)
        Assert.IsFalse(addresses(1).AbsoluteUri.Contains(Carberry), "The iD goes to OpenAlex only when asked.")
        StringAssert.Contains(Uri.UnescapeDataString(addresses(1).Query), "10.1371/journal.pone.0000001")

        Assert.AreEqual(5, lookup.Candidates.Count)
        Assert.IsTrue(lookup.Candidates.All(Function(item) item.Section = CitationSection.OrcidRecord AndAlso item.Selected))
        Assert.AreEqual(2, lookup.NotFound.Count, "The 1987 article and the manuscript weren't in the answer.")
        StringAssert.StartsWith(lookup.NotFound(0), "Bulk and surface plasmons")
        Assert.AreEqual(Carberry, lookup.Orcid)
        Assert.IsFalse(lookup.IncludeOpenAlexLinked)
    End Sub

    <TestMethod>
    Public Sub AskingOpenAlexForLinkedWorksSendsTheIdThere()
        Dim network As FixtureNetwork = UseNetwork(AddressOf Recorded)
        Dim lookup As CitationLookup = RunLookup(True)

        Dim linked As Uri = network.Requests.Select(Function(item) item.Uri).Single(Function(item) item.Query.Contains("authorships.author.orcid"))
        StringAssert.Contains(linked.Query, "authorships.author.orcid:" & Carberry)
        StringAssert.Contains(linked.Query, "cursor=%2A")
        Assert.AreEqual(3, network.Requests.Count, "The last page ends the cursor.")
        Assert.IsTrue(lookup.IncludeOpenAlexLinked)
        Assert.AreEqual(9L, lookup.OpenAlexLinkedTotal)
        Assert.AreEqual(1, lookup.Candidates.Where(Function(item) item.Work.Doi = "10.5555/12345678").Count(), "A work on your record isn't offered twice.")
        Assert.IsTrue(lookup.Candidates.Where(Function(item) item.Section = CitationSection.OpenAlexOnly).All(Function(item) Not item.Selected))
    End Sub

    <TestMethod>
    Public Sub ABusyOpenAlexSaysHowLongAndNothingIsSaved()
        UseNetwork(Function(uri) If(uri.Host = "pub.orcid.org", Answer(HttpStatusCode.OK, Fixture("orcid_works.json")),
                                    Answer(CType(429, HttpStatusCode), "{""error"":""Rate limit exceeded"",""retryAfter"":35}", retryAfter:=35)))
        Dim busy As OnlineServiceBusyException = Assert.ThrowsExactly(Of OnlineServiceBusyException)(
            Sub() RunLookup(False))
        Assert.AreEqual(TimeSpan.FromSeconds(35), busy.RetryAfter)
        Assert.IsFalse(busy.DailyAllowanceUsed)
    End Sub

    <TestMethod>
    Public Sub WorkingOfflineOrTurningTheServiceOffSendsNothing()
        Dim network As FixtureNetwork = UseNetwork(AddressOf Recorded)
        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(
            Sub() RunLookup(False))
        OnlineAccess.Configure(New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {OnlineServiceCatalog.Citations}})
        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(
            Sub() RunLookup(False))
        Assert.AreEqual(0, network.Requests.Count)

        Dim service As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.Citations)
        CollectionAssert.AreEqual({"pub.orcid.org", "api.openalex.org"}, service.Hosts.ToList())
    End Sub


    ' ---------------------------------------------------------------
    ' Saved figures
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub SavedFiguresRoundTripAndSurviveADamagedFile()
        Dim store As New CitationStore(Path.Combine(_directory, "data"))
        Assert.IsNull(store.Load(), "Nothing saved yet.")

        Dim first As New CitationSnapshot With {.Orcid = Carberry, .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = New DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc)}
        first.Works.Add(Work("W1", "https://doi.org/10.5555/ONE", 4))
        store.Save(first)
        Dim second As New CitationSnapshot With {.Orcid = Carberry, .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), .Excluded = New List(Of String) From {"doi:10.5555/two", " doi:10.5555/TWO "}}
        second.Works.Add(Work("W1", "10.5555/one", 6))
        second.Works(0).CountsByYear.AddRange({YearOf(2026, 2), YearOf(2026, 1), YearOf(1800, 9)})
        store.Save(second)

        Dim loaded As CitationSnapshot = store.Load()
        Assert.AreEqual(6, loaded.Works.Single().CitedByCount)
        Assert.AreEqual("10.5555/one", loaded.Works.Single().Doi)
        Assert.AreEqual(1, loaded.Excluded.Count, "Exclusions are kept once.")
        Assert.AreEqual(3, loaded.Works.Single().CountsByYear.Single().Count, "Years merge; impossible years go.")
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "data", "citations.bak")), "The previous file is kept.")

        File.WriteAllText(store.DataFilePath, "{ damaged")
        Assert.AreEqual(4, store.Load().Works.Single().CitedByCount, "A damaged file falls back to the previous one.")
        File.WriteAllText(Path.Combine(_directory, "data", "citations.bak"), "")
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() store.Load())

        Dim lenient As CitationSnapshot = CitationStore.ReadJson("{""Works"":[null,{""OpenAlexId"":""nonsense"",""Doi"":""not a doi""},{""OpenAlexId"":""W7"",""CitedByCount"":-4,""Fwci"":-1,""Percentile"":7}]}")
        Assert.AreEqual(1, lenient.Works.Count, "A work with neither an id nor a DOI is dropped.")
        Assert.AreEqual(0, lenient.Works(0).CitedByCount)
        Assert.IsFalse(lenient.Works(0).Fwci.HasValue)
        Assert.IsFalse(lenient.Works(0).Percentile.HasValue)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() CitationStore.ReadJson("[1,2]"))
    End Sub

    <TestMethod>
    Public Sub BackupsCarrySavedCitationsAndRestoreThem()
        Dim sourceData As String = Path.Combine(_directory, "source-data")
        Dim sourceManaged As String = Path.Combine(_directory, "source-managed")
        Directory.CreateDirectory(sourceData)
        Directory.CreateDirectory(sourceManaged)
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceManaged)
        Dim sourceManuscripts As New List(Of Manuscript) From {New Manuscript With {.Title = "Source"}}
        sourceRepository.Save(sourceManuscripts)
        Dim saved As New CitationSnapshot With {.Orcid = Carberry, .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = DateTime.UtcNow}
        saved.Works.Add(Work("W2396441275", "10.5555/12345678", 25))
        Dim sourceStore As New CitationStore(sourceData)
        sourceStore.Save(saved)

        Dim backupPath As String = Path.Combine(_directory, "backup.zip")
        Dim backup As New PortableBackupService(sourceManaged)
        backup.CreateBackup(backupPath, sourceManuscripts, sourceRepository)
        Using archive As IO.Compression.ZipArchive = IO.Compression.ZipFile.OpenRead(backupPath)
            Assert.IsNotNull(archive.GetEntry("citations.json"))
            Assert.IsNull(archive.GetEntry("citations.bak"))
        End Using

        Dim destinationData As String = Path.Combine(_directory, "destination-data")
        Dim destinationManaged As String = Path.Combine(_directory, "destination-managed")
        Directory.CreateDirectory(destinationData)
        Directory.CreateDirectory(destinationManaged)
        Dim destinationRepository As New ManuscriptRepository(destinationData, destinationManaged)
        Dim current As New List(Of Manuscript) From {New Manuscript With {.Title = "Current"}}
        destinationRepository.Save(current)

        Dim restore As New PortableRestoreService(destinationManaged)
        restore.InspectBackup(backupPath)
        restore.RestoreBackup(backupPath, current, destinationRepository)
        Assert.AreEqual(25, New CitationStore(destinationData).Load().Works.Single().CitedByCount)
        Assert.IsFalse(Directory.GetFiles(destinationData, "restore-*").Any(), "No staging or rollback files are left behind.")

        ' A backup whose citations can't be read is refused before anything changes.
        Dim damagedPath As String = Path.Combine(_directory, "damaged.zip")
        File.Copy(backupPath, damagedPath)
        Using archive As IO.Compression.ZipArchive = IO.Compression.ZipFile.Open(damagedPath, IO.Compression.ZipArchiveMode.Update)
            archive.GetEntry("citations.json").Delete()
            Using writer As New StreamWriter(archive.CreateEntry("citations.json").Open())
                writer.Write("{ damaged")
            End Using
        End Using
        Dim before As String = File.ReadAllText(Path.Combine(destinationData, "citations.json"))
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() restore.InspectBackup(damagedPath))
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() restore.RestoreBackup(damagedPath, current, destinationRepository))
        Assert.AreEqual(before, File.ReadAllText(Path.Combine(destinationData, "citations.json")))
    End Sub

    <TestMethod>
    Public Sub TheExampleLibraryHasFictionalCitations()
        Dim example = ExampleLibraryService.Create(New DateTime(2026, 9, 30))
        Dim snapshot As CitationSnapshot = ExampleLibraryService.CreateCitations(New DateTime(2026, 9, 30), example.Manuscripts)
        Assert.AreEqual(JournalFactCatalog.ExampleSource, snapshot.Source)
        Assert.AreEqual(String.Empty, snapshot.Orcid, "No ORCID iD, real or made up.")
        Assert.IsTrue(snapshot.Works.All(Function(item) item.Doi.StartsWith("10.5555/example.", StringComparison.Ordinal) AndAlso item.Title.StartsWith("Example: ", StringComparison.Ordinal)))
        Dim published As Manuscript = example.Manuscripts.Single(Function(item) item.Metadata.Doi = "10.5555/example.anchoring")
        Assert.AreEqual(published.Metadata.PublishedDate.Value.Year, snapshot.Works.Single(Function(item) item.Doi = "10.5555/example.anchoring").Year)
        Assert.IsTrue(snapshot.Works.All(Function(item) item.CountsByYear.All(Function(entry) entry.Year >= item.Year.Value)), "No citations before publication.")

        Dim summary As CitationSummary = CitationMetricsService.Summarize(snapshot.Works, 2026)
        Assert.AreEqual(6, summary.Works)
        Assert.IsTrue(summary.HIndex > 0 AndAlso summary.HIndex <= summary.Works)
        CitationStore.Normalize(snapshot)
        Assert.AreEqual(6, snapshot.Works.Count, "The example survives its own checks.")
    End Sub


    ' ---------------------------------------------------------------
    ' The update window
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheUpdateWindowSaysWhatItSendsThenAsksWhichWorksAreYours()
        RunOnStaThread(
            Sub()
                Dim groups As List(Of OrcidWorkGroup) = OrcidClient.ParseWorkGroups(Fixture("orcid_works.json"))
                Dim found As Dictionary(Of String, CitedWork) = CitationsService.ParseWorks(Fixture("works_batch.json")).ToDictionary(Function(item) item.Doi)
                Dim source As New RecordedCitations(CitationsService.Assemble(Carberry, groups, Nothing, found, Nothing, Nothing, {"doi:10.5555/12345679"}))
                Dim previous As New CitationSnapshot With {.Orcid = Carberry, .RetrievedUtc = New DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), .Excluded = New List(Of String) From {"doi:10.5555/12345679"}}

                Using dialog As New CitationsUpdateForm(Carberry, {("10.5555/12345678", "Toward a Unified Theory")}, previous, source)
                    ShowOffscreen(dialog)
                    StringAssert.Contains(dialog.IntroText, "sends your iD, " & Carberry & ", to ORCID")
                    StringAssert.Contains(dialog.IntroText, "your 1 published manuscript with a DOI")
                    StringAssert.Contains(dialog.IntroText, "Unpublished titles and abstracts are never sent.")
                    StringAssert.StartsWith(dialog.StatusText, "This replaces the figures from Sep 1, 2026.")
                    Assert.IsFalse(dialog.LinkedBox.Checked, "OpenAlex gets your iD only if you ask.")

                    dialog.LookUpAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Assert.AreEqual(Carberry, source.Orcid)
                    Assert.AreEqual(1, source.Manuscripts.Count)
                    CollectionAssert.AreEqual({"doi:10.5555/12345679"}, source.Excluded)

                    Dim rows As List(Of ListViewItem) = dialog.WorksList.Items.Cast(Of ListViewItem)().ToList()
                    Assert.AreEqual(5, rows.Count)
                    Assert.AreEqual(4, rows.Where(Function(item) item.Checked).Count(), "The work left out last time stays unchecked.")
                    StringAssert.Contains(dialog.StatusText, "Not found in OpenAlex: 1 (Bulk and surface plasmons")
                    StringAssert.StartsWith(dialog.SummaryText, "Checked: 4 works")

                    rows.First(Function(item) item.Checked).Checked = False
                    Application.DoEvents()
                    StringAssert.StartsWith(dialog.SummaryText, "Checked: 3 works")

                    dialog.SaveForTest()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(3, dialog.Result.Works.Count)
                    Assert.AreEqual(2, dialog.Result.Excluded.Count)
                End Using

                ' A failed lookup says why, and nothing is saved.
                Using dialog As New CitationsUpdateForm(Carberry, Nothing, Nothing, New RecordedCitations(Nothing, New OnlineServiceBusyException("OpenAlex", TimeSpan.FromSeconds(35), False)))
                    ShowOffscreen(dialog)
                    dialog.LookUpAsync().GetAwaiter().GetResult()
                    StringAssert.StartsWith(dialog.StatusText, "OpenAlex is busy and asked PaperRoute to wait about 35 seconds.")
                    StringAssert.EndsWith(dialog.StatusText, "Nothing was changed.")
                    Assert.IsNull(dialog.Result)
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function Works(ParamArray items As (Count As Integer, Year As Integer)()) As List(Of CitedWork)
        Return items.Select(Function(item, index) New CitedWork With {.OpenAlexId = "W" & (index + 1).ToString(), .CitedByCount = item.Count, .Year = item.Year}).ToList()
    End Function

    Private Shared Function Work(id As String, doi As String, count As Integer) As CitedWork
        Return New CitedWork With {.OpenAlexId = id, .Doi = CitationKeys.Doi(doi), .CitedByCount = count, .Title = "Work " & id}
    End Function

    Private Shared Function YearOf(value As Integer, count As Integer) As YearCount
        Return New YearCount With {.Year = value, .Count = count}
    End Function

    Private Shared Function RunLookup(includeLinked As Boolean) As CitationLookup
        Dim source As New OnlineCitationSource()
        Return source.LookupAsync(Carberry, Nothing, includeLinked, Nothing, Nothing, CancellationToken.None).GetAwaiter().GetResult()
    End Function

    Private Shared Function Fixture(name As String) As String
        Return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Citations", name))
    End Function

    ' ORCID's record, the five 10.5555 works for a DOI batch, and nine works
    ' (one of them Carberry's) for the iD's linked works.
    Private Shared Function Recorded(uri As Uri) As HttpResponseMessage
        If uri.Host = "pub.orcid.org" Then Return Answer(HttpStatusCode.OK, Fixture("orcid_works.json"))
        If uri.Query.Contains("authorships.author.orcid") Then Return Answer(HttpStatusCode.OK, Fixture("works_edgecases.json"))
        Return Answer(HttpStatusCode.OK, Fixture("works_batch.json"))
    End Function

    Private Shared Function Answer(status As HttpStatusCode, body As String, Optional retryAfter As Integer = 0) As HttpResponseMessage
        Dim response As New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
        If retryAfter > 0 Then response.Headers.RetryAfter = New Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfter))
        Return response
    End Function

    Private Function UseNetwork(respond As Func(Of Uri, HttpResponseMessage)) As FixtureNetwork
        OnlineAccess.ResetForTests()
        IsolateKeys()
        Dim network As New FixtureNetwork With {.Respond = respond}
        OnlineAccess.InnerHandlerFactory = Function() network
        Return network
    End Function

    Private NotInheritable Class SentRequest
        Public Property Uri As Uri
        Public Property Accept As String
    End Class

    Private NotInheritable Class FixtureNetwork
        Inherits HttpMessageHandler

        Public ReadOnly Requests As New List(Of SentRequest)()
        Public Property Respond As Func(Of Uri, HttpResponseMessage)

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            SyncLock Requests
                Requests.Add(New SentRequest With {.Uri = request.RequestUri, .Accept = String.Join(",", request.Headers.Accept.Select(Function(item) item.MediaType))})
            End SyncLock
            Dim response As HttpResponseMessage = Respond(request.RequestUri)
            response.RequestMessage = request
            Return Task.FromResult(response)
        End Function
    End Class

    Private NotInheritable Class RecordedCitations
        Implements ICitationSource

        Private ReadOnly _lookup As CitationLookup
        Private ReadOnly _failure As Exception

        Public Sub New(lookup As CitationLookup, Optional failure As Exception = Nothing)
            _lookup = lookup
            _failure = failure
        End Sub

        Public Property Orcid As String
        Public Property Manuscripts As List(Of (Doi As String, Title As String))
        Public Property Excluded As List(Of String)

        Public Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                    excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync
            Me.Orcid = orcid
            Manuscripts = manuscriptDois.ToList()
            Me.Excluded = excluded.ToList()
            If _failure IsNot Nothing Then Return Task.FromException(Of CitationLookup)(_failure)
            Return Task.FromResult(_lookup)
        End Function
    End Class

    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As ExceptionDispatchInfo = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                End Try
            End Sub)
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        thread.Join()
        failure?.Throw()
    End Sub

End Class
