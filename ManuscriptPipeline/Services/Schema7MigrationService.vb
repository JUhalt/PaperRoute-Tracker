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
                Dim dataDirectory = Path.Combine(currentRoot, "data")
                ' A primary that cannot be read passes when its safety backup
                ' reads (#124): the marker advances, and the load that follows
                ' recovers from the backup as it does under a current marker.
                ValidatePrimaryOrBackup(Path.Combine(dataDirectory, "manuscripts.json"), Path.Combine(dataDirectory, "manuscripts.bak"), Sub(json) ValidateManuscripts(json, options))
                ValidatePrimaryOrBackup(Path.Combine(dataDirectory, "authors.json"), Path.Combine(dataDirectory, "authors.bak"), Sub(json) ValidateAuthors(json, options))
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

        Private Shared Sub ValidatePrimaryOrBackup(primaryPath As String, backupPath As String, validate As Action(Of String))
            If Not File.Exists(primaryPath) Then Return
            Try
                validate(File.ReadAllText(primaryPath))
            Catch ex As Exception When (TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException) AndAlso ReadsWithCurrentRules(backupPath, validate)
                ' The backup stands in; the primary's error is the load's to report.
            End Try
        End Sub

        ' True when the file is there and reads with the current rules.
        Private Shared Function ReadsWithCurrentRules(filePath As String, validate As Action(Of String)) As Boolean
            If Not File.Exists(filePath) Then Return False
            Try
                validate(File.ReadAllText(filePath))
                Return True
            Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Return False
            End Try
        End Function

        Private Shared Sub ValidateManuscripts(json As String, options As JsonSerializerOptions)
            Dim manuscripts = JsonSerializer.Deserialize(Of List(Of Manuscript))(json, options)
            If manuscripts Is Nothing Then Throw New InvalidDataException("The manuscript library cannot be null.")
            For Each manuscript In manuscripts
                If manuscript Is Nothing Then Throw New InvalidDataException("The manuscript library contains a null record.")
                SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
                ReviewerResponseService.NormalizeAndValidateManuscript(manuscript)
                PublicationMatchService.NormalizeAndValidateManuscript(manuscript)
                WorkTypeService.NormalizeAndValidateManuscript(manuscript)
            Next
        End Sub

        Private Shared Sub ValidateAuthors(json As String, options As JsonSerializerOptions)
            Dim library = JsonSerializer.Deserialize(Of AuthorLibraryData)(json, options)
            If library Is Nothing Then Throw New InvalidDataException("The reusable metadata library cannot be null.")
        End Sub
    End Class

End Namespace
