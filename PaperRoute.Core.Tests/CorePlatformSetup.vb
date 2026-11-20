Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' Run once around PaperRoute.Core.Tests (core stage 2, design 3.5). Before
' the first test it registers the test platform (the fake key protector and
' the recording launcher; the clock, the ids and the file identity on their
' defaults). After the last test it puts the registry back the same way and
' fails the run if any test reached the real default folder: every core
' test works under a temporary root of its own.
<TestClass>
Public NotInheritable Class CorePlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Initialize(context As TestContext)

        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
        StorageEnvironment.DefaultRootUses = 0

    End Sub


    <AssemblyCleanup>
    Public Shared Sub Cleanup()

        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)

        Assert.AreEqual(
            0,
            StorageEnvironment.DefaultRootUses,
            "A core test reached the user's default storage root. Give it a temporary root " &
            "(StorageEnvironment.RootOverride, or a constructor that takes a folder)."
        )

    End Sub

End Class
