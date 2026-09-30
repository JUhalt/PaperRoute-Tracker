Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum CitationSection
        OrcidRecord
        PaperRouteManuscripts
        OpenAlexOnly
    End Enum


    ' A work offered for the researcher to confirm (#91).
    Public NotInheritable Class CitationCandidate

        Public Property Work As CitedWork
        Public Property Section As CitationSection
        ' Checked when offered: works on the ORCID record and published
        ' manuscripts are, works only OpenAlex links to the iD aren't.
        Public Property Selected As Boolean
        ' The researcher left it out last time.
        Public Property ExcludedBefore As Boolean

        Public ReadOnly Property Key As String
            Get
                Return CitationKeys.ForWork(Work)
            End Get
        End Property

    End Class


    ' What an update found, before the researcher confirms it.
    Public NotInheritable Class CitationLookup

        Public Property Orcid As String = String.Empty
        Public Property RetrievedUtc As DateTime = DateTime.UtcNow
        Public Property IncludeOpenAlexLinked As Boolean
        Public Property Candidates As New List(Of CitationCandidate)()
        ' Works on the ORCID record or in PaperRoute that OpenAlex doesn't have.
        Public Property NotFound As New List(Of String)()
        Public Property OrcidWithoutDoi As Integer
        ' How many works OpenAlex links to the iD, and how many were read.
        Public Property OpenAlexLinkedTotal As Long?
        Public Property OpenAlexLinkedRead As Integer

    End Class


    Public Interface ICitationSource

        ' manuscriptDois: (DOI, title) of the researcher's published manuscripts.
        Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                             excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup)

    End Interface


    ' Your citations (#91) through the Online services gate as the
    ' citations service: the ORCID iD to ORCID, the works' DOIs to OpenAlex in
    ' batches, and the iD to OpenAlex only when the researcher asks.
    Public Class OnlineCitationSource
        Implements ICitationSource

        Public Const WorksBase As String = "https://api.openalex.org/works"
        Public Const WorkFields As String = "id,doi,title,publication_year,primary_location,cited_by_count,counts_by_year,fwci,citation_normalized_percentile"
        Public Const MaximumBatch As Integer = 50
        Public Const MaximumUrlLength As Integer = 7500
        Public Const OpenAlexLinkedPages As Integer = 2

        Private ReadOnly _orcid As OrcidClient
        Private ReadOnly _client As HttpClient


        Public Sub New()
            _orcid = New OrcidClient(OnlineServiceCatalog.Citations)
            _client = OnlineAccess.ClientFor(OnlineServiceCatalog.Citations)
        End Sub


        Public Async Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                          excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync

            OnlineAccess.Check(OnlineServiceCatalog.Citations)
            Dim id As String = OrcidIdentifierService.NormalizeAndValidate(orcid)

            progress?.Report("Reading the works on your ORCID record...")
            Dim groups As List(Of OrcidWorkGroup) = Await _orcid.ReadWorkGroupsAsync(id, cancellationToken).ConfigureAwait(False)

            Dim manuscripts As List(Of (Doi As String, Title As String)) = If(manuscriptDois, Enumerable.Empty(Of (Doi As String, Title As String))()).
                Select(Function(item) (CitationKeys.Doi(item.Doi), If(item.Title, String.Empty))).
                Where(Function(item) item.Item1.Length > 0).
                ToList()
            Dim dois As List(Of String) = groups.SelectMany(Function(item) item.Dois).Concat(manuscripts.Select(Function(item) item.Doi)).Distinct().ToList()

            Dim found As New Dictionary(Of String, CitedWork)(StringComparer.Ordinal)
            Dim batches As List(Of List(Of String)) = CitationsService.DoiBatches(dois)
            For index As Integer = 0 To batches.Count - 1
                cancellationToken.ThrowIfCancellationRequested()
                progress?.Report("Looking up " & dois.Count.ToString("N0", CultureInfo.CurrentCulture) & " DOIs in OpenAlex (" &
                                 (index + 1).ToString(CultureInfo.CurrentCulture) & " of " & batches.Count.ToString(CultureInfo.CurrentCulture) & ")...")
                For Each work As CitedWork In CitationsService.ParseWorks(Await GetAsync(CitationsService.BatchUrl(batches(index)), cancellationToken).ConfigureAwait(False))
                    If work.Doi.Length > 0 AndAlso Not found.ContainsKey(work.Doi) Then found(work.Doi) = work
                Next
            Next

            ' A DOI a batch can't carry is looked up on its own (free).
            For Each doi As String In dois.Where(Function(item) Not CitationsService.CanBatch(item))
                cancellationToken.ThrowIfCancellationRequested()
                Dim body As String = Await GetAsync(WorksBase & "/doi:" & Uri.EscapeDataString(doi) & "?select=" & WorkFields, cancellationToken, allowNotFound:=True).ConfigureAwait(False)
                If body Is Nothing Then Continue For
                Dim work As CitedWork = CitationsService.ParseWorks(body).FirstOrDefault()
                If work IsNot Nothing Then found(doi) = work
            Next

            Dim linked As List(Of CitedWork) = Nothing
            Dim linkedTotal As Long? = Nothing
            If includeOpenAlexLinked Then
                linked = New List(Of CitedWork)()
                Dim cursor As String = "*"
                For page As Integer = 1 To OpenAlexLinkedPages
                    cancellationToken.ThrowIfCancellationRequested()
                    progress?.Report("Asking OpenAlex which works it links to your iD (page " & page.ToString(CultureInfo.CurrentCulture) & ")...")
                    Dim body As String = Await GetAsync(WorksBase & "?filter=authorships.author.orcid:" & id & "&per_page=100&select=" & WorkFields & "&cursor=" & Uri.EscapeDataString(cursor), cancellationToken).ConfigureAwait(False)
                    linked.AddRange(CitationsService.ParseWorks(body))
                    Dim meta = CitationsService.ParseMeta(body)
                    linkedTotal = meta.Count
                    If String.IsNullOrEmpty(meta.NextCursor) Then Exit For
                    cursor = meta.NextCursor
                Next
            End If

            Return CitationsService.Assemble(id, groups, manuscripts, found, linked, linkedTotal, excluded)

        End Function


        ' The body, or Nothing for an allowed 404.
        Private Async Function GetAsync(address As String, cancellationToken As CancellationToken, Optional allowNotFound As Boolean = False) As Task(Of String)
            Using response As HttpResponseMessage = Await _client.GetAsync(address, cancellationToken).ConfigureAwait(False)
                If allowNotFound AndAlso response.StatusCode = HttpStatusCode.NotFound Then Return Nothing
                Dim body As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                If CInt(response.StatusCode) = 429 Then Throw OnlineServiceBusyException.FromResponse("OpenAlex", response, body)
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException("OpenAlex answered with HTTP " & CInt(response.StatusCode).ToString(CultureInfo.InvariantCulture) & ".", Nothing, response.StatusCode)
                End If
                Return body
            End Using
        End Function

    End Class


    Public NotInheritable Class CitationsService

        Private Sub New()
        End Sub


        ' A DOI with a filter separator in it can't go in a batch.
        Public Shared Function CanBatch(doi As String) As Boolean
            Return doi.IndexOfAny({","c, "|"c, "+"c}) < 0
        End Function


        ' At most 50 DOIs and 7,500 bytes of address per batch (OpenAlex
        ' allows 100 values and about 8 KB).
        Public Shared Function DoiBatches(dois As IEnumerable(Of String)) As List(Of List(Of String))
            Dim batches As New List(Of List(Of String))()
            Dim current As New List(Of String)()
            For Each doi As String In If(dois, Enumerable.Empty(Of String)()).Where(AddressOf CanBatch)
                Dim candidate As New List(Of String)(current) From {doi}
                If current.Count > 0 AndAlso (candidate.Count > OnlineCitationSource.MaximumBatch OrElse BatchUrl(candidate).Length > OnlineCitationSource.MaximumUrlLength) Then
                    batches.Add(current)
                    current = New List(Of String) From {doi}
                Else
                    current = candidate
                End If
            Next
            If current.Count > 0 Then batches.Add(current)
            Return batches
        End Function


        Public Shared Function BatchUrl(dois As IEnumerable(Of String)) As String
            Return OnlineCitationSource.WorksBase & "?filter=doi:" & String.Join("|", dois.Select(AddressOf Uri.EscapeDataString)) &
                "&per_page=100&select=" & OnlineCitationSource.WorkFields
        End Function


        ' Works from a list answer, or one work from a single lookup.
        Friend Shared Function ParseWorks(json As String) As List(Of CitedWork)
            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim root As JsonElement = document.RootElement
                    Dim items As IEnumerable(Of JsonElement) =
                        If(JsonFacts.Child(root, "results").ValueKind = JsonValueKind.Array,
                           JsonFacts.Items(root, "results"),
                           New List(Of JsonElement) From {root})
                    Return items.Select(AddressOf ParseWork).Where(Function(item) item IsNot Nothing).ToList()
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try
        End Function


        Private Shared Function ParseWork(work As JsonElement) As CitedWork
            If work.ValueKind <> JsonValueKind.Object Then Return Nothing
            Dim id As String = JsonFacts.RawText(work, "id")
            Dim slash As Integer = id.LastIndexOf("/"c)
            If slash >= 0 Then id = id.Substring(slash + 1)
            If Not Text.RegularExpressions.Regex.IsMatch(id, "^W\d{1,15}$") Then Return Nothing
            Dim percentile As JsonElement = JsonFacts.Child(work, "citation_normalized_percentile")
            Return New CitedWork With {
                .OpenAlexId = id,
                .Doi = CitationKeys.Doi(JsonFacts.RawText(work, "doi")),
                .Title = JsonFacts.Text(work, "title"),
                .Year = JsonFacts.Whole(work, "publication_year"),
                .Journal = JsonFacts.Text(JsonFacts.Child(JsonFacts.Child(work, "primary_location"), "source"), "display_name"),
                .CitedByCount = CInt(Math.Min(Integer.MaxValue, Math.Max(0, If(JsonFacts.WholeLong(work, "cited_by_count"), 0L)))),
                .CountsByYear = JsonFacts.Items(work, "counts_by_year").
                    Select(Function(item) New YearCount With {.Year = If(JsonFacts.Whole(item, "year"), 0), .Count = If(JsonFacts.Whole(item, "cited_by_count"), 0)}).
                    Where(Function(item) item.Year > 0).
                    ToList(),
                .Fwci = JsonFacts.Number(work, "fwci"),
                .Percentile = JsonFacts.Number(percentile, "value"),
                .InTop1Percent = JsonFacts.Flag(percentile, "is_in_top_1_percent").GetValueOrDefault(),
                .InTop10Percent = JsonFacts.Flag(percentile, "is_in_top_10_percent").GetValueOrDefault()
            }
        End Function


        Friend Shared Function ParseMeta(json As String) As (Count As Long?, NextCursor As String)
            Using document As JsonDocument = JsonDocument.Parse(json)
                Dim meta As JsonElement = JsonFacts.Child(document.RootElement, "meta")
                Dim cursor As JsonElement = JsonFacts.Child(meta, "next_cursor")
                Return (JsonFacts.WholeLong(meta, "count"), If(cursor.ValueKind = JsonValueKind.String, cursor.GetString(), Nothing))
            End Using
        End Function


        ' One work per ORCID group (its versions' most-cited record), then the
        ' published manuscripts not already there, then, if asked, works only
        ' OpenAlex links to the iD. A work appears once.
        Public Shared Function Assemble(orcid As String, groups As IEnumerable(Of OrcidWorkGroup), manuscripts As IEnumerable(Of (Doi As String, Title As String)),
                                        found As IDictionary(Of String, CitedWork), linked As IEnumerable(Of CitedWork), linkedTotal As Long?,
                                        excluded As IEnumerable(Of String)) As CitationLookup

            Dim lookup As New CitationLookup With {.Orcid = orcid, .IncludeOpenAlexLinked = linked IsNot Nothing, .RetrievedUtc = DateTime.UtcNow}
            Dim left As New HashSet(Of String)(If(excluded, Enumerable.Empty(Of String)()), StringComparer.OrdinalIgnoreCase)
            Dim seenIds As New HashSet(Of String)(StringComparer.Ordinal)
            Dim seenDois As New HashSet(Of String)(StringComparer.Ordinal)

            Dim offer As Action(Of CitedWork, CitationSection, String) =
                Sub(work, section, foundBy)
                    If seenIds.Contains(work.OpenAlexId) OrElse (work.Doi.Length > 0 AndAlso seenDois.Contains(work.Doi)) Then Return
                    seenIds.Add(work.OpenAlexId)
                    If work.Doi.Length > 0 Then seenDois.Add(work.Doi)
                    work.FoundBy = foundBy
                    Dim wasLeft As Boolean = left.Contains(CitationKeys.ForWork(work))
                    lookup.Candidates.Add(New CitationCandidate With {
                        .Work = work, .Section = section, .ExcludedBefore = wasLeft,
                        .Selected = section <> CitationSection.OpenAlexOnly AndAlso Not wasLeft
                    })
                End Sub

            For Each group As OrcidWorkGroup In If(groups, Enumerable.Empty(Of OrcidWorkGroup)())
                If group.Dois.Count = 0 Then
                    lookup.OrcidWithoutDoi += 1
                    Continue For
                End If
                Dim versions As List(Of CitedWork) = group.Dois.Where(Function(doi) found.ContainsKey(doi)).Select(Function(doi) found(doi)).ToList()
                If versions.Count = 0 Then
                    lookup.NotFound.Add(If(group.Title.Length > 0, group.Title, group.Dois(0)))
                    Continue For
                End If
                offer(versions.OrderByDescending(Function(item) item.CitedByCount).ThenBy(Function(item) group.Dois.IndexOf(item.Doi)).First(), CitationSection.OrcidRecord, "orcid")
            Next

            For Each manuscript In If(manuscripts, Enumerable.Empty(Of (Doi As String, Title As String))())
                Dim work As CitedWork = Nothing
                If found.TryGetValue(manuscript.Doi, work) Then
                    offer(work, CitationSection.PaperRouteManuscripts, "paperroute")
                ElseIf Not seenDois.Contains(manuscript.Doi) Then
                    lookup.NotFound.Add(If(manuscript.Title.Length > 0, manuscript.Title, manuscript.Doi))
                End If
            Next

            If linked IsNot Nothing Then
                For Each work As CitedWork In linked
                    offer(work, CitationSection.OpenAlexOnly, "openalex")
                    lookup.OpenAlexLinkedRead += 1
                Next
                lookup.OpenAlexLinkedTotal = linkedTotal
            End If

            Return lookup

        End Function


        ' The snapshot saved from what the researcher confirmed.
        Public Shared Function BuildSnapshot(lookup As CitationLookup, previousExcluded As IEnumerable(Of String)) As CitationSnapshot
            Dim offered As New HashSet(Of String)(lookup.Candidates.Select(Function(item) item.Key), StringComparer.OrdinalIgnoreCase)
            ' Choices about works not offered this time are kept.
            Dim excluded As List(Of String) = If(previousExcluded, Enumerable.Empty(Of String)()).
                Where(Function(item) Not offered.Contains(item)).
                Concat(lookup.Candidates.Where(Function(item) Not item.Selected AndAlso item.Section <> CitationSection.OpenAlexOnly).Select(Function(item) item.Key)).
                Concat(lookup.Candidates.Where(Function(item) Not item.Selected AndAlso item.Section = CitationSection.OpenAlexOnly AndAlso item.ExcludedBefore).Select(Function(item) item.Key)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
            Return New CitationSnapshot With {
                .Orcid = lookup.Orcid,
                .Source = JournalFactCatalog.OpenAlexSource,
                .RetrievedUtc = lookup.RetrievedUtc,
                .IncludeOpenAlexLinked = lookup.IncludeOpenAlexLinked,
                .Excluded = excluded,
                .Works = lookup.Candidates.Where(Function(item) item.Selected).Select(Function(item) item.Work).ToList()
            }
        End Function


        ' Published manuscripts with a DOI: (DOI, title), never a preprint DOI.
        Public Shared Function PublishedDois(manuscripts As IEnumerable(Of Manuscript)) As List(Of (Doi As String, Title As String))
            Return If(manuscripts, Enumerable.Empty(Of Manuscript)()).
                Where(Function(item) item IsNot Nothing AndAlso (item.CurrentStage = PaperStage.Published OrElse item.Location = ManuscriptLocation.Published)).
                Select(Function(item) (CitationKeys.Doi(If(item.Metadata?.Doi, String.Empty)), If(item.Title, String.Empty))).
                Where(Function(item) item.Item1.Length > 0).
                GroupBy(Function(item) item.Item1).
                Select(Function(group) group.First()).
                ToList()
        End Function


        ' "89th", with "top 10%" or "top 1%" when OpenAlex flags it.
        Public Shared Function PercentileText(work As CitedWork) As String
            If work Is Nothing OrElse Not work.Percentile.HasValue Then Return "Not available"
            Dim value As Integer = CInt(Math.Floor(work.Percentile.Value * 100))
            Dim suffix As String = If(value Mod 100 >= 11 AndAlso value Mod 100 <= 13, "th", If(value Mod 10 = 1, "st", If(value Mod 10 = 2, "nd", If(value Mod 10 = 3, "rd", "th"))))
            Return value.ToString(CultureInfo.CurrentCulture) & suffix & If(work.InTop1Percent, " (top 1%)", If(work.InTop10Percent, " (top 10%)", String.Empty))
        End Function


        ' "1.47", "Not available", or "0.00 (provisional)" for recent work.
        Public Shared Function FwciText(work As CitedWork, retrievalYear As Integer) As String
            If work Is Nothing OrElse Not work.Fwci.HasValue Then Return "Not available"
            Dim provisional As Boolean = work.Year.HasValue AndAlso retrievalYear - work.Year.Value < 4
            Return work.Fwci.Value.ToString("N2", CultureInfo.CurrentCulture) & If(provisional, " (provisional)", String.Empty)
        End Function

    End Class

End Namespace
