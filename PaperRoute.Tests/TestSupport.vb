Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
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
