Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class ReadinessWorkflowNavigationTests

    <TestMethod>
    Public Sub SaveAndViewPackets_UsesExactProfileIdAndAdoptsOnlyReadiness()
        Dim fixture As New NavigationFixture()
        Dim sourceBefore As String = Snapshot(fixture.Manuscript)
        Dim otherState As String = SnapshotWithoutReadiness(fixture.Manuscript)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    Assert.IsTrue(RequirementDetail(dialog).Text.Contains("Second profile requirement"))
                    FindButton(dialog, "Mark Complete").PerformClick()
                    Assert.AreEqual(sourceBefore, Snapshot(fixture.Manuscript))
                    Assert.IsTrue(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.Packets))
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(SubmissionWorkflowTarget.Packets, dialog.RequestedNavigation.Target)
                    Assert.AreEqual(fixture.SecondProfile.Id, dialog.RequestedNavigation.ReadinessProfileId.Value)
                    dialog.Close()
                End Using
            End Sub)

        Dim first As ManuscriptReadiness = fixture.Manuscript.ReadinessProfiles.Single(Function(item) item.Id = fixture.FirstProfile.Id)
        Dim second As ManuscriptReadiness = fixture.Manuscript.ReadinessProfiles.Single(Function(item) item.Id = fixture.SecondProfile.Id)
        Assert.AreEqual(ReadinessItemStatus.Unresolved, first.Items(0).Status)
        Assert.AreEqual(ReadinessItemStatus.Complete, second.Items(0).Status)
        Assert.IsTrue(second.Items(0).CompletedAtUtc.HasValue)
        Assert.AreEqual(otherState, SnapshotWithoutReadiness(fixture.Manuscript),
            "Navigation must not record a submission, change lifecycle state, or replace packet/version history.")
    End Sub

    <TestMethod>
    Public Sub Cancel_DiscardsReadinessEditsAndDoesNotRequestNavigation()
        Dim fixture As New NavigationFixture()
        Dim original As String = Snapshot(fixture.Manuscript)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    FindButton(dialog, "Mark Complete").PerformClick()
                    dialog.CancelButton.PerformClick()
                    Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    Assert.IsNull(dialog.RequestedNavigation)
                    dialog.Close()
                End Using
            End Sub)

        Assert.AreEqual(original, Snapshot(fixture.Manuscript))
    End Sub

    <TestMethod>
    <DataRow("https://second.example.invalid/submit", True)>
    <DataRow("http://second.example.invalid/submit", True)>
    <DataRow("file:///C:/submission.txt", False)>
    <DataRow("javascript:alert(1)", False)>
    <DataRow("mailto:editor@example.invalid", False)>
    <DataRow("not a portal URL", False)>
    Public Sub Portal_UsesExactJournalIdentityAndAllowsOnlyHttpSchemes(portal As String, expected As Boolean)
        Dim fixture As New NavigationFixture()
        fixture.SecondJournal.SubmissionPortalUrl = portal
        Dim original As String = Snapshot(fixture.Manuscript)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    Dim actual As Uri = dialog.GetReadinessPortal()
                    If expected Then
                        Assert.IsNotNull(actual)
                        Assert.AreEqual(New Uri(portal).AbsoluteUri, actual.AbsoluteUri)
                        Assert.AreNotEqual(fixture.FirstJournal.SubmissionPortalUrl, actual.AbsoluteUri,
                            "Duplicate display names must not redirect to the other journal.")
                    Else
                        Assert.IsNull(actual)
                    End If
                    Dim portalButton As Button = Descendants(dialog).OfType(Of Button)().Single(
                        Function(item) item.AccessibleName = "Open selected journal submission portal")
                    Assert.AreEqual(expected, portalButton.Enabled)
                    dialog.Close()
                End Using
            End Sub)

        Assert.AreEqual(original, Snapshot(fixture.Manuscript))
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Portal_UnknownOrMissingJournalIdDoesNotFallBackToMatchingNames(unknownId As Boolean)
        Dim fixture As New NavigationFixture()
        fixture.SecondProfile.JournalId = If(unknownId, CType(Guid.NewGuid(), Guid?), Nothing)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    Assert.IsNull(dialog.GetReadinessPortal())
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow("minimum")>
    <DataRow("expanded")>
    Public Sub WorkflowToolbar_LeavesReadinessContentAndActionsReachable(sizeName As String)
        Dim fixture As New NavigationFixture()
        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    dialog.Size = If(sizeName = "minimum", dialog.MinimumSize,
                        New Size(dialog.Width + 320, dialog.Height + 240))
                    dialog.PerformLayout()
                    Application.DoEvents()
                    Dim detail As TextBox = RequirementDetail(dialog)
                    Dim list As ListBox = detail.Parent.Controls.OfType(Of ListBox)().Single()
                    Assert.IsTrue(list.ClientSize.Height >= list.ItemHeight * 3,
                        "The navigation toolbar must leave at least three checklist rows visible.")
                    Assert.IsTrue(detail.ClientSize.Height >= detail.Font.Height * 2)
                    AssertInsideAncestors(list)
                    AssertInsideAncestors(detail)
                    For Each button As Button In Descendants(dialog).OfType(Of Button)()
                        Assert.IsTrue(button.Visible)
                        AssertInsideAncestors(button)
                    Next
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub UnsupportedNavigationTarget_DoesNotAdoptEditsOrChangeLifecycle()
        Dim fixture As New NavigationFixture()
        Dim original As String = Snapshot(fixture.Manuscript)
        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(fixture)
                    ShowOffscreen(dialog)
                    FindButton(dialog, "Mark Complete").PerformClick()
                    Assert.IsFalse(dialog.RequestWorkflowNavigation(SubmissionWorkflowTarget.RecordSubmission))
                    Assert.IsNull(dialog.RequestedNavigation)
                    Assert.AreEqual(original, Snapshot(fixture.Manuscript))
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Function Snapshot(Of T)(value As T) As String
        Return JsonSerializer.Serialize(value, CreateJsonOptions())
    End Function

    Private Shared Function SnapshotWithoutReadiness(manuscript As Manuscript) As String
        Dim copy As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
        copy.ReadinessProfiles.Clear()
        Return Snapshot(copy)
    End Function

    Private Shared Function FindButton(dialog As Form, text As String) As Button
        Return Descendants(dialog).OfType(Of Button)().Single(Function(item) item.Text = text)
    End Function

    Private Shared Function RequirementDetail(dialog As Form) As TextBox
        Return Descendants(dialog).OfType(Of TextBox)().Single(
            Function(item) item.AccessibleName = "Selected readiness requirement details")
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub AssertInsideAncestors(control As Control)
        Dim screenBounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Assert.IsTrue(ancestor.ClientRectangle.Contains(ancestor.RectangleToClient(screenBounds)),
                control.GetType().Name & " '" & control.Text & "' is clipped by " & ancestor.GetType().Name)
            ancestor = ancestor.Parent
        End While
    End Sub

    Private Shared Sub ShowOffscreen(dialog As Form)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        Application.DoEvents()
    End Sub

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "The isolated readiness navigation test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private NotInheritable Class NavigationFixture
        Public ReadOnly Property Manuscript As New Manuscript With {
            .Title = "Readiness navigation fixture", .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline, .StageEnteredDate = New DateTime(2026, 9, 10)
        }
        Public ReadOnly Property Library As New AuthorLibraryData()
        Public ReadOnly Property FirstJournal As New JournalRecord With {
            .Name = "Duplicate journal name", .SubmissionPortalUrl = "https://first.example.invalid/submit"
        }
        Public ReadOnly Property SecondJournal As New JournalRecord With {
            .Name = "Duplicate journal name", .SubmissionPortalUrl = "https://second.example.invalid/submit"
        }
        Public ReadOnly Property FirstProfile As ManuscriptReadiness
        Public ReadOnly Property SecondProfile As ManuscriptReadiness

        Public Sub New()
            Library.Journals.Add(FirstJournal)
            Library.Journals.Add(SecondJournal)
            Manuscript.TargetJournalId = FirstJournal.Id
            Manuscript.TargetJournal = FirstJournal.Name
            FirstProfile = New ManuscriptReadiness With {.JournalId = FirstJournal.Id, .JournalName = FirstJournal.Name}
            SecondProfile = New ManuscriptReadiness With {.JournalId = SecondJournal.Id, .JournalName = SecondJournal.Name}
            For index As Integer = 0 To 4
                FirstProfile.Items.Add(New ReadinessItemState With {
                    .Title = "First profile requirement " & index, .IsRequired = True, .SortOrder = index
                })
                SecondProfile.Items.Add(New ReadinessItemState With {
                    .Title = "Second profile requirement " & index & ": confirm all manuscript-specific preparation details",
                    .Description = String.Join(Environment.NewLine, Enumerable.Repeat("Detailed checklist instructions remain readable while navigating among exact workflow records.", 20)),
                    .IsRequired = True, .SortOrder = index
                })
            Next
            Manuscript.ReadinessProfiles.Add(FirstProfile)
            Manuscript.ReadinessProfiles.Add(SecondProfile)
            Dim version As New ManuscriptVersion With {.Label = "Exact version retained during readiness navigation"}
            Manuscript.Versions.Add(version)
            Manuscript.CurrentVersionId = version.Id
            SubmissionPacketService.CreatePacket(Manuscript, version.Id, "Prepared packet", "No submission recorded", SecondProfile.Id)
        End Sub
    End Class

    Private Class NonactivatingReadinessForm
        Inherits ManuscriptReadinessForm

        Public Sub New(fixture As NavigationFixture)
            MyBase.New(fixture.Manuscript, fixture.Library, New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Readiness, .ReadinessProfileId = fixture.SecondProfile.Id
            })
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
