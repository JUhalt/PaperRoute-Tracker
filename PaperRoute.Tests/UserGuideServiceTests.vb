Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class UserGuideServiceTests

    <TestMethod>
    Public Sub ToPlainText_ConvertsHeadingsAndBullets()

        Dim markdown As String =
            "# Guide" & vbLf &
            "## Quick Start" & vbLf &
            "- Add a manuscript" & vbLf &
            "- Save it"

        Dim plain As String =
            UserGuideService.ToPlainText(
                markdown
            )

        StringAssert.Contains(
            plain,
            "GUIDE"
        )

        StringAssert.Contains(
            plain,
            "Quick Start"
        )

        StringAssert.Contains(
            plain,
            "• Add a manuscript"
        )

    End Sub


    <TestMethod>
    Public Sub ToPlainText_ExpandsMarkdownLinks()

        Dim plain As String =
            UserGuideService.ToPlainText(
                "[PaperRoute](https://github.com/JUhalt/PaperRoute-Tracker)"
            )

        StringAssert.Contains(
            plain,
            "PaperRoute — https://github.com/JUhalt/PaperRoute-Tracker"
        )

    End Sub


    <TestMethod>
    Public Sub ToPlainText_EmptyInput_ReturnsBlank()

        Assert.AreEqual(
            String.Empty,
            UserGuideService.ToPlainText(
                String.Empty
            )
        )

    End Sub


    ' Shared with #122: every folder the "Recovering your library" section
    ' names in backticks is one Diagnostics prints, so a reader can find it.
    ' The section therefore puts only file and folder names in backticks.
    <TestMethod>
    Public Sub RecoveringYourLibrary_NamesOnlyFoldersDiagnosticsPrints()

        Dim guide As String =
            File.ReadAllText(
                UserGuideService.GuideFilePath()
            ).Replace(
                vbCrLf,
                vbLf
            )

        Dim start As Integer =
            guide.IndexOf(
                vbLf & "## Recovering your library" & vbLf,
                StringComparison.Ordinal
            )

        Assert.IsTrue(
            start >= 0,
            "The guide has a Recovering your library section."
        )

        Dim finish As Integer =
            guide.IndexOf(
                vbLf & "## ",
                start + 1,
                StringComparison.Ordinal
            )

        Dim section As String =
            guide.Substring(
                start,
                If(finish < 0, guide.Length, finish) - start
            )

        Dim named As List(Of String) =
            Regex.Matches(
                section,
                "`([^`]+)`"
            ).Cast(Of Match)().
            Select(Function(match) match.Groups(1).Value).
            Distinct().
            ToList()

        CollectionAssert.Contains(
            named,
            "removed",
            "The section names the removed folder."
        )

        ' #122: the copies PaperRoute makes on its own, each one findable.
        For Each expected As String In {"manuscripts.bak", "authors.bak", "backups", "recovery"}

            CollectionAssert.Contains(
                named,
                expected,
                "The section names " & expected & "."
            )

        Next

        Dim report As String =
            String.Empty

        ' Every default root under a temporary folder: the report reads the
        ' schema marker and lists the logs and key folders, which would
        ' otherwise be the user's own.
        Dim root As String =
            CreateTemporaryRoot()

        StorageEnvironment.RootOverride =
            root

        Try

            RunOnSta(
                Sub()

                    Using diagnostics As New DiagnosticsForm(
                        New AppSettings()
                    )

                        report =
                            diagnostics.ReportText

                    End Using

                End Sub
            )

        Finally

            StorageEnvironment.RootOverride =
                Nothing

            DeleteTemporaryRoot(
                root
            )

        End Try

        Dim lines As String() =
            report.Replace(
                vbCrLf,
                vbLf
            ).Split(
                ChrW(10)
            )

        For Each folder As String In named

            Assert.IsTrue(
                lines.Any(
                    Function(line) line.TrimEnd().EndsWith(
                        "\" & folder,
                        StringComparison.OrdinalIgnoreCase
                    )
                ),
                "Diagnostics prints the folder the guide names: " & folder
            )

        Next

    End Sub


    ' #122: the move-to-another-computer answer links to the section that
    ' says where the files are.
    <TestMethod>
    Public Sub MoveToAnotherComputer_LinksToRecoveringYourLibrary()

        Dim guide As String =
            File.ReadAllText(
                UserGuideService.GuideFilePath()
            ).Replace(
                vbCrLf,
                vbLf
            )

        Dim start As Integer =
            guide.IndexOf(
                vbLf & "## How do I move PaperRoute to another computer?" & vbLf,
                StringComparison.Ordinal
            )

        Assert.IsTrue(
            start >= 0,
            "The guide answers how to move to another computer."
        )

        Dim finish As Integer =
            guide.IndexOf(
                vbLf & "## ",
                start + 1,
                StringComparison.Ordinal
            )

        Dim answer As String =
            guide.Substring(
                start,
                finish - start
            )

        StringAssert.Contains(
            answer,
            "[Recovering your library](",
            "The answer links to the section."
        )

        StringAssert.Contains(
            answer,
            "#recovering-your-library)",
            "The link goes to the section's heading."
        )

    End Sub


    Private Shared Sub RunOnSta(
        action As Action
    )

        StaRunner.Run(
            action
        )

    End Sub

End Class
