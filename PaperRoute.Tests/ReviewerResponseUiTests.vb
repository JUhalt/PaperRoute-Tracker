Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
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
Public Class ReviewerResponseUiTests
    <TestMethod>
    Public Sub Matrix_CancelDiscardsAndSaveCopiesOnlyResponses()
        RunOnSta(
            Sub()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                Dim original As String = JsonSerializer.Serialize(manuscript)
                Using dialog As New MatrixProbe(manuscript, submission)
                    Dim working As JournalSubmission = Field(Of JournalSubmission)(dialog, "_working")
                    working.ReviewerResponses(0).ResponseText = "Discarded response"
                    ReviewerResponseService.MoveItem(working, working.ReviewerResponses(0).Id, 1)
                    ShowInvisible(dialog)
                    Assert.IsTrue(dialog.Press(Keys.Escape))
                    Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                End Using
                Assert.AreEqual(original, JsonSerializer.Serialize(manuscript))
                Dim history As String = JsonSerializer.Serialize(submission.Decisions)
                Dim submitted As DateTime = submission.SubmittedDate
                Dim firstId As Guid = submission.ReviewerResponses(0).Id
                Using dialog As New MatrixProbe(manuscript, submission)
                    Dim working As JournalSubmission = Field(Of JournalSubmission)(dialog, "_working")
                    working.ReviewerResponses(0).ResponseText = "Saved working response"
                    ReviewerResponseService.MoveItem(working, firstId, 1)
                    ShowInvisible(dialog)
                    dialog.ActiveControl = Field(Of ListBox)(dialog, "lstResponses")
                    Assert.IsTrue(dialog.Press(Keys.Enter))
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(firstId, submission.ReviewerResponses(1).Id)
                    Assert.AreEqual("Saved working response", submission.ReviewerResponses(1).ResponseText)
                    working.ReviewerResponses(1).ResponseText = "Mutated after save"
                    Assert.AreEqual("Saved working response", submission.ReviewerResponses(1).ResponseText,
                                    "Saved responses must be independent copies.")
                End Using
                Assert.AreEqual(history, JsonSerializer.Serialize(submission.Decisions))
                Assert.AreEqual(submitted, submission.SubmittedDate)
                Assert.AreEqual(PaperStage.Submitted, manuscript.CurrentStage)
            End Sub)
    End Sub

    <TestMethod>
    Public Sub MatrixSave_OnManuscriptDetailsWorkingCopy_DoesNotChangeOriginalManuscript()
        RunOnSta(
            Sub()
                Dim original As Manuscript = Fixture()
                Dim before As String = JsonSerializer.Serialize(original)
                Dim detailsWorking As Manuscript = ManuscriptCloneService.CloneManuscript(original)
                Using dialog As New MatrixProbe(detailsWorking, detailsWorking.Submissions(0))
                    Dim matrixWorking As JournalSubmission = Field(Of JournalSubmission)(dialog, "_working")
                    matrixWorking.ReviewerResponses(0).ResponseText = "Accepted by child dialog only"
                    Invoke(dialog, "SaveChanges", Nothing, EventArgs.Empty)
                End Using
                Assert.AreEqual("Accepted by child dialog only", detailsWorking.Submissions(0).ReviewerResponses(0).ResponseText)
                Assert.AreEqual(before, JsonSerializer.Serialize(original),
                                "Closing outer Manuscript Details without saving must leave the original response matrix unchanged.")
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ItemEditor_SaveReturnsValidatedDraftWithoutChangingMatrix()
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = Fixture().Submissions(0)
                Dim before As String = JsonSerializer.Serialize(submission)
                Using editor As New EditorProbe(submission, submission.ReviewerResponses(0))
                    Field(Of TextBox)(editor, "txtReviewer").Text = "Editor"
                    Field(Of NumericUpDown)(editor, "nudRound").Value = 3
                    Field(Of TextBox)(editor, "txtResponse").Text = "We clarified the methods."
                    ShowInvisible(editor)
                    editor.ActiveControl = Field(Of TextBox)(editor, "txtReviewer")
                    Assert.IsTrue(editor.Press(Keys.Enter))
                    Assert.AreEqual(DialogResult.OK, editor.DialogResult)
                    Assert.AreEqual("Editor", editor.EditedItem.ReviewerLabel)
                    Assert.AreEqual(3, editor.EditedItem.RevisionRoundNumber)
                    Assert.AreEqual(submission.Decisions(0).Id, editor.EditedItem.DecisionId)
                    Assert.AreEqual("We clarified the methods.", editor.EditedItem.ResponseText)
                End Using
                Assert.AreEqual(before, JsonSerializer.Serialize(submission))
                Using editor As New EditorProbe(submission)
                    Field(Of TextBox)(editor, "txtComment").Text = "Discarded comment"
                    ShowInvisible(editor)
                    Assert.IsTrue(editor.Press(Keys.Escape))
                    Assert.AreEqual(DialogResult.Cancel, editor.DialogResult)
                    Assert.IsNull(editor.EditedItem)
                End Using
                Assert.AreEqual(before, JsonSerializer.Serialize(submission))
            End Sub)
    End Sub

    <TestMethod>
    Public Sub StatusFilter_DisablesReorderingAcrossHiddenComments()
        RunOnSta(
            Sub()
                Dim manuscript As Manuscript = Fixture()
                Using dialog As New MatrixProbe(manuscript, manuscript.Submissions(0))
                    ShowInvisible(dialog)
                    Dim list As ListBox = Field(Of ListBox)(dialog, "lstResponses")
                    Dim filter As ComboBox = Field(Of ComboBox)(dialog, "cmbStatus")
                    Dim working As JournalSubmission = Field(Of JournalSubmission)(dialog, "_working")
                    list.SelectedIndex = 1
                    Assert.IsTrue(Field(Of Button)(dialog, "btnUp").Enabled)
                    Invoke(dialog, "MoveItem", -1)
                    Assert.AreEqual(ReviewerResponseStatus.Addressed, working.ReviewerResponses(0).Status)
                    filter.SelectedIndex = 1 ' Unresolved only.
                    Assert.AreEqual(1, list.Items.Count)
                    Assert.IsFalse(Field(Of Button)(dialog, "btnUp").Enabled)
                    Assert.IsFalse(Field(Of Button)(dialog, "btnDown").Enabled)
                    Dim order As Guid() = working.ReviewerResponses.Select(Function(item) item.Id).ToArray()
                    Invoke(dialog, "MoveItem", -1)
                    CollectionAssert.AreEqual(order, working.ReviewerResponses.Select(Function(item) item.Id).ToArray())
                    filter.SelectedIndex = 0
                    Assert.AreEqual(2, list.Items.Count)
                    Assert.IsTrue(dialog.Press(Keys.Escape))
                    Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub SubmissionMetadataEdit_PreservesIndependentResponseMatrix(save As Boolean)
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = Fixture().Submissions(0)
                submission.Correspondence.Add(New CorrespondenceItem With {.Title = "Original letter"})
                Dim original As String = JsonSerializer.Serialize(submission)
                Using dialog As New SubmissionMetadataProbe(submission)
                    ShowInvisible(dialog, False)
                    Field(Of TextBox)(dialog, "txtManuscriptNumber").Text = "Corrected manuscript number"
                    Field(Of TextBox)(dialog, "txtNotes").Text = "Updated submission metadata"
                    dialog.ActiveControl = Field(Of TextBox)(dialog, "txtManuscriptNumber")
                    Assert.IsTrue(dialog.Press(If(save, Keys.Enter, Keys.Escape)))
                    Assert.AreEqual(original, JsonSerializer.Serialize(submission),
                                    "The metadata editor must not mutate the caller before its result is accepted.")
                    If save Then
                        Dim result As JournalSubmission = dialog.CreatedSubmission
                        Assert.IsNotNull(result)
                        Assert.AreEqual("Corrected manuscript number", result.ManuscriptNumber)
                        Assert.AreEqual("Updated submission metadata", result.Notes)
                        Assert.AreEqual(submission.Id, result.Id)
                        Assert.AreEqual(JsonSerializer.Serialize(submission.ReviewerResponses), JsonSerializer.Serialize(result.ReviewerResponses))
                        Assert.AreNotSame(submission.ReviewerResponses(0), result.ReviewerResponses(0))
                        Assert.AreNotSame(submission.Decisions(0), result.Decisions(0))
                        Assert.AreNotSame(submission.Correspondence(0), result.Correspondence(0))
                        result.ReviewerResponses(0).ResponseText = "Edited returned draft"
                        Assert.AreEqual(original, JsonSerializer.Serialize(submission))
                    Else
                        Assert.IsNull(dialog.CreatedSubmission)
                        Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    End If
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub EqualDateDecisionChoices_RemainDistinguishableWithoutInferringRound()
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = Fixture().Submissions(0)
                Dim secondDecision As New EditorialDecisionEvent With {
                    .Decision = submission.Decisions(0).Decision, .DecisionDate = submission.Decisions(0).DecisionDate
                }
                submission.Decisions.Add(secondDecision)
                Dim item As ReviewerResponseItem = submission.ReviewerResponses(0)
                item.DecisionId = secondDecision.Id
                item.RevisionRoundNumber = 7
                Using editor As New EditorProbe(submission, item)
                    Dim choices As ComboBox = Field(Of ComboBox)(editor, "cmbDecision")
                    Assert.AreNotEqual(choices.Items(0).ToString(), choices.Items(1).ToString())
                    Assert.AreEqual(1, choices.SelectedIndex)
                    Assert.IsTrue(choices.SelectedItem.ToString().Contains("Major revision"))
                    Assert.AreEqual(7D, Field(Of NumericUpDown)(editor, "nudRound").Value)
                    Invoke(editor, "SaveItem", Nothing, EventArgs.Empty)
                    Assert.AreEqual(secondDecision.Id, editor.EditedItem.DecisionId)
                    Assert.AreEqual(7, editor.EditedItem.RevisionRoundNumber)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub MinimumLayouts_KeepLongTextBoundedAndActionsVisible(mode As SystemColorMode)
        RunOnSta(
            Sub()
                Dim manuscript As Manuscript = Fixture()
                Using matrix As New MatrixProbe(manuscript, manuscript.Submissions(0))
                    ShowInvisible(matrix)
                    Dim list As ListBox = Field(Of ListBox)(matrix, "lstResponses")
                    Dim details As TextBox = Field(Of TextBox)(matrix, "txtDetails")
                    Assert.IsTrue(list.ClientSize.Height >= list.ItemHeight * 5)
                    Assert.IsTrue(details.ClientSize.Height >= details.Font.Height * 5)
                    Assert.IsTrue(details.Multiline AndAlso details.ReadOnly)
                    Assert.IsTrue(details.Text.Contains("Major revision"))
                    Assert.AreEqual(ScrollBars.Vertical, details.ScrollBars)
                    Assert.IsTrue(list.HorizontalScrollbar)
                    AssertInside(list)
                    AssertInside(details)
                    For Each button As Button In Descendants(matrix).OfType(Of Button)()
                        AssertInside(button)
                        Assert.IsTrue(button.TabStop)
                    Next
                    matrix.Close()
                End Using
                Using editor As New EditorProbe(manuscript.Submissions(0), manuscript.Submissions(0).ReviewerResponses(0))
                    ShowInvisible(editor)
                    AssertInside(DirectCast(editor.AcceptButton, Control))
                    AssertInside(DirectCast(editor.CancelButton, Control))
                    For Each combo As ComboBox In Descendants(editor).OfType(Of ComboBox)()
                        AssertInside(combo)
                    Next
                    Dim tabs As TabControl = Descendants(editor).OfType(Of TabControl)().Single()
                    For Each page As TabPage In tabs.TabPages
                        tabs.SelectedTab = page
                        Application.DoEvents()
                        For Each textbox As TextBox In Descendants(page).OfType(Of TextBox)()
                            AssertInside(textbox)
                            Assert.IsTrue(textbox.ClientSize.Height >= textbox.Font.Height * 2,
                                          "Each editor needs at least two readable lines at minimum size.")
                            Assert.IsTrue(textbox.TabStop)
                        Next
                    Next
                    editor.Close()
                End Using
                Using export As New ExportProbe(ReviewerResponseExportService.ExportMarkdown(manuscript, manuscript.Submissions(0)))
                    ShowInvisible(export)
                    Dim text As TextBox = Descendants(export).OfType(Of TextBox)().Single()
                    Assert.IsFalse(text.ReadOnly)
                    AssertInside(text)
                    For Each button As Button In Descendants(export).OfType(Of Button)()
                        AssertInside(button)
                    Next
                    export.Close()
                End Using
            End Sub, mode)
    End Sub

    <TestMethod>
    Public Sub EmptySubmission_ExplainsDecisionPrerequisite()
        RunOnSta(
            Sub()
                Dim submission As New JournalSubmission()
                Using dialog As New MatrixProbe(Nothing, submission)
                    Assert.IsFalse(Field(Of Button)(dialog, "btnAdd").Enabled)
                    Assert.IsTrue(Field(Of TextBox)(dialog, "txtDetails").Text.Contains("Record an editorial decision"))
                    Assert.AreEqual(0, Field(Of ListBox)(dialog, "lstResponses").Items.Count)
                End Using
            End Sub)
    End Sub

    Private Shared Function Fixture() As Manuscript
        Dim manuscript As New Manuscript With {.Title = "Response UI fixture", .CurrentStage = PaperStage.Submitted}
        Dim submission As New JournalSubmission With {.JournalName = "Fictional journal", .SubmittedDate = New DateTime(2026, 9, 20)}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision}
        submission.Decisions.Add(decision)
        manuscript.Submissions.Add(submission)
        For index As Integer = 0 To 1
            ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {
                .DecisionId = decision.Id, .RevisionRoundNumber = index + 1, .ReviewerLabel = "Reviewer " & (index + 1).ToString(),
                .CommentText = String.Join(Environment.NewLine, Enumerable.Repeat("Long reviewer comment with methods and eligibility details.", 40)),
                .ActionText = "Clarify the Methods section.", .ResponseText = "Draft response.",
                .ManuscriptLocation = "Page 4, lines 80–95", .Notes = "Working note",
                .Status = If(index = 0, ReviewerResponseStatus.Unresolved, ReviewerResponseStatus.Addressed)
            })
        Next
        Return manuscript
    End Function

    Private Shared Function Field(Of T)(instance As Object, name As String) As T
        Dim type As Type = instance.GetType()
        While type IsNot Nothing
            Dim info As FieldInfo = type.GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
            If info IsNot Nothing Then Return DirectCast(info.GetValue(instance), T)
            type = type.BaseType
        End While
        Throw New MissingFieldException(name)
    End Function

    Private Shared Sub Invoke(instance As Object, name As String, ParamArray arguments As Object())
        Dim type As Type = instance.GetType()
        While type IsNot Nothing
            Dim method As MethodInfo = type.GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
            If method IsNot Nothing Then
                method.Invoke(instance, arguments)
                Return
            End If
            type = type.BaseType
        End While
        Throw New MissingMethodException(name)
    End Sub

    Private Shared Sub ShowInvisible(dialog As Form, Optional resizeToMinimum As Boolean = True)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        If resizeToMinimum Then dialog.Size = dialog.MinimumSize
        dialog.PerformLayout()
        Application.DoEvents()
    End Sub

    Private Shared Sub AssertInside(control As Control)
        Assert.IsTrue(control.Visible)
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim screen As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Assert.IsTrue(ancestor.ClientRectangle.Contains(ancestor.RectangleToClient(screen)),
                          control.GetType().Name & " is clipped by " & ancestor.GetType().Name)
            ancestor = ancestor.Parent
        End While
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each nested As Control In Descendants(child)
                Yield nested
            Next
        Next
    End Function

    Private Shared Sub RunOnSta(action As Action, Optional mode As SystemColorMode = SystemColorMode.Classic)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Application.EnableVisualStyles()
                    Application.SetColorMode(mode)
                    action()
                Catch ex As Exception
                    failure = ex
                Finally
                    Application.SetColorMode(priorMode)
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Reviewer response UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private Class MatrixProbe
        Inherits ReviewerResponseMatrixForm
        Public Sub New(manuscript As Manuscript, submission As JournalSubmission)
            MyBase.New(manuscript, submission)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class EditorProbe
        Inherits ReviewerResponseItemForm
        Public Sub New(submission As JournalSubmission, Optional item As ReviewerResponseItem = Nothing)
            MyBase.New(submission, item)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class

    Private Class ExportProbe
        Inherits ReviewerResponseExportForm
        Public Sub New(markdown As String)
            MyBase.New(markdown)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class SubmissionMetadataProbe
        Inherits AddSubmissionForm
        Public Sub New(submission As JournalSubmission)
            MyBase.New(submission)
        End Sub
        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
        Public Function Press(key As Keys) As Boolean
            Return MyBase.ProcessDialogKey(key)
        End Function
    End Class
End Class
