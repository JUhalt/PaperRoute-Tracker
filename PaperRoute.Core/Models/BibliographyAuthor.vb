Namespace Models

    Public Class BibliographyAuthor

        Public Property GivenName As String = String.Empty
        Public Property MiddleName As String = String.Empty
        Public Property FamilyName As String = String.Empty
        Public Property Suffix As String = String.Empty
        Public Property DisplayNameOverride As String = String.Empty

        Public ReadOnly Property DisplayName As String
            Get
                If Not String.IsNullOrWhiteSpace(DisplayNameOverride) Then
                    Return DisplayNameOverride.Trim()
                End If

                Dim parts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(GivenName) Then
                    parts.Add(GivenName.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(MiddleName) Then
                    parts.Add(MiddleName.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(FamilyName) Then
                    parts.Add(FamilyName.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(Suffix) Then
                    parts.Add(Suffix.Trim())
                End If

                Return String.Join(" ", parts)
            End Get
        End Property

    End Class

End Namespace
