Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Micro-motion (#142): Windows' Animation effects setting read once until
' Windows says it changed, hover eased on one clock and pressed at once,
' colors that end on the theme's tokens, and nothing moving with motion
' off or under a contrast theme. Each test sets the seams it needs and
' puts them back.
<TestClass>
<DoNotParallelize>
Public Class MotionTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        ResetSeams()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    Private Shared Sub ResetSeams()
        UiMotion.EnabledOverride = Nothing
        UiMotion.FadeMilliseconds = UiMotion.DefaultFadeMilliseconds
        UiMotion.HoverMilliseconds = UiMotion.DefaultHoverMilliseconds
        UiTheme.HighContrastOverride = Nothing
    End Sub


    ' ---------------------------------------------------------------
    ' The setting
    ' ---------------------------------------------------------------

    ' Windows' setting is read once however often it is asked for, until
    ' Windows tells the window a setting changed (WM_SETTINGCHANGE), as it
    ' does when Animation effects is turned on or off: then it is read
    ' again, once, so the change counts in the session it is made in. It
    ' is what SPI_GETCLIENTAREAANIMATION says; a contrast theme turns
    ' motion off whatever the setting or the seam says.
    <TestMethod>
    Public Sub TheSettingIsReadOnceUntilWindowsSaysItChangedAndContrastWins()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiTheme.HighContrastOverride = False
                    UiMotion.EnabledOverride = Nothing

                    Dim ask As Func(Of Boolean()) =
                        Function()
                            Dim answers As New HashSet(Of Boolean)()
                            For index As Integer = 1 To 1000
                                answers.Add(UiMotion.Enabled)
                            Next
                            Return answers.ToArray()
                        End Function

                    ' Asked once first, so the setting has been read.
                    Dim asked As Boolean = UiMotion.Enabled
                    Dim reads As Integer = UiMotion.SettingReads
                    Dim first As Boolean() = ask()
                    If UiMotion.SettingReads <> reads Then report.Add("Windows' setting was read " & (UiMotion.SettingReads - reads).ToString() & " more times in 1000 asks.")
                    If Not first.SequenceEqual({SystemAnimates()}) Then report.Add("Motion is " & String.Join(", ", first) & ", not Windows' Animation effects setting.")

                    Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), 10), Path.Combine(_root, "settings"))
                        board.Start()
                        board.WaitForIdleWork()
                        reads = UiMotion.SettingReads
                        SendMessage(board.Handle, WM_SETTINGCHANGE, New IntPtr(SPI_SETCLIENTAREAANIMATION), IntPtr.Zero)
                        Dim again As Boolean() = ask()
                        If UiMotion.SettingReads <> reads + 1 Then report.Add("Once Windows said Animation effects changed, the setting was read " & (UiMotion.SettingReads - reads).ToString() & " times in 1000 asks, not once.")
                        If Not again.SequenceEqual({SystemAnimates()}) Then report.Add("Read again, motion is " & String.Join(", ", again) & ", not Windows' Animation effects setting.")
                        board.Close()
                    End Using

                    UiMotion.EnabledOverride = True
                    If Not UiMotion.Enabled Then report.Add("The seam does not turn motion on.")
                    UiMotion.EnabledOverride = False
                    If UiMotion.Enabled Then report.Add("The seam does not turn motion off.")

                    reads = UiMotion.SettingReads
                    UiTheme.HighContrastOverride = True
                    UiMotion.EnabledOverride = True
                    If UiMotion.Enabled Then report.Add("Under a contrast theme something moves.")
                    UiMotion.EnabledOverride = Nothing
                    If UiMotion.Enabled Then report.Add("Under a contrast theme something moves, with Windows' setting asked.")
                    If UiMotion.SettingReads <> reads Then report.Add("Under a contrast theme Windows' setting was read.")
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' ---------------------------------------------------------------
    ' Hover and press
    ' ---------------------------------------------------------------

    ' With motion off, the first paint after the pointer enters is the
    ' hover color, and no clock runs.
    <TestMethod>
    Public Sub WithMotionOffTheFirstPaintAfterMouseEnterIsTheHoverColour()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = False
                    UiTheme.HighContrastOverride = False

                    Using host As Form = NewHost(AllHovered())
                        For Each item In ItemsOf(host)
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Dim painted As Color = PixelOf(item.Control, item.Point)
                            If painted.ToArgb() <> item.Hover().ToArgb() Then
                                report.Add(item.Name & ": " & painted.ToString() & ", not the hover color " & item.Hover().ToString())
                            End If
                            If UiMotion.Animating Then report.Add(item.Name & ": a clock runs")
                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                            Dim rest As Color = PixelOf(item.Control, item.Point)
                            If rest.ToArgb() <> item.Rest().ToArgb() Then
                                report.Add(item.Name & ": " & rest.ToString() & " once left, not the rest color " & item.Rest().ToString())
                            End If
                        Next
                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' With motion on, the fill eases: the first paint that differs from
    ' rest lies between rest and hover, and the hover color is reached in
    ' about the tween's length. A press shows the pressed face at once,
    ' mid-ease too.
    <TestMethod>
    Public Sub WithMotionOnHoverEasesAndPressIsImmediate()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = False

                    Using host As Form = NewHost(AllHovered())
                        For Each item In ItemsOf(host)

                            Dim rest As Color = item.Rest()
                            Dim hover As Color = item.Hover()
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Dim watch As Stopwatch = Stopwatch.StartNew()

                            Dim first As Color = PumpUntil(item, Function(painted) painted.ToArgb() <> rest.ToArgb(), 500)
                            If Not Between(first, rest, hover) Then
                                report.Add(item.Name & ": the first paint after the pointer entered, " & first.ToString() & ", is not between " & rest.ToString() & " and " & hover.ToString())
                            End If

                            Dim reached As Color = PumpUntil(item, Function(painted) painted.ToArgb() = hover.ToArgb(), 1000)
                            Dim took As Double = watch.Elapsed.TotalMilliseconds
                            Console.WriteLine(item.Name & ": the hover color reached in " & took.ToString("0") & " ms")
                            If reached.ToArgb() <> hover.ToArgb() OrElse took > 200 Then
                                report.Add(item.Name & ": the hover color " & hover.ToString() & " was not reached by 200 ms (" & reached.ToString() & " at " & took.ToString("0") & " ms)")
                            End If

                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                            PumpUntil(item, Function(painted) painted.ToArgb() = rest.ToArgb(), 1000)

                        Next

                        ' A press, mid-ease and after it, is the pressed face there and then.
                        Dim primary As ActionButton = host.Controls(0).Controls.OfType(Of ActionButton)().First(Function(button) button.Role = ActionButtonRole.Primary)
                        Dim pressed As Color = ActionButton.FaceColors(ActionButtonRole.Primary, False, True, True).Fill
                        Raise(primary, "OnMouseEnter", EventArgs.Empty)
                        Raise(primary, "OnMouseDown", New MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0))
                        Dim atOnce As Color = PixelOf(primary, ButtonPoint(primary))
                        If atOnce.ToArgb() <> pressed.ToArgb() Then report.Add("A press mid-ease: " & atOnce.ToString() & ", not the pressed face " & pressed.ToString())
                        Raise(primary, "OnMouseUp", New MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0))
                        Dim released As Color = PixelOf(primary, ButtonPoint(primary))
                        Dim hovered As Color = ActionButton.FaceColors(ActionButtonRole.Primary, True, False, True).Fill
                        If released.ToArgb() <> hovered.ToArgb() Then report.Add("Let go: " & released.ToString() & ", not the full hover color " & hovered.ToString())
                        Raise(primary, "OnMouseLeave", EventArgs.Empty)

                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' A hover's steps repaint the control eased and nothing else: not its
    ' parent, not its neighbours.
    <TestMethod>
    Public Sub AHoverStepRepaintsOnlyTheControlItAnimates()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = False

                    Using host As Form = NewHost(AllHovered())
                        Dim flow As Control = host.Controls(0)
                        For Each item In ItemsOf(host)
                            Dim counts As Dictionary(Of Control, Integer) = CountPaints({host, flow}.Concat(flow.Controls.Cast(Of Control)()))
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Pump(250)
                            Dim own As Integer = counts(item.Control)
                            If own < 2 Then report.Add(item.Name & " painted " & own.ToString() & " times as it eased in")
                            For Each other In counts.Where(Function(pair) pair.Key IsNot item.Control AndAlso pair.Value > 0)
                                report.Add(item.Name & "'s hover repainted " & Describe(other.Key) & " " & other.Value.ToString() & " times")
                            Next
                            StopCounting(counts)
                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                            Pump(250)
                        Next
                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' A Board card highlights at once for the keyboard (its title focused)
    ' and lets go at once as the title loses it; a step of its border
    ' repaints the card and none of its parts; and the pointer crossing its
    ' parts while it is highlighted repaints nothing of it. Before #142 the
    ' card set its border again on each part's MouseEnter and MouseLeave,
    ' and repainted each time.
    <TestMethod>
    Public Sub ACardHighlightsAtOnceForTheKeyboardAndCrossingItsPartsRepaintsNothing()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = False

                    Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60), Path.Combine(_root, "settings"))
                        board.Start()
                        board.WaitForIdleWork()

                        Dim card As RoundedPanel = board.Shelf("pipelinePanel").Controls.OfType(Of RoundedPanel)().First()
                        Dim title As LinkLabel = card.Controls.OfType(Of LinkLabel)().First()
                        Dim search As TextBox = ViewCatalog.Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Search the board (Ctrl+F)")
                        Assert.AreEqual(UiTheme.CardBorder(), card.BorderColor, "A card at rest has the card border.")

                        ' The highlight follows Windows' focus on the title.
                        If Not title.Focus() OrElse Not title.Focused Then
                            board.Close()
                            Assert.Inconclusive("The test window was given no keyboard focus on this host.")
                        End If
                        If card.BorderColor <> UiTheme.AccentColor() Then report.Add("The title focused: the border is " & card.BorderColor.ToString() & " at once, not the accent")
                        If UiMotion.Animating Then report.Add("The title focused: a clock runs")
                        Pump(50)

                        ' A step of the border: the card repaints, its parts do not.
                        Dim counts As Dictionary(Of Control, Integer) = CountPaints({CType(card, Control)}.Concat(card.Controls.Cast(Of Control)()))
                        card.BorderColor = UiTheme.Blend(UiTheme.CardBorder(), UiTheme.AccentColor(), 0.5F)
                        Pump(50)
                        card.BorderColor = UiTheme.AccentColor()
                        Pump(50)
                        If counts(card) = 0 Then report.Add("A border step did not repaint the card")
                        For Each part In counts.Where(Function(pair) pair.Key IsNot card AndAlso pair.Value > 0)
                            report.Add("A border step repainted the card's " & Describe(part.Key))
                        Next
                        StopCounting(counts)

                        ' The pointer across each part, the card highlighted.
                        Dim crossed As Dictionary(Of Control, Integer) = CountPaints({CType(card, Control)}.Concat(card.Controls.Cast(Of Control)()))
                        For Each part As Control In card.Controls.Cast(Of Control)().ToList()
                            Raise(part, "OnMouseEnter", EventArgs.Empty)
                            Pump(20)
                            Raise(part, "OnMouseLeave", EventArgs.Empty)
                            Pump(20)
                        Next
                        Pump(200)
                        For Each part In crossed.Where(Function(pair) pair.Value > 0 AndAlso Not TypeOf pair.Key Is ActionButton)
                            report.Add("Crossing the card's parts repainted " & If(part.Key Is card, "the card", "its " & Describe(part.Key)) & " " & part.Value.ToString() & " times")
                        Next
                        StopCounting(crossed)
                        If card.BorderColor <> UiTheme.AccentColor() Then report.Add("Crossing the card's parts: the border is " & card.BorderColor.ToString())

                        search.Focus()
                        If card.BorderColor <> UiTheme.CardBorder() Then report.Add("The title left: the border is " & card.BorderColor.ToString() & " at once, not the card border")

                        board.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub,
            TimeSpan.FromMinutes(3))

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' Each hover ends on the theme's tokens, eased in and eased out, in
    ' Light and in Dark; the face part way there is FaceColors at both ends
    ' for every role and state.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub HoverEndsAreTheThemeTokens(mode As SystemColorMode)

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                If mode = SystemColorMode.Dark AndAlso (SystemInformation.HighContrast OrElse Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) Then
                    Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later, outside a Windows contrast theme.")
                End If

                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = False
                    Assert.AreEqual(mode = SystemColorMode.Dark, UiTheme.IsDark(), "The theme asked for.")

                    For Each role As ActionButtonRole In [Enum].GetValues(GetType(ActionButtonRole))
                        For Each enabled As Boolean In {True, False}
                            For Each pressed As Boolean In {True, False}
                                CheckFace(report, role, 0.0F, pressed, enabled, FaceColors(role, False, pressed, enabled))
                                CheckFace(report, role, 1.0F, pressed, enabled, FaceColors(role, Not pressed, pressed, enabled))
                            Next
                        Next
                    Next

                    Using host As Form = NewHost(AllHovered())
                        For Each item In ItemsOf(host)
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Dim hovered As Color = PumpUntil(item, Function(painted) painted.ToArgb() = item.Hover().ToArgb(), 1000)
                            If hovered.ToArgb() <> item.Hover().ToArgb() Then report.Add(mode.ToString() & ", " & item.Name & " hovered: " & hovered.ToString() & ", not " & item.Hover().ToString())
                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                            Dim rest As Color = PumpUntil(item, Function(painted) painted.ToArgb() = item.Rest().ToArgb(), 1000)
                            If rest.ToArgb() <> item.Rest().ToArgb() Then report.Add(mode.ToString() & ", " & item.Name & " left: " & rest.ToString() & ", not " & item.Rest().ToString())
                            ' The last steps can round to the rest color before the
                            ' ease's time is up; the clock stops once it is.
                            Dim settling As Stopwatch = Stopwatch.StartNew()
                            While UiMotion.Animating AndAlso settling.ElapsedMilliseconds < UiMotion.HoverMilliseconds * 2
                                Application.DoEvents()
                                Thread.Sleep(1)
                            End While
                            If UiMotion.Animating Then report.Add(mode.ToString() & ", " & item.Name & ": a clock still runs once the ease is done")
                        Next
                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub,
            mode:=mode)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' Under a contrast theme nothing eases, whatever the seam says: each
    ' state is there at once, and no clock runs; a chip shows no hover at
    ' all, its fill staying the window color.
    <TestMethod>
    Public Sub NothingAnimatesUnderAContrastTheme()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = True

                    Using host As Form = NewHost(AllHovered())
                        For Each item In ItemsOf(host)
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Dim painted As Color = PixelOf(item.Control, item.Point)
                            Dim expected As Color = If(TypeOf item.Control Is FilterChip, item.Rest(), item.Hover())
                            If painted.ToArgb() <> expected.ToArgb() Then report.Add(item.Name & ": " & painted.ToString() & " at once, not " & expected.ToString())
                            If UiMotion.Animating Then report.Add(item.Name & ": a clock runs")
                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                        Next
                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' With motion off, no hover, leave or press starts the clock.
    <TestMethod>
    Public Sub WithMotionOffNoClockRuns()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = False
                    UiTheme.HighContrastOverride = False

                    Using host As Form = NewHost(AllHovered())
                        For Each item In ItemsOf(host)
                            Raise(item.Control, "OnMouseEnter", EventArgs.Empty)
                            Raise(item.Control, "OnMouseDown", New MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0))
                            Raise(item.Control, "OnMouseUp", New MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0))
                            Raise(item.Control, "OnMouseLeave", EventArgs.Empty)
                            Pump(20)
                            If UiMotion.Animating Then report.Add(item.Name & ": a clock runs")
                        Next
                        host.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    ' A control that eases, where its fill is read, and its fill at rest
    ' and hovered from the theme as it is when asked.
    Private NotInheritable Class HoveredItem
        Public Name As String
        Public Control As Control
        Public Point As Point
        Public Rest As Func(Of Color)
        Public Hover As Func(Of Color)
    End Class


    Private Shared Function AllHovered() As Control()

        Dim controls As New List(Of Control)()

        For Each role As ActionButtonRole In [Enum].GetValues(GetType(ActionButtonRole))
            controls.Add(New ActionButton With {.Text = role.ToString() & " button", .Role = role, .Width = 140, .Height = 32})
        Next

        For Each tone As FilterChipTone In [Enum].GetValues(GetType(FilterChipTone))
            controls.Add(New FilterChip With {.Text = tone.ToString() & " chip", .Tone = tone})
        Next

        controls.Add(New RailButton(RailGlyph.Library, "Library") With {.Width = 188, .Height = 34})
        controls.Add(New RailCommandButton(RailGlyph.Help, "Help") With {.Width = 188, .Height = 34})

        Return controls.ToArray()

    End Function


    Private Shared Function ItemsOf(host As Form) As List(Of HoveredItem)

        Dim flow As Control = host.Controls(0)
        Dim behind As Func(Of Color) = Function() flow.BackColor
        Dim items As New List(Of HoveredItem)()

        For Each control As Control In flow.Controls

            Dim button As ActionButton = TryCast(control, ActionButton)
            Dim chip As FilterChip = TryCast(control, FilterChip)

            If button IsNot Nothing Then
                Dim role As ActionButtonRole = button.Role
                items.Add(New HoveredItem With {
                    .Name = "A " & role.ToString() & " ActionButton",
                    .Control = button,
                    .Point = ButtonPoint(button),
                    .Rest = Function() OrBehind(FaceColors(role, False, False, True).Fill, behind()),
                    .Hover = Function() OrBehind(FaceColors(role, True, False, True).Fill, behind())
                })
            ElseIf chip IsNot Nothing Then
                Dim tone As FilterChipTone = chip.Tone
                items.Add(New HoveredItem With {
                    .Name = "A " & tone.ToString() & " FilterChip",
                    .Control = chip,
                    .Point = New Point(chip.Padding.Left \ 2, chip.Height \ 2),
                    .Rest = Function() ChipFill(tone),
                    .Hover = Function() FilterChip.HoverFill(tone, ChipFill(tone))
                })
            Else
                items.Add(New HoveredItem With {
                    .Name = "A " & control.GetType().Name,
                    .Control = control,
                    .Point = New Point(control.Width - 6, control.Height \ 2),
                    .Rest = behind,
                    .Hover = Function() UiTheme.HoverBackground()
                })
            End If

        Next

        Return items

    End Function


    ' Inside a button's fill, above its text and clear of its outline.
    Private Shared Function ButtonPoint(button As Control) As Point
        Return New Point(button.Width \ 2, 4)
    End Function


    Private Shared Function ChipFill(tone As FilterChipTone) As Color
        Select Case tone
            Case FilterChipTone.Warning
                Return UiTheme.WarningMutedBackground()
            Case FilterChipTone.Danger
                Return UiTheme.DangerMutedBackground()
            Case FilterChipTone.Active
                Return UiTheme.AccentMutedBackground()
            Case Else
                Return UiTheme.CardBackground()
        End Select
    End Function


    Private Shared Function FaceColors(role As ActionButtonRole, hover As Boolean, pressed As Boolean, enabled As Boolean) As (Fill As Color, Border As Color, Ink As Color)
        Return ActionButton.FaceColors(role, hover, pressed, enabled)
    End Function


    Private Shared Sub CheckFace(report As List(Of String), role As ActionButtonRole, hover As Single, pressed As Boolean, enabled As Boolean, expected As (Fill As Color, Border As Color, Ink As Color))
        Dim behind As Color = UiTheme.BoardBackground()
        Dim face As (Fill As Color, Border As Color, Ink As Color) = ActionButton.FaceColorsAt(role, hover, pressed, enabled, behind)
        If face.Fill <> expected.Fill OrElse face.Border <> expected.Border OrElse face.Ink <> expected.Ink Then
            report.Add($"FaceColorsAt({role}, {hover}, pressed {pressed}, enabled {enabled}) is {face}, not {expected}")
        End If
    End Sub


    Private Shared Function OrBehind(fill As Color, behind As Color) As Color
        Return If(fill.IsEmpty, behind, fill)
    End Function


    ' Whether each channel of a color lies between two others, and the
    ' color is neither.
    Private Shared Function Between(value As Color, first As Color, last As Color) As Boolean
        Dim within As Func(Of Integer, Integer, Integer, Boolean) =
            Function(channel, one, other) channel >= Math.Min(one, other) AndAlso channel <= Math.Max(one, other)
        Return value.ToArgb() <> first.ToArgb() AndAlso value.ToArgb() <> last.ToArgb() AndAlso
            within(value.R, first.R, last.R) AndAlso within(value.G, first.G, last.G) AndAlso within(value.B, first.B, last.B)
    End Function


    ' A host window, offscreen, its controls in a row on the board color.
    Private Shared Function NewHost(controls As Control()) As Form
        Dim host As New Form With {.Size = New Size(1200, 240), .BackColor = UiTheme.BoardBackground()}
        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = UiTheme.BoardBackground()}
        flow.Controls.AddRange(controls)
        host.Controls.Add(flow)
        ViewCatalog.ShowOffscreen(host)
        Pump(50)
        Return host
    End Function


    Private Shared Function PixelOf(control As Control, point As Point) As Color
        Using picture As New Bitmap(control.Width, control.Height)
            control.DrawToBitmap(picture, New Rectangle(Drawing.Point.Empty, control.Size))
            Return picture.GetPixel(point.X, point.Y)
        End Using
    End Function


    ' Processes messages until what the item paints meets the condition,
    ' or the time is up; returns the last color read.
    Private Shared Function PumpUntil(item As HoveredItem, condition As Func(Of Color, Boolean), milliseconds As Integer) As Color
        Dim watch As Stopwatch = Stopwatch.StartNew()
        Dim painted As Color = PixelOf(item.Control, item.Point)
        While Not condition(painted) AndAlso watch.ElapsedMilliseconds < milliseconds
            Application.DoEvents()
            Thread.Sleep(1)
            painted = PixelOf(item.Control, item.Point)
        End While
        Return painted
    End Function


    Private Shared Sub Pump(milliseconds As Integer)
        Dim watch As Stopwatch = Stopwatch.StartNew()
        Do
            Application.DoEvents()
            Thread.Sleep(1)
        Loop While watch.ElapsedMilliseconds < milliseconds
    End Sub


    ' Counts the paints each control's window is given from now until
    ' StopCounting: the WM_PAINT messages the thread takes from its queue
    ' for it (a thread-local WH_GETMESSAGE hook), which a control that
    ' paints itself without raising Paint is given as any other.
    Private Shared ReadOnly Watches As New Dictionary(Of Dictionary(Of Control, Integer), PaintDispatchWatch)()

    Private Shared Function CountPaints(controls As IEnumerable(Of Control)) As Dictionary(Of Control, Integer)
        Dim counts As New Dictionary(Of Control, Integer)()
        For Each control As Control In controls
            counts(control) = 0
        Next
        Watches(counts) = New PaintDispatchWatch(counts)
        Return counts
    End Function


    ' What CountPaints counted no longer changes once read.
    Private Shared Sub StopCounting(counts As Dictionary(Of Control, Integer))
        Dim watch As PaintDispatchWatch = Nothing
        If Watches.TryGetValue(counts, watch) Then
            watch.Dispose()
            Watches.Remove(counts)
        End If
    End Sub


    Private NotInheritable Class PaintDispatchWatch
        Implements IDisposable

        Private ReadOnly _callback As HookProc
        Private ReadOnly _hook As IntPtr
        Private ReadOnly _counts As Dictionary(Of Control, Integer)
        Private ReadOnly _byHandle As New Dictionary(Of IntPtr, Control)()

        Private Delegate Function HookProc(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

        Private Const WH_GETMESSAGE As Integer = 3
        Private Const PM_REMOVE As Integer = 1
        Private Const WM_PAINT As Integer = &HF

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeMessage
            Public Handle As IntPtr
            Public Message As Integer
            Public WParam As IntPtr
            Public LParam As IntPtr
            Public Time As Integer
            Public X As Integer
            Public Y As Integer
        End Structure

        Public Sub New(counts As Dictionary(Of Control, Integer))
            _counts = counts
            For Each control As Control In counts.Keys
                If control.IsHandleCreated Then _byHandle(control.Handle) = control
            Next
            _callback = AddressOf Hook
            _hook = SetWindowsHookEx(WH_GETMESSAGE, _callback, IntPtr.Zero, GetCurrentThreadId())
        End Sub

        Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
            If code >= 0 AndAlso wParam.ToInt64() = PM_REMOVE Then
                Dim message As NativeMessage = Marshal.PtrToStructure(Of NativeMessage)(lParam)
                Dim control As Control = Nothing
                If message.Message = WM_PAINT AndAlso _byHandle.TryGetValue(message.Handle, control) Then
                    _counts(control) += 1
                End If
            End If
            Return CallNextHookEx(_hook, code, wParam, lParam)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            UnhookWindowsHookEx(_hook)
        End Sub

        <DllImport("user32.dll")>
        Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As HookProc, hMod As IntPtr, threadId As Integer) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThreadId() As Integer
        End Function

    End Class


    Private Shared Function Describe(control As Control) As String
        Return control.GetType().Name & If(String.IsNullOrEmpty(control.AccessibleName), String.Empty, " '" & control.AccessibleName & "'")
    End Function


    ' Raises a control's own handler for a mouse event, as Windows' message
    ' would, without moving the pointer.
    Private Shared Sub Raise(control As Control, name As String, args As EventArgs)
        GetType(Control).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, {args.GetType()}, Nothing).Invoke(control, {args})
    End Sub


    ' What Windows' Animation effects setting says, read here directly.
    Private Shared Function SystemAnimates() As Boolean
        Dim animate As Integer = 1
        If Not SystemParametersInfo(&H1042UI, 0, animate, 0) Then Return True
        Return animate <> 0
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SystemParametersInfo(action As UInteger, param As UInteger, ByRef value As Integer, winIni As UInteger) As Boolean
    End Function

    Private Const WM_SETTINGCHANGE As UInteger = &H1A
    Private Const SPI_SETCLIENTAREAANIMATION As Integer = &H1043

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

End Class
