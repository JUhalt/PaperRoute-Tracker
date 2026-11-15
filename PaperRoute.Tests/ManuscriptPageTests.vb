Imports System.Drawing
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Velopack

' The manuscript page (#55): one working copy per open manuscript, saved or
' discarded explicitly, and never lost silently when leaving.
<TestClass>
<DoNotParallelize>
Public Class ManuscriptPageTests

    <TestMethod>
    Public Sub OpensAsAPageAndGoesBackToWhereItWasOpened()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First manuscript")
                    board.Prepare(first)

                    board.Open(first)
                    Assert.AreEqual("Manuscript", board.PageName)
                    Assert.IsTrue(board.RailPage("Board").Checked, "The rail keeps showing the page the manuscript was opened from.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Opening a manuscript changes nothing.")
                    Assert.IsFalse(Descendants(board.Editor).OfType(Of Button)().Any(Function(button) button.Text.Contains("Save") AndAlso button.Text.Contains("Close")),
                        "The page has no Save & Close footer.")
                    CollectionAssert.AreEqual(
                        {"Overview", "Authors", "Versions", "Submissions", "Readiness & Packets"},
                        Descendants(board.Editor).OfType(Of RadioButton)().Select(Function(tab) tab.Text).ToList())

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual(0, board.SaveCount, "Viewing never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub SaveKeepsEditsAndDiscardReturnsToTheSavedVersion()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    board.Field("Title").Text = "Saved title"
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual("Original title", manuscript.Title, "Edits stay in the working copy until saved.")
                    board.Tick()
                    Assert.IsTrue(board.SaveBarVisible, "The Unsaved changes bar appears.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual("Saved title", manuscript.Title)
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Assert.IsFalse(board.SaveBarVisible)

                    board.Field("Title").Text = "Discarded title"
                    board.Discard()
                    Assert.AreEqual("Saved title", manuscript.Title, "Discard leaves the library unchanged.")
                    Assert.AreEqual("Saved title", board.Field("Title").Text, "The page shows the saved version again.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(1, board.SaveCount, "Discard never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub LeavingWithUnsavedChangesAsksToSaveDiscardOrStay()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual(1, asked)
                    Assert.AreEqual("Manuscript", board.PageName, "Cancel stays on the page.")
                    Assert.IsTrue(board.RailPage("Board").Checked, "The rail returns to the page's origin.")
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "Staying keeps the edits.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual("Original title", manuscript.Title, "No discards the edits.")
                    Assert.AreEqual(0, board.SaveCount)

                    board.Open(manuscript)
                    board.Field("Title").Text = "Kept title"
                    board.SetPrompt(Function(title) DialogResult.Yes)
                    board.PressCommandKey(Keys.Control Or Keys.D6)
                    Assert.AreEqual("ImportExport", board.PageName)
                    Assert.AreEqual("Kept title", manuscript.Title, "Yes saves before leaving.")
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub OpeningAnotherManuscriptProtectsTheOneBeingEdited()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First")
                    Dim second As Manuscript = Sample("Second")
                    board.Prepare(first, second)
                    board.Open(first)
                    board.Field("Title").Text = "First, edited"

                    board.SetPrompt(Function(title) DialogResult.Cancel)
                    board.Open(second)
                    Assert.AreEqual("First, edited", board.Field("Title").Text, "Cancel keeps the first manuscript open.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Open(second)
                    Assert.AreEqual("Second", board.Field("Title").Text)
                    Assert.AreEqual("First", first.Title)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("First", board.Field("Title").Text, "Back returns to the first manuscript, as saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ClosingPaperRouteWithUnsavedChangesAsks()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    board.SetPrompt(Function(title) DialogResult.Cancel)
                    board.Close()
                    Assert.IsFalse(board.IsDisposed, "Cancel keeps PaperRoute open.")
                    Assert.IsTrue(board.Visible)

                    board.SetPrompt(Function(title) DialogResult.Yes)
                    board.Close()
                    Assert.AreEqual("Edited title", manuscript.Title, "Yes saves before closing.")
                    Assert.AreEqual(1, board.SaveCount)
                End Using
            End Sub)
    End Sub

    ' Backup, Restore and Update ask about unsaved page edits before they
    ' start (#112).
    <TestMethod>
    Public Sub BackupAsksAboutUnsavedEditsAndCancelWritesNothing()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard(), dialogs As New DialogWatcher()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    board.BackUp()
                    Assert.AreEqual(1, asked, "Backup asks before anything else.")
                    Assert.AreEqual(0, board.SaveCount, "Cancel saves nothing.")
                    Assert.IsFalse(File.Exists(board.BackupPath), "Cancel writes no backup file.")
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "Cancel opens no dialog.")
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "Cancel keeps the edits.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub RestoreAsksAboutUnsavedEditsAndCancelBeginsNothing()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard(), dialogs As New DialogWatcher()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    board.Restore()
                    Assert.AreEqual(1, asked, "Restore asks before choosing a backup.")
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "Cancel opens no file dialog, so no restore begins.")
                    Assert.AreEqual(0, board.SaveCount)
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "Cancel keeps the edits.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub BackupYesSavesTheEditsIntoItAndNoBacksUpTheSavedVersion()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"

                        board.SetPrompt(Function(title) DialogResult.Yes)
                        board.BackUp()
                        Assert.AreEqual("Edited title", manuscript.Title, "Yes saves first.")
                        Assert.IsFalse(board.Editor.HasUnsavedChanges())
                        StringAssert.Contains(BackedUpJson(board.BackupPath), "Edited title", "The backup holds the edits.")
                        CollectionAssert.AreEqual({"Backup Complete"}, dialogs.Closed)

                        dialogs.Closed.Clear()
                        board.Field("Title").Text = "Later title"
                        board.SetPrompt(Function(title) DialogResult.No)
                        board.BackUp()
                        Dim json As String = BackedUpJson(board.BackupPath)
                        StringAssert.Contains(json, "Edited title", "No backs up the last saved version.")
                        Assert.IsFalse(json.Contains("Later title"))
                        Assert.AreEqual("Later title", board.Field("Title").Text, "No keeps the edits on the page.")
                        Assert.IsTrue(board.Editor.HasUnsavedChanges())
                        CollectionAssert.AreEqual({"Backup Complete"}, dialogs.Closed)

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub UpdateAsksAfterAcceptingAndCancelNeverApplies()
        RunOnStaThread(
            Sub()
                Dim update As New ScriptedUpdate()
                UpdateService.Steps = update.Steps()
                OnlineAccess.ResetForTests()
                Try
                    Using board As New PageBoard()
                        update.Window = board
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"

                        Dim asked As Integer = 0
                        board.SetPrompt(Function(title)
                                           asked += 1
                                           Return DialogResult.Cancel
                                       End Function)
                        Assert.IsFalse(board.OfferUpdate(interactive:=True))
                        Assert.AreEqual(1, update.Offered)
                        Assert.AreEqual(1, asked, "The question comes after the user accepts the update.")
                        Assert.AreEqual(0, update.Downloaded)
                        Assert.AreEqual(0, update.Applied, "Cancel never applies.")
                        Assert.AreEqual("Edited title", board.Field("Title").Text, "Cancel keeps the edits.")
                        Assert.IsTrue(board.Enabled)

                        board.SetPrompt(Function(title) DialogResult.Yes)
                        Assert.IsTrue(board.OfferUpdate(interactive:=True))
                        Assert.AreEqual("Edited title", manuscript.Title, "Yes saves before the download.")
                        Assert.AreEqual(1, board.SaveCount)
                        Assert.AreEqual(1, update.Downloaded)
                        Assert.IsFalse(update.WindowEnabledWhileDownloading, "The window takes no clicks while the update downloads.")
                        Assert.AreEqual(1, update.Applied)
                        Assert.AreEqual(0, update.Reports.Count)

                        board.Close()
                    End Using
                Finally
                    UpdateService.Steps = New UpdateSteps()
                    OnlineAccess.ResetForTests()
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ADownloadFailureAfterAcceptingIsReportedOnTheStartupPath()
        RunOnStaThread(
            Sub()
                Dim update As New ScriptedUpdate()
                UpdateService.Steps = update.Steps()
                OnlineAccess.ResetForTests()
                Try
                    Using board As New PageBoard()
                        update.Window = board
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)

                        ' Before the user accepted, the startup check stays quiet, as before.
                        update.CheckFailure = New Net.Http.HttpRequestException("The connection was reset.")
                        Assert.IsFalse(board.OfferUpdate(interactive:=False))
                        Assert.AreEqual(0, update.Offered)
                        Assert.AreEqual(0, update.Reports.Count)

                        update.CheckFailure = Nothing
                        update.DownloadFailure = New Net.Http.HttpRequestException("The connection was reset.")
                        Assert.IsFalse(board.OfferUpdate(interactive:=False))
                        Assert.AreEqual(1, update.Offered)
                        Assert.AreEqual(1, update.Downloaded)
                        Assert.AreEqual(1, update.Reports.Count, "A failure after the user accepted is shown on the startup path.")
                        StringAssert.StartsWith(update.Reports(0), "Update Error: PaperRoute could not finish the update.")
                        Assert.IsTrue(board.Enabled, "The window takes clicks again after a failed download.")
                        Assert.IsFalse(Application.OpenForms.OfType(Of UpdateProgressForm)().Any(), "The progress window is gone.")
                        Assert.AreEqual(0, update.Applied)

                        board.Close()
                    End Using
                Finally
                    UpdateService.Steps = New UpdateSteps()
                    OnlineAccess.ResetForTests()
                End Try
            End Sub)
    End Sub

    ' A failed save cannot be closed away (#113). These go through the real
    ' SaveManuscripts against a repository whose data folder is blocked by a
    ' file of the same name, so every save fails until the file is removed.
    <TestMethod>
    Public Sub AFailedSaveKeepsTheBarAndAsksBeforeClosing()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        File.WriteAllText(Path.Combine(root, "data"), "in the way")
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"

                        board.PressCommandKey(Keys.Control Or Keys.S)
                        Assert.AreEqual("Edited title", manuscript.Title, "The edits reached the model.")
                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                        Assert.IsFalse(board.Editor.HasUnsavedChanges(), "The editor and the model agree.")
                        Assert.IsTrue(board.SaveBarVisible, "The bar stays after a failed save.")
                        board.Tick()
                        Assert.IsTrue(board.SaveBarVisible, "The bar stays after the dirty check too.")
                        Assert.AreEqual("Your last save did not finish. Save tries again.", board.SaveBarHint, "There is no saved version to return to, so the hint says what Save does.")
                        Assert.IsTrue(board.LibrarySaveFailed)
                        Assert.AreEqual("Warning: latest changes were not saved.", board.StatusText)
                        board.SetStatus("Citation copied.")
                        Assert.AreEqual("Warning: latest changes were not saved.", board.StatusText, "The status line keeps its warning.")

                        Dim asked As Integer = 0
                        board.SetPrompt(Function(title)
                                           asked += 1
                                           Return DialogResult.Cancel
                                       End Function)
                        board.Close()
                        Assert.AreEqual(1, asked, "Closing asks about the unsaved library.")
                        Assert.IsFalse(board.IsDisposed, "Cancel keeps PaperRoute open.")

                        dialogs.Closed.Clear()
                        board.SetPrompt(Function(title) DialogResult.Yes)
                        board.Close()
                        Assert.AreEqual(2, board.SaveCount, "Retry Save tries again.")
                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                        Assert.IsFalse(board.IsDisposed, "A retry that fails keeps PaperRoute open.")
                        Assert.IsTrue(board.LibrarySaveFailed)

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                        Assert.IsTrue(board.IsDisposed, "Close Without Saving closes.")
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub RetryingTheSaveClearsTheFlagAndTheBar()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim blocker As String = Path.Combine(root, "data")
                        File.WriteAllText(blocker, "in the way")
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"
                        board.PressCommandKey(Keys.Control Or Keys.S)
                        Assert.IsTrue(board.SaveBarVisible, "The bar stays after a failed save.")

                        File.Delete(blocker)
                        board.PressCommandKey(Keys.Control Or Keys.S)
                        Assert.IsFalse(board.LibrarySaveFailed, "A save that succeeds clears the flag.")
                        Assert.IsFalse(board.SaveBarVisible, "A save that succeeds clears the bar.")
                        ' A guard: the hint never changed before #113's fix, so this passed before it too.
                        Assert.AreEqual("Save keeps them. Discard returns to the last saved version.", board.SaveBarHint, "The usual hint comes back.")
                        StringAssert.StartsWith(board.StatusText, "Saved locally")
                        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "data", "manuscripts.json")), "Edited title")

                        Dim asked As Integer = 0
                        board.SetPrompt(Function(title)
                                           asked += 1
                                           Return DialogResult.Cancel
                                       End Function)
                        board.Close()
                        Assert.AreEqual(0, asked, "Nothing is left to ask about.")
                        Assert.IsTrue(board.IsDisposed)
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub RetrySaveOnClosingSavesTheLibraryAndCloses()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim blocker As String = Path.Combine(root, "data")
                        File.WriteAllText(blocker, "in the way")
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"
                        board.PressCommandKey(Keys.Control Or Keys.S)
                        Assert.IsTrue(board.SaveBarVisible, "The bar stays after a failed save.")

                        File.Delete(blocker)
                        board.SetPrompt(Function(title) DialogResult.Yes)
                        board.Close()
                        Assert.IsTrue(board.IsDisposed, "Retry Save that succeeds lets the window close.")
                        Assert.AreEqual(2, board.SaveCount)
                        Assert.IsFalse(board.LibrarySaveFailed)
                        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "data", "manuscripts.json")), "Edited title", "The retried save reached the disk.")
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' A Windows shutdown after a failed save (#113 with #123): there is no one
    ' to answer the question a close asks, and the page's recovery draft does
    ' not hold what the save missed, since it is already in the model. The
    ' save is tried once more without a question. Before the change the
    ' shutdown closed with neither, and the changes were lost.
    <TestMethod>
    Public Sub AShutdownAfterAFailedSaveTriesTheSaveOnceWithoutAsking()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim blocker As String = Path.Combine(root, "data")
                        File.WriteAllText(blocker, "in the way")
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"
                        board.PressCommandKey(Keys.Control Or Keys.S)
                        Assert.IsTrue(board.LibrarySaveFailed)
                        Assert.AreEqual(1, board.SaveCount)

                        File.Delete(blocker)
                        Dim asked As Integer = 0
                        board.SetPrompt(Function(title)
                                           asked += 1
                                           Return DialogResult.Cancel
                                       End Function)
                        Dim closing As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)
                        board.SimulateClosing(closing)

                        Assert.AreEqual(0, asked, "Shutdown is not held up by a question.")
                        Assert.IsFalse(closing.Cancel)
                        Assert.AreEqual(2, board.SaveCount, "The failed save is tried once more.")
                        Assert.IsFalse(board.LibrarySaveFailed)
                        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "data", "manuscripts.json")), "Edited title", "The retried save reached the disk.")

                        board.Close()
                        Assert.AreEqual(0, asked, "Nothing is left to ask about.")
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' Yes to restarting in a new appearance (#178) restarts as a restore and
    ' a startup recovery do: the page's edits and a failed save are asked
    ' about first, and Cancel at either restarts nothing; then the window
    ' hands over and restarts through RestartOnTheLibraryOnDisk, which
    ' releases the single-instance lock, and the close the restart sends
    ' asks and saves nothing again. Before the change the restart was a
    ' plain Application.Restart, with no seam a test could stand in for: it
    ' asked nothing first and released nothing, and its close asked later.
    <TestMethod>
    Public Sub TheAppearanceRestartAsksFirstThenHandsOver()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim manuscript As Manuscript = Sample("Original title")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        board.Field("Title").Text = "Edited title"
                        Dim questions As New List(Of String)()
                        Dim answer As DialogResult = DialogResult.Cancel
                        board.SetPrompt(Function(title)
                                           questions.Add(title)
                                           Return answer
                                       End Function)

                        board.RestartForAppearance()
                        CollectionAssert.AreEqual({"Original title"}, questions, "The page's edits are asked about first.")
                        Assert.AreEqual(0, board.Restarts, "Cancel at the page's question restarts nothing.")
                        Assert.AreEqual(0, board.SaveCount)
                        Assert.AreEqual("Edited title", board.Field("Title").Text, "Cancel keeps the edits.")

                        Using held As FileStream = HoldTheSaveFile(root)
                            board.PressCommandKey(Keys.Control Or Keys.S)
                        End Using
                        Assert.IsTrue(board.LibrarySaveFailed)
                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                        questions.Clear()
                        board.RestartForAppearance()
                        CollectionAssert.AreEqual({"your library"}, questions, "The failed save is asked about.")
                        Assert.AreEqual(0, board.Restarts, "Cancel at the failed save's question restarts nothing.")
                        Assert.AreEqual(1, board.SaveCount)

                        questions.Clear()
                        answer = DialogResult.Yes
                        board.RestartForAppearance()
                        CollectionAssert.AreEqual({"your library"}, questions)
                        Assert.AreEqual(2, board.SaveCount, "Yes saved again.")
                        Assert.IsFalse(board.LibrarySaveFailed)
                        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "data", "manuscripts.json")), "Edited title")
                        Assert.AreEqual(1, board.Restarts, "Then PaperRoute restarts through RestartOnTheLibraryOnDisk.")
                        Assert.IsTrue(board.HandingOverAtRestart, "Handing over, so the restart's close neither asks nor saves.")

                        board.Close()
                        Assert.IsTrue(board.IsDisposed, "The restart's close goes through.")
                        CollectionAssert.AreEqual({"your library"}, questions, "Nothing is asked twice.")
                        Assert.AreEqual(2, board.SaveCount, "Nothing is saved twice.")
                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' The plain error dialog (#121) keeps the unsaved page reachable: Close
    ' PaperRoute goes through the normal close, Keep Working changes nothing.
    <TestMethod>
    Public Sub AnUnexpectedErrorOffersKeepWorkingOrTheNormalClose()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    Dim reported As Exception = Nothing
                    Dim unsavedShown As Boolean = False
                    Dim choice As DialogResult = DialogResult.Abort
                    board.SetErrorPrompt(Function(failure, unsaved)
                                             reported = failure
                                             unsavedShown = unsaved
                                             Return choice
                                         End Function)
                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)

                    Dim problem As New InvalidOperationException("The route map could not be drawn.")
                    board.ReportUnhandledError(problem)
                    Assert.AreSame(problem, reported)
                    Assert.IsTrue(unsavedShown, "The dialog is told the page has unsaved edits.")
                    Assert.AreEqual(1, asked, "Close PaperRoute goes through the normal close, which asks about the edits.")
                    Assert.IsFalse(board.IsDisposed, "Cancel there keeps PaperRoute open.")
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "The edits are still there.")

                    choice = DialogResult.Cancel
                    board.ReportUnhandledError(problem)
                    Assert.AreEqual(1, asked, "Keep Working asks nothing.")
                    Assert.IsFalse(board.IsDisposed)
                    Assert.AreEqual("Manuscript", board.PageName, "Keep Working returns to the page.")
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "The page is intact.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' With no window open (#121), an error while PaperRoute starts or after
    ' the window closed is still said out loud before PaperRoute exits. The
    ' real handler runs, so the box is the one a researcher would see, and
    ' the report of #120 is written first, under a root of this test's own
    ' so the user's profile is never touched.
    <TestMethod>
    Public Sub AnErrorWithNoWindowOpenIsShownBeforePaperRouteExits()
        RunOnStaThread(
            Sub()
                Assert.IsFalse(Application.OpenForms.OfType(Of Form1)().Any(), "This test needs no window open.")
                Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))
                StorageEnvironment.RootOverride = root
                Try
                    Using dialogs As New DialogWatcher()
                        ' Not named application: that would hide Application above.
                        Dim host As New ManuscriptPipeline.My.MyApplication()
                        Dim e As New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The library folder could not be located."))
                        GetType(ManuscriptPipeline.My.MyApplication).GetMethod("MyApplication_UnhandledException", BindingFlags.Instance Or BindingFlags.NonPublic).
                            Invoke(host, New Object() {host, e})
                        CollectionAssert.AreEqual({"PaperRoute"}, dialogs.Closed, "The error is shown even with nothing to keep working in.")
                        Assert.IsTrue(e.ExitApplication, "PaperRoute still exits afterwards.")
                    End Using

                    Assert.AreEqual(1, Directory.GetFiles(ErrorReportService.DefaultDirectory(), "error-report_*").Length, "The error is recorded on this computer before anything is shown (#120).")
                Finally
                    StorageEnvironment.RootOverride = Nothing
                    Try
                        Directory.Delete(root, True)
                    Catch
                    End Try
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub DeletingFromThePageRemovesTheManuscriptAndReturns()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim keep As Manuscript = Sample("Keep")
                    Dim remove As Manuscript = Sample("Remove")
                    board.Prepare(keep, remove)
                    board.Open(remove)
                    board.Field("Title").Text = "Edits on a deleted manuscript are not asked about"

                    Dim asked As Boolean = False
                    board.SetPrompt(Function(title)
                                       asked = True
                                       Return DialogResult.Cancel
                                   End Function)
                    board.ConfirmDelete()
                    Application.DoEvents()

                    Assert.IsFalse(asked)
                    Assert.AreEqual("Board", board.PageName)
                    CollectionAssert.AreEqual({"Keep"}, board.Library.Select(Function(item) item.Title).ToList())
                    Assert.AreEqual(1, board.SaveCount)

                    board.PressCommandKey(Keys.Alt Or Keys.Right)
                    Assert.AreEqual("Board", board.PageName, "History skips the deleted manuscript.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Delete Manuscript... on the page asks for DELETE, as the card's Delete
    ' does (#221). Cancel and Escape leave the manuscript, the page and the
    ' saved library as they were. The watcher answers Yes to the Yes/No box
    ' the page asked with before, as one Enter did, so that code fails here
    ' instead of waiting on the box.
    <TestMethod>
    Public Sub DeleteFromThePageAsksForDeleteAndCancelKeepsTheManuscript()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard(), dialogs As New DialogWatcher(answerYes:="Delete Manuscript")
                    Dim manuscript As Manuscript = Sample("Kept")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim answers As New List(Of (Name As String, Answer As Action(Of DeleteManuscriptForm))) From {
                        ("Cancel", Sub(dialog) DirectCast(dialog.CancelButton, Button).PerformClick()),
                        ("Escape", Sub(dialog) PressKey(ConfirmBox(dialog), Keys.Escape))
                    }

                    For Each choice In answers
                        Dim asked As Integer = 0
                        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of DeleteManuscriptForm)(
                            Sub(dialog)
                                asked += 1
                                choice.Answer(dialog)
                            End Sub)
                            board.Editor.ConfirmDeleteFromPage()
                        End Using
                        Application.DoEvents()

                        Assert.AreEqual(1, asked, choice.Name & ": the page asks for DELETE.")
                        CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, choice.Name & ": no Yes/No box asks instead.")
                        CollectionAssert.AreEqual({"Kept"}, board.Library.Select(Function(item) item.Title).ToList(), choice.Name & ": the manuscript stays in the library.")
                        Assert.AreEqual("Manuscript", board.PageName, choice.Name & ": the page stays open.")
                        Assert.IsNotNull(board.Editor, choice.Name & ": the page keeps its manuscript.")
                        Assert.AreEqual(0, board.SaveCount, choice.Name & ": nothing is saved.")
                    Next

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Enter, and the Delete Manuscript button before DELETE is typed, remove
    ' nothing (#221): the window has no default button, and its Delete
    ' button waits for the word. Cancel then closes it.
    <TestMethod>
    Public Sub DeleteFromThePageIgnoresEnterAndTheButtonUntilDeleteIsTyped()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard(), dialogs As New DialogWatcher(answerYes:="Delete Manuscript")
                    Dim manuscript As Manuscript = Sample("Kept")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim asked As Integer = 0
                    Dim stillAsking As Boolean = False
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of DeleteManuscriptForm)(
                        Sub(dialog)
                            asked += 1
                            PressKey(ConfirmBox(dialog), Keys.Enter)
                            DeleteButton(dialog).PerformClick()
                            stillAsking = dialog.Visible AndAlso dialog.DialogResult = DialogResult.None AndAlso Not DeleteButton(dialog).Enabled
                            DirectCast(dialog.CancelButton, Button).PerformClick()
                        End Sub)
                        board.Editor.ConfirmDeleteFromPage()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(1, asked, "The page asks for DELETE.")
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No Yes/No box asks instead.")
                    Assert.IsTrue(stillAsking, "Enter and the Delete button leave the window waiting for DELETE.")
                    CollectionAssert.AreEqual({"Kept"}, board.Library.Select(Function(item) item.Title).ToList(), "Nothing is removed.")
                    Assert.AreEqual("Manuscript", board.PageName, "The page stays open.")
                    Assert.AreEqual(0, board.SaveCount, "Nothing is saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' DELETE typed and Delete Manuscript clicked remove the manuscript, save
    ' the library once, and return to the page it was opened from (#221).
    <TestMethod>
    Public Sub DeleteFromThePageRemovesTheManuscriptOnceDeleteIsTyped()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard(), dialogs As New DialogWatcher(answerYes:="Delete Manuscript")
                    Dim keep As Manuscript = Sample("Keep")
                    Dim remove As Manuscript = Sample("Remove")
                    board.Prepare(keep, remove)
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    board.Open(remove)
                    Assert.AreEqual("Manuscript", board.PageName)

                    Dim asked As Integer = 0
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of DeleteManuscriptForm)(
                        Sub(dialog)
                            asked += 1
                            ConfirmBox(dialog).Text = "DELETE"
                            DeleteButton(dialog).PerformClick()
                        End Sub)
                        board.Editor.ConfirmDeleteFromPage()
                    End Using
                    Application.DoEvents()

                    Assert.AreEqual(1, asked, "The page asks for DELETE.")
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No Yes/No box asks instead.")
                    CollectionAssert.AreEqual({"Keep"}, board.Library.Select(Function(item) item.Title).ToList(), "The manuscript is removed.")
                    Assert.AreEqual(1, board.SaveCount, "The library is saved once.")
                    Assert.AreEqual("Library", board.PageName, "The window returns to the page the manuscript was opened from.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The status line says which manuscript the page deleted (#225), once
    ' the window is back where the page was opened from.
    <TestMethod>
    Public Sub DeletingFromThePageSaysWhatWasDeleted()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim keep As Manuscript = Sample("Keep")
                    Dim remove As Manuscript = Sample("Remove")
                    board.Prepare(keep, remove)
                    board.Open(remove)

                    board.ConfirmDelete()
                    Application.DoEvents()

                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual("Deleted ""Remove"".", board.StatusText)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A delete whose save leaves a folder in place, because a file in it
    ' is open, keeps the save's warning after what it did (#225), one space
    ' after the sentence. The other status tests save through a stand-in
    ' that never warns; this save is real.
    <TestMethod>
    Public Sub DeletingFromThePageKeepsTheSaveWarning()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim keep As Manuscript = Sample("Keep")
                        Dim remove As Manuscript = Sample("Remove")
                        Dim working As String = Path.Combine(root, "revision.txt")
                        File.WriteAllText(working, "A revision.")
                        Dim version As ManuscriptVersion =
                            ManuscriptVersionService.CreateVersion(remove, "Revision 1", String.Empty, working, True)
                        board.Prepare(keep, remove)
                        Assert.IsTrue(board.SaveNow(), "The snapshot is copied into the library.")
                        Dim folder As String = Path.GetDirectoryName(version.LocalFilePath)
                        board.Open(remove)

                        ' The snapshot is open in another program, so its folder cannot move.
                        Using File.Open(version.LocalFilePath, FileMode.Open, FileAccess.Read, FileShare.None)
                            board.ConfirmDelete()
                        End Using
                        Application.DoEvents()

                        Assert.IsTrue(File.Exists(version.LocalFilePath), "The folder in use stayed in place.")
                        Assert.AreEqual(
                            "Deleted ""Remove"". A folder stayed in place because a file in it is in use: " & folder,
                            board.StatusText)

                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' The Delete Manuscript window's DELETE box and Delete button.
    Private Shared Function ConfirmBox(dialog As DeleteManuscriptForm) As TextBox
        Return Descendants(dialog).OfType(Of TextBox)().Single()
    End Function

    Private Shared Function DeleteButton(dialog As DeleteManuscriptForm) As Button
        Return Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Delete Manuscript")
    End Function

    ' A key pressed in a control as the message loop hands it on, so the
    ' control and the window around it see it as they see a real press.
    Private Shared Sub PressKey(target As Control, key As Keys)
        Dim press As Message = Message.Create(target.Handle, &H100, New IntPtr(CInt(key)), IntPtr.Zero)
        target.PreProcessMessage(press)
    End Sub

    <TestMethod>
    Public Sub OverviewShowsTheLinkedJournalsNotesAndChecklist()
        RunOnStaThread(
            Sub()
                Dim directory As String = Path.Combine(Path.GetTempPath(), "PaperRoute-JournalNotes-" & Guid.NewGuid().ToString("N"))
                Try
                    Dim journal As New JournalRecord With {.Name = "Fictional Methods Review", .Notes = "Report exact p values."}
                    journal.ReadinessChecklistTemplate.Add(New JournalChecklistTemplateItem With {.Title = "Data availability statement"})
                    Dim library As New AuthorLibraryData()
                    library.Journals.Add(journal)
                    Dim repository As New AuthorLibraryRepository(directory)
                    repository.Save(library)

                    Dim linked As Manuscript = Sample("Linked")
                    linked.TargetJournal = journal.Name
                    linked.TargetJournalId = journal.Id

                    Using editor As New EditManuscriptForm(linked, {linked}, repository, pageMode:=True)
                        ShowOffscreen(editor)
                        Dim notes As GroupBox = Descendants(editor).OfType(Of GroupBox)().Single(Function(group) group.Text = "Notes for Fictional Methods Review")
                        Assert.IsTrue(notes.Visible)
                        Dim text As String = notes.Controls.OfType(Of Label)().Single().Text
                        StringAssert.Contains(text, "Report exact p values.")
                        StringAssert.Contains(text, "1 checklist item")
                        Assert.IsFalse(editor.HasUnsavedChanges(), "Showing notes changes nothing.")
                    End Using

                    Using editor As New EditManuscriptForm(Sample("Unlinked"), Nothing, repository, pageMode:=True)
                        ShowOffscreen(editor)
                        Assert.IsFalse(Descendants(editor).OfType(Of GroupBox)().Any(Function(group) group.Text.StartsWith("Notes for") AndAlso group.Visible),
                            "Without a linked journal record there are no notes to show.")
                    End Using
                Finally
                    If IO.Directory.Exists(directory) Then IO.Directory.Delete(directory, True)
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub SubmissionsShowTheListBesideTheSelectedSubmission()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = WithRoute()
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim list As ListBox = Descendants(board.Editor).OfType(Of ListBox)().Single(Function(box) box.AccessibleName = "Journal submissions")
                    Assert.AreEqual(2, list.Items.Count)
                    Assert.AreEqual(1, list.SelectedIndex, "The most recent submission is selected.")

                    Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    CollectionAssert.AreEqual(
                        {"Editorial History", "Reviewer Responses", "Correspondence & Files"},
                        Descendants(detail).OfType(Of TabPage)().Select(Function(page) page.Text.Replace("&&", "&")).ToList())
                    Assert.IsFalse(Descendants(detail).OfType(Of Button)().Any(Function(button) button.Text = "Close" OrElse button.Text = "Reviewer Responses..."),
                        "Inline, there is no Close button and responses are a tab.")
                    Dim matrix As ReviewerResponseMatrixForm = Descendants(detail).OfType(Of ReviewerResponseMatrixForm)().Single()
                    Assert.AreEqual(2, Descendants(matrix).OfType(Of ListBox)().Single().Items.Count, "The matrix lists the submission's comments.")

                    list.SelectedIndex = 0
                    Application.DoEvents()
                    Dim first As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    Assert.AreNotSame(detail, first, "Selecting another submission shows it.")
                    Assert.IsTrue(Descendants(first).OfType(Of Label)().Any(Function(label) label.Text = "First Journal"))

                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Viewing submissions changes nothing.")
                    Assert.AreEqual(2, manuscript.Submissions.Count)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub InlineReviewerResponsesAreSavedOrDiscardedWithThePage()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = WithRoute()
                    Dim responses As List(Of ReviewerResponseItem) = manuscript.Submissions(1).ReviewerResponses
                    Dim originalOrder As List(Of String) = responses.Select(Function(item) item.ReviewerLabel).ToList()
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    MoveFirstResponseDown(board)
                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "Reordering inline is an unsaved change.")
                    CollectionAssert.AreEqual(originalOrder, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList(),
                        "The stored manuscript is unchanged until Save.")

                    board.Discard()
                    CollectionAssert.AreEqual(originalOrder, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList())
                    Assert.AreEqual(0, board.SaveCount)

                    MoveFirstResponseDown(board)
                    board.PressCommandKey(Keys.Control Or Keys.S)
                    CollectionAssert.AreEqual({"Reviewer 2", "Reviewer 1"}, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList(),
                        "Save stores the new order.")
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsTrue(manuscript.Submissions(1).ReviewerResponses.All(Function(item) item.DecisionId = manuscript.Submissions(1).Decisions(0).Id AndAlso item.RevisionRoundNumber = 1),
                        "Items keep their decision and explicit round.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #59: an empty library opens on a welcome, which never adds data.
    <TestMethod>
    Public Sub AnEmptyLibraryOpensOnTheWelcomeUntilTheFirstManuscript()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare()

                    Assert.IsTrue(board.Welcome.Visible, "An empty library shows the welcome.")
                    Assert.AreEqual(0, board.Library.Count, "The welcome adds no sample data.")
                    Assert.AreEqual(0, board.SaveCount)
                    CollectionAssert.AreEqual(
                        {"Paste a title page", "ORCID works", "BibTeX or RIS", "Spreadsheet"},
                        Descendants(board.Welcome).OfType(Of FilterChip)().Select(Function(chip) chip.Text).ToList())

                    ' #83 and #96: the example library opens from the welcome,
                    ' Import & Export, and Help, in its own window; nothing
                    ' here changes.
                    Dim launches As Integer = 0
                    board.exampleLauncher = Sub() launches += 1
                    Dim example As LinkLabel = Descendants(board.Welcome).OfType(Of LinkLabel)().Single(Function(link) link.Text.EndsWith("Explore an example library", StringComparison.Ordinal))
                    GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(example, New Object() {New LinkLabelLinkClickedEventArgs(example.Links(0))})
                    board.PressCommandKey(Keys.Control Or Keys.D6)
                    Descendants(board).OfType(Of ActionButton)().Single(Function(button) button.Text = "Explore an Example Library...").PerformClick()
                    Using help As HelpForm = board.CreateUserGuide()
                        ShowOffscreen(help)
                        Descendants(help).OfType(Of Button)().Single(Function(button) button.Text = "Explore an Example Library...").PerformClick()
                        Assert.IsTrue(help.Visible, "Help stays open beside the example.")
                        help.Close()
                    End Using
                    Using plain As New HelpForm()
                        Assert.IsFalse(Descendants(plain).OfType(Of Button)().Any(Function(button) button.Text = "Explore an Example Library..."),
                                       "Help opened elsewhere, or inside the example, doesn't offer it.")
                    End Using
                    Assert.AreEqual(3, launches)
                    Assert.AreEqual(0, board.Library.Count, "Opening the example adds nothing to this library.")
                    Assert.AreEqual(0, board.SaveCount)
                    board.PressCommandKey(Keys.Control Or Keys.D1)

                    board.Library.Add(Sample("The first manuscript"))
                    board.Render()
                    Assert.IsFalse(board.Welcome.Visible, "The shelves replace the welcome.")

                    board.Library.Clear()
                    board.Render()
                    Assert.IsTrue(board.Welcome.Visible)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #130: the welcome links to the guide's Quick Start, which opens Help at
    ' that section. The dialog is modal, so the test looks at the window the
    ' link creates through the welcome's helpShower seam.
    <TestMethod>
    Public Sub TheWelcomeLinksToTheQuickStart()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare()

                    Dim opened As String = Nothing
                    board.helpShower =
                        Sub(help)
                            ShowOffscreen(help)
                            Application.DoEvents()
                            opened = help.GuideText.Substring(help.GuideSelectionStart)
                            help.Close()
                        End Sub
                    Dim quickStart As LinkLabel = Descendants(board.Welcome).OfType(Of LinkLabel)().Single(Function(link) link.Text = "Read the Quick Start")
                    GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(quickStart, New Object() {New LinkLabelLinkClickedEventArgs(quickStart.Links(0))})

                    Assert.IsNotNull(opened, "The link opens Help.")
                    Assert.IsTrue(opened.StartsWith("Quick Start", StringComparison.Ordinal),
                        "Help opens at the Quick Start section, not at: " & opened.Substring(0, Math.Min(60, opened.Length)))
                    Assert.AreEqual(0, board.Library.Count, "Reading the guide adds nothing to the library.")
                    Assert.AreEqual(0, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #54 and #59: the Library filters as you type and says when nothing matches.
    <TestMethod>
    Public Sub LibraryFilterNarrowsTheTableAndExplainsNoMatches()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim revising As Manuscript = Sample("Attention in the wild")
                    revising.TargetJournal = "Journal of Fictional Revisions"
                    board.Prepare(revising, Sample("A second study"), Sample("A third study"))
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual("Library", board.PageName)

                    Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    Dim filter As TextBox = Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter the Library")
                    Dim hint As Label = grid.Controls.OfType(Of Label)().Single()
                    Assert.AreEqual(3, grid.Rows.Count)
                    Assert.IsFalse(hint.Visible)

                    ' The grid refills once typing pauses (#133); the pause is
                    ' the debounce timer's tick.
                    filter.Text = "revisions"
                    Assert.AreEqual(3, grid.Rows.Count, "A keystroke alone leaves the grid as it is.")
                    board.PauseTyping()
                    Assert.AreEqual(1, grid.Rows.Count, "The filter matches the target journal.")
                    Assert.AreSame(revising, grid.Rows(0).Tag)

                    filter.Text = "no such paper"
                    board.PauseTyping()
                    Assert.AreEqual(0, grid.Rows.Count)
                    Assert.IsTrue(hint.Visible)
                    StringAssert.Contains(hint.Text, "no such paper")

                    filter.Text = String.Empty
                    board.PauseTyping()
                    Assert.AreEqual(3, grid.Rows.Count)
                    Assert.IsFalse(hint.Visible)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #58: ORCID works are listed with the other imports and open the author they belong to.
    <TestMethod>
    Public Sub ImportExistingWorkReachesOrcidWorksThroughTheAuthor()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare()

                    Descendants(board.Welcome).OfType(Of Button)().Single(Function(button) button.Text = "Import Existing Work").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("ImportExport", board.PageName)

                    Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "ORCID Works...").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Library", board.PageName)
                    Assert.IsTrue(board.AuthorEditorShown, "ORCID works open Authors & Affiliations.")
                    Assert.AreEqual(0, board.Library.Count)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The rail collapses to icons for more room and expands again.
    <TestMethod>
    Public Sub TheRailCollapsesToIconsAndExpandsAgain()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare(Sample("First manuscript"))
                    Dim toggle As Button = Descendants(board).OfType(Of Button)().Single(Function(button) button.AccessibleName = "Collapse navigation")
                    Dim expandedWidth As Integer = board.RailPage("Board").Width
                    Dim logo As RailLogo = Descendants(board).OfType(Of RailLogo)().Single()
                    StringAssert.Contains(logo.AccessibleName, "About PaperRoute")

                    toggle.PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Expand navigation", toggle.AccessibleName)
                    Assert.IsTrue(board.RailPage("Board").Width * 2 < expandedWidth, "Collapsed, the rail shows only icons.")
                    Assert.AreEqual("Library", board.RailPage("Library").Text, "Pages keep their names for tooltips and assistive technology.")
                    Assert.IsTrue(logo.Visible, "The logo remains when collapsed.")
                    Assert.IsFalse(Descendants(board).OfType(Of Label)().Single(Function(label) label.Text = ProductInfo.Tagline).Visible,
                        "The tagline folds away with the name.")
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual("Library", board.PageName, "Navigation works while collapsed.")

                    toggle.PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Collapse navigation", toggle.AccessibleName)
                    Assert.AreEqual(expandedWidth, board.RailPage("Board").Width)
                    Dim settings = DirectCast(GetType(Form1).GetField("appSettings", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), AppSettings)
                    Assert.IsFalse(settings.NavigationCollapsed, "Settings that were never loaded are never saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Deadlines (#28): grouped by when, filtered by kind or text, and every
    ' action changes the record that owns the date.
    <TestMethod>
    Public Sub DeadlinesGroupFilterAndChangeTheOwningRecord()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim today As DateTime = DateTime.Today
                    Dim revision As Manuscript = WithRoute()
                    Dim decision As EditorialDecisionEvent = revision.Submissions(1).Decisions.Single()
                    decision.RevisionDeadline = today.AddDays(-2)
                    Dim waiting As Manuscript = Sample("Grit scale")
                    waiting.CurrentStage = PaperStage.Submitted
                    Dim submission As New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = today.AddDays(-30), .FollowUpDate = today.AddDays(3)}
                    waiting.Submissions.Add(submission)
                    Dim reminder As New ManuscriptReminder With {.Title = "Send draft to coauthors", .DueDate = today}
                    waiting.Reminders.Add(reminder)
                    waiting.Reminders.Add(New ManuscriptReminder With {.Title = "Upload preregistration", .DueDate = today.AddDays(-4), .IsCompleted = True, .CompletedDate = today.AddDays(-1)})
                    board.Prepare(revision, waiting)

                    Dim rail As RailButton = DirectCast(board.RailPage("Deadlines"), RailButton)
                    Assert.AreEqual(2, rail.Badge, "The rail counts what is overdue or due today.")

                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Assert.AreEqual("Deadlines", board.PageName)
                    Assert.IsTrue(rail.Checked)
                    CollectionAssert.AreEqual({"OVERDUE  1", "TODAY  1", "NEXT 7 DAYS  1"},
                        DeadlineTexts(board).Where(Function(text) text.StartsWith("OVERDUE") OrElse text.StartsWith("TODAY") OrElse
                                                                  text.StartsWith("NEXT") OrElse text.StartsWith("LATER") OrElse
                                                                  text.StartsWith("NO DATE")).ToList())
                    CollectionAssert.AreEqual({"Revision due", "Send draft to coauthors", "Follow up with Assessment"},
                        DeadlineRows(board).Select(Function(row) row.Item.Title).ToList(), "Done stays folded away.")
                    Dim revisionRow As DeadlineRow = DeadlineRows(board).First()
                    StringAssert.Contains(revisionRow.AccessibleName, "Overdue")
                    StringAssert.Contains(revisionRow.AccessibleName, "0 of 2 comments", "A revision shows its reviewer-comment progress.")
                    CollectionAssert.AreEqual({"Open", "Postpone..."}, revisionRow.ActionTexts.ToList())

                    StringAssert.StartsWith(DoneLink(board).Description, "Done in the last")
                    Assert.AreEqual("Show done items", DoneLink(board).Name)
                    ClickDoneLink(board)
                    Assert.AreEqual(4, DeadlineRows(board).Count, "Show lists recently completed reminders.")
                    Assert.AreEqual("Hide done items", DoneLink(board).Name)
                    ClickDoneLink(board)

                    ClickControl(Descendants(board).OfType(Of FilterChip)().Single(Function(chip) chip.Text.StartsWith("Follow-ups")))
                    CollectionAssert.AreEqual({"Follow up with Assessment"}, DeadlineRows(board).Select(Function(row) row.Item.Title).ToList())
                    ClickControl(Descendants(board).OfType(Of FilterChip)().Single(Function(chip) chip.Text.StartsWith("Follow-ups")))
                    Assert.AreEqual(3, DeadlineRows(board).Count, "Clicking the active chip shows everything again.")

                    Dim filter As TextBox = Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter deadlines")
                    filter.Text = "grit"
                    Application.DoEvents()
                    Assert.IsTrue(DeadlineRows(board).All(Function(row) row.Item.ManuscriptTitle = "Grit scale"))
                    filter.Text = String.Empty
                    Application.DoEvents()

                    ' Postpone asks for a date and writes the submission's follow-up.
                    board.SetDeadlinePrompts(Function(item) today.AddDays(10), Function(question) True)
                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).RunAction("Postpone...")
                    Application.DoEvents()
                    Assert.AreEqual(today.AddDays(10), submission.FollowUpDate.Value)
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.AreEqual(DeadlineGroup.Later, DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).Item.Group)

                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.Reminder).RunAction("Done")
                    Application.DoEvents()
                    Assert.IsTrue(reminder.IsCompleted)
                    Assert.AreEqual(2, board.SaveCount)
                    Assert.AreEqual(1, rail.Badge, "The badge follows the change.")

                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).RunAction("Clear Follow-up...")
                    Application.DoEvents()
                    Assert.IsFalse(submission.FollowUpDate.HasValue)
                    Assert.AreEqual(1, waiting.Submissions.Count, "Clearing leaves the submission as it was.")
                    Assert.AreEqual(3, board.SaveCount)

                    ' Open lands on the revision's reviewer responses.
                    DeadlineRows(board).Single().RunAction("Open")
                    Application.DoEvents()
                    Assert.AreEqual("Manuscript", board.PageName)
                    Assert.IsTrue(Descendants(board.Editor).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Submissions").Checked)
                    Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    Assert.AreEqual("Second Journal", DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(detail), JournalSubmission).JournalName)
                    Assert.AreEqual("Reviewer Responses", Descendants(detail).OfType(Of TabControl)().First().SelectedTab.Text)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Deadlines", board.PageName, "Back returns to Deadlines.")
                    Assert.AreEqual(3, board.SaveCount, "Opening and returning save nothing.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Nothing to act on: a sentence, not an empty list.
    <TestMethod>
    Public Sub DeadlinesExplainAnEmptyList()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare(Sample("Draft only"))
                    Descendants(board).OfType(Of LinkLabel)().Single(Function(link) link.Text = "View in Deadlines →").Links(0).Enabled = True
                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Assert.AreEqual(0, DeadlineRows(board).Count)
                    Assert.IsTrue(DeadlineTexts(board).Any(Function(text) text.StartsWith("Nothing needs action right now.")))
                    Assert.AreEqual(0, DirectCast(board.RailPage("Deadlines"), RailButton).Badge)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Possible publications (#61) wait on Deadlines until Mark Published or
    ' Ignore; each choice is saved.
    <TestMethod>
    Public Sub PossiblePublicationsAreReviewedOnDeadlines()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("Anchoring effects in clinical risk estimates")
                    first.CurrentStage = PaperStage.UnderReview
                    first.PublicationMatches.Add(New PublicationMatch With {.Doi = "10.5555/anchoring", .Journal = "Collabra: Psychology", .PublishedDate = DateTime.Today.AddDays(-3)})
                    Dim second As Manuscript = Sample("Grit across four countries")
                    second.CurrentStage = PaperStage.Submitted
                    second.PublicationMatches.Add(New PublicationMatch With {.Doi = "10.5555/grit", .Journal = "Assessment"})
                    board.Prepare(first, second)
                    board.markPublishedPrompt = Function(manuscript, match) True

                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Dim chip As FilterChip = Descendants(board).OfType(Of FilterChip)().Single(Function(candidate) candidate.Text.StartsWith("Publications"))
                    Assert.AreEqual("Publications  2", chip.Text)
                    Assert.IsTrue(chip.Visible)
                    Dim row As DeadlineRow = DeadlineRows(board).First(Function(candidate) candidate.Item.ManuscriptId = first.Id)
                    Assert.AreEqual("May have been published in Collabra: Psychology", row.Item.Title)
                    CollectionAssert.AreEqual({"Review Match", "Mark Published...", "Ignore Match", "Open Manuscript"}, row.ActionTexts.ToList())

                    row.RunAction("Mark Published...")
                    Application.DoEvents()
                    Assert.AreEqual(PaperStage.Published, board.Library(0).CurrentStage)
                    Assert.AreEqual(ManuscriptLocation.Published, board.Library(0).Location)
                    Assert.AreEqual("10.5555/anchoring", board.Library(0).Metadata.Doi)
                    Assert.AreEqual(1, board.SaveCount)

                    DeadlineRows(board).Single(Function(candidate) candidate.Item.Kind = DeadlineKind.Publication).RunAction("Ignore Match")
                    Application.DoEvents()
                    Assert.AreEqual(PublicationMatchStatus.Ignored, second.PublicationMatches.Single().Status)
                    Assert.AreEqual(PaperStage.Submitted, second.CurrentStage, "Ignore changes nothing else.")
                    Assert.AreEqual(2, board.SaveCount)
                    Assert.IsFalse(Descendants(board).OfType(Of FilterChip)().Single(Function(candidate) candidate.Text.StartsWith("Publications")).Visible,
                        "The Publications chip appears only while there is something to review.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub PublicationCheckFindsKeepsAndMarksPublished()
        RunOnStaThread(
            Sub()
                Dim tracked As Manuscript = Sample("Measurement invariance of a short grit scale across four countries")
                tracked.CurrentStage = PaperStage.UnderReview
                tracked.Submissions.Add(New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = DateTime.Today.AddDays(-120)})
                Dim idea As Manuscript = Sample("An idea that has not been submitted anywhere yet")
                Dim library As New List(Of Manuscript) From {tracked, idea}
                Dim saves As Integer = 0
                Dim source As New OneTitleSource(tracked.Title, New CrossrefMetadataSuggestion With {
                    .Doi = "10.5555/grit", .Title = tracked.Title, .Journal = "Assessment", .WorkType = "journal-article", .PublishedDate = DateTime.Today.AddDays(-2)})

                Using dialog As New PublicationCheckForm(library, Nothing, String.Empty, source, Function()
                                                                                                   saves += 1
                                                                                                   Return True
                                                                                               End Function, DateTime.Today)
                    dialog.Pause = TimeSpan.Zero
                    dialog.ConfirmMarkPublished = Function(manuscript, match) True
                    ShowOffscreen(dialog)
                    CollectionAssert.AreEqual({tracked}, dialog.SelectedManuscripts, "Work that has gone to a journal is checked by default.")

                    Dim check As Task = dialog.CheckAsync()
                    While Not check.IsCompleted
                        Application.DoEvents()
                    End While
                    check.GetAwaiter().GetResult()

                    Assert.AreEqual(PublicationMatchStatus.Pending, tracked.PublicationMatches.Single().Status, "A found match is kept for later review.")
                    Assert.AreEqual(1, saves)
                    Assert.AreEqual(PaperStage.UnderReview, tracked.CurrentStage, "Finding a match changes nothing else.")
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "A publication matching this manuscript may have appeared."))

                    Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Mark Published...").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual(PaperStage.Published, tracked.CurrentStage)
                    Assert.AreEqual(2, saves)
                    Assert.IsTrue(dialog.Changed)
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("Marked published.") AndAlso label.Visible))
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    ' A failed save in Fill Blanks puts a copy of each manuscript it filled
    ' back in the library, and the window stays open to try again (#184).
    ' Before the change the retry filled the preview's own records, which
    ' the library no longer held, saved the copies without the fills, and
    ' reported them filled. The retry now fills the records the library
    ' holds, and the count it reports is what the saved library has.
    <TestMethod>
    Public Sub FillBlanksRetriedAfterAFailedSaveFillsTheSavedLibrary()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim manuscript As Manuscript = Sample("Measurement invariance of a short grit scale across four countries")
                        manuscript.Metadata.Doi = "10.5555/grit"
                        board.Prepare(manuscript)
                        Dim source As New OneDoiSource("10.5555/grit", New CrossrefMetadataSuggestion With {.Doi = "10.5555/grit", .Journal = "Assessment", .Volume = "33"})

                        Using dialog As New FillBlanksForm(board.Library, source, AddressOf board.SaveNow)
                            dialog.Pause = TimeSpan.Zero
                            ShowOffscreen(dialog)
                            Dim lookUp As Task = dialog.LookUpAsync()
                            While Not lookUp.IsCompleted
                                Application.DoEvents()
                            End While
                            lookUp.GetAwaiter().GetResult()
                            CollectionAssert.AreEqual({"Journal", "Volume"}, dialog.PlannedChanges.Select(Function(fill) fill.Field).ToList())

                            Using held As FileStream = HoldTheSaveFile(root)
                                dialog.ApplyChecked()
                            End Using
                            CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                            Assert.IsFalse(dialog.IsDisposed, "The window stays open to try again.")
                            Assert.AreEqual(0, dialog.FilledCount, "Nothing is reported filled.")
                            Assert.AreEqual(String.Empty, board.Library.Single().Metadata.PublicationJournal, "The library is as it was.")

                            dialog.ApplyChecked()
                            Assert.AreEqual(DialogResult.OK, dialog.DialogResult, "The retry saved.")
                            Dim saved As Manuscript = SavedLibrary(root).Single()
                            Assert.AreEqual("Assessment", saved.Metadata.PublicationJournal, "The fills reached the saved library.")
                            Assert.AreEqual("33", saved.Metadata.Volume)
                            Assert.AreEqual(2, dialog.FilledCount, "Filled 2 empty fields: the two the saved library has.")
                        End Using

                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed, "Only the first save failed.")
                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' A failed save after Mark Published puts a copy of the manuscript back
    ' in the library, and the card stays to try again (#184). Before the
    ' change the retry marked the card's own record, which the library no
    ' longer held, saved the copy unmarked, and said it was on the Published
    ' shelf. The retry now marks the record the library holds.
    <TestMethod>
    Public Sub MarkPublishedRetriedAfterAFailedSaveMarksTheSavedLibrary()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim tracked As Manuscript = TrackedForPublication()
                        board.Prepare(tracked)

                        Using dialog As PublicationCheckForm = ShowCheckedPublications(board, tracked)
                            Dim mark As Button = Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Mark Published...")

                            Using held As FileStream = HoldTheSaveFile(root)
                                mark.PerformClick()
                                Application.DoEvents()
                            End Using
                            CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                            Assert.IsFalse(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("Marked published.") AndAlso label.Visible), "Nothing is reported marked.")
                            Assert.AreEqual(PaperStage.UnderReview, board.Library.Single().CurrentStage, "The library is as it was.")
                            Assert.IsTrue(mark.Visible, "The card's actions stay to try again.")

                            mark.PerformClick()
                            Application.DoEvents()
                            Dim saved As Manuscript = SavedLibrary(root).Single()
                            Assert.AreEqual(PaperStage.Published, saved.CurrentStage, "The retry reached the saved library.")
                            Assert.AreEqual(ManuscriptLocation.Published, saved.Location)
                            Assert.AreEqual(PublicationMatchStatus.Confirmed, saved.PublicationMatches.Single().Status)
                            Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "Marked published. It is on the Published shelf." AndAlso label.Visible))
                            Assert.IsTrue(dialog.Changed)
                            dialog.Close()
                        End Using

                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed, "Only the first save failed.")
                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' Ignore after a failed save, as Mark Published (#184); a save refused
    ' because other library work runs is a failed save to the window too.
    ' Before the change the last press ignored the card's own match, saved
    ' the library's copy still pending, and said it was ignored.
    <TestMethod>
    Public Sub IgnoreRetriedAfterAFailedSaveIgnoresInTheSavedLibrary()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim tracked As Manuscript = TrackedForPublication()
                        board.Prepare(tracked)

                        Using dialog As PublicationCheckForm = ShowCheckedPublications(board, tracked)
                            Dim ignore As Button = Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Ignore")
                            Dim ignored As Func(Of Boolean) = Function() Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "Ignored. Later checks will not show this match again." AndAlso label.Visible)

                            Using held As FileStream = HoldTheSaveFile(root)
                                ignore.PerformClick()
                                Application.DoEvents()
                            End Using
                            CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                            Assert.IsFalse(ignored(), "Nothing is reported ignored.")
                            Assert.AreEqual(PublicationMatchStatus.Pending, board.Library.Single().PublicationMatches.Single().Status, "The library is as it was.")

                            Assert.IsTrue(board.RunWork("Backing up your library...", Sub() board.Invoke(New Action(Sub() ignore.PerformClick()))))
                            Assert.IsFalse(ignored(), "A refused save reports nothing either.")
                            Assert.AreEqual(PublicationMatchStatus.Pending, board.Library.Single().PublicationMatches.Single().Status, "The library is still as it was.")

                            ignore.PerformClick()
                            Application.DoEvents()
                            Dim saved As Manuscript = SavedLibrary(root).Single()
                            Assert.AreEqual(PublicationMatchStatus.Ignored, saved.PublicationMatches.Single().Status, "The retry reached the saved library.")
                            Assert.AreEqual(PaperStage.UnderReview, saved.CurrentStage, "Ignore changes nothing else.")
                            Assert.IsTrue(ignored())
                            dialog.Close()
                        End Using

                        CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed, "Only the first save failed.")
                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' New matches whose save fails are taken back out, so none waits on the
    ' Deadlines page (#184). Before the change the window still said they
    ' were there to review and stayed on the Deadlines page; it now says
    ' they were not kept.
    <TestMethod>
    Public Sub NewMatchesThatFailToSaveAreNotReportedAsKept()
        RunOnStaThread(
            Sub()
                Dim root As String = CreateTemporaryRoot()
                Try
                    Using board As New PageBoard(), dialogs As New DialogWatcher()
                        board.UseRepository(root)
                        Dim tracked As Manuscript = TrackedForPublication()
                        board.Prepare(tracked)
                        Assert.IsTrue(board.SaveNow(), "The library is saved before the check.")
                        Dim source As New OneTitleSource(tracked.Title, New CrossrefMetadataSuggestion With {
                            .Doi = "10.5555/grit", .Title = tracked.Title, .Journal = "Assessment", .WorkType = "journal-article", .PublishedDate = DateTime.Today.AddDays(-2)})

                        Using dialog As New PublicationCheckForm(board.Library, Nothing, String.Empty, source, AddressOf board.SaveNow, DateTime.Today)
                            dialog.Pause = TimeSpan.Zero
                            ShowOffscreen(dialog)
                            Using held As FileStream = HoldTheSaveFile(root)
                                Dim check As Task = dialog.CheckAsync()
                                While Not check.IsCompleted
                                    Application.DoEvents()
                                End While
                                check.GetAwaiter().GetResult()
                            End Using

                            CollectionAssert.AreEqual({"Save Error"}, dialogs.Closed)
                            Assert.IsFalse(Descendants(dialog).OfType(Of Button)().Any(Function(button) button.Text = "Mark Published..." OrElse button.Text = "Ignore"), "No card offers a match that was not kept.")
                            Dim intro As String = Descendants(dialog).OfType(Of Label)().Single(Function(label) label.Text.StartsWith("Checked 1 manuscript.")).Text
                            Assert.IsFalse(intro.Contains("Deadlines page"), "Nothing is said to wait on the Deadlines page: " & intro)
                            Assert.IsTrue(intro.Contains("not kept"), "The window says the match was not kept: " & intro)
                            Assert.AreEqual(0, board.Library.Single().PublicationMatches.Count, "The library is as it was.")
                            Assert.AreEqual(0, SavedLibrary(root).Single().PublicationMatches.Count, "Nothing reached the saved library.")
                            Assert.IsFalse(dialog.Changed)
                            dialog.Close()
                        End Using

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                Finally
                    DeleteTemporaryRoot(root)
                End Try
            End Sub)
    End Sub

    ' Under review, with a match a title search finds.
    Private Shared Function TrackedForPublication() As Manuscript
        Dim tracked As Manuscript = Sample("Measurement invariance of a short grit scale across four countries")
        tracked.CurrentStage = PaperStage.UnderReview
        tracked.Submissions.Add(New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = DateTime.Today.AddDays(-120)})
        Return tracked
    End Function

    ' Check for Publications on the board's library, saving through the
    ' board, with the check done and its match kept.
    Private Shared Function ShowCheckedPublications(board As PageBoard, tracked As Manuscript) As PublicationCheckForm
        Dim source As New OneTitleSource(tracked.Title, New CrossrefMetadataSuggestion With {
            .Doi = "10.5555/grit", .Title = tracked.Title, .Journal = "Assessment", .WorkType = "journal-article", .PublishedDate = DateTime.Today.AddDays(-2)})
        Dim dialog As New PublicationCheckForm(board.Library, Nothing, String.Empty, source, AddressOf board.SaveNow, DateTime.Today)
        dialog.Pause = TimeSpan.Zero
        dialog.ConfirmMarkPublished = Function(manuscript, match) True
        ShowOffscreen(dialog)
        Dim check As Task = dialog.CheckAsync()
        While Not check.IsCompleted
            Application.DoEvents()
        End While
        check.GetAwaiter().GetResult()
        Assert.AreEqual(PublicationMatchStatus.Pending, board.Library.Single().PublicationMatches.Single().Status, "The match was found and kept.")
        Return dialog
    End Function

    ' A file held open where the save writes makes it fail, as a sync
    ' client or a virus scanner can.
    Private Shared Function HoldTheSaveFile(root As String) As FileStream
        Dim data As String = Path.Combine(root, "data")
        Directory.CreateDirectory(data)
        Return New FileStream(Path.Combine(data, "manuscripts.tmp"), FileMode.Create, FileAccess.Write, FileShare.None)
    End Function

    ' The library as the next start would load it from the test's root.
    Private Shared Function SavedLibrary(root As String) As List(Of Manuscript)
        Return New ManuscriptRepository(Path.Combine(root, "data"), Path.Combine(root, "library")).Load()
    End Function

    ' Crossref's record for one DOI; nothing else is found.
    Private NotInheritable Class OneDoiSource
        Implements IPublicationSource

        Private ReadOnly _doi As String
        Private ReadOnly _work As CrossrefMetadataSuggestion

        Public Sub New(doi As String, work As CrossrefMetadataSuggestion)
            _doi = doi
            _work = work
        End Sub

        Public Function LookupDoiAsync(doi As String, cancellationToken As Threading.CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Return Task.FromResult(If(String.Equals(doi, _doi, StringComparison.OrdinalIgnoreCase), _work, Nothing))
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Return Task.FromResult(New List(Of CrossrefMetadataSuggestion)())
        End Function

        Public Function OrcidWorksAsync(orcid As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Return Task.FromResult(New List(Of OrcidWorkSuggestion)())
        End Function
    End Class

    Private NotInheritable Class OneTitleSource
        Implements IPublicationSource

        Private ReadOnly _title As String
        Private ReadOnly _work As CrossrefMetadataSuggestion

        Public Sub New(title As String, work As CrossrefMetadataSuggestion)
            _title = title
            _work = work
        End Sub

        Public Function LookupDoiAsync(doi As String, cancellationToken As Threading.CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Return Task.FromResult(Of CrossrefMetadataSuggestion)(Nothing)
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Return Task.FromResult(If(title = _title, New List(Of CrossrefMetadataSuggestion) From {_work}, New List(Of CrossrefMetadataSuggestion)()))
        End Function

        Public Function OrcidWorksAsync(orcid As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Return Task.FromResult(New List(Of OrcidWorkSuggestion)())
        End Function
    End Class

    ' Insights (#30, #62): your journals and routes, from recorded dates.
    <TestMethod>
    Public Sub InsightsSummarizeJournalsAndRoutesFromTheLibrary()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim routed As Manuscript = WithRoute()
                    board.Prepare(routed, Sample("Unsubmitted idea"))

                    board.PressCommandKey(Keys.Control Or Keys.D5)
                    board.WaitForInsights()
                    Assert.AreEqual("Insights", board.PageName)
                    Dim tiles As List(Of String) = Descendants(board).OfType(Of RoundedPanel)().Select(Function(tile) tile.AccessibleName).Where(Function(name) name IsNot Nothing).ToList()
                    Assert.IsTrue(tiles.Contains("2 submissions  ·  0 accepted"), String.Join(" | ", tiles))
                    Assert.IsTrue(tiles.Contains("19.5 days median to a first decision (of 2)"), "The median of 7 and 32 days.")
                    Assert.IsTrue(tiles.Contains("— median from first submission to acceptance (not recorded yet)"), "Nothing is estimated.")

                    Dim journals As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    CollectionAssert.AreEquivalent({"First Journal", "Second Journal"}, journals.Rows.Cast(Of DataGridViewRow)().Select(Function(row) CStr(row.Cells("Journal").Value)).ToList())
                    Dim second As DataGridViewRow = journals.Rows.Cast(Of DataGridViewRow)().Single(Function(row) CStr(row.Cells("Journal").Value) = "Second Journal")
                    Assert.AreEqual(1.0, CDbl(second.Cells("Revisions").Value))
                    Assert.AreEqual(32.0, CDbl(second.Cells("FirstDecision").Value))

                    journals.CurrentCell = second.Cells(0)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(journals, New Object() {New KeyEventArgs(Keys.Enter)})
                    Application.DoEvents()
                    board.WaitForInsights()
                    Dim routes As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    Assert.AreEqual(1, routes.Rows.Count, "Enter on a journal shows the routes that went through it.")
                    Assert.AreEqual("Routed manuscript", CStr(routes.Rows(0).Cells("Title").Value))
                    Assert.IsTrue(Descendants(board).OfType(Of LinkLabel)().Any(Function(link) link.Visible AndAlso link.Text.StartsWith("Showing manuscripts sent to Second Journal")))

                    routes.CurrentCell = routes.Rows(0).Cells(0)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(routes, New Object() {New KeyEventArgs(Keys.Enter)})
                    Application.DoEvents()
                    Assert.AreEqual("Manuscript", board.PageName, "Enter on a route opens the manuscript.")
                    Assert.AreEqual(0, board.SaveCount, "Insights never save.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The route map (#82): every route side by side on Insights, each one
    ' opening its own route view.
    <TestMethod>
    Public Sub TheRouteMapLinesUpRoutesAndOpensTheChosenOne()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim published As Manuscript = RouteMapServiceTests.Anchoring()
                    Dim routed As Manuscript = WithRoute()
                    board.Prepare(published, routed, Sample("Unsubmitted idea"))
                    Dim opened As New List(Of Manuscript)()
                    board.routeViewOpener = Sub(item) opened.Add(item)

                    board.PressCommandKey(Keys.Control Or Keys.D5)
                    board.WaitForInsights()
                    Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Route Map").Checked = True
                    Application.DoEvents()
                    board.WaitForInsights()
                    Dim chart As RouteMapChart = Descendants(board).OfType(Of RouteMapChart)().Single()
                    CollectionAssert.AreEqual({published}, chart.Routes.Select(Function(item) item.Manuscript).ToList(), "Published routes by default.")
                    Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text = "1 published route lined up at day 0. Median 264 days from first submission to publication."))
                    Assert.IsTrue(Descendants(board).OfType(Of RouteMapLegend)().Single().AccessibleName.StartsWith("With a journal  ·  40%"), "105 of 264 days.")

                    Dim toggle As LinkLabel = Descendants(board).OfType(Of LinkLabel)().Single(Function(link) link.Text = "Include work not yet published")
                    GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(toggle, New Object() {New LinkLabelLinkClickedEventArgs(toggle.Links(0))})
                    Application.DoEvents()
                    board.WaitForInsights()
                    chart = Descendants(board).OfType(Of RouteMapChart)().Single()
                    CollectionAssert.AreEquivalent({published, routed}, chart.Routes.Select(Function(item) item.Manuscript).ToList(), "Work in progress runs to today; an idea has no route.")
                    Assert.IsTrue(chart.Routes.Single(Function(item) item.Manuscript Is routed).Map.Ongoing)

                    chart.SelectedIndex = chart.Routes.FindIndex(Function(item) item.Manuscript Is routed)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(chart, New Object() {New KeyEventArgs(Keys.Enter)})
                    CollectionAssert.AreEqual({routed}, opened, "Enter opens the selected route.")
                    Assert.AreEqual(0, board.SaveCount, "The route map never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Your Citations (#91): saved figures on Insights, computed here; only
    ' Update from OpenAlex... asks anything, and the tab never saves the library.
    <TestMethod>
    Public Sub YourCitationsShowSavedFiguresAndUpdateOnlyWhenAsked()
        RunOnStaThread(
            Sub()
                Dim folder As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Citations-" & Guid.NewGuid().ToString("N"))
                Dim network As New NoNetwork()
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Dim published As Manuscript = RouteMapServiceTests.Anchoring()
                        published.Metadata.Doi = "https://doi.org/10.5555/Example.Anchoring"
                        Dim second As Manuscript = RouteMapServiceTests.Anchoring()
                        second.Title = "A second published manuscript"
                        second.Metadata.Doi = "10.5555/example.second"
                        board.Prepare(published, second, Sample("Unsubmitted idea"))
                        Dim store As New CitationStore(folder)
                        board.citationStoreFactory = Function() store
                        Dim prompted As New List(Of CitationSnapshot)()
                        board.citationsUpdatePrompt =
                            Function(previous)
                                prompted.Add(previous)
                                Dim snapshot As New CitationSnapshot With {.Orcid = "0000-0002-1825-0097", .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)}
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W1", .Doi = "10.5555/example.anchoring", .Year = 2026, .CitedByCount = 12, .Fwci = 1.2, .Percentile = 0.8,
                                                                       .CountsByYear = New List(Of YearCount) From {New YearCount With {.Year = 2026, .Count = 12}}})
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W2", .Doi = "10.5555/example.other", .Year = 2020, .CitedByCount = 3})
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W3", .Doi = "10.5555/example.second", .Year = 2018, .CitedByCount = 40, .Fwci = 12.3, .Percentile = 0.07})
                                Return snapshot
                            End Function

                        ' Without an ORCID iD, the tab says where to add one.
                        board.PressCommandKey(Keys.Control Or Keys.D5)
                        board.WaitForInsights()
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        board.WaitForInsights()
                        Assert.IsTrue(Descendants(board).OfType(Of Button)().Any(Function(button) button.Text = "Open Authors && Affiliations"))
                        Assert.IsFalse(Descendants(board).OfType(Of Label)().Single(Function(label) label.Text.StartsWith("Days are calendar days")).Visible, "The route footnote belongs to the other views.")

                        board.SetAuthors(New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .IsMe = True, .Orcid = "https://orcid.org/0000-0002-1825-0097"})
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text.StartsWith("Your Journals")).Checked = True
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        board.WaitForInsights()
                        Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("See how your published work has been cited, from OpenAlex") AndAlso label.Text.Contains("(0000-0002-1825-0097)")))
                        Dim update As Button = Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "Update from OpenAlex...")
                        Assert.IsTrue(update.Enabled)

                        ClickControl(update)
                        ' The update redraws the page, and its figures are worked out
                        ' off the UI thread (#132): wait for them before reading.
                        board.WaitForInsights()
                        Assert.AreEqual(1, prompted.Count)
                        Assert.IsNull(prompted(0), "Nothing saved before.")
                        Assert.AreEqual(3, store.Load().Works.Count, "The confirmed works are saved.")
                        Dim labels As List(Of String) = Descendants(board).OfType(Of Label)().Select(Function(label) label.Text).ToList()
                        CollectionAssert.IsSubsetOf({"Citations", "h-index", "i10-index", "g-index", "m-quotient", "55"}, labels)
                        Assert.IsTrue(labels.Any(Function(text) text.StartsWith("From OpenAlex on Sep 30, 2026, for the 3 works you confirmed (ORCID iD 0000-0002-1825-0097).")))
                        Assert.IsTrue(labels.Any(Function(text) text.Contains("2026 12 so far")))
                        Assert.IsTrue(labels.Contains("Other works on your record: 1 (not tracked in PaperRoute)."))
                        ' The figures sit inside the card, clear of its border.
                        Dim figures As SectionCard = Descendants(board).OfType(Of SectionCard)().Single(Function(card) card.AccessibleName = "Your citations")
                        Assert.IsTrue(figures.Controls(0).Left >= figures.Padding.Left AndAlso figures.Controls(0).Top >= figures.Padding.Top, "The card's content starts inside its padding.")
                        Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                        Assert.AreEqual(2, grid.Rows.Count, "Your manuscripts, joined by DOI.")
                        Dim row As DataGridViewRow = grid.Rows.Cast(Of DataGridViewRow)().Single(Function(item) CStr(item.Cells("Title").Value) = published.Title)
                        Assert.AreEqual(12.0, CDbl(row.Cells("Citations").Value))
                        Assert.AreEqual("80th", CStr(row.Cells("Percentile").Value))
                        StringAssert.EndsWith(CStr(row.Cells("Fwci").Value), "(provisional)")
                        ' Numbers sort as numbers: 12.30 above 1.20, the 80th above the 7th.
                        grid.Sort(grid.Columns("Fwci"), System.ComponentModel.ListSortDirection.Descending)
                        Assert.AreEqual("A second published manuscript", CStr(grid.Rows(0).Cells("Title").Value))
                        grid.Sort(grid.Columns("Percentile"), System.ComponentModel.ListSortDirection.Descending)
                        Assert.AreEqual(published.Title, CStr(grid.Rows(0).Cells("Title").Value))

                        ' Working offline leaves the saved figures and turns Update off.
                        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text.StartsWith("Your Routes")).Checked = True
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        board.WaitForInsights()
                        Assert.IsFalse(Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "Update from OpenAlex...").Enabled)
                        Assert.AreEqual(2, Descendants(board).OfType(Of DataGridView)().Single().Rows.Count)
                        Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("You're working offline, so updating is off.")))

                        Assert.AreEqual(0, network.Requests, "Showing citations never sends anything.")
                        Assert.AreEqual(0, board.SaveCount, "Citations never save the library.")
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                    If Directory.Exists(folder) Then Directory.Delete(folder, True)
                End Try
            End Sub)
    End Sub

    ' Your Citations' table gives a manuscript's title twice the width of
    ' its journal, as its columns' weights ask, 220 to 110 (#258, item 12).
    ' At 125% the split was the other way round: titles were cut short in
    ' about 275 pixels while Journal held "Fictional Journal" in about 458.
    ' With the window's text at 100%, 125% and 150%, from saved figures,
    ' Manuscript is drawn within a tenth of twice as wide as Journal.
    <TestMethod>
    Public Sub YourCitationsTableSplitsTitleAndJournalAsItsWeightsAsk()
        For Each scale As Single In {1.0F, 1.25F, 1.5F}
            Dim textScale As Single = scale
            RunOnStaThread(
                Sub()
                    Dim folder As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Citations-" & Guid.NewGuid().ToString("N"))
                    Dim network As New NoNetwork()
                    OnlineAccess.ResetForTests()
                    OnlineAccess.InnerHandlerFactory = Function() network
                    Try
                        Using board As New PageBoard()
                            Dim published As Manuscript = RouteMapServiceTests.Anchoring()
                            published.Metadata.Doi = "10.5555/example.anchoring"
                            Dim second As Manuscript = RouteMapServiceTests.Anchoring()
                            second.Title = "A second published manuscript"
                            second.Metadata.Doi = "10.5555/example.second"
                            board.Prepare(published, second)
                            board.Font = New Font(board.Font.FontFamily, 10.0F * textScale)

                            Dim store As New CitationStore(folder)
                            Dim snapshot As New CitationSnapshot With {.Orcid = "0000-0002-1825-0097", .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)}
                            snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W1", .Doi = "10.5555/example.anchoring", .Journal = "Fictional Journal", .Year = 2026, .CitedByCount = 12})
                            snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W2", .Doi = "10.5555/example.second", .Journal = "Fictional Journal", .Year = 2018, .CitedByCount = 40})
                            store.Save(snapshot)
                            board.citationStoreFactory = Function() store
                            board.SetAuthors(New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .IsMe = True, .Orcid = "https://orcid.org/0000-0002-1825-0097"})

                            board.PressCommandKey(Keys.Control Or Keys.D5)
                            board.WaitForInsights()
                            Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                            Application.DoEvents()
                            board.WaitForInsights()

                            Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                            Dim title As Integer = grid.Columns("Title").Width
                            Dim journal As Integer = grid.Columns("Journal").Width
                            Dim least As Integer = grid.Columns("Title").MinimumWidth
                            Dim line As String = $"With the text at {textScale:P0}, in a table {grid.Width} pixels wide, titles have {title} pixels and journals {journal}"

                            ' Where two thirds of what the two share is less than the
                            ' title's least width, the title has that width.
                            If (title + journal) * 2 \ 3 < least Then
                                Assert.AreEqual(least, title, line & ", and the title is held at its least width.")
                            Else
                                Assert.IsTrue(Math.Abs(title / journal - 2.0) <= 0.2, $"{line}: {title / journal:F2} to 1, not 220 to 110.")
                            End If

                            Assert.AreEqual(0, network.Requests, "Saved figures send nothing.")
                            board.Close()
                        End Using
                    Finally
                        OnlineAccess.ResetForTests()
                        If Directory.Exists(folder) Then Directory.Delete(folder, True)
                    End Try
                End Sub)
        Next
    End Sub

    <TestMethod>
    Public Sub TheRouteViewOpensOnItsRouteDrawnToScale()
        RunOnStaThread(
            Sub()
                Using view As New ManuscriptRouteViewForm(RouteMapServiceTests.Anchoring())
                    ShowOffscreen(view)
                    Dim bar As RouteMapBar = Descendants(view).OfType(Of RouteMapBar)().Single()
                    Assert.AreEqual("Route drawn to scale. 264 days from first submission to publication: 105 days with the journals, 131 days with you, 28 days in production.", bar.AccessibleName)
                    Assert.AreEqual(8, bar.Steps.Count)
                    StringAssert.StartsWith(bar.AccessibleDescription, "1: Desk rejected, Jan 12, 7 days after submission")
                    Assert.IsTrue(bar.Height > 0 AndAlso bar.Width > 0)
                    Dim onScreen As Rectangle = view.RectangleToClient(bar.RectangleToScreen(bar.ClientRectangle))
                    Assert.IsTrue(onScreen.Right <= view.ClientSize.Width, "The map fits the window's width.")
                    view.Close()
                End Using

                Using view As New ManuscriptRouteViewForm(Sample("Unsubmitted idea"))
                    ShowOffscreen(view)
                    Assert.IsFalse(Descendants(view).OfType(Of RouteMapBar)().Any(), "No route map before a first submission.")
                    view.Close()
                End Using
            End Sub)
    End Sub

    ' The journal shortlist (#65) on the Overview: added with its reasons,
    ' offered after a rejection, and saved with the page.
    <TestMethod>
    Public Sub TheShortlistOffersTheNextJournalAfterARejectionAndSavesWithThePage()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Rerouted manuscript")
                    manuscript.TargetJournal = "First Journal"
                    Dim first As New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)}
                    first.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
                    manuscript.Submissions.Add(first)
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Assert.AreEqual(String.Empty, board.Editor.ShortlistOfferText, "Nothing to offer before a journal is shortlisted.")
                    board.Editor.candidatePrompt =
                        Function(existing) New JournalCandidate With {.JournalName = "Open Psychology", .Status = CandidateStatus.Preferred,
                                                                     .Notes = "Publishes replications", .Checks = New List(Of String) From {"trust.known", "fit.scope"}}
                    board.Editor.AddShortlistCandidateForTest()

                    Assert.AreEqual("Desk rejected by First Journal on Jan 12, 2026. Next on your shortlist: Open Psychology (Preferred).", board.Editor.ShortlistOfferText)
                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The shortlist waits for Save like any change.")
                    Assert.AreEqual(0, manuscript.JournalShortlist.Count, "The saved record is untouched until Save.")

                    board.Editor.ShortlistOfferButton.PerformClick()
                    Assert.AreEqual(String.Empty, board.Editor.ShortlistOfferText, "Once it is the target journal, there is nothing more to offer.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual("Open Psychology", manuscript.TargetJournal)
                    Dim saved As JournalCandidate = manuscript.JournalShortlist.Single()
                    Assert.AreEqual("Publishes replications", saved.Notes)
                    CollectionAssert.AreEquivalent({"trust.known", "fit.scope"}, saved.Checks)
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub FoundJournalsJoinTheShortlistWithTheirEvidenceAndWaitForSave()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Anchoring in clinical risk estimates")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim messages As New List(Of String)()
                    AddHandler board.Editor.StatusMessage, Sub(sender, message) messages.Add(message)
                    Dim request As New JournalSuggestionRequest With {.Keywords = New List(Of String) From {"anchoring effects"}, .MatchAll = True, .SinceDate = New DateTime(2021, 9, 30)}
                    Dim found As New List(Of JournalSuggestion) From {
                        New JournalSuggestion With {.OpenAlexId = "S196734849", .Name = "Scientific Reports", .Issns = New List(Of String) From {"2045-2322"}, .MatchingArticles = 3, .AllArticles = 163365,
                                                    .Publisher = "Nature Portfolio", .Topics = New List(Of String) From {"Cancer-related molecular mechanisms research", "MicroRNA in disease regulation"},
                                                    .HomepageUrl = "http://www.nature.com/srep/index.html",
                                                    .Examples = New List(Of EvidenceExample) From {New EvidenceExample With {.Title = "Anchoring in triage", .Year = 2026, .Doi = "10.1038/s41598-026-66155-3"}}},
                        New JournalSuggestion With {.OpenAlexId = "S9692511", .Name = "Frontiers in Psychology", .Issns = New List(Of String) From {"1664-1078"}, .MatchingArticles = 2}
                    }
                    Dim result As New JournalSuggestionsResult With {.Request = request, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), .Journals = found}
                    board.Editor.journalSuggestionPrompt = Function() (found, result)
                    board.Editor.FindJournalsForTest()

                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "Found journals wait for Save like any change.")
                    Assert.AreEqual(0, manuscript.JournalShortlist.Count)
                    CollectionAssert.AreEqual({"Added 2 journals to the shortlist as Considering. Save the manuscript page to keep them."}, messages)

                    board.Editor.FindJournalsForTest()
                    Assert.AreEqual(1, messages.Count, "Journals already on the shortlist aren't added again.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual(2, manuscript.JournalShortlist.Count)
                    Dim saved As JournalCandidate = manuscript.JournalShortlist.First()
                    Assert.AreEqual(CandidateStatus.Considering, saved.Status)
                    Assert.AreEqual(3L, saved.Evidence.MatchingArticles)
                    Assert.AreEqual("Anchoring in triage", saved.Evidence.Examples.Single().Title)
                    CollectionAssert.AreEqual({"anchoring effects"}, saved.Evidence.Keywords)
                    Assert.AreEqual("Nature Portfolio", saved.Evidence.Publisher, "The publisher, topics, and homepage are kept with the evidence (#96).")
                    CollectionAssert.AreEqual({"Cancer-related molecular mechanisms research", "MicroRNA in disease regulation"}, saved.Evidence.Topics)
                    Assert.AreEqual("http://www.nature.com/srep/index.html", saved.Evidence.HomepageUrl)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Find Journals is unavailable while working offline (#96): the button is
    ' off, the shortlist says why, and turning Work offline off brings it back.
    <TestMethod>
    Public Sub FindJournalsIsUnavailableWhileWorkingOffline()
        RunOnStaThread(
            Sub()
                Dim network As New NoNetwork()
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Dim manuscript As Manuscript = Sample("Anchoring in clinical risk estimates")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        Assert.IsTrue(board.Editor.FindJournalsButton.Enabled)
                        Assert.AreEqual(String.Empty, board.Editor.FindJournalsOffText)

                        ' Work offline, turned on from the rail or the menu.
                        Dim settings As AppSettings = DirectCast(GetType(Form1).GetField("appSettings", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), AppSettings)
                        settings.OnlineServices.WorkOffline = True
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Application.DoEvents()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled, "Work offline turns the command off...")
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Work offline is on", "...and the shortlist says why.")
                        Assert.IsTrue(Descendants(board.Editor).OfType(Of Label)().Any(Function(label) label.Visible AndAlso label.Text = board.Editor.FindJournalsOffText))
                        Assert.AreEqual(board.Editor.FindJournalsOffText, board.Editor.FindJournalsButton.AccessibleDescription)

                        ' A shortlist change, or the page shown again, keeps it off.
                        board.Editor.candidatePrompt = Function(existing) New JournalCandidate With {.JournalName = "Open Psychology"}
                        board.Editor.AddShortlistCandidateForTest()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        board.Discard()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Work offline is on")

                        settings.OnlineServices.WorkOffline = False
                        settings.OnlineServices.TurnedOff.Add(OnlineServiceCatalog.JournalSuggestions)
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Find journals (OpenAlex) is turned off", "Turned off in Online services, it says so.")

                        settings.OnlineServices.TurnedOff.Clear()
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Application.DoEvents()
                        Assert.IsTrue(board.Editor.FindJournalsButton.Enabled, "Back online, Find Journals is available again.")
                        Assert.AreEqual(String.Empty, board.Editor.FindJournalsOffText)
                        Assert.IsFalse(Descendants(board.Editor).OfType(Of Label)().Any(Function(label) label.Visible AndAlso label.Text.StartsWith("Work offline is on", StringComparison.Ordinal)))
                        Assert.AreEqual(0, network.Requests, "Nothing was sent.")
                        Assert.AreEqual(0, board.SaveCount)
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ReportsPreviewAndSaveWhatIsChosen()
        RunOnStaThread(
            Sub()
                Dim path As String = IO.Path.Combine(IO.Path.GetTempPath(), "PaperRoute-Report-" & Guid.NewGuid().ToString("N") & ".html")
                Try
                    Dim pipeline As Manuscript = Sample("In the pipeline")
                    Dim drawer As Manuscript = Sample("In the drawer")
                    drawer.Location = ManuscriptLocation.FileDrawer
                    Using dialog As New ReportForm("Pipeline Report", "report.html", New List(Of Manuscript) From {pipeline, drawer},
                                                   Function(chosen, deadlines) ReportService.PipelineReport(chosen, DateTime.Today))
                        dialog.SavePathPrompt = Function() path
                        ShowOffscreen(dialog)
                        CollectionAssert.AreEqual({pipeline}, dialog.Chosen, "Pipeline work is included by default; the File Drawer is not.")
                        dialog.SaveReport(Nothing, EventArgs.Empty)
                        Assert.AreEqual(path, dialog.SavedPath)
                    End Using
                    Dim saved As String = IO.File.ReadAllText(path)
                    StringAssert.Contains(saved, "In the pipeline")
                    Assert.IsFalse(saved.Contains("In the drawer"))
                Finally
                    If IO.File.Exists(path) Then IO.File.Delete(path)
                End Try
            End Sub)
    End Sub

    ' Work types and tags (#64): edited on the page, saved with it, shown on
    ' the card, and found by board search.
    <TestMethod>
    Public Sub TypesAndTagsAreEditedOnThePageAndShownOnTheBoard()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Tagged manuscript")
                    Dim other As Manuscript = Sample("Other manuscript")
                    other.Tags.Add("grant")
                    board.Prepare(manuscript, other)
                    board.Open(manuscript)

                    Dim type As ComboBox = Descendants(board.Editor).OfType(Of ComboBox)().Single(Function(box) box.AccessibleName = "Type of work")
                    Assert.AreEqual("Not specified", type.Text, "No type is inferred.")
                    type.SelectedIndex = CInt(WorkType.Poster)
                    Dim tags As TagEditor = Descendants(board.Editor).OfType(Of TagEditor)().Single()
                    CollectionAssert.Contains(tags.Input.AutoCompleteCustomSource.Cast(Of String)().ToList(), "grant", "Tags already in the library are suggested.")
                    tags.Input.Text = "teaching"
                    tags.Commit()
                    tags.Input.Text = " Teaching "
                    tags.Commit()
                    Assert.AreEqual(1, Descendants(tags).OfType(Of TagChipButton)().Count(), "A tag is added once.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(WorkType.Unspecified, manuscript.WorkType, "Edits stay in the working copy until saved.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual(WorkType.Poster, manuscript.WorkType)
                    CollectionAssert.AreEqual({"teaching"}, manuscript.Tags)
                    Assert.AreEqual(1, board.SaveCount)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Dim strip As TagStrip = Descendants(board).OfType(Of TagStrip)().Single(Function(item) item.AccessibleName = "Tags: teaching")
                    Assert.IsTrue(strip.Width > 0, "The card shows the tag on its journal line.")

                    Dim search As TextBox = DirectCast(GetType(Form1).GetField("txtBoardSearch", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), TextBox)
                    ' What the Board shows for the search typed (#133: the search
                    ' is read once a render, not once a manuscript).
                    Dim visible = GetType(Form1).GetMethod("GetVisibleManuscripts", BindingFlags.Instance Or BindingFlags.NonPublic)
                    Dim shown As Func(Of List(Of Manuscript)) = Function() DirectCast(visible.Invoke(board, Nothing), List(Of Manuscript))
                    search.Text = "#teaching"
                    Assert.IsTrue(shown().Contains(manuscript))
                    Assert.IsFalse(shown().Contains(other))
                    search.Text = "poster"
                    Assert.IsTrue(shown().Contains(manuscript), "The type is searchable too.")
                    search.Text = String.Empty

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Two records with the same Id, which a manuscripts.json merged or edited
    ' by hand can hold and the library loads, each keep their own card on
    ' the Board (#133): one never replaces the other's in the same render.
    <TestMethod>
    Public Sub TwoRecordsWithTheSameIdEachHaveACard()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First copy")
                    Dim second As Manuscript = ManuscriptCloneService.CloneManuscript(first)
                    second.Title = "Second copy"
                    board.Prepare(first, second)

                    For Each pass As String In {"shown", "rendered again"}
                        CollectionAssert.AreEquivalent({"First copy", "Second copy"}, board.Cards().Select(Function(card) card.AccessibleName).ToList(),
                                                       "Both records are on the Board once " & pass & ".")
                        Assert.IsFalse(board.Cards().Any(Function(card) card.IsDisposed), "No card on a shelf is disposed once " & pass & ".")
                        board.Render()
                    Next

                    second.Title = first.Title
                    board.Render()
                    Assert.AreEqual(2, board.Cards().Count, "Records that show the same text still have a card each.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Board cards are kept between renders (#133). A kept card acts on the
    ' record the library holds when the card is used, as when an equal
    ' record with the same Id takes the old one's place, and a card is
    ' rebuilt when its title or its shelf changes. A guard: #133 already
    ' behaves so; the test fails if a card acts on the record it was built
    ' from, or if the title or the location leaves the text a card is kept
    ' by.
    <TestMethod>
    Public Sub KeptCardsActOnTheLibrarysRecordAndShowItsChanges()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim filed As Manuscript = Sample("Filed manuscript")
                    filed.Location = ManuscriptLocation.FileDrawer
                    board.Prepare(filed)
                    Dim card As RoundedPanel = board.Card("Filed manuscript")

                    ' An equal record with the same Id takes its place.
                    Dim restored As Manuscript = ManuscriptCloneService.CloneManuscript(filed)
                    board.ReplaceLibrary(restored)
                    Assert.AreSame(card, board.Card("Filed manuscript"), "The card is kept: what it shows is unchanged.")

                    MenuItem(card, "Restore to Pipeline").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual(ManuscriptLocation.Pipeline, restored.Location, "The kept card restored the library's record.")
                    Assert.AreEqual(ManuscriptLocation.FileDrawer, filed.Location, "The record from before the restore is left alone.")
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsNotNull(MenuItem(board.Card("Filed manuscript"), "Move to File Drawer..."), "On the Pipeline the card offers the Pipeline's menu.")

                    restored.Title = "Renamed manuscript"
                    board.Render()
                    CollectionAssert.AreEqual({"Renamed manuscript"}, board.Cards().Select(Function(item) item.AccessibleName).ToList(), "The card shows the new title.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Of two records that share an Id, each card acts on its own record, as
    ' each card did before cards were kept (#133). Looked up by the Id, the
    ' second copy's Restore to Pipeline restored the first copy instead,
    ' and left the second in the File Drawer.
    <TestMethod>
    Public Sub ACardActsOnItsOwnRecordWhenTwoRecordsShareAnId()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First copy")
                    first.Location = ManuscriptLocation.FileDrawer
                    Dim second As Manuscript = ManuscriptCloneService.CloneManuscript(first)
                    second.Title = "Second copy"
                    Assert.AreEqual(first.Id, second.Id, "The two records share an Id.")
                    board.Prepare(first, second)

                    MenuItem(board.Card("Second copy"), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, second.Location, "The second copy's card restored the second copy.")
                    Assert.AreEqual(ManuscriptLocation.FileDrawer, first.Location, "The first copy is left alone.")
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsNotNull(MenuItem(board.Card("Second copy"), "Move to File Drawer..."), "The second copy's card follows it to the Pipeline.")
                    Assert.IsNotNull(MenuItem(board.Card("First copy"), "Restore to Pipeline"), "The first copy's card stays in the File Drawer.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The people a Crossref lookup adds to the reusable author library wait
    ' in the page's copy of it until Save writes them. Opening Submission
    ' Readiness in between must not drop them: Save then has to write every
    ' person the manuscript names (#354): Submission Readiness, Manage
    ' Library and Journal, Preprint & Links each read the library from disk
    ' again, and Save wrote the copy read.
    <TestMethod>
    Public Sub PeopleACrossrefLookupAddedAreSavedAfterSubmissionReadinessIsOpened()
        RunOnStaThread(
            Sub()
                Dim network As New CrossrefWork()
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Dim manuscript As Manuscript = Sample("Looked-up manuscript")
                        manuscript.Metadata = New ManuscriptMetadata With {.Doi = CrossrefWork.Doi}
                        board.Prepare(manuscript)
                        board.Open(manuscript)

                        Dim editor As EditManuscriptForm = board.Editor
                        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
                        Dim repository As AuthorLibraryRepository = DirectCast(GetType(EditManuscriptForm).GetField("_authorRepository", flags).GetValue(editor), AuthorLibraryRepository)
                        Dim disk As New AuthorLibraryRepository(Path.GetDirectoryName(repository.DataFilePath))
                        Dim working As Manuscript = DirectCast(GetType(EditManuscriptForm).GetProperty("_workingManuscript", flags).GetValue(editor), Manuscript)

                        Using dialogs As New DialogWatcher()

                            ' DOI & Crossref Metadata: Look Up, then Apply Selected
                            ' with the authors box ticked.
                            Dim lookups As Integer = 0
                            Using lookup As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of CrossrefLookupForm)(
                                Sub(dialog)
                                    lookups += 1
                                    Dim field As Func(Of String, Object) = Function(name) GetType(CrossrefLookupForm).GetField(name, flags).GetValue(dialog)
                                    DirectCast(field("btnLookup"), Button).PerformClick()
                                    Dim apply As Button = DirectCast(field("btnApply"), Button)
                                    Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                                    While (field("_suggestion") Is Nothing OrElse Not apply.Enabled) AndAlso waited.Elapsed < TimeSpan.FromSeconds(10)
                                        Application.DoEvents()
                                        Thread.Sleep(1)
                                    End While
                                    If field("_suggestion") Is Nothing OrElse Not apply.Enabled Then
                                        dialog.DialogResult = DialogResult.Cancel
                                        Return
                                    End If
                                    DirectCast(field("chkAuthors"), CheckBox).Checked = True
                                    apply.PerformClick()
                                End Sub)
                                GetType(EditManuscriptForm).GetMethod("OpenCrossrefMetadata", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
                                Application.DoEvents()
                            End Using

                            Assert.AreEqual(1, lookups, "DOI & Crossref Metadata opened once.")
                            Assert.AreEqual(1, network.Requests, "One scripted Crossref request.")
                            CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                            Assert.AreEqual(2, working.Authors.Count, "The lookup put both people on the manuscript.")
                            Dim inMemory As AuthorLibraryData = DirectCast(GetType(EditManuscriptForm).GetProperty("_authorLibrary", flags).GetValue(editor), AuthorLibraryData)
                            Assert.IsTrue(working.Authors.All(Function(link) inMemory.Authors.Any(Function(person) person.Id = link.AuthorId)), "The lookup added both people to the page's author library.")
                            Assert.IsTrue(CBool(GetType(EditManuscriptForm).GetProperty("_authorLibraryDirty", flags).GetValue(editor)), "The page marks its author library changed.")
                            Assert.AreEqual(0, disk.Load().Authors.Count, "Nothing is on disk before Save.")

                            ' Submission Readiness, opened and closed.
                            Dim readinessOpened As Integer = 0
                            Using readiness As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of ManuscriptReadinessForm)(
                                Sub(dialog)
                                    readinessOpened += 1
                                    dialog.DialogResult = DialogResult.Cancel
                                End Sub)
                                GetType(EditManuscriptForm).GetMethod("OpenSubmissionReadiness", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
                                Application.DoEvents()
                            End Using
                            Assert.AreEqual(1, readinessOpened, "Submission Readiness opened once.")

                            board.PressCommandKey(Keys.Control Or Keys.S)
                            Assert.AreEqual(1, board.SaveCount, "The page saved.")
                            CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                        End Using

                        Assert.AreEqual(2, manuscript.Authors.Count, "The saved manuscript names both looked-up people.")
                        Dim saved As AuthorLibraryData = disk.Load()
                        For Each link As ManuscriptAuthor In manuscript.Authors
                            Assert.IsTrue(
                                saved.Authors.Any(Function(person) person.Id = link.AuthorId),
                                "Every person the saved manuscript names is in the author library on disk. Missing id " & link.AuthorId.ToString() &
                                "; on disk: " & saved.Authors.Count.ToString() & " people (" & String.Join(", ", saved.Authors.Select(Function(person) person.DisplayName)) & ").")
                        Next

                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                End Try
            End Sub)
    End Sub

    ' The same through Manage Library, closed (#354): the Authors tab names
    ' everyone at once, and Save writes the people and the affiliation the
    ' lookup created.
    <TestMethod>
    Public Sub PeopleACrossrefLookupAddedAreSavedAfterManageLibraryIsClosed()
        WithCrossrefPeopleUnsaved(
            New AuthorLibraryData(),
            Sub(board, manuscript, disk, root)
                CloseManageLibrary(board.Editor)
                Dim authorsList As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), ListBox)
                Dim shown As List(Of String) = authorsList.Items.Cast(Of Object)().Select(Function(item) authorsList.GetItemText(item)).ToList()
                Assert.AreEqual(2, shown.Count, "The Authors tab lists both people.")
                Assert.IsFalse(shown.Any(Function(text) text.Contains("[Missing author record]")), "No one is missing: " & String.Join(" | ", shown))

                SavePage(board)
                AssertEveryoneIsOnDisk(manuscript, disk)
            End Sub)
    End Sub

    ' The same through Journal, Preprint & Links closed with OK (#354),
    ' while a person already in the library was edited on disk: Save keeps
    ' that edit and adds the looked-up people.
    <TestMethod>
    Public Sub PeopleACrossrefLookupAddedAreSavedAfterJournalLinksAndADiskEditIsKept()
        Dim seed As New AuthorLibraryData()
        Dim existing As New AuthorRecord With {.GivenName = "Disk", .FamilyName = "Person"}
        seed.Authors.Add(existing)
        WithCrossrefPeopleUnsaved(
            seed,
            Sub(board, manuscript, disk, root)
                Dim opened As Integer = 0
                Using links As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of ManuscriptLinksForm)(
                    Sub(dialog)
                        opened += 1
                        Dim onDisk As AuthorLibraryData = disk.Load()
                        onDisk.Authors.Single(Function(person) person.Id = existing.Id).FamilyName = "Edited"
                        disk.Save(onDisk)
                        dialog.DialogResult = DialogResult.OK
                    End Sub)
                    GetType(EditManuscriptForm).GetMethod("OpenJournalLinks", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board.Editor, New Object() {Nothing, EventArgs.Empty})
                    Application.DoEvents()
                End Using
                Assert.AreEqual(1, opened, "Journal, Preprint & Links opened once.")

                SavePage(board)
                AssertEveryoneIsOnDisk(manuscript, disk)
                Assert.AreEqual("Edited", disk.Load().Authors.Single(Function(person) person.Id = existing.Id).FamilyName, "The edit made on disk meanwhile is kept.")
            End Sub)
    End Sub

    ' A lookup, Submission Readiness, then Discard (#354): neither file
    ' changes, and the page's library holds only what is on disk.
    <TestMethod>
    Public Sub ACrossrefLookupThenSubmissionReadinessThenDiscardWritesNothing()
        Dim seed As New AuthorLibraryData()
        seed.Authors.Add(New AuthorRecord With {.GivenName = "Disk", .FamilyName = "Person"})
        WithCrossrefPeopleUnsaved(
            seed,
            Sub(board, manuscript, disk, root)
                board.UseRepository(root)
                Assert.IsTrue(board.SaveNow(), "The library is on disk.")
                Dim manuscriptsFile As String = Path.Combine(root, "data", "manuscripts.json")
                Dim manuscriptsBefore As Byte() = File.ReadAllBytes(manuscriptsFile)
                Dim authorsBefore As Byte() = File.ReadAllBytes(disk.DataFilePath)

                CloseSubmissionReadiness(board.Editor)
                board.Discard()

                CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(disk.DataFilePath), "The author library on disk is unchanged.")
                CollectionAssert.AreEqual(manuscriptsBefore, File.ReadAllBytes(manuscriptsFile), "manuscripts.json is unchanged.")
                Assert.AreEqual(1, PageLibrary(board.Editor).Authors.Count, "The page's library holds only the person on disk.")
                Assert.IsFalse(CBool(GetType(EditManuscriptForm).GetProperty("_authorLibraryDirty", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor)), "Nothing is left to save.")
            End Sub)
    End Sub

    ' A person and an affiliation deleted in Authors & Affiliations while
    ' the page holds unsaved looked-up people (#354) stay deleted: the
    ' reload carries over only the records the page made, not everything
    ' missing from disk.
    <TestMethod>
    Public Sub APersonDeletedInManageLibraryIsNotBroughtBackByTheReload()
        Dim seed As New AuthorLibraryData()
        Dim gone As New AuthorRecord With {.GivenName = "Gone", .FamilyName = "Person"}
        seed.Authors.Add(gone)
        Dim goneAffiliation As New AffiliationRecord With {.Institution = "Gone University", .Country = "USA"}
        seed.Affiliations.Add(goneAffiliation)
        WithCrossrefPeopleUnsaved(
            seed,
            Sub(board, manuscript, disk, root)
                ' The window deletes the person and the affiliation and
                ' saves, as Delete does.
                CloseManageLibrary(board.Editor,
                    Sub()
                        Dim onDisk As AuthorLibraryData = disk.Load()
                        onDisk.Authors.RemoveAll(Function(person) person.Id = gone.Id)
                        onDisk.Affiliations.RemoveAll(Function(affiliation) affiliation.Id = goneAffiliation.Id)
                        disk.Save(onDisk)
                    End Sub)
                Assert.IsFalse(PageLibrary(board.Editor).Authors.Any(Function(person) person.Id = gone.Id), "The page's library does not bring the deleted person back.")
                Assert.IsFalse(PageLibrary(board.Editor).Affiliations.Any(Function(affiliation) affiliation.Id = goneAffiliation.Id), "The page's library does not bring the deleted affiliation back.")

                SavePage(board)
                AssertEveryoneIsOnDisk(manuscript, disk)
                Assert.IsFalse(disk.Load().Authors.Any(Function(person) person.Id = gone.Id), "Save does not write the deleted person back.")
                Assert.IsFalse(disk.Load().Affiliations.Any(Function(affiliation) affiliation.Id = goneAffiliation.Id), "Save does not write the deleted affiliation back.")
            End Sub)
    End Sub

    ' Two reloads before Save, Submission Readiness then Manage Library
    ' (#354): what the first carried over is still the page's own at the
    ' second, so Save writes everyone.
    <TestMethod>
    Public Sub PeopleACrossrefLookupAddedAreSavedAfterTwoReloads()
        WithCrossrefPeopleUnsaved(
            New AuthorLibraryData(),
            Sub(board, manuscript, disk, root)
                CloseSubmissionReadiness(board.Editor)
                CloseManageLibrary(board.Editor)
                Assert.AreEqual(2, PageLibrary(board.Editor).Authors.Count, "Both looked-up people survive two reloads.")
                Assert.AreEqual(1, PageLibrary(board.Editor).Affiliations.Count, "The looked-up affiliation survives two reloads.")

                SavePage(board)
                AssertEveryoneIsOnDisk(manuscript, disk)
            End Sub)
    End Sub

    ' A tag colour chosen on the page survives Submission Readiness, and
    ' one chosen after it is the one saved (#354): the tag editor works on
    ' the library read again.
    <TestMethod>
    Public Sub ATagColourChosenOnThePageSurvivesSubmissionReadinessAndIsSaved()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Tagged manuscript")
                    manuscript.Tags = New List(Of String) From {"teaching"}
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    Dim disk As New AuthorLibraryRepository(board.AuthorDirectory)

                    Using dialogs As New DialogWatcher()
                        ChooseTagColour(board.Editor, "teaching", TagPalette.Violet)
                        CloseSubmissionReadiness(board.Editor)
                        Assert.AreEqual(TagPalette.Violet, WorkTypeService.ColorOf("teaching", PageLibrary(board.Editor)), "The colour chosen survives the reload.")
                        ChooseTagColour(board.Editor, "teaching", TagPalette.Orange)

                        Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                        CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                    End Using

                    Dim saved As TagColor = disk.Load().TagColors.SingleOrDefault(Function(entry) entry.Tag = "teaching")
                    Assert.IsNotNull(saved, "The tag's colour is saved.")
                    Assert.AreEqual(TagPalette.Orange, saved.Color, "The colour chosen last is saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Probe (v1.0.1, recovery draft): the people and affiliation a Crossref
    ' lookup created wait in the page's author library until Save. When the
    ' session ends before Save, the recovery draft (#123) holds only the
    ' manuscript. The next start opens the draft as unsaved changes; the
    ' Authors tab, and Save, must still have everyone the manuscript names.
    ' Session one writes the draft as a shutdown does; its window is then
    ' closed with No (which removes the draft it wrote) and the draft's bytes
    ' are put back, as a session that ended without closing leaves them.
    ' Session two is a fresh window on the same files.
    <TestMethod>
    Public Sub ARecoveredDraftKeepsThePeopleACrossrefLookupAddedBeforeTheSessionEnded()
        RunOnStaThread(
            Sub()
                Dim network As New CrossrefWork("Example Institute")
                Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))
                Dim dataDirectory As String = Path.Combine(root, "data")
                Dim draftPath As String = Path.Combine(dataDirectory, "recovery", RecoveryDraftStore.FileName)
                Dim authorDirectory As String = Nothing
                StorageEnvironment.RootOverride = Path.Combine(root, "storage")
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Dim manuscript As Manuscript = Sample("Looked-up manuscript")
                    manuscript.Metadata = New ManuscriptMetadata With {.Doi = CrossrefWork.Doi}
                    Dim draftBytes As Byte()

                    ' Session one: the lookup, then the session ends unsaved.
                    Using first As New PageBoard()
                        authorDirectory = first.AuthorDirectory
                        Directory.CreateDirectory(authorDirectory)
                        Call New AuthorLibraryRepository(authorDirectory).Save(New AuthorLibraryData())
                        first.UseRepository(root)
                        first.Prepare(manuscript)
                        Assert.IsTrue(first.SaveNow(), "The library is on disk.")
                        first.Open(manuscript)

                        Using dialogs As New DialogWatcher()
                            ApplyCrossrefAuthors(first.Editor)
                            Assert.AreEqual(2, PageLibrary(first.Editor).Authors.Count, "The lookup added both people to the page's library.")
                            Assert.IsTrue(first.WriteRecoveryDraft("Windows was shutting down"), "The draft is written.")
                            CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                        End Using

                        draftBytes = File.ReadAllBytes(draftPath)
                        Assert.AreEqual(0, New AuthorLibraryRepository(authorDirectory).Load().Authors.Count, "The looked-up people never reached authors.json.")
                        first.SetPrompt(Function(title) DialogResult.No)
                        first.Close()
                    End Using

                    Directory.CreateDirectory(Path.GetDirectoryName(draftPath))
                    File.WriteAllBytes(draftPath, draftBytes)

                    ' Session two: the next start offers the draft; Yes, then Save.
                    ' Its author library is the one session one left: empty. (The
                    ' board removes its own author folder when disposed.)
                    Using second As New PageBoard()
                        authorDirectory = second.AuthorDirectory
                        Directory.CreateDirectory(authorDirectory)
                        Dim disk As New AuthorLibraryRepository(authorDirectory)
                        disk.Save(New AuthorLibraryData())
                        second.UseRepository(root)
                        second.Prepare(New ManuscriptRepository(dataDirectory, Path.Combine(root, "library")).Load().ToArray())
                        Dim authorsBefore As Byte() = File.ReadAllBytes(disk.DataFilePath)
                        Dim manuscriptsBefore As Byte() = File.ReadAllBytes(Path.Combine(dataDirectory, "manuscripts.json"))
                        Dim offered As Integer = 0
                        second.recoveryDraftPrompt = Function(title)
                                                         offered += 1
                                                         Return DialogResult.Yes
                                                     End Function
                        Dim shown As List(Of String)

                        Using dialogs As New DialogWatcher()
                            GetType(Form1).GetMethod("OfferRecoveryDraft", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(second, Array.Empty(Of Object)())
                            Application.DoEvents()
                            Assert.AreEqual(1, offered, "The draft is offered once.")
                            Assert.AreEqual("Manuscript", second.PageName, "Yes opens the manuscript page.")
                            Assert.IsFalse(File.Exists(draftPath), "The draft file goes once it is opened.")
                            CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(disk.DataFilePath), "Recovery itself writes nothing to authors.json.")
                            CollectionAssert.AreEqual(manuscriptsBefore, File.ReadAllBytes(Path.Combine(dataDirectory, "manuscripts.json")), "Recovery itself writes nothing to manuscripts.json.")

                            Dim authorsList As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(second.Editor), ListBox)
                            shown = authorsList.Items.Cast(Of Object)().Select(Function(item) authorsList.GetItemText(item)).ToList()

                            second.PressCommandKey(Keys.Control Or Keys.S)
                            Assert.AreEqual(1, second.SaveCount, "The page saved.")
                            CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                        End Using

                        Dim saved As Manuscript = New ManuscriptRepository(dataDirectory, Path.Combine(root, "library")).Load().Single()
                        Assert.AreEqual(2, saved.Authors.Count, "The saved manuscript names both looked-up people.")
                        Dim library As AuthorLibraryData = disk.Load()
                        For Each link As ManuscriptAuthor In saved.Authors
                            Assert.IsTrue(library.Authors.Any(Function(person) person.Id = link.AuthorId),
                                "Every person the saved manuscript names is in authors.json. Missing id " & link.AuthorId.ToString() &
                                "; on disk: " & library.Authors.Count.ToString() & " people. The Authors tab showed before Save: " & String.Join(" | ", shown))
                            For Each affiliationId As Guid In link.AffiliationIds
                                Assert.IsTrue(library.Affiliations.Any(Function(affiliation) affiliation.Id = affiliationId), "Every affiliation the saved manuscript names is in authors.json. Missing id " & affiliationId.ToString() & ".")
                            Next
                        Next
                        Assert.IsFalse(shown.Any(Function(text) text.Contains("[Missing author record]")), "The recovered page names everyone: " & String.Join(" | ", shown))

                        second.SetPrompt(Function(title) DialogResult.No)
                        second.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                    StorageEnvironment.RootOverride = Nothing
                    For Each folder As String In {root, authorDirectory}
                        Try
                            If folder IsNot Nothing Then Directory.Delete(folder, True)
                        Catch
                        End Try
                    Next
                End Try
            End Sub)
    End Sub

    ' A recovered draft's people, then Discard (#361): neither file changes,
    ' and the page holds only what is on disk with nothing left to save.
    <TestMethod>
    Public Sub DiscardAfterOpeningARecoveredDraftWritesNoneOfItsPeople()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData())
                Dim left As AuthorLibraryData = RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Dim authorsBefore As Byte() = File.ReadAllBytes(disk.DataFilePath)
                        Dim manuscriptsBefore As Byte() = File.ReadAllBytes(ManuscriptsFile(root))
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        Assert.AreEqual(2, PageLibrary(board.Editor).Authors.Count, "The recovered page holds both people.")

                        board.Discard()

                        CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(disk.DataFilePath), "authors.json is unchanged.")
                        CollectionAssert.AreEqual(manuscriptsBefore, File.ReadAllBytes(ManuscriptsFile(root)), "manuscripts.json is unchanged.")
                        Assert.AreEqual(0, PageLibrary(board.Editor).Authors.Count, "The page's library holds only what is on disk: no one.")
                        Assert.AreEqual(0, PageLibrary(board.Editor).Affiliations.Count, "No affiliation either.")
                        Assert.IsFalse(LibraryDirty(board.Editor), "Nothing is left to save in the library.")
                        Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Closing now has nothing to ask about.")
                    End Sub)
                Assert.AreEqual(0, left.Authors.Count, "No one reached authors.json.")
            End Sub)
    End Sub

    ' No to the offer (#361): the draft goes, both files are byte-identical
    ' and the page is not opened.
    <TestMethod>
    Public Sub NoToARecoveredDraftWithPeopleWritesNothing()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData())
                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Dim authorsBefore As Byte() = File.ReadAllBytes(disk.DataFilePath)
                        Dim manuscriptsBefore As Byte() = File.ReadAllBytes(ManuscriptsFile(root))

                        Assert.AreEqual(1, OfferDraft(board, DialogResult.No), "The draft is offered once.")

                        Assert.IsFalse(File.Exists(RecoveryDraftFile(root)), "No discards the draft.")
                        Assert.AreEqual("Board", board.PageName, "The page is not opened.")
                        CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(disk.DataFilePath), "authors.json is unchanged.")
                        CollectionAssert.AreEqual(manuscriptsBefore, File.ReadAllBytes(ManuscriptsFile(root)), "manuscripts.json is unchanged.")
                    End Sub)
            End Sub)
    End Sub

    ' A second session ends before Save too (#361): its draft carries the
    ' people again, and a third session that opens it and saves writes them.
    <TestMethod>
    Public Sub ARecoveredDraftsPeopleSurviveASecondSessionEndingBeforeSave()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData())
                library = RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        Assert.IsTrue(board.WriteRecoveryDraft("Windows was shutting down"), "The second draft is written.")
                        Dim again As RecoveryDraft = New RecoveryDraftStore(Path.Combine(root, "data")).Read()
                        Assert.IsNotNull(again?.UnsavedLibrary, "The second draft carries the library records.")
                        Assert.AreEqual(2, again.UnsavedLibrary.Authors.Count, "It carries both people.")
                        Assert.AreEqual(1, again.UnsavedLibrary.Affiliations.Count, "It carries the affiliation.")
                    End Sub)
                Assert.AreEqual(0, library.Authors.Count, "Nothing reached authors.json in the second session.")
                Assert.IsTrue(File.Exists(RecoveryDraftFile(root)), "The second session left its draft.")

                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The second draft is offered once.")
                        SavePage(board)
                        AssertEveryoneIsOnDisk(SavedManuscript(root), disk)
                        Assert.AreEqual(2, disk.Load().Authors.Count, "authors.json holds both people.")
                        Assert.AreEqual(1, disk.Load().Affiliations.Count, "authors.json holds the affiliation.")
                    End Sub)
            End Sub)
    End Sub

    ' A person the draft carries is on disk by the next start under the same
    ' id and another name (#361): the disk's record wins, on the Authors tab
    ' and at Save, and no second record with that id is written.
    <TestMethod>
    Public Sub APersonAlreadyOnDiskUnderTheDraftsIdKeepsTheDiskRecord()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData())
                Dim carried As AuthorRecord = New RecoveryDraftStore(Path.Combine(root, "data")).Read().UnsavedLibrary.Authors(0)
                Dim onDisk As New AuthorRecord With {.Id = carried.Id, .GivenName = "Disk", .FamilyName = "Kept"}
                library.Authors.Add(onDisk)

                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        Dim shown As List(Of String) = AuthorsShown(board.Editor)
                        Assert.IsTrue(shown.Any(Function(text) text.Contains(onDisk.DisplayName)), "The Authors tab shows the disk's name: " & String.Join(" | ", shown))
                        Assert.IsFalse(shown.Any(Function(text) text.Contains(carried.DisplayName)), "The draft's name for that id is not shown: " & String.Join(" | ", shown))
                        Assert.AreEqual(1, PageLibrary(board.Editor).Authors.Where(Function(person) person.Id = carried.Id).Count(), "The page holds one record with that id.")

                        SavePage(board)
                        Dim saved As AuthorLibraryData = disk.Load()
                        Assert.AreEqual(2, saved.Authors.Count, "authors.json holds exactly the two people.")
                        Assert.AreEqual("Kept", saved.Authors.Single(Function(person) person.Id = carried.Id).FamilyName, "The record with that id keeps the disk's name.")
                        AssertEveryoneIsOnDisk(SavedManuscript(root), disk)
                    End Sub)
            End Sub)
    End Sub

    ' Authors & Affiliations after a recovered draft is opened (#361, #354):
    ' its reload keeps the draft's people as the page's own, and Save
    ' writes them.
    <TestMethod>
    Public Sub ARecoveredDraftsPeopleSurviveManageLibraryAndAreSaved()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData())
                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        CloseManageLibrary(board.Editor)
                        Assert.AreEqual(2, PageLibrary(board.Editor).Authors.Count, "Both people survive the reload.")
                        Assert.AreEqual(1, PageLibrary(board.Editor).Affiliations.Count, "The affiliation survives the reload.")

                        SavePage(board)
                        AssertEveryoneIsOnDisk(SavedManuscript(root), disk)
                    End Sub)
            End Sub)
    End Sub

    ' The page is reused (#361): a recovered draft opened and discarded,
    ' then another manuscript edited; that manuscript's draft carries no
    ' library records and names none of the first draft's people.
    <TestMethod>
    Public Sub AnotherManuscriptsDraftAfterADiscardedRecoveryCarriesNoneOfItsPeople()
        WithRecoverySessions(
            Sub(root)
                Dim library As AuthorLibraryData = LookupThenSessionEnds(root, New AuthorLibraryData(), others:={Sample("Second manuscript")})
                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        board.Discard()
                        board.Open(board.Library.Single(Function(item) item.Title = "Second manuscript"))
                        board.Field("Title").Text = "Second manuscript, edited"

                        Assert.IsTrue(board.WriteRecoveryDraft("Windows was shutting down"), "The draft is written.")

                        Dim json As String = File.ReadAllText(RecoveryDraftFile(root))
                        Assert.IsFalse(json.Contains("UnsavedLibrary"), "The draft carries no library records.")
                        Assert.IsFalse(json.Contains("""Lookup""") OrElse json.Contains("""Crossref"""), "The draft names none of the first draft's people.")
                        StringAssert.Contains(json, "Second manuscript, edited")
                        Dim draft As RecoveryDraft = New RecoveryDraftStore(Path.Combine(root, "data")).Read()
                        Assert.IsNull(draft.UnsavedLibrary, "The draft reads with no library records.")
                        Assert.AreEqual(0, draft.Manuscript.Authors.Count, "The second manuscript names no one.")
                    End Sub)
            End Sub)
    End Sub

    ' With a person already on disk (#361), the draft carries only what the
    ' lookup added: the two new people and the new affiliation.
    <TestMethod>
    Public Sub ADraftCarriesOnlyThePeopleThePageAdded()
        WithRecoverySessions(
            Sub(root)
                Dim seed As New AuthorLibraryData()
                Dim existing As New AuthorRecord With {.GivenName = "Disk", .FamilyName = "Person"}
                Dim existingAffiliation As New AffiliationRecord With {.Institution = "Disk University", .Country = "USA"}
                seed.Authors.Add(existing)
                seed.Affiliations.Add(existingAffiliation)

                LookupThenSessionEnds(root, seed)

                Dim draft As RecoveryDraft = New RecoveryDraftStore(Path.Combine(root, "data")).Read()
                Assert.IsNotNull(draft?.UnsavedLibrary, "The draft carries library records.")
                CollectionAssert.AreEquivalent(New List(Of String) From {"Lookup", "Crossref"}, draft.UnsavedLibrary.Authors.Select(Function(person) person.FamilyName).ToList(), "Only the two looked-up people.")
                Assert.AreEqual(1, draft.UnsavedLibrary.Affiliations.Count, "Only the looked-up affiliation.")
                Assert.AreEqual("Example Institute", draft.UnsavedLibrary.Affiliations(0).Institution)
                Assert.IsFalse(draft.UnsavedLibrary.Authors.Any(Function(person) person.Id = existing.Id), "Not the person on disk.")
                Assert.IsFalse(draft.UnsavedLibrary.Affiliations.Any(Function(affiliation) affiliation.Id = existingAffiliation.Id), "Not the affiliation on disk.")
            End Sub)
    End Sub

    ' A tag colour chosen on the page before the session ended (#361) is
    ' saved after recovery and Save.
    <TestMethod>
    Public Sub ATagColourChosenBeforeTheSessionEndedIsSavedAfterRecovery()
        WithRecoverySessions(
            Sub(root)
                Dim manuscript As Manuscript = Sample("Tagged manuscript")
                manuscript.Tags = New List(Of String) From {"teaching"}
                Dim library As AuthorLibraryData = RunRecoverySession(root, New AuthorLibraryData(), {manuscript},
                    Sub(board, disk)
                        board.Open(manuscript)
                        ChooseTagColour(board.Editor, "teaching", TagPalette.Violet)
                        Assert.IsTrue(board.WriteRecoveryDraft("Windows was shutting down"), "The draft is written.")
                    End Sub)
                Assert.AreEqual(0, library.TagColors.Count, "The colour never reached authors.json.")

                RunRecoverySession(root, library, Nothing,
                    Sub(board, disk)
                        Assert.AreEqual(1, OfferDraft(board, DialogResult.Yes), "The draft is offered once.")
                        SavePage(board)
                        Dim saved As TagColor = disk.Load().TagColors.SingleOrDefault(Function(entry) entry.Tag = "teaching")
                        Assert.IsNotNull(saved, "The tag's colour is saved.")
                        Assert.AreEqual(TagPalette.Violet, saved.Color, "The colour chosen before the session ended is saved.")
                    End Sub)
            End Sub)
    End Sub

    ' Sessions of the main window on files of the test's own under root,
    ' with Crossref answered by the scripted work (#361).
    Private Shared Sub WithRecoverySessions(steps As Action(Of String))
        RunOnStaThread(
            Sub()
                Dim network As New CrossrefWork("Example Institute")
                Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))
                StorageEnvironment.RootOverride = Path.Combine(root, "storage")
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    steps(root)
                Finally
                    OnlineAccess.ResetForTests()
                    StorageEnvironment.RootOverride = Nothing
                    Try
                        Directory.Delete(root, True)
                    Catch
                    End Try
                End Try
            End Sub)
    End Sub

    ' One session of the main window on the files under root. Its author
    ' library starts as the one given; its manuscripts are those given,
    ' saved first, or else what manuscripts.json holds. The steps run with
    ' no message box allowed. A draft on disk when they end is put back
    ' after the window closes (its close removes a draft it wrote), as a
    ' session that ended without closing leaves it. Returns the author
    ' library on disk as the session left it.
    Private Shared Function RunRecoverySession(root As String, library As AuthorLibraryData, records As Manuscript(), steps As Action(Of PageBoard, AuthorLibraryRepository)) As AuthorLibraryData
        Dim dataDirectory As String = Path.Combine(root, "data")
        Dim draftPath As String = RecoveryDraftFile(root)
        Dim draftBytes As Byte() = Nothing
        Dim left As AuthorLibraryData
        Using board As New PageBoard()
            Directory.CreateDirectory(board.AuthorDirectory)
            Dim disk As New AuthorLibraryRepository(board.AuthorDirectory)
            disk.Save(library)
            board.UseRepository(root)
            If records IsNot Nothing Then
                board.Prepare(records)
                Assert.IsTrue(board.SaveNow(), "The library is on disk.")
            Else
                board.Prepare(New ManuscriptRepository(dataDirectory, Path.Combine(root, "library")).Load().ToArray())
            End If

            Using dialogs As New DialogWatcher()
                steps(board, disk)
                CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
            End Using

            If File.Exists(draftPath) Then draftBytes = File.ReadAllBytes(draftPath)
            left = disk.Load()
            board.SetPrompt(Function(title) DialogResult.No)
            board.Close()
        End Using
        If draftBytes IsNot Nothing AndAlso Not File.Exists(draftPath) Then
            Directory.CreateDirectory(Path.GetDirectoryName(draftPath))
            File.WriteAllBytes(draftPath, draftBytes)
        End If
        Return left
    End Function

    ' Session one: a manuscript with the scripted DOI (and any others) is
    ' saved, the page looks its people up, and the session ends unsaved
    ' with a recovery draft. Returns the author library it left on disk.
    Private Shared Function LookupThenSessionEnds(root As String, seed As AuthorLibraryData, Optional others As Manuscript() = Nothing) As AuthorLibraryData
        Dim manuscript As Manuscript = Sample("Looked-up manuscript")
        manuscript.Metadata = New ManuscriptMetadata With {.Doi = CrossrefWork.Doi}
        Dim records As New List(Of Manuscript) From {manuscript}
        If others IsNot Nothing Then records.AddRange(others)
        Dim seededPeople As Integer = seed.Authors.Count
        Dim left As AuthorLibraryData = RunRecoverySession(root, seed, records.ToArray(),
            Sub(board, disk)
                board.Open(manuscript)
                ApplyCrossrefAuthors(board.Editor)
                Assert.AreEqual(seededPeople + 2, PageLibrary(board.Editor).Authors.Count, "The lookup added both people to the page's library.")
                Assert.IsTrue(board.WriteRecoveryDraft("Windows was shutting down"), "The draft is written.")
            End Sub)
        Assert.AreEqual(seededPeople, left.Authors.Count, "The looked-up people never reached authors.json.")
        Assert.IsTrue(File.Exists(RecoveryDraftFile(root)), "The draft is on disk for the next start.")
        Return left
    End Function

    ' The next start's offer of the draft on disk, answered as given.
    ' Returns how many times it asked.
    Private Shared Function OfferDraft(board As PageBoard, answer As DialogResult) As Integer
        Dim offered As Integer = 0
        board.recoveryDraftPrompt = Function(title)
                                        offered += 1
                                        Return answer
                                    End Function
        GetType(Form1).GetMethod("OfferRecoveryDraft", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Array.Empty(Of Object)())
        Application.DoEvents()
        Return offered
    End Function

    Private Shared Function RecoveryDraftFile(root As String) As String
        Return Path.Combine(root, "data", "recovery", RecoveryDraftStore.FileName)
    End Function

    Private Shared Function ManuscriptsFile(root As String) As String
        Return Path.Combine(root, "data", "manuscripts.json")
    End Function

    ' The looked-up manuscript as manuscripts.json holds it.
    Private Shared Function SavedManuscript(root As String) As Manuscript
        Return New ManuscriptRepository(Path.Combine(root, "data"), Path.Combine(root, "library")).Load().Single(Function(item) item.Title = "Looked-up manuscript")
    End Function

    ' The Authors tab's rows as shown.
    Private Shared Function AuthorsShown(editor As EditManuscriptForm) As List(Of String)
        Dim authorsList As ListBox = DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), ListBox)
        Return authorsList.Items.Cast(Of Object)().Select(Function(item) authorsList.GetItemText(item)).ToList()
    End Function

    Private Shared Function LibraryDirty(editor As EditManuscriptForm) As Boolean
        Return CBool(GetType(EditManuscriptForm).GetProperty("_authorLibraryDirty", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor))
    End Function

    ' The page shows a manuscript with the scripted DOI; DOI & Crossref
    ' Metadata has added two new people to it and to the page's author
    ' library (the second with a new affiliation), and nothing is saved.
    ' The author library starts as seed; Authors & Affiliations, which reads
    ' its own files, reads them under a root of the test's own.
    Private Shared Sub WithCrossrefPeopleUnsaved(seed As AuthorLibraryData, steps As Action(Of PageBoard, Manuscript, AuthorLibraryRepository, String))
        RunOnStaThread(
            Sub()
                Dim network As New CrossrefWork("Example Institute")
                Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))
                StorageEnvironment.RootOverride = Path.Combine(root, "storage")
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Directory.CreateDirectory(board.AuthorDirectory)
                        Dim disk As New AuthorLibraryRepository(board.AuthorDirectory)
                        disk.Save(seed)
                        Dim seededPeople As Integer = seed.Authors.Count
                        Dim seededAffiliations As Integer = seed.Affiliations.Count

                        Dim manuscript As Manuscript = Sample("Looked-up manuscript")
                        manuscript.Metadata = New ManuscriptMetadata With {.Doi = CrossrefWork.Doi}
                        board.Prepare(manuscript)
                        board.Open(manuscript)

                        Using dialogs As New DialogWatcher()
                            ApplyCrossrefAuthors(board.Editor)
                            Assert.AreEqual(1, network.Requests, "One scripted Crossref request.")
                            Dim working As Manuscript = DirectCast(GetType(EditManuscriptForm).GetProperty("_workingManuscript", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), Manuscript)
                            Assert.AreEqual(2, working.Authors.Count, "The lookup put both people on the manuscript.")
                            Assert.AreEqual(seededPeople + 2, PageLibrary(board.Editor).Authors.Count, "The lookup added both people to the page's library.")
                            Assert.AreEqual(seededAffiliations + 1, PageLibrary(board.Editor).Affiliations.Count, "The lookup added the affiliation to the page's library.")
                            Assert.AreEqual(seededPeople, disk.Load().Authors.Count, "Nothing is on disk before Save.")

                            steps(board, manuscript, disk, root)

                            CollectionAssert.AreEqual(New List(Of String)(), dialogs.Closed, "No message box opened.")
                        End Using

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                    StorageEnvironment.RootOverride = Nothing
                    Try
                        Directory.Delete(root, True)
                    Catch
                    End Try
                End Try
            End Sub)
    End Sub

    ' DOI & Crossref Metadata: Look Up, then Apply Selected with the authors
    ' box ticked.
    Private Shared Sub ApplyCrossrefAuthors(editor As EditManuscriptForm)
        Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim lookups As Integer = 0
        Using lookup As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of CrossrefLookupForm)(
            Sub(dialog)
                lookups += 1
                Dim field As Func(Of String, Object) = Function(name) GetType(CrossrefLookupForm).GetField(name, flags).GetValue(dialog)
                DirectCast(field("btnLookup"), Button).PerformClick()
                Dim apply As Button = DirectCast(field("btnApply"), Button)
                Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
                While (field("_suggestion") Is Nothing OrElse Not apply.Enabled) AndAlso waited.Elapsed < TimeSpan.FromSeconds(10)
                    Application.DoEvents()
                    Thread.Sleep(1)
                End While
                If field("_suggestion") Is Nothing OrElse Not apply.Enabled Then
                    dialog.DialogResult = DialogResult.Cancel
                    Return
                End If
                DirectCast(field("chkAuthors"), CheckBox).Checked = True
                apply.PerformClick()
            End Sub)
            GetType(EditManuscriptForm).GetMethod("OpenCrossrefMetadata", flags).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, lookups, "DOI & Crossref Metadata opened once.")
        Assert.IsTrue(CBool(GetType(EditManuscriptForm).GetProperty("_authorLibraryDirty", flags).GetValue(editor)), "The page marks its author library changed.")
    End Sub

    ' Manage Library (its Click handler); in the window, during runs, and
    ' the window is closed.
    Private Shared Sub CloseManageLibrary(editor As EditManuscriptForm, Optional during As Action = Nothing)
        Dim opened As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorLibraryForm)(
            Sub(dialog)
                opened += 1
                during?.Invoke()
                dialog.Close()
            End Sub)
            GetType(EditManuscriptForm).GetMethod("ManageAuthorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "Authors & Affiliations opened once.")
    End Sub

    ' Submission Readiness, opened and closed with Cancel.
    Private Shared Sub CloseSubmissionReadiness(editor As EditManuscriptForm)
        Dim opened As Integer = 0
        Using readiness As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of ManuscriptReadinessForm)(
            Sub(dialog)
                opened += 1
                dialog.DialogResult = DialogResult.Cancel
            End Sub)
            GetType(EditManuscriptForm).GetMethod("OpenSubmissionReadiness", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(editor, New Object() {Nothing, EventArgs.Empty})
            Application.DoEvents()
        End Using
        Assert.AreEqual(1, opened, "Submission Readiness opened once.")
    End Sub

    ' Ctrl+S, once.
    Private Shared Sub SavePage(board As PageBoard)
        board.PressCommandKey(Keys.Control Or Keys.S)
        Assert.AreEqual(1, board.SaveCount, "The page saved.")
    End Sub

    ' Every person and affiliation the saved manuscript names is in the
    ' author library on disk, and the affiliation the lookup made is among
    ' them.
    Private Shared Sub AssertEveryoneIsOnDisk(manuscript As Manuscript, disk As AuthorLibraryRepository)
        Assert.AreEqual(2, manuscript.Authors.Count, "The saved manuscript names both looked-up people.")
        Dim saved As AuthorLibraryData = disk.Load()
        For Each link As ManuscriptAuthor In manuscript.Authors
            Assert.IsTrue(saved.Authors.Any(Function(person) person.Id = link.AuthorId),
                "Every person the saved manuscript names is on disk. Missing id " & link.AuthorId.ToString() & "; on disk: " & String.Join(", ", saved.Authors.Select(Function(person) person.DisplayName)) & ".")
            For Each affiliationId As Guid In link.AffiliationIds
                Assert.IsTrue(saved.Affiliations.Any(Function(affiliation) affiliation.Id = affiliationId), "Every affiliation the saved manuscript names is on disk. Missing id " & affiliationId.ToString() & ".")
            Next
        Next
        Assert.AreEqual(1, manuscript.Authors.Sum(Function(link) link.AffiliationIds.Count), "The looked-up affiliation is on the manuscript.")
    End Sub

    ' The page's copy of the reusable author library.
    Private Shared Function PageLibrary(editor As EditManuscriptForm) As AuthorLibraryData
        Return DirectCast(GetType(EditManuscriptForm).GetProperty("_authorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), AuthorLibraryData)
    End Function

    ' A colour from a tag chip's menu on the page.
    Private Shared Sub ChooseTagColour(editor As EditManuscriptForm, tag As String, colour As TagPalette)
        Dim chip As TagChipButton = Descendants(editor).OfType(Of TagChipButton)().Single(Function(item) item.TagText = tag)
        Dim colours As ToolStripMenuItem = chip.ContextMenuStrip.Items.OfType(Of ToolStripMenuItem)().Single(Function(item) item.Text = "Color")
        colours.DropDownItems.OfType(Of ToolStripMenuItem)().Single(Function(item) item.Text = colour.ToString()).PerformClick()
        Application.DoEvents()
    End Sub

    Private Shared Function MenuItem(card As Control, text As String) As ToolStripItem
        Return card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().SingleOrDefault(Function(item) item.Text = text)
    End Function

    ' The Deadlines page draws its rows, headings and Done line in one list
    ' (#133); what it shows is read as a screen reader reads it.
    Private Shared Function DeadlineRows(board As PageBoard) As List(Of DeadlineRow)
        Return Descendants(board).OfType(Of DeadlineList)().Single().Rows.ToList()
    End Function

    Private Shared Function DeadlineChildren(board As PageBoard) As List(Of AccessibleObject)
        Dim list As AccessibleObject = Descendants(board).OfType(Of DeadlineList)().Single().AccessibilityObject
        Return Enumerable.Range(0, list.GetChildCount()).Select(Function(index) list.GetChild(index)).ToList()
    End Function

    ' The headings and the sentence of an empty list.
    Private Shared Function DeadlineTexts(board As PageBoard) As List(Of String)
        Return DeadlineChildren(board).Where(Function(child) child.Role = AccessibleRole.StaticText).Select(Function(child) child.Name).ToList()
    End Function

    ' The Done line's one link.
    Private Shared Function DoneLink(board As PageBoard) As AccessibleObject
        Return DeadlineChildren(board).Single(Function(child) child.Role = AccessibleRole.Link)
    End Function

    Private Shared Sub ClickDoneLink(board As PageBoard)
        DoneLink(board).DoDefaultAction()
        Application.DoEvents()
    End Sub

    Private Shared Sub ClickControl(control As Control)
        GetType(Control).GetMethod("OnClick", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(control, New Object() {EventArgs.Empty})
        Application.DoEvents()
    End Sub

    Private Shared Sub MoveFirstResponseDown(board As PageBoard)
        Dim matrix As ReviewerResponseMatrixForm = Descendants(board.Editor).OfType(Of ReviewerResponseMatrixForm)().Single()
        Dim list As ListBox = DirectCast(GetType(ReviewerResponseMatrixForm).GetField("lstResponses", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(matrix), ListBox)
        list.SelectedIndex = 0
        GetType(ReviewerResponseMatrixForm).GetMethod("MoveItem", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(matrix, New Object() {1})
        Application.DoEvents()
    End Sub

    ' Two submissions: a desk rejection, then a major revision with two
    ' reviewer comments.
    Private Shared Function WithRoute() As Manuscript
        Dim manuscript As Manuscript = Sample("Routed manuscript")
        manuscript.CurrentStage = PaperStage.Revision
        Dim first As New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)}
        first.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
        Dim second As New JournalSubmission With {.JournalName = "Second Journal", .SubmittedDate = New DateTime(2026, 3, 1)}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 4, 2)}
        second.Decisions.Add(decision)
        second.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample."})
        second.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 2", .CommentText = "Share the data."})
        manuscript.Submissions.Add(first)
        manuscript.Submissions.Add(second)
        Return manuscript
    End Function

    ' Children report Visible only while their form is shown.
    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Function Sample(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    ' The real board and manuscript page, on in-memory samples. Saving is
    ' counted instead of written, and the reusable author library is a
    ' throwaway directory.
    Private NotInheritable Class NoNetwork
        Inherits Net.Http.HttpMessageHandler

        Public Requests As Integer

        Protected Overrides Function SendAsync(request As Net.Http.HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of Net.Http.HttpResponseMessage)
            Interlocked.Increment(Requests)
            Throw New InvalidOperationException("No request was expected.")
        End Function
    End Class

    ' Crossref's answer for one DOI: a work with two authors, neither of
    ' them in the reusable library; given an affiliation, the second
    ' author has it.
    Private NotInheritable Class CrossrefWork
        Inherits Net.Http.HttpMessageHandler

        Public Const Doi As String = "10.5555/paperroute.crossref"

        Public Requests As Integer

        Private ReadOnly _affiliation As String

        Public Sub New(Optional affiliation As String = Nothing)
            _affiliation = affiliation
        End Sub

        Protected Overrides Function SendAsync(request As Net.Http.HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of Net.Http.HttpResponseMessage)
            Interlocked.Increment(Requests)
            Dim affiliation As String = If(_affiliation Is Nothing, String.Empty, ", ""affiliation"": [{""name"": """ & _affiliation & """}]")
            Dim body As String =
                "{""message"": {""DOI"": """ & Doi & """, ""title"": [""Looked-up manuscript""], " &
                """author"": [{""given"": ""Ada"", ""family"": ""Lookup""}, {""given"": ""Grace"", ""family"": ""Crossref""" & affiliation & "}]}}"
            Return Task.FromResult(New Net.Http.HttpResponseMessage(Net.HttpStatusCode.OK) With {.Content = New Net.Http.StringContent(body), .RequestMessage = request})
        End Function
    End Class

    Private NotInheritable Class PageBoard
        Inherits Form1

        Private ReadOnly _authorDirectory As String =
            Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))

        Public SaveCount As Integer

        ' Where the page's reusable author library is read and written.
        Public ReadOnly Property AuthorDirectory As String
            Get
                Return _authorDirectory
            End Get
        End Property

        ' Saves are written only after UseRepository pointed them at a
        ' throwaway directory.
        Private _realSaves As Boolean
        Private _managedDirectory As String

        Public ReadOnly BackupPath As String =
            Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N") & ".zip")

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function CreateManuscriptEditor(manuscript As Manuscript) As EditManuscriptForm
            Directory.CreateDirectory(_authorDirectory)
            Return New EditManuscriptForm(manuscript, Library, New AuthorLibraryRepository(_authorDirectory), pageMode:=True)
        End Function

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            If _realSaves Then Return MyBase.SaveManuscripts()
            Return True
        End Function

        ' The save a dialog the window opens is given (Check for
        ' Publications, Fill Blanks).
        Public Function SaveNow() As Boolean
            Return SaveManuscripts()
        End Function

        ' Work of the test's own behind the disabled window, as a save,
        ' backup or restore runs (#132).
        Public Function RunWork(status As String, work As Action) As Boolean
            Return CBool(GetType(Form1).GetMethod("RunLibraryWork", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, New Object() {status, work, Nothing}))
        End Function

        ' A restart is counted, with whether the window was handing over
        ' when it was asked for; nothing restarts the test host.
        Public Restarts As Integer
        Public HandingOverAtRestart As Boolean

        Protected Overrides Sub RestartOnTheLibraryOnDisk()
            Restarts += 1
            HandingOverAtRestart = CBool(GetType(Form1).GetField("handingOverAfterRestore", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me))
        End Sub

        ' Yes to the Restart Required question after a new appearance.
        Public Sub RestartForAppearance()
            CallPrivate("RestartForNewAppearance")
            Application.DoEvents()
        End Sub

        ' The library is written to a throwaway directory, as the app writes it.
        Public Sub UseRepository(root As String)
            _managedDirectory = Path.Combine(root, "library")
            GetType(Form1).GetField("repository", BindingFlags.Instance Or BindingFlags.NonPublic).
                SetValue(Me, New ManuscriptRepository(Path.Combine(root, "data"), _managedDirectory))
            _realSaves = True
        End Sub

        Public Sub BackUp()
            CallPrivate("BackupLibrary", Nothing, EventArgs.Empty)
            Application.DoEvents()
        End Sub

        Public Sub Restore()
            CallPrivate("RestoreLibraryBackup", Nothing, EventArgs.Empty)
            Application.DoEvents()
        End Sub

        Protected Overrides Function ChooseBackupPath() As String
            Return BackupPath
        End Function

        ' Fails loudly without UseRepository rather than touch the real
        ' managed library.
        Protected Overrides Function CreateBackupService() As PortableBackupService
            Return New PortableBackupService(_managedDirectory)
        End Function

        Public Function OfferUpdate(interactive As Boolean) As Boolean
            Dim operation As Task(Of Boolean) =
                DirectCast(GetType(Form1).GetMethod("OfferUpdateAsync", BindingFlags.Instance Or BindingFlags.NonPublic).
                    Invoke(Me, New Object() {interactive}), Task(Of Boolean))
            PumpUntilComplete(operation)
            Return operation.Result
        End Function

        Protected Overrides Function LoadAuthorLibrary() As Boolean
            Return True
        End Function

        Private _authorEditor As Form

        Protected Overrides Function CreateAuthorLibraryEditor() As Form
            _authorEditor = New Form()
            Return _authorEditor
        End Function

        Public ReadOnly Property AuthorEditorShown As Boolean
            Get
                Return _authorEditor IsNot Nothing AndAlso Not _authorEditor.IsDisposed AndAlso _authorEditor.Visible
            End Get
        End Property

        Public ReadOnly Property Welcome As Control
            Get
                Return DirectCast(GetType(Form1).GetField("boardWelcome", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
            End Get
        End Property

        Public Sub Render()
            CallPrivate("RenderManuscripts")
            Application.DoEvents()
        End Sub

        Public Sub SetAuthors(ParamArray authors As AuthorRecord())
            Dim library As New AuthorLibraryData()
            library.Authors.AddRange(authors)
            GetType(Form1).GetField("authorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, library)
        End Sub

        Public Sub Prepare(ParamArray samples As Manuscript())
            CallPrivate("BuildInterface")
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, samples.ToList())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), List(Of Manuscript))
            End Get
        End Property

        ' As a restore leaves it: the library's records replaced, then a render.
        Public Sub ReplaceLibrary(ParamArray records As Manuscript())
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, records.ToList())
            Render()
        End Sub

        ' The cards on the Board's three shelves.
        Public Function Cards() As List(Of RoundedPanel)
            Return {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.
                SelectMany(Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control).Controls.OfType(Of RoundedPanel)()).
                ToList()
        End Function

        Public Function Card(title As String) As RoundedPanel
            Return Cards().Single(Function(item) item.AccessibleName = title)
        End Function

        Public Sub Open(manuscript As Manuscript)
            CallPrivate("OpenManuscript", manuscript, Nothing)
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Editor As EditManuscriptForm
            Get
                Return DirectCast(GetType(Form1).GetField("manuscriptEditor", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), EditManuscriptForm)
            End Get
        End Property

        Public Function Field(name As String) As TextBox
            Return Descendants(Editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
        End Function

        Public ReadOnly Property PageName As String
            Get
                Return GetType(Form1).GetField("currentPage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me).ToString()
            End Get
        End Property

        Public Function RailPage(text As String) As RadioButton
            Dim buttons = DirectCast(GetType(Form1).GetField("railButtons", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), System.Collections.IDictionary)
            Return buttons.Values.Cast(Of RadioButton)().Single(Function(button) button.Text = text)
        End Function

        Public Sub SetDeadlinePrompts(datePrompt As Func(Of DeadlineItem, DateTime?), confirmPrompt As Func(Of String, Boolean))
            deadlineDatePrompt = datePrompt
            deadlineConfirmPrompt = confirmPrompt
        End Sub

        ' A method rather than a property, which WinForms would try to serialize.
        Public Sub SetPrompt(value As Func(Of String, DialogResult))
            unsavedChangesPrompt = value
        End Sub

        Public Sub SetErrorPrompt(value As Func(Of Exception, Boolean, DialogResult))
            unhandledErrorPrompt = value
        End Sub

        Public ReadOnly Property SaveBarVisible As Boolean
            Get
                Dim bar = DirectCast(GetType(Form1).GetField("manuscriptSaveBar", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
                Return bar IsNot Nothing AndAlso bar.Visible
            End Get
        End Property

        ' The bar's hint is its one label that shortens with an ellipsis.
        Public ReadOnly Property SaveBarHint As String
            Get
                Dim bar = DirectCast(GetType(Form1).GetField("manuscriptSaveBar", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
                Return Descendants(bar).OfType(Of Label)().Single(Function(label) label.AutoEllipsis).Text
            End Get
        End Property

        Public ReadOnly Property LibrarySaveFailed As Boolean
            Get
                Return CBool(GetType(Form1).GetField("librarySaveFailed", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me))
            End Get
        End Property

        Public ReadOnly Property StatusText As String
            Get
                Return Status.Text
            End Get
        End Property

        Public Sub SetStatus(text As String)
            Status.Text = text
        End Sub

        Private ReadOnly Property Status As Label
            Get
                Return DirectCast(GetType(Form1).GetField("lblStatus", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label)
            End Get
        End Property

        Public Sub Tick()
            CallPrivate("ManuscriptDirtyTimer_Tick", Nothing, EventArgs.Empty)
        End Sub

        ' The Library filter's debounce timer firing (#133).
        Public Sub PauseTyping()
            CallPrivate("LibraryFilterDebounceElapsed", Nothing, EventArgs.Empty)
        End Sub

        ' The Insights figures are worked out off the UI thread (#132); pumps
        ' until they are shown.
        Public Sub WaitForInsights()
            TestSupport.WaitForInsights(Me, TimeSpan.FromSeconds(30))
        End Sub

        Public Sub Discard()
            CallPrivate("DiscardManuscriptChanges")
            Application.DoEvents()
        End Sub

        Public Sub ConfirmDelete()
            CallPrivate("ManuscriptDeleteConfirmed", Nothing, EventArgs.Empty)
        End Sub

        Public Sub PressCommandKey(keyData As Keys)
            Dim message As New Message()
            GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(Me, New Object() {message, keyData})
            Application.DoEvents()
        End Sub

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub

        ' The close Windows sends at shutdown, without closing the window.
        Public Sub SimulateClosing(e As FormClosingEventArgs)
            OnFormClosing(e)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            MyBase.Dispose(disposing)
            Try
                If Directory.Exists(_authorDirectory) Then Directory.Delete(_authorDirectory, True)
                If File.Exists(BackupPath) Then File.Delete(BackupPath)
            Catch ex As IOException
            End Try
        End Sub
    End Class

    ' Closes any Windows dialog (a message box or a file dialog, class #32770)
    ' that opens on the test thread while it is alive, and keeps their titles:
    ' a production dialog then fails a test by name instead of blocking it.
    ' Given a title, it answers Yes to the Yes/No box with that title
    ' instead, which closing cannot do, and keeps its title the same way.
    Friend NotInheritable Class DialogWatcher
        Implements IDisposable

        Private ReadOnly _threadId As Integer = GetCurrentThreadId()
        Private ReadOnly _timer As New System.Windows.Forms.Timer With {.Interval = 50}
        Private ReadOnly _seen As New HashSet(Of IntPtr)()
        Private ReadOnly _answerYes As String

        Public ReadOnly Closed As New List(Of String)()
        ' The text of each dialog in Closed, at the same index; empty for a
        ' dialog that is not a message box.
        Public ReadOnly Messages As New List(Of String)()

        Public Sub New(Optional answerYes As String = Nothing)
            _answerYes = answerYes
            AddHandler _timer.Tick, AddressOf CloseDialogs
            _timer.Start()
        End Sub

        Private Sub CloseDialogs(sender As Object, e As EventArgs)
            EnumThreadWindows(_threadId,
                Function(handle, parameter)
                    Dim className As New System.Text.StringBuilder(256)
                    GetClassName(handle, className, className.Capacity)
                    If className.ToString() = "#32770" AndAlso IsWindowVisible(handle) AndAlso _seen.Add(handle) Then
                        Dim title As New System.Text.StringBuilder(256)
                        GetWindowText(handle, title, title.Capacity)
                        Closed.Add(title.ToString())
                        Dim message As New System.Text.StringBuilder(2048)
                        Dim textControl As IntPtr = GetDlgItem(handle, MessageBoxTextId)
                        If textControl <> IntPtr.Zero Then GetWindowText(textControl, message, message.Capacity)
                        Messages.Add(message.ToString())
                        If _answerYes IsNot Nothing AndAlso title.ToString() = _answerYes Then
                            PostMessage(handle, WM_COMMAND, New IntPtr(IDYES), IntPtr.Zero)
                        Else
                            PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
                        End If
                    End If
                    Return True
                End Function, IntPtr.Zero)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Dispose()
        End Sub
    End Class

    ' An update that is always available and accepted at once, then
    ' downloaded and applied without Velopack or a network. A download given
    ' a gate ends when the test completes it. BackgroundLibraryWorkTests
    ' drives one too.
    Friend NotInheritable Class ScriptedUpdate

        Public Window As Control
        Public Offered, Downloaded, Applied As Integer
        Public WindowEnabledWhileDownloading As Boolean = True
        Public CheckFailure As Exception
        Public DownloadFailure As Exception
        Public DownloadGate As Task
        Public ReadOnly Reports As New List(Of String)()

        Public Function Steps() As UpdateSteps
            Return New UpdateSteps With {
                .Manager = Function(channel) Nothing,
                .IsInstalled = Function(manager) True,
                .Check = Function(manager) If(CheckFailure Is Nothing, Task.FromResult(Available()), Task.FromException(Of UpdateInfo)(CheckFailure)),
                .Offer = Function(owner, currentVersion, availableVersion, channelName, releaseNotes)
                             Offered += 1
                             Return True
                         End Function,
                .Download = Function(manager, updateInfo, progress)
                                Downloaded += 1
                                WindowEnabledWhileDownloading = Window.Enabled
                                If DownloadFailure IsNot Nothing Then Return Task.FromException(DownloadFailure)
                                Return If(DownloadGate, Task.CompletedTask)
                            End Function,
                .Apply = Sub(manager, release) Applied += 1,
                .Report = Sub(owner, text, title, icon) Reports.Add(title & ": " & text)
            }
        End Function

        Private Shared Function Available() As UpdateInfo
            Dim release As New VelopackAsset With {
                .PackageId = "PaperRoute",
                .Version = New SemanticVersion(New Version(9, 9, 9, 0), Nothing, Nothing),
                .NotesMarkdown = "A fictional release."
            }
            Return New UpdateInfo(release, False, Nothing, Nothing)
        End Function
    End Class

    Private Shared Function BackedUpJson(backupPath As String) As String
        Using archive As ZipArchive = ZipFile.OpenRead(backupPath)
            Using reader As New StreamReader(archive.GetEntry("manuscripts.json").Open())
                Return reader.ReadToEnd()
            End Using
        End Using
    End Function

    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The update check timed out.")
    End Sub

    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action)
    End Sub

    Private Delegate Function EnumWindowsCallback(handle As IntPtr, parameter As IntPtr) As Boolean

    <System.Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsCallback, parameter As IntPtr) As Boolean
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function GetWindowText(handle As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function GetClassName(handle As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(handle As IntPtr) As Boolean
    End Function

    Private Const WM_CLOSE As UInteger = &H10
    Private Const WM_COMMAND As UInteger = &H111
    Private Const IDYES As Integer = 6
    ' The id of a message box's text control.
    Private Const MessageBoxTextId As Integer = &HFFFF

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetDlgItem(dialog As IntPtr, itemId As Integer) As IntPtr
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

End Class
