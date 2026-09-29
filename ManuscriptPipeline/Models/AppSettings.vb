Imports System

Namespace Models

    Public Class AppSettings

        Public Property Appearance As AppAppearance =
            AppAppearance.System

        Public Property FileDrawerSuggestionThreshold As Integer =
            3

        Public Property LongReviewThresholdDays As Integer =
            90

        Public Property RevisionWarningDays As Integer =
            14

        Public Property RecentRejectionThresholdDays As Integer =
            30

        Public Property ReminderNotificationsEnabled As Boolean =
            False

        Public Property ReminderNotificationDaysAhead As Integer =
            3

        Public Property LastReminderNotificationDate As DateTime? =
            Nothing

        ' Fresh stable installations default to Stable.
        ' Existing users keep whichever channel is already persisted
        ' in settings.json, including Preview.
        Public Property UpdateChannel As AppUpdateChannel =
            AppUpdateChannel.Stable

        Public Property CheckForUpdatesAutomatically As Boolean =
            True

        ' Online services and Work offline (#86).
        Public Property OnlineServices As OnlineServicesSettings =
            New OnlineServicesSettings()

        ' The left rail shows only icons.
        Public Property NavigationCollapsed As Boolean =
            False

    End Class

End Namespace
