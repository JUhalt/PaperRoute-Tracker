Imports System
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services

' Online services and Work offline (#86): the settings reach the gate every
' request passes through, and the rail says when PaperRoute is offline.
Partial Public Class Form1

    Private btnWorkingOffline As RailCommandButton = Nothing
    Private mnuWorkOffline As ToolStripMenuItem = Nothing


    ' After settings load or change: the gate, the rail, and the menu.
    Private Sub ApplyOnlineSettings()

        OnlineAccess.Configure(appSettings.OnlineServices)
        RefreshOnlineIndicators()

    End Sub


    Private Sub RefreshOnlineIndicators()

        Dim offline As Boolean = OnlineAccess.IsWorkingOffline

        If btnWorkingOffline IsNot Nothing Then btnWorkingOffline.Visible = offline
        If mnuWorkOffline IsNot Nothing Then mnuWorkOffline.Checked = offline

    End Sub


    ' Shown above Settings only while Work offline is on.
    Private Function CreateWorkingOfflineButton(width As Integer, height As Integer) As RailCommandButton

        Dim button As New RailCommandButton(RailGlyph.Offline, "Working offline") With {
            .Width = width,
            .Height = height,
            .Attention = True,
            .Visible = False,
            .AccessibleDescription = "Work offline is on. Opens Online services in Preferences."
        }

        AddHandler button.Click, Sub(sender, e) OpenSettingsAt(showOnlineServices:=True)
        cardToolTip.SetToolTip(button, "Work offline is on: PaperRoute contacts no online service. Choose to review Online services.")

        btnWorkingOffline = button
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

End Class
