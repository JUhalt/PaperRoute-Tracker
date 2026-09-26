Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services

    Public NotInheritable Class UserGuideService

        Public Const GitHubGuideUrl As String =
            "https://github.com/JUhalt/PaperRoute-Tracker/blob/master/docs/USER_GUIDE.md"


        Private Sub New()
        End Sub


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


        Private Shared Function BuiltInFallback() As String

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
                    "2. Open Manuscript Details to add authors, target journal, metadata, submissions, decisions, and files.",
                    "3. Use Data for import/export, reusable authors and journals, bibliography interchange, CV exports, backup, and restore.",
                    "4. Open Reminders in the left rail for revision deadlines, journal follow-ups, custom reminders, and calendar export.",
                    "5. Use Settings > Backup Library before major changes or moving PaperRoute to another computer.",
                    String.Empty,
                    "The complete guide is also available on the PaperRoute GitHub repository."
                }
            )

        End Function

    End Class

End Namespace
