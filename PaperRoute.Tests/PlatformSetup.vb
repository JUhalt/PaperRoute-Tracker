Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Run once before PaperRoute.Tests' first test. Core stage 2's step 8
' fills it: it registers the Windows platform (WindowsPlatform.Register,
' the same call as Program.Main), so these tests keep real DPAPI keys, the
' real shell and the real file identity. Until then the core's defaults
' serve: no test here reaches a key protector, a launcher or a file
' identity through PlatformServices yet.
<TestClass>
Public NotInheritable Class PlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Register(context As TestContext)
    End Sub

End Class
