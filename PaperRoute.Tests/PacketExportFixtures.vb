Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Net
Imports System.Security
Imports System.Text
Imports System.Text.Json
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Synthetic files with known hidden information, a packet whose records
' carry SECRET- markers in every field a packet export (#45) must leave
' out, and a deep scan of a written package. The documents' own hidden
' information uses HIDDEN- markers, which do travel inside the files.
Friend Module PacketExportFixtures

    Friend ReadOnly ExportTime As New DateTime(2026, 10, 3, 16, 0, 0, DateTimeKind.Utc)
    Friend Const TestVersion As String = "0.9.0-test"
    Friend Const ExampleTitle As String = "Example: anchoring effects in clinical risk estimates"
    Friend Const ExamplePacketLabel As String = "Revision 2 to Fictional Journal of Psychology"
    Friend Const ExampleJournal As String = "Fictional Journal of Psychology"
    Friend Const ValidOrcid As String = "0000-0002-1825-0097"

    Private Const WordNamespace As String = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


    ' ---- Synthetic files --------------------------------------------------------

    Friend Sub WriteDocx(
        target As String,
        creator As String,
        lastModifiedBy As String,
        Optional company As String = "",
        Optional templatePath As String = "",
        Optional commentAuthor As String = "",
        Optional trackedAuthor As String = "",
        Optional body As String = "Synthetic document for PaperRoute tests.",
        Optional extraCommentAuthors As IEnumerable(Of String) = Nothing
    )

        Using output As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
            Using archive As New ZipArchive(output, ZipArchiveMode.Create)
                AddText(archive, "[Content_Types].xml",
                    "<?xml version=""1.0"" encoding=""UTF-8""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" &
                    "<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" &
                    "<Default Extension=""xml"" ContentType=""application/xml""/>" &
                    "<Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>" &
                    "<Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>" &
                    "<Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/></Types>")
                AddText(archive, "_rels/.rels",
                    "<?xml version=""1.0"" encoding=""UTF-8""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                    "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>" &
                    "<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>" &
                    "<Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/></Relationships>")

                Dim tracked As String = String.Empty
                If trackedAuthor.Length > 0 Then
                    tracked = "<w:ins w:id=""1"" w:author=""" & Xml(trackedAuthor) & """><w:r><w:t>added</w:t></w:r></w:ins>" &
                        "<w:del w:id=""2"" w:author=""" & Xml(trackedAuthor) & """><w:r><w:delText>removed</w:delText></w:r></w:del>"
                End If
                AddText(archive, "word/document.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><w:document xmlns:w=""" & WordNamespace & """><w:body><w:p><w:r><w:t>" &
                    Xml(body) & "</w:t></w:r>" & tracked & "</w:p></w:body></w:document>")

                Dim commenters As New List(Of String)()
                If commentAuthor.Length > 0 Then commenters.Add(commentAuthor)
                If extraCommentAuthors IsNot Nothing Then commenters.AddRange(extraCommentAuthors)
                If commenters.Count > 0 Then
                    Dim comments As New StringBuilder()
                    comments.Append("<?xml version=""1.0"" encoding=""UTF-8""?><w:comments xmlns:w=""").Append(WordNamespace).Append(""">")
                    For index As Integer = 0 To commenters.Count - 1
                        comments.Append("<w:comment w:id=""").Append(index).Append(""" w:author=""").Append(Xml(commenters(index))).Append(""" w:initials=""X""><w:p><w:r><w:t>A comment.</w:t></w:r></w:p></w:comment>")
                    Next
                    comments.Append("</w:comments>")
                    AddText(archive, "word/comments.xml", comments.ToString())
                End If

                AddText(archive, "docProps/core.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" " &
                    "xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:dcterms=""http://purl.org/dc/terms/""><dc:title>Synthetic</dc:title>" &
                    If(creator.Length > 0, "<dc:creator>" & Xml(creator) & "</dc:creator>", String.Empty) &
                    If(lastModifiedBy.Length > 0, "<cp:lastModifiedBy>" & Xml(lastModifiedBy) & "</cp:lastModifiedBy>", String.Empty) &
                    "</cp:coreProperties>")
                AddText(archive, "docProps/app.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"">" &
                    If(templatePath.Length > 0, "<Template>" & Xml(templatePath) & "</Template>", String.Empty) &
                    "<Application>Synthetic Writer</Application>" &
                    If(company.Length > 0, "<Company>" & Xml(company) & "</Company>", String.Empty) &
                    "</Properties>")
            End Using
        End Using

    End Sub


    Friend Sub WriteOdt(target As String, initialCreator As String, creator As String)
        Using output As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
            Using archive As New ZipArchive(output, ZipArchiveMode.Create)
                AddText(archive, "mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression)
                AddText(archive, "META-INF/manifest.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8""?><manifest:manifest xmlns:manifest=""urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"" manifest:version=""1.2"">" &
                    "<manifest:file-entry manifest:full-path=""/"" manifest:media-type=""application/vnd.oasis.opendocument.text""/>" &
                    "<manifest:file-entry manifest:full-path=""content.xml"" manifest:media-type=""text/xml""/>" &
                    "<manifest:file-entry manifest:full-path=""meta.xml"" manifest:media-type=""text/xml""/></manifest:manifest>")
                AddText(archive, "content.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8""?><office:document-content xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" " &
                    "xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" office:version=""1.2""><office:body><office:text><text:p>Synthetic supplement.</text:p></office:text></office:body></office:document-content>")
                AddText(archive, "meta.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8""?><office:document-meta xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" " &
                    "xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" office:version=""1.2""><office:meta>" &
                    If(initialCreator.Length > 0, "<meta:initial-creator>" & Xml(initialCreator) & "</meta:initial-creator>", String.Empty) &
                    If(creator.Length > 0, "<dc:creator>" & Xml(creator) & "</dc:creator>", String.Empty) &
                    "<meta:generator>Synthetic Writer</meta:generator></office:meta></office:document-meta>")
            End Using
        End Using
    End Sub


    Friend Sub WritePdf(
        target As String,
        Optional infoAuthorLiteral As String = Nothing,
        Optional infoAuthorHexUtf16 As String = Nothing,
        Optional xmpCreatorFlate As String = Nothing,
        Optional objStmAuthor As String = Nothing,
        Optional producer As String = Nothing,
        Optional padding As Integer = 0
    )
        File.WriteAllBytes(target, BuildPdf(infoAuthorLiteral, infoAuthorHexUtf16, xmpCreatorFlate, objStmAuthor, producer, padding))
    End Sub


    ' A small, well-formed PDF built by hand. infoAuthorLiteral is written
    ' as is between ( and ), so it may hold PDF escapes. Padding puts that
    ' many bytes of comments before and after the Info dictionary.
    Friend Function BuildPdf(
        Optional infoAuthorLiteral As String = Nothing,
        Optional infoAuthorHexUtf16 As String = Nothing,
        Optional xmpCreatorFlate As String = Nothing,
        Optional objStmAuthor As String = Nothing,
        Optional producer As String = Nothing,
        Optional padding As Integer = 0
    ) As Byte()

        Dim latin1 As Encoding = Encoding.Latin1
        Dim output As New MemoryStream()
        Dim offsets As New SortedDictionary(Of Integer, Long)()

        Dim add = Sub(number As Integer, body As Byte())
                      offsets(number) = output.Length
                      Dim header As Byte() = latin1.GetBytes(number.ToString() & " 0 obj" & vbLf)
                      output.Write(header, 0, header.Length)
                      output.Write(body, 0, body.Length)
                      Dim footer As Byte() = latin1.GetBytes(vbLf & "endobj" & vbLf)
                      output.Write(footer, 0, footer.Length)
                  End Sub
        Dim addText = Sub(number As Integer, body As String) add(number, latin1.GetBytes(body))
        Dim addStream = Sub(number As Integer, dictionary As String, data As Byte())
                            Dim start As Byte() = latin1.GetBytes("<< " & dictionary & " /Length " & data.Length.ToString() & " >>" & vbLf & "stream" & vbCrLf)
                            Dim finish As Byte() = latin1.GetBytes(vbCrLf & "endstream")
                            add(number, start.Concat(data).Concat(finish).ToArray())
                        End Sub
        Dim pad = Sub()
                      If padding <= 0 Then Return
                      Dim line As String = "%" & New String("P"c, 69) & vbLf
                      Dim written As Integer = 0
                      Do While written < padding
                          Dim bytes As Byte() = latin1.GetBytes(line)
                          output.Write(bytes, 0, bytes.Length)
                          written += bytes.Length
                      Loop
                  End Sub

        Dim headerBytes As Byte() = {&H25, &H50, &H44, &H46, &H2D, &H31, &H2E, &H37, &HA, &H25, &HE2, &HE3, &HCF, &HD3, &HA}
        output.Write(headerBytes, 0, headerBytes.Length)

        Dim hasXmp As Boolean = Not String.IsNullOrEmpty(xmpCreatorFlate)
        addText(1, "<< /Type /Catalog /Pages 2 0 R" & If(hasXmp, " /Metadata 6 0 R", "") & " >>")
        addText(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>")
        addText(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>")
        addStream(4, "", latin1.GetBytes("BT /F1 12 Tf 72 720 Td (Synthetic test page) Tj ET"))

        pad()
        Dim info As New StringBuilder("<< /Title (Synthetic test)")
        If infoAuthorLiteral IsNot Nothing Then info.Append(" /Author (").Append(infoAuthorLiteral).Append(")")
        Dim hexAuthor As String = Nothing
        If infoAuthorHexUtf16 IsNot Nothing Then
            hexAuthor = "<FEFF" & Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(infoAuthorHexUtf16)) & ">"
            If infoAuthorLiteral Is Nothing Then info.Append(" /Author ").Append(hexAuthor)
        End If
        If producer IsNot Nothing Then info.Append(" /Producer (").Append(producer).Append(") /Creator (").Append(producer).Append(")")
        info.Append(" >>")
        addText(5, info.ToString())
        pad()

        If hasXmp Then
            Dim xmp As String =
                "<?xpacket begin="""" id=""W5M0MpCehiHzreSzNTczkc9d""?><x:xmpmeta xmlns:x=""adobe:ns:meta/""><rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">" &
                "<rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:pdf=""http://ns.adobe.com/pdf/1.3/"" xmlns:xmp=""http://ns.adobe.com/xap/1.0/"">" &
                "<dc:creator><rdf:Seq><rdf:li>" & Xml(xmpCreatorFlate) & "</rdf:li></rdf:Seq></dc:creator>" &
                If(producer IsNot Nothing, "<pdf:Producer>" & Xml(producer) & "</pdf:Producer><xmp:CreatorTool>" & Xml(producer) & "</xmp:CreatorTool>", "") &
                "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=""w""?>"
            addStream(6, "/Type /Metadata /Subtype /XML /Filter /FlateDecode", Zlib(Encoding.UTF8.GetBytes(xmp)))
        End If

        If hexAuthor IsNot Nothing AndAlso infoAuthorLiteral IsNot Nothing Then
            addText(7, "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /Author " & hexAuthor & " >>")
        End If

        If Not String.IsNullOrEmpty(objStmAuthor) Then
            Dim objectText As String = "<< /Author (" & objStmAuthor & ") >>"
            Dim headerText As String = "9 0 "
            addStream(8, "/Type /ObjStm /N 1 /First " & headerText.Length.ToString() & " /Filter /FlateDecode", Zlib(latin1.GetBytes(headerText & objectText)))
        End If

        Dim xref As Long = output.Length
        Dim highest As Integer = offsets.Keys.Max()
        Dim table As New StringBuilder()
        table.Append("xref" & vbLf & "0 " & (highest + 1).ToString() & vbLf & "0000000000 65535 f " & vbCrLf)
        For number As Integer = 1 To highest
            Dim offset As Long = 0
            If offsets.TryGetValue(number, offset) Then
                table.Append(offset.ToString("0000000000") & " 00000 n " & vbCrLf)
            Else
                table.Append("0000000000 00000 f " & vbCrLf)
            End If
        Next
        table.Append("trailer" & vbLf & "<< /Size " & (highest + 1).ToString() & " /Root 1 0 R /Info 5 0 R >>" & vbLf & "startxref" & vbLf & xref.ToString() & vbLf & "%%EOF" & vbLf)
        Dim trailer As Byte() = latin1.GetBytes(table.ToString())
        output.Write(trailer, 0, trailer.Length)

        Return output.ToArray()

    End Function


    Friend Sub WriteJpeg(target As String, make As String, model As String, withGps As Boolean, Optional artist As String = "")
        File.WriteAllBytes(target, BuildJpeg(make, model, withGps, artist))
    End Sub


    ' SOI, an Exif APP1 segment (little-endian TIFF with Make, Model,
    ' optionally Artist, and a pointer to a one-entry GPS directory), EOI.
    Friend Function BuildJpeg(make As String, model As String, withGps As Boolean, Optional artist As String = "") As Byte()

        Dim strings As New List(Of (Tag As Integer, Value As Byte()))()
        If make.Length > 0 Then strings.Add((&H10F, Encoding.ASCII.GetBytes(make & ChrW(0))))
        If model.Length > 0 Then strings.Add((&H110, Encoding.ASCII.GetBytes(model & ChrW(0))))
        If artist.Length > 0 Then strings.Add((&H13B, Encoding.ASCII.GetBytes(artist & ChrW(0))))

        Dim entryCount As Integer = strings.Count + If(withGps, 1, 0)
        Dim ifd0Size As Integer = 2 + 12 * entryCount + 4
        Dim gpsOffset As Integer = 8 + ifd0Size
        Dim dataOffset As Integer = gpsOffset + If(withGps, 2 + 12 + 4, 0)

        Using tiff As New MemoryStream()
            Using writer As New BinaryWriter(tiff)
                writer.Write(New Byte() {&H49, &H49, &H2A, 0})
                writer.Write(8UI)
                writer.Write(CUShort(entryCount))

                Dim nextData As Integer = dataOffset
                For Each item In strings
                    writer.Write(CUShort(item.Tag))
                    writer.Write(CUShort(2))
                    writer.Write(CUInt(item.Value.Length))
                    If item.Value.Length <= 4 Then
                        Dim inline(3) As Byte
                        Array.Copy(item.Value, inline, item.Value.Length)
                        writer.Write(inline)
                    Else
                        writer.Write(CUInt(nextData))
                        nextData += item.Value.Length
                    End If
                Next
                If withGps Then
                    writer.Write(CUShort(&H8825))
                    writer.Write(CUShort(4))
                    writer.Write(1UI)
                    writer.Write(CUInt(gpsOffset))
                End If
                writer.Write(0UI)

                If withGps Then
                    writer.Write(CUShort(1))
                    writer.Write(CUShort(0))
                    writer.Write(CUShort(1))
                    writer.Write(4UI)
                    writer.Write(New Byte() {2, 3, 0, 0})
                    writer.Write(0UI)
                End If

                For Each item In strings
                    If item.Value.Length > 4 Then writer.Write(item.Value)
                Next
                writer.Flush()

                Dim exif As Byte() = {&H45, &H78, &H69, &H66, 0, 0}
                Dim payload As Byte() = exif.Concat(tiff.ToArray()).ToArray()
                Dim segmentLength As Integer = payload.Length + 2

                Dim jpeg As New List(Of Byte) From {&HFF, &HD8, &HFF, &HE1, CByte(segmentLength >> 8), CByte(segmentLength And &HFF)}
                jpeg.AddRange(payload)
                jpeg.AddRange(New Byte() {&HFF, &HD9})
                Return jpeg.ToArray()
            End Using
        End Using

    End Function


    Friend Sub WritePng(target As String, Optional textAuthor As String = "", Optional itxtAuthor As String = "", Optional shade As Byte = 128)
        File.WriteAllBytes(target, BuildPng(textAuthor, itxtAuthor, shade))
    End Sub


    ' A 1x1 RGB image with optional tEXt and iTXt Author chunks.
    Friend Function BuildPng(Optional textAuthor As String = "", Optional itxtAuthor As String = "", Optional shade As Byte = 128) As Byte()

        Dim output As New List(Of Byte) From {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}
        PngChunk(output, "IHDR", New Byte() {0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0})
        If textAuthor.Length > 0 Then
            PngChunk(output, "tEXt", Encoding.Latin1.GetBytes("Author" & ChrW(0) & textAuthor))
        End If
        If itxtAuthor.Length > 0 Then
            Dim chunk As New List(Of Byte)(Encoding.Latin1.GetBytes("Author"))
            chunk.AddRange(New Byte() {0, 0, 0, 0, 0})
            chunk.AddRange(Encoding.UTF8.GetBytes(itxtAuthor))
            PngChunk(output, "iTXt", chunk.ToArray())
        End If
        PngChunk(output, "IDAT", Zlib(New Byte() {0, shade, CByte(255 - shade), shade}))
        PngChunk(output, "IEND", Array.Empty(Of Byte)())
        Return output.ToArray()

    End Function


    Private Sub PngChunk(output As List(Of Byte), chunkType As String, data As Byte())
        Dim typeBytes As Byte() = Encoding.ASCII.GetBytes(chunkType)
        output.AddRange(BigEndian(CUInt(data.Length)))
        output.AddRange(typeBytes)
        output.AddRange(data)
        output.AddRange(BigEndian(Crc32(typeBytes.Concat(data).ToArray())))
    End Sub


    Private Function BigEndian(value As UInteger) As Byte()
        Return {CByte((value >> 24) And &HFFUI), CByte((value >> 16) And &HFFUI), CByte((value >> 8) And &HFFUI), CByte(value And &HFFUI)}
    End Function


    Private Function Crc32(data As Byte()) As UInteger
        Dim crc As UInteger = &HFFFFFFFFUI
        For Each value As Byte In data
            crc = crc Xor value
            For bit As Integer = 0 To 7
                crc = If((crc And 1UI) <> 0UI, (crc >> 1) Xor &HEDB88320UI, crc >> 1)
            Next
        Next
        Return crc Xor &HFFFFFFFFUI
    End Function


    Friend Function Zlib(data As Byte()) As Byte()
        Using output As New MemoryStream()
            Using compressor As New ZLibStream(output, CompressionLevel.Optimal, leaveOpen:=True)
                compressor.Write(data, 0, data.Length)
            End Using
            Return output.ToArray()
        End Using
    End Function


    Private Sub AddText(archive As ZipArchive, entryName As String, content As String, Optional level As CompressionLevel = CompressionLevel.Optimal)
        Dim entry As ZipArchiveEntry = archive.CreateEntry(entryName, level)
        Using target As Stream = entry.Open()
            Dim bytes As Byte() = New UTF8Encoding(False).GetBytes(content)
            target.Write(bytes, 0, bytes.Length)
        End Using
    End Sub


    Private Function Xml(value As String) As String
        Return SecurityElement.Escape(If(value, String.Empty))
    End Function


    ' ---- The example packet ------------------------------------------------------

    Friend NotInheritable Class ExportFixture
        Friend Property Root As String
        Friend Property SourceFolder As String
        Friend Property Manuscript As Manuscript
        Friend Property Library As AuthorLibraryData
        Friend Property Packet As SubmissionPacket
        Friend Property Version As ManuscriptVersion
        Friend Property Submission As JournalSubmission

        Friend Function FileWithRole(role As SubmissionPacketFileRole, Optional index As Integer = 0) As SubmissionPacketFile
            Return Packet.Files.Where(Function(item) item.Role = role).ElementAt(index)
        End Function

        ' Every id of a record the export reads.
        Friend Function RecordIds() As List(Of Guid)
            Dim ids As New List(Of Guid) From {Manuscript.Id, Packet.Id, Version.Id, Submission.Id}
            ids.AddRange(Packet.Files.Select(Function(item) item.Id))
            ids.AddRange(Library.Authors.Select(Function(item) item.Id))
            ids.AddRange(Library.Affiliations.Select(Function(item) item.Id))
            ids.AddRange(Library.Journals.Select(Function(item) item.Id))
            Return ids
        End Function
    End Class


    ' The packet of the RO-Crate evaluation: a revision 2 sent in round 2 of
    ' a submission dated 2026-03-02, with two authors, a DOI, an ISSN, two
    ' files named "Figure 1.png", a name with "#" and "%", a file changed
    ' since its fingerprint, one never fingerprinted, a cover letter, a
    ' response to reviewers, and a records-only checklist. With secrets,
    ' every field the export must leave out carries a SECRET- marker.
    Friend Function BuildExampleFixture(root As String, Optional withSecrets As Boolean = False) As ExportFixture

        Dim secret = Function(marker As String) If(withSecrets, "SECRET-" & marker, String.Empty)
        Dim folder As String = Path.Combine(root, If(withSecrets, "SECRET-STORE", "store"))
        Directory.CreateDirectory(folder)

        Dim library As New AuthorLibraryData()
        Dim brown As New AffiliationRecord With {
            .Institution = "Brown University",
            .City = secret("CITY"), .Region = secret("REGION"), .Country = secret("COUNTRY"), .Notes = secret("AFFILIATION-NOTE")
        }
        Dim fictional As New AffiliationRecord With {.Institution = "Fictional Institute of Psychology", .Department = "Department of Examples"}
        library.Affiliations.AddRange({brown, fictional})
        Dim carberry As New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .Orcid = ValidOrcid, .Notes = secret("AUTHOR-NOTE"), .IsMe = True}
        Dim placeholder As New AuthorRecord With {.GivenName = "Riley", .FamilyName = "Placeholder", .Notes = secret("AUTHOR-NOTE-2")}
        library.Authors.AddRange({carberry, placeholder})
        Dim journal As New JournalRecord With {
            .Name = ExampleJournal, .Issns = New List(Of String) From {"0000-0019"},
            .Notes = secret("JOURNAL-NOTE"),
            .SubmissionPortalUrl = If(withSecrets, "https://portal.example/SECRET-JOURNAL-PORTAL", ""),
            .HomepageUrl = If(withSecrets, "https://example.org/SECRET-HOMEPAGE", ""),
            .Publisher = secret("JOURNAL-PUBLISHER")
        }
        library.Journals.Add(journal)

        Dim manuscript As New Manuscript With {
            .Title = ExampleTitle,
            .CoAuthors = secret("COAUTHORS legacy text"),
            .ManuscriptUrl = If(withSecrets, "file:///C:/Users/SECRETUSER/draft.docx", ""),
            .FileDrawerReason = secret("FILEDRAWER"),
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Published,
            .TargetJournal = ExampleJournal
        }
        If withSecrets Then
            manuscript.Tags.Add("SECRET-TAG")
            manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Draft, .Note = "SECRET-HISTORY"})
            manuscript.RelatedLinks.Add(New ManuscriptExternalLink With {.Label = "SECRET-LINK-LABEL", .Url = "https://example.org/SECRET-LINK", .Notes = "SECRET-LINK-NOTE"})
            manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "SECRET-REMINDER", .Notes = "SECRET-REMINDER-NOTE"})
            manuscript.ReadinessProfiles.Add(New ManuscriptReadiness With {.JournalId = journal.Id, .JournalName = ExampleJournal, .Notes = "SECRET-READINESS"})
        End If
        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = carberry.Id, .AffiliationIds = New List(Of Guid) From {brown.Id}, .IsCorrespondingAuthor = True})
        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = placeholder.Id, .AffiliationIds = New List(Of Guid) From {fictional.Id}})

        With manuscript.Metadata
            .AbstractText = "A fictional study of anchoring in clinicians' risk estimates, made for testing."
            .Keywords = New List(Of String) From {"anchoring", "clinical judgment", "risk estimates"}
            .Doi = "10.5555/example.anchoring"
            .PublicationJournal = ExampleJournal
            .PublishedDate = New DateTime(2026, 1, 1)
            .PreprintDoi = If(withSecrets, "10.5555/SECRET-PREPRINT", "10.5555/example.preprint")
            .Publisher = secret("PUBLISHER")
            .Volume = secret("VOLUME")
            .Issue = secret("ISSUE")
            .Pages = secret("PAGES")
            .PublicationUrl = If(withSecrets, "https://example.org/SECRET-PUBLICATION", "")
            .PreprintUrl = If(withSecrets, "https://example.org/SECRET-PREPRINT-URL", "")
            If withSecrets Then .ExternalIdentifiers("pmid") = "SECRET-PMID"
        End With

        Dim version As New ManuscriptVersion With {
            .Label = "Revision 2",
            .CreatedDate = New DateTime(2026, 8, 20),
            .Notes = secret("VERSION-NOTE"),
            .LocalFilePath = If(withSecrets, Path.Combine(folder, "version_SECRETSTORE.docx"), "")
        }
        manuscript.Versions.Add(version)

        Dim submission As New JournalSubmission With {
            .JournalName = ExampleJournal,
            .JournalId = journal.Id,
            .SubmittedDate = New DateTime(2026, 3, 2, 9, 30, 0),
            .ManuscriptNumber = secret("MS-NUMBER FJP-2026-0142"),
            .Notes = secret("SUBMISSION-NOTE"),
            .PortalUrl = If(withSecrets, "https://portal.example/SECRET-PORTAL", "")
        }
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 5, 1), .Notes = secret("DECISION")}
        submission.Decisions.Add(decision)
        If withSecrets Then
            submission.Correspondence.Add(New CorrespondenceItem With {
                .Title = "SECRET-CORRESPONDENCE", .Notes = "SECRET-CORRESPONDENCE-NOTE",
                .LocalFilePath = "C:\Users\SECRETUSER\mail.eml", .SourceUrl = "https://mail.example/SECRET-SOURCE"
            })
            submission.ReviewerResponses.Add(New ReviewerResponseItem With {
                .DecisionId = decision.Id, .RevisionRoundNumber = 1,
                .ReviewerLabel = "SECRET-REVIEWER", .CommentText = "SECRET-COMMENT", .ActionText = "SECRET-ACTION",
                .ResponseText = "SECRET-RESPONSE", .ManuscriptLocation = "SECRET-LOCATION", .Notes = "SECRET-RESPONSE-NOTE"
            })
        End If
        manuscript.Submissions.Add(submission)

        Dim packet As New SubmissionPacket With {
            .Label = ExamplePacketLabel,
            .Notes = secret("PACKET-NOTE"),
            .JournalId = journal.Id,
            .JournalName = ExampleJournal,
            .ManuscriptVersionId = version.Id,
            .SubmissionId = submission.Id,
            .RevisionRoundNumber = 2
        }
        manuscript.SubmissionPackets.Add(packet)

        Dim stored = Function(stem As String, number As Integer, extension As String) _
            Path.Combine(folder, stem & If(withSecrets, "_SECRETSTORE" & number.ToString(), "_" & number.ToString()) & extension)

        Dim addFile = Function(role As SubmissionPacketFileRole, label As String, original As String, sourcePath As String, record As Boolean) As SubmissionPacketFile
                          Dim item As New SubmissionPacketFile With {
                              .Role = role, .Label = label, .OriginalFileName = original,
                              .LocalFilePath = sourcePath, .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy,
                              .Notes = secret("FILE-NOTE " & label)
                          }
                          If record Then SubmissionPacketIntegrityService.CaptureBaseline(item)
                          packet.Files.Add(item)
                          Return item
                      End Function

        Dim main As String = stored("main", 1, ".pdf")
        WritePdf(main, infoAuthorLiteral:="HIDDEN-PDF-AUTHOR", xmpCreatorFlate:="HIDDEN-PDF-XMP", producer:="Synthetic PDF Writer")
        addFile(SubmissionPacketFileRole.Manuscript, "Main text, revision 2", "Main text (revision 2).pdf", main, True)

        Dim title As String = stored("title", 2, ".docx")
        WriteDocx(title, "HIDDEN-DOCX-CREATOR", "HIDDEN-DOCX-SAVER", body:="Title page. Josiah Carberry, Brown University.")
        addFile(SubmissionPacketFileRole.TitlePage, "Title page", "Title page.docx", title, True)

        Dim figure As String = stored("fig", 3, ".png")
        WritePng(figure, textAuthor:="HIDDEN-PNG-AUTHOR", shade:=200)
        addFile(SubmissionPacketFileRole.Figure, "Figure 1", "Figure 1.png", figure, True)

        Dim recolored As String = stored("fig", 4, ".png")
        WritePng(recolored, shade:=40)
        addFile(SubmissionPacketFileRole.Figure, "Figure 1, recolored", "Figure 1.png", recolored, True)

        Dim table As String = stored("table", 5, ".csv")
        File.WriteAllText(table, "condition,estimate" & vbLf & "anchor high,0.42" & vbLf & "anchor low,0.31" & vbLf)
        addFile(SubmissionPacketFileRole.Table, "Table 1", "Table #1 (50% sample).csv", table, False)
        If withSecrets Then
            ' A downloaded file's Zone.Identifier stream can hold the portal link.
            File.WriteAllText(table & ":Zone.Identifier", "[ZoneTransfer]" & vbCrLf & "ZoneId=3" & vbCrLf & "HostUrl=https://portal.example/SECRET-ADS" & vbCrLf)
        End If

        Dim letter As String = stored("letter", 6, ".txt")
        File.WriteAllText(letter, "Dear Editor, please find our second revision attached. -- the authors" & vbLf)
        addFile(SubmissionPacketFileRole.CoverLetter, "Cover letter", "Cover letter.txt", letter, True)

        Dim response As String = stored("response", 7, ".txt")
        File.WriteAllText(response, If(withSecrets, "Response to SECRET-REVIEWER-IN-FILE: we revised the methods.", "Response to Reviewer 2: we revised the methods.") & vbLf)
        addFile(SubmissionPacketFileRole.ResponseToReviewers,
                If(withSecrets, "SECRET-RESPONSE-LABEL", "Response to reviewers"),
                If(withSecrets, "SECRET-RESPONSE-NAME.txt", "Response to reviewers.txt"),
                response, True)

        Dim data As String = stored("data", 8, ".txt")
        File.WriteAllText(data, "Data availability: the synthetic data are in table 1." & vbLf)
        addFile(SubmissionPacketFileRole.DataAvailability, "Data availability statement", "Data availability.txt", data, True)

        Dim supplementA As String = stored("suppa", 9, ".odt")
        WriteOdt(supplementA, "HIDDEN-ODT-CREATOR", "HIDDEN-ODT-SAVER")
        addFile(SubmissionPacketFileRole.Supplement, "Supplementary materials", "Supplement A.odt", supplementA, True)

        Dim supplementB As String = stored("suppb", 10, ".txt")
        File.WriteAllText(supplementB, "Supplementary note, as first recorded." & vbLf)
        addFile(SubmissionPacketFileRole.Supplement, "Supplementary note", "Supplement B.txt", supplementB, True)
        File.WriteAllText(supplementB, "Supplementary note, edited after the fingerprint was recorded." & vbLf)

        packet.Files.Add(New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.ReportingChecklist,
            .Label = If(withSecrets, "SECRET-LABEL-CHECKLIST", "Reporting checklist (entered in the portal)"),
            .Notes = secret("FILE-NOTE checklist"),
            .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
        })

        Return New ExportFixture With {
            .Root = root, .SourceFolder = folder, .Manuscript = manuscript, .Library = library,
            .Packet = packet, .Version = version, .Submission = submission
        }

    End Function


    ' ---- Reading a written package -------------------------------------------------

    Friend Function EntryBytes(zipPath As String, entryName As String) As Byte()
        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            Dim entry As ZipArchiveEntry = archive.GetEntry(entryName)
            If entry Is Nothing Then Return Nothing
            Using source As Stream = entry.Open()
                Using copy As New MemoryStream()
                    source.CopyTo(copy)
                    Return copy.ToArray()
                End Using
            End Using
        End Using
    End Function


    Friend Function EntryText(zipPath As String, entryName As String) As String
        Dim bytes As Byte() = EntryBytes(zipPath, entryName)
        Return If(bytes Is Nothing, Nothing, Encoding.UTF8.GetString(bytes))
    End Function


    Friend Function EntryNames(zipPath As String) As List(Of String)
        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            Return archive.Entries.Select(Function(item) item.FullName).ToList()
        End Using
    End Function


    ' Every entry; inside an entry that is itself a zip (a .docx or .odt),
    ' every inner entry; inside a PDF, every stream that inflates. Entry
    ' names are scanned too.
    Friend Function DeepScan(zipPath As String) As List(Of (Entry As String, Bytes As Byte()))

        Dim result As New List(Of (Entry As String, Bytes As Byte()))()

        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            For Each entry As ZipArchiveEntry In archive.Entries
                Dim bytes As Byte()
                Using source As Stream = entry.Open()
                    Using copy As New MemoryStream()
                        source.CopyTo(copy)
                        bytes = copy.ToArray()
                    End Using
                End Using

                result.Add((entry.FullName & " (name)", Encoding.UTF8.GetBytes(entry.FullName)))
                result.Add((entry.FullName, bytes))

                If bytes.Length >= 4 AndAlso bytes(0) = &H50 AndAlso bytes(1) = &H4B AndAlso bytes(2) = 3 AndAlso bytes(3) = 4 Then
                    Using inner As New ZipArchive(New MemoryStream(bytes), ZipArchiveMode.Read)
                        For Each part As ZipArchiveEntry In inner.Entries
                            Using partSource As Stream = part.Open()
                                Using copy As New MemoryStream()
                                    partSource.CopyTo(copy)
                                    result.Add((entry.FullName & " > " & part.FullName, copy.ToArray()))
                                End Using
                            End Using
                        Next
                    End Using
                End If

                If bytes.Length >= 5 AndAlso Encoding.ASCII.GetString(bytes, 0, 5) = "%PDF-" Then
                    Dim streamNumber As Integer = 0
                    For Each inflated As Byte() In PdfStreams(bytes)
                        streamNumber += 1
                        result.Add((entry.FullName & " > stream " & streamNumber.ToString(), inflated))
                    Next
                End If
            Next
        End Using

        Return result

    End Function


    ' Inflates every "stream ... endstream" that holds zlib data.
    Private Iterator Function PdfStreams(pdf As Byte()) As IEnumerable(Of Byte())
        Dim latin1 As String = Encoding.Latin1.GetString(pdf)
        Dim position As Integer = 0
        Do
            Dim start As Integer = latin1.IndexOf("stream", position, StringComparison.Ordinal)
            If start < 0 Then Exit Do
            position = start + 6
            If start >= 3 AndAlso latin1.Substring(start - 3, 3) = "end" Then Continue Do
            Dim dataStart As Integer = position
            If dataStart < latin1.Length AndAlso latin1(dataStart) = ChrW(13) Then dataStart += 1
            If dataStart < latin1.Length AndAlso latin1(dataStart) = ChrW(10) Then dataStart += 1
            Dim finish As Integer = latin1.IndexOf("endstream", dataStart, StringComparison.Ordinal)
            If finish < 0 Then Exit Do
            Dim inflated As Byte() = Nothing
            Try
                Using compressed As New MemoryStream(pdf, dataStart, finish - dataStart)
                    Using inflater As New ZLibStream(compressed, CompressionMode.Decompress)
                        Using copy As New MemoryStream()
                            inflater.CopyTo(copy)
                            inflated = copy.ToArray()
                        End Using
                    End Using
                End Using
            Catch ex As InvalidDataException
                inflated = Nothing
            End Try
            If inflated IsNot Nothing AndAlso inflated.Length > 0 Then Yield inflated
            position = finish + 9
        Loop
    End Function


    ' Where the marker appears in any common encoding (UTF-8, UTF-16LE,
    ' UTF-16BE, percent-encoded, JSON-escaped, or HTML-encoded); Nothing
    ' when it appears nowhere.
    Friend Function FindMarker(scan As IEnumerable(Of (Entry As String, Bytes As Byte())), marker As String) As String

        Dim forms As New List(Of Byte()) From {
            Encoding.UTF8.GetBytes(marker),
            Encoding.Unicode.GetBytes(marker),
            Encoding.BigEndianUnicode.GetBytes(marker),
            Encoding.UTF8.GetBytes(Uri.EscapeDataString(marker)),
            Encoding.UTF8.GetBytes(JsonEncodedText.Encode(marker).ToString()),
            Encoding.UTF8.GetBytes(WebUtility.HtmlEncode(marker))
        }

        For Each item In scan
            For Each form As Byte() In forms
                If IndexOfBytes(item.Bytes, form) >= 0 Then Return item.Entry
            Next
        Next

        Return Nothing

    End Function


    Friend Function IndexOfBytes(data As Byte(), pattern As Byte()) As Integer
        If pattern.Length = 0 Then Return 0
        Dim position As Integer = 0
        Do While position <= data.Length - pattern.Length
            position = Array.IndexOf(data, pattern(0), position, data.Length - pattern.Length - position + 1)
            If position < 0 Then Return -1
            Dim matched As Boolean = True
            For index As Integer = 1 To pattern.Length - 1
                If data(position + index) <> pattern(index) Then
                    matched = False
                    Exit For
                End If
            Next
            If matched Then Return position
            position += 1
        Loop
        Return -1
    End Function


    ' A serialized snapshot of a record, to show the export changed nothing.
    Friend Function Snapshot(value As Object) As String
        Return JsonSerializer.Serialize(value, CreateJsonOptions())
    End Function

End Module
