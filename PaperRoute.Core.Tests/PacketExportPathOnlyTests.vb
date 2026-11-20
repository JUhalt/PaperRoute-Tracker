Imports System
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Packet export where no file identity is known (core stage 2, step 11):
' the core's default, and the answer on Linux and macOS in v1.1. The
' destination is then compared with the packet's own files by path only:
' the same path (however Path.GetFullPath spells it) is still refused, so a
' packet file is never overwritten by its own .zip; a different path is
' never taken for a packet file; and nothing throws for want of an identity.
' On Windows, PaperRoute.Tests runs the same export with WindowsFileIdentity
' registered (PacketExportTests).
<TestClass>
<DoNotParallelize>
Public Class PacketExportPathOnlyTests

    Private Shared ReadOnly ExportTime As New DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc)
    Private Const TestVersion As String = "1.1.0-test"

    Private _root As String


    <TestInitialize>
    Public Sub CreateRoot()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub RemoveRoot()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub NoIdentityIsKnownAndAskingNeverThrows()

        Assert.AreSame(NoFileIdentity.Instance, PlatformServices.FileIdentity)

        Dim existing As String = Path.Combine(_root, "here.txt")
        File.WriteAllText(existing, "here")
        For Each filePath As String In {existing, Path.Combine(_root, "not here.txt"), "", "   ", Nothing}
            Assert.IsNull(PlatformServices.FileIdentity.Identify(filePath), If(filePath, "(Nothing)"))
        Next

    End Sub


    <TestMethod>
    Public Sub ASmallPacketExports()

        Dim plan As PacketExportPlan = CheckedPlan()
        Dim zipPath As String = Path.Combine(OutputFolder(), "packet.zip")

        Dim result As PacketExportResult = PacketExportService.WriteZip(plan, zipPath, ExportTime, TestVersion)

        Assert.AreEqual(2, result.FileCount)
        Assert.IsTrue(File.Exists(zipPath))
        Assert.AreEqual(0, Directory.GetFiles(OutputFolder(), "*.partial").Length)
        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            Assert.AreEqual(2, archive.Entries.Where(Function(entry) entry.FullName.StartsWith("files/", StringComparison.Ordinal)).Count())
        End Using

    End Sub


    ' What IsPacketFile protects against: the .zip written over one of the
    ' packet's own files. By path, that still holds without an identity.
    <TestMethod>
    Public Sub APacketFileByPathIsRefusedAndKept()

        Dim plan As PacketExportPlan = CheckedPlan()
        Dim supplement As String = plan.Rows.Single(Function(row) row.Label = "Data").Source.LocalFilePath
        Dim folder As String = Path.GetDirectoryName(supplement)
        Dim name As String = Path.GetFileName(supplement)
        Dim before As Byte() = File.ReadAllBytes(supplement)

        ' The path itself, and spellings Path.GetFullPath turns into it.
        For Each destination As String In {
            supplement,
            Path.Combine(folder, ".", name),
            Path.Combine(folder, "elsewhere", "..", name)
        }

            Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(
                Sub() PacketExportService.WriteZip(plan, destination, ExportTime, TestVersion))

            Assert.AreEqual(PacketExportFailure.DestinationUnwritable, failure.Kind, destination)
            Assert.AreEqual("That file is part of this packet. Choose another name for the .zip.", failure.Message, destination)
            CollectionAssert.AreEqual(before, File.ReadAllBytes(supplement), "A packet file is never overwritten: " & destination)
            Assert.AreEqual(0, Directory.GetFiles(folder, "*.partial").Length, destination)

        Next

    End Sub


    ' Two different paths are never one file: a copy of a packet file, with
    ' the same bytes, is an ordinary destination and is replaced.
    <TestMethod>
    Public Sub ACopyOfAPacketFileIsNotThePacketFile()

        Dim plan As PacketExportPlan = CheckedPlan()
        Dim supplement As String = plan.Rows.Single(Function(row) row.Label = "Data").Source.LocalFilePath
        Dim before As Byte() = File.ReadAllBytes(supplement)
        Dim copy As String = Path.Combine(OutputFolder(), "copy of the supplement.zip")
        File.Copy(supplement, copy)

        Dim result As PacketExportResult = PacketExportService.WriteZip(plan, copy, ExportTime, TestVersion)

        Assert.AreEqual(2, result.FileCount)
        Using archive As ZipArchive = ZipFile.OpenRead(copy)
            Assert.IsTrue(archive.Entries.Count > 2, "The copy now holds the packet.")
        End Using
        CollectionAssert.AreEqual(before, File.ReadAllBytes(supplement), "The packet file itself is unchanged.")

    End Sub


    ' A packet of two default-included files, written under the temporary
    ' root and checked, as the export window checks it before writing.
    Private Function CheckedPlan() As PacketExportPlan

        Dim manuscript As New Manuscript With {.Title = "A test manuscript"}
        Dim version As New ManuscriptVersion With {.Label = "Version 1"}
        manuscript.Versions.Add(version)
        Dim packet As New SubmissionPacket With {.Label = "Test packet", .ManuscriptVersionId = version.Id, .JournalName = "Test Journal"}
        manuscript.SubmissionPackets.Add(packet)

        Dim folder As String = Path.Combine(_root, "files")
        Directory.CreateDirectory(folder)
        packet.Files.Add(PacketFile(folder, SubmissionPacketFileRole.Supplement, "Data", "data.txt", "the packet's own supplement"))
        packet.Files.Add(PacketFile(folder, SubmissionPacketFileRole.Table, "Table", "table.csv", "a,b" & vbCrLf & "1,2" & vbCrLf))

        Dim plan As PacketExportPlan = PacketExportService.Prepare(manuscript, packet, New AuthorLibraryData())
        PacketExportService.CheckFiles(plan)
        Assert.AreEqual(2, plan.IncludedRows().Count, "Both files are included by default.")
        Return plan

    End Function


    Private Shared Function PacketFile(folder As String, role As SubmissionPacketFileRole, label As String, fileName As String, content As String) As SubmissionPacketFile

        Dim stored As String = Path.Combine(folder, fileName)
        File.WriteAllBytes(stored, Encoding.UTF8.GetBytes(content))
        Return New SubmissionPacketFile With {
            .Role = role, .Label = label, .OriginalFileName = fileName,
            .LocalFilePath = stored, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        }

    End Function


    Private Function OutputFolder() As String
        Dim folder As String = Path.Combine(_root, "out")
        Directory.CreateDirectory(folder)
        Return folder
    End Function

End Class
