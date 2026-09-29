Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class AppSettingsService

        Private ReadOnly _settingsDirectory As String
        Private ReadOnly _settingsPath As String
        Private ReadOnly _backupPath As String
        Private ReadOnly _jsonOptions As JsonSerializerOptions

        Private _loadFailed As Boolean = False


        Public Sub New()

            Me.New(StorageMigrationService.CurrentDataRoot())

        End Sub


        ' For tests: settings in a folder of their own.
        Friend Sub New(directory As String)

            _settingsDirectory = directory
            _settingsPath = Path.Combine(_settingsDirectory, "settings.json")
            _backupPath = Path.Combine(_settingsDirectory, "settings.bak")

            _jsonOptions =
                New JsonSerializerOptions With {
                    .WriteIndented = True,
                    .PropertyNameCaseInsensitive = True
                }

            _jsonOptions.Converters.Add(
                New JsonStringEnumConverter()
            )

        End Sub


        ' True when settings.json existed but couldn't be read, or was missing
        ' beside its backup. The backup's other preferences are used, but
        ' PaperRoute works offline until the settings are saved again (#86): the
        ' backup is one save older, so it may predate a Work offline choice.
        Public ReadOnly Property LoadFailed As Boolean
            Get
                Return _loadFailed
            End Get
        End Property


        Public Function Load() As AppSettings

            _loadFailed = False

            If Not File.Exists(_settingsPath) AndAlso Not File.Exists(_backupPath) Then
                Return New AppSettings()
            End If

            Dim settings As AppSettings = TryRead(_settingsPath)

            If settings Is Nothing Then
                _loadFailed = True
                settings = If(TryRead(_backupPath), New AppSettings())
                Normalize(settings)
                settings.OnlineServices.WorkOffline = True
                Return settings
            End If

            Normalize(settings)
            Return settings

        End Function


        ' Written to a temporary file and swapped in, keeping the previous
        ' settings as settings.bak.
        Public Sub Save(
            settings As AppSettings
        )

            If settings Is Nothing Then
                Throw New ArgumentNullException(NameOf(settings))
            End If

            Normalize(
                settings
            )

            Directory.CreateDirectory(
                _settingsDirectory
            )

            Dim temporary As String = _settingsPath & ".tmp"
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, _jsonOptions))

            ' A file that could not be read is set aside, not kept as the backup.
            If _loadFailed AndAlso File.Exists(_settingsPath) Then
                File.Move(_settingsPath, Path.Combine(_settingsDirectory, "settings.unreadable.json"), overwrite:=True)
            End If

            If File.Exists(_settingsPath) Then
                File.Replace(temporary, _settingsPath, _backupPath)
            Else
                File.Move(temporary, _settingsPath)
            End If

            _loadFailed = False

        End Sub


        Private Function TryRead(path As String) As AppSettings

            Try

                If Not File.Exists(path) Then Return Nothing

                Dim json As String = File.ReadAllText(path)
                If String.IsNullOrWhiteSpace(json) Then Return Nothing

                Return JsonSerializer.Deserialize(Of AppSettings)(json, _jsonOptions)

            Catch ex As Exception When TypeOf ex Is IOException OrElse
                                       TypeOf ex Is UnauthorizedAccessException OrElse
                                       TypeOf ex Is JsonException OrElse
                                       TypeOf ex Is NotSupportedException

                Return Nothing

            End Try

        End Function


        Private Sub Normalize(
            settings As AppSettings
        )

            settings.FileDrawerSuggestionThreshold =
                Math.Max(
                    1,
                    Math.Min(
                        20,
                        settings.FileDrawerSuggestionThreshold
                    )
                )

            settings.LongReviewThresholdDays =
                Math.Max(
                    1,
                    Math.Min(
                        730,
                        settings.LongReviewThresholdDays
                    )
                )

            settings.RevisionWarningDays =
                Math.Max(
                    1,
                    Math.Min(
                        180,
                        settings.RevisionWarningDays
                    )
                )

            settings.RecentRejectionThresholdDays =
                Math.Max(
                    1,
                    Math.Min(
                        365,
                        settings.RecentRejectionThresholdDays
                    )
                )

            settings.ReminderNotificationDaysAhead =
                Math.Max(
                    0,
                    Math.Min(
                        30,
                        settings.ReminderNotificationDaysAhead
                    )
                )

            If settings.LastReminderNotificationDate.HasValue Then

                settings.LastReminderNotificationDate =
                    settings.LastReminderNotificationDate.Value.Date

            End If

            If settings.OnlineServices Is Nothing Then settings.OnlineServices = New OnlineServicesSettings()
            settings.OnlineServices.TurnedOff =
                If(settings.OnlineServices.TurnedOff, New List(Of String)()).
                    Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                    Select(Function(item) item.Trim()).
                    Distinct(StringComparer.Ordinal).
                    ToList()

        End Sub

    End Class

End Namespace
