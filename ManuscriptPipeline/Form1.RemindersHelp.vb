Imports System
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Partial Public Class Form1

    Private ReadOnly reminderNotificationService As New WindowsNotificationService()

    Private reminderNotificationCleanupInstalled As Boolean = False


    Private Sub OpenUserGuide(
        sender As Object,
        e As EventArgs
    )

        Using dialog As New HelpForm()

            dialog.ShowDialog(
                Me
            )

        End Using

    End Sub


    Private Sub TryShowStartupReminderNotification()

        InstallReminderNotificationCleanup()

        If appSettings Is Nothing OrElse
           Not appSettings.ReminderNotificationsEnabled Then

            Return

        End If

        If appSettings.LastReminderNotificationDate.HasValue AndAlso
           appSettings.LastReminderNotificationDate.Value.Date =
               DateTime.Today Then

            Return

        End If

        Dim candidates =
            ReminderService.NotificationCandidates(
                manuscripts,
                DateTime.Today,
                appSettings.ReminderNotificationDaysAhead
            )

        If candidates.Count = 0 Then
            Return
        End If

        Dim summary As String =
            ReminderService.CreateNotificationSummary(
                candidates,
                appSettings.ReminderNotificationDaysAhead
            )

        Dim shown As Boolean =
            reminderNotificationService.TryShow(
                "PaperRoute reminders",
                summary
            )

        If Not shown Then
            Return
        End If

        appSettings.LastReminderNotificationDate =
            DateTime.Today

        Try

            settingsService.Save(
                appSettings
            )

        Catch
            ' Reminder persistence must never block normal PaperRoute use.
        End Try

    End Sub


    Private Sub InstallReminderNotificationCleanup()

        If reminderNotificationCleanupInstalled Then
            Return
        End If

        reminderNotificationCleanupInstalled =
            True

        AddHandler Me.FormClosed,
            Sub(sender, e)

                reminderNotificationService.Dispose()

            End Sub

    End Sub

End Class
