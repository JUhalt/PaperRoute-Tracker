Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The same data on every system (core stage 2, step 14; acceptance A6; plan
' section 3, check 4). A scripted session on the standing certification
' fixture, with a fixed clock and counter ids, writes a library, backs it up
' and restores the backup into a second root, all through the core's own
' services. Its hash list (one line per file written, the SHA-256 of its
' bytes after the one normalisation check 4 allows, and three more that step
' 14 found necessary, each in one named function) must be the same on every
' system, and the same as the golden file recorded from a CI run.
'
' The tests in the category SameDataEverywhere compare with checked-in
' files recorded in UTC with the en-US culture, so they run only on the two
' CI runners (design 7.3); Build-ReleaseCandidate.ps1 leaves them out. Each
' attaches what it made to the test results, which is where a missing
' golden file or backup is taken from (Fixtures\CrossSystem\README.md).
' RootOverride stands in for every default root, so the class does not run
' in parallel.
<TestClass>
<DoNotParallelize>
Public Class CrossSystemSessionTests

    Private Const SameDataEverywhere As String = "SameDataEverywhere"

    ' The checked-in files, copied to the output folder with the project.
    Private Const GoldenFileName As String = "session-hashes.txt"
    Private Const FixtureFolder As String = "PaperRoute.Core.Tests/Fixtures/CrossSystem/"

    ' The prefixes of the hash list: the library the session saved, the
    ' entries of its portable backup, and the library restored from it.
    Private Const SavedPart As String = "saved/"
    Private Const BackupPart As String = "backup/"
    Private Const RestoredPart As String = "restored/"

    ' What check 4's normalisation puts in place of a managed root, and of
    ' the fixture's linked folder (NormaliseTheLinkedFolder).
    Private Const ManagedRootToken As String = "{managed root}"
    Private Const LinkedFolderToken As String = "{linked folder}"

    ' The session's clock: each step at its own fixed instant.
    Private Shared ReadOnly OpenedUtc As New DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)
    Private Shared ReadOnly EditedUtc As DateTimeOffset = OpenedUtc.AddMinutes(20)
    Private Shared ReadOnly VersionAddedUtc As DateTimeOffset = OpenedUtc.AddMinutes(40)
    Private Shared ReadOnly SavedUtc As DateTimeOffset = OpenedUtc.AddMinutes(45)
    Private Shared ReadOnly BackedUpUtc As DateTimeOffset = OpenedUtc.AddHours(1)
    Private Shared ReadOnly RestoredUtc As DateTimeOffset = OpenedUtc.AddDays(1)

    ' The fixture's linked version: its stored path names the folder the
    ' fixture's linked files were written to on the system that made it.
    Private Shared ReadOnly LinkedRevisionDraftId As Guid = CertificationFixture.Id(1021)

    Private _root As String = String.Empty
    Private _defaultRoots As String = String.Empty

    Public Property TestContext As TestContext


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _defaultRoots = Path.Combine(_root, "default roots")
        StorageEnvironment.RootOverride = _defaultRoots
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' Runs anywhere, in any zone: two sessions in two fresh roots write the
    ' same hash list, byte for byte. This is what shows the list holds no
    ' value that changes from run to run.
    <TestMethod>
    Public Sub TwoSessionsInFreshRootsWriteTheSameHashList()

        Dim first As SessionResult = RunSession(Path.Combine(_root, "first"))
        Dim second As SessionResult = RunSession(Path.Combine(_root, "second"))

        Attach("session-hashes-repeatability-" & SystemName() & ".txt", first.HashList)

        Assert.IsTrue(first.HashList.Length > 0)
        AssertSameList(first.HashList, second.HashList, "The second session's list")

        ' The first session's backup, restored into a third root the way the
        ' same-data tests restore a backup made on another system, gives the
        ' same restored library as the session's own restore.
        AssertSameList(
            RestoredLibraryPart(first.HashList),
            RestoreAsElsewhere(first.BackupPath, Path.Combine(_root, "third")),
            "The library restored from the first session's backup, as elsewhere")

        Assert.IsFalse(Directory.Exists(_defaultRoots), "Nothing was written to a default root.")

    End Sub


    ' The session's list is the golden file's: the bytes this system wrote
    ' are the bytes the recording runner wrote.
    <TestMethod>
    <TestCategory(SameDataEverywhere)>
    Public Sub TheSessionWritesTheGoldenHashList()

        AssertRecordingZoneAndCulture()

        Dim sessionRoot As String = Path.Combine(_root, "session")
        Dim session As SessionResult = RunSession(sessionRoot)
        Dim attachment As String = "session-hashes-" & SystemName() & ".txt"
        Attach(attachment, session.HashList)

        ' Every file the session wrote, so a line that differs can be
        ' diffed from the two runners' artifacts.
        Dim filesAttachment As String = AttachFolder("session-files-" & SystemName() & ".zip", sessionRoot)

        AssertSameList(ReadGolden(attachment), session.HashList, "The session's list", filesAttachment)

    End Sub


    ' The backup this system's session made restores here. It is attached,
    ' named for this system, so it can be checked in for the other one.
    <TestMethod>
    <TestCategory(SameDataEverywhere)>
    Public Sub ThisSystemsBackupRestores()

        AssertRecordingZoneAndCulture()

        Dim session As SessionResult = RunSession(Path.Combine(_root, "session"))
        Attach(SystemName() & "-backup.zip", File.ReadAllBytes(session.BackupPath))

        Assert.AreEqual(4, session.RestoredManuscripts, "Every manuscript of the fixture was restored.")
        Assert.IsTrue(
            SplitList(session.HashList).Any(Function(line) line.StartsWith(RestoredPart & ProductInfo.ManagedLibraryFolderName & "/", StringComparison.Ordinal) AndAlso
                                                           line.Contains("/draft-2_", StringComparison.Ordinal)),
            "The version the session added came back with its managed copy.")

        ' Restored again as the other system will restore the checked-in copy.
        AssertSameList(
            RestoredLibraryPart(session.HashList),
            RestoreAsElsewhere(session.BackupPath, Path.Combine(_root, "restored elsewhere")),
            "The library restored from this system's backup, as elsewhere")

    End Sub


    <TestMethod>
    <TestCategory(SameDataEverywhere)>
    Public Sub AWindowsMadeBackupRestoresToTheGoldenLibrary()
        AssertCheckedInBackupRestoresToTheGoldenLibrary("windows", "the Windows Build job's artifact PaperRoute-test-results")
    End Sub


    <TestMethod>
    <TestCategory(SameDataEverywhere)>
    Public Sub ALinuxMadeBackupRestoresToTheGoldenLibrary()
        AssertCheckedInBackupRestoresToTheGoldenLibrary("linux", "the Core on Linux job's artifact PaperRoute-core-linux-results")
    End Sub


    ' A backup made on the named system, checked in, restored here as the
    ' session restores its own, gives the golden file's restored library.
    Private Sub AssertCheckedInBackupRestoresToTheGoldenLibrary(
        madeOn As String,
        artifact As String
    )

        AssertRecordingZoneAndCulture()

        Dim backupName As String = madeOn & "-backup.zip"
        Dim backupPath As String = FixturePath(backupName)

        ' The backup is named first: the golden file may be missing as well.
        Assert.IsTrue(
            File.Exists(backupPath),
            "No backup made on " & madeOn & " is checked in at " & FixtureFolder & backupName & ". " &
            "Take " & backupName & " from the attachments of ThisSystemsBackupRestores in " & artifact &
            " (from a run where that test passed) and check it in there unchanged." &
            If(File.Exists(FixturePath(GoldenFileName)), String.Empty, " The golden hash list " & FixtureFolder & GoldenFileName & " is missing too: see TheSessionWritesTheGoldenHashList."))

        Dim golden As String = ReadGolden("session-hashes-" & SystemName() & ".txt")

        Dim restoredRoot As String = Path.Combine(_root, "restored from " & madeOn)
        Dim restoredList As String = RestoreAsElsewhere(backupPath, restoredRoot)
        Dim filesAttachment As String = AttachFolder("restored-from-" & madeOn & "-on-" & SystemName() & ".zip", restoredRoot)

        AssertSameList(
            RestoredLibraryPart(golden),
            restoredList,
            "The library restored from " & backupName,
            filesAttachment)

        Assert.IsFalse(Directory.Exists(_defaultRoots), "Nothing was written to a default root.")

    End Sub


    ' A backup file restored into a fresh root, as the tests above restore a
    ' checked-in one: the restored library's part of the hash list, with
    ' the linked folder read from the backup itself.
    Private Function RestoreAsElsewhere(
        backupPath As String,
        restoredRoot As String
    ) As String

        Dim restored As Integer = RestoreInto(backupPath, restoredRoot)

        Assert.AreEqual(4, restored, "Every manuscript of the fixture was restored from " & Path.GetFileName(backupPath) & ".")

        Dim roots As New StoredRoots With {
            .ManagedRoots = {ManagedRootOf(restoredRoot)},
            .LinkedFolder = LinkedFolderIn(backupPath)
        }

        Dim lines As New List(Of String)()
        AddLibrary(lines, RestoredPart, restoredRoot, roots)
        Return JoinList(lines)

    End Function


    Private Shared Function RestoredLibraryPart(
        list As String
    ) As String

        Return JoinList(SplitList(list).Where(Function(line) line.StartsWith(RestoredPart, StringComparison.Ordinal)))

    End Function


    ' ---- The session ---------------------------------------------------------

    Private NotInheritable Class SessionResult
        Public Property HashList As String = String.Empty
        Public Property BackupPath As String = String.Empty
        Public Property RestoredManuscripts As Integer
    End Class


    ' The stored strings the normalisation looks for.
    Private NotInheritable Class StoredRoots
        Public Property ManagedRoots As String() = Array.Empty(Of String)()
        Public Property LinkedFolder As String = String.Empty
    End Class


    ' The scripted session, under runRoot, with the services the app uses
    ' for each step (CertificationFixtureTests describes them):
    '
    ' 1. Open the fixture, written as a 0.9.x library: EnsureCurrentStorage,
    '    then the manuscripts, the author library and citations.json.
    ' 2. Edit a manuscript: the draft gets an abstract and is submitted.
    ' 3. Add a version with a managed copy.
    ' 4. Save: the manuscripts, which copies the version into the library
    '    (as Form1.SaveManuscripts does: the author library is not written,
    '    since the session does not change it).
    ' 5. Back up: a portable backup.
    ' 6. Restore it into a second root, as on a new computer, and open that.
    Private Function RunSession(
        runRoot As String
    ) As SessionResult

        Dim clock As New FixedTimeProvider(OpenedUtc)
        PlatformServices.Clock = clock
        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        Dim libraryRoot As String = Path.Combine(runRoot, "library")
        Dim fixture As CertificationFixture.CertificationLibrary = CertificationFixture.Write(libraryRoot)

        ' 1. Open.
        EnsureStorage(runRoot, fixture.DataRoot, fixture.ManagedDirectory)

        Dim repository As New ManuscriptRepository(fixture.DataDirectory, fixture.ManagedDirectory)
        Dim authorRepository As New AuthorLibraryRepository(fixture.DataDirectory)
        Dim manuscripts As List(Of Manuscript) = repository.Load()
        Assert.IsNotNull(authorRepository.Load())
        Assert.IsNotNull(New CitationStore(fixture.DataDirectory).Load())

        ' 2. Edit.
        clock.UtcNow = EditedUtc

        Dim draft As Manuscript = manuscripts.Single(Function(item) item.Id = CertificationFixture.Id(4))
        draft.Metadata.AbstractText = "A fictional abstract, written in the cross-system session."

        Dim submission As New JournalSubmission With {
            .JournalName = draft.TargetJournal,
            .JournalId = draft.TargetJournalId,
            .ManuscriptNumber = "ZZZ-CERT-04-2026",
            .SubmittedDate = CoreClock.Today,
            .Notes = "Submitted in the cross-system session."
        }

        ChronologyProvenanceService.StampCreated(submission)
        draft.Submissions.Add(submission)

        Assert.IsTrue(ManuscriptLifecycleService.ApplySubmission(draft, submission), "The submission moves the draft on.")

        ' 3. Add a version with a managed copy.
        clock.UtcNow = VersionAddedUtc

        Dim incoming As String = Path.Combine(runRoot, "incoming")
        Directory.CreateDirectory(incoming)
        Dim source As String = Path.Combine(incoming, "draft-2.txt")
        File.WriteAllBytes(source, New UTF8Encoding(False).GetBytes("ZZZ-CERT fictional draft 2, as submitted." & vbCrLf))

        ManuscriptVersionService.CreateVersion(
            draft, "Draft 2", "Added in the cross-system session.", source, isManagedCopy:=True, submissionId:=submission.Id)

        ' 4. Save.
        clock.UtcNow = SavedUtc

        repository.Save(manuscripts)

        Assert.AreEqual(String.Empty, repository.LastSaveWarning)

        ' 5. Back up.
        clock.UtcNow = BackedUpUtc

        Dim backupPath As String = Path.Combine(runRoot, "PaperRoute Backup.zip")
        Call New PortableBackupService(fixture.ManagedDirectory).CreateBackup(backupPath, manuscripts, repository)

        ' 6. Restore into a second root, and open it.
        Dim restoredRoot As String = Path.Combine(runRoot, "new computer")
        Dim restored As Integer = RestoreInto(backupPath, restoredRoot)

        ' The list holds every file the session wrote: beside the library
        ' there is only the fixture's linked folder.
        AssertOnlyEntries(
            libraryRoot,
            {ProductInfo.DataFolderName, ProductInfo.ManagedLibraryFolderName, Path.GetFileName(fixture.LinkedDirectory)})

        ' The hash list.
        Dim savedRoots As New StoredRoots With {
            .ManagedRoots = {fixture.ManagedDirectory},
            .LinkedFolder = fixture.LinkedDirectory
        }

        Dim restoredRoots As New StoredRoots With {
            .ManagedRoots = {ManagedRootOf(restoredRoot)},
            .LinkedFolder = fixture.LinkedDirectory
        }

        Dim lines As New List(Of String)()
        AddLibrary(lines, SavedPart, libraryRoot, savedRoots)
        AddBackupEntries(lines, BackupPart, backupPath, savedRoots)
        AddLibrary(lines, RestoredPart, restoredRoot, restoredRoots)

        Dim hashList As String = JoinList(lines)
        AssertTheRestoreIsComplete(hashList)

        Return New SessionResult With {
            .HashList = hashList,
            .BackupPath = backupPath,
            .RestoredManuscripts = restored
        }

    End Function


    ' Step 6 of the session, for its own backup or a checked-in one: the
    ' first start on the new computer makes an empty library, Restore
    ' Backup... restores over it, and the restart opens it. Its clock and
    ' ids start afresh, so a backup made elsewhere restores to the same
    ' bytes as the session's own.
    Private Function RestoreInto(
        backupPath As String,
        restoredRoot As String
    ) As Integer

        PlatformServices.Clock = New FixedTimeProvider(RestoredUtc)
        PlatformServices.NewId = CoreTestPlatform.CounterIds()

        Dim dataRoot As String = Path.Combine(restoredRoot, ProductInfo.DataFolderName)
        Dim dataDirectory As String = Path.Combine(dataRoot, "data")
        Dim managedRoot As String = ManagedRootOf(restoredRoot)

        EnsureStorage(restoredRoot, dataRoot, managedRoot)

        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        Dim current As List(Of Manuscript) = repository.Load()

        Assert.AreEqual(0, current.Count, "The new computer starts with an empty library.")

        Dim result As RestoreResult = New PortableRestoreService(managedRoot).RestoreBackup(backupPath, current, repository)

        EnsureStorage(restoredRoot, dataRoot, managedRoot)

        Assert.AreEqual(result.ManuscriptCount, New ManuscriptRepository(dataDirectory, managedRoot).Load().Count)

        ' Restore stages and rolls back beside the managed folder and
        ' deletes those folders as best effort; none may be left, since
        ' the list holds only the library's two folders.
        AssertOnlyEntries(restoredRoot, {ProductInfo.DataFolderName, ProductInfo.ManagedLibraryFolderName})

        Return result.ManuscriptCount

    End Function


    ' The folder holds exactly the named entries, compared by ordinal name.
    Private Shared Sub AssertOnlyEntries(
        folder As String,
        expected As String()
    )

        Dim actual As String() = Directory.GetFileSystemEntries(folder).Select(Function(entry) Path.GetFileName(entry)).ToArray()

        Dim extra As String() = actual.Except(expected, StringComparer.Ordinal).ToArray()
        Dim missing As String() = expected.Except(actual, StringComparer.Ordinal).ToArray()

        Assert.IsTrue(
            extra.Length = 0 AndAlso missing.Length = 0,
            folder & " holds other entries than the hash list covers. Extra: " & String.Join(" | ", extra) &
            ". Missing: " & String.Join(" | ", missing) & ".")

    End Sub


    ' The restore is complete, without any recorded file: the restored
    ' managed library holds the saved one's files with the same bytes, the
    ' author library and citations are restored unchanged, and the backup
    ' holds every saved managed file with the same bytes.
    Private Shared Sub AssertTheRestoreIsComplete(
        list As String
    )

        Dim hashes As New Dictionary(Of String, String)(StringComparer.Ordinal)
        For Each line As String In SplitList(list)
            Dim tab As Integer = line.LastIndexOf(vbTab, StringComparison.Ordinal)
            hashes.Add(line.Substring(0, tab), line.Substring(tab + 1))
        Next

        Dim savedManaged As String = SavedPart & ProductInfo.ManagedLibraryFolderName & "/"
        Dim restoredManaged As String = RestoredPart & ProductInfo.ManagedLibraryFolderName & "/"

        Dim savedFiles As Dictionary(Of String, String) = FilesUnder(hashes, savedManaged)
        Dim restoredFiles As Dictionary(Of String, String) = FilesUnder(hashes, restoredManaged)

        Dim problems As New List(Of String)()

        For Each saved As KeyValuePair(Of String, String) In savedFiles

            Dim restoredHash As String = Nothing
            If Not restoredFiles.TryGetValue(saved.Key, restoredHash) OrElse Not String.Equals(restoredHash, saved.Value, StringComparison.Ordinal) Then
                problems.Add("not restored as saved: " & restoredManaged & saved.Key)
            End If

            Dim backupHash As String = Nothing
            If Not hashes.TryGetValue(BackupPart & "files/" & saved.Key, backupHash) OrElse Not String.Equals(backupHash, saved.Value, StringComparison.Ordinal) Then
                problems.Add("not backed up as saved: " & BackupPart & "files/" & saved.Key)
            End If

        Next

        For Each restored As String In restoredFiles.Keys.Where(Function(item) Not savedFiles.ContainsKey(item))
            problems.Add("restored but never saved: " & restoredManaged & restored)
        Next

        For Each dataFile As String In {"authors.json", "citations.json"}

            Dim relative As String = ProductInfo.DataFolderName & "/data/" & dataFile
            Dim savedHash As String = Nothing
            Dim restoredHash As String = Nothing

            If Not hashes.TryGetValue(SavedPart & relative, savedHash) OrElse
               Not hashes.TryGetValue(RestoredPart & relative, restoredHash) OrElse
               Not String.Equals(savedHash, restoredHash, StringComparison.Ordinal) Then
                problems.Add("not restored as saved: " & RestoredPart & relative)
            End If

        Next

        Assert.IsTrue(savedFiles.Count > 0, "The session saved managed files.")
        Assert.AreEqual(0, problems.Count, "The restore is not complete: " & String.Join(" | ", problems) & ".")

    End Sub


    Private Shared Function FilesUnder(
        hashes As Dictionary(Of String, String),
        prefix As String
    ) As Dictionary(Of String, String)

        Return hashes.
            Where(Function(item) item.Key.StartsWith(prefix, StringComparison.Ordinal)).
            ToDictionary(Function(item) item.Key.Substring(prefix.Length), Function(item) item.Value, StringComparer.Ordinal)

    End Function


    ' What startup does before anything is loaded (Program.PrepareStorage),
    ' with no older library beside it.
    Private Shared Sub EnsureStorage(
        runRoot As String,
        dataRoot As String,
        managedDirectory As String
    )

        StorageMigrationService.EnsureCurrentStorage(
            dataRoot,
            Path.Combine(runRoot, "no legacy data"),
            managedDirectory,
            Path.Combine(runRoot, "no legacy library"))

    End Sub


    Private Shared Function ManagedRootOf(
        root As String
    ) As String

        Return Path.Combine(root, ProductInfo.ManagedLibraryFolderName)

    End Function


    ' ---- The hash list -------------------------------------------------------

    ' Every file of the library under root (its data folder and its managed
    ' library), by its path within root.
    Private Shared Sub AddLibrary(
        lines As List(Of String),
        part As String,
        root As String,
        roots As StoredRoots
    )

        For Each folder As String In {ProductInfo.DataFolderName, ProductInfo.ManagedLibraryFolderName}

            Dim libraryFolder As String = Path.Combine(root, folder)
            If Not Directory.Exists(libraryFolder) Then Continue For

            For Each filePath As String In Directory.GetFiles(libraryFolder, "*", SearchOption.AllDirectories)

                Dim relative As String = part & Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, "/"c)

                ' A ZIP's container holds the times its files were written
                ' and how they were compressed, so its entries are listed.
                If String.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase) Then
                    AddBackupEntries(lines, relative & "!/", filePath, roots)
                Else
                    lines.Add(HashLine(relative, Normalise(relative, File.ReadAllBytes(filePath), roots)))
                End If

            Next

        Next

    End Sub


    ' Each entry of a ZIP by its name, with the SHA-256 of its contents.
    Private Shared Sub AddBackupEntries(
        lines As List(Of String),
        part As String,
        zipPath As String,
        roots As StoredRoots
    )

        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)

            For Each entry As ZipArchiveEntry In archive.Entries

                ' A folder entry has no contents.
                If entry.FullName.EndsWith("/", StringComparison.Ordinal) Then Continue For

                Dim content As Byte()
                Using stream As Stream = entry.Open(), buffer As New MemoryStream()
                    stream.CopyTo(buffer)
                    content = buffer.ToArray()
                End Using

                Dim relative As String = part & entry.FullName

                If String.Equals(entry.FullName, ExcelExportName, StringComparison.Ordinal) Then
                    AddTheExcelExport(lines, relative & "!/", content, roots)
                Else
                    lines.Add(HashLine(relative, Normalise(relative, content, roots)))
                End If

            Next

        End Using

    End Sub


    Private Shared Function HashLine(
        relative As String,
        content As Byte()
    ) As String

        Return relative & vbTab & Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()

    End Function


    ' Sorted by ordinal path, one line each, every line ending in LF, so the
    ' list's bytes are the same on every system.
    Private Shared Function JoinList(
        lines As IEnumerable(Of String)
    ) As String

        Dim sorted As List(Of String) = lines.ToList()
        sorted.Sort(StringComparer.Ordinal)
        Return String.Concat(sorted.Select(Function(item) item & vbLf))

    End Function


    Private Shared Function SplitList(
        list As String
    ) As String()

        Return list.Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)

    End Function


    ' ---- The normalisation ---------------------------------------------------

    ' A stored LocalFilePath value in a JSON file: the property name, then
    ' the JSON string.
    Private Shared ReadOnly StoredLocalFilePath As New Regex(
        """LocalFilePath""\s*:\s*""(?<value>(?:[^""\\]|\\.)*)""",
        RegexOptions.CultureInvariant)


    ' Plan section 3, check 4: in every stored LocalFilePath value of the
    ' JSON data files (manuscripts.json and its .bak), a managed root becomes
    ' {managed root} and every separator becomes "/". All other bytes are
    ' hashed as written. The only normalisations beyond check 4 are the three
    ' named findings, NormaliseTheLinkedFolder, AddTheExcelExport and
    ' NormaliseTheAppVersion.
    Private Shared Function Normalise(
        relative As String,
        content As Byte(),
        roots As StoredRoots
    ) As Byte()

        If String.Equals(relative, BackupPart & BackupManifest.FileName, StringComparison.Ordinal) Then
            Return NormaliseTheAppVersion(content)
        End If

        Dim extension As String = Path.GetExtension(relative)
        If Not (String.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(extension, ".bak", StringComparison.OrdinalIgnoreCase)) Then
            Return content
        End If

        Dim text As String = Encoding.UTF8.GetString(content)
        If Not StoredLocalFilePath.IsMatch(text) Then Return content

        Dim normalised As String = StoredLocalFilePath.Replace(
            text,
            Function(found As Match)

                Dim raw As Group = found.Groups("value")
                Dim stored As String = JsonSerializer.Deserialize(Of String)("""" & raw.Value & """")
                Dim value As String = stored

                For Each managedRoot As String In roots.ManagedRoots
                    value = ReplacePrefix(value, managedRoot, ManagedRootToken)
                Next

                value = NormaliseTheLinkedFolder(value, roots.LinkedFolder)
                value = value.Replace("\"c, "/"c)

                If String.Equals(value, stored, StringComparison.Ordinal) Then Return found.Value

                Dim encoded As String = JsonSerializer.Serialize(value)
                Return found.Value.Substring(0, raw.Index - found.Index) & encoded.Substring(1, encoded.Length - 2) & """"

            End Function)

        Return Encoding.UTF8.GetBytes(normalised)

    End Function


    ' Beyond check 4 (a finding of step 14): the certification fixture links
    ' files it never copies, and their stored LocalFilePath values begin with
    ' the folder they were written to, under the session's temporary root.
    ' That folder differs from run to run and from system to system as the
    ' managed root does, and check 4 does not name it. It is normalised here,
    ' by name, and nowhere else.
    Private Shared Function NormaliseTheLinkedFolder(
        value As String,
        linkedFolder As String
    ) As String

        Return ReplacePrefix(value, linkedFolder, LinkedFolderToken)

    End Function


    ' Beyond check 4 (a finding of step 14): the backup's manifest,
    ' backup.json, stores the app's version (PortableBackupService,
    ' PacketExportService.AppVersion(), from <Version> in the core's project
    ' file). Hashed as written, it would change the golden list at every
    ' version bump, and every bump would need a recording round. So the
    ' stored value is checked against this build's version and then, in
    ' backup.json and nowhere else, replaced with {app version}.
    Private Const AppVersionToken As String = "{app version}"

    Private Shared Function NormaliseTheAppVersion(
        content As Byte()
    ) As Byte()

        Dim version As String = PacketExportService.AppVersion()
        Dim text As String = Encoding.UTF8.GetString(content)

        Using document As JsonDocument = JsonDocument.Parse(text)
            Assert.AreEqual(
                version,
                document.RootElement.GetProperty("AppVersion").GetString(),
                "backup.json stores this build's version.")
        End Using

        Dim stored As New Regex(
            "(?<name>""AppVersion""\s*:\s*"")" & Regex.Escape(JsonSerializer.Serialize(version).Trim(""""c)) & """",
            RegexOptions.CultureInvariant)

        Assert.AreEqual(1, stored.Matches(text).Count, "backup.json holds one AppVersion value.")

        Return Encoding.UTF8.GetBytes(stored.Replace(text, Function(found As Match) found.Groups("name").Value & AppVersionToken & """"))

    End Function


    ' Beyond check 4 (a finding of step 14): a backup's library.xlsx, the
    ' human-readable export (LibraryExcelExporter, through ClosedXML and
    ' System.IO.Packaging), differs between two runs on one machine with the
    ' clock and the ids fixed. Three things change: the workbook is a ZIP, so
    ' its container holds write times; the packaging layer names the core
    ' properties part and two relationships with fresh random ids
    ' (_rels/.rels and package/services/metadata/core-properties/*.psmdcp)
    ' and stamps that part with the real clock, which no core seam reaches;
    ' and its shared strings hold each managed and linked file's full path,
    ' which is not a LocalFilePath field. So the export is listed here, by
    ' name and nowhere else, as its entries. In _rels/.rels each random
    ' relationship Id becomes {random id} and the core properties part's
    ' random name {core properties}; that part is listed under the same
    ' fixed name, with only its dcterms:created and dcterms:modified text
    ' replaced by {real clock}. In xl/sharedStrings.xml a cell that begins
    ' with a managed root or the linked folder gets the token and "/"
    ' separators, as a stored LocalFilePath does. Every other byte, the
    ' worksheets included, is hashed as written.
    Private Const ExcelExportName As String = "library.xlsx"

    Private Const CorePropertiesFolder As String = "package/services/metadata/core-properties/"

    Private Shared ReadOnly RandomRelationshipId As New Regex(
        "Id=""R[0-9a-f]{16}""", RegexOptions.CultureInvariant)

    Private Shared ReadOnly CorePropertiesName As New Regex(
        "(?<=core-properties/)[0-9a-f]+(?=\.psmdcp)", RegexOptions.CultureInvariant)

    Private Shared ReadOnly RealClockStamp As New Regex(
        "(?<open><dcterms:(?<name>created|modified)\b[^>]*>)[^<]*(?=</dcterms:\k<name>>)", RegexOptions.CultureInvariant)

    Private Shared Sub AddTheExcelExport(
        lines As List(Of String),
        part As String,
        content As Byte(),
        roots As StoredRoots
    )

        Using archive As New ZipArchive(New MemoryStream(content), ZipArchiveMode.Read)

            For Each entry As ZipArchiveEntry In archive.Entries

                If entry.FullName.EndsWith("/", StringComparison.Ordinal) Then Continue For

                Dim bytes As Byte()
                Using stream As Stream = entry.Open(), buffer As New MemoryStream()
                    stream.CopyTo(buffer)
                    bytes = buffer.ToArray()
                End Using

                Dim name As String = entry.FullName

                ' Random names and the real clock, from the packaging layer.
                If String.Equals(name, "_rels/.rels", StringComparison.Ordinal) Then

                    Dim text As String = Encoding.UTF8.GetString(bytes)
                    text = RandomRelationshipId.Replace(text, "Id=""{random id}""")
                    text = CorePropertiesName.Replace(text, "{core properties}")
                    bytes = Encoding.UTF8.GetBytes(text)

                ElseIf name.StartsWith(CorePropertiesFolder, StringComparison.Ordinal) AndAlso
                       name.EndsWith(".psmdcp", StringComparison.Ordinal) Then

                    name = CorePropertiesName.Replace(name, "{core properties}")

                    Dim text As String = Encoding.UTF8.GetString(bytes)
                    text = RealClockStamp.Replace(text, Function(found As Match) found.Groups("open").Value & "{real clock}")
                    bytes = Encoding.UTF8.GetBytes(text)

                ElseIf String.Equals(name, "xl/sharedStrings.xml", StringComparison.Ordinal) Then

                    Dim text As String = Encoding.UTF8.GetString(bytes)

                    Dim folders As New List(Of KeyValuePair(Of String, String))()
                    For Each managedRoot As String In roots.ManagedRoots
                        folders.Add(New KeyValuePair(Of String, String)(managedRoot, ManagedRootToken))
                    Next
                    folders.Add(New KeyValuePair(Of String, String)(roots.LinkedFolder, LinkedFolderToken))

                    For Each folder As KeyValuePair(Of String, String) In folders.Where(Function(item) item.Key.Length > 0)

                        Dim token As String = folder.Value
                        Dim cell As New Regex(
                            "(?<=>)" & Regex.Escape(SecurityElement.Escape(folder.Key)) & "(?<rest>[\\/][^<]*)(?=</)",
                            RegexOptions.CultureInvariant)

                        text = cell.Replace(text, Function(found As Match) token & found.Groups("rest").Value.Replace("\"c, "/"c))

                    Next

                    bytes = Encoding.UTF8.GetBytes(text)

                End If

                lines.Add(HashLine(part & name, bytes))

            Next

        End Using

    End Sub


    ' The value with the folder at its start replaced by the token, when it
    ' starts with that folder and a separator (either one: a path stored on
    ' another system keeps that system's separator).
    Private Shared Function ReplacePrefix(
        value As String,
        folder As String,
        token As String
    ) As String

        If String.IsNullOrEmpty(folder) OrElse value.Length <= folder.Length Then Return value

        If value.StartsWith(folder, StringComparison.Ordinal) AndAlso
           (value(folder.Length) = "\"c OrElse value(folder.Length) = "/"c) Then
            Return token & value.Substring(folder.Length)
        End If

        Return value

    End Function


    ' The fixture's linked folder as the system that made the backup stored
    ' it: the folder of the linked revision draft's stored path, cut at the
    ' last separator of either kind.
    Private Shared Function LinkedFolderIn(
        backupPath As String
    ) As String

        Using archive As ZipArchive = ZipFile.OpenRead(backupPath)

            Dim entry As ZipArchiveEntry = archive.GetEntry("manuscripts.json")
            Assert.IsNotNull(entry, "The backup holds manuscripts.json.")

            Using stream As Stream = entry.Open()

                Using document As JsonDocument = JsonDocument.Parse(stream)

                    For Each manuscript As JsonElement In document.RootElement.EnumerateArray()

                        Dim versions As JsonElement
                        If Not manuscript.TryGetProperty("Versions", versions) Then Continue For

                        For Each version As JsonElement In versions.EnumerateArray()
                            If version.GetProperty("Id").GetGuid() = LinkedRevisionDraftId Then
                                Dim stored As String = version.GetProperty("LocalFilePath").GetString()
                                Return stored.Substring(0, stored.LastIndexOfAny({"\"c, "/"c}))
                            End If
                        Next

                    Next

                End Using

            End Using

        End Using

        Assert.Fail("The backup's manuscripts.json holds the fixture's linked revision draft.")
        Return String.Empty

    End Function


    ' ---- Recording and comparing ---------------------------------------------

    ' The checked-in files are recorded on CI runners in UTC with the en-US
    ' culture: a local date is stored without its offset, and backup-info.txt
    ' writes the local time, so elsewhere the bytes differ by design.
    Private Shared Sub AssertRecordingZoneAndCulture()

        Dim zone As TimeZoneInfo = TimeZoneInfo.Local

        Assert.IsTrue(
            zone.BaseUtcOffset = TimeSpan.Zero AndAlso Not zone.SupportsDaylightSavingTime,
            "The same-data tests (category " & SameDataEverywhere & ") compare bytes recorded in UTC, " &
            "and this machine's time zone is " & zone.Id & " (UTC offset " & zone.BaseUtcOffset.ToString() &
            If(zone.SupportsDaylightSavingTime, ", with daylight saving time", String.Empty) & "). " &
            "They run on the CI runners (TZ=Etc/UTC on Linux, the UTC windows-latest image); elsewhere leave them out with " &
            "--filter ""TestCategory!=" & SameDataEverywhere & """, as Build-ReleaseCandidate.ps1 does.")

        Assert.AreEqual(
            "en-US",
            CultureInfo.CurrentCulture.Name,
            "The same-data tests (category " & SameDataEverywhere & ") compare bytes recorded with the en-US culture, " &
            "and this machine's culture is '" & CultureInfo.CurrentCulture.Name & "' (on Linux, set LANG=en_US.UTF-8).")

    End Sub


    ' The golden hash list, read as bytes; a missing one fails the test,
    ' naming the attachment to take it from.
    Private Shared Function ReadGolden(
        attachment As String
    ) As String

        Dim goldenPath As String = FixturePath(GoldenFileName)

        Assert.IsTrue(
            File.Exists(goldenPath),
            "No golden hash list is checked in at " & FixtureFolder & GoldenFileName & ". " &
            "This run attached the list it made as " & attachment & " to its test results (the artifact " &
            "PaperRoute-test-results of the Windows Build job, PaperRoute-core-linux-results of Core on Linux). " &
            "Check it in there as " & GoldenFileName & ", byte for byte, once the Windows and the Linux runners' lists " &
            "(session-hashes-windows.txt and session-hashes-linux.txt) are identical.")

        Return New UTF8Encoding(False, True).GetString(File.ReadAllBytes(goldenPath))

    End Function


    ' Equal lists, or a message naming every line only one of them has,
    ' and the attachment that holds the files the actual list was made from.
    Private Shared Sub AssertSameList(
        expected As String,
        actual As String,
        message As String,
        Optional filesAttachment As String = Nothing
    )

        If String.Equals(expected, actual, StringComparison.Ordinal) Then Return

        Dim expectedLines As String() = SplitList(expected)
        Dim actualLines As String() = SplitList(actual)

        Assert.Fail(
            message & " differs from the expected one" &
            If(expected.EndsWith(vbLf, StringComparison.Ordinal), String.Empty, " (which does not end in LF, or has CR LF line ends)") &
            ". Expected only: " & String.Join(" | ", expectedLines.Except(actualLines, StringComparer.Ordinal)) &
            ". Actual only: " & String.Join(" | ", actualLines.Except(expectedLines, StringComparer.Ordinal)) & "." &
            If(String.IsNullOrEmpty(filesAttachment), String.Empty,
               " The files this list was made from are attached to this test's results as " & filesAttachment &
               ", to diff with the same attachment of the run that recorded the expected list."))

    End Sub


    Private Shared Function FixturePath(
        name As String
    ) As String

        Return Path.Combine(AppContext.BaseDirectory, "Fixtures", "CrossSystem", name)

    End Function


    ' Written to this test's results folder and added to the test results,
    ' so the folder each CI job uploads (TestResults/) holds it.
    Private Sub Attach(
        name As String,
        content As String
    )

        Attach(name, New UTF8Encoding(False).GetBytes(content))

    End Sub


    Private Sub Attach(
        name As String,
        content As Byte()
    )

        Dim folder As String = TestContext.TestResultsDirectory
        Assert.IsFalse(String.IsNullOrEmpty(folder), "The test run gives a results folder.")
        Directory.CreateDirectory(folder)

        Dim filePath As String = Path.Combine(folder, name)
        File.WriteAllBytes(filePath, content)
        TestContext.AddResultFile(filePath)

    End Sub


    ' A folder's files, zipped under the run's root and attached as name.
    Private Function AttachFolder(
        name As String,
        folder As String
    ) As String

        Dim zipPath As String = Path.Combine(_root, name)
        ZipFile.CreateFromDirectory(folder, zipPath)
        Attach(name, File.ReadAllBytes(zipPath))
        Return name

    End Function


    ' The kind of system, as the attachments and the checked-in backups
    ' are named.
    Private Shared Function SystemName() As String

        If OperatingSystem.IsWindows() Then Return "windows"
        If OperatingSystem.IsLinux() Then Return "linux"
        If OperatingSystem.IsMacOS() Then Return "macos"
        Return "other"

    End Function

End Class
