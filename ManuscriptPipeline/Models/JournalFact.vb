Imports System

Namespace Models

    ' One fact or metric about a journal (#87): from an open index (DOAJ or
    ' OpenAlex), or entered by the researcher with its source and year. The
    ' value is display text, never a score, so metrics are never combined or
    ' ranked.
    Public Class JournalFact

        ' A JournalFactCatalog key, such as "apc" or "citescore".
        Public Property Key As String = String.Empty

        ' The metric's name, for Key "other".
        Public Property Label As String = String.Empty

        Public Property Value As String = String.Empty

        Public Property Url As String = String.Empty

        ' "DOAJ", "OpenAlex", "Example", or where the researcher found it.
        Public Property Source As String = String.Empty

        ' The metric's year, required for metrics entered by the researcher.
        Public Property Year As Integer?

        ' When the source last reviewed its record, if it says.
        Public Property SourceUpdatedUtc As DateTime?

        ' When PaperRoute last checked the source, or the researcher entered it.
        Public Property CheckedUtc As DateTime?

        Public Property EnteredByYou As Boolean = False

    End Class


    ' Which source filled a field of a journal record, and with what value,
    ' so a later refresh can tell a looked-up value from one the researcher
    ' typed. The entry is dropped once the field no longer holds that value.
    Public Class FieldSource

        Public Property Source As String = String.Empty

        Public Property Value As String = String.Empty

        Public Property RetrievedUtc As DateTime?

    End Class

End Namespace
