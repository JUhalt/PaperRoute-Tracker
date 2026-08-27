Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Friend NotInheritable Class Schema5MigrationService

        Private Sub New()
        End Sub


        Friend Shared Sub Migrate(
            currentRoot As String,
            schemaPath As String
        )

            ValidateManuscriptData(
                currentRoot
            )

            ValidateReusableMetadata(
                currentRoot
            )

            Dim backupPath As String =
                Path.Combine(
                    Path.GetDirectoryName(schemaPath),
                    "schema.v4.bak"
                )

            WriteSchemaVersion5(
                schemaPath,
                backupPath
            )

        End Sub


        Private Shared Sub ValidateManuscriptData(
            currentRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    currentRoot,
                    "data",
                    "manuscripts.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(
                    dataPath
                )

            If String.IsNullOrWhiteSpace(json) Then
                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 4 because the manuscript data file is empty. " &
                    "The existing schema and manuscript data were left unchanged."
                )
            End If

            Try

                Dim manuscripts As List(Of Manuscript) =
                    JsonSerializer.Deserialize(
                        Of List(Of Manuscript)
                    )(
                        json,
                        CreateJsonOptions()
                    )

                If manuscripts Is Nothing Then
                    Throw New InvalidDataException(
                        "PaperRoute cannot migrate storage schema 4 because the manuscript data could not be read. " &
                        "The existing schema and manuscript data were left unchanged."
                    )
                End If

                For Each manuscript As Manuscript In manuscripts

                    If manuscript Is Nothing Then
                        Throw New InvalidDataException(
                            "PaperRoute cannot migrate storage schema 4 because the manuscript library contains a null record. " &
                            "The existing schema and manuscript data were left unchanged."
                        )
                    End If

                    SubmissionReadinessValidationService.NormalizeAndValidateManuscript(
                            manuscript
                        )

                Next

            Catch ex As JsonException

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 4 because the manuscript data contains invalid JSON or unsupported values. " &
                    "The existing schema and manuscript data were left unchanged.",
                    ex
                )

            End Try

        End Sub


        Private Shared Sub ValidateReusableMetadata(
            currentRoot As String
        )

            Dim dataPath As String =
                Path.Combine(
                    currentRoot,
                    "data",
                    "authors.json"
                )

            If Not File.Exists(dataPath) Then
                Return
            End If

            Dim json As String =
                File.ReadAllText(
                    dataPath
                )

            If String.IsNullOrWhiteSpace(json) Then
                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 4 because the reusable metadata library is empty. " &
                    "The existing schema and metadata were left unchanged."
                )
            End If

            Try

                Dim library As AuthorLibraryData =
                    JsonSerializer.Deserialize(
                        Of AuthorLibraryData
                    )(
                        json,
                        CreateJsonOptions()
                    )

                If library Is Nothing Then
                    Throw New InvalidDataException(
                        "PaperRoute cannot migrate storage schema 4 because the reusable metadata library could not be read. " &
                        "The existing schema and metadata were left unchanged."
                    )
                End If

                If library.Journals Is Nothing Then
                    Return
                End If

                Dim journalIds As New HashSet(Of Guid)()

                For Each journal As JournalRecord In library.Journals

                    If journal Is Nothing Then
                        Throw New InvalidDataException(
                            "PaperRoute cannot migrate storage schema 4 because the reusable metadata library contains a null journal record."
                        )
                    End If

                    If journal.Id = Guid.Empty OrElse
                       Not journalIds.Add(journal.Id) Then

                        Throw New InvalidDataException(
                            "PaperRoute cannot migrate storage schema 4 because the reusable metadata library contains invalid or duplicate journal identifiers."
                        )

                    End If

                    SubmissionReadinessValidationService.NormalizeAndValidateJournal(
                            journal
                        )

                Next

            Catch ex As JsonException

                Throw New InvalidDataException(
                    "PaperRoute cannot migrate storage schema 4 because the reusable metadata library contains invalid JSON or unsupported values. " &
                    "The existing schema and metadata were left unchanged.",
                    ex
                )

            End Try

        End Sub


        Private Shared Sub WriteSchemaVersion5(
            schemaPath As String,
            backupPath As String
        )

            Dim payload As New Dictionary(Of String, Object) From {
                {
                    "SchemaVersion",
                    5
                },
                {
                    "UpdatedAtUtc",
                    DateTime.UtcNow.ToString("O")
                }
            }

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }

            Dim tempPath As String =
                schemaPath &
                ".tmp-" &
                Guid.NewGuid().ToString("N")

            Try

                File.WriteAllText(
                    tempPath,
                    JsonSerializer.Serialize(
                        payload,
                        options
                    )
                )

                If File.Exists(backupPath) Then
                    File.Delete(
                        backupPath
                    )
                End If

                File.Replace(
                    tempPath,
                    schemaPath,
                    backupPath,
                    True
                )

            Finally

                If File.Exists(tempPath) Then

                    Try
                        File.Delete(tempPath)
                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

            End Try

        End Sub


        Private Shared Function CreateJsonOptions() As JsonSerializerOptions

            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True,
                .IgnoreReadOnlyProperties = True,
                .PropertyNameCaseInsensitive = True
            }

            options.Converters.Add(
                New JsonStringEnumConverter()
            )

            Return options

        End Function

    End Class

End Namespace
