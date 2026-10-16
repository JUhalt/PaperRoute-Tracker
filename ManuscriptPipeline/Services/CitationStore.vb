Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    ' data\citations.json (#91): the last citation snapshot, written only
    ' when the researcher saves an update. Saved atomically, keeping the
    ' previous file as citations.bak; included in portable backups.
    Public Class CitationStore

        Public Const FileName As String = "citations.json"

        Private ReadOnly _path As String
        Private ReadOnly _backupPath As String

        Private Shared ReadOnly Options As New JsonSerializerOptions With {
            .WriteIndented = True,
            .PropertyNameCaseInsensitive = True
        }


        Public Sub New()
            Me.New(Path.Combine(StorageMigrationService.CurrentDataRoot(), "data"))
        End Sub


        Friend Sub New(dataDirectory As String)
            _path = Path.Combine(dataDirectory, FileName)
            _backupPath = Path.Combine(dataDirectory, "citations.bak")
        End Sub


        Public ReadOnly Property DataFilePath As String
            Get
                Return _path
            End Get
        End Property


        ' The snapshot, or Nothing when none was saved. A damaged file falls
        ' back to citations.bak, and throws only when neither can be read.
        Public Function Load() As CitationSnapshot
            If Not File.Exists(_path) AndAlso Not File.Exists(_backupPath) Then Return Nothing
            Dim snapshot As CitationSnapshot = TryRead(_path)
            If snapshot Is Nothing Then snapshot = TryRead(_backupPath)
            If snapshot Is Nothing Then Throw New InvalidDataException("PaperRoute couldn't read your saved citations.")
            Return snapshot
        End Function


        Public Sub Save(snapshot As CitationSnapshot)
            If snapshot Is Nothing Then Throw New ArgumentNullException(NameOf(snapshot))
            Normalize(snapshot)
            Directory.CreateDirectory(Path.GetDirectoryName(_path))
            StorageFile.Write(_path, _backupPath, Sub(stream) JsonSerializer.Serialize(stream, snapshot, Options))
        End Sub


        Private Shared Function TryRead(file As String) As CitationSnapshot
            Try
                If Not IO.File.Exists(file) Then Return Nothing
                Return ReadJson(IO.File.ReadAllText(file))
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidDataException
                Return Nothing
            End Try
        End Function


        ' A snapshot from JSON, normalized; InvalidDataException when it isn't
        ' one. Restore validates a backup's citations.json with this.
        Public Shared Function ReadJson(json As String) As CitationSnapshot
            If String.IsNullOrWhiteSpace(json) Then Throw New InvalidDataException("The citations file is empty.")
            Dim snapshot As CitationSnapshot
            Try
                snapshot = JsonSerializer.Deserialize(Of CitationSnapshot)(json, Options)
            Catch ex As JsonException
                Throw New InvalidDataException("The citations file isn't valid.", ex)
            End Try
            If snapshot Is Nothing Then Throw New InvalidDataException("The citations file is empty.")
            Normalize(snapshot)
            Return snapshot
        End Function


        ' Lenient: a bad work or value is dropped, never fatal.
        Public Shared Sub Normalize(snapshot As CitationSnapshot)

            If snapshot Is Nothing Then Return
            snapshot.Orcid = If(snapshot.Orcid, String.Empty).Trim()
            snapshot.Source = If(snapshot.Source, String.Empty).Trim()
            snapshot.Excluded = If(snapshot.Excluded, New List(Of String)()).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Select(Function(item) item.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()

            snapshot.Works = If(snapshot.Works, New List(Of CitedWork)()).Where(Function(item) item IsNot Nothing).ToList()
            For Each work As CitedWork In snapshot.Works
                work.OpenAlexId = If(Regex.IsMatch(If(work.OpenAlexId, String.Empty).Trim(), "^W\d{1,15}$"), work.OpenAlexId.Trim(), String.Empty)
                work.Doi = CitationKeys.Doi(work.Doi)
                work.VersionDois = If(work.VersionDois, New List(Of String)()).
                    Select(AddressOf CitationKeys.Doi).
                    Where(Function(item) item.Length > 0 AndAlso item <> work.Doi).
                    Distinct().
                    ToList()
                work.Title = Truncate(If(work.Title, String.Empty).Trim(), 400)
                work.Journal = Truncate(If(work.Journal, String.Empty).Trim(), 200)
                work.FoundBy = If(work.FoundBy, String.Empty).Trim()
                If work.CitedByCount < 0 Then work.CitedByCount = 0
                If work.Year.HasValue AndAlso (work.Year.Value < 1600 OrElse work.Year.Value > 2200) Then work.Year = Nothing
                If work.Fwci.HasValue AndAlso (Double.IsNaN(work.Fwci.Value) OrElse Double.IsInfinity(work.Fwci.Value) OrElse work.Fwci.Value < 0) Then work.Fwci = Nothing
                If work.Percentile.HasValue AndAlso (Double.IsNaN(work.Percentile.Value) OrElse work.Percentile.Value < 0 OrElse work.Percentile.Value > 1) Then work.Percentile = Nothing
                work.CountsByYear = If(work.CountsByYear, New List(Of YearCount)()).
                    Where(Function(item) item IsNot Nothing AndAlso item.Year >= 1900 AndAlso item.Year <= 2200 AndAlso item.Count >= 0).
                    GroupBy(Function(item) item.Year).
                    Select(Function(group) New YearCount With {.Year = group.Key, .Count = group.Sum(Function(item) item.Count)}).
                    OrderBy(Function(item) item.Year).
                    ToList()
            Next
            snapshot.Works = snapshot.Works.Where(Function(item) item.OpenAlexId.Length > 0 OrElse item.Doi.Length > 0).ToList()

        End Sub


        Private Shared Function Truncate(value As String, length As Integer) As String
            Return If(value.Length <= length, value, value.Substring(0, length))
        End Function

    End Class


    ' Keys that identify one work across ORCID, OpenAlex, and PaperRoute.
    Public NotInheritable Class CitationKeys

        Private Sub New()
        End Sub

        ' A DOI without https://doi.org/, lower-case; "" when it isn't one.
        Public Shared Function Doi(value As String) As String
            Dim normalized As String = DoiNormalizer.Normalize(If(value, String.Empty)).Trim().ToLowerInvariant()
            Return If(DoiNormalizer.IsValid(normalized), normalized, String.Empty)
        End Function

        ' Every key the work goes by: each of its DOIs, and its OpenAlex id.
        Public Shared Function AllForWork(work As CitedWork) As List(Of String)
            If work Is Nothing Then Return New List(Of String)()
            Dim keys As List(Of String) = If(work.VersionDois, New List(Of String)()).Prepend(work.Doi).
                Where(Function(item) Not String.IsNullOrEmpty(item)).
                Select(Function(item) "doi:" & item).
                ToList()
            If Not String.IsNullOrEmpty(work.OpenAlexId) Then keys.Add("openalex:" & work.OpenAlexId)
            Return keys
        End Function

        ' "doi:…" when the work has a DOI, else "openalex:W…".
        Public Shared Function ForWork(work As CitedWork) As String
            If work Is Nothing Then Return String.Empty
            If work.Doi.Length > 0 Then Return "doi:" & work.Doi
            Return If(work.OpenAlexId.Length > 0, "openalex:" & work.OpenAlexId, String.Empty)
        End Function

    End Class

End Namespace
