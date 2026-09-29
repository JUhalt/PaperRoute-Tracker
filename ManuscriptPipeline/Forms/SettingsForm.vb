Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
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
        Private ReadOnly lblKeyStatus As New Label()
        Private ReadOnly btnAddKey As New Button()
        Private ReadOnly btnRemoveKey As New Button()
        Private contentScroller As Panel = Nothing
        Private onlineGroup As Control = Nothing

        ' A key change waits for Save: Nothing, or the key to add.
        Private _pendingKey As String = Nothing
        Private _removeKey As Boolean = False
        Private _hasKey As Boolean = False

        ' Asks for an OpenAlex key; tests replace it.
        Friend keyPrompt As Func(Of IWin32Window, String) = Nothing

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
        ' at Online services.
        Friend Sub New(
            settings As AppSettings,
            settingsService As AppSettingsService,
            Optional showOnlineServices As Boolean = False
        )

            _settings =
                If(
                    settings,
                    New AppSettings()
                )

            _settingsService = If(settingsService, New AppSettingsService())
            _showOnlineServices = showOnlineServices

            BuildInterface()
            LoadValues()

            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnShown(e As EventArgs)

            MyBase.OnShown(e)

            If _showOnlineServices AndAlso contentScroller IsNot Nothing AndAlso onlineGroup IsNot Nothing Then
                contentScroller.AutoScrollPosition = New Point(0, onlineGroup.Top)
                chkWorkOffline.Focus()
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
                .RowCount = 6,
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

            For rowIndex As Integer = 0 To 5

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
                .Margin = New Padding(0, 0, 0, 4)
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
            chkWorkOffline.Font = New Font(Me.Font, FontStyle.Bold)
            chkWorkOffline.Margin = New Padding(0, 4, 0, 0)
            AddHandler chkWorkOffline.CheckedChanged, Sub(sender, e) RefreshOnlineState()
            addRow(chkWorkOffline, Nothing)

            Dim offlineHelp As Label = muted("Stops every service below, update checks included. Your choices below are kept for when you turn it off.", New Padding(22, 2, 0, 10))
            addRow(offlineHelp, Nothing)
            spanning.Add(offlineHelp)

            For Each service As OnlineService In OnlineServiceCatalog.Services

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
                    "Contacts: " & String.Join(", ", service.Hosts) & Environment.NewLine &
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

            Dim keyPanel As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 4, 0, 0)
            }

            lblKeyStatus.AutoSize = True
            lblKeyStatus.UseMnemonic = False
            lblKeyStatus.Margin = New Padding(0, 6, 12, 4)

            btnAddKey.AutoSize = True
            btnAddKey.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            btnAddKey.Margin = New Padding(0, 0, 8, 4)
            AddHandler btnAddKey.Click, AddressOf AddOpenAlexKey

            btnRemoveKey.Text = "Remove Key"
            btnRemoveKey.AutoSize = True
            btnRemoveKey.MinimumSize = New Size(UiTheme.Px(100, DeviceDpi), UiTheme.Px(32, DeviceDpi))
            btnRemoveKey.Margin = New Padding(0, 0, 0, 4)
            AddHandler btnRemoveKey.Click,
                Sub(sender, e)
                    _pendingKey = Nothing
                    _removeKey = _hasKey
                    RefreshKeyState()
                End Sub

            keyPanel.Controls.Add(lblKeyStatus)
            keyPanel.Controls.Add(btnAddKey)
            keyPanel.Controls.Add(btnRemoveKey)
            addRow(lblKey, keyPanel)

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

        End Sub


        Private Sub RefreshKeyState()

            Dim willHaveKey As Boolean = _pendingKey IsNot Nothing OrElse (_hasKey AndAlso Not _removeKey)

            lblKeyStatus.Text =
                If(_pendingKey IsNot Nothing, "Will be added when you save",
                   If(_removeKey, "Will be removed when you save",
                      If(_hasKey, "Added", "Not added")))

            btnAddKey.Text = If(willHaveKey, "Replace Key...", "Add Key...")
            btnRemoveKey.Visible = willHaveKey

        End Sub


        Private Sub AddOpenAlexKey(sender As Object, e As EventArgs)

            Dim key As String

            If keyPrompt IsNot Nothing Then
                key = keyPrompt(Me)
            Else
                Using dialog As New OpenAlexKeyForm()
                    key = If(dialog.ShowDialog(Me) = DialogResult.OK, dialog.Key, Nothing)
                End Using
            End If

            If String.IsNullOrWhiteSpace(key) OrElse Not ProtectedKeyStore.IsPlausibleKey(key.Trim()) Then Return

            _pendingKey = key.Trim()
            _removeKey = False
            RefreshKeyState()

        End Sub


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

            Try
                _hasKey = OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.OpenAlex)
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                _hasKey = False
            End Try

            RefreshOnlineState()
            RefreshKeyState()

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


        Private Sub SaveSettings(
            sender As Object,
            e As EventArgs
        )

            Dim newAppearance As AppAppearance

            If rbLight.Checked Then
                newAppearance = AppAppearance.Light
            ElseIf rbDark.Checked Then
                newAppearance = AppAppearance.Dark
            Else
                newAppearance = AppAppearance.System
            End If

            _appearanceChanged =
                newAppearance <>
                _settings.Appearance

            _settings.Appearance =
                newAppearance

            _settings.FileDrawerSuggestionThreshold =
                CInt(
                    numFileDrawerThreshold.Value
                )

            _settings.LongReviewThresholdDays =
                CInt(
                    numLongReview.Value
                )

            _settings.RevisionWarningDays =
                CInt(
                    numRevisionWarning.Value
                )

            _settings.RecentRejectionThresholdDays =
                CInt(
                    numRecentRejection.Value
                )

            _settings.ReminderNotificationsEnabled =
                chkReminderNotifications.Checked

            _settings.ReminderNotificationDaysAhead =
                CInt(
                    numReminderNotificationDays.Value
                )

            _settings.UpdateChannel =
                SelectedUpdateChannel()

            _settings.CheckForUpdatesAutomatically =
                chkAutomaticUpdates.Checked

            ' Ids this version doesn't know are kept, so a newer version's
            ' choices survive a visit to this page.
            If _settings.OnlineServices Is Nothing Then _settings.OnlineServices = New OnlineServicesSettings()
            Dim turnedOff As List(Of String) =
                _settings.OnlineServices.TurnedOff.
                    Where(Function(id) Not serviceChecks.ContainsKey(id)).
                    ToList()
            turnedOff.AddRange(serviceChecks.Where(Function(pair) Not pair.Value.Checked).Select(Function(pair) pair.Key))
            _settings.OnlineServices.WorkOffline = chkWorkOffline.Checked
            _settings.OnlineServices.TurnedOff = turnedOff

            Try

                Dim keys As ProtectedKeyStore = OnlineAccess.KeyStore()
                If _pendingKey IsNot Nothing Then
                    keys.Save(ProtectedKeyStore.OpenAlex, _pendingKey)
                ElseIf _removeKey Then
                    keys.Remove(ProtectedKeyStore.OpenAlex)
                End If

                _settingsService.Save(
                    _settings
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not save the preferences." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Preferences Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

                Return

            End Try

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
