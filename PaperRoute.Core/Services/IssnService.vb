Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text

Namespace Services

    ' ISSNs as the open indexes need them (#87): "NNNN-NNNC", hyphen included
    ' and an uppercase X, with the check digit verified. DOAJ matches only
    ' that exact form, and nothing invalid is ever sent.
    Public NotInheritable Class IssnService

        Private Sub New()
        End Sub

        Public Const MaximumPerJournal As Integer = 4


        ' The normalized ISSN, or "" when the value isn't a valid one.
        Public Shared Function Normalize(value As String) As String

            If String.IsNullOrWhiteSpace(value) Then Return String.Empty

            Dim text As String = value.Trim().ToUpperInvariant()
            If text.StartsWith("ISSN", StringComparison.Ordinal) Then text = text.Substring(4).TrimStart(":"c, " "c)

            Dim compact As New StringBuilder()
            For Each character As Char In text
                If character = "-"c OrElse character = " "c Then Continue For
                compact.Append(character)
            Next

            Dim digits As String = compact.ToString()
            If digits.Length <> 8 Then Return String.Empty

            Dim sum As Integer = 0
            For index As Integer = 0 To 6
                Dim character As Char = digits(index)
                If character < "0"c OrElse character > "9"c Then Return String.Empty
                sum += (8 - index) * (AscW(character) - AscW("0"c))
            Next

            Dim remainder As Integer = (11 - sum Mod 11) Mod 11
            Dim expected As Char = If(remainder = 10, "X"c, ChrW(AscW("0"c) + remainder))
            If digits(7) <> expected Then Return String.Empty
            If digits = "00000000" Then Return String.Empty

            Return digits.Substring(0, 4) & "-" & digits.Substring(4)

        End Function


        Public Shared Function IsValid(value As String) As Boolean
            Return Normalize(value).Length > 0
        End Function


        ' Valid ISSNs once each, in order, at most four; invalid ones dropped.
        Public Shared Function NormalizeList(values As IEnumerable(Of String)) As List(Of String)
            Return If(values, Enumerable.Empty(Of String)()).
                Select(AddressOf Normalize).
                Where(Function(item) item.Length > 0).
                Distinct(StringComparer.Ordinal).
                Take(MaximumPerJournal).
                ToList()
        End Function


        ' A typed list, separated by commas, semicolons, spaces, or lines.
        ' Returns the valid ISSNs and the entries that aren't ISSNs.
        Public Shared Function ParseList(text As String) As (Valid As List(Of String), Invalid As List(Of String))

            Dim valid As New List(Of String)()
            Dim invalid As New List(Of String)()

            For Each part As String In If(text, String.Empty).Split({","c, ";"c, " "c, ControlChars.Tab, ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
                Dim token As String = part.Trim()
                If token.Length = 0 OrElse String.Equals(token, "ISSN", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim normalized As String = Normalize(token)
                If normalized.Length = 0 Then
                    invalid.Add(token)
                ElseIf Not valid.Contains(normalized) Then
                    valid.Add(normalized)
                End If
            Next

            Return (valid, invalid)

        End Function

    End Class

End Namespace
