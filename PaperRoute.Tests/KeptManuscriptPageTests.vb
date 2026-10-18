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

' The manuscript page is built once and kept (#133): built for the first
' manuscript opened, it loads each one after into the same editor and lets
' go of it once left. ManuscriptEditorReuseTests hold what it shows to a
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


    ' Built for the first manuscript opened, in place, and the same page for
    ' every manuscript opened after; none of its windows is made on WinForms'
    ' parking window, while manuscripts open or after they are left.
    <TestMethod>
    Public Sub TheKeptPageIsBuiltAtTheFirstOpenWithItsWindowsInPlace()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library60()

                Using watch As New WindowCreationWatch(),
                      board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    Assert.IsNull(board.KeptPage, "No page is built before a manuscript is opened.")

                    Dim page As Control = Nothing

                    For Each manuscript As Manuscript In board.Library.Take(3)
                        board.Open(manuscript)
                        Application.DoEvents()
                        If page Is Nothing Then page = board.KeptPage
                        Assert.AreSame(board.ContentHost, page.Parent)
                        Assert.AreSame(page, board.PageView, "Every manuscript opens on the kept page.")
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

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

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
                        current = board.ActiveControl
                        While TypeOf current Is ContainerControl AndAlso DirectCast(current, ContainerControl).ActiveControl IsNot Nothing
                            current = DirectCast(current, ContainerControl).ActiveControl
                        End While
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
