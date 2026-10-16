Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services

    Public NotInheritable Class UserGuideService

        Private Const GitHubGuideUrlFormat As String =
            "https://github.com/JUhalt/PaperRoute-Tracker/blob/{0}/docs/USER_GUIDE.md"


        Private Sub New()
        End Sub


        ' The guide of the running version, from its release tag, so an
        ' installed release opens its own guide and not whatever master says
        ' that day (#129). A copy Setup did not install, from Visual Studio
        ' or the portable build, opens master's.
        Public Shared ReadOnly Property GitHubGuideUrl As String
            Get

                Return GitHubGuideUrlFor(
                    UpdateService.CurrentVersionText(),
                    developmentBuild:=Not UpdateService.IsInstalledBuild(
                        Models.AppUpdateChannel.Stable
                    )
                )

            End Get
        End Property


        Friend Shared Function GitHubGuideUrlFor(
            version As String,
            developmentBuild As Boolean
        ) As String

            Dim reference As String =
                "master"

            If Not developmentBuild AndAlso
               Not String.IsNullOrWhiteSpace(version) Then

                ' The tag is "v" and the version without build metadata.
                Dim tag As String =
                    version.Trim()

                Dim metadataIndex As Integer =
                    tag.IndexOf("+"c)

                If metadataIndex >= 0 Then
                    tag = tag.Substring(0, metadataIndex)
                End If

                reference =
                    "v" & tag

            End If

            Return String.Format(
                GitHubGuideUrlFormat,
                reference
            )

        End Function


        Public Shared Function GuideFilePath() As String

            Return Path.Combine(
                AppContext.BaseDirectory,
                "docs",
                "USER_GUIDE.md"
            )

        End Function


        Public Shared Function LoadMarkdown() As String

            Dim path As String =
                GuideFilePath()

            Try

                If File.Exists(path) Then

                    Dim content As String =
                        File.ReadAllText(
                            path
                        )

                    If Not String.IsNullOrWhiteSpace(
                        content
                    ) Then

                        Return content

                    End If

                End If

            Catch
                ' Fall through to the built-in quick-start text.
            End Try

            Return BuiltInFallback()

        End Function


        Public Shared Function ToPlainText(
            markdown As String
        ) As String

            If String.IsNullOrWhiteSpace(
                markdown
            ) Then

                Return String.Empty

            End If

            Dim output As New StringBuilder()

            Dim inCodeFence As Boolean =
                False

            ' The header row of the table being read, or Nothing outside
            ' one, and how many of its rows have been written.
            Dim tableHeaders As List(Of String) =
                Nothing

            Dim tableRows As Integer =
                0

            Dim normalized As String =
                markdown.
                    Replace(
                        vbCrLf,
                        vbLf
                    ).
                    Replace(
                        vbCr,
                        vbLf
                    )

            For Each rawLine As String In
                normalized.Split(
                    New Char() {
                        ChrW(10)
                    },
                    StringSplitOptions.None
                )

                Dim line As String =
                    rawLine

                If line.TrimStart().
                    StartsWith(
                        "```",
                        StringComparison.Ordinal
                    ) Then

                    inCodeFence =
                        Not inCodeFence

                    Continue For

                End If

                If inCodeFence Then

                    output.AppendLine(
                        "    " &
                        line
                    )

                    Continue For

                End If

                Dim trimmed As String =
                    line.Trim()

                ' A table row (#129). The first row names the columns, the
                ' separator row is dropped, and each later row becomes one
                ' "Header: value" line per column, one row per paragraph.
                If trimmed.Length >= 2 AndAlso
                   trimmed.StartsWith(
                       "|",
                       StringComparison.Ordinal
                   ) AndAlso
                   trimmed.EndsWith(
                       "|",
                       StringComparison.Ordinal
                   ) Then

                    Dim cells As List(Of String) =
                        TableCells(
                            trimmed
                        )

                    If tableHeaders Is Nothing Then

                        tableHeaders =
                            cells

                        tableRows =
                            0

                    ElseIf Not Regex.IsMatch(
                        trimmed,
                        "^\|(\s*:?-+:?\s*\|)+$"
                    ) Then

                        If tableRows > 0 Then
                            output.AppendLine()
                        End If

                        Dim columns As Integer =
                            Math.Min(
                                cells.Count,
                                tableHeaders.Count
                            )

                        For index As Integer = 0 To columns - 1

                            output.AppendLine(
                                tableHeaders(index) &
                                ": " &
                                cells(index)
                            )

                        Next

                        tableRows += 1

                    End If

                    Continue For

                End If

                tableHeaders =
                    Nothing

                If trimmed.StartsWith(
                    "# ",
                    StringComparison.Ordinal
                ) Then

                    Dim heading As String =
                        CleanInlineMarkdown(
                            trimmed.Substring(2)
                        ).
                        ToUpperInvariant()

                    output.AppendLine()
                    output.AppendLine(heading)
                    output.AppendLine(
                        New String(
                            "="c,
                            Math.Min(
                                80,
                                Math.Max(
                                    3,
                                    heading.Length
                                )
                            )
                        )
                    )

                    Continue For

                End If

                If trimmed.StartsWith(
                    "## ",
                    StringComparison.Ordinal
                ) Then

                    Dim heading As String =
                        CleanInlineMarkdown(
                            trimmed.Substring(3)
                        )

                    output.AppendLine()
                    output.AppendLine(heading)
                    output.AppendLine(
                        New String(
                            "-"c,
                            Math.Min(
                                80,
                                Math.Max(
                                    3,
                                    heading.Length
                                )
                            )
                        )
                    )

                    Continue For

                End If

                If trimmed.StartsWith(
                    "### ",
                    StringComparison.Ordinal
                ) Then

                    output.AppendLine()
                    output.AppendLine(
                        CleanInlineMarkdown(
                            trimmed.Substring(4)
                        )
                    )

                    Continue For

                End If

                If trimmed.StartsWith(
                    "- ",
                    StringComparison.Ordinal
                ) OrElse
                   trimmed.StartsWith(
                       "* ",
                       StringComparison.Ordinal
                   ) Then

                    output.AppendLine(
                        "• " &
                        CleanInlineMarkdown(
                            trimmed.Substring(2)
                        )
                    )

                    Continue For

                End If

                If trimmed.StartsWith(
                    "> ",
                    StringComparison.Ordinal
                ) Then

                    output.AppendLine(
                        CleanInlineMarkdown(
                            trimmed.Substring(2)
                        )
                    )

                    Continue For

                End If

                output.AppendLine(
                    CleanInlineMarkdown(
                        line
                    )
                )

            Next

            Return output.ToString().
                Trim()

        End Function


        Private Shared Function CleanInlineMarkdown(
            value As String
        ) As String

            If value Is Nothing Then
                Return String.Empty
            End If

            Dim result As String =
                value

            result =
                Regex.Replace(
                    result,
                    "\[([^\]]+)\]\(([^)]+)\)",
                    "$1 — $2"
                )

            result =
                result.
                    Replace(
                        "**",
                        String.Empty
                    ).
                    Replace(
                        "__",
                        String.Empty
                    ).
                    Replace(
                        "`",
                        String.Empty
                    )

            Return result

        End Function


        ' The cells of one "| a | b |" row, trimmed and cleaned.
        Private Shared Function TableCells(
            row As String
        ) As List(Of String)

            Dim cells As New List(Of String)()

            For Each cell As String In
                row.Substring(
                    1,
                    row.Length - 2
                ).Split(
                    "|"c
                )

                cells.Add(
                    CleanInlineMarkdown(
                        cell.Trim()
                    )
                )

            Next

            Return cells

        End Function


        ' Shown when the shipped guide can't be read; Friend for the test
        ' that keeps it naming today's pages (#129).
        Friend Shared Function BuiltInFallback() As String

            Return String.Join(
                Environment.NewLine,
                New String() {
                    "# PaperRoute User Guide",
                    String.Empty,
                    "The full local guide file could not be loaded.",
                    String.Empty,
                    "## Quick Start",
                    String.Empty,
                    "1. Add a manuscript from the PaperRoute board.",
                    "2. Open a manuscript from its card to add authors, target journal, metadata, submissions, decisions, and files.",
                    "3. Use Import & Export in the left rail for spreadsheets, BibTeX and RIS, and CV exports; the Library page for reusable authors and affiliations; the Journals page for your journals and their checklists; and Settings > Backup Library and Restore Backup for a portable backup.",
                    "4. Open Deadlines in the left rail (Ctrl+4) for revision deadlines, journal follow-ups, your reminders, and calendar export.",
                    "5. Use Settings > Backup Library before major changes or moving PaperRoute to another computer.",
                    String.Empty,
                    "The complete guide is also available on the PaperRoute GitHub repository."
                }
            )

        End Function

    End Class

End Namespace
