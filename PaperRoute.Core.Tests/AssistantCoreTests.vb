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
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports PaperRoute.Tests.AssistantTestSupport

' The optional AI assistant's core (#84): off until turned on, every
' request through the gate with only what it needs, keys only to their own
' service, and answers checked against what was sent. No test reaches the
' network or a model. Stored keys are kept by the test platform's key
' protector (CorePlatformSetup); what the key store itself does with them
' (a key kept with its address, refused for any other) is the same on every
' system. The tests that go through the Windows app's AssistantRunner, or
' read the source through the repository root, are AssistantCoreSourceTests
' in PaperRoute.Tests (core stage 2, batch 3).
<TestClass>
<DoNotParallelize>
Public Class AssistantCoreTests

    Private Shared ReadOnly Today As New DateTime(2026, 10, 1)
    Private _directory As String
    Private _keys As ProtectedKeyStore

    ' The letter, the answers, the settings and the capturing network are
    ' in AssistantTestSupport, which the other assistant tests share.

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        _directory = CoreTestSupport.CreateTemporaryRoot()
        _keys = New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() _keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        CoreTestSupport.DeleteTemporaryRoot(_directory)
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
        Dim previousTraceContent As String = Environment.GetEnvironmentVariable("ANTHROPIC_OPEN_TELEMETRY_TRACES_CONTENT_MODE")
        Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", "token-from-the-environment")
        Environment.SetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS", "X-From-Environment: yes")
        Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", "https://elsewhere.example.com")
        Environment.SetEnvironmentVariable("ANTHROPIC_OPEN_TELEMETRY_TRACES_CONTENT_MODE", "content")
        ' A listener on the SDK's spans, as a tracer in the process would add
        ' (#298): the SDK is told to make none, whatever the variables say.
        Dim spans As New List(Of String)()
        Dim reply As AssistantReply
        Using listener As New System.Diagnostics.ActivityListener With {
            .ShouldListenTo = Function(source) source.Name.StartsWith("com.anthropic", StringComparison.Ordinal),
            .Sample = Function(ByRef options As System.Diagnostics.ActivityCreationOptions(Of System.Diagnostics.ActivityContext)) System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            .ActivityStarted = Sub(activity)
                                   SyncLock spans
                                       spans.Add(activity.Source.Name & " " & activity.OperationName)
                                   End SyncLock
                               End Sub
        }
            System.Diagnostics.ActivitySource.AddActivityListener(listener)
            Try
                Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
                Assert.IsInstanceOfType(Of ClaudeAssistantProvider)(provider)
                reply = provider.CompleteAsync(AssistantService.BuildLetterRequest(Letter), CancellationToken.None).GetAwaiter().GetResult()
            Finally
                Environment.SetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN", previousToken)
                Environment.SetEnvironmentVariable("ANTHROPIC_CUSTOM_HEADERS", previousHeaders)
                Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", previousBase)
                Environment.SetEnvironmentVariable("ANTHROPIC_OPEN_TELEMETRY_TRACES_CONTENT_MODE", previousTraceContent)
            End Try
        End Using
        Assert.AreEqual(0, spans.Count, "No span holds the letter or adds trace headers: " & String.Join(", ", spans))

        ' Since SDK 13 (#298): the beta header alone marks the beta route, and
        ' the API version is 2026-10-21.
        Dim sent As SentRequest = network.Requests.Single()
        Assert.AreEqual("https://api.anthropic.com/v1/messages", sent.Uri.AbsoluteUri)
        Assert.AreEqual("sk-ant-test-key-0001", sent.Header("x-api-key"), "The gate adds the key; the SDK only holds a placeholder.")
        Assert.AreEqual("2026-10-21", sent.Header("anthropic-version"))
        Assert.AreEqual("server-side-fallback-2026-07-01", sent.Header("anthropic-beta"))
        StringAssert.StartsWith(sent.Header("User-Agent"), "PaperRoute-Tracker/")
        Assert.IsNull(sent.Header("Authorization"), "Neither the SDK's placeholder, which it sends as a bearer token, nor a token from the environment.")
        Assert.IsFalse(sent.HeaderNames.Any(Function(name) name.StartsWith("X-Stainless", StringComparison.OrdinalIgnoreCase) OrElse name.Equals("X-From-Environment", StringComparison.OrdinalIgnoreCase)),
                       "No system details or extra headers: " & String.Join(", ", sent.HeaderNames))

        Using body As JsonDocument = JsonDocument.Parse(sent.Body)
            Dim root As JsonElement = body.RootElement
            Assert.AreEqual("claude-opus-5-5", root.GetProperty("model").GetString())
            Assert.AreEqual(AssistantService.LetterInstructions, root.GetProperty("system").GetString())
            Assert.AreEqual(Letter.Trim(), root.GetProperty("messages")(0).GetProperty("content").GetString(), "The letter as pasted, and nothing else.")
            Assert.AreEqual("user", root.GetProperty("messages")(0).GetProperty("role").GetString(), "The SDK's user message type sends the role it no longer takes (#298).")
            Assert.AreEqual(1, root.GetProperty("messages").GetArrayLength())
            Assert.AreEqual("medium", root.GetProperty("output_config").GetProperty("effort").GetString())
            Assert.AreEqual("json_schema", root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString())
            Assert.AreEqual("default", root.GetProperty("fallbacks").GetString())
            ' The gate drops headers it does not know, but not body fields: an
            ' SDK update that adds a default one fails here (#298).
            CollectionAssert.AreEquivalent({"model", "max_tokens", "system", "messages", "output_config", "fallbacks"}, root.EnumerateObject().Select(Function(field) field.Name).ToList(), "Nothing else is sent.")
            CollectionAssert.AreEquivalent({"role", "content"}, root.GetProperty("messages")(0).EnumerateObject().Select(Function(field) field.Name).ToList(), "The one message holds only its role and the letter.")
        End Using

        Assert.AreEqual("Claude", reply.ProviderName)
        Dim proposal As DecisionLetterProposal = AssistantService.ReadLetterReply(reply, Letter, Today)
        Assert.AreEqual(EditorialDecision.MajorRevision, proposal.Decision)
        Assert.AreEqual(4, proposal.Comments.Count)
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
    ' Test Connection in Preferences (#96)
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TestConnectionListsClaudeModelsWithOnlyTheKey()
        Dim network As CapturingNetwork = UseNetwork(
            Function(request)
                If request.Uri.AbsolutePath = "/v1/models" Then Return Answer(HttpStatusCode.OK, ClaudeModelList("claude-opus-5-5", "claude-haiku-4-5-20251001"))
                If request.Uri.AbsolutePath = "/v1/models/claude-haiku-4-5" Then Return Answer(HttpStatusCode.OK, ClaudeModelEntry("claude-haiku-4-5-20251001"))
                Return Answer(HttpStatusCode.NotFound, "{""type"":""error"",""error"":{""type"":""not_found_error"",""message"":""model: claude-nope""}}")
            End Function)
        ' Saved, the assistant is off: the trial is what the window shows.
        OnlineAccess.Configure(New OnlineServicesSettings())
        Dim trial As AssistantTrial = ClaudeTrial("claude-opus-5-5", pendingKey:="sk-ant-test-key-0001")
        Assert.AreEqual("Test Connection sends only your Claude key to api.anthropic.com, to list the models your account can use and check the one you chose.", AssistantConnectionTest.WhatIsSent(trial.Connection, True))

        Dim result As AssistantTestResult = RunTest(trial)
        Assert.IsTrue(result.Succeeded)
        Assert.AreEqual("Connected. Claude accepted your key, and your account can use claude-opus-5-5. The Model list now shows your account's 2 models.", result.Message)
        CollectionAssert.AreEqual({"claude-opus-5-5", "claude-haiku-4-5-20251001"}, result.Models)
        Dim sent As SentRequest = network.Requests.Single()
        Assert.AreEqual(HttpMethod.Get, sent.Method)
        Assert.AreEqual("https://api.anthropic.com/v1/models?limit=1000", sent.Uri.AbsoluteUri)
        Assert.AreEqual(String.Empty, sent.Body, "Only the key is sent.")
        Assert.AreEqual("sk-ant-test-key-0001", sent.Header("x-api-key"), "The key added in the window, as a header, by the gate.")
        Assert.AreEqual("2026-10-21", sent.Header("anthropic-version"))
        Assert.IsNull(sent.Header("Authorization"), "The gate removes the SDK's placeholder bearer token.")
        Assert.IsFalse(sent.HeaderNames.Any(Function(name) name.StartsWith("X-Stainless", StringComparison.OrdinalIgnoreCase)), String.Join(", ", sent.HeaderNames))
        Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic), "Testing stores no key.")
        Assert.IsNull(OnlineAccess.CurrentAssistant(), "...and changes nothing the gate goes by.")
        Assert.AreEqual(OnlineBlockReason.NotTurnedOn, OnlineAccess.BlockReason(OnlineServiceCatalog.AssistantClaude))

        ' The list gives full names: an alias is looked up on its own.
        network.Requests.Clear()
        result = RunTest(ClaudeTrial("claude-haiku-4-5", pendingKey:="sk-ant-test-key-0001"))
        Assert.IsTrue(result.Succeeded, result.Message)
        Assert.AreEqual(2, network.Requests.Count)
        Assert.AreEqual("https://api.anthropic.com/v1/models/claude-haiku-4-5", network.Requests(1).Uri.AbsoluteUri)
        Assert.AreEqual("sk-ant-test-key-0001", network.Requests(1).Header("x-api-key"))

        ' A model the account can't use is a warning, not a failure.
        result = RunTest(ClaudeTrial("claude-nope", pendingKey:="sk-ant-test-key-0001"))
        Assert.IsFalse(result.Succeeded)
        Assert.AreEqual("Claude accepted your key, but your account can't use the model claude-nope. Choose one from the Model list, which now shows your account's 2 models.", result.Message)
        Assert.AreEqual(2, result.Models.Count)

        ' The stored key, when the window keeps it.
        _keys.Save(ProtectedKeyStore.Anthropic, "sk-ant-stored-key-0002")
        network.Requests.Clear()
        Assert.IsTrue(RunTest(ClaudeTrial("claude-opus-5-5", useStoredKey:=True)).Succeeded)
        Assert.AreEqual("sk-ant-stored-key-0002", network.Requests.Single().Header("x-api-key"))

        ' Neither (the window removes the stored key): nothing is sent.
        network.Requests.Clear()
        Dim noKey As AssistantException = Assert.ThrowsExactly(Of AssistantException)(Sub() RunTest(ClaudeTrial("claude-opus-5-5")))
        Assert.AreEqual("Add your Claude key first. Nothing was sent.", noKey.Message)
        Assert.AreEqual(0, network.Requests.Count)

        ' Only the assistant can be tried before Save.
        Assert.ThrowsExactly(Of ArgumentException)(Sub() OnlineAccess.CreateTrialClient(New AssistantTrial With {.Connection = New AssistantConnection With {.ServiceId = OnlineServiceCatalog.Crossref}}, TimeSpan.FromSeconds(1)))
    End Sub

    <TestMethod>
    Public Sub TestConnectionAsksTheServerForItsModelsAtItsAddressOnly()
        Dim network As CapturingNetwork = UseNetwork(Function(request) Answer(HttpStatusCode.OK, ServerModelList("llama3.1:latest", "qwen3:8b")))
        ' Saved: a server elsewhere, with its key. The window names a model on this computer.
        Dim saved As OnlineServicesSettings = CompatibleSettings("https://models.example.org/v1")
        saved.Assistant.EndpointKeyOrigin = "https://models.example.org:443"
        OnlineAccess.Configure(saved)
        _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "https://models.example.org:443", "saved-server-key-1")
        _keys.Save(ProtectedKeyStore.Anthropic, "sk-ant-test-key-0001")
        Dim trial As AssistantTrial = ServerTrial("http://localhost:11434/v1", "llama3.1", useStoredKey:=True)
        Assert.AreEqual("Test Connection asks localhost:11434 for its models and sends nothing else. Nothing leaves this computer.", AssistantConnectionTest.WhatIsSent(trial.Connection, False))
        Assert.AreEqual("Test Connection sends only your server key to models.example.org, to list its models.", AssistantConnectionTest.WhatIsSent(OnlineAccess.CurrentAssistant(), True))

        Dim result As AssistantTestResult = RunTest(trial)
        Assert.IsTrue(result.Succeeded, "llama3.1 is Ollama's llama3.1:latest.")
        Assert.AreEqual("Connected. The server at localhost:11434 has llama3.1.", result.Message)
        Dim sent As SentRequest = network.Requests.Single()
        Assert.AreEqual(HttpMethod.Get, sent.Method)
        Assert.AreEqual("http://localhost:11434/v1/models", sent.Uri.AbsoluteUri)
        Assert.AreEqual(String.Empty, sent.Body)
        Assert.AreEqual("application/json", sent.Header("Accept"))
        Assert.IsNull(sent.Header("Authorization"), "The stored server key was added for another address.")
        Assert.IsNull(sent.Header("x-api-key"), "The Claude key goes only to Anthropic.")

        ' A key stored for this address goes with it while the window keeps it.
        _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434", "local-key-123")
        network.Requests.Clear()
        RunTest(trial)
        Assert.AreEqual("Bearer local-key-123", network.Requests.Single().Header("Authorization"))
        network.Requests.Clear()
        RunTest(ServerTrial("http://localhost:11434/v1", "llama3.1"))
        Assert.IsNull(network.Requests.Single().Header("Authorization"), "Not when the window removes it.")

        ' A key added in the window goes to its address only, and isn't stored.
        network.Requests.Clear()
        RunTest(ServerTrial("http://localhost:11434/v1", "llama3.1", pendingKey:="pending-key-456"))
        Assert.AreEqual("Bearer pending-key-456", network.Requests.Single().Header("Authorization"))
        Assert.AreEqual("local-key-123", _keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434"))

        ' The saved setup is untouched: other requests still go only to the saved address.
        Assert.AreEqual("https://models.example.org/v1", OnlineAccess.CurrentAssistant().Endpoint.AbsoluteUri)
        Dim service As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantCompatible)
        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() OnlineAccess.CheckHost(service, New Uri("http://localhost:11434/v1/chat/completions")))

        ' A redirect elsewhere is stopped before anything, the key included, goes there.
        network = UseNetwork(
            Function(request)
                Dim moved As New HttpResponseMessage(HttpStatusCode.Found)
                moved.Headers.Location = New Uri("https://elsewhere.example.net/v1/models")
                Return moved
            End Function)
        Dim blocked As OnlineServiceBlockedException = Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() RunTest(ServerTrial("https://models.example.org/v1", "llama3.1", pendingKey:="pending-key-456")))
        Assert.AreEqual(OnlineBlockReason.UnexpectedHost, blocked.Reason)
        Assert.AreEqual("https://models.example.org/v1/models", network.Requests.Single().Uri.AbsoluteUri)

        ' Ollama's tag rule, any case, and nothing else.
        Assert.IsTrue(CompatibleAssistantProvider.ListsModel({"Llama3.1"}, "llama3.1"))
        Assert.IsFalse(CompatibleAssistantProvider.ListsModel({"llama3.1:8b"}, "llama3.1"))
        Assert.IsFalse(CompatibleAssistantProvider.ListsModel({"llama3.1:latest"}, "llama3.1:8b"))
        CollectionAssert.AreEqual({"first second", "third"}, CompatibleAssistantProvider.ParseModelList("{""data"":[{""id"":""first\nsecond""},{""id"":""""},{""id"":""third""},{""id"":""third""},{""name"":""no id""}]}"))
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


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function RunTest(trial As AssistantTrial) As AssistantTestResult
        Return AssistantConnectionTest.RunAsync(trial, CancellationToken.None).GetAwaiter().GetResult()
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

End Class
