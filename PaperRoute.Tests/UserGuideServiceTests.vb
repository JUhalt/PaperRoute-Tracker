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


    ' #139: the guide's Accessibility section lists every keyboard
    ' shortcut PaperRoute handles. Its table of window-wide shortcuts is
    ' exactly the keys Form1.ProcessCmdKey acts on, and every key any other
    ' ProcessCmdKey, ProcessDialogKey, OnKeyDown, KeyDown or PreviewKeyDown
    ' handler in the app acts on is named in the section. The keys are read
    ' from the source: pressing them would act on a library and open
    ' windows, and F1 opens this guide in a modal window.
    <TestMethod>
    Public Sub TheAccessibilitySectionListsEveryShortcut()

        Dim section As String =
            SectionText(
                File.ReadAllText(
                    UserGuideService.GuideFilePath()
                ).Replace(
                    vbCrLf,
                    vbLf
                ),
                "Accessibility"
            )

        Dim app As String =
            Path.Combine(
                AssistantCoreTests.RepositoryRoot(),
                "ManuscriptPipeline"
            )

        Dim handlers As New List(Of (Place As String, Name As String, Body As String))()

        For Each sourceFile As String In Directory.GetFiles(app, "*.vb", SearchOption.AllDirectories).
            Where(Function(candidate) Not candidate.Contains("\obj\", StringComparison.OrdinalIgnoreCase) AndAlso
                                      Not candidate.Contains("\bin\", StringComparison.OrdinalIgnoreCase))

            For Each handler In KeyHandlers(File.ReadAllText(sourceFile))
                handlers.Add((Path.GetFileName(sourceFile), handler.Name, handler.Body))
            Next

        Next

        Dim windowWide As List(Of (Place As String, Name As String, Body As String)) =
            handlers.Where(Function(handler) handler.Place = "Form1.vb" AndAlso handler.Name = "ProcessCmdKey").ToList()

        Assert.AreEqual(
            1,
            windowWide.Count,
            "Form1.ProcessCmdKey is found."
        )

        ' The search has teeth: it finds the handlers the app has, not none.
        Assert.IsTrue(
            handlers.Count >= 12,
            "Key handlers found: " & String.Join(", ", handlers.Select(Function(handler) handler.Place & " " & handler.Name))
        )

        Dim commands As New HashSet(Of String)(
            CaseShortcuts(windowWide(0).Body),
            StringComparer.Ordinal
        )

        Dim listed As New HashSet(Of String)(
            section.Split(ChrW(10)).
                Select(Function(line) Regex.Match(line, "^\|\s*\*\*([^*]+)\*\*\s*\|")).
                Where(Function(match) match.Success).
                Select(Function(match) match.Groups(1).Value),
            StringComparer.Ordinal
        )

        Assert.IsTrue(
            commands.Count >= 10,
            "Form1.ProcessCmdKey's shortcuts are read: " & String.Join(", ", commands)
        )

        CollectionAssert.AreEquivalent(
            commands.OrderBy(Function(item) item, StringComparer.Ordinal).ToList(),
            listed.OrderBy(Function(item) item, StringComparer.Ordinal).ToList(),
            "The table lists " & String.Join(", ", listed) & "; ProcessCmdKey handles " & String.Join(", ", commands)
        )

        Dim missing As New List(Of String)()

        For Each handler In handlers.Except(windowWide)

            For Each key As String In HandlerKeys(handler.Body)

                If Not section.Contains("**" & key & "**", StringComparison.Ordinal) Then
                    missing.Add(handler.Place & " " & handler.Name & ": " & key)
                End If

            Next

        Next

        Assert.AreEqual(
            0,
            missing.Count,
            "Keys the Accessibility section does not name: " & String.Join("; ", missing)
        )

    End Sub


    ' The bodies of a source file's key handlers: overrides of
    ' ProcessCmdKey, ProcessDialogKey and OnKeyDown, and the lambdas and
    ' subs added to a KeyDown or PreviewKeyDown event.
    Private Shared Function KeyHandlers(
        source As String
    ) As List(Of (Name As String, Body As String))

        Dim lines As String() =
            source.Replace(vbCrLf, vbLf).Split(ChrW(10))

        Dim found As New List(Of (Name As String, Body As String))()

        For index As Integer = 0 To lines.Length - 1

            Dim overriding As Match =
                Regex.Match(lines(index), "^(\s*)(?:\w+ )*Overrides (Sub|Function) (ProcessCmdKey|ProcessDialogKey|OnKeyDown)\(")

            If overriding.Success Then
                found.Add((overriding.Groups(3).Value, BodyFrom(lines, index, overriding.Groups(1).Value & "End " & overriding.Groups(2).Value)))
                Continue For
            End If

            Dim added As Match =
                Regex.Match(lines(index), "AddHandler\s+[\w.]+\.(KeyDown|PreviewKeyDown),\s*(?:AddressOf\s+(\w+))?\s*$")

            If Not added.Success Then
                Continue For
            End If

            Dim target As String =
                If(added.Groups(2).Success, added.Groups(2).Value, String.Empty)

            Dim start As Integer =
                index + 1

            If target.Length = 0 Then

                Dim nextLine As Match =
                    Regex.Match(lines(start), "^(\s*)(?:AddressOf\s+(\w+)|(?:Async\s+)?Sub\()")

                If nextLine.Groups(2).Success Then

                    target = nextLine.Groups(2).Value

                Else

                    Assert.IsTrue(
                        nextLine.Success,
                        "The handler added at line " & (index + 1).ToString() & " is read: " & lines(start).Trim()
                    )

                    found.Add((added.Groups(1).Value, BodyFrom(lines, start, nextLine.Groups(1).Value & "End Sub")))
                    Continue For

                End If

            End If

            Dim declared As Integer =
                Array.FindIndex(lines, Function(line) Regex.IsMatch(line, "^\s*(?:\w+ )*Sub " & target & "\("))

            Assert.IsTrue(
                declared >= 0,
                "The handler " & target & " is found."
            )

            found.Add((target, BodyFrom(lines, declared, Regex.Match(lines(declared), "^\s*").Value & "End Sub")))

        Next

        Return found

    End Function


    ' The lines after start, up to the line that closes it.
    Private Shared Function BodyFrom(
        lines As String(),
        start As Integer,
        closing As String
    ) As String

        Dim body As New List(Of String)()

        For index As Integer = start + 1 To lines.Length - 1

            If lines(index).TrimEnd() = closing Then
                Exit For
            End If

            body.Add(lines(index))

        Next

        Return String.Join(vbLf, body)

    End Function


    ' The shortcuts a ProcessCmdKey's Case lines name, such as "Ctrl+1"
    ' for Keys.Control Or Keys.D1; the number pad's keys are the same
    ' shortcuts.
    Private Shared Function CaseShortcuts(
        body As String
    ) As List(Of String)

        Dim shortcuts As New List(Of String)()

        For Each line As String In body.Split(ChrW(10)).Where(Function(item) item.TrimStart().StartsWith("Case Keys.", StringComparison.Ordinal))

            For Each alternative As String In line.Trim().Substring("Case ".Length).Split(","c)

                Dim keys As List(Of String) =
                    Regex.Matches(alternative, "Keys\.(\w+)").Cast(Of Match)().Select(Function(match) match.Groups(1).Value).ToList()

                Dim modifiers As List(Of String) =
                    keys.Where(Function(key) Modifier(key).Length > 0).Select(Function(key) Modifier(key)).ToList()

                Dim main As String =
                    keys.Single(Function(key) Modifier(key).Length = 0)

                shortcuts.Add(String.Join("+", modifiers.Concat({KeyLabel(main)})))

            Next

        Next

        Return shortcuts

    End Function


    ' The keys another handler acts on, as the guide names them: a letter
    ' after a check of e.Control is a Ctrl shortcut.
    Private Shared Function HandlerKeys(
        body As String
    ) As List(Of String)

        Dim control As Boolean =
            body.Contains("e.Control", StringComparison.Ordinal)

        Return Regex.Matches(body, "Keys\.(\w+)").Cast(Of Match)().
            Select(Function(match) match.Groups(1).Value).
            Where(Function(key) Modifier(key).Length = 0 AndAlso key <> "KeyCode").
            Select(Function(key) If(control AndAlso key.Length = 1, "Ctrl+", String.Empty) & KeyLabel(key)).
            Distinct().
            ToList()

    End Function


    Private Shared Function Modifier(
        key As String
    ) As String

        Select Case key
            Case "Control" : Return "Ctrl"
            Case "Alt" : Return "Alt"
            Case "Shift" : Return "Shift"
            Case Else : Return String.Empty
        End Select

    End Function


    ' A key as the guide writes it.
    Private Shared Function KeyLabel(
        key As String
    ) As String

        If Regex.IsMatch(key, "^(D|NumPad)\d$") Then
            Return key.Substring(key.Length - 1)
        End If

        Select Case key
            Case "Return", "Enter" : Return "Enter"
            Case "Escape" : Return "Esc"
            Case "Back" : Return "Backspace"
            Case "BrowserBack" : Return "Browser Back"
            Case "BrowserForward" : Return "Browser Forward"
            Case Else : Return key
        End Select

    End Function


    Private Shared Sub RunOnSta(
        action As Action
    )

        StaRunner.Run(
            action
        )

    End Sub

End Class
