Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports Velopack
Imports Velopack.Sources

Namespace Services

    Public NotInheritable Class UpdateService

        Private Const RepositoryUrl As String =
            "https://github.com/JUhalt/PaperRoute-Tracker"


        Private Sub New()
        End Sub


        Public Shared Function CurrentVersionText() As String

            Dim version As String =
                Application.ProductVersion

            Dim metadataIndex As Integer =
                version.IndexOf("+"c)

            If metadataIndex >= 0 Then
                version = version.Substring(0, metadataIndex)
            End If

            Return version

        End Function


        Public Shared Function IsInstalledBuild(
            channel As AppUpdateChannel
        ) As Boolean

            Try

                Dim manager As UpdateManager =
                    CreateManager(channel)

                Return manager.IsInstalled

            Catch

                Return False

            End Try

        End Function


        Public Shared Function ChannelDisplayName(
            channel As AppUpdateChannel
        ) As String

            If channel = AppUpdateChannel.Preview Then
                Return "Preview"
            End If

            Return "Stable"

        End Function


        Public Shared Async Function CheckAndOfferUpdateAsync(
            owner As IWin32Window,
            channel As AppUpdateChannel,
            interactive As Boolean
        ) As Task(Of Boolean)

            ' Work offline, or the update check switched off (#86): nothing is
            ' contacted, and only a check the user asked for explains why.
            Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.Updates)
            If blocked.HasValue Then
                If interactive Then
                    MessageBox.Show(
                        owner,
                        OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.Updates), blocked.Value),
                        "PaperRoute Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )
                End If
                Return False
            End If

            Try

                Dim manager As UpdateManager =
                    CreateManager(channel)

                If Not manager.IsInstalled Then

                    If interactive Then

                        MessageBox.Show(
                            owner,
                            "Update checking is available in installed PaperRoute builds." &
                            Environment.NewLine & Environment.NewLine &
                            "This copy appears to be running from Visual Studio or as a portable build. " &
                            "Install PaperRoute using the Setup program from a GitHub release to test automatic updates.",
                            "Updates Unavailable",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        )

                    End If

                    Return False

                End If


                Dim updateInfo As UpdateInfo =
                    Await manager.CheckForUpdatesAsync()

                If updateInfo Is Nothing Then

                    If interactive Then

                        MessageBox.Show(
                            owner,
                            "You're up to date." &
                            Environment.NewLine & Environment.NewLine &
                            "Current version: " & CurrentInstalledVersion(manager) &
                            Environment.NewLine &
                            "Channel: " & ChannelDisplayName(channel),
                            "PaperRoute Updates",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        )

                    End If

                    Return False

                End If


                Dim availableVersion As String =
                    updateInfo.TargetFullRelease.Version.ToString()

                Dim releaseNotes As String =
                    updateInfo.TargetFullRelease.NotesMarkdown

                If String.IsNullOrWhiteSpace(releaseNotes) Then
                    releaseNotes = "No release notes were included with this update."
                End If


                Using prompt As New UpdatePromptForm(
                    CurrentInstalledVersion(manager),
                    availableVersion,
                    ChannelDisplayName(channel),
                    releaseNotes
                )

                    If prompt.ShowDialog(owner) <> DialogResult.OK Then
                        Return False
                    End If

                End Using


                Using progressDialog As New UpdateProgressForm(
                    availableVersion
                )

                    progressDialog.Show(owner)
                    progressDialog.SetProgress(0)
                    progressDialog.SetStatus("Downloading update...")

                    Dim progressCallback As New Action(Of Integer)(
                        Sub(value As Integer)
                            progressDialog.SetProgress(value)
                        End Sub
                    )

                    Await manager.DownloadUpdatesAsync(
                        updateInfo,
                        progressCallback
                    )

                    progressDialog.SetProgress(100)
                    progressDialog.SetStatus("Installing update and restarting PaperRoute...")
                    progressDialog.Refresh()

                    manager.ApplyUpdatesAndRestart(
                        updateInfo.TargetFullRelease
                    )

                End Using

                Return True

            Catch ex As Exception

                If interactive Then

                    MessageBox.Show(
                        owner,
                        "PaperRoute could not complete the update check." &
                        Environment.NewLine & Environment.NewLine &
                        OnlineAccess.Describe(ex, "GitHub"),
                        "Update Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End If

                Return False

            End Try

        End Function


        Private Shared Function CreateManager(
            channel As AppUpdateChannel
        ) As UpdateManager

            Dim includePrereleases As Boolean =
                channel = AppUpdateChannel.Preview

            ' Velopack downloads through the Online services gate (#86).
            Dim source As New GithubSource(
                RepositoryUrl,
                Nothing,
                includePrereleases,
                New GatedDownloader()
            )

            Dim options As New UpdateOptions With {
                .ExplicitChannel = ChannelName(channel),
                .AllowVersionDowngrade = False
            }

            Return New UpdateManager(
                source,
                options
            )

        End Function


        Private Shared Function ChannelName(
            channel As AppUpdateChannel
        ) As String

            If channel = AppUpdateChannel.Preview Then
                Return "preview"
            End If

            Return "stable"

        End Function


        Private Shared Function CurrentInstalledVersion(
            manager As UpdateManager
        ) As String

            If manager.CurrentVersion IsNot Nothing Then
                Return manager.CurrentVersion.ToString()
            End If

            Return CurrentVersionText()

        End Function

    End Class


    ' Velopack's downloader behind the Online services gate (#86): each
    ' update request, and each redirect GitHub sends it through, is refused
    ' while Work offline is on or the update check is switched off, and only
    ' GitHub's listed hosts are reached.
    Friend NotInheritable Class GatedDownloader
        Inherits HttpClientFileDownloader

        Public Overrides Function DownloadBytes(url As String, headers As IDictionary(Of String, String), timeout As Double) As Task(Of Byte())
            Allow(url)
            Return MyBase.DownloadBytes(url, headers, timeout)
        End Function

        Public Overrides Function DownloadFile(url As String, targetFile As String, progress As Action(Of Integer), headers As IDictionary(Of String, String), timeout As Double, Optional cancelToken As CancellationToken = Nothing) As Task
            Allow(url)
            Return MyBase.DownloadFile(url, targetFile, progress, headers, timeout, cancelToken)
        End Function

        Public Overrides Function DownloadString(url As String, headers As IDictionary(Of String, String), timeout As Double) As Task(Of String)
            Allow(url)
            Return MyBase.DownloadString(url, headers, timeout)
        End Function

        ' Velopack's timeout is in minutes. The gate follows redirects, so
        ' every hop is checked.
        Protected Overrides Function CreateHttpClient(headers As IDictionary(Of String, String), timeout As Double) As HttpClient
            Dim client As HttpClient = OnlineAccess.CreateClient(OnlineServiceCatalog.Updates, TimeSpan.FromMinutes(timeout))
            If headers IsNot Nothing Then
                For Each header As KeyValuePair(Of String, String) In headers
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value)
                Next
            End If
            Return client
        End Function

        ' Refused before Velopack starts, with the plain message.
        Private Shared Sub Allow(url As String)
            OnlineAccess.Check(OnlineServiceCatalog.Updates)
            Dim target As Uri = Nothing
            Uri.TryCreate(url, UriKind.Absolute, target)
            OnlineAccess.CheckHost(OnlineServiceCatalog.Find(OnlineServiceCatalog.Updates), target)
        End Sub

    End Class

End Namespace
