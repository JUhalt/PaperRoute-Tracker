Imports System

Namespace Models

    Public Class AuthorRecord

        Public Property Id As Guid =
            Guid.NewGuid()

        Public Property GivenName As String =
            String.Empty

        Public Property MiddleName As String =
            String.Empty

        Public Property FamilyName As String =
            String.Empty

        Public Property Suffix As String =
            String.Empty

        Public Property DisplayNameOverride As String =
            String.Empty

        Public Property Orcid As String =
            String.Empty

        ' A successful anonymous/public registry lookup verifies that the
        ' ORCID iD exists. It does NOT mean the record holder authenticated
        ' the iD to PaperRoute.
        Public Property OrcidLastCheckedUtc As DateTime? =
            Nothing

        Public Property Notes As String =
            String.Empty

        Public Property IsMe As Boolean =
            False


        Public ReadOnly Property DisplayName As String
            Get

                If Not String.IsNullOrWhiteSpace(
                    DisplayNameOverride
                ) Then

                    Return DisplayNameOverride.Trim()

                End If

                Dim parts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(
                    GivenName
                ) Then

                    parts.Add(
                        GivenName.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    MiddleName
                ) Then

                    parts.Add(
                        MiddleName.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    FamilyName
                ) Then

                    parts.Add(
                        FamilyName.Trim()
                    )

                End If

                If Not String.IsNullOrWhiteSpace(
                    Suffix
                ) Then

                    parts.Add(
                        Suffix.Trim()
                    )

                End If

                Dim result As String =
                    String.Join(
                        " ",
                        parts
                    ).Trim()

                If String.IsNullOrWhiteSpace(
                    result
                ) Then

                    Return "(Unnamed author)"

                End If

                Return result

            End Get
        End Property


        Public Overrides Function ToString() As String

            Dim result As String =
                DisplayName

            If IsMe Then

                result &=
                    "  •  Me"

            End If

            Return result

        End Function

    End Class

End Namespace
