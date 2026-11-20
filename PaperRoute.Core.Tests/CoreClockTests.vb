Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline

' CoreClock and CoreIds (core stage 2, design 6.2 and risk R8). With the
' system clock each CoreClock member is the very DateTime call it stands
' for, so a stored date keeps its Kind and its offset; a test clock gives
' its instant in this machine's zone.
<TestClass>
Public Class CoreClockTests

    <TestCleanup>
    Public Sub Cleanup()
        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
    End Sub


    <TestMethod>
    Public Sub TheDefaultClockIsTheSystemClock()

        Assert.AreSame(TimeProvider.System, PlatformServices.Clock)

    End Sub


    <TestMethod>
    Public Sub NowIsDateTimeNowWithItsKind()

        Dim value As DateTime = CoreClock.Now
        Dim system As DateTime = DateTime.Now

        Assert.AreEqual(DateTimeKind.Local, value.Kind)
        Assert.AreEqual(system.Kind, value.Kind)
        Assert.IsTrue(
            Math.Abs((system - value).TotalSeconds) < 1.0,
            $"CoreClock.Now was {value:O}; DateTime.Now was {system:O}."
        )

    End Sub


    <TestMethod>
    Public Sub UtcNowIsDateTimeUtcNowWithItsKind()

        Dim value As DateTime = CoreClock.UtcNow
        Dim system As DateTime = DateTime.UtcNow

        Assert.AreEqual(DateTimeKind.Utc, value.Kind)
        Assert.AreEqual(system.Kind, value.Kind)
        Assert.IsTrue(
            Math.Abs((system - value).TotalSeconds) < 1.0,
            $"CoreClock.UtcNow was {value:O}; DateTime.UtcNow was {system:O}."
        )

    End Sub


    ' Read between two system readings, so a run across midnight passes.
    <TestMethod>
    Public Sub TodayIsDateTimeToday()

        Dim before As DateTime = DateTime.Today
        Dim value As DateTime = CoreClock.Today
        Dim after As DateTime = DateTime.Today

        Assert.AreEqual(before.Kind, value.Kind)
        Assert.IsTrue(
            value = before OrElse value = after,
            $"CoreClock.Today was {value:O}; DateTime.Today was {before:O}, then {after:O}."
        )

    End Sub


    <TestMethod>
    Public Sub AFixedClockGivesItsInstantInThisMachinesZone()

        Dim instant As New DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)
        PlatformServices.Clock = New FixedTimeProvider(instant)

        Assert.AreEqual(instant.UtcDateTime, CoreClock.UtcNow)
        Assert.AreEqual(DateTimeKind.Utc, CoreClock.UtcNow.Kind)

        Assert.AreEqual(instant.UtcDateTime.ToLocalTime(), CoreClock.Now)
        Assert.AreEqual(DateTimeKind.Local, CoreClock.Now.Kind)

        Assert.AreEqual(instant.UtcDateTime.ToLocalTime().Date, CoreClock.Today)

    End Sub


    <TestMethod>
    Public Sub TheDefaultIdsAreNewGuids()

        Dim first As Guid = CoreIds.NewId()
        Dim second As Guid = CoreIds.NewId()

        Assert.AreNotEqual(Guid.Empty, first)
        Assert.AreNotEqual(first, second)
        Assert.AreEqual(4, first.Version)

    End Sub


    <TestMethod>
    Public Sub CounterIdsCountFromOne()

        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        Assert.AreEqual(New Guid("00000001-0000-0000-0000-000000000000"), CoreIds.NewId())
        Assert.AreEqual(New Guid("00000002-0000-0000-0000-000000000000"), CoreIds.NewId())

    End Sub


    <TestMethod>
    Public Sub ResetForTestsPutsTheClockAndTheIdsBack()

        PlatformServices.Clock = New FixedTimeProvider(DateTimeOffset.UnixEpoch)
        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)

        Assert.AreSame(TimeProvider.System, PlatformServices.Clock)
        Assert.AreNotEqual(New Guid("00000001-0000-0000-0000-000000000000"), CoreIds.NewId())

    End Sub

End Class
