Imports System.Collections.Generic

Namespace Models

    ' Fields proposed from a pasted title page, for preview before anything is
    ' applied. Text that could not be assigned is kept in UnplacedLines.
    Public Class TitlePageParseResult

        Public Property SourceFormat As TitlePageSourceFormat = TitlePageSourceFormat.None

        Public Property Title As String = String.Empty

        Public Property Authors As List(Of TitlePageAuthor) = New List(Of TitlePageAuthor)()

        Public Property AbstractText As String = String.Empty

        Public Property Keywords As List(Of String) = New List(Of String)()

        Public Property UnplacedLines As List(Of String) = New List(Of String)()

        Public Property Warnings As List(Of String) = New List(Of String)()

        Public ReadOnly Property HasContent As Boolean
            Get
                Return Title.Length > 0 OrElse
                    Authors.Count > 0 OrElse
                    AbstractText.Length > 0 OrElse
                    Keywords.Count > 0 OrElse
                    UnplacedLines.Count > 0
            End Get
        End Property

    End Class

End Namespace
