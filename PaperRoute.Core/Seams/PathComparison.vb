Imports System

' How two file-system paths, or two folder names, are compared: without
' case on Windows and on macOS, whose default volumes ignore it, and with
' case on Linux, where "Draft.docx" and "draft.docx" are two files.
'
' On Windows, Comparison is StringComparison.OrdinalIgnoreCase and Comparer
' is StringComparer.OrdinalIgnoreCase, exactly what the call sites used
' before (core stage 2, step 4). A comparison of anything that is not a
' path (an extension, a device name, a host) keeps its own rule.
Public NotInheritable Class PathComparison

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property IgnoresCase As Boolean =
        OperatingSystem.IsWindows() OrElse OperatingSystem.IsMacOS()

    Public Shared ReadOnly Property Comparison As StringComparison =
        If(IgnoresCase, StringComparison.OrdinalIgnoreCase, StringComparison.Ordinal)

    Public Shared ReadOnly Property Comparer As StringComparer =
        If(IgnoresCase, StringComparer.OrdinalIgnoreCase, StringComparer.Ordinal)

End Class
