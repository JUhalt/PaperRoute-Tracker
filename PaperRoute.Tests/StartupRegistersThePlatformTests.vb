Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Xml.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' Every host registers the Windows platform before anything could read a
' stored key (core stage 2, step 8; risk R4). A host that forgot would show
' every stored key as missing. The hosts are the app (Program.Main), the
' display evidence tool, the workflow demo, this suite (PlatformSetup) and
' the isolated-session test's own copy of Program.Main's start (risk R9).
<TestClass>
Public Class StartupRegistersThePlatformTests

    <TestMethod>
    Public Sub AfterRegisterTheKeyProtectorIsDpapi()

        ' PlatformSetup has registered it before the first test.
        Assert.IsInstanceOfType(PlatformServices.Secrets, GetType(DpapiSecretProtector))

        WindowsPlatform.Register()

        Assert.IsInstanceOfType(PlatformServices.Secrets, GetType(DpapiSecretProtector))
        Assert.IsTrue(PlatformServices.Secrets.IsAvailable)

    End Sub


    ' Velopack first (its hooks may end the process), then the platform,
    ' then everything else: the example session, the error reports, the
    ' single-instance check and the storage step.
    <TestMethod>
    Public Sub TheAppRegistersStraightAfterVelopack()

        Dim main As List(Of String) = BodyOf("ManuscriptPipeline\Program.vb", "Public Sub Main(", "'")

        Assert.AreEqual("VelopackApp.Build().Run()", main(0))
        Assert.AreEqual("WindowsPlatform.Register()", main(1))
        Assert.AreEqual(1, main.Where(Function(line) line.Contains("WindowsPlatform.Register", StringComparison.Ordinal)).Count())

        For Each later As String In {"ExampleLibraryService", "ErrorReportService", "SingleInstanceService", "StorageMigrationService", "Run(args"}
            Dim use As Integer = main.FindIndex(Function(line) line.Contains(later, StringComparison.Ordinal))
            Assert.IsTrue(use > 1, "Program.Main uses " & later & " after WindowsPlatform.Register().")
        Next

    End Sub


    <TestMethod>
    Public Sub TheDisplayEvidenceToolRegistersFirst()

        Dim main As List(Of String) = BodyOf("ManualCertification\DisplayEvidence\Program.vb", "Public Function Main(", "'")

        Assert.AreEqual("WindowsPlatform.Register()", main(0))

    End Sub


    <TestMethod>
    Public Sub TheWorkflowDemoRegistersFirst()

        Dim main As List(Of String) = BodyOf("ManualCertification\V04Demo\Program.cs", "private static void Main(", "//")

        Assert.AreEqual("{", main(0))
        Assert.AreEqual("WindowsPlatform.Register();", main(1))

    End Sub


    ' The isolated-session test starts a fresh copy of PaperRoute as
    ' Program.Main starts it, in a load context of its own, whose registry is
    ' not this suite's: it registers there first.
    <TestMethod>
    Public Sub TheIsolatedSessionTestRegistersAsMainDoes()

        Dim code As List(Of String) = BodyOf("PaperRoute.Tests\IsolatedSessionStorageTests.vb", "Public Sub TheExampleLeavesTheResearchersProfileByteIdentical(", "'")

        Dim register As Integer = code.FindIndex(Function(line) line = "session.CallStatic(""WindowsPlatform"", ""Register"")")
        Dim start As Integer = code.FindIndex(Function(line) line = "session.CallStatic(""ExampleLibraryService"", ""StartSession"")")

        Assert.IsTrue(register >= 0, "The isolated copy registers the Windows platform.")
        Assert.IsTrue(start > register, "It registers before the example session starts, as Program.Main does.")

    End Sub


    <TestMethod>
    Public Sub ThisSuiteRegistersBeforeItsFirstTest()

        Dim setup As List(Of String) = BodyOf("PaperRoute.Tests\PlatformSetup.vb", "Public Shared Sub Register(context As TestContext)", "'")

        Assert.AreEqual("WindowsPlatform.Register()", setup(0))
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(TestRepository.Root(), "PaperRoute.Tests", "PlatformSetup.vb")),
            "<AssemblyInitialize>")

    End Sub


    ' The Windows code stays out of the core: the core references neither
    ' the platform library nor the DPAPI package, which moved there.
    <TestMethod>
    Public Sub TheCoreNeverReferencesThePlatform()

        Dim root As String = TestRepository.Root()
        Dim core As XDocument = XDocument.Load(Path.Combine(root, "PaperRoute.Core", "PaperRoute.Core.vbproj"))
        Dim platform As XDocument = XDocument.Load(Path.Combine(root, "PaperRoute.Platform", "PaperRoute.Platform.vbproj"))

        Assert.IsFalse(core.Descendants("ProjectReference").Any(), "PaperRoute.Core references no project.")
        Assert.IsFalse(core.Descendants("PackageReference").Any(Function(item) CStr(item.Attribute("Include")) = "System.Security.Cryptography.ProtectedData"))
        Assert.AreEqual("10.0.0",
            CStr(platform.Descendants("PackageReference").Single(Function(item) CStr(item.Attribute("Include")) = "System.Security.Cryptography.ProtectedData").Attribute("Version")),
            "The DPAPI package moved at the version the core had.")

        Assert.IsFalse(
            GetType(ProtectedKeyStore).Assembly.GetReferencedAssemblies().Any(
                Function(name) name.Name = "PaperRoute.Platform" OrElse name.Name = "System.Security.Cryptography.ProtectedData"),
            "PaperRoute.Core.dll loads neither the platform library nor DPAPI.")

        Assert.AreEqual("PaperRoute.Platform", GetType(DpapiSecretProtector).Assembly.GetName().Name)
        Assert.AreEqual("PaperRoute.Platform", GetType(WindowsPlatform).Assembly.GetName().Name)

    End Sub


    ' The code lines of a method, trimmed, from the line after its signature
    ' to the end of the file, without blank lines and whole-line comments.
    Private Shared Function BodyOf(relativePath As String, signature As String, commentStart As String) As List(Of String)

        Dim source As String = Path.Combine(TestRepository.Root(), relativePath)
        Dim lines As List(Of String) = File.ReadAllLines(source).Select(Function(line) line.Trim()).ToList()
        Dim start As Integer = lines.FindIndex(Function(line) line.Contains(signature, StringComparison.Ordinal))

        Assert.IsTrue(start >= 0, relativePath & " has " & signature)

        ' A signature over several lines ends where its parentheses close.
        Dim depth As Integer = 0
        Do
            depth += lines(start).Count(Function(character) character = "("c) - lines(start).Count(Function(character) character = ")"c)
            If depth <= 0 Then Exit Do
            start += 1
        Loop

        Return lines.Skip(start + 1).
            Where(Function(line) line.Length > 0 AndAlso Not line.StartsWith(commentStart, StringComparison.Ordinal)).
            ToList()

    End Function

End Class
