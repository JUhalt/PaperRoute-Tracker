Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models

<TestClass>
Public Class SettingsTests

    <TestMethod>
    Public Sub NewAppSettings_DefaultsToStableUpdateChannel()

        Dim settings As New AppSettings()

        Assert.AreEqual(
            AppUpdateChannel.Stable,
            settings.UpdateChannel
        )

    End Sub


    <TestMethod>
    Public Sub ExistingPreviewSetting_RemainsPreviewWhenDeserialized()

        Dim options As JsonSerializerOptions =
            CreateSettingsJsonOptions()

        Dim json As String =
            "{""UpdateChannel"":""Preview"",""CheckForUpdatesAutomatically"":true}"

        Dim settings As AppSettings =
            JsonSerializer.Deserialize(Of AppSettings)(
                json,
                options
            )

        Assert.IsNotNull(settings)

        Assert.AreEqual(
            AppUpdateChannel.Preview,
            settings.UpdateChannel
        )

    End Sub


    <TestMethod>
    Public Sub MissingUpdateChannel_UsesStableDefaultWhenDeserialized()

        Dim options As JsonSerializerOptions =
            CreateSettingsJsonOptions()

        Dim settings As AppSettings =
            JsonSerializer.Deserialize(Of AppSettings)(
                "{}",
                options
            )

        Assert.IsNotNull(settings)

        Assert.AreEqual(
            AppUpdateChannel.Stable,
            settings.UpdateChannel
        )

    End Sub


    Private Shared Function CreateSettingsJsonOptions() As JsonSerializerOptions

        Dim options As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }

        options.Converters.Add(
            New JsonStringEnumConverter()
        )

        Return options

    End Function

End Class
