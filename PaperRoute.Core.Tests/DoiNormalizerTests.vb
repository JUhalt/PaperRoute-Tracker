Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class DoiNormalizerTests

    ' A "doi:" prefix, a doi.org URL and trailing citation punctuation each
    ' come off, one row each.
    <TestMethod>
    <DataRow("doi: 10.1234/example.1", "10.1234/example.1")>
    <DataRow("https://doi.org/10.1234/example.2", "10.1234/example.2")>
    <DataRow("10.1234/example.3.", "10.1234/example.3")>
    Public Sub Normalize_ReturnsTheBareDoi(value As String, expected As String)

        Assert.AreEqual(
            expected,
            DoiNormalizer.Normalize(
                value
            )
        )

    End Sub


    <TestMethod>
    Public Sub IsValid_RejectsNonDoi()

        Assert.IsFalse(
            DoiNormalizer.IsValid(
                "not-a-doi"
            )
        )

    End Sub

End Class
