Imports System.Collections.Generic

Namespace Models

    Public Enum AssistantProvider
        ' Claude, with the researcher's own Anthropic key.
        Claude
        ' Any server that speaks the OpenAI chat completions protocol: a model
        ' on this computer (Ollama, LM Studio) or another service over https.
        Compatible
    End Enum


    ' The optional AI assistant (#84). Unlike the other online services it
    ' is off until the researcher turns it on and chooses a provider, and
    ' Work offline turns it off too.
    Public Class AssistantSettings

        Public Property Enabled As Boolean = False

        Public Property Provider As AssistantProvider = AssistantProvider.Claude

        ' A Claude model id, such as claude-opus-5-5.
        Public Property ClaudeModel As String = "claude-opus-5-5"

        ' The compatible server's address up to /v1, such as
        ' http://localhost:11434/v1. http is allowed only on this computer.
        Public Property Endpoint As String = String.Empty

        Public Property EndpointModel As String = String.Empty

        ' The address (scheme, host, and port) the endpoint key was added
        ' for; the key is sent only there, so editing the address never
        ' sends it somewhere new.
        Public Property EndpointKeyOrigin As String = String.Empty

        ' Features the researcher agreed to send to a service without being
        ' asked again: "feature|origin", such as
        ' "decision-letter|https://api.anthropic.com".
        Public Property ConfirmedUses As List(Of String) = New List(Of String)()

    End Class

End Namespace
