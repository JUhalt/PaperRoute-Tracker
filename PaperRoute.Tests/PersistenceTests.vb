Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class PersistenceTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedLibrary = Path.Combine(_root, "managed")

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub SaveAndLoad_RoundTripsRepresentativeLibrary()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim expected As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(expected)

        Dim actual As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(3, actual.Count)

        Assert.AreEqual(
            "Active Study",
            actual(0).Title
        )

        Assert.AreEqual(
            PaperStage.UnderReview,
            actual(0).CurrentStage
        )

        Assert.AreEqual(
            1,
            actual(0).Submissions.Count
        )

        Assert.AreEqual(
            1,
            actual(0).Submissions(0).Decisions.Count
        )

        Assert.AreEqual(
            EditorialDecision.MajorRevision,
            actual(0).Submissions(0).Decisions(0).Decision
        )

        Assert.AreEqual(
            ManuscriptLocation.Published,
            actual(1).Location
        )

        Assert.AreEqual(
            ManuscriptLocation.FileDrawer,
            actual(2).Location
        )

        Assert.AreEqual(
            1,
            actual(2).RejectionCount
        )

    End Sub


    <TestMethod>
    Public Sub SaveAndLoad_RoundTripsSchema2Metadata()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        manuscripts(0).Metadata.AbstractText =
            "A representative abstract for schema 2."

        manuscripts(0).Metadata.Keywords.Add(
            "metadata"
        )

        manuscripts(0).Metadata.Keywords.Add(
            "manuscript tracking"
        )

        manuscripts(0).Metadata.Doi =
            "10.1234/example.2026.1"

        manuscripts(0).Metadata.PublicationJournal =
            "Journal of Example Studies"

        manuscripts(0).Metadata.PublishedDate =
            New DateTime(
                2026,
                8,
                20
            )

        manuscripts(0).Metadata.Volume =
            "12"

        manuscripts(0).Metadata.Issue =
            "3"

        manuscripts(0).Metadata.Pages =
            "101-118"

        manuscripts(0).Metadata.PublicationUrl =
            "https://example.invalid/article"

        manuscripts(0).Metadata.PreprintUrl =
            "https://example.invalid/preprint"

        manuscripts(0).Metadata.ExternalIdentifiers("crossref") =
            "example-record"

        repository.Save(
            manuscripts
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            "A representative abstract for schema 2.",
            loaded(0).Metadata.AbstractText
        )

        CollectionAssert.AreEqual(
            New List(Of String) From {
                "metadata",
                "manuscript tracking"
            },
            loaded(0).Metadata.Keywords
        )

        Assert.AreEqual(
            "10.1234/example.2026.1",
            loaded(0).Metadata.Doi
        )

        Assert.AreEqual(
            New DateTime(2026, 8, 20),
            loaded(0).Metadata.PublishedDate.Value
        )

        Assert.AreEqual(
            "example-record",
            loaded(0).Metadata.ExternalIdentifiers(
                "crossref"
            )
        )

    End Sub


    <TestMethod>
    Public Sub Load_NormalizesNullSchema2MetadataCollections()

        Directory.CreateDirectory(
            _dataDirectory
        )

        Dim json As String =
            "[{" &
            """Title"":""Schema 2 Null Metadata""," &
            """CurrentStage"":""Draft""," &
            """Location"":""Pipeline""," &
            """Metadata"":{" &
                """Keywords"":null," &
                """ExternalIdentifiers"":null" &
            "}" &
            "}]"

        File.WriteAllText(
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            ),
            json
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsNotNull(
            loaded(0).Metadata
        )

        Assert.IsNotNull(
            loaded(0).Metadata.Keywords
        )

        Assert.IsNotNull(
            loaded(0).Metadata.ExternalIdentifiers
        )

        Assert.AreEqual(
            0,
            loaded(0).Metadata.Keywords.Count
        )

        Assert.AreEqual(
            0,
            loaded(0).Metadata.ExternalIdentifiers.Count
        )

    End Sub


    <TestMethod>
    Public Sub SecondSave_CreatesBackupOfPreviousData()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        manuscripts(0).Title =
            "Active Study Revised"

        repository.Save(manuscripts)

        Assert.IsTrue(
            File.Exists(repository.BackupFilePath)
        )

        Dim backupJson As String =
            File.ReadAllText(repository.BackupFilePath)

        Assert.IsTrue(
            backupJson.Contains("Active Study")
        )

        Assert.IsFalse(
            backupJson.Contains("Active Study Revised")
        )

    End Sub


    <TestMethod>
    Public Sub Load_NormalizesPublishedStageLocation()

        Directory.CreateDirectory(
            _dataDirectory
        )

        Dim manuscript As New Manuscript With {
            .Title = "Legacy Published Location",
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim json As String =
            JsonSerializer.Serialize(
                New List(Of Manuscript) From {
                    manuscript
                },
                CreateJsonOptions()
            )

        File.WriteAllText(
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            ),
            json
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            ManuscriptLocation.Published,
            loaded(0).Location
        )

    End Sub


    <TestMethod>
    Public Sub Load_NormalizesNullCollections()

        Directory.CreateDirectory(
            _dataDirectory
        )

        Dim id As Guid =
            Guid.NewGuid()

        Dim json As String =
            "[{" &
            """Id"":""" & id.ToString() & """," &
            """Title"":""Null Collections""," &
            """CurrentStage"":""Draft""," &
            """Location"":""Pipeline""," &
            """History"":null," &
            """Submissions"":null" &
            "}]"

        File.WriteAllText(
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            ),
            json
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsNotNull(
            loaded(0).History
        )

        Assert.IsNotNull(
            loaded(0).Submissions
        )

        Assert.AreEqual(
            0,
            loaded(0).History.Count
        )

        Assert.AreEqual(
            0,
            loaded(0).Submissions.Count
        )

    End Sub


    <TestMethod>
    Public Sub Load_CorruptPrimaryRecoversFromValidBackup()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        manuscripts(0).Title =
            "Second Version"

        repository.Save(manuscripts)

        File.WriteAllText(
            repository.DataFilePath,
            "{ definitely not valid json"
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            3,
            loaded.Count
        )

        Assert.AreEqual(
            "Active Study",
            loaded(0).Title
        )

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(
                repository.LastRecoveryPreservedFilePath
            )
        )

        Assert.IsTrue(
            File.Exists(
                repository.LastRecoveryPreservedFilePath
            )
        )

        Dim reloaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            "Active Study",
            reloaded(0).Title
        )

    End Sub


    <TestMethod>
    Public Sub Load_BlankPrimaryRecoversFromValidBackup()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        manuscripts(0).Title =
            "Second Version"

        repository.Save(manuscripts)

        File.WriteAllText(
            repository.DataFilePath,
            String.Empty
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            "Active Study",
            loaded(0).Title
        )

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

    End Sub


    <TestMethod>
    Public Sub Load_MissingPrimaryRecoversFromValidBackup()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        manuscripts(0).Title =
            "Second Version"

        repository.Save(manuscripts)

        File.Delete(
            repository.DataFilePath
        )

        Assert.IsTrue(
            File.Exists(repository.BackupFilePath)
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            "Active Study",
            loaded(0).Title
        )

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.IsTrue(
            File.Exists(repository.DataFilePath)
        )

    End Sub


    <TestMethod>
    Public Sub Load_CorruptPrimaryAndBackupThrowsSafely()

        Directory.CreateDirectory(
            _dataDirectory
        )

        Dim dataPath As String =
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            )

        Dim backupPath As String =
            Path.Combine(
                _dataDirectory,
                "manuscripts.bak"
            )

        Const corruptPrimary As String =
            "{ corrupt primary"

        Const corruptBackup As String =
            "{ corrupt backup"

        File.WriteAllText(
            dataPath,
            corruptPrimary
        )

        File.WriteAllText(
            backupPath,
            corruptBackup
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As List(Of Manuscript) =
                    repository.Load()
            End Sub
        )

        Assert.AreEqual(
            corruptPrimary,
            File.ReadAllText(dataPath)
        )

        Assert.AreEqual(
            corruptBackup,
            File.ReadAllText(backupPath)
        )

    End Sub


    <TestMethod>
    Public Sub Load_ValidEmptyLibraryRemainsValid()

        Directory.CreateDirectory(
            _dataDirectory
        )

        File.WriteAllText(
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            ),
            "[]"
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            0,
            loaded.Count
        )

        Assert.IsFalse(
            repository.LastLoadRecoveredFromBackup
        )

    End Sub


    <TestMethod>
    Public Sub SaveAndLoad_RoundTripsCorrespondenceMetadata()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim linkedFilePath As String =
            Path.Combine(
                _root,
                "linked-decision-letter.txt"
            )

        File.WriteAllText(
            linkedFilePath,
            "Synthetic linked correspondence."
        )

        manuscripts(0).Submissions(0).Correspondence.Add(
            New CorrespondenceItem With {
                .ItemDate = New DateTime(2026, 8, 19),
                .Type = CorrespondenceType.DecisionLetter,
                .Title = "Round-trip decision letter",
                .LocalFilePath = linkedFilePath,
                .IsManagedCopy = False
            }
        )

        repository.Save(
            manuscripts
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            1,
            loaded(0).Submissions(0).Correspondence.Count
        )

        Dim correspondence As CorrespondenceItem =
            loaded(0).Submissions(0).Correspondence(0)

        Assert.AreEqual(
            "Round-trip decision letter",
            correspondence.Title
        )

        Assert.AreEqual(
            CorrespondenceType.DecisionLetter,
            correspondence.Type
        )

        Assert.AreEqual(
            New DateTime(2026, 8, 19),
            correspondence.ItemDate
        )

        Assert.AreEqual(
            linkedFilePath,
            correspondence.LocalFilePath
        )

        Assert.IsFalse(
            correspondence.IsManagedCopy
        )

    End Sub


    <TestMethod>
    Public Sub SaveAndLoad_RoundTripsStructuredAuthorLinks()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        Dim authorId As Guid =
            Guid.NewGuid()

        Dim affiliationId As Guid =
            Guid.NewGuid()

        manuscripts(0).Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = authorId,
                .AffiliationIds =
                    New List(Of Guid) From {
                        affiliationId
                    },
                .IsCorrespondingAuthor = True
            }
        )

        repository.Save(
            manuscripts
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            1,
            loaded(0).Authors.Count
        )

        Assert.AreEqual(
            authorId,
            loaded(0).Authors(0).AuthorId
        )

        Assert.AreEqual(
            affiliationId,
            loaded(0).Authors(0).AffiliationIds(0)
        )

        Assert.IsTrue(
            loaded(0).Authors(0).IsCorrespondingAuthor
        )

    End Sub


    <TestMethod>
    Public Sub Save_AfterLoadFromBackupKeepsFoldersOnlyTheSetAsidePrimaryReferenced()

        Dim scenario =
            CreateDamagedPrimaryBesideOlderBackup()

        ' A restart falls back to the older safety backup. The newer primary
        ' file is set aside under data\recovery and still references the
        ' later version and packet file.
        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.AreEqual(
            1,
            loaded(0).Versions.Count
        )

        Assert.AreEqual(
            1,
            loaded(0).SubmissionPackets(0).Files.Count
        )

        repository.Save(
            loaded
        )

        Assert.IsTrue(
            File.Exists(
                scenario.NewerVersion.LocalFilePath
            ),
            "A save after a backup recovery must keep the version snapshot only the set-aside primary references."
        )

        Assert.IsTrue(
            File.Exists(
                scenario.NewerPacketFile.LocalFilePath
            ),
            "A save after a backup recovery must keep the packet file only the set-aside primary references."
        )

        Assert.IsTrue(
            File.Exists(
                scenario.OlderVersion.LocalFilePath
            )
        )

    End Sub


    <TestMethod>
    Public Sub Load_FromBackupKeepsStagedFoldersItDoesNotReferenceAndReportsThem()

        Dim scenario =
            CreateDamagedPrimaryBesideOlderBackup()

        ' An interrupted save staged the later version and packet file.
        Dim stagedVersion As String =
            Path.Combine(
                _managedLibrary,
                ".paperroute-version-delete",
                Guid.NewGuid().ToString("N"),
                scenario.Manuscript.Id.ToString("N"),
                scenario.NewerVersion.Id.ToString("N")
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                stagedVersion
            )
        )

        Directory.Move(
            Path.GetDirectoryName(
                scenario.NewerVersion.LocalFilePath
            ),
            stagedVersion
        )

        Dim stagedPacket As String =
            Path.Combine(
                _managedLibrary,
                ManagedPacketDeletionService.StagingFolderName,
                Guid.NewGuid().ToString("N"),
                scenario.Manuscript.Id.ToString("N"),
                scenario.Manuscript.SubmissionPackets(0).Id.ToString("N"),
                scenario.NewerPacketFile.Id.ToString("N")
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                stagedPacket
            )
        )

        Directory.Move(
            Path.GetDirectoryName(
                scenario.NewerPacketFile.LocalFilePath
            ),
            stagedPacket
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.AreEqual(
            1,
            loaded.Count
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    stagedVersion,
                    "snapshot.txt"
                )
            ),
            "A load from the safety backup must keep a staged version it does not reference."
        )

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    stagedPacket,
                    "packet.txt"
                )
            ),
            "A load from the safety backup must keep a staged packet file it does not reference."
        )

        StringAssert.Contains(
            repository.LastRecoveryKeptStagingNotice,
            stagedVersion
        )

        StringAssert.Contains(
            repository.LastRecoveryKeptStagingNotice,
            stagedPacket
        )

        ' A deliberate keep is not a recovery failure: nothing is missing,
        ' and the warning that steers the user to clear the staging folder,
        ' which holds the only copy of these files, stays off.
        Assert.AreEqual(
            String.Empty,
            repository.LastManagedLibraryRecoveryWarning
        )

    End Sub


    <TestMethod>
    Public Sub Save_WithoutLoadStagesNothing()

        ' AuthorLibraryForm and ExampleLibraryService.Seed save through a
        ' repository that never loaded. Such a save knows nothing about the
        ' managed folder, so it must leave every unreferenced folder alone.
        Dim manuscript As New Manuscript With {
            .Title = "Saved without a load"
        }

        Dim version As ManuscriptVersion =
            AddManagedVersion(
                manuscript,
                "Kept version"
            )

        Dim packet As New SubmissionPacket With {
            .Label = "Packet",
            .ManuscriptVersionId = version.Id
        }

        manuscript.SubmissionPackets.Add(
            packet
        )

        AddManagedPacketFile(
            manuscript,
            packet,
            "Kept packet file"
        )

        ' Folders no record references, left by an earlier library.
        Dim orphanVersion As ManuscriptVersion =
            AddManagedVersion(
                manuscript,
                "Orphan version"
            )

        manuscript.Versions.Remove(
            orphanVersion
        )

        Dim orphanPacketFile As SubmissionPacketFile =
            AddManagedPacketFile(
                manuscript,
                packet,
                "Orphan packet file"
            )

        packet.Files.Remove(
            orphanPacketFile
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Assert.IsTrue(
            File.Exists(
                orphanVersion.LocalFilePath
            ),
            "A save without a load must not remove a version folder it never knew."
        )

        Assert.IsTrue(
            File.Exists(
                orphanPacketFile.LocalFilePath
            ),
            "A save without a load must not remove a packet file folder it never knew."
        )

        ' The save gave the repository a baseline, which does not include
        ' the orphans either.
        manuscript.Title =
            "Saved again"

        repository.Save(
            library
        )

        Assert.IsTrue(
            File.Exists(
                orphanVersion.LocalFilePath
            )
        )

        Assert.IsTrue(
            File.Exists(
                orphanPacketFile.LocalFilePath
            )
        )

    End Sub


    ' Two saves, so manuscripts.bak holds the first library and
    ' manuscripts.json the second, which adds a version and a packet file;
    ' then manuscripts.json is damaged, so the next load falls back to the
    ' older backup while the newer file is set aside under data\recovery.
    Private Function CreateDamagedPrimaryBesideOlderBackup() As (
        Manuscript As Manuscript,
        OlderVersion As ManuscriptVersion,
        NewerVersion As ManuscriptVersion,
        NewerPacketFile As SubmissionPacketFile
    )

        Dim manuscript As New Manuscript With {
            .Title = "Recovered from backup"
        }

        Dim olderVersion As ManuscriptVersion =
            AddManagedVersion(
                manuscript,
                "Older version"
            )

        Dim packet As New SubmissionPacket With {
            .Label = "Packet",
            .ManuscriptVersionId = olderVersion.Id
        }

        manuscript.SubmissionPackets.Add(
            packet
        )

        AddManagedPacketFile(
            manuscript,
            packet,
            "Older packet file"
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim library As New List(Of Manuscript) From {
            manuscript
        }

        repository.Save(
            library
        )

        Dim newerVersion As ManuscriptVersion =
            AddManagedVersion(
                manuscript,
                "Newer version"
            )

        Dim newerPacketFile As SubmissionPacketFile =
            AddManagedPacketFile(
                manuscript,
                packet,
                "Newer packet file"
            )

        repository.Save(
            library
        )

        File.WriteAllText(
            repository.DataFilePath,
            "{ damaged primary"
        )

        Return (
            manuscript,
            olderVersion,
            newerVersion,
            newerPacketFile
        )

    End Function


    ' A version whose snapshot already sits in the managed library, as a
    ' saved library leaves it.
    Private Function AddManagedVersion(
        manuscript As Manuscript,
        label As String
    ) As ManuscriptVersion

        Dim version As New ManuscriptVersion With {
            .Label = label,
            .IsManagedCopy = True
        }

        Dim versionDirectory As String =
            Path.Combine(
                _managedLibrary,
                manuscript.Id.ToString("N"),
                "versions",
                version.Id.ToString("N")
            )

        Directory.CreateDirectory(
            versionDirectory
        )

        version.LocalFilePath =
            Path.Combine(
                versionDirectory,
                "snapshot.txt"
            )

        File.WriteAllText(
            version.LocalFilePath,
            label
        )

        manuscript.Versions.Add(
            version
        )

        Return version

    End Function


    Private Function AddManagedPacketFile(
        manuscript As Manuscript,
        packet As SubmissionPacket,
        label As String
    ) As SubmissionPacketFile

        Dim packetFile As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.Manuscript,
            .Label = label,
            .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy
        }

        Dim fileDirectory As String =
            Path.Combine(
                _managedLibrary,
                manuscript.Id.ToString("N"),
                "packets",
                packet.Id.ToString("N"),
                packetFile.Id.ToString("N")
            )

        Directory.CreateDirectory(
            fileDirectory
        )

        packetFile.LocalFilePath =
            Path.Combine(
                fileDirectory,
                "packet.txt"
            )

        File.WriteAllText(
            packetFile.LocalFilePath,
            label
        )

        packet.Files.Add(
            packetFile
        )

        Return packetFile

    End Function


End Class