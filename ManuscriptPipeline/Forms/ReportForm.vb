Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Previews a report exactly as it will be saved (#30, #63). For the
    ' pipeline report, the user chooses which manuscripts it covers.
    Friend Class ReportForm
        Inherits Form

        Private ReadOnly _build As Func(Of List(Of Manuscript), Boolean, String)
        Private ReadOnly _choices As List(Of Manuscript)
        Private ReadOnly _fileName As String

        Private ReadOnly lstManuscripts As New CheckedListBox()
        Private ReadOnly chkDeadlines As New CheckBox()
        Private ReadOnly chkOpen As New CheckBox()
        Private ReadOnly preview As New WebBrowser()
        Private ReadOnly btnSave As New ActionButton()
        Private ReadOnly previewTimer As New Timer With {.Interval = 250}

        ' Tests choose the file instead of a Save dialog.
        Friend SavePathPrompt As Func(Of String) = Nothing

        <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
        Friend Property SavedPath As String = String.Empty

        ' A report of the chosen manuscripts, with a list to choose them.
        Public Sub New(title As String, fileName As String, choices As List(Of Manuscript), build As Func(Of List(Of Manuscript), Boolean, String))
            _build = build
            _choices = choices
            _fileName = fileName
            BuildInterface(title)
        End Sub

        Private Sub BuildInterface(title As String)

            Text = title
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            AutoScaleMode = AutoScaleMode.Dpi
            Font = New Font("Segoe UI", 9.0F)
            ClientSize = New Size(1120, 700)
            MinimumSize = New Size(720, 480)

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = If(_choices Is Nothing, 1, 2), .RowCount = 2, .Padding = New Padding(16, 14, 16, 12)}
            If _choices IsNot Nothing Then root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 280))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            If _choices IsNot Nothing Then
                Dim side As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Margin = New Padding(0, 0, 12, 0)}
                side.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                side.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
                side.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                side.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                side.Controls.Add(New Label With {.Text = "Include", .AutoSize = True, .Font = New Font(Font, FontStyle.Bold), .Margin = New Padding(0, 0, 0, 4)}, 0, 0)

                lstManuscripts.Dock = DockStyle.Fill
                lstManuscripts.CheckOnClick = True
                lstManuscripts.IntegralHeight = False
                lstManuscripts.HorizontalScrollbar = True
                lstManuscripts.AccessibleName = "Manuscripts in the report"
                For Each manuscript As Manuscript In _choices
                    Dim index As Integer = lstManuscripts.Items.Add(ReminderService.SafeManuscriptTitle(manuscript))
                    lstManuscripts.SetItemChecked(index, manuscript.Location = ManuscriptLocation.Pipeline)
                Next
                AddHandler lstManuscripts.ItemCheck, Sub(sender, e) SchedulePreview()
                side.Controls.Add(lstManuscripts, 0, 1)

                chkDeadlines.Text = "Include the next deadline"
                chkDeadlines.AutoSize = True
                chkDeadlines.Checked = True
                chkDeadlines.Margin = New Padding(0, 8, 0, 0)
                AddHandler chkDeadlines.CheckedChanged, Sub(sender, e) SchedulePreview()
                side.Controls.Add(chkDeadlines, 0, 2)

                side.Controls.Add(New Label With {
                    .Text = "The report never includes notes, correspondence, reviewer comments, files, or contact details. Reminders appear only as ""Reminder"".",
                    .AutoSize = True,
                    .MaximumSize = New Size(260, 0),
                    .ForeColor = SystemColors.GrayText,
                    .UseMnemonic = False,
                    .Margin = New Padding(0, 10, 0, 0)
                }, 0, 3)
                root.Controls.Add(side, 0, 0)
            End If

            preview.Dock = DockStyle.Fill
            preview.ScriptErrorsSuppressed = True
            preview.IsWebBrowserContextMenuEnabled = False
            preview.WebBrowserShortcutsEnabled = False
            preview.AllowWebBrowserDrop = False
            preview.AccessibleName = "Report preview"
            ' The preview shows the report; it never leaves it.
            AddHandler preview.Navigating,
                Sub(sender, e)
                    If e.Url IsNot Nothing AndAlso e.Url.ToString() <> "about:blank" Then e.Cancel = True
                End Sub
            Dim frame As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(1), .Margin = New Padding(0), .BackColor = UiTheme.CardBorder()}
            frame.Controls.Add(preview)
            root.Controls.Add(frame, If(_choices Is Nothing, 0, 1), 0)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            btnSave.Text = "Save Report..."
            btnSave.Role = ActionButtonRole.Primary
            btnSave.Height = 34
            btnSave.Width = 130
            AddHandler btnSave.Click, AddressOf SaveReport
            Dim btnCancel As New ActionButton With {.Text = "Cancel", .Height = 34, .Width = 90, .DialogResult = DialogResult.Cancel, .Margin = New Padding(0, 0, 8, 0)}
            chkOpen.Text = "Open in my browser after saving, to print or save as PDF"
            chkOpen.AutoSize = True
            chkOpen.Checked = True
            chkOpen.Margin = New Padding(0, 8, 16, 0)
            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)
            buttons.Controls.Add(chkOpen)
            root.Controls.Add(buttons, 0, 1)
            root.SetColumnSpan(buttons, root.ColumnCount)

            Controls.Add(root)
            AcceptButton = btnSave
            CancelButton = btnCancel

            AddHandler previewTimer.Tick,
                Sub(sender, e)
                    previewTimer.Stop()
                    RefreshPreview()
                End Sub

            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            RefreshPreview()
        End Sub


        Private Sub SchedulePreview()
            previewTimer.Stop()
            previewTimer.Start()
        End Sub


        Friend ReadOnly Property Chosen As List(Of Manuscript)
            Get
                If _choices Is Nothing Then Return Nothing
                Return lstManuscripts.CheckedIndices.Cast(Of Integer)().Select(Function(index) _choices(index)).ToList()
            End Get
        End Property


        Friend Function CurrentHtml() As String
            Return _build(Chosen, chkDeadlines.Checked)
        End Function


        Private Sub RefreshPreview()
            If IsDisposed Then Return
            btnSave.Enabled = _choices Is Nothing OrElse lstManuscripts.CheckedIndices.Count > 0
            preview.DocumentText = CurrentHtml()
        End Sub


        Friend Sub SaveReport(sender As Object, e As EventArgs)

            Dim path As String
            If SavePathPrompt IsNot Nothing Then
                path = SavePathPrompt()
            Else
                Using picker As New SaveFileDialog With {
                    .Title = Text,
                    .Filter = "Web page (*.html)|*.html",
                    .DefaultExt = "html",
                    .AddExtension = True,
                    .OverwritePrompt = True,
                    .FileName = _fileName
                }
                    If picker.ShowDialog(Me) <> DialogResult.OK Then Return
                    path = picker.FileName
                End Using
            End If
            If String.IsNullOrWhiteSpace(path) Then Return

            Try
                File.WriteAllText(path, CurrentHtml(), New UTF8Encoding(False))
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                MessageBox.Show(Me, "PaperRoute could not save the report." & Environment.NewLine & Environment.NewLine & ex.Message,
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End Try

            SavedPath = path

            If chkOpen.Checked AndAlso SavePathPrompt Is Nothing Then
                Try
                    Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
                Catch ex As Exception
                    ' Saving succeeded; opening is a convenience.
                End Try
            End If

            DialogResult = DialogResult.OK
            Close()

        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then previewTimer.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
