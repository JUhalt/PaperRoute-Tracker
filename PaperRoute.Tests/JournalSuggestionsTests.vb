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

' Journals that publish work like yours (#88), on answers recorded from
' OpenAlex on September 30, 2026. No test reaches the network.
<TestClass>
<DoNotParallelize>
Public Class JournalSuggestionsTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 30)
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
    ' Keywords
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub KeywordsComeFromTheManuscriptsKeywordsAndTitleOnly()
        Dim proposals As List(Of KeywordProposal) = JournalSuggestionService.ProposeKeywords(
            "Example: anchoring effects in clinical risk estimates, a preregistered replication",
            {"Clinical judgment", "clinical judgment", "  ", "Anchoring"})

        CollectionAssert.AreEqual({"Clinical judgment", "Anchoring"}, proposals.Where(Function(item) Not item.FromTitle).Select(Function(item) item.Text).ToList(),
                                  "The manuscript's keywords, once each.")
        Assert.IsTrue(proposals.Where(Function(item) Not item.FromTitle).All(Function(item) item.Checked))
        CollectionAssert.AreEqual({"anchoring effects", "clinical risk estimates", "preregistered replication"},
                                  proposals.Where(Function(item) item.FromTitle).Select(Function(item) item.Text).ToList(),
                                  "Title phrases, split at small words and punctuation, with the Example prefix removed.")
        Assert.IsTrue(proposals.Where(Function(item) item.FromTitle).All(Function(item) Not item.Checked), "Title phrases start unchecked.")
        Assert.AreEqual(0, JournalSuggestionService.ProposeKeywords("", Nothing).Count)
    End Sub

    <TestMethod>
    <DataRow("what works?", "what works")>
    <DataRow("""quoted"" AND (retrieval practice)", "quoted and retrieval practice")>
    <DataRow("HIV/AIDS stigma", "HIV AIDS stigma")>
    <DataRow("anchor* effect~2", "anchor effect 2")>
    <DataRow("  NOT  replicable ", "not replicable")>
    <DataRow("   ", "")>
    Public Sub KeywordsAreSentLiterallyWithoutSearchOperators(keyword As String, expected As String)
        Assert.AreEqual(expected, JournalSuggestionService.Sanitize(keyword))
    End Sub

    <TestMethod>
    Public Sub TheRequestShownIsTheRequestSent()
        Dim request As New JournalSuggestionRequest With {.Keywords = New List(Of String) From {"anchoring effects", "clinical judgment"}, .MatchAll = True, .SinceDate = New DateTime(2021, 9, 30)}
        Assert.AreEqual("https://api.openalex.org/works?search=%22anchoring%20effects%22%20AND%20%22clinical%20judgment%22&filter=from_publication_date:2021-09-30,type:article,primary_location.source.type:journal&group_by=primary_location.source.id&per_page=20",
                        JournalSuggestionService.JournalsUrl(request), "The recorded request, with the type pinned: type:journal-article answers nothing.")
        Assert.AreEqual("OpenAlex will receive: ""anchoring effects"" AND ""clinical judgment"" · journal articles published since " & request.SinceDate.ToString("MMM d, yyyy"),
                        JournalSuggestionService.RequestLine(request))
        request.MatchAll = False
        StringAssert.Contains(JournalSuggestionService.SearchText(request), """anchoring effects"" OR ""clinical judgment""")
        request.Keywords = New List(Of String) From {"a", "b", "c", "d", "e", "f", "g", "a"}
        Assert.AreEqual(JournalSuggestionService.MaximumKeywords, JournalSuggestionService.CleanKeywords(request).Count)
        Assert.AreEqual(Today.AddYears(-5), JournalSuggestionService.SinceDate(Today, 5))
    End Sub


    ' ---------------------------------------------------------------
    ' Parsing and searching
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub RecordedAnswersAreRead()
        Dim groups = JournalSuggestionService.ParseGroups(Fixture("journals_and.json"))
        Assert.AreEqual(18, groups.Count)
        Assert.AreEqual("S117214846", groups(0).Id, "Group keys arrive as full URLs.")
        Assert.AreEqual("Journal of Behavioral Decision Making", groups(0).Name)
        Assert.AreEqual(0, JournalSuggestionService.ParseGroups(Fixture("journals_none.json")).Count)

        Dim totals = JournalSuggestionService.ParseGroups(Fixture("totals.json"))
        Assert.AreEqual(163365L, totals.Single(Function(item) item.Id = "S196734849").Count)

        Dim details As List(Of OpenAlexSource) = JournalSuggestionService.ParseSourceList(Fixture("details.json"))
        Assert.AreEqual(18, details.Count)
        Dim reports As OpenAlexSource = details.Single(Function(item) item.Id = "S196734849")
        Assert.AreEqual("Scientific Reports", reports.DisplayName)
        CollectionAssert.AreEqual({"USD 2690", "EUR 2390", "GBP 2190"}, reports.ApcPrices.Select(Function(item) item.ToString()).ToList(), "Listed prices, not the converted apc_usd.")

        Dim examples = JournalSuggestionService.ParseExamples(Fixture("examples.json"))
        Assert.AreEqual(10, examples.Count)
        Assert.AreEqual("S196734849", examples(0).SourceId)
        Assert.AreEqual("10.1038/s41598-026-66155-3", examples(0).Example.Doi, "Without the https://doi.org/ prefix.")
        Assert.AreEqual(2026, examples(0).Example.Year)
    End Sub

    <TestMethod>
    Public Sub ASearchSendsOnlyTheKeywordsAndGathersTheEvidence()
        Dim network As FixtureNetwork = UseNetwork(AddressOf Recorded)
        Dim request As JournalSuggestionRequest = AnchoringRequest()

        Dim result As JournalSuggestionsResult = New OnlineJournalSuggestionsSource().SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult()

        Assert.AreEqual(4, network.Requests.Count, "Journals, totals, details, and examples: four requests.")
        Assert.IsTrue(network.Requests.All(Function(item) item.Uri.Host = "api.openalex.org"))
        Assert.IsFalse(network.Requests.Any(Function(item) item.Uri.AbsoluteUri.Contains("preregistered", StringComparison.OrdinalIgnoreCase)), "Nothing but the checked keywords.")
        Assert.AreEqual(18, result.Journals.Count)
        Assert.IsTrue(result.Journals.All(Function(item) item.MatchingArticles >= 1))
        Dim reports As JournalSuggestion = result.Journals.Single(Function(item) item.OpenAlexId = "S196734849")
        Assert.AreEqual(163365L, reports.AllArticles)
        Assert.AreEqual(True, reports.IsOa)
        CollectionAssert.AreEqual({"2045-2322"}, reports.Issns)
        Assert.IsTrue(reports.ExamplesLoaded)
        Assert.IsTrue(reports.Examples.Count > 0 AndAlso reports.Examples.Count <= 3)
        StringAssert.Contains(network.Requests.Last().Uri.AbsoluteUri, "sort=publication_date:desc", "Examples come newest first.")
        Assert.AreEqual(18, CountIds(network.Requests.Last().Uri), "With few matches, one examples call covers every journal.")
        StringAssert.StartsWith(JournalSuggestionService.AccessText(reports), "Open access · in DOAJ · USD ")
        StringAssert.Contains(JournalSuggestionService.ShareText(reports), " of 163,365 (")
    End Sub

    <TestMethod>
    Public Sub OneStepFailingLeavesTheJournalsFound()
        Dim network As FixtureNetwork = UseNetwork(
            Function(uri)
                If uri.AbsoluteUri.Contains("sort=publication_date", StringComparison.Ordinal) Then
                    Return Answer(CType(429, HttpStatusCode), Fixture("examples_429.json"), retryAfter:=36)
                End If
                Return Recorded(uri)
            End Function)

        Dim result As JournalSuggestionsResult = New OnlineJournalSuggestionsSource().SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual(18, result.Journals.Count)
        Assert.IsTrue(result.Journals.All(Function(item) Not item.ExamplesLoaded))
        StringAssert.StartsWith(result.ExamplesError, "OpenAlex is busy and asked PaperRoute to wait about 36 seconds.")
        StringAssert.Contains(result.ExamplesError, "A free OpenAlex key")

        UseNetwork(Function(uri) Answer(CType(429, HttpStatusCode), Fixture("examples_429.json"), retryAfter:=36))
        Dim busySource As New OnlineJournalSuggestionsSource()
        Dim busy As OnlineServiceBusyException = Assert.ThrowsExactly(Of OnlineServiceBusyException)(
            Sub() busySource.SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult())
        Assert.AreEqual(TimeSpan.FromSeconds(36), busy.RetryAfter)
        Assert.IsFalse(busy.DailyAllowanceUsed)

        network = UseNetwork(Function(uri) Answer(HttpStatusCode.OK, Fixture("journals_none.json")))
        Dim none As JournalSuggestionsResult = New OnlineJournalSuggestionsSource().SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual(0, none.Journals.Count)
        Assert.AreEqual(1, network.Requests.Count, "No journals, no further requests.")
    End Sub

    <TestMethod>
    Public Sub TheDailyAllowanceIsExplainedDifferentlyFromAWait()
        Dim daily As New OnlineServiceBusyException("OpenAlex", TimeSpan.FromHours(6), dailyAllowanceUsed:=True)
        StringAssert.StartsWith(OnlineAccess.Describe(daily, "OpenAlex"), "OpenAlex's free daily allowance is used up. It starts again at ")
        Using response As New HttpResponseMessage(CType(429, HttpStatusCode))
            Dim fromBody As OnlineServiceBusyException = OnlineServiceBusyException.FromResponse("OpenAlex", response, "{""error"":""Budget exceeded"",""retryAfter"":40000}")
            Assert.IsTrue(fromBody.DailyAllowanceUsed, "A long wait outside the anonymous-search limit is the daily allowance.")
            Assert.AreEqual(TimeSpan.FromSeconds(40000), fromBody.RetryAfter)
        End Using
    End Sub

    <TestMethod>
    Public Sub WorkOfflineSendsNothing()
        Dim network As FixtureNetwork = UseNetwork(AddressOf Recorded)
        Dim source As New OnlineJournalSuggestionsSource()
        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        Assert.AreEqual(OnlineBlockReason.WorkOffline,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() source.SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()).Reason)
        OnlineAccess.Configure(New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {OnlineServiceCatalog.JournalSuggestions}})
        Assert.AreEqual(OnlineBlockReason.ServiceOff,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() source.ExamplesAsync(AnchoringRequest(), "S196734849", CancellationToken.None).GetAwaiter().GetResult()).Reason)
        Assert.AreEqual(0, network.Requests.Count)
    End Sub


    ' ---------------------------------------------------------------
    ' The shortlist
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub FoundJournalsJoinTheShortlistOnceAndNeverOverwriteOne()
        Dim mine As New JournalRecord With {.Name = "Sci Rep (my record)", .Issns = New List(Of String) From {"2045-2322"}}
        Dim manuscript As New Manuscript With {.Title = "Anchoring"}
        manuscript.JournalShortlist.Add(New JournalCandidate With {.JournalName = "Frontiers in Psychology", .Status = CandidateStatus.Preferred, .Notes = "Mine"})

        Dim added = JournalShortlistService.AddFound(manuscript, "Scientific Reports", {"2045-2322"}, "S196734849", {mine})
        Assert.IsTrue(added.Created)
        Assert.AreEqual("Sci Rep (my record)", added.Candidate.JournalName, "Linked to the Journal Library by ISSN, under the library's name.")
        Assert.AreEqual(mine.Id, added.Candidate.JournalId)
        Assert.AreEqual(CandidateStatus.Considering, added.Candidate.Status)
        added.Candidate.Evidence = New CandidateEvidence With {.OpenAlexId = "S196734849", .Issns = New List(Of String) From {"2045-2322"}}

        Dim again = JournalShortlistService.AddFound(manuscript, "Scientific Reports", {"2045-2322"}, "S196734849", {mine})
        Assert.IsFalse(again.Created)
        Assert.AreSame(added.Candidate, again.Candidate)

        Dim existing = JournalShortlistService.AddFound(manuscript, "frontiers in  psychology", {"1664-1078"}, "S9692511", Nothing)
        Assert.IsFalse(existing.Created)
        Assert.AreEqual(CandidateStatus.Preferred, existing.Candidate.Status, "An existing candidate is untouched.")
        Assert.AreEqual("Mine", existing.Candidate.Notes)
        Assert.IsNull(existing.Candidate.Evidence)

        Assert.IsNotNull(JournalShortlistService.FindCandidate(manuscript, "Some other name", Nothing, "S196734849", Nothing), "Known by its OpenAlex id.")
        Assert.AreEqual(2, manuscript.JournalShortlist.Count)
    End Sub

    <TestMethod>
    Public Sub EvidenceIsCopiedTidiedAndSaved()
        Dim candidate As New JournalCandidate With {
            .JournalName = "Scientific Reports",
            .Evidence = New CandidateEvidence With {
                .Source = "OpenAlex", .OpenAlexId = "https://openalex.org/S196734849", .Issns = New List(Of String) From {"2045-2322", "bad"},
                .Keywords = New List(Of String) From {"anchoring effects", " "}, .MatchingArticles = -3, .AllArticles = -1,
                .Examples = New List(Of EvidenceExample) From {
                    New EvidenceExample With {.Title = "One", .Doi = "https://doi.org/10.1038/X1", .Year = 2025},
                    New EvidenceExample With {.Title = "Two", .Doi = "not a doi"},
                    Nothing,
                    New EvidenceExample With {.Title = "Three"},
                    New EvidenceExample With {.Title = "Four"}
                }
            }
        }
        Dim manuscript As New Manuscript With {.Title = "Anchoring"}
        manuscript.JournalShortlist.AddRange({candidate, Nothing})

        JournalShortlistService.NormalizeManuscript(manuscript)
        Assert.AreEqual(1, manuscript.JournalShortlist.Count, "An empty candidate is dropped, not fatal.")
        Dim evidence As CandidateEvidence = candidate.Evidence
        Assert.AreEqual("S196734849", evidence.OpenAlexId)
        CollectionAssert.AreEqual({"2045-2322"}, evidence.Issns)
        CollectionAssert.AreEqual({"anchoring effects"}, evidence.Keywords)
        Assert.AreEqual(0L, evidence.MatchingArticles)
        Assert.IsFalse(evidence.AllArticles.HasValue)
        Assert.AreEqual(3, evidence.Examples.Count)
        Assert.AreEqual("10.1038/X1", evidence.Examples(0).Doi)
        Assert.AreEqual(String.Empty, evidence.Examples(1).Doi)

        Dim copy As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
        copy.JournalShortlist(0).Evidence.Examples(0).Title = "Changed in the copy"
        Assert.AreEqual("One", candidate.Evidence.Examples(0).Title, "The page's working copy has its own evidence.")

        Dim repository As New ManuscriptRepository(Path.Combine(_directory, "data"), Path.Combine(_directory, "managed"))
        repository.Save(New List(Of Manuscript) From {manuscript})
        Dim loaded As JournalCandidate = repository.Load().Single().JournalShortlist.Single()
        Assert.AreEqual(3, loaded.Evidence.Examples.Count)
        Assert.AreEqual("S196734849", loaded.Evidence.OpenAlexId)

        Dim line As String = JournalShortlistService.EvidenceLine(New CandidateEvidence With {.Source = "OpenAlex", .MatchingArticles = 144, .AllArticles = 9812, .SinceDate = New DateTime(2021, 9, 30), .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)})
        StringAssert.StartsWith(line, "Found by keyword search: 144 matching articles of " & 9812.ToString("N0") & " since 2021 (OpenAlex, ")
        Assert.IsFalse(GetType(CandidateEvidence).GetProperties().Any(Function(item) item.Name.Contains("Score") OrElse item.Name.Contains("Rank") OrElse item.Name.Contains("Chance")), "Evidence, never a score.")
    End Sub

    <TestMethod>
    Public Sub TheExampleLibrarysEvidenceIsFictional()
        Dim evidence As CandidateEvidence = ExampleLibraryService.Create(Today).Manuscripts.
            SelectMany(Function(item) item.JournalShortlist).
            Select(Function(item) item.Evidence).
            Single(Function(item) item IsNot Nothing)
        Assert.AreEqual(JournalFactCatalog.ExampleSource, evidence.Source)
        Assert.IsTrue(evidence.Examples.All(Function(item) item.Title.StartsWith("Example:", StringComparison.Ordinal) AndAlso item.Doi.StartsWith("10.5555/", StringComparison.Ordinal)))
    End Sub


    ' ---------------------------------------------------------------
    ' The window
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheWindowShowsTheRequestThenTheEvidence()
        RunOnStaThread(
            Sub()
                Dim result As JournalSuggestionsResult = RecordedResult()
                Dim source As New RecordedSuggestions(result)
                Using dialog As New JournalSuggestionsForm("Example: anchoring effects in clinical risk estimates", {"clinical judgment"}, source,
                                                           Function(journal) journal.OpenAlexId = "S196734849",
                                                           Function(journal) If(journal.OpenAlexId = "S9692511", "Submitted 2" & ChrW(&HD7) & ", last 2024", String.Empty),
                                                           Today)
                    ShowOffscreen(dialog)
                    CollectionAssert.AreEqual({"clinical judgment", "anchoring effects", "clinical risk estimates"}, dialog.KeywordsList.Items.Cast(Of String)().ToList())
                    Assert.AreEqual(1, dialog.KeywordsList.CheckedItems.Count)
                    dialog.KeywordsList.SetItemChecked(1, True)
                    dialog.AddKeywordForTest("what works?")
                    Application.DoEvents()
                    StringAssert.StartsWith(dialog.RequestText, "OpenAlex will receive: ""clinical judgment"" AND ""anchoring effects"" AND ""what works"" · journal articles published since ")
                    Assert.AreEqual(JournalSuggestionService.JournalsUrl(dialog.CurrentRequest()), dialog.AddressText, "The address shown is the one sent.")

                    dialog.SearchAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Assert.AreEqual(3, source.Request.Keywords.Count)
                    Dim rows As List(Of ListViewItem) = dialog.JournalsList.Items.Cast(Of ListViewItem)().ToList()
                    Assert.AreEqual(result.Journals.Count, rows.Count)
                    Dim onShortlist As ListViewItem = rows.Single(Function(item) DirectCast(item.Tag, JournalSuggestion).OpenAlexId = "S196734849")
                    Assert.AreEqual("On your shortlist", onShortlist.SubItems(4).Text)
                    onShortlist.Checked = True
                    Assert.IsFalse(onShortlist.Checked, "A journal already shortlisted can't be added again.")
                    Assert.AreEqual("Submitted 2" & ChrW(&HD7) & ", last 2024", rows.Single(Function(item) DirectCast(item.Tag, JournalSuggestion).OpenAlexId = "S9692511").SubItems(4).Text)

                    Dim withExamples As ListViewItem = rows.First(Function(item) DirectCast(item.Tag, JournalSuggestion).Examples.Count > 0)
                    withExamples.Selected = True
                    dialog.ShowExamplesForTest().GetAwaiter().GetResult()
                    StringAssert.StartsWith(dialog.ExampleTexts(0), "Recent matching articles in ")
                    Assert.AreEqual(DirectCast(withExamples.Tag, JournalSuggestion).Examples(0).Title & " (" & DirectCast(withExamples.Tag, JournalSuggestion).Examples(0).Year.ToString() & ")", dialog.ExampleTexts(1))

                    Dim picks As List(Of ListViewItem) = rows.Where(Function(item) item IsNot onShortlist).Take(2).ToList()
                    picks.ForEach(Sub(item) item.Checked = True)
                    Application.DoEvents()
                    Assert.AreEqual("Add 2 to Shortlist", dialog.PrimaryButton.Text)
                    dialog.AddCheckedForTest()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(2, dialog.Chosen.Count)
                End Using

                ' A busy answer says how long, and Search waits that long.
                Using dialog As New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, New RecordedSuggestions(Nothing, New OnlineServiceBusyException("OpenAlex", TimeSpan.FromSeconds(36), False)),
                                                           Nothing, Nothing, Today)
                    ShowOffscreen(dialog)
                    dialog.SearchAsync().GetAwaiter().GetResult()
                    StringAssert.StartsWith(dialog.StatusText, "OpenAlex is busy and asked PaperRoute to wait about 36 seconds.")
                    Assert.IsFalse(dialog.PrimaryButton.Enabled)
                    StringAssert.StartsWith(dialog.PrimaryButton.Text, "Search (")
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    ' Review fixes (#88).
    <TestMethod>
    Public Sub ExamplesLeftOutOfTheNewestHundredAreFetchedLater()
        UseNetwork(
            Function(uri)
                If uri.AbsoluteUri.Contains("sort=publication_date", StringComparison.Ordinal) Then
                    Return Answer(HttpStatusCode.OK, Fixture("examples.json").Replace("{""meta"": {""count"": 10,", "{""meta"": {""count"": 500,"))
                End If
                Return Recorded(uri)
            End Function)
        Dim result As JournalSuggestionsResult = New OnlineJournalSuggestionsSource().SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()
        Assert.IsTrue(result.Journals.Any(Function(item) Not item.ExamplesLoaded) AndAlso result.Journals.Where(Function(item) item.Examples.Count < 3).All(Function(item) Not item.ExamplesLoaded),
                      "A journal the newest 100 may have missed is looked up on its own when shown.")
        Assert.IsTrue(result.Journals.Where(Function(item) item.Examples.Count >= 3).All(Function(item) item.ExamplesLoaded))
    End Sub

    <TestMethod>
    Public Sub TypographicApostrophesKeepWordsWhole()
        Dim proposals As List(Of KeywordProposal) = JournalSuggestionService.ProposeKeywords("Children" & ChrW(&H2019) & "s sleep quality and reading", Nothing)
        Assert.IsFalse(proposals.Any(Function(item) item.Text.StartsWith("s ", StringComparison.Ordinal)), String.Join(" | ", proposals.Select(Function(item) item.Text)))
    End Sub

    <TestMethod>
    Public Sub TheKeyHintIsOnlyForResearchersWithoutAKey()
        Dim busy As New OnlineServiceBusyException("OpenAlex", TimeSpan.FromSeconds(30), False)
        StringAssert.Contains(OnlineAccess.Describe(busy, "OpenAlex"), "A free OpenAlex key")
        OnlineAccess.KeyStore().Save(ProtectedKeyStore.OpenAlex, "test-key-for-this-test-only")
        Assert.IsFalse(OnlineAccess.Describe(busy, "OpenAlex").Contains("A free OpenAlex key"), "A researcher with a key isn't told to add one.")
    End Sub

    <TestMethod>
    Public Sub AJournalShownAsAddableIsAdded()
        ' The shortlist has the journal under OpenAlex's name; the library knows it, by ISSN, under another.
        Dim manuscript As New Manuscript With {.Title = "A manuscript"}
        manuscript.JournalShortlist.Add(New JournalCandidate With {.JournalName = "Scientific Reports", .Status = CandidateStatus.Considering})
        Dim library As New List(Of JournalRecord) From {New JournalRecord With {.Name = "Sci Rep", .Issns = New List(Of String) From {"2045-2322"}}}
        Dim shown As JournalCandidate = JournalShortlistService.FindFound(manuscript, "Scientific Reports", {"2045-2322"}, "S196734849", library)
        Assert.IsNotNull(shown, "The window says it is on the shortlist...")
        Dim added = JournalShortlistService.AddFound(manuscript, "Scientific Reports", {"2045-2322"}, "S196734849", library)
        Assert.AreSame(shown, added.Candidate, "...because adding it finds the same candidate.")
        Assert.IsFalse(added.Created)
    End Sub

    <TestMethod>
    Public Sub EnterInTheKeywordBoxAddsTheKeywordWithoutSearching()
        RunOnStaThread(
            Sub()
                Dim source As New RecordedSuggestions(New JournalSuggestionsResult())
                Using dialog As New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, source, Nothing, Nothing, Today)
                    ShowOffscreen(dialog)
                    GetType(Control).GetMethod("OnEnter", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).Invoke(dialog.AddKeywordBox, New Object() {EventArgs.Empty})
                    Assert.AreNotSame(dialog.PrimaryButton, dialog.AcceptButton, "Enter belongs to Add while typing a keyword.")
                    dialog.AddKeywordBox.Text = "risk perception"
                    DirectCast(dialog.AcceptButton, Button).PerformClick()
                    Application.DoEvents()
                    Assert.IsTrue(dialog.KeywordsList.Items.Cast(Of String)().Contains("risk perception"))
                    Assert.IsNull(source.Request, "Nothing was searched.")
                    GetType(Control).GetMethod("OnLeave", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).Invoke(dialog.AddKeywordBox, New Object() {EventArgs.Empty})
                    Assert.AreSame(dialog.PrimaryButton, dialog.AcceptButton)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ABusyAnswerForExamplesIsRespected()
        RunOnStaThread(
            Sub()
                Dim result As JournalSuggestionsResult = RecordedResult()
                result.Journals.ForEach(Sub(item) item.ExamplesLoaded = False)
                Dim source As New RecordedSuggestions(result, examplesFailure:=New OnlineServiceBusyException("OpenAlex", TimeSpan.FromSeconds(36), False))
                Using dialog As New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, source, Nothing, Nothing, Today)
                    ShowOffscreen(dialog)
                    dialog.SearchAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Dim first As Integer = source.ExampleRequests
                    Assert.AreEqual(1, first, "The first journal shown asks once.")
                    StringAssert.StartsWith(dialog.ExampleTexts(1), "Examples couldn't be loaded: OpenAlex is busy")
                    dialog.JournalsList.Items(1).Selected = True
                    dialog.ShowExamplesForTest().GetAwaiter().GetResult()
                    Assert.AreEqual(first, source.ExampleRequests, "No request while OpenAlex asked PaperRoute to wait.")
                    StringAssert.StartsWith(dialog.ExampleTexts(1), "Examples couldn't be loaded: OpenAlex is busy")
                    Assert.IsTrue(dialog.CancelButtonForTest.Enabled, "Cancel still works.")
                    dialog.Close()
                End Using

                ' A busy answer during the search holds the examples too.
                Dim held As JournalSuggestionsResult = RecordedResult()
                held.Journals.ForEach(Sub(item) item.ExamplesLoaded = False)
                held.ExamplesBusy = New OnlineServiceBusyException("OpenAlex", TimeSpan.FromSeconds(36), False)
                Dim quiet As New RecordedSuggestions(held)
                Using dialog As New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, quiet, Nothing, Nothing, Today)
                    ShowOffscreen(dialog)
                    dialog.SearchAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Assert.AreEqual(0, quiet.ExampleRequests, "No examples request right after OpenAlex asked PaperRoute to wait.")
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function AnchoringRequest() As JournalSuggestionRequest
        Return New JournalSuggestionRequest With {.Keywords = New List(Of String) From {"anchoring effects", "clinical judgment"}, .MatchAll = True, .SinceDate = New DateTime(2021, 9, 30)}
    End Function

    Private Shared Function Fixture(name As String) As String
        Return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "JournalSuggestions", name))
    End Function

    ' The recorded answer for each of the four requests.
    Private Shared Function Recorded(uri As Uri) As HttpResponseMessage
        Dim address As String = uri.AbsoluteUri
        If address.Contains("/sources?", StringComparison.Ordinal) Then Return Answer(HttpStatusCode.OK, Fixture("details.json"))
        If address.Contains("sort=publication_date", StringComparison.Ordinal) Then Return Answer(HttpStatusCode.OK, Fixture("examples.json"))
        If address.Contains("search=", StringComparison.Ordinal) Then Return Answer(HttpStatusCode.OK, Fixture("journals_and.json"))
        Return Answer(HttpStatusCode.OK, Fixture("totals.json"))
    End Function

    Private Shared Function RecordedResult() As JournalSuggestionsResult
        OnlineAccess.ResetForTests()
        OnlineAccess.InnerHandlerFactory = Function() New FixtureNetwork With {.Respond = AddressOf Recorded}
        Try
            Return New OnlineJournalSuggestionsSource().SearchAsync(AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()
        Finally
            OnlineAccess.ResetForTests()
        End Try
    End Function

    Private Shared Function CountIds(uri As Uri) As Integer
        Dim text As String = Uri.UnescapeDataString(uri.AbsoluteUri)
        Dim start As Integer = text.IndexOf("primary_location.source.id:", StringComparison.Ordinal) + "primary_location.source.id:".Length
        Dim finish As Integer = text.IndexOf("&", start, StringComparison.Ordinal)
        Return text.Substring(start, finish - start).Split("|"c).Length
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
    End Class

    Private NotInheritable Class FixtureNetwork
        Inherits HttpMessageHandler

        Public ReadOnly Requests As New List(Of SentRequest)()
        Public Property Respond As Func(Of Uri, HttpResponseMessage)

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            SyncLock Requests
                Requests.Add(New SentRequest With {.Uri = request.RequestUri})
            End SyncLock
            Dim response As HttpResponseMessage = Respond(request.RequestUri)
            response.RequestMessage = request
            Return Task.FromResult(response)
        End Function
    End Class

    Private NotInheritable Class RecordedSuggestions
        Implements IJournalSuggestionsSource

        Private ReadOnly _result As JournalSuggestionsResult
        Private ReadOnly _failure As Exception
        Private ReadOnly _examplesFailure As Exception

        Public Sub New(result As JournalSuggestionsResult, Optional failure As Exception = Nothing, Optional examplesFailure As Exception = Nothing)
            _result = result
            _failure = failure
            _examplesFailure = examplesFailure
        End Sub

        Public Property ExampleRequests As Integer

        Public Property Request As JournalSuggestionRequest

        Public Function SearchAsync(request As JournalSuggestionRequest, cancellationToken As CancellationToken) As Task(Of JournalSuggestionsResult) Implements IJournalSuggestionsSource.SearchAsync
            Me.Request = request
            If _failure IsNot Nothing Then Return Task.FromException(Of JournalSuggestionsResult)(_failure)
            _result.Request = request
            Return Task.FromResult(_result)
        End Function

        Public Function ExamplesAsync(request As JournalSuggestionRequest, openAlexId As String, cancellationToken As CancellationToken) As Task(Of List(Of EvidenceExample)) Implements IJournalSuggestionsSource.ExamplesAsync
            ExampleRequests += 1
            If _examplesFailure IsNot Nothing Then Return Task.FromException(Of List(Of EvidenceExample))(_examplesFailure)
            Return Task.FromResult(New List(Of EvidenceExample))
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
