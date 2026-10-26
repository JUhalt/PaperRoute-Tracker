Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' One atomic writer for every storage file (#125).
<TestClass>
Public Class StorageFileTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' A successful write takes the live file's place and keeps the previous
    ' file as the backup, with no temporary file left behind.
    <TestMethod>
    Public Sub Write_ReplacesTheLiveFileAndKeepsThePrevious()

        Dim live As String = Path.Combine(_root, "settings.json")
        Dim backup As String = Path.Combine(_root, "settings.bak")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "first"))
        Assert.AreEqual("first", File.ReadAllText(live))
        Assert.IsFalse(File.Exists(backup), "Nothing to keep yet.")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "second"))
        Assert.AreEqual("second", File.ReadAllText(live))
        Assert.AreEqual("first", File.ReadAllText(backup), "The previous file is the backup.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length, "No temporary file is left.")

        ' Without a backup path the previous file is simply replaced.
        StorageFile.Write(live, Nothing, Sub(stream) WriteText(stream, "third"))
        Assert.AreEqual("third", File.ReadAllText(live))
        Assert.AreEqual("first", File.ReadAllText(backup))

    End Sub


    ' A failure before the replace leaves the live file byte-identical, the
    ' backup as it was, and no temporary file behind.
    <TestMethod>
    Public Sub Write_FailureBeforeTheReplaceLeavesTheLiveFileAndNoTemp()

        Dim live As String = Path.Combine(_root, "authors.json")
        Dim backup As String = Path.Combine(_root, "authors.bak")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "first"))
        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "second"))
        Dim liveBefore As Byte() = File.ReadAllBytes(live)
        Dim backupBefore As Byte() = File.ReadAllBytes(backup)

        Dim failure As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub()
                    StorageFile.Write(live, backup,
                        Sub(stream)
                            WriteText(stream, "half of the third")
                            Throw New InvalidOperationException("The disk is full.")
                        End Sub)
                End Sub)

        Assert.AreEqual("The disk is full.", failure.Message)
        CollectionAssert.AreEqual(liveBefore, File.ReadAllBytes(live), "The live file is untouched.")
        CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backup), "The backup is untouched.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length, "No temporary file is left.")

    End Sub


    ' No storage service writes a live data file with File.WriteAllText,
    ' which leaves an empty or torn file after a power cut at the wrong
    ' moment. StorageMigrationService keeps two: the migration receipt,
    ' which is diagnostic, and the copied data's path rewrite, which is
    ' not yet the live file.
    <TestMethod>
    Public Sub StorageServices_WriteLiveFilesOnlyThroughTheAtomicWriter()

        Dim services As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Services")

        Dim allowed As New Dictionary(Of String, String()) From {
            {"StorageMigrationService.vb", {"dataPath,", "receiptPath,"}}
        }

        For Each name As String In {
            "ManuscriptRepository.vb",
            "AuthorLibraryRepository.vb",
            "CitationStore.vb",
            "AppSettingsService.vb",
            "StorageMigrationService.vb",
            "Schema5MigrationService.vb",
            "Schema6MigrationService.vb",
            "Schema7MigrationService.vb",
            "StorageFile.vb"
        }

            Dim lines As String() =
                File.ReadAllLines(Path.Combine(services, name)).
                    Where(Function(line) Not line.TrimStart().StartsWith("'"c)).
                    ToArray()

            Dim targets As New List(Of String)()

            For index As Integer = 0 To lines.Length - 1

                If Not lines(index).Contains("File.WriteAllText(", StringComparison.Ordinal) Then Continue For

                ' The file written: on the same line, or the next non-blank one.
                Dim rest As String = lines(index).Substring(lines(index).IndexOf("File.WriteAllText(", StringComparison.Ordinal) + "File.WriteAllText(".Length).Trim()
                Dim following As Integer = index + 1
                While rest.Length = 0 AndAlso following < lines.Length
                    rest = lines(following).Trim()
                    following += 1
                End While
                targets.Add(rest)

            Next

            Dim expected As String() = If(allowed.ContainsKey(name), allowed(name), New String() {})
            CollectionAssert.AreEqual(expected, targets, name & " writes " & String.Join(", ", targets) & " with File.WriteAllText.")

        Next

        Dim writer As String = File.ReadAllText(Path.Combine(services, "StorageFile.vb"))
        StringAssert.Contains(writer, "flushToDisk:=True", "The writer flushes to disk before the replace.")

    End Sub


    ' An export's file held open the way Excel holds a workbook (shared for
    ' reading only) fails with a sharing violation, from File.WriteAllText
    ' and from ClosedXML's SaveAs alike; the box then names the file and
    ' says to close it there. The old file is left as it was. This also
    ' pins that ClosedXML raises a bare IOException, not a wrapped one.
    <TestMethod>
    Public Sub WriteFailureText_NamesAFileAnotherProgramHasOpen()

        Dim exporter As New LibraryExcelExporter()

        Dim writers As New Dictionary(Of String, Action(Of String)) From {
            {"PaperRoute-Publications.md", Sub(filePath) File.WriteAllText(filePath, "new")},
            {"PaperRoute_Export.xlsx", Sub(filePath) exporter.Export(filePath, New List(Of ManuscriptPipeline.Models.Manuscript)())}
        }

        For Each writer As KeyValuePair(Of String, Action(Of String)) In writers

            Dim target As String = Path.Combine(_root, writer.Key)
            File.WriteAllText(target, "old")
            Dim before As Byte() = File.ReadAllBytes(target)
            Dim failure As IOException

            Using New FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)
                failure = Assert.Throws(Of IOException)(Sub() writer.Value.Invoke(target))
            End Using

            Assert.IsTrue(StorageFile.IsSharingViolation(failure), writer.Key & ": " & failure.GetType().Name & " 0x" & failure.HResult.ToString("X8"))
            Assert.AreEqual(
                writer.Key & " is open in another program. Close it there, then try again.",
                StorageFile.WriteFailureText(target, failure))
            CollectionAssert.AreEqual(before, File.ReadAllBytes(target), writer.Key & " is left as it was.")

        Next

    End Sub


    ' Every other failure keeps .NET's own words. Access denied is not "open
    ' in another program": it is also a read-only file or a protected folder.
    <TestMethod>
    Public Sub WriteFailureText_KeepsEveryOtherErrorsOwnWords()

        Const target As String = "C:\x\Export.xlsx"
        Const inUse As String = "Export.xlsx is open in another program. Close it there, then try again."

        Dim denied As New UnauthorizedAccessException("Access to the path 'C:\x\Export.xlsx' is denied.")
        Assert.AreEqual(denied.Message, StorageFile.WriteFailureText(target, denied))
        Assert.IsFalse(StorageFile.IsSharingViolation(denied))
        Assert.IsTrue(StorageFile.IsInUse(denied))

        Dim full As New IOException("There is not enough space on the disk.", &H80070070)
        Assert.AreEqual("There is not enough space on the disk.", StorageFile.WriteFailureText(target, full))
        Assert.IsFalse(StorageFile.IsSharingViolation(full))
        Assert.IsFalse(StorageFile.IsInUse(full))

        Dim gone As New FileNotFoundException("gone")
        Assert.AreEqual("gone", StorageFile.WriteFailureText(target, gone))
        Assert.IsFalse(StorageFile.IsSharingViolation(gone))
        Assert.IsFalse(StorageFile.IsInUse(gone))

        Dim locked As New IOException("locked", &H80070021)
        Assert.AreEqual(inUse, StorageFile.WriteFailureText(target, locked))
        Assert.IsTrue(StorageFile.IsSharingViolation(locked))
        Assert.IsTrue(StorageFile.IsInUse(locked))

        Dim sharing As New IOException("shared", &H80070020)
        Assert.AreEqual(inUse, StorageFile.WriteFailureText(target, sharing))
        Assert.IsTrue(StorageFile.IsSharingViolation(sharing))
        Assert.IsTrue(StorageFile.IsInUse(sharing))

    End Sub


    ' Each export that writes the file the researcher chose says so when
    ' another program has it open: its message is built with
    ' WriteFailureText, not the bare ex.Message. Each lead is found exactly
    ' once, so a moved or duplicated message fails here too.
    <TestMethod>
    Public Sub ExportSites_SayWhenTheFileIsOpenInAnotherProgram()

        Dim app As String = Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")

        Dim sites As (File As String, Lead As String)() = {
            ("Form1.vb", "could not create the Excel template."),
            ("Form1.vb", "could not export the library."),
            ("Form1.Bibliography.vb", "could not export the bibliography."),
            ("Form1.Deadlines.vb", "could not create the calendar file."),
            ("Forms\PublicationExportForm.vb", "could not save the publication export."),
            ("Forms\ReportForm.vb", "could not save the report."),
            ("Forms\ReviewerResponseExportForm.vb", "The Markdown file could not be saved."),
            ("Forms\CoverLetterForm.vb", "The letter couldn't be saved."),
            ("Forms\DiagnosticsForm.vb", "could not save the diagnostic report.")
        }

        For Each site In sites

            Dim lines As String() = File.ReadAllLines(Path.Combine(app, site.File))
            Dim found As List(Of Integer) =
                Enumerable.Range(0, lines.Length).
                    Where(Function(index) lines(index).Contains(site.Lead, StringComparison.Ordinal)).
                    ToList()

            Assert.AreEqual(1, found.Count, site.File & ": """ & site.Lead & """ is on exactly one line.")

            Dim message As String = String.Join(Environment.NewLine, lines.Skip(found(0)).Take(6))
            StringAssert.Contains(message, "StorageFile.WriteFailureText(", site.File & ": """ & site.Lead & """")
            Assert.IsFalse(message.Contains("ex.Message", StringComparison.Ordinal), site.File & ": """ & site.Lead & """ still shows ex.Message.")

        Next

    End Sub


    Private Shared Sub WriteText(stream As Stream, content As String)
        Dim bytes As Byte() = System.Text.Encoding.UTF8.GetBytes(content)
        stream.Write(bytes, 0, bytes.Length)
    End Sub

End Class
