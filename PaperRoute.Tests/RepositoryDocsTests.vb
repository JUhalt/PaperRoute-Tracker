Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The repository's own documents, read as files: what they promise about
' supported versions, security updates and 1.0. Each check names the
' words it looks for, so a failure doesn't print a whole file.
<TestClass>
Public Class RepositoryDocsTests

    ' #153: only the 0.9 releases update straight to 1.0, and Windows 11
    ' on x64 is the one Windows that is tested. The upgrade notes and the
    ' security policy each say so under "Supported versions", in the
    ' guide's words for Windows, so the three cannot drift apart.
    ' The guide's sentence is a guard: it predates #153, so only the other
    ' two documents could fail before it.
    <TestMethod>
    Public Sub TheUpgradeNotesAndSecurityPolicyNameTheSupportedVersions()

        Const untested As String =
            "Windows 10 and Windows on ARM may work, but they are not tested."

        ' #153's decision, worded for what the update check does: an older
        ' version is offered 1.0 too, so the documents say which path is
        ' supported and what to do before taking the other one.
        Const updatePath As String =
            "Only the 0.9 releases are supported for updating straight to 1.0. An older version's update check offers 1.0 as well: the migration tests cover that update, but no release certification does, so create a backup with **Settings → Backup Library...** first."

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

            Assert.IsFalse(
                supported.Contains(
                    "certified",
                    StringComparison.Ordinal
                ),
                document & " promises nothing about certification under Supported versions."
            )

            For Each expected As String In {
                "Windows 11",
                "x64",
                untested,
                updatePath
            }

                StringAssert.Contains(
                    supported,
                    expected,
                    document & " says under Supported versions: " & expected
                )

            Next

        Next

    End Sub


    ' #154: PaperRoute bundles the .NET runtime, so a runtime security fix
    ' reaches users only in a PaperRoute release. The security policy
    ' states the best-effort target in one paragraph: what affects
    ' PaperRoute, how the fix arrives, and the issue that gives the signal.
    <TestMethod>
    Public Sub TheSecurityPolicyStatesTheRuntimeUpdateTarget()

        Dim paragraph As String =
            Section(
                ReadDocument(
                    Path.Combine(".github", "SECURITY.md")
                ),
                "## Supported versions"
            ).Split(
                ChrW(10)
            ).FirstOrDefault(
                Function(line) line.Contains(
                    "30 days",
                    StringComparison.Ordinal
                )
            )

        Assert.IsNotNull(
            paragraph,
            "Supported versions has a paragraph with the 30-day target."
        )

        For Each expected As String In {
            ".NET runtime",
            "best-effort",
            "component PaperRoute ships",
            "update check",
            "/issues/144)"
        }

            StringAssert.Contains(
                paragraph,
                expected,
                "The runtime paragraph says: " & expected
            )

        Next

    End Sub


    ' #148: each decision 1.0 needed (#152 to #156), and each default
    ' adopted without a decision issue (onboarding, #130; Portable.zip,
    ' #149), has a dated paragraph in the roadmap linking the issue that
    ' does the work.
    <TestMethod>
    Public Sub TheRoadmapRecordsEachDispositionWithItsDate()

        Dim paragraphs As String() =
            Section(
                ReadDocument(
                    "ROADMAP.md"
                ),
                "## Decisions and deferrals"
            ).Split(
                ChrW(10)
            ).Where(
                Function(line) line.StartsWith(
                    "**",
                    StringComparison.Ordinal
                )
            ).ToArray()

        Dim dated As New Regex(
            "(January|February|March|April|May|June|July|August|September|October|November|December) \d{1,2}, \d{4}"
        )

        For Each issue As Integer In {152, 153, 154, 155, 156, 130, 149}

            Dim paragraph As String =
                paragraphs.FirstOrDefault(
                    Function(line) line.Contains(
                        "/issues/" & issue.ToString() & ")",
                        StringComparison.Ordinal
                    )
                )

            Assert.IsNotNull(
                paragraph,
                "A paragraph under Decisions and deferrals links #" & issue.ToString() & "."
            )

            Assert.IsTrue(
                dated.IsMatch(paragraph),
                "The paragraph for #" & issue.ToString() & " is dated: " & paragraph
            )

        Next

    End Sub


    ' #148: what 1.0 knowingly leaves as it is, in one list #44 can cite.
    ' The scaling entry is #93's not-converted list as it stood when
    ' written; it is updated when #93 lands. It is not counted against the
    ' code, so a #93 conversion doesn't fail a roadmap test.
    <TestMethod>
    Public Sub TheResidualLimitationsAreListedInOnePlace()

        Dim limitations As String =
            Section(
                ReadDocument(
                    "ROADMAP.md"
                ),
                "## Residual limitations"
            )

        For Each expected As String In {
            "Schema 10",
            "Crossref, ORCID, BibTeX and RIS imports",
            "/issues/93)",
            "docs/USER_GUIDE.md#what-windows-draws",
            "contrast theme",
            "`removed`",
            "recovery draft",
            "CODE_SIGNING.md"
        }

            StringAssert.Contains(
                limitations,
                expected,
                "The list covers: " & expected
            )

        Next

    End Sub


    ' #148: onboarding for 1.0 is the welcome, the example library and the
    ' welcome's Quick Start link (#130); the guided tutorial comes after
    ' 1.0. No sentence in the roadmap or the guide promises one sooner.
    <TestMethod>
    Public Sub NothingPromisesATutorialBeforeVersion1()

        For Each document As String In {
            "ROADMAP.md",
            Path.Combine("docs", "USER_GUIDE.md")
        }

            Dim promises As List(Of String) =
                Regex.Split(
                    ReadDocument(
                        document
                    ),
                    "(?<=[.!?])\s+|\n"
                ).Where(
                    Function(sentence) Regex.IsMatch(
                        sentence,
                        "tutorial|guided onboarding",
                        RegexOptions.IgnoreCase
                    ) AndAlso Not sentence.Contains(
                        "after 1.0",
                        StringComparison.Ordinal
                    )
                ).ToList()

            Assert.AreEqual(
                0,
                promises.Count,
                document & " promises a tutorial before 1.0: " & String.Join(" | ", promises)
            )

        Next

    End Sub


    ' #148: the README's build badge is captioned with the name the
    ' workflow runs under, as the Actions page shows it.
    <TestMethod>
    Public Sub TheReadmeBadgeIsCaptionedWithTheWorkflowsName()

        Dim workflow As String =
            ReadDocument(
                Path.Combine(".github", "workflows", "build.yml")
            ).Split(
                ChrW(10)
            ).First(
                Function(line) line.StartsWith(
                    "name:",
                    StringComparison.Ordinal
                )
            ).Substring(
                "name:".Length
            ).Trim()

        Dim badge As Match =
            Regex.Match(
                ReadDocument(
                    "README.md"
                ),
                "\[!\[([^\]]+)\]\([^)]+/actions/workflows/build\.yml/badge\.svg\)\]"
            )

        Assert.IsTrue(
            badge.Success,
            "The README shows the build workflow's badge."
        )

        Assert.AreEqual(
            workflow,
            badge.Groups(1).Value,
            "The badge is captioned with the workflow's name."
        )

    End Sub


    ' #156, recorded with #148: the 1.0 bar's accessibility claim is the
    ' one the maintainer decided, and it points at what was tested.
    <TestMethod>
    Public Sub TheOneZeroBarSaysWhatAccessibleMeans()

        Dim bar As String =
            Section(
                ReadDocument(
                    "ROADMAP.md"
                ),
                "# v1.0 Trusted Research Workflow"
            )

        Assert.IsFalse(
            bar.Contains(
                "accessible keyboard-first UI",
                StringComparison.Ordinal
            ),
            "The 1.0 bar no longer leaves accessible undefined."
        )

        For Each expected As String In {
            "keyboard-complete",
            "Narrator",
            "contrast themes",
            "docs/USER_GUIDE.md#accessibility",
            "/issues/156)"
        }

            StringAssert.Contains(
                bar,
                expected,
                "The 1.0 bar says: " & expected
            )

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
