Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class RisService

        Private Shared ReadOnly KnownTags As New HashSet(Of String)(
            StringComparer.OrdinalIgnoreCase
        ) From {
            "TY",
            "ER",
            "TI",
            "T1",
            "AU",
            "A1",
            "JO",
            "JF",
            "JA",
            "T2",
            "PY",
            "Y1",
            "DA",
            "VL",
            "IS",
            "SP",
            "EP",
            "PB",
            "DO",
            "UR",
            "AB",
            "N2",
            "KW"
        }

        Private Sub New()
        End Sub

        Public Shared Function Parse(text As String) As BibliographyParseResult
            Dim result As New BibliographyParseResult With {
                .Format = BibliographyFormat.Ris
            }

            If String.IsNullOrWhiteSpace(text) Then
                result.FileWarnings.Add("The RIS file was empty.")
                Return result
            End If

            Dim current As Dictionary(Of String, List(Of String)) = Nothing
            Dim lastTag As String = String.Empty
            Dim lineNumber As Integer = 0

            For Each rawLine As String In
                NormalizeLineEndings(text).Split(ControlChars.Lf)

                lineNumber += 1

                Dim line As String =
                    rawLine.TrimEnd(ControlChars.Cr)

                If String.IsNullOrWhiteSpace(line) Then
                    Continue For
                End If

                Dim match As Match =
                    Regex.Match(
                        line,
                        "^([A-Za-z0-9]{2})\s{2}-\s?(.*)$"
                    )

                If match.Success Then
                    Dim tag As String =
                        match.Groups(1).Value.ToUpperInvariant()

                    Dim value As String =
                        match.Groups(2).Value.Trim()

                    If String.Equals(tag, "TY", StringComparison.Ordinal) Then
                        If current IsNot Nothing Then
                            result.FileWarnings.Add(
                                "A new RIS TY record began before the previous record ended; the previous record was closed automatically."
                            )
                            result.Records.Add(MapRecord(current))
                        End If

                        current =
                            New Dictionary(Of String, List(Of String))(
                                StringComparer.OrdinalIgnoreCase
                            )
                    End If

                    If current Is Nothing Then
                        current =
                            New Dictionary(Of String, List(Of String))(
                                StringComparer.OrdinalIgnoreCase
                            )
                        result.FileWarnings.Add(
                            "RIS data before the first TY tag was accepted as a record."
                        )
                    End If

                    AddTagValue(current, tag, value)
                    lastTag = tag

                    If String.Equals(tag, "ER", StringComparison.Ordinal) Then
                        result.Records.Add(MapRecord(current))
                        current = Nothing
                        lastTag = String.Empty
                    End If

                    Continue For
                End If

                If current IsNot Nothing AndAlso
                   Not String.IsNullOrWhiteSpace(lastTag) Then

                    Dim values As List(Of String) = current(lastTag)

                    If values.Count > 0 Then
                        values(values.Count - 1) =
                            values(values.Count - 1) &
                            " " &
                            line.Trim()
                    End If
                Else
                    result.FileWarnings.Add(
                        "Unrecognized RIS line " &
                        lineNumber.ToString() &
                        " was ignored."
                    )
                End If
            Next

            If current IsNot Nothing Then
                result.FileWarnings.Add(
                    "The final RIS record did not contain ER and was closed automatically."
                )
                result.Records.Add(MapRecord(current))
            End If

            If result.Records.Count = 0 Then
                result.FileWarnings.Add("No RIS bibliography records were found.")
            End If

            Return result
        End Function

        Public Shared Function Export(
            manuscripts As IEnumerable(Of Manuscript),
            authorLibrary As AuthorLibraryData
        ) As String

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            Dim builder As New StringBuilder()

            For Each manuscript As Manuscript In manuscripts
                If manuscript Is Nothing OrElse
                   String.IsNullOrWhiteSpace(manuscript.Title) Then
                    Continue For
                End If

                Dim recordType As String =
                    WorkTypeService.ToRis(
                        manuscript.WorkType,
                        If(
                            manuscript.Metadata IsNot Nothing AndAlso
                            Not String.IsNullOrWhiteSpace(
                                manuscript.Metadata.PublicationJournal
                            ),
                            "JOUR",
                            "GEN"
                        )
                    )

                AddLine(builder, "TY", recordType)
                AddLine(builder, "TI", manuscript.Title)

                For Each authorText As String In
                    ExportAuthors(manuscript, authorLibrary)
                    AddLine(builder, "AU", authorText)
                Next

                If manuscript.Metadata IsNot Nothing Then
                    AddLine(builder, "JO", manuscript.Metadata.PublicationJournal)

                    If manuscript.Metadata.PublishedDate.HasValue Then
                        AddLine(
                            builder,
                            "PY",
                            manuscript.Metadata.PublishedDate.Value.Year.ToString()
                        )
                        AddLine(
                            builder,
                            "DA",
                            manuscript.Metadata.PublishedDate.Value.ToString(
                                "yyyy/MM/dd"
                            )
                        )
                    End If

                    AddLine(builder, "VL", manuscript.Metadata.Volume)
                    AddLine(builder, "IS", manuscript.Metadata.Issue)

                    Dim startPage As String = String.Empty
                    Dim endPage As String = String.Empty

                    SplitPages(
                        manuscript.Metadata.Pages,
                        startPage,
                        endPage
                    )

                    AddLine(builder, "SP", startPage)
                    AddLine(builder, "EP", endPage)
                    AddLine(builder, "PB", manuscript.Metadata.Publisher)
                    AddLine(
                        builder,
                        "DO",
                        DoiNormalizer.Normalize(manuscript.Metadata.Doi)
                    )
                    AddLine(builder, "UR", manuscript.Metadata.PublicationUrl)
                    AddLine(builder, "AB", manuscript.Metadata.AbstractText)

                    If manuscript.Metadata.Keywords IsNot Nothing Then
                        For Each keyword As String In manuscript.Metadata.Keywords
                            AddLine(builder, "KW", keyword)
                        Next
                    End If
                End If

                AddLine(builder, "ER", String.Empty, allowBlank:=True)
                builder.AppendLine()
            Next

            Return builder.ToString()
        End Function

        Private Shared Function MapRecord(
            fields As Dictionary(Of String, List(Of String))
        ) As BibliographyRecord

            Dim record As New BibliographyRecord With {
                .SourceFormat = BibliographyFormat.Ris,
                .SourceType = FirstValue(fields, "TY")
            }

            record.Title =
                FirstNonBlank(
                    FirstValue(fields, "TI"),
                    FirstValue(fields, "T1")
                )

            record.Journal =
                FirstNonBlank(
                    FirstValue(fields, "JO"),
                    FirstValue(fields, "JF"),
                    FirstValue(fields, "JA"),
                    FirstValue(fields, "T2")
                )

            record.Volume = FirstValue(fields, "VL")
            record.Issue = FirstValue(fields, "IS")

            Dim startPage As String = FirstValue(fields, "SP")
            Dim endPage As String = FirstValue(fields, "EP")

            If Not String.IsNullOrWhiteSpace(startPage) AndAlso
               Not String.IsNullOrWhiteSpace(endPage) Then
                record.Pages = startPage & "-" & endPage
            Else
                record.Pages = FirstNonBlank(startPage, endPage)
            End If

            record.Publisher = FirstValue(fields, "PB")
            record.Doi =
                DoiNormalizer.Normalize(
                    FirstValue(fields, "DO")
                )
            record.Url = FirstValue(fields, "UR")
            record.AbstractText =
                FirstNonBlank(
                    FirstValue(fields, "AB"),
                    FirstValue(fields, "N2")
                )

            record.Keywords =
                ValuesOf(fields, "KW").
                    Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                    Select(Function(item) item.Trim()).
                    Distinct(StringComparer.OrdinalIgnoreCase).
                    ToList()

            Dim authorValues As New List(Of String)()
            authorValues.AddRange(ValuesOf(fields, "AU"))
            authorValues.AddRange(ValuesOf(fields, "A1"))

            For Each authorValue As String In authorValues
                Dim parsed As BibliographyAuthor =
                    BibliographyTextService.ParsePersonName(
                        authorValue,
                        record.Warnings
                    )

                If Not String.IsNullOrWhiteSpace(parsed.DisplayName) Then
                    record.Authors.Add(parsed)
                End If
            Next

            record.PublishedDate =
                FirstParsedDate(
                    FirstValue(fields, "DA"),
                    FirstValue(fields, "Y1"),
                    FirstValue(fields, "PY")
                )

            For Each pair As KeyValuePair(Of String, List(Of String)) In fields
                If KnownTags.Contains(pair.Key) Then
                    Continue For
                End If

                record.UnmappedFields(pair.Key) =
                    String.Join(" | ", pair.Value)

                If String.Equals(
                    pair.Key,
                    "AD",
                    StringComparison.OrdinalIgnoreCase
                ) Then
                    record.Warnings.Add(
                        "RIS AD affiliation/address data was not assigned because RIS does not reliably identify which address belongs to which author."
                    )
                Else
                    record.Warnings.Add(
                        "Unsupported RIS tag '" & pair.Key &
                        "' was not mapped into PaperRoute."
                    )
                End If
            Next

            If fields.ContainsKey("AD") AndAlso
               Not record.UnmappedFields.ContainsKey("AD") Then
                record.UnmappedFields("AD") =
                    String.Join(" | ", fields("AD"))
                record.Warnings.Add(
                    "RIS AD affiliation/address data was not assigned because RIS does not reliably identify which address belongs to which author."
                )
            End If

            If String.IsNullOrWhiteSpace(record.Title) Then
                record.Warnings.Add("This record has no title.")
            End If

            If Not IsCommonRisType(record.SourceType) Then
                record.Warnings.Add(
                    "RIS type '" & record.SourceType &
                    "' is not modeled directly by PaperRoute; common metadata will still be imported."
                )
            End If

            Return record
        End Function

        Private Shared Sub AddTagValue(
            fields As Dictionary(Of String, List(Of String)),
            tag As String,
            value As String
        )
            Dim values As List(Of String) = Nothing

            If Not fields.TryGetValue(tag, values) Then
                values = New List(Of String)()
                fields(tag) = values
            End If

            values.Add(value)
        End Sub

        Private Shared Function FirstValue(
            fields As Dictionary(Of String, List(Of String)),
            tag As String
        ) As String
            Dim values As List(Of String) = Nothing

            If fields.TryGetValue(tag, values) AndAlso values.Count > 0 Then
                Return values(0).Trim()
            End If

            Return String.Empty
        End Function

        Private Shared Function ValuesOf(
            fields As Dictionary(Of String, List(Of String)),
            tag As String
        ) As List(Of String)
            Dim values As List(Of String) = Nothing

            If fields.TryGetValue(tag, values) Then
                Return values
            End If

            Return New List(Of String)()
        End Function

        Private Shared Function FirstNonBlank(
            ParamArray values As String()
        ) As String
            For Each value As String In values
                If Not String.IsNullOrWhiteSpace(value) Then
                    Return value.Trim()
                End If
            Next

            Return String.Empty
        End Function

        Private Shared Function FirstParsedDate(
            ParamArray values As String()
        ) As DateTime?
            For Each value As String In values
                Dim parsed As DateTime? =
                    BibliographyTextService.ParseDate(value)

                If parsed.HasValue Then
                    Return parsed
                End If
            Next

            Return Nothing
        End Function

        Private Shared Function IsCommonRisType(value As String) As Boolean
            Dim supported As String() = {
                "JOUR",
                "CONF",
                "CPAPER",
                "BOOK",
                "CHAP",
                "RPRT",
                "THES",
                "UNPB",
                "GEN"
            }

            Return supported.Contains(
                If(value, String.Empty).Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        End Function

        Private Shared Function NormalizeLineEndings(value As String) As String
            Return value.
                Replace(ControlChars.CrLf, ControlChars.Lf).
                Replace(ControlChars.Cr, ControlChars.Lf)
        End Function

        Private Shared Sub AddLine(
            builder As StringBuilder,
            tag As String,
            value As String,
            Optional allowBlank As Boolean = False
        )
            If Not allowBlank AndAlso
               String.IsNullOrWhiteSpace(value) Then
                Return
            End If

            builder.Append(
                tag.PadRight(4) &
                "- " &
                If(value, String.Empty).Trim() &
                Environment.NewLine
            )
        End Sub

        Private Shared Sub SplitPages(
            pages As String,
            ByRef startPage As String,
            ByRef endPage As String
        )
            startPage = String.Empty
            endPage = String.Empty

            If String.IsNullOrWhiteSpace(pages) Then
                Return
            End If

            Dim value As String = pages.Trim()
            Dim separatorIndex As Integer =
                value.IndexOf("--", StringComparison.Ordinal)
            Dim separatorLength As Integer = 2

            If separatorIndex < 0 Then
                separatorIndex = value.IndexOf("-"c)
                separatorLength = 1
            End If

            If separatorIndex < 0 Then
                startPage = value
                Return
            End If

            startPage =
                value.Substring(0, separatorIndex).Trim()

            endPage =
                value.Substring(separatorIndex + separatorLength).Trim()
        End Sub

        Private Shared Function ExportAuthors(
            manuscript As Manuscript,
            library As AuthorLibraryData
        ) As List(Of String)

            Dim results As New List(Of String)()

            If manuscript.Authors Is Nothing OrElse
               manuscript.Authors.Count = 0 Then
                If Not String.IsNullOrWhiteSpace(manuscript.CoAuthors) Then
                    results.Add(manuscript.CoAuthors.Trim())
                End If
                Return results
            End If

            For Each link As ManuscriptAuthor In manuscript.Authors
                Dim author As AuthorRecord =
                    library.Authors.
                        FirstOrDefault(
                            Function(item) item.Id = link.AuthorId
                        )

                If author Is Nothing Then
                    Continue For
                End If

                If Not String.IsNullOrWhiteSpace(author.FamilyName) Then
                    Dim rightParts As New List(Of String)()

                    If Not String.IsNullOrWhiteSpace(author.GivenName) Then
                        rightParts.Add(author.GivenName.Trim())
                    End If

                    If Not String.IsNullOrWhiteSpace(author.MiddleName) Then
                        rightParts.Add(author.MiddleName.Trim())
                    End If

                    Dim value As String = author.FamilyName.Trim()

                    If rightParts.Count > 0 Then
                        value &= ", " & String.Join(" ", rightParts)
                    End If

                    If Not String.IsNullOrWhiteSpace(author.Suffix) Then
                        value &= ", " & author.Suffix.Trim()
                    End If

                    results.Add(value)
                Else
                    results.Add(author.DisplayName)
                End If
            Next

            Return results
        End Function

    End Class

End Namespace
