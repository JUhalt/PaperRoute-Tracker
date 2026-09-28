Imports System
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' One PaperRoute window per library (#77).
<TestClass>
Public Class SingleInstanceTests

    <TestMethod>
    Public Sub ASecondLaunchOfTheSameLibrarySignalsTheFirstInsteadOfOpening()
        Dim key As String = SingleInstanceService.KeyFor("C:\Test\PaperRoute-" & Guid.NewGuid().ToString("N"))
        Dim activated As New ManualResetEventSlim(False)

        Using first As SingleInstanceService = SingleInstanceService.TryStart(key)
            Assert.IsNotNull(first)
            first.Listen(Sub() activated.Set())

            Assert.IsNull(SingleInstanceService.TryStart(key), "The library is already open.")
            Assert.IsTrue(SingleInstanceService.SignalExisting(key))
            Assert.IsTrue(activated.Wait(TimeSpan.FromSeconds(5)), "The running window is asked to come forward.")

            Dim otherLibrary As SingleInstanceService = SingleInstanceService.TryStart(SingleInstanceService.KeyFor("C:\Test\Another-" & Guid.NewGuid().ToString("N")))
            Assert.IsNotNull(otherLibrary, "A different library, such as the development profile, opens separately.")
            otherLibrary.Dispose()
        End Using

        Using again As SingleInstanceService = SingleInstanceService.TryStart(key)
            Assert.IsNotNull(again, "Closing the first window frees the library.")
        End Using
        Assert.IsFalse(SingleInstanceService.SignalExisting(SingleInstanceService.KeyFor("C:\Nobody\" & Guid.NewGuid().ToString("N"))))
    End Sub

    <TestMethod>
    Public Sub TheKeyIgnoresCaseAndTrailingSeparators()
        Assert.AreEqual(SingleInstanceService.KeyFor("C:\Users\A\AppData\Local\PaperRoute\"), SingleInstanceService.KeyFor("c:\users\a\appdata\local\paperroute"))
        Assert.AreNotEqual(SingleInstanceService.KeyFor("C:\Users\A\AppData\Local\PaperRoute"), SingleInstanceService.KeyFor("C:\Users\A\AppData\Local\PaperRoute-Dev"))
    End Sub

End Class
