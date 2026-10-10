Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' One atomic writer for every storage file (#125).
<TestClass>
Public Class StorageFileTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' A successful write takes the live file's place and keeps the previous
    ' file as the backup, with no temporary file left behind.
    <TestMethod>
    Public Sub Write_ReplacesTheLiveFileAndKeepsThePrevious()

        Dim live As String = Path.Combine(_root, "settings.json")
        Dim backup As String = Path.Combine(_root, "settings.bak")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "first"))
        Assert.AreEqual("first", File.ReadAllText(live))
        Assert.IsFalse(File.Exists(backup), "Nothing to keep yet.")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "second"))
        Assert.AreEqual("second", File.ReadAllText(live))
        Assert.AreEqual("first", File.ReadAllText(backup), "The previous file is the backup.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length, "No temporary file is left.")

        ' Without a backup path the previous file is simply replaced.
        StorageFile.Write(live, Nothing, Sub(stream) WriteText(stream, "third"))
        Assert.AreEqual("third", File.ReadAllText(live))
        Assert.AreEqual("first", File.ReadAllText(backup))

    End Sub


    ' A failure before the replace leaves the live file byte-identical, the
    ' backup as it was, and no temporary file behind.
    <TestMethod>
    Public Sub Write_FailureBeforeTheReplaceLeavesTheLiveFileAndNoTemp()

        Dim live As String = Path.Combine(_root, "authors.json")
        Dim backup As String = Path.Combine(_root, "authors.bak")

        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "first"))
        StorageFile.Write(live, backup, Sub(stream) WriteText(stream, "second"))
        Dim liveBefore As Byte() = File.ReadAllBytes(live)
        Dim backupBefore As Byte() = File.ReadAllBytes(backup)

        Dim failure As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub()
                    StorageFile.Write(live, backup,
                        Sub(stream)
                            WriteText(stream, "half of the third")
                            Throw New InvalidOperationException("The disk is full.")
                        End Sub)
                End Sub)

        Assert.AreEqual("The disk is full.", failure.Message)
        CollectionAssert.AreEqual(liveBefore, File.ReadAllBytes(live), "The live file is untouched.")
        CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backup), "The backup is untouched.")
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length, "No temporary file is left.")

    End Sub


    ' No storage service writes a live data file with File.WriteAllText,
    ' which leaves an empty or torn file after a power cut at the wrong
    ' moment. StorageMigrationService keeps two: the migration receipt,
    ' which is diagnostic, and the copied data's path rewrite, which is
    ' not yet the live file.
    <TestMethod>
    Public Sub StorageServices_WriteLiveFilesOnlyThroughTheAtomicWriter()

        Dim services As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Services")

        Dim allowed As New Dictionary(Of String, String()) From {
            {"StorageMigrationService.vb", {"dataPath,", "receiptPath,"}}
        }

        For Each name As String In {
            "ManuscriptRepository.vb",
            "AuthorLibraryRepository.vb",
            "CitationStore.vb",
            "AppSettingsService.vb",
            "StorageMigrationService.vb",
            "Schema5MigrationService.vb",
            "Schema6MigrationService.vb",
            "Schema7MigrationService.vb",
            "StorageFile.vb"
        }

            Dim lines As String() =
                File.ReadAllLines(Path.Combine(services, name)).
                    Where(Function(line) Not line.TrimStart().StartsWith("'"c)).
                    ToArray()

            Dim targets As New List(Of String)()

            For index As Integer = 0 To lines.Length - 1

                If Not lines(index).Contains("File.WriteAllText(", StringComparison.Ordinal) Then Continue For

                ' The file written: on the same line, or the next non-blank one.
                Dim rest As String = lines(index).Substring(lines(index).IndexOf("File.WriteAllText(", StringComparison.Ordinal) + "File.WriteAllText(".Length).Trim()
                Dim following As Integer = index + 1
                While rest.Length = 0 AndAlso following < lines.Length
                    rest = lines(following).Trim()
                    following += 1
                End While
                targets.Add(rest)

            Next

            Dim expected As String() = If(allowed.ContainsKey(name), allowed(name), New String() {})
            CollectionAssert.AreEqual(expected, targets, name & " writes " & String.Join(", ", targets) & " with File.WriteAllText.")

        Next

        Dim writer As String = File.ReadAllText(Path.Combine(services, "StorageFile.vb"))
        StringAssert.Contains(writer, "flushToDisk:=True", "The writer flushes to disk before the replace.")

    End Sub


    Private Shared Sub WriteText(stream As Stream, content As String)
        Dim bytes As Byte() = System.Text.Encoding.UTF8.GetBytes(content)
        stream.Write(bytes, 0, bytes.Length)
    End Sub

End Class
