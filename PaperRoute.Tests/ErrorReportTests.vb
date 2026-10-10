Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Unexpected errors are recorded in a local report (#120): a text file on
' this computer with the version, Windows build, exception type, message
' and stack, no manuscript content, the newest ten kept, never sent.
<TestClass>
<DoNotParallelize>
Public Class ErrorReportTests

    Private _root As String = String.Empty
    Private _logs As String = String.Empty
    Private _reports As ErrorReportService = Nothing


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

        _logs =
            Path.Combine(
                _root,
                "logs"
            )

        _reports =
            New ErrorReportService(
                _logs
            )

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        ErrorReportService.DirectoryOverride =
            Nothing

        OnlineAccess.KeyStoreFactory =
            Nothing

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub AThrownException_ProducesAReportWithNoManuscriptContent()

        Dim library As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim reportPath As String =
            String.Empty

        Try

            Throw New InvalidOperationException(
                "The board could not refresh."
            )

        Catch ex As Exception

            reportPath =
                _reports.Write(
                    ex,
                    "main window"
                )

        End Try

        Assert.IsTrue(
            File.Exists(reportPath),
            "The report is written under the logs folder."
        )

        Assert.AreEqual(
            _logs,
            Path.GetDirectoryName(reportPath)
        )

        Dim text As String =
            File.ReadAllText(
                reportPath
            )

        StringAssert.Contains(
            text,
            "Version: " & UpdateService.CurrentVersionText()
        )

        StringAssert.Contains(
            text,
            "Windows: "
        )

        StringAssert.Contains(
            text,
            "Exception: System.InvalidOperationException"
        )

        StringAssert.Contains(
            text,
            "Message: The board could not refresh."
        )

        StringAssert.Contains(
            text,
            "Where: main window"
        )

        StringAssert.Contains(
            text,
            NameOf(AThrownException_ProducesAReportWithNoManuscriptContent),
            "The stack names the method that threw."
        )

        For Each manuscript As Manuscript In library

            Assert.IsFalse(
                text.Contains(manuscript.Title, StringComparison.OrdinalIgnoreCase),
                "No title in the report: " & manuscript.Title
            )

            For Each submission As JournalSubmission In manuscript.Submissions

                If Not String.IsNullOrWhiteSpace(submission.Notes) Then

                    Assert.IsFalse(
                        text.Contains(submission.Notes, StringComparison.OrdinalIgnoreCase),
                        "No note in the report: " & submission.Notes
                    )

                End If

                For Each decision As EditorialDecisionEvent In submission.Decisions

                    Assert.IsFalse(
                        text.Contains(decision.Notes, StringComparison.OrdinalIgnoreCase),
                        "No decision note in the report: " & decision.Notes
                    )

                Next

            Next

        Next

        Assert.AreEqual(
            reportPath,
            _reports.LastReportPath()
        )

    End Sub


    <TestMethod>
    Public Sub AnUnobservedTaskException_ProducesAReportAndNoDialog()

        Dim openForms As Integer =
            Application.OpenForms.Count

        _reports.OnUnobservedTaskException(
            Nothing,
            New UnobservedTaskExceptionEventArgs(
                New AggregateException(
                    New IOException(
                        "A background copy failed."
                    )
                )
            )
        )

        Dim reportPath As String =
            _reports.LastReportPath()

        Assert.IsTrue(
            File.Exists(reportPath),
            "A faulted task nobody awaited is recorded."
        )

        Dim text As String =
            File.ReadAllText(
                reportPath
            )

        StringAssert.Contains(
            text,
            "A background copy failed."
        )

        StringAssert.Contains(
            text,
            "Where: background task"
        )

        ' Guard: no handler existed before this change, so nothing could
        ' have opened a window; the handler must keep it that way, because
        ' an unobserved task exception does not end a .NET process.
        Assert.AreEqual(
            openForms,
            Application.OpenForms.Count,
            "No dialog for a background task's error."
        )

    End Sub


    <TestMethod>
    Public Sub TheEleventhReport_RemovesTheOldest()

        Dim written As New List(Of String)()

        For index As Integer = 1 To ErrorReportService.KeptReports + 1

            written.Add(
                _reports.Write(
                    New InvalidOperationException(
                        "Failure " & index.ToString()
                    ),
                    "test"
                )
            )

        Next

        Assert.AreEqual(
            ErrorReportService.KeptReports + 1,
            written.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Each report has its own file."
        )

        Assert.AreEqual(
            ErrorReportService.KeptReports,
            Directory.GetFiles(_logs).Length,
            "Only the newest ten are kept."
        )

        Assert.IsFalse(
            File.Exists(written(0)),
            "The oldest report is removed."
        )

        For index As Integer = 1 To ErrorReportService.KeptReports

            Assert.IsTrue(
                File.Exists(written(index)),
                "Report " & index.ToString() & " is kept."
            )

        Next

        Assert.AreEqual(
            written(ErrorReportService.KeptReports),
            _reports.LastReportPath()
        )

    End Sub


    <TestMethod>
    Public Sub Diagnostics_ShowTheLastReportPath()

        ErrorReportService.DirectoryOverride =
            _logs

        OnlineAccess.KeyStoreFactory =
            Function() New ProtectedKeyStore(
                Path.Combine(
                    _root,
                    "keys"
                )
            )

        StringAssert.Contains(
            DiagnosticsForm.BuildReport(New AppSettings()),
            "Last error report: None" & Environment.NewLine
        )

        Dim reportPath As String =
            _reports.Write(
                New InvalidOperationException(
                    "Shown in Diagnostics."
                ),
                "test"
            )

        StringAssert.Contains(
            DiagnosticsForm.BuildReport(New AppSettings()),
            "Last error report: " & reportPath & Environment.NewLine
        )

    End Sub

End Class
