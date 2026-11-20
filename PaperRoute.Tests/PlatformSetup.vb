Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Run once before PaperRoute.Tests' first test: it registers the Windows
' platform (WindowsPlatform.Register, the same call as Program.Main), so
' these tests keep real DPAPI keys and the real shell launcher, as the app
' has them. The file identity is not reached through PlatformServices yet;
' the step that moves it registers it in WindowsPlatform.Register.
<TestClass>
Public NotInheritable Class PlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Register(context As TestContext)
        WindowsPlatform.Register()
    End Sub

End Class
