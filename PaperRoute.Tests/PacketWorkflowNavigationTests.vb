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
Public Class PacketWorkflowNavigationTests

    <TestMethod>
    <DataRow("minimum")>
    <DataRow("default")>
    <DataRow("expanded")>
    Public Sub ContextualVault_ResizingKeepsListsDetailsAndWorkflowActionsUsable(sizeName As String)

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim longNotes As String = String.Join(Environment.NewLine,
                    Enumerable.Repeat("Long manuscript preparation notes remain available without displacing the lists or actions.", 40))
                For Each packet As SubmissionPacket In manuscript.SubmissionPackets
                    packet.Notes = longNotes
                    For index As Integer = 1 To 6
                        packet.Files.Add(New SubmissionPacketFile With {
                            .Label = "Long descriptive packet file label for checking list scrolling and layout " & index.ToString(),
                            .Notes = longNotes,
                            .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
                        })
                    Next
                Next

                ' All three supported filters exercise the longest scope heading.
                Using dialog As New NonactivatingVault(manuscript, ContextFor(manuscript.SubmissionPackets(0)))
                    ShowOffscreen(dialog)
                    Dim defaultSize As Size = dialog.Size
                    Select Case sizeName
                        Case "minimum"
                            dialog.Size = dialog.MinimumSize
                        Case "expanded"
                            dialog.Size = New Size(defaultSize.Width + 320, defaultSize.Height + 240)
                        Case "default"
                            dialog.Size = defaultSize
                        Case Else
                            Assert.Fail("Unknown contextual layout size: " & sizeName)
                    End Select
                    dialog.PerformLayout()
                    Application.DoEvents()
                    AssertWorkflowGeometry(dialog)

                    Dim scope As Label = ControlsIn(dialog).OfType(Of Label)().Single(
                        Function(item) item.AccessibleName = "Packet workflow scope")
                    Dim showAll As LinkLabel = ControlsIn(dialog).OfType(Of LinkLabel)().Single(
                        Function(item) item.AccessibleName = "Show all manuscript packets")
                    AssertFullyVisible(dialog, scope)
                    AssertFullyVisible(dialog, showAll)

                    ' Removing the scope and switching packets must also release
                    ' header height, rather than retaining an earlier wrapped size.
                    dialog.ShowAllWorkflowPackets()
                    PacketList(dialog).SelectedIndex = PacketList(dialog).Items.Count - 1
                    dialog.PerformLayout()
                    Application.DoEvents()
                    AssertWorkflowGeometry(dialog)
                    AssertFullyVisible(dialog, scope)
                    Assert.IsFalse(showAll.Visible)
                    dialog.CancelButton.PerformClick()
                    dialog.Close()
                End Using
            End Sub)

    End Sub

    <TestMethod>
    Public Sub Scope_IntersectsExactIdsAndDetailsIdentifySelectedRecords()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
                Dim context As SubmissionWorkflowRequest = ContextFor(packet)
                ' PacketId is only a selection hint; it cannot override the filters.
                context.PacketId = manuscript.SubmissionPackets(1).Id

                Using dialog As New NonactivatingVault(manuscript, context)
                    ShowOffscreen(dialog)
                    Assert.AreEqual(1, PacketList(dialog).Items.Count)
                    Dim detail As String = PacketDetail(dialog).Text
                    Assert.IsTrue(detail.Contains("Exact packet version"))
                    Assert.IsTrue(detail.Contains("Readiness: Same Journal"))
                    Assert.IsFalse(detail.Contains(packet.ManuscriptVersionId.ToString()))
                    Assert.IsTrue(detail.Contains("MS-EXACT-1"))
                    Assert.IsTrue(detail.Contains(New DateTime(2026, 8, 10).ToShortDateString()))
                    Assert.IsTrue(detail.Contains("Revision round: 2"))
                    Assert.IsTrue(ControlsIn(dialog).OfType(Of Label)().Single(
                        Function(item) item.AccessibleName = "Packet workflow scope").Text.Contains("this exact version"))
                    Assert.IsTrue(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Version))
                    Assert.AreEqual(packet.Id, dialog.RequestedNavigation.PacketId.Value)
                    Assert.AreEqual(4, manuscript.SubmissionPackets.Count,
                        "Saving a filtered vault must preserve all hidden packets.")
                    dialog.Close()
                End Using
            End Sub)

    End Sub

    <TestMethod>
    Public Sub PacketId_SelectsWithoutFilteringAndShowAllDoesNotChangeData()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim expected As String = Snapshot(manuscript)
                Dim selected As SubmissionPacket = manuscript.SubmissionPackets(1)

                Using dialog As New NonactivatingVault(manuscript,
                    New SubmissionWorkflowRequest With {.PacketId = selected.Id})
                    ShowOffscreen(dialog)
                    Assert.AreEqual(4, PacketList(dialog).Items.Count)
                    Assert.IsTrue(PacketDetail(dialog).Text.Contains("MS-EXACT-2"))
                    dialog.CancelButton.PerformClick()
                    dialog.Close()
                End Using

                Using dialog As New NonactivatingVault(manuscript, ContextFor(manuscript.SubmissionPackets(0)))
                    ShowOffscreen(dialog)
                    Assert.AreEqual(1, PacketList(dialog).Items.Count)
                    dialog.ShowAllWorkflowPackets()
                    Assert.AreEqual(4, PacketList(dialog).Items.Count)
                    Assert.IsTrue(ControlsIn(dialog).OfType(Of Label)().Single(
                        Function(item) item.AccessibleName = "Packet workflow scope").Text.Contains("all manuscript packets"))
                    dialog.CancelButton.PerformClick()
                    Assert.IsNull(dialog.RequestedNavigation)
                    dialog.Close()
                End Using

                Assert.AreEqual(expected, Snapshot(manuscript))
            End Sub)

    End Sub

    <TestMethod>
    <DataRow(SubmissionWorkflowTarget.Manuscript)>
    <DataRow(SubmissionWorkflowTarget.Version)>
    <DataRow(SubmissionWorkflowTarget.Readiness)>
    <DataRow(SubmissionWorkflowTarget.Submission)>
    Public Sub Navigation_CapturesSelectedPacketIdsWithoutChangingLifecycle(target As SubmissionWorkflowTarget)

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim expected As String = Snapshot(manuscript)
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)

                Using dialog As New NonactivatingVault(manuscript, New SubmissionWorkflowRequest With {.PacketId = packet.Id})
                    ShowOffscreen(dialog)
                    Assert.IsTrue(dialog.RequestWorkflowNavigation(target))
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Dim request As SubmissionWorkflowRequest = dialog.RequestedNavigation
                    Assert.AreEqual(target, request.Target)
                    Assert.AreEqual(packet.Id, request.PacketId.Value)
                    Assert.AreEqual(packet.ReadinessProfileId, request.ReadinessProfileId)
                    Assert.AreEqual(packet.ManuscriptVersionId, request.VersionId.Value)
                    Assert.AreEqual(packet.SubmissionId, request.SubmissionId)
                    Assert.AreNotEqual(manuscript.CurrentVersionId, request.VersionId,
                        "Exact Version must retain the packet's version even when another version is current.")
                    dialog.Close()
                End Using

                Assert.AreEqual(expected, Snapshot(manuscript))
            End Sub)

    End Sub

    <TestMethod>
    Public Sub RecordSubmission_ReturnsOnlyAnIntentForAnUnlinkedPacket()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim expected As String = Snapshot(manuscript)
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(3)

                Using dialog As New NonactivatingVault(manuscript, New SubmissionWorkflowRequest With {.PacketId = packet.Id})
                    ShowOffscreen(dialog)
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Submission))
                    Assert.IsTrue(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.RecordSubmission))
                    Assert.IsFalse(dialog.RequestedNavigation.SubmissionId.HasValue)
                    Assert.AreEqual(packet.Id, dialog.RequestedNavigation.PacketId.Value)
                    dialog.Close()
                End Using

                Assert.AreEqual(expected, Snapshot(manuscript),
                    "The navigation request must not invent a submission, date, stage, or association.")
            End Sub)

    End Sub

    <TestMethod>
    Public Sub MissingLinks_AreDisplayedAndCannotNavigateToAnotherRecordWithSameJournal()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
                packet.ReadinessProfileId = Guid.NewGuid()
                packet.SubmissionId = Guid.NewGuid()
                packet.ManuscriptVersionId = Guid.NewGuid()
                Dim expected As String = Snapshot(manuscript)

                Using dialog As New NonactivatingVault(manuscript, New SubmissionWorkflowRequest With {.PacketId = packet.Id})
                    ShowOffscreen(dialog)
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Version))
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Readiness))
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Submission))
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.RecordSubmission))
                    Assert.IsNull(dialog.RequestedNavigation)
                    Assert.IsTrue(PacketDetail(dialog).Text.Contains("(Missing version)"))
                    Assert.IsTrue(PacketDetail(dialog).Text.Contains("(Missing profile)"))
                    Assert.IsTrue(PacketDetail(dialog).Text.Contains("(Missing record)"))
                    dialog.CancelButton.PerformClick()
                    dialog.Close()
                End Using

                Assert.AreEqual(expected, Snapshot(manuscript))
            End Sub)

    End Sub

    <TestMethod>
    Public Sub IntegrityBusy_RefusesNavigationAndScopeChanges()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim expected As String = Snapshot(manuscript)

                Using dialog As New NonactivatingVault(manuscript, ContextFor(manuscript.SubmissionPackets(0)))
                    ShowOffscreen(dialog)
                    ' Fix the busy boundary deterministically without relying on disk speed.
                    Dim busy As FieldInfo = GetType(SubmissionPacketVaultForm).GetField(
                        "_integrityBusy", BindingFlags.Instance Or BindingFlags.NonPublic)
                    busy.SetValue(dialog, True)
                    Try
                        Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Manuscript))
                        Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Version))
                        dialog.ShowAllWorkflowPackets()
                        Assert.AreEqual(1, PacketList(dialog).Items.Count)
                        Assert.IsNull(dialog.RequestedNavigation)
                        Assert.AreEqual(DialogResult.None, dialog.DialogResult)
                    Finally
                        busy.SetValue(dialog, False)
                    End Try
                    dialog.CancelButton.PerformClick()
                    dialog.Close()
                End Using

                Assert.AreEqual(expected, Snapshot(manuscript))
            End Sub)

    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub CapturedFingerprint_IsAdoptedOnlyByExplicitSaveAndNavigate(saveAndNavigate As Boolean)

        Dim temporaryRoot As String = CreateTemporaryRoot()
        Try
            Dim sourcePath As String = Path.Combine(temporaryRoot, "sample.txt")
            File.WriteAllText(sourcePath, "abc")
            Dim manuscript As Manuscript = CreateManuscript()
            Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
            SubmissionPacketService.AddFile(packet, SubmissionPacketFileRole.CoverLetter,
                "Sample", "", sourcePath, SubmissionPacketFileStorageMode.LinkedExternal)
            Dim expected As String = Snapshot(manuscript)

            RunOnStaThread(
                Sub()
                    Using dialog As New NonactivatingVault(manuscript, ContextFor(packet))
                        ShowOffscreen(dialog)
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))
                        Assert.AreEqual(expected, Snapshot(manuscript))
                        If saveAndNavigate Then
                            Assert.IsTrue(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Readiness))
                            Assert.AreEqual(packet.ReadinessProfileId, dialog.RequestedNavigation.ReadinessProfileId)
                        Else
                            dialog.CancelButton.PerformClick()
                            Assert.IsNull(dialog.RequestedNavigation)
                        End If
                        dialog.Close()
                    End Using
                End Sub)

            Assert.AreEqual(4, manuscript.SubmissionPackets.Count)
            If saveAndNavigate Then
                Assert.AreEqual(64, manuscript.SubmissionPackets(0).Files.Single().Sha256.Length)
                Assert.AreEqual(2, manuscript.Submissions.Count)
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
            Else
                Assert.AreEqual(expected, Snapshot(manuscript))
            End If
            Assert.AreEqual("abc", File.ReadAllText(sourcePath))
        Finally
            DeleteTemporaryRoot(temporaryRoot)
        End Try

    End Sub

    <TestMethod>
    Public Sub NewPacket_UsesContextDefaultsForExactVersionReadinessAndSubmission()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
                Dim context As SubmissionWorkflowRequest = ContextFor(packet)

                Using dialog As New NonactivatingEditor(manuscript, Nothing, context)
                    ShowOffscreen(dialog)
                    dialog.AcceptButton.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    dialog.Close()
                End Using

                Dim created As SubmissionPacket = manuscript.SubmissionPackets.Last()
                Assert.AreEqual(5, manuscript.SubmissionPackets.Count)
                Assert.AreEqual(context.VersionId.Value, created.ManuscriptVersionId)
                Assert.AreEqual(context.ReadinessProfileId, created.ReadinessProfileId)
                Assert.AreEqual(context.SubmissionId, created.SubmissionId)
                Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
                Assert.AreEqual(2, manuscript.Submissions.Count)
            End Sub)

    End Sub

    <TestMethod>
    <DataRow(100)>
    <DataRow(Integer.MaxValue)>
    Public Sub ExistingPacket_NotesEditPreservesImportedRoundAndIgnoresCreationDefaults(round As Integer)

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                Dim packet As SubmissionPacket = manuscript.SubmissionPackets(0)
                packet.RevisionRoundNumber = round
                Dim expectedVersion As Guid = packet.ManuscriptVersionId
                Dim expectedReadiness As Guid? = packet.ReadinessProfileId
                Dim expectedSubmission As Guid? = packet.SubmissionId
                Dim unrelatedContext As SubmissionWorkflowRequest = ContextFor(manuscript.SubmissionPackets(1))

                Using dialog As New NonactivatingEditor(manuscript, packet, unrelatedContext)
                    ShowOffscreen(dialog)
                    Dim notes As TextBox = DirectCast(GetType(SubmissionPacketEditForm).GetField(
                        "txtNotes", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), TextBox)
                    notes.Text = "Updated notes only"
                    dialog.AcceptButton.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    dialog.Close()
                End Using

                Assert.AreEqual(round, packet.RevisionRoundNumber.Value)
                Assert.AreEqual(expectedVersion, packet.ManuscriptVersionId)
                Assert.AreEqual(expectedReadiness, packet.ReadinessProfileId)
                Assert.AreEqual(expectedSubmission, packet.SubmissionId)
                Assert.AreEqual("Updated notes only", packet.Notes)
            End Sub)

    End Sub

    Private Shared Function CreateManuscript() As Manuscript

        Dim manuscript As New Manuscript With {.Title = "Workflow test", .CurrentStage = PaperStage.Draft}
        Dim firstVersion As New ManuscriptVersion With {.Label = "Exact packet version", .CreatedDate = New DateTime(2026, 8, 1)}
        Dim secondVersion As New ManuscriptVersion With {.Label = "Different current version", .CreatedDate = New DateTime(2026, 8, 2)}
        manuscript.Versions.AddRange({firstVersion, secondVersion})
        manuscript.CurrentVersionId = secondVersion.Id
        Dim firstProfile As New ManuscriptReadiness With {.JournalName = "Same Journal"}
        Dim secondProfile As New ManuscriptReadiness With {.JournalName = "Same Journal"}
        manuscript.ReadinessProfiles.AddRange({firstProfile, secondProfile})
        Dim firstSubmission As New JournalSubmission With {
            .JournalName = "Same Journal", .ManuscriptNumber = "MS-EXACT-1", .SubmittedDate = New DateTime(2026, 8, 10)
        }
        Dim secondSubmission As New JournalSubmission With {
            .JournalName = "Same Journal", .ManuscriptNumber = "MS-EXACT-2", .SubmittedDate = New DateTime(2026, 8, 11)
        }
        manuscript.Submissions.AddRange({firstSubmission, secondSubmission})
        SubmissionPacketService.CreatePacket(manuscript, firstVersion.Id, "Exact selected packet", "Selected notes",
            firstProfile.Id, firstSubmission.Id, 2)
        SubmissionPacketService.CreatePacket(manuscript, secondVersion.Id, "Same readiness, different submission", "",
            firstProfile.Id, secondSubmission.Id)
        SubmissionPacketService.CreatePacket(manuscript, firstVersion.Id, "Same submission, different readiness", "",
            secondProfile.Id, firstSubmission.Id)
        SubmissionPacketService.CreatePacket(manuscript, firstVersion.Id, "Preparation only", "", firstProfile.Id)
        Return manuscript

    End Function

    Private Shared Function ContextFor(packet As SubmissionPacket) As SubmissionWorkflowRequest
        Return New SubmissionWorkflowRequest With {
            .Target = SubmissionWorkflowTarget.Packets,
            .PacketId = packet.Id,
            .VersionId = packet.ManuscriptVersionId,
            .ReadinessProfileId = packet.ReadinessProfileId,
            .SubmissionId = packet.SubmissionId
        }
    End Function

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private Shared Sub AssertWorkflowGeometry(dialog As Form)

        Dim panels As TextBox() = {
            PacketDetail(dialog),
            ControlsIn(dialog).OfType(Of TextBox)().Single(
                Function(item) item.AccessibleName = "Selected file details")
        }
        Dim contentBounds As New List(Of Rectangle)()
        For Each detail As TextBox In panels
            Dim list As ListBox = detail.Parent.Controls.OfType(Of ListBox)().Single()
            Assert.IsTrue(list.ClientSize.Height >= list.ItemHeight * 3,
                $"{detail.AccessibleName} list needs three visible rows at {dialog.Size}; actual height {list.ClientSize.Height}, row height {list.ItemHeight}.")
            Assert.IsTrue(detail.ClientSize.Height >= detail.Font.Height * 2,
                $"{detail.AccessibleName} needs two text lines at {dialog.Size}; actual height {detail.ClientSize.Height}, line height {detail.Font.Height}.")
            AssertFullyVisible(dialog, list)
            AssertFullyVisible(dialog, detail)
            Dim listBounds As Rectangle = BoundsInDialog(dialog, list)
            Dim detailBounds As Rectangle = BoundsInDialog(dialog, detail)
            Assert.IsFalse(listBounds.IntersectsWith(detailBounds), "Packet/file lists and their details must not overlap.")
            contentBounds.Add(listBounds)
            contentBounds.Add(detailBounds)
        Next

        Dim actions As List(Of Button) = ControlsIn(dialog).OfType(Of Button)().ToList()
        Assert.IsTrue(actions.Any(Function(item) item.AccessibleName = "Save packet changes and go to related record"),
            "The contextual geometry test must include the workflow navigation action.")
        For Each action As Button In actions
            AssertFullyVisible(dialog, action)
            Dim actionBounds As Rectangle = BoundsInDialog(dialog, action)
            For Each content As Rectangle In contentBounds
                Assert.IsFalse(actionBounds.IntersectsWith(content),
                    $"Action '{action.Text}' overlaps a packet/file list or detail at {dialog.Size}.")
            Next
        Next

    End Sub

    Private Shared Sub AssertFullyVisible(dialog As Form, control As Control)

        Assert.IsTrue(control.Visible, $"'{control.AccessibleName}' must be visible at {dialog.Size}.")
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim screenBounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Dim bounds As Rectangle = ancestor.RectangleToClient(screenBounds)
            Assert.IsTrue(ancestor.ClientRectangle.Contains(bounds),
                $"{control.GetType().Name} '{control.Text}' is clipped by {ancestor.GetType().Name} at {dialog.Size}: {bounds} outside {ancestor.ClientRectangle}.")
            ancestor = ancestor.Parent
        End While

    End Sub

    Private Shared Function BoundsInDialog(dialog As Form, control As Control) As Rectangle
        Return dialog.RectangleToClient(control.Parent.RectangleToScreen(control.Bounds))
    End Function

    Private Shared Function PacketDetail(dialog As Form) As TextBox
        Return ControlsIn(dialog).OfType(Of TextBox)().Single(
            Function(item) item.AccessibleName = "Selected packet details")
    End Function

    Private Shared Function PacketList(dialog As Form) As ListBox
        Return PacketDetail(dialog).Parent.Controls.OfType(Of ListBox)().Single()
    End Function

    Private Shared Iterator Function ControlsIn(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In ControlsIn(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub ShowOffscreen(dialog As Form)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        Application.DoEvents()
    End Sub

    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The isolated fingerprint operation timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Shared Sub RunOnStaThread(testAction As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    testAction()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "The isolated workflow UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private Class NonactivatingVault
        Inherits SubmissionPacketVaultForm

        Public Sub New(manuscript As Manuscript, context As SubmissionWorkflowRequest)
            MyBase.New(manuscript, context)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class NonactivatingEditor
        Inherits SubmissionPacketEditForm

        Public Sub New(manuscript As Manuscript, packet As SubmissionPacket, context As SubmissionWorkflowRequest)
            MyBase.New(manuscript, packet, context)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
