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


        ' An unexpected error on the UI thread is recorded on this computer
        ' first (#120); nothing is sent. The stock dialog stays until
        ' PaperRoute's own lands: Quit closes PaperRoute, Continue goes on.
        Private Sub MyApplication_UnhandledException(
            sender As Object,
            e As Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs
        ) Handles Me.UnhandledException

            Dim reports As New ErrorReportService()

            reports.Write(
                e.Exception,
                "main window"
            )

            Using dialog As New ThreadExceptionDialog(e.Exception)

                e.ExitApplication =
                    dialog.ShowDialog() = DialogResult.Abort

            End Using

        End Sub

    End Class

End Namespace