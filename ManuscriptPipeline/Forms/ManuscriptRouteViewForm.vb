Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptRouteViewForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _route As ManuscriptRoute


        Public Sub New(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            _manuscript =
                manuscript

            _route =
                ManuscriptRouteProjectionService.Project(
                    manuscript
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Route View - " &
                If(
                    String.IsNullOrWhiteSpace(_manuscript.Title),
                    "Untitled manuscript",
                    _manuscript.Title
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    1040,
                    800
                )

            Me.MinimumSize =
                New Size(
                    780,
                    600
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Me.BackColor =
                UiTheme.BoardBackground()

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(0),
                .BackColor = UiTheme.BoardBackground()
            }

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.Absolute,
                    62
                )
            )

            root.Controls.Add(
                BuildHeader(),
                0,
                0
            )

            root.Controls.Add(
                BuildRouteBody(),
                0,
                1
            )

            root.Controls.Add(
                BuildFooter(),
                0,
                2
            )

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function BuildHeader() As Control

            Dim header As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(24, 20, 24, 16),
                .Margin = New Padding(0),
                .BackColor = UiTheme.HeaderBackground()
            }

            For index As Integer = 0 To 3
                header.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )
            Next

            Dim lblEyebrow As New Label With {
                .Text = "MANUSCRIPT ROUTE",
                .AutoSize = True,
                .Margin = New Padding(0),
                .Font = New Font(
                    Me.Font.FontFamily,
                    9.0F,
                    FontStyle.Bold
                ),
                .ForeColor = UiTheme.AccentColor()
            }

            Dim lblTitle As New Label With {
                .Text =
                    If(
                        String.IsNullOrWhiteSpace(_manuscript.Title),
                        "Untitled manuscript",
                        _manuscript.Title
                    ),
                .AutoSize = True,
                .MaximumSize = New Size(640, 0),
                .Margin = New Padding(0, 4, 0, 0),
                .Font = New Font(
                    Me.Font.FontFamily,
                    17.0F,
                    FontStyle.Bold
                ),
                .ForeColor = UiTheme.PrimaryText()
            }

            Dim lblSummary As New Label With {
                .Text = BuildSummaryText(),
                .AutoSize = True,
                .MaximumSize = New Size(640, 0),
                .Margin = New Padding(0, 8, 0, 0),
                .ForeColor = UiTheme.SecondaryText()
            }

            Dim lblHint As New Label With {
                .Text =
                    "PaperRoute builds this view only from stored manuscript history, submissions, decisions, versions, and current state.",
                .AutoSize = True,
                .MaximumSize = New Size(640, 0),
                .Margin = New Padding(0, 10, 0, 0),
                .ForeColor = UiTheme.SecondaryText()
            }

            header.Controls.Add(
                lblEyebrow,
                0,
                0
            )

            header.Controls.Add(
                lblTitle,
                0,
                1
            )

            header.Controls.Add(
                lblSummary,
                0,
                2
            )

            header.Controls.Add(
                lblHint,
                0,
                3
            )

            Dim resizeHeaderText As Action =
                Sub()

                    Dim availableWidth As Integer =
                        Math.Max(
                            220,
                            Me.ClientSize.Width -
                            header.Padding.Horizontal -
                            12
                        )

                    lblTitle.MaximumSize =
                        New Size(
                            availableWidth,
                            0
                        )

                    lblSummary.MaximumSize =
                        New Size(
                            availableWidth,
                            0
                        )

                    lblHint.MaximumSize =
                        New Size(
                            availableWidth,
                            0
                        )

                    header.PerformLayout()

                End Sub

            AddHandler Me.ClientSizeChanged,
                Sub(sender, e)
                    resizeHeaderText()
                End Sub

            AddHandler Me.Shown,
                Sub(sender, e)
                    resizeHeaderText()
                End Sub

            resizeHeaderText()

            Return header

        End Function


        Private Function BuildRouteBody() As Control

            Dim scrollHost As New Panel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .Padding = New Padding(18, 14, 18, 18),
                .BackColor = UiTheme.BoardBackground()
            }

            Dim timeline As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 3,
                .RowCount = Math.Max(
                    1,
                    _route.Waypoints.Count
                ),
                .Margin = New Padding(0),
                .BackColor = UiTheme.BoardBackground()
            }

            timeline.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    112
                )
            )

            timeline.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    46
                )
            )

            timeline.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            Dim fitTimelineToViewport As Action =
                Sub()

                    Dim availableWidth As Integer =
                        Math.Max(
                            280,
                            scrollHost.ClientSize.Width -
                            scrollHost.Padding.Horizontal -
                            SystemInformation.VerticalScrollBarWidth -
                            6
                        )

                    timeline.MinimumSize =
                        New Size(
                            availableWidth,
                            0
                        )

                    timeline.MaximumSize =
                        New Size(
                            availableWidth,
                            0
                        )

                    timeline.Width =
                        availableWidth

                End Sub

            AddHandler scrollHost.ClientSizeChanged,
                Sub(sender, e)
                    fitTimelineToViewport()
                End Sub

            If _route.Waypoints.Count = 0 Then

                timeline.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )

                Dim emptyLabel As New Label With {
                    .Text = "No route data is available for this manuscript.",
                    .AutoSize = True,
                    .Padding = New Padding(12),
                    .ForeColor = UiTheme.SecondaryText()
                }

                timeline.Controls.Add(
                    emptyLabel,
                    2,
                    0
                )

                scrollHost.Controls.Add(
                    timeline
                )

                fitTimelineToViewport()

                Return scrollHost

            End If

            For index As Integer = 0 To _route.Waypoints.Count - 1

                timeline.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )

                Dim waypoint As ManuscriptRouteWaypoint =
                    _route.Waypoints(index)

                Dim dateLabel As Label =
                    CreateDateLabel(
                        waypoint
                    )

                Dim marker As New RouteMarkerControl With {
                    .Dock = DockStyle.Fill,
                    .Margin = New Padding(0),
                    .MarkerColor = WaypointAccentColor(waypoint),
                    .DrawLineAbove = index > 0,
                    .DrawLineBelow =
                        index < _route.Waypoints.Count - 1
                }

                Dim card As Control =
                    CreateWaypointCard(
                        waypoint
                    )

                timeline.Controls.Add(
                    dateLabel,
                    0,
                    index
                )

                timeline.Controls.Add(
                    marker,
                    1,
                    index
                )

                timeline.Controls.Add(
                    card,
                    2,
                    index
                )

            Next

            scrollHost.Controls.Add(
                timeline
            )

            fitTimelineToViewport()

            Return scrollHost

        End Function


        Private Function BuildFooter() As Control

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = False,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(18, 10, 18, 10),
                .Margin = New Padding(0),
                .BackColor = UiTheme.HeaderBackground()
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .MinimumSize = New Size(96, 36),
                .DialogResult = DialogResult.OK
            }

            footer.Controls.Add(
                btnClose
            )

            Me.AcceptButton =
                btnClose

            Me.CancelButton =
                btnClose

            Return footer

        End Function


        Private Function CreateDateLabel(
            waypoint As ManuscriptRouteWaypoint
        ) As Label

            ' Route chronology presents the real-world event date only.
            ' RecordedAtUtc and LastModifiedAtUtc are audit metadata, not
            ' scholarly-history timestamps, and therefore stay out of the
            ' normal timeline display.
            Return New Label With {
                .Text =
                    waypoint.EventDate.ToString(
                        "MMM d, yyyy"
                    ),
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .TextAlign = ContentAlignment.TopRight,
                .Padding = New Padding(0, 12, 8, 0),
                .Margin = New Padding(0),
                .ForeColor = UiTheme.SecondaryText(),
                .Font = New Font(
                    Me.Font.FontFamily,
                    9.0F,
                    FontStyle.Regular
                )
            }

        End Function


        Private Function CreateWaypointCard(
            waypoint As ManuscriptRouteWaypoint
        ) As Control

            Dim card As New RoundedPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(16, 13, 16, 13),
                .Margin = New Padding(0, 4, 0, 12),
                .BackColor =
                    If(
                        waypoint.IsCurrent,
                        UiTheme.AccentMutedBackground(),
                        UiTheme.CardBackground()
                    ),
                .BorderColor =
                    If(
                        waypoint.IsCurrent,
                        UiTheme.AccentColor(),
                        UiTheme.CardBorder()
                    ),
                .BorderThickness =
                    If(
                        waypoint.IsCurrent,
                        2.0F,
                        1.0F
                    ),
                .CornerRadius = 12
            }

            Dim content As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 4,
                .Margin = New Padding(0),
                .Padding = New Padding(0)
            }

            For index As Integer = 0 To 3
                content.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )
            Next

            Dim topRow As New FlowLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0),
                .Padding = New Padding(0)
            }

            Dim lblKind As New Label With {
                .Text = WaypointKindText(waypoint),
                .AutoSize = True,
                .Margin = New Padding(0, 2, 10, 0),
                .Font = New Font(
                    Me.Font.FontFamily,
                    8.5F,
                    FontStyle.Bold
                ),
                .ForeColor = WaypointAccentColor(waypoint)
            }

            topRow.Controls.Add(
                lblKind
            )

            AddWaypointBadges(
                topRow,
                waypoint
            )

            Dim lblTitle As New Label With {
                .Text = WaypointTitle(waypoint),
                .AutoSize = True,
                .MaximumSize = New Size(420, 0),
                .Margin = New Padding(0, 5, 0, 0),
                .Font = New Font(
                    Me.Font.FontFamily,
                    11.0F,
                    FontStyle.Bold
                ),
                .ForeColor = UiTheme.PrimaryText()
            }

            Dim detailText As String =
                WaypointDetailText(
                    waypoint
                )

            Dim lblDetail As New Label With {
                .Text = detailText,
                .AutoSize = True,
                .MaximumSize = New Size(420, 0),
                .Margin = New Padding(0, 7, 0, 0),
                .ForeColor = UiTheme.SecondaryText(),
                .Visible =
                    Not String.IsNullOrWhiteSpace(
                        detailText
                    )
            }

            Dim versionsText As String =
                RelatedVersionsText(
                    waypoint
                )

            Dim lblVersions As New Label With {
                .Text = versionsText,
                .AutoSize = True,
                .MaximumSize = New Size(420, 0),
                .Margin = New Padding(0, 8, 0, 0),
                .ForeColor = UiTheme.AccentSecondaryColor(),
                .Visible =
                    Not String.IsNullOrWhiteSpace(
                        versionsText
                    )
            }

            content.Controls.Add(
                topRow,
                0,
                0
            )

            content.Controls.Add(
                lblTitle,
                0,
                1
            )

            content.Controls.Add(
                lblDetail,
                0,
                2
            )

            content.Controls.Add(
                lblVersions,
                0,
                3
            )

            card.Controls.Add(
                content
            )

            Dim resizeWrappedText As Action =
                Sub()

                    Dim wrapWidth As Integer =
                        Math.Max(
                            160,
                            card.ClientSize.Width -
                            card.Padding.Horizontal -
                            6
                        )

                    topRow.MaximumSize =
                        New Size(
                            wrapWidth,
                            0
                        )

                    lblTitle.MaximumSize =
                        New Size(
                            wrapWidth,
                            0
                        )

                    lblDetail.MaximumSize =
                        New Size(
                            wrapWidth,
                            0
                        )

                    lblVersions.MaximumSize =
                        New Size(
                            wrapWidth,
                            0
                        )

                End Sub

            AddHandler card.ClientSizeChanged,
                Sub(sender, e)
                    resizeWrappedText()
                End Sub

            resizeWrappedText()

            Return card

        End Function


        Private Sub AddWaypointBadges(
            host As FlowLayoutPanel,
            waypoint As ManuscriptRouteWaypoint
        )

            If waypoint.IsCurrent Then

                host.Controls.Add(
                    CreateBadge(
                        "CURRENT",
                        UiTheme.AccentColor()
                    )
                )

            End If

            If waypoint.ContainsCurrentVersion AndAlso
               Not waypoint.IsCurrent Then

                host.Controls.Add(
                    CreateBadge(
                        "CURRENT VERSION",
                        UiTheme.AccentSecondaryColor()
                    )
                )

            End If

            If waypoint.IsRerouteSource Then

                host.Controls.Add(
                    CreateBadge(
                        "REROUTED",
                        UiTheme.DangerColor()
                    )
                )

            End If

            If waypoint.RevisionRoundNumber.HasValue Then

                host.Controls.Add(
                    CreateBadge(
                        "REVISION " &
                        waypoint.RevisionRoundNumber.Value.ToString(),
                        UiTheme.WarningColor()
                    )
                )

            End If

            If waypoint.HasUnresolvedLink Then

                host.Controls.Add(
                    CreateBadge(
                        "HISTORICAL LINK",
                        UiTheme.SecondaryText()
                    )
                )

            End If

        End Sub


        Private Function CreateBadge(
            text As String,
            color As Color
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Padding = New Padding(7, 2, 7, 2),
                .Margin = New Padding(0, 0, 6, 2),
                .BackColor = UiTheme.CardBackground(),
                .ForeColor = color,
                .Font = New Font(
                    Me.Font.FontFamily,
                    8.0F,
                    FontStyle.Bold
                )
            }

        End Function


        Private Function BuildSummaryText() As String

            Return FormatStage(
                _manuscript.CurrentStage
            ) &
                "  •  " &
                _manuscript.SubmissionCount.ToString() &
                " submission" &
                If(
                    _manuscript.SubmissionCount = 1,
                    String.Empty,
                    "s"
                ) &
                "  •  " &
                _manuscript.RejectionCount.ToString() &
                " rejection" &
                If(
                    _manuscript.RejectionCount = 1,
                    String.Empty,
                    "s"
                ) &
                "  •  " &
                _manuscript.Versions.Count.ToString() &
                " tracked version" &
                If(
                    _manuscript.Versions.Count = 1,
                    String.Empty,
                    "s"
                )

        End Function


        Private Function WaypointKindText(
            waypoint As ManuscriptRouteWaypoint
        ) As String

            Select Case waypoint.Kind

                Case ManuscriptRouteWaypointKind.Stage
                    Return "STAGE"

                Case ManuscriptRouteWaypointKind.Submission
                    Return "SUBMISSION"

                Case ManuscriptRouteWaypointKind.Decision
                    Return "DECISION"

                Case ManuscriptRouteWaypointKind.Version
                    Return "VERSION"

                Case ManuscriptRouteWaypointKind.CurrentState
                    Return "CURRENT STATE"

                Case ManuscriptRouteWaypointKind.FileDrawer
                    Return "FILE DRAWER"

                Case Else
                    Return "ROUTE"

            End Select

        End Function


        Private Function WaypointTitle(
            waypoint As ManuscriptRouteWaypoint
        ) As String

            Select Case waypoint.Kind

                Case ManuscriptRouteWaypointKind.Stage

                    If waypoint.Stage.HasValue Then
                        Return FormatStage(
                            waypoint.Stage.Value
                        )
                    End If

                    Return "Stage change"

                Case ManuscriptRouteWaypointKind.CurrentState

                    If waypoint.Stage.HasValue Then

                        Return "Currently " &
                            FormatStage(
                                waypoint.Stage.Value
                            )

                    End If

                    Return "Current manuscript state"

                Case ManuscriptRouteWaypointKind.Submission

                    If String.IsNullOrWhiteSpace(
                        waypoint.JournalName
                    ) Then

                        Return "Submitted to journal"

                    End If

                    Return "Submitted to " &
                        waypoint.JournalName

                Case ManuscriptRouteWaypointKind.Decision

                    Dim decisionText As String =
                        If(
                            waypoint.Decision.HasValue,
                            FormatDecision(
                                waypoint.Decision.Value
                            ),
                            "Editorial decision"
                        )

                    If String.IsNullOrWhiteSpace(
                        waypoint.JournalName
                    ) Then

                        Return decisionText

                    End If

                    Return decisionText &
                        " - " &
                        waypoint.JournalName

                Case ManuscriptRouteWaypointKind.Version

                    Dim version As ManuscriptVersion =
                        FindVersion(
                            waypoint.VersionId
                        )

                    If version IsNot Nothing AndAlso
                       Not String.IsNullOrWhiteSpace(
                           version.Label
                       ) Then

                        Return version.Label

                    End If

                    Return "Historical manuscript version"

                Case ManuscriptRouteWaypointKind.FileDrawer
                    Return "Moved to File Drawer"

                Case Else
                    Return "Manuscript event"

            End Select

        End Function


        Private Function WaypointDetailText(
            waypoint As ManuscriptRouteWaypoint
        ) As String

            Dim parts As New List(Of String)()

            If waypoint.Kind =
               ManuscriptRouteWaypointKind.Submission AndAlso
               waypoint.SubmissionId.HasValue Then

                Dim submission As JournalSubmission =
                    FindSubmission(
                        waypoint.SubmissionId.Value
                    )

                If submission IsNot Nothing AndAlso
                   Not String.IsNullOrWhiteSpace(
                       submission.ManuscriptNumber
                   ) Then

                    parts.Add(
                        "Journal manuscript ID: " &
                        submission.ManuscriptNumber
                    )

                End If

            End If

            If waypoint.Kind =
               ManuscriptRouteWaypointKind.Decision AndAlso
               waypoint.DecisionId.HasValue Then

                Dim decision As EditorialDecisionEvent =
                    FindDecision(
                        waypoint.DecisionId.Value
                    )

                If decision IsNot Nothing AndAlso
                   decision.RevisionDeadline.HasValue Then

                    parts.Add(
                        "Revision deadline: " &
                        decision.RevisionDeadline.Value.ToString(
                            "MMM d, yyyy"
                        )
                    )

                End If

            End If

            If waypoint.Kind =
               ManuscriptRouteWaypointKind.Version Then

                Dim version As ManuscriptVersion =
                    FindVersion(
                        waypoint.VersionId
                    )

                If version IsNot Nothing Then

                    If version.IsManagedCopy Then
                        parts.Add("Snapshot stored in the PaperRoute Library")
                    ElseIf Not String.IsNullOrWhiteSpace(
                        version.LocalFilePath
                    ) Then
                        parts.Add("Externally linked file")
                    End If

                End If

            End If

            If waypoint.Kind =
               ManuscriptRouteWaypointKind.CurrentState AndAlso
               waypoint.Location.HasValue Then

                Select Case waypoint.Location.Value

                    Case ManuscriptLocation.Published
                        parts.Add("Published library")

                    Case ManuscriptLocation.FileDrawer
                        parts.Add("File Drawer")

                    Case ManuscriptLocation.Pipeline
                        parts.Add("Active Pipeline")

                End Select

            End If

            If Not String.IsNullOrWhiteSpace(
                waypoint.Note
            ) Then

                parts.Add(
                    waypoint.Note.Trim()
                )

            End If

            Return String.Join(
                Environment.NewLine,
                parts
            )

        End Function


        Private Function RelatedVersionsText(
            waypoint As ManuscriptRouteWaypoint
        ) As String

            If waypoint.RelatedVersionIds Is Nothing OrElse
               waypoint.RelatedVersionIds.Count = 0 Then

                Return String.Empty

            End If

            Dim labels As New List(Of String)()

            For Each versionId As Guid In waypoint.RelatedVersionIds

                Dim version As ManuscriptVersion =
                    FindVersion(
                        versionId
                    )

                If version Is Nothing Then

                    labels.Add(
                        "Tracked version"
                    )

                    Continue For

                End If

                Dim label As String =
                    If(
                        String.IsNullOrWhiteSpace(
                            version.Label
                        ),
                        "Tracked version",
                        version.Label.Trim()
                    )

                If _manuscript.CurrentVersionId.HasValue AndAlso
                   _manuscript.CurrentVersionId.Value = version.Id Then

                    label &=
                        " (current)"

                End If

                labels.Add(
                    label
                )

            Next

            Return "Version: " &
                String.Join(
                    ", ",
                    labels
                )

        End Function


        Private Function FindSubmission(
            submissionId As Guid
        ) As JournalSubmission

            If _manuscript.Submissions Is Nothing Then
                Return Nothing
            End If

            Return _manuscript.Submissions.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.Id = submissionId
                    End Function
                )

        End Function


        Private Function FindDecision(
            decisionId As Guid
        ) As EditorialDecisionEvent

            If _manuscript.Submissions Is Nothing Then
                Return Nothing
            End If

            For Each submission As JournalSubmission In
                _manuscript.Submissions

                If submission Is Nothing OrElse
                   submission.Decisions Is Nothing Then

                    Continue For

                End If

                Dim decision As EditorialDecisionEvent =
                    submission.Decisions.
                        FirstOrDefault(
                            Function(item)
                                Return item IsNot Nothing AndAlso
                                    item.Id = decisionId
                            End Function
                        )

                If decision IsNot Nothing Then
                    Return decision
                End If

            Next

            Return Nothing

        End Function


        Private Function FindVersion(
            versionId As Guid?
        ) As ManuscriptVersion

            If Not versionId.HasValue OrElse
               _manuscript.Versions Is Nothing Then

                Return Nothing

            End If

            Return _manuscript.Versions.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.Id = versionId.Value
                    End Function
                )

        End Function


        Private Function WaypointAccentColor(
            waypoint As ManuscriptRouteWaypoint
        ) As Color

            If waypoint.IsCurrent Then
                Return UiTheme.AccentColor()
            End If

            Select Case waypoint.Kind

                Case ManuscriptRouteWaypointKind.Decision

                    If waypoint.Decision.HasValue Then

                        Select Case waypoint.Decision.Value

                            Case EditorialDecision.Rejected,
                                 EditorialDecision.DeskRejected,
                                 EditorialDecision.RejectedAfterReview

                                Return UiTheme.DangerColor()

                            Case EditorialDecision.Accepted

                                Return UiTheme.SuccessColor()

                            Case EditorialDecision.MajorRevision,
                                 EditorialDecision.MinorRevision,
                                 EditorialDecision.ReviseAndResubmit

                                Return UiTheme.WarningColor()

                        End Select

                    End If

                    Return UiTheme.AccentSecondaryColor()

                Case ManuscriptRouteWaypointKind.Submission
                    Return UiTheme.AccentSecondaryColor()

                Case ManuscriptRouteWaypointKind.FileDrawer
                    Return UiTheme.WarningColor()

                Case ManuscriptRouteWaypointKind.Version
                    Return UiTheme.SecondaryText()

                Case ManuscriptRouteWaypointKind.Stage,
                     ManuscriptRouteWaypointKind.CurrentState

                    If waypoint.Stage.HasValue Then

                        Return UiTheme.StageForeground(
                            waypoint.Stage.Value
                        )

                    End If

                    Return UiTheme.AccentColor()

                Case Else
                    Return UiTheme.SecondaryText()

            End Select

        End Function


        Private Function FormatStage(
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


        Private Function FormatDecision(
            decision As EditorialDecision
        ) As String

            Select Case decision

                Case EditorialDecision.None
                    Return "Editorial decision"

                Case EditorialDecision.Rejected
                    Return "Rejected"

                Case EditorialDecision.DeskRejected
                    Return "Desk rejected"

                Case EditorialDecision.RejectedAfterReview
                    Return "Rejected after review"

                Case EditorialDecision.MajorRevision
                    Return "Major revision"

                Case EditorialDecision.MinorRevision
                    Return "Minor revision"

                Case EditorialDecision.ReviseAndResubmit
                    Return "Revise and resubmit"

                Case EditorialDecision.Accepted
                    Return "Accepted"

                Case EditorialDecision.Withdrawn
                    Return "Withdrawn"

                Case Else
                    Return decision.ToString()

            End Select

        End Function


        Private Class RouteMarkerControl
            Inherits Control

            <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
            Public Property MarkerColor As Color =
                SystemColors.ControlDark

            <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
            Public Property DrawLineAbove As Boolean =
                True

            <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
            Public Property DrawLineBelow As Boolean =
                True


            Public Sub New()

                Me.DoubleBuffered =
                    True

                Me.MinimumSize =
                    New Size(
                        42,
                        54
                    )

                Me.BackColor =
                    UiTheme.BoardBackground()

                Me.SetStyle(
                    ControlStyles.SupportsTransparentBackColor,
                    True
                )

            End Sub


            Protected Overrides Sub OnPaint(
                e As PaintEventArgs
            )

                MyBase.OnPaint(
                    e
                )

                e.Graphics.SmoothingMode =
                    SmoothingMode.AntiAlias

                Dim centerX As Integer =
                    Me.ClientSize.Width \ 2

                Dim markerY As Integer =
                    Math.Min(
                        28,
                        Math.Max(
                            18,
                            Me.ClientSize.Height \ 3
                        )
                    )

                Using linePen As New Pen(
                    UiTheme.CardBorder(),
                    2.0F
                )

                    If DrawLineAbove Then

                        e.Graphics.DrawLine(
                            linePen,
                            centerX,
                            0,
                            centerX,
                            markerY - 8
                        )

                    End If

                    If DrawLineBelow Then

                        e.Graphics.DrawLine(
                            linePen,
                            centerX,
                            markerY + 8,
                            centerX,
                            Me.ClientSize.Height
                        )

                    End If

                End Using

                Using fillBrush As New SolidBrush(
                    MarkerColor
                )

                    e.Graphics.FillEllipse(
                        fillBrush,
                        centerX - 7,
                        markerY - 7,
                        14,
                        14
                    )

                End Using

                Using innerBrush As New SolidBrush(
                    UiTheme.CardBackground()
                )

                    e.Graphics.FillEllipse(
                        innerBrush,
                        centerX - 3,
                        markerY - 3,
                        6,
                        6
                    )

                End Using

            End Sub

        End Class

    End Class

End Namespace
