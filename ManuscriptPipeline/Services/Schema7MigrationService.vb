Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    ' Schemas 7 and 8 add fields that default to empty: remembered
    ' publication matches (#61), then work types, tags, and tag colors
    ' (#64). Upgrading validates the whole library with the current rules
    ' and then advances only the marker, keeping the old one as
    ' schema.vN.bak. The older JSON, managed files, and backups stay
    ' byte-for-byte, and the new marker keeps an older build from opening
    ' the library and silently dropping the new fields.
    Friend NotInheritable Class MarkerOnlyMigration
        Private Sub New()
        End Sub

        Friend Shared Sub Migrate(currentRoot As String, schemaPath As String, fromVersion As Integer)
            Dim options As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            options.Converters.Add(New JsonStringEnumConverter())
            Try
                Dim manuscriptPath = Path.Combine(currentRoot, "data", "manuscripts.json")
                If File.Exists(manuscriptPath) Then
                    Dim manuscripts = JsonSerializer.Deserialize(Of List(Of Manuscript))(File.ReadAllText(manuscriptPath), options)
                    If manuscripts Is Nothing Then Throw New InvalidDataException("The manuscript library cannot be null.")
                    For Each manuscript In manuscripts
                        If manuscript Is Nothing Then Throw New InvalidDataException("The manuscript library contains a null record.")
                        SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
                        ReviewerResponseService.NormalizeAndValidateManuscript(manuscript)
                        PublicationMatchService.NormalizeAndValidateManuscript(manuscript)
                        WorkTypeService.NormalizeAndValidateManuscript(manuscript)
                    Next
                End If
                Dim authorsPath = Path.Combine(currentRoot, "data", "authors.json")
                If File.Exists(authorsPath) Then
                    Dim library = JsonSerializer.Deserialize(Of AuthorLibraryData)(File.ReadAllText(authorsPath), options)
                    If library Is Nothing Then Throw New InvalidDataException("The reusable metadata library cannot be null.")
                End If
            Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New InvalidDataException("PaperRoute cannot migrate storage schema " & fromVersion.ToString() &
                    " because existing data is invalid. The existing schema and data were left unchanged. " & ex.Message, ex)
            End Try

            Dim payload As New Dictionary(Of String, Object) From {
                {"SchemaVersion", fromVersion + 1}, {"UpdatedAtUtc", DateTime.UtcNow.ToString("O")}
            }
            ' Flushed and swapped in, keeping the old marker as schema.vN.bak:
            ' a power cut must never leave an empty schema.json (#125).
            StorageFile.Write(schemaPath, Path.Combine(Path.GetDirectoryName(schemaPath), "schema.v" & fromVersion.ToString() & ".bak"),
                Sub(stream) JsonSerializer.Serialize(stream, payload, New JsonSerializerOptions With {.WriteIndented = True}))
        End Sub
    End Class

End Namespace
