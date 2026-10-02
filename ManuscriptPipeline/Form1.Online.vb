Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Online services and Work offline (#86): the settings reach the gate every
' request passes through, and the rail shows Online in green or Working
' offline in blue.
Partial Public Class Form1

    Private btnOnlineStatus As RailCommandButton = Nothing
    Private mnuWorkOffline As ToolStripMenuItem = Nothing


    ' After settings load or change: the gate, the rail, and the menu.
    Private Sub ApplyOnlineSettings()

        OnlineAccess.Configure(appSettings.OnlineServices)
        OnlineAccess.AssistantUseConfirmed = AddressOf RememberAssistantUse
        RefreshOnlineIndicators()

        ' Your Citations shows whether it can update.
        If currentPage = WorkspacePage.Insights AndAlso tabInsightsCitations IsNot Nothing AndAlso tabInsightsCitations.Checked Then FillInsights()

    End Sub


    Private Sub RefreshOnlineIndicators()

        Dim offline As Boolean = OnlineAccess.IsWorkingOffline

        If mnuWorkOffline IsNot Nothing Then mnuWorkOffline.Checked = offline
        If btnOnlineStatus Is Nothing Then Return

        Dim turnedOff As String = String.Join(", ",
            OnlineServiceCatalog.Services.
                Where(Function(service) Not service.OffUntilTurnedOn AndAlso appSettings.OnlineServices.TurnedOff.Contains(service.Id)).
                Select(Function(service) service.Name))

        Dim assistant As AssistantConnection = OnlineAccess.CurrentAssistant()

        Dim description As String =
            If(offline,
               "Working offline: PaperRoute contacts no online service.",
               "Online: PaperRoute goes online only when you use a feature listed in Online services" &
               If(turnedOff.Length = 0, ".", " (turned off: " & turnedOff & ").") &
               If(assistant Is Nothing, String.Empty, " AI assistant: on (" & assistant.ProviderName & ")."))

        btnOnlineStatus.Glyph = If(offline, RailGlyph.Offline, RailGlyph.Online)
        btnOnlineStatus.Tone = If(offline, RailTone.Info, RailTone.Success)
        btnOnlineStatus.Text = If(offline, "Working offline", "Online")
        btnOnlineStatus.AccessibleName = btnOnlineStatus.Text
        btnOnlineStatus.AccessibleDescription = description & " Opens Online services in Preferences."
        cardToolTip.SetToolTip(btnOnlineStatus, description & " Choose to review Online services.")
        btnOnlineStatus.Invalidate()

    End Sub


    ' Above Settings: Online or Working offline, opening Online services.
    Private Function CreateOnlineStatusButton(width As Integer, height As Integer) As RailCommandButton

        Dim button As New RailCommandButton(RailGlyph.Online, "Online") With {
            .Width = width,
            .Height = height,
            .Tone = RailTone.Success
        }

        AddHandler button.Click, Sub(sender, e) OpenSettingsAt(showOnlineServices:=True)

        btnOnlineStatus = button
        Return button

    End Function


    Private Function CreateWorkOfflineMenuItem() As ToolStripMenuItem

        Dim item As New ToolStripMenuItem("Work Offline") With {
            .CheckOnClick = False,
            .ToolTipText = "Stop every online service, update checks included, until you turn this off."
        }

        AddHandler item.Click, Sub(sender, e) SetWorkOffline(Not OnlineAccess.IsWorkingOffline)

        mnuWorkOffline = item
        Return item

    End Function


    Friend Sub SetWorkOffline(offline As Boolean)

        appSettings.OnlineServices.WorkOffline = offline
        ApplyOnlineSettings()

        Dim saved As Boolean = True
        Try
            settingsService.Save(appSettings)
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
            saved = False
        End Try

        lblStatus.Text =
            If(offline,
               "Working offline. PaperRoute won't contact any online service until you turn Work Offline off.",
               "Online services are available again.") &
            If(saved, String.Empty, " PaperRoute couldn't save this choice, so it lasts until PaperRoute closes.")

    End Sub


    ' The researcher chose not to be asked again before an AI assistant
    ' feature sends to a service (#84): "feature|origin", kept in settings
    ' so the choice lasts until they forget it in Preferences.
    Friend Sub RememberAssistantUse(use As String)

        If String.IsNullOrWhiteSpace(use) Then Return

        If appSettings.OnlineServices Is Nothing Then appSettings.OnlineServices = New OnlineServicesSettings()
        If appSettings.OnlineServices.Assistant Is Nothing Then appSettings.OnlineServices.Assistant = New AssistantSettings()
        If appSettings.OnlineServices.Assistant.ConfirmedUses Is Nothing Then appSettings.OnlineServices.Assistant.ConfirmedUses = New List(Of String)()

        Dim uses As List(Of String) = appSettings.OnlineServices.Assistant.ConfirmedUses
        If uses.Contains(use.Trim(), StringComparer.OrdinalIgnoreCase) Then Return
        uses.Add(use.Trim())

        Try
            settingsService.Save(appSettings)
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
            lblStatus.Text = "PaperRoute couldn't save your choice not to be asked again, so it lasts until PaperRoute closes."
        End Try

    End Sub


    ' Preferences, opened at the AI assistant.
    Friend Sub OpenAssistantSettings()

        OpenSettingsAt(showOnlineServices:=False, showAssistant:=True)

    End Sub

End Class
