Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Windows.Forms.Automation
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

' Screen-reader basics (#139): a status or progress line is announced
' when it changes, without moving focus, and a control drawn as a button
' can be pressed by a screen reader, whose Invoke is the control's
' default action.
<TestClass>
<DoNotParallelize>
Public Class AccessibleActionTests

    ' Every view's status and progress lines, the fields named lblStatus or
    ' lblProgress, are polite live regions, and so are the two lines a
    ' running check reports in: Test Connection's result and the packet
    ' vault's integrity summary. They are found by name over the catalog,
    ' so a new view's status line is held to it as well; the six named
    ' below must be among them, so the search cannot pass by finding
    ' nothing.
    <TestMethod>
    Public Sub StatusAndProgressLabelsAreLiveRegions()

        Dim found As New List(Of String)()
        Dim silent As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.Dialogs.Select(Function(entry) (entry.Key.Name, entry.Value)),
                Sub(name, shown)

                    For Each status In StatusLabels(shown)

                        found.Add(name & "." & status.Field)

                        If status.Label.LiveSetting <> AutomationLiveSetting.Polite Then
                            silent.Add(name & "." & status.Field & " is " & status.Label.LiveSetting.ToString())
                        End If

                    Next

                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        For Each expected As String In {
            "Form1.lblStatus",
            "UpdateProgressForm.lblStatus",
            "FillBlanksForm.lblProgress",
            "PublicationCheckForm.lblProgress",
            "SettingsForm.lblTestResult",
            "SubmissionPacketVaultForm.lblIntegritySummary"
        }

            CollectionAssert.Contains(
                found,
                expected,
                "The search finds " & expected & " among: " & String.Join(", ", found)
            )

        Next

        Assert.AreEqual(
            0,
            silent.Count,
            "Not announced: " & String.Join(", ", silent)
        )

    End Sub


    <TestMethod>
    Public Sub FilterChipDefaultActionRaisesClick()

        AssertDefaultActionClicks(
            Function() New FilterChip With {.Text = "Overdue revisions"}
        )

    End Sub


    <TestMethod>
    Public Sub TagChipButtonDefaultActionRaisesClick()

        AssertDefaultActionClicks(
            Function() New TagChipButton("teaching", TagPalette.Teal)
        )

    End Sub


    <TestMethod>
    Public Sub ContextHelpControlDefaultActionRaisesClick()

        AssertDefaultActionClicks(
            Function() New ContextHelpControl("What the field is for.")
        )

    End Sub


    ' Shows the control in a form offscreen and invokes its default action
    ' as a screen reader would: one Click, a push button, and "Press".
    Private Shared Sub AssertDefaultActionClicks(
        create As Func(Of Control)
    )

        Dim clicks As Integer =
            0

        Dim role As AccessibleRole =
            AccessibleRole.None

        Dim action As String =
            Nothing

        StaRunner.Run(
            Sub()

                Using host As New Form With {
                    .StartPosition = FormStartPosition.Manual,
                    .Location = New Point(-20000, -20000),
                    .ShowInTaskbar = False
                }

                    Dim control As Control =
                        create()

                    AddHandler control.Click,
                        Sub(sender, e)
                            clicks += 1
                        End Sub

                    host.Controls.Add(control)
                    host.Show()
                    Application.DoEvents()

                    role = control.AccessibilityObject.Role
                    action = control.AccessibilityObject.DefaultAction

                    control.AccessibilityObject.DoDefaultAction()
                    Application.DoEvents()

                    host.Close()

                End Using

            End Sub
        )

        Assert.AreEqual(
            1,
            clicks,
            "The default action raises Click once."
        )

        Assert.AreEqual(
            AccessibleRole.PushButton,
            role
        )

        Assert.AreEqual(
            "Press",
            action
        )

    End Sub


    ' The view's Label fields named lblStatus, lblProgress, lblTestResult
    ' or lblIntegritySummary, on its own class and those it inherits from.
    Private Shared Iterator Function StatusLabels(
        form As Form
    ) As IEnumerable(Of (Field As String, Label As Label))

        Dim type As Type =
            form.GetType()

        While type IsNot Nothing AndAlso type IsNot GetType(Form)

            For Each field As FieldInfo In type.GetFields(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)

                If GetType(Label).IsAssignableFrom(field.FieldType) AndAlso
                   Regex.IsMatch(field.Name, "^_?lbl(Status|Progress|TestResult|IntegritySummary)$") Then

                    Dim label As Label =
                        TryCast(field.GetValue(form), Label)

                    If label IsNot Nothing Then
                        Yield (field.Name, label)
                    End If

                End If

            Next

            type = type.BaseType

        End While

    End Function

End Class
