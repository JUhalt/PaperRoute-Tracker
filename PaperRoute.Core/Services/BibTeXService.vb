Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class BibTeXService

        Private Shared ReadOnly KnownFields As New HashSet(Of String)(
            StringComparer.OrdinalIgnoreCase
        ) From {
            "author",
            "title",
            "journal",
            "journaltitle",
            "booktitle",
            "year",
            "month",
            "date",
            "volume",
            "number",
            "issue",
            "pages",
            "publisher",
            "doi",
            "url",
            "abstract",
            "keywords"
        }

        Private Sub New()
        End Sub

        Public Shared Function Parse(text As String) As BibliographyParseResult
            Dim result As New BibliographyParseResult With {
                .Format = BibliographyFormat.BibTeX
            }

            If String.IsNullOrWhiteSpace(text) Then
                result.FileWarnings.Add("The BibTeX file was empty.")
                Return result
            End If

            For Each rawEntry As RawBibEntry In ExtractEntries(text, result.FileWarnings)
                If String.Equals(rawEntry.EntryType, "comment", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(rawEntry.EntryType, "preamble", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(rawEntry.EntryType, "string", StringComparison.OrdinalIgnoreCase) Then

                    Continue For
                End If

                result.Records.Add(MapEntry(rawEntry))
            Next

            If result.Records.Count = 0 Then
                result.FileWarnings.Add("No BibTeX bibliography records were found.")
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
            Dim usedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each manuscript As Manuscript In manuscripts
                If manuscript Is Nothing OrElse
                   String.IsNullOrWhiteSpace(manuscript.Title) Then
                    Continue For
                End If

                Dim entryType As String =
                    WorkTypeService.ToBibTeX(
                        manuscript.WorkType,
                        If(
                            manuscript.Metadata IsNot Nothing AndAlso
                            Not String.IsNullOrWhiteSpace(
                                manuscript.Metadata.PublicationJournal
                            ),
                            "article",
                            "misc"
                        )
                    )

                Dim key As String =
                    CreateCitationKey(manuscript, authorLibrary, usedKeys)

                builder.Append(
                    "@" & entryType & "{" & key & "," & Environment.NewLine
                )

                AddField(builder, "author", ExportAuthors(manuscript, authorLibrary))
                AddField(builder, "title", manuscript.Title)

                If manuscript.Metadata IsNot Nothing Then
                    AddField(builder, "journal", manuscript.Metadata.PublicationJournal)

                    If manuscript.Metadata.PublishedDate.HasValue Then
                        AddField(
                            builder,
                            "year",
                            manuscript.Metadata.PublishedDate.Value.Year.ToString()
                        )

                        If manuscript.Metadata.PublishedDate.Value.Month <> 1 OrElse
                           manuscript.Metadata.PublishedDate.Value.Day <> 1 Then
                            AddField(
                                builder,
                                "month",
                                manuscript.Metadata.PublishedDate.Value.Month.ToString()
                            )
                        End If
                    End If

                    AddField(builder, "volume", manuscript.Metadata.Volume)
                    AddField(builder, "number", manuscript.Metadata.Issue)
                    AddField(builder, "pages", manuscript.Metadata.Pages)
                    AddField(builder, "publisher", manuscript.Metadata.Publisher)
                    AddField(
                        builder,
                        "doi",
                        DoiNormalizer.Normalize(manuscript.Metadata.Doi)
                    )
                    AddField(builder, "url", manuscript.Metadata.PublicationUrl)
                    AddField(builder, "abstract", manuscript.Metadata.AbstractText)

                    If manuscript.Metadata.Keywords IsNot Nothing AndAlso
                       manuscript.Metadata.Keywords.Count > 0 Then
                        AddField(
                            builder,
                            "keywords",
                            String.Join(", ", manuscript.Metadata.Keywords)
                        )
                    End If
                End If

                TrimLastFieldComma(builder)
                builder.AppendLine()
                builder.AppendLine("}")
                builder.AppendLine()
            Next

            Return builder.ToString()
        End Function

        Private Shared Function MapEntry(rawEntry As RawBibEntry) As BibliographyRecord
            Dim fields As Dictionary(Of String, String) =
                ParseFields(rawEntry.FieldText)

            Dim record As New BibliographyRecord With {
                .SourceFormat = BibliographyFormat.BibTeX,
                .SourceType = rawEntry.EntryType.Trim(),
                .SourceKey = rawEntry.CitationKey.Trim()
            }

            record.Title = CleanValue(ValueOf(fields, "title"))
            record.Journal =
                CleanValue(
                    FirstNonBlank(
                        ValueOf(fields, "journal"),
                        ValueOf(fields, "journaltitle"),
                        ValueOf(fields, "booktitle")
                    )
                )
            record.Volume = CleanValue(ValueOf(fields, "volume"))
            record.Issue =
                CleanValue(
                    FirstNonBlank(
                        ValueOf(fields, "number"),
                        ValueOf(fields, "issue")
                    )
                )
            record.Pages = CleanValue(ValueOf(fields, "pages"))
            record.Publisher = CleanValue(ValueOf(fields, "publisher"))
            record.Doi =
                DoiNormalizer.Normalize(
                    CleanValue(ValueOf(fields, "doi"))
                )
            record.Url = CleanValue(ValueOf(fields, "url"))
            record.AbstractText = CleanValue(ValueOf(fields, "abstract"))
            record.Keywords =
                BibliographyTextService.SplitKeywords(
                    CleanValue(ValueOf(fields, "keywords"))
                )

            ParseBibAuthors(
                CleanValue(ValueOf(fields, "author")),
                record
            )

            record.PublishedDate = ParseBibDate(fields)

            For Each pair As KeyValuePair(Of String, String) In fields
                If KnownFields.Contains(pair.Key) Then
                    Continue For
                End If

                record.UnmappedFields(pair.Key) = CleanValue(pair.Value)
                record.Warnings.Add(
                    "Unsupported BibTeX field '" & pair.Key &
                    "' was not mapped into PaperRoute."
                )
            Next

            If String.IsNullOrWhiteSpace(record.Title) Then
                record.Warnings.Add("This record has no title.")
            End If

            If String.Equals(
                rawEntry.EntryType,
                "inproceedings",
                StringComparison.OrdinalIgnoreCase
            ) AndAlso
               Not String.IsNullOrWhiteSpace(ValueOf(fields, "booktitle")) Then
                record.Warnings.Add(
                    "BibTeX booktitle was mapped to PaperRoute's publication-outlet field."
                )
            End If

            If Not IsCommonEntryType(rawEntry.EntryType) Then
                record.Warnings.Add(
                    "BibTeX entry type '" & rawEntry.EntryType &
                    "' is not modeled directly by PaperRoute; common metadata will still be imported."
                )
            End If

            Return record
        End Function

        Private Shared Function ParseBibDate(
            fields As Dictionary(Of String, String)
        ) As DateTime?

            Dim dateValue As String =
                CleanValue(ValueOf(fields, "date"))

            If Not String.IsNullOrWhiteSpace(dateValue) Then
                Dim parsedDate As DateTime? =
                    BibliographyTextService.ParseDate(dateValue)

                If parsedDate.HasValue Then
                    Return parsedDate
                End If
            End If

            Dim yearText As String =
                CleanValue(ValueOf(fields, "year"))

            Dim year As Integer

            If Not Integer.TryParse(yearText, year) OrElse
               year < 1 OrElse year > 9999 Then
                Return Nothing
            End If

            Dim month As Integer =
                BibliographyTextService.MonthNumber(
                    CleanValue(ValueOf(fields, "month"))
                )

            Return New DateTime(year, month, 1)
        End Function

        Private Shared Sub ParseBibAuthors(
            authorText As String,
            record As BibliographyRecord
        )
            If String.IsNullOrWhiteSpace(authorText) Then
                Return
            End If

            For Each rawAuthor As String In SplitAuthors(authorText)
                Dim parsed As BibliographyAuthor =
                    BibliographyTextService.ParsePersonName(
                        rawAuthor,
                        record.Warnings
                    )

                If Not String.IsNullOrWhiteSpace(parsed.DisplayName) Then
                    record.Authors.Add(parsed)
                End If
            Next
        End Sub

        Friend Shared Function SplitAuthors(value As String) As List(Of String)
            Dim result As New List(Of String)()
            Dim buffer As New StringBuilder()
            Dim braceDepth As Integer = 0
            Dim index As Integer = 0

            While index < value.Length
                Dim current As Char = value(index)

                If current = "{"c Then
                    braceDepth += 1
                ElseIf current = "}"c AndAlso braceDepth > 0 Then
                    braceDepth -= 1
                End If

                If braceDepth = 0 AndAlso
                   index + 5 <= value.Length AndAlso
                   String.Equals(
                       value.Substring(index, 5),
                       " and ",
                       StringComparison.OrdinalIgnoreCase
                   ) Then

                    Dim item As String = buffer.ToString().Trim()

                    If item.Length > 0 Then
                        result.Add(item)
                    End If

                    buffer.Clear()
                    index += 5
                    Continue While
                End If

                buffer.Append(current)
                index += 1
            End While

            Dim finalItem As String = buffer.ToString().Trim()

            If finalItem.Length > 0 Then
                result.Add(finalItem)
            End If

            Return result
        End Function

        Private Shared Function ExtractEntries(
            text As String,
            fileWarnings As List(Of String)
        ) As List(Of RawBibEntry)

            Dim result As New List(Of RawBibEntry)()
            Dim index As Integer = 0

            While index < text.Length
                Dim atIndex As Integer = text.IndexOf("@"c, index)

                If atIndex < 0 Then
                    Exit While
                End If

                Dim typeStart As Integer = atIndex + 1
                Dim openIndex As Integer = typeStart

                While openIndex < text.Length AndAlso
                      text(openIndex) <> "{"c AndAlso
                      text(openIndex) <> "("c
                    openIndex += 1
                End While

                If openIndex >= text.Length Then
                    Exit While
                End If

                Dim entryType As String =
                    text.Substring(
                        typeStart,
                        openIndex - typeStart
                    ).Trim()

                Dim openChar As Char = text(openIndex)
                Dim closeChar As Char =
                    If(openChar = "{"c, "}"c, ")"c)

                Dim closeIndex As Integer =
                    FindMatchingDelimiter(
                        text,
                        openIndex,
                        openChar,
                        closeChar
                    )

                If closeIndex < 0 Then
                    fileWarnings.Add(
                        "A BibTeX entry beginning near character " &
                        atIndex.ToString() &
                        " was not closed and was skipped."
                    )
                    Exit While
                End If

                Dim inner As String =
                    text.Substring(
                        openIndex + 1,
                        closeIndex - openIndex - 1
                    )

                Dim commaIndex As Integer =
                    FindTopLevelComma(inner)

                Dim citationKey As String = String.Empty
                Dim fieldText As String = String.Empty

                If commaIndex >= 0 Then
                    citationKey =
                        inner.Substring(0, commaIndex).Trim()
                    fieldText =
                        inner.Substring(commaIndex + 1)
                Else
                    citationKey = inner.Trim()
                End If

                result.Add(
                    New RawBibEntry With {
                        .EntryType = entryType,
                        .CitationKey = citationKey,
                        .FieldText = fieldText
                    }
                )

                index = closeIndex + 1
            End While

            Return result
        End Function

        Private Shared Function FindMatchingDelimiter(
            text As String,
            openIndex As Integer,
            openChar As Char,
            closeChar As Char
        ) As Integer

            Dim depth As Integer = 0
            Dim inQuote As Boolean = False
            Dim escaped As Boolean = False

            For index As Integer = openIndex To text.Length - 1
                Dim current As Char = text(index)

                If escaped Then
                    escaped = False
                    Continue For
                End If

                If current = "\"c Then
                    escaped = True
                    Continue For
                End If

                If current = """"c Then
                    inQuote = Not inQuote
                    Continue For
                End If

                If inQuote Then
                    Continue For
                End If

                If current = openChar Then
                    depth += 1
                ElseIf current = closeChar Then
                    depth -= 1

                    If depth = 0 Then
                        Return index
                    End If
                End If
            Next

            Return -1
        End Function

        Private Shared Function FindTopLevelComma(value As String) As Integer
            Dim braceDepth As Integer = 0
            Dim inQuote As Boolean = False
            Dim escaped As Boolean = False

            For index As Integer = 0 To value.Length - 1
                Dim current As Char = value(index)

                If escaped Then
                    escaped = False
                    Continue For
                End If

                If current = "\"c Then
                    escaped = True
                    Continue For
                End If

                If current = """"c Then
                    inQuote = Not inQuote
                    Continue For
                End If

                If inQuote Then
                    Continue For
                End If

                If current = "{"c Then
                    braceDepth += 1
                ElseIf current = "}"c AndAlso braceDepth > 0 Then
                    braceDepth -= 1
                ElseIf current = ","c AndAlso braceDepth = 0 Then
                    Return index
                End If
            Next

            Return -1
        End Function

        Friend Shared Function ParseFields(
            fieldText As String
        ) As Dictionary(Of String, String)

            Dim fields As New Dictionary(Of String, String)(
                StringComparer.OrdinalIgnoreCase
            )

            Dim index As Integer = 0

            While index < fieldText.Length
                SkipSeparatorsAndWhitespace(fieldText, index)

                If index >= fieldText.Length Then
                    Exit While
                End If

                Dim nameStart As Integer = index

                While index < fieldText.Length AndAlso
                      fieldText(index) <> "="c AndAlso
                      fieldText(index) <> ","c
                    index += 1
                End While

                If index >= fieldText.Length OrElse
                   fieldText(index) <> "="c Then
                    Exit While
                End If

                Dim fieldName As String =
                    fieldText.Substring(
                        nameStart,
                        index - nameStart
                    ).Trim()

                index += 1

                While index < fieldText.Length AndAlso
                      Char.IsWhiteSpace(fieldText(index))
                    index += 1
                End While

                Dim rawValue As String =
                    ReadFieldValue(fieldText, index)

                If Not String.IsNullOrWhiteSpace(fieldName) Then
                    fields(fieldName) = rawValue
                End If

                While index < fieldText.Length AndAlso
                      fieldText(index) <> ","c
                    index += 1
                End While

                If index < fieldText.Length AndAlso
                   fieldText(index) = ","c Then
                    index += 1
                End If
            End While

            Return fields
        End Function

        Private Shared Function ReadFieldValue(
            text As String,
            ByRef index As Integer
        ) As String

            If index >= text.Length Then
                Return String.Empty
            End If

            If text(index) = "{"c Then
                Dim start As Integer = index
                Dim depth As Integer = 0
                Dim escaped As Boolean = False

                While index < text.Length
                    Dim current As Char = text(index)

                    If escaped Then
                        escaped = False
                        index += 1
                        Continue While
                    End If

                    If current = "\"c Then
                        escaped = True
                        index += 1
                        Continue While
                    End If

                    If current = "{"c Then
                        depth += 1
                    ElseIf current = "}"c Then
                        depth -= 1

                        If depth = 0 Then
                            index += 1
                            Return text.Substring(start, index - start)
                        End If
                    End If

                    index += 1
                End While

                Return text.Substring(start)
            End If

            If text(index) = """"c Then
                Dim start As Integer = index
                index += 1

                Dim escaped As Boolean = False

                While index < text.Length
                    Dim current As Char = text(index)

                    If escaped Then
                        escaped = False
                        index += 1
                        Continue While
                    End If

                    If current = "\"c Then
                        escaped = True
                        index += 1
                        Continue While
                    End If

                    If current = """"c Then
                        index += 1
                        Return text.Substring(start, index - start)
                    End If

                    index += 1
                End While

                Return text.Substring(start)
            End If

            Dim bareStart As Integer = index

            While index < text.Length AndAlso
                  text(index) <> ","c
                index += 1
            End While

            Return text.Substring(
                bareStart,
                index - bareStart
            ).Trim()
        End Function

        Private Shared Sub SkipSeparatorsAndWhitespace(
            text As String,
            ByRef index As Integer
        )
            While index < text.Length AndAlso
                  (
                      Char.IsWhiteSpace(text(index)) OrElse
                      text(index) = ","c
                  )
                index += 1
            End While
        End Sub

        Friend Shared Function CleanValue(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Dim result As String = value.Trim()

            If result.Length >= 2 AndAlso
               (
                   (
                       result.StartsWith("{", StringComparison.Ordinal) AndAlso
                       result.EndsWith("}", StringComparison.Ordinal)
                   ) OrElse
                   (
                       result.StartsWith("""", StringComparison.Ordinal) AndAlso
                       result.EndsWith("""", StringComparison.Ordinal)
                   )
               ) Then

                result =
                    result.Substring(1, result.Length - 2)
            End If

            result =
                result.Replace("\&", "&").
                    Replace("\%", "%").
                    Replace("\_", "_").
                    Replace("\#", "#").
                    Replace("\{", "{").
                    Replace("\}", "}").
                    Replace("~", " ")

            Return BibliographyTextService.CollapseWhitespace(result)
        End Function

        Private Shared Function ValueOf(
            fields As Dictionary(Of String, String),
            key As String
        ) As String

            Dim value As String = Nothing

            If fields.TryGetValue(key, value) Then
                Return value
            End If

            Return String.Empty
        End Function

        Private Shared Function FirstNonBlank(
            ParamArray values As String()
        ) As String

            For Each value As String In values
                If Not String.IsNullOrWhiteSpace(value) Then
                    Return value
                End If
            Next

            Return String.Empty
        End Function

        Private Shared Function IsCommonEntryType(value As String) As Boolean
            Dim supported As String() = {
                "article",
                "inproceedings",
                "proceedings",
                "book",
                "inbook",
                "incollection",
                "phdthesis",
                "mastersthesis",
                "techreport",
                "unpublished",
                "misc"
            }

            Return supported.Contains(
                value.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        End Function

        Private Shared Sub AddField(
            builder As StringBuilder,
            fieldName As String,
            value As String
        )
            If String.IsNullOrWhiteSpace(value) Then
                Return
            End If

            builder.Append(
                "  " & fieldName & " = {" &
                EscapeBibValue(value) &
                "}," & Environment.NewLine
            )
        End Sub

        Friend Shared Function EscapeBibValue(value As String) As String
            If value Is Nothing Then
                Return String.Empty
            End If

            Return value.
                Replace("\", "\\").
                Replace("{", "\{").
                Replace("}", "\}").
                Replace("&", "\&").
                Replace("%", "\%").
                Replace("#", "\#").
                Replace("_", "\_")
        End Function

        Private Shared Sub TrimLastFieldComma(builder As StringBuilder)
            Dim suffix As String = "," & Environment.NewLine

            If builder.Length >= suffix.Length AndAlso
               String.Equals(
                   builder.ToString(
                       builder.Length - suffix.Length,
                       suffix.Length
                   ),
                   suffix,
                   StringComparison.Ordinal
               ) Then

                builder.Remove(
                    builder.Length - suffix.Length,
                    1
                )
            End If
        End Sub

        Private Shared Function ExportAuthors(
            manuscript As Manuscript,
            library As AuthorLibraryData
        ) As String

            If manuscript.Authors Is Nothing OrElse
               manuscript.Authors.Count = 0 Then
                Return If(manuscript.CoAuthors, String.Empty).Trim()
            End If

            Dim results As New List(Of String)()

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
                    Dim givenParts As New List(Of String)()

                    If Not String.IsNullOrWhiteSpace(author.GivenName) Then
                        givenParts.Add(author.GivenName.Trim())
                    End If

                    If Not String.IsNullOrWhiteSpace(author.MiddleName) Then
                        givenParts.Add(author.MiddleName.Trim())
                    End If

                    Dim value As String = author.FamilyName.Trim()

                    If Not String.IsNullOrWhiteSpace(author.Suffix) Then
                        value &= ", " & author.Suffix.Trim()
                    End If

                    If givenParts.Count > 0 Then
                        value &= ", " & String.Join(" ", givenParts)
                    End If

                    results.Add(value)
                Else
                    results.Add("{" & author.DisplayName & "}")
                End If
            Next

            Return String.Join(" and ", results)
        End Function

        Private Shared Function CreateCitationKey(
            manuscript As Manuscript,
            library As AuthorLibraryData,
            usedKeys As HashSet(Of String)
        ) As String

            Dim family As String = "paperroute"

            If manuscript.Authors IsNot Nothing AndAlso
               manuscript.Authors.Count > 0 Then

                Dim authorId As Guid = manuscript.Authors(0).AuthorId

                Dim author As AuthorRecord =
                    library.Authors.
                        FirstOrDefault(
                            Function(item) item.Id = authorId
                        )

                If author IsNot Nothing Then
                    If Not String.IsNullOrWhiteSpace(author.FamilyName) Then
                        family = author.FamilyName
                    ElseIf Not String.IsNullOrWhiteSpace(author.DisplayName) Then
                        family = author.DisplayName
                    End If
                End If
            End If

            Dim yearText As String = "nd"

            If manuscript.Metadata IsNot Nothing AndAlso
               manuscript.Metadata.PublishedDate.HasValue Then
                yearText = manuscript.Metadata.PublishedDate.Value.Year.ToString()
            End If

            Dim titleWord As String = "work"
            Dim titleTokens As String() =
                If(manuscript.Title, String.Empty).
                    Split(
                        New Char() {" "c},
                        StringSplitOptions.RemoveEmptyEntries
                    )

            If titleTokens.Length > 0 Then
                titleWord = titleTokens(0)
            End If

            Dim baseKey As String =
                SanitizeKey(family & yearText & titleWord)

            If String.IsNullOrWhiteSpace(baseKey) Then
                baseKey = "paperroute" & yearText
            End If

            Dim candidate As String = baseKey
            Dim suffix As Integer = 2

            While usedKeys.Contains(candidate)
                candidate = baseKey & suffix.ToString()
                suffix += 1
            End While

            usedKeys.Add(candidate)
            Return candidate
        End Function

        Private Shared Function SanitizeKey(value As String) As String
            Dim builder As New StringBuilder()

            For Each ch As Char In value
                If Char.IsLetterOrDigit(ch) OrElse
                   ch = "_"c OrElse
                   ch = "-"c Then
                    builder.Append(ch)
                End If
            Next

            Return builder.ToString()
        End Function

        Private Class RawBibEntry
            Public Property EntryType As String = String.Empty
            Public Property CitationKey As String = String.Empty
            Public Property FieldText As String = String.Empty
        End Class

    End Class

End Namespace
