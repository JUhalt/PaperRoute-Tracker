Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports ManuscriptPipeline.Models

Namespace Services

    ' The three package files of a packet export (#45): the checksum list,
    ' the summary page, and the RO-Crate 1.3 metadata. All are written from
    ' the plan alone; notes, paths, ids, and hidden information never are.
    Friend NotInheritable Class PacketExportPackage

        Private Const RootId As String = "./"
        Private Const ManuscriptId As String = "#manuscript"
        Private Const RightsId As String = "#rights-not-stated"
        Private Const JournalId As String = "#journal"
        Private Const PublishedInId As String = "#published-in"
        Private Const SubmissionId As String = "#submission"
        Private Const ExportId As String = "#export"
        Private Const RoundDateNote As String = "The date this revision round was sent is not recorded."
        Private Const RevisionDateNote As String = "The date this revision was sent is not recorded."
        ' Scoped to what PaperRoute writes: an included file may hold any of these.
        Friend Const LeftOutText As String =
            "The summary and metadata PaperRoute wrote leave out notes, correspondence, reviewer names and comments, manuscript numbers, portal links, and where files are kept on the computer. " &
            "The files themselves are copied as they are."
        Friend Const PackageDateNote As String = "The package's date is the day it is exported."

        Private Sub New()
        End Sub


        ' sha256sum format: "<hash>  files/<name>", LF line ends.
        Friend Shared Function Manifest(files As IReadOnlyList(Of PacketExportWrittenFile)) As String
            Dim lines As New StringBuilder()
            For Each written As PacketExportWrittenFile In files
                lines.Append(written.Sha256).Append("  ").Append(PacketExportService.FilesFolder).Append(written.OutputName).Append(vbLf)
            Next
            Return lines.ToString()
        End Function


        ' Says what the package is, that its date is the export date, what it
        ' leaves out (by role), and that files travel exactly as they are.
        Friend Shared Function RootDescription(plan As PacketExportPlan, exportUtc As DateTime) As String

            Dim description As New StringBuilder()
            description.Append("Submission packet for """).Append(plan.Title).Append(""", exported from PaperRoute on ").Append(IsoDate(exportUtc)).Append(".")
            description.Append(" The package's date is the day it was exported, not a publication date.")

            Dim leftOut As List(Of String) = LeftOutRoles(plan)
            If leftOut.Count > 0 Then description.Append(" Not included: ").Append(String.Join(", ", leftOut)).Append(".")

            description.Append(" Files are copied exactly as they are, so anything stored inside them, such as a document's author, travels with them.")
            Return description.ToString()

        End Function


        Private Shared Function LeftOutRoles(plan As PacketExportPlan) As List(Of String)
            Return plan.Rows.
                Where(Function(item) Not item.Include).
                Select(Function(item) item.RoleName).
                Distinct(StringComparer.Ordinal).
                ToList()
        End Function


        ' ---- ro-crate-metadata.json ---------------------------------------------

        Friend Shared Function MetadataJson(
            plan As PacketExportPlan,
            files As IReadOnlyList(Of PacketExportWrittenFile),
            exportUtc As DateTime,
            appVersion As String
        ) As Byte()

            Dim people As List(Of CratePerson) = BuildPeople(plan)
            Dim organizations As New List(Of String)()
            For Each person As CratePerson In people
                For Each affiliation As String In person.Author.Affiliations
                    If Not organizations.Contains(affiliation, StringComparer.Ordinal) Then organizations.Add(affiliation)
                Next
            Next

            Dim writesJournal As Boolean = plan.JournalName.Length > 0
            Dim writesPublished As Boolean = plan.PublishedDoi.Length > 0
            Dim publishedId As String = If(writesPublished, PublishedWorkId(plan.PublishedDoi), String.Empty)

            Dim options As New JsonWriterOptions With {
                .Indented = True,
                .NewLine = vbLf,
                .Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }

            Using buffer As New MemoryStream()
                Using writer As New Utf8JsonWriter(buffer, options)

                    writer.WriteStartObject()
                    writer.WriteString("@context", PacketExportService.RoCrateContext)
                    writer.WriteStartArray("@graph")

                    ' The metadata descriptor.
                    writer.WriteStartObject()
                    writer.WriteString("@id", PacketExportService.MetadataName)
                    writer.WriteString("@type", "CreativeWork")
                    WriteRef(writer, "conformsTo", PacketExportService.RoCrateSpec)
                    WriteRef(writer, "about", RootId)
                    writer.WriteEndObject()

                    ' The package itself. Its date is the day it was exported.
                    Dim mentions As New List(Of String)()
                    If plan.HasSubmission Then mentions.Add(SubmissionId)
                    mentions.Add(ExportId)
                    If writesJournal AndAlso Not plan.HasSubmission Then mentions.Add(JournalId)

                    writer.WriteStartObject()
                    writer.WriteString("@id", RootId)
                    writer.WriteString("@type", "Dataset")
                    writer.WriteString("name", plan.EffectivePackageName())
                    writer.WriteString("description", RootDescription(plan, exportUtc))
                    writer.WriteString("datePublished", IsoDate(exportUtc))
                    WriteRef(writer, "license", RightsId)
                    WriteRef(writer, "mainEntity", ManuscriptId)
                    WriteRefs(writer, "mentions", mentions)
                    WriteRefs(writer, "hasPart", files.Select(Function(item) item.CrateId))
                    writer.WriteEndObject()

                    writer.WriteStartObject()
                    writer.WriteString("@id", RightsId)
                    writer.WriteString("@type", "CreativeWork")
                    writer.WriteString("name", PacketExportService.RightsName)
                    writer.WriteString("description", PacketExportService.RightsDescription)
                    writer.WriteEndObject()

                    ' The submitted version, never presented as the published article.
                    writer.WriteStartObject()
                    writer.WriteString("@id", ManuscriptId)
                    writer.WriteString("@type", "ScholarlyArticle")
                    writer.WriteString("name", plan.Title)
                    If plan.VersionLabel.Length > 0 Then writer.WriteString("version", plan.VersionLabel)
                    WriteRefs(writer, "encoding", files.
                        Where(Function(item) item.Row.Role = SubmissionPacketFileRole.Manuscript OrElse item.Row.Role = SubmissionPacketFileRole.BlindedManuscript).
                        Select(Function(item) item.CrateId))
                    WriteRefs(writer, "author", people.Select(Function(item) item.Id))
                    If plan.IncludeAbstract Then
                        If plan.AbstractText.Length > 0 Then writer.WriteString("abstract", plan.AbstractText)
                        If plan.Keywords.Count > 0 Then writer.WriteString("keywords", String.Join(", ", plan.Keywords))
                    End If
                    If writesPublished Then WriteRef(writer, "exampleOfWork", publishedId)
                    writer.WriteEndObject()

                    ' The published work, by its DOI. No date: none is recorded
                    ' at a known precision.
                    If writesPublished Then
                        writer.WriteStartObject()
                        writer.WriteString("@id", publishedId)
                        writer.WriteString("@type", "ScholarlyArticle")
                        writer.WriteString("name", plan.Title)
                        If plan.IsPublished Then writer.WriteString("creativeWorkStatus", "Published")
                        If plan.PublicationJournal.Length > 0 Then WriteRef(writer, "isPartOf", PublishedInId)
                        writer.WriteEndObject()

                        If plan.PublicationJournal.Length > 0 Then
                            writer.WriteStartObject()
                            writer.WriteString("@id", PublishedInId)
                            writer.WriteString("@type", "Periodical")
                            writer.WriteString("name", plan.PublicationJournal)
                            writer.WriteEndObject()
                        End If
                    End If

                    ' The journal the packet was for.
                    If writesJournal Then
                        writer.WriteStartObject()
                        writer.WriteString("@id", JournalId)
                        writer.WriteString("@type", "Periodical")
                        writer.WriteString("name", plan.JournalName)
                        WriteStrings(writer, "issn", plan.JournalIssns)
                        writer.WriteEndObject()
                    End If

                    ' The real submission. A revision has no recorded date, so
                    ' none is given: never the first submission's.
                    If plan.HasSubmission Then
                        writer.WriteStartObject()
                        writer.WriteString("@id", SubmissionId)
                        writer.WriteString("@type", "SendAction")
                        writer.WriteString("name", SubmissionName(plan))
                        WriteRef(writer, "object", ManuscriptId)
                        If writesJournal Then WriteRef(writer, "recipient", JournalId)
                        If plan.IsRevision Then
                            writer.WriteString("description", If(plan.RevisionRound.HasValue, RoundDateNote, RevisionDateNote))
                        ElseIf plan.SubmittedDate.HasValue Then
                            writer.WriteString("startTime", plan.SubmittedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                        End If
                        writer.WriteEndObject()
                    End If

                    ' The export. No agent: the package never names its maker.
                    writer.WriteStartObject()
                    writer.WriteString("@id", ExportId)
                    writer.WriteString("@type", "CreateAction")
                    writer.WriteString("name", "Exported from PaperRoute")
                    writer.WriteString("endTime", exportUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                    WriteRef(writer, "instrument", PacketExportService.SoftwareUrl)
                    WriteRef(writer, "result", RootId)
                    writer.WriteEndObject()

                    ' The software, without a license entity.
                    writer.WriteStartObject()
                    writer.WriteString("@id", PacketExportService.SoftwareUrl)
                    writer.WriteString("@type", "SoftwareApplication")
                    writer.WriteString("name", PacketExportService.SoftwareName)
                    writer.WriteString("url", PacketExportService.SoftwareUrl)
                    writer.WriteString("version", appVersion)
                    writer.WriteEndObject()

                    ' The files, each with its fingerprint status.
                    For Each written As PacketExportWrittenFile In files
                        Dim fingerprintId As String = "#file-" & written.Number.ToString(CultureInfo.InvariantCulture) & "-fingerprint"
                        Dim recordedId As String = "#file-" & written.Number.ToString(CultureInfo.InvariantCulture) & "-recorded-sha256"
                        Dim properties As New List(Of String) From {fingerprintId}
                        If written.Row.RecordedSha256.Length > 0 Then properties.Add(recordedId)

                        writer.WriteStartObject()
                        writer.WriteString("@id", written.CrateId)
                        writer.WriteString("@type", "File")
                        writer.WriteString("name", written.OutputName)
                        writer.WriteString("description", written.Row.Description())
                        writer.WriteString("encodingFormat", PacketExportService.MediaType(written.OutputName))
                        writer.WriteString("contentSize", written.SizeBytes.ToString(CultureInfo.InvariantCulture))
                        writer.WriteString("sha256", written.Sha256)
                        WriteRefs(writer, "additionalProperty", properties)
                        writer.WriteEndObject()

                        writer.WriteStartObject()
                        writer.WriteString("@id", fingerprintId)
                        writer.WriteString("@type", "PropertyValue")
                        writer.WriteString("name", "Fingerprint")
                        writer.WriteString("value", PacketExportService.FingerprintText(written.Status))
                        writer.WriteEndObject()

                        If written.Row.RecordedSha256.Length > 0 Then
                            writer.WriteStartObject()
                            writer.WriteString("@id", recordedId)
                            writer.WriteString("@type", "PropertyValue")
                            writer.WriteString("name", "Recorded SHA-256")
                            writer.WriteString("value", written.Row.RecordedSha256)
                            writer.WriteEndObject()
                        End If
                    Next

                    ' People and their organizations: no email, ROR, or address.
                    For Each person As CratePerson In people
                        writer.WriteStartObject()
                        writer.WriteString("@id", person.Id)
                        writer.WriteString("@type", "Person")
                        writer.WriteString("name", person.Author.Name)
                        WriteRefs(writer, "affiliation", person.Author.Affiliations.Select(Function(item) OrganizationId(organizations, item)))
                        writer.WriteEndObject()
                    Next

                    For Each organization As String In organizations
                        writer.WriteStartObject()
                        writer.WriteString("@id", OrganizationId(organizations, organization))
                        writer.WriteString("@type", "Organization")
                        writer.WriteString("name", organization)
                        writer.WriteEndObject()
                    Next

                    writer.WriteEndArray()
                    writer.WriteEndObject()

                End Using

                buffer.WriteByte(10)
                Return buffer.ToArray()
            End Using

        End Function


        Private NotInheritable Class CratePerson
            Public Sub New(id As String, author As PacketExportAuthor)
                Me.Id = id
                Me.Author = author
            End Sub
            Public ReadOnly Property Id As String
            Public ReadOnly Property Author As PacketExportAuthor
        End Class


        ' An ORCID iD is the id only when its checksum passed (and once);
        ' otherwise "#author-n" by the author's position.
        Private Shared Function BuildPeople(plan As PacketExportPlan) As List(Of CratePerson)

            Dim people As New List(Of CratePerson)()
            If Not plan.IncludeAuthors OrElse plan.IsBlinded Then Return people

            Dim used As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For index As Integer = 0 To plan.Authors.Count - 1
                Dim author As PacketExportAuthor = plan.Authors(index)
                Dim id As String = If(author.Orcid.Length > 0, "https://orcid.org/" & author.Orcid, String.Empty)
                If id.Length = 0 OrElse Not used.Add(id) Then id = "#author-" & (index + 1).ToString(CultureInfo.InvariantCulture)
                people.Add(New CratePerson(id, author))
            Next

            Return people

        End Function


        Private Shared Function OrganizationId(organizations As List(Of String), name As String) As String
            Return "#org-" & (organizations.IndexOf(name) + 1).ToString(CultureInfo.InvariantCulture)
        End Function


        Friend Shared Function PublishedWorkId(doi As String) As String
            Return "https://doi.org/" & String.Join("/", doi.Split("/"c).Select(Function(part) Uri.EscapeDataString(part)))
        End Function


        Private Shared Function SubmissionName(plan As PacketExportPlan) As String
            Dim name As String = "Sent to " & If(plan.JournalName.Length > 0, plan.JournalName, "the journal")
            If plan.RevisionRound.HasValue Then
                name &= ", revision round " & plan.RevisionRound.Value.ToString(CultureInfo.InvariantCulture)
            ElseIf plan.IsRevision Then
                name &= ", revision (round not recorded)"
            End If
            Return name
        End Function


        Private Shared Sub WriteRef(writer As Utf8JsonWriter, propertyName As String, id As String)
            writer.WriteStartObject(propertyName)
            writer.WriteString("@id", id)
            writer.WriteEndObject()
        End Sub


        ' Nothing for none, one reference for one, an array for more: never a
        ' one-item array.
        Private Shared Sub WriteRefs(writer As Utf8JsonWriter, propertyName As String, ids As IEnumerable(Of String))
            Dim values As List(Of String) = ids.ToList()
            If values.Count = 0 Then Return
            If values.Count = 1 Then
                WriteRef(writer, propertyName, values(0))
                Return
            End If
            writer.WriteStartArray(propertyName)
            For Each id As String In values
                writer.WriteStartObject()
                writer.WriteString("@id", id)
                writer.WriteEndObject()
            Next
            writer.WriteEndArray()
        End Sub


        Private Shared Sub WriteStrings(writer As Utf8JsonWriter, propertyName As String, items As IEnumerable(Of String))
            Dim values As List(Of String) = items.ToList()
            If values.Count = 0 Then Return
            If values.Count = 1 Then
                writer.WriteString(propertyName, values(0))
                Return
            End If
            writer.WriteStartArray(propertyName)
            For Each value As String In values
                writer.WriteStringValue(value)
            Next
            writer.WriteEndArray()
        End Sub


        ' ---- ro-crate-preview.html ----------------------------------------------

        ' A static page that needs nothing but a browser: no scripts, no
        ' images, and only plain links.
        Friend Shared Function PreviewHtml(
            plan As PacketExportPlan,
            files As IReadOnlyList(Of PacketExportWrittenFile),
            exportUtc As DateTime,
            appVersion As String
        ) As String

            Dim html As New StringBuilder()
            Dim packageName As String = plan.EffectivePackageName()

            ReportService.OpenDocument(html, packageName)
            html.Append("<header><h1>").Append(Encode(packageName)).Append("</h1>")
            html.Append("<p class=""meta"">Exported from PaperRoute on ").Append(IsoDate(exportUtc)).Append("</p></header>")
            html.Append("<p>").Append(Encode(RootDescription(plan, exportUtc))).Append("</p>")

            html.Append("<h2>Package details</h2><table><tbody>")
            DetailRow(html, "Manuscript", Encode(plan.Title))
            DetailRow(html, "Version", Encode(If(plan.VersionLabel.Length > 0, plan.VersionLabel, If(plan.VersionLinked AndAlso Not plan.VersionFound, "Not found", "Not recorded"))))

            If plan.IncludeAuthors AndAlso Not plan.IsBlinded AndAlso plan.Authors.Count > 0 Then
                Dim authors As New List(Of String)()
                For Each author As PacketExportAuthor In plan.Authors
                    Dim entry As String = If(
                        author.Orcid.Length > 0,
                        "<a href=""" & Encode("https://orcid.org/" & author.Orcid) & """>" & Encode(author.Name) & "</a>",
                        Encode(author.Name))
                    If author.Affiliations.Count > 0 Then entry &= " (" & Encode(String.Join("; ", author.Affiliations)) & ")"
                    authors.Add(entry)
                Next
                DetailRow(html, "Authors", String.Join("; ", authors))
            End If

            Dim journal As String = If(plan.JournalName.Length > 0, plan.JournalName, "Not recorded")
            If plan.JournalName.Length > 0 AndAlso plan.JournalIssns.Count > 0 Then journal &= ", ISSN " & String.Join(", ", plan.JournalIssns)
            DetailRow(html, "Journal", Encode(journal))
            DetailRow(html, "Submission", Encode(SubmissionText(plan)))

            If plan.PublishedDoi.Length > 0 Then
                Dim doiUrl As String = PublishedWorkId(plan.PublishedDoi)
                Dim published As String = "<a href=""" & Encode(doiUrl) & """>" & Encode(doiUrl) & "</a>"
                If plan.PublicationJournal.Length > 0 Then published &= " in " & Encode(plan.PublicationJournal)
                ' Like the metadata, it claims publication only when the
                ' manuscript is published.
                DetailRow(html, If(plan.IsPublished, "Published as", "DOI"), published)
            End If

            If plan.IncludeAbstract Then
                If plan.AbstractText.Length > 0 Then DetailRow(html, "Abstract", Encode(plan.AbstractText))
                If plan.Keywords.Count > 0 Then DetailRow(html, "Keywords", Encode(String.Join(", ", plan.Keywords)))
            End If

            DetailRow(html, "Rights", Encode(PacketExportService.RightsName & ": " & PacketExportService.RightsDescription))
            DetailRow(html, "Package date", Encode(PackageDateNote))
            html.Append("</tbody></table>")

            html.Append("<h2>Files</h2><table><thead><tr><th>File</th><th>Role</th><th>Size</th><th>SHA-256</th><th>Fingerprint</th></tr></thead><tbody>")
            For Each written As PacketExportWrittenFile In files
                html.Append("<tr><td><a href=""").Append(Encode(written.CrateId)).Append(""">").Append(Encode(written.OutputName)).Append("</a></td>")
                html.Append("<td>").Append(Encode(written.Row.Description())).Append("</td>")
                html.Append("<td class=""num"">").Append(written.SizeBytes.ToString("N0", CultureInfo.CurrentCulture)).Append(If(written.SizeBytes = 1, " byte", " bytes")).Append("</td>")
                html.Append("<td><code style=""word-break:break-all"">").Append(written.Sha256).Append("</code></td>")
                html.Append("<td>").Append(Encode(PacketExportService.FingerprintText(written.Status)))
                If written.Status = PacketExportFingerprint.Changed Then
                    html.Append("<br>Recorded SHA-256: <code style=""word-break:break-all"">").Append(Encode(written.Row.RecordedSha256)).Append("</code>")
                End If
                html.Append("</td></tr>")
            Next
            html.Append("</tbody></table>")

            Dim leftOut As List(Of PacketExportRow) = plan.Rows.Where(Function(item) Not item.Include).ToList()
            If leftOut.Count > 0 Then
                html.Append("<h2>Not included</h2><ul>")
                For Each row As PacketExportRow In leftOut
                    html.Append("<li>").Append(Encode(row.RoleName & ": " & LeftOutReason(row.Fingerprint))).Append("</li>")
                Next
                html.Append("</ul>")
            End If

            html.Append("<h2>Check the files</h2>")
            html.Append("<p><a href=""").Append(PacketExportService.ManifestName).Append(""">").Append(PacketExportService.ManifestName).Append("</a> lists each file's SHA-256. ")
            html.Append("On Windows, in PowerShell, run <code>Get-FileHash -Algorithm SHA256 -LiteralPath .\files\NAME</code> for a file and compare the result with the list (case doesn't matter). ")
            html.Append("Elsewhere, run <code>sha256sum -c ").Append(PacketExportService.ManifestName).Append("</code> in this folder.</p>")
            html.Append("<p><a href=""").Append(PacketExportService.MetadataName).Append(""">").Append(PacketExportService.MetadataName).Append("</a> describes the package for research tools, using ")
            html.Append("<a href=""").Append(PacketExportService.RoCrateSpec).Append(""">RO-Crate 1.3</a>.</p>")
            html.Append("<p>").Append(Encode(LeftOutText)).Append("</p>")

            html.Append("<footer>Made with ").Append(Encode(PacketExportService.SoftwareName)).Append(" ").Append(Encode(appVersion)).Append(".</footer>")
            ReportService.CloseDocument(html)
            Return html.ToString()

        End Function


        Private Shared Sub DetailRow(html As StringBuilder, term As String, valueHtml As String)
            html.Append("<tr><th scope=""row"">").Append(Encode(term)).Append("</th><td>").Append(valueHtml).Append("</td></tr>")
        End Sub


        ' Also shown in the export window's package details.
        Friend Shared Function SubmissionText(plan As PacketExportPlan) As String
            If Not plan.HasSubmission Then Return "Not linked to a recorded submission"
            If plan.RevisionRound.HasValue Then
                Return "Revision round " & plan.RevisionRound.Value.ToString(CultureInfo.InvariantCulture) & ". " & RoundDateNote
            End If
            If plan.IsRevision Then Return "A revision (round not recorded). " & RevisionDateNote
            Return "Sent on " & plan.SubmittedDate.Value.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
        End Function


        ' Neutral reasons only: the warnings shown before export stay out.
        Private Shared Function LeftOutReason(value As PacketExportFingerprint) As String
            Select Case value
                Case PacketExportFingerprint.NoFile : Return "no file: recorded in PaperRoute only"
                Case PacketExportFingerprint.Missing : Return "file not found"
                Case PacketExportFingerprint.Unreadable : Return "file couldn't be read"
                Case Else : Return "left out of this package"
            End Select
        End Function


        Private Shared Function IsoDate(value As DateTime) As String
            Return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        End Function


        Private Shared Function Encode(value As String) As String
            Return ReportService.Encode(value)
        End Function

    End Class

End Namespace
