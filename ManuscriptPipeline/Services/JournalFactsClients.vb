Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Namespace Services

    ' An answer from DOAJ or OpenAlex that PaperRoute couldn't read (#87). Its
    ' message is the plain sentence shown to the researcher.
    Public Class JournalFactsFormatException
        Inherits InvalidOperationException

        Public Sub New(sourceName As String, Optional inner As Exception = Nothing)
            MyBase.New(sourceName & " sent an answer PaperRoute couldn't read. Try again later.", inner)
        End Sub

    End Class


    ' A price as the index lists it; never converted between currencies.
    Public Structure ListedPrice

        Public Sub New(amount As Decimal, currency As String)
            Me.Amount = amount
            Me.Currency = currency
        End Sub

        Public ReadOnly Property Amount As Decimal
        Public ReadOnly Property Currency As String

        Public Overrides Function ToString() As String
            Return Currency & " " & Amount.ToString("#,##0.##", CultureInfo.InvariantCulture)
        End Function

    End Structure


    ' One DOAJ journal record (v4 search API), as parsed from live responses:
    ' DOAJ's published schema is out of date.
    Public NotInheritable Class DoajJournal

        Public Property Id As String = String.Empty
        Public Property Title As String = String.Empty
        Public Property AlternativeTitle As String = String.Empty
        Public Property Publisher As String = String.Empty
        Public Property Pissn As String = String.Empty
        Public Property Eissn As String = String.Empty
        Public Property HomepageUrl As String = String.Empty
        Public Property AimsScopeUrl As String = String.Empty
        Public Property AuthorInstructionsUrl As String = String.Empty
        Public Property LicenseTermsUrl As String = String.Empty
        Public Property EditorialBoardUrl As String = String.Empty
        Public Property ReviewUrl As String = String.Empty
        Public Property ReviewProcess As New List(Of String)()
        Public Property HasApc As Boolean?
        Public Property ApcPrices As New List(Of ListedPrice)()
        Public Property ApcUrl As String = String.Empty
        Public Property HasOtherCharges As Boolean?
        Public Property OtherChargesUrl As String = String.Empty
        Public Property HasWaiver As Boolean?
        Public Property WaiverUrl As String = String.Empty
        Public Property PublicationTimeWeeks As Integer?
        Public Property PlagiarismDetection As Boolean?
        Public Property PlagiarismUrl As String = String.Empty
        Public Property AuthorRetainsCopyright As Boolean?
        Public Property CopyrightUrl As String = String.Empty
        Public Property Licenses As New List(Of String)()
        Public Property DepositPolicyServices As New List(Of String)()
        Public Property DepositPolicyUrl As String = String.Empty
        ' admin.last_full_review; DOAJ's last_updated is a site-wide reindex date.
        Public Property LastFullReview As DateTime?

        Public ReadOnly Property Issns As List(Of String)
            Get
                Return IssnService.NormalizeList({Eissn, Pissn})
            End Get
        End Property

    End Class


    ' Looks up one journal in DOAJ by ISSN, through the Online services gate.
    Public Class DoajClient

        Public Const SearchBase As String = "https://doaj.org/api/v4/search/journals/issn:"

        Private ReadOnly _client As HttpClient


        Public Sub New(Optional serviceId As String = OnlineServiceCatalog.JournalFacts)
            _client = OnlineAccess.ClientFor(serviceId)
        End Sub


        ' The journal, or Nothing when DOAJ doesn't list it. DOAJ lists fully
        ' open-access journals only.
        Public Async Function FindByIssnAsync(issn As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DoajJournal)

            Dim normalized As String = IssnService.Normalize(issn)
            If normalized.Length = 0 Then Throw New ArgumentException("A valid ISSN is required.", NameOf(issn))

            Using response As HttpResponseMessage = Await _client.GetAsync(SearchBase & normalized, cancellationToken).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException("DOAJ answered with HTTP " & CInt(response.StatusCode).ToString(CultureInfo.InvariantCulture) & ".", Nothing, response.StatusCode)
                End If
                Dim json As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                Return ParseSearch(json, normalized)
            End Using

        End Function


        ' The record whose eISSN or print ISSN is the one asked for, from a
        ' search envelope; Nothing when there is none.
        Friend Shared Function ParseSearch(json As String, issn As String) As DoajJournal

            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim results As JsonElement = Nothing
                    If document.RootElement.ValueKind <> JsonValueKind.Object OrElse
                       Not document.RootElement.TryGetProperty("results", results) OrElse
                       results.ValueKind <> JsonValueKind.Array Then
                        Throw New JournalFactsFormatException("DOAJ")
                    End If

                    Dim wanted As String = IssnService.Normalize(issn)
                    For Each result As JsonElement In results.EnumerateArray()
                        Dim journal As DoajJournal = ParseRecord(result)
                        If journal IsNot Nothing AndAlso journal.Issns.Contains(wanted) Then Return journal
                    Next
                    Return Nothing
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("DOAJ", ex)
            End Try

        End Function


        Friend Shared Function ParseRecord(record As JsonElement) As DoajJournal

            If record.ValueKind <> JsonValueKind.Object Then Return Nothing
            Dim bib As JsonElement = JsonFacts.Child(record, "bibjson")
            If bib.ValueKind <> JsonValueKind.Object Then Return Nothing

            Dim journal As New DoajJournal With {
                .Id = JsonFacts.Text(record, "id"),
                .Title = JsonFacts.Text(bib, "title"),
                .AlternativeTitle = JsonFacts.Text(bib, "alternative_title"),
                .Publisher = JsonFacts.Text(JsonFacts.Child(bib, "publisher"), "name"),
                .Pissn = JsonFacts.Text(bib, "pissn"),
                .Eissn = JsonFacts.Text(bib, "eissn"),
                .PublicationTimeWeeks = JsonFacts.Whole(bib, "publication_time_weeks"),
                .Licenses = JsonFacts.Items(bib, "license").Select(Function(item) JsonFacts.Text(item, "type")).Where(Function(item) item.Length > 0).Distinct().ToList(),
                .LastFullReview = JsonFacts.DateOnly(JsonFacts.Child(record, "admin"), "last_full_review")
            }

            Dim ref As JsonElement = JsonFacts.Child(bib, "ref")
            journal.HomepageUrl = JsonFacts.Text(ref, "journal")
            journal.AimsScopeUrl = JsonFacts.Text(ref, "aims_scope")
            journal.AuthorInstructionsUrl = JsonFacts.Text(ref, "author_instructions")
            journal.LicenseTermsUrl = JsonFacts.Text(ref, "license_terms")

            Dim editorial As JsonElement = JsonFacts.Child(bib, "editorial")
            journal.ReviewProcess = JsonFacts.Strings(editorial, "review_process")
            journal.ReviewUrl = JsonFacts.Text(editorial, "review_url")
            journal.EditorialBoardUrl = JsonFacts.Text(editorial, "board_url")

            Dim apc As JsonElement = JsonFacts.Child(bib, "apc")
            journal.HasApc = JsonFacts.Flag(apc, "has_apc")
            journal.ApcUrl = JsonFacts.Text(apc, "url")
            For Each price As JsonElement In JsonFacts.Items(apc, "max")
                Dim amount As Decimal? = JsonFacts.Amount(price, "price")
                Dim currency As String = JsonFacts.Text(price, "currency").ToUpperInvariant()
                If amount.HasValue AndAlso Regex.IsMatch(currency, "^[A-Z]{3}$") Then journal.ApcPrices.Add(New ListedPrice(amount.Value, currency))
            Next

            Dim other As JsonElement = JsonFacts.Child(bib, "other_charges")
            journal.HasOtherCharges = JsonFacts.Flag(other, "has_other_charges")
            journal.OtherChargesUrl = JsonFacts.Text(other, "url")

            Dim waiver As JsonElement = JsonFacts.Child(bib, "waiver")
            journal.HasWaiver = JsonFacts.Flag(waiver, "has_waiver")
            journal.WaiverUrl = JsonFacts.Text(waiver, "url")

            Dim plagiarism As JsonElement = JsonFacts.Child(bib, "plagiarism")
            journal.PlagiarismDetection = JsonFacts.Flag(plagiarism, "detection")
            journal.PlagiarismUrl = JsonFacts.Text(plagiarism, "url")

            Dim copyright As JsonElement = JsonFacts.Child(bib, "copyright")
            journal.AuthorRetainsCopyright = JsonFacts.Flag(copyright, "author_retains")
            journal.CopyrightUrl = JsonFacts.Text(copyright, "url")

            Dim deposit As JsonElement = JsonFacts.Child(bib, "deposit_policy")
            journal.DepositPolicyServices = JsonFacts.Strings(deposit, "service")
            journal.DepositPolicyUrl = JsonFacts.Text(deposit, "url")

            Return journal

        End Function

    End Class


    ' One OpenAlex source (a journal), from a single-entity lookup.
    Public NotInheritable Class OpenAlexSource

        ' The short id, such as "S202381698".
        Public Property Id As String = String.Empty
        Public Property DisplayName As String = String.Empty
        Public Property IssnL As String = String.Empty
        Public Property Issns As New List(Of String)()
        Public Property Publisher As String = String.Empty
        Public Property HomepageUrl As String = String.Empty
        Public Property Type As String = String.Empty
        Public Property IsOa As Boolean?
        Public Property IsInDoaj As Boolean?
        Public Property InDoajSinceYear As Integer?
        Public Property ApcPrices As New List(Of ListedPrice)()
        Public Property TwoYearMeanCitedness As Double?
        Public Property HIndex As Long?
        Public Property I10Index As Long?
        Public Property Topics As New List(Of String)()
        Public Property WorksCount As Long?
        Public Property UpdatedUtc As DateTime?

    End Class


    ' One result of a search by journal name, for the researcher to pick.
    Public NotInheritable Class OpenAlexSourceMatch

        Public Property Id As String = String.Empty
        Public Property DisplayName As String = String.Empty
        Public Property IssnL As String = String.Empty
        Public Property Issns As New List(Of String)()
        Public Property Publisher As String = String.Empty
        Public Property Type As String = String.Empty
        Public Property WorksCount As Long?

    End Class


    ' Looks up journals in OpenAlex, through the Online services gate. A
    ' lookup by ISSN or id is free; a search by name uses a little of the
    ' daily allowance.
    Public Class OpenAlexSourceClient

        Public Const SourcesBase As String = "https://api.openalex.org/sources/"
        Public Const SearchBase As String = "https://api.openalex.org/sources?search="

        ' Only the fields the Journals page uses.
        Public Const LookupFields As String =
            "id,issn_l,issn,display_name,host_organization_name,homepage_url,type,is_oa,is_in_doaj,is_in_doaj_since_year,apc_prices,summary_stats,topics,works_count,updated_date"

        Public Const SearchFields As String = "id,display_name,issn_l,issn,host_organization_name,works_count,type"

        Private ReadOnly _client As HttpClient


        Public Sub New(Optional serviceId As String = OnlineServiceCatalog.JournalFacts)
            _client = OnlineAccess.ClientFor(serviceId)
        End Sub


        ' The source, or Nothing when OpenAlex has no record for the ISSN.
        Public Function FindByIssnAsync(issn As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OpenAlexSource)
            Dim normalized As String = IssnService.Normalize(issn)
            If normalized.Length = 0 Then Throw New ArgumentException("A valid ISSN is required.", NameOf(issn))
            Return GetSourceAsync(SourcesBase & "issn:" & normalized & "?select=" & LookupFields, cancellationToken)
        End Function


        ' The source, or Nothing when the id is unknown (merged ids now answer
        ' 404, so look the journal up again by ISSN).
        Public Function GetAsync(openAlexId As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OpenAlexSource)
            Dim id As String = NormalizeId(openAlexId)
            If id.Length = 0 Then Throw New ArgumentException("A valid OpenAlex source id is required.", NameOf(openAlexId))
            Return GetSourceAsync(SourcesBase & id & "?select=" & LookupFields, cancellationToken)
        End Function


        Public Async Function SearchAsync(name As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of OpenAlexSourceMatch))

            Dim query As String = If(name, String.Empty).Trim()
            If query.Length = 0 Then Return New List(Of OpenAlexSourceMatch)()
            If query.Length > 200 Then query = query.Substring(0, 200)

            Using response As HttpResponseMessage = Await _client.GetAsync(SearchBase & Uri.EscapeDataString(query) & "&per_page=8&select=" & SearchFields, cancellationToken).ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException("OpenAlex answered with HTTP " & CInt(response.StatusCode).ToString(CultureInfo.InvariantCulture) & ".", Nothing, response.StatusCode)
                End If
                Return ParseSearch(Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
            End Using

        End Function


        Private Async Function GetSourceAsync(address As String, cancellationToken As CancellationToken) As Task(Of OpenAlexSource)
            Using response As HttpResponseMessage = Await _client.GetAsync(address, cancellationToken).ConfigureAwait(False)
                ' An unknown ISSN or id answers 404 with an HTML page.
                If response.StatusCode = HttpStatusCode.NotFound Then Return Nothing
                If Not response.IsSuccessStatusCode Then
                    Throw New HttpRequestException("OpenAlex answered with HTTP " & CInt(response.StatusCode).ToString(CultureInfo.InvariantCulture) & ".", Nothing, response.StatusCode)
                End If
                Return ParseSource(Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
            End Using
        End Function


        ' "S202381698" from "S202381698" or "https://openalex.org/S202381698";
        ' "" for anything else.
        Public Shared Function NormalizeId(value As String) As String
            Dim text As String = If(value, String.Empty).Trim()
            Dim slash As Integer = text.LastIndexOf("/"c)
            If slash >= 0 Then text = text.Substring(slash + 1)
            text = text.ToUpperInvariant()
            Return If(Regex.IsMatch(text, "^S\d{1,15}$"), text, String.Empty)
        End Function


        Friend Shared Function ParseSource(json As String) As OpenAlexSource

            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim root As JsonElement = document.RootElement
                    If root.ValueKind <> JsonValueKind.Object Then Throw New JournalFactsFormatException("OpenAlex")
                    Dim id As String = NormalizeId(JsonFacts.Text(root, "id"))
                    If id.Length = 0 Then Throw New JournalFactsFormatException("OpenAlex")

                    Dim source As New OpenAlexSource With {
                        .Id = id,
                        .DisplayName = JsonFacts.Text(root, "display_name"),
                        .IssnL = IssnService.Normalize(JsonFacts.Text(root, "issn_l")),
                        .Issns = IssnService.NormalizeList({JsonFacts.Text(root, "issn_l")}.Concat(JsonFacts.Strings(root, "issn"))),
                        .Publisher = JsonFacts.Text(root, "host_organization_name"),
                        .HomepageUrl = JsonFacts.Text(root, "homepage_url"),
                        .Type = JsonFacts.Text(root, "type"),
                        .IsOa = JsonFacts.Flag(root, "is_oa"),
                        .IsInDoaj = JsonFacts.Flag(root, "is_in_doaj"),
                        .InDoajSinceYear = JsonFacts.Whole(root, "is_in_doaj_since_year"),
                        .WorksCount = JsonFacts.WholeLong(root, "works_count"),
                        .UpdatedUtc = JsonFacts.Utc(root, "updated_date")
                    }

                    For Each price As JsonElement In JsonFacts.Items(root, "apc_prices")
                        Dim amount As Decimal? = JsonFacts.Amount(price, "price")
                        Dim currency As String = JsonFacts.Text(price, "currency").ToUpperInvariant()
                        If amount.HasValue AndAlso Regex.IsMatch(currency, "^[A-Z]{3}$") Then source.ApcPrices.Add(New ListedPrice(amount.Value, currency))
                    Next

                    ' "2yr_mean_citedness" is OpenAlex's own key.
                    Dim stats As JsonElement = JsonFacts.Child(root, "summary_stats")
                    source.TwoYearMeanCitedness = JsonFacts.Number(stats, "2yr_mean_citedness")
                    source.HIndex = JsonFacts.WholeLong(stats, "h_index")
                    source.I10Index = JsonFacts.WholeLong(stats, "i10_index")

                    source.Topics = JsonFacts.Items(root, "topics").
                        Select(Function(item) JsonFacts.Text(item, "display_name")).
                        Where(Function(item) item.Length > 0).
                        Take(3).
                        ToList()

                    Return source
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try

        End Function


        Friend Shared Function ParseSearch(json As String) As List(Of OpenAlexSourceMatch)

            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim results As JsonElement = Nothing
                    If document.RootElement.ValueKind <> JsonValueKind.Object OrElse
                       Not document.RootElement.TryGetProperty("results", results) OrElse
                       results.ValueKind <> JsonValueKind.Array Then
                        Throw New JournalFactsFormatException("OpenAlex")
                    End If

                    Dim matches As New List(Of OpenAlexSourceMatch)()
                    For Each result As JsonElement In results.EnumerateArray()
                        Dim id As String = NormalizeId(JsonFacts.Text(result, "id"))
                        If id.Length = 0 Then Continue For
                        matches.Add(New OpenAlexSourceMatch With {
                            .Id = id,
                            .DisplayName = JsonFacts.Text(result, "display_name"),
                            .IssnL = IssnService.Normalize(JsonFacts.Text(result, "issn_l")),
                            .Issns = IssnService.NormalizeList({JsonFacts.Text(result, "issn_l")}.Concat(JsonFacts.Strings(result, "issn"))),
                            .Publisher = JsonFacts.Text(result, "host_organization_name"),
                            .Type = JsonFacts.Text(result, "type"),
                            .WorksCount = JsonFacts.WholeLong(result, "works_count")
                        })
                    Next
                    Return matches
                End Using
            Catch ex As JsonException
                Throw New JournalFactsFormatException("OpenAlex", ex)
            End Try

        End Function

    End Class


    ' Tolerant readers for the indexes' JSON: a missing or mistyped value is
    ' blank, text has markup stripped, and dates are UTC.
    Friend NotInheritable Class JsonFacts

        Private Sub New()
        End Sub

        Public Shared Function Child(parent As JsonElement, name As String) As JsonElement
            Dim value As JsonElement = Nothing
            If parent.ValueKind = JsonValueKind.Object AndAlso parent.TryGetProperty(name, value) Then Return value
            Return Nothing
        End Function

        Public Shared Function Text(parent As JsonElement, name As String) As String
            Dim value As JsonElement = Child(parent, name)
            If value.ValueKind <> JsonValueKind.String Then Return String.Empty
            Return Clean(value.GetString())
        End Function

        Public Shared Function Strings(parent As JsonElement, name As String) As List(Of String)
            Return Items(parent, name).
                Where(Function(item) item.ValueKind = JsonValueKind.String).
                Select(Function(item) Clean(item.GetString())).
                Where(Function(item) item.Length > 0).
                ToList()
        End Function

        Public Shared Function Items(parent As JsonElement, name As String) As List(Of JsonElement)
            Dim value As JsonElement = Child(parent, name)
            If value.ValueKind <> JsonValueKind.Array Then Return New List(Of JsonElement)()
            Return value.EnumerateArray().ToList()
        End Function

        Public Shared Function Flag(parent As JsonElement, name As String) As Boolean?
            Dim value As JsonElement = Child(parent, name)
            If value.ValueKind = JsonValueKind.True Then Return True
            If value.ValueKind = JsonValueKind.False Then Return False
            Return Nothing
        End Function

        Public Shared Function Whole(parent As JsonElement, name As String) As Integer?
            Dim value As JsonElement = Child(parent, name)
            Dim result As Integer
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetInt32(result) Then Return result
            Return Nothing
        End Function

        Public Shared Function WholeLong(parent As JsonElement, name As String) As Long?
            Dim value As JsonElement = Child(parent, name)
            Dim result As Long
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetInt64(result) Then Return result
            Return Nothing
        End Function

        Public Shared Function Number(parent As JsonElement, name As String) As Double?
            Dim value As JsonElement = Child(parent, name)
            Dim result As Double
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetDouble(result) AndAlso Not Double.IsNaN(result) AndAlso Not Double.IsInfinity(result) Then Return result
            Return Nothing
        End Function

        Public Shared Function Amount(parent As JsonElement, name As String) As Decimal?
            Dim value As JsonElement = Child(parent, name)
            Dim result As Decimal
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetDecimal(result) AndAlso result >= 0 Then Return result
            Return Nothing
        End Function

        ' OpenAlex dates have no zone and are UTC.
        Public Shared Function Utc(parent As JsonElement, name As String) As DateTime?
            Dim text As String = RawText(parent, name)
            Dim result As DateTime
            If DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, result) Then
                Return DateTime.SpecifyKind(result, DateTimeKind.Utc)
            End If
            Return Nothing
        End Function

        ' A calendar date, such as DOAJ's "2026-07-24", kept as UTC midnight.
        Public Shared Function DateOnly(parent As JsonElement, name As String) As DateTime?
            Dim text As String = RawText(parent, name)
            Dim result As DateTime
            If DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, result) Then
                Return DateTime.SpecifyKind(result, DateTimeKind.Utc)
            End If
            Return Nothing
        End Function

        Private Shared Function RawText(parent As JsonElement, name As String) As String
            Dim value As JsonElement = Child(parent, name)
            Return If(value.ValueKind = JsonValueKind.String, If(value.GetString(), String.Empty).Trim(), String.Empty)
        End Function

        ' Markup stripped, entities decoded, whitespace collapsed.
        Public Shared Function Clean(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return String.Empty
            Dim withoutTags As String = Regex.Replace(value, "<[^>]+>", " ")
            Return WebUtility.HtmlDecode(Regex.Replace(withoutTags, "\s+", " ")).Trim()
        End Function

    End Class

End Namespace
