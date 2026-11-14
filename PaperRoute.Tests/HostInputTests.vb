Imports System
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The key state the key helpers set before they post (#340, #318).
<TestClass>
Public Class HostInputTests

    ' WinForms reads the mouse buttons and modifiers from this thread's key
    ' state, so what HostInput sets there is what a posted key meets.
    ' HoldOnly releases the buttons, Tab and modifiers, holds only the
    ' modifiers it is given, and leaves other keys and other threads alone.
    ' It runs on a runner thread, so the test's own thread keeps its key
    ' state.
    <TestMethod>
    Public Sub HoldOnlyReleasesButtonsAndModifiersInThisThreadOnly()

        StaRunner.Run(
            Sub()

                ' F24, which no one at the PC holds, marks this thread's
                ' table for the look from another thread below.
                HostInput.MarkDown(Keys.LButton, Keys.ControlKey, Keys.Tab, Keys.F24)

                Assert.IsTrue((Control.MouseButtons And MouseButtons.Left) = MouseButtons.Left, "WinForms reads the left button from this thread's key state: " & HostInput.Describe() & ".")
                Assert.IsTrue((Control.ModifierKeys And Keys.Control) = Keys.Control, "WinForms reads Ctrl from this thread's key state: " & HostInput.Describe() & ".")

                HostInput.HoldOnly()

                Assert.AreEqual(HostInput.AllReleased, HostInput.Describe(), "HoldOnly releases the buttons, the modifiers and Tab.")
                Assert.IsTrue(HostInput.IsDown(Keys.F24), "HoldOnly leaves other keys as they are.")

                HostInput.HoldOnly(Keys.Shift)

                Assert.AreEqual("buttons None, modifiers Shift, Tab up", HostInput.Describe(), "HoldOnly(Shift) holds Shift alone.")

                HostInput.HoldOnly(Keys.Control Or Keys.Alt)

                Assert.AreEqual("buttons None, modifiers Control, Alt, Tab up", HostInput.Describe(), "HoldOnly(Ctrl, Alt) holds those two alone.")

                Dim elsewhere As Boolean =
                    True

                Dim other As New Thread(
                    Sub()
                        elsewhere = HostInput.IsDown(Keys.F24)
                    End Sub
                )

                other.Start()
                other.Join()

                Assert.IsFalse(elsewhere, "Another thread does not see this thread's key state.")

            End Sub
        )

    End Sub

End Class
