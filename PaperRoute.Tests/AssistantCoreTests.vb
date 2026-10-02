Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Sockets
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
        ' A wait is named only when the service gave one, and a refused
        ' request shows the service's own reason, such as billing.
        For Each item In {(Status:=429, Expected:="Claude is limiting requests right now. It said: Your credit balance is too low."), (Status:=401, Expected:="didn't accept the key"),
                          (Status:=400, Expected:="Claude couldn't accept the request (HTTP 400). It said: Your credit balance is too low."),
                          (Status:=529, Expected:="overloaded"), (Status:=500, Expected:="problem on its side")}
            Dim status As Integer = item.Status
            UseNetwork(Function(sent) Answer(CType(status, HttpStatusCode), "{""type"":""error"",""error"":{""type"":""x"",""message"":""Your credit   balance is too low""}}"))
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
        _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434", "local-key-123")
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

        ' Settings that name the new address, as after a key that couldn't
        ' be written: the stored key still belongs to the old one.
        moved.Assistant.EndpointKeyOrigin = "https://models.example.org:443"
        OnlineAccess.Configure(moved)
        network.Requests.Clear()
        AssistantService.CurrentProvider().CompleteAsync(AssistantService.BuildResponseRequest("Reviewer 1", "Clarify.", ""), CancellationToken.None).GetAwaiter().GetResult()
        Assert.IsNull(network.Requests.Single().Header("Authorization"), "The key is stored with its address, so it goes nowhere else.")
        Assert.AreEqual("http://localhost:11434", _keys.OriginOf(ProtectedKeyStore.AssistantEndpoint))
        Assert.IsNull(_keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://models.example.org:443"))
        Assert.ThrowsExactly(Of ArgumentException)(Sub() _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, " ", "local-key-123"))
        Assert.ThrowsExactly(Of ArgumentException)(Sub() _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "https://a.example:443" & vbLf & "https://b.example:443", "local-key-123"))

        ' An address in another script is kept as written, and is its own address.
        Dim accented As String = "https://mod" & ChrW(&HE8) & "le.example:443"
        _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, accented, "local-key-123")
        Assert.AreEqual("local-key-123", _keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, accented))
        Assert.IsNull(_keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://modele.example:443"))

        ' A key saved without an address is given to none.
        _keys.Save(ProtectedKeyStore.AssistantEndpoint, "local-key-123")
        Assert.IsNull(_keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434"))
    End Sub

    <TestMethod>
    Public Sub AModelOnThisComputerIsNeverReachedThroughAProxy()
        Dim listener As New TcpListener(IPAddress.Loopback, 0)
        listener.Start()
        Dim port As Integer = DirectCast(listener.LocalEndpoint, IPEndPoint).Port
        Dim served As Task(Of String) = Task.Run(Function() ServeOnceAsync(listener, CompatibleAnswer("Hello")))

        Dim proxy As New RecordingProxy()
        Dim previous As IWebProxy = HttpClient.DefaultProxy
        OnlineAccess.ResetForTests()
        OnlineAccess.KeyStoreFactory = Function() _keys
        HttpClient.DefaultProxy = proxy
        Try
            OnlineAccess.Configure(CompatibleSettings("http://127.0.0.1:" & port.ToString(Globalization.CultureInfo.InvariantCulture) & "/v1"))
            Using limit As New CancellationTokenSource(TimeSpan.FromSeconds(60))
                Dim reply As AssistantReply = AssistantService.CurrentProvider().CompleteAsync(AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", ""), limit.Token).GetAwaiter().GetResult()
                Assert.AreEqual("Hello", reply.Text)
            End Using
            StringAssert.StartsWith(served.GetAwaiter().GetResult(), "POST /v1/chat/completions ", "Sent straight to this computer.")
            Assert.AreEqual(0, proxy.Asked, "No proxy is consulted for a request to this computer.")
        Finally
            HttpClient.DefaultProxy = previous
            OnlineAccess.ResetForTests()
            listener.Stop()
        End Try
    End Sub

    <TestMethod>
    Public Sub AServerThatNamesItsLimitDifferentlyIsAskedOnceMoreAndARefusalGivesItsReason()
        Dim network As CapturingNetwork = UseNetwork(
            Function(sent)
                If sent.Body.Contains("""max_tokens""") Then
                    Return Answer(HttpStatusCode.BadRequest, "{""error"":{""message"":""Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.""}}")
                End If
                Return Answer(HttpStatusCode.OK, CompatibleAnswer("Hello"))
            End Function)
        OnlineAccess.Configure(CompatibleSettings("https://models.example.org/v1"))
        Dim request As AssistantRequest = AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", "")
        Assert.AreEqual("Hello", AssistantService.CurrentProvider().CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult().Text)
        Assert.AreEqual(2, network.Requests.Count)
        StringAssert.Contains(network.Requests(1).Body, """max_completion_tokens"":4000")
        Assert.IsFalse(network.Requests(1).Body.Contains("""max_tokens"""))

        For Each item In {(Status:=400, Body:="{""error"":{""message"":""This model's maximum context length is 8192 tokens.""}}",
                           Expected:="The server couldn't accept the request (HTTP 400). It said: This model's maximum context length is 8192 tokens. Nothing was changed."),
                          (Status:=400, Body:="{""error"":""model requires more system memory""}",
                           Expected:="The server couldn't accept the request (HTTP 400). It said: model requires more system memory. Nothing was changed."),
                          (Status:=400, Body:="not json",
                           Expected:="The server couldn't accept the request (HTTP 400). Check the model in Settings > Preferences > AI assistant, or send less text. Nothing was changed."),
                          (Status:=429, Body:="{}",
                           Expected:="The server is limiting requests right now. Try again in a few minutes. Nothing was changed."),
                          (Status:=429, Body:="{""error"":{""message"":""You exceeded your current quota, please check your plan and billing details."",""type"":""insufficient_quota""}}",
                           Expected:="The server is limiting requests right now. It said: You exceeded your current quota, please check your plan and billing details. Nothing was changed."),
                          (Status:=400, Body:="{""error"":{""message"":""first&#10;&#10;second\u0000third‮""}}",
                           Expected:="The server couldn't accept the request (HTTP 400). It said: first second third. Nothing was changed.")}
            Dim scenario = item
            UseNetwork(Function(sent) Answer(CType(scenario.Status, HttpStatusCode), scenario.Body))
            OnlineAccess.Configure(CompatibleSettings("https://models.example.org/v1"))
            Dim failure As Exception = Nothing
            Try
                AssistantService.CurrentProvider().CompleteAsync(request, CancellationToken.None).GetAwaiter().GetResult()
            Catch ex As Exception
                failure = ex
            End Try
            Assert.IsNotNull(failure, scenario.Body)
            Assert.AreEqual(scenario.Expected, AssistantRunner.Describe(failure))
        Next

        Assert.AreEqual(243, AssistantErrorText.FromBody("{""error"":{""message"":""" & New String("x"c, 500) & """}}").Length, "A long reason is cut to a line.")
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
        Assert.AreEqual("60 days after Sep 15, 2026 (the letter's date)", proposal.DeadlineBasis)
        Assert.AreEqual(60, proposal.DeadlineDays.Value, "Kept, so the deadline can follow a corrected decision date.")
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
        Assert.IsFalse(proposal.DecisionDate.HasValue, "A date far from today is not used.")
        Assert.AreEqual(New DateTime(1999, 1, 1), proposal.LetterDateNotUsed.Value, "It is reported, to be checked.")
        Assert.IsFalse(proposal.RevisionDeadline.HasValue, "A deadline before the letter is dropped.")

        ' A period is never counted from today when the letter's date is in doubt.
        Dim oldLetter As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = "{""decision"":""major_revision"",""decision_date"":""2022-03-03"",""deadline_days"":60,""comments"":[]}"}, Letter, Today)
        Assert.AreEqual(New DateTime(2022, 3, 3), oldLetter.LetterDateNotUsed.Value)
        Assert.IsFalse(oldLetter.RevisionDeadline.HasValue OrElse oldLetter.DeadlineDays.HasValue)
        Assert.IsTrue(oldLetter.DeadlineNotUsed, "The letter gave a deadline, so the researcher is asked to set it.")
        Assert.IsFalse(AssistantService.ReadLetterReply(New AssistantReply With {.Text = "{""decision"":""rejected"",""decision_date"":""2022-03-03"",""deadline_date"":"""",""deadline_days"":0,""comments"":[]}"}, Letter, Today).DeadlineNotUsed,
                       "A letter with no deadline doesn't ask for one.")
        Assert.AreEqual(1, proposal.Comments.Count)
        Assert.AreEqual("Reviewer", proposal.Comments(0).ReviewerLabel)

        Dim noDate As DecisionLetterProposal = AssistantService.ReadLetterReply(New AssistantReply With {.Text = "{""decision"":""desk_rejected"",""deadline_days"":14,""comments"":[]}"}, Letter, Today)
        Assert.AreEqual(EditorialDecision.DeskRejected, noDate.Decision)
        Assert.AreEqual(Today.AddDays(14), noDate.RevisionDeadline.Value, "Without the letter's date, from today.")
        StringAssert.EndsWith(noDate.DeadlineBasis, "(today)")
        Assert.AreEqual(14, noDate.DeadlineDays.Value)

        ' An answer that stopped at its length limit says so.
        Dim cut As AssistantException = Assert.ThrowsExactly(Of AssistantException)(
            Sub() AssistantService.ReadLetterReply(New AssistantReply With {.Text = LetterAnswer.Substring(0, LetterAnswer.Length - 40), .Truncated = True}, Letter, Today))
        Assert.AreEqual("The answer was cut short before it finished. Paste the letter in parts, such as one reviewer at a time. Nothing was changed.", cut.Message)
        Assert.IsTrue(AssistantService.ReadLetterReply(New AssistantReply With {.Text = LetterAnswer, .Truncated = True}, Letter, Today).Truncated, "A complete answer that was cut short is still read.")

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
        Dim suggested As New DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        Dim suggestion As AssistantSuggestion = AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, New AssistantReply With {.ProviderName = "Claude", .Model = "claude-opus-5-5"}, New String("x"c, 5000), suggested)
        Assert.AreEqual(AssistantSuggestionService.MaximumSourceLength, suggestion.SourceText.Length)
        ' The day as it is here: noon UTC is the next day in some zones.
        StringAssert.StartsWith(AssistantSuggestionService.Describe(suggestion), "Began as an AI suggestion (Claude, claude-opus-5-5, " & suggested.ToLocalTime().ToString("MMM d, yyyy", Globalization.CultureInfo.CurrentCulture) & ")")

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

    Friend Shared Function RepositoryRoot() As String
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

    ' One request answered on this computer; gives back the request's first line.
    Private Shared Async Function ServeOnceAsync(listener As TcpListener, body As String) As Task(Of String)
        Using client As TcpClient = Await listener.AcceptTcpClientAsync()
            Using stream As NetworkStream = client.GetStream()
                Dim received As New MemoryStream()
                Dim buffer(8191) As Byte
                Dim text As String = String.Empty
                Do
                    Dim count As Integer = Await stream.ReadAsync(buffer, 0, buffer.Length)
                    If count = 0 Then Exit Do
                    received.Write(buffer, 0, count)
                    text = Encoding.UTF8.GetString(received.ToArray())
                    Dim headersEnd As Integer = text.IndexOf(vbCrLf & vbCrLf, StringComparison.Ordinal)
                    If headersEnd < 0 Then Continue Do
                    Dim lengthLine As String = text.Substring(0, headersEnd).Split({vbCrLf}, StringSplitOptions.None).
                        FirstOrDefault(Function(line) line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    Dim expected As Integer = If(lengthLine Is Nothing, 0, Integer.Parse(lengthLine.Substring("Content-Length:".Length).Trim(), Globalization.CultureInfo.InvariantCulture))
                    If received.Length >= Encoding.UTF8.GetByteCount(text.Substring(0, headersEnd + 4)) + expected Then Exit Do
                Loop
                Dim payload As Byte() = Encoding.UTF8.GetBytes(body)
                Dim head As Byte() = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK" & vbCrLf & "Content-Type: application/json" & vbCrLf &
                    "Content-Length: " & payload.Length.ToString(Globalization.CultureInfo.InvariantCulture) & vbCrLf & "Connection: close" & vbCrLf & vbCrLf)
                Await stream.WriteAsync(head, 0, head.Length)
                Await stream.WriteAsync(payload, 0, payload.Length)
                Await stream.FlushAsync()
                Return text.Split({vbCrLf}, StringSplitOptions.None)(0)
            End Using
        End Using
    End Function

    ' A proxy that counts every time it is consulted.
    Private NotInheritable Class RecordingProxy
        Implements IWebProxy

        Public Asked As Integer

        Public Property Credentials As ICredentials Implements IWebProxy.Credentials

        Public Function GetProxy(destination As Uri) As Uri Implements IWebProxy.GetProxy
            Interlocked.Increment(Asked)
            Return New Uri("http://127.0.0.1:9")
        End Function

        Public Function IsBypassed(host As Uri) As Boolean Implements IWebProxy.IsBypassed
            Interlocked.Increment(Asked)
            Return False
        End Function
    End Class

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
