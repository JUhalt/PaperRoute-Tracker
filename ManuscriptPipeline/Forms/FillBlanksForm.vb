Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Fill Blanks from Crossref (#61): for manuscripts with a DOI, look up
    ' the Crossref record and fill only empty fields, after a preview of
    ' every change.
    Friend Class FillBlanksForm
        Inherits Form

        Private ReadOnly _library As List(Of Manuscript)
        Private ReadOnly _eligible As List(Of Manuscript)
        Private ReadOnly _source As IPublicationSource
        Private ReadOnly _save As Func(Of Boolean)

        Private ReadOnly lstManuscripts As New CheckedListBox()
        Private ReadOnly lstChanges As New ListView()
        Private ReadOnly lblIntro As New Label()
        Private ReadOnly lblProgress As New Label()
        Private ReadOnly body As New Panel()
        Private ReadOnly btnPrimary As New ActionButton()
        Private ReadOnly btnCancel As New ActionButton()

        Private _plan As New List(Of MetadataFill)()
        Private _works As New Dictionary(Of Manuscript, CrossrefMetadataSuggestion)()
        Private _cancellation As CancellationTokenSource = Nothing
        Private _stage As Integer = 0

        Friend Pause As TimeSpan = TimeSpan.FromMilliseconds(250)

        <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
        Friend Property FilledCount As Integer

        Public Sub New(library As List(Of Manuscript), source As IPublicationSource, save As Func(Of Boolean))

            _library = library
            _source = source
            _save = save
            _eligible = library.Where(Function(item) DoiNormalizer.IsValid(DoiNormalizer.Normalize(If(item.Metadata?.Doi, String.Empty)))).
                OrderBy(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).ToList()

            Text = "Fill Blanks from Crossref"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            AutoScaleMode = AutoScaleMode.Dpi
            Font = New Font("Segoe UI", 9.0F)
            ClientSize = New Size(760, 540)
            MinimumSize = New Size(560, 420)

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.Text = "For manuscripts with a DOI, PaperRoute reads the Crossref record and fills fields that are empty, " &
                            "such as the journal, date, volume, issue, pages, and abstract. You see every change first. A field that has a value is never changed."
            lblIntro.AutoSize = True
            lblIntro.MaximumSize = New Size(720, 0)
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 12)
            root.Controls.Add(lblIntro, 0, 0)

            body.Dock = DockStyle.Fill
            body.Margin = New Padding(0)
            root.Controls.Add(body, 0, 1)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
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

            lstManuscripts.Dock = DockStyle.Fill
            lstManuscripts.CheckOnClick = True
            lstManuscripts.IntegralHeight = False
            lstManuscripts.AccessibleName = "Manuscripts with a DOI"
            For Each manuscript As Manuscript In _eligible
                Dim index As Integer = lstManuscripts.Items.Add(ReminderService.SafeManuscriptTitle(manuscript) & "  ·  DOI " & DoiNormalizer.Normalize(manuscript.Metadata.Doi))
                lstManuscripts.SetItemChecked(index, HasBlanks(manuscript))
            Next
            AddHandler lstManuscripts.ItemCheck, Sub(sender, e) If IsHandleCreated Then BeginInvoke(New Action(AddressOf UpdateButtons))
            body.Controls.Add(lstManuscripts)

            If _eligible.Count = 0 Then
                lblIntro.Text &= Environment.NewLine & Environment.NewLine & "No manuscript has a DOI yet. Add one on a manuscript's Overview tab, or use Check for Publications."
                lstManuscripts.Enabled = False
            End If

            UpdateButtons()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Shared Function HasBlanks(manuscript As Manuscript) As Boolean
            Dim metadata As ManuscriptMetadata = manuscript.Metadata
            Return String.IsNullOrWhiteSpace(metadata.PublicationJournal) OrElse Not metadata.PublishedDate.HasValue OrElse
                   String.IsNullOrWhiteSpace(metadata.Volume) OrElse String.IsNullOrWhiteSpace(metadata.Pages) OrElse
                   String.IsNullOrWhiteSpace(metadata.AbstractText)
        End Function


        Private Sub UpdateButtons()
            Select Case _stage
                Case 0
                    Dim count As Integer = lstManuscripts.CheckedIndices.Count
                    btnPrimary.Text = If(count = 1, "Look Up 1 Manuscript", "Look Up " & count.ToString(CultureInfo.CurrentCulture) & " Manuscripts")
                    btnPrimary.Enabled = count > 0
                Case 2
                    Dim count As Integer = lstChanges.CheckedItems.Count
                    btnPrimary.Text = If(count = 1, "Fill 1 Field", "Fill " & count.ToString(CultureInfo.CurrentCulture) & " Fields")
                    btnPrimary.Enabled = count > 0
            End Select
            btnPrimary.Width = TextRenderer.MeasureText(btnPrimary.Text, Font).Width + 36
        End Sub


        Private Async Sub PrimaryClicked(sender As Object, e As EventArgs)
            Select Case _stage
                Case 0 : Await LookUpAsync()
                Case 2 : ApplyChecked()
            End Select
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


        Friend Async Function LookUpAsync() As Task

            Dim chosen As List(Of Manuscript) = lstManuscripts.CheckedIndices.Cast(Of Integer)().Select(Function(index) _eligible(index)).ToList()
            If chosen.Count = 0 Then Return

            _stage = 1
            body.Controls.Remove(lstManuscripts)
            lblProgress.AutoSize = True
            lblProgress.UseMnemonic = False
            lblProgress.Margin = New Padding(0, 24, 0, 0)
            lblProgress.Dock = DockStyle.Top
            body.Controls.Add(lblProgress)
            btnPrimary.Visible = False
            btnCancel.Text = "Stop"

            Dim failures As New List(Of String)()
            Dim stopped As String = String.Empty
            _cancellation = New CancellationTokenSource()

            Try
                For index As Integer = 0 To chosen.Count - 1
                    Dim manuscript As Manuscript = chosen(index)
                    lblProgress.Text = "Looking up " & (index + 1).ToString(CultureInfo.CurrentCulture) & " of " & chosen.Count.ToString(CultureInfo.CurrentCulture) & ":  " & manuscript.Title
                    Try
                        Dim work As CrossrefMetadataSuggestion = Await _source.LookupDoiAsync(DoiNormalizer.Normalize(manuscript.Metadata.Doi), _cancellation.Token)
                        If work Is Nothing Then
                            failures.Add(ReminderService.SafeManuscriptTitle(manuscript) & ": Crossref has no record for its DOI.")
                        Else
                            _works(manuscript) = work
                            _plan.AddRange(PublicationMatchService.PlanFill(manuscript, work))
                        End If
                        If Pause > TimeSpan.Zero Then Await Task.Delay(Pause, _cancellation.Token)
                    Catch ex As CrossrefRateLimitException
                        stopped = ex.Message
                        Exit For
                    Catch ex As OnlineServiceBlockedException
                        stopped = ex.Message
                        Exit For
                    Catch ex As OperationCanceledException When _cancellation.IsCancellationRequested
                        stopped = "The lookup was stopped."
                        Exit For
                    Catch ex As Exception When TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is InvalidOperationException OrElse
                                               TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException
                        failures.Add(ReminderService.SafeManuscriptTitle(manuscript) & ": " & OnlineAccess.Describe(ex, "Crossref"))
                    End Try
                Next
            Finally
                _cancellation.Dispose()
                _cancellation = Nothing
            End Try

            ShowPreview(failures, stopped)

        End Function


        Private Sub ShowPreview(failures As List(Of String), stopped As String)

            _stage = 2
            body.Controls.Remove(lblProgress)
            btnCancel.Text = "Cancel"
            btnCancel.Enabled = True
            btnPrimary.Visible = True

            lblIntro.Text = If(_plan.Count = 0,
                               "Every field Crossref could fill already has a value. Nothing to change.",
                               "Uncheck anything you do not want. Only empty fields are listed; nothing else changes.")
            If failures.Count > 0 Then lblIntro.Text &= Environment.NewLine & "Not looked up:" & Environment.NewLine & String.Join(Environment.NewLine, failures.Select(Function(line) "•  " & line))
            If stopped.Length > 0 Then lblIntro.Text &= Environment.NewLine & "Stopped early: " & stopped

            lstChanges.View = View.Details
            lstChanges.CheckBoxes = True
            lstChanges.FullRowSelect = True
            lstChanges.HeaderStyle = ColumnHeaderStyle.Nonclickable
            lstChanges.Dock = DockStyle.Fill
            lstChanges.AccessibleName = "Changes to make"
            lstChanges.Columns.Add("Manuscript", LogicalToDeviceUnits(250))
            lstChanges.Columns.Add("Field", LogicalToDeviceUnits(130))
            lstChanges.Columns.Add("New value", LogicalToDeviceUnits(330))
            For Each fill As MetadataFill In _plan
                Dim value As String = If(fill.Value.Length > 160, fill.Value.Substring(0, 157) & "...", fill.Value)
                Dim row As New ListViewItem({ReminderService.SafeManuscriptTitle(fill.Manuscript), fill.Field, value}) With {.Checked = True, .Tag = fill}
                lstChanges.Items.Add(row)
            Next
            AddHandler lstChanges.ItemChecked, Sub(sender, e) UpdateButtons()
            body.Controls.Add(lstChanges)

            If _plan.Count = 0 Then
                btnPrimary.Text = "Close"
                btnPrimary.Width = 90
                btnPrimary.Enabled = True
                RemoveHandler btnPrimary.Click, AddressOf PrimaryClicked
                AddHandler btnPrimary.Click, Sub(sender, e) Close()
                btnCancel.Visible = False
            Else
                UpdateButtons()
            End If

            UiPolish.ApplyDialog(Me)

        End Sub


        Friend ReadOnly Property PlannedChanges As List(Of MetadataFill)
            Get
                Return _plan
            End Get
        End Property


        ' All at once: if the save fails, every manuscript goes back.
        Friend Sub ApplyChecked()

            Dim fills As List(Of MetadataFill) = lstChanges.CheckedItems.Cast(Of ListViewItem)().Select(Function(row) DirectCast(row.Tag, MetadataFill)).ToList()
            If fills.Count = 0 Then Return

            Dim touched As List(Of Manuscript) = fills.Select(Function(fill) fill.Manuscript).Distinct().ToList()
            Dim snapshots As Dictionary(Of Manuscript, String) = touched.ToDictionary(Function(item) item, Function(item) JsonSerializer.Serialize(item))

            For Each manuscript As Manuscript In touched
                PublicationMatchService.ApplyFill(manuscript, fills.Where(Function(fill) fill.Manuscript Is manuscript))
                CrossrefApplyService.RecordProvenance(manuscript.Metadata, _works(manuscript))
            Next

            If Not _save() Then
                For Each manuscript As Manuscript In touched
                    Dim index As Integer = _library.IndexOf(manuscript)
                    If index >= 0 Then _library(index) = JsonSerializer.Deserialize(Of Manuscript)(snapshots(manuscript))
                Next
                Return
            End If

            FilledCount = fills.Count
            DialogResult = DialogResult.OK
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

    End Class

End Namespace
