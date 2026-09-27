Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
<DoNotParallelize>
Public Class TitlePageApplyTests

    ' All names and institutions are fictional.
    Private Shared ReadOnly TitlePage As String = String.Join(vbLf, {
        "A preregistered replication of anchoring effects in clinical risk estimates",
        "",
        "Dana Whitfield1, Rui Okafor1,2*, and Lena Marsh2",
        "",
        "1 Department of Psychology, Blue Mountain University",
        "2 School of Nursing, Cascade State University",
        "",
        "Abstract",
        "Anchoring is among the most replicated effects in judgment research.",
        "",
        "Keywords: anchoring; clinical judgment; replication"
    })

    Private Const Psychology As String = "Department of Psychology, Blue Mountain University"
    Private Const Nursing As String = "School of Nursing, Cascade State University"


    <TestMethod>
    Public Sub Apply_ReusesMatchingLibraryRecordsAndCreatesOnlyNewOnes()

        Dim library As AuthorLibraryData = CreateLibrary()
        Dim existingAuthor As AuthorRecord = library.Authors.Single()
        Dim existingAffiliation As AffiliationRecord = library.Affiliations.Single()
        Dim manuscript As New Manuscript With {.Title = "Synthetic manuscript"}

        Dim result As TitlePageApplyResult =
            TitlePageApplyService.Apply(TitlePageParserService.Parse(TitlePage), manuscript, library)

        Assert.AreEqual(1, result.AuthorsMatched)
        Assert.AreEqual(2, result.AuthorsCreated)
        Assert.AreEqual(1, result.AffiliationsCreated)
        Assert.IsTrue(result.LibraryChanged)
        Assert.AreEqual(3, library.Authors.Count)
        Assert.AreEqual(2, library.Affiliations.Count)

        Dim names As List(Of String) =
            manuscript.Authors.
                Select(Function(link) library.Authors.Single(Function(author) author.Id = link.AuthorId).FamilyName).
                ToList()
        CollectionAssert.AreEqual({"Whitfield", "Okafor", "Marsh"}, names)
        Assert.AreEqual(existingAuthor.Id, manuscript.Authors(0).AuthorId)

        Dim nursingRecord As AffiliationRecord = library.Affiliations.Single(Function(item) item.Institution = Nursing)
        CollectionAssert.AreEqual({existingAffiliation.Id}, manuscript.Authors(0).AffiliationIds)
        CollectionAssert.AreEqual({existingAffiliation.Id, nursingRecord.Id}, manuscript.Authors(1).AffiliationIds)
        CollectionAssert.AreEqual({nursingRecord.Id}, manuscript.Authors(2).AffiliationIds)

        CollectionAssert.AreEqual({False, True, False}, manuscript.Authors.Select(Function(link) link.IsCorrespondingAuthor).ToList())
        Assert.AreEqual("Anchoring is among the most replicated effects in judgment research.", manuscript.Metadata.AbstractText)
        CollectionAssert.AreEqual({"anchoring", "clinical judgment", "replication"}, manuscript.Metadata.Keywords)

    End Sub

    <TestMethod>
    Public Sub Apply_FillsOnlyEmptyMetadataAndLinksEachAuthorOnce()

        Dim library As New AuthorLibraryData()
        Dim manuscript As New Manuscript With {.Title = "Synthetic manuscript"}
        manuscript.Metadata.AbstractText = "Existing abstract."
        manuscript.Metadata.Keywords.Add("Anchoring")

        Dim proposal As TitlePageParseResult = TitlePageParserService.Parse(TitlePage)
        proposal.Authors.Add(proposal.Authors(0))

        TitlePageApplyService.Apply(proposal, manuscript, library)

        Assert.AreEqual("Existing abstract.", manuscript.Metadata.AbstractText)
        CollectionAssert.AreEqual({"Anchoring", "clinical judgment", "replication"}, manuscript.Metadata.Keywords)
        Assert.AreEqual(3, manuscript.Authors.Count)

    End Sub

    <TestMethod>
    Public Sub Apply_WithEverythingAlreadyInTheLibrary_LeavesItUnchanged()

        Dim library As New AuthorLibraryData()
        TitlePageApplyService.Apply(TitlePageParserService.Parse(TitlePage), New Manuscript(), library)

        Dim result As TitlePageApplyResult =
            TitlePageApplyService.Apply(TitlePageParserService.Parse(TitlePage), New Manuscript(), library)

        Assert.AreEqual(3, result.AuthorsMatched)
        Assert.IsFalse(result.LibraryChanged)
        Assert.AreEqual(3, library.Authors.Count)
        Assert.AreEqual(2, library.Affiliations.Count)

    End Sub

    <TestMethod>
    Public Sub ImportDialog_ShowsLibraryMatchesAndReturnsTheEditedProposal()

        Using dialog As New TitlePageImportForm(CreateLibrary(), TitlePage)
            Dim grid As DataGridView = Descendants(dialog).OfType(Of DataGridView)().Single()

            Assert.AreEqual(3, grid.Rows.Count)
            CollectionAssert.AreEqual(
                {"In your library", "New author", "New author"},
                grid.Rows.Cast(Of DataGridViewRow)().Select(Function(row) CStr(row.Cells("Library").Value)).ToList())

            ' Leave out the third author and rename the second.
            grid.Rows(2).Cells("Use").Value = False
            grid.Rows(1).Cells("Author").Value = "Rui A. Okafor"

            InvokePrivate(dialog, "UseDetails")

            Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
            Dim reviewed As TitlePageParseResult = dialog.ReviewedProposal
            Assert.AreEqual(2, reviewed.Authors.Count)
            Assert.AreEqual("A.", reviewed.Authors(1).Name.MiddleName)
            Assert.IsTrue(reviewed.Authors(1).IsCorrespondingAuthor)
            CollectionAssert.AreEqual({Psychology, Nursing}, reviewed.Authors(1).Affiliations)
            CollectionAssert.AreEqual({"anchoring", "clinical judgment", "replication"}, reviewed.Keywords)
        End Using

    End Sub

    <TestMethod>
    Public Sub AddManuscript_AppliesTheProposalAndCreatesAnOrdinaryReminder()

        Dim library As AuthorLibraryData = CreateLibrary()

        Using dialog As New AddManuscriptForm(library)
            dialog.UseTitlePageProposal(TitlePageParserService.Parse(TitlePage))
            dialog.SetFirstDeadline("Send draft to coauthors", New DateTime(2026, 10, 10))

            InvokePrivate(dialog, "AddManuscript")

            Dim created As Manuscript = dialog.CreatedManuscript
            Assert.IsNotNull(created)
            Assert.AreEqual("A preregistered replication of anchoring effects in clinical risk estimates", created.Title)
            Assert.AreEqual(3, created.Authors.Count)
            Assert.AreEqual(1, created.History.Count)
            Assert.IsTrue(dialog.AuthorLibraryChanged)
            Assert.AreEqual(3, library.Authors.Count)

            Assert.AreEqual(1, created.Reminders.Count)
            Assert.AreEqual("Send draft to coauthors", created.Reminders(0).Title)
            Assert.AreEqual(New DateTime(2026, 10, 10), created.Reminders(0).DueDate)
            Assert.IsFalse(created.Reminders(0).IsCompleted)
        End Using

    End Sub

    <TestMethod>
    Public Sub AddManuscript_WithoutALibrary_StillAddsATitleOnlyManuscript()

        Using dialog As New AddManuscriptForm()
            Dim paste As Button =
                Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Paste a Title Page...")
            Assert.IsFalse(paste.Enabled)

            DirectCast(GetField(dialog, "txtTitle"), TextBox).Text = "Synthetic title-only manuscript"
            InvokePrivate(dialog, "AddManuscript")

            Assert.AreEqual("Synthetic title-only manuscript", dialog.CreatedManuscript.Title)
            Assert.AreEqual(0, dialog.CreatedManuscript.Authors.Count)
            Assert.AreEqual(0, dialog.CreatedManuscript.Reminders.Count)
            Assert.IsFalse(dialog.AuthorLibraryChanged)
        End Using

    End Sub


    Private Shared Function CreateLibrary() As AuthorLibraryData

        Dim library As New AuthorLibraryData()
        library.Authors.Add(New AuthorRecord With {.GivenName = "Dana", .FamilyName = "Whitfield"})
        library.Affiliations.Add(New AffiliationRecord With {.Institution = Psychology})
        Return library

    End Function

    Private Shared Sub InvokePrivate(target As Object, name As String)

        target.GetType().
            GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).
            Invoke(target, New Object() {Nothing, EventArgs.Empty})

    End Sub

    Private Shared Function GetField(target As Object, name As String) As Object

        Return target.GetType().
            GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).
            GetValue(target)

    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

End Class
