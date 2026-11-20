Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class OrcidParsingTests

    Private Const SampleJson As String =
        "{" &
        """orcid-identifier"":{""path"":""0000-0002-1825-0097""}," &
        """person"":{" &
            """name"":{" &
                """given-names"":{""value"":""Joshua""}," &
                """family-name"":{""value"":""Uhalt""}," &
                """credit-name"":{""value"":""Joshua Uhalt""}" &
            "}," &
            """keywords"":{""keyword"":[{""content"":""social psychology""}]}," &
            """researcher-urls"":{""researcher-url"":[{""url"":{""value"":""https://example.org/profile""}}]}" &
        "}," &
        """activities-summary"":{" &
            """employments"":{""affiliation-group"":[{" &
                """summaries"":[{""employment-summary"":{" &
                    """department-name"":""Psychology""," &
                    """role-title"":""Professor""," &
                    """organization"":{" &
                        """name"":""Example University""," &
                        """address"":{""city"":""Hartford"",""region"":""CT"",""country"":""US""}" &
                    "}" &
                "}}]" &
            "}]}," &
            """works"":{""group"":[{" &
                """work-summary"":[{" &
                    """put-code"":123," &
                    """display-index"":""1""," &
                    """title"":{""title"":{""value"":""A Test Article""}}," &
                    """type"":""JOURNAL_ARTICLE""," &
                    """publication-date"":{""year"":{""value"":""2026""},""month"":{""value"":""08""}}," &
                    """external-ids"":{""external-id"":[{" &
                        """external-id-type"":""doi""," &
                        """external-id-value"":""10.1234/example""" &
                    "}]}" &
                "}]" &
            "}]}" &
        "}" &
        "}"


    <TestMethod>
    Public Sub ParseRecordJson_ReadsPublicIdentity()

        Dim result As OrcidProfileSuggestion =
            OrcidClient.ParseRecordJson(
                SampleJson
            )

        Assert.AreEqual(
            "0000-0002-1825-0097",
            result.Orcid
        )

        Assert.AreEqual(
            "Joshua",
            result.GivenName
        )

        Assert.AreEqual(
            "Uhalt",
            result.FamilyName
        )

        Assert.AreEqual(
            "Joshua Uhalt",
            result.CreditName
        )

        Assert.AreEqual(
            1,
            result.Keywords.Count
        )

    End Sub


    <TestMethod>
    Public Sub ParseRecordJson_ReadsEmploymentAndWork()

        Dim result As OrcidProfileSuggestion =
            OrcidClient.ParseRecordJson(
                SampleJson
            )

        Assert.AreEqual(
            1,
            result.Affiliations.Count
        )

        Assert.AreEqual(
            "Example University",
            result.Affiliations(0).Institution
        )

        Assert.AreEqual(
            1,
            result.Works.Count
        )

        Assert.AreEqual(
            123L,
            result.Works(0).PutCode
        )

        Assert.AreEqual(
            "10.1234/example",
            result.Works(0).Doi
        )

        Assert.AreEqual(
            2026,
            result.Works(0).PublishedDate.Value.Year
        )

    End Sub

End Class
