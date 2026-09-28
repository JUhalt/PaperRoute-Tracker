Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' Who holds the ball during a stretch of a route (#82).
    Public Enum RouteSegmentKind
        ' Submitted and waiting on the journal.
        Journal
        ' Revising, or preparing the next submission after a rejection.
        Author
        ' Accepted and waiting on the publisher.
        Production
        ' The route continued, but its dates do not say whose turn it was,
        ' such as a revision whose resubmission date was never entered.
        NotRecorded
    End Enum


    Public Enum RouteMarkerKind
        Submitted
        Decision
        Resubmitted
        Published
    End Enum


    Public NotInheritable Class RouteSegment
        Public Property Kind As RouteSegmentKind
        Public Property Start As DateTime
        Public Property Finish As DateTime
        ' Still running today.
        Public Property Ongoing As Boolean
        Public Property JournalName As String = String.Empty

        Public ReadOnly Property Days As Integer
            Get
                Return (Finish.Date - Start.Date).Days
            End Get
        End Property
    End Class


    Public NotInheritable Class RouteMarker
        Public Property Kind As RouteMarkerKind
        Public Property MarkerDate As DateTime
        Public Property Decision As EditorialDecision = EditorialDecision.None
        Public Property JournalName As String = String.Empty
    End Class


    Public NotInheritable Class RouteMap

        Public Property Segments As New List(Of RouteSegment)()

        Public Property Markers As New List(Of RouteMarker)()

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Segments.Count = 0
            End Get
        End Property

        Public ReadOnly Property Start As DateTime
            Get
                Return If(IsEmpty, DateTime.MinValue, Segments.Min(Function(item) item.Start))
            End Get
        End Property

        Public ReadOnly Property Finish As DateTime
            Get
                Return If(IsEmpty, DateTime.MinValue, Segments.Max(Function(item) item.Finish))
            End Get
        End Property

        Public ReadOnly Property TotalDays As Integer
            Get
                Return If(IsEmpty, 0, (Finish.Date - Start.Date).Days)
            End Get
        End Property

        Public Function DaysOf(kind As RouteSegmentKind) As Integer
            Return Segments.Where(Function(item) item.Kind = kind).Sum(Function(item) item.Days)
        End Function

        Public ReadOnly Property Ongoing As Boolean
            Get
                Return Segments.Any(Function(item) item.Ongoing)
            End Get
        End Property

        ' The journals in route order.
        Public ReadOnly Property Journals As List(Of String)
            Get
                Return Segments.Where(Function(item) item.JournalName.Length > 0).Select(Function(item) item.JournalName).Distinct().ToList()
            End Get
        End Property

    End Class


    Public NotInheritable Class RouteStep
        Public Property Number As Integer
        Public Property Marker As RouteMarker
        Public Property Title As String = String.Empty
        Public Property Note As String = String.Empty
    End Class


    Public NotInheritable Class RouteMapEntry
        Public Property Manuscript As Manuscript
        Public Property Map As RouteMap
        ' Reached a recorded publication date.
        Public Property IsPublished As Boolean
    End Class


    Public NotInheritable Class RouteMapLibrary

        Public Property Routes As New List(Of RouteMapEntry)()

        ' Days from first submission to publication, published routes only.
        Public ReadOnly Property MedianDaysToPublication As Median
            Get
                Return RouteAnalyticsService.MedianOf(Routes.Where(Function(item) item.IsPublished).Select(Function(item) CType(item.Map.TotalDays, Integer?)))
            End Get
        End Property

        Public ReadOnly Property TotalDays As Integer
            Get
                Return Routes.Sum(Function(item) item.Map.Segments.Sum(Function(segment) segment.Days))
            End Get
        End Property

        ' Share of all drawn days, 0 to 1.
        Public Function Share(kind As RouteSegmentKind) As Double
            Dim total As Integer = TotalDays
            If total = 0 Then Return 0
            Return Routes.Sum(Function(item) item.Map.DaysOf(kind)) / CDbl(total)
        End Function

    End Class


    ' Draws a manuscript's route to scale from recorded dates only (#82).
    ' Waiting on a journal runs from a submission (or resubmission) to its
    ' decision; the author's turn runs from a revision request to the
    ' recorded return to review, or from a rejection to the next
    ' submission; production runs from acceptance to publication. A stretch
    ' whose split is not recorded is shown as such, never estimated.
    Public NotInheritable Class RouteMapService

        Private Sub New()
        End Sub


        Public Shared Function Build(manuscript As Manuscript, today As DateTime) As RouteMap

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))

            Dim map As New RouteMap()
            Dim now As DateTime = today.Date
            Dim submissions As List(Of JournalSubmission) =
                If(manuscript.Submissions, New List(Of JournalSubmission)()).
                    Where(Function(item) item IsNot Nothing AndAlso item.SubmittedDate.Date <= now).
                    OrderBy(Function(item) item.SubmittedDate).ToList()

            If submissions.Count = 0 Then Return map

            Dim returns As List(Of DateTime) =
                If(manuscript.History, New List(Of HistoryEvent)()).
                    Where(Function(item) item IsNot Nothing AndAlso (item.Stage = PaperStage.Submitted OrElse item.Stage = PaperStage.UnderReview)).
                    Select(Function(item) item.EventDate.Date).OrderBy(Function(item) item).ToList()

            Dim published As DateTime? = If(manuscript.Location = ManuscriptLocation.Published OrElse manuscript.CurrentStage = PaperStage.Published,
                                            RouteAnalyticsService.PublishedDateOf(manuscript), CType(Nothing, DateTime?))
            Dim active As Boolean = manuscript.Location = ManuscriptLocation.Pipeline AndAlso manuscript.CurrentStage <> PaperStage.Published

            For index As Integer = 0 To submissions.Count - 1

                Dim submission As JournalSubmission = submissions(index)
                Dim journal As String = If(submission.JournalName, String.Empty).Trim()
                Dim nextStart As DateTime? = If(index < submissions.Count - 1, submissions(index + 1).SubmittedDate.Date, CType(Nothing, DateTime?))
                Dim isLast As Boolean = Not nextStart.HasValue
                Dim cursor As DateTime = submission.SubmittedDate.Date
                Dim phase As RouteSegmentKind = RouteSegmentKind.Journal
                Dim closed As EditorialDecision = EditorialDecision.None

                map.Markers.Add(New RouteMarker With {.Kind = RouteMarkerKind.Submitted, .MarkerDate = cursor, .JournalName = journal})

                Dim decisions As List(Of EditorialDecisionEvent) =
                    If(submission.Decisions, New List(Of EditorialDecisionEvent)()).
                        Where(Function(item) item IsNot Nothing AndAlso item.Decision <> EditorialDecision.None AndAlso item.DecisionDate.Date <= now).
                        OrderBy(Function(item) item.DecisionDate).ToList()

                For decisionIndex As Integer = 0 To decisions.Count - 1

                    Dim decision As EditorialDecisionEvent = decisions(decisionIndex)
                    Dim decided As DateTime = decision.DecisionDate.Date
                    If decided < cursor Then Continue For

                    AddSegment(map, phase, cursor, decided, False, journal)
                    map.Markers.Add(New RouteMarker With {.Kind = RouteMarkerKind.Decision, .MarkerDate = decided, .Decision = decision.Decision, .JournalName = journal})
                    cursor = decided

                    If IsRevision(decision.Decision) Then
                        ' The author's turn ends at the recorded return to
                        ' review, if one falls before the next decision.
                        Dim nextDecision As DateTime? = If(decisionIndex < decisions.Count - 1, decisions(decisionIndex + 1).DecisionDate.Date, CType(Nothing, DateTime?))
                        Dim limit As DateTime = If(nextDecision, If(nextStart, now))
                        Dim resubmitted As DateTime? = returns.Where(Function(item) item > decided AndAlso item <= limit).Select(Function(item) CType(item, DateTime?)).FirstOrDefault()

                        If resubmitted.HasValue Then
                            AddSegment(map, RouteSegmentKind.Author, cursor, resubmitted.Value, False, journal)
                            map.Markers.Add(New RouteMarker With {.Kind = RouteMarkerKind.Resubmitted, .MarkerDate = resubmitted.Value, .JournalName = journal})
                            cursor = resubmitted.Value
                            phase = RouteSegmentKind.Journal
                        ElseIf nextDecision.HasValue Then
                            phase = RouteSegmentKind.NotRecorded
                        Else
                            ' Still revising, when that is the current stage.
                            phase = If(isLast AndAlso active AndAlso manuscript.CurrentStage = PaperStage.Revision, RouteSegmentKind.Author, RouteSegmentKind.NotRecorded)
                        End If
                    ElseIf decision.Decision = EditorialDecision.Accepted OrElse RouteAnalyticsService.OutcomeOf(decision.Decision) <> SubmissionOutcome.Open Then
                        closed = decision.Decision
                        Exit For
                    Else
                        phase = RouteSegmentKind.Journal
                    End If

                Next

                ' What follows the last recorded decision.
                If closed = EditorialDecision.Accepted Then
                    If published.HasValue AndAlso published.Value.Date >= cursor Then
                        AddSegment(map, RouteSegmentKind.Production, cursor, published.Value.Date, False, String.Empty)
                        map.Markers.Add(New RouteMarker With {.Kind = RouteMarkerKind.Published, .MarkerDate = published.Value.Date})
                    ElseIf active Then
                        AddSegment(map, RouteSegmentKind.Production, cursor, now, True, String.Empty)
                    End If
                ElseIf closed <> EditorialDecision.None Then
                    ' Rejected or withdrawn: preparing the next submission.
                    If nextStart.HasValue Then
                        AddSegment(map, RouteSegmentKind.Author, cursor, nextStart.Value, False, String.Empty)
                    ElseIf active Then
                        AddSegment(map, RouteSegmentKind.Author, cursor, now, True, String.Empty)
                    End If
                Else
                    If isLast AndAlso Not active Then
                        ' Filed, or published without a recorded decision:
                        ' what happened after the last decision is unknown.
                        If published.HasValue Then AddSegment(map, RouteSegmentKind.NotRecorded, cursor, published.Value.Date, False, journal)
                    Else
                        AddSegment(map, phase, cursor, If(nextStart, now), isLast, journal)
                    End If
                End If

            Next

            Return map

        End Function


        Private Shared Sub AddSegment(map As RouteMap, kind As RouteSegmentKind, start As DateTime, finish As DateTime, ongoing As Boolean, journal As String)
            If finish <= start Then Return
            Dim previous As RouteSegment = map.Segments.LastOrDefault()
            ' Overlapping submissions: never draw a stretch twice.
            If previous IsNot Nothing AndAlso start < previous.Finish Then start = previous.Finish
            If finish <= start Then Return
            map.Segments.Add(New RouteSegment With {
                .Kind = kind, .Start = start, .Finish = finish, .Ongoing = ongoing,
                .JournalName = If(kind = RouteSegmentKind.Journal OrElse kind = RouteSegmentKind.NotRecorded, journal, String.Empty)
            })
        End Sub


        Private Shared Function IsRevision(decision As EditorialDecision) As Boolean
            Return decision = EditorialDecision.MajorRevision OrElse
                   decision = EditorialDecision.MinorRevision OrElse
                   decision = EditorialDecision.ReviseAndResubmit
        End Function


        ' The numbered events under a route map, each with what it took:
        ' "Desk rejected" / "7 days after submission". The first submission
        ' is where the map starts, so it is not numbered.
        Public Shared Function Steps(map As RouteMap) As List(Of RouteStep)

            Dim result As New List(Of RouteStep)()
            If map Is Nothing Then Return result

            For index As Integer = 1 To map.Markers.Count - 1
                Dim marker As RouteMarker = map.Markers(index)
                Dim previous As RouteMarker = map.Markers(index - 1)
                Dim days As String = DaysText((marker.MarkerDate.Date - previous.MarkerDate.Date).Days)
                Dim title As String
                Dim note As String

                Select Case marker.Kind
                    Case RouteMarkerKind.Submitted
                        title = If(marker.JournalName.Length > 0, "Submitted to " & marker.JournalName, "Submitted")
                        note = If(previous.Kind = RouteMarkerKind.Decision AndAlso RouteAnalyticsService.OutcomeOf(previous.Decision) <> SubmissionOutcome.Open,
                                  "after " & days & " of rerouting", days & " after the previous submission")
                    Case RouteMarkerKind.Resubmitted
                        title = "Resubmitted"
                        note = "after " & days & " of revising"
                    Case RouteMarkerKind.Published
                        title = "Published"
                        note = days & If(previous.Kind = RouteMarkerKind.Decision AndAlso previous.Decision = EditorialDecision.Accepted, " after acceptance", " later")
                    Case Else
                        title = EditorialDecisionDisplayService.Format(marker.Decision)
                        Select Case previous.Kind
                            Case RouteMarkerKind.Submitted : note = days & " after submission"
                            Case RouteMarkerKind.Resubmitted : note = days & " after resubmission"
                            Case Else : note = days & " after the previous decision"
                        End Select
                End Select

                result.Add(New RouteStep With {.Number = index, .Marker = marker, .Title = title, .Note = note})
            Next

            Return result

        End Function


        ' "2 journals · 2 revisions · 1 rejection" for a row of the
        ' side-by-side map.
        Public Shared Function Brief(map As RouteMap) As String
            Dim decisions As List(Of RouteMarker) = map.Markers.Where(Function(item) item.Kind = RouteMarkerKind.Decision).ToList()
            Dim revisions As Integer = decisions.Where(Function(item) IsRevision(item.Decision)).Count()
            Dim rejections As Integer = decisions.Where(Function(item) RouteAnalyticsService.OutcomeOf(item.Decision) = SubmissionOutcome.DeskRejected OrElse
                                                                       RouteAnalyticsService.OutcomeOf(item.Decision) = SubmissionOutcome.RejectedAfterReview OrElse
                                                                       RouteAnalyticsService.OutcomeOf(item.Decision) = SubmissionOutcome.Rejected).Count()
            Dim parts As New List(Of String) From {Plural(map.Journals.Count, "journal")}
            If revisions > 0 Then parts.Add(Plural(revisions, "revision"))
            If rejections > 0 Then parts.Add(Plural(rejections, "rejection"))
            If map.Ongoing Then parts.Add("in progress")
            Return String.Join("  ·  ", parts)
        End Function


        ' Routes lined up at day 0, shortest first (#82). Published routes
        ' by default; with unfinished ones, open stretches run to today.
        Public Shared Function Library(manuscripts As IEnumerable(Of Manuscript), today As DateTime, includeUnfinished As Boolean) As RouteMapLibrary

            Dim result As New RouteMapLibrary()
            For Each manuscript As Manuscript In If(manuscripts, Enumerable.Empty(Of Manuscript)())
                If manuscript Is Nothing Then Continue For
                Dim isPublished As Boolean = manuscript.Location = ManuscriptLocation.Published OrElse manuscript.CurrentStage = PaperStage.Published
                If Not isPublished AndAlso Not includeUnfinished Then Continue For
                Dim map As RouteMap = Build(manuscript, today)
                If map.IsEmpty Then Continue For
                result.Routes.Add(New RouteMapEntry With {.Manuscript = manuscript, .Map = map, .IsPublished = isPublished AndAlso map.Markers.Any(Function(item) item.Kind = RouteMarkerKind.Published)})
            Next
            result.Routes.Sort(Function(left, right) If(left.Map.TotalDays <> right.Map.TotalDays, left.Map.TotalDays.CompareTo(right.Map.TotalDays),
                                                         String.Compare(left.Manuscript.Title, right.Manuscript.Title, StringComparison.CurrentCultureIgnoreCase)))
            Return result

        End Function


        Private Shared Function DaysText(days As Integer) As String
            Return Plural(days, "day")
        End Function


        Private Shared Function Plural(count As Integer, noun As String) As String
            Return count.ToString(Globalization.CultureInfo.CurrentCulture) & " " & noun & If(count = 1, String.Empty, "s")
        End Function


        Public Shared Function KindName(kind As RouteSegmentKind) As String
            Select Case kind
                Case RouteSegmentKind.Journal : Return "with the journal"
                Case RouteSegmentKind.Author : Return "with you"
                Case RouteSegmentKind.Production : Return "in production"
                Case Else : Return "not recorded"
            End Select
        End Function

    End Class

End Namespace
