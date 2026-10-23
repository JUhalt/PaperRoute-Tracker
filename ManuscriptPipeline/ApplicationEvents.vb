Imports Microsoft.VisualBasic.ApplicationServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace My

    Partial Friend Class MyApplication

        Private Sub MyApplication_ApplyApplicationDefaults(
            sender As Object,
            e As ApplyApplicationDefaultsEventArgs
        ) Handles Me.ApplyApplicationDefaults

            Dim settingsService As New AppSettingsService()

            Dim settings As AppSettings =
                settingsService.Load()

            e.ColorMode =
                UiTheme.ColorModeFor(
                    settings.Appearance
                )

            ' The framework's default, which the installed build has run
            ' with, and what the startup window (StartupFailureForm) and the
            ' certification harness set: pinned so the dialogs' one scaling
            ' rule (#93) is measured against the mode PaperRoute runs in.
            e.HighDpiMode =
                HighDpiMode.SystemAware

        End Sub


        ' An error on the UI thread is recorded on this computer first (#120);
        ' nothing is sent, and a report that cannot be written costs nothing.
        ' The open manuscript page's unsaved edits go to a recovery draft
        ' next (#123), before anything is shown, so the next start offers
        ' them however this ends. Then the main window shows one plain dialog
        ' and decides whether PaperRoute closes (#121). With no window left
        ' to keep working in, PaperRoute says why and exits as the stock
        ' dialog's Quit did.
        Private Sub MyApplication_UnhandledException(
            sender As Object,
            e As UnhandledExceptionEventArgs
        ) Handles Me.UnhandledException

            Dim reports As New ErrorReportService()

            reports.Write(
                e.Exception,
                "main window"
            )

            Dim window As Form1 =
                Application.OpenForms.OfType(Of Form1)().FirstOrDefault()

            ' A draft that cannot be written must never stand between the
            ' error and its dialog.
            Try
                window?.WriteRecoveryDraft("an error ended the session")
            Catch ex As Exception
            End Try

            If window Is Nothing OrElse window.IsDisposed Then

                ' Informational only: ExitApplication stays True, so the
                ' framework exits once this returns. Keep Working would be
                ' a false promise here.
                MessageBox.Show(
                    "PaperRoute ran into a problem and has to close." &
                    Environment.NewLine &
                    Environment.NewLine &
                    e.Exception.Message,
                    "PaperRoute",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

                Return

            End If

            e.ExitApplication = False
            window.ReportUnhandledError(e.Exception)

        End Sub

    End Class

End Namespace