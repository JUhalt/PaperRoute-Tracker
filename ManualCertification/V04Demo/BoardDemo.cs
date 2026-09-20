using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using System.Reflection;
using System.Text.Json;

namespace PaperRoute.V04Demo;

internal static class BoardDemo
{
    internal static void RecordLayoutEvidence(ManuscriptPipeline.Form1 board, string sessionRoot)
    {
        // Harness-only diagnostics for native scaling and maximize/restore checks.
        // Delay until the resize settles; keep the last observations in this run's directory.
        var observations = new List<object>();
        var timer = new System.Windows.Forms.Timer { Interval = 300 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var shelves = new[] { "pipelinePanel", "publishedPanel", "fileDrawerPanel" }.Select(name =>
            {
                var shelf = (FlowLayoutPanel)typeof(ManuscriptPipeline.Form1)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(board)!;
                return new
                {
                    name, shelf.ClientSize, shelf.DisplayRectangle, shelf.AutoScrollPosition,
                    horizontal = shelf.HorizontalScroll.Visible, vertical = shelf.VerticalScroll.Visible,
                    cards = shelf.Controls.Cast<Control>().Select(card => new { card.Bounds, card.Margin }).ToArray()
                };
            }).ToArray();
            observations.Add(new { time = DateTime.UtcNow, board.DeviceDpi, board.WindowState, board.Bounds, board.ClientSize, shelves });
            if (observations.Count > 30) observations.RemoveAt(0);
            File.WriteAllText(Path.Combine(sessionRoot, "board-layout.json"),
                JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        };
        board.Shown += (_, _) => timer.Start();
        board.SizeChanged += (_, _) => { timer.Stop(); timer.Start(); };
        board.FormClosed += (_, _) => timer.Dispose();
    }

    internal static void CreateSamples(string sessionRoot)
    {
        // Verify every root before constructing any repository or the real board.
        if (!StorageEnvironment.IsIsolatedSession ||
            StorageMigrationService.CurrentDataRoot() != Path.Combine(sessionRoot, "current-data") ||
            StorageMigrationService.LegacyDataRoot() != Path.Combine(sessionRoot, "legacy-data") ||
            StorageMigrationService.CurrentManagedLibraryRoot() != Path.Combine(sessionRoot, "managed-library") ||
            StorageMigrationService.LegacyManagedLibraryRoot() != Path.Combine(sessionRoot, "legacy-managed-library"))
            throw new InvalidOperationException("All board demo storage roots must be isolated.");

        new AppSettingsService().Save(new AppSettings
        {
            CheckForUpdatesAutomatically = false,
            ReminderNotificationsEnabled = false
        });
        var manuscripts = new List<Manuscript>();
        foreach (var location in Enum.GetValues<ManuscriptLocation>())
        {
            for (var index = 1; index <= 5; index++)
            {
                var manuscript = new Manuscript
                {
                    Title = $"DEMO {location} {index}: Reproducible research across disciplines and journal-specific manuscript preparation",
                    Location = location,
                    CurrentStage = location == ManuscriptLocation.Published ? PaperStage.Published : PaperStage.Draft,
                    StageEnteredDate = DateTime.Today.AddDays(-index),
                    TargetJournal = "Fictional Journal of Reproducible Research and Interdisciplinary Methods"
                };
                manuscript.History.Add(new HistoryEvent
                {
                    Stage = manuscript.CurrentStage,
                    EventDate = manuscript.StageEnteredDate,
                    Note = "Fictional sample for testing shelf layout and card actions."
                });
                manuscripts.Add(manuscript);
            }
        }
        new AuthorLibraryRepository().Save(new AuthorLibraryData());
        new ManuscriptRepository().Save(manuscripts);
    }
}
