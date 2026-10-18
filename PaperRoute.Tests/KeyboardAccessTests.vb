Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
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

    Private Const VK_SHIFT As Integer =
        &H10

    Private Const WM_CHANGEUISTATE As UInteger =
        &H127

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

                    ' Focus cues show, as once a key has been pressed in a
                    ' window Windows activated; in one shown while another
                    ' program has the foreground they can stay hidden.
                    view.ActiveControl = map
                    SendMessage(view.Handle, WM_CHANGEUISTATE, New IntPtr(UIS_CLEAR Or (UISF_HIDEFOCUS << 16)), IntPtr.Zero)
                    Application.DoEvents()

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


    Private Shared Function DeepestActiveControl(
        form As Form
    ) As Control

        Dim active As Control =
            form.ActiveControl

        While TypeOf active Is ContainerControl AndAlso DirectCast(active, ContainerControl).ActiveControl IsNot Nothing
            active = DirectCast(active, ContainerControl).ActiveControl
        End While

        Return active

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
