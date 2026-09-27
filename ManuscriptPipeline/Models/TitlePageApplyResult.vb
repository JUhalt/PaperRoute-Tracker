Namespace Models

    Public Class TitlePageApplyResult

        Public Property AuthorsMatched As Integer

        Public Property AuthorsCreated As Integer

        Public Property AffiliationsCreated As Integer

        Public ReadOnly Property LibraryChanged As Boolean
            Get
                Return AuthorsCreated > 0 OrElse AffiliationsCreated > 0
            End Get
        End Property

    End Class

End Namespace
