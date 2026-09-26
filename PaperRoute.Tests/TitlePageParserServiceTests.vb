Imports System
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class TitlePageParserServiceTests

    ' All names, institutions, and addresses below are fictional.
    Private Shared ReadOnly WordTitlePage As String = String.Join(vbLf, {
        "Running head: ANCHORING IN CLINICAL ESTIMATES",
        "",
        "A preregistered replication of anchoring effects in clinical risk estimates",
        "",
        "Dana Whitfield1, Rui Okafor1,2*, and Lena Marsh2",
        "",
        "1 Department of Psychology, Blue Mountain University",
        "2School of Nursing, Cascade State University",
        "* Corresponding author: Rui Okafor, rokafor@example.edu",
        "",
        "Abstract",
        "Anchoring is among the most replicated effects in judgment research,",
        "yet few studies test it with practicing clinicians.",
        "",
        "We ran a preregistered replication with 412 nurses and physicians.",
        "",
        "Keywords: anchoring; clinical judgment; replication."
    })

    Private Shared ReadOnly LatexTitlePage As String = String.Join(vbLf, {
        "\documentclass{article}",
        "\usepackage{authblk}",
        "\title{A preregistered replication of anchoring effects in clinical risk estimates}",
        "\author[1]{Dana Whitfield}",
        "\author[1,2]{Rui Okafor\thanks{Corresponding author. Email: rokafor@example.edu}}",
        "\author[2]{Lena Marsh}",
        "\affil[1]{Department of Psychology, Blue Mountain University}",
        "\affil[2]{School of Nursing, Cascade State University}",
        "\begin{document}",
        "\maketitle",
        "Running head: ANCHORING IN CLINICAL ESTIMATES",
        "\begin{abstract}",
        "Anchoring is among the most replicated effects in judgment research.",
        "",
        "We ran a preregistered replication with 412 nurses and physicians.",
        "\end{abstract}",
        "Keywords: anchoring, clinical judgment, replication",
        "% internal reminder that must not appear",
        "\end{document}"
    })

    Private Const Psychology As String = "Department of Psychology, Blue Mountain University"
    Private Const Nursing As String = "School of Nursing, Cascade State University"


    <TestMethod>
    Public Sub WordTitlePage_ProposesTitleAuthorsAffiliationsAbstractAndKeywords()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(WordTitlePage)

        Assert.AreEqual(TitlePageSourceFormat.PlainText, result.SourceFormat)
        Assert.AreEqual("A preregistered replication of anchoring effects in clinical risk estimates", result.Title)
        AssertAuthors(result)

        Assert.AreEqual(
            "Anchoring is among the most replicated effects in judgment research, yet few studies test it with practicing clinicians." &
            Environment.NewLine & Environment.NewLine &
            "We ran a preregistered replication with 412 nurses and physicians.",
            result.AbstractText)
        CollectionAssert.AreEqual({"anchoring", "clinical judgment", "replication"}, result.Keywords)

        CollectionAssert.AreEquivalent(
            {"Running head: ANCHORING IN CLINICAL ESTIMATES", "* Corresponding author: Rui Okafor, rokafor@example.edu"},
            result.UnplacedLines)
        Assert.AreEqual(0, result.Warnings.Count, String.Join("; ", result.Warnings))

    End Sub

    <TestMethod>
    Public Sub WordTitlePage_ReadsUnicodeSuperscriptMarkers()

        Dim text As String = String.Join(vbLf, {
            "A preregistered replication of anchoring effects in clinical risk estimates",
            "Dana Whitfield" & ChrW(&HB9) & ", Rui Okafor" & ChrW(&HB9) & "," & ChrW(&HB2) & ChrW(&H2217) & ", Lena Marsh" & ChrW(&HB2),
            ChrW(&HB9) & " " & Psychology,
            ChrW(&HB2) & " " & Nursing
        })

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(text)

        ' Without a correspondence line, the asterisk marks the corresponding author.
        AssertAuthors(result)
        Assert.AreEqual(0, result.UnplacedLines.Count, String.Join("; ", result.UnplacedLines))

    End Sub

    <TestMethod>
    Public Sub SingleAuthor_SharesOneUnmarkedAffiliation()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "Retrieval practice and far transfer in introductory statistics",
            "Lena Marsh",
            "Cascade State University"
        }))

        Assert.AreEqual("Retrieval practice and far transfer in introductory statistics", result.Title)
        Assert.AreEqual(1, result.Authors.Count)
        Assert.AreEqual("Marsh", result.Authors(0).Name.FamilyName)
        CollectionAssert.AreEqual({"Cascade State University"}, result.Authors(0).Affiliations)
        Assert.AreEqual(String.Empty, result.AbstractText)
        Assert.AreEqual(0, result.UnplacedLines.Count)
        Assert.AreEqual(0, result.Warnings.Count, String.Join("; ", result.Warnings))

    End Sub

    <TestMethod>
    Public Sub UnmatchedMarkers_AreReportedAndUnusedAffiliationsStayVisible()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "Measurement invariance of a short grit scale across four countries",
            "",
            "Dana Whitfield1 and Rui Okafor3",
            "",
            "1 " & Psychology,
            "2 " & Nursing
        }))

        Assert.AreEqual(2, result.Authors.Count)
        CollectionAssert.AreEqual({Psychology}, result.Authors(0).Affiliations)
        Assert.AreEqual(0, result.Authors(1).Affiliations.Count)
        Assert.IsTrue(result.Warnings.Any(Function(item) item.Contains("marker 3") AndAlso item.Contains("Okafor")))
        Assert.IsTrue(result.Warnings.Any(Function(item) item.Contains("Affiliation 2")))
        CollectionAssert.Contains(result.UnplacedLines, "2 " & Nursing)

    End Sub

    <TestMethod>
    Public Sub TitleCaseSubtitle_IsNotReadAsAnAuthor()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "Working Memory and Attention Control",
            "Evidence From Three Experiments",
            "Dana Whitfield and Lena Marsh"
        }))

        Assert.AreEqual("Working Memory and Attention Control Evidence From Three Experiments", result.Title)
        CollectionAssert.AreEqual({"Whitfield", "Marsh"}, result.Authors.Select(Function(author) author.Name.FamilyName).ToList())

    End Sub

    <TestMethod>
    Public Sub ApaCorrespondenceSentence_MarksTheNamedAuthor()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "Teaching open science to nursing students",
            "Lena Marsh and Rui Okafor",
            "Cascade State University",
            "Author Note",
            "Correspondence concerning this article should be addressed to Rui Okafor, Cascade State University."
        }))

        Assert.AreEqual(2, result.Authors.Count)
        Assert.IsFalse(result.Authors(0).IsCorrespondingAuthor)
        Assert.IsTrue(result.Authors(1).IsCorrespondingAuthor)
        Assert.IsTrue(result.Authors.All(Function(author) author.Affiliations.SequenceEqual({"Cascade State University"})))
        CollectionAssert.Contains(result.UnplacedLines, "Author Note")

    End Sub

    <TestMethod>
    Public Sub LatexAuthblk_ProposesTheSameFieldsAsWordText()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(LatexTitlePage)

        Assert.AreEqual(TitlePageSourceFormat.Latex, result.SourceFormat)
        Assert.AreEqual("A preregistered replication of anchoring effects in clinical risk estimates", result.Title)
        AssertAuthors(result)

        Assert.AreEqual(
            "Anchoring is among the most replicated effects in judgment research." &
            Environment.NewLine & Environment.NewLine &
            "We ran a preregistered replication with 412 nurses and physicians.",
            result.AbstractText)
        CollectionAssert.AreEqual({"anchoring", "clinical judgment", "replication"}, result.Keywords)

        CollectionAssert.AreEquivalent(
            {"Corresponding author. Email: rokafor@example.edu", "Running head: ANCHORING IN CLINICAL ESTIMATES"},
            result.UnplacedLines)
        Assert.IsFalse(result.UnplacedLines.Any(Function(line) line.Contains("internal reminder")))
        Assert.AreEqual(0, result.Warnings.Count, String.Join("; ", result.Warnings))

    End Sub

    <TestMethod>
    Public Sub LatexAndSeparatedAuthors_UseThanksForCorrespondence()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "\title{Teaching open science to nursing students: a mixed-methods evaluation}",
            "\author{Lena Marsh\thanks{Cascade State University} \and Dana Whitfield\thanks{Corresponding author.}}",
            "\begin{document}",
            "\maketitle",
            "\begin{abstract}Nursing students rarely meet open-science practices.\end{abstract}",
            "\keywords{open science \sep nursing education}",
            "\end{document}"
        }))

        Assert.AreEqual("Teaching open science to nursing students: a mixed-methods evaluation", result.Title)
        CollectionAssert.AreEqual({"Marsh", "Whitfield"}, result.Authors.Select(Function(author) author.Name.FamilyName).ToList())
        Assert.IsFalse(result.Authors(0).IsCorrespondingAuthor)
        Assert.IsTrue(result.Authors(1).IsCorrespondingAuthor)
        Assert.AreEqual("Nursing students rarely meet open-science practices.", result.AbstractText)
        CollectionAssert.AreEqual({"open science", "nursing education"}, result.Keywords)
        CollectionAssert.AreEquivalent({"Cascade State University", "Corresponding author."}, result.UnplacedLines)

    End Sub

    <TestMethod>
    Public Sub RevtexAffiliations_ApplyToAuthorsSinceThePreviousAffiliation()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "\documentclass[aps,prl]{revtex4-2}",
            "\title{Measurement invariance of a short grit scale across four countries}",
            "\author{Dana Whitfield}",
            "\affiliation{" & Psychology & "}",
            "\author{Rui Okafor}",
            "\author{Lena Marsh}",
            "\affiliation{" & Nursing & "}",
            "\email{lmarsh@example.edu}"
        }))

        CollectionAssert.AreEqual({Psychology}, result.Authors(0).Affiliations)
        CollectionAssert.AreEqual({Nursing}, result.Authors(1).Affiliations)
        CollectionAssert.AreEqual({Nursing}, result.Authors(2).Affiliations)
        CollectionAssert.Contains(result.UnplacedLines, "lmarsh@example.edu")
        Assert.AreEqual(0, result.Warnings.Count, String.Join("; ", result.Warnings))

    End Sub

    <TestMethod>
    Public Sub Apa7Commands_MapAuthorsAffiliationsAndAuthorNote()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "\documentclass[man]{apa7}",
            "\title{Retrieval practice and far transfer in introductory statistics}",
            "\shorttitle{Retrieval and Transfer}",
            "\authorsnames[1,{1,2},2]{Dana Whitfield, Rui Okafor, Lena Marsh}",
            "\authorsaffiliations{{" & Psychology & "}, {" & Nursing & "}}",
            "\authornote{Correspondence concerning this article should be addressed to Rui Okafor, " & Nursing & ".}",
            "\abstract{Retrieval practice improves retention, but its effect on far transfer is less clear.}",
            "\keywords{retrieval practice, transfer, statistics education}"
        }))

        Assert.AreEqual("Retrieval practice and far transfer in introductory statistics", result.Title)
        AssertAuthors(result)
        Assert.AreEqual("Retrieval practice improves retention, but its effect on far transfer is less clear.", result.AbstractText)
        CollectionAssert.AreEqual({"retrieval practice", "transfer", "statistics education"}, result.Keywords)
        CollectionAssert.Contains(result.UnplacedLines, "Running head: Retrieval and Transfer")
        Assert.AreEqual(0, result.Warnings.Count, String.Join("; ", result.Warnings))

    End Sub

    <TestMethod>
    Public Sub LatexAccentsAndFormatting_AreConvertedToPlainText()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse(String.Join(vbLf, {
            "\title{Caf\'{e} culture and \emph{self-reported} well-being --- a \textbf{registered} report}",
            "\author{Ren\'e Dupont \and Zo\""e M\""uller}"
        }))

        Assert.AreEqual(
            "Caf" & ChrW(&HE9) & " culture and self-reported well-being " & ChrW(&H2014) & " a registered report",
            result.Title)
        Assert.AreEqual("Ren" & ChrW(&HE9), result.Authors(0).Name.GivenName)
        Assert.AreEqual("Dupont", result.Authors(0).Name.FamilyName)
        Assert.AreEqual("Zo" & ChrW(&HEB), result.Authors(1).Name.GivenName)
        Assert.AreEqual("M" & ChrW(&HFC) & "ller", result.Authors(1).Name.FamilyName)

    End Sub

    <TestMethod>
    Public Sub EmptyText_ProducesNoProposal()

        Dim result As TitlePageParseResult = TitlePageParserService.Parse("   " & vbLf & "  ")

        Assert.AreEqual(TitlePageSourceFormat.None, result.SourceFormat)
        Assert.IsFalse(result.HasContent)
        Assert.AreEqual(0, result.Warnings.Count)

    End Sub

    <TestMethod>
    Public Sub Parsing_IsDeterministic()

        For Each text As String In {WordTitlePage, LatexTitlePage}
            Dim first As String = JsonSerializer.Serialize(TitlePageParserService.Parse(text))
            Dim second As String = JsonSerializer.Serialize(TitlePageParserService.Parse(text))
            Assert.AreEqual(first, second)
        Next

    End Sub

    <TestMethod>
    Public Sub Parsing_DoesNotRequestNetworkAccess()

        Dim original As IWebProxy = HttpClient.DefaultProxy
        Dim recorder As New RecordingProxy()

        Try
            HttpClient.DefaultProxy = recorder
            TitlePageParserService.Parse(WordTitlePage)
            TitlePageParserService.Parse(LatexTitlePage)
        Finally
            HttpClient.DefaultProxy = original
        End Try

        Assert.AreEqual(0, recorder.Requests)

    End Sub


    ' Whitfield (1), Okafor (1, 2; corresponding), Marsh (2).
    Private Shared Sub AssertAuthors(result As TitlePageParseResult)

        Assert.AreEqual(3, result.Authors.Count)
        CollectionAssert.AreEqual(
            {"Whitfield", "Okafor", "Marsh"},
            result.Authors.Select(Function(author) author.Name.FamilyName).ToList())
        CollectionAssert.AreEqual({"Dana", "Rui", "Lena"}, result.Authors.Select(Function(author) author.Name.GivenName).ToList())

        CollectionAssert.AreEqual({Psychology}, result.Authors(0).Affiliations)
        CollectionAssert.AreEqual({Psychology, Nursing}, result.Authors(1).Affiliations)
        CollectionAssert.AreEqual({Nursing}, result.Authors(2).Affiliations)

        Assert.IsFalse(result.Authors(0).IsCorrespondingAuthor)
        Assert.IsTrue(result.Authors(1).IsCorrespondingAuthor)
        Assert.IsFalse(result.Authors(2).IsCorrespondingAuthor)

    End Sub

    Private NotInheritable Class RecordingProxy
        Implements IWebProxy

        Public Property Requests As Integer

        Public Property Credentials As ICredentials Implements IWebProxy.Credentials

        Public Function GetProxy(destination As Uri) As Uri Implements IWebProxy.GetProxy
            Requests += 1
            Return destination
        End Function

        Public Function IsBypassed(host As Uri) As Boolean Implements IWebProxy.IsBypassed
            Requests += 1
            Return False
        End Function

    End Class

End Class
