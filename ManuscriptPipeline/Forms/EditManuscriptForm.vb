Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class EditManuscriptForm
        Inherits Form

        Private ReadOnly _originalManuscript As Manuscript
        Private ReadOnly _workingManuscript As Manuscript
        Private ReadOnly _allManuscripts As List(Of Manuscript)

        Private ReadOnly _authorRepository As AuthorLibraryRepository
        Private _authorLibrary As AuthorLibraryData

        Private _deleteRequested As Boolean = False

        ' In page mode the form is hosted inside the main window: there is no
        ' Save & Close or Cancel footer, and the page's Save / Discard bar
        ' commits or drops this working copy.
        Private ReadOnly _pageMode As Boolean

        ' The working copy as last loaded or saved; unsaved changes are any
        ' difference from it.
        Private _baseline As String = String.Empty

        Private ReadOnly _sectionTabs As New List(Of ShelfTabButton)()
        Private ReadOnly lblReadinessSummary As New Label()
        Private ReadOnly journalNotesGroup As New SectionCard()
        Private ReadOnly lblJournalNotes As New Label()

        ' Raised in page mode after the user confirms Delete Manuscript.
        Friend Event DeleteConfirmed As EventHandler

        ' A line for the main window's status bar, such as what Find Journals
        ' added (#88).
        Friend Event StatusMessage As EventHandler(Of String)

        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtTargetJournal As New TextBox()
        Private ReadOnly cmbStage As New ComboBox()
        Private ReadOnly btnMetadata As New Button()
        Private ReadOnly btnJournalLinks As New Button()
        Private ReadOnly lblRevisionDeadlineValue As New Label()
        Private ReadOnly btnRevisionDeadline As New Button()
        Private _authorLibraryDirty As Boolean = False

        Private ReadOnly fileDrawerGroup As New SectionCard()

        ' The kind of work and its tags (#64), edited in the working copy.
        Private ReadOnly classificationGroup As New SectionCard()
        Private ReadOnly shortlistGroup As New SectionCard()
        Private ReadOnly shortlistRows As New TableLayoutPanel()
        Private ReadOnly shortlistOffer As New TableLayoutPanel()
        Private ReadOnly lblShortlistOffer As New Label()
        Private ReadOnly btnShortlistOffer As New Button()
        Private ReadOnly lblShortlistEmpty As New Label()

        ' Adds or edits a shortlisted journal; returns the edited values, or
        ' Nothing when cancelled. Tests replace the dialog.
        Friend candidatePrompt As Func(Of JournalCandidate, JournalCandidate) = Nothing

        ' Runs Find Journals and returns the journals chosen with the search,
        ' or Nothing; tests replace it (#88).
        Friend journalSuggestionPrompt As Func(Of (Chosen As List(Of JournalSuggestion), Result As JournalSuggestionsResult)) = Nothing
        Private ReadOnly shortlistToolTip As New ToolTip()
        Private ReadOnly cmbWorkType As New ComboBox()
        Private ReadOnly tagEditor As New TagEditor()

        Private ReadOnly lblFileDrawerDateValue As New Label()
        Private ReadOnly txtFileDrawerReason As New TextBox()

        Private ReadOnly lstAuthors As New ListBox()
        Private ReadOnly btnEditAuthor As New Button()
        Private ReadOnly btnRemoveAuthor As New Button()
        Private ReadOnly btnMoveAuthorUp As New Button()
        Private ReadOnly btnMoveAuthorDown As New Button()
        Private ReadOnly lblAuthorInfo As New Label()

        Private ReadOnly lstSubmissions As New ListBox()

        Private ReadOnly btnEditSubmission As New Button()
        Private ReadOnly btnDeleteSubmission As New Button()

        Private ReadOnly lblSubmissionInfo As New Label()

        ' The Submissions tab: the list beside the selected submission's
        ' details, decisions, reviewer responses, and correspondence.
        Private _submissionsSection As Control = Nothing
        Private ReadOnly _submissionDetailHost As New Panel()
        Private _submissionDetail As SubmissionDetailsForm = Nothing

        Private versionHistoryControl As ManuscriptVersionHistoryControl = Nothing

        Private _pendingRouteWaypoint As ManuscriptRouteWaypoint = Nothing

        Private ReadOnly _displayedSubmissions As New List(Of JournalSubmission)()


        Public ReadOnly Property DeleteRequested As Boolean
            Get
                Return _deleteRequested
            End Get
        End Property


        Public Sub New(
            manuscript As Manuscript,
            Optional allManuscripts As IEnumerable(Of Manuscript) = Nothing
        )

            Me.New(
                manuscript,
                allManuscripts,
                New AuthorLibraryRepository(),
                pageMode:=False
            )

        End Sub


        Friend Sub New(
            manuscript As Manuscript,
            allManuscripts As IEnumerable(Of Manuscript),
            authorRepository As AuthorLibraryRepository,
            pageMode As Boolean
        )

            _pageMode =
                pageMode

            _authorRepository =
                If(authorRepository, New AuthorLibraryRepository())

            _originalManuscript =
                manuscript

            _workingManuscript =
                CloneManuscript(manuscript)

            _allManuscripts =
                If(
                    allManuscripts Is Nothing,
                    New List(Of Manuscript) From {
                        manuscript
                    },
                    allManuscripts.ToList()
                )

            _authorLibrary =
                _authorRepository.Load()

            BuildInterface()
            EmptyHint.Attach(lstAuthors, "No authors yet. Add Author picks people from your reusable library; Manage Library creates new ones.")
            EmptyHint.Attach(lstSubmissions, "No submissions yet.")
            UiPolish.ApplyDialog(Me)
            LoadManuscript()
            RefreshReadinessSummary()
            RefreshJournalNotes()

            _baseline =
                Snapshot()

        End Sub


        ' =====================================================
        ' Page mode
        ' =====================================================

        ' True when the working copy or a field not yet applied to it differs
        ' from what was loaded or last saved.
        Friend Function HasUnsavedChanges() As Boolean

            If _authorLibraryDirty Then
                Return True
            End If

            If Not String.Equals(txtTitle.Text.Trim(), If(_workingManuscript.Title, String.Empty).Trim(), StringComparison.Ordinal) OrElse
               Not String.Equals(txtTargetJournal.Text.Trim(), If(_workingManuscript.TargetJournal, String.Empty).Trim(), StringComparison.Ordinal) Then
                Return True
            End If

            If cmbStage.SelectedItem IsNot Nothing AndAlso
               CType(cmbStage.SelectedItem, PaperStage) <> _workingManuscript.CurrentStage Then
                Return True
            End If

            If fileDrawerGroup.Visible AndAlso
               Not String.Equals(txtFileDrawerReason.Text.Trim(), If(_workingManuscript.FileDrawerReason, String.Empty).Trim(), StringComparison.Ordinal) Then
                Return True
            End If

            Return Not String.Equals(Snapshot(), _baseline, StringComparison.Ordinal)

        End Function


        Private Sub RefreshJournalNotes()

            Dim journal As JournalRecord = Nothing

            If _workingManuscript.TargetJournalId.HasValue AndAlso
               _authorLibrary IsNot Nothing AndAlso
               _authorLibrary.Journals IsNot Nothing Then

                journal =
                    _authorLibrary.Journals.FirstOrDefault(
                        Function(item) item IsNot Nothing AndAlso item.Id = _workingManuscript.TargetJournalId.Value)

            End If

            Dim notes As String = If(journal?.Notes, String.Empty).Trim()
            Dim checklist As Integer =
                If(journal?.ReadinessChecklistTemplate, New List(Of JournalChecklistTemplateItem)()).
                    Where(Function(item) item IsNot Nothing).Count()

            If journal Is Nothing OrElse (notes.Length = 0 AndAlso checklist = 0) Then
                journalNotesGroup.Visible = False
                Return
            End If

            Dim lines As New List(Of String)

            If notes.Length > 0 Then
                lines.Add(If(notes.Length > 700, notes.Substring(0, 700).TrimEnd() & ChrW(&H2026), notes))
            End If

            If checklist > 0 Then
                lines.Add(
                    checklist.ToString() &
                    If(checklist = 1, " checklist item", " checklist items") &
                    " in the Journal Library; open Submission Readiness on the Readiness & Packets tab to work through them.")
            End If

            journalNotesGroup.Text = "Notes for " & journal.Name.Replace("&", "&&")
            lblJournalNotes.Text = String.Join(Environment.NewLine & Environment.NewLine, lines)
            journalNotesGroup.Visible = True

        End Sub


        Private Function Snapshot() As String
            Return System.Text.Json.JsonSerializer.Serialize(_workingManuscript)
        End Function


        ' Opens the section a Route waypoint refers to, for a hosted page.
        Friend Sub ShowRouteWaypoint(
            waypoint As ManuscriptRouteWaypoint
        )

            _pendingRouteWaypoint =
                waypoint

            ApplyPendingRouteNavigation()

        End Sub


        ' Opens the Submissions tab on one submission, for a deadline opened
        ' from the Deadlines page; a revision with comments lands on its
        ' reviewer responses.
        Friend Sub ShowSubmission(
            submissionId As Guid,
            showResponses As Boolean
        )

            ShowSection(_submissionsSection)
            SelectSubmissionById(submissionId)

            If showResponses Then
                _submissionDetail?.ShowReviewerResponses()
            End If

        End Sub


        Friend Sub ShowReadinessAndPackets()

            For Each tab As ShelfTabButton In _sectionTabs
                If tab.Text = "Readiness & Packets" Then
                    ShowSection(DirectCast(tab.Tag, Control))
                    Return
                End If
            Next

        End Sub


        Friend Sub ConfirmDeleteFromPage()

            RequestDelete(
                Me,
                EventArgs.Empty
            )

        End Sub


        Private Sub ShowSection(
            section As Control
        )

            For Each tab As ShelfTabButton In _sectionTabs
                Dim panel As Control = DirectCast(tab.Tag, Control)
                panel.Visible = panel Is section
                If panel Is section AndAlso Not tab.Checked Then
                    tab.Checked = True
                End If
            Next

        End Sub


        ' Shows the tab holding a control, such as a waypoint's version.
        Private Sub ShowSectionContaining(
            target As Control
        )

            Dim current As Control = target

            While current IsNot Nothing
                For Each tab As ShelfTabButton In _sectionTabs
                    If tab.Tag Is current Then
                        ShowSection(current)
                        Return
                    End If
                Next
                current = current.Parent
            End While

        End Sub


        Public Sub NavigateToRouteWaypoint(
            waypoint As ManuscriptRouteWaypoint
        )

            _pendingRouteWaypoint =
                waypoint

        End Sub


        Protected Overrides Sub OnShown(
            e As EventArgs
        )

            MyBase.OnShown(
                e
            )

            ' A hosted page is sized by the main window.
            If TopLevel Then
                ApplyResponsiveInitialSize()
            End If

            If _pendingRouteWaypoint IsNot Nothing Then

                BeginInvoke(
                    New Action(
                        AddressOf ApplyPendingRouteNavigation
                    )
                )

            End If

        End Sub


        Private Sub ApplyResponsiveInitialSize()

            Dim referenceControl As Control =
                If(
                    Me.Owner,
                    Me
                )

            Dim workingArea As Rectangle =
                Screen.FromControl(
                    referenceControl
                ).WorkingArea

            Dim desiredHeight As Integer =
                If(
                    _workingManuscript.Location =
                    ManuscriptLocation.FileDrawer,
                    980,
                    940
                )

            Dim initialSize As Size =
                ResponsiveDialogSizingService.CalculateInitialSize(
                    workingArea,
                    New Size(1180, desiredHeight),
                    Me.MinimumSize,
                    72
                )

            Me.Size =
                initialSize

            Me.Location =
                ResponsiveDialogSizingService.CalculateCenteredLocation(
                    workingArea,
                    initialSize
                )

        End Sub


        Private Sub ApplyPendingRouteNavigation()

            Dim waypoint As ManuscriptRouteWaypoint =
                _pendingRouteWaypoint

            _pendingRouteWaypoint =
                Nothing

            If waypoint Is Nothing Then
                Return
            End If

            Select Case RouteNavigationService.SectionForWaypoint(
                waypoint
            )

                Case ManuscriptDetailsSection.VersionHistory

                    If waypoint.VersionId.HasValue AndAlso
                       versionHistoryControl IsNot Nothing Then

                        versionHistoryControl.SelectVersionById(
                            waypoint.VersionId.Value
                        )

                    End If

                    ScrollControlIntoDetailsView(
                        versionHistoryControl
                    )

                Case ManuscriptDetailsSection.JournalSubmissions

                    If waypoint.SubmissionId.HasValue Then

                        SelectSubmissionById(
                            waypoint.SubmissionId.Value
                        )

                    End If

                    ScrollControlIntoDetailsView(
                        _submissionsSection
                    )

                Case Else

                    ScrollControlIntoDetailsView(
                        FindGroupBoxByText(
                            Me,
                            "Manuscript"
                        )
                    )

            End Select

        End Sub


        Private Sub SelectSubmissionById(
            submissionId As Guid
        )

            For index As Integer =
                0 To _displayedSubmissions.Count - 1

                Dim submission As JournalSubmission =
                    _displayedSubmissions(index)

                If submission IsNot Nothing AndAlso
                   submission.Id =
                   submissionId Then

                    lstSubmissions.SelectedIndex =
                        index

                    Return

                End If

            Next

        End Sub


        Private Sub ScrollControlIntoDetailsView(
            target As Control
        )

            If target Is Nothing Then
                Return
            End If

            ShowSectionContaining(
                target
            )

            Dim current As Control =
                target.Parent

            While current IsNot Nothing

                Dim scrollable As ScrollableControl =
                    TryCast(
                        current,
                        ScrollableControl
                    )

                If scrollable IsNot Nothing AndAlso
                   scrollable.AutoScroll Then

                    scrollable.ScrollControlIntoView(
                        target
                    )

                    Return

                End If

                current =
                    current.Parent

            End While

        End Sub


        Private Function FindGroupBoxByText(
            rootControl As Control,
            groupText As String
        ) As GroupBox

            If rootControl Is Nothing Then
                Return Nothing
            End If

            Dim group As GroupBox =
                TryCast(
                    rootControl,
                    GroupBox
                )

            If group IsNot Nothing AndAlso
               String.Equals(
                   group.Text,
                   groupText,
                   StringComparison.CurrentCultureIgnoreCase
               ) Then

                Return group

            End If

            For Each child As Control In
                rootControl.Controls

                Dim match As GroupBox =
                    FindGroupBoxByText(
                        child,
                        groupText
                    )

                If match IsNot Nothing Then
                    Return match
                End If

            Next

            Return Nothing

        End Function


        ' =====================================================
        ' Interface
        ' =====================================================

        Private Sub BuildInterface()

            Me.Text = "Manuscript Details"
            Me.StartPosition = FormStartPosition.CenterParent

            If _workingManuscript.Location =
                ManuscriptLocation.FileDrawer Then

                Me.Size = New Size(980, 980)

            Else

                Me.Size = New Size(980, 960)

            End If

            Me.MinimumSize = New Size(860, 760)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim shell As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(0)
            }

            shell.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            shell.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            shell.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            Dim scrollHost As New Panel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .Padding = New Padding(0)
            }

            ' The Overview tab: the manuscript's fields and, when filed, its
            ' File Drawer details.
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(0)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 330))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            BuildShortlistGroup()
            BuildClassificationGroup()

            ' The linked Journal Library record's notes and checklist, so its
            ' requirements are in view while editing.
            journalNotesGroup.Text = "Target journal notes"
            journalNotesGroup.Dock = DockStyle.Top
            journalNotesGroup.AutoSize = True
            journalNotesGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink
            journalNotesGroup.Padding = New Padding(14, 8, 14, 12)
            journalNotesGroup.Margin = New Padding(3, 8, 3, 8)
            journalNotesGroup.Visible = False

            lblJournalNotes.AutoSize = True
            lblJournalNotes.UseMnemonic = False
            lblJournalNotes.Dock = DockStyle.Top
            lblJournalNotes.Margin = New Padding(0)
            journalNotesGroup.Controls.Add(lblJournalNotes)

            AddHandler journalNotesGroup.Resize,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(200, journalNotesGroup.ClientSize.Width - journalNotesGroup.Padding.Horizontal)
                    If lblJournalNotes.MaximumSize.Width <> width Then
                        lblJournalNotes.MaximumSize = New Size(width, 0)
                    End If
                End Sub

            ' =================================================
            ' Manuscript metadata
            ' =================================================

            Dim detailsGroup As New SectionCard With {
                .Text = "Manuscript",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14)
            }

            Dim details As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 6
            }

            details.ColumnStyles.Add(
                New ColumnStyle(SizeType.AutoSize)
            )

            details.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 100)
            )

            For i As Integer = 0 To 5
                details.RowStyles.Add(
                    New RowStyle(SizeType.Percent, 16.6667F)
                )
            Next

            txtTitle.Dock = DockStyle.Fill
            txtTitle.AccessibleName = "Title"
            txtTargetJournal.Dock = DockStyle.Fill
            txtTargetJournal.AccessibleName = "Target journal"
            cmbStage.AccessibleName = "Current stage"

            cmbStage.Dock = DockStyle.Fill
            cmbStage.DropDownStyle = ComboBoxStyle.DropDownList

            For Each stage As PaperStage In
                System.Enum.GetValues(GetType(PaperStage))

                cmbStage.Items.Add(stage)

            Next

            details.Controls.Add(CreateFieldLabel("Title"), 0, 0)
            details.Controls.Add(txtTitle, 1, 0)

            details.Controls.Add(CreateFieldLabel("Target journal"), 0, 1)
            details.Controls.Add(txtTargetJournal, 1, 1)

            details.Controls.Add(CreateFieldLabel("Current stage"), 0, 2)
            details.Controls.Add(cmbStage, 1, 2)

            AddHandler cmbStage.SelectedIndexChanged,
                Sub(sender, e)
                    RefreshRevisionDeadlineDisplay()
                End Sub

            Dim revisionDeadlinePanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0)
            }

            lblRevisionDeadlineValue.AutoSize = True
            lblRevisionDeadlineValue.Anchor = AnchorStyles.Left
            lblRevisionDeadlineValue.ForeColor = SystemColors.GrayText
            lblRevisionDeadlineValue.Margin = New Padding(0, 9, 10, 0)

            btnRevisionDeadline.Text = "Set / Edit..."
            btnRevisionDeadline.AutoSize = True
            btnRevisionDeadline.Height = 34

            AddHandler btnRevisionDeadline.Click,
                AddressOf OpenRevisionDeadlineEditor

            revisionDeadlinePanel.Controls.Add(
                lblRevisionDeadlineValue
            )

            revisionDeadlinePanel.Controls.Add(
                btnRevisionDeadline
            )

            details.Controls.Add(
                CreateFieldLabel("Revision deadline"),
                0,
                3
            )

            details.Controls.Add(
                revisionDeadlinePanel,
                1,
                3
            )

            btnMetadata.Text =
                "DOI && Crossref Metadata..."

            btnMetadata.AutoSize =
                True

            btnMetadata.Height =
                36

            btnMetadata.Anchor =
                AnchorStyles.Left

            AddHandler btnMetadata.Click,
                AddressOf OpenCrossrefMetadata

            details.Controls.Add(CreateFieldLabel("Metadata"), 0, 4)
            details.Controls.Add(btnMetadata, 1, 4)

            btnJournalLinks.Text =
                "Journal, Preprint && Links..."

            btnJournalLinks.AutoSize =
                True

            btnJournalLinks.Height =
                36

            btnJournalLinks.Anchor =
                AnchorStyles.Left

            AddHandler btnJournalLinks.Click,
                AddressOf OpenJournalLinks

            details.Controls.Add(CreateFieldLabel("Links"), 0, 5)
            details.Controls.Add(CreateJournalToolsPanel(), 1, 5)

            detailsGroup.Controls.Add(details)

            ' =================================================
            ' File Drawer metadata
            ' =================================================

            fileDrawerGroup.Text = "File Drawer"
            fileDrawerGroup.Dock = DockStyle.Top
            fileDrawerGroup.AutoSize = True
            fileDrawerGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink
            fileDrawerGroup.Padding = New Padding(14)
            fileDrawerGroup.Margin = New Padding(3, 8, 3, 8)
            fileDrawerGroup.Visible =
                _workingManuscript.Location =
                ManuscriptLocation.FileDrawer

            Dim fileDrawerLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .RowCount = 2,
                .Margin = New Padding(0)
            }

            fileDrawerLayout.ColumnStyles.Add(
                New ColumnStyle(SizeType.AutoSize)
            )

            fileDrawerLayout.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 100)
            )

            fileDrawerLayout.RowStyles.Add(
                New RowStyle(SizeType.Absolute, 34)
            )

            fileDrawerLayout.RowStyles.Add(
                New RowStyle(SizeType.Absolute, 74)
            )

            lblFileDrawerDateValue.AutoSize = True
            lblFileDrawerDateValue.Anchor = AnchorStyles.Left
            lblFileDrawerDateValue.ForeColor = SystemColors.GrayText

            txtFileDrawerReason.Dock = DockStyle.Fill
            txtFileDrawerReason.AccessibleName = "File Drawer reason"
            txtFileDrawerReason.Multiline = True
            txtFileDrawerReason.ScrollBars = ScrollBars.Vertical
            txtFileDrawerReason.MinimumSize = New Size(0, 58)

            fileDrawerLayout.Controls.Add(
                CreateFieldLabel("Filed on"),
                0,
                0
            )

            fileDrawerLayout.Controls.Add(
                lblFileDrawerDateValue,
                1,
                0
            )

            fileDrawerLayout.Controls.Add(
                CreateFieldLabel("Reason"),
                0,
                1
            )

            fileDrawerLayout.Controls.Add(
                txtFileDrawerReason,
                1,
                1
            )

            fileDrawerGroup.Controls.Add(fileDrawerLayout)


            ' =================================================
            ' Structured authors
            ' =================================================

            Dim authorsGroup As New SectionCard With {
                .Text = "Authors",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14),
                .Margin = New Padding(3, 8, 3, 8)
            }

            Dim authorsLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(0),
                .Margin = New Padding(0)
            }

            authorsLayout.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            authorsLayout.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            authorsLayout.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            lblAuthorInfo.AutoSize = True
            lblAuthorInfo.Anchor = AnchorStyles.Left
            lblAuthorInfo.ForeColor = SystemColors.GrayText
            lblAuthorInfo.Margin = New Padding(0, 0, 0, 4)

            Dim authorButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0),
                .Margin = New Padding(0, 0, 0, 6)
            }

            Dim btnManageAuthorLibrary As New Button With {
                .Text = "Manage Library",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnAddAuthor As New Button With {
                .Text = "Add Author",
                .AutoSize = True,
                .Height = 36
            }

            btnEditAuthor.Text = "Edit"
            btnEditAuthor.AutoSize = True
            btnEditAuthor.Height = 36

            btnRemoveAuthor.Text = "Remove"
            btnRemoveAuthor.AutoSize = True
            btnRemoveAuthor.Height = 36

            btnMoveAuthorUp.Text = "↑"
            btnMoveAuthorUp.AutoSize = True
            btnMoveAuthorUp.Height = 36
            btnMoveAuthorUp.AccessibleName = "Move author up"

            btnMoveAuthorDown.Text = "↓"
            btnMoveAuthorDown.AutoSize = True
            btnMoveAuthorDown.Height = 36
            btnMoveAuthorDown.AccessibleName = "Move author down"

            AddHandler btnManageAuthorLibrary.Click,
                AddressOf ManageAuthorLibrary

            AddHandler btnAddAuthor.Click,
                AddressOf AddStructuredAuthor

            AddHandler btnEditAuthor.Click,
                AddressOf EditStructuredAuthor

            AddHandler btnRemoveAuthor.Click,
                AddressOf RemoveStructuredAuthor

            AddHandler btnMoveAuthorUp.Click,
                AddressOf MoveStructuredAuthorUp

            AddHandler btnMoveAuthorDown.Click,
                AddressOf MoveStructuredAuthorDown

            authorButtons.Controls.Add(btnManageAuthorLibrary)
            authorButtons.Controls.Add(btnAddAuthor)
            authorButtons.Controls.Add(btnEditAuthor)
            authorButtons.Controls.Add(btnRemoveAuthor)
            authorButtons.Controls.Add(btnMoveAuthorUp)
            authorButtons.Controls.Add(btnMoveAuthorDown)

            lstAuthors.Dock = DockStyle.Fill
            lstAuthors.IntegralHeight = False
            lstAuthors.HorizontalScrollbar = True
            lstAuthors.MinimumSize = New Size(0, 105)
            lstAuthors.Margin = New Padding(0)

            authorsLayout.Controls.Add(lblAuthorInfo, 0, 0)
            authorsLayout.Controls.Add(authorButtons, 0, 1)

            AddHandler lstAuthors.SelectedIndexChanged,
                AddressOf AuthorSelectionChanged

            AddHandler lstAuthors.DoubleClick,
                AddressOf EditStructuredAuthor

            authorsLayout.Controls.Add(lstAuthors, 0, 2)

            authorsGroup.Controls.Add(authorsLayout)

            ' =================================================
            ' Manuscript version history
            ' =================================================

            versionHistoryControl =
                New ManuscriptVersionHistoryControl(
                    _workingManuscript
                ) With {
                    .Dock = DockStyle.Fill,
                    .Margin = New Padding(3, 8, 3, 8)
                }

            AddHandler versionHistoryControl.ViewPacketsRequested,
                Sub(versionId)
                    RunSubmissionWorkflow(New SubmissionWorkflowRequest With {
                        .Target = SubmissionWorkflowTarget.Packets, .VersionId = versionId
                    })
                End Sub

            ' =================================================
            ' Submissions
            ' =================================================

            Dim submissionsGroup As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .Margin = New Padding(0),
                .Padding = New Padding(0),
                .AccessibleName = "Journal Submissions"
            }

            ' Runtime-built column widths are not rescaled with the form, so
            ' scale the list's width for the display.
            Dim listWidth As Integer = CInt(Math.Round(330 * DeviceDpi / 96.0))
            submissionsGroup.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, listWidth))
            submissionsGroup.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            submissionsGroup.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim submissionsList As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Margin = New Padding(0, 0, 14, 0),
                .Padding = New Padding(0)
            }

            submissionsList.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            submissionsList.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            submissionsList.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            lblSubmissionInfo.AutoSize = True
            lblSubmissionInfo.MaximumSize = New Size(CInt(Math.Round(310 * DeviceDpi / 96.0)), 0)
            lblSubmissionInfo.ForeColor = SystemColors.GrayText
            lblSubmissionInfo.Margin = New Padding(0, 0, 0, 6)

            Dim submissionButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 0, 0, 6),
                .Padding = New Padding(0)
            }

            ' Recording a submission stays an explicit action.
            Dim btnAddSubmission As New Button With {
                .Text = "Record Submission...",
                .AutoSize = True,
                .Height = 34
            }

            btnEditSubmission.Text = "Edit"
            btnEditSubmission.AutoSize = True
            btnEditSubmission.Height = 34
            btnEditSubmission.Enabled = False
            btnEditSubmission.AccessibleName = "Edit the selected submission's journal, dates, and portal"

            btnDeleteSubmission.Text = "Delete"
            btnDeleteSubmission.AutoSize = True
            btnDeleteSubmission.Height = 34
            btnDeleteSubmission.Enabled = False
            btnDeleteSubmission.AccessibleName = "Delete the selected submission"

            AddHandler btnEditSubmission.Click,
                AddressOf EditSelectedSubmission

            AddHandler btnDeleteSubmission.Click,
                AddressOf DeleteSelectedSubmission

            AddHandler btnAddSubmission.Click,
                AddressOf AddSubmission

            submissionButtons.Controls.Add(btnAddSubmission)
            submissionButtons.Controls.Add(btnEditSubmission)
            submissionButtons.Controls.Add(btnDeleteSubmission)

            lstSubmissions.Dock = DockStyle.Fill
            lstSubmissions.IntegralHeight = False
            lstSubmissions.DrawMode = DrawMode.OwnerDrawVariable
            lstSubmissions.AccessibleName = "Journal submissions"

            AddHandler lstSubmissions.MeasureItem, AddressOf MeasureSubmissionItem
            AddHandler lstSubmissions.DrawItem, AddressOf DrawSubmissionItem

            AddHandler lstSubmissions.SelectedIndexChanged,
                AddressOf SubmissionSelectionChanged

            submissionsList.Controls.Add(submissionButtons, 0, 0)
            submissionsList.Controls.Add(lblSubmissionInfo, 0, 1)
            submissionsList.Controls.Add(lstSubmissions, 0, 2)

            _submissionDetailHost.Dock = DockStyle.Fill
            _submissionDetailHost.Margin = New Padding(0)
            _submissionDetailHost.Padding = New Padding(0)

            submissionsGroup.Controls.Add(submissionsList, 0, 0)
            submissionsGroup.Controls.Add(_submissionDetailHost, 1, 0)

            _submissionsSection = submissionsGroup

            ' =================================================
            ' Footer
            ' =================================================

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .RowCount = 1,
                .Padding = New Padding(0, 10, 0, 2)
            }

            footer.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 50)
            )

            footer.ColumnStyles.Add(
                New ColumnStyle(SizeType.Percent, 50)
            )

            Dim btnDeleteManuscript As New Button With {
                .Text = "Delete Manuscript",
                .AutoSize = True,
                .Height = 38,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(3, 3, 3, 4)
            }

            AddHandler btnDeleteManuscript.Click,
                AddressOf RequestDelete

            Dim rightButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Margin = New Padding(0)
            }

            Dim btnSave As New Button With {
                .Text = "Save && Close",
                .AutoSize = True,
                .Height = 38,
                .Margin = New Padding(3, 3, 3, 4)
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel,
                .Margin = New Padding(3, 3, 3, 4)
            }

            AddHandler btnSave.Click,
                AddressOf SaveChanges

            rightButtons.Controls.Add(btnSave)
            rightButtons.Controls.Add(btnCancel)

            footer.Controls.Add(btnDeleteManuscript, 0, 0)
            footer.Controls.Add(rightButtons, 1, 0)

            root.Controls.Add(detailsGroup, 0, 0)
            root.Controls.Add(shortlistGroup, 0, 1)
            root.Controls.Add(classificationGroup, 0, 2)
            root.Controls.Add(fileDrawerGroup, 0, 3)
            root.Controls.Add(journalNotesGroup, 0, 4)

            scrollHost.Controls.Add(root)

            ' =================================================
            ' Sections as tabs, one visible at a time
            ' =================================================

            Dim sectionTabs As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(20, 6, 20, 0),
                .Margin = New Padding(0),
                .AccessibleName = "Manuscript sections",
                .AccessibleRole = AccessibleRole.PageTabList
            }

            AddHandler sectionTabs.Paint,
                Sub(sender, e)
                    Using line As New Pen(UiTheme.CardBorder())
                        e.Graphics.DrawLine(line, 20, sectionTabs.Height - 1, sectionTabs.Width - 20, sectionTabs.Height - 1)
                    End Using
                End Sub

            Dim sectionHost As New Panel With {
                .Dock = DockStyle.Fill,
                .Padding = New Padding(20, 12, 20, 8),
                .Margin = New Padding(0)
            }

            authorsGroup.Margin = New Padding(0)
            versionHistoryControl.Margin = New Padding(0)

            For Each section In {
                ("Overview", CType(scrollHost, Control)),
                ("Authors", CType(authorsGroup, Control)),
                ("Versions", CType(versionHistoryControl, Control)),
                ("Submissions", CType(submissionsGroup, Control)),
                ("Readiness & Packets", CreateReadinessSection())
            }
                Dim panel As Control = section.Item2
                panel.Dock = DockStyle.Fill
                panel.Visible = False
                sectionHost.Controls.Add(panel)

                Dim tab As New ShelfTabButton With {
                    .Text = section.Item1,
                    .UseMnemonic = False,
                    .Tag = panel
                }

                AddHandler tab.CheckedChanged,
                    Sub(sender, e)
                        If tab.Checked Then
                            ShowSection(DirectCast(tab.Tag, Control))
                        End If
                    End Sub

                _sectionTabs.Add(tab)
                sectionTabs.Controls.Add(tab)
            Next

            _sectionTabs(0).Checked = True
            ShowSection(scrollHost)

            shell.Controls.Add(
                sectionTabs,
                0,
                0
            )

            shell.Controls.Add(
                sectionHost,
                0,
                1
            )

            If Not _pageMode Then

                shell.Controls.Add(
                    footer,
                    0,
                    2
                )

                Me.AcceptButton = btnSave
                Me.CancelButton = btnCancel

            End If

            Me.Controls.Add(
                shell
            )

        End Sub


        ' =================================================
        ' Journal shortlist (#65) and choosing a journal (#89)
        ' =================================================

        Private Sub BuildShortlistGroup()

            shortlistGroup.Text = "Journal shortlist"
            shortlistGroup.Dock = DockStyle.Top
            shortlistGroup.AutoSize = True
            shortlistGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink
            shortlistGroup.Padding = New Padding(14, 8, 14, 12)
            shortlistGroup.Margin = New Padding(3, 8, 3, 8)

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 4,
                .Margin = New Padding(0)
            }
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For index As Integer = 0 To 3
                layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Next

            ' After a rejection: the next shortlisted journal, as an offer.
            shortlistOffer.Dock = DockStyle.Top
            shortlistOffer.AutoSize = True
            shortlistOffer.AutoSizeMode = AutoSizeMode.GrowAndShrink
            shortlistOffer.ColumnCount = 2
            shortlistOffer.RowCount = 1
            shortlistOffer.Padding = New Padding(10, 8, 10, 8)
            shortlistOffer.Margin = New Padding(0, 2, 0, 8)
            shortlistOffer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            shortlistOffer.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            lblShortlistOffer.AutoSize = True
            lblShortlistOffer.UseMnemonic = False
            lblShortlistOffer.Anchor = AnchorStyles.Left
            lblShortlistOffer.Margin = New Padding(0, 4, 12, 4)
            btnShortlistOffer.AutoSize = True
            btnShortlistOffer.UseMnemonic = False
            btnShortlistOffer.Anchor = AnchorStyles.Right
            AddHandler btnShortlistOffer.Click,
                Sub(sender, e)
                    Dim candidate As JournalCandidate = TryCast(btnShortlistOffer.Tag, JournalCandidate)
                    If candidate IsNot Nothing Then MakeShortlistTarget(candidate)
                End Sub
            shortlistOffer.Controls.Add(lblShortlistOffer, 0, 0)
            shortlistOffer.Controls.Add(btnShortlistOffer, 1, 0)
            shortlistOffer.Visible = False
            AddHandler shortlistOffer.Paint,
                Sub(sender, e)
                    Using fill As New SolidBrush(UiTheme.AccentMutedBackground())
                        e.Graphics.FillRectangle(fill, shortlistOffer.ClientRectangle)
                    End Using
                End Sub
            layout.Controls.Add(shortlistOffer, 0, 0)

            shortlistRows.Dock = DockStyle.Top
            shortlistRows.AutoSize = True
            shortlistRows.AutoSizeMode = AutoSizeMode.GrowAndShrink
            shortlistRows.ColumnCount = 1
            shortlistRows.Margin = New Padding(0)
            shortlistRows.AccessibleName = "Shortlisted journals"
            shortlistRows.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.Controls.Add(shortlistRows, 0, 1)

            lblShortlistEmpty.Text = "Add the journals you are considering for this manuscript, in your order of preference, with your reasons. " &
                                     "If a submission is rejected, PaperRoute offers the next one. " &
                                     "Not sure where to start? Find Journals... looks for journals that recently published work like this, using keywords you review first."
            lblShortlistEmpty.AutoSize = True
            lblShortlistEmpty.UseMnemonic = False
            lblShortlistEmpty.Margin = New Padding(3, 2, 3, 6)
            layout.Controls.Add(lblShortlistEmpty, 0, 2)

            Dim actions As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .WrapContents = True,
                .Margin = New Padding(0, 4, 0, 0)
            }
            Dim btnAdd As New Button With {.Text = "Add Journal...", .AutoSize = True, .Margin = New Padding(3, 0, 6, 0)}
            AddHandler btnAdd.Click, Sub(sender, e) AddShortlistCandidate()
            Dim btnFind As New Button With {.Text = "Find Journals...", .AutoSize = True, .Margin = New Padding(3, 0, 12, 0)}
            AddHandler btnFind.Click, Sub(sender, e) FindJournals()
            shortlistToolTip.SetToolTip(btnFind, "Find journals that recently published articles mentioning keywords you review, using OpenAlex, an open index.")
            AddHandler Me.Disposed, Sub(sender, e) shortlistToolTip.Dispose()
            Dim guide As New LinkLabel With {.Text = "How to choose a journal", .AutoSize = True, .Margin = New Padding(3, 7, 3, 0)}
            AddHandler guide.LinkClicked,
                Sub(sender, e)
                    Using help As New HelpForm("Choosing a Journal")
                        help.ShowDialog(Me.FindForm())
                    End Using
                End Sub
            actions.Controls.Add(btnAdd)
            actions.Controls.Add(btnFind)
            actions.Controls.Add(guide)
            layout.Controls.Add(actions, 0, 3)

            AddHandler shortlistGroup.Resize,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(240, shortlistGroup.ClientSize.Width - shortlistGroup.Padding.Horizontal - 6)
                    lblShortlistEmpty.MaximumSize = New Size(width, 0)
                    lblShortlistOffer.MaximumSize = New Size(Math.Max(200, width - btnShortlistOffer.Width - 40), 0)
                End Sub

            shortlistGroup.Controls.Add(layout)

        End Sub


        Private Sub RefreshShortlist()

            If _workingManuscript.JournalShortlist Is Nothing Then _workingManuscript.JournalShortlist = New List(Of JournalCandidate)()
            Dim list As List(Of JournalCandidate) = _workingManuscript.JournalShortlist

            shortlistRows.SuspendLayout()
            For Each child As Control In shortlistRows.Controls.Cast(Of Control)().ToList()
                shortlistRows.Controls.Remove(child)
                child.Dispose()
            Next
            shortlistRows.RowStyles.Clear()
            shortlistRows.RowCount = Math.Max(1, list.Count)
            For index As Integer = 0 To list.Count - 1
                shortlistRows.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                shortlistRows.Controls.Add(CreateShortlistRow(list(index), index, list.Count), 0, index)
            Next
            shortlistRows.ResumeLayout(True)
            shortlistRows.Visible = list.Count > 0
            lblShortlistEmpty.Visible = list.Count = 0

            Dim offer As RerouteOffer = JournalShortlistService.RerouteOfferFor(_workingManuscript, DateTime.Today)
            shortlistOffer.Visible = offer IsNot Nothing
            If offer IsNot Nothing Then
                Dim outcome As String
                Select Case offer.Outcome
                    Case SubmissionOutcome.DeskRejected : outcome = "Desk rejected by "
                    Case SubmissionOutcome.Withdrawn : outcome = "Withdrawn from "
                    Case Else : outcome = "Rejected by "
                End Select
                lblShortlistOffer.Text = outcome & offer.ClosedSubmission.JournalName &
                    If(offer.ClosedDate.HasValue, " on " & offer.ClosedDate.Value.ToString("MMM d, yyyy", Globalization.CultureInfo.CurrentCulture), String.Empty) &
                    ". Next on your shortlist: " & offer.NextCandidate.JournalName & " (" & JournalShortlistService.StatusName(offer.NextCandidate.Status) & ")."
                btnShortlistOffer.Text = "Make It the Target Journal"
                btnShortlistOffer.Tag = offer.NextCandidate
                lblShortlistOffer.BackColor = UiTheme.AccentMutedBackground()
                lblShortlistOffer.ForeColor = UiTheme.PrimaryText()
                shortlistOffer.Invalidate()
            End If

        End Sub


        Private Function CreateShortlistRow(candidate As JournalCandidate, index As Integer, count As Integer) As Control

            Dim isTarget As Boolean = RouteAnalyticsService.NameKey(candidate.JournalName) = RouteAnalyticsService.NameKey(txtTargetJournal.Text)
            Dim row As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 4,
                .RowCount = 1,
                .Margin = New Padding(0, 0, 0, 6),
                .AccessibleName = candidate.JournalName,
                .AccessibleRole = AccessibleRole.ListItem
            }
            Dim statusWidth As Integer
            Using bold As New Font(Me.Font, FontStyle.Bold)
                statusWidth = TextRenderer.MeasureText(count.ToString(Globalization.CultureInfo.CurrentCulture) & ".  Considering", bold).Width + 12
            End Using
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, statusWidth))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            Dim statusColor As Color
            Select Case candidate.Status
                Case CandidateStatus.Preferred : statusColor = UiTheme.AccentColor()
                Case CandidateStatus.RuledOut : statusColor = UiTheme.MutedText()
                Case Else : statusColor = UiTheme.SecondaryText()
            End Select
            Dim lblStatus As New Label With {
                .Text = (index + 1).ToString(Globalization.CultureInfo.CurrentCulture) & ".  " & JournalShortlistService.StatusName(candidate.Status),
                .AutoSize = True,
                .UseMnemonic = False,
                .ForeColor = statusColor,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0, 3, 6, 0)
            }

            Dim details As New List(Of String)
            If isTarget Then details.Add("Target journal")
            Dim submitted As String = JournalShortlistService.SubmissionText(_workingManuscript, candidate, DateTime.Today)
            If submitted.Length > 0 Then details.Add(submitted)
            Dim note As String = If(candidate.Notes, String.Empty).Trim().Replace(Environment.NewLine, " ")
            If note.Length > 90 Then note = note.Substring(0, 90).TrimEnd() & ChrW(&H2026)
            If note.Length > 0 Then details.Add(note)
            Dim checks As String = JournalChoiceGuide.Summary(candidate)
            If checks.Length > 0 Then details.Add(checks)

            Dim text As New Label With {
                .AutoSize = True,
                .UseMnemonic = False,
                .Text = candidate.JournalName & If(details.Count > 0, Environment.NewLine & String.Join("  ·  ", details), String.Empty),
                .Margin = New Padding(0, 3, 8, 0),
                .Font = If(candidate.Status = CandidateStatus.RuledOut, New Font(Me.Font, FontStyle.Strikeout), Me.Font)
            }

            ' One line of the journal's facts from the Journals page (#87).
            Dim linked As JournalRecord = ShortlistRecord(candidate)
            Dim factsLine As String = JournalFactsService.OneLine(linked)
            Dim facts As New Label With {
                .AutoSize = True,
                .UseMnemonic = False,
                .Text = factsLine,
                .Visible = factsLine.Length > 0,
                .ForeColor = UiTheme.MutedText(),
                .Margin = New Padding(0, 2, 8, 0)
            }
            Dim textStack As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .Margin = New Padding(0)
            }
            textStack.Controls.Add(text)
            textStack.Controls.Add(facts)

            ' Why Find Journals suggested it (#88), with the keywords on hover.
            Dim evidenceLine As String = JournalShortlistService.EvidenceLine(candidate.Evidence)
            Dim evidence As New Label With {
                .AutoSize = True,
                .UseMnemonic = False,
                .Text = evidenceLine,
                .Visible = evidenceLine.Length > 0,
                .ForeColor = UiTheme.MutedText(),
                .Margin = New Padding(0, 2, 8, 0)
            }
            If candidate.Evidence IsNot Nothing Then
                shortlistToolTip.SetToolTip(evidence, "Keywords: " & String.Join(If(candidate.Evidence.MatchAll, " AND ", " OR "), candidate.Evidence.Keywords.Select(Function(item) """" & item & """")) &
                    If(candidate.Evidence.Examples.Count > 0, Environment.NewLine & "For example: " & candidate.Evidence.Examples(0).Title, String.Empty))
            End If
            textStack.Controls.Add(evidence)

            Dim btnEdit As New Button With {.Text = "Edit...", .AutoSize = True, .AccessibleName = "Edit " & candidate.JournalName, .Margin = New Padding(3, 0, 3, 0)}
            AddHandler btnEdit.Click, Sub(sender, e) EditShortlistCandidate(candidate)

            Dim menu As New ContextMenuStrip()
            Dim makeTarget As ToolStripItem = menu.Items.Add("Make Target Journal", Nothing, Sub(sender, e) MakeShortlistTarget(candidate))
            makeTarget.Enabled = Not isTarget
            Dim moveUp As ToolStripItem = menu.Items.Add("Move Up", Nothing, Sub(sender, e) MoveShortlistCandidate(candidate, -1))
            moveUp.Enabled = index > 0
            Dim moveDown As ToolStripItem = menu.Items.Add("Move Down", Nothing, Sub(sender, e) MoveShortlistCandidate(candidate, 1))
            moveDown.Enabled = index < count - 1
            menu.Items.Add(New ToolStripSeparator())
            menu.Items.Add("Remove from Shortlist", Nothing, Sub(sender, e) RemoveShortlistCandidate(candidate))
            Dim btnMore As New Button With {.Text = ChrW(&H22EF), .AutoSize = True, .AccessibleName = "More actions for " & candidate.JournalName, .Margin = New Padding(3, 0, 0, 0)}
            AddHandler btnMore.Click, Sub(sender, e) menu.Show(btnMore, New Point(0, btnMore.Height))
            AddHandler row.Disposed, Sub(sender, e) menu.Dispose()
            AddHandler row.Resize,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(200, row.ClientSize.Width - statusWidth - btnEdit.Width - btnMore.Width - 24)
                    text.MaximumSize = New Size(width, 0)
                    facts.MaximumSize = New Size(width, 0)
                    evidence.MaximumSize = New Size(width, 0)
                End Sub

            row.Controls.Add(lblStatus, 0, 0)
            row.Controls.Add(textStack, 1, 0)
            row.Controls.Add(btnEdit, 2, 0)
            row.Controls.Add(btnMore, 3, 0)
            Return row

        End Function


        ' The candidate's Journal Library record: by link, by its evidence's
        ' ISSNs, else by name.
        Private Function ShortlistRecord(candidate As JournalCandidate) As JournalRecord
            Dim library As List(Of JournalRecord) = If(_authorLibrary?.Journals, New List(Of JournalRecord)())
            If candidate.JournalId.HasValue Then
                Dim byId As JournalRecord = library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = candidate.JournalId.Value)
                If byId IsNot Nothing Then Return byId
            End If
            Dim key As String = RouteAnalyticsService.NameKey(candidate.JournalName)
            If candidate.Evidence IsNot Nothing AndAlso candidate.Evidence.Issns.Count > 0 Then
                Dim byIssn As JournalRecord = library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso IssnService.NormalizeList(item.Issns).Intersect(candidate.Evidence.Issns).Any())
                If byIssn IsNot Nothing Then Return byIssn
            End If
            Return library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
        End Function


        Private Function PromptCandidate(existing As JournalCandidate) As JournalCandidate

            If candidatePrompt IsNot Nothing Then Return candidatePrompt(existing)

            Dim library As List(Of JournalRecord) = If(_authorLibrary?.Journals, New List(Of JournalRecord)())
            Dim names As New List(Of String)(library.Where(Function(item) item IsNot Nothing).Select(Function(item) item.Name))
            For Each manuscript As Manuscript In If(_allManuscripts, New List(Of Manuscript)())
                If manuscript Is Nothing Then Continue For
                If Not String.IsNullOrWhiteSpace(manuscript.TargetJournal) Then names.Add(manuscript.TargetJournal)
                names.AddRange(If(manuscript.Submissions, New List(Of JournalSubmission)()).Where(Function(item) item IsNot Nothing).Select(Function(item) item.JournalName))
            Next

            Dim statistics As LibraryStatistics = RouteAnalyticsService.ForLibrary(If(_allManuscripts, New List(Of Manuscript)()), DateTime.Today, library)
            Dim history As Func(Of String, String) =
                Function(name)
                    Dim key As String = RouteAnalyticsService.NameKey(name)
                    Dim record As JournalRecord = library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
                    Return RouteAnalyticsService.DescribeHistory(RouteAnalyticsService.FindHistory(statistics, name, record?.Id))
                End Function

            ' The journal's facts from the Journals page, beside the questions (#87).
            Dim factsFor As Func(Of String, JournalRecord) =
                Function(name)
                    Dim key As String = RouteAnalyticsService.NameKey(name)
                    If existing IsNot Nothing AndAlso RouteAnalyticsService.NameKey(existing.JournalName) = key Then Return ShortlistRecord(existing)
                    Return library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
                End Function

            Using dialog As New JournalCandidateForm(existing, names, history, factsFor)
                If dialog.ShowDialog(Me.FindForm()) <> DialogResult.OK Then Return Nothing
                Return New JournalCandidate With {.JournalName = dialog.JournalName, .Status = dialog.Status, .Notes = dialog.Notes, .Checks = dialog.Checks}
            End Using

        End Function


        Private Sub AddShortlistCandidate()
            Dim result As JournalCandidate = PromptCandidate(Nothing)
            If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.JournalName) Then Return
            Dim candidate As JournalCandidate = JournalShortlistService.Add(_workingManuscript, result.JournalName, _authorLibrary?.Journals, result.Status)
            candidate.Status = result.Status
            candidate.Notes = result.Notes
            candidate.Checks = result.Checks
            RefreshShortlist()
        End Sub


        Private Sub EditShortlistCandidate(candidate As JournalCandidate)
            Dim result As JournalCandidate = PromptCandidate(candidate)
            If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.JournalName) Then Return
            If RouteAnalyticsService.NameKey(result.JournalName) <> RouteAnalyticsService.NameKey(candidate.JournalName) Then
                Dim key As String = RouteAnalyticsService.NameKey(result.JournalName)
                Dim record As JournalRecord = If(_authorLibrary?.Journals, New List(Of JournalRecord)()).FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
                candidate.JournalName = If(record IsNot Nothing, record.Name, result.JournalName.Trim())
                candidate.JournalId = If(record IsNot Nothing, CType(record.Id, Guid?), Nothing)
                candidate.Evidence = Nothing
            End If
            candidate.Status = result.Status
            candidate.Notes = result.Notes
            candidate.Checks = result.Checks
            RefreshShortlist()
        End Sub


        Private Sub MoveShortlistCandidate(candidate As JournalCandidate, offset As Integer)
            JournalShortlistService.Move(_workingManuscript, candidate, offset)
            RefreshShortlist()
        End Sub


        Private Sub RemoveShortlistCandidate(candidate As JournalCandidate)
            _workingManuscript.JournalShortlist.Remove(candidate)
            RefreshShortlist()
        End Sub


        ' The target journal box is what Save reads, so it changes too.
        Private Sub MakeShortlistTarget(candidate As JournalCandidate)
            JournalShortlistService.MakeTarget(_workingManuscript, candidate)
            txtTargetJournal.Text = candidate.JournalName
            RefreshJournalNotes()
            RefreshShortlist()
        End Sub


        ' For tests.
        ' Find Journals (#88): the researcher reviews keywords, OpenAlex finds
        ' journals that published matching articles, and the checked ones join
        ' the shortlist as Considering, with their evidence. Nothing else changes.
        Private Sub FindJournals()

            Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.JournalSuggestions)
            If blocked.HasValue AndAlso journalSuggestionPrompt Is Nothing Then
                MessageBox.Show(Me.FindForm(), OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.JournalSuggestions), blocked.Value),
                                "Find Journals", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim library As List(Of JournalRecord) = If(_authorLibrary?.Journals, New List(Of JournalRecord)())
            Dim chosen As List(Of JournalSuggestion) = Nothing
            Dim result As JournalSuggestionsResult = Nothing

            If journalSuggestionPrompt IsNot Nothing Then
                Dim answer = journalSuggestionPrompt()
                chosen = answer.Chosen
                result = answer.Result
            Else
                Dim statistics As LibraryStatistics = RouteAnalyticsService.ForLibrary(If(_allManuscripts, New List(Of Manuscript)()), DateTime.Today, library)
                Dim recordFor As Func(Of JournalSuggestion, JournalRecord) =
                    Function(journal) JournalShortlistService.LibraryRecordFor(journal.Name, journal.Issns, library)
                ' The same match Add to Shortlist makes, so an addable journal is added.
                Dim onShortlist As Func(Of JournalSuggestion, Boolean) =
                    Function(journal) JournalShortlistService.FindFound(_workingManuscript, journal.Name, journal.Issns, journal.OpenAlexId, library) IsNot Nothing
                Dim yours As Func(Of JournalSuggestion, String) =
                    Function(journal)
                        Dim record As JournalRecord = recordFor(journal)
                        Dim history As JournalHistory = RouteAnalyticsService.FindHistory(statistics, If(record?.Name, journal.Name), record?.Id)
                        If history IsNot Nothing AndAlso history.Count > 0 Then
                            Return "Submitted " & history.Count.ToString(Globalization.CultureInfo.CurrentCulture) & ChrW(&HD7) & ", last " &
                                   history.LastSubmitted.Year.ToString(Globalization.CultureInfo.InvariantCulture)
                        End If
                        Return If(record IsNot Nothing, "In your Journal Library", String.Empty)
                    End Function
                Using dialog As New JournalSuggestionsForm(txtTitle.Text, _workingManuscript.Metadata?.Keywords, New OnlineJournalSuggestionsSource(), onShortlist, yours)
                    If dialog.ShowDialog(Me.FindForm()) = DialogResult.OK Then
                        chosen = dialog.Chosen.ToList()
                        result = dialog.Result
                    End If
                End Using
            End If

            If chosen Is Nothing OrElse result Is Nothing OrElse chosen.Count = 0 Then Return

            Dim added As Integer = 0
            For Each journal As JournalSuggestion In chosen
                Dim entry = JournalShortlistService.AddFound(_workingManuscript, journal.Name, journal.Issns, journal.OpenAlexId, library)
                If Not entry.Created Then Continue For
                entry.Candidate.Evidence = JournalSuggestionService.EvidenceFor(journal, result)
                added += 1
            Next

            RefreshShortlist()
            If added > 0 Then
                RaiseEvent StatusMessage(Me,
                    If(added = 1, "Added 1 journal", "Added " & added.ToString(Globalization.CultureInfo.CurrentCulture) & " journals") &
                    " to the shortlist as Considering. Save the manuscript page to keep them.")
            End If

        End Sub


        Friend Sub FindJournalsForTest()
            FindJournals()
        End Sub


        Friend Sub AddShortlistCandidateForTest()
            AddShortlistCandidate()
        End Sub

        Friend Sub MakeShortlistTargetForTest(candidate As JournalCandidate)
            MakeShortlistTarget(candidate)
        End Sub

        Friend ReadOnly Property ShortlistOfferText As String
            Get
                Return If(shortlistOffer.Visible, lblShortlistOffer.Text, String.Empty)
            End Get
        End Property

        Friend ReadOnly Property ShortlistOfferButton As Button
            Get
                Return btnShortlistOffer
            End Get
        End Property


        Private Sub BuildClassificationGroup()

            classificationGroup.Text = "Type and tags"
            classificationGroup.Dock = DockStyle.Top
            classificationGroup.AutoSize = True
            classificationGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink
            classificationGroup.Padding = New Padding(14, 8, 14, 12)
            classificationGroup.Margin = New Padding(3, 8, 3, 8)

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .RowCount = 2,
                .Margin = New Padding(0)
            }
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            cmbWorkType.DropDownStyle = ComboBoxStyle.DropDownList
            cmbWorkType.AccessibleName = "Type of work"
            cmbWorkType.Width = 240
            cmbWorkType.Anchor = AnchorStyles.Left
            cmbWorkType.Margin = New Padding(3, 4, 3, 6)
            For Each type As WorkType In [Enum].GetValues(GetType(WorkType))
                cmbWorkType.Items.Add(WorkTypeService.DisplayName(type))
            Next
            AddHandler cmbWorkType.SelectedIndexChanged,
                Sub(sender, e)
                    If cmbWorkType.SelectedIndex >= 0 Then _workingManuscript.WorkType = CType(cmbWorkType.SelectedIndex, WorkType)
                End Sub

            tagEditor.Anchor = AnchorStyles.Left Or AnchorStyles.Right
            tagEditor.Margin = New Padding(3, 2, 3, 2)
            AddHandler tagEditor.ColorChanged, Sub(sender, e) _authorLibraryDirty = True

            Dim typeLabel As Label = CreateFieldLabel("Type")
            Dim tagsLabel As Label = CreateFieldLabel("Tags")
            typeLabel.Margin = New Padding(3, 4, 16, 6)
            layout.Controls.Add(typeLabel, 0, 0)
            layout.Controls.Add(cmbWorkType, 1, 0)
            layout.Controls.Add(tagsLabel, 0, 1)
            layout.Controls.Add(tagEditor, 1, 1)

            classificationGroup.Controls.Add(layout)

        End Sub


        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function


        ' =====================================================
        ' Load
        ' =====================================================

        Private Sub LoadManuscript()

            ManuscriptLifecycleService.
                ReconcileFromLatestWorkflow(
                    _workingManuscript
                )

            txtTitle.Text =
                _workingManuscript.Title

            txtTargetJournal.Text =
                _workingManuscript.TargetJournal

            cmbStage.SelectedItem =
                _workingManuscript.CurrentStage

            fileDrawerGroup.Visible =
                _workingManuscript.Location =
                ManuscriptLocation.FileDrawer

            If _workingManuscript.FileDrawerDate.HasValue Then

                lblFileDrawerDateValue.Text =
                    _workingManuscript.FileDrawerDate.Value.ToString(
                        "MMMM d, yyyy"
                    )

            Else

                lblFileDrawerDateValue.Text =
                    "Date not recorded"

            End If

            txtFileDrawerReason.Text =
                If(
                    _workingManuscript.FileDrawerReason,
                    String.Empty
                )

            If _workingManuscript.Tags Is Nothing Then _workingManuscript.Tags = New List(Of String)()
            cmbWorkType.SelectedIndex = CInt(_workingManuscript.WorkType)
            ' Tags already used in the library are suggested while typing.
            tagEditor.Bind(_workingManuscript.Tags, _authorLibrary, WorkTypeService.AllTags(_allManuscripts))

            RefreshAuthorsList()
            RefreshSubmissionList()

        End Sub



        Private Sub RefreshLifecycleControls()

            txtTargetJournal.Text =
                _workingManuscript.TargetJournal

            cmbStage.SelectedItem =
                _workingManuscript.CurrentStage

            RefreshRevisionDeadlineDisplay()

        End Sub


        ' =====================================================
        ' Revision deadline shortcut
        ' =====================================================

        Private Sub RefreshRevisionDeadlineDisplay()

            If lblRevisionDeadlineValue Is Nothing OrElse
               btnRevisionDeadline Is Nothing Then

                Return

            End If

            Dim selectedStage As PaperStage =
                _workingManuscript.CurrentStage

            If cmbStage.SelectedItem IsNot Nothing Then

                selectedStage =
                    CType(
                        cmbStage.SelectedItem,
                        PaperStage
                    )

            End If

            If selectedStage <> PaperStage.Revision Then

                lblRevisionDeadlineValue.Text =
                    "Available when stage is Revision"

                btnRevisionDeadline.Text =
                    "Set / Edit..."

                btnRevisionDeadline.Enabled =
                    False

                Return

            End If

            btnRevisionDeadline.Enabled =
                True

            Dim latestSubmission As JournalSubmission =
                ManuscriptAttentionService.GetLatestSubmission(
                    _workingManuscript
                )

            Dim latestDecision As EditorialDecisionEvent =
                ManuscriptAttentionService.GetLatestDecision(
                    latestSubmission
                )

            If latestDecision IsNot Nothing AndAlso
               latestDecision.RevisionDeadline.HasValue Then

                lblRevisionDeadlineValue.Text =
                    latestDecision.RevisionDeadline.Value.ToString(
                        "MMMM d, yyyy"
                    )

                btnRevisionDeadline.Text =
                    "Edit..."

                Return

            End If

            If _workingManuscript.RevisionDeadline.HasValue Then

                lblRevisionDeadlineValue.Text =
                    _workingManuscript.RevisionDeadline.Value.ToString(
                        "MMMM d, yyyy"
                    ) &
                    " (legacy record)"

                btnRevisionDeadline.Text =
                    "Set / Edit..."

                Return

            End If

            lblRevisionDeadlineValue.Text =
                "Not set"

            btnRevisionDeadline.Text =
                "Set / Edit..."

        End Sub


        Private Sub OpenRevisionDeadlineEditor(
            sender As Object,
            e As EventArgs
        )

            Dim selectedStage As PaperStage =
                _workingManuscript.CurrentStage

            If cmbStage.SelectedItem IsNot Nothing Then

                selectedStage =
                    CType(
                        cmbStage.SelectedItem,
                        PaperStage
                    )

            End If

            If selectedStage <> PaperStage.Revision Then
                Return
            End If

            Dim latestSubmission As JournalSubmission =
                ManuscriptAttentionService.GetLatestSubmission(
                    _workingManuscript
                )

            If latestSubmission Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Revision deadlines belong to an editorial decision." &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Add the journal submission first, then use Revision deadline > Set / Edit to record the decision and its deadline.",
                    "Journal Submission Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim latestDecision As EditorialDecisionEvent =
                ManuscriptAttentionService.GetLatestDecision(
                    latestSubmission
                )

            If latestDecision Is Nothing Then

                Using dialog As New AddDecisionForm()

                    If dialog.ShowDialog(Me) <>
                       DialogResult.OK OrElse
                       dialog.CreatedDecision Is Nothing Then

                        Return

                    End If

                    latestSubmission.Decisions.Add(
                        dialog.CreatedDecision
                    )

                    ManuscriptLifecycleService.ApplyDecision(
                        _workingManuscript,
                        latestSubmission,
                        dialog.CreatedDecision
                    )

                End Using

            Else

                Using dialog As New AddDecisionForm(
                    latestDecision
                )

                    If dialog.ShowDialog(Me) <>
                       DialogResult.OK OrElse
                       dialog.CreatedDecision Is Nothing Then

                        Return

                    End If

                    Dim updated As EditorialDecisionEvent =
                        dialog.CreatedDecision

                    For decisionIndex As Integer = 0 To latestSubmission.Decisions.Count - 1

                        If latestSubmission.Decisions(decisionIndex).Id =
                           latestDecision.Id Then

                            latestSubmission.Decisions(decisionIndex) =
                                updated

                            Exit For

                        End If

                    Next

                    ManuscriptLifecycleService.ApplyDecision(
                        _workingManuscript,
                        latestSubmission,
                        updated
                    )

                End Using

            End If

            RefreshLifecycleControls()
            RefreshSubmissionList()
            RefreshRevisionDeadlineDisplay()

        End Sub


        ' =====================================================
        ' DOI / Crossref metadata
        ' =====================================================

        Private Sub OpenCrossrefMetadata(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New CrossrefLookupForm(
                _workingManuscript,
                _authorLibrary
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                If dialog.LibraryChanged Then

                    _authorLibraryDirty =
                        True

                End If

            End Using

            txtTitle.Text =
                _workingManuscript.Title

            RefreshAuthorsList()

        End Sub


        ' =====================================================
        ' Structured authors
        ' =====================================================

        Private Sub RefreshAuthorsList()

            lstAuthors.Items.Clear()

            If _workingManuscript.Authors Is Nothing Then

                _workingManuscript.Authors =
                    New List(Of ManuscriptAuthor)()

            End If

            For i As Integer = 0 To _workingManuscript.Authors.Count - 1

                Dim authorLink As ManuscriptAuthor =
                    _workingManuscript.Authors(i)

                lstAuthors.Items.Add(
                    FormatAuthorLink(
                        i,
                        authorLink
                    )
                )

            Next

            If _workingManuscript.Authors.Count = 0 Then

                lblAuthorInfo.Text =
                    "No authors assigned yet. Use Add Author to build the manuscript author list."

            Else

                lblAuthorInfo.Text =
                    _workingManuscript.Authors.Count.ToString() &
                    " structured author(s). Order is manuscript-specific."

            End If

            UpdateAuthorButtons()

        End Sub


        Private Function FormatAuthorLink(
            index As Integer,
            authorLink As ManuscriptAuthor
        ) As String

            Dim author As AuthorRecord =
                FindAuthor(
                    authorLink.AuthorId
                )

            Dim authorName As String =
                If(
                    author Is Nothing,
                    "[Missing author record]",
                    author.DisplayName
                )

            Dim result As String =
                (index + 1).ToString() &
                ". " &
                authorName

            If author IsNot Nothing AndAlso
               author.IsMe Then

                result &=
                    "  •  Me"

            End If

            If authorLink.IsCorrespondingAuthor Then

                result &=
                    "  •  Corresponding"

            End If

            Dim affiliationNames As New List(Of String)()

            If authorLink.AffiliationIds IsNot Nothing Then

                For Each affiliationId As Guid In
                    authorLink.AffiliationIds

                    Dim affiliation As AffiliationRecord =
                        FindAffiliation(
                            affiliationId
                        )

                    If affiliation IsNot Nothing Then

                        affiliationNames.Add(
                            affiliation.DisplayName
                        )

                    End If

                Next

            End If

            If affiliationNames.Count > 0 Then

                result &=
                    "  —  " &
                    String.Join(
                        "; ",
                        affiliationNames
                    )

            End If

            Return result

        End Function


        Private Function FindAuthor(
            authorId As Guid
        ) As AuthorRecord

            Return _authorLibrary.Authors.
                FirstOrDefault(
                    Function(item)
                        Return item.Id =
                            authorId
                    End Function
                )

        End Function


        Private Function FindAffiliation(
            affiliationId As Guid
        ) As AffiliationRecord

            Return _authorLibrary.Affiliations.
                FirstOrDefault(
                    Function(item)
                        Return item.Id =
                            affiliationId
                    End Function
                )

        End Function


        Private Function GetSelectedAuthorIndex() As Integer

            Dim index As Integer =
                lstAuthors.SelectedIndex

            If index < 0 OrElse
               index >= _workingManuscript.Authors.Count Then

                Return -1

            End If

            Return index

        End Function


        Private Sub AuthorSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateAuthorButtons()

        End Sub


        Private Sub UpdateAuthorButtons()

            Dim index As Integer =
                GetSelectedAuthorIndex()

            Dim hasSelection As Boolean =
                index >= 0

            btnEditAuthor.Enabled =
                hasSelection

            btnRemoveAuthor.Enabled =
                hasSelection

            btnMoveAuthorUp.Enabled =
                hasSelection AndAlso
                index > 0

            btnMoveAuthorDown.Enabled =
                hasSelection AndAlso
                index >= 0 AndAlso
                index < _workingManuscript.Authors.Count - 1

        End Sub


        Private Sub ManageAuthorLibrary(
            sender As Object,
            e As EventArgs
        )

            Dim usageSnapshot As New List(Of Manuscript)()

            For Each manuscript As Manuscript In
                _allManuscripts

                If manuscript.Id =
                   _workingManuscript.Id Then

                    usageSnapshot.Add(
                        _workingManuscript
                    )

                Else

                    usageSnapshot.Add(
                        manuscript
                    )

                End If

            Next

            If Not usageSnapshot.Any(
                Function(item)
                    Return item.Id =
                        _workingManuscript.Id
                End Function
            ) Then

                usageSnapshot.Add(
                    _workingManuscript
                )

            End If

            Using dialog As New AuthorLibraryForm(
                usageSnapshot
            )

                dialog.ShowDialog(
                    Me
                )

            End Using

            _authorLibrary =
                _authorRepository.Load()

            RefreshAuthorsList()

        End Sub


        Private Sub AddStructuredAuthor(
            sender As Object,
            e As EventArgs
        )

            If _authorLibrary.Authors.Count = 0 Then

                ManageAuthorLibrary(
                    sender,
                    e
                )

                If _authorLibrary.Authors.Count = 0 Then
                    Return
                End If

            End If

            Dim usedAuthorIds As IEnumerable(Of Guid) =
                _workingManuscript.Authors.
                    Select(
                        Function(item)
                            Return item.AuthorId
                        End Function
                    )

            Dim usedSet As New HashSet(Of Guid)(
                usedAuthorIds
            )

            If _authorLibrary.Authors.All(
                Function(item)
                    Return usedSet.Contains(
                        item.Id
                    )
                End Function
            ) Then

                MessageBox.Show(
                    Me,
                    "Every author in the reusable library is already assigned to this manuscript.",
                    "No Additional Authors",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using dialog As New ManuscriptAuthorForm(
                _authorLibrary,
                Nothing,
                usedAuthorIds
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                ApplyCorrespondingAuthorRule(
                    dialog.Result,
                    -1
                )

                _workingManuscript.Authors.Add(
                    dialog.Result
                )

                RefreshAuthorsList()

                lstAuthors.SelectedIndex =
                    lstAuthors.Items.Count - 1

            End Using

        End Sub


        Private Sub EditStructuredAuthor(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedAuthorIndex()

            If index < 0 Then
                Return
            End If

            Dim current As ManuscriptAuthor =
                _workingManuscript.Authors(index)

            Dim excludedAuthorIds As IEnumerable(Of Guid) =
                _workingManuscript.Authors.
                    Where(
                        Function(item)
                            Return item IsNot current
                        End Function
                    ).
                    Select(
                        Function(item)
                            Return item.AuthorId
                        End Function
                    )

            Using dialog As New ManuscriptAuthorForm(
                _authorLibrary,
                current,
                excludedAuthorIds
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                ApplyCorrespondingAuthorRule(
                    dialog.Result,
                    index
                )

                _workingManuscript.Authors(index) =
                    dialog.Result

                RefreshAuthorsList()

                lstAuthors.SelectedIndex =
                    index

            End Using

        End Sub


        Private Sub ApplyCorrespondingAuthorRule(
            result As ManuscriptAuthor,
            keepIndex As Integer
        )

            If result Is Nothing OrElse
               Not result.IsCorrespondingAuthor Then

                Return

            End If

            For i As Integer = 0 To _workingManuscript.Authors.Count - 1

                If i = keepIndex Then
                    Continue For
                End If

                _workingManuscript.Authors(i).IsCorrespondingAuthor =
                    False

            Next

        End Sub


        Private Sub RemoveStructuredAuthor(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedAuthorIndex()

            If index < 0 Then
                Return
            End If

            _workingManuscript.Authors.RemoveAt(
                index
            )

            RefreshAuthorsList()

            If lstAuthors.Items.Count > 0 Then

                lstAuthors.SelectedIndex =
                    Math.Min(
                        index,
                        lstAuthors.Items.Count - 1
                    )

            End If

        End Sub


        Private Sub MoveStructuredAuthorUp(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedAuthorIndex()

            If index <= 0 Then
                Return
            End If

            Dim item As ManuscriptAuthor =
                _workingManuscript.Authors(index)

            _workingManuscript.Authors.RemoveAt(
                index
            )

            _workingManuscript.Authors.Insert(
                index - 1,
                item
            )

            RefreshAuthorsList()
            lstAuthors.SelectedIndex =
                index - 1

        End Sub


        Private Sub MoveStructuredAuthorDown(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedAuthorIndex()

            If index < 0 OrElse
               index >= _workingManuscript.Authors.Count - 1 Then

                Return

            End If

            Dim item As ManuscriptAuthor =
                _workingManuscript.Authors(index)

            _workingManuscript.Authors.RemoveAt(
                index
            )

            _workingManuscript.Authors.Insert(
                index + 1,
                item
            )

            RefreshAuthorsList()
            lstAuthors.SelectedIndex =
                index + 1

        End Sub


        ' =====================================================
        ' Submission list
        ' =====================================================

        Private Sub RefreshSubmissionList()

            Dim selectedId As Guid? = GetSelectedSubmission()?.Id

            _refreshingSubmissions = True

            Try

                lstSubmissions.Items.Clear()
                _displayedSubmissions.Clear()

                For Each submission As JournalSubmission In
                    _workingManuscript.Submissions

                    _displayedSubmissions.Add(submission)

                    lstSubmissions.Items.Add(
                        FormatSubmission(submission)
                    )

                Next

                ' Keep the selection; otherwise show the most recent submission.
                Dim index As Integer =
                    If(selectedId.HasValue,
                       _displayedSubmissions.FindIndex(Function(item) item.Id = selectedId.Value),
                       -1)

                If index < 0 AndAlso _displayedSubmissions.Count > 0 Then
                    index = _displayedSubmissions.
                        Select(Function(item, position) New With {item, position}).
                        OrderBy(Function(entry) entry.item.SubmittedDate).
                        Last().position
                End If

                lstSubmissions.SelectedIndex = index

            Finally
                _refreshingSubmissions = False
            End Try

            If _displayedSubmissions.Count = 0 Then

                lblSubmissionInfo.Text =
                    "No journal submissions recorded. Record one when you send the manuscript to a journal."

            Else

                lblSubmissionInfo.Text =
                    "Select a submission to see its decisions, reviewer responses, and correspondence."

            End If

            ShowSubmissionDetail(GetSelectedSubmission())

            UpdateSubmissionButtons()
            RefreshRevisionDeadlineDisplay()

            If versionHistoryControl IsNot Nothing Then
                versionHistoryControl.RefreshVersions()
            End If

            ' A new decision can change what the shortlist offers.
            RefreshShortlist()

        End Sub


        Private Function FormatSubmission(
            submission As JournalSubmission
        ) As String

            Dim result As String =
                submission.SubmittedDate.ToString("MMM d, yyyy") &
                " - " &
                submission.JournalName

            If Not String.IsNullOrWhiteSpace(
                submission.ManuscriptNumber
            ) Then

                result &=
                    " - " &
                    submission.ManuscriptNumber

            End If

            If Not String.IsNullOrWhiteSpace(
                submission.Notes
            ) Then

                result &=
                    " - Notes"

            End If

            If Not String.IsNullOrWhiteSpace(
                submission.PortalUrl
            ) Then

                result &=
                    " - Portal"

            End If

            If submission.FollowUpDate.HasValue AndAlso
               submission.Decisions.Count = 0 Then

                result &=
                    " - Follow-up " &
                    submission.FollowUpDate.Value.ToString(
                        "MMM d"
                    )

            End If

            If submission.Decisions.Count > 0 Then

                result &=
                    " - " &
                    submission.Decisions.Count.ToString() &
                    " decision(s)"

            End If

            If submission.Correspondence.Count > 0 Then

                result &=
                    " - " &
                    submission.Correspondence.Count.ToString() &
                    " file(s)"

            End If

            Return result

        End Function


        Private Function GetSelectedSubmission() As JournalSubmission

            Dim selectedIndex As Integer =
                lstSubmissions.SelectedIndex

            If selectedIndex < 0 OrElse
               selectedIndex >= _displayedSubmissions.Count Then

                Return Nothing

            End If

            Return _displayedSubmissions(selectedIndex)

        End Function


        Private _refreshingSubmissions As Boolean = False


        Private Sub SubmissionSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateSubmissionButtons()

            If Not _refreshingSubmissions Then
                ShowSubmissionDetail(GetSelectedSubmission())
            End If

        End Sub


        ' Shows one submission in the detail pane. Its edits change the working
        ' copy directly and join the manuscript's unsaved changes.
        Private Sub ShowSubmissionDetail(
            submission As JournalSubmission
        )

            _submissionDetailHost.SuspendLayout()

            Try

                For Each child As Control In _submissionDetailHost.Controls.Cast(Of Control)().ToList()
                    _submissionDetailHost.Controls.Remove(child)
                    child.Dispose()
                Next

                _submissionDetail = Nothing

                If submission Is Nothing Then

                    _submissionDetailHost.Controls.Add(
                        New Label With {
                            .Text = "Record a submission when you send this manuscript to a journal. " &
                                    "Its editorial decisions, reviewer responses, and correspondence then appear here.",
                            .Dock = DockStyle.Top,
                            .AutoSize = True,
                            .MaximumSize = New Size(560, 0),
                            .UseMnemonic = False,
                            .ForeColor = SystemColors.GrayText,
                            .Padding = New Padding(4, 8, 4, 4)
                        })

                    Return

                End If

                Dim detail As New SubmissionDetailsForm(
                    _workingManuscript,
                    submission,
                    workflowNavigationEnabled:=True,
                    inline:=True
                ) With {
                    .TopLevel = False,
                    .FormBorderStyle = FormBorderStyle.None,
                    .Dock = DockStyle.Fill,
                    .MinimumSize = Size.Empty
                }

                AddHandler detail.Changed, AddressOf SubmissionDetailChanged

                ' Open the requested window after the detail's own click handler
                ' has returned, since it may select another submission.
                AddHandler detail.NavigationRequested,
                    Sub(request)
                        If IsHandleCreated Then
                            BeginInvoke(New Action(Sub() RunSubmissionWorkflow(request)))
                        Else
                            RunSubmissionWorkflow(request)
                        End If
                    End Sub

                detail.Visible = True
                _submissionDetailHost.Controls.Add(detail)
                _submissionDetail = detail

            Finally
                _submissionDetailHost.ResumeLayout()
            End Try

        End Sub


        ' A decision, correspondence, or reviewer-response edit in the detail
        ' pane: bring the stage, deadline, versions, and list up to date without
        ' rebuilding the pane being edited.
        Private Sub SubmissionDetailChanged(
            sender As Object,
            e As EventArgs
        )

            If cmbStage.SelectedItem Is Nothing OrElse
               CType(cmbStage.SelectedItem, PaperStage) <> _workingManuscript.CurrentStage Then
                cmbStage.SelectedItem = _workingManuscript.CurrentStage
            End If

            RefreshRevisionDeadlineDisplay()

            ' Update the list's accessible text without re-selecting, which
            ' would rebuild the pane being edited.
            _refreshingSubmissions = True
            Try
                For index As Integer = 0 To _displayedSubmissions.Count - 1
                    lstSubmissions.Items(index) = FormatSubmission(_displayedSubmissions(index))
                Next
            Finally
                _refreshingSubmissions = False
            End Try
            lstSubmissions.Invalidate()

            If versionHistoryControl IsNot Nothing Then
                versionHistoryControl.RefreshVersions()
            End If

        End Sub


        Private Sub MeasureSubmissionItem(
            sender As Object,
            e As MeasureItemEventArgs
        )

            e.ItemHeight = lstSubmissions.Font.Height * 3 + 20

        End Sub


        ' Journal, submitted date and ID, and the latest decision.
        Private Sub DrawSubmissionItem(
            sender As Object,
            e As DrawItemEventArgs
        )

            If e.Index < 0 OrElse e.Index >= _displayedSubmissions.Count Then
                Return
            End If

            Dim submission As JournalSubmission = _displayedSubmissions(e.Index)
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Dim bounds As Rectangle = e.Bounds

            Using background As New SolidBrush(If(selected, UiTheme.AccentMutedBackground(), UiTheme.CardBackground()))
                e.Graphics.FillRectangle(background, bounds)
            End Using

            If selected Then
                Using accent As New SolidBrush(UiTheme.AccentColor())
                    e.Graphics.FillRectangle(accent, bounds.Left, bounds.Top, 3, bounds.Height)
                End Using
            End If

            Using divider As New Pen(UiTheme.SubtleBorder())
                e.Graphics.DrawLine(divider, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1)
            End Using

            Dim lineHeight As Integer = lstSubmissions.Font.Height
            Dim left As Integer = bounds.Left + 12
            Dim width As Integer = bounds.Width - 20
            Dim flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine

            Using bold As New Font(lstSubmissions.Font, FontStyle.Bold)
                TextRenderer.DrawText(e.Graphics, submission.JournalName, bold,
                    New Rectangle(left, bounds.Top + 8, width, lineHeight), UiTheme.PrimaryText(), flags)
            End Using

            Dim submitted As String = "Submitted " & submission.SubmittedDate.ToString("MMM d, yyyy")
            If Not String.IsNullOrWhiteSpace(submission.ManuscriptNumber) Then
                submitted &= "  " & ChrW(&HB7) & "  " & submission.ManuscriptNumber
            End If

            TextRenderer.DrawText(e.Graphics, submitted, lstSubmissions.Font,
                New Rectangle(left, bounds.Top + 8 + lineHeight, width, lineHeight), UiTheme.SecondaryText(), flags)

            Dim latest As EditorialDecisionEvent = ManuscriptAttentionService.GetLatestDecision(submission)
            Dim outcome As String
            Dim outcomeColor As Color

            If latest Is Nothing Then
                outcome = If(submission.FollowUpDate.HasValue,
                             "Awaiting decision  " & ChrW(&HB7) & "  follow up " & submission.FollowUpDate.Value.ToString("MMM d"),
                             "Awaiting decision")
                outcomeColor = UiTheme.MutedText()
            Else
                outcome = EditorialDecisionDisplayService.Format(latest.Decision) & "  " & ChrW(&HB7) & "  " & latest.DecisionDate.ToString("MMM d, yyyy")
                outcomeColor =
                    If(ManuscriptAttentionService.IsRejectionDecision(latest.Decision), UiTheme.MutedText(),
                       If(latest.Decision = EditorialDecision.Accepted, UiTheme.SuccessColor(), UiTheme.WarningColor()))
            End If

            Using semibold As New Font(lstSubmissions.Font, FontStyle.Bold)
                TextRenderer.DrawText(e.Graphics, outcome, semibold,
                    New Rectangle(left, bounds.Top + 8 + lineHeight * 2, width, lineHeight), outcomeColor, flags)
            End Using

            e.DrawFocusRectangle()

        End Sub


        Private Sub UpdateSubmissionButtons()

            Dim hasSelection As Boolean =
                GetSelectedSubmission() IsNot Nothing

            btnEditSubmission.Enabled =
                hasSelection

            btnDeleteSubmission.Enabled =
                hasSelection

        End Sub


        ' =====================================================
        ' Add submission
        ' =====================================================

        Private Sub AddSubmission(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New AddSubmissionForm()

                If dialog.ShowDialog(Me) =
                    DialogResult.OK AndAlso
                   dialog.CreatedSubmission IsNot Nothing Then

                    _workingManuscript.Submissions.Add(
                        dialog.CreatedSubmission
                    )

                    ManuscriptLifecycleService.ApplySubmission(
                        _workingManuscript,
                        dialog.CreatedSubmission
                    )

                    RefreshLifecycleControls()
                    RefreshSubmissionList()

                    lstSubmissions.SelectedIndex =
                        lstSubmissions.Items.Count - 1

                End If

            End Using

        End Sub


        ' =====================================================
        ' Edit submission
        ' =====================================================

        Private Sub EditSelectedSubmission(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalSubmission =
                GetSelectedSubmission()

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New AddSubmissionForm(selected)

                If dialog.ShowDialog(Me) <>
                    DialogResult.OK OrElse
                   dialog.CreatedSubmission Is Nothing Then

                    Return

                End If

                Dim updated As JournalSubmission =
                    dialog.CreatedSubmission

                Try
                    SubmissionReadinessValidationService.ValidateSubmissionJournalAssociations(
                        _workingManuscript,
                        updated
                    )
                Catch ex As System.IO.InvalidDataException
                    MessageBox.Show(
                        Me,
                        ex.Message,
                        "Submission Used by Packet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )
                    Return
                End Try

                For i As Integer = 0 To _workingManuscript.Submissions.Count - 1

                    If _workingManuscript.Submissions(i).Id =
                        selected.Id Then

                        _workingManuscript.Submissions(i) =
                            updated

                        Exit For

                    End If

                Next

                ManuscriptLifecycleService.
                    ReconcileFromLatestWorkflow(
                        _workingManuscript,
                        allowSameDay:=True
                    )

                RefreshLifecycleControls()
                RefreshSubmissionList()

                For i As Integer = 0 To _displayedSubmissions.Count - 1

                    If _displayedSubmissions(i).Id =
                        updated.Id Then

                        lstSubmissions.SelectedIndex =
                            i

                        Exit For

                    End If

                Next

            End Using

        End Sub


        ' =====================================================
        ' Delete submission
        ' =====================================================

        Private Sub DeleteSelectedSubmission(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalSubmission =
                GetSelectedSubmission()

            If selected Is Nothing Then
                Return
            End If

            If _workingManuscript.SubmissionPackets IsNot Nothing AndAlso
               _workingManuscript.SubmissionPackets.Any(
                   Function(packet)
                       Return packet IsNot Nothing AndAlso
                           packet.SubmissionId.HasValue AndAlso
                           packet.SubmissionId.Value = selected.Id
                   End Function
               ) Then

                MessageBox.Show(
                    Me,
                    "This submission is linked to a Submission Packet. Open Submission Packets and unlink or delete the packet before deleting this submission.",
                    "Submission Used by Packet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim warning As String =
                "Delete the submission to '" &
                selected.JournalName &
                "'?" &
                Environment.NewLine &
                Environment.NewLine &
                "This will also remove:" &
                Environment.NewLine &
                "- " &
                selected.Decisions.Count.ToString() &
                " editorial decision(s)" &
                Environment.NewLine &
                "- " &
                selected.Correspondence.Count.ToString() &
                " correspondence/file record(s)"

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    warning,
                    "Delete Journal Submission",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <>
                DialogResult.Yes Then

                Return

            End If

            _workingManuscript.Submissions.Remove(
                selected
            )

            ManuscriptLifecycleService.
                ReconcileAfterSubmissionRemoval(
                    _workingManuscript,
                    selected
                )

            RefreshLifecycleControls()
            RefreshSubmissionList()

        End Sub


        ' =====================================================
        ' Delete manuscript
        ' =====================================================

        Private Sub RequestDelete(
            sender As Object,
            e As EventArgs
        )

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    "Delete '" &
                    _workingManuscript.Title &
                    "' from PaperRoute?" &
                    Environment.NewLine &
                    Environment.NewLine &
                    "The complete manuscript record will be removed.",
                    "Delete Manuscript",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <>
                DialogResult.Yes Then

                Return

            End If

            _deleteRequested =
                True

            If _pageMode Then

                RaiseEvent DeleteConfirmed(
                    Me,
                    EventArgs.Empty
                )

            Else

                Me.DialogResult =
                    DialogResult.Abort

            End If

        End Sub


        ' =====================================================
        ' Journal / preprint / project links
        ' =====================================================

        Private Sub OpenJournalLinks(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New ManuscriptLinksForm(
                _workingManuscript,
                _allManuscripts
            )

                If dialog.ShowDialog(Me) =
                   DialogResult.OK Then

                    txtTargetJournal.Text =
                        _workingManuscript.TargetJournal

                    _authorLibrary =
                        _authorRepository.Load()

                    RefreshJournalNotes()

                End If

            End Using

        End Sub


        Private Function RecordRequiredSubmission() As Boolean

            Using dialog As New AddSubmissionForm(
                txtTargetJournal.Text.Trim()
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.CreatedSubmission Is Nothing Then

                    Return False

                End If

                _workingManuscript.Submissions.Add(
                    dialog.CreatedSubmission
                )

                ManuscriptLifecycleService.ApplySubmission(
                    _workingManuscript,
                    dialog.CreatedSubmission
                )

            End Using

            RefreshLifecycleControls()
            RefreshSubmissionList()

            If lstSubmissions.Items.Count > 0 Then

                lstSubmissions.SelectedIndex =
                    lstSubmissions.Items.Count - 1

            End If

            Return True

        End Function


        Private Function EnsureWorkflowForRequestedStage(
            requestedStage As PaperStage
        ) As Boolean

            If ManuscriptStagePolicyService.IsStageSupported(
                _workingManuscript,
                requestedStage
            ) Then

                Return True

            End If

            Dim requirement As ManuscriptStageWorkflowRequirement =
                ManuscriptStagePolicyService.GetRequirement(
                    requestedStage
                )

            Select Case requirement

                Case ManuscriptStageWorkflowRequirement.ActiveSubmission

                    Dim recordSubmission As DialogResult =
                        MessageBox.Show(
                            Me,
                            "PaperRoute ties " &
                            FormatStageForPrompt(requestedStage) &
                            " to an active Journal Submission record." &
                            Environment.NewLine &
                            Environment.NewLine &
                            "Record the submission details now?",
                            "Submission Details Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        )

                    If recordSubmission <>
                       DialogResult.Yes Then

                        Return False

                    End If

                    If Not RecordRequiredSubmission() Then
                        Return False
                    End If

                Case ManuscriptStageWorkflowRequirement.RevisionDecision,
                     ManuscriptStageWorkflowRequirement.AcceptanceDecision

                    Dim latestSubmission As JournalSubmission =
                        ManuscriptAttentionService.
                            GetLatestSubmission(
                                _workingManuscript
                            )

                    If latestSubmission Is Nothing Then

                        Dim recordSubmission As DialogResult =
                            MessageBox.Show(
                                Me,
                                "This stage requires an editorial decision " &
                                "attached to a Journal Submission." &
                                Environment.NewLine &
                                Environment.NewLine &
                                "Record the submission first?",
                                "Submission Required",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information
                            )

                        If recordSubmission <>
                           DialogResult.Yes OrElse
                           Not RecordRequiredSubmission() Then

                            Return False

                        End If

                        latestSubmission =
                            ManuscriptAttentionService.
                                GetLatestSubmission(
                                    _workingManuscript
                                )

                    End If

                    If latestSubmission Is Nothing Then
                        Return False
                    End If

                    Dim decisionPrompt As String

                    If requirement =
                       ManuscriptStageWorkflowRequirement.RevisionDecision Then

                        decisionPrompt =
                            "Revision is driven by a Major Revision, " &
                            "Minor Revision, or Revise & Resubmit decision."

                    Else

                        decisionPrompt =
                            FormatStageForPrompt(requestedStage) &
                            " requires an Accepted editorial decision."

                    End If

                    Dim openSubmission As DialogResult =
                        MessageBox.Show(
                            Me,
                            decisionPrompt &
                            Environment.NewLine &
                            Environment.NewLine &
                            "Open the latest submission to record or edit " &
                            "the editorial decision now?",
                            "Editorial Decision Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        )

                    If openSubmission <>
                       DialogResult.Yes Then

                        Return False

                    End If

                    Using dialog As New SubmissionDetailsForm(
                        _workingManuscript,
                        latestSubmission
                    )

                        dialog.ShowDialog(
                            Me
                        )

                    End Using

                    ManuscriptLifecycleService.
                        ReconcileFromLatestWorkflow(
                            _workingManuscript,
                            allowSameDay:=True
                        )

                    ManuscriptLifecycleService.
                        ReconcileAfterWorkflowMutation(
                            _workingManuscript
                        )

                    RefreshLifecycleControls()
                    RefreshSubmissionList()

            End Select

            If ManuscriptStagePolicyService.IsStageSupported(
                _workingManuscript,
                requestedStage
            ) Then

                Return True

            End If

            MessageBox.Show(
                Me,
                "PaperRoute did not find " &
                ManuscriptStagePolicyService.RequirementDescription(
                    requirement
                ) &
                ", so the current stage was not changed.",
                "Stage Not Changed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

            Return False

        End Function


        Private Function FormatStageForPrompt(
            stage As PaperStage
        ) As String

            Select Case stage

                Case PaperStage.UnderReview
                    Return "Under Review"

                Case PaperStage.InPress
                    Return "In Press"

                Case Else
                    Return stage.ToString()

            End Select

        End Function


        ' =====================================================
        ' Save working copy
        ' =====================================================

        Private Sub SaveChanges(
            sender As Object,
            e As EventArgs
        )

            If CommitChanges() Then

                Me.DialogResult =
                    DialogResult.OK

            End If

        End Sub


        ' Validates the fields, applies them and any stage change to the working
        ' copy, saves new reusable authors, and copies the working copy to the
        ' manuscript being edited. The caller saves the library. Returns False,
        ' changing nothing, when a field needs attention or a stage change was
        ' not completed.
        Friend Function CommitChanges() As Boolean

            If String.IsNullOrWhiteSpace(
                txtTitle.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Please enter a manuscript title.",
                    "Title Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()

                Return False

            End If

            If cmbStage.SelectedItem Is Nothing Then
                Return False
            End If

            Dim oldStage As PaperStage =
                _workingManuscript.CurrentStage

            Dim newStage As PaperStage =
                CType(
                    cmbStage.SelectedItem,
                    PaperStage
                )

            If oldStage <> newStage Then

                If Not EnsureWorkflowForRequestedStage(
                    newStage
                ) Then

                    cmbStage.SelectedItem =
                        _workingManuscript.CurrentStage

                    RefreshRevisionDeadlineDisplay()

                    Return False

                End If

                oldStage =
                    _workingManuscript.CurrentStage

            End If

            If newStage = PaperStage.Published Then

                _workingManuscript.Location =
        ManuscriptLocation.Published

            ElseIf _workingManuscript.Location =
    ManuscriptLocation.Published Then

                _workingManuscript.Location =
        ManuscriptLocation.Pipeline

            End If

            _workingManuscript.Title =
                txtTitle.Text.Trim()

            Dim oldTargetJournalText As String =
                If(
                    _workingManuscript.TargetJournal,
                    String.Empty
                ).Trim()

            Dim newTargetJournalText As String =
                txtTargetJournal.Text.Trim()

            If _workingManuscript.TargetJournalId.HasValue AndAlso
               Not String.Equals(
                   oldTargetJournalText,
                   newTargetJournalText,
                   StringComparison.CurrentCultureIgnoreCase
               ) Then

                Dim linkedJournal As JournalRecord =
                    _authorLibrary.Journals.
                        FirstOrDefault(
                            Function(item)
                                Return item IsNot Nothing AndAlso
                                    item.Id =
                                        _workingManuscript.TargetJournalId.Value
                            End Function
                        )

                If linkedJournal Is Nothing OrElse
                   Not String.Equals(
                       linkedJournal.Name,
                       newTargetJournalText,
                       StringComparison.CurrentCultureIgnoreCase
                   ) Then

                    _workingManuscript.TargetJournalId =
                        Nothing

                End If

            End If

            _workingManuscript.TargetJournal =
                newTargetJournalText

            If oldStage <> newStage Then

                _workingManuscript.CurrentStage =
                    newStage

                _workingManuscript.StageEnteredDate =
                    DateTime.Now

                Dim stageHistory As New HistoryEvent With {
                    .EventDate =
                        _workingManuscript.StageEnteredDate,
                    .Stage = newStage,
                    .Note =
                        "Stage changed from " &
                        oldStage.ToString() &
                        " to " &
                        newStage.ToString() &
                        "."
                }

                ChronologyProvenanceService.StampCreated(
                    stageHistory
                )

                _workingManuscript.History.Add(
                    stageHistory
                )

            Else

                _workingManuscript.CurrentStage =
                    newStage

            End If

            UpdateFileDrawerReasonIfNeeded()

            If _authorLibraryDirty Then

                ' Tags and people only; journals stay as the Journals page saved them.
                _authorRepository.SaveKeepingJournals(
                    _authorLibrary
                )

                _authorLibraryDirty =
                    False

            End If

            CopyWorkingToOriginal()

            _baseline =
                Snapshot()

            Return True

        End Function


        Private Sub UpdateFileDrawerReasonIfNeeded()

            If Not fileDrawerGroup.Visible Then
                Return
            End If

            Dim oldReason As String =
                If(
                    _workingManuscript.FileDrawerReason,
                    String.Empty
                ).Trim()

            Dim newReason As String =
                txtFileDrawerReason.Text.Trim()

            If String.Equals(
                oldReason,
                newReason,
                StringComparison.Ordinal
            ) Then

                Return

            End If

            _workingManuscript.FileDrawerReason =
                newReason

            Dim note As String

            If String.IsNullOrWhiteSpace(newReason) Then

                note =
                    "File Drawer reason cleared."

            ElseIf String.IsNullOrWhiteSpace(oldReason) Then

                note =
                    "File Drawer reason added. Reason: " &
                    newReason

            Else

                note =
                    "File Drawer reason updated. Reason: " &
                    newReason

            End If

            Dim reasonHistory As New HistoryEvent With {
                .EventDate = DateTime.Now,
                .Stage =
                    _workingManuscript.CurrentStage,
                .Note =
                    note
            }

            ChronologyProvenanceService.StampCreated(
                reasonHistory
            )

            _workingManuscript.History.Add(
                reasonHistory
            )

        End Sub


        ' =====================================================
        ' Clone manuscript
        ' =====================================================

        Private Function CloneManuscript(
            source As Manuscript
        ) As Manuscript

            Return ManuscriptCloneService.CloneManuscript(
                source
            )

        End Function


        Private Function CloneSubmission(
            source As JournalSubmission
        ) As JournalSubmission
            Return ManuscriptCloneService.CloneSubmission(source)
        End Function


        ' =====================================================
        ' Commit working copy
        ' =====================================================

        Private Sub CopyWorkingToOriginal()

            Dim committed As Manuscript =
                CloneManuscript(
                    _workingManuscript
                )

            _originalManuscript.Id =
                committed.Id

            _originalManuscript.Title =
                committed.Title

            _originalManuscript.CoAuthors =
                committed.CoAuthors

            _originalManuscript.Authors =
                committed.Authors

            _originalManuscript.TargetJournal =
                committed.TargetJournal

            _originalManuscript.TargetJournalId =
                committed.TargetJournalId

            _originalManuscript.ManuscriptUrl =
                committed.ManuscriptUrl

            _originalManuscript.Metadata =
                committed.Metadata

            _originalManuscript.WorkType =
                committed.WorkType

            _originalManuscript.Tags =
                committed.Tags

            _originalManuscript.JournalShortlist =
                committed.JournalShortlist

            _originalManuscript.RelatedLinks =
                committed.RelatedLinks

            _originalManuscript.Reminders =
                committed.Reminders

            _originalManuscript.Versions =
                committed.Versions

            _originalManuscript.CurrentVersionId =
                committed.CurrentVersionId

            CopyReadinessStateToOriginal(
                committed
            )

            _originalManuscript.CurrentStage =
                committed.CurrentStage

            _originalManuscript.Location =
                committed.Location

            _originalManuscript.StageEnteredDate =
                committed.StageEnteredDate

            _originalManuscript.RevisionDeadline =
                committed.RevisionDeadline

            _originalManuscript.FileDrawerDate =
                committed.FileDrawerDate

            _originalManuscript.FileDrawerReason =
                committed.FileDrawerReason

            _originalManuscript.History =
                committed.History

            _originalManuscript.Submissions =
                committed.Submissions

        End Sub

    End Class

End Namespace
