Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

<TestClass>
<DoNotParallelize>
Public Class ReadinessLayoutTests

    <TestMethod>
    Public Sub PacketVault_ResizeAndSelectionChangesKeepIntegrityToolbarCompact()

        RunOnStaThread(
            Sub()
                Dim manuscript As Manuscript = CreateManuscript()
                For Each packet As SubmissionPacket In manuscript.SubmissionPackets
                    For index As Integer = 1 To packet.Files.Count - 1
                        packet.Files(index).StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
                        packet.Files(index).LocalFilePath = GetType(SubmissionPacketVaultForm).Assembly.Location
                    Next
                    packet.Files(1).LocalFilePath = String.Empty
                    packet.Files(3).Sha256 = New String("a"c, 64)
                Next

                Using dialog As New NonactivatingPacketVaultForm(manuscript)
                    ShowForLayout(dialog, "minimum")
                    Dim fileDetail As TextBox = FindTextBox(dialog, "Selected file details")
                    Dim fileList As ListBox = fileDetail.Parent.Controls.OfType(Of ListBox)().Single()
                    Dim summary As Label = Descendants(dialog).OfType(Of Label)().Single(
                        Function(item) item.AccessibleName = "Packet integrity summary")
                    Assert.IsTrue(summary.Text.Contains("not checked"))
                    Assert.IsTrue(summary.Text.Contains("no fingerprint"))

                    For Each targetSize As Size In {
                        dialog.MinimumSize,
                        New Size(dialog.MinimumSize.Width + 600, dialog.MinimumSize.Height + 300),
                        dialog.MinimumSize
                    }
                        dialog.Size = targetSize
                        For index As Integer = 0 To fileList.Items.Count - 1
                            fileList.SelectedIndex = index
                            dialog.PerformLayout()
                            Application.DoEvents()
                            AssertListAndDetailRemainUsable(dialog, fileDetail)
                            AssertButtonsRemainVisible(dialog)
                            AssertFullyInsideAncestors(dialog, summary)
                            Assert.IsTrue(summary.Parent.Height <= summary.Font.Height * 4,
                                "The integrity toolbar must not retain blank height after resizing or selection changes.")
                        Next
                    Next
                End Using
            End Sub
        )

    End Sub

    <TestMethod>
    <DataRow("minimum")>
    <DataRow("default")>
    <DataRow("expanded")>
    Public Sub PacketVault_LongNotesKeepPacketAndFileListsUsable(sizeName As String)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingPacketVaultForm(CreateManuscript())
                    ShowForLayout(dialog, sizeName)

                    Dim packetDetail As TextBox = FindTextBox(dialog, "Selected packet details")
                    Dim fileDetail As TextBox = FindTextBox(dialog, "Selected file details")
                    AssertListAndDetailRemainUsable(dialog, packetDetail)
                    AssertListAndDetailRemainUsable(dialog, fileDetail)
                    Assert.IsTrue(packetDetail.Text.Contains("Exact submission version"))
                    Assert.IsTrue(fileDetail.Text.Contains("Final long-note line."))
                    AssertButtonsRemainVisible(dialog)
                End Using
            End Sub
        )

    End Sub

    <TestMethod>
    <DataRow("minimum")>
    <DataRow("default")>
    <DataRow("expanded")>
    Public Sub Readiness_LongRequirementKeepsChecklistAndDetailsUsable(sizeName As String)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingReadinessForm(CreateManuscript())
                    ShowForLayout(dialog, sizeName)

                    Dim detail As TextBox = FindTextBox(dialog, "Selected readiness requirement details")
                    AssertListAndDetailRemainUsable(dialog, detail)
                    Assert.IsTrue(detail.Text.Contains("Final long-note line."))
                    AssertButtonsRemainVisible(dialog)
                End Using
            End Sub
        )

    End Sub

    <TestMethod>
    <DataRow("minimum")>
    <DataRow("default")>
    <DataRow("expanded")>
    Public Sub ReadinessNotes_LongInstructionsLeaveRoomToEditNotes(sizeName As String)

        RunOnStaThread(
            Sub()
                Dim requirement As ReadinessItemState = CreateManuscript().ReadinessProfiles(0).Items(0)
                Using dialog As New NonactivatingNotesForm(requirement)
                    ShowForLayout(dialog, sizeName)

                    Dim instructions As TextBox = FindTextBox(dialog, "Requirement instructions")
                    Dim editor As TextBox = FindTextBox(dialog, "Manuscript-specific notes")
                    AssertUsableTextBox(dialog, instructions)
                    AssertUsableTextBox(dialog, editor)
                    Assert.IsTrue(instructions.ReadOnly)
                    Assert.IsFalse(editor.ReadOnly)
                    Assert.IsTrue(instructions.Text.Contains("Final long-note line."))
                    Assert.AreEqual(requirement.UserNotes, editor.Text)
                    Assert.IsTrue(
                        BoundsInForm(dialog, instructions).Bottom <= BoundsInForm(dialog, editor).Top,
                        "Long instructions must not overlap the notes editor."
                    )

                    editor.AppendText(Environment.NewLine & "Additional review note.")
                    Assert.IsTrue(editor.Text.EndsWith("Additional review note."))
                    AssertButtonsRemainVisible(dialog)
                End Using
            End Sub
        )

    End Sub

    Private Shared Sub AssertListAndDetailRemainUsable(dialog As Form, detail As TextBox)

        Dim list As ListBox = detail.Parent.Controls.OfType(Of ListBox)().Single()
        Assert.IsTrue(list.Items.Count > 0, "The populated list is required for this regression.")
        Assert.IsTrue(list.HorizontalScrollbar, "Long requirement/file titles must remain reachable.")
        Assert.IsTrue(
            list.ClientSize.Height >= list.ItemHeight * 3,
            $"The list needs room for at least three rows; actual client height is {list.ClientSize.Height}."
        )
        AssertFullyInsideAncestors(dialog, list)
        AssertUsableTextBox(dialog, detail)
        Assert.IsTrue(detail.ReadOnly)
        Assert.IsTrue(
            BoundsInForm(dialog, list).Bottom <= BoundsInForm(dialog, detail).Top,
            "The list and selected-item details must not overlap."
        )

        AssertAboveFooter(dialog, list)
        AssertAboveFooter(dialog, detail)

    End Sub

    Private Shared Sub AssertUsableTextBox(dialog As Form, textBox As TextBox)

        Assert.IsTrue(textBox.Multiline)
        Assert.AreEqual(ScrollBars.Vertical, textBox.ScrollBars)
        Assert.IsTrue(
            textBox.ClientSize.Height >= textBox.Font.Height * 2,
            $"{textBox.AccessibleName} needs at least two text lines; actual client height is {textBox.ClientSize.Height}."
        )
        Assert.IsTrue(textBox.ClientSize.Width >= textBox.Font.Height * 6)
        AssertFullyInsideAncestors(dialog, textBox)
        AssertAboveFooter(dialog, textBox)

    End Sub

    Private Shared Sub AssertAboveFooter(dialog As Form, content As Control)

        Dim saveButton As Control = DirectCast(dialog.AcceptButton, Control)
        Dim cancelButton As Control = DirectCast(dialog.CancelButton, Control)
        Dim contentBottom As Integer = BoundsInForm(dialog, content).Bottom
        Assert.IsTrue(contentBottom <= BoundsInForm(dialog, saveButton).Top)
        Assert.IsTrue(contentBottom <= BoundsInForm(dialog, cancelButton).Top)

    End Sub

    Private Shared Sub AssertButtonsRemainVisible(dialog As Form)

        Dim buttons As List(Of Button) = Descendants(dialog).OfType(Of Button)().ToList()
        Assert.IsTrue(buttons.Count >= 2)
        For Each button As Button In buttons
            Assert.IsTrue(button.Visible, $"The '{button.Text}' action should remain visible.")
            AssertFullyInsideAncestors(dialog, button)
        Next

    End Sub

    Private Shared Sub AssertFullyInsideAncestors(dialog As Form, control As Control)

        Assert.IsTrue(control.Visible)
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim screenBounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Dim bounds As Rectangle = ancestor.RectangleToClient(screenBounds)
            Assert.IsTrue(
                ancestor.ClientRectangle.Contains(bounds),
                $"{control.GetType().Name} '{control.AccessibleName}' is clipped by {ancestor.GetType().Name} at dialog size {dialog.Size}: {bounds} outside {ancestor.ClientRectangle}."
            )
            ancestor = ancestor.Parent
        End While

    End Sub

    Private Shared Function BoundsInForm(dialog As Form, control As Control) As Rectangle
        Return dialog.RectangleToClient(control.Parent.RectangleToScreen(control.Bounds))
    End Function

    Private Shared Function FindTextBox(dialog As Form, accessibleName As String) As TextBox
        Return Descendants(dialog).OfType(Of TextBox)().Single(
            Function(item) item.AccessibleName = accessibleName
        )
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub ShowForLayout(dialog As Form, sizeName As String)

        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        Application.DoEvents()

        ' Use the form's actual configured sizes after initial system-DPI scaling.
        ' Resizing tests do not stand in for real multi-monitor/DPI certification.
        Select Case sizeName
            Case "minimum"
                dialog.Size = dialog.MinimumSize
            Case "expanded"
                dialog.Size = New Size(dialog.Width + 320, dialog.Height + 240)
            Case "default"
                ' Keep the initial form size.
            Case Else
                Assert.Fail("Unknown layout test size: " & sizeName)
        End Select

        dialog.PerformLayout()
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
            End Sub
        ) With {.IsBackground = True}

        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "The isolated UI layout test timed out.")

        If failure IsNot Nothing Then
            ExceptionDispatchInfo.Capture(failure).Throw()
        End If

    End Sub

    Private Shared Function CreateManuscript() As Manuscript

        Dim manuscript As New Manuscript With {
            .Title = "A longitudinal examination of manuscript preparation and editorial workflows across multiple research teams: implications for transparent reporting, reproducibility, and journal-specific submission requirements"
        }
        Dim longTitle As String =
            "Check the complete blinded manuscript, title page, author contributions, supplementary material, and all journal-specific reporting requirements before final submission"
        Dim longNotes As String = String.Join(
            Environment.NewLine,
            Enumerable.Repeat("Detailed journal instructions and manuscript-specific review notes remain available while the checklist stays usable.", 60)
        ) & Environment.NewLine & "Final long-note line."

        Dim version As New ManuscriptVersion With {.Label = "Exact submission version"}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim readiness As New ManuscriptReadiness With {.JournalName = "Journal of Long Submission Requirements"}
        For index As Integer = 0 To 4
            readiness.Items.Add(
                New ReadinessItemState With {
                    .Title = longTitle & " " & index.ToString(),
                    .Description = longNotes,
                    .UserNotes = longNotes,
                    .SortOrder = index
                }
            )
        Next
        manuscript.ReadinessProfiles.Add(readiness)

        For index As Integer = 0 To 3
            Dim packet As New SubmissionPacket With {
                .Label = longTitle & " " & index.ToString(),
                .ManuscriptVersionId = version.Id,
                .ReadinessProfileId = readiness.Id,
                .JournalName = readiness.JournalName,
                .Notes = longNotes
            }
            For fileIndex As Integer = 0 To 4
                packet.Files.Add(
                    New SubmissionPacketFile With {
                        .Label = longTitle & " " & fileIndex.ToString(),
                        .Notes = longNotes,
                        .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
                    }
                )
            Next
            manuscript.SubmissionPackets.Add(packet)
        Next

        Return manuscript

    End Function

    Private Class NonactivatingPacketVaultForm
        Inherits SubmissionPacketVaultForm

        Public Sub New(manuscript As Manuscript)
            MyBase.New(manuscript)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class NonactivatingReadinessForm
        Inherits ManuscriptReadinessForm

        Public Sub New(manuscript As Manuscript)
            MyBase.New(manuscript, New AuthorLibraryData())
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class NonactivatingNotesForm
        Inherits ReadinessItemNotesForm

        Public Sub New(item As ReadinessItemState)
            MyBase.New(item)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
