Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading.Tasks

Namespace Services

    ' A record of an unexpected error, kept on this computer and never sent
    ' (#120): the version, Windows build, exception type, message and stack,
    ' and no manuscript content. Reports live under the data root's logs
    ' folder, the newest ten are kept, and Diagnostics shows the newest.
    Public Class ErrorReportService

        Public Const KeptReports As Integer = 10

        Private Const ReportPrefix As String = "error-report_"

        ' For tests: reports in a folder of their own.
        Friend Shared DirectoryOverride As String = Nothing

        Private ReadOnly _directory As String


        Public Sub New()

            Me.New(
                If(DirectoryOverride, DefaultDirectory())
            )

        End Sub


        Friend Sub New(
            directory As String
        )

            If String.IsNullOrWhiteSpace(directory) Then
                Throw New ArgumentException("A report directory is required.", NameOf(directory))
            End If

            _directory =
                Path.GetFullPath(directory)

        End Sub


        Public Shared Function DefaultDirectory() As String

            Return Path.Combine(
                StorageMigrationService.CurrentDataRoot(),
                "logs"
            )

        End Function


        Public ReadOnly Property ReportDirectory As String
            Get
                Return _directory
            End Get
        End Property


        ' Writes a report and returns its path, or an empty string when the
        ' disk refuses: recording an error must never add a failure to it.
        Public Function Write(
            exception As Exception,
            where As String
        ) As String

            If exception Is Nothing Then
                Return String.Empty
            End If

            Try

                Directory.CreateDirectory(_directory)

                Dim reportPath As String =
                    NextReportPath()

                File.WriteAllText(
                    reportPath,
                    Compose(exception, where),
                    New UTF8Encoding(False)
                )

                Prune()

                Return reportPath

            Catch

                Return String.Empty

            End Try

        End Function


        ' The newest report's path, or an empty string when there is none.
        Public Function LastReportPath() As String

            Try
                Return If(Reports().FirstOrDefault(), String.Empty)
            Catch
                Return String.Empty
            End Try

        End Function


        ' For Program.Main: a crash anywhere in the process.
        Public Sub OnUnhandledException(
            sender As Object,
            e As UnhandledExceptionEventArgs
        )

            Write(TryCast(e.ExceptionObject, Exception), "process")

        End Sub


        ' For Program.Main: a faulted task nobody awaited. It does not end a
        ' .NET process and is not made to: a report only, no dialog, no exit.
        Public Sub OnUnobservedTaskException(
            sender As Object,
            e As UnobservedTaskExceptionEventArgs
        )

            Write(e.Exception, "background task")

        End Sub


        Private Shared Function Compose(
            exception As Exception,
            where As String
        ) As String

            Dim builder As New StringBuilder()

            builder.AppendLine("PaperRoute error report")
            builder.AppendLine("=======================")
            builder.AppendLine()
            builder.AppendLine("Time: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            builder.AppendLine("Version: " & UpdateService.CurrentVersionText())
            builder.AppendLine("Windows: " & RuntimeInformation.OSDescription)
            builder.AppendLine("Runtime: " & RuntimeInformation.FrameworkDescription)
            builder.AppendLine("Where: " & If(String.IsNullOrWhiteSpace(where), "unknown", where))
            builder.AppendLine("Exception: " & exception.GetType().FullName)
            builder.AppendLine("Message: " & exception.Message)
            builder.AppendLine()
            builder.AppendLine(exception.ToString())

            Return builder.ToString()

        End Function


        ' Named by the moment, so the names sort oldest to newest; a second
        ' report in the same millisecond gets a numbered suffix, which sorts
        ' after the first.
        Private Function NextReportPath() As String

            Dim stamp As String =
                ReportPrefix & DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")

            Dim candidate As String =
                Path.Combine(_directory, stamp & ".txt")

            Dim suffix As Integer = 2

            While File.Exists(candidate)
                candidate = Path.Combine(_directory, stamp & "_" & suffix.ToString("00") & ".txt")
                suffix += 1
            End While

            Return candidate

        End Function


        ' Newest first.
        Private Function Reports() As List(Of String)

            If Not Directory.Exists(_directory) Then
                Return New List(Of String)()
            End If

            Return Directory.GetFiles(_directory, ReportPrefix & "*.txt").
                OrderByDescending(Function(reportFile) Path.GetFileName(reportFile), StringComparer.Ordinal).
                ToList()

        End Function


        Private Sub Prune()

            For Each old As String In Reports().Skip(KeptReports)

                Try
                    File.Delete(old)
                Catch
                    ' One report too many is kept; the next write tries again.
                End Try

            Next

        End Sub

    End Class

End Namespace
