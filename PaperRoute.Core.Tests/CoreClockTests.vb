Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Reflection.Emit
Imports System.Runtime.CompilerServices
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

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


    ' Today under a test clock is a date with the Kind of DateTime.Today.
    <TestMethod>
    Public Sub AFixedClocksTodayIsADateOfTheSameKindAsDateTimeToday()

        PlatformServices.Clock = New FixedTimeProvider(New DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero))

        Dim value As DateTime = CoreClock.Today

        Assert.AreEqual(TimeSpan.Zero, value.TimeOfDay)
        Assert.AreEqual(DateTime.Today.Kind, value.Kind)

    End Sub


    ' Moving the test clock moves the core's time with it: nothing caches it.
    <TestMethod>
    Public Sub AMovedFixedClockGivesTheNewInstant()

        Dim clock As New FixedTimeProvider(New DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero))
        PlatformServices.Clock = clock

        clock.UtcNow = clock.UtcNow.AddDays(1)

        Assert.AreEqual(New DateTime(2026, 3, 5, 5, 6, 7, DateTimeKind.Utc), CoreClock.UtcNow)

    End Sub


    ' A model's time initialiser reads the core's clock (design 10, step 6).
    <TestMethod>
    Public Sub AManuscriptMadeUnderAFixedClockEnteredItsStageAtTheFixedTime()

        Dim instant As New DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)
        PlatformServices.Clock = New FixedTimeProvider(instant)

        Dim manuscript As New Manuscript()

        Assert.AreEqual(instant.UtcDateTime.ToLocalTime(), manuscript.StageEnteredDate)
        Assert.AreEqual(DateTimeKind.Local, manuscript.StageEnteredDate.Kind)

    End Sub


    ' A writer with plain options keeps the machine's offset (design R8):
    ' the settings file stores the reminder date, a Local value, with the
    ' offset this machine's zone gives it, as 1.0 did; only the library file
    ' drops offsets (CalendarDateJson). The settings writer keeps the date
    ' part (AppSettingsService.Normalize), so the stored value is that
    ' midnight, and its offset is the "zzz" of that value, which is
    ' DateTime.Now's "zzz" on every day but one on which the clocks change.
    ' It holds in any zone: under UTC the offset is "+00:00", never "Z".
    <TestMethod>
    Public Sub TheSettingsWriterStoresALocalTimeWithTheMachinesOffset()

        Dim root As String = CreateTemporaryRoot()

        Try

            Dim written As DateTime = CoreClock.Now
            Assert.AreEqual(DateTimeKind.Local, written.Kind)

            Dim service As New AppSettingsService(root)
            service.Save(New AppSettings With {.LastReminderNotificationDate = written})

            Dim stored As String
            Using document As JsonDocument = JsonDocument.Parse(IO.File.ReadAllText(Path.Combine(root, "settings.json")))
                stored = document.RootElement.GetProperty("LastReminderNotificationDate").GetString()
            End Using

            Dim kept As DateTime = written.Date
            Dim offset As String = kept.ToString("zzz", CultureInfo.InvariantCulture)
            Assert.AreEqual(kept.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) & offset, stored)

            Dim current As DateTime = DateTime.Now
            If TimeZoneInfo.Local.GetUtcOffset(current) = TimeZoneInfo.Local.GetUtcOffset(kept) Then
                StringAssert.EndsWith(stored, current.ToString("zzz", CultureInfo.InvariantCulture))
            End If

            Assert.AreEqual(kept, service.Load().LastReminderNotificationDate)

        Finally

            DeleteTemporaryRoot(root)

        End Try

    End Sub


    ' Every read of the system clock in PaperRoute.Core goes through
    ' CoreClock (design 6.3), so a test clock reaches all of it. The one
    ' exception is OnlineAccess's Retry-After wait, an interval that is never
    ' stored. The scan reads the built assembly's IL, not its source, so it
    ' sees a read however it is written: bare Now or Today, an Imports
    ' alias, [DateTime], a line continuation, an interpolated string, a
    ' lambda or a field initialiser.
    <TestMethod>
    Public Sub OnlyCoreClockReadsTheSystemClock()

        Dim clock As String = GetType(CoreClock).FullName
        Dim platform As String = GetType(PlatformServices).FullName
        Dim allowed As String() = {
            clock & ".get_Now|System.DateTime.get_Now",
            clock & ".get_Now|System.TimeProvider.get_System",
            clock & ".get_Now|System.TimeProvider.GetUtcNow",
            clock & ".get_UtcNow|System.DateTime.get_UtcNow",
            clock & ".get_UtcNow|System.TimeProvider.get_System",
            clock & ".get_UtcNow|System.TimeProvider.GetUtcNow",
            clock & ".get_Today|System.DateTime.get_Today",
            clock & ".get_Today|System.TimeProvider.get_System",
            platform & "..cctor|System.TimeProvider.get_System",
            platform & ".ResetForTests|System.TimeProvider.get_System",
            GetType(OnlineServiceBusyException).FullName & ".FromResponse|System.DateTimeOffset.get_UtcNow"
        }

        AssertOnlyTheseCalls(
            Function(target) IsClockRead(target),
            allowed,
            "Read the clock through CoreClock.Now, CoreClock.UtcNow or CoreClock.Today:"
        )

    End Sub


    Private Shared Function IsClockRead(target As MethodBase) As Boolean
        Dim owner As String = If(target.DeclaringType?.FullName, String.Empty)
        Select Case owner
            Case "System.DateTime"
                Return target.Name = "get_Now" OrElse target.Name = "get_UtcNow" OrElse target.Name = "get_Today"
            Case "System.DateTimeOffset"
                Return target.Name = "get_Now" OrElse target.Name = "get_UtcNow"
            Case "Microsoft.VisualBasic.DateAndTime"
                Return target.Name = "get_Now" OrElse target.Name = "get_Today" OrElse
                    target.Name = "get_TimeOfDay" OrElse target.Name = "get_Timer" OrElse
                    target.Name = "get_DateString" OrElse target.Name = "get_TimeString"
            Case "System.TimeProvider"
                Return target.Name = "get_System" OrElse target.Name = "GetUtcNow" OrElse target.Name = "GetLocalNow"
            Case Else
                Return False
        End Select
    End Function


    ' Fails unless the calls (call, callvirt, newobj, ldftn, ldvirtftn) to
    ' the targets that match, made anywhere in PaperRoute.Core, are exactly
    ' the allowed "site|target" entries, each as many times as it is listed.
    ' A site is the method that holds the call; a call in an async or
    ' iterator method is named by that method, and one in a lambda by its
    ' generated name.
    Private Shared Sub AssertOnlyTheseCalls(matches As Func(Of MethodBase, Boolean), allowed As String(), advice As String)

        Dim scan As CallScan = ScanCore()

        ' The scan itself works: it read the core's IL and resolved its calls.
        Assert.IsTrue(scan.MethodCount >= 1000, $"Only {scan.MethodCount} method bodies were read from PaperRoute.Core.")
        Assert.IsTrue(scan.Calls.Count >= 10000, $"Only {scan.Calls.Count} calls were resolved in PaperRoute.Core.")
        Assert.AreEqual(
            0,
            scan.Unresolved.Count,
            "Calls the scan could not resolve:" & Environment.NewLine & String.Join(Environment.NewLine, scan.Unresolved)
        )

        Dim found As List(Of String) = scan.Calls.
            Where(Function(item) matches(item.Target)).
            Select(Function(item) item.Site & "|" & item.Target.DeclaringType.FullName & "." & item.Target.Name).
            ToList()

        Dim unlisted As List(Of String) = found.ToList()
        For Each entry As String In allowed
            unlisted.Remove(entry)
        Next
        Dim missing As List(Of String) = allowed.ToList()
        For Each entry As String In found
            missing.Remove(entry)
        Next

        Assert.AreEqual(
            0,
            unlisted.Count,
            advice & Environment.NewLine & String.Join(Environment.NewLine, unlisted.OrderBy(Function(entry) entry, StringComparer.Ordinal))
        )
        Assert.AreEqual(
            0,
            missing.Count,
            "A listed call was not found; take it off the list:" &
            Environment.NewLine & String.Join(Environment.NewLine, missing)
        )

    End Sub


    Private NotInheritable Class CallScan
        Public Property MethodCount As Integer
        Public ReadOnly Property Calls As New List(Of (Site As String, Target As MethodBase))()
        Public ReadOnly Property Unresolved As New List(Of String)()
    End Class


    Private Const AllMembers As BindingFlags =
        BindingFlags.DeclaredOnly Or BindingFlags.Public Or BindingFlags.NonPublic Or
        BindingFlags.Instance Or BindingFlags.Static


    Private Shared ReadOnly OneByteOpCodes As New Dictionary(Of Integer, OpCode)()
    Private Shared ReadOnly TwoByteOpCodes As New Dictionary(Of Integer, OpCode)()


    Shared Sub New()
        For Each field As FieldInfo In GetType(OpCodes).GetFields(BindingFlags.Public Or BindingFlags.Static)
            Dim code As OpCode = DirectCast(field.GetValue(Nothing), OpCode)
            Dim value As Integer = code.Value And &HFFFF
            If code.Size = 1 Then
                OneByteOpCodes(value) = code
            Else
                TwoByteOpCodes(value And &HFF) = code
            End If
        Next
    End Sub


    Private Shared Function ScanCore() As CallScan

        Dim scan As New CallScan()

        For Each type As Type In GetType(CoreClock).Assembly.GetTypes()
            Dim methods As IEnumerable(Of MethodBase) =
                type.GetMethods(AllMembers).Cast(Of MethodBase)().
                Concat(type.GetConstructors(AllMembers))
            For Each method As MethodBase In methods
                Dim body As MethodBody = method.GetMethodBody()
                If body Is Nothing Then Continue For
                scan.MethodCount += 1
                ReadCalls(method, body.GetILAsByteArray(), scan)
            Next
        Next

        Return scan

    End Function


    Private Shared Sub ReadCalls(method As MethodBase, il As Byte(), scan As CallScan)

        Dim typeArguments As Type() = If(method.DeclaringType.IsGenericType, method.DeclaringType.GetGenericArguments(), Nothing)
        Dim methodArguments As Type() = If(TypeOf method Is MethodInfo AndAlso method.IsGenericMethod, method.GetGenericArguments(), Nothing)
        Dim site As String = SiteOf(method)

        Dim position As Integer = 0
        Do While position < il.Length

            Dim code As OpCode
            Dim first As Integer = il(position)
            position += 1
            If first = &HFE Then
                code = TwoByteOpCodes(il(position))
                position += 1
            Else
                code = OneByteOpCodes(first)
            End If

            Select Case code.OperandType
                Case OperandType.InlineNone
                Case OperandType.ShortInlineBrTarget, OperandType.ShortInlineI, OperandType.ShortInlineVar
                    position += 1
                Case OperandType.InlineVar
                    position += 2
                Case OperandType.InlineI8, OperandType.InlineR
                    position += 8
                Case OperandType.InlineSwitch
                    Dim count As Integer = BitConverter.ToInt32(il, position)
                    position += 4 + 4 * count
                Case OperandType.InlineMethod
                    Dim token As Integer = BitConverter.ToInt32(il, position)
                    position += 4
                    Try
                        scan.Calls.Add((site, method.Module.ResolveMethod(token, typeArguments, methodArguments)))
                    Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is TypeLoadException OrElse TypeOf ex Is MissingMemberException
                        scan.Unresolved.Add($"{site}: token {token:X8}: {ex.Message}")
                    End Try
                Case Else
                    position += 4
            End Select

        Loop

    End Sub


    ' The method a call belongs to as its author wrote it: an async or
    ' iterator method's state machine is named by that method, and a
    ' compiler-generated class by the class that holds it.
    Private Shared Function SiteOf(method As MethodBase) As String

        Dim type As Type = method.DeclaringType
        Dim name As String = method.Name

        Do While type.DeclaringType IsNot Nothing AndAlso type.IsDefined(GetType(CompilerGeneratedAttribute), False)
            Dim generated As Type = type
            Dim original As MethodInfo = type.DeclaringType.GetMethods(AllMembers).FirstOrDefault(
                Function(candidate) candidate.GetCustomAttributes(Of StateMachineAttribute)(False).Any(
                    Function(attribute) attribute.StateMachineType Is generated OrElse
                        (generated.IsGenericType AndAlso attribute.StateMachineType Is generated.GetGenericTypeDefinition())))
            If original IsNot Nothing Then name = original.Name
            type = type.DeclaringType
        Loop

        Return type.FullName & "." & name

    End Function


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


    ' A model's id initialiser takes the core's ids (design 10, step 7).
    <TestMethod>
    Public Sub AModelMadeUnderCounterIdsTakesTheNextId()

        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        Assert.AreEqual(New Guid("00000001-0000-0000-0000-000000000000"), New ManuscriptVersion().Id)
        Assert.AreEqual(New Guid("00000002-0000-0000-0000-000000000000"), New HistoryEvent().Id)

    End Sub


    ' The managed copy's stored name ends in the first 8 hex digits of a new
    ' id (ManagedLibraryService.CreateUniqueDestinationPath). With counter
    ' ids it is the first counter id's "00000001", so a scripted session
    ' stores the same path every time (design 10, step 7).
    <TestMethod>
    Public Sub WithCounterIdsAManagedCopysSuffixIsTheFirstCounterId()

        Dim root As String = CreateTemporaryRoot()

        Try

            Dim source As String = Path.Combine(root, "draft.docx")
            IO.File.WriteAllText(source, "draft")

            Dim manuscriptId As New Guid("aaaaaaaa-0000-0000-0000-000000000001")
            Dim versionId As New Guid("bbbbbbbb-0000-0000-0000-000000000002")
            Dim version As New ManuscriptVersion() With {
                .Id = versionId,
                .LocalFilePath = source,
                .IsManagedCopy = True
            }
            Dim manuscript As New Manuscript() With {.Id = manuscriptId}
            manuscript.Versions.Add(version)

            Dim libraryRoot As String = Path.Combine(root, "Library")
            Dim library As New ManagedLibraryService(libraryRoot)

            ' Set last, so the copy's name takes the first counter id.
            PlatformServices.NewId = CoreTestPlatform.CounterIds()
            library.CommitManagedCopies({manuscript})

            Dim expected As String = Path.Combine(
                Path.GetFullPath(libraryRoot),
                manuscriptId.ToString("N"),
                "versions",
                versionId.ToString("N"),
                "draft_00000001.docx"
            )

            Assert.AreEqual(expected, version.LocalFilePath)
            Assert.IsTrue(version.IsManagedCopy)
            Assert.IsTrue(IO.File.Exists(expected), expected)
            Assert.AreEqual("draft", IO.File.ReadAllText(expected))

        Finally

            DeleteTemporaryRoot(root)

        End Try

    End Sub


    ' Every new identifier in PaperRoute.Core comes from CoreIds (design
    ' 6.3), so counter ids reach all of them. Guid.NewGuid() is left only
    ' where it names a temporary file or folder that never reaches stored
    ' data; each such method is listed here, once for each call it makes.
    ' Guid.CreateVersion7 counts as well. The scan reads the built
    ' assembly's IL (as OnlyCoreClockReadsTheSystemClock does), so it sees
    ' a call however it is written, in every folder, Seams\ included.
    <TestMethod>
    Public Sub OnlyTheListedTemporaryNamesUseGuidNewGuid()

        Dim platform As String = GetType(PlatformServices).FullName
        Dim newGuid As String = "|System.Guid.NewGuid"
        Dim allowed As String() = {
            platform & "..cctor" & newGuid,
            platform & ".ResetForTests" & newGuid,
            GetType(ExampleLibraryService).FullName & ".StartSession" & newGuid,
            GetType(ManagedLibraryService.ManagedVersionDeletionTransaction).FullName & "..ctor" & newGuid,
            GetType(ManagedPacketDeletionService.ManagedPacketDeletionTransaction).FullName & "..ctor" & newGuid,
            GetType(PacketExportService).FullName & ".WriteZip" & newGuid,
            GetType(PortableBackupService).FullName & ".CreateBackup" & newGuid,
            GetType(PortableRestoreService).FullName & ".Restore" & newGuid,
            GetType(PortableRestoreService).FullName & ".CreateTemporaryDirectory" & newGuid,
            GetType(StorageMigrationService).FullName & ".MigrateApplicationDataIfNeeded" & newGuid,
            GetType(StorageMigrationService).FullName & ".MigrateManagedLibraryIfNeeded" & newGuid
        }
        Assert.AreEqual(11, allowed.Length, "The two PlatformServices defaults and the nine temporary names.")

        AssertOnlyTheseCalls(
            Function(target) target.DeclaringType Is GetType(Guid) AndAlso
                (target.Name = "NewGuid" OrElse target.Name = "CreateVersion7"),
            allowed,
            "Make a new identifier with CoreIds.NewId(); Guid.NewGuid() is left only for the listed temporary names:"
        )

    End Sub

End Class
