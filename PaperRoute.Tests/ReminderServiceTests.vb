Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ReminderServiceTests

    <TestMethod>
    Public Sub BuildOccurrences_RevisionDeadline_Overdue()

        Dim decision As New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MajorRevision,
            .DecisionDate = New DateTime(2026, 8, 1),
            .RevisionDeadline = New DateTime(2026, 8, 20)
        }

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal of Examples",
            .SubmittedDate = New DateTime(2026, 7, 1)
        }

        submission.Decisions.Add(
            decision
        )

        Dim manuscript As New Manuscript With {
            .Title = "Revision Paper",
            .CurrentStage = PaperStage.Revision
        }

        manuscript.Submissions.Add(
            submission
        )

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual(1, items.Count)
        Assert.AreEqual(ReminderKind.RevisionDeadline, items(0).Kind)
        Assert.AreEqual(ReminderStatus.Overdue, items(0).Status)
        Assert.AreEqual(decision.Id, items(0).SourceId)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_RevisionDeadline_IsIgnoredOutsideRevisionStage()

        Dim manuscript As New Manuscript With {
            .Title = "Accepted Paper",
            .CurrentStage = PaperStage.Accepted,
            .RevisionDeadline = New DateTime(2026, 8, 20)
        }

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual(0, items.Count)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_SubmissionFollowUp_IsIncludedWithoutDecision()

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal of Examples",
            .FollowUpDate = New DateTime(2026, 8, 25)
        }

        Dim manuscript As New Manuscript With {
            .Title = "Submitted Paper"
        }

        manuscript.Submissions.Add(submission)

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual(1, items.Count)
        Assert.AreEqual(ReminderKind.SubmissionFollowUp, items(0).Kind)
        Assert.AreEqual(ReminderStatus.Upcoming, items(0).Status)
        Assert.AreEqual(submission.Id, items(0).SubmissionId.Value)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_ExplicitSubmissionFollowUp_RemainsAfterDecision()

        Dim submission As New JournalSubmission With {
            .JournalName = "Journal of Examples",
            .FollowUpDate = New DateTime(2026, 8, 25)
        }

        submission.Decisions.Add(
            New EditorialDecisionEvent()
        )

        Dim manuscript As New Manuscript With {
            .Title = "Decided Paper"
        }

        manuscript.Submissions.Add(submission)

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual(1, items.Count)
        Assert.AreEqual(ReminderKind.SubmissionFollowUp, items(0).Kind)
        Assert.AreEqual(submission.Id, items(0).SourceId)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_CustomReminder_DueToday()

        Dim reminder As New ManuscriptReminder With {
            .Title = "Email coauthor",
            .DueDate = New DateTime(2026, 8, 21)
        }

        Dim manuscript As New Manuscript With {
            .Title = "Custom Reminder Paper"
        }

        manuscript.Reminders.Add(reminder)

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21, 18, 0, 0)
            )

        Assert.AreEqual(1, items.Count)
        Assert.AreEqual(ReminderKind.Custom, items(0).Kind)
        Assert.AreEqual(ReminderStatus.DueToday, items(0).Status)
        Assert.IsTrue(items(0).IsEditableReminder)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_CompletedCustomReminder_IsExcluded()

        Dim reminder As New ManuscriptReminder With {
            .Title = "Done",
            .DueDate = New DateTime(2026, 8, 20),
            .IsCompleted = True,
            .CompletedDate = New DateTime(2026, 8, 20)
        }

        Dim manuscript As New Manuscript With {
            .Title = "Completed Reminder Paper"
        }

        manuscript.Reminders.Add(reminder)

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual(0, items.Count)

    End Sub


    <TestMethod>
    Public Sub BuildOccurrences_AreSortedByDueDate()

        Dim manuscript As New Manuscript With {
            .Title = "Sorting Paper"
        }

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Later",
                .DueDate = New DateTime(2026, 8, 30)
            }
        )

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Earlier",
                .DueDate = New DateTime(2026, 8, 22)
            }
        )

        Dim items =
            ReminderService.BuildOccurrences(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21)
            )

        Assert.AreEqual("Earlier", items(0).Title)
        Assert.AreEqual("Later", items(1).Title)

    End Sub


    <TestMethod>
    Public Sub NotificationCandidates_IncludeOverdueAndConfiguredFutureWindow()

        Dim manuscript As New Manuscript With {
            .Title = "Notification Paper"
        }

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Overdue",
                .DueDate = New DateTime(2026, 8, 20)
            }
        )

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Soon",
                .DueDate = New DateTime(2026, 8, 24)
            }
        )

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Later",
                .DueDate = New DateTime(2026, 8, 25)
            }
        )

        Dim items =
            ReminderService.NotificationCandidates(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21),
                3
            )

        Assert.AreEqual(2, items.Count)
        Assert.AreEqual("Overdue", items(0).Title)
        Assert.AreEqual("Soon", items(1).Title)

    End Sub


    <TestMethod>
    Public Sub NotificationCandidates_NegativeDaysAhead_IsTreatedAsZero()

        Dim manuscript As New Manuscript With {
            .Title = "Notification Paper"
        }

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Today",
                .DueDate = New DateTime(2026, 8, 21)
            }
        )

        manuscript.Reminders.Add(
            New ManuscriptReminder With {
                .Title = "Tomorrow",
                .DueDate = New DateTime(2026, 8, 22)
            }
        )

        Dim items =
            ReminderService.NotificationCandidates(
                New List(Of Manuscript) From {
                    manuscript
                },
                New DateTime(2026, 8, 21),
                -3
            )

        Assert.AreEqual(1, items.Count)
        Assert.AreEqual("Today", items(0).Title)

    End Sub


    <TestMethod>
    Public Sub CreateNotificationSummary_ReportsStatusCounts()

        Dim items As New List(Of ReminderOccurrence) From {
            New ReminderOccurrence With {
                .Status = ReminderStatus.Overdue
            },
            New ReminderOccurrence With {
                .Status = ReminderStatus.DueToday
            },
            New ReminderOccurrence With {
                .Status = ReminderStatus.Upcoming
            }
        }

        Dim summary As String =
            ReminderService.CreateNotificationSummary(
                items,
                3
            )

        StringAssert.Contains(summary, "1 overdue")
        StringAssert.Contains(summary, "1 due today")
        StringAssert.Contains(summary, "1 upcoming within 3 days")

        ' A one-day window is one day, not "1 day(s)" (#245).
        StringAssert.Contains(
            ReminderService.CreateNotificationSummary(
                items,
                1
            ),
            "1 upcoming within 1 day."
        )

    End Sub


    <TestMethod>
    Public Sub AppSettings_ReminderNotifications_DefaultToOptInOff()

        Dim settings As New AppSettings()

        Assert.IsFalse(
            settings.ReminderNotificationsEnabled
        )

        Assert.AreEqual(
            3,
            settings.ReminderNotificationDaysAhead
        )

    End Sub


    <TestMethod>
    Public Sub WindowsNotification_BlankMessage_FailsGracefully()

        Using service As New WindowsNotificationService()

            Assert.IsFalse(
                service.TryShow(
                    "PaperRoute",
                    String.Empty
                )
            )

        End Using

    End Sub

End Class
