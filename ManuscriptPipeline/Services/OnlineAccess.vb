Imports System
Imports System.Collections.Generic
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
    End Enum


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

        ' Test seams: the handler under the gate, and the key store.
        Friend Shared InnerHandlerFactory As Func(Of HttpMessageHandler) = Nothing
        Friend Shared KeyStoreFactory As Func(Of ProtectedKeyStore) = Nothing


        Public Shared Sub Configure(settings As OnlineServicesSettings)
            SyncLock StateLock
                _workOffline = settings IsNot Nothing AndAlso settings.WorkOffline
                _turnedOff.Clear()
                For Each id As String In If(settings?.TurnedOff, New List(Of String)())
                    If Not String.IsNullOrWhiteSpace(id) Then _turnedOff.Add(id.Trim())
                Next
            End SyncLock
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
            SyncLock StateLock
                If _workOffline Then Return OnlineBlockReason.WorkOffline
                If _turnedOff.Contains(serviceId) Then Return OnlineBlockReason.ServiceOff
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


        ' Only https, and only a host the service lists.
        Friend Shared Sub CheckHost(service As OnlineService, target As Uri)
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


        ' A client of its own for a long download, such as an update package;
        ' disposing it leaves the shared connections open.
        Friend Shared Function CreateClient(serviceId As String, timeout As TimeSpan) As HttpClient
            Require(serviceId)
            SyncLock StateLock
                Return New HttpClient(New GatedHandler(serviceId, SharedInner()), disposeHandler:=False) With {
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
                Case Else
                    Return "PaperRoute stopped a request to " & If(String.IsNullOrWhiteSpace(host), "an unlisted address", host) & ", which isn't listed for " & name & ". Nothing was sent."
            End Select
        End Function


        ' A plain sentence for a failed request, instead of the raw exception.
        Public Shared Function Describe(ex As Exception, serviceName As String) As String

            If ex Is Nothing Then Return String.Empty
            If TypeOf ex Is OnlineServiceBlockedException Then Return ex.Message

            If TypeOf ex Is TaskCanceledException AndAlso TypeOf ex.InnerException Is TimeoutException Then
                Return serviceName & " didn't answer in time. Try again later."
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
                _workOffline = False
                _turnedOff.Clear()
            End SyncLock
            InnerHandlerFactory = Nothing
            KeyStoreFactory = Nothing
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

        Public Sub New(serviceId As String, inner As HttpMessageHandler)
            MyBase.New(inner)
            _serviceId = serviceId
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)

            Dim service As OnlineService = OnlineServiceCatalog.Find(_serviceId)
            Dim current As HttpRequestMessage = request

            For hop As Integer = 0 To MaxRedirects
                OnlineAccess.Check(_serviceId)
                OnlineAccess.CheckHost(service, current.RequestUri)
                Prepare(current)

                Dim response As HttpResponseMessage = Await MyBase.SendAsync(current, cancellationToken).ConfigureAwait(False)
                Dim location As Uri = response.Headers.Location
                If Not IsRedirect(response.StatusCode) OrElse location Is Nothing OrElse hop = MaxRedirects OrElse
                   (current.Method <> HttpMethod.Get AndAlso current.Method <> HttpMethod.Head) Then
                    Return response
                End If

                Dim target As Uri = If(location.IsAbsoluteUri, location, New Uri(current.RequestUri, location))
                Dim following As New HttpRequestMessage(current.Method, target)
                For Each header As KeyValuePair(Of String, IEnumerable(Of String)) In current.Headers
                    If Not String.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) Then
                        following.Headers.TryAddWithoutValidation(header.Key, header.Value)
                    End If
                Next
                response.Dispose()
                If current IsNot request Then current.Dispose()
                current = following
            Next

            Throw New InvalidOperationException("Too many redirects.")

        End Function

        ' One user agent; the OpenAlex key only to OpenAlex, only as a header.
        Friend Shared Sub Prepare(request As HttpRequestMessage)
            request.Headers.UserAgent.Clear()
            request.Headers.UserAgent.ParseAdd(OnlineAccess.UserAgent())
            request.Headers.Authorization = Nothing
            If String.Equals(request.RequestUri.Host, OpenAlexHost, StringComparison.OrdinalIgnoreCase) Then
                Dim key As String = OnlineAccess.KeyStore().Load(ProtectedKeyStore.OpenAlex)
                If Not String.IsNullOrEmpty(key) Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", key)
            End If
        End Sub

        Private Shared Function IsRedirect(status As HttpStatusCode) As Boolean
            Dim code As Integer = CInt(status)
            Return code = 301 OrElse code = 302 OrElse code = 303 OrElse code = 307 OrElse code = 308
        End Function

    End Class

End Namespace
