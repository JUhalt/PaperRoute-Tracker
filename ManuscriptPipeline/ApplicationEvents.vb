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

            Select Case settings.Appearance

                Case AppAppearance.Light

                    e.ColorMode =
                        SystemColorMode.Classic

                Case AppAppearance.Dark

                    e.ColorMode =
                        SystemColorMode.Dark

                Case Else

                    e.ColorMode =
                        SystemColorMode.System

            End Select

        End Sub


        ' An error on the UI thread (#121): the main window shows one plain
        ' dialog and decides whether PaperRoute closes. With no window left
        ' to keep working in, PaperRoute exits as the stock dialog's Quit did.
        Private Sub MyApplication_UnhandledException(
            sender As Object,
            e As UnhandledExceptionEventArgs
        ) Handles Me.UnhandledException

            Dim window As Form1 =
                Application.OpenForms.OfType(Of Form1)().FirstOrDefault()

            If window Is Nothing OrElse window.IsDisposed Then
                Return
            End If

            e.ExitApplication = False
            window.ReportUnhandledError(e.Exception)

        End Sub

    End Class

End Namespace