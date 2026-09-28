Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    ' Where a publication check looks. The app asks Crossref and ORCID;
    ' tests answer with synthetic records.
    Public Interface IPublicationSource

        ' Nothing when the DOI is unknown.
        Function LookupDoiAsync(doi As String, cancellationToken As CancellationToken) As Task(Of CrossrefMetadataSuggestion)

        Function SearchTitleAsync(title As String, cancellationToken As CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion))

        Function OrcidWorksAsync(orcid As String, cancellationToken As CancellationToken) As Task(Of List(Of OrcidWorkSuggestion))

    End Interface


    Public Class OnlinePublicationSource
        Implements IPublicationSource

        Private ReadOnly _crossref As New CrossrefClient()
        Private ReadOnly _orcid As New OrcidClient()

        Public Async Function LookupDoiAsync(doi As String, cancellationToken As CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Try
                Return Await _crossref.LookupAsync(doi, cancellationToken)
            Catch ex As CrossrefNotFoundException
                Return Nothing
            End Try
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Return _crossref.SearchByTitleAsync(title, cancellationToken)
        End Function

        Public Async Function OrcidWorksAsync(orcid As String, cancellationToken As CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Dim profile As OrcidProfileSuggestion = Await _orcid.LookupAsync(orcid, cancellationToken)
            Return If(profile?.Works, New List(Of OrcidWorkSuggestion)())
        End Function

    End Class


    Public Class PublicationCheckProgress
        Public Property Index As Integer
        Public Property Total As Integer
        Public Property ManuscriptTitle As String = String.Empty
    End Class


    Public Class PublicationCheckResult

        ' New possible publications, not yet added to their manuscripts.
        Public Property Matches As New List(Of (Manuscript As Manuscript, Match As PublicationMatch))()

        Public Property Checked As Integer

        ' Manuscripts that could not be checked, and why.
        Public Property Failures As New List(Of (Manuscript As Manuscript, Reason As String))()

        ' Set when the check ended early: cancelled, or Crossref asked to slow down.
        Public Property StoppedReason As String = String.Empty

    End Class


    ' Runs a user-initiated publication check (#61). It asks one question at
    ' a time, never changes a manuscript, and reports what it could not do.
    Public NotInheritable Class PublicationCheckService

        Private Sub New()
        End Sub


        Public Shared Async Function CheckAsync(
            manuscripts As IList(Of Manuscript),
            orcid As String,
            source As IPublicationSource,
            Optional progress As IProgress(Of PublicationCheckProgress) = Nothing,
            Optional pause As TimeSpan = Nothing,
            Optional cancellationToken As CancellationToken = Nothing
        ) As Task(Of PublicationCheckResult)

            If manuscripts Is Nothing Then Throw New ArgumentNullException(NameOf(manuscripts))
            If source Is Nothing Then Throw New ArgumentNullException(NameOf(source))

            Dim result As New PublicationCheckResult()
            Dim works As New List(Of OrcidWorkSuggestion)()

            Try

                If Not String.IsNullOrWhiteSpace(orcid) Then
                    Try
                        works = Await source.OrcidWorksAsync(OrcidIdentifierService.NormalizeAndValidate(orcid), cancellationToken)
                    Catch ex As Exception When IsLookupFailure(ex, cancellationToken)
                        result.Failures.Add((Nothing, "The ORCID record could not be read: " & ex.Message))
                    End Try
                End If

                For index As Integer = 0 To manuscripts.Count - 1

                    Dim manuscript As Manuscript = manuscripts(index)
                    If manuscript Is Nothing OrElse Not PublicationMatchService.IsEligible(manuscript) Then Continue For

                    cancellationToken.ThrowIfCancellationRequested()
                    progress?.Report(New PublicationCheckProgress With {.Index = index + 1, .Total = manuscripts.Count, .ManuscriptTitle = manuscript.Title})

                    Try
                        Dim match As PublicationMatch = Await CheckOneAsync(manuscript, works, source, pause, cancellationToken)
                        If match IsNot Nothing Then result.Matches.Add((manuscript, match))
                        result.Checked += 1
                    Catch ex As CrossrefRateLimitException
                        Throw
                    Catch ex As Exception When IsLookupFailure(ex, cancellationToken)
                        result.Failures.Add((manuscript, ex.Message))
                    End Try

                Next

            Catch ex As CrossrefRateLimitException
                result.StoppedReason = ex.Message
            Catch ex As OperationCanceledException When cancellationToken.IsCancellationRequested
                result.StoppedReason = "The check was cancelled."
            End Try

            Return result

        End Function


        ' The manuscript's own DOIs first, then its title, then the ORCID works.
        Private Shared Async Function CheckOneAsync(
            manuscript As Manuscript,
            works As List(Of OrcidWorkSuggestion),
            source As IPublicationSource,
            pause As TimeSpan,
            cancellationToken As CancellationToken
        ) As Task(Of PublicationMatch)

            Dim metadata As ManuscriptMetadata = If(manuscript.Metadata, New ManuscriptMetadata())

            For Each candidate In {(metadata.Doi, True), (metadata.PreprintDoi, False)}

                Dim doi As String = DoiNormalizer.Normalize(If(candidate.Item1, String.Empty))
                If Not DoiNormalizer.IsValid(doi) Then Continue For

                Dim work As CrossrefMetadataSuggestion = Await source.LookupDoiAsync(doi, cancellationToken)
                Await Wait(pause, cancellationToken)
                If work Is Nothing Then Continue For

                For Each published As String In If(work.PublishedVersionDois, New List(Of String)())
                    Dim version As CrossrefMetadataSuggestion = Await source.LookupDoiAsync(published, cancellationToken)
                    Await Wait(pause, cancellationToken)
                    Dim match As PublicationMatch = PublicationMatchService.Consider(manuscript, version, PublicationMatchSource.Preprint)
                    If match IsNot Nothing Then Return match
                Next

                If candidate.Item2 Then
                    Dim match As PublicationMatch = PublicationMatchService.Consider(manuscript, work, PublicationMatchSource.Doi)
                    If match IsNot Nothing Then Return match
                End If

            Next

            If PublicationMatchService.ContentWords(manuscript.Title).Count >= PublicationMatchService.MinimumTitleWords Then
                Dim found As List(Of CrossrefMetadataSuggestion) = Await source.SearchTitleAsync(manuscript.Title, cancellationToken)
                Await Wait(pause, cancellationToken)
                Dim best As PublicationMatch = BestByTitle(manuscript, found, PublicationMatchSource.Title)
                If best IsNot Nothing Then Return best
            End If

            Return BestByTitle(manuscript, works.Select(Function(work) PublicationMatchService.FromOrcidWork(work)), PublicationMatchSource.Orcid)

        End Function


        Private Shared Function BestByTitle(manuscript As Manuscript, works As IEnumerable(Of CrossrefMetadataSuggestion), source As PublicationMatchSource) As PublicationMatch
            Return If(works, Enumerable.Empty(Of CrossrefMetadataSuggestion)()).
                Where(Function(work) work IsNot Nothing).
                OrderByDescending(Function(work) PublicationMatchService.TitleSimilarity(manuscript.Title, work.Title)).
                Select(Function(work) PublicationMatchService.Consider(manuscript, work, source)).
                FirstOrDefault(Function(match) match IsNot Nothing)
        End Function


        ' A pause between requests keeps a large check polite to Crossref.
        Private Shared Async Function Wait(pause As TimeSpan, cancellationToken As CancellationToken) As Task
            If pause > TimeSpan.Zero Then Await Task.Delay(pause, cancellationToken)
        End Function


        ' Network trouble and unexpected responses affect one manuscript;
        ' the user's Cancel does not count.
        Private Shared Function IsLookupFailure(ex As Exception, cancellationToken As CancellationToken) As Boolean
            If TypeOf ex Is CrossrefRateLimitException Then Return False
            If TypeOf ex Is OperationCanceledException Then Return Not cancellationToken.IsCancellationRequested
            Return TypeOf ex Is HttpRequestException OrElse
                   TypeOf ex Is InvalidOperationException OrElse
                   TypeOf ex Is ArgumentException OrElse
                   TypeOf ex Is System.Text.Json.JsonException
        End Function

    End Class

End Namespace
