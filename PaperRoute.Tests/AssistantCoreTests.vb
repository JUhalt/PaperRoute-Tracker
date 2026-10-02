Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The optional AI assistant's core (#84): off until turned on, every
' request through the gate with only what it needs, keys only to their own
' service, and answers checked against what was sent. No test reaches the
' network or a model.
<TestClass>
<DoNotParallelize>
Public Class AssistantCoreTests

    Private Shared ReadOnly Today As New DateTime(2026, 10, 1)
    Private _directory As String
    Private _keys As ProtectedKeyStore

    Friend Const Letter As String =
        "Fictional Journal of Psychology" & vbCrLf & vbCrLf &
        "Dear Dr. Example," & vbCrLf & vbCrLf &
        "Thank you for submitting ""Example: anchoring effects in clinical risk estimates"" (FJP-2026-0142). It was reviewed by two experts. " &
        "Based on their comments, I would like to invite a major revision. Please submit your revised manuscript within 60 days." & vbCrLf & vbCrLf &
        "Reviewer 1" & vbCrLf &
        "1. The sample size justification is unclear;" & vbCrLf & "please report the power analysis." & vbCrLf &
        "2. Report the preregistered exclusion criteria in the main text." & vbCrLf & vbCrLf &
        "Reviewer 2" & vbCrLf &
        "The discussion should address how clinicians" & ChrW(&H2019) & " experience might moderate anchoring." & vbCrLf & vbCrLf &
        "Sincerely," & vbCrLf & "Riley Placeholder, Editor"

    ' What a model might answer: one comment re-spaced, one with a plain
    ' apostrophe, one invented, and one repeated.
    Friend Const LetterAnswer As String =
        "{""decision"":""major_revision"",""decision_quote"":""Based on their comments, I would like to invite a major revision."",""decision_date"":""2026-09-15""," &
        """deadline_date"":"""",""deadline_days"":60,""deadline_quote"":""Please submit your revised manuscript within 60 days.""," &
        """comments"":[" &
        "{""reviewer"":""Reviewer 1"",""text"":""The sample size justification is unclear; please report the power analysis.""}," &
        "{""reviewer"":""Reviewer 1"",""text"":""Report the preregistered exclusion criteria in the main text.""}," &
        "{""reviewer"":""Reviewer 2"",""text"":""The discussion should address how clinicians' experience might moderate anchoring.""}," &
        "{""reviewer"":""Reviewer 2"",""text"":""Please add a figure showing the anchoring effect by experience.""}," &
        "{""reviewer"":""Reviewer 1"",""text"":""Report the preregistered exclusion criteria in the main text.""}]}"

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        _directory = TestSupport.CreateTemporaryRoot()
        _keys = New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() _keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    ' ---------------------------------------------------------------
    ' Off until turned on
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheAssistantIsOffUntilTurnedOnAndWorkOfflineTurnsItOff()
        Dim network As CapturingNetwork = UseNetwork(Function(request) Answer(HttpStatusCode.OK, ClaudeAnswer("{}")))
        OnlineAccess.Configure(New OnlineServicesSettings())
        Assert.AreEqual(OnlineBlockReason.NotTurnedOn, OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantClaude))
        Assert.AreEqual(OnlineBlockReason.NotTurnedOn, OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantCompatible))
        Assert.IsNull(OnlineAccess.CurrentAssistant())
        Assert.IsNull(AssistantService.CurrentProvider())
        StringAssert.Contains(OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantClaude), OnlineBlockReason.NotTurnedOn), "Settings > Preferences > AI assistant")
        Dim off As New ClaudeAssistantProvider("claude-opus-5-5")
        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() off.CompleteAsync(AssistantService.BuildLetterRequest(Letter), CancellationToken.None).GetAwaiter().GetResult())

        ' On for Claude: the compatible service stays off.
        OnlineAccess.Configure(ClaudeSettings())
        Assert.IsFalse(OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantClaude).HasValue)
        Assert.AreEqual(OnlineBlockReason.NotTurnedOn, OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantCompatible))

        ' Work offline turns it off, a model on this computer included.
        Dim offline As OnlineServicesSettings = CompatibleSettings("http://localhost:11434/v1")
        offline.WorkOffline = True
        OnlineAccess.Configure(offline)
        Assert.AreEqual(OnlineBlockReason.WorkOffline, OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantCompatible))
        Assert.AreEqual(0, network.Requests.Count, "Nothing was sent.")
    End Sub

    <TestMethod>
    Public Sub OnlyAUsableAddressIsAccepted()
        For Each usable As String In {"http://localhost:11434/v1", "http://127.0.0.1:1234/v1/", "http://[::1]:8080/v1", "https://models.example.org/v1"}
            Assert.IsNotNull(OnlineAccess.ParseAssistantEndpoint(usable), usable)
        Next
        For Each refused As String In {"http://192.168.1.20:11434/v1", "http://localhost.example.com/v1", "http://127.0.0.1.nip.io/v1", "https://user:secret@models.example.org/v1",
                                       "https://models.example.org/v1?key=secret", "ftp://localhost/v1", "localhost:11434", "", "   "}
            Assert.IsNull(OnlineAccess.ParseAssistantEndpoint(refused), refused)
        Next
        Assert.AreEqual("http://localhost:11434", OnlineAccess.OriginOf(New Uri("http://LOCALHOST:11434/v1")))

        Dim settings As New AppSettings With {.OnlineServices = New OnlineServicesSettings With {.Assistant = New AssistantSettings With {
            .Endpoint = "https://user:secret@models.example.org/v1", .ClaudeModel = "bad model; drop table", .ConfirmedUses = New List(Of String) From {" a|b ", "A|B", ""}}}}
        Dim service As New AppSettingsService(_directory)
        service.Save(settings)
        Dim loaded As AssistantSettings = service.Load().OnlineServices.Assistant
        Assert.AreEqual(String.Empty, loaded.Endpoint, "An address with a password is never kept.")
        Assert.AreEqual("claude-opus-5-5", loaded.ClaudeModel)
        CollectionAssert.AreEqual({"a|b"}, loaded.ConfirmedUses)
        Assert.IsFalse(loaded.Enabled, "Off unless turned on.")
    End Sub

    <TestMethod>
    Public Sub ACompatibleServerIsReachedOnlyAtItsAddress()
        UseNetwork(Function(request) Answer(HttpStatusCode.OK, CompatibleAnswer("Hello")))
        OnlineAccess.Configure(CompatibleSettings("http://localhost:11434/v1"))
        Dim service As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantCompatible)
        OnlineAccess.CheckHost(service, New Uri("http://localhost:11434/v1/chat/completions"))
        For Each other As String In {"http://localhost:8080/v1/chat/completions", "https://localhost:11434/v1/chat/completions", "http://127.0.0.2:11434/v1/chat/completions", "https://api.anthropic.com/v1/messages"}
            Dim blocked As OnlineServiceBlockedException = Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() OnlineAccess.CheckHost(service, New Uri(other)))
            Assert.AreEqual(OnlineBlockReason.UnexpectedHost, blocked.Reason, other)
        Next
        Dim claude As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantClaude)
        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() OnlineAccess.CheckHost(claude, New Uri("http://api.anthropic.com/v1/messages")))
    End Sub


    ' ---------------------------------------------------------------
    ' Claude, through the official SDK and the gate
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ClaudeGetsTheLetterInstructionsAndKeyAndNothingElse()
        Dim network As CapturingNetwork = UseNetwork(Function(request) Answer(HttpStatusCode.OK, ClaudeAnswer(LetterAnswer)))
        OnlineAccess.Configure(ClaudeSettings())
        _keys.Save(ProtectedKeyStore.Anthropic, "sk-ant-test-key-0001")
        Dim previousToken As String = Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN")
        Dim previousHeaders As String = Environment.GetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS")
        Dim previousBase As String = Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL")
        Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", "token-from-the-environment")
        Environment.SetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS", "X-From-Environment: yes")
        Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", "https://elsewhere.example.com")
        Dim reply As AssistantReply
        Try
            Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
            Assert.IsInstanceOfType(Of ClaudeAssistantProvider)(provider)
            reply = provider.CompleteAsync(AssistantService.BuildLetterRequest(Letter), CancellationToken.None).GetAwaiter().GetResult()
        Finally
            Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", previousToken)
            Environment.SetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS", previousHeaders)
            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", previousBase)
        End Try

        Dim sent As SentRequest = network.Requests.Single()
        Assert.AreEqual("https://api.anthropic.com/v1/messages?beta=true", sent.Uri.AbsoluteUri)
        Assert.AreEqual("sk-ant-test-key-0001", sent.Header("x-api-key"), "The gate adds the key; the SDK only holds a placeholder.")
        Assert.AreEqual("2023-06-01", sent.Header("anthropic-version"))
        Assert.AreEqual("server-side-fallback-2026-07-01", sent.Header("anthropic-beta"))
        StringAssert.StartsWith(sent.Header("User-Agent"), "PaperRoute-Tracker/")
        Assert.IsNull(sent.Header("Authorization"), "No token from the environment.")
        Assert.IsFalse(sent.HeaderNames.Any(Function(name) name.StartsWith("X-Stainless", StringComparison.OrdinalIgnoreCase) OrElse name.Equals("X-From-Environment", StringComparison.OrdinalIgnoreCase)),
                       "No system details or extra headers: " & String.Join(", ", sent.HeaderNames))

        Using body As JsonDocument = JsonDocument.Parse(sent.Body)
            Dim root As JsonElement = body.RootElement
            Assert.AreEqual("claude-opus-5-5", root.GetProperty("model").GetString())
            Assert.AreEqual(AssistantService.LetterInstructions, root.GetProperty("system").GetString())
            Assert.AreEqual(Letter.Trim(), root.GetProperty("messages")(0).GetProperty("content").GetString(), "The letter as pasted, and nothing else.")
            Assert.AreEqual(1, root.GetProperty("messages").GetArrayLength())
            Assert.AreEqual("medium", root.GetProperty("output_config").GetProperty("effort").GetString())
            Assert.AreEqual("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString())
            Assert.AreEqual("default", root.GetProperty("fallbacks").GetString())
        End Using

        Assert.AreEqual("Claude", reply.ProviderName)
        Dim proposal As DecisionLetterProposal = AssistantService.ReadLetterReply(reply, Letter, Today)
        Assert.AreEqual(EditorialDecision.MajorRevision, proposal.Decision)
        Assert.AreEqual(4, proposal.Comments.Count)
    End Sub

    <TestMethod>
    Public Sub ClaudeProblemsReadPlainlyAndChangeNothing()
        Dim network As CapturingNetwork = UseNetwork(Function(sent) Answer(HttpStatusCode.OK, ClaudeAnswer("{}")))
        OnlineAccess.Configure(ClaudeSettings())
        Dim provider As New ClaudeAssistantProvider("claude-opus-5-5")
        Dim request As AssistantRequest = AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", "")

        Dim noKey As AssistantException = Assert.ThrowsExactly(Of AssistantException)(Sub() provider.CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult())
        StringAssert.Contains(noKey.Message, "Add your Claude key")
        Assert.AreEqual(0, network.Requests.Count, "Without a key, nothing is sent.")
        StringAssert.StartsWith(AssistantRunner.Unavailable(), "Add your Claude key")

        _keys.Save(ProtectedKeyStore.Anthropic, "sk-ant-test-key-0001")
        Assert.AreEqual(String.Empty, AssistantRunner.Unavailable())
        Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
        For Each item In {(Status:=429, Expected:="Claude is busy"), (Status:=401, Expected:="didn't accept the key"), (Status:=529, Expected:="overloaded"), (Status:=500, Expected:="problem on its side")}
            Dim status As Integer = item.Status
            UseNetwork(Function(sent) Answer(CType(status, HttpStatusCode), "{""type"":""error"",""error"":{""type"":""x"",""message"":""m""}}"))
            OnlineAccess.Configure(ClaudeSettings())
            Dim failure As Exception = Nothing
            Try
                provider.CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult()
            Catch ex As Exception
                failure = ex
            End Try
            Assert.IsNotNull(failure, status.ToString())
            Dim text As String = AssistantRunner.Describe(failure, connection)
            StringAssert.Contains(text, item.Expected, status.ToString() & ": " & text)
            StringAssert.EndsWith(text, "Nothing was changed.")
        Next

        UseNetwork(Function(sent) Answer(HttpStatusCode.OK, ClaudeAnswer("", "refusal")))
        OnlineAccess.Configure(ClaudeSettings())
        Dim declined As AssistantException = Assert.ThrowsExactly(Of AssistantException)(Sub() provider.CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult())
        StringAssert.StartsWith(declined.Message, "Claude declined this request.")

        UseNetwork(Function(sent) Answer(HttpStatusCode.OK, ClaudeAnswer("A partial", "max_tokens")))
        OnlineAccess.Configure(ClaudeSettings())
        Assert.IsTrue(provider.CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult().Truncated)
    End Sub


    ' ---------------------------------------------------------------
    ' A compatible server
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ACompatibleServerGetsItsKeyOnlyAtTheAddressItWasAddedFor()
        Dim network As CapturingNetwork = UseNetwork(Function(request) Answer(HttpStatusCode.OK, CompatibleAnswer(LetterAnswer)))
        Dim settings As OnlineServicesSettings = CompatibleSettings("http://localhost:11434/v1")
        settings.Assistant.EndpointKeyOrigin = "http://localhost:11434"
        OnlineAccess.Configure(settings)
        _keys.Save(ProtectedKeyStore.AssistantEndpoint, "local-key-123")
        _keys.Save(ProtectedKeyStore.Anthropic, "sk-ant-test-key-0001")

        Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
        Assert.IsInstanceOfType(Of CompatibleAssistantProvider)(provider)
        Assert.IsFalse(OnlineAccess.NeedsConfirmation(AssistantService.DecisionLetterFeature), "A model on this computer: nothing leaves it.")
        Dim reply As AssistantReply = provider.CompleteAsync(AssistantService.BuildLetterRequest(Letter), CancellationToken.None).GetAwaiter().GetResult()
        Dim sent As SentRequest = network.Requests.Single()
        Assert.AreEqual("http://localhost:11434/v1/chat/completions", sent.Uri.AbsoluteUri)
        Assert.AreEqual("Bearer local-key-123", sent.Header("Authorization"))
        Assert.IsNull(sent.Header("x-api-key"), "The Claude key never goes anywhere else.")
        Using body As JsonDocument = JsonDocument.Parse(sent.Body)
            Assert.AreEqual("llama3.1", body.RootElement.GetProperty("model").GetString())
            Assert.AreEqual("system", body.RootElement.GetProperty("messages")(0).GetProperty("role").GetString())
            Assert.AreEqual(Letter.Trim(), body.RootElement.GetProperty("messages")(1).GetProperty("content").GetString())
            Assert.AreEqual("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString())
        End Using
        Assert.AreEqual(EditorialDecision.MajorRevision, AssistantService.ReadLetterReply(reply, Letter, Today).Decision)

        ' The address changed and the key wasn't added again: no key.
        Dim moved As OnlineServicesSettings = CompatibleSettings("https://models.example.org/v1")
        moved.Assistant.EndpointKeyOrigin = "http://localhost:11434"
        OnlineAccess.Configure(moved)
        network.Requests.Clear()
        AssistantService.CurrentProvider().CompleteAsync(AssistantService.BuildResponseRequest("Reviewer 1", "Clarify.", ""), CancellationToken.None).GetAwaiter().GetResult()
        Assert.IsNull(network.Requests.Single().Header("Authorization"))
        Assert.IsTrue(OnlineAccess.NeedsConfirmation(AssistantService.DraftResponseFeature), "A server elsewhere: ask first.")
    End Sub

    <TestMethod>
    Public Sub AServerWithoutStructuredAnswersIsAskedOnceMorePlainly()
        Dim network As CapturingNetwork = UseNetwork(
            Function(request)
                If request.Body.Contains("response_format") Then Return Answer(HttpStatusCode.BadRequest, "{""error"":""response_format not supported""}")
                Return Answer(HttpStatusCode.OK, CompatibleAnswer("```json" & vbLf & LetterAnswer & vbLf & "```"))
            End Function)
        OnlineAccess.Configure(CompatibleSettings("http://127.0.0.1:1234/v1"))
        Dim reply As AssistantReply = AssistantService.CurrentProvider().CompleteAsync(AssistantService.BuildLetterRequest(Letter), CancellationToken.None).GetAwaiter().GetResult()
        Assert.AreEqual(2, network.Requests.Count)
        Assert.AreEqual(4, AssistantService.ReadLetterReply(reply, Letter, Today).Comments.Count, "A fenced answer is still read.")
    End Sub


    ' ---------------------------------------------------------------
    ' Reading a decision letter
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ALetterIsReadAndEveryCommentCheckedAgainstIt()
        Dim proposal As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = LetterAnswer, .ProviderName = "Claude", .Model = "claude-opus-5-5"}, Letter, Today)

        Assert.AreEqual(EditorialDecision.MajorRevision, proposal.Decision)
        Assert.AreEqual("Based on their comments, I would like to invite a major revision.", proposal.DecisionQuote)
        Assert.AreEqual(New DateTime(2026, 9, 15), proposal.DecisionDate.Value)
        Assert.AreEqual(New DateTime(2026, 11, 14), proposal.RevisionDeadline.Value, "60 days from the letter's date, worked out here.")
        StringAssert.StartsWith(proposal.DeadlineBasis, "60 days after Sep 15, 2026")
        Assert.AreEqual("Please submit your revised manuscript within 60 days.", proposal.DeadlineQuote)

        Assert.AreEqual(4, proposal.Comments.Count, "The repeated comment is offered once.")
        Dim first As LetterCommentCandidate = proposal.Comments(0)
        Assert.IsTrue(first.Found)
        Assert.AreEqual("The sample size justification is unclear;" & vbCrLf & "please report the power analysis.", first.Text, "The letter's own text, line break and all.")
        Assert.AreEqual(first.Text, Letter.Substring(first.SourceStart, first.SourceLength))
        Assert.IsTrue(proposal.Comments(2).Found, "A plain apostrophe matches the letter's curly one.")
        StringAssert.Contains(proposal.Comments(2).Text, ChrW(&H2019))
        Assert.IsFalse(proposal.Comments(3).Found, "A comment not in the letter is marked.")
        Assert.AreEqual("Reviewer 2", proposal.Comments(3).ReviewerLabel)
    End Sub

    <TestMethod>
    Public Sub UnclearOrOddAnswersProposeNothing()
        Dim unclear As String = "Preamble: {""decision"":""unclear"",""decision_quote"":""Not in the letter."",""decision_date"":""1999-01-01"",""deadline_date"":""2024-01-01""," &
                                """deadline_days"":0,""deadline_quote"":"""",""comments"":[{""reviewer"":"""",""text"":""""},{""reviewer"":""  "",""text"":""Report the preregistered exclusion criteria in the main text.""}]} Thanks!"
        Dim proposal As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = unclear}, Letter, Today)
        Assert.IsFalse(proposal.Decision.HasValue)
        Assert.AreEqual(String.Empty, proposal.DecisionQuote, "A quote not in the letter is dropped.")
        Assert.IsFalse(proposal.DecisionDate.HasValue, "A date far from today is dropped.")
        Assert.IsFalse(proposal.RevisionDeadline.HasValue, "A deadline before the letter is dropped.")
        Assert.AreEqual(1, proposal.Comments.Count)
        Assert.AreEqual("Reviewer", proposal.Comments(0).ReviewerLabel)

        Dim noDate As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = "{""decision"":""desk_rejected"",""deadline_days"":14,""comments"":[]}"}, Letter, Today)
        Assert.AreEqual(EditorialDecision.DeskRejected, noDate.Decision)
        Assert.AreEqual(Today.AddDays(14), noDate.RevisionDeadline.Value, "Without the letter's date, from today.")
        StringAssert.EndsWith(noDate.DeadlineBasis, "(today)")

        Assert.ThrowsExactly(Of AssistantException)(Sub() AssistantService.ReadLetterReply(New AssistantReply With {.Text = "I can't help with that."}, Letter, Today))
        Assert.ThrowsExactly(Of AssistantException)(Sub() AssistantService.ReadLetterReply(New AssistantReply With {.Text = "{not json}"}, Letter, Today))
        Assert.IsFalse(AssistantService.LocateExcerpt(Letter, "ab").HasValue, "Too short to place.")
    End Sub

    <TestMethod>
    Public Sub DraftsSendOnlyTheCommentOrTheManuscriptDetails()
        Dim response As AssistantRequest = AssistantService.BuildResponseRequest("Reviewer 2", "Compare the effect with published classroom studies.", "")
        Assert.AreEqual(AssistantService.DraftResponseFeature, response.Feature)
        Assert.AreEqual("Reviewer: Reviewer 2" & vbLf & vbLf & "Comment:" & vbLf & "Compare the effect with published classroom studies." & vbLf & vbLf & "Planned action (from the researcher):" & vbLf & "(none yet)", response.Content)
        Assert.IsNull(response.Schema)

        Dim cover As AssistantRequest = AssistantService.BuildCoverLetterRequest("Example: open materials", "Fictional Open Psychology", "Journal article", {"open science", " "}, "", "Open access; double-anonymous review")
        StringAssert.Contains(cover.Content, "Journal: Fictional Open Psychology")
        StringAssert.Contains(cover.Content, "Keywords: open science" & vbLf)
        StringAssert.EndsWith(cover.Content, "Abstract:" & vbLf & "(none given)")
        StringAssert.Contains(AssistantService.WhatIsSent(cover), cover.Content)
        StringAssert.Contains(AssistantService.WhatIsSent(cover), cover.Instructions)

        Assert.AreEqual("Dear reviewer", AssistantService.TextOf(New AssistantReply With {.Text = "```" & vbLf & "Dear reviewer" & vbLf & "```"}))
        Assert.ThrowsExactly(Of AssistantException)(Sub() AssistantService.TextOf(New AssistantReply With {.Text = "  "}))
    End Sub


    ' ---------------------------------------------------------------
    ' Asking first, and remembering where things came from
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheResearcherIsAskedOncePerFeatureAndService()
        OnlineAccess.Configure(ClaudeSettings())
        Dim saved As New List(Of String)()
        OnlineAccess.AssistantUseConfirmed = Sub(use) saved.Add(use)
        Assert.IsTrue(OnlineAccess.NeedsConfirmation(AssistantService.DecisionLetterFeature))

        Dim asked As Integer = 0
        AssistantRunner.ConsentPrompt = Function(owner, asking, connection)
                                            asked += 1
                                            Assert.AreEqual("Claude at api.anthropic.com", connection.Recipient)
                                            Return If(asked = 1, CType(Nothing, Boolean?), True)
                                        End Function
        Dim request As AssistantRequest = AssistantService.BuildLetterRequest(Letter)
        Assert.IsFalse(AssistantRunner.Confirm(Nothing, request), "Cancel sends nothing.")
        Assert.IsTrue(AssistantRunner.Confirm(Nothing, request))
        CollectionAssert.AreEqual({"decision-letter|https://api.anthropic.com:443"}, saved, "Saved, through Form1's settings.")
        Assert.IsFalse(OnlineAccess.NeedsConfirmation(AssistantService.DecisionLetterFeature))
        Assert.IsTrue(AssistantRunner.Confirm(Nothing, request))
        Assert.AreEqual(2, asked, "Not asked again.")
        Assert.IsTrue(OnlineAccess.NeedsConfirmation(AssistantService.CoverLetterFeature), "Each feature is asked about on its own.")

        ' Confirmations saved in settings are honoured on the next start.
        Dim restarted As OnlineServicesSettings = ClaudeSettings()
        restarted.Assistant.ConfirmedUses.Add("cover-letter|https://api.anthropic.com:443")
        OnlineAccess.Configure(restarted)
        Assert.IsFalse(OnlineAccess.NeedsConfirmation(AssistantService.CoverLetterFeature))
    End Sub

    <TestMethod>
    Public Sub AcceptedSuggestionsKeepWhereTheyCameFrom()
        Dim suggestion As AssistantSuggestion = AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, New AssistantReply With {.ProviderName = "Claude", .Model = "claude-opus-5-5"}, New String("x"c, 5000), New DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc))
        Assert.AreEqual(AssistantSuggestionService.MaximumSourceLength, suggestion.SourceText.Length)
        StringAssert.StartsWith(AssistantSuggestionService.Describe(suggestion), "Began as an AI suggestion (Claude, claude-opus-5-5, Oct 1, 2026)")

        Dim submission As New JournalSubmission With {.JournalName = "Fictional Open Psychology"}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .Suggestion = suggestion}
        submission.Decisions.Add(decision)
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify.",
                                                                        .CommentSuggestion = suggestion, .ResponseSuggestion = New AssistantSuggestion With {.Feature = "draft-response"}})
        Dim clone As JournalSubmission = ManuscriptCloneService.CloneSubmission(submission)
        Assert.AreNotSame(suggestion, clone.Decisions(0).Suggestion)
        Assert.AreEqual(JsonSerializer.Serialize(submission), JsonSerializer.Serialize(clone), "Every field is copied.")

        Dim manuscript As New Manuscript With {.Title = "A manuscript"}
        manuscript.Submissions.Add(clone)
        clone.ReviewerResponses(0).ResponseSuggestion = New AssistantSuggestion()
        AssistantSuggestionService.NormalizeManuscript(manuscript)
        Assert.IsNull(clone.ReviewerResponses(0).ResponseSuggestion, "An empty record is dropped.")
        Assert.IsNotNull(clone.ReviewerResponses(0).CommentSuggestion)

        Dim repository As New ManuscriptRepository(Path.Combine(_directory, "data"), Path.Combine(_directory, "managed"))
        repository.Save(New List(Of Manuscript) From {manuscript})
        Dim loaded As Manuscript = repository.Load().Single()
        Assert.AreEqual("claude-opus-5-5", loaded.Submissions(0).Decisions(0).Suggestion.Model)
        Assert.AreEqual("decision-letter", loaded.Submissions(0).ReviewerResponses(0).CommentSuggestion.Feature)
    End Sub

    <TestMethod>
    Public Sub TheGuideAndCatalogDescribeBothAssistantServices()
        Dim claude As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantClaude)
        Dim compatible As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantCompatible)
        Assert.IsTrue(claude.OffUntilTurnedOn AndAlso compatible.OffUntilTurnedOn)
        CollectionAssert.AreEqual({"api.anthropic.com"}, claude.Hosts.ToList())
        Assert.IsTrue(compatible.ConfiguredHost)
        Assert.AreEqual(0, compatible.Hosts.Count)
        StringAssert.Contains(compatible.Contacts, "http only on this computer")
        Dim source As String = File.ReadAllText(Path.Combine(RepositoryRoot(), "ManuscriptPipeline", "Services", "AssistantProviders.vb"))
        StringAssert.Contains(source, ".HttpClient = http,", "The SDK always gets the gate's client.")
        StringAssert.Contains(source, "OnlineAccess.CreateClient(OnlineServiceCatalog.AssistantClaude", "...from the gate.")
        StringAssert.Contains(source, ".MaxRetries = 0", "...and never resends the text by itself.")
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function RepositoryRoot() As String
        Dim folder As New DirectoryInfo(AppContext.BaseDirectory)
        While folder IsNot Nothing AndAlso Not File.Exists(Path.Combine(folder.FullName, "ManuscriptPipeline.slnx"))
            folder = folder.Parent
        End While
        Assert.IsNotNull(folder, "The repository root was not found.")
        Return folder.FullName
    End Function

    Friend Shared Function ClaudeSettings() As OnlineServicesSettings
        Return New OnlineServicesSettings With {.Assistant = New AssistantSettings With {.Enabled = True, .Provider = AssistantProvider.Claude, .ClaudeModel = "claude-opus-5-5"}}
    End Function

    Friend Shared Function CompatibleSettings(endpoint As String) As OnlineServicesSettings
        Return New OnlineServicesSettings With {.Assistant = New AssistantSettings With {.Enabled = True, .Provider = AssistantProvider.Compatible, .Endpoint = endpoint, .EndpointModel = "llama3.1"}}
    End Function

    Friend Shared Function ClaudeAnswer(text As String, Optional stopReason As String = "end_turn") As String
        Return "{""id"":""msg_test"",""type"":""message"",""role"":""assistant"",""model"":""claude-opus-5-5"",""content"":[{""type"":""text"",""text"":" &
            JsonSerializer.Serialize(text) & "}],""stop_reason"":""" & stopReason & """,""stop_sequence"":null,""usage"":{""input_tokens"":10,""output_tokens"":5}}"
    End Function

    Friend Shared Function CompatibleAnswer(text As String) As String
        Return "{""id"":""chatcmpl-1"",""object"":""chat.completion"",""model"":""llama3.1"",""choices"":[{""index"":0,""message"":{""role"":""assistant"",""content"":" &
            JsonSerializer.Serialize(text) & "},""finish_reason"":""stop""}]}"
    End Function

    Private Shared Function Answer(status As HttpStatusCode, body As String) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function

    Private Function UseNetwork(respond As Func(Of SentRequest, HttpResponseMessage)) As CapturingNetwork
        OnlineAccess.ResetForTests()
        OnlineAccess.KeyStoreFactory = Function() _keys
        Dim network As New CapturingNetwork With {.Respond = respond}
        OnlineAccess.InnerHandlerFactory = Function() network
        Return network
    End Function

    Friend NotInheritable Class SentRequest
        Public Property Uri As Uri
        Public Property Body As String = String.Empty
        Public Property Headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Public ReadOnly Property HeaderNames As IEnumerable(Of String)
            Get
                Return Headers.Keys
            End Get
        End Property

        Public Function Header(name As String) As String
            Dim value As String = Nothing
            Return If(Headers.TryGetValue(name, value), value, Nothing)
        End Function
    End Class

    ' Under the gate: records every header and the body exactly as sent.
    Friend NotInheritable Class CapturingNetwork
        Inherits HttpMessageHandler

        Public ReadOnly Requests As New List(Of SentRequest)()
        Public Property Respond As Func(Of SentRequest, HttpResponseMessage)

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim sent As New SentRequest With {.Uri = request.RequestUri}
            For Each header In request.Headers
                sent.Headers(header.Key) = String.Join(",", header.Value)
            Next
            If request.Content IsNot Nothing Then sent.Body = Await request.Content.ReadAsStringAsync(cancellationToken)
            SyncLock Requests
                Requests.Add(sent)
            End SyncLock
            Dim response As HttpResponseMessage = Respond(sent)
            response.RequestMessage = request
            Return response
        End Function
    End Class

End Class
