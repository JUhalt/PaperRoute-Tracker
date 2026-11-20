Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class CrossrefParsingTests

    <TestMethod>
    Public Sub ParseWorkJson_ReadsCoreMetadata()

        Dim json As String =
            "{" &
            """message"": {" &
            """DOI"": ""10.1234/example"", " &
            """title"": [""A Paper Title""], " &
            """container-title"": [""Journal of Examples""], " &
            """publisher"": ""Example Publisher"", " &
            """volume"": ""12"", " &
            """issue"": ""3"", " &
            """page"": ""44-55"", " &
            """URL"": ""https://doi.org/10.1234/example"", " &
            """published-print"": {""date-parts"": [[2026, 8, 1]]}, " &
            """subject"": [""Psychology"", ""Methods""]" &
            "}" &
            "}"

        Dim result As CrossrefMetadataSuggestion =
            CrossrefClient.ParseWorkJson(
                json
            )

        Assert.AreEqual(
            "10.1234/example",
            result.Doi
        )

        Assert.AreEqual(
            "A Paper Title",
            result.Title
        )

        Assert.AreEqual(
            "Journal of Examples",
            result.Journal
        )

        Assert.AreEqual(
            New DateTime(2026, 8, 1),
            result.PublishedDate.Value
        )

        Assert.AreEqual(
            2,
            result.Keywords.Count
        )

    End Sub


    <TestMethod>
    Public Sub ParseWorkJson_CleansAbstractAndReadsAuthors()

        Dim json As String =
            "{" &
            """message"": {" &
            """DOI"": ""10.1234/example"", " &
            """abstract"": ""<jats:p>An &amp; abstract.</jats:p>"", " &
            """author"": [{" &
            """given"": ""Jane"", " &
            """family"": ""Smith"", " &
            """ORCID"": ""https://orcid.org/0000-0001-2345-6789"", " &
            """affiliation"": [{""name"": ""Example University""}]" &
            "}]" &
            "}" &
            "}"

        Dim result As CrossrefMetadataSuggestion =
            CrossrefClient.ParseWorkJson(
                json
            )

        Assert.AreEqual(
            "An & abstract.",
            result.AbstractText
        )

        Assert.AreEqual(
            1,
            result.Authors.Count
        )

        Assert.AreEqual(
            "Jane Smith",
            result.Authors(0).DisplayName
        )

        Assert.AreEqual(
            "0000-0001-2345-6789",
            result.Authors(0).Orcid
        )

        Assert.AreEqual(
            "Example University",
            result.Authors(0).Affiliations(0)
        )

    End Sub

End Class
