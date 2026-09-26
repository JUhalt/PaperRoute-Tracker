Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddManuscriptForm
        Inherits Form

        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtTargetJournal As New TextBox()
        Private ReadOnly cmbStage As New ComboBox()
        Private ReadOnly btnPasteTitlePage As New Button()
        Private ReadOnly lblProposal As New Label()
        Private ReadOnly lnkClearProposal As New LinkLabel()
        Private ReadOnly chkDeadline As New CheckBox()
        Private ReadOnly txtDeadline As New TextBox()
        Private ReadOnly dtpDeadline As New DateTimePicker()

        ' Nothing when the author library could not be loaded; pasting a title
        ' page is then unavailable and authors are added after creation.
        Private ReadOnly _authorLibrary As AuthorLibraryData

        Private _createdManuscript As Manuscript
        Private _proposal As TitlePageParseResult
        Private _titlePageText As String = String.Empty
        Private _authorLibraryChanged As Boolean

        Public ReadOnly Property CreatedManuscript As Manuscript
            Get
                Return _createdManuscript
            End Get
        End Property

        ' True when adding the manuscript created reusable authors or
        ' affiliations; the caller saves the library before the manuscript.
        Public ReadOnly Property AuthorLibraryChanged As Boolean
            Get
                Return _authorLibraryChanged
            End Get
        End Property

        Public Sub New(Optional authorLibrary As AuthorLibraryData = Nothing)
            _authorLibrary = authorLibrary
            BuildInterface()
            UiPolish.ApplyDialog(Me)
            UpdateProposalSummary()
        End Sub

        ' Applies a reviewed title-page proposal as if it came from the paste dialog.
        Friend Sub UseTitlePageProposal(proposal As TitlePageParseResult)

            _proposal = proposal

            If proposal IsNot Nothing AndAlso proposal.Title.Length > 0 Then
                txtTitle.Text = proposal.Title
            End If

            UpdateProposalSummary()

        End Sub

        Friend Sub SetFirstDeadline(label As String, dueDate As DateTime)

            chkDeadline.Checked = True
            txtDeadline.Text = label
            dtpDeadline.Value = dueDate

        End Sub

        Private Sub BuildInterface()

            Me.Text = "Add Manuscript"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(660, 400)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            ' Grow to fit the wrapped help text and deadline row at any display scaling.
            Me.AutoSize = True
            Me.AutoSizeMode = AutoSizeMode.GrowOnly

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .ColumnCount = 2,
                .RowCount = 7,
                .Padding = New Padding(20)
            }

            layout.ColumnStyles.Add(
                New ColumnStyle(SizeType.Absolute, 145)
            )

            layout.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 100)
            )

            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))

            txtTitle.Dock = DockStyle.Fill
            txtTargetJournal.Dock = DockStyle.Fill

            cmbStage.Dock = DockStyle.Fill
            cmbStage.DropDownStyle = ComboBoxStyle.DropDownList

            cmbStage.Items.Add(
                PaperStage.Idea
            )

            cmbStage.Items.Add(
                PaperStage.Draft
            )

            cmbStage.SelectedItem =
                PaperStage.Draft

            layout.Controls.Add(
                CreateFieldLabel("Title"),
                0,
                0
            )

            layout.Controls.Add(txtTitle, 1, 0)

            layout.Controls.Add(
                CreateFieldLabel("Target journal"),
                0,
                1
            )

            layout.Controls.Add(txtTargetJournal, 1, 1)

            layout.Controls.Add(
                CreateFieldLabel("Starting stage"),
                0,
                2
            )

            layout.Controls.Add(cmbStage, 1, 2)

            layout.Controls.Add(
                CreateFieldLabel("From a draft"),
                0,
                3
            )

            layout.Controls.Add(BuildTitlePageRow(), 1, 3)

            layout.Controls.Add(
                CreateFieldLabel("First deadline"),
                0,
                4
            )

            layout.Controls.Add(BuildDeadlineRow(), 1, 4)

            Dim lblAuthorsNote As New Label With {
                .Text =
                    "New manuscripts begin as an Idea or Draft. Add a Journal Submission to enter the submission workflow; " &
                    "editorial decisions then drive Revision, Accepted, and rejection outcomes. " &
                    "Authors can come from a pasted title page or be added after the manuscript is created.",
                .AutoSize = True,
                .UseMnemonic = False,
                .MaximumSize = New Size(600, 0),
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 8, 0, 8)
            }

            layout.Controls.Add(
                lblAuthorsNote,
                0,
                5
            )

            layout.SetColumnSpan(
                lblAuthorsNote,
                2
            )

            Dim btnAdd As New Button With {
                .Text = "Add Manuscript",
                .AutoSize = True,
                .Height = 34
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 34,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnAdd.Click, AddressOf AddManuscript

            Dim buttonPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            buttonPanel.Controls.Add(btnAdd)
            buttonPanel.Controls.Add(btnCancel)

            layout.Controls.Add(buttonPanel, 0, 6)
            layout.SetColumnSpan(buttonPanel, 2)

            Me.AcceptButton = btnAdd
            Me.CancelButton = btnCancel

            Me.Controls.Add(layout)

        End Sub

        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function

        Private Function BuildTitlePageRow() As Control

            Dim row As New FlowLayoutPanel With {
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .WrapContents = True,
                .Margin = New Padding(0, 4, 0, 4)
            }

            btnPasteTitlePage.Text = "Paste a Title Page..."
            btnPasteTitlePage.AutoSize = True
            btnPasteTitlePage.Height = 34
            btnPasteTitlePage.Enabled = _authorLibrary IsNot Nothing
            AddHandler btnPasteTitlePage.Click, AddressOf OpenTitlePage

            lblProposal.AutoSize = True
            lblProposal.UseMnemonic = False
            lblProposal.MaximumSize = New Size(300, 0)
            lblProposal.ForeColor = UiTheme.SecondaryText()
            lblProposal.Margin = New Padding(8, 8, 0, 0)

            lnkClearProposal.Text = "Clear"
            lnkClearProposal.AutoSize = True
            lnkClearProposal.Margin = New Padding(6, 8, 0, 0)
            AddHandler lnkClearProposal.LinkClicked,
                Sub(sender, e)
                    _proposal = Nothing
                    UpdateProposalSummary()
                End Sub

            row.Controls.Add(btnPasteTitlePage)
            row.Controls.Add(lblProposal)
            row.Controls.Add(lnkClearProposal)

            Return row

        End Function

        Private Function BuildDeadlineRow() As Control

            Dim row As New FlowLayoutPanel With {
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .WrapContents = True,
                .Margin = New Padding(0, 4, 0, 4)
            }

            chkDeadline.Text = "Remind me"
            chkDeadline.AutoSize = True
            chkDeadline.Margin = New Padding(0, 6, 8, 0)

            txtDeadline.Width = 220
            txtDeadline.PlaceholderText = "e.g. Send draft to coauthors"
            txtDeadline.AccessibleName = "First deadline"

            dtpDeadline.Format = DateTimePickerFormat.Short
            dtpDeadline.Width = 130
            dtpDeadline.Value = DateTime.Today.AddDays(14)
            dtpDeadline.AccessibleName = "First deadline due date"

            Dim syncEnabled As Action =
                Sub()
                    txtDeadline.Enabled = chkDeadline.Checked
                    dtpDeadline.Enabled = chkDeadline.Checked
                End Sub

            AddHandler chkDeadline.CheckedChanged, Sub(sender, e) syncEnabled()
            syncEnabled()

            row.Controls.Add(chkDeadline)
            row.Controls.Add(txtDeadline)
            row.Controls.Add(dtpDeadline)

            Return row

        End Function

        Private Sub OpenTitlePage(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New TitlePageImportForm(_authorLibrary, _titlePageText)
                If dialog.ShowDialog(Me) <> DialogResult.OK OrElse
                   dialog.ReviewedProposal Is Nothing Then
                    Return
                End If

                _titlePageText = dialog.SourceText
                UseTitlePageProposal(dialog.ReviewedProposal)
            End Using

        End Sub

        Private Sub UpdateProposalSummary()

            If _authorLibrary Is Nothing Then
                lblProposal.Text = "Unavailable because the author library could not be loaded."
                lnkClearProposal.Visible = False
                Return
            End If

            If _proposal Is Nothing Then
                lblProposal.Text = "Optional. Proposes authors, abstract, and keywords."
                lnkClearProposal.Visible = False
                Return
            End If

            Dim parts As New List(Of String)()

            If _proposal.Authors.Count > 0 Then
                parts.Add(_proposal.Authors.Count.ToString() & If(_proposal.Authors.Count = 1, " author", " authors"))
            End If

            If _proposal.AbstractText.Length > 0 Then
                parts.Add("the abstract")
            End If

            If _proposal.Keywords.Count > 0 Then
                parts.Add(_proposal.Keywords.Count.ToString() & If(_proposal.Keywords.Count = 1, " keyword", " keywords"))
            End If

            If parts.Count = 0 Then
                lblProposal.Text = "Title only."
            ElseIf parts.Count = 1 Then
                lblProposal.Text = "Will add " & parts(0) & "."
            Else
                lblProposal.Text =
                    "Will add " &
                    String.Join(", ", parts.Take(parts.Count - 1)) &
                    If(parts.Count > 2, ",", String.Empty) &
                    " and " & parts(parts.Count - 1) & "."
            End If
            lnkClearProposal.Visible = True

        End Sub

        Private Sub AddManuscript(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(txtTitle.Text) Then

                MessageBox.Show(
                    Me,
                    "Please enter a manuscript title.",
                    "Title Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()
                Return

            End If

            Dim selectedStage As PaperStage =
                CType(cmbStage.SelectedItem, PaperStage)

            Dim createdAt As DateTime =
                DateTime.Now

            _createdManuscript = New Manuscript With {
                .Title = txtTitle.Text.Trim(),
                .TargetJournal = txtTargetJournal.Text.Trim(),
                .CurrentStage = selectedStage,
                .Location = ManuscriptLocation.Pipeline,
                .StageEnteredDate = createdAt
            }

            Dim initialHistory As New HistoryEvent With {
                .EventDate = createdAt,
                .Stage = selectedStage,
                .Note = "Manuscript added to PaperRoute."
            }

            ChronologyProvenanceService.StampCreated(
                initialHistory
            )

            _createdManuscript.History.Add(
                initialHistory
            )

            If _proposal IsNot Nothing AndAlso _authorLibrary IsNot Nothing Then
                _authorLibraryChanged =
                    TitlePageApplyService.Apply(
                        _proposal,
                        _createdManuscript,
                        _authorLibrary
                    ).LibraryChanged
            End If

            ' An ordinary custom reminder, shown with every other reminder.
            If chkDeadline.Checked Then
                _createdManuscript.Reminders.Add(
                    New ManuscriptReminder With {
                        .Title =
                            If(
                                txtDeadline.Text.Trim().Length > 0,
                                txtDeadline.Text.Trim(),
                                "First deadline"
                            ),
                        .DueDate = dtpDeadline.Value.Date
                    }
                )
            End If

            Me.DialogResult = DialogResult.OK

        End Sub

    End Class

End Namespace
