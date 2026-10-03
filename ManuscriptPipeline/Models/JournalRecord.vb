Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class JournalRecord

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Name As String = String.Empty

        Public Property Publisher As String = String.Empty

        Public Property HomepageUrl As String = String.Empty

        Public Property SubmissionPortalUrl As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property IsFavorite As Boolean = False

        ' Shown as "Watch list" (#96): it only sorts and labels the Journals
        ' list. The stored name stays for existing libraries; a manuscript's
        ' journal shortlist (#65) is Manuscript.JournalShortlist.
        Public Property IsShortlisted As Boolean = False

        Public Property ReadinessChecklistTemplate As List(Of JournalChecklistTemplateItem) =
            New List(Of JournalChecklistTemplateItem)()

        ' Journal facts (#87, Schema 9). ISSNs are "NNNN-NNNC", at most four.
        Public Property Issns As List(Of String) = New List(Of String)()

        Public Property AimsScopeUrl As String = String.Empty

        Public Property AuthorInstructionsUrl As String = String.Empty

        Public Property EditorialBoardUrl As String = String.Empty

        ' Ids in the open indexes, from the last lookup.
        Public Property OpenAlexId As String = String.Empty

        Public Property DoajId As String = String.Empty

        ' Fields a lookup filled, keyed by JournalFactsService field names.
        Public Property FieldSources As Dictionary(Of String, FieldSource) =
            New Dictionary(Of String, FieldSource)(StringComparer.Ordinal)

        Public Property Facts As List(Of JournalFact) = New List(Of JournalFact)()


        Public ReadOnly Property DisplayName As String
            Get
                Dim prefix As String = String.Empty

                If IsFavorite Then
                    prefix &= "★ "
                End If

                If IsShortlisted Then
                    prefix &= "[Watch list] "
                End If

                If String.IsNullOrWhiteSpace(Name) Then
                    Return prefix & "(Unnamed journal)"
                End If

                Return prefix & Name.Trim()
            End Get
        End Property


        Public Overrides Function ToString() As String
            Return DisplayName
        End Function

    End Class

End Namespace
