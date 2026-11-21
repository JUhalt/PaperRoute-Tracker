Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class IcsCalendarServiceTests

    <TestMethod>
    Public Sub Export_EmptyList_ProducesValidCalendar()

        Dim output As String =
            IcsCalendarService.Export(
                New List(Of ReminderOccurrence)(),
                DateTime.SpecifyKind(
                    New DateTime(2026, 8, 21, 12, 0, 0),
                    DateTimeKind.Utc
                )
            )

        StringAssert.Contains(output, "BEGIN:VCALENDAR")
        StringAssert.Contains(output, "END:VCALENDAR")
        Assert.IsFalse(
            output.Contains(
                "BEGIN:VEVENT",
                StringComparison.Ordinal
            )
        )

    End Sub


    <TestMethod>
    Public Sub Export_Reminder_UsesAllDayDateAndStableUid()

        Dim sourceId As Guid =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555"
            )

        Dim item As New ReminderOccurrence With {
            .SourceId = sourceId,
            .ManuscriptId = Guid.NewGuid(),
            .ManuscriptTitle = "Paper",
            .Kind = ReminderKind.Custom,
            .DueDate = New DateTime(2026, 8, 25),
            .Title = "Check analysis"
        }

        Dim output As String =
            IcsCalendarService.Export(
                New List(Of ReminderOccurrence) From {
                    item
                },
                DateTime.SpecifyKind(
                    New DateTime(2026, 8, 21, 12, 0, 0),
                    DateTimeKind.Utc
                )
            )

        StringAssert.Contains(
            output,
            "DTSTART;VALUE=DATE:20260825"
        )

        StringAssert.Contains(
            output,
            "UID:paperroute-custom-11111111222233334444555555555555@paperroute.local"
        )

    End Sub


    <TestMethod>
    Public Sub Export_UsesDeterministicDtstampWhenProvided()

        Dim output As String =
            IcsCalendarService.Export(
                New List(Of ReminderOccurrence) From {
                    New ReminderOccurrence With {
                        .SourceId = Guid.NewGuid(),
                        .ManuscriptId = Guid.NewGuid(),
                        .ManuscriptTitle = "Paper",
                        .Kind = ReminderKind.Custom,
                        .DueDate = New DateTime(2026, 8, 25),
                        .Title = "Reminder"
                    }
                },
                DateTime.SpecifyKind(
                    New DateTime(2026, 8, 21, 14, 15, 16),
                    DateTimeKind.Utc
                )
            )

        StringAssert.Contains(
            output,
            "DTSTAMP:20260821T141516Z"
        )

    End Sub


    <TestMethod>
    Public Sub EscapeText_EscapesIcalendarSpecialCharacters()

        Dim escaped As String =
            IcsCalendarService.EscapeText(
                "One, two; path\file" &
                Environment.NewLine &
                "next"
            )

        Assert.AreEqual(
            "One\, two\; path\\file\nnext",
            escaped
        )

    End Sub


    <TestMethod>
    Public Sub Export_IncludesUsefulDescriptionMetadata()

        Dim item As New ReminderOccurrence With {
            .SourceId = Guid.NewGuid(),
            .ManuscriptId = Guid.NewGuid(),
            .ManuscriptTitle = "A Study",
            .Kind = ReminderKind.SubmissionFollowUp,
            .DueDate = New DateTime(2026, 9, 1),
            .Title = "Submission follow-up",
            .JournalName = "Journal of Examples",
            .Notes = "Check the portal."
        }

        Dim output As String =
            IcsCalendarService.Export(
                New List(Of ReminderOccurrence) From {
                    item
                },
                DateTime.SpecifyKind(
                    New DateTime(2026, 8, 21),
                    DateTimeKind.Utc
                )
            )

        StringAssert.Contains(
            output,
            "Manuscript: A Study"
        )

        StringAssert.Contains(
            output,
            "Journal: Journal of Examples"
        )

        StringAssert.Contains(
            output,
            "Notes: Check the portal."
        )

    End Sub


    <TestMethod>
    Public Sub Export_DoesNotMutateReminderOccurrence()

        Dim originalDate As New DateTime(2026, 8, 25)

        Dim item As New ReminderOccurrence With {
            .SourceId = Guid.NewGuid(),
            .ManuscriptId = Guid.NewGuid(),
            .ManuscriptTitle = "Original",
            .Kind = ReminderKind.Custom,
            .DueDate = originalDate,
            .Title = "Original title"
        }

        IcsCalendarService.Export(
            New List(Of ReminderOccurrence) From {
                item
            },
            DateTime.SpecifyKind(
                New DateTime(2026, 8, 21),
                DateTimeKind.Utc
            )
        )

        Assert.AreEqual(
            originalDate,
            item.DueDate
        )

        Assert.AreEqual(
            "Original title",
            item.Title
        )

        Assert.AreEqual(
            "Original",
            item.ManuscriptTitle
        )

    End Sub

End Class
