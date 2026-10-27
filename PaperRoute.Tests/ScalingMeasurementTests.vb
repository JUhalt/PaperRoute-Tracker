Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Services

' Every top-level window of the catalog, measured at 96, 120 and 144 DPI
' after fitting to the working area of a 1920x1080 and then of a 1366x768
' screen at that DPI (ScreensAt) (#93, step 2, and the working areas of
' step 4): whether the accept and cancel buttons are inside the client
' area, whether any button, check box, single-line label or tab caption
' is cut, and whether any list, list view, grid, tree view or multi-line
' text box shows less than two rows (step 4), and whether any control is
' squeezed to a line or to nothing (#255). The per-window table goes to
' the test output, as GitHub markdown, for #93.
'
' The host runs at 96 DPI, so a display is simulated. Every font grows by
' the DPI's factor, as text does on a real display, and then a window
' whose source sets the 96-DPI base dimensions, or uses DialogScaling, is
' scaled by the same factor, as Windows Forms scales it when it opens
' with its text already at the display's size. What the test cannot
' see: a value the code scales by hand from DeviceDpi or
' LogicalToDeviceUnits reads 96 here, so a hand-scaled window shows its
' text grown but its hand-scaled sizes not, and a converted window with a
' hand-scaled value left on a property Windows Forms scales shows no
' double scaling, and the same holds for what a shared control scales by
' hand in any window (the underline tabs' padding, a card's title gap).
' A size a window computes from a font while it is built is computed
' here before the text grows. These are proven only at a real display,
' against which this table is calibrated (#93: step 2, and step 4 after
' the first real 150% and 125% sessions). The frame a real display draws
' around a window, larger than the host's, is taken from its client
' area (Measure), since a real 150% display showed what keeping the
' host's frame hid.
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

    ' The screens 1.0 certifies (#93's plan amendment A), 1920x1080 and
    ' 1366x768, with Windows 11's taskbar, 48 pixels tall at 100% and grown
    ' with the scale: working areas of 1920x1032 and 1366x720 at 96 DPI,
    ' 1920x1020 and 1366x708 at 120, and 1920x1008 and 1366x696 at 144, as
    ' the certification VM measured at 150% and 125%. The tightest that 1.0
    ' certifies is 1366x708 at 125%. The larger screen first: fitting
    ' shrinks only, so one window serves both. Off screen, so a window the
    ' fit centers stays off screen too.
    Friend Shared Function ScreensAt(dpi As Integer) As (Name As String, Area As Rectangle)()

        Dim taskbar As Integer = 48 * dpi \ 96

        Return {WorkingArea(1920, 1080 - taskbar), WorkingArea(1366, 768 - taskbar)}

    End Function


    Private Shared Function WorkingArea(width As Integer, height As Integer) As (Name As String, Area As Rectangle)
        Return (width & "x" & height, New Rectangle(-30000, -30000, width, height))
    End Function


    ' One screen, for a test that measures a window at a display's size. A
    ' screen larger than this computer's can hold is measured at the
    ' largest it can hold, which leaves the window less room, not more.
    Friend Shared Function HeldScreen(width As Integer, height As Integer) As (Name As String, Area As Rectangle)()

        Dim track As Size = SystemInformation.MaxWindowTrackSize

        Return {WorkingArea(Math.Min(width, track.Width - 1), Math.Min(height, track.Height - 1))}

    End Function

    ' The windows converted to the one scaling rule, DialogScaling (#93,
    ' step 3); each conversion adds its window. Batch one: the four that
    ' had the base dimensions but not the rest of the rule. Batch two: the
    ' top-level windows the first table showed cut or with a button
    ' outside the client area. Step 4: Edit Journal, scaled by hand until
    ' a real 150% capture showed it failing; then the Crossref window,
    ' whose preview box a real 150% display left one line.
    Friend Shared ReadOnly Converted As Type() = {
        GetType(AboutForm), GetType(JournalCandidateForm), GetType(UpdateProgressForm), GetType(UpdatePromptForm),
        GetType(DeadlineDateForm), GetType(DiagnosticsForm), GetType(ExcelMappingForm), GetType(JournalChecklistItemEditForm),
        GetType(ManuscriptLinksForm), GetType(ManuscriptVersionEditForm), GetType(PublicationCheckForm), GetType(StartupFailureForm),
        GetType(SubmissionPacketEditForm), GetType(UnexpectedErrorForm), GetType(JournalEditForm), GetType(CrossrefLookupForm)
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


    ' No control collapses in a window built at 120 or 144 DPI (#255). At
    ' a real 125% About's logo was 130x1 pixels and the version date had
    ' no width: the sweep above scales a window after it has opened at 96
    ' DPI, where both were whole, so it read "ok". Here every window of the
    ' catalog is built as at each display (AtDisplayDpi) and fails a
    ' control the measure reports collapsed; at 96 DPI the sweep's own
    ' windows are built so. Their text keeps the host's size, so nothing
    ' else the measure reports is judged here.
    <TestMethod>
    Public Sub NoControlCollapsesInAWindowBuiltAt120Or144Dpi()

        Dim found As New List(Of String)()
        Dim measured As Integer = 0

        For Each dpi As Integer In Dpis.Where(Function(value) value <> 96)

            Dim current As Integer = dpi

            AtDisplayDpi(
                current,
                Sub()
                    found.AddRange(
                        ViewCatalog.ShowEach(
                            ViewCatalog.Dialogs.Select(Function(entry) (entry.Key.Name, entry.Value)),
                            Sub(name, shown)

                                measured += 1

                                found.AddRange(
                                    Inspect(shown).Cut.
                                        Where(Function(entry) entry.Contains(" collapsed to ", StringComparison.Ordinal)).
                                        Select(Function(entry) name & " at " & current & " DPI: " & entry)
                                )

                            End Sub
                        )
                    )
                End Sub
            )

        Next

        Assert.AreEqual(2 * ViewCatalog.Dialogs.Count, measured, "Every window was built at each DPI.")
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' Add Metric's Year and the version date keep their widths in a window
    ' built at 96 to 144 DPI (#255). At a real 125% the Year showed only an
    ' edge of its box and the version date nothing: each window's grid
    ' squeezed them as they were added, before it had its size, and
    ' Windows Forms scaled the squeezed widths as the window finished. Each
    ' is its 96-DPI width scaled, within 3 pixels: Windows Forms scales
    ' what is inside a box's border, not the border. At 96 DPI they were
    ' whole before the fix too.
    <TestMethod>
    <DataRow(96)>
    <DataRow(120)>
    <DataRow(144)>
    Public Sub TheYearAndTheVersionDateKeepTheirWidthsAt96To144Dpi(dpi As Integer)

        Dim fields As (View As Type, Field As String, Width As Integer)() = {
            (GetType(JournalMetricForm), "numYear", 90),
            (GetType(ManuscriptVersionEditForm), "dtpVersionDate", 190)
        }

        Dim found As New List(Of String)()

        AtDisplayDpi(
            dpi,
            Sub()
                For Each field As (View As Type, Field As String, Width As Integer) In fields

                    Dim current As (View As Type, Field As String, Width As Integer) = field

                    found.AddRange(
                        ViewCatalog.ShowEach(
                            {(current.View.Name, ViewCatalog.Dialogs(current.View))},
                            Sub(name, shown)

                                Dim control As Control =
                                    DirectCast(current.View.GetField(current.Field, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(shown), Control)

                                Dim expected As Integer = CInt(Math.Round(current.Width * dpi / 96.0))

                                If Math.Abs(control.Width - expected) > 3 Then
                                    found.Add($"{name}'s {current.Field} at {dpi} DPI is {control.Width} pixels wide, not {expected}")
                                End If

                            End Sub
                        )
                    )

                Next
            End Sub
        )

        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

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


    ' The sweep's screens are the two 1.0 certifies as Windows 11 lays them
    ' out at each DPI (#93, step 4): 1920x1080 and 1366x768, less a taskbar
    ' 48 pixels tall at 100% that grows with the scale. The certification
    ' VM measured 1920x1008 at 150% and 1366x708 at 125%, where the sweep
    ' had 1920x1003 and 1366x728 at every DPI.
    <TestMethod>
    Public Sub TheScreensAreWindows11sWorkingAreasAtEachDpi()

        Dim expected As New Dictionary(Of Integer, String()) From {
            {96, {"1920x1032", "1366x720"}},
            {120, {"1920x1020", "1366x708"}},
            {144, {"1920x1008", "1366x696"}}
        }

        For Each dpi As Integer In Dpis

            Dim at As Integer = dpi
            Dim names As List(Of String) = Nothing

            ViewCatalog.ShowAndClose(
                "The probe",
                Function() Probe(),
                Sub(name, shown)
                    names = Measure(shown, at, False).Select(Function(cell) cell.Screen).ToList()
                End Sub
            )

            CollectionAssert.AreEqual(expected(at), names, $"At {at} DPI the screens are {String.Join(", ", expected(at))}: {String.Join(", ", names)}")

        Next

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


    ' The measure sees a size scaled twice (#93, step 4). At a real display
    ' a window's text has the display's size while the window is built, so
    ' a button that sizes itself to its caption has its caption's width
    ' there when Windows Forms scales the window, and one that only grows
    ' keeps the scaled width. The error dialog's four buttons, so built,
    ' overran their row at 125% and 150% and Close PaperRoute lay over Open
    ' Log Folder, which the measure before, scaling the window before it
    ' grew the text, reported whole. The same row of buttons that only
    ' grow, in a window that sets the base dimensions itself, is reported
    ' cut at 144 DPI. Built with the one scaling rule, DialogScaling, they
    ' are scaled once and fit, as buttons that also shrink do; at 96 DPI
    ' all fit.
    <TestMethod>
    Public Sub AButtonSizedToItsCaptionIsScaledAgain()

        For Each probe As (Mode As AutoSizeMode, Ruled As Boolean) In {
            (AutoSizeMode.GrowOnly, False), (AutoSizeMode.GrowOnly, True), (AutoSizeMode.GrowAndShrink, True)
        }

            Dim current As (Mode As AutoSizeMode, Ruled As Boolean) = probe

            For Each dpi As Integer In {96, 144}

                Dim at As Integer = dpi
                Dim result As Cell = Nothing

                ViewCatalog.ShowAndClose(
                    "The button row probe",
                    Function() ButtonRowProbe(current.Mode, current.Ruled),
                    Sub(name, shown)
                        result = Measure(shown, at, True, ProbeScreens).Single(Function(cell) cell.Screen = "roomy").Result
                    End Sub
                )

                Dim built As String = current.Mode.ToString() & If(current.Ruled, " with DialogScaling", " with base dimensions alone")

                Assert.IsNull(result.NotMeasuredHere, "The probe is measured on this computer: " & result.Describe())

                If current.Mode = AutoSizeMode.GrowOnly AndAlso Not current.Ruled AndAlso at = 144 Then
                    Assert.IsTrue(result.Cut.Any(Function(cut) cut.StartsWith("button 'Open Log Folder'", StringComparison.Ordinal)), "Grown to their captions and then scaled, the buttons overrun their row: " & result.Describe())
                Else
                    Assert.IsTrue(result.Passes, $"{built} at {at} DPI, the row holds its buttons: " & result.Describe())
                End If

            Next

        Next

    End Sub

    ' The error dialog's last row: each button's caption, the minimum it is
    ' given at 96 DPI, and whether it is on the left.
    Private Shared ReadOnly RowButtons As (Text As String, Minimum As Size, OnLeft As Boolean)() = {
        ("Copy Details", New Size(120, 36), True), ("Open Log Folder", New Size(140, 36), True),
        ("Keep Working", New Size(130, 36), False), ("Close PaperRoute", New Size(150, 36), False)
    }

    ' The error dialog's last row as it was: a 640-pixel window built with
    ' DialogScaling, or with the base dimensions and nothing else of the
    ' rule, two buttons on the left and two on the right, each sizing
    ' itself to its caption over a minimum width, 572 pixels of buttons in
    ' a 592-pixel row at 96 DPI. Its text is 10 points unless another size
    ' is asked for, set before the buttons are added.
    Private Shared Function ButtonRowProbe(mode As AutoSizeMode, ruled As Boolean, Optional points As Single = 10.0F) As Form

        Dim form As New Form()

        If ruled Then
            DialogScaling.Begin(form)
        Else
            form.SuspendLayout()
            form.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            form.AutoScaleMode = AutoScaleMode.Dpi
        End If

        form.ClientSize = New Size(640, 120)
        form.Font = New Font("Segoe UI", points)

        Dim row As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(24)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        Dim tools As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Anchor = AnchorStyles.Left, .Margin = New Padding(0)}
        Dim decisions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .FlowDirection = FlowDirection.RightToLeft, .Anchor = AnchorStyles.Right, .Margin = New Padding(0)}

        For Each spec As (Text As String, Minimum As Size, OnLeft As Boolean) In RowButtons
            Dim group As FlowLayoutPanel = If(spec.OnLeft, tools, decisions)
            group.Controls.Add(New Button With {.Text = spec.Text, .AutoSize = True, .AutoSizeMode = mode, .MinimumSize = spec.Minimum, .Padding = New Padding(8, 0, 8, 0), .Margin = New Padding(0, 0, 8, 0)})
        Next

        row.Controls.Add(tools, 0, 0)
        row.Controls.Add(decisions, 1, 0)
        form.Controls.Add(row)

        If ruled Then
            DialogScaling.Finish(form)
        Else
            form.ResumeLayout(False)
            form.PerformLayout()
        End If

        Return form

    End Function


    ' A text-sized button keeps the size it was given, not the width it
    ' grew to (#93, step 4). At a real display the window is built with its
    ' text at the display's size, so when DialogScaling.Finish runs such a
    ' button has already grown to its caption there; a minimum read from it
    ' then would be that caption's width, and scaled by 1.5 the button would
    ' be 1.5 times its caption again. The sweep cannot tell the two apart:
    ' it builds a window at 96 DPI, where a caption is its 96-DPI width. So
    ' the error dialog's row is built here as at 150%, its text at 15 points
    ' before its buttons are added, and then scaled by 1.5 as Windows Forms
    ' scales it when it opens, with nothing simulated. Each button is the
    ' larger of its 96-DPI minimum, scaled, and its caption, and not 1.5
    ' times the width it grew to. Each caption is wider than its minimum at
    ' 15 points, so the two differ on every button.
    <TestMethod>
    Public Sub AButtonBuiltAtTheDisplaysTextSizeIsScaledOnce()

        Dim grown As New Dictionary(Of String, Size)()
        Dim scaled As New Dictionary(Of String, (Size As Size, Caption As Size))()

        ViewCatalog.ShowAndClose(
            "The button row probe at 15 points",
            Function() ButtonRowProbe(AutoSizeMode.GrowOnly, True, 15.0F),
            Sub(name, shown)

                Dim buttons As List(Of Button) = ViewCatalog.Descendants(shown).OfType(Of Button)().ToList()

                For Each button As Button In buttons
                    grown(button.Text) = button.Size
                Next

                shown.Scale(New SizeF(1.5F, 1.5F))
                LayOutEveryControl(shown)
                Application.DoEvents()

                For Each button As Button In buttons
                    scaled(button.Text) = (button.Size, CaptionSize(button))
                Next

            End Sub
        )

        Assert.AreEqual(RowButtons.Length, scaled.Count, "Every button of the row was measured.")

        Dim found As New List(Of String)()
        Dim measured As New List(Of String)()

        For Each spec As (Text As String, Minimum As Size, OnLeft As Boolean) In RowButtons

            Dim before As Size = grown(spec.Text)
            Dim after As (Size As Size, Caption As Size) = scaled(spec.Text)

            Assert.IsTrue(before.Width > spec.Minimum.Width, $"At 15 points '{spec.Text}' grows past its minimum: {before.Width} of {spec.Minimum.Width} pixels.")

            Dim expected As New Size(
                Math.Max(CInt(Math.Round(spec.Minimum.Width * 1.5)), after.Caption.Width),
                Math.Max(CInt(Math.Round(spec.Minimum.Height * 1.5)), after.Caption.Height)
            )
            Dim again As Integer = CInt(Math.Round(before.Width * 1.5))
            Dim line As String =
                $"'{spec.Text}' scaled by 1.5: {after.Size.Width}x{after.Size.Height}; its minimum {spec.Minimum.Width}x{spec.Minimum.Height} scaled, or its caption {after.Caption.Width}x{after.Caption.Height}, gives {expected.Width}x{expected.Height}, and 1.5 times the {before.Width} it grew to is {again}"

            measured.Add(line)

            If Math.Abs(after.Size.Width - expected.Width) > 1 OrElse Math.Abs(after.Size.Height - expected.Height) > 1 OrElse after.Size.Width >= again - 1 Then
                found.Add(line)
            End If

        Next

        TestContext.WriteLine(String.Join(Environment.NewLine, measured))

        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' A button that sizes itself to its caption is scaled once in every
    ' window on DialogScaling (#93, step 4). At a real 150% display such a
    ' button that only grows took its caption's width at the display's text
    ' size while its window was built, then kept that width scaled by 1.5:
    ' ManuscriptLinksForm's "Choose from Library..." was 291 pixels,
    ' UpdatePromptForm's "Download && Restart" 332, SubmissionPacketEditForm's
    ' "Save Changes" 214 and AboutForm's "Copy Version Info" 268, about 1.5
    ' times their captions. At a simulated 144 DPI each such button of every
    ' converted window is the larger of its least size, scaled, and its
    ' caption's preferred size, on each side. Its least size is its
    ' MinimumSize and, for a button that only grows, the size it was given
    ' too, which Windows Forms gives back when AutoSize is turned off: a
    ' button given no size is never narrower than its 75-pixel default.
    ' Buttons whose equal width comes from a shared minimum keep it.
    <TestMethod>
    Public Sub EveryTextSizedButtonIsScaledOnce()

        Dim found As New List(Of String)()
        Dim measured As New List(Of String)()

        found.AddRange(
            ViewCatalog.ShowEach(
                Converted.Select(Function(view) (view.Name, ViewCatalog.Dialogs(view))),
                Sub(name, shown)

                    ' As built at 96 DPI: what each text-sized button is held at.
                    Dim least As New Dictionary(Of Button, Size)()

                    For Each button As Button In ViewCatalog.Descendants(shown).OfType(Of Button)().Where(Function(candidate) candidate.AutoSize).ToList()
                        least(button) = LeastSize(button)
                    Next

                    Simulate(shown, 144, True)

                    CompareTextSizedButtons(shown, name, least, found, measured)

                End Sub
            )
        )

        TestContext.WriteLine(String.Join(Environment.NewLine, measured))

        Assert.IsTrue(measured.Count >= 40, "The converted windows' text-sized buttons were measured: " & measured.Count)
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' Each text-sized button in view, the pages of a tab control in turn.
    Private Shared Sub CompareTextSizedButtons(parent As Control, window As String, least As Dictionary(Of Button, Size), found As List(Of String), measured As List(Of String))

        Dim tabs As TabControl = TryCast(parent, TabControl)

        If tabs IsNot Nothing Then

            Dim selected As TabPage = tabs.SelectedTab

            For index As Integer = 0 To tabs.TabCount - 1
                tabs.SelectedIndex = index
                Application.DoEvents()
                CompareTextSizedButtons(tabs.TabPages(index), window, least, found, measured)
            Next

            tabs.SelectedTab = selected
            Application.DoEvents()
            Return

        End If

        For Each child As Control In parent.Controls

            Dim button As Button = TryCast(child, Button)

            If button IsNot Nothing AndAlso button.Visible AndAlso least.ContainsKey(button) Then

                Dim held As Size = least(button)
                Dim caption As Size = CaptionSize(button)
                Dim expected As New Size(
                    Math.Max(CInt(Math.Round(held.Width * 1.5)), caption.Width),
                    Math.Max(CInt(Math.Round(held.Height * 1.5)), caption.Height)
                )
                Dim line As String =
                    $"{window} '{Abbreviate(button.Text)}' at 144 DPI: {button.Width}x{button.Height}; its least size {held.Width}x{held.Height} scaled, or its caption {caption.Width}x{caption.Height}, gives {expected.Width}x{expected.Height}"

                measured.Add(line)

                If Math.Abs(button.Width - expected.Width) > 1 OrElse Math.Abs(button.Height - expected.Height) > 1 Then
                    found.Add(line)
                End If

                least.Remove(button)

            End If

            CompareTextSizedButtons(child, window, least, found, measured)

        Next

    End Sub


    ' What a text-sized button is held at however short its caption: its
    ' MinimumSize and, if it only grows, the size it was given, which
    ' Windows Forms gives back when AutoSize is turned off. Its parent is
    ' held meanwhile and laid out after, so the button is as it was.
    Private Shared Function LeastSize(button As Button) As Size

        If button.AutoSizeMode = AutoSizeMode.GrowAndShrink Then
            Return button.MinimumSize
        End If

        Dim parent As Control = button.Parent
        parent.SuspendLayout()
        button.AutoSize = False
        Dim given As Size = button.Size
        button.AutoSize = True
        parent.ResumeLayout(True)

        Return New Size(Math.Max(given.Width, button.MinimumSize.Width), Math.Max(given.Height, button.MinimumSize.Height))

    End Function


    ' The size a button's caption asks for on its own: a button of its kind
    ' with its text, font, padding and style, that grows and shrinks to its
    ' caption with no minimum.
    Private Shared Function CaptionSize(button As Button) As Size

        Using probe As Button = DirectCast(Activator.CreateInstance(button.GetType()), Button)

            probe.Text = button.Text
            probe.Font = button.Font
            probe.Padding = button.Padding
            probe.FlatStyle = button.FlatStyle
            probe.FlatAppearance.BorderSize = button.FlatAppearance.BorderSize
            probe.UseMnemonic = button.UseMnemonic
            probe.Image = button.Image
            probe.TextImageRelation = button.TextImageRelation
            probe.AutoSize = True
            probe.AutoSizeMode = AutoSizeMode.GrowAndShrink

            Return probe.GetPreferredSize(Size.Empty)

        End Using

    End Function


    ' The measure judges lists (#93, step 4). At the real 1366x708 working
    ' area at 125%, Submission Details gave its Correspondence & Files list
    ' no height at all and its Editorial History list one row, so its
    ' letters and files could not be seen, and the measure, which judged
    ' only buttons, check boxes, single-line labels and tab captions, read
    ' "ok". A probe holds one of each kind it now judges below a tall fixed
    ' row: a list, a list view, a grid, a tree view, a multi-line text box,
    ' and a list under a toolbar of its own, as Submission Details' list is.
    ' On the roomy screen each shows two rows or more and none is reported;
    ' fitted to the short one each shows less, the last with no height at
    ' all, and each is: a list with none is judged before the measure skips
    ' a control with no size.
    <TestMethod>
    Public Sub ASqueezedListIsReported()

        Dim results As New Dictionary(Of String, Cell)()
        Dim filesHeight As Integer = -1

        ViewCatalog.ShowAndClose(
            "The list probe",
            Function() ListProbe(),
            Sub(name, shown)

                For Each cell As (Screen As String, Dpi As Integer, Result As Cell) In Measure(shown, 96, False, ProbeScreens)
                    results(cell.Screen) = cell.Result
                Next

                ' As fitted to the short screen, the last.
                filesHeight = ViewCatalog.Descendants(shown).OfType(Of ListBox)().Single(Function(list) list.AccessibleName = "Files").Height

            End Sub
        )

        For Each screen As String In {"roomy", "short"}
            Assert.IsNull(results(screen).NotMeasuredHere, "The probe is measured on this computer: " & results(screen).Describe())
        Next

        Assert.AreEqual(0, filesHeight, "On the short screen the last list has no height at all.")

        Assert.IsTrue(results("roomy").Passes, "With room, every list shows two rows: " & results("roomy").Describe())

        For Each squeezed As String In {"list 'Letters'", "list view 'Facts'", "grid 'Mappings'", "tree view 'Outline'", "text box 'Notes'", "list 'Files'"}
            Assert.IsTrue(results("short").Cut.Any(Function(cut) cut.StartsWith(squeezed, StringComparison.Ordinal)), "Squeezed, it is reported, " & squeezed & ": " & String.Join("; ", results("short").Cut))
        Next

    End Sub


    ' A 900x500 window: a 340-pixel row, then one row that takes the rest,
    ' with a list, a list view in details view, a grid, a tree view and a
    ' multi-line text box side by side, each with two rows of its own, and
    ' a list docked to fill a panel below a 60-pixel toolbar docked to its
    ' top. Fitted to the 400-pixel screen, the window keeps about 20 pixels
    ' for that row, and the toolbar leaves the last list none.
    Private Shared Function ListProbe() As Form

        Dim form As New Form With {.ClientSize = New Size(900, 500), .Font = New Font("Segoe UI", 10.0F), .AutoScaleMode = AutoScaleMode.Dpi}

        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 6, .RowCount = 2}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 340))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        For column As Integer = 0 To 5
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100 / 6.0F))
        Next

        Dim letters As New ListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False, .AccessibleName = "Letters"}
        letters.Items.AddRange({"Decision letter", "Reviews", "Revised manuscript"})

        Dim facts As New ListView With {.Dock = DockStyle.Fill, .View = View.Details, .AccessibleName = "Facts"}
        facts.Columns.Add("Fact", 100)
        facts.Items.Add("Impact")
        facts.Items.Add("Turnaround")

        Dim mappings As New DataGridView With {.Dock = DockStyle.Fill, .AccessibleName = "Mappings", .AllowUserToAddRows = False}
        mappings.Columns.Add("column", "Column")
        mappings.Rows.Add("Title")
        mappings.Rows.Add("Journal")

        Dim outline As New TreeView With {.Dock = DockStyle.Fill, .AccessibleName = "Outline"}
        outline.Nodes.Add("Introduction")
        outline.Nodes.Add("Methods")

        Dim notes As New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .AccessibleName = "Notes", .Text = "First line" & Environment.NewLine & "Second line"}

        ' Docked last, the list fills what the toolbar leaves of the panel.
        Dim files As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
        files.Controls.Add(New ListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False, .AccessibleName = "Files"})
        files.Controls.Add(New Panel With {.Dock = DockStyle.Top, .Height = 60})

        root.Controls.Add(New Label With {.Text = "Summary", .AutoSize = True}, 0, 0)
        root.Controls.Add(letters, 0, 1)
        root.Controls.Add(facts, 1, 1)
        root.Controls.Add(mappings, 2, 1)
        root.Controls.Add(outline, 3, 1)
        root.Controls.Add(notes, 4, 1)
        root.Controls.Add(files, 5, 1)
        form.Controls.Add(root)

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
                    result = Measure(shown, 96, False).Single(Function(cell) cell.Screen = "1366x720").Result
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


    ' The measure gives a window the frame a real display draws (#93). At
    ' a real 150% display a window's title bar and borders are 22x56
    ' pixels, where the host draws them 16x39 at 96 DPI. A window that
    ' Windows Forms does not scale and that keeps the outer size it was
    ' given has the difference less client area there: the Crossref
    ' window, 820x720 on both, had 664 pixels of client height at 150% to
    ' the sweep's 681, and its preview box, which takes what the rows
    ' around it leave, showed one line where the sweep counted two. A
    ' probe built so, whose box has two lines and 8 pixels to spare inside
    ' the frame drawn at 96 DPI, is reported at 144: the larger frame takes
    ' more than the 8. The same window given its client size keeps it, as
    ' Windows Forms keeps it, and is not reported; but on a screen only 4
    ' pixels taller than the window it is, since the working area holds
    ' the larger frame too. The measure before reported neither.
    <TestMethod>
    Public Sub TheDisplaysFrameIsTakenFromTheClientArea()

        For Each probe As (KeepsClientSize As Boolean, Held As Boolean) In {(False, False), (True, False), (True, True)}

            Dim current As (KeepsClientSize As Boolean, Held As Boolean) = probe
            Dim result As Cell = Nothing

            ViewCatalog.ShowAndClose(
                "The frame probe",
                Function() FrameProbe(8),
                Sub(name, shown)

                    Dim screen As (Name As String, Area As Rectangle)() =
                        If(current.Held,
                           {("held", New Rectangle(-30000, -30000, 1000, shown.Height + 4))},
                           ProbeScreens.Take(1).ToArray())

                    result = Measure(shown, 144, False, screen, current.KeepsClientSize).Single().Result

                End Sub
            )

            Dim built As String = If(current.KeepsClientSize, "given its client size", "keeping its outer size") & If(current.Held, ", on a screen 4 pixels taller than it", String.Empty)
            Dim reported As Boolean = result.Cut.Any(Function(cut) cut.StartsWith("text box 'Preview'", StringComparison.Ordinal))

            Assert.IsNull(result.NotMeasuredHere, "The probe is measured on this computer: " & result.Describe())

            If current.KeepsClientSize AndAlso Not current.Held Then
                Assert.IsFalse(reported, $"A window {built} keeps two lines of its box at 144 DPI: " & result.Describe())
            Else
                Assert.IsTrue(reported, $"A window {built} loses the larger frame from its box at 144 DPI: " & result.Describe())
            End If

        Next

    End Sub


    ' The measure reads a tab page's clip once it has shown the page
    ' (#93). Showing a page can move the focus and scroll a body around
    ' the tabs: at a real 125% display Submission Details' body scrolled as
    ' the measure showed its Correspondence & Files page, and the page's
    ' first line, its help, sat above where the tabs had been before the
    ' scroll. The measure had read the tabs' clip before it showed the
    ' page and reported the help clipped, while the display showed it
    ' whole. A probe whose second page scrolls its body as it shows has a
    ' label on that page's first line, in view once the page shows: it is
    ' not reported. The measure before reported it ("label 'Second page's
    ' first line' clipped").
    <TestMethod>
    Public Sub APageIsMeasuredWhereShowingItScrolled()

        Dim result As Cell = Nothing

        ViewCatalog.ShowAndClose(
            "The tab probe",
            Function() TabProbe(),
            Sub(name, shown)
                result = Measure(shown, 96, False, ProbeScreens.Take(1).ToArray()).Single().Result
            End Sub
        )

        Assert.IsNull(result.NotMeasuredHere, "The probe is measured on this computer: " & result.Describe())
        Assert.IsTrue(result.Passes, "Each page's first line is in view where showing the page scrolled the body: " & result.Describe())

    End Sub


    ' A 600x400 window whose body scrolls, holding a 300-pixel panel with a
    ' text box and, below it, tabs 400 pixels tall, each page with a label
    ' on its first line. Showing a page scrolls the body to the tabs, as a
    ' focus moved into them does. The text box takes the focus as the
    ' window shows, so the body is unscrolled when the measure begins.
    Private Shared Function TabProbe() As Form

        Dim form As New Form With {.ClientSize = New Size(600, 400), .Font = New Font("Segoe UI", 10.0F), .AutoScaleMode = AutoScaleMode.Dpi}
        Dim body As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
        Dim tabs As New TabControl With {.Location = New Point(0, 300), .Size = New Size(560, 400)}

        For Each caption As String In {"First", "Second"}
            Dim page As New TabPage With {.Text = caption}
            page.Controls.Add(New Label With {.Text = caption & " page's first line", .AutoSize = True, .Location = New Point(10, 10)})
            tabs.TabPages.Add(page)
        Next

        AddHandler tabs.SelectedIndexChanged,
            Sub(sender, e)
                body.AutoScrollPosition = New Point(0, 300)
            End Sub

        ' Without the box the tabs would take the focus as the window
        ' shows and scroll the body to them before the measure, and showing
        ' the second page would move nothing: the stale clip went untested.
        Dim top As New Panel With {.Location = New Point(0, 0), .Size = New Size(560, 300)}
        top.Controls.Add(New TextBox With {.Location = New Point(10, 10)})

        body.Controls.Add(top)
        body.Controls.Add(tabs)
        form.Controls.Add(body)
        Return form

    End Function


    ' A 600-pixel window of two fixed rows, 120 and 100 pixels, with a
    ' multi-line box between them that takes the rest: at 144 DPI, its
    ' text grown, the box has two lines and the given pixels to spare
    ' inside the frame drawn at 96 DPI.
    Private Shared Function FrameProbe(spare As Integer) As Form

        Dim line As Integer

        Using grown As New Font("Segoe UI", 15.0F)
            line = grown.Height
        End Using

        ' Two lines, the box's border and its margins, and the spare.
        Dim boxRow As Integer = 2 * line + 4 + 6 + spare

        Dim form As New Form With {.ClientSize = New Size(600, 220 + boxRow), .Font = New Font("Segoe UI", 10.0F), .AutoScaleMode = AutoScaleMode.Dpi}
        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 120))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 100))
        root.Controls.Add(New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .AccessibleName = "Preview"}, 0, 1)
        form.Controls.Add(root)
        Return form

    End Function


    ' ---------------------------------------------------------------
    ' Simulating the display
    ' ---------------------------------------------------------------

    ' Grows the window's text for the DPI, then scales the window as
    ' Windows Forms would if its source asks for that, gives it the frame
    ' the DPI draws, then fits it to each screen and inspects it. Whether a
    ' window keeps the client size it was given, rather than its outer
    ' size, is read from its source (KeepsItsClientSize) unless a probe
    ' says.
    Friend Shared Function Measure(
        shown As Form,
        dpi As Integer,
        scalesWithWinForms As Boolean,
        Optional areas As (Name As String, Area As Rectangle)() = Nothing,
        Optional keepsClientSize As Boolean? = Nothing
    ) As List(Of (Screen As String, Dpi As Integer, Result As Cell))

        Simulate(shown, dpi, scalesWithWinForms)

        ' A real display draws a window's title bar and borders at its DPI,
        ' larger than the host's at 96 (#93): at 150% a sizable window's
        ' are 22x56 pixels to 16x39 here. A window Windows Forms does not
        ' scale keeps the outer size it was given, if that is what it was
        ' given, so it has the difference less client area; Windows Forms
        ' scales a window's client area and keeps it. And the working area
        ' holds the larger frame, so a window it holds has that much less
        ' client area too. Both come off here: the window's size and least
        ' size, where it keeps its outer size, and each screen's area. At
        ' 96 DPI nothing changes.
        Dim frame As Size =
            FrameGrowth(shown, dpi)

        If Not frame.IsEmpty AndAlso
           Not scalesWithWinForms AndAlso
           Not If(keepsClientSize, KeepsItsClientSize(shown.GetType())) Then

            Dim least As Size = shown.MinimumSize

            shown.MinimumSize =
                New Size(
                    If(least.Width > 0, Math.Max(1, least.Width - frame.Width), 0),
                    If(least.Height > 0, Math.Max(1, least.Height - frame.Height), 0)
                )

            shown.Size = shown.Size - frame

        End If

        Dim results As New List(Of (Screen As String, Dpi As Integer, Result As Cell))()

        ' Windows Forms holds a window to the largest size this computer's
        ' screen can track. On a screen smaller than a cell's area, as on a
        ' 1024x768 build runner, a window held there is not the window the
        ' cell asks about, so the cell says it was not measured here rather
        ' than report the cuts the hold makes.
        Dim track As Size =
            SystemInformation.MaxWindowTrackSize

        For Each screen As (Name As String, Area As Rectangle) In If(areas, ScreensAt(dpi))

            ResponsiveDialogSizingService.FitToWorkingArea(
                shown,
                New Rectangle(screen.Area.Location, screen.Area.Size - frame)
            )

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


    ' Grows the window's text for the DPI and then scales the window as
    ' Windows Forms would if its source asks for that. Nothing changes at
    ' 96 DPI.
    Friend Shared Sub Simulate(
        shown As Form,
        dpi As Integer,
        scalesWithWinForms As Boolean
    )

        Dim factor As Single = dpi / 96.0F

        If dpi <> 96 Then

            ' In the order a window is built at a real display (#93, step
            ' 4): its text has the display's size from the start, so a
            ' control that sizes itself to its text is laid out at that size
            ' before Windows Forms scales the sizes written at 96 DPI, and a
            ' button that only grows keeps the width the scale gives it. At
            ' a real 150% the error dialog's buttons were 1.5 times their
            ' caption widths; scaling first, as this did before, gave them
            ' their scaled minimums and missed the overlap they made.
            GrowText(shown, dpi)

            LayOutEveryControl(shown)

            If scalesWithWinForms Then
                shown.Scale(New SizeF(factor, factor))
                LayOutEveryControl(shown)
            End If

            Application.DoEvents()

        End If

    End Sub


    ' Every font in the window grown for the DPI, as text has the display's
    ' size at a real display. Each control's font is read before any is
    ' set, so a font a control takes from its parent grows once. A test
    ' may grow a window's text while its layouts are held, as a window is
    ' built at a real display.
    Friend Shared Sub GrowText(shown As Control, dpi As Integer)

        Dim factor As Single = dpi / 96.0F
        Dim fonts As New Dictionary(Of Control, Font)()

        For Each control As Control In New Control() {shown}.Concat(ViewCatalog.Descendants(shown))
            fonts(control) = control.Font
        Next

        For Each pair As KeyValuePair(Of Control, Font) In fonts
            pair.Key.Font = New Font(pair.Value.FontFamily, pair.Value.Size * factor, pair.Value.Style, pair.Value.Unit)
        Next

    End Sub


    ' Runs the action as at a display of the DPI (#255): every window it
    ' builds is built as that display builds it. Windows Forms reads the
    ' display's DPI once, as the app starts, and scales a window from it
    ' when the window's layout resumes at the end of its build
    ' (DialogScaling.Finish, or a window's own ResumeLayout), before its
    ' first full layout and before it has a handle; Simulate scales a
    ' window after it has opened and laid out at 96 DPI. What a window's
    ' panels did to its controls while it was built is scaled here as at
    ' the display: at a real 125% About's logo was 130x1 pixels, which
    ' Simulate scaled from a whole 104x104. Windows Forms is told the DPI
    ' for the action, through the one value it keeps it in, and told the
    ' host's again after, with the rest of its scaling state as it was.
    ' Text keeps the host's size, since GDI measures it at the host's DPI,
    ' so what this judges is layout, not captions.
    Friend Shared Sub AtDisplayDpi(dpi As Integer, action As Action)

        Dim helper As Type =
            Type.GetType("System.Windows.Forms.ScaleHelper, System.Windows.Forms.Primitives", throwOnError:=False)

        Dim display As FieldInfo =
            helper?.GetField("<InitialSystemDpi>k__BackingField", BindingFlags.Static Or BindingFlags.NonPublic)

        Assert.IsNotNull(display, "Windows Forms keeps the display's DPI where a test can set it; without it no window can be built as at a display.")

        Dim state As List(Of (Field As FieldInfo, Value As Object)) =
            helper.GetFields(BindingFlags.Static Or BindingFlags.NonPublic Or BindingFlags.Public).
                Where(Function(field) Not field.IsLiteral AndAlso Not field.IsInitOnly).
                Select(Function(field) (field, field.GetValue(Nothing))).
                ToList()

        display.SetValue(Nothing, dpi)

        Try

            ' The measure has teeth only if Windows Forms scales from it.
            StaRunner.Run(
                Sub()
                    Using probe As New Form With {.AutoScaleMode = AutoScaleMode.Dpi}
                        Assert.AreEqual(New SizeF(dpi, dpi), probe.CurrentAutoScaleDimensions, "Windows Forms scales a window from the display's DPI.")
                    End Using
                End Sub
            )

            action()

        Finally

            For Each kept As (Field As FieldInfo, Value As Object) In state
                kept.Field.SetValue(Nothing, kept.Value)
            Next

        End Try

    End Sub


    ' Grown text and Scale defer the layouts they cause, and Scale drops
    ' them. A window that opens is laid out by the size messages its new
    ' windows get; these windows exist, so each control is laid out by
    ' hand, parents first, and once more for what the first pass moved.
    Private Shared Sub LayOutEveryControl(shown As Form)

        For pass As Integer = 1 To 2
            For Each control As Control In New Control() {shown}.Concat(ViewCatalog.Descendants(shown))
                control.PerformLayout()
            Next
        Next

    End Sub


    ' How much wider and taller the window's title bar and borders are at
    ' the DPI than at 96, as Windows sizes them for the window's own
    ' styles: 6x17 pixels at 144 and 2x8 at 120 for a sizable window on
    ' Windows 11, what the certification VM's windows showed.
    Private Shared Function FrameGrowth(shown As Form, dpi As Integer) As Size

        If dpi = 96 Then
            Return Size.Empty
        End If

        Dim style As UInteger = GetWindowLong(shown.Handle, GwlStyle)
        Dim extended As UInteger = GetWindowLong(shown.Handle, GwlExStyle)

        Return FrameAt(style, extended, dpi) - FrameAt(style, extended, 96)

    End Function


    Private Shared Function FrameAt(style As UInteger, extended As UInteger, dpi As Integer) As Size

        Dim frame As New NativeRect()

        If Not AdjustWindowRectExForDpi(frame, style, False, extended, CUInt(dpi)) Then
            Throw New InvalidOperationException("Windows did not size a window's frame at " & dpi & " DPI.")
        End If

        Return New Size(frame.Right - frame.Left, frame.Bottom - frame.Top)

    End Function


    Private Const GwlStyle As Integer = -16
    Private Const GwlExStyle As Integer = -20

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
    Private Shared Function GetWindowLong(handle As IntPtr, index As Integer) As UInteger
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function AdjustWindowRectExForDpi(ByRef rect As NativeRect, style As UInteger, menu As Boolean, extendedStyle As UInteger, dpi As UInteger) As Boolean
    End Function


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


    ' Every visible child, lists among them (InspectRows), clipped by its
    ' parents down to the window's client area, except inside a container
    ' that scrolls, whose content is reached by scrolling and so is
    ' measured against its scroll range: what lies past that range cannot
    ' be reached. Each page of a tab control is shown in turn so its
    ' content is measured too, against the tabs' clip read again once the
    ' page shows: showing it can move the focus and scroll a body around
    ' the tabs (ClipOf).
    Private Shared Sub InspectChildren(parent As Control, clip As Rectangle, cut As List(Of String))

        Dim tabs As TabControl = TryCast(parent, TabControl)

        If tabs IsNot Nothing Then

            Dim selected As TabPage = tabs.SelectedTab

            For index As Integer = 0 To tabs.TabCount - 1
                tabs.SelectedIndex = index
                Application.DoEvents()
                Dim page As TabPage = tabs.TabPages(index)
                InspectChildren(page, Rectangle.Intersect(ClipOf(tabs), page.RectangleToScreen(page.ClientRectangle)), cut)
            Next

            tabs.SelectedTab = selected
            Application.DoEvents()
            Return

        End If

        For Each child As Control In parent.Controls

            If Not child.Visible Then
                Continue For
            End If

            ' Before the size: a list squeezed to no height is reported too,
            ' and so is any other control squeezed to a line or to nothing.
            If Not InspectRows(child, clip, cut) Then
                InspectSize(child, cut)
            End If

            If child.Width <= 0 OrElse child.Height <= 0 Then
                Continue For
            End If

            Dim bounds As Rectangle = parent.RectangleToScreen(child.Bounds)

            InspectControl(child, bounds, clip, cut)

            InspectChildren(child, ChildClip(child, clip), cut)

        Next

    End Sub


    ' A child's clip within its parent's: its scroll range if it scrolls,
    ' or its client area inside the parent's clip.
    Private Shared Function ChildClip(child As Control, clip As Rectangle) As Rectangle

        Dim scrolls As ScrollableControl = TryCast(child, ScrollableControl)

        Return If(scrolls IsNot Nothing AndAlso scrolls.AutoScroll,
                  ScrollRange(scrolls),
                  Rectangle.Intersect(clip, child.RectangleToScreen(child.ClientRectangle)))

    End Function


    ' A control's clip read afresh from its window down, as InspectChildren
    ' narrows it on the way, a tab page inside its tabs' clip. At a real
    ' 125% display, showing Submission Details' Correspondence & Files page
    ' scrolled the body around the tabs after the measure had read their
    ' clip, and the page's first line was reported clipped (#93).
    Private Shared Function ClipOf(control As Control) As Rectangle

        Dim chain As New List(Of Control)()
        Dim current As Control = control

        While current IsNot Nothing
            chain.Insert(0, current)
            current = current.Parent
        End While

        Dim clip As Rectangle = chain(0).RectangleToScreen(chain(0).ClientRectangle)

        For Each inner As Control In chain.Skip(1)
            clip =
                If(TypeOf inner Is TabPage,
                   Rectangle.Intersect(clip, inner.RectangleToScreen(inner.ClientRectangle)),
                   ChildClip(inner, clip))
        Next

        Return clip

    End Function


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


    ' A list, list view, grid, tree view or multi-line text box that shows
    ' less than two of its rows (#93, step 4): what is seen of it is its
    ' client area inside its parents and the window, or in reach of a
    ' scroll, and one squeezed to no height shows none. A row is a list
    ' box's or a tree view's item height; a list view's first item, or a
    ' line of its font while it has none, below the details view's column
    ' header (the first item's top, or one row while it has none); a grid's
    ' first row, or its row template while it has none, below its column
    ' headers; and a line of a text box's font. Whether the control is one
    ' of these.
    Private Shared Function InspectRows(control As Control, clip As Rectangle, cut As List(Of String)) As Boolean

        Dim kind As String
        Dim row As Integer
        Dim above As Integer = 0

        If TypeOf control Is ListBox Then

            kind = "list"
            row = DirectCast(control, ListBox).ItemHeight

        ElseIf TypeOf control Is ListView Then

            Dim items As ListView = DirectCast(control, ListView)
            Dim first As Rectangle = If(items.Items.Count > 0, items.GetItemRect(0), Rectangle.Empty)

            kind = "list view"
            row = If(first.Height > 0, first.Height, TextRenderer.MeasureText("Xg", items.Font, New Size(Integer.MaxValue, Integer.MaxValue), CaptionFlags).Height)

            If items.View = View.Details AndAlso items.HeaderStyle <> ColumnHeaderStyle.None Then
                above = If(first.Height > 0, Math.Max(0, first.Top), row)
            End If

        ElseIf TypeOf control Is DataGridView Then

            Dim grid As DataGridView = DirectCast(control, DataGridView)

            kind = "grid"
            row = If(grid.Rows.Count > 0, grid.Rows(0).Height, grid.RowTemplate.Height)
            above = If(grid.ColumnHeadersVisible, grid.ColumnHeadersHeight, 0)

        ElseIf TypeOf control Is TreeView Then

            kind = "tree view"
            row = DirectCast(control, TreeView).ItemHeight

        ElseIf TypeOf control Is TextBoxBase AndAlso DirectCast(control, TextBoxBase).Multiline Then

            kind = "text box"
            row = control.Font.Height

        Else

            Return False

        End If

        Dim seen As Rectangle =
            Rectangle.Intersect(clip, control.RectangleToScreen(control.ClientRectangle))

        If seen.Height - above < 2 * row Then
            cut.Add(kind & Named(control) & " under two rows")
        End If

        Return True

    End Function


    ' Every visible control has a few pixels each way (#255). At a real
    ' 125% About's logo was 130x1 pixels and the version date had no
    ' width, and the measure, which judged only captions and lists, read
    ' "ok". What is designed smaller is not reported: a rule or a spacer,
    ' a panel with nothing in it (the Board card's divider is 1 pixel
    ' tall), and a label with nothing to say yet, which sizes itself to no
    ' width.
    Private Const FewestPixels As Integer = 4

    Private Shared Sub InspectSize(control As Control, cut As List(Of String))

        If control.Width >= FewestPixels AndAlso control.Height >= FewestPixels Then
            Return
        End If

        If (control.GetType() Is GetType(Panel) AndAlso control.Controls.Count = 0) OrElse
           (TypeOf control Is Label AndAlso String.IsNullOrEmpty(control.Text)) Then
            Return
        End If

        cut.Add(control.GetType().Name & Named(control) & " collapsed to " & control.Width & "x" & control.Height)

    End Sub


    ' A control's accessible name, or its name, quoted after a space; empty
    ' when it has neither.
    Private Shared Function Named(control As Control) As String
        Dim name As String = If(String.IsNullOrWhiteSpace(control.AccessibleName), control.Name, control.AccessibleName)
        Return If(String.IsNullOrWhiteSpace(name), String.Empty, " '" & Abbreviate(name) & "'")
    End Function


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

        Dim source As String = SourceOf(view, app)
        Dim winForms As Boolean = source.Contains("AutoScaleDimensions = New SizeF(96.0F, 96.0F)", StringComparison.Ordinal) OrElse source.Contains("DialogScaling.Begin(", StringComparison.Ordinal)
        Dim hand As Boolean = Regex.IsMatch(source, "DeviceDpi|LogicalToDeviceUnits")

        Return (If(winForms AndAlso hand, "both", If(winForms, "Windows Forms", If(hand, "by hand", "none"))), winForms)

    End Function


    ' Whether the window keeps the client size it was given at a real
    ' display, rather than its outer size: its source sets its ClientSize
    ' and never its Size or Bounds. Windows Forms keeps whichever a window
    ' set, with the frame the display draws around it (Measure). A window
    ' that sets neither keeps the outer size every form starts with, and
    ' so does a probe built in a test, which has no source here.
    Friend Shared Function KeepsItsClientSize(view As Type) As Boolean

        Dim source As String = SourceOf(view, Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline"))

        Return Regex.IsMatch(source, "^\s*(Me\.)?ClientSize\s*=", RegexOptions.Multiline) AndAlso
               Not Regex.IsMatch(source, "^\s*(Me\.)?(Size|Bounds)\s*=", RegexOptions.Multiline)

    End Function


    ' A window's files and its base forms', without comments.
    Private Shared Function SourceOf(view As Type, app As String) As String

        Dim code As New StringBuilder()
        Dim current As Type = view

        While current IsNot Nothing AndAlso current IsNot GetType(Form)

            Dim folder As String = If(current Is GetType(Form1), app, Path.Combine(app, "Forms"))

            For Each file As String In Directory.GetFiles(folder, current.Name & "*.vb")
                code.AppendLine(String.Join(Environment.NewLine, IO.File.ReadAllLines(file).Where(Function(line) Not line.TrimStart().StartsWith("'"c))))
            Next

            current = current.BaseType

        End While

        Return code.ToString()

    End Function


    ' ---------------------------------------------------------------
    ' The table
    ' ---------------------------------------------------------------

    Private Shared Function TableOf(rows As IEnumerable(Of WindowRow)) As String

        Dim columns As List(Of String) =
            Dpis.SelectMany(Function(dpi) ScreensAt(dpi).Reverse().Select(Function(screen) screen.Name & " " & dpi)).ToList()

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
