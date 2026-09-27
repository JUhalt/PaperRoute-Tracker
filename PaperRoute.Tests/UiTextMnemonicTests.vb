Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

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

    ' Found while certifying v0.6.0-rc.1: the update prompt read "Download  Restart".
    <TestMethod>
    Public Sub UpdatePrompt_ShowsLiteralAmpersands()

        Using prompt As New UpdatePromptForm("0.6.0", "0.6.1", "Stable", "Notes & fixes")
            AssertNoLostAmpersands(prompt)
            Assert.IsTrue(Descendants(prompt).OfType(Of Button)().Any(Function(button) button.Text = "Download && Restart"))
        End Using

    End Sub

    ' #71: names typed by the user keep their ampersands in dialogs.
    <TestMethod>
    Public Sub DialogsShowUserDataWithLiteralAmpersands()

        Dim manuscript As New Manuscript With {
            .Title = "Attention & Memory in Synthetic Samples",
            .TargetJournal = "Memory & Cognition",
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Revision
        }
        Dim submission As New JournalSubmission With {
            .JournalName = "Memory & Cognition",
            .SubmittedDate = New DateTime(2026, 3, 1),
            .ManuscriptNumber = "MC-2026-001"
        }
        submission.Decisions.Add(New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MajorRevision,
            .DecisionDate = New DateTime(2026, 4, 2)
        })
        manuscript.Submissions.Add(submission)

        Using details As New SubmissionDetailsForm(manuscript, submission)
            AssertNoLostAmpersands(details)
            AssertShowsLiterally(details, "Memory & Cognition")
        End Using

        Using route As New ManuscriptRouteViewForm(manuscript)
            AssertNoLostAmpersands(route)
            AssertShowsLiterally(route, "Memory & Cognition")
        End Using

        Using confirm As New DeleteManuscriptForm(manuscript.Title)
            AssertNoLostAmpersands(confirm)
        End Using

    End Sub

    ' #71: styling keeps the accelerators the reviewer-response dialogs rely on.
    <TestMethod>
    Public Sub DeliberateLabelAcceleratorsKeepWorking()

        Using item As New ReviewerResponseItemForm(New JournalSubmission With {.JournalName = "Memory & Cognition"})
            For Each text As String In {"Editorial &decision", "Revision &round", "Re&viewer (required)"}
                Dim label As Label = Descendants(item).OfType(Of Label)().Single(Function(candidate) candidate.Text = text)
                Assert.IsTrue(label.UseMnemonic, text & " keeps its accelerator.")
            Next
        End Using

        Assert.IsTrue(UiPolish.UsesAcceleratorSyntax("Show &status"))
        Assert.IsTrue(UiPolish.UsesAcceleratorSyntax("Save && Close"))
        Assert.IsFalse(UiPolish.UsesAcceleratorSyntax("Memory & Cognition"))
        Assert.IsFalse(UiPolish.UsesAcceleratorSyntax("Trailing &"))
        Assert.IsFalse(UiPolish.UsesAcceleratorSyntax(String.Empty))

    End Sub

    Private Shared Sub AssertShowsLiterally(root As Control, text As String)
        Assert.IsTrue(
            Descendants(root).OfType(Of Label)().Any(Function(label) label.Text.Contains(text) AndAlso Not label.UseMnemonic),
            "Expected a label showing " & text & " literally.")
    End Sub

    Private Shared Sub AssertNoLostAmpersands(root As Control)

        For Each control As Control In Descendants(root)
            Dim label As Label = TryCast(control, Label)
            Dim button As ButtonBase = TryCast(control, ButtonBase)
            Dim usesMnemonic As Boolean =
                (label IsNot Nothing AndAlso label.UseMnemonic) OrElse
                (button IsNot Nothing AndAlso button.UseMnemonic) OrElse
                TypeOf control Is GroupBox

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
