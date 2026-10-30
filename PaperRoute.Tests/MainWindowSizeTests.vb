Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' The main window's start and minimum size (#93, step 0). It opened at
' 1280x840 with a 960x680 minimum, unscaled and with no screen cap, so on
' a 1366x768 laptop the first window a new user saw was taller than the
' 720-pixel working area even at 100%. Both sizes now scale with the
' display, and the start size is capped to the working area as the
' dialogs' are. The content's own hand scaling is unchanged.
<TestClass>
<DoNotParallelize>
Public Class MainWindowSizeTests

    ' A 1366x768 laptop at 100%: the window fits the 720-pixel working
    ' area Windows 11's taskbar leaves, no smaller than its minimum, which
    ' is unchanged.
    <TestMethod>
    Public Sub FitsA1366x720WorkingAreaAt96Dpi()

        Dim area As New Rectangle(0, 0, 1366, 720)

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(area, 96)

        Assert.AreEqual(New Size(960, 680), sizes.Minimum, "The 96-DPI minimum is unchanged.")
        AssertInside(area, sizes.Start)
        AssertAtLeast(sizes.Minimum, sizes.Start)

    End Sub


    ' The certification VM: 1920x1080 at 150%, a 1008-pixel working area.
    ' The minimum scales by 1.5 to 1440 wide; its 1020 height is more
    ' than the screen has, so it is lowered to the screen.
    <TestMethod>
    Public Sub FitsA1920x1008WorkingAreaAt144Dpi()

        Dim area As New Rectangle(0, 0, 1920, 1008)

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(area, 144)

        AssertInside(area, sizes.Start)
        AssertAtLeast(sizes.Minimum, sizes.Start)
        Assert.AreEqual(1440, sizes.Minimum.Width, "The minimum width is the 96-DPI one scaled by 1.5.")
        Assert.IsTrue(sizes.Minimum.Height <= area.Height, "A minimum taller than the screen is lowered to it: " & sizes.Minimum.Height)
        Assert.IsTrue(sizes.Start.Width >= 1800, "The window is as wide as the screen allows, not its 96-DPI 1280: " & sizes.Start.Width)

    End Sub


    ' Where the window fits, nothing changes: 1280x840 and 960x680.
    <TestMethod>
    Public Sub KeepsItsSizesOnARoomyScreen()

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(New Rectangle(0, 0, 2560, 1400), 96)

        Assert.AreEqual(New Size(1280, 840), sizes.Start)
        Assert.AreEqual(New Size(960, 680), sizes.Minimum)

    End Sub


    ' The window the catalog builds opens at the computed size for one of
    ' this computer's screens, so BuildInterface applies the sizes and not
    ' the old literals. On a roomy screen both agree, so this cannot fail
    ' before the change there; the three tests above carry the proof. The
    ' catalog builds it on a temporary root, as it builds every view, so
    ' the window never reads the user's own data folder.
    <TestMethod>
    Public Sub TheBoardOpensAtTheComputedSize()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {("The Board", ViewCatalog.Dialogs(GetType(Form1)))},
                Sub(name, board)

                    Dim expected As String() =
                        Screen.AllScreens.
                            Select(Function(screen) Form1.StartSizes(screen.WorkingArea, board.DeviceDpi).Start.ToString()).
                            Distinct().
                            ToArray()

                    Assert.IsTrue(
                        expected.Contains(board.Size.ToString()),
                        "The Board opened at " & board.Size.ToString() & "; computed for this computer's screens: " & String.Join(", ", expected)
                    )

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub


    ' The window built as Form1_Load builds it has the minimum StartSizes
    ' gives, Px(960) wide on a screen that holds it, and opens at its start
    ' size inside the screen's working area, with its text at 100%, 125%
    ' and 150% (#259). Load builds the window once its handle exists, and
    ' BuildInterface set its font while the designer's Font scaling still
    ' held: Windows Forms scaled the window by its text's growth from 9 to
    ' 10 points, its size and minimum with it. At 125% the average
    ' character grows from 8 to 9 pixels, so the minimum was (1200 - 16) x
    ' 9/8 + 16 = 1348 pixels wide, as C3 measured, and the start size,
    ' capped to the screen, grew past it. At 100% the width does not grow,
    ' but the height does: 680 became 765, and the start size's 840 947.
    ' The catalog's Board builds the window before it has a handle, which
    ' does not scale, so the test above cannot see this. The host is at 96
    ' DPI, so 125% and 150% are the display's text: every font the window
    ' starts with or is given grown by the scale (GrownTextBoard), which is
    ' what the Font scaling measures; the window's own DPI stays 96, so the
    ' sizes it should keep are 96's.
    <TestMethod>
    Public Sub TheMinimumIsPx960WideAt96To144Dpi()

        Dim found As New List(Of String)()
        Dim judged As Integer = 0

        For Each scale As Single In {1.0F, 1.25F, 1.5F}

            Dim textScale As Single = scale

            Dim failures As List(Of String) =
                ViewCatalog.ShowEach(
                    {("The main window with its text at " & textScale.ToString("P0"),
                      CType(Function(root As String)
                                Dim board As New GrownTextBoard(textScale)
                                board.BuildAsLoadDoes()
                                Return board
                            End Function, Func(Of String, Form)))},
                    Sub(name, board)

                        Dim expected As List(Of (Area As Rectangle, Sizes As (Start As Size, Minimum As Size))) =
                            Screen.AllScreens.Select(Function(screen) (Area:=screen.WorkingArea, Sizes:=Form1.StartSizes(screen.WorkingArea, board.DeviceDpi))).ToList()

                        Dim match = expected.FirstOrDefault(Function(item) item.Sizes.Minimum = board.MinimumSize AndAlso item.Sizes.Start = board.Size)

                        judged += 1

                        If match.Area.IsEmpty Then
                            found.Add($"{name}: opened at {board.Size} with a minimum of {board.MinimumSize}; StartSizes gives {String.Join(", ", expected.Select(Function(item) item.Sizes.Start.ToString() & " and " & item.Sizes.Minimum.ToString()).Distinct())}.")
                            Return
                        End If

                        If match.Area.Width >= UiTheme.Px(960, board.DeviceDpi) AndAlso board.MinimumSize.Width <> UiTheme.Px(960, board.DeviceDpi) Then
                            found.Add($"{name}: a minimum {board.MinimumSize.Width} pixels wide, not {UiTheme.Px(960, board.DeviceDpi)}.")
                        End If

                        If board.Width > match.Area.Width OrElse board.Height > match.Area.Height Then
                            found.Add($"{name}: {board.Size} is larger than the {match.Area.Size} working area.")
                        End If

                    End Sub)

            found.AddRange(failures)

        Next

        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(3, judged, "The window was judged at each text size.")

    End Sub


    ' The main window with its text at a display's size: the font it has
    ' when Load builds it, the default font at that display, and every font
    ' it is then given, grown by the scale. The Font scaling measures the
    ' window's text from the first. Load does nothing; BuildAsLoadDoes
    ' builds the window as Form1_Load does, once the window has its handle.
    Private NotInheritable Class GrownTextBoard
        Inherits Form1

        Private ReadOnly _scale As Single

        Public Sub New(scale As Single)
            _scale = scale
        End Sub

        Public Overrides Property Font As Font
            Get
                Return MyBase.Font
            End Get
            Set(value As Font)
                MyBase.Font = If(_scale = 0, value, New Font(value.FontFamily, value.Size * _scale, value.Style))
            End Set
        End Property

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub BuildAsLoadDoes()
            CreateHandle()
            MyBase.Font = New Font(MyBase.Font.FontFamily, MyBase.Font.Size * _scale)
            WindowChrome.Apply(Me)
            GetType(Form1).GetMethod("BuildInterface", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
        End Sub

    End Class


    Private Shared Sub AssertInside(area As Rectangle, size As Size)
        Assert.IsTrue(
            size.Width <= area.Width AndAlso size.Height <= area.Height,
            size.ToString() & " is inside the " & area.Width & "x" & area.Height & " working area."
        )
    End Sub


    Private Shared Sub AssertAtLeast(minimum As Size, size As Size)
        Assert.IsTrue(
            size.Width >= minimum.Width AndAlso size.Height >= minimum.Height,
            size.ToString() & " is at or above the minimum " & minimum.ToString() & "."
        )
    End Sub

End Class
