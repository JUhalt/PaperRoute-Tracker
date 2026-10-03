Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum OnlineBlockReason
        WorkOffline
        ServiceOff
        UnexpectedHost
        ' The AI assistant (#84) is off, or set up for another service.
        NotTurnedOn
    End Enum


    ' The AI assistant's service as set up now (#84): which one, which
    ' model, and where it is.
    Public NotInheritable Class AssistantConnection

        Public Property ServiceId As String = String.Empty

        Public Property Provider As AssistantProvider

        ' "Claude" or "OpenAI-compatible server": what provenance records.
        Public Property ProviderName As String = String.Empty

        Public Property Model As String = String.Empty

        ' https://api.anthropic.com, or the compatible server's address
        ' (scheme, host, and port).
        Public Property Origin As Uri

        ' The compatible server's base address, up to /v1.
        Public Property Endpoint As Uri

        ' A server on this computer: nothing leaves it.
        Public Property IsOnThisComputer As Boolean

        ' "Claude at api.anthropic.com", "the server at localhost:11434 (on this computer)".
        Public ReadOnly Property Recipient As String
            Get
                If Provider = AssistantProvider.Claude Then Return "Claude at " & Origin.Host
                Return "the server at " & Origin.Authority & If(IsOnThisComputer, " (on this computer)", String.Empty)
            End Get
        End Property

    End Class


    ' The AI assistant as set up in the open Preferences window, for Test
    ' Connection (#96) before Save. It is only held in memory; the gate
    ' checks every request against it as it does the saved setup.
    Friend NotInheritable Class AssistantTrial

        Public Property Connection As AssistantConnection

        ' A key added in the window and not saved yet; Nothing if none.
        Public Property PendingKey As String

        ' Without a key added in the window: send the stored key (the Claude
        ' key, or the server key stored for this address), as Save keeps it.
        Public Property UseStoredKey As Boolean

    End Class


    ' A request the Online services settings did not allow (#86). It is an
    ' InvalidOperationException so existing error paths show its plain
    ' message; callers that run many lookups stop instead of failing each.
    Public Class OnlineServiceBlockedException
        Inherits InvalidOperationException

        Public Sub New(service As OnlineService, reason As OnlineBlockReason, Optional host As String = Nothing)
            MyBase.New(OnlineAccess.BlockedMessage(service, reason, host))
            ServiceId = service?.Id
            Me.Reason = reason
        End Sub

        Public ReadOnly Property ServiceId As String

        Public ReadOnly Property Reason As OnlineBlockReason

    End Class


    ' A service asked PaperRoute to wait (HTTP 429), with how long if it
    ' said. OpenAlex limits anonymous searches when it is busy, and ends a
    ' free daily allowance at midnight UTC.
    Public Class OnlineServiceBusyException
        Inherits HttpRequestException

        Public Sub New(serviceName As String, retryAfter As TimeSpan?, dailyAllowanceUsed As Boolean)
            MyBase.New(serviceName & " asked PaperRoute to wait.", Nothing, CType(429, HttpStatusCode))
            Me.RetryAfter = retryAfter
            Me.DailyAllowanceUsed = dailyAllowanceUsed
        End Sub

        Public ReadOnly Property RetryAfter As TimeSpan?

        Public ReadOnly Property DailyAllowanceUsed As Boolean

        ' From the Retry-After header, else the body's retryAfter seconds;
        ' a wait of more than five minutes means the daily allowance.
        Public Shared Function FromResponse(serviceName As String, response As HttpResponseMessage, body As String) As OnlineServiceBusyException
            Dim wait As TimeSpan? = Nothing
            If response?.Headers.RetryAfter IsNot Nothing Then
                If response.Headers.RetryAfter.Delta.HasValue Then
                    wait = response.Headers.RetryAfter.Delta
                ElseIf response.Headers.RetryAfter.Date.HasValue Then
                    wait = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow
                End If
            End If
            If Not wait.HasValue AndAlso Not String.IsNullOrEmpty(body) Then
                Dim match = Text.RegularExpressions.Regex.Match(body, """retryAfter""\s*:\s*(\d{1,6})")
                If match.Success Then wait = TimeSpan.FromSeconds(Integer.Parse(match.Groups(1).Value, Globalization.CultureInfo.InvariantCulture))
            End If
            If wait.HasValue AndAlso wait.Value < TimeSpan.Zero Then wait = TimeSpan.Zero
            Dim anonymousSearch As Boolean = If(body, String.Empty).IndexOf("Anonymous search", StringComparison.OrdinalIgnoreCase) >= 0
            Dim daily As Boolean = Not anonymousSearch AndAlso (Not wait.HasValue OrElse wait.Value > TimeSpan.FromMinutes(5))
            Return New OnlineServiceBusyException(serviceName, wait, daily)
        End Function

    End Class


    ' The one gate every PaperRoute request passes through (#86). A request
    ' is refused, before anything is sent, when Work offline is on, when its
    ' service is switched off, or when it would reach a host its service does
    ' not list. Redirects are followed here so every hop is checked.
    Public NotInheritable Class OnlineAccess

        Private Sub New()
        End Sub

        Public Const TimeoutSeconds As Integer = 20

        Private Shared ReadOnly StateLock As New Object()
        Private Shared _workOffline As Boolean
        Private Shared ReadOnly _turnedOff As New HashSet(Of String)(StringComparer.Ordinal)
        Private Shared ReadOnly _clients As New Dictionary(Of String, HttpClient)(StringComparer.Ordinal)
        Private Shared _inner As HttpMessageHandler
        Private Shared _loopback As HttpMessageInvoker
        Private Shared _assistant As New AssistantSettings()
        Private Shared _assistantEndpoint As Uri
        Private Shared ReadOnly _confirmedUses As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Public Const AnthropicHost As String = "api.anthropic.com"

        ' Test seams: the handler under the gate, and the key store.
        Friend Shared InnerHandlerFactory As Func(Of HttpMessageHandler) = Nothing
        Friend Shared KeyStoreFactory As Func(Of ProtectedKeyStore) = Nothing

        ' Called when the researcher agrees not to be asked again before a
        ' feature sends to a service ("feature|origin"); Form1 saves it.
        Friend Shared AssistantUseConfirmed As Action(Of String) = Nothing


        Public Shared Sub Configure(settings As OnlineServicesSettings)
            SyncLock StateLock
                _workOffline = settings IsNot Nothing AndAlso settings.WorkOffline
                _turnedOff.Clear()
                For Each id As String In If(settings?.TurnedOff, New List(Of String)())
                    If Not String.IsNullOrWhiteSpace(id) Then _turnedOff.Add(id.Trim())
                Next
                Dim assistant As AssistantSettings = If(settings?.Assistant, New AssistantSettings())
                _assistant = New AssistantSettings With {
                    .Enabled = assistant.Enabled,
                    .Provider = assistant.Provider,
                    .ClaudeModel = If(assistant.ClaudeModel, String.Empty).Trim(),
                    .Endpoint = If(assistant.Endpoint, String.Empty).Trim(),
                    .EndpointModel = If(assistant.EndpointModel, String.Empty).Trim(),
                    .EndpointKeyOrigin = If(assistant.EndpointKeyOrigin, String.Empty).Trim()
                }
                _assistantEndpoint = ParseAssistantEndpoint(_assistant.Endpoint)
                _confirmedUses.Clear()
                For Each use As String In If(assistant.ConfirmedUses, New List(Of String)())
                    If Not String.IsNullOrWhiteSpace(use) Then _confirmedUses.Add(use.Trim())
                Next
            End SyncLock
        End Sub


        ' A compatible server's base address, or Nothing when it can't be
        ' used: absolute http or https, no user name, password, query, or
        ' fragment, and http only on this computer (a loopback address).
        Public Shared Function ParseAssistantEndpoint(value As String) As Uri
            Dim text As String = If(value, String.Empty).Trim().TrimEnd("/"c)
            Dim parsed As Uri = Nothing
            If text.Length = 0 OrElse Not Uri.TryCreate(text, UriKind.Absolute, parsed) Then Return Nothing
            If parsed.Scheme <> Uri.UriSchemeHttps AndAlso parsed.Scheme <> Uri.UriSchemeHttp Then Return Nothing
            If parsed.UserInfo.Length > 0 OrElse parsed.Query.Length > 0 OrElse parsed.Fragment.Length > 0 OrElse parsed.Host.Length = 0 Then Return Nothing
            If parsed.Scheme = Uri.UriSchemeHttp AndAlso Not parsed.IsLoopback Then Return Nothing
            Return parsed
        End Function


        ' "http://localhost:11434": scheme, host, and port, lower-case.
        Public Shared Function OriginOf(address As Uri) As String
            If address Is Nothing OrElse Not address.IsAbsoluteUri Then Return String.Empty
            Return (address.Scheme & "://" & address.Host & ":" & address.Port.ToString(Globalization.CultureInfo.InvariantCulture)).ToLowerInvariant()
        End Function


        ' The AI assistant's service as set up now, or Nothing when it is
        ' off or not set up (whether or not it is blocked right now).
        Public Shared Function CurrentAssistant() As AssistantConnection
            SyncLock StateLock
                If Not _assistant.Enabled Then Return Nothing
                Return ConnectionFor(_assistant)
            End SyncLock
        End Function


        ' The service a setup names, whether or not it is turned on or
        ' saved (Test Connection tries one before Save, #96); Nothing when
        ' a compatible server has no usable address or model.
        Friend Shared Function ConnectionFor(assistant As AssistantSettings) As AssistantConnection
            If assistant Is Nothing Then Return Nothing
            If assistant.Provider = AssistantProvider.Claude Then
                Dim claudeModel As String = If(assistant.ClaudeModel, String.Empty).Trim()
                Return New AssistantConnection With {
                    .ServiceId = OnlineServiceCatalog.AssistantClaude,
                    .Provider = AssistantProvider.Claude,
                    .ProviderName = "Claude",
                    .Model = If(claudeModel.Length > 0, claudeModel, "claude-opus-5-5"),
                    .Origin = New Uri("https://" & AnthropicHost),
                    .IsOnThisComputer = False
                }
            End If
            Dim endpoint As Uri = ParseAssistantEndpoint(assistant.Endpoint)
            Dim endpointModel As String = If(assistant.EndpointModel, String.Empty).Trim()
            If endpoint Is Nothing OrElse endpointModel.Length = 0 Then Return Nothing
            Return New AssistantConnection With {
                .ServiceId = OnlineServiceCatalog.AssistantCompatible,
                .Provider = AssistantProvider.Compatible,
                .ProviderName = "OpenAI-compatible server",
                .Model = endpointModel,
                .Origin = New Uri(endpoint.GetLeftPart(UriPartial.Authority)),
                .Endpoint = endpoint,
                .IsOnThisComputer = endpoint.IsLoopback
            }
        End Function


        ' Whether to show what a feature sends and ask first: always for a
        ' service elsewhere, unless the researcher said not to ask again for
        ' this feature and this service; never for one on this computer.
        Public Shared Function NeedsConfirmation(feature As String) As Boolean
            Dim connection As AssistantConnection = CurrentAssistant()
            If connection Is Nothing OrElse connection.IsOnThisComputer Then Return False
            SyncLock StateLock
                Return Not _confirmedUses.Contains(feature & "|" & OriginOf(connection.Origin))
            End SyncLock
        End Function


        ' Don't ask again before this feature sends to this service.
        Public Shared Sub RememberConfirmation(feature As String)
            Dim connection As AssistantConnection = CurrentAssistant()
            If connection Is Nothing Then Return
            Dim use As String = feature & "|" & OriginOf(connection.Origin)
            SyncLock StateLock
                If Not _confirmedUses.Add(use) Then Return
            End SyncLock
            AssistantUseConfirmed?.Invoke(use)
        End Sub


        Public Shared ReadOnly Property IsWorkingOffline As Boolean
            Get
                SyncLock StateLock
                    Return _workOffline
                End SyncLock
            End Get
        End Property


        ' Why a service may not be used now, or Nothing when it may.
        Public Shared Function BlockReason(serviceId As String) As OnlineBlockReason?
            Dim service As OnlineService = OnlineServiceCatalog.Find(serviceId)
            SyncLock StateLock
                If _workOffline Then Return OnlineBlockReason.WorkOffline
                If _turnedOff.Contains(serviceId) Then Return OnlineBlockReason.ServiceOff
                If service IsNot Nothing AndAlso service.OffUntilTurnedOn Then
                    If Not _assistant.Enabled Then Return OnlineBlockReason.NotTurnedOn
                    If serviceId = OnlineServiceCatalog.AssistantClaude AndAlso _assistant.Provider <> AssistantProvider.Claude Then Return OnlineBlockReason.NotTurnedOn
                    If serviceId = OnlineServiceCatalog.AssistantCompatible AndAlso
                       (_assistant.Provider <> AssistantProvider.Compatible OrElse _assistantEndpoint Is Nothing) Then Return OnlineBlockReason.NotTurnedOn
                End If
                Return Nothing
            End SyncLock
        End Function


        Public Shared Function IsAllowed(serviceId As String) As Boolean
            Return Not BlockReason(serviceId).HasValue
        End Function


        Public Shared Sub Check(serviceId As String)
            Dim service As OnlineService = Require(serviceId)
            Dim reason As OnlineBlockReason? = BlockReason(serviceId)
            If reason.HasValue Then Throw New OnlineServiceBlockedException(service, reason.Value)
        End Sub


        ' Test Connection (#96) tries the AI assistant as set up in the
        ' open Preferences window, before it is saved, so only Work offline
        ' refuses it: the saved choice to turn the assistant on is the one
        ' being made.
        Friend Shared Sub CheckTrial(serviceId As String)
            Dim service As OnlineService = Require(serviceId)
            If Not service.OffUntilTurnedOn Then Throw New ArgumentException("Only the AI assistant is tried before it is saved.", NameOf(serviceId))
            If IsWorkingOffline Then Throw New OnlineServiceBlockedException(service, OnlineBlockReason.WorkOffline)
        End Sub


        ' Only https, and only a host the service lists; for a compatible
        ' server, only the address set for it (http only on this computer).
        Friend Shared Sub CheckHost(service As OnlineService, target As Uri)
            Dim endpoint As Uri
            SyncLock StateLock
                endpoint = _assistantEndpoint
            End SyncLock
            CheckHost(service, target, endpoint)
        End Sub


        ' The same, comparing a compatible server's request with the given
        ' address: the saved one, or the one a Test Connection trial is for.
        Friend Shared Sub CheckHost(service As OnlineService, target As Uri, endpoint As Uri)
            If service IsNot Nothing AndAlso service.ConfiguredHost Then
                If target Is Nothing OrElse Not target.IsAbsoluteUri OrElse endpoint Is Nothing OrElse OriginOf(target) <> OriginOf(endpoint) OrElse
                   (target.Scheme <> Uri.UriSchemeHttps AndAlso Not (target.Scheme = Uri.UriSchemeHttp AndAlso target.IsLoopback)) Then
                    Throw New OnlineServiceBlockedException(service, OnlineBlockReason.UnexpectedHost, If(target?.IsAbsoluteUri, target.Host, String.Empty))
                End If
                Return
            End If
            If target Is Nothing OrElse Not target.IsAbsoluteUri OrElse
               Not String.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) OrElse
               Not service.AllowsHost(target.Host) Then
                Throw New OnlineServiceBlockedException(service, OnlineBlockReason.UnexpectedHost, If(target?.IsAbsoluteUri, target.Host, String.Empty))
            End If
        End Sub


        ' The HTTP client for one service, reused for the life of the app.
        Public Shared Function ClientFor(serviceId As String) As HttpClient
            Require(serviceId)
            SyncLock StateLock
                Dim client As HttpClient = Nothing
                If _clients.TryGetValue(serviceId, client) Then Return client
                client = New HttpClient(New GatedHandler(serviceId, SharedInner()), disposeHandler:=False) With {
                    .Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
                }
                _clients(serviceId) = client
                Return client
            End SyncLock
        End Function


        ' The compatible server's address and the address its key was added
        ' for, as the gate checks them.
        Friend Shared Function AssistantEndpointKeyTarget() As (Endpoint As Uri, KeyOrigin As String)
            SyncLock StateLock
                Return (_assistantEndpoint, _assistant.EndpointKeyOrigin)
            End SyncLock
        End Function


        ' A client of its own for a long download, such as an update package,
        ' or a model's answer; disposing it leaves the shared connections open.
        Friend Shared Function CreateClient(serviceId As String, timeout As TimeSpan) As HttpClient
            Require(serviceId)
            SyncLock StateLock
                Return New HttpClient(New GatedHandler(serviceId, SharedInner()), disposeHandler:=False) With {
                    .Timeout = timeout
                }
            End SyncLock
        End Function


        ' A client for Test Connection (#96): the AI assistant as set up in
        ' the open Preferences window, with a key added there and not yet
        ' saved. The gate still checks every hop: Work offline refuses it,
        ' a compatible server is reached only at the trial's address, and
        ' each key goes only as a header to its own service. Nothing about
        ' the trial is kept.
        Friend Shared Function CreateTrialClient(trial As AssistantTrial, timeout As TimeSpan) As HttpClient
            If trial Is Nothing OrElse trial.Connection Is Nothing Then Throw New ArgumentNullException(NameOf(trial))
            Dim service As OnlineService = Require(trial.Connection.ServiceId)
            If Not service.OffUntilTurnedOn Then Throw New ArgumentException("Only the AI assistant is tried before it is saved.", NameOf(trial))
            SyncLock StateLock
                Return New HttpClient(New GatedHandler(service.Id, SharedInner(), trial), disposeHandler:=False) With {
                    .Timeout = timeout
                }
            End SyncLock
        End Function


        ' Called under StateLock.
        Private Shared Function SharedInner() As HttpMessageHandler
            If _inner Is Nothing Then
                _inner = If(InnerHandlerFactory IsNot Nothing, InnerHandlerFactory(), DefaultInnerHandler())
            End If
            Return _inner
        End Function


        ' Redirects are never followed below the gate, and no cookies are
        ' kept, so one service's requests can't be linked to another's.
        Friend Shared Function DefaultInnerHandler() As SocketsHttpHandler
            Return New SocketsHttpHandler With {
                .AllowAutoRedirect = False,
                .UseCookies = False,
                .AutomaticDecompression = DecompressionMethods.All,
                .PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            }
        End Function


        ' For requests to this computer (a model here, #84): the same
        ' handler without a proxy, so the text can't be handed to one. Nothing
        ' when a test supplies the handler under the gate, which then gets
        ' every request.
        Friend Shared Function LoopbackInvoker() As HttpMessageInvoker
            SyncLock StateLock
                If InnerHandlerFactory IsNot Nothing Then Return Nothing
                If _loopback Is Nothing Then
                    Dim handler As SocketsHttpHandler = DefaultInnerHandler()
                    handler.UseProxy = False
                    _loopback = New HttpMessageInvoker(handler, disposeHandler:=True)
                End If
                Return _loopback
            End SyncLock
        End Function


        ' "PaperRoute-Tracker/0.9.0 (+https://github.com/JUhalt/PaperRoute-Tracker)":
        ' the version, never anything about the user.
        Public Shared Function UserAgent() As String
            Dim version As String = GetType(OnlineAccess).Assembly.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()?.InformationalVersion
            If String.IsNullOrWhiteSpace(version) Then version = GetType(OnlineAccess).Assembly.GetName().Version.ToString(3)
            Dim metadata As Integer = version.IndexOf("+"c)
            If metadata >= 0 Then version = version.Substring(0, metadata)
            Return "PaperRoute-Tracker/" & version & " (+https://github.com/JUhalt/PaperRoute-Tracker)"
        End Function


        Friend Shared Function KeyStore() As ProtectedKeyStore
            Return If(KeyStoreFactory IsNot Nothing, KeyStoreFactory(), New ProtectedKeyStore())
        End Function


        Public Shared Function BlockedMessage(service As OnlineService, reason As OnlineBlockReason, Optional host As String = Nothing) As String
            Dim name As String = If(service?.Name, "This service")
            Select Case reason
                Case OnlineBlockReason.WorkOffline
                    Return "Work offline is on, so PaperRoute didn't go online. To use " & name & ", turn off Work offline in Settings > Preferences > Online services."
                Case OnlineBlockReason.ServiceOff
                    Return name & " is turned off in Settings > Preferences > Online services, so PaperRoute didn't go online."
                Case OnlineBlockReason.NotTurnedOn
                    Return "The AI assistant is off. To use it, turn it on and choose a service in Settings > Preferences > AI assistant."
                Case Else
                    Return "PaperRoute stopped a request to " & If(String.IsNullOrWhiteSpace(host), "an unlisted address", host) & ", which isn't listed for " & name & ". Nothing was sent."
            End Select
        End Function


        ' A plain sentence for a failed request, instead of the raw exception.
        ' Whether the researcher added an OpenAlex key; False if it can't be told.
        Private Shared Function HasOpenAlexKey() As Boolean
            Try
                Return KeyStore().HasKey(ProtectedKeyStore.OpenAlex)
            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is Security.Cryptography.CryptographicException
                Return False
            End Try
        End Function


        Public Shared Function Describe(ex As Exception, serviceName As String) As String

            If ex Is Nothing Then Return String.Empty
            If TypeOf ex Is OnlineServiceBlockedException Then Return ex.Message

            If TypeOf ex Is TaskCanceledException AndAlso TypeOf ex.InnerException Is TimeoutException Then
                Return serviceName & " didn't answer in time. Try again later."
            End If

            Dim busy As OnlineServiceBusyException = TryCast(ex, OnlineServiceBusyException)
            If busy IsNot Nothing Then
                Dim keyHint As String = If(HasOpenAlexKey(), String.Empty, " A free OpenAlex key, added in Settings > Preferences > Online services, raises the allowance.")
                If busy.DailyAllowanceUsed Then
                    Dim reset As DateTime = DateTime.UtcNow.Date.AddDays(1)
                    Return serviceName & "'s free daily allowance is used up. It starts again at " &
                        reset.ToLocalTime().ToString("t", Globalization.CultureInfo.CurrentCulture) & "." &
                        If(serviceName = "OpenAlex", keyHint, String.Empty)
                End If
                Dim seconds As Integer = CInt(Math.Ceiling(If(busy.RetryAfter, TimeSpan.FromMinutes(1)).TotalSeconds))
                Return serviceName & " is busy and asked PaperRoute to wait about " & seconds.ToString(Globalization.CultureInfo.CurrentCulture) &
                    If(seconds = 1, " second.", " seconds.") & If(serviceName = "OpenAlex", keyHint, String.Empty)
            End If

            Dim request As HttpRequestException = TryCast(ex, HttpRequestException)
            If request IsNot Nothing Then
                Dim status As HttpStatusCode? = request.StatusCode
                If Not status.HasValue Then Return "PaperRoute couldn't reach " & serviceName & ". Check your internet connection, then try again."
                Dim code As Integer = CInt(status.Value)
                If code = 429 Then Return serviceName & " is busy and asked PaperRoute to wait. Try again in a minute."
                If code = 401 OrElse code = 403 Then Return serviceName & " refused the request. If you added a key, check it in Settings > Preferences > Online services."
                If code >= 500 Then Return serviceName & " had a problem on its side (HTTP " & code.ToString(Globalization.CultureInfo.InvariantCulture) & "). Try again later."
                Return serviceName & " answered with an error (HTTP " & code.ToString(Globalization.CultureInfo.InvariantCulture) & ")."
            End If

            Return ex.Message

        End Function


        Friend Shared Sub ResetForTests()
            SyncLock StateLock
                For Each client As HttpClient In _clients.Values
                    client.Dispose()
                Next
                _clients.Clear()
                _inner?.Dispose()
                _inner = Nothing
                _loopback?.Dispose()
                _loopback = Nothing
                _workOffline = False
                _turnedOff.Clear()
                _assistant = New AssistantSettings()
                _assistantEndpoint = Nothing
                _confirmedUses.Clear()
            End SyncLock
            InnerHandlerFactory = Nothing
            KeyStoreFactory = Nothing
            AssistantUseConfirmed = Nothing
        End Sub


        Private Shared Function Require(serviceId As String) As OnlineService
            Dim service As OnlineService = OnlineServiceCatalog.Find(serviceId)
            If service Is Nothing Then Throw New ArgumentException("Unknown online service: " & serviceId, NameOf(serviceId))
            Return service
        End Function

    End Class


    ' Checks every request, and every redirect hop, against the gate.
    Friend NotInheritable Class GatedHandler
        Inherits DelegatingHandler

        Private Const MaxRedirects As Integer = 3
        Private Const OpenAlexHost As String = "api.openalex.org"

        Private ReadOnly _serviceId As String

        ' Test Connection's setup, not yet saved (#96); Nothing otherwise.
        Private ReadOnly _trial As AssistantTrial

        Public Sub New(serviceId As String, inner As HttpMessageHandler, Optional trial As AssistantTrial = Nothing)
            MyBase.New(inner)
            _serviceId = serviceId
            _trial = trial
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)

            Dim service As OnlineService = OnlineServiceCatalog.Find(_serviceId)
            Dim current As HttpRequestMessage = request

            For hop As Integer = 0 To MaxRedirects
                If _trial Is Nothing Then
                    OnlineAccess.Check(_serviceId)
                    OnlineAccess.CheckHost(service, current.RequestUri)
                Else
                    OnlineAccess.CheckTrial(_serviceId)
                    OnlineAccess.CheckHost(service, current.RequestUri, _trial.Connection.Endpoint)
                End If
                Prepare(_serviceId, current, _trial)

                ' A request to this computer never goes through a proxy, so
                ' "nothing leaves this computer" holds whatever proxy is set.
                Dim direct As HttpMessageInvoker = If(current.RequestUri.IsLoopback, OnlineAccess.LoopbackInvoker(), Nothing)
                Dim response As HttpResponseMessage =
                    If(direct IsNot Nothing,
                       Await direct.SendAsync(current, cancellationToken).ConfigureAwait(False),
                       Await MyBase.SendAsync(current, cancellationToken).ConfigureAwait(False))
                Dim location As Uri = response.Headers.Location
                If Not IsRedirect(response.StatusCode) OrElse location Is Nothing OrElse hop = MaxRedirects OrElse
                   (current.Method <> HttpMethod.Get AndAlso current.Method <> HttpMethod.Head) Then
                    Return response
                End If

                Dim target As Uri = If(location.IsAbsoluteUri, location, New Uri(current.RequestUri, location))
                Dim following As New HttpRequestMessage(current.Method, target)
                For Each header As KeyValuePair(Of String, IEnumerable(Of String)) In current.Headers
                    If Not SecretHeaders.Contains(header.Key) Then
                        following.Headers.TryAddWithoutValidation(header.Key, header.Value)
                    End If
                Next
                response.Dispose()
                If current IsNot request Then current.Dispose()
                current = following
            Next

            Throw New InvalidOperationException("Too many redirects.")

        End Function

        ' Headers that carry a key: never copied to a redirect, and set only here.
        Private Shared ReadOnly SecretHeaders As New HashSet(Of String)({"Authorization", "x-api-key", "api-key"}, StringComparer.OrdinalIgnoreCase)

        ' All an AI assistant request carries besides its content and the key
        ' (#84): anything else, such as an SDK's system details or headers
        ' from environment variables, is dropped.
        Private Shared ReadOnly AssistantHeaders As New HashSet(Of String)({"Accept", "anthropic-version", "anthropic-beta"}, StringComparer.OrdinalIgnoreCase)

        Friend Shared Sub Prepare(request As HttpRequestMessage)
            Prepare(Nothing, request)
        End Sub

        ' One user agent, and each key only to its own service, only as a
        ' header: the OpenAlex key to OpenAlex, the Claude key to Anthropic,
        ' and a compatible server's key only to the address it was added for.
        ' A Test Connection trial (#96) brings the key added in Preferences,
        ' or uses the stored one, under the same rules.
        Friend Shared Sub Prepare(serviceId As String, request As HttpRequestMessage, Optional trial As AssistantTrial = Nothing)
            Dim service As OnlineService = If(serviceId Is Nothing, Nothing, OnlineServiceCatalog.Find(serviceId))
            If service IsNot Nothing AndAlso service.OffUntilTurnedOn Then
                For Each name As String In request.Headers.Select(Function(header) header.Key).ToList()
                    If Not AssistantHeaders.Contains(name) Then request.Headers.Remove(name)
                Next
            End If
            request.Headers.UserAgent.Clear()
            request.Headers.UserAgent.ParseAdd(OnlineAccess.UserAgent())
            request.Headers.Authorization = Nothing
            request.Headers.Remove("x-api-key")
            request.Headers.Remove("api-key")
            Dim host As String = request.RequestUri.Host
            If String.Equals(host, OpenAlexHost, StringComparison.OrdinalIgnoreCase) Then
                Dim key As String = OnlineAccess.KeyStore().Load(ProtectedKeyStore.OpenAlex)
                If Not String.IsNullOrEmpty(key) Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", key)
            ElseIf serviceId = OnlineServiceCatalog.AssistantClaude AndAlso String.Equals(host, OnlineAccess.AnthropicHost, StringComparison.OrdinalIgnoreCase) AndAlso
                   request.RequestUri.Scheme = Uri.UriSchemeHttps Then
                Dim key As String = KeyFor(trial, Function() OnlineAccess.KeyStore().Load(ProtectedKeyStore.Anthropic))
                If Not String.IsNullOrEmpty(key) Then request.Headers.TryAddWithoutValidation("x-api-key", key)
            ElseIf serviceId = OnlineServiceCatalog.AssistantCompatible Then
                Dim endpoint As Uri = If(trial IsNot Nothing, trial.Connection?.Endpoint, OnlineAccess.AssistantEndpointKeyTarget().Endpoint)
                Dim origin As String = OnlineAccess.OriginOf(endpoint)
                ' The key is stored with the address it was added for, in one
                ' encrypted file, so it is read only for that address.
                If origin.Length > 0 AndAlso OnlineAccess.OriginOf(request.RequestUri) = origin Then
                    Dim key As String = KeyFor(trial, Function() OnlineAccess.KeyStore().LoadFor(ProtectedKeyStore.AssistantEndpoint, origin))
                    If Not String.IsNullOrEmpty(key) Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", key)
                End If
            End If
        End Sub

        ' The stored key; for a trial, the key added in Preferences, else
        ' the stored one only when the trial says it will be kept.
        Private Shared Function KeyFor(trial As AssistantTrial, stored As Func(Of String)) As String
            If trial Is Nothing Then Return stored()
            If trial.PendingKey IsNot Nothing Then Return trial.PendingKey
            Return If(trial.UseStoredKey, stored(), Nothing)
        End Function

        Private Shared Function IsRedirect(status As HttpStatusCode) As Boolean
            Dim code As Integer = CInt(status)
            Return code = 301 OrElse code = 302 OrElse code = 303 OrElse code = 307 OrElse code = 308
        End Function

    End Class

End Namespace
