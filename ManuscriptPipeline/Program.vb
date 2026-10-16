Imports System
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Velopack
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Services

Friend Module Program

    ' The running instance, kept so a restart can release it (#116).
    Private _instance As SingleInstanceService = Nothing

    <STAThread>
    Public Sub Main(
        args As String()
    )

        ' Velopack must be the first application startup work performed.
        ' In a normal F5/developer launch this simply returns and the
        ' existing VB application framework continues as usual.
        VelopackApp.Build().Run()

        ' The example library for teaching (#83) opens in its own disposable
        ' session, configured before any storage folder is resolved, so the
        ' user's library is never opened.
        Dim isExample As Boolean = ExampleLibraryService.IsExampleLaunch(args)
        If isExample Then ExampleLibraryService.StartSession()

        ' Unexpected errors are recorded on this computer and never sent
        ' (#120): a crash anywhere in the process, and a faulted task nobody
        ' awaited, which gets a report only. After the example session is
        ' configured, so its reports go to the disposable session too.
        Dim errorReports As New ErrorReportService()

        AddHandler AppDomain.CurrentDomain.UnhandledException,
            AddressOf errorReports.OnUnhandledException

        AddHandler TaskScheduler.UnobservedTaskException,
            AddressOf errorReports.OnUnobservedTaskException

        ' One window per library (#77): a second launch brings the running
        ' window forward and exits before touching storage.
        Dim instanceKey As String = SingleInstanceService.KeyFor(StorageMigrationService.CurrentDataRoot())
        Dim instance As SingleInstanceService = SingleInstanceService.TryStart(instanceKey)

        If instance Is Nothing Then
            SingleInstanceService.SignalExisting(instanceKey)
            Return
        End If

        _instance = instance

        Try
            Using instance
                Run(args, instance)
            End Using
        Finally
            If isExample Then ExampleLibraryService.EndSession()
        End Try

    End Sub


    ' Frees the library for the PaperRoute a restart is about to start,
    ' which runs before this process has unwound and would otherwise find
    ' the library still held and exit (#116). The release as this process
    ' ends then does nothing. Must run on the main thread, like Main.
    Friend Sub ReleaseInstanceForRestart()

        _instance?.Dispose()

    End Sub


    Private Sub Run(args As String(), instance As SingleInstanceService)

        Try

            ' Validate and migrate PaperRoute storage before settings,
            ' repositories, or the main application are constructed.
            StorageMigrationService.EnsureCurrentStorage()

        Catch ex As Exception

            ' What failed and where, with the actions Diagnostics would
            ' offer if PaperRoute could start (#119).
            StartupFailureForm.ShowBeforeStartup(
                StartupFailure.ForStorage(
                    ex,
                    StorageMigrationService.SchemaFilePath()
                )
            )

            Return

        End Try

        If ExampleLibraryService.IsActive Then ExampleLibraryService.Seed(DateTime.Today, ExampleLibraryService.OnlineSettingsFrom(args))

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