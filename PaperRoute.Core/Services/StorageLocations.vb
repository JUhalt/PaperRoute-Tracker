Imports System
Imports System.IO

Namespace Services

    ' Where PaperRoute keeps what (core stage 2, design 4.1): the one place
    ' the guide's per-system table, Diagnostics and a second interface read.
    ' Read-only. Each member returns exactly the expression the default-path
    ' builders used before they called it, so every folder is the same as in
    ' 1.0 on Windows; an isolated session or a test's RootOverride stands in
    ' for the root as before. File names (manuscripts.json, settings.json and
    ' the rest) stay with the code that writes them.
    Public NotInheritable Class StorageLocations

        Private Sub New()
        End Sub


        ' <LOCALAPPDATA>\PaperRoute on Windows (PaperRoute-Dev in the
        ' development profile).
        Public Shared Function DataRoot() As String
            Return StorageMigrationService.CurrentDataRoot()
        End Function


        ' manuscripts.json, authors.json, citations.json and schema.json.
        Public Shared Function LibraryDataFolder() As String
            Return Path.Combine(DataRoot(), "data")
        End Function


        ' settings.json and settings.bak sit in the data root itself.
        Public Shared Function SettingsFolder() As String
            Return DataRoot()
        End Function


        ' Error reports.
        Public Shared Function LogsFolder() As String
            Return Path.Combine(DataRoot(), "logs")
        End Function


        ' Keys the user chose to keep (ProtectedKeyStore).
        Public Shared Function KeysFolder() As String
            Return Path.Combine(DataRoot(), "keys")
        End Function


        ' <Documents>\PaperRoute Library on Windows (PaperRoute Dev Library in
        ' the development profile): the managed copies of the user's files.
        Public Shared Function ManagedLibraryRoot() As String
            Return StorageMigrationService.CurrentManagedLibraryRoot()
        End Function

    End Class

End Namespace
