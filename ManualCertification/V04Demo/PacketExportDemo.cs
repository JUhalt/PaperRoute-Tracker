using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// Export Submission Packet (#45) on synthetic files in a new temporary
// folder: the integrity fixture plus a PDF and a title page that name a
// "Demo Author" inside them, two figures both called "Figure 1.png", and a
// table whose name needs percent-encoding. Export... writes the .zip into
// that same folder; nothing is sent anywhere.
internal static class PacketExportDemo
{
    internal const string ZipName = "PaperRoute-Packet-Export-Demo.zip";

    internal static Form Dialog(bool blinded)
    {
        var fixture = DemoFixture.Create(integrity: true);
        var packet = fixture.Packet;
        var folder = Path.GetDirectoryName(packet.Files[0].LocalFilePath)!;
        packet.Label = blinded ? "Revision 1 - anonymized synthetic packet" : "Revision 1 - synthetic packet";
        if (!blinded) packet.Files[0].Role = SubmissionPacketFileRole.Manuscript;

        AddFile(packet, folder, "main.pdf", "Main text (revision 1).pdf", "Main text, revision 1",
            blinded ? SubmissionPacketFileRole.BlindedManuscript : SubmissionPacketFileRole.Manuscript,
            Encoding.Latin1.GetBytes(
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
                "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n" +
                "3 0 obj\n<< /Title (Synthetic manuscript) /Author (Demo Author) >>\nendobj\n" +
                "trailer\n<< /Root 1 0 R /Info 3 0 R >>\n%%EOF\n"),
            fingerprint: true);
        AddFile(packet, folder, "title.docx", "Title page.docx", "Title page", SubmissionPacketFileRole.TitlePage,
            TitlePage(), fingerprint: true);
        AddFile(packet, folder, "figure-a.png", "Figure 1.png", "Figure 1", SubmissionPacketFileRole.Figure,
            Png(Color.SteelBlue), fingerprint: false);
        AddFile(packet, folder, "figure-b.png", "Figure 1.png", "Figure 1, recolored", SubmissionPacketFileRole.Figure,
            Png(Color.DarkOrange), fingerprint: false);
        AddFile(packet, folder, "table.csv", "Table #1 (50% sample).csv", "Table 1", SubmissionPacketFileRole.Table,
            Encoding.UTF8.GetBytes("condition,estimate\nanchor high,0.42\nanchor low,0.31\n"), fingerprint: false);

        var brown = new AffiliationRecord { Institution = "Brown University" };
        fixture.Library.Affiliations.Add(brown);
        var carberry = new AuthorRecord { GivenName = "Josiah", FamilyName = "Carberry", Orcid = "0000-0002-1825-0097" };
        var placeholder = new AuthorRecord { GivenName = "Riley", FamilyName = "Placeholder" };
        fixture.Library.Authors.Add(carberry);
        fixture.Library.Authors.Add(placeholder);
        fixture.Manuscript.Authors.Add(new ManuscriptAuthor { AuthorId = carberry.Id, AffiliationIds = { brown.Id }, IsCorrespondingAuthor = true });
        fixture.Manuscript.Authors.Add(new ManuscriptAuthor { AuthorId = placeholder.Id });

        var metadata = fixture.Manuscript.Metadata;
        metadata.AbstractText = "A synthetic abstract for checking the packet export window; it describes no real study.";
        metadata.Keywords = new List<string> { "synthetic", "packet export", "demo" };
        metadata.Doi = "10.5555/demo.packet-export";
        metadata.PublicationJournal = packet.JournalName;

        return new PacketExportForm(fixture.Manuscript, packet, fixture.Library)
        {
            SavePathPrompt = () => Path.Combine(folder, ZipName)
        };
    }

    private static void AddFile(SubmissionPacket packet, string folder, string storedName, string originalName, string label,
        SubmissionPacketFileRole role, byte[] content, bool fingerprint)
    {
        var stored = Path.Combine(folder, storedName);
        File.WriteAllBytes(stored, content);
        var file = new SubmissionPacketFile
        {
            Role = role,
            Label = label,
            OriginalFileName = originalName,
            StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
            LocalFilePath = stored
        };
        if (fingerprint && SubmissionPacketIntegrityService.CaptureBaseline(file).Status != PacketFileIntegrityStatus.Unchanged)
        {
            throw new InvalidOperationException($"Could not record the fixture fingerprint: {stored}");
        }
        packet.Files.Add(file);
    }

    // The smallest Word document the hidden-information check reads: its
    // core properties name "Demo Author".
    private static byte[] TitlePage()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/></Types>");
            AddEntry(archive, "word/document.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                "<w:body><w:p><w:r><w:t>Synthetic title page for the packet export demo.</w:t></w:r></w:p></w:body></w:document>");
            AddEntry(archive, "docProps/core.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                "xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>Title page</dc:title><dc:creator>Demo Author</dc:creator>" +
                "<cp:lastModifiedBy>Demo Author</cp:lastModifiedBy></cp:coreProperties>");
        }
        return output.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] Png(Color color)
    {
        using var image = new Bitmap(8, 8);
        using (var graphics = Graphics.FromImage(image)) graphics.Clear(color);
        using var output = new MemoryStream();
        image.Save(output, ImageFormat.Png);
        return output.ToArray();
    }
}
