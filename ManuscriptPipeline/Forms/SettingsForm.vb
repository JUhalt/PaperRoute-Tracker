Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Text.Json
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class SettingsForm
        Inherits Form

        Private ReadOnly _settings As AppSettings
        Private ReadOnly _settingsService As AppSettingsService
        Private ReadOnly _showOnlineServices As Boolean

        Private ReadOnly rbSystem As New RadioButton()
        Private ReadOnly rbLight As New RadioButton()
        Private ReadOnly rbDark As New RadioButton()

        Private ReadOnly numFileDrawerThreshold As New NumericUpDown()
        Private ReadOnly numLongReview As New NumericUpDown()
        Private ReadOnly numRevisionWarning As New NumericUpDown()
        Private ReadOnly numRecentRejection As New NumericUpDown()

        Private ReadOnly chkReminderNotifications As New CheckBox()
        Private ReadOnly numReminderNotificationDays As New NumericUpDown()

        Private ReadOnly cboUpdateChannel As New ComboBox()
        Private ReadOnly chkAutomaticUpdates As New CheckBox()

        ' Online services (#86).
        Private ReadOnly chkWorkOffline As New CheckBox()
        Private ReadOnly serviceChecks As New Dictionary(Of String, CheckBox)(StringComparer.Ordinal)
        Private contentScroller As Panel = Nothing
        Private onlineGroup As Control = Nothing

        ' Keys the researcher adds; a change waits for Save.
        Private ReadOnly openAlexKey As New KeyRow(ProtectedKeyStore.OpenAlex, "OpenAlex", "Add Key...", "Replace Key...", "Remove Key")
        Private ReadOnly claudeKey As New KeyRow(ProtectedKeyStore.Anthropic, "Claude", "Add Claude Key...", "Replace Claude Key...", "Remove Claude Key")
        Private ReadOnly serverKey As New KeyRow(ProtectedKeyStore.AssistantEndpoint, "server", "Add Server Key...", "Replace Server Key...", "Remove Server Key")

        ' Asks for an OpenAlex key; tests replace it.
        Friend keyPrompt As Func(Of IWin32Window, String) = Nothing

        ' The optional AI assistant (#84).
        Private ReadOnly _showAssistant As Boolean
        Private ReadOnly chkAssistant As New CheckBox()
        Private ReadOnly lblAssistantOffline As New Label()
        Private ReadOnly rbClaude As New RadioButton()
        Private ReadOnly rbCompatible As New RadioButton()
        Private ReadOnly cboClaudeModel As New SteadyComboBox()
        Private ReadOnly txtEndpoint As New TextBox()
        Private ReadOnly txtEndpointModel As New TextBox()
        Private ReadOnly lblEndpointCheck As New Label()
        Private ReadOnly btnForgetChoices As New Button()
        Private ReadOnly lblChoices As New Label()

        ' Test Connection (#96): tries the setup typed here, before Save.
        Private ReadOnly btnTestConnection As New Button()
        Private ReadOnly lblTestSends As New Label()
        Private ReadOnly lblTestResult As New Label()
        Private _testCancellation As System.Threading.CancellationTokenSource = Nothing
        ' A result is shown only for the setup it tested.
        Private _testGeneration As Integer = 0
        Private _testedSetup As String = Nothing
        Private _keyEdits As Integer = 0
        Private _listingModels As Boolean = False

        Private ReadOnly claudeSection As New List(Of Control)()
        Private ReadOnly compatibleSection As New List(Of Control)()
        Private ReadOnly assistantServiceRows As New Dictionary(Of String, Label)(StringComparer.Ordinal)
        Private assistantGroup As Control = Nothing
        Private _boldFont As Font = Nothing

        ' The address the stored server key was added for, the "don't ask
        ' again" choices, and whether to forget them on Save.
        Private _endpointKeyOrigin As String = String.Empty
        Private _confirmedUses As Integer = 0
        Private _forgetChoices As Boolean = False

        Friend Shared ReadOnly ClaudeModels As String() = {"claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-4-5"}

        ' Asks for an AI assistant key by its key store name (Anthropic, or
        ' a compatible server's), returning Nothing to cancel; tests replace it.
        Friend assistantKeyPrompt As Func(Of IWin32Window, String, String) = Nothing

        ' Shows why Save can't go ahead; tests replace the message box.
        Friend problemNotice As Action(Of String) = Nothing

        ' For tests: what Test Connection last said.
        Friend ReadOnly Property TestResultText As String
            Get
                Return lblTestResult.Text
            End Get
        End Property

        Private _appearanceChanged As Boolean = False


        Public ReadOnly Property AppearanceChanged As Boolean
            Get
                Return _appearanceChanged
            End Get
        End Property


        Public Sub New(
            settings As AppSettings
        )

            Me.New(settings, New AppSettingsService())

        End Sub


        ' With the service that loaded the settings, so a damaged settings
        ' file is set aside rather than kept as the backup; optionally open
        ' at Online services or at the AI assistant.
        Friend Sub New(
            settings As AppSettings,
            settingsService As AppSettingsService,
            Optional showOnlineServices As Boolean = False,
            Optional showAssistant As Boolean = False
        )

            _settings =
                If(
                    settings,
                    New AppSettings()
                )

            _settingsService = If(settingsService, New AppSettingsService())
            _showOnlineServices = showOnlineServices
            _showAssistant = showAssistant

            BuildInterface()
            LoadValues()

            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnShown(e As EventArgs)

            MyBase.OnShown(e)

            If Not cboClaudeModel.Focused Then cboClaudeModel.SelectionLength = 0

            If _showAssistant AndAlso contentScroller IsNot Nothing AndAlso assistantGroup IsNot Nothing Then
                contentScroller.AutoScrollPosition = New Point(0, assistantGroup.Top)
                chkAssistant.Focus()
            ElseIf _showOnlineServices AndAlso contentScroller IsNot Nothing AndAlso onlineGroup IsNot Nothing Then
                contentScroller.AutoScrollPosition = New Point(0, onlineGroup.Top)
                chkWorkOffline.Focus()
            End If

        End Sub


        ' Closing stops a connection test still running; nothing it found is kept.
        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)

            _testCancellation?.Cancel()

            MyBase.OnFormClosed(e)

        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)

            MyBase.Dispose(disposing)

            If disposing Then
                _boldFont?.Dispose()
                _boldFont = Nothing
            End If

        End Sub


        Private Sub BuildInterface()

            Me.Text = "PaperRoute Preferences"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.MaximizeBox = True
            Me.MinimizeBox = False
            Me.SizeGripStyle = SizeGripStyle.Show
            Me.ClientSize = New Size(780, 760)
            Me.MinimumSize = New Size(640, 540)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(12)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim contentHost As New Panel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .Padding = New Padding(4)
            }

            contentScroller = contentHost

            Dim settingsStack As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .ColumnCount = 1,
                .RowCount = 7,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Margin = New Padding(0),
                .Padding = New Padding(0)
            }

            settingsStack.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For rowIndex As Integer = 0 To 6

                settingsStack.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )

            Next

            Dim appearanceGroup As GroupBox =
                BuildAppearanceGroup()

            Dim attentionGroup As GroupBox =
                BuildAttentionGroup()

            Dim remindersGroup As GroupBox =
                BuildRemindersGroup()

            Dim drawerGroup As GroupBox =
                BuildFileDrawerGroup()

            Dim updatesGroup As GroupBox =
                BuildUpdatesGroup()

            settingsStack.Controls.Add(
                appearanceGroup,
                0,
                0
            )

            settingsStack.Controls.Add(
                attentionGroup,
                0,
                1
            )

            settingsStack.Controls.Add(
                remindersGroup,
                0,
                2
            )

            settingsStack.Controls.Add(
                drawerGroup,
                0,
                3
            )

            settingsStack.Controls.Add(
                updatesGroup,
                0,
                4
            )

            onlineGroup = BuildOnlineServicesGroup()

            settingsStack.Controls.Add(
                onlineGroup,
                0,
                5
            )

            assistantGroup = BuildAssistantGroup()

            settingsStack.Controls.Add(
                assistantGroup,
                0,
                6
            )

            contentHost.Controls.Add(
                settingsStack
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save",
                .Width = 95,
                .Height = 36
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .Width = 95,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveSettings

            buttons.Controls.Add(
                btnSave
            )

            buttons.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                contentHost,
                0,
                0
            )

            root.Controls.Add(
                buttons,
                0,
                1
            )

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)

        End Sub


        Private Function BuildAppearanceGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "Appearance",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim panel As New FlowLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .Margin = New Padding(0)
            }

            rbSystem.Text = "Follow Windows"
            rbSystem.AutoSize = True
            rbSystem.Margin = New Padding(0, 4, 0, 6)

            rbLight.Text = "Light"
            rbLight.AutoSize = True
            rbLight.Margin = New Padding(0, 4, 0, 6)

            rbDark.Text = "Dark"
            rbDark.AutoSize = True
            rbDark.Margin = New Padding(0, 4, 0, 6)

            Dim help As New Label With {
                .Text = "Theme changes take effect after PaperRoute restarts.",
                .AutoSize = True,
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(22, 8, 0, 4)
            }

            panel.Controls.Add(rbSystem)
            panel.Controls.Add(rbLight)
            panel.Controls.Add(rbDark)
            panel.Controls.Add(help)

            group.Controls.Add(panel)

            Return group

        End Function


        Private Function BuildAttentionGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "Needs Attention",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim grid As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 3,
                .RowCount = 3,
                .Margin = New Padding(0)
            }

            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 82))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))

            ConfigureNumber(numLongReview, 1, 730)
            ConfigureNumber(numRevisionWarning, 1, 180)
            ConfigureNumber(numRecentRejection, 1, 365)

            AddSettingRow(
                grid,
                0,
                "Flag a long review after",
                numLongReview,
                "days"
            )

            AddSettingRow(
                grid,
                1,
                "Warn about revision deadlines",
                numRevisionWarning,
                "days early"
            )

            AddSettingRow(
                grid,
                2,
                "Treat a rejection as recent for",
                numRecentRejection,
                "days"
            )

            group.Controls.Add(grid)

            Return group

        End Function


        Private Function BuildRemindersGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "Reminders && Windows Notifications",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 3,
                .RowCount = 3,
                .Margin = New Padding(0)
            }

            layout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            layout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    82
                )
            )

            layout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    110
                )
            )

            chkReminderNotifications.Text =
                "Show a Windows reminder notification when PaperRoute starts"

            chkReminderNotifications.AutoSize =
                True

            chkReminderNotifications.Anchor =
                AnchorStyles.Left

            chkReminderNotifications.Margin =
                New Padding(
                    0,
                    8,
                    0,
                    8
                )

            layout.Controls.Add(
                chkReminderNotifications,
                0,
                0
            )

            layout.SetColumnSpan(
                chkReminderNotifications,
                3
            )

            ConfigureNumber(
                numReminderNotificationDays,
                0,
                30
            )

            AddSettingRow(
                layout,
                1,
                "Include upcoming reminders",
                numReminderNotificationDays,
                "days ahead"
            )

            Dim help As New Label With {
                .Text =
                    "Notifications are optional. The Deadlines page remains available even if Windows suppresses notifications.",
                .AutoSize = True,
                .UseMnemonic = False,
                .MaximumSize = New Size(680, 0),
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 8, 0, 4)
            }

            layout.Controls.Add(
                help,
                0,
                2
            )

            layout.SetColumnSpan(
                help,
                3
            )

            group.Controls.Add(layout)

            Return group

        End Function


        Private Function BuildFileDrawerGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "File Drawer",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim grid As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 3,
                .RowCount = 1,
                .Margin = New Padding(0)
            }

            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 82))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim label As New Label With {
                .Text = "Suggest the File Drawer after",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 10, 8, 10)
            }

            ConfigureNumber(
                numFileDrawerThreshold,
                1,
                20
            )

            numFileDrawerThreshold.Anchor =
                AnchorStyles.Left

            numFileDrawerThreshold.Margin =
                New Padding(0, 7, 0, 7)

            Dim suffix As New Label With {
                .Text = "rejections",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(8, 10, 0, 10)
            }

            grid.Controls.Add(label, 0, 0)
            grid.Controls.Add(numFileDrawerThreshold, 1, 0)
            grid.Controls.Add(suffix, 2, 0)

            group.Controls.Add(grid)

            Return group

        End Function


        Private Function BuildUpdatesGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "Updates",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim grid As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .RowCount = 2,
                .Margin = New Padding(0)
            }

            grid.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    165
                )
            )

            grid.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblChannel As New Label With {
                .Text = "Update channel",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 10, 8, 10)
            }

            cboUpdateChannel.DropDownStyle =
                ComboBoxStyle.DropDownList

            cboUpdateChannel.Width =
                190

            cboUpdateChannel.Items.Add(
                "Stable"
            )

            cboUpdateChannel.Items.Add(
                "Preview"
            )

            cboUpdateChannel.Anchor =
                AnchorStyles.Left

            cboUpdateChannel.Margin =
                New Padding(0, 6, 0, 6)

            Dim lblAutomatic As New Label With {
                .Text = "Automatic checks",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 10, 8, 10)
            }

            chkAutomaticUpdates.Text =
                "Check for updates when PaperRoute starts"

            chkAutomaticUpdates.AutoSize =
                True

            chkAutomaticUpdates.Anchor =
                AnchorStyles.Left

            chkAutomaticUpdates.Margin =
                New Padding(0, 8, 0, 8)

            grid.Controls.Add(lblChannel, 0, 0)
            grid.Controls.Add(cboUpdateChannel, 1, 0)
            grid.Controls.Add(lblAutomatic, 0, 1)
            grid.Controls.Add(chkAutomaticUpdates, 1, 1)

            group.Controls.Add(grid)

            Return group

        End Function


        ' Online services (#86): Work offline, each service with what it
        ' contacts, sends, and when, and the optional OpenAlex key. The rows
        ' come from OnlineServiceCatalog, as the User Guide's table does.
        Private Function BuildOnlineServicesGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "Online services",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim nameWidth As Integer = UiTheme.Px(210, DeviceDpi)
            Dim spanning As New List(Of Label)()
            Dim detailed As New List(Of Label)()

            Dim grid As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .Margin = New Padding(0)
            }

            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, nameWidth))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            Dim addRow As Action(Of Control, Control) =
                Sub(first, second)
                    Dim row As Integer = grid.RowCount
                    grid.RowCount = row + 1
                    grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                    If first IsNot Nothing Then grid.Controls.Add(first, 0, row)
                    If second IsNot Nothing Then grid.Controls.Add(second, 1, row)
                    If first IsNot Nothing AndAlso second Is Nothing Then grid.SetColumnSpan(first, 2)
                End Sub

            Dim muted As Func(Of String, Padding, Label) =
                Function(text, margin)
                    Return New Label With {
                        .Text = text,
                        .AutoSize = True,
                        .UseMnemonic = False,
                        .ForeColor = UiTheme.MutedText(),
                        .Margin = margin
                    }
                End Function

            Dim intro As Label = muted("PaperRoute works without the internet. These services are used only for the features listed, and each sends only what's shown here.", New Padding(0, 0, 0, 8))
            addRow(intro, Nothing)
            spanning.Add(intro)

            If _settingsService.LoadFailed Then
                Dim damaged As New Label With {
                    .Text = "PaperRoute couldn't read your saved settings, so Work offline is on. Review these choices, then choose Save.",
                    .AutoSize = True,
                    .UseMnemonic = False,
                    .ForeColor = UiTheme.WarningColor(),
                    .Margin = New Padding(0, 0, 0, 8)
                }
                addRow(damaged, Nothing)
                spanning.Add(damaged)
            End If

            chkWorkOffline.Text = "Work offline"
            chkWorkOffline.AutoSize = True
            chkWorkOffline.Font = BoldFont()
            chkWorkOffline.Margin = New Padding(0, 4, 0, 0)
            AddHandler chkWorkOffline.CheckedChanged, Sub(sender, e) RefreshOnlineState()
            addRow(chkWorkOffline, Nothing)

            Dim offlineHelp As Label = muted("Stops every service below, update checks included. Your choices below are kept for when you turn it off.", New Padding(22, 2, 0, 10))
            addRow(offlineHelp, Nothing)
            spanning.Add(offlineHelp)

            For Each service As OnlineService In OnlineServiceCatalog.Services

                ' The AI assistant is off until turned on below, so it is
                ' shown here but never switched here.
                If service.OffUntilTurnedOn Then

                    Dim name As New Label With {
                        .Text = service.Name,
                        .AutoSize = True,
                        .UseMnemonic = False,
                        .MaximumSize = New Size(nameWidth - UiTheme.Px(26, DeviceDpi), 0),
                        .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                        .Margin = New Padding(UiTheme.Px(18, DeviceDpi), 8, 8, 6)
                    }

                    Dim assistantDetails As Label = muted(
                        "Contacts: " & service.Contacts & Environment.NewLine &
                        "Sends: " & service.Sends & Environment.NewLine &
                        "When: " & service.WhenUsed,
                        New Padding(0, 8, 0, 8))
                    assistantDetails.Tag = assistantDetails.Text
                    assistantServiceRows(service.Id) = assistantDetails

                    addRow(name, assistantDetails)
                    detailed.Add(assistantDetails)
                    Continue For

                End If

                Dim check As New CheckBox With {
                    .Text = service.Name,
                    .AutoSize = True,
                    .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                    .Margin = New Padding(0, 6, 8, 6),
                    .AccessibleDescription = "Sends " & service.Sends
                }
                AddHandler check.CheckedChanged, Sub(sender, e) RefreshOnlineState()
                serviceChecks(service.Id) = check

                Dim details As Label = muted(
                    "Contacts: " & service.Contacts & Environment.NewLine &
                    "Sends: " & service.Sends & Environment.NewLine &
                    "When: " & service.WhenUsed,
                    New Padding(0, 8, 0, 8))

                addRow(check, details)
                detailed.Add(details)

            Next

            Dim newServices As Label = muted("A service added in a later version starts on and appears here, with what it sends.", New Padding(0, 4, 0, 10))
            addRow(newServices, Nothing)
            spanning.Add(newServices)

            ' The optional OpenAlex key.
            Dim lblKey As New Label With {
                .Text = "OpenAlex key",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                .Margin = New Padding(0, 10, 8, 4)
            }

            addRow(lblKey, BuildKeyPanel(openAlexKey))
            detailed.Add(openAlexKey.Status)

            Dim keyHelp As Label = muted("Optional, for PaperRoute's OpenAlex features. A free key raises OpenAlex's daily allowance. It's kept encrypted on this computer, never in backups or exports, and sent only to api.openalex.org.", New Padding(0, 0, 0, 8))
            Dim keySpacer As New Label With {.AutoSize = True, .Text = String.Empty, .Margin = New Padding(0)}
            addRow(keySpacer, keyHelp)
            detailed.Add(keyHelp)

            Dim lnkSends As New LinkLabel With {
                .Text = "What PaperRoute sends, and when",
                .AutoSize = True,
                .Margin = New Padding(0, 6, 0, 0)
            }
            AddHandler lnkSends.LinkClicked,
                Sub(sender, e)
                    Using help As New HelpForm("What PaperRoute sends, and when")
                        help.ShowDialog(Me)
                    End Using
                End Sub
            addRow(lnkSends, Nothing)

            ' Long text wraps to the card's width at every size and scale.
            AddHandler grid.SizeChanged,
                Sub(sender, e)
                    Dim minimum As Integer = UiTheme.Px(120, DeviceDpi)
                    For Each item As Label In spanning
                        item.MaximumSize = New Size(Math.Max(minimum, grid.ClientSize.Width - item.Margin.Horizontal - UiTheme.Px(4, DeviceDpi)), 0)
                    Next
                    For Each item As Label In detailed
                        item.MaximumSize = New Size(Math.Max(minimum, grid.ClientSize.Width - nameWidth - item.Margin.Horizontal - UiTheme.Px(4, DeviceDpi)), 0)
                    Next
                End Sub

            group.Controls.Add(grid)

            Return group

        End Function


        ' The optional AI assistant (#84): off until turned on, then Claude
        ' with the researcher's own key, or an OpenAI-compatible server (a
        ' model on this computer, or a server elsewhere over https).
        Private Function BuildAssistantGroup() As GroupBox

            Dim group As New SectionCard With {
                .Text = "AI assistant",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16),
                .Margin = New Padding(0, 0, 0, 4)
            }

            Dim nameWidth As Integer = UiTheme.Px(210, DeviceDpi)
            Dim indent As Integer = UiTheme.Px(22, DeviceDpi)
            Dim spanning As New List(Of Control)()
            Dim detailed As New List(Of Label)()

            Dim grid As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .Margin = New Padding(0)
            }

            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, nameWidth))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            Dim addRow As Action(Of Control, Control) =
                Sub(first, second)
                    Dim row As Integer = grid.RowCount
                    grid.RowCount = row + 1
                    grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                    If first IsNot Nothing Then grid.Controls.Add(first, 0, row)
                    If second IsNot Nothing Then grid.Controls.Add(second, 1, row)
                    If first IsNot Nothing AndAlso second Is Nothing Then grid.SetColumnSpan(first, 2)
                End Sub

            Dim muted As Func(Of String, Padding, Label) =
                Function(text, margin)
                    Return New Label With {
                        .Text = text,
                        .AutoSize = True,
                        .UseMnemonic = False,
                        .ForeColor = UiTheme.MutedText(),
                        .Margin = margin
                    }
                End Function

            Dim fieldLabel As Func(Of String, Label) =
                Function(text)
                    Return New Label With {
                        .Text = text,
                        .AutoSize = True,
                        .UseMnemonic = False,
                        .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                        .Margin = New Padding(indent, 10, 8, 4)
                    }
                End Function

            chkAssistant.Text = "Turn on the AI assistant"
            chkAssistant.AutoSize = True
            chkAssistant.Font = BoldFont()
            chkAssistant.Margin = New Padding(0, 4, 0, 0)
            AddHandler chkAssistant.CheckedChanged, Sub(sender, e) RefreshAssistantState()
            addRow(chkAssistant, Nothing)

            Dim intro As Label = muted("Off until you turn it on. Each window shows what it will send before sending, and nothing changes until you accept a suggestion.", New Padding(indent, 2, 0, 4))
            addRow(intro, Nothing)
            spanning.Add(intro)

            lblAssistantOffline.Text = "Work offline is on, so the assistant can't be used until you turn it off."
            lblAssistantOffline.AutoSize = True
            lblAssistantOffline.UseMnemonic = False
            lblAssistantOffline.ForeColor = UiTheme.MutedText()
            lblAssistantOffline.Margin = New Padding(indent, 2, 0, 4)
            addRow(lblAssistantOffline, Nothing)
            spanning.Add(lblAssistantOffline)

            ' Claude, with the researcher's own key.
            rbClaude.Text = "Claude (Anthropic), with your own key"
            rbClaude.AutoSize = True
            rbClaude.Margin = New Padding(0, 10, 0, 2)
            AddHandler rbClaude.CheckedChanged, Sub(sender, e) RefreshAssistantState()
            addRow(rbClaude, Nothing)

            cboClaudeModel.DropDownStyle = ComboBoxStyle.DropDown
            cboClaudeModel.Items.AddRange(ClaudeModels)
            cboClaudeModel.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            cboClaudeModel.MaximumSize = New Size(UiTheme.Px(260, DeviceDpi), 0)
            cboClaudeModel.Margin = New Padding(0, 7, 0, 4)
            cboClaudeModel.AccessibleName = "Claude model"
            ' A resized editable box selects its text; only typing should.
            AddHandler cboClaudeModel.Resize,
                Sub(sender, e)
                    If cboClaudeModel.IsHandleCreated AndAlso Not cboClaudeModel.Focused Then cboClaudeModel.SelectionLength = 0
                End Sub
            AddHandler cboClaudeModel.TextChanged,
                Sub(sender, e)
                    If Not _listingModels Then RefreshTestState()
                End Sub
            Dim lblClaudeModel As Label = fieldLabel("Model")
            addRow(lblClaudeModel, cboClaudeModel)

            Dim lblClaudeKey As Label = fieldLabel("Claude key")
            Dim claudeKeyPanel As FlowLayoutPanel = BuildKeyPanel(claudeKey)
            addRow(lblClaudeKey, claudeKeyPanel)
            detailed.Add(claudeKey.Status)

            Dim lnkAnthropic As New LinkLabel With {
                .Text = "Get a key from Anthropic",
                .AutoSize = True,
                .Margin = New Padding(0, 2, 0, 4)
            }
            AddHandler lnkAnthropic.LinkClicked, AddressOf OpenAnthropicKeys
            addRow(Nothing, lnkAnthropic)

            claudeSection.AddRange({lblClaudeModel, cboClaudeModel, lblClaudeKey, claudeKey.Status, claudeKey.AddButton, claudeKey.RemoveButton, lnkAnthropic})

            ' Any server that speaks the OpenAI chat completions protocol.
            rbCompatible.Text = "Another server, or a model on this computer (OpenAI-compatible)"
            rbCompatible.AutoSize = True
            rbCompatible.Margin = New Padding(0, 12, 0, 2)
            AddHandler rbCompatible.CheckedChanged, Sub(sender, e) RefreshAssistantState()
            addRow(rbCompatible, Nothing)
            spanning.AddRange({chkAssistant, rbClaude, rbCompatible})

            txtEndpoint.PlaceholderText = "http://localhost:11434/v1"
            txtEndpoint.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            txtEndpoint.MaximumSize = New Size(UiTheme.Px(420, DeviceDpi), 0)
            txtEndpoint.Margin = New Padding(0, 7, 0, 2)
            txtEndpoint.AccessibleName = "Server address"
            AddHandler txtEndpoint.TextChanged, Sub(sender, e) RefreshAssistantState()
            Dim lblEndpoint As Label = fieldLabel("Address")
            addRow(lblEndpoint, txtEndpoint)

            lblEndpointCheck.AutoSize = True
            lblEndpointCheck.UseMnemonic = False
            lblEndpointCheck.ForeColor = UiTheme.MutedText()
            lblEndpointCheck.Margin = New Padding(0, 2, 0, 6)
            addRow(Nothing, lblEndpointCheck)
            detailed.Add(lblEndpointCheck)

            txtEndpointModel.PlaceholderText = "llama3.1"
            txtEndpointModel.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            txtEndpointModel.MaximumSize = New Size(UiTheme.Px(260, DeviceDpi), 0)
            txtEndpointModel.Margin = New Padding(0, 7, 0, 4)
            txtEndpointModel.AccessibleName = "Server model"
            AddHandler txtEndpointModel.TextChanged, Sub(sender, e) RefreshTestState()
            Dim lblEndpointModel As Label = fieldLabel("Model")
            addRow(lblEndpointModel, txtEndpointModel)

            Dim lblServerKey As Label = fieldLabel("Server key (optional)")
            Dim serverKeyPanel As FlowLayoutPanel = BuildKeyPanel(serverKey)
            addRow(lblServerKey, serverKeyPanel)
            detailed.Add(serverKey.Status)

            compatibleSection.AddRange({lblEndpoint, txtEndpoint, lblEndpointCheck, lblEndpointModel, txtEndpointModel, lblServerKey, serverKey.Status, serverKey.AddButton, serverKey.RemoveButton})

            ' Test Connection (#96): the setup typed here, before Save, with
            ' what it sends below it, so nothing is sent unannounced.
            Dim testPanel As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 12, 0, 0)
            }

            btnTestConnection.Text = "Test Connection"
            btnTestConnection.AutoSize = True
            btnTestConnection.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            btnTestConnection.Margin = New Padding(0, 0, 12, 4)
            AddHandler btnTestConnection.Click, Async Sub(sender, e) Await TestConnectionAsync()
            testPanel.Controls.Add(btnTestConnection)
            addRow(testPanel, Nothing)

            lblTestSends.AutoSize = True
            lblTestSends.UseMnemonic = False
            lblTestSends.ForeColor = UiTheme.MutedText()
            lblTestSends.Margin = New Padding(0, 2, 0, 2)
            lblTestSends.AccessibleName = "What Test Connection sends"
            addRow(lblTestSends, Nothing)

            lblTestResult.AutoSize = True
            lblTestResult.UseMnemonic = False
            lblTestResult.Margin = New Padding(0, 4, 0, 4)
            lblTestResult.AccessibleName = "Test Connection result"
            addRow(lblTestResult, Nothing)
            spanning.AddRange({lblTestSends, lblTestResult})

            ' The features the researcher said not to ask about again.
            Dim choicesPanel As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 12, 0, 0)
            }

            btnForgetChoices.Text = "Forget Don't Ask Again Choices"
            btnForgetChoices.AutoSize = True
            btnForgetChoices.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            btnForgetChoices.Margin = New Padding(0, 0, 12, 4)
            AddHandler btnForgetChoices.Click,
                Sub(sender, e)
                    _forgetChoices = True
                    RefreshChoices()
                End Sub

            lblChoices.AutoSize = True
            lblChoices.UseMnemonic = False
            lblChoices.ForeColor = UiTheme.MutedText()
            lblChoices.Margin = New Padding(0, 7, 0, 4)

            choicesPanel.Controls.Add(btnForgetChoices)
            choicesPanel.Controls.Add(lblChoices)
            addRow(choicesPanel, Nothing)

            Dim lnkHelp As New LinkLabel With {
                .Text = "What the AI assistant sends",
                .AutoSize = True,
                .Margin = New Padding(0, 6, 0, 0)
            }
            AddHandler lnkHelp.LinkClicked,
                Sub(sender, e)
                    Using help As New HelpForm("AI Assistant")
                        help.ShowDialog(Me)
                    End Using
                End Sub
            addRow(lnkHelp, Nothing)

            ' Long text wraps to the card's width at every size and scale.
            AddHandler grid.SizeChanged,
                Sub(sender, e)
                    Dim minimum As Integer = UiTheme.Px(120, DeviceDpi)
                    For Each item As Control In spanning
                        item.MaximumSize = New Size(Math.Max(minimum, grid.ClientSize.Width - item.Margin.Horizontal - UiTheme.Px(4, DeviceDpi)), 0)
                    Next
                    For Each item As Label In detailed
                        item.MaximumSize = New Size(Math.Max(minimum, grid.ClientSize.Width - nameWidth - item.Margin.Horizontal - UiTheme.Px(4, DeviceDpi)), 0)
                    Next
                    lblChoices.MaximumSize = New Size(Math.Max(minimum, grid.ClientSize.Width - lblChoices.Margin.Horizontal - UiTheme.Px(4, DeviceDpi)), 0)
                End Sub

            group.Controls.Add(grid)

            Return group

        End Function


        Private Function BoldFont() As Font

            If _boldFont Is Nothing Then
                _boldFont = New Font(Me.Font, FontStyle.Bold)
            End If

            Return _boldFont

        End Function


        ' A key's status, Add or Replace, and Remove; a change waits for Save.
        Private Function BuildKeyPanel(row As KeyRow) As FlowLayoutPanel

            Dim keyPanel As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 4, 0, 0)
            }

            row.Status.AutoSize = True
            row.Status.UseMnemonic = False
            row.Status.Margin = New Padding(0, 6, 12, 4)

            row.AddButton.AutoSize = True
            row.AddButton.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            row.AddButton.Margin = New Padding(0, 0, 8, 4)
            AddHandler row.AddButton.Click, Sub(sender, e) AddKey(row)

            row.RemoveButton.AutoSize = True
            row.RemoveButton.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            row.RemoveButton.Margin = New Padding(0, 0, 0, 4)
            AddHandler row.RemoveButton.Click,
                Sub(sender, e)
                    row.PendingKey = Nothing
                    row.RemoveRequested = row.HasKey
                    _keyEdits += 1
                    RefreshKeyState()
                End Sub

            keyPanel.Controls.Add(row.Status)
            keyPanel.Controls.Add(row.AddButton)
            keyPanel.Controls.Add(row.RemoveButton)

            Return keyPanel

        End Function


        ' Service rows follow Work offline; the automatic update check
        ' follows the update check service.
        Private Sub RefreshOnlineState()

            Dim offline As Boolean = chkWorkOffline.Checked

            For Each check As CheckBox In serviceChecks.Values
                check.Enabled = Not offline
            Next

            Dim updates As CheckBox = Nothing
            chkAutomaticUpdates.Enabled =
                Not offline AndAlso
                serviceChecks.TryGetValue(OnlineServiceCatalog.Updates, updates) AndAlso
                updates.Checked

            ' The assistant's setup stays editable while offline.
            lblAssistantOffline.Visible = offline
            RefreshTestState()

        End Sub


        ' Only the chosen service's setup is enabled, and only once the
        ' assistant is turned on.
        Private Sub RefreshAssistantState()

            Dim turnedOn As Boolean = chkAssistant.Checked

            rbClaude.Enabled = turnedOn
            rbCompatible.Enabled = turnedOn

            SetActive(claudeSection, ClaudeChosen())
            SetActive(compatibleSection, CompatibleChosen())

            Dim check = EndpointCheck(txtEndpoint.Text)
            lblEndpointCheck.Text = check.Message
            lblEndpointCheck.ForeColor =
                If(check.Usable OrElse txtEndpoint.Text.Trim().Length = 0 OrElse Not CompatibleChosen(),
                   UiTheme.MutedText(),
                   UiTheme.WarningColor())

            For Each pair As KeyValuePair(Of String, Label) In assistantServiceRows
                Dim chosen As Boolean =
                    turnedOn AndAlso
                    If(pair.Key = OnlineServiceCatalog.AssistantCompatible, rbCompatible.Checked, rbClaude.Checked)
                pair.Value.Text =
                    CStr(pair.Value.Tag) & Environment.NewLine &
                    If(chosen, "On. Set up under AI assistant below.", "Off until turned on under AI assistant.")
            Next

            RefreshKeyState()

        End Sub


        Private Function ClaudeChosen() As Boolean
            Return chkAssistant.Checked AndAlso rbClaude.Checked
        End Function


        Private Function CompatibleChosen() As Boolean
            Return chkAssistant.Checked AndAlso rbCompatible.Checked
        End Function


        ' A section not in use: its fields and buttons disabled, and its
        ' labels quieter (a disabled label is hard to read in Dark).
        Private Shared Sub SetActive(items As IEnumerable(Of Control), active As Boolean)

            For Each item As Control In items
                If TypeOf item Is Label AndAlso Not TypeOf item Is LinkLabel Then
                    item.ForeColor = If(active, UiTheme.PrimaryText(), UiTheme.MutedText())
                Else
                    item.Enabled = active
                End If
            Next

        End Sub


        ' What the address box says about the address typed (#84).
        Friend Shared Function EndpointCheck(text As String) As (Message As String, Usable As Boolean)

            Dim parsed As Uri = OnlineAccess.ParseAssistantEndpoint(text)

            If parsed IsNot Nothing Then
                Return If(parsed.IsLoopback,
                          ("On this computer: nothing leaves it.", True),
                          ("Elsewhere, over https.", True))
            End If

            Dim typed As Uri = Nothing
            If Uri.TryCreate(If(text, String.Empty).Trim(), UriKind.Absolute, typed) AndAlso
               typed.Scheme = Uri.UriSchemeHttp AndAlso
               Not typed.IsLoopback Then
                Return ("http works only for a server on this computer. Use https for a server elsewhere.", False)
            End If

            Return ("Enter the server's address, such as http://localhost:11434/v1.", False)

        End Function


        Private Sub RefreshKeyState()

            ' A server key goes only to the address it was added for.
            Dim endpoint As Uri = OnlineAccess.ParseAssistantEndpoint(txtEndpoint.Text)
            serverKey.Outdated =
                serverKey.HasKey AndAlso
                Not String.Equals(_endpointKeyOrigin, OnlineAccess.OriginOf(endpoint), StringComparison.OrdinalIgnoreCase)

            openAlexKey.Refresh()
            claudeKey.Refresh()
            serverKey.Refresh()

            ' A stored key can always be removed, whichever service is chosen
            ' and even with the assistant off; adding one needs its service.
            claudeKey.AddButton.Enabled = ClaudeChosen()
            claudeKey.RemoveButton.Enabled = True
            serverKey.AddButton.Enabled = CompatibleChosen() AndAlso endpoint IsNot Nothing
            serverKey.RemoveButton.Enabled = True

            RefreshTestState()

        End Sub


        ' The AI assistant as set up in this window, saved or not; Nothing
        ' while it is off or a server's address or model is missing.
        Private Function FormConnection() As AssistantConnection

            If Not chkAssistant.Checked Then Return Nothing

            Dim assistant As New AssistantSettings()
            ApplyAssistantTo(assistant)
            Return OnlineAccess.ConnectionFor(assistant)

        End Function


        ' Test Connection is available for a complete setup while online,
        ' and says what it will send; a result stays only for the setup it
        ' tested.
        Private Sub RefreshTestState()

            Dim connection As AssistantConnection = FormConnection()
            Dim row As KeyRow = If(connection IsNot Nothing AndAlso connection.Provider = AssistantProvider.Compatible, serverKey, claudeKey)

            Dim setup As String =
                If(connection Is Nothing,
                   String.Empty,
                   String.Join("|", connection.ServiceId, connection.Model, OnlineAccess.OriginOf(connection.Endpoint), connection.Endpoint?.AbsolutePath,
                               row.WillHaveKey.ToString(), _keyEdits.ToString(Globalization.CultureInfo.InvariantCulture)))

            If Not String.Equals(setup, _testedSetup, StringComparison.Ordinal) Then
                _testedSetup = setup
                _testGeneration += 1
                ShowTestResult(String.Empty, UiTheme.PrimaryText())
            End If

            btnTestConnection.Enabled = _testCancellation Is Nothing AndAlso Not chkWorkOffline.Checked AndAlso connection IsNot Nothing
            lblTestSends.Text = AssistantConnectionTest.WhatIsSent(connection, row.WillHaveKey)
            lblTestSends.Visible = lblTestSends.Text.Length > 0
            btnTestConnection.AccessibleDescription = lblTestSends.Text

        End Sub


        Private Sub ShowTestResult(message As String, color As Color)

            lblTestResult.Text = message
            lblTestResult.ForeColor = color
            lblTestResult.Visible = message.Length > 0

        End Sub


        ' Tries the setup typed in this window (#96), keys not yet saved
        ' included, through the Online services gate. Nothing is stored or
        ' saved, and no Don't Ask Again choice is made: the line below the
        ' button says what is sent, and it is never the researcher's text.
        Friend Async Function TestConnectionAsync() As System.Threading.Tasks.Task

            If _testCancellation IsNot Nothing OrElse chkWorkOffline.Checked Then Return

            Dim connection As AssistantConnection = FormConnection()
            If connection Is Nothing Then Return
            Dim claude As Boolean = connection.Provider = AssistantProvider.Claude
            Dim row As KeyRow = If(claude, claudeKey, serverKey)

            If claude AndAlso Not IsUsableClaudeModel(ClaudeModelText()) Then
                ShowTestResult("Enter a Claude model, such as claude-opus-5-5. Nothing was sent.", UiTheme.WarningColor())
                Return
            End If

            If claude AndAlso Not row.WillHaveKey Then
                ShowTestResult("Add your Claude key first. Nothing was sent.", UiTheme.InfoColor())
                Return
            End If

            Dim trial As New AssistantTrial With {
                .Connection = connection,
                .PendingKey = row.PendingKey,
                .UseStoredKey = row.WillHaveKey AndAlso row.PendingKey Is Nothing
            }

            Dim generation As Integer = _testGeneration
            Dim cancellation As New System.Threading.CancellationTokenSource()
            _testCancellation = cancellation
            btnTestConnection.Enabled = False
            ShowTestResult("Testing the connection...", UiTheme.MutedText())

            Try

                Dim result As AssistantTestResult = Await AssistantConnectionTest.RunAsync(trial, cancellation.Token)
                If IsDisposed OrElse generation <> _testGeneration Then Return

                ' Claude's list becomes the Model list; the model typed stays.
                If claude AndAlso result.Models.Count > 0 Then ShowClaudeModels(result.Models)

                ShowTestResult(result.Message, If(result.Succeeded, UiTheme.PrimaryText(), UiTheme.WarningColor()))

            Catch ex As Exception

                If IsDisposed OrElse generation <> _testGeneration Then Return
                ShowTestResult(AssistantRunner.DescribeTest(ex, connection), UiTheme.InfoColor())

            Finally

                cancellation.Dispose()
                _testCancellation = Nothing
                If Not IsDisposed Then RefreshTestState()

            End Try

        End Function


        Private Sub ShowClaudeModels(models As IEnumerable(Of String))

            Dim typed As String = cboClaudeModel.Text
            _listingModels = True
            cboClaudeModel.BeginUpdate()
            Try
                cboClaudeModel.Items.Clear()
                cboClaudeModel.Items.AddRange(models.Cast(Of Object)().ToArray())
            Finally
                cboClaudeModel.EndUpdate()
                cboClaudeModel.Text = typed
                If Not cboClaudeModel.Focused Then cboClaudeModel.SelectionLength = 0
                _listingModels = False
            End Try

        End Sub


        Private Sub RefreshChoices()

            btnForgetChoices.Enabled = _confirmedUses > 0 AndAlso Not _forgetChoices

            lblChoices.Text =
                If(_forgetChoices,
                   "Will be forgotten when you save.",
                   If(_confirmedUses = 0,
                      "PaperRoute asks every time before sending to a service elsewhere.",
                      "Don't Ask Again choices saved: " &
                      _confirmedUses.ToString(Globalization.CultureInfo.CurrentCulture) &
                      ". Each is for one feature with one service."))

        End Sub


        Private Sub AddKey(row As KeyRow)

            Dim key As String

            If row Is openAlexKey Then
                If keyPrompt IsNot Nothing Then
                    key = keyPrompt(Me)
                Else
                    Using dialog As New OpenAlexKeyForm()
                        key = If(dialog.ShowDialog(Me) = DialogResult.OK, dialog.Key, Nothing)
                    End Using
                End If
            ElseIf assistantKeyPrompt IsNot Nothing Then
                key = assistantKeyPrompt(Me, row.Name)
            Else
                Using dialog As ApiKeyForm = ApiKeyForm.ForAssistant(row.Name)
                    key = If(dialog.ShowDialog(Me) = DialogResult.OK, dialog.Key, Nothing)
                End Using
            End If

            If String.IsNullOrWhiteSpace(key) OrElse Not ProtectedKeyStore.IsPlausibleKey(key.Trim()) Then Return

            row.PendingKey = key.Trim()
            row.RemoveRequested = False
            _keyEdits += 1
            RefreshKeyState()

        End Sub


        Private Sub OpenAnthropicKeys(sender As Object, e As LinkLabelLinkClickedEventArgs)

            Try
                UrlSafetyService.OpenInBrowser(ApiKeyForm.AnthropicKeyPage)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open Anthropic", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try

        End Sub


        Private Function ClaudeModelText() As String

            Dim model As String = cboClaudeModel.Text.Trim()
            Return If(model.Length = 0, ClaudeModels(0), model)

        End Function


        Private Shared Function IsUsableClaudeModel(model As String) As Boolean

            Return System.Text.RegularExpressions.Regex.IsMatch(If(model, String.Empty), "^[A-Za-z0-9._:@-]{1,100}$")

        End Function


        ' Only an assistant PaperRoute can use is saved as turned on (#84).
        Private Function AssistantChoicesAreUsable() As Boolean

            Dim endpoint As Uri = OnlineAccess.ParseAssistantEndpoint(txtEndpoint.Text)

            If chkAssistant.Checked AndAlso rbCompatible.Checked Then
                If endpoint Is Nothing Then Return Refuse(EndpointCheck(txtEndpoint.Text).Message, txtEndpoint)
                If txtEndpointModel.Text.Trim().Length = 0 Then Return Refuse("Enter the model the server should use, such as llama3.1.", txtEndpointModel)
            End If

            If chkAssistant.Checked AndAlso rbClaude.Checked AndAlso Not IsUsableClaudeModel(ClaudeModelText()) Then
                Return Refuse("Enter a Claude model, such as claude-opus-5-5.", cboClaudeModel)
            End If

            If serverKey.PendingKey IsNot Nothing AndAlso endpoint Is Nothing Then
                Return Refuse("Enter the server's address, or remove the server key, before saving.", txtEndpoint)
            End If

            Return True

        End Function


        Private Function Refuse(message As String, field As Control) As Boolean

            ShowProblem(message, "AI Assistant", MessageBoxIcon.Warning)
            If field.CanFocus Then field.Focus()
            Return False

        End Function


        Private Sub ShowProblem(message As String, title As String, icon As MessageBoxIcon)

            If problemNotice IsNot Nothing Then
                problemNotice(message)
            Else
                MessageBox.Show(Me, message, title, MessageBoxButtons.OK, icon)
            End If

        End Sub


        Private Shared Function HasStoredKey(name As String) As Boolean

            Try
                Return OnlineAccess.KeyStore().HasKey(name)
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return False
            End Try

        End Function


        ' The address a stored key was added for, or "".
        Private Shared Function StoredKeyOrigin(name As String) As String

            Try
                Return If(OnlineAccess.KeyStore().OriginOf(name), String.Empty)
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return String.Empty
            End Try

        End Function


        ' An editable list that keeps what was typed when it is resized.
        ' Windows otherwise replaces the text with the first item that
        ' starts with it, so claude-opus-5 would become claude-opus-5-5, or
        ' an alias the full name Test Connection listed (#96).
        Private NotInheritable Class SteadyComboBox
            Inherits ComboBox

            Private Const WM_SIZE As Integer = &H5
            Private _resizing As Boolean = False

            Protected Overrides Sub WndProc(ByRef m As Message)

                If m.Msg <> WM_SIZE OrElse DropDownStyle <> ComboBoxStyle.DropDown Then
                    MyBase.WndProc(m)
                    Return
                End If

                Dim typed As String = MyBase.Text
                _resizing = True
                Try
                    MyBase.WndProc(m)
                    If Not String.Equals(MyBase.Text, typed, StringComparison.Ordinal) Then MyBase.Text = typed
                Finally
                    _resizing = False
                End Try

            End Sub

            Protected Overrides Sub OnTextChanged(e As EventArgs)

                If Not _resizing Then MyBase.OnTextChanged(e)

            End Sub

        End Class


        ' One key's row: whether one is stored, and a change that waits for Save.
        Private NotInheritable Class KeyRow

            Public ReadOnly Name As String
            Public ReadOnly DisplayName As String
            Public ReadOnly Status As New Label()
            Public ReadOnly AddButton As New Button()
            Public ReadOnly RemoveButton As New Button()
            Private ReadOnly _addText As String
            Private ReadOnly _replaceText As String

            Public HasKey As Boolean
            ' Nothing, or the key to add on Save.
            Public PendingKey As String
            Public RemoveRequested As Boolean
            ' A stored server key added for an address other than the one set.
            Public Outdated As Boolean

            Public Sub New(name As String, displayName As String, addText As String, replaceText As String, removeText As String)
                Me.Name = name
                Me.DisplayName = displayName
                _addText = addText
                _replaceText = replaceText
                RemoveButton.Text = removeText
            End Sub

            Public ReadOnly Property WillHaveKey As Boolean
                Get
                    Return PendingKey IsNot Nothing OrElse (HasKey AndAlso Not RemoveRequested AndAlso Not Outdated)
                End Get
            End Property

            Public ReadOnly Property WillRemove As Boolean
                Get
                    Return PendingKey Is Nothing AndAlso HasKey AndAlso (RemoveRequested OrElse Outdated)
                End Get
            End Property

            Public Sub Refresh()
                Status.Text =
                    If(PendingKey IsNot Nothing, "Will be added when you save",
                       If(HasKey AndAlso RemoveRequested, "Will be removed when you save",
                          If(HasKey AndAlso Outdated, "Will be removed when you save: it wasn't added for this address",
                             If(HasKey, "Added", "Not added"))))
                AddButton.Text = If(WillHaveKey, _replaceText, _addText)
                RemoveButton.Visible = WillHaveKey
            End Sub

        End Class


        Private Sub ConfigureNumber(
            numeric As NumericUpDown,
            minimum As Integer,
            maximum As Integer
        )

            numeric.Minimum = minimum
            numeric.Maximum = maximum
            numeric.Width = 70

        End Sub


        Private Sub AddSettingRow(
            grid As TableLayoutPanel,
            row As Integer,
            description As String,
            numeric As NumericUpDown,
            suffix As String
        )

            While grid.RowStyles.Count <= row
                grid.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )
            End While

            Dim label As New Label With {
                .Text = description,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 10, 8, 10)
            }

            numeric.Anchor = AnchorStyles.Left
            numeric.Margin = New Padding(0, 7, 0, 7)

            Dim suffixLabel As New Label With {
                .Text = suffix,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(8, 10, 0, 10)
            }

            grid.Controls.Add(label, 0, row)
            grid.Controls.Add(numeric, 1, row)
            grid.Controls.Add(suffixLabel, 2, row)

        End Sub


        Private Sub LoadValues()

            Select Case _settings.Appearance

                Case AppAppearance.Light
                    rbLight.Checked = True

                Case AppAppearance.Dark
                    rbDark.Checked = True

                Case Else
                    rbSystem.Checked = True

            End Select

            numFileDrawerThreshold.Value =
                _settings.FileDrawerSuggestionThreshold

            numLongReview.Value =
                _settings.LongReviewThresholdDays

            numRevisionWarning.Value =
                _settings.RevisionWarningDays

            numRecentRejection.Value =
                _settings.RecentRejectionThresholdDays

            chkReminderNotifications.Checked =
                _settings.ReminderNotificationsEnabled

            numReminderNotificationDays.Value =
                _settings.ReminderNotificationDaysAhead

            If _settings.UpdateChannel =
               AppUpdateChannel.Preview Then

                cboUpdateChannel.SelectedItem =
                    "Preview"

            Else

                cboUpdateChannel.SelectedItem =
                    "Stable"

            End If

            chkAutomaticUpdates.Checked =
                _settings.CheckForUpdatesAutomatically

            Dim online As OnlineServicesSettings = If(_settings.OnlineServices, New OnlineServicesSettings())
            chkWorkOffline.Checked = online.WorkOffline
            For Each pair In serviceChecks
                pair.Value.Checked = Not online.TurnedOff.Contains(pair.Key)
            Next

            Dim assistant As AssistantSettings = If(online.Assistant, New AssistantSettings())
            chkAssistant.Checked = assistant.Enabled
            rbCompatible.Checked = assistant.Provider = AssistantProvider.Compatible
            rbClaude.Checked = Not rbCompatible.Checked
            cboClaudeModel.Text = If(String.IsNullOrWhiteSpace(assistant.ClaudeModel), ClaudeModels(0), assistant.ClaudeModel.Trim())
            txtEndpoint.Text = If(assistant.Endpoint, String.Empty)
            txtEndpointModel.Text = If(assistant.EndpointModel, String.Empty)
            ' The address the stored server key is for comes from the key
            ' itself, which is what the gate goes by.
            _endpointKeyOrigin = StoredKeyOrigin(serverKey.Name)
            _confirmedUses = If(assistant.ConfirmedUses, New List(Of String)()).Where(Function(use) Not String.IsNullOrWhiteSpace(use)).Count()

            openAlexKey.HasKey = HasStoredKey(openAlexKey.Name)
            claudeKey.HasKey = HasStoredKey(claudeKey.Name)
            serverKey.HasKey = HasStoredKey(serverKey.Name)

            RefreshOnlineState()
            RefreshAssistantState()
            RefreshChoices()

        End Sub


        Private Function SelectedUpdateChannel() As AppUpdateChannel

            If String.Equals(
                CStr(cboUpdateChannel.SelectedItem),
                "Preview",
                StringComparison.OrdinalIgnoreCase
            ) Then

                Return AppUpdateChannel.Preview

            End If

            Return AppUpdateChannel.Stable

        End Function


        ' Writes the form's choices into a settings object.
        Private Sub ApplyTo(
            target As AppSettings
        )

            Dim newAppearance As AppAppearance

            If rbLight.Checked Then
                newAppearance = AppAppearance.Light
            ElseIf rbDark.Checked Then
                newAppearance = AppAppearance.Dark
            Else
                newAppearance = AppAppearance.System
            End If

            target.Appearance =
                newAppearance

            target.FileDrawerSuggestionThreshold =
                CInt(
                    numFileDrawerThreshold.Value
                )

            target.LongReviewThresholdDays =
                CInt(
                    numLongReview.Value
                )

            target.RevisionWarningDays =
                CInt(
                    numRevisionWarning.Value
                )

            target.RecentRejectionThresholdDays =
                CInt(
                    numRecentRejection.Value
                )

            target.ReminderNotificationsEnabled =
                chkReminderNotifications.Checked

            target.ReminderNotificationDaysAhead =
                CInt(
                    numReminderNotificationDays.Value
                )

            target.UpdateChannel =
                SelectedUpdateChannel()

            target.CheckForUpdatesAutomatically =
                chkAutomaticUpdates.Checked

            ' Ids this version doesn't know are kept, so a newer version's
            ' choices survive a visit to this page. The AI assistant is
            ' turned on and off below, never here.
            If target.OnlineServices Is Nothing Then target.OnlineServices = New OnlineServicesSettings()
            Dim turnedOff As List(Of String) =
                If(target.OnlineServices.TurnedOff, New List(Of String)()).
                    Where(Function(id) Not serviceChecks.ContainsKey(id) AndAlso Not IsAssistantService(id)).
                    ToList()
            turnedOff.AddRange(serviceChecks.Where(Function(pair) Not pair.Value.Checked).Select(Function(pair) pair.Key))
            target.OnlineServices.WorkOffline = chkWorkOffline.Checked
            target.OnlineServices.TurnedOff = turnedOff

            ' The AI assistant (#84).
            Dim endpoint As Uri = OnlineAccess.ParseAssistantEndpoint(txtEndpoint.Text)
            Dim assistant As AssistantSettings = If(target.OnlineServices.Assistant, New AssistantSettings())
            target.OnlineServices.Assistant = assistant
            ApplyAssistantTo(assistant)

            ' A server key is sent only to the address it was added for.
            If serverKey.PendingKey IsNot Nothing Then
                assistant.EndpointKeyOrigin = OnlineAccess.OriginOf(endpoint)
            ElseIf Not serverKey.WillHaveKey Then
                assistant.EndpointKeyOrigin = String.Empty
            End If

            If _forgetChoices Then assistant.ConfirmedUses = New List(Of String)()

        End Sub


        ' The AI assistant's service and model as chosen in this window.
        Private Sub ApplyAssistantTo(
            assistant As AssistantSettings
        )

            assistant.Enabled = chkAssistant.Checked
            assistant.Provider = If(rbCompatible.Checked, AssistantProvider.Compatible, AssistantProvider.Claude)
            assistant.ClaudeModel = ClaudeModelText()
            assistant.Endpoint = txtEndpoint.Text.Trim().TrimEnd("/"c)
            assistant.EndpointModel = txtEndpointModel.Text.Trim()

        End Sub


        Private Shared Function IsAssistantService(id As String) As Boolean

            Dim service As OnlineService = OnlineServiceCatalog.Find(If(id, String.Empty).Trim())
            Return service IsNot Nothing AndAlso service.OffUntilTurnedOn

        End Function


        Private Sub SaveSettings(
            sender As Object,
            e As EventArgs
        )

            If Not AssistantChoicesAreUsable() Then Return

            ' Saved from a copy, so a failed save changes nothing PaperRoute is
            ' using, even if the dialog is then cancelled.
            Dim candidate As AppSettings =
                JsonSerializer.Deserialize(Of AppSettings)(JsonSerializer.Serialize(_settings))

            ApplyTo(candidate)

            Try

                _settingsService.Save(
                    candidate
                )

            Catch ex As Exception

                ShowProblem(
                    "PaperRoute could not save the preferences." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Preferences Error",
                    MessageBoxIcon.Error
                )

                Return

            End Try

            _appearanceChanged =
                candidate.Appearance <>
                _settings.Appearance

            ApplyTo(_settings)

            ' Keys change only once the preferences are saved.
            For Each row As KeyRow In {openAlexKey, claudeKey, serverKey}

                If row.PendingKey Is Nothing AndAlso Not row.WillRemove Then Continue For

                Try

                    Dim keys As ProtectedKeyStore = OnlineAccess.KeyStore()
                    If row.PendingKey IsNot Nothing AndAlso row Is serverKey Then
                        ' Stored with the address it is for, in one encrypted
                        ' file: the key can never be sent anywhere else.
                        keys.SaveFor(row.Name, _settings.OnlineServices.Assistant.EndpointKeyOrigin, row.PendingKey)
                    ElseIf row.PendingKey IsNot Nothing Then
                        keys.Save(row.Name, row.PendingKey)
                    Else
                        keys.Remove(row.Name)
                    End If

                Catch ex As Exception When TypeOf ex Is IO.IOException OrElse
                                           TypeOf ex Is UnauthorizedAccessException OrElse
                                           TypeOf ex Is Security.Cryptography.CryptographicException OrElse
                                           TypeOf ex Is ArgumentException

                    ShowProblem(
                        "PaperRoute saved your preferences, but couldn't " &
                        If(row.PendingKey IsNot Nothing, "add", "remove") &
                        " the " & row.DisplayName & " key." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        Char.ToUpperInvariant(row.DisplayName(0)) & row.DisplayName.Substring(1) & " Key",
                        MessageBoxIcon.Warning
                    )

                End Try

            Next

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
