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

        End Sub


        ' An error on the UI thread is recorded on this computer first (#120);
        ' nothing is sent, and a report that cannot be written costs nothing.
        ' Then the main window shows one plain dialog and decides whether
        ' PaperRoute closes (#121). With no window left to keep working in,
        ' PaperRoute says why and exits as the stock dialog's Quit did.
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