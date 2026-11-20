Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The parts of StorageFileTests that need Windows (core stage 2): a file
' another program holds open is "in use" only by Windows' sharing rules
' (design d2 5.3), and the export messages are in the Windows app's own
' source. The rest of StorageFileTests is in PaperRoute.Core.Tests.
<TestClass>
Public Class StorageFileSharingTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
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


    ' Each export that writes the file the researcher chose says so when
    ' another program has it open: its message is built with
    ' WriteFailureText, not the bare ex.Message. Each lead is found exactly
    ' once, so a moved or duplicated message fails here too.
    <TestMethod>
    Public Sub ExportSites_SayWhenTheFileIsOpenInAnotherProgram()

        Dim app As String = Path.Combine(TestRepository.Root(), "ManuscriptPipeline")

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

End Class
