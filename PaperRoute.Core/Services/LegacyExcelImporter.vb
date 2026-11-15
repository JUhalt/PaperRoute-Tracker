Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class ExcelImportResult

        Public Property Manuscripts As New List(Of Manuscript)()
        Public Property Warnings As New List(Of String)()
        Public Property RowsRead As Integer = 0


        Public ReadOnly Property SubmissionCount As Integer
            Get

                Dim count As Integer = 0

                For Each manuscript As Manuscript In Manuscripts
                    count += manuscript.Submissions.Count
                Next

                Return count

            End Get
        End Property


        Public ReadOnly Property DecisionCount As Integer
            Get

                Dim count As Integer = 0

                For Each manuscript As Manuscript In Manuscripts

                    For Each submission As JournalSubmission In manuscript.Submissions
                        count += submission.Decisions.Count
                    Next

                Next

                Return count

            End Get
        End Property

    End Class


    Public Class LegacyExcelImporter

        Public Function CanImport(filePath As String) As Boolean

            If String.IsNullOrWhiteSpace(filePath) OrElse
               Not File.Exists(filePath) Then

                Return False

            End If

            Try

                Using workbook As New XLWorkbook(filePath)

                    Dim worksheet As IXLWorksheet = FindWorksheet(workbook)
                    Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

                    Dim requiredHeaders As String() = {
                        "SUBMISSION",
                        "SUBMITTED",
                        "TITLE",
                        "JOURNAL",
                        "RESPONSE",
                        "STATUS"
                    }

                    For Each requiredHeader As String In requiredHeaders

                        If Not headers.ContainsKey(requiredHeader) Then
                            Return False
                        End If

                    Next

                End Using

                Return True

            Catch

                Return False

            End Try

        End Function


        Public Function Import(filePath As String) As ExcelImportResult

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("An Excel file path is required.")
            End If

            If Not File.Exists(filePath) Then
                Throw New FileNotFoundException("The Excel workbook could not be found.", filePath)
            End If

            Dim result As New ExcelImportResult()

            Using workbook As New XLWorkbook(filePath)

                Dim worksheet As IXLWorksheet = FindWorksheet(workbook)

                Dim headers As Dictionary(Of String, Integer) = BuildHeaderMap(worksheet)

                ValidateRequiredHeaders(headers)

                Dim lastRowUsed As IXLRow = worksheet.LastRowUsed()

                If lastRowUsed Is Nothing Then
                    Return result
                End If

                Dim lastRowNumber As Integer = lastRowUsed.RowNumber()

                Dim manuscriptsByTitle As New Dictionary(Of String, Manuscript)(StringComparer.OrdinalIgnoreCase)
                Dim manuscriptOrder As New List(Of Manuscript)()

                For rowNumber As Integer = 2 To lastRowNumber

                    result.RowsRead += 1

                    Dim title As String = ReadText(worksheet.Cell(rowNumber, headers("TITLE")))

                    If String.IsNullOrWhiteSpace(title) Then
                        Continue For
                    End If

                    Dim manuscript As Manuscript = Nothing

                    If Not manuscriptsByTitle.TryGetValue(title, manuscript) Then

                        manuscript = New Manuscript With {
                            .Id = Guid.NewGuid(),
                            .Title = title,
                            .CoAuthors = String.Empty,
                            .TargetJournal = String.Empty,
                            .CurrentStage = PaperStage.Draft,
                            .Location = ManuscriptLocation.Pipeline,
                            .StageEnteredDate = DateTime.Now
                        }

                        manuscriptsByTitle.Add(title, manuscript)
                        manuscriptOrder.Add(manuscript)

                    End If

                    Dim status As String = ReadText(worksheet.Cell(rowNumber, headers("STATUS"))).ToUpperInvariant()

                    Dim submittedDate As DateTime? = ReadDate(worksheet.Cell(rowNumber, headers("SUBMITTED")))

                    Dim responseCell As IXLCell = worksheet.Cell(rowNumber, headers("RESPONSE"))
                    Dim responseDate As DateTime? = ReadResponseDate(responseCell)

                    Dim rawJournal As String = ReadText(worksheet.Cell(rowNumber, headers("JOURNAL")))

                    Dim legacyJournalNote As String = String.Empty
                    Dim journalName As String = NormalizeJournalName(rawJournal, legacyJournalNote)

                    Dim attemptNumber As String = String.Empty

                    If headers.ContainsKey("SUBMISSION") Then
                        attemptNumber = ReadText(worksheet.Cell(rowNumber, headers("SUBMISSION")))
                    End If

                    Dim submission As JournalSubmission = Nothing

                    If Not String.IsNullOrWhiteSpace(journalName) Then

                        If submittedDate.HasValue Then

                            Dim submissionNotes As String = BuildSubmissionNotes(attemptNumber, legacyJournalNote)

                            submission = New JournalSubmission With {
                                .Id = Guid.NewGuid(),
                                .JournalName = journalName,
                                .ManuscriptNumber = String.Empty,
                                .SubmittedDate = submittedDate.Value.Date,
                                .Notes = submissionNotes,
                                .PortalUrl = String.Empty,
                                .Decisions = New List(Of EditorialDecisionEvent)(),
                                .Correspondence = New List(Of CorrespondenceItem)()
                            }

                            manuscript.Submissions.Add(submission)
                            manuscript.TargetJournal = journalName

                        Else

                            result.Warnings.Add(
                                "Row " &
                                rowNumber.ToString() &
                                ": '" &
                                title &
                                "' contains a journal but no readable submission date. The submission was skipped."
                            )

                        End If

                    End If

                    ApplyLegacyStatus(
                        manuscript,
                        submission,
                        status,
                        submittedDate,
                        responseDate,
                        rowNumber,
                        result
                    )

                    AddLegacyHistory(
                        manuscript,
                        journalName,
                        status,
                        submittedDate,
                        responseDate
                    )

                Next

                For Each manuscript As Manuscript In manuscriptOrder
                    result.Manuscripts.Add(manuscript)
                Next

            End Using

            Return result

        End Function


        ' =====================================================
        ' Worksheet / headers
        ' =====================================================

        Private Function FindWorksheet(workbook As XLWorkbook) As IXLWorksheet

            For Each candidate As IXLWorksheet In workbook.Worksheets

                If String.Equals(
                    candidate.Name.Trim(),
                    "Submission Sheet",
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return candidate

                End If

            Next

            If workbook.Worksheets.Count = 0 Then
                Throw New InvalidDataException("The Excel workbook contains no worksheets.")
            End If

            Return workbook.Worksheet(1)

        End Function


        Private Function BuildHeaderMap(
            worksheet As IXLWorksheet
        ) As Dictionary(Of String, Integer)

            Dim headers As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            Dim lastHeaderCell As IXLCell = worksheet.Row(1).LastCellUsed()

            If lastHeaderCell Is Nothing Then
                Return headers
            End If

            Dim lastColumn As Integer = lastHeaderCell.Address.ColumnNumber

            For columnNumber As Integer = 1 To lastColumn

                Dim headerText As String = ReadText(worksheet.Cell(1, columnNumber))

                If String.IsNullOrWhiteSpace(headerText) Then
                    Continue For
                End If

                Dim normalizedHeader As String = headerText.Trim().ToUpperInvariant()

                If Not headers.ContainsKey(normalizedHeader) Then
                    headers.Add(normalizedHeader, columnNumber)
                End If

            Next

            Return headers

        End Function


        Private Sub ValidateRequiredHeaders(
            headers As Dictionary(Of String, Integer)
        )

            Dim requiredHeaders As String() = {
                "SUBMISSION",
                "SUBMITTED",
                "TITLE",
                "JOURNAL",
                "RESPONSE",
                "STATUS"
            }

            For Each requiredHeader As String In requiredHeaders

                If Not headers.ContainsKey(requiredHeader) Then

                    Throw New InvalidDataException(
                        "This workbook is missing the required '" &
                        requiredHeader &
                        "' column."
                    )

                End If

            Next

        End Sub


        ' =====================================================
        ' Status mapping
        ' =====================================================

        Private Sub ApplyLegacyStatus(
            manuscript As Manuscript,
            submission As JournalSubmission,
            status As String,
            submittedDate As DateTime?,
            responseDate As DateTime?,
            rowNumber As Integer,
            result As ExcelImportResult
        )

            Select Case status

                Case "R"

                    manuscript.Location = ManuscriptLocation.Pipeline
                    manuscript.CurrentStage = PaperStage.Draft

                    AddDecisionIfPossible(
                        submission,
                        EditorialDecision.Rejected,
                        responseDate,
                        rowNumber,
                        result
                    )

                Case "RR"

                    manuscript.Location = ManuscriptLocation.Pipeline
                    manuscript.CurrentStage = PaperStage.Revision

                    AddDecisionIfPossible(
                        submission,
                        EditorialDecision.ReviseAndResubmit,
                        responseDate,
                        rowNumber,
                        result
                    )

                Case "S"

                    manuscript.Location = ManuscriptLocation.Pipeline
                    manuscript.CurrentStage = PaperStage.Submitted

                Case "IP"

                    manuscript.Location = ManuscriptLocation.Pipeline

                    If submission Is Nothing Then
                        manuscript.CurrentStage = PaperStage.Draft
                    Else
                        manuscript.CurrentStage = PaperStage.UnderReview
                    End If

                Case "P"

                    manuscript.Location = ManuscriptLocation.Published
                    manuscript.CurrentStage = PaperStage.Published

                Case "FD"

                    manuscript.Location = ManuscriptLocation.FileDrawer
                    manuscript.CurrentStage = PaperStage.Draft
                    manuscript.FileDrawerReason = "Imported from legacy spreadsheet."

                    If submittedDate.HasValue Then
                        manuscript.FileDrawerDate = submittedDate.Value
                    Else
                        manuscript.FileDrawerDate = DateTime.Now
                    End If

                Case ""

                    result.Warnings.Add(
                        "Row " &
                        rowNumber.ToString() &
                        ": no status was provided."
                    )

                Case Else

                    result.Warnings.Add(
                        "Row " &
                        rowNumber.ToString() &
                        ": unrecognized legacy status '" &
                        status &
                        "'."
                    )

            End Select

            manuscript.StageEnteredDate = DateTime.Now

        End Sub


        Private Sub AddDecisionIfPossible(
            submission As JournalSubmission,
            decision As EditorialDecision,
            responseDate As DateTime?,
            rowNumber As Integer,
            result As ExcelImportResult
        )

            If submission Is Nothing Then

                result.Warnings.Add(
                    "Row " &
                    rowNumber.ToString() &
                    ": a decision was recorded without an importable journal submission."
                )

                Return

            End If

            If Not responseDate.HasValue Then

                result.Warnings.Add(
                    "Row " &
                    rowNumber.ToString() &
                    ": a decision was recorded without a real response date."
                )

                Return

            End If

            submission.Decisions.Add(
                New EditorialDecisionEvent With {
                    .Id = Guid.NewGuid(),
                    .DecisionDate = responseDate.Value.Date,
                    .Decision = decision,
                    .RevisionDeadline = Nothing,
                    .Notes = "Imported from legacy spreadsheet."
                }
            )

        End Sub


        ' =====================================================
        ' History
        ' =====================================================

        Private Sub AddLegacyHistory(
            manuscript As Manuscript,
            journalName As String,
            status As String,
            submittedDate As DateTime?,
            responseDate As DateTime?
        )

            Dim eventDate As DateTime

            If responseDate.HasValue Then

                eventDate = responseDate.Value

            ElseIf submittedDate.HasValue Then

                eventDate = submittedDate.Value

            Else

                Return

            End If

            Dim note As String = "Imported legacy status: " & status & "."

            If Not String.IsNullOrWhiteSpace(journalName) Then
                note &= " Journal: " & journalName & "."
            End If

            manuscript.History.Add(
                New HistoryEvent With {
                    .Id = Guid.NewGuid(),
                    .EventDate = eventDate.Date,
                    .Stage = manuscript.CurrentStage,
                    .Note = note
                }
            )

        End Sub


        ' =====================================================
        ' Cell reading
        ' =====================================================

        Private Function ReadText(cell As IXLCell) As String

            If cell Is Nothing Then
                Return String.Empty
            End If

            Return cell.GetFormattedString().Trim()

        End Function


        Private Function ReadDate(cell As IXLCell) As DateTime?

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


        Private Function ReadResponseDate(cell As IXLCell) As DateTime?

            If cell Is Nothing Then
                Return Nothing
            End If

            If cell.HasFormula Then

                Dim formula As String = cell.FormulaA1

                If Not String.IsNullOrWhiteSpace(formula) AndAlso
                   formula.IndexOf("TODAY(", StringComparison.OrdinalIgnoreCase) >= 0 Then

                    Return Nothing

                End If

            End If

            Return ReadDate(cell)

        End Function


        ' =====================================================
        ' Legacy journal cleanup
        ' =====================================================

        Private Function NormalizeJournalName(
            rawJournal As String,
            ByRef legacyNote As String
        ) As String

            legacyNote = String.Empty

            If String.IsNullOrWhiteSpace(rawJournal) Then
                Return String.Empty
            End If

            Dim journal As String = rawJournal.Trim()

            Dim markerIndex As Integer =
                journal.IndexOf(
                    " - See Email",
                    StringComparison.OrdinalIgnoreCase
                )

            If markerIndex >= 0 Then

                legacyNote = "Legacy note: See Email."
                journal = journal.Substring(0, markerIndex).Trim()

                Return journal

            End If

            markerIndex =
                journal.IndexOf(
                    " - 2nd Attempt",
                    StringComparison.OrdinalIgnoreCase
                )

            If markerIndex >= 0 Then

                legacyNote = "Legacy note: 2nd attempt."
                journal = journal.Substring(0, markerIndex).Trim()

                Return journal

            End If

            markerIndex =
                journal.IndexOf(
                    " - 3rd Attempt",
                    StringComparison.OrdinalIgnoreCase
                )

            If markerIndex >= 0 Then

                legacyNote = "Legacy note: " & rawJournal.Substring(markerIndex + 3).Trim() & "."
                journal = journal.Substring(0, markerIndex).Trim()

                Return journal

            End If

            Return journal

        End Function


        Private Function BuildSubmissionNotes(
            attemptNumber As String,
            legacyJournalNote As String
        ) As String

            Dim notes As String = String.Empty

            If Not String.IsNullOrWhiteSpace(attemptNumber) Then
                notes = "Legacy submission attempt #" & attemptNumber & "."
            End If

            If Not String.IsNullOrWhiteSpace(legacyJournalNote) Then

                If Not String.IsNullOrWhiteSpace(notes) Then
                    notes &= " "
                End If

                notes &= legacyJournalNote

            End If

            Return notes

        End Function

    End Class

End Namespace