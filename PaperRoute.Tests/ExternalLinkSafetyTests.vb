Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Links and files that arrive in a library open safely (#117).
<TestClass>
Public Class ExternalLinkSafetyTests

    ' A Source URL that is a program path or a file: address starts no
    ' process: OpenInBrowser refuses anything but http and https before it
    ' reaches Process.Start. A guard: UrlSafetyService refused these
    ' already; the scan below is what proves the Open Source button and
    ' the other link sites now go through it.
    <TestMethod>
    Public Sub SourceUrl_ProgramPathOrFileAddressStartsNoProcess()

        For Each value As String In {
            "C:\Windows\System32\calc.exe",
            "file:///C:/Windows/System32/calc.exe",
            "\\server\share\run.bat",
            "javascript:alert(1)",
            "ms-settings:",
            String.Empty
        }

            Dim failure As ArgumentException =
                Assert.ThrowsExactly(Of ArgumentException)(Sub() UrlSafetyService.OpenInBrowser(value))

            StringAssert.Contains(failure.Message, "http://", value)

        Next

    End Sub


    ' A recorded file of a program or script type asks first, and stays
    ' closed when the answer is no; a document opens without a question.
    <TestMethod>
    Public Sub OpenRecordedFile_AsksBeforeAProgramOrScriptAndNotBeforeADocument()

        For Each extension As String In {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".lnk", ".msi",
            ".com", ".scr", ".jse", ".wsf", ".hta", ".reg", ".EXE", ".Ps1"
        }

            Dim filePath As String = "C:\PaperRoute Library\letter" & extension
            Dim asked As New List(Of String)()
            Dim started As New List(Of String)()

            FileOpenService.OpenRecordedFile(filePath, Function(candidate) Answer(asked, candidate, False), AddressOf started.Add)
            CollectionAssert.AreEqual({filePath}, asked, extension & " asks first.")
            Assert.AreEqual(0, started.Count, extension & " stays closed after No.")

            FileOpenService.OpenRecordedFile(filePath, Function(candidate) Answer(asked, candidate, True), AddressOf started.Add)
            CollectionAssert.AreEqual({filePath}, started, extension & " opens after Yes.")

        Next

        For Each extension As String In {".docx", ".pdf", ".html", ".txt", ".xlsx", ""}

            Dim filePath As String = "C:\PaperRoute Library\letter" & extension
            Dim asked As New List(Of String)()
            Dim started As New List(Of String)()

            FileOpenService.OpenRecordedFile(filePath, Function(candidate) Answer(asked, candidate, False), AddressOf started.Add)
            Assert.AreEqual(0, asked.Count, extension & " opens without a question.")
            CollectionAssert.AreEqual({filePath}, started)

        Next

        ' Windows ignores trailing dots and spaces, so "calc.exe ." runs calc.exe.
        Assert.IsTrue(FileOpenService.IsProgramOrScript("C:\tools\calc.exe ."))
        Assert.IsFalse(FileOpenService.IsProgramOrScript(Nothing))
        Assert.IsFalse(FileOpenService.IsProgramOrScript("   "))

    End Sub


    ' The shell is reached from three places only: the browser opener for
    ' web links, the file opener that asks before a program, and the
    ' open-folder button in Diagnostics.
    <TestMethod>
    Public Sub TheShellIsReachedOnlyThroughTheTwoOpenersAndDiagnostics()

        Dim root As String = Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")
        Dim found As New List(Of String)()

        For Each source As String In Directory.GetFiles(root, "*.vb", SearchOption.AllDirectories)

            If source.Contains(Path.DirectorySeparatorChar & "obj" & Path.DirectorySeparatorChar) OrElse
               source.Contains(Path.DirectorySeparatorChar & "bin" & Path.DirectorySeparatorChar) Then Continue For

            Dim code As String = String.Join(Environment.NewLine, File.ReadAllLines(source).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))

            ' One site writes the assignment across two lines.
            Dim uses As Integer = Regex.Matches(Regex.Replace(code, "\s+", ""), "UseShellExecute=True").Count

            For index As Integer = 1 To uses
                found.Add(Path.GetFileName(source))
            Next

        Next

        found.Sort(StringComparer.Ordinal)
        CollectionAssert.AreEqual({"DiagnosticsForm.vb", "FileOpenService.vb", "UrlSafetyService.vb"}, found, String.Join(", ", found))

    End Sub


    ' Records the question and answers it.
    Private Shared Function Answer(asked As List(Of String), filePath As String, yes As Boolean) As Boolean
        asked.Add(filePath)
        Return yes
    End Function

End Class
