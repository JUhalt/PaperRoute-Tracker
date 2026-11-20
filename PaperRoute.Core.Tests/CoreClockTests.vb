Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
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


    ' Every read of the system clock in PaperRoute.Core goes through
    ' CoreClock (design 6.3), so a test clock reaches all of it. The one
    ' exception is OnlineAccess's Retry-After wait, an interval that is never
    ' stored. Whole-line comments and string literals are skipped; a clock
    ' name in a trailing comment is reported, and is easier to reword than
    ' to allow.
    <TestMethod>
    Public Sub OnlyCoreClockReadsTheSystemClock()

        Dim core As String = Path.Combine(TestRepository.Root(), "PaperRoute.Core")
        Dim files As List(Of String) = SourceFiles(core)
        Assert.IsTrue(files.Count >= 160, $"Only {files.Count} .vb files were found under {core}.")

        Dim clockFile As String = Path.Combine(core, "Seams", "CoreClock.vb")
        Dim allowed As String() = {
            Path.Combine("Services", "OnlineAccess.vb") & "|RetryAfter.Date.Value - DateTimeOffset.UtcNow"
        }

        ' The scan itself works: it finds the three reads CoreClock makes.
        Assert.AreEqual(3, ClockReads(clockFile).Count, "The scan no longer finds CoreClock's own reads.")

        Dim found As New List(Of String)()
        For Each file As String In files
            If String.Equals(file, clockFile, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim relative As String = Path.GetRelativePath(core, file)
            For Each read As (LineNumber As Integer, Text As String) In ClockReads(file)
                Dim isAllowed As Boolean = allowed.Any(
                    Function(entry) entry.StartsWith(relative & "|", StringComparison.OrdinalIgnoreCase) AndAlso
                        read.Text.Contains(entry.Substring(relative.Length + 1), StringComparison.Ordinal))
                If Not isAllowed Then found.Add($"{relative}:{read.LineNumber}: {read.Text}")
            Next
        Next

        Assert.AreEqual(
            0,
            found.Count,
            "Read the clock through CoreClock.Now, CoreClock.UtcNow or CoreClock.Today:" &
            Environment.NewLine & String.Join(Environment.NewLine, found)
        )

    End Sub


    Private Shared ReadOnly ClockRead As New Regex(
        "\b(?:DateTime|DateTimeOffset|DateAndTime)\.(?:Now|UtcNow|Today)\b" &
        "|(?<![\w.])Date\.(?:Now|UtcNow|Today)\b" &
        "|\bTimeProvider\.System\.Get(?:Utc|Local)Now\b" &
        "|\bMicrosoft\.VisualBasic\.(?:Now|Today)\b",
        RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant
    )

    ' A Visual Basic string literal ("" inside it is two adjacent matches).
    Private Shared ReadOnly StringLiteral As New Regex("""[^""]*""", RegexOptions.CultureInvariant)


    Private Shared Function SourceFiles(core As String) As List(Of String)
        Dim skipped As String() = {
            Path.DirectorySeparatorChar & "bin" & Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar & "obj" & Path.DirectorySeparatorChar
        }
        Return Directory.EnumerateFiles(core, "*.vb", SearchOption.AllDirectories).
            Where(Function(file) Not skipped.Any(Function(part) file.Substring(core.Length).Contains(part, StringComparison.OrdinalIgnoreCase))).
            OrderBy(Function(file) file, StringComparer.Ordinal).
            ToList()
    End Function


    Private Shared Function ClockReads(file As String) As List(Of (LineNumber As Integer, Text As String))
        Dim reads As New List(Of (LineNumber As Integer, Text As String))()
        Dim lines As String() = IO.File.ReadAllLines(file)
        For i As Integer = 0 To lines.Length - 1
            Dim text As String = lines(i).Trim()
            If text.StartsWith("'", StringComparison.Ordinal) Then Continue For
            If ClockRead.IsMatch(StringLiteral.Replace(text, """""")) Then reads.Add((i + 1, text))
        Next
        Return reads
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
    ' data; each such line is listed here by its file and its trimmed text,
    ' as many times as it occurs. Whole-line comments and string literals
    ' are skipped, and Seams\ (CoreIds and the PlatformServices default)
    ' is left out.
    <TestMethod>
    Public Sub OnlyTheListedTemporaryNamesUseGuidNewGuid()

        Dim core As String = Path.Combine(TestRepository.Root(), "PaperRoute.Core")
        Dim files As List(Of String) = SourceFiles(core)
        Assert.IsTrue(files.Count >= 160, $"Only {files.Count} .vb files were found under {core}.")

        Dim allowed As String() = {
            Path.Combine("Services", "ExampleLibraryService.vb") & "|Dim root As String = Path.Combine(Path.GetTempPath(), ""PaperRoute-Example-"" & Guid.NewGuid().ToString(""N""))",
            Path.Combine("Services", "ManagedLibraryService.vb") & "|Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "ManagedPacketDeletionService.vb") & "|Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "PacketExportService.vb") & "|System.IO.Path.GetFileName(zipPath) & ""."" & Guid.NewGuid().ToString(""N"").Substring(0, 8) & "".partial"")",
            Path.Combine("Services", "PortableBackupService.vb") & "|""PaperRouteBackup_"" & Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "PortableRestoreService.vb") & "|Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "PortableRestoreService.vb") & "|Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "StorageMigrationService.vb") & "|Guid.NewGuid().ToString(""N"")",
            Path.Combine("Services", "StorageMigrationService.vb") & "|Guid.NewGuid().ToString(""N"")"
        }
        Assert.AreEqual(9, allowed.Length)

        Dim seams As String = Path.Combine(core, "Seams") & Path.DirectorySeparatorChar

        ' The scan itself works: it finds PlatformServices' two defaults.
        Assert.AreEqual(2, NewGuidCalls(Path.Combine(core, "Seams", "PlatformServices.vb")).Count, "The scan no longer finds PlatformServices' Guid.NewGuid defaults.")

        Dim found As New List(Of String)()
        For Each file As String In files
            If file.StartsWith(seams, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim relative As String = Path.GetRelativePath(core, file)
            For Each hit As (LineNumber As Integer, Text As String) In NewGuidCalls(file)
                found.Add(relative & "|" & hit.Text)
            Next
        Next

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
            "Make a new identifier with CoreIds.NewId(); Guid.NewGuid() is left only for the listed temporary names:" &
            Environment.NewLine & String.Join(Environment.NewLine, unlisted)
        )
        Assert.AreEqual(
            0,
            missing.Count,
            "A listed temporary name was not found; take it off the list:" &
            Environment.NewLine & String.Join(Environment.NewLine, missing)
        )

    End Sub


    Private Shared ReadOnly NewGuidCall As New Regex(
        "\bGuid\s*\.\s*NewGuid\b",
        RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant
    )


    Private Shared Function NewGuidCalls(file As String) As List(Of (LineNumber As Integer, Text As String))
        Dim calls As New List(Of (LineNumber As Integer, Text As String))()
        Dim lines As String() = IO.File.ReadAllLines(file)
        For i As Integer = 0 To lines.Length - 1
            Dim text As String = lines(i).Trim()
            If text.StartsWith("'", StringComparison.Ordinal) Then Continue For
            If NewGuidCall.IsMatch(StringLiteral.Replace(text, """""")) Then calls.Add((i + 1, text))
        Next
        Return calls
    End Function

End Class
