Imports System
Imports System.Text.RegularExpressions

Namespace Services

    Public NotInheritable Class DoiNormalizer

        Private Sub New()
        End Sub


        Public Shared Function Normalize(
            rawValue As String
        ) As String

            If String.IsNullOrWhiteSpace(rawValue) Then
                Return String.Empty
            End If

            Dim value As String =
                rawValue.Trim()

            value =
                Regex.Replace(
                    value,
                    "^doi\s*:\s*",
                    String.Empty,
                    RegexOptions.IgnoreCase
                )

            value =
                Regex.Replace(
                    value,
                    "^https?://(?:dx\.)?doi\.org/",
                    String.Empty,
                    RegexOptions.IgnoreCase
                )

            Try

                value =
                    Uri.UnescapeDataString(
                        value
                    ).Trim()

            Catch

                value =
                    value.Trim()

            End Try

            While value.EndsWith(
                ".",
                StringComparison.Ordinal
            ) OrElse
                  value.EndsWith(
                ",",
                StringComparison.Ordinal
            ) OrElse
                  value.EndsWith(
                ";",
                StringComparison.Ordinal
            )

                value =
                    value.Substring(
                        0,
                        value.Length - 1
                    ).TrimEnd()

            End While

            Return value

        End Function


        Public Shared Function IsValid(
            rawValue As String
        ) As Boolean

            Dim value As String =
                Normalize(
                    rawValue
                )

            If String.IsNullOrWhiteSpace(value) Then
                Return False
            End If

            Return Regex.IsMatch(
                value,
                "^10\.\d{4,9}/\S+$",
                RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant
            )

        End Function

    End Class

End Namespace
