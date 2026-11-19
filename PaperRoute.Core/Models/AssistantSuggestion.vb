Imports System

Namespace Models

    ' Where a record began, when it began as an AI assistant suggestion (#84)
    ' the researcher accepted: which feature, which service and model, when,
    ' and the text it came from. Nothing for anything entered by hand.
    Public Class AssistantSuggestion

        ' "decision-letter", "draft-response", "cover-letter", or
        ' "author-instructions".
        Public Property Feature As String = String.Empty

        ' "Claude" or "OpenAI-compatible server": never an address or a key.
        Public Property Provider As String = String.Empty

        Public Property Model As String = String.Empty

        Public Property SuggestedUtc As DateTime?

        ' The source the suggestion was made from, such as the sentence of
        ' the decision letter a comment was found in.
        Public Property SourceText As String = String.Empty

    End Class

End Namespace
