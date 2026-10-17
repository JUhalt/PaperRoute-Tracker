Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Switching between the Board and the Library, Journals and Insights pages
' (#133): what each page shows is the same however quickly it is built and
' put away.
<TestClass>
<DoNotParallelize>
Public Class PageSwitchTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
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


    Private Shared Sub AssertColumnsFitAllCells(grid As DataGridView, context As String)

        ' The width the column takes when it sizes itself to all its cells;
        ' the column is then put back as it was.
        For Each name As String In {"Stage", "Shelf", "Type", "Days"}
            Dim column As DataGridViewColumn = grid.Columns(name)
            Dim width As Integer = column.Width
            Dim mode As DataGridViewAutoSizeColumnMode = column.AutoSizeMode
            Dim resizable As DataGridViewTriState = column.Resizable
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            Dim allCells As Integer = column.Width
            column.AutoSizeMode = mode
            column.Width = width
            column.Resizable = resizable
            Assert.AreEqual(allCells, width, "The " & name & " column " & context & ", over " & grid.Rows.Count.ToString() & " rows.")
        Next

    End Sub

End Class
