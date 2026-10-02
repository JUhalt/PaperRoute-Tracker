Imports System

Namespace Forms

    ' Pastes a free personal OpenAlex key (#86). PaperRoute never ships a
    ' key of its own; the user's key is kept encrypted on this computer and
    ' sent only to api.openalex.org, in a header.
    Public Class OpenAlexKeyForm
        Inherits ApiKeyForm

        Public Const KeyPage As String = "https://openalex.org/settings/api"


        Public Sub New()
            MyBase.New(
                "Add OpenAlex Key",
                "A free OpenAlex key raises OpenAlex's daily allowance tenfold. Sign in at openalex.org, copy your key from Settings > API, and paste it here." &
                Environment.NewLine & Environment.NewLine &
                "PaperRoute keeps it encrypted on this computer, for your Windows account only, and sends it only to api.openalex.org. It is never in your backups or exports.",
                KeyPage,
                "Open openalex.org/settings/api",
                "OpenAlex key",
                "an OpenAlex key")
        End Sub


        ' Why a pasted value isn't accepted; nothing while a short key is
        ' still being typed.
        Friend Overloads Shared Function HintFor(value As String) As String
            Return ApiKeyForm.HintFor(value, "an OpenAlex key")
        End Function

    End Class

End Namespace
