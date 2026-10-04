Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Security
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports ManuscriptPipeline.Models
Imports Microsoft.Win32.SafeHandles

Namespace Services

    ' A file's state in a packet export (#45). The first three can be
    ' included; the rest can't.
    Public Enum PacketExportFingerprint
        Unchanged
        Changed
        NotRecorded
        Missing
        Unreadable
        NoFile
        Pending
    End Enum


    Public Enum PacketExportFailure
        SourceMissing
        SourceUnreadable
        SourceChangedWhileReading
        ChangedSincePreview
        DestinationUnwritable
        NothingIncluded
        Cancelled
    End Enum


    ' A failed export, with a plain message that names files only by their
    ' names in the package, never by where they are kept.
    Public NotInheritable Class PacketExportException
        Inherits Exception

        Public Sub New(kind As PacketExportFailure, message As String, Optional inner As Exception = Nothing)
            MyBase.New(message, inner)
            Me.Kind = kind
        End Sub

        Public ReadOnly Property Kind As PacketExportFailure

    End Class


    Public NotInheritable Class PacketExportAuthor

        Friend Sub New(name As String, orcid As String, familyName As String, affiliations As IEnumerable(Of String))
            Me.Name = name
            Me.Orcid = orcid
            Me.FamilyName = familyName
            Me.Affiliations = affiliations.ToList().AsReadOnly()
        End Sub

        ' The author's display name.
        Public ReadOnly Property Name As String

        ' Normalized, and only when its checksum passes; otherwise "".
        Public ReadOnly Property Orcid As String

        ' Used only to warn about names in an anonymized package.
        Public ReadOnly Property FamilyName As String

        ' "Department, Institution" or "Institution".
        Public ReadOnly Property Affiliations As IReadOnlyList(Of String)

    End Class


    ' One packet file in the export preview. It holds a copy of the record,
    ' never the record itself.
    Public NotInheritable Class PacketExportRow

        Private _include As Boolean
        Private _outputName As String = String.Empty
        Private _note As String = String.Empty
        Private _sizeBytes As Long?
        Private _fingerprint As PacketExportFingerprint
        Private _observedSha256 As String = String.Empty
        Private _hidden As HiddenMetadataReport

        Friend Sub New(source As SubmissionPacketFile)
            Me.Source = ManuscriptCloneService.CloneSubmissionPacketFile(source)
            Role = Me.Source.Role
            RoleName = SubmissionPacketService.FriendlyRoleName(Role)
            Label = If(Me.Source.Label, String.Empty).Trim()
            RequestedName = PacketExportService.DefaultRequestedName(Me.Source)
            DefaultExtension = PacketExportService.SplitName(
                PacketExportService.CleanFileName(RequestedName, RoleName, String.Empty)
            ).Extension
            RecordedSha256 = If(Me.Source.Sha256, String.Empty).Trim().ToLowerInvariant()
            _sizeBytes = Me.Source.FileSizeBytes

            If Me.Source.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly Then
                _fingerprint = PacketExportFingerprint.NoFile
            ElseIf String.IsNullOrWhiteSpace(Me.Source.LocalFilePath) Then
                _fingerprint = PacketExportFingerprint.Missing
            Else
                _fingerprint = PacketExportFingerprint.Pending
            End If
        End Sub

        Friend ReadOnly Property Source As SubmissionPacketFile

        Public ReadOnly Property Role As SubmissionPacketFileRole

        Public ReadOnly Property RoleName As String

        ' Editable: it becomes part of the file's description in the package.
        Public Property Label As String

        ' Editable: cleaned into OutputName by PacketExportPlan.RefreshNames.
        Public Property RequestedName As String

        ' The extension a name without one gets back.
        Friend ReadOnly Property DefaultExtension As String

        ' The cleaned requested name, before duplicates are numbered.
        Friend Property CleanName As String = String.Empty

        Public Property OutputName As String
            Get
                Return _outputName
            End Get
            Friend Set(value As String)
                _outputName = If(value, String.Empty)
            End Set
        End Property

        ' Asking to include a file that can't be included is ignored.
        Public Property Include As Boolean
            Get
                Return _include AndAlso CanInclude
            End Get
            Set(value As Boolean)
                _include = value AndAlso CanInclude
            End Set
        End Property

        Public ReadOnly Property CanInclude As Boolean
            Get
                Return _fingerprint = PacketExportFingerprint.Unchanged OrElse
                    _fingerprint = PacketExportFingerprint.Changed OrElse
                    _fingerprint = PacketExportFingerprint.NotRecorded
            End Get
        End Property

        Public ReadOnly Property IncludedByDefault As Boolean
            Get
                Return PacketExportService.IsIncludedByDefault(Role)
            End Get
        End Property

        ' Why the file starts unchecked or can't be included; "" otherwise.
        Public Property Note As String
            Get
                Return _note
            End Get
            Friend Set(value As String)
                _note = If(value, String.Empty)
            End Set
        End Property

        Public Property SizeBytes As Long?
            Get
                Return _sizeBytes
            End Get
            Friend Set(value As Long?)
                _sizeBytes = value
            End Set
        End Property

        Public Property Fingerprint As PacketExportFingerprint
            Get
                Return _fingerprint
            End Get
            Friend Set(value As PacketExportFingerprint)
                _fingerprint = value
                If Not CanInclude Then _include = False
            End Set
        End Property

        ' The stored fingerprint, lowercase; "" when none was recorded.
        Public ReadOnly Property RecordedSha256 As String

        ' The SHA-256 of the bytes the last check read (and inspected), for
        ' every file that can be included; "" otherwise. Never recorded.
        Public Property ObservedSha256 As String
            Get
                Return _observedSha256
            End Get
            Friend Set(value As String)
                _observedSha256 = If(value, String.Empty)
            End Set
        End Property

        ' Nothing until the files are checked.
        Public Property Hidden As HiddenMetadataReport
            Get
                Return _hidden
            End Get
            Friend Set(value As HiddenMetadataReport)
                _hidden = value
            End Set
        End Property


        ' The role, and the label when it adds something.
        Public Function Description() As String
            Dim cleanLabel As String = PacketExportService.SingleLine(Label)
            If cleanLabel.Length = 0 OrElse String.Equals(cleanLabel, RoleName, StringComparison.OrdinalIgnoreCase) Then
                Return RoleName
            End If
            Return RoleName & ": " & cleanLabel
        End Function


        ' Include as the defaults say: an allow-listed role, unchanged since
        ' its fingerprint, and in an anonymized packet not a file that names
        ' the authors (the title page, or the manuscript with author details).
        Friend Sub ApplyDefault(isBlinded As Boolean)
            Include = CanInclude AndAlso
                IncludedByDefault AndAlso
                _fingerprint <> PacketExportFingerprint.Changed AndAlso
                Not (isBlinded AndAlso NamesAuthorsByRole)
        End Sub


        ' A title page or the manuscript with author details: both name the
        ' authors, so an anonymized packet starts without them.
        Friend ReadOnly Property NamesAuthorsByRole As Boolean
            Get
                Return Role = SubmissionPacketFileRole.TitlePage OrElse Role = SubmissionPacketFileRole.Manuscript
            End Get
        End Property

    End Class


    ' Everything the export dialog shows and the package is written from,
    ' resolved once from the records. It keeps no reference to them.
    Public NotInheritable Class PacketExportPlan

        Private ReadOnly _allRows As List(Of PacketExportRow)
        Private _includeAuthors As Boolean

        Friend Sub New(manuscript As Manuscript, packet As SubmissionPacket, library As AuthorLibraryData)

            PackageName = PacketExportService.SingleLine(packet.Label)
            If PackageName.Length = 0 Then PackageName = PacketExportService.DefaultPackageName
            Title = PacketExportService.SingleLine(ReminderService.SafeManuscriptTitle(manuscript))

            ' The exact version this packet was made from.
            VersionLinked = packet.ManuscriptVersionId <> Guid.Empty
            Dim version As ManuscriptVersion = Nothing
            If VersionLinked Then
                version = If(manuscript.Versions, New List(Of ManuscriptVersion)()).
                    FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = packet.ManuscriptVersionId)
            End If
            VersionFound = version IsNot Nothing
            VersionLabel = If(VersionFound, PacketExportService.SingleLine(version.Label), String.Empty)

            ' The real submission it went with, if any; never guessed.
            Dim submission As JournalSubmission = Nothing
            If packet.SubmissionId.HasValue Then
                submission = If(manuscript.Submissions, New List(Of JournalSubmission)()).
                    FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = packet.SubmissionId.Value)
            End If
            HasSubmission = submission IsNot Nothing
            SubmissionLinkMissing = packet.SubmissionId.HasValue AndAlso Not HasSubmission
            SubmittedDate = If(HasSubmission, CType(submission.SubmittedDate, DateTime?), Nothing)

            ' A revision round comes from the packet, or else from its version
            ' when that version is a revision within the same submission (it
            ' records a round or answers a decision). A revision is never
            ' given the first submission's date.
            Dim round As Integer? = PositiveRound(packet.RevisionRoundNumber)
            Dim versionIsRevision As Boolean = False
            If HasSubmission AndAlso version IsNot Nothing AndAlso BelongsToSubmission(version, submission) Then
                Dim versionRound As Integer? = PositiveRound(version.RevisionRoundNumber)
                If Not round.HasValue Then round = versionRound
                versionIsRevision = versionRound.HasValue OrElse version.DecisionId.HasValue
            End If
            RevisionRound = If(HasSubmission, round, Nothing)
            IsRevision = HasSubmission AndAlso (RevisionRound.HasValue OrElse versionIsRevision)

            JournalName = PacketExportService.SingleLine(packet.JournalName)
            If JournalName.Length = 0 AndAlso HasSubmission Then JournalName = PacketExportService.SingleLine(submission.JournalName)

            Dim journal As JournalRecord = Nothing
            If packet.JournalId.HasValue AndAlso library IsNot Nothing Then
                journal = If(library.Journals, New List(Of JournalRecord)()).
                    FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = packet.JournalId.Value)
            End If
            JournalIssns = If(journal Is Nothing, New List(Of String)(), IssnService.NormalizeList(journal.Issns)).AsReadOnly()

            Authors = ResolveAuthors(manuscript, library).AsReadOnly()

            Dim metadata As ManuscriptMetadata = If(manuscript.Metadata, New ManuscriptMetadata())
            AbstractText = If(metadata.AbstractText, String.Empty).Trim()
            Keywords = If(metadata.Keywords, New List(Of String)()).
                Select(Function(item) PacketExportService.SingleLine(item)).
                Where(Function(item) item.Length > 0).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList().
                AsReadOnly()
            PublishedDoi = If(DoiNormalizer.IsValid(metadata.Doi), DoiNormalizer.Normalize(metadata.Doi), String.Empty)
            PublicationJournal = PacketExportService.SingleLine(metadata.PublicationJournal)
            IsPublished = manuscript.Location = ManuscriptLocation.Published OrElse manuscript.CurrentStage = PaperStage.Published

            ' Vault order: role, then label.
            _allRows = If(packet.Files, New List(Of SubmissionPacketFile)()).
                Where(Function(item) item IsNot Nothing).
                OrderBy(Function(item) CInt(item.Role)).
                ThenBy(Function(item) item.Label, StringComparer.CurrentCultureIgnoreCase).
                Select(Function(item) New PacketExportRow(item)).
                ToList()
            Rows = _allRows.AsReadOnly()

            IsBlinded = _allRows.Any(Function(item) item.Role = SubmissionPacketFileRole.BlindedManuscript)
            _includeAuthors = Not IsBlinded
            IncludeAbstract = True

            RefreshNames()

            For Each row As PacketExportRow In _allRows
                If row.Fingerprint = PacketExportFingerprint.NoFile OrElse row.Fingerprint = PacketExportFingerprint.Missing Then
                    row.Note = PacketExportService.NoteFor(row, IsBlinded)
                End If
            Next

        End Sub


        Private Shared Function PositiveRound(value As Integer?) As Integer?
            Return If(value.HasValue AndAlso value.Value > 0, value, Nothing)
        End Function


        ' The version was made within this submission: it links the
        ' submission, or answers one of its decisions.
        Private Shared Function BelongsToSubmission(version As ManuscriptVersion, submission As JournalSubmission) As Boolean
            If version.SubmissionId.HasValue AndAlso version.SubmissionId.Value = submission.Id Then Return True
            If Not version.DecisionId.HasValue Then Return False
            Return If(submission.Decisions, New List(Of EditorialDecisionEvent)()).
                Any(Function(item) item IsNot Nothing AndAlso item.Id = version.DecisionId.Value)
        End Function


        Private Shared Function ResolveAuthors(manuscript As Manuscript, library As AuthorLibraryData) As List(Of PacketExportAuthor)

            Dim result As New List(Of PacketExportAuthor)()
            If library Is Nothing Then Return result

            For Each link As ManuscriptAuthor In If(manuscript.Authors, New List(Of ManuscriptAuthor)())
                If link Is Nothing Then Continue For
                Dim person As AuthorRecord = If(library.Authors, New List(Of AuthorRecord)()).
                    FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = link.AuthorId)
                If person Is Nothing Then Continue For

                Dim name As String = PacketExportService.SingleLine(person.DisplayName)
                If name.Length = 0 OrElse name = "(Unnamed author)" Then Continue For

                Dim orcid As String = If(OrcidIdentifierService.IsValid(person.Orcid), OrcidIdentifierService.Normalize(person.Orcid), String.Empty)
                Dim familyName As String = PacketExportService.SingleLine(person.FamilyName)
                If familyName.Length = 0 Then familyName = name.Split(" "c).Last()

                Dim affiliations As New List(Of String)()
                For Each affiliationId As Guid In If(link.AffiliationIds, New List(Of Guid)())
                    Dim place As AffiliationRecord = If(library.Affiliations, New List(Of AffiliationRecord)()).
                        FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = affiliationId)
                    If place Is Nothing Then Continue For
                    Dim parts As String() = {PacketExportService.SingleLine(place.Department), PacketExportService.SingleLine(place.Institution)}
                    Dim affiliationName As String = String.Join(", ", parts.Where(Function(item) item.Length > 0))
                    If affiliationName.Length > 0 AndAlso Not affiliations.Contains(affiliationName, StringComparer.Ordinal) Then
                        affiliations.Add(affiliationName)
                    End If
                Next

                result.Add(New PacketExportAuthor(name, orcid, familyName, affiliations))
            Next

            Return result

        End Function


        ' Editable: names the package, its summary page, and the default .zip name.
        Public Property PackageName As String

        Public ReadOnly Property Title As String

        ' "" when the version doesn't resolve or has no label.
        Public ReadOnly Property VersionLabel As String

        Public ReadOnly Property VersionFound As Boolean

        ' The packet names a version (found or not).
        Friend ReadOnly Property VersionLinked As Boolean

        Public ReadOnly Property JournalName As String

        Public ReadOnly Property JournalIssns As IReadOnlyList(Of String)

        Public ReadOnly Property HasSubmission As Boolean

        Public ReadOnly Property SubmissionLinkMissing As Boolean

        ' The submission's date, only when the packet links a real submission.
        Public ReadOnly Property SubmittedDate As DateTime?

        ' From the packet, or from its version when that is a revision in the
        ' same submission; Nothing when no round is recorded.
        Public ReadOnly Property RevisionRound As Integer?

        ' A revision, with or without a recorded round: the date it was sent
        ' isn't recorded, so the submission's date is never given for it.
        Public ReadOnly Property IsRevision As Boolean

        ' The packet has an anonymized (blinded) manuscript.
        Public ReadOnly Property IsBlinded As Boolean

        ' Always off for an anonymized packet.
        Public Property IncludeAuthors As Boolean
            Get
                Return _includeAuthors AndAlso Not IsBlinded
            End Get
            Set(value As Boolean)
                _includeAuthors = value AndAlso Not IsBlinded
            End Set
        End Property

        Public Property IncludeAbstract As Boolean

        Public ReadOnly Property Authors As IReadOnlyList(Of PacketExportAuthor)

        Public ReadOnly Property AbstractText As String

        Public ReadOnly Property Keywords As IReadOnlyList(Of String)

        ' A valid DOI of the published work, or "".
        Public ReadOnly Property PublishedDoi As String

        Public ReadOnly Property PublicationJournal As String

        Public ReadOnly Property IsPublished As Boolean

        Public ReadOnly Property Rows As IReadOnlyList(Of PacketExportRow)

        ' Set once the files have been checked.
        Public Property FilesChecked As Boolean
            Get
                Return _filesChecked
            End Get
            Friend Set(value As Boolean)
                _filesChecked = value
            End Set
        End Property
        Private _filesChecked As Boolean


        Public Function IncludedRows() As IReadOnlyList(Of PacketExportRow)
            Return _allRows.Where(Function(item) item.Include).ToList().AsReadOnly()
        End Function


        ' The package's name as written: never empty.
        Friend Function EffectivePackageName() As String
            Dim name As String = PacketExportService.SingleLine(PackageName)
            Return If(name.Length > 0, name, PacketExportService.DefaultPackageName)
        End Function


        Public Function DefaultFileName() As String
            Return PacketExportService.CleanFileName(PackageName, PacketExportService.DefaultPackageName, String.Empty, lastSegmentOnly:=False) & ".zip"
        End Function


        ' Cleans every requested name, then numbers duplicates among the
        ' included files in row order. Files left out reserve no name.
        Public Sub RefreshNames()

            For Each row As PacketExportRow In _allRows
                row.CleanName = PacketExportService.CleanRequestedName(row)
            Next

            Dim included As List(Of PacketExportRow) = _allRows.Where(Function(item) item.Include).ToList()
            Dim unique As List(Of String) = PacketExportService.UniqueNames(included.Select(Function(item) item.CleanName))
            For index As Integer = 0 To included.Count - 1
                included(index).OutputName = unique(index)
            Next

            For Each row As PacketExportRow In _allRows.Where(Function(item) Not item.Include)
                row.OutputName = row.CleanName
            Next

        End Sub


        ' Hidden information was found in a file that will be exported.
        Public Function NeedsAcknowledgment() As Boolean
            Return IncludedRows().Any(Function(item) item.Hidden IsNot Nothing AndAlso item.Hidden.HasFindings)
        End Function


        ' The anonymized manuscript's hidden information names a person.
        Public Function BlindedManuscriptNamesPerson() As Boolean
            If Not IsBlinded Then Return False
            Return IncludedRows().Any(
                Function(item) item.Role = SubmissionPacketFileRole.BlindedManuscript AndAlso
                    item.Hidden IsNot Nothing AndAlso
                    item.Hidden.HasFindings AndAlso
                    item.Hidden.Findings.Any(Function(finding) finding.NamesPerson))
        End Function


        ' In an anonymized packet: every included file whose name, label, or
        ' hidden information includes an author's family name; the package
        ' name, version label, and journal name when they do; and a note when
        ' there are no family names to look for.
        Public Function AuthorNameWarnings() As IReadOnlyList(Of String)

            Dim warnings As New List(Of String)()
            If Not IsBlinded Then Return warnings.AsReadOnly()

            Dim familyNames As List(Of String) = Authors.
                Select(Function(item) item.FamilyName).
                Where(Function(item) item IsNot Nothing AndAlso item.Length >= 2).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
            If familyNames.Count = 0 Then
                warnings.Add(NoAuthorsToCheckText)
                Return warnings.AsReadOnly()
            End If

            Dim nameIn As Func(Of String, String) =
                Function(value) familyNames.FirstOrDefault(Function(familyName) PacketExportService.ContainsName(value, familyName))

            For Each row As PacketExportRow In IncludedRows()
                Dim texts As New List(Of String) From {row.OutputName, row.Label}
                If row.Hidden IsNot Nothing Then texts.AddRange(row.Hidden.Findings.Select(Function(item) item.Value))
                Dim matched As String = familyNames.FirstOrDefault(Function(familyName) texts.Any(Function(value) PacketExportService.ContainsName(value, familyName)))
                If matched IsNot Nothing Then
                    warnings.Add("Check " & row.OutputName & ": its name, label, or hidden information includes " & PacketExportService.Quoted(matched) & ".")
                End If
            Next

            Dim inPackageName As String = nameIn(PackageName)
            If inPackageName IsNot Nothing Then
                warnings.Add("The package name includes " & PacketExportService.Quoted(inPackageName) & ". Change it before exporting.")
            End If

            ' Both are written into the package but can't be edited here.
            Dim inVersion As String = nameIn(VersionLabel)
            If inVersion IsNot Nothing Then
                warnings.Add("The version label includes " & PacketExportService.Quoted(inVersion) & ". Change it in Version History before exporting.")
            End If
            Dim inJournal As String = nameIn(JournalName)
            If inJournal IsNot Nothing Then
                warnings.Add("The journal name includes " & PacketExportService.Quoted(inJournal) & ". Check the packet's journal before exporting.")
            End If

            Return warnings.AsReadOnly()

        End Function


        Public Const NoAuthorsToCheckText As String =
            "No structured authors with family names are recorded, so PaperRoute couldn't check the package for author names."


        ' An included file whose hidden information couldn't be looked at.
        Public Function HasUncheckedTypes() As Boolean
            Return IncludedRows().Any(
                Function(item) item.Hidden IsNot Nothing AndAlso
                    (item.Hidden.State = HiddenMetadataState.NotChecked OrElse item.Hidden.State = HiddenMetadataState.CouldNotCheck))
        End Function

    End Class


    Public NotInheritable Class PacketExportResult

        Friend Sub New(fileCount As Integer, zipFileName As String)
            Me.FileCount = fileCount
            Me.ZipFileName = zipFileName
        End Sub

        Public ReadOnly Property FileCount As Integer

        ' The file name only, never the folder.
        Public ReadOnly Property ZipFileName As String

    End Class


    ' A file as written into the package: what the metadata describes.
    Friend NotInheritable Class PacketExportWrittenFile

        Friend Sub New(row As PacketExportRow, number As Integer, sizeBytes As Long, sha256 As String, status As PacketExportFingerprint)
            Me.Row = row
            Me.Number = number
            OutputName = row.OutputName
            CrateId = PacketExportService.CrateId(row.OutputName)
            Me.SizeBytes = sizeBytes
            Me.Sha256 = sha256
            Me.Status = status
        End Sub

        Friend ReadOnly Property Row As PacketExportRow

        ' 1-based, in package order.
        Friend ReadOnly Property Number As Integer

        Friend ReadOnly Property OutputName As String

        Friend ReadOnly Property CrateId As String

        ' The bytes written.
        Friend ReadOnly Property SizeBytes As Long

        ' Of the bytes written, lowercase hex.
        Friend ReadOnly Property Sha256 As String

        Friend ReadOnly Property Status As PacketExportFingerprint

    End Class


    ' Exports a submission packet (#45) as one local .zip that reads two
    ' ways: a plain package (files/, a SHA-256 manifest, and a summary page)
    ' and an RO-Crate 1.3 crate. It never goes online, never changes a
    ' record, and copies files byte for byte, hidden information included.
    Public NotInheritable Class PacketExportService

        Public Const RoCrateSpec As String = "https://w3id.org/ro/crate/1.3"
        Public Const RoCrateContext As String = "https://w3id.org/ro/crate/1.3/context"
        Public Const SoftwareUrl As String = "https://github.com/JUhalt/PaperRoute-Tracker"
        Public Const SoftwareName As String = "PaperRoute Tracker"
        Public Const RightsName As String = "Rights not stated"
        Public Const RightsLine As String = "Rights not stated: PaperRoute records no license for these files, and this package grants none."
        Public Const RightsDescription As String = "PaperRoute records no license for these files, and this package grants none. The authors' and publishers' usual rights apply."
        Public Const NameStemLimit As Integer = 100

        Friend Const DefaultPackageName As String = "Submission packet"
        Friend Const ManifestName As String = "manifest-sha256.txt"
        Friend Const PreviewName As String = "ro-crate-preview.html"
        Friend Const MetadataName As String = "ro-crate-metadata.json"
        Friend Const FilesFolder As String = "files/"

        Private Const CopyBufferSize As Integer = 81920

        ' Roles exported by default. Any other role, including one added
        ' later, starts unchecked.
        Private Shared ReadOnly DefaultRoles As New HashSet(Of SubmissionPacketFileRole) From {
            SubmissionPacketFileRole.Manuscript,
            SubmissionPacketFileRole.BlindedManuscript,
            SubmissionPacketFileRole.TitlePage,
            SubmissionPacketFileRole.Figure,
            SubmissionPacketFileRole.Table,
            SubmissionPacketFileRole.Supplement,
            SubmissionPacketFileRole.Highlights,
            SubmissionPacketFileRole.GraphicalAbstract,
            SubmissionPacketFileRole.ReportingChecklist,
            SubmissionPacketFileRole.DataAvailability
        }

        Private Shared ReadOnly ReservedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
        }

        Private Shared ReadOnly InvalidNameCharacters As HashSet(Of Char) = BuildInvalidNameCharacters()

        Private Shared ReadOnly MediaTypes As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {".pdf", "application/pdf"},
            {".doc", "application/msword"},
            {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
            {".docm", "application/vnd.ms-word.document.macroEnabled.12"},
            {".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template"},
            {".xls", "application/vnd.ms-excel"},
            {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
            {".xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12"},
            {".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"},
            {".pptm", "application/vnd.ms-powerpoint.presentation.macroEnabled.12"},
            {".odt", "application/vnd.oasis.opendocument.text"},
            {".ods", "application/vnd.oasis.opendocument.spreadsheet"},
            {".odp", "application/vnd.oasis.opendocument.presentation"},
            {".rtf", "application/rtf"},
            {".tex", "application/x-tex"},
            {".txt", "text/plain"},
            {".md", "text/markdown"},
            {".csv", "text/csv"},
            {".tsv", "text/tab-separated-values"},
            {".html", "text/html"},
            {".htm", "text/html"},
            {".xml", "application/xml"},
            {".json", "application/json"},
            {".zip", "application/zip"},
            {".png", "image/png"},
            {".jpg", "image/jpeg"},
            {".jpeg", "image/jpeg"},
            {".gif", "image/gif"},
            {".tif", "image/tiff"},
            {".tiff", "image/tiff"},
            {".svg", "image/svg+xml"},
            {".eps", "application/postscript"},
            {".bmp", "image/bmp"},
            {".webp", "image/webp"},
            {".mp4", "video/mp4"},
            {".mov", "video/quicktime"},
            {".mp3", "audio/mpeg"},
            {".wav", "audio/wav"}
        }

        Private Sub New()
        End Sub


        ' ---- Defaults ------------------------------------------------------------

        Public Shared Function IsIncludedByDefault(role As SubmissionPacketFileRole) As Boolean
            Return DefaultRoles.Contains(role)
        End Function


        ' Shown in the preview only; never written into the package.
        Public Shared Function DefaultExclusionReason(role As SubmissionPacketFileRole) As String
            If IsIncludedByDefault(role) Then Return String.Empty
            Select Case role
                Case SubmissionPacketFileRole.CoverLetter
                    Return "can name the editor or suggested reviewers"
                Case SubmissionPacketFileRole.ResponseToReviewers
                    Return "names reviewers and quotes their comments"
                Case Else
                    Return "may contain anything, so check it first"
            End Select
        End Function


        Friend Shared Function NoteFor(row As PacketExportRow, isBlinded As Boolean) As String

            Select Case row.Fingerprint
                Case PacketExportFingerprint.NoFile
                    Return row.RoleName & " has no file. The package lists it by role only."
                Case PacketExportFingerprint.Missing
                    Return row.OutputName & " can't be included: the file wasn't found."
                Case PacketExportFingerprint.Unreadable
                    Return row.OutputName & " can't be included: the file couldn't be read. It may be open in another program."
                Case PacketExportFingerprint.Pending
                    Return String.Empty
            End Select

            If Not row.IncludedByDefault Then
                Return row.OutputName & " starts unchecked: it " & DefaultExclusionReason(row.Role) & "."
            End If
            If isBlinded AndAlso row.Role = SubmissionPacketFileRole.TitlePage Then
                Return row.OutputName & " starts unchecked: this packet is anonymized and a title page names the authors."
            End If
            If isBlinded AndAlso row.Role = SubmissionPacketFileRole.Manuscript Then
                Return row.OutputName & " starts unchecked: this packet is anonymized and, unlike the blinded manuscript, this one may name the authors."
            End If
            If row.Fingerprint = PacketExportFingerprint.Changed Then
                Return row.OutputName & " starts unchecked: it changed since its fingerprint was recorded."
            End If

            Return String.Empty

        End Function


        ' ---- Preparing and checking ---------------------------------------------

        ' Reads only its arguments and keeps copies: fast enough for the UI thread.
        Public Shared Function Prepare(manuscript As Manuscript, packet As SubmissionPacket, library As AuthorLibraryData) As PacketExportPlan
            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If packet Is Nothing Then Throw New ArgumentNullException(NameOf(packet))
            Return New PacketExportPlan(manuscript, packet, library)
        End Function


        ' Compares each file with its recorded fingerprint and looks for hidden
        ' information. Only reads files: run it off the UI thread. A second
        ' run keeps the user's choices for files whose state didn't change.
        ' Every file that can be included gets a SHA-256 of the bytes checked,
        ' recorded fingerprint or not, so the export can refuse a file that
        ' changed afterwards. Nothing is recorded.
        Public Shared Sub CheckFiles(plan As PacketExportPlan, Optional cancellationToken As CancellationToken = Nothing)

            If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
            Dim firstCheck As Boolean = Not plan.FilesChecked

            For Each row As PacketExportRow In plan.Rows
                cancellationToken.ThrowIfCancellationRequested()
                Dim before As PacketExportFingerprint = row.Fingerprint

                If row.Fingerprint <> PacketExportFingerprint.NoFile AndAlso Not String.IsNullOrWhiteSpace(row.Source.LocalFilePath) Then
                    CheckFile(row, cancellationToken)
                Else
                    row.ObservedSha256 = String.Empty
                    row.Hidden = Nothing
                End If

                If firstCheck OrElse before <> row.Fingerprint Then row.ApplyDefault(plan.IsBlinded)
            Next

            plan.RefreshNames()
            For Each row As PacketExportRow In plan.Rows
                row.Note = NoteFor(row, plan.IsBlinded)
            Next
            plan.FilesChecked = True

        End Sub


        ' The file is held open for reading, shared only with readers, so no
        ' program can change or replace it between its fingerprint and the
        ' look for hidden information: both describe the same bytes.
        Private Shared Sub CheckFile(row As PacketExportRow, cancellationToken As CancellationToken)

            Dim sourcePath As String = row.Source.LocalFilePath
            Dim held As FileStream = Nothing
            Try
                held = New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.SequentialScan)
            Catch ex As Exception When IsFileAccessException(ex)
                held = Nothing
            End Try

            Try
                Dim result As PacketFileIntegrityResult = SubmissionPacketIntegrityService.Verify(row.Source, cancellationToken)
                row.Fingerprint = MapStatus(result.Status)
                row.SizeBytes = If(result.FileSizeBytes, row.Source.FileSizeBytes)
                row.ObservedSha256 = String.Empty

                Select Case row.Fingerprint
                    Case PacketExportFingerprint.Unchanged, PacketExportFingerprint.Changed
                        row.ObservedSha256 = If(result.Sha256, String.Empty).ToLowerInvariant()
                    Case PacketExportFingerprint.NotRecorded
                        ' Only to notice a later change; never recorded.
                        Dim digest As String = If(held Is Nothing, Nothing, HashHeldFile(held, cancellationToken))
                        If digest Is Nothing Then
                            row.Fingerprint = PacketExportFingerprint.Unreadable
                        Else
                            row.ObservedSha256 = digest
                        End If
                End Select

                If row.CanInclude AndAlso row.ObservedSha256.Length = 0 Then row.Fingerprint = PacketExportFingerprint.Unreadable
                row.Hidden = If(row.CanInclude, HiddenMetadataService.Inspect(sourcePath, cancellationToken:=cancellationToken), Nothing)
            Finally
                held?.Dispose()
            End Try

        End Sub


        ' Lowercase hex, or Nothing when the file can't be read to the end.
        Private Shared Function HashHeldFile(held As FileStream, cancellationToken As CancellationToken) As String
            Try
                held.Seek(0, SeekOrigin.Begin)
                Dim expected As Long = held.Length
                Dim total As Long = 0
                Using hash As IncrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                    Dim buffer(CopyBufferSize - 1) As Byte
                    Do
                        cancellationToken.ThrowIfCancellationRequested()
                        Dim bytesRead As Integer = held.Read(buffer, 0, buffer.Length)
                        If bytesRead = 0 Then Exit Do
                        hash.AppendData(buffer, 0, bytesRead)
                        total += bytesRead
                    Loop
                    If total <> expected Then Return Nothing
                    Return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
                End Using
            Catch ex As Exception When IsFileAccessException(ex)
                Return Nothing
            End Try
        End Function


        Private Shared Function MapStatus(status As PacketFileIntegrityStatus) As PacketExportFingerprint
            Select Case status
                Case PacketFileIntegrityStatus.Unchanged : Return PacketExportFingerprint.Unchanged
                Case PacketFileIntegrityStatus.Changed : Return PacketExportFingerprint.Changed
                Case PacketFileIntegrityStatus.NotRecorded, PacketFileIntegrityStatus.NotChecked : Return PacketExportFingerprint.NotRecorded
                Case PacketFileIntegrityStatus.Missing : Return PacketExportFingerprint.Missing
                Case PacketFileIntegrityStatus.MetadataOnly : Return PacketExportFingerprint.NoFile
                Case Else : Return PacketExportFingerprint.Unreadable
            End Select
        End Function


        ' ---- Names ---------------------------------------------------------------

        ' The file's original name (its last part only); else its label with
        ' the stored file's extension; else its role name.
        Public Shared Function DefaultRequestedName(file As SubmissionPacketFile) As String

            If file Is Nothing Then Throw New ArgumentNullException(NameOf(file))

            Dim original As String = LastSegment(If(file.OriginalFileName, String.Empty)).Trim()
            If original.Length > 0 Then Return original

            Dim storedExtension As String = String.Empty
            Try
                storedExtension = If(System.IO.Path.GetExtension(If(file.LocalFilePath, String.Empty)), String.Empty)
            Catch ex As ArgumentException
                storedExtension = String.Empty
            End Try

            Dim stem As String = If(file.Label, String.Empty).Trim()
            If stem.Length = 0 Then stem = SubmissionPacketService.FriendlyRoleName(file.Role)
            Return stem & storedExtension

        End Function


        ' A safe file name on any system: the last path part only, invalid
        ' characters made "_", no trailing dots or spaces, never "." or "..",
        ' reserved device names prefixed with "_", and the stem at most 100
        ' characters.
        Public Shared Function CleanFileName(
            requested As String,
            fallbackStem As String,
            fallbackExtension As String,
            Optional lastSegmentOnly As Boolean = True
        ) As String

            Dim value As String = If(requested, String.Empty)
            If lastSegmentOnly Then value = LastSegment(value)
            value = TrimName(ReplaceInvalid(value))

            If IsEmptyName(value) Then
                Dim stem As String = TrimName(ReplaceInvalid(If(fallbackStem, String.Empty)))
                If IsEmptyName(stem) Then stem = "file"
                value = TrimName(stem & ReplaceInvalid(If(fallbackExtension, String.Empty)))
            End If

            Dim parts As (Stem As String, Extension As String) = SplitName(value)
            Dim cappedStem As String = CapStem(parts.Stem)
            If cappedStem.Length = 0 Then cappedStem = "file"

            ' Checked on the final name: capping can leave a bare device name,
            ' as "CON" followed by many spaces does.
            If IsReservedName(cappedStem & parts.Extension) Then cappedStem = CapStem("_" & cappedStem)

            Return cappedStem & parts.Extension

        End Function


        ' At most 100 characters, never splitting a surrogate pair, with no
        ' trailing dots or spaces.
        Private Shared Function CapStem(stem As String) As String
            Dim value As String = stem
            If value.Length > NameStemLimit Then
                Dim cut As Integer = NameStemLimit
                If Char.IsHighSurrogate(value(cut - 1)) Then cut -= 1
                value = value.Substring(0, cut)
            End If
            Return value.TrimEnd("."c, " "c)
        End Function


        ' CON, NUL, COM1, and the like, with or without an extension.
        Private Shared Function IsReservedName(name As String) As Boolean
            Dim firstDot As Integer = name.IndexOf("."c)
            Dim deviceName As String = If(firstDot >= 0, name.Substring(0, firstDot), name).TrimEnd(" "c)
            Return ReservedNames.Contains(deviceName)
        End Function


        Friend Shared Function CleanRequestedName(row As PacketExportRow) As String
            Dim cleaned As String = CleanFileName(row.RequestedName, row.RoleName, row.DefaultExtension)
            If row.DefaultExtension.Length > 0 AndAlso SplitName(cleaned).Extension.Length = 0 Then
                cleaned = CleanFileName(cleaned & row.DefaultExtension, row.RoleName, row.DefaultExtension)
            End If
            Return cleaned
        End Function


        ' Splits at the last "." when 1-10 letters or digits follow it and
        ' something comes before it.
        Friend Shared Function SplitName(name As String) As (Stem As String, Extension As String)
            Dim value As String = If(name, String.Empty)
            Dim dot As Integer = value.LastIndexOf("."c)
            If dot > 0 Then
                Dim tail As String = value.Substring(dot + 1)
                If tail.Length >= 1 AndAlso tail.Length <= 10 AndAlso tail.All(AddressOf IsAsciiLetterOrDigit) Then
                    Return (value.Substring(0, dot), value.Substring(dot))
                End If
            End If
            Return (value, String.Empty)
        End Function


        ' The first of equal names (ignoring case) is kept; later ones become
        ' "name (2).ext", "name (3).ext", and so on.
        Public Shared Function UniqueNames(names As IEnumerable(Of String)) As List(Of String)

            Dim used As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim result As New List(Of String)()

            For Each name As String In If(names, Enumerable.Empty(Of String)())
                Dim value As String = If(name, String.Empty)
                If used.Add(value) Then
                    result.Add(value)
                    Continue For
                End If

                Dim parts As (Stem As String, Extension As String) = SplitName(value)
                Dim number As Integer = 2
                Do
                    Dim suffix As String = " (" & number.ToString(CultureInfo.InvariantCulture) & ")"
                    Dim stem As String = parts.Stem
                    If stem.Length + suffix.Length > NameStemLimit Then
                        stem = stem.Substring(0, Math.Max(0, NameStemLimit - suffix.Length)).TrimEnd("."c, " "c)
                    End If
                    Dim candidate As String = stem & suffix & parts.Extension
                    If used.Add(candidate) Then
                        result.Add(candidate)
                        Exit Do
                    End If
                    number += 1
                Loop
            Next

            Return result

        End Function


        ' The entity id: "files/" and the name percent-encoded (RFC 3986:
        ' only unreserved characters are kept).
        Public Shared Function CrateId(outputName As String) As String
            Return FilesFolder & Uri.EscapeDataString(If(outputName, String.Empty))
        End Function


        Public Shared Function MediaType(outputName As String) As String
            Dim extension As String = SplitName(If(outputName, String.Empty)).Extension
            Dim found As String = Nothing
            If extension.Length > 0 AndAlso MediaTypes.TryGetValue(extension, found) Then Return found
            Return "application/octet-stream"
        End Function


        Public Shared Function FingerprintText(value As PacketExportFingerprint) As String
            Select Case value
                Case PacketExportFingerprint.Unchanged : Return "Unchanged"
                Case PacketExportFingerprint.Changed : Return "Changed since recorded"
                Case PacketExportFingerprint.NotRecorded : Return "Not recorded"
                Case PacketExportFingerprint.Missing : Return "Missing"
                Case PacketExportFingerprint.Unreadable : Return "Can't be read"
                Case PacketExportFingerprint.NoFile : Return "No file"
                Case Else : Return "Checking..."
            End Select
        End Function


        Public Shared Function SizeText(bytes As Long?) As String
            If Not bytes.HasValue Then Return "—"
            Dim value As Long = bytes.Value
            If value < 1024 Then Return value.ToString(CultureInfo.CurrentCulture) & If(value = 1, " byte", " bytes")
            If value < 1024L * 1024 Then Return (value / 1024.0).ToString("0.0", CultureInfo.CurrentCulture) & " KB"
            If value < 1024L * 1024 * 1024 Then Return (value / (1024.0 * 1024)).ToString("0.0", CultureInfo.CurrentCulture) & " MB"
            Return (value / (1024.0 * 1024 * 1024)).ToString("0.0", CultureInfo.CurrentCulture) & " GB"
        End Function


        ' PaperRoute's version, without build metadata.
        Public Shared Function AppVersion() As String
            Dim owner As Assembly = GetType(PacketExportService).Assembly
            Dim version As String = owner.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()?.InformationalVersion
            If String.IsNullOrWhiteSpace(version) Then version = owner.GetName().Version.ToString(3)
            Dim metadata As Integer = version.IndexOf("+"c)
            If metadata >= 0 Then version = version.Substring(0, metadata)
            Return version.Trim()
        End Function


        ' ---- Writing -------------------------------------------------------------

        ' Writes the .zip to a temporary file beside the destination, then
        ' moves it into place. On any failure nothing is left behind and an
        ' existing file at the destination is untouched.
        Public Shared Function WriteZip(
            plan As PacketExportPlan,
            destinationPath As String,
            Optional exportTimeUtc As DateTime? = Nothing,
            Optional appVersionText As String = Nothing,
            Optional cancellationToken As CancellationToken = Nothing
        ) As PacketExportResult

            If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))

            plan.RefreshNames()
            Dim included As IReadOnlyList(Of PacketExportRow) = plan.IncludedRows()
            If included.Count = 0 Then
                Throw New PacketExportException(PacketExportFailure.NothingIncluded, "Choose at least one file to export.")
            End If

            Dim exportUtc As DateTime = ToUtc(If(exportTimeUtc, DateTime.UtcNow))
            Dim version As String = If(String.IsNullOrWhiteSpace(appVersionText), AppVersion(), appVersionText.Trim())
            Dim entryTime As DateTimeOffset = New DateTimeOffset(exportUtc, TimeSpan.Zero).ToLocalTime()

            Dim zipPath As String = String.Empty
            Dim temporaryPath As String = String.Empty
            Try
                If String.IsNullOrWhiteSpace(destinationPath) Then Throw New ArgumentException("No destination.", NameOf(destinationPath))
                zipPath = System.IO.Path.GetFullPath(destinationPath)
                Dim folder As String = System.IO.Path.GetDirectoryName(zipPath)
                If String.IsNullOrEmpty(folder) Then Throw New ArgumentException("No folder.", NameOf(destinationPath))
                temporaryPath = System.IO.Path.Combine(
                    folder,
                    System.IO.Path.GetFileName(zipPath) & "." & Guid.NewGuid().ToString("N").Substring(0, 8) & ".partial")
            Catch ex As Exception When IsFileAccessException(ex)
                Throw DestinationFailure(ex)
            End Try

            ' Never write over one of this packet's own files, however the
            ' destination spells its path.
            If IsPacketFile(plan, zipPath) Then Throw OwnFileFailure()

            Dim written As New List(Of PacketExportWrittenFile)()

            Try
                Dim output As FileStream = Nothing
                Try
                    output = New FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                Catch ex As Exception When IsFileAccessException(ex)
                    Throw DestinationFailure(ex)
                End Try

                Using output
                    Using archive As New ZipArchive(output, ZipArchiveMode.Create, leaveOpen:=False)
                        For Each row As PacketExportRow In included
                            written.Add(CopyIntoArchive(archive, row, written.Count + 1, entryTime, cancellationToken))
                        Next

                        cancellationToken.ThrowIfCancellationRequested()
                        Dim withoutBom As New UTF8Encoding(False)
                        WriteEntry(archive, ManifestName, withoutBom.GetBytes(PacketExportPackage.Manifest(written)), entryTime)
                        WriteEntry(archive, PreviewName, withoutBom.GetBytes(PacketExportPackage.PreviewHtml(plan, written, exportUtc, version)), entryTime)
                        WriteEntry(archive, MetadataName, PacketExportPackage.MetadataJson(plan, written, exportUtc, version), entryTime)
                    End Using
                End Using

                cancellationToken.ThrowIfCancellationRequested()
                MoveIntoPlace(plan, temporaryPath, zipPath)

            Catch ex As PacketExportException
                DeleteQuietly(temporaryPath)
                Throw
            Catch ex As OperationCanceledException
                DeleteQuietly(temporaryPath)
                Throw New PacketExportException(PacketExportFailure.Cancelled, "The export was stopped. Nothing was written.", ex)
            Catch ex As Exception When IsFileAccessException(ex)
                DeleteQuietly(temporaryPath)
                Throw DestinationFailure(ex)
            Catch
                DeleteQuietly(temporaryPath)
                Throw
            End Try

            Return New PacketExportResult(written.Count, System.IO.Path.GetFileName(zipPath))

        End Function


        ' Copies one file into its entry while hashing the bytes written, and
        ' stops when the file changes during the copy or since the preview.
        Private Shared Function CopyIntoArchive(
            archive As ZipArchive,
            row As PacketExportRow,
            number As Integer,
            entryTime As DateTimeOffset,
            cancellationToken As CancellationToken
        ) As PacketExportWrittenFile

            Dim name As String = row.OutputName
            Dim sourcePath As String = row.Source.LocalFilePath
            If String.IsNullOrWhiteSpace(sourcePath) Then
                Throw New PacketExportException(PacketExportFailure.SourceMissing, Quoted(name) & " is missing, so nothing was exported.")
            End If

            Dim source As FileStream = Nothing
            Dim originalLength As Long
            Dim originalWriteTime As DateTime
            Try
                source = New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.SequentialScan)
            Catch ex As Exception When IsFileAccessException(ex)
                Throw SourceFailure(ex, name)
            End Try

            Using source
                Try
                    Dim info As New FileInfo(sourcePath)
                    info.Refresh()
                    originalLength = source.Length
                    originalWriteTime = info.LastWriteTimeUtc
                Catch ex As Exception When IsFileAccessException(ex)
                    Throw SourceFailure(ex, name)
                End Try

                Dim entry As ZipArchiveEntry = archive.CreateEntry(FilesFolder & name, CompressionLevel.Optimal)
                entry.LastWriteTime = entryTime

                Dim total As Long = 0
                Dim digest As String
                Using target As Stream = entry.Open()
                    Using hash As IncrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                        Dim buffer(CopyBufferSize - 1) As Byte
                        Do
                            cancellationToken.ThrowIfCancellationRequested()
                            Dim bytesRead As Integer
                            Try
                                bytesRead = source.Read(buffer, 0, buffer.Length)
                            Catch ex As Exception When IsFileAccessException(ex)
                                Throw SourceFailure(ex, name)
                            End Try
                            If bytesRead = 0 Then Exit Do
                            hash.AppendData(buffer, 0, bytesRead)
                            target.Write(buffer, 0, bytesRead)
                            total += bytesRead
                        Loop
                        digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
                    End Using
                End Using

                Dim changedWhileReading As Boolean
                Try
                    Dim info As New FileInfo(sourcePath)
                    info.Refresh()
                    changedWhileReading = source.Length <> originalLength OrElse
                        total <> originalLength OrElse
                        info.Length <> originalLength OrElse
                        info.LastWriteTimeUtc <> originalWriteTime
                Catch ex As Exception When IsFileAccessException(ex)
                    changedWhileReading = True
                End Try
                If changedWhileReading Then
                    Throw New PacketExportException(
                        PacketExportFailure.SourceChangedWhileReading,
                        Quoted(name) & " changed while it was being copied. Nothing was exported.")
                End If

                Dim status As PacketExportFingerprint
                If row.RecordedSha256.Length = 0 Then
                    status = PacketExportFingerprint.NotRecorded
                ElseIf String.Equals(row.RecordedSha256, digest, StringComparison.OrdinalIgnoreCase) Then
                    status = PacketExportFingerprint.Unchanged
                Else
                    status = PacketExportFingerprint.Changed
                End If

                ' Every file that can be included was fingerprinted by the
                ' check, so bytes that differ from what was checked (and
                ' shown) are refused, recorded fingerprint or not.
                If status <> row.Fingerprint OrElse
                   Not String.Equals(row.ObservedSha256, digest, StringComparison.OrdinalIgnoreCase) Then
                    Throw New PacketExportException(
                        PacketExportFailure.ChangedSincePreview,
                        Quoted(name) & " changed after the list was checked. Check the list again, then export.")
                End If

                Return New PacketExportWrittenFile(row, number, total, digest, status)
            End Using

        End Function


        ' Moves the finished .zip over the destination while the packet's
        ' files are held open, shared only with readers: Windows then refuses
        ' to replace any of them, even through a path spelled another way (a
        ' mapped drive, a junction, or the \\?\ form).
        Private Shared Sub MoveIntoPlace(plan As PacketExportPlan, temporaryPath As String, zipPath As String)

            Dim held As New List(Of FileStream)()
            Try
                For Each sourcePath As String In PacketFilePaths(plan)
                    Try
                        held.Add(New FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Catch ex As Exception When IsFileAccessException(ex)
                        ' Not there, or in use: the identity check below still applies.
                    End Try
                Next

                Try
                    File.Move(temporaryPath, zipPath, overwrite:=True)
                Catch ex As Exception When IsFileAccessException(ex)
                    If IsPacketFile(plan, zipPath) Then Throw OwnFileFailure(ex)
                    Throw
                End Try
            Finally
                For Each stream As FileStream In held
                    stream.Dispose()
                Next
            End Try

        End Sub


        Private Shared Function PacketFilePaths(plan As PacketExportPlan) As List(Of String)
            Return plan.Rows.
                Select(Function(item) item.Source.LocalFilePath).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
        End Function


        ' The destination is one of the packet's files: the same path, or the
        ' same file on the same volume reached another way.
        Private Shared Function IsPacketFile(plan As PacketExportPlan, zipPath As String) As Boolean
            Dim paths As List(Of String) = PacketFilePaths(plan)
            If paths.Any(Function(item) SamePath(item, zipPath)) Then Return True
            Dim destination As String = FileIdentity(zipPath)
            If destination Is Nothing Then Return False
            Return paths.Any(Function(item) String.Equals(FileIdentity(item), destination, StringComparison.Ordinal))
        End Function


        Private Shared Function OwnFileFailure(Optional inner As Exception = Nothing) As PacketExportException
            Return New PacketExportException(
                PacketExportFailure.DestinationUnwritable,
                "That file is part of this packet. Choose another name for the .zip.",
                inner)
        End Function


        ' The volume serial number and file index, which every path to one
        ' file shares; Nothing when the file isn't there or can't be opened.
        Private Shared Function FileIdentity(filePath As String) As String
            Try
                If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return Nothing
                Using handle As SafeFileHandle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    Dim information As ByHandleFileInformation
                    If Not GetFileInformationByHandle(handle, information) Then Return Nothing
                    Return information.VolumeSerialNumber.ToString("x8", CultureInfo.InvariantCulture) & ":" &
                        information.FileIndexHigh.ToString("x8", CultureInfo.InvariantCulture) &
                        information.FileIndexLow.ToString("x8", CultureInfo.InvariantCulture)
                End Using
            Catch ex As Exception When IsFileAccessException(ex)
                Return Nothing
            End Try
        End Function


        <StructLayout(LayoutKind.Sequential)>
        Private Structure ByHandleFileInformation
            Public FileAttributes As UInteger
            Public CreationTimeLow As UInteger
            Public CreationTimeHigh As UInteger
            Public LastAccessTimeLow As UInteger
            Public LastAccessTimeHigh As UInteger
            Public LastWriteTimeLow As UInteger
            Public LastWriteTimeHigh As UInteger
            Public VolumeSerialNumber As UInteger
            Public FileSizeHigh As UInteger
            Public FileSizeLow As UInteger
            Public NumberOfLinks As UInteger
            Public FileIndexHigh As UInteger
            Public FileIndexLow As UInteger
        End Structure


        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function GetFileInformationByHandle(handle As SafeFileHandle, <Out> ByRef information As ByHandleFileInformation) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function


        Private Shared Sub WriteEntry(archive As ZipArchive, entryName As String, content As Byte(), entryTime As DateTimeOffset)
            Dim entry As ZipArchiveEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal)
            entry.LastWriteTime = entryTime
            Using target As Stream = entry.Open()
                target.Write(content, 0, content.Length)
            End Using
        End Sub


        Private Shared Function SourceFailure(ex As Exception, name As String) As PacketExportException
            If TypeOf ex Is FileNotFoundException OrElse TypeOf ex Is DirectoryNotFoundException Then
                Return New PacketExportException(PacketExportFailure.SourceMissing, Quoted(name) & " is missing, so nothing was exported.", ex)
            End If
            Return New PacketExportException(
                PacketExportFailure.SourceUnreadable,
                Quoted(name) & " couldn't be read. It may be open in another program. Nothing was exported.",
                ex)
        End Function


        Private Shared Function DestinationFailure(ex As Exception) As PacketExportException
            Return New PacketExportException(
                PacketExportFailure.DestinationUnwritable,
                "PaperRoute couldn't write the .zip there. Choose another folder.",
                ex)
        End Function


        Private Shared Function IsFileAccessException(ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse
                TypeOf ex Is UnauthorizedAccessException OrElse
                TypeOf ex Is SecurityException OrElse
                TypeOf ex Is ArgumentException OrElse
                TypeOf ex Is NotSupportedException
        End Function


        Private Shared Sub DeleteQuietly(temporaryPath As String)
            If String.IsNullOrEmpty(temporaryPath) Then Return
            Try
                If File.Exists(temporaryPath) Then File.Delete(temporaryPath)
            Catch ex As Exception When IsFileAccessException(ex)
                ' Best effort: the name ends in .partial, so it can't be taken for an export.
            End Try
        End Sub


        Private Shared Function SamePath(first As String, second As String) As Boolean
            If String.IsNullOrWhiteSpace(first) OrElse String.IsNullOrWhiteSpace(second) Then Return False
            Try
                Return String.Equals(System.IO.Path.GetFullPath(first), System.IO.Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase)
            Catch ex As Exception When IsFileAccessException(ex)
                Return False
            End Try
        End Function


        Private Shared Function ToUtc(value As DateTime) As DateTime
            Select Case value.Kind
                Case DateTimeKind.Local : Return value.ToUniversalTime()
                Case DateTimeKind.Unspecified : Return DateTime.SpecifyKind(value, DateTimeKind.Utc)
                Case Else : Return value
            End Select
        End Function


        ' ---- Text helpers ----------------------------------------------------------

        ' A name in typographic quotes, as messages show it.
        Friend Shared Function Quoted(value As String) As String
            Return ChrW(&H201C) & value & ChrW(&H201D)
        End Function


        ' Trimmed, with every run of whitespace made one space.
        Friend Shared Function SingleLine(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return String.Empty
            Return System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\s+", " ")
        End Function


        ' The name as a word, ignoring case and accents ("Muller" and
        ' "Mueller" both match "Müller"). Digits, "_", and a change of case
        ' end a word, so "Carberry2026" and "CarberryEtAl" match, but
        ' "Carberryville" doesn't.
        Friend Shared Function ContainsName(value As String, name As String) As Boolean
            If String.IsNullOrEmpty(value) OrElse String.IsNullOrEmpty(name) Then Return False
            Dim names As List(Of String) = NameForms(name)
            For Each text As String In NameForms(value)
                For Each form As String In names
                    If form.Length = 0 Then Continue For
                    Dim start As Integer = 0
                    Do While start <= text.Length - form.Length
                        Dim index As Integer = text.IndexOf(form, start, StringComparison.OrdinalIgnoreCase)
                        If index < 0 Then Exit Do
                        If IsWordAt(text, index, form.Length) Then Return True
                        start = index + 1
                    Loop
                Next
            Next
            Return False
        End Function


        ' The text without accents, and also with German umlauts spelled
        ' out ("ü" as "ue").
        Private Shared Function NameForms(value As String) As List(Of String)
            Dim composed As String = value.Normalize(NormalizationForm.FormC)
            Dim spelledOut As String = composed.
                Replace(ChrW(&HE4), "ae").Replace(ChrW(&HF6), "oe").Replace(ChrW(&HFC), "ue").
                Replace(ChrW(&HC4), "Ae").Replace(ChrW(&HD6), "Oe").Replace(ChrW(&HDC), "Ue")
            Dim forms As New List(Of String) From {FoldAccents(composed)}
            Dim second As String = FoldAccents(spelledOut)
            If Not String.Equals(second, forms(0), StringComparison.Ordinal) Then forms.Add(second)
            Return forms
        End Function


        Private Shared Function FoldAccents(value As String) As String
            Dim folded As New StringBuilder(value.Length)
            For Each character As Char In value.Normalize(NormalizationForm.FormD)
                Select Case AscW(character)
                    Case &HDF : folded.Append("ss")     ' sharp s
                    Case &H1E9E : folded.Append("SS")
                    Case &HE6 : folded.Append("ae")     ' ae ligature
                    Case &HC6 : folded.Append("AE")
                    Case &H153 : folded.Append("oe")    ' oe ligature
                    Case &H152 : folded.Append("OE")
                    Case &HF8 : folded.Append("o")      ' o with stroke
                    Case &HD8 : folded.Append("O")
                    Case &H142 : folded.Append("l")     ' l with stroke
                    Case &H141 : folded.Append("L")
                    Case &H111, &HF0 : folded.Append("d")  ' d with stroke, eth
                    Case &H110, &HD0 : folded.Append("D")
                    Case &HFE : folded.Append("th")     ' thorn
                    Case &HDE : folded.Append("Th")
                    Case &H131 : folded.Append("i")     ' dotless i
                    Case Else
                        If CharUnicodeInfo.GetUnicodeCategory(character) <> UnicodeCategory.NonSpacingMark Then folded.Append(character)
                End Select
            Next
            Return folded.ToString().Normalize(NormalizationForm.FormC)
        End Function


        ' A letter just before or after the match ends it only where the
        ' case changes: "jCarberry", "JCarberry", "CarberryEtAl".
        Private Shared Function IsWordAt(text As String, index As Integer, length As Integer) As Boolean

            Dim first As Char = text(index)
            Dim last As Char = text(index + length - 1)

            Dim startsWord As Boolean = True
            If index > 0 AndAlso Char.IsLetter(text(index - 1)) Then
                Dim previous As Char = text(index - 1)
                startsWord = Char.IsUpper(first) AndAlso
                    (Char.IsLower(previous) OrElse
                     (Char.IsUpper(previous) AndAlso length > 1 AndAlso Char.IsLower(text(index + 1))))
            End If

            Dim endsWord As Boolean = True
            Dim after As Integer = index + length
            If after < text.Length AndAlso Char.IsLetter(text(after)) Then
                endsWord = Char.IsUpper(text(after)) AndAlso Char.IsLower(last)
            End If

            Return startsWord AndAlso endsWord

        End Function


        Private Shared Function LastSegment(value As String) As String
            Dim separator As Integer = Math.Max(value.LastIndexOf("\"c), value.LastIndexOf("/"c))
            Return If(separator >= 0, value.Substring(separator + 1), value)
        End Function


        Private Shared Function ReplaceInvalid(value As String) As String
            Dim cleaned As New StringBuilder(value.Length)
            For Each character As Char In value
                cleaned.Append(If(InvalidNameCharacters.Contains(character) OrElse AscW(character) < &H20 OrElse AscW(character) = &H7F, "_"c, character))
            Next
            Return cleaned.ToString()
        End Function


        Private Shared Function TrimName(value As String) As String
            Return value.TrimStart().TrimEnd("."c, " "c)
        End Function


        Private Shared Function IsEmptyName(value As String) As Boolean
            Return value.Length = 0 OrElse value.All(Function(character) character = "."c)
        End Function


        Private Shared Function IsAsciiLetterOrDigit(character As Char) As Boolean
            Return (character >= "a"c AndAlso character <= "z"c) OrElse
                (character >= "A"c AndAlso character <= "Z"c) OrElse
                (character >= "0"c AndAlso character <= "9"c)
        End Function


        Private Shared Function BuildInvalidNameCharacters() As HashSet(Of Char)
            Dim characters As New HashSet(Of Char)(System.IO.Path.GetInvalidFileNameChars())
            For Each character As Char In ":\/*?""<>|"
                characters.Add(character)
            Next
            Return characters
        End Function

    End Class

End Namespace
