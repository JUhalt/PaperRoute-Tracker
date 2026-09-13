Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Partial Public Class SubmissionPacketVaultForm
        Inherits Form

        Private ReadOnly _sourceManuscript As Manuscript
        Private ReadOnly _workingManuscript As Manuscript
        Private ReadOnly _managedLibrary As ManagedLibraryService

        Private ReadOnly lstPackets As New ListBox()
        Private ReadOnly txtPacketDetail As New TextBox()
        Private ReadOnly lstFiles As New ListBox()
        Private ReadOnly txtFileDetail As New TextBox()

        Private ReadOnly btnEditPacket As New Button()
        Private ReadOnly btnDeletePacket As New Button()

        Private ReadOnly btnAddFile As New Button()
        Private ReadOnly btnEditFile As New Button()
        Private ReadOnly btnRemoveFile As New Button()
        Private ReadOnly btnOpenFile As New Button()
        Private ReadOnly btnRecordFingerprint As New Button()
        Private ReadOnly btnCheckFiles As New Button()
        Private ReadOnly lblIntegritySummary As New Label()
        ' File identifiers are unique within a packet, not across every packet.
        ' Keep observations attached to their exact working-copy file record.
        Private ReadOnly _integrityResults As New Dictionary(Of SubmissionPacketFile, PacketFileIntegrityResult)()
        Private _integrityCancellation As CancellationTokenSource
        Private _integrityBusy As Boolean
        Private _workspace As SplitContainer

        Private ReadOnly _displayedPackets As New List(Of SubmissionPacket)()
        Private ReadOnly _displayedFiles As New List(Of SubmissionPacketFile)()


        Public Sub New(
            manuscript As Manuscript,
            Optional workflowContext As SubmissionWorkflowRequest = Nothing
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(
                    NameOf(manuscript)
                )
            End If

            _sourceManuscript =
                manuscript

            _workingManuscript =
                ManuscriptCloneService.CloneManuscript(
                    manuscript
                )

            _managedLibrary =
                New ManagedLibraryService()

            _workflowContext = CopyWorkflowContext(workflowContext)
            BuildInterface()
            UiPolish.ApplyDialog(Me)
            RefreshPackets(_workflowContext?.PacketId)

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Submission Packet Vault"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    1040,
                    760
                )

            Me.MinimumSize =
                New Size(
                    860,
                    640
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            Dim intro As New Label With {
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .Text =
                    "Assemble the files for an exact manuscript version. Preparing a packet does not record a submission. " &
                    "Keep managed copies, link external files, or track file metadata.",
                .Margin = New Padding(0, 0, 0, 12)
            }

            root.Controls.Add(
                intro,
                0,
                0
            )

            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical
            }
            _workspace = split

            Dim integrityBar As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 3,
                .RowCount = 1,
                .Margin = New Padding(0, 0, 0, 8)
            }
            integrityBar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            integrityBar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            integrityBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            integrityBar.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            btnCheckFiles.Text = "Check Files"
            btnCheckFiles.AutoSize = True
            btnCheckFiles.Height = 36
            btnCheckFiles.AccessibleName = "Check files in selected packet"
            AddHandler btnCheckFiles.Click, AddressOf CheckFilesClicked
            lblIntegritySummary.AutoSize = True
            lblIntegritySummary.Dock = DockStyle.Fill
            lblIntegritySummary.Margin = New Padding(10, 9, 0, 0)
            lblIntegritySummary.AccessibleName = "Packet integrity summary"
            integrityBar.Controls.Add(btnCheckFiles, 0, 0)
            integrityBar.Controls.Add(btnRecordFingerprint, 1, 0)
            integrityBar.Controls.Add(lblIntegritySummary, 2, 0)
            root.Controls.Add(integrityBar, 0, 1)

            BuildPacketPanel(
                split.Panel1
            )

            BuildFilePanel(
                split.Panel2
            )

            root.Controls.Add(
                split,
                0,
                2
            )

            Dim lastSplitWidth As Integer = -1
            AddHandler split.SizeChanged,
                Sub(senderObject As Object, eventArgs As EventArgs)
                    If split.ClientSize.Width = lastSplitWidth Then Return
                    lastSplitWidth = split.ClientSize.Width
                    Dim usableWidth As Integer = split.ClientSize.Width - split.SplitterWidth
                    If usableWidth >= split.Panel1MinSize + split.Panel2MinSize Then
                        split.SplitterDistance = Math.Max(split.Panel1MinSize,
                            Math.Min(usableWidth - split.Panel2MinSize, CInt(usableWidth * 0.42)))
                    End If
                End Sub

            Dim safety As New Label With {
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .UseMnemonic = False,
                .ForeColor = SystemColors.GrayText,
                .Text =
                    "Choose Save & Close here, then Save & Close in Manuscript Details to keep these changes. " &
                    "Linked external files are never deleted.",
                .Margin = New Padding(0, 10, 0, 0)
            }

            root.Controls.Add(
                safety,
                0,
                3
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 12, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save && Close",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveVault

            footer.Controls.Add(
                btnSave
            )

            footer.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                footer,
                0,
                4
            )

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub BuildPacketPanel(
            host As Control
        )

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(0, 0, 10, 0)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.Controls.Add(
                BuildWorkflowPacketHeading(),
                0,
                0
            )

            lstPackets.Dock =
                DockStyle.Fill
            lstPackets.IntegralHeight = False
            lstPackets.HorizontalScrollbar = True

            AddHandler lstPackets.SelectedIndexChanged,
                AddressOf PacketSelectionChanged

            AddHandler lstPackets.DoubleClick,
                AddressOf EditPacket

            root.Controls.Add(
                lstPackets,
                0,
                1
            )

            txtPacketDetail.Dock = DockStyle.Fill
            txtPacketDetail.Multiline = True
            txtPacketDetail.ReadOnly = True
            txtPacketDetail.ScrollBars = ScrollBars.Vertical
            txtPacketDetail.AccessibleName = "Selected packet details"

            txtPacketDetail.ForeColor =
                SystemColors.GrayText

            txtPacketDetail.Margin =
                New Padding(
                    0,
                    8,
                    0,
                    0
                )

            root.Controls.Add(
                txtPacketDetail,
                0,
                2
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnNew As New Button With {
                .Text = "New Packet...",
                .AutoSize = True,
                .Height = 36
            }

            btnEditPacket.Text =
                "Edit"

            btnEditPacket.AutoSize =
                True

            btnEditPacket.Height =
                36

            btnDeletePacket.Text =
                "Delete"

            btnDeletePacket.AutoSize =
                True

            btnDeletePacket.Height =
                36

            AddHandler btnNew.Click,
                AddressOf NewPacket

            AddHandler btnEditPacket.Click,
                AddressOf EditPacket

            AddHandler btnDeletePacket.Click,
                AddressOf DeletePacket

            buttons.Controls.Add(
                btnNew
            )

            buttons.Controls.Add(
                btnEditPacket
            )

            buttons.Controls.Add(
                btnDeletePacket
            )

            AddWorkflowNavigation(buttons)

            root.Controls.Add(
                buttons,
                0,
                3
            )

            host.Controls.Add(
                root
            )

        End Sub


        Private Sub BuildFilePanel(
            host As Control
        )

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(10, 0, 0, 0)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.Controls.Add(
                New Label With {
                    .Text = "Packet Files",
                    .AutoSize = True,
                    .Font = New Font(
                        Me.Font,
                        FontStyle.Bold
                    ),
                    .Margin = New Padding(0, 0, 0, 8)
                },
                0,
                0
            )

            lstFiles.Dock =
                DockStyle.Fill
            lstFiles.IntegralHeight = False
            lstFiles.HorizontalScrollbar = True

            AddHandler lstFiles.SelectedIndexChanged,
                AddressOf FileSelectionChanged

            AddHandler lstFiles.DoubleClick,
                AddressOf EditFile

            root.Controls.Add(
                lstFiles,
                0,
                1
            )

            txtFileDetail.Dock = DockStyle.Fill
            txtFileDetail.Multiline = True
            txtFileDetail.ReadOnly = True
            txtFileDetail.ScrollBars = ScrollBars.Vertical
            txtFileDetail.AccessibleName = "Selected file details"

            txtFileDetail.ForeColor =
                SystemColors.GrayText

            txtFileDetail.Margin =
                New Padding(
                    0,
                    8,
                    0,
                    0
                )

            root.Controls.Add(
                txtFileDetail,
                0,
                2
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            btnAddFile.Text =
                "Add File..."

            btnAddFile.AutoSize =
                True

            btnAddFile.Height =
                36

            btnEditFile.Text =
                "Edit"

            btnEditFile.AutoSize =
                True

            btnEditFile.Height =
                36

            btnRemoveFile.Text =
                "Remove"

            btnRemoveFile.AutoSize =
                True

            btnRemoveFile.Height =
                36

            btnOpenFile.Text =
                "Open"

            btnOpenFile.AutoSize =
                True

            btnOpenFile.Height =
                36

            btnRecordFingerprint.Text = "Record Fingerprint"
            btnRecordFingerprint.AutoSize = True
            btnRecordFingerprint.Height = 36
            btnRecordFingerprint.AccessibleName = "Record or replace selected file fingerprint"
            AddHandler btnRecordFingerprint.Click, AddressOf RecordFingerprintClicked

            AddHandler btnAddFile.Click,
                AddressOf AddFile

            AddHandler btnEditFile.Click,
                AddressOf EditFile

            AddHandler btnRemoveFile.Click,
                AddressOf RemoveFile

            AddHandler btnOpenFile.Click,
                AddressOf OpenFile

            buttons.Controls.Add(
                btnAddFile
            )

            buttons.Controls.Add(
                btnEditFile
            )

            buttons.Controls.Add(
                btnRemoveFile
            )

            buttons.Controls.Add(
                btnOpenFile
            )

            root.Controls.Add(
                buttons,
                0,
                3
            )

            host.Controls.Add(
                root
            )

        End Sub


        Private Sub RefreshPackets(
            Optional selectedPacketId As Guid? = Nothing
        )

            If Not selectedPacketId.HasValue Then

                Dim current As SubmissionPacket =
                    GetSelectedPacket()

                If current IsNot Nothing Then
                    selectedPacketId =
                        current.Id
                End If

            End If

            _displayedPackets.Clear()
            lstPackets.Items.Clear()

            If _workingManuscript.SubmissionPackets IsNot Nothing Then

                For Each packet As SubmissionPacket In
                    _workingManuscript.SubmissionPackets.
                        Where(
                            Function(item)
                                Return item IsNot Nothing AndAlso MatchesWorkflowScope(item)
                            End Function
                        ).
                        OrderByDescending(
                            Function(item)
                                Return item.CreatedAtUtc
                            End Function
                        )

                    _displayedPackets.Add(
                        packet
                    )

                    lstPackets.Items.Add(
                        FormatPacket(
                            packet
                        )
                    )

                Next

            End If

            If _displayedPackets.Count > 0 Then

                Dim selectedIndex As Integer =
                    0

                If selectedPacketId.HasValue Then

                    For index As Integer =
                        0 To _displayedPackets.Count - 1

                        If _displayedPackets(index).Id =
                           selectedPacketId.Value Then

                            selectedIndex =
                                index

                            Exit For

                        End If

                    Next

                End If

                lstPackets.SelectedIndex =
                    selectedIndex

            Else

                RefreshFiles()
                txtPacketDetail.Text =
                    If(HasWorkflowFilter(),
                       "No packets match this scope. Create a packet or choose Show all packets.",
                       "No packet yet. Create one from an exact Version History snapshot.")

            End If

            UpdatePacketButtons()
            UpdateWorkflowScopeLabel()

        End Sub


        Private Function FormatPacket(
            packet As SubmissionPacket
        ) As String

            Dim journal As String =
                If(
                    String.IsNullOrWhiteSpace(
                        packet.JournalName
                    ),
                    "No journal yet",
                    packet.JournalName.Trim()
                )

            Dim label As String =
                If(
                    String.IsNullOrWhiteSpace(
                        packet.Label
                    ),
                    "Submission packet",
                    packet.Label.Trim()
                )

            Return (
                label &
                "  •  " &
                journal
            )

        End Function


        Private Function GetSelectedPacket() As SubmissionPacket

            Dim index As Integer =
                lstPackets.SelectedIndex

            If index < 0 OrElse
               index >= _displayedPackets.Count Then

                Return Nothing

            End If

            Return _displayedPackets(index)

        End Function


        Private Sub PacketSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            If packet Is Nothing Then

                txtPacketDetail.Text =
                    String.Empty

            Else

                txtPacketDetail.Text =
                    BuildPacketDetail(
                        packet
                    )

            End If

            RefreshFiles()
            UpdatePacketButtons()

        End Sub


        Private Function BuildPacketDetail(
            packet As SubmissionPacket
        ) As String

            Return BuildWorkflowPacketDetail(packet)

        End Function


        Private Sub RefreshFiles(
            Optional selectedFileId As Guid? = Nothing
        )

            If Not selectedFileId.HasValue Then

                Dim current As SubmissionPacketFile =
                    GetSelectedFile()

                If current IsNot Nothing Then
                    selectedFileId =
                        current.Id
                End If

            End If

            _displayedFiles.Clear()
            lstFiles.Items.Clear()

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            If packet Is Nothing OrElse
               packet.Files Is Nothing Then

                txtFileDetail.Text =
                    If(
                        packet Is Nothing,
                        "Select or create a packet first.",
                        "This packet has no file records yet."
                    )

                UpdateFileButtons()
                Return

            End If

            For Each packetFile As SubmissionPacketFile In
                packet.Files.
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    OrderBy(
                        Function(item)
                            Return CInt(
                                item.Role
                            )
                        End Function
                    ).
                    ThenBy(
                        Function(item)
                            Return item.Label
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                _displayedFiles.Add(
                    packetFile
                )

                lstFiles.Items.Add(
                    FormatFile(
                        packetFile
                    )
                )

            Next

            If _displayedFiles.Count > 0 Then

                Dim selectedIndex As Integer =
                    0

                If selectedFileId.HasValue Then

                    For index As Integer =
                        0 To _displayedFiles.Count - 1

                        If _displayedFiles(index).Id =
                           selectedFileId.Value Then

                            selectedIndex =
                                index

                            Exit For

                        End If

                    Next

                End If

                lstFiles.SelectedIndex =
                    selectedIndex

            Else

                txtFileDetail.Text =
                    "This packet has no file records yet."

            End If

            UpdateFileButtons()

        End Sub


        Private Function FormatFile(
            packetFile As SubmissionPacketFile
        ) As String

            Return (
                "[" & IntegrityStatusText(GetIntegrityResult(packetFile).Status) & "]  " &
                FriendlyStorageName(
                    packetFile
                ) &
                "  •  " &
                SubmissionPacketService.FriendlyRoleName(
                    packetFile.Role
                ) &
                "  —  " &
                If(
                    String.IsNullOrWhiteSpace(
                        packetFile.Label
                    ),
                    "(Unlabeled file)",
                    packetFile.Label
                )
            )

        End Function


        Private Function FriendlyStorageName(
            packetFile As SubmissionPacketFile
        ) As String

            Select Case packetFile.StorageMode

                Case SubmissionPacketFileStorageMode.LinkedExternal
                    Return "Linked"

                Case SubmissionPacketFileStorageMode.ManagedCopy

                    If _managedLibrary.IsManagedPath(
                        packetFile.LocalFilePath
                    ) Then

                        Return "Managed"

                    End If

                    Return "Managed (pending)"

                Case Else
                    Return "Metadata"

            End Select

        End Function


        Private Function GetSelectedFile() As SubmissionPacketFile

            Dim index As Integer =
                lstFiles.SelectedIndex

            If index < 0 OrElse
               index >= _displayedFiles.Count Then

                Return Nothing

            End If

            Return _displayedFiles(index)

        End Function


        Private Sub FileSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            Dim packetFile As SubmissionPacketFile =
                GetSelectedFile()

            If packetFile Is Nothing Then

                txtFileDetail.Text =
                    String.Empty

            Else

                Dim pathText As String =
                    If(
                        String.IsNullOrWhiteSpace(
                            packetFile.LocalFilePath
                        ),
                        "No local file — metadata only.",
                        packetFile.LocalFilePath
                    )

                Dim notesText As String =
                    If(
                        String.IsNullOrWhiteSpace(
                            packetFile.Notes
                        ),
                        String.Empty,
                        Environment.NewLine &
                            "Notes: " &
                            packetFile.Notes
                    )

                txtFileDetail.Text =
                    packetFile.Label & Environment.NewLine &
                    BuildIntegrityDetail(packetFile) & Environment.NewLine & Environment.NewLine &
                    pathText &
                    notesText

            End If

            UpdateFileButtons()

        End Sub


        Private Sub UpdatePacketButtons()

            Dim hasPacket As Boolean =
                GetSelectedPacket() IsNot Nothing

            btnEditPacket.Enabled =
                hasPacket

            btnDeletePacket.Enabled =
                hasPacket

            btnAddFile.Enabled =
                hasPacket

            UpdateWorkflowNavigation()

        End Sub


        Private Sub UpdateFileButtons()

            Dim packetFile As SubmissionPacketFile =
                GetSelectedFile()

            Dim hasFile As Boolean =
                packetFile IsNot Nothing

            btnEditFile.Enabled =
                hasFile

            btnRemoveFile.Enabled =
                hasFile

            btnOpenFile.Enabled =
                hasFile AndAlso
                Not String.IsNullOrWhiteSpace(
                    packetFile.LocalFilePath
                ) AndAlso
                File.Exists(
                    packetFile.LocalFilePath
                )

            btnRecordFingerprint.Enabled = Not _integrityBusy AndAlso hasFile AndAlso
                packetFile.StorageMode <> SubmissionPacketFileStorageMode.MetadataOnly AndAlso
                Not String.IsNullOrWhiteSpace(packetFile.LocalFilePath)
            btnRecordFingerprint.Text = If(hasFile AndAlso Not String.IsNullOrWhiteSpace(packetFile.Sha256),
                "Replace Fingerprint...", "Record Fingerprint")
            UpdateIntegritySummary()

        End Sub


        Private Sub NewPacket(
            sender As Object,
            e As EventArgs
        )

            If _workingManuscript.Versions Is Nothing OrElse
               _workingManuscript.Versions.Count = 0 Then

                MessageBox.Show(
                    Me,
                    "Create a Version History record first. A Submission Packet must identify the exact manuscript snapshot being prepared.",
                    "Version History Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim existingIds As New HashSet(Of Guid)(
                _workingManuscript.SubmissionPackets.
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    Select(
                        Function(item)
                            Return item.Id
                        End Function
                    )
            )

            Using dialog As New SubmissionPacketEditForm(
                _workingManuscript,
                Nothing,
                _workflowContext
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

            End Using

            Dim created As SubmissionPacket =
                _workingManuscript.SubmissionPackets.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                Not existingIds.Contains(
                                    item.Id
                                )
                        End Function
                    )

            EnsureWorkflowPacketVisible(created)
            RefreshPackets(
                If(
                    created Is Nothing,
                    Nothing,
                    CType(created.Id, Guid?)
                )
            )

        End Sub


        Private Sub EditPacket(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            If packet Is Nothing Then
                Return
            End If

            Using dialog As New SubmissionPacketEditForm(
                _workingManuscript,
                packet
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

            End Using

            EnsureWorkflowPacketVisible(packet)
            RefreshPackets(
                packet.Id
            )

        End Sub


        Private Sub DeletePacket(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            If packet Is Nothing Then
                Return
            End If

            Dim managedCount As Integer =
                If(
                    packet.Files Is Nothing,
                    0,
                    packet.Files.
                        Where(
                            Function(item)
                                Return item IsNot Nothing AndAlso
                                    SubmissionPacketService.IsCommittedManagedFile(
                                        item,
                                        _managedLibrary
                                    )
                            End Function
                        ).
                        Count()
                )

            Dim message As String =
                "Delete '" &
                packet.Label &
                "' from this manuscript?"

            If managedCount > 0 Then

                message &=
                    Environment.NewLine &
                    Environment.NewLine &
                    managedCount.ToString() &
                    " managed file(s) will be staged for physical deletion only when Manuscript Details is also saved. Canceling the outer manuscript edit leaves the stored files intact."

            End If

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    message,
                    "Delete Submission Packet",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <>
               DialogResult.Yes Then

                Return

            End If

            SubmissionPacketService.RemovePacket(
                _workingManuscript,
                packet.Id,
                _managedLibrary
            )

            RefreshPackets()

        End Sub


        Private Sub AddFile(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            If packet Is Nothing Then
                Return
            End If

            Dim existingIds As New HashSet(Of Guid)(
                If(
                    packet.Files,
                    New List(Of SubmissionPacketFile)()
                ).
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    Select(
                        Function(item)
                            Return item.Id
                        End Function
                    )
            )

            Using dialog As New SubmissionPacketFileEditForm(
                packet,
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

            End Using

            Dim added As SubmissionPacketFile =
                packet.Files.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                Not existingIds.Contains(
                                    item.Id
                                )
                        End Function
                    )

            RefreshFiles(
                If(
                    added Is Nothing,
                    Nothing,
                    CType(added.Id, Guid?)
                )
            )

            RefreshPackets(
                packet.Id
            )

        End Sub


        Private Sub EditFile(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            Dim packetFile As SubmissionPacketFile =
                GetSelectedFile()

            If packet Is Nothing OrElse
               packetFile Is Nothing Then

                Return

            End If

            Using dialog As New SubmissionPacketFileEditForm(
                packet,
                packetFile
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

            End Using

            RefreshFiles(
                packetFile.Id
            )

        End Sub


        Private Sub RemoveFile(
            sender As Object,
            e As EventArgs
        )

            Dim packet As SubmissionPacket =
                GetSelectedPacket()

            Dim packetFile As SubmissionPacketFile =
                GetSelectedFile()

            If packet Is Nothing OrElse
               packetFile Is Nothing Then

                Return

            End If

            Dim isManaged As Boolean =
                SubmissionPacketService.IsCommittedManagedFile(
                    packetFile,
                    _managedLibrary
                )

            Dim message As String =
                "Remove '" &
                packetFile.Label &
                "' from this Submission Packet?"

            If isManaged Then

                message &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "Its PaperRoute Library snapshot will be staged for deletion only when Manuscript Details is also saved. The original source file, if one still exists elsewhere, is never deleted."

            ElseIf packetFile.StorageMode =
                   SubmissionPacketFileStorageMode.LinkedExternal Then

                message &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "The external source file will not be deleted."

            End If

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    message,
                    "Remove Packet File",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <>
               DialogResult.Yes Then

                Return

            End If

            SubmissionPacketService.RemoveFile(
                packet,
                packetFile.Id,
                _managedLibrary
            )

            RefreshFiles()
            RefreshPackets(
                packet.Id
            )

        End Sub


        Private Sub OpenFile(
            sender As Object,
            e As EventArgs
        )

            Dim packetFile As SubmissionPacketFile =
                GetSelectedFile()

            If packetFile Is Nothing OrElse
               String.IsNullOrWhiteSpace(
                   packetFile.LocalFilePath
               ) Then

                Return

            End If

            If Not File.Exists(
                packetFile.LocalFilePath
            ) Then

                MessageBox.Show(
                    Me,
                    "PaperRoute cannot find this file at the recorded path:" &
                    Environment.NewLine &
                    Environment.NewLine &
                    packetFile.LocalFilePath,
                    "Packet File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Try

                Process.Start(
                    New ProcessStartInfo(
                        packetFile.LocalFilePath
                    ) With {
                        .UseShellExecute = True
                    }
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not open the file." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Open Packet File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Function GetIntegrityResult(packetFile As SubmissionPacketFile) As PacketFileIntegrityResult

            Dim result As PacketFileIntegrityResult = Nothing
            If Not _integrityResults.TryGetValue(packetFile, result) Then
                result = SubmissionPacketIntegrityService.Inspect(packetFile)
                _integrityResults(packetFile) = result
            End If
            Return result

        End Function


        Private Shared Function IntegrityStatusText(status As PacketFileIntegrityStatus) As String

            Select Case status
                Case PacketFileIntegrityStatus.MetadataOnly : Return "Metadata only"
                Case PacketFileIntegrityStatus.NotRecorded : Return "No fingerprint"
                Case PacketFileIntegrityStatus.NotChecked : Return "Not checked"
                Case PacketFileIntegrityStatus.Missing : Return "Missing"
                Case PacketFileIntegrityStatus.Unavailable : Return "Unavailable"
                Case PacketFileIntegrityStatus.Unchanged : Return "Unchanged"
                Case PacketFileIntegrityStatus.Changed : Return "Changed"
                Case Else : Return "Not checked"
            End Select

        End Function


        Private Function BuildIntegrityDetail(packetFile As SubmissionPacketFile) As String

            Dim result As PacketFileIntegrityResult = GetIntegrityResult(packetFile)
            Dim detail As String = "Status: " & IntegrityStatusText(result.Status) & Environment.NewLine & result.Message
            If result.CheckedAtUtc.HasValue Then
                detail &= Environment.NewLine & "Last checked: " & result.CheckedAtUtc.Value.ToLocalTime().ToString("g")
            End If
            If Not String.IsNullOrWhiteSpace(packetFile.Sha256) Then
                detail &= Environment.NewLine & "Saved SHA-256: " & packetFile.Sha256
                If packetFile.HashComputedAtUtc.HasValue Then
                    detail &= Environment.NewLine & "Fingerprint recorded: " & packetFile.HashComputedAtUtc.Value.ToLocalTime().ToString("g")
                End If
            End If
            If result.Status = PacketFileIntegrityStatus.Changed AndAlso Not String.IsNullOrWhiteSpace(result.Sha256) Then
                detail &= Environment.NewLine & "Observed SHA-256: " & result.Sha256
            End If
            If packetFile.StorageMode <> SubmissionPacketFileStorageMode.MetadataOnly Then
                detail &= Environment.NewLine & "Results reflect the last observation. Check Files again after editing a file."
            End If
            Return detail

        End Function


        Private Sub UpdateIntegritySummary()

            If _integrityBusy Then Return
            Dim packet As SubmissionPacket = GetSelectedPacket()
            btnCheckFiles.Enabled = packet IsNot Nothing AndAlso packet.Files IsNot Nothing AndAlso packet.Files.Count > 0
            If Not btnCheckFiles.Enabled Then
                lblIntegritySummary.Text = "Select a packet with files to check."
                Return
            End If

            Dim counts = packet.Files.Where(Function(item) item IsNot Nothing).
                GroupBy(Function(item) GetIntegrityResult(item).Status).
                OrderBy(Function(group) CInt(group.Key)).
                Select(Function(group) group.Count().ToString() & " " & IntegrityStatusText(group.Key).ToLowerInvariant())
            lblIntegritySummary.Text = "Last observation: " & String.Join(" · ", counts)

        End Sub


        Private Async Sub CheckFilesClicked(sender As Object, e As EventArgs)
            Await CheckPacketFilesAsync()
        End Sub


        Private Async Sub RecordFingerprintClicked(sender As Object, e As EventArgs)

            Dim packetFile As SubmissionPacketFile = GetSelectedFile()
            If packetFile Is Nothing OrElse _integrityBusy Then Return
            Dim replaceExisting As Boolean = Not String.IsNullOrWhiteSpace(packetFile.Sha256)
            If replaceExisting Then
                Dim answer = MessageBox.Show(Me,
                    "Replace the saved fingerprint for '" & packetFile.Label & "' with the file's current contents?" &
                    Environment.NewLine & Environment.NewLine &
                    "Future checks will use the new fingerprint. The previous comparison point will be lost when you save these changes. " &
                    "To preserve the earlier submitted file, keep its packet record and add a new file record instead.",
                    "Replace File Fingerprint", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                If answer <> DialogResult.Yes Then Return
            End If
            Await RecordSelectedFingerprintAsync(replaceExisting)

        End Sub


        Friend Function CheckPacketFilesAsync() As Task
            Return RunIntegrityOperationAsync(False, False)
        End Function


        Friend Function RecordSelectedFingerprintAsync(replaceExisting As Boolean) As Task
            Return RunIntegrityOperationAsync(True, replaceExisting)
        End Function


        Private Async Function RunIntegrityOperationAsync(recordBaseline As Boolean, replaceExisting As Boolean) As Task

            If _integrityBusy Then Return
            Dim packet As SubmissionPacket = GetSelectedPacket()
            Dim selectedFile As SubmissionPacketFile = GetSelectedFile()
            If packet Is Nothing OrElse packet.Files Is Nothing Then Return
            If recordBaseline AndAlso selectedFile Is Nothing Then Return

            ' Work on snapshots only. Closing/canceling the dialog cannot leave a
            ' background hash operation mutating its manuscript working copy.
            Dim snapshots As List(Of SubmissionPacketFile) =
                packet.Files.Where(Function(item) item IsNot Nothing AndAlso
                    (Not recordBaseline OrElse item.Id = selectedFile.Id)).Select(
                    Function(item) New SubmissionPacketFile With {
                        .Id = item.Id, .StorageMode = item.StorageMode, .LocalFilePath = item.LocalFilePath,
                        .Sha256 = item.Sha256, .FileSizeBytes = item.FileSizeBytes,
                        .LastWriteTimeUtc = item.LastWriteTimeUtc, .HashComputedAtUtc = item.HashComputedAtUtc
                    }).ToList()
            If snapshots.Count = 0 Then Return
            Dim selectedId As Guid? = If(selectedFile Is Nothing, Nothing, CType(selectedFile.Id, Guid?))
            Dim cancellation As New CancellationTokenSource()
            _integrityCancellation = cancellation
            _integrityBusy = True
            _workspace.Enabled = False
            btnCheckFiles.Enabled = False
            btnRecordFingerprint.Enabled = False
            DirectCast(Me.AcceptButton, Control).Enabled = False
            lblIntegritySummary.Text = If(recordBaseline, "Recording fingerprint locally...", "Checking files locally...")

            Try
                Dim observations As List(Of PacketFileIntegrityResult) = Await Task.Run(
                    Function()
                        Dim results As New List(Of PacketFileIntegrityResult)()
                        For Each snapshot As SubmissionPacketFile In snapshots
                            cancellation.Token.ThrowIfCancellationRequested()
                            results.Add(If(recordBaseline,
                                SubmissionPacketIntegrityService.CaptureBaseline(snapshot, replaceExisting,
                                    cancellationToken:=cancellation.Token),
                                SubmissionPacketIntegrityService.Verify(snapshot, cancellation.Token)))
                        Next
                        Return results
                    End Function, cancellation.Token)

                If cancellation.IsCancellationRequested OrElse IsDisposed OrElse Disposing OrElse Not Visible Then Return
                For index As Integer = 0 To snapshots.Count - 1
                    Dim snapshot = snapshots(index)
                    Dim result = observations(index)
                    Dim target = packet.Files.Single(Function(item) item.Id = snapshot.Id)
                    _integrityResults(target) = result
                    If recordBaseline AndAlso result.Status = PacketFileIntegrityStatus.Unchanged Then
                        target.Sha256 = snapshot.Sha256
                        target.FileSizeBytes = snapshot.FileSizeBytes
                        target.LastWriteTimeUtc = snapshot.LastWriteTimeUtc
                        target.HashComputedAtUtc = snapshot.HashComputedAtUtc
                        packet.LastModifiedAtUtc = snapshot.HashComputedAtUtc
                    End If
                Next
            Catch ex As OperationCanceledException
                ' Cancel closes the dialog without adopting any background results.
            Catch ex As Exception
                If Not IsDisposed AndAlso Not Disposing AndAlso Visible Then
                    MessageBox.Show(Me, "PaperRoute could not finish this integrity operation." &
                        Environment.NewLine & ex.Message, "File Integrity", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            Finally
                _integrityBusy = False
                _integrityCancellation = Nothing
                cancellation.Dispose()
                If Not IsDisposed AndAlso Not Disposing AndAlso Visible Then
                    _workspace.Enabled = True
                    DirectCast(Me.AcceptButton, Control).Enabled = True
                    RefreshFiles(selectedId)
                    UpdatePacketButtons()
                End If
            End Try

        End Function


        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _integrityCancellation IsNot Nothing Then _integrityCancellation.Cancel()
            MyBase.OnFormClosing(e)
        End Sub


        Private Sub SaveVault(
            sender As Object,
            e As EventArgs
        )

            If _integrityBusy Then Return

            Dim committed As Manuscript =
                ManuscriptCloneService.CloneManuscript(
                    _workingManuscript
                )

            _sourceManuscript.SubmissionPackets =
                committed.SubmissionPackets

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
