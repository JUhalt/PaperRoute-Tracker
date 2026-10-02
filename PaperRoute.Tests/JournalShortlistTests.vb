Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Per-manuscript journal shortlists (#65) and the questions for choosing a
' journal (#89).
<TestClass>
Public Class JournalShortlistTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    <TestMethod>
    Public Sub AddingLinksTheJournalLibraryRecordAndAddsAJournalOnce()
        Dim record As New JournalRecord With {.Name = "Open Psychology"}
        Dim manuscript As New Manuscript With {.Title = "Shortlisted"}

        Dim candidate As JournalCandidate = JournalShortlistService.Add(manuscript, "  open   psychology ", {record}, CandidateStatus.Preferred)
        Dim again As JournalCandidate = JournalShortlistService.Add(manuscript, "Open Psychology", {record})

        Assert.AreSame(candidate, again, "A journal is shortlisted once.")
        Assert.AreEqual("Open Psychology", candidate.JournalName, "The library's spelling is used.")
        Assert.AreEqual(record.Id, candidate.JournalId.Value)
        Assert.AreEqual(CandidateStatus.Preferred, candidate.Status)
        Assert.AreEqual(1, manuscript.JournalShortlist.Count)
        Assert.IsNull(JournalShortlistService.Add(manuscript, "Unlisted Journal", Nothing).JournalId, "A journal outside the library is kept by name.")
    End Sub

    <TestMethod>
    Public Sub TheNextJournalIsThePreferredOneNotYetTried()
        Dim manuscript As Manuscript = Rejected("First Journal")
        For Each item In {("First Journal", CandidateStatus.Preferred), ("Ruled Out Journal", CandidateStatus.RuledOut),
                          ("Considered Journal", CandidateStatus.Considering), ("Preferred Journal", CandidateStatus.Preferred),
                          ("Backup Journal", CandidateStatus.Backup)}
            JournalShortlistService.Add(manuscript, item.Item1, Nothing, item.Item2)
        Next

        Assert.AreEqual("Preferred Journal", JournalShortlistService.NextCandidate(manuscript).JournalName,
                        "Preferred first, skipping a journal already submitted to.")
        manuscript.JournalShortlist.Single(Function(item) item.JournalName = "Preferred Journal").Status = CandidateStatus.RuledOut
        Assert.AreEqual("Considered Journal", JournalShortlistService.NextCandidate(manuscript).JournalName, "Then Considering.")
        manuscript.JournalShortlist.Single(Function(item) item.JournalName = "Considered Journal").Status = CandidateStatus.RuledOut
        Assert.AreEqual("Backup Journal", JournalShortlistService.NextCandidate(manuscript).JournalName, "Then Backup; never a ruled-out journal.")

        Dim first As JournalCandidate = manuscript.JournalShortlist(0)
        Assert.AreEqual("Submitted Jan 5, 2026  ·  Desk rejected", JournalShortlistService.SubmissionText(manuscript, first, Today))
        Assert.AreEqual(String.Empty, JournalShortlistService.SubmissionText(manuscript, manuscript.JournalShortlist(4), Today))
    End Sub

    <TestMethod>
    Public Sub ARejectionOffersTheNextJournalWithoutChangingAnything()
        Dim manuscript As Manuscript = Rejected("First Journal")
        JournalShortlistService.Add(manuscript, "Second Journal", Nothing, CandidateStatus.Preferred)
        Dim before As String = Text.Json.JsonSerializer.Serialize(manuscript)

        Dim offer As RerouteOffer = JournalShortlistService.RerouteOfferFor(manuscript, Today)

        Assert.AreEqual("Second Journal", offer.NextCandidate.JournalName)
        Assert.AreEqual(SubmissionOutcome.DeskRejected, offer.Outcome)
        Assert.AreEqual(New DateTime(2026, 1, 12), offer.ClosedDate.Value)
        Assert.AreEqual(before, Text.Json.JsonSerializer.Serialize(manuscript), "An offer never changes the record.")

        JournalShortlistService.MakeTarget(manuscript, offer.NextCandidate)
        Assert.AreEqual("Second Journal", manuscript.TargetJournal)
        Assert.IsNull(JournalShortlistService.RerouteOfferFor(manuscript, Today), "No offer once it is the target journal.")

        Dim open As Manuscript = Rejected("First Journal")
        open.Submissions.Add(New JournalSubmission With {.JournalName = "Second Journal", .SubmittedDate = New DateTime(2026, 3, 1)})
        JournalShortlistService.Add(open, "Third Journal", Nothing)
        Assert.IsNull(JournalShortlistService.RerouteOfferFor(open, Today), "No offer while the latest submission is open.")

        Dim filed As Manuscript = Rejected("First Journal")
        filed.Location = ManuscriptLocation.FileDrawer
        JournalShortlistService.Add(filed, "Second Journal", Nothing)
        Assert.IsNull(JournalShortlistService.RerouteOfferFor(filed, Today), "No offer for filed work.")
    End Sub

    <TestMethod>
    Public Sub TheShortlistKeepsItsOrderAndCopiesIndependently()
        Dim manuscript As New Manuscript With {.Title = "Ordered"}
        Dim a As JournalCandidate = JournalShortlistService.Add(manuscript, "A", Nothing)
        Dim b As JournalCandidate = JournalShortlistService.Add(manuscript, "B", Nothing)
        Dim c As JournalCandidate = JournalShortlistService.Add(manuscript, "C", Nothing)
        c.Checks.Add("trust.known")

        JournalShortlistService.Move(manuscript, c, -1)
        JournalShortlistService.Move(manuscript, a, -1)
        CollectionAssert.AreEqual({"A", "C", "B"}, manuscript.JournalShortlist.Select(Function(item) item.JournalName).ToList(), "Moving past the top does nothing.")

        Dim clone As Manuscript = ManuscriptCloneService.CloneManuscript(manuscript)
        clone.JournalShortlist(1).Checks.Add("fit.scope")
        clone.JournalShortlist.RemoveAt(0)
        Assert.AreEqual(3, manuscript.JournalShortlist.Count, "Editing the working copy leaves the saved record alone.")
        Assert.AreEqual(1, c.Checks.Count)
    End Sub

    <TestMethod>
    Public Sub TheChecksAreAttributedAndSummarized()
        Dim ids As List(Of String) = JournalChoiceGuide.TrustChecks.Concat(JournalChoiceGuide.FitChecks).Select(Function(item) item.Id).ToList()
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Stored ids are unique.")
        Assert.AreEqual(7, JournalChoiceGuide.TrustChecks.Count)
        StringAssert.Contains(JournalChoiceGuide.Attribution, "Think. Check. Submit.")
        StringAssert.Contains(JournalChoiceGuide.Attribution, "CC BY 4.0")

        Dim candidate As New JournalCandidate With {.Checks = New List(Of String) From {"trust.known", "trust.fees", "fit.scope"}}
        Assert.AreEqual("2 of 7 trust checks  ·  1 of 6 fit checks", JournalChoiceGuide.Summary(candidate))
        Assert.AreEqual(String.Empty, JournalChoiceGuide.Summary(New JournalCandidate()))
    End Sub

    <TestMethod>
    Public Sub Schema9UpgradeChangesOnlyTheMarkerAndShortlistsRoundTrip()
        Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Schema9-" & Guid.NewGuid().ToString("N"))
        Try
            Dim current As String = Path.Combine(root, "current")
            Dim data As String = Path.Combine(current, "data")
            Directory.CreateDirectory(data)
            Dim schema As String = StorageMigrationService.SchemaFilePath(current)
            File.WriteAllText(schema, "{""SchemaVersion"":8}")
            Dim original As String = "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Before shortlists""}]"
            File.WriteAllText(Path.Combine(data, "manuscripts.json"), original)

            StorageMigrationService.EnsureCurrentStorage(current, Path.Combine(root, "legacy"), Path.Combine(root, "library"), Path.Combine(root, "legacy-library"))

            Assert.AreEqual(9, StorageMigrationService.ReadSchemaVersion(schema))
            Assert.AreEqual("{""SchemaVersion"":8}", File.ReadAllText(Path.Combine(data, "schema.v8.bak")))
            Assert.AreEqual(original, File.ReadAllText(Path.Combine(data, "manuscripts.json")))

            Dim repository As New ManuscriptRepository(data, Path.Combine(root, "library"))
            Dim loaded As List(Of Manuscript) = repository.Load()
            Assert.AreEqual(0, loaded.Single().JournalShortlist.Count)
            Dim candidate As JournalCandidate = JournalShortlistService.Add(loaded.Single(), "Open Psychology", Nothing, CandidateStatus.Backup)
            candidate.Notes = "Broad readership"
            candidate.Checks.Add("fit.audience")
            repository.Save(loaded)

            Dim reloaded As JournalCandidate = repository.Load().Single().JournalShortlist.Single()
            Assert.AreEqual(CandidateStatus.Backup, reloaded.Status)
            Assert.AreEqual("Broad readership", reloaded.Notes)
            CollectionAssert.AreEqual({"fit.audience"}, reloaded.Checks)
        Finally
            If Directory.Exists(root) Then Directory.Delete(root, True)
        End Try
    End Sub

    <TestMethod>
    Public Sub TheCandidateDialogShowsHistoryAndCollectsTheChecks()
        RunOnStaThread(
            Sub()
                Dim candidate As New JournalCandidate With {.JournalName = "Open Psychology", .Status = CandidateStatus.Preferred, .Notes = "Fits the scope", .Checks = New List(Of String) From {"trust.review"}}
                Using dialog As New JournalCandidateForm(candidate, {"Open Psychology", "Assessment"}, Function(name) "2 submissions · 1 accepted.")
                    ShowOffscreen(dialog)
                    Assert.IsTrue(dialog.CheckBoxes.Single(Function(box) CStr(box.Tag) = "trust.review").Checked)
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "Your history with this journal: 2 submissions · 1 accepted."))
                    Assert.IsTrue(Descendants(dialog).OfType(Of LinkLabel)().Any(Function(link) link.Text = JournalChoiceGuide.Attribution), "The adapted questions are attributed.")
                    dialog.CheckBoxes.Single(Function(box) CStr(box.Tag) = "fit.scope").Checked = True
                    dialog.JournalBox.Text = "Assessment"
                    dialog.AcceptForTest()
                    Assert.AreEqual("Assessment", dialog.JournalName)
                    Assert.AreEqual(CandidateStatus.Preferred, dialog.Status)
                    Assert.AreEqual("Fits the scope", dialog.Notes)
                    CollectionAssert.AreEquivalent({"trust.review", "fit.scope"}, dialog.Checks)
                    dialog.Close()
                End Using

                Using help As New HelpForm("Choosing a Journal")
                    ShowOffscreen(help)
                    Application.DoEvents()
                    StringAssert.StartsWith(help.GuideText.Substring(help.GuideSelectionStart), "Choosing a Journal", "Help opens at the section.")
                    help.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Function Rejected(journal As String) As Manuscript
        Dim manuscript As New Manuscript With {.Title = "Rerouted", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Draft, .TargetJournal = journal}
        Dim submission As New JournalSubmission With {.JournalName = journal, .SubmittedDate = New DateTime(2026, 1, 5)}
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
        manuscript.Submissions.Add(submission)
        Return manuscript
    End Function

    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As ExceptionDispatchInfo = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                End Try
            End Sub)
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        thread.Join()
        failure?.Throw()
    End Sub

End Class
