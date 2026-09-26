Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class EditManuscriptForm
        Inherits Form

        Private ReadOnly _originalManuscript As Manuscript
        Private ReadOnly _workingManuscript As Manuscript
        Private ReadOnly _allManuscripts As List(Of Manuscript)

        Private ReadOnly _authorRepository As New AuthorLibraryRepository()
        Private _authorLibrary As AuthorLibraryData

        Private _deleteRequested As Boolean = False

        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtTargetJournal As New TextBox()
        Private ReadOnly cmbStage As New ComboBox()
        Private ReadOnly btnMetadata As New Button()
        Private ReadOnly btnJournalLinks As New Button()
        Private ReadOnly lblRevisionDeadlineValue As New Label()
        Private ReadOnly btnRevisionDeadline As New Button()
        Private _authorLibraryDirty As Boolean = False

        Private ReadOnly fileDrawerGroup As New GroupBox()
        Private ReadOnly lblFileDrawerDateValue As New Label()
        Private ReadOnly txtFileDrawerReason As New TextBox()

        Private ReadOnly lstAuthors As New ListBox()
        Private ReadOnly btnEditAuthor As New Button()
        Private ReadOnly btnRemoveAuthor As New Button()
        Private ReadOnly btnMoveAuthorUp As New Button()
        Private ReadOnly btnMoveAuthorDown As New Button()
        Private ReadOnly lblAuthorInfo As New Label()

        Private ReadOnly lstSubmissions As New ListBox()

        Private ReadOnly btnViewSubmission As New Button()
        Private ReadOnly btnEditSubmission As New Button()
        Private ReadOnly btnDeleteSubmission As New Button()

        Private ReadOnly lblSubmissionInfo As New Label()

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
            UiPolish.ApplyDialog(Me)
            LoadManuscript()

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

            ApplyResponsiveInitialSize()

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
                        FindGroupBoxByText(
                            Me,
                            "Journal Submissions"
                        )
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

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(20, 20, 20, 12)
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 330))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 260))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 285))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 250))

            ' =================================================
            ' Manuscript metadata
            ' =================================================

            Dim detailsGroup As New GroupBox With {
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
                New ColumnStyle(SizeType.Absolute, 145)
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
            txtTargetJournal.Dock = DockStyle.Fill

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

            details.Controls.Add(CreateFieldLabel("Journal tools"), 0, 5)
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
                New ColumnStyle(SizeType.Absolute, 145)
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

            Dim authorsGroup As New GroupBox With {
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

            Dim submissionsGroup As New GroupBox With {
                .Text = "Journal Submissions",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14)
            }

            Dim submissionsLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2
            }

            submissionsLayout.RowStyles.Add(
                New RowStyle(SizeType.Absolute, 92)
            )

            submissionsLayout.RowStyles.Add(
                New RowStyle(SizeType.Percent, 100)
            )

            Dim submissionToolbar As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = False,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(0, 2, 0, 4),
                .Margin = New Padding(0)
            }

            submissionToolbar.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            submissionToolbar.RowStyles.Add(
                New RowStyle(
                    SizeType.Absolute,
                    34
                )
            )

            submissionToolbar.RowStyles.Add(
                New RowStyle(
                    SizeType.Absolute,
                    48
                )
            )

            lblSubmissionInfo.AutoSize = True
            lblSubmissionInfo.Anchor = AnchorStyles.Left
            lblSubmissionInfo.ForeColor = SystemColors.GrayText

            Dim submissionButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = False,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Margin = New Padding(0, 4, 0, 0),
                .Padding = New Padding(0)
            }

            btnViewSubmission.Text = "View"
            btnViewSubmission.AutoSize = True
            btnViewSubmission.Height = 36
            btnViewSubmission.Enabled = False

            btnEditSubmission.Text = "Edit Submission"
            btnEditSubmission.AutoSize = True
            btnEditSubmission.Height = 36
            btnEditSubmission.Enabled = False

            btnDeleteSubmission.Text = "Delete Submission"
            btnDeleteSubmission.AutoSize = True
            btnDeleteSubmission.Height = 36
            btnDeleteSubmission.Enabled = False

            Dim btnAddSubmission As New Button With {
                .Text = "Add Submission",
                .AutoSize = True,
                .Height = 36
            }

            AddHandler btnViewSubmission.Click,
                AddressOf ViewSelectedSubmission

            AddHandler btnEditSubmission.Click,
                AddressOf EditSelectedSubmission

            AddHandler btnDeleteSubmission.Click,
                AddressOf DeleteSelectedSubmission

            AddHandler btnAddSubmission.Click,
                AddressOf AddSubmission

            submissionButtons.Controls.Add(btnViewSubmission)
            submissionButtons.Controls.Add(btnEditSubmission)
            submissionButtons.Controls.Add(btnDeleteSubmission)
            submissionButtons.Controls.Add(btnAddSubmission)

            submissionToolbar.Controls.Add(lblSubmissionInfo, 0, 0)
            submissionToolbar.Controls.Add(submissionButtons, 0, 1)

            lstSubmissions.Dock = DockStyle.Fill
            lstSubmissions.IntegralHeight = False

            AddHandler lstSubmissions.SelectedIndexChanged,
                AddressOf SubmissionSelectionChanged

            AddHandler lstSubmissions.DoubleClick,
                AddressOf ViewSelectedSubmission

            submissionsLayout.Controls.Add(submissionToolbar, 0, 0)
            submissionsLayout.Controls.Add(lstSubmissions, 0, 1)

            submissionsGroup.Controls.Add(submissionsLayout)

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
            root.Controls.Add(fileDrawerGroup, 0, 1)
            root.Controls.Add(authorsGroup, 0, 2)
            root.Controls.Add(versionHistoryControl, 0, 3)
            root.Controls.Add(submissionsGroup, 0, 4)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            scrollHost.Controls.Add(root)

            Dim sectionNav As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(20, 7, 20, 5),
                .Margin = New Padding(0)
            }

            Dim lblSections As New Label With {
                .Text = "Sections:",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0, 8, 8, 0)
            }

            Dim btnJumpManuscript As New Button With {
                .Text = "Manuscript",
                .AutoSize = True,
                .Height = 32
            }

            Dim btnJumpAuthors As New Button With {
                .Text = "Authors",
                .AutoSize = True,
                .Height = 32
            }

            Dim btnJumpVersions As New Button With {
                .Text = "Version History",
                .AutoSize = True,
                .Height = 32
            }

            Dim btnJumpSubmissions As New Button With {
                .Text = "Journal Submissions",
                .AutoSize = True,
                .Height = 32
            }

            AddHandler btnJumpManuscript.Click,
                Sub(sender, e)
                    scrollHost.ScrollControlIntoView(
                        detailsGroup
                    )
                End Sub

            AddHandler btnJumpAuthors.Click,
                Sub(sender, e)
                    scrollHost.ScrollControlIntoView(
                        authorsGroup
                    )
                End Sub

            AddHandler btnJumpVersions.Click,
                Sub(sender, e)
                    scrollHost.ScrollControlIntoView(
                        versionHistoryControl
                    )
                End Sub

            AddHandler btnJumpSubmissions.Click,
                Sub(sender, e)
                    scrollHost.ScrollControlIntoView(
                        submissionsGroup
                    )
                End Sub

            sectionNav.Controls.Add(
                lblSections
            )

            sectionNav.Controls.Add(
                btnJumpManuscript
            )

            sectionNav.Controls.Add(
                btnJumpAuthors
            )

            sectionNav.Controls.Add(
                btnJumpVersions
            )

            sectionNav.Controls.Add(
                btnJumpSubmissions
            )

            shell.Controls.Add(
                sectionNav,
                0,
                0
            )

            shell.Controls.Add(
                scrollHost,
                0,
                1
            )

            shell.Controls.Add(
                footer,
                0,
                2
            )

            Me.Controls.Add(
                shell
            )

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

            lstSubmissions.Items.Clear()
            _displayedSubmissions.Clear()

            For Each submission As JournalSubmission In
                _workingManuscript.Submissions

                _displayedSubmissions.Add(submission)

                lstSubmissions.Items.Add(
                    FormatSubmission(submission)
                )

            Next

            If _displayedSubmissions.Count = 0 Then

                lblSubmissionInfo.Text =
                    "No journal submissions recorded. Add one to begin."

            Else

                lblSubmissionInfo.Text =
                    "Select a submission to view, edit, or delete it."

            End If

            UpdateSubmissionButtons()
            RefreshRevisionDeadlineDisplay()

            If versionHistoryControl IsNot Nothing Then
                versionHistoryControl.RefreshVersions()
            End If

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


        Private Sub SubmissionSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateSubmissionButtons()

        End Sub


        Private Sub UpdateSubmissionButtons()

            Dim hasSelection As Boolean =
                GetSelectedSubmission() IsNot Nothing

            btnViewSubmission.Enabled =
                hasSelection

            btnEditSubmission.Enabled =
                hasSelection

            btnDeleteSubmission.Enabled =
                hasSelection

        End Sub


        ' =====================================================
        ' View submission
        ' =====================================================

        Private Sub ViewSelectedSubmission(
            sender As Object,
            e As EventArgs
        )

            Dim submission As JournalSubmission =
                GetSelectedSubmission()

            If submission Is Nothing Then
                Return
            End If

            RunSubmissionWorkflow(New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Submission, .SubmissionId = submission.Id
            })

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

            Me.DialogResult =
                DialogResult.Abort

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

                Return

            End If

            If cmbStage.SelectedItem Is Nothing Then
                Return
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

                    Return

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

                _authorRepository.Save(
                    _authorLibrary
                )

                _authorLibraryDirty =
                    False

            End If

            CopyWorkingToOriginal()

            Me.DialogResult =
                DialogResult.OK

        End Sub


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
