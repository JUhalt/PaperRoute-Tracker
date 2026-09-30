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

        Public Sub New(id As String, name As String, hosts As String(), sends As String, whenUsed As String)
            Me.Id = id
            Me.Name = name
            Me.Hosts = hosts
            Me.Sends = sends
            Me.WhenUsed = whenUsed
        End Sub

        Public ReadOnly Property Id As String

        Public ReadOnly Property Name As String

        ' Every host a request may reach, redirects included.
        Public ReadOnly Property Hosts As IReadOnlyList(Of String)

        Public ReadOnly Property Sends As String

        Public ReadOnly Property WhenUsed As String

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
                              "Look Up Facts... on the Journals page.")
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
                table.AppendLine("| " & service.Name & " | " & String.Join(", ", service.Hosts) & " | " & service.Sends & " | " & service.WhenUsed & " |")
            Next
            Return table.ToString().TrimEnd()
        End Function

    End Class

End Namespace
