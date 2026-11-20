Imports System
Imports System.Diagnostics
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The Windows launcher hands the shell what PaperRoute 1.0.x handed it
' (core stage 2, step 10): the address or path itself as the file name,
' UseShellExecute, no verb, no arguments, no folder. Read from the start
' information it builds; no test here starts a process.
<TestClass>
Public Class WindowsLauncherTests

    <TestMethod>
    Public Sub TheShellGetsTheTextItselfAndNothingElse()

        For Each target As String In {
            "https://example.org",
            "https://EXAMPLE.org/Path?q=a%20b#part",
            "C:\PaperRoute Library\letter.docx",
            "C:\tools\calc.exe ."
        }

            Dim start As ProcessStartInfo = WindowsLauncher.ShellStartInfo(target)

            Assert.AreEqual(target, start.FileName, "The text itself, not a Uri's form.")
            Assert.IsTrue(start.UseShellExecute, target)
            Assert.AreEqual(String.Empty, start.Verb, target)
            Assert.AreEqual(String.Empty, start.Arguments, target)
            Assert.AreEqual(0, start.ArgumentList.Count, target)
            Assert.AreEqual(String.Empty, start.WorkingDirectory, target)
            Assert.IsFalse(start.CreateNoWindow, target)
            Assert.AreEqual(ProcessWindowStyle.Normal, start.WindowStyle, target)

        Next

    End Sub

End Class
