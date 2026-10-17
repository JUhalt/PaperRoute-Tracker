Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Microsoft.Win32
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' Windows 11's frame on every top-level window (#143): a title bar that
' follows the theme and rounded corners, set by one helper, WindowChrome,
' from UiPolish.ApplyDialog and from the main window's Load. They were run
' first with the helper in place but called from nowhere.
<TestClass>
<DoNotParallelize>
Public Class WindowChromeTests

    Public Property TestContext As TestContext

    ' Each view in the catalog, shown as a window of its own in Light, Dark
    ' and Follow Windows: the helper set both attributes, Windows took them,
    ' and reading them back gives the same where Windows lets them be read.
    ' The title bar is dark in Dark, light in Light, and in Follow Windows
    ' dark exactly when Windows' app mode is dark (read, never written).
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub EveryWindowGetsTheThemesTitleBarAndRoundCorners(mode As SystemColorMode)

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("Rounded corners and the dark title bar need Windows 11.")
        End If

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If

        Dim expectedDark As Boolean =
            mode = SystemColorMode.Dark OrElse
            (mode = SystemColorMode.System AndAlso WindowsAppsAreDark())

        Dim readable As New HashSet(Of String)()
        Dim views As Integer = 0

        Dim failures As List(Of String) = ViewCatalog.ShowEach(ViewCatalog.All(), mode,
            Sub(name, shown, found)

                views += 1

                Dim frame As WindowChrome.Frame = WindowChrome.FrameOf(shown)

                If frame Is Nothing Then
                    found.Add(name & ": the helper set no frame.")
                    Return
                End If

                If frame.Dark <> expectedDark OrElse frame.DarkResult <> 0 Then
                    found.Add($"{name}: dark title bar {frame.Dark}, answered 0x{frame.DarkResult:X8}; expected {expectedDark}.")
                End If

                If frame.Corners <> WindowChrome.DWMWCP_ROUND OrElse frame.CornersResult <> 0 Then
                    found.Add($"{name}: corners {frame.Corners}, answered 0x{frame.CornersResult:X8}.")
                End If

                Dim value As Integer = 0
                Dim answer As Integer = DwmGetWindowAttribute(shown.Handle, WindowChrome.DWMWA_USE_IMMERSIVE_DARK_MODE, value, 4)
                readable.Add($"dark title bar 0x{answer:X8}")

                If answer = 0 AndAlso (value <> 0) <> expectedDark Then
                    found.Add($"{name}: reads back a dark title bar of {value}.")
                End If

                answer = DwmGetWindowAttribute(shown.Handle, WindowChrome.DWMWA_WINDOW_CORNER_PREFERENCE, value, 4)
                readable.Add($"corners 0x{answer:X8}")

                If answer = 0 AndAlso value <> WindowChrome.DWMWCP_ROUND Then
                    found.Add($"{name}: reads back corners of {value}.")
                End If

            End Sub)

        TestContext.WriteLine($"{mode}: {views} windows; reading back answered: {String.Join(", ", readable)}.")
        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(ViewCatalog.All().Count, views, "Every view was shown.")
        Assert.IsTrue(readable.Contains("dark title bar 0x00000000"), "Windows 11 lets the dark title bar be read back: " & String.Join(", ", readable))

    End Sub


    ' A message box is Windows' own window, made in PaperRoute's process by
    ' MessageBox.Show. The helper can set both attributes on it when handed
    ' its window, but nothing hands it one: the box runs its own modal loop,
    ' where neither UiPolish's idle hook nor a form's Load runs. So in Dark
    ' a message box keeps Windows' light title bar, and message boxes stay
    ' on the Dark allow-list.
    <TestMethod>
    Public Sub AMessageBoxKeepsWindowsTitleBarThoughTheHelperCouldSetIt()

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("The dark title bar needs Windows 11.")
        End If

        Dim found As Boolean = False
        Dim before As Integer = -1
        Dim frame As WindowChrome.Frame = Nothing
        Dim after As Integer = -1
        Dim corners As Integer = -1

        StaRunner.Run(
            Sub()
                Using timer As New Timer With {.Interval = 50}

                    AddHandler timer.Tick,
                        Sub(sender, e)
                            Dim box As IntPtr = DialogOfThisThread(ProbeTitle)
                            If box = IntPtr.Zero Then Return
                            timer.Stop()
                            found = True
                            DwmGetWindowAttribute(box, WindowChrome.DWMWA_USE_IMMERSIVE_DARK_MODE, before, 4)
                            frame = WindowChrome.SetFrame(box)
                            DwmGetWindowAttribute(box, WindowChrome.DWMWA_USE_IMMERSIVE_DARK_MODE, after, 4)
                            DwmGetWindowAttribute(box, WindowChrome.DWMWA_WINDOW_CORNER_PREFERENCE, corners, 4)
                            PostMessage(box, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
                        End Sub

                    timer.Start()
                    MessageBox.Show("A message box for a test.", ProbeTitle, MessageBoxButtons.OK)

                End Using
            End Sub,
            TimeSpan.FromSeconds(30),
            SystemColorMode.Dark)

        Assert.IsTrue(found, "The message box opened on the test's thread.")
        Assert.AreEqual(0, before, "Shown in Dark, the message box's title bar is Windows' light one.")
        Assert.AreEqual(0, frame.DarkResult, "Handed the box's window, the helper sets the dark title bar.")
        Assert.AreEqual(1, after, "The box's title bar is then dark.")
        Assert.AreEqual(0, frame.CornersResult, "And its corners.")
        Assert.AreEqual(WindowChrome.DWMWCP_ROUND, corners)

    End Sub


    ' Guard: the catalog's main window skips Form1_Load, which would read
    ' the library, so the call there is checked in the source, as the
    ' recovery draft's offer is; ApplyDialog's is proved by every dialog
    ' above. It fails only if the call leaves Load or moves after the
    ' window is built.
    <TestMethod>
    Public Sub Form1LoadSetsTheFrameBeforeBuildingTheWindow()

        Dim source As String =
            File.ReadAllText(
                Path.Combine(
                    AssistantCoreTests.RepositoryRoot(),
                    "ManuscriptPipeline",
                    "Form1.vb"
                )
            )

        Dim loading As Integer =
            source.IndexOf("Private Sub Form1_Load(", StringComparison.Ordinal)

        Dim building As Integer =
            source.IndexOf("BuildInterface()", Math.Max(loading, 0), StringComparison.Ordinal)

        Dim framing As Integer =
            source.IndexOf("WindowChrome.Apply(Me)", Math.Max(loading, 0), StringComparison.Ordinal)

        Assert.IsTrue(
            loading >= 0 AndAlso framing > loading AndAlso framing < building,
            "Form1_Load sets the window's frame before it builds the window."
        )

    End Sub


    Private Const ProbeTitle As String = "PaperRoute frame probe"

    Private Const WM_CLOSE As UInteger = &H10


    ' Windows' app mode, read and never written: 0 is dark.
    Private Shared Function WindowsAppsAreDark() As Boolean

        Using key As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable:=False)
            Dim value As Object = key?.GetValue("AppsUseLightTheme")
            Return TypeOf value Is Integer AndAlso DirectCast(value, Integer) = 0
        End Using

    End Function


    ' A dialog box of this thread with the title, or none.
    Private Shared Function DialogOfThisThread(title As String) As IntPtr

        Dim found As IntPtr = IntPtr.Zero

        EnumThreadWindows(
            GetCurrentThreadId(),
            Function(window, parameter)
                Dim className As New StringBuilder(64)
                Dim caption As New StringBuilder(256)
                GetClassName(window, className, className.Capacity)
                GetWindowText(window, caption, caption.Capacity)
                If className.ToString() = "#32770" AndAlso caption.ToString() = title Then
                    found = window
                    Return False
                End If
                Return True
            End Function,
            IntPtr.Zero)

        Return found

    End Function


    Private Delegate Function EnumWindowsCallback(window As IntPtr, parameter As IntPtr) As Boolean

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmGetWindowAttribute(window As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsCallback, parameter As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(window As IntPtr, text As StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetWindowText(window As IntPtr, text As StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

End Class
