Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Update from OpenAlex (#91): says exactly what will be sent, reads the
    ' works on the researcher's ORCID record and their published manuscripts,
    ' and lets them confirm which works are theirs before anything is saved.
    Friend Class CitationsUpdateForm
        Inherits Form

        Private Enum Stage
            Ready
            Running
            Confirm
        End Enum

        Private ReadOnly _orcid As String
        Private ReadOnly _manuscripts As List(Of (Doi As String, Title As String))
        Private ReadOnly _previous As CitationSnapshot
        Private ReadOnly _source As ICitationSource
        Private _stage As Stage = Stage.Ready
        Private _cancellation As CancellationTokenSource
        Private _lookup As CitationLookup
        Private _allowCheck As Boolean

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly chkLinked As New CheckBox()
        Private ReadOnly lvWorks As New ListView()
        Private ReadOnly lblSummary As New Label()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnCancel As New Button()
        Private ReadOnly body As New Panel()


        Public Sub New(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), previous As CitationSnapshot, source As ICitationSource)
            _orcid = orcid
            _manuscripts = If(manuscriptDois, Enumerable.Empty(Of (Doi As String, Title As String))()).ToList()
            _previous = previous
            _source = source
            BuildInterface()
            ShowReady()
            UiPolish.ApplyDialog(Me)
        End Sub


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Result As CitationSnapshot


        Private Sub BuildInterface()

            SuspendLayout()
            Text = "Update Your Citations"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(900, 600)
            MinimumSize = New Size(640, 460)
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.AutoSize = True
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 10)

            chkLinked.Text = "Also ask OpenAlex which other works it links to my ORCID iD (sends your iD to OpenAlex)"
            chkLinked.AutoSize = True
            chkLinked.Margin = New Padding(0, 0, 0, 8)

            lvWorks.Dock = DockStyle.Fill
            lvWorks.View = View.Details
            lvWorks.CheckBoxes = True
            lvWorks.FullRowSelect = True
            lvWorks.MultiSelect = False
            lvWorks.HideSelection = False
            lvWorks.ShowItemToolTips = True
            lvWorks.BackColor = UiTheme.CardBackground()
            lvWorks.ForeColor = UiTheme.PrimaryText()
            lvWorks.AccessibleName = "Works found"
            lvWorks.Columns.Add("Work", LogicalToDeviceUnits(400))
            lvWorks.Columns.Add("Year", LogicalToDeviceUnits(60), HorizontalAlignment.Right)
            lvWorks.Columns.Add("Journal", LogicalToDeviceUnits(220))
            lvWorks.Columns.Add("Citations", LogicalToDeviceUnits(90), HorizontalAlignment.Right)
            lvWorks.Groups.Add(New ListViewGroup("orcid", "On your ORCID record"))
            lvWorks.Groups.Add(New ListViewGroup("paperroute", "Your published manuscripts in PaperRoute"))
            lvWorks.Groups.Add(New ListViewGroup("openalex", "Linked to your iD by OpenAlex only (check only works that are yours)"))
            AddHandler lvWorks.ItemChecked, Sub(sender, e) If Not _allowCheck Then RefreshSummary()

            body.Dock = DockStyle.Fill
            body.Margin = New Padding(0)

            lblSummary.AutoSize = True
            lblSummary.UseMnemonic = False
            lblSummary.Font = New Font(Font, FontStyle.Bold)
            lblSummary.Margin = New Padding(0, 8, 0, 0)
            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Margin = New Padding(0, 6, 0, 0)
            Dim status As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Margin = New Padding(0)}
            status.Controls.Add(lblSummary)
            status.Controls.Add(lblStatus)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            btnPrimary.AutoSize = True
            btnPrimary.MinimumSize = New Size(96, 34)
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            btnCancel.Text = "Cancel"
            btnCancel.AutoSize = True
            btnCancel.MinimumSize = New Size(96, 34)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnCancel)
            AcceptButton = btnPrimary
            CancelButton = btnCancel

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(chkLinked, 0, 1)
            root.Controls.Add(body, 0, 2)
            root.Controls.Add(status, 0, 3)
            root.Controls.Add(buttons, 0, 4)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    For Each label As Label In {lblIntro, lblSummary, lblStatus}
                        label.MaximumSize = New Size(width, 0)
                    Next
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub ShowReady()
            _stage = Stage.Ready
            Dim count As Integer = _manuscripts.Count
            lblIntro.Text =
                "PaperRoute will:" & Environment.NewLine &
                "• read the works on your public ORCID record (sends your iD, " & _orcid & ", to ORCID); and" & Environment.NewLine &
                "• look up those works" & If(count > 0, ", and your " & count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " published manuscript", " published manuscripts") & " with a DOI,", String.Empty) &
                " in OpenAlex (sends their DOIs)." & Environment.NewLine & Environment.NewLine &
                "Unpublished titles and abstracts are never sent. You confirm which works are yours before anything is saved."
            chkLinked.Checked = _previous IsNot Nothing AndAlso _previous.IncludeOpenAlexLinked
            chkLinked.Visible = True
            lblSummary.Text = String.Empty
            lblStatus.Text = If(_previous?.RetrievedUtc.HasValue = True,
                                "This replaces the figures from " & _previous.RetrievedUtc.Value.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & ".",
                                String.Empty)
            btnPrimary.Text = "Look Up"
            btnPrimary.Enabled = True
        End Sub


        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Ready : Await LookUpAsync()
                Case Stage.Confirm : SaveConfirmed()
            End Select
        End Function


        Friend Async Function LookUpAsync() As Task

            If _stage <> Stage.Ready OrElse _cancellation IsNot Nothing Then Return
            _stage = Stage.Running
            _cancellation = New CancellationTokenSource()
            btnPrimary.Enabled = False
            chkLinked.Enabled = False
            btnCancel.Text = "Stop"
            lblStatus.ForeColor = UiTheme.SecondaryText()

            Dim progress As New Progress(Of String)(Sub(message) lblStatus.Text = message)
            Try
                Dim lookup As CitationLookup = Await _source.LookupAsync(_orcid, _manuscripts, chkLinked.Checked, If(_previous?.Excluded, New List(Of String)()), progress, _cancellation.Token)
                ShowConfirm(lookup)
            Catch ex As OperationCanceledException When _cancellation.IsCancellationRequested
                ShowReady()
                lblStatus.Text = "The update was stopped. Nothing was changed."
            Catch ex As Exception When TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse
                                       TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
                ShowReady()
                lblStatus.ForeColor = UiTheme.InfoColor()
                lblStatus.Text = OnlineAccess.Describe(ex, If(TypeOf ex Is OnlineServiceBusyException, "OpenAlex", "ORCID or OpenAlex")) & " Nothing was changed."
            Finally
                _cancellation?.Dispose()
                _cancellation = Nothing
                chkLinked.Enabled = True
                btnCancel.Text = "Cancel"
                btnCancel.Enabled = True
            End Try

        End Function


        Private Sub ShowConfirm(lookup As CitationLookup)

            _lookup = lookup
            _stage = Stage.Confirm
            chkLinked.Visible = False
            body.Controls.Clear()
            body.Controls.Add(lvWorks)
            lblIntro.Text = "Check the works that are yours. Only checked works count toward your figures; versions of one work on your ORCID record count once."

            _allowCheck = True
            lvWorks.BeginUpdate()
            Try
                lvWorks.Items.Clear()
                For Each candidate As CitationCandidate In lookup.Candidates
                    Dim work As CitedWork = candidate.Work
                    Dim item As New ListViewItem({
                        If(work.Title.Length > 0, work.Title, work.Doi),
                        If(work.Year.HasValue, work.Year.Value.ToString(CultureInfo.InvariantCulture), String.Empty),
                        work.Journal,
                        work.CitedByCount.ToString("N0", CultureInfo.CurrentCulture)
                    }) With {
                        .Tag = candidate,
                        .Checked = candidate.Selected,
                        .Group = lvWorks.Groups(CInt(candidate.Section)),
                        .ToolTipText = If(candidate.ExcludedBefore, "You left this out last time. ", String.Empty) & If(work.Doi.Length > 0, "DOI " & work.Doi, work.OpenAlexId)
                    }
                    If candidate.ExcludedBefore Then item.ForeColor = UiTheme.MutedText()
                    lvWorks.Items.Add(item)
                Next
            Finally
                lvWorks.EndUpdate()
                _allowCheck = False
            End Try

            Dim notes As New List(Of String)()
            If lookup.NotFound.Count > 0 Then notes.Add("Not found in OpenAlex: " & lookup.NotFound.Count.ToString(CultureInfo.CurrentCulture) & " (" & String.Join("; ", lookup.NotFound.Take(3)) & If(lookup.NotFound.Count > 3, "; …", String.Empty) & ").")
            If lookup.OrcidWithoutDoi > 0 Then notes.Add("On your ORCID record without a DOI: " & lookup.OrcidWithoutDoi.ToString(CultureInfo.CurrentCulture) & ". PaperRoute looks works up by DOI only.")
            If lookup.IncludeOpenAlexLinked AndAlso lookup.OpenAlexLinkedTotal.HasValue Then
                If lookup.OpenAlexLinkedTotal.Value > lookup.OpenAlexLinkedRead Then
                    notes.Add("OpenAlex links " & lookup.OpenAlexLinkedTotal.Value.ToString("N0", CultureInfo.CurrentCulture) & " works to this iD; PaperRoute read the first " & lookup.OpenAlexLinkedRead.ToString("N0", CultureInfo.CurrentCulture) & ".")
                End If
                Dim yours As Integer = lookup.Candidates.Where(Function(item) item.Section <> CitationSection.OpenAlexOnly).Count()
                If lookup.OpenAlexLinkedTotal.Value > 3 * Math.Max(1, yours) Then
                    notes.Add("OpenAlex links many more works to this iD than your ORCID record lists. Some may belong to someone else, so check only works that are yours.")
                End If
            End If
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = String.Join(Environment.NewLine, notes)
            btnPrimary.Text = "Save"
            RefreshSummary()

        End Sub


        Private Sub RefreshSummary()
            If _lookup Is Nothing Then Return
            For Each item As ListViewItem In lvWorks.Items
                DirectCast(item.Tag, CitationCandidate).Selected = item.Checked
            Next
            Dim chosen As List(Of CitedWork) = _lookup.Candidates.Where(Function(item) item.Selected).Select(Function(item) item.Work).ToList()
            Dim summary As CitationSummary = CitationMetricsService.Summarize(chosen, _lookup.RetrievedUtc.Year)
            Dim culture As CultureInfo = CultureInfo.CurrentCulture
            lblSummary.Text = "Checked: " & summary.Works.ToString("N0", culture) & If(summary.Works = 1, " work", " works") &
                " · " & summary.Citations.ToString("N0", culture) & " citations · h-index " & summary.HIndex.ToString(culture) &
                " · i10-index " & summary.I10Index.ToString(culture) & " · g-index " & summary.GIndex.ToString(culture)
            btnPrimary.Enabled = _stage = Stage.Confirm
        End Sub


        Private Sub SaveConfirmed()
            If _lookup Is Nothing Then Return
            RefreshSummary()
            _Result = CitationsService.BuildSnapshot(_lookup, If(_previous?.Excluded, New List(Of String)()))
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


        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                e.Cancel = True
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                lvWorks.Dispose()
                _cancellation?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub


        ' For tests.
        Friend ReadOnly Property WorksList As ListView
            Get
                Return lvWorks
            End Get
        End Property

        Friend ReadOnly Property IntroText As String
            Get
                Return lblIntro.Text
            End Get
        End Property

        Friend ReadOnly Property SummaryText As String
            Get
                Return lblSummary.Text
            End Get
        End Property

        Friend ReadOnly Property StatusText As String
            Get
                Return lblStatus.Text
            End Get
        End Property

        Friend ReadOnly Property LinkedBox As CheckBox
            Get
                Return chkLinked
            End Get
        End Property

        Friend Sub SaveForTest()
            SaveConfirmed()
        End Sub

    End Class

End Namespace
