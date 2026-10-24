Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The manuscript page is built once and kept (#133): prepared while the
' window idles after startup (or built by a manuscript opened before then),
' it loads each one into the same editor and lets go of it once left. ManuscriptEditorReuseTests hold what it shows to a
' window that opened only that manuscript; these hold how it is kept.
<TestClass>
<DoNotParallelize>
Public Class KeptManuscriptPageTests

    Private _root As String = String.Empty


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


    ' Prepared while the window idles after startup (#133): the page is in
    ' the page area, hidden, holding nothing of any manuscript, with the
    ' windows its Overview shows made in place; the first manuscript opened
    ' shows on it, and so does every one after. None of its windows is
    ' made on WinForms' parking window, while it is prepared, while
    ' manuscripts open or after they are left.
    <TestMethod>
    Public Sub ThePageIsPreparedWhileTheWindowIdles()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using watch As New WindowCreationWatch(),
                      board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim page As Control = board.KeptPage
                    Assert.IsNotNull(page, "The page is prepared while the window idles.")
                    Assert.AreSame(board.ContentHost, page.Parent)
                    Assert.IsFalse(page.Visible, "It waits hidden.")
                    Dim all As List(Of Control) = ViewCatalog.Descendants(page).ToList()
                    Assert.IsTrue(all.OfType(Of TextBox)().All(Function(box) box.TextLength = 0), "It holds no manuscript.")
                    Assert.IsTrue(all.OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Title").IsHandleCreated, "The Overview has its windows.")
                    Assert.IsFalse(all.OfType(Of SubmissionDetailsForm)().Any(), "No submission's details.")

                    For Each manuscript As Manuscript In board.Library.Take(3)
                        board.Open(manuscript)
                        Application.DoEvents()
                        Assert.AreSame(page, board.KeptPage, "Every manuscript opens on the prepared page.")
                        Assert.AreSame(page, board.PageView)
                        Assert.AreEqual(manuscript.Title, board.Field("Title").Text)
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        Assert.IsFalse(page.Visible)
                    Next

                    report.AddRange(watch.Parked())
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

        Assert.AreEqual(0, report.Count, "Windows made on the parking window:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' The page left is hidden at once and, once the window has idled, holds
    ' nothing of the manuscript: no text, tag, author, shortlisted journal,
    ' submission, details or versions, so nothing that looks across the
    ' window finds it there.
    <TestMethod>
    Public Sub ThePageLeftHoldsNothingOfTheManuscript()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim subject As Manuscript = board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso item.Submissions.Count > 0 AndAlso item.JournalShortlist.Count > 0 AndAlso item.Tags.Count > 0)
                    board.Open(subject)
                    Application.DoEvents()
                    For Each tab As RadioButton In ViewCatalog.Descendants(board.Editor).OfType(Of RadioButton)().ToList()
                        tab.Checked = True
                        Application.DoEvents()
                    Next

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    Assert.IsFalse(board.KeptPage.Visible, "The page left is hidden at once.")
                    board.WaitForIdleWork()

                    Dim page As Control = board.KeptPage
                    Dim all As List(Of Control) = ViewCatalog.Descendants(page).ToList()
                    Assert.IsFalse(all.Any(Function(control) control.Text.Contains(subject.Title, StringComparison.Ordinal)), "No control shows the title.")
                    Assert.IsTrue(all.OfType(Of TextBox)().All(Function(box) box.TextLength = 0), "Every box is empty.")
                    Assert.IsFalse(all.OfType(Of TagChipButton)().Any(), "No tag.")
                    Assert.IsTrue(all.OfType(Of ListBox)().All(Function(list) list.Items.Count = 0), "No author or submission.")
                    Assert.IsFalse(all.OfType(Of SubmissionDetailsForm)().Any(), "No submission's details.")
                    Assert.IsFalse(all.OfType(Of ManuscriptVersionHistoryControl)().Any(), "No version history.")
                    Assert.IsFalse(all.Any(Function(control) control.AccessibleRole = AccessibleRole.ListItem), "No shortlisted journal.")
                    Assert.IsTrue(all.OfType(Of JournalShortlistList)().All(Function(list) list.AccessibilityObject.GetChildCount() = 0 AndAlso list.Entries.Count = 0), "No shortlisted journal drawn.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' An open on the kept page makes no window but its tag chips' (#133):
    ' the shortlist draws its rows, so a manuscript choosing among its
    ' shortlisted journals opens on the windows the page already has. With
    ' motion on (#142): a switch's cross-fade makes no child window.
    <TestMethod>
    Public Sub AnOpenMakesNoWindowButItsTagChips()

        Dim report As New List(Of String)()
        Dim chips As Integer = 0
        Dim tags As Integer = 0

        UiMotion.EnabledOverride = True
        UiTheme.HighContrastOverride = False
        Try
        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim subjects As List(Of Manuscript) =
                        board.Library.Where(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso item.JournalShortlist.Count > 1 AndAlso item.Tags.Count > 0).Take(2).ToList()
                    Assert.AreEqual(2, subjects.Count, "Two Pipeline manuscripts with shortlisted journals and tags.")

                    ' The page is built for the first; the second opens on it.
                    board.Open(subjects(0))
                    Application.DoEvents()
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    board.WaitForIdleWork()

                    Using watch As New WindowCreationWatch()
                        board.Open(subjects(1))
                        Application.DoEvents()
                        Dim made As List(Of String) = watch.Children()
                        chips = made.Where(Function(window) window.Contains(" in TagEditor 'Tags'", StringComparison.Ordinal)).Count()
                        report.AddRange(made.Where(Function(window) Not window.Contains(" in TagEditor 'Tags'", StringComparison.Ordinal)))
                    End Using

                    tags = subjects(1).Tags.Count
                    Dim shortlist As JournalShortlistList = ViewCatalog.Descendants(board.Editor).OfType(Of JournalShortlistList)().Single()
                    Assert.AreEqual(subjects(1).JournalShortlist.Count, shortlist.Entries.Count, "The shortlist shows the manuscript's journals.")

                    ' Leaving fades, and makes no child window either.
                    Using watch As New WindowCreationWatch()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        report.AddRange(watch.Children().Select(Function(window) "leaving: " & window))
                    End Using

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))
        Finally
            UiMotion.EnabledOverride = Nothing
            UiTheme.HighContrastOverride = Nothing
        End Try

        Assert.AreEqual(0, report.Count, "Windows the open made besides its tag chips:" & Environment.NewLine & String.Join(Environment.NewLine, report))
        Assert.AreEqual(tags, chips, "A window for each tag chip.")

    End Sub


    ' Discard, Save and Delete on the kept page, then the next manuscript
    ' opens clean.
    <TestMethod>
    Public Sub DiscardSaveAndDeleteOnTheKeptPage()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim first As Manuscript = board.Library(0)
                    Dim second As Manuscript = board.Library(1)
                    Dim title As String = first.Title

                    board.Open(first)
                    Application.DoEvents()
                    board.Field("Title").Text = title & " (discarded)"
                    board.Discard()
                    Assert.AreEqual(title, board.Field("Title").Text, "Discard shows the saved version again.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())

                    board.Field("Title").Text = title & " (saved)"
                    Assert.IsTrue(board.SaveManuscriptPage())
                    Assert.AreEqual(title & " (saved)", first.Title)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())

                    board.SetPrompt(Function(asked)
                                        Assert.Fail("Nothing is unsaved, so nothing is asked.")
                                        Return DialogResult.Cancel
                                    End Function)
                    board.ConfirmDelete()
                    Application.DoEvents()
                    Assert.AreEqual("Board", board.PageName)
                    Assert.IsFalse(board.Library.Contains(first), "The manuscript is deleted.")

                    board.Open(second)
                    Application.DoEvents()
                    Assert.AreEqual(second.Title, board.Field("Title").Text)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "The next manuscript opens clean.")

                    board.SetPrompt(Function(asked) DialogResult.No)
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A Windows contrast theme switched on while PaperRoute runs: the kept
    ' page, built in the colors before, is built again in the contrast
    ' theme's, as a page built for each manuscript always was.
    <TestMethod>
    Public Sub AContrastThemeSwitchedOnRebuildsTheKeptPage()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.Open(board.Library(0))
                    Application.DoEvents()
                    Dim before As Control = board.KeptPage
                    board.NavigateTo(Form1.WorkspacePage.Board)

                    UiTheme.HighContrastOverride = True
                    Try
                        board.Open(board.Library(1))
                        Application.DoEvents()
                        Assert.AreNotSame(before, board.KeptPage, "The page is built again.")
                        Assert.IsTrue(before.IsDisposed, "The page built in the old colors is gone.")
                        Assert.AreEqual(UiTheme.BoardBackground(), board.KeptPage.BackColor, "The new page has the contrast theme's surface.")
                        Assert.AreEqual(SystemColors.Window, board.KeptPage.BackColor)
                        board.NavigateTo(Form1.WorkspacePage.Board)
                    Finally
                        UiTheme.HighContrastOverride = Nothing
                    End Try

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' The system colors changing, as Windows changes them when a contrast
    ' theme is turned on or off, lets the kept page go as it lets the kept
    ' Deadlines and Import & Export pages go: hidden, at once and where it
    ' is; showing, as it is left, its manuscript on it until then. A page in
    ' the new colors is prepared once the window idles, or built by the
    ' next manuscript opened before then, and that page is kept again.
    ' Nothing is saved.
    <TestMethod>
    Public Sub TheSystemColorsChangingLetsTheKeptPageGo()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.Open(board.Library(0))
                    Application.DoEvents()
                    Dim hidden As Control = board.KeptPage
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    UiTheme.HighContrastOverride = True
                    Try
                        board.ChangeSystemColors()
                        Assert.IsTrue(hidden.IsDisposed, "The hidden page is let go at once.")
                        Assert.IsFalse(board.ContentHost.Controls.Contains(hidden), "It has left the page area.")

                        board.WaitForIdleWork()
                        Dim prepared As Control = board.KeptPage
                        Assert.IsNotNull(prepared, "A page is prepared once the window idles.")
                        Assert.AreEqual(SystemColors.Window, prepared.BackColor, "It is prepared in the contrast theme's colors.")

                        board.Open(board.Library(1))
                        Application.DoEvents()
                        Dim showing As Control = board.KeptPage
                        Assert.AreSame(prepared, showing, "The next manuscript opens on it.")
                        Assert.AreEqual(SystemColors.Window, showing.BackColor, "The next page is built in the contrast theme's colors.")

                        UiTheme.HighContrastOverride = False
                        board.ChangeSystemColors()
                        Assert.IsFalse(showing.IsDisposed, "The page showing stays while it shows.")
                        Assert.IsTrue(showing.Visible)
                        Assert.AreEqual(board.Library(1).Title, board.Field("Title").Text, "Its manuscript is still on it.")

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        Assert.IsTrue(showing.IsDisposed, "The page that was showing is let go as it is left.")

                        board.Open(board.Library(2))
                        Application.DoEvents()
                        Dim rebuilt As Control = board.KeptPage
                        Assert.AreNotSame(showing, rebuilt, "The next manuscript builds the page anew.")
                        Assert.AreEqual(UiTheme.BoardBackground(), rebuilt.BackColor, "In the colors of now.")
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        Assert.IsFalse(rebuilt.IsDisposed, "Leaving keeps the new page.")
                        board.Open(board.Library(0))
                        Application.DoEvents()
                        Assert.AreSame(rebuilt, board.KeptPage, "The new page is kept for the next manuscript.")
                        board.NavigateTo(Form1.WorkspacePage.Board)
                    Finally
                        UiTheme.HighContrastOverride = Nothing
                    End Try

                    Assert.AreEqual(0, board.SaveCount, "Nothing was saved.")
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Tab never reaches the hidden page.
    <TestMethod>
    Public Sub FocusNeverStaysInTheHiddenPage()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.Open(board.Library(0))
                    Application.DoEvents()
                    board.ActiveControl = board.Field("Title")
                    board.PressCommandKey(Keys.Control Or Keys.D1)
                    Application.DoEvents()

                    Dim page As Control = board.KeptPage
                    Dim current As Control = board.ActiveControl
                    Assert.IsFalse(page.Contains(current), "Leaving moves focus out of the page.")

                    For steps As Integer = 1 To 200
                        board.SelectNextControl(current, True, True, True, True)
                        current = DeepestActiveControl(board)
                        Assert.IsFalse(page.Contains(current), "Tab reached " & current.GetType().Name & " in the hidden page.")
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Opened while a save, backup or restore runs (#132), the page shows at
    ' once and builds its versions and details only once the work has
    ' ended, as idle work does.
    <TestMethod>
    Public Sub DeferredPartsWaitForLibraryWork()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim subject As Manuscript = board.Library.First(Function(item) item.Submissions.Count > 0)

                    Dim opened As Boolean = False
                    Dim detailsDuringWork As Boolean = True
                    Dim release As New ManualResetEventSlim(False)

                    ' Runs inside the work's message loop, as a click would.
                    board.BeginInvoke(New Action(
                        Sub()
                            board.Open(subject)
                            opened = True
                        End Sub))

                    Dim sample As New System.Windows.Forms.Timer With {.Interval = 100}
                    AddHandler sample.Tick,
                        Sub(sender, e)
                            If opened Then
                                detailsDuringWork = ViewCatalog.Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Any()
                                sample.Stop()
                                release.Set()
                            End If
                        End Sub
                    sample.Start()

                    Assert.IsTrue(board.RunWork("Saving your library...", Sub() release.Wait(TimeSpan.FromSeconds(30))))
                    sample.Dispose()

                    Assert.IsTrue(opened)
                    Assert.IsFalse(detailsDuringWork, "Nothing is built while the work runs.")

                    board.WaitForIdleWork()
                    Assert.IsTrue(ViewCatalog.Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Any(), "The details follow once it has ended.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    Private Function Library60() As LargeLibraryFixture.SyntheticLibrary
        Return LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)
    End Function

End Class
