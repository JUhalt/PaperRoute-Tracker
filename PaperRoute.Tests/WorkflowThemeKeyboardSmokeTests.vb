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

    ' The board, rail, and cards draw their text from these tokens directly.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub WorkspaceTextTokens_AreLegibleOnEverySurface(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                For Each surface In {("board", UiTheme.BoardBackground()), ("card", UiTheme.CardBackground())}
                    For Each ink In {("primary", UiTheme.PrimaryText()), ("secondary", UiTheme.SecondaryText()), ("muted", UiTheme.MutedText())}
                        AssertContrast(ink.Item2, surface.Item2, $"{ink.Item1} text on the {surface.Item1} background")
                    Next
                Next
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

    Private Shared Function DeepestActiveControl(dialog As Form) As Control
        Dim active As Control = dialog.ActiveControl
        While TypeOf active Is ContainerControl AndAlso DirectCast(active, ContainerControl).ActiveControl IsNot Nothing
            active = DirectCast(active, ContainerControl).ActiveControl
        End While
        Return active
    End Function

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

    Private Shared Sub AssertContrast(foreground As Color, background As Color, label As String)
        Dim ratio As Double = ContrastRatio(foreground, background)
        Assert.IsTrue(ratio >= 4.5,
            $"Normal-size text needs 4.5:1 contrast; '{label.Substring(0, Math.Min(90, label.Length))}' has {ratio:F2}:1 ({foreground} on {background}).")
    End Sub

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
