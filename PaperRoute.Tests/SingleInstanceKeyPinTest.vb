Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' One window per library (#77) across versions: a 1.0.x and a 1.1 build
' on the same library must make the same key, or both would open it
' (design risk R5). The literal was worked out from 1.0.1's KeyFor.
<TestClass>
Public Class SingleInstanceKeyPinTest

    <TestMethod>
    Public Sub TheKeyOfADataRootIsUnchanged()

        Assert.AreEqual(
            "PaperRoute-E2257C427B7C778251766553",
            SingleInstanceService.KeyFor("C:\Users\A\AppData\Local\PaperRoute")
        )

    End Sub

End Class
