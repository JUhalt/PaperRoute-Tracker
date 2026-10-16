Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class BackupRestoreTests

    Private _root As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub

    <TestMethod>
    Public Sub Backup_CreatesPortableArchiveWithExpectedContents()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()

        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Dim service As New PortableBackupService(managedDirectory)
        service.CreateBackup(backupPath, manuscripts, repository)

        Assert.IsTrue(File.Exists(backupPath))

        Using archive As ZipArchive = ZipFile.OpenRead(backupPath)
            Assert.IsNotNull(archive.GetEntry("manuscripts.json"))
            Assert.IsNotNull(archive.GetEntry("library.xlsx"))
            Assert.IsNotNull(archive.GetEntry("backup-info.txt"))
            Assert.IsTrue(
                archive.Entries.Any(
                    Function(entry) entry.FullName.Replace("\\", "/").StartsWith("files/", StringComparison.OrdinalIgnoreCase)
                )
            )
        End Using

        Dim inspection As BackupInspection = New PortableRestoreService(managedDirectory).InspectBackup(backupPath)
        Assert.AreEqual(3, inspection.ManuscriptCount)
        Assert.AreEqual(2, inspection.SubmissionCount)
        Assert.AreEqual(2, inspection.DecisionCount)
        Assert.AreEqual(1, inspection.CorrespondenceCount)
        Assert.AreEqual(1, inspection.ManagedFileCount)

    End Sub

    ' A decision's notes can hold a whole decision letter (#84), longer than
    ' an Excel cell holds: the workbook cuts it short and says so, and the
    ' backup keeps all of it.
    <TestMethod>
    Public Sub Backup_KeepsANoteLongerThanAnExcelCell()

        Dim managedDirectory As String = Path.Combine(_root, "long-library")
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "long-data"), managedDirectory)
        Dim longNote As String = String.Concat(Enumerable.Repeat("A sentence of a long decision letter. ", 1600)).Trim()
        Assert.IsTrue(longNote.Length > 60000)

        Dim manuscript As New Manuscript With {
            .Title = "Long Letter Study",
            .CurrentStage = PaperStage.Revision,
            .Location = ManuscriptLocation.Pipeline
        }
        Dim submission As New JournalSubmission With {.JournalName = "Fictional Journal of Psychology", .SubmittedDate = New DateTime(2026, 6, 1)}
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 9, 15), .Notes = longNote})
        manuscript.Submissions.Add(submission)
        Dim manuscripts As New List(Of Manuscript) From {manuscript}
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "long.zip")
        Call New PortableBackupService(managedDirectory).CreateBackup(backupPath, manuscripts, repository)

        Dim targetManaged As String = Path.Combine(_root, "long-target-library")
        Dim targetRepository As New ManuscriptRepository(Path.Combine(_root, "long-target-data"), targetManaged)
        Dim current As New List(Of Manuscript)()
        targetRepository.Save(current)
        Call New PortableRestoreService(targetManaged).RestoreBackup(backupPath, current, targetRepository)
        Assert.AreEqual(longNote, targetRepository.Load().Single().Submissions(0).Decisions(0).Notes, "The backup keeps the whole note.")

        Dim workbook As String = Path.Combine(_root, "long.xlsx")
        Call New LibraryExcelExporter().Export(workbook, manuscripts)
        Dim cell As String = New StandardExcelImporter().Import(workbook).Manuscripts.Single().Submissions(0).Decisions(0).Notes
        Assert.IsTrue(cell.Length <= LibraryExcelExporter.MaximumCellLength AndAlso cell.Length > 32000)
        StringAssert.StartsWith(cell, "A sentence of a long decision letter. A sentence")
        StringAssert.EndsWith(cell, "[cut short here: an Excel cell holds 32,767 characters]")

        Assert.AreEqual("A short note.", LibraryExcelExporter.CellText("A short note."))
        Assert.IsNull(LibraryExcelExporter.CellText(Nothing))
        Assert.AreEqual(LibraryExcelExporter.MaximumCellLength, LibraryExcelExporter.CellText(New String("n"c, 32767)).Length, "A full cell is left alone.")

        ' The cut never falls between the two halves of one character.
        Dim keep As Integer = LibraryExcelExporter.MaximumCellLength - LibraryExcelExporter.CutShortNote.Length
        Dim paired As String = LibraryExcelExporter.CellText(New String("n"c, keep - 1) & Char.ConvertFromUtf32(&H1D45D) & New String("n"c, 40000))
        Assert.AreEqual(New String("n"c, keep - 1) & LibraryExcelExporter.CutShortNote, paired)
        Assert.IsFalse(paired.Any(Function(character) Char.IsSurrogate(character)))

    End Sub

    <TestMethod>
    Public Sub Restore_RoundTripsLibraryAndRewritesManagedPaths()

        Dim sourceData As String = Path.Combine(_root, "source-data")
        Dim sourceManaged As String = Path.Combine(_root, "source-library")
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceManaged)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()

        AddManagedFixture(sourceManuscripts, sourceManaged)
        sourceRepository.Save(sourceManuscripts)

        Dim backupPath As String = Path.Combine(_root, "roundtrip.zip")
        Dim backupService As New PortableBackupService(sourceManaged)
        backupService.CreateBackup(backupPath, sourceManuscripts, sourceRepository)

        Dim targetData As String = Path.Combine(_root, "target-data")
        Dim targetManaged As String = Path.Combine(_root, "target-library")
        Dim targetRepository As New ManuscriptRepository(targetData, targetManaged)
        Dim current As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Current Before Restore",
                .CurrentStage = PaperStage.Draft,
                .Location = ManuscriptLocation.Pipeline
            }
        }

        targetRepository.Save(current)

        Dim result As RestoreResult =
            New PortableRestoreService(targetManaged).RestoreBackup(
                backupPath,
                current,
                targetRepository
            )

        Dim restored As List(Of Manuscript) = targetRepository.Load()

        Assert.AreEqual(3, result.ManuscriptCount)
        Assert.AreEqual(3, restored.Count)
        Assert.IsTrue(File.Exists(result.EmergencyBackupPath))

        Dim active As Manuscript = restored.Single(Function(item) item.Title = "Active Study")
        Dim correspondence As CorrespondenceItem = active.Submissions(0).Correspondence(0)

        Assert.IsTrue(correspondence.IsManagedCopy)
        Assert.IsTrue(File.Exists(correspondence.LocalFilePath))
        Assert.IsTrue(
            Path.GetFullPath(correspondence.LocalFilePath).StartsWith(
                Path.GetFullPath(targetManaged),
                StringComparison.OrdinalIgnoreCase
            )
        )

    End Sub

    ' A backup made before #118 carries the offsets its machine wrote. Restored
    ' in another zone, its dates stay where they were written: 9 October at
    ' +14:00 and at -12:00 is 9 October here too, in the file and on load.
    ' One of the two is a day off under the default reader unless this host
    ' sits at that offset, so the test fails somewhere for everyone.
    <TestMethod>
    Public Sub Restore_KeepsCalendarDatesWhereTheBackupWroteThem()

        Dim staging As String = Path.Combine(_root, "zones-staging")
        Directory.CreateDirectory(staging)

        File.WriteAllText(
            Path.Combine(staging, "manuscripts.json"),
            "[" & vbLf &
            "  {" & vbLf &
            "    ""Title"": ""Zones""," & vbLf &
            "    ""StageEnteredDate"": ""2026-10-09T00:00:00+14:00""," & vbLf &
            "    ""Submissions"": [" & vbLf &
            "      {" & vbLf &
            "        ""JournalName"": ""Journal of Zones""," & vbLf &
            "        ""SubmittedDate"": ""2026-10-09T00:00:00+14:00""," & vbLf &
            "        ""FollowUpDate"": ""2026-10-09T00:00:00-12:00""," & vbLf &
            "        ""RecordedAtUtc"": ""2026-10-08T10:00:00Z""," & vbLf &
            "        ""Decisions"": [" & vbLf &
            "          { ""DecisionDate"": ""2026-10-09T00:00:00-12:00"", ""Decision"": ""Rejected"" }" & vbLf &
            "        ]" & vbLf &
            "      }" & vbLf &
            "    ]" & vbLf &
            "  }" & vbLf &
            "]"
        )

        Dim backupPath As String = Path.Combine(_root, "zones.zip")
        ZipFile.CreateFromDirectory(staging, backupPath)

        Dim targetManaged As String = Path.Combine(_root, "zones-library")
        Dim targetRepository As New ManuscriptRepository(Path.Combine(_root, "zones-data"), targetManaged)
        Dim current As New List(Of Manuscript)()
        targetRepository.Save(current)

        Call New PortableRestoreService(targetManaged).RestoreBackup(backupPath, current, targetRepository)

        Dim json As String = File.ReadAllText(targetRepository.DataFilePath)
        StringAssert.Contains(json, """SubmittedDate"": ""2026-10-09T00:00:00""", "The restored file holds the date as the backup wrote it, with no offset.")
        StringAssert.Contains(json, """DecisionDate"": ""2026-10-09T00:00:00""", "The restored file holds the date as the backup wrote it, with no offset.")
        StringAssert.Contains(json, """RecordedAtUtc"": ""2026-10-08T10:00:00Z""", "The audit stamp stays an instant.")

        Dim restored As Manuscript = targetRepository.Load().Single()
        Dim submission As JournalSubmission = restored.Submissions(0)
        Dim ninth As New DateTime(2026, 10, 9)

        Assert.AreEqual(ninth, restored.StageEnteredDate.Date, "StageEnteredDate written at +14:00")
        Assert.AreEqual(ninth, submission.SubmittedDate.Date, "SubmittedDate written at +14:00")
        Assert.AreEqual(ninth, submission.FollowUpDate.Value, "FollowUpDate written at -12:00, cut to the day on load")
        Assert.AreEqual(ninth, submission.Decisions(0).DecisionDate.Date, "DecisionDate written at -12:00")
        Assert.AreEqual(New DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc), submission.RecordedAtUtc.Value)

    End Sub

    <TestMethod>
    Public Sub InspectBackup_RejectsArchiveWithoutManuscriptsJson()

        Dim badBackup As String = Path.Combine(_root, "invalid-backup.zip")
        Dim staging As String = Path.Combine(_root, "bad-staging")
        Directory.CreateDirectory(staging)
        File.WriteAllText(Path.Combine(staging, "readme.txt"), "Not a PaperRoute backup.")
        ZipFile.CreateFromDirectory(staging, badBackup)

        Dim restoreService As New PortableRestoreService(Path.Combine(_root, "managed"))

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As BackupInspection = restoreService.InspectBackup(badBackup)
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Backup_IncludesReusableAuthorLibraryWhenPresent()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "author-backup-data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "author-backup-library"
            )

        Dim manuscriptRepository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        manuscriptRepository.Save(
            manuscripts
        )

        Dim authorRepository As New AuthorLibraryRepository(
            dataDirectory
        )

        Dim authorLibrary As New AuthorLibraryData()

        authorLibrary.Authors.Add(
            New AuthorRecord With {
                .GivenName = "Jane",
                .FamilyName = "Researcher"
            }
        )

        authorRepository.Save(
            authorLibrary
        )

        Dim backupPath As String =
            Path.Combine(
                _root,
                "authors-backup.zip"
            )

        Dim backupService As New PortableBackupService(
            managedDirectory
        )

        backupService.CreateBackup(
            backupPath,
            manuscripts,
            manuscriptRepository
        )

        Using archive As ZipArchive =
            ZipFile.OpenRead(
                backupPath
            )

            Assert.IsNotNull(
                archive.GetEntry(
                    "authors.json"
                )
            )

        End Using

    End Sub


    <TestMethod>
    Public Sub Restore_RestoresReusableAuthorLibraryWhenPresent()

        Dim sourceData As String =
            Path.Combine(
                _root,
                "source-author-data"
            )

        Dim sourceManaged As String =
            Path.Combine(
                _root,
                "source-author-library"
            )

        Dim sourceRepository As New ManuscriptRepository(
            sourceData,
            sourceManaged
        )

        Dim sourceManuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        sourceRepository.Save(
            sourceManuscripts
        )

        Dim sourceAuthorRepository As New AuthorLibraryRepository(
            sourceData
        )

        Dim sourceAuthorLibrary As New AuthorLibraryData()

        sourceAuthorLibrary.Authors.Add(
            New AuthorRecord With {
                .GivenName = "Jane",
                .FamilyName = "Researcher",
                .Orcid = "0000-0000-0000-0000"
            }
        )

        sourceAuthorLibrary.Affiliations.Add(
            New AffiliationRecord With {
                .Institution = "Example University"
            }
        )

        sourceAuthorRepository.Save(
            sourceAuthorLibrary
        )

        Dim backupPath As String =
            Path.Combine(
                _root,
                "authors-roundtrip.zip"
            )

        Dim backupService As New PortableBackupService(
            sourceManaged
        )

        backupService.CreateBackup(
            backupPath,
            sourceManuscripts,
            sourceRepository
        )

        Dim targetData As String =
            Path.Combine(
                _root,
                "target-author-data"
            )

        Dim targetManaged As String =
            Path.Combine(
                _root,
                "target-author-library"
            )

        Dim targetRepository As New ManuscriptRepository(
            targetData,
            targetManaged
        )

        Dim current As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Before restore"
            }
        }

        targetRepository.Save(
            current
        )

        Dim restoreService As New PortableRestoreService(
            targetManaged
        )

        restoreService.RestoreBackup(
            backupPath,
            current,
            targetRepository
        )

        Dim targetAuthorRepository As New AuthorLibraryRepository(
            targetData
        )

        Dim restoredAuthors As AuthorLibraryData =
            targetAuthorRepository.Load()

        Assert.AreEqual(
            1,
            restoredAuthors.Authors.Count
        )

        Assert.AreEqual(
            "Jane Researcher",
            restoredAuthors.Authors(0).DisplayName
        )

        Assert.AreEqual(
            1,
            restoredAuthors.Affiliations.Count
        )

        Assert.AreEqual(
            "Example University",
            restoredAuthors.Affiliations(0).Institution
        )

    End Sub


    ' A write that fails after the destination existed leaves the previous
    ' backup byte-identical (#114): the ZIP is written beside it and takes
    ' its place only once it is finished.
    <TestMethod>
    Public Sub Backup_FailedWriteKeepsThePreviousBackup()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Dim service As New PortableBackupService(managedDirectory)
        service.CreateBackup(backupPath, manuscripts, repository)
        Dim previous As Byte() = File.ReadAllBytes(backupPath)

        manuscripts(0).Title = "Changed since the last backup"
        repository.Save(manuscripts)

        ' Another program holds the name the ZIP is written under.
        Using holder As New FileStream(backupPath & ".partial", FileMode.Create, FileAccess.ReadWrite, FileShare.None)
            Assert.ThrowsExactly(Of IOException)(Sub() service.CreateBackup(backupPath, manuscripts, repository))
        End Using

        CollectionAssert.AreEqual(previous, File.ReadAllBytes(backupPath), "The previous backup is untouched.")

    End Sub


    ' A ZIP that fails inspection never takes the destination's place, and
    ' the temporary file is gone. Here a managed file is missing from disk,
    ' so the archive could not be restored.
    <TestMethod>
    Public Sub Backup_ZipThatFailsInspectionKeepsThePreviousBackupAndLeavesNoPartial()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Dim service As New PortableBackupService(managedDirectory)
        service.CreateBackup(backupPath, manuscripts, repository)
        Dim previous As Byte() = File.ReadAllBytes(backupPath)

        File.Delete(manuscripts(0).Submissions(0).Correspondence(0).LocalFilePath)

        Assert.ThrowsExactly(Of InvalidDataException)(Sub() service.CreateBackup(backupPath, manuscripts, repository))

        CollectionAssert.AreEqual(previous, File.ReadAllBytes(backupPath), "The previous backup is untouched.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.partial").Length, "No partial file is left.")

    End Sub


    ' A library larger than Restore accepts is reported at backup time, not
    ' when the backup is needed. The limits are lowered here so the test
    ' need not write twenty thousand files or twenty gigabytes.
    <TestMethod>
    Public Sub Backup_ReportsALibraryLargerThanRestoreAccepts()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)
        Dim backupPath As String = Path.Combine(_root, "backup.zip")

        Dim byDefault As New PortableBackupService(managedDirectory)
        Assert.AreEqual(PortableRestoreService.MaximumArchiveEntries, byDefault.MaximumArchiveEntries, "Restore's own limit.")
        Assert.AreEqual(PortableRestoreService.MaximumUncompressedBytes, byDefault.MaximumUncompressedBytes, "Restore's own limit.")

        Dim tooManyFiles As New PortableBackupService(managedDirectory) With {.MaximumArchiveEntries = 2}
        Dim failure As InvalidDataException =
            Assert.ThrowsExactly(Of InvalidDataException)(Sub() tooManyFiles.CreateBackup(backupPath, manuscripts, repository))
        StringAssert.Contains(failure.Message, "Restore accepts a backup of up to 2 files")
        StringAssert.Contains(failure.Message, "The backup was not written.")

        Dim tooLarge As New PortableBackupService(managedDirectory) With {.MaximumUncompressedBytes = 10}
        failure = Assert.ThrowsExactly(Of InvalidDataException)(Sub() tooLarge.CreateBackup(backupPath, manuscripts, repository))
        StringAssert.Contains(failure.Message, "Restore accepts a backup of up to 0 GB")

        Assert.IsFalse(File.Exists(backupPath), "Nothing was written under the backup's name.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.partial").Length, "No partial file is left.")

    End Sub


    ' A researcher reaches for Restore when the current library is damaged,
    ' so its emergency backup is a plain copy of what is on disk, not held
    ' to Restore's acceptance checks: a managed file missing from the
    ' current library must not stop the restore (#114).
    <TestMethod>
    Public Sub Restore_CompletesWhenTheCurrentLibraryIsMissingAManagedFile()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Call New PortableBackupService(managedDirectory).CreateBackup(backupPath, manuscripts, repository)

        Dim managedFile As String = manuscripts(0).Submissions(0).Correspondence(0).LocalFilePath
        File.Delete(managedFile)

        Dim result As RestoreResult =
            New PortableRestoreService(managedDirectory).RestoreBackup(backupPath, manuscripts, repository)

        Assert.IsTrue(File.Exists(result.EmergencyBackupPath), "The emergency backup was written.")
        Using archive As ZipArchive = ZipFile.OpenRead(result.EmergencyBackupPath)
            Assert.IsNotNull(archive.GetEntry("manuscripts.json"), "The emergency backup is a usable archive.")
        End Using
        Assert.IsTrue(File.Exists(managedFile), "The managed file is back on disk.")
        Assert.AreEqual(3, repository.Load().Count)

    End Sub


    ' Guard: a backup over an existing one keeps the previous file under a
    ' sibling name only while File.Replace runs, so a replace that fails
    ' halfway leaves both files (#114). That failure cannot be forced here;
    ' this proves the sibling is gone afterwards and the new backup took the
    ' destination's place. It passed before the change as well.
    <TestMethod>
    Public Sub Backup_OverThePreviousBackupLeavesNoSiblingBehind()

        Dim dataDirectory As String = Path.Combine(_root, "source-data")
        Dim managedDirectory As String = Path.Combine(_root, "source-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        repository.Save(manuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Dim service As New PortableBackupService(managedDirectory)
        service.CreateBackup(backupPath, manuscripts, repository)
        Dim previous As Byte() = File.ReadAllBytes(backupPath)

        manuscripts(0).Title = "Changed since the last backup"
        repository.Save(manuscripts)
        service.CreateBackup(backupPath, manuscripts, repository)

        CollectionAssert.AreNotEqual(previous, File.ReadAllBytes(backupPath), "The new backup took the destination's place.")
        Assert.AreEqual(1, Directory.GetFiles(_root, "backup.zip*").Length, "No .partial or .previous sibling is left.")

    End Sub


    ' Files PaperRoute no longer needs wait under removed\<date> in the
    ' managed library (#110). A backup leaves that folder out, and a restore
    ' never writes into it.
    <TestMethod>
    Public Sub Backup_LeavesOutTheRemovedFolderAndRestoreNeverWritesIntoIt()

        Dim dataDirectory As String = Path.Combine(_root, "removed-data")
        Dim managedDirectory As String = Path.Combine(_root, "removed-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()

        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)

        Dim removedFile As String =
            Path.Combine(
                managedDirectory,
                "removed",
                "2026-10-09",
                Guid.NewGuid().ToString("N"),
                "versions",
                Guid.NewGuid().ToString("N"),
                "old-snapshot.txt"
            )

        Directory.CreateDirectory(Path.GetDirectoryName(removedFile))
        File.WriteAllText(removedFile, "No longer needed.")

        Dim backupPath As String = Path.Combine(_root, "removed.zip")
        Call New PortableBackupService(managedDirectory).CreateBackup(backupPath, manuscripts, repository)

        Using archive As ZipArchive = ZipFile.OpenRead(backupPath)
            Assert.IsTrue(
                archive.Entries.Any(
                    Function(entry) entry.FullName.Replace("\", "/").StartsWith("files/", StringComparison.OrdinalIgnoreCase)
                ),
                "The managed files are still backed up."
            )
            Assert.IsFalse(
                archive.Entries.Any(
                    Function(entry) entry.FullName.Replace("\", "/").StartsWith("files/removed/", StringComparison.OrdinalIgnoreCase)
                ),
                "A backup leaves out the removed folder."
            )
        End Using

        Assert.AreEqual(1, New PortableRestoreService(managedDirectory).InspectBackup(backupPath).ManagedFileCount)

        Dim targetManaged As String = Path.Combine(_root, "removed-target-library")
        Dim targetRepository As New ManuscriptRepository(Path.Combine(_root, "removed-target-data"), targetManaged)
        Dim current As New List(Of Manuscript)()
        targetRepository.Save(current)
        Call New PortableRestoreService(targetManaged).RestoreBackup(backupPath, current, targetRepository)

        Assert.AreEqual(3, targetRepository.Load().Count)
        Assert.IsFalse(Directory.Exists(Path.Combine(targetManaged, "removed")), "A restore never writes into removed.")
        Assert.AreEqual("No longer needed.", File.ReadAllText(removedFile), "The source's removed files stay where they are.")

    End Sub


    ' A restore replaces the managed library with the backup's. The library's
    ' removed folder is the one copy of every file a sweep moved, and no
    ' backup holds it (#110), so it comes across to the restored library
    ' instead of going with the old one.
    <TestMethod>
    Public Sub Restore_OverTheSameLibraryKeepsItsRemovedFolder()

        Dim dataDirectory As String = Path.Combine(_root, "in-place-data")
        Dim managedDirectory As String = Path.Combine(_root, "in-place-library")
        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()

        AddManagedFixture(manuscripts, managedDirectory)
        repository.Save(manuscripts)

        Dim removedFile As String =
            Path.Combine(
                managedDirectory,
                "removed",
                "2026-10-09",
                Guid.NewGuid().ToString("N"),
                "versions",
                Guid.NewGuid().ToString("N"),
                "old-snapshot.txt"
            )

        Directory.CreateDirectory(Path.GetDirectoryName(removedFile))
        File.WriteAllText(removedFile, "The one copy.")

        Dim backupPath As String = Path.Combine(_root, "in-place.zip")
        Call New PortableBackupService(managedDirectory).CreateBackup(backupPath, manuscripts, repository)

        Dim result As RestoreResult =
            New PortableRestoreService(managedDirectory).RestoreBackup(backupPath, manuscripts, repository)

        Assert.AreEqual(3, result.ManuscriptCount)
        Assert.AreEqual(3, repository.Load().Count)
        Assert.AreEqual("The one copy.", File.ReadAllText(removedFile), "The removed folder comes across to the restored library.")
        Assert.AreEqual(
            0,
            Directory.GetDirectories(_root, "PaperRoute Library.restore-rollback-*").Length,
            "Nothing of the old library is left beside the new one."
        )

    End Sub


    ' After a restore the safety backups still held the pre-restore library,
    ' which a later fallback would bring back as "the previous save" (#116):
    ' they are copies of the restored files instead.
    <TestMethod>
    Public Sub Restore_ReplacesTheSafetyBackupsWithTheRestoredFiles()

        Dim scenario As RestoreScenario = CreateRestoreScenario()

        Call New PortableRestoreService(scenario.TargetManaged).RestoreBackup(scenario.BackupPath, scenario.Current, scenario.TargetRepository)

        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(scenario.TargetData, "manuscripts.json")),
            File.ReadAllBytes(Path.Combine(scenario.TargetData, "manuscripts.bak")),
            "manuscripts.bak equals the restored manuscripts.json."
        )
        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(scenario.TargetData, "authors.json")),
            File.ReadAllBytes(Path.Combine(scenario.TargetData, "authors.bak")),
            "authors.bak equals the restored authors.json."
        )
        ' CitationStore falls back to citations.bak without a word, so a
        ' stale one would bring the pre-restore figures back.
        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(scenario.TargetData, CitationStore.FileName)),
            File.ReadAllBytes(Path.Combine(scenario.TargetData, "citations.bak")),
            "citations.bak equals the restored citations.json."
        )
        Assert.AreEqual("Jane Researcher", New AuthorLibraryRepository(scenario.TargetData).Load().Authors.Single().DisplayName)

    End Sub


    ' A failure at each step of the swap leaves the library as it was and
    ' names the emergency ZIP (#116). The failing step is the one whose
    ' destination is named: the restored managed folder taking the live
    ' folder's place, then each file taking its place.
    <TestMethod>
    <DataRow("files")>
    <DataRow("manuscripts.json")>
    <DataRow("authors.json")>
    <DataRow("citations.json")>
    Public Sub Restore_FailureAtEachSwapStepLeavesTheLibraryAsItWasAndNamesTheEmergencyZip(swapStep As String)

        Dim scenario As RestoreScenario = CreateRestoreScenario()
        Dim before As Dictionary(Of String, Byte()) = SnapshotLibrary(scenario)
        Dim failAt As String = If(swapStep = "files", scenario.TargetManaged, Path.Combine(scenario.TargetData, swapStep))
        Dim service As New FailingRestoreService(scenario.TargetManaged, failAt)

        Dim failure As RestoreFailedException =
            Assert.ThrowsExactly(Of RestoreFailedException)(
                Sub() service.RestoreBackup(scenario.BackupPath, scenario.Current, scenario.TargetRepository))

        Dim emergencyZip As String = Directory.GetFiles(Path.Combine(scenario.TargetData, "backups"), "PaperRoute_PreRestore_*.zip").Single()
        Assert.AreEqual(emergencyZip, failure.EmergencyBackupPath)
        StringAssert.Contains(failure.Message, emergencyZip, "The message names the emergency ZIP.")
        StringAssert.Contains(failure.Message, "put back as it was")
        StringAssert.Contains(failure.Message, Path.GetFileName(failAt), "The message carries the cause.")
        Assert.AreEqual(0, failure.LeftBehind.Count)

        AssertSameFiles(before, SnapshotLibrary(scenario), "The library on disk after failing at " & swapStep)
        Assert.AreEqual(
            0,
            Directory.GetFileSystemEntries(_root, "*restore-*", SearchOption.AllDirectories).Length,
            "Nothing of the restore is left beside the library."
        )

    End Sub


    ' A backup of a three-manuscript library with reusable authors, saved
    ' citations and a managed file, and a one-manuscript target library with
    ' its own authors, citations and managed file, saved twice so its .bak
    ' files exist.
    Private Structure RestoreScenario
        Public BackupPath As String
        Public TargetData As String
        Public TargetManaged As String
        Public TargetRepository As ManuscriptRepository
        Public Current As List(Of Manuscript)
    End Structure


    Private Function CreateRestoreScenario() As RestoreScenario

        Dim sourceData As String = Path.Combine(_root, "scenario-source-data")
        Dim sourceManaged As String = Path.Combine(_root, "scenario-source-library")
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceManaged)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        AddManagedFixture(sourceManuscripts, sourceManaged)
        sourceRepository.Save(sourceManuscripts)

        Dim sourceAuthors As New AuthorLibraryData()
        sourceAuthors.Authors.Add(New AuthorRecord With {.GivenName = "Jane", .FamilyName = "Researcher"})
        Call New AuthorLibraryRepository(sourceData).Save(sourceAuthors)
        Call New CitationStore(sourceData).Save(New CitationSnapshot With {.Orcid = "0000-0002-1825-0097", .Source = "Example"})

        Dim scenario As New RestoreScenario With {
            .BackupPath = Path.Combine(_root, "scenario.zip"),
            .TargetData = Path.Combine(_root, "scenario-target-data"),
            .TargetManaged = Path.Combine(_root, "scenario-target-library")
        }

        Call New PortableBackupService(sourceManaged).CreateBackup(scenario.BackupPath, sourceManuscripts, sourceRepository)

        scenario.TargetRepository = New ManuscriptRepository(scenario.TargetData, scenario.TargetManaged)
        scenario.Current = New List(Of Manuscript) From {
            New Manuscript With {.Title = "Current Before Restore", .CurrentStage = PaperStage.Draft, .Location = ManuscriptLocation.Pipeline}
        }
        scenario.Current(0).Submissions.Add(New JournalSubmission With {.JournalName = "Current Journal", .SubmittedDate = New DateTime(2026, 8, 1)})
        AddManagedFixture(scenario.Current, scenario.TargetManaged)

        Dim targetAuthors As New AuthorLibraryData()
        targetAuthors.Authors.Add(New AuthorRecord With {.GivenName = "Old", .FamilyName = "Author"})
        Dim targetAuthorRepository As New AuthorLibraryRepository(scenario.TargetData)
        Dim targetCitations As New CitationStore(scenario.TargetData)

        ' Twice, so the .bak files hold the pre-restore library.
        For pass As Integer = 1 To 2
            scenario.TargetRepository.Save(scenario.Current)
            targetAuthorRepository.Save(targetAuthors)
            targetCitations.Save(New CitationSnapshot With {.Orcid = "0000-0001-5000-0007", .Source = "Example"})
        Next

        Assert.IsTrue(File.Exists(Path.Combine(scenario.TargetData, "manuscripts.bak")))
        Assert.IsTrue(File.Exists(Path.Combine(scenario.TargetData, "authors.bak")))
        Assert.IsTrue(File.Exists(Path.Combine(scenario.TargetData, "citations.bak")))

        Return scenario

    End Function


    Private Sub AddManagedFixture(
        manuscripts As List(Of Manuscript),
        managedDirectory As String
    )

        Dim correspondence As New CorrespondenceItem With {
            .ItemDate = New DateTime(2026, 7, 10),
            .Type = CorrespondenceType.DecisionLetter,
            .Title = "Synthetic decision letter",
            .IsManagedCopy = True
        }

        Dim fixtureDirectory As String =
            Path.Combine(
                managedDirectory,
                manuscripts(0).Id.ToString("N"),
                manuscripts(0).Submissions(0).Id.ToString("N"),
                correspondence.Id.ToString("N")
            )

        Directory.CreateDirectory(fixtureDirectory)

        Dim fixturePath As String = Path.Combine(fixtureDirectory, "decision-letter.txt")
        File.WriteAllText(fixturePath, "Synthetic managed correspondence fixture.")

        correspondence.LocalFilePath = fixturePath
        manuscripts(0).Submissions(0).Correspondence.Add(correspondence)

    End Sub


    ' Every file under the data and managed folders, except the emergency
    ' ZIP a restore adds under data\backups.
    Private Shared Function SnapshotLibrary(scenario As RestoreScenario) As Dictionary(Of String, Byte())

        Dim files As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)
        Dim backups As String = Path.Combine(scenario.TargetData, "backups")

        For Each folder As String In {scenario.TargetData, scenario.TargetManaged}
            If Not Directory.Exists(folder) Then Continue For
            For Each filePath As String In Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                If filePath.StartsWith(backups, StringComparison.OrdinalIgnoreCase) Then Continue For
                files(filePath) = File.ReadAllBytes(filePath)
            Next
        Next

        Return files

    End Function


    Private Shared Sub AssertSameFiles(expected As Dictionary(Of String, Byte()), actual As Dictionary(Of String, Byte()), message As String)

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message & ": the same files.")

        For Each pair As KeyValuePair(Of String, Byte()) In expected
            CollectionAssert.AreEqual(pair.Value, actual(pair.Key), message & ": " & Path.GetFileName(pair.Key) & " is unchanged.")
        Next

    End Sub


    ' Fails the swap step whose destination is the given path (#116). The
    ' rollback does not go through these seams, so it runs as it would.
    Private NotInheritable Class FailingRestoreService
        Inherits PortableRestoreService

        Private ReadOnly _failAt As String

        Public Sub New(managedLibraryRootDirectory As String, failAt As String)
            MyBase.New(managedLibraryRootDirectory)
            _failAt = Path.GetFullPath(failAt)
        End Sub

        Friend Overrides Sub MoveDirectory(sourceDirectory As String, destinationDirectory As String)
            FailAtTarget(destinationDirectory)
            MyBase.MoveDirectory(sourceDirectory, destinationDirectory)
        End Sub

        Friend Overrides Sub CopyFile(sourceFile As String, destinationFile As String)
            FailAtTarget(destinationFile)
            MyBase.CopyFile(sourceFile, destinationFile)
        End Sub

        Private Sub FailAtTarget(destination As String)
            If String.Equals(Path.GetFullPath(destination), _failAt, StringComparison.OrdinalIgnoreCase) Then
                Throw New IOException("There is not enough space on the disk: " & Path.GetFileName(destination))
            End If
        End Sub
    End Class

End Class
