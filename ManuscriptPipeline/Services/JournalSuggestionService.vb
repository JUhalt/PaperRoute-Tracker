Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    ' A keyword offered for review before anything is sent (#88).
    Public NotInheritable Class KeywordProposal

        Public Property Text As String = String.Empty
        Public Property Checked As Boolean
        ' From the title rather than the manuscript's keywords.
        Public Property FromTitle As Boolean

    End Class


    ' What the researcher chose to search for.
    Public NotInheritable Class JournalSuggestionRequest

        Public Property Keywords As New List(Of String)()
        Public Property MatchAll As Boolean = True
        Public Property SinceDate As DateTime

    End Class


    ' One journal that published matching articles, with the evidence.
    Public NotInheritable Class JournalSuggestion

        Public Property OpenAlexId As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property Publisher As String = String.Empty
        Public Property Issns As New List(Of String)()
        Public Property IsOa As Boolean?
        Public Property IsInDoaj As Boolean?
        Public Property ApcPrices As New List(Of ListedPrice)()
        Public Property MatchingArticles As Long
        ' All its articles in the same years, when OpenAlex answered.
        Public Property AllArticles As Long?
        Public Property Examples As New List(Of EvidenceExample)()
        ' Examples were asked for; none may have been found.
        Public Property ExamplesLoaded As Boolean

    End Class


    Public NotInheritable Class JournalSuggestionsResult

        Public Property Request As JournalSuggestionRequest
        Public Property RetrievedUtc As DateTime = DateTime.UtcNow
        Public Property Journals As New List(Of JournalSuggestion)()
        ' Matching articles in all journals.
        Public Property TotalMatching As Long
        Public Property TotalsError As String = String.Empty
        Public Property DetailsError As String = String.Empty
        Public Property ExamplesError As String = String.Empty
        ' When OpenAlex asked PaperRoute to wait before more examples.
        Public Property ExamplesBusy As OnlineServiceBusyException

    End Class


    ' Where suggestions come from; tests supply recorded answers.
    Public Interface IJournalSuggestionsSource

        Function SearchAsync(request As JournalSuggestionRequest, cancellationToken As CancellationToken) As Task(Of JournalSuggestionsResult)

        ' Up to three recent matching articles in one journal.
        Function ExamplesAsync(request As JournalSuggestionRequest, openAlexId As String, cancellationToken As CancellationToken) As Task(Of List(Of EvidenceExample))

    End Interface


    ' Journals that publish work like yours (#88): recent journal articles in
    ' OpenAlex that mention the researcher's reviewed keywords, grouped by
    ' journal. Counts are evidence to read, never a ranking or a prediction.
    Public NotInheritable Class JournalSuggestionService

        Private Sub New()
        End Sub

        Public Const MaximumKeywords As Integer = 6
        Public Const MaximumKeywordLength As Integer = 80
        Public Const JournalsShown As Integer = 20

        Public Const WorksBase As String = "https://api.openalex.org/works"
        Public Const SourcesBase As String = "https://api.openalex.org/sources"

        Public Const Footnote As String =
            "Counts come from OpenAlex and favor large journals. They are evidence to read, not a ranking of quality or of your chances."

        Private Shared ReadOnly StopWords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "a", "an", "the", "and", "or", "but", "nor", "of", "in", "on", "at", "to", "for", "from", "by", "with", "without",
            "into", "onto", "over", "under", "about", "across", "among", "between", "through", "during", "after", "before",
            "versus", "vs", "via", "as", "is", "are", "was", "were", "be", "been", "being", "its", "their", "this", "that",
            "these", "those", "how", "what", "when", "where", "which", "who", "why", "do", "does", "can", "not", "new",
            "study", "studies", "using", "use", "toward", "towards", "case"
        }


        ' The manuscript's keywords, checked, then phrases from its title,
        ' unchecked. The abstract, notes, and files are never used.
        Public Shared Function ProposeKeywords(title As String, keywords As IEnumerable(Of String)) As List(Of KeywordProposal)

            Dim proposals As New List(Of KeywordProposal)()
            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each keyword As String In If(keywords, Enumerable.Empty(Of String)())
                Dim clean As String = Sanitize(keyword)
                If clean.Length < 3 OrElse Not seen.Add(clean) Then Continue For
                proposals.Add(New KeywordProposal With {.Text = clean, .Checked = proposals.Count < MaximumKeywords})
            Next

            ' A typographic apostrophe is an apostrophe, so "Children’s" stays whole.
            Dim text As String = If(title, String.Empty).Trim().Replace("’"c, "'"c)
            If text.StartsWith("Example:", StringComparison.OrdinalIgnoreCase) Then text = text.Substring("Example:".Length)

            Dim titlePhrases As Integer = 0
            For Each segment As String In Regex.Split(text, "[,:;.!?()\[\]{}""“”‘’/\\|–—]+|\s-\s")
                Dim phrase As New List(Of String)()
                For Each word As String In Regex.Split(segment.Trim(), "\s+")
                    Dim token As String = word.Trim("'"c, "-"c)
                    If token.Length = 0 OrElse StopWords.Contains(token) OrElse Regex.IsMatch(token, "^\d+$") Then
                        titlePhrases += AddPhrase(proposals, seen, phrase)
                        phrase.Clear()
                    Else
                        phrase.Add(token)
                    End If
                Next
                titlePhrases += AddPhrase(proposals, seen, phrase)
                If titlePhrases >= 8 Then Exit For
            Next

            Return proposals

        End Function


        Private Shared Function AddPhrase(proposals As List(Of KeywordProposal), seen As HashSet(Of String), words As List(Of String)) As Integer
            If words.Count = 0 OrElse words.Count > 5 Then Return 0
            Dim clean As String = Sanitize(String.Join(" ", words)).ToLowerInvariant()
            If clean.Length < 3 OrElse Not seen.Add(clean) Then Return 0
            proposals.Add(New KeywordProposal With {.Text = clean, .FromTitle = True})
            Return 1
        End Function


        ' A keyword OpenAlex reads literally: its search operators and
        ' wildcards (a "?" silently changes a phrase) become spaces, and the
        ' words AND, OR, and NOT are lower-cased.
        Public Shared Function Sanitize(keyword As String) As String
            If String.IsNullOrWhiteSpace(keyword) Then Return String.Empty
            Dim builder As New StringBuilder()
            For Each character As Char In keyword
                If Char.IsControl(character) OrElse """*?~()[]{}\:^!|,+&<>=/;".IndexOf(character) >= 0 Then
                    builder.Append(" "c)
                Else
                    builder.Append(character)
                End If
            Next
            Dim clean As String = Regex.Replace(builder.ToString(), "\s+", " ").Trim()
            clean = Regex.Replace(clean, "\b(AND|OR|NOT)\b", Function(match) match.Value.ToLowerInvariant())
            clean = clean.Trim("-"c, "'"c, " "c)
            If clean.Length > MaximumKeywordLength Then clean = clean.Substring(0, MaximumKeywordLength).Trim()
            Return clean
        End Function


        Public Shared Function SinceDate(today As DateTime, years As Integer) As DateTime
            Return today.Date.AddYears(-Math.Max(1, years))
        End Function


        ' "anchoring effects" AND "clinical judgment"
        Public Shared Function SearchText(request As JournalSuggestionRequest) As String
            Return String.Join(If(request.MatchAll, " AND ", " OR "), CleanKeywords(request).Select(Function(item) """" & item & """"))
        End Function


        Public Shared Function CleanKeywords(request As JournalSuggestionRequest) As List(Of String)
            Return If(request?.Keywords, New List(Of String)()).
                Select(AddressOf Sanitize).
                Where(Function(item) item.Length > 0).
                Distinct(StringComparer.OrdinalIgnoreCase).
                Take(MaximumKeywords).
                ToList()
        End Function


        ' What the researcher sees before searching.
        Public Shared Function RequestLine(request As JournalSuggestionRequest) As String
            If CleanKeywords(request).Count = 0 Then Return "Check at least one keyword."
            Return "OpenAlex will receive: " & SearchText(request) & " · journal articles published since " &
                request.SinceDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
        End Function


        ' ---------------------------------------------------------------
        ' Requests: the same functions build what is shown and what is sent.
        ' ---------------------------------------------------------------

        Private Shared Function Since(request As JournalSuggestionRequest) As String
            Return request.SinceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        End Function


        ' Matching journal articles, counted by journal (a list call).
        Public Shared Function JournalsUrl(request As JournalSuggestionRequest) As String
            Return WorksBase & "?search=" & Uri.EscapeDataString(SearchText(request)) &
                "&filter=from_publication_date:" & Since(request) & ",type:article,primary_location.source.type:journal" &
                "&group_by=primary_location.source.id&per_page=" & JournalsShown.ToString(CultureInfo.InvariantCulture)
        End Function


        ' All articles in the same years, for the same journals.
        Public Shared Function TotalsUrl(request As JournalSuggestionRequest, ids As IEnumerable(Of String)) As String
            Return WorksBase & "?filter=primary_location.source.id:" & String.Join("|", ids) &
                ",from_publication_date:" & Since(request) & ",type:article" &
                "&group_by=primary_location.source.id&per_page=50"
        End Function


        ' The journals' names, ISSNs, publishers, and open-access facts.
        Public Shared Function DetailsUrl(ids As IEnumerable(Of String)) As String
            Return SourcesBase & "?filter=openalex:" & String.Join("|", ids) &
                "&select=id,display_name,issn_l,issn,host_organization_name,is_oa,is_in_doaj,apc_prices,homepage_url&per_page=50"
        End Function


        ' The newest matching articles in these journals (a search).
        Public Shared Function ExamplesUrl(request As JournalSuggestionRequest, ids As IEnumerable(Of String), perPage As Integer) As String
            Return WorksBase & "?search=" & Uri.EscapeDataString(SearchText(request)) &
                "&filter=from_publication_date:" & Since(request) & ",type:article,primary_location.source.type:journal,primary_location.source.id:" & String.Join("|", ids) &
                "&sort=publication_date:desc&per_page=" & perPage.ToString(CultureInfo.InvariantCulture) & "&select=id,doi,title,publication_year,primary_location"
        End Function


        ' ---------------------------------------------------------------
        ' Answers
        ' ---------------------------------------------------------------

        ' group_by rows: (short id, name, count), skipping empty keys.
        Friend Shared Function ParseGroups(json As String) As List(Of (Id As String, Name As String, Count As Long))
            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim groups As JsonElement = JsonFacts.Child(document.RootElement, "group_by")
                    If groups.ValueKind <> JsonValueKind.Array Then Throw New JournalFactsFormatException("OpenAlex")
                    Dim rows As New List(Of (Id As String, Name As String, Count As Long))()
                    For Each group As JsonElement In groups.EnumerateArray()
                        Dim id As String = OpenAlexSourceClient.NormalizeId(JsonFacts.Text(group, "key"))
                        Dim count As Long? = JsonFacts.WholeLong(group, "count")
                        If id.Length = 0 OrElse Not count.HasValue OrElse count.Value <= 0 Then Continue For
                        rows.Add((id, JsonFacts.Text(group, "key_display_name"), count.Value))
                    Next
                    Return rows
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try
        End Function


        Friend Shared Function ParseSourceList(json As String) As List(Of OpenAlexSource)
            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim results As JsonElement = JsonFacts.Child(document.RootElement, "results")
                    If results.ValueKind <> JsonValueKind.Array Then Throw New JournalFactsFormatException("OpenAlex")
                    Return results.EnumerateArray().
                        Select(AddressOf OpenAlexSourceClient.ParseSourceElement).
                        Where(Function(item) item IsNot Nothing).
                        ToList()
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try
        End Function


        ' Works as (journal id, example), newest first as OpenAlex sorted them.
        Friend Shared Function ParseExamples(json As String) As List(Of (SourceId As String, Example As EvidenceExample))
            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim results As JsonElement = JsonFacts.Child(document.RootElement, "results")
                    If results.ValueKind <> JsonValueKind.Array Then Throw New JournalFactsFormatException("OpenAlex")
                    Dim examples As New List(Of (SourceId As String, Example As EvidenceExample))()
                    For Each work As JsonElement In results.EnumerateArray()
                        Dim source As String = OpenAlexSourceClient.NormalizeId(JsonFacts.Text(JsonFacts.Child(JsonFacts.Child(work, "primary_location"), "source"), "id"))
                        Dim title As String = JsonFacts.Text(work, "title")
                        If source.Length = 0 OrElse title.Length = 0 Then Continue For
                        Dim doi As String = DoiNormalizer.Normalize(JsonFacts.RawText(work, "doi"))
                        examples.Add((source, New EvidenceExample With {
                            .Title = If(title.Length > 300, title.Substring(0, 300), title),
                            .Year = JsonFacts.Whole(work, "publication_year"),
                            .Doi = If(DoiNormalizer.IsValid(doi), doi, String.Empty)
                        }))
                    Next
                    Return examples
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try
        End Function


        ' The evidence stored with a shortlisted journal.
        Public Shared Function EvidenceFor(suggestion As JournalSuggestion, result As JournalSuggestionsResult) As CandidateEvidence
            Return New CandidateEvidence With {
                .Source = JournalFactCatalog.OpenAlexSource,
                .OpenAlexId = suggestion.OpenAlexId,
                .Issns = suggestion.Issns.ToList(),
                .Keywords = CleanKeywords(result.Request),
                .MatchAll = result.Request.MatchAll,
                .SinceDate = result.Request.SinceDate,
                .MatchingArticles = suggestion.MatchingArticles,
                .AllArticles = suggestion.AllArticles,
                .Examples = suggestion.Examples.Take(3).Select(Function(item) New EvidenceExample With {.Title = item.Title, .Year = item.Year, .Doi = item.Doi}).ToList(),
                .RetrievedUtc = result.RetrievedUtc
            }
        End Function


        ' "144 of 9,812 (1.5%)"
        Public Shared Function ShareText(suggestion As JournalSuggestion) As String
            Dim culture As CultureInfo = CultureInfo.CurrentCulture
            If Not suggestion.AllArticles.HasValue OrElse suggestion.AllArticles.Value <= 0 Then Return "Not available"
            Dim share As Double = suggestion.MatchingArticles / CDbl(suggestion.AllArticles.Value)
            Dim percent As String = If(share < 0.0001, "<" & 0.0001.ToString("P2", culture), share.ToString(If(share < 0.001, "P2", "P1"), culture))
            Return suggestion.MatchingArticles.ToString("N0", culture) & " of " & suggestion.AllArticles.Value.ToString("N0", culture) & " (" & percent & ")"
        End Function


        ' "Open access · in DOAJ · USD 2,690", or "Subscription or hybrid ·
        ' USD 3,900 (optional)", from the listed prices, never converted.
        Public Shared Function AccessText(suggestion As JournalSuggestion) As String
            Dim parts As New List(Of String)()
            If suggestion.IsOa = True Then
                parts.Add("Open access")
            ElseIf suggestion.IsOa = False Then
                parts.Add("Subscription or hybrid")
            End If
            If suggestion.IsInDoaj = True Then parts.Add("in DOAJ")
            Dim price As ListedPrice? = If(suggestion.ApcPrices.Any(Function(item) item.Currency = "USD"),
                                           suggestion.ApcPrices.First(Function(item) item.Currency = "USD"),
                                           If(suggestion.ApcPrices.Count > 0, suggestion.ApcPrices(0), CType(Nothing, ListedPrice?)))
            If price.HasValue Then
                parts.Add(price.Value.Currency & " " & price.Value.Amount.ToString("#,##0.##", CultureInfo.CurrentCulture) &
                          If(suggestion.IsOa = False, " (optional)", String.Empty))
            End If
            Return If(parts.Count = 0, "Not known", String.Join(" · ", parts))
        End Function

    End Class


    ' OpenAlex through the Online services gate, as the journal-suggestions
    ' service. Grouped searches and lists are cheap; the examples call is a
    ' search, which OpenAlex may ask anonymous users to wait for.
    Public Class OnlineJournalSuggestionsSource
        Implements IJournalSuggestionsSource

        Private ReadOnly _client As HttpClient


        Public Sub New()
            _client = OnlineAccess.ClientFor(OnlineServiceCatalog.JournalSuggestions)
        End Sub


        Public Async Function SearchAsync(request As JournalSuggestionRequest, cancellationToken As CancellationToken) As Task(Of JournalSuggestionsResult) Implements IJournalSuggestionsSource.SearchAsync

            OnlineAccess.Check(OnlineServiceCatalog.JournalSuggestions)
            If JournalSuggestionService.CleanKeywords(request).Count = 0 Then Throw New ArgumentException("Check at least one keyword.", NameOf(request))

            Dim result As New JournalSuggestionsResult With {.Request = request, .RetrievedUtc = DateTime.UtcNow}

            ' Only this call failing fails the search.
            Dim groups = JournalSuggestionService.ParseGroups(Await GetAsync(JournalSuggestionService.JournalsUrl(request), cancellationToken).ConfigureAwait(False))
            Dim top = groups.OrderByDescending(Function(item) item.Count).ThenBy(Function(item) item.Name, StringComparer.CurrentCultureIgnoreCase).
                Take(JournalSuggestionService.JournalsShown).ToList()
            If top.Count = 0 Then Return result

            result.TotalMatching = groups.Sum(Function(item) item.Count)
            For Each group In top
                result.Journals.Add(New JournalSuggestion With {.OpenAlexId = group.Id, .Name = group.Name, .MatchingArticles = group.Count})
            Next
            Dim ids As List(Of String) = top.Select(Function(item) item.Id).ToList()

            Try
                Dim totals = JournalSuggestionService.ParseGroups(Await GetAsync(JournalSuggestionService.TotalsUrl(request, ids), cancellationToken).ConfigureAwait(False))
                For Each journal As JournalSuggestion In result.Journals
                    Dim total = totals.FirstOrDefault(Function(item) item.Id = journal.OpenAlexId)
                    If total.Id IsNot Nothing Then journal.AllArticles = total.Count
                Next
            Catch ex As Exception When IsPartialFailure(ex, cancellationToken)
                result.TotalsError = OnlineAccess.Describe(ex, "OpenAlex")
            End Try

            Try
                Dim details As List(Of OpenAlexSource) = JournalSuggestionService.ParseSourceList(Await GetAsync(JournalSuggestionService.DetailsUrl(ids), cancellationToken).ConfigureAwait(False))
                For Each journal As JournalSuggestion In result.Journals
                    Dim source As OpenAlexSource = details.FirstOrDefault(Function(item) item.Id = journal.OpenAlexId)
                    If source Is Nothing Then Continue For
                    If source.DisplayName.Length > 0 Then journal.Name = source.DisplayName
                    journal.Publisher = source.Publisher
                    journal.Issns = source.Issns
                    journal.IsOa = source.IsOa
                    journal.IsInDoaj = source.IsInDoaj
                    journal.ApcPrices = source.ApcPrices
                Next
            Catch ex As Exception When IsPartialFailure(ex, cancellationToken)
                result.DetailsError = OnlineAccess.Describe(ex, "OpenAlex")
            End Try

            ' One examples call: every journal when the matches are few,
            ' else the ten with the most.
            Dim exampleIds As List(Of String) = If(top.Sum(Function(item) item.Count) <= 100, ids, ids.Take(10).ToList())
            Try
                Dim body As String = Await GetAsync(JournalSuggestionService.ExamplesUrl(request, exampleIds, 100), cancellationToken).ConfigureAwait(False)
                Dim examples = JournalSuggestionService.ParseExamples(body)
                ' The 100 newest can leave a journal out; it is then fetched on its own when shown.
                Dim matching As Long? = CitationsService.ParseMeta(body).Count
                Dim complete As Boolean = matching.HasValue AndAlso matching.Value <= 100
                For Each journal As JournalSuggestion In result.Journals.Where(Function(item) exampleIds.Contains(item.OpenAlexId))
                    Dim id As String = journal.OpenAlexId
                    journal.Examples = examples.Where(Function(item) item.SourceId = id).Select(Function(item) item.Example).Take(3).ToList()
                    journal.ExamplesLoaded = complete OrElse journal.Examples.Count >= 3
                Next
            Catch ex As Exception When IsPartialFailure(ex, cancellationToken)
                result.ExamplesError = OnlineAccess.Describe(ex, "OpenAlex")
                result.ExamplesBusy = TryCast(ex, OnlineServiceBusyException)
            End Try

            Return result

        End Function


        Public Async Function ExamplesAsync(request As JournalSuggestionRequest, openAlexId As String, cancellationToken As CancellationToken) As Task(Of List(Of EvidenceExample)) Implements IJournalSuggestionsSource.ExamplesAsync
            OnlineAccess.Check(OnlineServiceCatalog.JournalSuggestions)
            Dim id As String = OpenAlexSourceClient.NormalizeId(openAlexId)
            If id.Length = 0 Then Return New List(Of EvidenceExample)()
            Dim examples = JournalSuggestionService.ParseExamples(Await GetAsync(JournalSuggestionService.ExamplesUrl(request, {id}, 3), cancellationToken).ConfigureAwait(False))
            Return examples.Select(Function(item) item.Example).Take(3).ToList()
        End Function


        Private Async Function GetAsync(address As String, cancellationToken As CancellationToken) As Task(Of String)
            Using response As HttpResponseMessage = Await _client.GetAsync(address, cancellationToken).ConfigureAwait(False)
                Dim body As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                If CInt(response.StatusCode) = 429 Then Throw OnlineServiceBusyException.FromResponse("OpenAlex", response, body)
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException("OpenAlex answered with HTTP " & CInt(response.StatusCode).ToString(CultureInfo.InvariantCulture) & ".", Nothing, response.StatusCode)
                End If
                Return body
            End Using
        End Function


        ' Totals, details, or examples failing leaves the journals found; a
        ' blocked service or a Stop ends the search.
        Private Shared Function IsPartialFailure(ex As Exception, cancellationToken As CancellationToken) As Boolean
            If TypeOf ex Is OnlineServiceBlockedException Then Return False
            If TypeOf ex Is OperationCanceledException AndAlso cancellationToken.IsCancellationRequested Then Return False
            Return TypeOf ex Is HttpRequestException OrElse
                   TypeOf ex Is TaskCanceledException OrElse
                   TypeOf ex Is InvalidOperationException OrElse
                   TypeOf ex Is JsonException
        End Function

    End Class

End Namespace
