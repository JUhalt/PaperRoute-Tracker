Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class OrcidIdentifierTests

    ' An orcid.org URL and an "ORCID iD:" prefix, one row each.
    <TestMethod>
    <DataRow("https://orcid.org/0000-0002-1825-0097", "0000-0002-1825-0097")>
    <DataRow("ORCID iD: 0000-0001-5109-3700", "0000-0001-5109-3700")>
    Public Sub Normalize_ReturnsCanonicalId(value As String, expected As String)

        Assert.AreEqual(
            expected,
            OrcidIdentifierService.Normalize(
                value
            )
        )

    End Sub


    ' A correct check digit passes, and the same iD with a wrong one fails.
    <TestMethod>
    <DataRow("0000-0002-1825-0097", True)>
    <DataRow("0000-0002-1825-0098", False)>
    Public Sub IsValid_ChecksTheCheckDigit(value As String, expected As Boolean)

        Assert.AreEqual(
            expected,
            OrcidIdentifierService.IsValid(
                value
            )
        )

    End Sub

End Class
