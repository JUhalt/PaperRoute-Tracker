Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class SubmissionReadinessService

        Private Sub New()
        End Sub

        Public Shared Function CreateProfileFromJournal(
            manuscript As Manuscript,
            journal As JournalRecord,
            Optional createdAtUtc As DateTime? = Nothing
        ) As ManuscriptReadiness

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If journal Is Nothing Then
                Throw New ArgumentNullException(NameOf(journal))
            End If

            If journal.Id = Guid.Empty Then
                Throw New ArgumentException(
                    "The reusable journal must have a valid identifier.",
                    NameOf(journal)
                )
            End If

            EnsureCollections(manuscript)

            Dim existing As ManuscriptReadiness =
                FindProfileForJournal(
                    manuscript,
                    journal.Id
                )

            If existing IsNot Nothing Then
                Return existing
            End If

            Dim timestamp As DateTime =
                If(
                    createdAtUtc.HasValue,
                    createdAtUtc.Value,
                    DateTime.UtcNow
                )

            Dim profile As New ManuscriptReadiness With {
                .JournalId = journal.Id,
                .JournalName = If(journal.Name, String.Empty),
                .CreatedAtUtc = timestamp
            }

            For Each templateItem As JournalChecklistTemplateItem In
                OrderedTemplateItems(journal)

                profile.Items.Add(
                    CreateSnapshot(templateItem)
                )

            Next

            manuscript.ReadinessProfiles.Add(profile)

            Return profile

        End Function

        Public Shared Function FindProfileForJournal(
            manuscript As Manuscript,
            journalId As Guid
        ) As ManuscriptReadiness

            If manuscript Is Nothing OrElse
               journalId = Guid.Empty OrElse
               manuscript.ReadinessProfiles Is Nothing Then

                Return Nothing

            End If

            Return manuscript.ReadinessProfiles.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.JournalId.HasValue AndAlso
                            item.JournalId.Value = journalId
                    End Function
                )

        End Function

        Public Shared Function AddMissingTemplateItems(
            profile As ManuscriptReadiness,
            journal As JournalRecord,
            Optional modifiedAtUtc As DateTime? = Nothing
        ) As Integer

            If profile Is Nothing Then
                Throw New ArgumentNullException(NameOf(profile))
            End If

            If journal Is Nothing Then
                Throw New ArgumentNullException(NameOf(journal))
            End If

            If journal.Id = Guid.Empty Then
                Throw New ArgumentException(
                    "The reusable journal must have a valid identifier.",
                    NameOf(journal)
                )
            End If

            If profile.JournalId.HasValue AndAlso
               profile.JournalId.Value <> journal.Id Then

                Throw New InvalidOperationException(
                    "This readiness profile belongs to a different reusable journal."
                )
            End If

            If Not profile.JournalId.HasValue Then
                profile.JournalId = journal.Id
            End If

            If profile.Items Is Nothing Then
                profile.Items = New List(Of ReadinessItemState)()
            End If

            Dim existingTemplateIds As New HashSet(Of Guid)(
                profile.Items.
                    Where(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.TemplateItemId.HasValue
                        End Function
                    ).
                    Select(
                        Function(item)
                            Return item.TemplateItemId.Value
                        End Function
                    )
            )

            Dim added As Integer = 0

            For Each templateItem As JournalChecklistTemplateItem In
                OrderedTemplateItems(journal)

                If existingTemplateIds.Contains(templateItem.Id) Then
                    Continue For
                End If

                profile.Items.Add(
                    CreateSnapshot(templateItem)
                )

                existingTemplateIds.Add(templateItem.Id)
                added += 1

            Next

            If added > 0 Then
                profile.LastModifiedAtUtc =
                    If(
                        modifiedAtUtc.HasValue,
                        modifiedAtUtc.Value,
                        DateTime.UtcNow
                    )
            End If

            Return added

        End Function

        Public Shared Sub SetStatus(
            item As ReadinessItemState,
            status As ReadinessItemStatus,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            If Not [Enum].IsDefined(
                GetType(ReadinessItemStatus),
                status
            ) Then
                Throw New ArgumentOutOfRangeException(NameOf(status))
            End If

            Dim timestamp As DateTime =
                If(
                    modifiedAtUtc.HasValue,
                    modifiedAtUtc.Value,
                    DateTime.UtcNow
                )

            Dim wasComplete As Boolean =
                item.Status = ReadinessItemStatus.Complete

            item.Status = status

            If status = ReadinessItemStatus.Complete Then

                If Not wasComplete OrElse
                   Not item.CompletedAtUtc.HasValue Then
                    item.CompletedAtUtc = timestamp
                End If

            Else
                item.CompletedAtUtc = Nothing
            End If

            item.LastModifiedAtUtc = timestamp

        End Sub

        Public Shared Sub SetItemNotes(
            item As ReadinessItemState,
            notes As String,
            Optional modifiedAtUtc As DateTime? = Nothing
        )

            If item Is Nothing Then
                Throw New ArgumentNullException(NameOf(item))
            End If

            item.UserNotes =
                If(notes, String.Empty).Trim()

            item.LastModifiedAtUtc =
                If(
                    modifiedAtUtc.HasValue,
                    modifiedAtUtc.Value,
                    DateTime.UtcNow
                )

        End Sub

        Public Shared Function GetSummary(
            profile As ManuscriptReadiness
        ) As ReadinessSummary

            If profile Is Nothing Then
                Throw New ArgumentNullException(NameOf(profile))
            End If

            Dim summary As New ReadinessSummary()

            If profile.Items Is Nothing Then
                Return summary
            End If

            For Each item As ReadinessItemState In profile.Items

                If item Is Nothing Then
                    Continue For
                End If

                If item.IsRequired Then
                    summary.RequiredTotal += 1
                    AddStatusCount(summary, item.Status, required:=True)
                Else
                    summary.OptionalTotal += 1
                    AddStatusCount(summary, item.Status, required:=False)
                End If

            Next

            Return summary

        End Function

        Public Shared Function RemoveProfile(
            manuscript As Manuscript,
            profileId As Guid
        ) As Boolean

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            EnsureCollections(manuscript)

            If manuscript.SubmissionPackets.Any(
                Function(packet)
                    Return packet IsNot Nothing AndAlso
                        packet.ReadinessProfileId.HasValue AndAlso
                        packet.ReadinessProfileId.Value = profileId
                End Function
            ) Then
                Throw New InvalidOperationException(
                    "This readiness profile is linked to a Submission Packet and cannot be removed."
                )
            End If

            Dim profile As ManuscriptReadiness =
                manuscript.ReadinessProfiles.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.Id = profileId
                        End Function
                    )

            If profile Is Nothing Then
                Return False
            End If

            Return manuscript.ReadinessProfiles.Remove(profile)

        End Function

        Private Shared Sub EnsureCollections(
            manuscript As Manuscript
        )

            If manuscript.ReadinessProfiles Is Nothing Then
                manuscript.ReadinessProfiles =
                    New List(Of ManuscriptReadiness)()
            End If

            If manuscript.SubmissionPackets Is Nothing Then
                manuscript.SubmissionPackets =
                    New List(Of SubmissionPacket)()
            End If

        End Sub

        Private Shared Function OrderedTemplateItems(
            journal As JournalRecord
        ) As IEnumerable(Of JournalChecklistTemplateItem)

            If journal.ReadinessChecklistTemplate Is Nothing Then
                Return Enumerable.Empty(Of JournalChecklistTemplateItem)()
            End If

            Return journal.ReadinessChecklistTemplate.
                Where(
                    Function(item)
                        Return item IsNot Nothing
                    End Function
                ).
                OrderBy(
                    Function(item)
                        Return item.SortOrder
                    End Function
                ).
                ThenBy(
                    Function(item)
                        Return item.Title
                    End Function,
                    StringComparer.CurrentCultureIgnoreCase
                )

        End Function

        Private Shared Function CreateSnapshot(
            templateItem As JournalChecklistTemplateItem
        ) As ReadinessItemState

            If templateItem Is Nothing Then
                Throw New ArgumentNullException(NameOf(templateItem))
            End If

            If templateItem.Id = Guid.Empty Then
                Throw New InvalidOperationException(
                    "A reusable checklist item must have a valid identifier before it can seed manuscript readiness."
                )
            End If

            Return New ReadinessItemState With {
                .TemplateItemId = templateItem.Id,
                .Title = If(templateItem.Title, String.Empty),
                .Description = If(templateItem.Description, String.Empty),
                .Category = If(templateItem.Category, String.Empty),
                .SortOrder = templateItem.SortOrder,
                .IsRequired = templateItem.IsRequired,
                .Status = ReadinessItemStatus.Unresolved
            }

        End Function

        Private Shared Sub AddStatusCount(
            summary As ReadinessSummary,
            status As ReadinessItemStatus,
            required As Boolean
        )

            Select Case status

                Case ReadinessItemStatus.Complete
                    If required Then
                        summary.RequiredComplete += 1
                    Else
                        summary.OptionalComplete += 1
                    End If

                Case ReadinessItemStatus.NotApplicable
                    If required Then
                        summary.RequiredNotApplicable += 1
                    Else
                        summary.OptionalNotApplicable += 1
                    End If

                Case Else
                    If required Then
                        summary.RequiredUnresolved += 1
                    Else
                        summary.OptionalUnresolved += 1
                    End If

            End Select

        End Sub

    End Class

End Namespace
