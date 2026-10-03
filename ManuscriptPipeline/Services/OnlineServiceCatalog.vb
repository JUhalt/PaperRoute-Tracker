Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text

Namespace Services

    ' One online service PaperRoute can contact (#86): what it contacts,
    ' what it sends, and when. The Online services page, the User Guide's
    ' "What PaperRoute sends, and when", and the gate's host list all read
    ' this one description.
    Public NotInheritable Class OnlineService

        Public Sub New(id As String, name As String, hosts As String(), sends As String, whenUsed As String,
                       Optional offUntilTurnedOn As Boolean = False, Optional contacts As String = Nothing, Optional configuredHost As Boolean = False)
            Me.Id = id
            Me.Name = name
            Me.Hosts = hosts
            Me.Sends = sends
            Me.WhenUsed = whenUsed
            Me.OffUntilTurnedOn = offUntilTurnedOn
            Me.Contacts = If(contacts, String.Join(", ", hosts))
            Me.ConfiguredHost = configuredHost
        End Sub

        Public ReadOnly Property Id As String

        Public ReadOnly Property Name As String

        ' Every host a request may reach, redirects included.
        Public ReadOnly Property Hosts As IReadOnlyList(Of String)

        Public ReadOnly Property Sends As String

        Public ReadOnly Property WhenUsed As String

        ' The AI assistant (#84): off until the researcher turns it on,
        ' where every other service is on until turned off.
        Public ReadOnly Property OffUntilTurnedOn As Boolean

        ' What the guide and the Online services page say it contacts.
        Public ReadOnly Property Contacts As String

        ' The address is the one the researcher sets (a compatible server),
        ' not a fixed list; the gate compares it with that address.
        Public ReadOnly Property ConfiguredHost As Boolean

        Public Function AllowsHost(host As String) As Boolean
            Return Hosts.Any(Function(item) String.Equals(item, host, StringComparison.OrdinalIgnoreCase))
        End Function

    End Class


    Public NotInheritable Class OnlineServiceCatalog

        Private Sub New()
        End Sub

        Public Const Updates As String = "updates"
        Public Const Crossref As String = "crossref"
        Public Const PublicationCheck As String = "publication-check"
        Public Const OrcidImport As String = "orcid-import"
        Public Const JournalFacts As String = "journal-facts"
        Public Const JournalSuggestions As String = "journal-suggestions"
        Public Const Citations As String = "citations"
        Public Const AssistantClaude As String = "assistant-claude"
        Public Const AssistantCompatible As String = "assistant-compatible"

        Private Const AssistantSends As String = "Only what an AI assistant window shows you before sending: a decision letter you paste; a journal's author instructions you paste, with the article type you enter; one reviewer comment with its reviewer label and your planned action; or a manuscript's title, abstract, keywords, type of work, and target journal with its Journal Library facts. Test Connection in Preferences sends only the key, if you added one, to list the models."
        Private Const AssistantWhen As String = "Read Decision Letter..., Add from Letter..., Read Author Instructions..., Suggest a Starting Point..., Draft Cover Letter..., and Test Connection, after you turn on the AI assistant in Preferences."

        ' In the order the Online services page lists them.
        Public Shared ReadOnly Property Services As IReadOnlyList(Of OnlineService) = {
            New OnlineService(Updates, "Update check",
                              {"api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"},
                              "Nothing about you or your library; it reads the list of PaperRoute releases.",
                              "At startup, if automatic checks are on, and Check for Updates."),
            New OnlineService(Crossref, "DOI lookup (Crossref)",
                              {"api.crossref.org"},
                              "A DOI.",
                              "DOI & Crossref Metadata on a manuscript page, and Fill Blanks from Crossref."),
            New OnlineService(PublicationCheck, "Publication check",
                              {"api.crossref.org", "orcid.org", "pub.orcid.org"},
                              "The DOIs and titles of the manuscripts you check, and your ORCID iD if you include it.",
                              "Check for Publications."),
            New OnlineService(OrcidImport, "ORCID import",
                              {"orcid.org", "pub.orcid.org"},
                              "An ORCID iD.",
                              "ORCID... in Library > Authors & Affiliations."),
            New OnlineService(JournalFacts, "Journal facts (DOAJ and OpenAlex)",
                              {"doaj.org", "api.openalex.org"},
                              "A journal's ISSNs, or a name you type to find a journal and the id of the one you pick.",
                              "Look Up Facts... on the Journals page."),
            New OnlineService(JournalSuggestions, "Find journals (OpenAlex)",
                              {"api.openalex.org"},
                              "The keywords you review, a start date, and the ids of the journals found.",
                              "Find Journals... on a manuscript's journal shortlist."),
            New OnlineService(Citations, "Your citations (ORCID and OpenAlex)",
                              {"pub.orcid.org", "api.openalex.org"},
                              "Your ORCID iD to ORCID, and the DOIs of your works to OpenAlex; your iD to OpenAlex only if you choose.",
                              "Update from OpenAlex... on Insights > Your Citations."),
            New OnlineService(AssistantClaude, "AI assistant: Claude (Anthropic)",
                              {"api.anthropic.com"},
                              AssistantSends, AssistantWhen,
                              offUntilTurnedOn:=True),
            New OnlineService(AssistantCompatible, "AI assistant: another server or a model on this computer",
                              Array.Empty(Of String)(),
                              AssistantSends, AssistantWhen,
                              offUntilTurnedOn:=True,
                              contacts:="The address you set in Preferences: http only on this computer, https elsewhere",
                              configuredHost:=True)
        }


        Public Shared Function Find(id As String) As OnlineService
            Return Services.FirstOrDefault(Function(item) String.Equals(item.Id, id, StringComparison.Ordinal))
        End Function


        ' The User Guide's table, so the guide and the app never disagree.
        Public Shared Function ToMarkdownTable() As String
            Dim table As New StringBuilder()
            table.AppendLine("| Service | Contacts | Sends | When |")
            table.AppendLine("| --- | --- | --- | --- |")
            For Each service As OnlineService In Services
                table.AppendLine("| " & service.Name & " | " & service.Contacts & " | " & service.Sends & " | " & service.WhenUsed & " |")
            Next
            Return table.ToString().TrimEnd()
        End Function

    End Class

End Namespace
