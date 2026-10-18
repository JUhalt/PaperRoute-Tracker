Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The standing certification fixture (#145) and the chained upgrade test
' that carries it through an update to this build. RootOverride stands in
' for every default root, so nothing here can reach the user's library;
' the class sets it, so it does not run in parallel.
<TestClass>
<DoNotParallelize>
Public Class CertificationFixtureTests

    ' Set to a new or empty folder to write the fixture there for a
    ' certification (WritesTheFixtureForACertificationOnDemand).
    Private Const FixtureFolderVariable As String = "PAPERROUTE_CERTIFICATION_FIXTURE"

    ' The fixture's portable backup, for Restore Backup... on the VM.
    Private Const BackupFileName As String = "PaperRoute-certification-fixture.zip"

    ' What the fixture is for, counted (Inventory).
    Private Const ExpectedInventory As String =
        "manuscripts 4, shortlisted journals 6, with evidence 1, journals 3, journal facts 8, " &
        "AI suggestions 7, submissions 4, decisions 4, reviewer responses 4, " &
        "managed versions 5, linked versions 1, packets 3, managed packet files 6, " &
        "linked packet files 1, listed packet files 2, managed correspondence 3, " &
        "linked correspondence 1, publication matches 1, cited works 2"

    Private _root As String = String.Empty
    Private _defaultRoots As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        _defaultRoots = Path.Combine(_root, "default roots")
        StorageEnvironment.RootOverride = _defaultRoots
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' Ids, dates and file contents are fixed, so the fixture written again
    ' to the same folder is the same, byte for byte. It is never written
    ' over anything: a folder that is not empty is refused, unchanged.
    <TestMethod>
    Public Sub TheFixtureIsTheSameEveryTimeItIsWritten()

        Dim folder As String = Path.Combine(_root, "fixture")

        Dim fixture As CertificationFixture.CertificationLibrary = CertificationFixture.Write(folder)
        Dim first As Dictionary(Of String, Byte()) = SnapshotTree(folder)

        Assert.IsTrue(
            fixture.Manuscripts.All(Function(item) item.Title.StartsWith(CertificationFixture.TitlePrefix, StringComparison.Ordinal)),
            "Every manuscript is named under the ZZZ-CERT- rule.")

        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() CertificationFixture.Write(folder))
        AssertSameTree(first, folder, "A second write over the fixture")

        Directory.Delete(folder, True)
        CertificationFixture.Write(folder)

        AssertSameTree(first, folder, "The fixture written again")

    End Sub


    ' The fixture never goes in or around PaperRoute's own folders, where
    ' its data folder would be the user's. Paths only: nothing is written.
    <TestMethod>
    Public Sub TheFixtureKeepsOutOfPaperRoutesOwnFolders()

        Dim localData As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim documents As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)

        For Each own As String In {
            localData,
            Path.Combine(localData, ProductInfo.DataFolderName),
            Path.Combine(localData, ProductInfo.DevelopmentDataFolderName, "certification"),
            Path.Combine(localData, ProductInfo.LegacyDataFolderName),
            documents,
            Path.Combine(documents, ProductInfo.ManagedLibraryFolderName),
            Path.Combine(documents, ProductInfo.DevelopmentManagedLibraryFolderName),
            Path.Combine(documents, ProductInfo.LegacyManagedLibraryFolderName, "certification")
        }
            Assert.IsTrue(CertificationFixture.OverlapsPaperRoutesFolders(own), own & " is refused.")
        Next

        Assert.IsFalse(CertificationFixture.OverlapsPaperRoutesFolders(_root), "A temporary folder is allowed.")
        Assert.IsFalse(CertificationFixture.OverlapsPaperRoutesFolders(Path.Combine(documents, "PaperRoute certification")), "A folder of its own in Documents is allowed.")

    End Sub


    ' The chained upgrade test. Only the 0.9 releases update straight to 1.0
    ' (#153), and every 0.9 release is Schema 9, so the chain starts there;
    ' the Schema 3 to 9 steps keep their own migration tests. The fixture is
    ' written as a 0.9.x library on disk and goes through what an update to
    ' this build, and a move to a new computer after it, do to that library,
    ' with the services the app uses for each step:
    '
    ' 1. Open: Program.PrepareStorage runs EnsureCurrentStorage before
    '    anything is loaded; the window then loads the manuscripts and the
    '    author library, and the Citations page loads citations.json.
    ' 2. Save: the first change saves the manuscripts and the author library.
    ' 3. Back up: Back Up... writes a portable backup.
    ' 4. Restore into a fresh root: the first start on the new computer makes
    '    an empty library, and Restore Backup... restores over it.
    ' 5. Open that: the restart after a restore starts PaperRoute again.
    '
    ' Every record, journal fact, citations.json, AI suggestion, packet,
    ' version and reviewer response must come through, compared whole as
    ' JSON with each managed copy moved to the new library as a restore moves
    ' it, and every managed and linked file byte for byte. It guards what the
    ' app already does and changes none of it, so it could not fail before it
    ' was written; it fails when a later change drops something on the way.
    <TestMethod>
    Public Sub ASchema9LibraryComesThroughAnUpdateABackupAndARestoreWhole()

        Dim source As CertificationFixture.CertificationLibrary = CertificationFixture.Write(Path.Combine(_root, "0.9 library"))
        Dim schemaPath As String = StorageMigrationService.SchemaFilePath(source.DataRoot)
        Dim marker As Byte() = File.ReadAllBytes(schemaPath)
        Dim managedFiles As Dictionary(Of String, Byte()) = RelativeTree(source.ManagedDirectory)
        Dim linkedFiles As Dictionary(Of String, Byte()) = SnapshotTree(source.LinkedDirectory)

        Assert.AreEqual(CertificationFixture.SchemaVersion, StorageMigrationService.ReadSchemaVersion(schemaPath))

        ' What the 0.9.x library holds, as this build reads it.
        Dim expected As List(Of Manuscript) = New ManuscriptRepository(source.DataDirectory, source.ManagedDirectory).Load()
        Dim expectedLibrary As AuthorLibraryData = New AuthorLibraryRepository(source.DataDirectory).Load()
        Dim expectedCitations As CitationSnapshot = New CitationStore(source.DataDirectory).Load()

        Assert.AreEqual(ExpectedInventory, Inventory(expected, expectedLibrary, expectedCitations), "The fixture holds every kind of record the chain checks.")

        ' 1. Open.
        EnsureStorage(source.DataRoot, source.ManagedDirectory)

        CollectionAssert.AreEqual(marker, File.ReadAllBytes(schemaPath), "A Schema 9 library keeps its marker: there is no step to take.")

        Dim repository As New ManuscriptRepository(source.DataDirectory, source.ManagedDirectory)
        Dim authorRepository As New AuthorLibraryRepository(source.DataDirectory)
        Dim manuscripts As List(Of Manuscript) = repository.Load()
        Dim library As AuthorLibraryData = authorRepository.Load()

        Assert.IsNotNull(New CitationStore(source.DataDirectory).Load())

        ' 2. Save.
        repository.Save(manuscripts)
        authorRepository.Save(library)

        Assert.AreEqual(String.Empty, repository.LastSaveWarning)
        AssertSameRelativeTree(managedFiles, source.ManagedDirectory, "The managed library after the save")

        ' 3. Back up.
        Dim backupPath As String = Path.Combine(_root, "PaperRoute Backup.zip")

        Call New PortableBackupService(source.ManagedDirectory).CreateBackup(backupPath, manuscripts, repository)

        AssertSameRelativeTree(managedFiles, source.ManagedDirectory, "The managed library after the backup")

        ' 4. Restore into a fresh root.
        Dim freshRoot As String = Path.Combine(_root, "new computer", ProductInfo.DataFolderName)
        Dim freshData As String = Path.Combine(freshRoot, "data")
        Dim freshManaged As String = Path.Combine(_root, "new computer", ProductInfo.ManagedLibraryFolderName)

        EnsureStorage(freshRoot, freshManaged)

        Dim freshRepository As New ManuscriptRepository(freshData, freshManaged)
        Dim current As List(Of Manuscript) = freshRepository.Load()

        Assert.AreEqual(0, current.Count, "The new computer starts with an empty library.")

        Dim result As RestoreResult = New PortableRestoreService(freshManaged).RestoreBackup(backupPath, current, freshRepository)

        Assert.AreEqual(expected.Count, result.ManuscriptCount)

        ' 5. Open that.
        EnsureStorage(freshRoot, freshManaged)

        Assert.AreEqual(CertificationFixture.SchemaVersion, StorageMigrationService.ReadSchemaVersion(StorageMigrationService.SchemaFilePath(freshRoot)))

        Dim restored As List(Of Manuscript) = New ManuscriptRepository(freshData, freshManaged).Load()
        Dim restoredLibrary As AuthorLibraryData = New AuthorLibraryRepository(freshData).Load()
        Dim restoredCitations As CitationSnapshot = New CitationStore(freshData).Load()

        ' Everything came through.
        MoveManagedPaths(expected, source.ManagedDirectory, freshManaged)

        Assert.AreEqual(Inventory(expected, expectedLibrary, expectedCitations), Inventory(restored, restoredLibrary, restoredCitations))
        CollectionAssert.AreEqual(expected.Select(Function(item) item.Id).ToList(), restored.Select(Function(item) item.Id).ToList(), "The same manuscripts, in the same order.")

        For index As Integer = 0 To expected.Count - 1
            Assert.AreEqual(Json(expected(index)), Json(restored(index)), expected(index).Title & " came through whole.")
        Next

        Assert.AreEqual(Json(expectedLibrary), Json(restoredLibrary), "The journals with their facts and checklists, the authors and the tag colors came through whole.")
        Assert.AreEqual(Json(expectedCitations), Json(restoredCitations), "citations.json came through whole.")

        AssertSameRelativeTree(managedFiles, freshManaged, "The restored managed library")

        ' A backup holds no linked file, so each stays where it is, unchanged,
        ' and the restored records point at it as before (compared above).
        AssertSameTree(linkedFiles, source.LinkedDirectory, "The linked files")

        Assert.IsFalse(Directory.Exists(_defaultRoots), "Nothing was written to a default root.")

    End Sub


    ' Writes the fixture for a certification into the folder
    ' PAPERROUTE_CERTIFICATION_FIXTURE names, with the portable backup of it
    ' that the certification VM restores. Run on demand only, because it
    ' writes outside the test's folder; Write refuses a folder that is not
    ' empty, and one in or around PaperRoute's own folders.
    <TestMethod>
    Public Sub WritesTheFixtureForACertificationOnDemand()

        Dim named As String = Environment.GetEnvironmentVariable(FixtureFolderVariable)

        If String.IsNullOrWhiteSpace(named) Then
            Assert.Inconclusive($"Writes outside the test's folder; set {FixtureFolderVariable} to a new or empty folder to write the certification fixture there.")
        End If

        Dim folder As String = Path.GetFullPath(named)
        Dim fixture As CertificationFixture.CertificationLibrary = CertificationFixture.Write(folder)

        Call New PortableBackupService(fixture.ManagedDirectory).CreateBackup(
            Path.Combine(folder, BackupFileName),
            fixture.Manuscripts,
            New ManuscriptRepository(fixture.DataDirectory, fixture.ManagedDirectory))

    End Sub


    ' What startup does before anything is loaded (Program.PrepareStorage),
    ' on this test's folders, with no older library beside them.
    Private Sub EnsureStorage(
        dataRoot As String,
        managedDirectory As String
    )

        StorageMigrationService.EnsureCurrentStorage(
            dataRoot,
            Path.Combine(_root, "no legacy data"),
            managedDirectory,
            Path.Combine(_root, "no legacy library"))

    End Sub


    ' The records as a restore leaves them on another computer: each managed
    ' copy at the same place in the new library. Linked files stay put.
    Private Shared Sub MoveManagedPaths(
        manuscripts As List(Of Manuscript),
        fromRoot As String,
        toRoot As String
    )

        Dim moved As Func(Of String, String) =
            Function(filePath) Path.Combine(toRoot, Path.GetRelativePath(fromRoot, filePath))

        For Each manuscript As Manuscript In manuscripts

            For Each version As ManuscriptVersion In manuscript.Versions.Where(Function(item) item.IsManagedCopy)
                version.LocalFilePath = moved(version.LocalFilePath)
            Next

            For Each packetFile As SubmissionPacketFile In manuscript.SubmissionPackets.SelectMany(Function(item) item.Files).Where(Function(item) item.StorageMode = SubmissionPacketFileStorageMode.ManagedCopy)
                packetFile.LocalFilePath = moved(packetFile.LocalFilePath)
            Next

            For Each item As CorrespondenceItem In manuscript.Submissions.SelectMany(Function(entry) entry.Correspondence).Where(Function(entry) entry.IsManagedCopy)
                item.LocalFilePath = moved(item.LocalFilePath)
            Next

        Next

    End Sub


    ' What the fixture is for, counted, so the chain cannot pass on a library
    ' that had lost a kind of record before it began.
    Private Shared Function Inventory(
        manuscripts As List(Of Manuscript),
        library As AuthorLibraryData,
        citations As CitationSnapshot
    ) As String

        Dim submissions As List(Of JournalSubmission) = manuscripts.SelectMany(Function(item) item.Submissions).ToList()
        Dim responses As IEnumerable(Of ReviewerResponseItem) = submissions.SelectMany(Function(item) item.ReviewerResponses).ToList()
        Dim correspondence As IEnumerable(Of CorrespondenceItem) = submissions.SelectMany(Function(item) item.Correspondence).ToList()
        Dim versions As IEnumerable(Of ManuscriptVersion) = manuscripts.SelectMany(Function(item) item.Versions).ToList()
        Dim packetFiles As IEnumerable(Of SubmissionPacketFile) = manuscripts.SelectMany(Function(item) item.SubmissionPackets).SelectMany(Function(item) item.Files).ToList()
        Dim shortlist As IEnumerable(Of JournalCandidate) = manuscripts.SelectMany(Function(item) item.JournalShortlist).ToList()

        Dim suggestions As Integer =
            submissions.SelectMany(Function(item) item.Decisions).Count(Function(item) item.Suggestion IsNot Nothing) +
            responses.Count(Function(item) item.CommentSuggestion IsNot Nothing) +
            responses.Count(Function(item) item.ResponseSuggestion IsNot Nothing) +
            manuscripts.SelectMany(Function(item) item.ReadinessProfiles).SelectMany(Function(item) item.Items).Count(Function(item) item.Suggestion IsNot Nothing) +
            library.Journals.SelectMany(Function(item) item.ReadinessChecklistTemplate).Count(Function(item) item.Suggestion IsNot Nothing)

        Return String.Join(
            ", ",
            $"manuscripts {manuscripts.Count}",
            $"shortlisted journals {shortlist.Count()}",
            $"with evidence {shortlist.Count(Function(item) item.Evidence IsNot Nothing)}",
            $"journals {library.Journals.Count}",
            $"journal facts {library.Journals.Sum(Function(item) item.Facts.Count)}",
            $"AI suggestions {suggestions}",
            $"submissions {submissions.Count}",
            $"decisions {submissions.Sum(Function(item) item.Decisions.Count)}",
            $"reviewer responses {responses.Count()}",
            $"managed versions {versions.Count(Function(item) item.IsManagedCopy)}",
            $"linked versions {versions.Count(Function(item) Not item.IsManagedCopy AndAlso item.LocalFilePath.Length > 0)}",
            $"packets {manuscripts.Sum(Function(item) item.SubmissionPackets.Count)}",
            $"managed packet files {packetFiles.Count(Function(item) item.StorageMode = SubmissionPacketFileStorageMode.ManagedCopy)}",
            $"linked packet files {packetFiles.Count(Function(item) item.StorageMode = SubmissionPacketFileStorageMode.LinkedExternal)}",
            $"listed packet files {packetFiles.Count(Function(item) item.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly)}",
            $"managed correspondence {correspondence.Count(Function(item) item.IsManagedCopy)}",
            $"linked correspondence {correspondence.Count(Function(item) Not item.IsManagedCopy AndAlso item.LocalFilePath.Length > 0)}",
            $"publication matches {manuscripts.Sum(Function(item) item.PublicationMatches.Count)}",
            $"cited works {If(citations?.Works.Count, 0)}")

    End Function


    Private Shared Function Json(Of T)(
        value As T
    ) As String

        Return JsonSerializer.Serialize(value, CreateJsonOptions())

    End Function


    ' Every file and folder under root by its path within root, with each
    ' file's bytes, so libraries in different places compare.
    Private Shared Function RelativeTree(
        root As String
    ) As Dictionary(Of String, Byte())

        Return SnapshotTree(root).ToDictionary(
            Function(pair) Path.GetRelativePath(root, pair.Key),
            Function(pair) pair.Value,
            StringComparer.OrdinalIgnoreCase)

    End Function


    Private Shared Sub AssertSameRelativeTree(
        expected As Dictionary(Of String, Byte()),
        root As String,
        message As String
    )

        Dim actual As Dictionary(Of String, Byte()) = RelativeTree(root)

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message & ": the same files and folders.")

        For Each pair As KeyValuePair(Of String, Byte()) In expected
            CollectionAssert.AreEqual(pair.Value, actual(pair.Key), message & ": " & pair.Key & " is the same, byte for byte.")
        Next

    End Sub

End Class
