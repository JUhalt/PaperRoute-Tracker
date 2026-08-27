Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports ClosedXML.Excel
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class ExcelColumnPreview

        Public Property ColumnNumber As Integer
        Public Property HeaderName As String = String.Empty
        Public Property SampleText As String = String.Empty

    End Class


    Public Class FlexibleExcelImporter

        Public Function GetWorksheetNames(
            filePath As String
        ) As List(Of String)

            ValidateFilePath(filePath)

            Dim names As New List(Of String)()

            Using workbook As New XLWorkbook(filePath)

                For Each worksheet As IXLWorksheet In workbook.Worksheets
                    names.Add(worksheet.Name)
                Next

            End Using

            Return names

        End Function


        Public Function ReadWorksheetColumns(
            filePath As String,
            worksheetName As String,
            headerRow As Integer
        ) As List(Of ExcelColumnPreview)

            ValidateFilePath(filePath)

            If headerRow < 1 Then
                Throw New ArgumentOutOfRangeException(NameOf(headerRow))
            End If

            Dim result As New List(Of ExcelColumnPreview)()

            Using workbook As New XLWorkbook(filePath)

                Dim worksheet As IXLWorksheet =
                    GetWorksheet(workbook, worksheetName)

                Dim lastHeaderCell As IXLCell =
                    worksheet.Row(headerRow).LastCellUsed()

                If lastHeaderCell Is Nothing Then
                    Return result
                End If

                Dim lastColumn As Integer =
                    lastHeaderCell.Address.ColumnNumber

                For columnNumber As Integer = 1 To lastColumn

                    Dim headerText As String =
                        ReadText(
                            worksheet.Cell(
                                headerRow,
                                columnNumber
                            )
                        )

                    If String.IsNullOrWhiteSpace(headerText) Then
                        headerText = "Column " & columnNumber.ToString()
                    End If

                    Dim samples As New List(Of String)()

                    For rowNumber As Integer = headerRow + 1 To headerRow + 3

                        Dim value As String =
                            ReadText(
                                worksheet.Cell(
                                    rowNumber,
                                    columnNumber
                                )
                            )

                        If Not String.IsNullOrWhiteSpace(value) Then
                            samples.Add(value)
                        End If

                    Next

                    result.Add(
                        New ExcelColumnPreview With {
                            .ColumnNumber = columnNumber,
                            .HeaderName = headerText,
                            .SampleText = String.Join("  •  ", samples)
                        }
                    )

                Next

            End Using

            Return result

        End Function


        Public Function SuggestField(
            headerText As String
        ) As ExcelImportField

            Dim normalized As String =
                NormalizeHeader(headerText)

            Select Case normalized

                Case "TITLE", "PAPER", "PAPERNAME", "MANUSCRIPT", "MANUSCRIPTTITLE", "PAPERTITLE", "PROJECT"
                    Return ExcelImportField.Title

                Case "COAUTHORS", "COAUTHOR", "AUTHORS", "AUTHOR", "COLLABORATORS", "COLLABORATOR"
                    ' PaperRoute authors are structured records. Do not
                    ' silently collapse author names into legacy free text.
                    Return ExcelImportField.Ignore

                Case "TARGETJOURNAL", "TARGETOUTLET", "TARGET", "NEXTJOURNAL", "NEXTTARGET"
                    Return ExcelImportField.TargetJournal

                Case "JOURNAL", "OUTLET", "PUBLICATION", "SUBMISSIONJOURNAL", "SUBMITTEDTO"
                    Return ExcelImportField.SubmissionJournal

                Case "STATUS", "STAGE", "CURRENTSTAGE", "CURRENTSTATUS", "PAPERSTATUS", "MANUSCRIPTSTATUS"
                    Return ExcelImportField.CurrentStage

                Case "LOCATION", "SHELF", "BUCKET"
                    Return ExcelImportField.Location

                Case "STAGEENTERED", "STAGEENTEREDDATE", "STATUSDATE", "STAGEDATE"
                    Return ExcelImportField.StageEnteredDate

                Case "SUBMITTED", "SUBMITTEDDATE", "SUBMISSIONDATE", "DATESUBMITTED", "DATESENT", "SENTDATE"
                    Return ExcelImportField.SubmissionDate

                Case "MANUSCRIPTNUMBER", "MANUSCRIPTNO", "MANUSCRIPTID", "SUBMISSIONID", "SUBMISSIONNUMBER", "TRACKINGNUMBER"
                    Return ExcelImportField.ManuscriptNumber

                Case "PORTAL", "PORTALURL", "SUBMISSIONURL", "URL", "LINK"
                    Return ExcelImportField.PortalUrl

                Case "DECISION", "OUTCOME", "EDITORIALDECISION", "RESULT", "RESPONSE"
                    Return ExcelImportField.Decision

                Case "DECISIONDATE", "RESPONSEDATE", "OUTCOMEDATE", "DATEDECIDED", "RECEIVEDDATE"
                    Return ExcelImportField.DecisionDate

                Case "REVISIONDEADLINE", "DEADLINE", "REVISIONDUE", "DUEDATE", "RRMDEADLINE"
                    Return ExcelImportField.RevisionDeadline

                Case "NOTES", "NOTE", "COMMENTS", "COMMENT", "MEMO", "DETAILS"
                    Return ExcelImportField.Notes

                Case "FILEDRAWERDATE", "FILEDDATE", "ARCHIVEDDATE"
                    Return ExcelImportField.FileDrawerDate

                Case "FILEDRAWERREASON", "FILEREASON", "ARCHIVEREASON"
                    Return ExcelImportField.FileDrawerReason

                Case Else
                    Return ExcelImportField.Ignore

            End Select

        End Function


        Public Function GetFieldDisplayNames() As List(Of String)

            Dim result As New List(Of String)()

            For Each field As ExcelImportField In [Enum].GetValues(GetType(ExcelImportField))

                If field = ExcelImportField.CoAuthors Then
                    Continue For
                End If

                result.Add(GetFieldDisplayName(field))

            Next

            Return result

        End Function


        Public Function GetFieldDisplayName(
            field As ExcelImportField
        ) As String

            Select Case field

                Case ExcelImportField.Ignore
                    Return "Ignore this column"

                Case ExcelImportField.Title
                    Return "Title"

                Case ExcelImportField.CoAuthors
                    Return "Co-authors"

                Case ExcelImportField.TargetJournal
                    Return "Target journal"

                Case ExcelImportField.CurrentStage
                    Return "Current stage"

                Case ExcelImportField.Location
                    Return "Location"

                Case ExcelImportField.StageEnteredDate
                    Return "Stage entered date"

                Case ExcelImportField.SubmissionJournal
                    Return "Submission journal"

                Case ExcelImportField.SubmissionDate
                    Return "Submitted date"

                Case ExcelImportField.ManuscriptNumber
                    Return "Manuscript number"

                Case ExcelImportField.PortalUrl
                    Return "Publisher portal URL"

                Case ExcelImportField.Decision
                    Return "Editorial decision"

                Case ExcelImportField.DecisionDate
                    Return "Decision date"

                Case ExcelImportField.RevisionDeadline
                    Return "Revision deadline"

                Case ExcelImportField.Notes
                    Return "Notes"

                Case ExcelImportField.FileDrawerDate
                    Return "File Drawer date"

                Case ExcelImportField.FileDrawerReason
                    Return "File Drawer reason"

                Case Else
                    Return field.ToString()

            End Select

        End Function


        Public Function FieldFromDisplayName(
            displayName As String
        ) As ExcelImportField

            For Each field As ExcelImportField In [Enum].GetValues(GetType(ExcelImportField))

                If String.Equals(
                    GetFieldDisplayName(field),
                    displayName,
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return field

                End If

            Next

            Return ExcelImportField.Ignore

        End Function


        Public Function Import(
            filePath As String,
            worksheetName As String,
            headerRow As Integer,
            mappings As IEnumerable(Of ExcelColumnMapping)
        ) As ExcelImportResult

            ValidateFilePath(filePath)

            If headerRow < 1 Then
                Throw New ArgumentOutOfRangeException(NameOf(headerRow))
            End If

            If mappings Is Nothing Then
                Throw New ArgumentNullException(NameOf(mappings))
            End If

            Dim mappingByField As New Dictionary(Of ExcelImportField, Integer)()

            For Each mapping As ExcelColumnMapping In mappings

                If mapping Is Nothing OrElse
                   mapping.Field = ExcelImportField.Ignore Then

                    Continue For

                End If

                If mappingByField.ContainsKey(mapping.Field) Then

                    Throw New InvalidDataException(
                        "More than one spreadsheet column was mapped to '" &
                        GetFieldDisplayName(mapping.Field) &
                        "'."
                    )

                End If

                mappingByField.Add(
                    mapping.Field,
                    mapping.ColumnNumber
                )

            Next

            If Not mappingByField.ContainsKey(ExcelImportField.Title) Then

                Throw New InvalidDataException(
                    "Map one spreadsheet column to Title before importing."
                )

            End If

            Dim result As New ExcelImportResult()

            Using workbook As New XLWorkbook(filePath)

                Dim worksheet As IXLWorksheet =
                    GetWorksheet(workbook, worksheetName)

                Dim lastRowUsed As IXLRow =
                    worksheet.LastRowUsed()

                If lastRowUsed Is Nothing Then
                    Return result
                End If

                Dim manuscriptsByTitle As New Dictionary(Of String, Manuscript)(StringComparer.OrdinalIgnoreCase)
                Dim manuscriptOrder As New List(Of Manuscript)()

                For rowNumber As Integer = headerRow + 1 To lastRowUsed.RowNumber()

                    Dim title As String =
                        ReadMappedText(
                            worksheet,
                            rowNumber,
                            mappingByField,
                            ExcelImportField.Title
                        )

                    If String.IsNullOrWhiteSpace(title) Then
                        Continue For
                    End If

                    result.RowsRead += 1

                    Dim normalizedTitle As String = title.Trim()
                    Dim manuscript As Manuscript = Nothing
                    Dim isNewManuscript As Boolean = False

                    If Not manuscriptsByTitle.TryGetValue(normalizedTitle, manuscript) Then

                        manuscript = New Manuscript With {
                            .Id = Guid.NewGuid(),
                            .Title = normalizedTitle,
                            .TargetJournal = String.Empty,
                            .CurrentStage = PaperStage.Draft,
                            .Location = ManuscriptLocation.Pipeline,
                            .StageEnteredDate = DateTime.Now,
                            .History = New List(Of HistoryEvent)(),
                            .Submissions = New List(Of JournalSubmission)()
                        }

                        manuscriptsByTitle.Add(normalizedTitle, manuscript)
                        manuscriptOrder.Add(manuscript)
                        isNewManuscript = True

                    End If

                    ApplyManuscriptFields(
                        worksheet,
                        rowNumber,
                        mappingByField,
                        manuscript,
                        isNewManuscript,
                        result
                    )

                    Dim submission As JournalSubmission =
                        BuildSubmission(
                            worksheet,
                            rowNumber,
                            mappingByField,
                            manuscript,
                            result
                        )

                    If submission IsNot Nothing Then
                        manuscript.Submissions.Add(submission)
                    End If

                    ApplyInferredState(
                        manuscript,
                        submission,
                        mappingByField
                    )

                    AddMappedHistory(
                        manuscript,
                        submission,
                        rowNumber
                    )

                Next

                For Each manuscript As Manuscript In manuscriptOrder
                    result.Manuscripts.Add(manuscript)
                Next

            End Using

            Return result

        End Function


        Private Sub ApplyManuscriptFields(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            mappingByField As Dictionary(Of ExcelImportField, Integer),
            manuscript As Manuscript,
            isNewManuscript As Boolean,
            result As ExcelImportResult
        )

            Dim targetJournal As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.TargetJournal
                )

            If Not String.IsNullOrWhiteSpace(targetJournal) Then
                manuscript.TargetJournal = targetJournal
            End If

            Dim stageText As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.CurrentStage
                )

            If Not String.IsNullOrWhiteSpace(stageText) Then

                Dim stage As PaperStage

                If TryParseStage(stageText, stage) Then
                    manuscript.CurrentStage = stage
                Else
                    result.Warnings.Add(
                        "Row " & rowNumber.ToString() &
                        ": unrecognized stage '" & stageText & "'."
                    )
                End If

            End If

            Dim locationText As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.Location
                )

            If Not String.IsNullOrWhiteSpace(locationText) Then

                Dim location As ManuscriptLocation

                If TryParseLocation(locationText, location) Then
                    manuscript.Location = location
                Else
                    result.Warnings.Add(
                        "Row " & rowNumber.ToString() &
                        ": unrecognized location '" & locationText & "'."
                    )
                End If

            End If

            Dim stageEnteredDate As DateTime? =
                ReadMappedDate(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.StageEnteredDate
                )

            If stageEnteredDate.HasValue Then
                manuscript.StageEnteredDate = stageEnteredDate.Value.Date
            End If

            Dim fileDrawerDate As DateTime? =
                ReadMappedDate(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.FileDrawerDate
                )

            If fileDrawerDate.HasValue Then
                manuscript.FileDrawerDate = fileDrawerDate.Value.Date
                manuscript.Location = ManuscriptLocation.FileDrawer
            End If

            Dim fileDrawerReason As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.FileDrawerReason
                )

            If Not String.IsNullOrWhiteSpace(fileDrawerReason) Then
                manuscript.FileDrawerReason = fileDrawerReason
            End If

            If manuscript.CurrentStage = PaperStage.Published Then
                manuscript.Location = ManuscriptLocation.Published
            ElseIf manuscript.Location = ManuscriptLocation.Published Then
                manuscript.CurrentStage = PaperStage.Published
            End If

        End Sub


        Private Function BuildSubmission(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            mappingByField As Dictionary(Of ExcelImportField, Integer),
            manuscript As Manuscript,
            result As ExcelImportResult
        ) As JournalSubmission

            Dim submissionJournal As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.SubmissionJournal
                )

            Dim journal As String = submissionJournal

            Dim submittedDate As DateTime? =
                ReadMappedDate(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.SubmissionDate
                )

            Dim manuscriptNumber As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.ManuscriptNumber
                )

            Dim portalUrl As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.PortalUrl
                )

            Dim notes As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.Notes
                )

            Dim decisionText As String =
                ReadMappedText(
                    worksheet,
                    rowNumber,
                    mappingByField,
                    ExcelImportField.Decision
                )

            Dim hasSubmissionData As Boolean =
                Not String.IsNullOrWhiteSpace(submissionJournal) OrElse
                submittedDate.HasValue OrElse
                Not String.IsNullOrWhiteSpace(manuscriptNumber) OrElse
                Not String.IsNullOrWhiteSpace(portalUrl) OrElse
                Not String.IsNullOrWhiteSpace(decisionText)

            If Not hasSubmissionData Then

                If Not String.IsNullOrWhiteSpace(notes) Then

                    manuscript.History.Add(
                        New HistoryEvent With {
                            .Id = Guid.NewGuid(),
                            .EventDate = DateTime.Now,
                            .Stage = manuscript.CurrentStage,
                            .Note = "Imported note: " & notes
                        }
                    )

                End If

                Return Nothing

            End If

            If String.IsNullOrWhiteSpace(journal) Then
                journal = manuscript.TargetJournal
            End If

            If String.IsNullOrWhiteSpace(journal) Then

                journal = "Unknown journal"

                result.Warnings.Add(
                    "Row " & rowNumber.ToString() &
                    ": submission-related data had no journal. 'Unknown journal' was used."
                )

            End If

            If Not submittedDate.HasValue Then

                submittedDate = DateTime.Now.Date

                result.Warnings.Add(
                    "Row " & rowNumber.ToString() &
                    ": submission-related data had no readable submission date. Today's date was used."
                )

            End If

            Dim submission As New JournalSubmission With {
                .Id = Guid.NewGuid(),
                .JournalName = journal,
                .ManuscriptNumber = manuscriptNumber,
                .SubmittedDate = submittedDate.Value.Date,
                .Notes = notes,
                .PortalUrl = portalUrl,
                .Decisions = New List(Of EditorialDecisionEvent)(),
                .Correspondence = New List(Of CorrespondenceItem)()
            }

            If String.IsNullOrWhiteSpace(manuscript.TargetJournal) Then
                manuscript.TargetJournal = journal
            End If

            If Not String.IsNullOrWhiteSpace(decisionText) Then

                Dim decision As EditorialDecision

                If TryParseDecision(decisionText, decision) Then

                    Dim decisionDate As DateTime? =
                        ReadMappedDate(
                            worksheet,
                            rowNumber,
                            mappingByField,
                            ExcelImportField.DecisionDate
                        )

                    If Not decisionDate.HasValue Then

                        decisionDate = submittedDate.Value.Date

                        result.Warnings.Add(
                            "Row " & rowNumber.ToString() &
                            ": decision '" & decisionText &
                            "' had no readable decision date. The submission date was used."
                        )

                    End If

                    Dim revisionDeadline As DateTime? =
                        ReadMappedDate(
                            worksheet,
                            rowNumber,
                            mappingByField,
                            ExcelImportField.RevisionDeadline
                        )

                    submission.Decisions.Add(
                        New EditorialDecisionEvent With {
                            .Id = Guid.NewGuid(),
                            .DecisionDate = decisionDate.Value.Date,
                            .Decision = decision,
                            .RevisionDeadline = revisionDeadline,
                            .Notes = "Imported from mapped spreadsheet."
                        }
                    )

                Else

                    result.Warnings.Add(
                        "Row " & rowNumber.ToString() &
                        ": unrecognized editorial decision '" & decisionText & "'."
                    )

                End If

            End If

            Return submission

        End Function


        Private Sub ApplyInferredState(
            manuscript As Manuscript,
            submission As JournalSubmission,
            mappingByField As Dictionary(Of ExcelImportField, Integer)
        )

            Dim hasExplicitStage As Boolean =
                mappingByField.ContainsKey(ExcelImportField.CurrentStage)

            Dim hasExplicitLocation As Boolean =
                mappingByField.ContainsKey(ExcelImportField.Location)

            If submission IsNot Nothing Then

                If Not hasExplicitStage Then

                    manuscript.CurrentStage = PaperStage.Submitted

                    If submission.Decisions.Count > 0 Then

                        Dim latestDecision As EditorialDecisionEvent =
                            submission.Decisions(submission.Decisions.Count - 1)

                        Select Case latestDecision.Decision

                            Case EditorialDecision.MajorRevision,
                                 EditorialDecision.MinorRevision,
                                 EditorialDecision.ReviseAndResubmit

                                manuscript.CurrentStage = PaperStage.Revision

                            Case EditorialDecision.Accepted
                                manuscript.CurrentStage = PaperStage.Accepted

                            Case EditorialDecision.Rejected,
                                 EditorialDecision.DeskRejected,
                                 EditorialDecision.RejectedAfterReview

                                manuscript.CurrentStage = PaperStage.Draft

                        End Select

                    End If

                End If

                If Not mappingByField.ContainsKey(ExcelImportField.StageEnteredDate) Then

                    manuscript.StageEnteredDate =
                        If(
                            submission.Decisions.Count > 0,
                            submission.Decisions(submission.Decisions.Count - 1).DecisionDate,
                            submission.SubmittedDate
                        )

                End If

            End If

            If manuscript.CurrentStage = PaperStage.Published Then
                manuscript.Location = ManuscriptLocation.Published
            ElseIf manuscript.Location = ManuscriptLocation.Published Then
                manuscript.CurrentStage = PaperStage.Published
            ElseIf Not hasExplicitLocation AndAlso
                   manuscript.Location <> ManuscriptLocation.FileDrawer Then

                manuscript.Location = ManuscriptLocation.Pipeline

            End If

        End Sub


        Private Sub AddMappedHistory(
            manuscript As Manuscript,
            submission As JournalSubmission,
            rowNumber As Integer
        )

            Dim eventDate As DateTime = manuscript.StageEnteredDate

            If submission IsNot Nothing Then

                eventDate = submission.SubmittedDate

                If submission.Decisions.Count > 0 Then
                    eventDate = submission.Decisions(submission.Decisions.Count - 1).DecisionDate
                End If

            End If

            manuscript.History.Add(
                New HistoryEvent With {
                    .Id = Guid.NewGuid(),
                    .EventDate = eventDate,
                    .Stage = manuscript.CurrentStage,
                    .Note = "Imported from mapped spreadsheet row " & rowNumber.ToString() & "."
                }
            )

        End Sub


        Private Function TryParseStage(
            value As String,
            ByRef stage As PaperStage
        ) As Boolean

            Dim normalized As String = NormalizeHeader(value)

            Select Case normalized

                Case "IDEA", "CONCEPT", "PLANNED"
                    stage = PaperStage.Idea

                Case "DRAFT", "WRITING", "INPREPARATION", "PREPARATION", "REJECTED", "REJECT", "WITHDRAWN"
                    stage = PaperStage.Draft

                Case "SUBMITTED", "SUBMISSION", "AWAITINGEDITOR"
                    stage = PaperStage.Submitted

                Case "UNDERREVIEW", "INREVIEW", "REVIEW", "PEERREVIEW"
                    stage = PaperStage.UnderReview

                Case "REVISION", "REVISING", "REVISEANDRESUBMIT", "RR", "MAJORREVISION", "MINORREVISION"
                    stage = PaperStage.Revision

                Case "ACCEPTED", "ACCEPT"
                    stage = PaperStage.Accepted

                Case "INPRESS", "ONLINEAHEADOFPRINT", "PRODUCTION"
                    stage = PaperStage.InPress

                Case "PUBLISHED", "PUBLICATION", "PUB"
                    stage = PaperStage.Published

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function TryParseLocation(
            value As String,
            ByRef location As ManuscriptLocation
        ) As Boolean

            Dim normalized As String = NormalizeHeader(value)

            Select Case normalized

                Case "PIPELINE", "ACTIVE", "INPROGRESS"
                    location = ManuscriptLocation.Pipeline

                Case "PUBLISHED", "PUBLICATION"
                    location = ManuscriptLocation.Published

                Case "FILEDRAWER", "FILED", "ARCHIVED", "ARCHIVE", "SHELVED"
                    location = ManuscriptLocation.FileDrawer

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function TryParseDecision(
            value As String,
            ByRef decision As EditorialDecision
        ) As Boolean

            Dim normalized As String = NormalizeHeader(value)

            Select Case normalized

                Case "REJECTED", "REJECT", "R", "DECLINED"
                    decision = EditorialDecision.Rejected

                Case "DESKREJECTED", "DESKREJECT", "REJECTEDWITHOUTREVIEW"
                    decision = EditorialDecision.DeskRejected

                Case "REJECTEDAFTERREVIEW", "REJECTAFTERREVIEW"
                    decision = EditorialDecision.RejectedAfterReview

                Case "MAJORREVISION", "MAJORREVISIONS"
                    decision = EditorialDecision.MajorRevision

                Case "MINORREVISION", "MINORREVISIONS"
                    decision = EditorialDecision.MinorRevision

                Case "REVISEANDRESUBMIT", "RESUBMIT", "RR", "R&R"
                    decision = EditorialDecision.ReviseAndResubmit

                Case "ACCEPTED", "ACCEPT"
                    decision = EditorialDecision.Accepted

                Case "WITHDRAWN", "WITHDRAW"
                    decision = EditorialDecision.Withdrawn

                Case Else
                    Return False

            End Select

            Return True

        End Function


        Private Function ReadMappedText(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            mappingByField As Dictionary(Of ExcelImportField, Integer),
            field As ExcelImportField
        ) As String

            Dim columnNumber As Integer

            If Not mappingByField.TryGetValue(field, columnNumber) Then
                Return String.Empty
            End If

            Return ReadText(
                worksheet.Cell(
                    rowNumber,
                    columnNumber
                )
            )

        End Function


        Private Function ReadMappedDate(
            worksheet As IXLWorksheet,
            rowNumber As Integer,
            mappingByField As Dictionary(Of ExcelImportField, Integer),
            field As ExcelImportField
        ) As DateTime?

            Dim columnNumber As Integer

            If Not mappingByField.TryGetValue(field, columnNumber) Then
                Return Nothing
            End If

            Return ReadDate(
                worksheet.Cell(
                    rowNumber,
                    columnNumber
                )
            )

        End Function


        Private Function ReadText(
            cell As IXLCell
        ) As String

            If cell Is Nothing OrElse cell.IsEmpty() Then
                Return String.Empty
            End If

            Return cell.GetFormattedString().Trim()

        End Function


        Private Function ReadDate(
            cell As IXLCell
        ) As DateTime?

            If cell Is Nothing OrElse cell.IsEmpty() Then
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

            Dim textValue As String =
                cell.GetFormattedString().Trim()

            If DateTime.TryParse(textValue, parsedDate) Then
                Return parsedDate.Date
            End If

            Return Nothing

        End Function


        Private Function NormalizeHeader(
            value As String
        ) As String

            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Dim builder As New StringBuilder()

            For Each character As Char In value.ToUpperInvariant()

                If Char.IsLetterOrDigit(character) OrElse character = "&"c Then
                    builder.Append(character)
                End If

            Next

            Return builder.ToString()

        End Function


        Private Function GetWorksheet(
            workbook As XLWorkbook,
            worksheetName As String
        ) As IXLWorksheet

            If workbook.Worksheets.Count = 0 Then
                Throw New InvalidDataException("The Excel workbook contains no worksheets.")
            End If

            If String.IsNullOrWhiteSpace(worksheetName) Then
                Return workbook.Worksheet(1)
            End If

            For Each worksheet As IXLWorksheet In workbook.Worksheets

                If String.Equals(
                    worksheet.Name,
                    worksheetName,
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return worksheet

                End If

            Next

            Throw New InvalidDataException(
                "The worksheet '" & worksheetName & "' could not be found."
            )

        End Function


        Private Sub ValidateFilePath(
            filePath As String
        )

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("An Excel file path is required.")
            End If

            If Not File.Exists(filePath) Then
                Throw New FileNotFoundException("The Excel workbook could not be found.", filePath)
            End If

        End Sub

    End Class

End Namespace
