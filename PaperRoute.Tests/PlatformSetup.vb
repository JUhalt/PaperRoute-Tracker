Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Run once before PaperRoute.Tests' first test: it registers the Windows
' platform (WindowsPlatform.Register, the same call as Program.Main), so
' these tests keep real DPAPI keys, the real shell launcher and the Windows
' file identity (volume and file index), as the app has them.
<TestClass>
Public NotInheritable Class PlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Register(context As TestContext)
        WindowsPlatform.Register()
    End Sub

End Class
