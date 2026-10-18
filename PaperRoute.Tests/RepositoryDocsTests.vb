Imports System
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The repository's own documents, read as files: what they promise about
' supported versions and 1.0. Each check names the words it looks for,
' so a failure doesn't print a whole file.
<TestClass>
Public Class RepositoryDocsTests

    ' #153: only the 0.9 releases update straight to 1.0, and Windows 11
    ' on x64 is the one Windows that is tested. The upgrade notes and the
    ' security policy each say so under "Supported versions", in the
    ' guide's words for Windows, so the three cannot drift apart.
    <TestMethod>
    Public Sub TheUpgradeNotesAndSecurityPolicyNameTheSupportedVersions()

        Const untested As String =
            "Windows 10 and Windows on ARM may work, but they are not tested."

        StringAssert.Contains(
            Section(
                ReadDocument(
                    Path.Combine("docs", "USER_GUIDE.md")
                ),
                "## Installing"
            ),
            untested,
            "The guide's Installing section says which Windows versions are not tested."
        )

        For Each document As String In {
            "UPGRADE_NOTES.md",
            Path.Combine(".github", "SECURITY.md")
        }

            Dim supported As String =
                Section(
                    ReadDocument(
                        document
                    ),
                    "## Supported versions"
                )

            For Each expected As String In {
                "Windows 11",
                "x64",
                untested,
                "Only the 0.9 releases update straight to 1.0"
            }

                StringAssert.Contains(
                    supported,
                    expected,
                    document & " says under Supported versions: " & expected
                )

            Next

        Next

    End Sub


    ' A file in the repository, with its line endings made plain.
    Private Shared Function ReadDocument(
        relativePath As String
    ) As String

        Return File.ReadAllText(
            Path.Combine(
                AssistantCoreTests.RepositoryRoot(),
                relativePath
            )
        ).Replace(
            vbCrLf,
            vbLf
        )

    End Function


    ' A Markdown section: its heading line, up to the next heading of the
    ' same level or higher.
    Private Shared Function Section(
        document As String,
        heading As String
    ) As String

        Dim lines As String() =
            document.Split(
                ChrW(10)
            )

        Dim start As Integer =
            Array.IndexOf(
                lines,
                heading
            )

        Assert.IsTrue(
            start >= 0,
            "The document has a section: " & heading
        )

        Dim higher As New Regex(
            "^#{1," & heading.IndexOf(" "c).ToString() & "} "
        )

        Dim finish As Integer =
            start + 1

        While finish < lines.Length AndAlso
              Not higher.IsMatch(lines(finish))

            finish += 1

        End While

        Return String.Join(
            vbLf,
            lines.Skip(start).Take(finish - start)
        )

    End Function

End Class
