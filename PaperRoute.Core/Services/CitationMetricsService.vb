Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' Researcher-level figures (#91), computed on this computer from the
    ' works the researcher confirmed. Each is shown on its own, with its
    ' definition, and never combined into a score.
    Public NotInheritable Class CitationSummary

        Public Property Works As Integer
        Public Property Citations As Long
        Public Property HIndex As Integer
        Public Property I10Index As Integer
        Public Property GIndex As Integer
        ' Nothing when no publication year is known.
        Public Property MQuotient As Double?
        Public Property FirstYear As Integer?

    End Class


    Public NotInheritable Class CitationMetricsService

        Private Sub New()
        End Sub

        Public Const HDefinition As String = "h-index: you have h works cited at least h times each."
        Public Const I10Definition As String = "i10-index: the number of your works cited at least 10 times."
        Public Const GDefinition As String = "g-index: your most-cited g works together have at least g² citations (g is at most the number of works)."
        Public Const MDefinition As String = "m-quotient: your h-index divided by the years since your first publication, counting the first year."
        Public Const FwciDefinition As String = "Field-weighted citation impact (FWCI) compares a work's citations with similar work of the same type, year, and field; 1.0 is average. It counts the year of publication and the next three, so it is provisional for recent work."
        Public Const DatabasesNote As String = "Citation counts differ between databases: OpenAlex, Scopus, Web of Science, and Google Scholar will not agree. They count citations; they do not measure the quality of the work."
        Public Const PerYearNote As String = "OpenAlex counts citations by year from 2012, so the yearly counts may not add up to the total."


        Public Shared Function Summarize(works As IEnumerable(Of CitedWork), retrievalYear As Integer) As CitationSummary

            Dim list As List(Of CitedWork) = If(works, Enumerable.Empty(Of CitedWork)()).Where(Function(item) item IsNot Nothing).ToList()
            Dim counts As List(Of Integer) = list.Select(Function(item) Math.Max(0, item.CitedByCount)).OrderByDescending(Function(item) item).ToList()

            Dim h As Integer = 0
            While h < counts.Count AndAlso counts(h) >= h + 1
                h += 1
            End While

            ' The largest g ≤ n whose top g works have at least g² citations.
            Dim g As Integer = 0
            Dim running As Long = 0
            For index As Integer = 0 To counts.Count - 1
                running += counts(index)
                If running >= CLng(index + 1) * (index + 1) Then g = index + 1
            Next

            Dim years As List(Of Integer) = list.
                Where(Function(item) item.Year.HasValue AndAlso item.Year.Value >= 1900 AndAlso item.Year.Value <= retrievalYear).
                Select(Function(item) item.Year.Value).
                ToList()
            Dim first As Integer? = If(years.Count = 0, CType(Nothing, Integer?), years.Min())

            Return New CitationSummary With {
                .Works = list.Count,
                .Citations = counts.Sum(Function(item) CLng(item)),
                .HIndex = h,
                .I10Index = counts.Where(Function(item) item >= 10).Count(),
                .GIndex = g,
                .FirstYear = first,
                .MQuotient = If(first.HasValue, Math.Round(h / CDbl(retrievalYear - first.Value + 1), 2), CType(Nothing, Double?))
            }

        End Function


        ' The ten calendar years ending with the retrieval year, with zeros
        ' for years without citations.
        Public Shared Function PerYear(works As IEnumerable(Of CitedWork), retrievalYear As Integer) As List(Of YearCount)
            Dim totals As New Dictionary(Of Integer, Integer)()
            For Each work As CitedWork In If(works, Enumerable.Empty(Of CitedWork)()).Where(Function(item) item IsNot Nothing)
                For Each entry As YearCount In If(work.CountsByYear, New List(Of YearCount)()).Where(Function(item) item IsNot Nothing)
                    Dim count As Integer = 0
                    totals.TryGetValue(entry.Year, count)
                    totals(entry.Year) = count + Math.Max(0, entry.Count)
                Next
            Next
            Return Enumerable.Range(retrievalYear - 9, 10).
                Select(Function(year) New YearCount With {.Year = year, .Count = If(totals.ContainsKey(year), totals(year), 0)}).
                ToList()
        End Function

    End Class

End Namespace
