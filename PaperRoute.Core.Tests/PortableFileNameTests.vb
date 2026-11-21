Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Services

' File-name safety (core stage 2, step 4; design risk R12): on Windows a
' managed copy and a packet entry get exactly the names they got before.
<TestClass>
Public Class PortableFileNameTests

    Private _root As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' The set packet export built before, from the same two parts.
    Private Shared Function OldInvalidNameCharacters() As HashSet(Of Char)
        Dim characters As New HashSet(Of Char)(Path.GetInvalidFileNameChars())
        For Each character As Char In ":\/*?""<>|"
            characters.Add(character)
        Next
        Return characters
    End Function


    ' The managed copy's name as CreateUniqueDestinationPath made it before.
    Private Shared Function OldManagedCopyName(sourcePath As String, uniqueSuffix As String) As String
        Dim sourceName As String = Path.GetFileNameWithoutExtension(sourcePath)
        Dim extension As String = Path.GetExtension(sourcePath)
        If String.IsNullOrWhiteSpace(sourceName) Then sourceName = "document"
        Return sourceName & "_" & uniqueSuffix & extension
    End Function


    <TestMethod>
    Public Sub OnWindowsTheInvalidSetIsTheOneBuiltBefore()

        Dim expected As New HashSet(Of Char)(Enumerable.Range(0, 32).Select(Function(code) ChrW(code)))
        For Each character As Char In ":\/*?""<>|"
            expected.Add(character)
        Next
        Assert.IsTrue(expected.SetEquals(PortableFileName.InvalidCharacters), "0 to 31 and :\/*?""<>|")
        Assert.AreEqual(41, PortableFileName.InvalidCharacters.Count)

        Dim build As MethodInfo = GetType(PacketExportService).GetMethod("BuildInvalidNameCharacters", BindingFlags.NonPublic Or BindingFlags.Static)
        Dim built As HashSet(Of Char) = DirectCast(build.Invoke(Nothing, Nothing), HashSet(Of Char))
        Assert.IsTrue(built.SetEquals(PortableFileName.InvalidCharacters))

        If OperatingSystem.IsWindows() Then
            Assert.IsTrue(OldInvalidNameCharacters().SetEquals(built), "The set packet export uses on Windows is unchanged.")
        End If

    End Sub


    <TestMethod>
    Public Sub TheDeviceNamesArePacketExportsList()

        Dim field As FieldInfo = GetType(PacketExportService).GetField("ReservedNames", BindingFlags.NonPublic Or BindingFlags.Static)
        Dim packetNames As HashSet(Of String) = DirectCast(field.GetValue(Nothing), HashSet(Of String))
        Assert.IsTrue(packetNames.SetEquals(PortableFileName.ReservedNames))
        Assert.AreEqual(30, PortableFileName.ReservedNames.Count)

    End Sub


    ' Names valid on Windows come back unchanged: accents, spaces and dots
    ' inside, "#", "&", "'", long names, digits only, and the managed-copy
    ' names made from "draft..docx" and "draft .docx".
    <TestMethod>
    Public Sub ANameValidOnWindowsIsUnchanged()

        Dim names As String() = {
            "draft.docx",
            "Résumé final.pdf",
            "Ångström über café.txt",
            "a b c.docx",
            "draft.v2.final.docx",
            "#1 & Smith's reply.docx",
            "12345",
            "12345.pdf",
            New String("x"c, 240) & ".docx",
            "draft._ab12cd34.docx",
            "draft _ab12cd34.docx",
            "NUL_ab12cd34.txt",
            "CON_ab12cd34",
            "CONSOLE.txt",
            "COM10.txt",
            "LPT0.txt",
            ".hidden_ab12cd34",
            "document_ab12cd34.docx",
            "emoji 😀_ab12cd34.png",
            "tab-free [brackets] (parens) {braces}.txt",
            "semi;colon,comma=equals+plus%percent.docx"
        }

        For Each name As String In names
            Assert.AreEqual(name, PortableFileName.Clean(name), name)
            Assert.AreEqual(name, PortableFileName.CleanOffWindows(name), name)
        Next

    End Sub


    <TestMethod>
    Public Sub EveryInvalidCharacterAndDeviceNameIsChanged()

        For Each character As Char In PortableFileName.InvalidCharacters
            Dim name As String = "a" & character & "b.docx"
            Dim cleaned As String = PortableFileName.Clean(name)
            Assert.AreEqual("ab.docx", cleaned, "Character " & AscW(character).ToString())
        Next

        For Each device As String In PortableFileName.ReservedNames
            For Each name As String In {device, device.ToLowerInvariant(), device & ".txt", device & ".v2.docx", device & " .docx", device & "."}
                Dim cleaned As String = PortableFileName.Clean(name)
                Assert.AreNotEqual(name, cleaned, name)
                Assert.IsFalse(PortableFileName.IsReservedName(cleaned), cleaned)
                Assert.IsTrue(cleaned.StartsWith("_", StringComparison.Ordinal), cleaned)
            Next
        Next

        ' Trailing dots and spaces go from the whole name only.
        Assert.AreEqual("draft.docx", PortableFileName.Clean("draft.docx. ."))
        Assert.AreEqual("draft .docx", PortableFileName.Clean("draft .docx "))
        Assert.AreEqual("draft..docx", PortableFileName.Clean("draft..docx"))
        Assert.AreEqual("_", PortableFileName.Clean("..."))
        Assert.AreEqual("_", PortableFileName.Clean("?*"))
        Assert.AreEqual("ab.docx", PortableFileName.Clean("a:b.docx"))
        Assert.AreEqual("_CON.v2_ab12cd34.docx", PortableFileName.Clean("CON.v2_ab12cd34.docx"))
        Assert.ThrowsExactly(Of ArgumentNullException)(Sub() PortableFileName.Clean(Nothing))

    End Sub


    ' The managed copy's name, old expression and new code side by side,
    ' for every shape of source name. On Windows they are equal for each,
    ' including names Clean would change but Windows holds (a device name
    ' before more dots, "CON.v2.docx", which Windows 11 accepts; a stream
    ' name after ":"; a trailing space only "\\?\" paths can make).
    <TestMethod>
    Public Sub OnWindowsAManagedCopyIsNamedAsBefore()

        If Not OperatingSystem.IsWindows() Then Return

        Dim folder As String = Path.Combine(_root, "files")
        Dim sources As String() = {
            "C:\docs\draft.docx",
            "C:\docs\draft..docx",
            "C:\docs\draft .docx",
            "C:\docs\Résumé final.pdf",
            "C:\docs\CON.v2.docx",
            "C:\docs\NUL.txt",
            "C:\docs\con",
            "C:\docs\nul.tar.gz",
            "C:\docs\.hidden",
            "C:\docs\noextension",
            "C:\docs\a.b.c.d.tar.gz",
            "C:\docs\" & New String("x"c, 200) & ".docx",
            "\\server\share\papers\Smith's reply #2.docx",
            "C:\docs/mixed\sep/notes.txt",
            "C:\docs\b:c.docx",
            "C:\docs\a?b*.docx",
            "C:\docs\draft.docx ",
            "C:\docs\draft.docx.",
            "C:\docs\.docx",
            "/home/a/linux draft.docx"
        }

        Dim library As New ManagedLibraryService(Path.Combine(_root, "Library"))
        Dim create As MethodInfo = GetType(ManagedLibraryService).GetMethod("CreateUniqueDestinationPath", BindingFlags.NonPublic Or BindingFlags.Instance)
        Assert.IsNotNull(create)

        For Each source As String In sources
            Dim made As String = CStr(create.Invoke(library, New Object() {folder, source}))
            Dim stem As String = OldManagedCopyName(source, String.Empty)
            Dim stemLength As Integer = stem.Length - Path.GetExtension(source).Length - 1
            Dim prefix As String = Path.Combine(folder, stem.Substring(0, stemLength)) & "_"
            StringAssert.StartsWith(made, prefix, source)
            Dim suffix As String = made.Substring(prefix.Length, 8)
            StringAssert.Matches(suffix, New Regex("^[0-9a-f]{8}$"), source)
            Assert.AreEqual(Path.Combine(folder, OldManagedCopyName(source, suffix)), made, source)
        Next

    End Sub

End Class
