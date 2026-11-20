Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The Windows file identity (core stage 2, step 11), moved with its code
' from PacketExportTests: what packet export compares to tell whether the
' destination is one of the packet's own files reached another way.
<TestClass>
Public Class WindowsFileIdentityTests

    <TestMethod>
    Public Sub FileIdentity_IndexZeroIsUnknown()
        ' Some file servers report 0 as every file's index: two different
        ' files would then look like one, so the identity is unknown and
        ' only the path comparison applies.
        Assert.IsNull(WindowsFileIdentity.IdentityText(&H1234ABCDUI, 0UI, 0UI))
        Assert.AreEqual("1234abcd:0000000000000007", WindowsFileIdentity.IdentityText(&H1234ABCDUI, 0UI, 7UI))
        Assert.AreEqual("1234abcd:0000000100000000", WindowsFileIdentity.IdentityText(&H1234ABCDUI, 1UI, 0UI))
    End Sub

End Class
