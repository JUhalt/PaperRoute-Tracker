Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' What the AI assistant's tests share (#84): a decision letter and what a
' model might answer to it, the settings and trials of both services,
' their answers as the services send them, and a network under the gate
' that records every request. The assistant, settings and letter tests
' use them. It holds no tests, and it names nothing of the app, so either
' test project can compile it.
Friend NotInheritable Class AssistantTestSupport

    Private Sub New()
    End Sub


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

    Friend Shared Function Answer(status As HttpStatusCode, body As String) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
    End Function

    ' Anthropic's model list, and one model, as the Models API gives them.
    Friend Shared Function ClaudeModelList(ParamArray ids As String()) As String
        Return "{""data"":[" & String.Join(",", ids.Select(Function(id) ClaudeModelEntry(id))) & "],""has_more"":false,""first_id"":""" & ids.First() & """,""last_id"":""" & ids.Last() & """}"
    End Function

    Friend Shared Function ClaudeModelEntry(id As String) As String
        Return "{""type"":""model"",""id"":""" & id & """,""display_name"":""" & id & """,""created_at"":""2026-08-01T00:00:00Z"",""max_input_tokens"":200000,""max_tokens"":64000}"
    End Function

    ' An OpenAI-compatible server's model list.
    Friend Shared Function ServerModelList(ParamArray ids As String()) As String
        Return "{""object"":""list"",""data"":[" & String.Join(",", ids.Select(Function(id) "{""id"":" & JsonSerializer.Serialize(id) & ",""object"":""model"",""owned_by"":""library""}")) & "]}"
    End Function

    ' The AI assistant as typed in Preferences, not saved.
    Friend Shared Function ClaudeTrial(model As String, Optional pendingKey As String = Nothing, Optional useStoredKey As Boolean = False) As AssistantTrial
        Return New AssistantTrial With {
            .Connection = OnlineAccess.ConnectionFor(New AssistantSettings With {.Provider = AssistantProvider.Claude, .ClaudeModel = model}),
            .PendingKey = pendingKey,
            .UseStoredKey = useStoredKey
        }
    End Function

    Friend Shared Function ServerTrial(endpoint As String, model As String, Optional pendingKey As String = Nothing, Optional useStoredKey As Boolean = False) As AssistantTrial
        Return New AssistantTrial With {
            .Connection = OnlineAccess.ConnectionFor(New AssistantSettings With {.Provider = AssistantProvider.Compatible, .Endpoint = endpoint, .EndpointModel = model}),
            .PendingKey = pendingKey,
            .UseStoredKey = useStoredKey
        }
    End Function


    Friend NotInheritable Class SentRequest
        Public Property Method As HttpMethod
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

        ' A request this returns True for waits until it is cancelled, like
        ' a server that never answers.
        Public Property Hold As Func(Of SentRequest, Boolean)

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim sent As New SentRequest With {.Method = request.Method, .Uri = request.RequestUri}
            For Each header In request.Headers
                sent.Headers(header.Key) = String.Join(",", header.Value)
            Next
            If request.Content IsNot Nothing Then sent.Body = Await request.Content.ReadAsStringAsync(cancellationToken)
            SyncLock Requests
                Requests.Add(sent)
            End SyncLock
            If Hold IsNot Nothing AndAlso Hold(sent) Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            Dim response As HttpResponseMessage = Respond(sent)
            response.RequestMessage = request
            Return response
        End Function
    End Class

End Class
