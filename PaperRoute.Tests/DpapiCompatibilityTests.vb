Imports System
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' Stored keys keep opening across the move of the DPAPI calls into
' PaperRoute.Platform (core stage 2, step 8; risk R3). A key file written by
' PaperRoute 1.0.x must open in this build, and one written by this build
' must open in 1.0.x: the same call, the same entropy, the same scope and
' the same bytes, in the same file. "Old code" below is 1.0.1's
' ProtectedKeyStore lines, copied literally. A checked-in key file cannot
' serve, since DPAPI ties it to the account, so each test makes its own
' keys in a temporary folder of its own.
<TestClass>
<DoNotParallelize>
Public Class DpapiCompatibilityTests

    Private _directory As String


    <TestInitialize>
    Public Sub Setup()
        _directory = CoreTestSupport.CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        CoreTestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    <TestMethod>
    Public Sub OldCodeWritesNewCodeReads()

        Dim origin As String = "https://assistant.example.org"
        Dim key As String = NewTestKey()
        Dim plainKey As String = NewTestKey()

        ' 1.0.1's ProtectedKeyStore.Write, for a key with its address and for
        ' a key without one.
        File.WriteAllBytes(Path.Combine(_directory, ProtectedKeyStore.AssistantEndpoint & ".key"),
            ProtectedData.Protect(Encoding.UTF8.GetBytes(origin & vbLf & key), Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1"), DataProtectionScope.CurrentUser))
        File.WriteAllBytes(Path.Combine(_directory, ProtectedKeyStore.OpenAlex & ".key"),
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plainKey), Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1"), DataProtectionScope.CurrentUser))

        Dim store As New ProtectedKeyStore(_directory, New DpapiSecretProtector())

        Assert.AreEqual(key, store.LoadFor(ProtectedKeyStore.AssistantEndpoint, origin))
        Assert.AreEqual(origin, store.OriginOf(ProtectedKeyStore.AssistantEndpoint))
        Assert.IsNull(store.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://elsewhere.example.org"),
            "The key is still tied to its own address.")
        Assert.AreEqual(plainKey, store.Load(ProtectedKeyStore.OpenAlex))
        Assert.AreEqual(String.Empty, store.OriginOf(ProtectedKeyStore.OpenAlex))
        Assert.IsTrue(store.HasKey(ProtectedKeyStore.OpenAlex))

        ' And through the protector the host registered, as the app reads it.
        Dim registered As New ProtectedKeyStore(_directory)
        Assert.AreEqual(key, registered.LoadFor(ProtectedKeyStore.AssistantEndpoint, origin))
        Assert.AreEqual(plainKey, registered.Load(ProtectedKeyStore.OpenAlex))

    End Sub


    <TestMethod>
    Public Sub NewCodeWritesOldCodeReads()

        Dim origin As String = "https://assistant.example.org"
        Dim key As String = NewTestKey()
        Dim plainKey As String = NewTestKey()

        Dim store As New ProtectedKeyStore(_directory, New DpapiSecretProtector())
        store.SaveFor(ProtectedKeyStore.AssistantEndpoint, origin, key)
        store.Save(ProtectedKeyStore.OpenAlex, plainKey)

        ' 1.0.1's ProtectedKeyStore.Read, the decryption line literally.
        Assert.AreEqual(origin & vbLf & key, OldCodeReads(ProtectedKeyStore.AssistantEndpoint))
        Assert.AreEqual(plainKey, OldCodeReads(ProtectedKeyStore.OpenAlex))

        ' The same file names, and no temporary file left behind.
        CollectionAssert.AreEquivalent(
            {ProtectedKeyStore.AssistantEndpoint & ".key", ProtectedKeyStore.OpenAlex & ".key"},
            Directory.GetFiles(_directory).Select(Function(file) Path.GetFileName(file)).ToArray())

        ' And through the protector the host registered, as the app writes it.
        Dim anthropicKey As String = NewTestKey()
        Dim registered As New ProtectedKeyStore(_directory)
        registered.Save(ProtectedKeyStore.Anthropic, anthropicKey)
        Assert.AreEqual(anthropicKey, OldCodeReads(ProtectedKeyStore.Anthropic))

    End Sub


    ' Bytes DPAPI protected with other entropy, as a key from another
    ' program, read as "no key", as they did in 1.0.1: the DPAPI error
    ' still reaches the store's catch.
    <TestMethod>
    Public Sub BytesTheStoreCannotDecryptReadAsNoKey()

        File.WriteAllBytes(Path.Combine(_directory, ProtectedKeyStore.OpenAlex & ".key"),
            ProtectedData.Protect(Encoding.UTF8.GetBytes(NewTestKey()), Encoding.UTF8.GetBytes("Another program"), DataProtectionScope.CurrentUser))
        File.WriteAllBytes(Path.Combine(_directory, ProtectedKeyStore.Anthropic & ".key"), {1, 2, 3, 4})

        Dim store As New ProtectedKeyStore(_directory, New DpapiSecretProtector())

        Assert.IsNull(store.Load(ProtectedKeyStore.OpenAlex))
        Assert.IsFalse(store.HasKey(ProtectedKeyStore.OpenAlex))
        Assert.AreEqual(String.Empty, store.OriginOf(ProtectedKeyStore.OpenAlex))
        Assert.IsNull(store.Load(ProtectedKeyStore.Anthropic))

        Dim protector As New DpapiSecretProtector()
        Dim foreign As Byte() = File.ReadAllBytes(Path.Combine(_directory, ProtectedKeyStore.OpenAlex & ".key"))
        Assert.ThrowsExactly(Of CryptographicException)(Sub() protector.Unprotect(foreign))

    End Sub


    <TestMethod>
    Public Sub TheWindowsProtectorKeepsKeys()

        Dim protector As New DpapiSecretProtector()
        Dim clear As Byte() = Encoding.UTF8.GetBytes(NewTestKey())

        Assert.IsTrue(protector.IsAvailable)
        CollectionAssert.AreEqual(clear, protector.Unprotect(protector.Protect(clear)))
        CollectionAssert.AreEqual(clear,
            ProtectedData.Unprotect(protector.Protect(clear), Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1"), DataProtectionScope.CurrentUser))

    End Sub


    Private Function OldCodeReads(name As String) As String
        Dim clear As Byte() = ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(_directory, name & ".key")), Encoding.UTF8.GetBytes("PaperRoute.ProtectedKeyStore.v1"), DataProtectionScope.CurrentUser)
        Return Encoding.UTF8.GetString(clear)
    End Function


    ' A key made by the test, never a real one.
    Private Shared Function NewTestKey() As String
        Return "test-" & Guid.NewGuid().ToString("N")
    End Function

End Class
