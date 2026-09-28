Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Shareable reports (#30, #63): only what a supervisor or coauthor should
' see, escaped, and built without changing a record.
<TestClass>
Public Class ReportServiceTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    <TestMethod>
    Public Sub PipelineReportLeavesOutPrivateMaterial()
        Dim manuscript As Manuscript = Sensitive()
        Dim before As String = JsonSerializer.Serialize(manuscript)

        Dim html As String = ReportService.PipelineReport({manuscript}, Today, "Dr. Example")

        StringAssert.Contains(html, "Grit &amp; resilience &lt;script&gt;", "Titles are escaped.")
        Assert.IsFalse(html.Contains("<script>"))
        StringAssert.Contains(html, "Under review")
        StringAssert.Contains(html, "Assessment")
        StringAssert.Contains(html, "Prepared by Dr. Example")
        StringAssert.Contains(html, "Reminder", "A reminder is named only by its kind.")
        StringAssert.Contains(html, "Oct 2")
        For Each secret In {"SECRET-NOTE", "SECRET-CORRESPONDENCE", "SECRET-REVIEW", "SECRET-RESPONSE", "secret-folder", "SECRET-REMINDER", "SECRET-HISTORY", "SECRET-DECISION"}
            Assert.IsFalse(html.Contains(secret), secret & " must never appear in a shared report.")
        Next
        Assert.AreEqual(before, JsonSerializer.Serialize(manuscript), "Reports never change records.")

        Dim withoutDeadlines As String = ReportService.PipelineReport({manuscript}, Today, includeDeadlines:=False)
        Assert.IsFalse(withoutDeadlines.Contains("Next deadline"))
        Assert.IsFalse(withoutDeadlines.Contains("Prepared by"))
    End Sub

    <TestMethod>
    Public Sub RouteReportShowsEachSubmissionAndItsDefinitions()
        Dim manuscript As Manuscript = Sensitive()
        manuscript.Submissions.Insert(0, New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = Today.AddDays(-200)})
        manuscript.Submissions(0).Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = Today.AddDays(-190)})

        Dim html As String = ReportService.RouteReport(manuscript, Today)

        StringAssert.Contains(html, "First Journal")
        StringAssert.Contains(html, "Desk rejected")
        StringAssert.Contains(html, "10 days", "Days to the first decision.")
        StringAssert.Contains(html, "so far", "An open submission counts to the report date.")
        StringAssert.Contains(html, "How this is calculated")
        StringAssert.Contains(html, "Not recorded", "No acceptance: shown as not recorded, never estimated.")
        For Each secret In {"SECRET-NOTE", "SECRET-CORRESPONDENCE", "SECRET-REVIEW", "SECRET-RESPONSE", "secret-folder", "SECRET-DECISION"}
            Assert.IsFalse(html.Contains(secret), secret)
        Next
    End Sub

    ' Every private field filled with a marker the reports must not show.
    Private Shared Function Sensitive() As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Grit & resilience <script>",
            .CurrentStage = PaperStage.UnderReview,
            .Location = ManuscriptLocation.Pipeline,
            .TargetJournal = "Assessment",
            .StageEnteredDate = Today.AddDays(-12),
            .CoAuthors = "SECRET-NOTE coauthor@example.org",
            .ManuscriptUrl = "file:///C:/secret-folder/draft.docx"
        }
        manuscript.Metadata.AbstractText = "SECRET-NOTE abstract"
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = Today.AddDays(-12), .Note = "SECRET-HISTORY"})
        Dim submission As New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = Today.AddDays(-40), .Notes = "SECRET-NOTE", .PortalUrl = "https://portal.example/secret-folder"}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.None, .DecisionDate = Today.AddDays(-30), .Notes = "SECRET-DECISION"}
        submission.Decisions.Add(decision)
        submission.Correspondence.Add(New CorrespondenceItem With {.Title = "SECRET-CORRESPONDENCE", .LocalFilePath = "C:\secret-folder\letter.pdf"})
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .CommentText = "SECRET-REVIEW", .ResponseText = "SECRET-RESPONSE"})
        manuscript.Submissions.Add(submission)
        manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "SECRET-REMINDER", .DueDate = Today.AddDays(4), .Notes = "SECRET-NOTE"})
        manuscript.Versions.Add(New ManuscriptVersion With {.Label = "v1", .LocalFilePath = "C:\secret-folder\v1.docx", .Notes = "SECRET-NOTE"})
        Return manuscript
    End Function

End Class
