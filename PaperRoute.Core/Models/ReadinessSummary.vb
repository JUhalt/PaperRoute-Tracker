Namespace Models

    Public Class ReadinessSummary

        Public Property RequiredTotal As Integer = 0
        Public Property RequiredComplete As Integer = 0
        Public Property RequiredNotApplicable As Integer = 0
        Public Property RequiredUnresolved As Integer = 0

        Public Property OptionalTotal As Integer = 0
        Public Property OptionalComplete As Integer = 0
        Public Property OptionalNotApplicable As Integer = 0
        Public Property OptionalUnresolved As Integer = 0

        Public ReadOnly Property RequiredResolved As Integer
            Get
                Return RequiredComplete + RequiredNotApplicable
            End Get
        End Property

        Public ReadOnly Property OptionalResolved As Integer
            Get
                Return OptionalComplete + OptionalNotApplicable
            End Get
        End Property

        Public ReadOnly Property IsReady As Boolean
            Get
                Return RequiredUnresolved = 0
            End Get
        End Property

    End Class

End Namespace
