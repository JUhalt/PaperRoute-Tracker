Imports System
Imports System.IO
Imports System.Linq
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
            Return Read(name).Key
        End Function


        ' A key kept with the address it was added for (#84): the key, only
        ' when that address is this one. The two are stored in one encrypted
        ' file, so a key can never be paired with another address.
        Public Function LoadFor(name As String, origin As String) As String
            Dim stored = Read(name)
            If stored.Key Is Nothing OrElse String.IsNullOrEmpty(origin) Then Return Nothing
            Return If(String.Equals(stored.Origin, origin.Trim(), StringComparison.OrdinalIgnoreCase), stored.Key, Nothing)
        End Function


        ' The address a key was added for, or "" when it has none.
        Public Function OriginOf(name As String) As String
            Return Read(name).Origin
        End Function


        Public Sub Save(name As String, key As String)
            Write(name, String.Empty, key)
        End Sub


        ' Saves a key with the address (scheme, host, and port) it is for.
        Public Sub SaveFor(name As String, origin As String, key As String)
            Dim address As String = If(origin, String.Empty).Trim().ToLowerInvariant()
            ' One line: the address and the key are stored a line apart.
            If address.Length = 0 OrElse address.Length > 300 OrElse address.Any(Function(character) Char.IsControl(character) OrElse Char.IsWhiteSpace(character)) Then
                Throw New ArgumentException("An address is required for this key.", NameOf(origin))
            End If
            Write(name, address, key)
        End Sub


        Private Sub Write(name As String, origin As String, key As String)
            Dim value As String = If(key, String.Empty).Trim()
            If Not IsPlausibleKey(value) Then Throw New ArgumentException("That doesn't look like a key: it should be one word of letters and numbers, without spaces.", NameOf(key))
            Directory.CreateDirectory(_directory)
            Dim file As String = PathFor(name)
            Dim temporary As String = file & ".tmp"
            Dim text As String = If(origin.Length > 0, origin & vbLf & value, value)
            IO.File.WriteAllBytes(temporary, ProtectedData.Protect(Encoding.UTF8.GetBytes(text), Entropy, DataProtectionScope.CurrentUser))
            IO.File.Move(temporary, file, overwrite:=True)
        End Sub


        ' The stored key and, when it was saved with one, its address; no
        ' key when none is stored or it cannot be decrypted (for example,
        ' after copying the folder to another account).
        Private Function Read(name As String) As (Origin As String, Key As String)
            Dim file As String = PathFor(name)
            If Not IO.File.Exists(file) Then Return (String.Empty, Nothing)
            Try
                Dim clear As Byte() = ProtectedData.Unprotect(IO.File.ReadAllBytes(file), Entropy, DataProtectionScope.CurrentUser)
                Dim text As String = Encoding.UTF8.GetString(clear)
                Dim origin As String = String.Empty
                Dim split As Integer = text.IndexOf(vbLf, StringComparison.Ordinal)
                If split >= 0 Then
                    origin = text.Substring(0, split)
                    text = text.Substring(split + 1)
                End If
                Return If(IsPlausibleKey(text), (origin, text), (String.Empty, Nothing))
            Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return (String.Empty, Nothing)
            End Try
        End Function


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
