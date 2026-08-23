Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class SubmissionDetailsForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _submission As JournalSubmission

        ' Editorial decisions
        Private ReadOnly lstDecisions As New ListBox()
        Private ReadOnly txtDecisionDetails As New TextBox()

        Private ReadOnly btnEditDecision As New Button()
        Private ReadOnly btnDeleteDecision As New Button()
        Private ReadOnly lblDecisionHelp As New Label()

        Private ReadOnly _displayedDecisions As New List(Of EditorialDecisionEvent)()

        ' Correspondence
        Private ReadOnly _managedLibrary As New ManagedLibraryService()
        Private ReadOnly lstCorrespondence As New ListBox()
        Private ReadOnly txtCorrespondenceDetails As New TextBox()

        Private ReadOnly btnOpenFile As New Button()
        Private ReadOnly btnOpenSource As New Button()
        Private ReadOnly btnEditCorrespondence As New Button()
        Private ReadOnly btnRemoveCorrespondence As New Button()
        Private ReadOnly lblCorrespondenceHelp As New Label()

        Private ReadOnly _displayedCorrespondence As New List(Of CorrespondenceItem)()


        Public Sub New(
            submission As JournalSubmission
        )

            Me.New(
                Nothing,
                submission
            )

        End Sub


        Public Sub New(
            manuscript As Manuscript,
            submission As JournalSubmission
        )

            _manuscript =
                manuscript

            _submission =
                submission

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            RefreshDecisionList()
            RefreshCorrespondenceList()

        End Sub


        ' =====================================================
        ' Interface
        ' =====================================================

        Private Sub BuildInterface()

            Me.Text = "Submission Details"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New Size(900, 840)
            Me.MinimumSize = New Size(780, 700)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(20)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 238))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 110))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))

            ' =================================================
            ' Submission summary
            ' =================================================

            Dim summaryGroup As New GroupBox With {
                .Text = "Submission",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14)
            }

            Dim summary As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 5
            }

            For summaryRowIndex As Integer = 0 To 4
                summary.RowStyles.Add(
                    New RowStyle(SizeType.Absolute, 44)
                )
            Next

            summary.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
            summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            summary.Controls.Add(CreateFieldLabel("Journal"), 0, 0)
            summary.Controls.Add(CreateValueLabel(_submission.JournalName), 1, 0)

            Dim manuscriptNumber As String = _submission.ManuscriptNumber

            If String.IsNullOrWhiteSpace(manuscriptNumber) Then
                manuscriptNumber = "Not recorded"
            End If

            summary.Controls.Add(CreateFieldLabel("Manuscript number"), 0, 1)
            summary.Controls.Add(CreateValueLabel(manuscriptNumber), 1, 1)

            summary.Controls.Add(CreateFieldLabel("Submitted"), 0, 2)
            summary.Controls.Add(
                CreateValueLabel(_submission.SubmittedDate.ToString("MMMM d, yyyy")),
                1,
                2
            )

            Dim followUpText As String =
                "Not set"

            If _submission.FollowUpDate.HasValue Then

                followUpText =
                    _submission.FollowUpDate.Value.ToString(
                        "MMMM d, yyyy"
                    )

            End If

            summary.Controls.Add(
                CreateFieldLabel("Follow-up"),
                0,
                3
            )

            summary.Controls.Add(
                CreateValueLabel(followUpText),
                1,
                3
            )

            summary.Controls.Add(CreateFieldLabel("Publisher portal"), 0, 4)

            Dim portalPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(0, 2, 0, 0)
            }

            Dim lblPortal As New Label With {
                .AutoSize = True,
                .Anchor = AnchorStyles.Left
            }

            Dim btnPortal As New Button With {
                .Text = "Open Publisher Portal",
                .AutoSize = True,
                .Height = 32
            }

            If String.IsNullOrWhiteSpace(_submission.PortalUrl) Then

                lblPortal.Text = "Not recorded"
                btnPortal.Visible = False

            Else

                lblPortal.Text = _submission.PortalUrl
                btnPortal.Visible = True

            End If

            AddHandler btnPortal.Click, AddressOf OpenPublisherPortal

            portalPanel.Controls.Add(btnPortal)
            portalPanel.Controls.Add(lblPortal)

            summary.Controls.Add(portalPanel, 1, 4)

            summaryGroup.Controls.Add(summary)

            ' =================================================
            ' Submission notes
            ' =================================================

            Dim notesGroup As New GroupBox With {
                .Text = "Submission Notes",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14)
            }

            Dim notesText As String = _submission.Notes

            If String.IsNullOrWhiteSpace(notesText) Then
                notesText = "No submission notes were recorded."
            End If

            Dim txtSubmissionNotes As New TextBox With {
                .Text = notesText,
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Vertical,
                .BackColor = SystemColors.Window
            }

            notesGroup.Controls.Add(txtSubmissionNotes)

            ' =================================================
            ' Tabs
            ' =================================================

            Dim tabs As New TabControl With {
                .Dock = DockStyle.Fill
            }

            Dim decisionsTab As New TabPage With {
                .Text = "Editorial History"
            }

            Dim correspondenceTab As New TabPage With {
                .Text = "Correspondence && Files"
            }

            decisionsTab.Controls.Add(BuildDecisionPanel())
            correspondenceTab.Controls.Add(BuildCorrespondencePanel())

            tabs.TabPages.Add(decisionsTab)
            tabs.TabPages.Add(correspondenceTab)

            ' =================================================
            ' Footer
            ' =================================================

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 8, 0, 0)
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            buttons.Controls.Add(btnClose)

            root.Controls.Add(summaryGroup, 0, 0)
            root.Controls.Add(notesGroup, 0, 1)
            root.Controls.Add(tabs, 0, 2)
            root.Controls.Add(buttons, 0, 3)

            Me.AcceptButton = btnClose

            Me.Controls.Add(root)

        End Sub


        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function


        Private Function CreateValueLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left
            }

        End Function


        ' =====================================================
        ' Publisher portal
        ' =====================================================

        Private Sub OpenPublisherPortal(sender As Object, e As EventArgs)

            If String.IsNullOrWhiteSpace(_submission.PortalUrl) Then
                Return
            End If

            OpenShellTarget(
                _submission.PortalUrl,
                "The publisher portal could not be opened."
            )

        End Sub


        ' =====================================================
        ' Decision UI
        ' =====================================================

        Private Function BuildDecisionPanel() As Control

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(10)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim toolbar As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1
            }

            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            lblDecisionHelp.AutoSize = True
            lblDecisionHelp.Anchor = AnchorStyles.Left
            lblDecisionHelp.ForeColor = SystemColors.GrayText

            Dim decisionButtons As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(0, 4, 0, 4),
                .Margin = New Padding(0)
            }

            btnEditDecision.Text = "Edit Decision"
            btnEditDecision.AutoSize = True
            btnEditDecision.Height = 34
            btnEditDecision.Visible = False

            btnDeleteDecision.Text = "Delete Decision"
            btnDeleteDecision.AutoSize = True
            btnDeleteDecision.Height = 34
            btnDeleteDecision.Visible = False

            Dim btnAddDecision As New Button With {
                .Text = "+ Add Decision",
                .AutoSize = True,
                .Height = 34
            }

            AddHandler btnEditDecision.Click, AddressOf EditSelectedDecision
            AddHandler btnDeleteDecision.Click, AddressOf DeleteSelectedDecision
            AddHandler btnAddDecision.Click, AddressOf AddDecision

            decisionButtons.Controls.Add(btnEditDecision)
            decisionButtons.Controls.Add(btnDeleteDecision)
            decisionButtons.Controls.Add(btnAddDecision)

            toolbar.Controls.Add(lblDecisionHelp, 0, 0)
            toolbar.Controls.Add(decisionButtons, 1, 0)

            lstDecisions.Dock = DockStyle.Fill
            lstDecisions.IntegralHeight = False
            lstDecisions.MinimumSize = New Size(0, 110)

            AddHandler lstDecisions.SelectedIndexChanged, AddressOf DecisionSelectionChanged
            AddHandler lstDecisions.DoubleClick, AddressOf EditSelectedDecision

            txtDecisionDetails.Dock = DockStyle.Fill
            txtDecisionDetails.Multiline = True
            txtDecisionDetails.ReadOnly = True
            txtDecisionDetails.ScrollBars = ScrollBars.Vertical
            txtDecisionDetails.BackColor = SystemColors.Window
            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical,
                .SplitterWidth = 6
            }

            AddHandler split.SizeChanged,
                Sub(sender, e)

                    Const minimumLeft As Integer = 220
                    Const minimumRight As Integer = 280

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
                                availableWidth * 0.43
                            )
                        )

                    split.SplitterDistance =
                        Math.Min(
                            availableWidth - minimumRight,
                            Math.Max(
                                minimumLeft,
                                desired
                            )
                        )

                End Sub

            split.Panel1.Controls.Add(
                lstDecisions
            )

            split.Panel2.Controls.Add(
                txtDecisionDetails
            )

            root.Controls.Add(toolbar, 0, 0)
            root.Controls.Add(split, 0, 1)

            Return root

        End Function


        Private Sub RefreshDecisionList()

            lstDecisions.Items.Clear()
            _displayedDecisions.Clear()

            For Each decisionEvent As EditorialDecisionEvent In _submission.Decisions

                _displayedDecisions.Add(decisionEvent)
                lstDecisions.Items.Add(FormatDecisionRow(decisionEvent))

            Next

            If _displayedDecisions.Count = 0 Then

                lblDecisionHelp.Text = "No editorial decisions recorded yet."
                txtDecisionDetails.Text = "No editorial decisions recorded."

            Else

                lblDecisionHelp.Text = "Select a decision to view, edit, or delete it."
                txtDecisionDetails.Text = "Select a decision to view its details."

            End If

            UpdateDecisionButtons()

        End Sub


        Private Function FormatDecisionRow(decisionEvent As EditorialDecisionEvent) As String

            Return decisionEvent.DecisionDate.ToString("MMM d, yyyy") & " - " & FormatDecision(decisionEvent.Decision)

        End Function


        Private Function GetSelectedDecision() As EditorialDecisionEvent

            Dim selectedIndex As Integer = lstDecisions.SelectedIndex

            If selectedIndex < 0 OrElse selectedIndex >= _displayedDecisions.Count Then
                Return Nothing
            End If

            Return _displayedDecisions(selectedIndex)

        End Function


        Private Sub UpdateDecisionButtons()

            Dim hasSelection As Boolean = GetSelectedDecision() IsNot Nothing

            btnEditDecision.Visible = hasSelection
            btnDeleteDecision.Visible = hasSelection

        End Sub


        Private Sub DecisionSelectionChanged(sender As Object, e As EventArgs)

            UpdateDecisionButtons()

            Dim decisionEvent As EditorialDecisionEvent = GetSelectedDecision()

            If decisionEvent Is Nothing Then

                If _displayedDecisions.Count = 0 Then
                    txtDecisionDetails.Text = "No editorial decisions recorded."
                Else
                    txtDecisionDetails.Text = "Select a decision to view its details."
                End If

                Return

            End If

            Dim details As String =
                "Decision: " &
                FormatDecision(decisionEvent.Decision) &
                Environment.NewLine &
                "Date: " &
                decisionEvent.DecisionDate.ToString("MMMM d, yyyy")

            If decisionEvent.RevisionDeadline.HasValue Then

                details &=
                    Environment.NewLine &
                    "Revision deadline: " &
                    decisionEvent.RevisionDeadline.Value.ToString("MMMM d, yyyy")

            End If

            details &=
                Environment.NewLine &
                "Workflow effect: " &
                DescribeDecisionWorkflowEffect(
                    decisionEvent.Decision
                )

            details &=
                Environment.NewLine &
                Environment.NewLine &
                "Notes:" &
                Environment.NewLine

            If String.IsNullOrWhiteSpace(decisionEvent.Notes) Then
                details &= "No decision notes were recorded."
            Else
                details &= decisionEvent.Notes
            End If

            txtDecisionDetails.Text = details

        End Sub


        Private Sub AddDecision(sender As Object, e As EventArgs)

            Using dialog As New AddDecisionForm()

                If dialog.ShowDialog(Me) = DialogResult.OK AndAlso dialog.CreatedDecision IsNot Nothing Then

                    _submission.Decisions.Add(dialog.CreatedDecision)

                    If _manuscript IsNot Nothing Then

                        ManuscriptLifecycleService.ApplyDecision(
                            _manuscript,
                            _submission,
                            dialog.CreatedDecision
                        )

                    End If

                    RefreshDecisionList()

                    lstDecisions.SelectedIndex = lstDecisions.Items.Count - 1

                End If

            End Using

        End Sub


        Private Sub EditSelectedDecision(sender As Object, e As EventArgs)

            Dim selected As EditorialDecisionEvent = GetSelectedDecision()

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New AddDecisionForm(selected)

                If dialog.ShowDialog(Me) <> DialogResult.OK OrElse dialog.CreatedDecision Is Nothing Then
                    Return
                End If

                Dim updated As EditorialDecisionEvent = dialog.CreatedDecision

                For i As Integer = 0 To _submission.Decisions.Count - 1

                    If _submission.Decisions(i).Id = selected.Id Then
                        _submission.Decisions(i) = updated
                        Exit For
                    End If

                Next

                If _manuscript IsNot Nothing Then

                    If Not ManuscriptLifecycleService.ApplyDecision(
                        _manuscript,
                        _submission,
                        updated
                    ) Then

                        ManuscriptLifecycleService.
                            ReconcileAfterWorkflowMutation(
                                _manuscript
                            )

                    End If

                End If

                RefreshDecisionList()

                For i As Integer = 0 To _displayedDecisions.Count - 1

                    If _displayedDecisions(i).Id = updated.Id Then
                        lstDecisions.SelectedIndex = i
                        Exit For
                    End If

                Next

            End Using

        End Sub


        Private Sub DeleteSelectedDecision(sender As Object, e As EventArgs)

            Dim selected As EditorialDecisionEvent = GetSelectedDecision()

            If selected Is Nothing Then
                Return
            End If

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    "Delete this editorial decision?" &
                    Environment.NewLine &
                    Environment.NewLine &
                    FormatDecision(selected.Decision) &
                    " - " &
                    selected.DecisionDate.ToString("MMM d, yyyy") &
                    Environment.NewLine &
                    Environment.NewLine &
                    "This may change the manuscript's rejection count.",
                    "Delete Editorial Decision",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <> DialogResult.Yes Then
                Return
            End If

            _submission.Decisions.Remove(selected)

            If _manuscript IsNot Nothing Then

                ManuscriptLifecycleService.
                    ReconcileAfterDecisionRemoval(
                        _manuscript,
                        selected
                    )

            End If

            RefreshDecisionList()

        End Sub


        ' =====================================================
        ' Correspondence UI
        ' =====================================================

        Private Function BuildCorrespondencePanel() As Control

            Dim root As New TableLayoutPanel With {
        .Dock = DockStyle.Fill,
        .ColumnCount = 1,
        .RowCount = 3,
        .Padding = New Padding(10)
    }

            root.RowStyles.Add(
        New RowStyle(SizeType.Absolute, 48)
    )

            root.RowStyles.Add(
        New RowStyle(SizeType.Percent, 50)
    )

            root.RowStyles.Add(
        New RowStyle(SizeType.Percent, 50)
    )

            Dim toolbar As New TableLayoutPanel With {
        .Dock = DockStyle.Fill,
        .ColumnCount = 2,
        .RowCount = 1
    }

            toolbar.ColumnStyles.Add(
        New ColumnStyle(SizeType.Percent, 100)
    )

            toolbar.ColumnStyles.Add(
        New ColumnStyle(SizeType.AutoSize)
    )

            lblCorrespondenceHelp.AutoSize = True
            lblCorrespondenceHelp.Anchor = AnchorStyles.Left
            lblCorrespondenceHelp.ForeColor = SystemColors.GrayText

            Dim itemButtons As New FlowLayoutPanel With {
        .AutoSize = True,
        .FlowDirection = FlowDirection.LeftToRight,
        .WrapContents = False
    }

            btnOpenFile.Text = "Open File"
            btnOpenFile.AutoSize = True
            btnOpenFile.Height = 34
            btnOpenFile.Visible = False

            btnOpenSource.Text = "Open Source"
            btnOpenSource.AutoSize = True
            btnOpenSource.Height = 34
            btnOpenSource.Visible = False

            btnEditCorrespondence.Text = "Edit"
            btnEditCorrespondence.AutoSize = True
            btnEditCorrespondence.Height = 34
            btnEditCorrespondence.Visible = False

            btnRemoveCorrespondence.Text = "Remove"
            btnRemoveCorrespondence.AutoSize = True
            btnRemoveCorrespondence.Height = 34
            btnRemoveCorrespondence.Visible = False

            Dim btnLinkFiles As New Button With {
        .Text = "Link Files...",
        .AutoSize = True,
        .Height = 34
    }

            Dim btnCopyFiles As New Button With {
        .Text = "Copy to Library...",
        .AutoSize = True,
        .Height = 34
    }

            Dim btnAddCorrespondence As New Button With {
        .Text = "+ Add Item",
        .AutoSize = True,
        .Height = 34
    }

            AddHandler btnOpenFile.Click, AddressOf OpenSelectedFile
            AddHandler btnOpenSource.Click, AddressOf OpenSelectedSource
            AddHandler btnEditCorrespondence.Click, AddressOf EditSelectedCorrespondence
            AddHandler btnRemoveCorrespondence.Click, AddressOf RemoveSelectedCorrespondence
            AddHandler btnLinkFiles.Click, AddressOf LinkFiles
            AddHandler btnCopyFiles.Click, AddressOf CopyFilesToLibrary
            AddHandler btnAddCorrespondence.Click, AddressOf AddCorrespondence

            itemButtons.Controls.Add(btnOpenFile)
            itemButtons.Controls.Add(btnOpenSource)
            itemButtons.Controls.Add(btnEditCorrespondence)
            itemButtons.Controls.Add(btnRemoveCorrespondence)
            itemButtons.Controls.Add(btnLinkFiles)
            itemButtons.Controls.Add(btnCopyFiles)
            itemButtons.Controls.Add(btnAddCorrespondence)

            toolbar.Controls.Add(
        lblCorrespondenceHelp,
        0,
        0
    )

            toolbar.Controls.Add(
        itemButtons,
        1,
        0
    )

            lstCorrespondence.Dock = DockStyle.Fill
            lstCorrespondence.IntegralHeight = False
            lstCorrespondence.AllowDrop = True

            AddHandler lstCorrespondence.SelectedIndexChanged, AddressOf CorrespondenceSelectionChanged
            AddHandler lstCorrespondence.DoubleClick, AddressOf OpenSelectedFile
            AddHandler lstCorrespondence.DragEnter, AddressOf CorrespondenceDragEnter
            AddHandler lstCorrespondence.DragDrop, AddressOf CorrespondenceDragDrop

            txtCorrespondenceDetails.Dock = DockStyle.Fill
            txtCorrespondenceDetails.Multiline = True
            txtCorrespondenceDetails.ReadOnly = True
            txtCorrespondenceDetails.ScrollBars = ScrollBars.Vertical
            txtCorrespondenceDetails.BackColor = SystemColors.Window

            root.Controls.Add(
        toolbar,
        0,
        0
    )

            root.Controls.Add(
        lstCorrespondence,
        0,
        1
    )

            root.Controls.Add(
        txtCorrespondenceDetails,
        0,
        2
    )

            Return root

        End Function


        Private Sub RefreshCorrespondenceList()

            lstCorrespondence.Items.Clear()
            _displayedCorrespondence.Clear()

            For Each item As CorrespondenceItem In _submission.Correspondence

                _displayedCorrespondence.Add(item)
                lstCorrespondence.Items.Add(FormatCorrespondenceRow(item))

            Next

            If _displayedCorrespondence.Count = 0 Then

                lblCorrespondenceHelp.Text =
                    "Drag files here, link files, or add a detailed item."

                txtCorrespondenceDetails.Text =
                    "Dropped and quick-linked files stay in their original location."

            Else

                lblCorrespondenceHelp.Text =
                    "Select an item to open, edit, or remove it."

                txtCorrespondenceDetails.Text =
                    "Select an item to view its details."

            End If

            UpdateCorrespondenceButtons()

        End Sub


        Private Function FormatCorrespondenceRow(
    item As CorrespondenceItem
) As String

            Dim result As String =
        item.ItemDate.ToString("MMM d, yyyy") &
        " - " &
        FormatCorrespondenceType(item.Type) &
        " - " &
        item.Title

            If item.IsManagedCopy Then

                If _managedLibrary.IsManagedPath(
            item.LocalFilePath
        ) Then

                    result &=
                " - LIBRARY"

                Else

                    result &=
                " - LIBRARY COPY PENDING"

                End If

            ElseIf Not String.IsNullOrWhiteSpace(
        item.LocalFilePath
    ) Then

                result &=
            " - LINKED"

            End If

            If Not String.IsNullOrWhiteSpace(
        item.LocalFilePath
    ) AndAlso
       Not File.Exists(
           item.LocalFilePath
       ) Then

                result &=
            " - FILE MISSING"

            End If

            Return result

        End Function


        Private Function GetSelectedCorrespondence() As CorrespondenceItem

            Dim selectedIndex As Integer = lstCorrespondence.SelectedIndex

            If selectedIndex < 0 OrElse selectedIndex >= _displayedCorrespondence.Count Then
                Return Nothing
            End If

            Return _displayedCorrespondence(selectedIndex)

        End Function


        Private Sub UpdateCorrespondenceButtons()

            Dim selected As CorrespondenceItem = GetSelectedCorrespondence()

            Dim hasSelection As Boolean = selected IsNot Nothing

            btnEditCorrespondence.Visible = hasSelection
            btnRemoveCorrespondence.Visible = hasSelection

            If Not hasSelection Then

                btnOpenFile.Visible = False
                btnOpenSource.Visible = False

                Return

            End If

            btnOpenFile.Visible =
                Not String.IsNullOrWhiteSpace(selected.LocalFilePath)

            btnOpenSource.Visible =
                Not String.IsNullOrWhiteSpace(selected.SourceUrl)

        End Sub


        Private Sub CorrespondenceSelectionChanged(sender As Object, e As EventArgs)

            UpdateCorrespondenceButtons()

            Dim item As CorrespondenceItem = GetSelectedCorrespondence()

            If item Is Nothing Then

                If _displayedCorrespondence.Count = 0 Then
                    txtCorrespondenceDetails.Text =
                        "Dropped and quick-linked files stay in their original location."
                Else
                    txtCorrespondenceDetails.Text =
                        "Select an item to view its details."
                End If

                Return

            End If

            Dim details As String =
                "Type: " &
                FormatCorrespondenceType(item.Type) &
                Environment.NewLine &
                "Date: " &
                item.ItemDate.ToString("MMMM d, yyyy") &
                Environment.NewLine &
                "Title: " &
                item.Title

            If item.IsManagedCopy Then

                If _managedLibrary.IsManagedPath(
        item.LocalFilePath
    ) Then

                    details &=
            Environment.NewLine &
            "Storage: PaperRoute Library"

                Else

                    details &=
            Environment.NewLine &
            "Storage: Will copy to PaperRoute Library when saved"

                End If

            ElseIf Not String.IsNullOrWhiteSpace(
    item.LocalFilePath
) Then

                details &=
        Environment.NewLine &
        "Storage: Linked to original file"

            End If

            If Not String.IsNullOrWhiteSpace(item.LocalFilePath) Then

                details &=
                    Environment.NewLine &
                    "File: " &
                    item.LocalFilePath

                If Not File.Exists(item.LocalFilePath) Then
                    details &= "  [FILE NOT FOUND]"
                End If

            End If

            If Not String.IsNullOrWhiteSpace(item.SourceUrl) Then

                details &=
                    Environment.NewLine &
                    "Source: " &
                    item.SourceUrl

            End If

            details &=
                Environment.NewLine &
                Environment.NewLine &
                "Notes:" &
                Environment.NewLine

            If String.IsNullOrWhiteSpace(item.Notes) Then
                details &= "No notes were recorded."
            Else
                details &= item.Notes
            End If

            txtCorrespondenceDetails.Text = details

        End Sub


        ' =====================================================
        ' Quick file linking
        ' =====================================================

        Private Sub LinkFiles(
    sender As Object,
    e As EventArgs
)

            Using dialog As New OpenFileDialog()

                dialog.Title =
            "Link files to this submission"

                dialog.Filter =
            "All files|*.*"

                dialog.Multiselect =
            True

                dialog.CheckFileExists =
            True

                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If

                AddFiles(
            dialog.FileNames,
            False
        )

            End Using

        End Sub

        Private Sub CopyFilesToLibrary(
    sender As Object,
    e As EventArgs
)

            Using dialog As New OpenFileDialog()

                dialog.Title =
            "Copy files into the PaperRoute Library"

                dialog.Filter =
            "All files|*.*"

                dialog.Multiselect =
            True

                dialog.CheckFileExists =
            True

                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If

                AddFiles(
            dialog.FileNames,
            True
        )

            End Using

        End Sub

        Private Sub CorrespondenceDragEnter(sender As Object, e As DragEventArgs)

            If e.Data Is Nothing Then

                e.Effect = DragDropEffects.None
                Return

            End If

            If e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
            Else
                e.Effect = DragDropEffects.None
            End If

        End Sub


        Private Sub CorrespondenceDragDrop(
    sender As Object,
    e As DragEventArgs
)

            If e.Data Is Nothing Then
                Return
            End If

            If Not e.Data.GetDataPresent(
        DataFormats.FileDrop
    ) Then

                Return

            End If

            Dim droppedObject As Object =
        e.Data.GetData(
            DataFormats.FileDrop
        )

            Dim paths As String() =
        TryCast(
            droppedObject,
            String()
        )

            If paths Is Nothing Then
                Return
            End If

            Dim choice As DialogResult =
        MessageBox.Show(
            Me,
            "How should PaperRoute handle the dropped file(s)?" &
            Environment.NewLine &
            Environment.NewLine &
            "YES  — Copy into the PaperRoute Library when you Save & Close" &
            Environment.NewLine &
            "NO   — Link to the files in their current location" &
            Environment.NewLine &
            "CANCEL — Do nothing",
            "Add Files",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question
        )

            If choice = DialogResult.Cancel Then
                Return
            End If

            Dim managedCopy As Boolean =
        choice = DialogResult.Yes

            AddFiles(
        paths,
        managedCopy
    )

        End Sub

        Private Sub AddFiles(
    paths As IEnumerable(Of String),
    managedCopy As Boolean
)

            Dim addedCount As Integer = 0
            Dim skippedCount As Integer = 0

            For Each filePath As String In paths

                If Not File.Exists(filePath) Then

                    skippedCount += 1
                    Continue For

                End If

                If IsFileAlreadyLinked(filePath) Then

                    skippedCount += 1
                    Continue For

                End If

                Dim item As New CorrespondenceItem With {
            .Id = Guid.NewGuid(),
            .ItemDate = DateTime.Today,
            .Type = CorrespondenceType.Other,
            .Title = Path.GetFileName(filePath),
            .Notes = String.Empty,
            .LocalFilePath = filePath,
            .SourceUrl = String.Empty,
            .IsManagedCopy = managedCopy
        }

                ChronologyProvenanceService.StampCreated(
                    item
                )

                _submission.Correspondence.Add(item)

                addedCount += 1

            Next

            RefreshCorrespondenceList()

            If addedCount > 0 Then
                lstCorrespondence.SelectedIndex = lstCorrespondence.Items.Count - 1
            End If

            If addedCount = 0 AndAlso skippedCount > 0 Then

                MessageBox.Show(
            Me,
            "No new files were added. The selected files were either unavailable or already attached.",
            "No Files Added",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        )

            ElseIf skippedCount > 0 Then

                MessageBox.Show(
            Me,
            addedCount.ToString() &
            " file(s) added. " &
            skippedCount.ToString() &
            " file(s) were skipped.",
            "Files Added",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        )

            End If

        End Sub

        Private Function IsFileAlreadyLinked(filePath As String) As Boolean

            For Each item As CorrespondenceItem In _submission.Correspondence

                If String.Equals(
                    item.LocalFilePath,
                    filePath,
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return True

                End If

            Next

            Return False

        End Function


        ' =====================================================
        ' Correspondence CRUD
        ' =====================================================

        Private Sub AddCorrespondence(sender As Object, e As EventArgs)

            Using dialog As New AddCorrespondenceForm()

                If dialog.ShowDialog(Me) =
                    DialogResult.OK AndAlso
                   dialog.CreatedItem IsNot Nothing Then

                    _submission.Correspondence.Add(dialog.CreatedItem)

                    RefreshCorrespondenceList()

                    lstCorrespondence.SelectedIndex =
                        lstCorrespondence.Items.Count - 1

                End If

            End Using

        End Sub


        Private Sub EditSelectedCorrespondence(sender As Object, e As EventArgs)

            Dim selected As CorrespondenceItem =
                GetSelectedCorrespondence()

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New AddCorrespondenceForm(selected)

                If dialog.ShowDialog(Me) <>
                    DialogResult.OK OrElse
                   dialog.CreatedItem Is Nothing Then

                    Return

                End If

                Dim updated As CorrespondenceItem =
                    dialog.CreatedItem

                For i As Integer = 0 To _submission.Correspondence.Count - 1

                    If _submission.Correspondence(i).Id = selected.Id Then
                        _submission.Correspondence(i) = updated
                        Exit For
                    End If

                Next

                RefreshCorrespondenceList()

                For i As Integer = 0 To _displayedCorrespondence.Count - 1

                    If _displayedCorrespondence(i).Id = updated.Id Then
                        lstCorrespondence.SelectedIndex = i
                        Exit For
                    End If

                Next

            End Using

        End Sub


        Private Sub RemoveSelectedCorrespondence(sender As Object, e As EventArgs)

            Dim selected As CorrespondenceItem =
                GetSelectedCorrespondence()

            If selected Is Nothing Then
                Return
            End If

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    "Remove '" &
                    selected.Title &
                    "' from this submission?" &
                    Environment.NewLine &
                    Environment.NewLine &
                    "The correspondence record will be removed. The original file will not be deleted.",
                    "Remove Correspondence",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <> DialogResult.Yes Then
                Return
            End If

            _submission.Correspondence.Remove(selected)

            RefreshCorrespondenceList()

        End Sub


        Private Sub OpenSelectedFile(sender As Object, e As EventArgs)

            Dim selected As CorrespondenceItem =
                GetSelectedCorrespondence()

            If selected Is Nothing Then
                Return
            End If

            If String.IsNullOrWhiteSpace(selected.LocalFilePath) Then
                Return
            End If

            If Not File.Exists(selected.LocalFilePath) Then

                MessageBox.Show(
                    Me,
                    "The linked file could not be found." &
                    Environment.NewLine &
                    Environment.NewLine &
                    selected.LocalFilePath &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Use Edit to point this record to the file's new location.",
                    "File Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            OpenShellTarget(
                selected.LocalFilePath,
                "The file could not be opened."
            )

        End Sub


        Private Sub OpenSelectedSource(sender As Object, e As EventArgs)

            Dim selected As CorrespondenceItem =
                GetSelectedCorrespondence()

            If selected Is Nothing Then
                Return
            End If

            If String.IsNullOrWhiteSpace(selected.SourceUrl) Then
                Return
            End If

            OpenShellTarget(
                selected.SourceUrl,
                "The source URL could not be opened."
            )

        End Sub


        ' =====================================================
        ' Shell helper
        ' =====================================================

        Private Sub OpenShellTarget(
            target As String,
            errorMessage As String
        )

            Try

                Dim startInfo As New ProcessStartInfo With {
                    .FileName = target,
                    .UseShellExecute = True
                }

                Process.Start(startInfo)

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    errorMessage &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Open Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        ' =====================================================
        ' Formatting
        ' =====================================================

        Private Function DescribeDecisionWorkflowEffect(
            decision As EditorialDecision
        ) As String

            Select Case decision

                Case EditorialDecision.MajorRevision,
                     EditorialDecision.MinorRevision,
                     EditorialDecision.ReviseAndResubmit

                    Return "If this is the latest workflow event, the manuscript is in Revision."

                Case EditorialDecision.Accepted

                    Return "If this is the latest workflow event, the manuscript is Accepted."

                Case EditorialDecision.Rejected,
                     EditorialDecision.DeskRejected,
                     EditorialDecision.RejectedAfterReview

                    Return "This closes the submission and returns the manuscript to Draft for rerouting unless a later workflow event exists."

                Case EditorialDecision.Withdrawn

                    Return "Withdrawn closes this submission and returns the manuscript to Draft for rerouting unless a later workflow event exists."

                Case Else

                    Return "No lifecycle change is associated with this decision."

            End Select

        End Function


        Private Function FormatDecision(
            decision As EditorialDecision
        ) As String

            Select Case decision

                Case EditorialDecision.Rejected
                    Return "Rejected"

                Case EditorialDecision.DeskRejected
                    Return "Desk Rejected"

                Case EditorialDecision.RejectedAfterReview
                    Return "Rejected After Review"

                Case EditorialDecision.MajorRevision
                    Return "Major Revision"

                Case EditorialDecision.MinorRevision
                    Return "Minor Revision"

                Case EditorialDecision.ReviseAndResubmit
                    Return "Revise and Resubmit"

                Case EditorialDecision.Accepted
                    Return "Accepted"

                Case EditorialDecision.Withdrawn
                    Return "Withdrawn"

                Case Else
                    Return "None"

            End Select

        End Function


        Private Function FormatCorrespondenceType(
            itemType As CorrespondenceType
        ) As String

            Select Case itemType

                Case CorrespondenceType.DecisionLetter
                    Return "Decision Letter"

                Case CorrespondenceType.ReviewerComments
                    Return "Reviewer Comments"

                Case CorrespondenceType.EditorEmail
                    Return "Editor Email"

                Case CorrespondenceType.CoverLetter
                    Return "Cover Letter"

                Case CorrespondenceType.ResponseToReviewers
                    Return "Response to Reviewers"

                Case CorrespondenceType.RevisedManuscript
                    Return "Revised Manuscript"

                Case CorrespondenceType.AcceptanceLetter
                    Return "Acceptance Letter"

                Case CorrespondenceType.PortalSnapshot
                    Return "Portal Snapshot"

                Case Else
                    Return "Other"

            End Select

        End Function

    End Class

End Namespace