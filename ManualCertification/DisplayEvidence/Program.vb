Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services
Imports PaperRoute.Tests

' How every top-level window of PaperRoute opens at this computer's own
' display, for the calibration of the scaling measure against a real
' display (#93, step 2). The maintainer runs it on the certification VM
' at 1920x1080 at 150% and at 1366x768 at 125% (ManualCertification's
' README, "Display evidence"). Each window of the view catalog is built
' and shown on a temporary data root of its own, as the sweep builds it,
' so no PaperRoute data on the computer is read or written. It is fitted
' to the working area of the screen it opens on, as the app fits it, and
' inspected as a cell of the sweep's table is, with nothing simulated.
' The evidence is one JSON file and one markdown table in the sweep's
' format, and with --png a picture of each window.
Friend Module Program

    Private Const Usage As String =
        "Usage: PaperRoute.DisplayEvidence.exe [--out <folder>] [--png]. " &
        "--out is the folder the evidence is written to (by default display-evidence-<yyyyMMdd-HHmmss> beside the tool), never one under %TEMP%; " &
        "--png adds a picture of each window."

    ' Nobody may be watching (the tool is started from a Run box), so
    ' nothing waits for OK: a failure goes to standard error with one of
    ' these exit codes, as the workflow demo's harness reports (#146).
    Private Const BadArguments As Integer = 2
    Private Const SetupFailed As Integer = 3
    Private Const WindowsFailed As Integer = 4
    Private Const Unexpected As Integer = 5

    ' The evidence, written once every window is done.
    Private Const JsonFile As String = "display-evidence.json"
    Private Const TableFile As String = "display-evidence.md"

    ' One line of a caption, as the measure reads it.
    Private Const CaptionFlags As TextFormatFlags =
        TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.HidePrefix

    Private ReadOnly JsonOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }


    <STAThread>
    Public Function Main(args As String()) As Integer

        ' Before any window, the mode the app pins (ApplicationEvents.vb),
        ' so each window opens at the DPI the installed build opens it at.
        Application.SetHighDpiMode(HighDpiMode.SystemAware)

        Try
            Return Run(args)
        Catch ex As Exception
            Return Report(Unexpected, ex.ToString())
        End Try

    End Function


    Private Function Run(args As String()) As Integer

        Dim out As String = Nothing
        Dim pictures As Boolean = False
        Dim remaining As New Queue(Of String)(args)

        While remaining.Count > 0

            Dim argument As String = remaining.Dequeue()

            If argument.Equals("--png", StringComparison.OrdinalIgnoreCase) AndAlso Not pictures Then
                pictures = True
            ElseIf argument.Equals("--out", StringComparison.OrdinalIgnoreCase) AndAlso out Is Nothing AndAlso
                   remaining.Count > 0 AndAlso Not remaining.Peek().StartsWith("--", StringComparison.Ordinal) Then
                out = remaining.Dequeue()
            Else
                Return Report(BadArguments, Usage)
            End If

        End While

        Dim folder As String = Nothing

        Try
            folder = Path.GetFullPath(If(out, Path.Combine(AppContext.BaseDirectory, "display-evidence-" & DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))))
        Catch ex As Exception
            Return Report(BadArguments, "The evidence folder is not a path: " & out & ". " & Usage)
        End Try

        ' Antivirus on the maintainer's computer removes PaperRouteTracker.dll
        ' from %TEMP%, and evidence is kept, so it never goes there.
        If IsUnder(folder, Path.GetTempPath()) Then
            Return Report(BadArguments, "The evidence folder must be outside %TEMP%: " & folder)
        End If

        ' Each window's scaling kind is read from the app's source, which the
        ' tool carries beside itself.
        Dim source As String = Path.Combine(AppContext.BaseDirectory, "AppSource")

        If Not Directory.Exists(Path.Combine(source, "Forms")) Then
            Return Report(SetupFailed, "The app's source is missing beside the tool (" & source & "); copy the whole published folder.")
        End If

        Dim primary As Screen = Screen.PrimaryScreen
        Dim dpi As Integer

        Using desktop As Graphics = Graphics.FromHwnd(IntPtr.Zero)
            dpi = CInt(Math.Round(desktop.DpiX))
        End Using

        Dim evidence As New Evidence With {
            .Display = primary.WorkingArea.Width & "x" & primary.WorkingArea.Height & " @ " & dpi,
            .RecordedAtUtc = DateTime.UtcNow,
            .Environment = New DisplayEnvironment With {
                .OSVersion = Environment.OSVersion.VersionString,
                .PrimaryScreenBounds = New Box(primary.Bounds),
                .PrimaryScreenWorkingArea = New Box(primary.WorkingArea),
                .ScreenDpi = dpi,
                .HighDpiMode = Application.HighDpiMode.ToString(),
                .AppVersion = GetType(ResponsiveDialogSizingService).Assembly.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()?.InformationalVersion
            }
        }

        ' A folder that cannot be written is found before any window opens.
        ' The evidence itself is written once every window is done, and an
        ' earlier run's is removed first, so a folder that holds it holds a
        ' finished run, even a folder used again.
        Try
            Directory.CreateDirectory(folder)
            Dim probe As String = Path.Combine(folder, "display-evidence.probe")
            File.WriteAllText(probe, String.Empty)
            File.Delete(probe)
            File.Delete(Path.Combine(folder, JsonFile))
            File.Delete(Path.Combine(folder, TableFile))
        Catch ex As Exception
            Return Report(SetupFailed, "The evidence could not be written to " & folder & ": " & ex.Message)
        End Try

        Dim scaling As Dictionary(Of String, String) =
            ViewCatalog.Dialogs.Keys.ToDictionary(
                Function(view) view.Name,
                Function(view) ScalingMeasurementTests.ScalingOf(view, source).Scaling
            )

        Dim windows As New List(Of WindowEvidence)()

        ' An error in one of a window's handlers, by the window's name.
        Dim faults As New ConcurrentDictionary(Of String, Exception)()

        ' ShowEach gives each window a temporary data root and fails one
        ' that asks for the default root, so the computer's own PaperRoute
        ' data is never used.
        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.Dialogs.Select(Function(entry) (entry.Key.Name, Guarded(entry.Key.Name, entry.Value, faults))),
                Sub(name, shown)

                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.MeasureAtThisDisplay(shown)

                    Dim window As New WindowEvidence With {
                        .Name = name,
                        .Scaling = scaling(name),
                        .Size = New Extent(shown.Size),
                        .ClientSize = New Extent(shown.ClientSize),
                        .DeviceDpi = shown.DeviceDpi,
                        .AcceptInside = cell.AcceptInside,
                        .CancelInside = cell.CancelInside,
                        .Cut = cell.Cut.ToList(),
                        .Cell = cell.Describe(),
                        .Controls = Calibration(shown)
                    }

                    If pictures Then
                        window.Picture = SavePicture(shown, folder, name)
                    End If

                    ' A window whose handler failed fails by name, and its
                    ' evidence is left out.
                    Dim fault As Exception = Nothing

                    If faults.TryRemove(name, fault) Then
                        Throw New InvalidOperationException(HandlerFailed(fault), fault)
                    End If

                    windows.Add(window)

                End Sub
            )

        ' And one whose handler failed after it was inspected, as it closed.
        For Each late As KeyValuePair(Of String, Exception) In faults.OrderBy(Function(entry) entry.Key, StringComparer.Ordinal)
            failures.Add(late.Key & ": " & HandlerFailed(late.Value))
        Next

        evidence.Windows = windows.OrderBy(Function(window) window.Name, StringComparer.Ordinal).ToList()
        evidence.Failures = failures

        Try
            Write(folder, evidence)
        Catch ex As Exception
            Return Report(SetupFailed, "The evidence could not be written to " & folder & ": " & ex.Message)
        End Try

        If failures.Count > 0 Then
            Return Report(WindowsFailed, failures.Count & " of " & ViewCatalog.Dialogs.Count & " windows failed, named in " & folder & ": " & String.Join(" | ", failures))
        End If

        Return 0

    End Function


    ' Each window is built, shown and inspected on a runner thread of its
    ' own (StaRunner), where Windows Forms catches an error in one of the
    ' window's handlers (paint, layout, Shown, a timer) and, with no
    ' handler of its own on that thread, shows its error dialog and waits.
    ' The factory runs on that thread before the window exists, so it first
    ' gives the thread a handler that records the error by the window's
    ' name. The thread ends with its window, and the handler with it.
    Private Function Guarded(name As String, build As Func(Of String, Form), faults As ConcurrentDictionary(Of String, Exception)) As Func(Of String, Form)

        Return Function(root)

                   AddHandler Application.ThreadException,
                       Sub(sender, e) faults.TryAdd(name, e.Exception)

                   Return build(root)

               End Function

    End Function


    ' The error and where it was thrown, for a window that fails by it.
    Private Function HandlerFailed(fault As Exception) As String

        Return "a handler failed: " & fault.ToString()

    End Function


    ' For calibration: every button, check box, option, single-line label
    ' and tab caption, walked as the measure walks them (each page of a tab
    ' control in turn), with its bounds in the window's client area and the
    ' size it would take. The main window's self-painted ones, which the
    ' measure leaves to a real display, are here too.
    Private Function Calibration(shown As Form) As List(Of ControlEvidence)

        Dim found As New List(Of ControlEvidence)()
        Collect(shown, shown, found)
        Return found

    End Function


    Private Sub Collect(shown As Form, parent As Control, found As List(Of ControlEvidence))

        Dim tabs As TabControl = TryCast(parent, TabControl)

        If tabs IsNot Nothing Then

            Dim selected As TabPage = tabs.SelectedTab

            For index As Integer = 0 To tabs.TabCount - 1
                tabs.SelectedIndex = index
                Application.DoEvents()
                Collect(shown, tabs.TabPages(index), found)
            Next

            tabs.SelectedTab = selected
            Application.DoEvents()
            Return

        End If

        For Each child As Control In parent.Controls

            If Not child.Visible OrElse child.Width <= 0 OrElse child.Height <= 0 Then
                Continue For
            End If

            Record(shown, parent, child, found)
            Collect(shown, child, found)

        Next

    End Sub


    ' A tab's caption is measured in the font it is drawn in, bold for the
    ' selected tab of an underline tab control, as the measure measures it.
    Private Sub Record(shown As Form, parent As Control, control As Control, found As List(Of ControlEvidence))

        Dim tabs As TabControl = TryCast(control, TabControl)

        If tabs IsNot Nothing Then

            For index As Integer = 0 To tabs.TabCount - 1

                Dim caption As String = tabs.TabPages(index).Text
                Dim bold As Boolean = TypeOf tabs Is UnderlineTabControl AndAlso index = tabs.SelectedIndex

                Using font As New Font(tabs.Font, If(bold, FontStyle.Bold, tabs.Font.Style))
                    found.Add(
                        New ControlEvidence(
                            "tab",
                            caption,
                            shown.RectangleToClient(tabs.RectangleToScreen(tabs.GetTabRect(index))),
                            TextRenderer.MeasureText(caption, font, New Size(Integer.MaxValue, Integer.MaxValue), CaptionFlags)
                        )
                    )
                End Using

            Next

            Return

        End If

        If String.IsNullOrWhiteSpace(control.Text) Then
            Return
        End If

        Dim kind As String =
            If(TypeOf control Is CheckBox, "check box",
            If(TypeOf control Is RadioButton, "option",
            If(TypeOf control Is ButtonBase, "button",
            If(TypeOf control Is Label AndAlso SingleLine(DirectCast(control, Label)), "label", Nothing))))

        If kind Is Nothing Then
            Return
        End If

        found.Add(
            New ControlEvidence(
                kind,
                control.Text,
                shown.RectangleToClient(parent.RectangleToScreen(control.Bounds)),
                control.GetPreferredSize(Size.Empty)
            )
        )

    End Sub


    ' A label on one line, as the measure judges one: no line break, and
    ' less than two lines tall.
    Private Function SingleLine(label As Label) As Boolean

        Dim lineHeight As Integer =
            TextRenderer.MeasureText("Xg", label.Font, New Size(Integer.MaxValue, Integer.MaxValue), CaptionFlags).Height

        Return Not label.Text.Contains(ChrW(10)) AndAlso label.Height < 2 * lineHeight

    End Function


    ' The window as Windows Forms draws it, at the size it was measured.
    Private Function SavePicture(shown As Form, folder As String, name As String) As String

        Dim pictureName As String = name & ".png"

        Using picture As New Bitmap(shown.Width, shown.Height)
            shown.DrawToBitmap(picture, New Rectangle(Point.Empty, shown.Size))
            picture.Save(Path.Combine(folder, pictureName), ImageFormat.Png)
        End Using

        Return pictureName

    End Function


    Private Sub Write(folder As String, evidence As Evidence)

        File.WriteAllText(Path.Combine(folder, JsonFile), JsonSerializer.Serialize(evidence, JsonOptions), New UTF8Encoding(False))
        File.WriteAllText(Path.Combine(folder, TableFile), TableOf(evidence), New UTF8Encoding(False))

    End Sub


    ' The sweep's table, with this display as its one column, named as the
    ' sweep names its columns: the working area, then the DPI.
    Private Function TableOf(evidence As Evidence) As String

        Dim machine As DisplayEnvironment = evidence.Environment
        Dim text As New StringBuilder()

        text.AppendLine("# Display evidence")
        text.AppendLine()
        text.AppendLine(
            "PaperRoute " & machine.AppVersion & " on " & machine.OSVersion & ", recorded " &
            evidence.RecordedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) & " UTC. " &
            "Primary screen " & machine.PrimaryScreenBounds.Width & "x" & machine.PrimaryScreenBounds.Height &
            ", working area " & machine.PrimaryScreenWorkingArea.Width & "x" & machine.PrimaryScreenWorkingArea.Height &
            ", " & machine.ScreenDpi & " DPI, HighDpiMode " & machine.HighDpiMode & "."
        )
        text.AppendLine()
        text.AppendLine("| Window | Scaling | " & evidence.Display & " |")
        text.AppendLine("|---|---|---|")

        For Each window As WindowEvidence In evidence.Windows
            text.AppendLine("| " & If(window.Name = "Form1", "Form1 (main window)", window.Name) & " | " & window.Scaling & " | " & window.Cell & " |")
        Next

        If evidence.Failures.Count > 0 Then

            text.AppendLine()
            text.AppendLine("Windows that failed:")
            text.AppendLine()

            For Each failure As String In evidence.Failures
                text.AppendLine("- " & failure.Replace(ControlChars.CrLf, " ").Replace(ControlChars.Lf, " "))
            Next

        End If

        Return text.ToString()

    End Function


    Private Function IsUnder(folder As String, parent As String) As Boolean

        Dim root As String =
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) & Path.DirectorySeparatorChar

        Return (Path.TrimEndingDirectorySeparator(folder) & Path.DirectorySeparatorChar).StartsWith(root, StringComparison.OrdinalIgnoreCase)

    End Function


    Private Function Report(exitCode As Integer, message As String) As Integer

        Console.Error.WriteLine("PaperRoute display evidence: " & message.Replace(ControlChars.CrLf, " ").Replace(ControlChars.Lf, " "))
        Return exitCode

    End Function

