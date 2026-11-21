Imports System
Imports System.IO
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' The key store asks the registered key protector, or the one it is handed,
' and keeps its file layout and its answers (core stage 2, step 8). The
' DPAPI side is DpapiCompatibilityTests, in PaperRoute.Tests.
<TestClass>
Public Class ProtectedKeyStoreProtectorTests

    Private _directory As String


    <TestInitialize>
    Public Sub Setup()
        _directory = CoreTestSupport.CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
        CoreTestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    <TestMethod>
    Public Sub TheStoreUsesTheRegisteredProtector()

        Dim store As New ProtectedKeyStore(_directory)
        store.SaveFor(ProtectedKeyStore.AssistantEndpoint, "https://assistant.example.org", "test-key-12345678")

        ' The registered protector's bytes, of the origin, a line feed and the key.
        Dim stored As Byte() = File.ReadAllBytes(Path.Combine(_directory, ProtectedKeyStore.AssistantEndpoint & ".key"))
        Assert.AreEqual("https://assistant.example.org" & vbLf & "test-key-12345678",
                        Encoding.UTF8.GetString(CoreTestPlatform.Secrets.Unprotect(stored)))

        Assert.AreEqual("test-key-12345678", store.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://assistant.example.org"))
        Assert.AreEqual("https://assistant.example.org", store.OriginOf(ProtectedKeyStore.AssistantEndpoint))

    End Sub


    <TestMethod>
    Public Sub AStoreHandedAProtectorUsesItInstead()

        Dim own As New TestSecretProtector()
        PlatformServices.Secrets = UnavailableSecretProtector.Instance

        Dim store As New ProtectedKeyStore(_directory, own)
        store.Save(ProtectedKeyStore.OpenAlex, "test-key-12345678")

        Assert.AreEqual("test-key-12345678", store.Load(ProtectedKeyStore.OpenAlex))
        Assert.AreEqual("test-key-12345678",
                        Encoding.UTF8.GetString(own.Unprotect(File.ReadAllBytes(Path.Combine(_directory, ProtectedKeyStore.OpenAlex & ".key")))))

    End Sub


    ' The registered protector is read at each use, so a store made before
    ' the host registered still finds the host's.
    <TestMethod>
    Public Sub TheRegisteredProtectorIsReadAtEachUse()

        PlatformServices.Secrets = UnavailableSecretProtector.Instance
        Dim store As New ProtectedKeyStore(_directory)
        PlatformServices.Secrets = CoreTestPlatform.Secrets

        store.Save(ProtectedKeyStore.OpenAlex, "test-key-12345678")

        Assert.AreEqual("test-key-12345678", store.Load(ProtectedKeyStore.OpenAlex))

    End Sub


    ' With no key protection (the core's default, until a host registers
    ' one) no key can be kept, and a stored key reads as no key.
    <TestMethod>
    Public Sub WithoutKeyProtectionNoKeyIsKeptOrRead()

        Dim keeper As New ProtectedKeyStore(_directory)
        keeper.Save(ProtectedKeyStore.OpenAlex, "test-key-12345678")

        Dim store As New ProtectedKeyStore(_directory, UnavailableSecretProtector.Instance)

        Assert.IsNull(store.Load(ProtectedKeyStore.OpenAlex))
        Assert.IsFalse(store.HasKey(ProtectedKeyStore.OpenAlex))
        Assert.AreEqual(String.Empty, store.OriginOf(ProtectedKeyStore.OpenAlex))
        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() store.Save(ProtectedKeyStore.Anthropic, "test-key-12345678"))
        Assert.IsFalse(File.Exists(Path.Combine(_directory, ProtectedKeyStore.Anthropic & ".key")))

    End Sub


    ' Bytes kept by another account read as no key, as before.
    <TestMethod>
    Public Sub BytesFromAnotherAccountReadAsNoKey()

        File.WriteAllBytes(Path.Combine(_directory, ProtectedKeyStore.OpenAlex & ".key"), Encoding.UTF8.GetBytes("test-key-12345678"))

        Dim store As New ProtectedKeyStore(_directory)

        Assert.IsNull(store.Load(ProtectedKeyStore.OpenAlex))
        Assert.IsFalse(store.HasKey(ProtectedKeyStore.OpenAlex))

    End Sub

End Class
