Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionPacketIntegrityTests

    Private Const AbcSha256 As String = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
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
    Public Sub CaptureBaseline_RecordsKnownHashWithoutChangingSourceOrOtherFields()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        Dim before As Byte() = File.ReadAllBytes(packetFile.LocalFilePath)
        Dim originalWriteTime As DateTime = File.GetLastWriteTimeUtc(packetFile.LocalFilePath)
        Dim capturedAt As New DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
        Dim beforeRecord As String = Snapshot(packetFile)

        Dim result As PacketFileIntegrityResult =
            SubmissionPacketIntegrityService.CaptureBaseline(packetFile, capturedAtUtc:=capturedAt)

        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, result.Status)
        Assert.AreEqual(AbcSha256, result.Sha256)
        Assert.AreEqual(AbcSha256, packetFile.Sha256)
        Assert.AreEqual(3L, packetFile.FileSizeBytes.Value)
        Assert.AreEqual(originalWriteTime, packetFile.LastWriteTimeUtc.Value)
        Assert.AreEqual(capturedAt, packetFile.HashComputedAtUtc.Value)
        Assert.IsTrue(result.CheckedAtUtc.HasValue)
        CollectionAssert.AreEqual(before, File.ReadAllBytes(packetFile.LocalFilePath))
        Assert.AreEqual(originalWriteTime, File.GetLastWriteTimeUtc(packetFile.LocalFilePath))

        Dim expected As SubmissionPacketFile = JsonSerializer.Deserialize(Of SubmissionPacketFile)(beforeRecord)
        expected.Sha256 = packetFile.Sha256
        expected.FileSizeBytes = packetFile.FileSizeBytes
        expected.LastWriteTimeUtc = packetFile.LastWriteTimeUtc
        expected.HashComputedAtUtc = packetFile.HashComputedAtUtc
        Assert.AreEqual(Snapshot(expected), Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub CaptureBaseline_SupportsEmptyFile()

        Dim packetFile As SubmissionPacketFile = CreateFile(String.Empty)

        Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.CaptureBaseline(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, result.Status)
        Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", packetFile.Sha256)
        Assert.AreEqual(0L, packetFile.FileSizeBytes.Value)
        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.Verify(packetFile).Status)

    End Sub

    <TestMethod>
    Public Sub InspectAndVerify_WithoutBaselineNeverRecordOne()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        packetFile.FileSizeBytes = 987
        packetFile.LastWriteTimeUtc = New DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim before As String = Snapshot(packetFile)

        Dim inspected As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Inspect(packetFile)
        Dim verified As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Verify(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.NotRecorded, inspected.Status)
        Assert.AreEqual(PacketFileIntegrityStatus.NotRecorded, verified.Status)
        Assert.AreEqual(String.Empty, inspected.Sha256)
        Assert.AreEqual(String.Empty, verified.Sha256)
        Assert.IsFalse(inspected.CheckedAtUtc.HasValue)
        Assert.IsFalse(verified.CheckedAtUtc.HasValue)
        Assert.AreEqual(3L, inspected.FileSizeBytes.Value)
        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub Inspect_WithBaselineDoesNotClaimContentsAreUnchanged()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        File.WriteAllText(packetFile.LocalFilePath, "def", New UTF8Encoding(False))
        Dim before As String = Snapshot(packetFile)

        Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Inspect(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.NotChecked, result.Status)
        Assert.AreEqual(String.Empty, result.Sha256)
        Assert.IsFalse(result.CheckedAtUtc.HasValue)
        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub Verify_UsesCaseInsensitiveHashAndDoesNotMutateBaseline()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        packetFile.Sha256 = packetFile.Sha256.ToUpperInvariant()
        Dim before As String = Snapshot(packetFile)

        Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Verify(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, result.Status)
        Assert.AreEqual(AbcSha256, result.Sha256)
        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub Verify_DetectsDifferentBytesEvenWithSameSizeAndTimestamp()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim before As String = Snapshot(packetFile)
        File.WriteAllText(packetFile.LocalFilePath, "def", New UTF8Encoding(False))
        File.SetLastWriteTimeUtc(packetFile.LocalFilePath, packetFile.LastWriteTimeUtc.Value)

        Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Verify(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.Changed, result.Status)
        Assert.AreEqual(packetFile.FileSizeBytes, result.FileSizeBytes)
        Assert.AreEqual(packetFile.LastWriteTimeUtc, result.LastWriteTimeUtc)
        Assert.AreNotEqual(packetFile.Sha256, result.Sha256)
        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub Verify_UnchangedContentsWithDifferentTimestampStillMatch()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim before As String = Snapshot(packetFile)
        File.SetLastWriteTimeUtc(packetFile.LocalFilePath, packetFile.LastWriteTimeUtc.Value.AddDays(-1))

        Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Verify(packetFile)

        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, result.Status)
        Assert.AreNotEqual(packetFile.LastWriteTimeUtc, result.LastWriteTimeUtc)
        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub DeletedFile_IsMissingAndBaselineIsPreserved()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim before As String = Snapshot(packetFile)
        File.Delete(packetFile.LocalFilePath)

        Assert.AreEqual(PacketFileIntegrityStatus.Missing, SubmissionPacketIntegrityService.Inspect(packetFile).Status)
        Assert.AreEqual(PacketFileIntegrityStatus.Missing, SubmissionPacketIntegrityService.Verify(packetFile).Status)
        Assert.AreEqual(PacketFileIntegrityStatus.Missing,
                        SubmissionPacketIntegrityService.CaptureBaseline(packetFile, replaceExisting:=True).Status)
        Assert.AreEqual(before, Snapshot(packetFile))
        Assert.IsFalse(File.Exists(packetFile.LocalFilePath))

    End Sub

    <TestMethod>
    Public Sub LockedFile_IsUnavailableAndFailedReplacementPreservesBaseline()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim before As String = Snapshot(packetFile)

        Using locked As New FileStream(packetFile.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Inspect(packetFile).Status)
            Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Verify(packetFile).Status)
            Assert.AreEqual(PacketFileIntegrityStatus.Unavailable,
                            SubmissionPacketIntegrityService.CaptureBaseline(packetFile, replaceExisting:=True).Status)
        End Using

        Assert.AreEqual(before, Snapshot(packetFile))
        Assert.AreEqual("abc", File.ReadAllText(packetFile.LocalFilePath))

    End Sub

    <TestMethod>
    Public Sub FailedFirstCapture_PreservesExistingSizeAndTimestamp()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        packetFile.FileSizeBytes = 123
        packetFile.LastWriteTimeUtc = New DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim before As String = Snapshot(packetFile)

        Using locked As New FileStream(packetFile.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Assert.AreEqual(PacketFileIntegrityStatus.Unavailable,
                            SubmissionPacketIntegrityService.CaptureBaseline(packetFile).Status)
        End Using

        Assert.AreEqual(before, Snapshot(packetFile))

    End Sub

    <TestMethod>
    Public Sub MetadataOnly_IgnoresStrayLockedPathAndExistingFingerprint()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        packetFile.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
        Dim before As String = Snapshot(packetFile)

        Using locked As New FileStream(packetFile.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Assert.AreEqual(PacketFileIntegrityStatus.MetadataOnly, SubmissionPacketIntegrityService.Inspect(packetFile).Status)
            Assert.AreEqual(PacketFileIntegrityStatus.MetadataOnly, SubmissionPacketIntegrityService.Verify(packetFile).Status)
            Assert.AreEqual(PacketFileIntegrityStatus.MetadataOnly, SubmissionPacketIntegrityService.CaptureBaseline(packetFile).Status)
        End Using

        Assert.AreEqual(before, Snapshot(packetFile))
        Assert.AreEqual("abc", File.ReadAllText(packetFile.LocalFilePath))

    End Sub

    <TestMethod>
    Public Sub CaptureBaseline_RequiresExplicitReplacement()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        File.WriteAllText(packetFile.LocalFilePath, "def", New UTF8Encoding(False))
        Dim before As String = Snapshot(packetFile)

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub() SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        )
        Assert.AreEqual(before, Snapshot(packetFile))
        Assert.AreEqual(PacketFileIntegrityStatus.Changed, SubmissionPacketIntegrityService.Verify(packetFile).Status)

        Dim replacedAt As New DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc)
        Dim replaced As PacketFileIntegrityResult = SubmissionPacketIntegrityService.CaptureBaseline(
            packetFile, replaceExisting:=True, capturedAtUtc:=replacedAt
        )

        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, replaced.Status)
        Assert.AreNotEqual(AbcSha256, packetFile.Sha256)
        Assert.AreEqual(replacedAt, packetFile.HashComputedAtUtc.Value)
        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.Verify(packetFile).Status)

    End Sub

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub Cancellation_PropagatesWithoutChangingRecord(hasBaseline As Boolean)

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        If hasBaseline Then SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim before As String = Snapshot(packetFile)

        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()

            Assert.ThrowsExactly(Of OperationCanceledException)(
                Sub() SubmissionPacketIntegrityService.Verify(packetFile, cancellation.Token)
            )
            Assert.ThrowsExactly(Of OperationCanceledException)(
                Sub() SubmissionPacketIntegrityService.CaptureBaseline(
                    packetFile, replaceExisting:=True, cancellationToken:=cancellation.Token
                )
            )
        End Using

        Assert.AreEqual(before, Snapshot(packetFile))
        Assert.AreEqual("abc", File.ReadAllText(packetFile.LocalFilePath))

    End Sub

    <TestMethod>
    Public Sub InvalidOrDirectoryPath_IsUnavailableInsteadOfMissing()

        Dim packetFile As New SubmissionPacketFile With {
            .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
            .LocalFilePath = _root,
            .Sha256 = AbcSha256
        }
        Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Inspect(packetFile).Status)
        Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Verify(packetFile).Status)

        packetFile.LocalFilePath = "invalid" & ChrW(0) & "path"
        Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Inspect(packetFile).Status)
        Assert.AreEqual(PacketFileIntegrityStatus.Unavailable, SubmissionPacketIntegrityService.Verify(packetFile).Status)

    End Sub

    <TestMethod>
    Public Sub ReadOnlyFile_CanBeCapturedAndVerifiedAlongsideOtherReaders()

        Dim packetFile As SubmissionPacketFile = CreateFile("abc")
        Dim attributes As FileAttributes = File.GetAttributes(packetFile.LocalFilePath)
        File.SetAttributes(packetFile.LocalFilePath, attributes Or FileAttributes.ReadOnly)

        Try
            Using reader As New FileStream(packetFile.LocalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                                SubmissionPacketIntegrityService.CaptureBaseline(packetFile).Status)
                Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                                SubmissionPacketIntegrityService.Verify(packetFile).Status)
            End Using

            Assert.IsTrue((File.GetAttributes(packetFile.LocalFilePath) And FileAttributes.ReadOnly) <> 0)
        Finally
            File.SetAttributes(packetFile.LocalFilePath, attributes)
        End Try

    End Sub

    <TestMethod>
    <DataRow(SubmissionPacketFileStorageMode.LinkedExternal)>
    <DataRow(SubmissionPacketFileStorageMode.ManagedCopy)>
    Public Sub SaveAndLoad_PreservesCapturedBaseline(storageMode As SubmissionPacketFileStorageMode)

        Dim manuscript As New Manuscript With {.Title = "Integrity round trip"}
        Dim version As New ManuscriptVersion With {.Label = "Submission candidate"}
        manuscript.Versions.Add(version)
        Dim packet As SubmissionPacket = SubmissionPacketService.CreatePacket(manuscript, version.Id, "Packet", "")
        Dim source As SubmissionPacketFile = CreateFile("abc")
        Dim packetFile As SubmissionPacketFile = SubmissionPacketService.AddFile(
            packet, SubmissionPacketFileRole.CoverLetter, "Cover letter", "", source.LocalFilePath, storageMode
        )
        SubmissionPacketIntegrityService.CaptureBaseline(packetFile)
        Dim expectedHash As String = packetFile.Sha256
        Dim expectedSize As Long? = packetFile.FileSizeBytes
        Dim expectedWriteTime As DateTime? = packetFile.LastWriteTimeUtc
        Dim expectedCaptured As DateTime? = packetFile.HashComputedAtUtc
        Dim repository As New ManuscriptRepository(Path.Combine(_root, "data"), Path.Combine(_root, "managed"))

        repository.Save(New List(Of Manuscript) From {manuscript})
        Dim loaded As SubmissionPacketFile = repository.Load()(0).SubmissionPackets(0).Files(0)

        Assert.AreEqual(expectedHash, loaded.Sha256)
        Assert.AreEqual(expectedSize, loaded.FileSizeBytes)
        Assert.AreEqual(expectedWriteTime, loaded.LastWriteTimeUtc)
        Assert.AreEqual(expectedCaptured, loaded.HashComputedAtUtc)
        Assert.AreEqual(PacketFileIntegrityStatus.NotChecked, SubmissionPacketIntegrityService.Inspect(loaded).Status)
        Assert.AreEqual(PacketFileIntegrityStatus.Unchanged, SubmissionPacketIntegrityService.Verify(loaded).Status)

        File.WriteAllText(loaded.LocalFilePath, "def", New UTF8Encoding(False))
        Assert.AreEqual(PacketFileIntegrityStatus.Changed, SubmissionPacketIntegrityService.Verify(loaded).Status)
        Assert.AreEqual(expectedHash, loaded.Sha256)

    End Sub

    Private Function CreateFile(contents As String) As SubmissionPacketFile
        Dim filePath As String = Path.Combine(_root, Guid.NewGuid().ToString("N") & ".txt")
        File.WriteAllText(filePath, contents, New UTF8Encoding(False))

        Return New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.CoverLetter,
            .Label = "Cover letter",
            .Notes = "Baseline test notes",
            .OriginalFileName = Path.GetFileName(filePath),
            .LocalFilePath = filePath,
            .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        }
    End Function

    Private Shared Function Snapshot(packetFile As SubmissionPacketFile) As String
        Return JsonSerializer.Serialize(packetFile)
    End Function

End Class