End Module


' The evidence file: the display, then each window by name.
Friend NotInheritable Class Evidence

    Public Property Tool As String = "PaperRoute.DisplayEvidence"
    Public Property Display As String
    Public Property RecordedAtUtc As DateTime
    Public Property Environment As DisplayEnvironment
    Public Property Windows As New List(Of WindowEvidence)()
    Public Property Failures As New List(Of String)()

End Class


Friend NotInheritable Class DisplayEnvironment

    Public Property OSVersion As String
    Public Property PrimaryScreenBounds As Box
    Public Property PrimaryScreenWorkingArea As Box
    Public Property ScreenDpi As Integer
    Public Property HighDpiMode As String
    Public Property AppVersion As String

End Class


' A window after it is fitted to the working area: what the sweep's cell
' records, its size and DPI, and the controls for calibration.
Friend NotInheritable Class WindowEvidence

    Public Property Name As String
    Public Property Scaling As String
    Public Property Size As Extent
    Public Property ClientSize As Extent
    Public Property DeviceDpi As Integer
    Public Property AcceptInside As Boolean?
    Public Property CancelInside As Boolean?
    Public Property Cut As List(Of String)
    Public Property Cell As String
    Public Property Picture As String
    Public Property Controls As List(Of ControlEvidence)

End Class


' Kind is the measure's word for the control: button, check box, option,
' label or tab.
Friend NotInheritable Class ControlEvidence

    Public Sub New(kind As String, text As String, bounds As Rectangle, preferred As Size)
        Me.Kind = kind
        Me.Text = text
        Me.Bounds = New Box(bounds)
        PreferredSize = New Extent(preferred)
    End Sub

    Public ReadOnly Property Kind As String
    Public ReadOnly Property Text As String
    Public ReadOnly Property Bounds As Box
    Public ReadOnly Property PreferredSize As Extent

End Class


Friend NotInheritable Class Box

    Public Sub New(rectangle As Rectangle)
        X = rectangle.X
        Y = rectangle.Y
        Width = rectangle.Width
        Height = rectangle.Height
    End Sub

    Public ReadOnly Property X As Integer
    Public ReadOnly Property Y As Integer
    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer

End Class


Friend NotInheritable Class Extent

    Public Sub New(size As Size)
        Width = size.Width
        Height = size.Height
    End Sub

    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer

End Class
