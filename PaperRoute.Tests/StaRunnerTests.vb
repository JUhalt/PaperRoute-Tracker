Imports System
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The one STA runner every UI test uses (#134): a test that never returns
' fails only itself, with the form it left open closed, so the theme tests
' that run after it in the same process start clean.
<TestClass>
<DoNotParallelize>
Public Class StaRunnerTests

    <TestMethod>
    Public Sub TheActionRunsOnAThreadOfItsOwnInTheStaApartment()

        Dim apartment As ApartmentState =
            ApartmentState.Unknown

        Dim threadId As Integer =
            0

        StaRunner.Run(
            Sub()
                apartment = Thread.CurrentThread.GetApartmentState()
                threadId = Environment.CurrentManagedThreadId
            End Sub
        )

        Assert.AreEqual(
            ApartmentState.STA,
            apartment
        )

        Assert.AreNotEqual(
            Environment.CurrentManagedThreadId,
            threadId,
            "The action does not run on the test's own thread."
        )

    End Sub


    ' Whatever the action throws reaches the test as itself: a failed
    ' assertion fails, an inconclusive one is inconclusive, and any other
    ' exception keeps its type and message.
    <TestMethod>
    Public Sub TheActionsOwnFailureComesBackAsItself()

        Assert.ThrowsExactly(Of AssertFailedException)(
            Sub() StaRunner.Run(Sub() Assert.Fail("from the action"))
        )

        Assert.ThrowsExactly(Of AssertInconclusiveException)(
            Sub() StaRunner.Run(Sub() Assert.Inconclusive("from the action"))
        )

        Dim failure As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub() StaRunner.Run(Sub() Throw New InvalidOperationException("from the action"))
            )

        Assert.AreEqual(
            "from the action",
            failure.Message
        )

    End Sub


    ' The cascade #97 recorded: a runner that abandons its thread leaves
    ' that thread's form open, and the next theme test counts it. Here the
    ' action sits in a modal dialog nobody closes, as a stuck message box
    ' would, so it never returns on its own.
    <TestMethod>
    Public Sub ATimedOutActionFailsOnlyItselfAndItsFormIsClosed()

        Dim openBefore As Integer =
            Application.OpenForms.Count

        Dim dialogReturned As Boolean =
            False

        Dim failure As AssertFailedException =
            Assert.ThrowsExactly(Of AssertFailedException)(
                Sub()
                    StaRunner.Run(
                        Sub()
                            Using dialog As Form = Offscreen(New Form With {.Text = "Stuck dialog"})
                                dialog.ShowDialog()
                                dialogReturned = True
                            End Using
                        End Sub,
                        TimeSpan.FromMilliseconds(500)
                    )
                End Sub
            )

        StringAssert.Contains(
            failure.Message,
            "did not finish within",
            "The failure says the test timed out."
        )

        Assert.IsTrue(
            dialogReturned,
            "The runner closed the dialog, so the stuck action returned."
        )

        Assert.AreEqual(
            openBefore,
            Application.OpenForms.Count,
            "Application.OpenForms is as it was."
        )

        ' A theme run after it starts clean, as DarkThemeLegibilityTests and
        ' WorkflowThemeKeyboardSmokeTests need.
        StaRunner.Run(
            Sub()
                Assert.AreEqual(
                    0,
                    StaRunner.OpenFormsOnThisThread().Count,
                    "No form is open on the next test's thread."
                )
            End Sub,
            mode:=SystemColorMode.Dark
        )

    End Sub


    <TestMethod>
    Public Sub AColorModeIsSetForTheActionAndPutBack()

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
        End If

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
        End If

        Dim priorMode As SystemColorMode =
            Application.ColorMode

        Dim darkInside As Boolean =
            False

        StaRunner.Run(
            Sub() darkInside = UiTheme.IsDark(),
            mode:=SystemColorMode.Dark
        )

        Assert.IsTrue(
            darkInside,
            "The action runs in the mode it asked for."
        )

        Assert.AreEqual(
            priorMode,
            Application.ColorMode,
            "The process is back in its prior mode afterwards."
        )

        ' Put back even when the action fails.
        Assert.ThrowsExactly(Of AssertFailedException)(
            Sub() StaRunner.Run(Sub() Assert.Fail("in Dark"), mode:=SystemColorMode.Dark)
        )

        Assert.AreEqual(
            priorMode,
            Application.ColorMode
        )

    End Sub


    ' A form held open on another thread, as a stuck test leaves one, is
    ' not counted among this thread's forms.
    <TestMethod>
    Public Sub OpenFormsOnThisThreadLeavesOutAnotherThreadsForm()

        Using shown As New ManualResetEventSlim(False), release As New ManualResetEventSlim(False)

            Dim other As New Thread(
                Sub()
                    Using form As Form = Offscreen(New Form With {.Text = "Another thread's form"})
                        form.Show()
                        shown.Set()
                        While Not release.IsSet
                            Application.DoEvents()
                            Thread.Sleep(10)
                        End While
                        form.Close()
                    End Using
                End Sub
            ) With {.IsBackground = True}

            other.SetApartmentState(ApartmentState.STA)
            other.Start()
            shown.Wait()

            Try

                StaRunner.Run(
                    Sub()
                        Assert.IsTrue(
                            Application.OpenForms.Count >= 1,
                            "The process as a whole sees the other thread's form."
                        )

                        Assert.AreEqual(
                            0,
                            StaRunner.OpenFormsOnThisThread().Count,
                            "This thread has opened nothing yet."
                        )

                        Using mine As Form = Offscreen(New Form With {.Text = "This thread's form"})
                            mine.Show()
                            Application.DoEvents()

                            Assert.AreEqual(
                                1,
                                StaRunner.OpenFormsOnThisThread().Count,
                                "Only the form this thread opened is counted."
                            )

                            mine.Close()
                        End Using
                    End Sub
                )

            Finally

                release.Set()
                other.Join(TimeSpan.FromSeconds(10))

            End Try

        End Using

    End Sub


    Private Shared Function Offscreen(
        form As Form
    ) As Form

        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        Return form

    End Function

End Class
