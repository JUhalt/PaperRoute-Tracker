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
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Journal facts from open indexes (#87), tested on answers recorded from DOAJ
' and OpenAlex on September 29, 2026. No test reaches the network.
<TestClass>
<DoNotParallelize>
Public Class JournalFactsTests

    Private Shared ReadOnly Checked As New DateTime(2026, 9, 29, 16, 45, 0, DateTimeKind.Utc)
    Private _directory As String

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        _directory = TestSupport.CreateTemporaryRoot()
        IsolateKeys()
    End Sub

    ' Keys come from the test folder, never the machine's own profile.
    Private Sub IsolateKeys()
        Dim keys As New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    ' ---------------------------------------------------------------
    ' ISSNs
    ' ---------------------------------------------------------------

    <TestMethod>
    <DataRow("1932-6203", "1932-6203")>
    <DataRow("19326203", "1932-6203")>
    <DataRow(" 2050-084x ", "2050-084X")>
    <DataRow("ISSN 0102-227X", "0102-227X")>
    <DataRow("1932-6204", "")>
    <DataRow("0000-0000", "")>
    <DataRow("1932-620", "")>
    <DataRow("abcd-efgh", "")>
    <DataRow("1234-5678", "")>
    <DataRow("1234-5679", "1234-5679")>
    Public Sub IssnsAreNormalizedAndTheirCheckDigitsVerified(value As String, expected As String)
        Assert.AreEqual(expected, IssnService.Normalize(value))
    End Sub

    <TestMethod>
    Public Sub TypedIssnListsKeepValidOnesOnceAndReportTheRest()
        Dim parsed = IssnService.ParseList("1932-6203, 2050-084x; 19326203" & Environment.NewLine & "bogus ISSN")
        CollectionAssert.AreEqual({"1932-6203", "2050-084X"}, parsed.Valid)
        CollectionAssert.AreEqual({"bogus"}, parsed.Invalid)
        Assert.AreEqual(4, IssnService.NormalizeList({"1932-6203", "2050-084X", "0102-227X", "0320-961X", "2050-7283"}).Count, "At most four per journal.")
    End Sub


    ' ---------------------------------------------------------------
    ' Parsing recorded answers
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub DoajRecordsAreReadFromLiveAnswers()
        Dim plos As DoajJournal = DoajClient.ParseSearch(Fixture("doaj_plos_one.json"), "1932-6203")
        Assert.AreEqual("2fdf1470373343b7bd4f825179c685f5", plos.Id)
        Assert.AreEqual("Public Library of Science (PLoS)", plos.Publisher)
        Assert.AreEqual(True, plos.HasApc)
        Assert.AreEqual("USD 2477", plos.ApcPrices.Single().ToString())
        Assert.AreEqual("https://plos.org/publish/fees/", plos.ApcUrl)
        CollectionAssert.AreEqual({"Single anonymous peer review"}, plos.ReviewProcess)
        Assert.AreEqual(29, plos.PublicationTimeWeeks)
        Assert.AreEqual(True, plos.PlagiarismDetection)
        CollectionAssert.AreEqual({"CC BY"}, plos.Licenses)
        Assert.AreEqual(True, plos.AuthorRetainsCopyright)
        Assert.AreEqual("https://journals.plos.org/plosone/s/journal-information", plos.AimsScopeUrl)
        Assert.AreEqual("https://journals.plos.org/plosone/s/submission-guidelines", plos.AuthorInstructionsUrl)
        Assert.AreEqual("https://journals.plos.org/plosone/static/editorial-board", plos.EditorialBoardUrl)
        Assert.AreEqual("https://openpolicyfinder.jisc.ac.uk/id/publication/17599", plos.DepositPolicyUrl)
        Assert.IsNull(plos.LastFullReview, "PLOS ONE's record has no last full review; last_updated is a site-wide reindex date and isn't used.")

        Dim bmc As DoajJournal = DoajClient.ParseSearch(Fixture("doaj_bmc_psychology.json"), "2050-7283")
        CollectionAssert.AreEqual({"EUR 1690", "USD 2090", "GBP 1390"}, bmc.ApcPrices.Select(Function(item) item.ToString()).ToList(), "Every currency as listed, never converted.")
        CollectionAssert.AreEqual({"Open peer review"}, bmc.ReviewProcess)
        Assert.AreEqual(New DateTime(2026, 3, 17), bmc.LastFullReview.Value.Date)

        Dim noFee As DoajJournal = DoajClient.ParseSearch(Fixture("doaj_no_apc_other_charges.json"), "2156-9703")
        Assert.AreEqual(False, noFee.HasApc)
        Assert.AreEqual(0, noFee.ApcPrices.Count)
        Assert.AreEqual(True, noFee.HasOtherCharges)
        Assert.AreEqual(String.Empty, noFee.PlagiarismUrl, "An absent plagiarism link is blank.")

        Assert.AreEqual(String.Empty, DoajClient.ParseSearch(Fixture("doaj_empty_plagiarism_url.json"), "0102-227X").PlagiarismUrl, "An empty one is blank too.")
        Assert.AreEqual("0320-961X", DoajClient.ParseSearch(Fixture("doaj_print_only.json"), "0320-961X").Pissn, "Found by a print ISSN.")
        Assert.IsNotNull(DoajClient.ParseSearch(Fixture("doaj_elife.json"), "2050-084x"), "The X check digit matches either case once normalized.")

        Assert.IsNull(DoajClient.ParseSearch(Fixture("doaj_not_listed.json"), "0956-7976"), "Not listed is no record, not an error.")
        Assert.IsNull(DoajClient.ParseSearch(Fixture("doaj_plos_one.json"), "2050-084X"), "A record for another ISSN isn't this journal.")
        Assert.ThrowsExactly(Of JournalFactsFormatException)(Sub() DoajClient.ParseSearch(Fixture("doaj_502.txt"), "1932-6203"))
        Assert.ThrowsExactly(Of JournalFactsFormatException)(Sub() DoajClient.ParseSearch("{""total"":1}", "1932-6203"))
    End Sub

    <TestMethod>
    Public Sub OpenAlexSourcesAreReadFromLiveAnswers()
        Dim plos As OpenAlexSource = OpenAlexSourceClient.ParseSource(Fixture("openalex_plos_one.json"))
        Assert.AreEqual("S202381698", plos.Id)
        Assert.AreEqual("1932-6203", plos.IssnL)
        Assert.AreEqual("Public Library of Science", plos.Publisher)
        Assert.AreEqual(True, plos.IsOa)
        Assert.AreEqual(3.3262679636523758, plos.TwoYearMeanCitedness.Value, 0.0000001, "Read from OpenAlex's own key, 2yr_mean_citedness.")
        Assert.AreEqual(605L, plos.HIndex)
        Assert.AreEqual(236858L, plos.I10Index)
        Assert.AreEqual(3, plos.Topics.Count)
        Assert.AreEqual("USD 2382", plos.ApcPrices.Single().ToString())
        Assert.AreEqual(DateTimeKind.Utc, plos.UpdatedUtc.Value.Kind, "OpenAlex dates carry no zone and are UTC.")
        Assert.AreEqual(New DateTime(2026, 9, 28, 10, 1, 26, DateTimeKind.Utc), plos.UpdatedUtc.Value)

        Dim science As OpenAlexSource = OpenAlexSourceClient.ParseSource(Fixture("openalex_psych_science.json"))
        Assert.AreEqual(False, science.IsOa)
        CollectionAssert.AreEqual({"0956-7976", "1467-9280"}, science.Issns)
        CollectionAssert.AreEqual({"USD 3900", "GBP 2859"}, science.ApcPrices.Select(Function(item) item.ToString()).ToList())

        Dim sparse As OpenAlexSource = OpenAlexSourceClient.ParseSource(Fixture("openalex_sparse.json"))
        Assert.AreEqual(0, sparse.ApcPrices.Count, "A null price list is no price.")
        Assert.AreEqual(String.Empty, sparse.HomepageUrl)
        Assert.AreEqual(0.0, sparse.TwoYearMeanCitedness.Value)

        Dim bare As OpenAlexSource = OpenAlexSourceClient.ParseSource(Fixture("openalex_id_only.json"))
        Assert.IsFalse(bare.TwoYearMeanCitedness.HasValue OrElse bare.HIndex.HasValue OrElse bare.I10Index.HasValue, "No summary_stats, no metrics.")

        Dim matches As List(Of OpenAlexSourceMatch) = OpenAlexSourceClient.ParseSearch(Fixture("openalex_search.json"))
        Assert.AreEqual(5, matches.Count)
        Assert.AreEqual("S58854535", matches(0).Id)
        Assert.AreEqual("Psychological Science", matches(0).DisplayName)

        Assert.AreEqual("S202381698", OpenAlexSourceClient.NormalizeId("https://openalex.org/S202381698"))
        Assert.AreEqual(String.Empty, OpenAlexSourceClient.NormalizeId("W2741809807"))
        Assert.AreEqual(String.Empty, OpenAlexSourceClient.NormalizeId("S1/../../works"))
        Assert.ThrowsExactly(Of JournalFactsFormatException)(Sub() OpenAlexSourceClient.ParseSource(Fixture("openalex_not_found.html")))
    End Sub


    ' ---------------------------------------------------------------
    ' Through the gate
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ALookupSendsOnlyTheIssnToTheTwoIndexes()
        Dim network As FixtureNetwork = UseNetwork(
            Function(uri)
                If uri.Host = "api.openalex.org" Then Return Answer(HttpStatusCode.OK, Fixture("openalex_plos_one.json"))
                Return Answer(HttpStatusCode.OK, Fixture("doaj_plos_one.json"))
            End Function)

        Dim lookup As JournalFactsLookup = New OnlineJournalFactsSource().LookupAsync({"1932-6203"}, "", CancellationToken.None).GetAwaiter().GetResult()

        Assert.IsNotNull(lookup.OpenAlex)
        Assert.IsNotNull(lookup.Doaj)
        Assert.IsTrue(lookup.DoajChecked AndAlso lookup.OpenAlexChecked)
        CollectionAssert.AreEqual({
            "https://api.openalex.org/sources/issn:1932-6203?select=" & OpenAlexSourceClient.LookupFields,
            "https://doaj.org/api/v4/search/journals/issn:1932-6203"
        }, network.Requests.Select(Function(item) item.Uri.AbsoluteUri).ToList(), "One free OpenAlex lookup, then DOAJ; the ISSN and nothing else.")
        Assert.IsTrue(network.Requests.All(Function(item) item.UserAgent.StartsWith("PaperRoute-Tracker/", StringComparison.Ordinal) AndAlso Not item.UserAgent.Contains("@"c)))
        Assert.IsTrue(network.Requests.All(Function(item) item.Authorization Is Nothing), "No key was added, so none is sent.")
    End Sub

    <TestMethod>
    Public Sub AJournalOutsideDoajIsCheckedByEachIssnOpenAlexKnows()
        Dim network As FixtureNetwork = UseNetwork(
            Function(uri)
                If uri.Host = "api.openalex.org" Then Return Answer(HttpStatusCode.OK, Fixture("openalex_psych_science.json"))
                Return Answer(HttpStatusCode.OK, Fixture("doaj_not_listed.json"))
            End Function)

        Dim lookup As JournalFactsLookup = New OnlineJournalFactsSource().LookupAsync({"0956-7976"}, "", CancellationToken.None).GetAwaiter().GetResult()

        Assert.IsNull(lookup.Doaj)
        Assert.IsTrue(lookup.DoajChecked)
        CollectionAssert.AreEqual({"0956-7976", "1467-9280"}, lookup.Issns)
        Assert.AreEqual(3, network.Requests.Count, "OpenAlex once, then DOAJ by at most two ISSNs.")

        Dim plan As JournalFactsPlan = JournalFactsService.Plan(New JournalRecord With {.Name = "Psychological Science"}, lookup)
        StringAssert.Contains(String.Join(" ", plan.Notes), "isn't listed in DOAJ")
        Dim fee As JournalFactChange = plan.Changes.Single(Function(item) item.Field = "Publication fee")
        Assert.AreEqual("USD " & Money(3900) & " · GBP " & Money(2859) & " (optional, to make an article open access)", fee.Found, "A subscription journal's fee is optional.")
    End Sub

    <TestMethod>
    Public Sub OneIndexFailingLeavesTheOthersFacts()
        UseNetwork(
            Function(uri)
                If uri.Host = "api.openalex.org" Then Return Answer(HttpStatusCode.NotFound, Fixture("openalex_not_found.html"), "text/html")
                Return Answer(HttpStatusCode.BadGateway, Fixture("doaj_502.txt"), "text/plain")
            End Function)

        Dim lookup As JournalFactsLookup = New OnlineJournalFactsSource().LookupAsync({"1932-6203"}, "", CancellationToken.None).GetAwaiter().GetResult()
        Assert.IsTrue(lookup.OpenAlexChecked)
        Assert.IsNull(lookup.OpenAlex, "An unknown ISSN answers 404 in HTML: no record, not a failure.")
        Assert.AreEqual("DOAJ had a problem on its side (HTTP 502). Try again later.", lookup.DoajError)
        Assert.IsFalse(lookup.DoajChecked)

        UseNetwork(
            Function(uri)
                If uri.Host = "api.openalex.org" Then Return Answer(CType(429, HttpStatusCode), Fixture("openalex_429.json"))
                Return Answer(HttpStatusCode.OK, Fixture("doaj_plos_one.json"))
            End Function)
        lookup = New OnlineJournalFactsSource().LookupAsync({"1932-6203"}, "", CancellationToken.None).GetAwaiter().GetResult()
        StringAssert.StartsWith(lookup.OpenAlexError, "OpenAlex is busy")
        Assert.IsNotNull(lookup.Doaj, "DOAJ is still checked by the stored ISSN.")

        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Facts = New List(Of JournalFact) From {New JournalFact With {.Key = JournalFactCatalog.MeanCitedness, .Value = "3.33", .Source = JournalFactCatalog.OpenAlexSource}}}
        Dim plan As JournalFactsPlan = JournalFactsService.Plan(record, lookup)
        Assert.IsFalse(plan.Changes.Any(Function(item) item.Kind = JournalFactChangeKind.Remove AndAlso item.Source = JournalFactCatalog.OpenAlexSource), "A source that failed removes nothing.")
        StringAssert.Contains(String.Join(" ", plan.Notes), "OpenAlex's facts weren't checked.")
    End Sub

    <TestMethod>
    Public Sub WorkOfflineOrTurningJournalFactsOffSendsNothing()
        Dim network As FixtureNetwork = UseNetwork(Function(uri) Answer(HttpStatusCode.OK, "{}"))
        Dim source As New OnlineJournalFactsSource()

        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        Assert.AreEqual(OnlineBlockReason.WorkOffline,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() source.LookupAsync({"1932-6203"}, "", CancellationToken.None).GetAwaiter().GetResult()).Reason)

        OnlineAccess.Configure(New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {OnlineServiceCatalog.JournalFacts}})
        Assert.AreEqual(OnlineBlockReason.ServiceOff,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() source.SearchAsync("PLOS ONE", CancellationToken.None).GetAwaiter().GetResult()).Reason)

        Assert.AreEqual(0, network.Requests.Count)
        CollectionAssert.AreEqual({"doaj.org", "api.openalex.org"}, OnlineServiceCatalog.Find(OnlineServiceCatalog.JournalFacts).Hosts.ToList())
    End Sub

    <TestMethod>
    Public Sub FindingAJournalByNameSendsOnlyTheName()
        Dim network As FixtureNetwork = UseNetwork(Function(uri) Answer(HttpStatusCode.OK, Fixture("openalex_search.json")))
        Dim matches As List(Of OpenAlexSourceMatch) = New OnlineJournalFactsSource().SearchAsync("  Cognitive, Affective, & Behavioral Neuroscience ", CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual(5, matches.Count)
        Assert.AreEqual("https://api.openalex.org/sources?search=Cognitive%2C%20Affective%2C%20%26%20Behavioral%20Neuroscience&per_page=8&select=" & OpenAlexSourceClient.SearchFields,
                        network.Requests.Single().Uri.AbsoluteUri)
    End Sub


    ' ---------------------------------------------------------------
    ' What a lookup changes
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ALookupFillsBlanksAndNeverReplacesWhatYouTyped()
        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Publisher = "PLOS (my spelling)", .Issns = New List(Of String) From {"1932-6203"}}
        Dim plan As JournalFactsPlan = JournalFactsService.Plan(record, PlosLookup())

        Dim publisher As JournalFactChange = plan.Changes.Single(Function(item) item.Field = "Publisher")
        Assert.AreEqual(JournalFactChangeKind.Keep, publisher.Kind, "Yours is kept.")
        Assert.IsFalse(publisher.CanApply)
        Assert.AreEqual(JournalFactChangeKind.Fill, plan.Changes.Single(Function(item) item.Field = "Homepage").Kind)
        Assert.AreEqual(JournalFactChangeKind.Fill, plan.Changes.Single(Function(item) item.Field = "Aims and scope").Kind)
        Assert.IsTrue(plan.Changes.Where(Function(item) item.Kind = JournalFactChangeKind.Add).All(Function(item) item.Selected))

        publisher.Selected = True
        JournalFactsService.Apply(record, plan)

        Assert.AreEqual("PLOS (my spelling)", record.Publisher, "Even when checked, a kept value isn't applied.")
        Assert.AreEqual("https://journals.plos.org/plosone/", record.HomepageUrl)
        Assert.AreEqual("https://journals.plos.org/plosone/s/submission-guidelines", record.AuthorInstructionsUrl)
        Assert.AreEqual(JournalFactCatalog.DoajSource, record.FieldSources(JournalFactsService.HomepageField).Source)
        Assert.AreEqual("2fdf1470373343b7bd4f825179c685f5", record.DoajId)
        Assert.AreEqual("S202381698", record.OpenAlexId)
        Assert.AreEqual("Up to USD 2477 (waivers available)", Fact(record, JournalFactCatalog.Apc, "DOAJ").Value, "DOAJ's highest fee as listed, not OpenAlex's conversion.")
        Assert.AreEqual("Listed in DOAJ", Fact(record, JournalFactCatalog.DoajListing, "DOAJ").Value)
        Assert.AreEqual("https://doaj.org/toc/1932-6203", Fact(record, JournalFactCatalog.DoajListing, "DOAJ").Url)
        Assert.AreEqual("About 29 weeks from submission to publication", Fact(record, JournalFactCatalog.Weeks, "DOAJ").Value)
        Assert.AreEqual("3.33", Fact(record, JournalFactCatalog.MeanCitedness, "OpenAlex").Value)
        Assert.AreEqual("605", Fact(record, JournalFactCatalog.OpenAlexHIndex, "OpenAlex").Value)
        Assert.AreEqual("https://openpolicyfinder.jisc.ac.uk/id/publication/17599", JournalFactCatalog.SharingPolicyUrl(record))
        Assert.IsTrue(record.Facts.All(Function(item) item.CheckedUtc = Checked AndAlso Not item.EnteredByYou))
        Assert.IsFalse(record.Facts.Any(Function(item) item.Source = JournalFactCatalog.OpenAlexSource AndAlso item.Key = JournalFactCatalog.Apc), "OpenAlex's fee is used only for journals DOAJ doesn't list.")
    End Sub

    <TestMethod>
    Public Sub RefreshUpdatesLookedUpValuesButNotOnesYouChanged()
        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
        JournalFactsService.Apply(record, JournalFactsService.Plan(record, PlosLookup()))

        ' Later, DOAJ changes the fee and the homepage; the researcher has
        ' since typed their own aims-and-scope link.
        record.AimsScopeUrl = "https://example.org/my-notes-on-scope"
        JournalFactsService.Normalize(record)
        Assert.IsFalse(record.FieldSources.ContainsKey(JournalFactsService.AimsScopeField), "A changed field becomes the researcher's.")

        Dim later As JournalFactsLookup = PlosLookup(Checked.AddDays(30))
        later.Doaj.ApcPrices = New List(Of ListedPrice) From {New ListedPrice(2600D, "USD")}
        later.Doaj.HomepageUrl = "https://journals.plos.org/plosone/home"
        later.Doaj.AimsScopeUrl = "https://journals.plos.org/plosone/scope"
        Dim plan As JournalFactsPlan = JournalFactsService.Plan(record, later)

        Dim fee As JournalFactChange = plan.Changes.Single(Function(item) item.Field = "Publication fee")
        Assert.AreEqual(JournalFactChangeKind.Update, fee.Kind)
        Assert.AreEqual("Up to USD " & Money(2477) & " (waivers available)", fee.Current)
        Assert.AreEqual(JournalFactChangeKind.Update, plan.Changes.Single(Function(item) item.Field = "Homepage").Kind, "A value the lookup filled is refreshed.")
        Assert.AreEqual(JournalFactChangeKind.Keep, plan.Changes.Single(Function(item) item.Field = "Aims and scope").Kind)
        Assert.IsTrue(plan.Unchanged > 5)

        JournalFactsService.Apply(record, plan)
        Assert.AreEqual("Up to USD 2600 (waivers available)", Fact(record, JournalFactCatalog.Apc, "DOAJ").Value)
        Assert.AreEqual("https://example.org/my-notes-on-scope", record.AimsScopeUrl)
        Assert.AreEqual(Checked.AddDays(30), Fact(record, JournalFactCatalog.Review, "DOAJ").CheckedUtc.Value, "Unchanged facts are marked as checked again.")
        Assert.AreEqual(Checked.AddDays(30), JournalFactsService.LastChecked(record).Value)
    End Sub

    <TestMethod>
    Public Sub FactsASourceNoLongerGivesAreOfferedForRemovalUnchecked()
        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
        JournalFactsService.Apply(record, JournalFactsService.Plan(record, PlosLookup()))
        record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.Review, .Value = "My note from the editor: double anonymous", .Year = 2025, .Source = JournalFactCatalog.DoajSource, .EnteredByYou = True})

        Dim gone As JournalFactsLookup = PlosLookup(Checked.AddDays(1))
        gone.Doaj = Nothing
        Dim plan As JournalFactsPlan = JournalFactsService.Plan(record, gone)
        Assert.IsFalse(plan.Changes.Any(Function(item) item.Current = "My note from the editor: double anonymous"), "Entered facts are never offered for removal, whatever their source.")

        Assert.AreEqual("Not listed in DOAJ", plan.Changes.Single(Function(item) item.Field = "DOAJ listing").Found)
        Dim removals As List(Of JournalFactChange) = plan.Changes.Where(Function(item) item.Kind = JournalFactChangeKind.Remove).ToList()
        Assert.IsTrue(removals.Count >= 5 AndAlso removals.All(Function(item) Not item.Selected AndAlso item.Source = "DOAJ"), "Offered, never assumed.")

        JournalFactsService.Apply(record, plan)
        Assert.IsNotNull(Fact(record, JournalFactCatalog.Review, "DOAJ"), "Unchecked removals keep the fact.")

        ' Kept, a delisted journal's DOAJ facts give way to current ones.
        Assert.AreEqual(JournalFactCatalog.OpenAlexSource, JournalFactsService.BestFact(record, JournalFactCatalog.Apc).Source)
        Assert.IsNull(JournalFactsService.BestFact(record, JournalFactCatalog.Review), "The old DOAJ review type isn't shown beside 'Not listed'.")
        Assert.AreEqual(Checked.AddDays(1), JournalFactsService.OldestShownCheck(record).Value)

        For Each removal As JournalFactChange In JournalFactsService.Plan(record, gone).Changes.Where(Function(item) item.Kind = JournalFactChangeKind.Remove)
            removal.Selected = True
        Next
        Dim all As JournalFactsPlan = JournalFactsService.Plan(record, gone)
        all.Changes.ForEach(Sub(item) item.Selected = item.CanApply)
        JournalFactsService.Apply(record, all)
        Assert.IsNull(Fact(record, JournalFactCatalog.Review, "DOAJ"))
        Assert.IsTrue(record.Facts.Any(Function(item) item.EnteredByYou AndAlso item.Value = "My note from the editor: double anonymous"), "Your own entries survive even when every removal is chosen.")
    End Sub

    <TestMethod>
    Public Sub LinksThatArentWebAddressesAreDroppedWithANote()
        Dim lookup As JournalFactsLookup = PlosLookup()
        lookup.Doaj.AimsScopeUrl = "javascript:alert(1)"
        lookup.Doaj.EditorialBoardUrl = "http://www.biomedcentral.com/about /board"
        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
        Dim plan As JournalFactsPlan = JournalFactsService.Plan(record, lookup)

        Assert.IsFalse(plan.Changes.Any(Function(item) item.Field = "Aims and scope"))
        StringAssert.Contains(String.Join(" ", plan.Notes), "Aims and scope: DOAJ's link wasn't saved, because it isn't a web address.")
        JournalFactsService.Apply(record, plan)
        Assert.AreEqual("http://www.biomedcentral.com/about%20/board", record.EditorialBoardUrl, "A space is escaped, not stored raw.")
    End Sub


    <TestMethod>
    Public Sub TheJournalsIssnsDecideWhichJournalIsLookedUp()
        ' A wrong journal was once picked; its id stayed after the ISSN was corrected.
        Dim network As FixtureNetwork = UseNetwork(
            Function(uri)
                If uri.Host = "api.openalex.org" Then Return Answer(HttpStatusCode.OK, Fixture("openalex_plos_one.json"))
                Return Answer(HttpStatusCode.OK, Fixture("doaj_plos_one.json"))
            End Function)

        Dim lookup As JournalFactsLookup = New OnlineJournalFactsSource().LookupAsync({"1932-6203"}, "S58854535", CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual("S202381698", lookup.OpenAlex.Id)
        Assert.IsFalse(network.Requests.Any(Function(item) item.Uri.AbsoluteUri.Contains("S58854535", StringComparison.Ordinal)), "Only ISSNs are sent for a journal that has them.")

        network.Requests.Clear()
        Dim byId As JournalFactsLookup = New OnlineJournalFactsSource().LookupAsync({}, "S202381698", CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual("S202381698", byId.OpenAlex.Id)
        StringAssert.StartsWith(network.Requests(0).Uri.AbsoluteUri, "https://api.openalex.org/sources/S202381698?select=", "An id is used for a journal without an ISSN.")

        RunOnStaThread(
            Sub()
                Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"0956-7976"}, .OpenAlexId = "S58854535", .DoajId = "2fdf1470373343b7bd4f825179c685f5"}
                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    editor.IssnsBox.Text = "1932-6203"
                    editor.SaveForTest()
                    Assert.AreEqual(String.Empty, editor.Result.OpenAlexId, "Correcting the ISSN retires the old index ids.")
                    Assert.AreEqual(String.Empty, editor.Result.DoajId)
                End Using
                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    editor.NotesBox.Text = "Unrelated edit."
                    editor.SaveForTest()
                    Assert.AreEqual("S58854535", editor.Result.OpenAlexId, "Other edits keep them.")
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub FeesNameOtherChargesAndSharingNamesOnlyWhatDoajRecorded()
        Dim lookup As JournalFactsLookup = PlosLookup()
        lookup.Doaj.HasOtherCharges = True
        lookup.Doaj.DepositPolicyServices = New List(Of String) From {"https://reseau-mirabel.info/revue/1"}
        lookup.Doaj.DepositPolicyUrl = "https://example.org/policy"
        Dim facts As List(Of JournalFact) = JournalFactsService.FactsFrom(lookup)

        Assert.AreEqual("Up to USD 2477; other charges apply (waivers available)", facts.Single(Function(item) item.Key = JournalFactCatalog.Apc).Value)
        Assert.IsFalse(facts.Any(Function(item) item.Key = JournalFactCatalog.Sharing), "A policy DOAJ gives only as another site's address isn't named as Open Policy Finder.")

        Dim record As New JournalRecord With {.Issns = New List(Of String) From {"1932-6203"}}
        Assert.AreEqual("https://openpolicyfinder.jisc.ac.uk/search?search=1932-6203", JournalFactCatalog.SharingPolicyUrl(record), "The link still searches Open Policy Finder by ISSN.")
    End Sub


    ' ---------------------------------------------------------------
    ' Storage
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub StoredFactsAreCheckedLenientlyAndSurviveASaveAndLoad()
        Dim journal As New JournalRecord With {
            .Name = "Collabra: Psychology",
            .Issns = New List(Of String) From {"2474-7394", "1234-5678", "2474-7394"},
            .AimsScopeUrl = "javascript:alert(1)",
            .AuthorInstructionsUrl = "https://online.ucpress.edu/collabra/pages/submissions",
            .OpenAlexId = "https://openalex.org/S4210200813",
            .DoajId = "not an id",
            .FieldSources = Nothing,
            .Facts = New List(Of JournalFact) From {
                New JournalFact With {.Key = "", .Value = "no key"},
                New JournalFact With {.Key = JournalFactCatalog.Review, .Value = "  "},
                New JournalFact With {.Key = "a-later-metric", .Value = "7", .Source = "A later index", .Url = "file:///c:/secret"},
                New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "4.0", .Year = 2025, .Source = "Scopus", .EnteredByYou = True}
            }
        }

        SubmissionReadinessValidationService.NormalizeAndValidateJournal(journal)

        CollectionAssert.AreEqual({"2474-7394"}, journal.Issns, "Invalid and repeated ISSNs are dropped.")
        Assert.AreEqual(String.Empty, journal.AimsScopeUrl)
        Assert.AreEqual("S4210200813", journal.OpenAlexId)
        Assert.AreEqual(String.Empty, journal.DoajId)
        Assert.AreEqual(2, journal.Facts.Count, "Blank facts are dropped; unknown keys are kept for later versions.")
        Assert.AreEqual(String.Empty, journal.Facts.Single(Function(item) item.Key = "a-later-metric").Url, "Only web links are kept.")
        Assert.IsNotNull(journal.FieldSources)

        Dim repository As New AuthorLibraryRepository(Path.Combine(_directory, "data"))
        repository.Save(New AuthorLibraryData With {.Journals = New List(Of JournalRecord) From {journal}})
        Dim loaded As JournalRecord = repository.Load().Journals.Single()
        CollectionAssert.AreEqual({"2474-7394"}, loaded.Issns)
        Assert.AreEqual(2025, loaded.Facts.Single(Function(item) item.EnteredByYou).Year)

        ' A library from before #87 loads with the new fields empty.
        Dim older As String = Path.Combine(_directory, "older")
        Directory.CreateDirectory(older)
        File.WriteAllText(Path.Combine(older, "authors.json"), "{""Journals"":[{""Id"":""" & Guid.NewGuid().ToString() & """,""Name"":""Memory & Cognition"",""ReadinessChecklistTemplate"":[]}]}")
        Dim old As JournalRecord = New AuthorLibraryRepository(older).Load().Journals.Single()
        Assert.AreEqual(0, old.Issns.Count)
        Assert.AreEqual(0, old.Facts.Count)
        Assert.AreEqual(0, old.FieldSources.Count)
    End Sub

    <TestMethod>
    Public Sub SavingPeopleElsewhereKeepsTheJournalsTheJournalsPageSaved()
        Dim repository As New AuthorLibraryRepository(Path.Combine(_directory, "data"))
        Dim journal As New JournalRecord With {.Name = "PLOS ONE"}
        repository.Save(New AuthorLibraryData With {.Journals = New List(Of JournalRecord) From {journal}})

        Dim manuscriptPageCopy As AuthorLibraryData = repository.Load()

        Dim journalsPage As AuthorLibraryData = repository.Load()
        journalsPage.Journals.Single().Facts.Add(New JournalFact With {.Key = JournalFactCatalog.Review, .Value = "Single anonymous peer review", .Source = "DOAJ"})
        repository.Save(journalsPage)

        manuscriptPageCopy.Authors.Add(New AuthorRecord With {.GivenName = "Avery", .FamilyName = "Example"})
        repository.SaveKeepingJournals(manuscriptPageCopy)

        Dim result As AuthorLibraryData = repository.Load()
        Assert.AreEqual(1, result.Authors.Count)
        Assert.AreEqual(1, result.Journals.Single().Facts.Count, "The older copy didn't undo the looked-up fact.")
    End Sub


    ' ---------------------------------------------------------------
    ' Reading a record
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub FactsSummarizeForShortlistsAndSitBesideTheQuestions()
        Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
        JournalFactsService.Apply(record, JournalFactsService.Plan(record, PlosLookup()))

        Assert.AreEqual("Open access (DOAJ) · Up to USD " & Money(2477) & " (waivers available) · Single anonymous peer review · about 29 weeks to publication", JournalFactsService.OneLine(record))
        Assert.AreEqual("Peer review: Single anonymous peer review (DOAJ)", JournalFactsService.HintFor("trust.review", record).Text)
        Assert.AreEqual("Fee: Up to USD " & Money(2477) & " (waivers available) (DOAJ)", JournalFactsService.HintFor("fit.fees", record).Text)
        Assert.AreEqual(record.AuthorInstructionsUrl, JournalFactsService.HintFor("trust.guidelines", record).Url)
        Assert.AreEqual("https://openpolicyfinder.jisc.ac.uk/id/publication/17599", JournalFactsService.HintFor("fit.sharing", record).Url)
        Assert.AreEqual(String.Empty, JournalFactsService.HintFor("fit.audience", record).Text, "Only questions a fact helps with get a hint.")
        Assert.AreEqual(String.Empty, JournalFactsService.OneLine(New JournalRecord()))

        StringAssert.StartsWith(JournalFactsService.SourcesLine(record), "Facts from DOAJ and OpenAlex, public domain (CC0), checked ")
        Assert.IsFalse(JournalFactsService.IsStale(Checked, Checked.AddDays(365)))
        Assert.IsTrue(JournalFactsService.IsStale(Checked, Checked.AddDays(366)), "Older than a year is marked.")

        Dim noIssn As New JournalRecord With {.Name = "Unindexed"}
        Assert.AreEqual(String.Empty, JournalFactCatalog.SharingPolicyUrl(noIssn))
        noIssn.Issns.Add("2050-084X")
        Assert.AreEqual("https://openpolicyfinder.jisc.ac.uk/search?search=2050-084X", JournalFactCatalog.SharingPolicyUrl(noIssn), "Otherwise, an Open Policy Finder search by ISSN.")
        Assert.IsFalse(JournalFactCatalog.IsOpenPolicyFinderRecord("https://v2.sherpa.ac.uk/id/publication/17599"))
        Assert.IsFalse(JournalFactCatalog.IsOpenPolicyFinderRecord("http://openpolicyfinder.jisc.ac.uk/id/publication/17599"))
        Assert.IsTrue(JournalFactCatalog.IsOpenPolicyFinderRecord("https://openpolicyfinder.jisc.ac.uk/publication/26217"))
    End Sub

    <TestMethod>
    Public Sub MetricsAreLabelledNeverScoredOrRanked()
        Dim valueProperties = GetType(JournalFact).GetProperties().Where(Function(item) item.Name = "Value").ToList()
        Assert.AreEqual(GetType(String), valueProperties.Single().PropertyType, "A metric is display text, never a number to rank by.")
        Assert.IsFalse(GetType(JournalFact).GetProperties().Any(Function(item) item.Name.Contains("Score", StringComparison.OrdinalIgnoreCase) OrElse item.Name.Contains("Rank", StringComparison.OrdinalIgnoreCase)))
        StringAssert.Contains(JournalFactCatalog.DoraNote, "never combines them into one score")

        For Each definition As JournalFactDefinition In JournalFactCatalog.Definitions.Where(Function(item) item.Group <> JournalFactGroup.Publishing)
            Assert.IsTrue(definition.Definition.Length > 40, definition.Key & " explains itself.")
            Assert.IsTrue(definition.WhereUrl.Length = 0 OrElse UrlSafetyService.IsSafeHttpUrl(definition.WhereUrl), definition.Key)
        Next
        Assert.AreEqual(JournalFactCatalog.Definitions.Count, JournalFactCatalog.Definitions.Select(Function(item) item.Key).Distinct().Count())

        Dim example = ExampleLibraryService.Create(New DateTime(2026, 9, 29))
        Dim facts As List(Of JournalFact) = example.Library.Journals.SelectMany(Function(item) item.Facts).ToList()
        Assert.IsTrue(facts.Count > 0 AndAlso facts.All(Function(item) item.Source = JournalFactCatalog.ExampleSource), "Example facts name no real index.")
        Assert.IsTrue(example.Library.Journals.All(Function(item) item.Facts.Any(Function(fact) fact.Source = JournalFactCatalog.ExampleSource)), "Every fictional journal is known as one, so Look Up explains instead of searching.")
        Assert.IsTrue(example.Library.Journals.All(Function(item) item.Issns.Count = 0))
        Assert.IsTrue(example.Library.Journals.SelectMany(Function(item) {item.AimsScopeUrl, item.AuthorInstructionsUrl, item.EditorialBoardUrl}).
                      Where(Function(item) item.Length > 0).All(Function(item) New Uri(item).Host = "example.org"))
    End Sub


    ' ---------------------------------------------------------------
    ' Windows
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheJournalsPageShowsFactsBesideTheListAndSavesALookup()
        RunOnStaThread(
            Sub()
                Dim repository As New AuthorLibraryRepository(Path.Combine(_directory, "data"))
                Dim plos As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
                Dim ampersand As New JournalRecord With {.Name = "Memory & Cognition", .Publisher = "Springer Science+Business Media"}
                repository.Save(New AuthorLibraryData With {.Journals = New List(Of JournalRecord) From {plos, ampersand}})

                Using page As New JournalLibraryForm(Enumerable.Empty(Of Manuscript)(), repository, New RecordedFacts(PlosLookup()))
                    ShowOffscreen(page)
                    page.SelectJournal(plos.Id)
                    Application.DoEvents()
                    Assert.AreEqual("Look Up Facts...", page.Card.LookUpButton.Text)
                    Assert.IsTrue(page.Card.LookUpButton.Enabled)
                    StringAssert.Contains(page.Card.ShownText, "Nothing looked up yet.")
                    StringAssert.Contains(page.Card.ShownText, "Journal metrics describe a journal as a whole")

                    page.lookupPrompt =
                        Function(copy)
                            Assert.AreNotSame(page.SelectedJournal, copy, "The lookup works on a copy.")
                            copy.Name = "Changed, then cancelled"
                            Return Nothing
                        End Function
                    page.LookUpFactsForTest()
                    Assert.AreEqual("PLOS ONE", page.SelectedJournal.Name, "A cancelled lookup changes nothing.")

                    page.lookupPrompt =
                        Function(copy)
                            JournalFactsService.Apply(copy, JournalFactsService.Plan(copy, PlosLookup()))
                            Return copy
                        End Function
                    page.LookUpFactsForTest()
                    Application.DoEvents()

                    Dim saved As JournalRecord = repository.Load().Journals.Single(Function(item) item.Id = plos.Id)
                    Assert.AreEqual("Up to USD 2477 (waivers available)", Fact(saved, JournalFactCatalog.Apc, "DOAJ").Value, "Saved at once.")
                    Assert.AreEqual("Refresh Facts...", page.Card.LookUpButton.Text)
                    Dim shown As String = page.Card.ShownText
                    For Each expected As String In {"Publishing", "Publication fee", "Single anonymous peer review", "Links", "Metrics", "From open data", "2-year mean citedness", "Facts from DOAJ and OpenAlex"}
                        StringAssert.Contains(shown, expected)
                    Next
                    StringAssert.Contains(shown, 605.ToString("N0"))

                    page.SelectJournal(ampersand.Id)
                    Application.DoEvents()
                    StringAssert.Contains(page.Card.ShownText, "Memory & Cognition")
                    Assert.IsTrue(page.Card.Labels.Where(Function(item) item.Text.Contains("&"c)).All(Function(item) Not item.UseMnemonic), "Names show their ampersands.")

                    OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
                    page.SelectJournal(plos.Id)
                    page.SelectJournal(ampersand.Id)
                    Application.DoEvents()
                    Assert.IsFalse(page.Card.LookUpButton.Enabled, "Work offline disables the command...")
                    StringAssert.Contains(page.Card.ShownText, "Work offline is on", "...and says why.")
                    page.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheLookupDialogPreviewsWhatWasFoundBeforeSaving()
        RunOnStaThread(
            Sub()
                Dim record As New JournalRecord With {.Name = "PLOS ONE", .Publisher = "PLOS (my spelling)", .Issns = New List(Of String) From {"1932-6203"}}
                Using dialog As New JournalFactsForm(record, New RecordedFacts(PlosLookup()))
                    ShowOffscreen(dialog)
                    StringAssert.Contains(dialog.IntroText, "send them only its ISSN (1932-6203)")
                    dialog.LookUpAsync().GetAwaiter().GetResult()
                    Application.DoEvents()

                    Dim rows As List(Of ListViewItem) = dialog.ChangesList.Items.Cast(Of ListViewItem)().ToList()
                    Dim kept As ListViewItem = rows.Single(Function(item) item.Text = "Publisher")
                    Assert.AreEqual("Yours is kept", kept.SubItems(4).Text)
                    kept.Checked = True
                    Assert.IsFalse(kept.Checked, "Your own value can't be chosen for replacing.")
                    Assert.IsTrue(rows.Where(Function(item) item.SubItems(4).Text = "Add").All(Function(item) item.Checked))
                    StringAssert.StartsWith(dialog.FooterText, "Data from DOAJ and OpenAlex, both public domain (CC0). Checked ")

                    rows.Single(Function(item) item.Text = "Main topics").Checked = False
                    dialog.SaveForTest()
                    Assert.AreSame(record, dialog.Result)
                End Using
                Assert.AreEqual("PLOS (my spelling)", record.Publisher)
                Assert.IsNull(record.Facts.FirstOrDefault(Function(item) item.Key = JournalFactCatalog.Topics), "Unchecked rows aren't saved.")
                Assert.IsNotNull(record.Facts.FirstOrDefault(Function(item) item.Key = JournalFactCatalog.Review))

                ' A journal without an ISSN is found by name first.
                Dim source As New RecordedFacts(PlosLookup())
                Using dialog As New JournalFactsForm(New JournalRecord With {.Name = "Psychological Science"}, source)
                    ShowOffscreen(dialog)
                    StringAssert.Contains(dialog.IntroText, "has no ISSN yet")
                    Assert.AreEqual("Psychological Science", dialog.SearchBox.Text)
                    Assert.IsFalse(dialog.PrimaryButton.Enabled)
                    dialog.FindAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(5, dialog.MatchesList.Items.Count)
                    dialog.MatchesList.Items(0).Selected = True
                    Application.DoEvents()
                    Assert.IsTrue(dialog.PrimaryButton.Enabled)
                    dialog.LookUpAsync().GetAwaiter().GetResult()
                    Assert.AreEqual("S58854535", source.LastOpenAlexId, "The picked journal is looked up by its OpenAlex id...")
                    CollectionAssert.AreEqual({"0956-7976", "1467-9280"}, source.LastIssns.ToList(), "...and its ISSNs.")
                    Assert.AreEqual("Psychological Science", source.LastSearch)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub EditingAJournalKeepsEverythingTheFormDoesNotShow()
        RunOnStaThread(
            Sub()
                Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
                JournalFactsService.Apply(record, JournalFactsService.Plan(record, PlosLookup()))
                record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "5.2", .Year = 2025, .Source = "Scopus", .EnteredByYou = True})
                Dim before As String = Text.Json.JsonSerializer.Serialize(record)

                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    Assert.AreEqual("1932-6203", editor.IssnsBox.Text)
                    Assert.AreEqual(record.Facts.Count, editor.FactsList.Items.Count)
                    editor.NotesBox.Text = "Quick decisions."
                    editor.SaveForTest()
                    Assert.AreEqual(DialogResult.OK, editor.DialogResult)
                    Dim result As JournalRecord = editor.Result
                    Assert.AreEqual("Quick decisions.", result.Notes)
                    result.Notes = record.Notes
                    Assert.AreEqual(before, Text.Json.JsonSerializer.Serialize(result), "Ids, where each field came from, and every fact survive an edit.")
                End Using

                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    editor.metricPrompt = Function(existing) New JournalFact With {.Key = JournalFactCatalog.Sjr, .Value = "1.1", .Year = 2025, .Source = "SCImago", .EnteredByYou = True}
                    editor.AddMetricForTest()
                    editor.SaveForTest()
                    Assert.AreEqual("1.1", editor.Result.Facts.Single(Function(item) item.Key = JournalFactCatalog.Sjr).Value)
                End Using

                Using metric As New JournalMetricForm()
                    ShowOffscreen(metric)
                    metric.MetricBox.SelectedIndex = JournalFactCatalog.EnteredMetrics.ToList().FindIndex(Function(item) item.Key = JournalFactCatalog.CiteScore)
                    Assert.AreEqual("Scopus (Elsevier)", metric.SourceBox.Text, "The usual source is suggested.")
                    metric.SaveForTest()
                    Assert.IsNull(metric.Result, "A value is required.")
                    metric.ValueBox.Text = "3.1"
                    metric.YearBox.Value = 2025
                    metric.SaveForTest()
                    Assert.AreEqual(JournalFactCatalog.CiteScore, metric.Result.Key)
                    Assert.AreEqual(2025, metric.Result.Year)
                    Assert.IsTrue(metric.Result.EnteredByYou)
                End Using

                Using metric As New JournalMetricForm()
                    ShowOffscreen(metric)
                    metric.MetricBox.SelectedIndex = JournalFactCatalog.EnteredMetrics.Count - 1
                    metric.ValueBox.Text = "42"
                    metric.SourceBox.Text = "The journal's annual report"
                    metric.SaveForTest()
                    Assert.IsNull(metric.Result, "An other metric needs a name.")
                    metric.NameBox.Text = "Median days to acceptance"
                    metric.SaveForTest()
                    Assert.AreEqual("Median days to acceptance", JournalFactCatalog.LabelOf(metric.Result))
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ShortlistQuestionsShowWhatTheJournalsPageKnows()
        RunOnStaThread(
            Sub()
                Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
                JournalFactsService.Apply(record, JournalFactsService.Plan(record, PlosLookup()))
                Using dialog As New JournalCandidateForm(New JournalCandidate With {.JournalName = "PLOS ONE"}, {"PLOS ONE"}, Function(name) "No submissions yet.",
                                                          Function(name) If(name = "PLOS ONE", record, Nothing))
                    ShowOffscreen(dialog)
                    Assert.IsTrue(dialog.Hints("trust.review").Visible)
                    Assert.AreEqual("Peer review: Single anonymous peer review (DOAJ)", dialog.Hints("trust.review").Text)
                    Assert.AreEqual(record.AimsScopeUrl, CStr(dialog.Hints("fit.scope").Tag))
                    Assert.IsFalse(dialog.Hints("fit.audience").Visible)
                    Assert.IsFalse(dialog.CheckBoxes.Any(Function(box) box.Checked), "Nothing is ticked for you.")
                    dialog.JournalBox.Text = "Another Journal"
                    Application.DoEvents()
                    Assert.IsFalse(dialog.Hints("trust.review").Visible)
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function Fixture(name As String) As String
        Return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "JournalFacts", name))
    End Function

    Private Shared Function PlosLookup(Optional checkedUtc As DateTime? = Nothing) As JournalFactsLookup
        Return New JournalFactsLookup With {
            .CheckedUtc = If(checkedUtc, Checked),
            .Issns = New List(Of String) From {"1932-6203"},
            .Doaj = DoajClient.ParseSearch(Fixture("doaj_plos_one.json"), "1932-6203"),
            .DoajChecked = True,
            .OpenAlex = OpenAlexSourceClient.ParseSource(Fixture("openalex_plos_one.json")),
            .OpenAlexChecked = True
        }
    End Function

    ' An amount as the reader's culture shows it.
    Private Shared Function Money(amount As Decimal) As String
        Return amount.ToString("#,##0.##", Globalization.CultureInfo.CurrentCulture)
    End Function

    Private Shared Function Fact(record As JournalRecord, key As String, source As String) As JournalFact
        Return record.Facts.FirstOrDefault(Function(item) item.Key = key AndAlso item.Source = source AndAlso Not item.EnteredByYou)
    End Function

    Private Shared Function Answer(status As HttpStatusCode, body As String, Optional mediaType As String = "application/json") As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, mediaType)}
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
        Public Property UserAgent As String
        Public Property Authorization As String
    End Class

    ' Stands in for the network under the gate.
    Private NotInheritable Class FixtureNetwork
        Inherits HttpMessageHandler

        Public ReadOnly Requests As New List(Of SentRequest)()
        Public Property Respond As Func(Of Uri, HttpResponseMessage)

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            SyncLock Requests
                Requests.Add(New SentRequest With {.Uri = request.RequestUri, .UserAgent = request.Headers.UserAgent.ToString(), .Authorization = request.Headers.Authorization?.ToString()})
            End SyncLock
            Dim response As HttpResponseMessage = Respond(request.RequestUri)
            response.RequestMessage = request
            Return Task.FromResult(response)
        End Function
    End Class

    ' Recorded answers for the dialog and the Journals page.
    Private NotInheritable Class RecordedFacts
        Implements IJournalFactsSource

        Private ReadOnly _lookup As JournalFactsLookup

        Public Sub New(lookup As JournalFactsLookup)
            _lookup = lookup
        End Sub

        Public Property LastIssns As IEnumerable(Of String)
        Public Property LastOpenAlexId As String
        Public Property LastSearch As String

        Public Function LookupAsync(issns As IEnumerable(Of String), openAlexId As String, cancellationToken As CancellationToken) As Task(Of JournalFactsLookup) Implements IJournalFactsSource.LookupAsync
            LastIssns = issns.ToList()
            LastOpenAlexId = openAlexId
            Return Task.FromResult(_lookup)
        End Function

        Public Function SearchAsync(name As String, cancellationToken As CancellationToken) As Task(Of List(Of OpenAlexSourceMatch)) Implements IJournalFactsSource.SearchAsync
            LastSearch = name
            Return Task.FromResult(OpenAlexSourceClient.ParseSearch(Fixture("openalex_search.json")))
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
