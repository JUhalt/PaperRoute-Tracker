Imports System
Imports System.Collections.Generic
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports ManuscriptPipeline

' The platform PaperRoute.Core.Tests runs the core on (core stage 2, design
' 3.5): a key protector that needs no key store and a launcher that never
' starts a process, which matters on a CI runner. CorePlatformSetup
' registers the two shared instances before the first test. A test class
' that changes the clock or the ids puts everything back in its
' TestCleanup with
'     PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
Friend NotInheritable Class CoreTestPlatform

    Private Sub New()
    End Sub


    Public Shared ReadOnly Secrets As New TestSecretProtector()

    ' Cleared by each test that reads it.
    Public Shared ReadOnly Launcher As New RecordingLauncher()


    ' Ids 00000001-0000-0000-0000-000000000000, 00000002-..., and so on.
    Public Shared Function CounterIds() As Func(Of Guid)
        Dim n As Integer = 0
        Return Function() New Guid(Interlocked.Increment(n), 0, 0, New Byte(7) {})
    End Function

End Class


' Keys "kept" by a reversible scramble: the 4 bytes "TEST", then each byte
' XOR &H5A. Bytes without the header read as written by another account.
Friend NotInheritable Class TestSecretProtector
    Implements ISecretProtector

    Private Shared ReadOnly Header As Byte() = Encoding.ASCII.GetBytes("TEST")


    Public ReadOnly Property IsAvailable As Boolean Implements ISecretProtector.IsAvailable
        Get
            Return True
        End Get
    End Property


    Public Function Protect(clear As Byte()) As Byte() Implements ISecretProtector.Protect

        If clear Is Nothing Then Throw New ArgumentNullException(NameOf(clear))

        Dim stored(Header.Length + clear.Length - 1) As Byte
        Array.Copy(Header, stored, Header.Length)
        For i As Integer = 0 To clear.Length - 1
            stored(Header.Length + i) = clear(i) Xor CByte(&H5A)
        Next
        Return stored

    End Function


    Public Function Unprotect(stored As Byte()) As Byte() Implements ISecretProtector.Unprotect

        If stored Is Nothing OrElse stored.Length < Header.Length Then
            Throw New CryptographicException("The key was not kept by this test platform.")
        End If
        For i As Integer = 0 To Header.Length - 1
            If stored(i) <> Header(i) Then
                Throw New CryptographicException("The key was not kept by this test platform.")
            End If
        Next

        Dim clear(stored.Length - Header.Length - 1) As Byte
        For i As Integer = 0 To clear.Length - 1
            clear(i) = stored(Header.Length + i) Xor CByte(&H5A)
        Next
        Return clear

    End Function

End Class


' Records each link and file it is asked to open; opens nothing.
Friend NotInheritable Class RecordingLauncher
    Implements ILauncher

    Public ReadOnly Property Links As New List(Of String)()
    Public ReadOnly Property Files As New List(Of String)()


    Public Sub OpenLink(address As String) Implements ILauncher.OpenLink
        Links.Add(address)
    End Sub


    Public Sub OpenFile(filePath As String) Implements ILauncher.OpenFile
        Files.Add(filePath)
    End Sub


    Public Sub Clear()
        Links.Clear()
        Files.Clear()
    End Sub

End Class


' A clock that stands still at UtcNow until a test moves it. The machine's
' zone gives the local time, as CoreClock.Now does (design 6.2), so no
' LocalTimeZone override is needed.
Friend NotInheritable Class FixedTimeProvider
    Inherits TimeProvider

    Public Sub New(instant As DateTimeOffset)
        UtcNow = instant
    End Sub


    Public Property UtcNow As DateTimeOffset


    Public Overrides Function GetUtcNow() As DateTimeOffset
        Return UtcNow
    End Function

End Class
