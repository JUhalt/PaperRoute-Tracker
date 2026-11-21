Imports System
Imports System.Security.Cryptography

' How the keys a user chooses to keep are encrypted on this computer (core
' stage 2, design 3.4). The host registers its implementation in
' PlatformServices.Secrets: DPAPI on Windows, from core stage 2's step 8.
Public Interface ISecretProtector

    ' Whether keys can be kept on this computer at all.
    ReadOnly Property IsAvailable As Boolean

    ' Encrypts for this user on this computer.
    Function Protect(clear As Byte()) As Byte()

    ' Decrypts; throws CryptographicException when the bytes cannot be read
    ' here (another account, another computer, another system).
    Function Unprotect(stored As Byte()) As Byte()

End Interface


' The default until a host registers one: no key can be kept, and a stored
' key reads as unreadable, which the key store already answers as "no key".
Friend NotInheritable Class UnavailableSecretProtector
    Implements ISecretProtector

    Public Shared ReadOnly Instance As New UnavailableSecretProtector()


    Private Sub New()
    End Sub


    Public ReadOnly Property IsAvailable As Boolean Implements ISecretProtector.IsAvailable
        Get
            Return False
        End Get
    End Property


    Public Function Protect(clear As Byte()) As Byte() Implements ISecretProtector.Protect
        Throw New InvalidOperationException("Keys can't be kept on this computer.")
    End Function


    Public Function Unprotect(stored As Byte()) As Byte() Implements ISecretProtector.Unprotect
        Throw New CryptographicException("Keys can't be read on this computer.")
    End Function

End Class
