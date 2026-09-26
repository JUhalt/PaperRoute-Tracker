Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

<TestClass>
<DoNotParallelize>
Public Class UiTextMnemonicTests

    ' WinForms treats a single "&" in button and label text as a mnemonic
    ' marker, so "Save & Close" renders as "Save  Close". Literal ampersands
    ' must be written as "&&" or shown in a control with UseMnemonic = False.
    Private Shared ReadOnly LostAmpersand As New Regex("(?<!&)&(?!&)(?=\s|$)")

    <TestMethod>
    Public Sub SettingsDialog_ShowsLiteralAmpersands()

        Using dialog As New SettingsForm(New AppSettings())
            AssertNoLostAmpersands(dialog)
        End Using

    End Sub

    <TestMethod>
    Public Sub VersionHistorySummary_ShowsLiteralAmpersands()

        Dim manuscript As New Manuscript With {
            .Title = "Synthetic mnemonic manuscript"
        }
        manuscript.Versions.Add(New ManuscriptVersion With {
            .Label = "Synthetic metadata-only version"
        })

        Using history As New ManuscriptVersionHistoryControl(manuscript)
            AssertNoLostAmpersands(history)
        End Using

    End Sub

    Private Shared Sub AssertNoLostAmpersands(root As Control)

        For Each control As Control In Descendants(root)
            Dim label As Label = TryCast(control, Label)
            Dim button As ButtonBase = TryCast(control, ButtonBase)
            Dim usesMnemonic As Boolean =
                (label IsNot Nothing AndAlso label.UseMnemonic) OrElse
                (button IsNot Nothing AndAlso button.UseMnemonic)

            If usesMnemonic Then
                Assert.IsFalse(
                    LostAmpersand.IsMatch(control.Text),
                    "A literal ampersand would disappear from: " & control.Text)
            End If
        Next

    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

End Class
