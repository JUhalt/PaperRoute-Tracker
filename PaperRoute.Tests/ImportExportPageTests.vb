Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Import & Export page (#133): its commands' buttons with their
' headings and descriptions drawn around them by one list, built once and
' kept. Each command keeps its place, its width and what a screen reader
' reads for it; the descriptions wrap at the page's width.
<TestClass>
<DoNotParallelize>
Public Class ImportExportPageTests

    Private Shared ReadOnly Commands As String() = {
        "Import Spreadsheet...", "Import BibTeX / RIS...", "ORCID Works...", "Add Manuscript...", "Get Import Template...",
        "Export Library to Excel...", "Export Library as BibTeX...", "Export Library as RIS...", "Publication & CV Export...",
        "Check for Publications...", "Fill Blanks from Crossref...",
        "Explore an Example Library...",
        "Backup Library...", "Restore Backup..."
    }

    Private Shared ReadOnly Headings As String() = {"BRING WORK IN", "TAKE WORK OUT", "KEEP RECORDS CURRENT", "LEARN AND TEACH", "KEEP IT SAFE"}

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = _root
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' The fourteen commands in order, each as wide as the widest plus 8,
    ' described by what it does, and read in order after its section's
    ' heading; the page makes at most 20 windows.
    <TestMethod>
    Public Sub ImportExportPageKeepsItsCommands()

        StaRunner.Run(
            Sub()
                Using board As New CommandBoard()
                    board.Prepare()
                    board.NavigateTo(Form1.WorkspacePage.ImportExport)
                    Application.DoEvents()

                    Dim list As CommandList = board.List()
                    Dim buttons As List(Of ActionButton) = list.Controls.OfType(Of ActionButton)().ToList()
                    CollectionAssert.AreEqual(Commands, buttons.Select(Function(button) button.Text).ToList())

                    Dim widest As Integer = {"Import Spreadsheet...", "Import BibTeX / RIS...", "ORCID Works...", "Get Import Template...", "Add Manuscript...",
                                             "Export Library to Excel...", "Export Library as BibTeX...", "Export Library as RIS...",
                                             "Publication & CV Export...", "Backup Library...", "Restore Backup..."}.
                        Max(Function(text) Math.Max(0, TextRenderer.MeasureText(text, board.Font).Width + 30))
                    Assert.IsTrue(buttons.All(Function(button) button.Width = widest + UiTheme.Px(8, board.DeviceDpi)), "Each button is as wide as the widest plus 8.")
                    CollectionAssert.AreEqual(buttons.Select(Function(button) button.AccessibleDescription).ToList(), list.Descriptions.ToList(),
                                              "Each button is described by the text drawn beside it.")

                    ' A heading, then each button and its description.
                    Dim root As AccessibleObject = list.AccessibilityObject
                    Dim children As List(Of AccessibleObject) = Enumerable.Range(0, root.GetChildCount()).Select(Function(index) root.GetChild(index)).ToList()
                    Dim expected As New List(Of String)()
                    Dim section As Integer = 0
                    For Each button As ActionButton In buttons
                        If button.Text = "Import Spreadsheet..." OrElse button.Text = "Export Library to Excel..." OrElse button.Text = "Check for Publications..." OrElse
                           button.Text = "Explore an Example Library..." OrElse button.Text = "Backup Library..." Then
                            expected.Add("StaticText: " & Headings(section))
                            section += 1
                        End If
                        expected.Add("PushButton: " & button.Text)
                        expected.Add("StaticText: " & button.AccessibleDescription)
                    Next
                    CollectionAssert.AreEqual(expected, children.Select(Function(child) child.Role.ToString() & ": " & child.Name).ToList())

                    Dim page As Control = board.Page()
                    Dim windows As Integer = 1 + ViewCatalog.Descendants(page).Count(Function(control) control.IsHandleCreated)
                    Assert.IsTrue(windows <= 20, "The page makes " & windows & " windows.")

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' Each description wraps at what the page leaves beside the buttons
    ' (never under 200 pixels), at a narrow window and at text size 1.5;
    ' rows never overlap and each button sits at the top of its row. A
    ' resize while the page is hidden is taken up when it is shown again.
    <TestMethod>
    Public Sub ImportExportDescriptionsWrapAtNarrowWidths()

        Dim failures As New List(Of String)()

        StaRunner.Run(
            Sub()
                For Each scale As Single In {1.0F, 1.5F}
                    Using board As New CommandBoard()
                        board.Prepare()
                        board.Font = New Font(board.Font.FontFamily, board.Font.SizeInPoints * scale)
                        board.Size = New Size(960, 720)
                        board.NavigateTo(Form1.WorkspacePage.ImportExport)
                        Application.DoEvents()
                        Dim list As CommandList = board.List()
                        Check(list, "at 960 pixels and size " & scale.ToString(), failures)

                        board.NavigateTo(Form1.WorkspacePage.Board)
                        board.Size = New Size(1400, 720)
                        Application.DoEvents()
                        board.NavigateTo(Form1.WorkspacePage.ImportExport)
                        Application.DoEvents()
                        Check(list, "after a resize while hidden, size " & scale.ToString(), failures)
                        If list.DescriptionWidth() <= UiTheme.Px(200, list.DeviceDpi) Then failures.Add("The wider window gave the descriptions no more room.")

                        board.Close()
                    End Using
                Next
            End Sub)

        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' The page is built once: the same page on each visit, shown and hidden
    ' in place, never disposed, with no window made again.
    <TestMethod>
    Public Sub ImportExportPageIsKeptBetweenVisits()

        StaRunner.Run(
            Sub()
                Using board As New CommandBoard()
                    board.Prepare()
                    board.NavigateTo(Form1.WorkspacePage.ImportExport)
                    Application.DoEvents()
                    Dim page As Control = board.Page()
                    Dim before As List(Of IntPtr) = ViewCatalog.Descendants(page).Select(Function(control) control.Handle).ToList()

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Assert.IsFalse(page.Visible, "Leaving hides the page.")
                    Assert.IsFalse(page.IsDisposed)
                    Assert.AreSame(board.ContentHost(), page.Parent, "The page stays where it is.")

                    board.NavigateTo(Form1.WorkspacePage.ImportExport)
                    Application.DoEvents()
                    Assert.AreSame(page, board.Page())
                    Assert.IsTrue(page.Visible)
                    CollectionAssert.AreEqual(before, ViewCatalog.Descendants(page).Select(Function(control) control.Handle).ToList(), "No window is made again.")

                    board.Close()
                End Using
            End Sub)

    End Sub


    Private Shared Sub Check(list As CommandList, label As String, failures As List(Of String))

        Dim wrap As Integer = list.DescriptionWidth()
        Dim expectedWrap As Integer = Math.Max(UiTheme.Px(200, list.DeviceDpi), list.ClientSize.Width - list.ButtonWidth - UiTheme.Px(40, list.DeviceDpi))
        If wrap <> expectedWrap Then failures.Add(label & ": the descriptions wrap at " & wrap & ", not " & expectedWrap & ".")

        Dim previousBottom As Integer = Integer.MinValue
        For Each button As ActionButton In list.Controls.OfType(Of ActionButton)()
            Dim text As Rectangle = list.DescriptionBounds(button)
            Dim needed As Size = TextRenderer.MeasureText(button.AccessibleDescription, list.Font, New Size(wrap, Integer.MaxValue),
                                                          TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.TextBoxControl)
            If text.Width > wrap Then failures.Add(label & ": " & button.Text & "'s description is wider than the wrap.")
            If text.Height < needed.Height Then failures.Add(label & ": " & button.Text & "'s description is cut off.")
            If text.Left <> button.Right + UiTheme.Px(14, list.DeviceDpi) Then failures.Add(label & ": " & button.Text & "'s description is not 14 pixels after it.")
            Dim top As Integer = Math.Min(button.Top, text.Top)
            Dim bottom As Integer = Math.Max(button.Bottom, text.Bottom)
            If button.Top <> top Then failures.Add(label & ": " & button.Text & " is not at the top of its row.")
            If top < previousBottom Then failures.Add(label & ": " & button.Text & "'s row overlaps the one before.")
            previousBottom = bottom
        Next

    End Sub


    ' The real main window on an empty in-memory library: Load is skipped
    ' and the window never activates.
    Private NotInheritable Class CommandBoard
        Inherits Form1

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function SaveManuscripts() As Boolean
            Assert.Fail("Nothing on the Import & Export page saves by being shown.")
            Return False
        End Function

        Public Sub Prepare()
            GetType(Form1).GetMethod("BuildInterface", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, New List(Of Manuscript)())
            GetType(Form1).GetMethod("RenderManuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public Function Page() As Control
            Return DirectCast(GetType(Form1).GetField("importExportPage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
        End Function

        Public Function List() As CommandList
            Return ViewCatalog.Descendants(Page()).OfType(Of CommandList)().Single()
        End Function

        Public Function ContentHost() As Control
            Return DirectCast(GetType(Form1).GetField("contentHost", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
        End Function

    End Class

End Class
