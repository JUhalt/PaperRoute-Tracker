Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Counts in plain words (#245): no "manuscript(s)", and no "1 days". A
' sweep of the app's source as DeleteQuestionTests reads it: every string
' literal outside comments is taken as text a person sees, with no
' allowlist, and the counts that used to miss the singular each choose it
' with If(n = 1, ...), the app's own form.
<TestClass>
Public Class CountWordingTests

    ' The counts that once read "1 days" and the like: each file holds an
    ' If whose condition ends in "= 1" and whose plural, the third
    ' argument, holds this literal.
    Private Shared ReadOnly KnownCounts As (Source As String, Plural As String)() = {
        ("Controls\RouteMapControls.vb", " days from first submission to "),
        ("Forms\ManuscriptRouteViewForm.vb", " days from first submission to "),
        ("Forms\CitationsUpdateForm.vb", " citations · h-index "),
        ("Forms\ReviewerResponseMatrixForm.vb", " comments"),
        ("Controls\DeadlineRow.vb", " required checklist items resolved"),
        ("Form1.vb", " rejected in last "),
        ("Form1.vb", "Waiting "),
        ("Form1.vb", " rejections; consider filing"),
        ("Form1.Insights.vb", " days drawn. "),
        ("Services\RouteAnalyticsService.vb", " days to a first decision"),
        ("Services\JournalFactsService.vb", " weeks from submission to publication"),
        ("Services\CitationsService.vb", " DOIs in OpenAlex ("),
        ("Forms\EditManuscriptForm.vb", "them."),
        ("Forms\SubmissionDetailsForm.vb", "NO   — Link to the files in their current location")
    }


    ' No literal counts with "(s)", "(es)" or "(ies)".
    <TestMethod>
    Public Sub NoStringCountsWithParenthesisS()

        Dim found As New List(Of String)()

        For Each source As String In DeleteQuestionTests.SourceFiles()
            For Each literal As String In DeleteQuestionTests.StringLiterals(File.ReadAllText(source))
                If Regex.IsMatch(literal, "\((?:e|ie)?s\)") Then
                    found.Add(Path.GetFileName(source) & ": """ & literal & """")
                End If
            Next
        Next

        Assert.AreEqual(0, found.Count, found.Count.ToString() & " literals count with (s):" & Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    <TestMethod>
    Public Sub TheKnownCountsChooseTheSingular()

        Dim app As String = Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")
        Dim codes As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim missing As New List(Of String)()

        For Each row In KnownCounts

            If Not codes.ContainsKey(row.Source) Then
                codes(row.Source) = DeleteQuestionTests.WithoutComments(File.ReadAllText(Path.Combine(app, row.Source)))
            End If

            If Not ChoosesTheSingular(codes(row.Source), row.Plural) Then
                missing.Add(row.Source & ": """ & row.Plural & """")
            End If

        Next

        Assert.AreEqual(0, missing.Count, missing.Count.ToString() & " counts do not choose the singular:" & Environment.NewLine & String.Join(Environment.NewLine, missing))

    End Sub


    ' A route submitted yesterday is one day long, not "1 days"; two days
    ' are still days.
    <TestMethod>
    Public Sub ARouteCountsItsDaysInPlainWords()

        Dim today As New DateTime(2026, 9, 28)

        Assert.AreEqual(
            "1 day from first submission to today: 1 day with the journal.",
            RouteSummary(today, today.AddDays(-1)))

        Assert.AreEqual(
            "2 days from first submission to today: 2 days with the journal.",
            RouteSummary(today, today.AddDays(-2)))

    End Sub


    Private Shared Function RouteSummary(today As DateTime, submitted As DateTime) As String

        Dim manuscript As New Manuscript With {
            .Title = "A route in review",
            .CurrentStage = PaperStage.UnderReview
        }

        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "Journal A", .SubmittedDate = submitted})

        Return RouteMapStyle.Summary(RouteMapService.Build(manuscript, today))

    End Function


    ' Whether some If( in the code has three top-level arguments, the
    ' first ending in "= 1" and the third holding the literal as written.
    Private Shared Function ChoosesTheSingular(code As String, plural As String) As Boolean

        Dim written As String = """" & plural.Replace("""", """""") & """"

        For Each opening As Match In Regex.Matches(code, "(?<![\w.])If\(")

            Dim arguments As List(Of String) =
                DeleteQuestionTests.TopLevelArguments(code, opening.Index + opening.Length)

            If arguments.Count = 3 AndAlso
               arguments(0).TrimEnd().EndsWith("= 1", StringComparison.Ordinal) AndAlso
               arguments(2).Contains(written, StringComparison.Ordinal) Then

                Return True

            End If

        Next

        Return False

    End Function

End Class
