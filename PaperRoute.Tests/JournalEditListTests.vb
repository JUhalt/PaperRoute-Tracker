Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

' Edit Journal's checklist (#93, step 1): at 150% a row ending "• AI
' suggestion" was cut at the list's right edge with no way to scroll
' sideways. With a requirement longer than the list is wide, the list
' scrolls sideways as the other lists do, its horizontal extent covering
' the widest row.
<TestClass>
<DoNotParallelize>
Public Class JournalEditListTests

    Private Const LB_GETHORIZONTALEXTENT As Integer = &H193


    <TestMethod>
    Public Sub ARowWiderThanTheChecklistCanBeScrolledTo()

        Dim journal As New JournalRecord With {.Name = "Fictional Journal"}

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Title = "Deposit the data, analysis code and materials in a public repository and cite the persistent identifier in the manuscript",
                .Category = "Data availability",
                .Suggestion = New AssistantSuggestion()
            }
        )

        ViewCatalog.ShowAndClose(
            "Edit Journal",
            Function() New JournalEditForm(journal),
            Sub(name, shown)

                For Each tabs As TabControl In ViewCatalog.Descendants(shown).OfType(Of TabControl)()
                    tabs.SelectedTab = tabs.TabPages.Cast(Of TabPage)().Single(Function(page) page.Text.Contains("Checklist", StringComparison.Ordinal))
                Next

                Application.DoEvents()

                Dim list As ListBox =
                    ViewCatalog.Descendants(shown).OfType(Of ListBox)().Single(Function(box) box.AccessibleName = "Checklist requirements")

                Dim widest As Integer =
                    list.Items.Cast(Of Object)().Max(Function(item) TextRenderer.MeasureText(item.ToString(), list.Font).Width)

                StringAssert.EndsWith(list.Items(0).ToString(), "AI suggestion", "The row is the one that was cut.")
                Assert.IsTrue(widest > list.ClientSize.Width, $"The case is real: the row ({widest}) is wider than the list ({list.ClientSize.Width}).")

                Dim extent As Integer =
                    CInt(SendMessage(list.Handle, LB_GETHORIZONTALEXTENT, IntPtr.Zero, IntPtr.Zero))

                Assert.IsTrue(
                    list.HorizontalScrollbar AndAlso extent >= widest,
                    $"The list scrolls sideways to its widest row: HorizontalScrollbar {list.HorizontalScrollbar}, extent {extent} for a row {widest} wide in a list {list.ClientSize.Width} wide."
                )

            End Sub
        )

    End Sub


    <DllImport("user32.dll")>
    Private Shared Function SendMessage(window As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

End Class
