Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    ' Proposes manuscript fields from a pasted title page: text copied from Word
    ' or LaTeX source (standard, authblk, REVTeX/elsarticle, and apa7 commands).
    ' Parsing is local and side-effect free. Nothing is saved, matched to the
    ' author library, or sent anywhere here, and every line that cannot be
    ' assigned is returned in UnplacedLines instead of being discarded.
    Public NotInheritable Class TitlePageParserService

        ' Footnote-style markers: digits or common footnote symbols (asterisk,
        ' dagger, double dagger, section sign, pilcrow, number sign).
        Private Const SymbolClass As String = "*\u2020\u2021\u00A7\u00B6#"
        Private Const MarkerClass As String = "0-9" & SymbolClass

        Private Shared ReadOnly LatexSignal As New Regex(
            "\\(?:documentclass|title|author|authorsnames|affil|affiliation|maketitle)(?![A-Za-z])|\\begin\s*\{abstract\}")

        Private Shared ReadOnly LatexCommand As New Regex(
            "\\(?<cmd>title|authorsnames|authorsaffiliations|author|affiliation|affil|address|keywords|abstract|begin|shorttitle|leftheader|authornote)(?![A-Za-z])")

        Private Shared ReadOnly LatexStructure As New Regex(
            "^\\(?:documentclass|usepackage|maketitle|begin|end|date|newcommand|renewcommand|def|setlength|pagestyle|thispagestyle|bibliographystyle|bibliography|input|include|label|noindent|newpage|clearpage|linenumbers|doublespacing|onehalfspacing|singlespacing|graphicspath|hypersetup|journal|volume|received|accepted|note)(?![A-Za-z])")

        Private Shared ReadOnly LatexComment As New Regex("(?<!\\)%.*$", RegexOptions.Multiline)

        Private Shared ReadOnly LatexAccent As New Regex(
            "\\(?<a>['`^""~=.])\s*(?:\{(?<c>[A-Za-z])\}|(?<c>[A-Za-z]))")

        Private Shared ReadOnly LatexCedilla As New Regex("\\c\s*\{(?<c>[A-Za-z])\}")

        Private Shared ReadOnly LatexFormatting As New Regex(
            "\\(?:textbf|textit|textsl|textsc|textrm|textsf|texttt|textup|textnormal|emph|underline|mbox|text|mathrm|mathit|mathbf|uppercase|MakeUppercase|MakeTextUppercase)\s*\{(?<body>[^{}]*)\}")

        ' $^{1,2}$ or ^{1} markers, but not a circumflex accent such as \^{o}.
        Private Shared ReadOnly LatexSuperscript As New Regex(
            "\$?(?<!\\)\^\{(?<m>[^}]*)\}\$?|\$\^(?<m>[^$\s]+)\$")

        Private Shared ReadOnly AuthorToken As New Regex(
            "\G\s*(?:[,;]\s*)*(?<name>[^,;" & MarkerClass & "]+?)\s*(?<marks>[" & MarkerClass & "]+(?:\s*,\s*[" & MarkerClass & "]+)*)?\s*(?:[,;]|$)")

        Private Shared ReadOnly MarkerUnit As New Regex("[0-9]+|[" & SymbolClass & "]")

        Private Shared ReadOnly SymbolOnly As New Regex("^[" & SymbolClass & "]+$")

        Private Shared ReadOnly AffiliationLine As New Regex(
            "^(?<m>[0-9]{1,2}|[" & SymbolClass & "])\s*(?<text>\p{L}.*)$")

        Private Shared ReadOnly NameWord As New Regex(
            "^(?:\p{Lu}[\p{L}\p{M}'\u2019\-]*\.?|(?:\p{Lu}\.-?)+)$")

        Private Shared ReadOnly ListJoinerAtEnd As New Regex("(?:,|\band|&)\s*$", RegexOptions.IgnoreCase)

        Private Shared ReadOnly CorrespondenceLabel As New Regex(
            "^(?:[" & SymbolClass & "]\s*)?(?:corresponding\s+author|correspondence\s*(?::|concerning\b|should\b|to\b))",
            RegexOptions.IgnoreCase)

        Private Shared ReadOnly AbstractLabel As New Regex(
            "^abstract\s*(?:[:.\u2014\u2013-]\s*(?<rest>.*)|$)", RegexOptions.IgnoreCase)

        Private Shared ReadOnly KeywordsLabel As New Regex(
            "^(?:keywords|key\s+words|index\s+terms)\s*(?:[:.\u2014\u2013-]\s*(?<rest>.*)|$)", RegexOptions.IgnoreCase)

        Private Shared ReadOnly OtherLabel As New Regex(
            "^(?:\d+\.?\s*)?(?:running\s+head|short\s+title|word\s+count|author\s+note|funding|acknowledge?ments?|conflicts?\s+of\s+interest|competing\s+interests?|data\s+availability|introduction)\s*(?:[:.\u2014\u2013-]|$)",
            RegexOptions.IgnoreCase)

        Private Shared ReadOnly NameParticles As New HashSet(Of String)(StringComparer.Ordinal) From {
            "van", "von", "de", "der", "den", "del", "della", "di", "da", "du", "dos", "das",
            "le", "la", "bin", "ibn", "al", "el", "ter", "ten", "y"
        }

        ' Capitalized title words that rarely appear in personal names. They keep a
        ' title-case subtitle such as "Evidence From Three Studies" from being read
        ' as an author.
        Private Shared ReadOnly TitleWords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "a", "an", "the", "of", "in", "on", "for", "from", "with", "to", "by", "at", "as",
            "under", "across", "among", "between", "versus", "vs", "via", "into", "over", "through",
            "without", "within", "toward", "towards", "after", "before", "during", "is", "are",
            "does", "do", "can", "why", "how", "what", "when", "not", "study", "studies",
            "effect", "effects", "evidence"
        }

        Private Enum LineKind
            Blank
            Plain
            AbstractHeading
            KeywordsHeading
            Correspondence
            OtherLabel
        End Enum

        Private NotInheritable Class AuthorCandidate
            Public Property Name As String = String.Empty
            Public Property Marks As List(Of String) = New List(Of String)()
        End Class


        Private Sub New()
        End Sub


        Public Shared Function Parse(text As String) As TitlePageParseResult

            Dim result As New TitlePageParseResult()

            If String.IsNullOrWhiteSpace(text) Then
                Return result
            End If

            Dim normalized As String = NormalizeInput(text)
            Dim affiliations As New Dictionary(Of String, String)(StringComparer.Ordinal)
            Dim correspondence As New List(Of String)()

            If LatexSignal.IsMatch(normalized) Then
                result.SourceFormat = TitlePageSourceFormat.Latex
                ParseLatex(normalized, result, affiliations, correspondence)
            Else
                result.SourceFormat = TitlePageSourceFormat.PlainText
                ParsePlainText(normalized, result, affiliations, correspondence)
            End If

            ResolveAffiliations(result, affiliations)
            ResolveCorrespondence(result, correspondence)

            If result.HasContent Then
                If result.Title.Length = 0 Then
                    result.Warnings.Add("No title was recognized.")
                End If

                If result.Authors.Count = 0 Then
                    result.Warnings.Add("No authors were recognized.")
                End If
            End If

            Return result

        End Function


        ' =====================================================================
        ' Plain text (Word, PDF, or email copy)
        ' =====================================================================

        Private Shared Sub ParsePlainText(
            text As String,
            result As TitlePageParseResult,
            affiliations As Dictionary(Of String, String),
            correspondence As List(Of String)
        )

            Dim lines As String() =
                text.Split(ControlChars.Lf).
                    Select(Function(item) item.Trim()).
                    ToArray()

            Dim index As Integer = SkipBlank(lines, 0)

            ' Labels such as a running head can precede the title.
            While index < lines.Length
                Dim kind As LineKind = Classify(lines(index))

                If kind = LineKind.OtherLabel Then
                    AddUnplaced(result, lines(index))
                ElseIf kind = LineKind.Correspondence Then
                    AddUnplaced(result, lines(index))
                    correspondence.Add(lines(index))
                Else
                    Exit While
                End If

                index = SkipBlank(lines, index + 1)
            End While

            ' Title: the first plain line, plus continuation lines in the same
            ' paragraph until the author line.
            If index < lines.Length AndAlso Classify(lines(index)) = LineKind.Plain Then
                Dim titleLines As New List(Of String) From {lines(index)}
                index += 1

                While index < lines.Length AndAlso
                      titleLines.Count < 3 AndAlso
                      Classify(lines(index)) = LineKind.Plain AndAlso
                      SplitAuthorLine(lines(index)) Is Nothing
                    titleLines.Add(lines(index))
                    index += 1
                End While

                result.Title = BibliographyTextService.CollapseWhitespace(String.Join(" ", titleLines))
            End If

            index = SkipBlank(lines, index)

            Dim candidates As List(Of AuthorCandidate) = Nothing

            If index < lines.Length AndAlso Classify(lines(index)) = LineKind.Plain Then
                candidates = SplitAuthorLine(lines(index))
            End If

            If candidates IsNot Nothing Then
                index += 1

                ' An author list can wrap after a trailing comma or "and".
                While index < lines.Length AndAlso ListJoinerAtEnd.IsMatch(lines(index - 1))
                    Dim more As List(Of AuthorCandidate) = SplitAuthorLine(lines(index))

                    If more Is Nothing Then
                        Exit While
                    End If

                    candidates.AddRange(more)
                    index += 1
                End While

                For Each candidate As AuthorCandidate In candidates
                    Dim author As New TitlePageAuthor With {
                        .Name = BibliographyTextService.ParsePersonName(candidate.Name, result.Warnings)
                    }
                    author.Markers.AddRange(candidate.Marks)
                    result.Authors.Add(author)
                Next

                index = ReadMarkedAffiliations(lines, index, result, affiliations, correspondence)
                index = ReadUnmarkedAffiliation(lines, index, result, affiliations)
            End If

            ReadBody(lines, index, result, correspondence)

        End Sub


        Private Shared Function ReadMarkedAffiliations(
            lines As String(),
            startIndex As Integer,
            result As TitlePageParseResult,
            affiliations As Dictionary(Of String, String),
            correspondence As List(Of String)
        ) As Integer

            Dim index As Integer = startIndex

            While index < lines.Length
                Dim line As String = lines(index)

                If line.Length = 0 Then
                    index += 1
                    Continue While
                End If

                Dim kind As LineKind = Classify(line)

                If kind = LineKind.Correspondence Then
                    AddUnplaced(result, line)
                    correspondence.Add(line)
                    index += 1
                    Continue While
                End If

                Dim match As Match = AffiliationLine.Match(line)

                If kind <> LineKind.Plain OrElse Not match.Success Then
                    Exit While
                End If

                Dim marker As String = match.Groups("m").Value

                If Char.IsDigit(marker(0)) AndAlso Not affiliations.ContainsKey(marker) Then
                    affiliations(marker) = TrimAffiliation(match.Groups("text").Value)
                Else
                    ' Symbol notes such as "† Equal contribution" stay visible.
                    AddUnplaced(result, line)
                End If

                index += 1
            End While

            Return index

        End Function


        ' APA-style title pages often list one shared affiliation without markers.
        Private Shared Function ReadUnmarkedAffiliation(
            lines As String(),
            startIndex As Integer,
            result As TitlePageParseResult,
            affiliations As Dictionary(Of String, String)
        ) As Integer

            If affiliations.Count > 0 OrElse
               result.Authors.Any(Function(author) author.Markers.Any(AddressOf IsDigitMarker)) Then
                Return startIndex
            End If

            Dim index As Integer = startIndex
            Dim block As New List(Of String)()

            While index < lines.Length AndAlso
                  lines(index).Length > 0 AndAlso
                  Classify(lines(index)) = LineKind.Plain
                block.Add(lines(index))
                index += 1
            End While

            If block.Count = 1 AndAlso
               block(0).Length <= 160 AndAlso
               Not block(0).EndsWith("."c) Then
                Dim affiliation As String = TrimAffiliation(block(0))

                For Each author As TitlePageAuthor In result.Authors
                    author.Affiliations.Add(affiliation)
                Next

                Return index
            End If

            If block.Count > 1 Then
                result.Warnings.Add("Affiliations without markers could not be matched to authors.")
            End If

            ' Leave the block for the body reader, which keeps it visible.
            Return startIndex

        End Function


        Private Shared Sub ReadBody(
            lines As String(),
            startIndex As Integer,
            result As TitlePageParseResult,
            correspondence As List(Of String)
        )

            Dim paragraphs As New List(Of String)()
            Dim current As New List(Of String)()
            Dim inAbstract As Boolean = False
            Dim awaitingKeywords As Boolean = False

            Dim flush As Action =
                Sub()
                    If current.Count > 0 Then
                        paragraphs.Add(BibliographyTextService.CollapseWhitespace(String.Join(" ", current)))
                        current.Clear()
                    End If
                End Sub

            For index As Integer = startIndex To lines.Length - 1
                Dim line As String = lines(index)

                If line.Length = 0 Then
                    flush()
                    Continue For
                End If

                Select Case Classify(line)

                    Case LineKind.AbstractHeading
                        flush()
                        inAbstract = True
                        awaitingKeywords = False
                        Dim rest As String = AbstractLabel.Match(line).Groups("rest").Value.Trim()

                        If rest.Length > 0 Then
                            current.Add(rest)
                        End If

                    Case LineKind.KeywordsHeading
                        flush()
                        inAbstract = False
                        Dim rest As String = KeywordsLabel.Match(line).Groups("rest").Value.Trim()
                        AddKeywords(result, rest)
                        awaitingKeywords = rest.Length = 0

                    Case LineKind.Correspondence
                        flush()
                        inAbstract = False
                        awaitingKeywords = False
                        AddUnplaced(result, line)
                        correspondence.Add(line)

                    Case LineKind.OtherLabel
                        flush()
                        inAbstract = False
                        awaitingKeywords = False
                        AddUnplaced(result, line)

                    Case Else
                        If awaitingKeywords Then
                            AddKeywords(result, line)
                            awaitingKeywords = False
                        ElseIf inAbstract Then
                            current.Add(line)
                        Else
                            AddUnplaced(result, line)
                        End If

                End Select
            Next

            flush()

            If paragraphs.Count > 0 Then
                result.AbstractText = String.Join(Environment.NewLine & Environment.NewLine, paragraphs)
            End If

        End Sub


        Private Shared Function SplitAuthorLine(line As String) As List(Of AuthorCandidate)

            If String.IsNullOrWhiteSpace(line) OrElse
               line.Length > 400 OrElse
               line.Contains(":"c) Then
                Return Nothing
            End If

            Dim prepared As String =
                Regex.Replace(line, "\s+(?:and|&)\s+", ", ", RegexOptions.IgnoreCase)

            prepared = Regex.Replace(prepared, "\s+(?:and|&)\s*$", ",", RegexOptions.IgnoreCase)

            Dim candidates As New List(Of AuthorCandidate)()
            Dim position As Integer = 0

            While position < prepared.Length
                Dim match As Match = AuthorToken.Match(prepared, position)

                If Not match.Success OrElse match.Length = 0 Then
                    Exit While
                End If

                Dim name As String = BibliographyTextService.CollapseWhitespace(match.Groups("name").Value)

                If Not LooksLikePersonName(name) Then
                    Return Nothing
                End If

                Dim candidate As New AuthorCandidate With {.Name = name}
                candidate.Marks.AddRange(SplitMarkers(match.Groups("marks").Value))
                candidates.Add(candidate)
                position = match.Index + match.Length
            End While

            If candidates.Count = 0 OrElse
               prepared.Substring(position).Trim(" "c, ","c, ";"c).Length > 0 Then
                Return Nothing
            End If

            Return candidates

        End Function


        Private Shared Function LooksLikePersonName(name As String) As Boolean

            Dim words As String() =
                name.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)

            If words.Length < 2 OrElse words.Length > 5 Then
                Return False
            End If

            Dim capitalized As Integer = 0

            For Each word As String In words
                If NameParticles.Contains(word) Then
                    Continue For
                End If

                If Not word.EndsWith("."c) AndAlso TitleWords.Contains(word) Then
                    Return False
                End If

                If Not NameWord.IsMatch(word) Then
                    Return False
                End If

                capitalized += 1
            Next

            Return capitalized >= 2

        End Function


        Private Shared Function Classify(line As String) As LineKind

            If line.Length = 0 Then
                Return LineKind.Blank
            End If

            If CorrespondenceLabel.IsMatch(line) Then
                Return LineKind.Correspondence
            End If

            If AbstractLabel.IsMatch(line) Then
                Return LineKind.AbstractHeading
            End If

            If KeywordsLabel.IsMatch(line) Then
                Return LineKind.KeywordsHeading
            End If

            If OtherLabel.IsMatch(line) Then
                Return LineKind.OtherLabel
            End If

            Return LineKind.Plain

        End Function


        ' =====================================================================
        ' LaTeX source
        ' =====================================================================

        Private Shared Sub ParseLatex(
            source As String,
            result As TitlePageParseResult,
            affiliations As Dictionary(Of String, String),
            correspondence As List(Of String)
        )

            Dim text As String = LatexComment.Replace(source, String.Empty)
            Dim consumed(Math.Max(text.Length - 1, 0)) As Boolean

            ' REVTeX/apa6: an unkeyed \affiliation applies to every author since
            ' the previous affiliation.
            Dim group As New List(Of TitlePageAuthor)()
            Dim lastWasAffiliation As Boolean = False

            ' apa7: \authorsnames and \authorsaffiliations.
            Dim apaAuthors As New List(Of TitlePageAuthor)()
            Dim apaUsesMarkers As Boolean = False

            Dim position As Integer = 0

            Do
                Dim match As Match = LatexCommand.Match(text, position)

                If Not match.Success Then
                    Exit Do
                End If

                Dim p As Integer = match.Index + match.Length

                Select Case match.Groups("cmd").Value

                    Case "title"
                        ReadGroup(text, p, "["c, "]"c)
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing AndAlso result.Title.Length = 0 Then
                            result.Title = CleanLatex(RemoveNotes(value, result))
                        End If

                    Case "author"
                        Dim options As String = ReadGroup(text, p, "["c, "]"c)
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            If lastWasAffiliation Then
                                group.Clear()
                            End If

                            lastWasAffiliation = False

                            For Each author As TitlePageAuthor In ParseLatexAuthors(value, options, result)
                                result.Authors.Add(author)
                                group.Add(author)
                            Next
                        End If

                    Case "affil", "affiliation", "address"
                        Dim options As String = ReadGroup(text, p, "["c, "]"c)
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            Dim emails As New List(Of String)()
                            Dim affiliation As String = CleanLatex(RemoveCommandGroups(value, "email", emails))

                            For Each email As String In emails
                                AddUnplaced(result, CleanLatex(email))
                            Next

                            If options IsNot Nothing AndAlso options.Trim().Length > 0 Then
                                If affiliation.Length > 0 Then
                                    affiliations(options.Trim()) = affiliation
                                End If
                            ElseIf affiliation.Length > 0 Then
                                For Each author As TitlePageAuthor In group
                                    If Not author.Affiliations.Contains(affiliation) Then
                                        author.Affiliations.Add(affiliation)
                                    End If
                                Next
                            End If

                            lastWasAffiliation = True
                        End If

                    Case "authorsnames"
                        Dim options As String = ReadGroup(text, p, "["c, "]"c)
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            Dim names As List(Of String) = SplitTopLevel(value)
                            Dim markerGroups As List(Of String) =
                                If(options Is Nothing, Nothing, SplitTopLevel(options))

                            apaUsesMarkers = markerGroups IsNot Nothing

                            For index As Integer = 0 To names.Count - 1
                                Dim name As String = CleanLatex(names(index))

                                If name.Length = 0 Then
                                    Continue For
                                End If

                                Dim author As New TitlePageAuthor With {
                                    .Name = BibliographyTextService.ParsePersonName(name, result.Warnings)
                                }

                                If markerGroups IsNot Nothing AndAlso index < markerGroups.Count Then
                                    author.Markers.AddRange(SplitLatexMarkers(markerGroups(index)))
                                End If

                                result.Authors.Add(author)
                                apaAuthors.Add(author)
                            Next
                        End If

                    Case "authorsaffiliations"
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            Dim items As List(Of String) =
                                SplitTopLevel(value).
                                    Select(Function(item) CleanLatex(item)).
                                    Where(Function(item) item.Length > 0).
                                    ToList()

                            For index As Integer = 0 To items.Count - 1
                                affiliations((index + 1).ToString()) = items(index)
                            Next

                            If Not apaUsesMarkers Then
                                ' apa7 without markers: one shared affiliation, or one per author in order.
                                For index As Integer = 0 To apaAuthors.Count - 1
                                    If items.Count = 1 Then
                                        apaAuthors(index).Markers.Add("1")
                                    ElseIf items.Count = apaAuthors.Count Then
                                        apaAuthors(index).Markers.Add((index + 1).ToString())
                                    End If
                                Next

                                If items.Count > 1 AndAlso items.Count <> apaAuthors.Count Then
                                    result.Warnings.Add("Affiliations without markers could not be matched to authors.")
                                End If
                            End If
                        End If

                    Case "keywords"
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            AddKeywords(result, CleanLatex(value.Replace("\sep", ",")))
                        End If

                    Case "abstract"
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            result.AbstractText = CleanLatexParagraphs(value)
                        End If

                    Case "shorttitle", "leftheader"
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            AddUnplaced(result, "Running head: " & CleanLatex(value))
                        End If

                    Case "authornote"
                        Dim value As String = ReadGroup(text, p, "{"c, "}"c)

                        If value IsNot Nothing Then
                            For Each paragraph As String In SplitParagraphs(value)
                                Dim cleaned As String = CleanLatex(paragraph)
                                AddUnplaced(result, cleaned)

                                If CorrespondenceLabel.IsMatch(cleaned) Then
                                    correspondence.Add(cleaned)
                                End If
                            Next
                        End If

                    Case "begin"
                        Dim environment As String = ReadGroup(text, p, "{"c, "}"c)

                        If environment IsNot Nothing Then
                            Dim name As String = environment.Trim()

                            If name = "abstract" OrElse name = "keyword" OrElse name = "keywords" Then
                                Dim endMarker As String = "\end{" & name & "}"
                                Dim endIndex As Integer = text.IndexOf(endMarker, p, StringComparison.Ordinal)
                                Dim content As String

                                If endIndex < 0 Then
                                    content = text.Substring(p)
                                    p = text.Length
                                Else
                                    content = text.Substring(p, endIndex - p)
                                    p = endIndex + endMarker.Length
                                End If

                                If name = "abstract" Then
                                    result.AbstractText = CleanLatexParagraphs(content)
                                Else
                                    AddKeywords(result, CleanLatex(content.Replace("\sep", ",")))
                                End If
                            End If
                        End If

                End Select

                For index As Integer = match.Index To Math.Min(p, text.Length) - 1
                    consumed(index) = True
                Next

                position = Math.Max(p, match.Index + 1)
            Loop

            ReadLatexRemainder(text, consumed, result, correspondence)

        End Sub


        Private Shared Function ParseLatexAuthors(
            value As String,
            options As String,
            result As TitlePageParseResult
        ) As List(Of TitlePageAuthor)

            Dim authors As New List(Of TitlePageAuthor)()
            Dim optionMarkers As List(Of String) = SplitLatexMarkers(options)

            For Each part As String In Regex.Split(value, "\\and(?![A-Za-z])|\\AND(?![A-Za-z])")
                Dim notes As New List(Of String)()
                Dim working As String = RemoveCommandGroups(part, "thanks", notes)
                working = RemoveCommandGroups(working, "footnote", notes)

                Dim emails As New List(Of String)()
                working = RemoveCommandGroups(working, "email", emails)

                Dim inline As New List(Of String)()
                working = RemoveCommandGroups(working, "textsuperscript", inline)
                working = RemoveCommandGroups(working, "inst", inline)
                working = LatexSuperscript.Replace(
                    working,
                    Function(found As Match) As String
                        inline.Add(found.Groups("m").Value)
                        Return String.Empty
                    End Function)

                Dim hasCorrespondingReference As Boolean =
                    Regex.IsMatch(working, "\\corref(?![A-Za-z])")

                working = Regex.Replace(working, "\\(?:corref|fnref)\s*\{[^}]*\}", String.Empty)

                Dim segments As String() = Regex.Split(working, "\\\\(?:\[[^\]]*\])?")
                Dim name As String = CleanLatex(segments(0))

                If name.Length = 0 Then
                    Continue For
                End If

                Dim author As New TitlePageAuthor With {
                    .Name = BibliographyTextService.ParsePersonName(name, result.Warnings)
                }

                author.Markers.AddRange(optionMarkers)

                For Each item As String In inline
                    author.Markers.AddRange(SplitLatexMarkers(item))
                Next

                author.Markers = author.Markers.Distinct(StringComparer.Ordinal).ToList()

                author.IsCorrespondingAuthor =
                    hasCorrespondingReference OrElse
                    notes.Any(Function(note) note.IndexOf("correspond", StringComparison.OrdinalIgnoreCase) >= 0)

                ' "Name \\ Affiliation" layouts put the affiliation inside \author.
                For Each extra As String In segments.Skip(1)
                    Dim line As String = CleanLatex(extra)

                    If line.Length > 0 AndAlso Not author.Affiliations.Contains(line) Then
                        author.Affiliations.Add(line)
                    End If
                Next

                For Each note As String In notes.Concat(emails)
                    AddUnplaced(result, CleanLatex(note))
                Next

                authors.Add(author)
            Next

            Return authors

        End Function


        ' Lines outside the recognized commands are either LaTeX structure, which
        ' carries no manuscript content, or text the user should see.
        Private Shared Sub ReadLatexRemainder(
            text As String,
            consumed As Boolean(),
            result As TitlePageParseResult,
            correspondence As List(Of String)
        )

            Dim remainder As New StringBuilder(text.Length)

            For index As Integer = 0 To text.Length - 1
                If consumed(index) AndAlso text(index) <> ControlChars.Lf Then
                    remainder.Append(" "c)
                Else
                    remainder.Append(text(index))
                End If
            Next

            Dim awaitingKeywords As Boolean = False

            For Each raw As String In remainder.ToString().Split(ControlChars.Lf)
                Dim line As String = raw.Trim()

                If line.Length = 0 OrElse LatexStructure.IsMatch(line) Then
                    Continue For
                End If

                Dim cleaned As String = CleanLatex(line)

                If cleaned.Length = 0 Then
                    Continue For
                End If

                Select Case Classify(cleaned)

                    Case LineKind.KeywordsHeading
                        Dim rest As String = KeywordsLabel.Match(cleaned).Groups("rest").Value.Trim()
                        AddKeywords(result, rest)
                        awaitingKeywords = rest.Length = 0

                    Case LineKind.Correspondence
                        AddUnplaced(result, cleaned)
                        correspondence.Add(cleaned)
                        awaitingKeywords = False

                    Case LineKind.AbstractHeading
                        Dim rest As String = AbstractLabel.Match(cleaned).Groups("rest").Value.Trim()

                        If result.AbstractText.Length = 0 AndAlso rest.Length > 0 Then
                            result.AbstractText = rest
                        Else
                            AddUnplaced(result, cleaned)
                        End If

                        awaitingKeywords = False

                    Case Else
                        If awaitingKeywords Then
                            AddKeywords(result, cleaned)
                        Else
                            AddUnplaced(result, cleaned)
                        End If

                        awaitingKeywords = False

                End Select
            Next

        End Sub


        ' Reads a {...} or [...] group starting at position (after optional
        ' whitespace) and advances position past it. Returns Nothing when the
        ' next character does not open the group.
        Private Shared Function ReadGroup(
            text As String,
            ByRef position As Integer,
            open As Char,
            close As Char
        ) As String

            Dim start As Integer = position

            While start < text.Length AndAlso Char.IsWhiteSpace(text(start))
                start += 1
            End While

            If start >= text.Length OrElse text(start) <> open Then
                Return Nothing
            End If

            Dim depth As Integer = 0
            Dim index As Integer = start

            While index < text.Length
                Dim character As Char = text(index)

                If character = "\"c Then
                    index += 2
                    Continue While
                End If

                If character = open Then
                    depth += 1
                ElseIf character = close Then
                    depth -= 1

                    If depth = 0 Then
                        position = index + 1
                        Return text.Substring(start + 1, index - start - 1)
                    End If
                End If

                index += 1
            End While

            Return Nothing

        End Function


        ' Removes \command{...} groups, collecting their contents.
        Private Shared Function RemoveCommandGroups(
            value As String,
            command As String,
            collected As List(Of String)
        ) As String

            Dim pattern As New Regex("\\" & command & "(?![A-Za-z])")
            Dim builder As New StringBuilder(value.Length)
            Dim position As Integer = 0

            Do
                Dim match As Match = pattern.Match(value, position)

                If Not match.Success Then
                    builder.Append(value, position, value.Length - position)
                    Exit Do
                End If

                builder.Append(value, position, match.Index - position)

                Dim p As Integer = match.Index + match.Length
                Dim content As String = ReadGroup(value, p, "{"c, "}"c)

                If content IsNot Nothing Then
                    collected.Add(content)
                End If

                position = p
            Loop

            Return builder.ToString()

        End Function


        Private Shared Function RemoveNotes(value As String, result As TitlePageParseResult) As String

            Dim notes As New List(Of String)()
            Dim remaining As String = RemoveCommandGroups(value, "thanks", notes)
            remaining = RemoveCommandGroups(remaining, "footnote", notes)

            For Each note As String In notes
                AddUnplaced(result, CleanLatex(note))
            Next

            Return remaining

        End Function


        ' Splits on commas outside braces and removes one outer brace pair.
        Private Shared Function SplitTopLevel(value As String) As List(Of String)

            Dim items As New List(Of String)()

            If String.IsNullOrEmpty(value) Then
                Return items
            End If

            Dim depth As Integer = 0
            Dim start As Integer = 0

            For index As Integer = 0 To value.Length - 1
                Select Case value(index)
                    Case "{"c
                        depth += 1
                    Case "}"c
                        depth -= 1
                    Case ","c
                        If depth = 0 Then
                            items.Add(Unwrap(value.Substring(start, index - start)))
                            start = index + 1
                        End If
                End Select
            Next

            items.Add(Unwrap(value.Substring(start)))

            Return items.Where(Function(item) item.Length > 0).ToList()

        End Function


        Private Shared Function Unwrap(value As String) As String

            Dim trimmed As String = value.Trim()

            If trimmed.Length >= 2 AndAlso trimmed(0) = "{"c AndAlso trimmed(trimmed.Length - 1) = "}"c Then
                Dim position As Integer = 0
                Dim inner As String = ReadGroup(trimmed, position, "{"c, "}"c)

                If inner IsNot Nothing AndAlso position = trimmed.Length Then
                    Return inner.Trim()
                End If
            End If

            Return trimmed

        End Function


        Private Shared Function SplitLatexMarkers(value As String) As List(Of String)

            If String.IsNullOrWhiteSpace(value) Then
                Return New List(Of String)()
            End If

            Dim prepared As String =
                value.
                    Replace("\ast", "*").
                    Replace("\star", "*").
                    Replace("\dagger", ChrW(&H2020)).
                    Replace("\ddagger", ChrW(&H2021)).
                    Replace("{", String.Empty).
                    Replace("}", String.Empty).
                    Replace("$", String.Empty)

            Return prepared.
                Split(","c).
                Select(Function(item) item.Trim()).
                Where(Function(item) item.Length > 0).
                ToList()

        End Function


        Private Shared Function CleanLatexParagraphs(value As String) As String

            Return String.Join(
                Environment.NewLine & Environment.NewLine,
                SplitParagraphs(value).
                    Select(Function(paragraph) CleanLatex(paragraph)).
                    Where(Function(paragraph) paragraph.Length > 0))

        End Function


        Private Shared Function SplitParagraphs(value As String) As String()

            Return Regex.Split(value, "\n\s*\n|\\par(?![A-Za-z])")

        End Function


        Private Shared Function CleanLatex(value As String) As String

            If String.IsNullOrEmpty(value) Then
                Return String.Empty
            End If

            Dim text As String = Regex.Replace(value, "\\\\(?:\[[^\]]*\])?", " ")

            text = LatexCedilla.Replace(text, Function(found As Match) found.Groups("c").Value & ChrW(&H327))
            text = LatexAccent.Replace(text, Function(found As Match) found.Groups("c").Value & CombiningAccent(found.Groups("a").Value))

            text = text.
                Replace("\&", "&").
                Replace("\%", "%").
                Replace("\$", "$").
                Replace("\_", "_").
                Replace("\#", "#")

            Dim previous As String

            Do
                previous = text
                text = LatexFormatting.Replace(text, "${body}")
            Loop While text <> previous

            text = Regex.Replace(text, "\\[A-Za-z]+\*?", " ")

            text = text.
                Replace("---", ChrW(&H2014)).
                Replace("--", ChrW(&H2013)).
                Replace("``", ChrW(&H201C)).
                Replace("''", ChrW(&H201D)).
                Replace("~", " ").
                Replace("$", String.Empty).
                Replace("{", String.Empty).
                Replace("}", String.Empty)

            Return BibliographyTextService.CollapseWhitespace(text.Normalize(NormalizationForm.FormC))

        End Function


        Private Shared Function CombiningAccent(accent As String) As String

            Select Case accent
                Case "'"
                    Return ChrW(&H301)
                Case "`"
                    Return ChrW(&H300)
                Case "^"
                    Return ChrW(&H302)
                Case """"
                    Return ChrW(&H308)
                Case "~"
                    Return ChrW(&H303)
                Case "="
                    Return ChrW(&H304)
                Case Else
                    Return ChrW(&H307)
            End Select

        End Function


        ' =====================================================================
        ' Shared helpers
        ' =====================================================================

        Private Shared Function NormalizeInput(text As String) As String

            Dim builder As New StringBuilder(text.Length)

            For Each character As Char In text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
                Dim code As Integer = AscW(character)

                Select Case code
                    Case &HB9
                        builder.Append("1"c)
                    Case &HB2
                        builder.Append("2"c)
                    Case &HB3
                        builder.Append("3"c)
                    Case &H2070
                        builder.Append("0"c)
                    Case &H2074 To &H2079
                        builder.Append(ChrW(AscW("4"c) + code - &H2074))
                    Case &H2217
                        builder.Append("*"c)
                    Case &HA0, &H2007, &H202F
                        builder.Append(" "c)
                    Case &H200B, &HFEFF
                        ' Zero-width characters from PDF and web copies.
                    Case Else
                        builder.Append(character)
                End Select
            Next

            Return builder.ToString()

        End Function


        Private Shared Function SplitMarkers(value As String) As List(Of String)

            Return MarkerUnit.
                Matches(value).
                Cast(Of Match)().
                Select(Function(match) match.Value).
                Distinct(StringComparer.Ordinal).
                ToList()

        End Function


        Private Shared Function IsDigitMarker(marker As String) As Boolean

            Return marker.Length > 0 AndAlso marker.All(AddressOf Char.IsDigit)

        End Function


        Private Shared Sub ResolveAffiliations(
            result As TitlePageParseResult,
            affiliations As Dictionary(Of String, String)
        )

            Dim used As New HashSet(Of String)(StringComparer.Ordinal)

            For Each author As TitlePageAuthor In result.Authors
                For Each marker As String In author.Markers
                    Dim affiliation As String = Nothing

                    If affiliations.TryGetValue(marker, affiliation) Then
                        used.Add(marker)

                        If Not author.Affiliations.Contains(affiliation) Then
                            author.Affiliations.Add(affiliation)
                        End If
                    ElseIf Not SymbolOnly.IsMatch(marker) Then
                        result.Warnings.Add(
                            "No affiliation was found for marker " & marker &
                            " on " & author.Name.DisplayName & ".")
                    End If
                Next
            Next

            For Each pair As KeyValuePair(Of String, String) In affiliations
                If Not used.Contains(pair.Key) Then
                    result.Warnings.Add("Affiliation " & pair.Key & " is not linked to any author.")
                    AddUnplaced(result, pair.Key & " " & pair.Value)
                End If
            Next

        End Sub


        Private Shared Sub ResolveCorrespondence(
            result As TitlePageParseResult,
            correspondence As List(Of String)
        )

            For Each line As String In correspondence
                Dim symbol As Match = Regex.Match(line, "^[" & SymbolClass & "]")
                Dim matched As Boolean = False

                If symbol.Success Then
                    For Each author As TitlePageAuthor In result.Authors
                        If author.Markers.Contains(symbol.Value) Then
                            author.IsCorrespondingAuthor = True
                            matched = True
                        End If
                    Next
                End If

                If Not matched Then
                    For Each author As TitlePageAuthor In result.Authors
                        Dim family As String = author.Name.FamilyName

                        If family.Length > 0 AndAlso
                           Regex.IsMatch(line, "\b" & Regex.Escape(family) & "\b", RegexOptions.IgnoreCase) Then
                            author.IsCorrespondingAuthor = True
                        End If
                    Next
                End If
            Next

            ' Without an explicit note, an asterisk conventionally marks the
            ' corresponding author.
            If Not result.Authors.Any(Function(author) author.IsCorrespondingAuthor) Then
                For Each author As TitlePageAuthor In result.Authors
                    If author.Markers.Contains("*") Then
                        author.IsCorrespondingAuthor = True
                    End If
                Next
            End If

        End Sub


        Private Shared Sub AddKeywords(result As TitlePageParseResult, value As String)

            For Each keyword As String In BibliographyTextService.SplitKeywords(value)
                Dim cleaned As String = keyword.TrimEnd("."c).Trim()

                If cleaned.Length > 0 AndAlso
                   Not result.Keywords.Contains(cleaned, StringComparer.OrdinalIgnoreCase) Then
                    result.Keywords.Add(cleaned)
                End If
            Next

        End Sub


        Private Shared Sub AddUnplaced(result As TitlePageParseResult, line As String)

            Dim cleaned As String = BibliographyTextService.CollapseWhitespace(line)

            If cleaned.Length > 0 AndAlso Not result.UnplacedLines.Contains(cleaned) Then
                result.UnplacedLines.Add(cleaned)
            End If

        End Sub


        Private Shared Function TrimAffiliation(value As String) As String

            Return BibliographyTextService.CollapseWhitespace(value).TrimEnd(";"c, ","c).Trim()

        End Function


        Private Shared Function SkipBlank(lines As String(), index As Integer) As Integer

            Dim position As Integer = index

            While position < lines.Length AndAlso lines(position).Length = 0
                position += 1
            End While

            Return position

        End Function

    End Class

End Namespace
