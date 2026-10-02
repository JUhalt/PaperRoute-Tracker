Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' What the shortlist suggests after a submission ends in rejection or
    ' withdrawal: an offer, never an automatic change.
    Public NotInheritable Class RerouteOffer
        Public Property ClosedSubmission As JournalSubmission
        Public Property Outcome As SubmissionOutcome
        Public Property ClosedDate As DateTime?
        Public Property NextCandidate As JournalCandidate
    End Class


    ' Per-manuscript journal shortlists (#65). The list order is the
    ' researcher's ranking; whether a journal was submitted to is read from
    ' the manuscript's submissions.
    Public NotInheritable Class JournalShortlistService

        Private Sub New()
        End Sub


        Public Shared Function StatusName(status As CandidateStatus) As String
            Select Case status
                Case CandidateStatus.Preferred : Return "Preferred"
                Case CandidateStatus.Backup : Return "Backup"
                Case CandidateStatus.RuledOut : Return "Ruled out"
                Case Else : Return "Considering"
            End Select
        End Function


        ' Adds a journal once, linked to its Journal Library record when the
        ' name matches one. Returns the existing candidate for a repeat.
        Public Shared Function Add(manuscript As Manuscript, journalName As String, library As IEnumerable(Of JournalRecord), Optional status As CandidateStatus = CandidateStatus.Considering) As JournalCandidate

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            Dim name As String = If(journalName, String.Empty).Trim()
            If name.Length = 0 Then Throw New ArgumentException("A journal name is required.", NameOf(journalName))
            If manuscript.JournalShortlist Is Nothing Then manuscript.JournalShortlist = New List(Of JournalCandidate)()

            Dim key As String = RouteAnalyticsService.NameKey(name)
            Dim existing As JournalCandidate = manuscript.JournalShortlist.FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.JournalName) = key)
            If existing IsNot Nothing Then Return existing

            Dim record As JournalRecord = If(library, Enumerable.Empty(Of JournalRecord)()).FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
            Dim candidate As New JournalCandidate With {
                .JournalName = If(record IsNot Nothing, record.Name, name),
                .JournalId = If(record IsNot Nothing, CType(record.Id, Guid?), Nothing),
                .Status = status,
                .AddedDate = DateTime.Today
            }
            manuscript.JournalShortlist.Add(candidate)
            Return candidate

        End Function


        Public Shared Sub Move(manuscript As Manuscript, candidate As JournalCandidate, offset As Integer)
            Dim list As List(Of JournalCandidate) = manuscript?.JournalShortlist
            If list Is Nothing OrElse candidate Is Nothing Then Return
            Dim index As Integer = list.IndexOf(candidate)
            Dim target As Integer = index + offset
            If index < 0 OrElse target < 0 OrElse target >= list.Count Then Return
            list.RemoveAt(index)
            list.Insert(target, candidate)
        End Sub


        ' The most recent submission to this journal, if any.
        Public Shared Function SubmissionTo(manuscript As Manuscript, candidate As JournalCandidate) As JournalSubmission
            If manuscript?.Submissions Is Nothing OrElse candidate Is Nothing Then Return Nothing
            Dim key As String = RouteAnalyticsService.NameKey(candidate.JournalName)
            Return manuscript.Submissions.
                Where(Function(item) item IsNot Nothing AndAlso
                                     ((candidate.JournalId.HasValue AndAlso item.JournalId.HasValue AndAlso item.JournalId.Value = candidate.JournalId.Value) OrElse
                                      RouteAnalyticsService.NameKey(item.JournalName) = key)).
                OrderBy(Function(item) item.SubmittedDate).
                LastOrDefault()
        End Function


        ' "Submitted Mar 1, 2026 · Desk rejected", or empty if never submitted.
        Public Shared Function SubmissionText(manuscript As Manuscript, candidate As JournalCandidate, today As DateTime) As String
            Dim submission As JournalSubmission = SubmissionTo(manuscript, candidate)
            If submission Is Nothing Then Return String.Empty
            Dim statistics As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(submission, today)
            Dim outcome As String
            Select Case statistics.Outcome
                Case SubmissionOutcome.Accepted : outcome = "Accepted"
                Case SubmissionOutcome.DeskRejected : outcome = "Desk rejected"
                Case SubmissionOutcome.RejectedAfterReview : outcome = "Rejected after review"
                Case SubmissionOutcome.Rejected : outcome = "Rejected"
                Case SubmissionOutcome.Withdrawn : outcome = "Withdrawn"
                Case Else : outcome = If(statistics.RevisionRounds > 0, "In revision", "Under consideration")
            End Select
            Return "Submitted " & submission.SubmittedDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & "  ·  " & outcome
        End Function


        ' The next journal to try: Preferred first, then Considering, then
        ' Backup, each in list order, leaving out ruled-out journals and
        ' journals already submitted to.
        Public Shared Function NextCandidate(manuscript As Manuscript) As JournalCandidate
            If manuscript?.JournalShortlist Is Nothing Then Return Nothing
            For Each status As CandidateStatus In {CandidateStatus.Preferred, CandidateStatus.Considering, CandidateStatus.Backup}
                Dim found As JournalCandidate = manuscript.JournalShortlist.FirstOrDefault(
                    Function(item) item IsNot Nothing AndAlso item.Status = status AndAlso SubmissionTo(manuscript, item) Is Nothing)
                If found IsNot Nothing Then Return found
            Next
            Return Nothing
        End Function


        ' When the latest submission of a manuscript still in the pipeline
        ' ended in rejection or withdrawal, the next shortlisted journal, unless
        ' it is already the target journal.
        Public Shared Function RerouteOfferFor(manuscript As Manuscript, today As DateTime) As RerouteOffer

            If manuscript Is Nothing OrElse manuscript.Location <> ManuscriptLocation.Pipeline OrElse manuscript.Submissions Is Nothing Then Return Nothing
            Dim latest As JournalSubmission = manuscript.Submissions.Where(Function(item) item IsNot Nothing).OrderBy(Function(item) item.SubmittedDate).LastOrDefault()
            If latest Is Nothing Then Return Nothing

            Dim statistics As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(latest, today)
            If statistics.Outcome = SubmissionOutcome.Open OrElse statistics.Outcome = SubmissionOutcome.Accepted Then Return Nothing

            Dim candidate As JournalCandidate = NextCandidate(manuscript)
            If candidate Is Nothing Then Return Nothing
            If RouteAnalyticsService.NameKey(candidate.JournalName) = RouteAnalyticsService.NameKey(manuscript.TargetJournal) Then Return Nothing

            Return New RerouteOffer With {
                .ClosedSubmission = latest,
                .Outcome = statistics.Outcome,
                .ClosedDate = statistics.ClosedDate,
                .NextCandidate = candidate
            }

        End Function


        ' Makes a shortlisted journal the target journal, linked to its
        ' Journal Library record when it has one.
        Public Shared Sub MakeTarget(manuscript As Manuscript, candidate As JournalCandidate)
            If manuscript Is Nothing OrElse candidate Is Nothing Then Return
            manuscript.TargetJournal = candidate.JournalName
            manuscript.TargetJournalId = candidate.JournalId
        End Sub



        ' Adds a journal found by Find Journals (#88) once, matched to the
        ' Journal Library by ISSN, then name, and to the shortlist by library
        ' record, name, ISSN, or OpenAlex id. An existing candidate is returned
        ' untouched: its status, notes, checks, and evidence stay as they are.
        Public Shared Function AddFound(manuscript As Manuscript, journalName As String, issns As IEnumerable(Of String), openAlexId As String,
                                        library As IEnumerable(Of JournalRecord)) As (Candidate As JournalCandidate, Created As Boolean)

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            Dim name As String = If(journalName, String.Empty).Trim()
            If name.Length = 0 Then Throw New ArgumentException("A journal name is required.", NameOf(journalName))
            If manuscript.JournalShortlist Is Nothing Then manuscript.JournalShortlist = New List(Of JournalCandidate)()

            Dim record As JournalRecord = LibraryRecordFor(name, issns, library)
            Dim existing As JournalCandidate = FindFound(manuscript, name, issns, openAlexId, library)
            If existing IsNot Nothing Then Return (existing, False)

            Dim candidate As New JournalCandidate With {
                .JournalName = If(record IsNot Nothing, record.Name, name),
                .JournalId = If(record IsNot Nothing, CType(record.Id, Guid?), Nothing),
                .Status = CandidateStatus.Considering,
                .AddedDate = DateTime.Today
            }
            manuscript.JournalShortlist.Add(candidate)
            Return (candidate, True)

        End Function


        ' The candidate a found journal would join, as AddFound decides it: by
        ' its library record, its own name, ISSN, or OpenAlex id.
        Public Shared Function FindFound(manuscript As Manuscript, journalName As String, issns As IEnumerable(Of String), openAlexId As String,
                                         library As IEnumerable(Of JournalRecord)) As JournalCandidate
            Dim name As String = If(journalName, String.Empty).Trim()
            Dim record As JournalRecord = LibraryRecordFor(name, issns, library)
            Dim existing As JournalCandidate = FindCandidate(manuscript, If(record?.Name, name), issns, openAlexId, record?.Id)
            If existing Is Nothing AndAlso record IsNot Nothing Then existing = FindCandidate(manuscript, name, issns, openAlexId, Nothing)
            Return existing
        End Function


        ' The Journal Library record for a found journal: by ISSN, then name.
        Public Shared Function LibraryRecordFor(journalName As String, issns As IEnumerable(Of String), library As IEnumerable(Of JournalRecord)) As JournalRecord
            Dim found As List(Of String) = IssnService.NormalizeList(issns)
            Dim records As List(Of JournalRecord) = If(library, Enumerable.Empty(Of JournalRecord)()).Where(Function(item) item IsNot Nothing).ToList()
            Return If(records.FirstOrDefault(Function(item) found.Count > 0 AndAlso IssnService.NormalizeList(item.Issns).Intersect(found).Any()),
                      records.FirstOrDefault(Function(item) RouteAnalyticsService.NameKey(item.Name) = RouteAnalyticsService.NameKey(If(journalName, String.Empty).Trim())))
        End Function


        ' The candidate that is this journal: by library record, name, ISSN,
        ' or OpenAlex id.
        Public Shared Function FindCandidate(manuscript As Manuscript, journalName As String, issns As IEnumerable(Of String), openAlexId As String, journalId As Guid?) As JournalCandidate
            If manuscript?.JournalShortlist Is Nothing Then Return Nothing
            Dim key As String = RouteAnalyticsService.NameKey(If(journalName, String.Empty))
            Dim found As List(Of String) = IssnService.NormalizeList(issns)
            Dim id As String = OpenAlexSourceClient.NormalizeId(openAlexId)
            Return manuscript.JournalShortlist.FirstOrDefault(
                Function(item)
                    If item Is Nothing Then Return False
                    If journalId.HasValue AndAlso item.JournalId.HasValue AndAlso item.JournalId.Value = journalId.Value Then Return True
                    If key.Length > 0 AndAlso RouteAnalyticsService.NameKey(item.JournalName) = key Then Return True
                    If item.Evidence Is Nothing Then Return False
                    If found.Count > 0 AndAlso IssnService.NormalizeList(item.Evidence.Issns).Intersect(found).Any() Then Return True
                    Return id.Length > 0 AndAlso String.Equals(OpenAlexSourceClient.NormalizeId(item.Evidence.OpenAlexId), id, StringComparison.Ordinal)
                End Function)
        End Function


        ' "Found by keyword search: 144 matching articles of 9,812 since 2021
        ' (OpenAlex, Sep 30, 2026)" for a shortlist row.
        Public Shared Function EvidenceLine(evidence As CandidateEvidence) As String
            If evidence Is Nothing Then Return String.Empty
            Dim culture As CultureInfo = CultureInfo.CurrentCulture
            Dim articles As String =
                evidence.MatchingArticles.ToString("N0", culture) &
                If(evidence.MatchingArticles = 1, " matching article", " matching articles") &
                If(evidence.AllArticles.HasValue, " of " & evidence.AllArticles.Value.ToString("N0", culture), String.Empty)
            Dim since As String = If(evidence.SinceDate.HasValue, " since " & evidence.SinceDate.Value.Year.ToString(CultureInfo.InvariantCulture), String.Empty)
            Dim where As String = If(String.IsNullOrWhiteSpace(evidence.Source), "OpenAlex", evidence.Source) &
                If(evidence.RetrievedUtc.HasValue, ", " & evidence.RetrievedUtc.Value.ToLocalTime().ToString("MMM d, yyyy", culture), String.Empty)
            Return "Found by keyword search: " & articles & since & " (" & where & ")"
        End Function


        ' Lenient, on load, save, and restore: an empty candidate is dropped,
        ' and evidence is tidied or dropped, never fatal.
        Public Shared Sub NormalizeManuscript(manuscript As Manuscript)

            If manuscript Is Nothing Then Return
            manuscript.JournalShortlist = If(manuscript.JournalShortlist, New List(Of JournalCandidate)()).Where(Function(item) item IsNot Nothing).ToList()

            For Each candidate As JournalCandidate In manuscript.JournalShortlist
                candidate.JournalName = If(candidate.JournalName, String.Empty)
                candidate.Notes = If(candidate.Notes, String.Empty)
                candidate.Checks = If(candidate.Checks, New List(Of String)()).Where(Function(item) Not String.IsNullOrWhiteSpace(item)).ToList()

                Dim evidence As CandidateEvidence = candidate.Evidence
                If evidence Is Nothing Then Continue For
                evidence.Source = Truncate(If(evidence.Source, String.Empty).Trim(), 40)
                evidence.OpenAlexId = OpenAlexSourceClient.NormalizeId(evidence.OpenAlexId)
                evidence.Issns = IssnService.NormalizeList(evidence.Issns)
                evidence.Keywords = If(evidence.Keywords, New List(Of String)()).
                    Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                    Select(Function(item) Truncate(item.Trim(), 80)).
                    Take(JournalSuggestionService.MaximumKeywords).
                    ToList()
                If evidence.MatchingArticles < 0 Then evidence.MatchingArticles = 0
                If evidence.AllArticles.HasValue AndAlso evidence.AllArticles.Value < 0 Then evidence.AllArticles = Nothing
                evidence.Examples = If(evidence.Examples, New List(Of EvidenceExample)()).
                    Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.Title)).
                    Take(3).
                    ToList()
                For Each example As EvidenceExample In evidence.Examples
                    example.Title = Truncate(example.Title.Trim(), 300)
                    Dim doi As String = DoiNormalizer.Normalize(If(example.Doi, String.Empty))
                    example.Doi = If(DoiNormalizer.IsValid(doi), doi, String.Empty)
                    If example.Year.HasValue AndAlso (example.Year.Value < 1600 OrElse example.Year.Value > 2200) Then example.Year = Nothing
                Next
            Next

        End Sub


        Private Shared Function Truncate(value As String, length As Integer) As String
            Return If(value.Length <= length, value, value.Substring(0, length))
        End Function

    End Class

End Namespace
