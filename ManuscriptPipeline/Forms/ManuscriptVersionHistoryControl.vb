Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptVersionHistoryControl
        Inherits UserControl

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _managedLibrary As New ManagedLibraryService()

        Private ReadOnly lblInfo As New Label()
        Private ReadOnly lstVersions As New ListBox()
        Private ReadOnly txtDetails As New TextBox()

        Private ReadOnly btnAdd As New Button()
        Private ReadOnly btnEdit As New Button()
        Private ReadOnly btnOpenFile As New Button()
        Private ReadOnly btnSetCurrent As New Button()
        Private ReadOnly btnDeleteVersion As New Button()
        Private ReadOnly btnPackets As New Button()
        Public Event ViewPacketsRequested(versionId As Guid)

        Private ReadOnly _displayedVersions As New List(Of ManuscriptVersion)()


        Public Sub New(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            _manuscript =
                manuscript

            BuildInterface()
            RefreshVersions()

        End Sub


        Private Sub BuildInterface()

            Me.Dock =
                DockStyle.Fill

            Me.Margin =
                New Padding(3, 8, 3, 8)

            Dim group As New GroupBox With {
                .Text = "Version History",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14)
            }

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(0),
                .Margin = New Padding(0)
            }

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.Absolute,
                    34
                )
            )

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.Absolute,
                    50
                )
            )

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            lblInfo.Dock =
                DockStyle.Fill

            lblInfo.AutoEllipsis =
                True

            lblInfo.ForeColor =
                UiTheme.SecondaryText()

            lblInfo.TextAlign =
                ContentAlignment.MiddleLeft

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0),
                .Margin = New Padding(0)
            }

            btnAdd.Text =
                "Add Version"

            layout.RowStyles(1).SizeType = SizeType.AutoSize
            btnPackets.Text = "Submission Packets..."
            btnPackets.AutoSize = True
            btnPackets.AccessibleName = "View packets for selected version"
            AddHandler btnPackets.Click,
                Sub()
                    Dim version = GetSelectedVersion()
                    If version IsNot Nothing Then RaiseEvent ViewPacketsRequested(version.Id)
                End Sub
            buttons.Controls.Add(btnPackets)

            btnAdd.AutoSize =
                True

            btnAdd.Height =
                36

            btnEdit.Text =
                "Edit Details"

            btnEdit.AutoSize =
                True

            btnEdit.Height =
                36

            btnEdit.Enabled =
                False

            btnOpenFile.Text =
                "Open File"

            btnOpenFile.AutoSize =
                True

            btnOpenFile.Height =
                36

            btnOpenFile.Enabled =
                False

            btnSetCurrent.Text =
                "Set Current"

            btnSetCurrent.AutoSize =
                True

            btnSetCurrent.Height =
                36

            btnSetCurrent.Enabled =
                False

            btnDeleteVersion.Text =
                "Delete Version"

            btnDeleteVersion.AutoSize =
                True

            btnDeleteVersion.Height =
                36

            btnDeleteVersion.Enabled =
                False

            btnDeleteVersion.ForeColor =
                UiTheme.DangerColor()

            AddHandler btnAdd.Click,
                AddressOf AddVersion

            AddHandler btnEdit.Click,
                AddressOf EditSelectedVersion

            AddHandler btnOpenFile.Click,
                AddressOf OpenSelectedVersionFile

            AddHandler btnSetCurrent.Click,
                AddressOf SetSelectedVersionCurrent

            AddHandler btnDeleteVersion.Click,
                AddressOf DeleteSelectedVersion

            buttons.Controls.Add(
                btnAdd
            )

            buttons.Controls.Add(
                btnEdit
            )

            buttons.Controls.Add(
                btnOpenFile
            )

            buttons.Controls.Add(
                btnSetCurrent
            )

            buttons.Controls.Add(
                btnDeleteVersion
            )

            lstVersions.Dock =
                DockStyle.Fill

            lstVersions.IntegralHeight =
                False

            lstVersions.HorizontalScrollbar =
                True

            AddHandler lstVersions.SelectedIndexChanged,
                AddressOf VersionSelectionChanged

            AddHandler lstVersions.DoubleClick,
                AddressOf EditSelectedVersion

            txtDetails.Dock =
                DockStyle.Fill

            txtDetails.Multiline =
                True

            txtDetails.ReadOnly =
                True

            txtDetails.ScrollBars =
                ScrollBars.Vertical

            txtDetails.BackColor =
                UiTheme.CardBackground()

            txtDetails.ForeColor =
                UiTheme.PrimaryText()

            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical,
                .SplitterWidth = 6
            }

            AddHandler split.SizeChanged,
                Sub(sender, e)

                    Const minimumLeft As Integer = 180
                    Const minimumRight As Integer = 240

                    Dim availableWidth As Integer =
                        split.Width -
                        split.SplitterWidth

                    If availableWidth <=
                       minimumLeft + minimumRight Then

                        Return

                    End If

                    Dim desired As Integer =
                        CInt(
                            Math.Round(
                                availableWidth * 0.46
                            )
                        )

                    Dim maximum As Integer =
                        availableWidth -
                        minimumRight

                    split.SplitterDistance =
                        Math.Min(
                            maximum,
                            Math.Max(
                                minimumLeft,
                                desired
                            )
                        )

                End Sub

            split.Panel1.Controls.Add(
                lstVersions
            )

            split.Panel2.Controls.Add(
                txtDetails
            )

            layout.Controls.Add(
                lblInfo,
                0,
                0
            )

            layout.Controls.Add(
                buttons,
                0,
                1
            )

            layout.Controls.Add(
                split,
                0,
                2
            )

            group.Controls.Add(
                layout
            )

            Me.Controls.Add(
                group
            )

        End Sub


        Public Sub RefreshVersions()

            Dim selectedId As Guid? =
                Nothing

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            If selected IsNot Nothing Then
                selectedId =
                    selected.Id
            End If

            lstVersions.BeginUpdate()

            Try

                lstVersions.Items.Clear()
                _displayedVersions.Clear()

                Dim versions As List(Of ManuscriptVersion) =
                    ManuscriptVersionService.
                        GetChronologicalVersions(
                            _manuscript
                        )

                For Each version As ManuscriptVersion In versions

                    _displayedVersions.Add(
                        version
                    )

                    lstVersions.Items.Add(
                        FormatVersionListItem(
                            version
                        )
                    )

                Next

            Finally

                lstVersions.EndUpdate()

            End Try

            If _displayedVersions.Count = 0 Then

                lblInfo.Text =
                    "No versions tracked yet. Add a working, submitted, or revised snapshot. Journal Submissions continue below ↓"

                txtDetails.Text =
                    "Version records are optional. Existing manuscripts remain valid without version history."

            Else

                lblInfo.Text =
                    _displayedVersions.Count.ToString() &
                    " version(s), oldest to newest. Library copies are immutable after Save & Close. Journal Submissions continue below ↓"

                Dim selectedIndex As Integer =
                    FindDisplayedVersionIndex(
                        selectedId
                    )

                If selectedIndex < 0 Then

                    If _manuscript.CurrentVersionId.HasValue Then

                        selectedIndex =
                            FindDisplayedVersionIndex(
                                _manuscript.CurrentVersionId
                            )

                    End If

                End If

                If selectedIndex < 0 Then
                    selectedIndex =
                        _displayedVersions.Count - 1
                End If

                lstVersions.SelectedIndex =
                    selectedIndex

            End If

            UpdateVersionButtons()
            RefreshVersionDetails()

        End Sub


        Public Function SelectVersionById(
            versionId As Guid
        ) As Boolean

            Dim index As Integer =
                FindDisplayedVersionIndex(
                    versionId
                )

            If index < 0 Then

                RefreshVersions()

                index =
                    FindDisplayedVersionIndex(
                        versionId
                    )

            End If

            If index < 0 Then
                Return False
            End If

            lstVersions.SelectedIndex =
                index

            Return True

        End Function


        Private Function FormatVersionListItem(
            version As ManuscriptVersion
        ) As String

            Dim result As String =
                version.CreatedDate.ToString(
                    "MMM d, yyyy"
                ) &
                " — " &
                If(
                    String.IsNullOrWhiteSpace(
                        version.Label
                    ),
                    "(unlabeled version)",
                    version.Label
                )

            If _manuscript.CurrentVersionId.HasValue AndAlso
               _manuscript.CurrentVersionId.Value =
               version.Id Then

                result &=
                    "   [CURRENT]"

            End If

            If String.IsNullOrWhiteSpace(
                version.LocalFilePath
            ) Then

                result &=
                    "   • metadata only"

            ElseIf version.IsManagedCopy Then

                If _managedLibrary.IsManagedPath(
                    version.LocalFilePath
                ) Then

                    result &=
                        "   • PaperRoute Library"

                Else

                    result &=
                        "   • pending Library copy"

                End If

            Else

                result &=
                    "   • linked file"

            End If

            Return result

        End Function


        Private Function GetSelectedVersion() As ManuscriptVersion

            Dim selectedIndex As Integer =
                lstVersions.SelectedIndex

            If selectedIndex < 0 OrElse
               selectedIndex >=
               _displayedVersions.Count Then

                Return Nothing

            End If

            Return _displayedVersions(
                selectedIndex
            )

        End Function


        Private Function FindDisplayedVersionIndex(
            versionId As Guid?
        ) As Integer

            If Not versionId.HasValue Then
                Return -1
            End If

            For index As Integer =
                0 To _displayedVersions.Count - 1

                If _displayedVersions(index).Id =
                   versionId.Value Then

                    Return index

                End If

            Next

            Return -1

        End Function


        Private Sub VersionSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateVersionButtons()
            RefreshVersionDetails()

        End Sub


        Private Sub UpdateVersionButtons()

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            Dim hasSelection As Boolean =
                selected IsNot Nothing

            btnPackets.Enabled = hasSelection

            btnEdit.Enabled =
                hasSelection

            btnOpenFile.Enabled =
                hasSelection AndAlso
                Not String.IsNullOrWhiteSpace(
                    selected.LocalFilePath
                )

            btnSetCurrent.Enabled =
                hasSelection AndAlso
                (
                    Not _manuscript.CurrentVersionId.HasValue OrElse
                    _manuscript.CurrentVersionId.Value <>
                    selected.Id
                )

            btnDeleteVersion.Enabled =
                hasSelection

        End Sub


        Private Sub RefreshVersionDetails()

            Dim version As ManuscriptVersion =
                GetSelectedVersion()

            If version Is Nothing Then

                If _displayedVersions.Count = 0 Then
                    Return
                End If

                txtDetails.Text =
                    "Select a version to view its file, workflow links, notes, and provenance."

                Return

            End If

            Dim lines As New List(Of String)()

            lines.Add(
                "Version date: " &
                version.CreatedDate.ToString(
                    "MMMM d, yyyy"
                )
            )

            lines.Add(
                "Current: " &
                If(
                    _manuscript.CurrentVersionId.HasValue AndAlso
                    _manuscript.CurrentVersionId.Value =
                    version.Id,
                    "Yes",
                    "No"
                )
            )

            AppendFileDetails(
                lines,
                version
            )

            AppendWorkflowDetails(
                lines,
                version
            )

            lines.Add(
                "Recorded in PaperRoute: " &
                FormatAuditTimestamp(
                    version.RecordedAtUtc,
                    "not available for this legacy record"
                )
            )

            lines.Add(
                "Last edited: " &
                FormatAuditTimestamp(
                    version.LastModifiedAtUtc,
                    "no later metadata edits recorded"
                )
            )

            lines.Add(
                String.Empty
            )

            lines.Add(
                "Notes:"
            )

            lines.Add(
                If(
                    String.IsNullOrWhiteSpace(
                        version.Notes
                    ),
                    "No notes were recorded.",
                    version.Notes
                )
            )

            txtDetails.Text =
                String.Join(
                    Environment.NewLine,
                    lines
                )

        End Sub


        Private Sub AppendFileDetails(
            lines As List(Of String),
            version As ManuscriptVersion
        )

            If String.IsNullOrWhiteSpace(
                version.LocalFilePath
            ) Then

                lines.Add(
                    "Storage: Metadata only"
                )

                lines.Add(
                    "File: No file attached"
                )

                Return

            End If

            Dim fileExists As Boolean =
                File.Exists(
                    version.LocalFilePath
                )

            Dim fileLabel As String

            If version.IsManagedCopy Then

                If _managedLibrary.IsManagedPath(
                    version.LocalFilePath
                ) Then

                    lines.Add(
                        "Storage: PaperRoute Library immutable snapshot"
                    )

                    fileLabel =
                        "Managed snapshot: "

                Else

                    lines.Add(
                        "Storage: Will copy into the PaperRoute Library when you Save & Close"
                    )

                    fileLabel =
                        "Source selected for snapshot: "

                End If

            Else

                lines.Add(
                    "Storage: Linked to original file"
                )

                fileLabel =
                    "Linked file: "

            End If

            lines.Add(
                fileLabel &
                version.LocalFilePath &
                If(
                    fileExists,
                    String.Empty,
                    "   [FILE NOT FOUND]"
                )
            )

        End Sub


        Private Sub AppendWorkflowDetails(
            lines As List(Of String),
            version As ManuscriptVersion
        )

            Dim submission As JournalSubmission =
                FindSubmission(
                    version.SubmissionId
                )

            If version.SubmissionId.HasValue Then

                If submission Is Nothing Then

                    lines.Add(
                        "Submission link on version: Historical link is unresolved"
                    )

                Else

                    lines.Add(
                        "Submission link on version: " &
                        submission.SubmittedDate.ToString(
                            "MMM d, yyyy"
                        ) &
                        " — " &
                        If(
                            String.IsNullOrWhiteSpace(
                                submission.JournalName
                            ),
                            "(journal not named)",
                            submission.JournalName
                        )
                    )

                End If

            Else

                lines.Add(
                    "Submission link on version: None"
                )

            End If

            If _manuscript.SubmissionPackets IsNot Nothing Then
                Dim packets = _manuscript.SubmissionPackets.Where(
                    Function(item) item IsNot Nothing AndAlso item.ManuscriptVersionId = version.Id).ToList()
                lines.Add("Submission packets: " & packets.Count.ToString() & " (" &
                    packets.Where(Function(item) item.SubmissionId.HasValue).Count().ToString() & " linked to recorded submissions)")
                If packets.Count > 0 Then lines.Add("Use Submission Packets to view each preparation or submission association.")
            End If

            If version.DecisionId.HasValue Then

                Dim decision As EditorialDecisionEvent =
                    FindDecision(
                        version.DecisionId
                    )

                If decision Is Nothing Then

                    lines.Add(
                        "Decision: Historical link is unresolved"
                    )

                Else

                    lines.Add(
                        "Decision: " &
                        decision.DecisionDate.ToString(
                            "MMM d, yyyy"
                        ) &
                        " — " &
                        FormatDecision(
                            decision.Decision
                        )
                    )

                End If

            Else

                lines.Add(
                    "Decision: None"
                )

            End If

            If version.RevisionRoundNumber.HasValue Then

                lines.Add(
                    "Revision round: " &
                    version.RevisionRoundNumber.Value.ToString()
                )

            Else

                lines.Add(
                    "Revision round: Not specified"
                )

            End If

        End Sub


        Private Function FindSubmission(
            submissionId As Guid?
        ) As JournalSubmission

            If Not submissionId.HasValue OrElse
               _manuscript.Submissions Is Nothing Then

                Return Nothing

            End If

            For Each submission As JournalSubmission In
                _manuscript.Submissions

                If submission IsNot Nothing AndAlso
                   submission.Id =
                   submissionId.Value Then

                    Return submission

                End If

            Next

            Return Nothing

        End Function


        Private Function FindDecision(
            decisionId As Guid?
        ) As EditorialDecisionEvent

            If Not decisionId.HasValue OrElse
               _manuscript.Submissions Is Nothing Then

                Return Nothing

            End If

            For Each submission As JournalSubmission In
                _manuscript.Submissions

                If submission Is Nothing OrElse
                   submission.Decisions Is Nothing Then

                    Continue For

                End If

                For Each decision As EditorialDecisionEvent In
                    submission.Decisions

                    If decision IsNot Nothing AndAlso
                       decision.Id =
                       decisionId.Value Then

                        Return decision

                    End If

                Next

            Next

            Return Nothing

        End Function


        Private Sub AddVersion(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New ManuscriptVersionEditForm(
                _manuscript
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                Try

                    Dim version As ManuscriptVersion =
                        ManuscriptVersionService.CreateVersion(
                            _manuscript,
                            dialog.VersionLabel,
                            dialog.VersionNotes,
                            dialog.VersionFilePath,
                            dialog.IsManagedCopy,
                            dialog.SubmissionId,
                            dialog.DecisionId,
                            dialog.RevisionRoundNumber,
                            dialog.MakeCurrentRequested,
                            dialog.VersionDate
                        )

                    RefreshVersions()

                    Dim index As Integer =
                        FindDisplayedVersionIndex(
                            version.Id
                        )

                    If index >= 0 Then
                        lstVersions.SelectedIndex =
                            index
                    End If

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not add this manuscript version." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "Version Not Added",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub


        Private Sub EditSelectedVersion(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New ManuscriptVersionEditForm(
                _manuscript,
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                Try

                    ManuscriptVersionService.UpdateVersion(
                        _manuscript,
                        selected.Id,
                        dialog.VersionLabel,
                        dialog.VersionNotes,
                        dialog.VersionDate,
                        dialog.VersionFilePath,
                        dialog.IsManagedCopy,
                        dialog.SubmissionId,
                        dialog.DecisionId,
                        dialog.RevisionRoundNumber
                    )

                    If dialog.MakeCurrentRequested Then

                        ManuscriptVersionService.SetCurrentVersion(
                            _manuscript,
                            selected.Id
                        )

                    End If

                    RefreshVersions()

                    Dim index As Integer =
                        FindDisplayedVersionIndex(
                            selected.Id
                        )

                    If index >= 0 Then
                        lstVersions.SelectedIndex =
                            index
                    End If

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not update this manuscript version." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "Version Not Updated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub



        Private Sub DeleteSelectedVersion(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            If selected Is Nothing Then
                Return
            End If

            Dim label As String =
                If(
                    String.IsNullOrWhiteSpace(
                        selected.Label
                    ),
                    "(unlabeled version)",
                    selected.Label
                )

            Dim message As String =
                "Delete the version '" &
                label &
                "' from this manuscript's Version History?" &
                Environment.NewLine &
                Environment.NewLine

            If selected.IsManagedCopy AndAlso
               _managedLibrary.IsManagedPath(
                   selected.LocalFilePath
               ) Then

                message &=
                    "Its PaperRoute Library snapshot will be removed only when you Save & Close Manuscript Details."

            ElseIf Not String.IsNullOrWhiteSpace(
                selected.LocalFilePath
            ) Then

                message &=
                    "The linked original file will NOT be deleted."

            Else

                message &=
                    "This removes the metadata-only version record."

            End If

            message &=
                Environment.NewLine &
                Environment.NewLine &
                "Canceling Manuscript Details before Save & Close leaves the saved library unchanged."

            If _manuscript.CurrentVersionId.HasValue AndAlso
               _manuscript.CurrentVersionId.Value =
               selected.Id Then

                message &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "This is the current version. PaperRoute will make the newest remaining version current, or clear Current Version if none remain."

            End If

            If MessageBox.Show(
                Me,
                message,
                "Delete Manuscript Version?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            ) <> DialogResult.Yes Then

                Return

            End If

            Try

                ManuscriptVersionService.DeleteVersion(
                    _manuscript,
                    selected.Id
                )

                RefreshVersions()

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not delete this manuscript version." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Version Not Deleted",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        Private Sub SetSelectedVersionCurrent(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            If selected Is Nothing Then
                Return
            End If

            Try

                ManuscriptVersionService.SetCurrentVersion(
                    _manuscript,
                    selected.Id
                )

                RefreshVersions()

                Dim index As Integer =
                    FindDisplayedVersionIndex(
                        selected.Id
                    )

                If index >= 0 Then
                    lstVersions.SelectedIndex =
                        index
                End If

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Current Version Not Changed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        Private Sub OpenSelectedVersionFile(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptVersion =
                GetSelectedVersion()

            If selected Is Nothing OrElse
               String.IsNullOrWhiteSpace(
                   selected.LocalFilePath
               ) Then

                Return

            End If

            If Not File.Exists(
                selected.LocalFilePath
            ) Then

                MessageBox.Show(
                    Me,
                    "The version file could not be found." &
                    Environment.NewLine &
                    Environment.NewLine &
                    selected.LocalFilePath &
                    Environment.NewLine &
                    Environment.NewLine &
                    "The historical version record is still preserved.",
                    "Version File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Try

                Process.Start(
                    New ProcessStartInfo With {
                        .FileName =
                            selected.LocalFilePath,
                        .UseShellExecute =
                            True
                    }
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not open this file." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Could Not Open Version File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        Private Shared Function FormatAuditTimestamp(
            value As DateTime?,
            fallback As String
        ) As String

            If Not value.HasValue Then
                Return fallback
            End If

            Return value.Value.
                ToLocalTime().
                ToString(
                    "MMM d, yyyy h:mm tt"
                )

        End Function


        Private Shared Function FormatDecision(
            value As EditorialDecision
        ) As String

            Select Case value

                Case EditorialDecision.DeskRejected
                    Return "Desk Rejected"

                Case EditorialDecision.RejectedAfterReview
                    Return "Rejected After Review"

                Case EditorialDecision.MajorRevision
                    Return "Major Revision"

                Case EditorialDecision.MinorRevision
                    Return "Minor Revision"

                Case EditorialDecision.ReviseAndResubmit
                    Return "Revise & Resubmit"

                Case Else
                    Return value.ToString()

            End Select

        End Function

    End Class

End Namespace
