Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class BibTeXServiceTests

    <TestMethod>
    Public Sub Parse_Article_MapsCommonMetadata()
        Dim text As String =
            "@article{smith2026," &
            "author={Smith, Jane and Doe, John}," &
            "title={A Useful Paper}," &
            "journal={Journal of Examples}," &
            "year={2026}," &
            "month={8}," &
            "volume={12}," &
            "number={3}," &
            "pages={10--22}," &
            "doi={10.1234/example}" &
            "}"

        Dim result As BibliographyParseResult =
            BibTeXService.Parse(text)

        Assert.AreEqual(1, result.Records.Count)

        Dim record As BibliographyRecord = result.Records(0)

        Assert.AreEqual("A Useful Paper", record.Title)
        Assert.AreEqual("Journal of Examples", record.Journal)
        Assert.AreEqual("10.1234/example", record.Doi)
        Assert.AreEqual(2, record.Authors.Count)
        Assert.AreEqual(2026, record.PublishedDate.Value.Year)
        Assert.AreEqual(8, record.PublishedDate.Value.Month)
    End Sub

    <TestMethod>
    Public Sub Parse_MultilineBracedTitle_PreservesText()
        Dim text As String =
            "@article{x," & Environment.NewLine &
            " title = {A Title with {Nested} Braces}," & Environment.NewLine &
            " year = {2025}" & Environment.NewLine &
            "}"

        Dim result As BibliographyParseResult =
            BibTeXService.Parse(text)

        Assert.AreEqual(
            "A Title with {Nested} Braces",
            result.Records(0).Title
        )
    End Sub

    <TestMethod>
    Public Sub Parse_MultipleEntries_ReturnsAllRecords()
        Dim text As String =
            "@article{a,title={One},year={2024}}" &
            Environment.NewLine &
            "@misc{b,title={Two},year={2025}}"

        Dim result As BibliographyParseResult =
            BibTeXService.Parse(text)

        Assert.AreEqual(2, result.Records.Count)
    End Sub

    <TestMethod>
    Public Sub Parse_UnsupportedField_ProducesWarning()
        Dim result As BibliographyParseResult =
            BibTeXService.Parse(
                "@article{x,title={Paper},isbn={12345}}"
            )

        Assert.IsTrue(
            result.Records(0).Warnings.Exists(
                Function(item)
                    Return item.Contains(
                        "isbn",
                        StringComparison.OrdinalIgnoreCase
                    )
                End Function
            )
        )
    End Sub

    <TestMethod>
    Public Sub Export_CommonArticle_WritesExpectedFields()
        Dim author As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim manuscript As New Manuscript With {
            .Title = "Exported Paper",
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Published
        }

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = author.Id
            }
        )

        manuscript.Metadata.PublicationJournal = "Example Journal"
        manuscript.Metadata.PublishedDate = New DateTime(2026, 4, 1)
        manuscript.Metadata.Doi = "10.1234/export"

        Dim output As String =
            BibTeXService.Export(
                New List(Of Manuscript) From {manuscript},
                library
            )

        StringAssert.Contains(output, "@article{")
        StringAssert.Contains(output, "author = {Smith, Jane}")
        StringAssert.Contains(output, "doi = {10.1234/export}")
    End Sub

    <TestMethod>
    Public Sub ExportThenParse_RoundTripsCoreArticleMetadata()
        Dim manuscript As New Manuscript With {
            .Title = "Round Trip"
        }

        manuscript.Metadata.PublicationJournal = "Journal"
        manuscript.Metadata.PublishedDate = New DateTime(2022, 1, 1)
        manuscript.Metadata.Volume = "4"
        manuscript.Metadata.Issue = "2"
        manuscript.Metadata.Pages = "10-20"
        manuscript.Metadata.Doi = "10.1000/roundtrip"

        Dim text As String =
            BibTeXService.Export(
                New List(Of Manuscript) From {manuscript},
                New AuthorLibraryData()
            )

        Dim parsed As BibliographyParseResult =
            BibTeXService.Parse(text)

        Assert.AreEqual("Round Trip", parsed.Records(0).Title)
        Assert.AreEqual("Journal", parsed.Records(0).Journal)
        Assert.AreEqual("4", parsed.Records(0).Volume)
        Assert.AreEqual("2", parsed.Records(0).Issue)
        Assert.AreEqual("10.1000/roundtrip", parsed.Records(0).Doi)
    End Sub

End Class
