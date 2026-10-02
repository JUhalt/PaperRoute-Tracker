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

End Class
