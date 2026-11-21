Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The order today's Board gives the 500-manuscript library under its sorts,
' checked in under PaperRoute.TestFixtures\SortOrders, so the new interface
' (v1.1) can be held to the same order without a window of today's (the
' Board trial's V8). The sort is List.Sort with Form1's comparisons, which
' does not promise to keep equal items in order, so the file is the order
' the Board gave, not one worked out again.
'
' All seven sorts (Form1.vb GetVisibleManuscripts, :2247-2285, and the
' comparisons at :2293-2372), none of which depends on the date. Current
' order does not sort. Title A-Z and Z-A compare Title (in the current
' culture, ignoring case). Most and Fewest rejections compare the attention
' snapshot's RejectionCount (:2319-2348); Form1 works the snapshot out for
' DateTime.Today (:2431-2465, :2670-2673), but RejectionCount counts every
' rejection decision with no date (ManuscriptAttentionService.vb:66-97), so
' their order, ties included, is pinned here too. Newest and Oldest stage
' change compare the stored StageEnteredDate (:2351-2372), which the
' fixture sets from its fixed LargeLibraryFixture.Today.
'
' A manuscript is named by its position in the library's order, from 0:
' its Id is a new Guid each time the fixture writes the library
' (Manuscript.vb), so (Id, occurrence) could not be checked in. Position
' n is the manuscript whose title starts "Synthetic" and n + 1.
'
' With PAPERROUTE_SORT_ORDERS_OUT naming a folder, the test also writes the
' orders it read there, as board-sort-orders-500.json, to check in after a
' deliberate change to the fixture or the sorts.
<TestClass>
<DoNotParallelize>
Public Class BoardSortOrderTests

    Private Const FileName As String = "board-sort-orders-500.json"
    Private Const ManuscriptCount As Integer = 500

    ' The sort box's index and its text: all seven sorts, none of which
    ' depends on the date.
    Private Shared ReadOnly BoardSorts As (Index As Integer, Text As String)() = {
        (0, "Sort: Current order"),
        (1, "Sort: Title A-Z"),
        (2, "Sort: Title Z-A"),
        (3, "Sort: Most rejections"),
        (4, "Sort: Fewest rejections"),
        (5, "Sort: Newest stage change"),
        (6, "Sort: Oldest stage change")
    }

    ' The JSON name of each shelf, its tab, its panel and its location.
    Private Shared ReadOnly Shelves As (Name As String, Tab As String, Panel As String, Location As ManuscriptLocation)() = {
        ("Pipeline", "tabPipeline", "pipelinePanel", ManuscriptLocation.Pipeline),
        ("Published", "tabPublished", "publishedPanel", ManuscriptLocation.Published),
        ("FileDrawer", "tabFileDrawer", "fileDrawerPanel", ManuscriptLocation.FileDrawer)
    }

    Private _root As String = String.Empty


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


    <TestMethod>
    Public Sub TheSevenSortsGiveTheCheckedInOrders()

        Dim observed As New List(Of (Index As Integer, Text As String, Orders As Dictionary(Of String, List(Of Integer))))()

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), ManuscriptCount), Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForIdleWork()
                    board.WaitForBoardFill()

                    Dim positions As Dictionary(Of Manuscript, Integer) = LibraryPositions(board.Library)
                    Dim box As ComboBox = board.Part(Of ComboBox)("cboBoardSort")

                    For Each sort In BoardSorts

                        Assert.AreEqual(sort.Text, box.Items(sort.Index).ToString(), "The sort box's item " & sort.Index & ".")
                        box.SelectedIndex = sort.Index
                        Application.DoEvents()

                        Dim orders As New Dictionary(Of String, List(Of Integer))()

                        For Each entry In Shelves
                            board.ShelfTab(entry.Tab).Checked = True
                            Application.DoEvents()
                            board.WaitForBoardFill()

                            Dim shown As List(Of Integer) = board.ShelfManuscripts(entry.Panel).Select(Function(item) positions(item)).ToList()
                            Dim location As ManuscriptLocation = entry.Location
                            Assert.AreEqual(board.Library.Where(Function(item) item.Location = location).Count(), shown.Count,
                                            sort.Text & ", " & entry.Name & ": a card for every manuscript on the shelf.")
                            orders(entry.Name) = shown
                        Next

                        observed.Add((sort.Index, sort.Text, orders))

                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(10))

        Dim written As String = ToJson(observed)

        Dim outFolder As String = Environment.GetEnvironmentVariable("PAPERROUTE_SORT_ORDERS_OUT")
        If Not String.IsNullOrWhiteSpace(outFolder) Then
            Directory.CreateDirectory(outFolder)
            File.WriteAllText(Path.Combine(outFolder, FileName), written, New UTF8Encoding(False))
        End If

        Dim checkedIn As String = Path.Combine(TestRepository.Root(), "PaperRoute.TestFixtures", "SortOrders", FileName)
        Assert.IsTrue(File.Exists(checkedIn), "The checked-in sort orders are at " & checkedIn & ".")

        Using expected As JsonDocument = JsonDocument.Parse(File.ReadAllText(checkedIn))

            Assert.AreEqual(ManuscriptCount, expected.RootElement.GetProperty("manuscripts").GetInt32(), "The file is for the 500 library.")

            Dim sorts As List(Of JsonElement) = expected.RootElement.GetProperty("sorts").EnumerateArray().ToList()
            Assert.AreEqual(observed.Count, sorts.Count, "The file holds the seven sorts.")

            For index As Integer = 0 To observed.Count - 1
                Dim seen = observed(index)
                Assert.AreEqual(seen.Index, sorts(index).GetProperty("index").GetInt32(), "The file's sort " & index & ".")
                Assert.AreEqual(seen.Text, sorts(index).GetProperty("text").GetString(), "The file's sort " & index & ".")
                For Each entry In Shelves
                    Dim want As List(Of Integer) = sorts(index).GetProperty(entry.Name).EnumerateArray().Select(Function(item) item.GetInt32()).ToList()
                    AssertSameOrder(want, seen.Orders(entry.Name), seen.Text & ", " & entry.Name)
                Next
            Next

        End Using

    End Sub


    ' Each manuscript's position in the library's order, by reference, so two
    ' records with one Id would keep their own. The library is the one the
    ' fixture wrote, in its order: position n is "Synthetic" n + 1.
    Private Shared Function LibraryPositions(library As List(Of Manuscript)) As Dictionary(Of Manuscript, Integer)

        Assert.AreEqual(ManuscriptCount, library.Count, "The Board loaded the 500 library.")

        Dim result As New Dictionary(Of Manuscript, Integer)(ReferenceEqualityComparer.Instance)

        For position As Integer = 0 To library.Count - 1
            StringAssert.StartsWith(library(position).Title, "Synthetic " & (position + 1).ToString("000", CultureInfo.InvariantCulture) & ":",
                                    "The library is in the order the fixture wrote it.")
            result.Add(library(position), position)
        Next

        Return result

    End Function


    ' The first place the two orders part, for a message that can be read.
    Private Shared Sub AssertSameOrder(expected As List(Of Integer), actual As List(Of Integer), what As String)

        Assert.AreEqual(expected.Count, actual.Count, what & ": the number of cards.")

        For index As Integer = 0 To expected.Count - 1
            If expected(index) <> actual(index) Then
                Assert.Fail(what & ": card " & index & " is the manuscript at position " & actual(index) & ", the checked-in order has " & expected(index) & ".")
            End If
        Next

    End Sub


    ' The file as it is checked in: one line for each shelf's order, so a
    ' change shows as a short diff.
    Private Shared Function ToJson(observed As List(Of (Index As Integer, Text As String, Orders As Dictionary(Of String, List(Of Integer))))) As String

        Dim text As New StringBuilder()
        text.Append("{" & vbLf)
        text.Append("  ""about"": ""The order today's WinForms Board gives LargeLibraryFixture.Create(root, 500) under its seven sorts, none of which depends on the date (Most and Fewest rejections compare the attention snapshot's RejectionCount, which counts every rejection decision with no date), shelf by shelf. Each number is a manuscript's position in the library's order, from 0 (position n is the title 'Synthetic' n + 1). Written by PaperRoute.Tests' BoardSortOrderTests.""," & vbLf)
        text.Append("  ""manuscripts"": " & ManuscriptCount.ToString(CultureInfo.InvariantCulture) & "," & vbLf)
        text.Append("  ""sorts"": [" & vbLf)

        For index As Integer = 0 To observed.Count - 1
            Dim seen = observed(index)
            text.Append("    {" & vbLf)
            text.Append("      ""index"": " & seen.Index.ToString(CultureInfo.InvariantCulture) & "," & vbLf)
            text.Append("      ""text"": " & JsonSerializer.Serialize(seen.Text) & "," & vbLf)
            For shelfIndex As Integer = 0 To Shelves.Length - 1
                Dim name As String = Shelves(shelfIndex).Name
                text.Append("      """ & name & """: [" & String.Join(", ", seen.Orders(name).Select(Function(item) item.ToString(CultureInfo.InvariantCulture))) & "]")
                text.Append(If(shelfIndex < Shelves.Length - 1, ",", String.Empty) & vbLf)
            Next
            text.Append("    }" & If(index < observed.Count - 1, ",", String.Empty) & vbLf)
        Next

        text.Append("  ]" & vbLf)
        text.Append("}" & vbLf)
        Return text.ToString()

    End Function

End Class
