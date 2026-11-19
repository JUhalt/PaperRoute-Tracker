Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class BibliographyTextService

        Private Sub New()
        End Sub

        Public Shared Function CollapseWhitespace(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return Regex.Replace(value.Trim(), "\s+", " ")
        End Function

        Public Shared Function SplitKeywords(value As String) As List(Of String)
            If String.IsNullOrWhiteSpace(value) Then
                Return New List(Of String)()
            End If

            Return value.
                Split(
                    New Char() {","c, ";"c},
                    StringSplitOptions.RemoveEmptyEntries
                ).
                Select(Function(item) item.Trim()).
                Where(Function(item) item.Length > 0).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
        End Function

        Public Shared Function ParsePersonName(
            rawName As String,
            warnings As List(Of String)
        ) As BibliographyAuthor

            Dim value As String = CollapseWhitespace(rawName)
            Dim author As New BibliographyAuthor()

            If String.IsNullOrWhiteSpace(value) Then
                Return author
            End If

            If value.StartsWith("{", StringComparison.Ordinal) AndAlso
               value.EndsWith("}", StringComparison.Ordinal) AndAlso
               value.Length > 2 Then

                author.DisplayNameOverride =
                    value.Substring(1, value.Length - 2).Trim()

                Return author
            End If

            Dim commaParts As String() =
                value.Split(","c).
                    Select(Function(item) item.Trim()).
                    ToArray()

            If commaParts.Length = 2 Then
                author.FamilyName = commaParts(0)
                FillGivenAndMiddle(author, commaParts(1))
                Return author
            End If

            If commaParts.Length = 3 Then
                author.FamilyName = commaParts(0)
                author.Suffix = commaParts(1)
                FillGivenAndMiddle(author, commaParts(2))
                Return author
            End If

            If commaParts.Length > 3 Then
                author.DisplayNameOverride = value

                If warnings IsNot Nothing Then
                    warnings.Add(
                        "Author name '" & value &
                        "' could not be split confidently and was preserved as a display name."
                    )
                End If

                Return author
            End If

            Dim tokens As String() =
                value.Split(
                    New Char() {" "c},
                    StringSplitOptions.RemoveEmptyEntries
                )

            If tokens.Length = 1 Then
                author.DisplayNameOverride = value

                If warnings IsNot Nothing Then
                    warnings.Add(
                        "Single-token author name '" & value &
                        "' was preserved as a display name."
                    )
                End If

                Return author
            End If

            author.GivenName = tokens(0)
            author.FamilyName = tokens(tokens.Length - 1)

            If tokens.Length > 2 Then
                author.MiddleName =
                    String.Join(" ", tokens.Skip(1).Take(tokens.Length - 2))
            End If

            Return author
        End Function

        Private Shared Sub FillGivenAndMiddle(
            author As BibliographyAuthor,
            givenText As String
        )
            Dim tokens As String() =
                CollapseWhitespace(givenText).
                    Split(
                        New Char() {" "c},
                        StringSplitOptions.RemoveEmptyEntries
                    )

            If tokens.Length = 0 Then
                Return
            End If

            author.GivenName = tokens(0)

            If tokens.Length > 1 Then
                author.MiddleName = String.Join(" ", tokens.Skip(1))
            End If
        End Sub

        Public Shared Function ParseDate(rawValue As String) As DateTime?
            If String.IsNullOrWhiteSpace(rawValue) Then
                Return Nothing
            End If

            Dim value As String =
                rawValue.Trim().TrimEnd("/"c, "."c, ";"c)

            Dim formats As String() = {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "yyyy-MM",
                "yyyy/MM",
                "yyyy"
            }

            For Each format As String In formats
                Dim parsed As DateTime

                If DateTime.TryParseExact(
                    value,
                    format,
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None,
                    parsed
                ) Then
                    Return parsed
                End If
            Next

            Dim general As DateTime

            If DateTime.TryParse(
                value,
                Globalization.CultureInfo.InvariantCulture,
                Globalization.DateTimeStyles.AllowWhiteSpaces,
                general
            ) Then
                Return general
            End If

            Return Nothing
        End Function

        Public Shared Function MonthNumber(value As String) As Integer
            If String.IsNullOrWhiteSpace(value) Then
                Return 1
            End If

            Dim trimmed As String =
                value.Trim().Trim("{"c, "}"c, """"c)

            Dim numeric As Integer

            If Integer.TryParse(trimmed, numeric) AndAlso
               numeric >= 1 AndAlso numeric <= 12 Then
                Return numeric
            End If

            Dim names As String() = {
                "jan", "feb", "mar", "apr", "may", "jun",
                "jul", "aug", "sep", "oct", "nov", "dec"
            }

            Dim lower As String = trimmed.ToLowerInvariant()

            For index As Integer = 0 To names.Length - 1
                If lower.StartsWith(names(index), StringComparison.Ordinal) Then
                    Return index + 1
                End If
            Next

            Return 1
        End Function

        Public Shared Function NormalizeName(value As String) As String
            Return CollapseWhitespace(value).ToLowerInvariant()
        End Function

    End Class

End Namespace
