Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManagedRecoveryConflictTests

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub VersionRecovery_ExistingDestinationPreservesStagedAndDestinationFiles(hasConflictingFile As Boolean)

        Using fixture As New RecoveryFixture()
            Dim stagedVersion As String = fixture.StageVersion()
            Directory.CreateDirectory(fixture.VersionDirectory)
            If hasConflictingFile Then File.WriteAllText(fixture.Version.LocalFilePath, "different destination version")

            Dim service As New ManagedLibraryService(fixture.ManagedRoot)
            Assert.ThrowsExactly(Of IOException)(
                Sub() service.RecoverStagedVersionDeletions(New List(Of Manuscript) From {fixture.Manuscript})
            )

            Assert.AreEqual("original version", File.ReadAllText(Path.Combine(stagedVersion, "version.txt")))
            Assert.IsTrue(Directory.Exists(fixture.VersionDirectory))
            If hasConflictingFile Then
                Assert.AreEqual("different destination version", File.ReadAllText(fixture.Version.LocalFilePath))
            Else
                Assert.AreEqual(0, Directory.GetFileSystemEntries(fixture.VersionDirectory).Length,
                    "An empty destination is a conflict; recovery must not discard the complete staged snapshot.")
            End If
        End Using

    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Load_VersionConflictStillRestoresIndependentPacketSnapshot(hasConflictingFile As Boolean)

        Using fixture As New RecoveryFixture()
            Dim originalJson As String = File.ReadAllText(fixture.Repository.DataFilePath)
            Dim stagedVersion As String = fixture.StageVersion()
            Dim stagedPacket As String = fixture.StagePacket()
            Directory.CreateDirectory(fixture.VersionDirectory)
            If hasConflictingFile Then File.WriteAllText(fixture.Version.LocalFilePath, "different destination version")

            Dim loaded As List(Of Manuscript) = fixture.Repository.Load()

            Assert.AreEqual(1, loaded.Count)
            Assert.AreEqual(fixture.Manuscript.Id, loaded(0).Id)
            Assert.AreEqual(fixture.Version.Id, loaded(0).Versions(0).Id)
            Assert.AreEqual(fixture.PacketFile.Id, loaded(0).SubmissionPackets(0).Files(0).Id)
            Assert.AreEqual("original packet", File.ReadAllText(fixture.PacketFile.LocalFilePath),
                "A Version History recovery failure must not prevent independent packet recovery.")
            Assert.IsFalse(Directory.Exists(stagedPacket))
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.ManagedRoot, ManagedPacketDeletionService.StagingFolderName)))
            Assert.AreEqual("original version", File.ReadAllText(Path.Combine(stagedVersion, "version.txt")))
            If hasConflictingFile Then
                Assert.AreEqual("different destination version", File.ReadAllText(fixture.Version.LocalFilePath))
            Else
                Assert.IsFalse(File.Exists(fixture.Version.LocalFilePath))
            End If
            StringAssert.Contains(fixture.Repository.LastManagedLibraryRecoveryWarning, "Version History:")
            StringAssert.Contains(fixture.Repository.LastManagedLibraryRecoveryWarning, stagedVersion)
            Assert.IsFalse(fixture.Repository.LastManagedLibraryRecoveryWarning.Contains("Submission Packets:"),
                "A successful packet recovery must not be reported as a packet failure.")
            Assert.IsFalse(fixture.Repository.LastLoadRecoveredFromBackup)
            Assert.AreEqual(originalJson, File.ReadAllText(fixture.Repository.DataFilePath),
                "Managed-file recovery must not discard or rewrite valid manuscript metadata.")
        End Using

    End Sub

    <TestMethod>
    Public Sub Load_BothRecoveryConflictsPreserveAllCopiesAndAggregateWarnings()

        Using fixture As New RecoveryFixture()
            Dim originalJson As String = File.ReadAllText(fixture.Repository.DataFilePath)
            Dim stagedVersion As String = fixture.StageVersion()
            Dim stagedPacket As String = fixture.StagePacket()
            Directory.CreateDirectory(fixture.VersionDirectory)
            Directory.CreateDirectory(fixture.PacketDirectory)
            File.WriteAllText(fixture.Version.LocalFilePath, "different destination version")
            File.WriteAllText(fixture.PacketFile.LocalFilePath, "different destination packet")

            Dim loaded As List(Of Manuscript) = fixture.Repository.Load()

            Assert.AreEqual(1, loaded.Count)
            Assert.AreEqual("original version", File.ReadAllText(Path.Combine(stagedVersion, "version.txt")))
            Assert.AreEqual("original packet", File.ReadAllText(Path.Combine(stagedPacket, "packet.txt")))
            Assert.AreEqual("different destination version", File.ReadAllText(fixture.Version.LocalFilePath))
            Assert.AreEqual("different destination packet", File.ReadAllText(fixture.PacketFile.LocalFilePath))
            Dim warning As String = fixture.Repository.LastManagedLibraryRecoveryWarning
            StringAssert.Contains(warning, "Version History:")
            StringAssert.Contains(warning, "Submission Packets:")
            StringAssert.Contains(warning, stagedVersion)
            StringAssert.Contains(warning, stagedPacket)
            Assert.AreEqual(originalJson, File.ReadAllText(fixture.Repository.DataFilePath))
        End Using

    End Sub

    <TestMethod>
    Public Sub Load_AfterEmptyVersionConflictIsResolvedRestoresPreservedSnapshotAndClearsWarning()

        Using fixture As New RecoveryFixture()
            Dim stagedVersion As String = fixture.StageVersion()
            Directory.CreateDirectory(fixture.VersionDirectory)

            fixture.Repository.Load()

            Assert.IsFalse(String.IsNullOrWhiteSpace(fixture.Repository.LastManagedLibraryRecoveryWarning))
            Assert.AreEqual("original version", File.ReadAllText(Path.Combine(stagedVersion, "version.txt")))
            Assert.AreEqual(0, Directory.GetFileSystemEntries(fixture.VersionDirectory).Length)
            ' Remove only this fixture's verified-empty conflicting directory.
            Directory.Delete(fixture.VersionDirectory, False)

            Dim loaded As List(Of Manuscript) = fixture.Repository.Load()

            Assert.AreEqual(1, loaded.Count)
            Assert.AreEqual("original version", File.ReadAllText(fixture.Version.LocalFilePath))
            Assert.IsFalse(Directory.Exists(stagedVersion))
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.ManagedRoot, ".paperroute-version-delete")))
            Assert.AreEqual(String.Empty, fixture.Repository.LastManagedLibraryRecoveryWarning)
            Assert.AreEqual("original packet", File.ReadAllText(fixture.PacketFile.LocalFilePath))
        End Using

    End Sub

    Private NotInheritable Class RecoveryFixture
        Implements IDisposable

        Public ReadOnly Property Root As String = CreateTemporaryRoot()
        Public ReadOnly Property ManagedRoot As String
        Public ReadOnly Property Repository As ManuscriptRepository
        Public ReadOnly Property Manuscript As Manuscript
        Public ReadOnly Property Version As ManuscriptVersion
        Public ReadOnly Property Packet As SubmissionPacket
        Public ReadOnly Property PacketFile As SubmissionPacketFile
        Public ReadOnly Property VersionDirectory As String
        Public ReadOnly Property PacketDirectory As String

        Public Sub New()

            ManagedRoot = Path.Combine(Root, "managed")
            Manuscript = New Manuscript With {.Title = "Managed recovery conflict"}
            Version = New ManuscriptVersion With {.Label = "Preserved version", .IsManagedCopy = True}
            VersionDirectory = Path.Combine(ManagedRoot, Manuscript.Id.ToString("N"), "versions", Version.Id.ToString("N"))
            Directory.CreateDirectory(VersionDirectory)
            Version.LocalFilePath = Path.Combine(VersionDirectory, "version.txt")
            File.WriteAllText(Version.LocalFilePath, "original version")
            Manuscript.Versions.Add(Version)
            Manuscript.CurrentVersionId = Version.Id

            Packet = New SubmissionPacket With {.Label = "Preserved packet", .ManuscriptVersionId = Version.Id}
            PacketFile = New SubmissionPacketFile With {
                .Label = "Packet snapshot",
                .Role = SubmissionPacketFileRole.Manuscript,
                .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy,
                .OriginalFileName = "packet.txt"
            }
            PacketDirectory = Path.Combine(ManagedRoot, Manuscript.Id.ToString("N"), "packets",
                Packet.Id.ToString("N"), PacketFile.Id.ToString("N"))
            Directory.CreateDirectory(PacketDirectory)
            PacketFile.LocalFilePath = Path.Combine(PacketDirectory, "packet.txt")
            File.WriteAllText(PacketFile.LocalFilePath, "original packet")
            Packet.Files.Add(PacketFile)
            Manuscript.SubmissionPackets.Add(Packet)

            Repository = New ManuscriptRepository(Path.Combine(Root, "data"), ManagedRoot)
            Repository.Save(New List(Of Manuscript) From {Manuscript})

        End Sub

        Public Function StageVersion() As String
            Dim staged As String = Path.Combine(ManagedRoot, ".paperroute-version-delete",
                Guid.NewGuid().ToString("N"), Manuscript.Id.ToString("N"), Version.Id.ToString("N"))
            Directory.CreateDirectory(Path.GetDirectoryName(staged))
            Directory.Move(VersionDirectory, staged)
            Return staged
        End Function

        Public Function StagePacket() As String
            Dim staged As String = Path.Combine(ManagedRoot, ManagedPacketDeletionService.StagingFolderName,
                Guid.NewGuid().ToString("N"), Manuscript.Id.ToString("N"), Packet.Id.ToString("N"), PacketFile.Id.ToString("N"))
            Directory.CreateDirectory(Path.GetDirectoryName(staged))
            Directory.Move(PacketDirectory, staged)
            Return staged
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            DeleteTemporaryRoot(Root)
        End Sub

    End Class

End Class
