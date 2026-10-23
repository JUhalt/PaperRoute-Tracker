Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The journal shortlist on the manuscript page, as a researcher meets it by
' keyboard, by screen reader and by eye (#65, #87, #88, #133): what Tab,
' Shift+Tab and the arrow keys reach, what Enter and Space do on each row's
' Edit... and ⋯, where focus goes after each action, what a screen reader
' reads in what order and what each item's default action does, the ⋯
' menu, the evidence's tooltip, the text's contrast, and every item inside
' the shortlist at three text sizes. Written against focus, the accessible
' tree and the picture, not against the controls that draw the rows, so it
' holds whatever draws them.
<TestClass>
<DoNotParallelize>
Public Class JournalShortlistRowsTests

    Private _root As String = String.Empty

    Private Const WM_KEYDOWN As UInteger = &H100
    Private Const WM_KEYUP As UInteger = &H101
    Private Const WM_MOUSEMOVE As UInteger = &H200
    Private Const VK_SHIFT As Integer = &H10

    Private Shared ReadOnly Journals As String() = {"Fictional Journal 01", "Fictional Journal 02", "Fictional Journal 03"}


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        UiTheme.HighContrastOverride = Nothing
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' Tab and Shift+Tab across the Overview, through the rows, and the
    ' arrow keys inside a row.
    <TestMethod>
    Public Sub TheKeyboardReachesEachRowsActionsInOrder()

        Dim report As New List(Of String)()

        OnShortlistPage(
            Sub(board)
                board.ActiveControl = board.Field("Title")
                Application.DoEvents()
                report.Add("start: " & FocusName(board))
                For steps As Integer = 1 To 14
                    Press(board, Keys.Tab)
                    report.Add("Tab: " & FocusName(board))
                Next
                For steps As Integer = 1 To 14
                    PressWithShift(board, Keys.Tab)
                    report.Add("Shift+Tab: " & FocusName(board))
                Next

                For Each start As String In {"Edit " & Journals(1), "More actions for " & Journals(1)}
                    FocusItem(board, start)
                    report.Add("on: " & FocusName(board))
                    For Each key As Keys In {Keys.Right, Keys.Right, Keys.Left, Keys.Down, Keys.Up, Keys.Up}
                        Press(board, key)
                        report.Add(key.ToString() & ": " & FocusName(board))
                    Next
                Next
            End Sub)

        AssertPinned(KeyboardExpected, report)

    End Sub


    ' Enter and Space on Edit... ask about that journal, and on ⋯ open
    ' its menu, with each item enabled as the row's place allows.
    <TestMethod>
    Public Sub EnterAndSpacePressEachRowsActions()

        Dim report As New List(Of String)()

        OnShortlistPage(
            Sub(board)
                Dim editor As EditManuscriptForm = board.Editor
                editor.candidatePrompt =
                    Function(existing)
                        report.Add("  asked about " & If(existing?.JournalName, "a new journal"))
                        Return Nothing
                    End Function

                For Each journal As String In Journals
                    For Each key As Keys In {Keys.Enter, Keys.Space}
                        FocusItem(board, "Edit " & journal)
                        report.Add(key.ToString() & " on Edit " & journal)
                        PressKey(board, key)
                        report.Add("  focus " & FocusName(board))

                        FocusItem(board, "More actions for " & journal)
                        report.Add(key.ToString() & " on More actions for " & journal)
                        Using watch As New MenuWatch()
                            PressKey(board, key)
                            report.Add("  menu " & watch.Describe())
                        End Using
                        report.Add("  focus " & FocusName(board))
                    Next
                Next

                editor.candidatePrompt = Nothing
            End Sub)

        AssertPinned(PressExpected, report)

    End Sub


    ' Where focus is after each row action taken from the keyboard: Edit
    ' with a change, and each item of the ⋯ menu.
    <TestMethod>
    Public Sub FocusAfterEachRowAction()

        Dim report As New List(Of String)()

        OnShortlistPage(
            Sub(board)
                Dim editor As EditManuscriptForm = board.Editor

                ' Edit..., changing the journal's notes.
                editor.candidatePrompt =
                    Function(existing)
                        Return New JournalCandidate With {.JournalName = existing.JournalName, .Status = existing.Status, .Notes = "Edited from the keyboard.", .Checks = existing.Checks.ToList()}
                    End Function
                FocusItem(board, "Edit " & Journals(1))
                PressKey(board, Keys.Enter)
                report.Add("Edit " & Journals(1) & ": focus " & FocusName(board) & "; rows " & RowNames(board))
                editor.candidatePrompt = Nothing
                board.Discard()

                For Each action In {("Move Up", Journals(1)), ("Move Down", Journals(1)), ("Make Target Journal", Journals(1)),
                                    ("Remove from Shortlist", Journals(1)), ("Move Up", Journals(2)), ("Move Down", Journals(0)),
                                    ("Remove from Shortlist", Journals(2))}
                    FocusItem(board, "More actions for " & action.Item2)
                    Using watch As New MenuWatch()
                        PressKey(board, Keys.Enter)
                        Dim item As ToolStripItem = watch.Item(action.Item1)
                        If item Is Nothing Then
                            report.Add(action.Item1 & " on " & action.Item2 & ": no such item (" & watch.Describe() & ")")
                            Continue For
                        End If
                        item.PerformClick()
                    End Using
                    Application.DoEvents()
                    report.Add(action.Item1 & " on " & action.Item2 & ": focus " & FocusName(board) & "; rows " & RowNames(board))
                    board.Discard()
                Next
            End Sub)

        AssertPinned(AfterActionExpected, report)

    End Sub


    ' What a screen reader reads under the shortlist, in its order, and
    ' what each item's default action does.
    <TestMethod>
    Public Sub AScreenReaderReadsEachRowInOrder()

        Dim report As New List(Of String)()

        OnShortlistPage(
            Sub(board)
                Dim shortlist As Control = ShortlistOf(board)
                report.Add("the shortlist: " & Describe(shortlist.AccessibilityObject))
                Dim items As List(Of AccessibleObject) = ReadingOrder(shortlist)
                For Each item As AccessibleObject In items
                    report.Add(Describe(item))
                Next

                Dim editor As EditManuscriptForm = board.Editor
                For Each item As AccessibleObject In items.Where(Function(candidate) Not String.IsNullOrEmpty(candidate.DefaultAction)).ToList()
                    Dim asked As String = String.Empty
                    editor.candidatePrompt =
                        Function(existing)
                            asked = If(existing?.JournalName, "a new journal")
                            Return Nothing
                        End Function
                    Dim name As String = item.Name
                    Using watch As New MenuWatch()
                        item.DoDefaultAction()
                        Application.DoEvents()
                        report.Add(item.DefaultAction & " " & name & ": " & If(asked.Length > 0, "asked about " & asked, "menu " & watch.Describe()))
                    End Using
                    editor.candidatePrompt = Nothing
                Next
            End Sub)

        AssertPinned(ReaderExpected, report)

    End Sub


    ' The evidence line's tooltip names the keywords and an example, when
    ' the pointer is over it.
    <TestMethod>
    Public Sub TheEvidenceShowsItsKeywordsOnHover()

        Dim tip As String = String.Empty
        Dim elsewhere As String = String.Empty

        OnShortlistPage(
            Sub(board)
                Dim shortlist As Control = ShortlistOf(board)
                Dim evidence As AccessibleObject = ReadingOrder(shortlist).Single(Function(item) item.Role = AccessibleRole.StaticText AndAlso If(item.Name, String.Empty).StartsWith("Found by keyword search", StringComparison.Ordinal))
                Dim tips As ToolTip = PrivateField(Of ToolTip)(board.Editor, "shortlistToolTip")
                tip = HoverTip(shortlist, tips, evidence.Bounds)
                Dim name As AccessibleObject = ReadingOrder(shortlist).First(Function(item) item.Role = AccessibleRole.StaticText AndAlso If(item.Name, String.Empty).StartsWith(Journals(0), StringComparison.Ordinal))
                elsewhere = HoverTip(shortlist, tips, name.Bounds)
            End Sub)

        Assert.AreEqual("Keywords: ""anchoring"" AND ""risk""" & Environment.NewLine & "For example: Anchoring in risk estimates", tip)
        Assert.AreEqual(String.Empty, elsewhere, "The journal's name has no tooltip.")

    End Sub


    ' The rows' text stands out from what it is drawn on, 4.5:1 or more, in
    ' Light and Dark; under a contrast theme it is drawn on the user's
    ' window or button color, in the user's own inks, whose contrast is the
    ' user's choice (#138).
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    Public Sub TheRowsTextIsLegible(mode As SystemColorMode, contrastTheme As Boolean)

        Dim failures As New List(Of String)()
        Dim judged As Integer = 0

        OnShortlistPage(
            Sub(board)
                Dim shortlist As Control = ShortlistOf(board)
                Using picture As New Bitmap(shortlist.Width, shortlist.Height)
                    shortlist.DrawToBitmap(picture, New Rectangle(Point.Empty, shortlist.Size))
                    Dim origin As Point = shortlist.PointToScreen(Point.Empty)
                    For Each item As AccessibleObject In ReadingOrder(shortlist).Where(Function(candidate) candidate.Role = AccessibleRole.StaticText OrElse candidate.Role = AccessibleRole.PushButton)
                        If String.IsNullOrEmpty(item.Name) Then Continue For
                        Dim bounds As Rectangle = item.Bounds
                        bounds.Offset(-origin.X, -origin.Y)
                        If item.Role = AccessibleRole.PushButton Then bounds.Inflate(-shortlist.LogicalToDeviceUnits(6), -shortlist.LogicalToDeviceUnits(6))
                        bounds.Intersect(New Rectangle(Point.Empty, picture.Size))
                        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
                            failures.Add(item.Name & " is not drawn inside the shortlist")
                            Continue For
                        End If
                        Dim seen As (Ratio As Double, Surface As Color, Ink As Color) = Contrast(picture, bounds)
                        judged += 1
                        If contrastTheme Then
                            If Not IsUserColor(seen.Surface) Then
                                failures.Add(item.Name & " is drawn on " & seen.Surface.ToString() & ", not one of the user's colors")
                            End If
                        ElseIf seen.Ratio < 4.5 Then
                            failures.Add(item.Name & ": " & seen.Ratio.ToString("F2", CultureInfo.InvariantCulture) & ":1, " & seen.Ink.ToString() & " on " & seen.Surface.ToString())
                        End If
                    Next
                End Using
            End Sub,
            mode:=mode,
            contrastTheme:=contrastTheme)

        Assert.IsTrue(judged >= 12, "Text judged: " & judged.ToString())
        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' At each text size every item lies inside the shortlist, a row's items
    ' never overlap, and a row's text is not cut off.
    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub EveryItemFitsInsideTheShortlist(fontScale As Single)

        Dim failures As New List(Of String)()

        OnShortlistPage(
            Sub(board)
                Dim shortlist As Control = ShortlistOf(board)
                Dim area As Rectangle = shortlist.RectangleToScreen(shortlist.ClientRectangle)
                Dim items As List(Of AccessibleObject) = ReadingOrder(shortlist)
                Dim row As New List(Of AccessibleObject)()

                For Each item As AccessibleObject In items.Concat({CType(Nothing, AccessibleObject)})
                    If item Is Nothing OrElse item.Role = AccessibleRole.ListItem Then
                        For Each first As AccessibleObject In row
                            For Each second As AccessibleObject In row.Where(Function(other) other IsNot first AndAlso other.Role <> AccessibleRole.ListItem)
                                If first.Role <> AccessibleRole.ListItem AndAlso first.Bounds.IntersectsWith(second.Bounds) AndAlso
                                   String.CompareOrdinal(first.Name, second.Name) < 0 Then
                                    failures.Add(first.Name & " overlaps " & second.Name)
                                End If
                            Next
                        Next
                        row.Clear()
                    End If
                    If item Is Nothing Then Exit For
                    row.Add(item)
                    If item.Bounds.Width <= 0 OrElse item.Bounds.Height <= 0 Then
                        failures.Add(item.Name & " has no size")
                    ElseIf Not area.Contains(item.Bounds) Then
                        failures.Add(item.Name & " at " & item.Bounds.ToString() & " is outside the shortlist at " & area.ToString())
                    End If
                    ' The lines the text takes at the item's width, with one
                    ' font height of slack for the glyph padding a measure adds
                    ' and a label leaves out: a line cut off is a font height.
                    If item.Role = AccessibleRole.StaticText AndAlso Not String.IsNullOrEmpty(item.Name) Then
                        Dim needed As Size = TextRenderer.MeasureText(item.Name, shortlist.Font, New Size(item.Bounds.Width + shortlist.Font.Height, Integer.MaxValue), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
                        If needed.Height > item.Bounds.Height + 1 Then
                            failures.Add(item.Name & " needs " & needed.Height.ToString() & " px, has " & item.Bounds.Height.ToString())
                        End If
                    End If
                Next

                If items.Where(Function(item) item.Role = AccessibleRole.ListItem).Count() <> 3 Then failures.Add("Rows read: " & items.Where(Function(item) item.Role = AccessibleRole.ListItem).Count().ToString())
            End Sub,
            fontScale:=fontScale)

        Assert.AreEqual(0, failures.Count, "At " & fontScale.ToString(CultureInfo.InvariantCulture) & "x:" & Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub


    ' ---------------------------------------------------------------
    ' The page and its shortlist
    ' ---------------------------------------------------------------

    ' A Pipeline manuscript whose shortlist holds the three journals: the
    ' first preferred and the target, with its facts from the Journals
    ' page; the second ruled out, so struck out; the third found by Find
    ' Journals, with its evidence and notes. The page shows it on the
    ' Overview, in the window's own size.
    Private Sub OnShortlistPage(
        work As Action(Of LibraryBoard),
        Optional mode As SystemColorMode = SystemColorMode.Classic,
        Optional contrastTheme As Boolean = False,
        Optional fontScale As Single = 1.0F
    )

        Dim library As LargeLibraryFixture.SyntheticLibrary = LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)
        Dim authors As AuthorLibraryData = library.AuthorRepository.Load()
        Dim records As List(Of JournalRecord) = Journals.Select(Function(name) authors.Journals.Single(Function(item) item.Name = name)).ToList()

        records(0).Facts.Add(New JournalFact With {.Key = JournalFactCatalog.OpenAccess, .Value = "Fully open access", .Source = "Example"})
        records(0).Facts.Add(New JournalFact With {.Key = JournalFactCatalog.Apc, .Value = "1,200 USD", .Source = "Example"})
        library.AuthorRepository.Save(authors)

        Dim subject As Manuscript = library.Manuscripts.First(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso item.Tags.Count > 0)
        subject.TargetJournal = Journals(0)
        subject.TargetJournalId = records(0).Id
        subject.JournalShortlist = New List(Of JournalCandidate) From {
            New JournalCandidate With {.JournalName = Journals(0), .JournalId = records(0).Id, .Status = CandidateStatus.Preferred,
                                       .Notes = "Fits the scope.", .Checks = New List(Of String) From {"trust.known", "fit.scope"}},
            New JournalCandidate With {.JournalName = Journals(1), .JournalId = records(1).Id, .Status = CandidateStatus.RuledOut,
                                       .Notes = "Too slow for this one."},
            New JournalCandidate With {.JournalName = Journals(2), .JournalId = records(2).Id, .Status = CandidateStatus.Considering,
                                       .Notes = "Suggested by Find Journals.",
                                       .Evidence = New CandidateEvidence With {
                                           .Source = "Example", .Keywords = New List(Of String) From {"anchoring", "risk"}, .MatchAll = True,
                                           .SinceDate = New DateTime(2021, 1, 1), .MatchingArticles = 14, .AllArticles = 1200,
                                           .Examples = New List(Of EvidenceExample) From {New EvidenceExample With {.Title = "Anchoring in risk estimates", .Year = 2024}}}}
        }
        library.Repository.Save(library.Manuscripts)

        StaRunner.Run(
            Sub()
                If mode = SystemColorMode.Dark AndAlso (SystemInformation.HighContrast OrElse Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) Then
                    Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later, outside a Windows contrast theme.")
                End If

                UiTheme.HighContrastOverride = If(contrastTheme, True, CType(Nothing, Boolean?))
                Try
                    Using board As New LibraryBoard(library, Path.Combine(_root, "settings"), fontScale)
                        board.Start()
                        board.WaitForIdleWork()
                        board.Open(board.Library.Single(Function(item) item.Id = subject.Id))
                        Application.DoEvents()
                        board.WaitForIdleWork()
                        work(board)
                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub,
            TimeSpan.FromMinutes(5),
            mode)

    End Sub


    Private Shared Function ShortlistOf(board As LibraryBoard) As Control
        Return PrivateField(Of Control)(board.Editor, "shortlistRows")
    End Function


    Private Shared Function PrivateField(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(target), T)
    End Function


    ' The rows' journals, as a screen reader reads them, in order.
    Private Shared Function RowNames(board As LibraryBoard) As String
        Return String.Join(", ", ReadingOrder(ShortlistOf(board)).Where(Function(item) item.Role = AccessibleRole.ListItem).Select(Function(item) item.Name))
    End Function


    ' Every named item under a control, depth first, as a screen reader
    ' walks it: a control that draws its items gives them as its children;
    ' a panel with no name of its own is read through.
    Friend Shared Function ReadingOrder(root As Control) As List(Of AccessibleObject)

        Dim items As New List(Of AccessibleObject)()

        If DrawsItsChildren(root) Then
            AddDrawnChildren(root.AccessibilityObject, items)
            Return items
        End If

        ' A layout panel given no name of its own is read through: Windows
        ' would name it after the label before it, which says nothing.
        For Each child As Control In root.Controls
            If Not child.Visible Then Continue For
            If Not TypeOf child Is Panel OrElse Not String.IsNullOrEmpty(child.AccessibleName) Then
                items.Add(child.AccessibilityObject)
            End If
            items.AddRange(ReadingOrder(child))
        Next

        Return items

    End Function


    ' A control of PaperRoute's own that draws its items and gives them to
    ' a screen reader as children of its own accessible object, rather than
    ' hosting a control for each (#133). Windows gives every window an
    ' accessible object with a child count of its own, so the count alone
    ' does not tell a drawn list from a panel of controls.
    Private Shared Function DrawsItsChildren(control As Control) As Boolean
        Dim accessible As AccessibleObject = control.AccessibilityObject
        Return accessible.GetType().Assembly Is GetType(Form1).Assembly AndAlso accessible.GetChildCount() > 0
    End Function


    Private Shared Sub AddDrawn(item As AccessibleObject, items As List(Of AccessibleObject))
        If item Is Nothing Then Return
        items.Add(item)
        Dim count As Integer = item.GetChildCount()
        For index As Integer = 0 To count - 1
            AddDrawn(item.GetChild(index), items)
        Next
    End Sub


    Private Shared Function Describe(item As AccessibleObject) As String
        Return item.Role.ToString() & " '" & If(item.Name, String.Empty) & "'" &
            If(String.IsNullOrEmpty(item.Description), String.Empty, " described '" & item.Description & "'") &
            If(String.IsNullOrEmpty(item.DefaultAction), String.Empty, " action '" & item.DefaultAction & "'") &
            If((item.State And AccessibleStates.Focusable) <> 0, " focusable", String.Empty)
    End Function


    ' Where the keyboard is: the item a control that draws its items has
    ' focused, or the focused control itself.
    Private Shared Function FocusName(board As LibraryBoard) As String
        Dim control As Control = DeepestActiveControl(board)
        If control Is Nothing Then Return "nothing"
        Dim own As AccessibleObject = control.AccessibilityObject
        Dim item As AccessibleObject = If(DrawsItsChildren(control), If(own.GetFocused(), own), own)
        Return item.Role.ToString() & " '" & If(item.Name, String.Empty) & "'"
    End Function


    ' Puts the keyboard on the item a screen reader reads by this name: the
    ' control itself, or the item of a control that draws it.
    Private Shared Sub FocusItem(board As LibraryBoard, name As String)

        Dim shown As List(Of Control) = ViewCatalog.Descendants(board.Editor).Where(Function(control) control.Visible).ToList()

        Dim own As Control = shown.FirstOrDefault(Function(control) control.AccessibleName = name AndAlso control.CanSelect)
        If own IsNot Nothing Then
            board.ActiveControl = own
            Application.DoEvents()
            Assert.AreEqual(AccessibleRole.PushButton.ToString() & " '" & name & "'", FocusName(board), "The keyboard is on " & name & ".")
            Return
        End If

        For Each control As Control In shown.Where(AddressOf DrawsItsChildren)
            Dim drawn As List(Of AccessibleObject) = New List(Of AccessibleObject)()
            AddDrawnChildren(control.AccessibilityObject, drawn)
            Dim item As AccessibleObject = drawn.FirstOrDefault(Function(candidate) candidate.Name = name)
            If item IsNot Nothing Then
                board.ActiveControl = control
                item.Select(AccessibleSelection.TakeFocus)
                Application.DoEvents()
                Assert.AreEqual(AccessibleRole.PushButton.ToString() & " '" & name & "'", FocusName(board), "The keyboard is on " & name & ".")
                Return
            End If
        Next

        Assert.Fail(name & " is not on the page.")

    End Sub


    Private Shared Sub AddDrawnChildren(accessible As AccessibleObject, items As List(Of AccessibleObject))
        For index As Integer = 0 To accessible.GetChildCount() - 1
            AddDrawn(accessible.GetChild(index), items)
        Next
    End Sub


    ' A key as Windows delivers it: down, and for Space, up again, as a
    ' button acts on Space only once it is let go.
    Private Shared Sub PressKey(board As LibraryBoard, key As Keys)
        Dim target As Control = If(DeepestActiveControl(board), board)
        PostMessage(target.Handle, WM_KEYDOWN, New IntPtr(CInt(key)), New IntPtr(1))
        If key = Keys.Space Then PostMessage(target.Handle, WM_KEYUP, New IntPtr(CInt(key)), New IntPtr(&HC0000001))
        Application.DoEvents()
    End Sub


    Private Shared Sub Press(board As LibraryBoard, key As Keys)
        Dim target As Control = If(DeepestActiveControl(board), board)
        PostMessage(target.Handle, WM_KEYDOWN, New IntPtr(CInt(key)), New IntPtr(1))
        Application.DoEvents()
    End Sub


    ' Shift held in this thread's key state only, where WinForms reads it.
    Private Shared Sub PressWithShift(board As LibraryBoard, key As Keys)
        Dim state(255) As Byte
        GetKeyboardState(state)
        Dim before As Byte = state(VK_SHIFT)
        state(VK_SHIFT) = CByte(before Or &H80)
        SetKeyboardState(state)
        Try
            Press(board, key)
        Finally
            GetKeyboardState(state)
            state(VK_SHIFT) = before
            SetKeyboardState(state)
        End Try
    End Sub


    ' The tooltip shown for a place under the pointer: the pointer moves
    ' over the control drawn there, and its tooltip is read.
    Private Shared Function HoverTip(shortlist As Control, tips As ToolTip, bounds As Rectangle) As String
        Dim spot As New Point(bounds.Left + Math.Min(8, bounds.Width \ 2), bounds.Top + bounds.Height \ 2)
        Dim under As Control = shortlist
        Do
            Dim inner As Control = under.Controls.Cast(Of Control)().FirstOrDefault(Function(child) child.Visible AndAlso child.RectangleToScreen(child.ClientRectangle).Contains(spot))
            If inner Is Nothing Then Exit Do
            under = inner
        Loop
        Dim local As Point = under.PointToClient(spot)
        PostMessage(under.Handle, WM_MOUSEMOVE, IntPtr.Zero, New IntPtr((local.Y << 16) Or (local.X And &HFFFF)))
        Application.DoEvents()
        Return If(tips.GetToolTip(under), String.Empty)
    End Function


    ' How far the strongest pixel stands out from the commonest one, which
    ' is the surface the text is drawn on.
    Private Shared Function Contrast(picture As Bitmap, area As Rectangle) As (Ratio As Double, Surface As Color, Ink As Color)
        Dim counts As New Dictionary(Of Integer, Integer)()
        For y As Integer = area.Top To area.Bottom - 1
            For x As Integer = area.Left To area.Right - 1
                Dim pixel As Integer = picture.GetPixel(x, y).ToArgb()
                Dim count As Integer = 0
                counts.TryGetValue(pixel, count)
                counts(pixel) = count + 1
            Next
        Next
        Dim surface As Color = Color.FromArgb(counts.OrderByDescending(Function(entry) entry.Value).First().Key)
        Dim ink As Color = Color.FromArgb(counts.Keys.OrderByDescending(Function(pixel) ContrastRatio(Color.FromArgb(pixel), surface)).First())
        Return (ContrastRatio(ink, surface), surface, ink)
    End Function


    ' A color the user's theme sets: a window, button or highlight color.
    Private Shared Function IsUserColor(color As Color) As Boolean
        Return {SystemColors.Window, SystemColors.Control, SystemColors.ButtonFace, SystemColors.Highlight, SystemColors.Info}.
            Any(Function(system) system.ToArgb() = color.ToArgb())
    End Function


    ' What the pins name: each line as the shortlist gave it on f5c6947,
    ' or, until then, the lines found.
    Private Shared Sub AssertPinned(expected As List(Of String), actual As List(Of String))
        CollectionAssert.AreEqual(expected, actual, Environment.NewLine & String.Join(Environment.NewLine, actual))
    End Sub


    ' The menus open on the test's thread while it is alive: each one shown
    ' is recorded with its items, and closed at the end.
    Private NotInheritable Class MenuWatch
        Implements IDisposable

        Public Function Shown() As List(Of ToolStripDropDown)
            Dim found As New List(Of ToolStripDropDown)()
            EnumThreadWindows(GetCurrentThreadId(),
                Function(window, parameter)
                    Dim menu As ToolStripDropDown = TryCast(Control.FromHandle(window), ToolStripDropDown)
                    If menu IsNot Nothing AndAlso menu.Visible Then found.Add(menu)
                    Return True
                End Function, IntPtr.Zero)
            Return found
        End Function

        Public Function Describe() As String
            Dim menus As List(Of ToolStripDropDown) = Shown()
            If menus.Count = 0 Then Return "(none)"
            Return String.Join(" / ", menus.Select(Function(menu) String.Join(", ", menu.Items.Cast(Of ToolStripItem)().
                Select(Function(item) If(TypeOf item Is ToolStripSeparator, "-", item.Text & If(item.Enabled, String.Empty, " (off)"))))))
        End Function

        Public Function Item(text As String) As ToolStripItem
            Return Shown().SelectMany(Function(menu) menu.Items.Cast(Of ToolStripItem)()).FirstOrDefault(Function(candidate) candidate.Text = text)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            For Each menu As ToolStripDropDown In Shown()
                menu.Close()
            Next
            Application.DoEvents()
        End Sub

    End Class


    ' ---------------------------------------------------------------
    ' Pins (f5c6947)
    ' ---------------------------------------------------------------

    ' Tab from the title reaches each row's Edit... and ⋯ in turn, then the
    ' shortlist's own buttons; the arrow keys stay within a row's two
    ' buttons, wrapping.
    Private Shared ReadOnly KeyboardExpected As New List(Of String) From {
        "start: Text 'Title'",
        "Tab: Text 'Target journal'",
        "Tab: ComboBox 'Current stage'",
        "Tab: PushButton 'DOI & Crossref Metadata...'",
        "Tab: PushButton 'Journal, Preprint & Links...'",
        "Tab: PushButton 'Edit Fictional Journal 01'",
        "Tab: PushButton 'More actions for Fictional Journal 01'",
        "Tab: PushButton 'Edit Fictional Journal 02'",
        "Tab: PushButton 'More actions for Fictional Journal 02'",
        "Tab: PushButton 'Edit Fictional Journal 03'",
        "Tab: PushButton 'More actions for Fictional Journal 03'",
        "Tab: PushButton 'Add Journal...'",
        "Tab: PushButton 'Find Journals...'",
        "Tab: StaticText 'How to choose a journal'",
        "Tab: ComboBox 'Type of work'",
        "Shift+Tab: StaticText 'How to choose a journal'",
        "Shift+Tab: PushButton 'Find Journals...'",
        "Shift+Tab: PushButton 'Add Journal...'",
        "Shift+Tab: PushButton 'More actions for Fictional Journal 03'",
        "Shift+Tab: PushButton 'Edit Fictional Journal 03'",
        "Shift+Tab: PushButton 'More actions for Fictional Journal 02'",
        "Shift+Tab: PushButton 'Edit Fictional Journal 02'",
        "Shift+Tab: PushButton 'More actions for Fictional Journal 01'",
        "Shift+Tab: PushButton 'Edit Fictional Journal 01'",
        "Shift+Tab: PushButton 'Journal, Preprint & Links...'",
        "Shift+Tab: PushButton 'DOI & Crossref Metadata...'",
        "Shift+Tab: ComboBox 'Current stage'",
        "Shift+Tab: Text 'Target journal'",
        "Shift+Tab: Text 'Title'",
        "on: PushButton 'Edit Fictional Journal 02'",
        "Right: PushButton 'More actions for Fictional Journal 02'",
        "Right: PushButton 'Edit Fictional Journal 02'",
        "Left: PushButton 'More actions for Fictional Journal 02'",
        "Down: PushButton 'Edit Fictional Journal 02'",
        "Up: PushButton 'More actions for Fictional Journal 02'",
        "Up: PushButton 'Edit Fictional Journal 02'",
        "on: PushButton 'More actions for Fictional Journal 02'",
        "Right: PushButton 'Edit Fictional Journal 02'",
        "Right: PushButton 'More actions for Fictional Journal 02'",
        "Left: PushButton 'Edit Fictional Journal 02'",
        "Down: PushButton 'More actions for Fictional Journal 02'",
        "Up: PushButton 'Edit Fictional Journal 02'",
        "Up: PushButton 'More actions for Fictional Journal 02'"
    }

    ' Enter and Space ask about the journal, or open its menu with Make
    ' Target Journal off on the target, Move Up off on the first row and
    ' Move Down off on the last; the keyboard stays on the button pressed.
    Private Shared ReadOnly PressExpected As New List(Of String) From {
        "Enter on Edit Fictional Journal 01",
        "  asked about Fictional Journal 01",
        "  focus PushButton 'Edit Fictional Journal 01'",
        "Enter on More actions for Fictional Journal 01",
        "  menu Make Target Journal (off), Move Up (off), Move Down, -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 01'",
        "Space on Edit Fictional Journal 01",
        "  asked about Fictional Journal 01",
        "  focus PushButton 'Edit Fictional Journal 01'",
        "Space on More actions for Fictional Journal 01",
        "  menu Make Target Journal (off), Move Up (off), Move Down, -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 01'",
        "Enter on Edit Fictional Journal 02",
        "  asked about Fictional Journal 02",
        "  focus PushButton 'Edit Fictional Journal 02'",
        "Enter on More actions for Fictional Journal 02",
        "  menu Make Target Journal, Move Up, Move Down, -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 02'",
        "Space on Edit Fictional Journal 02",
        "  asked about Fictional Journal 02",
        "  focus PushButton 'Edit Fictional Journal 02'",
        "Space on More actions for Fictional Journal 02",
        "  menu Make Target Journal, Move Up, Move Down, -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 02'",
        "Enter on Edit Fictional Journal 03",
        "  asked about Fictional Journal 03",
        "  focus PushButton 'Edit Fictional Journal 03'",
        "Enter on More actions for Fictional Journal 03",
        "  menu Make Target Journal, Move Up, Move Down (off), -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 03'",
        "Space on Edit Fictional Journal 03",
        "  asked about Fictional Journal 03",
        "  focus PushButton 'Edit Fictional Journal 03'",
        "Space on More actions for Fictional Journal 03",
        "  menu Make Target Journal, Move Up, Move Down (off), -, Remove from Shortlist",
        "  focus PushButton 'More actions for Fictional Journal 03'"
    }

    ' An action from the keyboard rebuilds the rows, and the keyboard lands
    ' on the editor's first stop, the Overview tab, as it did when the
    ' button pressed was disposed under it.
    Private Shared ReadOnly AfterActionExpected As New List(Of String) From {
        "Edit Fictional Journal 02: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 02, Fictional Journal 03",
        "Move Up on Fictional Journal 02: focus RadioButton 'Overview'; rows Fictional Journal 02, Fictional Journal 01, Fictional Journal 03",
        "Move Down on Fictional Journal 02: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 03, Fictional Journal 02",
        "Make Target Journal on Fictional Journal 02: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 02, Fictional Journal 03",
        "Remove from Shortlist on Fictional Journal 02: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 03",
        "Move Up on Fictional Journal 03: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 03, Fictional Journal 02",
        "Move Down on Fictional Journal 01: focus RadioButton 'Overview'; rows Fictional Journal 02, Fictional Journal 01, Fictional Journal 03",
        "Remove from Shortlist on Fictional Journal 03: focus RadioButton 'Overview'; rows Fictional Journal 01, Fictional Journal 02"
    }

    ' Each row as a list item, then its status, its name with its details,
    ' its facts and its evidence when it has them, and its two buttons;
    ' pressing a button from a screen reader does what a click does.
    Private Shared ReadOnly ReaderExpected As New List(Of String) From {
        "the shortlist: Client 'Shortlisted journals' focusable",
        "ListItem 'Fictional Journal 01' focusable",
        "StaticText '1.  Preferred'",
        "StaticText 'Fictional Journal 01" & Environment.NewLine & "Target journal  ·  Fits the scope.  ·  1 of 7 trust checks  ·  1 of 6 fit checks'",
        "StaticText 'Open access · 1,200 USD'",
        "PushButton 'Edit Fictional Journal 01' action 'Press' focusable",
        "PushButton 'More actions for Fictional Journal 01' action 'Press' focusable",
        "ListItem 'Fictional Journal 02' focusable",
        "StaticText '2.  Ruled out'",
        "StaticText 'Fictional Journal 02" & Environment.NewLine & "Submitted Jun 23, 2026  ·  Under consideration  ·  Too slow for this one.'",
        "PushButton 'Edit Fictional Journal 02' action 'Press' focusable",
        "PushButton 'More actions for Fictional Journal 02' action 'Press' focusable",
        "ListItem 'Fictional Journal 03' focusable",
        "StaticText '3.  Considering'",
        "StaticText 'Fictional Journal 03" & Environment.NewLine & "Suggested by Find Journals.'",
        "StaticText 'Found by keyword search: 14 matching articles of 1,200 since 2021 (Example)'",
        "PushButton 'Edit Fictional Journal 03' action 'Press' focusable",
        "PushButton 'More actions for Fictional Journal 03' action 'Press' focusable",
        "Press Edit Fictional Journal 01: asked about Fictional Journal 01",
        "Press More actions for Fictional Journal 01: menu Make Target Journal (off), Move Up (off), Move Down, -, Remove from Shortlist",
        "Press Edit Fictional Journal 02: asked about Fictional Journal 02",
        "Press More actions for Fictional Journal 02: menu Make Target Journal, Move Up, Move Down, -, Remove from Shortlist",
        "Press Edit Fictional Journal 03: asked about Fictional Journal 03",
        "Press More actions for Fictional Journal 03: menu Make Target Journal, Move Up, Move Down (off), -, Remove from Shortlist"
    }


    Private Delegate Function EnumWindowsProc(window As IntPtr, parameter As IntPtr) As Boolean

    <DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsProc, parameter As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetKeyboardState(state As Byte()) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetKeyboardState(state As Byte()) As Boolean
    End Function

End Class
