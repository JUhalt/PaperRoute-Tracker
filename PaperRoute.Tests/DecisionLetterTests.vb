Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Read Decision Letter and Add from Letter (#84): the assistant proposes a
' decision, its dates, and the reviewers' comments from a fictional letter;
' nothing changes until the researcher accepts, and what is accepted keeps
' where it came from. A fake provider answers: no test reaches the network
' or a model.
<TestClass>
<DoNotParallelize>
Public Class DecisionLetterTests

    Private Shared ReadOnly Today As New DateTime(2026, 10, 1)
    Private _directory As String

    <TestInitialize>
    Public Sub Setup()
        Reset()
        ' Windows opened by other windows check dates against this day too.
        DecisionLetterForm.TodayOverride = Today
        _directory = TestSupport.CreateTemporaryRoot()
        Dim keys As New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        Reset()
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub

    Private Shared Sub Reset()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        DecisionLetterForm.DialogRunner = Nothing
        DecisionLetterForm.TodayOverride = Nothing
    End Sub

    Private Shared Function UseProvider() As FakeProvider
        Dim provider As New FakeProvider()
        AssistantService.ProviderFactory = Function() provider
        Return provider
    End Function


    ' ---------------------------------------------------------------
    ' Read Decision Letter, from Editorial History
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ReadDecisionLetterAddsTheDecisionAndTheCheckedComments()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                Dim steps As New List(Of String)()

                DecisionLetterForm.DialogRunner =
                    Function(dialog, owner)
                        Dim letterDialog As DecisionLetterForm = TryCast(dialog, DecisionLetterForm)
                        If letterDialog IsNot Nothing Then
                            steps.Add("letter")
                            ShowOffscreen(letterDialog)
                            Assert.AreEqual(String.Empty, letterDialog.LetterBox.Text)
                            letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                            letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                            Assert.IsTrue(letterDialog.IsReviewing, letterDialog.StatusText)

                            Dim rows As IReadOnlyList(Of LetterCommentRow) = letterDialog.CandidateRows
                            Assert.AreEqual(4, rows.Count)
                            CollectionAssert.AreEqual({True, True, True, False}, rows.Select(Function(row) row.Use).ToList(), "The comment not in the letter starts unchecked.")
                            Assert.AreEqual(DecisionLetterForm.InLetterText, rows(0).StatusText)
                            Assert.AreEqual(DecisionLetterForm.NotFoundText, rows(3).StatusText)
                            Assert.AreEqual("AI suggestion from Claude (claude-opus-5-5): check each item before adding.", letterDialog.IntroText)
                            StringAssert.StartsWith(letterDialog.DecisionText, "Decision: Major revision")
                            StringAssert.Contains(letterDialog.DecisionText, ChrW(&H201C) & "Based on their comments, I would like to invite a major revision." & ChrW(&H201D))
                            StringAssert.Contains(letterDialog.DecisionText, "Letter date: Sep 15, 2026")
                            StringAssert.Contains(letterDialog.DecisionText, "Revision deadline: Nov 14, 2026 (60 days after Sep 15, 2026 (the letter's date))")
                            Assert.IsFalse(letterDialog.TruncatedNoteShown)
                            Assert.AreEqual("Add Decision && 3 Comments...", letterDialog.PrimaryButton.Text)
                            Assert.AreEqual("3 comments to add", letterDialog.StatusText)
                            Assert.IsTrue(letterDialog.ReadAgainButton.Visible)
                            letterDialog.RoundBox.Value = 2
                            letterDialog.AcceptForTest()
                            Return Finish(letterDialog)
                        End If

                        Dim decisionDialog As AddDecisionForm = DirectCast(dialog, AddDecisionForm)
                        steps.Add("decision")
                        ShowOffscreen(decisionDialog)
                        Assert.AreEqual("Major Revision", decisionDialog.DecisionList.Text)
                        Assert.AreEqual(New DateTime(2026, 9, 15), decisionDialog.DecisionDatePicker.Value.Date)
                        Assert.IsTrue(decisionDialog.DeadlineCheck.Checked)
                        Assert.AreEqual(New DateTime(2026, 11, 14), decisionDialog.DeadlinePicker.Value.Date)
                        Assert.IsTrue(decisionDialog.BannerShown)
                        StringAssert.Contains(decisionDialog.BannerBox.Text, "Based on their comments, I would like to invite a major revision.")
                        StringAssert.Contains(decisionDialog.BannerBox.Text, "60 days after Sep 15, 2026 (the letter's date)")
                        Assert.AreEqual(AssistantCoreTests.Letter, decisionDialog.NotesBox.Text, "The letter is kept in the decision's notes.")
                        decisionDialog.SaveForTest()
                        Return Finish(decisionDialog)
                    End Function

                ClickReadLetter(manuscript, submission)

                CollectionAssert.AreEqual({"letter", "decision"}, steps)
                Assert.AreEqual(1, provider.Requests.Count)
                Assert.AreEqual(AssistantCoreTests.Letter, provider.Requests(0).Content, "Only the letter is sent.")

                Dim decision As EditorialDecisionEvent = submission.Decisions.Single()
                Assert.AreEqual(EditorialDecision.MajorRevision, decision.Decision)
                Assert.AreEqual(New DateTime(2026, 9, 15), decision.DecisionDate)
                Assert.AreEqual(New DateTime(2026, 11, 14), decision.RevisionDeadline.Value)
                Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage, "Recorded as Add Decision records it.")
                Assert.IsNotNull(decision.Suggestion)
                Assert.AreEqual(AssistantService.DecisionLetterFeature, decision.Suggestion.Feature)
                Assert.AreEqual("Claude", decision.Suggestion.Provider)
                Assert.AreEqual("claude-opus-5-5", decision.Suggestion.Model)
                StringAssert.Contains(decision.Suggestion.SourceText, "Based on their comments, I would like to invite a major revision.")
                StringAssert.Contains(decision.Suggestion.SourceText, "Please submit your revised manuscript within 60 days.")

                Dim expected As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = AssistantCoreTests.LetterAnswer}, AssistantCoreTests.Letter, Today)
                Assert.AreEqual(3, submission.ReviewerResponses.Count)
                CollectionAssert.AreEqual({"Reviewer 1", "Reviewer 1", "Reviewer 2"}, submission.ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList())
                For index As Integer = 0 To 2
                    Dim response As ReviewerResponseItem = submission.ReviewerResponses(index)
                    Dim candidate As LetterCommentCandidate = expected.Comments(index)
                    Assert.AreEqual(decision.Id, response.DecisionId)
                    Assert.AreEqual(2, response.RevisionRoundNumber, "The round chosen.")
                    Assert.AreEqual(ReviewerResponseStatus.Unresolved, response.Status)
                    Assert.AreEqual(candidate.Text, response.CommentText)
                    Assert.IsNotNull(response.CommentSuggestion)
                    Assert.AreEqual(AssistantService.DecisionLetterFeature, response.CommentSuggestion.Feature)
                    Assert.AreEqual("claude-opus-5-5", response.CommentSuggestion.Model)
                    Assert.AreEqual(AssistantCoreTests.Letter.Substring(candidate.SourceStart, candidate.SourceLength).Trim(), response.CommentSuggestion.SourceText, "The letter's own text it came from.")
                    Assert.IsNull(response.ResponseSuggestion)
                Next
                Assert.IsFalse(submission.ReviewerResponses.Any(Function(item) item.CommentText.Contains("figure")), "Not added unless checked.")
                ReviewerResponseService.NormalizeAndValidateManuscript(manuscript)
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheReviewShowsEachCommentsSourceAndKeepsTheResearchersEdits()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                provider.Truncated = True
                Dim submission As JournalSubmission = SubmissionWithOneComment()
                Using letterDialog As New DecisionLetterForm(DecisionLetterMode.NewDecision, submission, AssistantCoreTests.Letter, Today)
                    ShowOffscreen(letterDialog)
                    Assert.AreEqual(AssistantCoreTests.Letter, letterDialog.LetterBox.Text, "The letter passed in is ready to read.")
                    Assert.AreEqual("Read Letter sends only the letter's text to Claude, with PaperRoute's instructions. Nothing else from your library is sent.", letterDialog.IntroText)
                    Assert.IsFalse(letterDialog.ReadAgainButton.Visible)

                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Dim rows As IReadOnlyList(Of LetterCommentRow) = letterDialog.CandidateRows
                    Assert.AreEqual(DecisionLetterForm.AlreadyInMatrixText, rows(1).StatusText)
                    CollectionAssert.AreEqual({True, False, True, False}, rows.Select(Function(row) row.Use).ToList(), "Comments already in the matrix start unchecked.")
                    Assert.IsTrue(letterDialog.TruncatedNoteShown, "The answer was cut short, so some comments may be missing.")
                    Assert.AreEqual("Add Decision && 2 Comments...", letterDialog.PrimaryButton.Text)

                    ' Read Again goes back to the letter, still pasted.
                    letterDialog.ReadAgainButton.PerformClick()
                    Assert.IsFalse(letterDialog.IsReviewing)
                    Assert.AreEqual(AssistantCoreTests.Letter, letterDialog.LetterBox.Text)
                    Assert.AreEqual("Read Letter", letterDialog.PrimaryButton.Text)
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(2, provider.Requests.Count)
                    rows = letterDialog.CandidateRows

                    ' Selecting a comment selects its source in the letter.
                    letterDialog.SelectCommentForTest(2)
                    Application.DoEvents()
                    Dim found As LetterCommentCandidate = rows(2).Candidate
                    Assert.AreEqual(found.SourceStart, letterDialog.ReviewLetterBox.SelectionStart)
                    Assert.AreEqual(found.SourceLength, letterDialog.ReviewLetterBox.SelectionLength)
                    Assert.AreEqual(found.Text, letterDialog.CommentBox.Text)

                    ' The reviewer and the comment can be corrected before adding.
                    letterDialog.CommentBox.Text = "The discussion should address how clinicians' experience moderates anchoring."
                    letterDialog.SetReviewerForTest(2, "  Referee   2 ")
                    Assert.AreEqual("Referee 2", rows(2).ReviewerLabel)
                    StringAssert.StartsWith(CStr(letterDialog.CommentsGrid.Rows(2).Cells(2).Value), "The discussion should address how clinicians' experience moderates")

                    letterDialog.SetUseForTest(0, False)
                    letterDialog.SetUseForTest(2, False)
                    Assert.AreEqual("Add Decision...", letterDialog.PrimaryButton.Text, "The decision alone can be added.")
                    Assert.AreEqual("No comments to add", letterDialog.StatusText)
                    letterDialog.SetUseForTest(2, True)
                    Assert.AreEqual("Add Decision && 1 Comment...", letterDialog.PrimaryButton.Text)
                    letterDialog.SetUseForTest(3, True)
                    Assert.AreEqual("2 comments to add", letterDialog.StatusText)

                    letterDialog.AcceptForTest()
                    Assert.AreEqual(DialogResult.OK, letterDialog.DialogResult)
                    Assert.AreEqual(2, letterDialog.AcceptedComments.Count)
                    Dim edited As AcceptedLetterComment = letterDialog.AcceptedComments(0)
                    Assert.AreEqual("Referee 2", edited.ReviewerLabel)
                    Assert.AreEqual("The discussion should address how clinicians' experience moderates anchoring.", edited.Text)
                    Assert.AreEqual(AssistantCoreTests.Letter.Substring(found.SourceStart, found.SourceLength), edited.SourceExcerpt, "The source stays the letter's text.")
                    Assert.AreEqual("Please add a figure showing the anchoring effect by experience.", letterDialog.AcceptedComments(1).Text)
                    Assert.AreEqual(String.Empty, letterDialog.AcceptedComments(1).SourceExcerpt, "Not in the letter: nothing is recorded as its source.")

                    Dim decisionId As Guid = Guid.NewGuid()
                    Dim drafts As List(Of ReviewerResponseItem) = letterDialog.DraftItems(decisionId)
                    Assert.AreEqual(2, drafts.Count)
                    Assert.AreEqual(String.Empty, drafts(1).CommentSuggestion.SourceText)
                    Assert.AreEqual(AssistantService.DecisionLetterFeature, drafts(1).CommentSuggestion.Feature, "Still recorded as an AI suggestion.")
                    Assert.IsTrue(drafts.All(Function(item) item.DecisionId = decisionId AndAlso item.RevisionRoundNumber = 1 AndAlso item.CommentSuggestion.Provider = "Claude"))
                    StringAssert.StartsWith(AssistantSuggestionService.Describe(drafts(0).CommentSuggestion), "Began as an AI suggestion (Claude, claude-opus-5-5, ")
                    Assert.AreEqual(AssistantService.DecisionLetterFeature, letterDialog.DecisionSuggestion().Feature)
                    letterDialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CancelOrAFailureAtAnyStepChangesNothing()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                Dim original As String = JsonSerializer.Serialize(manuscript)

                ' Cancel at the letter, after reading it.
                DecisionLetterForm.DialogRunner =
                    Function(dialog, owner)
                        Dim letterDialog As DecisionLetterForm = DirectCast(dialog, DecisionLetterForm)
                        ShowOffscreen(letterDialog)
                        letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        Assert.IsTrue(letterDialog.IsReviewing)
                        letterDialog.CancelForTest()
                        Return letterDialog.DialogResult
                    End Function
                ClickReadLetter(manuscript, submission)
                Assert.AreEqual(original, JsonSerializer.Serialize(manuscript), "Cancel at the letter.")

                ' Cancel at the decision goes back to the letter's proposals;
                ' Cancel there ends it.
                Dim steps As New List(Of String)()
                DecisionLetterForm.DialogRunner =
                    Function(dialog, owner)
                        Dim letterDialog As DecisionLetterForm = TryCast(dialog, DecisionLetterForm)
                        If letterDialog Is Nothing Then
                            steps.Add("decision")
                            Return DialogResult.Cancel
                        End If
                        steps.Add("letter")
                        If steps.Count > 1 Then
                            Assert.IsTrue(letterDialog.IsReviewing, "Back at the letter's proposals.")
                            Return DialogResult.Cancel
                        End If
                        ShowOffscreen(letterDialog)
                        letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        letterDialog.AcceptForTest()
                        Return Finish(letterDialog)
                    End Function
                ClickReadLetter(manuscript, submission)
                CollectionAssert.AreEqual({"letter", "decision", "letter"}, steps)
                Assert.AreEqual(original, JsonSerializer.Serialize(manuscript), "Cancel at the decision.")

                ' A failure says so plainly and keeps the letter to try again.
                Dim serverError As Exception = New HttpRequestException("Server error", Nothing, HttpStatusCode.InternalServerError)
                For Each failure In {
                    (Problem:=serverError, Answer:=AssistantCoreTests.LetterAnswer, Expected:="Claude had a problem on its side (HTTP 500). Try again later. Nothing was changed."),
                    (Problem:=CType(Nothing, Exception), Answer:="I can't help with that.", Expected:="The answer wasn't in the expected format. Nothing was changed.")
                }
                    provider.Failure = failure.Problem
                    provider.Answer = failure.Answer
                    Dim shown As String = Nothing
                    DecisionLetterForm.DialogRunner =
                        Function(dialog, owner)
                            Dim letterDialog As DecisionLetterForm = DirectCast(dialog, DecisionLetterForm)
                            ShowOffscreen(letterDialog)
                            letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                            letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                            Assert.IsFalse(letterDialog.IsReviewing)
                            shown = letterDialog.StatusText
                            Assert.AreEqual(AssistantCoreTests.Letter, letterDialog.LetterBox.Text, "The letter is kept to try again.")
                            Assert.IsTrue(letterDialog.PrimaryButton.Enabled)
                            Assert.AreEqual("Cancel", letterDialog.CancelButtonForTest.Text)
                            letterDialog.CancelForTest()
                            Return letterDialog.DialogResult
                        End Function
                    ClickReadLetter(manuscript, submission)
                    Assert.AreEqual(failure.Expected, shown)
                    Assert.AreEqual(original, JsonSerializer.Serialize(manuscript), failure.Expected)
                Next
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CancellingTheQuestionBeforeSendingSendsNothing()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Dim asked As New List(Of AssistantRequest)()
                AssistantRunner.ConsentPrompt =
                    Function(owner, request, connection)
                        asked.Add(request)
                        Return CType(Nothing, Boolean?)
                    End Function
                Using letterDialog As New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission(), AssistantCoreTests.Letter, Today)
                    ShowOffscreen(letterDialog)
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(1, asked.Count)
                    Assert.AreEqual(AssistantCoreTests.Letter, asked(0).Content, "The question shows exactly the letter.")
                    Assert.AreEqual(0, provider.Requests.Count, "Nothing was sent.")
                    Assert.IsFalse(letterDialog.IsReviewing)
                    Assert.AreEqual("Nothing was sent.", letterDialog.StatusText)
                    Assert.IsTrue(letterDialog.PrimaryButton.Enabled, "Read Letter can be chosen again.")

                    AssistantRunner.ConsentPrompt = Function(owner, request, connection) False
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(1, provider.Requests.Count)
                    Assert.IsTrue(letterDialog.IsReviewing)
                    letterDialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub OnlyALetterOfASendableLengthIsSentAndWhatIsSentIsShown()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Using letterDialog As New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission(), Nothing, Today)
                    ShowOffscreen(letterDialog)
                    Assert.AreEqual("Read Letter", letterDialog.PrimaryButton.Text)
                    Assert.IsFalse(letterDialog.PrimaryButton.Enabled, "Nothing to read yet.")

                    letterDialog.LetterBox.Text = New String("x"c, AssistantService.MaximumLetterLength + 1)
                    Assert.IsFalse(letterDialog.PrimaryButton.Enabled)
                    Assert.AreEqual("The letter is longer than PaperRoute sends at once. Paste the reviewers' comments in parts.", letterDialog.StatusText)
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(0, provider.Requests.Count)
                    letterDialog.LetterBox.Text = New String("x"c, AssistantService.MaximumLetterLength)
                    Assert.IsTrue(letterDialog.PrimaryButton.Enabled)
                    Assert.AreEqual(String.Empty, letterDialog.StatusText)

                    letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                    Assert.IsFalse(letterDialog.SentBox.Visible)
                    letterDialog.ToggleSentForTest()
                    Assert.IsTrue(letterDialog.SentBox.Visible)
                    Dim sent As String = AssistantService.WhatIsSent(AssistantService.BuildLetterRequest(AssistantCoreTests.Letter))
                    Assert.AreEqual(sent.Replace(vbCrLf, vbLf).Replace(vbLf, vbCrLf), letterDialog.SentBox.Text)
                    letterDialog.LetterBox.AppendText(vbCrLf & "Reviewer 3" & vbCrLf & "Add a limitations section.")
                    StringAssert.Contains(letterDialog.SentBox.Text, "Add a limitations section.", "What is shown follows the letter.")
                    letterDialog.ToggleSentForTest()
                    Assert.IsFalse(letterDialog.SentBox.Visible)
                    letterDialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub WhenTheAssistantCantBeUsedTheWindowSaysWhyAndSendsNothing()
        RunOnSta(
            Sub()
                Dim network As New AssistantCoreTests.CapturingNetwork()
                OnlineAccess.InnerHandlerFactory = Function() network
                OnlineAccess.Configure(AssistantCoreTests.ClaudeSettings())
                Assert.IsTrue(AssistantRunner.IsTurnedOn())
                Using letterDialog As New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission(), AssistantCoreTests.Letter, Today)
                    ShowOffscreen(letterDialog)
                    StringAssert.Contains(letterDialog.IntroText, "to Claude at api.anthropic.com, with PaperRoute's instructions.")
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    StringAssert.StartsWith(letterDialog.StatusText, "Add your Claude key")
                    Assert.IsFalse(letterDialog.IsReviewing)

                    Dim offline As OnlineServicesSettings = AssistantCoreTests.ClaudeSettings()
                    offline.WorkOffline = True
                    OnlineAccess.Configure(offline)
                    letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(AssistantRunner.Unavailable(), letterDialog.StatusText)
                    Assert.IsFalse(letterDialog.IsReviewing)
                    letterDialog.Close()
                End Using
                Assert.AreEqual(0, network.Requests.Count, "Nothing was sent.")
            End Sub)
    End Sub

    <TestMethod>
    Public Sub StopOrClosingWhileReadingStopsAndChangesNothing()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                provider.WaitForCancel = True
                Dim letterDialog As New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission(), AssistantCoreTests.Letter, Today)
                Try
                    ShowOffscreen(letterDialog)
                    Dim reading As Task = letterDialog.ReadLetterAsync()
                    Assert.IsTrue(letterDialog.IsRunning)
                    Assert.AreEqual("Stop", letterDialog.CancelButtonForTest.Text)
                    Assert.IsFalse(letterDialog.PrimaryButton.Enabled)
                    Assert.AreEqual("Reading the letter with Claude...", letterDialog.StatusText)
                    ' As a click or Esc gives it: through the window's cancel button.
                    letterDialog.CancelButtonForTest.PerformClick()
                    Assert.AreEqual(DialogResult.None, letterDialog.DialogResult, "Stop doesn't ask the window to close.")
                    PumpUntil(Function() reading.IsCompleted)
                    Assert.IsTrue(letterDialog.Visible AndAlso letterDialog.DialogResult = DialogResult.None)
                    Assert.IsFalse(letterDialog.IsRunning)
                    Assert.IsFalse(letterDialog.IsReviewing)
                    Assert.AreEqual("Stopped. Nothing was changed.", letterDialog.StatusText)
                    Assert.AreEqual("Cancel", letterDialog.CancelButtonForTest.Text)
                    Assert.IsTrue(letterDialog.CancelButtonForTest.Enabled)
                    Assert.IsFalse(letterDialog.IsDisposed, "Stop doesn't close the window.")

                    ' Closing while reading stops the request, then closes.
                    Dim closing As Task = letterDialog.ReadLetterAsync()
                    Assert.IsTrue(letterDialog.IsRunning)
                    letterDialog.Close()
                    PumpUntil(Function() closing.IsCompleted AndAlso letterDialog.IsDisposed)
                    Assert.AreEqual(DialogResult.Cancel, letterDialog.DialogResult)
                    Assert.AreEqual(2, provider.Requests.Count)
                Finally
                    letterDialog.Dispose()
                End Try
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Add from Letter, in the reviewer response matrix
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub AddFromLetterAddsNewCommentsToTheChosenDecision()
        RunOnSta(
            Sub()
                UseProvider()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                Dim first As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15), .Notes = AssistantCoreTests.Letter}
                submission.Decisions.Add(first)
                ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {
                    .DecisionId = first.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1",
                    .CommentText = "Report the preregistered  exclusion criteria" & vbCrLf & "in the main text."
                })

                DecisionLetterForm.DialogRunner =
                    Function(dialog, owner)
                        Dim letterDialog As DecisionLetterForm = DirectCast(dialog, DecisionLetterForm)
                        ShowOffscreen(letterDialog)
                        Assert.AreEqual(AssistantCoreTests.Letter, letterDialog.LetterBox.Text, "One decision: its notes hold the letter.")
                        Assert.AreEqual(0, letterDialog.DecisionBox.SelectedIndex, "The only decision is chosen.")
                        StringAssert.Contains(letterDialog.DecisionBox.Text, "Sep 15, 2026")
                        StringAssert.Contains(letterDialog.DecisionBox.Text, "Major revision")
                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        Assert.AreEqual(DecisionLetterForm.AlreadyInMatrixText, letterDialog.CandidateRows(1).StatusText, "Spacing and line breaks don't matter.")
                        CollectionAssert.AreEqual({True, False, True, False}, letterDialog.CandidateRows.Select(Function(row) row.Use).ToList())
                        Assert.AreEqual("Add 2 Comments", letterDialog.PrimaryButton.Text)
                        letterDialog.AcceptForTest()
                        Return Finish(letterDialog)
                    End Function

                Using matrix As New MatrixProbe(manuscript, submission)
                    ShowOffscreen(matrix)
                    Dim fromLetter As Button = Descendants(matrix).OfType(Of Button)().Single(Function(button) button.Text = "Add from &Letter...")
                    Assert.IsTrue(fromLetter.Visible AndAlso fromLetter.Enabled)
                    fromLetter.PerformClick()

                    Dim working As JournalSubmission = Field(Of JournalSubmission)(matrix, "_working")
                    Assert.AreEqual(3, working.ReviewerResponses.Count)
                    Dim added As List(Of ReviewerResponseItem) = working.ReviewerResponses.Skip(1).ToList()
                    CollectionAssert.AreEqual({"Reviewer 1", "Reviewer 2"}, added.Select(Function(item) item.ReviewerLabel).ToList())
                    Assert.IsTrue(added.All(Function(item) item.DecisionId = first.Id AndAlso item.RevisionRoundNumber = 1 AndAlso item.CommentSuggestion IsNot Nothing))
                    Dim details As String = Field(Of TextBox)(matrix, "txtDetails").Text
                    StringAssert.Contains(details, "Comment: Began as an AI suggestion (Claude, claude-opus-5-5, ")
                    StringAssert.Contains(details, "SOURCE (FROM THE DECISION LETTER)")
                    StringAssert.Contains(details, "please report the power analysis.")
                    Assert.AreEqual(1, submission.ReviewerResponses.Count, "Only the matrix's copy changes until Save & Close.")
                    matrix.Close()
                End Using

                ' With two decisions the researcher chooses one.
                Dim second As New EditorialDecisionEvent With {.Decision = EditorialDecision.MinorRevision, .DecisionDate = New DateTime(2026, 9, 30)}
                submission.Decisions.Add(second)
                DecisionLetterForm.DialogRunner =
                    Function(dialog, owner)
                        Dim letterDialog As DecisionLetterForm = DirectCast(dialog, DecisionLetterForm)
                        ShowOffscreen(letterDialog)
                        Assert.AreEqual(-1, letterDialog.DecisionBox.SelectedIndex, "Two decisions: none chosen for the researcher.")
                        Assert.AreEqual(String.Empty, letterDialog.LetterBox.Text)
                        letterDialog.LetterBox.Text = AssistantCoreTests.Letter
                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        letterDialog.RoundBox.Value = 2
                        letterDialog.AcceptForTest()
                        Assert.AreEqual(DialogResult.None, letterDialog.DialogResult)
                        Assert.AreEqual("Choose the decision these comments belong to.", letterDialog.StatusText)
                        letterDialog.DecisionBox.SelectedIndex = 1
                        letterDialog.AcceptForTest()
                        Return Finish(letterDialog)
                    End Function
                Using matrix As New MatrixProbe(manuscript, submission)
                    ShowOffscreen(matrix)
                    Descendants(matrix).OfType(Of Button)().Single(Function(button) button.Text = "Add from &Letter...").PerformClick()
                    Dim working As JournalSubmission = Field(Of JournalSubmission)(matrix, "_working")
                    Assert.AreEqual(3, working.ReviewerResponses.Count)
                    Assert.IsTrue(working.ReviewerResponses.Skip(1).All(Function(item) item.DecisionId = second.Id AndAlso item.RevisionRoundNumber = 2))
                    matrix.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub AddItemsAddsEveryCommentOrNone()
        Dim submission As New JournalSubmission With {.JournalName = "Fictional Journal of Psychology"}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision}
        submission.Decisions.Add(decision)
        ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Editor", .CommentText = "Shorten the abstract."})
        Dim before As String = JsonSerializer.Serialize(submission)

        Dim valid As New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample."}
        Dim noLabel As New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = " ", .CommentText = "No reviewer."}
        Dim elsewhere As New ReviewerResponseItem With {.DecisionId = Guid.NewGuid(), .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 2", .CommentText = "Another decision."}
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.AddItems(submission, {valid, noLabel}))
        Assert.ThrowsExactly(Of InvalidDataException)(Sub() ReviewerResponseService.AddItems(submission, {valid, elsewhere}))
        Assert.ThrowsExactly(Of ArgumentException)(Sub() ReviewerResponseService.AddItems(submission, {valid, Nothing}))
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() ReviewerResponseService.AddItems(submission, Nothing))
        Assert.AreEqual(before, JsonSerializer.Serialize(submission), "A failure adds none.")

        Dim second As New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 2", .CommentText = "Discuss experience.",
                                                     .CommentSuggestion = New AssistantSuggestion With {.Feature = AssistantService.DecisionLetterFeature, .SourceText = "Discuss experience."}}
        Dim added As IReadOnlyList(Of ReviewerResponseItem) = ReviewerResponseService.AddItems(submission, {valid, second})
        Assert.AreEqual(2, added.Count)
        CollectionAssert.AreEqual({"Editor", "Reviewer 1", "Reviewer 2"}, submission.ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList(), "Appended in order.")
        Assert.AreSame(added(0), submission.ReviewerResponses(1))
        Assert.AreNotEqual(valid.Id, added(0).Id, "Each gets a new identifier.")
        Assert.AreNotEqual(added(0).Id, added(1).Id)
        Assert.IsNull(added(1).LastModifiedAtUtc)
        Assert.IsTrue(added(1).CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1))
        Assert.AreNotSame(second.CommentSuggestion, added(1).CommentSuggestion, "Copied, not shared.")
        Assert.AreEqual("Discuss experience.", added(1).CommentSuggestion.SourceText)
        Assert.AreEqual(0, ReviewerResponseService.AddItems(submission, Array.Empty(Of ReviewerResponseItem)()).Count)
        Assert.AreEqual(3, submission.ReviewerResponses.Count)
    End Sub


    ' ---------------------------------------------------------------
    ' The decision step
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ADecisionTheLetterDoesntStatePlainlyIsLeftToChoose()
        RunOnSta(
            Sub()
                ' Without a decision chosen, Save says so instead of doing nothing.
                Using dialog As New AddDecisionForm()
                    ShowOffscreen(dialog)
                    Assert.IsFalse(dialog.BannerShown)
                    dialog.DecisionList.SelectedIndex = -1
                    dialog.SaveForTest()
                    Assert.AreEqual("Choose the decision.", dialog.CheckText)
                    Assert.IsNull(dialog.CreatedDecision)
                    Assert.AreEqual(DialogResult.None, dialog.DialogResult)
                    dialog.Close()
                End Using

                Dim unclear As New DecisionLetterProposal With {.DecisionDate = New DateTime(2026, 9, 15), .ProviderName = "Claude", .Model = "claude-opus-5-5"}
                Dim suggestion As AssistantSuggestion = AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, New AssistantReply With {.ProviderName = "Claude", .Model = "claude-opus-5-5"}, String.Empty, DateTime.UtcNow)
                Using dialog As New AddDecisionForm()
                    Dim plainHeight As Integer = dialog.ClientSize.Height
                    dialog.UseProposal(unclear, AssistantCoreTests.Letter, suggestion)
                    ShowOffscreen(dialog)
                    Assert.IsTrue(dialog.BannerShown)
                    Assert.IsTrue(dialog.ClientSize.Height > plainHeight, "The dialog grows to hold the banner.")
                    Assert.AreEqual(-1, dialog.DecisionList.SelectedIndex, "Not stated plainly: nothing chosen.")
                    Assert.AreEqual(New DateTime(2026, 9, 15), dialog.DecisionDatePicker.Value.Date)
                    Assert.IsFalse(dialog.DeadlineCheck.Checked)
                    StringAssert.Contains(dialog.BannerBox.Text, "Decision: not stated plainly in the letter. Choose it below.")
                    StringAssert.Contains(dialog.BannerBox.Text, "Decision date: Sep 15, 2026, the letter's date")
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: none found in the letter.")
                    For Each control As Control In {dialog.BannerBox, dialog.DecisionList, dialog.NotesBox, DirectCast(dialog.AcceptButton, Control), DirectCast(dialog.CancelButton, Control)}
                        AssertInside(control)
                    Next
                    Assert.IsTrue(dialog.NotesBox.ClientSize.Height >= dialog.NotesBox.Font.Height * 3, "The notes stay readable under the banner.")

                    dialog.SaveForTest()
                    Assert.AreEqual("Choose the decision.", dialog.CheckText)
                    Assert.IsNull(dialog.CreatedDecision)
                    dialog.DecisionList.SelectedIndex = dialog.DecisionList.FindStringExact("Major Revision")
                    Assert.AreEqual(String.Empty, dialog.CheckText, "Choosing a decision clears the note.")
                    dialog.SaveForTest()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.AreEqual(EditorialDecision.MajorRevision, dialog.CreatedDecision.Decision)
                    Assert.AreEqual(AssistantService.DecisionLetterFeature, dialog.CreatedDecision.Suggestion.Feature)
                    Assert.AreNotSame(suggestion, dialog.CreatedDecision.Suggestion)
                    dialog.Close()
                End Using

                ' Editing a decision keeps where it came from.
                Dim existing As New EditorialDecisionEvent With {.Decision = EditorialDecision.MinorRevision, .DecisionDate = New DateTime(2026, 9, 15), .Suggestion = suggestion}
                Using dialog As New AddDecisionForm(existing)
                    dialog.SaveForTest()
                    Assert.AreEqual("claude-opus-5-5", dialog.CreatedDecision.Suggestion.Model)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ProposedDatesStayWithinWhatTheDialogAccepts()
        RunOnSta(
            Sub()
                Dim early As New DecisionLetterProposal With {
                    .Decision = EditorialDecision.MinorRevision, .DecisionDate = New DateTime(2026, 9, 15),
                    .RevisionDeadline = New DateTime(2026, 9, 1), .DeadlineBasis = "Stated in the letter"
                }
                Using dialog As New AddDecisionForm()
                    dialog.NotesBox.Text = "My own notes."
                    dialog.UseProposal(early, AssistantCoreTests.Letter, Nothing)
                    Assert.AreEqual("Minor Revision", dialog.DecisionList.Text)
                    Assert.IsTrue(dialog.DeadlineCheck.Checked)
                    Assert.IsTrue(dialog.DeadlinePicker.Enabled)
                    Assert.AreEqual(New DateTime(2026, 9, 15), dialog.DeadlinePicker.Value.Date, "No earlier than the decision.")
                    Assert.AreEqual("My own notes.", dialog.NotesBox.Text, "Notes already written are kept.")
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: Stated in the letter")
                    dialog.SaveForTest()
                    Assert.IsNull(dialog.CreatedDecision.Suggestion, "No suggestion given, none recorded.")
                End Using

                Dim extreme As New DecisionLetterProposal With {
                    .Decision = EditorialDecision.Accepted, .DecisionDate = New DateTime(1700, 1, 1), .RevisionDeadline = New DateTime(9999, 12, 31)
                }
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(extreme, String.Empty, Nothing)
                    Assert.AreEqual(dialog.DecisionDatePicker.MinDate, dialog.DecisionDatePicker.Value)
                    Assert.AreEqual(dialog.DeadlinePicker.MaxDate, dialog.DeadlinePicker.Value)
                    Assert.AreEqual(String.Empty, dialog.NotesBox.Text)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub APeriodDeadlineFollowsACorrectedDecisionDateUntilTheDeadlineIsSet()
        RunOnSta(
            Sub()
                ' An email body with no date: the period was counted from today,
                ' the day the dialog fills in.
                Dim now As DateTime = DateTime.Today
                Dim dayText As Func(Of DateTime, String) = Function(day) day.ToString("MMM d, yyyy", Globalization.CultureInfo.CurrentCulture)
                Dim noDate As DecisionLetterProposal = AssistantService.ReadLetterReply(
                    New AssistantReply With {.Text = "{""decision"":""major_revision"",""deadline_days"":60,""deadline_quote"":""Please submit your revised manuscript within 60 days."",""comments"":[]}"},
                    AssistantCoreTests.Letter, now)
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(noDate, AssistantCoreTests.Letter, Nothing)
                    Assert.AreEqual(now.AddDays(60), dialog.DeadlinePicker.Value.Date)
                    StringAssert.Contains(dialog.BannerBox.Text, "Decision date: not found in the letter; today's date is filled in.")
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: 60 days after " & dayText(now) & " (today)")

                    dialog.DecisionDatePicker.Value = now.AddDays(-17)
                    Assert.AreEqual(now.AddDays(43), dialog.DeadlinePicker.Value.Date, "Counted from the corrected decision date.")
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: 60 days after " & dayText(now.AddDays(-17)) & " (the decision date)")
                    dialog.DecisionDatePicker.Value = now.AddDays(-12)
                    Assert.AreEqual(now.AddDays(48), dialog.DeadlinePicker.Value.Date, "And again.")

                    ' Unchecked, it still follows, so checking it again shows the right day.
                    dialog.DeadlineCheck.Checked = False
                    dialog.DecisionDatePicker.Value = now.AddDays(-10)
                    dialog.DeadlineCheck.Checked = True
                    Assert.AreEqual(now.AddDays(50), dialog.DeadlinePicker.Value.Date)
                    StringAssert.Contains(dialog.BannerBox.Text, "60 days after " & dayText(now.AddDays(-10)) & " (the decision date)")

                    ' A decision date at the very end of the picker's range is taken without a failure.
                    dialog.DecisionDatePicker.Value = dialog.DecisionDatePicker.MaxDate
                    Assert.AreEqual(dialog.DeadlinePicker.MaxDate.Date, dialog.DeadlinePicker.Value.Date)
                    dialog.DecisionDatePicker.Value = now.AddDays(-12)
                    Assert.AreEqual(now.AddDays(48), dialog.DeadlinePicker.Value.Date)

                    ' Once the researcher sets the deadline, it stays theirs.
                    dialog.DeadlinePicker.Value = now.AddDays(90)
                    dialog.DecisionDatePicker.Value = now.AddDays(-20)
                    Assert.AreEqual(now.AddDays(90), dialog.DeadlinePicker.Value.Date)
                End Using

                ' A deadline the letter states as a date never moves.
                Dim stated As New DecisionLetterProposal With {
                    .Decision = EditorialDecision.MinorRevision, .DecisionDate = New DateTime(2026, 9, 15),
                    .RevisionDeadline = New DateTime(2026, 11, 30), .DeadlineBasis = "Stated in the letter"
                }
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(stated, String.Empty, Nothing)
                    dialog.DecisionDatePicker.Value = New DateTime(2026, 9, 1)
                    Assert.AreEqual(New DateTime(2026, 11, 30), dialog.DeadlinePicker.Value.Date)
                End Using

                ' A letter date far from today is reported, and no deadline is worked out from it.
                Dim old As DecisionLetterProposal = AssistantService.ReadLetterReply(
                    New AssistantReply With {.Text = "{""decision"":""major_revision"",""decision_date"":""2022-03-03"",""deadline_days"":60,""deadline_quote"":""Please submit your revised manuscript within 60 days."",""comments"":[]}"},
                    AssistantCoreTests.Letter, Today)
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(old, String.Empty, Nothing)
                    Assert.IsFalse(dialog.DeadlineCheck.Checked)
                    StringAssert.Contains(dialog.BannerBox.Text, "Decision date: the letter's date, Mar 3, 2022, is far from today, so today's date is filled in. Check it.")
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: not filled in, because the letter's date needs checking; the letter says " &
                                          ChrW(&H201C) & "Please submit your revised manuscript within 60 days." & ChrW(&H201D) & ". Set it below.")
                End Using

                ' ...and a letter that gives no deadline isn't said to have one.
                Dim oldRejection As DecisionLetterProposal = AssistantService.ReadLetterReply(
                    New AssistantReply With {.Text = "{""decision"":""rejected"",""decision_date"":""2022-03-03"",""deadline_date"":"""",""deadline_days"":0,""comments"":[]}"},
                    AssistantCoreTests.Letter, Today)
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(oldRejection, String.Empty, Nothing)
                    StringAssert.Contains(dialog.BannerBox.Text, "Revision deadline: none found in the letter.")
                End Using

                ' A note longer than the box's usual limit, stored or from a
                ' letter, is kept whole and can still be typed in.
                Dim longNote As New String("n"c, 40000)
                Using dialog As New AddDecisionForm(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15), .Notes = longNote})
                    Assert.AreEqual(40000, dialog.NotesBox.TextLength)
                    Assert.AreEqual(0, dialog.NotesBox.MaxLength, "No limit, so the note can be edited.")
                    dialog.SaveForTest()
                    Assert.AreEqual(longNote, dialog.CreatedDecision.Notes)
                End Using
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(stated, longNote, Nothing)
                    Assert.AreEqual(40000, dialog.NotesBox.TextLength)
                    Assert.AreEqual(0, dialog.NotesBox.MaxLength)
                End Using
                Using dialog As New AddDecisionForm(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15), .Notes = "A short note."})
                    Assert.AreEqual(32767, dialog.NotesBox.MaxLength, "The usual limit otherwise.")
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Off until turned on, and layout
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheAssistantsButtonsAppearOnlyOnceItIsTurnedOn()
        RunOnSta(
            Sub()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15)})
                Assert.IsFalse(AssistantRunner.IsTurnedOn())

                Using details As New DetailsProbe(manuscript, submission)
                    ShowOffscreen(details)
                    Assert.IsFalse(ReadLetterButton(details).Visible, "Off until turned on.")
                    details.Close()
                End Using
                Using matrix As New MatrixProbe(manuscript, submission)
                    ShowOffscreen(matrix)
                    Assert.IsFalse(Descendants(matrix).OfType(Of Button)().Any(Function(button) button.Text = "Add from &Letter..."))

                    ' Turned on while open: shown at the next refresh, after Add Comment.
                    OnlineAccess.Configure(AssistantCoreTests.ClaudeSettings())
                    matrix.RefreshFromSubmission()
                    Application.DoEvents()
                    Dim fromLetter As Button = Descendants(matrix).OfType(Of Button)().Single(Function(button) button.Text = "Add from &Letter...")
                    Dim addComment As Button = Descendants(matrix).OfType(Of Button)().Single(Function(button) button.Text = "&Add Comment...")
                    Assert.IsTrue(fromLetter.Visible AndAlso fromLetter.Enabled)
                    Assert.AreEqual(fromLetter.Parent.Controls.IndexOf(addComment) + 1, fromLetter.Parent.Controls.IndexOf(fromLetter))
                    matrix.Close()
                End Using
                Using details As New DetailsProbe(manuscript, submission)
                    ShowOffscreen(details)
                    AssertInside(ReadLetterButton(details))
                    details.Close()
                End Using
                Using matrix As New MatrixProbe(Nothing, New JournalSubmission())
                    ShowOffscreen(matrix)
                    Assert.IsFalse(Descendants(matrix).OfType(Of Button)().Single(Function(button) button.Text = "Add from &Letter...").Enabled,
                                   "Like Add Comment, it needs a recorded decision.")
                    matrix.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub EveryStepFitsAtItsMinimumSize(mode As SystemColorMode)
        RunOnSta(
            Sub()
                UseProvider()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15)})

                For Each letterMode As DecisionLetterMode In {DecisionLetterMode.NewDecision, DecisionLetterMode.ExistingDecision}
                    Using letterDialog As New DecisionLetterForm(letterMode, submission, AssistantCoreTests.Letter, Today)
                        ShowOffscreen(letterDialog)
                        letterDialog.Size = letterDialog.MinimumSize
                        letterDialog.PerformLayout()
                        Application.DoEvents()
                        letterDialog.ToggleSentForTest()
                        Application.DoEvents()
                        For Each control As Control In {letterDialog.LetterBox, letterDialog.SentBox, letterDialog.PrimaryButton, letterDialog.CancelButtonForTest}
                            AssertInside(control)
                        Next
                        Assert.IsTrue(letterDialog.LetterBox.ClientSize.Height >= letterDialog.LetterBox.Font.Height * 4, "The letter box keeps a few lines.")

                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        Application.DoEvents()
                        Assert.IsTrue(letterDialog.IsReviewing)
                        Dim reviewControls As New List(Of Control) From {
                            letterDialog.ReviewLetterBox, letterDialog.CommentsGrid, letterDialog.CommentBox, letterDialog.RoundBox,
                            letterDialog.PrimaryButton, letterDialog.CancelButtonForTest, letterDialog.ReadAgainButton
                        }
                        If letterMode = DecisionLetterMode.ExistingDecision Then reviewControls.Add(letterDialog.DecisionBox)
                        For Each control As Control In reviewControls
                            AssertInside(control)
                        Next
                        Dim grid As DataGridView = letterDialog.CommentsGrid
                        Assert.IsTrue(grid.ClientSize.Height >= grid.ColumnHeadersHeight + grid.Rows(0).Height * 2, "At least two comments show.")
                        Assert.IsTrue(letterDialog.CommentBox.ClientSize.Height >= letterDialog.CommentBox.Font.Height * 2, "The comment editor keeps two lines.")
                        Assert.IsTrue(letterDialog.RoundBox.Width >= letterDialog.RoundBox.Font.Height * 3, "The round box keeps its width: " & letterDialog.RoundBox.Width.ToString())
                        letterDialog.Close()
                    End Using
                Next

                Using matrix As New MatrixProbe(manuscript, submission)
                    ShowOffscreen(matrix)
                    matrix.Size = matrix.MinimumSize
                    matrix.PerformLayout()
                    Application.DoEvents()
                    Assert.IsTrue(Descendants(matrix).OfType(Of Button)().Any(Function(button) button.Text = "Add from &Letter..."))
                    For Each button As Button In Descendants(matrix).OfType(Of Button)()
                        AssertInside(button)
                    Next
                    matrix.Close()
                End Using

                Dim proposal As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = AssistantCoreTests.LetterAnswer, .ProviderName = "Claude", .Model = "claude-opus-5-5"}, AssistantCoreTests.Letter, Today)
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(proposal, AssistantCoreTests.Letter, Nothing)
                    ShowOffscreen(dialog)
                    For Each control As Control In Descendants(dialog).Where(Function(item) TypeOf item Is Button OrElse TypeOf item Is TextBox OrElse TypeOf item Is ComboBox OrElse TypeOf item Is DateTimePicker OrElse TypeOf item Is CheckBox)
                        AssertInside(control)
                    Next
                    Assert.IsTrue(dialog.DecisionDatePicker.Width >= dialog.DecisionDatePicker.Font.Height * 5, "The decision date keeps its width: " & dialog.DecisionDatePicker.Width.ToString())
                    Assert.AreEqual(0, dialog.BannerBox.SelectionLength, "Nothing starts selected in the suggestion.")
                    dialog.Close()
                End Using

                ' The same window without a suggestion, as Add Decision opens it.
                Using plain As New AddDecisionForm()
                    ShowOffscreen(plain)
                    Assert.IsTrue(plain.DecisionDatePicker.Width >= plain.DecisionDatePicker.Font.Height * 5, "The decision date keeps its width: " & plain.DecisionDatePicker.Width.ToString())
                    plain.Close()
                End Using
            End Sub, mode)
    End Sub

    <TestMethod>
    Public Sub AmpersandsShowAndAcceleratorsDontCollide()
        RunOnSta(
            Sub()
                UseProvider()
                Dim manuscript As Manuscript = Fixture()
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15)})
                Using matrix As New MatrixProbe(manuscript, submission)
                    AssertDistinctAccelerators(matrix)
                End Using
                For Each letterMode As DecisionLetterMode In {DecisionLetterMode.NewDecision, DecisionLetterMode.ExistingDecision}
                    Using letterDialog As New DecisionLetterForm(letterMode, submission, AssistantCoreTests.Letter, Today)
                        ShowOffscreen(letterDialog)
                        letterDialog.ReadLetterAsync().GetAwaiter().GetResult()
                        AssertNoLostAmpersands(letterDialog)
                        AssertDistinctAccelerators(letterDialog)
                        letterDialog.Close()
                    End Using
                Next
                Using details As New SubmissionDetailsForm(manuscript, submission)
                    AssertNoLostAmpersands(details)
                End Using
                Using dialog As New AddDecisionForm()
                    dialog.UseProposal(New DecisionLetterProposal(), String.Empty, Nothing)
                    AssertNoLostAmpersands(dialog)
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function Fixture() As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Example: anchoring effects in clinical risk estimates",
            .CurrentStage = PaperStage.Submitted,
            .StageEnteredDate = New DateTime(2026, 6, 1)
        }
        manuscript.Submissions.Add(New JournalSubmission With {.JournalName = "Fictional Journal of Psychology", .SubmittedDate = New DateTime(2026, 6, 1)})
        Return manuscript
    End Function

    ' A submission with an earlier decision and one comment from the letter.
    Private Shared Function SubmissionWithOneComment() As JournalSubmission
        Dim submission As New JournalSubmission With {.JournalName = "Fictional Journal of Psychology"}
        Dim earlier As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 7, 1)}
        submission.Decisions.Add(earlier)
        ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {
            .DecisionId = earlier.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "reviewer 1",
            .CommentText = "Report the preregistered exclusion criteria in the main text."
        })
        Return submission
    End Function

    Private Shared Function ReadLetterButton(details As SubmissionDetailsForm) As Button
        Return Descendants(details).OfType(Of Button)().Single(Function(button) button.Text = "Read Decision Letter...")
    End Function

    ' Opens Read Decision Letter from a submission's Editorial History.
    Private Shared Sub ClickReadLetter(manuscript As Manuscript, submission As JournalSubmission)
        Using details As New DetailsProbe(manuscript, submission)
            ShowOffscreen(details)
            Dim readButton As Button = ReadLetterButton(details)
            Assert.IsTrue(readButton.Visible AndAlso readButton.Enabled)
            readButton.PerformClick()
            details.Close()
        End Using
    End Sub

    Private Shared Function Finish(dialog As Form) As DialogResult
        Dim result As DialogResult = dialog.DialogResult
        If Not dialog.IsDisposed Then dialog.Hide()
        Return result
    End Function

    Private Shared Sub PumpUntil(condition As Func(Of Boolean))
        Dim deadline As DateTime = DateTime.UtcNow.AddSeconds(10)
        While Not condition()
            Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting.")
            Application.DoEvents()
            Thread.Sleep(5)
        End While
    End Sub

    Private Shared Sub AssertNoLostAmpersands(root As Control)
        Dim lost As New Regex("(?<!&)&(?!&)(?=\s|$)")
        For Each control As Control In Descendants(root)
            Dim label As Label = TryCast(control, Label)
            Dim button As ButtonBase = TryCast(control, ButtonBase)
            If (label IsNot Nothing AndAlso label.UseMnemonic) OrElse (button IsNot Nothing AndAlso button.UseMnemonic) Then
                Assert.IsFalse(lost.IsMatch(control.Text), "A literal ampersand would disappear from: " & control.Text)
            End If
        Next
    End Sub

    Private Shared Sub AssertDistinctAccelerators(root As Control)
        Dim seen As New Dictionary(Of Char, String)()
        For Each control As Control In Descendants(root)
            Dim label As Label = TryCast(control, Label)
            Dim button As ButtonBase = TryCast(control, ButtonBase)
            If Not ((label IsNot Nothing AndAlso label.UseMnemonic) OrElse (button IsNot Nothing AndAlso button.UseMnemonic)) Then Continue For
            Dim match As Match = Regex.Match(control.Text.Replace("&&", String.Empty), "&(\w)")
            If Not match.Success Then Continue For
            Dim key As Char = Char.ToUpperInvariant(match.Groups(1).Value(0))
            Dim other As String = Nothing
            Assert.IsFalse(seen.TryGetValue(key, other), "Alt+" & key & " is used by both " & other & " and " & control.Text)
            seen(key) = control.Text
        Next
    End Sub

    Private Shared Sub ShowOffscreen(form As Form)
        If form.Visible Then Return
        form.ShowInTaskbar = False
        form.Opacity = 0
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.Show()
        Application.DoEvents()
        ' A window that centers itself when shown goes back offscreen.
        form.Location = New Point(-20000, -20000)
        form.PerformLayout()
        Application.DoEvents()
    End Sub

    Private Shared Sub AssertInside(control As Control)
        Assert.IsTrue(control.Visible, control.GetType().Name & " " & control.Text & " is hidden.")
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim bounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Assert.IsTrue(ancestor.ClientRectangle.Contains(ancestor.RectangleToClient(bounds)),
                          control.GetType().Name & " (" & control.AccessibleName & control.Text & ") is clipped by " & ancestor.GetType().Name &
                          "; child=" & ancestor.RectangleToClient(bounds).ToString() & "; available=" & ancestor.ClientRectangle.ToString())
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

    Private Shared Function Field(Of T)(instance As Object, name As String) As T
        Dim type As Type = instance.GetType()
        While type IsNot Nothing
            Dim info As FieldInfo = type.GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
            If info IsNot Nothing Then Return DirectCast(info.GetValue(instance), T)
            type = type.BaseType
        End While
        Throw New MissingFieldException(name)
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
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(5)), "The decision letter UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub


    ' Answers like a model would, without one; records what it was sent.
    Private NotInheritable Class FakeProvider
        Implements IAssistantProvider

        Public ReadOnly Requests As New List(Of AssistantRequest)()

        Public Property Answer As String = AssistantCoreTests.LetterAnswer

        Public Property Failure As Exception

        Public Property Truncated As Boolean

        ' Answers only by being cancelled, like a slow model.
        Public Property WaitForCancel As Boolean

        Public ReadOnly Property ProviderName As String Implements IAssistantProvider.ProviderName
            Get
                Return "Claude"
            End Get
        End Property

        Public ReadOnly Property Model As String Implements IAssistantProvider.Model
            Get
                Return "claude-opus-5-5"
            End Get
        End Property

        Public Function CompleteAsync(request As AssistantRequest, cancellationToken As CancellationToken) As Task(Of AssistantReply) Implements IAssistantProvider.CompleteAsync
            Requests.Add(request)
            If Failure IsNot Nothing Then Return Task.FromException(Of AssistantReply)(Failure)
            If WaitForCancel Then
                Dim pending As New TaskCompletionSource(Of AssistantReply)()
                cancellationToken.Register(Sub() pending.TrySetCanceled(cancellationToken))
                Return pending.Task
            End If
            Return Task.FromResult(New AssistantReply With {.Text = Answer, .ProviderName = ProviderName, .Model = Model, .Truncated = Truncated})
        End Function

    End Class

    Private Class DetailsProbe
        Inherits SubmissionDetailsForm

        Public Sub New(manuscript As Manuscript, submission As JournalSubmission)
            MyBase.New(manuscript, submission)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

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
    End Class

End Class
