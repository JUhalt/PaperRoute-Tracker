Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Calendar dates do not move when the computer's time zone changes (#118):
' a date the library file says is 9 October is 9 October on any machine,
' and the ...Utc audit stamps stay instants.
<TestClass>
Public Class CalendarDateTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedLibrary = Path.Combine(_root, "managed")

        Directory.CreateDirectory(_dataDirectory)

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' The issue's test: 2026-10-09T00:00:00+14:00 and 2026-10-09T00:00:00-12:00
    ' read through the repository are 9 October on any host. One of the two
    ' is the day before or after under the default reader unless the host
    ' happens to sit at that offset, so this fails somewhere for everyone.
    <TestMethod>
    Public Sub Load_ReadsCalendarDatesAsWrittenWhateverTheOffset()

        File.WriteAllText(
            Path.Combine(_dataDirectory, "manuscripts.json"),
            "[" & vbLf &
            "  {" & vbLf &
            "    ""Title"": ""Zones""," & vbLf &
            "    ""StageEnteredDate"": ""2026-10-09T00:00:00+14:00""," & vbLf &
            "    ""FileDrawerDate"": ""2026-10-09T00:00:00-12:00""," & vbLf &
            "    ""Reminders"": [" & vbLf &
            "      { ""Title"": ""Check"", ""DueDate"": ""2026-10-09T00:00:00+14:00"" }" & vbLf &
            "    ]," & vbLf &
            "    ""Submissions"": [" & vbLf &
            "      {" & vbLf &
            "        ""JournalName"": ""Journal of Zones""," & vbLf &
            "        ""SubmittedDate"": ""2026-10-09T00:00:00+14:00""," & vbLf &
            "        ""FollowUpDate"": ""2026-10-09T00:00:00-12:00""," & vbLf &
            "        ""RecordedAtUtc"": ""2026-10-08T10:00:00Z""," & vbLf &
            "        ""Decisions"": [" & vbLf &
            "          { ""DecisionDate"": ""2026-10-09T00:00:00-12:00"", ""Decision"": ""Rejected"" }" & vbLf &
            "        ]" & vbLf &
            "      }" & vbLf &
            "    ]" & vbLf &
            "  }" & vbLf &
            "]"
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Dim manuscript As Manuscript = loaded(0)
        Dim submission As JournalSubmission = manuscript.Submissions(0)
        Dim ninth As New DateTime(2026, 10, 9)

        Assert.AreEqual(ninth, manuscript.StageEnteredDate.Date, "StageEnteredDate written at +14:00")
        Assert.AreEqual(ninth, manuscript.FileDrawerDate.Value.Date, "FileDrawerDate written at -12:00")
        Assert.AreEqual(ninth, manuscript.Reminders(0).DueDate, "DueDate written at +14:00, cut to the day on load")
        Assert.AreEqual(ninth, submission.SubmittedDate.Date, "SubmittedDate written at +14:00")
        Assert.AreEqual(ninth, submission.FollowUpDate.Value, "FollowUpDate written at -12:00, cut to the day on load")
        Assert.AreEqual(ninth, submission.Decisions(0).DecisionDate.Date, "DecisionDate written at -12:00")

        ' The audit stamp is an instant and stays one.
        Assert.AreEqual(
            New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
            submission.RecordedAtUtc.Value
        )

        Assert.AreEqual(
            DateTimeKind.Utc,
            submission.RecordedAtUtc.Value.Kind
        )

    End Sub


    ' A save writes a calendar date without an offset, whatever kind the
    ' value in memory has, and an audit stamp in UTC as before.
    <TestMethod>
    Public Sub Save_WritesCalendarDatesWithoutAnOffsetAndAuditStampsInUtc()

        Dim manuscript As New Manuscript With {
            .Title = "Zones",
            .StageEnteredDate = DateTime.SpecifyKind(New DateTime(2026, 10, 9, 8, 30, 0), DateTimeKind.Local)
        }

        manuscript.Submissions.Add(
            New JournalSubmission With {
                .JournalName = "Journal of Zones",
                .SubmittedDate = New DateTime(2026, 10, 9),
                .RecordedAtUtc = New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc)
            }
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        repository.Save(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Dim json As String =
            File.ReadAllText(
                repository.DataFilePath
            )

        StringAssert.Contains(json, """StageEnteredDate"": ""2026-10-09T08:30:00""", "A local-kind value is written as its wall-clock date and time, with no offset.")
        StringAssert.Contains(json, """SubmittedDate"": ""2026-10-09T00:00:00""", "An unspecified-kind value is written as before.")
        StringAssert.Contains(json, """RecordedAtUtc"": ""2026-10-08T10:00:00Z""", "An audit stamp is written in UTC as before.")

        ' And reads back to the same day with the same instant.
        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(New DateTime(2026, 10, 9, 8, 30, 0), loaded(0).StageEnteredDate)
        Assert.AreEqual(New DateTime(2026, 10, 9), loaded(0).Submissions(0).SubmittedDate)
        Assert.AreEqual(New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc), loaded(0).Submissions(0).RecordedAtUtc.Value)

    End Sub


    ' The recovery draft (#123) holds a manuscript too, read at the next
    ' start, perhaps in another zone: it is written without offsets, and a
    ' date written at any offset reads as the day written. The WrittenAtUtc
    ' stamp stays an instant. Before the change the draft was written with
    ' the machine's offset, so this failed on every host.
    <TestMethod>
    Public Sub RecoveryDraft_KeepsCalendarDatesAsWritten()

        Dim store As New RecoveryDraftStore(
            _dataDirectory
        )

        Dim manuscript As New Manuscript With {
            .Title = "Zones",
            .StageEnteredDate = DateTime.SpecifyKind(New DateTime(2026, 10, 9, 0, 30, 0), DateTimeKind.Local)
        }

        store.Write(
            New RecoveryDraft With {
                .ManuscriptId = manuscript.Id,
                .WrittenAtUtc = New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
                .Reason = "test",
                .Manuscript = manuscript
            }
        )

        Dim draftPath As String =
            Path.Combine(
                _dataDirectory,
                "recovery",
                RecoveryDraftStore.FileName
            )

        Dim json As String =
            File.ReadAllText(
                draftPath
            )

        StringAssert.Contains(json, """StageEnteredDate"": ""2026-10-09T00:30:00""", "A local-kind value is written as its wall-clock date and time, with no offset.")
        StringAssert.Contains(json, """WrittenAtUtc"": ""2026-10-08T10:00:00Z""", "The stamp is written in UTC as before.")

        ' A draft written in a zone fourteen hours ahead.
        File.WriteAllText(
            draftPath,
            json.Replace(
                """StageEnteredDate"": ""2026-10-09T00:30:00""",
                """StageEnteredDate"": ""2026-10-09T00:30:00+14:00"""
            )
        )

        Dim read As RecoveryDraft =
            store.Read()

        Assert.AreEqual(New DateTime(2026, 10, 9, 0, 30, 0), read.Manuscript.StageEnteredDate, "The day written, whatever the offset.")
        Assert.AreEqual(New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc), read.WrittenAtUtc)
        Assert.AreEqual(DateTimeKind.Utc, read.WrittenAtUtc.Kind)

    End Sub

End Class
