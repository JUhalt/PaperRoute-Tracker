Imports System.Drawing
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Sections drawn as cards and underline tabs (#57 part 3b) keep the layout
' and keyboard behavior of the classic controls they replace.
<TestClass>
<DoNotParallelize>
Public Class SectionCardTests

    <TestMethod>
    Public Sub AnAutoSizedCardShowsAllOfItsWrappedText()
        RunOnStaThread(
            Sub()
                Using form As New Form With {.Size = New Size(700, 600), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000)}
                    Dim root As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 1}
                    root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
                    Dim card As New SectionCard With {.Text = "Notes for Memory & Cognition", .Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Padding = New Padding(14, 8, 14, 12)}
                    Dim label As New Label With {.AutoSize = True, .Dock = DockStyle.Top, .UseMnemonic = False}
                    card.Controls.Add(label)
                    AddHandler card.Resize, Sub(sender, e) label.MaximumSize = New Size(Math.Max(200, card.ClientSize.Width - card.Padding.Horizontal), 0)
                    root.Controls.Add(card, 0, 0)
                    form.Controls.Add(root)
                    form.Show()
                    Application.DoEvents()

                    label.Text = String.Join(Environment.NewLine & Environment.NewLine,
                        String.Join(" ", Enumerable.Repeat("Results sections must report exact values with intervals.", 6)),
                        "3 checklist items in the Journal Library.")
                    Application.DoEvents()

                    Assert.IsTrue(label.Height > label.Font.Height * 3, "The text wraps.")
                    Assert.IsTrue(label.Bottom <= card.DisplayRectangle.Bottom,
                        $"The card fits its text: label ends at {label.Bottom}, content area at {card.DisplayRectangle.Bottom}.")
                    Assert.IsTrue(label.Top >= card.Padding.Top + label.Font.Height, "The title stays above the content.")
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub UnderlineTabsKeepTabPagesAndKeyboardSelection()
        RunOnStaThread(
            Sub()
                Using form As New Form With {.Size = New Size(600, 400), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000)}
                    Dim tabs As New UnderlineTabControl With {.Dock = DockStyle.Fill}
                    tabs.TabPages.Add("Editorial History")
                    tabs.TabPages.Add("Correspondence && Files")
                    form.Controls.Add(tabs)
                    form.Show()
                    Application.DoEvents()

                    Dim page As TabPage = tabs.TabPages(0)
                    Assert.IsTrue(page.Top >= tabs.GetTabRect(0).Bottom, "Pages sit below the tab strip.")
                    Assert.AreEqual(tabs.ClientSize.Width, page.Width, "Pages use the full width, without a frame.")

                    ' The tab control still handles its own keys.
                    GetType(TabControl).GetMethod("OnKeyDown", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).
                        Invoke(tabs, New Object() {New KeyEventArgs(Keys.Control Or Keys.Tab)})
                    Application.DoEvents()
                    Assert.AreEqual(1, tabs.SelectedIndex, "Ctrl+Tab moves to the next tab.")
                    Assert.IsTrue(tabs.TabPages(1).Visible)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TextBoxesAndListsUseTheThemeBorder()
        RunOnStaThread(
            Sub()
                Application.EnableVisualStyles()
                Using form As New Form With {.Size = New Size(400, 300), .ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000)}
                    Dim box As New TextBox With {.Location = New Point(10, 10), .Width = 200}
                    Dim list As New ListBox With {.Location = New Point(10, 60), .Size = New Size(200, 100)}
                    form.Controls.AddRange({box, list})
                    UiPolish.ApplyDialog(form)
                    form.Show()
                    Application.DoEvents()

                    For Each control As Control In {box, list}
                        Using image As New Bitmap(control.Width, control.Height)
                            control.DrawToBitmap(image, New Rectangle(Point.Empty, control.Size))
                            Dim expected As Color = If(control.Focused, UiTheme.AccentColor(), UiTheme.CardBorder())
                            Assert.AreEqual(expected.ToArgb(), image.GetPixel(control.Width \ 2, 0).ToArgb(),
                                control.GetType().Name & " has the theme border (accent while focused), not the system frame.")
                        End Using
                    Next
                End Using
            End Sub)
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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Section card test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
