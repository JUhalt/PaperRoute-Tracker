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


    ' A file another program holds for a moment, such as a sync client,
    ' is tried again. It is not damage: the backup is never installed and
    ' nothing is set aside (#111).
    <TestMethod>
    Public Sub Load_FileInUseOnceIsTriedAgainWithoutRecovery()

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

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Dim opener As New HeldFileOpener(
            repository.DataFilePath,
            failures:=1
        )

        Dim held As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary,
            openFile:=AddressOf opener.Open
        )

        Dim loaded As List(Of Manuscript) =
            held.Load()

        Assert.AreEqual(
            "Second Version",
            loaded(0).Title
        )

        Assert.IsFalse(
            held.LastLoadRecoveredFromBackup
        )

        Assert.AreEqual(
            2,
            opener.Opens
        )

        CollectionAssert.AreEqual(
            primaryBefore,
            File.ReadAllBytes(repository.DataFilePath)
        )

        CollectionAssert.AreEqual(
            backupBefore,
            File.ReadAllBytes(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_dataDirectory, "recovery")
            )
        )

    End Sub


    ' Held for the whole launch, the file is reported in use and both files
    ' are left alone. Before #111 the backup was taken for the library, or
    ' "found a valid safety backup, but could not restore it".
    <TestMethod>
    Public Sub Load_FileInUseOnEveryAttemptStopsAndChangesNothing()

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

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Dim opener As New HeldFileOpener(
            repository.DataFilePath,
            failures:=Integer.MaxValue
        )

        Dim held As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary,
            openFile:=AddressOf opener.Open
        )

        Dim failure As StorageFileInUseException =
            Assert.ThrowsExactly(Of StorageFileInUseException)(
                Sub()
                    Dim ignored As List(Of Manuscript) =
                        held.Load()
                End Sub
            )

        StringAssert.Contains(
            failure.Message,
            "manuscripts.json"
        )

        StringAssert.Contains(
            failure.Message,
            "in use by another program"
        )

        Assert.IsTrue(
            opener.Opens > 1,
            "Tried again before giving up."
        )

        Assert.IsFalse(
            held.LastLoadRecoveredFromBackup
        )

        CollectionAssert.AreEqual(
            primaryBefore,
            File.ReadAllBytes(repository.DataFilePath)
        )

        CollectionAssert.AreEqual(
            backupBefore,
            File.ReadAllBytes(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_dataDirectory, "recovery")
            )
        )

    End Sub


    ' The same against the error Windows really raises: manuscripts.json
    ' held open by another program for the whole load.
    <TestMethod>
    Public Sub Load_FileHeldOpenByAnotherProgramIsReportedInUse()

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

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Using holder As New FileStream(
            repository.DataFilePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None
        )

            Assert.ThrowsExactly(Of StorageFileInUseException)(
                Sub()
                    Dim ignored As List(Of Manuscript) =
                        repository.Load()
                End Sub
            )

        End Using

        Assert.IsFalse(
            repository.LastLoadRecoveredFromBackup
        )

        CollectionAssert.AreEqual(
            primaryBefore,
            File.ReadAllBytes(repository.DataFilePath)
        )

        CollectionAssert.AreEqual(
            backupBefore,
            File.ReadAllBytes(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_dataDirectory, "recovery")
            )
        )

    End Sub


    ' An in-use error raised while reading, after the open succeeded (another
    ' program holds a byte-range lock), is in use too: the loader stops and
    ' nothing turns to the backup (#111).
    <TestMethod>
    Public Sub Load_FileInUseWhileReadingStopsAndChangesNothing()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscripts As List(Of Manuscript) =
            CreateRepresentativeLibrary()

        repository.Save(manuscripts)

        repository.Save(manuscripts)

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Dim held As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary,
            openFile:=AddressOf LockedReadStream.Open
        )

        Assert.ThrowsExactly(Of StorageFileInUseException)(
            Sub()
                Dim ignored As List(Of Manuscript) =
                    held.Load()
            End Sub
        )

        Assert.IsFalse(
            held.LastLoadRecoveredFromBackup
        )

        CollectionAssert.AreEqual(
            primaryBefore,
            File.ReadAllBytes(repository.DataFilePath)
        )

        CollectionAssert.AreEqual(
            backupBefore,
            File.ReadAllBytes(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_dataDirectory, "recovery")
            )
        )

    End Sub


End Class