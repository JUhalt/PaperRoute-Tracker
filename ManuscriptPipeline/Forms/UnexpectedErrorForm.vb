Imports System
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' One plain dialog for an error on the UI thread (#121): what happened,
    ' whether there are unsaved changes, and a way back to the window. Keep
    ' Working returns there; Close PaperRoute closes it the normal way, so
    ' the Save or Discard question still appears.
    Public Class UnexpectedErrorForm
        Inherits Form

        Private ReadOnly _failure As Exception
        Private ReadOnly _logFolder As String

        Private ReadOnly lblNote As New Label()


        Public Sub New(
            failure As Exception,
            unsavedChanges As Boolean,
            logFolder As String
        )

            _failure = failure
            _logFolder = logFolder

            BuildInterface(unsavedChanges)
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface(
            unsavedChanges As Boolean
        )

            ' Sizes below are at 96 DPI and scale with the display (#93).
            DialogScaling.Begin(Me)

            Me.Text = "PaperRoute"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            ' Wide enough for its four buttons (572 pixels with their gaps,
            ' inside 24 of padding a side): at 560 Keep Working was cut.
            Me.ClientSize = New Size(640, 250)
            Me.Font = New Font("Segoe UI", 10.0F)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(24)
            }

            ' One column as wide as the dialog, so long messages wrap to it
            ' and nothing widens the table past the edge. A width set from
            ' the table's size as it scales is scaled again (#93).
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblHeading As New Label With {
                .Text = "PaperRoute ran into a problem",
                .AutoSize = True,
                .UseMnemonic = False,
                .Font = New Font(Me.Font.FontFamily, 13, FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblMessage As New Label With {
                .Text = If(String.IsNullOrWhiteSpace(_failure?.Message), "An unexpected error occurred.", _failure.Message),
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 12)
            }

            ' The one thing the user needs to decide on.
            Dim lblUnsaved As New Label With {
                .Text =
                    If(
                        unsavedChanges,
                        "There are unsaved changes. Keep Working returns to the window so you can save them.",
                        "Nothing is waiting to be saved."
                    ),
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .UseMnemonic = False,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .ForeColor = If(unsavedChanges, UiTheme.WarningColor(), UiTheme.SecondaryText()),
                .Margin = New Padding(0)
            }

            lblNote.AutoSize = True
            lblNote.UseMnemonic = False
            lblNote.ForeColor = UiTheme.MutedText()
            lblNote.Margin = New Padding(0, 0, 0, 8)

            Dim buttons As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 2,
                .RowCount = 1,
                .Margin = New Padding(0)
            }

            buttons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            buttons.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            Dim tools As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .WrapContents = False,
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0)
            }

            Dim decisions As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Anchor = AnchorStyles.Right,
                .Margin = New Padding(0)
            }

            Dim btnCopy As Button = MakeButton("Copy Details", 120)
            Dim btnLog As Button = MakeButton("Open Log Folder", 140)

            Dim btnKeep As Button = MakeButton("Keep Working", 130)
            btnKeep.DialogResult = DialogResult.Cancel

            Dim btnClose As Button = MakeButton("Close PaperRoute", 150)
            btnClose.DialogResult = DialogResult.Abort

            AddHandler btnCopy.Click, AddressOf CopyDetails
            AddHandler btnLog.Click, AddressOf OpenLogFolder

            tools.Controls.Add(btnCopy)
            tools.Controls.Add(btnLog)
            decisions.Controls.Add(btnKeep)
            decisions.Controls.Add(btnClose)

            buttons.Controls.Add(tools, 0, 0)
            buttons.Controls.Add(decisions, 1, 0)

            root.Controls.Add(lblHeading, 0, 0)
            root.Controls.Add(lblMessage, 0, 1)
            root.Controls.Add(lblUnsaved, 0, 2)
            root.Controls.Add(lblNote, 0, 3)
            root.Controls.Add(buttons, 0, 4)

            ' Enter and Escape both keep working: the safe way out.
            Me.AcceptButton = btnKeep
            Me.CancelButton = btnKeep

            Me.Controls.Add(root)

            DialogScaling.Finish(Me)

        End Sub


        Private Function MakeButton(
            text As String,
            width As Integer
        ) As Button

            Return New Button With {
                .Text = text,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowOnly,
                .MinimumSize = New Size(width, 36),
                .Padding = New Padding(8, 0, 8, 0),
                .Margin = New Padding(0, 0, 8, 0)
            }

        End Function


        ' What a bug report needs: the version, when, and the error itself.
        Friend Shared Function Details(
            failure As Exception
        ) As String

            Dim builder As New StringBuilder()

            builder.AppendLine("PaperRoute Tracker " & UpdateService.CurrentVersionText())
            builder.AppendLine("Reported " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            builder.AppendLine()
            builder.Append(If(failure Is Nothing, "An unexpected error occurred.", failure.ToString()))

            Return builder.ToString()

        End Function


        ' Where the crash report of #120 is written.
        Friend Shared Function LogFolder() As String

            Return Path.Combine(
                StorageMigrationService.CurrentDataRoot(),
                "logs"
            )

        End Function


        Private Sub CopyDetails(
            sender As Object,
            e As EventArgs
        )

            Try
                Clipboard.SetText(Details(_failure))
                lblNote.Text = "Details copied."
            Catch ex As Exception When TypeOf ex Is System.Runtime.InteropServices.ExternalException OrElse TypeOf ex Is InvalidOperationException
                lblNote.Text = "The clipboard is busy; try Copy Details again."
            End Try

        End Sub


        Private Sub OpenLogFolder(
            sender As Object,
            e As EventArgs
        )

            Try

                DiagnosticsForm.OpenFolder(_logFolder)

            Catch ex As Exception

                lblNote.Text = "PaperRoute could not open " & _logFolder

            End Try

        End Sub

    End Class

End Namespace
