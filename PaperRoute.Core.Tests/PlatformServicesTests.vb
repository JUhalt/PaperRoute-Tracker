Imports System
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline

' The registry and its safe defaults (core stage 2, design 3.2 to 3.5).
<TestClass>
Public Class PlatformServicesTests

    <TestCleanup>
    Public Sub Cleanup()
        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
    End Sub


    <TestMethod>
    Public Sub ThisSuiteRunsOnTheTestPlatform()

        Assert.AreSame(CoreTestPlatform.Secrets, PlatformServices.Secrets)
        Assert.AreSame(CoreTestPlatform.Launcher, PlatformServices.Launcher)
        Assert.AreSame(NoFileIdentity.Instance, PlatformServices.FileIdentity)

    End Sub


    <TestMethod>
    Public Sub TheDefaultKeyProtectorKeepsNoKey()

        Dim protector As ISecretProtector = UnavailableSecretProtector.Instance

        Assert.IsFalse(protector.IsAvailable)
        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() protector.Protect(Encoding.UTF8.GetBytes("key")))
        ' The key store reads this exception as "no key".
        Assert.ThrowsExactly(Of CryptographicException)(
            Sub() protector.Unprotect(New Byte() {1, 2, 3}))

    End Sub


    <TestMethod>
    Public Sub TheDefaultLauncherOpensNothing()

        Dim launcher As ILauncher = UnavailableLauncher.Instance

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() launcher.OpenLink("https://example.org"))
        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() launcher.OpenFile("draft.docx"))

    End Sub


    <TestMethod>
    Public Sub TheDefaultFileIdentityIsNeverKnown()

        Assert.IsNull(NoFileIdentity.Instance.Identify("draft.docx"))

    End Sub


    <TestMethod>
    Public Sub NothingCannotBeRegistered()

        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PlatformServices.Secrets = Nothing)
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PlatformServices.Launcher = Nothing)
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PlatformServices.FileIdentity = Nothing)
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PlatformServices.Clock = Nothing)
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PlatformServices.NewId = Nothing)

        Assert.AreSame(CoreTestPlatform.Secrets, PlatformServices.Secrets)
        Assert.AreSame(CoreTestPlatform.Launcher, PlatformServices.Launcher)

    End Sub


    <TestMethod>
    Public Sub ResetForTestsPutsEveryMemberBack()

        PlatformServices.Secrets = UnavailableSecretProtector.Instance
        PlatformServices.Launcher = UnavailableLauncher.Instance
        PlatformServices.FileIdentity = New KnownFileIdentity()
        PlatformServices.Clock = New FixedTimeProvider(DateTimeOffset.UnixEpoch)
        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)

        Assert.AreSame(CoreTestPlatform.Secrets, PlatformServices.Secrets)
        Assert.AreSame(CoreTestPlatform.Launcher, PlatformServices.Launcher)
        Assert.AreSame(NoFileIdentity.Instance, PlatformServices.FileIdentity)
        Assert.AreSame(TimeProvider.System, PlatformServices.Clock)
        Assert.AreNotEqual(New Guid("00000001-0000-0000-0000-000000000000"), CoreIds.NewId())

    End Sub


    ' Knows every file as "x": only to move FileIdentity off its default.
    Private NotInheritable Class KnownFileIdentity
        Implements IFileIdentity

        Public Function Identify(filePath As String) As String Implements IFileIdentity.Identify
            Return "x"
        End Function

    End Class


    <TestMethod>
    Public Sub TheTestKeyProtectorRoundTripsAndRefusesForeignBytes()

        Dim clear As Byte() = Encoding.UTF8.GetBytes("https://api.openalex.org" & vbLf & "abc123")
        Dim stored As Byte() = CoreTestPlatform.Secrets.Protect(clear)

        Assert.AreEqual("TEST", Encoding.ASCII.GetString(stored, 0, 4))
        CollectionAssert.AreNotEqual(clear, stored)
        CollectionAssert.AreEqual(clear, CoreTestPlatform.Secrets.Unprotect(stored))
        Assert.ThrowsExactly(Of CryptographicException)(
            Sub() CoreTestPlatform.Secrets.Unprotect(clear))

    End Sub


    <TestMethod>
    Public Sub TheRecordingLauncherRecordsAndStartsNothing()

        CoreTestPlatform.Launcher.Clear()

        PlatformServices.Launcher.OpenLink("https://example.org")
        PlatformServices.Launcher.OpenFile("draft.docx")

        CollectionAssert.AreEqual({"https://example.org"}, CoreTestPlatform.Launcher.Links)
        CollectionAssert.AreEqual({"draft.docx"}, CoreTestPlatform.Launcher.Files)
        CoreTestPlatform.Launcher.Clear()

    End Sub

End Class
