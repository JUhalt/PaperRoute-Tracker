Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The AI assistant in Preferences, Diagnostics, and the main window (#84):
' off until turned on, only the chosen service's setup enabled, keys
' written only after the preferences are saved, a server key only for the
' address it was added for, and never an address or key in Diagnostics. No
' test reaches the network or a model.
<TestClass>
<DoNotParallelize>
Public Class AssistantSettingsTests

    Private Const ClaudeTestKey As String = "sk-ant-test-key-0123456789"
    Private Const ServerTestKey As String = "server-test-key-0123456789"

    Private _directory As String
    Private _keys As ProtectedKeyStore

    <TestInitialize>
    Public Sub Setup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        _directory = TestSupport.CreateTemporaryRoot()
        _keys = New ProtectedKeyStore(Path.Combine(_directory, "keys"))
        OnlineAccess.KeyStoreFactory = Function() _keys
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        OnlineAccess.ResetForTests()
        AssistantService.ProviderFactory = Nothing
        AssistantRunner.ConsentPrompt = Nothing
        TestSupport.DeleteTemporaryRoot(_directory)
    End Sub


    ' ---------------------------------------------------------------
    ' Preferences
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub TheCardIsOffUntilTurnedOnAndEnablesOnlyTheChosenService()
        RunOnSta(
            Sub()
                Using dialog As New SettingsForm(New AppSettings(), Service(), showAssistant:=True)
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)

                    Assert.IsTrue(card.TurnOn.Focused, "Opened at the AI assistant.")
                    Assert.IsFalse(card.TurnOn.Checked, "Off until turned on.")
                    Assert.IsTrue(card.Claude.Checked, "Claude is the suggested service.")
                    Assert.IsFalse(card.Claude.Enabled OrElse card.Compatible.Enabled)
                    Assert.IsFalse(card.ClaudeModel.Enabled OrElse card.Address.Enabled OrElse card.ServerModel.Enabled)
                    Assert.IsFalse(card.AddClaudeKey.Enabled OrElse card.AddServerKey.Enabled)
                    Assert.IsTrue(card.HasLabel("Off until you turn it on. Each window shows what it will send before sending, and nothing changes until you accept a suggestion."))
                    Assert.AreEqual("claude-opus-5-5", card.ClaudeModel.Text)
                    CollectionAssert.AreEqual({"claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-4-5"}, card.ClaudeModel.Items.Cast(Of String)().ToList())
                    Assert.AreEqual(ComboBoxStyle.DropDown, card.ClaudeModel.DropDownStyle, "Another model id can be typed.")
                    Assert.AreEqual("http://localhost:11434/v1", card.Address.PlaceholderText)
                    Assert.AreEqual("llama3.1", card.ServerModel.PlaceholderText)
                    Assert.IsTrue(card.HasLink("Get a key from Anthropic") AndAlso card.HasLink("What the AI assistant sends"))
                    Assert.IsTrue(card.HasLabel("Off until turned on under AI assistant.", contains:=True), "The Online services card says where it is turned on.")
                    Assert.IsFalse(card.TestConnection.Enabled, "Nothing to test while it is off.")
                    Assert.IsFalse(card.TestSends.Visible)

                    card.TurnOn.Checked = True
                    Assert.IsTrue(card.Claude.Enabled AndAlso card.Compatible.Enabled)
                    Assert.IsTrue(card.ClaudeModel.Enabled AndAlso card.AddClaudeKey.Enabled)
                    Assert.IsFalse(card.Address.Enabled OrElse card.ServerModel.Enabled, "Only the chosen service's setup.")
                    Assert.IsTrue(card.HasLabel("On. Set up under AI assistant below.", contains:=True))
                    Assert.IsTrue(card.TestConnection.Enabled, "Claude can be tested before Save.")
                    Assert.AreEqual("Test Connection sends only your Claude key to api.anthropic.com, to list the models your account can use.", card.TestSends.Text)
                    Assert.AreEqual(card.TestSends.Text, card.TestConnection.AccessibleDescription)

                    card.Compatible.Checked = True
                    Assert.IsFalse(card.ClaudeModel.Enabled OrElse card.AddClaudeKey.Enabled)
                    Assert.IsTrue(card.Address.Enabled AndAlso card.ServerModel.Enabled)
                    Assert.IsFalse(card.AddServerKey.Enabled, "A server key needs the server's address first.")
                    Assert.IsFalse(card.TestConnection.Enabled, "A server needs its address and model first.")
                    card.Address.Text = "http://localhost:11434/v1"
                    Assert.IsTrue(card.AddServerKey.Enabled)
                    Assert.IsFalse(card.TestConnection.Enabled)
                    card.ServerModel.Text = "llama3.1"
                    Assert.IsTrue(card.TestConnection.Enabled)
                    Assert.AreEqual("Test Connection asks localhost:11434 for its models and sends nothing else. Nothing leaves this computer.", card.TestSends.Text)
                    card.Address.Text = "https://ai.example.org/v1"
                    Assert.AreEqual("Test Connection asks ai.example.org for its models and sends nothing else.", card.TestSends.Text)

                    ' Work offline stops the assistant but leaves its setup editable.
                    Dim offlineNote As Label = card.Label("Work offline is on, so the assistant can't be used until you turn it off.")
                    Assert.IsFalse(offlineNote.Visible)
                    card.Check("Work offline").Checked = True
                    Assert.IsTrue(offlineNote.Visible)
                    Assert.IsTrue(card.Address.Enabled AndAlso card.ServerModel.Enabled)
                    Assert.IsFalse(card.TestConnection.Enabled, "Work offline stops Test Connection too.")
                    card.Check("Work offline").Checked = False
                    Assert.IsTrue(card.TestConnection.Enabled)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TurningOnClaudeSavesTheChoiceAndWritesTheKeyOnlyAfterSave()
        RunOnSta(
            Sub()
                ' A save that fails changes nothing, the key included.
                Dim blocked As String = Path.Combine(_directory, "not-a-folder")
                File.WriteAllText(blocked, "a file where the settings folder would be")
                Dim failing As New AppSettings()
                Using dialog As New SettingsForm(failing, New AppSettingsService(blocked))
                    Dim problems As New List(Of String)()
                    dialog.problemNotice = Sub(message) problems.Add(message)
                    dialog.assistantKeyPrompt = Function(owner, name) ClaudeTestKey
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    card.TurnOn.Checked = True
                    card.AddClaudeKey.PerformClick()
                    card.Save.PerformClick()
                    Assert.AreEqual(1, problems.Count)
                    StringAssert.StartsWith(problems(0), "PaperRoute could not save the preferences.")
                    Assert.AreNotEqual(DialogResult.OK, dialog.DialogResult)
                    Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic), "No key is written when the preferences aren't saved.")
                    Assert.IsFalse(failing.OnlineServices.Assistant.Enabled)
                    dialog.Close()
                End Using

                Dim settings As New AppSettings()
                Dim asked As New List(Of String)()
                Using dialog As New SettingsForm(settings, Service())
                    dialog.assistantKeyPrompt = Function(owner, name)
                                                    asked.Add(name)
                                                    Return " " & ClaudeTestKey & " "
                                                End Function
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    Assert.AreEqual("Not added", card.StatusOf(card.AddClaudeKey))
                    card.TurnOn.Checked = True
                    card.ClaudeModel.Text = "claude-sonnet-5-5"

                    card.AddClaudeKey.PerformClick()
                    CollectionAssert.AreEqual({ProtectedKeyStore.Anthropic}, asked)
                    Assert.AreEqual("Will be added when you save", card.StatusOf(card.AddClaudeKey))
                    Assert.AreEqual("Replace Claude Key...", card.AddClaudeKey.Text)
                    Assert.IsTrue(card.RemoveClaudeKey.Visible)
                    Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic), "Nothing is stored before Save.")

                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using

                Dim saved As AssistantSettings = New AppSettingsService(_directory).Load().OnlineServices.Assistant
                Assert.IsTrue(saved.Enabled)
                Assert.AreEqual(AssistantProvider.Claude, saved.Provider)
                Assert.AreEqual("claude-sonnet-5-5", saved.ClaudeModel)
                Assert.IsTrue(settings.OnlineServices.Assistant.Enabled, "The open settings follow.")
                Assert.AreEqual(ClaudeTestKey, _keys.Load(ProtectedKeyStore.Anthropic), "Trimmed, and stored once saved.")
                Assert.IsFalse(File.ReadAllText(Path.Combine(_directory, "settings.json")).Contains(ClaudeTestKey, StringComparison.Ordinal), "Never in settings.")

                ' Claude without a key can be saved; the key's row says so.
                _keys.Remove(ProtectedKeyStore.Anthropic)
                Using again As New SettingsForm(settings, Service())
                    ShowOffscreen(again)
                    Dim card As New Card(again)
                    Assert.IsTrue(card.TurnOn.Checked AndAlso card.Claude.Checked)
                    Assert.AreEqual("claude-sonnet-5-5", card.ClaudeModel.Text)
                    Assert.AreEqual("Not added", card.StatusOf(card.AddClaudeKey))
                    Assert.IsFalse(card.RemoveClaudeKey.Visible)
                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, again.DialogResult)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TestConnectionTestsWhatIsTypedBeforeSave()
        Dim network As New AssistantCoreTests.CapturingNetwork With {
            .Respond = Function(sent)
                           If sent.Uri.Host = "localhost" Then Return AssistantCoreTests.Answer(Net.HttpStatusCode.OK, AssistantCoreTests.ServerModelList("llama3.1:latest"))
                           If sent.Uri.AbsolutePath = "/v1/models" Then Return AssistantCoreTests.Answer(Net.HttpStatusCode.OK, AssistantCoreTests.ClaudeModelList("claude-opus-5-5", "claude-haiku-4-5-20251001"))
                           Return AssistantCoreTests.Answer(Net.HttpStatusCode.OK, AssistantCoreTests.ClaudeModelEntry("claude-haiku-4-5-20251001"))
                       End Function
        }
        OnlineAccess.InnerHandlerFactory = Function() network
        RunOnSta(
            Sub()
                Using dialog As New SettingsForm(New AppSettings(), Service(), showAssistant:=True)
                    dialog.assistantKeyPrompt = Function(owner, name) ClaudeTestKey
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    card.TurnOn.Checked = True

                    ' Without a key, nothing is sent.
                    PumpUntilComplete(dialog.TestConnectionAsync())
                    Assert.AreEqual("Add your Claude key first. Nothing was sent.", dialog.TestResultText)
                    Assert.AreEqual(0, network.Requests.Count)

                    ' Turned on, a key added, and a model typed, none of it saved.
                    card.AddClaudeKey.PerformClick()
                    Assert.AreEqual(String.Empty, dialog.TestResultText, "A result stays only for the setup it tested.")
                    card.ClaudeModel.Text = "claude-haiku-4-5"
                    PumpUntilComplete(dialog.TestConnectionAsync())
                    Dim connected As String = "Connected. Claude accepted your key, and your account can use claude-haiku-4-5. The Model list now shows your account's 2 models."
                    Assert.AreEqual(connected, dialog.TestResultText)
                    Assert.AreEqual(ClaudeTestKey, network.Requests(0).Header("x-api-key"))
                    Assert.AreEqual("https://api.anthropic.com/v1/models?limit=1000", network.Requests(0).Uri.AbsoluteUri)
                    CollectionAssert.AreEqual({"claude-opus-5-5", "claude-haiku-4-5-20251001"}, card.ClaudeModel.Items.Cast(Of String)().ToList())
                    Assert.AreEqual("claude-haiku-4-5", card.ClaudeModel.Text, "The model typed stays.")
                    ' Resized, the box would put the first listed name that starts with what was typed.
                    card.ClaudeModel.Width -= 7
                    Application.DoEvents()
                    Assert.AreEqual("claude-haiku-4-5", card.ClaudeModel.Text, "Resizing keeps the model typed.")
                    Assert.AreEqual(connected, dialog.TestResultText)
                    Assert.IsTrue(card.TestConnection.Enabled, "Ready to test again.")
                    Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic), "Testing stores no key.")
                    Assert.IsFalse(File.Exists(Path.Combine(_directory, "settings.json")), "...and saves nothing.")
                    Assert.IsNull(OnlineAccess.CurrentAssistant(), "The saved setup, still off, is what the app goes by.")
                    Assert.AreNotEqual(DialogResult.OK, dialog.DialogResult, "The window stays open.")

                    card.ClaudeModel.Text = "claude-sonnet-5-5"
                    Assert.AreEqual(String.Empty, dialog.TestResultText, "A result stays only for the setup it tested.")
                    card.ClaudeModel.Text = "claude-opus-5"
                    card.ClaudeModel.Width += 7
                    Application.DoEvents()
                    Assert.AreEqual("claude-opus-5", card.ClaudeModel.Text, "Not claude-opus-5-5, the first listed name that starts with it.")

                    ' A model on this computer, typed and not saved.
                    card.Compatible.Checked = True
                    card.Address.Text = "http://localhost:11434/v1"
                    card.ServerModel.Text = "llama3.1"
                    network.Requests.Clear()
                    PumpUntilComplete(dialog.TestConnectionAsync())
                    Assert.AreEqual("Connected. The server at localhost:11434 has llama3.1.", dialog.TestResultText)
                    Dim sent As AssistantCoreTests.SentRequest = network.Requests.Single()
                    Assert.AreEqual("http://localhost:11434/v1/models", sent.Uri.AbsoluteUri)
                    Assert.IsNull(sent.Header("Authorization"))
                    Assert.IsNull(sent.Header("x-api-key"), "The Claude key goes only to Anthropic.")

                    ' Work offline in the window stops it before anything is sent.
                    network.Requests.Clear()
                    card.Check("Work offline").Checked = True
                    PumpUntilComplete(dialog.TestConnectionAsync())
                    Assert.AreEqual(0, network.Requests.Count)
                    card.Check("Work offline").Checked = False

                    DirectCast(dialog.CancelButton, Button).PerformClick()
                    Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                End Using

                Assert.IsFalse(File.Exists(Path.Combine(_directory, "settings.json")), "Cancel saves nothing.")
                Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic) OrElse _keys.HasKey(ProtectedKeyStore.AssistantEndpoint))
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheAddressSaysWhereTheServerIsAsItIsTyped()
        Assert.AreEqual("On this computer: nothing leaves it.", SettingsForm.EndpointCheck("http://localhost:11434/v1").Message)
        Assert.AreEqual("On this computer: nothing leaves it.", SettingsForm.EndpointCheck(" http://127.0.0.1:1234/v1/ ").Message)
        Assert.AreEqual("Elsewhere, over https.", SettingsForm.EndpointCheck("https://ai.example.org/v1").Message)
        Assert.AreEqual("http works only for a server on this computer. Use https for a server elsewhere.", SettingsForm.EndpointCheck("http://192.168.1.20:11434/v1").Message)
        For Each junk As String In {"", "   ", "localhost:11434", "not an address", "ftp://example.org/v1", "https://user:secret@example.org/v1"}
            Assert.AreEqual("Enter the server's address, such as http://localhost:11434/v1.", SettingsForm.EndpointCheck(junk).Message, junk)
            Assert.IsFalse(SettingsForm.EndpointCheck(junk).Usable, junk)
        Next
        Assert.IsTrue(SettingsForm.EndpointCheck("https://ai.example.org/v1").Usable)
        Assert.IsFalse(SettingsForm.EndpointCheck("http://192.168.1.20:11434/v1").Usable)

        RunOnSta(
            Sub()
                Using dialog As New SettingsForm(New AppSettings(), Service(), showAssistant:=True)
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    card.TurnOn.Checked = True
                    card.Compatible.Checked = True
                    Dim line As Label = card.Label("Enter the server's address, such as http://localhost:11434/v1.")
                    card.Address.Text = "http://192.168.1.20:11434/v1"
                    Assert.AreEqual("http works only for a server on this computer. Use https for a server elsewhere.", line.Text)
                    Assert.AreEqual(UiTheme.WarningColor(), line.ForeColor)
                    card.Address.Text = "http://localhost:11434/v1"
                    Assert.AreEqual("On this computer: nothing leaves it.", line.Text)
                    Assert.AreEqual(UiTheme.MutedText(), line.ForeColor)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub SaveIsRefusedForAnUnusableAddressOrNoModel()
        RunOnSta(
            Sub()
                Dim settings As New AppSettings()
                Using dialog As New SettingsForm(settings, Service(), showAssistant:=True)
                    Dim problems As New List(Of String)()
                    dialog.problemNotice = Sub(message) problems.Add(message)
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    card.TurnOn.Checked = True
                    card.Compatible.Checked = True

                    card.Address.Text = "http://192.168.1.20:11434/v1"
                    card.ServerModel.Text = "llama3.1"
                    card.Save.PerformClick()
                    CollectionAssert.AreEqual({"http works only for a server on this computer. Use https for a server elsewhere."}, problems)
                    Assert.AreNotEqual(DialogResult.OK, dialog.DialogResult, "The dialog stays open.")
                    Assert.IsTrue(card.Address.Focused)

                    card.Address.Text = "not an address"
                    card.Save.PerformClick()
                    Assert.AreEqual("Enter the server's address, such as http://localhost:11434/v1.", problems.Last())

                    card.Address.Text = " http://localhost:11434/v1/ "
                    card.ServerModel.Text = "   "
                    card.Save.PerformClick()
                    Assert.AreEqual("Enter the model the server should use, such as llama3.1.", problems.Last())
                    Assert.IsTrue(card.ServerModel.Focused)
                    Assert.IsFalse(File.Exists(Path.Combine(_directory, "settings.json")), "Nothing was saved.")

                    card.ServerModel.Text = " llama3.1 "
                    card.Save.PerformClick()
                    Assert.AreEqual(3, problems.Count)
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using

                Dim saved As AssistantSettings = New AppSettingsService(_directory).Load().OnlineServices.Assistant
                Assert.IsTrue(saved.Enabled)
                Assert.AreEqual(AssistantProvider.Compatible, saved.Provider)
                Assert.AreEqual("http://localhost:11434/v1", saved.Endpoint, "Trimmed, without a trailing slash.")
                Assert.AreEqual("llama3.1", saved.EndpointModel)

                ' Turned off, an unfinished setup is kept without complaint.
                Using again As New SettingsForm(settings, Service())
                    Dim problems As New List(Of String)()
                    again.problemNotice = Sub(message) problems.Add(message)
                    ShowOffscreen(again)
                    Dim card As New Card(again)
                    card.TurnOn.Checked = False
                    card.ServerModel.Text = String.Empty
                    card.Save.PerformClick()
                    Assert.AreEqual(0, problems.Count)
                    Assert.AreEqual(DialogResult.OK, again.DialogResult)
                End Using
                Assert.IsFalse(New AppSettingsService(_directory).Load().OnlineServices.Assistant.Enabled)
            End Sub)
    End Sub

    <TestMethod>
    Public Sub AServerKeyIsKeptOnlyForTheAddressItWasAddedFor()
        RunOnSta(
            Sub()
                Dim settings As New AppSettings()
                Using dialog As New SettingsForm(settings, Service(), showAssistant:=True)
                    dialog.assistantKeyPrompt = Function(owner, name)
                                                    Assert.AreEqual(ProtectedKeyStore.AssistantEndpoint, name)
                                                    Return ServerTestKey
                                                End Function
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    card.TurnOn.Checked = True
                    card.Compatible.Checked = True
                    card.Address.Text = "https://ai.example.org/v1"
                    card.ServerModel.Text = "example-model"
                    card.AddServerKey.PerformClick()
                    Assert.AreEqual("Will be added when you save", card.StatusOf(card.AddServerKey))
                    Assert.AreEqual("Replace Server Key...", card.AddServerKey.Text)
                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using

                Assert.AreEqual("https://ai.example.org:443", settings.OnlineServices.Assistant.EndpointKeyOrigin)
                Assert.AreEqual("https://ai.example.org:443", New AppSettingsService(_directory).Load().OnlineServices.Assistant.EndpointKeyOrigin)
                Assert.AreEqual(ServerTestKey, _keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://ai.example.org:443"), "Stored with its address.")
                Assert.IsNull(_keys.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://other.example.net:443"), "And given to no other.")

                Using again As New SettingsForm(settings, Service())
                    ShowOffscreen(again)
                    Dim card As New Card(again)
                    Assert.AreEqual("Added", card.StatusOf(card.AddServerKey))
                    Assert.AreEqual("Remove Server Key", card.RemoveServerKey.Text)

                    ' Another path at the same address keeps the key.
                    card.Address.Text = "https://ai.example.org/openai/v1"
                    Assert.AreEqual("Added", card.StatusOf(card.AddServerKey))

                    card.Address.Text = "https://other.example.net/v1"
                    Assert.AreEqual("Will be removed when you save: it wasn't added for this address", card.StatusOf(card.AddServerKey))
                    Assert.IsFalse(card.RemoveServerKey.Visible)
                    Assert.AreEqual("Add Server Key...", card.AddServerKey.Text)
                    Assert.IsTrue(_keys.HasKey(ProtectedKeyStore.AssistantEndpoint), "Nothing changes before Save.")

                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, again.DialogResult)
                End Using

                Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.AssistantEndpoint), "The key never follows the address somewhere new.")
                Assert.AreEqual(String.Empty, settings.OnlineServices.Assistant.EndpointKeyOrigin)
                Assert.AreEqual(String.Empty, New AppSettingsService(_directory).Load().OnlineServices.Assistant.EndpointKeyOrigin)
                Assert.AreEqual("https://other.example.net/v1", settings.OnlineServices.Assistant.Endpoint)
            End Sub)
    End Sub

    <TestMethod>
    Public Sub AStoredKeyCanBeRemovedWithTheAssistantOffOrAnotherServiceChosen()
        RunOnSta(
            Sub()
                _keys.Save(ProtectedKeyStore.Anthropic, ClaudeTestKey)
                _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434", ServerTestKey)
                Dim settings As AppSettings = WithAssistant(AssistantCoreTests.CompatibleSettings("http://localhost:11434/v1"))
                settings.OnlineServices.Assistant.EndpointKeyOrigin = "http://localhost:11434"

                ' The other service is chosen: its key can still go.
                Using dialog As New SettingsForm(settings, Service(), showAssistant:=True)
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    Assert.IsTrue(card.Compatible.Checked)
                    Assert.IsFalse(card.AddClaudeKey.Enabled, "Adding a key needs its service chosen.")
                    Assert.IsTrue(card.RemoveClaudeKey.Visible AndAlso card.RemoveClaudeKey.Enabled)
                    card.RemoveClaudeKey.PerformClick()
                    Assert.IsTrue(_keys.HasKey(ProtectedKeyStore.Anthropic), "Nothing changes before Save.")
                    card.TurnOn.Checked = False
                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using
                Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.Anthropic))
                Assert.IsTrue(_keys.HasKey(ProtectedKeyStore.AssistantEndpoint))

                ' The assistant is off: the server key can still go.
                Using again As New SettingsForm(settings, Service(), showAssistant:=True)
                    ShowOffscreen(again)
                    Dim card As New Card(again)
                    Assert.IsFalse(card.TurnOn.Checked)
                    Assert.IsTrue(card.RemoveServerKey.Visible AndAlso card.RemoveServerKey.Enabled)
                    card.RemoveServerKey.PerformClick()
                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, again.DialogResult)
                End Using
                Assert.IsFalse(_keys.HasKey(ProtectedKeyStore.AssistantEndpoint))
                Assert.AreEqual(String.Empty, settings.OnlineServices.Assistant.EndpointKeyOrigin)
            End Sub)
    End Sub

    <TestMethod>
    Public Sub DontAskAgainChoicesCanBeForgotten()
        RunOnSta(
            Sub()
                Dim settings As AppSettings = WithAssistant(AssistantCoreTests.ClaudeSettings())
                settings.OnlineServices.Assistant.ConfirmedUses.AddRange({"decision-letter|https://api.anthropic.com:443", "cover-letter|https://api.anthropic.com:443"})

                Using dialog As New SettingsForm(settings, Service())
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    Dim forget As Button = card.Button("Forget Don't Ask Again Choices")
                    Assert.IsTrue(forget.Enabled)
                    Assert.IsTrue(card.HasLabel("Don't Ask Again choices saved: 2. Each is for one feature with one service."))
                    forget.PerformClick()
                    Assert.IsFalse(forget.Enabled)
                    Assert.IsTrue(card.HasLabel("Will be forgotten when you save."))
                    Assert.AreEqual(2, settings.OnlineServices.Assistant.ConfirmedUses.Count, "Nothing changes before Save.")
                    card.Save.PerformClick()
                End Using

                Assert.AreEqual(0, settings.OnlineServices.Assistant.ConfirmedUses.Count)
                Assert.AreEqual(0, New AppSettingsService(_directory).Load().OnlineServices.Assistant.ConfirmedUses.Count)

                Using again As New SettingsForm(settings, Service())
                    ShowOffscreen(again)
                    Dim card As New Card(again)
                    Assert.IsFalse(card.Button("Forget Don't Ask Again Choices").Enabled)
                    Assert.IsTrue(card.HasLabel("PaperRoute asks every time before sending to a service elsewhere."))
                    again.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheAssistantIsNeverSwitchedFromTheOnlineServicesList()
        RunOnSta(
            Sub()
                ' An earlier preview listed the assistant with the other services.
                Dim settings As New AppSettings()
                settings.OnlineServices.TurnedOff.AddRange({OnlineServiceCatalog.AssistantClaude, OnlineServiceCatalog.Crossref, "a-later-service"})

                Using dialog As New SettingsForm(settings, Service(), showOnlineServices:=True)
                    ShowOffscreen(dialog)
                    Dim card As New Card(dialog)
                    For Each item As OnlineService In OnlineServiceCatalog.Services.Where(Function(service) service.OffUntilTurnedOn)
                        Assert.IsFalse(card.Checks.Any(Function(box) box.Text = item.Name), item.Name & " has no switch here.")
                        Assert.IsTrue(card.HasLabel(item.Name), item.Name & " is still listed.")
                        Assert.IsTrue(card.HasLabel("Contacts: " & item.Contacts, contains:=True))
                    Next
                    StringAssert.Contains(OnlineServiceCatalog.Find(OnlineServiceCatalog.AssistantCompatible).Contacts, "The address you set")
                    card.TurnOn.Checked = True
                    card.Save.PerformClick()
                    Assert.AreEqual(DialogResult.OK, dialog.DialogResult)
                End Using

                CollectionAssert.AreEqual({"a-later-service", OnlineServiceCatalog.Crossref}, settings.OnlineServices.TurnedOff)
                CollectionAssert.AreEqual({"a-later-service", OnlineServiceCatalog.Crossref}, New AppSettingsService(_directory).Load().OnlineServices.TurnedOff)
                OnlineAccess.Configure(settings.OnlineServices)
                Assert.IsTrue(OnlineAccess.IsAllowed(OnlineServiceCatalog.AssistantClaude), "Turned on under AI assistant, nothing else stops it.")
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub TheCardFitsAtTheSmallestSize(mode As SystemColorMode)
        ' Test Connection's longest kind of result wraps too.
        Dim network As New AssistantCoreTests.CapturingNetwork With {
            .Respond = Function(sent) AssistantCoreTests.Answer(Net.HttpStatusCode.OK, AssistantCoreTests.ServerModelList(
                "qwen3-coder:30b-a3b-instruct-q4_K_M", "deepseek-r1:70b-llama-distill-q4_K_M", "mistral-small3.2:24b-instruct-2506-q8_0",
                "gemma3:27b-it-qat", "llama3.3:70b-instruct-q4_K_M", "phi4-reasoning:14b-plus-q8_0", "nomic-embed-text:latest"))
        }
        OnlineAccess.InnerHandlerFactory = Function() network
        RunOnSta(
            Sub()
                Dim settings As AppSettings = WithAssistant(AssistantCoreTests.CompatibleSettings("http://localhost:11434/v1"))
                _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "http://localhost:11434", ServerTestKey)
                settings.OnlineServices.Assistant.EndpointKeyOrigin = "http://localhost:11434"
                Using dialog As New SettingsForm(settings, Service(), showAssistant:=True)
                    ShowOffscreen(dialog)
                    dialog.Size = dialog.MinimumSize
                    Application.DoEvents()
                    PumpUntilComplete(dialog.TestConnectionAsync())
                    StringAssert.StartsWith(dialog.TestResultText, "The server at localhost:11434 is running, but doesn't list llama3.1. It lists: ")
                    Assert.AreEqual("Bearer " & ServerTestKey, network.Requests.Single().Header("Authorization"), "The stored key, at the address it was added for.")
                    Dim card As New Card(dialog)
                    Assert.AreEqual("Test Connection sends only your server key to localhost:11434, to list its models. Nothing leaves this computer.", card.TestSends.Text)
                    Dim group As Control = card.TurnOn.Parent.Parent
                    Assert.IsTrue(TypeOf group Is GroupBox)
                    Assert.AreEqual(UiTheme.CardBackground(), group.BackColor, "Themed as a card.")
                    For Each control As Control In Descendants(group).Where(Function(item) item.Visible AndAlso (TypeOf item Is ButtonBase OrElse TypeOf item Is TextBoxBase OrElse TypeOf item Is ComboBox OrElse TypeOf item Is Label))
                        Dim bounds As Rectangle = group.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))
                        Assert.IsTrue(bounds.Left >= 0 AndAlso bounds.Right <= group.ClientSize.Width, control.GetType().Name & " """ & control.Text & """ fits across the card.")
                    Next
                    For Each button As Button In {card.Save, DirectCast(dialog.CancelButton, Button)}
                        Assert.IsTrue(dialog.ClientRectangle.Contains(dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))), button.Text & " is visible.")
                    Next
                    Assert.AreEqual("Added", card.StatusOf(card.AddServerKey))
                    dialog.Close()
                End Using
            End Sub, mode)
    End Sub

    <TestMethod>
    Public Sub TheKeyDialogsSayWhereEachKeyGoes()
        RunOnSta(
            Sub()
                Using claude As ApiKeyForm = ApiKeyForm.ForAssistant(ProtectedKeyStore.Anthropic)
                    ShowOffscreen(claude)
                    Assert.AreEqual("Add Claude Key", claude.Text)
                    Assert.IsTrue(Descendants(claude).OfType(Of Label)().Any(Function(label) label.Text.Contains("PaperRoute keeps your Anthropic key encrypted for your Windows account, sends it only to api.anthropic.com in a request header, and never puts it in backups, exports, or Diagnostics.", StringComparison.Ordinal)))
                    Assert.IsTrue(Descendants(claude).OfType(Of LinkLabel)().Any(Function(link) link.Text = "Open console.anthropic.com/settings/keys"))
                    Assert.IsTrue(claude.KeyBox.Focused AndAlso claude.KeyBox.UseSystemPasswordChar)
                    Assert.IsFalse(claude.AddButton.Enabled)
                    claude.KeyBox.Text = " " & ClaudeTestKey & " "
                    Assert.IsTrue(claude.AddButton.Enabled)
                    Assert.AreEqual(ClaudeTestKey, claude.Key)
                    Assert.IsTrue(claude.RectangleToScreen(claude.ClientRectangle).Contains(claude.AddButton.RectangleToScreen(claude.AddButton.ClientRectangle)), "Add Key is fully inside the dialog.")
                    claude.Close()
                End Using

                Using server As ApiKeyForm = ApiKeyForm.ForAssistant(ProtectedKeyStore.AssistantEndpoint)
                    ShowOffscreen(server)
                    Assert.AreEqual("Add Server Key", server.Text)
                    Assert.IsTrue(Descendants(server).OfType(Of Label)().Any(Function(label) label.Text.Contains("sends it only to the address you set, and only while that address stays the same.", StringComparison.Ordinal)))
                    Assert.IsFalse(Descendants(server).OfType(Of LinkLabel)().Any(), "A server's key comes from its own setup.")
                    Assert.IsTrue(server.RectangleToScreen(server.ClientRectangle).Contains(server.AddButton.RectangleToScreen(server.AddButton.ClientRectangle)))
                    server.Close()
                End Using
            End Sub)

        StringAssert.Contains(ApiKeyForm.HintFor(New String("k"c, 201), "an Anthropic key"), "longer than an Anthropic key")
        Assert.AreEqual(OpenAlexKeyForm.HintFor("openalex test key"), ApiKeyForm.HintFor("openalex test key", "an OpenAlex key"))
    End Sub


    ' ---------------------------------------------------------------
    ' Diagnostics and the main window
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub DiagnosticsSayWhetherTheAssistantIsOnButNeverWhereOrWithWhatKey()
        Dim off As String() = DiagnosticsForm.AssistantLines(New AssistantSettings(), _keys)
        CollectionAssert.AreEqual({"AI assistant: Off", "Claude key: Not added", "Server key: Not added"}, off)
        Assert.AreEqual("AI assistant: Off", DiagnosticsForm.AssistantLines(Nothing, _keys)(0))

        _keys.Save(ProtectedKeyStore.Anthropic, ClaudeTestKey)
        _keys.SaveFor(ProtectedKeyStore.AssistantEndpoint, "https://ai.example.org:443", ServerTestKey)
        CollectionAssert.AreEqual({"AI assistant: On (Claude, claude-opus-5-5)", "Claude key: Added", "Server key: Added"},
                                  DiagnosticsForm.AssistantLines(AssistantCoreTests.ClaudeSettings().Assistant, _keys))
        Assert.AreEqual("AI assistant: On (OpenAI-compatible server on this computer)",
                        DiagnosticsForm.AssistantLines(AssistantCoreTests.CompatibleSettings("http://localhost:11434/v1").Assistant, _keys)(0))
        Assert.AreEqual("AI assistant: On (OpenAI-compatible server elsewhere, https)",
                        DiagnosticsForm.AssistantLines(AssistantCoreTests.CompatibleSettings("https://ai.example.org/v1").Assistant, _keys)(0))

        RunOnSta(
            Sub()
                Dim settings As AppSettings = WithAssistant(AssistantCoreTests.CompatibleSettings("https://ai.example.org/v1"))
                settings.OnlineServices.Assistant.EndpointKeyOrigin = "https://ai.example.org:443"
                Using report As New DiagnosticsForm(settings)
                    Dim text As String = report.ReportText
                    StringAssert.Contains(text, "AI assistant: On (OpenAI-compatible server elsewhere, https)" & Environment.NewLine)
                    StringAssert.Contains(text, "Claude key: Added" & Environment.NewLine)
                    StringAssert.Contains(text, "Server key: Added" & Environment.NewLine)
                    For Each secret As String In {"example.org", "ai.example", "llama3.1", ClaudeTestKey, ServerTestKey, "sk-ant"}
                        Assert.IsFalse(text.Contains(secret, StringComparison.OrdinalIgnoreCase), "Diagnostics never show " & secret)
                    Next
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheMainWindowSavesADontAskAgainChoice()
        RunOnSta(
            Sub()
                Using board As New AssistantBoard(_directory)
                    board.UseSettings(WithAssistant(AssistantCoreTests.ClaudeSettings()))
                    Dim asked As Integer = 0
                    AssistantRunner.ConsentPrompt = Function(owner, asking, connection)
                                                        asked += 1
                                                        Return True
                                                    End Function

                    Dim request As AssistantRequest = AssistantService.BuildLetterRequest(AssistantCoreTests.Letter)
                    Assert.IsTrue(AssistantRunner.Confirm(Nothing, request))
                    Assert.IsTrue(AssistantRunner.Confirm(Nothing, request))
                    Assert.AreEqual(1, asked, "Not asked again.")

                    Dim saved As List(Of String) = New AppSettingsService(_directory).Load().OnlineServices.Assistant.ConfirmedUses
                    CollectionAssert.AreEqual({"decision-letter|https://api.anthropic.com:443"}, saved, "Saved at once, so it lasts across restarts.")

                    ' Directly, as the gate calls it: a repeat is ignored.
                    board.RememberAssistantUse("DECISION-LETTER|https://api.anthropic.com:443")
                    board.RememberAssistantUse("  ")
                    Assert.AreEqual(1, New AppSettingsService(_directory).Load().OnlineServices.Assistant.ConfirmedUses.Count)
                End Using
            End Sub)

        ' The main window reaches the gate only through the step that also
        ' connects the saving of a choice, and startup takes that step
        ' before the interface is built.
        Dim folder As String = Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")
        Dim configuring As New List(Of String)()
        For Each source As String In Directory.GetFiles(folder, "Form1*.vb")
            Dim code As String = String.Join(Environment.NewLine, File.ReadAllLines(source).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))
            Dim found As Integer = code.Split({"OnlineAccess.Configure("}, StringSplitOptions.None).Length - 1
            For index As Integer = 1 To found
                configuring.Add(Path.GetFileName(source))
            Next
        Next
        CollectionAssert.AreEqual({"Form1.Online.vb"}, configuring, "Only ConnectOnlineAccess configures the gate.")

        Dim startup As String = File.ReadAllText(Path.Combine(folder, "Form1.vb"))
        Dim loading As Integer = startup.IndexOf("Private Sub Form1_Load(", StringComparison.Ordinal)
        Assert.IsTrue(loading >= 0)
        Dim connecting As Integer = startup.IndexOf("ConnectOnlineAccess()", loading, StringComparison.Ordinal)
        Dim building As Integer = startup.IndexOf("BuildInterface()", loading, StringComparison.Ordinal)
        Assert.IsTrue(connecting > loading AndAlso connecting < building, "Startup connects the gate, before the interface is built.")
    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Function Service() As AppSettingsService
        Return New AppSettingsService(_directory)
    End Function

    Private Shared Function WithAssistant(online As OnlineServicesSettings) As AppSettings
        Return New AppSettings With {.OnlineServices = online}
    End Function

    ' The Preferences window's controls, found as a researcher sees them.
    Private NotInheritable Class Card

        Private ReadOnly _form As Form

        Public Sub New(form As Form)
            _form = form
        End Sub

        Public ReadOnly Property Checks As List(Of CheckBox)
            Get
                Return Descendants(_form).OfType(Of CheckBox)().ToList()
            End Get
        End Property

        Public Function Check(text As String) As CheckBox
            Return Checks.Single(Function(box) box.Text = text)
        End Function

        Public ReadOnly Property TurnOn As CheckBox
            Get
                Return Check("Turn on the AI assistant")
            End Get
        End Property

        Public ReadOnly Property Claude As RadioButton
            Get
                Return Descendants(_form).OfType(Of RadioButton)().Single(Function(item) item.Text = "Claude (Anthropic), with your own key")
            End Get
        End Property

        Public ReadOnly Property Compatible As RadioButton
            Get
                Return Descendants(_form).OfType(Of RadioButton)().Single(Function(item) item.Text = "Another server, or a model on this computer (OpenAI-compatible)")
            End Get
        End Property

        Public ReadOnly Property ClaudeModel As ComboBox
            Get
                Return Descendants(_form).OfType(Of ComboBox)().Single(Function(item) item.AccessibleName = "Claude model")
            End Get
        End Property

        Public ReadOnly Property Address As TextBox
            Get
                Return Descendants(_form).OfType(Of TextBox)().Single(Function(item) item.AccessibleName = "Server address")
            End Get
        End Property

        Public ReadOnly Property ServerModel As TextBox
            Get
                Return Descendants(_form).OfType(Of TextBox)().Single(Function(item) item.AccessibleName = "Server model")
            End Get
        End Property

        Public Function Button(text As String) As Button
            Return Descendants(_form).OfType(Of Button)().Single(Function(item) item.Text = text)
        End Function

        Public ReadOnly Property AddClaudeKey As Button
            Get
                Return Descendants(_form).OfType(Of Button)().Single(Function(item) item.Text = "Add Claude Key..." OrElse item.Text = "Replace Claude Key...")
            End Get
        End Property

        Public ReadOnly Property RemoveClaudeKey As Button
            Get
                Return Button("Remove Claude Key")
            End Get
        End Property

        Public ReadOnly Property AddServerKey As Button
            Get
                Return Descendants(_form).OfType(Of Button)().Single(Function(item) item.Text = "Add Server Key..." OrElse item.Text = "Replace Server Key...")
            End Get
        End Property

        Public ReadOnly Property RemoveServerKey As Button
            Get
                Return Button("Remove Server Key")
            End Get
        End Property

        Public ReadOnly Property Save As Button
            Get
                Return Button("Save")
            End Get
        End Property

        Public ReadOnly Property TestConnection As Button
            Get
                Return Button("Test Connection")
            End Get
        End Property

        ' The line below Test Connection: what it sends, and to whom.
        Public ReadOnly Property TestSends As Label
            Get
                Return Descendants(_form).OfType(Of Label)().Single(Function(item) item.AccessibleName = "What Test Connection sends")
            End Get
        End Property

        ' A key's status sits beside its Add button.
        Public Function StatusOf(addButton As Button) As String
            Return addButton.Parent.Controls.OfType(Of Label)().Single().Text
        End Function

        Public Function Label(text As String) As Label
            Return Descendants(_form).OfType(Of Label)().Where(Function(item) Not TypeOf item Is LinkLabel).Single(Function(item) item.Text = text)
        End Function

        Public Function HasLabel(text As String, Optional contains As Boolean = False) As Boolean
            Return Descendants(_form).OfType(Of Label)().Any(Function(item) If(contains, item.Text.Contains(text, StringComparison.Ordinal), item.Text = text))
        End Function

        Public Function HasLink(text As String) As Boolean
            Return Descendants(_form).OfType(Of LinkLabel)().Any(Function(item) item.Text = text)
        End Function

    End Class

    ' The real main window with its settings in the test folder.
    Private NotInheritable Class AssistantBoard
        Inherits Form1

        Public Sub New(directory As String)
            GetType(Form1).GetField("settingsService", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, New AppSettingsService(directory))
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides Function SaveManuscripts() As Boolean
            Return True
        End Function

        Protected Overrides Function LoadAuthorLibrary() As Boolean
            Return True
        End Function

        ' As at startup, before the interface exists: the settings, then
        ' the one step that connects them to the gate.
        Public Sub UseSettings(settings As AppSettings)
            GetType(Form1).GetField("appSettings", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, settings)
            GetType(Form1).GetMethod("ConnectOnlineAccess", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
        End Sub
    End Class

    Private Shared Sub PumpUntilComplete(operation As Tasks.Task)
        Dim timer As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(10)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "Test Connection timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

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

    Private Shared Sub RunOnSta(action As Action, Optional mode As SystemColorMode = SystemColorMode.Classic)
        Dim failure As ExceptionDispatchInfo = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Application.SetColorMode(mode)
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                Finally
                    Application.SetColorMode(priorMode)
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(5)), "The AI assistant settings test timed out.")
        failure?.Throw()
    End Sub

End Class
