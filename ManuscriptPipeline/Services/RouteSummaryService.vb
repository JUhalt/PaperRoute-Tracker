Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum RouteDotState
        ' A submission that ended in a rejection or withdrawal.
        Closed
        ' The latest submission, still in progress.
        Current
        ' The latest submission, accepted.
        Accepted
    End Enum

    Public NotInheritable Class RouteSummary

        Public Sub New(dots As IReadOnlyList(Of RouteDotState), text As String)
            Me.Dots = dots
            Me.Text = text
        End Sub

        ' One dot per recorded submission, oldest first, at most MaximumDots.
        Public ReadOnly Property Dots As IReadOnlyList(Of RouteDotState)

        Public ReadOnly Property Text As String

    End Class

    ' Summarizes a manuscript's journey through journals for a board card,
    ' from recorded submissions and decisions only. Nothing is inferred from
    ' the manuscript's stage.
    Public NotInheritable Class RouteSummaryService

        Public Const MaximumDots As Integer = 6

        Private Sub New()
        End Sub

        Public Shared Function Describe(manuscript As Manuscript) As RouteSummary

            Dim submissions As List(Of JournalSubmission) =
                If(manuscript?.Submissions, New List(Of JournalSubmission)()).
                    Where(Function(item) item IsNot Nothing).
                    OrderBy(Function(item) item.SubmittedDate).
                    ToList()

            If submissions.Count = 0 Then
                Return New RouteSummary(
                    Array.Empty(Of RouteDotState)(),
                    If(manuscript IsNot Nothing AndAlso manuscript.Location = ManuscriptLocation.Pipeline,
                        "Not yet submitted",
                        "No submissions recorded"))
            End If

            Dim dots As New List(Of RouteDotState)
            Dim rejections As Integer = 0

            For index As Integer = 0 To submissions.Count - 1
                Dim latest As EditorialDecisionEvent = ManuscriptAttentionService.GetLatestDecision(submissions(index))
                Dim decision As EditorialDecision = If(latest Is Nothing, EditorialDecision.None, latest.Decision)

                If ManuscriptAttentionService.IsRejectionDecision(decision) Then
                    rejections += 1
                End If

                If ManuscriptAttentionService.IsRejectionDecision(decision) OrElse decision = EditorialDecision.Withdrawn Then
                    dots.Add(RouteDotState.Closed)
                ElseIf index < submissions.Count - 1 Then
                    ' An earlier submission without a closing decision is still history.
                    dots.Add(RouteDotState.Closed)
                ElseIf decision = EditorialDecision.Accepted Then
                    dots.Add(RouteDotState.Accepted)
                Else
                    dots.Add(RouteDotState.Current)
                End If
            Next

            Dim lastDecisionEvent As EditorialDecisionEvent = ManuscriptAttentionService.GetLatestDecision(submissions.Last())
            Dim lastDecision As EditorialDecision = If(lastDecisionEvent Is Nothing, EditorialDecision.None, lastDecisionEvent.Decision)
            Dim routeOpen As Boolean = dots.Last() <> RouteDotState.Closed

            Dim parts As New List(Of String)

            If routeOpen Then
                parts.Add(Ordinal(submissions.Count) & " journal")
                If lastDecision <> EditorialDecision.None Then
                    parts.Add(Lower(EditorialDecisionDisplayService.Format(lastDecision)))
                ElseIf rejections > 0 Then
                    parts.Add(Count(rejections, "rejection"))
                End If
            Else
                parts.Add(Count(submissions.Count, "journal"))
                parts.Add(If(rejections > 0, Count(rejections, "rejection"), "withdrawn"))
            End If

            Return New RouteSummary(
                dots.Skip(Math.Max(0, dots.Count - MaximumDots)).ToList(),
                String.Join(" " & ChrW(&HB7) & " ", parts))

        End Function

        Private Shared Function Ordinal(value As Integer) As String
            Dim suffix As String
            If value Mod 100 >= 11 AndAlso value Mod 100 <= 13 Then
                suffix = "th"
            Else
                Select Case value Mod 10
                    Case 1 : suffix = "st"
                    Case 2 : suffix = "nd"
                    Case 3 : suffix = "rd"
                    Case Else : suffix = "th"
                End Select
            End If
            Return value.ToString() & suffix
        End Function

        Private Shared Function Count(value As Integer, noun As String) As String
            Return value.ToString() & " " & noun & If(value = 1, "", "s")
        End Function

        Private Shared Function Lower(text As String) As String
            If String.IsNullOrEmpty(text) Then Return text
            Return Char.ToLowerInvariant(text(0)) & text.Substring(1)
        End Function

    End Class

End Namespace
