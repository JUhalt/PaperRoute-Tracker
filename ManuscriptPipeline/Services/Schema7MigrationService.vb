Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Friend NotInheritable Class Schema7MigrationService
        Private Sub New()
        End Sub

        Friend Shared Sub Migrate(currentRoot As String, schemaPath As String)
            ' Schema 7 adds remembered publication matches (#61), which default
            ' to empty. As in Schema 6, the older JSON, managed files, and
            ' backups stay byte-for-byte; only the marker advances, after all
            ' existing data validates. The marker keeps an older build from
            ' opening the library and silently dropping ignored matches.
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
                    Next
                End If
                Dim authorsPath = Path.Combine(currentRoot, "data", "authors.json")
                If File.Exists(authorsPath) Then
                    Dim library = JsonSerializer.Deserialize(Of AuthorLibraryData)(File.ReadAllText(authorsPath), options)
                    If library Is Nothing Then Throw New InvalidDataException("The reusable metadata library cannot be null.")
                End If
            Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New InvalidDataException("PaperRoute cannot migrate storage schema 6 because existing data is invalid. The existing schema and data were left unchanged. " & ex.Message, ex)
            End Try

            Dim payload As New Dictionary(Of String, Object) From {
                {"SchemaVersion", 7}, {"UpdatedAtUtc", DateTime.UtcNow.ToString("O")}
            }
            Dim temporaryPath = schemaPath & ".tmp-" & Guid.NewGuid().ToString("N")
            Try
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(payload, New JsonSerializerOptions With {.WriteIndented = True}))
                ' Replacing atomically also preserves any existing backup if
                ' the marker is locked. Never remove that backup beforehand.
                File.Replace(temporaryPath, schemaPath, Path.Combine(Path.GetDirectoryName(schemaPath), "schema.v6.bak"), True)
            Finally
                If File.Exists(temporaryPath) Then
                    Try
                        File.Delete(temporaryPath)
                    Catch
                        ' Best-effort temporary-file cleanup only.
                    End Try
                End If
            End Try
        End Sub
    End Class

End Namespace
