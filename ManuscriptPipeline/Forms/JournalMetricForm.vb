Imports System
Imports System.ComponentModel
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' A journal metric entered by the researcher (#87), exactly as its
    ' publisher reports it, with its year and source. PaperRoute doesn't look
    ' these up, and never combines them into a score.
    Public Class JournalMetricForm
        Inherits Form

        Private ReadOnly cboMetric As New ComboBox()
        Private ReadOnly txtName As New TextBox()
        Private ReadOnly lblName As New Label()
        Private ReadOnly txtValue As New TextBox()
        Private ReadOnly numYear As New NumericUpDown()
        Private ReadOnly txtSource As New TextBox()
        Private ReadOnly txtUrl As New TextBox()
        Private ReadOnly lblDefinition As New Label()
        Private ReadOnly lnkWhere As New LinkLabel()
        Private ReadOnly btnOk As New Button()
        Private ReadOnly root As New TableLayoutPanel()

        Private ReadOnly _existing As JournalFact
        Private _result As JournalFact
        Private _lastDefaultSource As String = String.Empty


        Public Sub New(Optional existing As JournalFact = Nothing)
            _existing = existing
            BuildInterface()
            LoadExisting()
            UiPolish.ApplyDialog(Me)
        End Sub


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Result As JournalFact
            Get
                Return _result
            End Get
        End Property


        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            Me.ClientSize = New Size(Me.ClientSize.Width, root.GetPreferredSize(New Size(Me.ClientSize.Width, 0)).Height)
            Me.ActiveControl = If(_existing Is Nothing, CType(cboMetric, Control), txtValue)
        End Sub


        Private Sub BuildInterface()

            Me.SuspendLayout()

            Me.Text = If(_existing Is Nothing, "Add Metric", "Edit Metric")
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            ' Sizes below are at 96 DPI and scale with the display.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(600, 480)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            root.Dock = DockStyle.Fill
            root.ColumnCount = 2
            root.Padding = New Padding(20, 16, 20, 12)
            root.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            Dim intro As New Label With {
                .Text = "Enter a metric exactly as its publisher reports it, with its year. PaperRoute doesn't look these up.",
                .AutoSize = True,
                .MaximumSize = New Size(556, 0),
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 10)
            }

            cboMetric.DropDownStyle = ComboBoxStyle.DropDownList
            cboMetric.Dock = DockStyle.Fill
            For Each definition As JournalFactDefinition In JournalFactCatalog.EnteredMetrics
                cboMetric.Items.Add(definition.Label)
            Next
            AddHandler cboMetric.SelectedIndexChanged, AddressOf MetricChanged

            txtName.Dock = DockStyle.Fill
            txtName.MaxLength = 120
            lblName.Text = "&Name"
            txtValue.Dock = DockStyle.Fill
            txtValue.MaxLength = 120
            txtValue.PlaceholderText = "e.g. 3.2, or 18%"
            numYear.Minimum = 1900
            numYear.Maximum = DateTime.Today.Year + 1
            numYear.Value = DateTime.Today.Year - 1
            numYear.Width = 90
            numYear.Anchor = AnchorStyles.Left
            txtSource.Dock = DockStyle.Fill
            txtSource.MaxLength = 120
            txtUrl.Dock = DockStyle.Fill
            txtUrl.PlaceholderText = "Optional: the page where you found it"

            lblDefinition.AutoSize = True
            lblDefinition.MaximumSize = New Size(556, 0)
            lblDefinition.UseMnemonic = False
            lblDefinition.ForeColor = UiTheme.MutedText()
            lblDefinition.Margin = New Padding(0, 10, 0, 2)

            lnkWhere.AutoSize = True
            lnkWhere.Text = "Where it's published"
            lnkWhere.Margin = New Padding(0, 2, 0, 6)
            AddHandler lnkWhere.LinkClicked, AddressOf OpenWhere

            Dim note As New Label With {
                .Text = JournalFactCatalog.DoraNote,
                .AutoSize = True,
                .MaximumSize = New Size(556, 0),
                .UseMnemonic = False,
                .ForeColor = UiTheme.MutedText(),
                .Margin = New Padding(0, 6, 0, 0)
            }

            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Margin = New Padding(0, 12, 0, 0)}
            btnOk.Text = "Save"
            btnOk.AutoSize = True
            btnOk.MinimumSize = New Size(96, 34)
            AddHandler btnOk.Click, AddressOf SaveClicked
            Dim btnCancel As New Button With {.Text = "Cancel", .AutoSize = True, .MinimumSize = New Size(96, 34), .DialogResult = DialogResult.Cancel}
            buttons.Controls.Add(btnOk)
            buttons.Controls.Add(btnCancel)

            AddSpanning(intro)
            AddRow("&Metric", cboMetric)
            AddRow(lblName, txtName)
            AddRow("&Value", txtValue)
            AddRow("&Year", numYear)
            AddRow("&Source", txtSource)
            AddRow("Where you &found it", txtUrl)
            AddSpanning(lblDefinition)
            AddSpanning(lnkWhere)
            AddSpanning(note)
            AddSpanning(buttons)

            AddHandler txtValue.TextChanged, Sub(sender, e) RefreshState()
            AddHandler txtSource.TextChanged, Sub(sender, e) RefreshState()
            AddHandler txtName.TextChanged, Sub(sender, e) RefreshState()

            Me.AcceptButton = btnOk
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)

            Me.ResumeLayout(False)
            Me.PerformLayout()

        End Sub


        Private Sub AddRow(label As String, control As Control)
            AddRow(New Label With {.Text = label}, control)
        End Sub


        Private Sub AddRow(label As Label, control As Control)
            label.AutoSize = True
            label.Anchor = AnchorStyles.Left
            label.Margin = New Padding(0, 6, 12, 6)
            control.Margin = New Padding(0, 4, 0, 4)
            Dim row As Integer = root.RowCount
            root.RowCount = row + 1
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.Controls.Add(label, 0, row)
            root.Controls.Add(control, 1, row)
        End Sub


        Private Sub AddSpanning(control As Control)
            Dim row As Integer = root.RowCount
            root.RowCount = row + 1
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.Controls.Add(control, 0, row)
            root.SetColumnSpan(control, 2)
        End Sub


        Private Sub LoadExisting()

            If _existing Is Nothing Then
                cboMetric.SelectedIndex = 0
                RefreshState()
                Return
            End If

            Dim index As Integer = JournalFactCatalog.EnteredMetrics.ToList().FindIndex(Function(item) item.Key = _existing.Key)
            cboMetric.SelectedIndex = If(index >= 0, index, JournalFactCatalog.EnteredMetrics.Count - 1)
            txtName.Text = If(index >= 0, _existing.Label, JournalFactCatalog.LabelOf(_existing))
            txtValue.Text = _existing.Value
            If _existing.Year.HasValue Then numYear.Value = Math.Max(numYear.Minimum, Math.Min(numYear.Maximum, _existing.Year.Value))
            txtSource.Text = _existing.Source
            txtUrl.Text = _existing.Url
            RefreshState()

        End Sub


        Private ReadOnly Property Selected As JournalFactDefinition
            Get
                Return If(cboMetric.SelectedIndex >= 0, JournalFactCatalog.EnteredMetrics(cboMetric.SelectedIndex), Nothing)
            End Get
        End Property


        Private Sub MetricChanged(sender As Object, e As EventArgs)

            Dim definition As JournalFactDefinition = Selected
            If definition Is Nothing Then Return

            Dim isOther As Boolean = definition.Key = JournalFactCatalog.Other
            lblName.Visible = isOther
            txtName.Visible = isOther
            lblDefinition.Text = definition.Definition
            lnkWhere.Visible = definition.WhereUrl.Length > 0

            ' The usual source, unless the researcher typed their own.
            If txtSource.Text.Length = 0 OrElse txtSource.Text = _lastDefaultSource Then txtSource.Text = definition.DefaultSource
            _lastDefaultSource = definition.DefaultSource

            RefreshState()

            If IsHandleCreated Then
                Me.ClientSize = New Size(Me.ClientSize.Width, root.GetPreferredSize(New Size(Me.ClientSize.Width, 0)).Height)
            End If

        End Sub


        Private Sub RefreshState()
            Dim definition As JournalFactDefinition = Selected
            btnOk.Enabled = definition IsNot Nothing AndAlso
                txtValue.Text.Trim().Length > 0 AndAlso
                txtSource.Text.Trim().Length > 0 AndAlso
                (definition.Key <> JournalFactCatalog.Other OrElse txtName.Text.Trim().Length > 0)
        End Sub


        Private Sub OpenWhere(sender As Object, e As LinkLabelLinkClickedEventArgs)
            Try
                UrlSafetyService.OpenInBrowser(Selected.WhereUrl)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open Link", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        Private Sub SaveClicked(sender As Object, e As EventArgs)

            Dim definition As JournalFactDefinition = Selected
            If definition Is Nothing OrElse Not btnOk.Enabled Then Return

            Dim url As String
            Try
                url = UrlSafetyService.NormalizeOptionalHttpUrl(txtUrl.Text, "Where you found it")
            Catch ex As ArgumentException
                MessageBox.Show(Me, ex.Message, "Check the Link", MessageBoxButtons.OK, MessageBoxIcon.Information)
                txtUrl.Focus()
                Return
            End Try

            _result = New JournalFact With {
                .Key = definition.Key,
                .Label = If(definition.Key = JournalFactCatalog.Other, txtName.Text.Trim(), String.Empty),
                .Value = txtValue.Text.Trim(),
                .Year = CInt(numYear.Value),
                .Source = txtSource.Text.Trim(),
                .Url = url,
                .CheckedUtc = DateTime.UtcNow,
                .EnteredByYou = True
            }

            Me.DialogResult = DialogResult.OK

        End Sub


        ' For tests.
        Friend ReadOnly Property MetricBox As ComboBox
            Get
                Return cboMetric
            End Get
        End Property

        Friend ReadOnly Property ValueBox As TextBox
            Get
                Return txtValue
            End Get
        End Property

        Friend ReadOnly Property SourceBox As TextBox
            Get
                Return txtSource
            End Get
        End Property

        Friend ReadOnly Property NameBox As TextBox
            Get
                Return txtName
            End Get
        End Property

        Friend ReadOnly Property YearBox As NumericUpDown
            Get
                Return numYear
            End Get
        End Property

        Friend Sub SaveForTest()
            SaveClicked(Me, EventArgs.Empty)
        End Sub

    End Class

End Namespace
