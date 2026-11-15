Imports System

Namespace Models

    Public Class AffiliationRecord

        Public Property Id As Guid =
            Guid.NewGuid()

        Public Property Institution As String =
            String.Empty

        Public Property Department As String =
            String.Empty

        Public Property City As String =
            String.Empty

        Public Property Region As String =
            String.Empty

        Public Property Country As String =
            String.Empty

        Public Property Notes As String =
            String.Empty


        Public ReadOnly Property DisplayName As String
            Get

                Dim parts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(
                    Department
                ) Then

                    parts.Add(
                        Department.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    Institution
                ) Then

                    parts.Add(
                        Institution.Trim()
                    )

                End If

                Dim locationParts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(
                    City
                ) Then

                    locationParts.Add(
                        City.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    Region
                ) Then

                    locationParts.Add(
                        Region.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    Country
                ) Then

                    locationParts.Add(
                        Country.Trim()
                    )

                End If

                If locationParts.Count > 0 Then

                    parts.Add(
                        String.Join(
                            ", ",
                            locationParts
                        )
                    )

                End If

                Dim result As String =
                    String.Join(
                        " — ",
                        parts
                    ).Trim()

                If String.IsNullOrWhiteSpace(
                    result
                ) Then

                    Return "(Unnamed affiliation)"

                End If

                Return result

            End Get
        End Property


        Public Overrides Function ToString() As String

            Return DisplayName

        End Function

    End Class

End Namespace
