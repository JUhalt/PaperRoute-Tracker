Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    ' The open manuscript page's working copy, written when PaperRoute
    ' closes without being able to ask about it (#123): Windows shutting
    ' down, or an error ending the session. Offered at the next start, and
    ' never written into the library until the user saves.
    Public Class RecoveryDraft

        Public Property ManuscriptId As Guid
        Public Property WrittenAtUtc As DateTime
        Public Property Reason As String = String.Empty
        Public Property Manuscript As Manuscript

        ' The people, affiliations and tag colours the page added to the
        ' author library and had not saved (#361). Nothing when the page
        ' changed nothing in the library; it is then left out of the file,
        ' so such a draft has the shape v0.9.2 to v1.0.0 wrote. A draft
        ' without it (v0.9.2, v1.0.0) means nothing there was unsaved. Older
        ' builds skip the key, since no UnmappedMemberHandling is set.
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property UnsavedLibrary As RecoveryDraftLibrary = Nothing

    End Class


    ' The author-library records a recovery draft carries (#361).
    Public Class RecoveryDraftLibrary

        Public Property Authors As List(Of AuthorRecord) = New List(Of AuthorRecord)()
        Public Property Affiliations As List(Of AffiliationRecord) = New List(Of AffiliationRecord)()
        Public Property TagColors As List(Of TagColor) = New List(Of TagColor)()

    End Class


    ' One file under data\recovery holds the one draft there can be: the
    ' page shows one manuscript at a time.
    Public Class RecoveryDraftStore

        Public Const FileName As String = "manuscript-page-draft.json"

        Private ReadOnly _path As String
        Private ReadOnly _jsonOptions As JsonSerializerOptions


        Public Sub New(
            dataDirectory As String
        )

            If String.IsNullOrWhiteSpace(dataDirectory) Then
                Throw New ArgumentException("A data directory is required.", NameOf(dataDirectory))
            End If

            _path =
                Path.Combine(
                    dataDirectory,
                    "recovery",
                    FileName
                )

            _jsonOptions =
                New JsonSerializerOptions With {
                    .WriteIndented = True,
                    .IgnoreReadOnlyProperties = True,
                    .PropertyNameCaseInsensitive = True
                }

            _jsonOptions.Converters.Add(
                New JsonStringEnumConverter()
            )

        End Sub


        ' Flushed and swapped in like the library's own files (#125), so a
        ' shutdown that cuts the write short leaves no half-written draft.
        ' Calendar dates are written and read as the library file has them
        ' (#118), so a draft read after a time-zone change keeps its days.
        Public Sub Write(
            draft As RecoveryDraft
        )

            If draft Is Nothing Then
                Throw New ArgumentNullException(NameOf(draft))
            End If

            If draft.Manuscript Is Nothing Then
                Throw New ArgumentException("A draft needs a manuscript.", NameOf(draft))
            End If

            Directory.CreateDirectory(
                Path.GetDirectoryName(_path)
            )

            StorageFile.Write(
                _path,
                Nothing,
                Sub(stream)

                    CalendarDateJson.Write(
                        stream,
                        JsonSerializer.Serialize(
                            Of RecoveryDraft
                        )(
                            draft,
                            _jsonOptions
                        )
                    )

                End Sub
            )

        End Sub


        ' The draft, or Nothing when there is none or it cannot be read. An
        ' unreadable draft is left where it is.
        Public Function Read() As RecoveryDraft

            If Not File.Exists(_path) Then
                Return Nothing
            End If

            Try

                Using stream As Stream = StorageFile.OpenRead(_path)

                    Dim draft As RecoveryDraft =
                        JsonSerializer.Deserialize(Of RecoveryDraft)(
                            CalendarDateJson.Read(stream),
                            _jsonOptions
                        )

                    If draft Is Nothing OrElse
                       draft.Manuscript Is Nothing OrElse
                       draft.ManuscriptId = Guid.Empty Then

                        Return Nothing

                    End If

                    Return draft

                End Using

            Catch ex As Exception When TypeOf ex Is IOException OrElse
                                       TypeOf ex Is UnauthorizedAccessException OrElse
                                       TypeOf ex Is JsonException

                Return Nothing

            End Try

        End Function


        Public Sub Delete()

            If File.Exists(_path) Then
                File.Delete(_path)
            End If

        End Sub

    End Class

End Namespace
