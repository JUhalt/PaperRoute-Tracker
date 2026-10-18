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
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Every view PaperRoute shows, for the sweeps that run over all of them
' (#135): scaling (#93), Dark (#106), accessible names, Esc and the
' Windows 11 chrome. The dialogs are keyed by their Form subclass, so a
' form with neither a factory nor a place on the not-yet list fails the
' catalog test; the hosted presentations and the main window's pages, which
' no Form subclass of their own keys, are listed by name, and so are the
' later steps of views whose first step hides a list or a grid. Every
' factory is handed the isolated storage root it runs on and builds its
' view the way the app does, with any files it needs under that root.
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
        {GetType(AboutForm), Function(root) New AboutForm()},
        {GetType(AddCorrespondenceForm), Function(root) New AddCorrespondenceForm()},
        {GetType(AddDecisionForm), Function(root) New AddDecisionForm()},
        {GetType(AddManuscriptForm), Function(root) New AddManuscriptForm()},
        {GetType(AddSubmissionForm), Function(root) New AddSubmissionForm()},
        {GetType(AffiliationEditForm), Function(root) New AffiliationEditForm(Nothing)},
        {GetType(ApiKeyForm), Function(root) ApiKeyForm.ForAssistant(ProtectedKeyStore.Anthropic)},
        {GetType(OpenAlexKeyForm), Function(root) New OpenAlexKeyForm()},
        {GetType(AssistantConsentForm), Function(root) New AssistantConsentForm(ResponseRequest(), ClaudeConnection())},
        {GetType(AssistantDraftForm), Function(root) New AssistantDraftForm(ResponseRequest(), String.Empty)},
        {GetType(AuthorEditForm), Function(root) New AuthorEditForm(Nothing)},
        {GetType(AuthorInstructionsForm), Function(root) New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, {}, {})},
        {GetType(AuthorLibraryForm), Function(root) New AuthorLibraryForm(New List(Of Manuscript)())},
        {GetType(BibliographyExportSelectionForm), Function(root) New BibliographyExportSelectionForm(CreateRepresentativeLibrary(), "BibTeX")},
        {GetType(BibliographyImportForm), Function(root) New BibliographyImportForm(BibTeXService.Parse(SampleBibTeX), CreateRepresentativeLibrary())},
        {GetType(CitationsUpdateForm), Function(root) New CitationsUpdateForm(Carberry, New List(Of (Doi As String, Title As String))(), Nothing, New NoCitations())},
        {GetType(CoverLetterForm), Function(root) New CoverLetterForm("Example: open materials", "Fictional Open Psychology", "Journal article", {"open science"}, "An abstract.")},
        {GetType(CrossrefLookupForm), Function(root) New CrossrefLookupForm(Routed(), AuthorLibrary())},
        {GetType(DeadlineDateForm), Function(root) New DeadlineDateForm("Postpone the follow-up", "Follow up  ·  Active Study", "This changes the follow-up date on the journal submission.", "Postpone to", DateTime.Today.AddDays(7), DateTime.Today)},
        {GetType(DecisionLetterForm), Function(root) New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission())},
        {GetType(DeleteManuscriptForm), Function(root) New DeleteManuscriptForm("Active Study")},
        {GetType(DiagnosticsForm), Function(root) New DiagnosticsForm(New AppSettings())},
        {GetType(EditManuscriptForm), Function(root) Editor(root)},
        {GetType(ExcelMappingForm), Function(root) New ExcelMappingForm(Workbook(root))},
        {GetType(ExternalLinkEditForm), Function(root) New ExternalLinkEditForm(Nothing)},
        {GetType(FillBlanksForm), Function(root) New FillBlanksForm(WithDoi(), New NoPublications(), Function() True)},
        {GetType(HelpForm), Function(root) New HelpForm()},
        {GetType(JournalCandidateForm), Function(root) New JournalCandidateForm(New JournalCandidate With {.JournalName = "PLOS ONE"}, {"PLOS ONE"}, Function(name) "No submissions yet.")},
        {GetType(JournalChecklistItemEditForm), Function(root) New JournalChecklistItemEditForm(New JournalChecklistTemplateItem With {.Title = "Data availability statement"})},
        {GetType(JournalEditForm), Function(root) New JournalEditForm(Journal())},
        {GetType(JournalFactsForm), Function(root) New JournalFactsForm(Journal(), New NoFacts())},
        {GetType(JournalLibraryForm), Function(root) New JournalLibraryForm(New List(Of Manuscript)())},
        {GetType(JournalMetricForm), Function(root) New JournalMetricForm()},
        {GetType(JournalPickerForm), Function(root) New JournalPickerForm(AuthorLibrary())},
        {GetType(JournalSuggestionsForm), Function(root) New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, New NoSuggestions(), Nothing, Nothing, DateTime.Today)},
        {GetType(ManuscriptAuthorForm), Function(root) New ManuscriptAuthorForm(AuthorsWithAffiliations(), Nothing, Array.Empty(Of Guid)())},
        {GetType(ManuscriptLinksForm), Function(root) New ManuscriptLinksForm(Routed(), CreateRepresentativeLibrary())},
        {GetType(ManuscriptReadinessForm), Function(root) New ManuscriptReadinessForm(WithPackets(), New AuthorLibraryData())},
        {GetType(ManuscriptRouteViewForm), Function(root) New ManuscriptRouteViewForm(InRevision())},
        {GetType(ManuscriptVersionEditForm), Function(root) VersionEditor()},
        {GetType(MarkPublishedForm), Function(root) New MarkPublishedForm(Routed(), Found(), DateTime.Today)},
        {GetType(OrcidLookupForm), Function(root) New OrcidLookupForm(AuthorLibrary().Authors(0))},
        {GetType(PacketExportForm), Function(root) PacketExport(root)},
        {GetType(PublicationCheckForm), Function(root) New PublicationCheckForm(CreateRepresentativeLibrary(), Nothing, String.Empty, New NoPublications(), Function() True, DateTime.Today)},
        {GetType(PublicationExportForm), Function(root) New PublicationExportForm(CreateRepresentativeLibrary(), AuthorLibrary())},
        {GetType(ReadinessItemNotesForm), Function(root) New ReadinessItemNotesForm(WithPackets().ReadinessProfiles(0).Items(0))},
        {GetType(ReminderEditForm), Function(root) New ReminderEditForm(CreateRepresentativeLibrary())},
        {GetType(ReportForm), Function(root) New ReportForm("Pipeline Report", "report.html", CreateRepresentativeLibrary(), Function(chosen, deadlines) ReportService.PipelineReport(chosen, DateTime.Today))},
        {GetType(ReviewerResponseExportForm), Function(root) New ReviewerResponseExportForm("# Responses to reviewers")},
        {GetType(ReviewerResponseItemForm), Function(root) New ReviewerResponseItemForm(Routed().Submissions(0))},
        {GetType(ReviewerResponseMatrixForm), Function(root) Matrix()},
        {GetType(SettingsForm), Function(root) New SettingsForm(New AppSettings())},
        {GetType(StartupFailureForm), Function(root) StartupWindow(root)},
        {GetType(SubmissionDetailsForm), Function(root) Details()},
        {GetType(SubmissionPacketEditForm), Function(root) PacketEditor()},
        {GetType(SubmissionPacketFileEditForm), Function(root) PacketFileEditor()},
        {GetType(SubmissionPacketVaultForm), Function(root) New SubmissionPacketVaultForm(WithPackets())},
        {GetType(TitlePageImportForm), Function(root) New TitlePageImportForm(New AuthorLibraryData(), "A preregistered replication of anchoring effects")},
        {GetType(UnexpectedErrorForm), Function(root) New UnexpectedErrorForm(New InvalidOperationException("The route map could not be drawn."), True, Path.Combine(root, "logs"))},
        {GetType(UpdateProgressForm), Function(root) PartwayThrough(New UpdateProgressForm("0.9.2"))},
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
        New NamedView("Deadlines page", Function(root) DeadlinesPage()),
        New NamedView("Insights page", Function(root) Page(Form1.WorkspacePage.Insights)),
        New NamedView("Import & Export page", Function(root) ImportExportPage()),
        New NamedView("EditManuscriptForm as the manuscript page", Function(root) Hosting(Of EditManuscriptForm)(ManuscriptPage())),
        New NamedView("SubmissionDetailsForm inside the manuscript page", Function(root) Hosting(Of SubmissionDetailsForm)(ManuscriptPage())),
        New NamedView("ReviewerResponseMatrixForm inside the submission's details", Function(root) Hosting(Of ReviewerResponseMatrixForm)(ManuscriptPage()))
    }


    ' Views at a step their first one does not show: the lists and grids a
    ' lookup, a reading or a parse fills, and what the main window shows
    ' only in some states (#106). Each factory shows its view to reach the
    ' step, as the main window's pages are shown, and fails when the step
    ' does not show.
    Public Shared ReadOnly Stages As NamedView() = {
        New NamedView("CitationsUpdateForm, confirming the works it found", Function(root) CitationsFound()),
        New NamedView("JournalFactsForm, with the changes it found", Function(root) FactsFound()),
        New NamedView("JournalSuggestionsForm, with the journals it found", Function(root) JournalsFound()),
        New NamedView("FillBlanksForm, with the blanks it can fill", Function(root) BlanksFound()),
        New NamedView("DecisionLetterForm, reviewing the comments it read", Function(root) LetterRead()),
        New NamedView("AuthorInstructionsForm, reviewing the requirements it read", Function(root) InstructionsRead()),
        New NamedView("TitlePageImportForm, with the authors it read", Function(root) TitlePageRead()),
        New NamedView("Insights page, Your Citations before an update", Function(root) Citations(root, saved:=False)),
        New NamedView("Insights page, Your Citations from a saved update", Function(root) Citations(root, saved:=True)),
        New NamedView("EditManuscriptForm as the manuscript page, after its shortlist is rebuilt", Function(root) ShortlistRebuilt())
    }


    ' Forms no test builds yet, each with what its factory will need. Each
    ' sweep shrinks this list; the catalog test fails when an entry is stale.
    ' The accessibility sweeps (#136, #137) needed every view, so it is
    ' empty; a new form without a factory goes here with its reason.
    Public Shared ReadOnly NotYet As (View As Type, Reason As String)() = {}


    ' Every view in the app: the non-abstract Form subclasses of its assembly.
    Public Shared Function AppViews() As List(Of Type)

        Return GetType(Form1).Assembly.GetTypes().
            Where(Function(type) GetType(Form).IsAssignableFrom(type) AndAlso Not type.IsAbstract).
            OrderBy(Function(type) type.Name, StringComparer.Ordinal).
            ToList()

    End Function


    ' The views with neither a factory nor a place on the not-yet list. A
    ' test may pass a not-yet list of its own, as the real one is empty.
    Public Shared Function Unlisted(
        views As IEnumerable(Of Type),
        Optional notYetViews As IEnumerable(Of Type) = Nothing
    ) As List(Of Type)

        Dim listed As New HashSet(Of Type)(
            Dialogs.Keys.Concat(If(notYetViews, NotYet.Select(Function(entry) entry.View)))
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


    ' Every view a sweep runs over: the dialogs, then the hosted
    ' presentations and the main window's pages.
    Public Shared Function AllViews() As List(Of (Name As String, Build As Func(Of String, Form)))

        Return Dialogs.Select(Function(entry) (entry.Key.Name, entry.Value)).
            Concat(Hosted.Select(Function(entry) (entry.Name, entry.Build))).
            ToList()

    End Function


    ' Every factory, by name: every view, then the stages.
    Public Shared Function AllViewsAndStages() As List(Of (Name As String, Build As Func(Of String, Form)))

        Return AllViews().
            Concat(Stages.Select(Function(entry) (entry.Name, entry.Build))).
            ToList()

    End Function


    ' Builds each view on a temporary root of its own and shows it, styles
    ' it, hands it to inspect and closes it on the shared runner, in the
    ' color mode if one is given (see ShowAndClose). Returns a line for
    ' every view that failed, or that asked for the default data root, so
    ' a sweep names them all, not just the first.
    Public Shared Function ShowEach(
        views As IEnumerable(Of (Name As String, Build As Func(Of String, Form))),
        Optional inspect As Action(Of String, Form) = Nothing,
        Optional mode As SystemColorMode? = Nothing,
        Optional styled As Boolean = True
    ) As List(Of String)

        Dim failures As New List(Of String)()

        For Each view In views

            Dim root As String =
                CreateTemporaryRoot()

            StorageEnvironment.RootOverride =
                root

            StorageEnvironment.DefaultRootUses =
                0

            Try

                ShowAndClose(
                    view.Name,
                    Function() view.Build(root),
                    inspect,
                    mode,
                    styled
                )

                ' A guard that cannot fail while RootOverride stands: every
                ' default root resolves through ResolveStorageRoot, so the
                ' override is what keeps the view off the user's profile. This
                ' catches only a view that clears the override as it builds;
                ' TheGuardCountsAnAnswerFromTheDefaultRoot proves the count.
                Assert.AreEqual(
                    0,
                    StorageEnvironment.DefaultRootUses,
                    view.Name & " asked for the default data root."
                )

            Catch ex As Exception

                failures.Add(view.Name & ": " & ex.Message)

            Finally

                StorageEnvironment.RootOverride =
                    Nothing

                DeleteTemporaryRoot(
                    root
                )

            End Try

        Next

        Return failures

    End Function


    ' Builds the view and shows it offscreen if its factory did not, then
    ' styles it once open, as the app's idle hook styles every window but
    ' the main one (UiPolish.InstallGlobalDialogStyling), hands it to
    ' inspect and closes it, on the shared runner and in the color mode if
    ' one is given. Anything the view left open is closed too. Unstyled,
    ' inspect sees the view as it first shows, before the idle hook runs.
    Public Shared Sub ShowAndClose(
        name As String,
        build As Func(Of Form),
        Optional inspect As Action(Of String, Form) = Nothing,
        Optional mode As SystemColorMode? = Nothing,
        Optional styled As Boolean = True
    )

        StaRunner.Run(
            Sub()

                Try

                    Using shown As Form = build()

                        ShowOffscreen(shown)

                        Assert.IsTrue(
                            shown.Visible,
                            name & " shows."
                        )

                        If styled AndAlso Not TypeOf shown Is Form1 Then
                            UiPolish.ApplyDialog(shown)
                            Application.DoEvents()
                        End If

                        inspect?.Invoke(name, shown)

                        shown.Close()

                    End Using

                    Assert.AreEqual(
                        0,
                        StaRunner.OpenFormsOnThisThread().Count,
                        name & " leaves no form open."
                    )

                Finally

                    ' A view that failed after it showed is closed too, so it
                    ' fails only itself.
                    For Each left As Form In StaRunner.OpenFormsOnThisThread()
                        left.Dispose()
                    Next

                End Try

            End Sub,
            mode:=mode
        )

    End Sub


    ' A view the factory may already have shown, such as a page of the main
    ' window, is left as it is.
    Public Shared Sub ShowOffscreen(
        form As Form
    )

        If form.Visible Then
            Return
        End If

        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()

        ' A window that centers itself when shown goes back offscreen.
        form.Location = New Point(-20000, -20000)
        Application.DoEvents()

    End Sub


    ' ---------------------------------------------------------------
    ' Samples
    ' ---------------------------------------------------------------

    Private Const Carberry As String = "0000-0002-1825-0097"


    ' The representative library's manuscript under review, with a
    ' submission and its revision decision.
    Private Shared Function Routed() As Manuscript
        Return CreateRepresentativeLibrary()(0)
    End Function


    ' The study in revision since its decision, with the revised version
    ' current, so Route View marks the current state and the current
    ' version on the decision's waypoint, each with its help.
    Private Shared Function InRevision() As Manuscript

        Dim manuscript As Manuscript = Routed()
        Dim submission As JournalSubmission = manuscript.Submissions(0)
        Dim decision As EditorialDecisionEvent = submission.Decisions(0)
        manuscript.CurrentStage = PaperStage.Revision
        manuscript.StageEnteredDate = decision.DecisionDate

        Dim version As New ManuscriptVersion With {.Label = "Revised manuscript", .SubmissionId = submission.Id, .DecisionId = decision.Id}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id
        Return manuscript

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


    ' A reusable author, affiliation and journal, as the author library
    ' holds them. Josiah Carberry is ORCID's own test record.
    Private Shared Function AuthorLibrary() As AuthorLibraryData
        Dim library As New AuthorLibraryData()
        library.Affiliations.Add(New AffiliationRecord With {.Institution = "Example University", .Department = "Psychology", .Country = "Canada"})
        library.Authors.Add(New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .Orcid = Carberry})
        library.Journals.Add(Journal())
        Return library
    End Function


    ' A possible publication of the manuscript under review.
    Private Shared Function Found() As PublicationMatch
        Return New PublicationMatch With {.Doi = "10.5555/example.active", .Title = "Active Study", .Journal = "Journal of Example Studies", .PublishedDate = New DateTime(2026, 9, 1)}
    End Function


    ' A workbook no importer recognises, under the root, so Excel import
    ' asks for its columns.
    Private Shared Function Workbook(root As String) As String
        Dim workbookPath As String = Path.Combine(root, "tracker.xlsx")
        Using book As New ClosedXML.Excel.XLWorkbook()
            Dim sheet = book.Worksheets.Add("My Tracker")
            sheet.Cell("A1").Value = "Project Name"
            sheet.Cell("B1").Value = "Outlet"
            sheet.Cell("A2").Value = "Mapped Study"
            sheet.Cell("B2").Value = "Journal of Mapping"
            book.SaveAs(workbookPath)
        End Using
        Return workbookPath
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


    Private Shared Function PacketFileEditor() As SubmissionPacketFileEditForm
        Dim packet As SubmissionPacket = WithPackets().SubmissionPackets(0)
        Return New SubmissionPacketFileEditForm(packet, packet.Files(0))
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


    ' The window PaperRoute shows when it cannot start, over a damaged
    ' library beside a previous save that reads, so both of its ways to put
    ' the library right (#124) are on it. Its recovery is the app's, built
    ' on files under the root, never on the default storage, through the
    ' constructor tests use; a window without both ways fails the catalog.
    Private Shared Function StartupWindow(root As String) As StartupFailureForm

        Dim current As String = Path.Combine(root, "current")
        Dim library As String = Path.Combine(root, "library")
        Dim repository As New ManuscriptRepository(Path.Combine(current, "data"), library)
        Dim saved As New List(Of Manuscript) From {New Manuscript With {.Title = "A preregistered replication of anchoring effects"}}
        repository.Save(saved)
        repository.Save(saved)
        File.WriteAllText(repository.DataFilePath, "{ damaged")

        Dim window As New StartupFailureForm(
            StartupFailure.ForManuscriptLibrary(New InvalidDataException("The library file could not be read."), repository.DataFilePath),
            Function() New StartupRecoveryService(repository, library, StorageMigrationService.SchemaFilePath(current)))

        Dim offered As Integer = Descendants(window).OfType(Of Button)().
            Count(Function(button) button.Text = "Use the Previous Save" OrElse button.Text = "Restore From Backup...")

        If offered <> 2 Then
            window.Dispose()
            Throw New InvalidOperationException("StartupFailureForm offers " & offered & " of its two ways to put the library right.")
        End If

        Return window

    End Function


    ' The update partway through, so a sweep judges the bar's fill and not
    ' an empty bar on the window color. Windows grows a bar to a higher
    ' value over a moment but draws a lower one at once, so the bar is
    ' shown, set just past the value and then back; a bar whose fill is not
    ' drawn fails the factory.
    Private Shared Function PartwayThrough(dialog As UpdateProgressForm) As Form

        ShowOffscreen(dialog)
        dialog.SetProgress(61)
        dialog.SetProgress(60)
        Application.DoEvents()

        Dim bar As ProgressBar = Descendants(dialog).OfType(Of ProgressBar)().Single()
        Using image As New Bitmap(bar.Width, bar.Height)
            bar.DrawToBitmap(image, New Rectangle(Point.Empty, bar.Size))
            Dim middle As Integer = bar.Height \ 2
            Dim drawn As Boolean = image.GetPixel(bar.Width * 3 \ 10, middle).ToArgb() <> image.GetPixel(bar.Width * 9 \ 10, middle).ToArgb()
            Return Reached(dialog, drawn, "does not draw its bar's fill")
        End Using

    End Function


    Private Const SampleBibTeX As String =
        "@article{whitfield2025, title={A preregistered replication of anchoring effects}, author={Whitfield, Dana and Okafor, Rui}, " &
        "journal={Fictional Journal of Psychology}, year={2025}, doi={10.5555/anchoring.2025}}"

    Private Const PublishedDoi As String = "10.5555/example.published"


    ' The representative library with a DOI on its published study, so
    ' Fill Blanks has a manuscript to offer, with its journal, volume,
    ' pages and abstract still blank.
    Private Shared Function WithDoi() As List(Of Manuscript)
        Dim library As List(Of Manuscript) = CreateRepresentativeLibrary()
        library(1).Metadata.Doi = PublishedDoi
        Return library
    End Function


    ' The researcher and two affiliations, so the manuscript's author
    ' window lists more than one affiliation to check.
    Private Shared Function AuthorsWithAffiliations() As AuthorLibraryData
        Dim library As New AuthorLibraryData()
        library.Affiliations.Add(New AffiliationRecord With {.Institution = "Blue Mountain University", .Department = "Department of Psychology"})
        library.Affiliations.Add(New AffiliationRecord With {.Institution = "Cascade State University", .Department = "School of Nursing"})
        library.Authors.Add(New AuthorRecord With {.GivenName = "Dana", .FamilyName = "Whitfield", .IsMe = True})
        Return library
    End Function


    ' ---------------------------------------------------------------
    ' Stages
    ' ---------------------------------------------------------------

    Private Shared ReadOnly LetterDay As New DateTime(2026, 10, 1)


    ' The update window on a recorded answer with a work in each group, not
    ' yet shown; DarkThemeLegibilityTests builds it too.
    Friend Shared Function CitationsDialog() As CitationsUpdateForm
        Dim groups As New List(Of OrcidWorkGroup) From {
            New OrcidWorkGroup With {.Dois = New List(Of String) From {"10.5555/test.orcid"}, .Title = "A work on the ORCID record", .WorkType = "journal-article"}
        }
        Dim found As New Dictionary(Of String, CitedWork) From {
            {"10.5555/test.orcid", Work("W1", "10.5555/test.orcid", "A work on the ORCID record")},
            {"10.5555/test.manuscript", Work("W2", "10.5555/test.manuscript", "A published manuscript")}
        }
        Dim linked As New List(Of CitedWork) From {Work("W3", "10.5555/test.linked", "A work OpenAlex links to the iD")}
        Dim manuscripts As New List(Of (Doi As String, Title As String)) From {("10.5555/test.manuscript", "A published manuscript")}
        Dim lookup As CitationLookup = CitationsService.Assemble(Carberry, groups, manuscripts, found, linked, 1, Array.Empty(Of String)())
        Return New CitationsUpdateForm(Carberry, manuscripts, Nothing, New RecordedCitations(lookup))
    End Function


    Private Shared Function CitationsFound() As Form
        Dim dialog As CitationsUpdateForm = Shown(CitationsDialog())
        dialog.LookUpAsync().GetAwaiter().GetResult()
        Application.DoEvents()
        Return Reached(dialog, dialog.WorksList.Items.Count > 0, "found no works")
    End Function


    ' The researcher's own publisher is kept, so its row starts unchecked.
    Private Shared Function FactsFound() As Form
        Dim record As New JournalRecord With {.Name = "Fictional Journal", .Publisher = "Fictional Press (my spelling)", .Issns = New List(Of String) From {"1932-6203"}}
        Dim lookup As New JournalFactsLookup With {
            .Issns = New List(Of String) From {"1932-6203"},
            .Doaj = New DoajJournal With {.Title = "Fictional Journal", .Publisher = "Fictional Press", .HomepageUrl = "https://journal.example.invalid/"},
            .DoajChecked = True
        }
        Dim dialog As JournalFactsForm = Shown(New JournalFactsForm(record, New RecordedFacts(lookup)))
        dialog.LookUpAsync().GetAwaiter().GetResult()
        Application.DoEvents()
        Return Reached(dialog, dialog.ChangesList.Items.Count > 0, "found no changes")
    End Function


    Private Shared Function JournalsFound() As Form
        Dim dialog As JournalSuggestionsForm = Shown(New JournalSuggestionsForm("Anchoring", {"anchoring effects"}, New RecordedSuggestions(), Nothing, Nothing, DateTime.Today))
        dialog.SearchAsync().GetAwaiter().GetResult()
        Application.DoEvents()
        Return Reached(dialog, dialog.JournalsList.Items.Count > 0, "found no journals")
    End Function


    Private Shared Function BlanksFound() As Form
        Dim work As New CrossrefMetadataSuggestion With {
            .Doi = PublishedDoi, .Title = "Published Study", .Journal = "Behavioral Examples", .Volume = "12", .Pages = "1-20",
            .PublishedDate = New DateTime(2026, 5, 15), .AbstractText = "A fictional abstract."
        }
        Dim dialog As FillBlanksForm = Shown(New FillBlanksForm(WithDoi(), New NoPublications(work), Function() True) With {.Pause = TimeSpan.Zero})
        dialog.LookUpAsync().GetAwaiter().GetResult()
        Application.DoEvents()
        Return Reached(dialog, dialog.PlannedChanges.Count > 0, "found no blanks to fill")
    End Function


    ' What the assistant's fictional answer proposes, put in front of the
    ' researcher in the open window as a reading does: no provider is asked.
    Private Shared Function LetterRead() As Form
        Dim dialog As DecisionLetterForm = Shown(New DecisionLetterForm(DecisionLetterMode.NewDecision, New JournalSubmission With {.JournalName = "Fictional Journal of Psychology"}, AssistantCoreTests.Letter, LetterDay))
        Dim found As DecisionLetterProposal = AssistantService.ReadLetterReply(
            New AssistantReply With {.Text = AssistantCoreTests.LetterAnswer, .ProviderName = "Claude", .Model = "claude-opus-5-5"}, AssistantCoreTests.Letter, LetterDay)
        SetPrivate(dialog, "_letter", AssistantCoreTests.Letter)
        CallPrivate(dialog, "ShowReview", found)
        Application.DoEvents()
        Return Reached(dialog, dialog.IsReviewing AndAlso dialog.CommentsGrid.Rows.Count > 0, "is not reviewing the letter's comments")
    End Function


    Private Shared Function InstructionsRead() As Form
        Dim dialog As AuthorInstructionsForm = Shown(New AuthorInstructionsForm("Fictional Open Psychology", String.Empty, {}, {}, AuthorInstructionsTests.Instructions))
        Dim found As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(
            New AssistantReply With {.Text = AuthorInstructionsTests.InstructionsAnswer, .ProviderName = "Claude", .Model = "claude-opus-5-5"},
            AuthorInstructionsTests.Instructions, AssistantService.CategoriesFor(Nothing))
        SetPrivate(dialog, "_text", AuthorInstructionsTests.Instructions)
        CallPrivate(dialog, "ShowReview", found)
        Application.DoEvents()
        Return Reached(dialog, dialog.IsReviewing AndAlso dialog.RequirementsGrid.Rows.Count > 0, "is not reviewing the requirements")
    End Function


    Private Shared Function TitlePageRead() As Form
        Dim dialog As New TitlePageImportForm(New AuthorLibraryData(), String.Join(vbLf, {
            "A preregistered replication of anchoring effects in clinical risk estimates",
            "",
            "Dana Whitfield1, Rui Okafor1,2*, and Lena Marsh2",
            "",
            "1 Department of Psychology, Blue Mountain University",
            "2 School of Nursing, Cascade State University"
        }))
        dialog.ParseNow()
        Return Reached(dialog, Descendants(dialog).OfType(Of DataGridView)().Any(Function(grid) grid.Rows.Count > 0), "read no authors")
    End Function


    ' Your Citations, with or without an update saved under the root. With
    ' one, Update from OpenAlex shows, off until the researcher's ORCID iD
    ' is known; without, Open Authors & Affiliations shows.
    Private Shared Function Citations(root As String, saved As Boolean) As Form
        Dim store As New CitationStore(Path.Combine(root, "citations"))
        If saved Then
            store.Save(New CitationSnapshot With {
                .Orcid = Carberry, .Source = "OpenAlex", .RetrievedUtc = DateTime.UtcNow,
                .Works = New List(Of CitedWork) From {Work("W1", "10.5555/test.orcid", "A work on the ORCID record")}
            })
        End If
        Dim board As New CatalogBoard()
        board.citationStoreFactory = Function() store
        board.Prepare(CreateRepresentativeLibrary())
        board.NavigateTo(Form1.WorkspacePage.Insights)
        ' The Insights figures are worked out off the UI thread (#132), on
        ' the page and again on the tab.
        TestSupport.WaitForInsights(board, TimeSpan.FromSeconds(30))
        board.ShowCitations()
        TestSupport.WaitForInsights(board, TimeSpan.FromSeconds(30))
        Dim action As String = If(saved, "Update from OpenAlex...", "Open Authors && Affiliations")
        Return Reached(board, Descendants(board).OfType(Of Button)().Any(Function(button) button.Visible AndAlso button.Text = action), "does not show " & action)
    End Function


    ' The manuscript page after its shortlist's rows are built again, as
    ' adding, moving or editing a candidate does.
    Private Shared Function ShortlistRebuilt() As Form
        Dim library As List(Of Manuscript) = CreateRepresentativeLibrary()
        library(0).JournalShortlist = New List(Of JournalCandidate) From {
            New JournalCandidate With {.JournalName = "PLOS ONE"},
            New JournalCandidate With {.JournalName = "Collabra: Psychology"}
        }
        Dim board As CatalogBoard = BoardOn(library)
        board.Open(library(0))
        Dim editor As EditManuscriptForm = Descendants(board).OfType(Of EditManuscriptForm)().Single()
        CallPrivate(editor, "RefreshShortlist")
        Application.DoEvents()
        Return Reached(board, Descendants(editor).OfType(Of Button)().Any(Function(button) button.Visible AndAlso button.AccessibleName = "Edit PLOS ONE"), "shows no shortlist row")
    End Function


    Private Shared Function Work(id As String, doi As String, title As String) As CitedWork
        Return New CitedWork With {.OpenAlexId = id, .Doi = doi, .Title = title, .Year = 2025, .Journal = "Fictional Journal", .CitedByCount = 3}
    End Function


    ' Shows a dialog offscreen, so a step that needs its window can run.
    Private Shared Function Shown(Of T As Form)(dialog As T) As T
        ShowOffscreen(dialog)
        Return dialog
    End Function


    ' The view, once the step it is for shows; otherwise it is closed and
    ' the factory fails.
    Private Shared Function Reached(view As Form, shows As Boolean, otherwise As String) As Form
        If Not shows Then
            Dim name As String = view.GetType().Name
            view.Dispose()
            Throw New InvalidOperationException(name & " " & otherwise & ".")
        End If
        Return view
    End Function


    Private Shared Sub CallPrivate(target As Object, name As String, ParamArray args As Object())
        target.GetType().GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(target, args)
    End Sub


    Private Shared Sub SetPrivate(target As Object, name As String, value As Object)
        target.GetType().GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(target, value)
    End Sub


    ' ---------------------------------------------------------------
    ' The main window and its pages
    ' ---------------------------------------------------------------

    Private Shared Function BoardOn(library As List(Of Manuscript)) As CatalogBoard
        Dim board As New CatalogBoard()
        board.Prepare(library)
        Return board
    End Function


    ' The Board, shown, on a library of the test's own.
    Friend Shared Function BoardOf(library As List(Of Manuscript)) As Form1
        Return BoardOn(library)
    End Function


    Private Shared Function Welcome() As Form
        Dim board As CatalogBoard = BoardOn(New List(Of Manuscript)())
        If Not board.Welcome.Visible Then
            Throw New InvalidOperationException("An empty library does not show the welcome.")
        End If
        Return board
    End Function


    Private Shared Function Page(target As Form1.WorkspacePage, Optional library As List(Of Manuscript) = Nothing) As CatalogBoard
        Dim board As CatalogBoard = BoardOn(If(library, CreateRepresentativeLibrary()))
        board.NavigateTo(target)
        Application.DoEvents()
        ' The Insights figures are worked out off the UI thread (#132): the
        ' page is listed with them shown, not while it says Calculating.
        TestSupport.WaitForInsights(board, TimeSpan.FromSeconds(30))
        Return board
    End Function


    ' The Deadlines page with something in each part of its drawn list
    ' (#133). The representative library asks for no action, so on it the
    ' list draws only its sentence; here a reminder is overdue and another
    ' due today, a follow-up is due in three days, and a reminder was done
    ' yesterday, so a sweep reads the list's headings, its rows with their
    ' drawn buttons and "⋯", and the Done line. The factory fails when the
    ' list does not draw each.
    Private Shared Function DeadlinesPage() As Form

        Dim today As DateTime = DateTime.Today
        Dim library As List(Of Manuscript) = CreateRepresentativeLibrary()
        Dim active As Manuscript = library(0)
        active.Reminders.Add(New ManuscriptReminder With {.Title = "Send the response letter to the coauthors", .DueDate = today.AddDays(-2)})
        active.Reminders.Add(New ManuscriptReminder With {.Title = "Check the journal's data policy", .DueDate = today})
        active.Reminders.Add(New ManuscriptReminder With {.Title = "Book the lab for the replication", .DueDate = today.AddDays(-6), .IsCompleted = True, .CompletedDate = today.AddDays(-1)})
        active.Submissions(0).FollowUpDate = today.AddDays(3)

        Dim board As CatalogBoard = Page(Form1.WorkspacePage.Deadlines, library)
        Dim list As DeadlineList = Descendants(board).OfType(Of DeadlineList)().Single()
        Dim drawn As List(Of DeadlineEntryKind) = list.Entries.Select(Function(entry) entry.Kind).ToList()
        Return Reached(board,
                       drawn.Contains(DeadlineEntryKind.Heading) AndAlso drawn.Contains(DeadlineEntryKind.DoneToggle) AndAlso
                           list.Rows.Any(Function(row) row.Buttons.Count > 0 AndAlso row.HasMore),
                       "does not draw headings, rows with their buttons and ""⋯"", and the Done line")

    End Function


    ' The Import & Export page, once its list holds its commands and draws
    ' its headings and descriptions around them (#133).
    Private Shared Function ImportExportPage() As Form

        Dim board As CatalogBoard = Page(Form1.WorkspacePage.ImportExport)
        Dim list As CommandList = Descendants(board).OfType(Of CommandList)().Single()
        Dim commands As Integer = list.Controls.OfType(Of ActionButton)().Count()
        Return Reached(board,
                       commands > 0 AndAlso list.Descriptions.Count = commands AndAlso list.AccessibilityObject.GetChildCount() > commands * 2,
                       "does not draw its headings and descriptions around its commands")

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
            ' The frame, as Form1_Load sets it (#143); WindowChromeTests checks Load's call.
            WindowChrome.Apply(Me)
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

        Public Sub ShowCitations()
            DirectCast(GetType(Form1).GetField("tabInsightsCitations", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), ShelfTabButton).Checked = True
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
    ' Sources that answer nothing, or a recorded answer, so no view goes
    ' online
    ' ---------------------------------------------------------------

    Private NotInheritable Class NoCitations
        Implements ICitationSource

        Public Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                    excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync
            Return Task.FromResult(New CitationLookup With {.Orcid = orcid})
        End Function
    End Class


    Private NotInheritable Class RecordedCitations
        Implements ICitationSource

        Private ReadOnly _lookup As CitationLookup

        Public Sub New(lookup As CitationLookup)
            _lookup = lookup
        End Sub

        Public Function LookupAsync(orcid As String, manuscriptDois As IEnumerable(Of (Doi As String, Title As String)), includeOpenAlexLinked As Boolean,
                                    excluded As IEnumerable(Of String), progress As IProgress(Of String), cancellationToken As CancellationToken) As Task(Of CitationLookup) Implements ICitationSource.LookupAsync
            Return Task.FromResult(_lookup)
        End Function
    End Class


    Private NotInheritable Class RecordedFacts
        Implements IJournalFactsSource

        Private ReadOnly _lookup As JournalFactsLookup

        Public Sub New(lookup As JournalFactsLookup)
            _lookup = lookup
        End Sub

        Public Function LookupAsync(issns As IEnumerable(Of String), openAlexId As String, cancellationToken As CancellationToken) As Task(Of JournalFactsLookup) Implements IJournalFactsSource.LookupAsync
            Return Task.FromResult(_lookup)
        End Function

        Public Function SearchAsync(name As String, cancellationToken As CancellationToken) As Task(Of List(Of OpenAlexSourceMatch)) Implements IJournalFactsSource.SearchAsync
            Return Task.FromResult(New List(Of OpenAlexSourceMatch)())
        End Function
    End Class


    ' One fictional journal for any request.
    Private NotInheritable Class RecordedSuggestions
        Implements IJournalSuggestionsSource

        Public Function SearchAsync(request As JournalSuggestionRequest, cancellationToken As CancellationToken) As Task(Of JournalSuggestionsResult) Implements IJournalSuggestionsSource.SearchAsync
            Dim journal As New JournalSuggestion With {
                .OpenAlexId = "S1", .Name = "Fictional Journal of Anchoring", .Publisher = "Fictional Press", .MatchingArticles = 12, .AllArticles = 400
            }
            Return Task.FromResult(New JournalSuggestionsResult With {.Request = request, .Journals = New List(Of JournalSuggestion) From {journal}, .TotalMatching = 12})
        End Function

        Public Function ExamplesAsync(request As JournalSuggestionRequest, openAlexId As String, cancellationToken As CancellationToken) As Task(Of List(Of EvidenceExample)) Implements IJournalSuggestionsSource.ExamplesAsync
            Return Task.FromResult(New List(Of EvidenceExample)())
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


    ' Answers a DOI lookup with the work it is given, if any.
    Private NotInheritable Class NoPublications
        Implements IPublicationSource

        Private ReadOnly _work As CrossrefMetadataSuggestion

        Public Sub New(Optional work As CrossrefMetadataSuggestion = Nothing)
            _work = work
        End Sub

        Public Function LookupDoiAsync(doi As String, cancellationToken As CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Return Task.FromResult(_work)
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
    ' reported, as an app form without a factory would be. Beside it are
    ' one form from each list, HelpForm with a factory and a probe form on
    ' a not-yet list of the test's own (the real one is empty now), and
    ' neither is reported.
    <TestMethod>
    Public Sub AFormWithNoFactoryAndNoReasonIsReported()

        CollectionAssert.AreEqual(
            {GetType(UnlistedProbeForm)},
            ViewCatalog.Unlisted({GetType(UnlistedProbeForm), GetType(NotYetProbeForm), GetType(HelpForm)}, {GetType(NotYetProbeForm)})
        )

    End Sub


    ' Every factory builds and shows its view, closes it, and never asks
    ' for the default data root: all of its storage resolves under a
    ' temporary root of its own. The stages are factories too.
    <TestMethod>
    Public Sub EveryFactoryBuildsItsViewOnIsolatedStorage()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(ViewCatalog.AllViewsAndStages())

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

    End Sub


    ' Every dialog is styled when it first shows, not only once the app's
    ' idle hook reaches it: in Dark, each one the catalog builds and shows,
    ' and does not style again, already has the dark title bar that
    ' UiPolish.ApplyDialog sets first. Unstyled until then, a dialog paints
    ' its first frames in Windows' colors. The main window styles itself.
    <TestMethod>
    Public Sub EveryDialogIsStyledWhenItFirstShows()

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) OrElse SystemInformation.HighContrast Then
            Assert.Inconclusive("The dark title bar needs Windows 11 and no contrast theme.")
        End If

        Dim unstyled As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.Dialogs.
                    Where(Function(entry) entry.Key IsNot GetType(Form1)).
                    Select(Function(entry) (entry.Key.Name, entry.Value)),
                Sub(name, shown)

                    Dim frame As WindowChrome.Frame =
                        WindowChrome.FrameOf(shown)

                    If frame Is Nothing OrElse Not frame.Dark Then
                        unstyled.Add(name)
                    End If

                End Sub,
                mode:=SystemColorMode.Dark,
                styled:=False
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        Assert.AreEqual(
            0,
            unstyled.Count,
            "Not styled when first shown: " & String.Join(", ", unstyled)
        )

    End Sub


    ' A view that fails after it has shown, as the welcome and the hosted
    ' views do when their check fails, fails only itself: its window is not
    ' left in Application.OpenForms, where ManuscriptPageTests and the
    ' unexpected-error path look for a main window.
    <TestMethod>
    Public Sub AViewThatFailsAfterItShowsIsClosedAllTheSame()

        Dim openBefore As Integer =
            Application.OpenForms.Count

        Dim failure As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub()
                    ViewCatalog.ShowAndClose(
                        "The probe",
                        Function()
                            Dim probe As New Form With {
                                .Text = "Failing probe",
                                .StartPosition = FormStartPosition.Manual,
                                .Location = New Point(-20000, -20000),
                                .ShowInTaskbar = False
                            }
                            probe.Show()
                            Application.DoEvents()
                            Throw New InvalidOperationException("The probe failed after it showed.")
                        End Function
                    )
                End Sub
            )

        Assert.AreEqual(
            "The probe failed after it showed.",
            failure.Message
        )

        Assert.AreEqual(
            openBefore,
            Application.OpenForms.Count,
            "The failed view's window is not left open."
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


    ' A Form subclass of the test assembly, so on no list.
    Private NotInheritable Class UnlistedProbeForm
        Inherits Form
    End Class


    ' Another, which the listing test puts on a not-yet list of its own.
    Private NotInheritable Class NotYetProbeForm
        Inherits Form
    End Class

End Class
