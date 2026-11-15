Imports System
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' Describes how long an active manuscript has been in its current stage,
    ' from recorded dates only. When no stage change has been recorded since
    ' the manuscript was added or imported, the date is when PaperRoute got
    ' the record, so the text says "added" rather than implying an observed
    ' stage change.
    Public NotInheritable Class StageClockService

        Private Sub New()
        End Sub

        Public Shared Function Describe(
            manuscript As Manuscript,
            today As DateTime
        ) As String

            If manuscript Is Nothing OrElse
               manuscript.Location <> ManuscriptLocation.Pipeline Then
                Return String.Empty
            End If

            Dim entered As DateTime = manuscript.StageEnteredDate.Date

            If entered > today.Date OrElse entered = DateTime.MinValue.Date Then
                Return String.Empty
            End If

            Dim days As Integer = (today.Date - entered).Days
            Dim stageChanged As Boolean =
                manuscript.History IsNot Nothing AndAlso
                manuscript.History.Where(Function(item) item IsNot Nothing).Count() > 1

            If stageChanged Then
                Return If(days = 0, "since today", DayCount(days))
            End If

            Return If(days = 0, "added today", "added " & DayCount(days) & " ago")

        End Function

        Private Shared Function DayCount(days As Integer) As String

            Return days.ToString() & If(days = 1, " day", " days")

        End Function

    End Class

End Namespace
