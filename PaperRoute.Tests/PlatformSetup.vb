Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Run once before PaperRoute.Tests' first test: it registers the Windows
' platform (WindowsPlatform.Register, the same call as Program.Main), so
' these tests keep real DPAPI keys, as the app has them. The launcher and
' the file identity are not reached through PlatformServices yet; the
' steps that move them register them in WindowsPlatform.Register.
<TestClass>
Public NotInheritable Class PlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Register(context As TestContext)
        WindowsPlatform.Register()
    End Sub

End Class
