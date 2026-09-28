Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Check for Publications (#61): choose manuscripts, ask Crossref (and
    ' optionally an ORCID record) once, then review each possible match.
    ' Nothing changes a manuscript except Mark Published.
    Friend Class PublicationCheckForm
        Inherits Form

        Private ReadOnly _library As List(Of Manuscript)
        Private ReadOnly _eligible As List(Of Manuscript)
        Private ReadOnly _source As IPublicationSource
        Private ReadOnly _save As Func(Of Boolean)
        Private ReadOnly _today As DateTime

        Private ReadOnly lstManuscripts As New CheckedListBox()
        Private ReadOnly lblSelected As New Label()
        Private ReadOnly chkOrcid As New CheckBox()
        Private ReadOnly txtOrcid As New TextBox()
        Private ReadOnly lblProgress As New Label()
        Private ReadOnly progressBar As New ProgressBar()
        Private ReadOnly btnPrimary As New ActionButton()
        Private ReadOnly btnCancel As New ActionButton()
        Private ReadOnly body As New Panel()
        Private ReadOnly lblIntro As New Label()

        Private _cancellation As CancellationTokenSource = Nothing

        ' Tests answer the Mark Published confirmation and skip the pause.
        Friend ConfirmMarkPublished As Func(Of Manuscript, PublicationMatch, Boolean) = Nothing
        Friend Pause As TimeSpan = TimeSpan.FromMilliseconds(250)

        ' Matches found or changed, for the caller to refresh its pages.
        <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
        Friend Property Changed As Boolean

        Public Sub New(
            library As List(Of Manuscript),
            checkIds As IEnumerable(Of Guid),
            defaultOrcid As String,
            source As IPublicationSource,
            save As Func(Of Boolean),
            today As DateTime
        )

            _library = library
            _source = source
            _save = save
            _today = today.Date

            Dim requested As New HashSet(Of Guid)(If(checkIds, Enumerable.Empty(Of Guid)()))
            _eligible = library.Where(Function(item) PublicationMatchService.IsEligible(item)).
                OrderByDescending(Function(item) If(requested.Count > 0, requested.Contains(item.Id), PublicationMatchService.IsSuggested(item))).
                ThenBy(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).ToList()

            Text = "Check for Publications"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            AutoScaleMode = AutoScaleMode.Dpi
            Font = New Font("Segoe UI", 9.0F)
            ClientSize = New Size(720, 560)
            MinimumSize = New Size(560, 440)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(18, 16, 18, 14)
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.Text = "PaperRoute asks Crossref whether the manuscripts you check have been published: by their DOI, " &
                            "a preprint's link to its published version, or the same title. Only now, only for these manuscripts. " &
                            "Nothing changes until you choose Mark Published."
            lblIntro.AutoSize = True
            lblIntro.MaximumSize = New Size(680, 0)
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 12)
            root.Controls.Add(lblIntro, 0, 0)

            body.Dock = DockStyle.Fill
            body.Margin = New Padding(0)
            root.Controls.Add(body, 0, 1)

            Dim buttons As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 12, 0, 0)
            }
            btnPrimary.Role = ActionButtonRole.Primary
            btnPrimary.Height = 34
            AddHandler btnPrimary.Click, AddressOf PrimaryClicked
            btnCancel.Text = "Cancel"
            btnCancel.Height = 34
            btnCancel.Width = 90
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnCancel)
            root.Controls.Add(buttons, 0, 2)

            Controls.Add(root)
            AcceptButton = btnPrimary

            ShowChoices(requested, defaultOrcid)
            UiPolish.ApplyDialog(Me)

        End Sub


        ' ---------------------------------------------------------------
        ' Choose
        ' ---------------------------------------------------------------

        Private Sub ShowChoices(requested As HashSet(Of Guid), defaultOrcid As String)

            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Margin = New Padding(0)}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lstManuscripts.Dock = DockStyle.Fill
            lstManuscripts.CheckOnClick = True
            lstManuscripts.IntegralHeight = False
            lstManuscripts.HorizontalScrollbar = True
            lstManuscripts.AccessibleName = "Manuscripts to check"
            For Each manuscript As Manuscript In _eligible
                Dim index As Integer = lstManuscripts.Items.Add(Describe(manuscript))
                lstManuscripts.SetItemChecked(index, If(requested.Count > 0, requested.Contains(manuscript.Id), PublicationMatchService.IsSuggested(manuscript)))
            Next
            AddHandler lstManuscripts.ItemCheck, Sub(sender, e) BeginInvokeIfReady(AddressOf UpdateSelection)
            layout.Controls.Add(lstManuscripts, 0, 0)

            Dim selectRow As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0, 6, 0, 6)}
            For Each entry In {("Select all", True), ("Select none", False)}
                Dim value As Boolean = entry.Item2
                Dim link As New LinkLabel With {.Text = entry.Item1, .AutoSize = True, .Margin = New Padding(0, 0, 14, 0), .UseMnemonic = False}
                AddHandler link.LinkClicked,
                    Sub(sender, e)
                        For index As Integer = 0 To lstManuscripts.Items.Count - 1
                            lstManuscripts.SetItemChecked(index, value)
                        Next
                        UpdateSelection()
                    End Sub
                selectRow.Controls.Add(link)
            Next
            lblSelected.AutoSize = True
            lblSelected.UseMnemonic = False
            lblSelected.ForeColor = SystemColors.GrayText
            selectRow.Controls.Add(lblSelected)
            layout.Controls.Add(selectRow, 0, 1)

            Dim orcidRow As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0, 0, 0, 4)}
            chkOrcid.Text = "Also look in the ORCID record"
            chkOrcid.AutoSize = True
            chkOrcid.Margin = New Padding(0, 4, 8, 0)
            chkOrcid.Checked = Not String.IsNullOrWhiteSpace(defaultOrcid)
            txtOrcid.Text = If(defaultOrcid, String.Empty)
            txtOrcid.Width = 190
            txtOrcid.PlaceholderText = "0000-0000-0000-0000"
            txtOrcid.AccessibleName = "ORCID iD"
            AddHandler txtOrcid.TextChanged, Sub(sender, e) chkOrcid.Checked = txtOrcid.TextLength > 0
            orcidRow.Controls.Add(chkOrcid)
            orcidRow.Controls.Add(txtOrcid)
            layout.Controls.Add(orcidRow, 0, 2)

            layout.Controls.Add(New Label With {
                .Text = "Crossref receives the titles and DOIs of the checked manuscripts; ORCID is only read. Nothing else leaves this computer.",
                .AutoSize = True,
                .MaximumSize = New Size(680, 0),
                .UseMnemonic = False,
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 2, 0, 0)
            }, 0, 3)

            body.Controls.Add(layout)

            If _eligible.Count = 0 Then
                lstManuscripts.Enabled = False
                lblSelected.Text = "Every manuscript is already on the Published shelf."
            End If

            UpdateSelection()

        End Sub


        Private Shared Function Describe(manuscript As Manuscript) As String
            Dim parts As New List(Of String) From {ReminderService.SafeManuscriptTitle(manuscript), StageText(manuscript)}
            If Not String.IsNullOrWhiteSpace(manuscript.TargetJournal) Then parts.Add(manuscript.TargetJournal.Trim())
            Return String.Join("  ·  ", parts)
        End Function


        Private Shared Function StageText(manuscript As Manuscript) As String
            If manuscript.Location = ManuscriptLocation.FileDrawer Then Return "File Drawer"
            Select Case manuscript.CurrentStage
                Case PaperStage.UnderReview : Return "Under review"
                Case PaperStage.InPress : Return "In press"
                Case Else : Return manuscript.CurrentStage.ToString()
            End Select
        End Function


        Private Sub BeginInvokeIfReady(action As Action)
            If IsHandleCreated Then BeginInvoke(action) Else action()
        End Sub


        Private Sub UpdateSelection()
            Dim count As Integer = lstManuscripts.CheckedIndices.Count
            If _eligible.Count > 0 Then
                lblSelected.Text = count.ToString(CultureInfo.CurrentCulture) & " of " & _eligible.Count.ToString(CultureInfo.CurrentCulture) & " selected"
            End If
            btnPrimary.Text = If(count = 1, "Check 1 Manuscript", "Check " & count.ToString(CultureInfo.CurrentCulture) & " Manuscripts")
            btnPrimary.Width = TextRenderer.MeasureText(btnPrimary.Text, Font).Width + 36
            btnPrimary.Enabled = count > 0
        End Sub


        Friend ReadOnly Property SelectedManuscripts As List(Of Manuscript)
            Get
                Return lstManuscripts.CheckedIndices.Cast(Of Integer)().Select(Function(index) _eligible(index)).ToList()
            End Get
        End Property


        ' ---------------------------------------------------------------
        ' Check
        ' ---------------------------------------------------------------

        Private Async Sub PrimaryClicked(sender As Object, e As EventArgs)
            If _cancellation IsNot Nothing Then Return
            If btnPrimary.Text = "Close" Then
                DialogResult = DialogResult.OK
                Close()
                Return
            End If
            Await CheckAsync()
        End Sub


        Private Sub CancelClicked(sender As Object, e As EventArgs)
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                btnCancel.Enabled = False
                Return
            End If
            DialogResult = DialogResult.Cancel
            Close()
        End Sub


        Friend Async Function CheckAsync() As Task

            Dim chosen As List(Of Manuscript) = SelectedManuscripts
            If chosen.Count = 0 Then Return

            Dim orcid As String = String.Empty
            If chkOrcid.Checked AndAlso txtOrcid.TextLength > 0 Then
                Try
                    orcid = OrcidIdentifierService.NormalizeAndValidate(txtOrcid.Text)
                Catch ex As ArgumentException
                    MessageBox.Show(Me, ex.Message, "ORCID iD", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    txtOrcid.Focus()
                    Return
                End Try
            End If

            ShowProgress(chosen.Count)
            _cancellation = New CancellationTokenSource()

            Dim result As PublicationCheckResult
            Try
                result = Await PublicationCheckService.CheckAsync(
                    chosen, orcid, _source,
                    New Progress(Of PublicationCheckProgress)(
                        Sub(update)
                            lblProgress.Text = "Checking " & update.Index.ToString(CultureInfo.CurrentCulture) & " of " &
                                               update.Total.ToString(CultureInfo.CurrentCulture) & ":  " & update.ManuscriptTitle
                            progressBar.Value = Math.Min(progressBar.Maximum, update.Index - 1)
                        End Sub),
                    Pause, _cancellation.Token)
            Finally
                _cancellation.Dispose()
                _cancellation = Nothing
            End Try

            RecordMatches(result)
            ShowResults(result)

        End Function


        Private Sub ShowProgress(total As Integer)

            ClearBody()
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1, .Margin = New Padding(0)}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            lblProgress.Text = "Starting..."
            lblProgress.AutoSize = True
            lblProgress.MaximumSize = New Size(680, 0)
            lblProgress.UseMnemonic = False
            lblProgress.Margin = New Padding(0, 24, 0, 8)
            progressBar.Maximum = Math.Max(1, total)
            progressBar.Value = 0
            progressBar.Dock = DockStyle.Top
            progressBar.Height = 12
            layout.Controls.Add(lblProgress)
            layout.Controls.Add(progressBar)
            body.Controls.Add(layout)

            btnPrimary.Visible = False
            btnCancel.Text = "Stop"

        End Sub


        ' New matches are kept at once, so closing the window keeps them for
        ' later review on the Deadlines page.
        Private Sub RecordMatches(result As PublicationCheckResult)

            If result.Matches.Count = 0 Then Return

            For Each found In result.Matches
                found.Manuscript.PublicationMatches.Add(found.Match)
            Next

            If Not _save() Then
                For Each found In result.Matches
                    found.Manuscript.PublicationMatches.Remove(found.Match)
                Next
                Return
            End If

            Changed = True

        End Sub


        ' ---------------------------------------------------------------
        ' Review
        ' ---------------------------------------------------------------

        Private Sub ShowResults(result As PublicationCheckResult)

            ClearBody()
            btnPrimary.Visible = True
            btnPrimary.Text = "Close"
            btnPrimary.Width = 90
            btnPrimary.Enabled = True
            btnCancel.Visible = False
            lblIntro.Text = Summary(result)

            Dim scroller As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Margin = New Padding(0)}
            Dim list As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .Margin = New Padding(0),
                .Padding = New Padding(0, 0, 8, 0),
                .AccessibleName = "Possible publications"
            }
            list.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            For Each found In result.Matches
                If found.Manuscript.PublicationMatches.Contains(found.Match) Then
                    AddRow(list, CreateMatchCard(found.Manuscript, found.Match))
                End If
            Next

            If result.Failures.Count > 0 Then
                Dim failures As String = String.Join(Environment.NewLine,
                    result.Failures.Select(Function(failure) "•  " & If(failure.Manuscript Is Nothing, String.Empty, ReminderService.SafeManuscriptTitle(failure.Manuscript) & ": ") & failure.Reason))
                AddRow(list, New Label With {
                    .Text = "Not checked" & Environment.NewLine & failures,
                    .AutoSize = True,
                    .MaximumSize = New Size(660, 0),
                    .UseMnemonic = False,
                    .ForeColor = SystemColors.GrayText,
                    .Margin = New Padding(0, 8, 0, 0)
                })
            End If

            scroller.Controls.Add(list)
            body.Controls.Add(scroller)
            UiPolish.ApplyDialog(Me)
            btnPrimary.Focus()

        End Sub


        Private Shared Sub AddRow(list As TableLayoutPanel, control As Control)
            list.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            list.Controls.Add(control, 0, list.RowCount)
            list.RowCount += 1
        End Sub


        Friend Shared Function Summary(result As PublicationCheckResult) As String

            Dim checkedText As String = "Checked " & result.Checked.ToString(CultureInfo.CurrentCulture) &
                                        If(result.Checked = 1, " manuscript. ", " manuscripts. ")
            Dim found As String
            Select Case result.Matches.Count
                Case 0 : found = "No new possible publications."
                Case 1 : found = "1 possible publication to review."
                Case Else : found = result.Matches.Count.ToString(CultureInfo.CurrentCulture) & " possible publications to review."
            End Select

            Dim text As String = checkedText & found
            If result.Matches.Count > 0 Then text &= " Any you leave stay on the Deadlines page, under No date."
            If Not String.IsNullOrWhiteSpace(result.StoppedReason) Then text &= Environment.NewLine & "Stopped early: " & result.StoppedReason
            Return text

        End Function


        Private Function CreateMatchCard(manuscript As Manuscript, match As PublicationMatch) As Control

            Dim card As New SectionCard With {
                .Text = ReminderService.SafeManuscriptTitle(manuscript).Replace("&", "&&"),
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(14),
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 1, .Margin = New Padding(0)}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            layout.Controls.Add(New Label With {.Text = "A publication matching this manuscript may have appeared.", .AutoSize = True, .UseMnemonic = False, .Margin = New Padding(0, 0, 0, 4)})
            layout.Controls.Add(New Label With {.Text = PublicationActions.Describe(match), .AutoSize = True, .MaximumSize = New Size(620, 0), .UseMnemonic = False, .Font = New Font(Font, FontStyle.Bold), .Margin = New Padding(0, 0, 0, 2)})
            layout.Controls.Add(New Label With {.Text = PublicationActions.SourceText(match), .AutoSize = True, .UseMnemonic = False, .ForeColor = SystemColors.GrayText, .Margin = New Padding(0, 0, 0, 8)})

            Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0)}
            Dim outcome As New Label With {.AutoSize = True, .UseMnemonic = False, .Visible = False, .Margin = New Padding(0, 6, 0, 0)}

            Dim review As New ActionButton With {.Text = "Review Match", .Height = 30, .Width = 118, .Margin = New Padding(0, 0, 8, 0), .Enabled = PublicationActions.MatchUri(match) IsNot Nothing}
            AddHandler review.Click, Sub(sender, e) PublicationActions.OpenMatch(Me, match)
            Dim mark As New ActionButton With {.Text = "Mark Published...", .Height = 30, .Width = 138, .Margin = New Padding(0, 0, 8, 0)}
            Dim ignore As New ActionButton With {.Text = "Ignore", .Height = 30, .Width = 80, .Margin = New Padding(0)}

            AddHandler mark.Click,
                Sub(sender, e)
                    If MarkPublished(manuscript, match) Then
                        actions.Visible = False
                        outcome.Text = "Marked published. It is on the Published shelf."
                        outcome.Visible = True
                    End If
                End Sub
            AddHandler ignore.Click,
                Sub(sender, e)
                    Dim target As PublicationMatch = match
                    If PublicationActions.Commit(_library, manuscript, Sub() PublicationMatchService.Ignore(target), _save) Then
                        Changed = True
                        actions.Visible = False
                        outcome.Text = "Ignored. Later checks will not show this match again."
                        outcome.Visible = True
                    End If
                End Sub

            actions.Controls.Add(review)
            actions.Controls.Add(mark)
            actions.Controls.Add(ignore)
            layout.Controls.Add(actions)
            layout.Controls.Add(outcome)
            card.Controls.Add(layout)
            Return card

        End Function


        Friend Function MarkPublished(manuscript As Manuscript, match As PublicationMatch) As Boolean

            Dim confirmed As Boolean
            If ConfirmMarkPublished IsNot Nothing Then
                confirmed = ConfirmMarkPublished(manuscript, match)
            Else
                Using dialog As New MarkPublishedForm(manuscript, match, _today)
                    confirmed = dialog.ShowDialog(Me) = DialogResult.OK
                End Using
            End If
            If Not confirmed Then Return False

            If Not PublicationActions.Commit(_library, manuscript, Sub() PublicationMatchService.MarkPublished(manuscript, match, _today), _save) Then Return False
            Changed = True
            Return True

        End Function


        Private Sub ClearBody()
            For Each child As Control In body.Controls.Cast(Of Control)().ToList()
                body.Controls.Remove(child)
                If child IsNot lstManuscripts Then child.Dispose()
            Next
        End Sub


        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            ' Closing while checking stops the check first.
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                e.Cancel = True
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub

    End Class

End Namespace
