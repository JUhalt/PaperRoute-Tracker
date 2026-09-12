Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class SubmissionPacketEditForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _existingPacket As SubmissionPacket

        Private ReadOnly txtLabel As New TextBox()
        Private ReadOnly cmbVersion As New ComboBox()
        Private ReadOnly cmbReadiness As New ComboBox()
        Private ReadOnly cmbSubmission As New ComboBox()
        Private ReadOnly nudRevisionRound As New NumericUpDown()
        Private ReadOnly txtNotes As New TextBox()


        Public Sub New(
            manuscript As Manuscript,
            existingPacket As SubmissionPacket,
            Optional workflowContext As SubmissionWorkflowRequest = Nothing
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(
                    NameOf(manuscript)
                )
            End If

            _manuscript =
                manuscript

            _existingPacket =
                existingPacket

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            PopulateOptions()
            LoadExistingPacket()
            If _existingPacket Is Nothing Then ApplyWorkflowDefaults(workflowContext)

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _existingPacket Is Nothing,
                    "New Submission Packet",
                    "Edit Submission Packet"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    760,
                    650
                )

            Me.MinimumSize =
                New Size(
                    660,
                    560
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
                .ColumnCount = 2,
                .RowCount = 8,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    190
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(680, 0),
                .Text =
                    "A Submission Packet is a preparation record tied to one exact Version History snapshot. " &
                    "It may exist before the manuscript is actually submitted. Linking a real journal submission is optional.",
                .Margin = New Padding(0, 0, 0, 12)
            }

            root.Controls.Add(
                intro,
                0,
                0
            )

            root.SetColumnSpan(
                intro,
                2
            )

            txtLabel.Dock =
                DockStyle.Fill

            txtLabel.PlaceholderText =
                "e.g., Initial submission packet"

            root.Controls.Add(
                CreateFieldLabel(
                    "Packet name"
                ),
                0,
                1
            )

            root.Controls.Add(
                txtLabel,
                1,
                1
            )

            cmbVersion.Dock =
                DockStyle.Fill

            cmbVersion.DropDownStyle =
                ComboBoxStyle.DropDownList

            root.Controls.Add(
                CreateFieldLabel(
                    "Exact manuscript version"
                ),
                0,
                2
            )

            root.Controls.Add(
                cmbVersion,
                1,
                2
            )

            cmbReadiness.Dock =
                DockStyle.Fill

            cmbReadiness.DropDownStyle =
                ComboBoxStyle.DropDownList

            root.Controls.Add(
                CreateFieldLabel(
                    "Readiness profile"
                ),
                0,
                3
            )

            root.Controls.Add(
                cmbReadiness,
                1,
                3
            )

            cmbSubmission.Dock =
                DockStyle.Fill

            cmbSubmission.DropDownStyle =
                ComboBoxStyle.DropDownList

            AddHandler cmbSubmission.SelectedIndexChanged,
                AddressOf SubmissionSelectionChanged

            root.Controls.Add(
                CreateFieldLabel(
                    "Real journal submission"
                ),
                0,
                4
            )

            root.Controls.Add(
                cmbSubmission,
                1,
                4
            )

            nudRevisionRound.Minimum =
                0

            nudRevisionRound.Maximum =
                Integer.MaxValue

            nudRevisionRound.Width =
                130

            Dim revisionPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False
            }

            revisionPanel.Controls.Add(
                nudRevisionRound
            )

            revisionPanel.Controls.Add(
                New Label With {
                    .Text = "0 = not associated with a revision round",
                    .AutoSize = True,
                    .ForeColor = SystemColors.GrayText,
                    .Margin = New Padding(8, 7, 0, 0)
                }
            )

            root.Controls.Add(
                CreateFieldLabel(
                    "Revision round"
                ),
                0,
                5
            )

            root.Controls.Add(
                revisionPanel,
                1,
                5
            )

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            txtNotes.PlaceholderText =
                "Optional preparation notes"

            root.Controls.Add(
                CreateFieldLabel(
                    "Notes"
                ),
                0,
                6
            )

            root.Controls.Add(
                txtNotes,
                1,
                6
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 12, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text =
                    If(
                        _existingPacket Is Nothing,
                        "Create Packet",
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
                AddressOf SavePacket

            footer.Controls.Add(
                btnSave
            )

            footer.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                footer,
                0,
                7
            )

            root.SetColumnSpan(
                footer,
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


        Private Function CreateFieldLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(
                    Me.Font,
                    FontStyle.Bold
                ),
                .Margin = New Padding(
                    3,
                    9,
                    3,
                    9
                )
            }

        End Function


        Private Sub PopulateOptions()

            cmbVersion.Items.Clear()

            For Each version As ManuscriptVersion In
                ManuscriptVersionService.GetChronologicalVersions(
                    _manuscript
                )

                cmbVersion.Items.Add(
                    New VersionOption(
                        version
                    )
                )

            Next

            cmbReadiness.Items.Clear()

            cmbReadiness.Items.Add(
                New NullableGuidOption(
                    "(No readiness profile)",
                    Nothing
                )
            )

            If _manuscript.ReadinessProfiles IsNot Nothing Then

                For Each profile As ManuscriptReadiness In
                    _manuscript.ReadinessProfiles.
                        Where(
                            Function(item)
                                Return item IsNot Nothing
                            End Function
                        ).
                        OrderBy(
                            Function(item)
                                Return item.JournalName
                            End Function,
                            StringComparer.CurrentCultureIgnoreCase
                        )

                    cmbReadiness.Items.Add(
                        New NullableGuidOption(
                            If(
                                String.IsNullOrWhiteSpace(
                                    profile.JournalName
                                ),
                                "(Unnamed readiness profile)",
                                profile.JournalName
                            ),
                            profile.Id
                        )
                    )

                Next

            End If

            cmbSubmission.Items.Clear()

            cmbSubmission.Items.Add(
                New NullableGuidOption(
                    "(Not yet submitted)",
                    Nothing
                )
            )

            If _manuscript.Submissions IsNot Nothing Then

                For Each submission As JournalSubmission In
                    _manuscript.Submissions.
                        Where(
                            Function(item)
                                Return item IsNot Nothing
                            End Function
                        ).
                        OrderByDescending(
                            Function(item)
                                Return item.SubmittedDate
                            End Function
                        )

                    Dim display As String =
                        If(
                            String.IsNullOrWhiteSpace(
                                submission.JournalName
                            ),
                            "(Unnamed journal)",
                            submission.JournalName
                        ) &
                        " — " &
                        submission.SubmittedDate.ToShortDateString()

                    cmbSubmission.Items.Add(
                        New NullableGuidOption(
                            display,
                            submission.Id
                        )
                    )

                Next

            End If

            If cmbReadiness.Items.Count > 0 Then
                cmbReadiness.SelectedIndex =
                    0
            End If

            If cmbSubmission.Items.Count > 0 Then
                cmbSubmission.SelectedIndex =
                    0
            End If

            If cmbVersion.Items.Count > 0 Then

                Dim preferredId As Guid? =
                    _manuscript.CurrentVersionId

                Dim preferredIndex As Integer =
                    0

                If preferredId.HasValue Then

                    For index As Integer =
                        0 To cmbVersion.Items.Count - 1

                        Dim optionItem As VersionOption =
                            TryCast(
                                cmbVersion.Items(index),
                                VersionOption
                            )

                        If optionItem IsNot Nothing AndAlso
                           optionItem.Id =
                               preferredId.Value Then

                            preferredIndex =
                                index

                            Exit For

                        End If

                    Next

                End If

                cmbVersion.SelectedIndex =
                    preferredIndex

            End If

        End Sub


        Private Sub ApplyWorkflowDefaults(context As SubmissionWorkflowRequest)

            If context Is Nothing Then Return

            If context.VersionId.HasValue Then
                cmbVersion.SelectedIndex = -1
                SelectGuidOption(cmbVersion, context.VersionId.Value)
            End If
            If context.ReadinessProfileId.HasValue Then
                cmbReadiness.SelectedIndex = -1
                SelectNullableGuidOption(cmbReadiness, context.ReadinessProfileId)
            End If
            If context.SubmissionId.HasValue Then
                cmbSubmission.SelectedIndex = -1
                SelectNullableGuidOption(cmbSubmission, context.SubmissionId)
            End If

        End Sub


        Private Sub LoadExistingPacket()

            If _existingPacket Is Nothing Then

                txtLabel.Text =
                    "Submission packet"

                Return

            End If

            txtLabel.Text =
                _existingPacket.Label

            txtNotes.Text =
                _existingPacket.Notes

            SelectGuidOption(
                cmbVersion,
                _existingPacket.ManuscriptVersionId
            )

            SelectNullableGuidOption(
                cmbReadiness,
                _existingPacket.ReadinessProfileId
            )

            SelectNullableGuidOption(
                cmbSubmission,
                _existingPacket.SubmissionId
            )

            nudRevisionRound.Value =
                If(
                    _existingPacket.RevisionRoundNumber.HasValue,
                    Math.Min(
                        CInt(nudRevisionRound.Maximum),
                        _existingPacket.RevisionRoundNumber.Value
                    ),
                    0
                )

        End Sub


        Private Sub SubmissionSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            Dim optionItem As NullableGuidOption =
                TryCast(
                    cmbSubmission.SelectedItem,
                    NullableGuidOption
                )

            Dim hasSubmission As Boolean =
                optionItem IsNot Nothing AndAlso
                optionItem.Id.HasValue

            nudRevisionRound.Enabled =
                hasSubmission

            If Not hasSubmission Then
                nudRevisionRound.Value =
                    0
            End If

        End Sub


        Private Sub SavePacket(
            sender As Object,
            e As EventArgs
        )

            Dim versionOption As VersionOption =
                TryCast(
                    cmbVersion.SelectedItem,
                    VersionOption
                )

            If versionOption Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Create at least one Version History record before creating a Submission Packet.",
                    "Exact Manuscript Version Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim readinessId As Guid? =
                SelectedNullableGuid(
                    cmbReadiness
                )

            Dim submissionId As Guid? =
                SelectedNullableGuid(
                    cmbSubmission
                )

            Dim revisionRound As Integer? =
                Nothing

            If submissionId.HasValue AndAlso
               nudRevisionRound.Value > 0 Then

                revisionRound =
                    Decimal.ToInt32(
                        nudRevisionRound.Value
                    )

            End If

            Try

                If _existingPacket Is Nothing Then

                    SubmissionPacketService.CreatePacket(
                        _manuscript,
                        versionOption.Id,
                        txtLabel.Text,
                        txtNotes.Text,
                        readinessId,
                        submissionId,
                        revisionRound
                    )

                Else

                    SubmissionPacketService.UpdatePacket(
                        _manuscript,
                        _existingPacket.Id,
                        versionOption.Id,
                        txtLabel.Text,
                        txtNotes.Text,
                        readinessId,
                        submissionId,
                        revisionRound
                    )

                End If

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check Submission Packet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            Catch ex As InvalidOperationException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check Submission Packet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End Try

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Shared Function SelectedNullableGuid(
            combo As ComboBox
        ) As Guid?

            Dim optionItem As NullableGuidOption =
                TryCast(
                    combo.SelectedItem,
                    NullableGuidOption
                )

            If optionItem Is Nothing Then
                Return Nothing
            End If

            Return optionItem.Id

        End Function


        Private Shared Sub SelectNullableGuidOption(
            combo As ComboBox,
            targetId As Guid?
        )

            For index As Integer =
                0 To combo.Items.Count - 1

                Dim optionItem As NullableGuidOption =
                    TryCast(
                        combo.Items(index),
                        NullableGuidOption
                    )

                If optionItem Is Nothing Then
                    Continue For
                End If

                If NullableGuidEquals(
                    optionItem.Id,
                    targetId
                ) Then

                    combo.SelectedIndex =
                        index

                    Return

                End If

            Next

        End Sub


        Private Shared Sub SelectGuidOption(
            combo As ComboBox,
            targetId As Guid
        )

            For index As Integer =
                0 To combo.Items.Count - 1

                Dim optionItem As VersionOption =
                    TryCast(
                        combo.Items(index),
                        VersionOption
                    )

                If optionItem IsNot Nothing AndAlso
                   optionItem.Id = targetId Then

                    combo.SelectedIndex =
                        index

                    Return

                End If

            Next

        End Sub


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


        Private NotInheritable Class VersionOption

            Friend ReadOnly Property Id As Guid
            Private ReadOnly _display As String


            Friend Sub New(
                version As ManuscriptVersion
            )

                Id =
                    version.Id

                Dim label As String =
                    If(
                        String.IsNullOrWhiteSpace(
                            version.Label
                        ),
                        "(Unlabeled version)",
                        version.Label
                    )

                _display =
                    version.CreatedDate.ToShortDateString() &
                    " — " &
                    label

            End Sub


            Public Overrides Function ToString() As String

                Return _display

            End Function

        End Class


        Private NotInheritable Class NullableGuidOption

            Friend ReadOnly Property Id As Guid?
            Private ReadOnly _display As String


            Friend Sub New(
                display As String,
                id As Guid?
            )

                _display =
                    display

                Me.Id =
                    id

            End Sub


            Public Overrides Function ToString() As String

                Return _display

            End Function

        End Class

    End Class

End Namespace
