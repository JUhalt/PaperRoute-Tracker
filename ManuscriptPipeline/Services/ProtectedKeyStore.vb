Imports System
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

Namespace Services

    ' Keys the user chooses to add, such as a free OpenAlex key (#86),
    ' encrypted for the current Windows account (DPAPI). They are kept beside
    ' the library's data but never in its records, backups, or exports, and
    ' PaperRoute never ships a key of its own.
    Public NotInheritable Class ProtectedKeyStore

        Public Const OpenAlex As String = "openalex"
        ' The AI assistant's keys (#84): the researcher's Anthropic key, and
        ' an optional key for a compatible server.
        Public Const Anthropic As String = "anthropic"
        Public Const AssistantEndpoint As String = "assistant-endpoint"

        Private Shared ReadOnly Entropy As Byte() = Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1")

        Private ReadOnly _directory As String


        Public Sub New()
            Me.New(Path.Combine(StorageMigrationService.CurrentDataRoot(), "keys"))
        End Sub


        Friend Sub New(directory As String)
            If String.IsNullOrWhiteSpace(directory) Then Throw New ArgumentException("A key directory is required.", NameOf(directory))
            _directory = directory
        End Sub


        Public Function HasKey(name As String) As Boolean
            Return Load(name) IsNot Nothing
        End Function


        ' The key, or Nothing when none is stored or it cannot be decrypted
        ' (for example, after copying the folder to another account).
        Public Function Load(name As String) As String
            Dim file As String = PathFor(name)
            If Not IO.File.Exists(file) Then Return Nothing
            Try
                Dim clear As Byte() = ProtectedData.Unprotect(IO.File.ReadAllBytes(file), Entropy, DataProtectionScope.CurrentUser)
                Dim key As String = Encoding.UTF8.GetString(clear)
                Return If(IsPlausibleKey(key), key, Nothing)
            Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return Nothing
            End Try
        End Function


        Public Sub Save(name As String, key As String)
            Dim value As String = If(key, String.Empty).Trim()
            If Not IsPlausibleKey(value) Then Throw New ArgumentException("That doesn't look like a key: it should be one word of letters and numbers, without spaces.", NameOf(key))
            Directory.CreateDirectory(_directory)
            Dim file As String = PathFor(name)
            Dim temporary As String = file & ".tmp"
            IO.File.WriteAllBytes(temporary, ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser))
            IO.File.Move(temporary, file, overwrite:=True)
        End Sub


        Public Sub Remove(name As String)
            Dim file As String = PathFor(name)
            If IO.File.Exists(file) Then IO.File.Delete(file)
        End Sub


        ' One word of printable characters: keys are pasted, so stray spaces
        ' and line breaks are the usual mistake.
        Public Shared Function IsPlausibleKey(value As String) As Boolean
            If String.IsNullOrEmpty(value) OrElse value.Length < 8 OrElse value.Length > 200 Then Return False
            For Each character As Char In value
                If character <= " "c OrElse character > "~"c Then Return False
            Next
            Return True
        End Function


        Private Function PathFor(name As String) As String
            If String.IsNullOrWhiteSpace(name) OrElse name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then Throw New ArgumentException("A key name is required.", NameOf(name))
            Return Path.Combine(_directory, name & ".key")
        End Function

    End Class

End Namespace
