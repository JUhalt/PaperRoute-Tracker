Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    ' How a journal submission ended, from its latest recorded decision.
    Public Enum SubmissionOutcome
        ' No closing decision yet, including a revision in progress.
        Open
        DeskRejected
        RejectedAfterReview
        ' Rejected without saying whether the paper was reviewed.
        Rejected
        Accepted
        Withdrawn
    End Enum


    Public NotInheritable Class SubmissionStatistics

        Public Property Submission As JournalSubmission

        Public Property JournalName As String = String.Empty

        Public Property SubmittedDate As DateTime

        ' The first recorded decision, and how long it took.
        Public Property FirstDecision As EditorialDecision? = Nothing

        Public Property DaysToFirstDecision As Integer? = Nothing

        ' Revision requests (major, minor, or revise and resubmit).
        Public Property RevisionRounds As Integer

        Public Property Outcome As SubmissionOutcome

        Public Property ClosedDate As DateTime? = Nothing

        ' Submitted to closed, or to today while open.
        Public Property DaysAtJournal As Integer? = Nothing

        ' True when the first decision was a review outcome; Nothing when the
        ' record does not say (no decision, withdrawn, or "rejected").
        Public Property WentToReview As Boolean? = Nothing

    End Class


    Public NotInheritable Class ManuscriptStatistics

        Public Property Manuscript As Manuscript

        ' Oldest first.
        Public Property Submissions As New List(Of SubmissionStatistics)()

        Public Property JournalCount As Integer

        Public Property FirstSubmitted As DateTime? = Nothing

        Public Property AcceptedDate As DateTime? = Nothing

        Public Property PublishedDate As DateTime? = Nothing

        Public Property DaysToAcceptance As Integer? = Nothing

        Public Property DaysToPublication As Integer? = Nothing

        Public ReadOnly Property RevisionRounds As Integer
            Get
                Return Submissions.Sum(Function(item) item.RevisionRounds)
            End Get
        End Property

    End Class


    ' Your own history with one journal (#62): personal records, not
    ' official journal statistics.
    Public NotInheritable Class JournalHistory

        Public Property JournalName As String = String.Empty

        Public Property JournalId As Guid? = Nothing

        Public Property Submissions As New List(Of (Manuscript As Manuscript, Statistics As SubmissionStatistics))()

        Public ReadOnly Property Count As Integer
            Get
                Return Submissions.Count
            End Get
        End Property

        Public Function CountOutcome(outcome As SubmissionOutcome) As Integer
            Return Submissions.Where(Function(item) item.Statistics.Outcome = outcome).Count()
        End Function

        ' Submissions that were asked for at least one revision.
        Public ReadOnly Property WithRevisions As Integer
            Get
                Return Submissions.Where(Function(item) item.Statistics.RevisionRounds > 0).Count()
            End Get
        End Property

        Public Property MedianDaysToFirstDecision As Median

        Public Property MedianReviewDays As Median

        Public ReadOnly Property LastSubmitted As DateTime
            Get
                Return Submissions.Max(Function(item) item.Statistics.SubmittedDate)
            End Get
        End Property

    End Class


    ' A median and how many records it is based on.
    Public Structure Median

        Public ReadOnly Value As Double?
        Public ReadOnly SampleSize As Integer

        Public Sub New(value As Double?, sampleSize As Integer)
            Me.Value = value
            Me.SampleSize = sampleSize
        End Sub

        Public ReadOnly Property HasValue As Boolean
            Get
                Return Value.HasValue
            End Get
        End Property

    End Structure


    Public NotInheritable Class LibraryStatistics

        Public Property Manuscripts As New List(Of ManuscriptStatistics)()

        Public Property Journals As New List(Of JournalHistory)()

        Public ReadOnly Property Submissions As IEnumerable(Of SubmissionStatistics)
            Get
                Return Manuscripts.SelectMany(Function(item) item.Submissions)
            End Get
        End Property

        Public Function CountOutcome(outcome As SubmissionOutcome) As Integer
            Return Submissions.Where(Function(item) item.Outcome = outcome).Count()
        End Function

        Public Property MedianDaysToFirstDecision As Median

        Public Property MedianDaysToDeskRejection As Median

        Public Property MedianReviewDays As Median

        Public Property MedianDaysToAcceptance As Median

        Public Property MedianDaysToPublication As Median

    End Class


    ' Route statistics (#30) from stored history only. A missing or
    ' inconsistent date leaves a value out; nothing is estimated, and
    ' nothing here changes a record.
    Public NotInheritable Class RouteAnalyticsService

        Private Sub New()
        End Sub


        Public Shared Function DescribeSubmission(submission As JournalSubmission, today As DateTime) As SubmissionStatistics

            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))

            Dim decisions As List(Of EditorialDecisionEvent) =
                If(submission.Decisions, New List(Of EditorialDecisionEvent)()).
                    Where(Function(item) item IsNot Nothing AndAlso item.Decision <> EditorialDecision.None).
                    OrderBy(Function(item) item.DecisionDate).ToList()

            Dim submitted As DateTime = submission.SubmittedDate.Date
            Dim statistics As New SubmissionStatistics With {
                .Submission = submission,
                .JournalName = If(submission.JournalName, String.Empty).Trim(),
                .SubmittedDate = submitted,
                .RevisionRounds = decisions.Where(Function(item) IsRevision(item.Decision)).Count()
            }

            If decisions.Count > 0 Then
                Dim first As EditorialDecisionEvent = decisions(0)
                statistics.FirstDecision = first.Decision
                statistics.DaysToFirstDecision = DaysBetween(submitted, first.DecisionDate)
                Select Case first.Decision
                    Case EditorialDecision.DeskRejected
                        statistics.WentToReview = False
                    Case EditorialDecision.RejectedAfterReview, EditorialDecision.MajorRevision, EditorialDecision.MinorRevision,
                         EditorialDecision.ReviseAndResubmit, EditorialDecision.Accepted
                        statistics.WentToReview = True
                End Select
            End If

            Dim latest As EditorialDecisionEvent = decisions.LastOrDefault()
            statistics.Outcome = OutcomeOf(If(latest Is Nothing, EditorialDecision.None, latest.Decision))

            If statistics.Outcome <> SubmissionOutcome.Open Then
                statistics.ClosedDate = latest.DecisionDate.Date
                statistics.DaysAtJournal = DaysBetween(submitted, latest.DecisionDate)
            Else
                statistics.DaysAtJournal = DaysBetween(submitted, today)
            End If

            Return statistics

        End Function


        Public Shared Function DescribeManuscript(manuscript As Manuscript, today As DateTime) As ManuscriptStatistics

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))

            Dim statistics As New ManuscriptStatistics With {.Manuscript = manuscript}

            statistics.Submissions = If(manuscript.Submissions, New List(Of JournalSubmission)()).
                Where(Function(item) item IsNot Nothing).
                OrderBy(Function(item) item.SubmittedDate).
                Select(Function(item) DescribeSubmission(item, today)).ToList()

            statistics.JournalCount = statistics.Submissions.
                Select(Function(item) NameKey(item.JournalName)).Distinct().Count()

            If statistics.Submissions.Count > 0 Then
                statistics.FirstSubmitted = statistics.Submissions(0).SubmittedDate
            End If

            Dim accepted As SubmissionStatistics = statistics.Submissions.FirstOrDefault(Function(item) item.Outcome = SubmissionOutcome.Accepted)
            If accepted IsNot Nothing Then statistics.AcceptedDate = accepted.ClosedDate

            statistics.PublishedDate = PublishedDateOf(manuscript)

            If statistics.FirstSubmitted.HasValue Then
                If statistics.AcceptedDate.HasValue Then
                    statistics.DaysToAcceptance = DaysBetween(statistics.FirstSubmitted.Value, statistics.AcceptedDate.Value)
                End If
                If statistics.PublishedDate.HasValue Then
                    statistics.DaysToPublication = DaysBetween(statistics.FirstSubmitted.Value, statistics.PublishedDate.Value)
                End If
            End If

            Return statistics

        End Function


        ' The publication date field, or else the date the manuscript was
        ' recorded as published. Nothing when neither exists.
        Public Shared Function PublishedDateOf(manuscript As Manuscript) As DateTime?

            If manuscript.Metadata IsNot Nothing AndAlso manuscript.Metadata.PublishedDate.HasValue Then
                Return manuscript.Metadata.PublishedDate.Value.Date
            End If

            Dim published As HistoryEvent =
                If(manuscript.History, New List(Of HistoryEvent)()).
                    Where(Function(item) item IsNot Nothing AndAlso item.Stage = PaperStage.Published).
                    OrderBy(Function(item) item.EventDate).FirstOrDefault()

            Return If(published Is Nothing, CType(Nothing, DateTime?), published.EventDate.Date)

        End Function


        Public Shared Function ForLibrary(
            manuscripts As IEnumerable(Of Manuscript),
            today As DateTime,
            Optional journalLibrary As IEnumerable(Of JournalRecord) = Nothing
        ) As LibraryStatistics

            If manuscripts Is Nothing Then Throw New ArgumentNullException(NameOf(manuscripts))

            Dim library As New LibraryStatistics With {
                .Manuscripts = manuscripts.Where(Function(item) item IsNot Nothing).
                    Select(Function(item) DescribeManuscript(item, today)).ToList()
            }

            Dim submissions As List(Of SubmissionStatistics) = library.Submissions.ToList()

            library.MedianDaysToFirstDecision = MedianOf(submissions.Select(Function(item) item.DaysToFirstDecision))
            library.MedianDaysToDeskRejection = MedianOf(submissions.Where(Function(item) item.FirstDecision.HasValue AndAlso item.FirstDecision.Value = EditorialDecision.DeskRejected).Select(Function(item) item.DaysToFirstDecision))
            library.MedianReviewDays = MedianOf(submissions.Where(Function(item) item.WentToReview.GetValueOrDefault()).Select(Function(item) item.DaysToFirstDecision))
            library.MedianDaysToAcceptance = MedianOf(library.Manuscripts.Select(Function(item) item.DaysToAcceptance))
            library.MedianDaysToPublication = MedianOf(library.Manuscripts.Select(Function(item) item.DaysToPublication))

            library.Journals = GroupByJournal(library.Manuscripts, journalLibrary)

            Return library

        End Function


        ' Submissions linked to a Journal Library record count under that
        ' record; free-text names count under the same record only when the
        ' name matches it exactly (ignoring case and spacing). Anything else
        ' keeps its own name: nothing is merged by guess.
        Private Shared Function GroupByJournal(manuscripts As List(Of ManuscriptStatistics), journalLibrary As IEnumerable(Of JournalRecord)) As List(Of JournalHistory)

            Dim records As List(Of JournalRecord) = If(journalLibrary, Enumerable.Empty(Of JournalRecord)()).Where(Function(item) item IsNot Nothing).ToList()
            Dim groups As New Dictionary(Of String, JournalHistory)(StringComparer.Ordinal)

            For Each manuscript As ManuscriptStatistics In manuscripts
                For Each submission As SubmissionStatistics In manuscript.Submissions

                    Dim record As JournalRecord = Nothing
                    If submission.Submission.JournalId.HasValue Then
                        record = records.FirstOrDefault(Function(item) item.Id = submission.Submission.JournalId.Value)
                    End If
                    If record Is Nothing AndAlso submission.JournalName.Length > 0 Then
                        Dim key As String = NameKey(submission.JournalName)
                        record = records.FirstOrDefault(Function(item) NameKey(item.Name) = key)
                    End If

                    Dim groupKey As String
                    Dim display As String
                    If record IsNot Nothing Then
                        groupKey = "id:" & record.Id.ToString()
                        display = record.Name.Trim()
                    ElseIf submission.JournalName.Length > 0 Then
                        groupKey = "name:" & NameKey(submission.JournalName)
                        display = submission.JournalName
                    Else
                        groupKey = "none"
                        display = "(No journal recorded)"
                    End If

                    Dim history As JournalHistory = Nothing
                    If Not groups.TryGetValue(groupKey, history) Then
                        history = New JournalHistory With {.JournalName = display, .JournalId = If(record Is Nothing, CType(Nothing, Guid?), record.Id)}
                        groups(groupKey) = history
                    End If
                    history.Submissions.Add((manuscript.Manuscript, submission))

                Next
            Next

            For Each history As JournalHistory In groups.Values
                Dim items As List(Of SubmissionStatistics) = history.Submissions.Select(Function(item) item.Statistics).ToList()
                history.MedianDaysToFirstDecision = MedianOf(items.Select(Function(item) item.DaysToFirstDecision))
                history.MedianReviewDays = MedianOf(items.Where(Function(item) item.WentToReview.GetValueOrDefault()).Select(Function(item) item.DaysToFirstDecision))
            Next

            Return groups.Values.
                OrderByDescending(Function(item) item.Count).
                ThenBy(Function(item) item.JournalName, StringComparer.CurrentCultureIgnoreCase).ToList()

        End Function


        Public Shared Function MedianOf(values As IEnumerable(Of Integer?)) As Median

            Dim known As List(Of Integer) = values.Where(Function(item) item.HasValue).Select(Function(item) item.Value).OrderBy(Function(item) item).ToList()
            If known.Count = 0 Then Return New Median(Nothing, 0)

            Dim middle As Integer = known.Count \ 2
            Dim value As Double = If(known.Count Mod 2 = 1, known(middle), (known(middle - 1) + known(middle)) / 2.0)
            Return New Median(value, known.Count)

        End Function


        Public Shared Function OutcomeOf(decision As EditorialDecision) As SubmissionOutcome
            Select Case decision
                Case EditorialDecision.DeskRejected : Return SubmissionOutcome.DeskRejected
                Case EditorialDecision.RejectedAfterReview : Return SubmissionOutcome.RejectedAfterReview
                Case EditorialDecision.Rejected : Return SubmissionOutcome.Rejected
                Case EditorialDecision.Accepted : Return SubmissionOutcome.Accepted
                Case EditorialDecision.Withdrawn : Return SubmissionOutcome.Withdrawn
                Case Else : Return SubmissionOutcome.Open
            End Select
        End Function


        Private Shared Function IsRevision(decision As EditorialDecision) As Boolean
            Return decision = EditorialDecision.MajorRevision OrElse
                   decision = EditorialDecision.MinorRevision OrElse
                   decision = EditorialDecision.ReviseAndResubmit
        End Function


        ' A later date before an earlier one is a data-entry slip, not a
        ' negative duration; it is left out.
        Private Shared Function DaysBetween(start As DateTime, finish As DateTime) As Integer?
            Dim days As Integer = (finish.Date - start.Date).Days
            Return If(days >= 0, days, CType(Nothing, Integer?))
        End Function


        ' "2 submissions · 1 accepted · median 45 days to a first decision ·
        ' last submitted Mar 1, 2026." for one journal (#62).
        Public Shared Function DescribeHistory(history As JournalHistory) As String

            If history Is Nothing OrElse history.Count = 0 Then Return "no submissions recorded yet."

            Dim parts As New List(Of String) From {history.Count.ToString(Globalization.CultureInfo.CurrentCulture) & If(history.Count = 1, " submission", " submissions")}
            For Each outcome In {(history.CountOutcome(SubmissionOutcome.Accepted), "accepted"),
                                 (history.WithRevisions, "asked to revise"),
                                 (history.CountOutcome(SubmissionOutcome.RejectedAfterReview) + history.CountOutcome(SubmissionOutcome.Rejected), "rejected"),
                                 (history.CountOutcome(SubmissionOutcome.DeskRejected), "desk rejected")}
                If outcome.Item1 > 0 Then parts.Add(outcome.Item1.ToString(Globalization.CultureInfo.CurrentCulture) & " " & outcome.Item2)
            Next
            If history.MedianDaysToFirstDecision.HasValue Then
                parts.Add("median " & history.MedianDaysToFirstDecision.Value.Value.ToString("0.#", Globalization.CultureInfo.CurrentCulture) & " days to a first decision")
            End If
            parts.Add("last submitted " & history.LastSubmitted.ToString("MMM d, yyyy", Globalization.CultureInfo.CurrentCulture) & ".")
            Return String.Join(" · ", parts)

        End Function


        ' A journal's history by Journal Library record, or else by exact name.
        Public Shared Function FindHistory(statistics As LibraryStatistics, journalName As String, journalId As Guid?) As JournalHistory
            If statistics Is Nothing Then Return Nothing
            If journalId.HasValue Then
                Dim linked As JournalHistory = statistics.Journals.FirstOrDefault(Function(item) item.JournalId.HasValue AndAlso item.JournalId.Value = journalId.Value)
                If linked IsNot Nothing Then Return linked
            End If
            Dim key As String = NameKey(journalName)
            Return statistics.Journals.FirstOrDefault(Function(item) NameKey(item.JournalName) = key)
        End Function


        Friend Shared Function NameKey(name As String) As String
            Return Regex.Replace(If(name, String.Empty).Trim(), "\s+", " ").ToUpperInvariant()
        End Function

    End Class

End Namespace
