Imports System
Imports System.Collections.Frozen
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text

' A file name Windows can hold, so a library or packet made on any system
' can be restored or unpacked on Windows. A name valid on Windows is valid
' on Linux and macOS as well.
Public NotInheritable Class PortableFileName

    Private Sub New()
    End Sub

    ' The characters Windows refuses in a file name: 0 to 31 and :\/*?"<>|.
    ' On Windows this is the set Path.GetInvalidFileNameChars() returns plus
    ' the same nine, which packet export used before (core stage 2, step 4);
    ' on Linux that call returns only NUL and "/".
    Public Shared ReadOnly Property InvalidCharacters As IReadOnlySet(Of Char) =
        Enumerable.Range(0, 32).Select(Function(code) ChrW(code)).
            Concat(":\/*?""<>|").
            ToFrozenSet()

    ' Windows' device names, which a file may not be named after, with or
    ' without an extension. The list packet export checks.
    Public Shared ReadOnly Property ReservedNames As IReadOnlySet(Of String) =
        New String() {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)


    ' The name with every invalid character removed, a trailing dot or
    ' space trimmed from the whole name (never from the part before the
    ' extension, so "draft _ab12cd34.docx" stays), and "_" put before a
    ' device name. A name left empty becomes "_".
    Public Shared Function Clean(name As String) As String

        If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))

        Dim kept As New StringBuilder(name.Length)
        For Each character As Char In name
            If Not InvalidCharacters.Contains(character) Then kept.Append(character)
        Next

        Dim cleaned As String = kept.ToString().TrimEnd("."c, " "c)
        If cleaned.Length = 0 Then Return "_"
        If IsReservedName(cleaned) Then Return "_" & cleaned
        Return cleaned

    End Function


    ' On Windows the name as given; elsewhere Clean(name). A managed copy's
    ' name goes through this: on Windows it stays byte for byte what it was
    ' before, because Windows 11 holds names Clean would change (such as
    ' "CON.v2.docx", whose copy is "CON.v2_ab12cd34.docx"), and the source
    ' of a copy made on Windows is a file Windows already holds.
    Public Shared Function CleanOffWindows(name As String) As String
        If OperatingSystem.IsWindows() Then Return name
        Return Clean(name)
    End Function


    ' A device name, checked on the part before the first dot with
    ' trailing spaces ignored: "NUL", "nul.txt", "CON .docx", "CON.v2.docx".
    Public Shared Function IsReservedName(name As String) As Boolean
        If name Is Nothing Then Return False
        Dim firstDot As Integer = name.IndexOf("."c)
        Dim deviceName As String = If(firstDot >= 0, name.Substring(0, firstDot), name).TrimEnd(" "c)
        Return ReservedNames.Contains(deviceName)
    End Function

End Class
