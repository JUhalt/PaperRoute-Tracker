Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Run once before PaperRoute.Core.Tests' first test. Core stage 2's step 5
' fills it: it registers the test platform (the fake key store and the
' recording launcher) and, at the end, checks that no test touched the real
' default folder. Until then the core needs nothing set up, so it is empty.
<TestClass>
Public NotInheritable Class CorePlatformSetup

    <AssemblyInitialize>
    Public Shared Sub Initialize(context As TestContext)
    End Sub

End Class