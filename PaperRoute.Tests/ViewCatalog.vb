Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Every view PaperRoute shows, for the sweeps that run over all of them
' (#135): scaling (#93), Dark (#106), accessible names, Esc and the
' Windows 11 chrome. The dialogs are keyed by their Form subclass, so a
' form with neither a factory nor a place on the not-yet list fails the
' catalog test; the hosted presentations and the main window's pages, which
' no Form subclass of their own keys, are listed by name. Every factory is
' handed the isolated storage root it runs on and builds its view the way
' the app does, with any files it needs under that root.
Friend NotInheritable Class ViewCatalog

    Private Sub New()
    End Sub


    ' A view with no Form subclass of its own to key it by.
    Friend NotInheritable Class NamedView

        Public ReadOnly Name As String
        Public ReadOnly Build As Func(Of String, Form)

        Public Sub New(
            name As String,
            build As Func(Of String, Form)
        )

            Me.Name = name
            Me.Build = build

        End Sub

    End Class


    ' The dialogs, keyed by the Form subclass each one builds. The main
    ' window is its Board page.
    Public Shared ReadOnly Dialogs As New Dictionary(Of Type, Func(Of String, Form)) From {
        {GetType(Form1), Function(root) BoardOn(CreateRepresentativeLibrary())},
        {GetType(AddDecisionForm), Function(root) New AddDecisionForm()},
        {GetType(AddManuscriptForm), Function(root) New AddManuscriptForm()},
        {GetType(AddSubmissionForm), Function(root) New AddSubmissionForm()},
        {GetType(ApiKeyForm), Function(root) ApiKeyForm.ForAssistant(ProtectedKeyStore.Anthropic)},
        {GetType(OpenAlexKeyForm), Function(root) New OpenAlexKeyForm()},
        {GetType(AssistantConsentForm), Function(root) New AssistantConsentForm(ResponseRequest(), ClaudeConnection())},
        {GetType(AssistantDraftForm), Function(root) New AssistantDraftForm(ResponseRequest(), String.Empty)},
        {GetType(AuthorInstructionsForm), Function(root) New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, {}, {})},
        {GetType(AuthorLibraryForm), Function(root) New AuthorLibraryForm(New List(Of Manuscript)())},
        {GetType(CitationsUpdateForm), Function(root) New CitationsUpdateForm(Carberry, New List(Of (Doi As String, Title As String))(), Nothing, New NoCitations())},
        {GetType(CoverLetterForm), Function(root) New CoverLetterForm("Example: open materials", "Fictional Open Psychology", "Journal article", {"open science"}, "An abstract.")},
        {GetType(DecisionLetterForm), Function(root) New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission())},
        {GetType(DeleteManuscriptForm), Function(root) New DeleteManuscriptForm("Active Study")},
        {GetType(DiagnosticsForm), Function(root) New DiagnosticsForm(New AppSettings())},
        {GetType(EditManuscriptForm), Function(root) Editor(root)},
        {GetType(HelpForm), Function(root) New HelpForm()},
        {GetType(JournalCandidateForm), Function(root) New JournalCandidateForm(New JournalCandidate With {.JournalName = "PLOS ONE"}, {"PLOS ONE"}, Function(name) "No submissions yet.")},
        {GetType(JournalChecklistItemEditForm), Function(root) New JournalChecklistItemEditForm(New JournalChecklistTemplateItem With {.Title = "Data availability statement"})},
        {GetType(JournalEditForm), Function(root) New JournalEditForm(Journal())},
        {GetType(JournalFactsForm), Function(root) New JournalFactsForm(Journal(), New NoFacts())},
        {GetType(JournalLibraryForm), Function(root) New JournalLibraryForm(New List(Of Manuscript)())},
        {GetType(JournalMetricForm), Function(root) New JournalMetricForm()},
        {GetType(JournalSuggestionsForm), Function(root) New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, New NoSuggestions(), Nothing, Nothing, DateTime.Today)},
        {GetType(ManuscriptReadinessForm), Function(root) New ManuscriptReadinessForm(WithPackets(), New AuthorLibraryData())},
        {GetType(ManuscriptRouteViewForm), Function(root) New ManuscriptRouteViewForm(Routed())},
        {GetType(ManuscriptVersionEditForm), Function(root) VersionEditor()},
        {GetType(PacketExportForm), Function(root) PacketExport(root)},
        {GetType(PublicationCheckForm), Function(root) New PublicationCheckForm(CreateRepresentativeLibrary(), Nothing, String.Empty, New NoPublications(), Function() True, DateTime.Today)},
        {GetType(ReadinessItemNotesForm), Function(root) New ReadinessItemNotesForm(WithPackets().ReadinessProfiles(0).Items(0))},
        {GetType(ReportForm), Function(root) New ReportForm("Pipeline Report", "report.html", CreateRepresentativeLibrary(), Function(chosen, deadlines) ReportService.PipelineReport(chosen, DateTime.Today))},
        {GetType(ReviewerResponseExportForm), Function(root) New ReviewerResponseExportForm("# Responses to reviewers")},
        {GetType(ReviewerResponseItemForm), Function(root) New ReviewerResponseItemForm(Routed().Submissions(0))},
        {GetType(ReviewerResponseMatrixForm), Function(root) Matrix()},
        {GetType(SettingsForm), Function(root) New SettingsForm(New AppSettings())},
        {GetType(StartupFailureForm), Function(root) New StartupFailureForm(StartupFailure.ForStorage(New InvalidOperationException("The schema is newer than this build."), Path.Combine(root, "schema.json")))},
        {GetType(SubmissionDetailsForm), Function(root) Details()},
        {GetType(SubmissionPacketEditForm), Function(root) PacketEditor()},
        {GetType(SubmissionPacketVaultForm), Function(root) New SubmissionPacketVaultForm(WithPackets())},
        {GetType(TitlePageImportForm), Function(root) New TitlePageImportForm(New AuthorLibraryData(), "A preregistered replication of anchoring effects")},
        {GetType(UnexpectedErrorForm), Function(root) New UnexpectedErrorForm(New InvalidOperationException("The route map could not be drawn."), True, Path.Combine(root, "logs"))},
        {GetType(UpdatePromptForm), Function(root) New UpdatePromptForm("0.9.1", "0.9.2", "Stable", "Notes & fixes")}
    }


    ' The five hosted presentations and each main-window page. A hosted
    ' presentation's factory returns its host and fails when the view is
    ' not inside it.
    Public Shared ReadOnly Hosted As NamedView() = {
        New NamedView("The welcome, on an empty library", Function(root) Welcome()),
        New NamedView("Library page", Function(root) Page(Form1.WorkspacePage.Library)),
        New NamedView("AuthorLibraryForm as the Library page", Function(root) LibraryAuthors()),
        New NamedView("Journals page, JournalLibraryForm hosted", Function(root) Hosting(Of JournalLibraryForm)(Page(Form1.WorkspacePage.Journals))),
        New NamedView("Deadlines page", Function(root) Page(Form1.WorkspacePage.Deadlines)),
        New NamedView("Insights page", Function(root) Page(Form1.WorkspacePage.Insights)),
        New NamedView("Import & Export page", Function(root) Page(Form1.WorkspacePage.ImportExport)),
        New NamedView("EditManuscriptForm as the manuscript page", Function(root) Hosting(Of EditManuscriptForm)(ManuscriptPage())),
        New NamedView("SubmissionDetailsForm inside the manuscript page", Function(root) Hosting(Of SubmissionDetailsForm)(ManuscriptPage())),
        New NamedView("ReviewerResponseMatrixForm inside the submission's details", Function(root) Hosting(Of ReviewerResponseMatrixForm)(ManuscriptPage()))
    }


    ' Forms no test builds yet, each with what its factory will need. Each
    ' sweep shrinks this list; the catalog test fails when an entry is stale.
    Public Shared ReadOnly NotYet As (View As Type, Reason As String)() = {
        (GetType(AboutForm), "New AboutForm() needs nothing; no test builds it yet."),
        (GetType(AddCorrespondenceForm), "New AddCorrespondenceForm() needs nothing; no test builds it yet."),
        (GetType(AffiliationEditForm), "Needs an AffiliationRecord; no test builds it yet."),
        (GetType(AuthorEditForm), "Needs an AuthorRecord; no test builds it yet."),
        (GetType(BibliographyExportSelectionForm), "Needs manuscripts and a format name; no test builds it yet."),
        (GetType(BibliographyImportForm), "Needs a BibliographyParseResult; no test builds it yet."),
        (GetType(CrossrefLookupForm), "Needs a manuscript and an author library; no test builds it yet."),
        (GetType(DeadlineDateForm), "Needs a heading, context, note, verb and dates; no test builds it yet."),
        (GetType(ExcelMappingForm), "Needs an .xlsx file under the root; no test builds it yet."),
        (GetType(ExternalLinkEditForm), "Needs a ManuscriptExternalLink; no test builds it yet."),
        (GetType(FillBlanksForm), "Needs a library, a publication source and a save callback; no test builds it yet."),
        (GetType(JournalPickerForm), "Needs an AuthorLibraryData; no test builds it yet."),
        (GetType(ManuscriptAuthorForm), "Needs a library, a ManuscriptAuthor and excluded ids; no test builds it yet."),
        (GetType(ManuscriptLinksForm), "Needs a manuscript; opens the author library from a field initialiser; no test builds it yet."),
        (GetType(MarkPublishedForm), "Needs a manuscript, a PublicationMatch and a date; no test builds it yet."),
        (GetType(OrcidLookupForm), "Needs an AuthorRecord; no test builds it yet."),
        (GetType(PublicationExportForm), "Needs manuscripts and an author library; no test builds it yet."),
        (GetType(ReminderEditForm), "Needs manuscripts; no test builds it yet."),
        (GetType(SubmissionPacketFileEditForm), "Needs a packet and a file record; no test builds it yet."),
        (GetType(UpdateProgressForm), "Needs a version string; no test builds it yet.")
    }


    ' Every view in the app: the non-abstract Form subclasses of its assembly.
    Public Shared Function AppViews() As List(Of Type)

        Return GetType(Form1).Assembly.GetTypes().
            Where(Function(type) GetType(Form).IsAssignableFrom(type) AndAlso Not type.IsAbstract).
            OrderBy(Function(type) type.Name, StringComparer.Ordinal).
            ToList()

    End Function


    ' The views with neither a factory nor a place on the not-yet list.
    Public Shared Function Unlisted(
        views As IEnumerable(Of Type)
    ) As List(Of Type)

        Dim listed As New HashSet(Of Type)(
            Dialogs.Keys.Concat(NotYet.Select(Function(entry) entry.View))
        )

        Return views.Where(Function(view) Not listed.Contains(view)).ToList()

    End Function


    ' Not-yet entries that no longer belong there: the form now has a
    ' factory, or it is not one of the views.
    Public Shared Function StaleNotYet(
        views As IEnumerable(Of Type)
    ) As List(Of Type)

        Dim app As New HashSet(Of Type)(views)

        Return NotYet.Select(Function(entry) entry.View).
            Where(Function(view) Dialogs.ContainsKey(view) OrElse Not app.Contains(view)).
            ToList()

    End Function


    ' ---------------------------------------------------------------
    ' Samples
    ' ---------------------------------------------------------------

    Private Const Carberry As String = "0000-0002-1825-0097"


    ' The representative library's manuscript under review, with a
    ' submission and its revision decision.
    Private Shared Function Routed() As Manuscript
        Return CreateRepresentativeLibrary()(0)
    End Function


    ' A manuscript with a version, a readiness profile and a packet.
    Private Shared Function WithPackets() As Manuscript

        Dim manuscript As Manuscript = Routed()
        Dim version As New ManuscriptVersion With {.Label = "Exact submission version"}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim readiness As New ManuscriptReadiness With {.JournalName = manuscript.TargetJournal}
        readiness.Items.Add(New ReadinessItemState With {.Title = "Title page", .Description = "A title page with every author.", .UserNotes = "Check the affiliations.", .SortOrder = 0})
        manuscript.ReadinessProfiles.Add(readiness)

        Dim packet As SubmissionPacket = SubmissionPacketService.CreatePacket(manuscript, version.Id, "Round one packet", "Sent as a PDF.", readiness.Id, manuscript.Submissions(0).Id)
        packet.Files.Add(New SubmissionPacketFile With {.Label = "Main document", .Notes = "Metadata only."})
        Return manuscript

    End Function


    Private Shared Function Journal() As JournalRecord
        Return New JournalRecord With {.Name = "Fictional Journal", .SubmissionPortalUrl = "https://journal.example.invalid/submit"}
    End Function


    Private Shared Function ResponseRequest() As AssistantRequest
        Return AssistantService.BuildResponseRequest("Reviewer 1", "Clarify the sample.", "")
    End Function


    ' A Claude connection built from settings, not from the process's
    ' online state.
    Private Shared Function ClaudeConnection() As AssistantConnection
        Return OnlineAccess.ConnectionFor(New AssistantSettings With {.Enabled = True, .Provider = AssistantProvider.Claude})
    End Function


    ' The manuscript page's editor, as Form1 builds it, on an author
    ' library under the root.
    Private Shared Function Editor(root As String) As EditManuscriptForm
        Dim manuscript As Manuscript = Routed()
        Return New EditManuscriptForm(manuscript, {manuscript}, New AuthorLibraryRepository(Path.Combine(root, "authors")), pageMode:=True)
    End Function


    Private Shared Function VersionEditor() As ManuscriptVersionEditForm
        Dim manuscript As Manuscript = WithPackets()
        Return New ManuscriptVersionEditForm(manuscript, manuscript.Versions(0))
    End Function


    Private Shared Function PacketEditor() As SubmissionPacketEditForm
        Dim manuscript As Manuscript = WithPackets()
        Return New SubmissionPacketEditForm(manuscript, manuscript.SubmissionPackets(0))
    End Function


    ' The RO-Crate evaluation's packet, with its files under the root.
    Private Shared Function PacketExport(root As String) As PacketExportForm
        Dim fixture As ExportFixture = BuildExampleFixture(root)
        Return New PacketExportForm(fixture.Manuscript, fixture.Packet, fixture.Library)
    End Function


    Private Shared Function Details() As SubmissionDetailsForm
        Dim manuscript As Manuscript = Routed()
        Return New SubmissionDetailsForm(manuscript, manuscript.Submissions(0))
    End Function


    Private Shared Function Matrix() As ReviewerResponseMatrixForm
        Dim manuscript As Manuscript = Routed()
        Return New ReviewerResponseMatrixForm(manuscript, manuscript.Submissions(0))
    End Function


    ' ---------------------------------------------------------------
    ' The main window and its pages
    ' ---------------------------------------------------------------

    Private Shared Function BoardOn(library As List(Of Manuscript)) As CatalogBoard
        Dim board As New CatalogBoard()
        board.Prepare(library)
        Return board
    End Function


    Private Shared Function Welcome() As Form
        Dim board As CatalogBoard = BoardOn(New List(Of Manuscript)())
        If Not board.Welcome.Visible Then
            Throw New InvalidOperationException("An empty library does not show the welcome.")
        End If
        Return board
    End Function


    Private Shared Function Page(target As Form1.WorkspacePage) As CatalogBoard
        Dim board As CatalogBoard = BoardOn(CreateRepresentativeLibrary())
        board.NavigateTo(target)
        Application.DoEvents()
        Return board
    End Function


    Private Shared Function LibraryAuthors() As Form
        Dim board As CatalogBoard = Page(Form1.WorkspacePage.Library)
        board.ShowLibraryAuthors()
        Return Hosting(Of AuthorLibraryForm)(board)
    End Function


    Private Shared Function ManuscriptPage() As CatalogBoard
        Dim library As List(Of Manuscript) = CreateRepresentativeLibrary()
        Dim board As CatalogBoard = BoardOn(library)
        board.Open(library(0))
        Return board
    End Function


    ' The host, once the hosted view is found inside it.
    Private Shared Function Hosting(Of T As Form)(host As Form) As Form
        If Not Descendants(host).OfType(Of T)().Any() Then
            Throw New InvalidOperationException(host.GetType().Name & " does not host a " & GetType(T).Name & ".")
        End If
        Return host
    End Function


    Friend Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function


    ' The real main window on in-memory samples, as ManuscriptPageTests
    ' builds it: Load is skipped, so no library is read, and the window
    ' never activates. Its repositories resolve under the catalog's root.
    Private NotInheritable Class CatalogBoard
        Inherits Form1

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub Prepare(library As List(Of Manuscript))
            CallPrivate("BuildInterface")
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, library)
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public Sub Open(manuscript As Manuscript)
            CallPrivate("OpenManuscript", manuscript, Nothing)
            Application.DoEvents()
        End Sub

        Public Sub ShowLibraryAuthors()
            CallPrivate("ShowLibraryAuthors")
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Welcome As Control
            Get
                Return DirectCast(GetType(Form1).GetField("boardWelcome", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
            End Get
        End Property

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub
    End Class


    ' ---------------------------------------------------------------
    ' Sources that answer nothing, so no view goes online
    ' ---------------------------------------------------------------

    Private NotInheritable Class NoCitations
        Implements ICitationSource

        Public Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                    excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync
            Return Task.FromResult(New CitationLookup With {.Orcid = orcid})
        End Function
    End Class


    Private NotInheritable Class NoFacts
        Implements IJournalFactsSource

        Public Function LookupAsync(issns As IEnumerable(Of String), openAlexId As String, cancellationToken As CancellationToken) As Task(Of JournalFactsLookup) Implements IJournalFactsSource.LookupAsync
            Return Task.FromResult(New JournalFactsLookup())
        End Function

        Public Function SearchAsync(name As String, cancellationToken As CancellationToken) As Task(Of List(Of OpenAlexSourceMatch)) Implements IJournalFactsSource.SearchAsync
            Return Task.FromResult(New List(Of OpenAlexSourceMatch)())
        End Function
    End Class


    Private NotInheritable Class NoSuggestions
        Implements IJournalSuggestionsSource

        Public Function SearchAsync(request As JournalSuggestionRequest, cancellationToken As CancellationToken) As Task(Of JournalSuggestionsResult) Implements IJournalSuggestionsSource.SearchAsync
            Return Task.FromResult(New JournalSuggestionsResult With {.Request = request})
        End Function

        Public Function ExamplesAsync(request As JournalSuggestionRequest, openAlexId As String, cancellationToken As CancellationToken) As Task(Of List(Of EvidenceExample)) Implements IJournalSuggestionsSource.ExamplesAsync
            Return Task.FromResult(New List(Of EvidenceExample)())
        End Function
    End Class


    Private NotInheritable Class NoPublications
        Implements IPublicationSource

        Public Function LookupDoiAsync(doi As String, cancellationToken As CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Return Task.FromResult(Of CrossrefMetadataSuggestion)(Nothing)
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Return Task.FromResult(New List(Of CrossrefMetadataSuggestion)())
        End Function

        Public Function OrcidWorksAsync(orcid As String, cancellationToken As CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Return Task.FromResult(New List(Of OrcidWorkSuggestion)())
        End Function
    End Class

End Class


' The catalog's own proof: every view is listed, the listing has teeth,
' and every factory builds its view on isolated storage and closes it.
' None of it could fail before the change: without the catalog and
' StorageEnvironment.DefaultRootUses this file does not compile.
<TestClass>
<DoNotParallelize>
Public Class ViewCatalogTests

    ' Every non-abstract Form subclass in the app has a factory or a place
    ' on the not-yet list, and the list names only forms still without one.
    <TestMethod>
    Public Sub EveryViewHasAFactoryOrAReasonItHasNoneYet()

        Dim views As List(Of Type) =
            ViewCatalog.AppViews()

        Assert.IsTrue(
            views.Count > 0,
            "The app assembly has forms."
        )

        Dim unlisted As List(Of Type) =
            ViewCatalog.Unlisted(views)

        Assert.AreEqual(
            0,
            unlisted.Count,
            "Forms with neither a factory nor a not-yet reason: " & Names(unlisted)
        )

        Dim stale As List(Of Type) =
            ViewCatalog.StaleNotYet(views)

        Assert.AreEqual(
            0,
            stale.Count,
            "Not-yet entries that now have a factory or are no longer forms: " & Names(stale)
        )

    End Sub


    ' The listing has teeth: a Form subclass known to neither list is
    ' reported, as an app form without a factory would be.
    <TestMethod>
    Public Sub AFormWithNoFactoryAndNoReasonIsReported()

        CollectionAssert.AreEqual(
            {GetType(UnlistedProbeForm)},
            ViewCatalog.Unlisted({GetType(UnlistedProbeForm), GetType(AboutForm), GetType(HelpForm)})
        )

    End Sub


    ' Every factory builds and shows its view, closes it, and never asks
    ' for the default data root: all of its storage resolves under a
    ' temporary root of its own.
    <TestMethod>
    Public Sub EveryFactoryBuildsItsViewOnIsolatedStorage()

        Dim failures As New List(Of String)()

        Dim views As IEnumerable(Of (Name As String, Build As Func(Of String, Form))) =
            ViewCatalog.Dialogs.Select(Function(entry) (entry.Key.Name, entry.Value)).
            Concat(ViewCatalog.Hosted.Select(Function(entry) (entry.Name, entry.Build)))

        For Each view In views

            Dim root As String =
                CreateTemporaryRoot()

            StorageEnvironment.RootOverride =
                root

            StorageEnvironment.DefaultRootUses =
                0

            Try

                StaRunner.Run(
                    Sub()

                        Using shown As Form = view.Build(root)

                            ShowOffscreen(shown)

                            Assert.IsTrue(
                                shown.Visible,
                                view.Name & " shows."
                            )

                            shown.Close()

                        End Using

                        Assert.AreEqual(
                            0,
                            StaRunner.OpenFormsOnThisThread().Count,
                            view.Name & " leaves no form open."
                        )

                    End Sub
                )

                Assert.AreEqual(
                    0,
                    StorageEnvironment.DefaultRootUses,
                    view.Name & " asked for the default data root."
                )

            Catch ex As Exception

                ' Every failing view is named, not just the first.
                failures.Add(view.Name & ": " & ex.Message)

            Finally

                StorageEnvironment.RootOverride =
                    Nothing

                DeleteTemporaryRoot(
                    root
                )

            End Try

        Next

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

    End Sub


    ' The guard has teeth: an answer from the default root is counted, and
    ' one from RootOverride is not. The probe's root is a plain string, so
    ' no real folder is named.
    <TestMethod>
    Public Sub TheGuardCountsAnAnswerFromTheDefaultRoot()

        Dim root As String =
            CreateTemporaryRoot()

        Try

            StorageEnvironment.RootOverride =
                Nothing

            StorageEnvironment.DefaultRootUses =
                0

            Assert.AreEqual(
                "the default",
                StorageEnvironment.ResolveStorageRoot("probe", Function() "the default")
            )

            Assert.AreEqual(
                1,
                StorageEnvironment.DefaultRootUses,
                "The default root answered once."
            )

            StorageEnvironment.RootOverride =
                root

            StorageEnvironment.DefaultRootUses =
                0

            Assert.AreEqual(
                Path.Combine(root, "probe"),
                StorageEnvironment.ResolveStorageRoot("probe", Function() "the default")
            )

            Assert.AreEqual(
                0,
                StorageEnvironment.DefaultRootUses,
                "RootOverride answered instead."
            )

        Finally

            StorageEnvironment.RootOverride =
                Nothing

            DeleteTemporaryRoot(
                root
            )

        End Try

    End Sub


    Private Shared Function Names(
        types As IEnumerable(Of Type)
    ) As String

        Return String.Join(", ", types.Select(Function(type) type.Name))

    End Function


    ' A view the factory may already have shown, such as a page of the main
    ' window, is left as it is.
    Private Shared Sub ShowOffscreen(
        form As Form
    )

        If form.Visible Then
            Return
        End If

        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()

    End Sub


    ' A Form subclass of the test assembly, so on no list.
    Private NotInheritable Class UnlistedProbeForm
        Inherits Form
    End Class

End Class
