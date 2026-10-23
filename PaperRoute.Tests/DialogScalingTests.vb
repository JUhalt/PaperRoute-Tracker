Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The one scaling rule for a dialog (#93, step 3): DialogScaling.Begin
' before the first control and Finish after the last give a window the
' 96-DPI base dimensions, the DPI scaling mode, one layout pass and a fit
' to the screen's working area when it opens. The converted windows (the
' four that had the base dimensions but not the rest of the rule, then
' the ones the measuring test failed) use it, with every hand-scaled
' value on a property Windows Forms scales written as its 96-DPI
' literal. And the mode the app runs in is pinned to the one it has
' always run in.
<TestClass>
<DoNotParallelize>
Public Class DialogScalingTests


    ' A window built with the rule has the base dimensions and the mode,
    ' and opens no larger than its screen's working area however large it
    ' was made. Without the rule (the probe's twin) it opens at its full
    ' size, off the screen.
    <TestMethod>
    Public Sub BeginAndFinishGiveAWindowTheRule()

        StaRunner.Run(
            Sub()

                For Each ruled As Boolean In {True, False}

                    Using dialog As New Form With {.Text = "Scaling probe", .StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False}

                        If ruled Then
                            DialogScaling.Begin(dialog)
                        End If

                        dialog.ClientSize = New Size(6000, 4000)
                        dialog.Controls.Add(New Button With {.Text = "OK", .Dock = DockStyle.Bottom})

                        If ruled Then
                            DialogScaling.Finish(dialog)
                        End If

                        dialog.Show()
                        Application.DoEvents()

                        Dim area As Rectangle = Screen.FromControl(dialog).WorkingArea

                        If ruled Then
                            Assert.AreEqual(New SizeF(96.0F, 96.0F), dialog.AutoScaleDimensions, "The 96-DPI base dimensions.")
                            Assert.AreEqual(AutoScaleMode.Dpi, dialog.AutoScaleMode, "The DPI scaling mode.")
                            Assert.IsTrue(dialog.Width <= area.Width AndAlso dialog.Height <= area.Height, $"Fitted to the working area {area.Size} on load: {dialog.Size}.")
                        Else
                            Assert.IsTrue(dialog.Width > area.Width OrElse dialog.Height > area.Height, $"Without the rule the window keeps its size: {dialog.Size}.")
                        End If

                        dialog.Close()

                    End Using

                Next

            End Sub
        )

    End Sub


    ' Each converted window (ScalingMeasurementTests.Converted, which the
    ' measuring test enforces) uses the rule and nothing of its own beside
    ' it: no base dimensions, scaling mode or fit of its own, and no
    ' DeviceDpi read, so no value is scaled by hand and by Windows Forms
    ' both.
    <TestMethod>
    Public Sub TheConvertedWindowsUseTheRuleAlone()

        Dim folder As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Forms")

        Assert.IsTrue(ScalingMeasurementTests.Converted.Length >= 14, "The converted windows are listed: " & ScalingMeasurementTests.Converted.Length)

        For Each name As String In ScalingMeasurementTests.Converted.Select(Function(view) view.Name)

            Dim code As String =
                String.Join(Environment.NewLine, File.ReadAllLines(Path.Combine(folder, name & ".vb")).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))

            StringAssert.Contains(code, "DialogScaling.Begin(Me)", name & " begins the rule.")
            StringAssert.Contains(code, "DialogScaling.Finish(Me)", name & " finishes the rule.")

            For Each own As String In {"AutoScaleDimensions", "AutoScaleMode", "FitToWorkingArea", "DeviceDpi", "LogicalToDeviceUnits"}
                Assert.IsFalse(code.Contains(own, StringComparison.Ordinal), name & " has nothing of its own beside the rule: " & own)
            Next

        Next

    End Sub


    ' The rule's Dpi is the one DPI accessor a converted window uses.
    <TestMethod>
    Public Sub DpiIsTheControlsDeviceDpi()

        StaRunner.Run(
            Sub()
                Using probe As New Button()
                    Assert.AreEqual(probe.DeviceDpi, DialogScaling.Dpi(probe))
                End Using
            End Sub
        )

    End Sub


    ' The installed build has run in the application framework's default
    ' mode, SystemAware, which the startup window sets for itself before
    ' the framework runs and the certification harness sets too.
    ' ApplicationEvents.vb now pins the same value, so a change of the
    ' framework's default cannot move the app to another mode unnoticed.
    <TestMethod>
    Public Sub TheAppRunsSystemAware()

        Assert.AreEqual(HighDpiMode.SystemAware, New ProbeApplication().DefaultHighDpiMode(), "The framework's default, which the installed build ran with.")

        Dim app As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")

        StringAssert.Contains(
            Code(Path.Combine(app, "ApplicationEvents.vb")),
            "e.HighDpiMode =" & Environment.NewLine & "                HighDpiMode.SystemAware",
            "ApplicationEvents pins SystemAware."
        )

        StringAssert.Contains(
            Code(Path.Combine(app, "Forms", "StartupFailureForm.vb")),
            "Application.SetHighDpiMode(HighDpiMode.SystemAware)",
            "The startup window sets the same mode before the framework runs."
        )

        Dim others As List(Of String) =
            Directory.GetFiles(app, "*.vb", SearchOption.AllDirectories).
                Where(Function(source) Not source.Contains("\obj\", StringComparison.OrdinalIgnoreCase) AndAlso Not source.Contains("\bin\", StringComparison.OrdinalIgnoreCase)).
                Where(Function(source) Regex.IsMatch(Code(source), "HighDpiMode\.(?!SystemAware)\w+")).
                Select(Function(source) Path.GetFileName(source)).
                ToList()

        Assert.AreEqual(0, others.Count, "No file names another mode: " & String.Join(", ", others))

    End Sub


    Private Shared Function Code(path As String) As String
        Return String.Join(Environment.NewLine, File.ReadAllLines(path).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))
    End Function


    ' The app's framework class, so its protected default can be read.
    Private NotInheritable Class ProbeApplication
        Inherits ManuscriptPipeline.My.MyApplication

        Public Function DefaultHighDpiMode() As HighDpiMode
            Return HighDpiMode
        End Function

    End Class

End Class
