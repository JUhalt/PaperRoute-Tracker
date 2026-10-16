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
    ' cannot be prepared; the main window when a library cannot be read or
    ' is held open by another program.
    Public NotInheritable Class StartupFailure

        Public ReadOnly Property Heading As String
        Public ReadOnly Property Explanation As String
        Public ReadOnly Property FilePath As String
        Public ReadOnly Property Cause As Exception

        ' What the window may offer to put right (#124). The previous save
        ' is manuscripts.bak, so it is offered only where the manuscript
        ' library or the storage it sits in failed; a backup archive also
        ' puts back the author library. A file another program holds is not
        ' damage, so neither is offered for it.
        Public ReadOnly Property OffersPreviousSave As Boolean
        Public ReadOnly Property OffersRestore As Boolean


        Private Sub New(
            heading As String,
            explanation As String,
            filePath As String,
            cause As Exception,
            offersPreviousSave As Boolean,
            offersRestore As Boolean
        )

            If cause Is Nothing Then
                Throw New ArgumentNullException(NameOf(cause))
            End If

            Me.Heading = heading
            Me.Explanation = explanation
            Me.FilePath = If(filePath, String.Empty)
            Me.Cause = cause
            Me.OffersPreviousSave = offersPreviousSave
            Me.OffersRestore = offersRestore

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
                cause,
                offersPreviousSave:=True,
                offersRestore:=True
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
                cause,
                offersPreviousSave:=True,
                offersRestore:=True
            )

        End Function


        ' A library file another program holds open (#111): not damage, so
        ' the loader stopped before reading it or turning to its backup.
        Public Shared Function ForLibraryInUse(
            cause As Exception,
            dataFilePath As String
        ) As StartupFailure

            Return New StartupFailure(
                "PaperRoute could not open a library file that another program is using.",
                "This is not a sign of damage. PaperRoute stopped before reading the file or turning to its backup, so nothing was read, recovered or changed. Close the program that has the file open, then start PaperRoute again.",
                dataFilePath,
                cause,
                offersPreviousSave:=False,
                offersRestore:=False
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
                cause,
                offersPreviousSave:=False,
                offersRestore:=True
            )

        End Function

    End Class


    ' Shown in place of the startup message boxes (#119), the in-use one
    ' (#111) among them: what failed, which file and folder, Open Data
    ' Folder, Copy Details, a link to the guide's Recovering your library
    ' section, and Close. Where the library itself failed, it also offers
    ' Use the Previous Save, when manuscripts.bak reads with the current
    ' rules, and Restore From Backup... (#124). Either one that succeeds
    ' says what it did and closes the window recovered, and the caller
    ' opens the recovered library; one that fails says why and changes
    ' nothing here.
    Public Class StartupFailureForm
        Inherits Form

        Private ReadOnly _failure As StartupFailure
        Private ReadOnly _recovery As StartupRecoveryService
        Private ReadOnly txtDetails As New TextBox()
        Private _recovered As Boolean = False

        ' Choose the backup archive and show a message; tests answer instead.
        Friend backupFilePrompt As Func(Of String) = Nothing
        Friend messagePrompt As Action(Of String, String, MessageBoxIcon) = Nothing


        Public Sub New(
            failure As StartupFailure
        )

            If failure Is Nothing Then
                Throw New ArgumentNullException(NameOf(failure))
            End If

            _failure = failure

            If failure.OffersPreviousSave OrElse failure.OffersRestore Then
                _recovery = New StartupRecoveryService()
            End If

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        ' True once the previous save or a backup was put in place.
        Friend ReadOnly Property Recovered As Boolean
            Get
                Return _recovered
            End Get
        End Property


        ' From the main window, which closes once this returns False, and
        ' starts PaperRoute again on the recovered library when it returns
        ' True (#124).
        Public Shared Function ShowFailure(
            failure As StartupFailure,
            owner As IWin32Window
        ) As Boolean

            Using window As New StartupFailureForm(failure)

                If owner Is Nothing Then

                    window.StartPosition = FormStartPosition.CenterScreen
                    window.ShowDialog()

                Else

                    window.ShowDialog(owner)

                End If

                Return window.Recovered

            End Using

        End Function


        ' From Program, before the application framework has run: the window
        ' needs the defaults the framework would have applied, and the
        ' appearance the user chose. True when the window recovered the
        ' library, so startup can go on (#124).
        Public Shared Function ShowBeforeStartup(
            failure As StartupFailure
        ) As Boolean

            Application.EnableVisualStyles()
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.SetColorMode(UiTheme.ColorModeFor(LoadSettingsSafely().Appearance))

            Return ShowFailure(failure, Nothing)

        End Function


        ' Settings may live beside the storage that failed.
        Private Shared Function LoadSettingsSafely() As AppSettings

            Try
                Return New AppSettingsService().Load()
            Catch
                Return New AppSettings()
            End Try

        End Function


        Private Sub BuildInterface()

            Dim offersPreviousSave As Boolean =
                PreviousSaveCanBeOffered()

            Dim offersRecovery As Boolean =
                offersPreviousSave OrElse _failure.OffersRestore

            Me.Text = "PaperRoute could not start"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.SizeGripStyle = SizeGripStyle.Show
            Me.ClientSize = New Size(If(offersRecovery, 820, 680), 440)
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

            ' The ways to put the library right (#124), at the left.
            If _failure.OffersRestore Then

                Dim btnRestore As New Button With {
                    .Text = "Restore From Backup...",
                    .Width = 180,
                    .Height = 36
                }

                AddHandler btnRestore.Click,
                    AddressOf RestoreFromBackup

                buttons.Controls.Add(btnRestore)

            End If

            If offersPreviousSave Then

                Dim btnPreviousSave As New Button With {
                    .Text = "Use the Previous Save",
                    .Width = 180,
                    .Height = 36
                }

                AddHandler btnPreviousSave.Click,
                    AddressOf UsePreviousSave

                buttons.Controls.Add(btnPreviousSave)

            End If

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


        ' Offered only when manuscripts.bak reads with the current rules and
        ' the marker is not from a newer PaperRoute. A check that cannot be
        ' made offers nothing: this window is what PaperRoute shows when it
        ' cannot start, so it must open.
        Private Function PreviousSaveCanBeOffered() As Boolean

            If Not _failure.OffersPreviousSave Then
                Return False
            End If

            Try
                Return _recovery.PreviousSaveIsValid()
            Catch ex As Exception
                Return False
            End Try

        End Function


        Private Sub UsePreviousSave(
            sender As Object,
            e As EventArgs
        )

            Dim replacedFile As String

            Try
                replacedFile = _recovery.UsePreviousSave()
            Catch ex As Exception
                ShowMessage(ex.Message, "Use the Previous Save", MessageBoxIcon.Error)
                Return
            End Try

            FinishRecovery(
                "PaperRoute put the previous save in place of your library file" &
                If(String.IsNullOrEmpty(replacedFile),
                   ".",
                   " and kept the file it replaced at " & replacedFile & ".")
            )

        End Sub


        Private Sub RestoreFromBackup(
            sender As Object,
            e As EventArgs
        )

            Dim backupPath As String =
                If(backupFilePrompt IsNot Nothing, backupFilePrompt(), ChooseBackupFile())

            If String.IsNullOrEmpty(backupPath) Then
                Return
            End If

            Dim result As RestoreResult

            Try
                result = _recovery.RestoreFromBackup(backupPath)
            Catch ex As Exception
                ' A failure during the swap says what was put back and names
                ' the emergency ZIP (#116); before it, nothing was changed.
                ShowMessage(ex.Message, "Restore From Backup", MessageBoxIcon.Error)
                Return
            End Try

            FinishRecovery(
                "PaperRoute restored " &
                result.ManuscriptCount.ToString() &
                " manuscript(s) from the backup" &
                If(String.IsNullOrWhiteSpace(result.EmergencyBackupPath),
                   ".",
                   " and kept your library files from before the restore at " & result.EmergencyBackupPath & ".")
            )

        End Sub


        Private Function ChooseBackupFile() As String

            Using dialog As New OpenFileDialog With {
                .Title = "Restore From Backup",
                .Filter = "PaperRoute backup (*.zip)|*.zip|ZIP archives (*.zip)|*.zip",
                .CheckFileExists = True,
                .Multiselect = False
            }

                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return Nothing
                End If

                Return dialog.FileName

            End Using

        End Function


        ' Says in one sentence what was put in place, and closes the window
        ' recovered: the caller opens the recovered library.
        Private Sub FinishRecovery(
            sentence As String
        )

            _recovered = True

            ShowMessage(sentence, "Library Recovered", MessageBoxIcon.Information)

            Close()

        End Sub


        Private Sub ShowMessage(
            text As String,
            caption As String,
            icon As MessageBoxIcon
        )

            If messagePrompt IsNot Nothing Then
                messagePrompt(text, caption, icon)
                Return
            End If

            MessageBox.Show(
                Me,
                text,
                caption,
                MessageBoxButtons.OK,
                icon
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
