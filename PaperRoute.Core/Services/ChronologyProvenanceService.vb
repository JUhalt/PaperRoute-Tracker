Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ChronologyProvenanceService

        Private Sub New()
        End Sub


        ' =====================================================
        ' Creation stamps
        ' =====================================================

        Public Shared Sub StampCreated(
            item As HistoryEvent,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not item.RecordedAtUtc.HasValue Then

                item.RecordedAtUtc =
                    ResolveUtcStamp(
                        recordedAtUtc
                    )

                item.LastModifiedAtUtc =
                    Nothing

            End If

        End Sub


        Public Shared Sub StampCreated(
            item As JournalSubmission,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not item.RecordedAtUtc.HasValue Then

                item.RecordedAtUtc =
                    ResolveUtcStamp(
                        recordedAtUtc
                    )

                item.LastModifiedAtUtc =
                    Nothing

            End If

        End Sub


        Public Shared Sub StampCreated(
            item As EditorialDecisionEvent,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not item.RecordedAtUtc.HasValue Then

                item.RecordedAtUtc =
                    ResolveUtcStamp(
                        recordedAtUtc
                    )

                item.LastModifiedAtUtc =
                    Nothing

            End If

        End Sub


        Public Shared Sub StampCreated(
            item As ManuscriptVersion,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not item.RecordedAtUtc.HasValue Then

                item.RecordedAtUtc =
                    ResolveUtcStamp(
                        recordedAtUtc
                    )

                item.LastModifiedAtUtc =
                    Nothing

            End If

        End Sub


        Public Shared Sub StampCreated(
            item As CorrespondenceItem,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not item.RecordedAtUtc.HasValue Then

                item.RecordedAtUtc =
                    ResolveUtcStamp(
                        recordedAtUtc
                    )

                item.LastModifiedAtUtc =
                    Nothing

            End If

        End Sub


        ' =====================================================
        ' Modification stamps
        ' =====================================================

        Public Shared Sub StampModified(
            item As HistoryEvent,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.LastModifiedAtUtc =
                ResolveUtcStamp(
                    modifiedAtUtc
                )

        End Sub


        Public Shared Sub StampModified(
            item As JournalSubmission,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.LastModifiedAtUtc =
                ResolveUtcStamp(
                    modifiedAtUtc
                )

        End Sub


        Public Shared Sub StampModified(
            item As EditorialDecisionEvent,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.LastModifiedAtUtc =
                ResolveUtcStamp(
                    modifiedAtUtc
                )

        End Sub


        Public Shared Sub StampModified(
            item As ManuscriptVersion,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.LastModifiedAtUtc =
                ResolveUtcStamp(
                    modifiedAtUtc
                )

        End Sub


        Public Shared Sub StampModified(
            item As CorrespondenceItem,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.LastModifiedAtUtc =
                ResolveUtcStamp(
                    modifiedAtUtc
                )

        End Sub


        ' =====================================================
        ' Imported records
        ' =====================================================

        Public Shared Sub StampImportedManuscript(
            manuscript As Manuscript,
            Optional recordedAtUtc As DateTime? = Nothing
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            Dim stamp As DateTime =
                ResolveUtcStamp(
                    recordedAtUtc
                )

            If manuscript.History IsNot Nothing Then

                For Each historyEvent As HistoryEvent In
                    manuscript.History

                    If historyEvent IsNot Nothing Then

                        StampCreated(
                            historyEvent,
                            stamp
                        )

                    End If

                Next

            End If

            If manuscript.Versions IsNot Nothing Then

                For Each version As ManuscriptVersion In
                    manuscript.Versions

                    If version IsNot Nothing Then

                        StampCreated(
                            version,
                            stamp
                        )

                    End If

                Next

            End If

            If manuscript.Submissions Is Nothing Then
                Return
            End If

            For Each submission As JournalSubmission In
                manuscript.Submissions

                If submission Is Nothing Then
                    Continue For
                End If

                StampCreated(
                    submission,
                    stamp
                )

                If submission.Decisions IsNot Nothing Then

                    For Each decision As EditorialDecisionEvent In
                        submission.Decisions

                        If decision IsNot Nothing Then

                            StampCreated(
                                decision,
                                stamp
                            )

                        End If

                    Next

                End If

                If submission.Correspondence IsNot Nothing Then

                    For Each item As CorrespondenceItem In
                        submission.Correspondence

                        If item IsNot Nothing Then

                            StampCreated(
                                item,
                                stamp
                            )

                        End If

                    Next

                End If

            Next

        End Sub


        Private Shared Function ResolveUtcStamp(
            value As DateTime?
        ) As DateTime

            If Not value.HasValue Then
                Return DateTime.UtcNow
            End If

            Select Case value.Value.Kind

                Case DateTimeKind.Utc
                    Return value.Value

                Case DateTimeKind.Local
                    Return value.Value.ToUniversalTime()

                Case Else
                    ' Explicit provenance values are audit timestamps.
                    ' Treat an unspecified supplied value as UTC rather than
                    ' applying the workstation's local offset implicitly.
                    Return DateTime.SpecifyKind(
                        value.Value,
                        DateTimeKind.Utc
                    )

            End Select

        End Function

    End Class

End Namespace
