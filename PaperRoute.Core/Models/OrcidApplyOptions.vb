Imports System.Collections.Generic

Namespace Models

    Public Class OrcidApplyOptions

        Public Property ApplyName As Boolean =
            False

        Public Property ApplyCreditName As Boolean =
            False

        Public Property AddAffiliations As Boolean =
            False

        Public Property ImportDatedWorksAsPublished As Boolean =
            False

        Public Property SelectedWorkPutCodes As List(Of Long) =
            New List(Of Long)()

    End Class

End Namespace
