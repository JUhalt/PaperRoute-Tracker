Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Xml

Namespace Services

    ' What a packet export (#45) shows about each file before it is copied:
    ' the names and details a file keeps inside itself. Files are only read,
    ' never changed, and nothing is ever removed from them.
    Public Enum HiddenMetadataState
        ' Inspected; the findings may be empty ("None found").
        Checked
        ' .txt, .csv, .md: no hidden fields to look at.
        PlainType
        ' Any other type PaperRoute doesn't read.
        NotChecked
        ' Damaged or unreadable.
        CouldNotCheck
    End Enum


    Public NotInheritable Class HiddenMetadataFinding

        Public Sub New(label As String, value As String, namesPerson As Boolean)
            Me.Label = If(label, String.Empty)
            Me.Value = If(value, String.Empty)
            Me.NamesPerson = namesPerson
        End Sub

        ' "Author", "Last saved by", "Company", "Template", "Comment authors",
        ' "Tracked changes by", "Camera details", or "Location".
        Public ReadOnly Property Label As String

        ' Trimmed, with whitespace collapsed; several values joined by ", ".
        Public ReadOnly Property Value As String

        ' True for the labels that name people.
        Public ReadOnly Property NamesPerson As Boolean

    End Class


    Public NotInheritable Class HiddenMetadataReport

        Public Sub New(
            state As HiddenMetadataState,
            Optional findings As IEnumerable(Of HiddenMetadataFinding) = Nothing,
            Optional reachedLimit As Boolean = False
        )
            Me.State = state
            Me.Findings = If(findings, Enumerable.Empty(Of HiddenMetadataFinding)()).
                Where(Function(item) item IsNot Nothing).
                ToList().
                AsReadOnly()
            Me.ReachedLimit = reachedLimit
        End Sub

        Public ReadOnly Property State As HiddenMetadataState

        Public ReadOnly Property Findings As IReadOnlyList(Of HiddenMetadataFinding)

        ' Only part of a very large file was read.
        Public ReadOnly Property ReachedLimit As Boolean

        Public ReadOnly Property HasFindings As Boolean
            Get
                Return State = HiddenMetadataState.Checked AndAlso Findings.Count > 0
            End Get
        End Property


        Public Function Summary() As String

            Dim result As String

            Select Case State
                Case HiddenMetadataState.Checked
                    result = If(
                        Findings.Count = 0,
                        "None found",
                        String.Join("; ", Findings.Select(Function(item) item.Label & ": " & item.Value))
                    )
                Case HiddenMetadataState.PlainType
                    result = "Not checked (no hidden fields for this type)"
                Case HiddenMetadataState.NotChecked
                    result = "Not checked"
                Case Else
                    result = "Couldn't be checked"
            End Select

            If ReachedLimit AndAlso State = HiddenMetadataState.Checked Then
                result &= " (checked part of this large file)"
            End If

            Return result

        End Function

    End Class


    ' Reads document properties, comment and tracked-change authors, template
    ' paths, PDF Info and XMP authors, and photo EXIF, GPS, and text chunks.
    ' Never throws for a bad file (only when cancelled): a damaged or
    ' unreadable file is reported as "Couldn't be checked".
    Public NotInheritable Class HiddenMetadataService

        Public Const DefaultByteLimit As Long = 64L * 1024 * 1024

        Private Const LargestLimit As Long = 1024L * 1024 * 1024
        Private Const MaximumValuesPerLabel As Integer = 10
        Private Const MaximumValueLength As Integer = 120
        Private Const MaximumInflatedStream As Integer = 8 * 1024 * 1024
        Private Const MaximumInflatedText As Integer = 1024 * 1024
        Private Const MaximumPdfString As Integer = 8192
        Private Const ReadBufferSize As Integer = 81920

        Private Const DcNamespace As String = "http://purl.org/dc/elements/1.1/"
        Private Const CoreNamespace As String = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
        Private Const AppNamespace As String = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
        Private Const WordNamespace As String = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
        Private Const OdfMetaNamespace As String = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0"

        Private Shared ReadOnly PlainExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".txt", ".csv", ".md"
        }

        ' Types the inspector reads: if their bytes don't match, the file is damaged.
        Private Shared ReadOnly KnownExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".docx", ".docm", ".xlsx", ".xlsm", ".pptx", ".pptm", ".dotx",
            ".odt", ".ods", ".odp", ".pdf", ".jpg", ".jpeg", ".png"
        }

        Private Shared ReadOnly LabelOrder As String() = {
            "Author", "Last saved by", "Comment authors", "Tracked changes by",
            "Company", "Template", "Camera details", "Location"
        }

        Private Shared ReadOnly PersonLabels As New HashSet(Of String)(StringComparer.Ordinal) From {
            "Author", "Last saved by", "Comment authors", "Tracked changes by"
        }

        ' Word elements that record a tracked change and its author.
        Private Shared ReadOnly TrackedChangeElements As New HashSet(Of String)(StringComparer.Ordinal) From {
            "ins", "del", "moveFrom", "moveTo", "cellIns", "cellDel", "cellMerge", "numberingChange"
        }

        Private Shared ReadOnly PdfSignature As Byte() = Encoding.ASCII.GetBytes("%PDF-")
        Private Shared ReadOnly PdfEndOfFile As Byte() = Encoding.ASCII.GetBytes("%%EOF")
        Private Shared ReadOnly PdfAuthorKey As Byte() = Encoding.ASCII.GetBytes("/Author")
        Private Shared ReadOnly PdfStreamKeyword As Byte() = Encoding.ASCII.GetBytes("stream")
        Private Shared ReadOnly PdfEndStreamKeyword As Byte() = Encoding.ASCII.GetBytes("endstream")
        Private Shared ReadOnly PdfObjKeyword As Byte() = Encoding.ASCII.GetBytes("obj")
        Private Shared ReadOnly XmpCreatorOpen As Byte() = Encoding.ASCII.GetBytes("<dc:creator")
        Private Shared ReadOnly XmpCreatorClose As Byte() = Encoding.ASCII.GetBytes("</dc:creator>")
        Private Shared ReadOnly XmpItemOpen As Byte() = Encoding.ASCII.GetBytes("<rdf:li")
        Private Shared ReadOnly XmpItemClose As Byte() = Encoding.ASCII.GetBytes("</rdf:li>")
        Private Shared ReadOnly ExifHeader As Byte() = {&H45, &H78, &H69, &H66, 0, 0}
        Private Shared ReadOnly JpegXmpHeader As Byte() = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/" & ChrW(0))
        Private Shared ReadOnly PngSignature As Byte() = {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}

        Private Shared ReadOnly StreamTypePattern As New Regex(
            "/Type\s*/(?:Metadata|ObjStm)(?![A-Za-z0-9])", RegexOptions.CultureInvariant
        )

        Private Shared ReadOnly StreamLengthPattern As New Regex(
            "/Length\s+(\d{1,10})(\s+\d+\s+R)?", RegexOptions.CultureInvariant
        )

        Private Sub New()
        End Sub


        Public Shared Function Inspect(
            sourcePath As String,
            Optional byteLimit As Long = DefaultByteLimit,
            Optional cancellationToken As CancellationToken = Nothing
        ) As HiddenMetadataReport

            Try
                cancellationToken.ThrowIfCancellationRequested()
                If String.IsNullOrWhiteSpace(sourcePath) Then Return New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck)

                Dim extension As String = ExtensionOf(sourcePath)
                Dim budget As New ReadBudget(Math.Min(LargestLimit, Math.Max(2L, byteLimit)), cancellationToken)

                Using source As New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, ReadBufferSize)

                    If source.Length = 0 Then
                        Return New HiddenMetadataReport(
                            If(PlainExtensions.Contains(extension), HiddenMetadataState.PlainType, HiddenMetadataState.Checked)
                        )
                    End If

                    Dim head As Byte() = ReadHead(source, 1024)
                    Dim found As New FindingCollector()

                    Select Case Detect(head)
                        Case DetectedKind.Pdf
                            InspectPdf(source, budget, found)
                        Case DetectedKind.Jpeg
                            InspectJpeg(source, budget, found)
                        Case DetectedKind.Png
                            InspectPng(source, budget, found)
                        Case DetectedKind.Zip
                            If Not InspectPackage(source, budget, found) Then
                                Return New HiddenMetadataReport(HiddenMetadataState.NotChecked)
                            End If
                        Case Else
                            If PlainExtensions.Contains(extension) Then Return New HiddenMetadataReport(HiddenMetadataState.PlainType)
                            If KnownExtensions.Contains(extension) Then Return New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck)
                            Return New HiddenMetadataReport(HiddenMetadataState.NotChecked)
                    End Select

                    Return New HiddenMetadataReport(HiddenMetadataState.Checked, found.ToFindings(), budget.ReachedLimit)

                End Using

            Catch ex As OperationCanceledException When cancellationToken.IsCancellationRequested
                Throw
            Catch
                Return New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck)
            End Try

        End Function


        Private Enum DetectedKind
            Unknown
            Pdf
            Jpeg
            Png
            Zip
        End Enum


        Private Shared Function Detect(head As Byte()) As DetectedKind
            If StartsWith(head, PngSignature, 0) Then Return DetectedKind.Png
            If head.Length >= 3 AndAlso head(0) = &HFF AndAlso head(1) = &HD8 AndAlso head(2) = &HFF Then Return DetectedKind.Jpeg
            If head.Length >= 4 AndAlso head(0) = &H50 AndAlso head(1) = &H4B AndAlso head(2) = 3 AndAlso head(3) = 4 Then Return DetectedKind.Zip
            ' A PDF header may follow a little leading junk.
            If IndexOf(head, PdfSignature, 0) >= 0 Then Return DetectedKind.Pdf
            Return DetectedKind.Unknown
        End Function


        Private Shared Function ExtensionOf(sourcePath As String) As String
            Try
                Return If(Path.GetExtension(sourcePath), String.Empty)
            Catch ex As ArgumentException
                Return String.Empty
            End Try
        End Function


        ' ---- Office Open XML and OpenDocument ---------------------------------

        Private Shared Function InspectPackage(source As FileStream, budget As ReadBudget, found As FindingCollector) As Boolean

            source.Position = 0

            Using archive As New ZipArchive(source, ZipArchiveMode.Read, leaveOpen:=True)

                Dim parts As New Dictionary(Of String, ZipArchiveEntry)(StringComparer.OrdinalIgnoreCase)
                For Each entry As ZipArchiveEntry In archive.Entries
                    Dim key As String = entry.FullName.Replace("\"c, "/"c).TrimStart("/"c)
                    If Not parts.ContainsKey(key) Then parts(key) = entry
                Next

                If parts.ContainsKey("[Content_Types].xml") Then
                    ReadPart(parts, "docProps/core.xml", budget, found, AddressOf ReadCoreProperties)
                    ReadPart(parts, "docProps/app.xml", budget, found, AddressOf ReadAppProperties)
                    ReadPart(parts, "word/comments.xml", budget, found, AddressOf ReadCommentAuthors)
                    ReadPart(parts, "word/document.xml", budget, found, AddressOf ReadTrackedChangeAuthors)
                    Return True
                End If

                If parts.ContainsKey("mimetype") OrElse parts.ContainsKey("meta.xml") Then
                    ReadPart(parts, "meta.xml", budget, found, AddressOf ReadOpenDocumentMeta)
                    Return True
                End If

                Return False

            End Using

        End Function


        Private Shared Sub ReadPart(
            parts As Dictionary(Of String, ZipArchiveEntry),
            partName As String,
            budget As ReadBudget,
            found As FindingCollector,
            reading As Action(Of XmlReader, FindingCollector)
        )

            Dim entry As ZipArchiveEntry = Nothing
            If Not parts.TryGetValue(partName, entry) Then Return

            budget.Token.ThrowIfCancellationRequested()
            If budget.Remaining <= 0 Then
                budget.ReachedLimit = True
                Return
            End If

            Dim settings As New XmlReaderSettings With {
                .DtdProcessing = DtdProcessing.Prohibit,
                .XmlResolver = Nothing,
                .MaxCharactersInDocument = 32L * 1024 * 1024,
                .IgnoreComments = True,
                .IgnoreProcessingInstructions = True,
                .CloseInput = True
            }

            Try
                Using limited As New BudgetStream(entry.Open(), budget)
                    Using reader As XmlReader = XmlReader.Create(limited, settings)
                        reading(reader, found)
                    End Using
                End Using
            Catch ex As XmlException When budget.ReachedLimit
                ' The part was cut off at the byte limit: keep what was read.
            End Try

        End Sub


        Private Shared Sub ReadCoreProperties(reader As XmlReader, found As FindingCollector)
            Do While Not reader.EOF
                If reader.NodeType = XmlNodeType.Element Then
                    If reader.LocalName = "creator" AndAlso reader.NamespaceURI = DcNamespace Then
                        found.Add("Author", ReadElementText(reader))
                        Continue Do
                    End If
                    If reader.LocalName = "lastModifiedBy" AndAlso reader.NamespaceURI = CoreNamespace Then
                        found.Add("Last saved by", ReadElementText(reader))
                        Continue Do
                    End If
                End If
                reader.Read()
            Loop
        End Sub


        Private Shared Sub ReadAppProperties(reader As XmlReader, found As FindingCollector)
            Do While Not reader.EOF
                If reader.NodeType = XmlNodeType.Element AndAlso reader.NamespaceURI = AppNamespace Then
                    If reader.LocalName = "Company" Then
                        found.Add("Company", ReadElementText(reader))
                        Continue Do
                    End If
                    If reader.LocalName = "Template" Then
                        ' A bare name such as Normal.dotm says nothing; a path can.
                        Dim template As String = ReadElementText(reader)
                        If template.IndexOfAny({"\"c, "/"c, ":"c}) >= 0 Then found.Add("Template", template)
                        Continue Do
                    End If
                End If
                reader.Read()
            Loop
        End Sub


        Private Shared Sub ReadCommentAuthors(reader As XmlReader, found As FindingCollector)
            Do While reader.Read()
                If reader.NodeType = XmlNodeType.Element AndAlso
                   reader.LocalName = "comment" AndAlso
                   reader.NamespaceURI = WordNamespace Then
                    found.Add("Comment authors", reader.GetAttribute("author", WordNamespace))
                End If
            Loop
        End Sub


        Private Shared Sub ReadTrackedChangeAuthors(reader As XmlReader, found As FindingCollector)
            Do While reader.Read()
                If reader.NodeType = XmlNodeType.Element AndAlso
                   reader.NamespaceURI = WordNamespace AndAlso
                   (TrackedChangeElements.Contains(reader.LocalName) OrElse reader.LocalName.EndsWith("PrChange", StringComparison.Ordinal)) Then
                    found.Add("Tracked changes by", reader.GetAttribute("author", WordNamespace))
                End If
            Loop
        End Sub


        Private Shared Sub ReadOpenDocumentMeta(reader As XmlReader, found As FindingCollector)
            Do While Not reader.EOF
                If reader.NodeType = XmlNodeType.Element Then
                    If reader.LocalName = "initial-creator" AndAlso reader.NamespaceURI = OdfMetaNamespace Then
                        found.Add("Author", ReadElementText(reader))
                        Continue Do
                    End If
                    If reader.LocalName = "creator" AndAlso reader.NamespaceURI = DcNamespace Then
                        found.Add("Last saved by", ReadElementText(reader))
                        Continue Do
                    End If
                End If
                reader.Read()
            Loop
        End Sub


        ' The element's text, leaving the reader on the node after its end tag.
        Private Shared Function ReadElementText(reader As XmlReader) As String

            If reader.IsEmptyElement Then
                reader.Read()
                Return String.Empty
            End If

            Dim depth As Integer = reader.Depth
            Dim content As New StringBuilder()
            reader.Read()

            Do While Not reader.EOF AndAlso
                     Not (reader.NodeType = XmlNodeType.EndElement AndAlso reader.Depth = depth)
                Select Case reader.NodeType
                    Case XmlNodeType.Text, XmlNodeType.CDATA, XmlNodeType.Whitespace, XmlNodeType.SignificantWhitespace
                        If content.Length < MaximumPdfString Then content.Append(reader.Value)
                End Select
                reader.Read()
            Loop

            If Not reader.EOF Then reader.Read()
            Return content.ToString()

        End Function


        ' ---- PDF ---------------------------------------------------------------

        ' Reports the Info dictionary's /Author and XMP dc:creator, from the raw
        ' bytes and from compressed metadata and object streams. /Creator,
        ' /Producer, pdf:Producer, and xmp:CreatorTool name software, so they
        ' are not reported.
        Private Shared Sub InspectPdf(source As FileStream, budget As ReadBudget, found As FindingCollector)

            Dim parts As New List(Of Byte())()
            Dim fileLength As Long = source.Length

            If fileLength <= budget.Limit Then
                parts.Add(ReadRange(source, 0, CInt(fileLength)))
            Else
                ' The trailer and Info dictionary usually sit near the end.
                Dim half As Integer = CInt(budget.Limit \ 2)
                parts.Add(ReadRange(source, 0, half))
                parts.Add(ReadRange(source, fileLength - half, half))
                budget.ReachedLimit = True
            End If

            ' A PDF cut short has no end-of-file marker near its end.
            Dim tail As Byte() = parts(parts.Count - 1)
            If IndexOf(tail, PdfEndOfFile, Math.Max(0, tail.Length - 2048)) < 0 Then
                Throw New InvalidDataException("The PDF has no end-of-file marker.")
            End If

            For Each part As Byte() In parts
                ScanPdfText(part, found)
                ScanPdfStreams(part, budget, found)
            Next

        End Sub


        Private Shared Sub ScanPdfText(data As Byte(), found As FindingCollector)
            For Each value As String In PdfStringValues(data, PdfAuthorKey)
                found.Add("Author", value)
            Next
            FindXmpCreators(data, 0, data.Length, found)
        End Sub


        ' Every string value that directly follows the key, such as /Author (...).
        Private Shared Function PdfStringValues(data As Byte(), key As Byte()) As List(Of String)

            Dim values As New List(Of String)()
            Dim position As Integer = 0

            Do
                Dim index As Integer = IndexOf(data, key, position)
                If index < 0 Then Exit Do
                position = index + key.Length
                If position < data.Length AndAlso IsPdfRegular(data(position)) Then Continue Do

                Dim cursor As Integer = SkipPdfWhitespace(data, position)
                If cursor >= data.Length Then Exit Do

                Dim raw As Byte() = Nothing
                If data(cursor) = AscW("("c) Then
                    raw = ReadPdfLiteral(data, cursor)
                ElseIf data(cursor) = AscW("<"c) AndAlso (cursor + 1 >= data.Length OrElse data(cursor + 1) <> AscW("<"c)) Then
                    raw = ReadPdfHex(data, cursor)
                End If

                If raw IsNot Nothing Then values.Add(DecodePdfText(raw))
                If values.Count >= 50 Then Exit Do
            Loop

            Return values

        End Function


        Private Shared Function IsPdfRegular(value As Byte) As Boolean
            Select Case value
                Case 0, 9, 10, 12, 13, 32,
                     AscW("("c), AscW(")"c), AscW("<"c), AscW(">"c), AscW("["c), AscW("]"c),
                     AscW("{"c), AscW("}"c), AscW("/"c), AscW("%"c)
                    Return False
                Case Else
                    Return True
            End Select
        End Function


        Private Shared Function IsPdfWhitespace(value As Byte) As Boolean
            Return value = 0 OrElse value = 9 OrElse value = 10 OrElse value = 12 OrElse value = 13 OrElse value = 32
        End Function


        Private Shared Function SkipPdfWhitespace(data As Byte(), start As Integer) As Integer
            Dim cursor As Integer = start
            Do While cursor < data.Length AndAlso IsPdfWhitespace(data(cursor))
                cursor += 1
            Loop
            Return cursor
        End Function


        ' A literal string "( ... )" with balanced parentheses and escapes;
        ' Nothing when it never closes.
        Private Shared Function ReadPdfLiteral(data As Byte(), start As Integer) As Byte()

            Dim output As New List(Of Byte)()
            Dim depth As Integer = 1
            Dim cursor As Integer = start + 1
            Dim stopAt As Integer = Math.Min(data.Length, start + 65536)

            Do While cursor < stopAt
                Dim current As Byte = data(cursor)

                If current = AscW("\"c) Then
                    cursor += 1
                    If cursor >= stopAt Then Return Nothing
                    Dim escaped As Byte = data(cursor)
                    Select Case escaped
                        Case AscW("n"c) : output.Add(10)
                        Case AscW("r"c) : output.Add(13)
                        Case AscW("t"c) : output.Add(9)
                        Case AscW("b"c) : output.Add(8)
                        Case AscW("f"c) : output.Add(12)
                        Case 13
                            ' A backslash at the end of a line continues the string.
                            If cursor + 1 < stopAt AndAlso data(cursor + 1) = 10 Then cursor += 1
                        Case 10
                            ' Continuation, as above.
                        Case AscW("0"c) To AscW("7"c)
                            Dim octal As Integer = escaped - AscW("0"c)
                            Dim digits As Integer = 1
                            Do While digits < 3 AndAlso cursor + 1 < stopAt AndAlso
                                     data(cursor + 1) >= AscW("0"c) AndAlso data(cursor + 1) <= AscW("7"c)
                                cursor += 1
                                octal = octal * 8 + (data(cursor) - AscW("0"c))
                                digits += 1
                            Loop
                            output.Add(CByte(octal And &HFF))
                        Case Else
                            ' \( \) \\ and unknown escapes: the character itself.
                            output.Add(escaped)
                    End Select
                ElseIf current = AscW("("c) Then
                    depth += 1
                    output.Add(current)
                ElseIf current = AscW(")"c) Then
                    depth -= 1
                    If depth = 0 Then Return If(output.Count > MaximumPdfString, output.Take(MaximumPdfString).ToArray(), output.ToArray())
                    output.Add(current)
                Else
                    output.Add(current)
                End If

                cursor += 1
            Loop

            Return Nothing

        End Function


        ' A hex string "< ... >"; whitespace is ignored and an odd digit count
        ' is padded with 0. Nothing when it isn't one.
        Private Shared Function ReadPdfHex(data As Byte(), start As Integer) As Byte()

            Dim digits As New StringBuilder()
            Dim cursor As Integer = start + 1

            Do While cursor < data.Length
                Dim current As Byte = data(cursor)
                If current = AscW(">"c) Then
                    If digits.Length Mod 2 = 1 Then digits.Append("0"c)
                    Return Convert.FromHexString(digits.ToString())
                End If
                If Not IsPdfWhitespace(current) Then
                    Dim character As Char = ChrW(current)
                    If Not Uri.IsHexDigit(character) Then Return Nothing
                    If digits.Length >= MaximumPdfString * 2 Then Return Nothing
                    digits.Append(character)
                End If
                cursor += 1
            Loop

            Return Nothing

        End Function


        ' UTF-16BE or UTF-8 with a byte order mark; otherwise Latin-1, close
        ' enough to PDFDocEncoding for names.
        Private Shared Function DecodePdfText(raw As Byte()) As String
            If raw.Length >= 2 AndAlso raw(0) = &HFE AndAlso raw(1) = &HFF Then
                Return Encoding.BigEndianUnicode.GetString(raw, 2, (raw.Length - 2) And Not 1)
            End If
            If raw.Length >= 3 AndAlso raw(0) = &HEF AndAlso raw(1) = &HBB AndAlso raw(2) = &HBF Then
                Return Encoding.UTF8.GetString(raw, 3, raw.Length - 3)
            End If
            Return Encoding.Latin1.GetString(raw)
        End Function


        ' Flate-compressed metadata and object streams: an Info dictionary or
        ' XMP packet can sit inside them.
        Private Shared Sub ScanPdfStreams(data As Byte(), budget As ReadBudget, found As FindingCollector)

            Dim position As Integer = 0

            Do
                budget.Token.ThrowIfCancellationRequested()

                Dim keyword As Integer = IndexOf(data, PdfStreamKeyword, position)
                If keyword < 0 Then Exit Do
                position = keyword + PdfStreamKeyword.Length

                If keyword >= 3 AndAlso
                   data(keyword - 3) = AscW("e"c) AndAlso data(keyword - 2) = AscW("n"c) AndAlso data(keyword - 1) = AscW("d"c) Then
                    Continue Do
                End If

                ' The data starts after "stream" and its end of line.
                Dim dataStart As Integer = position
                If dataStart < data.Length AndAlso data(dataStart) = 13 Then dataStart += 1
                If dataStart < data.Length AndAlso data(dataStart) = 10 Then dataStart += 1
                If dataStart = position Then Continue Do

                Dim dictionaryText As String = StreamDictionary(data, keyword)
                If dictionaryText Is Nothing Then Continue Do

                Dim dataEnd As Integer = StreamEnd(data, dictionaryText, dataStart)
                If dataEnd < dataStart Then Continue Do
                position = Math.Max(position, dataEnd)

                If dictionaryText.Contains("/FlateDecode") AndAlso StreamTypePattern.IsMatch(dictionaryText) Then
                    Dim inflated As Byte() = Inflate(data, dataStart, dataEnd - dataStart, MaximumInflatedStream, budget)
                    If inflated IsNot Nothing AndAlso inflated.Length > 0 Then ScanPdfText(inflated, found)
                End If
            Loop

        End Sub


        ' The stream's dictionary "<< ... >>", found from the object header
        ' before the "stream" keyword; Nothing when it can't be found.
        Private Shared Function StreamDictionary(data As Byte(), keyword As Integer) As String

            Dim windowStart As Integer = Math.Max(0, keyword - 16384)
            Dim header As Integer = LastIndexOf(data, PdfObjKeyword, keyword - 1, windowStart)
            Dim from As Integer = If(header >= 0, header + PdfObjKeyword.Length, windowStart)

            Dim cursor As Integer = from
            Do While cursor < keyword - 1
                If data(cursor) = AscW("<"c) AndAlso data(cursor + 1) = AscW("<"c) Then Exit Do
                cursor += 1
            Loop
            If cursor >= keyword - 1 Then Return Nothing

            Dim open As Integer = cursor
            Dim depth As Integer = 0

            Do While cursor < keyword - 1
                Dim current As Byte = data(cursor)
                If current = AscW("<"c) AndAlso data(cursor + 1) = AscW("<"c) Then
                    depth += 1
                    cursor += 2
                    Continue Do
                End If
                If current = AscW(">"c) AndAlso data(cursor + 1) = AscW(">"c) Then
                    depth -= 1
                    cursor += 2
                    If depth = 0 Then Return Encoding.Latin1.GetString(data, open, cursor - open)
                    Continue Do
                End If
                If current = AscW("("c) Then
                    ' Skip a literal string, so its contents can't unbalance the scan.
                    Dim nesting As Integer = 1
                    cursor += 1
                    Do While cursor < keyword AndAlso nesting > 0
                        If data(cursor) = AscW("\"c) Then
                            cursor += 1
                        ElseIf data(cursor) = AscW("("c) Then
                            nesting += 1
                        ElseIf data(cursor) = AscW(")"c) Then
                            nesting -= 1
                        End If
                        cursor += 1
                    Loop
                    Continue Do
                End If
                cursor += 1
            Loop

            Return Nothing

        End Function


        ' Where the stream's data ends: its direct /Length, or "endstream".
        Private Shared Function StreamEnd(data As Byte(), dictionaryText As String, dataStart As Integer) As Integer

            Dim lengthMatch As Match = StreamLengthPattern.Match(dictionaryText)
            If lengthMatch.Success AndAlso Not lengthMatch.Groups(2).Success Then
                Dim declared As Long
                If Long.TryParse(lengthMatch.Groups(1).Value, declared) AndAlso dataStart + declared <= data.Length Then
                    Return CInt(dataStart + declared)
                End If
            End If

            Dim finish As Integer = IndexOf(data, PdfEndStreamKeyword, dataStart)
            If finish < 0 Then Return -1
            If finish > dataStart AndAlso data(finish - 1) = 10 Then finish -= 1
            If finish > dataStart AndAlso data(finish - 1) = 13 Then finish -= 1
            Return finish

        End Function


        ' Inflates zlib data, at most "cap" bytes and what the budget allows.
        ' Data that doesn't inflate gives what was read before the error.
        Private Shared Function Inflate(data As Byte(), offset As Integer, count As Integer, cap As Integer, budget As ReadBudget) As Byte()

            Dim allowance As Long = Math.Min(cap, budget.InflateRemaining)
            If allowance <= 0 Then
                budget.ReachedLimit = True
                Return Nothing
            End If

            Using output As New MemoryStream()
                Try
                    Using compressed As New MemoryStream(data, offset, count, writable:=False)
                        Using inflater As New ZLibStream(compressed, CompressionMode.Decompress)
                            Dim buffer(ReadBufferSize - 1) As Byte
                            Do
                                budget.Token.ThrowIfCancellationRequested()
                                Dim wanted As Integer = CInt(Math.Min(buffer.Length, allowance - output.Length + 1))
                                Dim bytesRead As Integer = inflater.Read(buffer, 0, wanted)
                                If bytesRead = 0 Then Exit Do
                                output.Write(buffer, 0, bytesRead)
                                If output.Length > allowance Then
                                    budget.ReachedLimit = True
                                    output.SetLength(allowance)
                                    Exit Do
                                End If
                            Loop
                        End Using
                    End Using
                Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                    ' Damaged or not zlib data: keep what inflated, if anything.
                End Try

                budget.InflateRemaining -= output.Length
                Return output.ToArray()
            End Using

        End Function


        ' ---- XMP (in PDFs, JPEGs, and PNGs) --------------------------------------

        Private Shared Sub FindXmpCreators(data As Byte(), start As Integer, finish As Integer, found As FindingCollector)

            Dim position As Integer = start

            Do
                Dim open As Integer = IndexOf(data, XmpCreatorOpen, position, finish)
                If open < 0 Then Exit Do
                Dim afterName As Integer = open + XmpCreatorOpen.Length
                position = afterName
                If afterName >= finish OrElse Not IsXmlNameEnd(data(afterName)) Then Continue Do

                Dim close As Integer = IndexOf(data, XmpCreatorClose, afterName, finish)
                If close < 0 Then Exit Do
                position = close + XmpCreatorClose.Length

                Dim item As Integer = afterName
                Do
                    Dim itemOpen As Integer = IndexOf(data, XmpItemOpen, item, close)
                    If itemOpen < 0 Then Exit Do
                    Dim afterItemName As Integer = itemOpen + XmpItemOpen.Length
                    item = afterItemName
                    If afterItemName >= close OrElse Not IsXmlNameEnd(data(afterItemName)) Then Continue Do

                    Dim tagEnd As Integer = Array.IndexOf(data, CByte(AscW(">"c)), afterItemName, close - afterItemName)
                    If tagEnd < 0 Then Exit Do
                    item = tagEnd + 1
                    If data(tagEnd - 1) = AscW("/"c) Then Continue Do

                    Dim itemClose As Integer = IndexOf(data, XmpItemClose, tagEnd + 1, close)
                    If itemClose < 0 Then Exit Do
                    item = itemClose + XmpItemClose.Length

                    Dim raw As String = Encoding.UTF8.GetString(data, tagEnd + 1, Math.Min(itemClose - tagEnd - 1, MaximumPdfString))
                    found.Add("Author", WebUtility.HtmlDecode(raw))
                Loop
            Loop

        End Sub


        Private Shared Function IsXmlNameEnd(value As Byte) As Boolean
            Return value = AscW(">"c) OrElse value = AscW("/"c) OrElse value = 32 OrElse value = 9 OrElse value = 10 OrElse value = 13
        End Function


        ' ---- JPEG -------------------------------------------------------------

        Private Shared Sub InspectJpeg(source As FileStream, budget As ReadBudget, found As FindingCollector)

            source.Position = 2

            Do
                budget.Token.ThrowIfCancellationRequested()

                Dim marker As Integer = ReadJpegMarker(source)
                If marker = &HD9 OrElse marker = &HDA Then Exit Do
                If (marker >= &HD0 AndAlso marker <= &HD7) OrElse marker = 1 Then Continue Do

                Dim segmentLength As Integer = ReadUInt16BigEndian(source) - 2
                If segmentLength < 0 OrElse source.Position + segmentLength > source.Length Then
                    Throw New InvalidDataException("A JPEG segment runs past the end of the file.")
                End If

                If marker = &HE1 Then
                    If budget.Remaining < segmentLength Then
                        budget.ReachedLimit = True
                        Exit Do
                    End If
                    Dim segment As Byte() = ReadRange(source, source.Position, segmentLength)
                    budget.Remaining -= segmentLength

                    If StartsWith(segment, ExifHeader, 0) Then
                        ReadExif(segment, ExifHeader.Length, found)
                    ElseIf StartsWith(segment, JpegXmpHeader, 0) Then
                        FindXmpCreators(segment, JpegXmpHeader.Length, segment.Length, found)
                    End If
                Else
                    source.Seek(segmentLength, SeekOrigin.Current)
                End If
            Loop

        End Sub


        Private Shared Function ReadJpegMarker(source As FileStream) As Integer
            Dim first As Integer = source.ReadByte()
            If first <> &HFF Then Throw New InvalidDataException("A JPEG marker was expected.")
            Dim marker As Integer = source.ReadByte()
            Do While marker = &HFF
                marker = source.ReadByte()
            Loop
            If marker <= 0 Then Throw New InvalidDataException("A JPEG marker was expected.")
            Return marker
        End Function


        ' An Exif (TIFF) block: camera make and model, the artist, and whether
        ' a GPS location is recorded.
        Private Shared Sub ReadExif(data As Byte(), tiffStart As Integer, found As FindingCollector)

            Dim cameraDetails As String = String.Empty

            Try
                If tiffStart + 8 > data.Length Then Exit Try
                Dim littleEndian As Boolean
                If data(tiffStart) = AscW("I"c) AndAlso data(tiffStart + 1) = AscW("I"c) Then
                    littleEndian = True
                ElseIf data(tiffStart) = AscW("M"c) AndAlso data(tiffStart + 1) = AscW("M"c) Then
                    littleEndian = False
                Else
                    Exit Try
                End If
                If ReadUInt16(data, tiffStart + 2, littleEndian) <> 42 Then Exit Try

                Dim ifdStart As Long = tiffStart + CLng(ReadUInt32(data, tiffStart + 4, littleEndian))
                If ifdStart + 2 > data.Length Then Exit Try

                Dim entryCount As Integer = Math.Min(1000, ReadUInt16(data, CInt(ifdStart), littleEndian))
                Dim make As String = String.Empty
                Dim model As String = String.Empty

                For index As Integer = 0 To entryCount - 1
                    Dim entryStart As Long = ifdStart + 2 + 12L * index
                    If entryStart + 12 > data.Length Then Exit For
                    Dim tag As Integer = ReadUInt16(data, CInt(entryStart), littleEndian)
                    Select Case tag
                        Case &H10F : make = ReadExifAscii(data, tiffStart, CInt(entryStart), littleEndian)
                        Case &H110 : model = ReadExifAscii(data, tiffStart, CInt(entryStart), littleEndian)
                        Case &H13B : found.Add("Author", ReadExifAscii(data, tiffStart, CInt(entryStart), littleEndian))
                        Case &H8825 : found.Add("Location", "recorded")
                    End Select
                Next

                cameraDetails = (CleanValue(make) & " " & CleanValue(model)).Trim()
            Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                ' A damaged Exif block still means camera details are stored.
            End Try

            found.Add("Camera details", If(cameraDetails.Length > 0, cameraDetails, "recorded"))

        End Sub


        Private Shared Function ReadExifAscii(data As Byte(), tiffStart As Integer, entryStart As Integer, littleEndian As Boolean) As String
            If ReadUInt16(data, entryStart + 2, littleEndian) <> 2 Then Return String.Empty
            Dim count As Long = ReadUInt32(data, entryStart + 4, littleEndian)
            Dim valueStart As Long = If(count <= 4, entryStart + 8, tiffStart + CLng(ReadUInt32(data, entryStart + 8, littleEndian)))
            If count <= 0 OrElse count > MaximumPdfString OrElse valueStart + count > data.Length Then Return String.Empty
            Return Encoding.Latin1.GetString(data, CInt(valueStart), CInt(count)).TrimEnd(ChrW(0))
        End Function


        ' ---- PNG --------------------------------------------------------------

        Private Shared Sub InspectPng(source As FileStream, budget As ReadBudget, found As FindingCollector)

            source.Position = PngSignature.Length

            Do
                budget.Token.ThrowIfCancellationRequested()
                If source.Length - source.Position < 12 Then Throw New InvalidDataException("The PNG ends before its last chunk.")

                Dim chunkLength As Long = ReadUInt32BigEndian(source)
                Dim chunkType As String = Encoding.ASCII.GetString(ReadRange(source, source.Position, 4))
                If chunkLength > source.Length - source.Position - 4 Then Throw New InvalidDataException("A PNG chunk runs past the end of the file.")
                If chunkType = "IEND" Then Exit Do

                Select Case chunkType
                    Case "tEXt", "zTXt", "iTXt", "eXIf"
                        If budget.Remaining < chunkLength Then
                            budget.ReachedLimit = True
                            source.Seek(chunkLength + 4, SeekOrigin.Current)
                        Else
                            Dim chunk As Byte() = ReadRange(source, source.Position, CInt(chunkLength))
                            budget.Remaining -= chunkLength
                            source.Seek(4, SeekOrigin.Current)
                            ReadPngChunk(chunkType, chunk, budget, found)
                        End If
                    Case Else
                        source.Seek(chunkLength + 4, SeekOrigin.Current)
                End Select
            Loop

        End Sub


        Private Shared Sub ReadPngChunk(chunkType As String, chunk As Byte(), budget As ReadBudget, found As FindingCollector)

            If chunkType = "eXIf" Then
                ReadExif(chunk, 0, found)
                Return
            End If

            Dim keywordEnd As Integer = Array.IndexOf(chunk, CByte(0))
            If keywordEnd <= 0 Then Return
            Dim keyword As String = Encoding.Latin1.GetString(chunk, 0, keywordEnd)
            Dim isAuthor As Boolean = String.Equals(keyword, "Author", StringComparison.OrdinalIgnoreCase)
            Dim isXmp As Boolean = String.Equals(keyword, "XML:com.adobe.xmp", StringComparison.Ordinal)
            If Not isAuthor AndAlso Not isXmp Then Return

            Select Case chunkType
                Case "tEXt"
                    If isAuthor Then found.Add("Author", Encoding.Latin1.GetString(chunk, keywordEnd + 1, chunk.Length - keywordEnd - 1))

                Case "zTXt"
                    If isAuthor AndAlso keywordEnd + 2 <= chunk.Length Then
                        Dim inflated As Byte() = Inflate(chunk, keywordEnd + 2, chunk.Length - keywordEnd - 2, MaximumInflatedText, budget)
                        If inflated IsNot Nothing Then found.Add("Author", Encoding.Latin1.GetString(inflated))
                    End If

                Case "iTXt"
                    ' keyword NUL, compression flag, method, language NUL, translated keyword NUL, text
                    If keywordEnd + 3 > chunk.Length Then Return
                    Dim compressed As Boolean = chunk(keywordEnd + 1) = 1
                    Dim languageEnd As Integer = Array.IndexOf(chunk, CByte(0), keywordEnd + 3)
                    If languageEnd < 0 Then Return
                    Dim translatedEnd As Integer = Array.IndexOf(chunk, CByte(0), languageEnd + 1)
                    If translatedEnd < 0 Then Return
                    Dim textStart As Integer = translatedEnd + 1
                    Dim content As Byte()
                    If compressed Then
                        content = Inflate(chunk, textStart, chunk.Length - textStart, MaximumInflatedText, budget)
                        If content Is Nothing Then Return
                    Else
                        content = chunk.Skip(textStart).ToArray()
                    End If
                    If isAuthor Then
                        found.Add("Author", Encoding.UTF8.GetString(content))
                    Else
                        FindXmpCreators(content, 0, content.Length, found)
                    End If
            End Select

        End Sub


        ' ---- Shared helpers ------------------------------------------------------

        Private Shared Function ReadHead(source As FileStream, count As Integer) As Byte()
            Dim buffer(CInt(Math.Min(count, source.Length)) - 1) As Byte
            source.Position = 0
            source.ReadExactly(buffer, 0, buffer.Length)
            Return buffer
        End Function


        Private Shared Function ReadRange(source As FileStream, offset As Long, count As Integer) As Byte()
            Dim buffer(count - 1) As Byte
            source.Position = offset
            source.ReadExactly(buffer, 0, count)
            Return buffer
        End Function


        Private Shared Function ReadUInt16BigEndian(source As FileStream) As Integer
            Dim bytes As Byte() = ReadRange(source, source.Position, 2)
            Return (CInt(bytes(0)) << 8) Or bytes(1)
        End Function


        Private Shared Function ReadUInt32BigEndian(source As FileStream) As Long
            Dim bytes As Byte() = ReadRange(source, source.Position, 4)
            Return (CLng(bytes(0)) << 24) Or (CLng(bytes(1)) << 16) Or (CLng(bytes(2)) << 8) Or bytes(3)
        End Function


        Private Shared Function ReadUInt16(data As Byte(), offset As Integer, littleEndian As Boolean) As Integer
            If littleEndian Then Return CInt(data(offset)) Or (CInt(data(offset + 1)) << 8)
            Return (CInt(data(offset)) << 8) Or data(offset + 1)
        End Function


        Private Shared Function ReadUInt32(data As Byte(), offset As Integer, littleEndian As Boolean) As Long
            If littleEndian Then
                Return CLng(data(offset)) Or (CLng(data(offset + 1)) << 8) Or (CLng(data(offset + 2)) << 16) Or (CLng(data(offset + 3)) << 24)
            End If
            Return (CLng(data(offset)) << 24) Or (CLng(data(offset + 1)) << 16) Or (CLng(data(offset + 2)) << 8) Or data(offset + 3)
        End Function


        Private Shared Function StartsWith(data As Byte(), prefix As Byte(), offset As Integer) As Boolean
            If offset + prefix.Length > data.Length Then Return False
            For index As Integer = 0 To prefix.Length - 1
                If data(offset + index) <> prefix(index) Then Return False
            Next
            Return True
        End Function


        ' The first index of the pattern in data(start ..< finish), or -1.
        Private Shared Function IndexOf(data As Byte(), pattern As Byte(), start As Integer, Optional finish As Integer = -1) As Integer

            Dim limit As Integer = If(finish < 0, data.Length, Math.Min(finish, data.Length))
            Dim last As Integer = limit - pattern.Length
            Dim position As Integer = Math.Max(0, start)

            Do While position <= last
                position = Array.IndexOf(data, pattern(0), position, last - position + 1)
                If position < 0 Then Return -1
                If StartsWith(data, pattern, position) Then Return position
                position += 1
            Loop

            Return -1

        End Function


        ' The last index of the pattern starting at or before "from" and at or
        ' after "lowest", or -1.
        Private Shared Function LastIndexOf(data As Byte(), pattern As Byte(), from As Integer, lowest As Integer) As Integer
            Dim position As Integer = Math.Min(from, data.Length - pattern.Length)
            Do While position >= lowest
                position = Array.LastIndexOf(data, pattern(0), position, position - lowest + 1)
                If position < 0 Then Return -1
                If StartsWith(data, pattern, position) Then Return position
                position -= 1
            Loop
            Return -1
        End Function


        ' Trimmed, control characters and runs of whitespace made one space,
        ' and cut to a readable length.
        Friend Shared Function CleanValue(value As String) As String

            If String.IsNullOrEmpty(value) Then Return String.Empty

            Dim cleaned As New StringBuilder(value.Length)
            Dim pendingSpace As Boolean = False

            For Each character As Char In value
                If character = ChrW(0) Then Continue For
                If Char.IsWhiteSpace(character) OrElse Char.IsControl(character) Then
                    pendingSpace = cleaned.Length > 0
                    Continue For
                End If
                If pendingSpace Then cleaned.Append(" "c)
                pendingSpace = False
                cleaned.Append(character)
            Next

            Dim result As String = cleaned.ToString()
            If result.Length > MaximumValueLength Then
                Dim cut As Integer = MaximumValueLength
                If Char.IsHighSurrogate(result(cut - 1)) Then cut -= 1
                result = result.Substring(0, cut).TrimEnd() & "…"
            End If

            Return result

        End Function


        Private NotInheritable Class ReadBudget

            Public Sub New(limit As Long, token As CancellationToken)
                Me.Limit = limit
                Remaining = limit
                InflateRemaining = limit
                Me.Token = token
            End Sub

            Public ReadOnly Property Limit As Long

            ' Bytes a part or segment may still read.
            Public Property Remaining As Long

            ' Bytes compressed PDF and PNG data may still inflate to.
            Public Property InflateRemaining As Long

            Public Property ReachedLimit As Boolean

            Public ReadOnly Property Token As CancellationToken

        End Class


        ' Reads at most the budget's remaining bytes; notes when there was more.
        Private NotInheritable Class BudgetStream
            Inherits Stream

            Private ReadOnly _inner As Stream
            Private ReadOnly _budget As ReadBudget

            Public Sub New(inner As Stream, budget As ReadBudget)
                _inner = inner
                _budget = budget
            End Sub

            Public Overrides ReadOnly Property CanRead As Boolean
                Get
                    Return True
                End Get
            End Property

            Public Overrides ReadOnly Property CanSeek As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property CanWrite As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property Length As Long
                Get
                    Throw New NotSupportedException()
                End Get
            End Property

            Public Overrides Property Position As Long
                Get
                    Throw New NotSupportedException()
                End Get
                Set(value As Long)
                    Throw New NotSupportedException()
                End Set
            End Property

            Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
                _budget.Token.ThrowIfCancellationRequested()
                If count = 0 Then Return 0

                If _budget.Remaining <= 0 Then
                    Dim probe(0) As Byte
                    If _inner.Read(probe, 0, 1) > 0 Then _budget.ReachedLimit = True
                    Return 0
                End If

                Dim allowed As Integer = CInt(Math.Min(count, _budget.Remaining))
                Dim bytesRead As Integer = _inner.Read(buffer, offset, allowed)
                _budget.Remaining -= bytesRead
                Return bytesRead
            End Function

            Public Overrides Sub Flush()
            End Sub

            Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
                Throw New NotSupportedException()
            End Function

            Public Overrides Sub SetLength(value As Long)
                Throw New NotSupportedException()
            End Sub

            Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
                Throw New NotSupportedException()
            End Sub

            Protected Overrides Sub Dispose(disposing As Boolean)
                If disposing Then _inner.Dispose()
                MyBase.Dispose(disposing)
            End Sub

        End Class


        ' Values per label: cleaned, once each (ignoring case), at most ten.
        Private NotInheritable Class FindingCollector

            Private ReadOnly _values As New Dictionary(Of String, List(Of String))(StringComparer.Ordinal)

            Public Sub Add(findingLabel As String, value As String)
                Dim cleaned As String = CleanValue(value)
                If cleaned.Length = 0 Then Return

                Dim values As List(Of String) = Nothing
                If Not _values.TryGetValue(findingLabel, values) Then
                    values = New List(Of String)()
                    _values(findingLabel) = values
                End If

                If values.Count >= MaximumValuesPerLabel Then Return
                If values.Any(Function(item) String.Equals(item, cleaned, StringComparison.OrdinalIgnoreCase)) Then Return
                values.Add(cleaned)
            End Sub

            Public Function ToFindings() As List(Of HiddenMetadataFinding)
                Return LabelOrder.
                    Where(Function(item) _values.ContainsKey(item)).
                    Select(Function(item) New HiddenMetadataFinding(item, String.Join(", ", _values(item)), PersonLabels.Contains(item))).
                    ToList()
            End Function

        End Class

    End Class

End Namespace
