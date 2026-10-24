Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Keyboard access (#136): Route View is operated from the keyboard, and
' Esc cancels every modal dialog; and the help keys the guide names work
' in a dialog (#139). Keys are posted to the focused control, so they
' take the path a real key takes, past PreviewKeyDown, the form's command
' keys and its dialog keys.
<TestClass>
<DoNotParallelize>
Public Class KeyboardAccessTests

    ' Views the app never shows modally: the main window, and the editor,
    ' which is only ever the manuscript page.
    Private Shared ReadOnly NeverModal As Type() = {
        GetType(Form1),
        GetType(EditManuscriptForm)
    }

    ' Shown, not modal, while an update downloads; nothing in it can stop
    ' the download, so it has nothing for Esc to press.
    Private Shared ReadOnly AllowedWithoutCancel As Type() = {
        GetType(UpdateProgressForm)
    }

    ' The dialogs Enter presses no button in (#151), each for a reason of
    ' its own.
    Private Shared ReadOnly AllowedWithoutDefault As Type() = {
        GetType(DeleteManuscriptForm),          ' Enter must never delete; Delete waits for DELETE.
        GetType(ExcelMappingForm),              ' Enter in Header row would import before its columns are seen.
        GetType(JournalFactsForm),              ' Without an ISSN, Enter in the name box runs Find.
        GetType(OrcidLookupForm),               ' Enter would look up again and clear the works checked.
        GetType(PacketExportForm),              ' Enter commits an edit in the file list.
        GetType(PublicationExportForm),         ' Save Export and Copy are two equal ways out.
        GetType(ReviewerResponseExportForm),    ' Its one field takes Enter as a new line.
        GetType(UpdateProgressForm)             ' It has no buttons.
    }

    ' The three dialogs whose footer ends in a main action that was no
    ' default button (#151), with the button Enter now presses.
    Private Shared ReadOnly DefaultButtonGiven As (View As Type, Text As String)() = {
        (GetType(BibliographyExportSelectionForm), "Export Selected"),
        (GetType(BibliographyImportForm), "Import Selected"),
        (GetType(TitlePageImportForm), "Use These Details")
    }

    ' The fields the walk found without an access key in two dialogs that
    ' give their other fields one (#151), each with the key that now
    ' focuses it and the tab it is on (-1: the dialog has none), named as
    ' a screen reader names it.
    Private Shared ReadOnly AccessKeyGiven As (View As Type, Tab As Integer, Key As Char, Field As String)() = {
        (GetType(ReviewerResponseItemForm), 0, "m"c, "Reviewer comment (enter a comment or action)"),
        (GetType(ReviewerResponseItemForm), 0, "p"c, "Planned or completed action"),
        (GetType(ReviewerResponseItemForm), 1, "e"c, "Response draft for the journal"),
        (GetType(ReviewerResponseItemForm), 2, "l"c, "Manuscript location (page, line, or section)"),
        (GetType(ReviewerResponseItemForm), 2, "n"c, "Working notes (included in export)"),
        (GetType(JournalSuggestionsForm), -1, "l"c, "Published in the last")
    }

    ' The five dialogs that had no CancelButton, with the result Esc closes
    ' each with: that of its Cancel or its Close button.
    Private Shared ReadOnly EscapeProbed As (View As Type, Result As DialogResult)() = {
        (GetType(FillBlanksForm), DialogResult.Cancel),
        (GetType(PublicationCheckForm), DialogResult.Cancel),
        (GetType(SubmissionDetailsForm), DialogResult.OK),
        (GetType(AuthorLibraryForm), DialogResult.OK),
        (GetType(JournalLibraryForm), DialogResult.OK)
    }

    ' The dialogs with ? help buttons. Each has a default button, which
    ' Enter presses wherever focus is that is not a button.
    Private Shared ReadOnly WithHelp As Type() = {
        GetType(AddSubmissionForm),
        GetType(JournalChecklistItemEditForm),
        GetType(ManuscriptReadinessForm),
        GetType(ManuscriptRouteViewForm),
        GetType(ManuscriptVersionEditForm)
    }

    Private Const WM_KEYDOWN As UInteger =
        &H100

    Private Const WM_KEYUP As UInteger =
        &H101

    Private Const VK_SHIFT As Integer =
        &H10

    Private Const WM_UPDATEUISTATE As UInteger =
        &H128

    Private Const WM_SYSCHAR As UInteger =
        &H106

    Private Const UIS_CLEAR As Integer =
        2

    Private Const UISF_HIDEFOCUS As Integer =
        1

    Private Shared ReadOnly KeyDownMethod As MethodInfo =
        GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic)


    ' Every waypoint of a published route has one link that Tab reaches,
    ' in route order, and Enter on it opens that waypoint, as a double
    ' click on its card does. Each link Tab reaches is scrolled into view.
    <TestMethod>
    Public Sub RouteViewOpensEachWaypointFromItsOwnLinkInRouteOrder()

        Dim manuscript As Manuscript =
            RouteMapServiceTests.Anchoring()

        Dim waypoints As List(Of ManuscriptRouteWaypoint) =
            ManuscriptRouteProjectionService.Project(manuscript).Waypoints

        Assert.IsTrue(
            waypoints.Count >= 8,
            "The fixture route is long enough to scroll: " & waypoints.Count & " waypoints."
        )

        StaRunner.Run(
            Sub()

                Using view As New ManuscriptRouteViewForm(manuscript)

                    ViewCatalog.ShowOffscreen(view)

                    Dim links As List(Of LinkLabel) =
                        WaypointLinks(view, waypoints.Count)

                    For index As Integer = 0 To links.Count - 1
                        StringAssert.StartsWith(
                            links(index).AccessibleName,
                            "Open in Manuscript: ",
                            "Link " & index & " names what it opens."
                        )
                    Next

                    Assert.AreEqual(
                        links.Count,
                        links.Select(Function(link) link.AccessibleName).Distinct().Count(),
                        "Each link's name tells its waypoint apart: " & String.Join(" | ", links.Select(Function(link) link.AccessibleName))
                    )

                    ' Round the whole tab order once, from no focus.
                    Dim host As Control =
                        links(0).Parent

                    While Not (TypeOf host Is ScrollableControl AndAlso DirectCast(host, ScrollableControl).AutoScroll)
                        host = host.Parent
                    End While

                    Dim reached As New List(Of LinkLabel)()
                    Dim seen As New HashSet(Of Control)()

                    view.ActiveControl = Nothing

                    For count As Integer = 0 To ViewCatalog.Descendants(view).Count()

                        Press(view, Keys.Tab)

                        Dim active As Control =
                            DeepestActiveControl(view)

                        Assert.IsNotNull(
                            active,
                            "Tab selects a control."
                        )

                        If Not seen.Add(active) Then
                            Exit For
                        End If

                        Dim link As LinkLabel =
                            TryCast(active, LinkLabel)

                        If link IsNot Nothing Then

                            reached.Add(link)

                            Dim shown As Rectangle =
                                host.RectangleToClient(link.RectangleToScreen(link.ClientRectangle))

                            Assert.IsTrue(
                                host.ClientRectangle.Contains(shown),
                                link.AccessibleName & " is scrolled into view: " & shown.ToString() & " in " & host.ClientRectangle.ToString()
                            )

                        End If

                    Next

                    CollectionAssert.AreEqual(
                        links,
                        reached,
                        "Tab reaches every waypoint's link once, in route order: " & String.Join(" | ", reached.Select(Function(link) link.AccessibleName))
                    )

                    view.Close()

                End Using

                ' Enter on each link opens its own waypoint, never Close: a
                ' focused link is the form's default button while it has focus.
                For index As Integer = 0 To waypoints.Count - 1

                    Using view As New ManuscriptRouteViewForm(manuscript)

                        ViewCatalog.ShowOffscreen(view)

                        Dim link As LinkLabel =
                            WaypointLinks(view, waypoints.Count)(index)

                        Assert.IsNotNull(
                            view.AcceptButton,
                            "Close is the default button, so Enter has another button to press."
                        )

                        view.ActiveControl = link
                        Press(view, Keys.Enter)

                        Assert.AreEqual(
                            DialogResult.Yes,
                            view.DialogResult,
                            "Enter on " & link.AccessibleName & " opens it."
                        )

                        Assert.IsTrue(
                            SameWaypoint(waypoints(index), view.SelectedWaypoint),
                            link.AccessibleName & " opens waypoint " & index & "."
                        )

                        view.Close()

                    End Using

                Next

                Assert.AreEqual(
                    0,
                    StaRunner.OpenFormsOnThisThread().Count,
                    "Route View leaves no form open."
                )

            End Sub
        )

    End Sub


    ' Tab reaches everything the main window's pages draw rather than host
    ' as controls (#133), as it reached the controls they replaced: over
    ' the catalog's pages and hosted views, Tab from a drawn list's first
    ' stop lands on each of its items, buttons and links in reading order,
    ' each announced as the one focused, and then leaves the list; and Tab
    ' from Import & Export's first command lands on each of its commands in
    ' turn. The Deadlines page draws items, buttons, a "⋯" and the Done
    ' link, so the sweep cannot pass by finding none.
    <TestMethod>
    Public Sub TabReachesEveryDrawnItemButtonAndLink()

        Dim reports As New List(Of String)()
        Dim walked As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.Hosted.Select(Function(view) (view.Name, view.Build)),
                Sub(name, shown)

                    For Each list As DeadlineList In ViewCatalog.Descendants(shown).OfType(Of DeadlineList)().Where(Function(control) control.Visible).ToList()

                        Dim root As AccessibleObject =
                            list.AccessibilityObject

                        Dim children As List(Of AccessibleObject) =
                            Enumerable.Range(0, root.GetChildCount()).Select(Function(index) root.GetChild(index)).ToList()

                        Dim expected As List(Of Integer) =
                            Enumerable.Range(0, children.Count).
                                Where(Function(index) {AccessibleRole.ListItem, AccessibleRole.PushButton, AccessibleRole.Link}.Contains(children(index).Role)).
                                ToList()

                        Dim reached As New List(Of Integer)()

                        If Not list.Focus() Then
                            reports.Add(name & ": the drawn list takes no focus.")
                            Continue For
                        End If

                        For count As Integer = 0 To expected.Count - 1

                            If Not list.Focused Then
                                Exit For
                            End If

                            reached.Add(children.IndexOf(root.GetFocused()))
                            Press(shown, Keys.Tab)

                        Next

                        walked.Add(name & ": " & reached.Count & " stops")

                        If Not reached.SequenceEqual(expected) Then
                            reports.Add(name & ": Tab reached " & String.Join(", ", reached.Select(Function(index) If(index < 0, "(nothing)", children(index).Name))) &
                                        "; expected " & String.Join(", ", expected.Select(Function(index) children(index).Name)) & ".")
                        ElseIf list.Focused Then
                            reports.Add(name & ": Tab past the last stop stays in the list.")
                        End If

                    Next

                    For Each list As CommandList In ViewCatalog.Descendants(shown).OfType(Of CommandList)().Where(Function(control) control.Visible).ToList()

                        Dim commands As List(Of ActionButton) =
                            list.Controls.OfType(Of ActionButton)().ToList()

                        If commands.Count = 0 OrElse Not commands(0).Focus() Then
                            reports.Add(name & ": the first command takes no focus.")
                            Continue For
                        End If

                        Dim reached As New List(Of Control)()

                        For Each command As ActionButton In commands
                            reached.Add(DeepestActiveControl(shown))
                            Press(shown, Keys.Tab)
                        Next

                        walked.Add(name & ": " & reached.Count & " commands")

                        If Not reached.SequenceEqual(commands) Then
                            reports.Add(name & ": Tab reached " & String.Join(", ", reached.Select(Function(control) If(control Is Nothing, "(nothing)", control.Text))) & ".")
                        End If

                    Next

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.IsTrue(
            walked.Any(Function(line) line.StartsWith("Deadlines page: ", StringComparison.Ordinal) AndAlso line.EndsWith(" stops", StringComparison.Ordinal)) AndAlso
            walked.Contains("Import & Export page: 14 commands"),
            "Both pages were walked: " & String.Join(" | ", walked)
        )

        Assert.AreEqual(
            0,
            reports.Count,
            Environment.NewLine & String.Join(Environment.NewLine, reports)
        )

    End Sub


    ' Route View opens on a stop a keyboard user can see (#192). Every
    ' stop Tab reaches is a waypoint's link, a button, or the route map,
    ' which a screen reader reads and which draws a focus ring; the map's
    ' key and the route's markers, which drew none and do nothing, are no
    ' stops. The first Tab from the window's opening focus lands on the
    ' first waypoint's link, and Enter there opens that waypoint, never
    ' Close; Enter on the route map does nothing at all.
    <TestMethod>
    Public Sub RouteViewsFirstTabIsVisibleAndEnterNeverPressesClose()

        Dim manuscript As Manuscript =
            RouteMapServiceTests.Anchoring()

        Dim waypoints As List(Of ManuscriptRouteWaypoint) =
            ManuscriptRouteProjectionService.Project(manuscript).Waypoints

        StaRunner.Run(
            Sub()

                Using view As New ManuscriptRouteViewForm(manuscript)

                    ShowWithOpeningFocus(view)

                    Dim links As List(Of LinkLabel) =
                        WaypointLinks(view, waypoints.Count)

                    Dim opening As Control =
                        DeepestActiveControl(view)

                    Press(view, Keys.Tab)

                    Dim first As Control =
                        DeepestActiveControl(view)

                    Assert.AreSame(
                        links(0),
                        first,
                        "The first Tab from " & Describe(opening) & " lands on the first waypoint's link, not on " & Describe(first) & "."
                    )

                    ' Round the whole tab order once.
                    Dim hidden As New List(Of String)()
                    Dim seen As New HashSet(Of Control)()
                    Dim active As Control = first

                    For count As Integer = 0 To ViewCatalog.Descendants(view).Count()

                        If Not seen.Add(active) Then
                            Exit For
                        End If

                        If Not (TypeOf active Is LinkLabel OrElse TypeOf active Is ButtonBase OrElse TypeOf active Is ContextHelpControl OrElse TypeOf active Is RouteMapBar) Then
                            hidden.Add(Describe(active))
                        End If

                        Press(view, Keys.Tab)
                        active = DeepestActiveControl(view)

                    Next

                    Assert.AreEqual(
                        0,
                        hidden.Count,
                        "Stops that show no focus: " & String.Join(", ", hidden)
                    )

                    Dim map As RouteMapBar =
                        ViewCatalog.Descendants(view).OfType(Of RouteMapBar)().Single()

                    Assert.IsTrue(
                        seen.Contains(map),
                        "The route map is a stop, so a screen reader reads it."
                    )

                    ' Focus cues show, as once a key has been pressed. The
                    ' map is told directly, as the drawn lists' tests tell
                    ' theirs: asked of a window Windows never activated,
                    ' because another program had the foreground, the change
                    ' left the map's cues hidden.
                    view.ActiveControl = map
                    SendMessage(map.Handle, WM_UPDATEUISTATE, New IntPtr(UIS_CLEAR Or (UISF_HIDEFOCUS << 16)), IntPtr.Zero)
                    Application.DoEvents()

                    Assert.IsTrue(
                        map.Focused AndAlso CBool(GetType(Control).GetProperty("ShowFocusCues", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(map)),
                        "The route map has the focus and shows focus cues."
                    )

                    Using focused As New Bitmap(map.Width, map.Height)

                        map.DrawToBitmap(focused, New Rectangle(Point.Empty, map.Size))

                        Dim ring As Integer =
                            Enumerable.Range(0, map.Width * map.Height).
                                Count(Function(index) focused.GetPixel(index Mod map.Width, index \ map.Width).ToArgb() = UiTheme.AccentSecondaryColor().ToArgb())

                        Assert.IsTrue(
                            ring >= map.Width,
                            "Focused, the route map draws a ring: " & ring & " pixels of it."
                        )

                    End Using

                    Press(view, Keys.Enter)

                    Assert.IsTrue(
                        view.Visible AndAlso view.DialogResult = DialogResult.None,
                        "Enter on the route map does nothing; the window answered " & view.DialogResult.ToString() & "."
                    )

                    ' Windows presses the default button for Enter with
                    ' Shift held as well.
                    PressWithShift(view, Keys.Enter)

                    Assert.IsTrue(
                        view.Visible AndAlso view.DialogResult = DialogResult.None,
                        "Shift+Enter on the route map does nothing; the window answered " & view.DialogResult.ToString() & "."
                    )

                    view.Close()

                End Using

                Using view As New ManuscriptRouteViewForm(manuscript)

                    ShowWithOpeningFocus(view)
                    Press(view, Keys.Tab)
                    Press(view, Keys.Enter)

                    Assert.AreEqual(
                        DialogResult.Yes,
                        view.DialogResult,
                        "Enter at the first Tab opens a waypoint, never Close."
                    )

                    Assert.IsTrue(
                        SameWaypoint(waypoints(0), view.SelectedWaypoint),
                        "It opens the first waypoint."
                    )

                    view.Close()

                End Using

                Assert.AreEqual(
                    0,
                    StaRunner.OpenFormsOnThisThread().Count,
                    "Route View leaves no form open."
                )

            End Sub
        )

    End Sub


    ' On the Board, Tab reaches each card's title in card order, each just
    ' before the card's ⋯ button and named by its manuscript (#192), and
    ' Enter or Space on a title opens that manuscript's page, as a click
    ' does; Space with Alt, Ctrl or Shift does not. Tab and Enter already
    ' worked in this window before #192; Space did not.
    <TestMethod>
    <DataRow(Keys.Enter)>
    <DataRow(Keys.Space)>
    Public Sub BoardCardTitlesAreTabStopsInCardOrderAndOpenTheirManuscript(key As Keys)

        Dim library As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        library.Add(New Manuscript With {.Title = "Second Pipeline Study", .CurrentStage = PaperStage.Draft, .Location = ManuscriptLocation.Pipeline})
        library.Add(New Manuscript With {.Title = "Third Pipeline Study", .CurrentStage = PaperStage.Revision, .Location = ManuscriptLocation.Pipeline})

        Dim root As String =
            CreateTemporaryRoot()

        StorageEnvironment.RootOverride =
            root

        Try

            StaRunner.Run(
                Sub()

                    Using board As Form1 = ViewCatalog.BoardOf(library)

                        Dim cards As List(Of Control) =
                            ViewCatalog.Descendants(board).OfType(Of RoundedPanel)().
                                Where(Function(card) card.Visible AndAlso card.ContextMenuStrip IsNot Nothing).
                                Cast(Of Control)().
                                ToList()

                        Dim titles As List(Of LinkLabel) =
                            cards.Select(Function(card) card.Controls.OfType(Of LinkLabel)().Single(Function(link) link.Text = card.AccessibleName)).
                                ToList()

                        Assert.AreEqual(3, titles.Count, "The Pipeline shows three cards.")

                        For Each title As LinkLabel In titles
                            Assert.AreEqual(title.Text, title.AccessibilityObject.Name, "A title is named by its manuscript.")
                        Next

                        ' Round the Board from its search box.
                        board.ActiveControl =
                            ViewCatalog.Descendants(board).OfType(Of TextBox)().First(Function(box) box.Visible)

                        Dim reached As New List(Of LinkLabel)()
                        Dim after As New List(Of String)()
                        Dim previous As LinkLabel = Nothing

                        For count As Integer = 0 To 60

                            Press(board, Keys.Tab)

                            Dim active As Control =
                                DeepestActiveControl(board)

                            If previous IsNot Nothing Then
                                after.Add(If(active?.AccessibleName, "(nothing)"))
                                previous = Nothing
                            End If

                            Dim title As LinkLabel =
                                TryCast(active, LinkLabel)

                            If title IsNot Nothing AndAlso titles.Contains(title) Then
                                If reached.Contains(title) Then Exit For
                                reached.Add(title)
                                previous = title
                            End If

                        Next

                        CollectionAssert.AreEqual(
                            titles,
                            reached,
                            "Tab reaches each card's title once, in card order: " & String.Join(" | ", reached.Select(Function(link) link.Text))
                        )

                        CollectionAssert.AreEqual(
                            titles.Select(Function(link) "More actions for " & link.Text).ToList(),
                            after,
                            "Each title comes just before its card's ⋯."
                        )

                        ' The middle card, from its title.
                        board.ActiveControl = titles(1)
                        Application.DoEvents()

                        ' Alt+Space is the window menu's key, which reaches
                        ' Windows only if the key press is left alone. Given
                        ' to the title directly, as a real Alt+Space would
                        ' open the window menu's own loop here.
                        For Each modifier As Keys In {Keys.Alt, Keys.Control, Keys.Shift}

                            Dim held As New KeyEventArgs(modifier Or Keys.Space)
                            KeyDownMethod.Invoke(titles(1), New Object() {held})
                            Application.DoEvents()

                            Assert.AreEqual(
                                Form1.WorkspacePage.Board,
                                board.ActivePage,
                                held.KeyData.ToString() & " on a card's title leaves the Board as it is."
                            )

                            Assert.IsFalse(
                                held.SuppressKeyPress,
                                held.KeyData.ToString() & " on a card's title is left for Windows."
                            )

                        Next

                        Press(board, key)

                        Assert.AreEqual(
                            Form1.WorkspacePage.Manuscript,
                            board.ActivePage,
                            key.ToString() & " on a card's title opens the manuscript's page."
                        )

                        Assert.AreEqual(
                            library.Single(Function(item) item.Title = titles(1).Text).Id,
                            DirectCast(GetType(Form1).GetField("currentManuscriptId", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), Guid),
                            "It opens that card's manuscript."
                        )

                        board.Close()

                    End Using

                End Sub
            )

        Finally

            StorageEnvironment.RootOverride =
                Nothing

            DeleteTemporaryRoot(
                root
            )

        End Try

    End Sub


    ' Esc can cancel every dialog the app shows modally: each has a
    ' CancelButton. The allow-list has teeth: a form on it that gains a
    ' CancelButton is reported, so the list never outlives its reason.
    <TestMethod>
    Public Sub EveryModalDialogHasACancelButton()

        Dim missing As New List(Of String)()
        Dim stale As New List(Of String)()

        Dim modal As List(Of (Name As String, Build As Func(Of String, Form))) =
            ViewCatalog.Dialogs.
                Where(Function(entry) Not NeverModal.Contains(entry.Key)).
                Select(Function(entry) (entry.Key.Name, entry.Value)).
                ToList()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                modal,
                Sub(name, shown)

                    Dim allowed As Boolean =
                        AllowedWithoutCancel.Any(Function(type) type.Name = name)

                    If shown.CancelButton Is Nothing AndAlso Not allowed Then
                        missing.Add(name)
                    ElseIf shown.CancelButton IsNot Nothing AndAlso allowed Then
                        stale.Add(name)
                    End If

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.AreEqual(
            0,
            missing.Count,
            "Modal dialogs Esc cannot cancel: " & String.Join(", ", missing)
        )

        Assert.AreEqual(
            0,
            stale.Count,
            "Allowed without a CancelButton, yet it has one: " & String.Join(", ", stale)
        )

    End Sub


    ' Enter presses the main action of every dialog the app shows modally
    ' (#151): each has a default button, which UiPolish also fills as the
    ' one primary action, except those allowed none for a reason of their
    ' own. The three that had none press the button named for them. The
    ' allow-list has teeth: a form on it that gains a default button is
    ' reported, as one that loses its own would be.
    <TestMethod>
    Public Sub EveryDialogWithAMainActionHasADefaultButton()

        Dim missing As New List(Of String)()
        Dim stale As New List(Of String)()
        Dim wrong As New List(Of String)()

        Dim modal As List(Of (Name As String, Build As Func(Of String, Form))) =
            ViewCatalog.Dialogs.
                Where(Function(entry) Not NeverModal.Contains(entry.Key)).
                Select(Function(entry) (entry.Key.Name, entry.Value)).
                ToList()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                modal,
                Sub(name, shown)

                    Dim allowed As Boolean =
                        AllowedWithoutDefault.Any(Function(type) type.Name = name)

                    Dim defaultButton As Control =
                        TryCast(shown.AcceptButton, Control)

                    If defaultButton Is Nothing AndAlso Not allowed Then
                        missing.Add(name)
                    ElseIf defaultButton IsNot Nothing AndAlso allowed Then
                        stale.Add(name)
                    End If

                    For Each given In DefaultButtonGiven.Where(Function(entry) entry.View.Name = name)
                        If defaultButton Is Nothing OrElse defaultButton.Text <> given.Text Then
                            wrong.Add(name & " presses " & If(defaultButton?.Text, "nothing") & ", not " & given.Text)
                        End If
                    Next

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.AreEqual(
            0,
            missing.Count,
            "Modal dialogs Enter presses nothing in: " & String.Join(", ", missing)
        )

        Assert.AreEqual(
            0,
            stale.Count,
            "Allowed without a default button, yet it has one: " & String.Join(", ", stale)
        )

        Assert.AreEqual(
            0,
            wrong.Count,
            String.Join("; ", wrong)
        )

    End Sub


    ' Esc closes each of the five when it is shown modally, as the app
    ' shows it: the dialog returns from ShowDialog with its Cancel or Close
    ' button's result.
    <TestMethod>
    Public Sub EscClosesFillBlanksPublicationCheckSubmissionDetailsAndTheLibraries()

        Dim failures As New List(Of String)()

        For Each probed In EscapeProbed

            Dim root As String =
                CreateTemporaryRoot()

            StorageEnvironment.RootOverride =
                root

            Try

                Dim result As DialogResult? =
                    Nothing

                StaRunner.Run(
                    Sub()
                        result = EscapeResult(ViewCatalog.Dialogs(probed.View)(root))
                    End Sub
                )

                If Not result.HasValue Then
                    failures.Add(probed.View.Name & ": Esc did not close it.")
                ElseIf result.Value <> probed.Result Then
                    failures.Add(probed.View.Name & ": Esc closed it with " & result.Value.ToString() & ", not " & probed.Result.ToString() & ".")
                End If

            Catch ex As Exception

                failures.Add(probed.View.Name & ": " & ex.Message)

            Finally

                StorageEnvironment.RootOverride =
                    Nothing

                DeleteTemporaryRoot(
                    root
                )

            End Try

        Next

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

    End Sub


    ' Esc on a running lookup stops it, as Stop does, and the window stays
    ' open on what was found; Esc again closes it. Stop is the Cancel
    ' button renamed while the lookup runs, so its handler, not the button's
    ' DialogResult, decides what Esc does.
    <TestMethod>
    Public Sub EscStopsARunningLookupAndLeavesTheWindowOpen()

        Dim failures As New List(Of String)()

        Dim probes As (Name As String, Build As Func(Of Form), Result As DialogResult)() = {
            ("FillBlanksForm", Function() New FillBlanksForm(WithDois(), New UntilStopped(), Function() True) With {.Pause = TimeSpan.Zero}, DialogResult.Cancel),
            ("PublicationCheckForm", Function() New PublicationCheckForm(WithDois(), Nothing, String.Empty, New UntilStopped(), Function() True, DateTime.Today) With {.Pause = TimeSpan.Zero}, DialogResult.OK)
        }

        For Each probe In probes

            Dim root As String =
                CreateTemporaryRoot()

            StorageEnvironment.RootOverride =
                root

            Try

                Dim outcome As (Closed As Boolean, Result As DialogResult, Taken As Integer) =
                    Nothing

                Dim openAfterStop As Boolean =
                    False

                StaRunner.Run(
                    Sub()

                        Dim dialog As Form =
                            probe.Build()

                        outcome = Drive(
                            dialog,
                            (Function() True, Sub() PrimaryButton(dialog).PerformClick()),
                            (Function() Shows(dialog, "Stop"), Sub() Post(dialog, Keys.Escape)),
                            (Function() Shows(dialog, "Stopped early"),
                             Sub()
                                 openAfterStop = dialog.Visible
                                 Post(dialog, Keys.Escape)
                             End Sub)
                        )

                    End Sub
                )

                If outcome.Taken < 3 Then
                    failures.Add(probe.Name & ": Esc did not stop the lookup.")
                ElseIf Not openAfterStop Then
                    failures.Add(probe.Name & ": stopping the lookup closed the window.")
                ElseIf Not outcome.Closed Then
                    failures.Add(probe.Name & ": Esc did not close it once the lookup stopped.")
                ElseIf outcome.Result <> probe.Result Then
                    failures.Add(probe.Name & ": Esc closed it with " & outcome.Result.ToString() & ", not " & probe.Result.ToString() & ".")
                End If

            Catch ex As Exception

                failures.Add(probe.Name & ": " & ex.Message)

            Finally

                StorageEnvironment.RootOverride =
                    Nothing

                DeleteTemporaryRoot(
                    root
                )

            End Try

        Next

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

    End Sub


    ' The guide's line on ? help buttons: in a dialog, Space or F1 on one
    ' shows its help and a second press hides it, and neither presses the
    ' dialog's default button. A guard: the keys worked before the guide
    ' named them, so it cannot fail before; it holds the guide to what
    ' they do. Enter is not among them: in a dialog it presses the default
    ' button, as the guide says.
    <TestMethod>
    Public Sub SpaceAndF1OnAHelpButtonShowItsHelpInADialog()

        Dim reports As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                WithHelp.Select(Function(type) (type.Name, ViewCatalog.Dialogs(type))),
                Sub(name, shown)

                    Dim helps As List(Of ContextHelpControl) =
                        ViewCatalog.Descendants(shown).OfType(Of ContextHelpControl)().
                            Where(Function(help) help.CanSelect).
                            ToList()

                    Dim defaultButton As Control =
                        TryCast(shown.AcceptButton, Control)

                    If helps.Count = 0 OrElse defaultButton Is Nothing Then
                        reports.Add(name & " shows " & helps.Count & " help buttons and " & If(defaultButton Is Nothing, "no", "a") & " default button.")
                        Return
                    End If

                    Dim pressed As Integer =
                        0

                    AddHandler defaultButton.Click,
                        Sub(sender, e)
                            pressed += 1
                        End Sub

                    For Each help As ContextHelpControl In helps

                        For Each key As Keys In {Keys.Space, Keys.F1}

                            shown.ActiveControl = help
                            Press(shown, key)

                            Dim showed As Boolean =
                                HelpShowing(help)

                            Press(shown, key)

                            If Not showed OrElse HelpShowing(help) Then
                                reports.Add(name & ": " & key.ToString() & " on " & help.AccessibleName & " did not show and then hide its help.")
                            End If

                        Next

                    Next

                    If pressed > 0 OrElse Not shown.Visible Then
                        reports.Add(name & ": a help key pressed " & defaultButton.Text & ".")
                    End If

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.AreEqual(
            0,
            reports.Count,
            Environment.NewLine & String.Join(Environment.NewLine, reports)
        )

    End Sub


    ' In the two dialogs that gave some of their fields access keys and
    ' not the rest (#151), each field the walk found without one now has
    ' one: Alt and its letter, on the field's tab, focus the field its
    ' label names, and the field is still named without the "&".
    <TestMethod>
    Public Sub EachFieldOfTheReviewerCommentAndFindJournalsHasAnAccessKey()

        Dim reports As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                AccessKeyGiven.Select(Function(entry) entry.View).Distinct().Select(Function(type) (type.Name, ViewCatalog.Dialogs(type))),
                Sub(name, shown)

                    For Each given In AccessKeyGiven.Where(Function(entry) entry.View.Name = name)

                        If given.Tab >= 0 Then
                            ViewCatalog.Descendants(shown).OfType(Of TabControl)().Single().SelectedIndex = given.Tab
                            Application.DoEvents()
                        End If

                        ' Start from the window's first stop, away from the field.
                        shown.ActiveControl = Nothing
                        shown.SelectNextControl(Nothing, True, True, True, False)
                        Application.DoEvents()

                        PressAccessKey(shown, given.Key)

                        Dim active As Control =
                            DeepestActiveControl(shown)

                        If active Is Nothing OrElse active.AccessibilityObject.Name <> given.Field Then
                            reports.Add(name & ": Alt+" & Char.ToUpperInvariant(given.Key) & " focused " & Describe(active) & If(active Is Nothing, String.Empty, ", named '" & active.AccessibilityObject.Name & "'") & ", not " & given.Field & ".")
                        End If

                    Next

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.AreEqual(
            0,
            reports.Count,
            Environment.NewLine & String.Join(Environment.NewLine, reports)
        )

    End Sub


    ' No window gives one letter to two access keys it shows together, so
    ' Alt and the letter always reach the one control it names (#151).
    ' Every view and step in the catalog is read as it opens and again on
    ' each page of each of its tabs, with the keys of labels, buttons,
    ' check boxes and options that show them. A guard: no window repeats
    ' one today, so it cannot fail before the change; it holds the keys on
    ' each tab of a window to that. Sections a page hides some other way,
    ' such as the manuscript page's, are not switched.
    <TestMethod>
    Public Sub NoWindowRepeatsAnAccessKeyItShowsTogether()

        Dim repeated As New HashSet(Of String)()
        Dim read As New HashSet(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.AllViewsAndStages(),
                Sub(name, shown)

                    Dim readings As New List(Of List(Of (Key As Char, Text As String))) From {
                        ShownAccessKeys(shown)
                    }

                    ' A tab's keys show only while it is selected, so each
                    ' page is read on its own.
                    For Each tabs As TabControl In ViewCatalog.Descendants(shown).OfType(Of TabControl)().ToList()

                        For index As Integer = 0 To tabs.TabCount - 1

                            tabs.SelectedIndex = index
                            Application.DoEvents()

                            readings.Add(ShownAccessKeys(shown))

                        Next

                    Next

                    For Each keys In readings

                        For Each entry In keys
                            read.Add(name & " " & entry.Key)
                        Next

                        For Each letter In keys.GroupBy(Function(entry) entry.Key).Where(Function(group) group.Count() > 1)
                            repeated.Add(name & ": Alt+" & letter.Key & " is " & String.Join(" and ", letter.Select(Function(entry) "'" & entry.Text & "'")))
                        Next

                    Next

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        ' The keys the walk gave, three of them on tabs not shown first,
        ' were among those read.
        Dim unread As List(Of String) =
            AccessKeyGiven.
                Select(Function(given) given.View.Name & " " & Char.ToUpperInvariant(given.Key)).
                Where(Function(key) Not read.Contains(key)).
                ToList()

        Assert.AreEqual(
            0,
            unread.Count,
            "The sweep did not read: " & String.Join(", ", unread)
        )

        Assert.AreEqual(
            0,
            repeated.Count,
            Environment.NewLine & String.Join(Environment.NewLine, repeated)
        )

    End Sub


    ' Leaving a page by keyboard while focus is inside it (#133): where
    ' focus lands, and that the control left loses it once, is the same
    ' however the page is put away. Pinned on the window as it was when
    ' every page was removed and then disposed, before pages were disposed
    ' or hidden in place.
    <TestMethod>
    Public Sub LeavingAPageByKeyboardKeepsFocusWhereItLandsToday()

        Dim root As String = CreateTemporaryRoot()
        Dim report As New List(Of String)()

        ' The Journals page reads the author library from the default root.
        StorageEnvironment.RootOverride = Path.Combine(root, "storage")

        Try

            StaRunner.Run(
                Sub()

                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.WriteExample(Path.Combine(root, "library"))

                    Using board As New LibraryBoard(library, Path.Combine(root, "settings"))

                        board.Start()

                        Dim targets As (Name As String, Open As Action, Find As Func(Of Control))() = {
                            ("the Library filter", Sub() board.NavigateTo(Form1.WorkspacePage.Library),
                             Function() ViewCatalog.Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter the Library")),
                            ("an Insights tab", Sub() board.NavigateTo(Form1.WorkspacePage.Insights),
                             Function() ViewCatalog.Descendants(board).OfType(Of RadioButton)().Single(Function(tab) tab.Text.StartsWith("Your Routes", StringComparison.Ordinal))),
                            ("the Journals list", Sub() board.NavigateTo(Form1.WorkspacePage.Journals),
                             Function() ViewCatalog.Descendants(board).OfType(Of JournalLibraryForm)().Single().Controls.Cast(Of Control)().
                                 SelectMany(Function(child) {child}.Concat(ViewCatalog.Descendants(child))).OfType(Of ListBox)().First()),
                            ("the Deadlines filter", Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines),
                             Function() ViewCatalog.Descendants(board.PageView).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter deadlines")),
                            ("an Import & Export command", Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport),
                             Function() ViewCatalog.Descendants(board.PageView).OfType(Of ActionButton)().Single(Function(button) button.Text = "Import Spreadsheet...")),
                            ("the manuscript page's Back link", Sub() board.Open(board.Library(0)),
                             Function() ViewCatalog.Descendants(board.PageView).OfType(Of LinkLabel)().Single(Function(link) If(link.AccessibleName, String.Empty).StartsWith("Back to", StringComparison.Ordinal))),
                            ("the manuscript page's Title box", Sub() board.Open(board.Library(0)),
                             Function() board.Field("Title"))
                        }

                        Dim leaves As (Name As String, Leave As Action)() = {
                            ("Alt+Left", Sub() board.PressCommandKey(Keys.Alt Or Keys.Left)),
                            ("Ctrl+1", Sub() board.PressCommandKey(Keys.Control Or Keys.D1)),
                            ("Back", Sub() board.GoBackInHistory())
                        }

                        For Each target In targets
                            For Each leave In leaves

                                target.Open()
                                Application.DoEvents()

                                Dim focused As Control = target.Find()
                                board.ActiveControl = focused
                                Application.DoEvents()

                                Dim left As Integer = 0
                                Dim validating As Integer = 0
                                AddHandler focused.Leave, Sub(sender, e) left += 1
                                AddHandler focused.Validating, Sub(sender, e) validating += 1

                                Dim failure As String = String.Empty
                                Try
                                    leave.Leave()
                                    Application.DoEvents()
                                Catch ex As Exception
                                    failure = ex.GetType().Name & ": " & ex.Message
                                End Try

                                ' No control on these pages handles Validating,
                                ' so whether it runs is not seen: a removed
                                ' control was never validated, a control
                                ' focus moves away from is, once.
                                If validating > 1 Then failure &= " Validating ran " & validating.ToString() & " times."

                                report.Add(target.Name & ", " & leave.Name & ": page " & board.PageName &
                                           ", active " & Describe(DeepestActiveControl(board)) &
                                           ", Leave " & left.ToString() &
                                           If(failure.Length = 0, String.Empty, ", " & failure.Trim()))

                            Next
                        Next

                        board.Close()

                    End Using

                End Sub,
                TimeSpan.FromMinutes(5))

        Finally
            StorageEnvironment.RootOverride = Nothing
            DeleteTemporaryRoot(root)
        End Try

        ' Focus moves to the first stop of the window, the rail's collapse
        ' button, and the control left loses it once, from a page disposed
        ' as from one kept (the manuscript page, Deadlines and Import &
        ' Export, hidden in place and shown again on the next visit).
        Dim expected As List(Of String) =
            {"the Library filter", "an Insights tab", "the Journals list", "the Deadlines filter", "an Import & Export command",
             "the manuscript page's Back link", "the manuscript page's Title box"}.
                SelectMany(Function(target) {"Alt+Left", "Ctrl+1", "Back"}.
                    Select(Function(leave) target & ", " & leave & ": page Board, active RailCommandButton 'Collapse navigation', Leave 1")).
                ToList()

        CollectionAssert.AreEqual(
            expected,
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Entering a page by keyboard (#133): a card's title opened with Enter
    ' or Space on two shelves, each rail page by its command key and by the
    ' arrow keys along the rail, Alt+Left back, and the shelf tabs moved
    ' along with the arrow keys. Where focus lands, how often the control
    ' it left lost it and was validated, and how often the control it
    ' landed on was entered, are the same however a page is built, shown
    ' or painted. Pinned on the window before pages were prepared while it
    ' idled or switched without painting, and changed by #213 only where
    ' a page opened from the Board left the keyboard on the hidden Board.
    ' The way back to the Board names each control the keyboard passes
    ' through in turn: leaving the page puts it on the window's first stop,
    ' the rail's collapse button, before the Board's control takes it.
    <TestMethod>
    Public Sub EnteringAPageByKeyboardKeepsFocusWhereItLandsToday()

        Dim root As String = CreateTemporaryRoot()
        Dim report As New List(Of String)()

        StorageEnvironment.RootOverride = Path.Combine(root, "storage")

        Try

            StaRunner.Run(
                Sub()

                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()

                        ' Counts what happens to the control focus leaves and
                        ' the one it lands on, while one action runs; for the
                        ' way back to the Board, every control the keyboard
                        ' passes through on the way, in turn.
                        Dim record As Action(Of String, Control, Action) =
                            Sub(name, origin, act)
                                Dim entered As New Dictionary(Of Control, Integer)()
                                Dim path As New List(Of String)()
                                Dim watched As List(Of Control) = {DirectCast(board, Control)}.Concat(ViewCatalog.Descendants(board)).ToList()
                                Dim onEnter As EventHandler =
                                    Sub(sender, e)
                                        Dim control As Control = DirectCast(sender, Control)
                                        Dim count As Integer = 0
                                        entered.TryGetValue(control, count)
                                        entered(control) = count + 1
                                        If Not control.HasChildren Then path.Add(DescribeOnBoard(control))
                                    End Sub
                                For Each control As Control In watched
                                    AddHandler control.Enter, onEnter
                                Next
                                Dim left As Integer = 0
                                Dim validating As Integer = 0
                                AddHandler origin.Leave, Sub(sender, e) left += 1
                                AddHandler origin.Validating, Sub(sender, e) validating += 1

                                Dim failure As String = String.Empty
                                Try
                                    act()
                                    Application.DoEvents()
                                    board.WaitForIdleWork()
                                Catch ex As Exception
                                    failure = ", " & ex.GetType().Name & ": " & ex.Message
                                End Try

                                For Each control As Control In watched
                                    RemoveHandler control.Enter, onEnter
                                Next

                                Dim landed As Control = DeepestActiveControl(board)
                                Dim landedEntered As Integer = 0
                                Dim hooked As Boolean = landed IsNot Nothing AndAlso watched.Contains(landed)
                                If hooked Then entered.TryGetValue(landed, landedEntered)

                                report.Add(name & ": page " & board.PageName &
                                           ", active " & DescribeOnBoard(landed) &
                                           ", entered " & If(hooked, landedEntered.ToString(), "new") &
                                           ", left " & left.ToString() &
                                           ", validated " & Math.Min(validating, 2).ToString() &
                                           ", origin entered " & If(entered.ContainsKey(origin), entered(origin), 0).ToString() &
                                           If(name.EndsWith("then Alt+Left", StringComparison.Ordinal), ", through " & String.Join(" then ", path), String.Empty) &
                                           failure)
                            End Sub

                        ' Opens a manuscript once first, so every open below is
                        ' an open of the kept page, as most opens are.
                        board.Open(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        board.WaitForIdleWork()

                        For Each shelf In {("Pipeline", "tabPipeline", "pipelinePanel"), ("Published", "tabPublished", "publishedPanel")}
                            For Each key As Keys In {Keys.Enter, Keys.Space}

                                board.ShelfTab(shelf.Item2).Checked = True
                                Application.DoEvents()
                                board.WaitForBoardFill()
                                Dim card As Control = board.Shelf(shelf.Item3).Controls.OfType(Of RoundedPanel)().First()
                                Dim title As LinkLabel = card.Controls.OfType(Of LinkLabel)().First()
                                board.ActiveControl = title
                                Application.DoEvents()

                                record(shelf.Item1 & " title, " & key.ToString(), title,
                                       Sub()
                                           Press(board, key)
                                           If key = Keys.Space Then PostMessage(title.Handle, WM_KEYUP, New IntPtr(CInt(key)), New IntPtr(1))
                                       End Sub)

                                Dim landed As Control = DeepestActiveControl(board)
                                record(shelf.Item1 & " title, " & key.ToString() & ", then Alt+Left", If(landed, DirectCast(board, Control)),
                                       Sub() board.PressCommandKey(Keys.Alt Or Keys.Left))

                            Next
                        Next

                        board.ShelfTab("tabPipeline").Checked = True
                        Application.DoEvents()

                        ' Each rail page by its command key, from the Board's
                        ' search box, and back.
                        Dim search As TextBox = ViewCatalog.Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Search the board (Ctrl+F)")
                        For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Library, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Deadlines,
                                                                 Form1.WorkspacePage.Insights, Form1.WorkspacePage.ImportExport}
                            board.ActiveControl = search
                            Application.DoEvents()
                            record("Ctrl+" & (CInt(page) + 1).ToString() & " from the search box", search,
                                   Sub() board.PressCommandKey(Keys.Control Or CType(Keys.D0 + CInt(page) + 1, Keys)))
                            If board.PageName = Form1.WorkspacePage.Insights.ToString() Then board.WaitForInsights()
                            Dim landed As Control = DeepestActiveControl(board)
                            record("Ctrl+" & (CInt(page) + 1).ToString() & ", then Alt+Left", If(landed, DirectCast(board, Control)),
                                   Sub() board.PressCommandKey(Keys.Alt Or Keys.Left))
                        Next

                        ' Down along the rail visits each page with focus on the
                        ' rail, and Up comes back.
                        Dim rail As List(Of RailButton) = ViewCatalog.Descendants(board).OfType(Of RailButton)().ToList()
                        board.ActiveControl = rail(0)
                        Application.DoEvents()
                        For Each direction As Keys In {Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Up, Keys.Up, Keys.Up, Keys.Up, Keys.Up}
                            Dim origin As Control = If(DeepestActiveControl(board), DirectCast(board, Control))
                            record(direction.ToString() & " on the rail from " & DescribeOnBoard(origin), origin, Sub() Press(board, direction))
                            If board.PageName = Form1.WorkspacePage.Insights.ToString() Then board.WaitForInsights()
                        Next

                        ' The shelf tabs along with the arrow keys.
                        board.ActiveControl = board.ShelfTab("tabPipeline")
                        Application.DoEvents()
                        For Each direction As Keys In {Keys.Right, Keys.Right, Keys.Left, Keys.Left}
                            Dim origin As Control = If(DeepestActiveControl(board), DirectCast(board, Control))
                            record(direction.ToString() & " on the shelf tabs from " & DescribeOnBoard(origin), origin, Sub() Press(board, direction))
                            report(report.Count - 1) &= ", checked " & {"tabPipeline", "tabPublished", "tabFileDrawer"}.Single(Function(name) board.ShelfTab(name).Checked)
                        Next

                        board.Close()

                    End Using

                End Sub,
                TimeSpan.FromMinutes(5))

        Finally
            StorageEnvironment.RootOverride = Nothing
            DeleteTemporaryRoot(root)
        End Try

        CollectionAssert.AreEqual(
            EnteringAPageExpected,
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Where focus lands entering each page (#213). A title opened from the
    ' Board, or the Board's search box left by Ctrl+N, puts the keyboard on
    ' the opened page's first stop: the manuscript page's Overview tab, a
    ' rail page's own first control, entered once, the control left losing
    ' focus once; and Alt+Left back puts it on the control that opened the
    ' page, by way of the rail's collapse button, which takes it for a
    ' moment as the page is left (two focus changes, which the Narrator
    ' pass listens for). (On f5c6947, before pages were prepared while the
    ' window idled or switched without painting, and until #213, these
    ' rows left the keyboard where it was, on the hidden Board, entered,
    ' left and validated 0 times.) A rail button or a shelf tab reached
    ' with an arrow key is entered once, the one left loses focus once and
    ' is validated once, as on f5c6947.
    Private Shared ReadOnly EnteringAPageExpected As New List(Of String) From {
        "Pipeline title, Enter: page Manuscript, active ShelfTabButton 'Overview', entered 1, left 1, validated 1, origin entered 0",
        "Pipeline title, Enter, then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf, entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then LinkLabel 0 of card 0 on its shelf",
        "Pipeline title, Space: page Manuscript, active ShelfTabButton 'Overview', entered 1, left 1, validated 1, origin entered 0",
        "Pipeline title, Space, then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf, entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then LinkLabel 0 of card 0 on its shelf",
        "Published title, Enter: page Manuscript, active ShelfTabButton 'Overview', entered 1, left 1, validated 1, origin entered 0",
        "Published title, Enter, then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf, entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then LinkLabel 0 of card 0 on its shelf",
        "Published title, Space: page Manuscript, active ShelfTabButton 'Overview', entered 1, left 1, validated 1, origin entered 0",
        "Published title, Space, then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf, entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then LinkLabel 0 of card 0 on its shelf",
        "Ctrl+2 from the search box: page Library, active TextBox 'Filter the Library', entered new, left 1, validated 1, origin entered 0",
        "Ctrl+2, then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)', entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then TextBox 'Search the board (Ctrl+F)'",
        "Ctrl+3 from the search box: page Journals, active ListBox 'Journals', entered 1, left 1, validated 1, origin entered 0",
        "Ctrl+3, then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)', entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then TextBox 'Search the board (Ctrl+F)'",
        "Ctrl+4 from the search box: page Deadlines, active TextBox 'Filter deadlines', entered new, left 1, validated 1, origin entered 0",
        "Ctrl+4, then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)', entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then TextBox 'Search the board (Ctrl+F)'",
        "Ctrl+5 from the search box: page Insights, active ActionButton 'Pipeline Report...', entered 1, left 1, validated 1, origin entered 0",
        "Ctrl+5, then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)', entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then TextBox 'Search the board (Ctrl+F)'",
        "Ctrl+6 from the search box: page ImportExport, active ActionButton 'Import Spreadsheet...', entered new, left 1, validated 1, origin entered 0",
        "Ctrl+6, then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)', entered 1, left 1, validated 1, origin entered 0, through RailCommandButton 'Collapse navigation' then TextBox 'Search the board (Ctrl+F)'",
        "Down on the rail from RailButton 'Board': page Library, active RailButton 'Library', entered 1, left 1, validated 1, origin entered 0",
        "Down on the rail from RailButton 'Library': page Journals, active RailButton 'Journals', entered 1, left 1, validated 1, origin entered 0",
        "Down on the rail from RailButton 'Journals': page Deadlines, active RailButton 'Deadlines', entered 1, left 1, validated 1, origin entered 0",
        "Down on the rail from RailButton 'Deadlines': page Insights, active RailButton 'Insights', entered 1, left 1, validated 1, origin entered 0",
        "Down on the rail from RailButton 'Insights': page ImportExport, active RailButton 'Import & Export', entered 1, left 1, validated 1, origin entered 0",
        "Up on the rail from RailButton 'Import & Export': page Insights, active RailButton 'Insights', entered 1, left 1, validated 1, origin entered 0",
        "Up on the rail from RailButton 'Insights': page Deadlines, active RailButton 'Deadlines', entered 1, left 1, validated 1, origin entered 0",
        "Up on the rail from RailButton 'Deadlines': page Journals, active RailButton 'Journals', entered 1, left 1, validated 1, origin entered 0",
        "Up on the rail from RailButton 'Journals': page Library, active RailButton 'Library', entered 1, left 1, validated 1, origin entered 0",
        "Up on the rail from RailButton 'Library': page Board, active RailButton 'Board', entered 1, left 1, validated 1, origin entered 0",
        "Right on the shelf tabs from ShelfTabButton 'Pipeline': page Board, active ShelfTabButton 'Published', entered 1, left 1, validated 1, origin entered 0, checked tabPublished",
        "Right on the shelf tabs from ShelfTabButton 'Published': page Board, active ShelfTabButton 'File Drawer', entered 1, left 1, validated 1, origin entered 0, checked tabFileDrawer",
        "Left on the shelf tabs from ShelfTabButton 'File Drawer': page Board, active ShelfTabButton 'Published', entered 1, left 1, validated 1, origin entered 0, checked tabPublished",
        "Left on the shelf tabs from ShelfTabButton 'Published': page Board, active ShelfTabButton 'Pipeline', entered 1, left 1, validated 1, origin entered 0, checked tabPipeline"
    }


    ' A card's title opened with Enter while the window really has the
    ' keyboard (#133, #213). The pins above run in a window that never had
    ' it, so the Board never holds focus as it hides there. Here it does:
    ' the keyboard goes to the manuscript page's first stop, its Overview
    ' tab, and Alt+Left puts it back on the title. Until #213 (this test
    ' was OpeningACardHoldingTheKeyboardLandsFocusAsBefore), the move off
    ' the Board as it hid went into the Board, to its first stop, the
    ' search box, and the keyboard stayed there behind the page, as it did
    ' with the Board hidden as f5c6947 hid it: "Enter: page Manuscript,
    ' active TextBox 'Search the board (Ctrl+F)', focus TextBox 'Search the
    ' board (Ctrl+F)', the Board holds focus True", and the same on the
    ' Board after Alt+Left.
    <TestMethod>
    Public Sub OpeningACardHoldingTheKeyboardLandsFocusOnThePage()

        Dim root As String = CreateTemporaryRoot()
        Dim report As New List(Of String)()

        StorageEnvironment.RootOverride = Path.Combine(root, "storage")

        Try

            StaRunner.Run(
                Sub()

                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()
                        board.Open(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        board.WaitForIdleWork()

                        Dim landing As Func(Of String) =
                            Function()
                                Return "page " & board.PageName &
                                       ", active " & DescribeOnBoard(DeepestActiveControl(board)) &
                                       ", focus " & DescribeOnBoard(Control.FromChildHandle(GetFocus())) &
                                       ", the Board holds focus " & board.BoardPage.ContainsFocus.ToString()
                            End Function

                        Dim card As Control = board.Shelf("pipelinePanel").Controls.OfType(Of RoundedPanel)().First()
                        Dim title As LinkLabel = card.Controls.OfType(Of LinkLabel)().First()
                        If Not title.Focus() OrElse Not title.Focused Then
                            Assert.Inconclusive("The test window was given no keyboard focus on this host.")
                        End If
                        report.Add("Before: " & landing())

                        Press(board, Keys.Enter)
                        board.WaitForIdleWork()
                        report.Add("Enter: " & landing())

                        board.PressCommandKey(Keys.Alt Or Keys.Left)
                        Application.DoEvents()
                        board.WaitForIdleWork()
                        report.Add("Alt+Left: " & landing())

                        board.Close()

                    End Using

                End Sub,
                TimeSpan.FromMinutes(3))

        Finally
            StorageEnvironment.RootOverride = Nothing
            DeleteTemporaryRoot(root)
        End Try

        CollectionAssert.AreEqual(
            New List(Of String) From {
                "Before: page Board, active LinkLabel 0 of card 0 on its shelf, focus LinkLabel 0 of card 0 on its shelf, the Board holds focus True",
                "Enter: page Manuscript, active ShelfTabButton 'Overview', focus ShelfTabButton 'Overview', the Board holds focus False",
                "Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf, focus LinkLabel 0 of card 0 on its shelf, the Board holds focus True"
            },
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Each page opened from the Board by keyboard (#213), from a card's
    ' title and from the Board's search box: the rail pages by their
    ' command keys, the manuscript page by Enter on the title. The keyboard
    ' lands on the opened page's first stop, a control shown to the user
    ' and to Windows, inside the page and not in the Board hidden behind
    ' it; Alt+Left puts it back on the control the page was opened from. A
    ' manuscript the library no longer holds shows a page with no stop, and
    ' the keyboard goes to the window's first stop, outside the hidden
    ' Board. Where the window has the keyboard, Windows' focus is held to
    ' the same; without it, as on a locked desktop, the window's own record
    ' of the focused control is. The same with motion off and on (#142): a
    ' switch's cross-fade moves the keyboard nowhere.
    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub OpeningEachPageFromTheBoardByKeyboardLandsInsideIt(motion As Boolean)

        Dim root As String = CreateTemporaryRoot()
        Dim report As New List(Of String)()
        Dim windowsFocusChecked As Integer = 0

        StorageEnvironment.RootOverride = Path.Combine(root, "storage")
        UiMotion.EnabledOverride = motion
        PageFade.AwaitPicture = motion
        ' Motion on is on whatever the host's theme: a contrast theme stops it.
        If motion Then UiTheme.HighContrastOverride = False

        Try

            StaRunner.Run(
                Sub()

                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()

                        ' Every open below is an open of the kept page, as most are.
                        board.Open(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        board.WaitForIdleWork()

                        Dim search As TextBox = ViewCatalog.Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Search the board (Ctrl+F)")
                        Dim title As Func(Of Control) =
                            Function() board.Shelf("pipelinePanel").Controls.OfType(Of RoundedPanel)().First().Controls.OfType(Of LinkLabel)().First()

                        ' Where a control is, as the user and Windows see it.
                        Dim placeOf As Func(Of Control, String) =
                            Function(control)
                                If control Is Nothing Then Return "nowhere"
                                Dim shown As Boolean = control.Visible AndAlso control.IsHandleCreated AndAlso IsWindowVisible(control.Handle)
                                Dim place As String =
                                    If(board.PageView IsNot Nothing AndAlso board.PageView.Contains(control), "in the page",
                                       If(board.BoardPage.Contains(control), If(board.BoardPage.Visible, "in the Board", "in the hidden Board"), "outside the page area"))
                                Return If(shown, "shown", "not shown") & ", " & place
                            End Function

                        Dim opens As (Name As String, FromTitle As Boolean, FromSearch As Boolean, Open As Action)() = {
                            ("Ctrl+2", True, True, Sub() board.PressCommandKey(Keys.Control Or Keys.D2)),
                            ("Ctrl+3", True, True, Sub() board.PressCommandKey(Keys.Control Or Keys.D3)),
                            ("Ctrl+4", True, True, Sub() board.PressCommandKey(Keys.Control Or Keys.D4)),
                            ("Ctrl+5", True, True, Sub() board.PressCommandKey(Keys.Control Or Keys.D5)),
                            ("Ctrl+6", True, True, Sub() board.PressCommandKey(Keys.Control Or Keys.D6)),
                            ("Enter", True, False, Sub() Press(board, Keys.Enter)),
                            ("a manuscript no longer in the library", False, True, Sub() board.Open(New Manuscript With {.Title = "Not in the library"}))
                        }

                        Dim origins As (Name As String, Find As Func(Of Control), IsTitle As Boolean)() = {
                            ("the first card's title", title, True),
                            ("the search box", Function() search, False)
                        }

                        For Each origin In origins

                            For Each open In opens.Where(Function(item) If(origin.IsTitle, item.FromTitle, item.FromSearch))

                                ' The keyboard, where Windows gives the window it.
                                Dim from As Control = origin.Find()
                                If Not from.Focus() Then board.ActiveControl = from
                                Application.DoEvents()
                                Dim focusHeld As Boolean = from.Focused

                                open.Open()
                                Application.DoEvents()
                                board.WaitForIdleWork()
                                If board.PageName = Form1.WorkspacePage.Insights.ToString() Then board.WaitForInsights()

                                Dim landed As Control = DeepestActiveControl(board)
                                report.Add(origin.Name & ", " & open.Name & ": page " & board.PageName & ", active " & DescribeOnBoard(landed) & ", " & placeOf(landed))

                                ' Windows' focus, where the window has the keyboard.
                                If focusHeld Then
                                    windowsFocusChecked += 1
                                    Dim focused As Control = Control.FromChildHandle(GetFocus())
                                    Assert.AreSame(landed, focused, origin.Name & ", " & open.Name & ": Windows' focus is on " & DescribeOnBoard(focused))
                                End If

                                board.PressCommandKey(Keys.Alt Or Keys.Left)
                                Application.DoEvents()
                                board.WaitForIdleWork()

                                Dim back As Control = DeepestActiveControl(board)
                                report.Add("  then Alt+Left: page " & board.PageName & ", active " & DescribeOnBoard(back) & ", " & placeOf(back) &
                                           If(back Is origin.Find(), ", where the page was opened from", String.Empty))

                            Next
                        Next

                        board.Close()

                    End Using

                End Sub,
                TimeSpan.FromMinutes(5))

        Finally
            UiMotion.EnabledOverride = Nothing
            PageFade.AwaitPicture = False
            UiTheme.HighContrastOverride = Nothing
            StorageEnvironment.RootOverride = Nothing
            DeleteTemporaryRoot(root)
        End Try

        Console.WriteLine("Windows' focus was checked on " & windowsFocusChecked.ToString() & " of 12 opens (0 where the window is given no keyboard).")

        Dim backWhereOpened As String = ", shown, in the Board, where the page was opened from"

        CollectionAssert.AreEqual(
            New List(Of String) From {
                "the first card's title, Ctrl+2: page Library, active TextBox 'Filter the Library', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the first card's title, Ctrl+3: page Journals, active ListBox 'Journals', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the first card's title, Ctrl+4: page Deadlines, active TextBox 'Filter deadlines', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the first card's title, Ctrl+5: page Insights, active ActionButton 'Pipeline Report...', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the first card's title, Ctrl+6: page ImportExport, active ActionButton 'Import Spreadsheet...', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the first card's title, Enter: page Manuscript, active ShelfTabButton 'Overview', shown, in the page",
                "  then Alt+Left: page Board, active LinkLabel 0 of card 0 on its shelf" & backWhereOpened,
                "the search box, Ctrl+2: page Library, active TextBox 'Filter the Library', shown, in the page",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened,
                "the search box, Ctrl+3: page Journals, active ListBox 'Journals', shown, in the page",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened,
                "the search box, Ctrl+4: page Deadlines, active TextBox 'Filter deadlines', shown, in the page",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened,
                "the search box, Ctrl+5: page Insights, active ActionButton 'Pipeline Report...', shown, in the page",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened,
                "the search box, Ctrl+6: page ImportExport, active ActionButton 'Import Spreadsheet...', shown, in the page",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened,
                "the search box, a manuscript no longer in the library: page Manuscript, active RailCommandButton 'Collapse navigation', shown, outside the page area",
                "  then Alt+Left: page Board, active TextBox 'Search the board (Ctrl+F)'" & backWhereOpened
            },
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Back on the Board after the manuscript opened from a card's title by
    ' keyboard was saved or deleted (#213). A save that changes the card's
    ' text rebuilds the card at once, so the title the page was opened
    ' from is gone: Alt+Left puts the keyboard on the new card's title,
    ' shown, in the Board. A manuscript deleted from its page takes its
    ' card with it: back on the Board the keyboard is where leaving the
    ' page put it, the window's first stop, outside the Board and on no
    ' control that is gone.
    <TestMethod>
    Public Sub ReturningAfterASaveOrADeleteLandsOnAShownControl()

        Dim root As String = CreateTemporaryRoot()
        Dim report As New List(Of String)()

        StorageEnvironment.RootOverride = Path.Combine(root, "storage")

        Try

            StaRunner.Run(
                Sub()

                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()

                        ' Every open below is an open of the kept page, as most are.
                        board.Open(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        board.WaitForIdleWork()

                        Dim placeOf As Func(Of Control, String) =
                            Function(control)
                                If control Is Nothing Then Return "nowhere"
                                If control.IsDisposed Then Return "disposed"
                                Dim shown As Boolean = control.Visible AndAlso control.IsHandleCreated AndAlso IsWindowVisible(control.Handle)
                                Dim place As String =
                                    If(board.PageView IsNot Nothing AndAlso board.PageView.Contains(control), "in the page",
                                       If(board.BoardPage.Contains(control), If(board.BoardPage.Visible, "in the Board", "in the hidden Board"), "outside the page area"))
                                Return If(shown, "shown", "not shown") & ", " & place
                            End Function

                        Dim openFrom As Func(Of LinkLabel, String) =
                            Function(link)
                                If Not link.Focus() Then board.ActiveControl = link
                                Application.DoEvents()
                                Press(board, Keys.Enter)
                                board.WaitForIdleWork()
                                Return "page " & board.PageName & ", active " & DescribeOnBoard(DeepestActiveControl(board))
                            End Function

                        ' Opened, renamed and saved, then Alt+Left.
                        Dim first As LinkLabel = board.Shelf("pipelinePanel").Controls.OfType(Of RoundedPanel)().First().Controls.OfType(Of LinkLabel)().First()
                        report.Add("Enter on a title: " & openFrom(first))
                        Dim subject As Manuscript = board.Library.Single(Function(item) item.Id = board.OpenManuscriptId)
                        Dim renamed As String = subject.Title & " (renamed)"
                        board.Field("Title").Text = renamed
                        Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                        Application.DoEvents()
                        report.Add("Saved: the title opened from is " & If(first.IsDisposed, "disposed", "kept"))

                        board.PressCommandKey(Keys.Alt Or Keys.Left)
                        Application.DoEvents()
                        board.WaitForIdleWork()
                        Dim back As Control = DeepestActiveControl(board)
                        report.Add("Alt+Left: page " & board.PageName & ", active " & If(back?.GetType().Name, "nothing") &
                                   If(TypeOf back Is LinkLabel AndAlso back.Text = renamed, " titled as saved", String.Empty) & ", " & placeOf(back))

                        ' Opened again from that title, then deleted from the page.
                        Dim title As LinkLabel = TryCast(back, LinkLabel)
                        Assert.IsNotNull(title, "The keyboard is on a title to open the manuscript from again.")
                        report.Add("Enter on it: " & openFrom(title))
                        board.SetPrompt(Function(asked)
                                            Assert.Fail("Nothing is unsaved, so nothing is asked.")
                                            Return DialogResult.Cancel
                                        End Function)
                        board.ConfirmDelete()
                        Application.DoEvents()
                        board.WaitForIdleWork()
                        Dim landed As Control = DeepestActiveControl(board)
                        report.Add("Deleted: page " & board.PageName & ", the manuscript " & If(board.Library.Contains(subject), "kept", "gone") &
                                   ", the title opened from " & If(title.IsDisposed, "disposed", "kept") &
                                   ", active " & DescribeOnBoard(landed) & ", " & placeOf(landed))

                        board.SetPrompt(Function(asked) DialogResult.No)
                        board.Close()

                    End Using

                End Sub,
                TimeSpan.FromMinutes(5))

        Finally
            StorageEnvironment.RootOverride = Nothing
            DeleteTemporaryRoot(root)
        End Try

        CollectionAssert.AreEqual(
            New List(Of String) From {
                "Enter on a title: page Manuscript, active ShelfTabButton 'Overview'",
                "Saved: the title opened from is disposed",
                "Alt+Left: page Board, active LinkLabel titled as saved, shown, in the Board",
                "Enter on it: page Manuscript, active ShelfTabButton 'Overview'",
                "Deleted: page Board, the manuscript gone, the title opened from disposed, active RailCommandButton 'Collapse navigation', shown, outside the page area"
            },
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' A control as the pins name it: a card's parts by their place on the
    ' card and the card's on its shelf, since a card's text follows the
    ' day the test runs; a shelf tab without its count, which follows the
    ' library.
    Private Shared Function DescribeOnBoard(
        control As Control
    ) As String

        Dim card As CardPanel = TryCast(control?.Parent, CardPanel)

        If card IsNot Nothing AndAlso card.Parent IsNot Nothing Then
            Return control.GetType().Name & " " & card.Controls.GetChildIndex(control).ToString() &
                " of card " & card.Parent.Controls.GetChildIndex(card).ToString() & " on its shelf"
        End If

        Return Regex.Replace(Describe(control), " \(\d+\)'$", "'")

    End Function


    ' Shows the dialog modally, offscreen, and presses Esc in it once it
    ' has shown. Nothing is returned for a dialog Esc leaves open.
    Private Shared Function EscapeResult(
        dialog As Form
    ) As DialogResult?

        Dim outcome As (Closed As Boolean, Result As DialogResult, Taken As Integer) =
            Drive(
                dialog,
                (Function() True, Sub() Post(dialog, Keys.Escape))
            )

        Return If(outcome.Closed, outcome.Result, CType(Nothing, DialogResult?))

    End Function


    ' Shows the dialog modally, offscreen, and takes each step in turn once
    ' its condition holds. A dialog that waits more than five seconds for a
    ' step, or to close after its last, is closed, and is reported as not
    ' closed by its steps; closing is asked for again until a running lookup
    ' lets it go. Returns the result and how many steps were taken.
    Private Shared Function Drive(
        dialog As Form,
        ParamArray steps As (Ready As Func(Of Boolean), Take As Action)()
    ) As (Closed As Boolean, Result As DialogResult, Taken As Integer)

        Using dialog

            Dim taken As Integer =
                0

            Dim closedBySteps As Boolean =
                True

            Dim waited As New Stopwatch()

            Using poll As New Timer With {.Interval = 50}

                AddHandler dialog.Shown,
                    Sub(sender, e)
                        waited.Start()
                        poll.Start()
                    End Sub

                AddHandler poll.Tick,
                    Sub(sender, e)

                        If closedBySteps AndAlso taken < steps.Length AndAlso steps(taken).Ready() Then

                            taken += 1
                            waited.Restart()
                            steps(taken - 1).Take()

                        ElseIf Not closedBySteps OrElse waited.Elapsed > TimeSpan.FromSeconds(5) Then

                            closedBySteps = False
                            dialog.DialogResult = DialogResult.Abort

                        End If

                    End Sub

                dialog.StartPosition = FormStartPosition.Manual
                dialog.Location = New Point(-20000, -20000)
                dialog.ShowInTaskbar = False

                Dim result As DialogResult =
                    dialog.ShowDialog()

                poll.Stop()

                Return (closedBySteps, result, taken)

            End Using

        End Using

    End Function


    ' The view's main button: the primary one it shows.
    Private Shared Function PrimaryButton(
        view As Form
    ) As ActionButton

        Return ViewCatalog.Descendants(view).OfType(Of ActionButton)().
            Single(Function(button) button.Visible AndAlso button.Role = ActionButtonRole.Primary)

    End Function


    ' Whether a visible button or label in the view shows the text.
    Private Shared Function Shows(
        view As Form,
        text As String
    ) As Boolean

        Return ViewCatalog.Descendants(view).
            Any(Function(control) control.Visible AndAlso (TypeOf control Is ButtonBase OrElse TypeOf control Is Label) AndAlso control.Text.Contains(text, StringComparison.Ordinal))

    End Function


    ' The representative library with a DOI on its study under review and
    ' on its published study, so each lookup has a manuscript to ask about:
    ' Check for Publications skips a title as short as "Active Study".
    Private Shared Function WithDois() As List(Of Manuscript)

        Dim library As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        library(0).Metadata.Doi =
            "10.5555/example.active"

        library(1).Metadata.Doi =
            "10.5555/example.published"

        Return library

    End Function


    ' The links of the waypoint cards, one card after another: exactly one
    ' tab-stop link in each card, and a card for every waypoint.
    Private Shared Function WaypointLinks(
        view As Form,
        waypointCount As Integer
    ) As List(Of LinkLabel)

        Dim cards As List(Of RoundedPanel) =
            ViewCatalog.Descendants(view).OfType(Of RoundedPanel)().ToList()

        Assert.AreEqual(
            waypointCount,
            cards.Count,
            "A card for every waypoint."
        )

        Dim links As New List(Of LinkLabel)()

        For index As Integer = 0 To cards.Count - 1

            Dim stops As List(Of LinkLabel) =
                ViewCatalog.Descendants(cards(index)).OfType(Of LinkLabel)().
                    Where(Function(link) link.TabStop AndAlso link.CanSelect).
                    ToList()

            Assert.AreEqual(
                1,
                stops.Count,
                "Waypoint card " & index & " has one link to open it with."
            )

            links.Add(stops(0))

        Next

        Return links

    End Function


    ' The projection is rebuilt for each view, so a waypoint is matched by
    ' what it records rather than by reference.
    Private Shared Function SameWaypoint(
        expected As ManuscriptRouteWaypoint,
        actual As ManuscriptRouteWaypoint
    ) As Boolean

        Return actual IsNot Nothing AndAlso
            actual.Kind = expected.Kind AndAlso
            actual.EventDate = expected.EventDate AndAlso
            Nullable.Equals(actual.Stage, expected.Stage) AndAlso
            Nullable.Equals(actual.SubmissionId, expected.SubmissionId) AndAlso
            Nullable.Equals(actual.DecisionId, expected.DecisionId) AndAlso
            Nullable.Equals(actual.VersionId, expected.VersionId)

    End Function


    ' Posts the key to the focused control and lets the form handle it.
    Private Shared Sub Press(
        form As Form,
        key As Keys
    )

        Post(form, key)
        Application.DoEvents()

    End Sub


    ' Alt and the letter, as Windows delivers an access key: a WM_SYSCHAR
    ' to the focused control, which the window hands to its mnemonics.
    Private Shared Sub PressAccessKey(
        form As Form,
        key As Char
    )

        Dim target As Control =
            If(DeepestActiveControl(form), form)

        ' Bit 29 of the key data: Alt is held.
        PostMessage(target.Handle, WM_SYSCHAR, New IntPtr(AscW(key)), New IntPtr(1 << 29))
        Application.DoEvents()

    End Sub


    ' The letter Alt presses a label, button, check box or option with:
    ' the one after a single "&" in its text, while it shows mnemonics.
    Private Shared Function AccessKeyOf(
        control As Control
    ) As Char?

        Dim label As Label =
            TryCast(control, Label)

        Dim button As ButtonBase =
            TryCast(control, ButtonBase)

        If Not ((label IsNot Nothing AndAlso label.UseMnemonic) OrElse (button IsNot Nothing AndAlso button.UseMnemonic)) Then
            Return Nothing
        End If

        ' "&&" is a literal ampersand; a key is a letter or digit, as
        ' UiPolish reads one.
        Dim match As Match =
            Regex.Match(If(control.Text, String.Empty).Replace("&&", String.Empty), "&([\p{L}\p{N}])")

        Return If(match.Success, Char.ToUpperInvariant(match.Groups(1).Value(0)), CType(Nothing, Char?))

    End Function


    ' The access keys of the controls the window shows now.
    Private Shared Function ShownAccessKeys(
        form As Form
    ) As List(Of (Key As Char, Text As String))

        Return ViewCatalog.Descendants(form).
            Where(Function(control) control.Visible AndAlso AccessKeyOf(control).HasValue).
            Select(Function(control) (AccessKeyOf(control).Value, control.Text)).
            ToList()

    End Function


    ' Shows the window with the focus it opens with in the app, where it is
    ' activated as it opens. Shown while another program has the
    ' foreground, it is not, and nothing has the focus; activation would
    ' select its first stop, as this does.
    Private Shared Sub ShowWithOpeningFocus(
        form As Form
    )

        ViewCatalog.ShowOffscreen(form)

        If form.ActiveControl Is Nothing Then
            form.SelectNextControl(Nothing, True, True, True, False)
            Application.DoEvents()
        End If

    End Sub


    ' Presses the key with Shift held in this thread's key state only, as
    ' WinForms reads modifiers there, and lets Shift go again.
    Private Shared Sub PressWithShift(
        form As Form,
        key As Keys
    )

        Dim state(255) As Byte
        GetKeyboardState(state)

        Dim before As Byte =
            state(VK_SHIFT)

        state(VK_SHIFT) = CByte(before Or &H80)
        SetKeyboardState(state)

        Try
            Press(form, key)
        Finally
            GetKeyboardState(state)
            state(VK_SHIFT) = before
            SetKeyboardState(state)
        End Try

    End Sub


    Private Shared Sub Post(
        form As Form,
        key As Keys
    )

        Dim target As Control =
            If(DeepestActiveControl(form), form)

        PostMessage(target.Handle, WM_KEYDOWN, New IntPtr(CInt(key)), New IntPtr(1))

    End Sub


    ' Whether the help control shows the help a press asked for.
    Private Shared Function HelpShowing(
        help As ContextHelpControl
    ) As Boolean

        Return CBool(
            GetType(ContextHelpControl).
                GetField("_clickHelpVisible", BindingFlags.Instance Or BindingFlags.NonPublic).
                GetValue(help)
        )

    End Function


    Private Shared Function Describe(
        control As Control
    ) As String

        If control Is Nothing Then
            Return "no control"
        End If

        Return control.GetType().Name & " '" & If(control.AccessibleName, control.Text) & "'"

    End Function


    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetKeyboardState(state As Byte()) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetFocus() As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(handle As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetKeyboardState(state As Byte()) As Boolean
    End Function


    ' Answers nothing until the lookup is stopped, as a slow service would.
    Private NotInheritable Class UntilStopped
        Implements IPublicationSource

        Public Async Function LookupDoiAsync(doi As String, cancellationToken As Threading.CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Await Task.Delay(Threading.Timeout.InfiniteTimeSpan, cancellationToken)
            Return Nothing
        End Function

        Public Async Function SearchTitleAsync(title As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Await Task.Delay(Threading.Timeout.InfiniteTimeSpan, cancellationToken)
            Return New List(Of CrossrefMetadataSuggestion)()
        End Function

        Public Async Function OrcidWorksAsync(orcid As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Await Task.Delay(Threading.Timeout.InfiniteTimeSpan, cancellationToken)
            Return New List(Of OrcidWorkSuggestion)()
        End Function
    End Class

End Class
