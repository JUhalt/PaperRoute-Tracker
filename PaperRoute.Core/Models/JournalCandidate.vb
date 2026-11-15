Imports System
Imports System.Collections.Generic

Namespace Models

    ' Where a shortlisted journal stands for one manuscript (#65).
    Public Enum CandidateStatus
        Considering
        Preferred
        Backup
        RuledOut
    End Enum


    ' A journal on one manuscript's shortlist (#65), with the researcher's
    ' reasons and the choosing-a-journal checks they have answered (#89).
    ' Whether it was submitted to is read from the submissions, never
    ' stored here.
    Public Class JournalCandidate

        Public Property Id As Guid = Guid.NewGuid()

        Public Property JournalName As String = String.Empty

        ' The Journal Library record, when the name matches one.
        Public Property JournalId As Guid? = Nothing

        Public Property Status As CandidateStatus = CandidateStatus.Considering

        Public Property Notes As String = String.Empty

        ' Ids of the checks answered yes (JournalChoiceGuide).
        Public Property Checks As List(Of String) = New List(Of String)()

        Public Property AddedDate As DateTime = DateTime.Today

        ' Why the journal was suggested (#88), when it came from Find
        ' Journals; Nothing for a journal added by hand.
        Public Property Evidence As CandidateEvidence = Nothing

    End Class


    ' The evidence behind a suggested journal (#88): how many recent articles
    ' matching the researcher's keywords it published, with examples. Counts
    ' are evidence to read, never a score or a prediction.
    Public Class CandidateEvidence

        ' "OpenAlex", or "Example" in the example library.
        Public Property Source As String = String.Empty

        ' The journal's OpenAlex id ("S…") and ISSNs.
        Public Property OpenAlexId As String = String.Empty

        Public Property Issns As List(Of String) = New List(Of String)()

        ' The journal's publisher, main topics (up to three), and homepage,
        ' as the source listed them (#96).
        Public Property Publisher As String = String.Empty

        Public Property Topics As List(Of String) = New List(Of String)()

        Public Property HomepageUrl As String = String.Empty

        ' The keywords searched, and whether articles had to match all.
        Public Property Keywords As List(Of String) = New List(Of String)()

        Public Property MatchAll As Boolean = True

        Public Property SinceDate As DateTime?

        Public Property MatchingArticles As Long

        ' All of its articles in the same years, when known.
        Public Property AllArticles As Long?

        Public Property Examples As List(Of EvidenceExample) = New List(Of EvidenceExample)()

        Public Property RetrievedUtc As DateTime?

    End Class


    ' One recent matching article, as an example.
    Public Class EvidenceExample

        Public Property Title As String = String.Empty

        Public Property Year As Integer?

        ' Without the https://doi.org/ prefix; may be blank.
        Public Property Doi As String = String.Empty

    End Class

End Namespace
