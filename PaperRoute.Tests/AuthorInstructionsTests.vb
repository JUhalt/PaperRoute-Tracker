Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
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

' Read Author Instructions (#95): the assistant proposes checklist
' requirements from a fictional journal's instructions for authors, which the
' researcher pastes; each is checked against the text, nothing changes until
' the researcher accepts, and what is accepted keeps where it came from. A
' fake provider answers: no test reaches the network or a model.
<TestClass>
<DoNotParallelize>
Public Class AuthorInstructionsTests

    Friend Const Instructions As String =
        "Preparing your manuscript" & vbCrLf & vbCrLf &
        "Research articles should not exceed 8,000 words, including references. Brief reports are limited to 3,000 words." & vbCrLf &
        "Each manuscript must include a structured abstract of no more than 250 words." & vbCrLf &
        "Provide up to six keywords." & vbCrLf &
        "Remove all identifying information from the manuscript file, which is reviewed double-anonymously." & vbCrLf &
        "A data availability statement is required for all articles." & vbCrLf &
        "Authors are encouraged to follow the relevant reporting guideline, such as CONSORT or PRISMA." & vbCrLf &
        "Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher."

    ' Found and checked; found and checked, for every type; a number that
    ' isn't in its sentence; already in the checklist; found; optional; not
    ' in the text; a duplicate; no title; a category the checklist doesn't use.
    Friend Const InstructionsAnswer As String =
        "{""requirements"":[" &
        "{""title"":""Main text of at most 8,000 words"",""category"":""Manuscript"",""required"":true,""applies_to"":""Research articles"",""quote"":""Research articles should not exceed 8,000 words, including references.""}," &
        "{""title"":""Structured abstract of at most 250 words"",""category"":""manuscript"",""required"":true,""applies_to"":""All article types"",""quote"":""Each manuscript must include a structured abstract of no more than 250 words.""}," &
        "{""title"":""Up to 5 keywords"",""category"":""Manuscript"",""required"":true,""applies_to"":"""",""quote"":""Provide up to six keywords.""}," &
        "{""title"":""Anonymized manuscript file"",""category"":""peer review"",""required"":true,""applies_to"":"""",""quote"":""Remove all identifying information from the manuscript file, which is reviewed double-anonymously.""}," &
        "{""title"":""Data availability statement"",""category"":""Compliance"",""required"":true,""applies_to"":"""",""quote"":""A data availability statement is required for all articles.""}," &
        "{""title"":""Follow a reporting guideline"",""category"":""Compliance"",""required"":false,""applies_to"":"""",""quote"":""Authors are encouraged to follow the relevant reporting guideline, such as CONSORT or PRISMA.""}," &
        "{""title"":""One-page cover letter"",""category"":""Editorial"",""required"":true,""applies_to"":"""",""quote"":""Include a one-page cover letter addressed to the editor.""}," &
        "{""title"":""Data  Availability statement"",""category"":""Compliance"",""required"":true,""applies_to"":"""",""quote"":""A data availability statement is required for all articles.""}," &
        "{""title"":"""",""category"":""Files"",""required"":true,""applies_to"":"""",""quote"":""Figures must be uploaded.""}," &
        "{""title"":""Figures as separate TIFF or EPS files"",""category"":""Artwork"",""required"":true,""applies_to"":"""",""quote"":""Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher.""}" &
        "]}"

    Private Const Link As String = "https://journals.example.org/fop/authors"
    Private _directory As String

    <TestInitialize>
    Public Sub Setup()
        Reset()
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
        AuthorInstructionsForm.DialogRunner = Nothing
        AuthorInstructionsForm.LinkOpener = Nothing
    End Sub

    Private Shared Function UseProvider() As FakeProvider
        Dim provider As New FakeProvider()
        AssistantService.ProviderFactory = Function() provider
        Return provider
    End Function

    Private Shared Function Reply(Optional text As String = InstructionsAnswer, Optional truncated As Boolean = False) As AssistantReply
        Return New AssistantReply With {.Text = text, .ProviderName = "Claude", .Model = "claude-opus-5-5", .Truncated = truncated}
    End Function

    ' The checklist the journal already has: one requirement, in a
    ' category of its own.
    Private Shared Function Existing() As List(Of JournalChecklistTemplateItem)
        Return New List(Of JournalChecklistTemplateItem) From {
            New JournalChecklistTemplateItem With {.Title = "anonymized   Manuscript File", .Category = "Peer review", .SortOrder = 10, .IsRequired = True}
        }
    End Function


    ' ---------------------------------------------------------------
    ' What is sent, and how the answer is checked
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub OnlyThePastedTextAndArticleTypeAreSent()
        Dim categories As List(Of String) = AssistantService.CategoriesFor({"Peer review", " peer   review ", "manuscript", "", Nothing})
        CollectionAssert.AreEqual({"Manuscript", "Editorial", "Compliance", "Files", "Figures", "Peer review"}, categories, "The standard categories, then the checklist's own, once each.")

        Dim plain As AssistantRequest = AssistantService.BuildAuthorInstructionsRequest("  " & Instructions & "  ", "  ")
        Assert.AreEqual(AssistantService.AuthorInstructionsFeature, plain.Feature)
        Assert.AreEqual(Instructions, plain.Content, "Only the text.")
        Assert.IsNotNull(plain.Schema, "The answer's format is fixed.")
        Assert.AreEqual(8000, plain.MaxTokens)
        StringAssert.Contains(plain.Instructions, "- category: one of Manuscript, Editorial, Compliance, Files, Figures.")
        Assert.IsFalse(plain.Instructions.Contains("Peer review"), "Not even the checklist's own category names are sent.")
        StringAssert.Contains(plain.Instructions, "copied exactly from the text")
        Assert.AreEqual("Read Author Instructions", AssistantService.FeatureName(AssistantService.AuthorInstructionsFeature))

        Dim typed As AssistantRequest = AssistantService.BuildAuthorInstructionsRequest(Instructions, "  Brief   report ")
        Assert.AreEqual("Article type: Brief report" & vbLf & vbLf & Instructions, typed.Content)
        Dim shown As String = AssistantService.WhatIsSent(typed)
        StringAssert.Contains(shown, "Article type: Brief report")
        StringAssert.Contains(shown, "Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher.")
        StringAssert.Contains(shown, "PaperRoute also asks for the answer in a fixed format (JSON)")
    End Sub

    <TestMethod>
    Public Sub EachRequirementIsCheckedAgainstTheText()
        Dim categories As List(Of String) = AssistantService.CategoriesFor({"Peer review"})
        Dim found As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(Reply(), Instructions, categories)
        Assert.AreEqual("Claude", found.ProviderName)
        Assert.AreEqual("claude-opus-5-5", found.Model)
        Assert.IsFalse(found.Truncated)

        Dim requirements As List(Of RequirementCandidate) = found.Requirements
        CollectionAssert.AreEqual(
            {"Main text of at most 8,000 words", "Structured abstract of at most 250 words", "Up to 5 keywords", "Anonymized manuscript file",
             "Data availability statement", "Follow a reporting guideline", "One-page cover letter", "Figures as separate TIFF or EPS files"},
            requirements.Select(Function(item) item.Title).ToList(), "A duplicate and a requirement without a title are left out.")
        CollectionAssert.AreEqual({"Manuscript", "Manuscript", "Manuscript", "Peer review", "Compliance", "Compliance", "Editorial", ""},
                                  requirements.Select(Function(item) item.Category).ToList(), "Only the checklist's categories, in their own spelling.")
        CollectionAssert.AreEqual({True, True, True, True, True, False, True, True}, requirements.Select(Function(item) item.IsRequired).ToList())
        CollectionAssert.AreEqual({"Research articles", "", "", "", "", "", "", ""}, requirements.Select(Function(item) item.AppliesTo).ToList(), "For every type, said plainly.")
        CollectionAssert.AreEqual({True, True, True, True, True, True, False, True}, requirements.Select(Function(item) item.Found).ToList())
        CollectionAssert.AreEqual({True, True, False, True, True, True, True, True}, requirements.Select(Function(item) item.NumbersMatch).ToList(), "Six isn't 5.")

        ' Each found quote is the sentence the assistant quoted, and its place
        ' in the text is that sentence's place.
        Dim quoted As String() = {
            "Research articles should not exceed 8,000 words, including references.",
            "Each manuscript must include a structured abstract of no more than 250 words.",
            "Provide up to six keywords.",
            "Remove all identifying information from the manuscript file, which is reviewed double-anonymously.",
            "A data availability statement is required for all articles.",
            "Authors are encouraged to follow the relevant reporting guideline, such as CONSORT or PRISMA.",
            Nothing,
            "Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher."
        }
        For index As Integer = 0 To quoted.Length - 1
            If quoted(index) Is Nothing Then Continue For
            Assert.AreEqual(quoted(index), requirements(index).Quote)
            Assert.AreEqual(Instructions.IndexOf(quoted(index), StringComparison.Ordinal), requirements(index).SourceStart, quoted(index))
            Assert.AreEqual(quoted(index).Length, requirements(index).SourceLength, quoted(index))
        Next
        Dim invented As RequirementCandidate = requirements(6)
        Assert.AreEqual(-1, invented.SourceStart)
        Assert.AreEqual("Include a one-page cover letter addressed to the editor.", invented.Quote, "Shown, never recorded.")

        Assert.AreEqual("Research articles should not exceed 8,000 words, including references." & Environment.NewLine & "Applies to: Research articles.",
                        AssistantService.RequirementDescription(requirements(0)))
        Assert.AreEqual("Each manuscript must include a structured abstract of no more than 250 words.", AssistantService.RequirementDescription(requirements(1)))
        Assert.AreEqual(String.Empty, AssistantService.RequirementDescription(invented), "Nothing unverified is kept as the journal's words.")
        Assert.AreEqual(String.Empty, AssistantService.RequirementDescription(Nothing))

        ' A quote found with different spacing, quote marks, or dashes is
        ' still the text's own words.
        Dim curly As String = "Figures must be uploaded as separate " & ChrW(&H201C) & "TIFF" & ChrW(&H201D) & " files " & ChrW(&H2014) & " at 300 dpi."
        Dim straight As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(
            Reply("{""requirements"":[{""title"":""Figures at 300 dpi"",""category"":""Figures"",""required"":true,""applies_to"":""every type"",""quote"":""figures must be uploaded as   separate \""TIFF\"" files - at 300 dpi.""}]}"),
            curly, Nothing)
        Dim figure As RequirementCandidate = straight.Requirements.Single()
        Assert.IsTrue(figure.Found)
        Assert.AreEqual(curly, figure.Quote)
        Assert.AreEqual(String.Empty, figure.AppliesTo)
        Assert.AreEqual("Figures", figure.Category, "The standard categories without a checklist.")

        ' Text pasted from a table keeps its tabs; a quote of the row with
        ' spaces is still found, and its numbers checked.
        Dim table As String = "Article type" & vbTab & "Words" & vbTab & "Abstract" & vbCrLf &
                              "Research article" & vbTab & "8,000" & vbTab & "250" & vbCrLf &
                              "Brief report" & vbTab & "3,000" & vbTab & "150"
        Dim fromTable As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(
            Reply("{""requirements"":[" &
                  "{""title"":""Brief report of at most 3,000 words"",""category"":""Manuscript"",""required"":true,""applies_to"":""Brief report"",""quote"":""Brief report 3,000 150""}," &
                  "{""title"":""Brief report abstract of at most 200 words"",""category"":""Manuscript"",""required"":true,""applies_to"":""Brief report"",""quote"":""Brief report 3,000 150""}]}"),
            table, Nothing)
        Dim row As RequirementCandidate = fromTable.Requirements(0)
        Assert.IsTrue(row.Found)
        Assert.AreEqual("Brief report" & vbTab & "3,000" & vbTab & "150", row.Quote)
        Assert.IsTrue(row.NumbersMatch)
        Assert.IsTrue(fromTable.Requirements(1).Found)
        Assert.IsFalse(fromTable.Requirements(1).NumbersMatch, "200 isn't in the row.")
    End Sub

    <TestMethod>
    Public Sub NumbersAreReadAsTheTextWritesThem()
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 8000 words", "no more than 8,000 words"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 8,000 words", "no more than 8 000 words"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 8000 words", "no more than 8" & ChrW(&HA0) & "000 words"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 8000 words", "no more than 8" & ChrW(&H202F) & "000 words"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("Line spacing of 1.5", "Use 1.5 line spacing."))
        Assert.IsTrue(AssistantService.NumbersAppearIn("A title page", "Include a title page."), "No numbers, nothing to differ.")
        Assert.IsFalse(AssistantService.NumbersAppearIn("At most 800 words", "no more than 8,000 words"))
        Assert.IsFalse(AssistantService.NumbersAppearIn("Line spacing of 2", "Use 1.5 line spacing."))
        Assert.IsFalse(AssistantService.NumbersAppearIn("Up to 5 keywords", "Provide up to six keywords."))
        Assert.IsFalse(AssistantService.NumbersAppearIn("At most 250 words and 6 keywords", "An abstract of at most 250 words."), "Every number counts.")

        ' Numbers written as words count too.
        Assert.IsTrue(AssistantService.NumbersAppearIn("Up to 6 keywords", "Provide up to six keywords."))
        Assert.IsTrue(AssistantService.NumbersAppearIn("Up to Six keywords", "Provide up to six keywords."))
        Assert.IsFalse(AssistantService.NumbersAppearIn("Up to eight keywords", "Provide up to six keywords."), "A wrong word is caught.")
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 25 references", "List no more than twenty-five references."))
        Assert.IsFalse(AssistantService.NumbersAppearIn("At most 20 references", "List no more than twenty-five references."))
        Assert.IsTrue(AssistantService.NumbersAppearIn("Someone's ORCID iD", "Give the corresponding author's ORCID iD."), "Only whole words.")

        ' A thin-space thousands separator, and a table row pasted with spaces.
        Assert.IsTrue(AssistantService.NumbersAppearIn("At most 8000 words", "no more than 8" & ChrW(&H2009) & "000 words"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("Brief report of at most 3,000 words", "Brief report 3,000 150"))
        Assert.IsTrue(AssistantService.NumbersAppearIn("Brief report abstract of at most 150 words", "Brief report 3,000 150"))
        Assert.IsFalse(AssistantService.NumbersAppearIn("Brief report of at most 3,000 words", "Brief report 2,000 150"))

        Assert.IsTrue(AssistantService.SameRequirement("Data availability statement", "  data  AVAILABILITY statement "))
        Assert.IsTrue(AssistantService.SameRequirement("Author" & ChrW(&H2019) & "s contributions " & ChrW(&H2013) & " CRediT", "Author's contributions - CRediT"))
        Assert.IsFalse(AssistantService.SameRequirement("Data availability statement", "Data availability"))
    End Sub

    <TestMethod>
    Public Sub AnAnswerCutShortSaysSo()
        Dim cutShort As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(Reply(truncated:=True), Instructions, Nothing)
        Assert.IsTrue(cutShort.Truncated, "Finished JSON, but the list may be missing requirements.")

        Dim broken As AssistantException = Assert.ThrowsExactly(Of AssistantException)(
            Sub() AssistantService.ReadAuthorInstructionsReply(Reply(InstructionsAnswer.Substring(0, 300), truncated:=True), Instructions, Nothing))
        Assert.AreEqual("The answer was cut short before it finished. Paste the instructions in parts, such as one section at a time. Nothing was changed.", broken.Message)

        Dim wrong As AssistantException = Assert.ThrowsExactly(Of AssistantException)(
            Sub() AssistantService.ReadAuthorInstructionsReply(Reply("I can't help with that."), Instructions, Nothing))
        Assert.AreEqual("The answer wasn't in the expected format. Nothing was changed.", wrong.Message)

        Dim empty As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(Reply("{""requirements"":[]}"), Instructions, Nothing)
        Assert.AreEqual(0, empty.Requirements.Count)
    End Sub


    ' ---------------------------------------------------------------
    ' From a journal's Readiness Checklist tab
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub AcceptedRequirementsJoinTheChecklistAndKeepWhereTheyCameFrom()
        RunOnSta(
            Sub()
                Dim record As New JournalRecord With {.Name = "Fictional Open Psychology", .AuthorInstructionsUrl = Link, .ReadinessChecklistTemplate = Existing()}
                Dim opened As New List(Of String)()

                AuthorInstructionsForm.LinkOpener = Sub(url) opened.Add(url)
                AuthorInstructionsForm.DialogRunner =
                    Function(dialog, owner)
                        Dim reader As AuthorInstructionsForm = DirectCast(dialog, AuthorInstructionsForm)
                        ShowOffscreen(reader)
                        Assert.AreEqual("Read Author Instructions", reader.Text)
                        Assert.AreEqual(String.Empty, reader.InstructionsBox.Text)
                        Assert.IsTrue(reader.OpenLink.Visible)
                        Assert.AreEqual("Open the author instructions for Fictional Open Psychology", reader.OpenLink.Text)
                        reader.OpenLinkForTest()
                        CollectionAssert.AreEqual({Link}, opened, "Opened in the browser, never fetched.")
                        AuthorInstructionsForm.LinkOpener = Sub(url) Throw New InvalidOperationException("No browser is set up.")
                        reader.OpenLinkForTest()
                        Assert.AreEqual("The link couldn't be opened: No browser is set up.", reader.StatusText)

                        reader.InstructionsBox.Text = Instructions
                        reader.ReadAsync().GetAwaiter().GetResult()
                        Assert.IsTrue(reader.IsReviewing, reader.StatusText)
                        Assert.AreEqual("AI suggestion from Claude (claude-opus-5-5): check each requirement before adding. The assistant can miss requirements, including ones on other pages, so read the journal's instructions too.", reader.IntroText)

                        Dim rows As IReadOnlyList(Of RequirementRow) = reader.Rows
                        Assert.AreEqual(8, rows.Count)
                        CollectionAssert.AreEqual(
                            {AuthorInstructionsForm.InTextText, AuthorInstructionsForm.InTextText, AuthorInstructionsForm.NumbersDifferText, AuthorInstructionsForm.AlreadyInChecklistText,
                             AuthorInstructionsForm.InTextText, AuthorInstructionsForm.InTextText, AuthorInstructionsForm.NotFoundText, AuthorInstructionsForm.InTextText},
                            rows.Select(Function(row) row.StatusText).ToList())
                        CollectionAssert.AreEqual({True, True, False, False, True, True, False, True}, rows.Select(Function(row) row.Use).ToList(), "Only what the text plainly supports starts checked.")
                        Assert.AreEqual("Add 5 Requirements", reader.PrimaryButton.Text)
                        Assert.AreEqual("5 requirements to add. They join the checklist, and are kept when you save the journal.", reader.StatusText)
                        Assert.AreEqual("Peer review", CStr(reader.RequirementsGrid.Rows(3).Cells(2).Value), "The checklist's own category.")

                        ' Correcting the number clears the warning; then it can be added.
                        reader.SetTitleForTest(2, "  Up to six   keywords ")
                        Assert.AreEqual("Up to six keywords", rows(2).Title)
                        Assert.AreEqual(AuthorInstructionsForm.InTextText, rows(2).StatusText)
                        Assert.AreEqual(AuthorInstructionsForm.InTextText, CStr(reader.RequirementsGrid.Rows(2).Cells(5).Value))
                        Assert.IsFalse(rows(2).Use, "Still the researcher's choice.")
                        reader.SetUseForTest(2, True)
                        ' One not in the text can be added, after a look.
                        reader.SetUseForTest(6, True)
                        reader.SetUseForTest(7, False)
                        reader.SetRequiredForTest(4, False)
                        Assert.AreEqual("Add 6 Requirements", reader.PrimaryButton.Text)
                        reader.AcceptForTest()
                        Return Finish(reader)
                    End Function

                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    ShowChecklistTab(editor)
                    Assert.IsFalse(editor.ReadInstructionsButton.Visible, "Off until the assistant is turned on.")
                End Using

                Dim provider As FakeProvider = UseProvider()
                Using editor As New JournalEditForm(record)
                    ShowOffscreen(editor)
                    ShowChecklistTab(editor)
                    Assert.IsTrue(editor.ReadInstructionsButton.Visible AndAlso editor.ReadInstructionsButton.Enabled)
                    Assert.AreEqual("Read Author Instructions...", editor.ReadInstructionsButton.Text)
                    AssertInside(editor.ReadInstructionsButton)
                    editor.ReadAuthorInstructionsForTest()

                    Assert.AreEqual(1, provider.Requests.Count)
                    Assert.AreEqual(Instructions, provider.Requests(0).Content, "Only the pasted text is sent.")
                    Assert.AreEqual(AssistantService.AuthorInstructionsFeature, provider.Requests(0).Feature)
                    Assert.IsFalse(provider.Requests(0).Instructions.Contains("Peer review"), "The checklist's own categories stay on this computer...")

                    Dim items As IReadOnlyList(Of JournalChecklistTemplateItem) = editor.ChecklistItems
                    CollectionAssert.AreEqual(
                        {"anonymized   Manuscript File", "Main text of at most 8,000 words", "Structured abstract of at most 250 words", "Up to six keywords",
                         "Data availability statement", "Follow a reporting guideline", "One-page cover letter"},
                        items.Select(Function(item) item.Title).ToList(), "Added after the checklist's own, in order.")
                    CollectionAssert.AreEqual({10, 20, 30, 40, 50, 60, 70}, items.Select(Function(item) item.SortOrder).ToList())
                    CollectionAssert.AreEqual({True, True, True, True, False, False, True}, items.Select(Function(item) item.IsRequired).ToList(), "As set in the review.")
                    StringAssert.EndsWith(editor.ChecklistLines(1), "Main text of at most 8,000 words  •  AI suggestion")
                    Assert.IsFalse(editor.ChecklistLines(0).Contains("AI suggestion"))

                    editor.SelectChecklistItemForTest(1)
                    StringAssert.Contains(editor.ChecklistInfoText, "Research articles should not exceed 8,000 words, including references. Applies to: Research articles.")
                    StringAssert.Contains(editor.ChecklistInfoText, Environment.NewLine & "Began as an AI suggestion from the journal's author instructions (Claude, claude-opus-5-5, ")
                    Assert.IsFalse(editor.ChecklistInfoText.Contains("over a year ago"))

                    editor.SaveForTest()
                    Dim saved As List(Of JournalChecklistTemplateItem) = editor.Result.ReadinessChecklistTemplate
                    Assert.AreEqual(7, saved.Count)
                    Assert.IsNull(saved(0).Suggestion)
                    For Each item As JournalChecklistTemplateItem In saved.Skip(1)
                        Assert.IsNotNull(item.Suggestion, item.Title)
                        Assert.AreEqual(AssistantService.AuthorInstructionsFeature, item.Suggestion.Feature)
                        Assert.AreEqual("Claude", item.Suggestion.Provider)
                        Assert.AreEqual("claude-opus-5-5", item.Suggestion.Model)
                        Assert.IsTrue(item.Suggestion.SuggestedUtc.Value > DateTime.UtcNow.AddMinutes(-5))
                    Next
                    Assert.AreEqual("Research articles should not exceed 8,000 words, including references.", saved(1).Suggestion.SourceText)
                    Assert.AreEqual("Provide up to six keywords.", saved(3).Description, "Edited title, the text's own words kept.")
                    Assert.AreEqual(String.Empty, saved(6).Suggestion.SourceText, "Not in the text: nothing is recorded as its source.")
                    Assert.AreEqual(String.Empty, saved(6).Description)
                    Assert.AreEqual("Editorial", saved(6).Category)
                    Assert.IsFalse(record.ReadinessChecklistTemplate.Any(Function(item) item.Suggestion IsNot Nothing), "The record itself changes only when the library saves the result.")
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheReviewShowsEachRequirementsSourceAndReadAgainKeepsTheText()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                provider.Truncated = True
                Using reader As New AuthorInstructionsForm("Fictional Open Psychology", "javascript:alert(1)", {"Anonymized manuscript file"}, {"Peer review"}, Instructions)
                    ShowOffscreen(reader)
                    Assert.IsFalse(reader.OpenLink.Visible, "Only a web address is offered.")
                    Assert.AreEqual(Instructions, reader.InstructionsBox.Text, "The text passed in is ready to read.")
                    Assert.AreEqual("Read Instructions sends only the text you paste here, and the article type if you give one, to Claude, with PaperRoute's instructions. Nothing from your library is sent.", reader.IntroText)
                    Assert.IsFalse(reader.ReadAgainButton.Visible)

                    reader.ArticleTypeBox.Text = "Brief report"
                    reader.ReadAsync().GetAwaiter().GetResult()
                    Assert.AreEqual("Article type: Brief report" & vbLf & vbLf & Instructions, provider.Requests(0).Content)
                    Assert.IsTrue(reader.TruncatedNoteShown, "The answer was cut short, so some requirements may be missing.")
                    Assert.IsTrue(reader.ReadAgainButton.Visible)
                    Assert.AreEqual(Instructions, reader.SourceBox.Text)

                    ' Selecting a requirement selects its sentence in the text.
                    reader.SelectRequirementForTest(0)
                    Application.DoEvents()
                    Dim first As RequirementCandidate = reader.Rows(0).Candidate
                    Assert.AreEqual(first.SourceStart, reader.SourceBox.SelectionStart)
                    Assert.AreEqual(first.SourceLength, reader.SourceBox.SelectionLength)
                    StringAssert.StartsWith(reader.QuoteBox.Text, "Research articles should not exceed 8,000 words, including references.")
                    StringAssert.EndsWith(reader.QuoteBox.Text, "Applies to: Research articles.")

                    reader.SelectRequirementForTest(2)
                    StringAssert.Contains(reader.QuoteBox.Text, "A number in the requirement isn't in this sentence.")
                    reader.SelectRequirementForTest(3)
                    StringAssert.Contains(reader.QuoteBox.Text, "The checklist already has a requirement with this title.")
                    reader.SelectRequirementForTest(6)
                    StringAssert.StartsWith(reader.QuoteBox.Text, "The assistant's quote isn't in the text, so it may be invented.")
                    StringAssert.EndsWith(reader.QuoteBox.Text, "Include a one-page cover letter addressed to the editor.")
                    Assert.AreEqual(0, reader.SourceBox.SelectionLength, "Nothing to show in the text.")

                    ' Whether a requirement is already listed follows its title.
                    reader.SetTitleForTest(3, "Remove identifying information")
                    Assert.AreEqual(AuthorInstructionsForm.InTextText, reader.Rows(3).StatusText)
                    reader.SelectRequirementForTest(3)
                    Assert.IsFalse(reader.QuoteBox.Text.Contains("already has"))
                    reader.SetTitleForTest(4, "anonymized  MANUSCRIPT file")
                    Assert.AreEqual(AuthorInstructionsForm.AlreadyInChecklistText, reader.Rows(4).StatusText)
                    Assert.AreEqual(AuthorInstructionsForm.AlreadyInChecklistText, CStr(reader.RequirementsGrid.Rows(4).Cells(5).Value))

                    ' A requirement can't be added without a title.
                    reader.SetTitleForTest(0, "   ")
                    reader.AcceptForTest()
                    Assert.AreEqual(DialogResult.None, reader.DialogResult)
                    Assert.AreEqual("Enter each requirement you add, or uncheck it.", reader.StatusText)
                    Assert.AreEqual(0, reader.RequirementsGrid.CurrentCell.RowIndex)
                    Assert.AreEqual(0, reader.AcceptedRequirements.Count)

                    ' Unchecking everything leaves nothing to add.
                    For index As Integer = 0 To reader.Rows.Count - 1
                        reader.SetUseForTest(index, False)
                    Next
                    Assert.IsFalse(reader.PrimaryButton.Enabled)
                    Assert.AreEqual("Add Requirements", reader.PrimaryButton.Text)
                    StringAssert.StartsWith(reader.StatusText, "No requirements to add.")
                    reader.SetUseForTest(1, True)
                    Assert.AreEqual("Add 1 Requirement", reader.PrimaryButton.Text)

                    ' Read Again goes back to the text, still pasted.
                    reader.ReadAgainButton.PerformClick()
                    Assert.IsFalse(reader.IsReviewing)
                    Assert.AreEqual(Instructions, reader.InstructionsBox.Text)
                    Assert.AreEqual("Brief report", reader.ArticleTypeBox.Text)
                    Assert.AreEqual("Read Instructions", reader.PrimaryButton.Text)
                    Assert.IsTrue(reader.PrimaryButton.Enabled)
                    reader.ReadAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(2, provider.Requests.Count)
                    CollectionAssert.AreEqual({True, True, False, False, True, True, False, True}, reader.Rows.Select(Function(row) row.Use).ToList(), "A fresh review.")
                    reader.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub OnlyTextOfASendableLengthIsSentAndWhatIsSentIsShown()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Using reader As New AuthorInstructionsForm(String.Empty, String.Empty, Nothing, Nothing)
                    ShowOffscreen(reader)
                    Assert.IsFalse(reader.OpenLink.Visible)
                    Assert.AreEqual("Read Instructions", reader.PrimaryButton.Text)
                    Assert.IsFalse(reader.PrimaryButton.Enabled, "Nothing to read yet.")
                    Assert.AreEqual("Copy the part of the journal's instructions for authors that lists what to prepare, and paste it here.", reader.StatusText)
                    Dim limit As String = AssistantService.MaximumInstructionsLength.ToString("N0", CultureInfo.CurrentCulture)
                    StringAssert.StartsWith(reader.CountText, "0 of " & limit & " characters.")

                    reader.InstructionsBox.Text = New String("x"c, AssistantService.MaximumInstructionsLength + 1)
                    Assert.IsFalse(reader.PrimaryButton.Enabled)
                    Assert.AreEqual("The text is longer than PaperRoute sends at once. Paste one section at a time.", reader.StatusText)
                    reader.ReadAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(0, provider.Requests.Count)
                    reader.InstructionsBox.Text = New String("x"c, AssistantService.MaximumInstructionsLength)
                    Assert.IsTrue(reader.PrimaryButton.Enabled)
                    Assert.AreEqual(String.Empty, reader.StatusText)
                    StringAssert.StartsWith(reader.CountText, limit & " of " & limit & " characters.")

                    reader.InstructionsBox.Text = Instructions
                    Assert.IsFalse(reader.SentBox.Visible)
                    reader.ToggleSentForTest()
                    Assert.IsTrue(reader.SentBox.Visible)
                    Dim sent As String = AssistantService.WhatIsSent(AssistantService.BuildAuthorInstructionsRequest(Instructions, String.Empty))
                    Assert.AreEqual(sent.Replace(vbCrLf, vbLf).Replace(vbLf, vbCrLf), reader.SentBox.Text)
                    reader.ArticleTypeBox.Text = "Registered report"
                    StringAssert.Contains(reader.SentBox.Text, "Article type: Registered report", "What is shown follows the article type...")
                    reader.InstructionsBox.AppendText(vbCrLf & "Supplementary material is published as submitted.")
                    StringAssert.Contains(reader.SentBox.Text, "Supplementary material is published as submitted.", "...and the text.")
                    reader.ToggleSentForTest()
                    Assert.IsFalse(reader.SentBox.Visible)
                    reader.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CancelAFailureOrDecliningToSendChangesNothing()
        RunOnSta(
            Sub()
                Dim provider As FakeProvider = UseProvider()
                Dim record As New JournalRecord With {.Name = "Fictional Open Psychology", .ReadinessChecklistTemplate = Existing()}

                ' Cancel after reading.
                AuthorInstructionsForm.DialogRunner =
                    Function(dialog, owner)
                        Dim reader As AuthorInstructionsForm = DirectCast(dialog, AuthorInstructionsForm)
                        ShowOffscreen(reader)
                        reader.InstructionsBox.Text = Instructions
                        reader.ReadAsync().GetAwaiter().GetResult()
                        Assert.IsTrue(reader.IsReviewing)
                        reader.CancelForTest()
                        Return Finish(reader)
                    End Function
                Using editor As New JournalEditForm(record)
                    editor.ReadAuthorInstructionsForTest()
                    Assert.AreEqual(1, editor.ChecklistItems.Count, "Cancel adds nothing.")
                End Using

                ' A failure says so plainly and keeps the text to try again.
                Dim serverError As Exception = New HttpRequestException("Server error", Nothing, HttpStatusCode.InternalServerError)
                Dim failures = {
                    (Problem:=serverError, Answer:=InstructionsAnswer, Truncated:=False, Expected:="Claude had a problem on its side (HTTP 500). Try again later. Nothing was changed."),
                    (Problem:=CType(Nothing, Exception), Answer:="I can't help with that.", Truncated:=False, Expected:="The answer wasn't in the expected format. Nothing was changed."),
                    (Problem:=CType(Nothing, Exception), Answer:=InstructionsAnswer.Substring(0, 300), Truncated:=True, Expected:="The answer was cut short before it finished. Paste the instructions in parts, such as one section at a time. Nothing was changed.")
                }
                For Each failure In failures
                    provider.Failure = failure.Problem
                    provider.Answer = failure.Answer
                    provider.Truncated = failure.Truncated
                    Using reader As New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, Nothing, Nothing, Instructions)
                        ShowOffscreen(reader)
                        reader.ReadAsync().GetAwaiter().GetResult()
                        Assert.IsFalse(reader.IsReviewing)
                        Assert.AreEqual(failure.Expected, reader.StatusText)
                        Assert.AreEqual(Instructions, reader.InstructionsBox.Text, "The text is kept to try again.")
                        Assert.IsFalse(reader.InstructionsBox.ReadOnly)
                        Assert.IsTrue(reader.PrimaryButton.Enabled)
                        Assert.AreEqual("Cancel", reader.CancelButtonForTest.Text)
                        Assert.AreEqual(0, reader.AcceptedRequirements.Count)
                        reader.Close()
                    End Using
                Next

                ' Declining to send sends nothing.
                provider.Failure = Nothing
                provider.Answer = InstructionsAnswer
                provider.Truncated = False
                provider.Requests.Clear()
                Dim asked As New List(Of AssistantRequest)()
                AssistantRunner.ConsentPrompt =
                    Function(owner, request, connection)
                        asked.Add(request)
                        Return CType(Nothing, Boolean?)
                    End Function
                Using reader As New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, Nothing, Nothing, Instructions)
                    ShowOffscreen(reader)
                    reader.ReadAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(Instructions, asked.Single().Content, "The question shows exactly the text.")
                    Assert.AreEqual(0, provider.Requests.Count, "Nothing was sent.")
                    Assert.AreEqual("Nothing was sent.", reader.StatusText)
                    Assert.IsFalse(reader.IsReviewing)
                    reader.Close()
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
                Using reader As New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, Nothing, Nothing, Instructions)
                    ShowOffscreen(reader)
                    StringAssert.Contains(reader.IntroText, "to Claude at api.anthropic.com, with PaperRoute's instructions.")
                    reader.ReadAsync().GetAwaiter().GetResult()
                    StringAssert.StartsWith(reader.StatusText, "Add your Claude key")
                    Assert.IsFalse(reader.IsReviewing)

                    Dim offline As OnlineServicesSettings = AssistantCoreTests.ClaudeSettings()
                    offline.WorkOffline = True
                    OnlineAccess.Configure(offline)
                    reader.ReadAsync().GetAwaiter().GetResult()
                    Assert.AreEqual(AssistantRunner.Unavailable(), reader.StatusText)
                    reader.Close()
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
                Dim reader As New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, Nothing, Nothing, Instructions)
                Try
                    ShowOffscreen(reader)
                    Dim reading As Task = reader.ReadAsync()
                    Assert.IsTrue(reader.IsRunning)
                    Assert.AreEqual("Stop", reader.CancelButtonForTest.Text)
                    Assert.IsFalse(reader.PrimaryButton.Enabled)
                    Assert.IsTrue(reader.InstructionsBox.ReadOnly)
                    Assert.AreEqual("Reading the instructions with Claude...", reader.StatusText)
                    reader.CancelButtonForTest.PerformClick()
                    Assert.AreEqual(DialogResult.None, reader.DialogResult, "Stop doesn't ask the window to close.")
                    PumpUntil(Function() reading.IsCompleted)
                    Assert.IsFalse(reader.IsRunning)
                    Assert.IsFalse(reader.IsReviewing)
                    Assert.AreEqual("Stopped. Nothing was changed.", reader.StatusText)
                    Assert.AreEqual("Cancel", reader.CancelButtonForTest.Text)
                    Assert.IsTrue(reader.CancelButtonForTest.Enabled)
                    Assert.IsFalse(reader.InstructionsBox.ReadOnly)
                    Assert.IsFalse(reader.IsDisposed, "Stop doesn't close the window.")

                    Dim closing As Task = reader.ReadAsync()
                    Assert.IsTrue(reader.IsRunning)
                    reader.Close()
                    PumpUntil(Function() closing.IsCompleted AndAlso reader.IsDisposed)
                    Assert.AreEqual(DialogResult.Cancel, reader.DialogResult)
                    Assert.AreEqual(2, provider.Requests.Count)
                    Assert.AreEqual(0, reader.AcceptedRequirements.Count)
                Finally
                    reader.Dispose()
                End Try
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Kept, copied, and shown
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub WhereARequirementCameFromIsStoredCopiedAndShown()
        RunOnSta(
            Sub()
                Dim now As DateTime = DateTime.UtcNow
                Dim fresh As AssistantSuggestion = AssistantService.SuggestionFor(AssistantService.AuthorInstructionsFeature, Reply(), "A data availability statement is required for all articles.", now.AddDays(-30))
                Dim old As AssistantSuggestion = AssistantService.SuggestionFor(AssistantService.AuthorInstructionsFeature, Reply(), "Provide up to six keywords.", now.AddDays(-400))
                Dim journal As New JournalRecord With {.Name = "Fictional Open Psychology"}
                journal.ReadinessChecklistTemplate.Add(New JournalChecklistTemplateItem With {.Title = "Data availability statement", .Category = "Compliance", .SortOrder = 10, .Suggestion = fresh})
                journal.ReadinessChecklistTemplate.Add(New JournalChecklistTemplateItem With {.Title = "Up to six keywords", .Category = "Manuscript", .SortOrder = 20, .Suggestion = old})
                journal.ReadinessChecklistTemplate.Add(New JournalChecklistTemplateItem With {.Title = "Cover letter", .Category = "Editorial", .SortOrder = 30, .Suggestion = New AssistantSuggestion()})

                ' An empty record is dropped when the journal is checked.
                SubmissionReadinessValidationService.NormalizeAndValidateJournal(journal)
                Assert.IsNull(journal.ReadinessChecklistTemplate(2).Suggestion)

                Dim text As String = AssistantSuggestionService.DescribeRequirement(old, now)
                StringAssert.StartsWith(text, "Began as an AI suggestion from the journal's author instructions (Claude, claude-opus-5-5, ")
                StringAssert.EndsWith(text, "). That was over a year ago; check the journal's current instructions.")
                Assert.IsFalse(AssistantSuggestionService.DescribeRequirement(fresh, now).Contains("over a year"))
                Assert.AreEqual(String.Empty, AssistantSuggestionService.DescribeRequirement(Nothing, now))

                ' Kept in the journal library...
                Dim repository As New AuthorLibraryRepository(Path.Combine(_directory, "data"))
                Dim library As New AuthorLibraryData()
                library.Journals.Add(journal)
                repository.Save(library)
                Dim loaded As JournalRecord = repository.Load().Journals.Single()
                Assert.AreEqual("Provide up to six keywords.", loaded.ReadinessChecklistTemplate(1).Suggestion.SourceText)
                Assert.AreEqual(AssistantService.AuthorInstructionsFeature, loaded.ReadinessChecklistTemplate(0).Suggestion.Feature)

                ' ...copied into a manuscript's readiness, not shared...
                Dim manuscript As New Manuscript With {.Title = "Example: open materials"}
                Dim profile As ManuscriptReadiness = SubmissionReadinessService.CreateProfileFromJournal(manuscript, loaded)
                Assert.AreEqual("claude-opus-5-5", profile.Items(0).Suggestion.Model)
                Assert.AreNotSame(loaded.ReadinessChecklistTemplate(0).Suggestion, profile.Items(0).Suggestion)
                Assert.IsNull(profile.Items(2).Suggestion)
                Dim clone As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
                Assert.AreNotSame(profile.Items(1).Suggestion, clone.ReadinessProfiles(0).Items(1).Suggestion)
                Assert.AreEqual(JsonSerializer.Serialize(manuscript.ReadinessProfiles), JsonSerializer.Serialize(clone.ReadinessProfiles), "Every field is copied.")

                ' ...and kept with the manuscript.
                profile.Items(2).Suggestion = New AssistantSuggestion()
                AssistantSuggestionService.NormalizeManuscript(manuscript)
                Assert.IsNull(profile.Items(2).Suggestion, "An empty record is dropped, with no submissions.")
                Dim manuscripts As New ManuscriptRepository(Path.Combine(_directory, "data"), Path.Combine(_directory, "managed"))
                manuscripts.Save(New List(Of Manuscript) From {manuscript})
                Dim reloaded As ManuscriptReadiness = manuscripts.Load().Single().ReadinessProfiles.Single()
                Assert.AreEqual("Provide up to six keywords.", reloaded.Items(1).Suggestion.SourceText)

                ' A portable backup carries both, and restoring it keeps them.
                Dim backupPath As String = Path.Combine(_directory, "backup.zip")
                Call New PortableBackupService(Path.Combine(_directory, "managed")).CreateBackup(backupPath, New List(Of Manuscript) From {manuscript}, manuscripts)
                Dim restoredData As String = Path.Combine(_directory, "restored-data")
                Dim restoredManaged As String = Path.Combine(_directory, "restored-managed")
                Dim target As New ManuscriptRepository(restoredData, restoredManaged)
                Dim current As New List(Of Manuscript)()
                target.Save(current)
                Call New PortableRestoreService(restoredManaged).RestoreBackup(backupPath, current, target)
                Assert.AreEqual("Provide up to six keywords.", target.Load().Single().ReadinessProfiles.Single().Items(1).Suggestion.SourceText)
                Assert.AreEqual("Provide up to six keywords.", New AuthorLibraryRepository(restoredData).Load().Journals.Single().ReadinessChecklistTemplate(1).Suggestion.SourceText)

                ' Submission Readiness marks it, says where it came from, and
                ' that the list may be incomplete.
                Using readiness As New ReadinessProbe(manuscript, library)
                    ShowOffscreen(readiness)
                    StringAssert.EndsWith(readiness.SummaryText, Environment.NewLine & ManuscriptReadinessForm.AiFoundNote)
                    readiness.Size = readiness.MinimumSize
                    readiness.PerformLayout()
                    Application.DoEvents()
                    For Each button As Button In Descendants(readiness).OfType(Of Button)()
                        AssertInside(button)
                    Next
                    Dim lines As List(Of String) = readiness.ItemLines
                    StringAssert.EndsWith(lines(0), "Data availability statement  •  AI suggestion")
                    Assert.IsFalse(lines(2).Contains("AI suggestion"))
                    readiness.SelectItemForTest(1)
                    StringAssert.Contains(readiness.ItemDetailText, "Began as an AI suggestion from the journal's author instructions (Claude, claude-opus-5-5, ")
                    StringAssert.Contains(readiness.ItemDetailText, "That was over a year ago; check the journal's current instructions.")
                    readiness.SelectItemForTest(2)
                    Assert.IsFalse(readiness.ItemDetailText.Contains("AI suggestion"))
                    readiness.Close()
                End Using

                ' Without AI-found requirements, no note.
                For Each item As ReadinessItemState In profile.Items
                    item.Suggestion = Nothing
                Next
                Using readiness As New ReadinessProbe(manuscript, library)
                    Assert.IsFalse(readiness.SummaryText.Contains(ManuscriptReadinessForm.AiFoundNote))
                End Using

                ' Editing a requirement keeps where it came from.
                Using edit As New JournalChecklistItemEditForm(loaded.ReadinessChecklistTemplate(0))
                    edit.SaveForTest()
                    Assert.AreEqual("claude-opus-5-5", edit.Result.Suggestion.Model)
                    Assert.AreNotSame(loaded.ReadinessChecklistTemplate(0).Suggestion, edit.Result.Suggestion)
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Layout
    ' ---------------------------------------------------------------

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    <DataRow(SystemColorMode.System)>
    Public Sub EveryStepFitsAtItsMinimumSize(mode As SystemColorMode)
        RunOnSta(
            Sub()
                UseProvider()
                Using reader As New AuthorInstructionsForm("Fictional Open Psychology", Link, {"Anonymized manuscript file"}, {"Peer review"}, Instructions)
                    ShowOffscreen(reader)
                    reader.Size = reader.MinimumSize
                    reader.PerformLayout()
                    Application.DoEvents()
                    reader.ToggleSentForTest()
                    Application.DoEvents()
                    For Each control As Control In {reader.OpenLink, reader.ArticleTypeBox, reader.InstructionsBox, reader.SentBox, reader.PrimaryButton, reader.CancelButtonForTest}
                        AssertInside(control)
                    Next
                    Assert.IsTrue(reader.InstructionsBox.ClientSize.Height >= reader.InstructionsBox.Font.Height * 4,
                                  "The text box keeps a few lines: " & reader.InstructionsBox.ClientSize.Height.ToString() & " for a line of " & reader.InstructionsBox.Font.Height.ToString())

                    reader.ReadAsync().GetAwaiter().GetResult()
                    Application.DoEvents()
                    Assert.IsTrue(reader.IsReviewing)
                    For Each control As Control In {reader.SourceBox, reader.RequirementsGrid, reader.QuoteBox, reader.PrimaryButton, reader.CancelButtonForTest, reader.ReadAgainButton}
                        AssertInside(control)
                    Next
                    Dim grid As DataGridView = reader.RequirementsGrid
                    Assert.IsTrue(grid.ClientSize.Height >= grid.ColumnHeadersHeight + grid.Rows(0).Height * 3, "At least three requirements show.")
                    Assert.IsTrue(reader.QuoteBox.ClientSize.Height >= reader.QuoteBox.Font.Height * 2, "The quote keeps two lines.")
                    AssertNoLostAmpersands(reader)
                    AssertDistinctAccelerators(reader)
                    reader.Close()
                End Using

                OnlineAccess.Configure(AssistantCoreTests.ClaudeSettings())
                Using editor As New JournalEditForm(New JournalRecord With {.Name = "Fictional Open Psychology"})
                    ShowOffscreen(editor)
                    editor.Size = editor.MinimumSize
                    editor.PerformLayout()
                    ShowChecklistTab(editor)
                    Application.DoEvents()
                    AssertInside(editor.ReadInstructionsButton)
                    editor.Close()
                End Using
            End Sub, mode)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Sub ShowChecklistTab(editor As Form)
        For Each tabs As TabControl In Descendants(editor).OfType(Of TabControl)()
            For Each page As TabPage In tabs.TabPages
                If page.Text.Contains("Checklist") Then tabs.SelectedTab = page
            Next
        Next
        editor.PerformLayout()
        Application.DoEvents()
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
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(5)), "The author instructions UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub


    ' Answers like a model would, without one; records what it was sent.
    Private NotInheritable Class FakeProvider
        Implements IAssistantProvider

        Public ReadOnly Requests As New List(Of AssistantRequest)()

        Public Property Answer As String = InstructionsAnswer

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

    Private Class ReadinessProbe
        Inherits ManuscriptReadinessForm

        Public Sub New(manuscript As Manuscript, library As AuthorLibraryData)
            MyBase.New(manuscript, library)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
