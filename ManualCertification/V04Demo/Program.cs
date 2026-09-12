using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using System.Text;

namespace PaperRoute.V04Demo;

internal static class Program
{
    private const string Usage = "PaperRoute v0.4 manual demo\n\n" +
        "Surfaces: vault (default), readiness, packet, packet-new, file, file-new, notes, submission, workflow\n" +
        "Options: --minimum, --empty (vault/readiness only), --integrity (populated vault only), --dark or --system, --help\n\n" +
        "Default surfaces discard manuscript changes when the window closes.\n" +
        "workflow saves only in a new disposable temporary session.\n" +
        "--integrity creates and retains disposable files in a unique temporary directory.";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(Usage, "PaperRoute v0.4 demo");
            return;
        }

        var surfaces = new[] { "vault", "readiness", "packet", "packet-new", "file", "file-new", "notes", "submission", "workflow" };
        var positional = args.Where(argument => !argument.StartsWith("--")).ToArray();
        var surface = positional.FirstOrDefault()?.ToLowerInvariant() ?? "vault";
        var minimum = args.Contains("--minimum", StringComparer.OrdinalIgnoreCase);
        var empty = args.Contains("--empty", StringComparer.OrdinalIgnoreCase);
        var integrity = args.Contains("--integrity", StringComparer.OrdinalIgnoreCase);
        var dark = args.Contains("--dark", StringComparer.OrdinalIgnoreCase);
        var system = args.Contains("--system", StringComparer.OrdinalIgnoreCase);
        var invalidOption = args.Any(argument => argument.StartsWith("--") &&
            !argument.Equals("--minimum", StringComparison.OrdinalIgnoreCase) &&
            !argument.Equals("--empty", StringComparison.OrdinalIgnoreCase) &&
            !argument.Equals("--integrity", StringComparison.OrdinalIgnoreCase) &&
            !argument.Equals("--dark", StringComparison.OrdinalIgnoreCase) &&
            !argument.Equals("--system", StringComparison.OrdinalIgnoreCase));

        if (positional.Length > 1 || !surfaces.Contains(surface) || invalidOption ||
            (dark && system) ||
            (empty && surface != "vault" && surface != "readiness") ||
            (integrity && (surface != "vault" || empty)))
        {
            MessageBox.Show(Usage, "Invalid demo arguments", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Change only this disposable process, never Windows or saved preferences.
        Application.SetColorMode(dark ? SystemColorMode.Dark :
            system ? SystemColorMode.System : SystemColorMode.Classic);

        DemoFixture fixture;
        try
        {
            if (surface == "workflow")
            {
                // Configure before constructing ANY sample, repository, or form.
                // Nested production dialogs inherit the same process-only roots.
                var sessionRoot = Path.Combine(Path.GetTempPath(),
                    "PaperRoute-V04-Workflow-Demo-" + Guid.NewGuid().ToString("N"));
                StorageEnvironment.ConfigureIsolatedSessionRoot(sessionRoot);
                using var launcher = new WorkflowDemoForm(sessionRoot, minimum);
                launcher.ShowDialog();
                return;
            }

            fixture = DemoFixture.Create(integrity);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show("The disposable demo fixtures could not be prepared.\r\n\r\n" + ex.Message,
                "Demo setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (empty)
        {
            fixture.Manuscript.SubmissionPackets.Clear();
            fixture.Manuscript.ReadinessProfiles.Clear();
        }

        // Instantiate the real forms without the application's startup, migration,
        // repositories, or Manuscript Details persistence boundary.
        using Form form = surface switch
        {
            "readiness" => new ManuscriptReadinessForm(fixture.Manuscript, fixture.Library),
            "packet" => new SubmissionPacketEditForm(fixture.Manuscript, fixture.Packet),
            "packet-new" => new SubmissionPacketEditForm(fixture.Manuscript, null),
            "file" => new SubmissionPacketFileEditForm(fixture.Packet, fixture.Packet.Files[0]),
            "file-new" => new SubmissionPacketFileEditForm(fixture.Packet, null),
            "notes" => new ReadinessItemNotesForm(fixture.Manuscript.ReadinessProfiles[0].Items[0]),
            "submission" => new SubmissionDetailsForm(fixture.Manuscript, fixture.Manuscript.Submissions[0]),
            _ => new SubmissionPacketVaultForm(fixture.Manuscript)
        };

        form.StartPosition = FormStartPosition.CenterScreen;
        form.Text += integrity
            ? " [DEMO - unsaved sample data; disposable integrity files]"
            : " [DEMO - unsaved sample data]";
        if (minimum)
        {
            form.Shown += (_, _) =>
            {
                // Submission Details applies its own initial size after raising Shown.
                if (surface == "submission")
                    form.BeginInvoke(new Action(() => form.Size = form.MinimumSize));
                else
                    form.Size = form.MinimumSize;
            };
        }

        // These forms use DialogResult for Save and Cancel. Hosting them modally
        // preserves their normal behavior; closing ends this disposable process.
        form.ShowDialog();
    }
}

internal sealed record DemoFixture(Manuscript Manuscript, AuthorLibraryData Library, SubmissionPacket Packet)
{
    private const string LongNotes =
        "Synthetic certification notes: confirm the title, author details, reporting checklist, and " +
        "supplementary material agree with the selected manuscript snapshot. The methods appendix " +
        "contains an extended sensitivity analysis and a table of exclusions for review.\r\n\r\n" +
        "A second reviewer requested a clearer explanation of the study setting, a revised legend " +
        "for the primary figure, and an explicit data availability statement. Check that these notes " +
        "remain readable when the window is narrowed and when Windows display scaling is increased.\r\n\r\n" +
        "This is fictional sample content. No real submission or library record is represented.";

    internal static DemoFixture Create(bool integrity = false)
    {
        var now = new DateTime(2026, 9, 11, 14, 0, 0, DateTimeKind.Utc);
        var journal = new JournalRecord
        {
            Name = "Journal of Reproducible Research and Interdisciplinary Methods",
            Publisher = "Fictional Certification Press",
            IsFavorite = true
        };
        var otherJournal = new JournalRecord { Name = "Methods and Open Scholarship (Fictional)" };
        var requirementTitles = new[]
        {
            "Prepare a blinded manuscript with identifying details removed from the text, tables, and document properties",
            "Confirm the author contribution and affiliation information",
            "Include the reporting checklist and relevant page references",
            "Prepare a data availability statement",
            "Verify figure resolution and accessible captions",
            "Check the cover letter and supplementary material"
        };

        for (var index = 0; index < requirementTitles.Length; index++)
        {
            journal.ReadinessChecklistTemplate.Add(new JournalChecklistTemplateItem
            {
                Title = requirementTitles[index],
                Category = index < 3 ? "Required submission documents" : "Supporting material",
                Description = "Review the journal-specific requirement against the exact version you plan to submit. " +
                    "Confirm the main text, supplementary files, and metadata use consistent terminology. " +
                    "The journal template may change later; this profile preserves the requirement used for this preparation cycle.",
                SortOrder = index,
                IsRequired = index != 5
            });
        }

        var manuscript = new Manuscript
        {
            Title = "ZZZ-CERT-v0.4 Reproducible research across disciplines: a long manuscript title for checking narrow windows and wrapped text",
            TargetJournal = journal.Name,
            TargetJournalId = journal.Id,
            CurrentStage = PaperStage.Submitted
        };
        var readiness = new ManuscriptReadiness
        {
            JournalId = journal.Id,
            JournalName = journal.Name,
            CreatedAtUtc = now.AddDays(-3),
            Notes = LongNotes
        };
        for (var index = 0; index < journal.ReadinessChecklistTemplate.Count; index++)
        {
            var template = journal.ReadinessChecklistTemplate[index];
            readiness.Items.Add(new ReadinessItemState
            {
                TemplateItemId = template.Id,
                Title = template.Title,
                Description = template.Description,
                Category = template.Category,
                SortOrder = template.SortOrder,
                IsRequired = template.IsRequired,
                Status = index == 1 ? ReadinessItemStatus.Complete :
                    index == 4 ? ReadinessItemStatus.NotApplicable : ReadinessItemStatus.Unresolved,
                UserNotes = index == 0 ? LongNotes : "Synthetic checklist note for this requirement."
            });
        }
        // One template item is newer than the profile, so Add New Template Requirements is testable.
        journal.ReadinessChecklistTemplate.Add(new JournalChecklistTemplateItem
        {
            Title = "Provide the updated graphical abstract",
            Description = "Synthetic requirement added after this readiness profile was created.",
            SortOrder = 6,
            IsRequired = false
        });
        manuscript.ReadinessProfiles.Add(readiness);
        manuscript.ReadinessProfiles.Add(new ManuscriptReadiness
        {
            JournalId = otherJournal.Id,
            JournalName = otherJournal.Name,
            CreatedAtUtc = now.AddDays(-7)
        });

        for (var index = 1; index <= 3; index++)
        {
            manuscript.Versions.Add(new ManuscriptVersion
            {
                Label = $"Version {index} - revised methods, complete tables, and supplementary analysis",
                CreatedDate = now.AddDays(index - 4),
                RecordedAtUtc = now.AddDays(index - 4),
                Notes = LongNotes
            });
        }
        manuscript.CurrentVersionId = manuscript.Versions[2].Id;
        var submission = new JournalSubmission
        {
            JournalId = journal.Id,
            JournalName = journal.Name,
            ManuscriptNumber = "DEMO-2026-0042",
            SubmittedDate = now.AddDays(-2),
            RecordedAtUtc = now.AddDays(-2),
            Notes = "Fictional submission included to exercise optional packet associations."
        };
        manuscript.Submissions.Add(submission);

        var packet = new SubmissionPacket
        {
            Label = "Revision package - methods clarification and supplementary sensitivity analysis",
            JournalId = journal.Id,
            JournalName = journal.Name,
            ReadinessProfileId = readiness.Id,
            ManuscriptVersionId = manuscript.Versions[2].Id,
            SubmissionId = submission.Id,
            RevisionRoundNumber = 1,
            CreatedAtUtc = now,
            Notes = LongNotes
        };
        var roles = new[]
        {
            SubmissionPacketFileRole.BlindedManuscript,
            SubmissionPacketFileRole.CoverLetter,
            SubmissionPacketFileRole.ReportingChecklist,
            SubmissionPacketFileRole.Supplement
        };
        foreach (var role in roles)
        {
            packet.Files.Add(new SubmissionPacketFile
            {
                Role = role,
                Label = $"{role} - long descriptive label for checking file lists at the minimum window width",
                StorageMode = SubmissionPacketFileStorageMode.MetadataOnly,
                Notes = LongNotes
            });
        }
        // The unique temporary path is never created or written. It supports the
        // real missing-file message and a long path without pointing at user data.
        packet.Files.Add(new SubmissionPacketFile
        {
            Role = SubmissionPacketFileRole.Figure,
            Label = "Missing linked figure - synthetic path",
            StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
            LocalFilePath = Path.Combine(Path.GetTempPath(), "PaperRoute-V04-Demo-" + Guid.NewGuid().ToString("N"),
                "supplementary-material", "figure-01-study-flow-and-sensitivity-analysis.png"),
            OriginalFileName = "figure-01-study-flow-and-sensitivity-analysis.png",
            Notes = LongNotes
        });
        if (integrity)
        {
            CreateIntegrityFiles(packet);
        }
        manuscript.SubmissionPackets.Add(packet);
        manuscript.SubmissionPackets.Add(new SubmissionPacket
        {
            Label = "Initial preparation - not associated with a submission",
            JournalId = journal.Id,
            JournalName = journal.Name,
            ReadinessProfileId = readiness.Id,
            ManuscriptVersionId = manuscript.Versions[0].Id,
            CreatedAtUtc = now.AddDays(-4),
            Notes = "Synthetic preparation-only packet with no files."
        });

        var library = new AuthorLibraryData();
        library.Journals.Add(journal);
        library.Journals.Add(otherJournal);
        return new DemoFixture(manuscript, library, packet);
    }

    private static void CreateIntegrityFiles(SubmissionPacket packet)
    {
        // Only this explicit demo mode writes files. Every path is a fixed filename
        // under this run's unique temp directory; no application repository is used.
        var directory = Path.Combine(Path.GetTempPath(),
            "PaperRoute-V04-Integrity-Demo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        packet.Label = "Integrity checks - disposable synthetic files";
        packet.Notes = $"Disposable fixture directory: {directory}\r\n\r\n" + LongNotes;
        packet.Files.Clear();

        SubmissionPacketFile AddFixture(string name, string label, SubmissionPacketFileRole role,
            bool captureBaseline)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, "abc", encoding);
            var file = new SubmissionPacketFile
            {
                Role = role,
                Label = label,
                StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                LocalFilePath = path,
                OriginalFileName = name,
                Notes = $"Disposable integrity fixture: {path}\r\n\r\n" + LongNotes
            };
            if (captureBaseline)
            {
                var result = SubmissionPacketIntegrityService.CaptureBaseline(file);
                if (result.Status != PacketFileIntegrityStatus.Unchanged)
                {
                    throw new InvalidOperationException($"Could not record the fixture fingerprint: {path}");
                }
            }
            packet.Files.Add(file);
            return file;
        }

        AddFixture("no-baseline.txt", "No fingerprint - record one to compare later",
            SubmissionPacketFileRole.BlindedManuscript, captureBaseline: false);
        AddFixture("unchanged.txt", "Unchanged - original abc content",
            SubmissionPacketFileRole.CoverLetter, captureBaseline: true);
        var changed = AddFixture("changed.txt", "Changed - same size, different content",
            SubmissionPacketFileRole.Figure, captureBaseline: true);
        // Changing abc to xyz preserves the three-byte size, so checking length alone
        // cannot detect this fixture's change. Production integrity code only reads it.
        File.WriteAllText(changed.LocalFilePath, "xyz", encoding);
        var missing = AddFixture("missing.txt", "Missing - captured fixture removed during setup",
            SubmissionPacketFileRole.Supplement, captureBaseline: true);
        File.Delete(missing.LocalFilePath);
        packet.Files.Add(new SubmissionPacketFile
        {
            Role = SubmissionPacketFileRole.ReportingChecklist,
            Label = "Metadata only - no local file",
            StorageMode = SubmissionPacketFileStorageMode.MetadataOnly,
            Notes = LongNotes
        });

        var expectedStatuses = new[]
        {
            PacketFileIntegrityStatus.NotRecorded,
            PacketFileIntegrityStatus.Unchanged,
            PacketFileIntegrityStatus.Changed,
            PacketFileIntegrityStatus.Missing,
            PacketFileIntegrityStatus.MetadataOnly
        };
        for (var index = 0; index < packet.Files.Count; index++)
        {
            if (SubmissionPacketIntegrityService.Verify(packet.Files[index]).Status != expectedStatuses[index])
            {
                throw new InvalidOperationException(
                    $"The fixture did not produce its expected integrity state: {packet.Files[index].Label}");
            }
        }

        // Leave the directory in place for inspection after the demo closes. It is
        // deliberately never registered as managed library content or auto-deleted.
    }
}
