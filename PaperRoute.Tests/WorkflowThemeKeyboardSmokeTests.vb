Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class WorkflowThemeKeyboardSmokeTests

    Public Property TestContext As TestContext

    Private Shared ReadOnly DialogKeyMethod As MethodInfo =
        GetType(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance Or BindingFlags.NonPublic)

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub WorkflowSurfaces_KeepTextLegibleAndKeyboardActionsAvailable(mode As SystemColorMode)

        RunWithColorMode(mode,
            Sub()
                ReportNativeSelectionContrast(TestContext)
                Using fixture As New SmokeFixture()
                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        AssertTextContrast(dialog)
                        AssertTabReachability(dialog, New Control() {
                            Descendants(dialog).OfType(Of ComboBox)().Single(),
                            Descendants(dialog).OfType(Of ListBox)().Single(),
                            NamedControl(dialog, "Save readiness and navigate"),
                            NamedControl(dialog, "Open selected journal submission portal"),
                            DirectCast(dialog.AcceptButton, Control), DirectCast(dialog.CancelButton, Control)
                        })
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                        Assert.IsNull(dialog.RequestedNavigation)
                    End Using

                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        Dim fileList As ListBox = FileListIn(dialog)
                        Assert.IsTrue(fileList.Items.Cast(Of Object)().Any(Function(item) item.ToString().Contains("[Not checked]")))
                        PumpUntilComplete(dialog.CheckPacketFilesAsync())
                        For Each state As String In {"Metadata only", "No fingerprint", "Unchanged", "Changed", "Missing", "Unavailable"}
                            Dim index As Integer = Enumerable.Range(0, fileList.Items.Count).First(
                                Function(item) fileList.Items(item).ToString().Contains("[" & state & "]"))
                            fileList.SelectedIndex = index
                            Application.DoEvents()
                            Assert.IsTrue(NamedControl(dialog, "Selected file details").Text.Contains("Status: " & state),
                                "Integrity status must remain readable as text, not depend on a color.")
                            AssertTextContrast(dialog)
                        Next

                        SelectFile(fileList, "04 unchanged fixture")
                        ' A nonactivating form cannot faithfully reproduce native
                        ' focus transitions between LinkLabel and SplitContainer.
                        ' Verify eligibility here; exercise the full vault tab
                        ' sequence on the desktop during manual certification.
                        AssertKeyboardEligibility(New Control() {
                            fileList,
                            NamedControl(dialog, "Selected packet details").Parent.Controls.OfType(Of ListBox)().Single(),
                            NamedControl(dialog, "Show all manuscript packets"),
                            NamedControl(dialog, "Save packet changes and go to related record"),
                            NamedControl(dialog, "Check files in selected packet"),
                            NamedControl(dialog, "Record or replace selected file fingerprint"),
                            DirectCast(dialog.AcceptButton, Control), DirectCast(dialog.CancelButton, Control)
                        })
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                        Assert.IsNull(dialog.RequestedNavigation)
                    End Using

                    Using dialog As New SubmissionProbe(fixture)
                        ShowInvisible(dialog)
                        AssertTextContrast(dialog)
                        ' Submission Details also contains a native focus-managing splitter.
                        AssertKeyboardEligibility(New Control() {
                            NamedControl(dialog, "View packets for this submission"),
                            DirectCast(dialog.AcceptButton, Control)
                        })
                        dialog.Close()
                    End Using
                End Using
            End Sub)

    End Sub

    <TestMethod>
    Public Sub FormDefaultEnterAndEscape_KeepReadinessAndFingerprintSaveBoundaries()

        RunWithColorMode(SystemColorMode.Classic,
            Sub()
                Using fixture As New SmokeFixture()
                    Dim original As String = Snapshot(fixture.Manuscript)
                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        ButtonWithText(dialog, "Mark Complete").PerformClick()
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(original, Snapshot(fixture.Manuscript))

                    Using dialog As New ReadinessProbe(fixture)
                        ShowInvisible(dialog)
                        ButtonWithText(dialog, "Mark Complete").PerformClick()
                        dialog.ActiveControl = Descendants(dialog).OfType(Of ListBox)().Single()
                        Assert.IsTrue(dialog.Press(Keys.Enter))
                        Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(ReadinessItemStatus.Complete, fixture.Manuscript.ReadinessProfiles(0).Items(0).Status)
                    Assert.AreEqual(PaperStage.Submitted, fixture.Manuscript.CurrentStage)
                    Assert.AreEqual(1, fixture.Manuscript.Submissions.Count)

                    Dim fileId As Guid = fixture.NoFingerprint.Id
                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        SelectFile(FileListIn(dialog), "03 no fingerprint fixture")
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))
                        Assert.IsTrue(dialog.Press(Keys.Escape))
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(String.Empty, fixture.Manuscript.SubmissionPackets(0).Files.Single(Function(item) item.Id = fileId).Sha256)

                    Using dialog As New VaultProbe(fixture)
                        ShowInvisible(dialog)
                        Dim fileList As ListBox = FileListIn(dialog)
                        SelectFile(fileList, "03 no fingerprint fixture")
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))
                        dialog.ActiveControl = fileList
                        Assert.IsTrue(dialog.Press(Keys.Enter))
                        Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    End Using
                    Assert.AreEqual(64, fixture.Manuscript.SubmissionPackets(0).Files.Single(Function(item) item.Id = fileId).Sha256.Length)
                    Assert.AreEqual("abc", File.ReadAllText(fixture.NoFingerprint.LocalFilePath),
                        "Keyboard save must adopt only fingerprint metadata, never alter the source fixture.")
                End Using
            End Sub)

    End Sub

    Private Shared Sub RunWithColorMode(mode As SystemColorMode, action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Assert.AreEqual(0, Application.OpenForms.Count, "Theme smoke tests require no existing forms in the test process.")
                    If SystemInformation.HighContrast Then
                        Assert.Inconclusive("Windows high-contrast themes require separate certification; these checks do not change OS settings.")
                    End If
                    If mode = SystemColorMode.Dark AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
                        Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
                    End If
                    ' This changes only the test process, before constructing its forms.
                    ' The current Windows theme and all saved application settings are untouched.
                    Application.EnableVisualStyles()
                    Application.SetColorMode(mode)
                    Dim expectedDark As Boolean = mode = SystemColorMode.Dark OrElse
                        (mode = SystemColorMode.System AndAlso Application.SystemColorMode = SystemColorMode.Dark)
                    Assert.AreEqual(expectedDark, UiTheme.IsDark())
                    action()
                Catch ex As Exception
                    failure = ex
                Finally
                    Try
                        Application.SetColorMode(priorMode)
                    Catch ex As Exception
                        If failure Is Nothing Then failure = ex
                    End Try
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "The invisible theme/keyboard smoke test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private Shared Sub ShowInvisible(dialog As Form)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        dialog.Size = dialog.MinimumSize
        dialog.Location = New Point(-20000, -20000)
        dialog.PerformLayout()
        Application.DoEvents()
    End Sub

    Private Shared Sub AssertTabReachability(dialog As Form, required As IEnumerable(Of Control))
        AssertKeyboardEligibility(required)
        For Each backwards As Boolean In {False, True}
            dialog.ActiveControl = Nothing
            Dim visited As New HashSet(Of Control)()
            Dim sequence As New List(Of String)()
            For count As Integer = 0 To Descendants(dialog).Count() * 2 + 2
                Assert.IsTrue(DispatchTabFromActiveControl(dialog, If(backwards, Keys.Shift Or Keys.Tab, Keys.Tab)))
                Dim active As Control = DeepestActiveControl(dialog)
                Assert.IsNotNull(active, "Tab navigation must select a control without activating a visible window.")
                sequence.Add(active.GetType().Name & ": " & active.AccessibleName & " " & active.Text)
                If Not visited.Add(active) Then Exit For
            Next
            For Each control As Control In required
                Assert.IsTrue(visited.Contains(control),
                    If(backwards, "Shift+Tab", "Tab") & " could not reach " & control.AccessibleName & " " & control.Text &
                    "; visited: " & String.Join(" -> ", sequence))
            Next
        Next
    End Sub

    Private Shared Sub AssertKeyboardEligibility(required As IEnumerable(Of Control))
        For Each control As Control In required
            Assert.IsTrue(control.Visible AndAlso control.Enabled AndAlso control.TabStop AndAlso control.CanSelect,
                "The required keyboard action must be selectable: " & control.AccessibleName & " " & control.Text)
        Next
    End Sub

    Private Shared Function DeepestActiveControl(dialog As Form) As Control
        Dim active As Control = dialog.ActiveControl
        While TypeOf active Is ContainerControl AndAlso DirectCast(active, ContainerControl).ActiveControl IsNot Nothing
            active = DirectCast(active, ContainerControl).ActiveControl
        End While
        Return active
    End Function

    Private Shared Function DispatchTabFromActiveControl(dialog As Form, key As Keys) As Boolean
        ' Tab must start at the active child and bubble through its containers.
        ' Form-only routing skips focus-managing SplitContainer children. Calling
        ' the virtual managed handler also avoids physical keyboard modifier state.
        Dim target As Control = If(DeepestActiveControl(dialog), dialog)
        Try
            Return CBool(DialogKeyMethod.Invoke(target, New Object() {key}))
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function

    Private Shared Sub AssertTextContrast(dialog As Form)
        For Each control As Control In Descendants(dialog).Where(Function(item) item.Visible AndAlso item.Enabled)
            If TypeOf control Is Button Then
                Dim button As Button = DirectCast(control, Button)
                AssertContrast(button.ForeColor, button.BackColor, button.Text)
                AssertContrast(button.ForeColor, button.FlatAppearance.MouseOverBackColor, button.Text & " hovered")
            ElseIf TypeOf control Is LinkLabel Then
                Dim link As LinkLabel = DirectCast(control, LinkLabel)
                AssertContrast(link.LinkColor, EffectiveBackground(link), link.Text)
                AssertContrast(link.ActiveLinkColor, EffectiveBackground(link), link.Text & " active")
                AssertContrast(link.VisitedLinkColor, EffectiveBackground(link), link.Text & " visited")
            ElseIf TypeOf control Is TextBox OrElse TypeOf control Is ListBox OrElse TypeOf control Is ComboBox OrElse
                (TypeOf control Is Label AndAlso Not String.IsNullOrWhiteSpace(control.Text)) Then
                AssertContrast(control.ForeColor, EffectiveBackground(control), control.AccessibleName & " " & control.Text)
            End If
        Next
    End Sub

    Private Shared Sub ReportNativeSelectionContrast(context As TestContext)
        Dim foreground As Color = SystemColors.HighlightText
        Dim background As Color = SystemColors.Highlight
        Dim ratio As Double = ContrastRatio(foreground, background)
        ' Native selected-row colors are provided by Windows. Record the actual
        ' ratio without weakening the app-owned palette assertions or rounding it
        ' into a pass. Native selection rendering remains a manual release gate.
        context.WriteLine($"Native selected-row observation: {ratio:F5}:1 ({foreground} on {background}). " &
            "Windows-owned colors; verify native selection readability during manual certification.")
    End Sub

    Private Shared Function EffectiveBackground(control As Control) As Color
        While control.BackColor.A = 0 AndAlso control.Parent IsNot Nothing
            control = control.Parent
        End While
        Return control.BackColor
    End Function

    Private Shared Sub AssertContrast(foreground As Color, background As Color, label As String)
        Dim ratio As Double = ContrastRatio(foreground, background)
        Assert.IsTrue(ratio >= 4.5,
            $"Normal-size text needs 4.5:1 contrast; '{label.Substring(0, Math.Min(90, label.Length))}' has {ratio:F2}:1 ({foreground} on {background}).")
    End Sub

    Private Shared Function ContrastRatio(foreground As Color, background As Color) As Double
        Dim first As Double = Luminance(foreground)
        Dim second As Double = Luminance(background)
        Return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05)
    End Function

    Private Shared Function Luminance(color As Color) As Double
        Return 0.2126 * LinearChannel(color.R) + 0.7152 * LinearChannel(color.G) + 0.0722 * LinearChannel(color.B)
    End Function

    Private Shared Function LinearChannel(channel As Byte) As Double
        Dim value As Double = channel / 255.0
        Return If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))
    End Function

    Private Shared Function NamedControl(dialog As Form, name As String) As Control
        Return Descendants(dialog).Single(Function(item) item.AccessibleName = name)
    End Function

    Private Shared Function ButtonWithText(dialog As Form, text As String) As Button
        Return Descendants(dialog).OfType(Of Button)().Single(Function(item) item.Text = text)
    End Function

    Private Shared Function FileListIn(dialog As Form) As ListBox
        Return NamedControl(dialog, "Selected file details").Parent.Controls.OfType(Of ListBox)().Single()
    End Function

    Private Shared Sub SelectFile(list As ListBox, label As String)
        list.SelectedIndex = Enumerable.Range(0, list.Items.Count).Single(Function(index) list.Items(index).ToString().Contains(label))
        Application.DoEvents()
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The disposable integrity check timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private NotInheritable Class SmokeFixture
        Implements IDisposable

        Private ReadOnly _root As String = CreateTemporaryRoot()
        Private ReadOnly _lockedFile As FileStream
        Public ReadOnly Property Manuscript As New Manuscript With {.Title = "Invisible theme and keyboard fixture", .CurrentStage = PaperStage.Submitted}
        Public ReadOnly Property Library As New AuthorLibraryData()
        Public ReadOnly Property Profile As ManuscriptReadiness
        Public ReadOnly Property Packet As SubmissionPacket
        Public ReadOnly Property Submission As JournalSubmission
        Public ReadOnly Property NoFingerprint As SubmissionPacketFile

        Public Sub New()
            Dim journal As New JournalRecord With {.Name = "Synthetic theme journal", .SubmissionPortalUrl = "https://theme.example.invalid/submit"}
            Library.Journals.Add(journal)
            Profile = New ManuscriptReadiness With {.JournalId = journal.Id, .JournalName = journal.Name}
            For index As Integer = 0 To 3
                Profile.Items.Add(New ReadinessItemState With {
                    .Title = "Requirement " & index, .Description = "Readable preparation instructions.",
                    .UserNotes = "Disposable notes remain available with keyboard navigation.", .SortOrder = index
                })
            Next
            Manuscript.ReadinessProfiles.Add(Profile)
            Dim version As New ManuscriptVersion With {.Label = "Exact keyboard fixture version"}
            Manuscript.Versions.Add(version)
            Manuscript.CurrentVersionId = version.Id
            Submission = New JournalSubmission With {
                .JournalName = journal.Name, .JournalId = journal.Id, .ManuscriptNumber = "DEMO-THEME-1",
                .PortalUrl = journal.SubmissionPortalUrl, .Notes = "Fictional submission with no external activity."
            }
            Manuscript.Submissions.Add(Submission)
            Packet = SubmissionPacketService.CreatePacket(Manuscript, version.Id, "Themed packet", "Disposable sample", Profile.Id, Submission.Id)
            Packet.Files.Add(New SubmissionPacketFile With {.Label = "01 metadata fixture", .Notes = "Metadata-only packet record."})
            Packet.Files.Add(New SubmissionPacketFile With {
                .Label = "02 missing fixture", .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                .LocalFilePath = Path.Combine(_root, "missing.txt")
            })
            NoFingerprint = AddLocal("03 no fingerprint fixture", False)
            AddLocal("04 unchanged fixture", True)
            Dim changed As SubmissionPacketFile = AddLocal("05 changed fixture", True)
            File.WriteAllText(changed.LocalFilePath, "xyz")
            Dim unavailable As SubmissionPacketFile = AddLocal("06 unavailable fixture", True)
            _lockedFile = New FileStream(unavailable.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        End Sub

        Private Function AddLocal(label As String, capture As Boolean) As SubmissionPacketFile
            Dim path As String = System.IO.Path.Combine(_root, label & ".txt")
            File.WriteAllText(path, "abc")
            Dim packetFile As New SubmissionPacketFile With {
                .Label = label, .LocalFilePath = path, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
            }
            If capture Then Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.CaptureBaseline(packetFile).Status)
            Packet.Files.Add(packetFile)
            Return packetFile
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _lockedFile?.Dispose()
            DeleteTemporaryRoot(_root)
        End Sub
    End Class

    Private Interface IKeyboardProbe
        Function Press(key As Keys) As Boolean
    End Interface

    Private Class ReadinessProbe
        Inherits ManuscriptReadinessForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, fixture.Library, New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Readiness, .ReadinessProfileId = fixture.Profile.Id
            })
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class VaultProbe
        Inherits SubmissionPacketVaultForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Packets, .PacketId = fixture.Packet.Id, .VersionId = fixture.Packet.ManuscriptVersionId
            })
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class SubmissionProbe
        Inherits SubmissionDetailsForm
        Implements IKeyboardProbe
        Public Sub New(fixture As SmokeFixture)
            MyBase.New(fixture.Manuscript, fixture.Submission, True)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean Implements IKeyboardProbe.Press
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

End Class
