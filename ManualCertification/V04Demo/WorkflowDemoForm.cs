using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using System.Text;

namespace PaperRoute.V04Demo;

internal sealed class WorkflowDemoForm : Form
{
    private readonly ManuscriptRepository repository;
    private readonly Guid manuscriptId;
    private readonly bool minimum;
    private readonly Button openButton = new()
    {
        Text = "Open Manuscript Details", AutoSize = true, MinimumSize = new Size(0, 36)
    };
    private readonly TextBox savedState = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = true,
        AccessibleName = "Saved workflow sample"
    };

    internal WorkflowDemoForm(string sessionRoot, bool minimum)
    {
        this.minimum = minimum;
        VerifySessionPaths(sessionRoot);

        // The original fixture is synthetic and has no disk dependencies. Reduce
        // it to a preparation-only starting point for the connected workflow.
        var fixture = DemoFixture.Create();
        var manuscript = fixture.Manuscript;
        manuscriptId = manuscript.Id;
        manuscript.Title = "DEMO - Prepare a journal submission from an exact manuscript version";
        manuscript.CurrentStage = PaperStage.Draft;
        manuscript.Location = ManuscriptLocation.Pipeline;
        manuscript.StageEnteredDate = DateTime.Today.AddDays(-1);
        manuscript.Submissions.Clear();
        manuscript.SubmissionPackets.Clear();
        manuscript.History.Clear();
        manuscript.History.Add(new HistoryEvent
        {
            Stage = PaperStage.Draft,
            EventDate = manuscript.StageEnteredDate,
            Note = "Fictional draft created for this disposable workflow session."
        });
        manuscript.Versions.Clear();
        var version = new ManuscriptVersion
        {
            Label = "Draft snapshot - exact version for the first preparation packet",
            CreatedDate = DateTime.Today,
            RecordedAtUtc = DateTime.UtcNow,
            Notes = "Synthetic metadata-only version. No real manuscript file is attached."
        };
        manuscript.Versions.Add(version);
        manuscript.CurrentVersionId = version.Id;
        manuscript.ReadinessProfiles.RemoveAll(profile => profile.JournalId != manuscript.TargetJournalId);
        foreach (var item in manuscript.ReadinessProfiles[0].Items)
        {
            item.Status = ReadinessItemStatus.Unresolved;
        }

        var authorRepository = new AuthorLibraryRepository();
        authorRepository.Save(fixture.Library);
        repository = new ManuscriptRepository();
        repository.Save(new List<Manuscript> { manuscript });

        Text = "PaperRoute v0.4 workflow demo [disposable session]";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);
        MinimumSize = new Size(760, 540);
        ClientSize = new Size(920, 620);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            Text = "Practice readiness, Version History, Submission Packets, and recording a journal submission. " +
                "Save & Close in Manuscript Details writes only to this disposable session. Reopen to verify your changes.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(intro, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        var reloadButton = new Button { Text = "Reload Saved State", AutoSize = true, MinimumSize = new Size(0, 36) };
        openButton.Click += (_, _) => OpenManuscript();
        reloadButton.Click += (_, _) => ReloadWithErrorHandling();
        actions.Controls.Add(openButton);
        actions.Controls.Add(reloadButton);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(savedState, 0, 2);

        var path = new TextBox
        {
            Text = sessionRoot,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AccessibleName = "Disposable workflow session directory",
            Margin = new Padding(0, 12, 0, 8)
        };
        root.Controls.Add(path, 0, 3);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0)
        };
        var closeButton = new Button
        {
            Text = "Close Demo", AutoSize = true, MinimumSize = new Size(0, 36), DialogResult = DialogResult.Cancel
        };
        footer.Controls.Add(closeButton);
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);
        CancelButton = closeButton;
        AcceptButton = openButton;
        RefreshSavedState("A fresh synthetic Draft has been saved in this session.");
    }

    private static void VerifySessionPaths(string sessionRoot)
    {
        if (!StorageEnvironment.IsIsolatedSession ||
            StorageMigrationService.CurrentDataRoot() != Path.Combine(sessionRoot, "current-data") ||
            StorageMigrationService.LegacyDataRoot() != Path.Combine(sessionRoot, "legacy-data") ||
            StorageMigrationService.CurrentManagedLibraryRoot() != Path.Combine(sessionRoot, "managed-library") ||
            StorageMigrationService.LegacyManagedLibraryRoot() != Path.Combine(sessionRoot, "legacy-managed-library"))
        {
            throw new InvalidOperationException("All workflow storage roots must be isolated before the demo starts.");
        }
    }

    private void OpenManuscript()
    {
        try
        {
            var manuscripts = repository.Load();
            var manuscript = manuscripts.SingleOrDefault(item => item.Id == manuscriptId);
            if (manuscript is null)
            {
                RefreshSavedState("The sample manuscript has been deleted. Start a new demo process for a fresh sample.");
                return;
            }

            using var dialog = new EditManuscriptForm(manuscript, manuscripts);
            dialog.Text += " [DEMO - disposable session]";
            if (minimum)
            {
                // Details applies its own initial size after raising Shown.
                dialog.Shown += (_, _) => dialog.BeginInvoke(new Action(() => dialog.Size = dialog.MinimumSize));
            }

            var result = dialog.ShowDialog(this);
            if (dialog.DeleteRequested)
            {
                manuscripts.Remove(manuscript);
                repository.Save(manuscripts);
                RefreshSavedState("The sample was deleted from this disposable session.");
            }
            else if (result == DialogResult.OK)
            {
                repository.Save(manuscripts);
                RefreshSavedState("Saved and reloaded from the disposable repository. Reopen Details to inspect the result.");
            }
            else
            {
                RefreshSavedState("Manuscript Details was canceled. Its unsaved manuscript changes were discarded.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowSessionError(ex);
        }
    }

    private void ReloadWithErrorHandling()
    {
        try
        {
            RefreshSavedState("Reloaded the saved state from this disposable session.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowSessionError(ex);
        }
    }

    private void RefreshSavedState(string lastAction)
    {
        var manuscript = repository.Load().SingleOrDefault(item => item.Id == manuscriptId);
        openButton.Enabled = manuscript is not null;
        var text = new StringBuilder(lastAction).AppendLine().AppendLine();
        if (manuscript is not null)
        {
            text.AppendLine(manuscript.Title)
                .AppendLine($"Stage: {manuscript.CurrentStage}")
                .AppendLine($"Readiness profiles: {manuscript.ReadinessProfiles.Count}")
                .AppendLine($"Versions: {manuscript.Versions.Count}")
                .AppendLine($"Packets: {manuscript.SubmissionPackets.Count}")
                .AppendLine($"Recorded journal submissions: {manuscript.Submissions.Count}")
                .AppendLine();
            foreach (var packet in manuscript.SubmissionPackets)
            {
                var version = manuscript.Versions.FirstOrDefault(item => item.Id == packet.ManuscriptVersionId);
                var submission = manuscript.Submissions.FirstOrDefault(item => item.Id == packet.SubmissionId);
                text.AppendLine($"Packet: {packet.Label}")
                    .AppendLine($"Exact version: {version?.Label ?? "Missing version reference"}")
                    .AppendLine(submission is null
                        ? "Preparation only - no associated submission."
                        : $"Associated submission: {submission.JournalName} ({submission.ManuscriptNumber})")
                    .AppendLine();
            }
        }
        text.AppendLine("Preparing a checklist, version, or packet does not record a submission.")
            .AppendLine("This is fictional sample data. Session files remain in the displayed temporary directory when you close the demo.");
        savedState.Text = text.ToString();
    }

    private void ShowSessionError(Exception exception)
    {
        MessageBox.Show(this, "The disposable session could not complete the operation.\r\n\r\n" + exception.Message,
            "Demo operation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
