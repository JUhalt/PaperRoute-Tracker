Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class BibliographyExchangeServiceTests

    Private Function MakeRecord() As BibliographyRecord
        Dim record As New BibliographyRecord With {
            .SourceFormat = BibliographyFormat.BibTeX,
            .SourceType = "article",
            .SourceKey = "smith2026",
            .Title = "Imported Paper",
            .Journal = "Example Journal",
            .PublishedDate = New DateTime(2026, 1, 1),
            .Doi = "10.1234/imported"
        }

        record.Authors.Add(
            New BibliographyAuthor With {
                .GivenName = "Jane",
                .FamilyName = "Smith"
            }
        )

        record.Authors.Add(
            New BibliographyAuthor With {
                .GivenName = "John",
                .FamilyName = "Doe"
            }
        )

        Return record
    End Function

    <TestMethod>
    Public Sub DetectFormat_UsesExtensionAndContent()
        Assert.AreEqual(
            BibliographyFormat.BibTeX,
            BibliographyExchangeService.DetectFormat(
                "paper.bib",
                "anything"
            )
        )

        Assert.AreEqual(
            BibliographyFormat.Ris,
            BibliographyExchangeService.DetectFormat(
                "paper.txt",
                "TY  - JOUR"
            )
        )
    End Sub

    <TestMethod>
    Public Sub Apply_CreatesStructuredAuthorsInSourceOrder()
        Dim manuscripts As New List(Of Manuscript)()
        Dim library As New AuthorLibraryData()

        Dim result As BibliographyImportResult =
            BibliographyExchangeService.Apply(
                New List(Of BibliographyRecord) From {MakeRecord()},
                manuscripts,
                library,
                New BibliographyImportOptions()
            )

        Assert.AreEqual(1, result.ImportedCount)
        Assert.AreEqual(2, library.Authors.Count)
        Assert.AreEqual(2, manuscripts(0).Authors.Count)

        Assert.AreEqual(
            library.Authors(0).Id,
            manuscripts(0).Authors(0).AuthorId
        )

        Assert.AreEqual(
            library.Authors(1).Id,
            manuscripts(0).Authors(1).AuthorId
        )
    End Sub

    <TestMethod>
    Public Sub Apply_ReusesExistingAuthorByDisplayName()
        Dim existing As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(existing)

        Dim manuscripts As New List(Of Manuscript)()

        BibliographyExchangeService.Apply(
            New List(Of BibliographyRecord) From {MakeRecord()},
            manuscripts,
            library,
            New BibliographyImportOptions()
        )

        Assert.AreEqual(2, library.Authors.Count)

        Assert.AreEqual(
            existing.Id,
            manuscripts(0).Authors(0).AuthorId
        )
    End Sub

    <TestMethod>
    Public Sub Apply_DuplicateDoi_IsSkipped()
        Dim existing As New Manuscript With {
            .Title = "Different title"
        }

        existing.Metadata.Doi =
            "https://doi.org/10.1234/imported"

        Dim manuscripts As New List(Of Manuscript) From {existing}

        Dim result As BibliographyImportResult =
            BibliographyExchangeService.Apply(
                New List(Of BibliographyRecord) From {MakeRecord()},
                manuscripts,
                New AuthorLibraryData(),
                New BibliographyImportOptions()
            )

        Assert.AreEqual(0, result.ImportedCount)
        Assert.AreEqual(1, result.DuplicateCount)
        Assert.AreEqual(1, manuscripts.Count)
    End Sub

    <TestMethod>
    Public Sub Apply_DuplicateTitle_IsSkippedWhenNoDoi()
        Dim record As BibliographyRecord = MakeRecord()
        record.Doi = String.Empty

        Dim existing As New Manuscript With {
            .Title = "  Imported   Paper "
        }

        Dim manuscripts As New List(Of Manuscript) From {existing}

        Dim result As BibliographyImportResult =
            BibliographyExchangeService.Apply(
                New List(Of BibliographyRecord) From {record},
                manuscripts,
                New AuthorLibraryData(),
                New BibliographyImportOptions()
            )

        Assert.AreEqual(0, result.ImportedCount)
        Assert.AreEqual(1, result.DuplicateCount)
    End Sub

    <TestMethod>
    Public Sub Apply_PublishedOption_ImportsPublishedRecordToPublishedShelf()
        Dim manuscripts As New List(Of Manuscript)()

        BibliographyExchangeService.Apply(
            New List(Of BibliographyRecord) From {MakeRecord()},
            manuscripts,
            New AuthorLibraryData(),
            New BibliographyImportOptions With {
                .ImportPublishedRecordsAsPublished = True
            }
        )

        Assert.AreEqual(
            PaperStage.Published,
            manuscripts(0).CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Published,
            manuscripts(0).Location
        )

        Assert.AreEqual(
            0,
            manuscripts(0).Submissions.Count
        )

        Assert.AreEqual(
            String.Empty,
            manuscripts(0).TargetJournal
        )
    End Sub

    <TestMethod>
    Public Sub Apply_PublishedOptionOff_LeavesRecordAsIdea()
        Dim manuscripts As New List(Of Manuscript)()

        BibliographyExchangeService.Apply(
            New List(Of BibliographyRecord) From {MakeRecord()},
            manuscripts,
            New AuthorLibraryData(),
            New BibliographyImportOptions With {
                .ImportPublishedRecordsAsPublished = False
            }
        )

        Assert.AreEqual(
            PaperStage.Idea,
            manuscripts(0).CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscripts(0).Location
        )
    End Sub

End Class
