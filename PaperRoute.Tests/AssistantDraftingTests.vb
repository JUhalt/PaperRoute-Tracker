Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The AI assistant's drafting help (#84): a starting point for one reviewer
' response, and a cover letter starting point. A fake provider answers; no
' test reaches the network or a model.
<TestClass>
<DoNotParallelize>
Public Class AssistantDraftingTests

    Private _directory As String
    Private _provider As FakeProvider

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        _directory = TestSupport.CreateTemporaryRoot()
        Dim keys As New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
        _provider = New FakeProvider With {.Answer = "Thank you for this comment. We have [describe the change]."}
        AssistantService.ProviderFactory = Function() _provider
        AssistantRunner.ConsentPrompt = Nothing
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        OnlineAccess.ResetForTests()
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    ' ---------------------------------------------------------------
    ' A starting point for one response
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub AStartingPointSendsOnlyTheCommentAndChangesNothingUntilUsed()
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = SubmissionWithDecision()
                Dim before As String = JsonSerializer.Serialize(submission)
                Using editor As New ReviewerResponseItemForm(submission)
                    ShowOffscreen(editor)
                    ShowResponseTab(editor)
                    Assert.IsTrue(editor.SuggestButton.Visible, "Shown while the assistant is on.")
                    Field(Of TextBox)(editor, "txtReviewer").Text = "Reviewer 2"
                    Field(Of TextBox)(editor, "txtComment").Text = "Compare the effect with published classroom studies."
                    Field(Of TextBox)(editor, "txtAction").Text = "Added a comparison table."
                    Field(Of TextBox)(editor, "txtNotes").Text = "SECRET-NOTES"
                    Field(Of TextBox)(editor, "txtLocation").Text = "SECRET-LOCATION"

                    Dim shown As String = Nothing
                    editor.draftPrompt =
                        Function(dialog)
                            ShowOffscreen(dialog)
                            Assert.IsTrue(dialog.HasResult)
                            shown = dialog.HeadingText
                            Assert.AreEqual("&Use as Draft", dialog.PrimaryAction.Text)
                            Assert.IsFalse(dialog.AddBelowAction.Visible, "Nothing to add below yet.")
                            Assert.AreEqual(0, dialog.SuggestionBox.SelectionLength, "Typing never replaces the suggestion.")
                            Assert.AreEqual(String.Empty, Field(Of TextBox)(editor, "txtResponse").Text, "Nothing changes while the suggestion is only shown.")
                            dialog.SuggestionBox.Text = dialog.SuggestionBox.Text & " Edited."
                            dialog.PrimaryAction.PerformClick()
                            Return dialog.DialogResult
                        End Function
                    editor.SuggestStartingPointForTest()

                    Dim sent As AssistantRequest = _provider.Requests.Single()
                    Assert.AreEqual(AssistantService.DraftResponseFeature, sent.Feature)
                    Assert.AreEqual(AssistantService.BuildResponseRequest("Reviewer 2", "Compare the effect with published classroom studies.", "Added a comparison table.").Content, sent.Content)
                    Assert.IsFalse(sent.Content.Contains("SECRET"), "Never the notes or the location.")
                    Assert.AreEqual("AI suggestion from Fake service (fake-model): a draft for you to rewrite.", shown)
                    Assert.AreEqual("Thank you for this comment. We have [describe the change]. Edited.", Field(Of TextBox)(editor, "txtResponse").Text)
                    StringAssert.StartsWith(editor.ProvenanceText, "Began as an AI suggestion (Fake service, fake-model")
                    Assert.AreEqual(before, JsonSerializer.Serialize(submission), "Nothing is saved until Save Comment.")

                    Invoke(editor, "SaveItem", editor, EventArgs.Empty)
                    Assert.AreEqual(DialogResult.OK, editor.DialogResult)
                    Dim saved As ReviewerResponseItem = editor.EditedItem
                    Assert.AreEqual(AssistantService.DraftResponseFeature, saved.ResponseSuggestion.Feature)
                    Assert.AreEqual("fake-model", saved.ResponseSuggestion.Model)
                    Assert.AreEqual("Compare the effect with published classroom studies.", saved.ResponseSuggestion.SourceText)
                    Assert.IsNull(saved.CommentSuggestion, "The comment was typed by hand.")
                    Assert.AreEqual(before, JsonSerializer.Serialize(submission), "The editor never edits the caller's submission.")
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub AnExistingDraftIsReplacedOrAddedBelow()
        RunOnSta(
            Sub()
                For Each addBelow As Boolean In {False, True}
                    Dim submission As JournalSubmission = SubmissionWithDecision()
                    Dim existing As New ReviewerResponseItem With {
                        .DecisionId = submission.Decisions(0).Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1",
                        .CommentText = "Clarify the sample.", .ResponseText = "Thank you."
                    }
                    submission.ReviewerResponses.Add(existing)
                    Using editor As New ReviewerResponseItemForm(submission, existing)
                        ShowOffscreen(editor)
                        Dim below As Boolean = addBelow
                        editor.draftPrompt =
                            Function(dialog)
                                ShowOffscreen(dialog)
                                Assert.AreEqual("&Replace Draft", dialog.PrimaryAction.Text)
                                Assert.IsTrue(dialog.AddBelowAction.Visible)
                                If below Then dialog.AddBelowAction.PerformClick() Else dialog.PrimaryAction.PerformClick()
                                Return dialog.DialogResult
                            End Function
                        editor.SuggestStartingPointForTest()
                        Dim response As String = Field(Of TextBox)(editor, "txtResponse").Text
                        If addBelow Then
                            Assert.AreEqual("Thank you." & Environment.NewLine & Environment.NewLine & _provider.Answer, response)
                        Else
                            Assert.AreEqual(_provider.Answer, response)
                        End If
                        Assert.AreEqual("Thank you.", existing.ResponseText, "The saved comment is untouched until Save Comment.")
                    End Using
                Next
            End Sub)
    End Sub

    <TestMethod>
    Public Sub WhereAResponseBeganIsKeptWhileItHasText()
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = SubmissionWithDecision()
                Dim existing As New ReviewerResponseItem With {
                    .DecisionId = submission.Decisions(0).Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample.",
                    .ResponseText = "We clarified the sample.",
                    .ResponseSuggestion = New AssistantSuggestion With {.Feature = AssistantService.DraftResponseFeature, .Provider = "Claude", .Model = "claude-opus-5-5", .SuggestedUtc = New DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)}
                }
                submission.ReviewerResponses.Add(existing)

                Using editor As New ReviewerResponseItemForm(submission, existing)
                    ShowOffscreen(editor)
                    StringAssert.StartsWith(editor.ProvenanceText, "Began as an AI suggestion (Claude, claude-opus-5-5")
                    Field(Of TextBox)(editor, "txtResponse").Text = "We clarified the sample, in the Method."
                    Invoke(editor, "SaveItem", editor, EventArgs.Empty)
                    Assert.AreEqual("claude-opus-5-5", editor.EditedItem.ResponseSuggestion.Model, "Rewriting keeps where it began.")
                End Using

                Using editor As New ReviewerResponseItemForm(submission, existing)
                    ShowOffscreen(editor)
                    Field(Of TextBox)(editor, "txtResponse").Text = "   "
                    Assert.AreEqual(String.Empty, editor.ProvenanceText)
                    Invoke(editor, "SaveItem", editor, EventArgs.Empty)
                    Assert.IsNull(editor.EditedItem.ResponseSuggestion, "An emptied response began nowhere.")
                End Using

                ' With the assistant off the button is hidden, but where a response began still shows.
                AssistantService.ProviderFactory = Nothing
                Using editor As New ReviewerResponseItemForm(submission, existing)
                    ShowOffscreen(editor)
                    ShowResponseTab(editor)
                    Assert.IsTrue(Field(Of TextBox)(editor, "txtResponse").Visible, "The tab is showing.")
                    Assert.IsFalse(editor.SuggestButton.Visible, "Hidden until the assistant is turned on.")
                    StringAssert.StartsWith(editor.ProvenanceText, "Began as an AI suggestion")
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CancelStopAFailureOrSayingNoChangesNothing()
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = SubmissionWithDecision()
                Using editor As New ReviewerResponseItemForm(submission)
                    ShowOffscreen(editor)
                    Dim notices As New List(Of String)()
                    editor.noticePrompt = Sub(message) notices.Add(message)

                    ' No comment yet: nothing to send.
                    editor.SuggestStartingPointForTest()
                    CollectionAssert.AreEqual({"Write the comment first."}, notices)
                    Assert.AreEqual(0, _provider.Requests.Count)

                    Field(Of TextBox)(editor, "txtReviewer").Text = "Reviewer 1"
                    Field(Of TextBox)(editor, "txtComment").Text = "Clarify the sample."

                    ' Saying no to the question before sending sends nothing.
                    AssistantRunner.ConsentPrompt = Function(owner, request, connection) CType(Nothing, Boolean?)
                    editor.draftPrompt = Function(dialog)
                                             Assert.Fail("Nothing is asked of the provider after Cancel.")
                                             Return DialogResult.Cancel
                                         End Function
                    editor.SuggestStartingPointForTest()
                    Assert.AreEqual(0, _provider.Requests.Count)
                    AssistantRunner.ConsentPrompt = Nothing

                    ' A failure says why and offers Try Again; Close changes nothing.
                    _provider.Failure = New HttpRequestException("overloaded", Nothing, CType(529, HttpStatusCode))
                    editor.draftPrompt =
                        Function(dialog)
                            ShowOffscreen(dialog)
                            Assert.IsTrue(dialog.HasFailed)
                            StringAssert.Contains(dialog.HeadingText, "overloaded right now")
                            StringAssert.EndsWith(dialog.HeadingText, "Nothing was changed.")
                            Assert.AreEqual("&Try Again", dialog.PrimaryAction.Text)
                            Assert.AreEqual("Close", dialog.DismissAction.Text)
                            dialog.DismissAction.PerformClick()
                            Return dialog.DialogResult
                        End Function
                    editor.SuggestStartingPointForTest()
                    Assert.AreEqual(String.Empty, Field(Of TextBox)(editor, "txtResponse").Text)
                    Assert.AreEqual(String.Empty, editor.ProvenanceText)

                    ' Cancel on a result changes nothing either.
                    _provider.Failure = Nothing
                    editor.draftPrompt =
                        Function(dialog)
                            ShowOffscreen(dialog)
                            Assert.IsTrue(dialog.HasResult)
                            dialog.DismissAction.PerformClick()
                            Return dialog.DialogResult
                        End Function
                    editor.SuggestStartingPointForTest()
                    Assert.AreEqual(String.Empty, Field(Of TextBox)(editor, "txtResponse").Text)

                    ' Stop while the request runs: nothing comes back.
                    Dim pending As New TaskCompletionSource(Of AssistantReply)()
                    _provider.Pending = pending
                    editor.draftPrompt =
                        Function(dialog)
                            ShowOffscreen(dialog)
                            Assert.IsTrue(dialog.IsRunning)
                            Assert.AreEqual("Stop", dialog.DismissAction.Text)
                            dialog.DismissAction.PerformClick()
                            Assert.AreEqual(DialogResult.None, dialog.DialogResult, "Stop doesn't close the window.")
                            pending.TrySetCanceled(_provider.LastToken)
                            Application.DoEvents()
                            Assert.IsTrue(dialog.HasFailed)
                            Assert.IsTrue(dialog.Visible AndAlso dialog.DialogResult = DialogResult.None)
                            Assert.IsTrue(dialog.DismissAction.Enabled)
                            StringAssert.StartsWith(dialog.HeadingText, "Stopped.")
                            dialog.DismissAction.PerformClick()
                            Return dialog.DialogResult
                        End Function
                    editor.SuggestStartingPointForTest()
                    Assert.AreEqual(String.Empty, Field(Of TextBox)(editor, "txtResponse").Text)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub WhenTheAssistantCantBeUsedTheEditorSaysWhy()
        RunOnSta(
            Sub()
                ' Turned on for Claude, but no key yet.
                AssistantService.ProviderFactory = Nothing
                OnlineAccess.Configure(AssistantCoreTests.ClaudeSettings())
                Dim submission As JournalSubmission = SubmissionWithDecision()
                Using editor As New ReviewerResponseItemForm(submission)
                    ShowOffscreen(editor)
                    ShowResponseTab(editor)
                    Assert.IsTrue(editor.SuggestButton.Visible)
                    Dim notices As New List(Of String)()
                    editor.noticePrompt = Sub(message) notices.Add(message)
                    Field(Of TextBox)(editor, "txtComment").Text = "Clarify the sample."
                    editor.SuggestStartingPointForTest()
                    StringAssert.StartsWith(notices.Single(), "Add your Claude key")
                End Using
            End Sub)
    End Sub

    ' Each window's sizes are written at 96 DPI. Without that reference a
    ' window keeps its 100% size on a 150% display, which a check of the
    ' v0.9 windows at 150% found in Before Sending.
    <TestMethod>
    Public Sub EveryAssistantWindowScalesWithTheDisplay()
        RunOnSta(
            Sub()
                OnlineAccess.Configure(AssistantCoreTests.ClaudeSettings())
                Dim request As AssistantRequest = AssistantService.BuildLetterRequest(AssistantCoreTests.Letter)
                Dim windows As New List(Of Form) From {
                    New AssistantConsentForm(request, OnlineAccess.CurrentAssistant()),
                    New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission()),
                    New AssistantDraftForm(AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", ""), String.Empty),
                    New CoverLetterForm("Example: open materials", "Fictional Open Psychology", "Journal article", {"open science"}, "An abstract."),
                    ApiKeyForm.ForAssistant(ProtectedKeyStore.Anthropic),
                    ApiKeyForm.ForAssistant(ProtectedKeyStore.AssistantEndpoint)
                }
                Try
                    For Each window As Form In windows
                        Assert.AreEqual(New SizeF(96.0F, 96.0F), window.AutoScaleDimensions, window.Text)
                        Assert.AreEqual(AutoScaleMode.Dpi, window.AutoScaleMode, window.Text)
                    Next

                    ' Before Sending at its smallest size still shows its choices.
                    Dim consent As Form = windows(0)
                    ShowOffscreen(consent)
                    consent.Size = consent.MinimumSize
                    consent.PerformLayout()
                    Application.DoEvents()
                    For Each button As Button In {DirectCast(consent.AcceptButton, Button), DirectCast(consent.CancelButton, Button)}
                        AssertInside(consent, button)
                    Next
                Finally
                    For Each window As Form In windows
                        window.Dispose()
                    Next
                End Try
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub TheEditorAndItsWindowsFitAtTheirSmallestSize(mode As SystemColorMode)
        RunOnSta(
            Sub()
                Dim submission As JournalSubmission = SubmissionWithDecision()
                Using editor As New ReviewerResponseItemForm(submission)
                    ShowOffscreen(editor)
                    editor.Size = editor.MinimumSize
                    Field(Of TabControl)(editor, "tabs").SelectedIndex = 1
                    editor.PerformLayout()
                    Application.DoEvents()
                    AssertInside(editor, editor.SuggestButton)
                    Assert.IsTrue(Field(Of TextBox)(editor, "txtResponse").Height >= Field(Of TextBox)(editor, "txtResponse").Font.Height * 2, "The response box still shows two lines.")
                End Using

                Using draft As New AssistantDraftForm(AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", ""), "An existing draft.")
                    ShowOffscreen(draft)
                    draft.Size = draft.MinimumSize
                    draft.PerformLayout()
                    Application.DoEvents()
                    For Each button As Button In {draft.PrimaryAction, draft.AddBelowAction, draft.DismissAction}
                        AssertInside(draft, button)
                    Next
                End Using

                Using cover As New CoverLetterForm("Example: open materials in developmental science", "Fictional Open Psychology", "Journal article", {"open science"}, "An abstract.")
                    ShowOffscreen(cover)
                    cover.Size = cover.MinimumSize
                    cover.PerformLayout()
                    Application.DoEvents()
                    AssertInside(cover, cover.PrimaryAction)
                    AssertInside(cover, cover.CloseAction)
                    cover.DraftAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    For Each button As Button In {cover.PrimaryAction, cover.SaveAction, cover.AgainAction, cover.CloseAction}
                        AssertInside(cover, button)
                    Next
                End Using
            End Sub, mode)
    End Sub


    ' ---------------------------------------------------------------
    ' A cover letter starting point
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ACoverLetterSendsOnlyTheDetailsShownAndStoresNothing()
        RunOnSta(
            Sub()
                _provider.Answer = "Dear [Editor's name]," & vbLf & vbLf & "Please consider our manuscript." & vbLf & vbLf & "[Your name]"
                Using cover As New CoverLetterForm("Example: open materials", "Fictional Open Psychology", "Journal article", {"open science", " "}, "A first abstract.",
                                                   Function(name) If(name = "Fictional Open Psychology", "Open access · double-anonymous review", String.Empty))
                    ShowOffscreen(cover)
                    StringAssert.StartsWith(cover.IntroText, "Draft sends only these details to ")
                    StringAssert.EndsWith(cover.IntroText, "Author names and your notes are never sent.")
                    Assert.AreEqual("From your Journal Library: Open access · double-anonymous review", cover.FactsText)

                    ' What is shown is what is sent, edits included.
                    cover.AbstractBox.Text = "An edited abstract."
                    cover.KeywordsBox.Text = "open science; preregistration"
                    cover.ToggleSentForTest()
                    Assert.AreEqual(AssistantDraftForm.TextBoxText(AssistantService.WhatIsSent(cover.CurrentRequest())), cover.SentBox.Text)

                    cover.DraftAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Dim sent As AssistantRequest = _provider.Requests.Single()
                    Assert.AreEqual(AssistantService.CoverLetterFeature, sent.Feature)
                    Assert.AreEqual(cover.CurrentRequest().Content, sent.Content)
                    StringAssert.Contains(sent.Content, "Abstract:" & vbLf & "An edited abstract.")
                    StringAssert.Contains(sent.Content, "Keywords: open science, preregistration")
                    StringAssert.Contains(sent.Content, "About the journal: Open access · double-anonymous review")

                    Assert.IsTrue(cover.HasResult)
                    Assert.AreEqual("AI suggestion from Fake service (fake-model): a starting point to rewrite. Fill in the [bracketed] parts.", cover.IntroText)
                    Assert.AreEqual(AssistantDraftForm.TextBoxText(_provider.Answer), cover.LetterBox.Text)
                    Assert.AreEqual("&Copy", cover.PrimaryAction.Text)

                    ' Copy and Save As take the letter as the researcher edited it.
                    cover.LetterBox.Text = "Dear Dr. Placeholder," & Environment.NewLine & "An edited letter."
                    Dim copied As String = Nothing
                    cover.ClipboardWriter = Sub(text) copied = text
                    cover.PrimaryAction.PerformClick()
                    Assert.AreEqual(cover.LetterBox.Text, copied)
                    Assert.AreEqual("Copied.", cover.StatusText)

                    Dim target As String = Path.Combine(_directory, "Cover letter.txt")
                    cover.SavePathPrompt = Function() target
                    cover.SaveAction.PerformClick()
                    Dim bytes As Byte() = File.ReadAllBytes(target)
                    Assert.AreEqual(cover.LetterBox.Text, Encoding.UTF8.GetString(bytes))
                    Assert.AreNotEqual(CByte(&HEF), bytes(0), "No byte order mark.")
                    Assert.AreEqual("Saved as Cover letter.txt.", cover.StatusText)

                    ' Draft Again goes back to the details as they were.
                    cover.AgainAction.PerformClick()
                    Assert.IsFalse(cover.HasResult)
                    Assert.AreEqual("An edited abstract.", cover.AbstractBox.Text)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ACoverLetterThatCantBeDraftedSaysWhyAndSendsNothing()
        RunOnSta(
            Sub()
                Using cover As New CoverLetterForm("", "Fictional Open Psychology", "", Nothing, "")
                    ShowOffscreen(cover)
                    cover.DraftAsync().GetAwaiter().GetResult()
                    Assert.AreEqual("Enter the title first.", cover.StatusText)
                    Assert.AreEqual(0, _provider.Requests.Count)

                    cover.TitleBox.Text = "Example: open materials"
                    AssistantRunner.ConsentPrompt = Function(owner, request, connection) CType(Nothing, Boolean?)
                    cover.DraftAsync().GetAwaiter().GetResult()
                    Assert.AreEqual("Nothing was sent.", cover.StatusText)
                    Assert.AreEqual(0, _provider.Requests.Count)
                    AssistantRunner.ConsentPrompt = Nothing

                    _provider.Failure = New HttpRequestException("no route")
                    cover.DraftAsync().GetAwaiter().GetResult()
                    Assert.IsFalse(cover.HasResult)
                    StringAssert.EndsWith(cover.StatusText, "Nothing was changed.")
                    Assert.AreEqual("&Draft", cover.PrimaryAction.Text, "Back to the details to try again.")

                    ' Stop goes back to the details as edited; the window stays open.
                    _provider.Failure = Nothing
                    Dim pending As New TaskCompletionSource(Of AssistantReply)()
                    _provider.Pending = pending
                    cover.AbstractBox.Text = "An abstract typed here and saved nowhere."
                    Dim drafting As Task = cover.DraftAsync()
                    Assert.IsTrue(cover.IsRunning)
                    Assert.AreEqual("Stop", cover.CloseAction.Text)
                    cover.CloseAction.PerformClick()
                    Assert.AreEqual(DialogResult.None, cover.DialogResult, "Stop doesn't close the window.")
                    pending.TrySetCanceled(_provider.LastToken)
                    Application.DoEvents()
                    Assert.IsTrue(drafting.IsCompleted)
                    Assert.IsTrue(cover.Visible AndAlso cover.DialogResult = DialogResult.None)
                    Assert.AreEqual("Stopped. Nothing was changed.", cover.StatusText)
                    Assert.AreEqual("An abstract typed here and saved nowhere.", cover.AbstractBox.Text)
                    Assert.AreEqual("Close", cover.CloseAction.Text)
                    Assert.IsTrue(cover.CloseAction.Enabled)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheManuscriptPageOffersACoverLetterFromItsOwnDetailsOnly()
        RunOnSta(
            Sub()
                Dim authorDirectory As String = Path.Combine(_directory, "authors")
                Directory.CreateDirectory(authorDirectory)
                Dim repository As New AuthorLibraryRepository(authorDirectory)
                Dim library As New AuthorLibraryData()
                Dim author As New AuthorRecord With {.GivenName = "Avery", .FamilyName = "SECRETNAME", .IsMe = True}
                library.Authors.Add(author)
                library.Journals.Add(New JournalRecord With {.Name = "Fictional Open Psychology", .Publisher = "Fictional Press", .Notes = "SECRET-JOURNAL-NOTES"})
                repository.Save(library)

                Dim manuscript As New Manuscript With {.Title = "Example: open materials in developmental science", .TargetJournal = "Fictional Open Psychology", .WorkType = WorkType.JournalArticle}
                manuscript.Metadata.AbstractText = "A fictional abstract."
                manuscript.Metadata.Keywords.Add("open science")
                manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = author.Id})
                manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "SECRET-REMINDER", .DueDate = DateTime.Today})
                Dim before As String = JsonSerializer.Serialize(manuscript)

                Using page As New EditManuscriptForm(manuscript, New List(Of Manuscript) From {manuscript}, repository, pageMode:=True)
                    ShowOffscreen(page)
                    Dim content As String = Nothing
                    page.coverLetterPrompt =
                        Sub(dialog) content = dialog.CurrentRequest().Content
                    page.DraftCoverLetterForTest()
                    Assert.IsNotNull(content)
                    StringAssert.Contains(content, "Title: Example: open materials in developmental science")
                    StringAssert.Contains(content, "Journal: Fictional Open Psychology")
                    StringAssert.Contains(content, "Keywords: open science")
                    StringAssert.Contains(content, "A fictional abstract.")
                    Assert.IsFalse(content.Contains("SECRET"), "Never author names, notes, or reminders: " & content)
                    Assert.IsFalse(page.HasUnsavedChanges(), "Drafting a cover letter changes nothing on the page.")
                    Assert.AreEqual(before, JsonSerializer.Serialize(manuscript))
                End Using

                AssistantService.ProviderFactory = Nothing
                Using page As New EditManuscriptForm(manuscript, New List(Of Manuscript) From {manuscript}, repository, pageMode:=True)
                    ShowOffscreen(page)
                    Assert.IsFalse(page.CoverLetterButton.Visible, "Hidden until the assistant is turned on.")
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function SubmissionWithDecision() As JournalSubmission
        Dim submission As New JournalSubmission With {.JournalName = "Fictional Open Psychology", .SubmittedDate = New DateTime(2026, 3, 1)}
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 4, 2)})
        Return submission
    End Function

    ' The Suggest button lives on the Draft response tab.
    Private Shared Sub ShowResponseTab(editor As ReviewerResponseItemForm)
        Field(Of TabControl)(editor, "tabs").SelectedIndex = 1
        editor.PerformLayout()
        Application.DoEvents()
    End Sub

    Private NotInheritable Class FakeProvider
        Implements IAssistantProvider

        Public ReadOnly Requests As New List(Of AssistantRequest)()
        Public Property Answer As String = String.Empty
        Public Property Failure As Exception
        Public Property Pending As TaskCompletionSource(Of AssistantReply)
        Public Property LastToken As CancellationToken

        Public ReadOnly Property ProviderName As String Implements IAssistantProvider.ProviderName
            Get
                Return "Fake service"
            End Get
        End Property

        Public ReadOnly Property Model As String Implements IAssistantProvider.Model
            Get
                Return "fake-model"
            End Get
        End Property

        Public Function CompleteAsync(request As AssistantRequest, cancellationToken As CancellationToken) As Task(Of AssistantReply) Implements IAssistantProvider.CompleteAsync
            Requests.Add(request)
            LastToken = cancellationToken
            If Failure IsNot Nothing Then Return Task.FromException(Of AssistantReply)(Failure)
            If Pending IsNot Nothing Then
                Dim waiting As Task(Of AssistantReply) = Pending.Task
                Pending = Nothing
                Return waiting
            End If
            Return Task.FromResult(New AssistantReply With {.Text = Answer, .ProviderName = ProviderName, .Model = Model})
        End Function
    End Class

    Private Shared Sub AssertInside(form As Form, control As Control)
        Assert.IsTrue(control.Visible, control.Text & " is shown.")
        Dim bounds As Rectangle = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))
        Assert.IsTrue(form.ClientRectangle.Contains(bounds), control.Text & " fits: " & bounds.ToString() & " in " & form.ClientRectangle.ToString())
    End Sub

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
        Dim method As MethodInfo = instance.GetType().GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic)
        Try
            method.Invoke(instance, arguments)
        Catch ex As TargetInvocationException
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
        End Try
    End Sub

    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Sub RunOnSta(action As Action, Optional mode As SystemColorMode = SystemColorMode.Classic)
        Dim failure As ExceptionDispatchInfo = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Application.EnableVisualStyles()
                    Application.SetColorMode(mode)
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                Finally
                    Application.SetColorMode(priorMode)
                End Try
            End Sub)
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        thread.Join()
        failure?.Throw()
    End Sub

End Class
