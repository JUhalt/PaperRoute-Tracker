Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports Anthropic
Imports ManuscriptPipeline.Models
Imports Beta = Anthropic.Models.Beta.Messages
Imports SdkModels = Anthropic.Models.Models

Namespace Services

    ' One AI assistant request (#84): PaperRoute's instructions and the text
    ' the researcher saw before it was sent, and the JSON shape the answer
    ' must have, if any.
    Public NotInheritable Class AssistantRequest

        ' "decision-letter", "draft-response", or "cover-letter".
        Public Property Feature As String = String.Empty

        Public Property Instructions As String = String.Empty

        Public Property Content As String = String.Empty

        ' A JSON schema (an object) the answer must match; Nothing for text.
        Public Property Schema As Dictionary(Of String, JsonElement)

        Public Property MaxTokens As Integer = 16000

    End Class


    Public NotInheritable Class AssistantReply

        Public Property Text As String = String.Empty

        ' "Claude" or "OpenAI-compatible server", and the model that answered.
        Public Property ProviderName As String = String.Empty

        Public Property Model As String = String.Empty

        ' The answer stopped at its length limit.
        Public Property Truncated As Boolean

    End Class


    ' A plain-language reason an assistant request didn't give a usable
    ' answer, such as a missing key or a declined request.
    Public Class AssistantException
        Inherits InvalidOperationException

        Public Sub New(message As String, Optional inner As Exception = Nothing)
            MyBase.New(message, inner)
        End Sub

    End Class


    ' What a service said when it refused a request, such as "Your credit
    ' balance is too low": one short line, carried on the failure so the
    ' window can show the real reason.
    Friend NotInheritable Class AssistantErrorText

        Private Const DataKey As String = "PaperRoute.ServiceMessage"
        Private Const MaximumLength As Integer = 240

        Private Sub New()
        End Sub


        ' {"error":{"message":"..."}} (Anthropic, OpenAI), {"error":"..."}
        ' (Ollama), or {"message":"..."}; "" when the body has none.
        Public Shared Function FromBody(body As String) As String
            If String.IsNullOrWhiteSpace(body) Then Return String.Empty
            Try
                Using document As JsonDocument = JsonDocument.Parse(body)
                    Dim root As JsonElement = document.RootElement
                    Dim text As String = JsonFacts.Text(JsonFacts.Child(root, "error"), "message")
                    If text.Length = 0 Then text = JsonFacts.Text(root, "error")
                    If text.Length = 0 Then text = JsonFacts.Text(root, "message")
                    ' One plain line, whatever the service put in it: no
                    ' line breaks, control characters, or hidden formatting.
                    text = RegularExpressions.Regex.Replace(text, "\p{Cf}+", String.Empty)
                    text = RegularExpressions.Regex.Replace(text, "[\p{Cc}\s]+", " ").Trim()
                    If text.Length > MaximumLength Then text = text.Substring(0, MaximumLength).TrimEnd() & "..."
                    Return text
                End Using
            Catch ex As JsonException
                Return String.Empty
            End Try
        End Function


        Public Shared Function Attach(failure As Exception, message As String) As Exception
            If Not String.IsNullOrEmpty(message) Then failure.Data(DataKey) = message
            Return failure
        End Function


        Public Shared Function MessageOf(failure As Exception) As String
            Return If(TryCast(failure?.Data(DataKey), String), String.Empty)
        End Function

    End Class


    Public Interface IAssistantProvider

        ReadOnly Property ProviderName As String

        ReadOnly Property Model As String

        Function CompleteAsync(request As AssistantRequest, cancellationToken As CancellationToken) As Task(Of AssistantReply)

    End Interface


    ' Claude through the official Anthropic SDK, with the researcher's own
    ' key. The SDK is given the gate's HTTP client, so every request passes
    ' the gate: the gate adds the key (the SDK only ever holds a
    ' placeholder), drops every header but the few the API needs, and
    ' refuses the request while the assistant is off or Work offline is on.
    Public Class ClaudeAssistantProvider
        Implements IAssistantProvider

        Public Shared ReadOnly RequestTimeout As TimeSpan = TimeSpan.FromMinutes(5)

        ' The SDK needs a value; the gate replaces it with the real key.
        Friend Const PlaceholderKey As String = "added-by-paperroute"

        ' Models that take an effort level and Anthropic's server-side
        ' fallback, which re-serves a request a safety check declined on
        ' another model instead of failing it.
        Private Shared ReadOnly CurrentModels As New HashSet(Of String)({"claude-opus-5-5", "claude-opus-5", "claude-sonnet-5-5", "claude-fable-5-1"}, StringComparer.OrdinalIgnoreCase)

        Private ReadOnly _model As String

        Public Sub New(model As String)
            _model = If(String.IsNullOrWhiteSpace(model), "claude-opus-5-5", model.Trim())
        End Sub

        Public ReadOnly Property ProviderName As String Implements IAssistantProvider.ProviderName
            Get
                Return "Claude"
            End Get
        End Property

        Public ReadOnly Property Model As String Implements IAssistantProvider.Model
            Get
                Return _model
            End Get
        End Property


        Public Async Function CompleteAsync(request As AssistantRequest, cancellationToken As CancellationToken) As Task(Of AssistantReply) Implements IAssistantProvider.CompleteAsync

            OnlineAccess.Check(OnlineServiceCatalog.AssistantClaude)
            If Not OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.Anthropic) Then
                Throw New AssistantException("Add your Claude key in Settings > Preferences > AI assistant first. Nothing was sent.")
            End If

            Using http As HttpClient = OnlineAccess.CreateClient(OnlineServiceCatalog.AssistantClaude, RequestTimeout)
                Dim client As AnthropicClient = NewClient(http, RequestTimeout)
                Dim parameters As Beta.MessageCreateParams = BuildParameters(request, _model)
                Dim message As Beta.BetaMessage
                Try
                    message = Await client.Beta.Messages.Create(parameters, cancellationToken).ConfigureAwait(False)
                Catch ex As Exception When Not TypeOf ex Is OperationCanceledException OrElse Not cancellationToken.IsCancellationRequested
                    Throw Translate(ex)
                End Try
                Return ReadReply(message, _model)
            End Using

        End Function


        ' The SDK on the gate's client, holding only a placeholder key.
        Private Shared Function NewClient(http As HttpClient, timeout As TimeSpan) As AnthropicClient
            Return New AnthropicClient With {
                .HttpClient = http,
                .BaseUrl = "https://" & OnlineAccess.AnthropicHost,
                .ApiKey = PlaceholderKey,
                .AuthToken = Nothing,
                .MaxRetries = 0,
                .Timeout = timeout
            }
        End Function


        ' A name that is safe as part of an address, such as claude-opus-5-5.
        Friend Shared Function IsModelName(model As String) As Boolean
            Return RegularExpressions.Regex.IsMatch(If(model, String.Empty), "^[A-Za-z0-9][A-Za-z0-9._:@-]{0,99}$")
        End Function


        ' Test Connection (#96): the models the key's account can use, and
        ' whether it can use this one. The list gives full names, such as
        ' claude-haiku-4-5-20251001, so a name not in it, such as the alias
        ' claude-haiku-4-5, is looked up on its own; only "not found" means
        ' the account can't use it. Only the key is sent, by the gate.
        Friend Shared Async Function ListModelsAsync(http As HttpClient, model As String, cancellationToken As CancellationToken) As Task(Of (Models As List(Of String), ModelFound As Boolean))

            Dim client As AnthropicClient = NewClient(http, http.Timeout)
            Try
                Dim page As SdkModels.ModelListPage = Await client.Models.List(New SdkModels.ModelListParams With {.Limit = 1000}, cancellationToken).ConfigureAwait(False)
                Dim names As List(Of String) = page.Items.Select(Function(item) item.ID).Where(AddressOf IsModelName).Distinct(StringComparer.Ordinal).ToList()
                If names.Contains(model, StringComparer.Ordinal) Then Return (names, True)
                If Not IsModelName(model) Then Return (names, False)
                Dim found As Boolean
                Try
                    Await client.Models.Retrieve(model, Nothing, cancellationToken).ConfigureAwait(False)
                    found = True
                Catch ex As Exception When IsNotFound(ex)
                    found = False
                End Try
                Return (names, found)
            Catch ex As Exception When Not TypeOf ex Is OperationCanceledException OrElse Not cancellationToken.IsCancellationRequested
                Throw Translate(ex)
            End Try

        End Function


        Private Shared Function IsNotFound(ex As Exception) As Boolean
            Dim api As Anthropic.Exceptions.AnthropicApiException = FindInner(Of Anthropic.Exceptions.AnthropicApiException)(ex)
            Return api IsNot Nothing AndAlso CInt(api.StatusCode) = 404
        End Function


        ' The request as sent: the instructions as the system prompt, the
        ' researcher's text as the one user message.
        Friend Shared Function BuildParameters(request As AssistantRequest, model As String) As Beta.MessageCreateParams
            Dim current As Boolean = CurrentModels.Contains(model)
            ' Effort only where the model takes it; a JSON shape when asked.
            Dim output As Beta.BetaOutputConfig = Nothing
            If current AndAlso request.Schema IsNot Nothing Then
                output = New Beta.BetaOutputConfig With {.Effort = Beta.Effort.Medium, .Format = New Beta.BetaJsonOutputFormat With {.Schema = request.Schema}}
            ElseIf current Then
                output = New Beta.BetaOutputConfig With {.Effort = Beta.Effort.Medium}
            ElseIf request.Schema IsNot Nothing Then
                output = New Beta.BetaOutputConfig With {.Format = New Beta.BetaJsonOutputFormat With {.Schema = request.Schema}}
            End If
            Dim parameters As New Beta.MessageCreateParams With {
                .Model = model,
                .MaxTokens = request.MaxTokens,
                .System = request.Instructions,
                .Messages = New List(Of Beta.BetaMessageParam) From {
                    New Beta.BetaMessageParam With {.Role = Beta.Role.User, .Content = request.Content}
                }
            }
            If output IsNot Nothing Then parameters = New Beta.MessageCreateParams(parameters) With {.OutputConfig = output}
            If current Then
                parameters = New Beta.MessageCreateParams(parameters) With {
                    .Betas = New List(Of Anthropic.Core.ApiEnum(Of String, Anthropic.Models.Beta.AnthropicBeta)) From {Anthropic.Models.Beta.AnthropicBeta.ServerSideFallback2026_07_01},
                    .Fallbacks = New Beta.Default()
                }
            End If
            Return parameters
        End Function


        Friend Shared Function ReadReply(message As Beta.BetaMessage, model As String) As AssistantReply
            Dim stopReason As String = If(message.StopReason Is Nothing, String.Empty, message.StopReason.Raw())
            If String.Equals(stopReason, "refusal", StringComparison.Ordinal) Then
                Throw New AssistantException("Claude declined this request. Nothing was changed.")
            End If
            Dim text As New StringBuilder()
            For Each block As Beta.BetaContentBlock In message.Content
                Dim part As Beta.BetaTextBlock = Nothing
                If block.TryPickText(part) Then text.Append(part.Text)
            Next
            Return New AssistantReply With {
                .Text = text.ToString(),
                .ProviderName = "Claude",
                .Model = If(message.Model Is Nothing, model, message.Model.Raw()),
                .Truncated = String.Equals(stopReason, "max_tokens", StringComparison.Ordinal)
            }
        End Function


        ' The SDK's errors, and the gate's refusal inside them, as the
        ' exceptions the rest of PaperRoute explains in plain words.
        Friend Shared Function Translate(ex As Exception) As Exception
            Dim blocked As OnlineServiceBlockedException = FindInner(Of OnlineServiceBlockedException)(ex)
            If blocked IsNot Nothing Then Return blocked
            If FindInner(Of TimeoutException)(ex) IsNot Nothing Then
                Return New TaskCanceledException("Claude didn't answer in time.", New TimeoutException())
            End If
            Dim api As Anthropic.Exceptions.AnthropicApiException = FindInner(Of Anthropic.Exceptions.AnthropicApiException)(ex)
            If api IsNot Nothing Then
                Dim code As Integer = CInt(api.StatusCode)
                If code = 429 Then Return AssistantErrorText.Attach(New OnlineServiceBusyException("Claude", Nothing, False), AssistantErrorText.FromBody(api.ResponseBody))
                Return AssistantErrorText.Attach(
                    New HttpRequestException("Claude answered with HTTP " & code.ToString(CultureInfo.InvariantCulture) & ".", ex, api.StatusCode),
                    AssistantErrorText.FromBody(api.ResponseBody))
            End If
            If FindInner(Of Anthropic.Exceptions.AnthropicIOException)(ex) IsNot Nothing OrElse FindInner(Of HttpRequestException)(ex) IsNot Nothing Then
                Return New HttpRequestException("PaperRoute couldn't reach Claude.", ex)
            End If
            Return New AssistantException("Claude's answer couldn't be read. Nothing was changed.", ex)
        End Function


        Private Shared Function FindInner(Of T As Exception)(ex As Exception) As T
            Dim current As Exception = ex
            While current IsNot Nothing
                Dim match As T = TryCast(current, T)
                If match IsNot Nothing Then Return match
                current = current.InnerException
            End While
            Return Nothing
        End Function

    End Class


    ' A server that speaks the OpenAI chat completions protocol: a model on
    ' this computer (Ollama, LM Studio, llama.cpp) or another service over
    ' https. Plain HTTP through the gate, which checks the address against
    ' the one set in Preferences and adds the optional key only there.
    Public Class CompatibleAssistantProvider
        Implements IAssistantProvider

        ' A model on this computer can take minutes to load and answer.
        Public Shared ReadOnly RequestTimeout As TimeSpan = TimeSpan.FromMinutes(10)

        ' The answer's length limit: the name every server takes, and the
        ' one some newer models ask for instead.
        Friend Const LengthLimitName As String = "max_tokens"
        Friend Const NewerLengthLimitName As String = "max_completion_tokens"

        Private ReadOnly _endpoint As Uri
        Private ReadOnly _model As String

        Public Sub New(endpoint As Uri, model As String)
            _endpoint = endpoint
            _model = If(model, String.Empty).Trim()
        End Sub

        Public ReadOnly Property ProviderName As String Implements IAssistantProvider.ProviderName
            Get
                Return "OpenAI-compatible server"
            End Get
        End Property

        Public ReadOnly Property Model As String Implements IAssistantProvider.Model
            Get
                Return _model
            End Get
        End Property


        ' {endpoint}/chat/completions, such as http://localhost:11434/v1/chat/completions.
        Public Shared Function CompletionsAddress(endpoint As Uri) As Uri
            Return New Uri(endpoint.AbsoluteUri.TrimEnd("/"c) & "/chat/completions")
        End Function


        ' {endpoint}/models, such as http://localhost:11434/v1/models.
        Public Shared Function ModelsAddress(endpoint As Uri) As Uri
            Return New Uri(endpoint.AbsoluteUri.TrimEnd("/"c) & "/models")
        End Function


        Public Async Function CompleteAsync(request As AssistantRequest, cancellationToken As CancellationToken) As Task(Of AssistantReply) Implements IAssistantProvider.CompleteAsync

            OnlineAccess.Check(OnlineServiceCatalog.AssistantCompatible)
            If _endpoint Is Nothing OrElse _model.Length = 0 Then
                Throw New AssistantException("Set the server's address and model in Settings > Preferences > AI assistant first. Nothing was sent.")
            End If

            Using http As HttpClient = OnlineAccess.CreateClient(OnlineServiceCatalog.AssistantCompatible, RequestTimeout)
                Dim useSchema As Boolean = request.Schema IsNot Nothing
                Dim limitName As String = LengthLimitName
                Dim answer = Await SendAsync(http, request, useSchema, limitName, cancellationToken).ConfigureAwait(False)
                ' Some newer models take the length limit under another name,
                ' and say so; they are asked once more with that name.
                If answer.Status = HttpStatusCode.BadRequest AndAlso If(answer.Body, String.Empty).Contains(NewerLengthLimitName, StringComparison.OrdinalIgnoreCase) Then
                    limitName = NewerLengthLimitName
                    answer = Await SendAsync(http, request, useSchema, limitName, cancellationToken).ConfigureAwait(False)
                End If
                ' A server that doesn't support structured answers is asked
                ' once more without them; the instructions still ask for JSON.
                If answer.Status = HttpStatusCode.BadRequest AndAlso useSchema Then
                    answer = Await SendAsync(http, request, False, limitName, cancellationToken).ConfigureAwait(False)
                End If
                ThrowIfFailed(answer.Status, answer.Body, answer.RetryAfter)
                Return ParseReply(answer.Body, _model)
            End Using

        End Function


        ' A refusal or a wait the server asked for, with its own reason.
        Private Shared Sub ThrowIfFailed(status As HttpStatusCode, body As String, retryAfter As TimeSpan?)
            If CInt(status) = 429 Then
                Throw AssistantErrorText.Attach(New OnlineServiceBusyException("The server", retryAfter, False), AssistantErrorText.FromBody(body))
            End If
            If CInt(status) < 200 OrElse CInt(status) > 299 Then
                Throw AssistantErrorText.Attach(
                    New HttpRequestException("The server answered with HTTP " & CInt(status).ToString(CultureInfo.InvariantCulture) & ".", Nothing, status),
                    AssistantErrorText.FromBody(body))
            End If
        End Sub


        ' Test Connection (#96): the models the server lists, asked for at
        ' its address with nothing but the optional key, which the gate adds.
        Friend Shared Async Function ListModelsAsync(http As HttpClient, endpoint As Uri, cancellationToken As CancellationToken) As Task(Of List(Of String))
            Using message As New HttpRequestMessage(HttpMethod.Get, ModelsAddress(endpoint))
                message.Headers.Accept.ParseAdd("application/json")
                Using response As HttpResponseMessage = Await http.SendAsync(message, cancellationToken).ConfigureAwait(False)
                    Dim body As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                    ThrowIfFailed(response.StatusCode, body, response.Headers.RetryAfter?.Delta)
                    Return ParseModelList(body)
                End Using
            End Using
        End Function


        ' {"object":"list","data":[{"id":"llama3.1:latest"}, ...]}: each
        ' name as one short plain line, whatever the server put in it.
        Friend Shared Function ParseModelList(body As String) As List(Of String)
            Const NotAList As String = "The server answered, but not with a list of models. Check the address; it usually ends in /v1."
            Try
                Using document As JsonDocument = JsonDocument.Parse(If(body, String.Empty))
                    Dim data As JsonElement = JsonFacts.Child(document.RootElement, "data")
                    If data.ValueKind <> JsonValueKind.Array Then Throw New AssistantException(NotAList)
                    Return data.EnumerateArray().
                        Select(Function(item) CleanName(JsonFacts.RawText(item, "id"))).
                        Where(Function(name) name.Length > 0).
                        Distinct(StringComparer.Ordinal).
                        ToList()
                End Using
            Catch ex As JsonException
                Throw New AssistantException(NotAList, ex)
            End Try
        End Function


        Private Shared Function CleanName(name As String) As String
            Dim text As String = RegularExpressions.Regex.Replace(If(name, String.Empty), "\p{Cf}+", String.Empty)
            text = RegularExpressions.Regex.Replace(text, "[\p{Cc}\s]+", " ").Trim()
            Return If(text.Length > 100, text.Substring(0, 100).TrimEnd() & "...", text)
        End Function


        ' Whether the server lists the model: the same name in any case, or,
        ' as Ollama names them, the name without a tag for its :latest.
        Friend Shared Function ListsModel(models As IEnumerable(Of String), model As String) As Boolean
            Dim wanted As String = If(model, String.Empty).Trim()
            If wanted.Length = 0 OrElse models Is Nothing Then Return False
            Return models.Any(Function(name) String.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) OrElse
                                             (Not wanted.Contains(":"c) AndAlso String.Equals(name, wanted & ":latest", StringComparison.OrdinalIgnoreCase)))
        End Function


        Private Async Function SendAsync(http As HttpClient, request As AssistantRequest, useSchema As Boolean, limitName As String, cancellationToken As CancellationToken) As Task(Of (Status As HttpStatusCode, Body As String, RetryAfter As TimeSpan?))
            Using message As New HttpRequestMessage(HttpMethod.Post, CompletionsAddress(_endpoint))
                message.Content = New StringContent(BuildBody(request, _model, useSchema, limitName), Encoding.UTF8, "application/json")
                message.Headers.Accept.ParseAdd("application/json")
                Using response As HttpResponseMessage = Await http.SendAsync(message, cancellationToken).ConfigureAwait(False)
                    Dim body As String = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                    Return (response.StatusCode, body, response.Headers.RetryAfter?.Delta)
                End Using
            End Using
        End Function


        Friend Shared Function BuildBody(request As AssistantRequest, model As String, useSchema As Boolean, Optional limitName As String = LengthLimitName) As String
            Dim body As New Dictionary(Of String, Object) From {
                {"model", model},
                {"messages", New Object() {
                    New Dictionary(Of String, String) From {{"role", "system"}, {"content", request.Instructions}},
                    New Dictionary(Of String, String) From {{"role", "user"}, {"content", request.Content}}
                }},
                {limitName, request.MaxTokens},
                {"stream", False}
            }
            If useSchema AndAlso request.Schema IsNot Nothing Then
                body("response_format") = New Dictionary(Of String, Object) From {
                    {"type", "json_schema"},
                    {"json_schema", New Dictionary(Of String, Object) From {{"name", "answer"}, {"strict", True}, {"schema", request.Schema}}}
                }
            End If
            Return JsonSerializer.Serialize(body)
        End Function


        Friend Shared Function ParseReply(body As String, model As String) As AssistantReply
            Try
                Using document As JsonDocument = JsonDocument.Parse(body)
                    Dim choice As JsonElement = JsonFacts.Items(document.RootElement, "choices").FirstOrDefault()
                    If choice.ValueKind <> JsonValueKind.Object Then Throw New AssistantException("The server's answer had no text. Nothing was changed.")
                    Dim message As JsonElement = JsonFacts.Child(choice, "message")
                    Dim content As JsonElement = JsonFacts.Child(message, "content")
                    Dim reported As String = JsonFacts.RawText(document.RootElement, "model")
                    Return New AssistantReply With {
                        .Text = If(content.ValueKind = JsonValueKind.String, content.GetString(), String.Empty),
                        .ProviderName = "OpenAI-compatible server",
                        .Model = If(reported.Length > 0, reported, model),
                        .Truncated = String.Equals(JsonFacts.RawText(choice, "finish_reason"), "length", StringComparison.Ordinal)
                    }
                End Using
            Catch ex As JsonException
                Throw New AssistantException("The server's answer couldn't be read. Nothing was changed.", ex)
            End Try
        End Function

    End Class


    ' What Test Connection found (#96).
    Friend NotInheritable Class AssistantTestResult

        Public Property Message As String = String.Empty

        ' Connected, and the model is there; False is a warning about the model.
        Public Property Succeeded As Boolean

        ' The models the service listed, in its order.
        Public Property Models As New List(Of String)()

    End Class


    ' Test Connection in Preferences (#96): checks the AI assistant as set
    ' up in the window, before Save, by asking the service for its models.
    ' It sends no text of the researcher's, only the key if there is one,
    ' through the gate; a failure is thrown for the window to explain.
    Friend NotInheritable Class AssistantConnectionTest

        Private Sub New()
        End Sub

        Public Shared ReadOnly Timeout As TimeSpan = TimeSpan.FromSeconds(OnlineAccess.TimeoutSeconds)

        Private Const MaximumNamesShown As Integer = 5


        ' The line below the button: what testing sends, and to whom.
        Public Shared Function WhatIsSent(connection As AssistantConnection, willSendKey As Boolean) As String
            If connection Is Nothing Then Return String.Empty
            If connection.Provider = AssistantProvider.Claude Then
                Return "Test Connection sends only your Claude key to " & connection.Origin.Host & ", to list the models your account can use."
            End If
            Dim where As String = connection.Origin.Authority
            Return If(willSendKey,
                      "Test Connection sends only your server key to " & where & ", to list its models.",
                      "Test Connection asks " & where & " for its models and sends nothing else.") &
                   If(connection.IsOnThisComputer, " Nothing leaves this computer.", String.Empty)
        End Function


        Public Shared Async Function RunAsync(trial As AssistantTrial, cancellationToken As CancellationToken) As Task(Of AssistantTestResult)

            If trial Is Nothing OrElse trial.Connection Is Nothing Then Throw New ArgumentNullException(NameOf(trial))
            Dim connection As AssistantConnection = trial.Connection
            OnlineAccess.CheckTrial(connection.ServiceId)

            If connection.Provider = AssistantProvider.Claude Then
                If trial.PendingKey Is Nothing AndAlso Not (trial.UseStoredKey AndAlso OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.Anthropic)) Then
                    Throw New AssistantException("Add your Claude key first. Nothing was sent.")
                End If
                Using http As HttpClient = OnlineAccess.CreateTrialClient(trial, Timeout)
                    Dim listed = Await ClaudeAssistantProvider.ListModelsAsync(http, connection.Model, cancellationToken).ConfigureAwait(False)
                    Return ClaudeResult(connection.Model, listed.Models, listed.ModelFound)
                End Using
            End If

            Using http As HttpClient = OnlineAccess.CreateTrialClient(trial, Timeout)
                Dim models As List(Of String) = Await CompatibleAssistantProvider.ListModelsAsync(http, connection.Endpoint, cancellationToken).ConfigureAwait(False)
                Return ServerResult(connection, models)
            End Using

        End Function


        Friend Shared Function ClaudeResult(model As String, models As List(Of String), found As Boolean) As AssistantTestResult
            Dim count As String = models.Count.ToString(CultureInfo.CurrentCulture) & If(models.Count = 1, " model", " models")
            If found Then
                Return New AssistantTestResult With {
                    .Succeeded = True,
                    .Models = models,
                    .Message = "Connected. Claude accepted your key, and your account can use " & model & "." &
                               If(models.Count > 0, " The Model list now shows your account's " & count & ".", String.Empty)
                }
            End If
            Return New AssistantTestResult With {
                .Succeeded = False,
                .Models = models,
                .Message = "Claude accepted your key, but your account can't use the model " & model & "." &
                           If(models.Count > 0, " Choose one from the Model list, which now shows your account's " & count & ".", " Check the model's name.")
            }
        End Function


        Friend Shared Function ServerResult(connection As AssistantConnection, models As List(Of String)) As AssistantTestResult
            Dim server As String = "The server at " & connection.Origin.Authority
            If models.Count = 0 Then
                Return New AssistantTestResult With {
                    .Succeeded = False,
                    .Models = models,
                    .Message = server & " is running, but lists no models. Load or download one on it first."
                }
            End If
            If CompatibleAssistantProvider.ListsModel(models, connection.Model) Then
                Return New AssistantTestResult With {
                    .Succeeded = True,
                    .Models = models,
                    .Message = "Connected. " & server & " has " & connection.Model & "."
                }
            End If
            ' Some servers list one name but answer to any, so this is a
            ' warning to check the name, not a failure.
            Dim shown As String = String.Join(", ", models.Take(MaximumNamesShown))
            Dim more As Integer = models.Count - MaximumNamesShown
            If more > 0 Then shown &= ", and " & more.ToString(CultureInfo.CurrentCulture) & " more"
            Return New AssistantTestResult With {
                .Succeeded = False,
                .Models = models,
                .Message = server & " is running, but doesn't list " & connection.Model & ". It lists: " & shown & "."
            }
        End Function

    End Class

End Namespace
