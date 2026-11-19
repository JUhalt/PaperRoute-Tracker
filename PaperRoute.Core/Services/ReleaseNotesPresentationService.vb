Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services

    Public NotInheritable Class ReleaseNotesPresentationService

        Private Sub New()
        End Sub


        Public Shared Function ToDisplayText(
            markdown As String
        ) As String

            If String.IsNullOrWhiteSpace(markdown) Then
                Return "No release notes were included with this update."
            End If

            Dim normalized As String =
                markdown.Replace(
                    vbCrLf,
                    vbLf
                ).Replace(
                    vbCr,
                    vbLf
                )

            Dim output As New List(Of String)()
            Dim paragraph As New List(Of String)()
            Dim insideFence As Boolean = False

            For Each rawLine As String In normalized.Split(ControlChars.Lf)

                Dim line As String =
                    If(
                        rawLine,
                        String.Empty
                    )

                Dim trimmed As String =
                    line.Trim()

                If trimmed.StartsWith(
                    "```",
                    StringComparison.Ordinal
                ) Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    insideFence =
                        Not insideFence

                    Continue For

                End If

                If insideFence Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    output.Add(
                        "    " & line.TrimEnd()
                    )

                    Continue For

                End If

                If String.IsNullOrWhiteSpace(trimmed) Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    AddBlankLine(
                        output
                    )

                    Continue For

                End If

                Dim headingMatch As Match =
                    Regex.Match(
                        trimmed,
                        "^(#{1,6})\s+(.+)$"
                    )

                If headingMatch.Success Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    If output.Count > 0 AndAlso
                       Not String.IsNullOrEmpty(
                           output(output.Count - 1)
                       ) Then

                        output.Add(
                            String.Empty
                        )

                    End If

                    Dim heading As String =
                        CleanInlineMarkdown(
                            headingMatch.Groups(2).Value
                        )

                    output.Add(
                        heading
                    )

                    output.Add(
                        New String(
                            "─"c,
                            Math.Max(
                                3,
                                Math.Min(
                                    60,
                                    heading.Length
                                )
                            )
                        )
                    )

                    Continue For

                End If

                Dim bulletMatch As Match =
                    Regex.Match(
                        trimmed,
                        "^[-*+]\s+(.+)$"
                    )

                If bulletMatch.Success Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    output.Add(
                        "• " &
                        CleanInlineMarkdown(
                            bulletMatch.Groups(1).Value
                        )
                    )

                    Continue For

                End If

                Dim numberedMatch As Match =
                    Regex.Match(
                        trimmed,
                        "^(\d+)[\.\)]\s+(.+)$"
                    )

                If numberedMatch.Success Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    output.Add(
                        numberedMatch.Groups(1).Value &
                        ". " &
                        CleanInlineMarkdown(
                            numberedMatch.Groups(2).Value
                        )
                    )

                    Continue For

                End If

                If trimmed.StartsWith(
                    ">",
                    StringComparison.Ordinal
                ) Then

                    FlushParagraph(
                        output,
                        paragraph
                    )

                    output.Add(
                        "› " &
                        CleanInlineMarkdown(
                            trimmed.Substring(1).Trim()
                        )
                    )

                    Continue For

                End If

                paragraph.Add(
                    CleanInlineMarkdown(
                        trimmed
                    )
                )

            Next

            FlushParagraph(
                output,
                paragraph
            )

            While output.Count > 0 AndAlso
                  String.IsNullOrEmpty(
                      output(output.Count - 1)
                  )

                output.RemoveAt(
                    output.Count - 1
                )

            End While

            If output.Count = 0 Then
                Return "No release notes were included with this update."
            End If

            Return String.Join(
                Environment.NewLine,
                output
            )

        End Function


        Private Shared Sub FlushParagraph(
            output As List(Of String),
            paragraph As List(Of String)
        )

            If paragraph.Count = 0 Then
                Return
            End If

            output.Add(
                String.Join(
                    " ",
                    paragraph
                )
            )

            paragraph.Clear()

        End Sub


        Private Shared Sub AddBlankLine(
            output As List(Of String)
        )

            If output.Count = 0 OrElse
               String.IsNullOrEmpty(
                   output(output.Count - 1)
               ) Then

                Return

            End If

            output.Add(
                String.Empty
            )

        End Sub


        Private Shared Function CleanInlineMarkdown(
            value As String
        ) As String

            Dim result As String =
                If(
                    value,
                    String.Empty
                ).Trim()

            result =
                Regex.Replace(
                    result,
                    "\[([^\]]+)\]\(([^)]+)\)",
                    "$1 ($2)"
                )

            result =
                result.Replace(
                    "**",
                    String.Empty
                ).Replace(
                    "__",
                    String.Empty
                ).Replace(
                    "`",
                    String.Empty
                )

            Return result.Trim()

        End Function

    End Class

End Namespace
