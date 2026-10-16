Imports System
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' What stopped PaperRoute from starting (#119): one line saying what
    ' failed, the explanation the message box used to give, the file being
    ' read when it failed, and the error. Program builds one when storage
    ' cannot be prepared; the main window when a library cannot be read.
    Public NotInheritable Class StartupFailure

        Public ReadOnly Property Heading As String
        Public ReadOnly Property Explanation As String
        Public ReadOnly Property FilePath As String
        Public ReadOnly Property Cause As Exception


        Private Sub New(
            heading As String,
            explanation As String,
            filePath As String,
            cause As Exception
        )

            If cause Is Nothing Then
                Throw New ArgumentNullException(NameOf(cause))
            End If

            Me.Heading = heading
            Me.Explanation = explanation
            Me.FilePath = If(filePath, String.Empty)
            Me.Cause = cause

        End Sub


        Public ReadOnly Property FolderPath As String
            Get
                Return If(Path.GetDirectoryName(FilePath), String.Empty)
            End Get
        End Property


        Public Shared Function ForStorage(
            cause As Exception,
            schemaFilePath As String
        ) As StartupFailure

            Return New StartupFailure(
                "PaperRoute could not prepare its local storage.",
                "The storage format could not be verified, so PaperRoute closed rather than open or rewrite data in an unknown format. Existing files were left in place wherever possible.",
                schemaFilePath,
                cause
            )

        End Function


        Public Shared Function ForManuscriptLibrary(
            cause As Exception,
            dataFilePath As String
        ) As StartupFailure

            Return New StartupFailure(
                "PaperRoute could not read your manuscript library.",
                "The library file could not be read, and no safety backup could be recovered. PaperRoute closed rather than continue with an empty library. Your files were not overwritten.",
                dataFilePath,
                cause
            )

        End Function


        Public Shared Function ForAuthorLibrary(
            cause As Exception,
            dataFilePath As String
        ) As StartupFailure

            Return New StartupFailure(
                "PaperRoute could not read the reusable author library.",
                "PaperRoute closed rather than continue with uncertain author metadata. Your files were not changed.",
                dataFilePath,
                cause
            )

        End Function

    End Class


    ' Shown in place of the three startup message boxes (#119): what failed,
    ' which file and folder, Open Data Folder, Copy Details, a link to the
    ' guide's Recovering your library section, and Close. Nothing is
    ' restored or recovered from here; that is its own issue.
    Public Class StartupFailureForm
        Inherits Form

        Private ReadOnly _failure As StartupFailure
        Private ReadOnly txtDetails As New TextBox()


        Public Sub New(
            failure As StartupFailure
        )

            If failure Is Nothing Then
                Throw New ArgumentNullException(NameOf(failure))
            End If

            _failure = failure

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        ' From the main window, which closes once this returns.
        Public Shared Sub ShowFailure(
            failure As StartupFailure,
            owner As IWin32Window
        )

            Using window As New StartupFailureForm(failure)

                If owner Is Nothing Then

                    window.StartPosition = FormStartPosition.CenterScreen
                    window.ShowDialog()

                Else

                    window.ShowDialog(owner)

                End If

            End Using

        End Sub


        ' From Program, before the application framework has run: the window
        ' needs the defaults the framework would have applied, and the
        ' appearance the user chose.
        Public Shared Sub ShowBeforeStartup(
            failure As StartupFailure
        )

            Application.EnableVisualStyles()
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.SetColorMode(UiTheme.ColorModeFor(LoadSettingsSafely().Appearance))

            ShowFailure(failure, Nothing)

        End Sub


        ' Settings may live beside the storage that failed.
        Private Shared Function LoadSettingsSafely() As AppSettings

            Try
                Return New AppSettingsService().Load()
            Catch
                Return New AppSettings()
            End Try

        End Function


        Private Sub BuildInterface()

            Me.Text = "PaperRoute could not start"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.SizeGripStyle = SizeGripStyle.Show
            Me.ClientSize = New Size(680, 440)
            Me.MinimumSize = New Size(560, 380)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 6,
                .Padding = New Padding(20)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblTitle As New Label With {
                .Text = Me.Text,
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 16.0F, FontStyle.Bold),
                .ForeColor = UiTheme.AccentColor(),
                .Anchor = AnchorStyles.Left,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblHeading As New Label With {
                .Text = _failure.Heading,
                .AutoSize = True,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 6)
            }

            Dim lblExplanation As New Label With {
                .Text = _failure.Explanation,
                .AutoSize = True,
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 0, 0, 10)
            }

            AddHandler root.SizeChanged,
                Sub(sender As Object, e As EventArgs)
                    Dim width As Integer = Math.Max(200, root.ClientSize.Width - root.Padding.Horizontal - 8)
                    lblHeading.MaximumSize = New Size(width, 0)
                    lblExplanation.MaximumSize = New Size(width, 0)
                End Sub

            txtDetails.Dock = DockStyle.Fill
            txtDetails.Multiline = True
            txtDetails.ReadOnly = True
            txtDetails.ScrollBars = ScrollBars.Both
            txtDetails.WordWrap = False
            txtDetails.Font = New Font("Consolas", 9.5F)
            txtDetails.Text = BuildDetails()
            txtDetails.Margin = New Padding(0)

            Dim lnkGuide As New LinkLabel With {
                .Text = "Recovering your library (User Guide)",
                .AutoSize = True,
                .Margin = New Padding(0, 10, 0, 0)
            }

            AddHandler lnkGuide.LinkClicked,
                AddressOf OpenGuide

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .Width = 95,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            Dim btnCopy As New Button With {
                .Text = "Copy Details",
                .Width = 120,
                .Height = 36
            }

            Dim btnOpenData As New Button With {
                .Text = "Open Data Folder",
                .Width = 155,
                .Height = 36
            }

            AddHandler btnCopy.Click,
                AddressOf CopyDetails

            AddHandler btnOpenData.Click,
                AddressOf OpenDataFolder

            buttons.Controls.Add(btnClose)
            buttons.Controls.Add(btnCopy)
            buttons.Controls.Add(btnOpenData)

            root.Controls.Add(lblTitle, 0, 0)
            root.Controls.Add(lblHeading, 0, 1)
            root.Controls.Add(lblExplanation, 0, 2)
            root.Controls.Add(txtDetails, 0, 3)
            root.Controls.Add(lnkGuide, 0, 4)
            root.Controls.Add(buttons, 0, 5)

            Me.AcceptButton = btnClose
            Me.CancelButton = btnClose
            Me.Controls.Add(root)

        End Sub


        ' For tests.
        Friend ReadOnly Property DetailsText As String
            Get
                Return txtDetails.Text
            End Get
        End Property


        ' What the window shows: the file and folder, then the error and
        ' what it was caused by.
        Private Function BuildDetails() As String

            Dim builder As New StringBuilder()

            builder.AppendLine("File:   " & _failure.FilePath)
            builder.AppendLine("Folder: " & _failure.FolderPath)
            builder.AppendLine()
            builder.AppendLine("Error: " & _failure.Cause.Message)

            Dim inner As Exception = _failure.Cause.InnerException

            While inner IsNot Nothing
                builder.AppendLine("Because: " & inner.Message)
                inner = inner.InnerException
            End While

            Return builder.ToString().TrimEnd()

        End Function


        ' What Copy Details puts on the clipboard: the details above, the
        ' full error, and the Diagnostics report, which needs no loaded
        ' library.
        Friend Function CopyText() As String

            Dim builder As New StringBuilder()

            builder.AppendLine(Me.Text)
            builder.AppendLine(New String("="c, Me.Text.Length))
            builder.AppendLine()
            builder.AppendLine(_failure.Heading)
            builder.AppendLine(_failure.Explanation)
            builder.AppendLine()
            builder.AppendLine(BuildDetails())
            builder.AppendLine()
            builder.AppendLine("Error details:")
            builder.AppendLine(_failure.Cause.ToString())
            builder.AppendLine()
            builder.Append(DiagnosticsForm.BuildReport(LoadSettingsSafely()))

            Return builder.ToString()

        End Function


        Private Sub CopyDetails(
            sender As Object,
            e As EventArgs
        )

            Try

                Clipboard.SetText(CopyText())

                MessageBox.Show(
                    Me,
                    "The details were copied to the clipboard.",
                    Me.Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not copy the details." &
                    Environment.NewLine & Environment.NewLine &
                    ex.Message,
                    Me.Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub


        Private Sub OpenDataFolder(
            sender As Object,
            e As EventArgs
        )

            DiagnosticsForm.OpenFolder(
                Me,
                _failure.FolderPath,
                Me.Text
            )

        End Sub


        Private Sub OpenGuide(
            sender As Object,
            e As LinkLabelLinkClickedEventArgs
        )

            Try

                UrlSafetyService.OpenInBrowser(
                    UserGuideService.RecoveringYourLibraryUrl
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Open User Guide",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub

    End Class

End Namespace
