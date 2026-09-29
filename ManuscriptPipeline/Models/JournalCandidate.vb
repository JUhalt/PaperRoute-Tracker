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

    End Class

End Namespace
