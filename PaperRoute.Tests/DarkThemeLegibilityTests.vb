Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Text that Windows or WinForms colors without asking the theme stays
' readable in Dark: a list's group headings, and the captions of disabled
' check boxes, options, and buttons. In Light both are left as they were.
' And no view in the catalog keeps light colors in Dark (#106), but for
' what the allow-list names.
<TestClass>
<DoNotParallelize>
Public Class DarkThemeLegibilityTests

    ' What stays as Windows draws it in Dark for 1.0, by name: the residual
    ' list (#106, plan item 57), with the words the guide's Accessibility
    ' section names it in (#139). The sweep passes over what an entry
    ' matches and nothing else. An entry that matches nothing names what
    ' the sweep cannot judge: frames, borders, fills and links, which are
    ' never most of a control. Matching those by type would pass over the
    ' whole control, a grid's check marks with its frame. Message boxes and
    ' the file dialogs left the list when the dialog hook gave them the
    ' theme's title bar (#188); the rest of them is Windows' own, as the
    ' guide says above its list.
    Private Shared ReadOnly DarkAllowList As (Name As String, Reason As String, Guide As String, Matches As Func(Of Control, Boolean))() = {
        ("The report preview", "A web page that Windows' browser control draws.", "The report preview", Function(control As Control) TypeOf control Is WebBrowser),
        ("Grid frames", "WinForms draws a white frame one pixel wide round a grid; UiPolish leaves the grid's border as it is.", "The thin white frame around a table", Function(control As Control) False),
        ("Combo and number box borders", "Windows draws them in greys of its own.", "The borders of drop-down lists and number boxes", Function(control As Control) False),
        ("The route map's fills", "The journal and author segments and the numbered events keep their Light fills in Dark; their white labels keep 4.5:1.", "the journal and author segments and the numbered events", Function(control As Control) False),
        ("Help's links", "The guide's links are in the color Windows' rich text box gives links.", "The links in this guide's window", Function(control As Control) False),
        ("Input borders", "Text boxes and lists are outlined in the quiet card border, under the 3:1 an input's outline should have.", "The outlines of text boxes and lists", Function(control As Control) False)
    }

    ' ---------------------------------------------------------------
    ' Every view in Dark (#106)
    ' ---------------------------------------------------------------

    ' Each view in the catalog, shown in Dark: no control is mostly a light
    ' color, apart from what its children cover; no check mark in a list or
    ' grid is; and no button is a standard Windows button, whose face is
    ' dark in Dark but whose caption stays light when it is disabled. What
    ' the allow-list names is passed over. Every finding is listed.
    <TestMethod>
    Public Sub NoViewKeepsLightColorsInDark()

        SkipWhereThemesCannotBeChecked(SystemColorMode.Dark)

        Dim findings As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach(ViewCatalog.AllViewsAndStages(),
            Sub(name, shown) findings.AddRange(LightPartsOf(shown).Select(Function(part) name & ": " & part)),
            mode:=SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, findings.Count, Environment.NewLine & String.Join(Environment.NewLine, findings))

    End Sub

    ' The sweep has teeth: in Dark, a light panel, a list's and a grid's
    ' stock check marks, and a standard button are found, and the same
    ' window with a dark panel and none of the others shows nothing.
    <TestMethod>
    Public Sub TheDarkSweepFindsLightControlsAndStockCheckMarks()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using form As New Form With {.ClientSize = New Size(640, 300), .Font = New Font("Segoe UI", 9.0F)}
                    Dim light As New Panel With {.Name = "Light panel", .Bounds = New Rectangle(8, 8, 120, 60), .BackColor = Color.White}
                    Dim list As New ListView With {.Name = "Probe list", .View = View.Details, .CheckBoxes = True, .Bounds = New Rectangle(140, 8, 240, 120),
                                                   .BackColor = UiTheme.CardBackground(), .ForeColor = UiTheme.PrimaryText()}
                    list.Columns.Add("Work", 200)
                    list.Items.Add(New ListViewItem("Unchecked"))
                    Dim grid As New DataGridView With {.Name = "Probe grid", .Bounds = New Rectangle(390, 8, 240, 120), .AllowUserToAddRows = False}
                    grid.Columns.Add(New DataGridViewCheckBoxColumn With {.HeaderText = "Use"})
                    grid.Rows.Add(False)
                    form.Controls.Add(light)
                    UiPolish.ApplyDialog(form)
                    ' Added once the window is styled, so Windows draws them.
                    Dim standard As New Button With {.Name = "Probe button", .Text = "Standard", .Location = New Point(8, 140), .AutoSize = True}
                    form.Controls.AddRange(New Control() {list, grid, standard})
                    ShowOffscreen(form)

                    Dim found As List(Of String) = LightPartsOf(form, everything:=True)
                    For Each expected As String In {"Panel 'Light panel' is mostly", "ListView 'Probe list' has a check mark that is mostly", "DataGridView 'Probe grid' has a check mark that is mostly", "Button 'Probe button' is a standard Windows button"}
                        Assert.IsTrue(found.Any(Function(item) item.StartsWith(expected, StringComparison.Ordinal)), $"Found '{expected}...' among: {String.Join("; ", found)}")
                    Next
                    Assert.AreEqual(4, found.Count, String.Join("; ", found))

                    light.BackColor = UiTheme.CardBackground()
                    list.Dispose()
                    grid.Dispose()
                    standard.Dispose()
                    Application.DoEvents()
                    Assert.AreEqual(0, LightPartsOf(form, everything:=True).Count, String.Join("; ", LightPartsOf(form, everything:=True)))
                End Using
            End Sub)

    End Sub

    ' The guide's Accessibility section names exactly what the allow-list
    ' leaves to Windows in Dark (#139): one line of its list for each
    ' entry, in the entry's words, and no other line.
    <TestMethod>
    Public Sub TheGuideNamesEachDarkSurfaceLeftToWindows()

        Dim guide As String = File.ReadAllText(UserGuideService.GuideFilePath()).Replace(vbCrLf, vbLf)
        Dim heading As Integer = guide.IndexOf(vbLf & "### What Windows draws" & vbLf, StringComparison.Ordinal)
        Assert.IsTrue(heading >= 0, "The Accessibility section says what Windows draws.")

        Dim [next] As Integer = guide.IndexOf(vbLf & "#", heading + 1, StringComparison.Ordinal)
        Dim lines As List(Of String) = guide.Substring(heading, If([next] < 0, guide.Length, [next]) - heading).
            Split(ChrW(10)).
            Where(Function(line) line.StartsWith("- ", StringComparison.Ordinal)).
            ToList()

        Assert.AreEqual(DarkAllowList.Length, lines.Count, "A line for each entry: " & String.Join(" | ", lines))
        For Each entry In DarkAllowList
            Assert.AreEqual(1, lines.Where(Function(line) line.Contains(entry.Guide, StringComparison.Ordinal)).Count(), $"The guide names {entry.Name} on one line, as '{entry.Guide}'.")
        Next

    End Sub

    ' ---------------------------------------------------------------
    ' Buttons that escape the theme (#106)
    ' ---------------------------------------------------------------

    ' UiPolish never styles the main window, so every button on its pages
    ' paints itself from the tokens: over the board, every page, and Your
    ' Citations with and without a saved update, no Button outside the
    ' windows the pages host is anything but an ActionButton, apart from
    ' the rail's commands, which the rail paints.
    <TestMethod>
    Public Sub EveryButtonOnTheMainWindowsPagesIsAnActionButton()

        SkipWhereThemesCannotBeChecked(SystemColorMode.Dark)

        ' A dialog keyed by a Form subclass of its own never builds the main
        ' window; the hosted views and the stages may.
        Dim views = ViewCatalog.AllViewsAndStages().Where(Function(view) view.Name = NameOf(Form1) OrElse Not ViewCatalog.Dialogs.Keys.Any(Function(type) type.Name = view.Name))
        Dim boards As Integer = 0
        Dim standard As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach(views,
            Sub(name, shown)
                If Not TypeOf shown Is Form1 Then Return
                boards += 1
                For Each button As Button In OwnControls(shown).OfType(Of Button)()
                    If Not TypeOf button Is ActionButton AndAlso Not TypeOf button Is RailCommandButton Then
                        standard.Add($"{name}: {Describe(button)}")
                    End If
                Next
            End Sub,
            mode:=SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.IsTrue(boards >= 10, $"The board, its pages and Your Citations were looked at ({boards}).")
        Assert.AreEqual(0, standard.Count, Environment.NewLine & String.Join(Environment.NewLine, standard))

    End Sub

    ' The shortlist builds its rows again after the manuscript page was
    ' styled, and its rows' buttons are styled as the page's are: flat, on
    ' the card color. The view is the main window, which the shared loop
    ' does not style, so only RefreshShortlist can have styled them.
    <TestMethod>
    Public Sub TheShortlistsRebuiltRowsAreStyled()

        SkipWhereThemesCannotBeChecked(SystemColorMode.Dark)

        Dim view = ViewCatalog.Stages.Single(Function(item) item.Name = "EditManuscriptForm as the manuscript page, after its shortlist is rebuilt")
        Dim names As String() = {"Edit PLOS ONE", "More actions for PLOS ONE", "Edit Collabra: Psychology", "More actions for Collabra: Psychology"}
        Dim found As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach({(view.Name, view.Build)},
            Sub(name, shown)
                If Not UiTheme.IsDark() Then found.Add("The view is not shown in Dark.")
                Dim rows As List(Of Button) = Descendants(shown).OfType(Of Button)().Where(Function(button) names.Contains(button.AccessibleName)).ToList()
                If rows.Count <> 4 Then found.Add($"{rows.Count} of the four buttons of two rows, each with Edit... and More actions.")
                For Each button As Button In rows
                    If button.FlatStyle <> FlatStyle.Flat Then found.Add($"{Describe(button)} is not styled.")
                    If button.BackColor <> UiTheme.CardBackground() Then found.Add($"{Describe(button)} is on {button.BackColor}, not the card color.")
                Next
            End Sub,
            mode:=SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub

    ' A window's controls, but not those of the windows it hosts.
    Private Shared Iterator Function OwnControls(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            If TypeOf child Is Form Then Continue For
            Yield child
            For Each descendant As Control In OwnControls(child)
                Yield descendant
            Next
        Next
    End Function

    ' ---------------------------------------------------------------
    ' Date pickers (#106)
    ' ---------------------------------------------------------------

    ' The views with the app's nine date pickers.
    Private Shared ReadOnly DatePickerViews As String() = {
        "AddCorrespondenceForm", "AddDecisionForm", "AddManuscriptForm", "AddSubmissionForm",
        "DeadlineDateForm", "ManuscriptVersionEditForm", "ReminderEditForm"
    }

    ' In Dark each date field is mostly the card color, whether it is on or
    ' off, and the text of one that is on has 4.5:1 on it at least: judged
    ' where the date is written, not on the drop-down button or the frame.
    <TestMethod>
    Public Sub DatePicker_FieldsAreTheCardColorInDark()

        SkipWhereThemesCannotBeChecked(SystemColorMode.Dark)

        Dim pickers As New List(Of String)()
        Dim found As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach(ViewCatalog.AllViewsAndStages().Where(Function(item) DatePickerViews.Contains(item.Name)),
            Sub(name, shown)
                For Each picker As DateTimePicker In Descendants(shown).OfType(Of DateTimePicker)()
                    pickers.Add($"{name}: {Describe(picker)}{If(picker.Visible, String.Empty, " (hidden)")}{If(picker.Enabled, String.Empty, " (off)")}")
                    If Not picker.Visible Then Continue For
                    Using image As Bitmap = Picture(picker)
                        Dim field As Color = CommonestColor(image)
                        If field.ToArgb() <> UiTheme.CardBackground().ToArgb() Then
                            found.Add($"{name}: {Describe(picker)} is mostly {field}.")
                        End If
                        Dim ratio As Double = StrongestContrast(image, DateTextArea(picker), UiTheme.CardBackground())
                        If picker.Enabled AndAlso ratio < 4.5 Then
                            found.Add($"{name}: {Describe(picker)}'s text has {ratio:F2}:1 on the card color.")
                        End If
                    End Using
                Next
            End Sub,
            mode:=SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(9, pickers.Count, "The nine date pickers: " & String.Join("; ", pickers))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub

    ' The field keeps Windows' own editing in Dark: the Up arrow changes the
    ' part of the date being edited, and the field is still the card color.
    ' With the focus, the part being edited keeps Windows' highlight. On
    ' screen the field is painted for WM_PAINT, which DrawToBitmap never
    ' sends: handed a device context, as Windows may hand it one, the paint
    ' gives the card color too. The paint without one, which draws on the
    ' window itself, is left to the manual captures.
    <TestMethod>
    Public Sub DatePicker_KeepsWindowsEditingInDark()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using form As New Form With {.ClientSize = New Size(320, 120), .Font = New Font("Segoe UI", 9.0F)}
                    Dim picker As New ThemedDatePicker With {.Format = DateTimePickerFormat.Short, .Bounds = New Rectangle(8, 8, 200, 30), .Value = New DateTime(2026, 10, 15)}
                    form.Controls.Add(picker)
                    UiPolish.ApplyDialog(form)
                    ShowOffscreen(form)

                    picker.Focus()
                    SendMessage(picker.Handle, WM_KEYDOWN, New IntPtr(VK_UP), IntPtr.Zero)
                    SendMessage(picker.Handle, WM_KEYUP, New IntPtr(VK_UP), IntPtr.Zero)
                    Application.DoEvents()

                    Assert.AreNotEqual(New DateTime(2026, 10, 15), picker.Value.Date, "The Up arrow changes the date.")
                    Using image As Bitmap = Picture(picker)
                        Assert.AreEqual(UiTheme.CardBackground().ToArgb(), CommonestColor(image).ToArgb(), "The field is the card color.")
                        Dim highlight As Integer = ColorTranslator.FromWin32(GetSysColor(COLOR_HIGHLIGHT)).ToArgb()
                        Dim marked As Integer = CountPixels(image, New Rectangle(Point.Empty, image.Size), Function(pixel) pixel.ToArgb() = highlight)
                        Assert.AreEqual(picker.Focused, marked > 0, $"The part being edited is highlighted exactly when the field has the focus (focused: {picker.Focused}).")
                    End Using
                    Using painted As New Bitmap(picker.ClientSize.Width, picker.ClientSize.Height)
                        Using g As Graphics = Graphics.FromImage(painted)
                            Dim dc As IntPtr = g.GetHdc()
                            Try
                                SendMessage(picker.Handle, WM_PAINT, dc, IntPtr.Zero)
                            Finally
                                g.ReleaseHdc(dc)
                            End Try
                        End Using
                        Assert.AreEqual(UiTheme.CardBackground().ToArgb(), CommonestColor(painted).ToArgb(), "Painted as on screen, the field is the card color.")
                    End Using
                End Using
            End Sub)

    End Sub

    ' In Light a themed date picker makes the same picture as a plain one,
    ' on and off. A guard: in Light the picker changes nothing, so this
    ' cannot fail unless that changes.
    <TestMethod>
    Public Sub DatePicker_LeavesTheFieldToWindowsInLight()

        RunWithColorMode(SystemColorMode.Classic,
            Sub()
                For Each enabled As Boolean In {True, False}
                    Using plain As Bitmap = DatePickerPicture(New DateTimePicker(), enabled), themed As Bitmap = DatePickerPicture(New ThemedDatePicker(), enabled)
                        AssertSamePicture(plain, themed, $"A date picker in Light, {If(enabled, "on", "off")}")
                    End Using
                Next
            End Sub)

    End Sub

    ' A picker alone in a window of its own, on a fixed date.
    Private Shared Function DatePickerPicture(picker As DateTimePicker, enabled As Boolean) As Bitmap
        Using font As New Font("Segoe UI", 9.0F), form As New Form With {.ClientSize = New Size(320, 120), .Font = font}
            picker.Format = DateTimePickerFormat.Short
            picker.Bounds = New Rectangle(8, 8, 200, 30)
            picker.Value = New DateTime(2026, 10, 15)
            picker.Enabled = enabled
            form.Controls.Add(picker)
            ShowOffscreen(form)
            Return Picture(picker)
        End Using
    End Function

    ' Where a date field's text is in its picture: the client area inside
    ' its frame, left of the drop-down button Windows reports.
    Private Shared Function DateTextArea(picker As DateTimePicker) As Rectangle
        Dim info As New DATETIMEPICKERINFO With {.cbSize = Runtime.InteropServices.Marshal.SizeOf(Of DATETIMEPICKERINFO)()}
        SendMessage(picker.Handle, DTM_GETDATETIMEPICKERINFO, IntPtr.Zero, info)
        Dim frame As Integer = picker.LogicalToDeviceUnits(2)
        Dim area As Rectangle = Rectangle.FromLTRB(frame, frame, info.ButtonLeft, picker.ClientSize.Height - frame)
        area.Offset(ClientOffset(picker))
        Return area
    End Function

    ' The color of most pixels in a picture.
    Private Shared Function CommonestColor(image As Bitmap) As Color
        Return Color.FromArgb(PixelsOf(image).GroupBy(Function(pixel) pixel).OrderByDescending(Function(group) group.Count()).First().Key)
    End Function

    ' ---------------------------------------------------------------
    ' Check marks in lists and grids (#106)
    ' ---------------------------------------------------------------

    ' The views that show the four lists and the six grid columns with
    ' check boxes, at the step that fills them.
    Private Shared ReadOnly CheckBoxViews As String() = {
        "PacketExportForm",
        "CitationsUpdateForm, confirming the works it found",
        "JournalFactsForm, with the changes it found",
        "JournalSuggestionsForm, with the journals it found",
        "FillBlanksForm, with the blanks it can fill",
        "DecisionLetterForm, reviewing the comments it read",
        "AuthorInstructionsForm, reviewing the requirements it read",
        "TitlePageImportForm, with the authors it read"
    }

    ' In Dark each of their check marks is drawn from the tokens: mostly
    ' the accent when checked, and the card color inside the secondary
    ' outline when not, never a light color; in a list or grid that is off,
    ' as the packet's is while its files are checked, the muted color. Fill
    ' Blanks' list has the card colors as the other lists do.
    <TestMethod>
    Public Sub CheckGlyph_MarksInListsAndGridsFollowTheTokensInDark()

        SkipWhereThemesCannotBeChecked(SystemColorMode.Dark)

        Dim lists As Integer = 0
        Dim columns As Integer = 0
        Dim found As New List(Of String)()

        Dim failures As List(Of String) = ViewCatalog.ShowEach(ViewCatalog.AllViewsAndStages().Where(Function(item) CheckBoxViews.Contains(item.Name)),
            Sub(name, shown)
                For Each control As Control In Descendants(shown).Where(Function(item) item.Visible AndAlso CheckMarks(item).Any())
                    If TypeOf control Is ListView Then
                        lists += 1
                        If control.BackColor <> UiTheme.CardBackground() OrElse control.ForeColor <> UiTheme.PrimaryText() Then
                            found.Add($"{name}: {Describe(control)} is {control.ForeColor} on {control.BackColor}.")
                        End If
                    Else
                        columns += DirectCast(control, DataGridView).Columns.OfType(Of DataGridViewCheckBoxColumn)().Count()
                    End If
                    Using image As Bitmap = Picture(control)
                        Dim client As Point = ClientOffset(control)
                        For Each mark In CheckMarks(control)
                            Dim cell As Rectangle = mark.Cell
                            cell.Offset(client)
                            Dim color As Color? = MarkColor(image, cell)
                            Dim expected As Color() =
                                If(Not control.Enabled, {UiTheme.MutedText(), UiTheme.CardBackground()},
                                   If(mark.Checked, {UiTheme.AccentColor()}, {UiTheme.SecondaryText(), UiTheme.CardBackground()}))
                            If Not color.HasValue OrElse Not expected.Any(Function(item) item.ToArgb() = color.Value.ToArgb()) Then
                                found.Add($"{name}: {Describe(control)} has a {If(mark.Checked, "checked", "clear")} mark that is mostly {color}.")
                            End If
                        Next
                    End Using
                Next
            End Sub,
            mode:=SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(4, lists, "The four lists with check boxes were judged.")
        Assert.AreEqual(6, columns, "The six grid columns with check boxes were judged.")

    End Sub

    ' The list keeps Windows' own checking: Space checks the focused row,
    ' and its mark is then the accent.
    <TestMethod>
    Public Sub CheckGlyph_SpaceStillChecksAListRowInDark()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using form As New Form With {.ClientSize = New Size(320, 160), .Font = New Font("Segoe UI", 9.0F)}
                    Dim list As New ListView With {.View = View.Details, .CheckBoxes = True, .Dock = DockStyle.Fill,
                                                   .BackColor = UiTheme.CardBackground(), .ForeColor = UiTheme.PrimaryText()}
                    list.Columns.Add("Work", 200)
                    list.Items.Add(New ListViewItem("A work"))
                    form.Controls.Add(list)
                    UiPolish.ApplyDialog(form)
                    ShowOffscreen(form)

                    list.Items(0).Focused = True
                    SendMessage(list.Handle, WM_KEYDOWN, New IntPtr(VK_SPACE), IntPtr.Zero)
                    SendMessage(list.Handle, WM_KEYUP, New IntPtr(VK_SPACE), IntPtr.Zero)
                    Application.DoEvents()

                    Assert.IsTrue(list.Items(0).Checked, "Space checks the focused row.")
                    Using image As Bitmap = Picture(list)
                        Dim mark = CheckMarks(list).Single()
                        Dim cell As Rectangle = mark.Cell
                        cell.Offset(ClientOffset(list))
                        Assert.AreEqual(UiTheme.AccentColor().ToArgb(), MarkColor(image, cell).GetValueOrDefault().ToArgb(), "The checked mark is mostly the accent.")
                    End Using
                End Using
            End Sub)

    End Sub

    ' In Light a list and a grid with the painter attached make the same
    ' picture as plain ones, checked and clear. A guard: the painter does
    ' nothing in Light, so this cannot fail unless that changes.
    <TestMethod>
    Public Sub CheckGlyph_LeavesListsAndGridsToWindowsInLight()

        RunWithColorMode(SystemColorMode.Classic,
            Sub()
                Dim plain As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=False)
                Dim painted As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=True)
                Try
                    AssertSamePicture(plain.List, painted.List, "A list with check boxes in Light")
                    AssertSamePicture(plain.Grid, painted.Grid, "A grid with check boxes in Light")
                Finally
                    plain.List.Dispose()
                    plain.Grid.Dispose()
                    painted.List.Dispose()
                    painted.Grid.Dispose()
                End Try
            End Sub)

    End Sub

    ' ---------------------------------------------------------------
    ' Contrast themes (#138)
    ' ---------------------------------------------------------------

    ' Under a Windows contrast theme, dark or light, what this file's Dark
    ' work draws for itself is left to Windows, which draws in the theme's
    ' colors: IsDark() is never true there. A list and a grid with the
    ' check-mark painter, and a themed date field on and off, make the same
    ' picture as plain ones, and a window's frame is not asked to be dark.
    ' The seam stands in for the theme. The teeth: in Dark without one,
    ' each of the four pictures differs from the plain one. Built under the
    ' theme, the list and grid are never attached, as Attach asks first; so
    ' in Dark a grid is also attached before the theme comes on, with its
    ' window open, and is then painted as Windows paints it, because the
    ' painter asks again at each paint. A list's marks are drawn again only
    ' when Windows makes them anew, on a change of theme the seam does not
    ' send, so no list is judged that way.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub CheckGlyphDatePickerAndFrame_LeaveTheDrawingToWindowsUnderAContrastTheme(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                If mode = SystemColorMode.Dark Then
                    For Each specimen In SameAsPlain()
                        Assert.IsFalse(specimen.Same, $"{specimen.Name} in Dark draws its own.")
                    Next
                End If

                Try
                    If mode = SystemColorMode.Dark Then
                        Dim switched As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=True, whileOpen:=Sub() UiTheme.HighContrastOverride = True)
                        Dim plain As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=False)
                        Try
                            Assert.IsTrue(SamePicture(plain.Grid, switched.Grid), "A grid the painter was attached to in Dark, painted after a contrast theme came on, is as Windows draws it.")
                        Finally
                            switched.List.Dispose()
                            switched.Grid.Dispose()
                            plain.List.Dispose()
                            plain.Grid.Dispose()
                        End Try
                    End If

                    UiTheme.HighContrastOverride = True
                    Assert.IsFalse(UiTheme.IsDark(), "A contrast theme never picks the Dark palette.")

                    For Each specimen In SameAsPlain()
                        Assert.IsTrue(specimen.Same, $"{specimen.Name} under a contrast theme in {mode} is as Windows draws it.")
                    Next

                    Using form As New Form With {.ClientSize = New Size(320, 120)}
                        WindowChrome.Apply(form)
                        ShowOffscreen(form)
                        Assert.IsFalse(WindowChrome.FrameOf(form).Dark, $"Under a contrast theme in {mode} the title bar is Windows' own.")
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Whether a list and a grid with the check-mark painter, and a themed
    ' date field on and off, each make the same picture as a plain one.
    Private Shared Function SameAsPlain() As List(Of (Name As String, Same As Boolean))

        Dim specimens As New List(Of (Name As String, Same As Boolean))()

        Dim plain As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=False)
        Dim painted As (List As Bitmap, Grid As Bitmap) = CheckBoxSpecimens(attach:=True)
        Try
            specimens.Add(("A list with check boxes", SamePicture(plain.List, painted.List)))
            specimens.Add(("A grid with check boxes", SamePicture(plain.Grid, painted.Grid)))
        Finally
            plain.List.Dispose()
            plain.Grid.Dispose()
            painted.List.Dispose()
            painted.Grid.Dispose()
        End Try

        For Each enabled As Boolean In {True, False}
            Using field As Bitmap = DatePickerPicture(New DateTimePicker(), enabled), themed As Bitmap = DatePickerPicture(New ThemedDatePicker(), enabled)
                specimens.Add(($"A date field, {If(enabled, "on", "off")}", SamePicture(field, themed)))
            End Using
        Next

        Return specimens

    End Function

    Private Shared Function SamePicture(expected As Bitmap, actual As Bitmap) As Boolean
        Return expected.Size = actual.Size AndAlso PixelsOf(expected).SequenceEqual(PixelsOf(actual))
    End Function

    ' ---------------------------------------------------------------
    ' Group headings
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub GroupHeadingsInTheCitationsAndFactsListsAreReadableInDark()

        RunWithColorMode(SystemColorMode.Dark,
            Sub()
                Using dialog As CitationsUpdateForm = ViewCatalog.CitationsDialog()
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
                Using font As New Font("Segoe UI", 10.0F), form As New Form With {.ClientSize = New Size(520, 360), .Font = font}
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
                    Dim edit As New Button With {.Text = "Edit...", .AutoSize = True, .Location = New Point(140, 170)}
                    ' A button on a card has its parent's back color, and
                    ' WinForms then fills it in a grey of its own.
                    Dim card As New SectionCard With {.Text = "Authors", .Bounds = New Rectangle(16, 220, 480, 120)}
                    Dim onCard As New Button With {.Text = "Remove", .AutoSize = True, .Location = New Point(16, 50)}
                    Dim row As New FlowLayoutPanel With {.Bounds = New Rectangle(140, 44, 320, 60)}
                    Dim inRow As New Button With {.Text = "Remove", .AutoSize = True}
                    card.Controls.Add(onCard)
                    card.Controls.Add(row)
                    row.Controls.Add(inRow)
                    Dim specimens As ButtonBase() = {wrapped, rightAligned, choice, remove, edit, onCard, inRow}
                    For Each specimen As ButtonBase In specimens
                        ' The app draws text with GDI; a test host starts with GDI+.
                        specimen.UseCompatibleTextRendering = False
                    Next
                    form.Controls.AddRange(New Control() {wrapped, rightAligned, choice, remove, edit, card})

                    ' A dialog styles itself, and is styled again once open.
                    UiPolish.ApplyDialog(form)
                    UiPolish.ApplyDialog(form)
                    ShowOffscreen(form)

                    For Each specimen As ButtonBase In specimens
                        ' Disabled first: that picture is painted with the
                        ' handler UiPolish attached, or with none if it did not.
                        For Each enabled As Boolean In {False, True}
                            specimen.Enabled = enabled
                            Dim label As String = $"{specimen.GetType().Name} '{specimen.Text}' on a {specimen.Parent.GetType().Name}, {If(enabled, "enabled", "disabled")}, {mode}"
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

                Using dialog As CitationsUpdateForm = ViewCatalog.CitationsDialog()
                    ShowOffscreen(dialog)
                    dialog.LinkedBox.Enabled = False
                    AssertMutedCaption(dialog.LinkedBox)
                End Using
            End Sub)

    End Sub

    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    ' What in a shown view keeps light colors, one line for each control,
    ' passing over what the allow-list names unless told to look at all.
    Private Shared Function LightPartsOf(view As Form, Optional everything As Boolean = False) As List(Of String)

        Dim found As New List(Of String)()

        For Each control As Control In {CType(view, Control)}.Concat(Descendants(view))

            If Not control.Visible OrElse control.Width <= 0 OrElse control.Height <= 0 OrElse
               (Not everything AndAlso DarkAllowList.Any(Function(entry) entry.Matches(control))) Then
                Continue For
            End If

            If TypeOf control Is Button AndAlso Not TypeOf control Is ActionButton AndAlso
               DirectCast(control, Button).FlatStyle <> FlatStyle.Flat Then
                found.Add($"{Describe(control)} is a standard Windows button.")
                Continue For
            End If

            Using image As Bitmap = Picture(control)
                Dim own As Color? = CommonestOwnColor(control, image)
                If own.HasValue AndAlso IsLight(own.Value) Then
                    found.Add($"{Describe(control)} is mostly {own.Value}.")
                End If
                Dim mark As Color? = LightCheckMark(control, image)
                If mark.HasValue Then
                    found.Add($"{Describe(control)} has a check mark that is mostly {mark.Value}.")
                End If
            End Using

        Next

        Return found

    End Function

    ' Lighter than any surface the Dark tokens fill: the lightest, the
    ' accent of a primary button, is 0.51 in relative luminance.
    Private Shared Function IsLight(color As Color) As Boolean
        Return color.A = 255 AndAlso Luminance(color) > 0.6
    End Function

    Private Shared Function Describe(control As Control) As String
        Dim name As String = If(Not String.IsNullOrEmpty(control.AccessibleName), control.AccessibleName,
                                If(Not String.IsNullOrEmpty(control.Name), control.Name, If(control.Text, String.Empty)))
        name = name.Replace(Environment.NewLine, " ")
        Return $"{control.GetType().Name} '{If(name.Length > 60, name.Substring(0, 60) & "...", name)}'"
    End Function

    ' Where a control's client area starts in its picture.
    Private Shared Function ClientOffset(control As Control) As Point
        Dim window As Point = If(control.Parent Is Nothing, control.Location, control.Parent.PointToScreen(control.Location))
        Dim client As Point = control.PointToScreen(Point.Empty)
        Return New Point(client.X - window.X, client.Y - window.Y)
    End Function

    ' The commonest color of what the control paints itself: its picture
    ' without what its visible children cover, and a window without its
    ' frame and title bar.
    Private Shared Function CommonestOwnColor(control As Control, image As Bitmap) As Color?

        Dim client As Point = ClientOffset(control)
        Dim whole As New Rectangle(Point.Empty, image.Size)
        Dim area As Rectangle = If(TypeOf control Is Form, New Rectangle(client, control.ClientSize), whole)
        area.Intersect(whole)

        Dim covered(image.Width * image.Height - 1) As Boolean
        For Each child As Control In control.Controls
            If Not child.Visible Then Continue For
            Dim bounds As New Rectangle(child.Left + client.X, child.Top + client.Y, child.Width, child.Height)
            bounds.Intersect(whole)
            For y As Integer = bounds.Top To bounds.Bottom - 1
                For x As Integer = bounds.Left To bounds.Right - 1
                    covered(y * image.Width + x) = True
                Next
            Next
        Next

        Dim pixels As Integer() = PixelsOf(image)
        Dim counts As New Dictionary(Of Integer, Integer)()
        For y As Integer = area.Top To area.Bottom - 1
            For x As Integer = area.Left To area.Right - 1
                Dim index As Integer = y * image.Width + x
                If covered(index) Then Continue For
                Dim count As Integer = 0
                counts.TryGetValue(pixels(index), count)
                counts(pixels(index)) = count + 1
            Next
        Next

        If counts.Count = 0 Then Return Nothing
        Return Color.FromArgb(counts.OrderByDescending(Function(entry) entry.Value).First().Key)

    End Function

    Private Shared Function PixelsOf(image As Bitmap) As Integer()
        Dim area As New Rectangle(Point.Empty, image.Size)
        Dim data As Imaging.BitmapData = image.LockBits(area, Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
        Try
            Dim pixels(area.Width * area.Height - 1) As Integer
            Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
            Return pixels
        Finally
            image.UnlockBits(data)
        End Try
    End Function

    ' The first check mark in a list or grid whose commonest color, apart
    ' from the row behind it, is light.
    Private Shared Function LightCheckMark(control As Control, image As Bitmap) As Color?
        Dim client As Point = ClientOffset(control)
        For Each mark In CheckMarks(control)
            Dim cell As Rectangle = mark.Cell
            cell.Offset(client)
            Dim color As Color? = MarkColor(image, cell)
            If color.HasValue AndAlso IsLight(color.Value) Then Return color
        Next
        Return Nothing
    End Function

    ' Where each row's check mark is drawn, in client coordinates, and
    ' whether it is checked: left of a list row's icon and text, in a
    ' grid's check-box cell, and in the square at the start of a checked
    ' list's item.
    Private Shared Iterator Function CheckMarks(control As Control) As IEnumerable(Of (Cell As Rectangle, Checked As Boolean))

        Dim list As ListView = TryCast(control, ListView)
        If list IsNot Nothing AndAlso list.CheckBoxes Then
            For Each item As ListViewItem In list.Items
                Dim row As Rectangle = item.GetBounds(ItemBoundsPortion.Entire)
                Yield (Rectangle.FromLTRB(row.Left, row.Top, item.GetBounds(ItemBoundsPortion.Icon).Left, row.Bottom), item.Checked)
            Next
        End If

        Dim grid As DataGridView = TryCast(control, DataGridView)
        If grid IsNot Nothing Then
            For Each column As DataGridViewColumn In grid.Columns
                If Not column.Visible OrElse Not TypeOf column Is DataGridViewCheckBoxColumn Then Continue For
                For Each row As DataGridViewRow In grid.Rows
                    Dim cell As Rectangle = grid.GetCellDisplayRectangle(column.Index, row.Index, True)
                    Dim value As Object = row.Cells(column.Index).FormattedValue
                    If Not cell.IsEmpty Then Yield (cell, TypeOf value Is Boolean AndAlso DirectCast(value, Boolean))
                Next
            Next
        End If

        Dim checkedList As CheckedListBox = TryCast(control, CheckedListBox)
        If checkedList IsNot Nothing Then
            For index As Integer = 0 To checkedList.Items.Count - 1
                Dim item As Rectangle = checkedList.GetItemRectangle(index)
                Yield (New Rectangle(item.Left, item.Top, item.Height, item.Height), checkedList.GetItemChecked(index))
            Next
        End If

    End Function

    ' The commonest color in the square at the middle of a cell, where its
    ' mark is drawn, apart from the row behind the mark, which is the
    ' commonest color there: a mark is smaller than the square.
    Private Shared Function MarkColor(image As Bitmap, cell As Rectangle) As Color?

        Dim side As Integer = Math.Min(cell.Width, cell.Height)
        cell = New Rectangle(cell.Left + (cell.Width - side) \ 2, cell.Top + (cell.Height - side) \ 2, side, side)
        cell.Intersect(New Rectangle(Point.Empty, image.Size))
        If cell.Width < 3 OrElse cell.Height < 3 Then Return Nothing

        Dim counts As New Dictionary(Of Integer, Integer)()
        For y As Integer = cell.Top To cell.Bottom - 1
            For x As Integer = cell.Left To cell.Right - 1
                Dim pixel As Integer = image.GetPixel(x, y).ToArgb()
                Dim count As Integer = 0
                counts.TryGetValue(pixel, count)
                counts(pixel) = count + 1
            Next
        Next

        Dim ranked = counts.OrderByDescending(Function(entry) entry.Value).ToList()
        If ranked.Count < 2 Then Return Nothing
        Return Color.FromArgb(ranked(1).Key)

    End Function

    ' Each heading sits just above its group's first row. Text is judged
    ' by its strongest pixels: smoothing leaves the rest of a stroke paler.
    ' Its rule is a line in the card border color, longer than text is tall.
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
                Dim rule As Integer = CountPixels(image, heading, Function(pixel) pixel.ToArgb() = UiTheme.CardBorder().ToArgb())
                Assert.IsTrue(rule > list.Font.Height, $"'{group.Header}' keeps its rule.")
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

        Using image As Bitmap = Picture(control)
            Dim surface As Color = SurfaceOf(control, image)
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

    ' What a caption is read against. A check box or option paints its own
    ' back color. WinForms fills a button, in a grey of its own when the
    ' button's back color is also its parent's: a button's surface is the
    ' color of most of its picture.
    Private Shared Function SurfaceOf(control As ButtonBase, image As Bitmap) As Color
        If Not TypeOf control Is Button Then Return control.BackColor
        Dim area As Rectangle = CaptionArea(control)
        Dim counts As New Dictionary(Of Integer, Integer)()
        For y As Integer = area.Top To area.Bottom - 1
            For x As Integer = area.Left To area.Right - 1
                Dim pixel As Integer = image.GetPixel(x, y).ToArgb()
                Dim count As Integer = 0
                counts.TryGetValue(pixel, count)
                counts(pixel) = count + 1
            Next
        Next
        Return Color.FromArgb(counts.OrderByDescending(Function(entry) entry.Value).First().Key)
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

        Dim surface As Color = SurfaceOf(control, plain)
        Dim drawn As Color = ControlPaint.Dark(control.BackColor)
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

    ' A list and a grid, each with a clear and a checked row, in a window of
    ' their own, with or without the check-mark painter. Given a change,
    ' it is made once the window is open and both are painted again.
    Private Shared Function CheckBoxSpecimens(attach As Boolean, Optional whileOpen As Action = Nothing) As (List As Bitmap, Grid As Bitmap)
        Using font As New Font("Segoe UI", 10.0F), form As New Form With {.ClientSize = New Size(640, 200), .Font = font}
            Dim list As New ListView With {.View = View.Details, .CheckBoxes = True, .Bounds = New Rectangle(8, 8, 300, 150)}
            list.Columns.Add("Work", 200)
            list.Items.Add(New ListViewItem("Clear"))
            list.Items.Add(New ListViewItem("Checked") With {.Checked = True})
            Dim grid As New DataGridView With {.Bounds = New Rectangle(320, 8, 300, 150), .AllowUserToAddRows = False}
            grid.Columns.Add(New DataGridViewCheckBoxColumn With {.HeaderText = "Use"})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.HeaderText = "Title"})
            grid.Rows.Add(False, "Clear")
            grid.Rows.Add(True, "Checked")
            form.Controls.AddRange(New Control() {list, grid})
            If attach Then
                CheckGlyph.Attach(list)
                CheckGlyph.Attach(grid)
            End If
            ShowOffscreen(form)
            If whileOpen IsNot Nothing Then
                whileOpen()
                list.Invalidate()
                grid.Invalidate()
                Application.DoEvents()
            End If
            Return (Picture(list), Picture(grid))
        End Using
    End Function

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

    ' A journal with a fact from an open index and a metric entered by hand.
    Private Shared Function JournalWithFacts() As JournalRecord
        Dim record As New JournalRecord With {.Name = "Fictional Journal"}
        record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.OpenAccess, .Value = "Open access", .Source = JournalFactCatalog.DoajSource, .CheckedUtc = New DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc)})
        record.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "5.2", .Year = 2025, .Source = "Scopus", .EnteredByYou = True})
        Return record
    End Function

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

    Private Shared Function Luminance(color As Color) As Double
        Return 0.2126 * LinearChannel(color.R) + 0.7152 * LinearChannel(color.G) + 0.0722 * LinearChannel(color.B)
    End Function

    Private Shared Function LinearChannel(channel As Byte) As Double
        Dim value As Double = channel / 255.0
        Return If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))
    End Function

    Private Const WM_PAINT As Integer = &HF
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101
    Private Const VK_SPACE As Integer = &H20
    Private Const VK_UP As Integer = &H26
    Private Const COLOR_HIGHLIGHT As Integer = 13
    Private Const DTM_GETDATETIMEPICKERINFO As Integer = &H100E

    ' Where Windows draws a date picker's parts, in client coordinates;
    ' only the drop-down button's left edge is read.
    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure DATETIMEPICKERINFO
        Public cbSize As Integer
        Public CheckLeft, CheckTop, CheckRight, CheckBottom As Integer
        Public CheckState As Integer
        Public ButtonLeft, ButtonTop, ButtonRight, ButtonBottom As Integer
        Public ButtonState As Integer
        Public Edit, UpDown, DropDown As IntPtr
    End Structure

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, ByRef info As DATETIMEPICKERINFO) As IntPtr
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetSysColor(index As Integer) As Integer
    End Function

    ' Changes the color mode of the test process only, on a thread of its
    ' own, and puts it back; no form is left open. Only this thread's forms
    ' count: one another test left open elsewhere is that test's failure.
    Private Shared Sub RunWithColorMode(mode As SystemColorMode, action As Action)
        StaRunner.Run(
            Sub()
                SkipWhereThemesCannotBeChecked(mode)
                Assert.AreEqual(mode = SystemColorMode.Dark, UiTheme.IsDark())
                action()
                Assert.AreEqual(0, StaRunner.OpenFormsOnThisThread().Count, "Theme tests leave no form open.")
            End Sub, mode:=mode)
    End Sub

    ' What these checks cannot judge, so they do not run: a high-contrast
    ' theme, and Dark before Windows 11.
    Private Shared Sub SkipWhereThemesCannotBeChecked(mode As SystemColorMode)
        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If
        If mode = SystemColorMode.Dark AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
        End If
    End Sub

End Class
