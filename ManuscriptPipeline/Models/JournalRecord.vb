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

        Public Property IsShortlisted As Boolean = False

        Public Property ReadinessChecklistTemplate As List(Of JournalChecklistTemplateItem) =
            New List(Of JournalChecklistTemplateItem)()


        Public ReadOnly Property DisplayName As String
            Get
                Dim prefix As String = String.Empty

                If IsFavorite Then
                    prefix &= "★ "
                End If

                If IsShortlisted Then
                    prefix &= "[Shortlist] "
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
