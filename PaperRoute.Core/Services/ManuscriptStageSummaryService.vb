Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptStageSummaryService

        Private Sub New()
        End Sub


        Public Shared Function CountByStage(
            manuscripts As IEnumerable(Of Manuscript)
        ) As Dictionary(Of PaperStage, Integer)

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            Dim counts As New Dictionary(Of PaperStage, Integer)()

            For Each stage As PaperStage In [Enum].GetValues(Of PaperStage)()
                counts(stage) = 0
            Next

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing Then
                    Continue For
                End If

                counts(manuscript.CurrentStage) += 1

            Next

            Return counts

        End Function

    End Class

End Namespace
