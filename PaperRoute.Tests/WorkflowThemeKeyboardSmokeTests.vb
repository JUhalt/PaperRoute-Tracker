Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class WorkflowThemeKeyboardSmokeTests

    Public Property TestContext As TestContext

    Private Shared ReadOnly DialogKeyMethod As MethodInfo =
        GetType(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance Or BindingFlags.NonPublic)

    ' Every ink the code draws on every surface it draws it on (#106): text
    ' on the page surfaces, and on the tinted rows, pills, chips, banners,
    ' and filled buttons the survey found. Each pair says where it is drawn;
    ' the ratios are written to the test's output.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub EveryInkAndSurfacePairTheCodeDraws_Meets4_5To1(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                Dim failures As New List(Of String)()
                For Each pair In DrawnPairs()
                    Dim ratio As Double = ContrastRatio(pair.Ink, pair.Surface)
                    TestContext.WriteLine($"{mode}  {ratio:F2}:1  {pair.Where} ({pair.Ink} on {pair.Surface})")
                    If ratio < 4.5 Then failures.Add($"{pair.Where}: {ratio:F2}:1 ({pair.Ink} on {pair.Surface})")
                Next
                Assert.AreEqual(0, failures.Count, $"Pairs under 4.5:1 in {mode}:{Environment.NewLine}{String.Join(Environment.NewLine, failures)}")
            End Sub)

    End Sub

    ' Under a Windows contrast theme every color the app chooses for itself
    ' is one of the user's system colors, and the Dark palette is never
    ' picked, even when the theme's window color is dark (#138). Tests
    ' cannot switch the theme, so the seam stands in for it.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub EveryToken_IsASystemColorUnderAContrastTheme(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                UiTheme.HighContrastOverride = True
                Try
                    Assert.IsFalse(UiTheme.IsDark(), "A contrast theme never picks the Dark palette.")
                    For Each token In ThemeTokens()
                        Dim value As Color = token.Value()
                        Assert.IsTrue(value.IsSystemColor, $"{token.Name} is {value} under a contrast theme, not a system color.")
                    Next
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Without a contrast theme every token is the color it was before #138,
    ' in Light and in Dark. A guard: the colors were recorded from the code
    ' before #138, which matches them.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub EveryToken_IsUnchangedWithoutAContrastTheme(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                UiTheme.HighContrastOverride = False
                Try
                    Dim column As Integer = If(mode = SystemColorMode.Dark, 2, 1)
                    Dim before As Dictionary(Of String, String) = TokensBefore.Select(Function(row) row.Split(" "c)).ToDictionary(Function(row) row(0), Function(row) row(column))
                    Dim tokens = ThemeTokens()
                    CollectionAssert.AreEquivalent(before.Keys.ToList(), tokens.Select(Function(token) token.Name).ToList(), "Every token has its recorded color.")
                    For Each token In tokens
                        Dim value As Color = token.Value()
                        If before(token.Name) = "system" Then
                            Assert.AreEqual(SystemColors.ControlText, value, $"{token.Name} in {mode}")
                        Else
                            Assert.AreEqual("FF" & before(token.Name), value.ToArgb().ToString("X8"), $"{token.Name} in {mode}")
                            Assert.IsFalse(value.IsSystemColor, $"{token.Name} in {mode}")
                        End If
                    Next
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Time with the journal, time with the author, production, and time not
    ' recorded can be told apart without their colors, in greyscale or by a
    ' reader who cannot tell the hues apart (#140): each pair of kinds, as
    ' the route map paints them, differs by 3:1 in luminance or in a
    ' pattern whose lines show. Under a contrast theme the colors are the
    ' user's, so only a pattern counts.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    <DataRow(SystemColorMode.Dark, True)>
    Public Sub RouteMapKinds_CanBeToldApartWithoutColor(mode As SystemColorMode, contrastTheme As Boolean)

        RunWithColorMode(mode,
            Sub()
                UiTheme.HighContrastOverride = contrastTheme
                Try
                    Dim kinds As RouteSegmentKind() = [Enum].GetValues(Of RouteSegmentKind)()
                    Dim painted As Dictionary(Of RouteSegmentKind, Integer()) = kinds.ToDictionary(Function(kind) kind, Function(kind) PaintedSwatch(kind))
                    Dim alike As New List(Of String)()
                    For first As Integer = 0 To kinds.Length - 2
                        For second As Integer = first + 1 To kinds.Length - 1
                            Dim one As Integer() = painted(kinds(first))
                            Dim other As Integer() = painted(kinds(second))
                            Dim ratio As Double = ContrastRatio(Color.FromArgb(Commonest(one)), Color.FromArgb(Commonest(other)))
                            If SamePattern(one, other) AndAlso (contrastTheme OrElse ratio < 3.0) Then alike.Add($"{kinds(first)} and {kinds(second)} ({ratio:F2}:1)")
                        Next
                    Next
                    Assert.AreEqual(0, alike.Count, $"Same pattern and too little luminance contrast in {mode}{If(contrastTheme, " under a contrast theme", String.Empty)}: {String.Join(", ", alike)}.")
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Under a contrast theme a filled chip or pill takes the window color
    ' as its fill, so it keeps its shape with an outline in its text color
    ' (#191): each Needs Attention chip and stage pill on the Board, built
    ' under the theme, and a chip of every tone, a pill of every stage, a
    ' tag of every color, the rail's count badge and the Reviewer Response
    ' Matrix's status pill. The seam stands in for the theme.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub ChipsAndPills_KeepAnOutlineInTheirTextColorUnderAContrastTheme(mode As SystemColorMode)

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If

        Dim missing As New List(Of String)()
        Dim boardShapes As Integer = 0

        Dim board As Func(Of String, Form) =
            Function(root As String) As Form
                UiTheme.HighContrastOverride = True
                Return ViewCatalog.Dialogs(GetType(ManuscriptPipeline.Form1))(root)
            End Function

        Try
            Dim failures As List(Of String) = ViewCatalog.ShowEach(
                {("The Board under a contrast theme", board)},
                Sub(name, shown)
                    For Each chip As FilterChip In Descendants(shown).OfType(Of FilterChip)().Where(Function(control) control.Visible)
                        boardShapes += 1
                        CheckOutline(missing, $"The Board's chip '{chip.Text}'", chip, ChipInk(chip))
                    Next
                    For Each pill As PillLabel In Descendants(shown).OfType(Of PillLabel)().Where(Function(control) control.Visible)
                        boardShapes += 1
                        CheckOutline(missing, $"The Board's pill '{pill.Text}'", pill, pill.ForeColor)
                    Next
                End Sub,
                mode:=mode)
            Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Finally
            UiTheme.HighContrastOverride = Nothing
        End Try

        Assert.IsTrue(boardShapes >= 2, $"The Board showed a Needs Attention chip and a stage pill ({boardShapes}).")

        RunWithColorMode(mode,
            Sub()
                UiTheme.HighContrastOverride = True
                Try
                    Using host As New Form With {.ClientSize = New Size(1200, 200), .Font = New Font("Segoe UI", 9.0F), .BackColor = UiTheme.BoardBackground()}
                        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = UiTheme.BoardBackground()}
                        host.Controls.Add(flow)
                        For Each tone As FilterChipTone In [Enum].GetValues(GetType(FilterChipTone))
                            flow.Controls.Add(New FilterChip With {.Text = tone.ToString() & " chip", .Tone = tone})
                        Next
                        For Each stage As PaperStage In [Enum].GetValues(GetType(PaperStage))
                            flow.Controls.Add(New PillLabel With {.Text = stage.ToString().ToUpperInvariant(), .Size = New Size(120, 22), .UseMnemonic = False,
                                                                  .BackColor = UiTheme.StageBackground(stage), .ForeColor = UiTheme.StageForeground(stage)})
                        Next
                        ViewCatalog.ShowOffscreen(host)
                        For Each chip As FilterChip In flow.Controls.OfType(Of FilterChip)()
                            CheckOutline(missing, $"A {chip.Tone} chip", chip, ChipInk(chip))
                        Next
                        For Each pill As PillLabel In flow.Controls.OfType(Of PillLabel)()
                            CheckOutline(missing, $"The {pill.Text} pill", pill, pill.ForeColor)
                        Next
                        host.Close()
                    End Using

                    For Each palette As TagPalette In [Enum].GetValues(GetType(TagPalette))
                        Using font As New Font("Segoe UI", 9.0F)
                            Dim chipSize As Size = TagChipPainter.Measure("teaching", font, 96)
                            Using picture As New Bitmap(chipSize.Width, chipSize.Height)
                                Using g As Graphics = Graphics.FromImage(picture)
                                    g.Clear(UiTheme.CardBackground())
                                    TagChipPainter.Draw(g, New Rectangle(Point.Empty, chipSize), "teaching", palette, font)
                                End Using
                                CheckOutline(missing, $"A {palette} tag", picture, WorkTypeService.Foreground(palette, False))
                            End Using
                        End Using
                    Next

                    ' A rail item wide enough that the right half of it holds
                    ' only the count badge.
                    Using host As New Form With {.ClientSize = New Size(320, 60), .Font = New Font("Segoe UI", 9.0F), .BackColor = UiTheme.HeaderBackground()}
                        Dim item As New RailButton(RailGlyph.Reminders, "Reminders") With {.Location = New Point(10, 10), .Size = New Size(300, 36), .Badge = 12}
                        host.Controls.Add(item)
                        ViewCatalog.ShowOffscreen(host)
                        Using picture As Bitmap = PictureOf(item)
                            CheckOutlineWithin(missing, "The rail's count badge", picture, New Rectangle(picture.Width \ 2, 0, picture.Width - picture.Width \ 2, picture.Height), UiTheme.WarningColor(),
                                Function(pixel) Distance(pixel, UiTheme.WarningColor()) < Distance(pixel, SystemColors.Window))
                        End Using
                        host.Close()
                    End Using

                    ' A row of the Reviewer Response Matrix, drawn as its list
                    ' draws one. In progress is drawn in the accent color,
                    ' which nothing else in the row is, so the pill is found
                    ' by that color.
                    Dim manuscript As New Manuscript With {.Title = "Contrast pill fixture", .CurrentStage = PaperStage.Submitted}
                    Dim submission As New JournalSubmission With {.JournalName = "Fictional journal", .SubmittedDate = New DateTime(2026, 9, 20)}
                    Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision}
                    submission.Decisions.Add(decision)
                    manuscript.Submissions.Add(submission)
                    ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {
                        .DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1",
                        .CommentText = "Clarify the sampling frame.", .Status = ReviewerResponseStatus.InProgress
                    })
                    Using matrix As New ReviewerResponseMatrixForm(manuscript, submission)
                        Dim list As ListBox = DirectCast(GetType(ReviewerResponseMatrixForm).GetField("lstResponses", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(matrix), ListBox)
                        Dim ink As Color = ReviewerResponseMatrixForm.StatusPill(ReviewerResponseStatus.InProgress).Ink
                        Using row As New Bitmap(600, list.Font.Height * 2 + UiTheme.Px(26, matrix.DeviceDpi))
                            Using g As Graphics = Graphics.FromImage(row)
                                GetType(ReviewerResponseMatrixForm).GetMethod("DrawResponseItem", BindingFlags.Instance Or BindingFlags.NonPublic).
                                    Invoke(matrix, New Object() {list, New DrawItemEventArgs(g, list.Font, New Rectangle(Point.Empty, row.Size), 0, DrawItemState.None)})
                            End Using
                            CheckOutlineWithin(missing, "The Reviewer Response Matrix's In progress pill", row, New Rectangle(Point.Empty, row.Size), ink,
                                Function(pixel) pixel.ToArgb() = ink.ToArgb())
                        End Using
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

        Assert.AreEqual(0, missing.Count, $"Without an outline in their text color under a contrast theme in {mode}:{Environment.NewLine}{String.Join(Environment.NewLine, missing)}")

    End Sub

    ' Under a contrast theme a chip shows pressed and focused as Windows
    ' draws them there (#191): pressed in the highlight pair, and focused
    ' with a ring in the text color. Without one, a pressed chip is drawn
    ' as it was before, the same as one not pressed.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub AChip_ShowsPressedAndFocusAsWindowsDoesUnderAContrastTheme(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                Try
                    For Each contrastTheme As Boolean In {False, True}

                        UiTheme.HighContrastOverride = contrastTheme

                        Using host As New Form With {.ClientSize = New Size(400, 120), .Font = New Font("Segoe UI", 9.0F), .BackColor = UiTheme.BoardBackground()}

                            Dim other As New TextBox With {.Location = New Point(8, 8), .Width = 120}
                            Dim chip As New FilterChip With {.Text = "1 waiting 90+ days", .Tone = FilterChipTone.Warning, .Location = New Point(8, 48)}
                            host.Controls.AddRange(New Control() {other, chip})
                            ViewCatalog.ShowOffscreen(host)
                            other.Focus()
                            Application.DoEvents()

                            Dim middle As Integer = chip.Height \ 2
                            Dim inside As New Point(chip.Padding.Left \ 2, middle)
                            Dim plain As Bitmap = PictureOf(chip)

                            SendMessage(chip.Handle, WM_LBUTTONDOWN, New IntPtr(1), MousePoint(inside))
                            Application.DoEvents()
                            Dim pressed As Bitmap = PictureOf(chip)
                            SendMessage(chip.Handle, WM_LBUTTONUP, IntPtr.Zero, MousePoint(inside))
                            Application.DoEvents()

                            ' Focus cues show, as once a key has been pressed.
                            other.Focus()
                            SendMessage(host.Handle, WM_CHANGEUISTATE, New IntPtr(UIS_CLEAR Or (UISF_HIDEFOCUS << 16)), IntPtr.Zero)
                            chip.Focus()
                            Application.DoEvents()
                            Dim focused As Bitmap = PictureOf(chip)

                            Try
                                If contrastTheme Then
                                    Assert.AreEqual(SystemColors.Highlight.ToArgb(), pressed.GetPixel(inside.X, inside.Y).ToArgb(), $"Pressed under a contrast theme in {mode}, the chip is the highlight color.")
                                    Assert.AreEqual(SystemColors.Window.ToArgb(), plain.GetPixel(inside.X, inside.Y).ToArgb(), "Not pressed, it is the window color.")
                                    Assert.IsTrue(chip.Focused, "The chip took the focus.")
                                    Assert.AreEqual(SystemColors.WindowText.ToArgb(), focused.GetPixel(chip.Width \ 2, 2).ToArgb(), $"Focused under a contrast theme in {mode}, the chip's ring is the text color.")
                                    Assert.AreEqual(SystemColors.Window.ToArgb(), plain.GetPixel(chip.Width \ 2, 2).ToArgb(), "Not focused, there is no ring.")
                                Else
                                    Assert.IsTrue(SamePicture(plain, pressed), $"In {mode} a pressed chip is drawn as one not pressed.")
                                End If
                            Finally
                                plain.Dispose()
                                pressed.Dispose()
                                focused.Dispose()
                            End Try

                            host.Close()

                        End Using

                    Next
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Under a contrast theme menus are drawn as Windows draws them (#191):
    ' each item's text in the menu text color, and the hovered item in the
    ' highlight pair; WinForms drew the text in the button text color,
    ' which Night sky makes yellow. Main puts the renderer on the UI thread
    ' (see below); here it is put on the runner's, and the Board's card
    ' menu and a new menu both draw with it. The seam stands in for the
    ' theme; the colors are compared by name, as Light's menu text and
    ' button text are both black.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub Menus_UseTheMenuTextColorsUnderAContrastTheme(mode As SystemColorMode)

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If

        Dim found As New List(Of String)()
        Dim items As Integer = 0

        Dim board As Func(Of String, Form) =
            Function(root As String) As Form
                UiTheme.HighContrastOverride = True
                ThemedMenuRenderer.Install()
                Return ViewCatalog.Dialogs(GetType(ManuscriptPipeline.Form1))(root)
            End Function

        Try
            Dim failures As List(Of String) = ViewCatalog.ShowEach(
                {("The Board under a contrast theme", board)},
                Sub(name, shown)
                    Dim card As Control = Descendants(shown).OfType(Of RoundedPanel)().First(Function(control) control.Visible AndAlso control.ContextMenuStrip IsNot Nothing)
                    Using fresh As New ContextMenuStrip()
                        fresh.Items.Add("Preferences...")
                        For Each menu As ContextMenuStrip In {card.ContextMenuStrip, fresh}
                            For Each item As ToolStripMenuItem In menu.Items.OfType(Of ToolStripMenuItem)()
                                items += 1
                                Dim ink As Color = TextColorDrawn(menu.Renderer, item)
                                If ink <> SystemColors.MenuText Then found.Add($"'{item.Text}' is drawn in {ink.Name}, not MenuText.")
                            Next
                        Next
                        Dim hovered As ToolStripMenuItem = card.ContextMenuStrip.Items.OfType(Of ToolStripMenuItem)().First()
                        hovered.Select()
                        If Not hovered.Selected Then
                            found.Add("The first item could not be hovered.")
                        Else
                            Dim ink As Color = TextColorDrawn(card.ContextMenuStrip.Renderer, hovered)
                            If ink <> SystemColors.HighlightText Then found.Add($"Hovered, '{hovered.Text}' is drawn in {ink.Name}, not HighlightText.")
                            Using picture As Bitmap = BackgroundDrawn(card.ContextMenuStrip.Renderer, hovered)
                                If picture.GetPixel(picture.Width \ 2, picture.Height \ 2).ToArgb() <> SystemColors.Highlight.ToArgb() Then found.Add($"Hovered, '{hovered.Text}' is not on the highlight color.")
                            End Using
                        End If
                    End Using
                End Sub,
                mode:=mode)
            Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Finally
            UiTheme.HighContrastOverride = Nothing
        End Try

        Assert.IsTrue(items >= 4, $"The card menu's items and the new menu's were drawn ({items}).")
        Assert.AreEqual(0, found.Count, $"Under a contrast theme in {mode}:{Environment.NewLine}{String.Join(Environment.NewLine, found)}")

    End Sub

    ' In Light and Dark the menu renderer draws as WinForms' own, picture
    ' for picture: an item's text, a red Delete, and a hovered item's text
    ' and background. A guard: it cannot fail before #191, when WinForms'
    ' renderer drew every menu; it fails if the renderer changes either.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub Menus_DrawAsWinFormsDoesInLightAndDark(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                UiTheme.HighContrastOverride = False
                Try
                    Using menu As New ContextMenuStrip()
                        menu.Items.Add("Open")
                        Dim delete As ToolStripItem = menu.Items.Add("Delete...")
                        delete.ForeColor = UiTheme.DangerColor()
                        Dim hovered As ToolStripItem = menu.Items.Add("View Route")
                        hovered.Select()
                        Assert.IsTrue(hovered.Selected, "An item can be hovered.")

                        Dim own As New ToolStripProfessionalRenderer()
                        Dim themed As New ThemedMenuRenderer()

                        For Each item As ToolStripMenuItem In menu.Items.OfType(Of ToolStripMenuItem)()
                            Assert.AreEqual(TextColorDrawn(own, item), TextColorDrawn(themed, item), $"'{item.Text}' is drawn in the same color in {mode}.")
                            Using expected As Bitmap = TextDrawn(own, item), actual As Bitmap = TextDrawn(themed, item)
                                Assert.IsTrue(SamePicture(expected, actual), $"'{item.Text}' is drawn the same in {mode}.")
                            End Using
                            Using expected As Bitmap = BackgroundDrawn(own, item), actual As Bitmap = BackgroundDrawn(themed, item)
                                Assert.IsTrue(SamePicture(expected, actual), $"'{item.Text}''s background is drawn the same in {mode}.")
                            End Using
                        Next
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub

    ' Guard: Main puts the menu renderer on the UI thread, before anything
    ' is shown. It fails before #191, when nothing did.
    <TestMethod>
    Public Sub MainDrawsTheUiThreadsMenusWithTheThemedRenderer()

        Dim source As String =
            File.ReadAllText(Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Program.vb"))

        Dim main As Integer = source.IndexOf("Public Sub Main(", StringComparison.Ordinal)
        Dim installing As Integer = source.IndexOf("ThemedMenuRenderer.Install()", Math.Max(main, 0), StringComparison.Ordinal)
        Dim running As Integer = source.IndexOf("Run(args, instance)", Math.Max(main, 0), StringComparison.Ordinal)

        Assert.IsTrue(main >= 0 AndAlso installing > main AndAlso installing < running, "Main puts the menu renderer on the UI thread before running the app.")

    End Sub

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub WorkflowSurfaces_KeepTextLegibleAndKeyboardActionsAvailable(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                ReportNativeSelectionContrast(TestContext)
                Using fixture As New SmokeFixture()
                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        AssertTextContrast(dialog)
                        AssertTabReachability(dialog, New Control() {
                            Descendants(dialog).OfType(Of ComboBox)().Single(),
                            Descendants(dialog).OfType(Of ListBox)().Single(),
                            NamedControl(dialog, "Save readiness and navigate"),
                            NamedControl(dialog, "Open selected journal submission portal"),
                            DirectCast(dialog.AcceptButton, Control), DirectCast(dialog.CancelButton, Control)
                        })
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                        Assert.IsNull(dialog.RequestedNavigation)
                    End Using

                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        Dim fileList As ListBox = FileListIn(dialog)
                        Assert.IsTrue(fileList.Items.Cast(Of Object)().Any(Function(item) item.ToString().Contains("[Not checked]")))
                        PumpUntilComplete(dialog.CheckPacketFilesAsync())
                        For Each state As String In {"Metadata only", "No fingerprint", "Unchanged", "Changed", "Missing", "Unavailable"}
                            Dim index As Integer = Enumerable.Range(0, fileList.Items.Count).First(
                                Function(item) fileList.Items(item).ToString().Contains("[" & state & "]"))
                            fileList.SelectedIndex = index
                            Application.DoEvents()
                            Assert.IsTrue(NamedControl(dialog, "Selected file details").Text.Contains("Status: " & state),
                                "Integrity status must remain readable as text, not depend on a color.")
                            AssertTextContrast(dialog)
                        Next

                        SelectFile(fileList, "04 unchanged fixture")
                        ' A nonactivating form cannot faithfully reproduce native
                        ' focus transitions between LinkLabel and SplitContainer.
                        ' Verify eligibility here; exercise the full vault tab
                        ' sequence on the desktop during manual certification.
                        AssertKeyboardEligibility(New Control() {
                            fileList,
                            NamedControl(dialog, "Selected packet details").Parent.Controls.OfType(Of ListBox)().Single(),
                            NamedControl(dialog, "Show all manuscript packets"),
                            NamedControl(dialog, "Save packet changes and go to related record"),
                            NamedControl(dialog, "Check files in selected packet"),
                            NamedControl(dialog, "Record or replace selected file fingerprint"),
                            DirectCast(dialog.AcceptButton, Control), DirectCast(dialog.CancelButton, Control)
                        })
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                        Assert.IsNull(dialog.RequestedNavigation)
                    End Using

                    Using dialog As New SubmissionProbe(fixture)
                        ShowInvisible(dialog)
                        AssertTextContrast(dialog)
                        ' Submission Details also contains a native focus-managing splitter.
                        AssertKeyboardEligibility(New Control() {
                            NamedControl(dialog, "View packets for this submission"),
                            DirectCast(dialog.AcceptButton, Control)
                        })
                        dialog.Close()
                    End Using
                End Using
            End Sub)

    End Sub

    <TestMethod>
    Public Sub FormDefaultEnterAndEscape_KeepReadinessAndFingerprintSaveBoundaries()

        RunWithColorMode(SystemColorMode.Classic,
            Sub()
                Using fixture As New SmokeFixture()
                    Dim original As String = Snapshot(fixture.Manuscript)
                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        ButtonWithText(dialog, "Mark Complete").PerformClick()
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(original, Snapshot(fixture.Manuscript))

                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        ButtonWithText(dialog, "Mark Complete").PerformClick()
                        dialog.ActiveControl = Descendants(dialog).OfType(Of ListBox)().Single()
                        Assert.IsTrue(dialog.Press(Keys.Enter))
                        Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(ReadinessItemStatus.Complete, fixture.Manuscript.ReadinessProfiles(0).Items(0).Status)
                    Assert.AreEqual(PaperStage.Submitted, fixture.Manuscript.CurrentStage)
                    Assert.AreEqual(1, fixture.Manuscript.Submissions.Count)

                    Dim fileId As Guid = fixture.NoFingerprint.Id
                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        SelectFile(FileListIn(dialog), "03 no fingerprint fixture")
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(String.Empty, fixture.Manuscript.SubmissionPackets(0).Files.Single(Function(item) item.Id = fileId).Sha256)

                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        Dim fileList As ListBox = FileListIn(dialog)
                        SelectFile(fileList, "03 no fingerprint fixture")
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))
                        dialog.ActiveControl = fileList
                        Assert.IsTrue(dialog.Press(Keys.Enter))
                        Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(64, fixture.Manuscript.SubmissionPackets(0).Files.Single(Function(item) item.Id = fileId).Sha256.Length)
                    Assert.AreEqual("abc", File.ReadAllText(fixture.NoFingerprint.LocalFilePath),
                        "Keyboard save must adopt only fingerprint metadata, never alter the source fixture.")
                End Using
            End Sub)

    End Sub

    ' The runner changes only the test process's color mode, before this
    ' thread constructs its forms, and puts it back. The current Windows
    ' theme and all saved application settings are untouched. Only this
    ' thread's forms count: one another test left open elsewhere is that
    ' test's failure.
    Private Shared Sub RunWithColorMode(mode As SystemColorMode, action As Action)
        StaRunner.Run(
            Sub()
                If SystemInformation.HighContrast Then
                    Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
                End If
                If mode = SystemColorMode.Dark AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
                    Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
                End If
                Dim expectedDark As Boolean = mode = SystemColorMode.Dark OrElse
                    (mode = SystemColorMode.System AndAlso Application.SystemColorMode = SystemColorMode.Dark)
                Assert.AreEqual(expectedDark, UiTheme.IsDark())
                action()
                Assert.AreEqual(0, StaRunner.OpenFormsOnThisThread().Count, "Theme smoke tests leave no form open.")
            End Sub, mode:=mode)
    End Sub

    Private Shared Sub ShowInvisible(dialog As Form)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        dialog.Size = dialog.MinimumSize
        dialog.Location = New Point(-20000, -20000)
        dialog.PerformLayout()
        Application.DoEvents()
    End Sub

    Private Shared Sub AssertTabReachability(dialog As Form, required As IEnumerable(Of Control))
        AssertKeyboardEligibility(required)
        For Each backwards As Boolean In {False, True}
            dialog.ActiveControl = Nothing
            Dim visited As New HashSet(Of Control)()
            Dim sequence As New List(Of String)()
            For count As Integer = 0 To Descendants(dialog).Count() * 2 + 2
                Assert.IsTrue(DispatchTabFromActiveControl(dialog, If(backwards, Keys.Shift Or Keys.Tab, Keys.Tab)))
                Dim active As Control = DeepestActiveControl(dialog)
                Assert.IsNotNull(active, "Tab navigation must select a control without activating a visible window.")
                sequence.Add(active.GetType().Name & ": " & active.AccessibleName & " " & active.Text)
                If Not visited.Add(active) Then Exit For
            Next
            For Each control As Control In required
                Assert.IsTrue(visited.Contains(control),
                    If(backwards, "Shift+Tab", "Tab") & " could not reach " & control.AccessibleName & " " & control.Text &
                    "; visited: " & String.Join(" -> ", sequence))
            Next
        Next
    End Sub

    Private Shared Sub AssertKeyboardEligibility(required As IEnumerable(Of Control))
        For Each control As Control In required
            Assert.IsTrue(control.Visible AndAlso control.Enabled AndAlso control.TabStop AndAlso control.CanSelect,
                "The required keyboard action must be selectable: " & control.AccessibleName & " " & control.Text)
        Next
    End Sub

    Private Shared Function DispatchTabFromActiveControl(dialog As Form, key As Keys) As Boolean
        ' Tab must start at the active child and bubble through its containers.
        ' Form-only routing skips focus-managing SplitContainer children. Calling
        ' the virtual managed handler also avoids physical keyboard modifier state.
        Dim target As Control = If(DeepestActiveControl(dialog), dialog)
        Try
            Return CBool(DialogKeyMethod.Invoke(target, New Object() {key}))
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function

    Private Shared Sub AssertTextContrast(dialog As Form)
        For Each control As Control In Descendants(dialog).Where(Function(item) item.Visible AndAlso item.Enabled)
            If TypeOf control Is Button Then
                Dim button As Button = DirectCast(control, Button)
                AssertContrast(button.ForeColor, button.BackColor, button.Text)
                AssertContrast(button.ForeColor, button.FlatAppearance.MouseOverBackColor, button.Text & " hovered")
            ElseIf TypeOf control Is LinkLabel Then
                Dim link As LinkLabel = DirectCast(control, LinkLabel)
                AssertContrast(link.LinkColor, EffectiveBackground(link), link.Text)
                AssertContrast(link.ActiveLinkColor, EffectiveBackground(link), link.Text & " active")
                AssertContrast(link.VisitedLinkColor, EffectiveBackground(link), link.Text & " visited")
            ElseIf TypeOf control Is TextBox OrElse TypeOf control Is ListBox OrElse TypeOf control Is ComboBox OrElse
                (TypeOf control Is Label AndAlso Not String.IsNullOrWhiteSpace(control.Text)) Then
                AssertContrast(control.ForeColor, EffectiveBackground(control), control.AccessibleName & " " & control.Text)
            End If
        Next
    End Sub

    Private Shared Sub ReportNativeSelectionContrast(context As TestContext)
        Dim foreground As Color = SystemColors.HighlightText
        Dim background As Color = SystemColors.Highlight
        Dim ratio As Double = ContrastRatio(foreground, background)
        ' Native selected-row colors are provided by Windows. Record the actual
        ' ratio without weakening the app-owned palette assertions or rounding it
        ' into a pass. Native selection rendering remains a manual release gate.
        context.WriteLine($"Native selected-row observation: {ratio:F5}:1 ({foreground} on {background}). " &
            "Windows-owned colors; verify native selection readability during manual certification.")
    End Sub

    Private Shared Function EffectiveBackground(control As Control) As Color
        While control.BackColor.A = 0 AndAlso control.Parent IsNot Nothing
            control = control.Parent
        End While
        Return control.BackColor
    End Function

    ' The pairs, read from the code that chooses them where the code picks a
    ' color by state, and otherwise as the survey found them drawn.
    Private Shared Function DrawnPairs() As List(Of (Where As String, Ink As Color, Surface As Color))

        Dim pairs As New List(Of (Where As String, Ink As Color, Surface As Color))()

        ' Labels and painted text sit on the board, a card, or a header.
        Dim inks = {("primary", UiTheme.PrimaryText()), ("secondary", UiTheme.SecondaryText()), ("muted", UiTheme.MutedText()),
                    ("accent", UiTheme.AccentColor()), ("secondary accent", UiTheme.AccentSecondaryColor()), ("info", UiTheme.InfoColor()),
                    ("warning", UiTheme.WarningColor()), ("danger", UiTheme.DangerColor()), ("success", UiTheme.SuccessColor())}
        For Each surface In {("board", UiTheme.BoardBackground()), ("card", UiTheme.CardBackground()), ("header", UiTheme.HeaderBackground())}
            For Each ink In inks
                pairs.Add(($"{ink.Item1} text on the {surface.Item1}", ink.Item2, surface.Item2))
            Next
        Next

        ' Hovered buttons and rail items.
        pairs.Add(("A hovered button or hovered rail item: primary text", UiTheme.PrimaryText(), UiTheme.HoverBackground()))
        pairs.Add(("A hovered quiet button: accent text", UiTheme.AccentColor(), UiTheme.HoverBackground()))
        pairs.Add(("A hovered danger button: danger text", UiTheme.DangerColor(), UiTheme.HoverBackground()))

        ' A grid's selected row, as UiPolish styles a dialog's grids and the
        ' pages style their tables (#138).
        pairs.Add(("A selected row in a dialog's grid", UiTheme.SelectedRowText(), UiTheme.SelectedRowBackground(UiTheme.HoverBackground())))
        pairs.Add(("A selected row in a page's table", UiTheme.SelectedRowText(), UiTheme.SelectedRowBackground(UiTheme.AccentMutedBackground())))

        ' Selected rows, the selected rail item, and the current waypoint.
        Dim selected As Color = UiTheme.AccentMutedBackground()
        pairs.Add(("A selected row, the example banner, the welcome's privacy note, or the shortlist's offer: primary text", UiTheme.PrimaryText(), selected))
        pairs.Add(("A selected submission, comment, or route: secondary text", UiTheme.SecondaryText(), selected))
        pairs.Add(("The selected rail item, an active filter chip, or the current waypoint's kind: accent text", UiTheme.AccentColor(), selected))
        pairs.Add(("The current waypoint's link", UiTheme.AccentSecondaryColor(), selected))

        ' A submission's latest decision, the third line of its row.
        Dim decisions As New List(Of (Name As String, Latest As EditorialDecisionEvent)) From {("awaiting a decision", Nothing)}
        For Each decision As EditorialDecision In [Enum].GetValues(GetType(EditorialDecision))
            If decision <> EditorialDecision.None Then decisions.Add((decision.ToString(), New EditorialDecisionEvent With {.Decision = decision}))
        Next
        For Each decision In decisions
            pairs.Add(($"A submission's latest decision, {decision.Name}", EditManuscriptForm.OutcomeColor(decision.Latest, False), UiTheme.CardBackground()))
            pairs.Add(($"A selected submission's latest decision, {decision.Name}", EditManuscriptForm.OutcomeColor(decision.Latest, True), selected))
        Next

        ' Pills, chips, badges, and banners on their own fills.
        For Each status As ReviewerResponseStatus In [Enum].GetValues(GetType(ReviewerResponseStatus))
            Dim pill As (Fill As Color, Ink As Color) = ReviewerResponseMatrixForm.StatusPill(status)
            pairs.Add(($"The {ReviewerResponseService.FormatStatus(status)} pill", pill.Ink, pill.Fill))
        Next
        For Each stage As PaperStage In [Enum].GetValues(GetType(PaperStage))
            pairs.Add(($"The {stage} stage pill and deadline badge", UiTheme.StageForeground(stage), UiTheme.StageBackground(stage)))
        Next
        pairs.Add(("A warning chip, the rail's count, or an Unresolved pill", UiTheme.WarningColor(), UiTheme.WarningMutedBackground()))
        pairs.Add(("A danger chip", UiTheme.DangerColor(), UiTheme.DangerMutedBackground()))

        ' Filter chips hovered (#142): a plain chip on the hover color, a
        ' toned one on its own fill part way back to the card surface
        ' (FilterChip.HoverFill).
        For Each chip As (Tone As FilterChipTone, Ink As Color, Fill As Color) In {
            (FilterChipTone.Neutral, UiTheme.SecondaryText(), UiTheme.CardBackground()),
            (FilterChipTone.Warning, UiTheme.WarningColor(), UiTheme.WarningMutedBackground()),
            (FilterChipTone.Danger, UiTheme.DangerColor(), UiTheme.DangerMutedBackground()),
            (FilterChipTone.Active, UiTheme.AccentColor(), UiTheme.AccentMutedBackground())
        }
            pairs.Add(($"A {chip.Tone} filter chip, hovered", chip.Ink, FilterChip.HoverFill(chip.Tone, chip.Fill)))
        Next

        pairs.Add(("Working offline in the rail", UiTheme.InfoColor(), UiTheme.InfoMutedBackground()))
        pairs.Add(("Working offline in the rail, hovered", UiTheme.InfoColor(), UiTheme.Blend(UiTheme.InfoMutedBackground(), UiTheme.PrimaryText(), 0.08F)))
        pairs.Add(("The assistant's banner in Record Editorial Decision", UiTheme.PrimaryText(), UiTheme.InfoMutedBackground()))

        ' Filled buttons, at rest, hovered, and pressed.
        Dim accent As Color = UiTheme.AccentColor()
        Dim shade As Color = If(UiTheme.IsDark(), Color.White, Color.Black)
        pairs.Add(("A primary button", UiTheme.OnAccentText(), accent))
        pairs.Add(("A primary button, hovered", UiTheme.OnAccentText(), UiTheme.Blend(accent, shade, 0.1F)))
        pairs.Add(("A primary button, pressed", UiTheme.OnAccentText(), UiTheme.Blend(accent, shade, 0.2F)))

        ' The lists that draw their own text and buttons (#133): the Deadlines
        ' list's headings, its empty sentence and its Done line on the board;
        ' on a row's card its date by group, title, manuscript, journal,
        ' progress and the dash for no date, and its kind's badge; the counts
        ' and their captions on their tiles; Import & Export's headings and
        ' descriptions on the board; and each button the lists draw or hold,
        ' Secondary and the quiet "⋯", in each state, on what is under its
        ' text: its fill, or the row's card.
        For Each group As DeadlineGroup In [Enum].GetValues(GetType(DeadlineGroup))
            pairs.Add(($"A Deadlines heading, {group}", DeadlineList.HeadingInkOf(group), UiTheme.BoardBackground()))
            pairs.Add(($"A Deadlines row's date and how far away, {group}", DeadlineRow.WhenInkOf(group), UiTheme.CardBackground()))
            pairs.Add(($"A Deadlines row's day of the month, {group}", DeadlineRow.DayInkOf(group), UiTheme.CardBackground()))
            pairs.Add(($"A Deadlines row's title, {group}", DeadlineRow.TitleInkOf(group), UiTheme.CardBackground()))
        Next
        pairs.Add(("A Deadlines row's month, manuscript, journal and progress", UiTheme.SecondaryText(), UiTheme.CardBackground()))
        pairs.Add(("A Deadlines row's dash for no date", UiTheme.MutedText(), UiTheme.CardBackground()))
        For Each kind As DeadlineKind In [Enum].GetValues(GetType(DeadlineKind))
            Dim stage As PaperStage = DeadlineRow.StageOf(kind)
            pairs.Add(($"A Deadlines row's {kind} badge", UiTheme.StageForeground(stage), UiTheme.StageBackground(stage)))
        Next
        pairs.Add(("The Deadlines list's sentence when nothing is listed, and its Done line", UiTheme.SecondaryText(), UiTheme.BoardBackground()))
        pairs.Add(("The Done line's Show or Hide", UiTheme.AccentColor(), UiTheme.BoardBackground()))
        For index As Integer = 0 To 3
            pairs.Add(($"A Deadlines count, tile {index + 1}", DeadlineGlance.CountInkOf(index), UiTheme.CardBackground()))
        Next
        pairs.Add(("A Deadlines count's caption", UiTheme.SecondaryText(), UiTheme.CardBackground()))
        pairs.Add(("An Import & Export heading", UiTheme.MutedText(), UiTheme.BoardBackground()))
        pairs.Add(("An Import & Export description", UiTheme.SecondaryText(), UiTheme.BoardBackground()))
        Dim states As (Name As String, Hover As Boolean, Pressed As Boolean, Enabled As Boolean)() = {
            ("at rest", False, False, True), ("hovered", True, False, True), ("pressed", False, True, True), ("off", False, False, False)
        }
        For Each role As ActionButtonRole In {ActionButtonRole.Secondary, ActionButtonRole.Quiet}
            For Each state In states
                Dim face As (Fill As Color, Border As Color, Ink As Color) = ActionButton.FaceColors(role, state.Hover, state.Pressed, state.Enabled)
                pairs.Add(($"A {role} button the Deadlines or Import & Export list draws or holds, {state.Name}", face.Ink, If(face.Fill.IsEmpty, UiTheme.CardBackground(), face.Fill)))
            Next
        Next

        ' The route map's white labels on its fills and numbered events.
        For Each kind As RouteSegmentKind In {RouteSegmentKind.Journal, RouteSegmentKind.Author, RouteSegmentKind.Production}
            pairs.Add(($"A route map's {kind} segment label", Color.White, RouteMapStyle.Fill(kind)))
        Next
        For Each marker As RouteMarker In {
            New RouteMarker With {.Kind = RouteMarkerKind.Submitted},
            New RouteMarker With {.Kind = RouteMarkerKind.Published},
            New RouteMarker With {.Kind = RouteMarkerKind.Decision, .Decision = EditorialDecision.Accepted},
            New RouteMarker With {.Kind = RouteMarkerKind.Decision, .Decision = EditorialDecision.MajorRevision},
            New RouteMarker With {.Kind = RouteMarkerKind.Decision, .Decision = EditorialDecision.Withdrawn},
            New RouteMarker With {.Kind = RouteMarkerKind.Decision, .Decision = EditorialDecision.Rejected}
        }
            pairs.Add(($"A route map's numbered {marker.Kind} {marker.Decision} event", Color.White, RouteMapStyle.MarkerColor(marker)))
        Next

        Return pairs

    End Function

    Private Shared Sub AssertContrast(foreground As Color, background As Color, label As String)
        Dim ratio As Double = ContrastRatio(foreground, background)
        Assert.IsTrue(ratio >= 4.5,
            $"Normal-size text needs 4.5:1 contrast; '{label.Substring(0, Math.Min(90, label.Length))}' has {ratio:F2}:1 ({foreground} on {background}).")
    End Sub

    ' ---------------------------------------------------------------
    ' Chips, pills and menus under a contrast theme (#191)
    ' ---------------------------------------------------------------

    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_LBUTTONUP As Integer = &H202
    Private Const WM_CHANGEUISTATE As Integer = &H127
    Private Const UIS_CLEAR As Integer = 2
    Private Const UISF_HIDEFOCUS As Integer = 1

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Shared Function MousePoint(at As Point) As IntPtr
        Return New IntPtr((at.Y << 16) Or (at.X And &HFFFF))
    End Function

    ' The color a chip's text is drawn in, for its tone.
    Private Shared Function ChipInk(chip As FilterChip) As Color
        Select Case chip.Tone
            Case FilterChipTone.Warning
                Return UiTheme.WarningColor()
            Case FilterChipTone.Danger
                Return UiTheme.DangerColor()
            Case FilterChipTone.Active
                Return UiTheme.AccentColor()
            Case Else
                Return UiTheme.SecondaryText()
        End Select
    End Function

    Private Shared Function PictureOf(control As Control) As Bitmap
        Dim picture As New Bitmap(control.Width, control.Height)
        control.DrawToBitmap(picture, New Rectangle(Point.Empty, control.Size))
        Return picture
    End Function

    Private Shared Sub CheckOutline(missing As List(Of String), label As String, control As Control, ink As Color)
        Using picture As Bitmap = PictureOf(control)
            CheckOutline(missing, label, picture, ink)
        End Using
    End Sub

    ' A shape's outline: along its straight top edge and its straight bottom
    ' edge, a run at least half as long as the edge of pixels nearer the
    ' ink than the window color, which the shape is filled with; a smoothed
    ' line one pixel wide is not quite the ink.
    Private Shared Sub CheckOutline(missing As List(Of String), label As String, picture As Bitmap, ink As Color)
        Dim left As Integer = picture.Height \ 2
        Dim right As Integer = picture.Width - picture.Height \ 2
        Dim wanted As Integer = Math.Max(4, (right - left) \ 2)
        Dim top As Integer = LongestInkRun(picture, 0, left, right, ink)
        Dim bottom As Integer = Math.Max(LongestInkRun(picture, picture.Height - 1, left, right, ink), LongestInkRun(picture, picture.Height - 2, left, right, ink))
        If top < wanted OrElse bottom < wanted Then
            missing.Add($"{label}: {top} and {bottom} pixels of {ink.Name} along its top and bottom edges, not {wanted}.")
        End If
    End Sub

    ' A shape drawn among other things, found in the region as the smallest
    ' rectangle holding every pixel the test picks: with an outline, the
    ' outline; without one, only its text.
    Private Shared Sub CheckOutlineWithin(missing As List(Of String), label As String, picture As Bitmap, region As Rectangle, ink As Color, picks As Func(Of Color, Boolean))
        Dim left As Integer = Integer.MaxValue
        Dim top As Integer = Integer.MaxValue
        Dim right As Integer = -1
        Dim bottom As Integer = -1
        For y As Integer = region.Top To region.Bottom - 1
            For x As Integer = region.Left To region.Right - 1
                If picks(picture.GetPixel(x, y)) Then
                    left = Math.Min(left, x)
                    top = Math.Min(top, y)
                    right = Math.Max(right, x)
                    bottom = Math.Max(bottom, y)
                End If
            Next
        Next
        If right < 0 OrElse bottom - top < 2 Then
            missing.Add($"{label}: nothing drawn in {ink.Name} to find it by.")
            Return
        End If
        Using shape As Bitmap = picture.Clone(Rectangle.FromLTRB(left, top, right + 1, bottom + 1), picture.PixelFormat)
            CheckOutline(missing, label, shape, ink)
        End Using
    End Sub

    Private Shared Function LongestInkRun(picture As Bitmap, row As Integer, left As Integer, right As Integer, ink As Color) As Integer
        Dim longest As Integer = 0
        Dim run As Integer = 0
        For x As Integer = Math.Max(0, left) To Math.Min(picture.Width, right) - 1
            Dim pixel As Color = picture.GetPixel(x, row)
            run = If(Distance(pixel, ink) < Distance(pixel, SystemColors.Window), run + 1, 0)
            longest = Math.Max(longest, run)
        Next
        Return longest
    End Function

    Private Shared Function Distance(one As Color, other As Color) As Integer
        Dim red As Integer = CInt(one.R) - CInt(other.R)
        Dim green As Integer = CInt(one.G) - CInt(other.G)
        Dim blue As Integer = CInt(one.B) - CInt(other.B)
        Return red * red + green * green + blue * blue
    End Function

    Private Shared Function SamePicture(expected As Bitmap, actual As Bitmap) As Boolean
        If expected.Size <> actual.Size Then Return False
        For y As Integer = 0 To expected.Height - 1
            For x As Integer = 0 To expected.Width - 1
                If expected.GetPixel(x, y).ToArgb() <> actual.GetPixel(x, y).ToArgb() Then Return False
            Next
        Next
        Return True
    End Function

    ' The color the renderer draws a menu item's text in.
    Private Shared Function TextColorDrawn(renderer As ToolStripRenderer, item As ToolStripItem) As Color
        Using picture As New Bitmap(MenuItemSize.Width, MenuItemSize.Height), g As Graphics = Graphics.FromImage(picture)
            Dim args As New ToolStripItemTextRenderEventArgs(g, item, item.Text, New Rectangle(Point.Empty, MenuItemSize), item.ForeColor, item.Font, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
            renderer.DrawItemText(args)
            Return args.TextColor
        End Using
    End Function

    Private Shared Function TextDrawn(renderer As ToolStripRenderer, item As ToolStripItem) As Bitmap
        Dim picture As New Bitmap(MenuItemSize.Width, MenuItemSize.Height)
        Using g As Graphics = Graphics.FromImage(picture)
            g.Clear(Color.White)
            renderer.DrawItemText(New ToolStripItemTextRenderEventArgs(g, item, item.Text, New Rectangle(Point.Empty, MenuItemSize), item.ForeColor, item.Font, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter))
        End Using
        Return picture
    End Function

    Private Shared Function BackgroundDrawn(renderer As ToolStripRenderer, item As ToolStripItem) As Bitmap
        item.AutoSize = False
        item.Size = MenuItemSize
        Dim picture As New Bitmap(MenuItemSize.Width, MenuItemSize.Height)
        Using g As Graphics = Graphics.FromImage(picture)
            g.Clear(Color.White)
            renderer.DrawMenuItemBackground(New ToolStripItemRenderEventArgs(g, item))
        End Using
        Return picture
    End Function

    Private Shared ReadOnly MenuItemSize As New Size(160, 24)

    ' A kind's pixels as the route map paints it on a card.
    Private Shared Function PaintedSwatch(kind As RouteSegmentKind) As Integer()
        Using image As New Bitmap(48, 24)
            Using g As Graphics = Graphics.FromImage(image)
                g.Clear(UiTheme.CardBackground())
                RouteMapStyle.FillSegment(g, New RectangleF(0, 0, image.Width, image.Height), kind, UiTheme.CardBackground())
            End Using
            Return Enumerable.Range(0, image.Width * image.Height).Select(Function(index) image.GetPixel(index Mod image.Width, index \ image.Width).ToArgb()).ToArray()
        End Using
    End Function

    Private Shared Function Commonest(pixels As Integer()) As Integer
        Return pixels.GroupBy(Function(pixel) pixel).OrderByDescending(Function(group) group.Count()).First().Key
    End Function

    ' Two swatches share a pattern, whatever their colors, when nearly the
    ' same pixels are in each one's commonest color, or nearly the same
    ' pixels are not. A pixel within 2:1 of that color counts as in it:
    ' lines fainter than about the hatch of time not recorded (2.24:1 in
    ' Light) are no pattern a reader can see.
    Private Shared Function SamePattern(one As Integer(), other As Integer()) As Boolean
        Dim oneColor As Color = Color.FromArgb(Commonest(one))
        Dim otherColor As Color = Color.FromArgb(Commonest(other))
        Dim inOne As Boolean() = one.Select(Function(pixel) ContrastRatio(Color.FromArgb(pixel), oneColor) < 2.0).ToArray()
        Dim inOther As Boolean() = other.Select(Function(pixel) ContrastRatio(Color.FromArgb(pixel), otherColor) < 2.0).ToArray()
        Dim differing As Double = Enumerable.Range(0, one.Length).Count(Function(index) inOne(index) <> inOther(index)) / CDbl(one.Length)
        Return differing < 0.1 OrElse differing > 0.9
    End Function

    ' Every color the app chooses for itself, by name: each of the theme's
    ' tokens, a stage's badge, a tag's chip as a card paints it, a grid's
    ' selected rows, and the route map's fills, labels, and markers.
    Private Shared Function ThemeTokens() As List(Of (Name As String, Value As Func(Of Color)))
        Dim tokens As New List(Of (Name As String, Value As Func(Of Color)))()
        Dim add As Action(Of String, Func(Of Color)) = Sub(name, value) tokens.Add((name, value))
        For Each token As MethodInfo In GetType(UiTheme).GetMethods(BindingFlags.Public Or BindingFlags.Static).
            Where(Function(method) method.ReturnType = GetType(Color) AndAlso method.GetParameters().Length = 0)
            add(token.Name, Function() DirectCast(token.Invoke(Nothing, Nothing), Color))
        Next
        For Each stage As PaperStage In [Enum].GetValues(GetType(PaperStage))
            add($"StageBackground({stage})", Function() UiTheme.StageBackground(stage))
            add($"StageForeground({stage})", Function() UiTheme.StageForeground(stage))
        Next
        add("SelectedRowBackground(HoverBackground)", Function() UiTheme.SelectedRowBackground(UiTheme.HoverBackground()))
        add("SelectedRowBackground(AccentMutedBackground)", Function() UiTheme.SelectedRowBackground(UiTheme.AccentMutedBackground()))
        For Each palette As TagPalette In [Enum].GetValues(GetType(TagPalette))
            add($"Tag.Background({palette})", Function() WorkTypeService.Background(palette, UiTheme.IsDark()))
            add($"Tag.Foreground({palette})", Function() WorkTypeService.Foreground(palette, UiTheme.IsDark()))
        Next
        For Each kind As RouteSegmentKind In [Enum].GetValues(GetType(RouteSegmentKind))
            add($"Route.Fill({kind})", Function() RouteMapStyle.Fill(kind))
            add($"Route.LabelColor({kind})", Function() RouteMapStyle.LabelColor(kind))
            If RouteMapStyle.Pattern(kind).HasValue Then add($"Route.Stripe({kind})", Function() RouteMapStyle.Stripe(kind))
        Next
        For Each marker In {("Submitted", RouteMarkerKind.Submitted, EditorialDecision.None), ("Resubmitted", RouteMarkerKind.Resubmitted, EditorialDecision.None),
                            ("Published", RouteMarkerKind.Published, EditorialDecision.None), ("Accepted", RouteMarkerKind.Decision, EditorialDecision.Accepted),
                            ("Revision", RouteMarkerKind.Decision, EditorialDecision.MajorRevision), ("Withdrawn", RouteMarkerKind.Decision, EditorialDecision.Withdrawn),
                            ("Rejected", RouteMarkerKind.Decision, EditorialDecision.Rejected)}
            Dim item As New RouteMarker With {.Kind = marker.Item2, .Decision = marker.Item3}
            add($"Route.MarkerColor({marker.Item1})", Function() RouteMapStyle.MarkerColor(item))
        Next
        add("Route.NumberColor", AddressOf RouteMapStyle.NumberColor)
        Return tokens
    End Function

    ' Each token's color before #138, recorded from that code: its name,
    ' then Light, then Dark. "system" is the system's control text. The
    ' route map's stripes came later, with #140.
    Private Shared ReadOnly TokensBefore As String() = {
        "SubtleBorder E7EEF0 323841",
        "MutedText 64717D 8A95A1",
        "OnAccentText FFFFFF 07201D",
        "WarningMutedBackground FEF3C7 5C3F15",
        "DangerMutedBackground FEE2E2 5B2126",
        "BoardBackground F4F7F8 181B20",
        "HeaderBackground FFFFFF 1F2329",
        "CardBackground FFFFFF 282C33",
        "CardBorder DAE4E7 3D444F",
        "HoverBackground EBF4F5 323841",
        "PrimaryText system system",
        "SecondaryText 5B6977 ACB5C0",
        "BrandNavy 0F172A 0F172A",
        "AccentColor 0F766E 2DD4BF",
        "AccentSecondaryColor 0E7490 22D3EE",
        "AccentMutedBackground CCFBF1 144544",
        "DangerColor BE232D F87C7C",
        "WarningColor B45309 FBBF24",
        "SuccessColor 15794A 6EE7B7",
        "SuccessMutedBackground DCF7E8 153B2B",
        "InfoColor 1D4ED8 93C5FD",
        "InfoMutedBackground DBEAFE 1E3A64",
        "StageBackground(Idea) EDE9FE 483678",
        "StageForeground(Idea) 5B21B6 DDD6FE",
        "StageBackground(Draft) E5E7EB 41444B",
        "StageForeground(Draft) 374151 E5E7EB",
        "StageBackground(Submitted) DBEAFE 1E406E",
        "StageForeground(Submitted) 1D4ED8 BFDBFE",
        "StageBackground(UnderReview) CCFBF1 115E59",
        "StageForeground(UnderReview) 0F766E 99F6E4",
        "StageBackground(Revision) FEF3C7 5C3F15",
        "StageForeground(Revision) B45309 FDE68A",
        "StageBackground(Accepted) DCFCE7 1F4C2F",
        "StageForeground(Accepted) 15803D BBF7D0",
        "StageBackground(InPress) CFFAFE 134E4A",
        "StageForeground(InPress) 0E7490 A5F3FC",
        "StageBackground(Published) D1FAE5 114E3C",
        "StageForeground(Published) 047857 A7F3D0",
        "SelectedRowBackground(HoverBackground) EBF4F5 323841",
        "SelectedRowBackground(AccentMutedBackground) CCFBF1 144544",
        "SelectedRowText system system",
        "Tag.Background(Teal) CCFBF1 134E4A",
        "Tag.Foreground(Teal) 115E59 99F6E4",
        "Tag.Background(Blue) DBEAFE 1E3A8A",
        "Tag.Foreground(Blue) 1E40AF BFDBFE",
        "Tag.Background(Violet) EDE9FE 4C1D95",
        "Tag.Foreground(Violet) 5B21B6 DDD6FE",
        "Tag.Background(Rose) FFE4E6 881337",
        "Tag.Foreground(Rose) 9F1239 FECDD3",
        "Tag.Background(Amber) FEF3C7 78350F",
        "Tag.Foreground(Amber) 92400E FDE68A",
        "Tag.Background(Green) DCFCE7 14532D",
        "Tag.Foreground(Green) 166534 BBF7D0",
        "Tag.Background(Slate) E2E8F0 334155",
        "Tag.Foreground(Slate) 334155 E2E8F0",
        "Tag.Background(Orange) FFEDD5 7C2D12",
        "Tag.Foreground(Orange) 9A3412 FED7AA",
        "Route.Fill(Journal) 0F766E 0F766E",
        "Route.LabelColor(Journal) FFFFFF FFFFFF",
        "Route.Fill(Author) B45309 B45309",
        "Route.LabelColor(Author) FFFFFF FFFFFF",
        "Route.Stripe(Author) 482104 482104",
        "Route.Fill(Production) 475569 64748B",
        "Route.LabelColor(Production) FFFFFF FFFFFF",
        "Route.Stripe(Production) 1C222A 282E38",
        "Route.Fill(NotRecorded) A0B0B5 5C7076",
        "Route.LabelColor(NotRecorded) FFFFFF FFFFFF",
        "Route.MarkerColor(Submitted) 1D4ED8 1D4ED8",
        "Route.MarkerColor(Resubmitted) 1D4ED8 1D4ED8",
        "Route.MarkerColor(Published) 0F766E 0F766E",
        "Route.MarkerColor(Accepted) 15803D 15803D",
        "Route.MarkerColor(Revision) B45309 B45309",
        "Route.MarkerColor(Withdrawn) 475569 475569",
        "Route.MarkerColor(Rejected) B91C1C B91C1C",
        "Route.NumberColor FFFFFF FFFFFF"
    }

    Private Shared Function NamedControl(dialog As Form, name As String) As Control
        Return Descendants(dialog).Single(Function(item) item.AccessibleName = name)
    End Function

    Private Shared Function ButtonWithText(dialog As Form, text As String) As Button
        Return Descendants(dialog).OfType(Of Button)().Single(Function(item) item.Text = text)
    End Function

    Private Shared Function FileListIn(dialog As Form) As ListBox
        Return NamedControl(dialog, "Selected file details").Parent.Controls.OfType(Of ListBox)().Single()
    End Function

    Private Shared Sub SelectFile(list As ListBox, label As String)
        list.SelectedIndex = Enumerable.Range(0, list.Items.Count).Single(Function(index) list.Items(index).ToString().Contains(label))
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

    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The disposable integrity check timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private NotInheritable Class SmokeFixture
        Implements IDisposable

        Private ReadOnly _root As String = CreateTemporaryRoot()
        Private ReadOnly _lockedFile As FileStream
        Public ReadOnly Property Manuscript As New Manuscript With {.Title = "Invisible theme and keyboard fixture", .CurrentStage = PaperStage.Submitted}
        Public ReadOnly Property Library As New AuthorLibraryData()
        Public ReadOnly Property Profile As ManuscriptReadiness
        Public ReadOnly Property Packet As SubmissionPacket
        Public ReadOnly Property Submission As JournalSubmission
        Public ReadOnly Property NoFingerprint As SubmissionPacketFile

        Public Sub New()
            Dim journal As New JournalRecord With {.Name = "Synthetic theme journal", .SubmissionPortalUrl = "https://theme.example.invalid/submit"}
            Library.Journals.Add(journal)
            Profile = New ManuscriptReadiness With {.JournalId = journal.Id, .JournalName = journal.Name}
            For index As Integer = 0 To 3
                Profile.Items.Add(New ReadinessItemState With {
                    .Title = "Requirement " & index, .Description = "Readable preparation instructions.",
                    .UserNotes = "Disposable notes remain available with keyboard navigation.", .SortOrder = index
                })
            Next
            Manuscript.ReadinessProfiles.Add(Profile)
            Dim version As New ManuscriptVersion With {.Label = "Exact keyboard fixture version"}
            Manuscript.Versions.Add(version)
            Manuscript.CurrentVersionId = version.Id
            Submission = New JournalSubmission With {
                .JournalName = journal.Name, .JournalId = journal.Id, .ManuscriptNumber = "DEMO-THEME-1",
                .PortalUrl = journal.SubmissionPortalUrl, .Notes = "Fictional submission with no external activity."
            }
            Manuscript.Submissions.Add(Submission)
            Packet = SubmissionPacketService.CreatePacket(Manuscript, version.Id, "Themed packet", "Disposable sample", Profile.Id, Submission.Id)
            Packet.Files.Add(New SubmissionPacketFile With {.Label = "01 metadata fixture", .Notes = "Metadata-only packet record."})
            Packet.Files.Add(New SubmissionPacketFile With {
                .Label = "02 missing fixture", .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                .LocalFilePath = Path.Combine(_root, "missing.txt")
            })
            NoFingerprint = AddLocal("03 no fingerprint fixture", False)
            AddLocal("04 unchanged fixture", True)
            Dim changed As SubmissionPacketFile = AddLocal("05 changed fixture", True)
            File.WriteAllText(changed.LocalFilePath, "xyz")
            Dim unavailable As SubmissionPacketFile = AddLocal("06 unavailable fixture", True)
            _lockedFile = New FileStream(unavailable.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        End Sub

        Private Function AddLocal(label As String, capture As Boolean) As SubmissionPacketFile
            Dim path As String = System.IO.Path.Combine(_root, label & ".txt")
            File.WriteAllText(path, "abc")
            Dim packetFile As New SubmissionPacketFile With {
                .Label = label, .LocalFilePath = path, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
            }
            If capture Then Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.CaptureBaseline(packetFile).Status)
            Packet.Files.Add(packetFile)
            Return packetFile
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _lockedFile?.Dispose()
            DeleteTemporaryRoot(_root)
        End Sub
    End Class

    Private Interface IKeyboardProbe
        Function Press(key As Keys) As Boolean
    End Interface

    Private Class ReadinessProbe
        Inherits ManuscriptReadinessForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, fixture.Library, New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Readiness, .ReadinessProfileId = fixture.Profile.Id
            })
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class VaultProbe
        Inherits SubmissionPacketVaultForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Packets, .PacketId = fixture.Packet.Id, .VersionId = fixture.Packet.ManuscriptVersionId
            })
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class SubmissionProbe
        Inherits SubmissionDetailsForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, fixture.Submission, True)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

End Class
