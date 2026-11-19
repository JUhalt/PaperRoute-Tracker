Imports System

Namespace Models

    Public Class ManuscriptExternalLink

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Label As String = String.Empty

        Public Property Url As String = String.Empty

        Public Property Notes As String = String.Empty


        Public Overrides Function ToString() As String

            If String.IsNullOrWhiteSpace(Label) Then
                Return Url
            End If

            Return Label.Trim() &
                " — " &
                If(
                    Url,
                    String.Empty
                ).Trim()

        End Function

    End Class

End Namespace
