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
' from UiPolish.ApplyDialog and from the main window's Load, and on
' message boxes and file dialogs by its dialog hook (#188). They were run
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
        Dim found As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach(ViewCatalog.AllViewsAndStages(),
            Sub(name, shown)

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

            End Sub,
            mode:=mode)

        TestContext.WriteLine($"{mode}: {views} windows; reading back answered: {String.Join(", ", readable)}.")
        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(ViewCatalog.AllViewsAndStages().Count, views, "Every view was shown.")
        Assert.IsTrue(readable.Contains("dark title bar 0x00000000"), "Windows 11 lets the dark title bar be read back: " & String.Join(", ", readable))

    End Sub


    ' A message box is Windows' own window, made in PaperRoute's process by
    ' MessageBox.Show, in a modal loop of Windows' own where neither
    ' UiPolish's idle hook nor a form's Load runs (#188). With the dialog
    ' hook on the thread that shows it, the box is framed as it is
    ' activated: read back from its window once shown, its title bar is
    ' dark in Dark and light in Light, and under a contrast theme light,
    ' which leaves Windows' contrast frame (#138); its corners are round.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Dark, True)>
    Public Sub AMessageBoxGetsTheThemesTitleBarFromTheDialogHook(mode As SystemColorMode, contrastTheme As Boolean)

        Dim shown As HookedDialog =
            ShowWithDialogHook(
                Sub() MessageBox.Show("A message box for a test.", ProbeTitle, MessageBoxButtons.OK),
                mode,
                contrastTheme)

        AssertFramed(shown, mode = SystemColorMode.Dark AndAlso Not contrastTheme)

    End Sub


    ' The Open and Save dialogs and the folder picker are Windows' common
    ' item dialog, made on the thread that shows it like a message box, and
    ' each takes the frame from the hook in the same way (#188). Each opens
    ' on a folder the test makes, adds nothing to Windows' recent items and
    ' is cancelled.
    '
    ' Run only on demand, with PAPERROUTE_FILE_DIALOG_TESTS set, because a
    ' file dialog writes outside the test's folder: Windows' shell keeps
    ' view state under HKCU for every folder a file dialog shows (Shell's
    ' BagMRU and Bags), which outlives the folder, and on closing saves the
    ' dialog's size for testhost.exe as one value under ComDlg32's
    ' CIDSizeMRU. CI sets the variable on its disposable runners, and the
    ' local candidate build leaves the OnDemand category out (#196).
    ' Cancelled, with AddToRecent off, it adds no recent item
    ' and no last folder. Run on October 17, 2026, the four rows without a
    ' contrast theme passed: the Open and Save dialogs and the folder
    ' picker took the frame and read back dark in Dark and light in Light.
    ' Under a contrast theme the hook asks for what the Classic row asks
    ' for, as IsDark() is false; in routine runs that case rests on the
    ' message box's contrast row, through the same hook and SetFrame.
    <TestMethod>
    <TestCategory("OnDemand")>
    <DataRow(SystemColorMode.Classic, "Open", False)>
    <DataRow(SystemColorMode.Dark, "Open", False)>
    <DataRow(SystemColorMode.Dark, "Open", True)>
    <DataRow(SystemColorMode.Dark, "Save", False)>
    <DataRow(SystemColorMode.Dark, "Folder", False)>
    Public Sub TheFileDialogsGetTheThemesTitleBarFromTheDialogHook(mode As SystemColorMode, kind As String, contrastTheme As Boolean)

        If String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(FileDialogTestsVariable)) Then
            Assert.Inconclusive($"A file dialog writes Windows' view state for its folder under HKCU; set {FileDialogTestsVariable} to run it.")
        End If

        Dim folder As String =
            CreateTemporaryRoot()

        Try

            Dim shown As HookedDialog =
                ShowWithDialogHook(
                    Sub()
                        Using dialog As CommonDialog = FileDialogOf(kind, folder)
                            dialog.ShowDialog()
                        End Using
                    End Sub,
                    mode,
                    contrastTheme)

            AssertFramed(shown, mode = SystemColorMode.Dark AndAlso Not contrastTheme)

        Finally
            DeleteTemporaryRoot(folder)
        End Try

    End Sub


    ' The hook hears only of its own thread's windows, so never of another
    ' process's: in Dark, a message box another thread shows while the
    ' hook stands is not framed, and keeps Windows' light title bar. A
    ' guard: before #188 there was no hook to hear of it, so it cannot
    ' fail before; it fails if the hook ever reaches past its thread.
    <TestMethod>
    Public Sub TheDialogHookFramesOnlyItsOwnThreadsDialogs()

        SkipWhereTitleBarsCannotBeChecked()

        Dim heard As New List(Of String)()
        Dim box As IntPtr = IntPtr.Zero
        Dim dark As Integer = -1

        StaRunner.Run(
            Sub()
                Using WindowChrome.HookDialogs(Sub(window, frame) heard.Add(TitleOf(window)))

                    ' Windows' own message box, so no window of WinForms' is
                    ' made on the other thread.
                    Dim other As New Threading.Thread(
                        Sub() MessageBoxW(IntPtr.Zero, "A message box on another thread.", OtherTitle, 0)
                    ) With {.IsBackground = True}

                    other.SetApartmentState(Threading.ApartmentState.STA)
                    other.Start()

                    Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()

                    Do
                        Application.DoEvents()
                        Threading.Thread.Sleep(20)
                        box = FindWindow(DialogBoxClass, OtherTitle)
                    Loop Until (box <> IntPtr.Zero AndAlso IsWindowVisible(box)) OrElse waited.Elapsed > TimeSpan.FromSeconds(10)

                    ' Give the hook every chance to hear of the box first.
                    For count As Integer = 1 To 5
                        Application.DoEvents()
                        Threading.Thread.Sleep(20)
                    Next

                    If box <> IntPtr.Zero Then
                        DwmGetWindowAttribute(box, WindowChrome.DWMWA_USE_IMMERSIVE_DARK_MODE, dark, 4)
                        PostMessage(box, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
                    End If

                    other.Join(TimeSpan.FromSeconds(10))

                End Using
            End Sub,
            TimeSpan.FromSeconds(30),
            SystemColorMode.Dark)

        Assert.AreNotEqual(IntPtr.Zero, box, "The other thread's message box opened.")
        CollectionAssert.DoesNotContain(heard, OtherTitle, "The hook never heard of the other thread's box.")
        Assert.AreEqual(0, dark, "In Dark, the other thread's box keeps Windows' light title bar.")

    End Sub


    ' Guard: the app's UI thread is the one Main runs on. Main puts the
    ' hook on it around everything the app shows and removes it as the app
    ' ends, and nothing else in the app hooks a thread. It fails before
    ' #188, when nothing put the hook anywhere.
    <TestMethod>
    Public Sub MainHooksTheUiThreadsDialogsAroundTheWholeApp()

        Dim app As String =
            Path.Combine(
                AssistantCoreTests.RepositoryRoot(),
                "ManuscriptPipeline"
            )

        Dim source As String =
            File.ReadAllText(Path.Combine(app, "Program.vb"))

        Dim main As Integer =
            source.IndexOf("Public Sub Main(", StringComparison.Ordinal)

        Dim hooking As Integer =
            source.IndexOf("Using WindowChrome.HookDialogs()", Math.Max(main, 0), StringComparison.Ordinal)

        Dim running As Integer =
            source.IndexOf("Run(args, instance)", Math.Max(hooking, 0), StringComparison.Ordinal)

        Dim ending As Integer =
            source.IndexOf("End Sub", Math.Max(main, 0), StringComparison.Ordinal)

        Assert.IsTrue(
            main >= 0 AndAlso hooking > main AndAlso running > hooking AndAlso running < ending,
            "Main hooks the dialogs of its own thread around the whole app."
        )

        Dim callers As List(Of String) =
            Directory.GetFiles(app, "*.vb", SearchOption.AllDirectories).
                Where(Function(candidate) Not candidate.Contains("\obj\", StringComparison.OrdinalIgnoreCase) AndAlso
                                          Not candidate.Contains("\bin\", StringComparison.OrdinalIgnoreCase) AndAlso
                                          Not candidate.EndsWith("WindowChrome.vb", StringComparison.OrdinalIgnoreCase) AndAlso
                                          File.ReadAllText(candidate).Contains("HookDialogs(", StringComparison.Ordinal)).
                Select(Function(candidate) Path.GetFileName(candidate)).
                ToList()

        CollectionAssert.AreEqual(
            New List(Of String) From {"Program.vb"},
            callers,
            "Only Main hooks a thread: " & String.Join(", ", callers)
        )

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

    Private Const OtherTitle As String = "PaperRoute frame probe, another thread"

    Private Const DialogBoxClass As String = "#32770"

    ' Set, to any value, to run the file dialog cases.
    Private Const FileDialogTestsVariable As String = "PAPERROUTE_FILE_DIALOG_TESTS"

    Private Const WM_CLOSE As UInteger = &H10


    ' What the hook set on the probe's dialog box, and what its window
    ' answered once shown for each attribute (-1 when never read).
    Private NotInheritable Class HookedDialog
        Public Frame As WindowChrome.Frame
        Public Dark As Integer = -1
        Public Corners As Integer = -1
    End Class


    Private Shared Sub SkipWhereTitleBarsCannotBeChecked()

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("Rounded corners and the dark title bar need Windows 11.")
        End If

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If

    End Sub


    ' Shows a dialog box titled ProbeTitle on the shared runner with the
    ' dialog hook on its thread, in the mode, and under a contrast theme if
    ' asked (the seam stands in for one). Once the hook has framed the box
    ' and it shows, both attributes are read back from its window and it is
    ' told to close; a box the hook never framed is closed after ten
    ' seconds. The hook is removed before the runner's thread ends.
    Private Shared Function ShowWithDialogHook(
        show As Action,
        mode As SystemColorMode,
        contrastTheme As Boolean
    ) As HookedDialog

        SkipWhereTitleBarsCannotBeChecked()

        Dim shown As New HookedDialog()
        Dim framed As IntPtr = IntPtr.Zero

        StaRunner.Run(
            Sub()

                If contrastTheme Then
                    UiTheme.HighContrastOverride = True
                End If

                Try
                    Using poll As New Timer With {.Interval = 50}

                        Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()

                        AddHandler poll.Tick,
                            Sub(sender, e)

                                If framed <> IntPtr.Zero AndAlso IsWindowVisible(framed) Then
                                    poll.Stop()
                                    DwmGetWindowAttribute(framed, WindowChrome.DWMWA_USE_IMMERSIVE_DARK_MODE, shown.Dark, 4)
                                    DwmGetWindowAttribute(framed, WindowChrome.DWMWA_WINDOW_CORNER_PREFERENCE, shown.Corners, 4)
                                    PostMessage(framed, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
                                ElseIf waited.Elapsed > TimeSpan.FromSeconds(10) Then
                                    Dim box As IntPtr = DialogOfThisThread(ProbeTitle)
                                    If box <> IntPtr.Zero Then
                                        poll.Stop()
                                        PostMessage(box, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
                                    End If
                                End If

                            End Sub

                        Using WindowChrome.HookDialogs(
                            Sub(window, frame)
                                If framed = IntPtr.Zero AndAlso TitleOf(window) = ProbeTitle Then
                                    framed = window
                                    shown.Frame = frame
                                End If
                            End Sub)

                            poll.Start()
                            show()

                        End Using

                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try

            End Sub,
            TimeSpan.FromSeconds(60),
            mode)

        Return shown

    End Function


    Private Shared Sub AssertFramed(
        shown As HookedDialog,
        expectedDark As Boolean
    )

        Assert.IsNotNull(shown.Frame, "The hook framed the dialog box as it was activated.")
        Assert.AreEqual(expectedDark, shown.Frame.Dark, "The hook asked for the theme's title bar.")
        Assert.AreEqual(0, shown.Frame.DarkResult, $"Windows took the title bar: 0x{shown.Frame.DarkResult:X8}.")
        Assert.AreEqual(0, shown.Frame.CornersResult, $"Windows took the corners: 0x{shown.Frame.CornersResult:X8}.")
        Assert.AreEqual(If(expectedDark, 1, 0), shown.Dark, "Read back from the box once shown, its title bar is " & If(expectedDark, "dark.", "light."))
        Assert.AreEqual(WindowChrome.DWMWCP_ROUND, shown.Corners, "Read back, its corners are round.")

    End Sub


    ' A file dialog of the kind, titled ProbeTitle, on the folder.
    Private Shared Function FileDialogOf(
        kind As String,
        folder As String
    ) As CommonDialog

        Select Case kind

            Case "Open"
                Return New OpenFileDialog With {.Title = ProbeTitle, .InitialDirectory = folder, .AddToRecent = False, .ShowPinnedPlaces = False, .RestoreDirectory = True}

            Case "Save"
                Return New SaveFileDialog With {.Title = ProbeTitle, .InitialDirectory = folder, .AddToRecent = False, .ShowPinnedPlaces = False, .RestoreDirectory = True, .OverwritePrompt = False}

            Case Else
                Return New FolderBrowserDialog With {.Description = ProbeTitle, .UseDescriptionForTitle = True, .InitialDirectory = folder, .AddToRecent = False, .ShowPinnedPlaces = False}

        End Select

    End Function


    Private Shared Function TitleOf(window As IntPtr) As String

        Dim caption As New StringBuilder(256)
        GetWindowText(window, caption, caption.Capacity)
        Return caption.ToString()

    End Function


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

    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(window As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function FindWindow(className As String, title As String) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function MessageBoxW(owner As IntPtr, text As String, caption As String, style As UInteger) As Integer
    End Function

End Class
