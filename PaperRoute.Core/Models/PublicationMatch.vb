Imports System

Namespace Models

    ' Where a possible publication was found.
    Public Enum PublicationMatchSource
        ' The manuscript's own DOI now resolves to a published work.
        Doi
        ' The manuscript's preprint DOI names its published version.
        Preprint
        ' A Crossref search by title.
        Title
        ' A work in an ORCID record.
        Orcid
    End Enum


    Public Enum PublicationMatchStatus
        ' Found and not yet reviewed; listed on the Deadlines page.
        Pending
        ' Not this manuscript. Later checks never show it again.
        Ignored
        ' The manuscript was marked published from this match.
        Confirmed
    End Enum


    ' A possible publication found by a user-initiated check (#61). It is a
    ' finding, not a status: the manuscript changes only through Mark
    ' Published.
    Public Class PublicationMatch

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Doi As String = String.Empty

        Public Property Title As String = String.Empty

        Public Property Journal As String = String.Empty

        Public Property PublishedDate As DateTime? = Nothing

        Public Property Url As String = String.Empty

        Public Property Publisher As String = String.Empty

        Public Property Volume As String = String.Empty

        Public Property Issue As String = String.Empty

        Public Property Pages As String = String.Empty

        Public Property Source As PublicationMatchSource = PublicationMatchSource.Title

        Public Property Status As PublicationMatchStatus = PublicationMatchStatus.Pending

        Public Property FoundAtUtc As DateTime = DateTime.UtcNow

        Public Property ReviewedAtUtc As DateTime? = Nothing

    End Class

End Namespace
