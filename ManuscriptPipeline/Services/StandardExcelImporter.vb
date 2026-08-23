Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class StandardExcelImporter

        Public Function Import(filePath As String) As ExcelImportResult

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("An Excel file path is required.")
            End If

            If Not File.Exists(filePath) Then
                Throw New FileNotFoundException("The Excel workbook could not be found.", filePath)
            End If

            Dim result As New ExcelImportResult()

            Using workbook As New XLWorkbook(filePath)

                Dim manuscriptsSheet As IXLWorksheet = GetRequiredWorksheet(workbook, "Manuscripts")
                Dim submissionsSheet As IXLWorksheet = GetRequiredWorksheet(workbook, "Submissions")
                Dim decisionsSheet As IXLWorksheet = GetRequiredWorksheet(workbook, "Decisions")
                Dim correspondenceSheet As IXLWorksheet = GetRequiredWorksheet(workbook, "Correspondence")

                Dim manuscriptMap As New Dictionary(Of String, Manuscript)(StringComparer.OrdinalIgnoreCase)
                Dim submissionMap As New Dictionary(Of String, JournalSubmission)(StringComparer.OrdinalIgnoreCase)

                ReadManuscripts(manuscriptsSheet, manuscriptMap, result)
                ReadSubmissions(submissionsSheet, manuscriptMap, submissionMap, result)
                ReadDecisions(decisionsSheet, submissionMap, result)
                ReadCorrespondence(correspondenceSheet, submissionMap, result)

            End Using

            Return result

        End Function


        ' =====================================================
        ' Worksheets
        ' =====================================================

        Private Function GetRequiredWorksheet(
            workbook As XLWorkbook,
            worksheetName As String
        ) As IXLWorksheet

            For Each worksheet As IXLWorksheet In workbook.Worksheets

                If String.Equals(
                    worksheet.Name.Trim(),
                    worksheetName,
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return worksheet

                End If

            Next

            Throw New InvalidDataException(
                "The standard PaperRoute workbook is missing the '" &
                worksheetName &
                "' worksheet."
            )

        End Function


        ' =====================================================
        ' Manuscripts
        ' =====================================================

        Private Sub ReadManuscripts(
            worksheet As IXLWorksheet,
            manuscriptMap As Dictionary(Of String, Manuscript),
            result As ExcelImportResult
        )

            Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

            RequireHeaders(
                headers,
                "ManuscriptID",
                "Title",
                "CurrentStage",
                "Location"
            )

            Dim lastRow As Integer = GetLastRowNumber(worksheet)

            For rowNumber As Integer = 2 To lastRow

                Dim externalId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("MANUSCRIPTID"))
                )

                Dim title As String = ReadText(
                    worksheet.Cell(rowNumber, headers("TITLE"))
                )

                If String.IsNullOrWhiteSpace(externalId) AndAlso
                   String.IsNullOrWhiteSpace(title) Then

                    Continue For

                End If

                result.RowsRead += 1

                If String.IsNullOrWhiteSpace(externalId) Then

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": ManuscriptID is required. Row skipped."
                    )

                    Continue For

                End If

                If String.IsNullOrWhiteSpace(title) Then

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": Title is required. Row skipped."
                    )

                    Continue For

                End If

                If manuscriptMap.ContainsKey(externalId) Then

                    Throw New InvalidDataException(
                        "Duplicate ManuscriptID '" &
                        externalId &
                        "' was found."
                    )

                End If

                Dim stageText As String = ReadText(
                    worksheet.Cell(rowNumber, headers("CURRENTSTAGE"))
                )

                Dim locationText As String = ReadText(
                    worksheet.Cell(rowNumber, headers("LOCATION"))
                )

                Dim stage As PaperStage

                If Not TryParsePaperStage(stageText, stage) Then

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": unrecognized CurrentStage '" &
                        stageText &
                        "'. Row skipped."
                    )

                    Continue For

                End If

                Dim location As ManuscriptLocation

                If Not TryParseLocation(locationText, location) Then

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": unrecognized Location '" &
                        locationText &
                        "'. Row skipped."
                    )

                    Continue For

                End If

                If stage = PaperStage.Published AndAlso
                   location <> ManuscriptLocation.Published Then

                    location = ManuscriptLocation.Published

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": Published stage was automatically assigned to the Published shelf."
                    )

                ElseIf location = ManuscriptLocation.Published AndAlso
                       stage <> PaperStage.Published Then

                    stage = PaperStage.Published

                    result.Warnings.Add(
                        "Manuscripts row " &
                        rowNumber.ToString() &
                        ": Published location was automatically assigned Published stage."
                    )

                End If

                Dim targetJournal As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "TARGETJOURNAL"
                )

                Dim stageEnteredDate As DateTime? = ReadOptionalDate(
                    worksheet,
                    rowNumber,
                    headers,
                    "STAGEENTEREDDATE"
                )

                Dim fileDrawerDate As DateTime? = ReadOptionalDate(
                    worksheet,
                    rowNumber,
                    headers,
                    "FILEDRAWERDATE"
                )

                Dim fileDrawerReason As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "FILEDRAWERREASON"
                )

                Dim manuscript As New Manuscript With {
                    .Id = Guid.NewGuid(),
                    .Title = title,
                    .TargetJournal = targetJournal,
                    .CurrentStage = stage,
                    .Location = location,
                    .StageEnteredDate = If(stageEnteredDate.HasValue, stageEnteredDate.Value, DateTime.Now),
                    .FileDrawerDate = fileDrawerDate,
                    .FileDrawerReason = fileDrawerReason
                }

                manuscript.History.Add(
                    New HistoryEvent With {
                        .Id = Guid.NewGuid(),
                        .EventDate = manuscript.StageEnteredDate,
                        .Stage = manuscript.CurrentStage,
                        .Note = "Imported from standard PaperRoute workbook."
                    }
                )

                manuscriptMap.Add(externalId, manuscript)
                result.Manuscripts.Add(manuscript)

            Next

        End Sub


        ' =====================================================
        ' Submissions
        ' =====================================================

        Private Sub ReadSubmissions(
            worksheet As IXLWorksheet,
            manuscriptMap As Dictionary(Of String, Manuscript),
            submissionMap As Dictionary(Of String, JournalSubmission),
            result As ExcelImportResult
        )

            Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

            RequireHeaders(
                headers,
                "SubmissionID",
                "ManuscriptID",
                "Journal",
                "SubmittedDate"
            )

            Dim lastRow As Integer = GetLastRowNumber(worksheet)

            For rowNumber As Integer = 2 To lastRow

                Dim submissionId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("SUBMISSIONID"))
                )

                Dim manuscriptId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("MANUSCRIPTID"))
                )

                Dim journal As String = ReadText(
                    worksheet.Cell(rowNumber, headers("JOURNAL"))
                )

                If String.IsNullOrWhiteSpace(submissionId) AndAlso
                   String.IsNullOrWhiteSpace(manuscriptId) AndAlso
                   String.IsNullOrWhiteSpace(journal) Then

                    Continue For

                End If

                result.RowsRead += 1

                If String.IsNullOrWhiteSpace(submissionId) OrElse
                   String.IsNullOrWhiteSpace(manuscriptId) OrElse
                   String.IsNullOrWhiteSpace(journal) Then

                    result.Warnings.Add(
                        "Submissions row " &
                        rowNumber.ToString() &
                        ": SubmissionID, ManuscriptID, and Journal are required. Row skipped."
                    )

                    Continue For

                End If

                If submissionMap.ContainsKey(submissionId) Then

                    Throw New InvalidDataException(
                        "Duplicate SubmissionID '" &
                        submissionId &
                        "' was found."
                    )

                End If

                Dim parentManuscript As Manuscript = Nothing

                If Not manuscriptMap.TryGetValue(manuscriptId, parentManuscript) Then

                    result.Warnings.Add(
                        "Submissions row " &
                        rowNumber.ToString() &
                        ": ManuscriptID '" &
                        manuscriptId &
                        "' does not exist. Row skipped."
                    )

                    Continue For

                End If

                Dim submittedDate As DateTime? = ReadDate(
                    worksheet.Cell(rowNumber, headers("SUBMITTEDDATE"))
                )

                If Not submittedDate.HasValue Then

                    result.Warnings.Add(
                        "Submissions row " &
                        rowNumber.ToString() &
                        ": SubmittedDate is required. Row skipped."
                    )

                    Continue For

                End If

                Dim manuscriptNumber As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "MANUSCRIPTNUMBER"
                )

                Dim portalUrl As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "PORTALURL"
                )

                portalUrl = ValidateUrl(
                    portalUrl,
                    "Submissions row " & rowNumber.ToString(),
                    result
                )

                Dim notes As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "NOTES"
                )

                Dim submission As New JournalSubmission With {
                    .Id = Guid.NewGuid(),
                    .JournalName = journal,
                    .ManuscriptNumber = manuscriptNumber,
                    .SubmittedDate = submittedDate.Value.Date,
                    .PortalUrl = portalUrl,
                    .Notes = notes,
                    .Decisions = New List(Of EditorialDecisionEvent)(),
                    .Correspondence = New List(Of CorrespondenceItem)()
                }

                parentManuscript.Submissions.Add(submission)
                submissionMap.Add(submissionId, submission)

            Next

        End Sub


        ' =====================================================
        ' Decisions
        ' =====================================================

        Private Sub ReadDecisions(
            worksheet As IXLWorksheet,
            submissionMap As Dictionary(Of String, JournalSubmission),
            result As ExcelImportResult
        )

            Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

            RequireHeaders(
                headers,
                "DecisionID",
                "SubmissionID",
                "DecisionDate",
                "Decision"
            )

            Dim seenDecisionIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Dim lastRow As Integer = GetLastRowNumber(worksheet)

            For rowNumber As Integer = 2 To lastRow

                Dim decisionId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("DECISIONID"))
                )

                Dim submissionId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("SUBMISSIONID"))
                )

                Dim decisionText As String = ReadText(
                    worksheet.Cell(rowNumber, headers("DECISION"))
                )

                If String.IsNullOrWhiteSpace(decisionId) AndAlso
                   String.IsNullOrWhiteSpace(submissionId) AndAlso
                   String.IsNullOrWhiteSpace(decisionText) Then

                    Continue For

                End If

                result.RowsRead += 1

                If String.IsNullOrWhiteSpace(decisionId) OrElse
                   String.IsNullOrWhiteSpace(submissionId) OrElse
                   String.IsNullOrWhiteSpace(decisionText) Then

                    result.Warnings.Add(
                        "Decisions row " &
                        rowNumber.ToString() &
                        ": DecisionID, SubmissionID, and Decision are required. Row skipped."
                    )

                    Continue For

                End If

                If seenDecisionIds.Contains(decisionId) Then

                    Throw New InvalidDataException(
                        "Duplicate DecisionID '" &
                        decisionId &
                        "' was found."
                    )

                End If

                Dim parentSubmission As JournalSubmission = Nothing

                If Not submissionMap.TryGetValue(submissionId, parentSubmission) Then

                    result.Warnings.Add(
                        "Decisions row " &
                        rowNumber.ToString() &
                        ": SubmissionID '" &
                        submissionId &
                        "' does not exist. Row skipped."
                    )

                    Continue For

                End If

                Dim decisionDate As DateTime? = ReadDate(
                    worksheet.Cell(rowNumber, headers("DECISIONDATE"))
                )

                If Not decisionDate.HasValue Then

                    result.Warnings.Add(
                        "Decisions row " &
                        rowNumber.ToString() &
                        ": DecisionDate is required. Row skipped."
                    )

                    Continue For

                End If

                Dim decision As EditorialDecision

                If Not TryParseDecision(decisionText, decision) Then

                    result.Warnings.Add(
                        "Decisions row " &
                        rowNumber.ToString() &
                        ": unrecognized Decision '" &
                        decisionText &
                        "'. Row skipped."
                    )

                    Continue For

                End If

                Dim revisionDeadline As DateTime? = ReadOptionalDate(
                    worksheet,
                    rowNumber,
                    headers,
                    "REVISIONDEADLINE"
                )

                Dim notes As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "NOTES"
                )

                parentSubmission.Decisions.Add(
                    New EditorialDecisionEvent With {
                        .Id = Guid.NewGuid(),
                        .DecisionDate = decisionDate.Value.Date,
                        .Decision = decision,
                        .RevisionDeadline = revisionDeadline,
                        .Notes = notes
                    }
                )

                seenDecisionIds.Add(decisionId)

            Next

        End Sub


        ' =====================================================
        ' Correspondence
        ' =====================================================

        Private Sub ReadCorrespondence(
            worksheet As IXLWorksheet,
            submissionMap As Dictionary(Of String, JournalSubmission),
            result As ExcelImportResult
        )

            Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

            RequireHeaders(
                headers,
                "CorrespondenceID",
                "SubmissionID",
                "Date",
                "Type",
                "Title"
            )

            Dim seenIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            Dim lastRow As Integer = GetLastRowNumber(worksheet)

            For rowNumber As Integer = 2 To lastRow

                Dim correspondenceId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("CORRESPONDENCEID"))
                )

                Dim submissionId As String = ReadText(
                    worksheet.Cell(rowNumber, headers("SUBMISSIONID"))
                )

                Dim title As String = ReadText(
                    worksheet.Cell(rowNumber, headers("TITLE"))
                )

                If String.IsNullOrWhiteSpace(correspondenceId) AndAlso
                   String.IsNullOrWhiteSpace(submissionId) AndAlso
                   String.IsNullOrWhiteSpace(title) Then

                    Continue For

                End If

                result.RowsRead += 1

                If String.IsNullOrWhiteSpace(correspondenceId) OrElse
                   String.IsNullOrWhiteSpace(submissionId) OrElse
                   String.IsNullOrWhiteSpace(title) Then

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": CorrespondenceID, SubmissionID, and Title are required. Row skipped."
                    )

                    Continue For

                End If

                If seenIds.Contains(correspondenceId) Then

                    Throw New InvalidDataException(
                        "Duplicate CorrespondenceID '" &
                        correspondenceId &
                        "' was found."
                    )

                End If

                Dim parentSubmission As JournalSubmission = Nothing

                If Not submissionMap.TryGetValue(submissionId, parentSubmission) Then

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": SubmissionID '" &
                        submissionId &
                        "' does not exist. Row skipped."
                    )

                    Continue For

                End If

                Dim itemDate As DateTime? = ReadDate(
                    worksheet.Cell(rowNumber, headers("DATE"))
                )

                If Not itemDate.HasValue Then

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": Date is required. Row skipped."
                    )

                    Continue For

                End If

                Dim typeText As String = ReadText(
                    worksheet.Cell(rowNumber, headers("TYPE"))
                )

                Dim itemType As CorrespondenceType

                If Not TryParseCorrespondenceType(typeText, itemType) Then

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": unrecognized Type '" &
                        typeText &
                        "'. Row skipped."
                    )

                    Continue For

                End If

                Dim filePath As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "FILEPATH"
                )

                Dim storageMode As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "STORAGEMODE"
                )

                Dim sourceUrl As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "SOURCEURL"
                )

                sourceUrl = ValidateUrl(
                    sourceUrl,
                    "Correspondence row " & rowNumber.ToString(),
                    result
                )

                Dim notes As String = ReadOptionalText(
                    worksheet,
                    rowNumber,
                    headers,
                    "NOTES"
                )

                Dim managedCopy As Boolean =
                    NormalizeToken(storageMode) = "MANAGEDCOPY"

                If managedCopy AndAlso
                   String.IsNullOrWhiteSpace(filePath) Then

                    managedCopy = False

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": ManagedCopy was requested without a FilePath. Imported without managed-copy status."
                    )

                ElseIf managedCopy AndAlso
                       Not File.Exists(filePath) Then

                    managedCopy = False

                    result.Warnings.Add(
                        "Correspondence row " &
                        rowNumber.ToString() &
                        ": managed-copy source file was not found. The record was preserved as a link instead."
                    )

                End If

                parentSubmission.Correspondence.Add(
                    New CorrespondenceItem With {
                        .Id = Guid.NewGuid(),
                        .ItemDate = itemDate.Value.Date,
                        .Type = itemType,
                        .Title = title,
                        .Notes = notes,
                        .LocalFilePath = filePath,
                        .SourceUrl = sourceUrl,
                        .IsManagedCopy = managedCopy
                    }
                )

                seenIds.Add(correspondenceId)

            Next

        End Sub


        ' =====================================================
        ' Headers
        ' =====================================================

        Private Function BuildHeaderMap(
            worksheet As IXLWorksheet
        ) As Dictionary(Of String, Integer)

            Dim headers As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            Dim lastCell As IXLCell = worksheet.Row(1).LastCellUsed()

            If lastCell Is Nothing Then
                Return headers
            End If

            Dim lastColumn As Integer = lastCell.Address.ColumnNumber

            For columnNumber As Integer = 1 To lastColumn

                Dim rawHeader As String = ReadText(
                    worksheet.Cell(1, columnNumber)
                )

                If String.IsNullOrWhiteSpace(rawHeader) Then
                    Continue For
                End If

                Dim normalized As String = NormalizeHeader(rawHeader)

                If Not headers.ContainsKey(normalized) Then
                    headers.Add(normalized, columnNumber)
                End If

            Next

            Return headers

        End Function


        Private Function NormalizeHeader(
            headerText As String
        ) As String

            Dim value As String = headerText.Trim()

            If value.EndsWith("*", StringComparison.Ordinal) Then
                value = value.Substring(0, value.Length - 1)
            End If

            value = value.Replace(" ", String.Empty)
            value = value.Replace("_", String.Empty)

            Return value.ToUpperInvariant()

        End Function


        Private Sub RequireHeaders(
            headers As Dictionary(Of String, Integer),
            ParamArray requiredHeaders As String()
        )

            For Each requiredHeader As String In requiredHeaders

                Dim normalized As String = NormalizeHeader(requiredHeader)

                If Not headers.ContainsKey(normalized) Then

                    Throw New InvalidDataException(
                        "The worksheet is missing the required '" &
                        requiredHeader &
                        "' column."
                    )

                End If

            Next

        End Sub


        ' =====================================================
        ' Cell reading
        ' =====================================================

        Private Function GetLastRowNumber(
            worksheet As IXLWorksheet
        ) As Integer

            Dim lastRow As IXLRow = worksheet.LastRowUsed()

            If lastRow Is Nothing Then
                Return 1
            End If

            Return lastRow.RowNumber()

        End Function


        Private Function ReadText(
            cell As IXLCell
        ) As String

            If cell Is Nothing Then
                Return String.Empty
            End If

            Return cell.GetFormattedString().Trim()

        End Function


        Private Function ReadOptionalText(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            headers As Dictionary(Of String, Integer),
            headerName As String
        ) As String

            Dim normalized As String = NormalizeHeader(headerName)

            If Not headers.ContainsKey(normalized) Then
                Return String.Empty
            End If

            Return ReadText(
                worksheet.Cell(
                    rowNumber,
                    headers(normalized)
                )
            )

        End Function


        Private Function ReadOptionalDate(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            headers As Dictionary(Of String, Integer),
            headerName As String
        ) As DateTime?

            Dim normalized As String = NormalizeHeader(headerName)

            If Not headers.ContainsKey(normalized) Then
                Return Nothing
            End If

            Return ReadDate(
                worksheet.Cell(
                    rowNumber,
                    headers(normalized)
                )
            )

        End Function


        Private Function ReadDate(
            cell As IXLCell
        ) As DateTime?

            If cell Is Nothing Then
                Return Nothing
            End If

            If cell.IsEmpty() Then
                Return Nothing
            End If

            Dim parsedDate As DateTime

            If cell.TryGetValue(Of DateTime)(parsedDate) Then
                Return parsedDate.Date
            End If

            Dim numericValue As Double

            If cell.TryGetValue(Of Double)(numericValue) Then

                Try
                    Return DateTime.FromOADate(numericValue).Date
                Catch
                End Try

            End If

            Dim textValue As String = cell.GetFormattedString().Trim()

            If DateTime.TryParse(textValue, parsedDate) Then
                Return parsedDate.Date
            End If

            Return Nothing

        End Function


        ' =====================================================
        ' URL validation
        ' =====================================================

        Private Function ValidateUrl(
            value As String,
            rowDescription As String,
            result As ExcelImportResult
        ) As String

            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Dim parsedUri As Uri = Nothing

            If Not Uri.TryCreate(
                value,
                UriKind.Absolute,
                parsedUri
            ) Then

                result.Warnings.Add(
                    rowDescription &
                    ": invalid URL '" &
                    value &
                    "'. URL was left blank."
                )

                Return String.Empty

            End If

            If parsedUri Is Nothing Then
                Return String.Empty
            End If

            If parsedUri.Scheme <> Uri.UriSchemeHttp AndAlso
               parsedUri.Scheme <> Uri.UriSchemeHttps Then

                result.Warnings.Add(
                    rowDescription &
                    ": URL must use http or https. URL was left blank."
                )

                Return String.Empty

            End If

            Return value.Trim()

        End Function


        ' =====================================================
        ' Enum parsing
        ' =====================================================

        Private Function NormalizeToken(
            value As String
        ) As String

            If value Is Nothing Then
                Return String.Empty
            End If

            Dim result As String = value.Trim().ToUpperInvariant()

            result = result.Replace(" ", String.Empty)
            result = result.Replace("-", String.Empty)
            result = result.Replace("_", String.Empty)

            Return result

        End Function


        Private Function TryParsePaperStage(
            value As String,
            ByRef result As PaperStage
        ) As Boolean

            Select Case NormalizeToken(value)

                Case "IDEA"
                    result = PaperStage.Idea

                Case "DRAFT"
                    result = PaperStage.Draft

                Case "SUBMITTED"
                    result = PaperStage.Submitted

                Case "UNDERREVIEW"
                    result = PaperStage.UnderReview

                Case "REVISION"
                    result = PaperStage.Revision

                Case "ACCEPTED"
                    result = PaperStage.Accepted

                Case "INPRESS"
                    result = PaperStage.InPress

                Case "PUBLISHED"
                    result = PaperStage.Published

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function TryParseLocation(
            value As String,
            ByRef result As ManuscriptLocation
        ) As Boolean

            Select Case NormalizeToken(value)

                Case "PIPELINE"
                    result = ManuscriptLocation.Pipeline

                Case "PUBLISHED"
                    result = ManuscriptLocation.Published

                Case "FILEDRAWER"
                    result = ManuscriptLocation.FileDrawer

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function TryParseDecision(
            value As String,
            ByRef result As EditorialDecision
        ) As Boolean

            Select Case NormalizeToken(value)

                Case "REJECTED"
                    result = EditorialDecision.Rejected

                Case "DESKREJECTED"
                    result = EditorialDecision.DeskRejected

                Case "REJECTEDAFTERREVIEW"
                    result = EditorialDecision.RejectedAfterReview

                Case "MAJORREVISION"
                    result = EditorialDecision.MajorRevision

                Case "MINORREVISION"
                    result = EditorialDecision.MinorRevision

                Case "REVISEANDRESUBMIT"
                    result = EditorialDecision.ReviseAndResubmit

                Case "ACCEPTED"
                    result = EditorialDecision.Accepted

                Case "WITHDRAWN"
                    result = EditorialDecision.Withdrawn

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function TryParseCorrespondenceType(
            value As String,
            ByRef result As CorrespondenceType
        ) As Boolean

            Select Case NormalizeToken(value)

                Case "DECISIONLETTER"
                    result = CorrespondenceType.DecisionLetter

                Case "REVIEWERCOMMENTS"
                    result = CorrespondenceType.ReviewerComments

                Case "EDITOREMAIL"
                    result = CorrespondenceType.EditorEmail

                Case "COVERLETTER"
                    result = CorrespondenceType.CoverLetter

                Case "RESPONSETOREVIEWERS"
                    result = CorrespondenceType.ResponseToReviewers

                Case "REVISEDMANUSCRIPT"
                    result = CorrespondenceType.RevisedManuscript

                Case "ACCEPTANCELETTER"
                    result = CorrespondenceType.AcceptanceLetter

                Case "PORTALSNAPSHOT"
                    result = CorrespondenceType.PortalSnapshot

                Case "OTHER"
                    result = CorrespondenceType.Other

                Case Else
                    Return False

            End Select

            Return True

        End Function

    End Class

End Namespace