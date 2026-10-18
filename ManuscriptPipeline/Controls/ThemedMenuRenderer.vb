Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Menus as Windows draws them under a contrast theme (#191): each item's
    ' text in the menu text color, and the hovered item in the highlight
    ' pair. WinForms' own renderer draws the text in the button text color,
    ' which a theme such as Night sky makes yellow. Everything else, and
    ' everything in Light and Dark, is WinForms' own renderer's.
    Friend NotInheritable Class ThemedMenuRenderer
        Inherits ToolStripProfessionalRenderer


        ' Every menu the calling thread shows draws with it: PaperRoute's
        ' menus leave their drawing to the thread's renderer.
        Public Shared Sub Install()

            ToolStripManager.Renderer =
                New ThemedMenuRenderer()

        End Sub


        Protected Overrides Sub OnRenderMenuItemBackground(
            e As ToolStripItemRenderEventArgs
        )

            If UiTheme.IsHighContrast() AndAlso Hovered(e.Item) Then

                Using fill As New SolidBrush(SystemColors.Highlight)
                    e.Graphics.FillRectangle(fill, New Rectangle(Point.Empty, e.Item.Size))
                End Using

                Return

            End If

            MyBase.OnRenderMenuItemBackground(e)

        End Sub


        Protected Overrides Sub OnRenderItemText(
            e As ToolStripItemTextRenderEventArgs
        )

            If UiTheme.IsHighContrast() AndAlso
               TypeOf e.Item Is ToolStripMenuItem AndAlso
               e.Item.Enabled Then

                e.TextColor =
                    If(Hovered(e.Item), SystemColors.HighlightText, SystemColors.MenuText)

            End If

            MyBase.OnRenderItemText(e)

        End Sub


        Private Shared Function Hovered(
            item As ToolStripItem
        ) As Boolean

            Return item.Enabled AndAlso (item.Selected OrElse item.Pressed)

        End Function

    End Class

End Namespace
