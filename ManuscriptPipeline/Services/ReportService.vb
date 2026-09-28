Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    ' Self-contained HTML reports (#30, #63) that open in any browser and
    ' print to PDF. They are built only from titles, stages, journals, dates,
    ' and decisions: notes, correspondence, reviewer comments and responses,
    ' file paths, and contact details are never read, so they cannot leak.
    Public NotInheritable Class ReportService

        Private Sub New()
        End Sub


        Public Shared Function StageName(stage As PaperStage) As String
            Select Case stage
                Case PaperStage.UnderReview : Return "Under review"
                Case PaperStage.InPress : Return "In press"
                Case Else : Return stage.ToString()
            End Select
        End Function


        Public Shared Function ShelfName(location As ManuscriptLocation) As String
            Select Case location
                Case ManuscriptLocation.FileDrawer : Return "File Drawer"
                Case ManuscriptLocation.Published : Return "Published"
                Case Else : Return "Pipeline"
            End Select
        End Function


        ' A pipeline status report for a supervisor, mentor, or coauthor.
        Public Shared Function PipelineReport(
            manuscripts As IEnumerable(Of Manuscript),
            today As DateTime,
            Optional preparedBy As String = "",
            Optional includeDeadlines As Boolean = True
        ) As String

            Dim chosen As List(Of Manuscript) = manuscripts.Where(Function(item) item IsNot Nothing).ToList()
            Dim deadlines As List(Of DeadlineItem) =
                If(includeDeadlines, DeadlineService.Build(chosen, today).Where(Function(item) item.DueDate.HasValue AndAlso item.Group <> DeadlineGroup.Done).ToList(), New List(Of DeadlineItem)())

            Dim html As New StringBuilder()
            OpenDocument(html, "Manuscript pipeline")
            html.Append("<header><h1>Manuscript pipeline</h1><p class=""meta"">")
            html.Append(Encode(today.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)))
            If Not String.IsNullOrWhiteSpace(preparedBy) Then html.Append(" &middot; Prepared by ").Append(Encode(preparedBy.Trim()))
            html.Append(" &middot; ").Append(chosen.Count.ToString(CultureInfo.CurrentCulture)).Append(If(chosen.Count = 1, " manuscript", " manuscripts"))
            html.Append("</p></header>")

            For Each location In {ManuscriptLocation.Pipeline, ManuscriptLocation.Published, ManuscriptLocation.FileDrawer}
                Dim shelf As List(Of Manuscript) = chosen.Where(Function(item) item.Location = location).
                    OrderBy(Function(item) item.CurrentStage).ThenBy(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).ToList()
                If shelf.Count = 0 Then Continue For

                html.Append("<h2>").Append(Encode(ShelfName(location))).Append("</h2>")
                html.Append("<table><thead><tr><th>Manuscript</th><th>Stage</th><th>Journal</th><th>Route</th>")
                If location = ManuscriptLocation.Pipeline Then html.Append("<th>In stage</th>")
                If includeDeadlines AndAlso location = ManuscriptLocation.Pipeline Then html.Append("<th>Next deadline</th>")
                html.Append("</tr></thead><tbody>")

                For Each manuscript As Manuscript In shelf
                    html.Append("<tr><td class=""title"">").Append(Encode(ReminderService.SafeManuscriptTitle(manuscript))).Append("</td>")
                    html.Append("<td>").Append(Encode(StageName(manuscript.CurrentStage))).Append("</td>")
                    html.Append("<td>").Append(Encode(If(manuscript.TargetJournal, String.Empty).Trim())).Append("</td>")
                    html.Append("<td>").Append(Encode(RouteSummaryService.Describe(manuscript).Text)).Append("</td>")
                    If location = ManuscriptLocation.Pipeline Then
                        Dim entered As DateTime = manuscript.StageEnteredDate.Date
                        html.Append("<td class=""num"">")
                        If entered > DateTime.MinValue.Date AndAlso entered <= today.Date Then html.Append(DayCount((today.Date - entered).Days))
                        html.Append("</td>")
                    End If
                    If includeDeadlines AndAlso location = ManuscriptLocation.Pipeline Then
                        Dim nextDue As DeadlineItem = deadlines.Where(Function(item) item.ManuscriptId = manuscript.Id).OrderBy(Function(item) item.DueDate.Value).FirstOrDefault()
                        html.Append("<td>")
                        If nextDue IsNot Nothing Then
                            html.Append(Encode(nextDue.DueDate.Value.ToString("MMM d", CultureInfo.CurrentCulture))).Append(" &middot; ").Append(Encode(DeadlineLabel(nextDue)))
                        End If
                        html.Append("</td>")
                    End If
                    html.Append("</tr>")
                Next

                html.Append("</tbody></table>")
            Next

            html.Append("<footer>From PaperRoute records on ").Append(Encode(today.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)))
            html.Append(". Notes, correspondence, reviewer comments, and files are not included.</footer>")
            CloseDocument(html)
            Return html.ToString()

        End Function


        ' Custom reminder titles are the user's own words, so a shared report
        ' names only the kind of deadline.
        Private Shared Function DeadlineLabel(item As DeadlineItem) As String
            Select Case item.Kind
                Case DeadlineKind.Revision : Return "Revision due"
                Case DeadlineKind.FollowUp : Return "Journal follow-up"
                Case Else : Return "Reminder"
            End Select
        End Function


        ' One manuscript's route: every submission, its decisions, and the
        ' durations between recorded dates, with the definitions used.
        Public Shared Function RouteReport(manuscript As Manuscript, today As DateTime) As String

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))

            Dim statistics As ManuscriptStatistics = RouteAnalyticsService.DescribeManuscript(manuscript, today)
            Dim html As New StringBuilder()
            Dim title As String = ReminderService.SafeManuscriptTitle(manuscript)

            OpenDocument(html, title & " route")
            html.Append("<header><h1>").Append(Encode(title)).Append("</h1><p class=""meta"">")
            html.Append(Encode(StageName(manuscript.CurrentStage))).Append(" &middot; ").Append(Encode(ShelfName(manuscript.Location)))
            html.Append(" &middot; Route as of ").Append(Encode(today.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture))).Append("</p></header>")

            html.Append("<dl class=""summary"">")
            Summary(html, "Journals", statistics.JournalCount.ToString(CultureInfo.CurrentCulture))
            Summary(html, "Revision rounds", statistics.RevisionRounds.ToString(CultureInfo.CurrentCulture))
            Summary(html, "First submitted", DateText(statistics.FirstSubmitted))
            Summary(html, "First submission to acceptance", If(statistics.DaysToAcceptance.HasValue, DayCount(statistics.DaysToAcceptance.Value), "Not recorded"))
            Summary(html, "First submission to publication", If(statistics.DaysToPublication.HasValue, DayCount(statistics.DaysToPublication.Value), "Not recorded"))
            html.Append("</dl>")

            If statistics.Submissions.Count = 0 Then
                html.Append("<p>No submissions are recorded yet.</p>")
            Else
                html.Append("<h2>Submissions</h2><table><thead><tr><th>#</th><th>Journal</th><th>Submitted</th><th>Decisions</th><th>First decision</th><th>Outcome</th><th>At the journal</th></tr></thead><tbody>")
                Dim number As Integer = 0
                For Each submission As SubmissionStatistics In statistics.Submissions
                    number += 1
                    Dim decisions As String = String.Join("; ",
                        If(submission.Submission.Decisions, New List(Of EditorialDecisionEvent)()).
                            Where(Function(item) item IsNot Nothing AndAlso item.Decision <> EditorialDecision.None).
                            OrderBy(Function(item) item.DecisionDate).
                            Select(Function(item) EditorialDecisionDisplayService.Format(item.Decision) & ", " & item.DecisionDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)))
                    html.Append("<tr><td class=""num"">").Append(number.ToString(CultureInfo.CurrentCulture)).Append("</td>")
                    html.Append("<td class=""title"">").Append(Encode(If(submission.JournalName.Length > 0, submission.JournalName, "(No journal recorded)"))).Append("</td>")
                    html.Append("<td>").Append(Encode(submission.SubmittedDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture))).Append("</td>")
                    html.Append("<td>").Append(Encode(If(decisions.Length > 0, decisions, "None recorded"))).Append("</td>")
                    html.Append("<td class=""num"">").Append(If(submission.DaysToFirstDecision.HasValue, DayCount(submission.DaysToFirstDecision.Value), "&mdash;")).Append("</td>")
                    html.Append("<td>").Append(Encode(OutcomeName(submission.Outcome))).Append("</td>")
                    html.Append("<td class=""num"">").Append(If(submission.DaysAtJournal.HasValue, DayCount(submission.DaysAtJournal.Value) & If(submission.Outcome = SubmissionOutcome.Open, " so far", ""), "&mdash;")).Append("</td></tr>")
                Next
                html.Append("</tbody></table>")
            End If

            html.Append("<footer><p><strong>How this is calculated.</strong> Days are calendar days between dates recorded in PaperRoute. ")
            html.Append("A first decision is the earliest decision recorded for a submission. An open submission counts to the report date. ")
            html.Append("Missing or inconsistent dates are shown as not recorded, never estimated. Dates are when events happened as entered, not when they were typed in.</p>")
            html.Append("<p>Notes, correspondence, reviewer comments, and files are not included.</p></footer>")
            CloseDocument(html)
            Return html.ToString()

        End Function


        Public Shared Function OutcomeName(outcome As SubmissionOutcome) As String
            Select Case outcome
                Case SubmissionOutcome.DeskRejected : Return "Desk rejected"
                Case SubmissionOutcome.RejectedAfterReview : Return "Rejected after review"
                Case SubmissionOutcome.Rejected : Return "Rejected"
                Case SubmissionOutcome.Accepted : Return "Accepted"
                Case SubmissionOutcome.Withdrawn : Return "Withdrawn"
                Case Else : Return "Open"
            End Select
        End Function


        Private Shared Sub Summary(html As StringBuilder, term As String, value As String)
            html.Append("<div><dt>").Append(Encode(term)).Append("</dt><dd>").Append(Encode(value)).Append("</dd></div>")
        End Sub


        Private Shared Function DateText(value As DateTime?) As String
            Return If(value.HasValue, value.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture), "Not recorded")
        End Function


        Private Shared Function DayCount(days As Integer) As String
            Return days.ToString(CultureInfo.CurrentCulture) & If(days = 1, " day", " days")
        End Function


        Private Shared Function Encode(value As String) As String
            Return WebUtility.HtmlEncode(If(value, String.Empty))
        End Function


        ' Print-friendly, and readable on screen in light or dark.
        Private Shared Sub OpenDocument(html As StringBuilder, title As String)
            html.Append("<!DOCTYPE html><html lang=""en""><head><meta charset=""utf-8"">")
            html.Append("<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">")
            html.Append("<meta name=""viewport"" content=""width=device-width, initial-scale=1"">")
            html.Append("<meta name=""generator"" content=""PaperRoute Tracker"">")
            html.Append("<title>").Append(Encode(title)).Append("</title><style>")
            ' Each color is given plainly first for older viewers, then as a
            ' variable that follows a dark system theme.
            html.Append(":root{--ink:#1d2b2f;--muted:#5b6b70;--line:#dae4e7;--accent:#0f766e;--ground:#ffffff;--head:#f3f7f8}")
            html.Append("@media (prefers-color-scheme:dark){:root{--ink:#e3eced;--muted:#a3b3b6;--line:#34464b;--accent:#5eead4;--ground:#162124;--head:#1d2b2f}}")
            html.Append("body{font-family:'Segoe UI',system-ui,sans-serif;color:#1d2b2f;color:var(--ink);background:#fff;background:var(--ground);margin:0 auto;max-width:1100px;padding:32px 24px;line-height:1.45}")
            html.Append("h1{font-size:1.6rem;margin:0 0 4px}h2{font-size:1.05rem;color:#0f766e;color:var(--accent);margin:28px 0 8px;letter-spacing:.02em}")
            html.Append(".meta{color:#5b6b70;color:var(--muted);margin:0}table{width:100%;border-collapse:collapse;font-size:.92rem}")
            html.Append("th{text-align:left;font-weight:600;color:#5b6b70;color:var(--muted);background:#f3f7f8;background:var(--head);padding:8px 10px;border-bottom:1px solid #dae4e7;border-bottom:1px solid var(--line)}")
            html.Append("td{padding:8px 10px;border-bottom:1px solid #dae4e7;border-bottom:1px solid var(--line);vertical-align:top}td.title{font-weight:600;width:36%}.num{text-align:right;white-space:nowrap}")
            html.Append(".summary{display:flex;flex-wrap:wrap;margin:20px 0 0}.summary div{border:1px solid #dae4e7;border:1px solid var(--line);border-radius:8px;padding:10px 14px;min-width:150px;margin:0 12px 12px 0}")
            html.Append("dt{color:#5b6b70;color:var(--muted);font-size:.85rem}dd{margin:2px 0 0;font-weight:600;font-size:1.1rem}")
            html.Append("footer{margin-top:28px;color:#5b6b70;color:var(--muted);font-size:.85rem}")
            html.Append("@media print{body{padding:0;max-width:none;color:#000;background:#fff}h2{page-break-after:avoid}tr{page-break-inside:avoid}th{background:#f2f2f2}}")
            html.Append("</style></head><body>")
        End Sub


        Private Shared Sub CloseDocument(html As StringBuilder)
            html.Append("</body></html>")
        End Sub

    End Class

End Namespace
