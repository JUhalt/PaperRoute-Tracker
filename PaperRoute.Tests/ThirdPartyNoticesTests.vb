Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' THIRD-PARTY-NOTICES.txt ships beside the app (#149) and names each NuGet
' package at its version. A dependency update (Dependabot, #144) that moves a
' version without the notices would ship notices for a package the app no
' longer contains, so the restored graph and the notices are compared here.
' This guard cannot fail before #149: the file it reads did not exist.
<TestClass>
Public Class ThirdPartyNoticesTests

    <TestMethod>
    Public Sub TheNoticesNameEveryRestoredPackageAtItsVersion()

        Dim root As String =
            AssistantCoreTests.RepositoryRoot()

        Dim notices As String =
            File.ReadAllText(
                Path.Combine(root, "THIRD-PARTY-NOTICES.txt")
            )

        Dim assetsPath As String =
            Path.Combine(root, "ManuscriptPipeline", "obj", "project.assets.json")

        Assert.IsTrue(
            File.Exists(assetsPath),
            "The app project has been restored: " & assetsPath
        )

        Dim packages As New List(Of KeyValuePair(Of String, String))

        Using document As JsonDocument = JsonDocument.Parse(File.ReadAllText(assetsPath))

            For Each library As JsonProperty In document.RootElement.GetProperty("libraries").EnumerateObject()

                If library.Value.GetProperty("type").GetString() <> "package" Then
                    Continue For
                End If

                Dim parts() As String = library.Name.Split("/"c)

                packages.Add(
                    New KeyValuePair(Of String, String)(parts(0), parts(1))
                )

            Next

        End Using

        Assert.IsTrue(
            packages.Count > 0,
            "The restored graph lists packages."
        )

        For Each package As KeyValuePair(Of String, String) In packages

            ' The name followed by a version, and not as the start of a
            ' longer name (ClosedXML is not ClosedXML.Parser).
            Dim versions As List(Of String) =
                Regex.Matches(
                    notices,
                    "(?<![\w.])" & Regex.Escape(package.Key) & " (\d+(?:\.\d+)+)"
                ).Select(Function(found) found.Groups(1).Value).Distinct().ToList()

            CollectionAssert.AreEqual(
                {package.Value},
                versions,
                package.Key & " " & package.Value & " is named in THIRD-PARTY-NOTICES.txt at that version and no other (found: " & String.Join(", ", versions) & ")."
            )

        Next

    End Sub

End Class