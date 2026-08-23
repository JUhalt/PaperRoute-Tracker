Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddSubmissionForm
        Inherits Form

        Private ReadOnly _existingSubmission As JournalSubmission
        Private ReadOnly _authorRepository As New AuthorLibraryRepository()

        Private ReadOnly txtJournal As New TextBox()
        Private ReadOnly txtManuscriptNumber As New TextBox()
        Private ReadOnly dtpSubmitted As New DateTimePicker()
        Private ReadOnly txtPortalUrl As New TextBox()
        Private ReadOnly chkFollowUp As New CheckBox()
        Private ReadOnly dtpFollowUp As New DateTimePicker()
        Private ReadOnly txtNotes As New TextBox()

        Private _selectedJournalId As Guid?
        Private _createdSubmission As JournalSubmission


        Public ReadOnly Property CreatedSubmission As JournalSubmission
            Get
                Return _createdSubmission
            End Get
        End Property


        Public Sub New()

            _existingSubmission = Nothing

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Public Sub New(
            initialJournalName As String
        )

            Me.New()

            txtJournal.Text =
                If(
                    initialJournalName,
                    String.Empty
                ).Trim()

        End Sub


        Public Sub New(
            existingSubmission As JournalSubmission
        )

            _existingSubmission =
                existingSubmission

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadExistingSubmission()

        End Sub


        Private Sub BuildInterface()

            If _existingSubmission Is Nothing Then

                Me.Text =
                    "Record Journal Submission"

            Else

                Me.Text =
                    "Edit Journal Submission"

            End If

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.FormBorderStyle =
                FormBorderStyle.FixedDialog

            Me.MaximizeBox = False
            Me.MinimizeBox = False

            Me.ClientSize =
                New Size(760, 610)

            Me.Font =
                New Font("Segoe UI", 10.0F)

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 8,
                .Padding = New Padding(22)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    170
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))

            txtJournal.Dock =
                DockStyle.Fill

            Dim journalEditor As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .Margin = New Padding(0)
            }

            journalEditor.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            journalEditor.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            Dim btnJournalLibrary As New Button With {
                .Text = "Use Library...",
                .AutoSize = True,
                .Height = 32,
                .Margin = New Padding(8, 0, 0, 0)
            }

            AddHandler btnJournalLibrary.Click,
                AddressOf ChooseJournalFromLibrary

            journalEditor.Controls.Add(
                txtJournal,
                0,
                0
            )

            journalEditor.Controls.Add(
                btnJournalLibrary,
                1,
                0
            )

            txtManuscriptNumber.Dock =
                DockStyle.Fill

            txtPortalUrl.Dock =
                DockStyle.Fill

            dtpSubmitted.Format =
                DateTimePickerFormat.Short

            dtpSubmitted.Value =
                DateTime.Today

            dtpSubmitted.Width =
                180

            chkFollowUp.Text =
                "Remind me to follow up"

            chkFollowUp.AutoSize =
                True

            chkFollowUp.Anchor =
                AnchorStyles.Left

            chkFollowUp.Margin =
                New Padding(
                    0,
                    8,
                    12,
                    0
                )

            dtpFollowUp.Format =
                DateTimePickerFormat.Short

            dtpFollowUp.Value =
                DateTime.Today.AddDays(30)

            dtpFollowUp.Width =
                180

            dtpFollowUp.Enabled =
                False

            AddHandler chkFollowUp.CheckedChanged,
                Sub(sender, e)

                    dtpFollowUp.Enabled =
                        chkFollowUp.Checked

                End Sub

            AddHandler dtpSubmitted.ValueChanged,
                Sub(sender, e)

                    If _existingSubmission Is Nothing AndAlso
                       Not chkFollowUp.Checked Then

                        Dim candidate As DateTime =
                            dtpSubmitted.Value.Date.AddDays(30)

                        If candidate <=
                           DateTimePicker.MaximumDateTime Then

                            dtpFollowUp.Value =
                                candidate

                        End If

                    End If

                End Sub

            Dim followUpPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Margin = New Padding(0)
            }

            followUpPanel.Controls.Add(
                chkFollowUp
            )

            followUpPanel.Controls.Add(
                dtpFollowUp
            )

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            root.Controls.Add(
                CreateFieldLabel("Journal"),
                0,
                0
            )

            root.Controls.Add(
                journalEditor,
                1,
                0
            )

            root.Controls.Add(
                CreateFieldLabel("Manuscript number"),
                0,
                1
            )

            root.Controls.Add(
                txtManuscriptNumber,
                1,
                1
            )

            root.Controls.Add(
                CreateFieldLabel("Submitted"),
                0,
                2
            )

            root.Controls.Add(
                dtpSubmitted,
                1,
                2
            )

            root.Controls.Add(
                CreateFieldLabel("Publisher portal URL"),
                0,
                3
            )

            root.Controls.Add(
                txtPortalUrl,
                1,
                3
            )

            root.Controls.Add(
                CreateFieldLabel("Follow-up"),
                0,
                4
            )

            root.Controls.Add(
                followUpPanel,
                1,
                4
            )

            Dim lblNotes As New Label With {
                .Text = "Submission Notes",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    )
            }

            root.Controls.Add(
                lblNotes,
                0,
                5
            )

            root.SetColumnSpan(
                lblNotes,
                2
            )

            root.Controls.Add(
                txtNotes,
                0,
                6
            )

            root.SetColumnSpan(
                txtNotes,
                2
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection =
                    FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnSave As New Button With {
                .AutoSize = True,
                .Height = 36
            }

            If _existingSubmission Is Nothing Then

                btnSave.Text =
                    "Add Submission"

            Else

                btnSave.Text =
                    "Save Changes"

            End If

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult =
                    DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveSubmission

            buttons.Controls.Add(
                btnSave
            )

            buttons.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                buttons,
                0,
                7
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


        Private Sub LoadExistingSubmission()

            txtJournal.Text =
                _existingSubmission.JournalName

            _selectedJournalId =
                _existingSubmission.JournalId

            txtManuscriptNumber.Text =
                _existingSubmission.ManuscriptNumber

            dtpSubmitted.Value =
                SafePickerDate(
                    _existingSubmission.SubmittedDate
                )

            txtPortalUrl.Text =
                _existingSubmission.PortalUrl

            If _existingSubmission.FollowUpDate.HasValue Then

                chkFollowUp.Checked =
                    True

                dtpFollowUp.Value =
                    SafePickerDate(
                        _existingSubmission.FollowUpDate.Value
                    )

            End If

            txtNotes.Text =
                _existingSubmission.Notes

        End Sub


        Private Function CreateFieldLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor =
                    AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    )
            }

        End Function


        Private Sub ChooseJournalFromLibrary(
            sender As Object,
            e As EventArgs
        )

            Dim library As AuthorLibraryData =
                _authorRepository.Load()

            If library.Journals.Count = 0 Then

                MessageBox.Show(
                    Me,
                    "The Journal Library is empty. Add journals from Data → Journal Library first.",
                    "No Reusable Journals",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using dialog As New JournalPickerForm(
                library
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.SelectedJournal Is Nothing Then

                    Return

                End If

                Dim priorStandardPortal As String =
                    String.Empty

                If _selectedJournalId.HasValue Then

                    Dim priorJournal As JournalRecord =
                        library.Journals.
                            FirstOrDefault(
                                Function(item)
                                    Return item IsNot Nothing AndAlso
                                        item.Id = _selectedJournalId.Value
                                End Function
                            )

                    If priorJournal IsNot Nothing Then

                        priorStandardPortal =
                            priorJournal.SubmissionPortalUrl

                    End If

                End If

                Dim replacePortal As Boolean =
                    String.IsNullOrWhiteSpace(
                        txtPortalUrl.Text
                    ) OrElse
                    (
                        Not String.IsNullOrWhiteSpace(
                            priorStandardPortal
                        ) AndAlso
                        String.Equals(
                            txtPortalUrl.Text.Trim(),
                            priorStandardPortal.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )

                _selectedJournalId =
                    dialog.SelectedJournal.Id

                txtJournal.Text =
                    dialog.SelectedJournal.Name

                If replacePortal Then

                    txtPortalUrl.Text =
                        dialog.SelectedJournal.SubmissionPortalUrl

                End If

            End Using

        End Sub


        Private Sub SaveSubmission(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtJournal.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Please enter the journal name.",
                    "Journal Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtJournal.Focus()

                Return

            End If

            Dim portalText As String

            Try

                portalText =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtPortalUrl.Text,
                        "Publisher portal"
                    )

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message &
                    Environment.NewLine &
                    Environment.NewLine &
                    "You may also leave this field blank.",
                    "Invalid Publisher URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtPortalUrl.Focus()
                Return

            End Try

            Dim journalId As Guid? =
                _selectedJournalId

            If journalId.HasValue Then

                Dim originalJournalName As String =
                    If(
                        _existingSubmission Is Nothing,
                        String.Empty,
                        If(
                            _existingSubmission.JournalName,
                            String.Empty
                        ).Trim()
                    )

                Dim journalTextChanged As Boolean =
                    _existingSubmission Is Nothing OrElse
                    Not String.Equals(
                        originalJournalName,
                        txtJournal.Text.Trim(),
                        StringComparison.CurrentCultureIgnoreCase
                    )

                If journalTextChanged Then

                    Dim library As AuthorLibraryData =
                        _authorRepository.Load()

                    Dim linked As JournalRecord =
                        library.Journals.
                            FirstOrDefault(
                                Function(item)
                                    Return item IsNot Nothing AndAlso
                                        item.Id = journalId.Value
                                End Function
                            )

                    If linked Is Nothing OrElse
                       Not String.Equals(
                           linked.Name,
                           txtJournal.Text.Trim(),
                           StringComparison.CurrentCultureIgnoreCase
                       ) Then

                        journalId =
                            Nothing

                    End If

                End If

            End If

            Dim submissionId As Guid

            Dim decisions As List(Of EditorialDecisionEvent)

            Dim correspondence As List(Of CorrespondenceItem)

            If _existingSubmission Is Nothing Then

                submissionId =
                    Guid.NewGuid()

                decisions =
                    New List(Of EditorialDecisionEvent)()

                correspondence =
                    New List(Of CorrespondenceItem)()

            Else

                submissionId =
                    _existingSubmission.Id

                decisions =
                    New List(Of EditorialDecisionEvent)(
                        If(
                            _existingSubmission.Decisions,
                            New List(Of EditorialDecisionEvent)()
                        )
                    )

                correspondence =
                    New List(Of CorrespondenceItem)(
                        If(
                            _existingSubmission.Correspondence,
                            New List(Of CorrespondenceItem)()
                        )
                    )

            End If

            Dim followUpDate As DateTime? =
                Nothing

            If chkFollowUp.Checked Then

                followUpDate =
                    dtpFollowUp.Value.Date

            End If

            _createdSubmission =
                New JournalSubmission With {
                    .Id =
                        submissionId,
                    .JournalName =
                        txtJournal.Text.Trim(),
                    .JournalId =
                        journalId,
                    .ManuscriptNumber =
                        txtManuscriptNumber.Text.Trim(),
                    .SubmittedDate =
                        dtpSubmitted.Value.Date,
                    .FollowUpDate =
                        followUpDate,
                    .PortalUrl =
                        portalText,
                    .Notes =
                        txtNotes.Text.Trim(),
                    .Decisions =
                        decisions,
                    .Correspondence =
                        correspondence
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Shared Function SafePickerDate(
            value As DateTime
        ) As DateTime

            If value < DateTimePicker.MinimumDateTime OrElse
               value > DateTimePicker.MaximumDateTime Then

                Return DateTime.Today

            End If

            Return value.Date

        End Function

    End Class

End Namespace
