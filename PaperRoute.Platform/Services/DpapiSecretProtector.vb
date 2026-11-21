Imports System
Imports System.Runtime.Versioning
Imports System.Security.Cryptography
Imports System.Text

Namespace Services

    ' How the keys a user chooses to keep are encrypted on Windows: DPAPI, for
    ' the current Windows account. Moved from ProtectedKeyStore (core stage 2,
    ' step 8) unchanged: the same entropy, the same scope and the same bytes,
    ' so a key stored by PaperRoute 1.0.x opens here and one stored here opens
    ' in 1.0.x. Never change Entropy or the scope: every stored key would stop
    ' opening (DpapiCompatibilityTests).
    <SupportedOSPlatform("windows")>
    Public NotInheritable Class DpapiSecretProtector
        Implements ISecretProtector

        Private Shared ReadOnly Entropy As Byte() = Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1")


        Public ReadOnly Property IsAvailable As Boolean Implements ISecretProtector.IsAvailable
            Get
                Return True
            End Get
        End Property


        Public Function Protect(clear As Byte()) As Byte() Implements ISecretProtector.Protect
            Return ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser)
        End Function


        ' Throws CryptographicException when the bytes were protected for
        ' another account or on another computer.
        Public Function Unprotect(stored As Byte()) As Byte() Implements ISecretProtector.Unprotect
            Return ProtectedData.Unprotect(stored, Entropy, DataProtectionScope.CurrentUser)
        End Function

    End Class

End Namespace
