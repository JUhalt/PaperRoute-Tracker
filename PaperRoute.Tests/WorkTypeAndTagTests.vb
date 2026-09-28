Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Work types and tags (#64): set by the user or stated by a source, never
' inferred; kept through save, clone, backup, and the workbook.
<TestClass>
Public Class WorkTypeAndTagTests

    <TestMethod>
    Public Sub TagsAreTrimmedAndKeptOncePerManuscript()
        Assert.AreEqual("grant report", WorkTypeService.NormalizeTag("  #grant    report "))
        Assert.AreEqual(WorkTypeService.MaximumTagLength, WorkTypeService.NormalizeTag(New String("x"c, 80)).Length)

        Dim manuscript As New Manuscript()
        Assert.IsTrue(WorkTypeService.AddTag(manuscript, "Dissertation"))
        Assert.IsFalse(WorkTypeService.AddTag(manuscript, " dissertation "), "Tags are unique in any capitalization.")
        Assert.IsFalse(WorkTypeService.AddTag(manuscript, "   "))
        CollectionAssert.AreEqual({"Dissertation"}, manuscript.Tags)

        Dim other As New Manuscript With {.Tags = New List(Of String) From {"grant", "Dissertation"}}
        Dim third As New Manuscript With {.Tags = New List(Of String) From {"GRANT"}}
        Dim fourth As New Manuscript With {.Tags = New List(Of String) From {"Grant"}}
        CollectionAssert.AreEqual({"grant", "Dissertation"}, WorkTypeService.AllTags({manuscript, other, third, fourth}), "Most used first, in the first spelling used.")
    End Sub

    <TestMethod>
    Public Sub StoredTagsAreNormalizedAndUnknownTypesRejected()
        Dim manuscript As New Manuscript With {.Tags = New List(Of String) From {" a ", "A", "", Nothing, "b"}}
        WorkTypeService.NormalizeAndValidateManuscript(manuscript)
        CollectionAssert.AreEqual({"a", "b"}, manuscript.Tags)

        manuscript.Tags = Nothing
        WorkTypeService.NormalizeAndValidateManuscript(manuscript)
        Assert.AreEqual(0, manuscript.Tags.Count)

        manuscript.WorkType = CType(42, WorkType)
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() WorkTypeService.NormalizeAndValidateManuscript(manuscript))
    End Sub

    <TestMethod>
    Public Sub TagColorsComeFromTheNameUnlessChosen()
        Dim library As New AuthorLibraryData()
        Dim automatic As TagPalette = WorkTypeService.ColorOf("grant", library)
        Assert.AreEqual(automatic, WorkTypeService.ColorOf(" Grant ", library), "The same tag always has the same color.")

        Dim chosen As TagPalette = If(automatic = TagPalette.Rose, TagPalette.Blue, TagPalette.Rose)
        WorkTypeService.SetColor(library, "GRANT", chosen)
        WorkTypeService.SetColor(library, "grant", chosen)
        Assert.AreEqual(chosen, WorkTypeService.ColorOf("grant", library))
        Assert.AreEqual(1, library.TagColors.Count, "One color per tag.")

        library.TagColors.Add(Nothing)
        library.TagColors.Add(New TagColor With {.Tag = "  ", .Color = TagPalette.Amber})
        WorkTypeService.NormalizeTagColors(library)
        Assert.AreEqual(1, library.TagColors.Count)
    End Sub

    <TestMethod>
    Public Sub EveryTagColorIsLegibleInBothThemes()
        For Each palette As TagPalette In [Enum].GetValues(GetType(TagPalette))
            For Each dark In {False, True}
                Dim ratio As Double = Contrast(WorkTypeService.Foreground(palette, dark), WorkTypeService.Background(palette, dark))
                Assert.IsTrue(ratio >= 4.5, palette.ToString() & If(dark, " dark ", " light ") & ratio.ToString("0.00"))
            Next
        Next
    End Sub

    <TestMethod>
    <DataRow("article", WorkType.JournalArticle)>
    <DataRow("InProceedings", WorkType.ConferencePaper)>
    <DataRow("incollection", WorkType.BookChapter)>
    <DataRow("phdthesis", WorkType.Thesis)>
    <DataRow("misc", WorkType.Unspecified)>
    <DataRow("techreport", WorkType.Other)>
    Public Sub BibTeXTypesMap(entryType As String, expected As WorkType)
        Assert.AreEqual(expected, WorkTypeService.FromBibTeX(entryType))
    End Sub

    <TestMethod>
    <DataRow("JOUR", WorkType.JournalArticle)>
    <DataRow("CPAPER", WorkType.ConferencePaper)>
    <DataRow("ABST", WorkType.ConferenceAbstract)>
    <DataRow("CHAP", WorkType.BookChapter)>
    <DataRow("THES", WorkType.Thesis)>
    <DataRow("GEN", WorkType.Unspecified)>
    Public Sub RisTypesMap(ty As String, expected As WorkType)
        Assert.AreEqual(expected, WorkTypeService.FromRis(ty))
    End Sub

    <TestMethod>
    <DataRow("journal-article", WorkType.JournalArticle)>
    <DataRow("posted-content", WorkType.Preprint)>
    <DataRow("PREPRINT", WorkType.Preprint)>
    <DataRow("proceedings-article", WorkType.ConferencePaper)>
    <DataRow("conference_poster", WorkType.Poster)>
    <DataRow("dissertation-thesis", WorkType.Thesis)>
    <DataRow("", WorkType.Unspecified)>
    <DataRow("dataset", WorkType.Other)>
    Public Sub OrcidAndCrossrefTypesMap(type As String, expected As WorkType)
        Assert.AreEqual(expected, WorkTypeService.FromRegistry(type))
    End Sub

    <TestMethod>
    Public Sub TypesParseAndExportWithTheirFormerFallbacks()
        Assert.AreEqual(WorkType.JournalArticle, WorkTypeService.Parse("Journal article"))
        Assert.AreEqual(WorkType.Thesis, WorkTypeService.Parse("thesis or dissertation"))
        Assert.AreEqual(WorkType.ConferencePaper, WorkTypeService.Parse("conference_paper"))
        Assert.AreEqual(WorkType.Unspecified, WorkTypeService.Parse("a novel"))
        Assert.AreEqual("inproceedings", WorkTypeService.ToBibTeX(WorkType.ConferencePaper, "misc"))
        Assert.AreEqual("misc", WorkTypeService.ToBibTeX(WorkType.Unspecified, "misc"), "Without a type, export is unchanged.")
        Assert.AreEqual("CHAP", WorkTypeService.ToRis(WorkType.BookChapter, "GEN"))
        Assert.AreEqual("JOUR", WorkTypeService.ToRis(WorkType.Unspecified, "JOUR"))
    End Sub

    <TestMethod>
    Public Sub ImportsSetTheTypeTheSourceStates()
        Dim bib As BibliographyParseResult = BibTeXService.Parse("@inproceedings{key1, title={A conference talk}, author={Doe, Jane}, year={2025}}")
        Dim library As New List(Of Manuscript)
        BibliographyExchangeService.Apply(bib.Records, library, New AuthorLibraryData(), New BibliographyImportOptions())
        Assert.AreEqual(WorkType.ConferencePaper, library.Single().WorkType)

        Dim ris As BibliographyParseResult = RisService.Parse("TY  - CHAP" & vbCrLf & "TI  - A chapter" & vbCrLf & "ER  - " & vbCrLf)
        BibliographyExchangeService.Apply(ris.Records, library, New AuthorLibraryData(), New BibliographyImportOptions())
        Assert.AreEqual(WorkType.BookChapter, library.Last().WorkType)

        Dim enriched As New Manuscript With {.Title = "Enriched"}
        CrossrefApplyService.Apply(New CrossrefMetadataSuggestion With {.Doi = "10.5555/x", .WorkType = "journal-article"}, enriched, New AuthorLibraryData(), New CrossrefApplyOptions())
        Assert.AreEqual(WorkType.JournalArticle, enriched.WorkType)
        Dim chosen As New Manuscript With {.Title = "Chosen", .WorkType = WorkType.Preprint}
        CrossrefApplyService.Apply(New CrossrefMetadataSuggestion With {.Doi = "10.5555/y", .WorkType = "journal-article"}, chosen, New AuthorLibraryData(), New CrossrefApplyOptions())
        Assert.AreEqual(WorkType.Preprint, chosen.WorkType, "Crossref never replaces a type the user chose.")
    End Sub

    <TestMethod>
    Public Sub TypesAndTagsSurviveCloneSaveAndTheWorkbook()
        Dim root As String = CreateTemporaryRoot()
        Try
            Dim manuscript As New Manuscript With {.Title = "Typed and tagged", .WorkType = WorkType.Poster, .Tags = New List(Of String) From {"SIPS 2026", "teaching"}}

            Dim clone As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
            Assert.AreEqual(WorkType.Poster, clone.WorkType)
            CollectionAssert.AreEqual(manuscript.Tags, clone.Tags)
            Assert.AreNotSame(manuscript.Tags, clone.Tags)

            Dim repository As New ManuscriptRepository(Path.Combine(root, "data"), Path.Combine(root, "library"))
            repository.Save(New List(Of Manuscript) From {manuscript})
            Dim loaded As Manuscript = repository.Load().Single()
            Assert.AreEqual(WorkType.Poster, loaded.WorkType)
            CollectionAssert.AreEqual({"SIPS 2026", "teaching"}, loaded.Tags)

            Dim authors As New AuthorLibraryRepository(Path.Combine(root, "data"))
            Dim tagLibrary As New AuthorLibraryData()
            WorkTypeService.SetColor(tagLibrary, "teaching", TagPalette.Violet)
            authors.Save(tagLibrary)
            Assert.AreEqual(TagPalette.Violet, WorkTypeService.ColorOf("teaching", authors.Load()))

            Dim workbook As String = Path.Combine(root, "library.xlsx")
            Call New LibraryExcelExporter().Export(workbook, {manuscript})
            Dim imported As Manuscript = New StandardExcelImporter().Import(workbook).Manuscripts.Single()
            Assert.AreEqual(WorkType.Poster, imported.WorkType)
            CollectionAssert.AreEqual({"SIPS 2026", "teaching"}, imported.Tags)
        Finally
            DeleteTemporaryRoot(root)
        End Try
    End Sub

    <TestMethod>
    Public Sub CvExportCanGroupByType()
        Dim library As New List(Of Manuscript) From {
            New Manuscript With {.Title = "A poster", .WorkType = WorkType.Poster},
            New Manuscript With {.Title = "An article", .WorkType = WorkType.JournalArticle},
            New Manuscript With {.Title = "Untyped work"}
        }
        Dim markdown As String = PublicationExportService.Export(library, New AuthorLibraryData(), PublicationExportFormat.Markdown, PublicationExportStyle.CvSection, groupByType:=True)

        Dim articles As Integer = markdown.IndexOf("### Journal articles", StringComparison.Ordinal)
        Dim posters As Integer = markdown.IndexOf("### Posters", StringComparison.Ordinal)
        Dim other As Integer = markdown.IndexOf("### Other work", StringComparison.Ordinal)
        Assert.IsTrue(articles > 0 AndAlso articles < posters AndAlso posters < other, markdown)
        Assert.IsTrue(markdown.IndexOf("An article", StringComparison.Ordinal) < posters)
        Assert.IsTrue(markdown.IndexOf("Untyped work", StringComparison.Ordinal) > other)
        Assert.IsFalse(PublicationExportService.Export(library, New AuthorLibraryData(), PublicationExportFormat.Markdown, PublicationExportStyle.CvSection).Contains("###"),
            "Ungrouped export is unchanged.")
    End Sub

    Private Shared Function Contrast(foreground As Color, background As Color) As Double
        Dim luminance As Func(Of Color, Double) =
            Function(color)
                Dim channel As Func(Of Integer, Double) =
                    Function(value)
                        Dim c As Double = value / 255.0
                        Return If(c <= 0.03928, c / 12.92, Math.Pow((c + 0.055) / 1.055, 2.4))
                    End Function
                Return 0.2126 * channel(color.R) + 0.7152 * channel(color.G) + 0.0722 * channel(color.B)
            End Function
        Dim a As Double = luminance(foreground)
        Dim b As Double = luminance(background)
        Return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05)
    End Function

End Class
