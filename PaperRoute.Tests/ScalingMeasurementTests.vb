Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Services

' Every top-level window of the catalog, measured at 96, 120 and 144 DPI
' after fitting to a 1920x1003 and then to a 1366x728 working area (#93,
' step 2): whether the accept and cancel buttons are inside the client
' area, and whether any button, check box, single-line label or tab
' caption is cut. The per-window table goes to the test output, as GitHub
' markdown, for #93.
'
' The host runs at 96 DPI, so a display is simulated. Every font grows by
' the DPI's factor, as text does on a real display, and a window whose
' source sets the 96-DPI base dimensions, or uses DialogScaling, is scaled
' by the same factor, as Windows Forms scales it when it opens. What the
' test cannot see: a value the code scales by hand from DeviceDpi or
' LogicalToDeviceUnits reads 96 here, so a hand-scaled window shows its
' text grown but its hand-scaled sizes not, and a converted window with a
' hand-scaled value left on a property Windows Forms scales shows no
' double scaling. Both are proven only at a real 150% display, against
' which this table is calibrated once.
'
' Report-only for the windows not yet converted: the table is the proof.
' Enforcing for the windows in Converted, which must pass at 120 and 144
' DPI on both screens.
<TestClass>
<DoNotParallelize>
Public Class ScalingMeasurementTests

    Public Property TestContext As TestContext

    Private Shared ReadOnly Dpis As Integer() = {96, 120, 144}

    ' The probe's own screens, small enough that a 1024x768 build runner
    ' holds the probe at their size, so the probe keeps its teeth there: a
    ' roomy one that holds it whole at 96 DPI and a short one that cuts its
    ' lowest rows.
    Private Shared ReadOnly ProbeScreens As (Name As String, Area As Rectangle)() = {
        ("roomy", New Rectangle(-30000, -30000, 1000, 740)),
        ("short", New Rectangle(-30000, -30000, 1000, 400))
    }

    ' The larger screen first: fitting shrinks only, so one window serves
    ' both. Off screen, so a window the fit centers stays off screen too.
    Private Shared ReadOnly Screens As (Name As String, Area As Rectangle)() = {
        ("1920x1003", New Rectangle(-30000, -30000, 1920, 1003)),
        ("1366x728", New Rectangle(-30000, -30000, 1366, 728))
    }

    ' The windows converted to the one scaling rule, DialogScaling (#93,
    ' step 3); each conversion adds its window. Batch one: the four that
    ' had the base dimensions but not the rest of the rule. Batch two: the
    ' top-level windows the first table showed cut or with a button
    ' outside the client area.
    Friend Shared ReadOnly Converted As Type() = {
        GetType(AboutForm), GetType(JournalCandidateForm), GetType(UpdateProgressForm), GetType(UpdatePromptForm),
        GetType(DeadlineDateForm), GetType(DiagnosticsForm), GetType(ExcelMappingForm), GetType(JournalChecklistItemEditForm),
        GetType(ManuscriptLinksForm), GetType(ManuscriptVersionEditForm), GetType(PublicationCheckForm), GetType(StartupFailureForm),
        GetType(SubmissionPacketEditForm), GetType(UnexpectedErrorForm)
    }


    <TestMethod>
    Public Sub EveryTopLevelWindowIsMeasuredAt96To144Dpi()

        Dim rows As New SortedDictionary(Of String, WindowRow)(StringComparer.Ordinal)
        Dim failures As New List(Of String)()

        For Each dpi As Integer In Dpis

            Dim current As Integer = dpi

            failures.AddRange(
                ViewCatalog.ShowEach(
                    ViewCatalog.Dialogs.Select(Function(entry) (entry.Key.Name, entry.Value)),
                    Sub(name, shown)

                        Dim row As WindowRow = Nothing

                        If Not rows.TryGetValue(name, row) Then
                            row = New WindowRow(name, ScalingOf(ViewCatalog.Dialogs.Keys.Single(Function(view) view.Name = name)))
                            rows.Add(name, row)
                        End If

                        For Each cell As (Screen As String, Dpi As Integer, Result As Cell) In Measure(shown, current, row.ScalesWithWinForms)
                            row.Cells(cell.Screen & " " & cell.Dpi) = cell.Result
                        Next

                    End Sub
                )
            )

        Next

        Dim table As String = TableOf(rows.Values)

        TestContext.WriteLine(table)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(ViewCatalog.Dialogs.Count, rows.Count, "Every top-level window was measured.")

        ' The converted windows must pass at 120 and 144 on both screens.
        Dim convertedNames As New HashSet(Of String)(Converted.Select(Function(view) view.Name))

        Dim enforced As List(Of String) =
            rows.Values.
                Where(Function(row) convertedNames.Contains(row.Name)).
                SelectMany(Function(row) row.Cells.Where(Function(cell) Not cell.Key.EndsWith(" 96", StringComparison.Ordinal) AndAlso Not cell.Value.Passes).
                                                   Select(Function(cell) row.Name & " at " & cell.Key & ": " & cell.Value.Describe())).
                ToList()

        Assert.AreEqual(0, enforced.Count, Environment.NewLine & String.Join(Environment.NewLine, enforced))

    End Sub


    ' The measure has teeth. A probe window that is not scaled keeps its
    ' 96-DPI sizes while its text grows: at 144 DPI its fixed-width button's
    ' caption is cut, and its accept button, which does not move when the
    ' window is fitted to a short screen, is outside the client area. The
    ' same window scaled as Windows Forms scales it has room for the
    ' caption. At 96 DPI on the roomy screen the probe passes. The probe
    ' and its screens fit inside what a 1024x768 build runner can hold, so
    ' none of its cells is left unmeasured there.
    <TestMethod>
    Public Sub ACutCaptionAndAnOffScreenButtonAreReported()

        For Each scaled As Boolean In {False, True}

            Dim isScaled As Boolean = scaled
            Dim results As New Dictionary(Of String, Cell)()

            ViewCatalog.ShowAndClose(
                "The probe",
                Function() Probe(),
                Sub(name, shown)
                    For Each cell As (Screen As String, Dpi As Integer, Result As Cell) In Measure(shown, 144, isScaled, ProbeScreens)
                        results(cell.Screen) = cell.Result
                    Next
                End Sub
            )

            Dim small As Cell = results("short")

            Assert.IsNull(small.NotMeasuredHere, "The probe is measured on this computer: " & small.Describe())
            Dim captionCut As Boolean = small.Cut.Any(Function(cut) cut.Contains("Look Up Journal Facts", StringComparison.Ordinal))

            Assert.IsTrue(small.AcceptInside.HasValue AndAlso Not small.AcceptInside.Value, "The accept button left below the fitted client area is reported outside it: " & small.Describe())

            If isScaled Then
                Assert.IsFalse(captionCut, "Scaled as Windows Forms scales it, the button has room for its caption: " & small.Describe())
            Else
                Assert.IsTrue(captionCut, "Unscaled, the fixed-width button's caption is reported cut: " & small.Describe())
            End If

        Next

        Dim unscaled As New Dictionary(Of String, Cell)()

        ViewCatalog.ShowAndClose(
            "The probe at 96",
            Function() Probe(),
            Sub(name, shown)
                For Each cell As (Screen As String, Dpi As Integer, Result As Cell) In Measure(shown, 96, False, ProbeScreens)
                    unscaled(cell.Screen) = cell.Result
                Next
            End Sub
        )

        Assert.IsNull(unscaled("roomy").NotMeasuredHere, "The probe is measured on this computer: " & unscaled("roomy").Describe())
        Assert.IsTrue(unscaled("roomy").Passes, "At 96 DPI the probe passes: " & unscaled("roomy").Describe())

    End Sub


    ' A window larger than this computer's screen can hold is held at the
    ' screen's largest tracking size, so a cell whose area is larger still
    ' says it was not measured here instead of reporting the cuts the hold
    ' makes, and is not failed. On any computer: the window asks for more
    ' than the screen can track.
    <TestMethod>
    Public Sub AWindowTheScreenCannotHoldIsNotMeasured()

        Dim track As Size =
            SystemInformation.MaxWindowTrackSize

        Dim beyond As (Name As String, Area As Rectangle)() = {
            ("beyond", New Rectangle(-30000, -30000, track.Width + 400, track.Height + 400))
        }

        Dim result As Cell = Nothing

        ViewCatalog.ShowAndClose(
            "A window larger than the screen",
            Function()
                Dim form As Form = Probe()
                form.Size = New Size(track.Width + 200, track.Height + 200)
                Return form
            End Function,
            Sub(name, shown)
                result = Measure(shown, 96, False, beyond).Single().Result
            End Sub
        )

        StringAssert.StartsWith(result.NotMeasuredHere, "not measured: this screen holds a window to ", "The cell is not measured: " & result.Describe())
        Assert.IsTrue(result.Passes, "A cell not measured here is not failed.")
        Assert.AreEqual(result.NotMeasuredHere, result.Describe(), "The table says why.")

    End Sub

    ' A 600x500 window with a 160-pixel button whose caption fits at 96
    ' DPI, and OK and Cancel at the bottom, not anchored: fitted to the
    ' 400-pixel screen the window loses its lowest rows and the two stay
    ' where they were, below the client area; at 144 DPI, unscaled, the
    ' text has grown 1.5 times.
    Private Shared Function Probe() As Form

        Dim form As New Form With {.ClientSize = New Size(600, 500), .Font = New Font("Segoe UI", 10.0F), .AutoScaleMode = AutoScaleMode.Dpi}
        Dim lookup As New Button With {.Text = "Look Up Journal Facts", .Location = New Point(20, 20), .Size = New Size(160, 30)}
        Dim ok As New Button With {.Text = "OK", .Location = New Point(400, 460), .Size = New Size(90, 30)}
        Dim cancel As New Button With {.Text = "Cancel", .Location = New Point(500, 460), .Size = New Size(90, 30)}
        form.Controls.AddRange({lookup, ok, cancel})
        form.AcceptButton = ok
        form.CancelButton = cancel
        Return form

    End Function


    ' The measure counts only what scrolling reaches. A panel that scrolls
    ' leaves a child docked to fill it out of its scroll range, so a button
    ' near that child's foot, below the panel's client area, is reported
    ' clipped. Docked to the top, the same child is in the scroll range and
    ' its button is not reported.
    <TestMethod>
    Public Sub ContentPastAScrollRangeIsReported()

        For Each dock As DockStyle In {DockStyle.Fill, DockStyle.Top}

            Dim docked As DockStyle = dock
            Dim result As Cell = Nothing

            ViewCatalog.ShowAndClose(
                "The scrolling probe",
                Function() ScrollingProbe(docked),
                Sub(name, shown)
                    result = Measure(shown, 96, False).Single(Function(cell) cell.Screen = "1366x728").Result
                End Sub
            )

            Dim reported As Boolean = result.Cut.Any(Function(cut) cut.Contains("Reach Me", StringComparison.Ordinal))

            If docked = DockStyle.Fill Then
                Assert.IsTrue(reported, "Past the scroll range of the panel it fills, the button is reported: " & result.Describe())
            Else
                Assert.IsFalse(reported, "Inside the scroll range, the button is reached: " & result.Describe())
            End If

        Next

    End Sub


    ' A 600x500 window whose body scrolls, holding a 900-pixel panel with a
    ' button near its foot.
    Private Shared Function ScrollingProbe(dock As DockStyle) As Form

        Dim form As New Form With {.ClientSize = New Size(600, 500), .Font = New Font("Segoe UI", 10.0F), .AutoScaleMode = AutoScaleMode.Dpi}
        Dim body As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
        Dim content As New Panel With {.Dock = dock, .Height = 900, .MinimumSize = New Size(0, 900)}
        Dim reach As New Button With {.Text = "Reach Me", .Location = New Point(20, 850), .Size = New Size(120, 30)}
        content.Controls.Add(reach)
        body.Controls.Add(content)
        form.Controls.Add(body)
        Return form

    End Function


    ' ---------------------------------------------------------------
    ' Simulating the display
    ' ---------------------------------------------------------------

    ' Grows the window's text for the DPI, scales the window as Windows
    ' Forms would if its source asks for that, then fits it to each screen
    ' and inspects it.
    Friend Shared Function Measure(
        shown As Form,
        dpi As Integer,
        scalesWithWinForms As Boolean,
        Optional areas As (Name As String, Area As Rectangle)() = Nothing
    ) As List(Of (Screen As String, Dpi As Integer, Result As Cell))

        Simulate(shown, dpi, scalesWithWinForms)

        Dim results As New List(Of (Screen As String, Dpi As Integer, Result As Cell))()

        ' Windows Forms holds a window to the largest size this computer's
        ' screen can track. On a screen smaller than a cell's area, as on a
        ' 1024x768 build runner, a window held there is not the window the
        ' cell asks about, so the cell says it was not measured here rather
        ' than report the cuts the hold makes.
        Dim track As Size =
            SystemInformation.MaxWindowTrackSize

        For Each screen As (Name As String, Area As Rectangle) In If(areas, Screens)

            ResponsiveDialogSizingService.FitToWorkingArea(shown, screen.Area)
            shown.PerformLayout()
            Application.DoEvents()

            Dim cell As Cell =
                Inspect(shown)

            If (shown.Width >= track.Width AndAlso screen.Area.Width > track.Width) OrElse
               (shown.Height >= track.Height AndAlso screen.Area.Height > track.Height) Then
                cell.NotMeasuredHere = "not measured: this screen holds a window to " & track.Width & "x" & track.Height
            End If

            results.Add((screen.Name, dpi, cell))

        Next

        Return results

    End Function


    ' The window as it opened at this computer's own display, with nothing
    ' simulated: fitted to the working area of the screen it opens on, as
    ' the app fits it, and inspected as a cell of the table is. For the
    ' display evidence tool, whose run at a real 150% display calibrates
    ' this table (#93, step 2).
    Friend Shared Function MeasureAtThisDisplay(shown As Form) As Cell

        ResponsiveDialogSizingService.FitToWorkingArea(shown)
        shown.PerformLayout()
        Application.DoEvents()

        Return Inspect(shown)

    End Function


    ' Grows the window's text for the DPI and scales the window as Windows
    ' Forms would if its source asks for that. Nothing changes at 96 DPI.
    Friend Shared Sub Simulate(
        shown As Form,
        dpi As Integer,
        scalesWithWinForms As Boolean
    )

        Dim factor As Single = dpi / 96.0F

        If dpi <> 96 Then

            ' In the order Windows Forms keeps when a window opens: the sizes
            ' written at 96 DPI are scaled first, and the text is laid out at
            ' its size for the display after, so a control that grows to fit
            ' its text grows from its scaled size, not from a grown one.
            If scalesWithWinForms Then
                shown.Scale(New SizeF(factor, factor))
            End If

            Dim fonts As New Dictionary(Of Control, Font)()

            For Each control As Control In New Control() {shown}.Concat(ViewCatalog.Descendants(shown))
                fonts(control) = control.Font
            Next

            For Each pair As KeyValuePair(Of Control, Font) In fonts
                pair.Key.Font = New Font(pair.Value.FontFamily, pair.Value.Size * factor, pair.Value.Style, pair.Value.Unit)
            Next

            ' Scale defers the layouts it causes and drops them. A window that
            ' opens scaled is laid out by the size messages its new windows
            ' get; these windows exist, so each control is laid out by hand,
            ' parents first, and once more for what the first pass moved.
            For pass As Integer = 1 To 2
                For Each control As Control In New Control() {shown}.Concat(ViewCatalog.Descendants(shown))
                    control.PerformLayout()
                Next
            Next

            Application.DoEvents()

        End If

    End Sub


    ' ---------------------------------------------------------------
    ' Inspecting a window
    ' ---------------------------------------------------------------

    Friend NotInheritable Class Cell

        Public AcceptInside As Boolean?
        Public CancelInside As Boolean?
        Public ReadOnly Cut As New List(Of String)()

        ' Set when this computer's screen could not hold the window at the
        ' cell's size (see Measure): nothing here is judged.
        Public NotMeasuredHere As String

        Public ReadOnly Property Passes As Boolean
            Get
                Return NotMeasuredHere IsNot Nothing OrElse
                       (Not Outside(AcceptInside) AndAlso Not Outside(CancelInside) AndAlso Cut.Count = 0)
            End Get
        End Property

        ' A window without the button, or with it hidden, is not failed.
        Private Shared Function Outside(inside As Boolean?) As Boolean
            Return inside.HasValue AndAlso Not inside.Value
        End Function

        Public Function Describe() As String

            If NotMeasuredHere IsNot Nothing Then
                Return NotMeasuredHere
            End If

            If Passes Then
                Return "ok"
            End If

            Dim parts As New List(Of String)()
            If Outside(AcceptInside) Then parts.Add("accept button outside")
            If Outside(CancelInside) Then parts.Add("cancel button outside")

            If Cut.Count > 0 Then
                Dim named As String = String.Join("; ", Cut.Take(3))
                parts.Add(Cut.Count & " cut: " & named & If(Cut.Count > 3, " … +" & (Cut.Count - 3), String.Empty))
            End If

            Return String.Join("; ", parts)

        End Function

    End Class


    Private Shared Function Inspect(shown As Form) As Cell

        Dim cell As New Cell()
        Dim client As Rectangle = shown.RectangleToScreen(shown.ClientRectangle)

        cell.AcceptInside = Inside(TryCast(shown.AcceptButton, Control), client)
        cell.CancelInside = Inside(TryCast(shown.CancelButton, Control), client)

        InspectChildren(shown, client, cell.Cut)

        Return cell

    End Function


    ' Nothing for a window without the button, or one that is hidden.
    Private Shared Function Inside(button As Control, client As Rectangle) As Boolean?

        If button Is Nothing OrElse Not button.Visible OrElse button.Parent Is Nothing Then
            Return Nothing
        End If

        Return client.Contains(button.Parent.RectangleToScreen(button.Bounds))

    End Function


    ' Every visible child, clipped by its parents down to the window's
    ' client area, except inside a container that scrolls, whose content
    ' is reached by scrolling and so is measured against its scroll range:
    ' what lies past that range cannot be reached. Each page of a tab
    ' control is shown in turn so its content is measured too.
    Private Shared Sub InspectChildren(parent As Control, clip As Rectangle, cut As List(Of String))

        Dim tabs As TabControl = TryCast(parent, TabControl)

        If tabs IsNot Nothing Then

            Dim selected As TabPage = tabs.SelectedTab

            For index As Integer = 0 To tabs.TabCount - 1
                tabs.SelectedIndex = index
                Application.DoEvents()
                Dim page As TabPage = tabs.TabPages(index)
                InspectChildren(page, Rectangle.Intersect(clip, page.RectangleToScreen(page.ClientRectangle)), cut)
            Next

            tabs.SelectedTab = selected
            Application.DoEvents()
            Return

        End If

        For Each child As Control In parent.Controls

            If Not child.Visible OrElse child.Width <= 0 OrElse child.Height <= 0 Then
                Continue For
            End If

            Dim bounds As Rectangle = parent.RectangleToScreen(child.Bounds)

            InspectControl(child, bounds, clip, cut)

            Dim scrolls As ScrollableControl = TryCast(child, ScrollableControl)
            Dim childClip As Rectangle =
                If(scrolls IsNot Nothing AndAlso scrolls.AutoScroll,
                   ScrollRange(scrolls),
                   Rectangle.Intersect(clip, child.RectangleToScreen(child.ClientRectangle)))

            InspectChildren(child, childClip, cut)

        Next

    End Sub


    ' What a container that scrolls can bring into view, on screen: its
    ' display rectangle, which is its scroll range less its padding (the
    ' padding is reached too), and never less than its client area. A
    ' panel leaves a child docked to fill it out of that range, so the
    ' part of such a child below the client area cannot be reached.
    Private Shared Function ScrollRange(scrolls As ScrollableControl) As Rectangle

        Dim display As Rectangle = scrolls.DisplayRectangle
        Dim padding As Padding = scrolls.Padding

        Dim range As New Rectangle(
            display.X - padding.Left,
            display.Y - padding.Top,
            display.Width + padding.Horizontal,
            display.Height + padding.Vertical
        )

        Return scrolls.RectangleToScreen(Rectangle.Union(range, scrolls.ClientRectangle))

    End Function


    Private Const CaptionFlags As TextFormatFlags =
        TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.HidePrefix


    ' The main window's controls that paint their own captions by rules of
    ' their own (the rail's items, the shelf tabs, chips and pills): the
    ' real display judges them.
    Private Shared ReadOnly SelfPainted As Type() = {
        GetType(RailButton), GetType(RailCommandButton), GetType(ShelfTabButton), GetType(FilterChip), GetType(PillLabel)
    }


    ' A button, check box or single-line label clipped by a parent or the
    ' window, or whose caption needs more width than it has; a tab whose
    ' caption is wider than its tab or whose tab runs past the control's
    ' edge. A control Windows Forms sizes for its text (AutoSize) is not
    ' cut by it; a check box or label tall enough for two lines wraps.
    Private Shared Sub InspectControl(control As Control, bounds As Rectangle, clip As Rectangle, cut As List(Of String))

        Dim tabs As TabControl = TryCast(control, TabControl)

        If tabs IsNot Nothing Then

            For index As Integer = 0 To tabs.TabCount - 1

                Dim caption As String = tabs.TabPages(index).Text
                Dim rect As Rectangle = tabs.GetTabRect(index)
                Dim selectedBold As Boolean = TypeOf tabs Is UnderlineTabControl AndAlso index = tabs.SelectedIndex

                Using font As New Font(tabs.Font, If(selectedBold, FontStyle.Bold, tabs.Font.Style))
                    If TextWidth(caption, font, True) > rect.Width OrElse rect.Right > tabs.ClientSize.Width Then
                        cut.Add("tab '" & Abbreviate(caption) & "'")
                    End If
                End Using

            Next

            Return

        End If

        If SelfPainted.Contains(control.GetType()) OrElse String.IsNullOrWhiteSpace(control.Text) Then
            Return
        End If

        Dim lineHeight As Integer =
            TextRenderer.MeasureText("Xg", control.Font, New Size(Integer.MaxValue, Integer.MaxValue), CaptionFlags).Height

        If TypeOf control Is ButtonBase Then

            Dim button As ButtonBase = DirectCast(control, ButtonBase)
            Dim boxed As Boolean = TypeOf button Is CheckBox OrElse TypeOf button Is RadioButton

            If Not clip.Contains(bounds) Then
                cut.Add(Kind(control) & " '" & Abbreviate(button.Text) & "' clipped")
            ElseIf Not button.AutoSize AndAlso Not (boxed AndAlso button.Height >= 2 * lineHeight) Then

                ' The caption, the box and its gap, the button's padding (an
                ' ActionButton's is room it asks for, not a border it clips
                ' to) and three pixels of border a side.
                Dim needed As Integer =
                    TextWidth(button.Text, button.Font, button.UseMnemonic) +
                    If(boxed, UiTheme.Px(16, button.DeviceDpi) + 3, 0) +
                    If(TypeOf button Is ActionButton, 0, button.Padding.Horizontal) +
                    6

                If needed > button.ClientSize.Width Then
                    cut.Add(Kind(control) & " '" & Abbreviate(button.Text) & "'")
                End If

            End If

            Return

        End If

        If TypeOf control Is Label Then

            Dim label As Label = DirectCast(control, Label)

            If label.Text.Contains(ChrW(10)) OrElse label.Height >= 2 * lineHeight Then
                Return
            End If

            If Not clip.Contains(bounds) Then
                cut.Add("label '" & Abbreviate(label.Text) & "' clipped")
            ElseIf Not label.AutoSize AndAlso TextWidth(label.Text, label.Font, label.UseMnemonic) > label.ClientSize.Width - label.Padding.Horizontal Then
                cut.Add("label '" & Abbreviate(label.Text) & "'")
            End If

        End If

    End Sub


    ' The text's width on one line, with "&" read as a mnemonic prefix or
    ' shown literally as the control shows it.
    Private Shared Function TextWidth(text As String, font As Font, mnemonic As Boolean) As Integer

        Dim flags As TextFormatFlags =
            TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or If(mnemonic, TextFormatFlags.HidePrefix, TextFormatFlags.NoPrefix)

        Return TextRenderer.MeasureText(text, font, New Size(Integer.MaxValue, Integer.MaxValue), flags).Width

    End Function


    Private Shared Function Kind(control As Control) As String
        Return If(TypeOf control Is CheckBox, "check box", If(TypeOf control Is RadioButton, "option", "button"))
    End Function


    Private Shared Function Abbreviate(text As String) As String
        Dim line As String = text.Replace("&&", "&").Replace("|", "/").Replace(ChrW(13), " ").Replace(ChrW(10), " ")
        Return If(line.Length > 28, line.Substring(0, 27) & "…", line)
    End Function


    ' ---------------------------------------------------------------
    ' What the source says about a window's scaling
    ' ---------------------------------------------------------------

    Friend NotInheritable Class WindowRow

        Public ReadOnly Name As String
        Public ReadOnly Scaling As String
        Public ReadOnly ScalesWithWinForms As Boolean
        Public ReadOnly Cells As New Dictionary(Of String, Cell)(StringComparer.Ordinal)

        Public Sub New(name As String, scaling As (Scaling As String, ScalesWithWinForms As Boolean))
            Me.Name = name
            Me.Scaling = scaling.Scaling
            Me.ScalesWithWinForms = scaling.ScalesWithWinForms
        End Sub

    End Class


    ' "Windows Forms" when the window's source (its files and its base
    ' forms') sets the 96-DPI base dimensions or uses DialogScaling, "by
    ' hand" when it scales values from DeviceDpi or LogicalToDeviceUnits,
    ' "both" for both, which is where a value can be scaled twice, and
    ' "none" for neither.
    Friend Shared Function ScalingOf(view As Type) As (Scaling As String, ScalesWithWinForms As Boolean)
        Return ScalingOf(view, Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline"))
    End Function


    ' The same, read from a copy of the app's source laid out as the
    ' repository lays it out (Form1's files, and Forms), as the display
    ' evidence tool carries it to a computer without the repository.
    Friend Shared Function ScalingOf(view As Type, app As String) As (Scaling As String, ScalesWithWinForms As Boolean)

        Dim code As New StringBuilder()
        Dim current As Type = view

        While current IsNot Nothing AndAlso current IsNot GetType(Form)

            Dim folder As String = If(current Is GetType(Form1), app, Path.Combine(app, "Forms"))

            For Each file As String In Directory.GetFiles(folder, current.Name & "*.vb")
                code.AppendLine(String.Join(Environment.NewLine, IO.File.ReadAllLines(file).Where(Function(line) Not line.TrimStart().StartsWith("'"c))))
            Next

            current = current.BaseType

        End While

        Dim source As String = code.ToString()
        Dim winForms As Boolean = source.Contains("AutoScaleDimensions = New SizeF(96.0F, 96.0F)", StringComparison.Ordinal) OrElse source.Contains("DialogScaling.Begin(", StringComparison.Ordinal)
        Dim hand As Boolean = Regex.IsMatch(source, "DeviceDpi|LogicalToDeviceUnits")

        Return (If(winForms AndAlso hand, "both", If(winForms, "Windows Forms", If(hand, "by hand", "none"))), winForms)

    End Function


    ' ---------------------------------------------------------------
    ' The table
    ' ---------------------------------------------------------------

    Private Shared Function TableOf(rows As IEnumerable(Of WindowRow)) As String

        Dim columns As List(Of String) =
            Dpis.SelectMany(Function(dpi) Screens.Reverse().Select(Function(screen) screen.Name & " " & dpi)).ToList()

        Dim text As New StringBuilder()
        text.AppendLine("| Window | Scaling | " & String.Join(" | ", columns.Select(Function(column) column.Replace(" ", " @ "))) & " |")
        text.AppendLine("|---|---|" & String.Join("|", columns.Select(Function(column) "---")) & "|")

        For Each row As WindowRow In rows
            Dim name As String = If(row.Name = "Form1", "Form1 (main window)", row.Name)
            text.AppendLine("| " & name & " | " & row.Scaling & " | " & String.Join(" | ", columns.Select(Function(column) If(row.Cells.ContainsKey(column), row.Cells(column).Describe(), "not measured"))) & " |")
        Next

        Return text.ToString()

    End Function

End Class
