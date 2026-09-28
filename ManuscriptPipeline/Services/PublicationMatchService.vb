Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    ' The rules of the publication check (#61): which manuscripts to check,
    ' what counts as a possible publication, and what Mark Published and
    ' Fill Blanks change. Nothing here touches the network.
    Public NotInheritable Class PublicationMatchService

        ' Titles this similar (Dice coefficient over their meaningful words)
        ' are the same work.
        Public Const TitleThreshold As Double = 0.85

        ' Shorter titles, such as "Introduction", are too common to match.
        Public Const MinimumTitleWords As Integer = 4

        ' A title whose words all appear in the other, such as a working
        ' title that gained a subtitle, matches when it has at least this many.
        Public Const ContainedTitleWords As Integer = 5

        Private Shared ReadOnly StopWords As New HashSet(Of String)(
            {"a", "an", "the", "of", "in", "on", "for", "and", "or", "to", "with", "at", "by", "from", "as", "is", "are"})

        ' A publication earlier than the first submission, less this margin,
        ' is an older work that shares the title.
        Public Const SubmissionMarginDays As Integer = 30

        Private Shared ReadOnly NotPublications As String() = {
            "posted-content", "preprint", "peer-review", "dataset", "component", "working-paper", "grant"
        }

        Private Sub New()
        End Sub


        Public Shared Sub NormalizeAndValidateManuscript(manuscript As Manuscript)

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If manuscript.PublicationMatches Is Nothing Then manuscript.PublicationMatches = New List(Of PublicationMatch)()

            Dim ids As New HashSet(Of Guid)()

            For Each match As PublicationMatch In manuscript.PublicationMatches
                If match Is Nothing Then Throw New InvalidDataException("The manuscript library contains a null publication match.")
                If match.Id = Guid.Empty OrElse Not ids.Add(match.Id) Then
                    Throw New InvalidDataException("The manuscript library contains an invalid or duplicate publication match identifier.")
                End If
                If Not [Enum].IsDefined(match.Source) OrElse Not [Enum].IsDefined(match.Status) Then
                    Throw New InvalidDataException("The manuscript library contains a publication match with an unsupported source or status.")
                End If
                match.Doi = If(match.Doi, String.Empty)
                match.Title = If(match.Title, String.Empty)
                match.Journal = If(match.Journal, String.Empty)
                match.Url = If(match.Url, String.Empty)
                match.Publisher = If(match.Publisher, String.Empty)
                match.Volume = If(match.Volume, String.Empty)
                match.Issue = If(match.Issue, String.Empty)
                match.Pages = If(match.Pages, String.Empty)
            Next

        End Sub


        ' Anything not yet published can be checked.
        Public Shared Function IsEligible(manuscript As Manuscript) As Boolean
            Return manuscript IsNot Nothing AndAlso
                   manuscript.Location <> ManuscriptLocation.Published AndAlso
                   manuscript.CurrentStage <> PaperStage.Published
        End Function


        ' Checked by default: work that has gone to a journal.
        Public Shared Function IsSuggested(manuscript As Manuscript) As Boolean
            Return IsEligible(manuscript) AndAlso
                   manuscript.Location = ManuscriptLocation.Pipeline AndAlso
                   manuscript.CurrentStage >= PaperStage.Submitted
        End Function


        ' Lowercase words without accents or punctuation.
        Public Shared Function TitleWords(title As String) As List(Of String)

            Dim words As New List(Of String)()
            If String.IsNullOrWhiteSpace(title) Then Return words

            Dim decomposed As String = title.Normalize(NormalizationForm.FormD)
            Dim current As New StringBuilder()

            For Each character As Char In decomposed
                Select Case CharUnicodeInfo.GetUnicodeCategory(character)
                    Case UnicodeCategory.NonSpacingMark
                        Continue For
                    Case UnicodeCategory.LowercaseLetter, UnicodeCategory.UppercaseLetter, UnicodeCategory.TitlecaseLetter,
                         UnicodeCategory.OtherLetter, UnicodeCategory.DecimalDigitNumber
                        current.Append(Char.ToLowerInvariant(character))
                    Case Else
                        If current.Length > 0 Then words.Add(current.ToString()) : current.Clear()
                End Select
            Next

            If current.Length > 0 Then words.Add(current.ToString())
            Return words

        End Function


        ' The words that distinguish a title: no articles or prepositions.
        Public Shared Function ContentWords(title As String) As HashSet(Of String)
            Return New HashSet(Of String)(TitleWords(title).Where(Function(word) Not StopWords.Contains(word)))
        End Function


        ' 1 for the same words in any case or punctuation; 0 for none shared.
        Public Shared Function TitleSimilarity(first As String, second As String) As Double

            Dim a As HashSet(Of String) = ContentWords(first)
            Dim b As HashSet(Of String) = ContentWords(second)
            If a.Count = 0 OrElse b.Count = 0 Then Return 0

            Dim common As Integer = a.Where(Function(word) b.Contains(word)).Count()
            Return 2.0 * common / (a.Count + b.Count)

        End Function


        Public Shared Function IsSameTitle(first As String, second As String) As Boolean

            If TitleSimilarity(first, second) >= TitleThreshold Then Return True

            Dim a As HashSet(Of String) = ContentWords(first)
            Dim b As HashSet(Of String) = ContentWords(second)
            Dim smaller As HashSet(Of String) = If(a.Count <= b.Count, a, b)
            Dim larger As HashSet(Of String) = If(a.Count <= b.Count, b, a)
            Return smaller.Count >= ContainedTitleWords AndAlso smaller.IsSubsetOf(larger)

        End Function


        Public Shared Function IsPublicationType(workType As String) As Boolean
            Return Not NotPublications.Contains(If(workType, String.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
        End Function


        ' Found works are remembered by DOI, or by title when there is none.
        Public Shared Function MatchKey(doi As String, title As String) As String
            Dim normalized As String = DoiNormalizer.Normalize(If(doi, String.Empty))
            If DoiNormalizer.IsValid(normalized) Then Return "doi:" & normalized.ToLowerInvariant()
            Return "title:" & String.Join(" ", TitleWords(title))
        End Function


        Public Shared Function EarliestPlausibleDate(manuscript As Manuscript) As DateTime?

            If manuscript.Submissions Is Nothing Then Return Nothing

            Dim dates As List(Of DateTime) =
                manuscript.Submissions.Where(Function(submission) submission IsNot Nothing).
                    Select(Function(submission) submission.SubmittedDate.Date).ToList()

            If dates.Count = 0 Then Return Nothing
            Return dates.Min().AddDays(-SubmissionMarginDays)

        End Function


        ' Returns a new match when a found work qualifies and was not found
        ' before, whatever the earlier answer was; otherwise Nothing.
        Public Shared Function Consider(
            manuscript As Manuscript,
            work As CrossrefMetadataSuggestion,
            source As PublicationMatchSource
        ) As PublicationMatch

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If work Is Nothing Then Return Nothing

            If Not IsPublicationType(work.WorkType) Then Return Nothing

            Dim doi As String = DoiNormalizer.Normalize(If(work.Doi, String.Empty))
            Dim metadata As ManuscriptMetadata = If(manuscript.Metadata, New ManuscriptMetadata())

            If DoiNormalizer.IsValid(doi) AndAlso
               String.Equals(doi, DoiNormalizer.Normalize(If(metadata.PreprintDoi, String.Empty)), StringComparison.OrdinalIgnoreCase) Then
                Return Nothing
            End If

            Select Case source

                Case PublicationMatchSource.Doi, PublicationMatchSource.Preprint
                    ' A DOI names the work. It is a publication once it has a venue.
                    If String.IsNullOrWhiteSpace(work.Journal) Then Return Nothing

                Case Else
                    If ContentWords(manuscript.Title).Count < MinimumTitleWords Then Return Nothing
                    If Not IsSameTitle(manuscript.Title, work.Title) Then Return Nothing
                    Dim earliest As DateTime? = EarliestPlausibleDate(manuscript)
                    If earliest.HasValue AndAlso work.PublishedDate.HasValue AndAlso work.PublishedDate.Value.Date < earliest.Value Then
                        Return Nothing
                    End If

            End Select

            Dim key As String = MatchKey(doi, work.Title)
            If manuscript.PublicationMatches IsNot Nothing AndAlso
               manuscript.PublicationMatches.Any(Function(existing) existing IsNot Nothing AndAlso MatchKey(existing.Doi, existing.Title) = key) Then
                Return Nothing
            End If

            Return New PublicationMatch With {
                .Doi = If(DoiNormalizer.IsValid(doi), doi, String.Empty),
                .Title = If(work.Title, String.Empty).Trim(),
                .Journal = If(work.Journal, String.Empty).Trim(),
                .PublishedDate = If(work.PublishedDate.HasValue, work.PublishedDate.Value.Date, CType(Nothing, DateTime?)),
                .Url = If(work.Url, String.Empty).Trim(),
                .Publisher = If(work.Publisher, String.Empty).Trim(),
                .Volume = If(work.Volume, String.Empty).Trim(),
                .Issue = If(work.Issue, String.Empty).Trim(),
                .Pages = If(work.Pages, String.Empty).Trim(),
                .Source = source
            }

        End Function


        ' An ORCID work, in the shape Crossref results use.
        Public Shared Function FromOrcidWork(work As OrcidWorkSuggestion) As CrossrefMetadataSuggestion
            If work Is Nothing Then Return Nothing
            Return New CrossrefMetadataSuggestion With {
                .Doi = If(work.Doi, String.Empty),
                .Title = If(work.Title, String.Empty),
                .Journal = If(work.JournalTitle, String.Empty),
                .PublishedDate = work.PublishedDate,
                .WorkType = If(work.WorkType, String.Empty)
            }
        End Function


        Public Shared Function PendingMatches(manuscript As Manuscript) As List(Of PublicationMatch)
            If manuscript Is Nothing OrElse manuscript.PublicationMatches Is Nothing OrElse Not IsEligible(manuscript) Then
                Return New List(Of PublicationMatch)()
            End If
            Return manuscript.PublicationMatches.
                Where(Function(match) match IsNot Nothing AndAlso match.Status = PublicationMatchStatus.Pending).ToList()
        End Function


        Public Shared Sub Ignore(match As PublicationMatch, Optional reviewedAtUtc As DateTime? = Nothing)
            If match Is Nothing Then Throw New ArgumentNullException(NameOf(match))
            match.Status = PublicationMatchStatus.Ignored
            match.ReviewedAtUtc = If(reviewedAtUtc, DateTime.UtcNow)
        End Sub


        ' What Mark Published will change, in words, for the confirmation.
        Public Shared Function DescribeMarkPublished(manuscript As Manuscript, match As PublicationMatch, today As DateTime) As List(Of String)

            Dim lines As New List(Of String) From {
                "Move it to the Published shelf with the stage Published, from " &
                    PublishedFrom(match, today).ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) & ".",
                "Add """ & HistoryNote(match) & """ to its history."
            }

            If Not String.IsNullOrWhiteSpace(match.Journal) AndAlso
               Not String.Equals(If(manuscript.TargetJournal, String.Empty).Trim(), match.Journal.Trim(), StringComparison.CurrentCultureIgnoreCase) Then
                lines.Add(If(String.IsNullOrWhiteSpace(manuscript.TargetJournal),
                             "Set the journal to " & match.Journal & ".",
                             "Change the journal from " & manuscript.TargetJournal.Trim() & " to " & match.Journal & "."))
            End If

            Dim fills As List(Of String) = PlanFill(manuscript, ToWork(match)).Select(Function(fill) fill.Field).ToList()
            If fills.Count > 0 Then
                lines.Add("Fill empty fields: " & String.Join(", ", fills) & ". Fields that already have a value are kept.")
            End If

            Return lines

        End Function


        ' The explicit choice that changes stage, shelf, journal, and
        ' history. Earlier submissions and decisions are left as recorded.
        Public Shared Sub MarkPublished(manuscript As Manuscript, match As PublicationMatch, today As DateTime)

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If match Is Nothing Then Throw New ArgumentNullException(NameOf(match))

            Dim publishedOn As DateTime = PublishedFrom(match, today)

            ApplyFill(manuscript, PlanFill(manuscript, ToWork(match)))

            If Not String.IsNullOrWhiteSpace(match.Journal) AndAlso
               Not String.Equals(If(manuscript.TargetJournal, String.Empty).Trim(), match.Journal.Trim(), StringComparison.CurrentCultureIgnoreCase) Then
                manuscript.TargetJournal = match.Journal.Trim()
                manuscript.TargetJournalId = Nothing
            End If

            manuscript.CurrentStage = PaperStage.Published
            manuscript.Location = ManuscriptLocation.Published
            manuscript.StageEnteredDate = publishedOn
            manuscript.FileDrawerDate = Nothing
            manuscript.FileDrawerReason = String.Empty

            Dim published As New HistoryEvent With {
                .EventDate = publishedOn,
                .Stage = PaperStage.Published,
                .Note = HistoryNote(match)
            }
            ChronologyProvenanceService.StampCreated(published)
            If manuscript.History Is Nothing Then manuscript.History = New List(Of HistoryEvent)()
            manuscript.History.Add(published)

            match.Status = PublicationMatchStatus.Confirmed
            match.ReviewedAtUtc = DateTime.UtcNow

            ' Other possible matches no longer need review.
            For Each other As PublicationMatch In manuscript.PublicationMatches.Where(Function(item) item IsNot Nothing AndAlso item IsNot match AndAlso item.Status = PublicationMatchStatus.Pending)
                other.Status = PublicationMatchStatus.Ignored
                other.ReviewedAtUtc = match.ReviewedAtUtc
            Next

        End Sub


        Private Shared Function PublishedFrom(match As PublicationMatch, today As DateTime) As DateTime
            If match.PublishedDate.HasValue AndAlso match.PublishedDate.Value.Date <= today.Date Then Return match.PublishedDate.Value.Date
            Return today.Date
        End Function


        Private Shared Function HistoryNote(match As PublicationMatch) As String
            Dim note As String = "Published" & If(String.IsNullOrWhiteSpace(match.Journal), String.Empty, " in " & match.Journal.Trim())
            If Not String.IsNullOrWhiteSpace(match.Doi) Then note &= " (DOI " & match.Doi & ")"
            Return note & ". Marked from a publication check."
        End Function


        Private Shared Function ToWork(match As PublicationMatch) As CrossrefMetadataSuggestion
            Return New CrossrefMetadataSuggestion With {
                .Doi = match.Doi,
                .Title = match.Title,
                .Journal = match.Journal,
                .PublishedDate = match.PublishedDate,
                .Url = match.Url,
                .Publisher = match.Publisher,
                .Volume = match.Volume,
                .Issue = match.Issue,
                .Pages = match.Pages
            }
        End Function


        ' Empty fields a Crossref record can fill. A field with a value, even
        ' a different one, is never in the plan.
        Public Shared Function PlanFill(manuscript As Manuscript, work As CrossrefMetadataSuggestion) As List(Of MetadataFill)

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            Dim plan As New List(Of MetadataFill)()
            If work Is Nothing Then Return plan

            Dim metadata As ManuscriptMetadata = If(manuscript.Metadata, New ManuscriptMetadata())

            Dim add As Action(Of String, String, String, Action(Of ManuscriptMetadata)) =
                Sub(field, current, value, apply)
                    If String.IsNullOrWhiteSpace(current) AndAlso Not String.IsNullOrWhiteSpace(value) Then
                        plan.Add(New MetadataFill With {.Manuscript = manuscript, .Field = field, .Value = value.Trim(), .Apply = apply})
                    End If
                End Sub

            Dim doi As String = DoiNormalizer.Normalize(If(work.Doi, String.Empty))
            If DoiNormalizer.IsValid(doi) Then add("DOI", metadata.Doi, doi, Sub(target) target.Doi = doi)
            add("Journal", metadata.PublicationJournal, work.Journal, Sub(target) target.PublicationJournal = work.Journal.Trim())
            add("Publisher", metadata.Publisher, work.Publisher, Sub(target) target.Publisher = work.Publisher.Trim())
            If Not metadata.PublishedDate.HasValue AndAlso work.PublishedDate.HasValue Then
                Dim published As DateTime = work.PublishedDate.Value.Date
                plan.Add(New MetadataFill With {
                    .Manuscript = manuscript, .Field = "Publication date",
                    .Value = published.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture),
                    .Apply = Sub(target) target.PublishedDate = published})
            End If
            add("Volume", metadata.Volume, work.Volume, Sub(target) target.Volume = work.Volume.Trim())
            add("Issue", metadata.Issue, work.Issue, Sub(target) target.Issue = work.Issue.Trim())
            add("Pages", metadata.Pages, work.Pages, Sub(target) target.Pages = work.Pages.Trim())
            add("Publication URL", metadata.PublicationUrl, work.Url, Sub(target) target.PublicationUrl = work.Url.Trim())
            add("Abstract", metadata.AbstractText, work.AbstractText, Sub(target) target.AbstractText = work.AbstractText.Trim())

            Dim keywords As List(Of String) = If(work.Keywords, New List(Of String)()).
                Where(Function(keyword) Not String.IsNullOrWhiteSpace(keyword)).Select(Function(keyword) keyword.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            If (metadata.Keywords Is Nothing OrElse metadata.Keywords.Count = 0) AndAlso keywords.Count > 0 Then
                plan.Add(New MetadataFill With {
                    .Manuscript = manuscript, .Field = "Keywords", .Value = String.Join("; ", keywords),
                    .Apply = Sub(target) target.Keywords = keywords.ToList()})
            End If

            Return plan

        End Function


        Public Shared Sub ApplyFill(manuscript As Manuscript, fills As IEnumerable(Of MetadataFill))
            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If manuscript.Metadata Is Nothing Then manuscript.Metadata = New ManuscriptMetadata()
            For Each fill As MetadataFill In fills
                fill.Apply.Invoke(manuscript.Metadata)
            Next
        End Sub

    End Class


    ' One empty field and the value that would fill it.
    Public Class MetadataFill

        Public Property Manuscript As Manuscript

        Public Property Field As String = String.Empty

        Public Property Value As String = String.Empty

        Friend Property Apply As Action(Of ManuscriptMetadata)

    End Class

End Namespace
