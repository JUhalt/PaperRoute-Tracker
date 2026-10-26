Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Library > Authors & Affiliations and the Journals page keep the record
' just added or edited selected, so Edit, ORCID... and Delete act on it
' and the facts card shows it, as the manuscript's own author and link
' lists do. Each dialog is answered as it opens; any message box fails
' the test by its title.
<TestClass>
<DoNotParallelize>
Public Class LibraryListSelectionTests

    Private _root As String = String.Empty

    ' The author library's default repository resolves its folder under
    ' the override, so nothing is read from or written to the profile.
    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = _root
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub

    ' Before the change nothing was selected after Add Author, and a
    ' delete cleared the selection in both lists.
    <TestMethod>
    Public Sub AddingOrEditingAnAuthorOrAffiliationSelectsIt()
        RunOnStaThread(
            Sub()
                Dim seed As New AuthorLibraryData()
                For index As Integer = 1 To 30
                    seed.Authors.Add(New AuthorRecord With {.GivenName = "Author", .FamilyName = index.ToString("00")})
                    seed.Affiliations.Add(New AffiliationRecord With {.Institution = "Affiliation " & index.ToString("00")})
                Next
                Dim repository As New AuthorLibraryRepository()
                repository.Save(seed)

                Using dialogs As New ManuscriptPageTests.DialogWatcher(answerYes:="Delete Author"),
                      form As New AuthorLibraryForm(New List(Of Manuscript)())

                    ViewCatalog.ShowOffscreen(form)
                    Dim tabs As UnderlineTabControl = ViewCatalog.Descendants(form).OfType(Of UnderlineTabControl)().Single()
                    Dim authors As ListBox = ListNamed(form, "Authors")
                    Dim affiliations As ListBox = ListNamed(form, "Affiliations")

                    ' Add Author.
                    tabs.SelectedTab = PageOf(authors)
                    Application.DoEvents()
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorEditForm)(
                        Sub(dialog)
                            FieldOf(Of TextBox)(dialog, "txtGiven").Text = "Zara"
                            FieldOf(Of TextBox)(dialog, "txtFamily").Text = "Zylstra"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(authors, "Add Author").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after Add Author.")

                    Dim zara As AuthorRecord = repository.Load().Authors.Single(Function(item) item.DisplayName = "Zara Zylstra")
                    Dim selectedAuthor As AuthorRecord = TryCast(authors.SelectedItem, AuthorRecord)
                    Assert.IsNotNull(selectedAuthor, "The added author is selected.")
                    Assert.AreEqual(zara.Id, selectedAuthor.Id)
                    AssertVisible(authors)
                    Dim accessible As AccessibleObject = authors.AccessibilityObject.GetChild(authors.SelectedIndex)
                    Assert.AreEqual("Zara Zylstra", accessible.Name)
                    Assert.IsTrue((accessible.State And AccessibleStates.Selected) = AccessibleStates.Selected, "A screen reader finds it selected.")

                    ' Edit an author, marking it This is me.
                    Dim fifth As AuthorRecord = authors.Items.Cast(Of AuthorRecord)().Single(Function(record) record.DisplayName = "Author 05")
                    authors.SelectedIndex = authors.Items.IndexOf(fifth)
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorEditForm)(
                        Sub(dialog)
                            FieldOf(Of CheckBox)(dialog, "chkMe").Checked = True
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(authors, "Edit").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after Edit.")

                    selectedAuthor = TryCast(authors.SelectedItem, AuthorRecord)
                    Assert.IsNotNull(selectedAuthor, "The edited author stays selected.")
                    Assert.AreEqual(fifth.Id, selectedAuthor.Id)
                    Assert.AreEqual("Author 05  •  Me", authors.GetItemText(authors.SelectedItem))

                    ' Add Affiliation, then edit it.
                    tabs.SelectedTab = PageOf(affiliations)
                    Application.DoEvents()
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AffiliationEditForm)(
                        Sub(dialog)
                            FieldOf(Of TextBox)(dialog, "txtInstitution").Text = "Zenith University"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(affiliations, "Add Affiliation").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after Add Affiliation.")

                    Dim zenith As AffiliationRecord = repository.Load().Affiliations.Single(Function(record) record.Institution = "Zenith University")
                    Dim selectedAffiliation As AffiliationRecord = TryCast(affiliations.SelectedItem, AffiliationRecord)
                    Assert.IsNotNull(selectedAffiliation, "The added affiliation is selected.")
                    Assert.AreEqual(zenith.Id, selectedAffiliation.Id)
                    AssertVisible(affiliations)

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AffiliationEditForm)(
                        Sub(dialog)
                            FieldOf(Of TextBox)(dialog, "txtDepartment").Text = "Biology"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(affiliations, "Edit").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after editing the affiliation.")

                    selectedAffiliation = TryCast(affiliations.SelectedItem, AffiliationRecord)
                    Assert.IsNotNull(selectedAffiliation, "The edited affiliation stays selected.")
                    Assert.AreEqual(zenith.Id, selectedAffiliation.Id)
                    Assert.AreEqual("Biology", selectedAffiliation.Department)

                    ' Delete the author no manuscript uses: that list ends with
                    ' nothing selected, and the other keeps its selection.
                    tabs.SelectedTab = PageOf(authors)
                    Application.DoEvents()
                    Assert.AreEqual(fifth.Id, DirectCast(authors.SelectedItem, AuthorRecord).Id, "Author 05 is still selected.")
                    Command(authors, "Delete").PerformClick()
                    Application.DoEvents()

                    CollectionAssert.AreEqual({"Delete Author"}, dialogs.Closed)
                    Assert.IsFalse(repository.Load().Authors.Any(Function(record) record.Id = fifth.Id), "Author 05 is deleted.")
                    Assert.AreEqual(-1, authors.SelectedIndex, "Nothing is selected in place of the deleted author.")
                    selectedAffiliation = TryCast(affiliations.SelectedItem, AffiliationRecord)
                    Assert.IsNotNull(selectedAffiliation, "The Affiliations list keeps its selection.")
                    Assert.AreEqual(zenith.Id, selectedAffiliation.Id)

                    form.Close()
                End Using
            End Sub)
    End Sub

    ' Before the change Journal 01 stayed selected and shown after Add
    ' Journal. Edit already kept the edited journal selected; that part is
    ' a guard and passes before the change too.
    <TestMethod>
    Public Sub AddingAJournalSelectsItAndShowsItsFacts()
        RunOnStaThread(
            Sub()
                Dim repository As New AuthorLibraryRepository(Path.Combine(_root, "data"))
                Dim seed As New AuthorLibraryData()
                For index As Integer = 1 To 40
                    seed.Journals.Add(New JournalRecord With {.Name = "Journal " & index.ToString("00")})
                Next
                repository.Save(seed)
                Dim first As JournalRecord = seed.Journals(0)
                Dim twentieth As JournalRecord = seed.Journals(19)

                Using dialogs As New ManuscriptPageTests.DialogWatcher(),
                      page As New JournalLibraryForm(Enumerable.Empty(Of Manuscript)(), repository, Nothing)

                    ViewCatalog.ShowOffscreen(page)
                    Dim journals As ListBox = ListNamed(page, "Journals")
                    page.SelectJournal(first.Id)
                    Application.DoEvents()

                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of JournalEditForm)(
                        Sub(dialog)
                            FieldOf(Of TextBox)(dialog, "txtName").Text = "Zoology Letters"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(page, "Add Journal").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after Add Journal.")

                    Dim zoology As JournalRecord = repository.Load().Journals.Single(Function(record) record.Name = "Zoology Letters")
                    Assert.AreEqual("Zoology Letters", page.SelectedJournal.Name, "The added journal is selected.")
                    Assert.AreEqual(zoology.Id, page.SelectedJournal.Id)
                    AssertVisible(journals)
                    StringAssert.Contains(page.Card.ShownText, "Zoology Letters")

                    ' Guard: Edit already kept the edited journal selected.
                    page.SelectJournal(twentieth.Id)
                    Application.DoEvents()
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of JournalEditForm)(
                        Sub(dialog)
                            FieldOf(Of TextBox)(dialog, "txtName").Text = "Aardvark Review"
                            DirectCast(dialog.AcceptButton, Button).PerformClick()
                        End Sub)
                        Command(page, "Edit").PerformClick()
                    End Using
                    Application.DoEvents()
                    CollectionAssert.AreEqual(Array.Empty(Of String)(), dialogs.Closed, "No message box after Edit.")

                    Assert.AreEqual(twentieth.Id, page.SelectedJournal.Id, "The edited journal stays selected.")
                    Assert.AreEqual(0, journals.SelectedIndex, "Renamed, it sorts first.")
                    AssertVisible(journals)
                    StringAssert.Contains(page.Card.ShownText, "Aardvark Review")

                    page.Close()
                End Using
            End Sub)
    End Sub

    ' The list scrolls, and its selected item is in view.
    Private Shared Sub AssertVisible(list As ListBox)
        Dim rows As Integer = list.ClientSize.Height \ list.ItemHeight
        Assert.IsTrue(list.Items.Count > rows, "The list holds more than it shows (" & list.Items.Count.ToString() & " items, " & rows.ToString() & " rows), so it scrolls.")
        Assert.IsTrue(
            list.TopIndex <= list.SelectedIndex AndAlso list.SelectedIndex < list.TopIndex + rows,
            "Item " & list.SelectedIndex.ToString() & " is in view: rows " & list.TopIndex.ToString() & " to " & (list.TopIndex + rows - 1).ToString() & ".")
    End Sub

    Private Shared Function ListNamed(form As Form, name As String) As ListBox
        Return ViewCatalog.Descendants(form).OfType(Of ListBox)().Single(Function(list) list.AccessibleName = name)
    End Function

    ' The tab a list is on; its own Edit and Delete are there.
    Private Shared Function PageOf(list As ListBox) As TabPage
        Dim parent As Control = list.Parent
        While parent IsNot Nothing AndAlso TypeOf parent IsNot TabPage
            parent = parent.Parent
        End While
        Return DirectCast(parent, TabPage)
    End Function

    ' A list's command: the button on its own tab, which a tab hidden
    ' behind another would not press.
    Private Shared Function Command(list As ListBox, text As String) As Button
        Return Command(PageOf(list), text)
    End Function

    Private Shared Function Command(container As Control, text As String) As Button
        Return ViewCatalog.Descendants(container).OfType(Of Button)().Single(Function(button) button.Text = text)
    End Function

    Private Shared Function FieldOf(Of T)(dialog As Form, name As String) As T
        Return DirectCast(dialog.GetType().GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), T)
    End Function

End Class
