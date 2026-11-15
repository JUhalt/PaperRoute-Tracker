Imports System

Namespace Models

    Public Class OrcidAffiliationSuggestion

        Public Property Institution As String =
            String.Empty

        Public Property Department As String =
            String.Empty

        Public Property RoleTitle As String =
            String.Empty

        Public Property City As String =
            String.Empty

        Public Property Region As String =
            String.Empty

        Public Property Country As String =
            String.Empty

        Public Property StartDate As DateTime? =
            Nothing

        Public Property EndDate As DateTime? =
            Nothing


        Public ReadOnly Property IsCurrent As Boolean
            Get
                Return Not EndDate.HasValue
            End Get
        End Property


        Public ReadOnly Property DisplayName As String
            Get

                Dim parts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(Department) Then
                    parts.Add(Department.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(Institution) Then
                    parts.Add(Institution.Trim())
                End If

                Dim location As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(City) Then
                    location.Add(City.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(Region) Then
                    location.Add(Region.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(Country) Then
                    location.Add(Country.Trim())
                End If

                If location.Count > 0 Then
                    parts.Add(String.Join(", ", location))
                End If

                Return String.Join(" — ", parts)

            End Get
        End Property

    End Class

End Namespace
