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


    ' Every window the Library, its Authors view, Journals, Insights and
    ' the kept Deadlines and Import & Export pages (each visited twice, so
    ' a kept page shown again is held too) make as they open is made
    ' inside the main window, where it stays: none is made on WinForms'
    ' parking window and moved in afterwards, which costs every window
    ' twice on a slow machine (#133).
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
    ' and the page area then holds the Board alone, with the manuscript
    ' page kept hidden beside it.
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
                        ' Besides the manuscript page kept, hidden, for the
                        ' next manuscript opened (#133).
                        CollectionAssert.AreEqual(
                            {board.BoardPage},
                            host.Controls.Cast(Of Control)().Where(Function(control) control IsNot board.KeptPage).ToArray(),
                            "The page area holds the Board alone after leaving " & page.ToString() & ".")
                        Assert.IsFalse(If(board.KeptPage?.Visible, False), "The kept manuscript page is hidden.")

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
    ' left to draw once the window processed its messages.
    <TestMethod>
    Public Sub SwitchesPaintNothingSynchronously()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary =
                    LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60)

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

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

                    Dim watchSwitch As Action(Of String, Control, Action) =
                        Sub(name, within, switch)
                            Dim painted As Boolean = False
                            Using watch As New PaintWatch(within)
                                switch()
                                report.AddRange(watch.Sent().Select(Function(message) name & ": " & message))
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

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(10))

        Assert.AreEqual(0, report.Count, "Drawn while a switch ran:" & Environment.NewLine & String.Join(Environment.NewLine, report.Take(40)))

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
