Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' A link opens the address the shell got in PaperRoute 1.0.x (core stage 2,
' risk R6): the trimmed text itself, never a Uri's rewritten form, which
' could add a slash, lower-case the host or change escaping. An unsafe
' address is refused before any launcher is reached. The recording launcher
' opens nothing.
<TestClass>
<DoNotParallelize>
Public Class LauncherTextTests

    <TestInitialize>
    Public Sub Initialize()
        CoreTestPlatform.Launcher.Clear()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        CoreTestPlatform.Launcher.Clear()
    End Sub


    <TestMethod>
    Public Sub TheLauncherGetsTheTrimmedTextExactly()

        UrlSafetyService.OpenInBrowser("  https://example.org  ")

        CollectionAssert.AreEqual({"https://example.org"}, CoreTestPlatform.Launcher.Links)
        Assert.AreEqual(0, CoreTestPlatform.Launcher.Files.Count)

    End Sub


    <TestMethod>
    Public Sub TheLauncherGetsNoUriRewrite()

        For Each address As String In {
            "https://example.org",
            "https://EXAMPLE.org/Path?q=a%20b#part",
            "http://example.org/a/../b"
        }

            CoreTestPlatform.Launcher.Clear()

            UrlSafetyService.OpenInBrowser(address)

            CollectionAssert.AreEqual({address}, CoreTestPlatform.Launcher.Links, address)
            Assert.AreNotEqual(New Uri(address).ToString(), CoreTestPlatform.Launcher.Links(0), "Uri would have changed " & address)

        Next

    End Sub


    <TestMethod>
    Public Sub AnUnsafeAddressThrowsAndReachesNothing()

        For Each value As String In {
            "C:\Windows\System32\calc.exe",
            "file:///C:/Windows/System32/calc.exe",
            "\\server\share\run.bat",
            "javascript:alert(1)",
            "ms-settings:",
            "ftp://example.org/file",
            "   ",
            String.Empty,
            Nothing
        }

            Dim failure As ArgumentException =
                Assert.ThrowsExactly(Of ArgumentException)(Sub() UrlSafetyService.OpenInBrowser(value))

            Assert.AreEqual("Only valid http:// and https:// links can be opened.", failure.Message)

        Next

        Assert.AreEqual(0, CoreTestPlatform.Launcher.Links.Count)
        Assert.AreEqual(0, CoreTestPlatform.Launcher.Files.Count)

    End Sub

End Class
