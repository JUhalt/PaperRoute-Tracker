Imports System

Namespace Models

    Public Class OrcidWorkSuggestion

        Public Property PutCode As Long

        Public Property Title As String =
            String.Empty

        Public Property WorkType As String =
            String.Empty

        Public Property Doi As String =
            String.Empty

        Public Property JournalTitle As String =
            String.Empty

        Public Property PublishedDate As DateTime? =
            Nothing


        Public ReadOnly Property DisplayName As String
            Get

                Dim yearText As String =
                    If(
                        PublishedDate.HasValue,
                        PublishedDate.Value.Year.ToString(),
                        "n.d."
                    )

                Dim result As String =
                    yearText & " — " & Title

                If Not String.IsNullOrWhiteSpace(Doi) Then
                    result &= "  [" & Doi & "]"
                End If

                Return result

            End Get
        End Property


        Public Overrides Function ToString() As String
            Return DisplayName
        End Function

    End Class

End Namespace
