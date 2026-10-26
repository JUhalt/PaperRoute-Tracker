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


    ' Leaving a page built for its visit disposes it where it is: its
    ' windows are destroyed while it is still in the page area, and the
    ' page area then holds the Board alone, with the pages kept (#133)
    ' hidden beside it. Such pages are now the Library left on its Authors
    ' & Affiliations view, whose editor read the author library as it was
    ' made, and the page for a manuscript the library no longer holds. The
    ' Library left on its Manuscripts view is kept, hidden where it is, and
    ' the next visit shows that same page (#133); until it was kept, every
    ' Library left was disposed in place as these are.
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
                    Dim kept As Control = board.PageView
                    Dim keptDestroyed As Boolean = False
                    AddHandler kept.HandleDestroyed, Sub(sender, e) keptDestroyed = True

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    Assert.IsFalse(keptDestroyed OrElse kept.IsDisposed, "The Library left on its Manuscripts view is kept.")
                    Assert.AreSame(host, kept.Parent, "The Library stays in the page area.")
                    Assert.IsFalse(kept.Visible, "The Library is hidden.")

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Assert.AreSame(kept, board.PageView, "The next visit shows the same Library page.")

                    Dim visits As (Name As String, Open As Action)() = {
                        ("The Library left on its Authors & Affiliations view",
                         Sub() ViewCatalog.Descendants(board.PageView).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Authors & Affiliations").Checked = True),
                        ("The page of a manuscript the library no longer holds",
                         Sub() board.Open(New Models.Manuscript With {.Title = "Not in the library"}))
                    }

                    For Each visit In visits

                        visit.Open()
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

                        Assert.IsTrue(destroyed AndAlso view.IsDisposed, visit.Name & " is disposed.")
                        Assert.AreSame(host, parentWhenDestroyed, visit.Name & ": its windows are destroyed where they are.")
                        Dim keptPages As Control() = {board.KeptPage, board.JournalsPage, board.InsightsPage, board.LibraryPage}
                        CollectionAssert.AreEqual(
                            {board.BoardPage},
                            host.Controls.Cast(Of Control)().Where(Function(control) Not keptPages.Contains(control)).ToArray(),
                            visit.Name & ": the page area holds the Board alone, beside the kept pages.")
                        Assert.IsFalse(host.Controls.Cast(Of Control)().Any(Function(control) control IsNot board.BoardPage AndAlso control.Visible), "The kept pages are hidden.")

                    Next

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
    Private Const WM_GETTEXT As Integer = &HD
    Private Const WM_GETTEXTLENGTH As Integer = &HE
    Private Const CB_RESETCONTENT As Integer = &H14B


    ' An attention chip keeps its text and its bold weight rather than
    ' reading the text from its window and making the font at each measure
    ' (#133); it measures as it did, in every tone, as its text, its tone
    ' and its font change.
    <TestMethod>
    Public Sub AFilterChipMeasuresAsBefore()

        StaRunner.Run(
            Sub()
                ' Disposed once the window that took them has closed.
                Using larger As New Drawing.Font("Segoe UI", 14.0F),
                      italic As New Drawing.Font("Segoe UI", 9.0F, Drawing.FontStyle.Italic),
                      host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Drawing.Point(-20000, -20000), .ShowInTaskbar = False}
                    Dim chip As New Controls.FilterChip With {.Text = "3 overdue revisions"}
                    host.Controls.Add(chip)
                    host.Show()
                    Application.DoEvents()

                    Dim expected As Func(Of Drawing.Size) =
                        Function()
                            Using bold As New Drawing.Font(chip.Font, Drawing.FontStyle.Bold)
                                Dim font As Drawing.Font = If(chip.Tone = Controls.FilterChipTone.Neutral, chip.Font, bold)
                                Dim text As Drawing.Size = TextRenderer.MeasureText(chip.Text, font, Drawing.Size.Empty, TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
                                Return New Drawing.Size(text.Width + chip.Padding.Horizontal, text.Height + chip.Padding.Vertical)
                            End Using
                        End Function

                    For Each font As Drawing.Font In {host.Font, larger, italic}
                        host.Font = font
                        For Each tone As Controls.FilterChipTone In [Enum].GetValues(GetType(Controls.FilterChipTone))
                            chip.Tone = tone
                            For Each text As String In {"3 overdue revisions", "12 waiting 90+ days"}
                                chip.Text = text
                                Assert.AreEqual(expected(), chip.GetPreferredSize(Drawing.Size.Empty), tone.ToString() & ", " & font.ToString() & ", """ & text & """")
                                Assert.AreEqual(text, chip.Text)
                            Next
                        Next
                    Next

                    host.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(1))

    End Sub


    ' Leaving Journals with nothing changed renders the Board without
    ' filling the stage filter again or reading the search box once per
    ' manuscript (#133): the stage filter refilled the same nine options
    ' (emptying the box, adding each, measuring each and resizing the box
    ' and its list), and each manuscript read the search text from its
    ' window, 60 reads here and 500 on the 500 library. An attention chip
    ' keeps its own text, so nothing reads it from the chip's window: it
    ' was read at each of its measures and each time its text was set.
    <TestMethod>
    Public Sub LeavingJournalsWithNothingChangedRefillsNoFilter()

        Dim refills As Integer = -1
        Dim reads As Integer = -1
        Dim chipReads As New List(Of String)()
        Dim resized As Boolean = False

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim stages As ComboBox = board.Part(Of ComboBox)("cboStageFilter")
                    Dim search As TextBox = board.Part(Of TextBox)("txtBoardSearch")
                    Dim chips As List(Of Controls.FilterChip) =
                        {"lblOverdueRevisions", "lblRevisionDueSoon", "lblLongReviews", "lblMissingJournal", "lblRecentRejections"}.
                            Select(Function(name) board.Part(Of Controls.FilterChip)(name)).Where(Function(chip) chip.Visible AndAlso chip.IsHandleCreated).ToList()
                    Assert.IsTrue(chips.Count > 0, "The Board shows an attention chip.")
                    Dim before As String() = stages.Items.Cast(Of Object)().Select(Function(item) item.ToString()).ToArray()

                    board.NavigateTo(Form1.WorkspacePage.Journals)
                    Application.DoEvents()
                    Using watch As New SentMessageWatch()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        refills = watch.Count(stages, CB_RESETCONTENT)
                        reads = watch.Count(search, WM_GETTEXTLENGTH)
                        For Each chip As Controls.FilterChip In chips
                            Dim read As Integer = watch.Count(chip, WM_GETTEXTLENGTH) + watch.Count(chip, WM_GETTEXT)
                            If read > 0 Then chipReads.Add("'" & chip.Text & "' read " & read.ToString() & " times")
                        Next
                    End Using
                    Application.DoEvents()

                    CollectionAssert.AreEqual(before, stages.Items.Cast(Of Object)().Select(Function(item) item.ToString()).ToArray(), "The stage filter holds the same options.")

                    ' In another font, the same options are filled and sized
                    ' again, as the box was sized for its widest one.
                    Dim larger As New Drawing.Font(board.Font.FontFamily, board.Font.SizeInPoints * 1.5F)
                    board.Font = larger
                    board.Render()
                    Dim widest As Integer = stages.Items.Cast(Of Object)().Max(Function(item) TextRenderer.MeasureText(item.ToString(), board.Font).Width)
                    resized = stages.Width = Math.Max(150, widest + 46)

                    board.Close()
                    larger.Dispose()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        Dim problems As New List(Of String)()
        If refills <> 0 Then problems.Add("The stage filter was emptied and filled again " & refills.ToString() & " times.")
        If Not resized Then problems.Add("A render in a larger font left the stage filter at its width before.")
        ' Once for the filter, and once each for the three shelves' headings
        ' and the Clear filters button.
        If reads > 5 Then problems.Add("The search box was read " & reads.ToString() & " times in one render.")
        If chipReads.Count > 0 Then problems.Add("The attention chips' text was read from their windows: " & String.Join("; ", chipReads))
        Assert.AreEqual(0, problems.Count, String.Join(Environment.NewLine, problems))

    End Sub


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


    ' The Library is prepared while the window idles after startup, as
    ' Journals and Insights are (#133): built with every manuscript in its
    ' grid, put in the page area hidden, laid out at the size it shows at,
    ' and its windows made there a slice at a time, none on WinForms'
    ' parking window. Tab never reaches it while it waits, and the first
    ' visit shows that page. Before, every visit built it: 14 windows, its
    ' grid's among them, made as the page appeared.
    <TestMethod>
    Public Sub TheLibraryIsPreparedWhileTheWindowIdles()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using watch As New WindowCreationWatch(),
                      board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim prepared As Control = LibraryPageIn(board)
                    Assert.IsNotNull(prepared, "The Library was prepared while the window idled.")
                    Assert.IsFalse(prepared.Visible, "The prepared Library waits hidden.")
                    Assert.AreEqual(board.ContentHost.DisplayRectangle.Size, prepared.Size, "The prepared Library is laid out at the size it shows at.")
                    Assert.IsTrue(prepared.IsHandleCreated, "The prepared Library has its window.")
                    Dim grid As DataGridView = ViewCatalog.Descendants(prepared).OfType(Of DataGridView)().Single()
                    Assert.IsTrue(grid.IsHandleCreated, "Its grid has its window.")
                    CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(grid), "Its grid lists every manuscript.")
                    Console.WriteLine("The prepared Library holds " & {prepared}.Concat(ViewCatalog.Descendants(prepared)).Where(Function(control) control.IsHandleCreated).Count().ToString() & " windows while it waits hidden.")

                    ' Tab never reaches the prepared page while it waits.
                    Dim current As Control = board.ActiveControl
                    For steps As Integer = 1 To 200
                        board.SelectNextControl(current, True, True, True, True)
                        current = DeepestActiveControl(board)
                        Assert.IsFalse(current IsNot Nothing AndAlso prepared.Contains(current),
                                       "Tab reached " & If(current?.GetType().Name, "nothing") & " in the prepared Library.")
                    Next

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Assert.AreSame(prepared, board.PageView, "The first visit to the Library shows the prepared page.")
                    Assert.IsTrue(prepared.Visible)
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    report.AddRange(watch.Parked())
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        Assert.AreEqual(0, report.Count, "Windows made on the parking window:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Each visit to the Library begins as a new page did (#133), though the
    ' page is kept: on its Manuscripts view, the filter empty, every
    ' manuscript in the library's order with no column sorted, the grid at
    ' its top with no row current or selected (a new page's grid makes its
    ' first row current only as it takes the keyboard), its columns as wide
    ' as they were built, and the keyboard, entering by Ctrl+2, in the
    ' filter. The first visit, which shows the page as it was built, is
    ' the measure. Left filtered and sorted with a row current, then
    ' scrolled with a column made wider and the keyboard in the grid, then
    ' twice on its Authors & Affiliations view (the visit after it shown
    ' on a page built for it, then on one prepared again), the next visit
    ' begins as the first did. A guard: before the Library was kept, every
    ' visit built a new page, and this held there too.
    <TestMethod>
    Public Sub ALibraryVisitBeginsAsANewPageDid()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim search As TextBox = board.Part(Of TextBox)("txtBoardSearch")

                    Dim visit As Func(Of List(Of String)) =
                        Function()
                            board.ActiveControl = search
                            board.PressCommandKey(Keys.Control Or Keys.D2)
                            Application.DoEvents()
                            board.WaitForIdleWork()
                            Return DescribeLibraryVisit(board)
                        End Function

                    Dim fresh As List(Of String) = visit()
                    CollectionAssert.AreEqual(
                        New List(Of String) From {
                            "view: Manuscripts (60)",
                            "filter: ''",
                            "sorted: none None, glyphs: ",
                            "current: none, selected rows: 0",
                            "top row: 0, offset: 0",
                            "keyboard: TextBox 'Filter the Library'"
                        },
                        fresh.Where(Function(line) Not line.StartsWith("columns:", StringComparison.Ordinal)).ToList(),
                        "The first visit shows the page as it was built:" & Environment.NewLine & String.Join(Environment.NewLine, fresh))
                    Dim grid As DataGridView = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                    CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(grid), "The first visit lists every manuscript in the library's order.")

                    ' Filtered and sorted, a row current.
                    board.TypeInLibraryFilter("Synthetic 01")
                    board.PauseTyping()
                    grid.Sort(grid.Columns("Days"), System.ComponentModel.ListSortDirection.Descending)
                    grid.CurrentCell = grid.Rows(grid.Rows.Count \ 2).Cells(1)
                    Assert.IsTrue(grid.Rows.Count < board.Library.Count AndAlso grid.SortedColumn IsNot Nothing AndAlso grid.SelectedRows.Count = 1)
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    Dim again As List(Of String) = visit()
                    CollectionAssert.AreEqual(fresh, again, "Left filtered and sorted with a row current:" & Environment.NewLine & String.Join(Environment.NewLine, again))
                    grid = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                    CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(grid), "Left filtered and sorted: every manuscript, in the library's order.")

                    ' Scrolled, a column made wider, the keyboard in the grid.
                    grid.Columns("Journal").Width += 80
                    grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1
                    board.ActiveControl = grid
                    grid.CurrentCell = grid.Rows(grid.Rows.Count - 1).Cells(0)
                    Application.DoEvents()
                    Assert.IsTrue(grid.FirstDisplayedScrollingRowIndex > 0 AndAlso DeepestActiveControl(board) Is grid)
                    Dim widths As Func(Of List(Of String), String) = Function(lines) lines.Single(Function(line) line.StartsWith("columns:", StringComparison.Ordinal))
                    Assert.AreNotEqual(widths(fresh), widths(DescribeLibraryVisit(board)), "The Journal column was made wider.")
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    again = visit()
                    CollectionAssert.AreEqual(fresh, again, "Left scrolled with a column made wider and the keyboard in the grid:" & Environment.NewLine & String.Join(Environment.NewLine, again))

                    ' On its Authors & Affiliations view, which lets the page go:
                    ' the next visit, made before the window processes a message,
                    ' shows a page built for it, and the one after that, made once
                    ' the window has idled, a page prepared again.
                    For Each prepared As Boolean In {False, True}
                        ViewCatalog.Descendants(board.PageView).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Authors & Affiliations").Checked = True
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        If prepared Then board.WaitForIdleWork()
                        again = visit()
                        CollectionAssert.AreEqual(fresh, again, "Left on its Authors & Affiliations view, then shown " & If(prepared, "prepared again", "built for the visit") & ":" & Environment.NewLine & String.Join(Environment.NewLine, again))
                    Next

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' A Library whose Authors & Affiliations view failed to open (#133):
    ' that view's editor reads the author library as it is made, which
    ' throws for one damaged or locked, and once the error's Keep working
    ' is chosen the page is left with neither its grid nor the editor.
    ' Left so, it is let go as a Library left on that view is, and each
    ' visit after begins as a new page did, listing every manuscript: the
    ' next, on a page built for it or one prepared again once the window
    ' idled, and the one after, on the page kept since. Until the page
    ' left without its grid was let go it was kept, and every later visit
    ' threw (a NullReferenceException in BeginLibraryVisit, the Board still
    ' showing) until the colors changed or PaperRoute started again.
    <TestMethod>
    Public Sub TheLibraryComesBackAfterItsAuthorsViewFailedToOpen()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 20)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Dim search As TextBox = board.Part(Of TextBox)("txtBoardSearch")

                    Dim visit As Func(Of List(Of String)) =
                        Function()
                            board.ActiveControl = search
                            board.PressCommandKey(Keys.Control Or Keys.D2)
                            Application.DoEvents()
                            board.WaitForIdleWork()
                            Return DescribeLibraryVisit(board)
                        End Function

                    Dim fresh As List(Of String) = visit()
                    Assert.AreEqual("view: Manuscripts (20)", fresh(0), "The first visit shows the Manuscripts view.")

                    Dim visitAgain As Action(Of String) =
                        Sub(shown)
                            Dim again As List(Of String) = visit()
                            Dim context As String = "After the Authors & Affiliations view failed to open, the Library shown on " & shown
                            Assert.AreEqual(Form1.WorkspacePage.Library.ToString(), board.PageName, context & ": the window is on the Library.")
                            Assert.IsFalse(board.BoardPage.Visible, context & ": the Board is hidden.")
                            CollectionAssert.AreEqual(fresh, again, context & ":" & Environment.NewLine & String.Join(Environment.NewLine, again))
                            CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()), context & ": every manuscript, in the library's order.")
                        End Sub

                    For Each prepared As Boolean In {False, True}
                        Dim authors As RadioButton = ViewCatalog.Descendants(board.PageView).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Authors & Affiliations")
                        board.AuthorEditorFails = True
                        Assert.ThrowsExactly(Of InvalidDataException)(Sub() authors.Checked = True)
                        board.AuthorEditorFails = False
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        If prepared Then board.WaitForIdleWork()
                        visitAgain(If(prepared, "a page prepared again", "a page built for the visit"))
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        visitAgain("the page kept since")
                    Next

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Each visit to the Library shows the library as it is now (#133),
    ' though the page is kept: what a page built for the visit would list,
    ' in the library's order, each row the library's own record, its short
    ' columns as wide as their widest text, after an edit saved on the
    ' manuscript page, a manuscript added, one deleted from its page opened
    ' from the Library (back on the Library), an import, a move to the File
    ' Drawer and one back, a restore's records (equal, but not the same
    ' objects), a change no render followed, as the Journals and Insights
    ' tests make, and every type cleared with no render either, which
    ' leaves the Type column as narrow as a new page's only if the columns
    ' are fitted again once cells are set (without that it kept its width).
    ' A guard: before the Library was kept every visit built a new page
    ' from the records, and this held there too.
    <TestMethod>
    Public Sub TheLibraryShownAgainShowsTheLibraryAsItIsNow()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    Dim visitAfter As Action(Of String, Action) =
                        Sub(change, act)
                            act()
                            Application.DoEvents()
                            board.WaitForIdleWork()
                            If board.PageName <> Form1.WorkspacePage.Library.ToString() Then
                                board.NavigateTo(Form1.WorkspacePage.Library)
                                Application.DoEvents()
                            End If
                            Dim grid As DataGridView = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                            CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(grid), "After " & change & ", the Library lists the library as it is now.")
                            Assert.IsTrue(Enumerable.Range(0, grid.Rows.Count).All(Function(index) grid.Rows(index).Tag Is board.Library(index)),
                                          "After " & change & ", each row is the library's own record.")
                            AssertColumnsFitAllCells(grid, "after " & change)
                            board.NavigateTo(Form1.WorkspacePage.Board)
                            Application.DoEvents()
                            board.WaitForIdleWork()
                        End Sub

                    Dim pipeline As Func(Of Integer, Models.Manuscript) =
                        Function(index) board.Library.Where(Function(item) item.Location = Models.ManuscriptLocation.Pipeline).ElementAt(index)
                    Dim cardOf As Func(Of Models.Manuscript, Control) =
                        Function(manuscript) {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.
                            SelectMany(Function(shelf) board.Shelf(shelf).Controls.OfType(Of Controls.RoundedPanel)()).
                            Single(Function(card) card.AccessibleName = manuscript.Title)
                    Dim menuItem As Func(Of Models.Manuscript, String, ToolStripItem) =
                        Function(manuscript, text) cardOf(manuscript).ContextMenuStrip.Items.Cast(Of ToolStripItem)().Single(Function(item) item.Text = text)

                    visitAfter("nothing changed", Sub() board.NavigateTo(Form1.WorkspacePage.Board))

                    visitAfter("an edit saved on the manuscript page",
                        Sub()
                            Dim subject As Models.Manuscript = pipeline(2)
                            board.Open(subject)
                            Application.DoEvents()
                            board.Field("Title").Text = subject.Title & " (edited)"
                            Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                            board.NavigateTo(Form1.WorkspacePage.Board)
                        End Sub)

                    visitAfter("a manuscript added",
                        Sub()
                            Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of Forms.AddManuscriptForm)(
                                Sub(dialog)
                                    DirectCast(GetType(Forms.AddManuscriptForm).GetField("txtTitle", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).GetValue(dialog), TextBox).Text = "A study added since"
                                    DirectCast(dialog.AcceptButton, Button).PerformClick()
                                End Sub)
                                board.Add()
                            End Using
                            Assert.AreEqual("A study added since", board.Library.Last().Title)
                        End Sub)

                    visitAfter("a manuscript deleted from its page opened from the Library",
                        Sub()
                            Dim deleted As Models.Manuscript = pipeline(4)
                            board.NavigateTo(Form1.WorkspacePage.Library)
                            Application.DoEvents()
                            Dim grid As DataGridView = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                            grid.CurrentCell = grid.Rows.Cast(Of DataGridViewRow)().Single(Function(row) row.Tag Is deleted).Cells(0)
                            GetType(Control).GetMethod("OnKeyDown", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).
                                Invoke(grid, New Object() {New KeyEventArgs(Keys.Enter)})
                            Application.DoEvents()
                            Assert.AreEqual(deleted.Id, board.OpenManuscriptId, "Enter opens the row's manuscript.")
                            board.ConfirmDelete()
                            Application.DoEvents()
                            Assert.IsFalse(board.Library.Contains(deleted), "The manuscript is deleted.")
                            Assert.AreEqual(Form1.WorkspacePage.Library.ToString(), board.PageName, "The delete goes back to the Library.")
                        End Sub)

                    visitAfter("an import",
                        Sub()
                            board.Library.Add(New Models.Manuscript With {.Title = "An imported study", .CurrentStage = Models.PaperStage.Published, .Location = Models.ManuscriptLocation.Published, .TargetJournal = "Imported Journal"})
                            board.Library.Add(New Models.Manuscript With {.Title = "Another imported study", .Tags = New List(Of String) From {"imported"}})
                            Assert.IsTrue(board.SaveNow(), "The import saved.")
                            board.Render()
                        End Sub)

                    Dim filed As Models.Manuscript = pipeline(6)
                    visitAfter("a move to the File Drawer",
                        Sub()
                            Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of Forms.MoveToFileDrawerForm)(
                                Sub(dialog) DirectCast(dialog.AcceptButton, Button).PerformClick())
                                menuItem(filed, "Move to File Drawer...").PerformClick()
                            End Using
                            Assert.AreEqual(Models.ManuscriptLocation.FileDrawer, filed.Location)
                        End Sub)

                    visitAfter("a move back from the File Drawer",
                        Sub()
                            board.ShelfTab("tabFileDrawer").Checked = True
                            Application.DoEvents()
                            menuItem(filed, "Restore to Pipeline").PerformClick()
                            board.ShelfTab("tabPipeline").Checked = True
                            Assert.AreEqual(Models.ManuscriptLocation.Pipeline, filed.Location)
                        End Sub)

                    visitAfter("a restore's records",
                        Sub()
                            For index As Integer = 0 To board.Library.Count - 1
                                board.Library(index) = ManuscriptCloneService.CloneManuscript(board.Library(index))
                            Next
                            board.Render()
                        End Sub)

                    visitAfter("a change no render followed",
                        Sub()
                            Dim moved As Models.Manuscript = pipeline(1)
                            moved.Location = Models.ManuscriptLocation.Published
                            moved.CurrentStage = Models.PaperStage.Published
                            moved.Tags = New List(Of String) From {"changed", "unrendered"}
                        End Sub)

                    visitAfter("every type cleared",
                        Sub() board.Library.ForEach(Sub(item) item.WorkType = Models.WorkType.Unspecified))

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A visit to the kept Library puts its rows right only as far as the
    ' library changed since they were filled (#133): with nothing changed
    ' it sets no cell and adds or removes no row; after a title changed it
    ' sets that one cell, keeping every row; after a manuscript is added
    ' it fills the rows again, once. Before the Library was kept every
    ' visit built a new grid with new rows, so this failed there.
    <TestMethod>
    Public Sub ALibraryVisitPutsRightOnlyWhatChanged()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Dim grid As DataGridView = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()

                    Dim watchVisit As Func(Of String, String) =
                        Function(change)
                            Dim before As List(Of DataGridViewRow) = grid.Rows.Cast(Of DataGridViewRow)().ToList()
                            Dim cells As New List(Of String)()
                            Dim filled As Integer = 0
                            Dim removed As Integer = 0
                            Dim cellSet As DataGridViewCellEventHandler = Sub(sender, e) cells.Add(grid.Columns(e.ColumnIndex).Name & " of row " & e.RowIndex.ToString())
                            Dim added As DataGridViewRowsAddedEventHandler = Sub(sender, e) filled += 1
                            Dim gone As DataGridViewRowsRemovedEventHandler = Sub(sender, e) removed += 1
                            AddHandler grid.CellValueChanged, cellSet
                            AddHandler grid.RowsAdded, added
                            AddHandler grid.RowsRemoved, gone
                            Try
                                board.NavigateTo(Form1.WorkspacePage.Library)
                                Application.DoEvents()
                            Finally
                                RemoveHandler grid.CellValueChanged, cellSet
                                RemoveHandler grid.RowsAdded, added
                                RemoveHandler grid.RowsRemoved, gone
                            End Try
                            Dim shown As DataGridView = ViewCatalog.Descendants(board.PageView).OfType(Of DataGridView)().Single()
                            Dim kept As Integer = shown.Rows.Cast(Of DataGridViewRow)().Where(Function(row) before.Contains(row)).Count()
                            CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(shown), "After " & change & ", the Library lists the library as it is now.")
                            Dim seen As String =
                                change & ": " & If(shown Is grid, "the same grid", "another grid") & ", " & kept.ToString() & " of " & shown.Rows.Count.ToString() & " rows kept, " &
                                "cells set [" & String.Join(", ", cells) & "], rows added " & filled.ToString() & " times, removed " & removed.ToString() & " times"
                            board.NavigateTo(Form1.WorkspacePage.Board)
                            Application.DoEvents()
                            Return seen
                        End Function

                    report.Add(watchVisit("nothing changed"))

                    Dim renamed As Models.Manuscript = board.Library(7)
                    renamed.Title &= " (renamed)"
                    board.Render()
                    report.Add(watchVisit("a title changed"))

                    board.Library.Add(New Models.Manuscript With {.Title = "A study added since"})
                    board.Render()
                    report.Add(watchVisit("a manuscript added"))

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        CollectionAssert.AreEqual(
            New List(Of String) From {
                "nothing changed: the same grid, 60 of 60 rows kept, cells set [], rows added 0 times, removed 0 times",
                "a title changed: the same grid, 60 of 60 rows kept, cells set [Title of row 7], rows added 0 times, removed 0 times",
                "a manuscript added: the same grid, 0 of 61 rows kept, cells set [], rows added 1 times, removed 1 times"
            },
            report,
            Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' A Windows contrast theme turned on while PaperRoute runs, through the
    ' HighContrastOverride seam (#133): the Library showing as it changes
    ' stays while it shows and is let go as it is left; one prepared and
    ' hidden is let go at once. Each time it is prepared again in the new
    ' colors once the window idles, and that page is the one the next
    ' visit shows, as for Journals and Insights.
    <TestMethod>
    Public Sub TheLibraryIsPreparedAnewAfterTheSystemColorsChange()

        StaRunner.Run(
            Sub()
                Try
                    UiTheme.HighContrastOverride = False
                    Dim library As LargeLibraryFixture.SyntheticLibrary =
                        LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                    Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                        board.Start()
                        board.WaitForIdleWork()
                        Dim first As Control = LibraryPageIn(board)
                        Assert.IsNotNull(first, "The Library was prepared while the window idled.")
                        Assert.AreNotEqual(Drawing.SystemColors.Window.ToArgb(), first.BackColor.ToArgb(), "The first Library is on the board color.")

                        ' Shown as the colors change.
                        board.NavigateTo(Form1.WorkspacePage.Library)
                        Application.DoEvents()
                        Assert.AreSame(first, board.PageView)
                        UiTheme.HighContrastOverride = True
                        board.ChangeSystemColors()
                        Assert.IsFalse(first.IsDisposed, "The Library showing stays while it shows.")
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                        Assert.IsTrue(first.IsDisposed, "The Library that was showing is let go as it is left.")

                        board.WaitForIdleWork()
                        Dim second As Control = board.LibraryPage
                        Assert.IsNotNull(second, "The Library is prepared again.")
                        Assert.IsFalse(second.IsDisposed OrElse second.Visible)
                        Assert.AreEqual(Drawing.SystemColors.Window.ToArgb(), second.BackColor.ToArgb(), "The Library is prepared on the window color.")

                        ' Hidden as the colors change back.
                        UiTheme.HighContrastOverride = False
                        board.ChangeSystemColors()
                        Assert.IsTrue(second.IsDisposed, "The hidden prepared Library is let go at once.")
                        board.WaitForIdleWork()
                        Dim third As Control = board.LibraryPage
                        Assert.IsNotNull(third, "The Library is prepared again.")
                        Assert.AreNotEqual(Drawing.SystemColors.Window.ToArgb(), third.BackColor.ToArgb(), "The Library is prepared on the board color again.")

                        board.NavigateTo(Form1.WorkspacePage.Library)
                        Application.DoEvents()
                        Assert.AreSame(third, board.PageView, "The next visit shows the page prepared again.")
                        CollectionAssert.AreEqual(ExpectedRows(board.Library), RowTexts(ViewCatalog.Descendants(third).OfType(Of DataGridView)().Single()))
                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()

                        board.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' The kept Library, prepared at the window's size, is shown after the
    ' window was made as small as it goes, at each text size (#133): it
    ' fills the page area, nothing in it is left to lay out (laying each
    ' container out again moves nothing), and its short columns fit their
    ' widest text; left scrolled sideways there, as that window lets its
    ' grid be, it shows again from its left edge, as a new page did; and
    ' it fits again once the window is as large as it was.
    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub TheKeptLibraryShowsSettledAtTheSmallestWindow(textSize As Single)

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"), textSize)

                    board.Start()
                    board.WaitForIdleWork()
                    Dim prepared As Control = LibraryPageIn(board)
                    Assert.IsNotNull(prepared, "The Library was prepared while the window idled.")
                    Dim size As Drawing.Size = board.Size

                    For Each windowSize As Drawing.Size In {board.MinimumSize, size}
                        board.Size = windowSize
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.Library)
                        Application.DoEvents()
                        Dim context As String = "At text size " & textSize.ToString(Globalization.CultureInfo.InvariantCulture) & " in a window of " & board.Size.ToString()
                        Assert.AreSame(prepared, board.PageView, context & ": the prepared Library shows.")
                        Assert.AreEqual(board.ContentHost.DisplayRectangle, prepared.Bounds, context & ": it fills the page area.")
                        Dim problems As List(Of String) = LayoutProbe.Unsettled(prepared)
                        Assert.AreEqual(0, problems.Count, context & ", left with layout to do:" & Environment.NewLine & String.Join(Environment.NewLine, problems))
                        Dim grid As DataGridView = ViewCatalog.Descendants(prepared).OfType(Of DataGridView)().Single()
                        AssertColumnsFitAllCells(grid, context)

                        ' The smallest window scrolls the grid sideways; left so,
                        ' it shows again from its left edge.
                        If windowSize = board.MinimumSize Then
                            grid.HorizontalScrollingOffset = 60
                            Assert.IsTrue(grid.HorizontalScrollingOffset > 0, context & ": the grid scrolls sideways.")
                            board.NavigateTo(Form1.WorkspacePage.Board)
                            Application.DoEvents()
                            board.NavigateTo(Form1.WorkspacePage.Library)
                            Application.DoEvents()
                            Assert.AreEqual(0, grid.HorizontalScrollingOffset, context & ": left scrolled sideways, the grid shows again from its left edge.")
                        End If

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        Application.DoEvents()
                    Next

                    board.Close()
                End Using
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


    ' The Library page in the page area, shown or hidden, or Nothing: the
    ' page whose filter is the Library's.
    Private Shared Function LibraryPageIn(board As LibraryBoard) As Control
        Return board.ContentHost.Controls.Cast(Of Control)().SingleOrDefault(
            Function(page) ViewCatalog.Descendants(page).OfType(Of TextBox)().Any(Function(box) box.AccessibleName = "Filter the Library"))
    End Function


    ' What each Library row shows, as text: its title, stage, shelf, target
    ' journal, route, type, tags and days in stage.
    Private Shared Function RowTexts(grid As DataGridView) As List(Of String)
        Return grid.Rows.Cast(Of DataGridViewRow)().
            Select(Function(row) String.Join(" | ", row.Cells.Cast(Of DataGridViewCell)().Select(Function(cell) Convert.ToString(cell.Value, Globalization.CultureInfo.CurrentCulture)))).
            ToList()
    End Function


    ' What a Library page built now lists, worked out here from the records:
    ' every manuscript, in the library's order.
    Private Shared Function ExpectedRows(library As List(Of Models.Manuscript)) As List(Of String)
        Return library.Select(
            Function(manuscript)
                Dim entered As DateTime = manuscript.StageEnteredDate.Date
                Dim days As String =
                    If(manuscript.Location = Models.ManuscriptLocation.Pipeline AndAlso entered > DateTime.MinValue.Date AndAlso entered <= DateTime.Today,
                       (DateTime.Today - entered).Days.ToString(Globalization.CultureInfo.CurrentCulture),
                       String.Empty)
                Dim shelf As String =
                    If(manuscript.Location = Models.ManuscriptLocation.FileDrawer, "File Drawer",
                       If(manuscript.Location = Models.ManuscriptLocation.Published, "Published", "Pipeline"))
                Return String.Join(" | ", {
                    manuscript.Title,
                    WorkflowPresentationService.FormatStage(manuscript.CurrentStage),
                    shelf,
                    manuscript.TargetJournal,
                    RouteSummaryService.Describe(manuscript).Text,
                    If(manuscript.WorkType = Models.WorkType.Unspecified, String.Empty, WorkTypeService.DisplayName(manuscript.WorkType)),
                    String.Join(", ", manuscript.Tags),
                    days})
            End Function).ToList()
    End Function


    ' What a visit to the Library shows of what a researcher can change on
    ' a visit: its view, its filter, its sort, the row current and the rows
    ' selected, where the grid is scrolled to, its columns' widths and fill
    ' weights, and where the keyboard is.
    Private Shared Function DescribeLibraryVisit(board As LibraryBoard) As List(Of String)

        Dim page As Control = board.PageView
        Dim grid As DataGridView = ViewCatalog.Descendants(page).OfType(Of DataGridView)().Single()
        Dim filter As TextBox = ViewCatalog.Descendants(page).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter the Library")
        Dim views As IEnumerable(Of String) = ViewCatalog.Descendants(page).OfType(Of RadioButton)().Where(Function(tab) tab.Checked).Select(Function(tab) tab.Text)
        Dim glyphs As IEnumerable(Of String) =
            grid.Columns.Cast(Of DataGridViewColumn)().Where(Function(column) column.HeaderCell.SortGlyphDirection <> SortOrder.None).
                Select(Function(column) column.Name & " " & column.HeaderCell.SortGlyphDirection.ToString())
        Dim columns As IEnumerable(Of String) =
            grid.Columns.Cast(Of DataGridViewColumn)().
                Select(Function(column) column.Name & " " & column.Width.ToString() & " (" & column.FillWeight.ToString(Globalization.CultureInfo.InvariantCulture) & ")")
        Dim keyboard As Control = DeepestActiveControl(board)

        Return New List(Of String) From {
            "view: " & String.Join(", ", views),
            "filter: '" & filter.Text & "'",
            "sorted: " & If(grid.SortedColumn?.Name, "none") & " " & grid.SortOrder.ToString() & ", glyphs: " & String.Join(", ", glyphs),
            "current: " & If(grid.CurrentCell Is Nothing, "none", "row " & grid.CurrentCell.RowIndex.ToString()) & ", selected rows: " & grid.SelectedRows.Count.ToString(),
            "top row: " & grid.FirstDisplayedScrollingRowIndex.ToString() & ", offset: " & grid.HorizontalScrollingOffset.ToString(),
            "columns: " & String.Join(", ", columns),
            "keyboard: " & If(keyboard Is Nothing, "nowhere", keyboard.GetType().Name & " '" & keyboard.AccessibleName & "'")
        }

    End Function

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
    Private _unread As String = Nothing

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


    ' How many times a message was sent to a control's window. A message
    ' the hook could not read fails the count rather than leaving it short.
    Public Function Count(control As Control, message As Integer) As Integer
        If _unread IsNot Nothing Then Throw New InvalidOperationException("A message sent was not read: " & _unread)
        Dim found As Integer = 0
        If control.IsHandleCreated Then _counts.TryGetValue((control.Handle, message), found)
        Return found
    End Function


    ' Nothing thrown here crosses into the native callback.
    Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If code >= 0 Then
            Try
                Dim sent As CWPSTRUCT = Marshal.PtrToStructure(Of CWPSTRUCT)(lParam)
                Dim key As (IntPtr, Integer) = (sent.hwnd, sent.message)
                Dim found As Integer = 0
                _counts.TryGetValue(key, found)
                _counts(key) = found + 1
            Catch ex As Exception
                If _unread Is Nothing Then _unread = ex.GetType().Name & ": " & ex.Message
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
