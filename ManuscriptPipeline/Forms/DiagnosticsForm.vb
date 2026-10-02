Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class DiagnosticsForm
        Inherits Form

        Private ReadOnly _settings As AppSettings
        Private ReadOnly _repository As New ManuscriptRepository()
        Private ReadOnly _authorRepository As New AuthorLibraryRepository()
        Private ReadOnly _managedLibrary As New ManagedLibraryService()
        Private ReadOnly txtReport As New TextBox()


        Public Sub New(
            settings As AppSettings
        )

            _settings = settings

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            Me.Text = "PaperRoute Diagnostics"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.MaximizeBox = True
            Me.MinimizeBox = False
            Me.SizeGripStyle = SizeGripStyle.Show
            Me.ClientSize = New Size(840, 580)
            Me.MinimumSize = New Size(620, 420)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(20)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblTitle As New Label With {
                .Text = "Diagnostics",
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 16.0F, FontStyle.Bold),
                .ForeColor = UiTheme.AccentColor(),
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblHelp As New Label With {
                .Text = "Safe technical information for troubleshooting. Manuscript titles, notes, reviewer comments, and document contents are not included.",
                .AutoSize = True,
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 0, 0, 10)
            }

            AddHandler root.SizeChanged,
                Sub(sender As Object, e As EventArgs)
                    lblHelp.MaximumSize = New Size(Math.Max(200, root.ClientSize.Width - root.Padding.Horizontal - 8), 0)
                End Sub

            txtReport.Dock = DockStyle.Fill
            txtReport.Multiline = True
            txtReport.ReadOnly = True
            txtReport.ScrollBars = ScrollBars.Both
            txtReport.WordWrap = False
            txtReport.Font = New Font("Consolas", 9.5F)
            txtReport.Text = BuildDiagnosticReport()
            txtReport.Margin = New Padding(0)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .Width = 95,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            Dim btnCopy As New Button With {
                .Text = "Copy Report",
                .Width = 120,
                .Height = 36
            }

            Dim btnSave As New Button With {
                .Text = "Save Report...",
                .Width = 125,
                .Height = 36
            }

            Dim btnOpenData As New Button With {
                .Text = "Open Data Folder",
                .Width = 155,
                .Height = 36
            }

            AddHandler btnCopy.Click,
                AddressOf CopyReport

            AddHandler btnSave.Click,
                AddressOf SaveReport

            AddHandler btnOpenData.Click,
                AddressOf OpenDataFolder

            buttons.Controls.Add(btnClose)
            buttons.Controls.Add(btnCopy)
            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnOpenData)

            root.Controls.Add(lblTitle, 0, 0)
            root.Controls.Add(lblHelp, 0, 1)
            root.Controls.Add(txtReport, 0, 2)
            root.Controls.Add(buttons, 0, 3)

            Me.AcceptButton = btnClose
            Me.CancelButton = btnClose
            Me.Controls.Add(root)

        End Sub


        Private Function TurnedOffServices() As String

            Dim turnedOff As List(Of String) = If(_settings.OnlineServices?.TurnedOff, New List(Of String)())
            Dim names As New List(Of String)()

            For Each service As OnlineService In OnlineServiceCatalog.Services
                If Not service.OffUntilTurnedOn AndAlso turnedOff.Contains(service.Id) Then names.Add(service.Name)
            Next

            Return If(names.Count = 0, "None", String.Join(", ", names))

        End Function


        ' The AI assistant (#84): whether it is on, which kind of service,
        ' and whether its keys are stored; never an address, host, or key.
        Friend Shared Function AssistantLines(assistant As AssistantSettings, keys As ProtectedKeyStore) As String()

            Dim state As String

            If assistant Is Nothing OrElse Not assistant.Enabled Then
                state = "Off"
            ElseIf assistant.Provider = AssistantProvider.Claude Then
                Dim model As String = If(assistant.ClaudeModel, String.Empty).Trim()
                ' Settings keep only a well-formed model id, but say nothing else.
                If Not System.Text.RegularExpressions.Regex.IsMatch(model, "^[A-Za-z0-9._:@-]{1,100}$") Then model = "claude-opus-5-5"
                state = "On (Claude, " & model & ")"
            Else
                Dim endpoint As Uri = OnlineAccess.ParseAssistantEndpoint(assistant.Endpoint)
                state =
                    If(endpoint Is Nothing, "On (OpenAI-compatible server, not set up)",
                       If(endpoint.IsLoopback, "On (OpenAI-compatible server on this computer)",
                          "On (OpenAI-compatible server elsewhere, https)"))
            End If

            Return {
                "AI assistant: " & state,
                "Claude key: " & If(keys.HasKey(ProtectedKeyStore.Anthropic), "Added", "Not added"),
                "Server key: " & If(keys.HasKey(ProtectedKeyStore.AssistantEndpoint), "Added", "Not added")
            }

        End Function


        ' For tests.
        Friend ReadOnly Property ReportText As String
            Get
                Return txtReport.Text
            End Get
        End Property


        Private Function BuildDiagnosticReport() As String

            Dim builder As New StringBuilder()

            builder.AppendLine("PaperRoute Diagnostics")
            builder.AppendLine("======================")
            builder.AppendLine()
            builder.AppendLine("Version: " & UpdateService.CurrentVersionText())
            builder.AppendLine("Update channel: " & UpdateService.ChannelDisplayName(_settings.UpdateChannel))
            builder.AppendLine("Installed build: " & If(UpdateService.IsInstalledBuild(_settings.UpdateChannel), "Yes", "No (developer/portable)"))
            builder.AppendLine("Storage profile: " & StorageEnvironment.ProfileDisplayName())
            builder.AppendLine("Automatic update checks: " & If(_settings.CheckForUpdatesAutomatically, "On", "Off"))
            builder.AppendLine("Work offline: " & If(OnlineAccess.IsWorkingOffline, "On", "Off"))
            builder.AppendLine("Online services turned off: " & TurnedOffServices())
            ' Whether a key is stored, never the key.
            builder.AppendLine("OpenAlex key: " & If(OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.OpenAlex), "Added", "Not added"))
            For Each line As String In AssistantLines(_settings.OnlineServices?.Assistant, OnlineAccess.KeyStore())
                builder.AppendLine(line)
            Next
            builder.AppendLine("Storage schema: " & StorageMigrationService.ReadSchemaVersion().ToString())
            builder.AppendLine()
            builder.AppendLine("OS: " & RuntimeInformation.OSDescription)
            builder.AppendLine("Architecture: " & RuntimeInformation.OSArchitecture.ToString())
            builder.AppendLine("Process architecture: " & RuntimeInformation.ProcessArchitecture.ToString())
            builder.AppendLine("Runtime: " & RuntimeInformation.FrameworkDescription)
            builder.AppendLine("64-bit process: " & Environment.Is64BitProcess.ToString())
            builder.AppendLine()
            builder.AppendLine("Executable: " & Application.ExecutablePath)
            builder.AppendLine("Data file: " & _repository.DataFilePath)
            builder.AppendLine("Backup file: " & _repository.BackupFilePath)
            builder.AppendLine("Reusable metadata library: " & _authorRepository.DataFilePath)
            builder.AppendLine("Managed library: " & _managedLibrary.RootDirectory)
            builder.AppendLine("Legacy data retained: " & If(Directory.Exists(StorageMigrationService.LegacyDataRoot()), "Yes", "No"))
            builder.AppendLine("Legacy library retained: " & If(Directory.Exists(StorageMigrationService.LegacyManagedLibraryRoot()), "Yes", "No"))

            Return builder.ToString()

        End Function


        Private Sub CopyReport(
            sender As Object,
            e As EventArgs
        )

            Try

                Clipboard.SetText(txtReport.Text)

                MessageBox.Show(
                    Me,
                    "The diagnostic report was copied to the clipboard.",
                    "PaperRoute Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not copy the diagnostic report." &
                    Environment.NewLine & Environment.NewLine &
                    ex.Message,
                    "Diagnostics Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        Private Sub SaveReport(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New SaveFileDialog()

                dialog.Title = "Save PaperRoute Diagnostic Report"
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                dialog.DefaultExt = "txt"
                dialog.AddExtension = True
                dialog.FileName = "PaperRoute_Diagnostics_" & DateTime.Now.ToString("yyyy-MM-dd_HHmm") & ".txt"

                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If

                Try

                    File.WriteAllText(
                        dialog.FileName,
                        txtReport.Text,
                        New UTF8Encoding(False)
                    )

                    MessageBox.Show(
                        Me,
                        "The diagnostic report was saved successfully.",
                        "PaperRoute Diagnostics",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not save the diagnostic report." &
                        Environment.NewLine & Environment.NewLine &
                        ex.Message,
                        "Diagnostics Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub


        Private Sub OpenDataFolder(
            sender As Object,
            e As EventArgs
        )

            Try

                Dim dataFolder As String =
                    Path.GetDirectoryName(_repository.DataFilePath)

                If String.IsNullOrWhiteSpace(dataFolder) Then
                    Return
                End If

                Directory.CreateDirectory(dataFolder)

                Process.Start(
                    New ProcessStartInfo With {
                        .FileName = dataFolder,
                        .UseShellExecute = True
                    }
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not open the data folder." &
                    Environment.NewLine & Environment.NewLine &
                    ex.Message,
                    "Diagnostics Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub

    End Class

End Namespace
