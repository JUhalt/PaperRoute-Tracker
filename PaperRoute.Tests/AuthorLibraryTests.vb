Imports System
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class AuthorLibraryTests

    Private _root As String = String.Empty
    Private _data As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

        _data =
            Path.Combine(
                _root,
                "data"
            )

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub EmptyAuthorLibrary_LoadsAsEmpty()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As AuthorLibraryData =
            repository.Load()

        Assert.AreEqual(
            0,
            library.Authors.Count
        )

        Assert.AreEqual(
            0,
            library.Affiliations.Count
        )

    End Sub


    <TestMethod>
    Public Sub SaveAndLoad_RoundTripsAuthorsAndAffiliations()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim affiliation As New AffiliationRecord With {
            .Institution = "Example University",
            .Department = "Department of Psychology",
            .City = "Hartford",
            .Region = "CT",
            .Country = "USA"
        }

        Dim author As New AuthorRecord With {
            .GivenName = "Joshua",
            .FamilyName = "Uhalt",
            .Orcid = "0000-0000-0000-0000",
            .IsMe = True
        }

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            author
        )

        library.Affiliations.Add(
            affiliation
        )

        repository.Save(
            library
        )

        Dim loaded As AuthorLibraryData =
            repository.Load()

        Assert.AreEqual(
            1,
            loaded.Authors.Count
        )

        Assert.AreEqual(
            "Joshua Uhalt",
            loaded.Authors(0).DisplayName
        )

        Assert.IsTrue(
            loaded.Authors(0).IsMe
        )

        Assert.AreEqual(
            "0000-0000-0000-0000",
            loaded.Authors(0).Orcid
        )

        Assert.AreEqual(
            1,
            loaded.Affiliations.Count
        )

        Assert.AreEqual(
            "Example University",
            loaded.Affiliations(0).Institution
        )

    End Sub


    <TestMethod>
    Public Sub SecondSave_CreatesSafetyBackup()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .DisplayNameOverride = "First"
            }
        )

        repository.Save(
            library
        )

        library.Authors(0).DisplayNameOverride =
            "Second"

        repository.Save(
            library
        )

        Assert.IsTrue(
            File.Exists(
                repository.BackupFilePath
            )
        )

        Dim backupText As String =
            File.ReadAllText(
                repository.BackupFilePath
            )

        StringAssert.Contains(
            backupText,
            "First"
        )

    End Sub


    <TestMethod>
    Public Sub CorruptPrimary_WithValidBackup_RecoversAndPreservesCorruptFile()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .DisplayNameOverride = "Safe Author"
            }
        )

        repository.Save(
            library
        )

        repository.Save(
            library
        )

        File.WriteAllText(
            repository.DataFilePath,
            "{ deliberately broken json"
        )

        Dim loaded As AuthorLibraryData =
            repository.Load()

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.AreEqual(
            "Safe Author",
            loaded.Authors(0).DisplayName
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

    End Sub


    <TestMethod>
    Public Sub DuplicateAuthorIds_AreRejectedBeforeSave()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim duplicateId As Guid =
            Guid.NewGuid()

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .Id = duplicateId,
                .DisplayNameOverride = "One"
            }
        )

        library.Authors.Add(
            New AuthorRecord With {
                .Id = duplicateId,
                .DisplayNameOverride = "Two"
            }
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                repository.Save(
                    library
                )
            End Sub
        )

    End Sub


    ' A file another program holds for a moment is tried again, never
    ' treated as damage (#111).
    <TestMethod>
    Public Sub Load_FileInUseOnceIsTriedAgainWithoutRecovery()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .DisplayNameOverride = "First"
            }
        )

        repository.Save(
            library
        )

        library.Authors(0).DisplayNameOverride =
            "Second"

        repository.Save(
            library
        )

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Dim opener As New HeldFileOpener(
            repository.DataFilePath,
            failures:=1
        )

        Dim held As New AuthorLibraryRepository(
            _data,
            openFile:=AddressOf opener.Open
        )

        Dim loaded As AuthorLibraryData =
            held.Load()

        Assert.AreEqual(
            "Second",
            loaded.Authors(0).DisplayName
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
                Path.Combine(_data, "recovery")
            )
        )

    End Sub


    ' Held for the whole launch, authors.json is reported in use and both
    ' files are left alone.
    <TestMethod>
    Public Sub Load_FileInUseOnEveryAttemptStopsAndChangesNothing()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .DisplayNameOverride = "First"
            }
        )

        repository.Save(
            library
        )

        repository.Save(
            library
        )

        Dim primaryBefore As Byte() =
            File.ReadAllBytes(repository.DataFilePath)

        Dim backupBefore As Byte() =
            File.ReadAllBytes(repository.BackupFilePath)

        Dim opener As New HeldFileOpener(
            repository.DataFilePath,
            failures:=Integer.MaxValue
        )

        Dim held As New AuthorLibraryRepository(
            _data,
            openFile:=AddressOf opener.Open
        )

        Dim failure As StorageFileInUseException =
            Assert.ThrowsExactly(Of StorageFileInUseException)(
                Sub()
                    Dim ignored As AuthorLibraryData =
                        held.Load()
                End Sub
            )

        StringAssert.Contains(
            failure.Message,
            "authors.json"
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
                Path.Combine(_data, "recovery")
            )
        )

    End Sub


    ' An authors.json that parses but fails validation is damage too: the
    ' backup is loaded and the file set aside, instead of PaperRoute
    ' closing on every launch (#111).
    <TestMethod>
    Public Sub InvalidPrimary_WithValidBackup_LoadsTheBackup()

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            New AuthorRecord With {
                .DisplayNameOverride = "Safe Author"
            }
        )

        repository.Save(
            library
        )

        repository.Save(
            library
        )

        File.WriteAllText(
            repository.DataFilePath,
            InvalidAuthorLibraryJson
        )

        Dim loaded As AuthorLibraryData =
            repository.Load()

        Assert.IsTrue(
            repository.LastLoadRecoveredFromBackup
        )

        Assert.AreEqual(
            "Safe Author",
            loaded.Authors(0).DisplayName
        )

        Assert.IsTrue(
            File.Exists(
                repository.LastRecoveryPreservedFilePath
            )
        )

    End Sub


    ' A backup that parses but fails validation never takes the primary's
    ' place: it is proved before anything moves.
    <TestMethod>
    Public Sub InvalidBackup_NeverReplacesThePrimary()

        Directory.CreateDirectory(
            _data
        )

        Dim repository As New AuthorLibraryRepository(
            _data
        )

        Const brokenPrimary As String =
            "{ deliberately broken json"

        File.WriteAllText(
            repository.DataFilePath,
            brokenPrimary
        )

        File.WriteAllText(
            repository.BackupFilePath,
            InvalidAuthorLibraryJson
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                Dim ignored As AuthorLibraryData =
                    repository.Load()
            End Sub
        )

        Assert.AreEqual(
            brokenPrimary,
            File.ReadAllText(repository.DataFilePath)
        )

        Assert.AreEqual(
            InvalidAuthorLibraryJson,
            File.ReadAllText(repository.BackupFilePath)
        )

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(_data, "recovery")
            )
        )

    End Sub


    ' Valid JSON, but an author without an identifier.
    Private Const InvalidAuthorLibraryJson As String =
        "{ ""Authors"": [ { ""Id"": ""00000000-0000-0000-0000-000000000000"" } ] }"

End Class
