Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Responsiveness budgets (#131): every named action under a Stopwatch, on
' the example library and on the synthetic 500-manuscript library, against
' the budgets the issue proposes. Report-only: the table is written to the
' test output and each row is marked within or over its budget; a budget
' fails the test only once it is marked Enforced, which #133 does for each
' action it fixes.
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
                dpi = MeasureLibrary(example, rows)
                MeasureLibrary(large, rows)
            End Sub)

        Report(libraries, rows, dpi)

        Dim multiplier As Double = BudgetMultiplier()
        Dim failures As List(Of String) =
            rows.Where(Function(row) row.Budget.Enforced AndAlso Not row.Within(multiplier)).
                 Select(Function(row) row.Library & ": " & row.Action & " took " & Ms(row.Ui) & " ms against " & Ms(row.Budget.Milliseconds * multiplier) & " ms").
                 ToList()

        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' Every measured action, in the order a researcher would meet them.
    ' Returns the window's DPI for the report.
    Private Function MeasureLibrary(
        library As LargeLibraryFixture.SyntheticLibrary,
        rows As List(Of Row)
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

            rows.Add(Time(library, "Warm startup to an interactive Board (Show, queue drained)", Startup, Sub() board.Start()))
            _paintObserved(library.Name) = board.PaintObserved

            rows.Add(Time(library, "Board to Library", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Library)))
            rows.Add(Time(library, "One Library filter keystroke", FilterKeystroke, Sub() board.TypeInLibraryFilter("a")))
            rows.Add(Time(library, "Library to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            ' ShowPage builds the Journals page from the user's author library;
            ' here it is built the same way from the test's, since no seam
            ' lets a test supply one.
            rows.Add(Time(library, "Board to Journals (built as ShowPage builds it, from the test's author library)", PageSwitch, Sub() board.ShowJournals()))
            rows.Add(Time(library, "Journals to Board", PageSwitch, Sub() board.LeaveJournals()))

            rows.Add(Time(library, "Board to Deadlines", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Deadlines)))
            rows.Add(Time(library, "Deadlines to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            rows.Add(Time(library, "Board to Insights", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Insights)))
            rows.Add(Time(library, "Insights to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            rows.Add(Time(library, "Board to Import & Export", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.ImportExport)))
            rows.Add(Time(library, "Import & Export to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            rows.Add(Time(library, "Board to a manuscript page", PageSwitch, Sub() board.Open(subject)))
            rows.Add(TimeMedian(library, "One dirty-timer tick (median of 10)", DirtyTick, 10, Sub() board.Tick()))
            board.Field("Title").Text = title & " (edited)"
            rows.Add(Time(library, "Save from the manuscript page", Save, Sub() Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")))
            rows.Add(Time(library, "Manuscript page to Board", PageSwitch, Sub() board.NavigateTo(Form1.WorkspacePage.Board)))

            rows.Add(Time(library, "Board render (RenderManuscripts)", UiThread, Sub() board.Render()))

            ' The handler calls the service after Save and the file dialog.
            rows.Add(Time(library, "Backup (PortableBackupService.CreateBackup)", UiThread,
                          Sub() Call New PortableBackupService(library.ManagedDirectory).CreateBackup(backupPath, board.Library, library.Repository)))

            ' The handler inspects, asks twice, then restores and reloads.
            rows.Add(Time(library, "Restore: inspect the backup", UiThread,
                          Sub() Call New PortableRestoreService(library.ManagedDirectory).InspectBackup(backupPath)))
            rows.Add(Time(library, "Restore: replace the library, reload, render", UiThread,
                          Sub()
                              Call New PortableRestoreService(library.ManagedDirectory).RestoreBackup(backupPath, board.Library, library.Repository)
                              board.ReloadLibrary()
                          End Sub))

            Dim suggestions As JournalSuggestionsForm = Nothing
            Dim recorded As JournalSuggestionsResult = RecordedSuggestions()
            rows.Add(Time(library, "Find Journals: click to first paint", UiThread,
                          Sub()
                              suggestions = New JournalSuggestionsForm(title, keywords, New JournalSuggestionsTests.RecordedSuggestions(recorded), Nothing, Nothing, LargeLibraryFixture.Today)
                              ShowOffscreen(suggestions)
                          End Sub))
            rows.Add(Time(library, "Find Journals: results rendered (fixture responses)", UiThread, Sub() Pump(suggestions.SearchAsync())))
            suggestions.Close()
            suggestions.Dispose()

            Dim facts As JournalFactsForm = Nothing
            Dim record As New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}
            rows.Add(Time(library, "Look Up Journal Facts: click to first paint", UiThread,
                          Sub()
                              facts = New JournalFactsForm(record, New JournalFactsTests.RecordedFacts(JournalFactsTests.PlosLookup()))
                              ShowOffscreen(facts)
                          End Sub))
            rows.Add(Time(library, "Look Up Journal Facts: results rendered (fixture responses)", UiThread, Sub() Pump(facts.LookUpAsync())))
            facts.Close()
            facts.Dispose()

            Dim dpi As Integer = board.DeviceDpi
            board.Close()
            Return dpi

        End Using

    End Function


    ' ---------------------------------------------------------------
    ' Timing
    ' ---------------------------------------------------------------

    Friend NotInheritable Class Budget

        Public ReadOnly Name As String
        Public ReadOnly Milliseconds As Double

        ' Set by #133 for each action it fixes; a report-only budget never fails.
        Public ReadOnly Enforced As Boolean

        Public Sub New(name As String, milliseconds As Double, Optional enforced As Boolean = False)
            Me.Name = name
            Me.Milliseconds = milliseconds
            Me.Enforced = enforced
        End Sub

    End Class


    Private NotInheritable Class Row

        Public Property Library As String
        Public Property Action As String
        Public Property Wall As Double
        Public Property Ui As Double
        Public Property Budget As Budget

        Public Function Within(multiplier As Double) As Boolean
            Return Ui <= Budget.Milliseconds * multiplier
        End Function

    End Class


    Private Function Time(
        library As LargeLibraryFixture.SyntheticLibrary,
        action As String,
        budget As Budget,
        work As Action
    ) As Row

        Using probe As New UiThreadProbe()
            Dim watch As Stopwatch = Stopwatch.StartNew()
            work()
            watch.Stop()
            Dim wall As Double = watch.Elapsed.TotalMilliseconds
            Return New Row With {.Library = library.Name, .Action = action, .Wall = wall, .Ui = Math.Min(wall, probe.Finish()), .Budget = budget}
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
                            If(row.Within(multiplier), "within", "OVER") & If(row.Budget.Enforced, ", enforced", String.Empty) & " |")
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


    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        ' The 500 library's backup and restore take their time on a slow host.
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(10)), "Responsiveness budget test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
