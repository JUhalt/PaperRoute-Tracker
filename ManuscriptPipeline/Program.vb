Imports System
Imports System.Windows.Forms
Imports Velopack
Imports ManuscriptPipeline.Services

Friend Module Program

    <STAThread>
    Public Sub Main(
        args As String()
    )

        ' Velopack must be the first application startup work performed.
        ' In a normal F5/developer launch this simply returns and the
        ' existing VB application framework continues as usual.
        VelopackApp.Build().Run()

        ' One window per library (#77): a second launch brings the running
        ' window forward and exits before touching storage.
        Dim instanceKey As String = SingleInstanceService.KeyFor(StorageMigrationService.CurrentDataRoot())
        Dim instance As SingleInstanceService = SingleInstanceService.TryStart(instanceKey)

        If instance Is Nothing Then
            SingleInstanceService.SignalExisting(instanceKey)
            Return
        End If

        Using instance
            Run(args, instance)
        End Using

    End Sub


    Private Sub Run(args As String(), instance As SingleInstanceService)

        Try

            ' Validate and migrate PaperRoute storage before settings,
            ' repositories, or the main application are constructed.
            StorageMigrationService.EnsureCurrentStorage()

        Catch ex As Exception

            MessageBox.Show(
                "PaperRoute could not safely prepare its local storage." &
                Environment.NewLine &
                Environment.NewLine &
                "The storage format could not be verified, so PaperRoute will close rather than risk opening or rewriting data with an unknown format." &
                Environment.NewLine &
                Environment.NewLine &
                "Existing source data has been preserved wherever possible." &
                Environment.NewLine &
                Environment.NewLine &
                "Error details:" &
                Environment.NewLine &
                ex.Message,
                "PaperRoute Storage Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            Return

        End Try

        Dim application As New My.MyApplication()

        instance.Listen(
            Sub()
                Dim window As Form = Application.OpenForms.OfType(Of Form1)().FirstOrDefault()
                If window Is Nothing OrElse window.IsDisposed OrElse Not window.IsHandleCreated Then Return
                window.BeginInvoke(
                    New Action(
                        Sub()
                            If window.WindowState = FormWindowState.Minimized Then window.WindowState = FormWindowState.Normal
                            window.Activate()
                            window.BringToFront()
                        End Sub))
            End Sub)

        application.Run(
            args
        )

    End Sub

End Module