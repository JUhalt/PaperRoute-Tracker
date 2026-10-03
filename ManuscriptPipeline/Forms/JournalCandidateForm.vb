Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Adds or edits one journal on a manuscript's shortlist (#65): its status,
    ' the researcher's reasons, their history with it, and the trust and fit
    ' checks for choosing a journal (#89).
    Public Class JournalCandidateForm
        Inherits Form

        Private ReadOnly _history As Func(Of String, String)

        Private ReadOnly cmbJournal As New ComboBox()
        Private ReadOnly cmbStatus As New ComboBox()
        Private ReadOnly lblHistory As New Label()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly btnOk As New Button()
        Private ReadOnly _checkBoxes As New List(Of CheckBox)()

        ' The journal's stored facts beside the questions (#87); never ticks.
        Private ReadOnly _factsFor As Func(Of String, JournalRecord)
        Private ReadOnly _hints As New Dictionary(Of String, LinkLabel)(StringComparer.Ordinal)

        ' Opens Help at "Choosing a Journal"; tests replace it.
        Friend GuidePrompt As Action = Nothing

        ' The edited values, read after OK.
        <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public Property JournalName As String = String.Empty

        <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public Property Status As CandidateStatus = CandidateStatus.Considering

        <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public Property Notes As String = String.Empty

        <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public Property Checks As New List(Of String)()


        ' On a small screen or at a high scale, the window keeps its buttons
        ' above the taskbar.
        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        Public Sub New(candidate As JournalCandidate, journalNames As IEnumerable(Of String), history As Func(Of String, String), Optional factsFor As Func(Of String, JournalRecord) = Nothing)

            _history = history
            _factsFor = factsFor
            If candidate IsNot Nothing Then
                JournalName = If(candidate.JournalName, String.Empty)
                Status = candidate.Status
                Notes = If(candidate.Notes, String.Empty)
                Checks = If(candidate.Checks, New List(Of String)()).ToList()
            End If

            BuildInterface(candidate Is Nothing, If(journalNames, Enumerable.Empty(Of String)()))
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface(isNew As Boolean, journalNames As IEnumerable(Of String))

            Me.Text = If(isNew, "Add Journal to Shortlist", "Shortlisted Journal")
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            ' Sizes below are at 96 DPI and scale with the display.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(900, 560)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 5,
                .Padding = New Padding(20, 16, 20, 12)
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            ' Journal and status, then the researcher's history with it.
            Dim fields As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 4, .RowCount = 1, .Margin = New Padding(0)}
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            cmbJournal.DropDownStyle = ComboBoxStyle.DropDown
            cmbJournal.AccessibleName = "Journal"
            cmbJournal.Dock = DockStyle.Fill
            cmbJournal.Margin = New Padding(3, 3, 16, 3)
            Dim names As String() = journalNames.Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(Function(item) item, StringComparer.CurrentCultureIgnoreCase).ToArray()
            cmbJournal.Items.AddRange(names)
            cmbJournal.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            cmbJournal.AutoCompleteSource = AutoCompleteSource.ListItems
            cmbJournal.Text = JournalName
            AddHandler cmbJournal.TextChanged, Sub(sender, e) RefreshJournal()

            cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cmbStatus.AccessibleName = "Status"
            cmbStatus.Width = 150
            cmbStatus.Margin = New Padding(3)
            For Each status As CandidateStatus In [Enum].GetValues(GetType(CandidateStatus))
                cmbStatus.Items.Add(JournalShortlistService.StatusName(status))
            Next
            cmbStatus.SelectedIndex = CInt(Status)

            fields.Controls.Add(FieldLabel("&Journal"), 0, 0)
            fields.Controls.Add(cmbJournal, 1, 0)
            fields.Controls.Add(FieldLabel("&Status"), 2, 0)
            fields.Controls.Add(cmbStatus, 3, 0)
            root.Controls.Add(fields, 0, 0)
            root.SetColumnSpan(fields, 2)

            lblHistory.AutoSize = True
            lblHistory.UseMnemonic = False
            lblHistory.ForeColor = UiTheme.SecondaryText()
            lblHistory.Margin = New Padding(3, 4, 3, 8)
            root.Controls.Add(lblHistory, 0, 1)
            root.SetColumnSpan(lblHistory, 2)

            Dim notesPanel As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0, 0, 0, 8)}
            notesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            notesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            txtNotes.Multiline = True
            txtNotes.Height = 58
            txtNotes.Dock = DockStyle.Fill
            txtNotes.ScrollBars = ScrollBars.Vertical
            txtNotes.AccessibleName = "Why this journal"
            txtNotes.Text = Notes
            notesPanel.Controls.Add(FieldLabel("&Why this journal"), 0, 0)
            notesPanel.Controls.Add(txtNotes, 1, 0)
            root.Controls.Add(notesPanel, 0, 2)
            root.SetColumnSpan(notesPanel, 2)

            ' The two sets of checks, side by side.
            Dim answered As HashSet(Of String) = New HashSet(Of String)(Checks, StringComparer.Ordinal)
            Dim trust As SectionCard = ChecksCard(JournalChoiceGuide.TrustHeading, JournalChoiceGuide.TrustChecks, answered)
            Dim attribution As New LinkLabel With {
                .Text = JournalChoiceGuide.Attribution,
                .AutoSize = True,
                .UseMnemonic = False,
                .Dock = DockStyle.Bottom,
                .Padding = New Padding(0, 6, 0, 0),
                .AccessibleName = "Source: Think. Check. Submit."
            }
            ' The source and its license are each linked, as CC BY asks.
            attribution.Links.Clear()
            For Each link As (Text As String, Url As String) In {("thinkchecksubmit.org", JournalChoiceGuide.SourceUrl), ("CC BY 4.0", JournalChoiceGuide.LicenseUrl)}
                attribution.Links.Add(attribution.Text.IndexOf(link.Text, StringComparison.Ordinal), link.Text.Length, link.Url)
            Next
            AddHandler attribution.LinkClicked,
                Sub(sender, e)
                    Dim url As String = CStr(e.Link.LinkData)
                    Try
                        Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
                    Catch ex As Exception When TypeOf ex Is ComponentModel.Win32Exception OrElse TypeOf ex Is InvalidOperationException
                        MessageBox.Show(Me, "PaperRoute could not open " & url & ".", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End Try
                End Sub
            trust.Controls.Add(attribution)
            AddHandler trust.Resize, Sub(sender, e) attribution.MaximumSize = New Size(Math.Max(200, trust.DisplayRectangle.Width), 0)

            Dim fit As SectionCard = ChecksCard(JournalChoiceGuide.FitHeading, JournalChoiceGuide.FitChecks, answered)
            Dim fitSource As New Label With {
                .Text = JournalChoiceGuide.FitSource,
                .AutoSize = True,
                .UseMnemonic = False,
                .Dock = DockStyle.Bottom,
                .Padding = New Padding(0, 6, 0, 0)
            }
            fit.Controls.Add(fitSource)
            AddHandler fit.Resize, Sub(sender, e) fitSource.MaximumSize = New Size(Math.Max(200, fit.DisplayRectangle.Width), 0)
            trust.Margin = New Padding(3, 3, 8, 3)
            fit.Margin = New Padding(8, 3, 3, 3)
            root.Controls.Add(trust, 0, 3)
            root.Controls.Add(fit, 1, 3)

            ' Help on the left, the dialog buttons on the right.
            Dim guide As New LinkLabel With {
                .Text = "How to choose a journal",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(3, 12, 3, 3)
            }
            AddHandler guide.LinkClicked, Sub(sender, e) ShowGuide()
            root.Controls.Add(guide, 0, 4)

            Dim buttons As New FlowLayoutPanel With {.FlowDirection = FlowDirection.RightToLeft, .AutoSize = True, .Anchor = AnchorStyles.Right, .WrapContents = False, .Margin = New Padding(0, 8, 0, 0)}
            btnOk.Text = If(isNew, "Add", "OK")
            btnOk.AutoSize = True
            btnOk.MinimumSize = New Size(96, 34)
            btnOk.DialogResult = DialogResult.OK
            AddHandler btnOk.Click, Sub(sender, e) Accept()
            Dim btnCancel As New Button With {.Text = "Cancel", .AutoSize = True, .MinimumSize = New Size(96, 34), .DialogResult = DialogResult.Cancel}
            buttons.Controls.Add(btnOk)
            buttons.Controls.Add(btnCancel)
            root.Controls.Add(buttons, 1, 4)

            Me.AcceptButton = btnOk
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)
            RefreshJournal()
            ' Open with nothing selected in the journal box.
            AddHandler Me.Shown, Sub(sender, e) cmbJournal.Select(cmbJournal.Text.Length, 0)

        End Sub


        Private Function ChecksCard(heading As String, checks As IReadOnlyList(Of JournalCheck), answered As HashSet(Of String)) As SectionCard

            Dim card As New SectionCard With {.Text = heading, .Dock = DockStyle.Fill, .Padding = New Padding(14, 8, 14, 10)}
            Dim list As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True}
            For Each check As JournalCheck In checks
                Dim box As New CheckBox With {
                    .Text = check.Text,
                    .Tag = check.Id,
                    .AutoSize = False,
                    .CheckAlign = ContentAlignment.TopLeft,
                    .TextAlign = ContentAlignment.TopLeft,
                    .UseMnemonic = False,
                    .Checked = answered.Contains(check.Id),
                    .Margin = New Padding(0, 2, 0, 4)
                }
                _checkBoxes.Add(box)
                list.Controls.Add(box)

                Dim hint As New LinkLabel With {
                    .AutoSize = True,
                    .UseMnemonic = False,
                    .Visible = False,
                    .ForeColor = UiTheme.MutedText(),
                    .LinkColor = UiTheme.AccentColor(),
                    .ActiveLinkColor = UiTheme.AccentSecondaryColor(),
                    .VisitedLinkColor = UiTheme.AccentColor(),
                    .Margin = New Padding(CInt(Math.Ceiling(16 * DeviceDpi / 96.0)) + 8, 0, 0, 6)
                }
                AddHandler hint.LinkClicked, AddressOf OpenHint
                _hints(check.Id) = hint
                list.Controls.Add(hint)
            Next
            AddHandler list.Resize,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(160, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth)
                    Dim glyph As Integer = CInt(Math.Ceiling(16 * list.DeviceDpi / 96.0)) + 8
                    For Each box As CheckBox In list.Controls.OfType(Of CheckBox)()
                        box.Width = width
                        box.Height = TextRenderer.MeasureText(box.Text, box.Font, New Size(width - glyph, 0), TextFormatFlags.WordBreak).Height + 6
                    Next
                    For Each hint As LinkLabel In list.Controls.OfType(Of LinkLabel)()
                        hint.MaximumSize = New Size(Math.Max(120, width - glyph), 0)
                    Next
                End Sub
            card.Controls.Add(list)
            Return card

        End Function


        Private Function FieldLabel(text As String) As Label
            Return New Label With {.Text = text, .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 3, 10, 3)}
        End Function


        Private Sub RefreshJournal()
            Dim name As String = cmbJournal.Text.Trim()
            btnOk.Enabled = name.Length > 0
            lblHistory.Text = If(name.Length = 0 OrElse _history Is Nothing, "Choose or type a journal.", "Your history with this journal: " & _history(name))
            RefreshHints(If(name.Length = 0 OrElse _factsFor Is Nothing, Nothing, _factsFor(name)))
        End Sub


        ' A fact or link from the Journals page under the question it helps
        ' answer; a link opens in the browser.
        Private Sub RefreshHints(record As JournalRecord)
            For Each pair As KeyValuePair(Of String, LinkLabel) In _hints
                Dim hint = JournalFactsService.HintFor(pair.Key, record)
                pair.Value.Text = hint.Text
                pair.Value.Tag = hint.Url
                pair.Value.LinkArea = If(hint.Url.Length > 0, New LinkArea(0, hint.Text.Length), New LinkArea(0, 0))
                pair.Value.Visible = hint.Text.Length > 0
            Next
        End Sub


        Private Sub OpenHint(sender As Object, e As LinkLabelLinkClickedEventArgs)
            Dim url As String = TryCast(DirectCast(sender, LinkLabel).Tag, String)
            If String.IsNullOrEmpty(url) Then Return
            Try
                UrlSafetyService.OpenInBrowser(url)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open Link", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        ' For tests: the hint shown under each question.
        Friend ReadOnly Property Hints As IReadOnlyDictionary(Of String, LinkLabel)
            Get
                Return _hints
            End Get
        End Property


        Private Sub Accept()
            JournalName = cmbJournal.Text.Trim()
            Status = CType(Math.Max(0, cmbStatus.SelectedIndex), CandidateStatus)
            Notes = txtNotes.Text.Trim()
            Checks = _checkBoxes.Where(Function(box) box.Checked).Select(Function(box) CStr(box.Tag)).ToList()
        End Sub


        Private Sub ShowGuide()
            If GuidePrompt IsNot Nothing Then
                GuidePrompt()
                Return
            End If
            Using help As New HelpForm("Choosing a Journal")
                help.ShowDialog(Me)
            End Using
        End Sub


        ' For tests: the journal box and the checks.
        Friend ReadOnly Property JournalBox As ComboBox
            Get
                Return cmbJournal
            End Get
        End Property

        Friend ReadOnly Property CheckBoxes As List(Of CheckBox)
            Get
                Return _checkBoxes
            End Get
        End Property

        Friend Sub AcceptForTest()
            Accept()
        End Sub

    End Class

End Namespace
