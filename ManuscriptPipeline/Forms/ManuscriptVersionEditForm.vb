Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptVersionEditForm
        Inherits Form

        Private Class SubmissionOption

            Public ReadOnly Property SubmissionId As Guid?
            Public ReadOnly Property Label As String

            Public Sub New(
                submissionId As Guid?,
                label As String
            )
                Me.SubmissionId = submissionId
                Me.Label = label
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function

        End Class


        Private Class DecisionOption

            Public ReadOnly Property DecisionId As Guid?
            Public ReadOnly Property SubmissionId As Guid?
            Public ReadOnly Property Label As String

            Public Sub New(
                decisionId As Guid?,
                submissionId As Guid?,
                label As String
            )
                Me.DecisionId = decisionId
                Me.SubmissionId = submissionId
                Me.Label = label
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function

        End Class


        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _existingVersion As ManuscriptVersion
        Private ReadOnly _managedLibrary As New ManagedLibraryService()

        Private ReadOnly txtLabel As New TextBox()
        Private ReadOnly dtpVersionDate As New DateTimePicker()
        Private ReadOnly txtFilePath As New TextBox()
        Private ReadOnly btnBrowse As New Button()
        Private ReadOnly btnClearFile As New Button()

        Private ReadOnly rbManagedCopy As New RadioButton()
        Private ReadOnly rbLinkedFile As New RadioButton()
        Private ReadOnly rbMetadataOnly As New RadioButton()
        Private ReadOnly lblFileHelp As New Label()

        Private ReadOnly cboSubmission As New ComboBox()
        Private ReadOnly cboDecision As New ComboBox()
        Private ReadOnly lblAssociationHelp As New Label()

        Private ReadOnly chkRevisionRound As New CheckBox()
        Private ReadOnly nudRevisionRound As New NumericUpDown()
        Private ReadOnly chkMakeCurrent As New CheckBox()

        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly lblProvenance As New Label()

        Private _suppressAssociationEvents As Boolean = False
        Private _fileIdentityLocked As Boolean = False

        Public ReadOnly Property VersionLabel As String
            Get
                Return txtLabel.Text.Trim()
            End Get
        End Property

        Public ReadOnly Property VersionNotes As String
            Get
                Return txtNotes.Text.Trim()
            End Get
        End Property

        Public ReadOnly Property VersionFilePath As String
            Get
                Return txtFilePath.Text.Trim()
            End Get
        End Property

        Public ReadOnly Property IsManagedCopy As Boolean
            Get
                Return rbManagedCopy.Checked AndAlso
                    Not String.IsNullOrWhiteSpace(
                        VersionFilePath
                    )
            End Get
        End Property

        Public ReadOnly Property SubmissionId As Guid?
            Get
                Dim optionItem As SubmissionOption =
                    TryCast(
                        cboSubmission.SelectedItem,
                        SubmissionOption
                    )

                If optionItem Is Nothing Then
                    Return Nothing
                End If

                Return optionItem.SubmissionId
            End Get
        End Property

        Public ReadOnly Property DecisionId As Guid?
            Get
                Dim optionItem As DecisionOption =
                    TryCast(
                        cboDecision.SelectedItem,
                        DecisionOption
                    )

                If optionItem Is Nothing Then
                    Return Nothing
                End If

                Return optionItem.DecisionId
            End Get
        End Property

        Public ReadOnly Property RevisionRoundNumber As Integer?
            Get
                If Not chkRevisionRound.Checked Then
                    Return Nothing
                End If

                Return Decimal.ToInt32(
                    nudRevisionRound.Value
                )
            End Get
        End Property

        Public ReadOnly Property MakeCurrentRequested As Boolean
            Get
                Return chkMakeCurrent.Checked
            End Get
        End Property

        Public ReadOnly Property VersionDate As DateTime
            Get
                If _existingVersion IsNot Nothing AndAlso
                   dtpVersionDate.Value.Date =
                   _existingVersion.CreatedDate.Date Then

                    Return _existingVersion.CreatedDate
                End If

                Return dtpVersionDate.Value.Date
            End Get
        End Property


        Public Sub New(
            manuscript As Manuscript
        )

            Me.New(
                manuscript,
                Nothing
            )

        End Sub


        Public Sub New(
            manuscript As Manuscript,
            existingVersion As ManuscriptVersion
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            _manuscript = manuscript
            _existingVersion = existingVersion

            BuildInterface()
            PopulateAssociations()
            EnsureHistoricalAssociationOptions()
            LoadVersion()

            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _existingVersion Is Nothing,
                    "Add Manuscript Version",
                    "Edit Manuscript Version"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(840, 880)

            Me.MinimumSize =
                New Size(740, 760)

            Me.Font =
                New Font("Segoe UI", 10.0F)

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 12,
                .Padding = New Padding(22)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    175
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 150))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))

            txtLabel.Dock =
                DockStyle.Fill

            dtpVersionDate.Format =
                DateTimePickerFormat.Short

            dtpVersionDate.Width =
                190

            root.Controls.Add(
                CreateFieldLabel("Label"),
                0,
                0
            )

            root.Controls.Add(
                txtLabel,
                1,
                0
            )

            root.Controls.Add(
                CreateFieldLabelWithHelp(
                    "Version date",
                    WorkflowHelpCatalog.VersionDate
                ),
                0,
                1
            )

            root.Controls.Add(
                dtpVersionDate,
                1,
                1
            )

            BuildFileRow(
                root
            )

            BuildStorageRow(
                root
            )

            cboSubmission.Dock =
                DockStyle.Fill

            cboSubmission.DropDownStyle =
                ComboBoxStyle.DropDownList

            AddHandler cboSubmission.SelectedIndexChanged,
                AddressOf SubmissionChanged

            root.Controls.Add(
                CreateFieldLabelWithHelp(
                    "Submission",
                    WorkflowHelpCatalog.SubmissionAssociation
                ),
                0,
                4
            )

            root.Controls.Add(
                cboSubmission,
                1,
                4
            )

            cboDecision.Dock =
                DockStyle.Fill

            cboDecision.DropDownStyle =
                ComboBoxStyle.DropDownList

            AddHandler cboDecision.SelectedIndexChanged,
                AddressOf DecisionChanged

            root.Controls.Add(
                CreateFieldLabelWithHelp(
                    "Decision",
                    WorkflowHelpCatalog.DecisionAssociation
                ),
                0,
                5
            )

            root.Controls.Add(
                cboDecision,
                1,
                5
            )

            lblAssociationHelp.Dock =
                DockStyle.Fill

            lblAssociationHelp.AutoEllipsis =
                True

            lblAssociationHelp.ForeColor =
                UiTheme.SecondaryText()

            lblAssociationHelp.Padding =
                New Padding(2, 3, 2, 2)

            lblAssociationHelp.Text =
                "Workflow associations are optional. Use the ? hints for the distinction between a submitted version and a version created in response to editorial feedback."

            root.Controls.Add(
                lblAssociationHelp,
                0,
                6
            )

            root.SetColumnSpan(
                lblAssociationHelp,
                2
            )

            BuildRevisionRoundRow(
                root
            )

            chkMakeCurrent.Text =
                If(
                    _existingVersion Is Nothing,
                    "Make this the current working version",
                    "Set this version as the current working version"
                )

            chkMakeCurrent.AutoSize =
                True

            chkMakeCurrent.Anchor =
                AnchorStyles.Left

            root.Controls.Add(
                CreateFieldLabelWithHelp(
                    "Current version",
                    WorkflowHelpCatalog.CurrentVersion
                ),
                0,
                8
            )

            root.Controls.Add(
                chkMakeCurrent,
                1,
                8
            )

            Dim notesGroup As New GroupBox With {
                .Text = "Version Notes",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(10)
            }

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            notesGroup.Controls.Add(
                txtNotes
            )

            root.Controls.Add(
                notesGroup,
                0,
                9
            )

            root.SetColumnSpan(
                notesGroup,
                2
            )

            lblProvenance.Dock =
                DockStyle.Fill

            lblProvenance.AutoEllipsis =
                True

            lblProvenance.ForeColor =
                UiTheme.SecondaryText()

            lblProvenance.Padding =
                New Padding(2, 7, 2, 2)

            root.Controls.Add(
                lblProvenance,
                0,
                10
            )

            root.SetColumnSpan(
                lblProvenance,
                2
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnSave As New Button With {
                .Text =
                    If(
                        _existingVersion Is Nothing,
                        "Add Version",
                        "Save Changes"
                    ),
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
                AddressOf SaveVersion

            buttons.Controls.Add(
                btnSave
            )

            buttons.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                buttons,
                0,
                11
            )

            root.SetColumnSpan(
                buttons,
                2
            )

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub BuildFileRow(
            root As TableLayoutPanel
        )

            Dim fileLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .Margin = New Padding(0)
            }

            fileLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            fileLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            fileLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            txtFilePath.Dock =
                DockStyle.Fill

            txtFilePath.ReadOnly =
                True

            btnBrowse.Text =
                "Browse..."

            btnBrowse.AutoSize =
                True

            btnBrowse.Height =
                34

            btnClearFile.Text =
                "Clear"

            btnClearFile.AutoSize =
                True

            btnClearFile.Height =
                34

            AddHandler btnBrowse.Click,
                AddressOf BrowseForVersionFile

            AddHandler btnClearFile.Click,
                AddressOf ClearVersionFile

            fileLayout.Controls.Add(
                txtFilePath,
                0,
                0
            )

            fileLayout.Controls.Add(
                btnBrowse,
                1,
                0
            )

            fileLayout.Controls.Add(
                btnClearFile,
                2,
                0
            )

            root.Controls.Add(
                CreateFieldLabel("Version file"),
                0,
                2
            )

            root.Controls.Add(
                fileLayout,
                1,
                2
            )

        End Sub


        Private Sub BuildStorageRow(
            root As TableLayoutPanel
        )

            Dim storageLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Margin = New Padding(0)
            }

            storageLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
            storageLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
            storageLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
            storageLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            rbManagedCopy.Text =
                "Copy into PaperRoute Library (recommended immutable snapshot)"

            rbManagedCopy.AutoSize =
                True

            rbLinkedFile.Text =
                "Link to the original file"

            rbLinkedFile.AutoSize =
                True

            rbMetadataOnly.Text =
                "Track metadata only (no file)"

            rbMetadataOnly.AutoSize =
                True

            AddHandler rbManagedCopy.CheckedChanged,
                AddressOf StorageModeChanged

            AddHandler rbLinkedFile.CheckedChanged,
                AddressOf StorageModeChanged

            AddHandler rbMetadataOnly.CheckedChanged,
                AddressOf StorageModeChanged

            lblFileHelp.Dock =
                DockStyle.Fill

            lblFileHelp.AutoEllipsis =
                True

            lblFileHelp.UseMnemonic =
                False

            lblFileHelp.ForeColor =
                UiTheme.SecondaryText()

            lblFileHelp.Padding =
                New Padding(0, 3, 0, 0)

            storageLayout.Controls.Add(
                rbManagedCopy,
                0,
                0
            )

            storageLayout.Controls.Add(
                rbLinkedFile,
                0,
                1
            )

            storageLayout.Controls.Add(
                rbMetadataOnly,
                0,
                2
            )

            storageLayout.Controls.Add(
                lblFileHelp,
                0,
                3
            )

            root.Controls.Add(
                CreateFieldLabel("Storage"),
                0,
                3
            )

            root.Controls.Add(
                storageLayout,
                1,
                3
            )

        End Sub


        Private Sub BuildRevisionRoundRow(
            root As TableLayoutPanel
        )

            Dim roundPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Margin = New Padding(0)
            }

            chkRevisionRound.Text =
                "Track revision round"

            chkRevisionRound.AutoSize =
                True

            nudRevisionRound.Minimum =
                1

            nudRevisionRound.Maximum =
                Integer.MaxValue

            nudRevisionRound.Value =
                1

            nudRevisionRound.Width =
                128

            nudRevisionRound.Enabled =
                False

            AddHandler chkRevisionRound.CheckedChanged,
                Sub(sender, e)
                    nudRevisionRound.Enabled =
                        chkRevisionRound.Checked
                End Sub

            roundPanel.Controls.Add(
                chkRevisionRound
            )

            roundPanel.Controls.Add(
                nudRevisionRound
            )

            root.Controls.Add(
                CreateFieldLabelWithHelp(
                    "Revision round",
                    WorkflowHelpCatalog.RevisionRound
                ),
                0,
                7
            )

            root.Controls.Add(
                roundPanel,
                1,
                7
            )

        End Sub


        Private Function CreateFieldLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    )
            }

        End Function


        Private Function CreateFieldLabelWithHelp(
            text As String,
            helpText As String
        ) As Control

            Dim host As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Margin = New Padding(0),
                .Padding = New Padding(0, 6, 0, 0)
            }

            host.Controls.Add(
                CreateFieldLabel(
                    text
                )
            )

            host.Controls.Add(
                New ContextHelpControl(
                    helpText
                )
            )

            Return host

        End Function


        Private Sub PopulateAssociations()

            _suppressAssociationEvents =
                True

            Try

                cboSubmission.Items.Clear()

                cboSubmission.Items.Add(
                    New SubmissionOption(
                        Nothing,
                        "None"
                    )
                )

                cboDecision.Items.Clear()

                cboDecision.Items.Add(
                    New DecisionOption(
                        Nothing,
                        Nothing,
                        "None"
                    )
                )

                Dim submissions As New List(Of JournalSubmission)(
                    If(
                        _manuscript.Submissions,
                        New List(Of JournalSubmission)()
                    )
                )

                submissions.RemoveAll(
                    Function(item)
                        Return item Is Nothing
                    End Function
                )

                submissions.Sort(
                    Function(
                        left As JournalSubmission,
                        right As JournalSubmission
                    ) As Integer

                        Dim dateComparison As Integer =
                            DateTime.Compare(
                                left.SubmittedDate.Date,
                                right.SubmittedDate.Date
                            )

                        If dateComparison <> 0 Then
                            Return dateComparison
                        End If

                        Return StringComparer.CurrentCultureIgnoreCase.Compare(
                            left.JournalName,
                            right.JournalName
                        )

                    End Function
                )

                For Each submission As JournalSubmission In submissions

                    cboSubmission.Items.Add(
                        New SubmissionOption(
                            submission.Id,
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
                    )

                    If submission.Decisions Is Nothing Then
                        Continue For
                    End If

                    For Each decision As EditorialDecisionEvent In
                        submission.Decisions

                        If decision Is Nothing Then
                            Continue For
                        End If

                        cboDecision.Items.Add(
                            New DecisionOption(
                                decision.Id,
                                submission.Id,
                                decision.DecisionDate.ToString(
                                    "MMM d, yyyy"
                                ) &
                                " — " &
                                FormatDecision(
                                    decision.Decision
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
                        )

                    Next

                Next

                cboSubmission.SelectedIndex =
                    0

                cboDecision.SelectedIndex =
                    0

            Finally

                _suppressAssociationEvents =
                    False

            End Try

        End Sub


        Private Sub EnsureHistoricalAssociationOptions()

            If _existingVersion Is Nothing Then
                Return
            End If

            If _existingVersion.SubmissionId.HasValue AndAlso
               Not ContainsSubmissionOption(
                   _existingVersion.SubmissionId.Value
               ) Then

                cboSubmission.Items.Add(
                    New SubmissionOption(
                        _existingVersion.SubmissionId,
                        "Unresolved historical submission"
                    )
                )

            End If

            If _existingVersion.DecisionId.HasValue AndAlso
               Not ContainsDecisionOption(
                   _existingVersion.DecisionId.Value
               ) Then

                cboDecision.Items.Add(
                    New DecisionOption(
                        _existingVersion.DecisionId,
                        _existingVersion.SubmissionId,
                        "Unresolved historical editorial decision"
                    )
                )

            End If

        End Sub


        Private Function ContainsSubmissionOption(
            submissionId As Guid
        ) As Boolean

            For Each item As Object In cboSubmission.Items

                Dim optionItem As SubmissionOption =
                    TryCast(
                        item,
                        SubmissionOption
                    )

                If optionItem IsNot Nothing AndAlso
                   optionItem.SubmissionId.HasValue AndAlso
                   optionItem.SubmissionId.Value =
                   submissionId Then

                    Return True

                End If

            Next

            Return False

        End Function


        Private Function ContainsDecisionOption(
            decisionId As Guid
        ) As Boolean

            For Each item As Object In cboDecision.Items

                Dim optionItem As DecisionOption =
                    TryCast(
                        item,
                        DecisionOption
                    )

                If optionItem IsNot Nothing AndAlso
                   optionItem.DecisionId.HasValue AndAlso
                   optionItem.DecisionId.Value =
                   decisionId Then

                    Return True

                End If

            Next

            Return False

        End Function


        Private Sub LoadVersion()

            If _existingVersion Is Nothing Then

                dtpVersionDate.Value =
                    DateTime.Today

                rbMetadataOnly.Checked =
                    True

                chkMakeCurrent.Checked =
                    True

                _fileIdentityLocked =
                    False

                UpdateFileEditingState()

                lblProvenance.Text =
                    "Recorded in PaperRoute will be captured automatically when you add this version."

                Return

            End If

            txtLabel.Text =
                _existingVersion.Label

            dtpVersionDate.Value =
                SafePickerDate(
                    _existingVersion.CreatedDate
                )

            txtFilePath.Text =
                _existingVersion.LocalFilePath

            txtNotes.Text =
                _existingVersion.Notes

            If String.IsNullOrWhiteSpace(
                _existingVersion.LocalFilePath
            ) Then

                rbMetadataOnly.Checked =
                    True

            ElseIf _existingVersion.IsManagedCopy Then

                rbManagedCopy.Checked =
                    True

            Else

                rbLinkedFile.Checked =
                    True

            End If

            _fileIdentityLocked =
                _existingVersion.IsManagedCopy AndAlso
                Not String.IsNullOrWhiteSpace(
                    _existingVersion.LocalFilePath
                ) AndAlso
                _managedLibrary.IsManagedPath(
                    _existingVersion.LocalFilePath
                )

            UpdateFileEditingState()

            SelectSubmission(
                _existingVersion.SubmissionId
            )

            SelectDecision(
                _existingVersion.DecisionId
            )

            If _existingVersion.RevisionRoundNumber.HasValue Then

                chkRevisionRound.Checked =
                    True

                nudRevisionRound.Value =
                    Math.Min(
                        nudRevisionRound.Maximum,
                        Math.Max(
                            nudRevisionRound.Minimum,
                            CDec(
                                _existingVersion.
                                    RevisionRoundNumber.
                                    Value
                            )
                        )
                    )

            End If

            Dim isCurrent As Boolean =
                _manuscript.CurrentVersionId.HasValue AndAlso
                _manuscript.CurrentVersionId.Value =
                _existingVersion.Id

            chkMakeCurrent.Checked =
                isCurrent

            chkMakeCurrent.Enabled =
                Not isCurrent

            If isCurrent Then

                chkMakeCurrent.Text =
                    "This is the current working version"

            End If

            lblProvenance.Text =
                BuildProvenanceText(
                    _existingVersion
                )

        End Sub


        Private Sub StorageModeChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateFileEditingState()

        End Sub


        Private Sub UpdateFileEditingState()

            btnBrowse.Enabled =
                Not _fileIdentityLocked

            btnClearFile.Enabled =
                Not _fileIdentityLocked AndAlso
                Not String.IsNullOrWhiteSpace(
                    txtFilePath.Text
                )

            rbManagedCopy.Enabled =
                Not _fileIdentityLocked

            rbLinkedFile.Enabled =
                Not _fileIdentityLocked

            rbMetadataOnly.Enabled =
                Not _fileIdentityLocked

            If _fileIdentityLocked Then

                lblFileHelp.Text =
                    "This PaperRoute Library snapshot is immutable. You can edit its label, date, notes, and workflow links; create a new version to replace the file."

                Return

            End If

            If rbManagedCopy.Checked Then

                lblFileHelp.Text =
                    "On Save & Close, PaperRoute copies the selected file into its Library. That managed snapshot then becomes immutable."

            ElseIf rbLinkedFile.Checked Then

                lblFileHelp.Text =
                    "This points to the original file in place. Browse or Clear can change the link later."

            Else

                lblFileHelp.Text =
                    "No file is attached. Browse to add one, or keep this as a metadata-only historical version."

            End If

        End Sub


        Private Sub SelectSubmission(
            submissionId As Guid?
        )

            For index As Integer =
                0 To cboSubmission.Items.Count - 1

                Dim optionItem As SubmissionOption =
                    TryCast(
                        cboSubmission.Items(index),
                        SubmissionOption
                    )

                If optionItem Is Nothing Then
                    Continue For
                End If

                If NullableGuidEquals(
                    optionItem.SubmissionId,
                    submissionId
                ) Then

                    cboSubmission.SelectedIndex =
                        index

                    Return

                End If

            Next

            cboSubmission.SelectedIndex =
                0

        End Sub


        Private Sub SelectDecision(
            decisionId As Guid?
        )

            For index As Integer =
                0 To cboDecision.Items.Count - 1

                Dim optionItem As DecisionOption =
                    TryCast(
                        cboDecision.Items(index),
                        DecisionOption
                    )

                If optionItem Is Nothing Then
                    Continue For
                End If

                If NullableGuidEquals(
                    optionItem.DecisionId,
                    decisionId
                ) Then

                    cboDecision.SelectedIndex =
                        index

                    Return

                End If

            Next

            cboDecision.SelectedIndex =
                0

        End Sub


        Private Sub SubmissionChanged(
            sender As Object,
            e As EventArgs
        )

            If _suppressAssociationEvents Then
                Return
            End If

            Dim decision As DecisionOption =
                TryCast(
                    cboDecision.SelectedItem,
                    DecisionOption
                )

            If decision Is Nothing OrElse
               Not decision.DecisionId.HasValue Then

                Return
            End If

            If Not NullableGuidEquals(
                decision.SubmissionId,
                SubmissionId
            ) Then

                _suppressAssociationEvents =
                    True

                Try
                    cboDecision.SelectedIndex =
                        0
                Finally
                    _suppressAssociationEvents =
                        False
                End Try

            End If

        End Sub


        Private Sub DecisionChanged(
            sender As Object,
            e As EventArgs
        )

            If _suppressAssociationEvents Then
                Return
            End If

            Dim decision As DecisionOption =
                TryCast(
                    cboDecision.SelectedItem,
                    DecisionOption
                )

            If decision Is Nothing OrElse
               Not decision.DecisionId.HasValue OrElse
               Not decision.SubmissionId.HasValue Then

                Return
            End If

            _suppressAssociationEvents =
                True

            Try

                SelectSubmission(
                    decision.SubmissionId
                )

            Finally

                _suppressAssociationEvents =
                    False

            End Try

        End Sub


        Private Sub BrowseForVersionFile(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New OpenFileDialog With {
                .Title = "Select manuscript version file",
                .Filter =
                    "Manuscript files|*.doc;*.docx;*.pdf;*.rtf;*.txt;*.tex;*.odt|All files|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                txtFilePath.Text =
                    dialog.FileName

                If rbMetadataOnly.Checked Then
                    rbManagedCopy.Checked =
                        True
                End If

                UpdateFileEditingState()

            End Using

        End Sub


        Private Sub ClearVersionFile(
            sender As Object,
            e As EventArgs
        )

            txtFilePath.Text =
                String.Empty

            rbMetadataOnly.Checked =
                True

            UpdateFileEditingState()

        End Sub


        Private Sub SaveVersion(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtLabel.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Please enter a short label for this manuscript version.",
                    "Version Label Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtLabel.Focus()
                Return

            End If

            Dim filePath As String =
                VersionFilePath

            Dim filePathChanged As Boolean =
                _existingVersion Is Nothing OrElse
                Not String.Equals(
                    If(
                        _existingVersion.LocalFilePath,
                        String.Empty
                    ).Trim(),
                    filePath,
                    StringComparison.OrdinalIgnoreCase
                )

            If Not String.IsNullOrWhiteSpace(
                filePath
            ) AndAlso
               Not File.Exists(
                   filePath
               ) AndAlso
               filePathChanged Then

                MessageBox.Show(
                    Me,
                    "The selected manuscript version file could not be found." &
                    Environment.NewLine &
                    Environment.NewLine &
                    filePath,
                    "Version File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            If String.IsNullOrWhiteSpace(
                filePath
            ) Then

                rbMetadataOnly.Checked =
                    True

            ElseIf rbMetadataOnly.Checked Then

                MessageBox.Show(
                    Me,
                    "Choose whether PaperRoute should copy this file into the PaperRoute Library or link to the original file.",
                    "Choose Version Storage",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Function BuildProvenanceText(
            version As ManuscriptVersion
        ) As String

            Dim recordedText As String =
                FormatAuditTimestamp(
                    version.RecordedAtUtc,
                    "Recorded in PaperRoute: not available for this legacy record"
                )

            Dim modifiedText As String

            If version.LastModifiedAtUtc.HasValue Then

                modifiedText =
                    "Last edited: " &
                    version.LastModifiedAtUtc.
                        Value.
                        ToLocalTime().
                        ToString(
                            "MMM d, yyyy h:mm tt"
                        )

            Else

                modifiedText =
                    "Last edited: no later metadata edits recorded"

            End If

            Return recordedText &
                "   •   " &
                modifiedText

        End Function


        Private Function FormatAuditTimestamp(
            value As DateTime?,
            fallback As String
        ) As String

            If Not value.HasValue Then
                Return fallback
            End If

            Return "Recorded in PaperRoute: " &
                value.Value.
                    ToLocalTime().
                    ToString(
                        "MMM d, yyyy h:mm tt"
                    )

        End Function


        Private Shared Function NullableGuidEquals(
            left As Guid?,
            right As Guid?
        ) As Boolean

            If left.HasValue <>
               right.HasValue Then

                Return False

            End If

            If Not left.HasValue Then
                Return True
            End If

            Return left.Value =
                right.Value

        End Function


        Private Shared Function SafePickerDate(
            value As DateTime
        ) As DateTime

            If value < DateTimePicker.MinimumDateTime OrElse
               value > DateTimePicker.MaximumDateTime Then

                Return DateTime.Today

            End If

            Return value.Date

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
