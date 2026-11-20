Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ReminderPersistenceTests

    <TestMethod>
    Public Sub ManuscriptRepository_RoundTripsCustomReminderAndFollowUpDate()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim dataDirectory As String =
                Path.Combine(
                    root,
                    "data"
                )

            Dim managedDirectory As String =
                Path.Combine(
                    root,
                    "managed"
                )

            Directory.CreateDirectory(
                dataDirectory
            )

            Directory.CreateDirectory(
                managedDirectory
            )

            Dim repository As New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            )

            Dim manuscript As New Manuscript With {
                .Title = "Reminder Persistence"
            }

            manuscript.Reminders.Add(
                New ManuscriptReminder With {
                    .Title = "Custom reminder",
                    .DueDate = New DateTime(2026, 8, 30),
                    .Notes = "Remember this."
                }
            )

            manuscript.Submissions.Add(
                New JournalSubmission With {
                    .JournalName = "Journal",
                    .FollowUpDate = New DateTime(2026, 9, 15)
                }
            )

            repository.Save(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            Dim loaded As List(Of Manuscript) =
                repository.Load()

            Assert.AreEqual(1, loaded.Count)
            Assert.AreEqual(1, loaded(0).Reminders.Count)
            Assert.AreEqual(
                "Custom reminder",
                loaded(0).Reminders(0).Title
            )
            Assert.AreEqual(
                New DateTime(2026, 8, 30),
                loaded(0).Reminders(0).DueDate.Date
            )
            Assert.AreEqual(
                New DateTime(2026, 9, 15),
                loaded(0).Submissions(0).FollowUpDate.Value.Date
            )

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub ManuscriptRepository_RejectsDuplicateReminderIdsOnLoad()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim dataDirectory As String =
                Path.Combine(
                    root,
                    "data"
                )

            Dim managedDirectory As String =
                Path.Combine(
                    root,
                    "managed"
                )

            Directory.CreateDirectory(
                dataDirectory
            )

            Directory.CreateDirectory(
                managedDirectory
            )

            Dim repository As New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            )

            Dim sharedId As Guid =
                Guid.NewGuid()

            Dim manuscript As New Manuscript With {
                .Title = "Duplicate Reminder IDs"
            }

            manuscript.Reminders.Add(
                New ManuscriptReminder With {
                    .Id = sharedId,
                    .Title = "One",
                    .DueDate = DateTime.Today
                }
            )

            manuscript.Reminders.Add(
                New ManuscriptReminder With {
                    .Id = sharedId,
                    .Title = "Two",
                    .DueDate = DateTime.Today.AddDays(1)
                }
            )

            repository.Save(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            Assert.ThrowsExactly(Of InvalidDataException)(
                Sub()
                    repository.Load()
                End Sub
            )

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub CloneManuscript_DeepCopiesRemindersAndFollowUpDate()

        Dim source As New Manuscript With {
            .Title = "Clone Reminder Paper"
        }

        source.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Reminder",
                .DueDate = New DateTime(2026, 8, 30),
                .Notes = "Notes"
            }
        )

        source.Submissions.Add(
            New JournalSubmission With {
                .JournalName = "Journal",
                .FollowUpDate = New DateTime(2026, 9, 15)
            }
        )

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                source
            )

        Assert.AreEqual(1, clone.Reminders.Count)
        Assert.AreNotSame(
            source.Reminders(0),
            clone.Reminders(0)
        )
        Assert.AreEqual(
            source.Reminders(0).Id,
            clone.Reminders(0).Id
        )
        Assert.AreEqual(
            New DateTime(2026, 9, 15),
            clone.Submissions(0).FollowUpDate.Value.Date
        )

        clone.Reminders(0).Title =
            "Changed clone"

        Assert.AreEqual(
            "Reminder",
            source.Reminders(0).Title
        )

    End Sub


    Private Shared Function CreateTempDirectory() As String

        Dim tempPath As String =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PaperRouteReminderTests_" &
                Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(
            tempPath
        )

        Return tempPath

    End Function


    Private Shared Sub DeleteTempDirectory(
        path As String
    )

        If String.IsNullOrWhiteSpace(path) OrElse
           Not Directory.Exists(path) Then

            Return

        End If

        Try

            Directory.Delete(
                path,
                True
            )

        Catch
        End Try

    End Sub

End Class
