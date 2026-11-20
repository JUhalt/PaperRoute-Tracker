Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Tests of a rule core stage 2 keeps for Windows only, for now (design d2,
' section 5.3): another program holding a file open stops PaperRoute
' replacing, moving or deleting it, and "file in use" is recognised from
' the Windows error codes. On Linux and macOS a lock is advisory and
' replacing, moving or deleting an open file succeeds, so these tests stay
' in PaperRoute.Tests, which runs on Windows only. Each was cut unchanged
' from the core test class of the same name without "Sharing", in
' PaperRoute.Core.Tests, with copies of the helpers it shares there.
<TestClass>
Public Class BackupRestoreSharingTests

    Private _root As String = String.Empty

    ' The HResult of the IOException Directory.Move raises for a folder
    ' with a file held inside (#295).
    Private Const AccessDeniedHResult As Integer = &H80070005

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
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


    ' The rollback's move is tried again too (#295). The move into the
    ' managed folder is refused, and a file of the library set aside is
    ' held, so Windows itself refuses to put the library back until the
    ' hold ends during a wait. The library goes back as it was, with nothing
    ' beside it. Before the change it stayed set aside.
    <TestMethod>
    Public Sub Restore_PutsTheLibraryBackOnceItsHeldFileIsFree()

        Dim scenario As RestoreScenario = CreateRestoreScenario()
        Dim before As Dictionary(Of String, Byte()) = SnapshotLibrary(scenario)
        Dim heldFile As String =
            Path.GetRelativePath(scenario.TargetManaged, Directory.GetFiles(scenario.TargetManaged, "*", SearchOption.AllDirectories).First())
        Dim service As New BusyFolderRestoreService(
            scenario.TargetManaged,
            Integer.MaxValue,
            Function(folder) New IOException("Access to the path '" & folder & "' is denied.", AccessDeniedHResult),
            intoFolder:=scenario.TargetManaged,
            holdOnceSetAside:=heldFile
        )

        Dim failure As RestoreFailedException = Nothing

        Try
            failure = Assert.ThrowsExactly(Of RestoreFailedException)(
                Sub() service.RestoreBackup(scenario.BackupPath, scenario.Current, scenario.TargetRepository))
        Finally
            service.ReleaseHold()
        End Try

        Assert.IsTrue(service.WaitsWithTheLibraryAside > 0, "Putting the library back waited for the held file.")
        StringAssert.Contains(failure.Message, "put back as it was")
        Assert.AreEqual(0, failure.LeftBehind.Count)
        AssertSameFiles(before, SnapshotLibrary(scenario), "The library on disk after its held file was let go")
        Assert.AreEqual(
            0,
            Directory.GetFileSystemEntries(_root, "*restore-*", SearchOption.AllDirectories).Length,
            "Nothing of the restore is left beside the library."
        )

    End Sub


    ' The removed folder's move into the restored library is tried again
    ' too (#295): with a file inside it held once the old library is set
    ' aside, the move waits for the hold to end, and the old library does
    ' not stay beside the new one. Before the change it stayed.
    <TestMethod>
    Public Sub Restore_CarriesABusyRemovedFolderOverOnceItIsFree()

        Dim scenario As RestoreScenario = CreateRestoreScenario()
        Dim removedFile As String = Path.Combine(scenario.TargetManaged, "removed", "2026-10-09", "old-snapshot.txt")
        Directory.CreateDirectory(Path.GetDirectoryName(removedFile))
        File.WriteAllText(removedFile, "The one copy.")
        Dim service As New BusyFolderRestoreService(
            scenario.TargetManaged,
            0,
            Nothing,
            holdOnceSetAside:=Path.GetRelativePath(scenario.TargetManaged, removedFile)
        )

        Try
            service.RestoreBackup(scenario.BackupPath, scenario.Current, scenario.TargetRepository)
        Finally
            service.ReleaseHold()
        End Try

        Assert.IsTrue(service.Waits.Count > 0, "The move of the removed folder waited for the held file.")
        Assert.AreEqual("The one copy.", File.ReadAllText(removedFile), "The removed folder comes across to the restored library.")
        Assert.AreEqual(3, scenario.TargetRepository.Load().Count, "The library is the backup's.")
        Assert.AreEqual(
            0,
            Directory.GetDirectories(_root, "*.restore-rollback-*").Length,
            "Nothing of the old library is left beside the new one."
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


    Private Function CreateRestoreScenario(Optional targetManagedName As String = "scenario-target-library") As RestoreScenario

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
            .TargetManaged = Path.Combine(_root, targetManagedName)
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


    ' Refuses the swap's folder moves as OneDrive or Defender does while it
    ' holds a file inside (#295): each move busyAttempts times before it goes
    ' through, or, given intoFolder, only the move into that folder. Given
    ' holdOnceSetAside, a path inside the library, that file is held once the
    ' library is first set aside, so Windows itself refuses to move the
    ' folder that holds it until the next wait. The moves that went through,
    ' the waits between tries and the waits with the library not in its
    ' place are recorded; no wait is slept.
    Private NotInheritable Class BusyFolderRestoreService
        Inherits PortableRestoreService

        Private ReadOnly _managedRoot As String
        Private ReadOnly _busyAttempts As Integer
        Private ReadOnly _refusal As Func(Of String, Exception)
        Private ReadOnly _intoFolder As String
        Private ReadOnly _holdOnceSetAside As String
        Private ReadOnly _refusals As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Private _held As FileStream
        Private _holdTaken As Boolean

        Public ReadOnly Property Moves As New List(Of String)()
        Public ReadOnly Property Waits As New List(Of Integer)()
        Public Property WaitsWithTheLibraryAside As Integer

        Public Sub New(managedLibraryRootDirectory As String, busyAttempts As Integer, refusal As Func(Of String, Exception), Optional intoFolder As String = Nothing, Optional holdOnceSetAside As String = Nothing)
            MyBase.New(managedLibraryRootDirectory)
            _managedRoot = Path.GetFullPath(managedLibraryRootDirectory)
            _busyAttempts = busyAttempts
            _refusal = refusal
            _intoFolder = If(intoFolder Is Nothing, Nothing, Path.GetFullPath(intoFolder))
            _holdOnceSetAside = holdOnceSetAside
        End Sub

        Friend Overrides Sub MoveDirectory(sourceDirectory As String, destinationDirectory As String)
            Dim refused As Integer = 0
            _refusals.TryGetValue(sourceDirectory, refused)
            If refused < _busyAttempts AndAlso
               (_intoFolder Is Nothing OrElse String.Equals(Path.GetFullPath(destinationDirectory), _intoFolder, StringComparison.OrdinalIgnoreCase)) Then
                _refusals(sourceDirectory) = refused + 1
                Throw _refusal(sourceDirectory)
            End If
            MyBase.MoveDirectory(sourceDirectory, destinationDirectory)
            Moves.Add(Path.GetFileName(sourceDirectory) & " -> " & Path.GetFileName(destinationDirectory))
            If _holdOnceSetAside IsNot Nothing AndAlso Not _holdTaken AndAlso
               String.Equals(Path.GetFullPath(sourceDirectory), _managedRoot, StringComparison.OrdinalIgnoreCase) Then
                _holdTaken = True
                _held = New FileStream(Path.Combine(destinationDirectory, _holdOnceSetAside), FileMode.Open, FileAccess.Read, FileShare.None)
            End If
        End Sub

        Friend Overrides Sub WaitBeforeRetry(milliseconds As Integer)
            Waits.Add(milliseconds)
            If Not Directory.Exists(_managedRoot) Then
                WaitsWithTheLibraryAside += 1
            End If
            ReleaseHold()
        End Sub

        Public Sub ReleaseHold()
            If _held IsNot Nothing Then
                _held.Dispose()
                _held = Nothing
            End If
        End Sub
    End Class

End Class
