Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class PacketIntegrityIntegrationTests

    <TestMethod>
    <DataRow(False)>
    <DataRow(True)>
    Public Sub VaultCapture_AdoptsFingerprintOnlyWhenVaultIsSaved(saveVault As Boolean)

        Using fixture As New IntegrityFixture()
            Dim manuscript As Manuscript = fixture.CreateManuscript()
            Dim original As String = Snapshot(manuscript)
            Dim originalBytes As Byte() = File.ReadAllBytes(fixture.SourcePath)
            Dim originalWriteTime As DateTime = File.GetLastWriteTimeUtc(fixture.SourcePath)

            RunOnStaThread(
                Sub()
                    Using dialog As New NonactivatingPacketVaultForm(manuscript)
                        ShowOffscreen(dialog)
                        PumpUntilComplete(dialog.RecordSelectedFingerprintAsync(False))

                        Assert.AreEqual(original, Snapshot(manuscript),
                            "Recording a fingerprint must only affect the vault working copy.")
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Unchanged"))

                        If saveVault Then
                            dialog.AcceptButton.PerformClick()
                            Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                        Else
                            dialog.CancelButton.PerformClick()
                            Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                        End If
                        dialog.Close()
                    End Using
                End Sub
            )

            If saveVault Then
                Dim packetFile As SubmissionPacketFile = manuscript.SubmissionPackets.Single().Files.Single()
                Assert.AreEqual(64, packetFile.Sha256.Length)
                Assert.IsTrue(packetFile.HashComputedAtUtc.HasValue)
                Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                    SubmissionPacketIntegrityService.Verify(packetFile).Status)
                AssertLifecycleUnchanged(manuscript)
            Else
                Assert.AreEqual(original, Snapshot(manuscript),
                    "Cancel must discard the captured fingerprint and packet timestamp.")
            End If

            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(fixture.SourcePath))
            Assert.AreEqual(originalWriteTime, File.GetLastWriteTimeUtc(fixture.SourcePath))
        End Using

    End Sub

    <TestMethod>
    Public Sub VaultCheck_ReportsChangedWithoutReplacingBaselineOrChangingLifecycle()

        Using fixture As New IntegrityFixture()
            Dim manuscript As Manuscript = fixture.CreateManuscript()
            Dim packetFile As SubmissionPacketFile = manuscript.SubmissionPackets.Single().Files.Single()
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.CaptureBaseline(packetFile, False).Status)
            Dim original As String = Snapshot(manuscript)
            Dim recordedWriteTime As DateTime = packetFile.LastWriteTimeUtc.Value
            File.WriteAllText(fixture.SourcePath, "edited bytes")
            File.SetLastWriteTimeUtc(fixture.SourcePath, recordedWriteTime)
            Dim editedBytes As Byte() = File.ReadAllBytes(fixture.SourcePath)

            RunOnStaThread(
                Sub()
                    Using dialog As New NonactivatingPacketVaultForm(manuscript)
                        ShowOffscreen(dialog)
                        PumpUntilComplete(dialog.CheckPacketFilesAsync())
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Changed"))
                        Assert.AreEqual(original, Snapshot(manuscript))
                        dialog.AcceptButton.PerformClick()
                        Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                        dialog.Close()
                    End Using
                End Sub
            )

            Assert.AreEqual(original, Snapshot(manuscript),
                "Saving check results must not rewrite fingerprints, packet timestamps, or manuscript state.")
            AssertLifecycleUnchanged(manuscript)
            CollectionAssert.AreEqual(editedBytes, File.ReadAllBytes(fixture.SourcePath))
            Assert.AreEqual(recordedWriteTime, File.GetLastWriteTimeUtc(fixture.SourcePath))
        End Using

    End Sub

    <TestMethod>
    Public Sub VaultCheck_DuplicateFileIdsInDifferentPacketsKeepSeparateObservations()

        Using fixture As New IntegrityFixture()
            Dim manuscript As Manuscript = fixture.CreateManuscript()
            Dim firstPacket As SubmissionPacket = manuscript.SubmissionPackets.Single()
            firstPacket.CreatedAtUtc = New DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)
            Dim firstFile As SubmissionPacketFile = firstPacket.Files.Single()
            Dim secondPath As String = Path.Combine(fixture.Root, "second-packet-source.txt")
            File.WriteAllText(secondPath, "second bytes")
            Dim secondPacket As SubmissionPacket = SubmissionPacketService.CreatePacket(
                manuscript, manuscript.CurrentVersionId.Value, "Second packet", "Separate comparison point",
                createdAtUtc:=firstPacket.CreatedAtUtc.AddHours(-1))
            Dim secondFile As SubmissionPacketFile = SubmissionPacketService.AddFile(
                secondPacket, SubmissionPacketFileRole.CoverLetter, "Second cover letter", String.Empty,
                secondPath, SubmissionPacketFileStorageMode.LinkedExternal)
            secondFile.Id = firstFile.Id
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.CaptureBaseline(firstFile, False).Status)
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.CaptureBaseline(secondFile, False).Status)
            Assert.AreNotEqual(firstFile.Sha256, secondFile.Sha256)
            File.WriteAllText(secondPath, "second edits")
            ' This is valid persisted data: each packet has its own file-ID scope.
            SubmissionReadinessValidationService.NormalizeAndValidateManuscript(manuscript)
            Dim original As String = Snapshot(manuscript)

            RunOnStaThread(
                Sub()
                    Using dialog As New NonactivatingPacketVaultForm(manuscript)
                        ShowOffscreen(dialog)
                        Dim packetDetail As TextBox = Descendants(dialog).OfType(Of TextBox)().Single(
                            Function(item) item.AccessibleName = "Selected packet details")
                        Dim packetList As ListBox = packetDetail.Parent.Controls.OfType(Of ListBox)().Single()
                        Assert.AreEqual(0, packetList.SelectedIndex)
                        Assert.IsTrue(FileDetail(dialog).Text.Contains(fixture.SourcePath))
                        PumpUntilComplete(dialog.CheckPacketFilesAsync())
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Unchanged"))

                        packetList.SelectedIndex = 1
                        Application.DoEvents()
                        Assert.IsTrue(FileDetail(dialog).Text.Contains(secondPath))
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Not checked"),
                            "Another packet's file with the same ID must not inherit an Unchanged result.")
                        PumpUntilComplete(dialog.CheckPacketFilesAsync())
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Changed"))

                        packetList.SelectedIndex = 0
                        Application.DoEvents()
                        Assert.IsTrue(FileDetail(dialog).Text.Contains("Status: Unchanged"),
                            "Checking the second packet must not overwrite the first packet's observation.")
                        dialog.AcceptButton.PerformClick()
                        dialog.Close()
                    End Using
                End Sub)

            Assert.AreEqual(original, Snapshot(manuscript))
            Assert.AreEqual("source bytes", File.ReadAllText(fixture.SourcePath))
            Assert.AreEqual("second edits", File.ReadAllText(secondPath))
        End Using

    End Sub

    <TestMethod>
    Public Sub VaultCloseDuringCapture_DiscardsBackgroundResults()

        Using fixture As New IntegrityFixture()
            ' A disposable larger file gives the background operation time to
            ' remain pending while the form closes, without touching user data.
            Using stream As New FileStream(fixture.SourcePath, FileMode.Open, FileAccess.Write)
                stream.SetLength(16L * 1024 * 1024)
            End Using
            Dim manuscript As Manuscript = fixture.CreateManuscript()
            Dim original As String = Snapshot(manuscript)

            RunOnStaThread(
                Sub()
                    Using dialog As New NonactivatingPacketVaultForm(manuscript)
                        ShowOffscreen(dialog)
                        Dim pending As Task = dialog.RecordSelectedFingerprintAsync(False)
                        If Not pending.IsCompleted Then
                            Assert.IsFalse(DirectCast(dialog.AcceptButton, Control).Enabled,
                                "Saving must be disabled while a fingerprint is being recorded.")
                        End If
                        dialog.Close()
                        PumpUntilComplete(pending)
                        Assert.IsTrue(dialog.IsDisposed)
                    End Using
                End Sub
            )

            Assert.AreEqual(original, Snapshot(manuscript),
                "A late hash completion after closing must not alter the manuscript.")
            Assert.AreEqual(16L * 1024 * 1024, New FileInfo(fixture.SourcePath).Length)
        End Using

    End Sub

    <TestMethod>
    Public Sub ManagedSave_SourceChangedAfterCapturePreservesOriginalBaseline()

        Using fixture As New IntegrityFixture()
            Dim manuscript As Manuscript = fixture.CreateManuscript(SubmissionPacketFileStorageMode.ManagedCopy)
            Dim packetFile As SubmissionPacketFile = manuscript.SubmissionPackets.Single().Files.Single()
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.CaptureBaseline(packetFile, False).Status)
            Dim baseline As SubmissionPacketFile = ManuscriptCloneService.CloneSubmissionPacketFile(packetFile)
            File.WriteAllText(fixture.SourcePath, "edited bytes")
            File.SetLastWriteTimeUtc(fixture.SourcePath, baseline.LastWriteTimeUtc.Value)

            Dim managedRoot As String = Path.Combine(fixture.Root, "managed")
            Dim repository As New ManuscriptRepository(Path.Combine(fixture.Root, "data"), managedRoot)
            repository.Save(New List(Of Manuscript) From {manuscript})

            Dim reloaded As Manuscript = repository.Load().Single()
            Dim managedFile As SubmissionPacketFile = reloaded.SubmissionPackets.Single().Files.Single()
            Assert.AreNotEqual(fixture.SourcePath, managedFile.LocalFilePath)
            Assert.IsTrue(New ManagedLibraryService(managedRoot).IsManagedPath(managedFile.LocalFilePath))
            AssertBaselineEqual(baseline, managedFile)
            Assert.AreEqual(PacketFileIntegrityStatus.Changed,
                SubmissionPacketIntegrityService.Verify(managedFile).Status,
                "Copying a changed source must not silently accept new bytes as the original baseline.")
            Assert.AreEqual("edited bytes", File.ReadAllText(managedFile.LocalFilePath))
            Assert.AreEqual("edited bytes", File.ReadAllText(fixture.SourcePath))
            AssertLifecycleUnchanged(reloaded)
        End Using

    End Sub

    <TestMethod>
    Public Sub PortableRestore_RebasedSnapshotRemainsUnchangedWhenModificationTimeDiffers()

        Using fixture As New IntegrityFixture()
            Dim manuscript As Manuscript = fixture.CreateManuscript(SubmissionPacketFileStorageMode.ManagedCopy)
            Dim sourceData As String = Path.Combine(fixture.Root, "source-data")
            Dim sourceManaged As String = Path.Combine(fixture.Root, "source-library")
            Dim repository As New ManuscriptRepository(sourceData, sourceManaged)
            Dim manuscripts As New List(Of Manuscript) From {manuscript}
            Dim packetFile As SubmissionPacketFile = manuscript.SubmissionPackets.Single().Files.Single()
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.CaptureBaseline(packetFile, False).Status)
            repository.Save(manuscripts)
            Dim baseline As SubmissionPacketFile = ManuscriptCloneService.CloneSubmissionPacketFile(packetFile)
            Dim authors As New AuthorLibraryRepository(sourceData)
            authors.Save(New AuthorLibraryData())
            Dim backupPath As String = Path.Combine(fixture.Root, "packet.zip")
            Dim backup As New PortableBackupService(sourceManaged)
            backup.CreateBackup(backupPath, manuscripts, repository)

            Dim targetManaged As String = Path.Combine(fixture.Root, "restored-library")
            Dim targetRepository As New ManuscriptRepository(Path.Combine(fixture.Root, "restored-data"), targetManaged)
            Dim restore As New PortableRestoreService(targetManaged)
            restore.RestoreBackup(backupPath, New List(Of Manuscript)(), targetRepository)
            Dim restored As Manuscript = targetRepository.Load().Single()
            Dim restoredFile As SubmissionPacketFile = restored.SubmissionPackets.Single().Files.Single()
            File.SetLastWriteTimeUtc(restoredFile.LocalFilePath, baseline.LastWriteTimeUtc.Value.AddDays(1))

            Assert.AreNotEqual(baseline.LocalFilePath, restoredFile.LocalFilePath)
            AssertBaselineEqual(baseline, restoredFile)
            Assert.AreNotEqual(baseline.LastWriteTimeUtc.Value, File.GetLastWriteTimeUtc(restoredFile.LocalFilePath))
            Assert.AreEqual(PacketFileIntegrityStatus.Unchanged,
                SubmissionPacketIntegrityService.Verify(restoredFile).Status,
                "Portable restore changes the path; integrity must compare content rather than filesystem timestamps.")
            AssertBaselineEqual(baseline, restoredFile)
            Assert.AreEqual("source bytes", File.ReadAllText(fixture.SourcePath))
            AssertLifecycleUnchanged(restored)
        End Using

    End Sub

    Private Shared Sub AssertBaselineEqual(expected As SubmissionPacketFile, actual As SubmissionPacketFile)
        Assert.AreEqual(expected.Sha256, actual.Sha256)
        Assert.AreEqual(expected.FileSizeBytes, actual.FileSizeBytes)
        Assert.AreEqual(expected.LastWriteTimeUtc, actual.LastWriteTimeUtc)
        Assert.AreEqual(expected.HashComputedAtUtc, actual.HashComputedAtUtc)
    End Sub

    Private Shared Sub AssertLifecycleUnchanged(manuscript As Manuscript)
        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(ManuscriptLocation.Pipeline, manuscript.Location)
        Assert.AreEqual(New DateTime(2026, 9, 1), manuscript.StageEnteredDate)
        Assert.AreEqual(0, manuscript.Submissions.Count)
        Assert.AreEqual(1, manuscript.History.Count)
        Assert.AreEqual("Draft prepared", manuscript.History.Single().Note)
        Assert.AreEqual(manuscript.Versions.Single().Id, manuscript.CurrentVersionId.Value)
        Assert.AreEqual(manuscript.CurrentVersionId.Value, manuscript.SubmissionPackets.Single().ManuscriptVersionId)
    End Sub

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private Shared Function FileDetail(dialog As Form) As TextBox
        Return Descendants(dialog).OfType(Of TextBox)().Single(
            Function(item) item.AccessibleName = "Selected file details")
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub ShowOffscreen(dialog As Form)
        dialog.ShowInTaskbar = False
        dialog.Opacity = 0
        dialog.StartPosition = FormStartPosition.Manual
        dialog.Location = New Point(-20000, -20000)
        dialog.Show()
        Application.DoEvents()
        Assert.IsTrue(dialog.Visible)
    End Sub

    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The isolated integrity operation timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Shared Sub RunOnStaThread(testAction As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    testAction()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "The isolated integrity UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private NotInheritable Class IntegrityFixture
        Implements IDisposable

        Public ReadOnly Property Root As String = CreateTemporaryRoot()
        Public ReadOnly Property SourcePath As String

        Public Sub New()
            SourcePath = Path.Combine(Root, "packet-source.txt")
            File.WriteAllText(SourcePath, "source bytes")
        End Sub

        Public Function CreateManuscript(
            Optional storageMode As SubmissionPacketFileStorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        ) As Manuscript
            Dim manuscript As New Manuscript With {
                .Title = "Disposable packet integrity study",
                .CurrentStage = PaperStage.Draft,
                .Location = ManuscriptLocation.Pipeline,
                .StageEnteredDate = New DateTime(2026, 9, 1)
            }
            manuscript.History.Add(New HistoryEvent With {
                .Stage = PaperStage.Draft, .EventDate = manuscript.StageEnteredDate, .Note = "Draft prepared"
            })
            Dim version As New ManuscriptVersion With {.Label = "Exact packet version"}
            manuscript.Versions.Add(version)
            manuscript.CurrentVersionId = version.Id
            Dim packet As SubmissionPacket = SubmissionPacketService.CreatePacket(
                manuscript, version.Id, "Prepared packet", "Not a recorded submission")
            SubmissionPacketService.AddFile(packet, SubmissionPacketFileRole.CoverLetter,
                "Cover letter", "Disposable fixture", SourcePath, storageMode)
            Return manuscript
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            DeleteTemporaryRoot(Root)
        End Sub
    End Class

    Private Class NonactivatingPacketVaultForm
        Inherits SubmissionPacketVaultForm

        Public Sub New(manuscript As Manuscript)
            MyBase.New(manuscript)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
