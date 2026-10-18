Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models

Friend Module TestSupport

    Public Function CreateTemporaryRoot() As String

        Dim root As String =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteTests_" & Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(root)
        Return root

    End Function


    Public Sub DeleteTemporaryRoot(
        root As String
    )

        If String.IsNullOrWhiteSpace(root) Then
            Return
        End If

        Try
            If Directory.Exists(root) Then
                Directory.Delete(root, True)
            End If
        Catch
            ' Test cleanup is best-effort.
        End Try

    End Sub


    ' Where a save or a load puts a managed folder the library no longer
    ' needs: under removed\<date>, keeping the folder's path in the library.
    ' Exactly one dated folder is expected.
    Public Function RemovedCopyOf(
        managedDirectory As String,
        originalDirectory As String
    ) As String

        Dim removedRoot As String =
            Path.Combine(
                managedDirectory,
                "removed"
            )

        Dim dated As String() =
            If(
                Directory.Exists(removedRoot),
                Directory.GetDirectories(removedRoot),
                Array.Empty(Of String)()
            )

        If dated.Length <> 1 OrElse
           Not Regex.IsMatch(
               Path.GetFileName(dated(0)),
               "^\d{4}-\d{2}-\d{2}$"
           ) Then

            Throw New InvalidOperationException(
                "Expected one dated folder under " &
                removedRoot &
                ", found " &
                dated.Length.ToString() &
                "."
            )

        End If

        Return Path.Combine(
            dated(0),
            Path.GetRelativePath(
                managedDirectory,
                originalDirectory
            )
        )

    End Function


    ' Runs a window test on the shared STA runner (#134), which rethrows
    ' what it threw. A modal box would hold the thread; after ninety
    ' seconds the runner closes what that thread left open and fails the
    ' test, so the run reports it instead.
    Public Sub RunOnStaThread(
        action As Action
    )

        StaRunner.Run(
            action,
            TimeSpan.FromSeconds(90)
        )

    End Sub


    ' The Insights figures are worked out off the UI thread (#132): pumps
    ' the window's messages until they are shown, and fails if they are
    ' still being worked out at the limit.
    Public Sub WaitForInsights(
        window As ManuscriptPipeline.Form1,
        limit As TimeSpan
    )

        Dim waited As System.Diagnostics.Stopwatch =
            System.Diagnostics.Stopwatch.StartNew()

        While window.InsightsCalculating AndAlso
              waited.Elapsed < limit

            System.Windows.Forms.Application.DoEvents()
            System.Threading.Thread.Sleep(1)

        End While

        System.Windows.Forms.Application.DoEvents()

        Assert.IsFalse(
            window.InsightsCalculating,
            "Insights finished calculating."
        )

    End Sub


    ' Processes the window's messages while work it does as it idles is
    ' pending, and fails if it still is after a minute.
    Public Sub PumpWhile(
        pending As Func(Of Boolean),
        failure As String
    )

        Dim waited As System.Diagnostics.Stopwatch =
            System.Diagnostics.Stopwatch.StartNew()

        While pending() AndAlso waited.Elapsed < TimeSpan.FromSeconds(60)
            System.Windows.Forms.Application.DoEvents()
            System.Threading.Thread.Sleep(1)
        End While

        Assert.IsFalse(pending(), failure)

    End Sub


    ' The control focus is in: the active control of each container
    ' down from this one.
    Public Function DeepestActiveControl(
        container As System.Windows.Forms.ContainerControl
    ) As System.Windows.Forms.Control

        Dim active As System.Windows.Forms.Control =
            container.ActiveControl

        While TypeOf active Is System.Windows.Forms.ContainerControl AndAlso
              DirectCast(active, System.Windows.Forms.ContainerControl).ActiveControl IsNot Nothing

            active = DirectCast(active, System.Windows.Forms.ContainerControl).ActiveControl

        End While

        Return active

    End Function


    ' The WCAG contrast ratio of two colors, from 1 to 21.
    Public Function ContrastRatio(
        foreground As System.Drawing.Color,
        background As System.Drawing.Color
    ) As Double

        Dim first As Double = RelativeLuminance(foreground)
        Dim second As Double = RelativeLuminance(background)
        Return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05)

    End Function


    ' The WCAG relative luminance of a color, from 0 (black) to 1 (white).
    Public Function RelativeLuminance(
        color As System.Drawing.Color
    ) As Double

        Return 0.2126 * LinearChannel(color.R) + 0.7152 * LinearChannel(color.G) + 0.0722 * LinearChannel(color.B)

    End Function


    Private Function LinearChannel(
        channel As Byte
    ) As Double

        Dim value As Double = channel / 255.0
        Return If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))

    End Function


    ' Puts backup.json in a backup archive in place of any there, as a
    ' newer PaperRoute or a damaged archive would have it (#115); Nothing
    ' leaves the archive without one, as PaperRoute 0.9.1 and earlier wrote.
    Public Sub SetBackupManifest(
        zipPath As String,
        json As String
    )

        Using archive As ZipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update)

            archive.GetEntry("backup.json")?.Delete()

            If json IsNot Nothing Then
                Using writer As New StreamWriter(archive.CreateEntry("backup.json").Open())
                    writer.Write(json)
                End Using
            End If

        End Using

    End Sub


    ' A backup.json as its first format has it, at the given storage schema.
    Public Function BackupManifestJson(
        schemaVersion As Integer,
        Optional createdUtc As String = "2026-10-12T15:30:00Z",
        Optional appVersion As String = "0.9.2"
    ) As String

        Return "{""FormatVersion"":1,""SchemaVersion"":" & schemaVersion.ToString() &
               ",""AppVersion"":""" & appVersion & """,""CreatedUtc"":""" & createdUtc &
               """,""Manuscripts"":3,""Submissions"":2,""EditorialDecisions"":2,""CorrespondenceRecords"":0}"

    End Function


    ' Every file and folder under the root, with each file's bytes (a
    ' folder's are empty), so a test can show that nothing under it changed.
    Public Function SnapshotTree(
        root As String
    ) As Dictionary(Of String, Byte())

        Dim snapshot As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)

        For Each folder As String In Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            snapshot(folder & Path.DirectorySeparatorChar) = Array.Empty(Of Byte)()
        Next

        For Each filePath As String In Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            snapshot(filePath) = File.ReadAllBytes(filePath)
        Next

        Return snapshot

    End Function


    Public Sub AssertSameTree(
        expected As Dictionary(Of String, Byte()),
        root As String,
        message As String
    )

        Dim actual As Dictionary(Of String, Byte()) = SnapshotTree(root)

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message & ": the same files and folders.")

        For Each pair As KeyValuePair(Of String, Byte()) In expected
            CollectionAssert.AreEqual(pair.Value, actual(pair.Key), message & ": " & pair.Key & " is unchanged.")
        Next

    End Sub


    Public Function CreateJsonOptions() As JsonSerializerOptions

        Dim options As New JsonSerializerOptions With {
            .WriteIndented = True,
            .IgnoreReadOnlyProperties = True,
            .PropertyNameCaseInsensitive = True
        }

        options.Converters.Add(New JsonStringEnumConverter())
        Return options

    End Function


    Public Function CreateRepresentativeLibrary() As List(Of Manuscript)

        Dim active As New Manuscript With {
            .Title = "Active Study",
            .CoAuthors = "A. Researcher; B. Scholar",
            .TargetJournal = "Journal of Example Studies",
            .CurrentStage = PaperStage.UnderReview,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = New DateTime(2026, 7, 1)
        }

        Dim activeSubmission As New JournalSubmission With {
            .JournalName = "Journal of Example Studies",
            .ManuscriptNumber = "EX-2026-101",
            .SubmittedDate = New DateTime(2026, 6, 20),
            .Notes = "Round one submission.",
            .PortalUrl = "https://example.invalid/submission"
        }

        activeSubmission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 7, 10),
                .Decision = EditorialDecision.MajorRevision,
                .RevisionDeadline = New DateTime(2026, 9, 1),
                .Notes = "Representative revision decision."
            }
        )

        active.Submissions.Add(activeSubmission)

        Dim published As New Manuscript With {
            .Title = "Published Study",
            .TargetJournal = "Behavioral Examples",
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Published,
            .StageEnteredDate = New DateTime(2026, 5, 15)
        }

        Dim filed As New Manuscript With {
            .Title = "Filed Study",
            .TargetJournal = "Archive of Examples",
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.FileDrawer,
            .FileDrawerDate = New DateTime(2026, 4, 2),
            .FileDrawerReason = "Paused after several submissions."
        }

        Dim filedSubmission As New JournalSubmission With {
            .JournalName = "Archive of Examples",
            .SubmittedDate = New DateTime(2026, 3, 1)
        }

        filedSubmission.Decisions.Add(
            New EditorialDecisionEvent With {
                .DecisionDate = New DateTime(2026, 3, 15),
                .Decision = EditorialDecision.Rejected,
                .Notes = "Representative rejection."
            }
        )

        filed.Submissions.Add(filedSubmission)

        Return New List(Of Manuscript) From {
            active,
            published,
            filed
        }

    End Function

End Module


' Opens storage files as a repository does, except that the first
' `failures` opens of `heldPath` fail the way Windows fails when another
' program holds the file open (#111). No test waits on a real lock.
Friend Class HeldFileOpener

    Private ReadOnly _heldPath As String
    Private ReadOnly _failures As Integer


    Public Sub New(
        heldPath As String,
        failures As Integer
    )

        _heldPath = heldPath
        _failures = failures

    End Sub


    ' How many times the held file was asked for.
    Public Property Opens As Integer


    Public Function Open(
        filePath As String
    ) As Stream

        If String.Equals(
            filePath,
            _heldPath,
            StringComparison.OrdinalIgnoreCase
        ) Then

            Opens += 1

            If Opens <= _failures Then

                Throw New IOException(
                    "The process cannot access the file because it is being used by another process.",
                    &H80070020
                )

            End If

        End If

        Return New FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        )

    End Function

End Class


' Opens a storage file as a repository does, except that reading it fails
' the way Windows fails when another program holds a byte-range lock on
' the file (ERROR_LOCK_VIOLATION): the open itself succeeded (#111).
Friend Class LockedReadStream
    Inherits Stream

    Private _position As Long


    Public Shared Function Open(
        filePath As String
    ) As Stream

        Return New LockedReadStream()

    End Function


    Public Overrides ReadOnly Property CanRead As Boolean
        Get
            Return True
        End Get
    End Property


    Public Overrides ReadOnly Property CanSeek As Boolean
        Get
            Return True
        End Get
    End Property


    Public Overrides ReadOnly Property CanWrite As Boolean
        Get
            Return False
        End Get
    End Property


    ' Not empty, so a loader goes on to read it.
    Public Overrides ReadOnly Property Length As Long
        Get
            Return 1
        End Get
    End Property


    Public Overrides Property Position As Long
        Get
            Return _position
        End Get
        Set(value As Long)
            _position = value
        End Set
    End Property


    Public Overrides Function Read(
        buffer As Byte(),
        offset As Integer,
        count As Integer
    ) As Integer

        Throw New IOException(
            "The process cannot access the file because another process has locked a portion of the file.",
            &H80070021
        )

    End Function


    Public Overrides Sub Flush()
    End Sub


    Public Overrides Function Seek(
        offset As Long,
        origin As SeekOrigin
    ) As Long

        Return _position

    End Function


    Public Overrides Sub SetLength(
        value As Long
    )

        Throw New NotSupportedException()

    End Sub


    Public Overrides Sub Write(
        buffer As Byte(),
        offset As Integer,
        count As Integer
    )

        Throw New NotSupportedException()

    End Sub

End Class
