Imports System
Imports System.Text.RegularExpressions

Namespace Services

    Public NotInheritable Class OrcidIdentifierService

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
                    "^orcid\s*(?:iD)?\s*:\s*",
                    String.Empty,
                    RegexOptions.IgnoreCase Or
                    RegexOptions.CultureInvariant
                )

            value =
                Regex.Replace(
                    value,
                    "^https?://(?:www\.)?orcid\.org/",
                    String.Empty,
                    RegexOptions.IgnoreCase Or
                    RegexOptions.CultureInvariant
                )

            value =
                value.Trim().
                    TrimEnd(
                        "."c,
                        ","c,
                        ";"c,
                        "/"c
                    ).
                    ToUpperInvariant()

            Return value

        End Function


        Public Shared Function IsValid(
            rawValue As String
        ) As Boolean

            Dim value As String =
                Normalize(rawValue)

            If Not Regex.IsMatch(
                value,
                "^\d{4}-\d{4}-\d{4}-\d{3}[\dX]$",
                RegexOptions.CultureInvariant
            ) Then

                Return False

            End If

            Dim digits As String =
                value.Replace(
                    "-",
                    String.Empty
                )

            Dim total As Integer =
                0

            For index As Integer = 0 To 14

                Dim digit As Integer =
                    AscW(digits(index)) -
                    AscW("0"c)

                If digit < 0 OrElse digit > 9 Then
                    Return False
                End If

                total =
                    (total + digit) * 2

            Next

            Dim remainder As Integer =
                total Mod 11

            Dim result As Integer =
                (12 - remainder) Mod 11

            Dim expected As Char =
                If(
                    result = 10,
                    "X"c,
                    ChrW(
                        AscW("0"c) + result
                    )
                )

            Return digits(15) = expected

        End Function


        Public Shared Function NormalizeAndValidate(
            rawValue As String
        ) As String

            Dim normalized As String =
                Normalize(rawValue)

            If Not IsValid(normalized) Then

                Throw New ArgumentException(
                    "Please enter a valid ORCID iD, such as 0000-0002-1825-0097.",
                    NameOf(rawValue)
                )

            End If

            Return normalized

        End Function

    End Class

End Namespace
