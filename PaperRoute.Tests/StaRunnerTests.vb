Imports System
Imports System.Drawing
Imports System.Runtime.InteropServices
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

        ' The count the theme runners failed on in #97.
        Assert.AreEqual(
            openBefore,
            Application.OpenForms.Count,
            "Application.OpenForms is as it was."
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


    ' The app turns visual styles on before its first window, so its lists
    ' are comctl32 6 lists, which show groups; the runner does the same,
    ' with or without a color mode. WinForms registers one window class per
    ' kind of control per process, from the comctl32 active when the first
    ' such control is created: a list the accessibility sweeps showed
    ' without visual styles made every later list in the process a comctl32
    ' 5 list, which has no groups, and Dark's group headings then failed.
    <TestMethod>
    Public Sub AListShownWithoutAColorModeShowsItsGroupsAsInTheApp()

        Dim styled As Boolean =
            False

        Dim grouped As Boolean =
            False

        StaRunner.Run(
            Sub()

                styled = Application.RenderWithVisualStyles

                Using host As Form = Offscreen(New Form With {.Text = "A grouped list"})

                    Dim list As New ListView With {
                        .View = View.Details,
                        .Dock = DockStyle.Fill
                    }

                    list.Columns.Add("Work", 200)

                    Dim group As New ListViewGroup("On your ORCID record")
                    list.Groups.Add(group)
                    list.Items.Add(New ListViewItem("A work") With {.Group = group})

                    host.Controls.Add(list)
                    host.Show()
                    Application.DoEvents()

                    grouped = SendMessage(list.Handle, LVM_ISGROUPVIEWENABLED, IntPtr.Zero, IntPtr.Zero) <> IntPtr.Zero

                    host.Close()

                End Using

            End Sub
        )

        Assert.IsTrue(
            styled,
            "The action runs with visual styles on, as the app does."
        )

        Assert.IsTrue(
            grouped,
            "The list shows its groups, as a comctl32 6 list does."
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


    ' Whether a list view shows its items in groups (commctrl.h); comctl32
    ' 5 has no groups and answers no.
    Private Const LVM_ISGROUPVIEWENABLED As Integer =
        &H10AF

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

End Class
