Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Text that Windows or WinForms colors without asking the theme stays
' readable in Dark: a list's group headings, and the captions of disabled
' check boxes, options, and buttons. In Light both are left as they were.
<TestClass>
<DoNotParallelize>
Public Class DarkThemeLegibilityTests

    Private Const Carberry As String = "0000-0002-1825-0097"

    ' ---------------------------------------------------------------
    ' Group headings
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub GroupHeadingsInTheCitationsAndFactsListsAreReadableInDark()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using dialog As CitationsUpdateForm = CitationsDialog()
                    ShowOffscreen(dialog)
                    dialog.LookUpAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    AssertHeadingsReadable(dialog.WorksList, 3)
                End Using

                Using editor As New JournalEditForm(JournalWithFacts())
                    ShowOffscreen(editor)
                    Dim page As TabPage = Descendants(editor).OfType(Of TabPage)().Single(Function(item) item.Text = "Facts and Metrics")
                    DirectCast(page.Parent, TabControl).SelectedTab = page
                    Application.DoEvents()
                    AssertHeadingsReadable(editor.FactsList, 2)
                End Using
            End Sub)

    End Sub

    ' In Light the list is the one Windows draws: the same picture as a
    ' plain list view with the same groups and rows.
    <TestMethod>
    Public Sub AGroupedListIsLeftToWindowsInLight()

        RunWithColorMode(SystemColorMode.Classic,
            Sub()
                Using expected As Bitmap = PictureOfList(New ListView()), actual As Bitmap = PictureOfList(New GroupedListView())
                    AssertSamePicture(expected, actual, "A grouped list in Light")
                End Using
            End Sub)

    End Sub

    ' ---------------------------------------------------------------
    ' Disabled captions
    ' ---------------------------------------------------------------

    ' With and without UiPolish's caption handler, a control paints the same
    ' picture while it is enabled and, in Light, while it is disabled. In
    ' Dark only its disabled caption changes, to the muted text color.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub OnlyADisabledCaptionInDarkIsRecolored(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                Using font As New Font("Segoe UI", 10.0F), form As New Form With {.ClientSize = New Size(520, 260), .Font = font}
                    Dim wrapped As New CheckBox With {
                        .Text = PacketExportForm.AcknowledgeText, .AutoSize = False, .Bounds = New Rectangle(16, 12, 420, 70),
                        .CheckAlign = ContentAlignment.TopLeft, .TextAlign = ContentAlignment.TopLeft, .Checked = True
                    }
                    Dim rightAligned As New CheckBox With {
                        .Text = "Show", .AutoSize = False, .Bounds = New Rectangle(16, 90, 300, 30),
                        .CheckAlign = ContentAlignment.MiddleRight, .TextAlign = ContentAlignment.MiddleRight
                    }
                    Dim choice As New RadioButton With {.Text = "Only &checked works", .AutoSize = True, .Location = New Point(16, 130), .Checked = True}
                    Dim remove As New Button With {.Text = "Remove", .AutoSize = True, .Location = New Point(16, 170)}
                    Dim specimens As ButtonBase() = {wrapped, rightAligned, choice, remove}
                    For Each specimen As ButtonBase In specimens
                        ' The app draws text with GDI; a test host starts with GDI+.
                        specimen.UseCompatibleTextRendering = False
                    Next
                    form.Controls.AddRange(specimens)

                    ' A dialog styles itself, and is styled again once open.
                    UiPolish.ApplyDialog(form)
                    UiPolish.ApplyDialog(form)
                    ShowOffscreen(form)

                    For Each specimen As ButtonBase In specimens
                        For Each enabled As Boolean In {True, False}
                            specimen.Enabled = enabled
                            Dim label As String = $"{specimen.GetType().Name} '{specimen.Text}', {If(enabled, "enabled", "disabled")}, {mode}"
                            Using polished As Bitmap = Picture(specimen)
                                RemoveHandler specimen.Paint, AddressOf UiPolish.PaintMutedCaption
                                Using plain As Bitmap = Picture(specimen)
                                    AddHandler specimen.Paint, AddressOf UiPolish.PaintMutedCaption
                                    If enabled OrElse mode = SystemColorMode.Classic Then
                                        AssertSamePicture(plain, polished, label)
                                    Else
                                        AssertOnlyCaptionRecolored(specimen, plain, polished, label)
                                    End If
                                End Using
                            End Using
                        Next
                    Next
                End Using
            End Sub)

    End Sub

    <TestMethod>
    Public Sub DisabledCaptionsInTheKeyAndCitationsWindowsAreReadableInDark()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using dialog As New OpenAlexKeyForm()
                    ShowOffscreen(dialog)
                    Assert.IsFalse(dialog.AddButton.Enabled, "Add Key waits for a key.")
                    AssertMutedCaption(dialog.AddButton)
                End Using

                Using dialog As CitationsUpdateForm = CitationsDialog()
                    ShowOffscreen(dialog)
                    dialog.LinkedBox.Enabled = False
                    AssertMutedCaption(dialog.LinkedBox)
                End Using
            End Sub)

    End Sub

    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    ' Each heading sits just above its group's first row. Text is judged
    ' by its strongest pixels: smoothing leaves the rest of a stroke paler.
    Private Shared Sub AssertHeadingsReadable(list As ListView, expected As Integer)

        Using image As Bitmap = Picture(list)
            Dim frame As Integer = (list.Width - list.ClientSize.Width) \ 2
            Dim shown As Integer = 0
            For Each group As ListViewGroup In list.Groups
                If group.Items.Count = 0 Then Continue For
                Dim firstRow As Rectangle = group.Items(0).Bounds
                Dim heading As Rectangle = Rectangle.FromLTRB(frame, frame + firstRow.Top - list.Font.Height, frame + list.ClientSize.Width, frame + firstRow.Top - 2)
                Dim ratio As Double = StrongestContrast(image, heading, list.BackColor)
                Assert.IsTrue(ratio >= 4.5, $"A group heading needs 4.5:1 contrast; '{group.Header}' has {ratio:F2}:1 on {list.BackColor}.")
                shown += 1
            Next
            Assert.AreEqual(expected, shown, "Every group has rows, so every heading shows.")
        End Using

    End Sub

    ' The caption can be read, and none of it is left in the near-black
    ' WinForms gives a disabled caption on a dark surface.
    Private Shared Sub AssertMutedCaption(control As ButtonBase)

        ' The app draws text with GDI; a test host starts with GDI+.
        control.UseCompatibleTextRendering = False

        Dim surface As Color = control.BackColor

        Using image As Bitmap = Picture(control)
            Dim area As Rectangle = CaptionArea(control)
            Dim ratio As Double = StrongestContrast(image, area, surface)
            Assert.IsTrue(ratio >= 3.0, $"A disabled caption needs 3:1 contrast; '{control.Text}' has {ratio:F2}:1 on {surface}.")
            Assert.AreEqual(0, CountPixels(image, area, Function(pixel) IsDarker(pixel, surface)),
                $"None of the disabled caption '{control.Text}' is darker than the surface behind it.")
        End Using

    End Sub

    ' Where a caption can be: inside a button's frame, whose rounded edge
    ' blends into the color behind it, and beside a check box's or option's
    ' box, which is 13 pixels wide at 96 DPI and can have dark pixels too.
    Private Shared Function CaptionArea(control As Control) As Rectangle
        Dim area As New Rectangle(Point.Empty, control.Size)
        If TypeOf control Is Button Then
            Dim frame As Integer = control.LogicalToDeviceUnits(6)
            area.Inflate(-frame, -frame)
            Return area
        End If
        Dim box As Integer = control.LogicalToDeviceUnits(17)
        Dim boxSide As ContentAlignment = If(TypeOf control Is CheckBox, DirectCast(control, CheckBox).CheckAlign, DirectCast(control, RadioButton).CheckAlign)
        If boxSide = ContentAlignment.TopRight OrElse boxSide = ContentAlignment.MiddleRight OrElse boxSide = ContentAlignment.BottomRight Then
            Return Rectangle.FromLTRB(area.Left, area.Top, area.Right - box, area.Bottom)
        End If
        Return Rectangle.FromLTRB(area.Left + box, area.Top, area.Right, area.Bottom)
    End Function

    Private Shared Function IsDarker(pixel As Color, surface As Color) As Boolean
        Return pixel.R <= surface.R AndAlso pixel.G <= surface.G AndAlso pixel.B <= surface.B AndAlso
            (pixel.R < surface.R OrElse pixel.G < surface.G OrElse pixel.B < surface.B)
    End Function

    ' plain is the control as WinForms alone paints it, polished the same
    ' control with UiPolish's handler. Where a caption can be, every pixel
    ' WinForms drew darker than the surface is caption and is now between
    ' the surface and the muted text color, which is where a solid pixel
    ' ends up; no other pixel differs, so the box, its tick, and a button's
    ' frame are untouched.
    Private Shared Sub AssertOnlyCaptionRecolored(control As ButtonBase, plain As Bitmap, polished As Bitmap, label As String)

        Dim surface As Color = control.BackColor
        Dim drawn As Color = ControlPaint.Dark(surface)
        Dim muted As Color = UiTheme.MutedText()
        Dim area As Rectangle = CaptionArea(control)
        Dim caption As Integer = 0

        For y As Integer = 0 To plain.Height - 1
            For x As Integer = 0 To plain.Width - 1
                Dim before As Color = plain.GetPixel(x, y)
                Dim after As Color = polished.GetPixel(x, y)
                If area.Contains(x, y) AndAlso IsDarker(before, surface) Then
                    caption += 1
                    Dim towardMuted As Boolean =
                        after.R >= surface.R AndAlso after.R <= muted.R AndAlso
                        after.G >= surface.G AndAlso after.G <= muted.G AndAlso
                        after.B >= surface.B AndAlso after.B <= muted.B
                    If Not towardMuted Then Assert.Fail($"{label}: the caption pixel at {x},{y} is {after}, not between the surface and the muted text color.")
                    If before.ToArgb() = drawn.ToArgb() AndAlso after.ToArgb() <> muted.ToArgb() Then
                        Assert.Fail($"{label}: the solid caption pixel at {x},{y} is {after}, not the muted text color.")
                    End If
                ElseIf before.ToArgb() <> after.ToArgb() Then
                    Assert.Fail($"{label}: the pixel at {x},{y} is not caption but changed from {before} to {after}.")
                End If
            Next
        Next

        Assert.IsTrue(caption > 0, $"{label}: WinForms alone draws this caption darker than its surface.")
        Dim ratio As Double = StrongestContrast(polished, area, surface)
        Assert.IsTrue(ratio >= 3.0, $"{label}: a disabled caption needs 3:1 contrast; it has {ratio:F2}:1 on {surface}.")

    End Sub

    Private Shared Sub AssertSamePicture(expected As Bitmap, actual As Bitmap, label As String)
        Assert.AreEqual(expected.Size, actual.Size, label)
        For y As Integer = 0 To expected.Height - 1
            For x As Integer = 0 To expected.Width - 1
                If expected.GetPixel(x, y).ToArgb() <> actual.GetPixel(x, y).ToArgb() Then
                    Assert.Fail($"{label}: the pixel at {x},{y} is {actual.GetPixel(x, y)}, not {expected.GetPixel(x, y)}.")
                End If
            Next
        Next
    End Sub

    ' The list alone in a window of its own, with two groups and their rows.
    Private Shared Function PictureOfList(list As ListView) As Bitmap
        Using font As New Font("Segoe UI", 10.0F), form As New Form With {.ClientSize = New Size(480, 240), .Font = font}
            list.Dock = DockStyle.Fill
            list.View = View.Details
            list.CheckBoxes = True
            list.FullRowSelect = True
            list.Columns.Add("Work", 300)
            list.Columns.Add("Year", 80, HorizontalAlignment.Right)
            For Each heading As String In {"On your ORCID record", "Linked to your iD by OpenAlex only (check only works that are yours)"}
                Dim group As New ListViewGroup(heading)
                list.Groups.Add(group)
                list.Items.Add(New ListViewItem({"A work under " & heading, "2025"}) With {.Group = group})
            Next
            form.Controls.Add(list)
            ShowOffscreen(form)
            Return Picture(list)
        End Using
    End Function

    Private Shared Function Picture(control As Control) As Bitmap
        Dim image As New Bitmap(control.Width, control.Height)
        control.DrawToBitmap(image, New Rectangle(Point.Empty, control.Size))
        Return image
    End Function

    ' The most any pixel in the area contrasts with the surface.
    Private Shared Function StrongestContrast(image As Bitmap, area As Rectangle, surface As Color) As Double
        area.Intersect(New Rectangle(Point.Empty, image.Size))
        Dim seen As New HashSet(Of Integer)()
        Dim strongest As Double = 1.0
        For y As Integer = area.Top To area.Bottom - 1
            For x As Integer = area.Left To area.Right - 1
                Dim pixel As Color = image.GetPixel(x, y)
                If seen.Add(pixel.ToArgb()) Then strongest = Math.Max(strongest, ContrastRatio(pixel, surface))
            Next
        Next
        Return strongest
    End Function

    Private Shared Function CountPixels(image As Bitmap, area As Rectangle, matches As Func(Of Color, Boolean)) As Integer
        area.Intersect(New Rectangle(Point.Empty, image.Size))
        Dim count As Integer = 0
        For y As Integer = area.Top To area.Bottom - 1
            For x As Integer = area.Left To area.Right - 1
                If matches(image.GetPixel(x, y)) Then count += 1
            Next
        Next
        Return count
    End Function

    ' The update window on a recorded answer with a work in each group.
    Private Shared Function CitationsDialog() As CitationsUpdateForm
        Dim groups As New List(Of OrcidWorkGroup) From {
            New OrcidWorkGroup With {.Dois = New List(Of String) From {"10.5555/test.orcid"}, .Title = "A work on the ORCID record", .WorkType = "journal-article"}
        }
        Dim found As New Dictionary(Of String, CitedWork) From {
            {"10.5555/test.orcid", Work("W1", "10.5555/test.orcid", "A work on the ORCID record")},
            {"10.5555/test.manuscript", Work("W2", "10.5555/test.manuscript", "A published manuscript")}
        }
        Dim linked As New List(Of CitedWork) From {Work("W3", "10.5555/test.linked", "A work OpenAlex links to the iD")}
        Dim manuscripts As New List(Of (Doi As String, Title As String)) From {("10.5555/test.manuscript", "A published manuscript")}
        Dim lookup As CitationLookup = CitationsService.Assemble(Carberry, groups, manuscripts, found, linked, 1, Array.Empty(Of String)())
        Return New CitationsUpdateForm(Carberry, manuscripts, Nothing, New RecordedCitations(lookup))
    End Function

    Private Shared Function Work(id As String, doi As String, title As String) As CitedWork
        Return New CitedWork With {.OpenAlexId = id, .Doi = doi, .Title = title, .Year = 2025, .Journal = "Fictional Journal", .CitedByCount = 3}
    End Function

    ' A journal with a fact from an open index and a metric entered by hand.
    Private Shared Function JournalWithFacts() As JournalRecord
        Dim record As New JournalRecord With {.Name = "Fictional Journal"}
        record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.OpenAccess, .Value = "Open access", .Source = JournalFactCatalog.DoajSource, .CheckedUtc = New DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc)})
        record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "5.2", .Year = 2025, .Source = "Scopus", .EnteredByYou = True})
        Return record
    End Function

    Private NotInheritable Class RecordedCitations
        Implements ICitationSource

        Private ReadOnly _lookup As CitationLookup

        Public Sub New(lookup As CitationLookup)
            _lookup = lookup
        End Sub

        Public Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                    excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync
            Return Task.FromResult(_lookup)
        End Function
    End Class

    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        ' A window that centers itself when shown goes back offscreen.
        form.Location = New Point(-20000, -20000)
        Application.DoEvents()
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Function ContrastRatio(foreground As Color, background As Color) As Double
        Dim first As Double = Luminance(foreground)
        Dim second As Double = Luminance(background)
        Return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05)
    End Function

    Private Shared Function Luminance(color As Color) As Double
        Return 0.2126 * LinearChannel(color.R) + 0.7152 * LinearChannel(color.G) + 0.0722 * LinearChannel(color.B)
    End Function

    Private Shared Function LinearChannel(channel As Byte) As Double
        Dim value As Double = channel / 255.0
        Return If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))
    End Function

    ' Changes the color mode of the test process only, on a thread of its
    ' own, and puts it back; no form is left open.
    Private Shared Sub RunWithColorMode(mode As SystemColorMode, action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Assert.AreEqual(0, Application.OpenForms.Count, "Theme tests require no existing forms in the test process.")
                    If SystemInformation.HighContrast Then
                        Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
                    End If
                    If mode = SystemColorMode.Dark AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
                        Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
                    End If
                    Application.EnableVisualStyles()
                    Application.SetColorMode(mode)
                    Assert.AreEqual(mode = SystemColorMode.Dark, UiTheme.IsDark())
                    action()
                Catch ex As Exception
                    failure = ex
                Finally
                    Try
                        Application.SetColorMode(priorMode)
                    Catch ex As Exception
                        If failure Is Nothing Then failure = ex
                    End Try
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90)), "The dark theme legibility test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
