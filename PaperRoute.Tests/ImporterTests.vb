Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ImporterTests

    Private _root As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub StandardImporter_LoadsManuscriptSubmissionAndDecision()

        Dim workbookPath As String = Path.Combine(_root, "standard.xlsx")
        Dim templateGenerator As New StandardTemplateGenerator()
        templateGenerator.Generate(workbookPath)

        Using workbook As New XLWorkbook(workbookPath)
            Dim manuscripts = workbook.Worksheet("Manuscripts")
            manuscripts.Cell("A2").Value = "M001"
            manuscripts.Cell("B2").Value = "Template Study"
            manuscripts.Cell("C2").Value = "UnderReview"
            manuscripts.Cell("D2").Value = "Pipeline"
            manuscripts.Cell("E2").Value = "Journal of Template Studies"
            manuscripts.Cell("F2").Value = New DateTime(2026, 8, 1)

            Dim submissions = workbook.Worksheet("Submissions")
            submissions.Cell("A2").Value = "S001"
            submissions.Cell("B2").Value = "M001"
            submissions.Cell("C2").Value = "Journal of Template Studies"
            submissions.Cell("D2").Value = "TEMP-101"
            submissions.Cell("E2").Value = New DateTime(2026, 7, 20)
            submissions.Cell("F2").Value = "https://example.invalid/portal"
            submissions.Cell("G2").Value = "Synthetic standard-template submission."

            Dim decisions = workbook.Worksheet("Decisions")
            decisions.Cell("A2").Value = "D001"
            decisions.Cell("B2").Value = "S001"
            decisions.Cell("C2").Value = New DateTime(2026, 8, 10)
            decisions.Cell("D2").Value = "Major Revision"
            decisions.Cell("E2").Value = New DateTime(2026, 9, 15)
            decisions.Cell("F2").Value = "Synthetic decision."

            workbook.Save()
        End Using

        Dim result As ExcelImportResult = New StandardExcelImporter().Import(workbookPath)

        Assert.AreEqual(1, result.Manuscripts.Count)
        Assert.AreEqual(1, result.SubmissionCount)
        Assert.AreEqual(1, result.DecisionCount)

        Dim manuscript As Manuscript = result.Manuscripts(0)
        Assert.AreEqual("Template Study", manuscript.Title)
        Assert.AreEqual(PaperStage.UnderReview, manuscript.CurrentStage)
        Assert.AreEqual("TEMP-101", manuscript.Submissions(0).ManuscriptNumber)
        Assert.AreEqual(EditorialDecision.MajorRevision, manuscript.Submissions(0).Decisions(0).Decision)

    End Sub

    <TestMethod>
    Public Sub LegacyImporter_LoadsRejectedSubmission()

        Dim workbookPath As String = Path.Combine(_root, "legacy.xlsx")

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Tracker")
            Dim headers As String() = {"SUBMISSION", "SUBMITTED", "TITLE", "JOURNAL", "RESPONSE", "STATUS"}

            For index As Integer = 0 To headers.Length - 1
                worksheet.Cell(1, index + 1).Value = headers(index)
            Next

            worksheet.Cell("A2").Value = "1"
            worksheet.Cell("B2").Value = New DateTime(2026, 6, 1)
            worksheet.Cell("C2").Value = "Legacy Study"
            worksheet.Cell("D2").Value = "Journal of Legacy Examples"
            worksheet.Cell("E2").Value = New DateTime(2026, 6, 20)
            worksheet.Cell("F2").Value = "R"
            workbook.SaveAs(workbookPath)
        End Using

        Dim importer As New LegacyExcelImporter()
        Assert.IsTrue(importer.CanImport(workbookPath))

        Dim result As ExcelImportResult = importer.Import(workbookPath)
        Assert.AreEqual(1, result.Manuscripts.Count)
        Assert.AreEqual(1, result.SubmissionCount)
        Assert.AreEqual(1, result.DecisionCount)
        Assert.AreEqual(EditorialDecision.Rejected, result.Manuscripts(0).Submissions(0).Decisions(0).Decision)

    End Sub

    <TestMethod>
    Public Sub FlexibleImporter_MapsArbitraryColumns()

        Dim workbookPath As String = Path.Combine(_root, "mapped.xlsx")

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("My Tracker")
            worksheet.Cell("A1").Value = "Project Name"
            worksheet.Cell("B1").Value = "Outlet"
            worksheet.Cell("C1").Value = "Date Sent"
            worksheet.Cell("D1").Value = "Outcome"
            worksheet.Cell("E1").Value = "Response Date"
            worksheet.Cell("F1").Value = "Comments"

            worksheet.Cell("A2").Value = "Mapped Study"
            worksheet.Cell("B2").Value = "Journal of Mapping"
            worksheet.Cell("C2").Value = New DateTime(2026, 5, 1)
            worksheet.Cell("D2").Value = "Rejected"
            worksheet.Cell("E2").Value = New DateTime(2026, 5, 22)
            worksheet.Cell("F2").Value = "Synthetic mapped row."
            workbook.SaveAs(workbookPath)
        End Using

        Dim mappings As New List(Of ExcelColumnMapping) From {
            New ExcelColumnMapping With {.ColumnNumber = 1, .HeaderName = "Project Name", .Field = ExcelImportField.Title},
            New ExcelColumnMapping With {.ColumnNumber = 2, .HeaderName = "Outlet", .Field = ExcelImportField.SubmissionJournal},
            New ExcelColumnMapping With {.ColumnNumber = 3, .HeaderName = "Date Sent", .Field = ExcelImportField.SubmissionDate},
            New ExcelColumnMapping With {.ColumnNumber = 4, .HeaderName = "Outcome", .Field = ExcelImportField.Decision},
            New ExcelColumnMapping With {.ColumnNumber = 5, .HeaderName = "Response Date", .Field = ExcelImportField.DecisionDate},
            New ExcelColumnMapping With {.ColumnNumber = 6, .HeaderName = "Comments", .Field = ExcelImportField.Notes}
        }

        Dim result As ExcelImportResult =
            New FlexibleExcelImporter().Import(workbookPath, "My Tracker", 1, mappings)

        Assert.AreEqual(1, result.Manuscripts.Count)
        Assert.AreEqual(1, result.SubmissionCount)
        Assert.AreEqual(1, result.DecisionCount)
        Assert.AreEqual("Mapped Study", result.Manuscripts(0).Title)
        Assert.AreEqual("Journal of Mapping", result.Manuscripts(0).Submissions(0).JournalName)
        Assert.AreEqual(EditorialDecision.Rejected, result.Manuscripts(0).Submissions(0).Decisions(0).Decision)

    End Sub

    <TestMethod>
    Public Sub FlexibleImporter_RejectsDuplicateFieldMappings()

        Dim workbookPath As String = CreateMinimalWorkbook("duplicate.xlsx")

        Dim mappings As New List(Of ExcelColumnMapping) From {
            New ExcelColumnMapping With {.ColumnNumber = 1, .Field = ExcelImportField.Title},
            New ExcelColumnMapping With {.ColumnNumber = 2, .Field = ExcelImportField.Title}
        }

        Dim importer As New FlexibleExcelImporter()

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As ExcelImportResult = importer.Import(workbookPath, "Tracker", 1, mappings)
            End Sub
        )

    End Sub

    <TestMethod>
    Public Sub FlexibleImporter_RequiresTitleMapping()

        Dim workbookPath As String = CreateMinimalWorkbook("missing-title.xlsx")

        Dim mappings As New List(Of ExcelColumnMapping) From {
            New ExcelColumnMapping With {.ColumnNumber = 2, .Field = ExcelImportField.SubmissionJournal}
        }

        Dim importer As New FlexibleExcelImporter()

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As ExcelImportResult = importer.Import(workbookPath, "Tracker", 1, mappings)
            End Sub
        )

    End Sub

    <TestMethod>
    Public Sub StandardTemplate_DoesNotExposeLegacyCoAuthorsColumn()

        Dim workbookPath As String =
            Path.Combine(
                _root,
                "modern-template.xlsx"
            )

        Dim templateGenerator As New StandardTemplateGenerator()

        templateGenerator.Generate(
            workbookPath
        )

        Using workbook As New XLWorkbook(workbookPath)

            Dim worksheet =
                workbook.Worksheet("Manuscripts")

            Dim headers As New List(Of String)()

            For Each cell In worksheet.Row(1).CellsUsed()
                headers.Add(cell.GetString())
            Next

            CollectionAssert.DoesNotContain(
                headers,
                "CoAuthors"
            )

            Assert.AreEqual(
                "CurrentStage*",
                worksheet.Cell("C1").GetString()
            )

        End Using

    End Sub


    <TestMethod>
    Public Sub FlexibleImporter_DoesNotAutoMapAuthorTextToLegacyField()

        Dim importer As New FlexibleExcelImporter()

        Assert.AreEqual(
            ExcelImportField.Ignore,
            importer.SuggestField("Authors")
        )

        CollectionAssert.DoesNotContain(
            importer.GetFieldDisplayNames(),
            "Co-authors"
        )

    End Sub


    <TestMethod>
    Public Sub StandardImporter_IgnoresLegacyCoAuthorsColumn()

        Dim workbookPath As String =
            Path.Combine(
                _root,
                "old-author-column.xlsx"
            )

        Dim templateGenerator As New StandardTemplateGenerator()

        templateGenerator.Generate(
            workbookPath
        )

        Using workbook As New XLWorkbook(workbookPath)

            Dim manuscripts =
                workbook.Worksheet("Manuscripts")

            manuscripts.Column(3).InsertColumnsBefore(1)
            manuscripts.Cell("C1").Value = "CoAuthors"

            manuscripts.Cell("A2").Value = "M001"
            manuscripts.Cell("B2").Value = "Legacy Author Test"
            manuscripts.Cell("C2").Value = "A. Author; B. Author"
            manuscripts.Cell("D2").Value = "Draft"
            manuscripts.Cell("E2").Value = "Pipeline"

            workbook.Save()

        End Using

        Dim result As ExcelImportResult =
            New StandardExcelImporter().Import(
                workbookPath
            )

        Assert.AreEqual(
            1,
            result.Manuscripts.Count
        )

        Assert.AreEqual(
            String.Empty,
            result.Manuscripts(0).CoAuthors
        )

    End Sub


    ' A date the importer made up is written into the record (#126), not
    ' only into the import-time warning list: which date, and from what.
    <TestMethod>
    Public Sub FlexibleImporter_NoSubmissionDate_NotesTheImportDateItUsed()

        Dim workbookPath As String = Path.Combine(_root, "no-submission-date.xlsx")

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Tracker")
            worksheet.Cell("A1").Value = "Title"
            worksheet.Cell("B1").Value = "Outlet"
            worksheet.Cell("C1").Value = "Date Sent"
            worksheet.Cell("D1").Value = "Comments"
            worksheet.Cell("A2").Value = "Undated Study"
            worksheet.Cell("B2").Value = "Journal of Undated Examples"
            worksheet.Cell("D2").Value = "Synthetic comment."
            workbook.SaveAs(workbookPath)
        End Using

        Dim mappings As New List(Of ExcelColumnMapping) From {
            New ExcelColumnMapping With {.ColumnNumber = 1, .Field = ExcelImportField.Title},
            New ExcelColumnMapping With {.ColumnNumber = 2, .Field = ExcelImportField.SubmissionJournal},
            New ExcelColumnMapping With {.ColumnNumber = 3, .Field = ExcelImportField.SubmissionDate},
            New ExcelColumnMapping With {.ColumnNumber = 4, .Field = ExcelImportField.Notes}
        }

        Dim result As ExcelImportResult =
            New FlexibleExcelImporter().Import(workbookPath, "Tracker", 1, mappings)

        Dim submission As JournalSubmission = result.Manuscripts(0).Submissions(0)
        Dim today As String = DateTime.Today.ToString("yyyy-MM-dd")

        Assert.AreEqual(DateTime.Today, submission.SubmittedDate)
        Assert.IsTrue(result.Warnings.Any(Function(warning) warning.Contains("no readable submission date")), "The import-time warning stays.")
        StringAssert.StartsWith(submission.Notes, "Synthetic comment.", "The row's own notes come first.")
        StringAssert.Contains(submission.Notes, "no submission date", "The record says the date was missing.")
        StringAssert.Contains(submission.Notes, "import", "The record says where the date came from.")
        StringAssert.Contains(submission.Notes, today, "The record names the date that was used.")

    End Sub

    <TestMethod>
    Public Sub FlexibleImporter_NoDecisionDate_NotesTheSubmissionDateItUsed()

        Dim workbookPath As String = Path.Combine(_root, "no-decision-date.xlsx")

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Tracker")
            worksheet.Cell("A1").Value = "Title"
            worksheet.Cell("B1").Value = "Outlet"
            worksheet.Cell("C1").Value = "Date Sent"
            worksheet.Cell("D1").Value = "Outcome"
            worksheet.Cell("E1").Value = "Response Date"
            worksheet.Cell("A2").Value = "Dated Study"
            worksheet.Cell("B2").Value = "Journal of Dated Examples"
            worksheet.Cell("C2").Value = New DateTime(2026, 5, 1)
            worksheet.Cell("D2").Value = "Rejected"
            workbook.SaveAs(workbookPath)
        End Using

        Dim mappings As New List(Of ExcelColumnMapping) From {
            New ExcelColumnMapping With {.ColumnNumber = 1, .Field = ExcelImportField.Title},
            New ExcelColumnMapping With {.ColumnNumber = 2, .Field = ExcelImportField.SubmissionJournal},
            New ExcelColumnMapping With {.ColumnNumber = 3, .Field = ExcelImportField.SubmissionDate},
            New ExcelColumnMapping With {.ColumnNumber = 4, .Field = ExcelImportField.Decision},
            New ExcelColumnMapping With {.ColumnNumber = 5, .Field = ExcelImportField.DecisionDate}
        }

        Dim result As ExcelImportResult =
            New FlexibleExcelImporter().Import(workbookPath, "Tracker", 1, mappings)

        Dim submission As JournalSubmission = result.Manuscripts(0).Submissions(0)
        Dim decision As EditorialDecisionEvent = submission.Decisions(0)

        Assert.AreEqual(New DateTime(2026, 5, 1), decision.DecisionDate)
        Assert.IsTrue(result.Warnings.Any(Function(warning) warning.Contains("no readable decision date")), "The import-time warning stays.")
        StringAssert.Contains(decision.Notes, "Imported from mapped spreadsheet.", "The import note stays.")
        StringAssert.Contains(decision.Notes, "No decision date was given", "The record says the date was missing.")
        StringAssert.Contains(decision.Notes, "submission date", "The record says where the date came from.")
        StringAssert.Contains(decision.Notes, "2026-05-01", "The record names the date that was used.")
        Assert.IsFalse(submission.Notes.Contains("no submission date"), "A submission that had its date gets no such note.")

    End Sub

    Private Function CreateMinimalWorkbook(fileName As String) As String

        Dim workbookPath As String = System.IO.Path.Combine(_root, fileName)

        Using workbook As New XLWorkbook()
            Dim worksheet = workbook.Worksheets.Add("Tracker")
            worksheet.Cell("A1").Value = "Title"
            worksheet.Cell("B1").Value = "Journal"
            worksheet.Cell("A2").Value = "Minimal Study"
            worksheet.Cell("B2").Value = "Journal of Minimal Examples"
            workbook.SaveAs(workbookPath)
        End Using

        Return workbookPath

    End Function

End Class
