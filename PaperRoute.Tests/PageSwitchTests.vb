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


    ' Every window the Library, its Authors view, Journals and Insights
    ' make as they open is made inside the main window, where it stays:
    ' none is made on WinForms' parking window and moved in afterwards,
    ' which costs every window twice on a slow machine (#133).
    <TestMethod>
    Public Sub PageWindowsAreCreatedOnceInPlace()

        Dim report As New List(Of String)()

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
                        ("Insights",
                         Sub()
                             board.NavigateTo(Form1.WorkspacePage.Insights)
                             board.WaitForInsights()
                         End Sub)
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


    ' Leaving the Library, Journals or Insights disposes the page where it
    ' is: its windows are destroyed while it is still in the page area,
    ' and the page area then holds the Board alone.
    <TestMethod>
    Public Sub LeavingAPageDisposesItInPlace()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim host As Control = board.ContentHost

                    For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Library, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Insights}

                        board.NavigateTo(page)
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

                        Assert.IsTrue(destroyed AndAlso view.IsDisposed, page.ToString() & " is disposed.")
                        Assert.AreSame(host, parentWhenDestroyed, page.ToString() & "'s windows are destroyed where they are.")
                        CollectionAssert.AreEqual(
                            {board.BoardPage},
                            host.Controls.Cast(Of Control)().ToArray(),
                            "The page area holds the Board alone after leaving " & page.ToString() & ".")

                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

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


    Private Function Hook(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If code = HCBT_CREATEWND Then
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
        End If
        Return CallNextHookEx(_hook, code, wParam, lParam)
    End Function


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
