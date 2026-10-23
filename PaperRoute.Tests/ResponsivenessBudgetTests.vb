Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Responsiveness budgets (#131): every named action under a Stopwatch, on
' the example library and on the synthetic 500-manuscript library, against
' the budgets the issue proposes. The table is written to the test output
' and each row is marked within or over its budget; a row fails the test
' only when it is enforced, which #133 did for each action it fixed with
' room to spare on a busy host. The rest stay report-only.
'
' Wall is the Stopwatch around the action. UI is the longest stretch in
' which the window processed no messages during the action: a probe timer
' ticks on the UI thread, and the longest gap between its ticks is the
' longest stall. For an action that runs on the UI thread from start to end
' the two are the same. The verdict compares UI with the budget.
'
' The example library runs first, so it also warms the JIT for the 500
' run; the first measurement of each kind in a process includes JIT time.
<TestClass>
<DoNotParallelize>
Public Class ResponsivenessBudgetTests

    Public Property TestContext As TestContext

    ' A Debug build runs without the optimizer; the CI runner is shared and
    ' slower than a desktop. Each multiplies every budget.
    Friend Const DebugMultiplier As Double = 2
    Friend Const CiMultiplier As Double = 3

    ' The budgets, in milliseconds (#131).
    Friend Shared ReadOnly PageSwitch As New Budget("page switch", 100)
    Friend Shared ReadOnly FilterKeystroke As New Budget("filter keystroke", 50)
    Friend Shared ReadOnly DirtyTick As New Budget("dirty-timer tick", 5)
    Friend Shared ReadOnly Save As New Budget("save", 250)
    Friend Shared ReadOnly Startup As New Budget("warm startup", 1500)
    ' Nothing else holds the UI thread longer than this without its work
    ' moving to a background task behind a disabled window and a status line.
    Friend Shared ReadOnly UiThread As New Budget("UI thread", 250)
    ' The idle queue's slice: the work a window does between messages
    ' while it idles is cut into steps about this long (#133).
    Friend Shared ReadOnly IdleSlice As New Budget("idle slice", 30)

    Private _root As String = String.Empty
    Private ReadOnly _paintObserved As New Dictionary(Of String, Boolean)()


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        OnlineAccess.ResetForTests()
        Dim keys As New ProtectedKeyStore(Path.Combine(_root, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub EveryActionIsMeasuredOnBothLibraries()

        Dim rows As New List(Of Row)()
        Dim libraries As New List(Of LargeLibraryFixture.SyntheticLibrary)()
        Dim dpi As Integer = 0

        RunOnStaThread(
            Sub()
                Dim example = LargeLibraryFixture.WriteExample(Path.Combine(_root, "example"))
                Dim large = LargeLibraryFixture.Create(Path.Combine(_root, "large"), 500)
                libraries.Add(example)
                libraries.Add(large)
                ' The example library runs first and carries the process's
                ' first-time costs (JIT, the first text change), so its rows
                ' stay report-only; the budgets are held on the 500 library.
                dpi = MeasureLibrary(example, rows, enforce:=False)
                MeasureLibrary(large, rows, enforce:=True)
            End Sub)

        Report(libraries, rows, dpi)

        Dim multiplier As Double = BudgetMultiplier()
        Dim failures As List(Of String) =
            rows.Where(Function(row) row.Enforced AndAlso Not row.Within(multiplier)).
                 Select(Function(row) row.Library & ": " & row.Action & " took " & Ms(row.Ui) & " ms against " & Ms(row.Budget.Milliseconds * multiplier) & " ms").
                 ToList()

        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' Every measured action, in the order a researcher would meet them, with
    ' the budgets #133 holds enforced when asked. Returns the window's DPI
    ' for the report.
    Private Function MeasureLibrary(
        library As LargeLibraryFixture.SyntheticLibrary,
        rows As List(Of Row),
        enforce As Boolean
    ) As Integer

        Dim subject As Manuscript =
            library.Manuscripts.Where(Function(item) item.SubmissionPackets.Count > 0).
                OrderByDescending(Function(item) item.Submissions.Count).
                FirstOrDefault()

        If subject Is Nothing Then subject = library.Manuscripts.First()

        Dim title As String = subject.Title
        Dim keywords As List(Of String) = subject.Metadata.Keywords.ToList()
        Dim backupPath As String = Path.Combine(_root, library.Name & "-backup.zip")

        Using board As New LibraryBoard(library, Path.Combine(_root, library.Name & "-settings"))

            rows.Add(Time(library, "Warm startup to an interactive Board (Show, queue drained)", Startup, Sub() board.Start(), enforced:=enforce))
            _paintObserved(library.Name) = board.PaintObserved

            ' The shelf shown fills in after its first screen, a card at a
            ' time while the window idles; a shelf not shown yet is built when
            ' it is first shown (#133). The first switch is reported: it builds
            ' the shelf's cards where startup no longer does.
            '
            ' Beside the fill, the manuscript page, Journals and Insights are
            ' prepared while the window idles (#133): each built, laid out, and
            ' its windows made a slice at a time. The fill's row holds the
            ' longest step while the fill runs against the UI thread's budget,
            ' and the next row the longest step of the idle work left after
            ' it; their twin, reported, holds the longer of the two against
            ' the idle queue's own slice of 30 ms.
            Dim fill As Row = Time(library, "Board: the rest of the shown shelf, shown in idle slices", UiThread, Sub() board.WaitForBoardFill(), enforced:=enforce)
            rows.Add(fill)
            Dim prepared As Row = Time(library, "Idle work after the fill: the pages' preparation, its longest step", UiThread, Sub() board.WaitForIdleWork(), enforced:=enforce)
            rows.Add(prepared)
            rows.Add(New Row With {.Library = library.Name, .Action = "Idle work from startup: its longest step, against the idle slice", .Wall = fill.Wall + prepared.Wall, .Ui = Math.Max(fill.Ui, prepared.Ui), .Budget = IdleSlice})
            Assert.IsNotNull(board.JournalsPage, "Journals was prepared while the window idled.")
            Assert.IsNotNull(board.InsightsPage, "Insights was prepared while the window idled.")
            rows.Add(Time(library, "First switch to Published (the shelf built when first shown)", PageSwitch, Sub() board.ShelfTab("tabPublished").Checked = True))
            board.WaitForBoardFill()
            board.ShelfTab("tabPipeline").Checked = True
            board.WaitForBoardFill()

            ' #133 enforces the Board's startup and the page switches back to it
            ' from Insights and the manuscript page. Board to Library stays
            ' reported: it builds its page's windows and measured 54-163 ms
            ' across this host's quiet and busy runs, with no room under the 100
            ' ms line. Library to Board, which disposes the
            ' Library page and its 500-row grid, read 27 to 100 ms in most runs
            ' and 128 to 481 ms in others: the spikes were a wait of about half
            ' a second inside painting Windows did synchronously as the Board
            ' came forward (PageSwitchTests.SwitchesPaintNothingSynchronously
            ' records that painting). Since 2026-10-23 a switch paints nothing
            ' until it has returned; the row read 12 to 50 ms in 60 trials in
            ' a row on a Release build, and it is enforced.
            rows.Add(Time(library, "Board to Library", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Library)))
            rows.Add(Time(library, "One Library filter keystroke", FilterKeystroke, Sub() board.TypeInLibraryFilter("a"), enforced:=enforce))
            rows.Add(Time(library, "Library filter: refill once typing pauses", UiThread, Sub() board.PauseTyping(), enforced:=enforce))
            rows.Add(Time(library, "Library to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))

            ' The Journals page on the test's author library, through the
            ' window's own navigation (CreateJournalLibraryEditor). Journals and
            ' Insights are kept and prepared while the window idles (#133);
            ' each visit reads the author library afresh, or works its figures
            ' out anew. Built at each visit, Board to Journals read 80 to 312 ms
            ' and Board to Insights 62 to 210; on the prepared pages, 13 to 54
            ' and 10 to 47 ms in six Release runs on 2026-10-23 (second visits
            ' 13 to 32 and 20 to 56), and both are enforced, first and second
            ' visits. Journals to Board stays reported: leaving reads the
            ' author library its editor saved and renders the Board again, as
            ' it did after the Journal Library dialog (49 to 161 ms).
            rows.Add(Time(library, "Board to Journals (the page prepared while the window idled)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Journals), enforced:=enforce))
            rows.Add(Time(library, "Journals to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))
            rows.Add(Time(library, "Board to Journals (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Journals), enforced:=enforce))
            rows.Add(Time(library, "Journals to Board (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            ' The Deadlines page draws its rows in one list and is kept (#133).
            ' Outside the timed steps, the list is checked to hold every open
            ' item, so a row cannot pass by showing fewer; the filter "a"
            ' matches every item on the 500 library, so the keystroke refills
            ' all of them. Painting is timed as a row of its own, since the
            ' other rows do not pump messages. These rows and Import & Export's
            ' are enforced, as Board to Journals is, because #133 requires it;
            ' the first visits read 39 to 53 ms on a quiet host, but a busy one
            ' can push them past 100 ms (or 200 in Debug), so a failure here is
            ' run again on a quiet host before it is read as a regression.
            Dim openItems As Integer = OpenDeadlines(board, String.Empty)
            If enforce Then Assert.IsTrue(openItems >= 200, "The 500 library has " & openItems & " open deadline items.")
            rows.Add(Time(library, "Board to Deadlines", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines), enforced:=enforce))
            Assert.AreEqual(openItems, ShownDeadlines(board).Rows.Count, "The list holds every open item after the visit.")
            rows.Add(Time(library, "Deadlines: one filter keystroke", UiThread, Sub() board.TypeInDeadlinesFilter("a"), enforced:=enforce))
            Assert.AreEqual(OpenDeadlines(board, "a"), ShownDeadlines(board).Rows.Count, "The list holds every matching item after the keystroke.")
            If enforce Then Assert.AreEqual(openItems, ShownDeadlines(board).Rows.Count, "The filter ""a"" matches every item on the 500 library.")
            Dim shown As DeadlineList = ShownDeadlines(board)
            Using picture As New Bitmap(Math.Max(1, shown.Width), Math.Max(1, shown.Height))
                rows.Add(Time(library, "Deadlines: paint the visible rows (DrawToBitmap)", UiThread,
                              Sub() shown.DrawToBitmap(picture, New Rectangle(Point.Empty, shown.Size)), enforced:=enforce))
            End Using
            rows.Add(Time(library, "Deadlines: refill after a change (RenderManuscripts while Deadlines is open)", UiThread, Sub() board.Render(), enforced:=enforce))
            rows.Add(Time(library, "Deadlines to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))
            rows.Add(Time(library, "Board to Deadlines (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines), enforced:=enforce))
            Assert.AreEqual(openItems, ShownDeadlines(board).Rows.Count, "The list holds every open item after the second visit.")
            rows.Add(Time(library, "Deadlines to Board (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))

            rows.Add(Time(library, "Board to Insights (the page prepared while the window idled)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Insights), enforced:=enforce))
            rows.Add(Time(library, "Insights: figures ready (calculated off the UI thread)", UiThread, Sub() board.WaitForInsights()))
            rows.Add(Time(library, "Insights to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))
            rows.Add(Time(library, "Board to Insights (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Insights), enforced:=enforce))
            rows.Add(Time(library, "Insights: figures ready (second visit)", UiThread, Sub() board.WaitForInsights()))
            rows.Add(Time(library, "Insights to Board (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))

            ' Import & Export's commands are laid out and drawn by one list
            ' around its buttons, and the page is kept (#133).
            rows.Add(Time(library, "Board to Import & Export", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport), enforced:=enforce))
            rows.Add(Time(library, "Import & Export to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))
            rows.Add(Time(library, "Board to Import & Export (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport), enforced:=enforce))
            rows.Add(Time(library, "Import & Export to Board (second visit)", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))

            ' The manuscript page is prepared while the window idles after
            ' startup and kept: each manuscript opened loads into it, and it
            ' shows in one layout pass, its versions and the selected
            ' submission's details following once it has painted. Leaving hides
            ' it; its editor lets go of the record while the window idles
            ' (#133). Leaving is enforced, and so is letting go of the record,
            ' which follows it about 15 ms after the Board shows and holds the
            ' window as one step: the leave row alone is not what the researcher
            ' waits. The first open is timed on the prepared page; one made
            ' before the preparation ends builds the page there and then, as
            ' every first open did before 2026-10-23.
            ' The step after the first paint is enforced from 2026-10-23: it
            ' no longer forces that paint, which is where this machine waited
            ' on the screen (42 to 72 ms in Release, against 250).
            '
            ' A switch paints nothing until it has returned (#133); what it
            ' changed is painted once the window processes its messages. That
            ' paint is timed in a row of its own, reported, before a row that
            ' processes messages to time its own work: on this host the first
            ' paint after a switch has waited up to half a second with no CPU
            ' used (in 6 of 18 opens of the manuscript page and 1 of 18
            ' returns to the Board, timed on 2026-10-23), the wait the
            ' switches themselves used to take. Each paint takes 10 to 50 ms
            ' otherwise.
            Assert.IsNotNull(board.KeptPage, "The manuscript page was prepared while the window idled.")
            rows.Add(Time(library, "Board to a manuscript page (the first, on the page prepared while the window idled)", PageSwitch, Sub() board.Open(subject)))
            rows.Add(Time(library, "Manuscript page: its first paint (painting what the switch changed)", UiThread, Sub() board.PaintNow()))
            rows.Add(Time(library, "Manuscript page: versions and details after its first paint", UiThread, Sub() Application.DoEvents(), enforced:=enforce))
            rows.Add(TimeMedian(library, "One dirty-timer tick (median of 10)", DirtyTick, 10, Sub() board.Tick()))
            board.Field("Title").Text = title & " (edited)"
            rows.Add(Time(library, "Save from the manuscript page", Save, Sub() Assert.IsTrue(board.SaveManuscriptPage(), "The page saved."), enforced:=enforce))
            rows.Add(Time(library, "Manuscript page to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))
            rows.Add(Time(library, "Board: its paint once the manuscript page is left", UiThread, Sub() board.PaintNow()))
            rows.Add(Time(library, "Manuscript page: the record let go of while the window idles", UiThread, Sub() board.WaitForIdleWork(), enforced:=enforce))

            Dim another As Manuscript =
                If(library.Manuscripts.Where(Function(item) item IsNot subject AndAlso item.SubmissionPackets.Count > 0).
                       OrderByDescending(Function(item) item.Submissions.Count).FirstOrDefault(),
                   library.Manuscripts.First(Function(item) item IsNot subject))
            rows.Add(Time(library, "Board to a manuscript page (the kept page, another manuscript)", PageSwitch, Sub() board.Open(another)))
            Application.DoEvents()
            rows.Add(Time(library, "Kept manuscript page to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board), enforced:=enforce))
            board.WaitForIdleWork()

            ' The open most researchers make most often: a Pipeline manuscript
            ' still choosing its journal, with three journals on its shortlist
            ' and its tags, which the kept page builds rows and chips for. The
            ' manuscript above is a Published one with neither, so this row
            ' is checked to have them, and cannot pass on an easier record.
            Dim pipelineSubject As Manuscript = library.Manuscripts.First(Function(item) item.Location = ManuscriptLocation.Pipeline)
            Dim choosing As Manuscript =
                library.Manuscripts.FirstOrDefault(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso item.JournalShortlist.Count = 3 AndAlso
                                                                  item.Tags.Count >= 2 AndAlso item IsNot subject AndAlso item IsNot another AndAlso item IsNot pipelineSubject)
            If enforce Then Assert.IsNotNull(choosing, "The 500 library has a Pipeline manuscript with three shortlisted journals and two tags or more.")
            If choosing IsNot Nothing Then
                Assert.IsTrue(choosing.JournalShortlist.Count = 3 AndAlso choosing.Tags.Count >= 2)
                rows.Add(Time(library, "Board to a manuscript page (the kept page, a Pipeline manuscript with three shortlisted journals and tags)", PageSwitch, Sub() board.Open(choosing)))
                Application.DoEvents()
                board.NavigateTo(Form1.WorkspacePage.Board)
                board.WaitForIdleWork()
            End If

            rows.Add(Time(library, "Board render (RenderManuscripts)", UiThread, Sub() board.Render(), enforced:=enforce))

            ' A save that changes a card the Board shows with its windows: the
            ' card is replaced on its shelf, the others left in place (#133).
            board.Open(pipelineSubject)
            Application.DoEvents()
            board.Field("Title").Text = pipelineSubject.Title & " (edited)"
            rows.Add(Time(library, "Save of a Pipeline manuscript whose card changes", Save, Sub() Assert.IsTrue(board.SaveManuscriptPage(), "The page saved."), enforced:=enforce))
            board.NavigateTo(Form1.WorkspacePage.Board)
            Application.DoEvents()

            ' The handlers' work after their dialogs, behind the disabled window
            ' (#132). Backup's stall varied from 61 to 419 ms across runs on
            ' this busy host. The restore's replace step, whose re-render #133
            ' cut from 13 s, varied from 50 to 331 ms; since #116 a restore
            ' ends in a restart rather than a reload, so the step is the swap
            ' and the notice, the restart recorded. Both pump for 30 to 60 s,
            ' long enough to meet one of the host's own stalls, so both are
            ' reported, not enforced.
            rows.Add(Time(library, "Backup", UiThread, Sub() Assert.IsTrue(board.WriteBackup(backupPath), "The backup was written.")))
            rows.Add(Time(library, "Restore: inspect the backup", UiThread, Sub() Assert.IsNotNull(board.InspectBackupFile(backupPath), "The backup was read."), enforced:=enforce))
            rows.Add(Time(library, "Restore: replace the library (the window then restarts)", UiThread,
                          Sub()
                              board.Restore(backupPath)
                              Assert.AreEqual(1, board.RestoreNotices.Count, "The library was restored.")
                          End Sub))

            ' Each form is created inside its timed step and closed even when a
            ' step fails, so no window outlives the test.
            Dim suggestions As JournalSuggestionsForm = Nothing
            Dim recorded As JournalSuggestionsResult = RecordedSuggestions()
            Try
                rows.Add(Time(library, "Find Journals: click to first paint", UiThread,
                              Sub()
                                  suggestions = New JournalSuggestionsForm(title, keywords, New JournalSuggestionsTests.RecordedSuggestions(recorded), Nothing, Nothing, LargeLibraryFixture.Today)
                                  ShowOffscreen(suggestions)
                              End Sub))
                rows.Add(Time(library, "Find Journals: results rendered (fixture responses)", UiThread, Sub() Pump(suggestions.SearchAsync())))
            Finally
                suggestions?.Close()
                suggestions?.Dispose()
            End Try

            Dim facts As JournalFactsForm = Nothing
            Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
            Try
                rows.Add(Time(library, "Look Up Journal Facts: click to first paint", UiThread,
                              Sub()
                                  facts = New JournalFactsForm(record, New JournalFactsTests.RecordedFacts(JournalFactsTests.PlosLookup()))
                                  ShowOffscreen(facts)
                              End Sub))
                rows.Add(Time(library, "Look Up Journal Facts: results rendered (fixture responses)", UiThread, Sub() Pump(facts.LookUpAsync())))
            Finally
                facts?.Close()
                facts?.Dispose()
            End Try

            Dim dpi As Integer = board.DeviceDpi
            board.Close()
            Return dpi

        End Using

    End Function


    ' The open deadline items the page lists for a filter text, worked out
    ' as the page works them out, from the window's own records.
    Private Shared Function OpenDeadlines(board As LibraryBoard, filter As String) As Integer
        Return DeadlineService.Build(board.Library, DateTime.Today).
            Where(Function(item) item.Group <> DeadlineGroup.Done AndAlso
                                 (filter.Length = 0 OrElse
                                  {item.ManuscriptTitle, item.JournalName, item.Title}.Any(
                                      Function(value) If(value, String.Empty).IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0))).
            Count()
    End Function


    ' The Deadlines page's list, while the page is shown.
    Private Shared Function ShownDeadlines(board As LibraryBoard) As DeadlineList
        Return ViewCatalog.Descendants(board).OfType(Of DeadlineList)().Single(Function(list) list.Visible)
    End Function


    ' ---------------------------------------------------------------
    ' Timing
    ' ---------------------------------------------------------------

    Friend NotInheritable Class Budget

        Public ReadOnly Name As String
        Public ReadOnly Milliseconds As Double

        Public Sub New(name As String, milliseconds As Double)
            Me.Name = name
            Me.Milliseconds = milliseconds
        End Sub

    End Class


    Private NotInheritable Class Row

        Public Property Library As String
        Public Property Action As String
        Public Property Wall As Double
        Public Property Ui As Double
        Public Property Budget As Budget

        ' Set for an action #133 fixed; a report-only row never fails the test.
        Public Property Enforced As Boolean

        Public Function Within(multiplier As Double) As Boolean
            Return Ui <= Budget.Milliseconds * multiplier
        End Function

    End Class


    Private Function Time(
        library As LargeLibraryFixture.SyntheticLibrary,
        action As String,
        budget As Budget,
        work As Action,
        Optional enforced As Boolean = False
    ) As Row

        Using probe As New UiThreadProbe()
            Dim watch As Stopwatch = Stopwatch.StartNew()
            work()
            watch.Stop()
            Dim wall As Double = watch.Elapsed.TotalMilliseconds
            Return New Row With {.Library = library.Name, .Action = action, .Wall = wall, .Ui = Math.Min(wall, probe.Finish()), .Budget = budget, .Enforced = enforced}
        End Using

    End Function


    ' An action that repeats on its own (the dirty timer): the median of
    ' several runs, so one JIT or garbage-collection pause does not stand
    ' for the steady state.
    Private Function TimeMedian(
        library As LargeLibraryFixture.SyntheticLibrary,
        action As String,
        budget As Budget,
        count As Integer,
        work As Action
    ) As Row

        Dim samples As New List(Of Double)()

        For index As Integer = 1 To count
            Dim watch As Stopwatch = Stopwatch.StartNew()
            work()
            watch.Stop()
            samples.Add(watch.Elapsed.TotalMilliseconds)
        Next

        samples.Sort()
        Dim median As Double = samples(samples.Count \ 2)
        Return New Row With {.Library = library.Name, .Action = action, .Wall = median, .Ui = median, .Budget = budget}

    End Function


    ' Ticks on the UI thread while an action runs. The longest gap between
    ' ticks is the longest time the window went without processing messages:
    ' the whole action when it blocks, a few milliseconds when it pumps.
    Private NotInheritable Class UiThreadProbe
        Implements IDisposable

        Private ReadOnly _timer As New System.Windows.Forms.Timer With {.Interval = 1}
        Private ReadOnly _watch As Stopwatch = Stopwatch.StartNew()
        Private _lastTick As Double = 0
        Private _longestGap As Double = 0

        Public Sub New()
            AddHandler _timer.Tick,
                Sub(sender, e)
                    Dim now As Double = _watch.Elapsed.TotalMilliseconds
                    _longestGap = Math.Max(_longestGap, now - _lastTick)
                    _lastTick = now
                End Sub
            _timer.Start()
        End Sub

        Public Function Finish() As Double
            _timer.Stop()
            Return Math.Max(_longestGap, _watch.Elapsed.TotalMilliseconds - _lastTick)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Dispose()
        End Sub

    End Class


    ' Waits for a task the way the window does: by processing messages.
    Private Shared Sub Pump(task As Task)
        While Not task.IsCompleted
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        task.GetAwaiter().GetResult()
    End Sub


    Friend Shared Function BudgetMultiplier() As Double
        Dim multiplier As Double = 1
#If DEBUG Then
        multiplier *= DebugMultiplier
#End If
        If String.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) Then
            multiplier *= CiMultiplier
        End If
        Return multiplier
    End Function


    ' ---------------------------------------------------------------
    ' Report
    ' ---------------------------------------------------------------

    Private Sub Report(
        libraries As List(Of LargeLibraryFixture.SyntheticLibrary),
        rows As List(Of Row),
        dpi As Integer
    )

        Dim multiplier As Double = BudgetMultiplier()
        Dim text As New StringBuilder()

        text.AppendLine("Responsiveness budgets (#131). Verdict: the longest UI-thread stall against the budget x " &
                        multiplier.ToString("0.#", CultureInfo.InvariantCulture) & ".")
        text.AppendLine("Host: " & Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) & " logical processors, " &
                        Environment.OSVersion.VersionString & ", " & If(IsDebugBuild(), "Debug", "Release") & " build, window DPI " &
                        dpi.ToString(CultureInfo.InvariantCulture) & ".")

        For Each library In libraries
            Dim painted As Boolean = False
            _paintObserved.TryGetValue(library.Name, painted)
            text.AppendLine("Library """ & library.Name & """: " & library.Manuscripts.Count.ToString(CultureInfo.InvariantCulture) &
                            " manuscripts, " & library.ManagedFolderCount.ToString(CultureInfo.InvariantCulture) & " managed folders; " &
                            If(painted, "a Paint arrived during startup.", "no Paint arrived during startup (the window is off-screen)."))
        Next

        text.AppendLine()
        text.AppendLine("| Library | Action | Wall ms | UI ms | Budget ms | Verdict |")
        text.AppendLine("|---|---|---:|---:|---:|---|")

        For Each row In rows
            text.AppendLine("| " & row.Library & " | " & row.Action & " | " & Ms(row.Wall) & " | " & Ms(row.Ui) & " | " &
                            Ms(row.Budget.Milliseconds) & " (" & row.Budget.Name & ") | " &
                            If(row.Within(multiplier), "within", "OVER") & If(row.Enforced, ", enforced", String.Empty) & " |")
        Next

        TestContext.WriteLine(text.ToString())
        Console.WriteLine(text.ToString())

    End Sub


    Private Shared Function Ms(value As Double) As String
        Return value.ToString("0", CultureInfo.InvariantCulture)
    End Function


    Private Shared Function IsDebugBuild() As Boolean
#If DEBUG Then
        Return True
#Else
        Return False
#End If
    End Function


    ' The recorded OpenAlex answers, read through the gate with this test's
    ' keys, so no key store outside the test folder is read.
    Private Shared Function RecordedSuggestions() As JournalSuggestionsResult
        OnlineAccess.InnerHandlerFactory = Function() New JournalSuggestionsTests.FixtureNetwork With {.Respond = AddressOf JournalSuggestionsTests.Recorded}
        Try
            Return New OnlineJournalSuggestionsSource().SearchAsync(JournalSuggestionsTests.AnchoringRequest(), CancellationToken.None).GetAwaiter().GetResult()
        Finally
            OnlineAccess.InnerHandlerFactory = Nothing
        End Try
    End Function


    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub


    ' The shared runner (#134), given the time the 500 library's backup and
    ' restore take on a slow host.
    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action, TimeSpan.FromMinutes(10))
    End Sub

End Class
