Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class IcsCalendarService

        Private Const CrLf As String = vbCrLf


        Private Sub New()
        End Sub


        Public Shared Function Export(
            occurrences As IEnumerable(Of ReminderOccurrence)
        ) As String

            Return Export(
                occurrences,
                DateTime.UtcNow
            )

        End Function


        Public Shared Function Export(
            occurrences As IEnumerable(Of ReminderOccurrence),
            generatedAtUtc As DateTime
        ) As String

            If occurrences Is Nothing Then
                Throw New ArgumentNullException(NameOf(occurrences))
            End If

            Dim generatedUtc As DateTime =
                NormalizeUtc(
                    generatedAtUtc
                )

            Dim items As List(Of ReminderOccurrence) =
                occurrences.
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    OrderBy(
                        Function(item)
                            Return item.DueDate.Date
                        End Function
                    ).
                    ThenBy(
                        Function(item)
                            Return item.ManuscriptTitle
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    ).
                    ToList()

            Dim builder As New StringBuilder()

            AppendLine(builder, "BEGIN:VCALENDAR")
            AppendLine(builder, "VERSION:2.0")
            AppendLine(builder, "PRODID:-//PaperRoute//Reminder Calendar//EN")
            AppendLine(builder, "CALSCALE:GREGORIAN")
            AppendLine(builder, "METHOD:PUBLISH")
            AppendLine(builder, "X-WR-CALNAME:PaperRoute Reminders")

            For Each item As ReminderOccurrence In items

                AppendLine(builder, "BEGIN:VEVENT")

                AppendLine(
                    builder,
                    "UID:" &
                    CreateUid(
                        item
                    )
                )

                AppendLine(
                    builder,
                    "DTSTAMP:" &
                    generatedUtc.ToString(
                        "yyyyMMdd'T'HHmmss'Z'"
                    )
                )

                AppendLine(
                    builder,
                    "DTSTART;VALUE=DATE:" &
                    item.DueDate.Date.ToString(
                        "yyyyMMdd"
                    )
                )

                AppendLine(
                    builder,
                    "SUMMARY:" &
                    EscapeText(
                        CreateSummary(
                            item
                        )
                    )
                )

                AppendLine(
                    builder,
                    "DESCRIPTION:" &
                    EscapeText(
                        CreateDescription(
                            item
                        )
                    )
                )

                AppendLine(
                    builder,
                    "CATEGORIES:PaperRoute"
                )

                AppendLine(builder, "END:VEVENT")

            Next

            AppendLine(builder, "END:VCALENDAR")

            Return builder.ToString()

        End Function


        Public Shared Function EscapeText(
            value As String
        ) As String

            If value Is Nothing Then
                Return String.Empty
            End If

            Return value.
                Replace(
                    "\",
                    "\\"
                ).
                Replace(
                    vbCrLf,
                    "\n"
                ).
                Replace(
                    vbCr,
                    "\n"
                ).
                Replace(
                    vbLf,
                    "\n"
                ).
                Replace(
                    ";",
                    "\;"
                ).
                Replace(
                    ",",
                    "\,"
                )

        End Function


        Private Shared Function CreateUid(
            item As ReminderOccurrence
        ) As String

            Dim kindText As String =
                item.Kind.ToString().
                    ToLowerInvariant()

            Return "paperroute-" &
                kindText &
                "-" &
                item.SourceId.ToString("N") &
                "@paperroute.local"

        End Function


        Private Shared Function CreateSummary(
            item As ReminderOccurrence
        ) As String

            Dim title As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.Title
                    ),
                    "Reminder",
                    item.Title.Trim()
                )

            Dim manuscriptTitle As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.ManuscriptTitle
                    ),
                    "(Untitled manuscript)",
                    item.ManuscriptTitle.Trim()
                )

            Return title &
                " - " &
                manuscriptTitle

        End Function


        Private Shared Function CreateDescription(
            item As ReminderOccurrence
        ) As String

            Dim parts As New List(Of String) From {
                "Manuscript: " &
                    If(
                        item.ManuscriptTitle,
                        String.Empty
                    ),
                "Reminder type: " &
                    item.KindLabel
            }

            If Not String.IsNullOrWhiteSpace(
                item.JournalName
            ) Then

                parts.Add(
                    "Journal: " &
                    item.JournalName.Trim()
                )

            End If

            If Not String.IsNullOrWhiteSpace(
                item.Notes
            ) Then

                parts.Add(
                    "Notes: " &
                    item.Notes.Trim()
                )

            End If

            parts.Add(
                "Created by PaperRoute."
            )

            Return String.Join(
                Environment.NewLine,
                parts
            )

        End Function


        Private Shared Function NormalizeUtc(
            value As DateTime
        ) As DateTime

            If value.Kind = DateTimeKind.Utc Then
                Return value
            End If

            If value.Kind = DateTimeKind.Unspecified Then

                Return DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc
                )

            End If

            Return value.ToUniversalTime()

        End Function


        Private Shared Sub AppendLine(
            builder As StringBuilder,
            value As String
        )

            builder.Append(value)
            builder.Append(CrLf)

        End Sub

    End Class

End Namespace
