Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class StorageEnvironmentTests

    <TestMethod>
    Public Sub DevelopmentProfile_UsesDebuggerByDefault()

        Assert.IsTrue(
            StorageEnvironment.ShouldUseDevelopmentProfile(
                True,
                Nothing
            )
        )

    End Sub


    <TestMethod>
    Public Sub DevelopmentProfile_CanBeForcedByEnvironmentOverride()

        Assert.IsTrue(
            StorageEnvironment.ShouldUseDevelopmentProfile(
                False,
                "true"
            )
        )

        Assert.IsTrue(
            StorageEnvironment.ShouldUseDevelopmentProfile(
                False,
                "1"
            )
        )

    End Sub


    <TestMethod>
    Public Sub StandardProfile_RemainsDefaultOutsideDebugger()

        Assert.IsFalse(
            StorageEnvironment.ShouldUseDevelopmentProfile(
                False,
                Nothing
            )
        )

        Assert.IsFalse(
            StorageEnvironment.ShouldUseDevelopmentProfile(
                False,
                "false"
            )
        )

    End Sub

End Class
