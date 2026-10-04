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
        ' Damaged, unreadable, or encrypted.
        CouldNotCheck
    End Enum


    ' What a check couldn't read, from least to most serious.
    Public Enum HiddenMetadataGap
        ' Everything the check looks for was read.
        None
        ' Part of the file is damaged or couldn't be read.
        PartUnreadable
        ' The file is encrypted, so most of what it holds can't be read.
        Encrypted
    End Enum


    Public NotInheritable Class HiddenMetadataFinding

        Public Sub New(label As String, value As String, namesPerson As Boolean)
            Me.Label = If(label, String.Empty)
            Me.Value = If(value, String.Empty)
            Me.NamesPerson = namesPerson
        End Sub

        ' "Author", "Last saved by", "Comment authors", "Tracked changes by",
        ' "Custom properties", "Company", "Template", "Camera details", or
        ' "Location".
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
            Optional reachedLimit As Boolean = False,
            Optional gap As HiddenMetadataGap = HiddenMetadataGap.None
        )
            Me.Findings = If(findings, Enumerable.Empty(Of HiddenMetadataFinding)()).
                Where(Function(item) item IsNot Nothing).
                ToList().
                AsReadOnly()

            ' A file that couldn't all be read never claims "None found".
            If state = HiddenMetadataState.Checked AndAlso gap <> HiddenMetadataGap.None AndAlso Me.Findings.Count = 0 Then
                state = HiddenMetadataState.CouldNotCheck
            End If

            Me.State = state
            Me.ReachedLimit = reachedLimit
            Me.Gap = gap
        End Sub

        Public ReadOnly Property State As HiddenMetadataState

        Public ReadOnly Property Findings As IReadOnlyList(Of HiddenMetadataFinding)

        ' Only part of a very large file was read.
        Public ReadOnly Property ReachedLimit As Boolean

        ' What couldn't be read: after the findings of a checked file, or why
        ' a file couldn't be checked.
        Public ReadOnly Property Gap As HiddenMetadataGap

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
                    If ReachedLimit Then result &= " (checked part of this large file)"
                    Select Case Gap
                        Case HiddenMetadataGap.Encrypted
                            result &= "; the rest couldn't be checked (encrypted)"
                        Case HiddenMetadataGap.PartUnreadable
                            result &= "; part of this file couldn't be checked"
                    End Select
                Case HiddenMetadataState.PlainType
                    result = "Not checked (no hidden fields for this type)"
                Case HiddenMetadataState.NotChecked
                    result = "Not checked"
                Case Else
                    result = If(Gap = HiddenMetadataGap.Encrypted, "Couldn't be checked (encrypted)", "Couldn't be checked")
            End Select

            Return result

        End Function

    End Class


    ' Reads document properties, comment and tracked-change authors, template
    ' paths, PDF Info and XMP authors, and photo EXIF, GPS, and text chunks.
    ' Never throws for a bad file (only when cancelled): a damaged or
    ' unreadable file is reported as "Couldn't be checked", keeping anything
    ' already found. Every loop moves forward through what was read, so a
    ' crafted file can't make a check take much longer than reading it.
    Public NotInheritable Class HiddenMetadataService

        Public Const DefaultByteLimit As Long = 64L * 1024 * 1024

        Private Const LargestLimit As Long = 1024L * 1024 * 1024
        Private Const MaximumValuesPerLabel As Integer = 10
        Private Const MaximumValueLength As Integer = 120
        Private Const MaximumInflatedStream As Integer = 8 * 1024 * 1024
        Private Const MaximumInflatedText As Integer = 1024 * 1024
        Private Const MaximumPdfString As Integer = 8192
        ' How far a PDF literal string is followed looking for its end.
        Private Const MaximumPdfLiteral As Integer = 65536
        ' At most this many compressed PDF streams, package parts, and
        ' indirect /Author values are read, so a crafted file can't multiply
        ' the work.
        Private Const MaximumPdfStreams As Integer = 4096
        Private Const MaximumPackageParts As Integer = 2000
        Private Const MaximumPdfReferences As Integer = 16
        Private Const ReadBufferSize As Integer = 81920

        Private Const DcNamespace As String = "http://purl.org/dc/elements/1.1/"
        Private Const CoreNamespace As String = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
        Private Const AppNamespace As String = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
        Private Const CustomNamespace As String = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"
        Private Const RelationshipsNamespace As String = "http://schemas.openxmlformats.org/package/2006/relationships"
        Private Const WordNamespace As String = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
        Private Const Word2012Namespace As String = "http://schemas.microsoft.com/office/word/2012/wordml"
        Private Const SpreadsheetNamespace As String = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
        Private Const ThreadedCommentNamespace As String = "http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments"
        Private Const PresentationNamespace As String = "http://schemas.openxmlformats.org/presentationml/2006/main"
        Private Const Presentation2012Namespace As String = "http://schemas.microsoft.com/office/powerpoint/2012/main"
        Private Const Presentation2018Namespace As String = "http://schemas.microsoft.com/office/powerpoint/2018/8/main"
        Private Const OdfOfficeNamespace As String = "urn:oasis:names:tc:opendocument:xmlns:office:1.0"
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
            "Author", "Last saved by", "Comment authors", "Tracked changes by", "Custom properties",
            "Company", "Template", "Camera details", "Location"
        }

        Private Shared ReadOnly PersonLabels As New HashSet(Of String)(StringComparer.Ordinal) From {
            "Author", "Last saved by", "Comment authors", "Tracked changes by", "Custom properties"
        }

        ' Custom property names that suggest a person, such as _AuthorEmail.
        Private Shared ReadOnly PersonPropertyWords As String() = {
            "author", "owner", "editor", "creator", "reviewer", "manager", "contact",
            "person", "user", "sender", "approver", "signer"
        }

        Private Shared ReadOnly PdfSignature As Byte() = Encoding.ASCII.GetBytes("%PDF-")
        Private Shared ReadOnly PdfEndOfFile As Byte() = Encoding.ASCII.GetBytes("%%EOF")
        Private Shared ReadOnly PdfAuthorKey As Byte() = Encoding.ASCII.GetBytes("/Author")
        Private Shared ReadOnly PdfEncryptKey As Byte() = Encoding.ASCII.GetBytes("/Encrypt")
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
            "/Type\s*/(Metadata|ObjStm)(?![A-Za-z0-9])", RegexOptions.CultureInvariant
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

            Dim found As New FindingCollector()
            Dim budget As ReadBudget = Nothing

            Try
                cancellationToken.ThrowIfCancellationRequested()
                If String.IsNullOrWhiteSpace(sourcePath) Then Return New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck)

                Dim extension As String = ExtensionOf(sourcePath)
                budget = New ReadBudget(Math.Min(LargestLimit, Math.Max(2L, byteLimit)), cancellationToken)

                Using source As New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, ReadBufferSize)

                    If source.Length = 0 Then
                        Return New HiddenMetadataReport(
                            If(PlainExtensions.Contains(extension), HiddenMetadataState.PlainType, HiddenMetadataState.Checked)
                        )
                    End If

                    Dim head As Byte() = ReadHead(source, 1024)

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

                    Return New HiddenMetadataReport(HiddenMetadataState.Checked, found.ToFindings(), budget.ReachedLimit, found.Gap)

                End Using

            Catch ex As OperationCanceledException When cancellationToken.IsCancellationRequested
                Throw
            Catch
                ' What was read before the damage still shows, with a note that
                ' the rest wasn't checked; with nothing found, "Couldn't be checked".
                found.NoteGap(HiddenMetadataGap.PartUnreadable)
                Return New HiddenMetadataReport(
                    HiddenMetadataState.Checked,
                    found.ToFindings(),
                    budget IsNot Nothing AndAlso budget.ReachedLimit,
                    found.Gap
                )
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

                Dim reads As New List(Of (Entry As ZipArchiveEntry, Reading As Action(Of XmlReader, FindingCollector)))()

                If parts.ContainsKey("[Content_Types].xml") Then
                    AddPart(reads, parts, "docProps/core.xml", AddressOf ReadCoreProperties)
                    AddPart(reads, parts, "docProps/app.xml", AddressOf ReadAppProperties)
                    AddPart(reads, parts, "docProps/custom.xml", AddressOf ReadCustomProperties)
                    For Each part As KeyValuePair(Of String, ZipArchiveEntry) In parts
                        If IsTemplateRelationshipsPart(part.Key) Then
                            AddPart(reads, part.Value, AddressOf ReadAttachedTemplate)
                        ElseIf IsOfficePeoplePart(part.Key) Then
                            AddPart(reads, part.Value, AddressOf ReadOfficePeople)
                        End If
                    Next
                ElseIf parts.ContainsKey("mimetype") OrElse parts.ContainsKey("meta.xml") Then
                    AddPart(reads, parts, "meta.xml", AddressOf ReadOpenDocumentMeta)
                    AddPart(reads, parts, "styles.xml", AddressOf ReadOpenDocumentPeople)
                    AddPart(reads, parts, "content.xml", AddressOf ReadOpenDocumentPeople)
                Else
                    Return False
                End If

                ' Small parts first, so one very large part (usually the body)
                ' can't use up the byte budget before the others are read.
                Dim ordered = reads.
                    OrderBy(Function(item) item.Entry.Length).
                    ThenBy(Function(item) item.Entry.FullName, StringComparer.Ordinal).
                    ToList()

                For index As Integer = 0 To ordered.Count - 1
                    If index >= MaximumPackageParts Then
                        budget.ReachedLimit = True
                        Exit For
                    End If
                    ReadPart(ordered(index).Entry, budget, found, ordered(index).Reading)
                Next

                Return True

            End Using

        End Function


        Private Shared Sub AddPart(
            reads As List(Of (Entry As ZipArchiveEntry, Reading As Action(Of XmlReader, FindingCollector))),
            parts As Dictionary(Of String, ZipArchiveEntry),
            partName As String,
            reading As Action(Of XmlReader, FindingCollector)
        )
            Dim entry As ZipArchiveEntry = Nothing
            If parts.TryGetValue(partName, entry) Then AddPart(reads, entry, reading)
        End Sub


        Private Shared Sub AddPart(
            reads As List(Of (Entry As ZipArchiveEntry, Reading As Action(Of XmlReader, FindingCollector))),
            entry As ZipArchiveEntry,
            reading As Action(Of XmlReader, FindingCollector)
        )
            reads.Add((entry, reading))
        End Sub


        ' word/_rels/settings.xml.rels: where an attached template lives.
        Private Shared Function IsTemplateRelationshipsPart(partName As String) As Boolean
            Return partName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) AndAlso
                partName.EndsWith("_rels/settings.xml.rels", StringComparison.OrdinalIgnoreCase)
        End Function


        ' Parts that can name people: Word's body, notes, headers, footers,
        ' comments, people list, and styles (also in a glossary); Excel's
        ' comments and people; PowerPoint's comment authors.
        Private Shared Function IsOfficePeoplePart(partName As String) As Boolean

            If Not partName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) Then Return False

            If partName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) Then
                Dim rest As String = partName.Substring("word/".Length)
                If rest.StartsWith("glossary/", StringComparison.OrdinalIgnoreCase) Then rest = rest.Substring("glossary/".Length)
                Return rest.IndexOf("/"c) < 0
            End If

            If partName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) Then
                Return partName.IndexOf("comment", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    partName.StartsWith("xl/persons/", StringComparison.OrdinalIgnoreCase)
            End If

            Return String.Equals(partName, "ppt/commentAuthors.xml", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(partName, "ppt/authors.xml", StringComparison.OrdinalIgnoreCase)

        End Function


        Private Shared Sub ReadPart(
            entry As ZipArchiveEntry,
            budget As ReadBudget,
            found As FindingCollector,
            reading As Action(Of XmlReader, FindingCollector)
        )

            budget.Token.ThrowIfCancellationRequested()
            If budget.Remaining <= 0 Then
                budget.ReachedLimit = True
                Return
            End If

            ' The byte budget, not the character cap, cuts a large part short:
            ' XML never decodes to more characters than it has bytes, so the
            ' cap is only a backstop. No DTDs, so no entity expansion.
            Dim settings As New XmlReaderSettings With {
                .DtdProcessing = DtdProcessing.Prohibit,
                .XmlResolver = Nothing,
                .MaxCharactersInDocument = budget.Limit + 4096,
                .MaxCharactersFromEntities = 1024 * 1024,
                .IgnoreComments = True,
                .IgnoreProcessingInstructions = True,
                .CloseInput = True
            }

            Dim limited As BudgetStream = Nothing
            Try
                limited = New BudgetStream(entry.Open(), budget)
                Using reader As XmlReader = XmlReader.Create(limited, settings)
                    reading(reader, found)
                End Using
            Catch ex As XmlException When limited IsNot Nothing AndAlso limited.Truncated
                ' The part was cut off at the byte limit: keep what was read.
            Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                ' A damaged part: keep what was read, and say the rest wasn't checked.
                found.NoteGap(HiddenMetadataGap.PartUnreadable)
            Finally
                limited?.Dispose()
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
                        If LooksLikePath(template) Then found.Add("Template", template)
                        Continue Do
                    End If
                End If
                reader.Read()
            Loop
        End Sub


        ' docProps/custom.xml: properties that name a person or hold an email
        ' address, such as the _AuthorEmail and _AuthorEmailDisplayName that
        ' Outlook adds when it sends a document.
        Private Shared Sub ReadCustomProperties(reader As XmlReader, found As FindingCollector)
            Do While Not reader.EOF
                If reader.NodeType = XmlNodeType.Element AndAlso
                   reader.LocalName = "property" AndAlso
                   reader.NamespaceURI = CustomNamespace Then
                    Dim propertyName As String = If(reader.GetAttribute("name"), String.Empty)
                    Dim propertyValue As String = ReadElementText(reader)
                    If IsPersonProperty(propertyName, propertyValue) Then found.Add("Custom properties", propertyValue)
                    Continue Do
                End If
                reader.Read()
            Loop
        End Sub


        Private Shared Function IsPersonProperty(propertyName As String, propertyValue As String) As Boolean
            If propertyName.EndsWith("By", StringComparison.OrdinalIgnoreCase) Then Return True
            If PersonPropertyWords.Any(Function(word) propertyName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) Then Return True
            Return LooksLikeEmail(propertyValue)
        End Function


        Private Shared Function LooksLikeEmail(value As String) As Boolean
            If String.IsNullOrEmpty(value) Then Return False
            Dim at As Integer = value.IndexOf("@"c)
            Return at > 0 AndAlso at + 2 < value.Length AndAlso value.IndexOf("."c, at + 2) > 0
        End Function


        ' The email address in an Office user id such as
        ' "S::name@example.org::1234"; empty when there is none.
        Private Shared Function EmailIn(userId As String) As String
            If String.IsNullOrEmpty(userId) Then Return String.Empty
            For Each segment As String In userId.Split("::")
                If LooksLikeEmail(segment) Then Return segment
            Next
            Return String.Empty
        End Function


        Private Shared Function LooksLikePath(value As String) As Boolean
            Return value IsNot Nothing AndAlso value.IndexOfAny({"\"c, "/"c, ":"c}) >= 0
        End Function


        ' word/_rels/settings.xml.rels: the full path of an attached template,
        ' which can include the Windows user name. docProps/app.xml usually
        ' holds only the template's file name.
        Private Shared Sub ReadAttachedTemplate(reader As XmlReader, found As FindingCollector)
            Do While reader.Read()
                If reader.NodeType = XmlNodeType.Element AndAlso
                   reader.LocalName = "Relationship" AndAlso
                   reader.NamespaceURI = RelationshipsNamespace AndAlso
                   If(reader.GetAttribute("Type"), String.Empty).EndsWith("/attachedTemplate", StringComparison.OrdinalIgnoreCase) Then
                    Dim template As String = TemplatePath(reader.GetAttribute("Target"))
                    If LooksLikePath(template) Then found.Add("Template", template)
                End If
            Loop
        End Sub


        ' "file:///C:\Users\name\Custom%20Office%20Templates\x.dotx" as
        ' "C:\Users\name\Custom Office Templates\x.dotx".
        Private Shared Function TemplatePath(target As String) As String
            If String.IsNullOrEmpty(target) Then Return String.Empty
            Dim decoded As String = Uri.UnescapeDataString(target)
            If decoded.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) Then decoded = decoded.Substring("file:///".Length)
            Return decoded
        End Function


        ' People in Word, Excel, and PowerPoint parts: comment authors, the
        ' people behind modern comments, and tracked-change authors.
        Private Shared Sub ReadOfficePeople(reader As XmlReader, found As FindingCollector)

            Do While Not reader.EOF

                If reader.NodeType <> XmlNodeType.Element Then
                    reader.Read()
                    Continue Do
                End If

                Select Case reader.NamespaceURI
                    Case WordNamespace
                        ' Comments and every kind of tracked change carry w:author.
                        Dim wordAuthor As String = If(reader.HasAttributes, reader.GetAttribute("author", WordNamespace), Nothing)
                        If wordAuthor IsNot Nothing Then
                            found.Add(If(reader.LocalName = "comment", "Comment authors", "Tracked changes by"), wordAuthor)
                        End If

                    Case Word2012Namespace
                        If reader.LocalName = "person" Then found.Add("Comment authors", reader.GetAttribute("author", Word2012Namespace))
                        If reader.LocalName = "presenceInfo" Then found.Add("Comment authors", EmailIn(reader.GetAttribute("userId", Word2012Namespace)))

                    Case SpreadsheetNamespace
                        If reader.LocalName = "author" Then
                            Dim cellNoteAuthor As String = ReadElementText(reader)
                            ' Excel's stand-in author for a threaded comment is "tc={id}".
                            If Not cellNoteAuthor.StartsWith("tc=", StringComparison.OrdinalIgnoreCase) Then found.Add("Comment authors", cellNoteAuthor)
                            Continue Do
                        End If

                    Case ThreadedCommentNamespace
                        If reader.LocalName = "person" Then
                            found.Add("Comment authors", reader.GetAttribute("displayName"))
                            found.Add("Comment authors", EmailIn(reader.GetAttribute("userId")))
                        End If

                    Case PresentationNamespace
                        If reader.LocalName = "cmAuthor" Then found.Add("Comment authors", reader.GetAttribute("name"))

                    Case Presentation2012Namespace
                        If reader.LocalName = "presenceInfo" Then found.Add("Comment authors", EmailIn(reader.GetAttribute("userId")))

                    Case Presentation2018Namespace
                        If reader.LocalName = "author" Then
                            found.Add("Comment authors", reader.GetAttribute("name"))
                            found.Add("Comment authors", EmailIn(reader.GetAttribute("userId")))
                        End If
                End Select

                reader.Read()

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


        ' OpenDocument content.xml and styles.xml: dc:creator inside an
        ' office:change-info is a tracked change's author; anywhere else
        ' (office:annotation, or officeooo:annotation in a presentation) it
        ' is a comment's author.
        Private Shared Sub ReadOpenDocumentPeople(reader As XmlReader, found As FindingCollector)

            Dim openChangeInfo As Integer = 0

            Do While Not reader.EOF
                Select Case reader.NodeType
                    Case XmlNodeType.Element
                        If reader.LocalName = "change-info" AndAlso reader.NamespaceURI = OdfOfficeNamespace AndAlso Not reader.IsEmptyElement Then
                            openChangeInfo += 1
                        ElseIf reader.LocalName = "creator" AndAlso reader.NamespaceURI = DcNamespace Then
                            found.Add(If(openChangeInfo > 0, "Tracked changes by", "Comment authors"), ReadElementText(reader))
                            Continue Do
                        End If
                    Case XmlNodeType.EndElement
                        If reader.LocalName = "change-info" AndAlso reader.NamespaceURI = OdfOfficeNamespace Then
                            openChangeInfo = Math.Max(0, openChangeInfo - 1)
                        End If
                End Select
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

        ' Reports the Info dictionary's /Author (a string or a reference to
        ' one) and XMP dc:creator, from the raw bytes and from compressed
        ' metadata and object streams. /Creator, /Producer, pdf:Producer, and
        ' xmp:CreatorTool name software, so they are not reported. In an
        ' encrypted file the strings and streams are ciphertext: they are never
        ' shown as names, and the report says the file is encrypted.
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

            Dim encrypted As Boolean = parts.Any(Function(part) IsEncryptedPdf(part, budget.Token))
            Dim scanner As New PdfScanner(budget, found, encrypted)

            For Each part As Byte() In parts
                scanner.ScanText(part)
                scanner.ScanStreams(part)
            Next
            scanner.ResolveReferences(parts)

            If encrypted Then found.NoteGap(HiddenMetadataGap.Encrypted)

        End Sub


        ' The trailer, or a cross-reference stream's dictionary, names an
        ' /Encrypt dictionary, directly or as a reference such as "5 0 R".
        Private Shared Function IsEncryptedPdf(data As Byte(), token As CancellationToken) As Boolean

            Dim position As Integer = 0

            Do
                token.ThrowIfCancellationRequested()
                Dim index As Integer = IndexOf(data, PdfEncryptKey, position)
                If index < 0 Then Return False
                position = index + PdfEncryptKey.Length
                ' Not /EncryptMetadata or another longer name.
                If position < data.Length AndAlso IsPdfRegular(data(position)) Then Continue Do

                Dim cursor As Integer = SkipPdfWhitespace(data, position)
                position = cursor
                If cursor + 1 < data.Length AndAlso data(cursor) = AscW("<"c) AndAlso data(cursor + 1) = AscW("<"c) Then Return True
                Dim reference As (Long, Integer) = Nothing
                Dim finish As Integer
                If TryReadPdfReference(data, cursor, reference, finish) Then Return True
            Loop

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


        Private Shared Function IsDigit(value As Byte) As Boolean
            Return value >= AscW("0"c) AndAlso value <= AscW("9"c)
        End Function


        Private Shared Function SkipPdfWhitespace(data As Byte(), start As Integer) As Integer
            Dim cursor As Integer = start
            Do While cursor < data.Length AndAlso IsPdfWhitespace(data(cursor))
                cursor += 1
            Loop
            Return cursor
        End Function


        ' A literal or hex string at cursor, or Nothing. "position" moves past
        ' every byte looked at, so no byte is walked twice.
        Private Shared Function ReadPdfString(data As Byte(), cursor As Integer, ByRef position As Integer) As Byte()

            If cursor >= data.Length Then Return Nothing

            Dim finish As Integer = cursor
            Dim raw As Byte() = Nothing
            If data(cursor) = AscW("("c) Then
                raw = ReadPdfLiteral(data, cursor, finish)
            ElseIf data(cursor) = AscW("<"c) AndAlso (cursor + 1 >= data.Length OrElse data(cursor + 1) <> AscW("<"c)) Then
                raw = ReadPdfHex(data, cursor, finish)
            End If

            position = Math.Max(position, finish)
            Return raw

        End Function


        ' A literal string "( ... )" with balanced parentheses and escapes;
        ' Nothing when it doesn't close within reach. "finish" is where the
        ' walk stopped.
        Private Shared Function ReadPdfLiteral(data As Byte(), start As Integer, ByRef finish As Integer) As Byte()

            Dim output As New List(Of Byte)()
            Dim depth As Integer = 1
            Dim cursor As Integer = start + 1
            Dim stopAt As Integer = CInt(Math.Min(data.Length, CLng(start) + MaximumPdfLiteral))

            Do While cursor < stopAt
                Dim current As Byte = data(cursor)

                If current = AscW("\"c) Then
                    cursor += 1
                    If cursor >= stopAt Then Exit Do
                    Dim escaped As Byte = data(cursor)
                    Select Case escaped
                        Case AscW("n"c) : Append(output, 10)
                        Case AscW("r"c) : Append(output, 13)
                        Case AscW("t"c) : Append(output, 9)
                        Case AscW("b"c) : Append(output, 8)
                        Case AscW("f"c) : Append(output, 12)
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
                            Append(output, CByte(octal And &HFF))
                        Case Else
                            ' \( \) \\ and unknown escapes: the character itself.
                            Append(output, escaped)
                    End Select
                ElseIf current = AscW("("c) Then
                    depth += 1
                    Append(output, current)
                ElseIf current = AscW(")"c) Then
                    depth -= 1
                    If depth = 0 Then
                        finish = cursor + 1
                        Return output.ToArray()
                    End If
                    Append(output, current)
                Else
                    Append(output, current)
                End If

                cursor += 1
            Loop

            finish = Math.Min(cursor, data.Length)
            Return Nothing

        End Function


        Private Shared Sub Append(output As List(Of Byte), value As Byte)
            If output.Count < MaximumPdfString Then output.Add(value)
        End Sub


        ' A hex string "< ... >"; whitespace is ignored and an odd digit count
        ' is padded with 0. Nothing when it isn't one. "finish" is where the
        ' walk stopped.
        Private Shared Function ReadPdfHex(data As Byte(), start As Integer, ByRef finish As Integer) As Byte()

            Dim digits As New StringBuilder()
            Dim cursor As Integer = start + 1

            Do While cursor < data.Length
                Dim current As Byte = data(cursor)
                If current = AscW(">"c) Then
                    finish = cursor + 1
                    If digits.Length Mod 2 = 1 Then digits.Append("0"c)
                    Return Convert.FromHexString(digits.ToString())
                End If
                If Not IsPdfWhitespace(current) Then
                    Dim character As Char = ChrW(current)
                    If Not Uri.IsHexDigit(character) OrElse digits.Length >= MaximumPdfString * 2 Then Exit Do
                    digits.Append(character)
                End If
                cursor += 1
            Loop

            finish = cursor
            Return Nothing

        End Function


        ' An indirect reference such as "12 0 R" starting at start.
        Private Shared Function TryReadPdfReference(
            data As Byte(),
            start As Integer,
            ByRef reference As (Long, Integer),
            ByRef finish As Integer
        ) As Boolean

            Dim cursor As Integer = start
            Dim number As Long
            Dim generation As Long
            If Not TryReadPdfInteger(data, cursor, 10, number) Then Return False
            If Not SkipSomeWhitespace(data, cursor, 1) Then Return False
            If Not TryReadPdfInteger(data, cursor, 5, generation) Then Return False
            If Not SkipSomeWhitespace(data, cursor, 1) Then Return False
            If cursor >= data.Length OrElse data(cursor) <> AscW("R"c) Then Return False
            cursor += 1
            If cursor < data.Length AndAlso IsPdfRegular(data(cursor)) Then Return False

            reference = (number, CInt(generation))
            finish = cursor
            Return True

        End Function


        ' The "12 0" before the "obj" keyword at keyword.
        Private Shared Function TryReadObjectHeader(data As Byte(), keyword As Integer, ByRef reference As (Long, Integer)) As Boolean

            Dim cursor As Integer = keyword - 1
            Dim number As Long
            Dim generation As Long
            If Not SkipSomeWhitespace(data, cursor, -1) Then Return False
            If Not TryReadPdfIntegerBackward(data, cursor, 5, generation) Then Return False
            If Not SkipSomeWhitespace(data, cursor, -1) Then Return False
            If Not TryReadPdfIntegerBackward(data, cursor, 10, number) Then Return False

            reference = (number, CInt(generation))
            Return True

        End Function


        ' Digits from cursor forward (at most maximumDigits), moving past them.
        Private Shared Function TryReadPdfInteger(data As Byte(), ByRef cursor As Integer, maximumDigits As Integer, ByRef value As Long) As Boolean
            Dim digits As Integer = 0
            value = 0
            Do While cursor < data.Length AndAlso IsDigit(data(cursor))
                If digits = maximumDigits Then Return False
                value = value * 10 + (data(cursor) - AscW("0"c))
                digits += 1
                cursor += 1
            Loop
            Return digits > 0
        End Function


        ' Digits from cursor backward (at most maximumDigits), moving before them.
        Private Shared Function TryReadPdfIntegerBackward(data As Byte(), ByRef cursor As Integer, maximumDigits As Integer, ByRef value As Long) As Boolean
            Dim digits As Integer = 0
            Dim place As Long = 1
            value = 0
            Do While cursor >= 0 AndAlso IsDigit(data(cursor))
                If digits = maximumDigits Then Return False
                value += (data(cursor) - AscW("0"c)) * place
                place *= 10
                digits += 1
                cursor -= 1
            Loop
            Return digits > 0
        End Function


        ' One to 32 whitespace bytes from cursor, forward (step 1) or
        ' backward (step -1), moving past them.
        Private Shared Function SkipSomeWhitespace(data As Byte(), ByRef cursor As Integer, [step] As Integer) As Boolean
            Dim skipped As Integer = 0
            Do While cursor >= 0 AndAlso cursor < data.Length AndAlso IsPdfWhitespace(data(cursor)) AndAlso skipped < 32
                cursor += [step]
                skipped += 1
            Loop
            Return skipped > 0
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


        ' The stream's dictionary "<< ... >>", found from the object header
        ' before the "stream" keyword and never before "floor" (the end of
        ' the previous stream), so each byte is looked at once; Nothing when
        ' it can't be found.
        Private Shared Function StreamDictionary(data As Byte(), keyword As Integer, floor As Integer) As String

            Dim windowStart As Integer = Math.Max(floor, keyword - 16384)
            If windowStart >= keyword - 1 Then Return Nothing

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


        ' Where the stream's data ends by its direct /Length; -1 without one.
        Private Shared Function DeclaredStreamEnd(dictionaryText As String, dataStart As Integer, dataLength As Integer) As Integer
            Dim lengthMatch As Match = StreamLengthPattern.Match(dictionaryText)
            If lengthMatch.Success AndAlso Not lengthMatch.Groups(2).Success Then
                Dim declared As Long
                If Long.TryParse(lengthMatch.Groups(1).Value, declared) AndAlso dataStart + declared <= dataLength Then
                    Return CInt(dataStart + declared)
                End If
            End If
            Return -1
        End Function


        ' Inflates zlib data, at most "cap" bytes and what the budget allows.
        ' Data that doesn't inflate gives what was read before the error, and
        ' sets "failed".
        Private Shared Function Inflate(
            data As Byte(),
            offset As Integer,
            count As Integer,
            cap As Integer,
            budget As ReadBudget,
            ByRef failed As Boolean
        ) As Byte()

            failed = False
            Dim allowance As Long = Math.Min(cap, budget.InflateRemaining)
            If allowance <= 0 Then
                budget.ReachedLimit = True
                Return Nothing
            End If

            Using output As New MemoryStream()
                Try
                    Using compressed As New MemoryStream(data, offset, count, writable:=False)
                        Using inflater As New ZLibStream(compressed, CompressionMode.Decompress)
                            Dim buffer As Byte() = budget.ScratchBuffer()
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
                    failed = True
                End Try

                budget.InflateRemaining -= output.Length
                Return output.ToArray()
            End Using

        End Function


        ' One forward pass over a PDF's bytes for /Author values, XMP
        ' creators, and compressed metadata and object streams. No byte is
        ' scanned more than a few times, whatever the file holds, and every
        ' loop stops when the check is cancelled.
        Private NotInheritable Class PdfScanner

            Private ReadOnly _budget As ReadBudget
            Private ReadOnly _found As FindingCollector
            Private ReadOnly _encrypted As Boolean
            ' Objects an /Author names by reference, such as /Author 12 0 R.
            Private ReadOnly _references As New HashSet(Of (Long, Integer))()
            Private _referencesDropped As Boolean
            Private _streamsInflated As Integer

            Public Sub New(budget As ReadBudget, found As FindingCollector, encrypted As Boolean)
                _budget = budget
                _found = found
                _encrypted = encrypted
            End Sub


            ' Info /Author values and XMP creators in plain or inflated bytes.
            Public Sub ScanText(data As Byte())
                ' An encrypted file's strings are ciphertext, never names.
                If Not _encrypted Then ReadAuthorValues(data)
                FindXmpCreators(data, 0, data.Length, _found, _budget.Token)
            End Sub


            ' Every /Author value: a literal or hex string, or a reference to one.
            Private Sub ReadAuthorValues(data As Byte())

                Dim position As Integer = 0
                Dim values As Integer = 0

                Do
                    _budget.Token.ThrowIfCancellationRequested()
                    Dim index As Integer = IndexOf(data, PdfAuthorKey, position)
                    If index < 0 Then Exit Do
                    position = index + PdfAuthorKey.Length
                    If position < data.Length AndAlso IsPdfRegular(data(position)) Then Continue Do

                    Dim cursor As Integer = SkipPdfWhitespace(data, position)
                    position = cursor

                    Dim raw As Byte() = ReadPdfString(data, cursor, position)
                    If raw IsNot Nothing Then
                        _found.Add("Author", DecodePdfText(raw))
                        values += 1
                        If values >= 50 Then Exit Do
                        Continue Do
                    End If

                    Dim reference As (Long, Integer) = Nothing
                    If TryReadPdfReference(data, cursor, reference, position) Then AddReference(reference)
                Loop

            End Sub


            Private Sub AddReference(reference As (Long, Integer))
                If _references.Contains(reference) Then Return
                If _references.Count >= MaximumPdfReferences Then
                    _referencesDropped = True
                    Return
                End If
                _references.Add(reference)
            End Sub


            ' Flate-compressed metadata and object streams: an Info dictionary
            ' or XMP packet can sit inside them.
            Public Sub ScanStreams(data As Byte())

                Dim position As Integer = 0
                ' A stream's dictionary lies after the previous stream.
                Dim floor As Integer = 0
                ' The last search for "endstream", where it started, and where
                ' it found one (-1: none from there on). A later stream that
                ' starts before that find gets the same answer without a search.
                Dim endSearchFrom As Integer = -1
                Dim endFound As Integer = -1

                Do
                    _budget.Token.ThrowIfCancellationRequested()

                    Dim keyword As Integer = IndexOf(data, PdfStreamKeyword, position)
                    If keyword < 0 Then Exit Do
                    position = keyword + PdfStreamKeyword.Length

                    If keyword >= 3 AndAlso
                       data(keyword - 3) = AscW("e"c) AndAlso data(keyword - 2) = AscW("n"c) AndAlso data(keyword - 1) = AscW("d"c) Then
                        floor = position
                        Continue Do
                    End If

                    ' The data starts after "stream" and its end of line.
                    Dim dataStart As Integer = position
                    If dataStart < data.Length AndAlso data(dataStart) = 13 Then dataStart += 1
                    If dataStart < data.Length AndAlso data(dataStart) = 10 Then dataStart += 1
                    If dataStart = position Then Continue Do

                    Dim dictionaryText As String = StreamDictionary(data, keyword, floor)
                    floor = position
                    If dictionaryText Is Nothing Then Continue Do

                    Dim dataEnd As Integer = DeclaredStreamEnd(dictionaryText, dataStart, data.Length)
                    If dataEnd < 0 Then
                        If endSearchFrom < 0 OrElse dataStart < endSearchFrom OrElse (endFound >= 0 AndAlso dataStart > endFound) Then
                            endSearchFrom = dataStart
                            endFound = IndexOf(data, PdfEndStreamKeyword, dataStart)
                        End If
                        If endFound < 0 Then Continue Do
                        dataEnd = endFound
                        If dataEnd > dataStart AndAlso data(dataEnd - 1) = 10 Then dataEnd -= 1
                        If dataEnd > dataStart AndAlso data(dataEnd - 1) = 13 Then dataEnd -= 1
                    End If

                    position = Math.Max(position, dataEnd)
                    floor = position

                    Dim streamType As Match = StreamTypePattern.Match(dictionaryText)
                    If Not streamType.Success OrElse Not dictionaryText.Contains("/FlateDecode") Then Continue Do
                    ' An encrypted file's object streams are encrypted too; its
                    ' metadata may not be.
                    If _encrypted AndAlso streamType.Groups(1).Value <> "Metadata" Then Continue Do

                    If _streamsInflated >= MaximumPdfStreams Then
                        _budget.ReachedLimit = True
                        Exit Do
                    End If
                    _streamsInflated += 1

                    Dim failed As Boolean
                    Dim inflated As Byte() = Inflate(data, dataStart, dataEnd - dataStart, MaximumInflatedStream, _budget, failed)
                    ' A stream that may hold the Info dictionary or XMP couldn't
                    ' be read: the file must not read as "None found".
                    If failed AndAlso Not _encrypted Then _found.NoteGap(HiddenMetadataGap.PartUnreadable)
                    If inflated IsNot Nothing AndAlso inflated.Length > 0 Then ScanText(inflated)
                Loop

            End Sub


            ' Looks up the strings that /Author references name, in one pass
            ' over each part. A reference that can't be found means an author
            ' wasn't checked.
            Public Sub ResolveReferences(parts As IEnumerable(Of Byte()))

                Dim resolved As New HashSet(Of (Long, Integer))()

                If _references.Count > 0 Then
                    For Each data As Byte() In parts
                        Dim position As Integer = 0
                        Do
                            _budget.Token.ThrowIfCancellationRequested()
                            Dim keyword As Integer = IndexOf(data, PdfObjKeyword, position)
                            If keyword < 0 Then Exit Do
                            position = keyword + PdfObjKeyword.Length
                            If position < data.Length AndAlso IsPdfRegular(data(position)) Then Continue Do

                            Dim reference As (Long, Integer) = Nothing
                            If Not TryReadObjectHeader(data, keyword, reference) OrElse Not _references.Contains(reference) Then Continue Do

                            Dim cursor As Integer = SkipPdfWhitespace(data, position)
                            position = cursor
                            Dim raw As Byte() = ReadPdfString(data, cursor, position)
                            If raw IsNot Nothing Then
                                _found.Add("Author", DecodePdfText(raw))
                                resolved.Add(reference)
                            End If
                        Loop
                    Next
                End If

                If _referencesDropped OrElse resolved.Count < _references.Count Then
                    _found.NoteGap(HiddenMetadataGap.PartUnreadable)
                End If

            End Sub

        End Class


        ' ---- XMP (in PDFs, JPEGs, and PNGs) --------------------------------------

        Private Shared Sub FindXmpCreators(data As Byte(), start As Integer, finish As Integer, found As FindingCollector, token As CancellationToken)

            Dim position As Integer = start

            Do
                token.ThrowIfCancellationRequested()

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
                        FindXmpCreators(segment, JpegXmpHeader.Length, segment.Length, found, budget.Token)
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

            Dim failed As Boolean

            Select Case chunkType
                Case "tEXt"
                    If isAuthor Then found.Add("Author", Encoding.Latin1.GetString(chunk, keywordEnd + 1, chunk.Length - keywordEnd - 1))

                Case "zTXt"
                    If isAuthor AndAlso keywordEnd + 2 <= chunk.Length Then
                        Dim inflated As Byte() = Inflate(chunk, keywordEnd + 2, chunk.Length - keywordEnd - 2, MaximumInflatedText, budget, failed)
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
                        content = Inflate(chunk, textStart, chunk.Length - textStart, MaximumInflatedText, budget, failed)
                        If content Is Nothing Then Return
                    Else
                        content = chunk.Skip(textStart).ToArray()
                    End If
                    If isAuthor Then
                        found.Add("Author", Encoding.UTF8.GetString(content))
                    Else
                        FindXmpCreators(content, 0, content.Length, found, budget.Token)
                    End If
            End Select

            ' A compressed author or XMP packet that won't inflate wasn't checked.
            If failed Then found.NoteGap(HiddenMetadataGap.PartUnreadable)

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

            Private _scratch As Byte()

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

            ' One buffer for every inflate in a check.
            Public Function ScratchBuffer() As Byte()
                If _scratch Is Nothing Then _scratch = New Byte(ReadBufferSize - 1) {}
                Return _scratch
            End Function

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

            ' This part was cut off at the byte limit.
            Public Property Truncated As Boolean

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
                    If _inner.Read(probe, 0, 1) > 0 Then
                        _budget.ReachedLimit = True
                        Truncated = True
                    End If
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


        ' Values per label: cleaned, once each (ignoring case), at most ten;
        ' and the most serious thing that couldn't be read.
        Private NotInheritable Class FindingCollector

            Private ReadOnly _values As New Dictionary(Of String, List(Of String))(StringComparer.Ordinal)
            Private _gap As HiddenMetadataGap = HiddenMetadataGap.None

            Public ReadOnly Property Gap As HiddenMetadataGap
                Get
                    Return _gap
                End Get
            End Property

            Public Sub NoteGap(kind As HiddenMetadataGap)
                If kind > _gap Then _gap = kind
            End Sub

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
