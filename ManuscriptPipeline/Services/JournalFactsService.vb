Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    ' What DOAJ and OpenAlex said about one journal, at one moment.
    Public NotInheritable Class JournalFactsLookup

        Public Property CheckedUtc As DateTime = DateTime.UtcNow
        Public Property Issns As New List(Of String)()
        Public Property Doaj As DoajJournal
        ' DOAJ answered, whether or not it lists the journal.
        Public Property DoajChecked As Boolean
        Public Property DoajError As String = String.Empty
        Public Property OpenAlex As OpenAlexSource
        Public Property OpenAlexChecked As Boolean
        Public Property OpenAlexError As String = String.Empty

    End Class


    ' Where journal facts come from; tests supply recorded answers.
    Public Interface IJournalFactsSource

        Function LookupAsync(issns As IEnumerable(Of String), openAlexId As String, cancellationToken As CancellationToken) As Task(Of JournalFactsLookup)

        Function SearchAsync(name As String, cancellationToken As CancellationToken) As Task(Of List(Of OpenAlexSourceMatch))

    End Interface


    ' DOAJ and OpenAlex, through the Online services gate (#86, #87). At most
    ' four requests per lookup, all free: OpenAlex first, because it lists
    ' every ISSN a journal has, then DOAJ by those ISSNs. A journal's ISSNs
    ' always decide which journal it is; an OpenAlex id is used only for a
    ' journal without one, so a corrected ISSN is never overruled.
    Public Class OnlineJournalFactsSource
        Implements IJournalFactsSource

        Private ReadOnly _doaj As DoajClient
        Private ReadOnly _openAlex As OpenAlexSourceClient


        Public Sub New()
            _doaj = New DoajClient()
            _openAlex = New OpenAlexSourceClient()
        End Sub


        Public Async Function LookupAsync(issns As IEnumerable(Of String), openAlexId As String, cancellationToken As CancellationToken) As Task(Of JournalFactsLookup) Implements IJournalFactsSource.LookupAsync

            OnlineAccess.Check(OnlineServiceCatalog.JournalFacts)

            Dim result As New JournalFactsLookup With {.CheckedUtc = DateTime.UtcNow}
            Dim known As List(Of String) = IssnService.NormalizeList(issns)
            Dim id As String = OpenAlexSourceClient.NormalizeId(openAlexId)

            Try
                If known.Count = 0 AndAlso id.Length > 0 Then
                    result.OpenAlex = Await _openAlex.GetAsync(id, cancellationToken).ConfigureAwait(False)
                End If
                For Each issn As String In known.Take(2)
                    result.OpenAlex = Await _openAlex.FindByIssnAsync(issn, cancellationToken).ConfigureAwait(False)
                    If result.OpenAlex IsNot Nothing Then Exit For
                Next
                result.OpenAlexChecked = id.Length > 0 OrElse known.Count > 0
            Catch ex As Exception When IsLookupFailure(ex, cancellationToken)
                result.OpenAlexError = OnlineAccess.Describe(ex, "OpenAlex")
            End Try

            result.Issns = IssnService.NormalizeList(known.Concat(If(result.OpenAlex?.Issns, New List(Of String)())))

            Try
                For Each issn As String In result.Issns.Take(2)
                    result.Doaj = Await _doaj.FindByIssnAsync(issn, cancellationToken).ConfigureAwait(False)
                    If result.Doaj IsNot Nothing Then Exit For
                Next
                result.DoajChecked = result.Issns.Count > 0
            Catch ex As Exception When IsLookupFailure(ex, cancellationToken)
                result.DoajError = OnlineAccess.Describe(ex, "DOAJ")
            End Try

            Return result

        End Function


        Public Function SearchAsync(name As String, cancellationToken As CancellationToken) As Task(Of List(Of OpenAlexSourceMatch)) Implements IJournalFactsSource.SearchAsync
            OnlineAccess.Check(OnlineServiceCatalog.JournalFacts)
            Return _openAlex.SearchAsync(name, cancellationToken)
        End Function


        ' One source failing leaves the other's facts; a blocked service or a
        ' Stop ends the whole lookup.
        Private Shared Function IsLookupFailure(ex As Exception, cancellationToken As CancellationToken) As Boolean
            If TypeOf ex Is OnlineServiceBlockedException Then Return False
            If TypeOf ex Is OperationCanceledException AndAlso cancellationToken.IsCancellationRequested Then Return False
            Return TypeOf ex Is HttpRequestException OrElse
                   TypeOf ex Is TaskCanceledException OrElse
                   TypeOf ex Is InvalidOperationException OrElse
                   TypeOf ex Is JsonException OrElse
                   TypeOf ex Is ArgumentException
        End Function

    End Class


    Public Enum JournalFactChangeKind
        ' A blank field is filled.
        Fill
        ' A looked-up value changed at its source.
        Update
        ' A new fact, or ISSNs the record lacks.
        Add
        ' The researcher's own value differs; shown, never replaced.
        Keep
        ' The source no longer gives it; unchecked unless the researcher agrees.
        Remove
    End Enum


    ' One row of the preview.
    Public NotInheritable Class JournalFactChange

        Public Property Field As String = String.Empty
        Public Property Found As String = String.Empty
        Public Property Current As String = String.Empty
        Public Property Source As String = String.Empty
        Public Property Kind As JournalFactChangeKind
        Public Property Selected As Boolean

        Friend Property Apply As Action(Of JournalRecord)

        Public ReadOnly Property CanApply As Boolean
            Get
                Return Kind <> JournalFactChangeKind.Keep
            End Get
        End Property

        Public ReadOnly Property WhatHappens As String
            Get
                Select Case Kind
                    Case JournalFactChangeKind.Fill : Return "Fill"
                    Case JournalFactChangeKind.Update : Return "Update"
                    Case JournalFactChangeKind.Add : Return "Add"
                    Case JournalFactChangeKind.Keep : Return "Yours is kept"
                    Case Else : Return "Remove: no longer in " & Source
                End Select
            End Get
        End Property

    End Class


    Public NotInheritable Class JournalFactsPlan

        Public Property Lookup As JournalFactsLookup
        Public Property Changes As New List(Of JournalFactChange)()
        Public Property Notes As New List(Of String)()
        ' Facts the sources confirmed unchanged.
        Public Property Unchanged As Integer

        ' Every fact the sources gave this time.
        Friend Property Found As New List(Of JournalFact)()

    End Class


    ' Journal facts from open indexes (#87): what a lookup would change, and
    ' applying it. A lookup fills only blank fields and never replaces what
    ' the researcher typed; facts carry their source and the date checked.
    Public NotInheritable Class JournalFactsService

        Private Sub New()
        End Sub

        ' FieldSources keys, stored in authors.json.
        Public Const PublisherField As String = "Publisher"
        Public Const HomepageField As String = "HomepageUrl"
        Public Const IssnsField As String = "Issns"
        Public Const AimsScopeField As String = "AimsScopeUrl"
        Public Const AuthorInstructionsField As String = "AuthorInstructionsUrl"
        Public Const EditorialBoardField As String = "EditorialBoardUrl"

        Private Shared ReadOnly Sources As String() = {JournalFactCatalog.DoajSource, JournalFactCatalog.OpenAlexSource}


        Public Shared Function Plan(record As JournalRecord, lookup As JournalFactsLookup) As JournalFactsPlan

            If record Is Nothing Then Throw New ArgumentNullException(NameOf(record))
            If lookup Is Nothing Then Throw New ArgumentNullException(NameOf(lookup))

            Dim result As New JournalFactsPlan With {.Lookup = lookup}
            Dim doaj As DoajJournal = lookup.Doaj
            Dim openAlex As OpenAlexSource = lookup.OpenAlex

            If lookup.OpenAlexError.Length > 0 Then result.Notes.Add(lookup.OpenAlexError & " OpenAlex's facts weren't checked.")
            If lookup.OpenAlexChecked AndAlso openAlex Is Nothing Then result.Notes.Add("OpenAlex has no record for this journal's ISSN.")
            If lookup.DoajError.Length > 0 Then result.Notes.Add(lookup.DoajError & " DOAJ's facts weren't checked.")
            If lookup.DoajChecked AndAlso doaj Is Nothing Then
                result.Notes.Add("This journal isn't listed in DOAJ. DOAJ lists fully open-access journals only, so this is expected for subscription and hybrid journals.")
            End If
            If Not lookup.DoajChecked AndAlso lookup.DoajError.Length = 0 Then result.Notes.Add("DOAJ wasn't checked, because the journal has no ISSN to look up.")

            ' Fields the researcher owns: filled only when blank.
            Dim publisher As String = If(doaj IsNot Nothing AndAlso doaj.Publisher.Length > 0, doaj.Publisher, If(openAlex?.Publisher, String.Empty))
            Dim publisherSource As String = If(doaj IsNot Nothing AndAlso doaj.Publisher.Length > 0, JournalFactCatalog.DoajSource, JournalFactCatalog.OpenAlexSource)
            PlanField(result, record, PublisherField, "Publisher", publisher, publisherSource, isUrl:=False)

            ' The homepage: DOAJ's when it lists one, else OpenAlex's. OpenAlex
            ' never replaces a homepage DOAJ gave, even when DOAJ wasn't
            ' reached this time or no longer lists the journal.
            If doaj IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(doaj.HomepageUrl) Then
                PlanField(result, record, HomepageField, "Homepage", doaj.HomepageUrl, JournalFactCatalog.DoajSource, isUrl:=True)
            ElseIf openAlex IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(openAlex.HomepageUrl) AndAlso
                   Not CameFrom(record, HomepageField, JournalFactCatalog.DoajSource) Then
                PlanField(result, record, HomepageField, "Homepage", openAlex.HomepageUrl, JournalFactCatalog.OpenAlexSource, isUrl:=True)
            End If

            If doaj IsNot Nothing Then
                PlanField(result, record, AimsScopeField, "Aims and scope", doaj.AimsScopeUrl, JournalFactCatalog.DoajSource, isUrl:=True)
                PlanField(result, record, AuthorInstructionsField, "Author instructions", doaj.AuthorInstructionsUrl, JournalFactCatalog.DoajSource, isUrl:=True)
                PlanField(result, record, EditorialBoardField, "Editorial board", doaj.EditorialBoardUrl, JournalFactCatalog.DoajSource, isUrl:=True)
            End If

            PlanIssns(result, record, doaj, openAlex)

            ' Facts from the sources: added, refreshed, or offered for removal.
            result.Found = FactsFrom(lookup)
            Dim answered As New HashSet(Of String)(StringComparer.Ordinal)
            If lookup.DoajChecked Then answered.Add(JournalFactCatalog.DoajSource)
            If lookup.OpenAlexChecked Then answered.Add(JournalFactCatalog.OpenAlexSource)

            For Each found As JournalFact In result.Found
                Dim fact As JournalFact = found
                Dim existing As JournalFact = SourceFact(record, fact.Key, fact.Source)
                Dim label As String = JournalFactCatalog.LabelOf(fact)
                If existing Is Nothing Then
                    result.Changes.Add(New JournalFactChange With {
                        .Field = label, .Found = DisplayValue(fact), .Source = fact.Source,
                        .Kind = JournalFactChangeKind.Add, .Selected = True,
                        .Apply = Sub(target) target.Facts.Add(CopyOf(fact))
                    })
                ElseIf Not String.Equals(existing.Value, fact.Value, StringComparison.Ordinal) OrElse
                       Not String.Equals(existing.Url, fact.Url, StringComparison.Ordinal) Then
                    result.Changes.Add(New JournalFactChange With {
                        .Field = label, .Found = DisplayValue(fact), .Current = DisplayValue(existing), .Source = fact.Source,
                        .Kind = JournalFactChangeKind.Update, .Selected = True,
                        .Apply = Sub(target)
                                     Dim stored As JournalFact = SourceFact(target, fact.Key, fact.Source)
                                     If stored Is Nothing Then
                                         target.Facts.Add(CopyOf(fact))
                                     Else
                                         stored.Value = fact.Value
                                         stored.Url = fact.Url
                                     End If
                                 End Sub
                    })
                Else
                    result.Unchanged += 1
                End If
            Next

            For Each stored As JournalFact In record.Facts.Where(Function(item) Not item.EnteredByYou AndAlso answered.Contains(item.Source)).ToList()
                Dim fact As JournalFact = stored
                If result.Found.Any(Function(item) item.Key = fact.Key AndAlso item.Source = fact.Source) Then Continue For
                result.Changes.Add(New JournalFactChange With {
                    .Field = JournalFactCatalog.LabelOf(fact), .Current = DisplayValue(fact), .Source = fact.Source,
                    .Kind = JournalFactChangeKind.Remove, .Selected = False,
                    .Apply = Sub(target) target.Facts.RemoveAll(Function(item) Not item.EnteredByYou AndAlso item.Key = fact.Key AndAlso item.Source = fact.Source)
                })
            Next

            Return result

        End Function


        ' Applies the selected rows, records the index ids, and marks every
        ' fact the sources gave again as checked now.
        Public Shared Sub Apply(record As JournalRecord, plan As JournalFactsPlan)

            If record Is Nothing Then Throw New ArgumentNullException(NameOf(record))
            If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))

            For Each change As JournalFactChange In plan.Changes.Where(Function(item) item.Selected AndAlso item.CanApply)
                change.Apply?.Invoke(record)
            Next

            For Each found As JournalFact In plan.Found
                Dim stored As JournalFact = SourceFact(record, found.Key, found.Source)
                If stored Is Nothing OrElse
                   Not String.Equals(stored.Value, found.Value, StringComparison.Ordinal) OrElse
                   Not String.Equals(stored.Url, found.Url, StringComparison.Ordinal) Then Continue For
                stored.CheckedUtc = found.CheckedUtc
                stored.SourceUpdatedUtc = found.SourceUpdatedUtc
            Next

            If plan.Lookup.Doaj IsNot Nothing AndAlso plan.Lookup.Doaj.Id.Length > 0 Then record.DoajId = plan.Lookup.Doaj.Id
            If plan.Lookup.OpenAlex IsNot Nothing Then record.OpenAlexId = plan.Lookup.OpenAlex.Id

        End Sub


        ' The facts a lookup found, as they would be stored.
        Friend Shared Function FactsFrom(lookup As JournalFactsLookup) As List(Of JournalFact)

            Dim facts As New List(Of JournalFact)()
            Dim doaj As DoajJournal = lookup.Doaj
            Dim openAlex As OpenAlexSource = lookup.OpenAlex

            Dim add As Action(Of String, String, String, String, DateTime?) =
                Sub(key, value, url, source, updated)
                    If String.IsNullOrWhiteSpace(value) Then Return
                    facts.Add(New JournalFact With {
                        .Key = key, .Value = value.Trim(), .Url = SafeUrl(url), .Source = source,
                        .SourceUpdatedUtc = updated, .CheckedUtc = lookup.CheckedUtc
                    })
                End Sub

            If doaj IsNot Nothing Then

                Dim reviewed As DateTime? = doaj.LastFullReview
                Dim d As String = JournalFactCatalog.DoajSource
                Dim issn As String = doaj.Issns.FirstOrDefault()

                add(JournalFactCatalog.DoajListing, "Listed in DOAJ", If(issn Is Nothing, String.Empty, "https://doaj.org/toc/" & issn), d, reviewed)

                If doaj.HasApc = True Then
                    ' DOAJ gives the highest fee, in each currency listed.
                    Dim prices As String = If(doaj.ApcPrices.Count = 0, "Charges a publication fee", "Up to " & String.Join(" · ", doaj.ApcPrices.Select(Function(item) item.ToString())))
                    add(JournalFactCatalog.Apc, prices &
                        If(doaj.HasOtherCharges = True, "; other charges apply", String.Empty) &
                        If(doaj.HasWaiver = True, " (waivers available)", String.Empty),
                        If(doaj.ApcUrl.Length > 0, doaj.ApcUrl, doaj.OtherChargesUrl), d, reviewed)
                ElseIf doaj.HasApc = False Then
                    If doaj.HasOtherCharges = True Then
                        add(JournalFactCatalog.Apc, "No publication fee; other charges apply", If(doaj.OtherChargesUrl.Length > 0, doaj.OtherChargesUrl, doaj.ApcUrl), d, reviewed)
                    Else
                        add(JournalFactCatalog.Apc, "No publication fee", doaj.ApcUrl, d, reviewed)
                    End If
                End If

                add(JournalFactCatalog.License, String.Join(", ", doaj.Licenses), doaj.LicenseTermsUrl, d, reviewed)

                If doaj.AuthorRetainsCopyright.HasValue Then
                    add(JournalFactCatalog.Copyright, If(doaj.AuthorRetainsCopyright.Value, "Authors keep copyright", "Authors don't keep copyright without restrictions"), doaj.CopyrightUrl, d, reviewed)
                End If

                add(JournalFactCatalog.Review, String.Join("; ", doaj.ReviewProcess), doaj.ReviewUrl, d, reviewed)

                If doaj.PublicationTimeWeeks.HasValue AndAlso doaj.PublicationTimeWeeks.Value > 0 Then
                    add(JournalFactCatalog.Weeks, "About " & doaj.PublicationTimeWeeks.Value.ToString(CultureInfo.InvariantCulture) & " weeks from submission to publication", String.Empty, d, reviewed)
                End If

                If doaj.PlagiarismDetection.HasValue Then
                    add(JournalFactCatalog.Plagiarism, If(doaj.PlagiarismDetection.Value, "Screens submissions for plagiarism", "No plagiarism screening stated"), doaj.PlagiarismUrl, d, reviewed)
                End If

                Dim services As String = String.Join(", ", doaj.DepositPolicyServices.Where(Function(item) Not item.Contains("://", StringComparison.Ordinal)))
                Dim policyRecord As Boolean = JournalFactCatalog.IsOpenPolicyFinderRecord(doaj.DepositPolicyUrl)
                If services.Length > 0 OrElse policyRecord Then
                    add(JournalFactCatalog.Sharing, If(services.Length > 0, "Recorded in " & services, "Recorded in Open Policy Finder"),
                        If(policyRecord, doaj.DepositPolicyUrl, String.Empty), d, reviewed)
                End If

            ElseIf lookup.DoajChecked Then

                add(JournalFactCatalog.DoajListing, "Not listed in DOAJ", String.Empty, JournalFactCatalog.DoajSource, Nothing)

            End If

            If openAlex IsNot Nothing Then

                Dim o As String = JournalFactCatalog.OpenAlexSource
                Dim updated As DateTime? = openAlex.UpdatedUtc

                If openAlex.IsOa.HasValue Then
                    add(JournalFactCatalog.OpenAccess, If(openAlex.IsOa.Value, "Fully open access", "Not fully open access (subscription or hybrid)"), String.Empty, o, updated)
                End If

                add(JournalFactCatalog.Topics, String.Join(" · ", openAlex.Topics), String.Empty, o, updated)

                ' DOAJ's fee wins; OpenAlex's is shown only for journals DOAJ
                ' doesn't list, and a subscription journal's is optional.
                If doaj Is Nothing AndAlso openAlex.ApcPrices.Count > 0 Then
                    add(JournalFactCatalog.Apc, String.Join(" · ", openAlex.ApcPrices.Select(Function(item) item.ToString())) &
                        If(openAlex.IsOa = False, " (optional, to make an article open access)", String.Empty), String.Empty, o, updated)
                End If

                If openAlex.TwoYearMeanCitedness.HasValue Then
                    add(JournalFactCatalog.MeanCitedness, Math.Round(openAlex.TwoYearMeanCitedness.Value, 2).ToString("0.00", CultureInfo.InvariantCulture), String.Empty, o, updated)
                End If
                If openAlex.HIndex.HasValue Then add(JournalFactCatalog.OpenAlexHIndex, openAlex.HIndex.Value.ToString(CultureInfo.InvariantCulture), String.Empty, o, updated)
                If openAlex.I10Index.HasValue Then add(JournalFactCatalog.OpenAlexI10Index, openAlex.I10Index.Value.ToString(CultureInfo.InvariantCulture), String.Empty, o, updated)

            End If

            Return facts

        End Function


        ' ---------------------------------------------------------------
        ' Reading a record
        ' ---------------------------------------------------------------

        ' A fact's value as shown: numbers from OpenAlex in the reader's format.
        Public Shared Function DisplayValue(fact As JournalFact) As String
            If fact Is Nothing Then Return String.Empty
            If Not fact.EnteredByYou AndAlso fact.Key = JournalFactCatalog.Apc Then
                Return Text.RegularExpressions.Regex.Replace(fact.Value, "\b([A-Z]{3}) (\d+(?:\.\d+)?)\b",
                    Function(match) match.Groups(1).Value & " " & Decimal.Parse(match.Groups(2).Value, CultureInfo.InvariantCulture).ToString("#,##0.##", CultureInfo.CurrentCulture))
            End If
            If Not fact.EnteredByYou AndAlso JournalFactCatalog.GroupOf(fact) = JournalFactGroup.OpenMetric Then
                Dim number As Double
                If Double.TryParse(fact.Value, NumberStyles.Float, CultureInfo.InvariantCulture, number) Then
                    Return If(fact.Key = JournalFactCatalog.MeanCitedness, number.ToString("N2", CultureInfo.CurrentCulture), number.ToString("N0", CultureInfo.CurrentCulture))
                End If
            End If
            Return fact.Value
        End Function


        ' The oldest check among the index facts the card shows, so a fact a
        ' later lookup didn't confirm is never presented as checked today.
        Public Shared Function OldestShownCheck(record As JournalRecord) As DateTime?
            Dim shown As New List(Of JournalFact)()
            For Each definition As JournalFactDefinition In JournalFactCatalog.Definitions.Where(Function(item) item.Group = JournalFactGroup.Publishing)
                Dim fact As JournalFact = BestFact(record, definition.Key)
                If fact IsNot Nothing Then shown.Add(fact)
            Next
            shown.AddRange(If(record?.Facts, New List(Of JournalFact)()).Where(Function(item) Not item.EnteredByYou AndAlso JournalFactCatalog.GroupOf(item) = JournalFactGroup.OpenMetric))
            Return shown.
                Where(Function(item) Sources.Contains(item.Source) AndAlso item.CheckedUtc.HasValue).
                Select(Function(item) item.CheckedUtc).
                DefaultIfEmpty(Nothing).
                Min()
        End Function


        ' The latest check of any fact from an index.
        Public Shared Function LastChecked(record As JournalRecord) As DateTime?
            Return If(record?.Facts, New List(Of JournalFact)()).
                Where(Function(item) Not item.EnteredByYou AndAlso Sources.Contains(item.Source) AndAlso item.CheckedUtc.HasValue).
                Select(Function(item) item.CheckedUtc).
                DefaultIfEmpty(Nothing).
                Max()
        End Function


        Public Shared Function IsStale(checkedUtc As DateTime?, nowUtc As DateTime) As Boolean
            Return checkedUtc.HasValue AndAlso (nowUtc - checkedUtc.Value).TotalDays > JournalFactCatalog.FreshDays
        End Function


        ' "Facts from DOAJ and OpenAlex (both CC0), checked Sep 29, 2026."
        Public Shared Function SourcesLine(record As JournalRecord) As String

            Dim facts As List(Of JournalFact) = If(record?.Facts, New List(Of JournalFact)()).Where(Function(item) Not item.EnteredByYou).ToList()
            Dim names As New List(Of String)()

            Dim doajReviewed As DateTime? = facts.Where(Function(item) item.Source = JournalFactCatalog.DoajSource AndAlso item.SourceUpdatedUtc.HasValue).Select(Function(item) item.SourceUpdatedUtc).DefaultIfEmpty(Nothing).Max()
            If facts.Any(Function(item) item.Source = JournalFactCatalog.DoajSource) Then
                names.Add("DOAJ" & If(doajReviewed.HasValue, " (last reviewed " & doajReviewed.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & ")", String.Empty))
            End If
            If facts.Any(Function(item) item.Source = JournalFactCatalog.OpenAlexSource) Then names.Add("OpenAlex")
            If facts.Any(Function(item) item.Source = JournalFactCatalog.ExampleSource) Then Return "Fictional facts for the example library."
            If names.Count = 0 Then Return String.Empty

            Dim checkedUtc As DateTime? = OldestShownCheck(record)
            Return "Facts from " & String.Join(" and ", names) & ", public domain (CC0)" &
                If(checkedUtc.HasValue, ", checked " & checkedUtc.Value.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture), String.Empty) & "."

        End Function


        ' One line for a shortlist row: "Open access · USD 2,477 · Double
        ' anonymous peer review · about 29 weeks to publication".
        Public Shared Function OneLine(record As JournalRecord) As String

            If record Is Nothing OrElse record.Facts Is Nothing OrElse record.Facts.Count = 0 Then Return String.Empty

            Dim parts As New List(Of String)()
            Dim listing As JournalFact = BestFact(record, JournalFactCatalog.DoajListing)
            Dim access As JournalFact = BestFact(record, JournalFactCatalog.OpenAccess)
            If listing IsNot Nothing AndAlso listing.Value = "Listed in DOAJ" Then
                parts.Add("Open access (DOAJ)")
            ElseIf access IsNot Nothing Then
                parts.Add(If(access.Value.StartsWith("Fully", StringComparison.Ordinal), "Open access", "Subscription or hybrid"))
            End If

            Dim fee As JournalFact = BestFact(record, JournalFactCatalog.Apc)
            If fee IsNot Nothing Then parts.Add(DisplayValue(fee))

            Dim review As JournalFact = BestFact(record, JournalFactCatalog.Review)
            If review IsNot Nothing Then parts.Add(review.Value)

            Dim weeks As JournalFact = BestFact(record, JournalFactCatalog.Weeks)
            If weeks IsNot Nothing Then parts.Add(weeks.Value.Replace("About ", "about ").Replace(" from submission to publication", " to publication"))

            Return String.Join(" · ", parts)

        End Function


        ' The researcher's stored fact for a key, preferring DOAJ, then
        ' OpenAlex, then any other source.
        Public Shared Function BestFact(record As JournalRecord, key As String) As JournalFact
            Dim candidates As List(Of JournalFact) = If(record?.Facts, New List(Of JournalFact)()).Where(Function(item) item IsNot Nothing AndAlso item.Key = key AndAlso Not item.EnteredByYou).ToList()
            ' Once DOAJ no longer lists the journal, its older facts give way.
            Dim delisted As Boolean = If(record?.Facts, New List(Of JournalFact)()).Any(Function(item) item IsNot Nothing AndAlso item.Key = JournalFactCatalog.DoajListing AndAlso item.Source = JournalFactCatalog.DoajSource AndAlso item.Value = "Not listed in DOAJ")
            If delisted AndAlso key <> JournalFactCatalog.DoajListing Then candidates.RemoveAll(Function(item) item.Source = JournalFactCatalog.DoajSource)
            Return If(candidates.FirstOrDefault(Function(item) item.Source = JournalFactCatalog.DoajSource),
                   If(candidates.FirstOrDefault(Function(item) item.Source = JournalFactCatalog.OpenAlexSource), candidates.FirstOrDefault()))
        End Function


        ' Stored facts or links beside a journal-choice question (#89), such as
        ' the peer review type beside "Is its peer review described?", one
        ' line each; none when nothing helps. evidence: what Find Journals
        ' kept for the journal (#96), used where the record has nothing, such
        ' as its main topics and homepage beside the scope question. The
        ' question is never answered for the researcher.
        Public Shared Function HintFor(checkId As String, record As JournalRecord, Optional evidence As CandidateEvidence = Nothing) As List(Of (Text As String, Url As String))

            Dim parts As New List(Of (Text As String, Url As String))()
            Dim add As Action(Of String, String) =
                Sub(text, url)
                    If Not String.IsNullOrWhiteSpace(text) Then parts.Add((text, If(url, String.Empty)))
                End Sub
            Dim link As Action(Of String, String) =
                Sub(text, url)
                    If UrlSafetyService.IsSafeHttpUrl(url) Then add(text, url)
                End Sub
            Dim fact As Func(Of String, String, String) =
                Function(key, prefix)
                    Dim found As JournalFact = BestFact(record, key)
                    Return If(found Is Nothing, String.Empty, prefix & DisplayValue(found) & " (" & found.Source & ")")
                End Function
            ' Where the evidence came from: "OpenAlex", or "Example".
            Dim evidenceSource As String = If(String.IsNullOrWhiteSpace(evidence?.Source), JournalFactCatalog.OpenAlexSource, evidence.Source.Trim())

            Select Case checkId
                Case "trust.review"
                    add(fact(JournalFactCatalog.Review, "Peer review: "), Nothing)
                Case "trust.fees", "fit.fees"
                    add(fact(JournalFactCatalog.Apc, "Fee: "), Nothing)
                Case "trust.indexed"
                    Dim listing As String = fact(JournalFactCatalog.DoajListing, String.Empty)
                    add(If(listing.Length > 0, listing, fact(JournalFactCatalog.OpenAccess, String.Empty)), Nothing)
                Case "trust.publisher"
                    If Not String.IsNullOrWhiteSpace(record?.Publisher) Then
                        add("Publisher: " & record.Publisher.Trim(), Nothing)
                    ElseIf Not String.IsNullOrWhiteSpace(evidence?.Publisher) Then
                        add("Publisher: " & evidence.Publisher.Trim() & " (" & evidenceSource & ")", Nothing)
                    End If
                Case "trust.guidelines"
                    If record IsNot Nothing Then link("Open author instructions", record.AuthorInstructionsUrl)
                Case "fit.scope"
                    Dim topics As String = fact(JournalFactCatalog.Topics, "Main topics: ")
                    Dim evidenceTopics As List(Of String) = If(evidence?.Topics, New List(Of String)()).Where(Function(item) Not String.IsNullOrWhiteSpace(item)).Select(Function(item) item.Trim()).ToList()
                    If topics.Length = 0 AndAlso evidenceTopics.Count > 0 Then topics = "Main topics: " & String.Join(" · ", evidenceTopics) & " (" & evidenceSource & ")"
                    add(topics, Nothing)
                    If record IsNot Nothing Then link("Open aims and scope", record.AimsScopeUrl)
                    If UrlSafetyService.IsSafeHttpUrl(record?.HomepageUrl) Then
                        Dim origin As String = FilledBy(record, HomepageField)
                        link("Open homepage" & If(origin.Length > 0, " (" & origin & ")", String.Empty), record.HomepageUrl)
                    ElseIf evidence IsNot Nothing Then
                        link("Open homepage (" & evidenceSource & ")", evidence.HomepageUrl)
                    End If
                Case "fit.sharing"
                    If record IsNot Nothing Then link("Open sharing policy (Open Policy Finder)", JournalFactCatalog.SharingPolicyUrl(record))
                Case "fit.timeline"
                    add(fact(JournalFactCatalog.Weeks, String.Empty), Nothing)
            End Select

            Return parts

        End Function


        ' The source that filled the field, while it still holds that value;
        ' "" for a value the researcher typed.
        Private Shared Function FilledBy(record As JournalRecord, key As String) As String
            Dim origin As FieldSource = Nothing
            If record?.FieldSources Is Nothing OrElse Not record.FieldSources.TryGetValue(key, origin) OrElse origin Is Nothing Then Return String.Empty
            Return If(String.Equals(origin.Value, FieldValue(record, key), StringComparison.Ordinal), If(origin.Source, String.Empty).Trim(), String.Empty)
        End Function


        ' A deep copy, so a lookup or an edit works on its own record until
        ' it is saved.
        Public Shared Function Clone(record As JournalRecord) As JournalRecord
            If record Is Nothing Then Return Nothing
            Dim copy As JournalRecord = JsonSerializer.Deserialize(Of JournalRecord)(JsonSerializer.Serialize(record))
            copy.FieldSources = New Dictionary(Of String, FieldSource)(If(copy.FieldSources, New Dictionary(Of String, FieldSource)()), StringComparer.Ordinal)
            Return copy
        End Function


        ' Lenient on load, save, and restore: a bad ISSN, link, or fact is
        ' dropped, never fatal, so one bad value can't make the library
        ' unreadable. A field no longer holding a looked-up value is the
        ' researcher's, so its FieldSources entry goes.
        Public Shared Sub Normalize(journal As JournalRecord)

            If journal Is Nothing Then Return

            journal.Issns = IssnService.NormalizeList(journal.Issns)
            journal.AimsScopeUrl = StoredUrl(journal.AimsScopeUrl)
            journal.AuthorInstructionsUrl = StoredUrl(journal.AuthorInstructionsUrl)
            journal.EditorialBoardUrl = StoredUrl(journal.EditorialBoardUrl)
            journal.OpenAlexId = OpenAlexSourceClient.NormalizeId(journal.OpenAlexId)
            Dim doajId As String = If(journal.DoajId, String.Empty).Trim().ToLowerInvariant()
            journal.DoajId = If(Text.RegularExpressions.Regex.IsMatch(doajId, "^[0-9a-f]{32}$"), doajId, String.Empty)

            journal.Facts = If(journal.Facts, New List(Of JournalFact)()).
                Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.Key) AndAlso Not String.IsNullOrWhiteSpace(item.Value)).
                ToList()
            For Each fact As JournalFact In journal.Facts
                fact.Key = fact.Key.Trim()
                fact.Value = Truncate(fact.Value.Trim(), 500)
                fact.Label = Truncate(If(fact.Label, String.Empty).Trim(), 120)
                fact.Source = Truncate(If(fact.Source, String.Empty).Trim(), 120)
                fact.Url = StoredUrl(fact.Url)
                If fact.Year.HasValue AndAlso (fact.Year.Value < 1900 OrElse fact.Year.Value > 2200) Then fact.Year = Nothing
            Next

            Dim kept As New Dictionary(Of String, FieldSource)(StringComparer.Ordinal)
            If journal.FieldSources IsNot Nothing Then
                For Each pair As KeyValuePair(Of String, FieldSource) In journal.FieldSources
                    If pair.Key Is Nothing OrElse pair.Value Is Nothing OrElse pair.Value.Value Is Nothing Then Continue For
                    If Not String.Equals(FieldValue(journal, pair.Key), pair.Value.Value, StringComparison.Ordinal) Then Continue For
                    pair.Value.Source = If(pair.Value.Source, String.Empty)
                    kept(pair.Key) = pair.Value
                Next
            End If
            journal.FieldSources = kept

        End Sub


        ' A stored link as typed when it is a web address, with spaces
        ' escaped; "" otherwise.
        Friend Shared Function StoredUrl(value As String) As String
            If String.IsNullOrWhiteSpace(value) OrElse Not UrlSafetyService.IsSafeHttpUrl(value) Then Return String.Empty
            Dim text As String = value.Trim()
            Return If(text.Any(AddressOf Char.IsWhiteSpace), New Uri(text).AbsoluteUri, text)
        End Function


        Private Shared Function Truncate(value As String, length As Integer) As String
            Return If(value.Length <= length, value, value.Substring(0, length))
        End Function


        ' ---------------------------------------------------------------
        ' Helpers
        ' ---------------------------------------------------------------

        Private Shared Sub PlanField(plan As JournalFactsPlan, record As JournalRecord, key As String, label As String, found As String, source As String, isUrl As Boolean)

            Dim value As String = If(found, String.Empty).Trim()
            If value.Length = 0 Then Return
            If isUrl Then
                value = SafeUrl(value)
                If value.Length = 0 Then
                    plan.Notes.Add(label & ": " & source & "'s link wasn't saved, because it isn't a web address.")
                    Return
                End If
            End If

            Dim current As String = FieldValue(record, key)
            If String.Equals(current, value, StringComparison.Ordinal) Then
                plan.Unchanged += 1
                Return
            End If

            Dim origin As FieldSource = Nothing
            If record.FieldSources IsNot Nothing Then record.FieldSources.TryGetValue(key, origin)
            Dim retrieved As DateTime = plan.Lookup.CheckedUtc
            Dim apply As Action(Of JournalRecord) =
                Sub(target)
                    SetFieldValue(target, key, value)
                    If target.FieldSources Is Nothing Then target.FieldSources = New Dictionary(Of String, FieldSource)(StringComparer.Ordinal)
                    target.FieldSources(key) = New FieldSource With {.Source = source, .Value = value, .RetrievedUtc = retrieved}
                End Sub

            If current.Length = 0 Then
                plan.Changes.Add(New JournalFactChange With {.Field = label, .Found = value, .Source = source, .Kind = JournalFactChangeKind.Fill, .Selected = True, .Apply = apply})
            ElseIf origin IsNot Nothing AndAlso String.Equals(origin.Value, current, StringComparison.Ordinal) Then
                plan.Changes.Add(New JournalFactChange With {.Field = label, .Found = value, .Current = current, .Source = source, .Kind = JournalFactChangeKind.Update, .Selected = True, .Apply = apply})
            Else
                plan.Changes.Add(New JournalFactChange With {.Field = label, .Found = value, .Current = current, .Source = source, .Kind = JournalFactChangeKind.Keep, .Selected = False})
            End If

        End Sub


        ' True when the field still holds the value the source filled it with.
        Private Shared Function CameFrom(record As JournalRecord, key As String, source As String) As Boolean
            Dim origin As FieldSource = Nothing
            Return record.FieldSources IsNot Nothing AndAlso
                   record.FieldSources.TryGetValue(key, origin) AndAlso
                   origin IsNot Nothing AndAlso
                   String.Equals(origin.Source, source, StringComparison.Ordinal) AndAlso
                   String.Equals(origin.Value, FieldValue(record, key), StringComparison.Ordinal)
        End Function


        Private Shared Sub PlanIssns(plan As JournalFactsPlan, record As JournalRecord, doaj As DoajJournal, openAlex As OpenAlexSource)

            Dim fromDoaj As List(Of String) = If(doaj?.Issns, New List(Of String)())
            Dim found As List(Of String) = IssnService.NormalizeList(fromDoaj.Concat(If(openAlex?.Issns, New List(Of String)())))
            If found.Count = 0 Then Return

            Dim source As String = If(fromDoaj.Count > 0, JournalFactCatalog.DoajSource, JournalFactCatalog.OpenAlexSource)
            Dim current As List(Of String) = IssnService.NormalizeList(record.Issns)
            Dim missing As List(Of String) = found.Where(Function(item) Not current.Contains(item)).ToList()
            If missing.Count = 0 Then
                plan.Unchanged += 1
                Return
            End If

            Dim retrieved As DateTime = plan.Lookup.CheckedUtc
            If current.Count = 0 Then
                plan.Changes.Add(New JournalFactChange With {
                    .Field = "ISSNs", .Found = String.Join(", ", found), .Source = source, .Kind = JournalFactChangeKind.Fill, .Selected = True,
                    .Apply = Sub(target)
                                 target.Issns = IssnService.NormalizeList(found)
                                 If target.FieldSources Is Nothing Then target.FieldSources = New Dictionary(Of String, FieldSource)(StringComparer.Ordinal)
                                 target.FieldSources(IssnsField) = New FieldSource With {.Source = source, .Value = String.Join(", ", target.Issns), .RetrievedUtc = retrieved}
                             End Sub
                })
            ElseIf current.Count < IssnService.MaximumPerJournal Then
                plan.Changes.Add(New JournalFactChange With {
                    .Field = "ISSNs", .Found = String.Join(", ", missing), .Current = String.Join(", ", current), .Source = source, .Kind = JournalFactChangeKind.Add, .Selected = True,
                    .Apply = Sub(target) target.Issns = IssnService.NormalizeList(If(target.Issns, New List(Of String)()).Concat(missing))
                })
            End If

        End Sub


        Friend Shared Function FieldValue(record As JournalRecord, key As String) As String
            Select Case key
                Case PublisherField : Return If(record.Publisher, String.Empty).Trim()
                Case HomepageField : Return If(record.HomepageUrl, String.Empty).Trim()
                Case AimsScopeField : Return If(record.AimsScopeUrl, String.Empty).Trim()
                Case AuthorInstructionsField : Return If(record.AuthorInstructionsUrl, String.Empty).Trim()
                Case EditorialBoardField : Return If(record.EditorialBoardUrl, String.Empty).Trim()
                Case IssnsField : Return String.Join(", ", If(record.Issns, New List(Of String)()))
            End Select
            Return String.Empty
        End Function


        Private Shared Sub SetFieldValue(record As JournalRecord, key As String, value As String)
            Select Case key
                Case PublisherField : record.Publisher = value
                Case HomepageField : record.HomepageUrl = value
                Case AimsScopeField : record.AimsScopeUrl = value
                Case AuthorInstructionsField : record.AuthorInstructionsUrl = value
                Case EditorialBoardField : record.EditorialBoardUrl = value
            End Select
        End Sub


        Private Shared Function SourceFact(record As JournalRecord, key As String, source As String) As JournalFact
            Return If(record.Facts, New List(Of JournalFact)()).FirstOrDefault(Function(item) item IsNot Nothing AndAlso Not item.EnteredByYou AndAlso item.Key = key AndAlso item.Source = source)
        End Function


        Private Shared Function CopyOf(fact As JournalFact) As JournalFact
            Return New JournalFact With {
                .Key = fact.Key, .Label = fact.Label, .Value = fact.Value, .Url = fact.Url, .Source = fact.Source, .Year = fact.Year,
                .SourceUpdatedUtc = fact.SourceUpdatedUtc, .CheckedUtc = fact.CheckedUtc, .EnteredByYou = fact.EnteredByYou
            }
        End Function


        ' An http or https address in canonical form, or "" (remote links can
        ' hold spaces or text that isn't an address).
        Friend Shared Function SafeUrl(value As String) As String
            If String.IsNullOrWhiteSpace(value) OrElse Not UrlSafetyService.IsSafeHttpUrl(value) Then Return String.Empty
            Return New Uri(value.Trim()).AbsoluteUri
        End Function

    End Class

End Namespace
