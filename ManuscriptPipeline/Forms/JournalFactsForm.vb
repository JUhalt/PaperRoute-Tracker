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

    ' Look Up Facts (#87): says what will be sent, looks the journal up in
    ' OpenAlex and DOAJ, and previews what was found. Checked rows fill empty
    ' fields or refresh looked-up facts; nothing the researcher entered is
    ' replaced, and nothing is saved until Save.
    Friend Class JournalFactsForm
        Inherits Form

        Private Enum Stage
            Ready
            Running
            Preview
        End Enum

        Private ReadOnly _record As JournalRecord
        Private ReadOnly _source As IJournalFactsSource
        Private _stage As Stage = Stage.Ready
        Private _cancellation As CancellationTokenSource
        Private _plan As JournalFactsPlan
        Private _picked As OpenAlexSourceMatch
        Private _allowCheck As Boolean

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly searchRow As New TableLayoutPanel()
        Private ReadOnly txtSearch As New TextBox()
        Private ReadOnly btnFind As New ActionButton()
        Private ReadOnly lvMatches As New ListView()
        Private ReadOnly lvChanges As New ListView()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly lblFooter As New Label()
        Private ReadOnly btnPrimary As New ActionButton()
        Private ReadOnly btnCancel As New ActionButton()
        Private ReadOnly body As New Panel()
        Private ReadOnly root As New TableLayoutPanel()


        ' On a small screen or at a high scale, the window keeps its buttons
        ' above the taskbar.
        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        ' record: a copy, which Save changes and returns as Result.
        Public Sub New(record As JournalRecord, source As IJournalFactsSource)

            _record = If(record, New JournalRecord())
            _source = source

            BuildInterface()
            ShowReady()
            UiPolish.ApplyDialog(Me)

        End Sub


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Result As JournalRecord


        Private Sub BuildInterface()

            SuspendLayout()

            Text = "Look Up Journal Facts"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(860, 560)
            MinimumSize = New Size(620, 440)
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi

            root.Dock = DockStyle.Fill
            root.ColumnCount = 1
            root.RowCount = 5
            root.Padding = New Padding(18, 16, 18, 14)
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.AutoSize = True
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 10)

            ' Find by name, for a journal without an ISSN.
            searchRow.AutoSize = True
            searchRow.Dock = DockStyle.Top
            searchRow.ColumnCount = 3
            searchRow.Margin = New Padding(0, 0, 0, 8)
            searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            Dim lblSearch As New Label With {.Text = "Journal &name", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 6, 8, 6)}
            txtSearch.Dock = DockStyle.Fill
            txtSearch.MaxLength = 200
            txtSearch.Margin = New Padding(0, 4, 8, 4)
            btnFind.Text = "Find"
            btnFind.AutoSize = True
            btnFind.MinimumSize = New Size(80, 32)
            btnFind.Margin = New Padding(0, 2, 0, 2)
            AddHandler btnFind.Click, Async Sub(sender, e) Await FindAsync()
            AddHandler txtSearch.KeyDown,
                Async Sub(sender, e)
                    If e.KeyCode = Keys.Enter Then
                        e.SuppressKeyPress = True
                        Await FindAsync()
                    End If
                End Sub
            searchRow.Controls.Add(lblSearch, 0, 0)
            searchRow.Controls.Add(txtSearch, 1, 0)
            searchRow.Controls.Add(btnFind, 2, 0)

            ConfigureList(lvMatches, checkBoxes:=False)
            lvMatches.AccessibleName = "Journals found in OpenAlex"
            lvMatches.Columns.Add("Journal", LogicalToDeviceUnits(330))
            lvMatches.Columns.Add("Publisher", LogicalToDeviceUnits(220))
            lvMatches.Columns.Add("ISSN", LogicalToDeviceUnits(110))
            lvMatches.Columns.Add("Works", LogicalToDeviceUnits(90), HorizontalAlignment.Right)
            AddHandler lvMatches.SelectedIndexChanged, Sub(sender, e) MatchChanged()
            AddHandler lvMatches.DoubleClick, Async Sub(sender, e) If _picked IsNot Nothing Then Await LookUpAsync()

            ConfigureList(lvChanges, checkBoxes:=True)
            lvChanges.AccessibleName = "What was found"
            lvChanges.Columns.Add("Field", LogicalToDeviceUnits(170))
            lvChanges.Columns.Add("Found", LogicalToDeviceUnits(260))
            lvChanges.Columns.Add("Now", LogicalToDeviceUnits(160))
            lvChanges.Columns.Add("Source", LogicalToDeviceUnits(80))
            lvChanges.Columns.Add("What happens", LogicalToDeviceUnits(140))
            AddHandler lvChanges.ItemCheck, AddressOf ChangeChecking

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
            btnPrimary.Role = ActionButtonRole.Primary
            btnPrimary.AutoSize = True
            btnPrimary.MinimumSize = New Size(96, 34)
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            btnCancel.Text = "Cancel"
            btnCancel.AutoSize = True
            btnCancel.MinimumSize = New Size(96, 34)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            ' Esc stops a request, or closes; a running request vetoes the close.
            CancelButton = btnCancel
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(searchRow, 0, 1)
            root.Controls.Add(body, 0, 2)
            root.Controls.Add(status, 0, 3)
            root.Controls.Add(buttons, 0, 4)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    lblIntro.MaximumSize = New Size(width, 0)
                    lblStatus.MaximumSize = New Size(width, 0)
                    lblFooter.MaximumSize = New Size(width, 0)
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        ' UiPolish has no ListView branch, so the lists take the theme here.
        Private Sub ConfigureList(list As ListView, checkBoxes As Boolean)
            list.Dock = DockStyle.Fill
            list.View = View.Details
            list.FullRowSelect = True
            list.MultiSelect = False
            list.HideSelection = False
            list.CheckBoxes = checkBoxes
            list.ShowItemToolTips = True
            list.BackColor = UiTheme.CardBackground()
            list.ForeColor = UiTheme.PrimaryText()
        End Sub


        Private ReadOnly Property Issns As List(Of String)
            Get
                Return IssnService.NormalizeList(_record.Issns)
            End Get
        End Property


        Private Sub ShowReady()

            _stage = Stage.Ready
            body.Controls.Clear()
            lblFooter.Text = String.Empty

            Dim name As String = If(String.IsNullOrWhiteSpace(_record.Name), "this journal", _record.Name.Trim())

            If Issns.Count > 0 Then
                searchRow.Visible = False
                lblIntro.Text =
                    "PaperRoute will look up " & name & " in OpenAlex and DOAJ, two open indexes, and send them only its ISSN (" & String.Join(", ", Issns.Take(2)) & "), and any other ISSN OpenAlex lists for it." &
                    Environment.NewLine & Environment.NewLine &
                    "You'll see what was found before anything is saved. Empty fields are filled; nothing you entered is replaced."
                lblStatus.Text = String.Empty
            Else
                searchRow.Visible = True
                txtSearch.Text = If(_record.Name, String.Empty).Trim()
                lblIntro.Text =
                    name & " has no ISSN yet, so first find it in OpenAlex by name. PaperRoute sends only the name you type, and then the ISSNs, or the OpenAlex id, of the journal you pick." &
                    Environment.NewLine & Environment.NewLine &
                    "Pick the journal from the list, then choose Look Up. You'll see what was found before anything is saved."
                body.Controls.Add(lvMatches)
                lblStatus.Text = "Choose Find to search OpenAlex."
            End If

            btnPrimary.Text = "Look Up"
            btnPrimary.Enabled = Issns.Count > 0 OrElse _picked IsNot Nothing
            btnCancel.Text = "Cancel"
            btnCancel.Enabled = True
            AcceptButton = If(Issns.Count > 0, btnPrimary, Nothing)

        End Sub


        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Ready : Await LookUpAsync()
                Case Stage.Preview : SaveChecked()
            End Select
        End Function


        Friend Async Function FindAsync() As Task

            If _stage <> Stage.Ready OrElse _cancellation IsNot Nothing Then Return
            Dim name As String = txtSearch.Text.Trim()
            If name.Length = 0 Then
                lblStatus.Text = "Type the journal's name, then choose Find."
                Return
            End If

            _cancellation = New CancellationTokenSource()
            btnFind.Enabled = False
            btnCancel.Text = "Stop"
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = "Searching OpenAlex..."
            lvMatches.Items.Clear()
            _picked = Nothing

            Try
                Dim matches As List(Of OpenAlexSourceMatch) = Await _source.SearchAsync(name, _cancellation.Token)
                For Each match As OpenAlexSourceMatch In matches
                    lvMatches.Items.Add(New ListViewItem({
                        match.DisplayName,
                        match.Publisher,
                        If(match.IssnL.Length > 0, match.IssnL, match.Issns.FirstOrDefault()),
                        If(match.WorksCount.HasValue, match.WorksCount.Value.ToString("N0", CultureInfo.CurrentCulture), String.Empty)
                    }) With {.Tag = match})
                Next
                lblStatus.Text = If(matches.Count = 0,
                    "OpenAlex found no journal by that name. Check the spelling, or add the journal's ISSN in Edit... and look it up by ISSN.",
                    "Pick the journal, then choose Look Up. Journals with the same name are told apart by publisher and ISSN.")
            Catch ex As OperationCanceledException When _cancellation.IsCancellationRequested
                lblStatus.Text = "The search was stopped."
            Catch ex As Exception When TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse
                                       TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is InvalidOperationException
                lblStatus.ForeColor = UiTheme.InfoColor()
                lblStatus.Text = OnlineAccess.Describe(ex, "OpenAlex")
            Finally
                _cancellation.Dispose()
                _cancellation = Nothing
                btnFind.Enabled = True
                btnCancel.Text = "Cancel"
                btnCancel.Enabled = True
            End Try

        End Function


        Private Sub MatchChanged()
            _picked = If(lvMatches.SelectedItems.Count = 0, Nothing, TryCast(lvMatches.SelectedItems(0).Tag, OpenAlexSourceMatch))
            If _stage = Stage.Ready Then btnPrimary.Enabled = Issns.Count > 0 OrElse _picked IsNot Nothing
        End Sub


        Friend Async Function LookUpAsync() As Task

            If _stage <> Stage.Ready OrElse _cancellation IsNot Nothing Then Return
            If Issns.Count = 0 AndAlso _picked Is Nothing Then Return

            _stage = Stage.Running
            _cancellation = New CancellationTokenSource()
            btnPrimary.Enabled = False
            btnFind.Enabled = False
            btnCancel.Text = "Stop"
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = "Looking up in OpenAlex and DOAJ..."

            Dim issnsToSend As IEnumerable(Of String) = If(Issns.Count > 0, Issns, If(_picked?.Issns, New List(Of String)()))
            ' An id matters only for a journal without an ISSN.
            Dim openAlexId As String = If(_picked IsNot Nothing, _picked.Id, _record.OpenAlexId)

            Try
                Dim lookup As JournalFactsLookup = Await _source.LookupAsync(issnsToSend, openAlexId, _cancellation.Token)
                ShowPreview(JournalFactsService.Plan(_record, lookup))
            Catch ex As OperationCanceledException When _cancellation.IsCancellationRequested
                _stage = Stage.Ready
                ShowReady()
                lblStatus.Text = "The lookup was stopped. Nothing was changed."
            Catch ex As Exception When TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is InvalidOperationException OrElse
                                       TypeOf ex Is Net.Http.HttpRequestException OrElse TypeOf ex Is TaskCanceledException
                _stage = Stage.Ready
                ShowReady()
                btnPrimary.Enabled = False
                lblStatus.ForeColor = UiTheme.InfoColor()
                lblStatus.Text = OnlineAccess.Describe(ex, "the journal indexes") & " Nothing was changed."
            Finally
                _cancellation?.Dispose()
                _cancellation = Nothing
                btnFind.Enabled = True
                btnCancel.Enabled = True
                btnCancel.Text = "Cancel"
            End Try

        End Function


        Private Sub ShowPreview(plan As JournalFactsPlan)

            _plan = plan
            _stage = Stage.Preview
            searchRow.Visible = False
            body.Controls.Clear()
            body.Controls.Add(lvChanges)

            _allowCheck = True
            lvChanges.BeginUpdate()
            Try
                lvChanges.Items.Clear()
                For Each change As JournalFactChange In plan.Changes
                    Dim item As New ListViewItem({change.Field, change.Found, change.Current, change.Source, change.WhatHappens}) With {
                        .Tag = change,
                        .Checked = change.Selected AndAlso change.CanApply,
                        .ToolTipText = If(change.CanApply, change.Found, "Your own value is kept. To use the one found, change it in Edit...")
                    }
                    If Not change.CanApply Then item.ForeColor = UiTheme.MutedText()
                    lvChanges.Items.Add(item)
                Next
            Finally
                lvChanges.EndUpdate()
                _allowCheck = False
            End Try

            Dim answered As Boolean = plan.Lookup.DoajChecked OrElse plan.Lookup.OpenAlexChecked
            lblIntro.Text =
                If(plan.Changes.Count = 0,
                   "Everything found matches what's saved. Save marks the facts as checked today.",
                   "Checked rows fill empty fields and refresh looked-up facts. Nothing you entered is replaced.")

            Dim notes As New List(Of String)(plan.Notes)
            If plan.Unchanged > 0 Then notes.Add(plan.Unchanged.ToString(CultureInfo.CurrentCulture) & If(plan.Unchanged = 1, " fact is unchanged.", " facts are unchanged."))
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = String.Join(Environment.NewLine, notes)
            lblFooter.Text = JournalFactCatalog.Attribution & " Checked " & plan.Lookup.CheckedUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & "."

            btnPrimary.Text = "Save"
            btnPrimary.Enabled = answered
            AcceptButton = btnPrimary

        End Sub


        ' A kept value (the researcher's own) can't be checked.
        Private Sub ChangeChecking(sender As Object, e As ItemCheckEventArgs)
            If _allowCheck Then Return
            Dim change As JournalFactChange = TryCast(lvChanges.Items(e.Index).Tag, JournalFactChange)
            If change IsNot Nothing AndAlso Not change.CanApply Then e.NewValue = CheckState.Unchecked
        End Sub


        Private Sub SaveChecked()

            If _plan Is Nothing Then Return

            For Each item As ListViewItem In lvChanges.Items
                Dim change As JournalFactChange = TryCast(item.Tag, JournalFactChange)
                If change IsNot Nothing Then change.Selected = item.Checked AndAlso change.CanApply
            Next

            JournalFactsService.Apply(_record, _plan)
            _Result = _record
            DialogResult = DialogResult.OK
            Close()

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


        ' Closing while a request runs stops it first.
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                e.Cancel = True
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub


        ' Either list may be out of the form's controls, so both are disposed here.
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                lvMatches.Dispose()
                lvChanges.Dispose()
                _cancellation?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub


        ' For tests.
        Friend ReadOnly Property ChangesList As ListView
            Get
                Return lvChanges
            End Get
        End Property

        Friend ReadOnly Property MatchesList As ListView
            Get
                Return lvMatches
            End Get
        End Property

        Friend ReadOnly Property SearchBox As TextBox
            Get
                Return txtSearch
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

        Friend ReadOnly Property FooterText As String
            Get
                Return lblFooter.Text
            End Get
        End Property

        Friend ReadOnly Property PrimaryButton As Button
            Get
                Return btnPrimary
            End Get
        End Property

        Friend Sub SaveForTest()
            SaveChecked()
        End Sub

    End Class

End Namespace
