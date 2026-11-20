Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class RisServiceTests

    <TestMethod>
    Public Sub Parse_JournalRecord_MapsCommonMetadata()
        Dim text As String =
            "TY  - JOUR" & Environment.NewLine &
            "TI  - A RIS Paper" & Environment.NewLine &
            "AU  - Smith, Jane" & Environment.NewLine &
            "AU  - Doe, John" & Environment.NewLine &
            "JO  - Example Journal" & Environment.NewLine &
            "DA  - 2026/08/21" & Environment.NewLine &
            "VL  - 8" & Environment.NewLine &
            "IS  - 2" & Environment.NewLine &
            "SP  - 11" & Environment.NewLine &
            "EP  - 19" & Environment.NewLine &
            "DO  - 10.1234/ris" & Environment.NewLine &
            "ER  - " & Environment.NewLine

        Dim result As BibliographyParseResult =
            RisService.Parse(text)

        Assert.AreEqual(1, result.Records.Count)

        Dim record As BibliographyRecord = result.Records(0)

        Assert.AreEqual("A RIS Paper", record.Title)
        Assert.AreEqual("Example Journal", record.Journal)
        Assert.AreEqual(2, record.Authors.Count)
        Assert.AreEqual("11-19", record.Pages)
        Assert.AreEqual("10.1234/ris", record.Doi)
        Assert.AreEqual(New DateTime(2026, 8, 21), record.PublishedDate.Value)
    End Sub

    <TestMethod>
    Public Sub Parse_AdTag_ProducesAmbiguityWarning()
        Dim text As String =
            "TY  - JOUR" & Environment.NewLine &
            "TI  - Paper" & Environment.NewLine &
            "AU  - Smith, Jane" & Environment.NewLine &
            "AD  - Example University" & Environment.NewLine &
            "ER  - " & Environment.NewLine

        Dim result As BibliographyParseResult =
            RisService.Parse(text)

        Assert.IsTrue(
            result.Records(0).Warnings.Exists(
                Function(item)
                    Return item.Contains(
                        "affiliation",
                        StringComparison.OrdinalIgnoreCase
                    )
                End Function
            )
        )
    End Sub

    <TestMethod>
    Public Sub Parse_MissingFinalEr_ClosesRecordWithWarning()
        Dim text As String =
            "TY  - JOUR" & Environment.NewLine &
            "TI  - Paper"

        Dim result As BibliographyParseResult =
            RisService.Parse(text)

        Assert.AreEqual(1, result.Records.Count)
        Assert.IsTrue(result.FileWarnings.Count > 0)
    End Sub

    <TestMethod>
    Public Sub Export_CommonArticle_WritesExpectedTags()
        Dim author As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim manuscript As New Manuscript With {
            .Title = "RIS Export"
        }

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = author.Id
            }
        )

        manuscript.Metadata.PublicationJournal = "Example Journal"
        manuscript.Metadata.PublishedDate = New DateTime(2026, 8, 21)
        manuscript.Metadata.Doi = "10.1234/export-ris"

        Dim output As String =
            RisService.Export(
                New List(Of Manuscript) From {manuscript},
                library
            )

        StringAssert.Contains(output, "TY  - JOUR")
        StringAssert.Contains(output, "AU  - Smith, Jane")
        StringAssert.Contains(output, "DO  - 10.1234/export-ris")
        StringAssert.Contains(output, "ER  - ")
    End Sub

    <TestMethod>
    Public Sub ExportThenParse_RoundTripsCoreArticleMetadata()
        Dim manuscript As New Manuscript With {
            .Title = "RIS Round Trip"
        }

        manuscript.Metadata.PublicationJournal = "Journal"
        manuscript.Metadata.PublishedDate = New DateTime(2021, 5, 4)
        manuscript.Metadata.Volume = "9"
        manuscript.Metadata.Issue = "1"
        manuscript.Metadata.Pages = "33-44"
        manuscript.Metadata.Doi = "10.1000/risround"

        Dim text As String =
            RisService.Export(
                New List(Of Manuscript) From {manuscript},
                New AuthorLibraryData()
            )

        Dim parsed As BibliographyParseResult =
            RisService.Parse(text)

        Assert.AreEqual("RIS Round Trip", parsed.Records(0).Title)
        Assert.AreEqual("Journal", parsed.Records(0).Journal)
        Assert.AreEqual("9", parsed.Records(0).Volume)
        Assert.AreEqual("1", parsed.Records(0).Issue)
        Assert.AreEqual("33-44", parsed.Records(0).Pages)
        Assert.AreEqual("10.1000/risround", parsed.Records(0).Doi)
    End Sub

End Class
