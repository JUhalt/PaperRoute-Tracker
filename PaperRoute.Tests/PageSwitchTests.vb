Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Switching between the Board and the Library, Journals and Insights pages
' (#133): what each page shows is the same however quickly it is built and
' put away.
<TestClass>
<DoNotParallelize>
Public Class PageSwitchTests

    Private _root As String = String.Empty


    ' Anything that reads the default data root reads this test's.
    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' The Library's Stage, Shelf, Type and Days columns are as wide as
    ' their widest cell or header, as when each sized itself to all its
    ' cells, after the first fill and after a filter refills the grid.
    <TestMethod>
    Public Sub LibraryColumnsMatchAllCells()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Dim grid As DataGridView = ViewCatalog.Descendants(board).OfType(Of DataGridView)().Single()
                    AssertColumnsFitAllCells(grid, "after the first fill")

                    grid.Sort(grid.Columns("Days"), System.ComponentModel.ListSortDirection.Descending)
                    Application.DoEvents()
                    AssertColumnsFitAllCells(grid, "after sorting by Days")

                    For Each filter As String In {"Published", "File Drawer", "zzz", String.Empty}
                        board.TypeInLibraryFilter(filter)
                        board.PauseTyping()
                        Application.DoEvents()
                        AssertColumnsFitAllCells(grid, "after the filter """ & filter & """")
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Every window the Library, its Authors view, and the kept Journals,
    ' Deadlines, Insights and Import & Export pages (each visited twice, so
    ' a kept page shown again is held too) make as they open is made
    ' inside the main window, where it stays: none is made on WinForms'
    ' parking window and moved in afterwards, which costs every window
    ' twice on a slow machine (#133). With motion on (#142), so a switch's
    ' cross-fade, whose overlay is a window of its own, makes none either.
    <TestMethod>
    Public Sub PageWindowsAreCreatedOnceInPlace()

        Dim report As New List(Of String)()

        UiMotion.EnabledOverride = True
        UiTheme.HighContrastOverride = False
        ' The test host is DPI-unaware: above 100% Windows scales the board,
        ' where a switch is a cut.
        Controls.PageFade.ScaledOverride = False
        Try
        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()

                    Dim views As (Name As String, Open As Action)() = {
                        ("the Library", Sub() board.NavigateTo(Form1.WorkspacePage.Library)),
                        ("the Library's Authors view",
                         Sub()
                             board.NavigateTo(Form1.WorkspacePage.Library)
                             ViewCatalog.Descendants(board).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Authors & Affiliations").Checked = True
                         End Sub),
                        ("Journals", Sub() board.NavigateTo(Form1.WorkspacePage.Journals)),
                        ("Journals, shown again", Sub() board.NavigateTo(Form1.WorkspacePage.Journals)),
                        ("Insights",
                         Sub()
                             board.NavigateTo(Form1.WorkspacePage.Insights)
                             board.WaitForInsights()
                         End Sub),
                        ("Insights, shown again",
                         Sub()
                             board.NavigateTo(Form1.WorkspacePage.Insights)
                             board.WaitForInsights()
                         End Sub),
                        ("Deadlines", Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines)),
                        ("Deadlines, shown again", Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines)),
                        ("Import & Export", Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport)),
                        ("Import & Export, shown again", Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport))
                    }

                    For Each view In views
                        Using watch As New WindowCreationWatch()
                            view.Open()
                            Application.DoEvents()
                            report.AddRange(watch.Parked().Select(Function(window) view.Name & ": " & window))
                        End Using
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))
        Finally
            UiMotion.EnabledOverride = Nothing
            UiTheme.HighContrastOverride = Nothing
            Controls.PageFade.ScaledOverride = Nothing
        End Try

        Assert.AreEqual(0, report.Count, "Windows made on the parking window:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' The manuscript editor, with its submission details and reviewer
    ' responses, makes no window while it is built: each is made once the
    ' page that holds it is in the main window, where it stays, and a
    ' section's or a tab's only when it first shows. Before, building the
    ' editor alone made 99 windows, and opening a manuscript made 98 on
    ' WinForms' parking window (#133).
    <TestMethod>
    Public Sub TheManuscriptEditorMakesItsWindowsOnlyInPlace()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)
                Dim subject As Models.Manuscript =
                    library.Manuscripts.Where(Function(item) item.Submissions.Count > 0).OrderByDescending(Function(item) item.Submissions.Count).First()

                Using watch As New WindowCreationWatch()
                    Using editor As New Forms.EditManuscriptForm(subject, library.Manuscripts, library.AuthorRepository, pageMode:=True)
                        report.AddRange(watch.Children().Select(Function(window) "built alone: " & window))
                    End Using
                End Using

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()

                    For Each manuscript As Models.Manuscript In {subject, library.Manuscripts.First(Function(item) item IsNot subject)}
                        Using watch As New WindowCreationWatch()
                            board.Open(manuscript)
                            Application.DoEvents()
                            report.AddRange(watch.Parked().Select(Function(window) "opened: " & window))
                        End Using
                        Using watch As New WindowCreationWatch()
                            For Each tab As RadioButton In ViewCatalog.Descendants(board.Editor).OfType(Of RadioButton)().ToList()
                                tab.Checked = True
                                Application.DoEvents()
                                For Each tabs As TabControl In ViewCatalog.Descendants(DirectCast(tab.Tag, Control)).OfType(Of TabControl)().ToList()
                                    For Each page As TabPage In tabs.TabPages
                                        tabs.SelectedTab = page
                                        Application.DoEvents()
                                    Next
                                Next
                            Next
                            report.AddRange(watch.Parked().Select(Function(window) "each section shown: " & window))
                        End Using
                        board.NavigateTo(Form1.WorkspacePage.Board)
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        CollectionAssert.AreEqual(
            Array.Empty(Of String)(),
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Leaving the Library disposes the page where it is: its windows are
    ' destroyed while it is still in the page area, and the page area then
    ' holds the Board alone, with the pages kept (#133) hidden beside it.
    <TestMethod>
    Public Sub LeavingAPageDisposesItInPlace()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim host As Control = board.ContentHost

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Dim view As Control = board.PageView
                    Dim parentWhenDestroyed As Control = Nothing
                    Dim destroyed As Boolean = False
                    AddHandler view.HandleDestroyed,
                        Sub(sender, e)
                            destroyed = True
                            parentWhenDestroyed = view.Parent
                        End Sub

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    Assert.IsTrue(destroyed AndAlso view.IsDisposed, "The Library is disposed.")
                    Assert.AreSame(host, parentWhenDestroyed, "The Library's windows are destroyed where they are.")
                    Dim kept As Control() = {board.KeptPage, board.JournalsPage, board.InsightsPage}
                    CollectionAssert.AreEqual(
                        {board.BoardPage},
                        host.Controls.Cast(Of Control)().Where(Function(control) Not kept.Contains(control)).ToArray(),
                        "The page area holds the Board alone after leaving the Library.")
                    Assert.IsFalse(host.Controls.Cast(Of Control)().Any(Function(control) control IsNot board.BoardPage AndAlso control.Visible), "The kept pages are hidden.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Leaving Journals or Insights hides the page where it is, with its
    ' windows, and the next visit shows that same page (#133). Before, each
    ' visit built the page and leaving disposed it.
    <TestMethod>
    Public Sub LeavingJournalsAndInsightsHidesThemInPlace()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim host As Control = board.ContentHost

                    For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Journals, Form1.WorkspacePage.Insights}

                        board.NavigateTo(page)
                        If page = Form1.WorkspacePage.Insights Then board.WaitForInsights()
                        Application.DoEvents()
                        Dim view As Control = board.PageView
                        Dim destroyed As Boolean = False
                        AddHandler view.HandleDestroyed, Sub(sender, e) destroyed = True

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()

                        Assert.IsFalse(destroyed OrElse view.IsDisposed, page.ToString() & " is kept.")
                        Assert.AreSame(host, view.Parent, page.ToString() & " stays in the page area.")
                        Assert.IsFalse(view.Visible, page.ToString() & " is hidden.")
                        Assert.IsTrue(board.BoardPage.Visible, "The Board shows.")

                        board.NavigateTo(page)
                        If page = Form1.WorkspacePage.Insights Then board.WaitForInsights()
                        Application.DoEvents()
                        Assert.AreSame(view, board.PageView, "The next visit shows the same " & page.ToString() & " page.")
                        Assert.IsTrue(view.Visible AndAlso Not destroyed)

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()

                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Journals and Insights are prepared while the window idles after
    ' startup (#133): each is put in the page area hidden, laid out at the
    ' size it shows at, and its windows made there a slice at a time, none
    ' on WinForms' parking window. The first visit then shows that page,
    ' as every later one does. Before, the first visit built it.
    <TestMethod>
    Public Sub JournalsAndInsightsArePreparedWhileTheWindowIdles()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using watch As New WindowCreationWatch(),
                      board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim journals As Control = board.JournalsPage
                    Dim insights As Control = board.InsightsPage

                    For Each prepared As Control In {journals, insights}
                        Assert.IsNotNull(prepared, "Journals and Insights were prepared while the window idled.")
                        Assert.AreSame(board.ContentHost, prepared.Parent, "A prepared page is in the page area.")
                        Assert.IsFalse(prepared.Visible, "A prepared page waits hidden.")
                        Assert.AreEqual(board.ContentHost.DisplayRectangle.Size, prepared.Size, "A prepared page is laid out at the size it shows at.")
                        Assert.IsTrue(prepared.IsHandleCreated, "A prepared page has its window.")
                    Next

                    Assert.IsTrue(ViewCatalog.Descendants(journals).OfType(Of ListBox)().Single().IsHandleCreated, "The journals list has its window.")
                    Assert.IsTrue(ViewCatalog.Descendants(insights).OfType(Of RadioButton)().All(Function(tab) tab.IsHandleCreated), "Insights' tabs have their windows.")

                    ' Tab never reaches a prepared page while it waits.
                    Dim current As Control = board.ActiveControl
                    For steps As Integer = 1 To 200
                        board.SelectNextControl(current, True, True, True, True)
                        current = DeepestActiveControl(board)
                        Assert.IsFalse(current IsNot Nothing AndAlso (journals.Contains(current) OrElse insights.Contains(current)),
                                       "Tab reached " & If(current?.GetType().Name, "nothing") & " in a prepared page.")
                    Next

                    For Each visit As (Page As Form1.WorkspacePage, View As Control) In {(Form1.WorkspacePage.Journals, journals), (Form1.WorkspacePage.Insights, insights)}
                        board.NavigateTo(visit.Page)
                        If visit.Page = Form1.WorkspacePage.Insights Then board.WaitForInsights()
                        Application.DoEvents()
                        Assert.AreSame(visit.View, board.PageView, "The first visit to " & visit.Page.ToString() & " shows the prepared page.")
                        Assert.IsTrue(visit.View.Visible)
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                    Next

                    report.AddRange(watch.Parked())
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        Assert.AreEqual(0, report.Count, "Windows made on the parking window:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' The Journals page shown again reads the author library and the
    ' manuscripts as they are now (#133), as the page built for each visit
    ' did: a journal added and a tag color set since (as the manuscript
    ' page or the Authors view saves them) show and are kept when the page
    ' saves an edit, since the page saves the whole library; a manuscript
    ' newly targeting a journal is counted; and no journal is chosen.
    <TestMethod>
    Public Sub TheJournalsPageShownAgainReadsTheLibraryAfresh()

        StaRunner.Run(
            Sub()
                OnlineAccess.ResetForTests()
                Dim keys As New ProtectedKeyStore(Path.Combine(_root, "keys"))
                OnlineAccess.KeyStoreFactory = Function() keys

                Try
                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()

                        board.NavigateTo(Form1.WorkspacePage.Journals)
                        Application.DoEvents()
                        Dim page As Control = board.PageView
                        Dim editor As Forms.JournalLibraryForm = ViewCatalog.Descendants(page).OfType(Of Forms.JournalLibraryForm)().Single()
                        Dim first As Models.JournalRecord = library.AuthorRepository.Load().Journals.First()
                        editor.SelectJournal(first.Id)
                        Assert.IsNotNull(editor.SelectedJournal)
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()

                        ' Saved elsewhere while the page is hidden.
                        Dim added As New Models.JournalRecord With {.Name = "A Journal Added Since"}
                        Dim since As Models.AuthorLibraryData = library.AuthorRepository.Load()
                        since.Journals.Add(added)
                        since.TagColors.Add(New Models.TagColor With {.Tag = "kept-page tag", .Color = Models.TagPalette.Violet})
                        library.AuthorRepository.Save(since)
                        board.Library.First(Function(item) item.Location = Models.ManuscriptLocation.Pipeline).TargetJournalId = added.Id

                        board.NavigateTo(Form1.WorkspacePage.Journals)
                        Application.DoEvents()
                        Assert.AreSame(page, board.PageView, "The page is kept.")
                        Assert.IsNull(editor.SelectedJournal, "No journal is chosen, as on a new page.")

                        editor.SelectJournal(added.Id)
                        Application.DoEvents()
                        Assert.IsNotNull(editor.SelectedJournal, "The journal added since is listed.")
                        StringAssert.Contains(editor.Card.ShownText, "Target of 1 manuscript", "The manuscript targeting it since is counted.")

                        editor.lookupPrompt =
                            Function(copy)
                                copy.Publisher = "Looked up on the kept page"
                                Return copy
                            End Function
                        editor.LookUpFactsForTest()
                        Application.DoEvents()

                        Dim saved As Models.AuthorLibraryData = library.AuthorRepository.Load()
                        Assert.AreEqual("Looked up on the kept page", saved.Journals.Single(Function(item) item.Id = added.Id).Publisher, "The edit is saved.")
                        Assert.IsTrue(saved.TagColors.Any(Function(item) item.Tag = "kept-page tag" AndAlso item.Color = Models.TagPalette.Violet), "The tag color set since is kept.")

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                End Try
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Leaving Journals reads back the author library its editor saved and
    ' renders the Board from it, as after the Journal Library dialog. The
    ' library is read while the page area still draws (#133): reading it
    ' can ask about a recovered save (#111), and that question over a page
    ' area whose drawing is held would sit on what the page area showed.
    <TestMethod>
    Public Sub LeavingJournalsReadsTheAuthorLibraryWhileThePageAreaDraws()

        Dim reads As List(Of Boolean) = Nothing

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))
                    board.Start()
                    board.WaitForIdleWork()
                    board.NavigateTo(Form1.WorkspacePage.Journals)
                    Application.DoEvents()
                    board.AuthorLibraryReads.Clear()
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    reads = board.AuthorLibraryReads.ToList()
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        CollectionAssert.AreEqual(New List(Of Boolean) From {True}, reads, "Each read on leaving Journals: whether the page area could paint.")

    End Sub


    ' Leaving Journals renders the Board from the library as it is now
    ' (#133): a stage count that changed meanwhile is in the stage filter,
    ' and an attention count that rose from none shows its chip, counted;
    ' one that fell to none hides it.
    <TestMethod>
    Public Sub LeavingJournalsShowsWhatChangedOnTheBoard()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim stages As ComboBox = board.Part(Of ComboBox)("cboStageFilter")
                    Dim chip As Controls.FilterChip = board.Part(Of Controls.FilterChip)("lblMissingJournal")
                    Dim stageItem As Func(Of String, String) =
                        Function(stage) stages.Items.Cast(Of Object)().Select(Function(item) item.ToString()).Single(Function(text) text.StartsWith(stage & " (", StringComparison.Ordinal))
                    Dim visitJournals As Action =
                        Sub()
                            board.NavigateTo(Form1.WorkspacePage.Journals)
                            Application.DoEvents()
                            board.NavigateTo(Form1.WorkspacePage.Board)
                            Application.DoEvents()
                        End Sub

                    ' No manuscript lacks a target journal: the chip goes.
                    For Each manuscript As Models.Manuscript In board.Library.Where(Function(item) String.IsNullOrWhiteSpace(item.TargetJournal))
                        manuscript.TargetJournal = "Fictional Journal 01"
                    Next
                    visitJournals()
                    Assert.AreEqual(0, CInt(chip.Tag), "No manuscript lacks a target journal.")
                    Assert.IsFalse(chip.Visible, "The chip of a count of none is hidden.")

                    ' One manuscript is a draft again, with no target journal.
                    Dim moved As Models.Manuscript = board.Library.First(Function(item) item.Location = Models.ManuscriptLocation.Pipeline AndAlso item.CurrentStage = Models.PaperStage.Submitted)
                    Dim drafts As Integer = board.Library.Where(Function(item) item.CurrentStage = Models.PaperStage.Draft).Count()
                    Dim submitted As Integer = board.Library.Where(Function(item) item.CurrentStage = Models.PaperStage.Submitted).Count()
                    Assert.AreEqual("Draft (" & drafts.ToString() & ")", stageItem("Draft"))
                    moved.CurrentStage = Models.PaperStage.Draft
                    moved.TargetJournal = String.Empty
                    moved.TargetJournalId = Nothing
                    visitJournals()

                    Assert.AreEqual("Draft (" & (drafts + 1).ToString() & ")", stageItem("Draft"), "The stage filter counts the draft.")
                    Assert.AreEqual("Submitted (" & (submitted - 1).ToString() & ")", stageItem("Submitted"))
                    Assert.AreEqual(1, CInt(chip.Tag), "One manuscript lacks a target journal.")
                    Assert.IsTrue(chip.Visible, "Its chip shows.")
                    StringAssert.StartsWith(chip.Text, "1 ", "The chip counts it.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Leaving Journals with nothing changed shows no attention chip again
    ' (#133). The Board renders before it is shown again, and WinForms
    ' treats Visible = True on a chip already shown, under a hidden parent,
    ' as a change: it showed the chip's window again, raised VisibleChanged
    ' and laid out the chips' bar, chip by chip.
    <TestMethod>
    Public Sub LeavingJournalsReshowsNoAttentionChip()

        Dim reshown As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim chips As List(Of Controls.FilterChip) =
                        {"lblOverdueRevisions", "lblRevisionDueSoon", "lblLongReviews", "lblMissingJournal", "lblRecentRejections"}.
                            Select(Function(name) board.Part(Of Controls.FilterChip)(name)).Where(Function(chip) chip.Visible).ToList()
                    Assert.IsTrue(chips.Count > 0, "The Board shows an attention chip.")

                    board.NavigateTo(Form1.WorkspacePage.Journals)
                    Application.DoEvents()

                    ' While the Board is hidden; the Board shown again shows its
                    ' chips, which raises VisibleChanged in each, as it should.
                    Dim changed As EventHandler =
                        Sub(sender, e)
                            If Not board.BoardPage.Visible Then reshown.Add("VisibleChanged on '" & DirectCast(sender, Control).Text & "' under the hidden Board")
                        End Sub
                    For Each chip As Controls.FilterChip In chips
                        AddHandler chip.VisibleChanged, changed
                    Next
                    Using watch As New SentMessageWatch()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        For Each chip As Controls.FilterChip In chips
                            Dim moved As Integer = watch.Count(chip, WM_WINDOWPOSCHANGING)
                            If moved > 0 Then reshown.Add(moved.ToString() & " WM_WINDOWPOSCHANGING to '" & chip.Text & "'")
                        Next
                    End Using
                    For Each chip As Controls.FilterChip In chips
                        RemoveHandler chip.VisibleChanged, changed
                    Next
                    Application.DoEvents()

                    ' Shown again in place of the page, the chips show as they did.
                    reshown.AddRange(chips.Where(Function(chip) Not chip.Visible).Select(Function(chip) "'" & chip.Text & "' is hidden"))

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        Assert.AreEqual(0, reshown.Count, "Leaving Journals with nothing changed:" & Environment.NewLine & String.Join(Environment.NewLine, reshown))

    End Sub

    Private Const WM_WINDOWPOSCHANGING As Integer = &H46


    ' Each visit to Insights begins as a new page did (#133): on Your
    ' Journals, no journal chosen, and the figures worked out anew from the
    ' records as they are, though the page is kept and was left on a
    ' journal's routes, or on Your Journals. Until the figures are back,
    ' nothing of the last visit's shows: no tile, no list, no count on a
    ' tab, only the line that says they are being worked out.
    <TestMethod>
    Public Sub AnInsightsVisitBeginsAsANewPageDid()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    board.NavigateTo(Form1.WorkspacePage.Insights)
                    board.WaitForInsights()
                    Dim page As Control = board.PageView
                    Dim journals As DataGridView = ViewCatalog.Descendants(page).OfType(Of DataGridView)().Single()
                    Assert.IsTrue(journals.Columns.Contains("Journal") AndAlso journals.Rows.Count > 0)
                    journals.CurrentCell = journals.Rows(0).Cells(0)
                    GetType(Control).GetMethod("OnKeyDown", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).
                        Invoke(journals, New Object() {New KeyEventArgs(Keys.Enter)})
                    Application.DoEvents()
                    board.WaitForInsights()
                    Dim routes As DataGridView = ViewCatalog.Descendants(page).OfType(Of DataGridView)().Single()
                    Assert.IsTrue(routes.Columns.Contains("Title"), "A journal's routes show.")
                    Assert.IsTrue(ViewCatalog.Descendants(page).OfType(Of LinkLabel)().Any(Function(link) link.Visible AndAlso link.Text.StartsWith("Showing manuscripts sent to ", StringComparison.Ordinal)))

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    ' The records change while the page is hidden.
                    board.Library.First(Function(item) item.Location = Models.ManuscriptLocation.Pipeline).Location = Models.ManuscriptLocation.Published
                    Dim published As Integer = board.Library.Where(Function(item) item.Location = Models.ManuscriptLocation.Published).Count()

                    board.NavigateTo(Form1.WorkspacePage.Insights)
                    AssertNothingOfTheLastVisitShows(page, "left on a journal's routes")
                    board.WaitForInsights()
                    Assert.AreSame(page, board.PageView, "The page is kept.")
                    Dim checkedTabs As List(Of String) = ViewCatalog.Descendants(page).OfType(Of RadioButton)().Where(Function(tab) tab.Checked).Select(Function(tab) tab.Text).ToList()
                    Assert.AreEqual(1, checkedTabs.Count, String.Join(" | ", checkedTabs))
                    StringAssert.StartsWith(checkedTabs(0), "Your Journals", "The visit begins on Your Journals.")
                    Assert.IsFalse(ViewCatalog.Descendants(page).OfType(Of LinkLabel)().Any(Function(link) link.Visible), "No journal is chosen.")
                    Assert.IsTrue(routes.IsDisposed, "The routes shown before are gone.")
                    Dim grid As DataGridView = ViewCatalog.Descendants(page).OfType(Of DataGridView)().Single()
                    Assert.IsTrue(grid.Columns.Contains("Journal"), "Your Journals shows.")
                    Dim tiles As List(Of String) = ViewCatalog.Descendants(page).OfType(Of Controls.RoundedPanel)().Select(Function(tile) tile.AccessibleName).ToList()
                    Dim expected As String = board.Library.Count.ToString(Globalization.CultureInfo.CurrentCulture) & " manuscripts  ·  " & published.ToString(Globalization.CultureInfo.CurrentCulture) & " published"
                    Assert.IsTrue(tiles.Contains(expected), "The figures are worked out anew: " & String.Join(" | ", tiles))
                    Assert.IsTrue(grid.Visible AndAlso ViewCatalog.Descendants(page).OfType(Of Controls.RoundedPanel)().All(Function(tile) tile.Visible), "The figures show once they are back.")

                    ' Left on Your Journals, as it most often is.
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    board.NavigateTo(Form1.WorkspacePage.Insights)
                    AssertNothingOfTheLastVisitShows(page, "left on Your Journals")
                    board.WaitForInsights()
                    Dim again As DataGridView = ViewCatalog.Descendants(page).OfType(Of DataGridView)().Single()
                    Assert.IsTrue(grid.IsDisposed AndAlso again.Visible AndAlso again.Columns.Contains("Journal"), "Your Journals shows anew.")
                    StringAssert.StartsWith(tabs(page).First().Text, "Your Journals (", "The tab counts once the figures are back.")

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' A kept Insights page just shown again, before its figures are back.
    Private Shared Sub AssertNothingOfTheLastVisitShows(page As Control, left As String)
        Dim shown As List(Of String) =
            ViewCatalog.Descendants(page).Where(Function(control) control.Visible AndAlso (TypeOf control Is DataGridView OrElse TypeOf control Is Controls.RoundedPanel)).
                Select(Function(control) control.GetType().Name & " '" & control.AccessibleName & "'").ToList()
        Assert.AreEqual(0, shown.Count, "Shown again after it was " & left & ", before its figures are back: " & String.Join(" | ", shown))
        CollectionAssert.AreEqual({"Your Journals", "Your Routes", "Route Map", "Your Citations"}, tabs(page).Select(Function(tab) tab.Text).ToList(), "The tabs carry no counts yet.")
        Assert.IsTrue(ViewCatalog.Descendants(page).OfType(Of Label)().Any(Function(label) label.Visible AndAlso label.Text = "Calculating from your records..."), "The page says its figures are being worked out.")
    End Sub


    Private Shared Function Tabs(page As Control) As List(Of RadioButton)
        Return ViewCatalog.Descendants(page).OfType(Of RadioButton)().ToList()
    End Function


    ' A Windows contrast theme turned on while PaperRoute runs, through the
    ' HighContrastOverride seam (#133): a prepared Journals or Insights page
    ' built on the old surface is let go at once, and one showing as it is
    ' left; each is prepared again in the new colors once the window idles,
    ' and that page is the one the next visit shows.
    <TestMethod>
    Public Sub JournalsAndInsightsArePreparedAnewAfterTheSystemColorsChange()

        StaRunner.Run(
            Sub()
                Try
                    UiTheme.HighContrastOverride = False
                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()
                        Dim oldJournals As Control = board.JournalsPage
                        Dim oldInsights As Control = board.InsightsPage
                        Assert.IsNotNull(oldJournals)
                        Assert.IsNotNull(oldInsights)
                        Assert.AreNotEqual(Drawing.SystemColors.Window.ToArgb(), oldInsights.BackColor.ToArgb(), "The first pages are on the board color.")

                        board.NavigateTo(Form1.WorkspacePage.Journals)
                        Application.DoEvents()
                        Assert.AreSame(oldJournals, board.PageView)

                        UiTheme.HighContrastOverride = True
                        board.ChangeSystemColors()
                        Assert.IsTrue(oldInsights.IsDisposed, "The hidden prepared page is let go.")
                        Assert.IsFalse(oldJournals.IsDisposed, "The page showing stays while it shows.")

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        Assert.IsTrue(oldJournals.IsDisposed, "The page that was showing is let go as it is left.")

                        board.WaitForIdleWork()
                        Dim newJournals As Control = board.JournalsPage
                        Dim newInsights As Control = board.InsightsPage

                        For Each visit As (Page As Form1.WorkspacePage, View As Control, Old As Control) In
                            {(Form1.WorkspacePage.Journals, newJournals, oldJournals), (Form1.WorkspacePage.Insights, newInsights, oldInsights)}
                            Assert.IsNotNull(visit.View, visit.Page.ToString() & " is prepared again.")
                            Assert.AreNotSame(visit.Old, visit.View)
                            Assert.IsFalse(visit.View.IsDisposed OrElse visit.View.Visible)
                            Assert.AreEqual(Drawing.SystemColors.Window.ToArgb(), visit.View.BackColor.ToArgb(), visit.Page.ToString() & " is prepared on the window color.")
                            board.NavigateTo(visit.Page)
                            If visit.Page = Form1.WorkspacePage.Insights Then board.WaitForInsights()
                            Application.DoEvents()
                            Assert.AreSame(visit.View, board.PageView, "The next visit shows the page prepared again.")
                            board.NavigateTo(Form1.WorkspacePage.Board)
                            Application.DoEvents()
                        Next

                        board.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' A page switch draws nothing while it runs (#133): no window in the
    ' page area is painted, its frame drawn or its background erased before
    ' the switch returns, from the Board to each page and from each page to
    ' each other, the manuscript page included; nor is any window in the
    ' shelves' host while the Board changes shelves (the two shelf tabs
    ' themselves repaint as WinForms checks a radio button, in its Checked
    ' setter before CheckedChanged, outside what a shelf switch can hold).
    ' What the switch changed is drawn once it is done, as the window
    ' processes its messages. Drawing during a switch is where this
    ' machine held a switch half a second, waiting on the screen. Before
    ' switches held drawing (a80a319) this test read 789 lines, Library to
    ' Board among them: "WM_PAINT to ComboBox 'Filter by stage'" and
    ' "WM_PAINT to ComboBox 'Sort the board'", sent as the Board came back
    ' and the boxes were resized, with erases of the Board's panels; and a
    ' shelf switch had drawn everything before it returned, so nothing was
    ' left to draw once the window processed its messages. Nor is the rail
    ' drawn while any switch runs: the two page buttons a switch checks
    ' and unchecks drew themselves there and then until SyncRail held the
    ' rail's pages (#142), and its collapse button takes the keyboard for a
    ' moment on the way back to the Board (#213). With motion on (#142),
    ' each switch but a manuscript's open also reads the page left and
    ' shows its picture over the page area as it returns (the picture
    ' waited for here, so every switch that can fade does): that draws
    ' nothing either. Each fade is let end before the next switch, as the
    ' key or click that starts a switch ends a fade still showing before
    ' the switch begins.
    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub SwitchesPaintNothingSynchronously(motion As Boolean)

        Dim report As New List(Of String)()
        Dim faded As Integer = 0

        StaRunner.Run(
            Sub()
                UiMotion.EnabledOverride = motion
                Controls.PageFade.AwaitPicture = motion
                ' Motion on is on whatever the host's theme: a contrast theme stops it.
                If motion Then UiTheme.HighContrastOverride = False
                ' The test host is DPI-unaware: above 100% Windows scales the
                ' board, where a switch is a cut.
                If motion Then Controls.PageFade.ScaledOverride = False
                Try
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    ' What the switches draw, and nothing of the tests' own
                    ' check that an open left nothing to lay out: laying a
                    ' page out again once its hold is released has Windows
                    ' frame and erase what it touches there and then.
                    board.VerifySettled = False
                    board.Start()
                    board.WaitForIdleWork()
                    Dim subject As Models.Manuscript = board.Library.First(Function(item) item.Location = Models.ManuscriptLocation.Pipeline)

                    ' The manuscript page and the kept pages exist, as after a
                    ' first visit to each.
                    board.Open(subject)
                    Application.DoEvents()
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    board.WaitForIdleWork()

                    Dim pages As Form1.WorkspacePage() = {
                        Form1.WorkspacePage.Library, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Deadlines,
                        Form1.WorkspacePage.Insights, Form1.WorkspacePage.ImportExport, Form1.WorkspacePage.Manuscript}

                    Dim go As Action(Of Form1.WorkspacePage) =
                        Sub(page)
                            If page = Form1.WorkspacePage.Manuscript Then
                                board.Open(subject)
                            Else
                                board.NavigateTo(page)
                            End If
                        End Sub

                    ' The rail is watched through every switch too: the two page
                    ' buttons a switch checks and unchecks (SyncRail), and the
                    ' collapse button, which the keyboard passes through on the
                    ' way back to the Board from a page that had it (#213).
                    Dim rail As Control = ViewCatalog.Descendants(board).OfType(Of Controls.RailButton)().First().Parent.Parent

                    Dim watchSwitch As Action(Of String, Control, Action) =
                        Sub(name, within, switch)
                            Dim painted As Boolean = False
                            Using watch As New PaintWatch(within), railWatch As New PaintWatch(rail)
                                switch()
                                report.AddRange(watch.Sent().Select(Function(message) name & ": " & message))
                                report.AddRange(railWatch.Sent().Select(Function(message) name & ", the rail: " & message))
                            End Using
                            Dim shown As Control = If(board.PageView, board.BoardPage)
                            Dim handler As PaintEventHandler = Sub(sender, e) painted = True
                            Dim parts As List(Of Control) = {shown}.Concat(ViewCatalog.Descendants(shown)).Where(Function(control) control.Visible).ToList()
                            For Each part As Control In parts
                                AddHandler part.Paint, handler
                            Next
                            Application.DoEvents()
                            For Each part As Control In parts
                                RemoveHandler part.Paint, handler
                            Next
                            If Not painted Then report.Add(name & ": nothing was drawn once the window processed its messages")
                            If board.PageName = Form1.WorkspacePage.Insights.ToString() Then board.WaitForInsights()
                            board.WaitForIdleWork()
                            ' A switch begun while the screen may still show a fade is
                            ' a cut, so each is given the time a fade takes to go.
                            If motion Then
                                While board.PageFade IsNot Nothing AndAlso board.PageFade.Fading
                                    Application.DoEvents()
                                    Threading.Thread.Sleep(1)
                                End While
                                Threading.Thread.Sleep(60)
                            End If
                        End Sub

                    For Each first As Form1.WorkspacePage In pages
                        watchSwitch("Board to " & first.ToString(), board.ContentHost, Sub() go(first))
                        For Each second As Form1.WorkspacePage In pages.Where(Function(page) page <> first)
                            watchSwitch(first.ToString() & " to " & second.ToString(), board.ContentHost, Sub() go(second))
                            watchSwitch(second.ToString() & " back to " & first.ToString(), board.ContentHost, Sub() go(first))
                        Next
                        watchSwitch(first.ToString() & " to Board", board.ContentHost, Sub() board.NavigateTo(Form1.WorkspacePage.Board))
                    Next

                    Dim shelves As Control = board.Shelf("pipelinePanel").Parent
                    For Each tab As String In {"tabPublished", "tabFileDrawer", "tabPipeline", "tabFileDrawer", "tabPublished", "tabPipeline"}
                        watchSwitch("the shelf " & tab, shelves, Sub() board.ShelfTab(tab).Checked = True)
                    Next

                    ' With the keyboard on a card's title (#213): the page opened
                    ' takes it, and on the way back the title takes it again, which
                    ' shows the row after its card (RevealPastCard); neither draws
                    ' while the switch runs.
                    For Each page As Form1.WorkspacePage In pages
                        Dim title As LinkLabel = board.Shelf("pipelinePanel").Controls.OfType(Of Controls.RoundedPanel)().First().Controls.OfType(Of LinkLabel)().First()
                        If Not title.Focus() Then board.ActiveControl = title
                        Application.DoEvents()
                        watchSwitch("Board to " & page.ToString() & " from a card's title", board.ContentHost, Sub() go(page))
                        watchSwitch(page.ToString() & " back to the card's title", board.ContentHost, Sub() board.NavigateTo(Form1.WorkspacePage.Board))
                        Dim back As Control = DeepestActiveControl(board)
                        If back IsNot title Then report.Add(page.ToString() & " back to the Board: the keyboard is on " & If(back?.GetType().Name, "nothing") & ", not the card's title")
                    Next

                    faded = If(board.PageFade?.FadesShown, 0)
                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
                Finally
                    UiMotion.EnabledOverride = Nothing
                    Controls.PageFade.AwaitPicture = False
                    UiTheme.HighContrastOverride = Nothing
                    Controls.PageFade.ScaledOverride = Nothing
                End Try
            End Sub,
            TimeSpan.FromMinutes(10))

        Assert.AreEqual(0, report.Count, "Drawn while a switch ran:" & Environment.NewLine & String.Join(Environment.NewLine, report.Take(40)))

        ' With motion on, the switches did fade: 42 between pages, 12 to and
        ' from the Board with the keyboard on a title, less the opens of the
        ' manuscript, which are cuts.
        If motion Then
            Assert.IsTrue(faded >= 40, "Switches faded: " & faded.ToString())
        Else
            Assert.AreEqual(0, faded, "With motion off nothing fades.")
        End If

    End Sub


    Private Shared Sub AssertColumnsFitAllCells(grid As DataGridView, context As String)

        ' The width sizing to all cells gave each column in the window: its
        ' widest cell, or its heading on one line, as the page showed it. (A
        ' column sized to its cells again later may wrap its heading to the
        ' height another heading needs; the page never showed that.) The
        ' columns cannot be dragged wider, as before.
        For Each name As String In {"Stage", "Shelf", "Type", "Days"}
            Dim column As DataGridViewColumn = grid.Columns(name)
            Dim widest As Integer =
                Math.Max(
                    column.HeaderCell.PreferredSize.Width,
                    column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCellsExceptHeader, True))
            Assert.AreEqual(widest, column.Width, "The " & name & " column " & context & ", over " & grid.Rows.Count.ToString() & " rows.")
            Assert.IsFalse(column.Resizable = DataGridViewTriState.True, "The " & name & " column cannot be dragged wider.")
        Next

    End Sub

End Class


' Records each paint, frame drawing and background erase sent to a window
' inside a control while it is alive: the ones a window is given there and
' then, rather than when its thread next has nothing else to do (a
' thread-local WH_CALLWNDPROC hook, which hears only messages sent, so
' paints the message loop dispatches later are not counted).
Friend NotInheritable Class PaintWatch
    Implements IDisposable

    Private ReadOnly _callback As HookProc
    Private ReadOnly _hook As IntPtr
    Private ReadOnly _within As IntPtr
    Private ReadOnly _sent As New List(Of String)()

    Private Delegate Function HookProc(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

    Private Const WH_CALLWNDPROC As Integer = 4
    Private Const WM_PAINT As Integer = &HF
    Private Const WM_ERASEBKGND As Integer = &H14
    Private Const WM_NCPAINT As Integer = &H85

    <StructLayout(LayoutKind.Sequential)>
    Private Structure CWPSTRUCT
        Public lParam As IntPtr
        Public wParam As IntPtr
        Public message As Integer
        Public hwnd As IntPtr
    End Structure


    Public Sub New(within As Control)
        _within = within.Handle
        _callback = AddressOf Hook
        _hook = SetWindowsHookEx(WH_CALLWNDPROC, _callback, IntPtr.Zero, GetCurrentThreadId())
    End Sub


    ' Each message sent, as "WM_PAINT to Label 'name'".
    Public Function Sent() As List(Of String)
        Return _sent.ToList()
    End Function


    Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If code >= 0 Then
            Try
                Dim sent As CWPSTRUCT = Marshal.PtrToStructure(Of CWPSTRUCT)(lParam)
                If (sent.message = WM_PAINT OrElse sent.message = WM_ERASEBKGND OrElse sent.message = WM_NCPAINT) AndAlso
                   (sent.hwnd = _within OrElse IsChild(_within, sent.hwnd)) Then
                    Dim target As Control = Control.FromHandle(sent.hwnd)
                    _sent.Add(If(sent.message = WM_PAINT, "WM_PAINT", If(sent.message = WM_NCPAINT, "WM_NCPAINT", "WM_ERASEBKGND")) & " to " &
                              If(target Is Nothing, "a window of no control", target.GetType().Name & If(String.IsNullOrEmpty(target.AccessibleName), String.Empty, " '" & target.AccessibleName & "'")))
                End If
            Catch ex As Exception
                _sent.Add("a message not read: " & ex.GetType().Name)
            End Try
        End If
        Return CallNextHookEx(_hook, code, wParam, lParam)
    End Function


    Public Sub Dispose() Implements IDisposable.Dispose
        UnhookWindowsHookEx(_hook)
    End Sub


    <DllImport("user32.dll")>
    Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As HookProc, hMod As IntPtr, threadId As Integer) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsChild(parent As IntPtr, window As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

End Class


' Counts the messages sent to the test's thread's windows while it is
' alive, by window and message (a thread-local WH_CALLWNDPROC hook, as
' PaintWatch's): what a step asks of a window there and then.
Friend NotInheritable Class SentMessageWatch
    Implements IDisposable

    Private ReadOnly _callback As HookProc
    Private ReadOnly _hook As IntPtr
    Private ReadOnly _counts As New Dictionary(Of (Window As IntPtr, Message As Integer), Integer)()

    Private Delegate Function HookProc(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

    Private Const WH_CALLWNDPROC As Integer = 4

    <StructLayout(LayoutKind.Sequential)>
    Private Structure CWPSTRUCT
        Public lParam As IntPtr
        Public wParam As IntPtr
        Public message As Integer
        Public hwnd As IntPtr
    End Structure


    Public Sub New()
        _callback = AddressOf Hook
        _hook = SetWindowsHookEx(WH_CALLWNDPROC, _callback, IntPtr.Zero, GetCurrentThreadId())
    End Sub


    ' How many times a message was sent to a control's window.
    Public Function Count(control As Control, message As Integer) As Integer
        Dim found As Integer = 0
        If control.IsHandleCreated Then _counts.TryGetValue((control.Handle, message), found)
        Return found
    End Function


    Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If code >= 0 Then
            Dim sent As CWPSTRUCT = Marshal.PtrToStructure(Of CWPSTRUCT)(lParam)
            Dim key As (IntPtr, Integer) = (sent.hwnd, sent.message)
            Dim found As Integer = 0
            _counts.TryGetValue(key, found)
            _counts(key) = found + 1
        End If
        Return CallNextHookEx(_hook, code, wParam, lParam)
    End Function


    Public Sub Dispose() Implements IDisposable.Dispose
        UnhookWindowsHookEx(_hook)
    End Sub


    <DllImport("user32.dll")>
    Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As HookProc, hMod As IntPtr, threadId As Integer) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

End Class


' Records every window the test's thread creates while it is alive, with
' where it was created (a thread-local CBT hook, so no other process or
' thread is touched).
Friend NotInheritable Class WindowCreationWatch
    Implements IDisposable

    Private ReadOnly _callback As HookProc
    Private ReadOnly _hook As IntPtr
    Private ReadOnly _windows As New List(Of (Description As String, Parked As Boolean, Child As Boolean))()

    Private Delegate Function HookProc(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

    Private Const WH_CBT As Integer = 5
    Private Const HCBT_CREATEWND As Integer = 3
    Private Const WS_CHILD As Integer = &H40000000
    Private Const GA_ROOT As UInteger = 2


    Public Sub New()
        _callback = AddressOf Hook
        _hook = SetWindowsHookEx(WH_CBT, _callback, IntPtr.Zero, GetCurrentThreadId())
    End Sub


    ' Every window created, popups such as tooltips included.
    Public ReadOnly Property Count As Integer
        Get
            Return _windows.Count
        End Get
    End Property


    ' The windows created inside WinForms' parking window, which holds a
    ' control's window until its parent has one, by class and parent.
    Public Function Parked() As List(Of String)
        Return _windows.Where(Function(window) window.Parked).Select(Function(window) window.Description).ToList()
    End Function


    ' The child windows created, wherever: each one a control's own.
    Public Function Children() As List(Of String)
        Return _windows.Where(Function(window) window.Child).Select(Function(window) window.Description).ToList()
    End Function


    ' Every call is passed on (CallNextHookEx), whatever recording it does:
    ' another CBT hook on the thread, as the dialog hook is
    ' (WindowChrome.HookDialogs, #188), hears each window too, and nothing
    ' may escape into Windows. A window it could not record is counted as
    ' parked and as a child, so no test passes over it.
    Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If code = HCBT_CREATEWND Then
            Try
                Record(wParam, lParam)
            Catch ex As Exception
                _windows.Add(("a window not recorded: " & ex.GetType().Name & ": " & ex.Message, True, True))
            End Try
        End If
        Return CallNextHookEx(_hook, code, wParam, lParam)
    End Function


    Private Sub Record(wParam As IntPtr, lParam As IntPtr)
        ' CBT_CREATEWND: a CREATESTRUCT pointer, whose hwndParent follows
        ' three pointers and whose style follows four ints after it.
        Dim create As IntPtr = Marshal.ReadIntPtr(lParam)
        Dim parent As IntPtr = Marshal.ReadIntPtr(create, IntPtr.Size * 3)
        Dim style As Integer = Marshal.ReadInt32(create, IntPtr.Size * 4 + 16)
        Dim root As Control = If(parent = IntPtr.Zero, Nothing, Control.FromHandle(GetAncestor(parent, GA_ROOT)))
        Dim parked As Boolean = (style And WS_CHILD) <> 0 AndAlso root IsNot Nothing AndAlso root.GetType().Name.Contains("ParkingWindow", StringComparison.Ordinal)
        ' Where in PaperRoute the window was asked for, for the message.
        Dim frames As List(Of String) = Environment.StackTrace.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(frame) frame.Trim().Split(" in ")(0)).ToList()
        Dim app As Integer = frames.FindIndex(Function(frame) frame.StartsWith("at ManuscriptPipeline.", StringComparison.Ordinal))
        Dim caller As String = If(app < 0, String.Empty, ", " & frames(app) & " < " & String.Join(" < ", frames.Take(app).Skip(Math.Max(0, app - 3))))
        _windows.Add((ClassOf(wParam) & " in " & Describe(Control.FromHandle(parent)) & caller, parked, (style And WS_CHILD) <> 0))
    End Sub


    Private Shared Function ClassOf(handle As IntPtr) As String
        Dim name As New Text.StringBuilder(256)
        GetClassName(handle, name, name.Capacity)
        Return name.ToString()
    End Function


    Private Shared Function Describe(control As Control) As String
        Return If(control Is Nothing, "(no control)", control.GetType().Name & If(String.IsNullOrEmpty(control.AccessibleName), String.Empty, " '" & control.AccessibleName & "'"))
    End Function


    Public Sub Dispose() Implements IDisposable.Dispose
        UnhookWindowsHookEx(_hook)
    End Sub


    <DllImport("user32.dll")>
    Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As HookProc, hMod As IntPtr, threadId As Integer) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetAncestor(handle As IntPtr, flags As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(handle As IntPtr, text As Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

End Class
