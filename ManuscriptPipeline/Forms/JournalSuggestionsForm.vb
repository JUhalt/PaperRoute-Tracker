Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
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

    ' Journals that publish work like this (#88). The researcher reviews the
    ' keywords, and sees exactly what will be sent, before anything goes to
    ' OpenAlex; the journals found come with their evidence, never a score.
    Friend Class JournalSuggestionsForm
        Inherits Form

        Private Enum Stage
            Keywords
            Running
            Evidence
        End Enum

        ' The journals list's columns.
        Private Const JournalColumn As Integer = 0
        Private Const PublisherColumn As Integer = 1
        Private Const MatchesColumn As Integer = 2
        Private Const ShareColumn As Integer = 3

        Private ReadOnly _source As IJournalSuggestionsSource
        Private ReadOnly _isOnShortlist As Func(Of JournalSuggestion, Boolean)
        Private ReadOnly _yoursFor As Func(Of JournalSuggestion, String)
        Private ReadOnly _today As DateTime
        Private _stage As Stage = Stage.Keywords
        Private _cancellation As CancellationTokenSource
        ' The on-demand examples fetch: never blocks Cancel or closing.
        Private _examplesCancellation As CancellationTokenSource
        Private _examplesBusy As OnlineServiceBusyException
        Private _examplesBusyUntil As DateTime
        Private _result As JournalSuggestionsResult
        Private _allowCheck As Boolean
        Private _sortColumn As Integer = MatchesColumn
        Private _waitUntil As DateTime?

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly keywordsPanel As New TableLayoutPanel()
        Private ReadOnly clbKeywords As New CheckedListBox()
        Private ReadOnly txtAdd As New TextBox()
        Private ReadOnly btnAdd As New Button()
        Private ReadOnly rbAll As New RadioButton()
        Private ReadOnly rbAny As New RadioButton()
        Private ReadOnly cboYears As New ComboBox()
        Private ReadOnly lblRequest As New Label()
        Private ReadOnly lnkAddress As New LinkLabel()
        Private ReadOnly txtAddress As New TextBox()
        Private ReadOnly evidencePanel As New TableLayoutPanel()
        Private ReadOnly lvJournals As New ListView()
        Private ReadOnly examplesPanel As New FlowLayoutPanel()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly lblFooter As New Label()
        ' Plain buttons size to their text; the accept button is styled as
        ' the primary action.
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnBack As New Button()
        Private ReadOnly btnCancel As New Button()
        Private ReadOnly waitTimer As New System.Windows.Forms.Timer With {.Interval = 1000}
        Private ReadOnly body As New Panel()
        Private ReadOnly toolTip As New ToolTip()


        ' On a small screen or at a high scale, the window keeps its buttons
        ' above the taskbar.
        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        ' title: the page's current title text; keywords: the manuscript's.
        Public Sub New(title As String, keywords As IEnumerable(Of String), source As IJournalSuggestionsSource,
                       isOnShortlist As Func(Of JournalSuggestion, Boolean), yoursFor As Func(Of JournalSuggestion, String),
                       Optional today As DateTime? = Nothing)

            _source = source
            _isOnShortlist = If(isOnShortlist, Function(item) False)
            _yoursFor = If(yoursFor, Function(item) String.Empty)
            _today = If(today, DateTime.Today)

            BuildInterface()
            For Each proposal As KeywordProposal In JournalSuggestionService.ProposeKeywords(title, keywords)
                clbKeywords.Items.Add(proposal.Text, proposal.Checked)
            Next
            ShowKeywords()
            UiPolish.ApplyDialog(Me)

        End Sub


        ' The journals checked, with the search that found them, after OK.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Chosen As New List(Of JournalSuggestion)()

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Result As JournalSuggestionsResult
            Get
                Return _result
            End Get
        End Property


        Private Sub BuildInterface()

            SuspendLayout()

            Text = "Journals That Publish Work Like This"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(1120, 640)
            MinimumSize = New Size(700, 500)
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.AutoSize = True
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 10)

            BuildKeywordsPanel()
            BuildEvidencePanel()

            body.Dock = DockStyle.Fill
            body.Margin = New Padding(0)

            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.Margin = New Padding(0, 8, 0, 0)
            lblFooter.AutoSize = True
            lblFooter.UseMnemonic = False
            lblFooter.ForeColor = UiTheme.MutedText()
            lblFooter.Margin = New Padding(0, 6, 0, 0)
            Dim status As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Margin = New Padding(0)}
            status.Controls.Add(lblStatus)
            status.Controls.Add(lblFooter)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            btnPrimary.AutoSize = True
            btnPrimary.MinimumSize = New Size(96, 34)
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            btnCancel.Text = "Cancel"
            btnCancel.AutoSize = True
            btnCancel.MinimumSize = New Size(96, 34)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            btnBack.Text = "Back"
            btnBack.AutoSize = True
            btnBack.MinimumSize = New Size(96, 34)
            btnBack.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnBack.Click, Sub(sender, e) ShowKeywords()
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnCancel)
            buttons.Controls.Add(btnBack)
            ' Esc stops a search, or closes; a running search vetoes the close.
            CancelButton = btnCancel

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(body, 0, 1)
            root.Controls.Add(status, 0, 2)
            root.Controls.Add(buttons, 0, 3)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    For Each label As Label In {lblIntro, lblStatus, lblFooter, lblRequest}
                        label.MaximumSize = New Size(width, 0)
                    Next
                End Sub

            AddHandler waitTimer.Tick, Sub(sender, e) RefreshWait()

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub BuildKeywordsPanel()

            keywordsPanel.Dock = DockStyle.Fill
            keywordsPanel.ColumnCount = 1
            keywordsPanel.Margin = New Padding(0)
            keywordsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For Each style As SizeType In {SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize}
                keywordsPanel.RowStyles.Add(If(style = SizeType.Percent, New RowStyle(SizeType.Percent, 100), New RowStyle(SizeType.AutoSize)))
            Next
            keywordsPanel.RowCount = 7

            Dim heading As New Label With {.Text = "&Keywords to search for", .AutoSize = True, .Margin = New Padding(0, 0, 0, 4)}

            clbKeywords.Dock = DockStyle.Fill
            clbKeywords.CheckOnClick = True
            clbKeywords.IntegralHeight = False
            clbKeywords.AccessibleName = "Keywords to search for"
            clbKeywords.BackColor = UiTheme.CardBackground()
            clbKeywords.ForeColor = UiTheme.PrimaryText()
            AddHandler clbKeywords.ItemCheck, Sub(sender, e) If IsHandleCreated Then BeginInvoke(New Action(AddressOf RefreshRequest))

            Dim addRow As New TableLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Top, .ColumnCount = 3, .Margin = New Padding(0, 6, 0, 0)}
            addRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            addRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            addRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            Dim lblAdd As New Label With {.Text = "A&dd a keyword", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 6, 8, 6)}
            txtAdd.Dock = DockStyle.Fill
            txtAdd.MaxLength = JournalSuggestionService.MaximumKeywordLength
            txtAdd.Margin = New Padding(0, 4, 8, 4)
            AddHandler txtAdd.Enter, Sub(sender, e) AcceptButton = btnAdd
            AddHandler txtAdd.Leave, Sub(sender, e) AcceptButton = btnPrimary
            btnAdd.Text = "Add"
            btnAdd.AutoSize = True
            btnAdd.MinimumSize = New Size(72, 32)
            AddHandler btnAdd.Click, Sub(sender, e) AddKeyword()
            addRow.Controls.Add(lblAdd, 0, 0)
            addRow.Controls.Add(txtAdd, 1, 0)
            addRow.Controls.Add(btnAdd, 2, 0)

            Dim options As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = True, .Dock = DockStyle.Top, .Margin = New Padding(0, 8, 0, 0)}
            Dim lblMatch As New Label With {.Text = "Articles must match", .AutoSize = True, .Margin = New Padding(0, 6, 8, 0)}
            rbAll.Text = "every keyword"
            rbAll.AutoSize = True
            rbAll.Checked = True
            rbAll.Margin = New Padding(0, 4, 12, 0)
            rbAny.Text = "any keyword"
            rbAny.AutoSize = True
            rbAny.Margin = New Padding(0, 4, 24, 0)
            AddHandler rbAll.CheckedChanged, Sub(sender, e) RefreshRequest()
            Dim lblYears As New Label With {.Text = "Published in the last", .AutoSize = True, .Margin = New Padding(0, 6, 8, 0)}
            cboYears.DropDownStyle = ComboBoxStyle.DropDownList
            cboYears.Items.AddRange({"3 years", "5 years", "10 years"})
            cboYears.SelectedIndex = 1
            cboYears.Width = LogicalToDeviceUnits(110)
            cboYears.Margin = New Padding(0, 2, 0, 0)
            AddHandler cboYears.SelectedIndexChanged, Sub(sender, e) RefreshRequest()
            options.Controls.AddRange({lblMatch, rbAll, rbAny, lblYears, cboYears})

            lblRequest.AutoSize = True
            lblRequest.UseMnemonic = False
            lblRequest.Margin = New Padding(0, 10, 0, 0)
            lblRequest.Font = New Font(Font, FontStyle.Bold)

            lnkAddress.Text = "Show the exact web address"
            lnkAddress.AutoSize = True
            lnkAddress.Margin = New Padding(0, 4, 0, 0)
            AddHandler lnkAddress.LinkClicked,
                Sub(sender, e)
                    txtAddress.Visible = Not txtAddress.Visible
                    lnkAddress.Text = If(txtAddress.Visible, "Hide the web address", "Show the exact web address")
                End Sub

            txtAddress.ReadOnly = True
            txtAddress.Multiline = True
            txtAddress.WordWrap = True
            txtAddress.Visible = False
            txtAddress.Dock = DockStyle.Top
            txtAddress.Height = LogicalToDeviceUnits(64)
            txtAddress.Margin = New Padding(0, 4, 0, 0)
            txtAddress.AccessibleName = "The exact web address PaperRoute will send"

            keywordsPanel.Controls.Add(heading, 0, 0)
            keywordsPanel.Controls.Add(clbKeywords, 0, 1)
            keywordsPanel.Controls.Add(addRow, 0, 2)
            keywordsPanel.Controls.Add(options, 0, 3)
            keywordsPanel.Controls.Add(lblRequest, 0, 4)
            keywordsPanel.Controls.Add(lnkAddress, 0, 5)
            keywordsPanel.Controls.Add(txtAddress, 0, 6)

        End Sub


        Private Sub BuildEvidencePanel()

            evidencePanel.Dock = DockStyle.Fill
            evidencePanel.ColumnCount = 1
            evidencePanel.RowCount = 2
            evidencePanel.Margin = New Padding(0)
            evidencePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            evidencePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            evidencePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lvJournals.Dock = DockStyle.Fill
            lvJournals.View = View.Details
            lvJournals.FullRowSelect = True
            lvJournals.MultiSelect = False
            lvJournals.HideSelection = False
            lvJournals.CheckBoxes = True
            lvJournals.ShowItemToolTips = True
            lvJournals.BackColor = UiTheme.CardBackground()
            lvJournals.ForeColor = UiTheme.PrimaryText()
            lvJournals.AccessibleName = "Journals that published matching articles"
            lvJournals.Columns.Add("Journal", LogicalToDeviceUnits(230))
            lvJournals.Columns.Add("Publisher", LogicalToDeviceUnits(180))
            lvJournals.Columns.Add("Matching articles", LogicalToDeviceUnits(120), HorizontalAlignment.Right)
            lvJournals.Columns.Add("Of all its articles", LogicalToDeviceUnits(150), HorizontalAlignment.Right)
            lvJournals.Columns.Add("Open access and listed fee", LogicalToDeviceUnits(230))
            lvJournals.Columns.Add("Yours", LogicalToDeviceUnits(150))
            AddHandler lvJournals.ItemCheck, AddressOf JournalChecking
            AddHandler lvJournals.ItemChecked, Sub(sender, e) RefreshAddButton()
            AddHandler lvJournals.SelectedIndexChanged, Async Sub(sender, e) Await ShowExamplesAsync()
            AddHandler lvJournals.ColumnClick, AddressOf SortBy

            examplesPanel.AutoSize = True
            examplesPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink
            examplesPanel.FlowDirection = FlowDirection.TopDown
            examplesPanel.WrapContents = False
            examplesPanel.Dock = DockStyle.Top
            examplesPanel.Margin = New Padding(0, 8, 0, 0)

            evidencePanel.Controls.Add(lvJournals, 0, 0)
            evidencePanel.Controls.Add(examplesPanel, 0, 1)

        End Sub


        ' ---------------------------------------------------------------
        ' Keywords
        ' ---------------------------------------------------------------

        Private Sub ShowKeywords()

            _stage = Stage.Keywords
            body.Controls.Clear()
            body.Controls.Add(keywordsPanel)
            lblIntro.Text =
                "PaperRoute looks in OpenAlex, an open index of published research, for recent journal articles that mention the keywords you check, and groups them by journal. " &
                "Only the keywords you check are sent: never the abstract, notes, or files."
            lblFooter.Text = String.Empty
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = If(clbKeywords.Items.Count = 0,
                                "Add a keyword or two that describe the work, such as a method or topic.",
                                "Check the keywords that describe the work. Phrases from the title start unchecked.")
            btnBack.Visible = False
            btnPrimary.Text = "Search"
            btnCancel.Text = "Cancel"
            AcceptButton = btnPrimary
            RefreshRequest()

        End Sub


        Private Sub AddKeyword()
            Dim keyword As String = JournalSuggestionService.Sanitize(txtAdd.Text)
            If keyword.Length = 0 Then Return
            Dim index As Integer = clbKeywords.Items.Cast(Of String)().ToList().FindIndex(Function(item) String.Equals(item, keyword, StringComparison.OrdinalIgnoreCase))
            If index < 0 Then index = clbKeywords.Items.Add(keyword)
            clbKeywords.SetItemChecked(index, True)
            txtAdd.Clear()
            RefreshRequest()
        End Sub


        Friend Function CurrentRequest() As JournalSuggestionRequest
            Dim years As Integer = {3, 5, 10}(Math.Max(0, cboYears.SelectedIndex))
            Return New JournalSuggestionRequest With {
                .Keywords = clbKeywords.CheckedItems.Cast(Of String)().ToList(),
                .MatchAll = rbAll.Checked,
                .SinceDate = JournalSuggestionService.SinceDate(_today, years)
            }
        End Function


        Private Sub RefreshRequest()
            If _stage <> Stage.Keywords Then Return
            Dim request As JournalSuggestionRequest = CurrentRequest()
            Dim count As Integer = JournalSuggestionService.CleanKeywords(request).Count
            lblRequest.Text = JournalSuggestionService.RequestLine(request) &
                If(request.Keywords.Count > JournalSuggestionService.MaximumKeywords, Environment.NewLine & "Only the first " & JournalSuggestionService.MaximumKeywords.ToString(CultureInfo.CurrentCulture) & " keywords are searched.", String.Empty)
            txtAddress.Text = If(count = 0, String.Empty, JournalSuggestionService.JournalsUrl(request))
            btnPrimary.Enabled = count > 0 AndAlso Not IsWaiting()
        End Sub


        ' ---------------------------------------------------------------
        ' Search
        ' ---------------------------------------------------------------

        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Keywords : Await SearchAsync()
                Case Stage.Evidence : AddChecked()
            End Select
        End Function


        Friend Async Function SearchAsync() As Task

            If _stage <> Stage.Keywords OrElse _cancellation IsNot Nothing OrElse IsWaiting() Then Return
            Dim request As JournalSuggestionRequest = CurrentRequest()
            If JournalSuggestionService.CleanKeywords(request).Count = 0 Then Return

            _stage = Stage.Running
            _cancellation = New CancellationTokenSource()
            btnPrimary.Enabled = False
            btnCancel.Text = "Stop"
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = "Searching OpenAlex..."

            Try
                Dim result As JournalSuggestionsResult = Await _source.SearchAsync(request, _cancellation.Token)
                ShowEvidence(result)
            Catch ex As OperationCanceledException When _cancellation.IsCancellationRequested
                ShowKeywords()
                lblStatus.Text = "The search was stopped. Nothing was changed."
            Catch ex As Exception When TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse
                                       TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
                ShowKeywords()
                Dim busy As OnlineServiceBusyException = TryCast(ex, OnlineServiceBusyException)
                If busy IsNot Nothing AndAlso Not busy.DailyAllowanceUsed Then
                    _waitUntil = DateTime.UtcNow.Add(If(busy.RetryAfter, TimeSpan.FromMinutes(1)))
                    waitTimer.Start()
                End If
                lblStatus.ForeColor = UiTheme.InfoColor()
                lblStatus.Text = OnlineAccess.Describe(ex, "OpenAlex") & " Nothing was changed."
                RefreshRequest()
                If IsWaiting() Then RefreshWait()
            Finally
                _cancellation?.Dispose()
                _cancellation = Nothing
                btnCancel.Text = "Cancel"
                btnCancel.Enabled = True
            End Try

        End Function


        Private Function IsWaiting() As Boolean
            Return _waitUntil.HasValue AndAlso DateTime.UtcNow < _waitUntil.Value
        End Function


        ' After a busy answer, Search waits the time OpenAlex asked for.
        Private Sub RefreshWait()
            If IsWaiting() Then
                btnPrimary.Text = "Search (" & CInt(Math.Ceiling((_waitUntil.Value - DateTime.UtcNow).TotalSeconds)).ToString(CultureInfo.CurrentCulture) & ")"
                btnPrimary.Enabled = False
                Return
            End If
            waitTimer.Stop()
            _waitUntil = Nothing
            If _stage = Stage.Keywords Then
                btnPrimary.Text = "Search"
                RefreshRequest()
            End If
        End Sub


        ' ---------------------------------------------------------------
        ' Evidence
        ' ---------------------------------------------------------------

        Private Sub ShowEvidence(result As JournalSuggestionsResult)

            _result = result
            _stage = Stage.Evidence
            _sortColumn = MatchesColumn
            If result.ExamplesBusy IsNot Nothing Then NoteExamplesBusy(result.ExamplesBusy)
            body.Controls.Clear()
            body.Controls.Add(evidencePanel)
            btnBack.Visible = True
            btnPrimary.Text = "Add Checked to Shortlist"

            Dim since As String = result.Request.SinceDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
            lblIntro.Text =
                If(result.Journals.Count = 0,
                   "No journals found for these keywords. Try fewer keywords, match any keyword, or search more years.",
                   "Journals that published articles mentioning " & JournalSuggestionService.SearchText(result.Request) & " since " & since & ", most matches first. " &
                   "Check the ones to add to the shortlist as Considering.")

            _allowCheck = True
            lvJournals.BeginUpdate()
            Try
                lvJournals.Items.Clear()
                For Each journal As JournalSuggestion In result.Journals
                    lvJournals.Items.Add(CreateItem(journal))
                Next
                SortItems()
            Finally
                lvJournals.EndUpdate()
                _allowCheck = False
            End Try

            Dim notes As New List(Of String)
            If result.TotalsError.Length > 0 Then notes.Add("Totals couldn't be loaded: " & result.TotalsError)
            If result.DetailsError.Length > 0 Then notes.Add("Journal details couldn't be loaded: " & result.DetailsError)
            If result.ExamplesError.Length > 0 Then notes.Add("Examples couldn't be loaded: " & result.ExamplesError)
            lblStatus.ForeColor = If(notes.Count > 0, UiTheme.InfoColor(), UiTheme.SecondaryText())
            lblStatus.Text = String.Join(Environment.NewLine, notes)
            lblFooter.Text = JournalSuggestionService.Footnote & " Data from OpenAlex (CC0), " &
                result.RetrievedUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & "."

            For Each control As Control In examplesPanel.Controls.Cast(Of Control)().ToList()
                control.Dispose()
            Next
            examplesPanel.Controls.Clear()
            If lvJournals.Items.Count > 0 Then lvJournals.Items(0).Selected = True
            RefreshAddButton()
            AcceptButton = btnPrimary

        End Sub


        Private Function CreateItem(journal As JournalSuggestion) As ListViewItem
            Dim onShortlist As Boolean = _isOnShortlist(journal)
            Dim yours As String = If(onShortlist, "On your shortlist", _yoursFor(journal))
            Dim item As New ListViewItem({
                journal.Name,
                If(journal.Publisher, String.Empty),
                journal.MatchingArticles.ToString("N0", CultureInfo.CurrentCulture),
                JournalSuggestionService.ShareText(journal),
                JournalSuggestionService.AccessText(journal),
                yours
            }) With {
                .Tag = journal,
                .Checked = False,
                .ToolTipText = journal.Name & If(journal.Publisher.Length > 0, " · " & journal.Publisher, String.Empty) &
                               If(journal.Issns.Count > 0, " · ISSN " & String.Join(", ", journal.Issns), String.Empty)
            }
            If onShortlist Then item.ForeColor = UiTheme.MutedText()
            Return item
        End Function


        ' A journal already on the shortlist can't be added again.
        Private Sub JournalChecking(sender As Object, e As ItemCheckEventArgs)
            If _allowCheck Then Return
            Dim journal As JournalSuggestion = TryCast(lvJournals.Items(e.Index).Tag, JournalSuggestion)
            If journal IsNot Nothing AndAlso _isOnShortlist(journal) Then e.NewValue = CheckState.Unchecked
        End Sub


        Private Sub RefreshAddButton()
            If _stage <> Stage.Evidence Then Return
            Dim count As Integer = lvJournals.CheckedItems.Count
            btnPrimary.Text = If(count = 1, "Add 1 to Shortlist", If(count = 0, "Add Checked to Shortlist", "Add " & count.ToString(CultureInfo.CurrentCulture) & " to Shortlist"))
            btnPrimary.Enabled = count > 0
        End Sub


        ' Sorting by journal, publisher, matches, or share only: never by a
        ' metric.
        Private Sub SortBy(sender As Object, e As ColumnClickEventArgs)
            If e.Column > ShareColumn Then Return
            _sortColumn = e.Column
            SortItems()
        End Sub


        Private Sub SortItems()
            Dim items As List(Of ListViewItem) = lvJournals.Items.Cast(Of ListViewItem)().ToList()
            Dim journalOf As Func(Of ListViewItem, JournalSuggestion) = Function(item) DirectCast(item.Tag, JournalSuggestion)
            Dim share As Func(Of JournalSuggestion, Double) = Function(journal) If(journal.AllArticles.HasValue AndAlso journal.AllArticles.Value > 0, journal.MatchingArticles / CDbl(journal.AllArticles.Value), -1)
            Select Case _sortColumn
                Case JournalColumn : items = items.OrderBy(Function(item) journalOf(item).Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                ' An unknown publisher sorts last.
                Case PublisherColumn : items = items.OrderBy(Function(item) String.IsNullOrWhiteSpace(journalOf(item).Publisher)).
                                           ThenBy(Function(item) journalOf(item).Publisher, StringComparer.CurrentCultureIgnoreCase).
                                           ThenBy(Function(item) journalOf(item).Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                Case ShareColumn : items = items.OrderByDescending(Function(item) share(journalOf(item))).ThenBy(Function(item) journalOf(item).Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                Case Else : items = items.OrderByDescending(Function(item) journalOf(item).MatchingArticles).ThenBy(Function(item) journalOf(item).Name, StringComparer.CurrentCultureIgnoreCase).ToList()
            End Select
            Dim wasAllowed As Boolean = _allowCheck
            _allowCheck = True
            lvJournals.BeginUpdate()
            Try
                lvJournals.Items.Clear()
                lvJournals.Items.AddRange(items.ToArray())
            Finally
                lvJournals.EndUpdate()
                _allowCheck = wasAllowed
            End Try
        End Sub


        ' Up to three recent matching articles, fetched when first needed.
        Private Async Function ShowExamplesAsync() As Task

            If _stage <> Stage.Evidence OrElse lvJournals.SelectedItems.Count = 0 Then Return
            Dim journal As JournalSuggestion = DirectCast(lvJournals.SelectedItems(0).Tag, JournalSuggestion)

            If Not journal.ExamplesLoaded AndAlso _result IsNot Nothing Then
                ' One fetch at a time; the journal selected when it ends is shown next.
                If _examplesCancellation IsNot Nothing Then
                    ShowExampleLinks(journal, "Loading recent matching articles...")
                    Return
                End If
                ' OpenAlex asked PaperRoute to wait: no request until then.
                If _examplesBusy IsNot Nothing AndAlso DateTime.UtcNow < _examplesBusyUntil Then
                    ShowExampleLinks(journal, "Examples couldn't be loaded: " & OnlineAccess.Describe(_examplesBusy, "OpenAlex"))
                    Return
                End If
                ShowExampleLinks(journal, "Loading recent matching articles...")
                _examplesCancellation = New CancellationTokenSource()
                Try
                    journal.Examples = Await _source.ExamplesAsync(_result.Request, journal.OpenAlexId, _examplesCancellation.Token)
                    journal.ExamplesLoaded = True
                Catch ex As OperationCanceledException When _examplesCancellation.IsCancellationRequested
                    Return
                Catch ex As Exception When TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse
                                           TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is InvalidOperationException
                    Dim busy As OnlineServiceBusyException = TryCast(ex, OnlineServiceBusyException)
                    If busy IsNot Nothing Then NoteExamplesBusy(busy)
                    If Not IsDisposed Then ShowExampleLinks(journal, "Examples couldn't be loaded: " & OnlineAccess.Describe(ex, "OpenAlex"))
                    Return
                Finally
                    _examplesCancellation?.Dispose()
                    _examplesCancellation = Nothing
                End Try
                If IsDisposed OrElse _stage <> Stage.Evidence OrElse lvJournals.SelectedItems.Count = 0 Then Return
                If lvJournals.SelectedItems(0).Tag IsNot journal Then
                    Await ShowExamplesAsync()
                    Return
                End If
            End If

            ShowExampleLinks(journal, If(journal.Examples.Count = 0, "No example articles to show.", String.Empty))

        End Function


        ' No examples request until OpenAlex's wait is over.
        Private Sub NoteExamplesBusy(busy As OnlineServiceBusyException)
            _examplesBusy = busy
            _examplesBusyUntil = If(busy.DailyAllowanceUsed, DateTime.UtcNow.Date.AddDays(1), DateTime.UtcNow.Add(If(busy.RetryAfter, TimeSpan.FromMinutes(1))))
        End Sub


        Private Sub ShowExampleLinks(journal As JournalSuggestion, message As String)

            examplesPanel.SuspendLayout()
            For Each control As Control In examplesPanel.Controls.Cast(Of Control)().ToList()
                control.Dispose()
            Next
            examplesPanel.Controls.Clear()
            toolTip.RemoveAll()

            Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), evidencePanel.ClientSize.Width - LogicalToDeviceUnits(8))
            Dim heading As New Label With {
                .Text = "Recent matching articles in " & journal.Name,
                .AutoSize = True, .UseMnemonic = False, .MaximumSize = New Size(width, 0),
                .ForeColor = UiTheme.SecondaryText(), .Margin = New Padding(0, 0, 0, 2)
            }
            examplesPanel.Controls.Add(heading)

            If message.Length > 0 Then
                examplesPanel.Controls.Add(New Label With {.Text = message, .AutoSize = True, .UseMnemonic = False, .MaximumSize = New Size(width, 0), .ForeColor = UiTheme.MutedText(), .Margin = New Padding(0, 2, 0, 2)})
            Else
                For Each example As EvidenceExample In journal.Examples
                    Dim text As String = example.Title & If(example.Year.HasValue, " (" & example.Year.Value.ToString(CultureInfo.InvariantCulture) & ")", String.Empty)
                    Dim url As String = If(example.Doi.Length > 0, "https://doi.org/" & example.Doi, String.Empty)
                    Dim link As New LinkLabel With {
                        .Text = text, .AutoSize = True, .UseMnemonic = False, .MaximumSize = New Size(width, 0), .Margin = New Padding(0, 2, 0, 2),
                        .LinkBehavior = LinkBehavior.HoverUnderline, .LinkColor = UiTheme.AccentColor(), .ActiveLinkColor = UiTheme.AccentSecondaryColor(),
                        .VisitedLinkColor = UiTheme.AccentColor(), .Tag = url
                    }
                    If url.Length = 0 OrElse Not UrlSafetyService.IsSafeHttpUrl(url) Then
                        link.LinkArea = New LinkArea(0, 0)
                    Else
                        toolTip.SetToolTip(link, url)
                        AddHandler link.LinkClicked, AddressOf OpenExample
                    End If
                    examplesPanel.Controls.Add(link)
                Next
            End If
            examplesPanel.ResumeLayout(True)

        End Sub


        Private Sub OpenExample(sender As Object, e As LinkLabelLinkClickedEventArgs)
            Try
                UrlSafetyService.OpenInBrowser(CStr(DirectCast(sender, LinkLabel).Tag))
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open Article", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        Private Sub AddChecked()
            If _result Is Nothing Then Return
            Chosen.Clear()
            Chosen.AddRange(lvJournals.CheckedItems.Cast(Of ListViewItem)().
                Select(Function(item) DirectCast(item.Tag, JournalSuggestion)).
                Where(Function(journal) Not _isOnShortlist(journal)))
            If Chosen.Count = 0 Then Return
            DialogResult = DialogResult.OK
            Close()
        End Sub


        Private Sub CancelClicked(sender As Object, e As EventArgs)
            _examplesCancellation?.Cancel()
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                btnCancel.Enabled = False
                Return
            End If
            DialogResult = DialogResult.Cancel
            Close()
        End Sub


        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            _examplesCancellation?.Cancel()
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                e.Cancel = True
                Return
            End If
            waitTimer.Stop()
            MyBase.OnFormClosing(e)
        End Sub


        ' Panels out of the form's controls are disposed here too.
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                keywordsPanel.Dispose()
                evidencePanel.Dispose()
                waitTimer.Dispose()
                toolTip.Dispose()
                _cancellation?.Dispose()
                _examplesCancellation?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub


        ' For tests.
        Friend ReadOnly Property KeywordsList As CheckedListBox
            Get
                Return clbKeywords
            End Get
        End Property

        Friend ReadOnly Property JournalsList As ListView
            Get
                Return lvJournals
            End Get
        End Property

        Friend ReadOnly Property RequestText As String
            Get
                Return lblRequest.Text
            End Get
        End Property

        Friend ReadOnly Property AddressText As String
            Get
                Return txtAddress.Text
            End Get
        End Property

        Friend ReadOnly Property IntroText As String
            Get
                Return lblIntro.Text
            End Get
        End Property

        Friend ReadOnly Property StatusText As String
            Get
                Return lblStatus.Text
            End Get
        End Property

        Friend ReadOnly Property ExampleTexts As List(Of String)
            Get
                Return examplesPanel.Controls.Cast(Of Control)().Select(Function(item) item.Text).ToList()
            End Get
        End Property

        Friend ReadOnly Property PrimaryButton As Button
            Get
                Return btnPrimary
            End Get
        End Property

        Friend ReadOnly Property CancelButtonForTest As Button
            Get
                Return btnCancel
            End Get
        End Property

        Friend ReadOnly Property AddKeywordBox As TextBox
            Get
                Return txtAdd
            End Get
        End Property

        Friend Sub AddKeywordForTest(text As String)
            txtAdd.Text = text
            AddKeyword()
        End Sub

        Friend Sub AddCheckedForTest()
            AddChecked()
        End Sub

        Friend Function ShowExamplesForTest() As Task
            Return ShowExamplesAsync()
        End Function

        Friend Sub SortByForTest(column As Integer)
            SortBy(lvJournals, New ColumnClickEventArgs(column))
        End Sub

    End Class

End Namespace
