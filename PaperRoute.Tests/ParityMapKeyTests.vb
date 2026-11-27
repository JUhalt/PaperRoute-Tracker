Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The lock on the view catalog's keys (v1.1 harness design 2.3, "Keeping
' the 85 keys honest"). The Avalonia tests keep the parity map
' (PaperRoute.Desktop.Tests\Parity\parity-map.tsv), which gives every
' view in this catalog its place in the new interface, but they run on
' net10.0 and on Ubuntu and cannot load this suite. So this project links
' the map, and this test compares its key column with ViewCatalog's
' Dialogs, Hosted and Stages: a new form without a row, or a renamed view,
' fails the required Windows build.
<TestClass>
Public Class ParityMapKeyTests

    <TestMethod>
    Public Sub TheParityMapNamesEveryCatalogKeyOnce()

        Dim mapPath = Path.Combine(AppContext.BaseDirectory, "Parity", "parity-map.tsv")
        Assert.IsTrue(File.Exists(mapPath), "The parity map is linked into the test output: " & mapPath)

        ' Each key as "part<tab>key", the map's first two columns.
        Dim catalog As New List(Of String)()
        catalog.AddRange(ViewCatalog.Dialogs.Keys.Select(Function(view) "dialog" & vbTab & view.Name))
        catalog.AddRange(ViewCatalog.Hosted.Select(Function(view) "hosted" & vbTab & view.Name))
        catalog.AddRange(ViewCatalog.Stages.Select(Function(view) "stage" & vbTab & view.Name))

        Dim mapped As New List(Of String)()
        Dim header = True
        For Each line In File.ReadAllLines(mapPath)

            If line.StartsWith("#", StringComparison.Ordinal) Then
                Continue For
            End If

            Dim cells = line.Split(ControlChars.Tab)
            If header Then
                Assert.IsTrue(cells.Length >= 2 AndAlso cells(0) = "part" AndAlso cells(1) = "key", "The map's header starts ""part<tab>key"": " & line)
                header = False
                Continue For
            End If

            Assert.IsGreaterThanOrEqualTo(2, cells.Length, "A map line has a part and a key: " & line)
            mapped.Add(cells(0) & vbTab & cells(1))

        Next

        Dim problems As New List(Of String)()
        For Each key In catalog.Where(Function(item) Not mapped.Contains(item))
            problems.Add("no row for " & key.Replace(vbTab, " "))
        Next
        For Each key In mapped.Where(Function(item) Not catalog.Contains(item)).Distinct()
            problems.Add("a row for " & key.Replace(vbTab, " ") & ", which the catalog does not have")
        Next
        For Each twice In mapped.GroupBy(Function(item) item, StringComparer.Ordinal).Where(Function(rows) rows.Count() > 1)
            problems.Add(twice.Count().ToString() & " rows for " & twice.Key.Replace(vbTab, " "))
        Next

        Assert.AreEqual(
            0,
            problems.Count,
            Environment.NewLine & String.Join(Environment.NewLine, problems)
        )
        Assert.AreEqual(catalog.Count, mapped.Count, "One row per catalog key.")

    End Sub

End Class
