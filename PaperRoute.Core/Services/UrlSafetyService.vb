Imports System
Imports System.Diagnostics

Namespace Services

    Public NotInheritable Class UrlSafetyService

        Private Sub New()
        End Sub


        Public Shared Function IsSafeHttpUrl(
            value As String
        ) As Boolean

            If String.IsNullOrWhiteSpace(value) Then
                Return False
            End If

            Dim uri As Uri = Nothing

            If Not Uri.TryCreate(
                value.Trim(),
                UriKind.Absolute,
                uri
            ) OrElse
               uri Is Nothing Then

                Return False
            End If

            Return (
                String.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase
                ) OrElse
                String.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase
                )
            )

        End Function


        Public Shared Function NormalizeOptionalHttpUrl(
            value As String,
            fieldName As String
        ) As String

            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Dim trimmed As String = value.Trim()

            If Not IsSafeHttpUrl(trimmed) Then

                Throw New ArgumentException(
                    If(
                        String.IsNullOrWhiteSpace(fieldName),
                        "The URL",
                        fieldName
                    ) &
                    " must be a valid http:// or https:// address."
                )

            End If

            Return trimmed

        End Function


        Public Shared Sub OpenInBrowser(
            value As String
        )

            If Not IsSafeHttpUrl(value) Then

                Throw New ArgumentException(
                    "Only valid http:// and https:// links can be opened."
                )

            End If

            Process.Start(
                New ProcessStartInfo With {
                    .FileName = value.Trim(),
                    .UseShellExecute = True
                }
            )

        End Sub

    End Class

End Namespace
