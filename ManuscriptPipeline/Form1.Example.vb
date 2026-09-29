Imports System
Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services

' The example library for teaching and first-time exploration (#83). It
' opens in a separate window with its own disposable storage; this window
' and its library are never touched.
Partial Public Class Form1

    ' Opens the example; tests replace it.
    Friend exampleLauncher As Action = Nothing


    Private Sub OpenExampleLibrary()

        Try
            If exampleLauncher IsNot Nothing Then
                exampleLauncher()
            Else
                ExampleLibraryService.Launch()
            End If
            lblStatus.Text = "The example library opens in a separate window. Your own library stays here, unchanged."
        Catch ex As Exception When TypeOf ex Is Win32Exception OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is PlatformNotSupportedException
            MessageBox.Show(Me, "PaperRoute could not open the example library." & Environment.NewLine & Environment.NewLine & ex.Message,
                            "Example Library", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try

    End Sub


    ' Across the top of the example window, always: what this is, and the
    ' way back.
    Private Function CreateExampleBanner() As Control

        Dim dpi As Integer = DeviceDpi
        Dim banner As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(UiTheme.Px(16, dpi), UiTheme.Px(8, dpi), UiTheme.Px(16, dpi), UiTheme.Px(8, dpi)),
            .Margin = New Padding(0),
            .BackColor = UiTheme.AccentMutedBackground(),
            .AccessibleName = "Example library",
            .AccessibleRole = AccessibleRole.Alert
        }
        banner.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        banner.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

        Dim lblBanner As New Label With {
            .Text = "Example library: a fictional research group's manuscripts, to explore and to teach with. " &
                    "Change anything; it is discarded when this window closes, and your own library is never opened.",
            .AutoSize = True,
            .UseMnemonic = False,
            .Anchor = AnchorStyles.Left,
            .ForeColor = UiTheme.PrimaryText(),
            .BackColor = UiTheme.AccentMutedBackground(),
            .Margin = New Padding(0, UiTheme.Px(4, dpi), UiTheme.Px(16, dpi), UiTheme.Px(4, dpi))
        }
        Dim btnReturn As New ActionButton With {
            .Text = "Return to My Library",
            .Role = ActionButtonRole.Primary,
            .Width = GetResponsiveButtonWidth("Return to My Library", 170),
            .Height = GetResponsiveButtonHeight(32),
            .Anchor = AnchorStyles.Right,
            .AccessibleDescription = "Closes the example window. Your own PaperRoute window is unchanged."
        }
        AddHandler btnReturn.Click, Sub(sender, e) Close()

        banner.Controls.Add(lblBanner, 0, 0)
        banner.Controls.Add(btnReturn, 1, 0)
        AddHandler banner.Resize,
            Sub(sender, e)
                lblBanner.MaximumSize = New Size(Math.Max(UiTheme.Px(240, dpi), banner.ClientSize.Width - banner.Padding.Horizontal - btnReturn.Width - UiTheme.Px(24, dpi)), 0)
            End Sub
        Return banner

    End Function

End Class
