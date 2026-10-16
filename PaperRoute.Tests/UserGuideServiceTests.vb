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


    ' #129: a Markdown table reaches Help as "Header: value" lines, one
    ' row per paragraph, never as raw pipe rows.
    <TestMethod>
    Public Sub ToPlainText_WritesTableRowsAsHeaderValueLines()

        Dim markdown As String =
            "| Service | Sends |" & vbLf &
            "| --- | --- |" & vbLf &
            "| Update check | Nothing about you. |" & vbLf &
            "| DOI lookup (**Crossref**) | A DOI. |"

        Assert.AreEqual(
            "Service: Update check" & Environment.NewLine &
            "Sends: Nothing about you." & Environment.NewLine &
            Environment.NewLine &
            "Service: DOI lookup (Crossref)" & Environment.NewLine &
            "Sends: A DOI.",
            UserGuideService.ToPlainText(
                markdown
            )
        )

    End Sub


    <TestMethod>
    Public Sub ToPlainText_OverTheRealGuide_LeavesNoRawTableRow()

        Dim plain As String =
            UserGuideService.ToPlainText(
                File.ReadAllText(
                    UserGuideService.GuideFilePath()
                )
            )

        Dim raw As List(Of String) =
            plain.Replace(
                vbCrLf,
                vbLf
            ).Split(
                ChrW(10)
            ).Where(
                Function(line) line.TrimStart().StartsWith(
                    "|",
                    StringComparison.Ordinal
                )
            ).ToList()

        Assert.AreEqual(
            0,
            raw.Count,
            "Raw table rows reached Help, such as: " &
            String.Join(
                " / ",
                raw.Take(2)
            )
        )

    End Sub


    ' #129: the built-in fallback, shown when the shipped guide can't be
    ' read, names no surface that v0.6 removed, and the ones that replaced
    ' them.
    <TestMethod>
    Public Sub BuiltInFallback_NamesNoRemovedSurface()

        Dim fallback As String =
            UserGuideService.BuiltInFallback()

        For Each removed As String In {
            "Use Data",
            "Manuscript Details"
        }

            Assert.IsFalse(
                fallback.Contains(
                    removed,
                    StringComparison.Ordinal
                ),
                "The fallback names a removed surface: " & removed
            )

        Next

        For Each current As String In {
            "Import & Export",
            "Library page",
            "Journals page",
            "Settings > Backup Library"
        }

            StringAssert.Contains(
                fallback,
                current
            )

        Next

    End Sub


    ' #129: Open Guide on GitHub shows the running version's guide, from
    ' its release tag; a copy Setup did not install, which no tag need
    ' match, shows master's.
    <TestMethod>
    Public Sub GitHubGuideUrl_IsTheRunningVersionsTag()

        StringAssert.EndsWith(
            UserGuideService.GitHubGuideUrlFor(
                "0.9.2",
                developmentBuild:=False
            ),
            "/blob/v0.9.2/docs/USER_GUIDE.md"
        )

        StringAssert.EndsWith(
            UserGuideService.GitHubGuideUrlFor(
                "1.0.0-rc.1",
                developmentBuild:=False
            ),
            "/blob/v1.0.0-rc.1/docs/USER_GUIDE.md",
            "A Preview release has its own tag."
        )

        StringAssert.EndsWith(
            UserGuideService.GitHubGuideUrlFor(
                "0.9.2",
                developmentBuild:=True
            ),
            "/blob/master/docs/USER_GUIDE.md"
        )

        StringAssert.EndsWith(
            UserGuideService.GitHubGuideUrlFor(
                String.Empty,
                developmentBuild:=False
            ),
            "/blob/master/docs/USER_GUIDE.md",
            "No version, no tag."
        )

        StringAssert.StartsWith(
            UserGuideService.GitHubGuideUrl,
            "https://github.com/JUhalt/PaperRoute-Tracker/blob/",
            "The link Help opens is built the same way."
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


    ' #127: the guide has Installing and Uninstalling sections, and the
    ' README's own Installing section points at both. What they say about
    ' the Windows prompt and uninstalling is matched against the release
    ' certification record by hand (#147); it is not pinned here.
    <TestMethod>
    Public Sub TheGuideHasInstallingAndUninstallingSections()

        Dim guide As String =
            File.ReadAllText(
                UserGuideService.GuideFilePath()
            ).Replace(
                vbCrLf,
                vbLf
            )

        Dim readme As String =
            File.ReadAllText(
                Path.Combine(
                    AssistantCoreTests.RepositoryRoot(),
                    "README.md"
                )
            )

        For Each section As String In {
            "Installing",
            "Uninstalling"
        }

            StringAssert.Contains(
                guide,
                vbLf & "## " & section & vbLf,
                "The guide has a section: " & section
            )

            StringAssert.Contains(
                readme,
                "docs/USER_GUIDE.md#" & section.ToLowerInvariant(),
                "The README points at the guide's section: " & section
            )

        Next

        Dim installing As String =
            SectionText(
                guide,
                "Installing"
            )

        For Each named As String In {
            "SHA256SUMS.txt",
            "Get-FileHash",
            "Install Partially Succeeded",
            "Windows 11",
            "Preview"
        }

            StringAssert.Contains(
                installing,
                named,
                "Installing covers: " & named
            )

        Next

        Dim uninstalling As String =
            SectionText(
                guide,
                "Uninstalling"
            )

        For Each kept As String In {
            "%LocalAppData%\PaperRoute\",
            "Documents\PaperRoute Library"
        }

            StringAssert.Contains(
                uninstalling,
                kept,
                "Uninstalling names what stays: " & kept
            )

        Next

    End Sub


    ' One "## " section of the guide, heading to the next "## ".
    Private Shared Function SectionText(
        guide As String,
        heading As String
    ) As String

        Dim start As Integer =
            guide.IndexOf(
                vbLf & "## " & heading & vbLf,
                StringComparison.Ordinal
            )

        Assert.IsTrue(
            start >= 0,
            "The guide has a section: " & heading
        )

        Dim finish As Integer =
            guide.IndexOf(
                vbLf & "## ",
                start + 1,
                StringComparison.Ordinal
            )

        Return guide.Substring(
            start,
            If(finish < 0, guide.Length, finish) - start
        )

    End Function


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

        Dim failure As ExceptionDispatchInfo =
            Nothing

        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                End Try
            End Sub
        )

        thread.SetApartmentState(
            ApartmentState.STA
        )

        thread.Start()
        thread.Join()

        failure?.Throw()

    End Sub

End Class
