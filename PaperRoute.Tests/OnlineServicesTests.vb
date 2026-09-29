Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Online services and Work offline (#86). Every request passes one gate:
' refused before anything is sent while Work offline is on or its service is
' switched off, and never to a host its service doesn't list. No test here
' reaches the network; a scripted handler stands in for it.
<TestClass>
<DoNotParallelize>
Public Class OnlineServicesTests

    Private _directory As String

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        _directory = Path.Combine(Path.GetTempPath(), "PaperRoute-OnlineTests-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(_directory)
        Dim keys As New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        Try
            If Directory.Exists(_directory) Then Directory.Delete(_directory, True)
        Catch ex As IOException
        End Try
    End Sub


    ' ---------------------------------------------------------------
    ' The gate
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub WorkOfflineStopsEveryServiceBeforeAnythingIsSent()
        Dim network As ScriptedNetwork = UseNetwork()
        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})

        For Each service As OnlineService In OnlineServiceCatalog.Services
            Dim blocked As OnlineServiceBlockedException = Refused(service.Id, "https://" & service.Hosts(0) & "/")
            Assert.AreEqual(OnlineBlockReason.WorkOffline, blocked.Reason, service.Name)
            Assert.AreEqual(service.Id, blocked.ServiceId)
            StringAssert.Contains(blocked.Message, "Work offline is on")
        Next

        Assert.AreEqual(0, network.Requests.Count, "Nothing reached the network.")
        Assert.IsTrue(OnlineAccess.IsWorkingOffline)
    End Sub

    <TestMethod>
    Public Sub ATurnedOffServiceStopsOnlyItself()
        Dim network As ScriptedNetwork = UseNetwork()
        OnlineAccess.Configure(New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {OnlineServiceCatalog.Crossref}})

        Dim blocked As OnlineServiceBlockedException = Refused(OnlineServiceCatalog.Crossref, "https://api.crossref.org/works/10.5555/x")
        Assert.AreEqual(OnlineBlockReason.ServiceOff, blocked.Reason)
        Assert.AreEqual(0, network.Requests.Count)

        ' The publication check also reaches Crossref, as its own service.
        Using response As HttpResponseMessage = OnlineAccess.ClientFor(OnlineServiceCatalog.PublicationCheck).GetAsync("https://api.crossref.org/works/10.5555/x").GetAwaiter().GetResult()
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode)
        End Using
        Assert.AreEqual(1, network.Requests.Count)
        Assert.IsTrue(OnlineAccess.IsAllowed(OnlineServiceCatalog.PublicationCheck))
        Assert.IsFalse(OnlineAccess.IsAllowed(OnlineServiceCatalog.Crossref))
    End Sub

    <TestMethod>
    Public Sub RequestsReachOnlyTheirServicesHostsOverHttps()
        Dim network As ScriptedNetwork = UseNetwork()

        For Each address As String In {"https://example.org/works", "http://api.crossref.org/works/10.5555/x", "https://api.crossref.org.example.org/works", "https://orcid.org/0000-0002-1825-0097"}
            Dim blocked As OnlineServiceBlockedException = Refused(OnlineServiceCatalog.Crossref, address)
            Assert.AreEqual(OnlineBlockReason.UnexpectedHost, blocked.Reason, address)
            StringAssert.Contains(blocked.Message, "Nothing was sent.")
        Next

        Assert.AreEqual(0, network.Requests.Count)
    End Sub

    <TestMethod>
    Public Sub TheGateFollowsRedirectsAndChecksEveryHop()
        ' ORCID's public page redirects to its API host, which ORCID lists.
        Dim network As ScriptedNetwork = UseNetwork(
            Function(request)
                If request.RequestUri.Host = "orcid.org" Then Return Redirect("https://pub.orcid.org/v3.0/0000-0002-1825-0097")
                Return Ok("{}")
            End Function)

        Using response As HttpResponseMessage = OnlineAccess.ClientFor(OnlineServiceCatalog.OrcidImport).GetAsync("https://orcid.org/0000-0002-1825-0097").GetAwaiter().GetResult()
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode)
        End Using
        CollectionAssert.AreEqual({"orcid.org", "pub.orcid.org"}, network.Requests.Select(Function(item) item.Uri.Host).ToList())

        ' A redirect anywhere else stops at the gate.
        network.Respond = Function(request) Redirect("https://tracker.example.net/collect")
        Dim blocked As OnlineServiceBlockedException = Refused(OnlineServiceCatalog.Crossref, "https://api.crossref.org/works/10.5555/x")
        Assert.AreEqual(OnlineBlockReason.UnexpectedHost, blocked.Reason)
        StringAssert.Contains(blocked.Message, "tracker.example.net")
        Assert.AreEqual(3, network.Requests.Count, "The redirect's target was never contacted.")

        ' Nor does a redirect down to plain http get through.
        network.Respond = Function(request) Redirect("http://api.crossref.org/works/10.5555/x")
        Assert.AreEqual(OnlineBlockReason.UnexpectedHost, Refused(OnlineServiceCatalog.Crossref, "https://api.crossref.org/works/10.5555/x").Reason)
    End Sub

    <TestMethod>
    Public Sub RequestsIdentifyPaperRouteAndNothingAboutTheUser()
        Dim network As ScriptedNetwork = UseNetwork()
        OnlineAccess.KeyStore().Save(ProtectedKeyStore.OpenAlex, "openalex-test-key-123")

        Using request As New HttpRequestMessage(HttpMethod.Get, "https://api.crossref.org/works/10.5555/x")
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer should-not-leave")
            OnlineAccess.ClientFor(OnlineServiceCatalog.Crossref).SendAsync(request).GetAwaiter().GetResult().Dispose()
        End Using

        Dim sent As SentRequest = network.Requests.Single()
        Assert.IsTrue(Text.RegularExpressions.Regex.IsMatch(sent.UserAgent, "^PaperRoute-Tracker/\d+\.\d+\.\d+(-[0-9A-Za-z.]+)? \(\+https://github\.com/JUhalt/PaperRoute-Tracker\)$"),
                      "The version, without build metadata, and nothing else: " & sent.UserAgent)
        Assert.IsNull(sent.Authorization, "No key, and nothing a caller set, goes to Crossref.")
        Assert.AreEqual(OnlineAccess.UserAgent(), sent.UserAgent)
    End Sub

    <TestMethod>
    Public Sub TheOpenAlexKeyGoesOnlyToOpenAlexInAHeader()
        Using noKey As New HttpRequestMessage(HttpMethod.Get, "https://api.openalex.org/sources?search=psychology")
            GatedHandler.Prepare(noKey)
            Assert.IsNull(noKey.Headers.Authorization, "Without a key, nothing is added.")
        End Using

        OnlineAccess.KeyStore().Save(ProtectedKeyStore.OpenAlex, "openalex-test-key-123")

        Using toOpenAlex As New HttpRequestMessage(HttpMethod.Get, "https://api.openalex.org/sources?search=psychology")
            GatedHandler.Prepare(toOpenAlex)
            Assert.AreEqual("Bearer", toOpenAlex.Headers.Authorization.Scheme)
            Assert.AreEqual("openalex-test-key-123", toOpenAlex.Headers.Authorization.Parameter)
            Assert.IsFalse(toOpenAlex.RequestUri.ToString().Contains("openalex-test-key-123", StringComparison.Ordinal), "Never in the address.")
        End Using

        For Each address As String In {"https://api.crossref.org/works", "https://openalex.org/sources", "https://api.openalex.org.example.net/sources"}
            Using elsewhere As New HttpRequestMessage(HttpMethod.Get, address)
                GatedHandler.Prepare(elsewhere)
                Assert.IsNull(elsewhere.Headers.Authorization, address)
            End Using
        Next
    End Sub

    <TestMethod>
    Public Sub TheNetworkBelowTheGateNeitherRedirectsNorKeepsCookies()
        Using handler As SocketsHttpHandler = OnlineAccess.DefaultInnerHandler()
            Assert.IsFalse(handler.AllowAutoRedirect, "Only the gate follows redirects, so every hop is checked.")
            Assert.IsFalse(handler.UseCookies, "No cookie links one service's requests to another's.")
        End Using
    End Sub

    <TestMethod>
    Public Sub FailuresAreDescribedInPlainWords()
        Assert.AreEqual("Crossref didn't answer in time. Try again later.",
                        OnlineAccess.Describe(New TaskCanceledException("x", New TimeoutException()), "Crossref"))
        Assert.AreEqual("PaperRoute couldn't reach Crossref. Check your internet connection, then try again.",
                        OnlineAccess.Describe(New HttpRequestException("No such host is known."), "Crossref"))
        StringAssert.Contains(OnlineAccess.Describe(New HttpRequestException("x", Nothing, CType(429, HttpStatusCode)), "ORCID"), "is busy")
        StringAssert.Contains(OnlineAccess.Describe(New HttpRequestException("x", Nothing, HttpStatusCode.Forbidden), "ORCID"), "refused the request")
        StringAssert.Contains(OnlineAccess.Describe(New HttpRequestException("x", Nothing, HttpStatusCode.BadGateway), "GitHub"), "HTTP 502")

        Dim offline As New OnlineServiceBlockedException(OnlineServiceCatalog.Find(OnlineServiceCatalog.Updates), OnlineBlockReason.WorkOffline)
        Assert.AreEqual(offline.Message, OnlineAccess.Describe(offline, "GitHub"))
        Assert.AreEqual(
            "Update check is turned off in Settings > Preferences > Online services, so PaperRoute didn't go online.",
            OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.Updates), OnlineBlockReason.ServiceOff))
    End Sub


    ' ---------------------------------------------------------------
    ' Features behind the gate
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub UpdatesAreRefusedWhileOfflineOrTurnedOffAndFollowOnlyGitHubsHosts()
        Dim network As ScriptedNetwork = UseNetwork(
            Function(request)
                If request.RequestUri.Host = "github.com" Then Return Redirect("https://release-assets.githubusercontent.com/asset/1")
                Return Ok("release")
            End Function)
        Dim downloader As New GatedDownloader()

        Dim bytes As Byte() = downloader.DownloadBytes("https://github.com/JUhalt/PaperRoute-Tracker/releases/download/v0.9.0/releases.win.json", Nothing, 1).GetAwaiter().GetResult()
        Assert.AreEqual("release", Encoding.UTF8.GetString(bytes))
        CollectionAssert.AreEqual({"github.com", "release-assets.githubusercontent.com"}, network.Requests.Select(Function(item) item.Uri.Host).ToList())
        Assert.AreEqual(OnlineAccess.UserAgent(), network.Requests(0).UserAgent)

        ' A GitHub redirect to anywhere unlisted is refused.
        network.Respond = Function(request) Redirect("https://downloads.example.net/PaperRoute-Setup.exe")
        Dim hop As Exception = Assert.ThrowsExactly(Of OnlineServiceBlockedException)(
            Sub() downloader.DownloadBytes("https://github.com/JUhalt/PaperRoute-Tracker/releases/download/v0.9.0/x.nupkg", Nothing, 1).GetAwaiter().GetResult())
        StringAssert.Contains(hop.Message, "downloads.example.net")

        network.Requests.Clear()
        OnlineAccess.Configure(New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {OnlineServiceCatalog.Updates}})
        Assert.AreEqual(OnlineBlockReason.ServiceOff,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() downloader.DownloadString("https://api.github.com/repos/JUhalt/PaperRoute-Tracker/releases", Nothing, 1)).Reason)

        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        Assert.AreEqual(OnlineBlockReason.WorkOffline,
                        Assert.ThrowsExactly(Of OnlineServiceBlockedException)(Sub() downloader.DownloadFile("https://github.com/x", Path.Combine(_directory, "x"), Nothing, Nothing, 1)).Reason)
        Assert.IsFalse(UpdateService.CheckAndOfferUpdateAsync(Nothing, AppUpdateChannel.Stable, False).GetAwaiter().GetResult(),
                       "A startup check while offline quietly does nothing.")
        Assert.AreEqual(0, network.Requests.Count)
    End Sub

    <TestMethod>
    Public Sub APublicationCheckWhileOfflineStopsOnceAndSaysWhy()
        Dim network As ScriptedNetwork = UseNetwork()
        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
        Dim manuscripts As New List(Of Manuscript)()
        For index As Integer = 1 To 3
            Dim manuscript As New Manuscript With {.Title = "A preregistered replication of anchoring effects, study " & index.ToString(), .CurrentStage = PaperStage.UnderReview}
            manuscript.Metadata.Doi = "10.5555/example." & index.ToString()
            manuscripts.Add(manuscript)
        Next

        Dim result As PublicationCheckResult =
            PublicationCheckService.CheckAsync(manuscripts, "0000-0002-1825-0097", New OnlinePublicationSource()).GetAwaiter().GetResult()

        Assert.AreEqual("Work offline is on, so PaperRoute didn't go online. To use Publication check, turn off Work offline in Settings > Preferences > Online services.", result.StoppedReason)
        Assert.AreEqual(0, result.Failures.Count, "One explanation, not a failure per manuscript.")
        Assert.AreEqual(0, result.Checked)
        Assert.AreEqual(0, network.Requests.Count)
    End Sub

    <TestMethod>
    Public Sub EveryRequestInTheAppGoesThroughTheGate()
        Dim root As String = RepositoryRoot()
        Dim sources As String() = Directory.GetFiles(Path.Combine(root, "ManuscriptPipeline"), "*.vb", SearchOption.AllDirectories).
            Where(Function(file) Not file.Contains(Path.DirectorySeparatorChar & "obj" & Path.DirectorySeparatorChar) AndAlso
                                 Not file.Contains(Path.DirectorySeparatorChar & "bin" & Path.DirectorySeparatorChar)).ToArray()
        Assert.IsTrue(sources.Length > 50)

        For Each file As String In sources
            Dim name As String = Path.GetFileName(file)
            Dim code As String = String.Join(Environment.NewLine, IO.File.ReadAllLines(file).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))
            If name <> "OnlineAccess.vb" Then
                Assert.IsFalse(code.Contains("New HttpClient(", StringComparison.Ordinal), name & " creates its own HttpClient.")
                Assert.IsFalse(code.Contains("New SocketsHttpHandler", StringComparison.Ordinal) OrElse code.Contains("New HttpClientHandler", StringComparison.Ordinal), name & " creates its own handler.")
            End If
            For Each banned As String In {"WebClient", "WebRequest.Create", "HttpWebRequest", "TcpClient", "New Socket("}
                Assert.IsFalse(code.Contains(banned, StringComparison.Ordinal), name & " uses " & banned & ".")
            Next
            If name <> "UpdateService.vb" Then
                Assert.IsFalse(code.Contains("GithubSource", StringComparison.Ordinal) OrElse code.Contains("HttpClientFileDownloader", StringComparison.Ordinal), name & " downloads outside the gate.")
            End If
        Next

        Dim updates As String = IO.File.ReadAllText(Path.Combine(root, "ManuscriptPipeline", "Services", "UpdateService.vb"))
        StringAssert.Contains(updates, "New GatedDownloader()", "Velopack downloads through the gate.")
    End Sub


    ' ---------------------------------------------------------------
    ' Settings and the key
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub SettingsAreSavedAtomicallyWithThePreviousVersionKept()
        Dim service As New AppSettingsService(_directory)
        service.Save(New AppSettings With {.OnlineServices = New OnlineServicesSettings With {.WorkOffline = True}})
        service.Save(New AppSettings With {.OnlineServices = New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {" crossref ", "crossref", "", "a-later-service"}}})

        Dim loaded As AppSettings = New AppSettingsService(_directory).Load()
        Assert.IsFalse(loaded.OnlineServices.WorkOffline)
        CollectionAssert.AreEqual({"crossref", "a-later-service"}, loaded.OnlineServices.TurnedOff, "Trimmed, once each, and a later version's ids kept.")
        Assert.IsTrue(IO.File.Exists(Path.Combine(_directory, "settings.bak")))
        StringAssert.Contains(IO.File.ReadAllText(Path.Combine(_directory, "settings.bak")), """WorkOffline"": true")
        Assert.IsFalse(IO.File.Exists(Path.Combine(_directory, "settings.json.tmp")))
    End Sub

    <TestMethod>
    Public Sub ADamagedSettingsFileMeansWorkingOfflineWithTheBackupsOtherPreferences()
        ' The backup is one save older: here, from before Work offline was
        ' turned on. Damage to the latest file must not turn it back off.
        Dim service As New AppSettingsService(_directory)
        service.Save(New AppSettings With {.RevisionWarningDays = 21})
        service.Save(New AppSettings With {.RevisionWarningDays = 21, .OnlineServices = New OnlineServicesSettings With {.WorkOffline = True}})
        IO.File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json")

        Dim fromBackup As New AppSettingsService(_directory)
        Dim settings As AppSettings = fromBackup.Load()
        Assert.IsTrue(settings.OnlineServices.WorkOffline, "An unreadable settings file never turns online services on.")
        Assert.IsTrue(fromBackup.LoadFailed, "The damage is reported.")
        Assert.AreEqual(21, settings.RevisionWarningDays, "Other preferences come from the backup.")

        fromBackup.Save(settings)
        Assert.IsFalse(fromBackup.LoadFailed)
        Assert.AreEqual("{ not json", IO.File.ReadAllText(Path.Combine(_directory, "settings.unreadable.json")), "The damaged file is set aside, not kept as the backup.")
        StringAssert.Contains(IO.File.ReadAllText(Path.Combine(_directory, "settings.bak")), """RevisionWarningDays"": 21", "The good backup is kept.")
        Assert.IsTrue(New AppSettingsService(_directory).Load().OnlineServices.WorkOffline)

        IO.File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json")
        IO.File.WriteAllText(Path.Combine(_directory, "settings.bak"), String.Empty)
        Dim unreadable As New AppSettingsService(_directory)
        Assert.IsTrue(unreadable.Load().OnlineServices.WorkOffline, "Neither file readable: offline, with defaults.")
        Assert.IsTrue(unreadable.LoadFailed)

        IO.File.Delete(Path.Combine(_directory, "settings.json"))
        IO.File.WriteAllText(Path.Combine(_directory, "settings.bak"), "{""RevisionWarningDays"":21}")
        Dim orphaned As New AppSettingsService(_directory)
        Assert.IsTrue(orphaned.Load().OnlineServices.WorkOffline, "A backup without its settings file is treated as damage too.")
        Assert.IsTrue(orphaned.LoadFailed)

        Dim missing As New AppSettingsService(Path.Combine(_directory, "fresh"))
        Assert.IsFalse(missing.Load().OnlineServices.WorkOffline, "A first run is online.")
        Assert.IsFalse(missing.LoadFailed)
    End Sub

    <TestMethod>
    Public Sub SettingsFromEarlierVersionsAreOnline()
        IO.File.WriteAllText(Path.Combine(_directory, "settings.json"), "{""UpdateChannel"":""Preview"",""OnlineServices"":null}")
        Dim settings As AppSettings = New AppSettingsService(_directory).Load()
        Assert.IsFalse(settings.OnlineServices.WorkOffline)
        Assert.AreEqual(0, settings.OnlineServices.TurnedOff.Count)
        Assert.AreEqual(AppUpdateChannel.Preview, settings.UpdateChannel)
    End Sub

    <TestMethod>
    Public Sub TheKeyIsEncryptedForThisAccountAndKeptOutOfTheLibrary()
        Dim folder As String = Path.Combine(_directory, "keys")
        Dim keys As New ProtectedKeyStore(folder)
        Assert.IsFalse(keys.HasKey(ProtectedKeyStore.OpenAlex))
        Assert.IsNull(keys.Load(ProtectedKeyStore.OpenAlex))

        keys.Save(ProtectedKeyStore.OpenAlex, "  openalex-test-key-123 ")
        Assert.AreEqual("openalex-test-key-123", keys.Load(ProtectedKeyStore.OpenAlex))
        Dim stored As Byte() = IO.File.ReadAllBytes(Path.Combine(folder, "openalex.key"))
        Assert.IsFalse(Encoding.UTF8.GetString(stored).Contains("openalex-test-key-123", StringComparison.Ordinal), "Encrypted on disk.")
        Assert.IsFalse(Encoding.Unicode.GetString(stored).Contains("openalex-test-key-123", StringComparison.Ordinal))

        IO.File.WriteAllBytes(Path.Combine(folder, "openalex.key"), {1, 2, 3})
        Assert.IsNull(keys.Load(ProtectedKeyStore.OpenAlex), "A key that can't be decrypted, as on another account, is no key.")

        keys.Save(ProtectedKeyStore.OpenAlex, "openalex-test-key-456")
        keys.Remove(ProtectedKeyStore.OpenAlex)
        Assert.IsFalse(keys.HasKey(ProtectedKeyStore.OpenAlex))

        For Each bad As String In {"", "short", "has a space in it", "line" & vbLf & "break-in-key", New String("x"c, 201), "ключ-не-ascii"}
            Assert.IsFalse(ProtectedKeyStore.IsPlausibleKey(bad), bad)
        Next
        Assert.ThrowsExactly(Of ArgumentException)(Sub() keys.Save(ProtectedKeyStore.OpenAlex, "has a space in it"))
        Assert.ThrowsExactly(Of ArgumentException)(Sub() keys.Save("..\escape", "openalex-test-key-789"))
    End Sub

    <TestMethod>
    Public Sub TheKeyDialogSaysWhyAPastedValueIsNotAKey()
        Assert.AreEqual(String.Empty, OpenAlexKeyForm.HintFor(""))
        Assert.AreEqual(String.Empty, OpenAlexKeyForm.HintFor("abc"), "Nothing while a key is still being typed.")
        Assert.AreEqual(String.Empty, OpenAlexKeyForm.HintFor("openalex-test-key-123"))
        StringAssert.Contains(OpenAlexKeyForm.HintFor("openalex test key"), "without spaces")
        StringAssert.Contains(OpenAlexKeyForm.HintFor("openalex-key" & vbCrLf & "second-line"), "line breaks")
        StringAssert.Contains(OpenAlexKeyForm.HintFor(New String("k"c, 201)), "longer than an OpenAlex key")
    End Sub

    <TestMethod>
    Public Sub TheExampleWindowKeepsTheUsersOnlineChoices()
        Dim choices As New OnlineServicesSettings With {.WorkOffline = True, .TurnedOff = New List(Of String) From {OnlineServiceCatalog.Crossref, "not an id!", OnlineServiceCatalog.OrcidImport}}
        Dim arguments As List(Of String) = ExampleLibraryService.OnlineArguments(choices)
        CollectionAssert.AreEqual({"--work-offline", "--services-off=crossref,orcid-import"}, arguments)

        Dim carried As OnlineServicesSettings = ExampleLibraryService.OnlineSettingsFrom({"--example"}.Concat(arguments).ToArray())
        Assert.IsTrue(carried.WorkOffline)
        CollectionAssert.AreEqual({OnlineServiceCatalog.Crossref, OnlineServiceCatalog.OrcidImport}, carried.TurnedOff)
        Assert.IsFalse(ExampleLibraryService.OnlineSettingsFrom({"--example"}).WorkOffline)
        Assert.AreEqual(0, ExampleLibraryService.OnlineArguments(New OnlineServicesSettings()).Count)
    End Sub

    <TestMethod>
    Public Sub TheGuideListsExactlyWhatTheAppSends()
        Dim guide As String = IO.File.ReadAllText(UserGuideService.GuideFilePath()).Replace(vbCrLf, vbLf)
        StringAssert.Contains(guide, "### What PaperRoute sends, and when" & vbLf & vbLf & OnlineServiceCatalog.ToMarkdownTable().Replace(vbCrLf, vbLf) & vbLf,
                              "The User Guide's table matches the services PaperRoute can contact.")
        For Each service As OnlineService In OnlineServiceCatalog.Services
            Assert.IsTrue(service.Hosts.Count > 0 AndAlso service.Hosts.All(Function(host) host = host.ToLowerInvariant() AndAlso Not host.Contains("/"c)), service.Name)
        Next
        Assert.AreEqual(OnlineServiceCatalog.Services.Count, OnlineServiceCatalog.Services.Select(Function(item) item.Id).Distinct().Count())
    End Sub


    ' ---------------------------------------------------------------
    ' Preferences and the main window
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub PreferencesShowEachServiceAndApplyChangesOnSave()
        RunOnStaThread(
            Sub()
                Dim service As New AppSettingsService(_directory)
                Dim settings As New AppSettings With {.OnlineServices = New OnlineServicesSettings With {.TurnedOff = New List(Of String) From {"a-later-service"}}}

                Using dialog As New SettingsForm(settings, service, showOnlineServices:=True)
                    dialog.keyPrompt = Function(owner) "openalex-test-key-123"
                    ShowOffscreen(dialog)

                    Dim checks As List(Of CheckBox) = Descendants(dialog).OfType(Of CheckBox)().ToList()
                    Dim workOffline As CheckBox = checks.Single(Function(box) box.Text = "Work offline")
                    Dim rows As List(Of CheckBox) = OnlineServiceCatalog.Services.Select(Function(item) checks.Single(Function(box) box.Text = item.Name)).ToList()
                    Dim automatic As CheckBox = checks.Single(Function(box) box.Text = "Check for updates when PaperRoute starts")
                    Assert.IsTrue(rows.All(Function(box) box.Checked AndAlso box.Enabled))
                    Assert.IsTrue(workOffline.Focused OrElse workOffline.ContainsFocus, "Opened at Online services.")

                    For Each item As OnlineService In OnlineServiceCatalog.Services
                        Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text.Contains("Contacts: " & String.Join(", ", item.Hosts), StringComparison.Ordinal) AndAlso
                                                                                     label.Text.Contains("Sends: " & item.Sends, StringComparison.Ordinal)), item.Name)
                    Next

                    workOffline.Checked = True
                    Assert.IsTrue(rows.All(Function(box) Not box.Enabled), "Work offline covers every service.")
                    Assert.IsFalse(automatic.Enabled)
                    workOffline.Checked = False
                    rows(0).Checked = False
                    Assert.IsFalse(automatic.Enabled, "No automatic check while the update check is off.")
                    rows(0).Checked = True
                    Assert.IsTrue(automatic.Enabled)

                    rows.Single(Function(box) box.Text = "DOI lookup (Crossref)").Checked = False
                    Dim addKey As Button = Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Add Key...")
                    addKey.PerformClick()
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "Will be added when you save"))
                    Assert.IsFalse(OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.OpenAlex), "Nothing is stored before Save.")
                    Assert.AreEqual("Replace Key...", addKey.Text)

                    Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Save").PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using

                CollectionAssert.AreEqual({"a-later-service", OnlineServiceCatalog.Crossref}, settings.OnlineServices.TurnedOff)
                Assert.IsTrue(OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.OpenAlex))
                CollectionAssert.AreEqual({"a-later-service", OnlineServiceCatalog.Crossref}, New AppSettingsService(_directory).Load().OnlineServices.TurnedOff)

                Using again As New SettingsForm(settings, service)
                    ShowOffscreen(again)
                    Assert.IsTrue(Descendants(again).OfType(Of Label)().Any(Function(label) label.Text = "Added"))
                    Descendants(again).OfType(Of Button)().Single(Function(button) button.Text = "Remove Key").PerformClick()
                    Descendants(again).OfType(Of Button)().Single(Function(button) button.Text = "Save").PerformClick()
                End Using
                Assert.IsFalse(OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.OpenAlex))

                Using keyDialog As New OpenAlexKeyForm()
                    ShowOffscreen(keyDialog)
                    Assert.IsTrue(keyDialog.KeyBox.Focused, "Ready to paste.")
                    Assert.IsTrue(keyDialog.AddButton.Bottom <= keyDialog.AddButton.Parent.ClientSize.Height AndAlso
                                  keyDialog.RectangleToScreen(keyDialog.ClientRectangle).Contains(keyDialog.AddButton.RectangleToScreen(keyDialog.AddButton.ClientRectangle)),
                                  "Add Key is fully inside the dialog.")
                    Assert.IsTrue(keyDialog.KeyBox.UseSystemPasswordChar)
                    Assert.IsFalse(keyDialog.AddButton.Enabled)
                    keyDialog.KeyBox.Text = "has a space"
                    Assert.IsFalse(keyDialog.AddButton.Enabled)
                    keyDialog.KeyBox.Text = " openalex-test-key-123 "
                    Assert.IsTrue(keyDialog.AddButton.Enabled)
                    Assert.AreEqual("openalex-test-key-123", keyDialog.Key)
                    keyDialog.Close()
                End Using

                Using help As New HelpForm("What PaperRoute sends, and when")
                    ShowOffscreen(help)
                    Application.DoEvents()
                    StringAssert.StartsWith(help.GuideText.Substring(help.GuideSelectionStart), "What PaperRoute sends, and when", "The card's link opens Help at the table.")
                    help.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheRailAndMenuShowWhenPaperRouteIsWorkingOffline()
        RunOnStaThread(
            Sub()
                Using board As New OnlineBoard(_directory)
                    board.Prepare()
                    Dim indicator As RailCommandButton = Descendants(board).OfType(Of RailCommandButton)().Single(Function(button) button.Text = "Online")
                    Assert.IsTrue(indicator.Visible)
                    Assert.AreEqual(RailTone.Success, indicator.Tone, "Online is green.")
                    Assert.AreEqual(RailGlyph.Online, indicator.Glyph)

                    board.SetWorkOffline(True)
                    Application.DoEvents()
                    Assert.IsTrue(OnlineAccess.IsWorkingOffline)
                    Assert.AreEqual("Working offline", indicator.Text)
                    Assert.AreEqual("Working offline", indicator.AccessibleName)
                    Assert.AreEqual(RailTone.Info, indicator.Tone, "Working offline is blue: a choice, not a warning.")
                    Assert.AreEqual(RailGlyph.Offline, indicator.Glyph)
                    Assert.IsTrue(New AppSettingsService(_directory).Load().OnlineServices.WorkOffline, "The choice is saved at once.")
                    Assert.IsTrue(board.StatusText.StartsWith("Working offline.", StringComparison.Ordinal))

                    board.SetWorkOffline(False)
                    Application.DoEvents()
                    Assert.AreEqual("Online", indicator.Text)
                    Assert.AreEqual(RailTone.Success, indicator.Tone)
                    Assert.IsFalse(OnlineAccess.IsWorkingOffline)
                    board.Close()
                End Using
            End Sub)
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Function Refused(serviceId As String, address As String) As OnlineServiceBlockedException
        Try
            OnlineAccess.ClientFor(serviceId).GetAsync(address).GetAwaiter().GetResult().Dispose()
        Catch ex As OnlineServiceBlockedException
            Return ex
        End Try
        Assert.Fail("The request to " & address & " was not refused.")
        Return Nothing
    End Function

    Private Shared Function UseNetwork(Optional respond As Func(Of HttpRequestMessage, HttpResponseMessage) = Nothing) As ScriptedNetwork
        Dim network As New ScriptedNetwork With {.Respond = If(respond, Function(request) Ok("{}"))}
        OnlineAccess.InnerHandlerFactory = Function() network
        Return network
    End Function

    Private Shared Function Ok(body As String) As HttpResponseMessage
        Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body)}
    End Function

    Private Shared Function Redirect(location As String) As HttpResponseMessage
        Dim response As New HttpResponseMessage(HttpStatusCode.Found)
        response.Headers.Location = New Uri(location)
        Return response
    End Function

    Private Shared Function RepositoryRoot() As String
        Dim folder As DirectoryInfo = New DirectoryInfo(AppContext.BaseDirectory)
        While folder IsNot Nothing AndAlso Not IO.File.Exists(Path.Combine(folder.FullName, "ManuscriptPipeline.slnx"))
            folder = folder.Parent
        End While
        Assert.IsNotNull(folder, "The repository root was not found.")
        Return folder.FullName
    End Function

    Private NotInheritable Class SentRequest
        Public Property Uri As Uri
        Public Property UserAgent As String
        Public Property Authorization As String
    End Class

    ' Stands in for the network under the gate.
    Private NotInheritable Class ScriptedNetwork
        Inherits HttpMessageHandler

        Public ReadOnly Requests As New List(Of SentRequest)()
        Public Property Respond As Func(Of HttpRequestMessage, HttpResponseMessage)

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            SyncLock Requests
                Requests.Add(New SentRequest With {
                    .Uri = request.RequestUri,
                    .UserAgent = request.Headers.UserAgent.ToString(),
                    .Authorization = request.Headers.Authorization?.ToString()
                })
            End SyncLock
            Dim response As HttpResponseMessage = Respond(request)
            response.RequestMessage = request
            Return Task.FromResult(response)
        End Function
    End Class

    ' The real main window with its settings in the test folder.
    Private NotInheritable Class OnlineBoard
        Inherits Form1

        Public Sub New(directory As String)
            GetType(Form1).GetField("settingsService", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, New AppSettingsService(directory))
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function SaveManuscripts() As Boolean
            Return True
        End Function

        Protected Overrides Function LoadAuthorLibrary() As Boolean
            Return True
        End Function

        Public Sub Prepare()
            GetType(Form1).GetMethod("BuildInterface", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
            GetType(Form1).GetMethod("RefreshOnlineIndicators", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public ReadOnly Property StatusText As String
            Get
                Return DirectCast(GetType(Form1).GetField("lblStatus", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label).Text
            End Get
        End Property
    End Class

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
