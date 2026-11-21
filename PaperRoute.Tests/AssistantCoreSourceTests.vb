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
Imports PaperRoute.Tests.AssistantTestSupport

' The parts of the AI assistant's core tests (#84) that need Windows (core
' stage 2, batch 3): the four that go through AssistantRunner, which is in
' the Windows app (its error text, Test Connection's text and the consent
' prompt), and the one that reads the core's source through the repository
' root. Each is as it was in AssistantCoreTests; the rest of AssistantCoreTests
' is in PaperRoute.Core.Tests, and this class has copies of the helpers it
' shares there.
<TestClass>
<DoNotParallelize>
Public Class AssistantCoreSourceTests

    Private _directory As String
    Private _keys As ProtectedKeyStore

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        _directory = CoreTestSupport.CreateTemporaryRoot()
        _keys = New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() _keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        CoreTestSupport.DeleteTemporaryRoot(_directory)
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
    Public Sub TestConnectionProblemsReadPlainly()
        Dim local As AssistantTrial = ServerTrial("http://localhost:11434/v1", "llama3.1")
        Dim elsewhere As AssistantTrial = ServerTrial("https://models.example.org/v1", "llama3.1", pendingKey:="pending-key-456")
        Dim claude As AssistantTrial = ClaudeTrial("claude-opus-5-5", pendingKey:="sk-ant-test-key-0001")

        ' Work offline, as saved, refuses a trial before anything is sent.
        Dim network As CapturingNetwork = UseNetwork(Function(sent) Answer(HttpStatusCode.OK, ServerModelList("llama3.1")))
        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        For Each trial As AssistantTrial In {claude, local}
            Dim failure As Exception = TestFailure(trial)
            Assert.IsInstanceOfType(Of OnlineServiceBlockedException)(failure)
            Assert.AreEqual("Work offline is on until you save, so PaperRoute didn't go online. Save with Work offline turned off, then test again.", AssistantRunner.DescribeTest(failure, trial.Connection))
        Next
        Assert.AreEqual(0, network.Requests.Count, "Nothing was sent.")

        Dim many As String = ServerModelList("a", "b", "c", "d", "e", "f", "g")
        For Each item In {(Trial:=claude, Status:=401, Body:="{""type"":""error"",""error"":{""type"":""authentication_error"",""message"":""invalid x-api-key""}}",
                           Expected:="Claude didn't accept the key. Check that you copied all of it, or add it again."),
                          (Trial:=claude, Status:=500, Body:="{}",
                           Expected:="Claude had a problem on its side (HTTP 500). Try again later."),
                          (Trial:=elsewhere, Status:=401, Body:="{""error"":""unauthorized""}",
                           Expected:="The server at models.example.org didn't accept the request. Add its key, or check the one you added."),
                          (Trial:=local, Status:=404, Body:="404 page not found",
                           Expected:="The server at localhost:11434 has no list of models at /v1/models. Check the address; it usually ends in /v1."),
                          (Trial:=local, Status:=200, Body:="<html>Open WebUI</html>",
                           Expected:="The server answered, but not with a list of models. Check the address; it usually ends in /v1."),
                          (Trial:=local, Status:=200, Body:="{""object"":""list""}",
                           Expected:="The server answered, but not with a list of models. Check the address; it usually ends in /v1."),
                          (Trial:=local, Status:=200, Body:=ServerModelList(),
                           Expected:="The server at localhost:11434 is running, but lists no models. Load or download one on it first."),
                          (Trial:=local, Status:=200, Body:=many,
                           Expected:="The server at localhost:11434 is running, but doesn't list llama3.1. It lists: a, b, c, d, e, and 2 more.")}
            Dim scenario = item
            UseNetwork(Function(sent) Answer(CType(scenario.Status, HttpStatusCode), scenario.Body))
            Dim text As String
            Try
                Dim result As AssistantTestResult = RunTest(scenario.Trial)
                Assert.IsFalse(result.Succeeded, scenario.Expected)
                text = result.Message
            Catch ex As Exception
                text = AssistantRunner.DescribeTest(ex, scenario.Trial.Connection)
            End Try
            Assert.AreEqual(scenario.Expected, text)
        Next

        ' A server that isn't running, and no network at all.
        UseNetwork(Function(sent)
                       Throw New HttpRequestException("refused", New SocketException(10061))
                   End Function)
        Assert.AreEqual("PaperRoute couldn't reach the server at localhost:11434. Check that it is running.", AssistantRunner.DescribeTest(TestFailure(local), local.Connection))
        Assert.AreEqual("PaperRoute couldn't reach Claude. Check your internet connection, then try again.", AssistantRunner.DescribeTest(TestFailure(claude), claude.Connection))

        ' A feature's failure still says nothing was changed.
        StringAssert.EndsWith(AssistantRunner.Describe(New HttpRequestException("refused"), local.Connection), "Check that it is running. Nothing was changed.")
    End Sub

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
    Public Sub TheGuideAndCatalogDescribeBothAssistantServices()
        Dim claude As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantClaude)
        Dim compatible As OnlineService = OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantCompatible)
        Assert.IsTrue(claude.OffUntilTurnedOn AndAlso compatible.OffUntilTurnedOn)
        CollectionAssert.AreEqual({"api.anthropic.com"}, claude.Hosts.ToList())
        Assert.IsTrue(compatible.ConfiguredHost)
        Assert.AreEqual(0, compatible.Hosts.Count)
        StringAssert.Contains(compatible.Contacts, "http only on this computer")
        Dim source As String = File.ReadAllText(Path.Combine(TestRepository.Root(), "PaperRoute.Core", "Services", "AssistantProviders.vb"))
        StringAssert.Contains(source, ".HttpClient = http,", "The SDK always gets the gate's client.")
        StringAssert.Contains(source, "OnlineAccess.CreateClient(OnlineServiceCatalog.AssistantClaude", "...from the gate.")
        StringAssert.Contains(source, ".MaxRetries = 0", "...and never resends the text by itself.")
        ' Test Connection (#96) is listed with what it sends, and goes through the gate too.
        For Each item As OnlineService In {claude, compatible}
            StringAssert.Contains(item.Sends, "Test Connection in Preferences sends only the key, if you added one, to list the models and check the one you chose.")
            StringAssert.Contains(item.WhenUsed, "and Test Connection,")
        Next
        StringAssert.Contains(source, "OnlineAccess.CreateTrialClient(trial", "Test Connection uses the gate's client for the setup in the window.")
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function RunTest(trial As AssistantTrial) As AssistantTestResult
        Return AssistantConnectionTest.RunAsync(trial, CancellationToken.None).GetAwaiter().GetResult()
    End Function

    ' The exception a connection test ended with, or Nothing.
    Private Shared Function TestFailure(trial As AssistantTrial) As Exception
        Try
            RunTest(trial)
        Catch ex As Exception
            Return ex
        End Try
        Return Nothing
    End Function

    Private Function UseNetwork(respond As Func(Of SentRequest, HttpResponseMessage)) As CapturingNetwork
        OnlineAccess.ResetForTests()
        OnlineAccess.KeyStoreFactory = Function() _keys
        Dim network As New CapturingNetwork With {.Respond = respond}
        OnlineAccess.InnerHandlerFactory = Function() network
        Return network
    End Function

End Class
