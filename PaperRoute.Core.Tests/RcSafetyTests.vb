Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports ClosedXML.Excel
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class RcSafetyTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    ' =====================================================
    ' Standard importer:
    ' complex manuscript route
    ' =====================================================

    <TestMethod>
    Public Sub StandardImporter_PreservesComplexRouteRelationships()

        Dim workbookPath As String =
            Path.Combine(
                _root,
                "complex-route.xlsx"
            )

        Dim generator As New StandardTemplateGenerator()

        generator.Generate(
            workbookPath
        )

        Using workbook As New XLWorkbook(
            workbookPath
        )

            Dim manuscripts As IXLWorksheet =
                workbook.Worksheet(
                    "Manuscripts"
                )

            SetText(
                manuscripts,
                2,
                "ManuscriptID",
                "M100"
            )

            SetText(
                manuscripts,
                2,
                "Title",
                "Complex Route Study"
            )


            SetText(
                manuscripts,
                2,
                "CurrentStage",
                "Revision"
            )

            SetText(
                manuscripts,
                2,
                "Location",
                "Pipeline"
            )

            SetText(
                manuscripts,
                2,
                "TargetJournal",
                "Journal B"
            )

            SetDate(
                manuscripts,
                2,
                "StageEnteredDate",
                New DateTime(
                    2026,
                    4,
                    1
                )
            )


            Dim submissions As IXLWorksheet =
                workbook.Worksheet(
                    "Submissions"
                )

            SetText(
                submissions,
                2,
                "SubmissionID",
                "S100"
            )

            SetText(
                submissions,
                2,
                "ManuscriptID",
                "M100"
            )

            SetText(
                submissions,
                2,
                "Journal",
                "Journal A"
            )

            SetText(
                submissions,
                2,
                "ManuscriptNumber",
                "JA-100"
            )

            SetDate(
                submissions,
                2,
                "SubmittedDate",
                New DateTime(
                    2026,
                    1,
                    10
                )
            )

            SetText(
                submissions,
                2,
                "Notes",
                "First route."
            )


            SetText(
                submissions,
                3,
                "SubmissionID",
                "S101"
            )

            SetText(
                submissions,
                3,
                "ManuscriptID",
                "M100"
            )

            SetText(
                submissions,
                3,
                "Journal",
                "Journal B"
            )

            SetText(
                submissions,
                3,
                "ManuscriptNumber",
                "JB-200"
            )

            SetDate(
                submissions,
                3,
                "SubmittedDate",
                New DateTime(
                    2026,
                    3,
                    1
                )
            )

            SetText(
                submissions,
                3,
                "Notes",
                "Second route."
            )


            Dim decisions As IXLWorksheet =
                workbook.Worksheet(
                    "Decisions"
                )

            SetText(
                decisions,
                2,
                "DecisionID",
                "D100"
            )

            SetText(
                decisions,
                2,
                "SubmissionID",
                "S100"
            )

            SetDate(
                decisions,
                2,
                "DecisionDate",
                New DateTime(
                    2026,
                    2,
                    1
                )
            )

            SetText(
                decisions,
                2,
                "Decision",
                "Rejected"
            )

            SetText(
                decisions,
                2,
                "Notes",
                "Rejected from Journal A."
            )


            SetText(
                decisions,
                3,
                "DecisionID",
                "D101"
            )

            SetText(
                decisions,
                3,
                "SubmissionID",
                "S101"
            )

            SetDate(
                decisions,
                3,
                "DecisionDate",
                New DateTime(
                    2026,
                    4,
                    1
                )
            )

            SetText(
                decisions,
                3,
                "Decision",
                "Major Revision"
            )

            SetDate(
                decisions,
                3,
                "RevisionDeadline",
                New DateTime(
                    2026,
                    5,
                    15
                )
            )

            SetText(
                decisions,
                3,
                "Notes",
                "Major revision from Journal B."
            )


            Dim correspondence As IXLWorksheet =
                workbook.Worksheet(
                    "Correspondence"
                )

            SetText(
                correspondence,
                2,
                "CorrespondenceID",
                "C100"
            )

            SetText(
                correspondence,
                2,
                "SubmissionID",
                "S100"
            )

            SetDate(
                correspondence,
                2,
                "Date",
                New DateTime(
                    2026,
                    2,
                    1
                )
            )

            SetText(
                correspondence,
                2,
                "Type",
                "Decision Letter"
            )

            SetText(
                correspondence,
                2,
                "Title",
                "Journal A rejection letter"
            )


            SetText(
                correspondence,
                3,
                "CorrespondenceID",
                "C101"
            )

            SetText(
                correspondence,
                3,
                "SubmissionID",
                "S101"
            )

            SetDate(
                correspondence,
                3,
                "Date",
                New DateTime(
                    2026,
                    4,
                    1
                )
            )

            SetText(
                correspondence,
                3,
                "Type",
                "Reviewer Comments"
            )

            SetText(
                correspondence,
                3,
                "Title",
                "Journal B reviewer comments"
            )

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
            2,
            result.SubmissionCount
        )

        Assert.AreEqual(
            2,
            result.DecisionCount
        )


        Dim manuscript As Manuscript =
            result.Manuscripts.Single()

        Assert.AreEqual(
            "Complex Route Study",
            manuscript.Title
        )

        Assert.AreEqual(
            2,
            manuscript.Submissions.Count
        )


        Dim journalA As JournalSubmission =
            manuscript.Submissions.Single(
                Function(item)
                    Return String.Equals(
                        item.JournalName,
                        "Journal A",
                        StringComparison.Ordinal
                    )
                End Function
            )

        Dim journalB As JournalSubmission =
            manuscript.Submissions.Single(
                Function(item)
                    Return String.Equals(
                        item.JournalName,
                        "Journal B",
                        StringComparison.Ordinal
                    )
                End Function
            )


        Assert.AreEqual(
            1,
            journalA.Decisions.Count
        )

        Assert.AreEqual(
            EditorialDecision.Rejected,
            journalA.Decisions(0).Decision
        )

        Assert.AreEqual(
            1,
            journalA.Correspondence.Count
        )

        Assert.AreEqual(
            "Journal A rejection letter",
            journalA.Correspondence(0).Title
        )


        Assert.AreEqual(
            1,
            journalB.Decisions.Count
        )

        Assert.AreEqual(
            EditorialDecision.MajorRevision,
            journalB.Decisions(0).Decision
        )

        Assert.AreEqual(
            New DateTime(
                2026,
                5,
                15
            ),
            journalB.Decisions(0).RevisionDeadline.Value
        )

        Assert.AreEqual(
            1,
            journalB.Correspondence.Count
        )

        Assert.AreEqual(
            CorrespondenceType.ReviewerComments,
            journalB.Correspondence(0).Type
        )

    End Sub


    ' =====================================================
    ' Standard importer:
    ' missing managed source degrades to link
    ' =====================================================

    <TestMethod>
    Public Sub StandardImporter_MissingManagedSourcePreservesRecordAsLink()

        Dim workbookPath As String =
            Path.Combine(
                _root,
                "missing-managed-source.xlsx"
            )

        Dim missingFile As String =
            Path.Combine(
                _root,
                "file-that-does-not-exist.pdf"
            )

        Dim generator As New StandardTemplateGenerator()

        generator.Generate(
            workbookPath
        )


        Using workbook As New XLWorkbook(
            workbookPath
        )

            Dim manuscripts As IXLWorksheet =
                workbook.Worksheet(
                    "Manuscripts"
                )

            SetText(
                manuscripts,
                2,
                "ManuscriptID",
                "M200"
            )

            SetText(
                manuscripts,
                2,
                "Title",
                "Missing File Study"
            )

            SetText(
                manuscripts,
                2,
                "CurrentStage",
                "Under Review"
            )

            SetText(
                manuscripts,
                2,
                "Location",
                "Pipeline"
            )


            Dim submissions As IXLWorksheet =
                workbook.Worksheet(
                    "Submissions"
                )

            SetText(
                submissions,
                2,
                "SubmissionID",
                "S200"
            )

            SetText(
                submissions,
                2,
                "ManuscriptID",
                "M200"
            )

            SetText(
                submissions,
                2,
                "Journal",
                "Journal of Missing Files"
            )

            SetDate(
                submissions,
                2,
                "SubmittedDate",
                New DateTime(
                    2026,
                    8,
                    1
                )
            )


            Dim correspondence As IXLWorksheet =
                workbook.Worksheet(
                    "Correspondence"
                )

            SetText(
                correspondence,
                2,
                "CorrespondenceID",
                "C200"
            )

            SetText(
                correspondence,
                2,
                "SubmissionID",
                "S200"
            )

            SetDate(
                correspondence,
                2,
                "Date",
                New DateTime(
                    2026,
                    8,
                    2
                )
            )

            SetText(
                correspondence,
                2,
                "Type",
                "Decision Letter"
            )

            SetText(
                correspondence,
                2,
                "Title",
                "Missing decision letter"
            )

            SetText(
                correspondence,
                2,
                "FilePath",
                missingFile
            )

            SetText(
                correspondence,
                2,
                "StorageMode",
                "ManagedCopy"
            )

            workbook.Save()

        End Using


        Dim result As ExcelImportResult =
            New StandardExcelImporter().Import(
                workbookPath
            )

        Dim item As CorrespondenceItem =
            result.Manuscripts(0).
                Submissions(0).
                Correspondence(0)


        Assert.AreEqual(
            missingFile,
            item.LocalFilePath
        )

        Assert.IsFalse(
            item.IsManagedCopy
        )

        Assert.IsTrue(
            result.Warnings.Any(
                Function(message)
                    Return message.IndexOf(
                        "managed-copy source file was not found",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                End Function
            )
        )

    End Sub


    ' =====================================================
    ' Repository:
    ' missing linked file remains valid metadata
    ' =====================================================

    <TestMethod>
    Public Sub Repository_MissingLinkedFilePreservesCorrespondenceRecord()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim missingFile As String =
            Path.Combine(
                _root,
                "missing-linked-file.pdf"
            )

        manuscripts(0).
            Submissions(0).
            Correspondence.Add(
                New CorrespondenceItem With {
                    .ItemDate =
                        New DateTime(
                            2026,
                            8,
                            19
                        ),
                    .Type =
                        CorrespondenceType.EditorEmail,
                    .Title =
                        "Missing external file",
                    .Notes =
                        "Metadata must survive even if the external file disappears.",
                    .LocalFilePath =
                        missingFile,
                    .IsManagedCopy =
                        False
                }
            )


        repository.Save(
            manuscripts
        )

        Assert.IsFalse(
            File.Exists(
                missingFile
            )
        )


        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Dim item As CorrespondenceItem =
            loaded(0).
                Submissions(0).
                Correspondence.Single()


        Assert.AreEqual(
            "Missing external file",
            item.Title
        )

        Assert.AreEqual(
            missingFile,
            item.LocalFilePath
        )

        Assert.IsFalse(
            item.IsManagedCopy
        )

        Assert.IsFalse(
            File.Exists(
                item.LocalFilePath
            )
        )

    End Sub


    ' =====================================================
    ' Managed library:
    ' failed batch copy rolls back created files
    ' =====================================================

    <TestMethod>
    Public Sub ManagedLibrary_BatchFailureRollsBackCreatedCopies()

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed-library"
            )

        Dim sourceA As String =
            Path.Combine(
                _root,
                "source-a.txt"
            )

        Dim sourceB As String =
            Path.Combine(
                _root,
                "source-b.txt"
            )

        File.WriteAllText(
            sourceA,
            "Source A"
        )

        File.WriteAllText(
            sourceB,
            "Source B"
        )


        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim manuscript As Manuscript =
            manuscripts(0)

        Dim submission As JournalSubmission =
            manuscript.Submissions(0)


        Dim itemA As New CorrespondenceItem With {
            .ItemDate =
                New DateTime(
                    2026,
                    8,
                    18
                ),
            .Type =
                CorrespondenceType.DecisionLetter,
            .Title =
                "Copy A",
            .LocalFilePath =
                sourceA,
            .IsManagedCopy =
                True
        }

        Dim itemB As New CorrespondenceItem With {
            .ItemDate =
                New DateTime(
                    2026,
                    8,
                    19
                ),
            .Type =
                CorrespondenceType.ReviewerComments,
            .Title =
                "Copy B",
            .LocalFilePath =
                sourceB,
            .IsManagedCopy =
                True
        }


        submission.Correspondence.Add(
            itemA
        )

        submission.Correspondence.Add(
            itemB
        )


        Dim submissionDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscript.Id.ToString(
                    "N"
                ),
                submission.Id.ToString(
                    "N"
                )
            )

        Directory.CreateDirectory(
            submissionDirectory
        )


        ' Deliberately create a FILE where item B's destination
        ' DIRECTORY is supposed to be. This forces the second
        ' managed-copy operation to fail after item A has copied.
        Dim blockedDestination As String =
            Path.Combine(
                submissionDirectory,
                itemB.Id.ToString(
                    "N"
                )
            )

        File.WriteAllText(
            blockedDestination,
            "This file deliberately blocks Directory.CreateDirectory."
        )


        Dim service As New ManagedLibraryService(
            managedDirectory
        )

        Dim threw As Boolean =
            False

        Try

            service.CommitManagedCopies(
                manuscripts
            )

        Catch ex As IOException

            threw =
                True

        End Try


        Assert.IsTrue(
            threw,
            "The deliberately blocked managed-copy operation should fail."
        )


        ' Source files must never be harmed.
        Assert.IsTrue(
            File.Exists(
                sourceA
            )
        )

        Assert.IsTrue(
            File.Exists(
                sourceB
            )
        )


        ' In-memory paths must not be rewritten on a failed batch.
        Assert.AreEqual(
            sourceA,
            itemA.LocalFilePath
        )

        Assert.AreEqual(
            sourceB,
            itemB.LocalFilePath
        )


        Dim itemADestinationDirectory As String =
            Path.Combine(
                submissionDirectory,
                itemA.Id.ToString(
                    "N"
                )
            )

        If Directory.Exists(
            itemADestinationDirectory
        ) Then

            Assert.AreEqual(
                0,
                Directory.EnumerateFiles(
                    itemADestinationDirectory
                ).Count()
            )

        End If

    End Sub


    ' =====================================================
    ' Workbook helpers
    ' =====================================================

    Private Sub SetText(
        worksheet As IXLWorksheet,
        rowNumber As Integer,
        headerName As String,
        value As String
    )

        worksheet.Cell(
            rowNumber,
            FindColumn(
                worksheet,
                headerName
            )
        ).Value =
            value

    End Sub


    Private Sub SetDate(
        worksheet As IXLWorksheet,
        rowNumber As Integer,
        headerName As String,
        value As DateTime
    )

        worksheet.Cell(
            rowNumber,
            FindColumn(
                worksheet,
                headerName
            )
        ).Value =
            value

    End Sub


    Private Function FindColumn(
        worksheet As IXLWorksheet,
        headerName As String
    ) As Integer

        Dim lastCell As IXLCell =
            worksheet.Row(1).
                LastCellUsed()

        If lastCell Is Nothing Then

            Throw New InvalidOperationException(
                "Worksheet '" &
                worksheet.Name &
                "' has no header row."
            )

        End If


        Dim wanted As String =
            NormalizeHeader(
                headerName
            )

        For columnNumber As Integer =
            1 To lastCell.Address.ColumnNumber

            Dim candidate As String =
                NormalizeHeader(
                    worksheet.Cell(
                        1,
                        columnNumber
                    ).
                    GetFormattedString()
                )

            If String.Equals(
                candidate,
                wanted,
                StringComparison.OrdinalIgnoreCase
            ) Then

                Return columnNumber

            End If

        Next


        Throw New InvalidOperationException(
            "Worksheet '" &
            worksheet.Name &
            "' does not contain the expected '" &
            headerName &
            "' column."
        )

    End Function


    Private Function NormalizeHeader(
        value As String
    ) As String

        If value Is Nothing Then
            Return String.Empty
        End If

        Dim normalized As String =
            value.Trim()

        If normalized.EndsWith(
            "*",
            StringComparison.Ordinal
        ) Then

            normalized =
                normalized.Substring(
                    0,
                    normalized.Length - 1
                )

        End If

        normalized =
            normalized.Replace(
                " ",
                String.Empty
            )

        normalized =
            normalized.Replace(
                "_",
                String.Empty
            )

        Return normalized.ToUpperInvariant()

    End Function

End Class