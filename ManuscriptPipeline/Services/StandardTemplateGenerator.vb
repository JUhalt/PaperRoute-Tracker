Imports System
Imports System.IO
Imports ClosedXML.Excel

Namespace Services

    Public Class StandardTemplateGenerator

        Public Sub Generate(filePath As String)

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("A destination file path is required.")
            End If

            Dim parentDirectory As String = Path.GetDirectoryName(filePath)

            If Not String.IsNullOrWhiteSpace(parentDirectory) Then
                Directory.CreateDirectory(parentDirectory)
            End If

            Using workbook As New XLWorkbook()

                Dim instructions As IXLWorksheet = workbook.Worksheets.Add("Instructions")
                Dim manuscripts As IXLWorksheet = workbook.Worksheets.Add("Manuscripts")
                Dim submissions As IXLWorksheet = workbook.Worksheets.Add("Submissions")
                Dim decisions As IXLWorksheet = workbook.Worksheets.Add("Decisions")
                Dim correspondence As IXLWorksheet = workbook.Worksheets.Add("Correspondence")
                Dim lists As IXLWorksheet = workbook.Worksheets.Add("Lists")

                BuildLists(lists)
                CreateNamedLists(lists)

                BuildInstructions(instructions)
                BuildManuscriptsSheet(manuscripts)
                BuildSubmissionsSheet(submissions)
                BuildDecisionsSheet(decisions)
                BuildCorrespondenceSheet(correspondence)

                ApplyValidation(manuscripts, decisions, correspondence)

                lists.Hide()

                workbook.SaveAs(filePath)

            End Using

        End Sub


        ' =====================================================
        ' Instructions
        ' =====================================================

        Private Sub BuildInstructions(
            worksheet As IXLWorksheet
        )

            worksheet.Cell("A1").Value = "PaperRoute Import Template"
            worksheet.Range("A1:F1").Merge()

            StyleTitle(
                worksheet.Range("A1:F1")
            )

            worksheet.Cell("A3").Value = "How to use this workbook"
            worksheet.Range("A3:F3").Merge()

            StyleSection(
                worksheet.Range("A3:F3")
            )

            worksheet.Cell("A4").Value =
                "Keep the worksheet names and column headers unchanged. " &
                "Enter one manuscript per row on the Manuscripts sheet. " &
                "A manuscript may have multiple submissions, and each submission may have multiple decisions and correspondence records. " &
                "Authors and affiliations are managed as structured records inside PaperRoute; BibTeX/RIS import can bring structured author names into the library."

            worksheet.Range("A4:F6").Merge()

            StyleNote(
                worksheet.Range("A4:F6")
            )

            worksheet.Cell("A8").Value = "Stage vs. Location"
            worksheet.Range("A8:F8").Merge()

            StyleSection(
                worksheet.Range("A8:F8")
            )

            worksheet.Cell("A9").Value =
                "CurrentStage describes publication progress: Idea, Draft, Submitted, UnderReview, Revision, Accepted, InPress, or Published. " &
                "Location describes where the manuscript lives in PaperRoute: Pipeline, Published, or FileDrawer. " &
                "FileDrawer is not a publication stage. A filed manuscript should retain its last meaningful CurrentStage."

            worksheet.Range("A9:F12").Merge()

            StyleNote(
                worksheet.Range("A9:F12")
            )

            worksheet.Cell("A14").Value = "Stable IDs"
            worksheet.Range("A14:F14").Merge()

            StyleSection(
                worksheet.Range("A14:F14")
            )

            worksheet.Cell("A15").Value =
                "Use simple unique IDs to connect records across worksheets. " &
                "For example: M001 for a manuscript, S001 for a submission, D001 for a decision, and C001 for correspondence. " &
                "The IDs only need to be unique within this workbook."

            worksheet.Range("A15:F18").Merge()

            StyleNote(
                worksheet.Range("A15:F18")
            )

            worksheet.Cell("A20").Value = "Relationships"
            worksheet.Range("A20:F20").Merge()

            StyleSection(
                worksheet.Range("A20:F20")
            )

            worksheet.Cell("A21").Value = "Sheet"
            worksheet.Cell("B21").Value = "Example"
            worksheet.Cell("C21").Value = "Meaning"

            worksheet.Cell("A22").Value = "Manuscripts"
            worksheet.Cell("B22").Value = "M001"
            worksheet.Cell("C22").Value = "One manuscript"

            worksheet.Cell("A23").Value = "Submissions"
            worksheet.Cell("B23").Value = "S001 → M001"
            worksheet.Cell("C23").Value = "Submission belonging to manuscript M001"

            worksheet.Cell("A24").Value = "Decisions"
            worksheet.Cell("B24").Value = "D001 → S001"
            worksheet.Cell("C24").Value = "Decision belonging to submission S001"

            worksheet.Cell("A25").Value = "Correspondence"
            worksheet.Cell("B25").Value = "C001 → S001"
            worksheet.Cell("C25").Value = "File, email, or reference belonging to submission S001"

            StyleHeader(
                worksheet.Range("A21:C21")
            )

            worksheet.Cell("A27").Value = "Dates, URLs, and files"
            worksheet.Range("A27:F27").Merge()

            StyleSection(
                worksheet.Range("A27:F27")
            )

            worksheet.Cell("A28").Value =
                "Use real Excel dates and leave unknown dates blank. " &
                "PortalURL and SourceURL should begin with http:// or https://. " &
                "For correspondence, StorageMode=Link keeps the existing file path. " &
                "StorageMode=ManagedCopy asks PaperRoute to archive its own copy when the workbook is imported."

            worksheet.Range("A28:F32").Merge()

            StyleNote(
                worksheet.Range("A28:F32")
            )

            worksheet.Column("A").Width = 22
            worksheet.Column("B").Width = 24
            worksheet.Column("C").Width = 58
            worksheet.Column("D").Width = 14
            worksheet.Column("E").Width = 14
            worksheet.Column("F").Width = 14

        End Sub


        ' =====================================================
        ' Manuscripts
        ' =====================================================

        Private Sub BuildManuscriptsSheet(
            worksheet As IXLWorksheet
        )

            Dim headers As String() = {
                "ManuscriptID*",
                "Title*",
                "CurrentStage*",
                "Location*",
                "TargetJournal",
                "StageEnteredDate",
                "FileDrawerDate",
                "FileDrawerReason"
            }

            WriteHeaders(
                worksheet,
                headers
            )

            worksheet.SheetView.FreezeRows(1)

            worksheet.Column("A").Width = 16
            worksheet.Column("B").Width = 38
            worksheet.Column("C").Width = 18
            worksheet.Column("D").Width = 16
            worksheet.Column("E").Width = 34
            worksheet.Column("F").Width = 18
            worksheet.Column("G").Width = 18
            worksheet.Column("H").Width = 42

            worksheet.Range("F2:G500").Style.NumberFormat.Format =
                "yyyy-mm-dd"

        End Sub


        ' =====================================================
        ' Submissions
        ' =====================================================

        Private Sub BuildSubmissionsSheet(
            worksheet As IXLWorksheet
        )

            Dim headers As String() = {
                "SubmissionID*",
                "ManuscriptID*",
                "Journal*",
                "ManuscriptNumber",
                "SubmittedDate*",
                "PortalURL",
                "Notes"
            }

            WriteHeaders(
                worksheet,
                headers
            )

            worksheet.SheetView.FreezeRows(1)

            worksheet.Column("A").Width = 16
            worksheet.Column("B").Width = 16
            worksheet.Column("C").Width = 34
            worksheet.Column("D").Width = 24
            worksheet.Column("E").Width = 18
            worksheet.Column("F").Width = 44
            worksheet.Column("G").Width = 48

            worksheet.Range("E2:E750").Style.NumberFormat.Format =
                "yyyy-mm-dd"

        End Sub


        ' =====================================================
        ' Decisions
        ' =====================================================

        Private Sub BuildDecisionsSheet(
            worksheet As IXLWorksheet
        )

            Dim headers As String() = {
                "DecisionID*",
                "SubmissionID*",
                "DecisionDate*",
                "Decision*",
                "RevisionDeadline",
                "Notes"
            }

            WriteHeaders(
                worksheet,
                headers
            )

            worksheet.SheetView.FreezeRows(1)

            worksheet.Column("A").Width = 16
            worksheet.Column("B").Width = 16
            worksheet.Column("C").Width = 18
            worksheet.Column("D").Width = 28
            worksheet.Column("E").Width = 18
            worksheet.Column("F").Width = 55

            worksheet.Range("C2:C1000").Style.NumberFormat.Format =
                "yyyy-mm-dd"

            worksheet.Range("E2:E1000").Style.NumberFormat.Format =
                "yyyy-mm-dd"

        End Sub


        ' =====================================================
        ' Correspondence
        ' =====================================================

        Private Sub BuildCorrespondenceSheet(
            worksheet As IXLWorksheet
        )

            Dim headers As String() = {
                "CorrespondenceID*",
                "SubmissionID*",
                "Date*",
                "Type*",
                "Title*",
                "FilePath",
                "StorageMode",
                "SourceURL",
                "Notes"
            }

            WriteHeaders(
                worksheet,
                headers
            )

            worksheet.SheetView.FreezeRows(1)

            worksheet.Column("A").Width = 20
            worksheet.Column("B").Width = 16
            worksheet.Column("C").Width = 18
            worksheet.Column("D").Width = 28
            worksheet.Column("E").Width = 38
            worksheet.Column("F").Width = 48
            worksheet.Column("G").Width = 18
            worksheet.Column("H").Width = 46
            worksheet.Column("I").Width = 52

            worksheet.Range("C2:C1250").Style.NumberFormat.Format =
                "yyyy-mm-dd"

        End Sub


        ' =====================================================
        ' Lists
        ' =====================================================

        Private Sub BuildLists(
            worksheet As IXLWorksheet
        )

            worksheet.Cell("A1").Value = "CurrentStage"
            worksheet.Cell("B1").Value = "Location"
            worksheet.Cell("C1").Value = "Decision"
            worksheet.Cell("D1").Value = "CorrespondenceType"
            worksheet.Cell("E1").Value = "StorageMode"

            StyleHeader(
                worksheet.Range("A1:E1")
            )

            Dim stages As String() = {
                "Idea",
                "Draft",
                "Submitted",
                "UnderReview",
                "Revision",
                "Accepted",
                "InPress",
                "Published"
            }

            For i As Integer = 0 To stages.Length - 1
                worksheet.Cell(i + 2, 1).Value = stages(i)
            Next

            Dim locations As String() = {
                "Pipeline",
                "Published",
                "FileDrawer"
            }

            For i As Integer = 0 To locations.Length - 1
                worksheet.Cell(i + 2, 2).Value = locations(i)
            Next

            Dim decisionValues As String() = {
                "Rejected",
                "Desk Rejected",
                "Rejected After Review",
                "Major Revision",
                "Minor Revision",
                "Revise and Resubmit",
                "Accepted",
                "Withdrawn"
            }

            For i As Integer = 0 To decisionValues.Length - 1
                worksheet.Cell(i + 2, 3).Value = decisionValues(i)
            Next

            Dim correspondenceTypes As String() = {
                "Decision Letter",
                "Reviewer Comments",
                "Editor Email",
                "Cover Letter",
                "Response to Reviewers",
                "Revised Manuscript",
                "Acceptance Letter",
                "Portal Snapshot",
                "Other"
            }

            For i As Integer = 0 To correspondenceTypes.Length - 1
                worksheet.Cell(i + 2, 4).Value = correspondenceTypes(i)
            Next

            worksheet.Cell("E2").Value = "Link"
            worksheet.Cell("E3").Value = "ManagedCopy"

            worksheet.Column("A").Width = 22
            worksheet.Column("B").Width = 18
            worksheet.Column("C").Width = 28
            worksheet.Column("D").Width = 28
            worksheet.Column("E").Width = 18

        End Sub


        Private Sub CreateNamedLists(
            worksheet As IXLWorksheet
        )

            worksheet.Range("A2:A9").AddToNamed(
                "StageList"
            )

            worksheet.Range("B2:B4").AddToNamed(
                "LocationList"
            )

            worksheet.Range("C2:C9").AddToNamed(
                "DecisionList"
            )

            worksheet.Range("D2:D10").AddToNamed(
                "CorrespondenceTypeList"
            )

            worksheet.Range("E2:E3").AddToNamed(
                "StorageModeList"
            )

        End Sub


        ' =====================================================
        ' Validation
        ' =====================================================

        Private Sub ApplyValidation(
            manuscripts As IXLWorksheet,
            decisions As IXLWorksheet,
            correspondence As IXLWorksheet
        )

            manuscripts.Range("C2:C500").CreateDataValidation().List(
                "=StageList"
            )

            manuscripts.Range("D2:D500").CreateDataValidation().List(
                "=LocationList"
            )

            decisions.Range("D2:D1000").CreateDataValidation().List(
                "=DecisionList"
            )

            correspondence.Range("D2:D1250").CreateDataValidation().List(
                "=CorrespondenceTypeList"
            )

            correspondence.Range("G2:G1250").CreateDataValidation().List(
                "=StorageModeList"
            )

        End Sub


        ' =====================================================
        ' Common formatting
        ' =====================================================

        Private Sub WriteHeaders(
            worksheet As IXLWorksheet,
            headers As String()
        )

            For i As Integer = 0 To headers.Length - 1
                worksheet.Cell(1, i + 1).Value = headers(i)
            Next

            StyleHeader(
                worksheet.Range(
                    1,
                    1,
                    1,
                    headers.Length
                )
            )

        End Sub


        Private Sub StyleTitle(
            target As IXLRange
        )

            target.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1F2937")

            target.Style.Font.FontColor =
                XLColor.White

            target.Style.Font.Bold =
                True

            target.Style.Font.FontSize =
                16

            target.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center

            target.Style.Alignment.WrapText =
                True

            target.Worksheet.Row(target.RangeAddress.FirstAddress.RowNumber).Height = 28

        End Sub


        Private Sub StyleSection(
            target As IXLRange
        )

            target.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#E5E7EB")

            target.Style.Font.Bold =
                True

            target.Style.Font.FontColor =
                XLColor.FromHtml("#111827")

        End Sub


        Private Sub StyleHeader(
            target As IXLRange
        )

            target.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#374151")

            target.Style.Font.FontColor =
                XLColor.White

            target.Style.Font.Bold =
                True

            target.Style.Alignment.WrapText =
                True

            target.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center

            target.Worksheet.Row(target.RangeAddress.FirstAddress.RowNumber).Height = 28

        End Sub


        Private Sub StyleNote(
            target As IXLRange
        )

            target.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#F3F4F6")

            target.Style.Font.FontColor =
                XLColor.FromHtml("#374151")

            target.Style.Alignment.WrapText =
                True

            target.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top

        End Sub

    End Class

End Namespace