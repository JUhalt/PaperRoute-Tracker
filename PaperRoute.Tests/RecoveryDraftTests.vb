Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A recovery draft of the open manuscript page (#123): written when
' PaperRoute closes without being able to ask, offered at the next start,
' and never saved into the library until the user saves.
<TestClass>
<DoNotParallelize>
Public Class RecoveryDraftTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedDirectory As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()
        _dataDirectory = Path.Combine(_root, "data")
        _managedDirectory = Path.Combine(_root, "managed")

        Dim repository As New ManuscriptRepository(_dataDirectory, _managedDirectory)
        repository.Save(New List(Of Manuscript) From {Sample("Saved title")})

    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' Windows shutting down closes the window with no one to answer a
    ' prompt: the open page's working copy goes to data\recovery instead,
    ' and manuscripts.json is untouched.
    <TestMethod>
    Public Sub ShutdownWithUnsavedChangesWritesTheDraftAndDoesNotPrompt()
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the shutdown"
                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

                    Dim closing As New FormClosingEventArgs(CloseReason.WindowsShutDown, False)
                    board.SimulateClosing(closing)

                    Assert.AreEqual(0, asked, "Shutdown is not held up by a prompt.")
                    Assert.IsFalse(closing.Cancel)
                    Assert.IsTrue(File.Exists(DraftPath), "The working copy was written to data\recovery.")
                    StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the shutdown")
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath), "manuscripts.json is untouched.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The next start offers the draft. Opening it shows the page with the
    ' recovered copy as unsaved changes; the file has served and goes, and
    ' manuscripts.json is byte-identical until the user saves.
    <TestMethod>
    Public Sub TheNextStartOffersTheDraftAndOpeningItShowsThePageWithUnsavedChanges()
        LeaveADraft("Edited before the crash")
        Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Dim offered As String = Nothing
                    board.SetDraftPrompt(Function(title)
                                             offered = title
                                             Return DialogResult.Yes
                                         End Function)

                    board.Offer()

                    Assert.AreEqual("Saved title", offered, "The offer names the manuscript as saved.")
                    Assert.AreEqual("Manuscript", board.PageName, "Accepting opens the manuscript page.")
                    Assert.AreEqual("Edited before the crash", board.Field("Title").Text, "With the recovered copy.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    board.Tick()
                    Assert.IsTrue(board.SaveBarVisible, "The Unsaved changes bar shows.")
                    Assert.IsFalse(File.Exists(DraftPath), "The draft now lives in the page.")
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath), "manuscripts.json is byte-identical until the user saves.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    board.Close()
                End Using
            End Sub)

        Assert.AreEqual("Edited before the crash", New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title, "Save writes the recovered changes.")
    End Sub


    <TestMethod>
    Public Sub DiscardingTheOfferDeletesTheDraftAndLeavesTheLibraryUntouched()
        LeaveADraft("Edited before the crash")
        Dim before As Byte() = File.ReadAllBytes(ManuscriptsPath)

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title) DialogResult.No)

                    board.Offer()

                    Assert.AreEqual("Board", board.PageName)
                    Assert.IsFalse(File.Exists(DraftPath), "Discarding deletes the file.")
                    board.Close()
                End Using
            End Sub)

        CollectionAssert.AreEqual(before, File.ReadAllBytes(ManuscriptsPath))
        Assert.AreEqual("Saved title", New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title)
    End Sub


    ' The error dialog's fatal path calls this before PaperRoute ends. With
    ' nothing unsaved there is nothing to write.
    <TestMethod>
    Public Sub AnErrorEndingTheSessionWritesTheDraftOnlyWhenThePageHasUnsavedChanges()
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Assert.IsFalse(board.WriteDraft("an error"), "No page is open.")
                    board.Open(board.Library(0))
                    Assert.IsFalse(board.WriteDraft("an error"), "The page has no unsaved changes.")
                    Assert.IsFalse(File.Exists(DraftPath))

                    board.Field("Title").Text = "Edited before the error"
                    Assert.IsTrue(board.WriteDraft("an error"))
                    StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the error")
                    StringAssert.Contains(File.ReadAllText(DraftPath), "an error")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The UI-thread error handler (#121) writes the open page's unsaved
    ' edits to the draft before it shows anything, so they are offered at
    ' the next start whatever follows; Keep Working then leaves the page as
    ' it was. A close the user then answers lets the edits go, and the draft
    ' with them. The real handler runs, its error report under this test's
    ' root so the user's profile is never touched.
    <TestMethod>
    Public Sub TheErrorHandlerWritesTheDraftBeforeItShowsTheDialog()
        Dim draftWhenAsked As Boolean = False
        Dim e As New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The route map could not be drawn."))

        StorageEnvironment.RootOverride = _root
        Try
            RunOnStaThread(
                Sub()
                    Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                        board.Prepare()
                        board.Open(board.Library(0))
                        board.Field("Title").Text = "Edited before the error"
                        board.SetErrorPrompt(Function(failure, unsaved)
                                                 draftWhenAsked = File.Exists(DraftPath)
                                                 Return DialogResult.Cancel
                                             End Function)

                        RaiseUnhandledError(board, e)

                        Assert.IsFalse(board.IsDisposed, "Keep Working goes on.")
                        Assert.AreEqual("Edited before the error", board.Field("Title").Text, "The page is intact.")
                        StringAssert.Contains(File.ReadAllText(DraftPath), "Edited before the error")
                        StringAssert.Contains(File.ReadAllText(DraftPath), "an error ended the session")

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                End Sub)

            Assert.AreEqual(1, Directory.GetFiles(ErrorReportService.DefaultDirectory(), "error-report_*").Length, "The report comes first (#120).")
        Finally
            StorageEnvironment.RootOverride = Nothing
        End Try

        Assert.IsTrue(draftWhenAsked, "The draft was there before the dialog was shown.")
        Assert.IsFalse(e.ExitApplication)
        Assert.IsFalse(File.Exists(DraftPath), "Discarding at the close lets the draft go; the next start offers nothing.")
    End Sub


    ' After an error and Keep Working, the draft holds the page's edits only
    ' until they are saved or discarded; then it goes, and the next start
    ' offers nothing. Before the change it outlived the session, and the
    ' next start offered the stale edits over the saved manuscript.
    <TestMethod>
    <DataRow("Save")>
    <DataRow("Discard")>
    Public Sub AfterAnErrorTheDraftGoesOnceThePageIsSavedOrDiscarded(resolution As String)
        StorageEnvironment.RootOverride = _root
        Try
            RunOnStaThread(
                Sub()
                    Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                        board.Prepare()
                        board.Open(board.Library(0))
                        board.Field("Title").Text = "Edited before the error"
                        board.SetErrorPrompt(Function(failure, unsaved) DialogResult.Cancel)

                        RaiseUnhandledError(board, New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The route map could not be drawn.")))
                        Assert.IsTrue(File.Exists(DraftPath), "The error wrote the draft.")

                        board.Field("Title").Text = "Edited after the error"

                        If resolution = "Save" Then
                            board.PressCommandKey(Keys.Control Or Keys.S)
                        Else
                            board.Discard()
                        End If

                        Assert.IsFalse(board.Editor.HasUnsavedChanges())
                        Assert.IsFalse(File.Exists(DraftPath), "Nothing is left to recover after " & resolution & ".")

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                End Sub)
        Finally
            StorageEnvironment.RootOverride = Nothing
        End Try

        Dim offered As Integer = 0

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title)
                                             offered += 1
                                             Return DialogResult.Yes
                                         End Function)
                    board.Offer()
                    Assert.AreEqual("Board", board.PageName)
                    board.Close()
                End Using
            End Sub)

        Assert.AreEqual(0, offered, "The next start offers nothing.")
        Assert.AreEqual(
            If(resolution = "Save", "Edited after the error", "Saved title"),
            New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title)
    End Sub


    ' A restore hands over to a new session (#116) with the library it
    ' replaced still in memory: a draft this session wrote of that library
    ' would be offered over the restored one at the next start, so the
    ' hand-over takes it, still without asking.
    <TestMethod>
    Public Sub AHandOverAfterARestoreTakesTheSessionsDraftWithIt()
        Dim asked As Integer = 0

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the restore"
                    Assert.IsTrue(board.WriteDraft("an error ended the session"))
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)

                    board.HandOver()
                    board.Close()
                    Assert.IsTrue(board.IsDisposed, "The hand-over close goes through.")
                End Using
            End Sub)

        Assert.AreEqual(0, asked)
        Assert.IsFalse(File.Exists(DraftPath))
    End Sub


    ' A draft that cannot be written (data\recovery is a file here) does
    ' not keep the error dialog from being shown. A guard: it passed before
    ' the handler wrote any draft, and it proves only that a failed write
    ' never suppresses the dialog, since WriteRecoveryDraft catches its own
    ' write failures before the handler's Try sees them.
    <TestMethod>
    Public Sub ADraftThatCannotBeWrittenDoesNotHideTheErrorDialog()
        Dim asked As Integer = 0
        File.WriteAllText(Path.Combine(_dataDirectory, "recovery"), "in the way")

        StorageEnvironment.RootOverride = _root
        Try
            RunOnStaThread(
                Sub()
                    Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                        board.Prepare()
                        board.Open(board.Library(0))
                        board.Field("Title").Text = "Edited before the error"
                        board.SetErrorPrompt(Function(failure, unsaved)
                                                 asked += 1
                                                 Return DialogResult.Cancel
                                             End Function)

                        RaiseUnhandledError(board, New Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs(True, New InvalidOperationException("The route map could not be drawn.")))

                        board.SetPrompt(Function(title) DialogResult.No)
                        board.Close()
                    End Using
                End Sub)
        Finally
            StorageEnvironment.RootOverride = Nothing
        End Try

        Assert.AreEqual(1, asked, "The error dialog is still shown.")
        Assert.IsFalse(File.Exists(DraftPath))
    End Sub


    ' The handler as the application framework calls it, with the board as
    ' the one main window open. A session's window is listed in
    ' Application.OpenForms by Form.OnLoad, which the board skips with
    ' Form1's own load, so it is listed here for the call and unlisted
    ' after, whatever happens.
    Private Shared Sub RaiseUnhandledError(board As Form1, e As Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs)
        Dim openForms As FormCollection = Application.OpenForms
        Dim list As MethodInfo = GetType(FormCollection).GetMethod("Add", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, {GetType(Form)}, Nothing)
        Dim unlist As MethodInfo = GetType(FormCollection).GetMethod("Remove", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, {GetType(Form)}, Nothing)

        list.Invoke(openForms, New Object() {board})
        Try
            Assert.AreSame(board, openForms.OfType(Of Form1)().Single(), "The board is the one main window open.")
            ' Not named application: that would hide Application above.
            Dim host As New ManuscriptPipeline.My.MyApplication()
            GetType(ManuscriptPipeline.My.MyApplication).GetMethod("MyApplication_UnhandledException", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(host, New Object() {host, e})
            Application.DoEvents()
        Finally
            unlist.Invoke(openForms, New Object() {board})
        End Try
    End Sub


    ' A draft for a manuscript no longer in the library cannot be opened; it
    ' is neither offered nor deleted.
    <TestMethod>
    Public Sub ADraftForAManuscriptNoLongerInTheLibraryIsLeftAlone()
        Dim store As New RecoveryDraftStore(_dataDirectory)
        store.Write(New RecoveryDraft With {.ManuscriptId = Guid.NewGuid(), .WrittenAtUtc = DateTime.UtcNow, .Reason = "test", .Manuscript = Sample("Gone")})

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    Dim asked As Boolean = False
                    board.SetDraftPrompt(Function(title)
                                             asked = True
                                             Return DialogResult.No
                                         End Function)
                    board.Offer()
                    Assert.IsFalse(asked)
                    Assert.AreEqual("Board", board.PageName)
                    board.Close()
                End Using
            End Sub)

        Assert.IsTrue(File.Exists(DraftPath))
    End Sub


    ' A draft's library records with one id twice, one with no id and one
    ' missing (#361): opening it adds the one person once, and Save writes
    ' only that person.
    <TestMethod>
    Public Sub ADraftsRepeatedEmptyAndMissingPeopleAddOnlyOneRecord()
        Dim person As New AuthorRecord With {.GivenName = "Carried", .FamilyName = "Person"}
        Dim repeated As New AuthorRecord With {.Id = person.Id, .GivenName = "Carried", .FamilyName = "Twice"}
        Dim noId As New AuthorRecord With {.Id = Guid.Empty, .GivenName = "No", .FamilyName = "Id"}
        LeaveADraft("Edited before the crash", New RecoveryDraftLibrary With {.Authors = New List(Of AuthorRecord) From {person, repeated, noId, Nothing}})
        Assert.AreEqual(4, New RecoveryDraftStore(_dataDirectory).Read().UnsavedLibrary.Authors.Count, "The draft holds all four entries.")

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title) DialogResult.Yes)
                    board.Offer()

                    Assert.AreEqual("Manuscript", board.PageName, "Accepting opens the manuscript page.")
                    Dim people As List(Of AuthorRecord) = PageLibrary(board.Editor).Authors
                    Assert.AreEqual(1, people.Count, "One record is added.")
                    Assert.AreEqual(person.Id, people(0).Id)
                    Assert.AreEqual("Person", people(0).FamilyName, "The first record with the id is the one added.")
                    Assert.IsTrue(LibraryDirty(board.Editor), "The added record waits for Save.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    board.Close()
                End Using
            End Sub)

        Dim saved As AuthorLibraryData = AuthorStore.Load()
        Assert.AreEqual(1, saved.Authors.Count, "Save writes the one person.")
        Assert.AreEqual(person.Id, saved.Authors(0).Id)
    End Sub


    ' A page that changed nothing in the author library writes a draft with
    ' exactly v1.0.0's four keys (#361).
    <TestMethod>
    Public Sub ADraftWithoutLibraryChangesHasTheKeysV100Wrote()
        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.Open(board.Library(0))
                    board.Field("Title").Text = "Edited before the shutdown"
                    Assert.IsTrue(board.WriteDraft("Windows was shutting down"), "The draft is written.")

                    Dim json As String = File.ReadAllText(DraftPath)
                    Assert.IsFalse(json.Contains("UnsavedLibrary"), "No library records are written.")
                    Using document As JsonDocument = JsonDocument.Parse(json)
                        CollectionAssert.AreEqual(
                            New List(Of String) From {"ManuscriptId", "WrittenAtUtc", "Reason", "Manuscript"},
                            document.RootElement.EnumerateObject().Select(Function(item) item.Name).ToList(),
                            "The draft has v1.0.0's keys and no others.")
                    End Using

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' The library records survive the store's Write and Read (#361), the
    ' ORCID check time in UTC included.
    <TestMethod>
    Public Sub ADraftsLibraryRecordsSurviveWriteAndRead()
        Dim checkedAt As New DateTime(2026, 11, 1, 12, 34, 56, DateTimeKind.Utc)
        Dim affiliation As New AffiliationRecord With {.Institution = "Example Institute", .Department = "Physics", .City = "Springfield", .Region = "IL", .Country = "USA", .Notes = "Lab"}
        Dim person As New AuthorRecord With {
            .GivenName = "Ada", .MiddleName = "B", .FamilyName = "Lookup", .Suffix = "Jr", .DisplayNameOverride = "A. Lookup",
            .Orcid = "0000-0002-1825-0097", .OrcidLastCheckedUtc = checkedAt, .Notes = "Found by Crossref"}
        LeaveADraft("Edited", New RecoveryDraftLibrary With {
            .Authors = New List(Of AuthorRecord) From {person},
            .Affiliations = New List(Of AffiliationRecord) From {affiliation},
            .TagColors = New List(Of TagColor) From {New TagColor With {.Tag = "teaching", .Color = TagPalette.Violet}}})

        Dim read As RecoveryDraftLibrary = New RecoveryDraftStore(_dataDirectory).Read().UnsavedLibrary

        Assert.IsNotNull(read)
        Dim author As AuthorRecord = read.Authors.Single()
        Assert.AreEqual(person.Id, author.Id)
        Assert.AreEqual("Ada", author.GivenName)
        Assert.AreEqual("B", author.MiddleName)
        Assert.AreEqual("Lookup", author.FamilyName)
        Assert.AreEqual("Jr", author.Suffix)
        Assert.AreEqual("A. Lookup", author.DisplayNameOverride)
        Assert.AreEqual("0000-0002-1825-0097", author.Orcid)
        Assert.AreEqual(checkedAt, author.OrcidLastCheckedUtc.Value, "The check time is kept.")
        Assert.AreEqual(DateTimeKind.Utc, author.OrcidLastCheckedUtc.Value.Kind, "The check time stays in UTC.")
        Assert.AreEqual("Found by Crossref", author.Notes)
        Dim place As AffiliationRecord = read.Affiliations.Single()
        Assert.AreEqual(affiliation.Id, place.Id)
        Assert.AreEqual("Example Institute", place.Institution)
        Assert.AreEqual("Physics", place.Department)
        Assert.AreEqual("Springfield", place.City)
        Assert.AreEqual("IL", place.Region)
        Assert.AreEqual("USA", place.Country)
        Assert.AreEqual("Lab", place.Notes)
        Dim colour As TagColor = read.TagColors.Single()
        Assert.AreEqual("teaching", colour.Tag)
        Assert.AreEqual(TagPalette.Violet, colour.Color)
    End Sub


    ' A draft as v1.0.0 wrote it (#361): written here through v1.0.0's own
    ' four-property class and options. It reads with no library records,
    ' opens, and Save leaves authors.json byte-identical.
    <TestMethod>
    Public Sub AV100DraftOpensAndSaveLeavesTheAuthorLibraryAlone()
        Dim seed As New AuthorLibraryData()
        seed.Authors.Add(New AuthorRecord With {.GivenName = "Disk", .FamilyName = "Person"})
        AuthorStore.Save(seed)
        Dim authorsBefore As Byte() = File.ReadAllBytes(AuthorStore.DataFilePath)

        Dim edited As Manuscript = New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single()
        edited.Title = "Edited under v1.0.0"
        Directory.CreateDirectory(Path.GetDirectoryName(DraftPath))
        Using stream As New FileStream(DraftPath, FileMode.Create, FileAccess.Write)
            CalendarDateJson.Write(
                stream,
                JsonSerializer.Serialize(
                    New V100RecoveryDraft With {.ManuscriptId = edited.Id, .WrittenAtUtc = DateTime.UtcNow, .Reason = "Windows was shutting down", .Manuscript = edited},
                    V100Options()))
        End Using

        Dim read As RecoveryDraft = New RecoveryDraftStore(_dataDirectory).Read()
        Assert.IsNotNull(read, "The v1.0.0 draft reads.")
        Assert.IsNull(read.UnsavedLibrary, "It carries no library records.")

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title) DialogResult.Yes)
                    board.Offer()

                    Assert.AreEqual("Edited under v1.0.0", board.Field("Title").Text, "The recovered copy opens.")
                    Assert.AreEqual(1, PageLibrary(board.Editor).Authors.Count, "The page's library is the one on disk.")
                    Assert.IsFalse(LibraryDirty(board.Editor), "Nothing in the library waits for Save.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    board.Close()
                End Using
            End Sub)

        Assert.AreEqual("Edited under v1.0.0", New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single().Title, "Save writes the recovered changes.")
        CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(AuthorStore.DataFilePath), "authors.json is byte-identical.")
    End Sub


    ' Library records whose lists are JSON null (#361), as a hand-edited
    ' file could hold: the draft reads and opens without an error and adds
    ' nothing.
    <TestMethod>
    Public Sub ADraftWithNullLibraryListsOpensAndAddsNothing()
        Dim seed As New AuthorLibraryData()
        seed.Authors.Add(New AuthorRecord With {.GivenName = "Disk", .FamilyName = "Person"})
        seed.Affiliations.Add(New AffiliationRecord With {.Institution = "Disk University"})
        AuthorStore.Save(seed)
        LeaveADraft("Edited before the crash")
        Dim json As String = File.ReadAllText(DraftPath)
        Dim opening As Integer = json.IndexOf("{"c)
        File.WriteAllText(DraftPath, json.Substring(0, opening + 1) & """UnsavedLibrary"": {""Authors"": null, ""Affiliations"": null, ""TagColors"": null}," & json.Substring(opening + 1))

        Dim read As RecoveryDraft = New RecoveryDraftStore(_dataDirectory).Read()
        Assert.IsNotNull(read?.UnsavedLibrary, "The draft reads with its library records.")
        Assert.IsNull(read.UnsavedLibrary.Authors, "The lists are null as written.")

        RunOnStaThread(
            Sub()
                Using board As New DraftBoard(_dataDirectory, _managedDirectory)
                    board.Prepare()
                    board.SetDraftPrompt(Function(title) DialogResult.Yes)
                    board.Offer()

                    Assert.AreEqual("Manuscript", board.PageName, "Accepting opens the manuscript page.")
                    Assert.AreEqual(1, PageLibrary(board.Editor).Authors.Count, "No person is added.")
                    Assert.AreEqual(1, PageLibrary(board.Editor).Affiliations.Count, "No affiliation is added.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    board.Close()
                End Using
            End Sub)

        Assert.AreEqual(1, AuthorStore.Load().Authors.Count, "authors.json holds the one person.")
        Assert.AreEqual(1, AuthorStore.Load().Affiliations.Count, "authors.json holds the one affiliation.")
    End Sub


    ' v1.0.0 reading a 1.0.1 draft that carries library records (#361):
    ' the unknown key is skipped and the manuscript is intact, so v1.0.0
    ' recovers the manuscript and loses only the people.
    <TestMethod>
    Public Sub V100ReadsADraftWithLibraryRecords()
        LeaveADraft("Edited before the crash", New RecoveryDraftLibrary With {
            .Authors = New List(Of AuthorRecord) From {New AuthorRecord With {.GivenName = "Ada", .FamilyName = "Lookup"}}})
        StringAssert.Contains(File.ReadAllText(DraftPath), "UnsavedLibrary")
        Dim expected As RecoveryDraft = New RecoveryDraftStore(_dataDirectory).Read()

        Dim old As V100RecoveryDraft
        Using stream As Stream = File.OpenRead(DraftPath)
            old = JsonSerializer.Deserialize(Of V100RecoveryDraft)(CalendarDateJson.Read(stream), V100Options())
        End Using

        Assert.IsNotNull(old)
        Assert.AreEqual(expected.ManuscriptId, old.ManuscriptId)
        Assert.IsNotNull(old.Manuscript)
        Assert.AreEqual(expected.Manuscript.Id, old.Manuscript.Id)
        Assert.AreEqual("Edited before the crash", old.Manuscript.Title)
    End Sub


    ' Guard: the window tests call OfferRecoveryDraft directly, so the one
    ' call that wires it into startup, after the board is rendered, is
    ' checked in the source, as the online gate's wiring is. It fails only
    ' if that call is removed; it passed before any change here.
    <TestMethod>
    Public Sub StartupOffersTheDraftAfterTheBoardIsRendered()
        Dim startup As String = File.ReadAllText(Path.Combine(TestRepository.Root(), "ManuscriptPipeline", "Form1.vb"))
        Dim loading As Integer = startup.IndexOf("Private Sub Form1_Load(", StringComparison.Ordinal)
        Dim rendering As Integer = startup.IndexOf("RenderManuscripts()", loading, StringComparison.Ordinal)
        Dim offering As Integer = startup.IndexOf("OfferRecoveryDraft()", loading, StringComparison.Ordinal)
        Assert.IsTrue(loading >= 0 AndAlso rendering > loading AndAlso offering > rendering, "Startup offers the draft once the board is rendered.")
    End Sub


    ' A session edited the one manuscript and ended without asking, so its
    ' draft is on disk. Written through the store: a session's own close
    ' removes the draft it wrote.
    Private Sub LeaveADraft(editedTitle As String, Optional library As RecoveryDraftLibrary = Nothing)
        Dim edited As Manuscript = New ManuscriptRepository(_dataDirectory, _managedDirectory).Load().Single()
        edited.Title = editedTitle

        Call New RecoveryDraftStore(_dataDirectory).Write(
            New RecoveryDraft With {
                .ManuscriptId = edited.Id,
                .WrittenAtUtc = DateTime.UtcNow,
                .Reason = "test",
                .Manuscript = edited,
                .UnsavedLibrary = library
            })
    End Sub


    ' The author library the window's page reads and saves.
    Private ReadOnly Property AuthorStore As AuthorLibraryRepository
        Get
            Return New AuthorLibraryRepository(_dataDirectory)
        End Get
    End Property


    ' The page's copy of the reusable author library.
    Private Shared Function PageLibrary(editor As EditManuscriptForm) As AuthorLibraryData
        Return DirectCast(GetType(EditManuscriptForm).GetProperty("_authorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), AuthorLibraryData)
    End Function


    Private Shared Function LibraryDirty(editor As EditManuscriptForm) As Boolean
        Return CBool(GetType(EditManuscriptForm).GetProperty("_authorLibraryDirty", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor))
    End Function


    ' The draft as v1.0.0 declared it: four properties, read and written
    ' with v1.0.0's options. RecoveryDraftStore was unchanged from v1.0.0
    ' until #361 added UnsavedLibrary.
    Public NotInheritable Class V100RecoveryDraft
        Public Property ManuscriptId As Guid
        Public Property WrittenAtUtc As DateTime
        Public Property Reason As String = String.Empty
        Public Property Manuscript As Manuscript
    End Class


    Private Shared Function V100Options() As JsonSerializerOptions
        Dim options As New JsonSerializerOptions With {
            .WriteIndented = True,
            .IgnoreReadOnlyProperties = True,
            .PropertyNameCaseInsensitive = True
        }
        options.Converters.Add(New JsonStringEnumConverter())
        Return options
    End Function


    Private ReadOnly Property ManuscriptsPath As String
        Get
            Return Path.Combine(_dataDirectory, "manuscripts.json")
        End Get
    End Property


    Private ReadOnly Property DraftPath As String
        Get
            Return Path.Combine(_dataDirectory, "recovery", "manuscript-page-draft.json")
        End Get
    End Property


    Private Shared Function Sample(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function


    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function


    ' The real main window on a temporary library: its own load, save and
    ' manuscript page, so a draft is written and read as in a session.
    Private NotInheritable Class DraftBoard
        Inherits Form1

        Public Sub New(dataDirectory As String, managedDirectory As String)
            SetField("repository", New ManuscriptRepository(dataDirectory, managedDirectory))
            SetField("authorRepository", New AuthorLibraryRepository(dataDirectory))
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        ' The window as a session starts it, up to the point where a draft
        ' would be offered.
        Public Sub Prepare()
            CallPrivate("BuildInterface")
            Assert.IsTrue(CBool(CallPrivate("LoadManuscripts")), "The library loads.")
            Assert.IsTrue(LoadAuthorLibrary())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(FieldValue("manuscripts"), List(Of Manuscript))
            End Get
        End Property

        Public Sub Open(manuscript As Manuscript)
            CallPrivate("OpenManuscript", manuscript, Nothing)
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Editor As EditManuscriptForm
            Get
                Return DirectCast(FieldValue("manuscriptEditor"), EditManuscriptForm)
            End Get
        End Property

        Public Function Field(name As String) As TextBox
            Return Descendants(Editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
        End Function

        Public ReadOnly Property PageName As String
            Get
                Return FieldValue("currentPage").ToString()
            End Get
        End Property

        Public ReadOnly Property SaveBarVisible As Boolean
            Get
                Dim bar As Control = DirectCast(FieldValue("manuscriptSaveBar"), Control)
                Return bar IsNot Nothing AndAlso bar.Visible
            End Get
        End Property

        Public Sub Tick()
            CallPrivate("ManuscriptDirtyTimer_Tick", Nothing, EventArgs.Empty)
        End Sub

        ' The Discard button of the Unsaved changes bar.
        Public Sub Discard()
            CallPrivate("DiscardManuscriptChanges")
            Application.DoEvents()
        End Sub

        ' The state a restore that succeeded leaves before its restart.
        Public Sub HandOver()
            SetField("handingOverAfterRestore", True)
        End Sub

        ' A method rather than a property, which WinForms would try to serialize.
        Public Sub SetPrompt(value As Func(Of String, DialogResult))
            unsavedChangesPrompt = value
        End Sub

        Public Sub SetDraftPrompt(value As Func(Of String, DialogResult))
            recoveryDraftPrompt = value
        End Sub

        Public Sub SetErrorPrompt(value As Func(Of Exception, Boolean, DialogResult))
            unhandledErrorPrompt = value
        End Sub

        ' What a session does right after it has loaded and rendered.
        Public Sub Offer()
            CallPrivate("OfferRecoveryDraft")
            Application.DoEvents()
        End Sub

        ' What the error dialog's fatal path does before PaperRoute ends.
        Public Function WriteDraft(reason As String) As Boolean
            Return WriteRecoveryDraft(reason)
        End Function

        ' The close Windows sends at shutdown, without closing the window.
        Public Sub SimulateClosing(e As FormClosingEventArgs)
            OnFormClosing(e)
        End Sub

        Public Sub PressCommandKey(keyData As Keys)
            Dim message As New Message()
            GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(Me, New Object() {message, keyData})
            Application.DoEvents()
        End Sub

        Private Function FieldValue(name As String) As Object
            Return GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me)
        End Function

        Private Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, value)
        End Sub

        Private Function CallPrivate(name As String, ParamArray args As Object()) As Object
            Return GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Function
    End Class

End Class
